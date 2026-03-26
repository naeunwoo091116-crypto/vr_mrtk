// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.Utilities;
using Microsoft.MixedReality.Toolkit.XRSDK.Input;
using System;
using UnityEngine;
using UnityEngine.XR;

namespace Microsoft.MixedReality.Toolkit.XRSDK.OpenXR
{
    /// <summary>
    /// Android/Quest-compatible OpenXR device manager for MRTK.
    /// Handles Meta Quest Touch controllers via Unity's XR SDK + OpenXR.
    /// XR subsystem readiness is handled in Update() via XRSubsystemHelpers.
    /// </summary>
    [MixedRealityDataProvider(
        typeof(IMixedRealityInputSystem),
        SupportedPlatforms.Android,
        "Meta Quest OpenXR Device Manager")]
    public class MetaQuestOpenXRDeviceManager : XRSDKDeviceManager
    {
        public MetaQuestOpenXRDeviceManager(
            IMixedRealityInputSystem inputSystem,
            string name = null,
            uint priority = DefaultPriority,
            BaseMixedRealityProfile profile = null)
            : base(inputSystem, name, priority, profile) { }

        /// <inheritdoc />
        public override void Enable()
        {
            // Do not check for active loader here — XR initialization on Android
            // is asynchronous (coroutine). The base Update() already checks
            // XRSubsystemHelpers.InputSubsystem.running before processing input.
            base.Enable();
            Debug.Log("[MetaQuestOpenXRDeviceManager] Enabled.");
        }

        /// <inheritdoc />
        protected override Type GetControllerType(SupportedControllerType supportedControllerType)
        {
            switch (supportedControllerType)
            {
                case SupportedControllerType.OculusTouch:
                    return typeof(MetaQuestController);
                case SupportedControllerType.ArticulatedHand:
                    return typeof(GenericXRSDKController);
                default:
                    return base.GetControllerType(supportedControllerType);
            }
        }

        /// <inheritdoc />
        protected override InputSourceType GetInputSourceType(SupportedControllerType supportedControllerType)
        {
            switch (supportedControllerType)
            {
                case SupportedControllerType.OculusTouch:
                    return InputSourceType.Controller;
                case SupportedControllerType.ArticulatedHand:
                    return InputSourceType.Hand;
                default:
                    return base.GetInputSourceType(supportedControllerType);
            }
        }

        /// <inheritdoc />
        protected override SupportedControllerType GetCurrentControllerType(InputDevice inputDevice)
        {
            if (inputDevice.characteristics.HasFlag(InputDeviceCharacteristics.HandTracking))
            {
                return SupportedControllerType.ArticulatedHand;
            }

            if (inputDevice.characteristics.HasFlag(InputDeviceCharacteristics.Controller))
            {
                return SupportedControllerType.OculusTouch;
            }

            return base.GetCurrentControllerType(inputDevice);
        }
    }
}
