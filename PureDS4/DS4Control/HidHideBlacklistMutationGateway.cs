using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using DS4WinWPF.DS4Control;

namespace DS4Windows
{
    internal sealed class HidHideBlacklistMutationResult
    {
        internal HidHideBlacklistMutationResult(bool succeeded, bool changed,
            bool writeAttempted, IReadOnlyCollection<string> before,
            IReadOnlyCollection<string> after, string error,
            bool afterKnown = true)
        {
            Succeeded = succeeded;
            Changed = changed;
            WriteAttempted = writeAttempted;
            Before = before ?? Array.Empty<string>();
            After = after ?? Array.Empty<string>();
            AfterKnown = afterKnown;
            Error = error ?? string.Empty;
        }

        internal bool Succeeded { get; }
        internal bool Changed { get; }
        internal bool WriteAttempted { get; }
        internal IReadOnlyCollection<string> Before { get; }
        internal IReadOnlyCollection<string> After { get; }
        internal bool AfterKnown { get; }
        internal string Error { get; }
    }

    /// <summary>
    /// Serializes cooperating DS4Windows processes around HidHide's global,
    /// whole-list persistent blacklist API. HidHide has no compare-and-swap;
    /// the immediate pre-write read and post-write verification detect every
    /// conflict except a third-party write in the final read/write race.
    /// </summary>
    internal static class HidHideBlacklistMutationGateway
    {
        internal const string MachineMutexName =
            ProductIdentity.HidHideBlacklistMutexName;
        private static readonly object processLock = new object();
        private static readonly TimeSpan MutexTimeout = TimeSpan.FromSeconds(5);

        internal static HidHideBlacklistMutationResult Mutate(
            IHidHideBlacklistDevice device,
            Func<IReadOnlyCollection<string>, IReadOnlyCollection<string>>
                createDesired,
            Func<bool> beforeWrite = null, bool useMachineMutex = true)
        {
            if (device == null)
            {
                return Failure("HidHide is unavailable.");
            }
            if (createDesired == null)
            {
                return Failure("No HidHide mutation was specified.");
            }

            lock (processLock)
            {
                Mutex mutex = null;
                bool mutexHeld = false;
                List<string> before = null;
                bool writeAttempted = false;
                try
                {
                    if (useMachineMutex)
                    {
                        mutex = new Mutex(false, MachineMutexName);
                        try
                        {
                            mutexHeld = mutex.WaitOne(MutexTimeout);
                        }
                        catch (AbandonedMutexException)
                        {
                            mutexHeld = true;
                        }

                        if (!mutexHeld)
                        {
                            return Failure(
                                $"Another {ProductIdentity.Name} process is changing HidHide configuration.");
                        }
                    }

                    before = Normalize(ReadBlacklist(device));
                    IReadOnlyCollection<string> requested = createDesired(before);
                    if (requested == null)
                    {
                        throw new InvalidOperationException(
                            "The HidHide mutation produced no configuration.");
                    }
                    List<string> desired = Normalize(requested);
                    if (Equivalent(before, desired))
                    {
                        return new HidHideBlacklistMutationResult(true, false,
                            false, before, before, string.Empty);
                    }

                    if (beforeWrite != null && !beforeWrite())
                    {
                        return new HidHideBlacklistMutationResult(false, false,
                            false, before, before,
                            "The durable HidHide recovery record could not be written.");
                    }

                    List<string> immediatelyBeforeWrite = Normalize(
                        ReadBlacklist(device));
                    if (!Equivalent(before, immediatelyBeforeWrite))
                    {
                        return new HidHideBlacklistMutationResult(false, false,
                            false, before, immediatelyBeforeWrite,
                            "HidHide configuration changed concurrently; no mutation was applied.");
                    }

                    // A setter can apply the update and then fail or throw.
                    // Once entered, the durable intent must survive uncertainty.
                    writeAttempted = true;
                    if (!device.SetBlacklist(desired))
                    {
                        return new HidHideBlacklistMutationResult(false, false,
                            true, before, Normalize(ReadBlacklist(device)),
                            "HidHide rejected the persistent blacklist update.");
                    }

                    List<string> after = Normalize(ReadBlacklist(device));
                    if (!Equivalent(desired, after))
                    {
                        return new HidHideBlacklistMutationResult(false, true,
                            true, before, after,
                            "HidHide did not preserve the exact verified blacklist delta.");
                    }

                    return new HidHideBlacklistMutationResult(true, true, true,
                        before, after, string.Empty);
                }
                catch (Exception ex)
                {
                    return new HidHideBlacklistMutationResult(false, false,
                        writeAttempted, before, null,
                        $"HidHide mutation failed: {ex.Message}",
                        afterKnown: false);
                }
                finally
                {
                    if (mutexHeld)
                    {
                        try { mutex.ReleaseMutex(); }
                        catch (ApplicationException) { }
                    }
                    mutex?.Dispose();
                }
            }
        }

