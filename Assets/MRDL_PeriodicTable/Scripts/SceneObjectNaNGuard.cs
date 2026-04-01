using System.Collections;
using System.Collections.Generic;
using Microsoft.MixedReality.Toolkit.UI;
using UnityEngine;

namespace HoloToolkit.MRDL.PeriodicTable
{
    /// <summary>
    /// Transform position/rotation/scale NaN 감지 → 로컬 공간에서 즉시 복원 + 진행 중인 grab 강제 취소.
    /// localPosition/localRotation/localScale을 사용하여 부모 NaN과의 독립성 보장.
    /// DefaultExecutionOrder(32000) 으로 모든 MRTK LateUpdate 이후 실행 보장.
    ///
    /// 핸드 트래킹 양손 조작 시 twoHandedManipulationType에 Scale이 포함되어 있으면
    /// 두 손이 가까워질 때 scale이 0으로 붕괴할 수 있습니다.
    /// scale 보호 로직이 이를 감지하고 즉시 복원합니다.
    /// </summary>
    [DefaultExecutionOrder(32000)]
    public class SceneObjectNaNGuard : MonoBehaviour
    {
        // 이 값 이하로 scale이 작아지면 비정상으로 간주
        private const float MinValidScale = 0.001f;

        // 한 프레임에 이 거리(m) 이상 이동하면 포인터 오작동으로 인한 순간이동으로 간주.
        // (핸드트래킹 끊김 시 SpatialPointer가 (0,0,0)을 참조해 SceneObject가 튀는 현상 방지)
        private const float MaxDeltaPerFrame = 0.5f;

        private Vector3    lastValidLocalPosition;
        private Quaternion lastValidLocalRotation;
        private Vector3    lastValidLocalScale;
        private bool       wasNaN;

        private void Awake()
        {
            lastValidLocalPosition = transform.localPosition;
            lastValidLocalRotation = transform.localRotation;
            lastValidLocalScale    = transform.localScale;
        }

        private void LateUpdate()
        {
            Vector3    pos   = transform.localPosition;
            Quaternion rot   = transform.localRotation;
            Vector3    scale = transform.localScale;

            bool posNaN = float.IsNaN(pos.x) || float.IsNaN(pos.y) || float.IsNaN(pos.z)
                       || float.IsInfinity(pos.x) || float.IsInfinity(pos.y) || float.IsInfinity(pos.z);

            bool rotBad = float.IsNaN(rot.x) || float.IsNaN(rot.y) || float.IsNaN(rot.z) || float.IsNaN(rot.w)
                       || (rot.x == 0f && rot.y == 0f && rot.z == 0f && rot.w == 0f);

            // scale이 0 이하 또는 NaN이면 비정상 (핸드트래킹 양손 오인식으로 인한 scale 붕괴 포함)
            bool scaleBad = float.IsNaN(scale.x) || float.IsNaN(scale.y) || float.IsNaN(scale.z)
                         || scale.x <= MinValidScale || scale.y <= MinValidScale || scale.z <= MinValidScale;

            // 한 프레임 이동 거리가 MaxDeltaPerFrame 초과 = 포인터 (0,0,0) 오작동으로 인한 순간이동.
            // 핸드트래킹이 끊겼다 복귀할 때 SpatialPointer 초기값(0,0,0)을 참조해
            // SceneObject가 한 프레임에 수 미터씩 튀는 현상을 차단.
            bool posTeleport = !posNaN && Vector3.Distance(pos, lastValidLocalPosition) > MaxDeltaPerFrame;

            if (posNaN || rotBad || scaleBad || posTeleport)
            {
                // 로컬 공간에서 복원 — 부모 transform에 의존하지 않음
                transform.localPosition = lastValidLocalPosition;
                transform.localRotation = lastValidLocalRotation;
                transform.localScale    = lastValidLocalScale;

                if (!wasNaN)
                {
                    string reason = posNaN    ? "NaN위치"
                                  : rotBad    ? "NaN회전"
                                  : scaleBad  ? "비정상스케일"
                                              : $"순간이동({Vector3.Distance(pos, lastValidLocalPosition):F2}m)";
                    Debug.LogWarning($"[NaNGuard] {reason} 감지 [{gameObject.name}]  grab 취소");
                    StartCoroutine(CancelAndResumeManipulators());
                }
                wasNaN = true;
                return;
            }

            wasNaN = false;
            lastValidLocalPosition = pos;
            lastValidLocalRotation = rot;
            lastValidLocalScale    = scale;
        }

        private IEnumerator CancelAndResumeManipulators()
        {
            // 이 오브젝트와 모든 자식의 ObjectManipulator를 끊어서 grab 상태 리셋
            var manipulators = new List<ObjectManipulator>();
            GetComponentsInChildren(true, manipulators);

            foreach (var m in manipulators)
                m.enabled = false;

            // 0.5초 대기: 핀치를 완전히 해제할 시간을 확보
            yield return new WaitForSeconds(0.5f);

            foreach (var m in manipulators)
                m.enabled = true;
        }
    }
}
