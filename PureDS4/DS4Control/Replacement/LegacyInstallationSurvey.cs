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

using System.Collections.Generic;

namespace DS4Windows
{
    /// <summary>
    /// What <see cref="LegacyInstallationDetector"/> found on this machine,
    /// as separate, individually-inspectable signals rather than one opaque
    /// yes/no. The replacement flow's later steps (requiring the old
    /// application to close, offering uninstall, importing configuration)
    /// each need to know which specific signal fired.
    /// </summary>
    internal sealed class LegacyInstallationSurvey
    {
        internal LegacyInstallationSurvey(bool registryRootPresent,
            IReadOnlyList<string> installDirectories,
            bool configurationDirectoryPresent,
            string configurationDirectoryPath,
            bool processRunning,
            bool startupTaskPresent,
            bool viiperTaskPresent,
            IReadOnlyList<UninstallRegistryEntry> uninstallEntries)
        {
            RegistryRootPresent = registryRootPresent;
            InstallDirectories = installDirectories ??
                System.Array.Empty<string>();
            ConfigurationDirectoryPresent = configurationDirectoryPresent;
            ConfigurationDirectoryPath = configurationDirectoryPath ??
                string.Empty;
            ProcessRunning = processRunning;
            StartupTaskPresent = startupTaskPresent;
            ViiperTaskPresent = viiperTaskPresent;
            UninstallEntries = uninstallEntries ??
                System.Array.Empty<UninstallRegistryEntry>();
        }

        /// <summary>HKLM\SOFTWARE\DS4Windows exists.</summary>
        internal bool RegistryRootPresent { get; }

        /// <summary>Install directories found under any Program Files root,
        /// named "DS4Windows" or "DS4Windows Reworked".</summary>
        internal IReadOnlyList<string> InstallDirectories { get; }

        /// <summary>%AppData%\DS4Windows exists.</summary>
        internal bool ConfigurationDirectoryPresent { get; }
        internal string ConfigurationDirectoryPath { get; }

        /// <summary>A process literally named DS4Windows is running right
        /// now. This is the strongest signal that the old application must
        /// be closed before PureDS4 can safely take over ownership.
        /// </summary>
        internal bool ProcessRunning { get; }

        /// <summary>The RunDS4Windows scheduled task exists, in any enabled
        /// state.</summary>
        internal bool StartupTaskPresent { get; }

        /// <summary>The RunVIIPER scheduled task exists, in any enabled
        /// state. VIIPER itself is shared infrastructure PureDS4 may
        /// legitimately reuse; this signal exists so the replacement flow
        /// can tell a DS4Windows-owned task from a PureDS4-owned one
        /// (RunPureDS4VIIPER) rather than assume there is only one.
        /// </summary>
        internal bool ViiperTaskPresent { get; }

        /// <summary>Add/Remove Programs entries whose display name contains
        /// "DS4Windows" — covers both "DS4Windows" and "DS4Windows
        /// Reworked", and both the Burn bundle and the underlying MSI
        /// entries a Burn install typically registers.</summary>
        internal IReadOnlyList<UninstallRegistryEntry> UninstallEntries { get; }

        /// <summary>
        /// True when any signal indicates a DS4Windows or DS4Windows
        /// Reworked installation is present on this machine. False means
        /// none of the checked signals fired — not a guarantee nothing is
        /// left, only that nothing this detector looks for was found.
        /// </summary>
        internal bool IsPresent =>
            RegistryRootPresent ||
            InstallDirectories.Count > 0 ||
            ConfigurationDirectoryPresent ||
            ProcessRunning ||
            StartupTaskPresent ||
            ViiperTaskPresent ||
            UninstallEntries.Count > 0;

        /// <summary>
        /// True when the old application's process is known to be running.
        /// The replacement flow must require this to be false — the
        /// application closed — before doing anything that assumes
        /// exclusive ownership of shared files or the HidHide blacklist.
        /// </summary>
        internal bool RequiresApplicationClosed => ProcessRunning;
    }
}
