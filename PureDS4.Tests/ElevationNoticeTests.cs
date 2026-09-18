using DS4Windows;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.ComponentModel;

namespace DS4WindowsTests;

/// <summary>
/// Running without administrator rights leaves PureDS4 able to read the
/// controller but unable to hide it, so no game output is created. The window
/// has to say that, and offer the restart that fixes it.
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
    public void UnelevatedProcessExplainsTheConsequenceAndOffersARestart()
    {
        ElevationNotice notice = ElevationNotice.Evaluate(isElevated: false);

        Assert.IsNotNull(notice);
        StringAssert.Contains(notice.Title, ProductIdentity.Name);
        StringAssert.Contains(notice.Title, "administrator");
        // The point of the notice is the consequence, not the permission.
        StringAssert.Contains(notice.Message, "game output");
        StringAssert.Contains(notice.Message, "hidden from games");
        Assert.AreEqual(ElevationNotice.RestartActionLabel, notice.ActionLabel);
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
