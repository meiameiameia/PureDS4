using DS4Windows;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace DS4WindowsTests
{
    /// <summary>
    /// Step 6, second pass: handing off to a predecessor's own uninstaller.
    /// Nothing here starts a process. These tests cover the two decisions
    /// PureDS4 actually makes — how to read a registered uninstall command,
    /// and whether running it may be offered at all.
    /// </summary>
    [TestClass]
    public class LegacyUninstallLauncherTests
    {
        [TestMethod]
        public void ParsesABareMsiExecCommand()
        {
            Assert.IsTrue(LegacyUninstallCommand.TryParse(
                "MsiExec.exe /X{FE00C21E-C0E6-4509-81EA-2D4B34D377A1}",
                out LegacyUninstallCommand command));

            Assert.AreEqual("MsiExec.exe", command.ExecutablePath);
            Assert.AreEqual("/X{FE00C21E-C0E6-4509-81EA-2D4B34D377A1}",
                command.Arguments);
        }

        [TestMethod]
        public void ParsesAQuotedPackageCachePathWithSpaces()
        {
            Assert.IsTrue(LegacyUninstallCommand.TryParse(
                "\"C:\\ProgramData\\Package Cache\\{abc}\\Setup_x64.exe\" /uninstall",
                out LegacyUninstallCommand command));

            Assert.AreEqual(
                @"C:\ProgramData\Package Cache\{abc}\Setup_x64.exe",
                command.ExecutablePath);
            Assert.AreEqual("/uninstall", command.Arguments);
        }

        [TestMethod]
        public void ParsesACommandWithNoArguments()
        {
            Assert.IsTrue(LegacyUninstallCommand.TryParse(
                @"C:\Program Files\Thing\uninst.exe",
                out LegacyUninstallCommand command));

            Assert.AreEqual(@"C:\Program Files\Thing\uninst.exe",
                command.ExecutablePath);
            Assert.AreEqual(string.Empty, command.Arguments);
        }

        [TestMethod]
        public void ParsesAnUnquotedPathWithSpacesAndArguments()
        {
            // The ambiguous shape: unquoted, spaces in the path, and a
            // trailing switch. Splitting on the first space would launch
            // "C:\Program", which is why the boundary is the .exe token.
            Assert.IsTrue(LegacyUninstallCommand.TryParse(
                @"C:\Program Files\Thing\uninst.exe /S",
                out LegacyUninstallCommand command));

            Assert.AreEqual(@"C:\Program Files\Thing\uninst.exe",
                command.ExecutablePath);
            Assert.AreEqual("/S", command.Arguments);
        }

        [DataTestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("   ")]
        [DataRow("\"")]
        [DataRow("\"\"")]
        public void RefusesAnUnusableCommand(string uninstallString)
        {
            Assert.IsFalse(LegacyUninstallCommand.TryParse(uninstallString,
                out LegacyUninstallCommand command));
            Assert.IsNull(command);
        }

        [TestMethod]
        public void OffersTheUninstallerWhenNothingBlocksIt()
        {
            LegacyInstallationSurvey survey = SurveyWith(processRunning: false);
            UninstallRegistryEntry entry = new UninstallRegistryEntry(
                "DS4Windows Reworked", "5.1.0", string.Empty,
                "MsiExec.exe /X{ABC}");

            Assert.AreEqual(LegacyUninstallReadiness.Ready,
                LegacyUninstallPolicy.Evaluate(survey, entry,
                    out LegacyUninstallCommand command));
            Assert.IsNotNull(command);
        }

        [TestMethod]
        public void RefusesWhileTheOtherApplicationIsStillRunning()
        {
            LegacyInstallationSurvey survey = SurveyWith(processRunning: true);
            UninstallRegistryEntry entry = new UninstallRegistryEntry(
                "DS4Windows Reworked", "5.1.0", string.Empty,
                "MsiExec.exe /X{ABC}");

            Assert.AreEqual(LegacyUninstallReadiness.ApplicationRunning,
                LegacyUninstallPolicy.Evaluate(survey, entry,
                    out LegacyUninstallCommand command));
            Assert.IsNull(command,
                "No command may be handed back while the old app runs.");
        }

        [TestMethod]
        public void RefusesWhenTheProductRegisteredNoUninstallCommand()
        {
            LegacyInstallationSurvey survey = SurveyWith(processRunning: false);
            UninstallRegistryEntry entry = new UninstallRegistryEntry(
                "DS4Windows", "5.1.0", string.Empty, string.Empty);

            Assert.AreEqual(LegacyUninstallReadiness.NoCommandRecorded,
                LegacyUninstallPolicy.Evaluate(survey, entry, out _));
        }

        [TestMethod]
        public void RefusesWhenThereIsNoEntryAtAll()
        {
            LegacyInstallationSurvey survey = SurveyWith(processRunning: false);

            Assert.AreEqual(LegacyUninstallReadiness.NoCommandRecorded,
                LegacyUninstallPolicy.Evaluate(survey, null, out _));
        }

        [TestMethod]
        public void PrefersTheBundleOverTheMsiItOwns()
        {
            // Both entries as a Burn-installed predecessor actually
            // registers them: the bundle uninstalls, while the MSI
            // underneath registers the *modify* verb. Removing the inner
            // MSI would strand the bundle's registration, and /I would show
            // maintenance mode rather than an uninstall.
            LegacyInstallationSurvey survey = SurveyWith(
                processRunning: false,
                new UninstallRegistryEntry("DS4Windows Reworked", "5.1.0",
                    string.Empty, "MsiExec.exe /I{DB4B65B9-72A1-49EB-AB9F-5A240704B177}"),
                new UninstallRegistryEntry("DS4Windows Reworked",
                    "5.1.0-beta.1", string.Empty,
                    "\"C:\\ProgramData\\Package Cache\\{02665702}\\DS4Windows-Reworked_5.1.0-beta.1_Setup_x64.exe\"  /uninstall"));

            UninstallRegistryEntry chosen =
                LegacyUninstallPolicy.SelectPrimaryEntry(survey);

            Assert.IsNotNull(chosen);
            Assert.AreEqual("5.1.0-beta.1", chosen.DisplayVersion,
                "The bundle, not the MSI it owns, must be selected.");
        }

        [DataTestMethod]
        [DataRow("MsiExec.exe /I{ABC}", false, "modify is not uninstall")]
        [DataRow("MsiExec.exe /X{ABC}", true, "msiexec uninstall verb")]
        [DataRow("MsiExec.exe /x{ABC}", true, "verb is case-insensitive")]
        [DataRow("\"C:\\a\\Setup.exe\" /uninstall", true, "burn bundle")]
        [DataRow("\"C:\\a\\Setup.exe\" /repair", false, "repair is not uninstall")]
        [DataRow("\"C:\\a\\Setup.exe\"", false, "no verb at all")]
        public void OnlyRecognisesCommandsThatActuallyUninstall(
            string uninstallString, bool expected, string because)
        {
            LegacyUninstallCommand.TryParse(uninstallString,
                out LegacyUninstallCommand command);

            Assert.AreEqual(expected,
                LegacyUninstallPolicy.IsUninstallCommand(command), because);
        }

        [TestMethod]
        public void OffersNothingWhenOnlyAModifyCommandExists()
        {
            LegacyInstallationSurvey survey = SurveyWith(
                processRunning: false,
                new UninstallRegistryEntry("DS4Windows", "5.1.0",
                    string.Empty, "MsiExec.exe /I{ABC}"));

            Assert.IsNull(LegacyUninstallPolicy.SelectPrimaryEntry(survey));
            Assert.AreEqual(LegacyUninstallReadiness.NoCommandRecorded,
                LegacyUninstallPolicy.Evaluate(survey,
                    survey.UninstallEntries[0], out LegacyUninstallCommand c));
            Assert.IsNull(c);
        }

        private static LegacyInstallationSurvey SurveyWith(
            bool processRunning, params UninstallRegistryEntry[] entries)
        {
            return new LegacyInstallationSurvey(
                registryRootPresent: true,
                installDirectories: new[] { @"C:\Program Files\DS4Windows" },
                configurationDirectoryPresent: true,
                configurationDirectoryPath:
                    @"C:\Users\owner\AppData\Roaming\DS4Windows",
                processRunning: processRunning,
                startupTaskPresent: false,
                viiperTaskPresent: false,
                uninstallEntries: entries);
        }

        private static LegacyInstallationSurvey SurveyWith(bool processRunning)
        {
            return new LegacyInstallationSurvey(
                registryRootPresent: true,
                installDirectories: new[] { @"C:\Program Files\DS4Windows" },
                configurationDirectoryPresent: true,
                configurationDirectoryPath:
                    @"C:\Users\owner\AppData\Roaming\DS4Windows",
                processRunning: processRunning,
                startupTaskPresent: false,
                viiperTaskPresent: false,
                uninstallEntries: new List<UninstallRegistryEntry>());
        }
    }
}
