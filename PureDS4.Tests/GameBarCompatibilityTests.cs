using DS4WinWPF.DS4Control.DTOXml;
using System.Xml.Serialization;
using DS4Windows;

namespace DS4WindowsTests
{
    [TestClass]
    public class GameBarCompatibilityTests
    {
        [DataTestMethod]
        [DataRow(OutContType.ViiperDualSense)]
        [DataRow(OutContType.ViiperDualSenseEdge)]
        [DataRow(OutContType.ViiperDS4)]
        public void UsesTemporaryXInputForNativePlayStationOutputs(OutContType outputType)
        {
            Assert.IsTrue(ControlService.ShouldUseGameBarControllerCompatibility(
                enabled: true, outputType, dInputOnly: false));
        }

        [DataTestMethod]
        [DataRow(OutContType.None)]
        [DataRow(OutContType.X360)]
        [DataRow(OutContType.DS4)]
        [DataRow(OutContType.ViiperX360)]
        public void DoesNotCreateCompanionForOtherOutputs(OutContType outputType)
        {
            Assert.IsFalse(ControlService.ShouldUseGameBarControllerCompatibility(
                enabled: true, outputType, dInputOnly: false));
        }

        [TestMethod]
        public void RequiresEnabledVirtualOutputProfile()
        {
            Assert.IsFalse(ControlService.ShouldUseGameBarControllerCompatibility(
                enabled: false, OutContType.ViiperDualSense, dInputOnly: false));
            Assert.IsFalse(ControlService.ShouldUseGameBarControllerCompatibility(
                enabled: true, OutContType.ViiperDualSense, dInputOnly: true));
        }

        [DataTestMethod]
        [DataRow(OutContType.ViiperX360)]
        [DataRow(OutContType.None)]
        public void ActiveCompanionRetiresBeforeNonPlayStationProfileAppears(
            OutContType requestedOutputType)
        {
            Assert.IsTrue(ControlService.
                ShouldRetireGameBarCompatibilityBeforeProfileChange(
                    routeActive: true, enabled: true, requestedOutputType,
                    requestedDInputOnly: false));
        }

        [TestMethod]
        public void ActiveCompanionCanRemainAcrossCompatiblePlayStationProfile()
        {
            Assert.IsFalse(ControlService.
                ShouldRetireGameBarCompatibilityBeforeProfileChange(
                    routeActive: true, enabled: true,
                    OutContType.ViiperDualSense,
                    requestedDInputOnly: false));
        }

        [TestMethod]
        public void InactiveCompanionRequiresNoProfileTransitionWork()
        {
            Assert.IsFalse(ControlService.
                ShouldRetireGameBarCompatibilityBeforeProfileChange(
                    routeActive: false, enabled: true,
                    OutContType.ViiperX360,
                    requestedDInputOnly: false));
        }

        [DataTestMethod]
        [DataRow(true, 0L, 1000L, false)]
        [DataRow(false, 1000L, 2000L, false)]
        [DataRow(true, 1000L, 999L, false)]
        [DataRow(true, 1000L, 1000L, true)]
        [DataRow(true, 1000L, 9999L, true)]
        [DataRow(true, 1000L, 10000L, false)]
        [DataRow(true, 9000L, 10000L, true)]
        public void BluetoothInputTimeoutHasBoundedGraceFromLastValidState(
            bool interfaceStillPresent, long lastValidTick, long nowTick,
            bool expectedRetry)
        {
            Assert.AreEqual(expectedRetry,
                DS4Device.ShouldRetryBluetoothInputAfterTimeout(
                    interfaceStillPresent, lastValidTick, nowTick));
        }

        [DataTestMethod]
        [DataRow(0L, 3000L, false)]
        [DataRow(1000L, 999L, false)]
        [DataRow(1000L, 3999L, true)]
        [DataRow(1000L, 4000L, false)]
        [DataRow(5000L, 6000L, true)]
        public void BluetoothReadinessRequiresRecentPhysicalState(
            long lastValidTick, long nowTick, bool expectedFresh)
        {
            Assert.AreEqual(expectedFresh,
                DS4Device.IsBluetoothInputFresh(lastValidTick, nowTick));
        }

        [DataTestMethod]
        [DataRow(1000L, 0L, 3999L, false)]
        [DataRow(1000L, 0L, 4000L, true)]
        [DataRow(1000L, 2000L, 10999L, false)]
        [DataRow(1000L, 2000L, 11000L, true)]
        [DataRow(1000L, 9000L, 11000L, false)]
        [DataRow(1000L, 2000L, 1999L, true)]
        public void AudioOnlyReportsCannotKeepControllerStateAlive(
            long inputLoopStartTick, long lastValidTick, long nowTick,
            bool expectedRetirement)
        {
            Assert.AreEqual(expectedRetirement,
                DS4Device.ShouldRetireBluetoothAudioOnlyStream(
                    inputLoopStartTick, lastValidTick, nowTick));
        }

