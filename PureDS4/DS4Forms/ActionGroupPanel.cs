using System;
using System.Windows;
using System.Windows.Controls;

namespace DS4WinWPF.DS4Forms
{
    /// <summary>
    /// Two intact action groups: leading/trailing on one line when they fit,
    /// otherwise on separate lines. Measures content, not a window breakpoint.
    /// </summary>
    public sealed class ActionGroupPanel : Panel
    {
        private const double GroupGap = 16;
        private const double RowGap = 8;

        protected override Size MeasureOverride(Size availableSize)
        {
            if (InternalChildren.Count != 2)
                throw new InvalidOperationException("ActionGroupPanel requires exactly two groups.");

            foreach (UIElement child in InternalChildren)
                child.Measure(new Size(double.PositiveInfinity, availableSize.Height));
            Size first = InternalChildren[0].DesiredSize;
            Size second = InternalChildren[1].DesiredSize;
            double width = first.Width + Gap(first, second) + second.Width;
            return width <= availableSize.Width
                ? new Size(width, Math.Max(first.Height, second.Height))
                : new Size(Math.Max(first.Width, second.Width), first.Height + RowGap + second.Height);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            Size first = InternalChildren[0].DesiredSize;
            Size second = InternalChildren[1].DesiredSize;
            bool stacked = first.Width + Gap(first, second) + second.Width > finalSize.Width;
            InternalChildren[0].Arrange(new Rect(new Point(), first));
            InternalChildren[1].Arrange(new Rect(
                new Point(Math.Max(0, finalSize.Width - second.Width), stacked ? first.Height + RowGap : 0), second));
            return finalSize;
        }

        private static double Gap(Size first, Size second) =>
            first.Width > 0 && second.Width > 0 ? GroupGap : 0;
    }
}
