/*
DS4Windows
Copyright (C) 2026 hbashton

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.
*/

using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows;

namespace DS4WinWPF.DS4Forms
{
    public partial class WelcomeDialog : Window
    {
        private const string HidHideInstallerFileName =
            "HidHide_1.5.230_x64.exe";
        private const string HidHideInstallerSha256 =
            "F4BBBCB82E6258641B887C74BC81C4C5F66E4AA811808DFC304347687B7605F6";

        public WelcomeDialog(bool loadConfig = false)
        {
            if (loadConfig)
            {
                DS4Windows.Global.FindConfigLocation();
                DS4Windows.Global.Load();
            }

            InitializeComponent();
            step4HidHidePanel.IsEnabled = IsHidHideCompatible();

            DS4Windows.ViiperPrerequisiteStatus status =
                DS4Windows.ViiperSetupManager.GetStatus(tryStartServer: true);
            if (status.Ready)
            {
                viiperInstallBtn.Content = "Game output is ready";
            }
        }

        private void ViiperInstallBtn_Click(object sender, RoutedEventArgs e)
        {
            DS4Windows.ViiperPrerequisiteStatus status =
                DS4Windows.ViiperSetupManager.GetStatus(tryStartServer: true);
            if (status.Ready)
            {
                viiperInstallBtn.Content = "Game output is ready";
                return;
            }

            bool launched = DS4Windows.ViiperSetupManager.
                EnsureReadyWithPrompt(this, forcePrompt: true);
            viiperInstallBtn.Content = launched
                ? "Setup opened — finish it, then click here to verify"
                : "Game output setup needs attention";
        }

        private async void HidHideInstall_Click(object sender, RoutedEventArgs e)
        {
            await RunBundledInstallerAsync(hidHideInstallBtn, "HidHide",
                "Controller protection",
                HidHideInstallerFileName,
                HidHideInstallerSha256);
        }

        private async Task RunBundledInstallerAsync(
            System.Windows.Controls.Button button, string componentName,
            string displayName,
            string bundledFileName, string expectedSha256)
        {
            string target = Path.Combine(AppContext.BaseDirectory, "extras",
                bundledFileName);
            try
            {
                SetInstallerControlsEnabled(false);

                if (!File.Exists(target))
                {
                    throw new FileNotFoundException(
                        $"The offline PureDS4 package is incomplete: " +
                        $"{bundledFileName} is missing.", target);
                }

                button.Content = $"Verifying {displayName.ToLowerInvariant()}…";
                if (!await InstallerMatchesSha256Async(target,
                    expectedSha256))
                {
                    throw new InvalidDataException(
                        $"The bundled {componentName} installer failed its " +
                        "SHA-256 integrity check.");
                }

                button.Content = $"Installing {displayName.ToLowerInvariant()}…";
                using Process process = Process.Start(new ProcessStartInfo
                {
                    FileName = target,
                    UseShellExecute = true,
                    Verb = "runas",
                }) ?? throw new InvalidOperationException(
                    $"Windows did not start the {componentName} installer.");
                await process.WaitForExitAsync();

                const int RestartRequiredExitCode = 3010;
                bool restartRequired = process.ExitCode ==
                    RestartRequiredExitCode;
                if (process.ExitCode != 0 && !restartRequired)
                {
                    throw new InvalidOperationException(
                        $"The {componentName} installer exited with code {process.ExitCode}.");
                }

                if (componentName == "HidHide")
                {
                    DS4Windows.Global.RefreshHidHideInfo();
                }
                button.Content = restartRequired ?
                    $"{displayName} setup complete — restart required" :
                    $"{displayName} setup complete";
                if (restartRequired)
                {
                    MessageBox.Show(this,
                        $"{displayName} was installed successfully. Restart Windows to finish setup.",
                        $"{displayName} setup", MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                button.Content = $"{displayName} setup failed";
                MessageBox.Show(this,
                    $"Could not install {displayName.ToLowerInvariant()}: {ex.Message}",
                    $"{displayName} setup", MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                SetInstallerControlsEnabled(true);
            }
        }

        private static async Task<bool> InstallerMatchesSha256Async(
            string path, string expectedSha256)
        {
            if (string.IsNullOrWhiteSpace(expectedSha256)) return true;

            using FileStream stream = new FileStream(path, FileMode.Open,
                FileAccess.Read, FileShare.Read, bufferSize: 81920,
                useAsync: true);
            using SHA256 sha256 = SHA256.Create();
            byte[] hash = await sha256.ComputeHashAsync(stream);
            return string.Equals(Convert.ToHexString(hash), expectedSha256,
                StringComparison.OrdinalIgnoreCase);
        }

        private void SetInstallerControlsEnabled(bool enabled)
        {
            viiperInstallBtn.IsEnabled = enabled;
            step4HidHidePanel.IsEnabled = enabled && IsHidHideCompatible();
        }

        private static bool IsHidHideCompatible() =>
            DS4Windows.Global.IsWin10OrGreater() &&
            Environment.Is64BitOperatingSystem;

        private void Step2Btn_Click(object sender, RoutedEventArgs e) =>
            DS4Windows.Util.StartProcessHelper(
                "https://support.xbox.com/help/hardware-network/controller/connect-xbox-wireless-controller-to-pc");

        private void BluetoothSetLink_Click(object sender,
            RoutedEventArgs e) => Process.Start("control", "bthprops.cpl");

        private void FinishedBtn_Click(object sender, RoutedEventArgs e) =>
            Close();
    }

    public class WelcomeDialogResourcePaths
    {
        public string PairmodePNG =>
            $"{DS4Windows.Global.RESOURCES_PREFIX}/Pairmode.png";
    }
}
