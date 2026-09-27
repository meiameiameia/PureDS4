using DS4WinWPF.DS4Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;

namespace DS4WindowsTests;

/// <summary>
/// Measure the real Home markup with disposable presentation fixtures, never
/// MainWindow/App startup (which owns hardware, profiles, and tray resources).
/// Native monitor DPI and owner visual acceptance remain separate checks.
/// </summary>
[TestClass]
[DoNotParallelize]
public class HomeWorkspaceTests
{
    [TestMethod]
    public void HomeKeepsRawControllerIdentifierOutOfTheEverydayView()
    {
        XElement home = HomeSource();
        Assert.IsFalse(home.DescendantsAndSelf().Attributes()
            .Any(attribute => attribute.Value.Contains("IdText", StringComparison.Ordinal)),
            "Controller IDs belong in technical details, not Home rows or the summary.");
    }

    [TestMethod]
    public void HomeFitsBothViewportsWithZeroOneTwoAndFourControllers()
    {
        WpfTestHost.Run(() =>
        {
            foreach (var dark in new[] { false, true })
            {
                LoadTheme(dark);
                foreach (var size in new[] { new Size(704, 360), new Size(1008, 540) })
                foreach (var count in new[] { 0, 1, 2, 4 })
                foreach (var degraded in new[] { false, true })
                {
                    var state = new HomeFixture(count) { Degraded = degraded };
                    Grid home = LoadHome();
                    var owner = new Window { DataContext = state, Content = home };
                    var list = (ListView)home.FindName("controllerLV");
                    list.DataContext = state;
                    Measure(home, size);
                    var inspector = (Border)home.FindName("homeInspector");
                    Assert.AreEqual(count > 0, inspector.IsEnabled);
                    Assert.AreEqual(count > 0 ? Visibility.Visible : Visibility.Collapsed, inspector.Visibility);
                    AssertInside(home, inspector, "selected controller inspector");
                    Assert.AreEqual(count, list.Items.Count);
                    var rows = Descendants<ListViewItem>(list).ToArray();
                    Assert.AreEqual(count, rows.Length);
                    foreach (var row in rows)
                    {
                        Assert.IsTrue(row.ActualHeight >= 48);
                        foreach (var control in Descendants<Control>(row).Where(c => c.Visibility == Visibility.Visible))
                            AssertInside(row, control, control.GetType().Name);
                    }
                    // Real WPF header and cell geometry must agree, even with a scrollbar.
                    var grids = Descendants<Grid>(list).Where(g => g.ColumnDefinitions.Count == 5).ToArray();
                    foreach (var grid in grids.Skip(1))
                        for (int i = 0; i < 5; i++)
                            Assert.AreEqual(grids[0].ColumnDefinitions[i].ActualWidth,
                                grid.ColumnDefinitions[i].ActualWidth, 1.0, $"Column {i}");

                    if (count > 0)
                    {
                        list.SelectedIndex = count - 1;
                        Measure(home, size);
                        Assert.AreSame(state.ControllerCol[count - 1], state.SelectedController);
                        var buttons = Descendants<Button>(inspector).ToArray();
                        var disconnect = buttons.Single(b => Equals(b.Content, "Disconnect"));
                        Assert.AreEqual(state.SelectedController.IsWireless ? Visibility.Visible : Visibility.Collapsed,
                            disconnect.Visibility);
                        foreach (var button in buttons.Where(b => b.Visibility == Visibility.Visible))
                            AssertInside(home, button, button.Content?.ToString());
                        var actions = (ActionGroupPanel)home.FindName("homeActions");
                        var firstGroup = (StackPanel)actions.Children[0];
                        var secondGroup = (StackPanel)actions.Children[1];
                        Rect left = firstGroup.TransformToAncestor(actions).TransformBounds(new Rect(firstGroup.RenderSize));
                        Rect right = secondGroup.TransformToAncestor(actions).TransformBounds(new Rect(secondGroup.RenderSize));
                        Assert.IsTrue(left.Right + 15 <= right.Left || left.Bottom + 7 <= right.Top,
                            "Profile and connection actions must remain distinct, non-overlapping groups.");
                        var profileButton = (Button)home.FindName("homeProfileMenuButton");
                        var menu = profileButton.ContextMenu;
                        // A ContextMenu is outside the visual tree. Its action target must
                        // track PlacementTarget, including a subsequent controller selection.
                        menu.PlacementTarget = profileButton;
                        Measure(menu, new Size(200, 60));
                        var newProfile = (MenuItem)menu.Items[0];
                        Assert.AreEqual("New profile", newProfile.Header);
                        Assert.AreSame(state.SelectedController, newProfile.DataContext);
                        list.SelectedIndex = 0;
                        Measure(home, size);
                        Assert.AreSame(state.SelectedController, newProfile.DataContext);
                        if (!degraded && ((dark && count == 4 && size.Width == 704) ||
                            (!dark && count == 2 && size.Width == 1008)))
                            SavePreviewIfRequested(home, dark);
                        // Clearing selection, as on stop/removal, must disable
                        // actions rather than retaining a stale device target.
                        list.SelectedIndex = -1;
                        Measure(home, size);
                        Assert.IsNull(state.SelectedController);
                        Assert.IsNull(newProfile.DataContext, "Profile menu must not retain a disconnected target.");
                        Assert.IsFalse(inspector.IsEnabled);
                        Assert.AreEqual(Visibility.Collapsed, inspector.Visibility,
                            "No selected controller must mean no output claim or stale actions.");
                    }
                    owner.Content = null;
                }
            }
        });
    }

