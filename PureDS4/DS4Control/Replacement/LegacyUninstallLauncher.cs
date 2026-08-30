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
using System.Diagnostics;
using System.IO;

namespace DS4Windows
{
    /// <summary>
    /// A predecessor's own uninstall command, split into an executable and
    /// its arguments. PureDS4 never invents one of these: the only source
    /// is the UninstallString the other product itself registered in
    /// Add/Remove Programs.
    /// </summary>
    internal sealed class LegacyUninstallCommand
    {
        private LegacyUninstallCommand(string executablePath, string arguments)
        {
            ExecutablePath = executablePath;
            Arguments = arguments;
        }

        internal string ExecutablePath { get; }
        internal string Arguments { get; }

        /// <summary>
        /// Splits a registered UninstallString into its executable and
        /// arguments. Handles both of the shapes Windows products actually
        /// register: a quoted path followed by switches (typical of a Burn
        /// bundle in the package cache) and a bare command such as
        /// <c>MsiExec.exe /X{GUID}</c>.
        /// </summary>
        internal static bool TryParse(string uninstallString,
            out LegacyUninstallCommand command)
        {
            command = null;
            if (string.IsNullOrWhiteSpace(uninstallString))
            {
                return false;
            }

            string value = uninstallString.Trim();
            string executablePath;
            string arguments;

            if (value.StartsWith("\"", StringComparison.Ordinal))
            {
                int closingQuote = value.IndexOf('"', 1);
                if (closingQuote <= 1)
                {
                    return false;
                }

                executablePath = value.Substring(1, closingQuote - 1);
                arguments = value.Substring(closingQuote + 1).Trim();
            }
            else
            {
                // An unquoted command whose path contains spaces is
                // genuinely ambiguous. Resolve it the way Windows itself
                // does, by taking the longest leading token that ends in
                // ".exe" at a word boundary: that reads
                // "MsiExec.exe /X{...}" and
                // "C:\Program Files\App\uninst.exe /S" correctly, and
                // leaves a bare path with spaces and no arguments intact
                // rather than truncating it at "C:\Program".
                int split = FindExecutableBoundary(value);
                if (split < 0)
                {
                    int firstSpace = value.IndexOf(' ');
                    if (firstSpace < 0)
                    {
                        executablePath = value;
                        arguments = string.Empty;
                    }
                    else
                    {
                        executablePath = value.Substring(0, firstSpace);
                        arguments = value.Substring(firstSpace + 1).Trim();
                    }
                }
                else
                {
                    executablePath = value.Substring(0, split);
                    arguments = value.Substring(split).Trim();
                }
            }

            if (string.IsNullOrWhiteSpace(executablePath))
            {
                return false;
            }

            command = new LegacyUninstallCommand(executablePath, arguments);
            return true;
        }

        /// <summary>
        /// The index just past the first ".exe" that sits at a word
        /// boundary, or -1 when the value contains no such token.
        /// </summary>
        private static int FindExecutableBoundary(string value)
        {
            const string extension = ".exe";
            int searchFrom = 0;
            while (searchFrom < value.Length)
            {
                int found = value.IndexOf(extension, searchFrom,
                    StringComparison.OrdinalIgnoreCase);
                if (found < 0)
                {
                    return -1;
                }

                int boundary = found + extension.Length;
                if (boundary == value.Length || value[boundary] == ' ')
                {
                    return boundary;
                }

                searchFrom = found + 1;
            }

            return -1;
        }
    }

    /// <summary>Why a predecessor's uninstaller can or cannot be offered.
    /// </summary>
    internal enum LegacyUninstallReadiness
    {
        /// <summary>The command is usable and nothing blocks running it.
        /// </summary>
        Ready,

        /// <summary>The product registered no uninstall command, so there
        /// is nothing PureDS4 could legitimately start.</summary>
        NoCommandRecorded,

        /// <summary>The other application is still running. Uninstalling
        /// underneath a live process leaves debris and can fail
        /// mid-transaction.</summary>
        ApplicationRunning,
    }

