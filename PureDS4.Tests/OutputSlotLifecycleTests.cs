using System;
using System.Collections.Generic;
using System.ComponentModel;
using DS4Windows;
using DS4WinWPF.DS4Control;

namespace DS4WindowsTests
{
    [TestClass]
    public class OutputSlotLifecycleTests
    {
        [TestMethod]
        public void DynamicAttachmentConnectsBeforePublishingSlotAndAssignment()
        {
            List<string> calls = new();
            OutputSlotManager manager = new();
            OutputDevice[] runtimeOutputs = NewRuntimeOutputs(manager);
            RecordingOutputDevice output = new(OutContType.ViiperX360, calls);
            int assignedSlot = -1;

            manager.SlotAssigned += (_, slot, outSlot) =>
            {
                calls.Add("assigned");
                assignedSlot = slot;
                Assert.AreSame(output, runtimeOutputs[2]);
                Assert.AreSame(output, outSlot.OutputDevice);
                Assert.AreEqual(OutSlotDevice.AttachedStatus.Attached,
                    outSlot.CurrentAttachedStatus);
                Assert.AreEqual(OutSlotDevice.InputBound.Bound,
                    outSlot.CurrentInputBound);
            };

            manager.DeferredPlugin(output, 2, "DS4", runtimeOutputs,
                OutContType.ViiperX360);

            CollectionAssert.AreEqual(new[] { "connect", "assigned" }, calls);
            Assert.AreEqual(0, assignedSlot);
            Assert.AreSame(output, manager.GetOutSlotDevice(output).OutputDevice);
            Assert.AreEqual(1, manager.NumAttachedDevices);
        }

        [TestMethod]
        public void ConnectionFailureDoesNotPublishSlotOrRaiseAssignment()
        {
            List<string> calls = new();
            OutputSlotManager manager = new();
            OutputDevice[] runtimeOutputs = NewRuntimeOutputs(manager);
            RecordingOutputDevice output = new(OutContType.ViiperX360, calls)
            {
                ConnectException = new Win32Exception(5, "denied"),
            };
            int assignmentCount = 0;
            manager.SlotAssigned += (_, _, _) => assignmentCount++;

            manager.DeferredPlugin(output, 0, "DS4", runtimeOutputs,
                OutContType.ViiperX360);

            CollectionAssert.AreEqual(new[] { "connect" }, calls);
            Assert.AreEqual(0, assignmentCount);
            Assert.IsNull(runtimeOutputs[0]);
            Assert.IsNull(manager.GetOutSlotDevice(output));
            Assert.AreEqual(0, manager.NumAttachedDevices);
            Assert.AreEqual(OutSlotDevice.AttachedStatus.UnAttached,
                manager.OutputSlots[0].CurrentAttachedStatus);
        }

        [TestMethod]
        public void NoAvailableSlotDoesNotPartiallyBindAnotherOutput()
        {
            OutputSlotManager manager = new();
            OutputDevice[] runtimeOutputs = NewRuntimeOutputs(manager);
            for (int slot = 0; slot < manager.OutputSlots.Length; slot++)
            {
                manager.DeferredPlugin(new RecordingOutputDevice(OutContType.ViiperX360),
                    slot, $"input-{slot}", runtimeOutputs, OutContType.ViiperX360);
            }

            RecordingOutputDevice rejected = new(OutContType.ViiperX360);
            int assignmentCount = 0;
            manager.SlotAssigned += (_, _, _) => assignmentCount++;

            manager.DeferredPlugin(rejected, 0, "new-input", runtimeOutputs,
                OutContType.ViiperX360);

            Assert.AreEqual(0, rejected.ConnectCount);
            Assert.AreEqual(0, assignmentCount);
            Assert.IsNull(manager.GetOutSlotDevice(rejected));
            Assert.AreEqual(manager.OutputSlots.Length, manager.NumAttachedDevices);
            Assert.AreNotSame(rejected, runtimeOutputs[0]);
        }

