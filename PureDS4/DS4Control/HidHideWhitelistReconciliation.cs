using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using DS4WinWPF.DS4Control;

namespace DS4Windows
{
    internal enum HidHideOwnedApplicationPathState
    {
        Present,
        Missing,
        Unknown,
    }

    internal sealed class HidHideWhitelistReconciliationPlan
    {
        internal HidHideWhitelistReconciliationPlan(
            IReadOnlyCollection<string> entriesToRemove,
            IReadOnlyCollection<string> journalEntriesToForget)
        {
            EntriesToRemove = entriesToRemove ?? Array.Empty<string>();
            JournalEntriesToForget = journalEntriesToForget ??
                Array.Empty<string>();
        }

        internal IReadOnlyCollection<string> EntriesToRemove { get; }
        internal IReadOnlyCollection<string> JournalEntriesToForget { get; }
    }

    /// <summary>
    /// Plans cleanup only for application whitelist entries whose ownership
    /// was durably recorded by PureDS4. Missing journal entries and paths
    /// whose existence cannot be established are never removal candidates.
    /// </summary>
    internal static class HidHideWhitelistReconciliationPolicy
    {
        internal static HidHideWhitelistReconciliationPlan Create(
            IEnumerable<string> currentWhitelist,
            IEnumerable<string> ownedJournalEntries,
            Func<string, HidHideOwnedApplicationPathState> inspectPath)
        {
            ArgumentNullException.ThrowIfNull(inspectPath);
            HashSet<string> current = new HashSet<string>(
                Normalize(currentWhitelist), StringComparer.OrdinalIgnoreCase);
            List<string> remove = new List<string>();
            List<string> forget = new List<string>();

            foreach (string owned in Normalize(ownedJournalEntries))
            {
                if (!current.Contains(owned))
                {
                    forget.Add(owned);
                    continue;
                }

                if (inspectPath(owned) ==
                    HidHideOwnedApplicationPathState.Missing)
                {
                    remove.Add(owned);
                    forget.Add(owned);
                }
            }

            return new HidHideWhitelistReconciliationPlan(remove, forget);
        }

