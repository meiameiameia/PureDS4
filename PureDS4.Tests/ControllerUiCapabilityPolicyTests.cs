using DS4Windows;
using DS4Windows.InputDevices;
using DS4WinWPF.DS4Forms.ViewModels;

namespace DS4WindowsTests
{
    [TestClass]
    public class ControllerUiCapabilityPolicyTests
    {
        [TestMethod]
        public void DualShock4UsesDs4ArtworkAndRumbleWording()
        {
            ControllerUiCapabilities capabilities =
                ControllerUiCapabilities.For(InputDeviceType.DS4);

            Assert.AreEqual("DualShock 4 Controller.png",
                capabilities.ImageResourceName);
            Assert.AreEqual("Rumble strength", capabilities.FeedbackLabel);
            Assert.AreEqual("DualShock 4 audio", capabilities.AudioHeader);
            Assert.AreEqual("Enable headset microphone input",
                capabilities.MicrophoneToggleLabel);
            Assert.IsTrue(capabilities.ShowControllerAudioSettings);
            Assert.IsFalse(capabilities.SupportsAdaptiveTriggers);
            Assert.IsFalse(capabilities.SupportsMuteButton);
        }

        [TestMethod]
        public void OfflineProfileEditingDoesNotOfferRemovedHardware()
        {
            ControllerUiCapabilities capabilities =
                ControllerUiCapabilities.For(null);

            // A profile with no controller attached still edits the full
            // audio surface, but must not offer adaptive triggers, voice-coil
            // haptics or a mute button: no supported controller has them.
            Assert.IsTrue(capabilities.ShowControllerAudioSettings);
            Assert.IsFalse(capabilities.SupportsAdaptiveTriggers);
            Assert.IsFalse(capabilities.SupportsAdvancedHaptics);
            Assert.IsFalse(capabilities.SupportsMuteButton);
        }

        [TestMethod]
        public void MappingAvailabilityMatchesPhysicalDualShock4Controls()
        {
            ControllerUiCapabilities dualShock4 =
                ControllerUiCapabilities.For(InputDeviceType.DS4);
            ControllerUiCapabilities offlineProfile =
                ControllerUiCapabilities.For(null);

            Assert.IsTrue(dualShock4.IsMappingControlAvailable(
                DS4Controls.Cross));

            // Controls that only ever existed on a DualSense or an Edge stay
            // unavailable on the physical DualShock 4 diagram, while their
            // saved backend mappings are preserved.
            foreach (DS4Controls absent in new[]
            {
                DS4Controls.Mute, DS4Controls.Capture, DS4Controls.SideL,
                DS4Controls.SideR, DS4Controls.FnL, DS4Controls.FnR,
                DS4Controls.BLP, DS4Controls.BRP,
            })
            {
                Assert.IsFalse(dualShock4.IsMappingControlAvailable(
                    absent),
                    $"{absent} is not a DualShock 4 control.");
            }

            // Offline editing keeps every control remappable.
            Assert.IsTrue(offlineProfile.IsMappingControlAvailable(
                DS4Controls.FnL));
        }

        [DataTestMethod]
        [DataRow((int)InputDeviceType.DS4, (int)ConnectionType.BT,
            0x054C, 0x09CC, true)]
        [DataRow((int)InputDeviceType.DS4, (int)ConnectionType.USB,
            0x054C, 0x09CC, false)]
        [DataRow((int)InputDeviceType.DS4, (int)ConnectionType.BT,
            0x054C, 0xFFFF, false)]
        public void PhysicalAudioCapabilityMatchesConnectionAndSonyIdentity(
            int deviceType, int connectionType, int vendorId, int productId,
            bool expected)
        {
            ControllerUiCapabilities capabilities =
                ControllerUiCapabilities.For((InputDeviceType)deviceType,
                    (ConnectionType)connectionType, vendorId, productId);

            Assert.AreEqual(expected, capabilities.SupportsControllerAudio);
        }

        [TestMethod]
        public void LegacyMicRoutingOnlyAppearsForOfflineProfileEditing()
        {
            ControllerUiCapabilities offlineProfile =
                ControllerUiCapabilities.For(null);
            ControllerUiCapabilities bluetoothDualShock4 =
                ControllerUiCapabilities.For(InputDeviceType.DS4,
                    ConnectionType.BT, 0x054C, 0x09CC);

            Assert.IsTrue(offlineProfile.ShowLegacyMicrophoneRouting,
                "Offline editing keeps the legacy endpoint settings available under Advanced audio.");
            Assert.IsFalse(bluetoothDualShock4.ShowLegacyMicrophoneRouting);
        }

