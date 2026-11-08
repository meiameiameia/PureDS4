using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using DS4Windows;

namespace DS4WindowsTests;

[TestClass]
public class PipelineIncidentMonitorTests
{
    private static long Ticks(long ms) => ms * Stopwatch.Frequency / 1000;

    [DataTestMethod]
    [DataRow((int)PipelineStage.Read)]
    [DataRow((int)PipelineStage.Processing)]
    [DataRow((int)PipelineStage.Dispatch)]
    [DataRow((int)PipelineStage.Feedback)]
    [DataRow((int)PipelineStage.PacketHandling)]
    [DataRow((int)PipelineStage.Callbacks)]
    public void InFlightStageIsReportedBeforeItCompletesThenRecoversOnce(int value)
    {
        var activity = new PipelineActivity();
        var events = new List<PipelineIncidentMonitor.Incident>();
        using var monitor = new PipelineIncidentMonitor(activity.Snapshot, events.Add);
        var stage = (PipelineStage)value;
        long started = activity.Begin(stage);
        monitor.Sample(started + Ticks(999));
        Assert.AreEqual(0, events.Count);
        monitor.Sample(started + Ticks(1000));
        monitor.Sample(started + Ticks(2000));
        Assert.AreEqual(1, events.Count, "A still-blocked stage must not spam the log.");
        Assert.AreEqual(stage, events[0].Stage);
        Assert.IsFalse(events[0].Recovered);
        activity.End(stage, started);
        monitor.Sample(started + Ticks(2250));
        monitor.Sample(started + Ticks(2500));
        Assert.AreEqual(2, events.Count);
        Assert.IsTrue(events[1].Recovered);
        Assert.AreEqual(events[0].Session, events[1].Session);
    }

