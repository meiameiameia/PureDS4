using System;

namespace DS4Windows
{
    // Owned by DS4Device's output-report lock; no background worker or HID
    // access. Measures effect submission, including lock/driver wait time,
    // not radio latency. The slow threshold is diagnostic, not a timeout.
    internal sealed class BluetoothEffectWriteDiagnostics
    {
        internal const long SummaryIntervalMs = 30_000;
        internal const double SlowWriteMs = 100;
        private bool started, reported, anomalyReported;
        private long windowStart, lastReport;
        private long attempts, failures, slowWrites, exceptions;
        private double maxMs;
        private int lastError;

        internal readonly record struct Summary(long WindowMs, long Attempts,
            long Failures, long SlowWrites, long Exceptions, double MaxMs,
            int LastWin32Error)
        {
            public override string ToString() => FormattableString.Invariant(
                $"windowMs={WindowMs} writes={Attempts} failures={Failures} slowWrites(>=100ms)={SlowWrites} exceptions={Exceptions} maxWriteMs={MaxMs:F2} lastFailedWin32={LastWin32Error}");
        }

        internal Summary? Record(long nowMs, double elapsedMs, bool success,
            int win32Error, bool threw)
        {
            if (!started)
            {
                started = true;
                windowStart = nowMs - (long)Math.Ceiling(elapsedMs);
            }
            attempts++;
            bool failed = !success || threw;
            if (failed)
            {
                failures++;
                lastError = threw ? 0 : win32Error;
            }
            if (threw) exceptions++;
            bool slow = elapsedMs >= SlowWriteMs;
            if (slow) slowWrites++;
            maxMs = Math.Max(maxMs, elapsedMs);

            // Do not hide the first anomaly behind a recent healthy summary:
            // a failing connection may never produce another output pass.
            // Subsequent summaries, including repeated errors, are rate-limited.
            bool firstAnomaly = (failed || slow) && !anomalyReported;
            if (firstAnomaly) anomalyReported = true;
            if (!firstAnomaly && (reported ?
                nowMs - lastReport < SummaryIntervalMs :
                nowMs - windowStart < SummaryIntervalMs))
                return null;

            var summary = new Summary(Math.Max(0, nowMs - windowStart),
                attempts, failures, slowWrites, exceptions, maxMs, lastError);
            reported = true;
            lastReport = windowStart = nowMs;
            attempts = failures = slowWrites = exceptions = 0;
            maxMs = 0;
            lastError = 0;
            return summary;
        }
    }
}
