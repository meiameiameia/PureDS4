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
/// Measure the real Settings, Tools and Auto Profiles markup the same way the
/// Home and profile workspaces are measured: detached from MainWindow/App
/// startup, with only event hookups stripped. Layout and spacing checks, not
/// owner visual acceptance.
/// </summary>
[TestClass]
[DoNotParallelize]
public class SettingsWorkspacePresentationTests
{
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";
    private static readonly XNamespace Wpf = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly Size[] Viewports = { new Size(704, 360), new Size(1008, 540) };

    [TestMethod]
    public void SettingsCategoriesFitBothThemesAndViewports()
    {
        WpfTestHost.Run(() =>
        {
            foreach (bool dark in new[] { false, true })
            foreach (Size size in Viewports)
            {
                Theme(dark);
                UserControl view = LoadTabWorkspace("settingsTab");
                var owner = new Window { Content = view, DataContext = new SettingsFixture() };
                var categories = (TabControl)view.FindName("settingsCategoryTabs");
                Assert.AreEqual(6, categories.Items.Count, "Settings keeps six categories.");
                for (int category = 0; category < categories.Items.Count; category++)
                {
                    categories.SelectedIndex = category;
                    Layout(view, size);
                    Preview(view, "settings-" + category + "-" + (int)size.Width, dark);
                    foreach (Control control in Descendants<Control>(view).Where(c => c.ActualWidth > 0 && Participates(c)))
                        Inside(view, control, "category " + category);
                }
                owner.Content = null;
            }
        });
    }

    [TestMethod]
    public void ToolsAndAutoProfilesFitBothThemesAndViewports()
    {
        WpfTestHost.Run(() =>
        {
            foreach (bool dark in new[] { false, true })
            foreach (Size size in Viewports)
            {
                Theme(dark);
                UserControl tools = LoadTabWorkspace("toolsTab");
                var toolsOwner = new Window { Content = tools, DataContext = new SettingsFixture() };
                Layout(tools, size);
                Preview(tools, "tools-" + (int)size.Width, dark);
                foreach (Control control in Descendants<Control>(tools).Where(c => c.ActualWidth > 0 && Participates(c)))
                    Inside(tools, control, "tools");
                toolsOwner.Content = null;

                UserControl auto = LoadControl("AutoProfiles");
                var autoOwner = new Window { Content = auto, DataContext = new SettingsFixture() };
                Layout(auto, size);
                Preview(auto, "autoprofiles-" + (int)size.Width, dark);
                foreach (Control control in Descendants<Control>(auto).Where(c => c.ActualWidth > 0 && Participates(c)))
                    Inside(auto, control, "auto profiles");
                autoOwner.Content = null;
            }
        });
    }

    /// <summary>
    /// Settings, Tools and Auto Profiles must share Home's workspace inset. Home
    /// is the accepted reference; content never sits flush against the window.
    /// </summary>
    [TestMethod]
    public void PublicMigrationCopyNamesOnlyTheReleasedPredecessor()
    {
        XElement tools = Source("MainWindow").Descendants()
            .Single(element => (string)element.Attribute(X + "Name") == "toolsTab");
        string[] copy = tools.Descendants(Wpf + "TextBlock")
            .Select(element => (string)element.Attribute("Text"))
            .Where(text => text != null).ToArray();

        Assert.IsTrue(copy.Any(text => text.Contains("existing DS4Windows installation")));
        Assert.IsTrue(copy.Any(text => text.Contains("Opening the plan does not remove anything")));
        Assert.IsFalse(copy.Any(text => text.Contains("DS4Windows Reworked")),
            "An unreleased development name must not be offered as a public migration path.");
    }

