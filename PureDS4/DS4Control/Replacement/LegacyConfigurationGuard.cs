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
using System.IO;

namespace DS4Windows
{
    /// <summary>
    /// Step 3 of the replacement flow described in AGENTS.md: preserve or
    /// archive user configuration unless deletion was specifically
    /// authorized.
    ///
    /// PureDS4 currently reads and writes only paths it owns itself
    /// (%AppData%\PureDS4, or a portable folder beside PureDS4.exe — see
    /// <see cref="Global.SaveWhere"/> and its two callers,
    /// <see cref="Global.FindConfigLocation"/> and
    /// <c>DS4WinWPF.DS4Forms.FirstRunStorage</c>). Nothing computes a path
    /// under DS4Windows' own configuration directory today, so this class
    /// does not need to preserve or archive anything by itself. What it
    /// does is turn that fact into an enforced invariant rather than an
    /// unverified belief: <see cref="Global.SaveWhere"/> is the single
    /// choke point every stored-configuration path passes through, and it
    /// calls <see cref="EnsureNotLegacyConfigurationPath"/> before
    /// accepting one. A future change that accidentally pointed PureDS4's
    /// own storage at DS4Windows' configuration directory — which would
    /// put that data at risk the next time PureDS4 saves — fails loudly
    /// here instead of silently writing into another product's files.
    /// </summary>
    internal static class LegacyConfigurationGuard
    {
        /// <summary>
        /// True when <paramref name="candidatePath"/> refers to the same
        /// directory as <paramref name="legacyConfigurationDirectory"/>,
        /// independent of trailing separators, relative segments, or case.
        /// Pure string/path comparison — takes the legacy directory as a
        /// parameter instead of reading the real environment, so it can be
        /// exercised with arbitrary paths in a test.
        /// </summary>
        internal static bool IsLegacyConfigurationPath(string candidatePath,
            string legacyConfigurationDirectory)
        {
            if (string.IsNullOrWhiteSpace(candidatePath) ||
                string.IsNullOrWhiteSpace(legacyConfigurationDirectory))
            {
                return false;
            }

            return string.Equals(
                NormalizeForComparison(candidatePath),
                NormalizeForComparison(legacyConfigurationDirectory),
                StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeForComparison(string path)
        {
            return Path.GetFullPath(path)
                .TrimEnd(Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);
        }

        /// <summary>
        /// DS4Windows' own configuration directory on this real machine,
        /// using the same roaming AppData root and directory name the
        /// step-1 detector looks for.
        /// </summary>
        internal static string ResolveLegacyConfigurationDirectory()
        {
            return Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.ApplicationData),
                LegacyInstallationDetector.ConfigurationDirectoryName);
        }

        /// <summary>
        /// Throws if <paramref name="candidatePath"/> is DS4Windows' own
        /// configuration directory. Called from <see
        /// cref="Global.SaveWhere"/>, the single place PureDS4 decides
        /// where its own configuration lives, before that path is accepted.
        /// </summary>
        internal static void EnsureNotLegacyConfigurationPath(
            string candidatePath)
        {
            string legacyDirectory = ResolveLegacyConfigurationDirectory();
            if (IsLegacyConfigurationPath(candidatePath, legacyDirectory))
            {
                throw new InvalidOperationException(
                    "PureDS4 refused to use \"" + candidatePath + "\" as " +
                    "its own configuration directory: it is DS4Windows' " +
                    "configuration directory. PureDS4 must never write to " +
                    "or delete another product's data.");
            }
        }
    }
}