        internal static List<string> AddExact(
            IReadOnlyCollection<string> current, string instanceId)
        {
            List<string> result = Normalize(current);
            if (!Contains(result, instanceId))
            {
                result.Add(instanceId);
            }
            return result;
        }

        internal static List<string> RemoveExact(
            IReadOnlyCollection<string> current, string instanceId)
        {
            List<string> result = Normalize(current);
            result.RemoveAll(entry => string.Equals(entry, instanceId,
                StringComparison.OrdinalIgnoreCase));
            return result;
        }

        internal static List<string> RemoveExact(
            IReadOnlyCollection<string> current,
            IEnumerable<string> instanceIds)
        {
            HashSet<string> targets = new HashSet<string>(
                instanceIds ?? Array.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);
            List<string> result = Normalize(current);
            result.RemoveAll(targets.Contains);
            return result;
        }

        internal static bool Contains(IEnumerable<string> entries,
            string instanceId) =>
            !string.IsNullOrWhiteSpace(instanceId) &&
            (entries ?? Array.Empty<string>()).Any(entry =>
                string.Equals(entry, instanceId,
                    StringComparison.OrdinalIgnoreCase));

        internal static bool Equivalent(IEnumerable<string> left,
            IEnumerable<string> right) =>
            new HashSet<string>(Normalize(left),
                StringComparer.OrdinalIgnoreCase).SetEquals(Normalize(right));

        private static List<string> ReadBlacklist(
            IHidHideBlacklistDevice device) =>
            device.GetBlacklist() ?? throw new InvalidOperationException(
                "HidHide returned no persistent blacklist configuration.");

        private static List<string> Normalize(IEnumerable<string> entries) =>
            (entries ?? Array.Empty<string>())
                .Where(entry => !string.IsNullOrWhiteSpace(entry))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

        private static HidHideBlacklistMutationResult Failure(string error) =>
            new HidHideBlacklistMutationResult(false, false, false,
                Array.Empty<string>(), Array.Empty<string>(), error,
                afterKnown: false);
    }

    internal sealed class HidHideRecoveryPreview
    {
        internal HidHideRecoveryPreview(IReadOnlyCollection<string> pending,
            IReadOnlyCollection<string> present,
            IReadOnlyCollection<string> observedBlacklist,
            bool activeStateUncertain, bool? activeStateObserved,
            string error)
        {
            Pending = pending ?? Array.Empty<string>();
            Present = present ?? Array.Empty<string>();
            ObservedBlacklist = observedBlacklist ?? Array.Empty<string>();
            ActiveStateUncertain = activeStateUncertain;
            ActiveStateObserved = activeStateObserved;
            Error = error ?? string.Empty;
        }

        internal bool CanRecover => string.IsNullOrEmpty(Error);
        internal IReadOnlyCollection<string> Pending { get; }
        internal IReadOnlyCollection<string> Present { get; }
        internal IReadOnlyCollection<string> ObservedBlacklist { get; }
        internal bool ActiveStateUncertain { get; }
        internal bool? ActiveStateObserved { get; }
        internal string Error { get; }
    }

