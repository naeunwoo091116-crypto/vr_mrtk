// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.Utilities;
using Microsoft.MixedReality.Toolkit.XRSDK.Input;
using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.XR;

namespace Microsoft.MixedReality.Toolkit.XRSDK.OpenXR
{
    /// <summary>
    /// OpenXR + XR SDK hand tracking implementation for Meta Quest 3.
    /// Uses Unity's Hand/Bone API (XR_EXT_hand_tracking) via CommonUsages.handData,
    /// and XR_MSFT_hand_interaction aim pose via CustomUsages.PointerPosition/PointerRotation.
    /// </summary>
    [MixedRealityController(
        SupportedControllerType.ArticulatedHand,
        new[] { Utilities.Handedness.Left, Utilities.Handedness.Right })]
    public class MetaQuestHand : GenericXRSDKController, IMixedRealityHand
    {
        public MetaQuestHand(
            TrackingState trackingState,
            Utilities.Handedness controllerHandedness,
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

        private Vector3 currentPointerPosition = Vector3.zero;
        private Quaternion currentPointerRotation = Quaternion.identity;
        private MixedRealityPose currentPointerPose = MixedRealityPose.ZeroIdentity;

        private static readonly HandFinger[] handFingers =
            Enum.GetValues(typeof(HandFinger)) as HandFinger[];
        private readonly List<Bone> fingerBones = new List<Bone>();

        #region IMixedRealityHand

        /// <inheritdoc/>
        public bool TryGetJoint(TrackedHandJoint joint, out MixedRealityPose pose) =>
            unityJointPoses.TryGetValue(joint, out pose);

        #endregion

        /// <inheritdoc/>
        public override bool IsInPointingPose => HandDefinition.IsInPointingPose;

        private static readonly ProfilerMarker UpdateControllerPerfMarker =
            new ProfilerMarker("[MRTK] MetaQuestHand.UpdateController");

        /// <inheritdoc/>
        public override void UpdateController(InputDevice inputDevice)
        {
            if (!Enabled) { return; }

            if (Interactions == null)
            {
                Debug.LogError($"No interaction configuration for {GetType().Name}");
                Enabled = false;
                return;
            }

            using (UpdateControllerPerfMarker.Auto())
            {
                UpdateHandData(inputDevice);

                for (int i = 0; i < Interactions?.Length; i++)
                {
                    switch (Interactions[i].AxisType)
                    {
                        case AxisType.SixDof:
                            UpdatePoseData(Interactions[i], inputDevice);
                            break;
                        case AxisType.Digital:
                            UpdateButtonData(Interactions[i], inputDevice);
                            break;
                        case AxisType.SingleAxis:
                            UpdateSingleAxisData(Interactions[i], inputDevice);
                            break;
                    }
                }
            }
        }

        private static readonly ProfilerMarker UpdatePoseDataPerfMarker =
            new ProfilerMarker("[MRTK] MetaQuestHand.UpdatePoseData");

        /// <inheritdoc/>
        protected override void UpdatePoseData(MixedRealityInteractionMapping interactionMapping, InputDevice inputDevice)
        {
            using (UpdatePoseDataPerfMarker.Auto())
            {
                switch (interactionMapping.InputType)
                {
                    case DeviceInputType.IndexFinger:
                        HandDefinition?.UpdateCurrentIndexPose(interactionMapping);
                        break;

                    case DeviceInputType.SpatialPointer:
                        if (inputDevice.TryGetFeatureValue(CustomUsages.PointerPosition, out currentPointerPosition))
                        {
                            currentPointerPose.Position = MixedRealityPlayspace.TransformPoint(currentPointerPosition);
                        }
                        if (inputDevice.TryGetFeatureValue(CustomUsages.PointerRotation, out currentPointerRotation))
                        {
                            currentPointerPose.Rotation = MixedRealityPlayspace.Rotation * currentPointerRotation;
                        }

                        interactionMapping.PoseData = currentPointerPose;
                        if (interactionMapping.Changed)
                        {
                            CoreServices.InputSystem?.RaisePoseInputChanged(
                                InputSource, ControllerHandedness,
                                interactionMapping.MixedRealityInputAction, currentPointerPose);
                        }
                        break;

                    default:
                        base.UpdatePoseData(interactionMapping, inputDevice);
                        break;
                }
            }
        }

        private static readonly ProfilerMarker UpdateButtonDataPerfMarker =
            new ProfilerMarker("[MRTK] MetaQuestHand.UpdateButtonData");

        /// <inheritdoc/>
        protected override void UpdateButtonData(MixedRealityInteractionMapping interactionMapping, InputDevice inputDevice)
        {
            using (UpdateButtonDataPerfMarker.Auto())
            {
                switch (interactionMapping.InputType)
                {
                    case DeviceInputType.Select:
                        if (inputDevice.TryGetFeatureValue(CommonUsages.trigger, out float triggerAmount))
                        {
                            interactionMapping.BoolData = triggerAmount > 0.5f;
                        }
                        break;
                    default:
                        base.UpdateButtonData(interactionMapping, inputDevice);
                        return;
                }

                if (interactionMapping.Changed)
                {
                    if (interactionMapping.BoolData)
                    {
                        CoreServices.InputSystem?.RaiseOnInputDown(
                            InputSource, ControllerHandedness, interactionMapping.MixedRealityInputAction);
                    }
                    else
                    {
                        CoreServices.InputSystem?.RaiseOnInputUp(
                            InputSource, ControllerHandedness, interactionMapping.MixedRealityInputAction);
                    }
                }
            }
        }

        private static readonly ProfilerMarker UpdateSingleAxisDataPerfMarker =
            new ProfilerMarker("[MRTK] MetaQuestHand.UpdateSingleAxisData");

        /// <inheritdoc/>
        protected override void UpdateSingleAxisData(MixedRealityInteractionMapping interactionMapping, InputDevice inputDevice)
        {
            using (UpdateSingleAxisDataPerfMarker.Auto())
            {
                switch (interactionMapping.InputType)
                {
                    case DeviceInputType.TriggerPress:
                    case DeviceInputType.GripPress:
                        if (inputDevice.TryGetFeatureValue(CommonUsages.trigger, out float triggerAmount))
                        {
                            interactionMapping.BoolData = triggerAmount > 0.5f;
                        }
                        break;
                    default:
                        base.UpdateSingleAxisData(interactionMapping, inputDevice);
                        return;
                }

                if (interactionMapping.Changed)
                {
                    if (interactionMapping.BoolData)
                    {
                        CoreServices.InputSystem?.RaiseOnInputDown(
                            InputSource, ControllerHandedness, interactionMapping.MixedRealityInputAction);
                    }
                    else
                    {
                        CoreServices.InputSystem?.RaiseOnInputUp(
                            InputSource, ControllerHandedness, interactionMapping.MixedRealityInputAction);
                    }
                }
            }
        }

        private static readonly ProfilerMarker UpdateHandDataPerfMarker =
            new ProfilerMarker("[MRTK] MetaQuestHand.UpdateHandData");

        private void UpdateHandData(InputDevice inputDevice)
        {
            using (UpdateHandDataPerfMarker.Auto())
            {
                if (inputDevice.TryGetFeatureValue(CommonUsages.handData, out Hand hand))
                {
                    foreach (HandFinger finger in handFingers)
                    {
                        if (hand.TryGetRootBone(out Bone rootBone))
                        {
                            ReadHandJoint(TrackedHandJoint.Wrist, rootBone);
                        }

                        if (hand.TryGetFingerBones(finger, fingerBones))
                        {
                            for (int i = 0; i < fingerBones.Count; i++)
                            {
                                ReadHandJoint(ConvertToTrackedHandJoint(finger, i), fingerBones[i]);
                            }
                        }
                    }

                    HandDefinition?.UpdateHandJoints(unityJointPoses);
                }
            }
        }

        private void ReadHandJoint(TrackedHandJoint trackedHandJoint, Bone bone)
        {
            bool positionAvailable = bone.TryGetPosition(out Vector3 position);
            bool rotationAvailable = bone.TryGetRotation(out Quaternion rotation);

            if (positionAvailable && rotationAvailable)
            {
                position = MixedRealityPlayspace.TransformPoint(position);
                rotation = MixedRealityPlayspace.Rotation * rotation;
                unityJointPoses[trackedHandJoint] = new MixedRealityPose(position, rotation);
            }
        }

        /// <summary>
        /// Converts a Unity finger bone into an MRTK hand joint.
        /// For Quest, Unity provides four joints for the thumb and five for other fingers.
        /// The wrist joint is provided as the hand root bone.
        /// </summary>
        private TrackedHandJoint ConvertToTrackedHandJoint(HandFinger finger, int index)
        {
            switch (finger)
            {
                case HandFinger.Thumb:  return TrackedHandJoint.ThumbMetacarpalJoint + index;
                case HandFinger.Index:  return TrackedHandJoint.IndexMetacarpal + index;
                case HandFinger.Middle: return TrackedHandJoint.MiddleMetacarpal + index;
                case HandFinger.Ring:   return TrackedHandJoint.RingMetacarpal + index;
                case HandFinger.Pinky:  return TrackedHandJoint.PinkyMetacarpal + index;
                default:                return TrackedHandJoint.None;
            }
        }
    }
}
