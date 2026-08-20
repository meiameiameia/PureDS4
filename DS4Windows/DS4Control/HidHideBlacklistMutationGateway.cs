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
            IReadOnlyCollection<string> after, string error)
        {
            Succeeded = succeeded;
            Changed = changed;
            WriteAttempted = writeAttempted;
            Before = before ?? Array.Empty<string>();
            After = after ?? Array.Empty<string>();
            Error = error ?? string.Empty;
        }

        internal bool Succeeded { get; }
        internal bool Changed { get; }
        internal bool WriteAttempted { get; }
        internal IReadOnlyCollection<string> Before { get; }
        internal IReadOnlyCollection<string> After { get; }
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
            @"Global\DS4Windows-Reworked-HidHide-Blacklist";
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
                                "Another DS4Windows process is changing HidHide configuration.");
                        }
                    }

                    List<string> before = Normalize(device.GetBlacklist());
                    List<string> desired = Normalize(createDesired(before));
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
                        device.GetBlacklist());
                    if (!Equivalent(before, immediatelyBeforeWrite))
                    {
                        return new HidHideBlacklistMutationResult(false, false,
                            false, before, immediatelyBeforeWrite,
                            "HidHide configuration changed concurrently; no mutation was applied.");
                    }

                    if (!device.SetBlacklist(desired))
                    {
                        return new HidHideBlacklistMutationResult(false, false,
                            true, before, Normalize(device.GetBlacklist()),
                            "HidHide rejected the persistent blacklist update.");
                    }

                    List<string> after = Normalize(device.GetBlacklist());
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
                    return Failure($"HidHide mutation failed: {ex.Message}");
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

        private static List<string> Normalize(IEnumerable<string> entries) =>
            (entries ?? Array.Empty<string>())
                .Where(entry => !string.IsNullOrWhiteSpace(entry))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

        private static HidHideBlacklistMutationResult Failure(string error) =>
            new HidHideBlacklistMutationResult(false, false, false,
                Array.Empty<string>(), Array.Empty<string>(), error);
    }
}