    internal static class HidHidePersistentRecovery
    {
        internal static HidHideRecoveryPreview Inspect(
            IHidHideBlacklistDevice device, HidHideOwnershipJournal journal,
            Func<bool> readActiveState = null)
        {
            if (device == null || journal == null || !journal.Load() ||
                !journal.IsReliable)
            {
                return Failure("HidHide or its ownership record is unavailable.");
            }
            if (journal.IsTransientRunInProgress)
            {
                return Failure("Stop PureDS4 controller handling before recovery.");
            }
            if (journal.ExternalContainmentSuspensions.Count > 0)
            {
                return Failure("External HidHide containment must be restored " +
                    "before persistent recovery.");
            }

            string[] pending = journal.UnresolvedPersistentBlacklistEntries
                .OrderBy(id => id, StringComparer.OrdinalIgnoreCase).ToArray();
            bool activeUncertain = journal.ActiveStateRecoveryRequired;
            if (pending.Length == 0 && !activeUncertain)
            {
                return Failure("No pending HidHide recovery was found.");
            }

            try
            {
                List<string> observed = device.GetBlacklist() ??
                    throw new InvalidOperationException(
                        "HidHide returned no blacklist configuration.");
                bool? active = readActiveState?.Invoke();
                string[] present = pending.Where(id =>
                    HidHideBlacklistMutationGateway.Contains(observed, id))
                    .ToArray();
                return new HidHideRecoveryPreview(pending, present,
                    observed.ToArray(), activeUncertain, active,
                    string.Empty);
            }
            catch (Exception ex)
            {
                return Failure("HidHide blacklist could not be inspected: " +
                    ex.Message);
            }
        }

        internal static string Complete(IHidHideBlacklistDevice device,
            HidHideOwnershipJournal journal, HidHideRecoveryPreview preview,
            bool useMachineMutex = true,
            Func<bool> readActiveState = null)
        {
            if (device == null || journal == null || preview?.CanRecover != true ||
                !journal.Load() || !journal.IsReliable ||
                journal.IsTransientRunInProgress ||
                journal.ExternalContainmentSuspensions.Count > 0 ||
                journal.ActiveStateRecoveryRequired !=
                    preview.ActiveStateUncertain ||
                !HidHideBlacklistMutationGateway.Equivalent(
                    journal.UnresolvedPersistentBlacklistEntries,
                    preview.Pending))
            {
                return "The recovery record changed; inspect it again.";
            }

            try
            {
                if (preview.ActiveStateObserved.HasValue &&
                    (readActiveState == null || readActiveState() !=
                        preview.ActiveStateObserved.Value))
                {
                    return "HidHide's active setting changed after " +
                        "inspection; inspect again.";
                }
            }
            catch (Exception ex)
            {
                return "HidHide's active setting could not be verified: " +
                    ex.Message;
            }

            HidHideBlacklistMutationResult mutation =
                HidHideBlacklistMutationGateway.Mutate(device,
                    current =>
                    {
                        if (!HidHideBlacklistMutationGateway.Equivalent(
                                current, preview.ObservedBlacklist))
                        {
                            throw new InvalidOperationException(
                                "HidHide configuration changed after inspection.");
                        }
                        return HidHideBlacklistMutationGateway.RemoveExact(
                            current, preview.Pending);
                    }, useMachineMutex: useMachineMutex);
            if (!mutation.Succeeded ||
                preview.Pending.Any(id =>
                    HidHideBlacklistMutationGateway.Contains(
                        mutation.After, id)))
            {
                return string.IsNullOrWhiteSpace(mutation.Error)
                    ? "The exact HidHide entries could not be verified as absent."
                    : mutation.Error;
            }

            if (!journal.CompleteVerifiedRecovery(preview.Pending,
                    acknowledgeActiveState: true))
            {
                return "HidHide entries are absent, but the recovery record " +
                    "could not be finalized; retry after inspecting again.";
            }
            return string.Empty;
        }

        private static HidHideRecoveryPreview Failure(string error) =>
            new HidHideRecoveryPreview(null, null, null, false, null, error);
    }
}
