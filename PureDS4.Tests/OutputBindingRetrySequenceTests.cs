using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DS4WinWPF.DS4Control;

namespace DS4Windows.Tests
{
    [TestClass]
    public class OutputBindingRetrySequenceTests
    {
        private static readonly int[] Delays = { 2000, 4000, 8000, 16000 };

        [TestMethod]
        public async Task TransientFailuresStopAfterSuccessfulBinding()
        {
            List<int> waited = new();
            int attempts = 0;

            bool exhausted = await OutputBindingRetrySequence.RunAsync(
                Delays,
                (milliseconds, _) =>
                {
                    waited.Add(milliseconds);
                    return Task.CompletedTask;
                },
                () => ++attempts < 3,
                CancellationToken.None);

            Assert.IsFalse(exhausted);
            Assert.AreEqual(3, attempts);
            CollectionAssert.AreEqual(new[] { 2000, 4000, 8000 }, waited);
        }

        [TestMethod]
        public async Task ChangedControllerStateStopsWithoutFurtherAttempts()
        {
            int waits = 0;
            int attempts = 0;

            bool exhausted = await OutputBindingRetrySequence.RunAsync(
                Delays,
                (_, _) =>
                {
                    waits++;
                    return Task.CompletedTask;
                },
                () => { attempts++; return false; },
                CancellationToken.None);

            Assert.IsFalse(exhausted);
            Assert.AreEqual(1, waits);
            Assert.AreEqual(1, attempts);
        }

        [TestMethod]
        public async Task CancellationPreventsAnotherBindingAttempt()
        {
            using CancellationTokenSource cancellation = new();
            int waits = 0;
            int attempts = 0;

            await Assert.ThrowsExceptionAsync<OperationCanceledException>(
                async () => await OutputBindingRetrySequence.RunAsync(
                    Delays,
                    (_, _) =>
                    {
                        waits++;
                        return Task.CompletedTask;
                    },
                    () =>
                    {
                        attempts++;
                        cancellation.Cancel();
                        return true;
                    },
                    cancellation.Token));

            Assert.AreEqual(1, waits);
            Assert.AreEqual(1, attempts);
        }

        [TestMethod]
        public async Task CancellationDuringDelayPreventsBindingAttempt()
        {
            using CancellationTokenSource cancellation = new();
            TaskCompletionSource<bool> delayStarted = new(
                TaskCreationOptions.RunContinuationsAsynchronously);
            int attempts = 0;

            Task<bool> run = OutputBindingRetrySequence.RunAsync(
                Delays,
                (_, token) =>
                {
                    delayStarted.TrySetResult(true);
                    return Task.Delay(Timeout.Infinite, token);
                },
                () => { attempts++; return true; },
                cancellation.Token);

            await delayStarted.Task;
            cancellation.Cancel();
            await Assert.ThrowsExceptionAsync<TaskCanceledException>(
                async () => await run);

            Assert.AreEqual(0, attempts);
        }

        [TestMethod]
        public async Task RepeatedFailureExhaustsOnlyConfiguredAttempts()
        {
            List<int> waited = new();
            int attempts = 0;

            bool exhausted = await OutputBindingRetrySequence.RunAsync(
                Delays,
                (milliseconds, _) =>
                {
                    waited.Add(milliseconds);
                    return Task.CompletedTask;
                },
                () => { attempts++; return true; },
                CancellationToken.None);

            Assert.IsTrue(exhausted);
            Assert.AreEqual(Delays.Length, attempts);
            CollectionAssert.AreEqual(Delays, waited);
        }

        [TestMethod]
        public async Task BindingUnavailableThenAvailablePublishesOnlyRecoveredOutput()
        {
            OutputSlotManager manager = new();
            OutputDevice[] runtimeOutputs = new OutputDevice[manager.OutputSlots.Length];
            TestOutputDevice recoveredOutput = new();
            List<int> waited = new();
            int attempts = 0;

            bool exhausted = await OutputBindingRetrySequence.RunAsync(
                Delays,
                (milliseconds, _) =>
                {
                    waited.Add(milliseconds);
                    return Task.CompletedTask;
                },
                () =>
                {
                    attempts++;
                    bool bound = manager.TryBindInput(0, "DS4", runtimeOutputs,
                        OutContType.ViiperX360,
                        () => attempts < 3 ? null : recoveredOutput,
                        out OutSlotDevice slot);
                    if (attempts < 3)
                    {
                        Assert.IsFalse(bound);
                        Assert.IsNull(slot);
                        Assert.IsNull(runtimeOutputs[0]);
                        Assert.AreEqual(0, manager.NumAttachedDevices);
                    }

                    return !bound;
                },
                CancellationToken.None);

            Assert.IsFalse(exhausted);
            Assert.AreEqual(3, attempts);
            CollectionAssert.AreEqual(new[] { 2000, 4000, 8000 }, waited);
            Assert.AreSame(recoveredOutput, runtimeOutputs[0]);
            Assert.AreEqual(1, recoveredOutput.ConnectCount);
            Assert.AreEqual(1, manager.NumAttachedDevices);
        }

        private sealed class TestOutputDevice : OutputDevice
        {
            public int ConnectCount { get; private set; }

            public override void ConvertandSendReport(DS4State state, int device) { }
            public override void Connect() => ConnectCount++;
            public override void Disconnect() { }
            public override void ResetState(bool submit = true) { }
            public override string GetDeviceType() => nameof(TestOutputDevice);
            public override void RemoveFeedbacks() { }
            public override void RemoveFeedback(int inIdx) { }
        }
    }
}
