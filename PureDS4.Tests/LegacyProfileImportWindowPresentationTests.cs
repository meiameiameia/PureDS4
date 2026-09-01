using DS4Windows;
using DS4WinWPF.DS4Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace DS4WindowsTests
{
    /// <summary>
    /// The copy shown on the "Import from DS4Windows" screen (step 4 of the
    /// replacement flow). Checked separately from the window so wording
    /// changes cannot silently start claiming DS4Windows' own files are
    /// touched.
    /// </summary>
    [TestClass]
    public class LegacyProfileImportWindowPresentationTests
    {
        [TestMethod]
        public void SummaryForNoProfilesNeverImpliesAnythingWasChanged()
        {
            LegacyProfileImportSurvey survey =
                new LegacyProfileImportSurvey(false, null);

            string summary = LegacyProfileImportWindow.BuildSummary(survey);

            StringAssert.Contains(summary, "No DS4Windows profiles");
            StringAssert.Contains(summary, "never changed");
        }

        [TestMethod]
        public void SummaryForFoundProfilesNamesTheCountAndPreservesTheSource()
        {
            LegacyProfileImportSurvey survey = new LegacyProfileImportSurvey(
                true,
                new[]
                {
                    new LegacyProfileImportCandidate("Default.xml", "x"),
                    new LegacyProfileImportCandidate("Racing.xml", "y"),
                });

            string summary = LegacyProfileImportWindow.BuildSummary(survey);

            StringAssert.Contains(summary, "Found 2 profiles");
            StringAssert.Contains(summary, "never changes or removes");
            StringAssert.Contains(summary, "DS4Windows suffix");
        }

        [TestMethod]
        public void RenamedImportShowsItsActualDestinationName()
        {
            LegacyProfileImportResult result = new LegacyProfileImportResult(
                "Default.xml", LegacyProfileImportOutcome.Imported,
                destinationFileName: "Default (DS4Windows).xml");

            string description = LegacyProfileImportWindow.DescribeResult(
                result);

            StringAssert.Contains(description,
                "Imported as Default (DS4Windows).xml");
        }

        [TestMethod]
        public void ResultSummaryReportsEachOutcomeSeparately()
        {
            List<LegacyProfileImportResult> results = new List<LegacyProfileImportResult>
            {
                new LegacyProfileImportResult("A.xml",
                    LegacyProfileImportOutcome.Imported),
                new LegacyProfileImportResult("B.xml",
                    LegacyProfileImportOutcome.Imported),
                new LegacyProfileImportResult("C.xml",
                    LegacyProfileImportOutcome.SkippedAlreadyExists),
                new LegacyProfileImportResult("D.xml",
                    LegacyProfileImportOutcome.Failed, "locked"),
            };

            string summary = LegacyProfileImportWindow.BuildResultSummary(results);

            StringAssert.Contains(summary, "Imported 2 profiles");
            StringAssert.Contains(summary, "1 was already present");
            StringAssert.Contains(summary, "1 could not be copied");
        }

        [TestMethod]
        public void ResultSummaryForAllImportedMentionsNoSkipsOrFailures()
        {
            List<LegacyProfileImportResult> results = new List<LegacyProfileImportResult>
            {
                new LegacyProfileImportResult("A.xml",
                    LegacyProfileImportOutcome.Imported),
            };

            string summary = LegacyProfileImportWindow.BuildResultSummary(results);

            Assert.AreEqual("Imported 1 profile.", summary);
        }
    }
}
