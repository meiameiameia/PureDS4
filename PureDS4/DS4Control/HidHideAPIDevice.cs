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
using System.Buffers.Binary;
using System.ComponentModel;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using DS4Windows;

namespace DS4WinWPF.DS4Control
{
    internal interface IHidHideBlacklistDevice
    {
        List<string> GetBlacklist();
        bool SetBlacklist(List<string> instances);
    }

    internal interface IHidHideWhitelistDevice
    {
        List<string> GetWhitelist();
        bool SetWhitelist(List<string> instances);
    }

    class HidHideAPIDevice : IDisposable, IHidHideBlacklistDevice,
        IHidHideWhitelistDevice
    {
        private const int MaximumConfigurationListBytes = 1024 * 1024;
        private const uint IOCTL_GET_WHITELIST = 0x80016000;
        private const uint IOCTL_SET_WHITELIST = 0x80016004;
        private const uint IOCTL_GET_BLACKLIST = 0x80016008;
        private const uint IOCTL_SET_BLACKLIST = 0x8001600C;
        private const uint IOCTL_GET_ACTIVE = 0x80016010;
        private const uint IOCTL_SET_ACTIVE = 0x80016014;
        private const uint IOCTL_GET_WL_INVERT = 0x80016018;
        private const uint IOCTL_SET_WL_INVERT = 0x8001601C;
        private const uint IOCTL_ADD_SESSION_BLACKLIST = 0x80016020;
        private const uint IOCTL_CLR_SESSION_BLACKLIST = 0x80016024;

        private const string CONTROL_DEVICE_FILENAME = "\\\\.\\HidHide";

        private SafeHandle hidHideHandle;

        public HidHideAPIDevice(bool writeAccess = true)
        {
            uint desiredAccess = NativeMethods.GENERIC_READ;
            if (writeAccess)
            {
                desiredAccess |= NativeMethods.GENERIC_WRITE;
            }

            hidHideHandle = NativeMethods.CreateFile(CONTROL_DEVICE_FILENAME,
                    desiredAccess,
                    NativeMethods.FILE_SHARE_READ | NativeMethods.FILE_SHARE_WRITE,
                    IntPtr.Zero,
                    NativeMethods.OpenExisting,
                    NativeMethods.FILE_ATTRIBUTE_NORMAL, 0);
        }

        public bool GetActiveState()
        {
            if (!TryGetActiveState(out bool result))
            {
                throw new InvalidDataException(
                    "HidHide active state could not be read.");
            }
            return result;
        }

        public bool TryGetActiveState(out bool state)
        {
            bool result = false;
            bool succeeded;
            unsafe
            {
                int bytesReturned = 0;
                succeeded = NativeMethods.DeviceIoControl(
                    hidHideHandle.DangerousGetHandle(),
                    HidHideAPIDevice.IOCTL_GET_ACTIVE,
                    IntPtr.Zero,
                    0,
                    new IntPtr(&result),
                    1,
                    ref bytesReturned,
                    IntPtr.Zero);
                succeeded &= bytesReturned == 1;
            }
            state = result;
            return succeeded;
        }

        public bool SetActiveState(bool state)
        {
            bool result = false;

            unsafe
            {
                int bytesReturned = 0;
                result = NativeMethods.DeviceIoControl(hidHideHandle.DangerousGetHandle(),
                    HidHideAPIDevice.IOCTL_SET_ACTIVE,
                    new IntPtr(&state),
                    1,
                    IntPtr.Zero,
                    0,
                    ref bytesReturned,
                    IntPtr.Zero);

                //int error = Marshal.GetLastWin32Error();
            }

            return result;
        }

        public List<string> GetBlacklist()
        {
            return ReadMultiSzList(IOCTL_GET_BLACKLIST,
                "persistent blacklist");
        }

        public bool SetBlacklist(List<string> instances)
        {
            bool result = false;
            int bytesReturned = 0;
            IntPtr inBuffer =
                StringListToMultiSzPointer(instances, out int inBufferLength);

            result = NativeMethods.DeviceIoControl(hidHideHandle.DangerousGetHandle(),
                IOCTL_SET_BLACKLIST,
                inBuffer,
                inBufferLength,
                IntPtr.Zero,
                0,
                ref bytesReturned,
                IntPtr.Zero);

            //int error = Marshal.GetLastWin32Error();
            // Free buffer returned from StringListToMultiSzPointer
            Marshal.FreeHGlobal(inBuffer);

            return result;
        }

        /// <summary>
        /// Adds device instance paths to a process-lifetime blacklist.
        /// Entries are automatically removed by HidHide when this process exits,
        /// regardless of whether the exit is clean or due to a crash.
        /// Requires a HidHide build with session blacklist support. Released
        /// HidHide 1.5 builds do not expose this API, so callers need a fallback.
        /// </summary>
        public bool AddSessionBlacklist(List<string> instances)
        {
            if (instances == null || instances.Count == 0) return true;

            int bytesReturned = 0;
            IntPtr inBuffer = StringListToMultiSzPointer(instances, out int inBufferLength);

            bool result = NativeMethods.DeviceIoControl(hidHideHandle.DangerousGetHandle(),
                IOCTL_ADD_SESSION_BLACKLIST,
                inBuffer,
                inBufferLength,
                IntPtr.Zero,
                0,
                ref bytesReturned,
                IntPtr.Zero);

            Marshal.FreeHGlobal(inBuffer);
            return result;
        }

        /// <summary>
        /// Removes all session blacklist entries registered by this process.
        /// Called automatically by HidHide on process exit; only needed for explicit early release.
        /// </summary>
        public bool ClearSessionBlacklist()
        {
            int bytesReturned = 0;
            return NativeMethods.DeviceIoControl(hidHideHandle.DangerousGetHandle(),
                IOCTL_CLR_SESSION_BLACKLIST,
                IntPtr.Zero, 0, IntPtr.Zero, 0,
                ref bytesReturned, IntPtr.Zero);
        }

        public List<string> GetWhitelist()
        {
            return ReadMultiSzList(IOCTL_GET_WHITELIST, "whitelist");
        }

        public bool SetWhitelist(List<string> instances)
        {
            bool result = false;
            int bytesReturned = 0;
            IntPtr inBuffer =
                StringListToMultiSzPointer(instances, out int inBufferLength);

            result = NativeMethods.DeviceIoControl(hidHideHandle.DangerousGetHandle(),
                IOCTL_SET_WHITELIST,
                inBuffer,
                inBufferLength,
                IntPtr.Zero,
                0,
                ref bytesReturned,
                IntPtr.Zero);

            //int error = Marshal.GetLastWin32Error();
            // Free buffer returned from StringListToMultiSzPointer
            Marshal.FreeHGlobal(inBuffer);

            return result;
        }

        public bool GetWhiteListInverseState()
        {
            if (!TryGetWhiteListInverseState(out bool result))
            {
                throw new InvalidDataException(
                    "HidHide inverse whitelist state could not be read.");
            }
            return result;
        }

        private List<string> ReadMultiSzList(uint ioctl, string listName)
        {
            int requiredBytes = 0;
            bool result = NativeMethods.DeviceIoControl(
                hidHideHandle.DangerousGetHandle(), ioctl, IntPtr.Zero, 0,
                IntPtr.Zero, 0, ref requiredBytes, IntPtr.Zero);
            if (!result)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(),
                    $"HidHide {listName} size query failed.");
            }
            if (requiredBytes < sizeof(char) ||
                requiredBytes > MaximumConfigurationListBytes ||
                requiredBytes % sizeof(char) != 0)
            {
                throw new InvalidDataException(
                    $"HidHide returned an invalid {listName} size.");
            }

