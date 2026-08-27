using DS4Windows;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;

namespace DS4WindowsTests
{
    /// <summary>
    /// Step 1 of the replacement flow: detecting a DS4Windows or DS4Windows
    /// Reworked installation. Exercised entirely against a fake environment
    /// so these tests never read, and can never be thrown off by, whatever
    /// is actually installed on the machine running them.
    /// </summary>
    [TestClass]
    public class LegacyInstallationDetectorTests
    {
        [TestMethod]
        public void CleanMachineReportsNoLegacyInstallation()
        {
            LegacyInstallationSurvey survey = LegacyInstallationDetector.Scan(
                new FakeLegacyInstallationEnvironment());

            Assert.IsFalse(survey.IsPresent);
            Assert.IsFalse(survey.RequiresApplicationClosed);
            Assert.AreEqual(0, survey.InstallDirectories.Count);
            Assert.AreEqual(0, survey.UninstallEntries.Count);
        }

        [TestMethod]
        public void RegistryRootAloneIsEnoughToReportPresence()
        {
            FakeLegacyInstallationEnvironment environment =
                new FakeLegacyInstallationEnvironment
                {
                    LocalMachineKeys = { @"SOFTWARE\DS4Windows" },
                };

            LegacyInstallationSurvey survey =
                LegacyInstallationDetector.Scan(environment);

            Assert.IsTrue(survey.IsPresent);
            Assert.IsTrue(survey.RegistryRootPresent);
            Assert.IsFalse(survey.RequiresApplicationClosed,
                "The registry key alone does not mean the process is running.");
        }

        [TestMethod]
        public void RunningProcessRequiresTheApplicationToBeClosed()
        {
            FakeLegacyInstallationEnvironment environment =
                new FakeLegacyInstallationEnvironment
                {
                    ProcessNames = { "DS4Windows" },
                };

            LegacyInstallationSurvey survey =
                LegacyInstallationDetector.Scan(environment);

            Assert.IsTrue(survey.ProcessRunning);
            Assert.IsTrue(survey.RequiresApplicationClosed);
        }

        [TestMethod]
        public void PureDS4sOwnProcessAndTasksAreNeverMatched()
        {
            // The detector's whole job is telling PureDS4 apart from what it
            // is replacing. It must never mistake its own running instance,
            // or its own scheduled tasks, for the legacy product.
            FakeLegacyInstallationEnvironment environment =
                new FakeLegacyInstallationEnvironment
                {
                    ProcessNames = { "PureDS4" },
                    ScheduledTasks = { "RunPureDS4", "RunPureDS4VIIPER" },
                };

            LegacyInstallationSurvey survey =
                LegacyInstallationDetector.Scan(environment);

            Assert.IsFalse(survey.IsPresent);
            Assert.IsFalse(survey.ProcessRunning);
            Assert.IsFalse(survey.StartupTaskPresent);
            Assert.IsFalse(survey.ViiperTaskPresent);
        }

        [TestMethod]
        public void BothInheritedInstallDirectoryNamesAreRecognised()
        {
            FakeLegacyInstallationEnvironment environment =
                new FakeLegacyInstallationEnvironment
                {
                    ProgramFilesRoots = { @"C:\Program Files" },
                    Directories =
                    {
                        @"C:\Program Files\DS4Windows",
                        @"C:\Program Files\DS4Windows Reworked",
                    },
                };

            LegacyInstallationSurvey survey =
                LegacyInstallationDetector.Scan(environment);

            Assert.IsTrue(survey.IsPresent);
            CollectionAssert.AreEquivalent(
                new[]
                {
                    @"C:\Program Files\DS4Windows",
                    @"C:\Program Files\DS4Windows Reworked",
                },
                survey.InstallDirectories.ToList());
        }

        [TestMethod]
        public void ConfigurationDirectoryUsesTheRoamingAppDataRoot()
        {
            FakeLegacyInstallationEnvironment environment =
                new FakeLegacyInstallationEnvironment
                {
                    RoamingAppDataPath = @"C:\Users\owner\AppData\Roaming",
                    Directories =
                        { @"C:\Users\owner\AppData\Roaming\DS4Windows" },
                };

            LegacyInstallationSurvey survey =
                LegacyInstallationDetector.Scan(environment);

            Assert.IsTrue(survey.ConfigurationDirectoryPresent);
            Assert.AreEqual(@"C:\Users\owner\AppData\Roaming\DS4Windows",
                survey.ConfigurationDirectoryPath);
        }

        [TestMethod]
        public void StartupAndViiperTasksAreReportedSeparately()
        {
            FakeLegacyInstallationEnvironment environment =
                new FakeLegacyInstallationEnvironment
                {
                    ScheduledTasks = { "RunDS4Windows" },
                };

            LegacyInstallationSurvey survey =
                LegacyInstallationDetector.Scan(environment);

            Assert.IsTrue(survey.StartupTaskPresent);
            Assert.IsFalse(survey.ViiperTaskPresent,
                "Only RunDS4Windows was reported present.");
        }

