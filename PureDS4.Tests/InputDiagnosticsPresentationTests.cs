using DS4Windows;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DS4WindowsTests;

/// <summary>
/// What the diagnostics screen tells a person. A verdict has to be earned by
/// enough samples, name the cost in reports rather than in statistics, and not
/// blame the controller when the PC is the late part.
/// </summary>
[TestClass]
public class InputDiagnosticsPresentationTests
{
    private static InputReportStatisticsSnapshot Stream(double intervalMs, int reports,
        double stallMs = 0, int stallsPerRun = 0)
    {
        var statistics = new InputReportStatistics();
        for (int i = 0; i < reports; i++)
        {
            statistics.Add(intervalMs, intervalMs);
        }

        for (int i = 0; i < stallsPerRun; i++)
        {
            statistics.Add(stallMs, stallMs);
        }

        return statistics.Snapshot();
    }

    [TestMethod]
    public void AVerdictWaitsForEnoughReports()
    {
        InputReportStatisticsSnapshot brief = Stream(4.0, 20);

        Assert.AreEqual(InputHealthVerdict.Unknown, InputDiagnosticsPresentation.Judge(brief));
        StringAssert.Contains(InputDiagnosticsPresentation.Explain(brief, wireless: true),
            "Measuring");
    }

    [TestMethod]
    public void NoReportsSaysSoPlainly()
    {
        InputReportStatisticsSnapshot empty = new InputReportStatistics().Snapshot();

        Assert.AreEqual(InputHealthVerdict.Unknown, InputDiagnosticsPresentation.Judge(empty));
        StringAssert.Contains(InputDiagnosticsPresentation.Explain(empty, wireless: false),
            "Waiting");
        Assert.AreEqual("—", InputDiagnosticsPresentation.FormatRate(empty.RateHz));
        Assert.AreEqual("—", InputDiagnosticsPresentation.FormatLoss(empty));
    }

    [TestMethod]
    public void AnUninterruptedStreamIsCalledSteady()
    {
        InputReportStatisticsSnapshot healthy = Stream(4.0, 1000);

        Assert.AreEqual(InputHealthVerdict.Steady, InputDiagnosticsPresentation.Judge(healthy));
        Assert.AreEqual("Steady", InputDiagnosticsPresentation.VerdictLabel(InputHealthVerdict.Steady));
        StringAssert.Contains(InputDiagnosticsPresentation.Explain(healthy, wireless: true),
            "on time");
    }

    [TestMethod]
    public void TheMeasuredBluetoothFaultReadsAsFrequentStalls()
    {
        // The real capture: a 20 ms hole about once a second at 250 Hz.
        var statistics = new InputReportStatistics();
        for (int second = 0; second < 8; second++)
        {
            for (int i = 0; i < 245; i++)
            {
                statistics.Add(4.0, 4.0);
            }

            statistics.Add(20.0, 20.0);
        }

        InputReportStatisticsSnapshot snapshot = statistics.Snapshot();
        Assert.AreEqual(InputHealthVerdict.Frequent, InputDiagnosticsPresentation.Judge(snapshot));

        string explanation = InputDiagnosticsPresentation.Explain(snapshot, wireless: true);
        StringAssert.Contains(explanation, "never arrived");
        StringAssert.Contains(explanation, "20 ms");
        StringAssert.Contains(explanation, "Bluetooth adapter",
            "A wireless link should point at the likely cause.");
    }

    [TestMethod]
    public void ACabledFaultBlamesTheCableRatherThanTheAir()
    {
        var statistics = new InputReportStatistics();
        for (int i = 0; i < 900; i++)
        {
            statistics.Add(4.0, 4.0);
        }

        for (int i = 0; i < 20; i++)
        {
            statistics.Add(40.0, 40.0);
        }

        string explanation = InputDiagnosticsPresentation.Explain(statistics.Snapshot(),
            wireless: false);

        StringAssert.Contains(explanation, "cable");
        Assert.IsFalse(explanation.Contains("Bluetooth"));
    }

    [TestMethod]
    public void ARareHitchIsOccasionalRatherThanFrequent()
    {
        var statistics = new InputReportStatistics();
        for (int i = 0; i < 3000; i++)
        {
            statistics.Add(4.0, 4.0);
        }

        statistics.Add(12.0, 12.0);

        Assert.AreEqual(InputHealthVerdict.Occasional,
            InputDiagnosticsPresentation.Judge(statistics.Snapshot()));
    }

    [TestMethod]
    public void WindowsBeingLateIsNotBlamedOnTheController()
    {
        var statistics = new InputReportStatistics();
        for (int i = 0; i < 400; i++)
        {
            statistics.Add(4.0, i % 4 == 0 ? 24.0 : 0.2);
        }

        InputReportStatisticsSnapshot snapshot = statistics.Snapshot();

        Assert.AreEqual(InputHealthVerdict.Steady, InputDiagnosticsPresentation.Judge(snapshot),
            "The controller itself kept cadence.");
        Assert.IsTrue(InputDiagnosticsPresentation.HostDeliveryIsWorse(snapshot),
            "The screen still has to show that the PC delivered late.");
    }

    [TestMethod]
    public void SteadyDeliveryIsNotFlaggedAsAHostProblem()
    {
        Assert.IsFalse(InputDiagnosticsPresentation.HostDeliveryIsWorse(Stream(4.0, 500)));
    }

    [TestMethod]
    public void MeasuresAreFormattedWithTheirUnits()
    {
        InputReportStatisticsSnapshot snapshot = Stream(4.0, 500);

        StringAssert.Contains(InputDiagnosticsPresentation.FormatRate(snapshot.RateHz), "Hz");
        StringAssert.Contains(InputDiagnosticsPresentation.FormatInterval(snapshot.TypicalIntervalMs), "ms");
        StringAssert.Contains(InputDiagnosticsPresentation.FormatWindow(snapshot), "reports over");
        Assert.AreEqual("—", InputDiagnosticsPresentation.FormatInterval(0));
    }
}
