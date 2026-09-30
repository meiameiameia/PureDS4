using DS4WinWPF.DS4Control;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DS4Windows.Tests
{
    // Compose the production retry, slot and exposure coordinators. Only the
    // backend/physical operations are simulated; this is not a driver or
    // ControlService end-to-end test and never opens a real controller.
    [TestClass]
    public class VirtualOutputRecoveryScenarioTests
    {
        private static readonly int[] Delays = { 2000, 4000, 8000, 16000 };

        [TestMethod]
        public async Task TransientConnectFailurePublishesOnlyOneRecoveredOutput()
        {
            SimulatedOperations operations = new();
            int attempts = 0;
            bool exhausted = await OutputBindingRetrySequence.RunAsync(
                Delays, (_, _) => Task.CompletedTask, () =>
                {
                    operations.BackendAvailable = ++attempts == 3;
                    bool bound = operations.BindOutput();
                    if (!bound) operations.AssertNoOutput();
                    return !bound;
                }, CancellationToken.None);

            Assert.IsFalse(exhausted);
            Assert.AreEqual(3, attempts);
            Assert.AreEqual(1, operations.Slots.NumAttachedDevices);
            Assert.IsTrue(operations.Output.Connected);
            Assert.IsTrue(operations.ManagedPostcondition().Succeeded);
        }

        [TestMethod]
        public async Task ExhaustedBindingCanReleasePhysicalAndLaterReturnToManaged()
        {
            SimulatedOperations operations = new();
            ControllerExposureTransitionCoordinator coordinator = new();
            await ExhaustBinding(operations);

            Assert.IsFalse(operations.ManagedPostcondition().Succeeded);
            Assert.IsTrue(ControllerExposureEntryPolicy.CanAttemptNativePhysical(
                true, false, true, VirtualOutputBlockReason.OutputBindingFailed));
            ControllerExposureTransitionResult direct = coordinator.TransitionTo(
                ControllerExposureMode.NativePhysical, operations);
            Assert.IsTrue(direct.Succeeded);
            operations.AssertNative(coordinator);
            CollectionAssert.AreEqual(new[]
            {
                "neutralize", "quiesce", "retire", "release handle", "release containment",
            }, operations.Calls);

            // Backend still unavailable: returning to managed must roll back
            // to the exposed physical controller, not claim virtual readiness.
            ControllerExposureTransitionResult unavailable = coordinator.TransitionTo(
                ControllerExposureMode.ManagedVirtual, operations);
            Assert.IsFalse(unavailable.Succeeded);
            StringAssert.Contains(unavailable.Status.Detail, "simulated backend unavailable");
            operations.AssertNative(coordinator);
            Assert.IsFalse(operations.ManagedPostcondition().Succeeded);

            operations.BackendAvailable = true;
            ControllerExposureTransitionResult restored = coordinator.TransitionTo(
                ControllerExposureMode.ManagedVirtual, operations);
            Assert.IsTrue(restored.Succeeded);
            Assert.IsTrue(restored.Status.IsReady);
            Assert.IsTrue(operations.Contained);
            Assert.IsTrue(operations.HandleOpen);
            Assert.IsTrue(operations.ManagedPostcondition().Succeeded);
            Assert.AreEqual(1, operations.Slots.NumAttachedDevices);

            SimulatedOutput retired = operations.Output;
            Assert.IsTrue(coordinator.TransitionTo(
                ControllerExposureMode.NativePhysical, operations).Succeeded);
            operations.AssertNative(coordinator);
            Assert.IsFalse(retired.Connected);
        }

        [TestMethod]
        [Timeout(5000)]
        public async Task CancelledRetryCannotRecreateOutputAfterDirectTransition()
        {
            SimulatedOperations operations = new();
            ControllerExposureTransitionCoordinator coordinator = new();
            using CancellationTokenSource cancellation = new();
            TaskCompletionSource<bool> delayStarted = new(
                TaskCreationOptions.RunContinuationsAsynchronously);
            int attempts = 0;
            Task<bool> retry = OutputBindingRetrySequence.RunAsync(
                Delays, (_, token) =>
                {
                    delayStarted.TrySetResult(true);
                    return Task.Delay(Timeout.Infinite, token);
                }, () => { attempts++; return !operations.BindOutput(); },
                cancellation.Token);

            await delayStarted.Task;
            // ControlService cancels its retry token before this transition.
            cancellation.Cancel();
            Assert.IsTrue(coordinator.TransitionTo(
                ControllerExposureMode.NativePhysical, operations).Succeeded);
            operations.BackendAvailable = true;
            await Assert.ThrowsExceptionAsync<TaskCanceledException>(async () => await retry);

            Assert.AreEqual(0, attempts);
            operations.AssertNative(coordinator);
        }

        [TestMethod]
        public async Task FailedPhysicalReleaseAndFailedRollbackRequireRecovery()
        {
            SimulatedOperations operations = new();
            ControllerExposureTransitionCoordinator coordinator = new();
            await ExhaustBinding(operations);
            operations.FailPhysicalRelease = true;

            ControllerExposureTransitionResult failed = coordinator.TransitionTo(
                ControllerExposureMode.NativePhysical, operations);
            Assert.IsFalse(failed.Succeeded);
            Assert.IsTrue(failed.Status.NeedsRecovery);
            Assert.IsFalse(failed.Status.IsReady);
            StringAssert.Contains(failed.Status.Detail, "simulated containment failure");
            StringAssert.Contains(failed.Status.Detail, "simulated backend unavailable");
            operations.AssertNoOutput();
            Assert.IsFalse(operations.ManagedPostcondition().Succeeded);

            operations.BackendAvailable = true;
            int calls = operations.Calls.Count;
            Assert.IsFalse(coordinator.TransitionTo(
                ControllerExposureMode.NativePhysical, operations).Succeeded);
            Assert.AreEqual(calls, operations.Calls.Count);
            Assert.IsTrue(coordinator.Status.NeedsRecovery);
            operations.AssertNoOutput();
        }

        private static async Task ExhaustBinding(SimulatedOperations operations)
        {
            int attempts = 0;
            List<int> waited = new();
            bool exhausted = await OutputBindingRetrySequence.RunAsync(
                Delays, (milliseconds, _) =>
                {
                    waited.Add(milliseconds);
                    return Task.CompletedTask;
                }, () =>
                {
                    attempts++;
                    Assert.IsFalse(operations.BindOutput());
                    operations.AssertNoOutput();
                    return true;
                }, CancellationToken.None);

            Assert.IsTrue(exhausted);
            Assert.AreEqual(4, attempts);
            CollectionAssert.AreEqual(Delays, waited);
        }

        private sealed class SimulatedOperations : IControllerExposureTransitionOperations
        {
            internal readonly OutputSlotManager Slots = new();
            private readonly OutputDevice[] outputs = new OutputDevice[
                ControlService.CURRENT_DS4_CONTROLLER_LIMIT];
            internal readonly List<string> Calls = new();
            internal bool BackendAvailable;
            internal bool FailPhysicalRelease;
            internal bool Contained = true;
            internal bool HandleOpen = true;
            private bool readerRunning = true;
            internal SimulatedOutput Output => (SimulatedOutput)outputs[0];

            internal bool BindOutput() => Slots.TryBindInput(0, "simulated DS4", outputs,
                OutContType.ViiperX360, () => new SimulatedOutput(BackendAvailable), out _);

            internal ControllerExposureOperationResult ManagedPostcondition() =>
                ControllerExposureRecoveryPostcondition.Evaluate(true,
                    Output?.Connected == true,
                    Output == null ? OutContType.None : OutContType.ViiperX360,
                    OutContType.ViiperX360);

            internal void AssertNoOutput()
            {
                Assert.IsNull(Output);
                Assert.AreEqual(0, Slots.NumAttachedDevices);
            }

            internal void AssertNative(ControllerExposureTransitionCoordinator coordinator)
            {
                Assert.AreEqual(ControllerExposureStage.NativePhysicalReady, coordinator.Status.Stage);
                Assert.AreEqual(ControllerExposureMode.NativePhysical, coordinator.Status.Mode);
                Assert.IsFalse(Contained);
                Assert.IsFalse(HandleOpen);
                Assert.IsFalse(readerRunning);
                AssertNoOutput();
            }

            private ControllerExposureOperationResult Success(string call)
            {
                Calls.Add(call);
                return ControllerExposureOperationResult.Success();
            }

            public ControllerExposureOperationResult NeutralizeSyntheticOutputs() => Success("neutralize");

            public ControllerExposureOperationResult QuiescePhysicalInput()
            {
                readerRunning = false;
                return Success("quiesce");
            }

            public ControllerExposureOperationResult RetireVirtualOutputs()
            {
                Assert.IsFalse(readerRunning);
                if (Output != null)
                    Assert.IsTrue(Slots.TryUnbindInput(Output, 0, outputs, force: true));
                AssertNoOutput();
                return Success("retire");
            }

            public ControllerExposureOperationResult ReleasePhysicalHandle()
            {
                Assert.IsFalse(readerRunning);
                AssertNoOutput();
                HandleOpen = false;
                return Success("release handle");
            }

            public ControllerExposureOperationResult ReleasePhysicalContainment()
            {
                Assert.IsFalse(HandleOpen);
                AssertNoOutput();
                Calls.Add("release containment");
                if (FailPhysicalRelease)
                    return ControllerExposureOperationResult.Failure("simulated containment failure");
                Contained = false;
                return ControllerExposureOperationResult.Success();
            }

            public ControllerExposureOperationResult AcquirePhysicalContainment()
            {
                Contained = true;
                return Success("contain");
            }

            public ControllerExposureOperationResult AcquirePhysicalHandle()
            {
                Assert.IsTrue(Contained);
                HandleOpen = readerRunning = true;
                return Success("acquire handle");
            }

            public ControllerExposureOperationResult ResumePhysicalInput()
            {
                Assert.IsTrue(Contained && HandleOpen);
                readerRunning = true;
                return Success("resume");
            }

            public ControllerExposureOperationResult CreateVirtualOutputs()
            {
                Assert.IsTrue(Contained && HandleOpen && readerRunning);
                Calls.Add("create output");
                return BindOutput() ? ManagedPostcondition() :
                    ControllerExposureOperationResult.Failure("simulated backend unavailable");
            }
        }

        private sealed class SimulatedOutput : OutputDevice
        {
            private readonly bool backendAvailable;
            internal bool Connected;
            internal SimulatedOutput(bool backendAvailable) => this.backendAvailable = backendAvailable;
            public override void Connect()
            {
                if (!backendAvailable) throw new InvalidOperationException("simulated backend unavailable");
                Connected = true;
            }
            public override void Disconnect() => Connected = false;
            public override void ConvertandSendReport(DS4State state, int device) { }
            public override void ResetState(bool submit = true) { }
            public override string GetDeviceType() => nameof(SimulatedOutput);
            public override void RemoveFeedbacks() { }
            public override void RemoveFeedback(int inIdx) { }
        }
    }
}