        [TestMethod]
        public void BluetoothDisconnectReportRequiresPriorValidInput()
        {
            Assert.IsFalse(DS4Device.ShouldSendBluetoothDisconnectReport(
                receivedValidInput: false));
            Assert.IsTrue(DS4Device.ShouldSendBluetoothDisconnectReport(
                receivedValidInput: true));
        }

        [TestMethod]
        public void PrewarmKeepsCompanionUntilVisibilityProbeCatchesUp()
        {
            const long now = 1000;

            Assert.IsTrue(ControlService.ShouldKeepGameBarCompatibilityRoute(
                gameBarVisible: false, now, prewarmUntilTicks: 2000));
            Assert.IsTrue(ControlService.ShouldKeepGameBarCompatibilityRoute(
                gameBarVisible: true, now, prewarmUntilTicks: 0));
            Assert.IsFalse(ControlService.ShouldKeepGameBarCompatibilityRoute(
                gameBarVisible: false, now, prewarmUntilTicks: 999));
        }

        [DataTestMethod]
        [DataRow(true, false, false, false, true)]
        [DataRow(true, false, true, false, true)]
        [DataRow(true, true, false, false, true)]
        [DataRow(true, true, true, false, false)]
        [DataRow(false, true, true, true, true)]
        [DataRow(false, true, true, false, false)]
        public void VisibilityLatchChangesOnlyForCompletedSupportedResults(
            bool confirmedVisible, bool probeCompleted, bool supported,
            bool probeVisible, bool expected)
        {
            Assert.AreEqual(expected,
                GameBarIntegration.ResolveGameBarApiVisibility(
                    confirmedVisible, probeCompleted, supported, probeVisible));
        }

        [TestMethod]
        public void ProfileSettingPersistsThroughDtoAndXml()
        {
            var store = new BackingStore();
            var dto = new ProfileDTO
            {
                DeviceIndex = 0,
                GameBarControllerCompatibilityString = bool.TrueString,
            };

            dto.MapTo(store);
            Assert.IsTrue(store.gameBarControllerCompatibility[0]);

            var roundTrip = new ProfileDTO { DeviceIndex = 0 };
            roundTrip.MapFrom(store);
            Assert.AreEqual(bool.TrueString,
                roundTrip.GameBarControllerCompatibilityString);

            var serializer = new XmlSerializer(typeof(ProfileDTO),
                ProfileDTO.GetAttributeOverrides());
            using var writer = new StringWriter();
            serializer.Serialize(writer, roundTrip);
            StringAssert.Contains(writer.ToString(),
                "<GameBarControllerCompatibility>True</GameBarControllerCompatibility>");
        }

        [TestMethod]
        public void LegacyProfileSwitcherMigratesToCompanionAndIsNotRewritten()
        {
            const string xml = """
                <DS4Windows>
                  <GameBarHomeButtonSupport>True</GameBarHomeButtonSupport>
                  <GameBarProfileName>Legacy Game Bar</GameBarProfileName>
                </DS4Windows>
                """;
            var serializer = new XmlSerializer(typeof(ProfileDTO),
                ProfileDTO.GetAttributeOverrides());
            ProfileDTO dto;
            using (var reader = new StringReader(xml))
            {
                dto = (ProfileDTO)serializer.Deserialize(reader);
            }

            var store = new BackingStore();
            dto.DeviceIndex = 0;
            dto.MapTo(store);
            Assert.IsTrue(store.gameBarControllerCompatibility[0]);
            Assert.IsFalse(store.gameBarHomeButtonSupport[0]);
            Assert.AreEqual(string.Empty, store.gameBarProfileName[0]);

            var migrated = new ProfileDTO { DeviceIndex = 0 };
            migrated.MapFrom(store);
            using var writer = new StringWriter();
            serializer.Serialize(writer, migrated);
            string migratedXml = writer.ToString();
            StringAssert.Contains(migratedXml,
                "<GameBarControllerCompatibility>True</GameBarControllerCompatibility>");
            Assert.IsFalse(migratedXml.Contains("GameBarHomeButtonSupport"));
            Assert.IsFalse(migratedXml.Contains("GameBarProfileName"));
        }
    }
}