    [TestMethod]
    public void WorkspaceInsetsMatchHome()
    {
        XElement main = Source("MainWindow");
        string home = (string)main.Descendants().Single(e => (string)e.Attribute(X + "Name") == "homeWorkspace").Attribute("Margin");
        Assert.AreEqual("16,8", home, "Home's accepted inset is the reference.");
        Assert.AreEqual(home, (string)Workspace(main, "settingsTab").Attribute("Margin"), "Settings must share Home's workspace inset.");
        Assert.AreEqual(home, (string)Workspace(main, "toolsTab").Attribute("Margin"), "Tools must share Home's workspace inset.");
        XElement autoPanel = Source("AutoProfiles").Elements(Wpf + "DockPanel").Single();
        Assert.AreEqual(home, (string)autoPanel.Attribute("Margin"), "Auto Profiles must share Home's workspace inset.");
    }

    private static XElement Workspace(XElement main, string tabName)
    {
        return main.Descendants().Single(e => (string)e.Attribute(X + "Name") == tabName)
            .Elements().Single(e => e.Name.LocalName == "Grid" || e.Name.LocalName == "DockPanel");
    }

    private static XElement Source(string name)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "PureDS4.sln"))) directory = directory.Parent;
        Assert.IsNotNull(directory);
        return XElement.Load(Path.Combine(directory.FullName, "PureDS4", "DS4Forms", name + ".xaml"));
    }

    /// <summary>Lift one primary tab's workspace, with that tab's own resources.</summary>
    private static UserControl LoadTabWorkspace(string tabName)
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

    private static UserControl LoadControl(string name)
    {
        XElement source = Source(name);
        source.Name = Wpf + "UserControl";
        source.Attribute(X + "Class")?.Remove();
        return Parse(source);
    }

    private static UserControl Parse(XElement source)
    {
        System.Reflection.Assembly.Load("WPFLocalizeExtension");
        System.Reflection.Assembly.Load(typeof(Xceed.Wpf.Toolkit.DoubleUpDown).Assembly.FullName);
        foreach (string property in new[] { "Title", "Height", "Width", "MinHeight", "MinWidth", "Style", "ResizeMode", "WindowStartupLocation", "ShowInTaskbar" })
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

    private static void Theme(bool dark)
    {
        Application.Current.Resources.MergedDictionaries.Clear();
        foreach (string name in new[] { dark ? "DarkTheme" : "DefaultTheme", "Foundation", "BridgeShellStyles" })
            WpfTestHost.LoadDictionary("/PureDS4;component/DS4Forms/Themes/" + name + ".xaml");
        Application.Current.Resources["BooleanToVisibilityConverter"] = new BooleanToVisibilityConverter();
    }

    private static void Layout(FrameworkElement view, Size size) { view.Measure(size); view.Arrange(new Rect(size)); view.UpdateLayout(); }

    private static void Inside(FrameworkElement root, FrameworkElement child, string where)
    {
        Rect b = child.TransformToAncestor(root).TransformBounds(new Rect(child.RenderSize));
        Assert.IsTrue(b.Left >= -1 && b.Right <= root.ActualWidth + 1,
            where + ": " + child.Name + "/" + child.GetType().Name + ": " + b + " outside " + root.ActualWidth);
    }

    private static bool Participates(DependencyObject element)
    {
        for (var current = element; current != null; current = VisualTreeHelper.GetParent(current))
            if (current is UIElement ui && ui.Visibility != Visibility.Visible) return false;
        return true;
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T typed) yield return typed;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }

    private static void Preview(FrameworkElement view, string name, bool dark)
    {
        string directory = Environment.GetEnvironmentVariable("PUREDS4_VISUAL_PREVIEW_DIRECTORY");
        if (string.IsNullOrEmpty(directory)) return;
        Assert.IsTrue(Directory.Exists(directory));
        var bitmap = new RenderTargetBitmap(Math.Max(1, (int)view.ActualWidth), Math.Max(1, (int)view.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        var drawing = new DrawingVisual();
        using (var dc = drawing.RenderOpen()) dc.DrawRectangle((Brush)Application.Current.FindResource("SurfaceBaseBrush"), null, new Rect(view.RenderSize));
        bitmap.Render(drawing); bitmap.Render(view);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(directory, "pureds4-" + name + "-" + (dark ? "dark" : "light") + ".png"));
        encoder.Save(stream);
    }

    /// <summary>Disposable presentation state; never the owner's settings.</summary>
    public sealed class SettingsFixture
    {
        public bool RunAtStartup { get; set; } = true;
        public bool CanChangeRunAtStartup { get; set; } = true;
        public bool CanWriteTask { get; set; } = true;
        public bool RunStartProg { get; set; } = true;
        public bool RunStartTask { get; set; }
        public Visibility ShowRunStartPanel { get; set; } = Visibility.Visible;
        public int ShowNotificationsIndex { get; set; } = 2;
        public Visibility IsProfileChangedCheckVisible { get; set; } = Visibility.Visible;
        public bool ProfileChangedNotification { get; set; }
        public bool StartMinimize { get; set; }
        public bool MinimizeToTaskbar { get; set; }
        public bool CloseMinimizes { get; set; }
        public int IconChoiceIndex { get; set; }
        public int AppChoiceIndex { get; set; }
        public bool CheckForUpdates { get; set; }
        public int CheckEvery { get; set; } = 1;
        public int CheckEveryUnit { get; set; } = 1;
        public bool HideDS4Controller { get; set; } = true;
        public bool ReclaimSteamInput { get; set; }
        public bool SwipeTouchSwitchProfile { get; set; } = true;
        public bool DisconnectBTStop { get; set; }
        public bool QuickCharge { get; set; }
        public bool FlashHighLatency { get; set; } = true;
        public int FlashHighLatencyAt { get; set; } = 30;
        public bool PromptForViiperSetup { get; set; } = true;
        public bool UseOSCServer { get; set; } = true;
        public int OscPort { get; set; } = 9000;
        public bool InterpretingOscMonitoring { get; set; }
        public bool UseOSCSender { get; set; }
        public string OscSenderAddress { get; set; } = "127.0.0.1";
        public int OscSendPort { get; set; } = 9001;
        public bool UseUDPServer { get; set; } = true;
        public string UdpIpAddress { get; set; } = "127.0.0.1";
        public int UdpPort { get; set; } = 26760;
        public bool UseUdpSmoothing { get; set; } = true;
        public Visibility UdpServerOneEuroPanelVisibility { get; set; } = Visibility.Visible;
        public double UdpSmoothMinCutoff { get; set; } = 0.4;
        public double UdpSmoothBeta { get; set; } = 0.2;
        public bool UseCustomSteamFolder { get; set; } = true;
        public string CustomSteamFolder { get; set; } = "C:\\Program Files (x86)\\Steam";
        public string FakeExeName { get; set; } = "example.exe";
        public bool VerboseStartupLogging { get; set; }
        public int ProcessPriorityIndex { get; set; } = 1;
        public object[] AbsMonitorChoices { get; } = { new { DisplayItemString = "Primary monitor", EDID = "fixture" } };
        public string AbsMonitorSettingEDID { get; set; } = "fixture";
        public bool HidHideClientFound { get; set; } = true;
        public bool RevertDefaultProfileOnUnknown { get; set; } = true;
        public bool ApplyToAllControllers { get; set; } = true;
        public Visibility ControllerSpecificProfilesVisible { get; set; } = Visibility.Visible;
        public Visibility ExpandedControllerSpecificProfilesVisible { get; set; } = Visibility.Collapsed;
        public int SelectedIndexCon1 { get; set; }
        public int SelectedIndexCon2 { get; set; }
        public int SelectedIndexCon3 { get; set; }
        public int SelectedIndexCon4 { get; set; }
        public int SelectedIndexCon5 { get; set; }
        public int SelectedIndexCon6 { get; set; }
        public int SelectedIndexCon7 { get; set; }
        public int SelectedIndexCon8 { get; set; }
        public object[] DisplayProfileSwitchList { get; } = { new { DisplayName = "Profile switch", ChoiceValue = 0 } };
        public int ProfileSwitchChoice { get; set; }
    }
}
