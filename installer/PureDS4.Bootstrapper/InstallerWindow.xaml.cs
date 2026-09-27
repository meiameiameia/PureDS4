using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WixToolset.BootstrapperApplicationApi;

namespace PureDS4.Bootstrapper
{
    public partial class InstallerWindow : Window
    {
        private readonly InstallerApplication application;
        private InstallerMode mode;
        private bool applying;

        internal InstallerWindow(InstallerApplication application)
        {
            this.application = application;
            InitializeComponent();
            ApplyWindowsTheme();
            Closing += (_, e) =>
            {
                if (applying) e.Cancel = true;
            };
            Closed += (_, __) => application.OnWindowClosed();
        }

        /// <summary>
        /// Shows the DualShock 3 opt-in and reports whether the driver it
        /// depends on is already present.
        ///
        /// PureDS4 does not ship or install DsHidMini. It is a third-party
        /// user-mode driver whose author asks that it be obtained only from
        /// their own releases, so this build detects it and points there
        /// rather than redistributing it. Nothing here installs, removes, or
        /// reconfigures a driver.
        ///
        /// In a standard installer the whole section stays collapsed, so an
        /// ordinary install never mentions DualShock 3 at all.
        /// </summary>
        private void ConfigureExperimentalDS3Section()
        {
#if PUREDS4_EXPERIMENTAL_DS3
            Ds3Section.Visibility = Visibility.Visible;
            RefreshDsHidMiniStatus();
#else
            Ds3Section.Visibility = Visibility.Collapsed;
#endif
        }

        /// <summary>
        /// Re-reads the driver state and updates the DualShock 3 panel. Split
        /// out so the Re-check button can call it: installing DsHidMini in
        /// another window and pressing Re-check is far less error-prone than
        /// asking someone to close and re-run the whole setup.
        /// </summary>
        private void RefreshDsHidMiniStatus()
        {
            bool present = IsDsHidMiniPresent();
            Ds3DriverStatus.Text = present
                ? "DsHidMini is installed. Open the DsHidMini app and set this "
                    + "controller to SXS or DS4 emulation mode before testing."
                : "DsHidMini is NOT installed. A DualShock 3 cannot be read "
                    + "without it, and PureDS4 does not bundle it.";
            Ds3DriverHelp.Text = present
                ? "Nothing more is needed here. Tick the box above to enable "
                    + "DualShock 3 detection in this build."
                : "Download the .msi from the official releases page (v3.5.1 "
                    + "or newer) and run it, then press Re-check. Requires "
                    + "64-bit Windows 10 or 11.";
        }

        private void Ds3Recheck_Click(object sender, RoutedEventArgs e)
        {
            RefreshDsHidMiniStatus();
        }

        /// <summary>
        /// Read-only probe for the DsHidMini driver. Checks the service
        /// registration rather than enumerating devices, so it reports the
        /// driver as present whether or not a controller is plugged in.
        /// </summary>
        private static bool IsDsHidMiniPresent()
        {
            try
            {
                using (RegistryKey service = Registry.LocalMachine.OpenSubKey(
                    @"SYSTEM\CurrentControlSet\Services\dshidmini"))
                {
                    if (service != null)
                    {
                        return true;
                    }
                }

                using (RegistryKey vendor = Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Nefarius Software Solutions e.U.\DsHidMini"))
                {
                    return vendor != null;
                }
            }
            catch
            {
                // A probe that cannot read the registry must not claim the
                // driver is missing, and must never block the install.
                return false;
            }
        }

        private void Ds3DriverLink_RequestNavigate(object sender,
            System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo(
                        e.Uri.AbsoluteUri) { UseShellExecute = true });
            }
            catch
            {
                // An unavailable browser must not take the installer down.
            }

            e.Handled = true;
        }

