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
using System.Globalization;

namespace DS4Windows
{
    /// <summary>How a connection's measured report stream should be presented.</summary>
    public enum InputHealthVerdict
    {
        /// <summary>Not enough reports yet to say anything.</summary>
        Unknown,

        /// <summary>Every report arrived on cadence.</summary>
        Steady,

        /// <summary>Occasional gaps: usually unnoticed, worth knowing about.</summary>
        Occasional,

        /// <summary>Frequent or large gaps: a player feels these.</summary>
        Frequent,
    }

    /// <summary>
    /// Turns a measured report stream into the words shown on the diagnostics
    /// screen. Kept apart from the view so the judgement and the wording can be
    /// tested without a window, and so both stay honest about uncertainty.
    /// </summary>
    public static class InputDiagnosticsPresentation
    {
        /// <summary>Below this share of lost reports a link still counts as good.</summary>
        public const double OccasionalLostFraction = 0.001;

        /// <summary>At or above this share, the losses are no longer incidental.</summary>
        public const double FrequentLostFraction = 0.01;

        /// <summary>Reports needed before any verdict is offered.</summary>
        public const int MinimumSamples = 200;

        public static InputHealthVerdict Judge(InputReportStatisticsSnapshot snapshot)
        {
            if (!snapshot.HasData || snapshot.SampleCount < MinimumSamples)
            {
                return InputHealthVerdict.Unknown;
            }

            if (snapshot.StallCount == 0 && snapshot.LostFraction < OccasionalLostFraction)
            {
                return InputHealthVerdict.Steady;
            }

            return snapshot.LostFraction >= FrequentLostFraction
                ? InputHealthVerdict.Frequent
                : InputHealthVerdict.Occasional;
        }

        public static string VerdictLabel(InputHealthVerdict verdict)
        {
            switch (verdict)
            {
                case InputHealthVerdict.Steady: return "Steady";
                case InputHealthVerdict.Occasional: return "Occasional stalls";
                case InputHealthVerdict.Frequent: return "Frequent stalls";
                default: return "Measuring";
            }
        }

        /// <summary>
        /// One sentence a person can act on: what was lost, how bad the worst
        /// gap was, and where the fault sits when the PC is the slow part.
        /// </summary>
        public static string Explain(InputReportStatisticsSnapshot snapshot,
            bool wireless)
        {
            if (!snapshot.HasData)
            {
                return "Waiting for the controller to report.";
            }

            if (snapshot.SampleCount < MinimumSamples)
            {
                return "Measuring. Keep the controller connected for a few seconds.";
            }

            InputHealthVerdict verdict = Judge(snapshot);
            if (verdict == InputHealthVerdict.Steady)
            {
                return string.Format(CultureInfo.CurrentCulture,
                    "Every report arrived on time, about {0:N0} per second.",
                    snapshot.RateHz);
            }

            double cadences = snapshot.TypicalIntervalMs > 0
                ? snapshot.WorstIntervalMs / snapshot.TypicalIntervalMs
                : 0;
            string loss = string.Format(CultureInfo.CurrentCulture,
                "About {0:N1}% of reports never arrived. The worst gap was {1:N0} ms, " +
                "around {2:N0} times the usual {3:N1} ms.",
                snapshot.LostFraction * 100, snapshot.WorstIntervalMs,
                cadences, snapshot.TypicalIntervalMs);

            string cause = wireless
                ? " On a wireless link this is usually interference or the Bluetooth adapter."
                : " On a cable this usually means the cable, the port, or the controller's socket.";
            return loss + cause;
        }

        /// <summary>
        /// Whether Windows, rather than the controller, is the late part: the
        /// PC's own delivery gaps are far worse than the controller's cadence.
        /// </summary>
        public static bool HostDeliveryIsWorse(InputReportStatisticsSnapshot snapshot)
        {
            return snapshot.HasData && snapshot.TypicalIntervalMs > 0 &&
                snapshot.HostWorstIntervalMs >= snapshot.WorstIntervalMs * 1.5 &&
                snapshot.HostWorstIntervalMs >= snapshot.TypicalIntervalMs * 3;
        }

        public static string FormatInterval(double milliseconds)
        {
            return milliseconds <= 0
                ? "—"
                : string.Format(CultureInfo.CurrentCulture, "{0:N1} ms", milliseconds);
        }

        public static string FormatRate(double rateHz)
        {
            return rateHz <= 0
                ? "—"
                : string.Format(CultureInfo.CurrentCulture, "{0:N0} Hz", rateHz);
        }

        public static string FormatLoss(InputReportStatisticsSnapshot snapshot)
        {
            if (!snapshot.HasData)
            {
                return "—";
            }

            return string.Format(CultureInfo.CurrentCulture, "{0:N0} ({1:N2}%)",
                snapshot.EstimatedLostReports, snapshot.LostFraction * 100);
        }

        public static string FormatWindow(InputReportStatisticsSnapshot snapshot)
        {
            return !snapshot.HasData
                ? "—"
                : string.Format(CultureInfo.CurrentCulture, "{0:N0} reports over {1:N0} s",
                    snapshot.SampleCount, snapshot.WindowSeconds);
        }
    }
}
