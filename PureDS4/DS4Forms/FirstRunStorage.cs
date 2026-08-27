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

using System.IO;

namespace DS4WinWPF.DS4Forms
{
    /// <summary>
    /// Applies the first-run storage choice.
    ///
    /// This is deliberately separate from the dialog. The behaviour that
    /// matters is what happens to the two stores on disk, and that has to be
    /// testable without constructing a window.
    ///
    /// The inherited implementation deleted whichever store the user did not
    /// pick, recursively, and did so by default. PureDS4 does not: selecting a
    /// location records which store is active and nothing else. Configuration
    /// is preserved unless deletion has been explicitly authorized, and
    /// removing or importing an old store belongs to the replacement workflow
    /// rather than to picking a folder on first run.
    /// </summary>
    internal static class FirstRunStorage
    {
        /// <summary>
        /// Makes <paramref name="location"/> the active store.
        /// </summary>
        /// <param name="freshInstall">
        /// True when neither location holds configuration yet, in which case
        /// the chosen store is seeded with a default profile. When both
        /// locations already hold data, nothing is written and nothing is
        /// removed: the user is only choosing which existing store to use.
        /// </param>
        internal static void Activate(FirstRunStorageLocation location,
            bool freshInstall)
        {
            string target = ResolvePath(location);

            DS4Windows.Global.SaveWhere(target);

            if (freshInstall)
            {
                DS4Windows.Global.SaveDefault(
                    Path.Combine(target, "Profiles.xml"));
            }
        }

        /// <summary>
        /// The directory a storage choice refers to. Both are PureDS4-owned:
        /// no branch of this can resolve to another product's configuration.
        /// </summary>
        internal static string ResolvePath(FirstRunStorageLocation location)
        {
            return location == FirstRunStorageLocation.PortableFolder
                ? DS4Windows.Global.exedirpath
                : DS4Windows.Global.appDataPpath;
        }
    }
}