    [TestMethod]
    public void ActionGroupsWrapAsUnitsAndReturnToOneLineWhenSpaceReturns()
    {
        WpfTestHost.Run(() =>
        {
            var panel = new ActionGroupPanel();
            var first = new Border { Width = 260, Height = 28 };
            var second = new Border { Width = 240, Height = 32 };
            panel.Children.Add(first);
            panel.Children.Add(second);
            foreach (double width in new[] { 600d, 500d, 516d, 400d, 600d })
            {
                panel.Measure(new Size(width, double.PositiveInfinity));
                bool stacked = width < 516;
                Assert.AreEqual(stacked ? 68d : 32d, panel.DesiredSize.Height);
                panel.Arrange(new Rect(0, 0, width, panel.DesiredSize.Height));
                Assert.AreEqual(new Point(0, 0), first.TranslatePoint(new Point(), panel));
                Assert.AreEqual(new Point(width - 240, stacked ? 36 : 0), second.TranslatePoint(new Point(), panel));
                AssertInside(panel, first, "leading group");
                AssertInside(panel, second, "trailing group");
            }
        });
    }

    [TestMethod]
    public void ColorDialogKeepsLiveRgbAndHexBehaviorWithReadableThreeDigitFields()
    {
        WpfTestHost.Run(() =>
        {
            foreach (bool dark in new[] { false, true })
            {
                LoadTheme(dark);
                var dialog = new ColorPickerWindow();
                Assert.AreEqual("Lightbar color", dialog.Title);
                Assert.AreEqual(SizeToContent.Height, dialog.SizeToContent);
                var content = (FrameworkElement)dialog.Content;
                content.Measure(new Size(344, double.PositiveInfinity));
                content.Arrange(new Rect(new Size(344, content.DesiredSize.Height)));
                content.UpdateLayout();
                var canvas = (Xceed.Wpf.Toolkit.ColorCanvas)dialog.FindName("colorPicker");
                Assert.IsFalse(canvas.UsingAlphaChannel);
                canvas.SelectedColor = Colors.Blue;
                Color? observed = null;
                dialog.ColorChanged += (_, color) => observed = color;
                foreach (string channel in new[] { "R", "G", "B" })
                {
                    var field = (Xceed.Wpf.Toolkit.ByteUpDown)canvas.Template.FindName(channel + "Value", canvas);
                    Assert.IsNotNull(field);
                    field.SetCurrentValue(Xceed.Wpf.Toolkit.ByteUpDown.ValueProperty, (byte?)0);
                    field.SetCurrentValue(Xceed.Wpf.Toolkit.ByteUpDown.ValueProperty, (byte?)255);
                    content.UpdateLayout();
                    Assert.IsNotNull(observed, "RGB changes must keep raising the live event.");
                    var text = (TextBox)field.Template.FindName("PART_TextBox", field);
                    Assert.IsNotNull(text);
                    Assert.AreEqual("255", text.Text);
                    var formatted = new FormattedText(text.Text, System.Globalization.CultureInfo.CurrentCulture,
                        text.FlowDirection, new Typeface(text.FontFamily, text.FontStyle, text.FontWeight, text.FontStretch),
                        text.FontSize, text.Foreground, VisualTreeHelper.GetDpi(text).PixelsPerDip);
                    Assert.IsTrue(text.ActualWidth - text.Padding.Left - text.Padding.Right >= formatted.Width,
                        $"{channel}: three digits must fit beside the spinner.");
                }
                canvas.HexadecimalString = "#12ABEF";
                content.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.DataBind);
                content.UpdateLayout();
                Assert.AreEqual(Color.FromRgb(0x12, 0xAB, 0xEF), canvas.SelectedColor);
                Assert.AreEqual(canvas.SelectedColor, observed);
                Assert.AreEqual((byte?)0x12, ((Xceed.Wpf.Toolkit.ByteUpDown)canvas.Template.FindName("RValue", canvas)).Value);
                Assert.AreEqual((byte?)0xAB, ((Xceed.Wpf.Toolkit.ByteUpDown)canvas.Template.FindName("GValue", canvas)).Value);
                Assert.AreEqual((byte?)0xEF, ((Xceed.Wpf.Toolkit.ByteUpDown)canvas.Template.FindName("BValue", canvas)).Value);
                Assert.AreEqual("#12ABEF", ((TextBox)canvas.Template.FindName("PART_HexadecimalTextBox", canvas)).Text);
                SavePreviewIfRequested(content, dark, dark ? "pureds4-color-wpf-dark.png" : "pureds4-color-wpf-light.png");
                Color? beforeClose = observed;
                dialog.Close();
                Assert.AreEqual(beforeClose, observed, "Closing must not emit a rollback color.");
            }
        });
    }

    [TestMethod]
    public void LightbarChoiceMenuUsesSharedCheckedAndKeyboardStates()
    {
        WpfTestHost.Run(() =>
        {
            foreach (bool dark in new[] { false, true })
            {
                LoadTheme(dark);
                var menu = new ContextMenu { Style = (Style)Application.Current.FindResource("FoundationChoiceMenuStyle") };
                var choice = new MenuItem { Header = "Use Profile Controls", IsChecked = true };
                menu.Items.Add(choice);
                menu.Items.Add(new MenuItem { Header = "Use Custom Color" });
                Measure(menu, new Size(220, 100));
                Assert.AreSame(Application.Current.FindResource("FoundationChoiceMenuItemStyle"), choice.Style);
                Assert.AreEqual(Visibility.Visible,
                    ((TextBlock)choice.Template.FindName("CheckGlyph", choice)).Visibility);
                choice.IsChecked = false;
                Assert.AreEqual(Visibility.Hidden,
                    ((TextBlock)choice.Template.FindName("CheckGlyph", choice)).Visibility);
                Assert.AreEqual(KeyboardNavigationMode.Cycle,
                    KeyboardNavigation.GetDirectionalNavigation(Descendants<ItemsPresenter>(menu).Single()));
            }
        });
    }

    [TestMethod]
    public void ChildInputSelectsOwningRowWithoutConsumingInputAndCanDetach()
    {
        WpfTestHost.Run(() =>
        {
            var list = new ListView { SelectionMode = SelectionMode.Single };
            var first = new ListViewItem { Content = new Button() };
            var second = new ListViewItem { Content = new Button() };
            list.Items.Add(first);
            list.Items.Add(second);
            ControllerRowSelection.SetSelectOnInteraction(second, true);
            first.IsSelected = true;
            var mouse = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
                { RoutedEvent = Mouse.PreviewMouseDownEvent };
            ((Button)second.Content).RaiseEvent(mouse);
            Assert.AreSame(second, list.SelectedItem);
            Assert.IsFalse(mouse.Handled);
            first.IsSelected = true;
            var focus = new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice, 0,
                (Button)first.Content, (Button)second.Content)
                { RoutedEvent = Keyboard.PreviewGotKeyboardFocusEvent };
            ((Button)second.Content).RaiseEvent(focus);
            Assert.AreSame(second, list.SelectedItem);
            Assert.IsFalse(focus.Handled);
            ControllerRowSelection.SetSelectOnInteraction(second, false);
            first.IsSelected = true;
            ((Button)second.Content).RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
                { RoutedEvent = Mouse.PreviewMouseDownEvent });
            Assert.AreSame(first, list.SelectedItem);
        });
    }

    [TestMethod]
    public void HomeKeepsExistingActionsAndSeparatesRuntimeTruthFromRows()
    {
        XElement home = HomeSource();
        var elements = home.Descendants().ToArray();
        var clicks = elements.Attributes("Click").Select(a => a.Value).ToArray();
        foreach (string handler in new[] { "LightColorBtn_Click", "HomeControllerDetailsBtn_Click",
            "ControllerOverview_EditProfileRequested", "HomeNewProfileBtn_Click",
            "HomeUseNativeBtn_Click", "HomeDisconnectBtn_Click", "HomeUseManagedBtn_Click",
            "HomeRecoverControllerBtn_Click" })
            CollectionAssert.Contains(clicks, handler);
        var rowTemplate = elements.Single(e => e.Name.LocalName == "ListView.ItemTemplate");
        Assert.IsFalse(rowTemplate.ToString().Contains("SelectedControllerStartup"),
            "Selected readiness must not be repeated as if it described every controller.");
        Assert.IsFalse(rowTemplate.ToString().Contains("Bridge"));
        Assert.IsFalse(home.ToString().Contains("Mode=OneTime"),
            "Recycled or selected targets must follow the current model, not an old slot.");
    }

    [TestMethod]
    public void ExposureActionsExplainTheModeInsteadOfOnlyNamingIt()
    {
        XElement home = HomeSource();
        XElement Action(string handler) => home.Descendants().Single(e =>
            (string)e.Attribute("Click") == handler);

        Assert.AreEqual("Use controller directly",
            (string)Action("HomeUseNativeBtn_Click").Attribute("Content"));
        StringAssert.Contains((string)Action("HomeUseNativeBtn_Click")
            .Attribute("ToolTip"), "Stop virtual game output");
        Assert.AreEqual("Use game output",
            (string)Action("HomeUseManagedBtn_Click").Attribute("Content"));
    }

    [TestMethod]
    public void ReportIntervalHasBoundedPrecisionAndDoesNotClaimGameLatency()
    {
        string format = DS4WinWPF.Properties.Resources.ResourceManager.GetString(
            "InputDelay", CultureInfo.InvariantCulture);
        Assert.AreEqual("Report interval: 4.00 ms", string.Format(
            CultureInfo.InvariantCulture, format, 3.9996149987304));

        XElement readings = PresentationHarness.Source("ControllerReadingsControl");
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        XElement label = readings.Descendants().Single(e =>
            (string)e.Attribute(x + "Name") == "inputDelayLb");
        StringAssert.Contains((string)label.Attribute("ToolTip"),
            "not controller-to-game latency");
    }

    private static void LoadTheme(bool dark)
    {
        Application.Current.Resources.MergedDictionaries.Clear();
        foreach (var file in new[] { dark ? "DarkTheme" : "DefaultTheme", "Foundation", "BridgeShellStyles" })
            WpfTestHost.LoadDictionary($"/PureDS4;component/DS4Forms/Themes/{file}.xaml");
    }

    private static XElement HomeSource()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "PureDS4.sln")))
            directory = directory.Parent;
        Assert.IsNotNull(directory, "Repository source is required for the actual Home markup.");
        var document = XDocument.Load(Path.Combine(directory.FullName, "PureDS4", "DS4Forms", "MainWindow.xaml"));
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        return new XElement(document.Descendants().Single(e => (string)e.Attribute(x + "Name") == "homeWorkspace"));
    }

    private static Grid LoadHome()
    {
        XElement source = HomeSource();
        source.SetAttributeValue(XNamespace.Xmlns + "x", "http://schemas.microsoft.com/winfx/2006/xaml");
        source.SetAttributeValue(XNamespace.Xmlns + "local", "clr-namespace:DS4WinWPF.DS4Forms");
        // Only event hookups are stripped. All templates, bindings, resources,
        // selection behavior, and layout remain the product's actual XAML.
        var events = new[] { "Click", "SelectionChanged", "KeyDown", "MouseRightButtonUp", "ToolTipOpening" };
        foreach (var attribute in source.DescendantsAndSelf().Attributes()
            .Where(a => events.Contains(a.Name.LocalName)).ToArray())
            attribute.Remove();
        Application.Current.Resources["BooleanToVisibilityConverter"] = new BooleanToVisibilityConverter();
        var home = (Grid)XamlReader.Parse(source.ToString().Replace(
            "clr-namespace:DS4WinWPF.DS4Forms", "clr-namespace:DS4WinWPF.DS4Forms;assembly=PureDS4"));
        return home;
    }

    private static void Measure(FrameworkElement element, Size size)
    {
        element.Measure(size);
        element.Arrange(new Rect(size));
        element.UpdateLayout();
    }

    private static void SavePreviewIfRequested(FrameworkElement home, bool dark, string fileName = null)
    {
        // Opt-in synthetic WPF render, not a capture of an installed/running app.
        string directory = Environment.GetEnvironmentVariable("PUREDS4_VISUAL_PREVIEW_DIRECTORY");
        if (string.IsNullOrEmpty(directory)) return;
        Assert.IsTrue(Directory.Exists(directory), "Preview destination must be explicitly prepared.");
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(home.ActualWidth),
            (int)Math.Ceiling(home.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        var drawing = new DrawingVisual();
        using (var context = drawing.RenderOpen())
        {
            var bounds = new Rect(home.RenderSize);
            context.DrawRectangle((Brush)Application.Current.FindResource("SurfaceBaseBrush"), null, bounds);
            context.DrawRectangle(new VisualBrush(home), null, bounds);
        }
        bitmap.Render(drawing);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(directory,
            fileName ?? (dark ? "pureds4-home-wpf-dark.png" : "pureds4-home-wpf-light.png")));
        encoder.Save(stream);
    }

    private static void AssertInside(FrameworkElement root, FrameworkElement child, string label)
    {
        Rect bounds = child.TransformToAncestor(root).TransformBounds(new Rect(child.RenderSize));
        Assert.IsTrue(bounds.Left >= -1 && bounds.Top >= -1 &&
            bounds.Right <= root.ActualWidth + 1 && bounds.Bottom <= root.ActualHeight + 1,
            $"{label}: {bounds} outside {root.ActualWidth}×{root.ActualHeight}");
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T typed) yield return typed;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }

    public sealed class HomeFixture : INotifyPropertyChanged
    {
        private ControllerFixture selectedController;
        public HomeFixture(int count)
        {
            for (int i = 0; i < count; i++) ControllerCol.Add(new ControllerFixture(i));
            selectedController = ControllerCol.FirstOrDefault();
        }
        public ObservableCollection<ControllerFixture> ControllerCol { get; } = new();
        public object[] NativePhysicalSessions => Array.Empty<object>();
        public int CurrentIndex { get; set; }
        public ControllerFixture SelectedController
        {
            get => selectedController;
            set
            {
                selectedController = value;
                PropertyChanged?.Invoke(this, new(nameof(SelectedController)));
                PropertyChanged?.Invoke(this, new(nameof(HasSelectedController)));
            }
        }
        public bool HasSelectedController => SelectedController != null;
        public bool Degraded { get; set; }
        public string SelectedRuntimeOutputControllerName => Degraded ? "Unavailable" : "Xbox 360";
        public string SelectedControllerStartupTitle => Degraded ? "Needs attention" : "Ready";
        public string SelectedControllerStartupDetail => Degraded
            ? "Game output is unavailable. Open game output settings to review setup and recovery options."
            : "Physical input and virtual game output are ready.";
        public object SelectedControllerStartupStage => Degraded
            ? DS4Windows.ControllerStartupStage.Attention : DS4Windows.ControllerStartupStage.Ready;
        public event PropertyChangedEventHandler PropertyChanged;
    }

    public sealed class ControllerFixture
    {
        public ControllerFixture(int index) { DevIndex = index; }
        public int DevIndex { get; }
        public int DisplayDevIndex => DevIndex + 1;
        public string ControllerDisplayName => "DualShock 4";
        public string IdText => $"DualShock 4 · {DisplayDevIndex} (fixture)";
        public string ConnectionText => IsWireless ? "Bluetooth" : "USB";
        public bool IsWireless => DevIndex % 2 == 0;
        public bool PrimaryDevice => true;
        public bool HasControllerArtwork => false;
        public string ControllerImageSource => null;
        public string StatusSource => null;
        public string TooltipIDText => "Synthetic latency";
        public string IsExclusiveText => "Protected";
        public string BatteryState => "Charging unavailable";
        public string LightColor => "#FF2080FF";
        public string LightbarModeText => "From profile";
        public string SelectedProfile => ProfileListCol[0].Name;
        public int SelectedIndex { get; set; }
        public ProfileFixture[] ProfileListCol { get; } = { new() };
    }

    public sealed class ProfileFixture
    {
        public string Name => "A very long game profile name that must not widen the controller row";
    }
}
