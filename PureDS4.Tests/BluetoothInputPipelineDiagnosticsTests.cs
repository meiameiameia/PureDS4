using System.Globalization;
using DS4Windows;

namespace DS4WindowsTests;

[TestClass]
public class BluetoothInputPipelineDiagnosticsTests
{
    [TestMethod]
    public void ReadTimeoutRetainsContextWithoutBlamingVirtualOutput()
    {
        var diagnostics = new BluetoothInputPipelineDiagnostics();
        Assert.IsNull(diagnostics.Record(BluetoothInputStage.Dispatch, 1, 0.4));
        Assert.IsNull(diagnostics.Record(BluetoothInputStage.Feedback, 2, 1.2));
        var incident = diagnostics.Record(BluetoothInputStage.Read, 3002, 3000,
            HidDevice.ReadStatus.WaitTimedOut, 995).Value;
        Assert.AreEqual(BluetoothInputStage.Read, incident.Stage);
        Assert.AreEqual(3000d, incident.ReadMs);
        Assert.AreEqual(0.4d, incident.DispatchMs);
        Assert.AreEqual(1.2d, incident.FeedbackMs);
        Assert.AreEqual(1L, incident.ReadTimeouts);
        Assert.AreEqual(0L, incident.ReadErrors);
        Assert.AreEqual(995, incident.NativeError);
    }

    [DataTestMethod]
    [DataRow((int)BluetoothInputStage.Processing)]
    [DataRow((int)BluetoothInputStage.Dispatch)]
    [DataRow((int)BluetoothInputStage.Feedback)]
    public void FirstBlockedStageIsVisibleEvenAfterRecentReadTimeout(
        int stageValue)
    {
        var stage = (BluetoothInputStage)stageValue;
        var diagnostics = new BluetoothInputPipelineDiagnostics();
        diagnostics.Record(BluetoothInputStage.Read, 3000, 3000,
            HidDevice.ReadStatus.WaitTimedOut, 995);
        diagnostics.Record(BluetoothInputStage.Read, 3001, 0.5);
        var incident = diagnostics.Record(stage, 3201, 200).Value;
        Assert.AreEqual(stage, incident.Stage);
        Assert.AreEqual(0.5d, incident.ReadMs);
        Assert.AreEqual(HidDevice.ReadStatus.Success, incident.ReadStatus);
        Assert.AreEqual(0, incident.NativeError);
        Assert.AreEqual(2L, incident.SlowStages);
    }

    [TestMethod]
    public void RepeatedIncidentsAreBoundedButCountsDoNotExpireWithHealthyInput()
    {
        var diagnostics = new BluetoothInputPipelineDiagnostics();
        Assert.IsNotNull(diagnostics.Record(BluetoothInputStage.Read, 3000,
            3000, HidDevice.ReadStatus.WaitTimedOut, 995));
        Assert.IsNull(diagnostics.Record(BluetoothInputStage.Read, 6000,
            3000, HidDevice.ReadStatus.WaitTimedOut, 995));
        for (int i = 6001; i < 33000; i++)
            Assert.IsNull(diagnostics.Record(BluetoothInputStage.Read, i, 1));
        var incident = diagnostics.Record(BluetoothInputStage.Read, 33000,
            0.1, HidDevice.ReadStatus.ReadError, 1167).Value;
        Assert.AreEqual(2L, incident.ReadTimeouts);
        Assert.AreEqual(1L, incident.ReadErrors);
        Assert.AreEqual(2L, incident.SlowStages);
        Assert.AreEqual(1167, incident.NativeError);
    }

    [TestMethod]
    public void HealthyStagesDoNotAllocateOrProducePerPacketLogs()
    {
        var diagnostics = new BluetoothInputPipelineDiagnostics();
        for (int i = 0; i < 1000; i++)
            diagnostics.Record(BluetoothInputStage.Read, i, 1);
        long before = GC.GetAllocatedBytesForCurrentThread();
        bool reported = false;
        for (int i = 0; i < 10000; i++)
        {
            reported |= diagnostics.Record(BluetoothInputStage.Read, i, 1).HasValue;
            reported |= diagnostics.Record(BluetoothInputStage.Processing, i, 0.1).HasValue;
            reported |= diagnostics.Record(BluetoothInputStage.Dispatch, i, 0.2).HasValue;
            reported |= diagnostics.Record(BluetoothInputStage.Feedback, i, 0.3).HasValue;
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.IsFalse(reported);
        Assert.AreEqual(0L, allocated);
    }

    [DataTestMethod]
    [DataRow(-1d)]
    [DataRow(double.NaN)]
    [DataRow(double.PositiveInfinity)]
    public void InvalidMeasurementsCannotCreateAnIncident(double duration)
    {
        Assert.IsNull(new BluetoothInputPipelineDiagnostics().Record(
            BluetoothInputStage.Read, 1000, duration));
    }

    [TestMethod]
    public void DiagnosticTextUsesStableNumbersAndNoControllerIdentifiers()
    {
        var diagnostics = new BluetoothInputPipelineDiagnostics();
        var incident = diagnostics.Record(BluetoothInputStage.Dispatch, 300,
            123.5).Value;
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pt-BR");
            StringAssert.Contains(incident.ToString(), "dispatchMs=123.50");
            StringAssert.Contains(incident.ToString(), "stage=Dispatch");
            Assert.IsFalse(incident.ToString().Contains("MAC", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
