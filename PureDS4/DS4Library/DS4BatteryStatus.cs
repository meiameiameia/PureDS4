using System;

namespace DS4Windows
{
    internal enum DS4BatteryStatus
    {
        OnBattery,
        Charging,
        Full,
        ChargingUnavailable,
        ChargingError,
    }

    internal readonly record struct DS4BatteryStatusReading(bool CableConnected,
        DS4BatteryStatus Status, int Capacity, bool HasCapacity);

    internal readonly record struct DS4BatteryPresentation(
        DS4BatteryStatus Status, int Capacity, bool HasCapacity, bool IsSettling);

    internal sealed class DS4BatteryPresentationStabilizer
    {
        private static readonly TimeSpan OnBatterySettlingWindow =
            TimeSpan.FromSeconds(3);
        private const int CriticalBatteryCapacity = 15;

        private bool hasPreviousStatus;
        private DS4BatteryStatus previousStatus;
        private DateTime onBatterySettlingUntilUtc;

        public DS4BatteryPresentation Current { get; private set; }

        public DS4BatteryPresentation Update(DS4BatteryStatusReading reading,
            DateTime timestampUtc)
        {
            if (reading.Status != DS4BatteryStatus.OnBattery)
            {
                hasPreviousStatus = true;
                previousStatus = reading.Status;
                Current = new DS4BatteryPresentation(reading.Status, 0, false, false);
                return Current;
            }

            bool enteringOnBattery = !hasPreviousStatus ||
                previousStatus != DS4BatteryStatus.OnBattery;
            hasPreviousStatus = true;
            previousStatus = DS4BatteryStatus.OnBattery;
            if (enteringOnBattery)
            {
                onBatterySettlingUntilUtc = timestampUtc + OnBatterySettlingWindow;
            }

            if (!reading.HasCapacity)
            {
                Current = new DS4BatteryPresentation(reading.Status, 0, false, false);
                return Current;
            }

            bool criticalBattery = reading.Capacity <= CriticalBatteryCapacity;
            if (criticalBattery || timestampUtc >= onBatterySettlingUntilUtc)
            {
                Current = new DS4BatteryPresentation(reading.Status,
                    reading.Capacity, true, false);
                return Current;
            }

            Current = new DS4BatteryPresentation(reading.Status, 0, false, true);
            return Current;
        }

        public void Reset()
        {
            hasPreviousStatus = false;
            previousStatus = default;
            onBatterySettlingUntilUtc = default;
            Current = default;
        }
    }

    internal static class DS4BatteryStatusDecoder
    {
        private const byte CableConnectedMask = 0x10;
        private const byte CapacityMask = 0x0F;
        private const byte FullCapacityCode = 10;
        private const byte FullStatusCode = 11;
        private const byte ChargingUnavailableCode = 14;
        private const byte ChargingErrorCode = 15;

        public static DS4BatteryStatusReading Decode(byte statusByte)
        {
            bool cableConnected = (statusByte & CableConnectedMask) != 0;
            byte capacityCode = (byte)(statusByte & CapacityMask);

            if (!cableConnected)
            {
                return capacityCode < FullCapacityCode
                    ? WithCapacity(false, DS4BatteryStatus.OnBattery, capacityCode)
                    : new DS4BatteryStatusReading(false, DS4BatteryStatus.OnBattery,
                        100, true);
            }

            return capacityCode switch
            {
                < FullCapacityCode => WithCapacity(true, DS4BatteryStatus.Charging,
                    capacityCode),
                FullCapacityCode => new DS4BatteryStatusReading(true,
                    DS4BatteryStatus.Charging, 100, true),
                FullStatusCode => new DS4BatteryStatusReading(true,
                    DS4BatteryStatus.Full, 100, true),
                ChargingUnavailableCode => new DS4BatteryStatusReading(true,
                    DS4BatteryStatus.ChargingUnavailable, 0, false),
                ChargingErrorCode => new DS4BatteryStatusReading(true,
                    DS4BatteryStatus.ChargingError, 0, false),
                _ => new DS4BatteryStatusReading(true,
                    DS4BatteryStatus.ChargingUnavailable, 0, false),
            };
        }

        private static DS4BatteryStatusReading WithCapacity(bool cableConnected,
            DS4BatteryStatus status, byte capacityCode)
        {
            // The controller exposes ten-percent ranges, not a precise state of charge.
            return new DS4BatteryStatusReading(cableConnected, status,
                capacityCode * 10 + 5, true);
        }
    }
}
