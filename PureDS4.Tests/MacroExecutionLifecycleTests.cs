using DS4Windows;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DS4WindowsTests;

[TestClass]
[DoNotParallelize]
public class MacroExecutionLifecycleTests
{
    [TestMethod]
    public async Task ProductionMacroCancellationReleasesHeldInputAndPreventsLaterSteps()
    {
        var events = new List<(int Code, bool Down)>();
        var pressed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Mapping.ResumeMacros(0);
        Mapping.MacroInputEmitterForTests = (code, scan, down) =>
        {
            lock (events) events.Add((code, down));
            if (code == 65 && down) pressed.TrySetResult();
        };
        try
        {
            Task macro = Mapping.ScheduleMacroForTests(0, new[] { 65, 5300, 66 });
            await pressed.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.IsTrue(Mapping.SuspendMacros(0, TimeSpan.FromSeconds(2)));
            await macro.WaitAsync(TimeSpan.FromSeconds(2));
            await Mapping.ScheduleMacroForTests(0, new[] { 67 }); // admission remains closed
            CollectionAssert.AreEqual(new[] { (65, true), (65, false) }, events);
            Mapping.ResumeMacros(0);
            await Mapping.ScheduleMacroForTests(0, new[] { 68, 68 });
            CollectionAssert.AreEqual(new[] { (65, true), (65, false), (68, true), (68, false) }, events);
        }
        finally
        {
            Mapping.SuspendMacros(0, TimeSpan.FromSeconds(2));
            Mapping.ResumeMacros(0);
            Mapping.MacroInputEmitterForTests = null;
        }
    }

    [TestMethod]
    public async Task CancellationSkipsQueuedMacrosAndDoesNotStopAnotherController()
    {
        var coordinator = new MacroExecutionCoordinator();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        bool queuedRan = false, otherRan = false;
        _ = coordinator.Schedule(0, "same trigger", token =>
        {
            started.TrySetResult();
            Task.Delay(TimeSpan.FromSeconds(10), token).GetAwaiter().GetResult();
        });
        _ = coordinator.Schedule(0, "same trigger", _ => queuedRan = true);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await coordinator.Schedule(1, null, _ => otherRan = true);
        Assert.IsTrue(coordinator.SuspendAndDrain(0, TimeSpan.FromSeconds(2)));
        Assert.IsFalse(queuedRan);
        Assert.IsTrue(otherRan);
    }

    [TestMethod]
    public void ReleasingOneOwnerCannotReleaseInputHeldByAnotherOwner()
    {
        var ownership = new MacroInputOwnership();
        var first = new object();
        var second = new object();
        var events = new List<bool>();
        ownership.Set(first, 65, false, true, events.Add);
        ownership.Set(second, 65, false, true, events.Add);
        ownership.Release(first);
        CollectionAssert.AreEqual(new[] { true }, events);
        ownership.Release(second);
        CollectionAssert.AreEqual(new[] { true, false }, events);
    }

    [TestMethod]
    public void HeldCleanupFailureRemainsRetryableAndCannotConfirmDrain()
    {
        var coordinator = new MacroExecutionCoordinator();
        bool fail = true;
        coordinator.RegisterCleanup(0, () => { if (fail) throw new IOException("release failed"); });
        Assert.ThrowsException<IOException>(() => coordinator.SuspendAndDrain(0, TimeSpan.FromSeconds(1)));
        Assert.IsFalse(coordinator.Resume(0));
        fail = false;
        Assert.IsTrue(coordinator.SuspendAndDrain(0, TimeSpan.FromSeconds(1)));
        Assert.IsTrue(coordinator.Resume(0));
    }

    [TestMethod]
    public void FailedPressEmissionStillRetainsOwnershipForFinallyCleanup()
    {
        var ownership = new MacroInputOwnership();
        var owner = new object();
        var events = new List<bool>();
        Assert.ThrowsException<IOException>(() => ownership.Set(owner, 65, false, true, down =>
        {
            events.Add(down);
            if (down) throw new IOException("handler failed after receiving press");
        }));
        ownership.Release(owner);
        CollectionAssert.AreEqual(new[] { true, false }, events);
    }

