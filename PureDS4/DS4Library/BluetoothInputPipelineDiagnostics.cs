using System;

namespace DS4Windows
{
    internal enum BluetoothInputStage { Read, Processing, Dispatch, Feedback }

    // Completion statistics have one input-thread owner. The separate activity
    // snapshot is safe for a diagnostic observer; neither path reads input contents.
    // Keep incident counts for the connection even after the rolling report
    // statistics window expires. Timings describe host work, not radio latency.
    internal sealed class BluetoothInputPipelineDiagnostics
    {
        internal const double SlowStageMs = 100;
        internal const long ReportIntervalMs = 30_000;
        private double readMs, processingMs, dispatchMs, feedbackMs;
        private HidDevice.ReadStatus readStatus;
        private int nativeError, reportedStages;
        private long slowStages, readTimeouts, readErrors, lastReport;
        internal PipelineActivity Activity { get; } = new();

        internal readonly record struct Summary(BluetoothInputStage Stage,
            double ReadMs, double ProcessingMs, double DispatchMs,
            double FeedbackMs, HidDevice.ReadStatus ReadStatus, int NativeError,
            long SlowStages, long ReadTimeouts, long ReadErrors)
        {
            public override string ToString() => FormattableString.Invariant(
                $"stage={Stage} readMs={ReadMs:F2} processingMs={ProcessingMs:F2} dispatchMs={DispatchMs:F2} feedbackMs={FeedbackMs:F2} readStatus={ReadStatus} nativeError={NativeError} slowStages={SlowStages} readTimeouts={ReadTimeouts} readErrors={ReadErrors}");
        }

        internal Summary? Record(BluetoothInputStage stage, long nowMs,
            double elapsedMs, HidDevice.ReadStatus status = HidDevice.ReadStatus.Success,
            int win32Error = 0)
        {
            if (!double.IsFinite(elapsedMs) || elapsedMs < 0)
            {
                return null;
            }

            switch (stage)
            {
                case BluetoothInputStage.Read:
                    readMs = elapsedMs;
                    readStatus = status;
                    nativeError = status == HidDevice.ReadStatus.Success ? 0 : win32Error;
                    if (status == HidDevice.ReadStatus.WaitTimedOut) readTimeouts++;
                    else if (status != HidDevice.ReadStatus.Success) readErrors++;
                    break;
                case BluetoothInputStage.Processing: processingMs = elapsedMs; break;
                case BluetoothInputStage.Dispatch: dispatchMs = elapsedMs; break;
                case BluetoothInputStage.Feedback: feedbackMs = elapsedMs; break;
                default: return null;
            }

            bool slow = elapsedMs >= SlowStageMs;
            if (slow) slowStages++;
            bool failed = stage == BluetoothInputStage.Read &&
                status != HidDevice.ReadStatus.Success;
            if (!slow && !failed)
            {
                return null;
            }

            // A different stage's first incident must not be hidden behind a
            // recent read timeout. Repeated incidents are bounded, not logged
            // per report; counters still include the suppressed incidents.
            int stageBit = 1 << (int)stage;
            if ((reportedStages & stageBit) != 0 &&
                nowMs - lastReport < ReportIntervalMs)
            {
                return null;
            }

            reportedStages |= stageBit;
            lastReport = nowMs;
            return new Summary(stage, readMs, processingMs, dispatchMs,
                feedbackMs, readStatus, nativeError, slowStages,
                readTimeouts, readErrors);
        }
    }
}
