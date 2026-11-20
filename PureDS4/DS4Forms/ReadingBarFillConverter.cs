using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DS4WinWPF.DS4Forms
{
    /// <summary>
    /// Margin for the fill of a read-only reading bar: from zero (the left end for
    /// 0..255 triggers, the centre for signed motion values) to the current value.
    /// Inputs: Value, Minimum, Maximum, track width.
    /// </summary>
    public sealed class ReadingBarFillConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 4 || values[0] is not double value || values[1] is not double min ||
                values[2] is not double max || values[3] is not double width || max <= min || width <= 0)
            {
                // Not measured yet: keep the fill empty rather than full.
                return new Thickness(0, 0, 100000, 0);
            }

            double origin = Math.Clamp(0.0, min, max);
            value = Math.Clamp(value, min, max);
            double Fraction(double v) => (v - min) / (max - min);
            double left = Fraction(Math.Min(origin, value)) * width;
            double right = width - Fraction(Math.Max(origin, value)) * width;
            return new Thickness(left, 0, right, 0);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
