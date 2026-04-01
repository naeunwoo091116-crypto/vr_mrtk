// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.Utilities;
using Microsoft.MixedReality.Toolkit.XRSDK.Input;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Hands;
using MRTKHandedness = Microsoft.MixedReality.Toolkit.Utilities.Handedness;

namespace Microsoft.MixedReality.Toolkit.XRSDK.OpenXR
{
    /// <summary>
    /// Meta Quest OpenXR hand tracking implementation for MRTK.
    /// Uses com.unity.xr.hands (XRHandSubsystem) which properly supports
    /// the XR_EXT_hand_tracking OpenXR extension on Meta Quest.
    /// </summary>
    [MixedRealityController(
        SupportedControllerType.ArticulatedHand,
        new[] { MRTKHandedness.Left, MRTKHandedness.Right })]
    public class MetaQuestArticulatedHand : GenericXRSDKController, IMixedRealityHand
    {
        public MetaQuestArticulatedHand(
            TrackingState trackingState,
            MRTKHandedness controllerHandedness,
            IMixedRealityInputSource inputSource = null,
            MixedRealityInteractionMapping[] interactions = null)
            : base(trackingState, controllerHandedness, inputSource, interactions,
                  new ArticulatedHandDefinition(inputSource, controllerHandedness))
        { }

        private ArticulatedHandDefinition handDefinition;
        private ArticulatedHandDefinition HandDefinition =>
            handDefinition ?? (handDefinition = Definition as ArticulatedHandDefinition);

        protected readonly Dictionary<TrackedHandJoint, MixedRealityPose> unityJointPoses =
            new Dictionary<TrackedHandJoint, MixedRealityPose>();

        private static readonly List<XRHandSubsystem> handSubsystems = new List<XRHandSubsystem>();

        private Vector3 currentPointerPosition = Vector3.zero;
        private Quaternion currentPointerRotation = Quaternion.identity;
        private MixedRealityPose currentPointerPose = MixedRealityPose.ZeroIdentity;

        /// <inheritdoc />
        public bool TryGetJoint(TrackedHandJoint joint, out MixedRealityPose pose) =>
            unityJointPoses.TryGetValue(joint, out pose);

        /// <inheritdoc />
        public override bool IsInPointingPose => HandDefinition?.IsInPointingPose ?? false;

        /// <summary>
        /// Overrides the base six-dof update to use XRHandSubsystem for hand tracking
        /// instead of CommonUsages.devicePosition/deviceRotation.
        /// Sets TrackingState, poses, and updates SixDof interaction mappings.
        /// </summary>
        protected override void UpdateSixDofData(InputDevice inputDevice)
        {
            var lastState = TrackingState;
            LastControllerPose = CurrentControllerPose;

            bool isHandTracked = UpdateHandData();

            IsPositionAvailable = IsRotationAvailable = isHandTracked;
            IsPositionApproximate = false;
            TrackingState = isHandTracked ? TrackingState.Tracked : TrackingState.NotTracked;

            if (isHandTracked &&
                unityJointPoses.TryGetValue(TrackedHandJoint.Wrist, out MixedRealityPose wristPose) &&
                IsValidPose(wristPose))
            {
                CurrentControllerPosition = wristPose.Position;
                CurrentControllerRotation = wristPose.Rotation;
                CurrentControllerPose.Position = wristPose.Position;
                CurrentControllerPose.Rotation = wristPose.Rotation;
            }

            if (lastState != TrackingState)
            {
                CoreServices.InputSystem?.RaiseSourceTrackingStateChanged(InputSource, this, TrackingState);
            }

            if (TrackingState == Microsoft.MixedReality.Toolkit.TrackingState.Tracked &&
                LastControllerPose != CurrentControllerPose)
            {
                CoreServices.InputSystem?.RaiseSourcePoseChanged(InputSource, this, CurrentControllerPose);
            }

            for (int i = 0; i < Interactions?.Length; i++)
            {
                if (Interactions[i].AxisType == AxisType.SixDof)
                {
                    UpdatePoseData(Interactions[i], inputDevice);
                }
            }
        }

