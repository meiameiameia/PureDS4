/*
PureDS4
Copyright (C) 2026 meiameiameia

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

using System;

namespace DS4Windows
{
    /// <summary>
    /// What one controller's report stream looked like over the recent past.
    /// </summary>
    public readonly struct InputReportStatisticsSnapshot
    {
        public InputReportStatisticsSnapshot(int sampleCount, double windowSeconds,
            double rateHz, double typicalIntervalMs, double p95IntervalMs,
            double p99IntervalMs, double worstIntervalMs, int stallCount,
            int estimatedLostReports, double hostP95IntervalMs,
            double hostWorstIntervalMs)
        {
            SampleCount = sampleCount;
            WindowSeconds = windowSeconds;
            RateHz = rateHz;
            TypicalIntervalMs = typicalIntervalMs;
            P95IntervalMs = p95IntervalMs;
            P99IntervalMs = p99IntervalMs;
            WorstIntervalMs = worstIntervalMs;
            StallCount = stallCount;
            EstimatedLostReports = estimatedLostReports;
            HostP95IntervalMs = hostP95IntervalMs;
            HostWorstIntervalMs = hostWorstIntervalMs;
        }

        /// <summary>Reports measured in the window.</summary>
        public int SampleCount { get; }

        /// <summary>How much time the window covers, by the controller's clock.</summary>
        public double WindowSeconds { get; }

        /// <summary>Reports per second actually received.</summary>
        public double RateHz { get; }

        /// <summary>The median gap between reports: the controller's real cadence.</summary>
        public double TypicalIntervalMs { get; }

        public double P95IntervalMs { get; }

        public double P99IntervalMs { get; }

        public double WorstIntervalMs { get; }

        /// <summary>Gaps at least twice the typical one: a visible hitch.</summary>
        public int StallCount { get; }

        /// <summary>
        /// Reports the controller almost certainly sent that never arrived,
        /// counted from how many cadences each oversized gap spans.
        /// </summary>
        public int EstimatedLostReports { get; }

        /// <summary>
        /// The same measure taken on the PC's clock. A gap here that the
        /// controller's clock does not show is Windows delivering late, not the
        /// controller sending late.
        /// </summary>
        public double HostP95IntervalMs { get; }

        public double HostWorstIntervalMs { get; }

        public bool HasData => SampleCount > 0;

        /// <summary>Share of the expected reports that went missing, 0 to 1.</summary>
        public double LostFraction => SampleCount + EstimatedLostReports <= 0
            ? 0
            : (double)EstimatedLostReports / (SampleCount + EstimatedLostReports);
    }

    /// <summary>
    /// Collects the gap between consecutive input reports so a connection's
    /// health can be judged from inside the application. Latency alone, as a
    /// rolling average of twenty samples, hides exactly what matters: an
    /// occasional stall of several cadences averages away to nothing.
    ///
    /// Add runs on the input thread for every report, so it only stores two
    /// numbers in a fixed ring buffer; percentiles are computed in Snapshot,
    /// off that thread.
    /// </summary>
    public sealed class InputReportStatistics
    {
        /// <summary>About 30 seconds at 250 Hz, and 8 seconds at 1000 Hz.</summary>
        public const int Capacity = 8192;

        /// <summary>A gap of at least this many cadences counts as a stall.</summary>
        public const double StallFactor = 2.0;

        /// <summary>Below this many cadences a gap is jitter, not a lost report.</summary>
        private const double LostReportFactor = 1.5;

        private readonly object sync = new object();
        private readonly double[] controllerIntervals = new double[Capacity];
        private readonly double[] hostIntervals = new double[Capacity];
        private int count;
        private int next;

        /// <summary>
        /// Record one report. Intervals are milliseconds: the first by the
        /// controller's own clock, the second by the PC's.
        /// </summary>
        public void Add(double controllerIntervalMs, double hostIntervalMs)
        {
            // A non-finite or non-positive gap says the clock restarted or the
            // report carried a duplicate timestamp; it measures nothing.
            if (!IsUsable(controllerIntervalMs))
            {
                return;
            }

            lock (sync)
            {
                controllerIntervals[next] = controllerIntervalMs;
                hostIntervals[next] = IsUsable(hostIntervalMs) ? hostIntervalMs : 0;
                next = (next + 1) % Capacity;
                if (count < Capacity)
                {
                    count++;
                }
            }
        }

        public void Reset()
        {
            lock (sync)
            {
                count = 0;
                next = 0;
            }
        }

        public InputReportStatisticsSnapshot Snapshot()
        {
            double[] controller;
            double[] host;
            lock (sync)
            {
                if (count == 0)
                {
                    return default;
                }

                controller = new double[count];
                host = new double[count];
                Array.Copy(controllerIntervals, controller, count);
                Array.Copy(hostIntervals, host, count);
            }

            double windowMs = 0;
            foreach (double interval in controller)
            {
                windowMs += interval;
            }

            Array.Sort(controller);
            Array.Sort(host);
            double typical = Percentile(controller, 0.50);
            int stalls = 0;
            int lost = 0;
            if (typical > 0)
            {
                foreach (double interval in controller)
                {
                    if (interval >= typical * StallFactor)
                    {
                        stalls++;
                    }

                    if (interval >= typical * LostReportFactor)
                    {
                        lost += (int)Math.Round(interval / typical) - 1;
                    }
                }
            }

            double windowSeconds = windowMs / 1000.0;
            return new InputReportStatisticsSnapshot(
                sampleCount: controller.Length,
                windowSeconds: windowSeconds,
                rateHz: windowSeconds > 0 ? controller.Length / windowSeconds : 0,
                typicalIntervalMs: typical,
                p95IntervalMs: Percentile(controller, 0.95),
                p99IntervalMs: Percentile(controller, 0.99),
                worstIntervalMs: controller[controller.Length - 1],
                stallCount: stalls,
                estimatedLostReports: lost,
                hostP95IntervalMs: Percentile(host, 0.95),
                hostWorstIntervalMs: host[host.Length - 1]);
        }

        private static bool IsUsable(double intervalMs)
        {
            return intervalMs > 0 && !double.IsNaN(intervalMs) &&
                !double.IsInfinity(intervalMs);
        }

        /// <summary>Linear interpolation between order statistics, on sorted input.</summary>
        internal static double Percentile(double[] sorted, double fraction)
        {
            if (sorted == null || sorted.Length == 0)
            {
                return 0;
            }

            if (sorted.Length == 1)
            {
                return sorted[0];
            }

            double position = (sorted.Length - 1) * fraction;
            int lower = (int)Math.Floor(position);
            int upper = Math.Min(lower + 1, sorted.Length - 1);
            return sorted[lower] + (sorted[upper] - sorted[lower]) * (position - lower);
        }
    }
}