        internal void ShowConfirmation(InstallerMode detectedMode, IReadOnlyDictionary<string, PackageState> packages, bool infrastructureHealthy)
        {
            mode = detectedMode;
            HidePages();
            ConfirmationPage.Visibility = Visibility.Visible;
            OptionsCard.Visibility = Visibility.Visible;
            applying = false;
            ConfigureExperimentalDS3Section();

            switch (mode)
            {
                case InstallerMode.Update:
                    ModeTitle.Text = "Update PureDS4";
                    ModeDescription.Text = "A managed DS4Windows installation was found. It will be upgraded in place while profiles and settings are preserved.";
                    ActionButton.Content = "Update";
                    break;
                case InstallerMode.Repair:
                    ModeTitle.Text = "Repair PureDS4";
                    ModeDescription.Text = "This version is already installed. Setup will verify and repair its managed components.";
                    ActionButton.Content = "Repair";
                    break;
                case InstallerMode.Uninstall:
                    ModeTitle.Text = "Uninstall PureDS4";
                    ModeDescription.Text = "PureDS4 and its managed VIIPER installation will be removed. Profiles, settings, and shared system drivers are preserved.";
                    ActionButton.Content = "Uninstall";
                    OptionsCard.Visibility = Visibility.Collapsed;
                    break;
                default:
                    ModeTitle.Text = "Install PureDS4";
                    ModeDescription.Text = "Everything needed for a standard x64 installation is included and works offline.";
                    ActionButton.Content = "Install";
                    break;
            }

            SetStatus(Ds4Status, PackageStatus(packages, "DS4WindowsMsi"));
            SetStatus(ViiperStatus,
                infrastructureHealthy ? "Ready" : "Will install or repair");
            SetStatus(UsbipStatus,
                infrastructureHealthy ? "Ready" : "Will verify before changing");
        }

        /// <summary>
        /// Renders a component status the way the application renders state:
        /// colour and glyph together, so "Ready" and "will be changed" are not
        /// the same accent blue.
        /// </summary>
        private void SetStatus(TextBlock target, string state)
        {
            bool ready = state.StartsWith("Ready", StringComparison.OrdinalIgnoreCase)
                || state.StartsWith("Installed", StringComparison.OrdinalIgnoreCase);
            target.Text = ready ? "✓ " + state : state;
            target.Foreground = (Brush)(ready
                ? FindResource("SuccessBrush")
                : FindResource("AccentBrush"));
        }

        internal void ShowPlanning()
        {
            HidePages();
            ProgressPage.Visibility = Visibility.Visible;
            ProgressTitle.Text = "Preparing installation…";
            ProgressDetail.Text = "Building a safe installation plan";
            OverallProgress.IsIndeterminate = true;
            applying = true;
        }

        internal void ShowApplying()
        {
            OverallProgress.IsIndeterminate = false;
            ProgressTitle.Text = mode == InstallerMode.Uninstall ? "Removing PureDS4…" : "Installing PureDS4…";
            ProgressDetail.Text = "Administrator permission is requested once";
        }

        internal void SetCurrentPackage(string packageId)
        {
            switch (packageId)
            {
                case "CloseRunningApplications": ProgressDetail.Text = "Closing running DS4Windows and VIIPER processes"; break;
                case "DS4WindowsMsi": ProgressDetail.Text = "Installing PureDS4"; break;
                case "ViiperUsbipSetup": ProgressDetail.Text = "Verifying VIIPER and USB-IP"; break;
                case "HidHide": ProgressDetail.Text = "Installing optional HidHide"; break;
                default: ProgressDetail.Text = "Verifying installation"; break;
            }
        }

        internal void ShowInstallerBusyRetry(int attempt, int maximumRetries)
        {
            ProgressDetail.Text = "Another Windows installation is finishing; " +
                "retrying safely (" + attempt + " of " + maximumRetries + ")";
        }

        internal void SetProgress(int percent)
        {
            OverallProgress.Value = Math.Max(0, Math.Min(100, percent));
            ProgressPercent.Text = percent + "%";
        }

        internal void ShowComplete(LaunchAction action, bool hidHideAvailable,
            bool hidHideFailed)
        {
            HidePages();
            CompletePage.Visibility = Visibility.Visible;
            LaunchCheckBox.Visibility = Visibility.Visible;
            OpenCompleteLogButton.Visibility = hidHideFailed
                ? Visibility.Visible : Visibility.Collapsed;
            bool managedOutputNeedsAttention = action != LaunchAction.Uninstall &&
                !hidHideAvailable;
            CompleteStatusBorder.BorderBrush = (Brush)FindResource(
                managedOutputNeedsAttention ? "WarningBrush" : "SuccessBrush");
            CompleteStatusGlyph.Foreground = CompleteStatusBorder.BorderBrush;
            CompleteStatusGlyph.Text = managedOutputNeedsAttention ? "!" : "✓";
            applying = false;
            if (action == LaunchAction.Uninstall)
            {
                CompleteTitle.Text = "PureDS4 was removed";
                CompleteDescription.Text = "Profiles, settings, and shared system drivers were preserved.";
                LaunchCheckBox.Visibility = Visibility.Collapsed;
            }
            else
            {
                CompleteTitle.Text = managedOutputNeedsAttention
                    ? "PureDS4 installed — setup needs attention"
                    : "PureDS4 was installed";
                CompleteDescription.Text = hidHideFailed
                    ? "The app and VIIPER/USB-IP were installed, but HidHide failed. Managed virtual output is not ready. Review the setup log and run setup again to repair HidHide."
                    : hidHideAvailable
                        ? "The app and VIIPER/USB-IP were installed. HidHide is present; PureDS4 checks controller protection when it connects."
                        : "The app and VIIPER/USB-IP were installed without confirmed HidHide protection. Managed virtual output is not ready until HidHide is installed and verified.";
            }
        }

