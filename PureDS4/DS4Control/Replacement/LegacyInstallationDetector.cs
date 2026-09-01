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
using System.IO;

namespace DS4Windows
{
    /// <summary>
    /// Step 1 of the replacement flow described in AGENTS.md: detect a
    /// DS4Windows or DS4Windows Reworked installation, its process, its
    /// tasks, and its configuration. Read-only — this class inspects the
    /// host and reports what it finds; it does not close the process, touch
    /// the tasks, or move any file. Closing the old application, offering
    /// uninstall, and importing configuration are later, separate steps that
    /// build on this survey rather than living inside it.
    ///
    /// Detection is intentionally over-inclusive rather than exact. A false
    /// positive here costs the user one more confirmation dialog later in
    /// the flow; a false negative risks two products fighting over the same
    /// files, which is the failure this whole flow exists to prevent.
    /// </summary>
    internal static class LegacyInstallationDetector
    {
        /// <summary>
        /// HKLM\SOFTWARE key name the inherited installer registers under,
        /// regardless of whether Add/Remove Programs displays "DS4Windows"
        /// or "DS4Windows Reworked".
        /// </summary>
        internal const string RegistrySubKeyName = "DS4Windows";

        /// <summary>Install directory names to look for under each Program
        /// Files root. Both the original product name and this project's own
        /// prior "Reworked" identity are checked, because a machine may
        /// carry either depending on when it was installed.</summary>
        internal static readonly IReadOnlyList<string> InstallDirectoryNames =
            new[] { "DS4Windows", "DS4Windows Reworked" };

        internal const string ConfigurationDirectoryName = "DS4Windows";
        internal const string ExecutableFileName = "DS4Windows.exe";
        internal const string ProcessName = "DS4Windows";
        internal const string StartupTaskName = "RunDS4Windows";
        internal const string ViiperTaskName = "RunVIIPER";

        internal static LegacyInstallationSurvey Scan(
            ILegacyInstallationEnvironment environment)
        {
            ArgumentNullException.ThrowIfNull(environment);

            bool registryRootPresent = environment.LocalMachineKeyExists(
                @"SOFTWARE\" + RegistrySubKeyName);

            List<string> installDirectories = new List<string>();
            List<string> runtimeExecutablePaths = new List<string>();
            foreach (string root in environment.ProgramFilesRoots)
            {
                foreach (string name in InstallDirectoryNames)
                {
                    string candidate = Path.Combine(root, name);
                    if (environment.DirectoryExists(candidate))
                    {
                        installDirectories.Add(candidate);
                        string executablePath = Path.Combine(candidate,
                            ExecutableFileName);
                        if (environment.FileExists(executablePath))
                        {
                            runtimeExecutablePaths.Add(executablePath);
                        }
                    }
                }
            }

            string configurationDirectoryPath = Path.Combine(
                environment.RoamingAppDataPath, ConfigurationDirectoryName);
            bool configurationDirectoryPresent =
                environment.DirectoryExists(configurationDirectoryPath);

            bool processRunning = false;
            foreach (string name in environment.RunningProcessNames())
            {
                if (string.Equals(name, ProcessName,
                    StringComparison.OrdinalIgnoreCase))
                {
                    processRunning = true;
                    break;
                }
            }

            bool startupTaskPresent =
                environment.ScheduledTaskExists(StartupTaskName);
            bool viiperTaskPresent =
                environment.ScheduledTaskExists(ViiperTaskName);

            List<UninstallRegistryEntry> uninstallEntries =
                new List<UninstallRegistryEntry>();
            foreach (UninstallRegistryEntry entry in
                environment.UninstallEntries())
            {
                if (entry.DisplayName.Contains(ProcessName,
                    StringComparison.OrdinalIgnoreCase))
                {
                    uninstallEntries.Add(entry);
                }
            }

            return new LegacyInstallationSurvey(
                registryRootPresent,
                installDirectories,
                configurationDirectoryPresent,
                configurationDirectoryPath,
                processRunning,
                startupTaskPresent,
                viiperTaskPresent,
                uninstallEntries,
                runtimeExecutablePaths);
        }
    }
}