    [TestMethod]
    public void PoolObserverWorksWhileProducerIsBlocked()
    {
        var activity = new PipelineActivity();
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var observed = new ManualResetEventSlim();
        var worker = new Thread(() =>
        {
            using var scope = activity.Watch(PipelineStage.Dispatch);
            entered.Set();
            release.Wait();
        }) { IsBackground = true };
        using var monitor = new PipelineIncidentMonitor(activity.Snapshot, incident =>
        {
            if (!incident.Recovered && incident.Stage == PipelineStage.Dispatch)
                observed.Set();
        });
        worker.Start();
        try
        {
            Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(5)));
            monitor.Start();
            Assert.IsTrue(observed.Wait(TimeSpan.FromSeconds(5)),
                "No producer completion or WPF Dispatcher pump should be required.");
        }
        finally
        {
            monitor.Dispose();
            release.Set();
            Assert.IsTrue(worker.Join(TimeSpan.FromSeconds(5)));
        }
    }

    [TestMethod]
    public void HealthyStageSamplingAndLongIdleProduceNoIncidentsOrAllocations()
    {
        var activity = new PipelineActivity();
        int incidents = 0;
        using var monitor = new PipelineIncidentMonitor(activity.Snapshot, _ => incidents++);
        for (int i = 0; i < 1000; i++)
        {
            long started = activity.Begin(PipelineStage.Read);
            monitor.Sample(started);
            activity.End(PipelineStage.Read, started);
        }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 14_400; i++)
        {
            long started = activity.Begin(PipelineStage.Read);
            monitor.Sample(started + Ticks(1));
            activity.End(PipelineStage.Read, started);
            monitor.Sample(started + Ticks(250));
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.AreEqual(0, incidents);
        Assert.AreEqual(0L, allocated);
        // A large idle clock jump, not physical-controller validation.
        monitor.Sample(Ticks(3_600_000));
        Assert.AreEqual(0, incidents);
    }

    [TestMethod]
    public void RepeatedStageIncidentsAreRateLimitedButOtherStagesRemainVisible()
    {
        PipelineProgressSnapshot? current = new(PipelineStage.Read, 1, Ticks(1));
        var events = new List<PipelineIncidentMonitor.Incident>();
        using var monitor = new PipelineIncidentMonitor(() => current, events.Add);
        monitor.Sample(Ticks(1001));
        current = new(PipelineStage.Read, 2, Ticks(1002));
        monitor.Sample(Ticks(2002));
        Assert.AreEqual(2, events.Count, "Only the first stall and its recovery are logged.");
        current = new(PipelineStage.Feedback, 3, Ticks(2003));
        monitor.Sample(Ticks(3003));
        Assert.AreEqual(3, events.Count);
        current = new(PipelineStage.Read, 4, Ticks(31_002));
        monitor.Sample(Ticks(32_002));
        Assert.AreEqual(5, events.Count);
    }

    [TestMethod]
    public void RacedSnapshotsDoNotPretendToRecoverAndDisposalSuppressesLateWork()
    {
        PipelineProgressSnapshot? current = new(PipelineStage.Dispatch, 1, Ticks(1));
        var events = new List<PipelineIncidentMonitor.Incident>();
        using var monitor = new PipelineIncidentMonitor(() => current, events.Add);
        monitor.Sample(Ticks(1001));
        current = null;
        monitor.Sample(Ticks(2001));
        Assert.AreEqual(1, events.Count);
        monitor.Dispose();
        current = new(PipelineStage.Dispatch, 0);
        monitor.Sample(Ticks(3001));
        monitor.Start();
        Assert.AreEqual(1, events.Count);
    }

    [TestMethod]
    public void ThrowingLoggerCannotRepeatRecoveryOrEscapeObserver()
    {
        PipelineProgressSnapshot? current = new(PipelineStage.Read, 1, Ticks(1));
        int calls = 0;
        using var monitor = new PipelineIncidentMonitor(() => current, _ =>
        {
            calls++;
            throw new IOException("fixture logger failure");
        });
        monitor.Sample(Ticks(1001));
        current = new(PipelineStage.Read, 0);
        monitor.Sample(Ticks(2001));
        monitor.Sample(Ticks(3001));
        Assert.AreEqual(2, calls);
    }

    [TestMethod]
    public void EndingAnOldScopeCannotClearTheNextActivity()
    {
        var activity = new PipelineActivity();
        long old = activity.Begin(PipelineStage.Read);
        long current = activity.Begin(PipelineStage.Dispatch);
        activity.End(PipelineStage.Read, old);
        Assert.AreEqual(PipelineStage.Dispatch, activity.Snapshot().Value.Stage);
        activity.End(PipelineStage.Dispatch, current);
        Assert.AreEqual(0L, activity.Snapshot().Value.Identity);
    }

    [TestMethod]
    public void DiagnosticTextContainsOnlyControlledFieldsAndInvariantNumbers()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pt-BR");
            var incident = new PipelineIncidentMonitor.Incident(7,
                PipelineStage.Feedback, false, 1234.5);
            StringAssert.Contains(incident.ToString(), "session=7 stage=Feedback event=in-flight observedMs=1234");
            Assert.IsFalse(incident.ToString().Contains("MAC", StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(incident.ToString().Contains('\\'));
        }
        finally { CultureInfo.CurrentCulture = original; }
    }

    [TestMethod]
    public void ViiperCoalescingAndSuccessfulWritesDoNotLookLikeAStall()
    {
        var device = CreateUnconnectedFixture();
        var activity = new PipelineActivity();
        Set(device, "pendingStatePacket", new byte[1]);
        Set(device, "submittedPacketCount", 100_000L);
        Set(device, "replacedPendingPacketCount", 90_000L);
        int incidents = 0;
        using var monitor = new PipelineIncidentMonitor(() =>
            device.GetStateWriterProgress(1, activity), _ => incidents++);
        for (int i = 0; i < 14_400; i++)
        {
            Set(device, "writtenPacketCount", (long)i);
            monitor.Sample(Ticks(1 + i * 250));
        }
        Set(device, "pendingStatePacket", null);
        monitor.Sample(Ticks(3_700_000));
        Assert.AreEqual(0, incidents);
    }

    [TestMethod]
    public void ViiperQueuedOrInFlightWorkWithoutProgressIsVisibleWithoutVerboseLogging()
    {
        var device = CreateUnconnectedFixture();
        var activity = new PipelineActivity();
        var events = new List<PipelineIncidentMonitor.Incident>();
        using var monitor = new PipelineIncidentMonitor(() =>
            device.GetStateWriterProgress(1, activity), events.Add);
        Set(device, "pendingStatePacket", new byte[1]);
        monitor.Sample(Ticks(1));
        // New submissions do not hide a writer which has made no progress.
        Set(device, "submittedPacketCount", 1000L);
        monitor.Sample(Ticks(1001));
        monitor.Sample(Ticks(2001));
        Assert.AreEqual(1, events.Count);
        Set(device, "pendingStatePacket", null);
        Set(device, "writtenPacketCount", 1L);
        monitor.Sample(Ticks(2251));
        Assert.AreEqual(2, events.Count);
        using var inFlight = activity.Watch(PipelineStage.ViiperWriter);
        monitor.Sample(Ticks(33_001));
        monitor.Sample(Ticks(34_001));
        Assert.AreEqual(3, events.Count, "A dequeued but blocked write must also be visible.");
    }

    [TestMethod]
    public void ViiperStopNativeIdleAndRetiredGenerationsAreNotStalls()
    {
        var device = CreateUnconnectedFixture();
        var oldActivity = new PipelineActivity();
        Assert.AreEqual(0L, device.GetStateWriterProgress(1, oldActivity).Value.Identity);
        Set(device, "pendingStatePacket", new byte[1]);
        Set(device, "writerStopRequested", true);
        Assert.AreEqual(0L, device.GetStateWriterProgress(1, oldActivity).Value.Identity);
        Set(device, "writerStopRequested", false);
        Set(device, "stateWriterGeneration", 2L);
        using var stale = oldActivity.Watch(PipelineStage.ViiperWriter);
        Assert.AreEqual(0L, device.GetStateWriterProgress(1, oldActivity).Value.Identity);
        var nextActivity = new PipelineActivity();
        Set(device, "pendingStatePacket", null);
        Assert.AreEqual(0L, device.GetStateWriterProgress(2, nextActivity).Value.Identity);
    }

    [TestMethod]
    public void ViiperSnapshotDoesNotTakeTheQueueLock()
    {
        var device = CreateUnconnectedFixture();
        var activity = new PipelineActivity();
        var gate = new object();
        Set(device, "pendingPacketLock", gate);
        Set(device, "pendingStatePacket", new byte[1]);
        using var done = new ManualResetEventSlim();
        var worker = new Thread(() =>
        {
            device.GetStateWriterProgress(1, activity);
            done.Set();
        }) { IsBackground = true };
        try
        {
            lock (gate)
            {
                worker.Start();
                Assert.IsTrue(done.Wait(TimeSpan.FromSeconds(5)),
                    "A stalled output lock must not stall its diagnostic observer.");
            }
        }
        finally { Assert.IsTrue(worker.Join(TimeSpan.FromSeconds(5))); }
    }

    private static ViiperOutDevice CreateUnconnectedFixture()
    {
        // No sockets, services, drivers, tasks or hardware are opened.
        var device = (ViiperOutDevice)RuntimeHelpers.GetUninitializedObject(typeof(ViiperOutDevice));
        typeof(OutputDevice).GetField("connected", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(device, true);
        Set(device, "stateWriterGeneration", 1L);
        return device;
    }

    private static void Set(ViiperOutDevice device, string field, object value) =>
        typeof(ViiperOutDevice).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(device, value);
}
