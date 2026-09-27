using DS4Windows;
using DS4WinWPF.DS4Control;
using System.Text;

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
        public void InterruptedRunCanRecoverExactIdWithoutTouchingForeignRules()
        {
            string root = CreateTemporaryRoot();
            string path = Path.Combine(root, HidHideOwnershipJournal.FileName);
            try
            {
                HidHideOwnershipJournal first = new(path);
                Assert.IsTrue(first.BeginTransientRun());
                Assert.IsTrue(first.RecordPersistentBlacklistEntry(
                    @"HID\OWNED"));
                Assert.IsTrue(first.RecordActiveStateEnabled());

                HidHideOwnershipJournal recovered = new(path);
                Assert.IsTrue(recovered.Load());
                FakeBlacklistDevice device = new(
                    new[] { @"HID\OWNED", @"HID\FOREIGN" });
                HidHideRecoveryPreview preview =
                    HidHidePersistentRecovery.Inspect(device, recovered);

                Assert.IsTrue(preview.CanRecover, preview.Error);
                CollectionAssert.AreEqual(new[] { @"HID\OWNED" },
                    preview.Present.ToArray());
                Assert.IsTrue(preview.ActiveStateUncertain);
                Assert.AreEqual(string.Empty,
                    HidHidePersistentRecovery.Complete(device, recovered,
                        preview, useMachineMutex: false));
                CollectionAssert.AreEqual(new[] { @"HID\FOREIGN" },
                    device.GetBlacklist());
                Assert.IsFalse(recovered.RecoveryRequired);

                HidHideOwnershipJournal reopened = new(path);
                Assert.IsTrue(reopened.Load());
                Assert.IsFalse(reopened.RecoveryRequired);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public void IntentOnlyCrashAcknowledgesAlreadyAbsentIdWithoutWriting()
        {
            string root = CreateTemporaryRoot();
            string path = Path.Combine(root, HidHideOwnershipJournal.FileName);
            try
            {
                HidHideOwnershipJournal first = new(path);
                Assert.IsTrue(first.BeginTransientRun());
                Assert.IsTrue(first.RecordPersistentBlacklistEntry(
                    @"HID\NEVER_WRITTEN"));
                HidHideOwnershipJournal recovered = new(path);
                Assert.IsTrue(recovered.Load());
                FakeBlacklistDevice device = new(new[] { @"HID\FOREIGN" });

                HidHideRecoveryPreview preview =
                    HidHidePersistentRecovery.Inspect(device, recovered);
                Assert.IsTrue(preview.CanRecover, preview.Error);
                Assert.AreEqual(0, preview.Present.Count);
                Assert.AreEqual(string.Empty,
                    HidHidePersistentRecovery.Complete(device, recovered,
                        preview, useMachineMutex: false));
                Assert.AreEqual(0, device.WriteCount);
                CollectionAssert.AreEqual(new[] { @"HID\FOREIGN" },
                    device.GetBlacklist());
                Assert.IsFalse(recovered.RecoveryRequired);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public void RecoveryHandlesPresentBluetoothAndAbsentUsbIdsIndividually()
        {
            string root = CreateTemporaryRoot();
            string path = Path.Combine(root, HidHideOwnershipJournal.FileName);
            try
            {
                HidHideOwnershipJournal first = new(path);
                Assert.IsTrue(first.BeginTransientRun());
                Assert.IsTrue(first.RecordPersistentBlacklistEntry(
                    @"HID\DS4_USB_OLD"));
                Assert.IsTrue(first.RecordPersistentBlacklistEntry(
                    @"HID\DS4_BT_CURRENT"));
                HidHideOwnershipJournal recovered = new(path);
                Assert.IsTrue(recovered.Load());
                FakeBlacklistDevice device = new(new[]
                {
                    @"HID\DS4_BT_CURRENT", @"HID\OTHER_CONTROLLER",
                });

                HidHideRecoveryPreview preview =
                    HidHidePersistentRecovery.Inspect(device, recovered);
                Assert.IsTrue(preview.CanRecover, preview.Error);
                CollectionAssert.AreEqual(new[] { @"HID\DS4_BT_CURRENT" },
                    preview.Present.ToArray());
                Assert.AreEqual(string.Empty,
                    HidHidePersistentRecovery.Complete(device, recovered,
                        preview, useMachineMutex: false));
                CollectionAssert.AreEqual(new[] { @"HID\OTHER_CONTROLLER" },
                    device.GetBlacklist());
                Assert.IsFalse(recovered.RecoveryRequired);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public void RecoveryInspectionFailureCannotWriteOrClearRecord()
        {
            string root = CreateTemporaryRoot();
            string path = Path.Combine(root, HidHideOwnershipJournal.FileName);
            try
            {
                HidHideOwnershipJournal first = new(path);
                Assert.IsTrue(first.BeginTransientRun());
                Assert.IsTrue(first.RecordPersistentBlacklistEntry(
                    @"HID\OWNED"));
                HidHideOwnershipJournal recovered = new(path);
                Assert.IsTrue(recovered.Load());
                FakeBlacklistDevice device = new(new[] { @"HID\OWNED" })
                {
                    ThrowOnReadNumber = 1,
                };

                HidHideRecoveryPreview preview =
                    HidHidePersistentRecovery.Inspect(device, recovered);
                Assert.IsFalse(preview.CanRecover);
                Assert.AreNotEqual(string.Empty,
                    HidHidePersistentRecovery.Complete(device, recovered,
                        preview, useMachineMutex: false));
                Assert.AreEqual(0, device.WriteCount);
                Assert.IsTrue(recovered.RecoveryRequired);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public void ActiveStateOnlyRecoveryAcknowledgesWithoutDriverWrite()
        {
            string root = CreateTemporaryRoot();
            string path = Path.Combine(root, HidHideOwnershipJournal.FileName);
            try
            {
                HidHideOwnershipJournal first = new(path);
                Assert.IsTrue(first.BeginTransientRun());
                Assert.IsTrue(first.RecordActiveStateEnabled());
                HidHideOwnershipJournal recovered = new(path);
                Assert.IsTrue(recovered.Load());
                FakeBlacklistDevice device = new(new[] { @"HID\FOREIGN" });

                HidHideRecoveryPreview preview =
                    HidHidePersistentRecovery.Inspect(device, recovered);
                Assert.IsTrue(preview.CanRecover, preview.Error);
                Assert.IsTrue(preview.ActiveStateUncertain);
                Assert.AreEqual(0, preview.Pending.Count);
                Assert.AreEqual(string.Empty,
                    HidHidePersistentRecovery.Complete(device, recovered,
                        preview, useMachineMutex: false));
                Assert.AreEqual(0, device.WriteCount);
                Assert.IsFalse(recovered.RecoveryRequired);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public void ActiveStateChangeAfterPreviewRefusesRecovery()
        {
            string root = CreateTemporaryRoot();
            string path = Path.Combine(root, HidHideOwnershipJournal.FileName);
            try
            {
                HidHideOwnershipJournal first = new(path);
                Assert.IsTrue(first.BeginTransientRun());
                Assert.IsTrue(first.RecordPersistentBlacklistEntry(
                    @"HID\OWNED"));
                HidHideOwnershipJournal recovered = new(path);
                Assert.IsTrue(recovered.Load());
                FakeBlacklistDevice device = new(new[] { @"HID\OWNED" });
                bool active = true;
                HidHideRecoveryPreview preview =
                    HidHidePersistentRecovery.Inspect(device, recovered,
                        () => active);
                active = false;

                StringAssert.Contains(
                    HidHidePersistentRecovery.Complete(device, recovered,
                        preview, useMachineMutex: false,
                        readActiveState: () => active),
                    "active setting changed");
                Assert.AreEqual(0, device.WriteCount);
                Assert.IsTrue(recovered.RecoveryRequired);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public void ChangedBlacklistAfterPreviewRefusesRecoveryAndKeepsJournal()
        {
            string root = CreateTemporaryRoot();
            string path = Path.Combine(root, HidHideOwnershipJournal.FileName);
            try
            {
                HidHideOwnershipJournal first = new(path);
                Assert.IsTrue(first.BeginTransientRun());
                Assert.IsTrue(first.RecordPersistentBlacklistEntry(
                    @"HID\OWNED"));
                HidHideOwnershipJournal recovered = new(path);
                Assert.IsTrue(recovered.Load());
                FakeBlacklistDevice device = new(new[] { @"HID\OWNED" });
                HidHideRecoveryPreview preview =
                    HidHidePersistentRecovery.Inspect(device, recovered);
                device.AddEntry(@"HID\FOREIGN");

                StringAssert.Contains(
                    HidHidePersistentRecovery.Complete(device, recovered,
                        preview, useMachineMutex: false),
                    "changed after inspection");
                Assert.AreEqual(0, device.WriteCount);
                Assert.IsTrue(recovered.RecoveryRequired);
                CollectionAssert.AreEquivalent(
                    new[] { @"HID\OWNED", @"HID\FOREIGN" },
                    device.GetBlacklist());
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public void UncertainRecoveryWriteCanBeRetriedAfterFreshInspection()
        {
            string root = CreateTemporaryRoot();
            string path = Path.Combine(root, HidHideOwnershipJournal.FileName);
            try
            {
                HidHideOwnershipJournal first = new(path);
                Assert.IsTrue(first.BeginTransientRun());
                Assert.IsTrue(first.RecordPersistentBlacklistEntry(
                    @"HID\OWNED"));
                HidHideOwnershipJournal recovered = new(path);
                Assert.IsTrue(recovered.Load());
                FakeBlacklistDevice uncertain = new(new[] { @"HID\OWNED" })
                {
                    ThrowAfterWrite = true,
                };
                HidHideRecoveryPreview preview =
                    HidHidePersistentRecovery.Inspect(uncertain, recovered);

                Assert.AreNotEqual(string.Empty,
                    HidHidePersistentRecovery.Complete(uncertain, recovered,
                        preview, useMachineMutex: false));
                Assert.IsTrue(recovered.RecoveryRequired);

                FakeBlacklistDevice nowAbsent = new(
                    uncertain.GetBlacklist());
                HidHideRecoveryPreview retry =
                    HidHidePersistentRecovery.Inspect(nowAbsent, recovered);
                Assert.AreEqual(0, retry.Present.Count);
                Assert.AreEqual(string.Empty,
                    HidHidePersistentRecovery.Complete(nowAbsent, recovered,
                        retry, useMachineMutex: false));
                Assert.AreEqual(0, nowAbsent.WriteCount);
                Assert.IsFalse(recovered.RecoveryRequired);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public void StoppedIncompleteRunBecomesPersistentRecoveryObligation()
        {
            string root = CreateTemporaryRoot();
            string path = Path.Combine(root, HidHideOwnershipJournal.FileName);
            try
            {
                HidHideOwnershipJournal journal = new(path);
                Assert.IsTrue(journal.BeginTransientRun());
                Assert.IsTrue(journal.RecordPersistentBlacklistEntry(
                    @"HID\STOP_FAILED"));
                Assert.IsTrue(journal.RecordActiveStateEnabled());
                Assert.IsTrue(journal.MarkStoppedRunRecoveryRequired());
                Assert.IsFalse(journal.IsTransientRunInProgress);
                Assert.IsTrue(journal.RecoveryRequired);

                HidHideOwnershipJournal reopened = new(path);
                Assert.IsTrue(reopened.Load());
                CollectionAssert.Contains(reopened.
                    UnresolvedPersistentBlacklistEntries.ToList(),
                    @"HID\STOP_FAILED");
                Assert.IsTrue(reopened.ActiveStateRecoveryRequired);
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
        public void UnreadableInitialBlacklistDoesNotCreateIntentOrWrite()
        {
            FakeBlacklistDevice device = new(new[] { @"HID\BASELINE" })
            {
                ThrowOnReadNumber = 1,
            };
            bool intentRecorded = false;

            HidHideBlacklistMutationResult result =
                HidHideBlacklistMutationGateway.Mutate(device,
                    current => HidHideBlacklistMutationGateway.AddExact(
                        current, @"HID\NEW"),
                    () => intentRecorded = true, useMachineMutex: false);

            Assert.IsFalse(result.Succeeded);
            Assert.IsFalse(result.WriteAttempted);
            Assert.IsFalse(result.AfterKnown);
            Assert.IsFalse(intentRecorded);
            Assert.AreEqual(0, device.WriteCount);
        }

        [TestMethod]
        public void NullBlacklistOrDesiredConfigurationNeverWrites()
        {
            FakeBlacklistDevice device = new(new[] { @"HID\BASELINE" })
            {
                ReturnNullOnReadNumber = 1,
            };

            HidHideBlacklistMutationResult unreadable =
                HidHideBlacklistMutationGateway.Mutate(device,
                    current => new[] { @"HID\NEW" },
                    useMachineMutex: false);
            Assert.IsFalse(unreadable.Succeeded);
            Assert.IsFalse(unreadable.WriteAttempted);
            Assert.AreEqual(0, device.WriteCount);

            FakeBlacklistDevice second = new(new[] { @"HID\BASELINE" });
            HidHideBlacklistMutationResult noDesired =
                HidHideBlacklistMutationGateway.Mutate(second,
                    _ => null, useMachineMutex: false);
            Assert.IsFalse(noDesired.Succeeded);
            Assert.IsFalse(noDesired.WriteAttempted);
            Assert.AreEqual(0, second.WriteCount);
        }

        [TestMethod]
        public void UnreadablePreWriteBlacklistDoesNotWrite()
        {
            FakeBlacklistDevice device = new(new[] { @"HID\BASELINE" })
            {
                ThrowOnReadNumber = 2,
            };
            bool intentRecorded = false;

            HidHideBlacklistMutationResult result =
                HidHideBlacklistMutationGateway.Mutate(device,
                    current => HidHideBlacklistMutationGateway.AddExact(
                        current, @"HID\NEW"),
                    () => intentRecorded = true, useMachineMutex: false);

            Assert.IsFalse(result.Succeeded);
            Assert.IsFalse(result.WriteAttempted);
            Assert.IsTrue(intentRecorded);
            CollectionAssert.AreEqual(new[] { @"HID\BASELINE" },
                result.Before.ToArray());
            Assert.AreEqual(0, device.WriteCount);
        }

        [TestMethod]
        public void FailedIntentWritePreventsBlacklistMutation()
        {
            FakeBlacklistDevice device = new(new[] { @"HID\BASELINE" });

            HidHideBlacklistMutationResult result =
                HidHideBlacklistMutationGateway.Mutate(device,
                    current => HidHideBlacklistMutationGateway.AddExact(
                        current, @"HID\NEW"),
                    () => false, useMachineMutex: false);

            Assert.IsFalse(result.Succeeded);
            Assert.IsFalse(result.WriteAttempted);
            Assert.IsTrue(result.AfterKnown);
            Assert.AreEqual(0, device.WriteCount);
        }

        [TestMethod]
        public void SetterThatAppliesThenThrowsKeepsDurableRecoveryIntent()
        {
            string root = CreateTemporaryRoot();
            try
            {
                string path = Path.Combine(root,
                    HidHideOwnershipJournal.FileName);
                HidHideOwnershipJournal journal = new(path);
                Assert.IsTrue(journal.BeginTransientRun());
                FakeBlacklistDevice device = new(new[] { @"HID\BASELINE" })
                {
                    ThrowAfterWrite = true,
                };
                bool intentRecorded = false;

                HidHideBlacklistMutationResult result =
                    HidHideBlacklistMutationGateway.Mutate(device,
                        current => HidHideBlacklistMutationGateway.AddExact(
                            current, @"HID\NEW"),
                        () => intentRecorded =
                            journal.RecordPersistentBlacklistEntry(@"HID\NEW"),
                        useMachineMutex: false);
                if (intentRecorded && !result.WriteAttempted)
                {
                    Assert.IsTrue(journal.CompleteTransientRun(
                        new[] { @"HID\NEW" }, false));
                }

                Assert.IsFalse(result.Succeeded);
                Assert.IsTrue(result.WriteAttempted);
                Assert.IsFalse(result.AfterKnown);
                Assert.AreEqual(1, device.WriteCount);
                CollectionAssert.Contains(device.GetBlacklist(), @"HID\NEW");
                CollectionAssert.AreEqual(new[] { @"HID\BASELINE" },
                    result.Before.ToArray());

                HidHideOwnershipJournal recovered = new(path);
                Assert.IsTrue(recovered.Load());
                Assert.IsTrue(recovered.RecoveryRequired);
                CollectionAssert.Contains(recovered.
                    UnresolvedPersistentBlacklistEntries.ToList(),
                    @"HID\NEW");
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public void PostWriteReadFailurePreservesWriteAttemptAndBaseline()
        {
            FakeBlacklistDevice device = new(new[] { @"HID\BASELINE" })
            {
                ThrowOnReadNumber = 3,
            };

            HidHideBlacklistMutationResult result =
                HidHideBlacklistMutationGateway.Mutate(device,
                    current => HidHideBlacklistMutationGateway.AddExact(
                        current, @"HID\NEW"),
                    () => true, useMachineMutex: false);

            Assert.IsFalse(result.Succeeded);
            Assert.IsTrue(result.WriteAttempted);
            Assert.IsFalse(result.AfterKnown);
            Assert.AreEqual(1, device.WriteCount);
            CollectionAssert.AreEqual(new[] { @"HID\BASELINE" },
                result.Before.ToArray());
        }

        [TestMethod]
        public void RejectedSetterWithUnreadableOutcomeStillReportsAttempt()
        {
            FakeBlacklistDevice device = new(new[] { @"HID\BASELINE" })
            {
                ReturnFalseAfterWrite = true,
                ThrowOnReadNumber = 3,
            };

            HidHideBlacklistMutationResult result =
                HidHideBlacklistMutationGateway.Mutate(device,
                    current => HidHideBlacklistMutationGateway.AddExact(
                        current, @"HID\NEW"),
                    () => true, useMachineMutex: false);

            Assert.IsFalse(result.Succeeded);
            Assert.IsTrue(result.WriteAttempted);
            Assert.IsFalse(result.AfterKnown);
            Assert.AreEqual(1, device.WriteCount);
        }

        [TestMethod]
        public void UnreadableWhitelistDoesNotTriggerCleanupWrite()
        {
            foreach (int failedRead in new[] { 1, 2 })
            {
                FakeWhitelistDevice device = new(
                    new[] { @"\Device\HarddiskVolume1\pureds4.exe" })
                {
                    ThrowOnReadNumber = failedRead,
                };

                HidHideWhitelistMutationResult result =
                    HidHideWhitelistMutationGateway.RemoveExact(device,
                        new[] { @"\Device\HarddiskVolume1\pureds4.exe" },
                        useMachineMutex: false);

                Assert.IsFalse(result.Succeeded);
                Assert.AreEqual(0, device.WriteCount);
            }

            FakeWhitelistDevice nullDevice = new(
                new[] { @"\Device\HarddiskVolume1\pureds4.exe" })
            {
                ReturnNullOnReadNumber = 1,
            };
            HidHideWhitelistMutationResult nullResult =
                HidHideWhitelistMutationGateway.RemoveExact(nullDevice,
                    new[] { @"\Device\HarddiskVolume1\pureds4.exe" },
                    useMachineMutex: false);
            Assert.IsFalse(nullResult.Succeeded);
            Assert.AreEqual(0, nullDevice.WriteCount);
        }

        [TestMethod]
        public void HidHideStringListRejectsPartialOrUnterminatedReads()
        {
            CollectionAssert.AreEqual(Array.Empty<string>(),
                HidHideAPIDevice.ParseMultiSz(
                    Encoding.Unicode.GetBytes("\0")));
            CollectionAssert.AreEqual(Array.Empty<string>(),
                HidHideAPIDevice.ParseMultiSz(
                    Encoding.Unicode.GetBytes("\0\0")));
            CollectionAssert.AreEqual(new[] { "one", "two" },
                HidHideAPIDevice.ParseMultiSz(
                    Encoding.Unicode.GetBytes("one\0two\0\0")));
            Assert.ThrowsException<InvalidDataException>(() =>
                HidHideAPIDevice.ParseMultiSz(
                    Encoding.Unicode.GetBytes("one\0")));
            CollectionAssert.AreEqual(new[] { "one" },
                HidHideAPIDevice.ParseMultiSz(
                    Encoding.Unicode.GetBytes("one\0\0unused tail")));
            Assert.ThrowsException<InvalidDataException>(() =>
                HidHideAPIDevice.ParseMultiSz(
                    Encoding.Unicode.GetBytes("\0not empty")));
            Assert.ThrowsException<InvalidDataException>(() =>
                HidHideAPIDevice.ParseMultiSz(new byte[] { 1, 0, 0 }));
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
            internal int ThrowOnReadNumber { get; init; }
            internal int ReturnNullOnReadNumber { get; init; }
            internal bool ThrowAfterWrite { get; init; }
            internal bool ReturnFalseAfterWrite { get; init; }
            internal int WriteCount { get; private set; }
            internal List<string> Events { get; } = new();

            internal void AddEntry(string instanceId) => entries.Add(instanceId);

            public List<string> GetBlacklist()
            {
                readCount++;
                Events.Add("read");
                if (readCount == ThrowOnReadNumber)
                {
                    throw new IOException("Simulated HidHide read failure.");
                }
                if (readCount == ReturnNullOnReadNumber)
                {
                    return null;
                }
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
                if (ThrowAfterWrite)
                {
                    throw new IOException("Simulated post-write failure.");
                }
                return !ReturnFalseAfterWrite;
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
            internal int ThrowOnReadNumber { get; init; }
            internal int ReturnNullOnReadNumber { get; init; }
            internal int WriteCount { get; private set; }

            public List<string> GetWhitelist()
            {
                readCount++;
                if (readCount == ThrowOnReadNumber)
                {
                    throw new IOException("Simulated HidHide read failure.");
                }
                if (readCount == ReturnNullOnReadNumber)
                {
                    return null;
                }
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
