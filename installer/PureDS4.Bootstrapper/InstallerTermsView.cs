using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace PureDS4.Bootstrapper;

/// <summary>Read-only presentation; no installer, browser or host-state access.</summary>
internal static class InstallerTermsView
{
    internal static Window Create(FlowDocument document, string title, ResourceDictionary resources)
    {
        var terms = new Window
        {
            Title = title, Width = 720, Height = 580, MinWidth = 480, MinHeight = 360,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        terms.Resources.MergedDictionaries.Add(resources);
        terms.SetResourceReference(FrameworkElement.StyleProperty, "WindowStyle");
        // Keep the complete offline text and its formatting, but make vendor
        // black-on-white text readable on the selected application surface.
        new TextRange(document.ContentStart, document.ContentEnd)
            .ApplyPropertyValue(TextElement.ForegroundProperty, terms.FindResource("TextPrimaryBrush"));
        document.FontFamily = (FontFamily)terms.FindResource("TypeUiFontFamily");
        var viewer = new FlowDocumentScrollViewer
        {
            Document = document, IsToolBarVisible = true, Margin = new Thickness(16)
        };
        viewer.SetResourceReference(Control.BackgroundProperty, "SurfaceBaseBrush");
        viewer.SetResourceReference(Control.ForegroundProperty, "TextPrimaryBrush");
        var close = new Button
        {
            Content = "Close", MinWidth = 88, IsCancel = true,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        close.SetResourceReference(FrameworkElement.StyleProperty, "FoundationButtonStyle");
        close.Click += (_, _) => terms.Close();
        var footer = new Border { Child = close, Margin = new Thickness(16, 0, 16, 16) };
        footer.SetResourceReference(FrameworkElement.StyleProperty, "FoundationDialogActionsStyle");
        DockPanel.SetDock(footer, Dock.Bottom);
        var content = new DockPanel();
        content.Children.Add(footer);
        content.Children.Add(viewer);
        terms.Content = content;
        return terms;
    }
}
