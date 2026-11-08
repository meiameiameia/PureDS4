using System;
using System.Diagnostics;
using System.Threading;

namespace DS4Windows
{
    internal enum PipelineStage
    {
        Read, Processing, Dispatch, Feedback, PacketHandling, Callbacks, ViiperWriter
    }

    internal readonly record struct PipelineProgressSnapshot(
        PipelineStage Stage, long Identity, long StartedAt = 0);

    // A single producer publishes its current activity. Sampling never takes
    // a HID, mapping, audio, output or UI lock, and never waits for a producer.
    internal sealed class PipelineActivity
    {
        private long revision, startedAt;
        private int stage;

        internal long Begin(PipelineStage value)
        {
            long started = Math.Max(1, Stopwatch.GetTimestamp());
            Interlocked.Increment(ref revision);
            Volatile.Write(ref stage, (int)value);
            Volatile.Write(ref startedAt, started);
            Interlocked.Increment(ref revision);
            return started;
        }

        internal void End(PipelineStage value, long started)
        {
            if (Volatile.Read(ref startedAt) != started ||
                Volatile.Read(ref stage) != (int)value) return;
            Interlocked.Increment(ref revision);
            Volatile.Write(ref startedAt, 0);
            Interlocked.Increment(ref revision);
        }

        internal PipelineProgressSnapshot? Snapshot()
        {
            long before = Volatile.Read(ref revision);
            if ((before & 1) != 0) return null;
            var value = (PipelineStage)Volatile.Read(ref stage);
            long started = Volatile.Read(ref startedAt);
            if (before != Volatile.Read(ref revision)) return null;
            return new PipelineProgressSnapshot(value, started == 0 ? 0 : before,
                started);
        }

        internal Scope Watch(PipelineStage value) => new(this, value);

        internal readonly struct Scope : IDisposable
        {
            private readonly PipelineActivity owner;
            private readonly PipelineStage stage;
            private readonly long started;

            internal Scope(PipelineActivity owner, PipelineStage stage)
            {
                this.owner = owner;
                this.stage = stage;
                started = owner.Begin(stage);
            }

            public void Dispose() => owner?.End(stage, started);
        }
    }

    // Diagnostic only: the independent pool timer observes progress but cannot
    // cancel, reset, restart, neutralize or otherwise change either pipeline.
    internal sealed class PipelineIncidentMonitor : IDisposable
    {
        internal const int PollMilliseconds = 250;
        internal const int StallMilliseconds = 1_000;
        internal const int RepeatMilliseconds = 30_000;
        private static long nextSession;
        private readonly Func<PipelineProgressSnapshot?> snapshot;
        private readonly Action<Incident> report;
        private readonly long[] lastIncident = new long[7];
        private readonly long session = Interlocked.Increment(ref nextSession);
        private Timer timer;
        private int disposed, sampling;
        private PipelineStage trackedStage;
        private long trackedIdentity, trackedSince;
        private bool reported;

        internal readonly record struct Incident(long Session, PipelineStage Stage,
            bool Recovered, double ObservedMilliseconds)
        {
            // Recovery is observed at polling resolution, not an exact duration
            // or a claim that the game received the resumed input.
            public override string ToString() => FormattableString.Invariant(
                $"session={Session} stage={Stage} event={(Recovered ? "progress-observed" : "in-flight")} observedMs={ObservedMilliseconds:F0}");
        }

        internal PipelineIncidentMonitor(Func<PipelineProgressSnapshot?> snapshot,
            Action<Incident> report)
        {
            this.snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            this.report = report ?? throw new ArgumentNullException(nameof(report));
        }

        internal void Start()
        {
            if (Volatile.Read(ref disposed) != 0) return;
            var candidate = new Timer(_ => Sample(Stopwatch.GetTimestamp()), null,
                PollMilliseconds, PollMilliseconds);
            if (Interlocked.CompareExchange(ref timer, candidate, null) != null)
                candidate.Dispose();
            if (Volatile.Read(ref disposed) != 0)
                Interlocked.Exchange(ref timer, null)?.Dispose();
        }

        internal void Sample(long now)
        {
            if (Volatile.Read(ref disposed) != 0 ||
                Interlocked.CompareExchange(ref sampling, 1, 0) != 0) return;
            try
            {
                if (Volatile.Read(ref disposed) != 0) return;
                var current = snapshot();
                // A raced publication is not evidence of progress or failure.
                if (!current.HasValue) return;
                var value = current.Value;
                if (value.Identity != trackedIdentity || value.Stage != trackedStage)
                {
                    if (reported)
                        Emit(new Incident(session, trackedStage, true,
                            ElapsedMilliseconds(trackedSince, now)));
                    reported = false;
                    trackedIdentity = value.Identity;
                    trackedStage = value.Stage;
                    trackedSince = value.StartedAt > 0 ? value.StartedAt : now;
                }
                if (trackedIdentity == 0 || reported || now < trackedSince) return;
                double elapsed = ElapsedMilliseconds(trackedSince, now);
                int index = (int)trackedStage;
                if (elapsed < StallMilliseconds || index < 0 || index >= lastIncident.Length)
                    return;
                long previous = lastIncident[index];
                if (previous != 0 && ElapsedMilliseconds(previous, now) < RepeatMilliseconds)
                    return;
                lastIncident[index] = Math.Max(1, now);
                reported = true;
                Emit(new Incident(session, trackedStage, false, elapsed));
            }
            catch
            {
                // Optional file diagnostics must never escape a timer callback.
            }
            finally { Volatile.Write(ref sampling, 0); }
        }

        private void Emit(Incident incident)
        {
            if (Volatile.Read(ref disposed) == 0)
            {
                try { report(incident); }
                catch { /* Logging failure must not alter diagnostic state. */ }
            }
        }

        private static double ElapsedMilliseconds(long start, long end) =>
            Math.Max(0, (end - start) * 1000.0 / Stopwatch.Frequency);

        public void Dispose()
        {
            Interlocked.Exchange(ref disposed, 1);
            Interlocked.Exchange(ref timer, null)?.Dispose();
        }
    }
}