        [DataTestMethod]
        [DataRow("DS4Windows")]
        [DataRow("DS4Windows Reworked")]
        public void UninstallEntriesMatchBothProductNames(string displayName)
        {
            FakeLegacyInstallationEnvironment environment =
                new FakeLegacyInstallationEnvironment
                {
                    Uninstall =
                    {
                        new UninstallRegistryEntry(displayName, "5.1.0",
                            string.Empty, "MsiExec.exe /X{...}"),
                    },
                };

            LegacyInstallationSurvey survey =
                LegacyInstallationDetector.Scan(environment);

            Assert.AreEqual(1, survey.UninstallEntries.Count);
            Assert.AreEqual(displayName,
                survey.UninstallEntries[0].DisplayName);
        }

        [TestMethod]
        public void UnrelatedUninstallEntriesAreIgnored()
        {
            FakeLegacyInstallationEnvironment environment =
                new FakeLegacyInstallationEnvironment
                {
                    Uninstall =
                    {
                        new UninstallRegistryEntry("Some Other Application",
                            "1.0", string.Empty, string.Empty),
                        new UninstallRegistryEntry("PureDS4", "5.1.0",
                            string.Empty, string.Empty),
                    },
                };

            LegacyInstallationSurvey survey =
                LegacyInstallationDetector.Scan(environment);

            Assert.AreEqual(0, survey.UninstallEntries.Count);
            Assert.IsFalse(survey.IsPresent);
        }

        [TestMethod]
        public void EveryReportedSignalContributesToOverallPresence()
        {
            // Each signal is independently sufficient. A future edit that
            // accidentally drops one from IsPresent's aggregation should
            // fail here rather than silently under-detect.
            Assert.IsTrue(Scan(env => env.LocalMachineKeys.Add(
                @"SOFTWARE\DS4Windows")).IsPresent);
            Assert.IsTrue(Scan(env =>
            {
                env.ProgramFilesRoots.Add(@"C:\Program Files");
                env.Directories.Add(@"C:\Program Files\DS4Windows");
            }).IsPresent);
            Assert.IsTrue(Scan(env =>
            {
                env.RoamingAppDataPath = @"C:\Users\owner\AppData\Roaming";
                env.Directories.Add(
                    @"C:\Users\owner\AppData\Roaming\DS4Windows");
            }).IsPresent);
            Assert.IsTrue(Scan(
                env => env.ProcessNames.Add("DS4Windows")).IsPresent);
            Assert.IsTrue(Scan(
                env => env.ScheduledTasks.Add("RunDS4Windows")).IsPresent);
            Assert.IsTrue(Scan(
                env => env.ScheduledTasks.Add("RunVIIPER")).IsPresent);
            Assert.IsTrue(Scan(env => env.Uninstall.Add(
                new UninstallRegistryEntry("DS4Windows Reworked", "5.1.0",
                    string.Empty, string.Empty))).IsPresent);
        }

        private static LegacyInstallationSurvey Scan(
            System.Action<FakeLegacyInstallationEnvironment> configure)
        {
            FakeLegacyInstallationEnvironment environment =
                new FakeLegacyInstallationEnvironment();
            configure(environment);
            return LegacyInstallationDetector.Scan(environment);
        }
    }

    /// <summary>
    /// An in-memory stand-in for the real machine. Every collection starts
    /// empty, which corresponds to a clean machine with no legacy install.
    /// </summary>
    internal sealed class FakeLegacyInstallationEnvironment
        : ILegacyInstallationEnvironment
    {
        internal List<string> ProgramFilesRootsList { get; } =
            new List<string>();
        internal List<string> Directories { get; } = new List<string>();
        internal HashSet<string> LocalMachineKeys { get; } =
            new HashSet<string>();
        internal List<string> ProcessNames { get; } = new List<string>();
        internal HashSet<string> ScheduledTasks { get; } =
            new HashSet<string>();
        internal List<UninstallRegistryEntry> Uninstall { get; } =
            new List<UninstallRegistryEntry>();

        internal List<string> ProgramFilesRoots => ProgramFilesRootsList;
        internal string RoamingAppDataPath { get; set; } =
            @"C:\Users\test\AppData\Roaming";

        IReadOnlyList<string> ILegacyInstallationEnvironment
            .ProgramFilesRoots => ProgramFilesRootsList;
        string ILegacyInstallationEnvironment.RoamingAppDataPath =>
            RoamingAppDataPath;

        public bool DirectoryExists(string path) => Directories.Contains(path);

        public bool LocalMachineKeyExists(string subKeyPath) =>
            LocalMachineKeys.Contains(subKeyPath);

        public IReadOnlyList<string> RunningProcessNames() => ProcessNames;

        public bool ScheduledTaskExists(string taskName) =>
            ScheduledTasks.Contains(taskName);

        public IReadOnlyList<UninstallRegistryEntry> UninstallEntries() =>
            Uninstall;
    }
}
