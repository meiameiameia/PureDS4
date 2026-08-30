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

            StringAssert.Contains(summary, "does not remove anything");
            StringAssert.Contains(summary, "only describes");
        }
    }
}
