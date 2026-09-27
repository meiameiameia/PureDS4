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
using System;
using System.Linq;
using System.Windows;
using System.Windows.Automation;

namespace DS4WinWPF.DS4Forms
{
    /// <summary>
    /// Step 6 of the replacement flow described in AGENTS.md: shows what
    /// removing a detected DS4Windows or DS4Windows Reworked installation
    /// would involve, and can hand off to that product's own uninstaller.
    ///
    /// PureDS4 never removes anything itself here. It does not delete a
    /// directory, remove a registry key, or unregister a scheduled task
    /// belonging to another product. The only action available is starting
    /// the uninstall command that product registered for itself, so Windows
    /// Installer owns the elevation prompt, the progress UI, the
    /// transaction and the rollback — the same path as removing it from
    /// Apps and Features by hand.
    /// </summary>
    public partial class LegacyRemovalPlanWindow : Window
    {
        private UninstallRegistryEntry primaryEntry;
        private LegacyUninstallCommand uninstallCommand;
        private readonly bool requireRuntimeOwnershipRelease;

        internal bool ExitRequested { get; private set; }
        internal bool RuntimeOwnershipReleased { get; private set; }

        public LegacyRemovalPlanWindow()
            : this(null, false)
        {
        }

        internal LegacyRemovalPlanWindow(LegacyInstallationSurvey survey,
            bool requireRuntimeOwnershipRelease = false)
        {
            this.requireRuntimeOwnershipRelease =
                requireRuntimeOwnershipRelease;
            ExitRequested = requireRuntimeOwnershipRelease;
            InitializeComponent();

            headingText.Text = requireRuntimeOwnershipRelease
                ? "Finish replacing DS4Windows"
                : "DS4Windows removal plan";
            if (requireRuntimeOwnershipRelease)
            {
                closeButton.Content = "Exit PureDS4";
                AutomationProperties.SetHelpText(closeButton,
                    "Close PureDS4 without activating controller services.");
            }
            Render(survey ?? ScanNow());
        }

        private static LegacyInstallationSurvey ScanNow()
        {
            return LegacyInstallationDetector.Scan(
                new Win32LegacyInstallationEnvironment());
        }

        private void Render(LegacyInstallationSurvey survey)
        {
            LegacyRemovalPlan plan = LegacyRemovalPlanner.Build(survey);
            summaryText.Text = BuildSummary(plan);
            itemListBorder.Visibility = plan.HasDetectedState
                ? Visibility.Visible : Visibility.Collapsed;
            itemList.ItemsSource = plan.Items
                .Select(item => $"{item.Description}: {item.Detail}")
                .ToList();

            preservationText.Visibility = plan.ConfigurationWouldSurvive
                ? Visibility.Visible : Visibility.Collapsed;
            if (plan.ConfigurationWouldSurvive)
            {
                preservationText.Text =
                    "Your DS4Windows profiles and settings are not part of " +
                    "this plan. Windows application uninstallers do not " +
                    "normally delete a per-user AppData folder, and " +
                    "PureDS4 does not either - removing that data, if " +
                    "you ever want to, is a separate, manual decision.";
            }

            primaryEntry = LegacyUninstallPolicy.SelectPrimaryEntry(survey);
            LegacyUninstallReadiness readiness =
                LegacyUninstallPolicy.Evaluate(survey, primaryEntry,
                    out uninstallCommand);

            uninstallButton.Visibility = plan.HasAnythingToRemove
                ? Visibility.Visible : Visibility.Collapsed;
            uninstallButton.IsEnabled =
                readiness == LegacyUninstallReadiness.Ready;
            recheckButton.Visibility = plan.HasAnythingToRemove
                ? Visibility.Visible : Visibility.Collapsed;

            string blocked = DescribeBlocker(readiness);
            blockedText.Text = blocked;
            blockedText.Visibility = string.IsNullOrEmpty(blocked)
                ? Visibility.Collapsed : Visibility.Visible;
        }

        internal static string DescribeBlocker(
            LegacyUninstallReadiness readiness)
        {
            switch (readiness)
            {
                case LegacyUninstallReadiness.ApplicationRunning:
                    return "Close DS4Windows first, including its " +
                        "notification area icon, then press Re-check. " +
                        "Removing it while it is running can leave the " +
                        "installation half-removed.";
                case LegacyUninstallReadiness.NoCommandRecorded:
                    return "PureDS4 could not find a command it recognises " +
                        "as uninstalling this installation, so it will not " +
                        "start one. Remove it from Windows' own Apps list " +
                        "instead.";
                default:
                    return string.Empty;
            }
        }

        internal static string BuildSummary(LegacyRemovalPlan plan)
        {
            if (!plan.HasDetectedState)
            {
                return "No DS4Windows installation " +
                    "was detected on this machine. There is nothing to " +
                    "remove.";
            }

            if (plan.HasResidualState)
            {
                return "No active DS4Windows " +
                    "runtime was detected. Preserved profiles, settings, " +
                    "or a historical registry record remain, but they " +
                    "cannot start the old application and do not block " +
                    "PureDS4.";
            }

            return "PureDS4 replaces DS4Windows rather than running " +
                "alongside it. Below is exactly what removing it involves. " +
                "PureDS4 does not remove any of it itself - it starts " +
                "Windows' own uninstaller for this product, which asks for " +
                "administrator approval and can roll itself back.";
        }

        internal static string BuildConfirmation(string productName,
            LegacyUninstallCommand command)
        {
            return $"Windows will now uninstall {productName}.\n\n" +
                "PureDS4 does not perform the removal itself. It starts " +
                "the uninstaller that this product registered, and Windows " +
                "will ask you to approve it:\n\n" +
                $"{command.ExecutablePath} {command.Arguments}".Trim() +
                "\n\nYour DS4Windows profiles and settings are not deleted " +
                "by this.\n\nContinue?";
        }

        private void UninstallBtn_Click(object sender, RoutedEventArgs e)
        {
            if (uninstallCommand == null || primaryEntry == null)
            {
                return;
            }

            string productName =
                string.IsNullOrWhiteSpace(primaryEntry.DisplayName)
                    ? "DS4Windows" : primaryEntry.DisplayName;
            if (MessageBox.Show(
                    BuildConfirmation(productName, uninstallCommand),
                    "Uninstall " + productName, MessageBoxButton.YesNo,
                    MessageBoxImage.Warning,
                    MessageBoxResult.No) != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                LegacyUninstallLauncher.Launch(uninstallCommand);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Windows could not start the uninstaller for " +
                    productName + ".\n\n" + ex.Message +
                    "\n\nRemove it from Windows' own Apps list instead.",
                    "Could not start the uninstaller", MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            summaryText.Text = "Windows' uninstaller for " + productName +
                " has been started. Follow its prompts, then press " +
                "Re-check to confirm what is left.";
            uninstallButton.IsEnabled = false;
        }

        private void RecheckBtn_Click(object sender, RoutedEventArgs e)
        {
            LegacyInstallationSurvey survey = ScanNow();
            if (requireRuntimeOwnershipRelease &&
                !survey.HasCompetingRuntimeOwnership)
            {
                RuntimeOwnershipReleased = true;
                ExitRequested = false;
                Close();
                return;
            }

            Render(survey);
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            ExitRequested = requireRuntimeOwnershipRelease;
            Close();
        }
    }
}
