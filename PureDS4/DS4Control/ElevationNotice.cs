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

namespace DS4Windows
{
    /// <summary>
    /// Started without administrator rights, PureDS4 finds the controller but
    /// cannot hide it from games, so no virtual controller is created and the
    /// controller-protection app cannot open either. That state used to reach
    /// only the log, leaving the window looking like everything worked.
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
        /// elevated and can protect the controller.
        /// </summary>
        public static ElevationNotice Evaluate(bool isElevated)
        {
            if (isElevated)
            {
                return None;
            }

            return new ElevationNotice(
                $"{ProductIdentity.Name} is running without administrator rights",
                "Controllers cannot be hidden from games, so no game output is " +
                "created: a game may see the controller twice, or not at all. " +
                "Controller protection settings cannot open either.");
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

    /// <summary>
    /// Restarts PureDS4 with administrator rights. The launcher is injected so
    /// the decision and result mapping stay testable without spawning a process
    /// or showing a real elevation prompt.
    /// </summary>
    public static class ElevationRelaunch
    {
        /// <summary>The Windows error for a dismissed elevation prompt.</summary>
        public const int ErrorCancelled = 1223;

        public delegate void Launcher(string fileName, string arguments);

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
