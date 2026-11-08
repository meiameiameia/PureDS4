using DS4WinWPF.DS4Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml.Linq;

namespace DS4WindowsTests;

/// <summary>
/// Real shell markup in a plain, test-owned Window, never App/MainWindow.
/// Native screenshots are explicitly opt-in. Ordinary runs create no HWND and
/// show no window; captures are not acceptance of hardware or the installed app.
/// </summary>
[TestClass]
[DoNotParallelize]
public class NativeWindowPreviewTests
{
    private static readonly XNamespace X = PresentationHarness.X;
    private static readonly XNamespace Wpf = PresentationHarness.Wpf;
    private const string OutputVariable = "PUREDS4_NATIVE_PREVIEW_DIRECTORY";

    [TestMethod]
    public void WholeWindowFixturesIncludeRealShellAndOnlyShowWhenCaptureIsRequested()
    {
        string output = Environment.GetEnvironmentVariable(OutputVariable);
        bool offscreen = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PUREDS4_VISUAL_PREVIEW_DIRECTORY"));
        Assert.IsFalse(offscreen && !string.IsNullOrEmpty(output),
            "Choose offscreen content rendering or explicitly authorized native capture, never both.");
        if (!string.IsNullOrEmpty(output))
        {
            Assert.IsTrue(Path.IsPathFullyQualified(output));
            Assert.IsTrue(Directory.Exists(output), "Prepare a disposable output directory explicitly.");
        }

        // Ordinary construction/ownership checks do not measure geometry or
        // theme pixels. Keep one shell per page; the explicit preview path
        // still captures the full theme/viewport matrix.
        bool capture = !string.IsNullOrEmpty(output);
        foreach (bool dark in capture || offscreen ? new[] { false, true } : new[] { true })
        foreach (bool compact in capture || offscreen ? new[] { true, false } : new[] { true })
        foreach (string page in new[] { "home", "home-native", "home-recovery", "profiles", "editor", "settings", "tools", "autoprofiles", "diagnostics", "outputslots", "log" })
            WpfTestHost.Run(() =>
            {
                ProfileWorkspacePresentationTests.Theme(dark);
                Window window = CreateWindow(page, compact);
                try
                {
                    var shell = (UserControl)window.Content;
                    var tabs = (TabControl)shell.FindName("mainTabCon");
                    Assert.AreEqual(8, tabs.Items.Count);
                    Assert.IsNotNull(shell.FindName("elevationStatusBanner"));
                    Assert.AreEqual("Service running", ((TextBlock)shell.FindName("serviceStatusText")).Text);
                    Assert.AreEqual(WindowStyle.SingleBorderWindow, window.WindowStyle);
                    Assert.IsFalse(window.AllowsTransparency);
                    Assert.IsTrue(NativeWindowFrame.IsAttached(window));
                    Assert.AreEqual(IntPtr.Zero, new WindowInteropHelper(window).Handle,
                        "Ordinary presentation checks must not create native windows.");

                    if (offscreen)
                    {
                        // Conservative client viewports, not a fabricated native
                        // frame. No Show/EnsureHandle/focus/desktop capture is used.
                        PresentationHarness.Layout(shell, compact ? new Size(704, 430) : new Size(1008, 610));
                        PresentationHarness.Preview(shell, $"offscreen-shell-{page}-{(int)shell.ActualWidth}", dark);
                        Assert.AreEqual(IntPtr.Zero, new WindowInteropHelper(window).Handle,
                            "Offscreen rendering must not create a window handle.");
                    }

                    if (!string.IsNullOrEmpty(output))
                    {
                        window.Topmost = true;
                        window.Show();
                        PumpRendering();
                        window.UpdateLayout();
                        IntPtr handle = new WindowInteropHelper(window).Handle;
                        var bitmap = CaptureOwnWindow(handle, out NativeRect bounds);
                        // Native pixels must contain the real client selection cue,
                        // not merely a title bar with a blank/unrendered WPF client.
                        var tools = (TabItem)shell.FindName("toolsTab");
                        var selected = PrimaryNavigation.GetIsSectionActive(tools) ? tools : (TabItem)tabs.SelectedItem;
                        var cue = (Border)selected.Template.FindName("SelectionIndicator", selected);
                        AssertPixel(bitmap, bounds, cue.PointToScreen(new Point(cue.ActualWidth / 2, cue.ActualHeight / 2)),
                            ((SolidColorBrush)cue.Background).Color);
                        if (NativeWindowFrame.LastRequest(window) is { Succeeded: true })
                            AssertPixel(bitmap, bounds, new Point((bounds.Left + bounds.Right) / 2, bounds.Top + 12),
                                ((SolidColorBrush)Application.Current.FindResource("SurfaceBaseBrush")).Color);
                        var encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(bitmap));
                        string name = $"pureds4-window-{page}-{(int)window.Width}-{(dark ? "dark" : "light")}.png";
                        using var stream = File.Create(Path.Combine(output, name));
                        encoder.Save(stream);
                    }
                }
                finally
                {
                    window.Close();
                    Assert.IsFalse(NativeWindowFrame.IsAttached(window));
                }
            });
    }

    internal static Window CreateWindow(string page, bool compact)
    {
        XElement source = PresentationHarness.Source("MainWindow");
        XElement dock = new(source.Element(Wpf + "DockPanel"));
        dock.Elements().Single(e => e.Name.LocalName == "TaskbarIcon").Remove();
        // Avoid a localization extension resetting the fictional service state
        // on Loaded. The real StartStop handler is deliberately not present.
        dock.Descendants().Single(e => (string)e.Attribute(X + "Name") == "StartStopBtn")
            .SetAttributeValue("Content", "Stop");
        XElement tabMarkup = dock.Descendants(Wpf + "TabControl").First();
        string[] names = { "controllersTab", "profilesTab", "settingsTab", "toolsTab",
            "autoProfilesTab", "inputDiagnosticsTab", "outputSlotsTab", "logTab" };
        string selectedName = page switch
        {
            "settings" => "settingsTab", "tools" => "toolsTab", "autoprofiles" => "autoProfilesTab",
            "diagnostics" => "inputDiagnosticsTab", "outputslots" => "outputSlotsTab", "log" => "logTab",
            "profiles" or "editor" => "profilesTab", _ => "controllersTab",
        };
        foreach (XElement tab in tabMarkup.Elements().ToArray())
        {
            if (!names.Contains((string)tab.Attribute(X + "Name"))) { tab.Remove(); continue; }
            // Preview a stable English caption without running App's global
            // localization initialization. Read the actual product resources,
            // not the localization extension's missing-key placeholder.
            string resource = (string)tab.Attribute(X + "Name") switch
            {
                "profilesTab" => "Profiles", "settingsTab" => "Settings", _ => null,
            };
            if (resource != null) tab.SetAttributeValue("Header",
                DS4WinWPF.Translations.Strings.ResourceManager.GetString(resource, CultureInfo.InvariantCulture));
            XElement style = tab.Element(Wpf + "TabItem.Style");
            XElement toolWrapper = names.Skip(4).Contains((string)tab.Attribute(X + "Name"))
                && (string)tab.Attribute(X + "Name") == selectedName && page != "log"
                ? new XElement(tab.Element(Wpf + "Grid")) : null;
            if (toolWrapper != null)
                toolWrapper.Elements().Last().ReplaceWith(new XElement(Wpf + "ContentControl",
                    new XAttribute(X + "Name", "previewToolPageHost"), new XAttribute("Grid.Row", "1")));
            tab.RemoveNodes(); // Never instantiate product workspace constructors.
            tab.Attribute("IsEnabled")?.Remove(); // Fixture represents an available service.
            if (style != null) tab.Add(style);
            tab.Add(toolWrapper ?? new XElement(Wpf + "Grid"));
        }
        var wrapper = new XElement(Wpf + "UserControl", source.Attributes().Where(a =>
            a.IsNamespaceDeclaration || a.Name.LocalName == "Ignorable" ||
            a.Name.LocalName == "DefaultAssembly" || a.Name.LocalName == "DefaultDictionary" ||
            a.Name == XNamespace.Xml + "lang"), dock);
        UserControl shell = PresentationHarness.Parse(wrapper);
        var tabs = (TabControl)shell.FindName("mainTabCon");
        FrameworkElement workspace;
        if (page.StartsWith("home", StringComparison.Ordinal)) workspace = HomeWorkspaceTests.LoadHome();
        else if (page == "diagnostics") workspace = ToolsDiagnosticsPresentationTests.CreateDiagnosticsView();
        else if (page == "log") workspace = ToolsDiagnosticsPresentationTests.CreateLogView();
        else if (page == "outputslots") workspace = ToolsDiagnosticsPresentationTests.CreateOutputSlotsView();
        else if (page is "settings" or "tools") workspace = PresentationHarness.LoadTabWorkspace(page + "Tab");
        else if (page == "autoprofiles") workspace = PresentationHarness.LoadControl("AutoProfiles");
        else
        {
            UserControl view = page == "profiles"
                ? ProfileWorkspacePresentationTests.Load("MainWindow", "profilesBrowserPanel")
                : ProfileWorkspacePresentationTests.Load("ProfileEditor");
            // Same page margin as the production profiles DockPanel.
            XElement profileDock = PresentationHarness.Workspace(source, "profilesTab");
            view.Margin = (Thickness)new ThicknessConverter().ConvertFromInvariantString((string)profileDock.Attribute("Margin"));
            if (page == "profiles")
            {
                var list = (ListBox)view.FindName("profilesListBox");
                list.ItemsSource = new[]
                {
                    new { Name = "Default", GameOutputDisplay = "Xbox 360", ModifiedDisplay = "Preview" },
                    new { Name = "Steam games", GameOutputDisplay = "DS4", ModifiedDisplay = "Preview" },
                };
                list.SelectedIndex = 0;
                foreach (string name in new[] { "editProfBtn", "dupProfBtn", "renameProfBtn", "deleteProfBtn" })
                    ((Button)view.FindName(name)).IsEnabled = true;
            }
            else
            {
                view.DataContext = new ProfileWorkspacePresentationTests.EditorFixture();
                var profileName = (TextBox)view.FindName("profileNameTxt");
                profileName.Text = "Default";
                profileName.IsEnabled = false;
                foreach (string name in new[] { "picBoxHover", "picBoxHover2" })
                    ((UIElement)view.FindName(name)).Visibility = Visibility.Hidden;
                foreach (string name in new[] { "muteConBtn", "captureConBtn", "fnlConBtn", "fnrConBtn", "blpConBtn", "brpConBtn" })
                    ((UIElement)view.FindName(name)).Visibility = Visibility.Collapsed;
                ((Image)view.FindName("controllerDiagram")).Source = new BitmapImage(
                    new Uri("pack://application:,,,/PureDS4;component/Resources/DualShock 4 Controller.png"));
                foreach (Button target in ((Canvas)view.FindName("conCanvas")).Children.OfType<Button>()) target.Content = null;
                // MainWindow disables Home/Settings while editing a profile.
                ((TabItem)shell.FindName("controllersTab")).IsEnabled = false;
                ((TabItem)shell.FindName("settingsTab")).IsEnabled = false;
            }
            workspace = view;
        }
        var selectedTab = (TabItem)shell.FindName(selectedName);
        if (shell.FindName("previewToolPageHost") is ContentControl host) host.Content = workspace;
        else selectedTab.Content = workspace;
        selectedTab.Visibility = Visibility.Visible;
        tabs.SelectedItem = selectedTab;
        // Run the same pure navigation action as the production back handler.
        foreach (string name in new[] { "autoProfilesBackBtn", "inputDiagnosticsBackBtn", "outputSlotsBackBtn", "logBackBtn" })
            if ((shell.FindName(name) ?? (workspace as UserControl)?.FindName(name)) is Button back)
                back.Click += (_, _) => PrimaryNavigation.ReturnToTools(tabs);
        var notice = DS4Windows.ElevationNotice.Evaluate(false);
        var banner = (StatusBanner)shell.FindName("elevationStatusBanner");
        banner.Title = notice.Title;
        banner.Message = notice.Message;
        banner.ActionLabel = notice.ActionLabel;
        banner.IsOpen = true;
        ((TextBlock)shell.FindName("serviceStatusText")).Text = "Service running";
        ((Button)shell.FindName("StartStopBtn")).Content = "Stop";

        double Value(string attribute) => double.Parse((string)source.Attribute(attribute), CultureInfo.InvariantCulture);
        var window = new Window
        {
            Title = (string)source.Attribute("Title") + " — UI preview",
            Icon = BitmapFrame.Create(new Uri("pack://application:,,," + (string)source.Attribute("Icon"))),
            Width = Value(compact ? "MinWidth" : "Width"),
            Height = Value(compact ? "MinHeight" : "Height"),
            MinWidth = Value("MinWidth"), MinHeight = Value("MinHeight"),
            ShowActivated = false, ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            DataContext = page is "settings" or "tools" or "autoprofiles" ?
                new SettingsWorkspacePresentationTests.SettingsFixture() : HomeState(page),
            Content = shell,
        };
        window.SetResourceReference(FrameworkElement.StyleProperty, "WindowStyle");
        return window;
    }

    private static HomeWorkspaceTests.HomeFixture HomeState(string page)
    {
        if (page is not ("home-native" or "home-recovery")) return new(1);
        bool recovery = page == "home-recovery";
        return new(0)
        {
            NativePhysicalSessions = new object[] { new
            {
                ModeTitle = recovery ? "Recovery required" : "Using controller directly",
                DisplayName = "DualShock 4", SlotText = "Controller 1", Connection = "Bluetooth",
                Detail = recovery ? "Physical controller visibility could not be restored. Recover it before using it directly."
                    : "Physical controller is available to Steam and other apps. PureDS4 game output is off for this controller.",
                CanReturnToManaged = !recovery, CanRecover = recovery, InstanceId = "disposable-preview",
            } },
        };
    }

    private static UserControl ToolWorkspace(UserControl shell, TabControl tabs) =>
        shell.FindName("previewToolPageHost") is ContentControl host ? (UserControl)host.Content :
        (UserControl)((TabItem)tabs.SelectedItem).Content;

    [TestMethod]
    public void InputDiagnosticsPreviewUsesTheFullShellWithoutStartingInputMeasurement()
    {
        string output = Environment.GetEnvironmentVariable(OutputVariable);
        bool capture = !string.IsNullOrEmpty(output);
        if (capture) Assert.IsTrue(Path.IsPathFullyQualified(output) && Directory.Exists(output));
        foreach (bool dark in capture ? new[] { false, true } : new[] { true })
        foreach (bool compact in capture ? new[] { true, false } : new[] { true })
            WpfTestHost.Run(() =>
            {
                PresentationHarness.Theme(dark);
                Window window = CreateWindow("diagnostics", compact);
                try
                {
                    Assert.AreEqual(IntPtr.Zero, new WindowInteropHelper(window).Handle);
                    if (!capture) return;
                    Rect work = SystemParameters.WorkArea;
                    window.WindowStartupLocation = WindowStartupLocation.Manual;
                    window.Left = work.Left + Math.Max(0, (work.Width - window.Width) / 2);
                    window.Top = work.Top + Math.Max(0, (work.Height - window.Height) / 2);
                    window.Topmost = true;
                    window.Show(); PumpRendering(); window.UpdateLayout();
                    var tabs = (TabControl)((UserControl)window.Content).FindName("mainTabCon");
                    var view = ToolWorkspace((UserControl)window.Content, tabs);
                    foreach (string name in new[] { "copyReportBtn", "resetMeasurementsBtn" })
                        PresentationHarness.AssertReachable(view, (Button)view.FindName(name), "full shell diagnostics");
                    foreach (bool bottom in new[] { false, true })
                    {
                        var scroll = (ScrollViewer)view.FindName("diagnosticsScroll");
                        if (bottom) scroll.ScrollToEnd(); else scroll.ScrollToTop();
                        PumpRendering(); window.UpdateLayout();
                        var bitmap = CaptureOwnWindow(new WindowInteropHelper(window).Handle, out _);
                        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                        using var stream = File.Create(Path.Combine(output,
                            $"pureds4-window-diagnostics-{(int)window.Width}-{(dark ? "dark" : "light")}-{(bottom ? "bottom" : "top")}.png"));
                        encoder.Save(stream);
                    }
                }
                finally
                {
                    window.Close();
                    Assert.IsFalse(NativeWindowFrame.IsAttached(window));
                }
            });
    }

    [TestMethod]
    public void ControllerDetectionPreviewUsesOnlyDisposableStateAndItsOwnWindow()
    {
        string output = Environment.GetEnvironmentVariable(OutputVariable);
        bool capture = !string.IsNullOrEmpty(output);
        if (capture) Assert.IsTrue(Path.IsPathFullyQualified(output) && Directory.Exists(output));
        foreach (bool dark in capture ? new[] { false, true } : new[] { true })
        foreach (bool compact in capture ? new[] { true, false } : new[] { true })
            WpfTestHost.Run(() =>
            {
                PresentationHarness.Theme(dark);
                var window = new Window
                {
                    Title = "PureDS4 — Controller detection — UI preview",
                    Icon = BitmapFrame.Create(new Uri("pack://application:,,,/PureDS4;component/Resources/AppIcon.png")),
                    Width = compact ? 720 : 800, Height = compact ? 400 : 480,
                    ShowActivated = false, ShowInTaskbar = false,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    Content = ControllerDetectionPresentationTests.CreateView(),
                };
                window.SetResourceReference(FrameworkElement.StyleProperty, "WindowStyle");
                try
                {
                    Assert.AreEqual(IntPtr.Zero, new WindowInteropHelper(window).Handle);
                    Assert.IsTrue(NativeWindowFrame.IsAttached(window));
                    if (!capture) return;
                    window.Show(); PumpRendering(); window.UpdateLayout();
                    var bitmap = CaptureOwnWindow(new WindowInteropHelper(window).Handle, out _);
                    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var stream = File.Create(Path.Combine(output,
                        $"pureds4-window-detection-{(int)window.Width}-{(dark ? "dark" : "light")}.png"));
                    encoder.Save(stream);
                }
                finally
                {
                    window.Close();
                    Assert.IsFalse(NativeWindowFrame.IsAttached(window));
                }
            });
    }

    [TestMethod]
    public void ProfileLightbarPreviewUsesRealShellAndDisposableFields()
    {
        string output = Environment.GetEnvironmentVariable(OutputVariable);
        bool capture = !string.IsNullOrEmpty(output);
        if (capture) Assert.IsTrue(Path.IsPathFullyQualified(output) && Directory.Exists(output));
        foreach (bool dark in capture ? new[] { false, true } : new[] { true })
        foreach (bool compact in capture ? new[] { true, false } : new[] { true })
            WpfTestHost.Run(() =>
            {
                ProfileWorkspacePresentationTests.Theme(dark);
                Window window = CreateWindow("editor", compact);
                try
                {
                    var shell = (UserControl)window.Content;
                    var tabs = (TabControl)shell.FindName("mainTabCon");
                    var editor = (UserControl)((TabItem)tabs.SelectedItem).Content;
                    LightbarPresentationTests.Configure(editor);
                    Assert.AreEqual(IntPtr.Zero, new WindowInteropHelper(window).Handle);
                    if (!capture) return;
                    // Keep only this short-lived fixture above other windows.
                    // The overlap/privacy checks still apply; never move or
                    // minimize the owner's apps to obtain a screenshot.
                    window.Topmost = true;
                    window.Show(); PumpRendering(); window.UpdateLayout();
                    foreach (bool bottom in new[] { false, true })
                    {
                        var scroll = (ScrollViewer)editor.FindName("lightbarScroll");
                        if (bottom) scroll.ScrollToEnd(); else scroll.ScrollToTop();
                        PumpRendering(); window.UpdateLayout();
                        var bitmap = CaptureOwnWindow(new WindowInteropHelper(window).Handle, out _);
                        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                        using var stream = File.Create(Path.Combine(output,
                            $"pureds4-window-lightbar-{(int)window.Width}-{(dark ? "dark" : "light")}-{(bottom ? "bottom" : "top")}.png"));
                        encoder.Save(stream);
                    }
                }
                finally
                {
                    window.Close();
                    Assert.IsFalse(NativeWindowFrame.IsAttached(window));
                }
            });
    }

    [DataTestMethod]
    [DataRow("axis", 3)]
    [DataRow("touchpad", 5)]
    [DataRow("gyro", 6)]
    public void ProfileInputPreviewUsesRealShellAndDisposableModes(string page, int section)
    {
        string output = Environment.GetEnvironmentVariable(OutputVariable);
        bool capture = !string.IsNullOrEmpty(output);
        if (capture) Assert.IsTrue(Path.IsPathFullyQualified(output) && Directory.Exists(output));
        foreach (bool dark in capture ? new[] { false, true } : new[] { true })
        foreach (bool compact in capture ? new[] { true, false } : new[] { true })
            WpfTestHost.Run(() =>
            {
                ProfileWorkspacePresentationTests.Theme(dark);
                Window window = CreateWindow("editor", compact);
                try
                {
                    var tabs = (TabControl)((UserControl)window.Content).FindName("mainTabCon");
                    var editor = (UserControl)((TabItem)tabs.SelectedItem).Content;
                    var fixture = ProfileInputPresentationTests.Configure(editor, section);
                    fixture.MappingName = "Unassigned";
                    if (section == 6) fixture.GyroOutModeIndex = 1;
                    Assert.AreEqual(IntPtr.Zero, new WindowInteropHelper(window).Handle);
                    if (!capture) return;
                    window.Topmost = true;
                    window.Show(); PumpRendering(); window.UpdateLayout();
                    foreach (bool bottom in new[] { false, true })
                    {
                        var scroll = (ScrollViewer)editor.FindName(page + "PropertiesScroll");
                        if (bottom) scroll.ScrollToEnd(); else scroll.ScrollToTop();
                        PumpRendering(); window.UpdateLayout();
                        var bitmap = CaptureOwnWindow(new WindowInteropHelper(window).Handle, out _);
                        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                        using var stream = File.Create(Path.Combine(output,
                            $"pureds4-window-{page}-{(int)window.Width}-{(dark ? "dark" : "light")}-{(bottom ? "bottom" : "top")}.png"));
                        encoder.Save(stream);
                    }
                }
                finally
                {
                    window.Close();
                    Assert.IsFalse(NativeWindowFrame.IsAttached(window));
                }
            });
    }

    [DataTestMethod]
    [DataRow("advanced")]
    [DataRow("preset-intro")]
    [DataRow("preset")]
    [DataRow("rename")]
    [DataRow("duplicate")]
    [DataRow("calibration")]
    public void AdvancedAndProfileHelperPreviewsUseOnlyDisposableWindowState(string page)
    {
        string output = Environment.GetEnvironmentVariable(OutputVariable);
        bool capture = !string.IsNullOrEmpty(output);
        if (capture) Assert.IsTrue(Path.IsPathFullyQualified(output) && Directory.Exists(output));
        foreach (bool dark in capture ? new[] { false, true } : new[] { true })
        foreach (bool compact in capture && page != "calibration" ? new[] { true, false } : new[] { false })
            WpfTestHost.Run(() =>
            {
                ProfileWorkspacePresentationTests.Theme(dark);
                Window window;
                ScrollViewer properties = null;
                if (page is "advanced" or "duplicate")
                {
                    window = CreateWindow(page == "advanced" ? "editor" : "profiles", compact);
                    var tabs = (TabControl)((UserControl)window.Content).FindName("mainTabCon");
                    var view = (UserControl)((TabItem)tabs.SelectedItem).Content;
                    if (page == "advanced")
                    {
                        AdvancedProfilePresentationTests.Configure(view);
                        properties = (ScrollViewer)view.FindName("advancedPropertiesScroll");
                    }
                    else
                    {
                        var bar = (Border)view.FindName("dupBoxBar");
                        // Replace the inline control with detached markup before
                        // showing it: even an accidental click cannot copy a file.
                        bar.Child = AdvancedProfilePresentationTests.CreateHelper(page);
                        bar.Visibility = Visibility.Visible;
                    }
                }
                else
                {
                    string source = page.StartsWith("preset") ? "PresetOptionWindow" : page == "rename" ? "RenameProfileWindow" : "StickCalibrationWindow";
                    XElement markup = PresentationHarness.Source(source);
                    double Dimension(string name, string fallback) => double.Parse((string)(markup.Attribute(name) ?? markup.Attribute(fallback)), CultureInfo.InvariantCulture);
                    window = new Window
                    {
                        Title = "PureDS4 — " + (page.StartsWith("preset") ? "Select Preset" : page == "rename" ? "Rename Profile" : "Stick calibration") + " — UI preview",
                        Icon = BitmapFrame.Create(new Uri("pack://application:,,,/PureDS4;component/Resources/AppIcon.png")),
                        Width = Dimension(compact ? "MinWidth" : "Width", "Width"),
                        Height = Dimension(compact ? "MinHeight" : "Height", "Height"),
                        ResizeMode = page == "calibration" ? ResizeMode.NoResize : ResizeMode.CanResize,
                        ShowActivated = false, ShowInTaskbar = false,
                        WindowStartupLocation = WindowStartupLocation.CenterScreen,
                        Content = AdvancedProfilePresentationTests.CreateHelper(page),
                    };
                    window.SetResourceReference(FrameworkElement.StyleProperty, "WindowStyle");
                }
                try
                {
                    Assert.AreEqual(IntPtr.Zero, new WindowInteropHelper(window).Handle);
                    if (!capture) return;
                    window.Topmost = true;
                    window.Show(); PumpRendering(); window.UpdateLayout();
                    foreach (bool bottom in page == "advanced" ? new[] { false, true } : new[] { false })
                    {
                        if (bottom)
                        {
                            foreach (Expander expander in PresentationHarness.Descendants<Expander>(properties)) expander.IsExpanded = true;
                            properties.ScrollToEnd();
                        }
                        PumpRendering(); window.UpdateLayout();
                        var bitmap = CaptureOwnWindow(new WindowInteropHelper(window).Handle, out _);
                        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                        using var stream = File.Create(Path.Combine(output,
                            $"pureds4-window-{page}-{(int)window.Width}-{(dark ? "dark" : "light")}-{(bottom ? "bottom" : "top")}.png"));
                        encoder.Save(stream);
                    }
                }
                finally
                {
                    window.Close();
                    Assert.IsFalse(NativeWindowFrame.IsAttached(window));
                }
            });
    }

    [TestMethod]
    [DataRow("binding-controller")]
    [DataRow("binding-keyboard")]
    [DataRow("binding-extras")]
    [DataRow("macro")]
    [DataRow("macro-recording")]
    [DataRow("action-macro")]
    [DataRow("action-launch")]
    [DataRow("action-battery")]
    [DataRow("action-unload")]
    public void BindingMacroAndActionPreviewsUseOnlyDisposableWindowState(string page)
    {
        string output = Environment.GetEnvironmentVariable(OutputVariable);
        bool capture = !string.IsNullOrEmpty(output);
        if (capture) Assert.IsTrue(Path.IsPathFullyQualified(output) && Directory.Exists(output));
        foreach (bool dark in capture ? new[] { false, true } : new[] { true })
        foreach (bool compact in capture ? new[] { true, false } : new[] { false })
            WpfTestHost.Run(() =>
            {
                ProfileWorkspacePresentationTests.Theme(dark);
                Window window;
                UserControl view;
                if (page.StartsWith("action"))
                {
                    window = CreateWindow("editor", compact);
                    var tabs = (TabControl)((UserControl)window.Content).FindName("mainTabCon");
                    var editor = (UserControl)((TabItem)tabs.SelectedItem).Content;
                    int index = page == "action-launch" ? 2 : page == "action-battery" ? 6 : page == "action-unload" ? 3 : 1;
                    view = BindingMacroActionPresentationTests.ConfigureActionShell(editor, index);
                    if (page == "action-unload")
                    {
                        ((ListView)view.FindName("triggersListView")).Visibility = Visibility.Collapsed;
                        ((ListView)view.FindName("unloadTriggersListView")).Visibility = Visibility.Visible;
                    }
                }
                else
                {
                    bool binding = page.StartsWith("binding");
                    XElement markup = PresentationHarness.Source(binding ? "BindingWindow" : "RecordBoxWindow");
                    double Dimension(string name) => double.Parse((string)markup.Attribute(name), CultureInfo.InvariantCulture);
                    view = binding ? BindingMacroActionPresentationTests.CreateBinding(page) :
                        BindingMacroActionPresentationTests.CreateMacro(page == "macro-recording");
                    window = new Window
                    {
                        Title = "PureDS4 — " + (binding ? "Select action for Cross" : "Macro") + " — UI preview",
                        Icon = BitmapFrame.Create(new Uri("pack://application:,,,/PureDS4;component/Resources/AppIcon.png")),
                        Width = Dimension(compact ? "MinWidth" : "Width"),
                        Height = Dimension(compact ? "MinHeight" : "Height"),
                        ShowActivated = false, ShowInTaskbar = false,
                        WindowStartupLocation = WindowStartupLocation.CenterScreen, Content = view,
                    };
                    window.SetResourceReference(FrameworkElement.StyleProperty, "WindowStyle");
                }
                try
                {
                    Assert.AreEqual(IntPtr.Zero, new WindowInteropHelper(window).Handle);
                    if (!capture) return;
                    // Keep this disposable preview inside the primary work area,
                    // away from shell surfaces on a secondary monitor. All
                    // overlap/hit-target privacy checks still apply.
                    Rect work = SystemParameters.WorkArea;
                    window.WindowStartupLocation = WindowStartupLocation.Manual;
                    window.Left = work.Left + Math.Max(0, (work.Width - window.Width) / 2);
                    window.Top = work.Top + Math.Max(0, (work.Height - window.Height) / 2);
                    window.Topmost = true;
                    window.Show(); PumpRendering(); window.UpdateLayout();
                    var bitmap = CaptureOwnWindow(new WindowInteropHelper(window).Handle, out _);
                    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var stream = File.Create(Path.Combine(output,
                        $"pureds4-window-{page}-{(int)window.Width}-{(dark ? "dark" : "light")}.png"));
                    encoder.Save(stream);
                }
                finally
                {
                    window.Close();
                    Assert.IsFalse(NativeWindowFrame.IsAttached(window));
                }
            });
    }

    [TestMethod]
    [DataRow("setup-required")]
    [DataRow("setup-repair")]
    [DataRow("setup-existing")]
    [DataRow("setup-conflict")]
    [DataRow("progress-running")]
    [DataRow("progress-attention")]
    [DataRow("exposure")]
    [DataRow("app-warning")]
    public void SetupDialogPreviewsNeverStartTheProductOrARepair(string page)
    {
        string output = Environment.GetEnvironmentVariable(OutputVariable);
        bool capture = !string.IsNullOrEmpty(output);
        if (capture) Assert.IsTrue(Path.IsPathFullyQualified(output) && Directory.Exists(output));
        foreach (bool dark in capture ? new[] { false, true } : new[] { true })
        foreach (bool compact in capture ? new[] { true, false } : new[] { true })
            WpfTestHost.Run(() =>
            {
                PresentationHarness.Theme(dark);
                string sourceName = page.StartsWith("setup") ? "ViiperSetupPrompt" : page.StartsWith("progress") ? "ViiperSetupProgress" :
                    page == "exposure" ? "ControllerExposureDialog" : "AppDialog";
                XElement source = PresentationHarness.Source(sourceName);
                UserControl view = SetupDialogPresentationTests.CreateView(page);
                if (page is "setup-repair" or "setup-existing")
                    ((Expander)view.FindName("technicalDetailsExpander")).IsExpanded = true;
                PreviewDialog(source, view, page, compact, dark, output,
                    page.StartsWith("setup") ? 360 : 300, page.StartsWith("progress"));
            });
    }

    [TestMethod]
    public void MigrationDialogPreviewsNeverScanImportRemoveOrSelectStorage()
    {
        string output = Environment.GetEnvironmentVariable(OutputVariable);
        bool capture = !string.IsNullOrEmpty(output);
        if (capture) Assert.IsTrue(Path.IsPathFullyQualified(output) && Directory.Exists(output));
        string[] states = capture ? MigrationDialogPresentationTests.States :
            new[] { "import-results", "removal-blocked", "running-recheck", "storage-protected" };
        foreach (string state in states)
        foreach (bool dark in capture ? new[] { false, true } : new[] { true })
        foreach (bool compact in capture ? new[] { true, false } : new[] { true })
            WpfTestHost.Run(() =>
            {
                PresentationHarness.Theme(dark);
                XElement source = PresentationHarness.Source(MigrationDialogPresentationTests.SourceName(state));
                UserControl view = MigrationDialogPresentationTests.CreateView(state);
                PreviewDialog(source, view, state, compact, dark, output, 340);
            });
    }

    [DataTestMethod]
    [DataRow("overview", 0)]
    [DataRow("shortcuts", 1)]
    [DataRow("credits", 2)]
    public void AboutPreviewsUseDetachedTabsAndOnlyTheirOwnWindow(string page, int section)
    {
        string output = Environment.GetEnvironmentVariable(OutputVariable);
        bool capture = !string.IsNullOrEmpty(output);
        if (capture) Assert.IsTrue(Path.IsPathFullyQualified(output) && Directory.Exists(output));
        foreach (bool dark in capture ? new[] { false, true } : new[] { true })
        foreach (bool compact in capture ? new[] { true, false } : new[] { true })
            WpfTestHost.Run(() =>
            {
                PresentationHarness.Theme(dark);
                XElement source = PresentationHarness.Source("About");
                UserControl view = ToolsDiagnosticsPresentationTests.CreateAboutView(section);
                // Match the real dialog's fixed default/minimum heights rather
                // than turning About into an unbounded SizeToContent window.
                double Dimension(string name) => double.Parse((string)source.Attribute(name), CultureInfo.InvariantCulture);
                var window = new Window
                {
                    Title = "About PureDS4 — UI preview",
                    Icon = BitmapFrame.Create(new Uri("pack://application:,,,/PureDS4;component/Resources/DS4W.ico")),
                    Width = Dimension(compact ? "MinWidth" : "Width"),
                    Height = Dimension(compact ? "MinHeight" : "Height"),
                    ShowActivated = false, ShowInTaskbar = false, Content = view,
                };
                window.SetResourceReference(FrameworkElement.StyleProperty, "WindowStyle");
                try
                {
                    Assert.AreEqual(IntPtr.Zero, new WindowInteropHelper(window).Handle);
                    if (!capture) return;
                    Rect work = SystemParameters.WorkArea;
                    window.WindowStartupLocation = WindowStartupLocation.Manual;
                    window.Left = work.Left + Math.Max(0, (work.Width - window.Width) / 2);
                    window.Top = work.Top + Math.Max(0, (work.Height - window.Height) / 2);
                    window.Topmost = true;
                    window.Show(); PumpRendering(); window.UpdateLayout();
                    PresentationHarness.AssertReachable(view, (Button)view.FindName("aboutCloseBtn"), "About preview footer");
                    var bitmap = CaptureOwnWindow(new WindowInteropHelper(window).Handle, out _);
                    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var stream = File.Create(Path.Combine(output,
                        $"pureds4-window-about-{page}-{(int)window.Width}-{(dark ? "dark" : "light")}.png"));
                    encoder.Save(stream);
                }
                finally
                {
                    window.Close();
                    Assert.IsFalse(NativeWindowFrame.IsAttached(window));
                }
            });
    }

    [DataTestMethod]
    [DataRow("log")]
    [DataRow("outputslots")]
    public void OutputAndLogPreviewsUseTheFullShellWithoutLiveTools(string page)
    {
        string output = Environment.GetEnvironmentVariable(OutputVariable);
        bool capture = !string.IsNullOrEmpty(output);
        if (capture) Assert.IsTrue(Path.IsPathFullyQualified(output) && Directory.Exists(output));
        foreach (bool dark in capture ? new[] { false, true } : new[] { true })
        foreach (bool compact in capture ? new[] { true, false } : new[] { true })
            WpfTestHost.Run(() =>
            {
                PresentationHarness.Theme(dark);
                Window window = CreateWindow(page, compact);
                try
                {
                    Assert.AreEqual(IntPtr.Zero, new WindowInteropHelper(window).Handle);
                    if (!capture) return;
                    Rect work = SystemParameters.WorkArea;
                    window.WindowStartupLocation = WindowStartupLocation.Manual;
                    // Keep this test-owned preview away from the desktop center,
                    // where transient overlays repeatedly interrupted captures.
                    // Other windows are never moved or dismissed.
                    window.Left = work.Left + Math.Max(0, work.Width - window.Width - 24);
                    window.Top = work.Top + Math.Max(0, (work.Height - window.Height) / 2);
                    window.Topmost = true;
                    window.Show(); PumpRendering(); window.UpdateLayout();
                    var shell = (UserControl)window.Content;
                    var tabs = (TabControl)shell.FindName("mainTabCon");
                    var view = ToolWorkspace(shell, tabs);
                    ToolsDiagnosticsPresentationTests.AssertToolsReachable(view, "full shell " + page);
                    var bitmap = CaptureOwnWindow(new WindowInteropHelper(window).Handle, out _);
                    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var stream = File.Create(Path.Combine(output,
                        $"pureds4-window-{page}-{(int)window.Width}-{(dark ? "dark" : "light")}.png"));
                    encoder.Save(stream);
                    if (page == "log")
                    {
                        ((ListView)view.FindName("logListView")).ItemsSource = System.Array.Empty<object>();
                        PumpRendering(); window.UpdateLayout();
                        ToolsDiagnosticsPresentationTests.AssertToolsReachable(view, "full shell empty log");
                        var empty = CaptureOwnWindow(new WindowInteropHelper(window).Handle, out _);
                        var emptyEncoder = new PngBitmapEncoder(); emptyEncoder.Frames.Add(BitmapFrame.Create(empty));
                        using var emptyStream = File.Create(Path.Combine(output,
                            $"pureds4-window-log-empty-{(int)window.Width}-{(dark ? "dark" : "light")}.png"));
                        emptyEncoder.Save(emptyStream);
                    }
                }
                finally
                {
                    window.Close();
                    Assert.IsFalse(NativeWindowFrame.IsAttached(window));
                }
            });
    }

    [TestMethod]
    public void PlugOutputPreviewUsesDetachedChoicesWithoutCreatingVirtualOutput()
    {
        string output = Environment.GetEnvironmentVariable(OutputVariable);
        bool capture = !string.IsNullOrEmpty(output);
        if (capture) Assert.IsTrue(Path.IsPathFullyQualified(output) && Directory.Exists(output));
        foreach (bool dark in capture ? new[] { false, true } : new[] { true })
        foreach (bool compact in capture ? new[] { true, false } : new[] { true })
            WpfTestHost.Run(() =>
            {
                PresentationHarness.Theme(dark);
                PreviewDialog(PresentationHarness.Source("PluginOutDevWindow"),
                    PresentationHarness.LoadControl("PluginOutDevWindow"), "plugoutput", compact, dark, output,
                    350, fixedHeight: true);
            });
    }

    [TestMethod]
    [DataRow("swipe")]
    [DataRow("lightbar-sequence")]
    [DataRow("log-message")]
    [DataRow("release-notes-long")]
    [DataRow("release-notes")]
    public void RemainingHelperPreviewsUseDetachedListsAndDocuments(string page)
    {
        string output = Environment.GetEnvironmentVariable(OutputVariable);
        bool capture = !string.IsNullOrEmpty(output);
        if (capture) Assert.IsTrue(Path.IsPathFullyQualified(output) && Directory.Exists(output));
        foreach (bool dark in capture ? new[] { false, true } : new[] { true })
        foreach (bool compact in capture ? new[] { true, false } : new[] { true })
            WpfTestHost.Run(() =>
            {
                PresentationHarness.Theme(dark);
                string name = page == "swipe" ? "SwipeProfilesEditor" : page == "lightbar-sequence" ? "LightbarMacroCreator" :
                    page == "log-message" ? "LogMessageDisplay" : "ChangelogWindow";
                XElement source = PresentationHarness.Source(name);
                UserControl view = page is "swipe" or "lightbar-sequence" ? BindingMacroActionPresentationTests.CreateProfileHelper(page) :
                    ToolsDiagnosticsPresentationTests.CreateDocumentView(name, page != "release-notes");
                double height = double.Parse((string)source.Attribute("MinHeight"), CultureInfo.InvariantCulture);
                PreviewDialog(source, view, page, compact, dark, output, height, fixedHeight: true);
            });
    }

    // Shared by detached setup/migration fixtures. The privacy/occlusion checks
    // stay in CaptureOwnWindow; an ordinary test never shows or captures a window.
    private static void PreviewDialog(XElement source, UserControl view, string page,
        bool compact, bool dark, string output, double compactHeight, bool disableClose = false, bool fixedHeight = false)
    {
        double width = double.Parse((string)source.Attribute(compact ? "MinWidth" : "Width"), CultureInfo.InvariantCulture);
        string title = (string)source.Attribute("Title");
        const string localizationPrefix = "{lex:Loc ";
        if (title.StartsWith(localizationPrefix, StringComparison.Ordinal) && title.EndsWith('}'))
        {
            string key = title.Substring(localizationPrefix.Length, title.Length - localizationPrefix.Length - 1);
            title = DS4WinWPF.Translations.Strings.ResourceManager.GetString(key, CultureInfo.InvariantCulture);
            Assert.IsFalse(string.IsNullOrEmpty(title), "Resolve the real dialog title before rendering a preview.");
        }
        var window = new Window
        {
            Title = title + " — UI preview",
            Icon = BitmapFrame.Create(new Uri("pack://application:,,,/PureDS4;component/Resources/AppIcon.png")),
            Width = width, MaxHeight = SystemParameters.WorkArea.Height,
            ResizeMode = (ResizeMode)Enum.Parse(typeof(ResizeMode), (string)source.Attribute("ResizeMode")),
            ShowActivated = false, ShowInTaskbar = false, Content = view,
            SizeToContent = compact || fixedHeight ? SizeToContent.Manual : SizeToContent.Height,
        };
        if (compact) window.Height = compactHeight;
        else if (fixedHeight) window.Height = double.Parse((string)source.Attribute("Height"), CultureInfo.InvariantCulture);
        window.SetResourceReference(FrameworkElement.StyleProperty, "WindowStyle");
        if (disableClose)
            window.SourceInitialized += (_, _) => Assert.IsTrue(NativeWindowFrame.SetCloseCommandEnabled(window, false));
        try
        {
            Assert.AreEqual(IntPtr.Zero, new WindowInteropHelper(window).Handle);
            if (string.IsNullOrEmpty(output)) return;
            Rect work = SystemParameters.WorkArea;
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = work.Left + Math.Max(0, (work.Width - window.Width) / 2);
            window.Top = fixedHeight ? work.Top + Math.Max(0, (work.Height - window.Height) / 2) : work.Top + 64;
            window.Topmost = true;
            window.Show(); PumpRendering(); window.UpdateLayout();
            var bitmap = CaptureOwnWindow(new WindowInteropHelper(window).Handle, out _);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(Path.Combine(output,
                $"pureds4-window-{page}-{(int)window.Width}-{(dark ? "dark" : "light")}.png"));
            encoder.Save(stream);
        }
        finally
        {
            window.Close();
            Assert.IsFalse(NativeWindowFrame.IsAttached(window));
        }
    }

    private static void PumpRendering()
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(200) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        try { timer.Start(); Dispatcher.PushFrame(frame); }
        finally { timer.Stop(); }
    }

    /// <summary>Only this fixture's HWND; never the desktop or another app.</summary>
    private static BitmapSource CaptureOwnWindow(IntPtr handle, out NativeRect bounds)
    {
        Assert.AreNotEqual(IntPtr.Zero, handle);
        Assert.IsTrue(GetWindowRect(handle, out NativeRect rect));
        // DWM's visible frame excludes invisible resize margins, which would
        // otherwise expose strips of unrelated windows around this fixture.
        Assert.AreEqual(0, DwmGetWindowAttribute(handle, 9, out bounds, Marshal.SizeOf<NativeRect>()));
        Assert.IsTrue(bounds.Left >= rect.Left && bounds.Top >= rect.Top &&
            bounds.Right <= rect.Right && bounds.Bottom <= rect.Bottom, "Native coordinate spaces must agree.");
        int width = bounds.Right - bounds.Left, height = bounds.Bottom - bounds.Top;
        Assert.IsTrue(width > 0 && height > 0 && width <= 8192 && height <= 8192);
        IntPtr dc = CreateCompatibleDC(IntPtr.Zero);
        Assert.AreNotEqual(IntPtr.Zero, dc);
        IntPtr bitmap = IntPtr.Zero, previous = IntPtr.Zero, sourceDc = IntPtr.Zero;
        try
        {
            // A WM_PRINT/PrintWindow image can synthesize legacy non-client
            // chrome rather than the composited Windows 11 caption. Read this
            // visible fixture's own window DC, never a desktop-wide bitmap.
            // Refuse any visible, non-cloaked window above the fixture whose
            // bounds intersect it, including overlays between sample points.
            var visited = new HashSet<IntPtr>();
            for (IntPtr above = GetWindow(handle, 3); above != IntPtr.Zero; above = GetWindow(above, 3))
            {
                Assert.IsTrue(visited.Add(above), "Window stacking order changed during capture.");
                if (!IsWindowVisible(above)) continue;
                if (DwmGetWindowAttribute(above, 14, out int cloaked, sizeof(int)) == 0 && cloaked != 0) continue;
                if (!GetWindowRect(above, out NativeRect other)) continue;
                Assert.IsFalse(other.Left < bounds.Right && other.Right > bounds.Left &&
                    other.Top < bounds.Bottom && other.Bottom > bounds.Top,
                    "Another window overlaps the preview; refuse capture.");
            }
            // Also check current hit targets in case the stacking order changed.
            for (int y = 10; y < height - 10; y += 20)
            for (int x = 10; x < width - 10; x += 20)
                Assert.AreEqual(handle, GetAncestor(WindowFromPoint(new NativePoint { X = bounds.Left + x, Y = bounds.Top + y }), 2),
                    $"The preview is occluded at ({bounds.Left + x}, {bounds.Top + y}) within " +
                    $"({bounds.Left}, {bounds.Top})–({bounds.Right}, {bounds.Bottom}); refuse to capture another app's pixels.");
            sourceDc = GetWindowDC(handle);
            Assert.AreNotEqual(IntPtr.Zero, sourceDc);
            var info = new BitmapInfoHeader
            {
                Size = (uint)Marshal.SizeOf<BitmapInfoHeader>(), Width = width, Height = -height,
                Planes = 1, BitCount = 32,
            };
            bitmap = CreateDIBSection(dc, ref info, 0, out IntPtr pixels, IntPtr.Zero, 0);
            Assert.AreNotEqual(IntPtr.Zero, bitmap);
            previous = SelectObject(dc, bitmap);
            Assert.IsTrue(previous != IntPtr.Zero && previous != new IntPtr(-1));
            Assert.IsTrue(BitBlt(dc, 0, 0, width, height, sourceDc, bounds.Left - rect.Left, bounds.Top - rect.Top, 0x00CC0020),
                "Full-window capture must include the composited native caption.");
            var bytes = new byte[checked(width * height * 4)];
            Marshal.Copy(pixels, bytes, 0, bytes.Length);
            // GDI leaves alpha undefined. The captured window is opaque.
            for (int i = 3; i < bytes.Length; i += 4) bytes[i] = 255;
            var result = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, bytes, width * 4);
            result.Freeze();
            return result;
        }
        finally
        {
            if (previous != IntPtr.Zero && previous != new IntPtr(-1)) SelectObject(dc, previous);
            if (bitmap != IntPtr.Zero) DeleteObject(bitmap);
            if (sourceDc != IntPtr.Zero) ReleaseDC(handle, sourceDc);
            DeleteDC(dc);
        }
    }

    private static void AssertPixel(BitmapSource bitmap, NativeRect bounds, Point screenPoint, Color expected)
    {
        var pixel = new byte[4];
        bitmap.CopyPixels(new Int32Rect((int)screenPoint.X - bounds.Left, (int)screenPoint.Y - bounds.Top, 1, 1), pixel, 4, 0);
        Assert.IsTrue(Math.Abs(pixel[0] - expected.B) <= 3 && Math.Abs(pixel[1] - expected.G) <= 3 &&
            Math.Abs(pixel[2] - expected.R) <= 3, "Native capture must contain the rendered selection cue.");
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        public uint Size;
        public int Width, Height;
        public ushort Planes, BitCount;
        public uint Compression, SizeImage;
        public int XPelsPerMeter, YPelsPerMeter;
        public uint ColorsUsed, ColorsImportant;
    }
    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out NativeRect rect, int size);
    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out int value, int size);
    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr window, uint command);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr window, out NativeRect rect);
    [DllImport("user32.dll")]
    private static extern IntPtr GetWindowDC(IntPtr window);
    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr window, IntPtr dc);
    [DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(NativePoint point);
    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr window, uint flags);
    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BitBlt(IntPtr destination, int x, int y, int width, int height,
        IntPtr source, int sourceX, int sourceY, uint operation);
    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateDIBSection(IntPtr dc, ref BitmapInfoHeader info, uint usage,
        out IntPtr pixels, IntPtr section, uint offset);
    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteDC(IntPtr dc);
}
