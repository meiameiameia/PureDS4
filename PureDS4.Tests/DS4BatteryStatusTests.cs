using DS4Windows;

namespace DS4WindowsTests
{
    [TestClass]
    public class DS4BatteryStatusTests
    {
        [DataTestMethod]
        [DataRow(0, 5)]
        [DataRow(5, 55)]
        [DataRow(9, 95)]
        public void DecodesBatteryCapacityBucketsWithoutCable(int rawCapacity,
            int expectedCapacity)
        {
            DS4BatteryStatusReading reading =
                DS4BatteryStatusDecoder.Decode((byte)rawCapacity);

            Assert.IsFalse(reading.CableConnected);
            Assert.AreEqual(DS4BatteryStatus.OnBattery, reading.Status);
            Assert.IsTrue(reading.HasCapacity);
            Assert.AreEqual(expectedCapacity, reading.Capacity);
        }

        [DataTestMethod]
        [DataRow(0, 5)]
        [DataRow(5, 55)]
        [DataRow(10, 100)]
        public void DecodesChargingCapacityBucketsWithCable(int rawCapacity,
            int expectedCapacity)
        {
            DS4BatteryStatusReading reading =
                DS4BatteryStatusDecoder.Decode((byte)(0x10 | rawCapacity));

            Assert.IsTrue(reading.CableConnected);
            Assert.AreEqual(DS4BatteryStatus.Charging, reading.Status);
            Assert.IsTrue(reading.HasCapacity);
            Assert.AreEqual(expectedCapacity, reading.Capacity);
        }

        [TestMethod]
        public void DecodesFullSeparatelyFromCharging()
        {
            DS4BatteryStatusReading reading = DS4BatteryStatusDecoder.Decode(0x1B);

            Assert.IsTrue(reading.CableConnected);
            Assert.AreEqual(DS4BatteryStatus.Full, reading.Status);
            Assert.IsTrue(reading.HasCapacity);
            Assert.AreEqual(100, reading.Capacity);
        }

        [DataTestMethod]
        [DataRow(0x1E)]
        [DataRow(0x1F)]
        public void DecodesCableErrorStatesWithoutCapacity(int rawStatus)
        {
            DS4BatteryStatusReading reading =
                DS4BatteryStatusDecoder.Decode((byte)rawStatus);
            DS4BatteryStatus expectedStatus = rawStatus == 0x1E
                ? DS4BatteryStatus.ChargingUnavailable
                : DS4BatteryStatus.ChargingError;

            Assert.IsTrue(reading.CableConnected);
            Assert.AreEqual(expectedStatus, reading.Status);
            Assert.IsFalse(reading.HasCapacity);
        }

        [TestMethod]
        public void UsbAndNormalizedBluetoothReportsDecodeIdentically()
        {
            byte[] usbReport = new byte[64];
            byte[] normalizedBluetoothReport = new byte[64];
            usbReport[30] = 0x15;
            normalizedBluetoothReport[30] = 0x15;

            DS4BatteryStatusReading usb = DS4BatteryStatusDecoder.Decode(usbReport[30]);
            DS4BatteryStatusReading bluetooth =
                DS4BatteryStatusDecoder.Decode(normalizedBluetoothReport[30]);

            Assert.AreEqual(usb, bluetooth);
            Assert.AreEqual(DS4BatteryStatus.Charging, bluetooth.Status);
        }

        [TestMethod]
        public void CapacityRemainsAtLastValidValueWhenStatusHasNoCapacity()
        {
            DS4BatteryStatusReading valid = DS4BatteryStatusDecoder.Decode(0x15);
            DS4BatteryStatusReading unavailable = DS4BatteryStatusDecoder.Decode(0x1E);

            int battery = valid.Capacity;
            if (unavailable.HasCapacity)
            {
                battery = unavailable.Capacity;
            }

            Assert.AreEqual(55, battery);
            Assert.AreEqual(DS4BatteryStatus.ChargingUnavailable, unavailable.Status);
        }

        [TestMethod]
        public void OnBatteryPresentationAcceptsStableCapacityAfterSettling()
        {
            DS4BatteryPresentationStabilizer stabilizer = new();
            DateTime start = new(2026, 8, 16, 12, 0, 0, DateTimeKind.Utc);

            DS4BatteryPresentation initial = stabilizer.Update(
                DS4BatteryStatusDecoder.Decode(0x03), start);
            DS4BatteryPresentation settled = stabilizer.Update(
                DS4BatteryStatusDecoder.Decode(0x03), start.AddSeconds(3));

            Assert.IsTrue(initial.IsSettling);
            Assert.IsFalse(initial.HasCapacity);
            Assert.IsFalse(settled.IsSettling);
            Assert.IsTrue(settled.HasCapacity);
            Assert.AreEqual(35, settled.Capacity);
        }