        /// <inheritdoc />
        protected override void UpdatePoseData(MixedRealityInteractionMapping interactionMapping, InputDevice inputDevice)
        {
            switch (interactionMapping.InputType)
            {
                case DeviceInputType.IndexFinger:
                    HandDefinition?.UpdateCurrentIndexPose(interactionMapping);
                    break;

                case DeviceInputType.SpatialPointer:
                    // Try OpenXR/MetaHandTrackingAim aim pose first, fall back to wrist joint.
                    if (inputDevice.TryGetFeatureValue(CustomUsages.PointerPosition, out currentPointerPosition) &&
                        inputDevice.TryGetFeatureValue(CustomUsages.PointerRotation, out currentPointerRotation) &&
                        IsValidVector3(currentPointerPosition) &&
                        IsValidQuaternion(currentPointerRotation))
                    {
                        var resultPos = MixedRealityPlayspace.TransformPoint(currentPointerPosition);
                        var resultRot = MixedRealityPlayspace.Rotation * currentPointerRotation;
                        // Validate multiplication result — MixedRealityPlayspace.Rotation itself can be degenerate
                        if (IsValidVector3(resultPos) && IsValidQuaternion(resultRot))
                        {
                            currentPointerPose.Position = resultPos;
                            currentPointerPose.Rotation = resultRot;
                        }
                    }
                    else if (unityJointPoses.TryGetValue(TrackedHandJoint.Wrist, out MixedRealityPose wristPose) &&
                             IsValidPose(wristPose))
                    {
                        currentPointerPose.Position = wristPose.Position;
                        currentPointerPose.Rotation = wristPose.Rotation;
                    }

                    interactionMapping.PoseData = currentPointerPose;
                    if (interactionMapping.Changed)
                    {
                        CoreServices.InputSystem?.RaisePoseInputChanged(
                            InputSource, ControllerHandedness,
                            interactionMapping.MixedRealityInputAction, interactionMapping.PoseData);
                    }
                    break;

                default:
                    base.UpdatePoseData(interactionMapping, inputDevice);
                    break;
            }
        }

        /// <inheritdoc />
        protected override void UpdateButtonData(MixedRealityInteractionMapping interactionMapping, InputDevice inputDevice)
        {
            switch (interactionMapping.InputType)
            {
                case DeviceInputType.Select:
                    interactionMapping.BoolData = HandDefinition?.IsPinching ?? false;
                    if (interactionMapping.Changed)
                    {
                        if (interactionMapping.BoolData)
                            CoreServices.InputSystem?.RaiseOnInputDown(InputSource, ControllerHandedness, interactionMapping.MixedRealityInputAction);
                        else
                            CoreServices.InputSystem?.RaiseOnInputUp(InputSource, ControllerHandedness, interactionMapping.MixedRealityInputAction);
                    }
                    break;

                default:
                    base.UpdateButtonData(interactionMapping, inputDevice);
                    break;
            }
        }

        /// <inheritdoc />
        protected override void UpdateSingleAxisData(MixedRealityInteractionMapping interactionMapping, InputDevice inputDevice)
        {
            switch (interactionMapping.InputType)
            {
                case DeviceInputType.TriggerPress:
                    bool isPinching = HandDefinition?.IsPinching ?? false;
                    interactionMapping.BoolData = isPinching;
                    if (interactionMapping.Changed)
                    {
                        if (isPinching)
                            CoreServices.InputSystem?.RaiseOnInputDown(InputSource, ControllerHandedness, interactionMapping.MixedRealityInputAction);
                        else
                            CoreServices.InputSystem?.RaiseOnInputUp(InputSource, ControllerHandedness, interactionMapping.MixedRealityInputAction);
                    }
                    break;

                default:
                    base.UpdateSingleAxisData(interactionMapping, inputDevice);
                    break;
            }
        }

        private bool UpdateHandData()
        {
            SubsystemManager.GetSubsystems(handSubsystems);
            if (handSubsystems.Count == 0) return false;

            var handSubsystem = handSubsystems[0];
            if (!handSubsystem.running) return false;

            XRHand hand = ControllerHandedness == MRTKHandedness.Left
                ? handSubsystem.leftHand
                : handSubsystem.rightHand;

            if (!hand.isTracked)
            {
                // Clear하지 않고 마지막 유효 관절 데이터를 유지.
                // Clear 후 return 하면 SpatialPointer 폴백(Wrist)도 지워져
                // currentPointerPose가 초기값 (0,0,0)으로 남아
                // ObjectManipulator가 SceneObject를 원점으로 날려버리는 버그 방지.
                // Palm 폴백도 여기서 실행해 HandConstraintPalmUp 경고 제거.
                if (!unityJointPoses.ContainsKey(TrackedHandJoint.Palm) &&
                    unityJointPoses.TryGetValue(TrackedHandJoint.Wrist, out MixedRealityPose wristForPalm))
                {
                    unityJointPoses[TrackedHandJoint.Palm] = wristForPalm;
                }
                return false;
            }

            for (int i = XRHandJointID.BeginMarker.ToIndex();
                     i < XRHandJointID.EndMarker.ToIndex(); i++)
            {
                XRHandJointID jointId = XRHandJointIDUtility.FromIndex(i);
                XRHandJoint joint = hand.GetJoint(jointId);

                if (joint.TryGetPose(out Pose pose) &&
                    IsValidVector3(pose.position) &&
                    IsValidQuaternion(pose.rotation))
                {
                    TrackedHandJoint mrtkJoint = ConvertToTrackedHandJoint(jointId);
                    if (mrtkJoint != TrackedHandJoint.None)
                    {
                        var worldPos = MixedRealityPlayspace.TransformPoint(pose.position);
                        var worldRot = MixedRealityPlayspace.Rotation * pose.rotation;
                        if (IsValidVector3(worldPos) && IsValidQuaternion(worldRot))
                        {
                            unityJointPoses[mrtkJoint] = new MixedRealityPose(worldPos, worldRot);
                        }
                    }
                }
            }

            // Palm joint fallback: Meta Quest OpenXR sometimes does not report Palm pose data.
            // Use Wrist as fallback so HandConstraintPalmUp can function correctly.
            if (!unityJointPoses.ContainsKey(TrackedHandJoint.Palm) &&
                unityJointPoses.TryGetValue(TrackedHandJoint.Wrist, out MixedRealityPose wristFallback))
            {
                unityJointPoses[TrackedHandJoint.Palm] = wristFallback;
            }

            HandDefinition?.UpdateHandJoints(unityJointPoses);
            return true;
        }

