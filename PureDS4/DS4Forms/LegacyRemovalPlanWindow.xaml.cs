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

using DS4Windows;
using System.Linq;
using System.Windows;

namespace DS4WinWPF.DS4Forms
{
    /// <summary>
    /// Step 6 of the replacement flow described in AGENTS.md, first pass:
    /// shows what removing a detected DS4Windows or DS4Windows Reworked
    /// installation would involve. This screen only reads and describes;
    /// it does not uninstall anything, delete a directory, disable a task,
    /// or touch the registry. Actually performing a removal is a later,
    /// separately authorized step.
    /// </summary>
    public partial class LegacyRemovalPlanWindow : Window
    {
        public LegacyRemovalPlanWindow()
            : this(LegacyInstallationDetector.Scan(
                new Win32LegacyInstallationEnvironment()))
        {
        }

        internal LegacyRemovalPlanWindow(LegacyInstallationSurvey survey)
        {
            InitializeComponent();

            headingText.Text = "DS4Windows removal plan";

            LegacyRemovalPlan plan = LegacyRemovalPlanner.Build(survey);
            summaryText.Text = BuildSummary(plan);
            itemListBorder.Visibility = plan.HasAnythingToRemove
                ? Visibility.Visible : Visibility.Collapsed;
            itemList.ItemsSource = plan.Items
                .Select(item => $"{item.Description}: {item.Detail}")
                .ToList();

            if (plan.ConfigurationWouldSurvive)
            {
                preservationText.Text =
                    "Your DS4Windows profiles and settings are not part of " +
                    "this plan. Windows application uninstallers do not " +
                    "normally delete a per-user AppData folder, and " +
                    "PureDS4 does not either — removing that data, if " +
                    "you ever want to, is a separate, manual decision.";
                preservationText.Visibility = Visibility.Visible;
            }
        }

        internal static string BuildSummary(LegacyRemovalPlan plan)
        {
            if (!plan.HasAnythingToRemove)
            {
                return "No DS4Windows or DS4Windows Reworked installation " +
                    "was detected on this machine. There is nothing to " +
                    "remove.";
            }

            return "This screen only describes what removing the old " +
                "installation would involve. Nothing on this machine is " +
                "changed by looking at it, and PureDS4 does not remove " +
                "anything from here — use Windows' own \"Apps\" or " +
                "\"Programs and Features\" list to actually uninstall it.";
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
