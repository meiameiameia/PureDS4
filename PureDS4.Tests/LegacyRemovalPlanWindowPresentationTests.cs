using DS4Windows;
using DS4WinWPF.DS4Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace DS4WindowsTests
{
    /// <summary>
    /// The copy shown on the "DS4Windows removal plan" screen (step 6,
    /// first pass). Checked separately from the window so wording changes
    /// cannot silently start implying PureDS4 removes anything from here.
    /// </summary>
    [TestClass]
    public class LegacyRemovalPlanWindowPresentationTests
    {
        [TestMethod]
        public void SummaryForNothingDetectedSaysSo()
        {
            LegacyRemovalPlan plan = new LegacyRemovalPlan(
                new List<LegacyRemovalItem>(), configurationWouldSurvive: false);

            string summary = LegacyRemovalPlanWindow.BuildSummary(plan);

            StringAssert.Contains(summary, "No DS4Windows");
            StringAssert.Contains(summary, "nothing to");
        }

        [TestMethod]
        public void SummaryForADetectedInstallationNeverClaimsToRemoveAnything()
        {
            LegacyRemovalPlan plan = new LegacyRemovalPlan(
                new List<LegacyRemovalItem>
                {
                    new LegacyRemovalItem(
                        LegacyRemovalItemCategory.ApplicationUninstaller,
                        "Uninstall DS4Windows Reworked", "MsiExec.exe /X{ABC}"),
                },
                configurationWouldSurvive: false);

            string summary = LegacyRemovalPlanWindow.BuildSummary(plan);

            StringAssert.Contains(summary, "does not remove any of it itself");
            StringAssert.Contains(summary, "Windows' own uninstaller");
        }

        [TestMethod]
        public void ConfirmationNamesTheProductAndTheExactCommand()
        {
            LegacyUninstallCommand.TryParse("MsiExec.exe /X{ABC}",
                out LegacyUninstallCommand command);

            string text = LegacyRemovalPlanWindow.BuildConfirmation(
                "DS4Windows Reworked", command);

            StringAssert.Contains(text, "DS4Windows Reworked");
            StringAssert.Contains(text, "MsiExec.exe /X{ABC}");
            StringAssert.Contains(text, "does not perform the removal itself");
            StringAssert.Contains(text, "not deleted");
        }

        [TestMethod]
        public void ARunningApplicationIsExplainedRatherThanSilentlyBlocking()
        {
            string text = LegacyRemovalPlanWindow.DescribeBlocker(
                LegacyUninstallReadiness.ApplicationRunning);

            StringAssert.Contains(text, "Close DS4Windows first");
            StringAssert.Contains(text, "Re-check");
        }

        [TestMethod]
        public void AMissingUninstallCommandPointsAtWindowsOwnAppsList()
        {
            string text = LegacyRemovalPlanWindow.DescribeBlocker(
                LegacyUninstallReadiness.NoCommandRecorded);

            StringAssert.Contains(text, "Apps list");
        }

        [TestMethod]
        public void NothingIsSaidWhenRemovalIsSimplyAvailable()
        {
            Assert.AreEqual(string.Empty,
                LegacyRemovalPlanWindow.DescribeBlocker(
                    LegacyUninstallReadiness.Ready));
        }
    }
}
