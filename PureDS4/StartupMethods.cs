/*
DS4Windows
Copyright (C) 2023  Travis Nickles

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
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32.TaskScheduler;
using Task = Microsoft.Win32.TaskScheduler.Task;

namespace DS4WinWPF
{
    [System.Security.SuppressUnmanagedCodeSecurity]
    public static class StartupMethods
    {
        // PureDS4 owns exactly one Startup shortcut. The shortcuts written by
        // DS4Windows and by an earlier DS4Windows Reworked install belong to
        // those products: an ordinary startup preference must not report them
        // as its own state, adopt them, or delete them. Removing them belongs
        // to an explicit, owner-visible replacement flow.
        public static string lnkpath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Startup),
            DS4Windows.ProductIdentity.StartupShortcutFileName);

        public static bool HasStartProgEntry()
        {
            // Exception handling should not be needed here. Method handles most cases
            return File.Exists(lnkpath);
        }

        public static bool HasTaskEntry()
        {
            using TaskService ts = new TaskService();
            using Task tasker = ts.GetTask(
                @"\" + DS4Windows.ProductIdentity.StartupTaskName);
            return tasker != null && TaskTargetsCurrentExecutable(
                tasker.Definition, tasker.Enabled,
                DS4Windows.Global.exelocation, DS4Windows.Global.exedirpath,
                WindowsIdentity.GetCurrent().User?.Value);
        }

        public static bool IsRunAtStartupEnabled()
        {
            if (HasStartProgEntry())
            {
                return true;
            }

            try
            {
                return HasTaskEntry();
            }
            catch
            {
                // A Task Scheduler failure must not be interpreted as an
                // affirmative startup preference by setup.
                return false;
            }
        }

        public static void WriteStartProgEntry()
        {
            Type t = Type.GetTypeFromCLSID(new Guid("72C24DD5-D70A-438B-8A42-98424B88AFB8")); // Windows Script Host Shell Object
            dynamic shell = Activator.CreateInstance(t);
            try
            {
                var lnk = shell.CreateShortcut(lnkpath);
                try
                {
                    string app = DS4Windows.Global.exelocation;
                    lnk.TargetPath = DS4Windows.Global.exelocation;
                    lnk.Arguments = "-m";
                    // Need to add the install directory as cwd or
                    // language assemblies cannot be discovered
                    lnk.WorkingDirectory = DS4Windows.Global.exedirpath;

                    //lnk.TargetPath = Assembly.GetExecutingAssembly().Location;
                    //lnk.Arguments = "-m";
                    lnk.IconLocation = app.Replace('\\', '/');
                    lnk.Save();
                }
                finally
                {
                    Marshal.FinalReleaseComObject(lnk);
                }
            }
            finally
            {
                Marshal.FinalReleaseComObject(shell);
            }
        }

        public static void DeleteStartProgEntry()
        {
            DeleteShortcutIfWritable(lnkpath);
        }

        public static bool CanWriteStartEntry()
        {
            return !IsExistingShortcutReadOnly(lnkpath);
        }

        public static bool CheckStartupExeLocation()
        {
            string lnkprogpath = ResolveShortcut(lnkpath);
            return lnkprogpath != DS4Windows.Global.exelocation;
        }

        private static bool IsExistingShortcutReadOnly(string path)
        {
            return File.Exists(path) && new FileInfo(path).IsReadOnly;
        }

        private static void DeleteShortcutIfWritable(string path)
        {
            if (File.Exists(path) && !new FileInfo(path).IsReadOnly)
            {
                File.Delete(path);
            }
        }

        public static void LaunchOldTask()
        {
            TaskService ts = new TaskService();
            Task tasker = ts.GetTask(
                @"\" + DS4Windows.ProductIdentity.StartupTaskName);
            if (tasker != null)
            {
                tasker.Run("");
            }
        }

        private static string ResolveShortcut(string filePath)
        {
            Type t = Type.GetTypeFromCLSID(new Guid("72C24DD5-D70A-438B-8A42-98424B88AFB8")); // Windows Script Host Shell Object
            dynamic shell = Activator.CreateInstance(t);
            string result;

            try
            {
                var shortcut = shell.CreateShortcut(filePath);
                result = shortcut.TargetPath;
                Marshal.FinalReleaseComObject(shortcut);
            }
            catch (COMException)
            {
                // A COMException is thrown if the file is not a valid shortcut (.lnk) file 
                result = null;
            }
            finally
            {
                Marshal.FinalReleaseComObject(shell);
            }

            return result;
        }

        private static bool PathsEqual(string first, string second)
        {
            if (string.IsNullOrWhiteSpace(first) ||
                string.IsNullOrWhiteSpace(second))
            {
                return false;
            }

            try
            {
                return string.Equals(Path.GetFullPath(first),
                    Path.GetFullPath(second),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        // The same read-only predicate is used for live definitions and
        // unregistered installer fixtures. It never registers or runs a task.
        internal static bool TaskTargetsCurrentExecutable(
            TaskDefinition definition, bool enabled, string executablePath,
            string executableDirectory, string currentUserSid)
        {
            if (definition == null || definition.Actions.Count != 1 ||
                definition.Actions[0] is not ExecAction action ||
                definition.Triggers.Count != 1 ||
                definition.Triggers[0] is not LogonTrigger trigger)
            {
                return false;
            }

            return enabled && definition.Settings.Enabled &&
                definition.Principal.RunLevel == TaskRunLevel.Highest &&
                definition.Principal.LogonType ==
                    TaskLogonType.InteractiveToken &&
                AccountMatchesSid(definition.Principal.UserId,
                    currentUserSid) &&
                trigger.Enabled &&
                (string.IsNullOrWhiteSpace(trigger.UserId) ||
                 AccountMatchesSid(trigger.UserId, currentUserSid)) &&
                definition.Settings.ExecutionTimeLimit == TimeSpan.Zero &&
                definition.Settings.MultipleInstances ==
                    TaskInstancesPolicy.IgnoreNew &&
                IsSupportedOwnedTaskPriority(definition.Settings.Priority) &&
                !definition.Settings.StopIfGoingOnBatteries &&
                !definition.Settings.DisallowStartIfOnBatteries &&
                PathsEqual(action.Path, executablePath) &&
                string.Equals(action.Arguments?.Trim(), "-m",
                    StringComparison.Ordinal) &&
                PathsEqual(action.WorkingDirectory, executableDirectory);
        }

        // New-ScheduledTaskSettingsSet uses Windows priority 7 (BelowNormal).
        // Accept that installer contract without rewriting registered tasks;
        // retain High for already-existing definitions accepted by older builds.
        // Process priority is distinct from the required elevated RunLevel.
        internal static bool IsSupportedOwnedTaskPriority(
            ProcessPriorityClass priority) =>
            priority == ProcessPriorityClass.BelowNormal ||
            priority == ProcessPriorityClass.High;

        private static bool AccountMatchesSid(string account,
            string expectedSid)
        {
            if (string.IsNullOrWhiteSpace(account) ||
                string.IsNullOrWhiteSpace(expectedSid))
            {
                return false;
            }

            try
            {
                string actualSid = account.StartsWith("S-1-",
                        StringComparison.OrdinalIgnoreCase)
                    ? new SecurityIdentifier(account).Value
                    : ((SecurityIdentifier)new NTAccount(account).Translate(
                        typeof(SecurityIdentifier))).Value;
                return string.Equals(actualSid, expectedSid,
                    StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

    }
}