        private static bool IsValidVector3(Vector3 v) =>
            !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z) &&
            !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);

        // (0,0,0,0) 제로 쿼터니언은 벡터 회전 시 NaN을 유발하므로 magnitude > 0 확인
        private static bool IsValidQuaternion(Quaternion q) =>
            !float.IsNaN(q.x) && !float.IsNaN(q.y) && !float.IsNaN(q.z) && !float.IsNaN(q.w) &&
            !float.IsInfinity(q.x) && !float.IsInfinity(q.y) && !float.IsInfinity(q.z) && !float.IsInfinity(q.w) &&
            (q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w) > 0.0001f;

        private static bool IsValidPose(MixedRealityPose pose) =>
            IsValidVector3(pose.Position) && IsValidQuaternion(pose.Rotation);

        private static TrackedHandJoint ConvertToTrackedHandJoint(XRHandJointID jointId)
        {
            switch (jointId)
            {
                case XRHandJointID.Palm:               return TrackedHandJoint.Palm;
                case XRHandJointID.Wrist:              return TrackedHandJoint.Wrist;

                case XRHandJointID.ThumbMetacarpal:    return TrackedHandJoint.ThumbMetacarpalJoint;
                case XRHandJointID.ThumbProximal:      return TrackedHandJoint.ThumbProximalJoint;
                case XRHandJointID.ThumbDistal:        return TrackedHandJoint.ThumbDistalJoint;
                case XRHandJointID.ThumbTip:           return TrackedHandJoint.ThumbTip;

                case XRHandJointID.IndexMetacarpal:    return TrackedHandJoint.IndexMetacarpal;
                case XRHandJointID.IndexProximal:      return TrackedHandJoint.IndexKnuckle;
                case XRHandJointID.IndexIntermediate:  return TrackedHandJoint.IndexMiddleJoint;
                case XRHandJointID.IndexDistal:        return TrackedHandJoint.IndexDistalJoint;
                case XRHandJointID.IndexTip:           return TrackedHandJoint.IndexTip;

                case XRHandJointID.MiddleMetacarpal:   return TrackedHandJoint.MiddleMetacarpal;
                case XRHandJointID.MiddleProximal:     return TrackedHandJoint.MiddleKnuckle;
                case XRHandJointID.MiddleIntermediate: return TrackedHandJoint.MiddleMiddleJoint;
                case XRHandJointID.MiddleDistal:       return TrackedHandJoint.MiddleDistalJoint;
                case XRHandJointID.MiddleTip:          return TrackedHandJoint.MiddleTip;

                case XRHandJointID.RingMetacarpal:     return TrackedHandJoint.RingMetacarpal;
                case XRHandJointID.RingProximal:       return TrackedHandJoint.RingKnuckle;
                case XRHandJointID.RingIntermediate:   return TrackedHandJoint.RingMiddleJoint;
                case XRHandJointID.RingDistal:         return TrackedHandJoint.RingDistalJoint;
                case XRHandJointID.RingTip:            return TrackedHandJoint.RingTip;

                case XRHandJointID.LittleMetacarpal:   return TrackedHandJoint.PinkyMetacarpal;
                case XRHandJointID.LittleProximal:     return TrackedHandJoint.PinkyKnuckle;
                case XRHandJointID.LittleIntermediate: return TrackedHandJoint.PinkyMiddleJoint;
                case XRHandJointID.LittleDistal:       return TrackedHandJoint.PinkyDistalJoint;
                case XRHandJointID.LittleTip:          return TrackedHandJoint.PinkyTip;

                default:                               return TrackedHandJoint.None;
            }
        }
    }
}
