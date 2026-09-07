using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using DS4Windows;
using DS4WinWPF.DS4Control;

namespace DS4WindowsTests;

[TestClass]
public class BluetoothOutputKeepAliveTests
{
    [DataTestMethod]
    [DataRow(0, 0)]
    [DataRow(60, 120)]
    public void IdenticalBluetoothFeedbackDoesNotRepeatPhysicalWrites(int fast, int slow)
    {
        var device = CreateDevice();
        device.setRumble((byte)fast, (byte)slow);
        Send(device, force: true);
        for (int i = 0; i < 1000; i++)
        {
            device.setRumble((byte)fast, (byte)slow);
            Send(device);
        }
        Assert.AreEqual(1, device.Writes);
        Assert.AreEqual(1001L, typeof(DS4Device).GetField("rumbleCommandGeneration",
            BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(device),
            "Deduplication must not change command ordering/acknowledgement.");
    }

    [TestMethod]
    public void RepeatedPublicationPreservesPendingChangeAndExplicitStop()
    {
        var device = CreateDevice();
        Send(device, force: true);
        device.setRumble(60, 120);
        device.setRumble(60, 120);
        Send(device);
        Assert.AreEqual(2, device.Writes);
        Assert.AreEqual((byte)60, device.LastReport[6]);
        Assert.AreEqual((byte)120, device.LastReport[7]);
        device.setRumble(0, 0);
        device.setRumble(0, 0);
        Send(device);
        Assert.AreEqual(3, device.Writes);
        Assert.AreEqual((byte)0, device.LastReport[6]);
        Assert.AreEqual((byte)0, device.LastReport[7]);
    }

    [TestMethod]
    public void NeutralFeedbackDoesNotErasePreviewOrOtherProducerDirtyLatch()
    {
        var device = CreateDevice();
        device.SetRumblePreview(true, 211, false, 0);
        Send(device);
        device.setRumble(0, 0);
        Send(device);
        Assert.AreEqual(1, device.Writes);
        Assert.AreEqual((byte)211, device.LastReport[6]);
        var field = typeof(DS4Device).GetField("currentHap",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        var state = (DS4HapticState)field.GetValue(device)!;
        state.dirty = true; // e.g. another producer requesting an effect refresh
        field.SetValue(device, state);
        device.setRumble(0, 0);
        Send(device);
        Assert.AreEqual(2, device.Writes);
        device.ClearRumblePreview();
        device.setRumble(0, 0);
        Send(device);
        Assert.AreEqual(3, device.Writes);
        Assert.AreEqual((byte)0, device.LastReport[6]);
    }

    [TestMethod]
    public void FailedChangedBluetoothReportRemainsPendingWithoutNewFeedback()
    {
        var device = CreateDevice();
        Send(device, force: true);
        device.WriteSucceeds = false;
        device.setRumble(20, 30);
        Send(device);
        device.WriteSucceeds = true;
        Send(device);
        Assert.AreEqual(3, device.Writes);
        Assert.AreEqual((byte)20, device.LastReport[6]);
        Send(device);
        Assert.AreEqual(3, device.Writes);
    }

    [TestMethod]
    public void WriteExceptionRetainsPendingBluetoothReport()
    {
        var device = CreateDevice();
        device.ThrowOnWrite = true;
        Send(device, force: true);
        device.ThrowOnWrite = false;
        Send(device);
        Assert.AreEqual(2, device.Writes);
    }

    [TestMethod]
    public void UsbRepeatedRumbleKeepsExistingPublicationBehavior()
    {
        var device = CreateDevice(ConnectionType.USB);
        device.setRumble(60, 120);
        Send(device);
        device.setRumble(60, 120);
        Send(device);
        Assert.AreEqual(2, device.Writes);
    }

    [TestMethod]
    public void DormantDs3RumbleMailboxRetainsItsDirtyPublication()
    {
        var device = CreateDevice();
        Set(device, "deviceType", DS4Windows.InputDevices.InputDeviceType.DS3);
        device.setRumble(60, 120);
        Send(device);
        device.setRumble(60, 120);
        Send(device);
        Assert.AreEqual(2, device.Writes,
            "Do not change the mailbox contract consumed by the dormant DS3 writer.");
    }

    [TestMethod]
    public void AutomaticRumbleStopStillPublishesZero()
    {
        var device = CreateDevice();
        Set(device, "rumbleAutostopTime", 10);
        device.setRumble(60, 120);
        Send(device);
        Thread.Sleep(20);
        // The existing scheduler publishes the timeout's neutral mailbox
        // after composing this pass; the following pass consumes that stop.
        Send(device);
        Send(device);
        Assert.AreEqual((byte)0, device.LastReport[6]);
        Assert.AreEqual((byte)0, device.LastReport[7]);
    }

    [TestMethod]
    public void LightbarChangeIsSentAlongsideRepeatedRumble()
    {
        var device = CreateDevice();
        device.setRumble(60, 120);
        Send(device);
        device.LightBarColor = new DS4Color(12, 34, 56);
        device.setRumble(60, 120);
        Send(device);
        Assert.AreEqual(2, device.Writes);
        CollectionAssert.AreEqual(new byte[] { 60, 120, 12, 34, 56 },
            device.LastReport[6..11]);
    }

    [TestMethod]
    public void ChangedFeedbackMustLeaveBluetoothKeepAliveRunning()
    {
        var device = CreateDevice();
        device.setRumble(100, 150);
        Send(device);

        Assert.AreEqual(1, device.Writes);
        Assert.IsTrue(device.Timer.IsRunning,
            "Sending changed feedback must not stop the clock needed when feedback becomes quiet.");

        // A game moving to the background commonly publishes one neutral
        // report, then no more feedback. That final change must also rearm.
        device.setRumble(0, 0);
        Send(device);
        Assert.AreEqual(2, device.Writes);
        Assert.IsTrue(device.Timer.IsRunning);
        Send(device);
        Assert.AreEqual(2, device.Writes, "No redundant effect before the keepalive deadline.");
    }

    [TestMethod]
    public void QuietBluetoothStillWritesPeriodicKeepAlive()
    {
        var device = CreateDevice();
        device.setRumble(100, 150);
        Send(device);
        device.setRumble(0, 0);
        Send(device);
        Assert.IsTrue(device.Timer.IsRunning);
        Assert.IsTrue(SpinWait.SpinUntil(
            () => device.Timer.ElapsedMilliseconds >= 4000, TimeSpan.FromSeconds(10)));

        device.setRumble(0, 0); // Repeated neutral feedback must not postpone keepalive.
        Send(device);
        Assert.AreEqual(3, device.Writes,
            "Input-loop output passes must send a keepalive even without a game feedback change.");
        Assert.IsTrue(device.Timer.IsRunning);
        Send(device);
        Assert.AreEqual(3, device.Writes);
    }

    [TestMethod]
    public void SpeakerStreamSuppressesPeriodicEffectsAndDefersChangedEffects()
    {
        var device = CreateDevice();
        AudioState(device).Update(true, false, null, null, null, null);
        int audioWrites = 0;
        Assert.IsTrue(device.RegisterDualShock4BluetoothAudioControlLane(
            new object(), report => { audioWrites++; return true; }));
        Send(device, force: true);
        Assert.AreEqual(1, audioWrites);
        Assert.AreEqual(0, device.Writes, "Speaker effects must use the owned audio lane.");
        Assert.IsTrue(device.Timer.IsRunning);

        Assert.IsTrue(SpinWait.SpinUntil(
            () => device.Timer.ElapsedMilliseconds >= 4000, TimeSpan.FromSeconds(10)));
        Send(device);
        Assert.AreEqual(1, audioWrites,
            "Speaker packets already maintain the connection; do not add idle effects.");

        // A just-written audio effect defers a new effect until the existing
        // rate limiter allows it; a deferred attempt must not rearm the clock.
        device.Timer.Stop();
        Set(device, "lastBluetoothEffectReportDuringAudioTick", long.MaxValue);
        device.setRumble(50, 60);
        Send(device);
        Assert.AreEqual(1, audioWrites);
        Assert.IsFalse(device.Timer.IsRunning);

        Set(device, "lastBluetoothEffectReportDuringAudioTick", 0L);
        Send(device);
        Assert.AreEqual(2, audioWrites);
        Assert.IsTrue(device.Timer.IsRunning);
    }

    [TestMethod]
    public void MicrophoneOnlyKeepsItsShorterInterval()
    {
        var device = CreateDevice();
        AudioState(device).Update(false, true, null, null, null, null);
        Send(device, force: true);
        Assert.IsTrue(device.Timer.IsRunning);
        Assert.IsTrue(SpinWait.SpinUntil(
            () => device.Timer.ElapsedMilliseconds >= 1000, TimeSpan.FromSeconds(10)));
        Send(device);
        Assert.AreEqual(2, device.Writes);
        Assert.AreEqual((byte)0xA1, device.LastReport[2], "Preserve microphone mode in the keepalive.");
        Assert.IsTrue(device.Timer.IsRunning);
    }

    [TestMethod]
    public void FailedBluetoothWriteDoesNotPretendToRearmKeepAlive()
    {
        var device = CreateDevice();
        device.WriteSucceeds = false;
        device.Timer.Stop();
        Send(device, force: true);
        Assert.AreEqual(1, device.Writes);
        Assert.IsFalse(device.Timer.IsRunning,
            "Only an accepted output write can restart the successful-output clock.");
    }

    [TestMethod]
    public void DisabledPhysicalOutputDoesNotSendEvenForcedFeedback()
    {
        var device = CreateDevice();
        device.ModifyFeatureSetFlag(VidPidFeatureSet.NoOutputData, true);
        Send(device, force: true);
        Assert.AreEqual(0, device.Writes);
    }

    [TestMethod]
    public void UsbChangedFeedbackRetainsExistingTimerBehavior()
    {
        var device = CreateDevice(ConnectionType.USB);
        Send(device, force: true);
        Assert.AreEqual(1, device.Writes);
        Assert.IsFalse(device.Timer.IsRunning, "This correction is scoped to Bluetooth.");
    }

    private static RecordingDevice CreateDevice(ConnectionType transport = ConnectionType.BT)
    {
        // Deliberately bypass the hardware-opening DS4Device constructor.
        // Exercise the real output scheduler/serialization, with only the
        // physical write replaced. No HID handles, profiles or services.
        var device = (RecordingDevice)RuntimeHelpers.GetUninitializedObject(typeof(RecordingDevice));
        Set(device, "outputReportStateLock", new object());
        Set(device, "rumbleStateLock", new object());
        Set(device, "bluetoothAudioControlLaneLock", new object());
        Set(device, "bluetoothAudioState", new DualShock4BluetoothAudioState());
        Set(device, "rumbleAutostopTimer", new Stopwatch());
        Set(device, "standbySw", Stopwatch.StartNew());
        Set(device, "conType", transport);
        Set(device, "deviceType", DS4Windows.InputDevices.InputDeviceType.DS4);
        Set(device, "outReportBuffer", new byte[78]);
        Set(device, "outputReport", new byte[78]);
        Set(device, "outputBTCrc32Head", new byte[] { 0xA2 });
        Set(device, "btOutputPayloadLen", 78);
        Set(device, "knownGoodBTOutputReportType", (byte)0x11);
        Set(device, "outputFeaturesByte", (byte)0x07);
        device.WriteSucceeds = true;
        return device;
    }

    private static DualShock4BluetoothAudioState AudioState(DS4Device device) =>
        (DualShock4BluetoothAudioState)typeof(DS4Device).GetField(
            "bluetoothAudioState", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(device)!;

    private static void Set(DS4Device device, string name, object value) =>
        typeof(DS4Device).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(device, value);

    private static void Send(DS4Device device, bool force = false) =>
        typeof(DS4Device).GetMethod("sendOutputReport", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(device, new object[] { true, force, false });

    private sealed class RecordingDevice : DS4Device
    {
        private RecordingDevice() : base(null, "test-only") =>
            throw new InvalidOperationException("Never open a real device in these tests.");

        public int Writes;
        public bool WriteSucceeds;
        public bool ThrowOnWrite;
        public byte[] LastReport;
        public Stopwatch Timer => standbySw;

        protected override bool writeOutput()
        {
            Writes++;
            LastReport = (byte[])outputReport.Clone();
            if (ThrowOnWrite) throw new IOException("Simulated write failure");
            return WriteSucceeds;
        }
    }
}
