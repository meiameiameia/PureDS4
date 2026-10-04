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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using DS4WinWPF.DS4Control;
using DS4WinWPF.DS4Control.DTOXml;

namespace DS4WinWPF
{
    public class ProfileEntity
    {
        private string name;
        public string Name
        {
            get => name;
            set
            {
                if (name == value) return;
                name = value;
                NameChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public event EventHandler NameChanged;
        public event EventHandler GameOutputDisplayChanged;
        public event EventHandler ModifiedDisplayChanged;
        public event EventHandler ProfileSaved;
        public event EventHandler ProfileDeleted;

        private string ProfilePath => Path.Combine(DS4Windows.Global.appdatapath,
            "Profiles", $"{name}.xml");

        public string GameOutputDisplay => ReadGameOutputDisplay(ProfilePath);

        public string ModifiedDisplay
        {
            get
            {
                try
                {
                    return File.Exists(ProfilePath)
                        ? File.GetLastWriteTime(ProfilePath).ToString("g")
                        : "Not saved";
                }
                catch
                {
                    return "Unavailable";
                }
            }
        }

        internal static string ReadGameOutputDisplay(string profilePath)
        {
            try
            {
                if (!File.Exists(profilePath)) return "Not saved";

                string value = XDocument.Load(profilePath)
                    .Descendants()
                    .FirstOrDefault(element =>
                        element.Name.LocalName == "OutputContDevice")?.Value;
                return DS4Windows.OutContTypeCompatibility.ToDisplayName(
                    OutputSlotPersistDTO.ParseOutputDeviceType(value,
                        DS4Windows.BackingStore.DEFAULT_OUT_CONT_TYPE));
            }
            catch
            {
                return "Unavailable";
            }
        }

        private void NotifyPresentationChanged()
        {
            GameOutputDisplayChanged?.Invoke(this, EventArgs.Empty);
            ModifiedDisplayChanged?.Invoke(this, EventArgs.Empty);
        }

        public void DeleteFile()
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                string filepath = DS4Windows.Global.appdatapath + @"\Profiles\" + name + ".xml";
                if (File.Exists(filepath))
                {
                    File.Delete(filepath);
                    ProfileDeleted?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public bool SaveProfile(int deviceNum, string newName = null)
        {
            string targetName = newName ?? name;
            if (string.IsNullOrWhiteSpace(targetName) ||
                !DS4Windows.Global.SaveProfile(deviceNum, targetName, name))
                return false;
            Name = targetName;
            DS4Windows.Global.CacheExtraProfileInfo(deviceNum);
            NotifyPresentationChanged();
            return true;
        }

        public void FireSaved()
        {
            NotifyPresentationChanged();
            ProfileSaved?.Invoke(this, EventArgs.Empty);
        }

        public void RenameProfile(string newProfileName)
        {
            string oldFilePath = Path.Combine(DS4Windows.Global.appdatapath,
                "Profiles", $"{name}.xml");

            string newFilePath = Path.Combine(DS4Windows.Global.appdatapath,
                "Profiles", $"{newProfileName}.xml");

            if (File.Exists(oldFilePath) && !File.Exists(newFilePath))
            {
                File.Move(oldFilePath, newFilePath);
                // Send NameChanged event so controls get updated with new name
                Name = newProfileName;
                NotifyPresentationChanged();
            }
        }
    }
}
