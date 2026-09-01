using DS4Windows;
using DS4WinWPF.DS4Control;

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
        public void SessionMigrationAddsOnlyMissingOwnedEntries()
        {
            HidHideSessionMigrationPlan plan =
                HidHideSessionMigrationPolicy.Create(
                    new[] { @"HID\EXISTING", @"HID\PERSISTENT" },
                    new[] { @"HID\SESSION", @"hid\persistent" },
                    new[] { @"HID\PERSISTENT" });

            Assert.IsTrue(plan.CanMigrate);
            CollectionAssert.AreEqual(new[] { @"HID\SESSION" },
                plan.EntriesToAdd.ToArray());
        }

        [TestMethod]
        public void SessionMigrationFailsClosedOnForeignPersistentCollision()
        {
            HidHideSessionMigrationPlan plan =
                HidHideSessionMigrationPolicy.Create(
                    new[] { @"HID\SESSION" },
                    new[] { @"hid\session" },
                    Array.Empty<string>());

            Assert.IsFalse(plan.CanMigrate);
            Assert.AreEqual(0, plan.EntriesToAdd.Count);
            StringAssert.Contains(plan.Error, "ownership changed");
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
        public void WhitelistReconciliationRemovesOnlyProvenMissingOwnedPaths()
        {
            const string missingOwned =
                @"\Device\HarddiskVolume1\PureDS4\old.exe";
            const string presentOwned =
                @"\Device\HarddiskVolume1\PureDS4\current.exe";
            const string unowned =
                @"\Device\HarddiskVolume1\Other\missing.exe";

            HidHideWhitelistReconciliationPlan plan =
                HidHideWhitelistReconciliationPolicy.Create(
                    new[] { missingOwned, presentOwned, unowned },
                    new[] { missingOwned, presentOwned },
                    path => path == missingOwned
                        ? HidHideOwnedApplicationPathState.Missing
                        : HidHideOwnedApplicationPathState.Present);

            CollectionAssert.AreEqual(new[] { missingOwned },
                plan.EntriesToRemove.ToArray());
            CollectionAssert.AreEqual(new[] { missingOwned },
                plan.JournalEntriesToForget.ToArray());
            CollectionAssert.DoesNotContain(plan.EntriesToRemove.ToList(),
                unowned);
        }

        [TestMethod]
        public void ForgettingWhitelistEntriesPersistsRemainingOwnership()
        {
            string root = CreateTemporaryRoot();
            string path = Path.Combine(root, HidHideOwnershipJournal.FileName);
            try
            {
                HidHideOwnershipJournal journal = new(path);
                Assert.IsTrue(journal.RecordWhitelistEntry(
                    @"\Device\HarddiskVolume1\old.exe"));
                Assert.IsTrue(journal.RecordWhitelistEntry(
                    @"\Device\HarddiskVolume1\current.exe"));
                Assert.IsTrue(journal.ForgetWhitelistEntries(new[]
                {
                    @"\device\harddiskvolume1\OLD.exe",
                }));

                HidHideOwnershipJournal reloaded = new(path);
                Assert.IsTrue(reloaded.Load());
                Assert.IsFalse(reloaded.OwnsWhitelistEntry(
                    @"\Device\HarddiskVolume1\old.exe"));
                Assert.IsTrue(reloaded.OwnsWhitelistEntry(
                    @"\Device\HarddiskVolume1\current.exe"));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public void UnknownOwnedPathIsPreservedFailClosed()
        {
            const string owned = @"\Device\UnmountedVolume\PureDS4.exe";

            HidHideWhitelistReconciliationPlan plan =
                HidHideWhitelistReconciliationPolicy.Create(
                    new[] { owned }, new[] { owned }, _ =>
                        HidHideOwnedApplicationPathState.Unknown);

            Assert.AreEqual(0, plan.EntriesToRemove.Count);
            Assert.AreEqual(0, plan.JournalEntriesToForget.Count);
        }

        [TestMethod]
        public void JournalEntryAbsentFromWhitelistIsForgottenWithoutMutation()
        {
            const string owned = @"\Device\HarddiskVolume1\old.exe";

            HidHideWhitelistReconciliationPlan plan =
                HidHideWhitelistReconciliationPolicy.Create(
                    new[] { @"\Device\HarddiskVolume1\unrelated.exe" },
                    new[] { owned }, _ =>
                        HidHideOwnedApplicationPathState.Missing);

            Assert.AreEqual(0, plan.EntriesToRemove.Count);
            CollectionAssert.AreEqual(new[] { owned },
                plan.JournalEntriesToForget.ToArray());
        }

        [TestMethod]
        public void WhitelistMutationRemovesExactOwnedEntryOnly()
        {
            const string owned = @"\Device\HarddiskVolume1\old.exe";
            const string unrelated =
                @"\Device\HarddiskVolume1\unrelated.exe";
            FakeWhitelistDevice device = new(new[] { owned, unrelated });

            HidHideWhitelistMutationResult result =
                HidHideWhitelistMutationGateway.RemoveExact(device,
                    new[] { owned }, useMachineMutex: false);

            Assert.IsTrue(result.Succeeded, result.Error);
            Assert.IsTrue(result.Changed);
            CollectionAssert.AreEqual(new[] { unrelated },
                result.After.ToArray());
            Assert.AreEqual(1, device.WriteCount);
        }

        [TestMethod]
        public void UnexpectedPostWriteWhitelistDeltaIsReportedAsFailure()
        {
            const string owned = @"\Device\HarddiskVolume1\old.exe";
            FakeWhitelistDevice device = new(new[] { owned })
            {
                AddUnexpectedEntryAfterWrite = true,
            };

            HidHideWhitelistMutationResult result =
                HidHideWhitelistMutationGateway.RemoveExact(device,
                    new[] { owned }, useMachineMutex: false);

            Assert.IsFalse(result.Succeeded);
            Assert.IsTrue(result.Changed);
            StringAssert.Contains(result.Error, "exact");
            CollectionAssert.Contains(result.After.ToList(),
                @"\Device\HarddiskVolume1\foreign.exe");
        }

        [TestMethod]
        public void ConcurrentWhitelistChangeFailsBeforeCleanupWrite()
        {
            const string owned = @"\Device\HarddiskVolume1\old.exe";
            FakeWhitelistDevice device = new(new[] { owned })
            {
                ChangeBeforeSecondRead = true,
            };

            HidHideWhitelistMutationResult result =
                HidHideWhitelistMutationGateway.RemoveExact(device,
                    new[] { owned }, useMachineMutex: false);

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(0, device.WriteCount);
            StringAssert.Contains(result.Error, "concurrently");
            CollectionAssert.Contains(result.After.ToList(),
                @"\Device\HarddiskVolume1\foreign.exe");
        }

        [TestMethod]
        public void DosPathInspectorDistinguishesPresentAndMissingFiles()
        {
            string root = CreateTemporaryRoot();
            string present = Path.Combine(root, "PureDS4.exe");
            string missing = Path.Combine(root, "missing.exe");
            try
            {
                File.WriteAllText(present, "test");

                Assert.AreEqual(HidHideOwnedApplicationPathState.Present,
                    HidHideOwnedApplicationPathInspector.Inspect(present));
                Assert.AreEqual(HidHideOwnedApplicationPathState.Missing,
                    HidHideOwnedApplicationPathInspector.Inspect(missing));
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

        [TestMethod]
        public void ExternalSuspensionIsDurableRestoreObligationNotOwnership()
        {
            string root = CreateTemporaryRoot();
            string path = Path.Combine(root, HidHideOwnershipJournal.FileName);
            const string external = @"HID\EXTERNAL";
            try
            {
                HidHideOwnershipJournal active = new(path);
                Assert.IsTrue(active.RecordExternalContainmentSuspensionIntent(
                    external, activeStateObserved: true,
                    inverseStateObserved: false));
                Assert.IsTrue(active.MarkExternalContainmentSuspended(external));
                Assert.IsFalse(active.RecoveryRequired,
                    "A live session owns its restore path.");
                Assert.AreEqual(HidHideExternalSuspensionState.Suspended,
                    active.ExternalContainmentSuspensions.Single().State);
                Assert.AreEqual(0,
                    active.UnresolvedPersistentBlacklistEntries.Count);

                HidHideOwnershipJournal recovered = new(path);
                Assert.IsTrue(recovered.Load());
                Assert.IsTrue(recovered.RecoveryRequired);
                HidHideExternalSuspensionInfo suspension = recovered.
                    ExternalContainmentSuspensions.Single();
                Assert.AreEqual(external, suspension.InstanceId);
                Assert.AreEqual(HidHideExternalSuspensionState.Suspended,
                    suspension.State);
                Assert.IsTrue(suspension.ActiveStateObserved);
                Assert.IsFalse(suspension.InverseStateObserved);
                Assert.AreEqual(0,
                    recovered.UnresolvedPersistentBlacklistEntries.Count,
                    "External configuration must never enter owned cleanup.");

                Assert.IsTrue(recovered.
                    CompleteExternalContainmentSuspension(external));
                Assert.IsFalse(recovered.RecoveryRequired);
                Assert.IsFalse(File.Exists(path));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public void DuplicateExternalSuspensionIntentFailsClosed()
        {
            string root = CreateTemporaryRoot();
            string path = Path.Combine(root, HidHideOwnershipJournal.FileName);
            try
            {
                HidHideOwnershipJournal journal = new(path);
                Assert.IsTrue(journal.RecordExternalContainmentSuspensionIntent(
                    @"HID\EXTERNAL", true, false));
                Assert.IsFalse(journal.RecordExternalContainmentSuspensionIntent(
                    @"hid\external", true, false));
                Assert.AreEqual(1,
                    journal.ExternalContainmentSuspensions.Count);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public void VerifiedMutationWritesIntentBeforeWholeListUpdate()
        {
            FakeBlacklistDevice device = new(
                new[] { @"HID\EXTERNAL", @"HID\UNRELATED" });
            bool intentRecorded = false;

            HidHideBlacklistMutationResult result =
                HidHideBlacklistMutationGateway.Mutate(device,
                    current => HidHideBlacklistMutationGateway.RemoveExact(
                        current, @"hid\external"),
                    () =>
                    {
                        intentRecorded = true;
                        device.Events.Add("intent");
                        return true;
                    }, useMachineMutex: false);

            Assert.IsTrue(result.Succeeded);
            Assert.IsTrue(result.Changed);
            Assert.IsTrue(result.WriteAttempted);
            Assert.IsTrue(intentRecorded);
            CollectionAssert.AreEqual(
                new[] { "read", "intent", "read", "write", "read" },
                device.Events);
            CollectionAssert.AreEqual(new[] { @"HID\UNRELATED" },
                result.After.ToArray());
        }

        [TestMethod]
        public void NoOpMutationDoesNotCreateRecoveryIntent()
        {
            FakeBlacklistDevice device = new(new[] { @"HID\UNRELATED" });
            bool intentRecorded = false;

            HidHideBlacklistMutationResult result =
                HidHideBlacklistMutationGateway.Mutate(device,
                    current => HidHideBlacklistMutationGateway.RemoveExact(
                        current, @"HID\MISSING"),
                    () => intentRecorded = true,
                    useMachineMutex: false);

            Assert.IsTrue(result.Succeeded);
            Assert.IsFalse(result.Changed);
            Assert.IsFalse(result.WriteAttempted);
            Assert.IsFalse(intentRecorded);
            Assert.AreEqual(0, device.WriteCount);
        }

        [TestMethod]
        public void MachineMutationMutexIsAvailableToNormalProcess()
        {
            FakeBlacklistDevice device = new(new[] { @"HID\BASELINE" });

            HidHideBlacklistMutationResult result =
                HidHideBlacklistMutationGateway.Mutate(device,
                    current => current.ToArray());

            Assert.IsTrue(result.Succeeded, result.Error);
            Assert.IsFalse(result.Changed);
            Assert.AreEqual(0, device.WriteCount);
        }

        [TestMethod]
        public void ConcurrentBlacklistChangeFailsBeforeWrite()
        {
            FakeBlacklistDevice device = new(new[] { @"HID\EXTERNAL" })
            {
                ChangeBeforeSecondRead = true,
            };

            HidHideBlacklistMutationResult result =
                HidHideBlacklistMutationGateway.Mutate(device,
                    current => HidHideBlacklistMutationGateway.RemoveExact(
                        current, @"HID\EXTERNAL"),
                    () => true, useMachineMutex: false);

            Assert.IsFalse(result.Succeeded);
            Assert.IsFalse(result.WriteAttempted);
            Assert.AreEqual(0, device.WriteCount);
            StringAssert.Contains(result.Error, "concurrently");
            CollectionAssert.Contains(result.After.ToList(), @"HID\FOREIGN");
        }

        [TestMethod]
        public void PostWriteVerificationRejectsUnexpectedDelta()
        {
            FakeBlacklistDevice device = new(new[] { @"HID\EXTERNAL" })
            {
                AddUnexpectedEntryAfterWrite = true,
            };

            HidHideBlacklistMutationResult result =
                HidHideBlacklistMutationGateway.Mutate(device,
                    current => HidHideBlacklistMutationGateway.RemoveExact(
                        current, @"HID\EXTERNAL"),
                    () => true, useMachineMutex: false);

            Assert.IsFalse(result.Succeeded);
            Assert.IsTrue(result.WriteAttempted);
            Assert.IsTrue(result.Changed);
            StringAssert.Contains(result.Error, "exact verified");
            CollectionAssert.Contains(result.After.ToList(), @"HID\FOREIGN");
        }

        [TestMethod]
        public void RecoveryAddsOnlyExactExternalEntryToFreshState()
        {
            FakeBlacklistDevice device = new(
                new[] { @"HID\BASELINE", @"HID\FOREIGN" });

            HidHideBlacklistMutationResult result =
                HidHideBlacklistMutationGateway.Mutate(device,
                    current => HidHideBlacklistMutationGateway.AddExact(
                        current, @"HID\EXTERNAL"),
                    useMachineMutex: false);

            Assert.IsTrue(result.Succeeded);
            Assert.IsTrue(result.Changed);
            CollectionAssert.AreEquivalent(
                new[] { @"HID\BASELINE", @"HID\FOREIGN", @"HID\EXTERNAL" },
                result.After.ToArray());
        }

        private static string CreateTemporaryRoot()
        {
            string root = Path.Combine(Path.GetTempPath(),
                "DS4WindowsTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return root;
        }

        private sealed class FakeBlacklistDevice : IHidHideBlacklistDevice
        {
            private List<string> entries;
            private int readCount;

            internal FakeBlacklistDevice(IEnumerable<string> entries)
            {
                this.entries = entries.ToList();
            }

            internal bool ChangeBeforeSecondRead { get; init; }
            internal bool AddUnexpectedEntryAfterWrite { get; init; }
            internal int WriteCount { get; private set; }
            internal List<string> Events { get; } = new();

            public List<string> GetBlacklist()
            {
                readCount++;
                Events.Add("read");
                if (ChangeBeforeSecondRead && readCount == 2)
                {
                    entries.Add(@"HID\FOREIGN");
                }
                return entries.ToList();
            }

            public bool SetBlacklist(List<string> instances)
            {
                Events.Add("write");
                WriteCount++;
                entries = instances.ToList();
                if (AddUnexpectedEntryAfterWrite)
                {
                    entries.Add(@"HID\FOREIGN");
                }
                return true;
            }
        }

        private sealed class FakeWhitelistDevice : IHidHideWhitelistDevice
        {
            private List<string> entries;
            private int readCount;

            internal FakeWhitelistDevice(IEnumerable<string> entries)
            {
                this.entries = entries.ToList();
            }

            internal bool ChangeBeforeSecondRead { get; init; }
            internal bool AddUnexpectedEntryAfterWrite { get; init; }
            internal int WriteCount { get; private set; }

            public List<string> GetWhitelist()
            {
                readCount++;
                if (ChangeBeforeSecondRead && readCount == 2)
                {
                    entries.Add(
                        @"\Device\HarddiskVolume1\foreign.exe");
                }
                return entries.ToList();
            }

            public bool SetWhitelist(List<string> instances)
            {
                WriteCount++;
                entries = instances.ToList();
                if (AddUnexpectedEntryAfterWrite)
                {
                    entries.Add(
                        @"\Device\HarddiskVolume1\foreign.exe");
                }
                return true;
            }
        }
    }
}
