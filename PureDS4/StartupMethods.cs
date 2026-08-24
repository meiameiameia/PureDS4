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
        private const string StartupShortcutName = "DS4Windows Reworked.lnk";
        private const string LegacyStartupShortcutName = "DS4Windows.lnk";

        public static string lnkpath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Startup),
            StartupShortcutName);

        private static readonly string legacyLnkPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Startup),
            LegacyStartupShortcutName);

        public static bool HasStartProgEntry()
        {
            // Exception handling should not be needed here. Method handles most cases
            return File.Exists(lnkpath) || File.Exists(legacyLnkPath);
        }

        public static bool HasTaskEntry()
        {
            using TaskService ts = new TaskService();
            using Task tasker = ts.GetTask(@"\RunPureDS4");
            return tasker != null && TaskTargetsCurrentExecutable(tasker);
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
                    // Need to add the DS4Windows directory as cwd or
                    // language assemblies cannot be discovered
                    lnk.WorkingDirectory = DS4Windows.Global.exedirpath;

                    //lnk.TargetPath = Assembly.GetExecutingAssembly().Location;
                    //lnk.Arguments = "-m";
                    lnk.IconLocation = app.Replace('\\', '/');
                    lnk.Save();

                    DeleteShortcutIfWritable(legacyLnkPath);
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
            DeleteShortcutIfWritable(legacyLnkPath);
        }

        public static bool CanWriteStartEntry()
        {
            return !IsExistingShortcutReadOnly(lnkpath) &&
                !IsExistingShortcutReadOnly(legacyLnkPath);
        }

        public static bool CheckStartupExeLocation()
        {
            string shortcutPath = File.Exists(lnkpath) ? lnkpath : legacyLnkPath;
            string lnkprogpath = ResolveShortcut(shortcutPath);
            return lnkprogpath != DS4Windows.Global.exelocation;
        }

        public static void MigrateLegacyStartProgEntry()
        {
            if (File.Exists(lnkpath) || !File.Exists(legacyLnkPath) ||
                IsExistingShortcutReadOnly(legacyLnkPath))
            {
                return;
            }

            WriteStartProgEntry();
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
            Task tasker = ts.GetTask(@"\RunPureDS4");
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

        private static bool TaskTargetsCurrentExecutable(Task task)
        {
            if (task.Definition.Actions.Count != 1 ||
                task.Definition.Actions[0] is not ExecAction action ||
                task.Definition.Triggers.Count != 1 ||
                task.Definition.Triggers[0] is not LogonTrigger trigger)
            {
                return false;
            }

            TaskDefinition definition = task.Definition;
            string currentUserSid = WindowsIdentity.GetCurrent().User?.Value;
            return task.Enabled && definition.Settings.Enabled &&
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
                definition.Settings.Priority == ProcessPriorityClass.High &&
                !definition.Settings.StopIfGoingOnBatteries &&
                !definition.Settings.DisallowStartIfOnBatteries &&
                PathsEqual(action.Path, DS4Windows.Global.exelocation) &&
                string.Equals(action.Arguments?.Trim(), "-m",
                    StringComparison.Ordinal) &&
                PathsEqual(action.WorkingDirectory,
                    DS4Windows.Global.exedirpath);
        }

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
