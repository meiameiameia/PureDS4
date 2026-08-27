using DS4Windows;
using DS4Windows.InputDevices;

namespace DS4WinWPF.DS4Forms.ViewModels
{
    internal enum ControllerMicrophoneUiStatus
    {
        Ready,
        OfflineConfiguration,
        UsbLegacyRoute,
        RequiresCompatibleController,
        RequiresBluetooth,
        RequiresPlayStationOutput,
        OutputStarting,
    }

    internal readonly struct ControllerMicrophoneUiState
    {
        internal ControllerMicrophoneUiState(ControllerMicrophoneUiStatus status,
            bool canEnable, string message)
        {
            Status = status;
            CanEnable = canEnable;
            Message = message ?? string.Empty;
        }

        internal ControllerMicrophoneUiStatus Status { get; }
        internal bool CanEnable { get; }
        internal string Message { get; }
        internal bool ShowMessage => !string.IsNullOrEmpty(Message);
        internal bool CanChange(bool currentlyEnabled) =>
            CanEnable || currentlyEnabled;
        internal bool CanAdjustLevel(bool currentlyEnabled) =>
            CanEnable && currentlyEnabled;
    }

    /// <summary>
    /// Keeps physical-controller presentation decisions in one place. Profile
    /// settings remain stored for every controller, while the frontend only
    /// advertises hardware features that the selected physical pad can use.
    /// </summary>
    internal sealed class ControllerUiCapabilities
    {
        private ControllerUiCapabilities(InputDeviceType? deviceType,
            string controllerName, string imageResourceName,
            bool isPlayStationController, bool showControllerAudioSettings,
            string feedbackLabel,
            string audioHeader, string audioDescription,
            string microphoneToggleLabel,
            string microphoneDescription)
        {
            DeviceType = deviceType;
            ControllerName = controllerName;
            ImageResourceName = imageResourceName;
            IsPlayStationController = isPlayStationController;
            ShowControllerAudioSettings = showControllerAudioSettings;
            FeedbackLabel = feedbackLabel;
            AudioHeader = audioHeader;
            AudioDescription = audioDescription;
            MicrophoneToggleLabel = microphoneToggleLabel;
            MicrophoneDescription = microphoneDescription;
        }

        internal InputDeviceType? DeviceType { get; }
        internal string ControllerName { get; }
        internal string ImageResourceName { get; }
        internal bool HasControllerArtwork => ImageResourceName != null;
        internal bool IsPlayStationController { get; }
        internal bool IsDualShock4 => DeviceType == InputDeviceType.DS4;
        internal ConnectionType? ConnectionType { get; private set; }
        internal int? VendorId { get; private set; }
        internal int? ProductId { get; private set; }
        internal bool PhysicalIdentityKnown { get; private set; }
        internal bool ShowControllerAudioSettings { get; }
        internal bool ShowPlayStationControllerSettings =>
            ShowControllerAudioSettings;
        // Adaptive triggers, voice-coil haptics and the mute button were
        // DualSense hardware features. Neither supported controller has them,
        // so these stay false even for a profile with no controller attached:
        // offering them would promise hardware this tool no longer supports.
        internal bool SupportsAdaptiveTriggers => false;
        internal bool SupportsAdvancedHaptics => false;
        internal bool SupportsMuteButton => false;
        internal string FeedbackLabel { get; }
        internal string AudioHeader { get; }
        internal string AudioDescription { get; }
        internal string MicrophoneToggleLabel { get; }
        internal string MicrophoneDescription { get; }

        internal bool IsGenuineSonyController => PhysicalIdentityKnown &&
            VendorId == DS4Devices.SONY_VID && ProductId.HasValue &&
            IsSupportedSonyProduct(DeviceType, ProductId.Value);

        internal bool SupportsControllerAudio
        {
            get
            {
                if (!PhysicalIdentityKnown)
                {
                    return DeviceType == InputDeviceType.DS4;
                }

                if (!IsGenuineSonyController)
                {
                    return false;
                }

                return IsDualShock4 &&
                    ConnectionType == DS4Windows.ConnectionType.BT;
            }
        }

        internal bool ShowLegacyMicrophoneRouting => DeviceType == null;

        internal ControllerMicrophoneUiState GetMicrophoneUiState(
            OutContType outputType, bool activeStreamSupportsMicrophone,
            bool requireActiveStream)
        {
            if (DeviceType == null || !PhysicalIdentityKnown)
            {
                return new ControllerMicrophoneUiState(
                    ControllerMicrophoneUiStatus.OfflineConfiguration,
                    canEnable: true,
                    "These settings will activate when the profile is used with a compatible PlayStation controller.");
            }

            if (!IsGenuineSonyController ||
                !IsDualShock4)
            {
                return new ControllerMicrophoneUiState(
                    ControllerMicrophoneUiStatus.RequiresCompatibleController,
                    canEnable: false,
                    "Controller microphone routing requires a genuine Sony DualShock 4.");
            }

            if (ConnectionType != DS4Windows.ConnectionType.BT)
            {
                return new ControllerMicrophoneUiState(
                    ControllerMicrophoneUiStatus.RequiresBluetooth,
                    canEnable: false,
                    "Direct controller microphone input requires a Bluetooth DualShock 4.");
            }

            if (!ControllerMicrophoneRoutePolicy
                .SupportsVirtualMicrophoneOutput(outputType))
            {
                return new ControllerMicrophoneUiState(
                    ControllerMicrophoneUiStatus.RequiresPlayStationOutput,
                    canEnable: false,
                    "A virtual PlayStation audio interface is required to expose the controller microphone.");
            }

            if (requireActiveStream && !activeStreamSupportsMicrophone)
            {
                return new ControllerMicrophoneUiState(
                    ControllerMicrophoneUiStatus.OutputStarting,
                    canEnable: false,
                    "The virtual microphone interface is starting. This control will become available automatically.");
            }

            return new ControllerMicrophoneUiState(
                ControllerMicrophoneUiStatus.Ready, canEnable: true,
                string.Empty);
        }