    /// <summary>
    /// Step 6 of the replacement flow in AGENTS.md, second pass: handing
    /// off to a predecessor's own uninstaller.
    ///
    /// PureDS4 does not uninstall anything itself. It does not delete a
    /// directory, remove a registry key, or unregister a scheduled task
    /// belonging to another product. All it does is start the uninstall
    /// command that product registered for itself, so Windows Installer
    /// owns the elevation prompt, the progress UI, the transaction, and
    /// the rollback — the same code path as removing it from Apps and
    /// Features by hand.
    /// </summary>
    internal static class LegacyUninstallPolicy
    {
        /// <summary>
        /// True when a registered command actually removes the product,
        /// rather than repairing or modifying it.
        ///
        /// This distinction is not academic. A Burn-installed product
        /// registers two Add/Remove Programs entries: the bundle, whose
        /// command ends in <c>/uninstall</c>, and the MSI underneath it,
        /// which commonly registers <c>MsiExec.exe /I{GUID}</c> — the
        /// *modify* verb, which opens maintenance mode. Starting that one
        /// under an "Uninstall" label would show the user the wrong dialog,
        /// and removing the inner MSI directly would strand the bundle's
        /// own registration. PureDS4 never rewrites another product's
        /// command to fix this; it declines to offer one it cannot read as
        /// an uninstall.
        /// </summary>
        internal static bool IsUninstallCommand(
            LegacyUninstallCommand command)
        {
            if (command == null)
            {
                return false;
            }

            string arguments = command.Arguments ?? string.Empty;
            foreach (string token in arguments.Split(' ',
                StringSplitOptions.RemoveEmptyEntries))
            {
                if (token.Equals("/uninstall",
                        StringComparison.OrdinalIgnoreCase) ||
                    token.Equals("-uninstall",
                        StringComparison.OrdinalIgnoreCase) ||
                    token.StartsWith("/x", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The entry whose uninstaller should be offered, preferring one
        /// that genuinely uninstalls. Returns null when no entry carries a
        /// recognisable uninstall command.
        /// </summary>
        internal static UninstallRegistryEntry SelectPrimaryEntry(
            LegacyInstallationSurvey survey)
        {
            ArgumentNullException.ThrowIfNull(survey);

            foreach (UninstallRegistryEntry candidate in
                survey.UninstallEntries)
            {
                if (LegacyUninstallCommand.TryParse(
                        candidate.UninstallString,
                        out LegacyUninstallCommand parsed) &&
                    IsUninstallCommand(parsed))
                {
                    return candidate;
                }
            }

            return null;
        }

        /// <summary>
        /// Whether the uninstaller for <paramref name="entry"/> may be
        /// offered, given what the detector last saw.
        /// </summary>
        internal static LegacyUninstallReadiness Evaluate(
            LegacyInstallationSurvey survey, UninstallRegistryEntry entry,
            out LegacyUninstallCommand command)
        {
            ArgumentNullException.ThrowIfNull(survey);
            command = null;

            if (entry == null ||
                !LegacyUninstallCommand.TryParse(entry.UninstallString,
                    out command) ||
                !IsUninstallCommand(command))
            {
                command = null;
                return LegacyUninstallReadiness.NoCommandRecorded;
            }

            // Requiring the old application closed is the same rule the
            // launch gate applies, for the same reason: two products must
            // not compete while one of them is being removed.
            if (survey.RequiresApplicationClosed)
            {
                command = null;
                return LegacyUninstallReadiness.ApplicationRunning;
            }

            return LegacyUninstallReadiness.Ready;
        }
    }

    internal static class LegacyUninstallLauncher
    {
        /// <summary>
        /// Starts the predecessor's own uninstaller and returns without
        /// waiting for it. UseShellExecute lets Windows raise the
        /// uninstaller's own elevation prompt; PureDS4 neither requests nor
        /// holds elevation for this, and never bypasses UAC.
        /// </summary>
        internal static void Launch(LegacyUninstallCommand command)
        {
            ArgumentNullException.ThrowIfNull(command);

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = command.ExecutablePath,
                Arguments = command.Arguments,
                UseShellExecute = true,
            };

            string workingDirectory =
                TryGetExistingDirectory(command.ExecutablePath);
            if (workingDirectory != null)
            {
                startInfo.WorkingDirectory = workingDirectory;
            }

            using Process process = Process.Start(startInfo);
        }

        private static string TryGetExistingDirectory(string executablePath)
        {
            try
            {
                string directory = Path.GetDirectoryName(executablePath);
                return !string.IsNullOrEmpty(directory) &&
                    Directory.Exists(directory) ? directory : null;
            }
            catch
            {
                // A bare tool name such as MsiExec.exe has no directory.
                // Letting the shell resolve it from PATH is correct.
                return null;
            }
        }
    }
}
