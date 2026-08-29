using DS4Windows;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace DS4WindowsTests
{
    /// <summary>
    /// Step 2 of the replacement flow: which name to show the user for the
    /// installation the detector found still running. The wording decision
    /// lives outside the window so it can be checked without constructing
    /// any WPF object.
    /// </summary>
    [TestClass]
    public class LegacyApplicationGateTests
    {
        [TestMethod]
        public void FallsBackToTheGenericNameWithNoUninstallEntry()
        {
            LegacyInstallationSurvey survey = new LegacyInstallationSurvey(
                registryRootPresent: false,
                installDirectories: null,
                configurationDirectoryPresent: false,
                configurationDirectoryPath: null,
                processRunning: true,
                startupTaskPresent: false,
                viiperTaskPresent: false,
                uninstallEntries: null);

            Assert.AreEqual("DS4Windows",
                LegacyApplicationGate.DescribeDetectedProduct(survey));
        }

        [DataTestMethod]
        [DataRow("DS4Windows")]
        [DataRow("DS4Windows Reworked")]
        public void PrefersTheExactAddRemoveProgramsDisplayName(
            string displayName)
        {
            LegacyInstallationSurvey survey = new LegacyInstallationSurvey(
                registryRootPresent: false,
                installDirectories: null,
                configurationDirectoryPresent: false,
                configurationDirectoryPath: null,
                processRunning: true,
                startupTaskPresent: false,
                viiperTaskPresent: false,
                uninstallEntries: new List<UninstallRegistryEntry>
                {
                    new UninstallRegistryEntry(displayName, "5.1.0",
                        string.Empty, string.Empty),
                });

            Assert.AreEqual(displayName,
                LegacyApplicationGate.DescribeDetectedProduct(survey));
        }
    }
}
