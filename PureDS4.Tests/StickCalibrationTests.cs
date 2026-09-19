using DS4Windows;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DS4WindowsTests;

/// <summary>
/// Correcting a stick that rests off centre must not cost the player any of
/// their travel, and the correction must be measured rather than snapped from
/// one instant. Both were wrong: the old correction shifted the axis and
/// clamped it, and the calibration window saved a single sample.
/// </summary>
[TestClass]
public class StickCalibrationTests
{
    [TestMethod]
    public void NoDriftLeavesEveryReadingAlone()
    {
        for (int raw = 0; raw <= 255; raw++)
        {
            Assert.AreEqual((byte)raw, StickCalibration.Correct((byte)raw, 0));
        }
    }

    [TestMethod]
    public void TheMeasuredRestingPointBecomesCentre()
    {
        Assert.AreEqual(128, StickCalibration.Correct(131, 3));
        Assert.AreEqual(128, StickCalibration.Correct(125, -3));
    }

    [TestMethod]
    public void BothEndsStayReachable()
    {
        // The defect: with the old shift-and-clamp, a drift of +3 made 255
        // unreachable on one side and 0 arrive early on the other.
        foreach (int drift in new[] { -8, -3, -1, 1, 3, 8 })
        {
            Assert.AreEqual(0, StickCalibration.Correct(0, drift),
                "Full travel one way must survive a drift of " + drift);
            Assert.AreEqual(255, StickCalibration.Correct(255, drift),
                "Full travel the other way must survive a drift of " + drift);
        }
    }

    [TestMethod]
    public void CorrectionStaysMonotonic()
    {
        foreach (int drift in new[] { -6, -2, 2, 6 })
        {
            int previous = -1;
            for (int raw = 0; raw <= 255; raw++)
            {
                int corrected = StickCalibration.Correct((byte)raw, drift);
                Assert.IsTrue(corrected >= previous,
                    $"Pushing the stick further must never move the output back (drift {drift}, raw {raw}).");
                previous = corrected;
            }
        }
    }

    [TestMethod]
    public void AnUnusableRestingPointChangesNothing()
    {
        // A stick reported as resting at an extreme is a broken measurement,
        // not a calibration: distorting the whole axis would be worse.
        Assert.AreEqual(200, StickCalibration.Correct(200, -128));
        Assert.AreEqual(50, StickCalibration.Correct(50, 127));
    }

    [TestMethod]
    public void AMeasurementNeedsEnoughSteadyReadings()
    {
        var measurement = new StickCalibration.CentreMeasurement();
        for (int i = 0; i < StickCalibration.CentreMeasurement.RequiredSamples - 1; i++)
        {
            measurement.Add(131, 126);
        }

        Assert.IsFalse(measurement.IsSteady, "One sample short is not a measurement.");
        StringAssert.Contains(measurement.Describe(), "Measuring");

        measurement.Add(131, 126);
        Assert.IsTrue(measurement.IsSteady);
        Assert.AreEqual(3, measurement.DriftX);
        Assert.AreEqual(-2, measurement.DriftY);
    }

    [TestMethod]
    public void AStickThatMovedIsRejected()
    {
        var measurement = new StickCalibration.CentreMeasurement();
        for (int i = 0; i < 80; i++)
        {
            measurement.Add(128, 128);
        }

        measurement.Add(190, 128);

        Assert.IsFalse(measurement.IsSteady, "A shove during measuring must not be averaged in.");
        StringAssert.Contains(measurement.Describe(), "moved while measuring");
    }

    [TestMethod]
    public void NoiseIsAveragedRatherThanSnapped()
    {
        // The old window would store whichever of these arrived at the instant
        // Save was pressed.
        var measurement = new StickCalibration.CentreMeasurement();
        for (int i = 0; i < 100; i++)
        {
            measurement.Add((byte)(i % 2 == 0 ? 130 : 131), 128);
        }

        Assert.IsTrue(measurement.IsSteady);
        Assert.AreEqual(3, measurement.DriftX, "130.5 rounds to 131, three from centre.");
        Assert.AreEqual(0, measurement.DriftY);
    }

    [TestMethod]
    public void ACentredStickIsToldItNeedsNothing()
    {
        var measurement = new StickCalibration.CentreMeasurement();
        for (int i = 0; i < 90; i++)
        {
            measurement.Add(128, 128);
        }

        Assert.IsTrue(measurement.IsSteady);
        Assert.IsFalse(measurement.NeedsCorrection);
        StringAssert.Contains(measurement.Describe(), "already rests at centre");
    }

    [TestMethod]
    public void ResetStartsTheMeasurementOver()
    {
        var measurement = new StickCalibration.CentreMeasurement();
        for (int i = 0; i < 90; i++)
        {
            measurement.Add(140, 140);
        }

        measurement.Reset();

        Assert.AreEqual(0, measurement.Count);
        Assert.IsFalse(measurement.IsSteady);
    }

    [TestMethod]
    public void CalibrationIsAppliedBeforeFuzzAndAntiSnapback()
    {
        // Fuzz and anti-snapback judge how far a stick moved, so they have to
        // see corrected values. This guards the ordering the defect had wrong.
        string mapping = System.IO.File.ReadAllText(SourcePath("DS4Control/Mapping.cs"));
        int calibration = mapping.IndexOf("ApplyStickCalibration(device", System.StringComparison.Ordinal);
        int antiSnapback = mapping.IndexOf("CalcAntiSnapbackStick(device", System.StringComparison.Ordinal);
        int fuzz = mapping.IndexOf("CalcStickAxisFuzz(device", System.StringComparison.Ordinal);

        Assert.IsTrue(calibration > 0 && antiSnapback > 0 && fuzz > 0);
        Assert.IsTrue(calibration < antiSnapback,
            "Anti-snapback must judge corrected values.");
        Assert.IsTrue(calibration < fuzz, "Fuzz must judge corrected values.");
    }

    private static string SourcePath(string relative)
    {
        var directory = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory);
        while (directory != null &&
            !System.IO.File.Exists(System.IO.Path.Combine(directory.FullName, "PureDS4.sln")))
        {
            directory = directory.Parent;
        }

        Assert.IsNotNull(directory);
        return System.IO.Path.Combine(directory.FullName, "PureDS4", relative);
    }
}