        [DataTestMethod]
        [DataRow((int)InputDeviceType.DS4, (int)ConnectionType.BT,
            0x054C, 0x09CC, (int)OutContType.ViiperDS4, true,
            (int)ControllerMicrophoneUiStatus.Ready, true)]
        [DataRow((int)InputDeviceType.DS4, (int)ConnectionType.BT,
            0x054C, 0x09CC, (int)OutContType.ViiperDS4, true,
            (int)ControllerMicrophoneUiStatus.Ready, true)]
        [DataRow((int)InputDeviceType.DS4, (int)ConnectionType.BT,
            0x054C, 0x09CC, (int)OutContType.ViiperSwitch2Pro, true,
            (int)ControllerMicrophoneUiStatus.Ready, true)]
        [DataRow((int)InputDeviceType.DS4, (int)ConnectionType.USB,
            0x054C, 0x09CC, (int)OutContType.ViiperDS4, true,
            (int)ControllerMicrophoneUiStatus.RequiresBluetooth, false)]
        public void MicrophoneUiStateExplainsPhysicalPersonaAndStreamReadiness(
            int deviceType, int connectionType, int vendorId, int productId,
            int outputType, bool activeStreamSupportsMicrophone,
            int expectedStatus, bool expectedCanEnable)
        {
            ControllerUiCapabilities capabilities =
                ControllerUiCapabilities.For((InputDeviceType)deviceType,
                    (ConnectionType)connectionType, vendorId, productId);

            ControllerMicrophoneUiState state =
                capabilities.GetMicrophoneUiState((OutContType)outputType,
                    activeStreamSupportsMicrophone,
                    requireActiveStream: true);

            Assert.AreEqual((ControllerMicrophoneUiStatus)expectedStatus,
                state.Status);
            Assert.AreEqual(expectedCanEnable, state.CanEnable);
            Assert.AreEqual(state.Status != ControllerMicrophoneUiStatus.Ready,
                state.ShowMessage);

            if (!expectedCanEnable)
            {
                Assert.IsTrue(state.CanChange(currentlyEnabled: true),
                    "An unsupported stale setting must remain switch-off-able.");
                Assert.IsFalse(state.CanAdjustLevel(currentlyEnabled: true));
            }
        }

        [TestMethod]
        public void ProfileOutputSelectionPreservesCustomAudioProcessing()
        {
            int device = Global.TEST_PROFILE_INDEX;
            OutContType previousOutput = Global.OutContType[device];
            OutContType previousTemporaryOutput = Global.outDevTypeTemp[device];
            byte previousCompression = Global.DualSenseSpeakerCompression[device];
            byte previousBassBoost = Global.DualSenseSpeakerBassBoost[device];
            byte previousHeadphoneVolume = Global.DualSenseHeadphoneVolume[device];

            try
            {
                Global.OutContType[device] = OutContType.ViiperDS4;
                Global.DualSenseSpeakerCompression[device] =
                    (byte)DualSenseSpeakerCompression.Strong;
                Global.DualSenseSpeakerBassBoost[device] = 6;
                Global.DualSenseHeadphoneVolume[device] = 173;

                int selectedIndex = 1;
                Assert.IsTrue(ProfileSettingsViewModel
                    .ApplyTemporaryOutputControllerSelection(device,
                        ref selectedIndex, 2));

                Assert.AreEqual(OutContType.ViiperSwitch2Pro,
                    ProfileSettingsViewModel.GetOutputControllerType(
                        selectedIndex));
                Assert.AreEqual((byte)DualSenseSpeakerCompression.Strong,
                    Global.DualSenseSpeakerCompression[device]);
                Assert.AreEqual((byte)6,
                    Global.DualSenseSpeakerBassBoost[device]);
                Assert.AreEqual((byte)173,
                    Global.DualSenseHeadphoneVolume[device]);
            }
            finally
            {
                Global.OutContType[device] = previousOutput;
                Global.outDevTypeTemp[device] = previousTemporaryOutput;
                Global.DualSenseSpeakerCompression[device] = previousCompression;
                Global.DualSenseSpeakerBassBoost[device] = previousBassBoost;
                Global.DualSenseHeadphoneVolume[device] =
                    previousHeadphoneVolume;
            }
        }
    }
}