        [TestMethod]
        public void ChargingPresentationHidesCapacity()
        {
            DS4BatteryPresentation presentation = new DS4BatteryPresentationStabilizer().Update(
                DS4BatteryStatusDecoder.Decode(0x17), DateTime.UtcNow);

            Assert.AreEqual(DS4BatteryStatus.Charging, presentation.Status);
            Assert.IsFalse(presentation.HasCapacity);
            Assert.IsFalse(presentation.IsSettling);
        }

        [TestMethod]
        public void FullAndChargingProblemsRemainExplicitPresentationStates()
        {
            DateTime now = DateTime.UtcNow;
            DS4BatteryPresentation full = new DS4BatteryPresentationStabilizer().Update(
                DS4BatteryStatusDecoder.Decode(0x1B), now);
            DS4BatteryPresentation unavailable = new DS4BatteryPresentationStabilizer().Update(
                DS4BatteryStatusDecoder.Decode(0x1E), now);
            DS4BatteryPresentation error = new DS4BatteryPresentationStabilizer().Update(
                DS4BatteryStatusDecoder.Decode(0x1F), now);

            Assert.AreEqual(DS4BatteryStatus.Full, full.Status);
            Assert.AreEqual(DS4BatteryStatus.ChargingUnavailable, unavailable.Status);
            Assert.AreEqual(DS4BatteryStatus.ChargingError, error.Status);
        }

        [TestMethod]
        public void CableRemovalSettlesBeforeExposingNormalCapacity()
        {
            DS4BatteryPresentationStabilizer stabilizer = new();
            DateTime start = new(2026, 8, 16, 12, 0, 0, DateTimeKind.Utc);
            stabilizer.Update(DS4BatteryStatusDecoder.Decode(0x1A), start);

            DS4BatteryPresentation transition = stabilizer.Update(
                DS4BatteryStatusDecoder.Decode(0x03), start.AddSeconds(1));
            DS4BatteryPresentation settled = stabilizer.Update(
                DS4BatteryStatusDecoder.Decode(0x02), start.AddSeconds(4));

            Assert.IsTrue(transition.IsSettling);
            Assert.IsFalse(transition.HasCapacity);
            Assert.IsTrue(settled.HasCapacity);
            Assert.AreEqual(25, settled.Capacity);
        }

        [TestMethod]
        public void CriticalBatteryIsNotSuppressedDuringSettling()
        {
            DS4BatteryPresentation presentation = new DS4BatteryPresentationStabilizer().Update(
                DS4BatteryStatusDecoder.Decode(0x00), DateTime.UtcNow);

            Assert.IsTrue(presentation.HasCapacity);
            Assert.IsFalse(presentation.IsSettling);
            Assert.AreEqual(5, presentation.Capacity);
        }

        [TestMethod]
        public void ResetDoesNotRetainPriorPresentationCapacity()
        {
            DS4BatteryPresentationStabilizer stabilizer = new();
            DateTime start = new(2026, 8, 16, 12, 0, 0, DateTimeKind.Utc);
            stabilizer.Update(DS4BatteryStatusDecoder.Decode(0x03), start);
            stabilizer.Update(DS4BatteryStatusDecoder.Decode(0x03), start.AddSeconds(3));

            stabilizer.Reset();
            DS4BatteryPresentation afterReset = stabilizer.Update(
                DS4BatteryStatusDecoder.Decode(0x03), start.AddSeconds(4));

            Assert.IsTrue(afterReset.IsSettling);
            Assert.IsFalse(afterReset.HasCapacity);
        }

        [TestMethod]
        public void PresentationStateDoesNotLeakBetweenControllers()
        {
            DS4BatteryPresentationStabilizer first = new();
            DS4BatteryPresentationStabilizer second = new();
            DateTime start = new(2026, 8, 16, 12, 0, 0, DateTimeKind.Utc);
            first.Update(DS4BatteryStatusDecoder.Decode(0x03), start);
            DS4BatteryPresentation firstSettled = first.Update(
                DS4BatteryStatusDecoder.Decode(0x03), start.AddSeconds(3));
            DS4BatteryPresentation secondInitial = second.Update(
                DS4BatteryStatusDecoder.Decode(0x03), start.AddSeconds(3));

            Assert.IsTrue(firstSettled.HasCapacity);
            Assert.IsTrue(secondInitial.IsSettling);
            Assert.IsFalse(secondInitial.HasCapacity);
        }
    }
}
