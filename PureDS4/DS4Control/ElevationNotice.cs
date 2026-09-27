/*
PureDS4
Copyright (C) 2026 meiameiameia

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
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace DS4Windows
{
    /// <summary>
    /// Elevation is a process capability, not evidence that a particular
    /// controller was contained or that its virtual output was created.
    /// </summary>
    public sealed class ElevationNotice
    {
        public const string RestartActionLabel = "Restart as administrator";

        private ElevationNotice(string title, string message)
        {
            Title = title;
            Message = message;
        }

        public string Title { get; }

        public string Message { get; }

        public string ActionLabel => RestartActionLabel;

        /// <summary>No notice: the process already has the rights it needs.</summary>
        public static ElevationNotice None { get; } = null;

        /// <summary>
        /// The notice to show, or <see langword="null"/> when PureDS4 is
        /// elevated. Controller readiness is reported separately.
        /// </summary>
        public static ElevationNotice Evaluate(bool isElevated)
        {
            if (isElevated)
            {
                return None;
            }

            return new ElevationNotice(
                "Some actions need administrator rights",
                "Game output may still work. Setup and protection changes may " +
                "need elevation; keyboard and mouse mappings cannot reach " +
                "an elevated game from this process.");
        }
    }

    public enum ElevationRelaunchResult
    {
        /// <summary>An elevated process started; this one should now exit.</summary>
        Started,

        /// <summary>The user dismissed the Windows elevation prompt.</summary>
        Cancelled,

        /// <summary>Windows refused to start the elevated process.</summary>
        Failed,
    }

    public enum RelaunchStorageLocation
    {
        Unspecified,
        Portable,
        WindowsAccount,
    }

    /// <summary>
    /// Restarts PureDS4 with administrator rights. The launcher is injected so
    /// the decision and result mapping stay testable without spawning a process
    /// or showing a real elevation prompt.
    /// </summary>
    public static class ElevationRelaunch
    {
        /// <summary>The Windows error for a dismissed elevation prompt.</summary>
        public const int ErrorCancelled = 1223;
        public const string PortableStorageArgument = "portable";
        public const string WindowsAccountStorageArgument = "appdata";
        public const int PredecessorExitTimeoutMilliseconds = 30000;

        public delegate void Launcher(string fileName, string arguments);
        public delegate bool PredecessorWaiter(int processId,
            int timeoutMilliseconds);

        public static string BuildArguments(int predecessorProcessId,
            RelaunchStorageLocation storageLocation)
        {
            if (predecessorProcessId <= 0 ||
                storageLocation == RelaunchStorageLocation.Unspecified)
            {
                return null;
            }

            string storageArgument = storageLocation switch
            {
                RelaunchStorageLocation.Portable => PortableStorageArgument,
                RelaunchStorageLocation.WindowsAccount =>
                    WindowsAccountStorageArgument,
                _ => null,
            };
            if (storageArgument == null)
            {
                return null;
            }
            return "-wait-for-process " + predecessorProcessId.ToString(
                CultureInfo.InvariantCulture) + " -storage " + storageArgument;
        }

        public static bool TryParseStorageArgument(string value,
            out RelaunchStorageLocation storageLocation)
        {
            if (string.Equals(value, PortableStorageArgument,
                    StringComparison.OrdinalIgnoreCase))
            {
                storageLocation = RelaunchStorageLocation.Portable;
                return true;
            }

            if (string.Equals(value, WindowsAccountStorageArgument,
                    StringComparison.OrdinalIgnoreCase))
            {
                storageLocation = RelaunchStorageLocation.WindowsAccount;
                return true;
            }

            storageLocation = RelaunchStorageLocation.Unspecified;
            return false;
        }

        public static RelaunchStorageLocation DetectStorageLocation(
            string activePath, string portablePath, string accountPath)
        {
            if (PathsEqual(activePath, portablePath))
            {
                return RelaunchStorageLocation.Portable;
            }

            if (PathsEqual(activePath, accountPath))
            {
                return RelaunchStorageLocation.WindowsAccount;
            }

            return RelaunchStorageLocation.Unspecified;
        }

        public static bool TryResolveStoragePath(
            RelaunchStorageLocation storageLocation, string portablePath,
            string accountPath, Func<string, bool> markerExists,
            out string selectedPath)
        {
            selectedPath = null;
            if (markerExists == null)
            {
                return false;
            }

            string candidate = storageLocation switch
            {
                RelaunchStorageLocation.Portable => portablePath,
                RelaunchStorageLocation.WindowsAccount => accountPath,
                _ => null,
            };
            if (string.IsNullOrWhiteSpace(candidate) ||
                !markerExists(Path.Combine(candidate, "Auto Profiles.xml")))
            {
                return false;
            }

            selectedPath = candidate;
            return true;
        }

        public static bool WaitForPredecessor(int processId,
            int timeoutMilliseconds, PredecessorWaiter waiter)
        {
            return processId > 0 && processId != Environment.ProcessId &&
                timeoutMilliseconds > 0 && waiter != null &&
                waiter(processId, timeoutMilliseconds);
        }

        public static bool WaitForProcessExit(int processId,
            int timeoutMilliseconds)
        {
            try
            {
                using Process predecessor = Process.GetProcessById(processId);
                return predecessor.WaitForExit(timeoutMilliseconds);
            }
            catch (ArgumentException)
            {
                return true;
            }
            catch (InvalidOperationException)
            {
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool PathsEqual(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left) ||
                string.IsNullOrWhiteSpace(right))
            {
                return false;
            }

            try
            {
                return string.Equals(Path.GetFullPath(left).TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar),
                    Path.GetFullPath(right).TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        public static ElevationRelaunchResult Restart(string executablePath,
            string arguments, Launcher launcher)
        {
            if (string.IsNullOrWhiteSpace(executablePath) || launcher == null)
            {
                return ElevationRelaunchResult.Failed;
            }

            try
            {
                launcher(executablePath, arguments ?? string.Empty);
                return ElevationRelaunchResult.Started;
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorCancelled)
            {
                return ElevationRelaunchResult.Cancelled;
            }
            catch (Exception)
            {
                return ElevationRelaunchResult.Failed;
            }
        }

        /// <summary>Starts the elevated process through Windows' own prompt.</summary>
        public static void ShellExecuteElevated(string fileName, string arguments)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments ?? string.Empty,
                UseShellExecute = true,
                Verb = "runas",
            };

            using (Process started = Process.Start(startInfo))
            {
            }
        }
    }
}
