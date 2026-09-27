using DS4Windows;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.ComponentModel;
using DS4WinWPF;

namespace DS4WindowsTests;

/// <summary>
/// Elevation alone does not establish whether physical containment or virtual
/// output succeeded. The notice must distinguish process limitations from
/// the selected controller's actual status.
/// </summary>
[TestClass]
public class ElevationNoticeTests
{
    [TestMethod]
    public void ElevatedProcessShowsNoNotice()
    {
        Assert.IsNull(ElevationNotice.Evaluate(isElevated: true));
    }

    [TestMethod]
    public void UnelevatedProcessDoesNotClaimGameOutputFailed()
    {
        ElevationNotice notice = ElevationNotice.Evaluate(isElevated: false);

        Assert.IsNotNull(notice);
        StringAssert.Contains(notice.Title, "administrator");
        // Output may be working without elevation; the banner cannot decide.
        StringAssert.Contains(notice.Message, "Game output");
        StringAssert.Contains(notice.Message, "may still work");
        StringAssert.Contains(notice.Message, "elevated game");
        Assert.IsFalse(notice.Message.Contains("no game output",
            StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual(ElevationNotice.RestartActionLabel, notice.ActionLabel);
    }

    [DataTestMethod]
    [DataRow(false, true, VirtualOutputBlockReason.None, true)]
    [DataRow(false, false,
        VirtualOutputBlockReason.PhysicalContainmentUnavailable, false)]
    [DataRow(true, true, VirtualOutputBlockReason.None, true)]
    [DataRow(true, false,
        VirtualOutputBlockReason.OutputBindingFailed, false)]
    public void ElevationDoesNotDecideControllerOutputStatus(
        bool elevated, bool backendConnected,
        VirtualOutputBlockReason blockReason, bool expectedReady)
    {
        ElevationNotice notice = ElevationNotice.Evaluate(elevated);
        ControllerRuntimeSignals signals = new(true, true, true, true,
            backendConnected, backendConnected,
            ControllerRuntimeLaneState.NotRequired,
            ControllerRuntimeLaneState.NotRequired,
            ControllerRuntimeLaneState.NotRequired, "Xbox 360",
            virtualOutputBlockReason: blockReason);

        ControllerStartupStatus status =
            ControllerRuntimeStatusPolicy.Evaluate(signals);

        Assert.AreEqual(!elevated, notice != null);
        Assert.AreEqual(expectedReady, status.IsReady);
        if (!backendConnected)
        {
            Assert.IsTrue(status.NeedsAttention);
        }
    }

    [TestMethod]
    public void NoticeNeverNamesTheDriverItDependsOn()
    {
        // Normal UI keeps HidHide in technical details; see the visual contract.
        ElevationNotice notice = ElevationNotice.Evaluate(isElevated: false);

        Assert.IsFalse(notice.Title.Contains("HidHide", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(notice.Message.Contains("HidHide", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void RestartReportsStartedWhenWindowsLaunchesTheElevatedProcess()
    {
        string launchedPath = null;
        string launchedArguments = null;

        ElevationRelaunchResult result = ElevationRelaunch.Restart(
            @"C:\Program Files\PureDS4\PureDS4.exe", string.Empty,
            (path, arguments) => { launchedPath = path; launchedArguments = arguments; });

        Assert.AreEqual(ElevationRelaunchResult.Started, result);
        Assert.AreEqual(@"C:\Program Files\PureDS4\PureDS4.exe", launchedPath);
        Assert.AreEqual(string.Empty, launchedArguments);
    }

    [TestMethod]
    public void RelaunchArgumentsWaitForTheOldInstanceAndPreserveStorage()
    {
        Assert.AreEqual("-wait-for-process 42 -storage portable",
            ElevationRelaunch.BuildArguments(42,
                RelaunchStorageLocation.Portable));
        Assert.AreEqual("-wait-for-process 42 -storage appdata",
            ElevationRelaunch.BuildArguments(42,
                RelaunchStorageLocation.WindowsAccount));
        Assert.IsNull(ElevationRelaunch.BuildArguments(0,
            RelaunchStorageLocation.Portable));
        Assert.IsNull(ElevationRelaunch.BuildArguments(42,
            RelaunchStorageLocation.Unspecified));
    }

    [TestMethod]
    public void RelaunchParserAcceptsOnlyKnownStorageAndPositiveProcessId()
    {
        ArgumentParser parser = new ArgumentParser();
        parser.Parse(new[]
        {
            "-wait-for-process", "42", "-storage", "portable",
        });

        Assert.IsFalse(parser.HasErrors);
        Assert.AreEqual(42, parser.RelaunchPredecessorProcessId);
        Assert.AreEqual(RelaunchStorageLocation.Portable,
            parser.RelaunchStorageLocation);

        parser = new ArgumentParser();
        parser.Parse(new[]
        {
            "-wait-for-process", "0", "-storage", @"C:\profiles",
        });
        Assert.IsTrue(parser.HasErrors);
        Assert.AreEqual(RelaunchStorageLocation.Unspecified,
            parser.RelaunchStorageLocation);

        parser = new ArgumentParser();
        parser.Parse(new[] { "-storage", "appdata" });
        Assert.IsTrue(parser.HasErrors);
    }

    [TestMethod]
    public void RelaunchWaitUsesTheExactPredecessorAndBoundedTimeout()
    {
        int observedProcessId = 0;
        int observedTimeout = 0;

        bool ready = ElevationRelaunch.WaitForPredecessor(42, 30000,
            (processId, timeout) =>
            {
                observedProcessId = processId;
                observedTimeout = timeout;
                return true;
            });

        Assert.IsTrue(ready);
        Assert.AreEqual(42, observedProcessId);
        Assert.AreEqual(30000, observedTimeout);
        Assert.IsFalse(ElevationRelaunch.WaitForPredecessor(0, 30000,
            (processId, timeout) => true));
    }

    [TestMethod]
    public void RelaunchStorageSelectionUsesOnlyAnExistingKnownStore()
    {
        Assert.AreEqual(RelaunchStorageLocation.Portable,
            ElevationRelaunch.DetectStorageLocation(@"C:\Portable\.",
                @"C:\Portable", @"C:\Account"));

        bool resolved = ElevationRelaunch.TryResolveStoragePath(
            RelaunchStorageLocation.WindowsAccount, @"C:\Portable",
            @"C:\Account", path => path == @"C:\Account\Auto Profiles.xml",
            out string selectedPath);
        Assert.IsTrue(resolved);
        Assert.AreEqual(@"C:\Account", selectedPath);

        Assert.IsFalse(ElevationRelaunch.TryResolveStoragePath(
            RelaunchStorageLocation.Portable, @"C:\Portable",
            @"C:\Account", path => false, out selectedPath));
        Assert.IsNull(selectedPath);
    }

    [TestMethod]
    public void DismissedElevationPromptIsNotTreatedAsAFailure()
    {
        ElevationRelaunchResult result = ElevationRelaunch.Restart(
            @"C:\Program Files\PureDS4\PureDS4.exe", null,
            (path, arguments) => throw new Win32Exception(ElevationRelaunch.ErrorCancelled));

        Assert.AreEqual(ElevationRelaunchResult.Cancelled, result);
    }

    [TestMethod]
    public void OtherLaunchFailuresAreReportedAsFailures()
    {
        ElevationRelaunchResult result = ElevationRelaunch.Restart(
            @"C:\Program Files\PureDS4\PureDS4.exe", null,
            (path, arguments) => throw new Win32Exception(2));

        Assert.AreEqual(ElevationRelaunchResult.Failed, result);
    }

    [TestMethod]
    public void MissingExecutablePathOrLauncherCannotStartAnything()
    {
        Assert.AreEqual(ElevationRelaunchResult.Failed,
            ElevationRelaunch.Restart(null, null, (path, arguments) => { }));
        Assert.AreEqual(ElevationRelaunchResult.Failed,
            ElevationRelaunch.Restart(" ", null, (path, arguments) => { }));
        Assert.AreEqual(ElevationRelaunchResult.Failed,
            ElevationRelaunch.Restart(@"C:\Program Files\PureDS4\PureDS4.exe", null, null));
    }
}