        /// <summary>
        /// Controls that only ever existed on a DualSense or a DualSense Edge.
        /// Neither supported controller has them, so they are unavailable even
        /// while editing a profile with nothing attached: offering them would
        /// promise hardware this tool does not support. Existing saved
        /// mappings for these controls are still preserved on disk.
        /// </summary>
        private static bool IsUnsupportedHardwareControl(DS4Controls control)
        {
            return control == DS4Controls.Mute ||
                control == DS4Controls.Capture ||
                control == DS4Controls.SideL ||
                control == DS4Controls.SideR ||
                control == DS4Controls.FnL ||
                control == DS4Controls.FnR ||
                control == DS4Controls.BLP ||
                control == DS4Controls.BRP;
        }

        internal bool IsMappingControlAvailable(DS4Controls control)
        {
            return !IsUnsupportedHardwareControl(control);
        }

        /// <summary>
        /// How to name the hardware an unavailable control is missing from.
        /// With a controller attached this is that controller; while editing a
        /// profile offline it has to describe the supported hardware instead
        /// of the placeholder profile name.
        /// </summary>
        internal string MappingAvailabilityScopeName => DeviceType == null
            ? "a DualShock 4"
            : ControllerName;

        internal static ControllerUiCapabilities ForDevice(DS4Device device)
        {
            if (device == null)
            {
                return For(null);
            }

            int? vendorId = device.HidDevice?.Attributes?.VendorId;
            int? productId = device.HidDevice?.Attributes?.ProductId;
            return For(device.DeviceType, device.ConnectionType, vendorId,
                productId, physicalIdentityKnown: true);
        }

        internal static ControllerUiCapabilities For(InputDeviceType? deviceType)
        {
            return For(deviceType, null, null, null,
                physicalIdentityKnown: false);
        }

        internal static ControllerUiCapabilities For(InputDeviceType? deviceType,
            ConnectionType? connectionType, int? vendorId, int? productId)
        {
            return For(deviceType, connectionType, vendorId, productId,
                physicalIdentityKnown: true);
        }

        private static ControllerUiCapabilities For(InputDeviceType? deviceType,
            ConnectionType? connectionType, int? vendorId, int? productId,
            bool physicalIdentityKnown)
        {
            ControllerUiCapabilities capabilities = deviceType switch
            {
                InputDeviceType.DS4 => new ControllerUiCapabilities(
                    deviceType,
                    "DualShock 4",
                    "DualShock 4 Controller.png",
                    isPlayStationController: true,
                    showControllerAudioSettings: true,
                    feedbackLabel: "Rumble strength",
                    audioHeader: "DualShock 4 audio",
                    audioDescription:
                        "Bluetooth speaker output and headset-mic input through the controller's 3.5 mm jack.",
                    microphoneToggleLabel: "Enable headset microphone input",
                    microphoneDescription:
                        "DualShock 4 microphone input comes from a headset connected to the controller's 3.5 mm jack."),
                InputDeviceType.DS3 => new ControllerUiCapabilities(
                    deviceType,
                    "DualShock 3",
                    "DualShock 4 Controller.png",
                    isPlayStationController: true,
                    showControllerAudioSettings: false,
                    feedbackLabel: "Rumble strength",
                    audioHeader: "Controller audio",
                    audioDescription: "Controller audio is not available on DualShock 3.",
                    microphoneToggleLabel: "Enable controller microphone input",
                    microphoneDescription: "Controller microphone input is not available on DualShock 3."),
                null => new ControllerUiCapabilities(
                    null,
                    "Generic profile",
                    null,
                    isPlayStationController: false,
                    showControllerAudioSettings: true,
                    feedbackLabel: "Rumble strength",
                    audioHeader: "Controller audio",
                    audioDescription:
                        "Speaker and headset-mic settings for a DualShock 4.",
                    microphoneToggleLabel: "Enable headset microphone input",
                    microphoneDescription:
                        "These settings become active when the profile is used with a DualShock 4."),
                _ => new ControllerUiCapabilities(
                    deviceType,
                    "Controller",
                    null,
                    isPlayStationController: false,
                    showControllerAudioSettings: false,
                    feedbackLabel: "Rumble strength",
                    audioHeader: "PlayStation controller audio",
                    audioDescription:
                        "Select a compatible PlayStation controller to use controller audio.",
                    microphoneToggleLabel: "Enable controller microphone input",
                    microphoneDescription:
                        "Controller audio is available on compatible PlayStation controllers."),
            };

            capabilities.ConnectionType = connectionType;
            capabilities.VendorId = vendorId;
            capabilities.ProductId = productId;
            capabilities.PhysicalIdentityKnown = physicalIdentityKnown;
            return capabilities;
        }

        private static bool IsSupportedSonyProduct(InputDeviceType? deviceType,
            int productId)
        {
            return deviceType switch
            {
                InputDeviceType.DS4 => productId == 0x05C4 ||
                    productId == 0x09CC,
                _ => false,
            };
        }
    }
}
