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
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using Microsoft.Win32.TaskScheduler;

namespace DS4Windows
{
    /// <summary>
    /// One installed application's Add/Remove Programs entry, as far as
    /// detection needs it.
    /// </summary>
    internal sealed class UninstallRegistryEntry
    {
        internal UninstallRegistryEntry(string displayName,
            string displayVersion, string installLocation,
            string uninstallString)
        {
            DisplayName = displayName ?? string.Empty;
            DisplayVersion = displayVersion ?? string.Empty;
            InstallLocation = installLocation ?? string.Empty;
            UninstallString = uninstallString ?? string.Empty;
        }

        internal string DisplayName { get; }
        internal string DisplayVersion { get; }
        internal string InstallLocation { get; }
        internal string UninstallString { get; }
    }

    /// <summary>
    /// Every host fact the legacy-installation detector needs, behind one
    /// seam. All reads; nothing here may mutate host state.
    ///
    /// The detector is exercised against this interface rather than the real
    /// registry, process list, task scheduler and filesystem directly, so it
    /// can be tested without touching the machine PureDS4 is actually
    /// running on — inspecting a real DS4Windows install here means the real
    /// thing has to stay real, and a test run must never depend on, or
    /// disturb, whatever happens to be installed on the developer's machine.
    /// </summary>
    internal interface ILegacyInstallationEnvironment
    {
        /// <summary>%ProgramFiles% and %ProgramFiles(x86)%, whichever apply
        /// on this machine.</summary>
        IReadOnlyList<string> ProgramFilesRoots { get; }

        /// <summary>%AppData% (roaming) for the current user.</summary>
        string RoamingAppDataPath { get; }

        bool DirectoryExists(string path);

        bool FileExists(string path);

        /// <summary>True when the given HKLM subkey path exists at all,
        /// regardless of its values.</summary>
        bool LocalMachineKeyExists(string subKeyPath);

        /// <summary>Every currently running process image name, without the
        /// ".exe" extension, exactly as <see cref="Process.ProcessName"/>
        /// reports it.</summary>
        IReadOnlyList<string> RunningProcessNames();

        /// <summary>True when a scheduled task with this exact name exists
        /// at the root task path, whatever its enabled state.</summary>
        bool ScheduledTaskExists(string taskName);

        /// <summary>Every Add/Remove Programs entry under the machine-wide
        /// (not per-user) uninstall registry root, 32-bit and 64-bit views
        /// alike.</summary>
        IReadOnlyList<UninstallRegistryEntry> UninstallEntries();
    }

    /// <summary>
    /// The real environment: actual registry, process list, task scheduler
    /// and filesystem. Every member is read-only by construction — there is
    /// no write, delete, start, or stop anywhere in this class.
    /// </summary>
    internal sealed class Win32LegacyInstallationEnvironment
        : ILegacyInstallationEnvironment
    {
        public IReadOnlyList<string> ProgramFilesRoots
        {
            get
            {
                List<string> roots = new List<string>();
                string programFiles = Environment.GetFolderPath(
                    Environment.SpecialFolder.ProgramFiles);
                string programFilesX86 = Environment.GetFolderPath(
                    Environment.SpecialFolder.ProgramFilesX86);

                if (!string.IsNullOrEmpty(programFiles))
                {
                    roots.Add(programFiles);
                }

                if (!string.IsNullOrEmpty(programFilesX86) &&
                    !string.Equals(programFilesX86, programFiles,
                        StringComparison.OrdinalIgnoreCase))
                {
                    roots.Add(programFilesX86);
                }

                return roots;
            }
        }

        public string RoamingAppDataPath => Environment.GetFolderPath(
            Environment.SpecialFolder.ApplicationData);

        public bool DirectoryExists(string path)
        {
            return !string.IsNullOrWhiteSpace(path) && Directory.Exists(path);
        }

        public bool FileExists(string path)
        {
            return !string.IsNullOrWhiteSpace(path) && File.Exists(path);
        }

        public bool LocalMachineKeyExists(string subKeyPath)
        {
            try
            {
                using RegistryKey key =
                    Registry.LocalMachine.OpenSubKey(subKeyPath);
                return key != null;
            }
            catch (System.Security.SecurityException)
            {
                return false;
            }
        }

        public IReadOnlyList<string> RunningProcessNames()
        {
            List<string> names = new List<string>();
            foreach (Process process in Process.GetProcesses())
            {
                try
                {
                    names.Add(process.ProcessName);
                }
                catch
                {
                    // A process that exits between enumeration and read is
                    // not evidence either way; skip it rather than fail the
                    // whole scan.
                }
                finally
                {
                    process.Dispose();
                }
            }

            return names;
        }

        public bool ScheduledTaskExists(string taskName)
        {
            if (string.IsNullOrWhiteSpace(taskName))
            {
                return false;
            }

            try
            {
                using TaskService taskService = new TaskService();
                using Task task = taskService.GetTask(@"\" + taskName);
                return task != null;
            }
            catch
            {
                // Task Scheduler being unavailable is not evidence that the
                // task does not exist.
                return false;
            }
        }

        public IReadOnlyList<UninstallRegistryEntry> UninstallEntries()
        {
            List<UninstallRegistryEntry> entries =
                new List<UninstallRegistryEntry>();

            foreach (string uninstallRoot in new[]
            {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
            })
            {
                ReadUninstallEntries(uninstallRoot, entries);
            }

            return entries;
        }

        private static void ReadUninstallEntries(string uninstallRoot,
            List<UninstallRegistryEntry> entries)
        {
            try
            {
                using RegistryKey root =
                    Registry.LocalMachine.OpenSubKey(uninstallRoot);
                if (root == null)
                {
                    return;
                }

                foreach (string subKeyName in root.GetSubKeyNames())
                {
                    try
                    {
                        using RegistryKey subKey =
                            root.OpenSubKey(subKeyName);
                        string displayName =
                            subKey?.GetValue("DisplayName") as string;
                        if (string.IsNullOrWhiteSpace(displayName))
                        {
                            continue;
                        }

                        entries.Add(new UninstallRegistryEntry(
                            displayName,
                            subKey.GetValue("DisplayVersion") as string,
                            subKey.GetValue("InstallLocation") as string,
                            subKey.GetValue("UninstallString") as string));
                    }
                    catch
                    {
                        // One malformed entry must not abort the whole scan.
                    }
                }
            }
            catch
            {
                // A missing or inaccessible uninstall root reports no
                // entries from that view rather than failing detection.
            }
        }
    }
}
