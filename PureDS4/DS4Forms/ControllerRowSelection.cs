using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DS4WinWPF.DS4Forms
{
    /// <summary>
    /// Select a device before an embedded profile/lightbar control handles input.
    /// Do not consume the event: native selector and keyboard behavior still apply.
    /// </summary>
    public static class ControllerRowSelection
    {
        public static readonly DependencyProperty SelectOnInteractionProperty =
            DependencyProperty.RegisterAttached("SelectOnInteraction", typeof(bool),
                typeof(ControllerRowSelection), new PropertyMetadata(false, OnChanged));

        public static bool GetSelectOnInteraction(DependencyObject element) =>
            (bool)element.GetValue(SelectOnInteractionProperty);

        public static void SetSelectOnInteraction(DependencyObject element, bool value) =>
            element.SetValue(SelectOnInteractionProperty, value);

        private static void OnChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is not ListViewItem row) return;
            if ((bool)e.OldValue)
            {
                row.PreviewMouseDown -= SelectRow;
                row.PreviewGotKeyboardFocus -= SelectRow;
            }
            if ((bool)e.NewValue)
            {
                row.PreviewMouseDown += SelectRow;
                row.PreviewGotKeyboardFocus += SelectRow;
            }
        }

        private static void SelectRow(object sender, RoutedEventArgs e)
        {
            if (sender is ListViewItem { IsEnabled: true } row)
                row.SetCurrentValue(ListViewItem.IsSelectedProperty, true);
        }
    }
}