        private static IEnumerable<string> Normalize(
            IEnumerable<string> values) =>
            (values ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    internal static class HidHideOwnedApplicationPathInspector
    {
        internal static HidHideOwnedApplicationPathState Inspect(
            string hidHidePath)
        {
            string dosPath;
            try
            {
                if (!TryResolveDosPath(hidHidePath, out dosPath))
                {
                    return HidHideOwnedApplicationPathState.Unknown;
                }
            }
            catch (Exception)
            {
                return HidHideOwnedApplicationPathState.Unknown;
            }

            try
            {
                FileAttributes attributes = File.GetAttributes(dosPath);
                return attributes.HasFlag(FileAttributes.Directory)
                    ? HidHideOwnedApplicationPathState.Unknown
                    : HidHideOwnedApplicationPathState.Present;
            }
            catch (FileNotFoundException)
            {
                return HidHideOwnedApplicationPathState.Missing;
            }
            catch (DirectoryNotFoundException)
            {
                return HidHideOwnedApplicationPathState.Missing;
            }
            catch (UnauthorizedAccessException)
            {
                return HidHideOwnedApplicationPathState.Unknown;
            }
            catch (IOException)
            {
                return HidHideOwnedApplicationPathState.Unknown;
            }
            catch (NotSupportedException)
            {
                return HidHideOwnedApplicationPathState.Unknown;
            }
        }

        internal static bool TryResolveDosPath(string hidHidePath,
            out string dosPath)
        {
            dosPath = string.Empty;
            if (string.IsNullOrWhiteSpace(hidHidePath))
            {
                return false;
            }

            string value = hidHidePath.Trim();
            if (value.StartsWith(@"\??\", StringComparison.Ordinal))
            {
                value = value.Substring(4);
            }
            if (Path.IsPathFullyQualified(value) &&
                value.Length >= 3 && value[1] == ':')
            {
                dosPath = value;
                return true;
            }

            string bestDevicePrefix = null;
            string bestDrive = null;
            foreach (string driveRoot in Environment.GetLogicalDrives())
            {
                string drive = driveRoot.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);
                StringBuilder target = new StringBuilder(1024);
                if (NativeMethods.QueryDosDevice(drive, target,
                        target.Capacity) == 0)
                {
                    continue;
                }

                string devicePrefix = target.ToString();
                bool exact = string.Equals(value, devicePrefix,
                    StringComparison.OrdinalIgnoreCase);
                bool descendant = value.StartsWith(devicePrefix + "\\",
                    StringComparison.OrdinalIgnoreCase);
                if ((exact || descendant) &&
                    (bestDevicePrefix == null || devicePrefix.Length >
                        bestDevicePrefix.Length))
                {
                    bestDevicePrefix = devicePrefix;
                    bestDrive = drive;
                }
            }

            if (bestDevicePrefix == null)
            {
                return false;
            }

            dosPath = bestDrive + value.Substring(bestDevicePrefix.Length);
            return true;
        }
    }

    internal sealed class HidHideWhitelistMutationResult
    {
        internal HidHideWhitelistMutationResult(bool succeeded, bool changed,
            IReadOnlyCollection<string> after, string error)
        {
            Succeeded = succeeded;
            Changed = changed;
            After = after ?? Array.Empty<string>();
            Error = error ?? string.Empty;
        }

        internal bool Succeeded { get; }
        internal bool Changed { get; }
        internal IReadOnlyCollection<string> After { get; }
        internal string Error { get; }
    }

    /// <summary>
    /// Serializes PureDS4 whitelist changes, rereads immediately before the
    /// whole-list HidHide write, and verifies the exact result afterward.
    /// </summary>
    internal static class HidHideWhitelistMutationGateway
    {
        private static readonly object ProcessLock = new object();
        private static readonly TimeSpan MutexTimeout = TimeSpan.FromSeconds(5);

        internal static HidHideWhitelistMutationResult RemoveExact(
            IHidHideWhitelistDevice device, IEnumerable<string> entries,
            bool useMachineMutex = true)
        {
            if (device == null)
            {
                return Failure("HidHide is unavailable.");
            }

            HashSet<string> targets = new HashSet<string>(
                Normalize(entries), StringComparer.OrdinalIgnoreCase);
            if (targets.Count == 0)
            {
                return new HidHideWhitelistMutationResult(true, false,
                    Normalize(device.GetWhitelist()), string.Empty);
            }

            lock (ProcessLock)
            {
                Mutex mutex = null;
                bool mutexHeld = false;
                try
                {
                    if (useMachineMutex)
                    {
                        mutex = new Mutex(false,
                            HidHideBlacklistMutationGateway.MachineMutexName);
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
                            return Failure("Another PureDS4 process is " +
                                "changing HidHide configuration.");
                        }
                    }

                    List<string> before = Normalize(device.GetWhitelist());
                    List<string> desired = before
                        .Where(entry => !targets.Contains(entry)).ToList();
                    if (Equivalent(before, desired))
                    {
                        return new HidHideWhitelistMutationResult(true, false,
                            before, string.Empty);
                    }

                    List<string> immediatelyBefore = Normalize(
                        device.GetWhitelist());
                    if (!Equivalent(before, immediatelyBefore))
                    {
                        return new HidHideWhitelistMutationResult(false, false,
                            immediatelyBefore, "HidHide whitelist changed " +
                            "concurrently; no cleanup was applied.");
                    }
                    if (!device.SetWhitelist(desired))
                    {
                        return new HidHideWhitelistMutationResult(false, false,
                            Normalize(device.GetWhitelist()),
                            "HidHide rejected the whitelist cleanup.");
                    }

                    List<string> after = Normalize(device.GetWhitelist());
                    if (!Equivalent(desired, after))
                    {
                        return new HidHideWhitelistMutationResult(false, true,
                            after, "HidHide did not preserve the exact " +
                            "verified whitelist cleanup.");
                    }

                    return new HidHideWhitelistMutationResult(true, true,
                        after, string.Empty);
                }
                catch (Exception ex)
                {
                    return Failure("HidHide whitelist cleanup failed: " +
                        ex.Message);
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

        private static List<string> Normalize(IEnumerable<string> entries) =>
            (entries ?? Array.Empty<string>())
                .Where(entry => !string.IsNullOrWhiteSpace(entry))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

        private static bool Equivalent(IEnumerable<string> left,
            IEnumerable<string> right) =>
            new HashSet<string>(Normalize(left),
                StringComparer.OrdinalIgnoreCase).SetEquals(Normalize(right));

        private static HidHideWhitelistMutationResult Failure(string error) =>
            new HidHideWhitelistMutationResult(false, false,
                Array.Empty<string>(), error);
    }
}
