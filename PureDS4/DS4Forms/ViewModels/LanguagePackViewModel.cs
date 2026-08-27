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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DS4Windows;

namespace DS4WinWPF.DS4Forms.ViewModels
{
    public class LanguagePackViewModel
    {
        private List<LangPackItem> langPackList;
        private const string invariantCultureTextValue = "No (English UI)";

        private int selectedIndex;

        public List<LangPackItem> LangPackList { get => langPackList; }
        public int SelectedIndex
        {
            get => selectedIndex;
            set
            {
                if (selectedIndex == value) return;
                selectedIndex = value;
                SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler SelectedIndexChanged;
        public event EventHandler ScanFinished;

        public LanguagePackViewModel()
        {
        }

        public void ScanForLangPacks()
        {
            string tempculture = Thread.CurrentThread.CurrentUICulture.Name;
            //string tempculture = new CultureInfo(Global.UseLang).Name;
            Task.Run(() =>
            {
                CreateLanguageAssembliesBindingSource();

                int index = langPackList.Select((item, idx) => new { item, idx })
                                        .Where(x => x.item.Name == tempculture)
                                        .Select(x => x.idx)
                                        .DefaultIfEmpty(-1)
                                        .First();
                if (index > -1)
                {
                    selectedIndex = index;
                }

                ScanFinished?.Invoke(this, EventArgs.Empty);
            });
        }

        public bool ChangeLanguagePack()
        {
            bool result = false;
            if (selectedIndex > -1)
            {
                LangPackItem item = langPackList[selectedIndex];
                string newValue = item.Name;
                if (newValue != Global.UseLang)
                {
                    Global.UseLang = newValue;
                    //Global.Save();
                    result = true;
                }
            }

            return result;
        }

        private void CreateLanguageAssembliesBindingSource()
        {
            langPackList = EnumerateAvailableLanguages(
                typeof(Global).Assembly).ToList();
        }

        /// <summary>
        /// Every language the running application can actually render. The
        /// first entry is always the invariant English resources built into
        /// the main assembly.
        ///
        /// This asks the runtime to load each candidate satellite assembly
        /// rather than looking for a satellite file on disk. The two answers
        /// differ: a self-contained single-file publish bundles the satellite
        /// assemblies inside the executable, so no per-culture directory
        /// exists and a filesystem probe finds nothing even though every
        /// translation shipped. Satellite resolution is the same mechanism
        /// that already renders localized strings at runtime, so it reports
        /// the truth in a loose build and in a bundled one without unpacking
        /// anything or bypassing assembly integrity checks.
        /// </summary>
        internal static IReadOnlyList<LangPackItem> EnumerateAvailableLanguages(
            Assembly resourceAssembly)
        {
            ArgumentNullException.ThrowIfNull(resourceAssembly);

            List<LangPackItem> languages = new List<LangPackItem>
            {
                new LangPackItem(string.Empty, invariantCultureTextValue),
            };

            foreach (CultureInfo culture in CultureInfo.GetCultures(
                CultureTypes.AllCultures))
            {
                if (string.IsNullOrEmpty(culture.Name) ||
                    !HasSatelliteResources(resourceAssembly, culture))
                {
                    continue;
                }

                languages.Add(new LangPackItem(culture.Name,
                    culture.NativeName));
            }

            return languages;
        }

        private static bool HasSatelliteResources(Assembly resourceAssembly,
            CultureInfo culture)
        {
            try
            {
                return resourceAssembly.GetSatelliteAssembly(culture) != null;
            }
            catch (FileNotFoundException)
            {
                // No translation shipped for this culture.
                return false;
            }
            catch (FileLoadException)
            {
                // Present but unloadable. Offering it would switch the user to
                // a language the application cannot actually render.
                return false;
            }
            catch (BadImageFormatException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                // Culture the runtime will not accept as a satellite target.
                return false;
            }
        }
    }

    public class LangPackItem
    {
        private string name;
        private string nativeName;

        public string Name { get => name; }
        public string NativeName { get => nativeName; }

        public LangPackItem(string name, string nativeName)
        {
            this.name = name;
            this.nativeName = nativeName;
        }
    }
}