        [TestMethod]
        public void UnboundPermanentSlotIsReusedWithoutAnotherVirtualConnect()
        {
            OutputSlotManager manager = new();
            OutputDevice[] runtimeOutputs = NewRuntimeOutputs(manager);
            RecordingOutputDevice output = new(OutContType.ViiperDS4);

            manager.DeferredPlugin(output, -1, string.Empty, runtimeOutputs,
                OutContType.ViiperDS4);
            OutSlotDevice slot = manager.GetOutSlotDevice(output);
            slot.CurrentReserveStatus = OutSlotDevice.ReserveStatus.Permanent;

            OutSlotDevice reusable = manager.FindExistUnboundSlotType(
                OutContType.ViiperDS4);
            reusable.CurrentInputBound = OutSlotDevice.InputBound.Bound;
            runtimeOutputs[1] = reusable.OutputDevice;

            Assert.AreSame(slot, reusable);
            Assert.AreSame(output, runtimeOutputs[1]);
            Assert.AreEqual(1, output.ConnectCount);
            Assert.AreEqual(1, manager.NumAttachedDevices);
            Assert.AreEqual(OutSlotDevice.ReserveStatus.Permanent,
                reusable.CurrentReserveStatus);
        }

        [TestMethod]
        public void DynamicDisconnectRemovesFeedbackBeforeDisconnectAndClearsProjections()
        {
            List<string> calls = new();
            OutputSlotManager manager = new();
            OutputDevice[] runtimeOutputs = NewRuntimeOutputs(manager);
            RecordingOutputDevice output = new(OutContType.ViiperX360, calls);
            int unassignedSlot = -1;

            manager.DeferredPlugin(output, 0, "DS4", runtimeOutputs,
                OutContType.ViiperX360);
            OutSlotDevice slot = manager.GetOutSlotDevice(output);
            manager.SlotUnassigned += (_, index, outSlot) =>
            {
                calls.Add("unassigned");
                unassignedSlot = index;
                Assert.IsNull(runtimeOutputs[0]);
                Assert.IsNull(outSlot.OutputDevice);
                Assert.AreEqual(OutSlotDevice.AttachedStatus.UnAttached,
                    outSlot.CurrentAttachedStatus);
            };

            manager.DeferredRemoval(output, 0, runtimeOutputs);

            CollectionAssert.AreEqual(new[] { "connect", "feedbacks", "disconnect", "unassigned" },
                calls);
            Assert.AreEqual(0, unassignedSlot);
            Assert.IsNull(manager.GetOutSlotDevice(output));
            Assert.AreEqual(0, manager.NumAttachedDevices);
            Assert.AreEqual(OutSlotDevice.InputBound.Unbound,
                slot.CurrentInputBound);
            Assert.AreEqual(OutSlotDevice.INPUT_INDEX_DEFAULT, slot.InputIndex);
            Assert.AreEqual(string.Empty, slot.InputDisplayString);
        }

        [TestMethod]
        public void PermanentDisconnectCallerSequencePreservesVirtualOutput()
        {
            List<string> calls = new();
            OutputSlotManager manager = new();
            OutputDevice[] runtimeOutputs = NewRuntimeOutputs(manager);
            RecordingOutputDevice output = new(OutContType.ViiperDS4, calls);

            manager.DeferredPlugin(output, 0, "DS4", runtimeOutputs,
                OutContType.ViiperDS4);
            OutSlotDevice slot = manager.GetOutSlotDevice(output);
            slot.CurrentReserveStatus = OutSlotDevice.ReserveStatus.Permanent;

            // This is the existing permanent-slot caller sequence in
            // ControlService.UnplugOutDev, not OutputSlotManager.DeferredRemoval.
            runtimeOutputs[0] = null;
            slot.CurrentInputBound = OutSlotDevice.InputBound.Unbound;
            output.ResetState();
            output.RemoveFeedbacks();

            CollectionAssert.AreEqual(new[] { "connect", "reset", "feedbacks" }, calls);
            Assert.AreSame(output, slot.OutputDevice);
            Assert.AreEqual(OutSlotDevice.AttachedStatus.Attached,
                slot.CurrentAttachedStatus);
            Assert.AreEqual(OutSlotDevice.InputBound.Unbound,
                slot.CurrentInputBound);
            Assert.IsNull(runtimeOutputs[0]);
            Assert.AreEqual(0, output.DisconnectCount);
        }

