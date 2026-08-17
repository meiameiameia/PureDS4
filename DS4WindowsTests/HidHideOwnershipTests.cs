using DS4Windows;

namespace DS4WindowsTests
{
    [TestClass]
    public class HidHideOwnershipTests
    {
        [TestMethod]
        public void PreExistingMatchingEntryRemainsBaselineOnly()
        {
            const string instanceId = @"HID\VID_054C&PID_09CC\OWNER";

            List<string> result = HidHideOwnershipPolicy.AddIfMissing(
                new[] { instanceId }, instanceId.ToLowerInvariant(),
                out bool added);

            Assert.IsFalse(added);
            CollectionAssert.AreEqual(new[] { instanceId }, result);
        }

        [TestMethod]
        public void ApplicationCreatedEntryIsAddedOnce()
        {
            const string instanceId = @"HID\VID_054C&PID_09CC\CREATED";

            List<string> first = HidHideOwnershipPolicy.AddIfMissing(
                Array.Empty<string>(), instanceId, out bool firstAdded);
            List<string> repeated = HidHideOwnershipPolicy.AddIfMissing(
                first, instanceId, out bool repeatedAdded);

            Assert.IsTrue(firstAdded);
            Assert.IsFalse(repeatedAdded);
            CollectionAssert.AreEqual(new[] { instanceId }, repeated);
        }

        [TestMethod]
        public void CleanupRemovesOnlyProvenApplicationDelta()
        {
            const string baseline = @"HID\BASELINE";
            const string owned = @"HID\OWNED";
            const string unrelated = @"HID\UNRELATED";

            List<string> result = HidHideOwnershipPolicy.RemoveOwned(
                new[] { baseline, owned, unrelated }, new[] { owned },
                out List<string> removed);

            CollectionAssert.AreEqual(new[] { owned }, removed);
            CollectionAssert.AreEqual(new[] { baseline, unrelated }, result);
        }

        [TestMethod]
        public void ActiveStateRestoresOnlyWhenGlobalBlacklistReturnsToBaseline()
        {
            string[] baseline = { @"HID\BASELINE" };

            Assert.IsTrue(HidHideOwnershipPolicy.CanRestoreInactiveState(
                baseline, new[] { @"hid\baseline" }));
            Assert.IsFalse(HidHideOwnershipPolicy.CanRestoreInactiveState(
                baseline, new[] { @"HID\BASELINE", @"HID\FOREIGN" }));
        }

        [TestMethod]
        public void InterruptedRunBecomesRecoveryRequiredNotOwnedCleanup()
        {
            string root = CreateTemporaryRoot();
            string path = Path.Combine(root, HidHideOwnershipJournal.FileName);
            try
            {
                HidHideOwnershipJournal first = new(path);
                Assert.IsTrue(first.BeginTransientRun());
                Assert.IsTrue(first.RecordPersistentBlacklistEntry(
                    @"HID\INTERRUPTED"));
                Assert.IsTrue(first.RecordActiveStateEnabled());

                HidHideOwnershipJournal recovered = new(path);
                Assert.IsTrue(recovered.Load());
                Assert.IsTrue(recovered.RecoveryRequired);
                CollectionAssert.Contains(recovered.
                    UnresolvedPersistentBlacklistEntries.ToList(),
                    @"HID\INTERRUPTED");

                List<string> preserved = HidHideOwnershipPolicy.RemoveOwned(
                    new[] { @"HID\INTERRUPTED", @"HID\USER" },
                    Array.Empty<string>(), out List<string> removed);
                Assert.AreEqual(0, removed.Count);
                CollectionAssert.AreEquivalent(
                    new[] { @"HID\INTERRUPTED", @"HID\USER" }, preserved);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public void CleanRunClearsTransientJournalState()
        {
            string root = CreateTemporaryRoot();
            string path = Path.Combine(root, HidHideOwnershipJournal.FileName);
            try
            {
                HidHideOwnershipJournal journal = new(path);
                Assert.IsTrue(journal.BeginTransientRun());
                Assert.IsTrue(journal.RecordPersistentBlacklistEntry(
                    @"HID\CLEAN"));
                Assert.IsTrue(journal.RecordActiveStateEnabled());
                Assert.IsTrue(journal.CompleteTransientRun(
                    new[] { @"HID\CLEAN" }, activeStateRestored: true));
                Assert.IsFalse(File.Exists(path));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public void WhitelistOwnershipPersistsOnlyForRecordedEntries()
        {
            string root = CreateTemporaryRoot();
            string path = Path.Combine(root, HidHideOwnershipJournal.FileName);
            try
            {
                HidHideOwnershipJournal journal = new(path);
                Assert.IsTrue(journal.RecordWhitelistEntry(
                    @"\Device\HarddiskVolume1\Owned.exe"));

                HidHideOwnershipJournal reloaded = new(path);
                Assert.IsTrue(reloaded.Load());
                Assert.IsTrue(reloaded.OwnsWhitelistEntry(
                    @"\device\harddiskvolume1\owned.exe"));
                Assert.IsFalse(reloaded.OwnsWhitelistEntry(
                    @"\Device\HarddiskVolume1\PreExisting.exe"));
                Assert.IsTrue(reloaded.ForgetWhitelistEntry(
                    @"\Device\HarddiskVolume1\Owned.exe"));
                Assert.IsFalse(File.Exists(path));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public void CorruptJournalFailsClosed()
        {
            string root = CreateTemporaryRoot();
            string path = Path.Combine(root, HidHideOwnershipJournal.FileName);
            try
            {
                File.WriteAllText(path, "not-json");
                HidHideOwnershipJournal journal = new(path);

                Assert.IsFalse(journal.Load());
                Assert.IsFalse(journal.IsReliable);
                Assert.IsFalse(journal.BeginTransientRun());
                Assert.IsFalse(journal.RecordWhitelistEntry(
                    @"\Device\HarddiskVolume1\Unsafe.exe"));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        private static string CreateTemporaryRoot()
        {
            string root = Path.Combine(Path.GetTempPath(),
                "DS4WindowsTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return root;
        }
    }
}
