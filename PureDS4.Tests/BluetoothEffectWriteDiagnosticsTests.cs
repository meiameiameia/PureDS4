using DS4Windows;

namespace DS4WindowsTests;

[TestClass]
public class BluetoothEffectWriteDiagnosticsTests
{
    [TestMethod]
    public void FirstAnomalyIsNotHiddenByRecentHealthySummary()
    {
        var diagnostics = new BluetoothEffectWriteDiagnostics();
        Assert.IsNull(diagnostics.Record(1000, 1, true, 0, false));
        Assert.IsNotNull(diagnostics.Record(31_000, 1, true, 0, false));
        var failure = diagnostics.Record(31_001, 1, false, 1167, false).Value;
        Assert.AreEqual(1L, failure.Failures);
        Assert.AreEqual(1167, failure.LastWin32Error);
        Assert.IsNull(diagnostics.Record(31_002, 1, false, 1167, false));
    }

    [TestMethod]
    public void HealthyWritesAreAggregatedWithoutPerPacketMessages()
    {
        var diagnostics = new BluetoothEffectWriteDiagnostics();
        for (int i = 0; i < 1000; i++)
            Assert.IsNull(diagnostics.Record(1000 + i, 1, true, 0, false));
        var summary = diagnostics.Record(31_000, 2, true, 0, false).Value;
        Assert.AreEqual(1001L, summary.Attempts);
        Assert.AreEqual(0L, summary.Failures);
        Assert.AreEqual(2d, summary.MaxMs);
    }

    [TestMethod]
    public void FirstFailureIsImmediateAndFurtherFailuresAreRateLimited()
    {
        var diagnostics = new BluetoothEffectWriteDiagnostics();
        var first = diagnostics.Record(10_000, 3000, false, 1167, false).Value;
        Assert.AreEqual(1L, first.Failures);
        Assert.AreEqual(1L, first.SlowWrites);
        Assert.AreEqual(1167, first.LastWin32Error);
        for (int i = 1; i < 1000; i++)
            Assert.IsNull(diagnostics.Record(10_000 + i, 3, false, 2, false));
        var next = diagnostics.Record(40_000, 1, true, 0, false).Value;
        Assert.AreEqual(1000L, next.Attempts);
        Assert.AreEqual(999L, next.Failures);
        Assert.AreEqual(2, next.LastWin32Error);
        Assert.AreEqual(0L, next.SlowWrites);
        Assert.AreEqual(3d, next.MaxMs);
    }

    [TestMethod]
    public void SlowSuccessfulWriteAndExceptionAreDistinguished()
    {
        var diagnostics = new BluetoothEffectWriteDiagnostics();
        var slow = diagnostics.Record(1000, 100, true, 0, false).Value;
        Assert.AreEqual(1L, slow.SlowWrites);
        Assert.AreEqual(0L, slow.Failures);
        Assert.IsNull(diagnostics.Record(2000, 5, false, 999, true));
        var exception = diagnostics.Record(31_000, 1, true, 0, false).Value;
        Assert.AreEqual(1L, exception.Exceptions);
        Assert.AreEqual(1L, exception.Failures);
        Assert.AreEqual(0, exception.LastWin32Error,
            "A managed exception must not claim an unrelated stale native error.");
    }
}
