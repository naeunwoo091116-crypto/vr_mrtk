// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.Utilities;
using Microsoft.MixedReality.Toolkit.XRSDK.Input;

namespace Microsoft.MixedReality.Toolkit.XRSDK.OpenXR
{
    /// <summary>
    /// OpenXR-based controller for Meta Quest Touch controllers.
    /// Uses OculusTouchControllerDefinition for proper button mapping
    /// and GenericXRSDKController base for cross-platform XR SDK input.
    /// </summary>
    [MixedRealityController(
        SupportedControllerType.OculusTouch,
        new[] { Handedness.Left, Handedness.Right },
        "Textures/OculusControllersTouch")]
    public class MetaQuestController : GenericXRSDKController
    {
        public MetaQuestController(
            TrackingState trackingState,
            Handedness controllerHandedness,
            IMixedRealityInputSource inputSource = null,
            MixedRealityInteractionMapping[] interactions = null)
            : base(trackingState, controllerHandedness, inputSource, interactions,
                  new OculusTouchControllerDefinition(controllerHandedness))
        { }
    }
}
