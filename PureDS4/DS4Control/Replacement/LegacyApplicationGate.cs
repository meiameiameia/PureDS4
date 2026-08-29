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

namespace DS4Windows
{
    /// <summary>
    /// Step 2 of the replacement flow described in AGENTS.md: require the
    /// old application to close, through an explicit, owner-visible step,
    /// rather than silently working around it or closing it automatically.
    /// This class holds only the wording decision — which product name to
    /// show the user — so it can be tested without constructing a window.
    /// </summary>
    internal static class LegacyApplicationGate
    {
        /// <summary>
        /// The name to show the user for the installation
        /// <paramref name="survey"/> found running. Prefers the exact
        /// Add/Remove Programs display name when one was found, since that
        /// is the name the user will recognize from their own machine
        /// (typically "DS4Windows" or "DS4Windows Reworked"); falls back to
        /// the generic product name otherwise.
        /// </summary>
        internal static string DescribeDetectedProduct(
            LegacyInstallationSurvey survey)
        {
            System.ArgumentNullException.ThrowIfNull(survey);

            if (survey.UninstallEntries.Count > 0)
            {
                string displayName = survey.UninstallEntries[0].DisplayName;
                if (!string.IsNullOrWhiteSpace(displayName))
                {
                    return displayName;
                }
            }

            return "DS4Windows";
        }
    }
}
