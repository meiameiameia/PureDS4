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
    /// Correcting a stick that does not rest at centre.
    ///
    /// The correction used to subtract the offset and clamp, which moved the
    /// whole axis: a stick resting three counts high could then never reach
    /// full travel on one side. Rescaling each half of the axis instead keeps
    /// both ends reachable, which is what a player notices.
    /// </summary>
    public static class StickCalibration
    {
        /// <summary>Where an axis rests when the stick is untouched.</summary>
        public const int Centre = 128;

        public const byte Minimum = 0;

        public const byte Maximum = 255;

        /// <summary>
        /// Map a raw axis reading so the measured resting point becomes centre,
        /// while 0 stays 0 and 255 stays 255.
        /// </summary>
        /// <param name="raw">The value the controller reported.</param>
        /// <param name="drift">
        /// Measured resting point minus <see cref="Centre"/>. Zero means the
        /// stick rests where it should and the reading passes through.
        /// </param>
        public static byte Correct(byte raw, int drift)
        {
            if (drift == 0)
            {
                return raw;
            }

            int rest = Centre + drift;
            // A resting point at either extreme leaves no half to rescale; the
            // measurement is unusable, so change nothing rather than distort.
            if (rest <= Minimum || rest >= Maximum)
            {
                return raw;
            }

            if (raw == rest)
            {
                return Centre;
            }

            double corrected = raw < rest
                ? (double)raw / rest * Centre
                : Centre + (double)(raw - rest) / (Maximum - rest) * (Maximum - Centre);

            // Round halves away from zero rather than to even: predictable is
            // worth more here than statistically tidy.
            return (byte)Math.Clamp(
                (int)Math.Round(corrected, MidpointRounding.AwayFromZero),
                Minimum, Maximum);
        }

        /// <summary>
        /// Collects where a stick actually rests over many reports, so a single
        /// noisy sample, or a stick still moving, cannot become the correction.
        /// </summary>
        public sealed class CentreMeasurement
        {
            /// <summary>Samples needed before a measurement can be trusted.</summary>
            public const int RequiredSamples = 60;

            /// <summary>
            /// How far readings may spread and still count as a stick at rest.
            /// A resting DS4 stick varies by a count or two; more than this
            /// means it was being touched.
            /// </summary>
            public const int SteadySpread = 4;

            private int count;
            private long sumX;
            private long sumY;
            private int minX = int.MaxValue;
            private int maxX = int.MinValue;
            private int minY = int.MaxValue;
            private int maxY = int.MinValue;

            public int Count => count;

            public int SpreadX => count == 0 ? 0 : maxX - minX;

            public int SpreadY => count == 0 ? 0 : maxY - minY;

            public double MeanX => count == 0 ? Centre : (double)sumX / count;

            public double MeanY => count == 0 ? Centre : (double)sumY / count;

            /// <summary>Enough samples, and the stick held still for all of them.</summary>
            public bool IsSteady => count >= RequiredSamples &&
                SpreadX <= SteadySpread && SpreadY <= SteadySpread;

            /// <summary>Whether the measured rest is far enough to be worth correcting.</summary>
            public bool NeedsCorrection => IsSteady && (DriftX != 0 || DriftY != 0);

            public sbyte DriftX => ToDrift(MeanX);

            public sbyte DriftY => ToDrift(MeanY);

            public void Add(byte x, byte y)
            {
                count++;
                sumX += x;
                sumY += y;
                minX = Math.Min(minX, x);
                maxX = Math.Max(maxX, x);
                minY = Math.Min(minY, y);
                maxY = Math.Max(maxY, y);
            }

            public void Reset()
            {
                count = 0;
                sumX = 0;
                sumY = 0;
                minX = int.MaxValue;
                maxX = int.MinValue;
                minY = int.MaxValue;
                maxY = int.MinValue;
            }

            /// <summary>What to tell the person once measuring has finished.</summary>
            public string Describe()
            {
                if (count < RequiredSamples)
                {
                    return "Measuring. Let go of the stick and keep the controller still.";
                }

                if (!IsSteady)
                {
                    return "The stick moved while measuring. Let go of it and try again.";
                }

                if (!NeedsCorrection)
                {
                    return "This stick already rests at centre. No correction is needed.";
                }

                return $"Resting point measured {DriftX:+0;-0;0} across and " +
                    $"{DriftY:+0;-0;0} down from centre, over {count} readings. " +
                    "Saving keeps full travel in both directions.";
            }

            private static sbyte ToDrift(double mean)
            {
                int drift = (int)Math.Round(mean, MidpointRounding.AwayFromZero) - Centre;
                return (sbyte)Math.Clamp(drift, sbyte.MinValue, sbyte.MaxValue);
            }
        }
    }
}