        internal void ShowRestart(bool hidHideFailed)
        {
            HidePages();
            RestartPage.Visibility = Visibility.Visible;
            RestartDescription.Text = "Windows must restart before setup can safely continue. Setup will resume after you sign in.";
            if (hidHideFailed)
            {
                RestartDescription.Text += " HidHide also failed to install; managed virtual output is not ready until it is repaired.";
            }
            RestartNowButton.IsEnabled = true;
            applying = false;
        }

        internal void ShowFailure(string message)
        {
            HidePages();
            FailurePage.Visibility = Visibility.Visible;
            FailureMessage.Text = message;
            applying = false;
        }

        private void Action_Click(object sender, RoutedEventArgs e)
        {
            var action = mode == InstallerMode.Uninstall ? LaunchAction.Uninstall :
                         mode == InstallerMode.Repair ? LaunchAction.Repair : LaunchAction.Install;
            application.Begin(action, DesktopShortcutCheckBox.IsChecked == true,
                HidHideCheckBox.IsChecked == true);
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => application.Close(1223);
        private void CloseFailure_Click(object sender, RoutedEventArgs e) => application.CloseWithCurrentResult();
        private void Retry_Click(object sender, RoutedEventArgs e) { HidePages(); DetectingPage.Visibility = Visibility.Visible; application.Retry(); }
        private void OpenLog_Click(object sender, RoutedEventArgs e) => application.OpenLog();
        private void CopyDiagnostics_Click(object sender, RoutedEventArgs e)
        {
            try { Clipboard.SetText(application.Diagnostics()); }
            catch
            {
                FailureMessage.Text += "\r\n\r\nWindows could not access the clipboard. Use Open log instead.";
            }
        }
        private void RestartLater_Click(object sender, RoutedEventArgs e) => application.Close(3010);
        private void RestartNow_Click(object sender, RoutedEventArgs e)
        {
            if (application.RestartWindows())
            {
                application.Close(3010);
            }
            else
            {
                RestartDescription.Text = "Windows could not start the restart automatically. Restart manually; setup will resume after you sign in.";
                RestartNowButton.IsEnabled = false;
            }
        }
        private void Finish_Click(object sender, RoutedEventArgs e)
        {
            if (LaunchCheckBox.Visibility == Visibility.Visible && LaunchCheckBox.IsChecked == true) application.LaunchDs4Windows();
            application.Close();
        }

        private void HidePages()
        {
            DetectingPage.Visibility = Visibility.Collapsed;
            ConfirmationPage.Visibility = Visibility.Collapsed;
            ProgressPage.Visibility = Visibility.Collapsed;
            CompletePage.Visibility = Visibility.Collapsed;
            RestartPage.Visibility = Visibility.Collapsed;
            FailurePage.Visibility = Visibility.Collapsed;
        }

        private static string PackageStatus(IReadOnlyDictionary<string, PackageState> packages, string id)
        {
            return packages.TryGetValue(id, out var state) && state == PackageState.Present ? "Installed" : "Will install";
        }

        private void ApplyWindowsTheme()
        {
            var light = true;
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    light = Convert.ToInt32(key?.GetValue("AppsUseLightTheme", 1)) != 0;
                }
            }
            catch { }

            Resources["WindowBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(light ? "#E0E6EA" : "#12181E"));
            Resources["CardBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(light ? "#FFFFFF" : "#222C35"));
            Resources["HoverBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(light ? "#D8E3EA" : "#354754"));
            Resources["BorderBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(light ? "#65727C" : "#71808C"));
            Resources["TextBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(light ? "#20272C" : "#F1F4F6"));
            Resources["MutedBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(light ? "#515E67" : "#B7C1C8"));
        }
    }
}
