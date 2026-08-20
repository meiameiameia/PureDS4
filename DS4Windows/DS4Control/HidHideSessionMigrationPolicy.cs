using System;
using System.Collections.Generic;
using System.Linq;

namespace DS4Windows
{
    internal readonly struct HidHideSessionMigrationPlan
    {
        internal HidHideSessionMigrationPlan(bool canMigrate,
            IReadOnlyCollection<string> entriesToAdd, string error)
        {
            CanMigrate = canMigrate;
            EntriesToAdd = entriesToAdd ?? Array.Empty<string>();
            Error = error ?? string.Empty;
        }

        internal bool CanMigrate { get; }
        internal IReadOnlyCollection<string> EntriesToAdd { get; }
        internal string Error { get; }
    }

    internal static class HidHideSessionMigrationPolicy
    {
        internal static HidHideSessionMigrationPlan Create(
            IEnumerable<string> currentPersistentBlacklist,
            IEnumerable<string> sessionOwnedEntries,
            IEnumerable<string> persistentOwnedEntries)
        {
            HashSet<string> current = new HashSet<string>(
                currentPersistentBlacklist ?? Array.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);
            HashSet<string> sessionOwned = new HashSet<string>(
                sessionOwnedEntries ?? Array.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);
            HashSet<string> persistentOwned = new HashSet<string>(
                persistentOwnedEntries ?? Array.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);

            string collision = sessionOwned.FirstOrDefault(entry =>
                current.Contains(entry) && !persistentOwned.Contains(entry));
            if (!string.IsNullOrWhiteSpace(collision))
            {
                return new HidHideSessionMigrationPlan(false,
                    Array.Empty<string>(),
                    $"Persistent ownership changed for {collision}; " +
                    "the session entry was preserved.");
            }

            return new HidHideSessionMigrationPlan(true,
                sessionOwned.Where(entry => !current.Contains(entry)).ToArray(),
                string.Empty);
        }
    }
}
