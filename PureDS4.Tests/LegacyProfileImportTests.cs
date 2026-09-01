using DS4Windows;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace DS4WindowsTests
{
    /// <summary>
    /// Step 4 of the replacement flow: optionally import old profiles
    /// read-only. Scoped to game profiles only for this pass — the
    /// individual XML files under a legacy Profiles folder.
    /// </summary>
    [TestClass]
    public class LegacyProfileImportTests
    {
        [TestMethod]
        public void ReportsNoProfilesDirectoryWhenNoneExists()
        {
            WithTempDirectory(legacyRoot =>
            {
                LegacyProfileImportSurvey survey =
                    LegacyProfileImportScanner.Scan(legacyRoot);

                Assert.IsFalse(survey.ProfilesDirectoryPresent);
                Assert.IsFalse(survey.HasImportableProfiles);
            });
        }

        [TestMethod]
        public void ListsEveryXmlFileUnderTheLegacyProfilesFolder()
        {
            WithTempDirectory(legacyRoot =>
            {
                string profilesDir = Directory.CreateDirectory(
                    Path.Combine(legacyRoot, "Profiles")).FullName;
                File.WriteAllText(
                    Path.Combine(profilesDir, "Default.xml"), "default");
                File.WriteAllText(
                    Path.Combine(profilesDir, "Racing.xml"), "racing");
                File.WriteAllText(
                    Path.Combine(profilesDir, "notes.txt"), "ignore me");

                LegacyProfileImportSurvey survey =
                    LegacyProfileImportScanner.Scan(legacyRoot);

                Assert.IsTrue(survey.ProfilesDirectoryPresent);
                CollectionAssert.AreEquivalent(
                    new[] { "Default.xml", "Racing.xml" },
                    survey.Candidates.Select(c => c.FileName).ToList());
            });
        }

        [TestMethod]
        public void ImportCopiesEveryCandidateIntoTheDestination()
        {
            WithTempDirectory(legacyRoot => WithTempDirectory(destination =>
            {
                string legacyProfiles = Directory.CreateDirectory(
                    Path.Combine(legacyRoot, "Profiles")).FullName;
                File.WriteAllText(
                    Path.Combine(legacyProfiles, "Default.xml"), "default");

                LegacyProfileImportSurvey survey =
                    LegacyProfileImportScanner.Scan(legacyRoot);
                var results = LegacyProfileImporter.Import(survey, destination);

                Assert.AreEqual(1, results.Count);
                Assert.AreEqual(LegacyProfileImportOutcome.Imported,
                    results[0].Outcome);
                string copied = Path.Combine(destination, "Profiles",
                    "Default.xml");
                Assert.IsTrue(File.Exists(copied));
                Assert.AreEqual("default", File.ReadAllText(copied));
            }));
        }

        [TestMethod]
        public void ImportNeverOpensTheSourceForWriteOrDeletesIt()
        {
            WithTempDirectory(legacyRoot => WithTempDirectory(destination =>
            {
                string legacyProfiles = Directory.CreateDirectory(
                    Path.Combine(legacyRoot, "Profiles")).FullName;
                string sourcePath = Path.Combine(legacyProfiles,
                    "Default.xml");
                File.WriteAllText(sourcePath, "default");
                DateTime writeTimeBefore = File.GetLastWriteTimeUtc(sourcePath);

                LegacyProfileImportSurvey survey =
                    LegacyProfileImportScanner.Scan(legacyRoot);
                LegacyProfileImporter.Import(survey, destination);

                Assert.IsTrue(File.Exists(sourcePath),
                    "The source profile was removed.");
                Assert.AreEqual(writeTimeBefore,
                    File.GetLastWriteTimeUtc(sourcePath),
                    "The source profile was modified.");
                Assert.AreEqual("default", File.ReadAllText(sourcePath));
            }));
        }

        [TestMethod]
        public void ImportPreservesBothProfilesWhenTheNameAlreadyExists()
        {
            WithTempDirectory(legacyRoot => WithTempDirectory(destination =>
            {
                string legacyProfiles = Directory.CreateDirectory(
                    Path.Combine(legacyRoot, "Profiles")).FullName;
                File.WriteAllText(
                    Path.Combine(legacyProfiles, "Default.xml"), "legacy");

                string destinationProfiles = Directory.CreateDirectory(
                    Path.Combine(destination, "Profiles")).FullName;
                string existingPath = Path.Combine(destinationProfiles,
                    "Default.xml");
                File.WriteAllText(existingPath, "already here");

                LegacyProfileImportSurvey survey =
                    LegacyProfileImportScanner.Scan(legacyRoot);
                var results = LegacyProfileImporter.Import(survey, destination);

                Assert.AreEqual(1, results.Count);
                Assert.AreEqual(LegacyProfileImportOutcome.Imported,
                    results[0].Outcome);
                Assert.AreEqual("already here", File.ReadAllText(existingPath));
                Assert.AreEqual("Default (DS4Windows).xml",
                    results[0].DestinationFileName);
                Assert.AreEqual("legacy", File.ReadAllText(Path.Combine(
                    destinationProfiles, "Default (DS4Windows).xml")));
            }));
        }

        [TestMethod]
        public void RepeatedImportRecognisesTheEarlierRenamedCopy()
        {
            WithTempDirectory(legacyRoot => WithTempDirectory(destination =>
            {
                string legacyProfiles = Directory.CreateDirectory(
                    Path.Combine(legacyRoot, "Profiles")).FullName;
                File.WriteAllText(
                    Path.Combine(legacyProfiles, "Default.xml"), "legacy");
                string destinationProfiles = Directory.CreateDirectory(
                    Path.Combine(destination, "Profiles")).FullName;
                File.WriteAllText(Path.Combine(destinationProfiles,
                    "Default.xml"), "PureDS4 default");

                LegacyProfileImportSurvey survey =
                    LegacyProfileImportScanner.Scan(legacyRoot);
                var first = LegacyProfileImporter.Import(survey, destination);
                var repeated = LegacyProfileImporter.Import(survey, destination);

                Assert.AreEqual(LegacyProfileImportOutcome.Imported,
                    first.Single().Outcome);
                Assert.AreEqual(
                    LegacyProfileImportOutcome.SkippedAlreadyExists,
                    repeated.Single().Outcome);
                Assert.AreEqual("Default (DS4Windows).xml",
                    repeated.Single().DestinationFileName);
                Assert.AreEqual(2,
                    Directory.GetFiles(destinationProfiles, "*.xml").Length);
            }));
        }

        [TestMethod]
        public void ImportUsesANumberedNameWhenTheFirstAliasIsDifferent()
        {
            WithTempDirectory(legacyRoot => WithTempDirectory(destination =>
            {
                string legacyProfiles = Directory.CreateDirectory(
                    Path.Combine(legacyRoot, "Profiles")).FullName;
                File.WriteAllText(Path.Combine(legacyProfiles, "Racing.xml"),
                    "legacy racing");
                string destinationProfiles = Directory.CreateDirectory(
                    Path.Combine(destination, "Profiles")).FullName;
                File.WriteAllText(Path.Combine(destinationProfiles,
                    "Racing.xml"), "PureDS4 racing");
                File.WriteAllText(Path.Combine(destinationProfiles,
                    "Racing (DS4Windows).xml"), "unrelated alias");

                LegacyProfileImportResult result = LegacyProfileImporter.
                    Import(LegacyProfileImportScanner.Scan(legacyRoot),
                        destination).Single();

                Assert.AreEqual("Racing (DS4Windows 2).xml",
                    result.DestinationFileName);
                Assert.AreEqual("legacy racing", File.ReadAllText(Path.Combine(
                    destinationProfiles, result.DestinationFileName)));
            }));
        }

        [TestMethod]
        public void ImportRefusesToTargetTheLegacyDirectoryItself()
        {
            string legacyDirectory = LegacyConfigurationGuard
                .ResolveLegacyConfigurationDirectory();
            LegacyProfileImportSurvey survey =
                new LegacyProfileImportSurvey(false, null);

            Assert.ThrowsException<InvalidOperationException>(() =>
                LegacyProfileImporter.Import(survey, legacyDirectory));
        }

        private static void WithTempDirectory(Action<string> body)
        {
            string directory = Path.Combine(Path.GetTempPath(),
                "PureDS4Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                body(directory);
            }
            finally
            {
                try
                {
                    Directory.Delete(directory, true);
                }
                catch (IOException)
                {
                }
            }
        }
    }
}