            IntPtr buffer = Marshal.AllocHGlobal(requiredBytes);
            try
            {
                int bytesReturned = 0;
                result = NativeMethods.DeviceIoControl(
                    hidHideHandle.DangerousGetHandle(), ioctl, IntPtr.Zero, 0,
                    buffer, requiredBytes, ref bytesReturned, IntPtr.Zero);
                if (!result)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(),
                        $"HidHide {listName} read failed.");
                }
                if (bytesReturned <= 0 || bytesReturned > requiredBytes ||
                    bytesReturned % sizeof(char) != 0)
                {
                    throw new InvalidDataException(
                        $"HidHide returned an invalid {listName} payload.");
                }

                byte[] data = new byte[bytesReturned];
                Marshal.Copy(buffer, data, 0, bytesReturned);
                return ParseMultiSz(data);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        internal static List<string> ParseMultiSz(byte[] data)
        {
            if (data == null || data.Length < sizeof(char) ||
                data.Length > MaximumConfigurationListBytes ||
                data.Length % sizeof(char) != 0)
            {
                throw new InvalidDataException(
                    "HidHide returned an invalid string-list payload.");
            }

            char[] characters = new char[data.Length / sizeof(char)];
            for (int index = 0; index < characters.Length; index++)
            {
                characters[index] = (char)BinaryPrimitives.ReadUInt16LittleEndian(
                    data.AsSpan(index * sizeof(char), sizeof(char)));
            }

            if (characters[0] == '\0')
            {
                if (characters.Length == 1 || characters[1] == '\0')
                {
                    return new List<string>();
                }
                throw new InvalidDataException(
                    "HidHide returned an invalid string-list entry.");
            }

            List<string> entries = new List<string>();
            int start = 0;
            for (int index = 0; index < characters.Length; index++)
            {
                if (characters[index] != '\0')
                {
                    continue;
                }
                if (index == start)
                {
                    return entries;
                }
                entries.Add(new string(characters, start, index - start));
                start = index + 1;
            }
            throw new InvalidDataException(
                "HidHide returned an unterminated string list.");
        }

        public bool TryGetWhiteListInverseState(out bool state)
        {
            bool result = false;
            bool succeeded;
            unsafe
            {
                int bytesReturned = 0;
                succeeded = NativeMethods.DeviceIoControl(
                    hidHideHandle.DangerousGetHandle(),
                    HidHideAPIDevice.IOCTL_GET_WL_INVERT,
                    IntPtr.Zero,
                    0,
                    new IntPtr(&result),
                    1,
                    ref bytesReturned,
                    IntPtr.Zero);
                succeeded &= bytesReturned == 1;
            }
            state = result;
            return succeeded;
        }

        public bool SetWhitelistInverseState(bool state)
        {
            bool result = false;

            unsafe
            {
                int bytesReturned = 0;
                NativeMethods.DeviceIoControl(hidHideHandle.DangerousGetHandle(),
                    HidHideAPIDevice.IOCTL_SET_WL_INVERT,
                    new IntPtr(&state),
                    1,
                    IntPtr.Zero,
                    0,
                    ref bytesReturned,
                    IntPtr.Zero);

                //int error = Marshal.GetLastWin32Error();
            }

            return result;
        }

        public bool IsOpen()
        {
            return hidHideHandle != null && (!hidHideHandle.IsClosed && !hidHideHandle.IsInvalid);
        }

        public void Close()
        {
            if (IsOpen())
            {
                hidHideHandle.Close();
                hidHideHandle.Dispose();
                hidHideHandle = null;
            }
        }

        public void Dispose()
        {
            Close();
        }

        private IntPtr StringListToMultiSzPointer(List<string> strList,
            out int length)
        {
            // Temporary byte list
            IEnumerable<byte> multiSz = new List<byte>();

            // Convert each string into wide multi-byte and add NULL-terminator in between
            multiSz = strList.Aggregate(multiSz,
                (current, entry) =>
                {
                    return current.Concat(Encoding.Unicode.GetBytes(entry))
                                    .Concat(Encoding.Unicode.GetBytes(new[] { char.MinValue }));
                });

            // Add another NULL-terminator to signal end of list
            multiSz = multiSz.Concat(Encoding.Unicode.GetBytes(new[] { char.MinValue }));

            // Convert list to array
            byte[] multiSzArray = multiSz.ToArray();

            // Copy array content to allocated buffer
            length = multiSzArray.Length;
            IntPtr buffer = Marshal.AllocHGlobal(length);
            Marshal.Copy(multiSzArray, 0, buffer, length);

            // Return IntPtr to caller. Caller MUST free data when finished with it
            return buffer;
        }
    }
}
