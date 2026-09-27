using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace DS4Windows
{
    internal enum HidHideExternalSuspensionState
    {
        IntentRecorded = 0,
        Suspended = 1,
        RecoveryRequired = 2,
    }

    internal sealed class HidHideExternalSuspensionInfo
    {
        internal HidHideExternalSuspensionInfo(string instanceId, string runId,
            HidHideExternalSuspensionState state, bool activeStateObserved,
            bool inverseStateObserved, DateTimeOffset createdUtc)
        {
            InstanceId = instanceId ?? string.Empty;
            RunId = runId ?? string.Empty;
            State = state;
            ActiveStateObserved = activeStateObserved;
            InverseStateObserved = inverseStateObserved;
            CreatedUtc = createdUtc;
        }

        internal string InstanceId { get; }
        internal string RunId { get; }
        internal HidHideExternalSuspensionState State { get; }
        internal bool ActiveStateObserved { get; }
        internal bool InverseStateObserved { get; }
        internal DateTimeOffset CreatedUtc { get; }
    }

    /// <summary>
    /// Records only HidHide entries that this application proved it created.
    /// Stale transient entries are deliberately moved to an unresolved set:
    /// after a crash, their continued ownership cannot be proven safely.
    /// </summary>
    internal sealed class HidHideOwnershipJournal
    {
        internal const string FileName = "HidHideOwnership.json";

        private readonly string path;
        private JournalState state = new JournalState();
        private bool loaded;
        private bool externalContainmentRecoveryRequired;

        internal HidHideOwnershipJournal(string path)
        {
            this.path = path ?? throw new ArgumentNullException(nameof(path));
        }

        internal string Path => path;
        internal bool IsReliable { get; private set; } = true;
        internal bool RecoveryRequired =>
            state.UnresolvedPersistentBlacklistEntries.Count > 0 ||
            state.ActiveStateRecoveryRequired ||
            externalContainmentRecoveryRequired;
        internal bool IsTransientRunInProgress => state.RunInProgress;
        internal bool ActiveStateRecoveryRequired =>
            state.ActiveStateRecoveryRequired;
        internal IReadOnlyCollection<string> UnresolvedPersistentBlacklistEntries =>
            state.UnresolvedPersistentBlacklistEntries;
        internal IReadOnlyCollection<string> PersistentWhitelistEntries
        {
            get
            {
                Load();
                return state.PersistentWhitelistEntries.ToArray();
            }
        }
        internal IReadOnlyCollection<HidHideExternalSuspensionInfo>
            ExternalContainmentSuspensions => state.ExternalContainmentSuspensions
                .Select(record => new HidHideExternalSuspensionInfo(
                    record.InstanceId, record.RunId, record.State,
                    record.ActiveStateObserved, record.InverseStateObserved,
                    record.CreatedUtc))
                .ToArray();

        internal bool Load()
        {
            if (loaded)
            {
                return IsReliable;
            }

            loaded = true;
            if (!File.Exists(path))
            {
                return true;
            }

            try
            {
                JournalState loadedState = JsonSerializer.Deserialize<JournalState>(
                    File.ReadAllText(path));
                state = loadedState ?? new JournalState();
                if (state.Version > 2)
                {
                    throw new InvalidDataException(
                        "The HidHide ownership journal uses a newer schema.");
                }
                state.Normalize();
                externalContainmentRecoveryRequired =
                    state.ExternalContainmentSuspensions.Count > 0;

                if (state.RunInProgress)
                {
                    state.UnresolvedPersistentBlacklistEntries.UnionWith(
                        state.CurrentRunPersistentBlacklistEntries);
                    state.ActiveStateRecoveryRequired |=
                        state.CurrentRunEnabledActiveState;
                    state.CurrentRunPersistentBlacklistEntries.Clear();
                    state.CurrentRunEnabledActiveState = false;
                    state.RunInProgress = false;
                    return Save();
                }

                return true;
            }
            catch
            {
                IsReliable = false;
                return false;
            }
        }

        internal bool BeginTransientRun()
        {
            if (!Load() || state.RunInProgress)
            {
                return IsReliable;
            }

            state.RunInProgress = true;
            state.RunId = Guid.NewGuid().ToString("N");
            state.CurrentRunPersistentBlacklistEntries.Clear();
            state.CurrentRunEnabledActiveState = false;
            return Save();
        }

        internal bool RecordPersistentBlacklistEntry(string instanceId)
        {
            if (!state.RunInProgress || string.IsNullOrWhiteSpace(instanceId))
            {
                return false;
            }

            state.CurrentRunPersistentBlacklistEntries.Add(instanceId);
            return Save();
        }

        internal bool RecordActiveStateEnabled()
        {
            if (!state.RunInProgress)
            {
                return false;
            }

            state.CurrentRunEnabledActiveState = true;
            return Save();
        }

        internal bool RecordExternalContainmentSuspensionIntent(
            string instanceId, bool activeStateObserved,
            bool inverseStateObserved)
        {
            if (!Load() || string.IsNullOrWhiteSpace(instanceId) ||
                state.ExternalContainmentSuspensions.Any(record =>
                    string.Equals(record.InstanceId, instanceId,
                        StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            state.ExternalContainmentSuspensions.Add(
                new ExternalContainmentSuspensionRecord
                {
                    InstanceId = instanceId,
                    RunId = string.IsNullOrWhiteSpace(state.RunId)
                        ? Guid.NewGuid().ToString("N") : state.RunId,
                    State = HidHideExternalSuspensionState.IntentRecorded,
                    OriginalEntryPresent = true,
                    ActiveStateObserved = activeStateObserved,
                    InverseStateObserved = inverseStateObserved,
                    CreatedUtc = DateTimeOffset.UtcNow,
                    UpdatedUtc = DateTimeOffset.UtcNow,
                });
            return Save();
        }

        internal bool MarkExternalContainmentSuspended(string instanceId)
        {
            ExternalContainmentSuspensionRecord record = FindExternalSuspension(
                instanceId);
            if (record == null)
            {
                return false;
            }

            record.State = HidHideExternalSuspensionState.Suspended;
            record.UpdatedUtc = DateTimeOffset.UtcNow;
            return Save();
        }

        internal bool MarkExternalContainmentRecoveryRequired(string instanceId)
        {
            ExternalContainmentSuspensionRecord record = FindExternalSuspension(
                instanceId);
            if (record == null)
            {
                return false;
            }

            record.State = HidHideExternalSuspensionState.RecoveryRequired;
            record.UpdatedUtc = DateTimeOffset.UtcNow;
            externalContainmentRecoveryRequired = true;
            return Save();
        }

        internal bool CompleteExternalContainmentSuspension(string instanceId)
        {
            if (!Load() || string.IsNullOrWhiteSpace(instanceId))
            {
                return false;
            }

            int removed = state.ExternalContainmentSuspensions.RemoveAll(record =>
                string.Equals(record.InstanceId, instanceId,
                    StringComparison.OrdinalIgnoreCase));
            if (removed == 0)
            {
                return true;
            }

            externalContainmentRecoveryRequired =
                state.ExternalContainmentSuspensions.Count > 0 &&
                externalContainmentRecoveryRequired;
            return Save();
        }

        internal bool CompleteTransientRun(
            IEnumerable<string> releasedPersistentEntries,
            bool activeStateRestored)
        {
            state.CurrentRunPersistentBlacklistEntries.ExceptWith(
                releasedPersistentEntries ?? Array.Empty<string>());
            if (activeStateRestored)
            {
                state.CurrentRunEnabledActiveState = false;
            }

            if (state.CurrentRunPersistentBlacklistEntries.Count == 0 &&
                !state.CurrentRunEnabledActiveState)
            {
                state.RunInProgress = false;
                state.RunId = string.Empty;
            }

            return Save();
        }

        internal bool MarkStoppedRunRecoveryRequired()
        {
            if (!Load())
            {
                return false;
            }
            if (!state.RunInProgress)
            {
                return true;
            }

            state.UnresolvedPersistentBlacklistEntries.UnionWith(
                state.CurrentRunPersistentBlacklistEntries);
            state.ActiveStateRecoveryRequired |=
                state.CurrentRunEnabledActiveState;
            state.CurrentRunPersistentBlacklistEntries.Clear();
            state.CurrentRunEnabledActiveState = false;
            state.RunInProgress = false;
            state.RunId = string.Empty;
            return Save();
        }

        internal bool CompleteVerifiedRecovery(
            IReadOnlyCollection<string> verifiedAbsentEntries,
            bool acknowledgeActiveState)
        {
            if (!Load() || state.RunInProgress ||
                verifiedAbsentEntries == null)
            {
                return false;
            }

            HashSet<string> verified = new HashSet<string>(
                verifiedAbsentEntries, StringComparer.OrdinalIgnoreCase);
            if (verified.Any(string.IsNullOrWhiteSpace) ||
                !verified.SetEquals(state.UnresolvedPersistentBlacklistEntries) ||
                (state.ActiveStateRecoveryRequired && !acknowledgeActiveState))
            {
                return false;
            }

            state.UnresolvedPersistentBlacklistEntries.Clear();
            if (acknowledgeActiveState)
            {
                state.ActiveStateRecoveryRequired = false;
            }
            return Save();
        }

        internal bool OwnsWhitelistEntry(string pathValue)
        {
            Load();
            return !string.IsNullOrWhiteSpace(pathValue) &&
                state.PersistentWhitelistEntries.Contains(pathValue);
        }

        internal bool RecordWhitelistEntry(string pathValue)
        {
            if (!Load() || string.IsNullOrWhiteSpace(pathValue))
            {
                return false;
            }

            state.PersistentWhitelistEntries.Add(pathValue);
            return Save();
        }

        internal bool ForgetWhitelistEntry(string pathValue)
        {
            if (!Load() || string.IsNullOrWhiteSpace(pathValue))
            {
                return false;
            }

            state.PersistentWhitelistEntries.Remove(pathValue);
            return Save();
        }

        internal bool ForgetWhitelistEntries(IEnumerable<string> pathValues)
        {
            if (!Load())
            {
                return false;
            }

            state.PersistentWhitelistEntries.ExceptWith(
                pathValues ?? Array.Empty<string>());
            return Save();
        }

        private bool Save()
        {
            if (!IsReliable)
            {
                return false;
            }

            try
            {
                bool hasState = state.RunInProgress ||
                    state.PersistentWhitelistEntries.Count > 0 ||
                    state.UnresolvedPersistentBlacklistEntries.Count > 0 ||
                    state.ActiveStateRecoveryRequired ||
                    state.ExternalContainmentSuspensions.Count > 0;
                if (!hasState)
                {
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                    return true;
                }

                string directory = System.IO.Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string temporaryPath = path + ".tmp";
                File.WriteAllText(temporaryPath, JsonSerializer.Serialize(state,
                    new JsonSerializerOptions { WriteIndented = true }));
                File.Move(temporaryPath, path, true);
                return true;
            }
            catch
            {
                IsReliable = false;
                return false;
            }
        }

        private ExternalContainmentSuspensionRecord FindExternalSuspension(
            string instanceId)
        {
            if (!Load() || string.IsNullOrWhiteSpace(instanceId))
            {
                return null;
            }

            return state.ExternalContainmentSuspensions.FirstOrDefault(record =>
                string.Equals(record.InstanceId, instanceId,
                    StringComparison.OrdinalIgnoreCase));
        }

        private sealed class JournalState
        {
            public int Version { get; set; } = 2;
            public string RunId { get; set; } = string.Empty;
            public bool RunInProgress { get; set; }
            public bool CurrentRunEnabledActiveState { get; set; }
            public bool ActiveStateRecoveryRequired { get; set; }
            public HashSet<string> CurrentRunPersistentBlacklistEntries { get; set; } =
                NewSet();
            public HashSet<string> UnresolvedPersistentBlacklistEntries { get; set; } =
                NewSet();
            public HashSet<string> PersistentWhitelistEntries { get; set; } =
                NewSet();
            public List<ExternalContainmentSuspensionRecord>
                ExternalContainmentSuspensions { get; set; } = new();

            public void Normalize()
            {
                CurrentRunPersistentBlacklistEntries = NormalizeSet(
                    CurrentRunPersistentBlacklistEntries);
                UnresolvedPersistentBlacklistEntries = NormalizeSet(
                    UnresolvedPersistentBlacklistEntries);
                PersistentWhitelistEntries = NormalizeSet(
                    PersistentWhitelistEntries);
                ExternalContainmentSuspensions ??= new();
                if (ExternalContainmentSuspensions.Any(record =>
                        record == null ||
                        string.IsNullOrWhiteSpace(record.InstanceId) ||
                        !record.OriginalEntryPresent))
                {
                    throw new InvalidDataException(
                        "The external HidHide restore record is invalid.");
                }
                ExternalContainmentSuspensions =
                    ExternalContainmentSuspensions
                    .GroupBy(record => record.InstanceId,
                        StringComparer.OrdinalIgnoreCase)
                    .Select(group => group.OrderByDescending(record =>
                        record.UpdatedUtc).First())
                    .ToList();
                RunId ??= string.Empty;
                Version = Math.Max(Version, 2);
            }

            private static HashSet<string> NormalizeSet(IEnumerable<string> values) =>
                new HashSet<string>((values ?? Array.Empty<string>())
                    .Where(value => !string.IsNullOrWhiteSpace(value)),
                    StringComparer.OrdinalIgnoreCase);

            private static HashSet<string> NewSet() =>
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        private sealed class ExternalContainmentSuspensionRecord
        {
            public string InstanceId { get; set; } = string.Empty;
            public string RunId { get; set; } = string.Empty;
            public HidHideExternalSuspensionState State { get; set; }
            public bool OriginalEntryPresent { get; set; }
            public bool ActiveStateObserved { get; set; }
            public bool InverseStateObserved { get; set; }
            public DateTimeOffset CreatedUtc { get; set; }
            public DateTimeOffset UpdatedUtc { get; set; }
        }
    }

    internal static class HidHideOwnershipPolicy
    {
        internal static bool Contains(IEnumerable<string> entries, string value) =>
            !string.IsNullOrWhiteSpace(value) &&
            (entries ?? Array.Empty<string>()).Any(entry =>
                string.Equals(entry, value, StringComparison.OrdinalIgnoreCase));

        internal static List<string> AddIfMissing(IEnumerable<string> baseline,
            string value, out bool added)
        {
            List<string> result = Normalize(baseline);
            added = !Contains(result, value);
            if (added)
            {
                result.Add(value);
            }
            return result;
        }

        internal static List<string> RemoveOwned(IEnumerable<string> current,
            IEnumerable<string> owned, out List<string> removed)
        {
            HashSet<string> ownedSet = new HashSet<string>(
                owned ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            List<string> result = Normalize(current);
            removed = result.Where(ownedSet.Contains).ToList();
            result.RemoveAll(ownedSet.Contains);
            return result;
        }

        internal static bool CanRestoreInactiveState(
            IEnumerable<string> baselineBlacklist,
            IEnumerable<string> currentAfterOwnedCleanup)
        {
            HashSet<string> baseline = new HashSet<string>(
                Normalize(baselineBlacklist), StringComparer.OrdinalIgnoreCase);
            return baseline.SetEquals(Normalize(currentAfterOwnedCleanup));
        }

        private static List<string> Normalize(IEnumerable<string> values) =>
            (values ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
    }
}
