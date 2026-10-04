using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using DS4Windows;

namespace DS4WinWPF.DS4Forms;

public partial class MainWindow
{
    private ReleaseNotificationService releaseNotifications;
    private readonly CancellationTokenSource releaseCheckCancellation = new();
    private bool releaseNotificationChecking;
    private bool automaticReleaseCheck;
    private CancellationTokenSource activeReleaseCheck;

    private void InitializeReleaseNotifications()
    {
        releaseNotifications = ReleaseNotificationService.Create(
            Path.Combine(Global.appdatapath, "release-check.json"));
        settingsWrapVM.CheckForUpdatesChanged += ReleaseCheckPreferenceChanged;
    }

    private async void ReleaseCheckPreferenceChanged(object sender, EventArgs e)
    {
        if (!settingsWrapVM.CheckForUpdates)
        {
            if (automaticReleaseCheck) activeReleaseCheck?.Cancel();
            return;
        }
        await CheckReleaseNotificationAsync(automatic: true);
    }

    private async Task CheckReleaseNotificationAsync(bool automatic)
    {
        if (!UpdateAuthorityPolicy.ReleaseNotificationsEnabled ||
            releaseCheckCancellation.IsCancellationRequested || releaseNotifications == null ||
            releaseNotificationChecking) return;
        releaseNotificationChecking = true;
        automaticReleaseCheck = automatic;
        using var requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(releaseCheckCancellation.Token);
        activeReleaseCheck = requestCancellation;
        checkUpdatesBtn.IsEnabled = false;
        releaseCheckStatusText.Text = "Checking GitHub…";
        try
        {
            string version = Global.exeversion;
            int interval = Global.CheckWhen;
            CancellationToken token = requestCancellation.Token;
            ReleaseCheckResult result = await Task.Run(() => releaseNotifications.CheckAsync(
                version, automatic, interval, token), token);
            if (token.IsCancellationRequested) return;
            if (automatic && !settingsWrapVM.CheckForUpdates)
            { releaseCheckStatusText.Text = ""; return; }

            switch (result.Status)
            {
                case ReleaseCheckStatus.Available:
                    releaseCheckStatusText.Text = $"{result.Tag} is available.";
                    releaseNotificationBanner.Title = $"PureDS4 {result.Tag.TrimStart('v', 'V')} is available";
                    releaseNotificationBanner.Message = "Download and installation stay manual.";
                    releaseNotificationBanner.State = StatusVisualState.Neutral;
                    releaseNotificationBanner.ShowAction = true;
                    releaseNotificationBanner.IsOpen = true;
                    break;
                case ReleaseCheckStatus.Current:
                    releaseCheckStatusText.Text = "No newer stable release is available.";
                    releaseNotificationBanner.IsOpen = false;
                    break;
                case ReleaseCheckStatus.Unavailable:
                    releaseCheckStatusText.Text = automatic ? "" : "Could not check. Try again later.";
                    break;
                case ReleaseCheckStatus.Skipped:
                    releaseCheckStatusText.Text = "";
                    break;
            }
        }
        catch (OperationCanceledException) when (requestCancellation.IsCancellationRequested)
        {
            if (!releaseCheckCancellation.IsCancellationRequested) releaseCheckStatusText.Text = "";
        }
        finally
        {
            releaseNotificationChecking = false;
            automaticReleaseCheck = false;
            activeReleaseCheck = null;
            if (!releaseCheckCancellation.IsCancellationRequested) checkUpdatesBtn.IsEnabled = true;
        }
    }

    private void ReleaseNotificationBanner_ActionRequested(object sender, EventArgs e)
    {
        try
        {
            // Hardcoded product-owned browser destination, not API-provided URLs.
            Process.Start(new ProcessStartInfo(ReleaseNotificationService.ReleasePageUri.AbsoluteUri)
            { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            releaseCheckStatusText.Text = "Could not open your browser. Visit github.com/meiameiameia/PureDS4/releases.";
        }
    }

    private void DisposeReleaseNotifications()
    {
        settingsWrapVM.CheckForUpdatesChanged -= ReleaseCheckPreferenceChanged;
        releaseCheckCancellation.Cancel();
        releaseNotifications?.Dispose();
        releaseCheckCancellation.Dispose();
    }
}