        [TestMethod]
        public void BindDisconnectBindUsesANewDynamicOutputAfterTeardown()
        {
            OutputSlotManager manager = new();
            OutputDevice[] runtimeOutputs = NewRuntimeOutputs(manager);
            RecordingOutputDevice first = new(OutContType.ViiperX360);
            RecordingOutputDevice second = new(OutContType.ViiperX360);

            manager.DeferredPlugin(first, 0, "first", runtimeOutputs,
                OutContType.ViiperX360);
            manager.DeferredRemoval(first, 0, runtimeOutputs);
            manager.DeferredPlugin(second, 0, "second", runtimeOutputs,
                OutContType.ViiperX360);

            OutSlotDevice slot = manager.GetOutSlotDevice(second);
            Assert.AreEqual(1, first.ConnectCount);
            Assert.AreEqual(1, first.DisconnectCount);
            Assert.AreEqual(1, second.ConnectCount);
            Assert.AreSame(second, runtimeOutputs[0]);
            Assert.AreEqual(0, slot.Index);
            Assert.AreEqual(OutSlotDevice.InputBound.Bound,
                slot.CurrentInputBound);
        }

        [TestMethod]
        public void RepeatedAndPartialTeardownLeaveRuntimeProjectionCleared()
        {
            OutputSlotManager manager = new();
            OutputDevice[] runtimeOutputs = NewRuntimeOutputs(manager);
            RecordingOutputDevice attached = new(OutContType.ViiperX360);
            RecordingOutputDevice untracked = new(OutContType.ViiperX360);

            manager.DeferredPlugin(attached, 0, "DS4", runtimeOutputs,
                OutContType.ViiperX360);
            manager.DeferredRemoval(attached, 0, runtimeOutputs);
            manager.DeferredRemoval(attached, 0, runtimeOutputs);
            manager.DeferredRemoval(untracked, 0, runtimeOutputs);

            Assert.IsNull(runtimeOutputs[0]);
            Assert.AreEqual(1, attached.RemoveFeedbacksCount);
            Assert.AreEqual(1, attached.DisconnectCount);
            Assert.AreEqual(0, untracked.RemoveFeedbacksCount);
            Assert.AreEqual(0, untracked.DisconnectCount);
            Assert.AreEqual(0, manager.NumAttachedDevices);
        }

        [TestMethod]
        public void LegacyRequestedOutputTypesAreNormalizedBeforeSlotPublication()
        {
            OutputSlotManager manager = new();
            OutputDevice[] runtimeOutputs = NewRuntimeOutputs(manager);
            RecordingOutputDevice output = new(OutContType.ViiperDS4);

            manager.DeferredPlugin(output, 0, "DS4", runtimeOutputs,
                OutContType.DS4);
            OutSlotDevice slot = manager.GetOutSlotDevice(output);
            slot.CurrentReserveStatus = OutSlotDevice.ReserveStatus.Permanent;
            slot.CurrentInputBound = OutSlotDevice.InputBound.Unbound;

            Assert.AreEqual(OutContType.ViiperDS4, slot.CurrentType);
            Assert.AreEqual(OutContType.ViiperDS4, slot.PermanentType);
            Assert.AreSame(slot, manager.FindExistUnboundSlotType(
                OutContType.ViiperDS4));
            Assert.IsNull(manager.FindExistUnboundSlotType(OutContType.DS4));
        }

