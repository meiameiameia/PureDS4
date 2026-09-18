using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;

namespace DS4WindowsTests;

/// <summary>
/// Loads the product's real markup detached from MainWindow/App startup, so a
/// screen can be measured and rendered without hardware, profiles or timers.
/// Only event hookups are stripped; templates, bindings and layout stay real.
/// </summary>
internal static class PresentationHarness
{
    internal static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";
    internal static readonly XNamespace Wpf = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    /// <summary>The two viewport sizes every workspace has to survive.</summary>
    internal static readonly Size[] Viewports = { new Size(704, 360), new Size(1008, 540) };

    internal static XElement Source(string name)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "PureDS4.sln"))) directory = directory.Parent;
        Assert.IsNotNull(directory);
        return XElement.Load(Path.Combine(directory.FullName, "PureDS4", "DS4Forms", name + ".xaml"));
    }

    /// <summary>The Grid or DockPanel that fills one of MainWindow's tabs.</summary>
    internal static XElement Workspace(XElement main, string tabName)
    {
        return main.Descendants().Single(e => (string)e.Attribute(X + "Name") == tabName)
            .Elements().Single(e => e.Name.LocalName == "Grid" || e.Name.LocalName == "DockPanel"
                || e.Name.LocalName == "OutputSlotManagerControl");
    }

    /// <summary>Lift one tab's workspace, with that tab's own resources.</summary>
    internal static UserControl LoadTabWorkspace(string tabName)
    {
        XElement main = Source("MainWindow");
        XElement tab = main.Descendants().Single(e => (string)e.Attribute(X + "Name") == tabName);
        // Carry mc:Ignorable as well as the namespaces, so design-time
        // attributes such as d:IsHidden stay ignorable in the fixture.
        var wrapper = new XElement(Wpf + "UserControl",
            main.Attributes().Where(a => a.IsNamespaceDeclaration || a.Name.LocalName == "Ignorable"));
        XElement resources = tab.Elements(Wpf + "TabItem.Resources").SingleOrDefault();
        if (resources != null)
            wrapper.Add(new XElement(Wpf + "UserControl.Resources", resources.Elements()));
        wrapper.Add(new XElement(Workspace(main, tabName)));
        return Parse(wrapper);
    }

    internal static UserControl LoadControl(string name)
    {
        XElement source = Source(name);
        string rootKind = source.Name.LocalName;
        source.Name = Wpf + "UserControl";
        source.Attribute(X + "Class")?.Remove();
        foreach (var resource in source.Elements(Wpf + rootKind + ".Resources").ToArray())
            resource.Name = Wpf + "UserControl.Resources";
        return Parse(source);
    }

    internal static UserControl Parse(XElement source)
    {
        System.Reflection.Assembly.Load("WPFLocalizeExtension");
        System.Reflection.Assembly.Load(typeof(Xceed.Wpf.Toolkit.DoubleUpDown).Assembly.FullName);
        foreach (string property in new[] { "Title", "Height", "Width", "MinHeight", "MinWidth", "Style", "ResizeMode", "WindowStartupLocation", "ShowInTaskbar", "SizeToContent", "WindowStyle" })
            source.Attribute(property)?.Remove();
        foreach (XAttribute a in source.DescendantsAndSelf().Attributes().ToArray())
        {
            // Product event methods are not runnable in a detached XAML fixture.
            if (!a.IsNamespaceDeclaration && Regex.IsMatch(a.Value, "^[A-Za-z]+[A-Za-z0-9]*_[A-Za-z][A-Za-z0-9_]*$") &&
                a.Name.LocalName != "Tag" && a.Name.LocalName != "Name" && a.Name.LocalName != "Key") a.Remove();
        }
        // These controls own live state or a product constructor of their own.
        foreach (var live in source.Descendants()
            .Where(e => e.Name.LocalName == "ControllerReadingsControl" || e.Name.LocalName == "LanguagePackControl").ToArray())
            live.ReplaceWith(new XElement(Wpf + "Border", live.Attributes().Where(a => a.Name == X + "Name")));
        string text = Regex.Replace(source.ToString(), "clr-namespace:DS4WinWPF[^\";]*", m => m.Value + ";assembly=PureDS4");
        return (UserControl)XamlReader.Parse(text);
    }

    internal static void Theme(bool dark)
    {
        Application.Current.Resources.MergedDictionaries.Clear();
        foreach (string name in new[] { dark ? "DarkTheme" : "DefaultTheme", "Foundation", "BridgeShellStyles" })
            WpfTestHost.LoadDictionary("/PureDS4;component/DS4Forms/Themes/" + name + ".xaml");
        Application.Current.Resources["BooleanToVisibilityConverter"] = new BooleanToVisibilityConverter();
    }

    internal static void Layout(FrameworkElement view, Size size)
    {
        view.Measure(size);
        view.Arrange(new Rect(size));
        view.UpdateLayout();
    }

    internal static void AssertInside(FrameworkElement root, FrameworkElement child, string where)
    {
        Rect b = child.TransformToAncestor(root).TransformBounds(new Rect(child.RenderSize));
        Assert.IsTrue(b.Left >= -1 && b.Right <= root.ActualWidth + 1,
            where + ": " + child.Name + "/" + child.GetType().Name + ": " + b + " outside " + root.ActualWidth);
    }

    internal static bool Participates(DependencyObject element)
    {
        for (var current = element; current != null; current = VisualTreeHelper.GetParent(current))
            if (current is UIElement ui && ui.Visibility != Visibility.Visible) return false;
        return true;
    }

    internal static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T typed) yield return typed;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }

    /// <summary>Opt-in PNG of the arranged view; no window is ever shown.</summary>
    internal static void Preview(FrameworkElement view, string name, bool dark)
    {
        string directory = Environment.GetEnvironmentVariable("PUREDS4_VISUAL_PREVIEW_DIRECTORY");
        if (string.IsNullOrEmpty(directory)) return;
        Assert.IsTrue(Directory.Exists(directory));
        var bitmap = new RenderTargetBitmap(Math.Max(1, (int)view.ActualWidth), Math.Max(1, (int)view.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        var drawing = new DrawingVisual();
        using (var dc = drawing.RenderOpen()) dc.DrawRectangle((Brush)Application.Current.FindResource("SurfaceBaseBrush"), null, new Rect(view.RenderSize));
        bitmap.Render(drawing);
        bitmap.Render(view);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(directory, "pureds4-" + name + "-" + (dark ? "dark" : "light") + ".png"));
        encoder.Save(stream);
    }
}
