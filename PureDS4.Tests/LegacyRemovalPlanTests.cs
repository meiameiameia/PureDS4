using DS4Windows;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;

namespace DS4WindowsTests
{
    /// <summary>
    /// Step 6 of the replacement flow, first pass: a read-only plan
    /// describing what removing the old installation would involve.
    /// Nothing under test here executes anything; these tests pin what the
    /// plan says, not any effect on the machine.
    /// </summary>
    [TestClass]
    public class LegacyRemovalPlanTests
    {
        [TestMethod]
        public void ACleanSurveyProducesAnEmptyPlan()
        {
            LegacyInstallationSurvey survey = new LegacyInstallationSurvey(
                registryRootPresent: false, installDirectories: null,
                configurationDirectoryPresent: false,
                configurationDirectoryPath: null, processRunning: false,
                startupTaskPresent: false, viiperTaskPresent: false,
                uninstallEntries: null);

            LegacyRemovalPlan plan = LegacyRemovalPlanner.Build(survey);

            Assert.IsFalse(plan.HasAnythingToRemove);
            Assert.IsFalse(plan.HasDetectedState);
            Assert.IsFalse(plan.HasCompetingRuntimeOwnership);
            Assert.IsFalse(plan.ConfigurationWouldSurvive);
            Assert.AreEqual(0, plan.Items.Count);
        }

        [TestMethod]
        public void ListsEachUninstallEntryWithItsExactCommand()
        {
            LegacyInstallationSurvey survey = new LegacyInstallationSurvey(
                registryRootPresent: false, installDirectories: null,
                configurationDirectoryPresent: false,
                configurationDirectoryPath: null, processRunning: false,
                startupTaskPresent: false, viiperTaskPresent: false,
                uninstallEntries: new List<UninstallRegistryEntry>
                {
                    new UninstallRegistryEntry("DS4Windows Reworked", "5.1.0",
                        string.Empty, "MsiExec.exe /X{ABC}"),
                });

            LegacyRemovalPlan plan = LegacyRemovalPlanner.Build(survey);

            LegacyRemovalItem item = plan.Items.Single();
            Assert.AreEqual(LegacyRemovalItemCategory.ApplicationUninstaller,
                item.Category);
            StringAssert.Contains(item.Description, "DS4Windows Reworked");
            StringAssert.Contains(item.Description, "5.1.0");
            Assert.AreEqual("MsiExec.exe /X{ABC}", item.Detail);
        }

        [TestMethod]
        public void NotesAMissingUninstallCommandInsteadOfLeavingItBlank()
        {
            LegacyInstallationSurvey survey = new LegacyInstallationSurvey(
                registryRootPresent: false, installDirectories: null,
                configurationDirectoryPresent: false,
                configurationDirectoryPath: null, processRunning: false,
                startupTaskPresent: false, viiperTaskPresent: false,
                uninstallEntries: new List<UninstallRegistryEntry>
                {
                    new UninstallRegistryEntry("DS4Windows", string.Empty,
                        string.Empty, string.Empty),
                });

            LegacyRemovalPlan plan = LegacyRemovalPlanner.Build(survey);

            StringAssert.Contains(plan.Items.Single().Detail,
                "No uninstall command was recorded");
        }

        [TestMethod]
        public void ListsInstallDirectoriesRegistryRootAndTasksSeparately()
        {
            LegacyInstallationSurvey survey = new LegacyInstallationSurvey(
                registryRootPresent: true,
                installDirectories: new[] { @"C:\Program Files\DS4Windows" },
                configurationDirectoryPresent: false,
                configurationDirectoryPath: null, processRunning: false,
                startupTaskPresent: true, viiperTaskPresent: true,
                uninstallEntries: null);

            LegacyRemovalPlan plan = LegacyRemovalPlanner.Build(survey);

            Assert.IsTrue(plan.Items.Any(i =>
                i.Category == LegacyRemovalItemCategory.InstallDirectory &&
                i.Detail == @"C:\Program Files\DS4Windows"));
            Assert.IsTrue(plan.Items.Any(i =>
                i.Category == LegacyRemovalItemCategory.RegistryRoot &&
                i.Detail.Contains("DS4Windows")));
            Assert.IsTrue(plan.Items.Any(i =>
                i.Category == LegacyRemovalItemCategory.ScheduledTask &&
                i.Detail == "RunDS4Windows"));
            Assert.IsTrue(plan.Items.Any(i =>
                i.Category == LegacyRemovalItemCategory.ScheduledTask &&
                i.Detail == "RunVIIPER"));
        }

        [TestMethod]
        public void FlagsThatConfigurationSurvivesRemovalByDefault()
        {
            LegacyInstallationSurvey survey = new LegacyInstallationSurvey(
                registryRootPresent: false, installDirectories: null,
                configurationDirectoryPresent: true,
                configurationDirectoryPath: @"C:\Users\owner\AppData\Roaming\DS4Windows",
                processRunning: false, startupTaskPresent: false,
                viiperTaskPresent: false, uninstallEntries: null);

            LegacyRemovalPlan plan = LegacyRemovalPlanner.Build(survey);

            Assert.IsTrue(plan.ConfigurationWouldSurvive);
            Assert.IsTrue(plan.HasDetectedState);
            Assert.IsFalse(plan.HasAnythingToRemove);
            Assert.IsTrue(plan.HasResidualState);
            LegacyRemovalItem configItem = plan.Items.Single(i =>
                i.Category == LegacyRemovalItemCategory.Configuration);
            StringAssert.Contains(configItem.Description, "left behind");
            Assert.AreEqual(@"C:\Users\owner\AppData\Roaming\DS4Windows",
                configItem.Detail);
        }

        [TestMethod]
        public void ActiveOwnershipIsDistinguishedFromPreservedResidue()
        {
            LegacyInstallationSurvey survey = new LegacyInstallationSurvey(
                registryRootPresent: true,
                installDirectories: new[] { @"C:\Program Files\DS4Windows" },
                configurationDirectoryPresent: true,
                configurationDirectoryPath:
                    @"C:\Users\owner\AppData\Roaming\DS4Windows",
                processRunning: false, startupTaskPresent: false,
                viiperTaskPresent: false, uninstallEntries: null,
                runtimeExecutablePaths: new[]
                {
                    @"C:\Program Files\DS4Windows\DS4Windows.exe",
                });

            LegacyRemovalPlan plan = LegacyRemovalPlanner.Build(survey);

            Assert.IsTrue(plan.HasCompetingRuntimeOwnership);
            Assert.IsTrue(plan.HasAnythingToRemove);
            Assert.IsFalse(plan.HasResidualState);
        }
    }
}