        [TestMethod]
        public void TryBindInputDynamicallyConnectsBeforePublicationAndAssignment()
        {
            List<string> calls = new();
            OutputSlotManager manager = new();
            OutputDevice[] runtimeOutputs = NewRuntimeOutputs(manager);
            RecordingOutputDevice output = new(OutContType.ViiperX360, calls);
            int factoryCalls = 0;

            manager.SlotAssigned += (_, slot, outSlot) =>
            {
                calls.Add("assigned");
                Assert.AreEqual(0, slot);
                Assert.AreSame(output, runtimeOutputs[2]);
                Assert.AreSame(output, outSlot.OutputDevice);
                Assert.AreEqual(OutSlotDevice.InputBound.Bound,
                    outSlot.CurrentInputBound);
            };

            bool bound = manager.TryBindInput(2, "DS4", runtimeOutputs,
                OutContType.ViiperX360, () =>
                {
                    factoryCalls++;
                    return output;
                }, out OutSlotDevice slotDevice);

            Assert.IsTrue(bound);
            Assert.AreEqual(1, factoryCalls);
            Assert.AreSame(output, slotDevice.OutputDevice);
            CollectionAssert.AreEqual(new[] { "connect", "assigned" }, calls);
        }

        [TestMethod]
        public void TryBindInputReusesPermanentSlotWithoutCreatingOutput()
        {
            OutputSlotManager manager = new();
            OutputDevice[] runtimeOutputs = NewRuntimeOutputs(manager);
            RecordingOutputDevice output = new(OutContType.ViiperDS4);
            manager.DeferredPlugin(output, -1, string.Empty, runtimeOutputs,
                OutContType.ViiperDS4);
            OutSlotDevice permanentSlot = manager.GetOutSlotDevice(output);
            permanentSlot.CurrentReserveStatus = OutSlotDevice.ReserveStatus.Permanent;
            int factoryCalls = 0;

            bool bound = manager.TryBindInput(1, "DS4", runtimeOutputs,
                OutContType.ViiperDS4, () =>
                {
                    factoryCalls++;
                    return new RecordingOutputDevice(OutContType.ViiperDS4);
                }, out OutSlotDevice slotDevice);

            Assert.IsTrue(bound);
            Assert.AreSame(permanentSlot, slotDevice);
            Assert.AreSame(output, runtimeOutputs[1]);
            Assert.AreEqual(0, factoryCalls);
            Assert.AreEqual(1, output.ConnectCount);
        }

        [TestMethod]
        public void TryBindInputCanSkipPermanentReuseWhenRequested()
        {
            OutputSlotManager manager = new();
            OutputDevice[] runtimeOutputs = NewRuntimeOutputs(manager);
            RecordingOutputDevice permanentOutput = new(OutContType.ViiperDS4);
            manager.DeferredPlugin(permanentOutput, -1, string.Empty, runtimeOutputs,
                OutContType.ViiperDS4);
            OutSlotDevice permanentSlot = manager.GetOutSlotDevice(permanentOutput);
            permanentSlot.CurrentReserveStatus = OutSlotDevice.ReserveStatus.Permanent;
            RecordingOutputDevice dynamicOutput = new(OutContType.ViiperDS4);
            int factoryCalls = 0;

            bool bound = manager.TryBindInput(1, "DS4", runtimeOutputs,
                OutContType.ViiperDS4, () =>
                {
                    factoryCalls++;
                    return dynamicOutput;
                }, out OutSlotDevice slotDevice, allowPermanentSlotReuse: false);

            Assert.IsTrue(bound);
            Assert.AreEqual(1, factoryCalls);
            Assert.AreNotSame(permanentSlot, slotDevice);
            Assert.AreSame(dynamicOutput, runtimeOutputs[1]);
            Assert.AreEqual(OutSlotDevice.InputBound.Unbound,
                permanentSlot.CurrentInputBound);
            Assert.AreEqual(2, manager.NumAttachedDevices);
        }

        [TestMethod]
        public void TryBindInputConnectionFailureLeavesNoPublishedState()
        {
            OutputSlotManager manager = new();
            OutputDevice[] runtimeOutputs = NewRuntimeOutputs(manager);
            RecordingOutputDevice output = new(OutContType.ViiperX360)
            {
                ConnectException = new Win32Exception(5, "denied"),
            };
            int assignmentCount = 0;
            manager.SlotAssigned += (_, _, _) => assignmentCount++;

            bool bound = manager.TryBindInput(0, "DS4", runtimeOutputs,
                OutContType.ViiperX360, () => output,
                out OutSlotDevice slotDevice);

            Assert.IsFalse(bound);
            Assert.IsNull(slotDevice);
            Assert.AreEqual(0, assignmentCount);
            Assert.IsNull(runtimeOutputs[0]);
            Assert.AreEqual(0, manager.NumAttachedDevices);
        }

