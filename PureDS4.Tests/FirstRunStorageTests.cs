using DS4Windows;
using DS4WinWPF.DS4Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;

namespace DS4WindowsTests
{
    /// <summary>
    /// First-run storage selection.
    ///
    /// The inherited dialog deleted the store the user did not pick, and
    /// defaulted to doing so. On a fresh install it also claimed files in the
    /// other location would be deleted when no other location existed. Both
    /// conflict with the replacement contract: configuration is preserved
    /// unless deletion has been explicitly authorized, and cleanup belongs to
    /// the later replacement workflow rather than to picking a folder.
    ///
    /// These tests pin the two things that matter: the copy is truthful, and
    /// choosing a location never removes the other one.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class FirstRunStorageTests
    {
        private const string DeletionClaim = "will be deleted";

        [TestMethod]
        public void FreshInstallCopyOffersBothStoresWithoutMentioningDeletion()
        {
            FirstRunStoragePresentation presentation =
                SaveWhere.CreatePresentation(multipleStores: false);

            Assert.AreEqual("Choose where PureDS4 stores its data",
                presentation.Title);
            Assert.AreEqual("For this Windows account (Recommended)",
                presentation.AccountAction);
            Assert.AreEqual("Portable folder", presentation.PortableAction);

            StringAssert.Contains(presentation.AccountDescription,
                @"%AppData%\PureDS4");
            StringAssert.Contains(presentation.PortableDescription,
                "PureDS4.exe");
            StringAssert.Contains(presentation.PortableDescription, "write");

            // Nothing exists in the other location on a fresh install, so the
            // inherited claim that it would be deleted was simply false.
            Assert.IsFalse(presentation.ShowExistingDataNotice);
            AssertNoDeletionClaim(presentation);
        }

        [TestMethod]
        public void MultipleStoresCopyIsTruthfulAndPromisesPreservation()
        {
            FirstRunStoragePresentation presentation =
                SaveWhere.CreatePresentation(multipleStores: true);

            Assert.IsTrue(presentation.ShowExistingDataNotice);
            StringAssert.Contains(presentation.ExistingDataNotice,
                "exists in both locations");
            StringAssert.Contains(presentation.ExistingDataNotice,
                "Nothing is deleted");
            StringAssert.Contains(presentation.ExistingDataNotice,
                "left untouched");

            AssertNoDeletionClaim(presentation);
        }

        [TestMethod]
        public void StorageCopyUsesNoInheritedTerminologyOrBranding()
        {
            foreach (bool multipleStores in new[] { false, true })
            {
                FirstRunStoragePresentation presentation =
                    SaveWhere.CreatePresentation(multipleStores);

                foreach (string line in AllCopy(presentation))
                {
                    Assert.IsFalse(line.Contains("DS4Windows",
                            StringComparison.OrdinalIgnoreCase),
                        $"First-run storage copy names another product: {line}");
                    Assert.IsFalse(line.Contains("Save Where",
                            StringComparison.OrdinalIgnoreCase),
                        $"Inherited dialog title survived: {line}");
                    Assert.IsFalse(line.Contains("Program Folder",
                            StringComparison.OrdinalIgnoreCase),
                        $"Inherited technical terminology survived: {line}");
                }
            }
        }

        [DataTestMethod]
        [DataRow((int)FirstRunStorageLocation.WindowsAccount)]
        [DataRow((int)FirstRunStorageLocation.PortableFolder)]
        public void AFreshChoiceSelectsThatStoreAndDeletesNothing(int location)
        {
            WithIsolatedStores((accountRoot, portableRoot) =>
            {
                // A fresh install has no data anywhere yet.
                FirstRunStorage.Activate((FirstRunStorageLocation)location,
                    freshInstall: true);

                string expected =
                    (FirstRunStorageLocation)location ==
                        FirstRunStorageLocation.PortableFolder
                        ? portableRoot : accountRoot;
                Assert.AreEqual(expected, Global.appdatapath);

                // The chosen store is seeded, and neither root is removed.
                Assert.IsTrue(Directory.Exists(accountRoot));
                Assert.IsTrue(Directory.Exists(portableRoot));
                Assert.IsTrue(File.Exists(Path.Combine(expected,
                    "Profiles.xml")), "The chosen store was not initialized.");
            });
        }

        [DataTestMethod]
        [DataRow((int)FirstRunStorageLocation.WindowsAccount)]
        [DataRow((int)FirstRunStorageLocation.PortableFolder)]
        public void ChoosingBetweenTwoExistingStoresPreservesBoth(int location)
        {
            WithIsolatedStores((accountRoot, portableRoot) =>
            {
                // Both stores already hold real user configuration.
                SeedStore(accountRoot, "account");
                SeedStore(portableRoot, "portable");

                FirstRunStorage.Activate((FirstRunStorageLocation)location,
                    freshInstall: false);

                string expected =
                    (FirstRunStorageLocation)location ==
                        FirstRunStorageLocation.PortableFolder
                        ? portableRoot : accountRoot;
                Assert.AreEqual(expected, Global.appdatapath);

                // Neither store is touched: not the one that was passed over,
                // and not the one that was chosen.
                AssertStoreIntact(accountRoot, "account");
                AssertStoreIntact(portableRoot, "portable");
            });
        }

        [TestMethod]
        public void AnotherProductsConfigurationIsNeverTargeted()
        {
            WithIsolatedStores((accountRoot, portableRoot) =>
            {
                // A DS4Windows install that has not been removed yet keeps its
                // own configuration directory. Picking a PureDS4 store must
                // not read, move, or remove it.
                string foreign = Path.Combine(
                    Path.GetDirectoryName(accountRoot), "DS4Windows");
                SeedStore(foreign, "ds4windows");

                SeedStore(accountRoot, "account");
                SeedStore(portableRoot, "portable");

                FirstRunStorage.Activate(
                    FirstRunStorageLocation.WindowsAccount,
                    freshInstall: false);

                AssertStoreIntact(foreign, "ds4windows");
                Assert.IsFalse(Global.appdatapath.Contains("DS4Windows",
                        StringComparison.OrdinalIgnoreCase),
                    "PureDS4 selected another product's storage directory.");
            });
        }

        [TestMethod]
        public void OwnedStorageRootCarriesThePureDS4Identity()
        {
            StringAssert.EndsWith(Global.appDataPpath,
                Path.DirectorySeparatorChar + ProductIdentity.DataFolderName);
            Assert.IsFalse(Global.appDataPpath.Contains("DS4Windows",
                StringComparison.OrdinalIgnoreCase));
        }

        private static string[] AllCopy(FirstRunStoragePresentation value)
        {
            return new[]
            {
                value.Title, value.Heading, value.Summary,
                value.AccountAction, value.AccountDescription,
                value.PortableAction, value.PortableDescription,
                value.ExistingDataNotice,
            };
        }

        private static void AssertNoDeletionClaim(
            FirstRunStoragePresentation presentation)
        {
            Assert.IsFalse(presentation.DeletesTheOtherLocation);

            foreach (string line in AllCopy(presentation))
            {
                Assert.IsFalse(line.Contains(DeletionClaim,
                        StringComparison.OrdinalIgnoreCase),
                    $"Storage copy still threatens deletion: {line}");
            }
        }

        private static void SeedStore(string root, string marker)
        {
            Directory.CreateDirectory(Path.Combine(root, "Profiles"));
            File.WriteAllText(Path.Combine(root, "Auto Profiles.xml"), marker);
            File.WriteAllText(Path.Combine(root, "Profiles.xml"), marker);
            File.WriteAllText(Path.Combine(root, "Profiles", "Default.xml"),
                marker);
        }

        private static void AssertStoreIntact(string root, string marker)
        {
            Assert.IsTrue(Directory.Exists(root), $"{root} was removed.");
            Assert.IsTrue(Directory.Exists(Path.Combine(root, "Profiles")),
                $"{root} lost its Profiles directory.");
            Assert.AreEqual(marker,
                File.ReadAllText(Path.Combine(root, "Auto Profiles.xml")),
                $"{root} lost its auto-profile rules.");
            Assert.AreEqual(marker,
                File.ReadAllText(Path.Combine(root, "Profiles", "Default.xml")),
                $"{root} lost a saved profile.");
        }

        private static void WithIsolatedStores(Action<string, string> body)
        {
            string previousAppData = Global.appDataPpath;
            string previousExeDir = Global.exedirpath;
            string previousActive = Global.appdatapath;

            string sandbox = Path.Combine(Path.GetTempPath(), "PureDS4Tests",
                Guid.NewGuid().ToString("N"));
            string accountRoot = Path.Combine(sandbox, "AppData",
                ProductIdentity.DataFolderName);
            string portableRoot = Path.Combine(sandbox, "Portable");

            Directory.CreateDirectory(accountRoot);
            Directory.CreateDirectory(portableRoot);
            try
            {
                Global.appDataPpath = accountRoot;
                Global.exedirpath = portableRoot;
                body(accountRoot, portableRoot);
            }
            finally
            {
                Global.appDataPpath = previousAppData;
                Global.exedirpath = previousExeDir;
                Global.appdatapath = previousActive;
                try
                {
                    Directory.Delete(sandbox, true);
                }
                catch (IOException)
                {
                }
            }
        }
    }
}
