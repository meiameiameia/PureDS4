using DS4Windows;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace DS4WindowsTests;

/// <summary>
/// The measurement behind the diagnostics screen. A Bluetooth link that stalls
/// for 20 ms once a second still averages out to a healthy-looking latency, so
/// these checks are about what the average hides: the worst gaps, how often
/// they happen, and how many reports never arrived.
/// </summary>
[TestClass]
public class InputReportStatisticsTests
{
    private static InputReportStatistics WithSteadyStream(double intervalMs, int reports)
    {
        var statistics = new InputReportStatistics();
        for (int i = 0; i < reports; i++)
        {
            statistics.Add(intervalMs, intervalMs);
        }

        return statistics;
    }

    [TestMethod]
    public void NoReportsMeasureNothing()
    {
        InputReportStatisticsSnapshot snapshot = new InputReportStatistics().Snapshot();

        Assert.IsFalse(snapshot.HasData);
        Assert.AreEqual(0, snapshot.SampleCount);
        Assert.AreEqual(0, snapshot.RateHz);
    }

    [TestMethod]
    public void SteadyStreamReportsItsRealCadenceAndRate()
    {
        // 4 ms apart is the DS4's 250 Hz default over Bluetooth.
        InputReportStatisticsSnapshot snapshot = WithSteadyStream(4.0, 500).Snapshot();

        Assert.AreEqual(500, snapshot.SampleCount);
        Assert.AreEqual(4.0, snapshot.TypicalIntervalMs, 0.001);
        Assert.AreEqual(250.0, snapshot.RateHz, 0.5);
        Assert.AreEqual(2.0, snapshot.WindowSeconds, 0.01);
        Assert.AreEqual(0, snapshot.StallCount);
        Assert.AreEqual(0, snapshot.EstimatedLostReports);
        Assert.AreEqual(0, snapshot.LostFraction, 0.0001);
    }

    [TestMethod]
    public void PeriodicStallIsCountedAndItsLostReportsEstimated()
    {
        // The measured Bluetooth fault: a 20 ms hole roughly once a second,
        // which is five cadences, so four reports never arrived each time.
        var statistics = new InputReportStatistics();
        for (int second = 0; second < 4; second++)
        {
            for (int i = 0; i < 245; i++)
            {
                statistics.Add(4.0, 4.0);
            }

            statistics.Add(20.0, 20.0);
        }

        InputReportStatisticsSnapshot snapshot = statistics.Snapshot();

        Assert.AreEqual(4.0, snapshot.TypicalIntervalMs, 0.001, "The cadence is still 4 ms.");
        Assert.AreEqual(20.0, snapshot.WorstIntervalMs, 0.001);
        Assert.AreEqual(4, snapshot.StallCount, "One stall per second.");
        Assert.AreEqual(16, snapshot.EstimatedLostReports, "Four reports lost per stall.");
        Assert.IsTrue(snapshot.LostFraction > 0.015 && snapshot.LostFraction < 0.02,
            "About 1.6% of reports went missing, not a rounding error: " + snapshot.LostFraction);
    }

    [TestMethod]
    public void OrdinaryJitterIsNotReportedAsLoss()
    {
        var statistics = new InputReportStatistics();
        for (int i = 0; i < 300; i++)
        {
            // Alternating 3.7/4.4 ms is the DS4's own timestamp granularity.
            statistics.Add(i % 2 == 0 ? 3.7 : 4.4, 4.0);
        }

        InputReportStatisticsSnapshot snapshot = statistics.Snapshot();

        Assert.AreEqual(0, snapshot.StallCount);
        Assert.AreEqual(0, snapshot.EstimatedLostReports);
    }

    [TestMethod]
    public void PercentilesSeparateTheTypicalCaseFromTheTail()
    {
        var statistics = new InputReportStatistics();
        for (int i = 0; i < 990; i++)
        {
            statistics.Add(4.0, 4.0);
        }

        for (int i = 0; i < 10; i++)
        {
            statistics.Add(30.0, 30.0);
        }

        InputReportStatisticsSnapshot snapshot = statistics.Snapshot();

        Assert.AreEqual(4.0, snapshot.TypicalIntervalMs, 0.001);
        Assert.AreEqual(4.0, snapshot.P95IntervalMs, 0.001, "95% of reports are on cadence.");
        Assert.IsTrue(snapshot.P99IntervalMs > 4.0, "The worst 1% must not be averaged away.");
        Assert.AreEqual(30.0, snapshot.WorstIntervalMs, 0.001);
    }

    [TestMethod]
    public void HostDeliveryIsMeasuredSeparatelyFromTheController()
    {
        // The controller sends on cadence while Windows delivers in bursts.
        var statistics = new InputReportStatistics();
        for (int i = 0; i < 400; i++)
        {
            statistics.Add(4.0, i % 4 == 0 ? 16.0 : 0.2);
        }

        InputReportStatisticsSnapshot snapshot = statistics.Snapshot();

        Assert.AreEqual(4.0, snapshot.TypicalIntervalMs, 0.001);
        Assert.AreEqual(0, snapshot.StallCount, "The controller itself never stalled.");
        Assert.AreEqual(16.0, snapshot.HostWorstIntervalMs, 0.001,
            "The delay Windows added still has to be visible.");
    }

    [TestMethod]
    public void UnusableIntervalsAreIgnored()
    {
        var statistics = new InputReportStatistics();
        statistics.Add(0, 4.0);
        statistics.Add(-1, 4.0);
        statistics.Add(double.NaN, 4.0);
        statistics.Add(double.PositiveInfinity, 4.0);

        Assert.IsFalse(statistics.Snapshot().HasData,
            "A duplicate or restarted timestamp measures nothing.");

        statistics.Add(4.0, double.NaN);
        Assert.AreEqual(1, statistics.Snapshot().SampleCount,
            "A usable controller interval still counts when the host one is not.");
    }

    [TestMethod]
    public void TheWindowKeepsTheMostRecentReportsOnly()
    {
        var statistics = new InputReportStatistics();
        for (int i = 0; i < InputReportStatistics.Capacity; i++)
        {
            statistics.Add(30.0, 30.0);
        }

        for (int i = 0; i < InputReportStatistics.Capacity; i++)
        {
            statistics.Add(4.0, 4.0);
        }

        InputReportStatisticsSnapshot snapshot = statistics.Snapshot();

        Assert.AreEqual(InputReportStatistics.Capacity, snapshot.SampleCount);
        Assert.AreEqual(4.0, snapshot.TypicalIntervalMs, 0.001,
            "A connection that recovered must stop being judged by its past.");
        Assert.AreEqual(4.0, snapshot.WorstIntervalMs, 0.001);
    }

    [TestMethod]
    public void ResetClearsTheWindow()
    {
        InputReportStatistics statistics = WithSteadyStream(4.0, 100);
        Assert.IsTrue(statistics.Snapshot().HasData);

        statistics.Reset();

        Assert.IsFalse(statistics.Snapshot().HasData);
    }

    [TestMethod]
    public void PercentileInterpolatesBetweenSamples()
    {
        double[] sorted = { 1.0, 2.0, 3.0, 4.0 };

        Assert.AreEqual(1.0, InputReportStatistics.Percentile(sorted, 0), 0.0001);
        Assert.AreEqual(2.5, InputReportStatistics.Percentile(sorted, 0.5), 0.0001);
        Assert.AreEqual(4.0, InputReportStatistics.Percentile(sorted, 1.0), 0.0001);
        Assert.AreEqual(0, InputReportStatistics.Percentile(Array.Empty<double>(), 0.5), 0.0001);
    }
}