        [TestMethod]
        public void TryBindInputDoesNotCreateOutputWhenNoSlotIsAvailable()
        {
            OutputSlotManager manager = new();
            OutputDevice[] runtimeOutputs = NewRuntimeOutputs(manager);
            for (int slot = 0; slot < manager.OutputSlots.Length; slot++)
            {
                manager.DeferredPlugin(new RecordingOutputDevice(OutContType.ViiperX360),
                    slot, $"input-{slot}", runtimeOutputs, OutContType.ViiperX360);
            }

            int factoryCalls = 0;
            bool bound = manager.TryBindInput(0, "new-input", runtimeOutputs,
                OutContType.ViiperX360, () =>
                {
                    factoryCalls++;
                    return new RecordingOutputDevice(OutContType.ViiperX360);
                }, out OutSlotDevice slotDevice);

            Assert.IsFalse(bound);
            Assert.IsNull(slotDevice);
            Assert.AreEqual(0, factoryCalls);
            Assert.AreEqual(manager.OutputSlots.Length, manager.NumAttachedDevices);
        }

        [TestMethod]
        public void TryBindInputNormalizesLegacyOutputType()
        {
            OutputSlotManager manager = new();
            OutputDevice[] runtimeOutputs = NewRuntimeOutputs(manager);

            bool bound = manager.TryBindInput(0, "DS4", runtimeOutputs,
                OutContType.DS4, () => new RecordingOutputDevice(OutContType.ViiperDS4),
                out OutSlotDevice slotDevice);

            Assert.IsTrue(bound);
            Assert.AreEqual(OutContType.ViiperDS4, slotDevice.CurrentType);
            Assert.AreSame(slotDevice, manager.GetOutSlotDevice(slotDevice.OutputDevice));
        }

        [TestMethod]
        public void TryUnbindInputDestroysDynamicOutputAfterFeedbackRemoval()
        {
            List<string> calls = new();
            OutputSlotManager manager = new();
            OutputDevice[] runtimeOutputs = NewRuntimeOutputs(manager);
            RecordingOutputDevice output = new(OutContType.ViiperX360, calls);

            Assert.IsTrue(manager.TryBindInput(0, "DS4", runtimeOutputs,
                OutContType.ViiperX360, () => output, out _));
            Assert.IsTrue(manager.TryUnbindInput(output, 0, runtimeOutputs));

            CollectionAssert.AreEqual(new[] { "connect", "feedbacks", "disconnect" },
                calls);
            Assert.IsNull(runtimeOutputs[0]);
            Assert.IsNull(manager.GetOutSlotDevice(output));
        }

        [TestMethod]
        public void TryUnbindInputRetainsPermanentOutputAfterResetAndFeedbackRemoval()
        {
            List<string> calls = new();
            OutputSlotManager manager = new();
            OutputDevice[] runtimeOutputs = NewRuntimeOutputs(manager);
            RecordingOutputDevice output = new(OutContType.ViiperDS4, calls);

            Assert.IsTrue(manager.TryBindInput(0, "DS4", runtimeOutputs,
                OutContType.ViiperDS4, () => output, out OutSlotDevice slotDevice));
            slotDevice.CurrentReserveStatus = OutSlotDevice.ReserveStatus.Permanent;

            Assert.IsTrue(manager.TryUnbindInput(output, 0, runtimeOutputs));

            CollectionAssert.AreEqual(new[] { "connect", "reset", "feedbacks" },
                calls);
            Assert.IsNull(runtimeOutputs[0]);
            Assert.AreSame(output, slotDevice.OutputDevice);
            Assert.AreEqual(OutSlotDevice.InputBound.Unbound,
                slotDevice.CurrentInputBound);
            Assert.AreEqual(0, output.DisconnectCount);
        }

