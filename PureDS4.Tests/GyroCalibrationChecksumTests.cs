using DS4Windows;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DS4WindowsTests;

/// <summary>
/// Factory gyro calibration arrives over Bluetooth with a checksum. The guard
/// that was supposed to reject a bad read could never fire — the loop condition
/// ended it before the check could be true — so calibration that failed its own
/// checksum was applied, and the failure was never reported.
/// </summary>
[TestClass]
public class GyroCalibrationChecksumTests
{
    private const int ReportLength = 41;
    private const int ChecksumPosition = ReportLength - 4;

    private static byte[] ReportWithValidChecksum()
    {
        var report = new byte[ReportLength];
        report[0] = 0x05;
        // Plausible bias and range values; the checksum is what is under test.
        for (int i = 1; i < ChecksumPosition; i++)
        {
            report[i] = (byte)(i * 3);
        }

        uint computed = ~Crc32Algorithm.Compute(new byte[] { 0xA3 });
        computed = ~Crc32Algorithm.CalculateBasicHash(ref computed, ref report, 0, ChecksumPosition);
        report[ChecksumPosition] = (byte)computed;
        report[ChecksumPosition + 1] = (byte)(computed >> 8);
        report[ChecksumPosition + 2] = (byte)(computed >> 16);
        report[ChecksumPosition + 3] = (byte)(computed >> 24);
        return report;
    }

    [TestMethod]
    public void AReportCarryingItsOwnChecksumIsAccepted()
    {
        Assert.IsTrue(DS4Device.HasValidCalibrationChecksum(ReportWithValidChecksum()));
    }

    [TestMethod]
    public void ACorruptedPayloadIsRejected()
    {
        byte[] report = ReportWithValidChecksum();
        report[7] ^= 0xFF;

        Assert.IsFalse(DS4Device.HasValidCalibrationChecksum(report),
            "Calibration whose payload changed in transit must not be applied.");
    }

    [TestMethod]
    public void ACorruptedChecksumIsRejected()
    {
        byte[] report = ReportWithValidChecksum();
        report[ChecksumPosition] ^= 0x01;

        Assert.IsFalse(DS4Device.HasValidCalibrationChecksum(report));
    }

    [TestMethod]
    public void AnEmptyOrShortReportIsRejected()
    {
        Assert.IsFalse(DS4Device.HasValidCalibrationChecksum(null));
        Assert.IsFalse(DS4Device.HasValidCalibrationChecksum(new byte[ReportLength - 1]));
        Assert.IsFalse(DS4Device.HasValidCalibrationChecksum(new byte[ReportLength]),
            "An all-zero read is a failed read, not calibration.");
    }

    [TestMethod]
    public void TheChecksumDependsOnThePayload()
    {
        // The slice-by-16 CRC table is filled lazily, and an uninitialised one
        // returns the seed for every input: two different payloads would then
        // share a checksum and any report would validate.
        var first = new byte[ReportLength];
        var second = new byte[ReportLength];
        second[7] = 0xFF;

        uint firstCrc = ~Crc32Algorithm.Compute(new byte[] { 0xA3 });
        firstCrc = ~Crc32Algorithm.CalculateBasicHash(ref firstCrc, ref first, 0, ChecksumPosition);
        uint secondCrc = ~Crc32Algorithm.Compute(new byte[] { 0xA3 });
        secondCrc = ~Crc32Algorithm.CalculateBasicHash(ref secondCrc, ref second, 0, ChecksumPosition);

        Assert.AreNotEqual(firstCrc, secondCrc,
            "A checksum that ignores its payload validates every report.");
    }

    [TestMethod]
    public void TheDeviceGivesUpAfterAFixedNumberOfAttempts()
    {
        Assert.AreEqual(5, DS4Device.CALIBRATION_READ_ATTEMPTS,
            "The retry count is what the failure message promises the person.");
    }
}
