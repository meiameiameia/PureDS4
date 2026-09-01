using DS4Windows;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;

namespace DS4WindowsTests
{
    /// <summary>
    /// Step 3 of the replacement flow: preserve or archive user
    /// configuration unless deletion was specifically authorized. PureDS4
    /// has nothing that currently computes a path under DS4Windows'
    /// configuration directory, so what these tests pin is the enforced
    /// invariant that it never can, rather than any archiving behavior.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class LegacyConfigurationGuardTests
    {
        [DataTestMethod]
        [DataRow(@"C:\Users\owner\AppData\Roaming\DS4Windows",
            @"C:\Users\owner\AppData\Roaming\DS4Windows")]
        [DataRow(@"C:\Users\owner\AppData\Roaming\DS4Windows\",
            @"C:\Users\owner\AppData\Roaming\DS4Windows")]
        [DataRow(@"C:\Users\owner\AppData\Roaming\ds4windows",
            @"C:\Users\owner\AppData\Roaming\DS4Windows")]
        public void RecognisesTheSameDirectoryRegardlessOfCaseOrTrailingSlash(
            string candidatePath, string legacyDirectory)
        {
            Assert.IsTrue(LegacyConfigurationGuard.IsLegacyConfigurationPath(
                candidatePath, legacyDirectory));
        }

        [TestMethod]
        public void DoesNotMatchAPureDS4OwnedDirectory()
        {
            Assert.IsFalse(LegacyConfigurationGuard.IsLegacyConfigurationPath(
                @"C:\Users\owner\AppData\Roaming\PureDS4",
                @"C:\Users\owner\AppData\Roaming\DS4Windows"));
        }

        [TestMethod]
        public void MatchesDescendantsButNotTheParentDirectory()
        {
            Assert.IsTrue(LegacyConfigurationGuard.IsLegacyConfigurationPath(
                @"C:\Users\owner\AppData\Roaming\DS4Windows\Profiles",
                @"C:\Users\owner\AppData\Roaming\DS4Windows"));
            Assert.IsFalse(LegacyConfigurationGuard.IsLegacyConfigurationPath(
                @"C:\Users\owner\AppData\Roaming",
                @"C:\Users\owner\AppData\Roaming\DS4Windows"));
        }

        [TestMethod]
        public void SimilarPrefixIsNotTreatedAsADescendant()
        {
            Assert.IsFalse(LegacyConfigurationGuard.IsLegacyConfigurationPath(
                @"C:\Users\owner\AppData\Roaming\DS4Windows-Archive",
                @"C:\Users\owner\AppData\Roaming\DS4Windows"));
        }

        [DataTestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("   ")]
        public void TreatsAMissingCandidateAsNotLegacy(string candidatePath)
        {
            Assert.IsFalse(LegacyConfigurationGuard.IsLegacyConfigurationPath(
                candidatePath, @"C:\Users\owner\AppData\Roaming\DS4Windows"));
        }

        [TestMethod]
        public void RefusesTheRealMachinesLegacyConfigurationDirectory()
        {
            string legacyDirectory =
                LegacyConfigurationGuard.ResolveLegacyConfigurationDirectory();

            Assert.ThrowsException<InvalidOperationException>(() =>
                LegacyConfigurationGuard.EnsureNotLegacyConfigurationPath(
                    legacyDirectory));
        }

        [TestMethod]
        public void AcceptsAnOrdinaryPureDS4OwnedPath()
        {
            string ownedPath = Path.Combine(Path.GetTempPath(),
                "PureDS4Tests", ProductIdentity.DataFolderName);

            // Must not throw.
            LegacyConfigurationGuard.EnsureNotLegacyConfigurationPath(
                ownedPath);
        }

        [TestMethod]
        public void GlobalSaveWhereRefusesTheLegacyConfigurationDirectory()
        {
            string previousActive = Global.appdatapath;
            try
            {
                string legacyDirectory = LegacyConfigurationGuard
                    .ResolveLegacyConfigurationDirectory();

                Assert.ThrowsException<InvalidOperationException>(() =>
                    Global.SaveWhere(legacyDirectory));

                // The refused call must not have taken effect.
                Assert.AreEqual(previousActive, Global.appdatapath);
            }
            finally
            {
                Global.appdatapath = previousActive;
            }
        }
    }
}