        [TestMethod]
        public void TryUnbindInputForceDestroysPermanentOutput()
        {
            List<string> calls = new();
            OutputSlotManager manager = new();
            OutputDevice[] runtimeOutputs = NewRuntimeOutputs(manager);
            RecordingOutputDevice output = new(OutContType.ViiperDS4, calls);

            Assert.IsTrue(manager.TryBindInput(0, "DS4", runtimeOutputs,
                OutContType.ViiperDS4, () => output, out OutSlotDevice slotDevice));
            slotDevice.CurrentReserveStatus = OutSlotDevice.ReserveStatus.Permanent;

            Assert.IsTrue(manager.TryUnbindInput(output, 0, runtimeOutputs, force: true));

            CollectionAssert.AreEqual(new[] { "connect", "feedbacks", "disconnect" },
                calls);
            Assert.IsNull(runtimeOutputs[0]);
            Assert.IsNull(manager.GetOutSlotDevice(output));
            Assert.AreEqual(1, output.DisconnectCount);
        }

        [TestMethod]
        public void TryUnbindInputIsSafeAfterDynamicTeardownAndForMissingOutput()
        {
            OutputSlotManager manager = new();
            OutputDevice[] runtimeOutputs = NewRuntimeOutputs(manager);
            RecordingOutputDevice output = new(OutContType.ViiperX360);

            Assert.IsTrue(manager.TryBindInput(0, "DS4", runtimeOutputs,
                OutContType.ViiperX360, () => output, out _));
            Assert.IsTrue(manager.TryUnbindInput(output, 0, runtimeOutputs));
            Assert.IsFalse(manager.TryUnbindInput(output, 0, runtimeOutputs));
            runtimeOutputs[0] = output;
            Assert.IsFalse(manager.TryUnbindInput(null, 0, runtimeOutputs));

            Assert.IsNull(runtimeOutputs[0]);
            Assert.AreEqual(1, output.DisconnectCount);
        }

        [TestMethod]
        public void TryBindUnbindBindCreatesANewDynamicOutput()
        {
            OutputSlotManager manager = new();
            OutputDevice[] runtimeOutputs = NewRuntimeOutputs(manager);
            RecordingOutputDevice first = new(OutContType.ViiperX360);
            RecordingOutputDevice second = new(OutContType.ViiperX360);

            Assert.IsTrue(manager.TryBindInput(0, "first", runtimeOutputs,
                OutContType.ViiperX360, () => first, out _));
            Assert.IsTrue(manager.TryUnbindInput(first, 0, runtimeOutputs));
            Assert.IsTrue(manager.TryBindInput(0, "second", runtimeOutputs,
                OutContType.ViiperX360, () => second, out OutSlotDevice slotDevice));

            Assert.AreEqual(1, first.DisconnectCount);
            Assert.AreEqual(1, second.ConnectCount);
            Assert.AreSame(second, runtimeOutputs[0]);
            Assert.AreSame(second, slotDevice.OutputDevice);
        }

        private static OutputDevice[] NewRuntimeOutputs(OutputSlotManager manager)
        {
            return new OutputDevice[manager.OutputSlots.Length];
        }

        private sealed class RecordingOutputDevice : OutputDevice
        {
            private readonly List<string> calls;
            private readonly OutContType type;

            public RecordingOutputDevice(OutContType type, List<string> calls = null)
            {
                this.type = type.Normalize();
                this.calls = calls;
            }

            public Exception ConnectException { get; init; }
            public int ConnectCount { get; private set; }
            public int DisconnectCount { get; private set; }
            public int RemoveFeedbacksCount { get; private set; }

            public override void ConvertandSendReport(DS4State state, int device)
            {
            }

            public override void Connect()
            {
                ConnectCount++;
                calls?.Add("connect");
                if (ConnectException != null)
                {
                    throw ConnectException;
                }
            }

            public override void Disconnect()
            {
                DisconnectCount++;
                calls?.Add("disconnect");
            }

            public override void ResetState(bool submit = true)
            {
                calls?.Add("reset");
            }

            public override string GetDeviceType()
            {
                return type.ToString();
            }

            public override void RemoveFeedbacks()
            {
                RemoveFeedbacksCount++;
                calls?.Add("feedbacks");
            }

            public override void RemoveFeedback(int inIdx)
            {
            }
        }
    }
}
