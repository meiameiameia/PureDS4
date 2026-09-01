/*
This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;

namespace DS4Windows
{
    /// <summary>What kind of thing one removal item refers to.</summary>
    internal enum LegacyRemovalItemCategory
    {
        ApplicationUninstaller,
        InstallDirectory,
        RegistryRoot,
        ScheduledTask,
        Configuration,
    }

    /// <summary>
    /// One thing a full removal of the old installation would involve.
    /// Building this never removes, moves, or opens anything for write —
    /// it only describes, from a <see cref="LegacyInstallationSurvey"/>,
    /// what is there.
    /// </summary>
    internal sealed class LegacyRemovalItem
    {
        internal LegacyRemovalItem(LegacyRemovalItemCategory category,
            string description, string detail)
        {
            Category = category;
            Description = description;
            Detail = detail ?? string.Empty;
        }

        internal LegacyRemovalItemCategory Category { get; }
        internal string Description { get; }
        internal string Detail { get; }
    }

    /// <summary>
    /// Step 6 of the replacement flow in AGENTS.md, first pass: a
    /// read-only plan describing what removing the old installation would
    /// involve, built from the same survey step 1 already produces.
    /// Nothing here executes an uninstall, deletes a directory, disables a
    /// task, or touches the registry. This is the confirmation-screen
    /// content for a later, explicitly authorized action, not the action
    /// itself.
    /// </summary>
    internal sealed class LegacyRemovalPlan
    {
        internal LegacyRemovalPlan(IReadOnlyList<LegacyRemovalItem> items,
            bool configurationWouldSurvive,
            bool hasCompetingRuntimeOwnership = false)
        {
            Items = items ?? Array.Empty<LegacyRemovalItem>();
            ConfigurationWouldSurvive = configurationWouldSurvive;
            HasCompetingRuntimeOwnership = hasCompetingRuntimeOwnership;
        }

        internal IReadOnlyList<LegacyRemovalItem> Items { get; }
        internal bool HasDetectedState => Items.Count > 0;
        internal bool HasAnythingToRemove =>
            HasDetectedState && HasCompetingRuntimeOwnership;
        internal bool HasCompetingRuntimeOwnership { get; }
        internal bool HasResidualState =>
            HasDetectedState && !HasCompetingRuntimeOwnership;

        /// <summary>
        /// True when the survey found a configuration directory, meaning
        /// it is one of the things removal would leave behind: Windows
        /// application uninstallers do not normally delete a per-user
        /// AppData folder, and PureDS4 does not either (see step 3's
        /// LegacyConfigurationGuard) — removing it is a separate, manual,
        /// explicitly authorized decision, not part of this plan.
        /// </summary>
        internal bool ConfigurationWouldSurvive { get; }
    }

    internal static class LegacyRemovalPlanner
    {
        internal static LegacyRemovalPlan Build(LegacyInstallationSurvey survey)
        {
            ArgumentNullException.ThrowIfNull(survey);

            List<LegacyRemovalItem> items = new List<LegacyRemovalItem>();

            foreach (UninstallRegistryEntry entry in survey.UninstallEntries)
            {
                string description = string.IsNullOrWhiteSpace(
                        entry.DisplayVersion)
                    ? $"Uninstall {entry.DisplayName}"
                    : $"Uninstall {entry.DisplayName} ({entry.DisplayVersion})";
                string detail = string.IsNullOrWhiteSpace(entry.UninstallString)
                    ? "No uninstall command was recorded for this entry."
                    : entry.UninstallString;
                items.Add(new LegacyRemovalItem(
                    LegacyRemovalItemCategory.ApplicationUninstaller,
                    description, detail));
            }

            foreach (string directory in survey.InstallDirectories)
            {
                items.Add(new LegacyRemovalItem(
                    LegacyRemovalItemCategory.InstallDirectory,
                    "Legacy install directory", directory));
            }

            if (survey.RegistryRootPresent)
            {
                items.Add(new LegacyRemovalItem(
                    LegacyRemovalItemCategory.RegistryRoot,
                    "Registry settings key",
                    @"HKEY_LOCAL_MACHINE\SOFTWARE\" +
                        LegacyInstallationDetector.RegistrySubKeyName));
            }

            if (survey.StartupTaskPresent)
            {
                items.Add(new LegacyRemovalItem(
                    LegacyRemovalItemCategory.ScheduledTask,
                    "Startup scheduled task",
                    LegacyInstallationDetector.StartupTaskName));
            }

            if (survey.ViiperTaskPresent)
            {
                items.Add(new LegacyRemovalItem(
                    LegacyRemovalItemCategory.ScheduledTask,
                    "VIIPER scheduled task",
                    LegacyInstallationDetector.ViiperTaskName));
            }

            if (survey.ConfigurationDirectoryPresent)
            {
                items.Add(new LegacyRemovalItem(
                    LegacyRemovalItemCategory.Configuration,
                    "Profiles and settings (left behind by default)",
                    survey.ConfigurationDirectoryPath));
            }

            return new LegacyRemovalPlan(items,
                survey.ConfigurationDirectoryPresent,
                survey.HasCompetingRuntimeOwnership);
        }
    }
}
