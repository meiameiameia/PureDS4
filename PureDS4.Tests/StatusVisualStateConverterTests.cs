using DS4Windows;
using DS4WinWPF.DS4Forms;
using DS4WinWPF.DS4Forms.Converters;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Globalization;

namespace DS4WindowsTests
{
    [TestClass]
    public class StatusVisualStateConverterTests
    {
        [TestMethod]
        public void GameOutputWarningWaitsForStartupAndClearsWhenReady()
        {
            Assert.IsFalse(MainWindow.ShouldShowGameOutputWarning(false, false),
                "A backend still starting is not yet an actionable setup failure.");
            Assert.IsTrue(MainWindow.ShouldShowGameOutputWarning(true, false),
                "A failed probe after startup must remain visible.");
            Assert.IsFalse(MainWindow.ShouldShowGameOutputWarning(true, true),
                "A recovered backend must clear the stale warning.");
        }

        [TestMethod]
        public void ControllerStagesPreserveReadyAttentionAndProgressMeaning()
        {
            var converter = new ControllerStartupStageVisualStateConverter();

            Assert.AreEqual(StatusVisualState.Success,
                converter.Convert(ControllerStartupStage.Ready, null, null,
                    CultureInfo.InvariantCulture));
            Assert.AreEqual(StatusVisualState.Warning,
                converter.Convert(ControllerStartupStage.Attention, null, null,
                    CultureInfo.InvariantCulture));
            Assert.AreEqual(StatusVisualState.Neutral,
                converter.Convert(ControllerStartupStage.Connecting, null,
                    null, CultureInfo.InvariantCulture));
        }

        [DataTestMethod]
        [DataRow("Full", StatusVisualState.Success)]
        [DataRow("Charging", StatusVisualState.Success)]
        [DataRow("75%+", StatusVisualState.Success)]
        [DataRow("~75%", StatusVisualState.Neutral)]
        [DataRow("~20%", StatusVisualState.Warning)]
        [DataRow("5%", StatusVisualState.Warning)]
        [DataRow("Charging unavailable", StatusVisualState.Warning)]
        [DataRow("Charging error", StatusVisualState.Error)]
        [DataRow("...", StatusVisualState.Neutral)]
        public void BatteryPresentationUsesTruthfulNonAlarmistState(string text,
            StatusVisualState expected)
        {
            var converter = new BatteryTextVisualStateConverter();
            Assert.AreEqual(expected, converter.Convert(text, null, null,
                CultureInfo.InvariantCulture));
        }
    }
}
