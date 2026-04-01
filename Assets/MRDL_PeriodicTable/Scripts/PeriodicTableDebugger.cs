using System.Collections;
using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.Utilities;
using Microsoft.MixedReality.Toolkit.Utilities.Solvers;
using UnityEngine;

namespace HoloToolkit.MRDL.PeriodicTable
{
    /// <summary>
    /// 주기율표 사라짐 버그 진단용 디버거.
    /// SceneObject / TableParent 활성화 상태와 조작 이벤트를 매 초 로그에 기록합니다.
    /// </summary>
    public class PeriodicTableDebugger : MonoBehaviour
    {
        [Header("Monitor Targets")]
        public GameObject SceneObject;       // 주기율표 루트 (SceneObject)
        public GameObject TableParent;       // 원소 부모 (TableParent)
        public GameObject MenuContent;       // 핸드메뉴 콘텐츠

        private bool lastSceneObjectActive  = true;
        private bool lastTableParentActive  = true;
        private bool lastMenuContentActive  = true;

        private HandConstraintPalmUp palmConstraint;

        private void Start()
        {
            // HandConstraintPalmUp 컴포넌트를 씬에서 찾아 이벤트 구독
            palmConstraint = FindObjectOfType<HandConstraintPalmUp>();
            if (palmConstraint != null)
            {
                palmConstraint.OnFirstHandDetected.AddListener(OnFirstHandDetected);
                palmConstraint.OnLastHandLost.AddListener(OnLastHandLost);
                Debug.Log("[PTDebug] HandConstraintPalmUp 이벤트 구독 완료.");
            }
            else
            {
                Debug.LogWarning("[PTDebug] HandConstraintPalmUp을 찾을 수 없습니다!");
            }

            StartCoroutine(MonitorActiveStates());
            LogHandJoints();
        }

        private void Update()
        {
            // 활성화 상태 변화 실시간 감지
            if (SceneObject != null && SceneObject.activeSelf != lastSceneObjectActive)
            {
                Debug.LogError($"[PTDebug] !! SceneObject active 변경: {lastSceneObjectActive} → {SceneObject.activeSelf}  Time={Time.time:F2}");
                lastSceneObjectActive = SceneObject.activeSelf;
                LogCallStack("SceneObject");
            }
            if (TableParent != null && TableParent.activeSelf != lastTableParentActive)
            {
                Debug.LogError($"[PTDebug] !! TableParent active 변경: {lastTableParentActive} → {TableParent.activeSelf}  Time={Time.time:F2}");
                lastTableParentActive = TableParent.activeSelf;
                LogCallStack("TableParent");
            }
            if (MenuContent != null && MenuContent.activeSelf != lastMenuContentActive)
            {
                Debug.LogWarning($"[PTDebug] MenuContent active 변경: {lastMenuContentActive} → {MenuContent.activeSelf}  Time={Time.time:F2}");
                lastMenuContentActive = MenuContent.activeSelf;
            }
        }

        private IEnumerator MonitorActiveStates()
        {
            while (true)
            {
                yield return new WaitForSeconds(2f);
                string scenePos   = SceneObject  != null ? SceneObject.transform.position.ToString("F2")   : "null";
                string sceneRot   = SceneObject  != null ? SceneObject.transform.rotation.ToString("F2")   : "null";
                string tableWorld = TableParent  != null ? TableParent.transform.position.ToString("F2")   : "null";
                string tableLocal = TableParent  != null ? TableParent.transform.localPosition.ToString("F2") : "null";
                string tableLocalRot = TableParent != null ? TableParent.transform.localRotation.ToString("F2") : "null";

                Debug.Log($"[PTDebug] 상태 — SceneObject pos={scenePos} rot={sceneRot} | " +
                          $"TableParent world={tableWorld} local={tableLocal} localRot={tableLocalRot} | " +
                          $"active: SO={SceneObject?.activeSelf} TP={TableParent?.activeSelf} MC={MenuContent?.activeSelf}");

                // Hand joint 상태 로그
                LogHandJoints();
            }
        }

        private void LogHandJoints()
        {
            var inputSystem = CoreServices.InputSystem;
        if (inputSystem == null) return;
        foreach (var controller in inputSystem.DetectedControllers)
            {
                if (controller is IMixedRealityHand hand)
                {
                    bool hasPalm  = hand.TryGetJoint(TrackedHandJoint.Palm,  out var palmPose);
                    bool hasWrist = hand.TryGetJoint(TrackedHandJoint.Wrist, out var wristPose);
                    Debug.Log($"[PTDebug] Hand {controller.ControllerHandedness}: " +
                              $"Palm={hasPalm}({(hasPalm  ? palmPose.Position.ToString("F2")  : "N/A")})  " +
                              $"Wrist={hasWrist}({(hasWrist ? wristPose.Position.ToString("F2") : "N/A")})");
                }
            }
        }

        private void LogCallStack(string target)
        {
            Debug.LogError($"[PTDebug] {target} 변경 스택:\n{System.Environment.StackTrace}");
        }

        private void OnFirstHandDetected()
        {
            Debug.Log($"[PTDebug] onFirstHandDetected 발동  Time={Time.time:F2}  MenuContent={MenuContent?.activeSelf}");
        }

        private void OnLastHandLost()
        {
            Debug.LogWarning($"[PTDebug] onLastHandLost 발동  Time={Time.time:F2}  MenuContent={MenuContent?.activeSelf}  SceneObject={SceneObject?.activeSelf}");
        }

        private void OnDestroy()
        {
            if (palmConstraint != null)
            {
                palmConstraint.OnFirstHandDetected.RemoveListener(OnFirstHandDetected);
                palmConstraint.OnLastHandLost.RemoveListener(OnLastHandLost);
            }
        }
    }
}
