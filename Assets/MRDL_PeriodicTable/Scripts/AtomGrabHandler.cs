using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.Utilities;
using UnityEngine;

namespace HoloToolkit.MRDL.PeriodicTable
{
    /// <summary>
    /// HandJointUtils로 직접 핀치를 감지해 원소를 잡아 이동합니다.
    /// MRTK의 ManipulationHandler와 충돌하여 포인터가 먹통이 되는 현상을 방지하기 위해
    /// 내부적으로 MRTK 조작 컴포넌트를 제어합니다.
    /// </summary>
    public class AtomGrabHandler : MonoBehaviour
    {
        private const float PinchThreshold = 0.04f;
        private const float GrabRadius = 0.15f;

        private static int grabCount = 0;
        public static bool IsAnyGrabActive => grabCount > 0;

        public bool IsGrabbing => isGrabbing;

        private PresentToPlayer present;
        private Element element;
        private Animator animator;
        private bool grabEnabled = false;

        private bool isGrabbing = false;
        private Handedness grabbingHand = Handedness.None;
        private Vector3 grabOffset;
        private int pinchLostFrames = 0;
        private const int PinchLostThreshold = 6;

        void Start()
        {
            present = GetComponent<PresentToPlayer>();
            element = GetComponent<Element>();
            animator = GetComponent<Animator>();

            // [핵심] MRTK의 기본 조작 컴포넌트들이 우리 로직과 충돌하여 포인터를 프리징시키는 것 방지
            // MoleculeObject에 붙은 MRTK 컴포넌트들을 찾아 비활성화합니다.
            Transform molecule = transform.Find("MoleculeObject");
            if (molecule != null)
            {
                var comps = molecule.GetComponents<MonoBehaviour>();
                foreach (var c in comps)
                {
                    if (c == null) continue;
                    string typeName = c.GetType().Name;
                    if (typeName.Contains("ManipulationHandler") || 
                        typeName.Contains("ObjectManipulator") || 
                        typeName.Contains("NearInteractionGrabbable"))
                    {
                        c.enabled = false;
                        Debug.Log($"[AtomGrab] Disabled MRTK component {typeName} on {molecule.name} to prevent freeze.");
                    }
                }
            }
        }

        void Update()
        {
            // 손 유실 시 강제 해제 (Fail-safe)
            if (isGrabbing && !HandJointUtils.TryGetJointPose(TrackedHandJoint.Palm, grabbingHand, out _))
            {
                pinchLostFrames++;
                if (pinchLostFrames >= PinchLostThreshold * 5)
                {
                    StopGrab();
                    return;
                }
            }

            bool shouldGrab = present != null && present.Presenting && present.InPosition;

            if (shouldGrab && !grabEnabled)
            {
                grabEnabled = true;
            }
            else if (!shouldGrab && grabEnabled)
            {
                if (!isGrabbing)
                {
                    StopGrab();
                    grabEnabled = false;
                }
            }

            if (!grabEnabled) return;

            if (isGrabbing)
            {
                element?.SetActiveElement();
                if (animator != null) animator.speed = 0; // 애니메이션 중지하여 사라짐 방지

                if (HandJointUtils.TryGetJointPose(TrackedHandJoint.Palm, grabbingHand, out MixedRealityPose palmPose))
                {
                    transform.position = palmPose.Position + grabOffset;
                }

                if (!IsPinching(grabbingHand))
                {
                    pinchLostFrames++;
                    if (pinchLostFrames >= PinchLostThreshold)
                    {
                        StopGrab();
                    }
                }
                else
                {
                    pinchLostFrames = 0;
                }
            }
            else
            {
                TryStartGrab(Handedness.Left);
                if (!isGrabbing)
                    TryStartGrab(Handedness.Right);
            }
        }

        private void TryStartGrab(Handedness hand)
        {
            if (!HandJointUtils.TryGetJointPose(TrackedHandJoint.Palm, hand, out MixedRealityPose palmPose)) return;
            if (Vector3.Distance(palmPose.Position, transform.position) > GrabRadius) return;
            if (!IsPinching(hand)) return;

            grabbingHand = hand;
            grabOffset = transform.position - palmPose.Position;
            
            if (!isGrabbing)
            {
                isGrabbing = true;
                grabCount++;
            }
            
            pinchLostFrames = 0;
            element?.SetActiveElement();
            if (animator != null) animator.speed = 0;
            Debug.Log($"[AtomGrab] STARTED grab on {gameObject.name}");
        }

        private bool IsPinching(Handedness hand)
        {
            if (!HandJointUtils.TryGetJointPose(TrackedHandJoint.IndexTip, hand, out MixedRealityPose indexPose)) return false;
            if (!HandJointUtils.TryGetJointPose(TrackedHandJoint.ThumbTip, hand, out MixedRealityPose thumbPose)) return false;
            return Vector3.Distance(indexPose.Position, thumbPose.Position) < PinchThreshold;
        }

        private void StopGrab()
        {
            if (isGrabbing)
            {
                grabCount = Mathf.Max(0, grabCount - 1);
                if (animator != null) animator.speed = 1;
                Debug.Log($"[AtomGrab] STOPPED grab on {gameObject.name}");
            }
            isGrabbing = false;
            grabbingHand = Handedness.None;
            pinchLostFrames = 0;
        }

        void OnDisable() { StopGrab(); }
    }
}