    [TestMethod]
    public async Task TimedOutMacroMustFinishDrainingBeforeAdmissionCanResume()
    {
        var coordinator = new MacroExecutionCoordinator();
        using var release = new ManualResetEventSlim();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task macro = coordinator.Schedule(0, null, token =>
        {
            started.TrySetResult();
            release.Wait(TimeSpan.FromSeconds(5)); // deliberately slow, non-cancellable operation
        });
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.IsFalse(coordinator.SuspendAndDrain(0, TimeSpan.Zero));
            Assert.IsFalse(coordinator.Resume(0));
            bool admitted = false;
            await coordinator.Schedule(0, null, _ => admitted = true);
            Assert.IsFalse(admitted);
            release.Set();
            await macro.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.IsTrue(coordinator.SuspendAndDrain(0, TimeSpan.FromSeconds(2)));
            Assert.IsTrue(coordinator.Resume(0));
            await coordinator.Schedule(0, null, _ => admitted = true);
            Assert.IsTrue(admitted);
        }
        finally
        {
            release.Set();
            await macro.WaitAsync(TimeSpan.FromSeconds(2));
        }
    }

    [TestMethod]
    public async Task OrdinaryMappingAndMacroShareAKeyUntilBothOwnersRelease()
    {
        var events = new List<(int Code, bool Down)>();
        var pressed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Mapping.ResumeMacros(0);
        Mapping.MacroInputEmitterForTests = (code, scan, down) =>
        {
            lock (events) events.Add((code, down));
            if (down) pressed.TrySetResult();
        };
        try
        {
            Task macro = Mapping.ScheduleMacroForTests(0, new[] { 65, 5300 });
            await pressed.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Mapping.EmitMappedKey(65, 65, false, true);
            Mapping.EmitMappedKey(65, 65, false, false);
            CollectionAssert.AreEqual(new[] { (65, true) }, events);
            Mapping.EmitMappedKey(65, 65, false, true);
            Assert.IsTrue(Mapping.SuspendMacros(0, TimeSpan.FromSeconds(2)));
            await macro;
            CollectionAssert.AreEqual(new[] { (65, true) }, events);
            Mapping.EmitMappedKey(65, 65, false, false);
            CollectionAssert.AreEqual(new[] { (65, true), (65, false) }, events);
        }
        finally
        {
            Mapping.SuspendMacros(0, TimeSpan.FromSeconds(2));
            Mapping.EmitMappedKey(65, 65, false, false);
            Mapping.ResumeMacros(0);
            Mapping.MacroInputEmitterForTests = null;
        }
    }

    [TestMethod]
    public async Task ScanCodeMacroMouseAndOrdinaryMappingShareButtonOwnership()
    {
        var events = new List<(int Code, bool Down)>();
        var pressed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Mapping.ResumeMacros(0);
        Mapping.MacroInputEmitterForTests = (code, scan, down) =>
        {
            Assert.IsFalse(scan, "Mouse buttons must not acquire a separate scan-code identity.");
            lock (events) events.Add((code, down));
            if (down) pressed.TrySetResult();
        };
        try
        {
            Task macro = Mapping.ScheduleMacroForTests(0, new[] { 256, 5300 }, DS4KeyType.ScanCode);
            await pressed.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Mapping.EmitMappedMouse(256, true);
            Assert.IsTrue(Mapping.SuspendMacros(0, TimeSpan.FromSeconds(2)));
            await macro;
            CollectionAssert.AreEqual(new[] { (256, true) }, events);
            Mapping.EmitMappedMouse(256, false);
            CollectionAssert.AreEqual(new[] { (256, true), (256, false) }, events);
        }
        finally
        {
            Mapping.SuspendMacros(0, TimeSpan.FromSeconds(2));
            Mapping.EmitMappedMouse(256, false);
            Mapping.ResumeMacros(0);
            Mapping.MacroInputEmitterForTests = null;
        }
    }
}
