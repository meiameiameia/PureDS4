using DS4Windows;
using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Data;

namespace DS4WinWPF.DS4Forms.Converters
{
    public sealed class ControllerStartupStageVisualStateConverter :
        IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter,
            CultureInfo culture)
        {
            return value switch
            {
                ControllerStartupStage.Ready => StatusVisualState.Success,
                ControllerStartupStage.Attention => StatusVisualState.Warning,
                _ => StatusVisualState.Neutral,
            };
        }

        public object ConvertBack(object value, Type targetType,
            object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }

    /// <summary>
    /// Formats an item count with a correctly pluralised noun, e.g. "1 profile"
    /// and "3 profiles". The noun is passed as the converter parameter.
    /// </summary>
    public sealed class CountLabelConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter,
            CultureInfo culture)
        {
            string noun = parameter as string ?? "item";
            int count = value is int i ? i : 0;
            return count == 1
                ? $"{count} {noun}"
                : $"{count} {noun}s";
        }

        public object ConvertBack(object value, Type targetType,
            object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }

    public sealed class BatteryTextVisualStateConverter : IValueConverter
    {
        private static readonly Regex CapacityPattern = new(
            @"(?<capacity>\d{1,3})", RegexOptions.Compiled);

        public object Convert(object value, Type targetType, object parameter,
            CultureInfo culture)
        {
            string text = value as string ?? string.Empty;
            if (text.Equals("Full", StringComparison.OrdinalIgnoreCase) ||
                text.Equals("Charging", StringComparison.OrdinalIgnoreCase) ||
                text.EndsWith("+", StringComparison.Ordinal))
            {
                return StatusVisualState.Success;
            }

            if (text.Contains("error", StringComparison.OrdinalIgnoreCase))
            {
                return StatusVisualState.Error;
            }

            if (text.Contains("unavailable",
                    StringComparison.OrdinalIgnoreCase))
            {
                return StatusVisualState.Warning;
            }

            Match match = CapacityPattern.Match(text);
            if (match.Success &&
                int.TryParse(match.Groups["capacity"].Value,
                    NumberStyles.Integer, CultureInfo.InvariantCulture,
                    out int capacity) && capacity <= 20)
            {
                return StatusVisualState.Warning;
            }

            return StatusVisualState.Neutral;
        }

        public object ConvertBack(object value, Type targetType,
            object parameter, CultureInfo culture) =>
            DependencyProperty.UnsetValue;
    }
}
