using System.Windows;
using System.Windows.Controls;

namespace DS4WinWPF.DS4Forms;

/// <summary>Marks a parent section without pretending that two pages are selected.</summary>
public static class PrimaryNavigation
{
    public static readonly DependencyProperty IsSectionActiveProperty =
        DependencyProperty.RegisterAttached("IsSectionActive", typeof(bool),
            typeof(PrimaryNavigation), new PropertyMetadata(false));

    public static bool GetIsSectionActive(DependencyObject element) =>
        (bool)element.GetValue(IsSectionActiveProperty);

    public static void SetIsSectionActive(DependencyObject element, bool value) =>
        element.SetValue(IsSectionActiveProperty, value);

    internal static void ReturnToTools(TabControl navigation)
    {
        foreach (object item in navigation.Items)
            if (item is TabItem { Name: "toolsTab", IsEnabled: true } tools)
            {
                navigation.SelectedItem = tools;
                return;
            }
    }
}
