using DS4WinWPF.DS4Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;

namespace DS4WindowsTests;

// Load the real markup without production constructors, event handlers, timers,
// profile models or live readings. These are layout/contract checks, not hardware
// or save/cancel acceptance. Native controls, bindings and templates remain real.
[TestClass]
[DoNotParallelize]
public class ProfileWorkspacePresentationTests
{
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";
    private static readonly XNamespace Wpf = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    [TestMethod]
    public void EditorSectionsAndActionsFitBothThemesAndViewportSizes()
    {
        WpfTestHost.Run(() =>
        {
            foreach (bool dark in new[] { false, true })
            foreach (Size size in new[] { new Size(704, 360), new Size(1008, 540) })
            {
                Theme(dark);
                UserControl view = Load("ProfileEditor");
                var owner = new Window { Content = view };
                view.DataContext = new EditorFixture();
                var nav = (ListBox)view.FindName("sectionNavigationList");
                var sidebar = (TabControl)view.FindName("sidebarTabControl");
                var settings = (TabControl)view.FindName("profileSettingsTabCon");
                foreach (string name in new[] { "picBoxHover", "picBoxHover2" })
                    ((UIElement)view.FindName(name)).Visibility = Visibility.Hidden;
                foreach (string name in new[] { "muteConBtn", "captureConBtn", "fnlConBtn", "fnrConBtn", "blpConBtn", "brpConBtn" })
                    ((UIElement)view.FindName(name)).Visibility = Visibility.Collapsed;
                // The production constructor chooses the DS4 artwork and clears
                // hit-target text. Do not exercise its hardware/model setup here.
                ((Image)view.FindName("controllerDiagram")).Source = new BitmapImage(new Uri("pack://application:,,,/PureDS4;component/Resources/DualShock 4 Controller.png"));
                foreach (Button target in ((Canvas)view.FindName("conCanvas")).Children.OfType<Button>()) target.Content = null;
                Assert.AreEqual(0, nav.SelectedIndex);
                Assert.AreEqual(8, nav.Items.Count);
                for (int section = 0; section < 8; section++)
                {
                    // Mirror only the existing section-selection routing; do not
                    // construct ProfileEditor, which would connect to live state.
                    nav.SelectedIndex = section;
                    sidebar.Visibility = section < 3 ? Visibility.Visible : Visibility.Collapsed;
                    settings.Visibility = section >= 3 ? Visibility.Visible : Visibility.Collapsed;
                    if (section < 3) sidebar.SelectedIndex = section;
                    else settings.SelectedIndex = section - 3;
                    Layout(view, size);
                    Assert.AreEqual(190, ((Border)nav.Parent).ActualWidth, 0.1);
                    foreach (string name in new[] { "profileNameTxt", "presetBtn", "applyBtn", "cancelBtn", "saveBtn" })
                        Inside(view, (FrameworkElement)view.FindName(name));
                    Assert.IsFalse(((Button)view.FindName("applyBtn")).IsEnabled);
                    if (section == 0)
                    {
                        var mappings = (ListBox)view.FindName("mappingListBox");
                        var edit = (Button)view.FindName("editMappingButton");
                        mappings.SelectedIndex = -1;
                        Layout(view, size);
                        Assert.IsFalse(edit.IsEnabled, "No selection must not offer a mapping edit.");
                        mappings.SelectedIndex = 29;
                        Layout(view, size);
                        Assert.IsFalse(edit.IsEnabled, "An unavailable imported control must not become editable.");
                        mappings.SelectedIndex = 0;
                        Layout(view, size);
                        Assert.IsTrue(edit.IsEnabled);
                        Inside(view, edit);
                    }
                    if (section >= 3)
                        foreach (Control control in Descendants<Control>(view).Where(c => c.ActualWidth > 0 && Participates(c)))
                        {
                            Rect b = control.TransformToAncestor(view).TransformBounds(new Rect(control.RenderSize));
                            Assert.IsTrue(b.Left >= -1 && b.Right <= view.ActualWidth + 1,
                                $"Section {section}: {control.Name}/{control.GetType().Name}: {b} outside {view.ActualWidth}");
                        }
                    foreach (ScrollViewer scroll in Descendants<ScrollViewer>(view))
                        if (scroll.IsVisible || scroll.ActualHeight > 0)
                            Assert.IsTrue(double.IsFinite(scroll.ViewportHeight));
                    if (section is 0 or 3 or 4 or 7)
                        Preview(view, $"editor-{section}-{(int)size.Width}", dark);
                }
                owner.Content = null;
            }
        });
    }

    [TestMethod]
    public void AuxiliaryEditorsKeepActionsReachableAndNumericInputsNative()
    {
        WpfTestHost.Run(() =>
        {
            foreach (bool dark in new[] { false, true })
            {
                Theme(dark);
                foreach (string name in new[] { "BindingWindow", "SpecialActionEditor", "RecordBox", "PresetOptionWindow", "RenameProfileWindow", "DupBox", "LightbarMacroCreator", "StickCalibrationWindow", "AxialStickUserControl" })
                {
                    UserControl view = Load(name);
                    var host = new Window { Content = view };
                    view.DataContext = new EditorFixture();
                    Layout(view, new Size(name == "BindingWindow" ? 1008 : 640, 400));
                    foreach (var button in Descendants<Button>(view).Where(b => b.Name is "saveBtn" or "cancelBtn" or "confirmBtn"))
                        Inside(view, button);
                    Preview(view, name, dark);
                    host.Content = null;
                }
                var numeric = new Xceed.Wpf.Toolkit.DoubleUpDown
                {
                    Style = (Style)Application.Current.FindResource("FoundationNumericInputStyle"),
                    Minimum = 0, Maximum = 1, Increment = 0.1, FormatString = "F2", Value = 0.5
                };
                var owner = new Window { Content = numeric };
                Layout(numeric, new Size(100, 28));
                numeric.ApplyTemplate();
                Assert.IsNotNull(numeric.Template, $"Numeric template ({(dark ? "dark" : "light")})");
                Assert.IsNotNull(numeric.Template.FindName("PART_TextBox", numeric));
                Assert.IsNotNull(numeric.Template.FindName("PART_Spinner", numeric));
                Assert.AreEqual(0.5, numeric.Value);
                Assert.AreEqual(0.1, numeric.Increment);
                Size enabledSize = numeric.RenderSize;
                numeric.IsEnabled = false;
                Layout(numeric, new Size(100, 28));
                Assert.AreEqual(enabledSize, numeric.RenderSize);
                owner.Content = null;
            }
        });
    }

    [TestMethod]
    public void ProfileBrowserHandlesEmptyLongAndScrollableLists()
    {
        WpfTestHost.Run(() =>
        {
            foreach (bool dark in new[] { false, true })
            foreach (int count in new[] { 0, 2, 40 })
            {
                Theme(dark);
                UserControl view = Load("MainWindow", "profilesBrowserPanel");
                var owner = new Window { Content = view };
                var list = (ListBox)view.FindName("profilesListBox");
                list.ItemsSource = Enumerable.Range(1, count).Select(i => new
                {
                    Name = $"Profile {i} with a long descriptive name", GameOutputDisplay = "DualShock 4", ModifiedDisplay = "08 Sep 2026 12:00"
                }).ToArray();
                Layout(view, new Size(704, 360));
                foreach (string name in new[] { "newProfListBtn", "editProfBtn", "dupProfBtn", "renameProfBtn", "deleteProfBtn", "importProfBtn", "exportProfBtn", "profilesSearchTextBox" })
                    Inside(view, (FrameworkElement)view.FindName(name));
                Assert.AreEqual(count, list.Items.Count);
                var columnGrids = Descendants<Grid>(list).Where(g => g.ColumnDefinitions.Count == 4).ToArray();
                foreach (Grid row in columnGrids.Skip(1))
                    for (int column = 0; column < 4; column++)
                        Assert.AreEqual(columnGrids[0].ColumnDefinitions[column].ActualWidth,
                            row.ColumnDefinitions[column].ActualWidth, 1.0, $"Profile column {column}");
                Assert.IsFalse(((Button)view.FindName("editProfBtn")).IsEnabled);
                if (count > 0)
                {
                    list.SelectedIndex = count - 1;
                    list.ScrollIntoView(list.SelectedItem);
                    Layout(view, new Size(704, 360));
                    Assert.AreEqual(count - 1, list.SelectedIndex);
                }
                Preview(view, $"profiles-{count}", dark);
                owner.Content = null;
            }
        });
    }

    [TestMethod]
    public void SectionContentAssociationsAndCommitBindingsAreUnchanged()
    {
        XElement editor = Source("ProfileEditor");
        XElement Named(string name) => editor.Descendants().Single(e => (string)e.Attribute(X + "Name") == name);
        CollectionAssert.AreEqual(new[] { "Button Mapping", "Special Actions", "Controller Readings", "Axis Config", "Lightbar", "Touchpad", "Gyro", "Advanced" },
            Named("sectionNavigationList").Elements().Select(e => (string)e.Attribute("Content")).ToArray());
        Assert.AreEqual("0", (string)Named("sectionNavigationList").Attribute("SelectedIndex"));
        CollectionAssert.AreEqual(new[] { "{lex:Loc Controls}", "Special Actions", "Controller Readings" },
            Named("sidebarTabControl").Elements(Wpf + "TabItem").Select(e => (string)e.Attribute("Header")).ToArray());
        CollectionAssert.AreEqual(new[] { "{lex:Loc AxisConfig}", "{lex:Loc Lightbar}", "{lex:Loc Touchpad}", "{lex:Loc Gyro}", "Advanced" },
            Named("profileSettingsTabCon").Elements(Wpf + "TabItem").Select(e => (string)e.Attribute("Header")).ToArray());
        foreach (string action in new[] { "Apply", "Cancel", "Save", "Preset" })
            Assert.AreEqual(action + "Btn_Click", (string)Named(char.ToLowerInvariant(action[0]) + action.Substring(1) + "Btn").Attribute("Click"));
        Assert.IsTrue(editor.Descendants().Attributes("Value").Any(a => a.Value == "{Binding LSDeadZone,UpdateSourceTrigger=LostFocus}"));
        XElement macro = Source("RecordBox");
        Assert.IsTrue(macro.Descendants().Attributes("Value").Any(a => a.Value == "{Binding DisplayValue,UpdateSourceTrigger=Explicit}"));
        Assert.AreEqual("UserControl_KeyDown", (string)macro.Attribute("KeyDown"));
        Assert.AreEqual("UserControl_KeyUp", (string)macro.Attribute("KeyUp"));
        XElement actions = Source("SpecialActionEditor");
        Assert.IsTrue(actions.Descendants().Attributes("Text").Any(a => a.Value.Contains("ValidatesOnNotifyDataErrors=True")));
        Assert.AreEqual("{Binding ActionTypeIndex}", (string)actions.Descendants().Single(e => (string)e.Attribute(X + "Name") == "actionTypeTabControl").Attribute("SelectedIndex"));
    }

    [TestMethod]
    public void ControllerReadingsRemainReachableAtCompactWindowHeights()
    {
        XElement readings = Source("ControllerReadingsControl");
        XElement scroll = readings.Elements(Wpf + "ScrollViewer").Single();

        Assert.AreEqual("readingsScroll", (string)scroll.Attribute(X + "Name"));
        Assert.AreEqual("Auto", (string)scroll.Attribute("VerticalScrollBarVisibility"));
        Assert.AreEqual("Disabled", (string)scroll.Attribute("HorizontalScrollBarVisibility"));
        Assert.AreEqual("False", (string)scroll.Attribute("CanContentScroll"));
        Assert.AreEqual("DockPanel", scroll.Elements().Single().Name.LocalName,
            "All readings must remain inside the vertical viewport instead of being clipped below it.");
    }

    [TestMethod]
    public void ControllerReadingsKeepChartsAndValuesSeparatedAcrossWidths()
    {
        WpfTestHost.Run(() =>
        {
            foreach (bool dark in new[] { false, true })
            foreach (double width in new[] { 480d, 640d, 1600d })
            {
                Theme(dark);
                UserControl view = PresentationHarness.LoadControl("ControllerReadingsControl");
                var owner = new Window { Content = view };
                Layout(view, new Size(width, width == 1600 ? 600 : 240));

                var left = (Canvas)view.FindName("lsCanvas");
                var right = (Canvas)view.FindName("rsCanvas");
                var motion = (Canvas)view.FindName("sixaxisCanvas");
                Rect Bounds(FrameworkElement element) =>
                    element.TransformToAncestor(view).TransformBounds(new Rect(element.RenderSize));

                Assert.IsTrue(Bounds(left).Right + 8 <= Bounds(right).Left,
                    $"Left and right charts overlap at {width} DIP: {Bounds(left)}, {Bounds(right)}.");
                Assert.IsTrue(Bounds(right).Right + 8 <= Bounds(motion).Left,
                    $"Right and motion charts overlap at {width} DIP: {Bounds(right)}, {Bounds(motion)}.");
                Assert.AreEqual(Bounds(left).Top, Bounds(right).Top, 1);
                Assert.AreEqual(Bounds(right).Top, Bounds(motion).Top, 1);

                foreach (var pair in new[]
                {
                    ("lxOutValLb", "rxInValLb"), ("lyOutValLb", "ryInValLb"),
                    ("rxOutValLb", "sixAxisXInValLb"), ("ryOutValLb", "sixAxisZInValLb")
                })
                    Assert.IsTrue(Bounds((FrameworkElement)view.FindName(pair.Item1)).Right + 2 <=
                        Bounds((FrameworkElement)view.FindName(pair.Item2)).Left,
                        $"{pair.Item1} and {pair.Item2} overlap at {width} DIP.");

                var chartGrid = PresentationHarness.Descendants<Grid>(view)
                    .Single(grid => grid.MaxWidth == 660);
                var sensorGrid = PresentationHarness.Descendants<Grid>(view)
                    .Single(grid => grid.MaxWidth == 700);
                Assert.IsTrue(chartGrid.ActualWidth <= 660);
                Assert.IsTrue(sensorGrid.ActualWidth <= 700);
                if (width == 1600)
                {
                    double viewportWidth = ((ScrollViewer)view.FindName("readingsScroll")).ViewportWidth;
                    Assert.AreEqual((viewportWidth - chartGrid.ActualWidth) / 2, Bounds(chartGrid).Left, 2,
                        "The bounded charts should remain centered on wide windows.");
                    Assert.AreEqual((viewportWidth - sensorGrid.ActualWidth) / 2, Bounds(sensorGrid).Left, 2,
                        "The lower readings should remain centered on wide windows.");
                }
                if (width == 480)
                    Assert.IsTrue(((ScrollViewer)view.FindName("readingsScroll")).ScrollableHeight > 0,
                        "Small windows must scroll to the lower readings.");
                if (width == 480 || width == 1600)
                    PresentationHarness.Preview(view, $"readings-{width}", dark);
                owner.Content = null;
            }
        });
    }

    // Two shell-level regressions this gate fixed, both invisible to a
    // screenshot of a single section:
    //  * every page sized itself to its own content and floated in the middle
    //    of the window, so the editor jumped sideways between sections;
    //  * the selected tab's content produced no automation peers at all, so a
    //    screen reader found nothing but the four navigation tabs.
    [TestMethod]
    public void MainNavigationHoverTracksOnlyTheVisibleHeader()
    {
        WpfTestHost.Run(() =>
        {
            foreach (bool dark in new[] { false, true })
            {
                Theme(dark);
                var tab = new TabItem
                {
                    Header = "Tools",
                    Style = (Style)Application.Current.FindResource("BridgeNavigationTabItemStyle"),
                };
                tab.ApplyTemplate();
                var hover = tab.Template.Triggers.OfType<Trigger>()
                    .Single(trigger => trigger.Property == UIElement.IsMouseOverProperty);
                Assert.AreEqual("TabChrome", hover.SourceName,
                    "The selected tab owns page content; hovering that content must not light up its header.");
                Assert.IsInstanceOfType(tab.Template.FindName("TabChrome", tab), typeof(Border));
            }
        });
    }

    [TestMethod]
    public void ShellTabsFillTheirHostAndPublishContentToAutomation()
    {
        WpfTestHost.Run(() =>
        {
            Theme(false);
            var page = new Grid { Name = "Page" };
            var tabs = new TabControl
            {
                Style = (Style)Application.Current.FindResource("BridgeMainTabControlStyle"),
            };
            tabs.Items.Add(new TabItem { Header = "One", Content = page });
            tabs.Items.Add(new TabItem { Header = "Two", Content = new Grid() });
            var window = new Window { Content = tabs };
            Layout(tabs, new Size(800, 600));
            // A content-sized page collapses to nothing here; a filling one is
            // the host width less the template's own padding.
            Assert.IsTrue(page.ActualWidth >= 780,
                $"A page must fill the shell, not size itself to its own content (was {page.ActualWidth}).");
            Assert.IsTrue(Peers(tabs).Any(),
                "The selected tab's content must reach the automation tree.");
            // TabControl copies this from the selected TabItem, and the
            // selected-content presenter follows it.
            Assert.AreEqual(HorizontalAlignment.Stretch, tabs.HorizontalContentAlignment);

            // Headerless content tabs are driven by the section list. They still
            // need an items host, or the selected content has no TabItem peer.
            var sections = new TabControl
            {
                Style = (Style)Application.Current.FindResource("FoundationContentTabsStyle"),
            };
            var section = new Grid();
            sections.Items.Add(new TabItem { Header = "Section", Content = section });
            var host = new Window { Content = sections };
            Layout(sections, new Size(800, 600));
            Assert.AreEqual(HorizontalAlignment.Stretch, sections.HorizontalContentAlignment);
            Assert.IsTrue(section.ActualWidth >= 780,
                $"A section must fill the editor (was {section.ActualWidth}).");
            Assert.IsTrue(Peers(sections).Any(),
                "A headerless content TabControl must still publish its content.");
            // WPF attaches the selected content's peers through the presenter
            // it finds by this name. An unnamed presenter renders correctly and
            // is silently absent from the automation tree.
            Assert.IsInstanceOfType(
                sections.Template.FindName("PART_SelectedContentHost", sections),
                typeof(ContentPresenter));
            window.Content = null;
            host.Content = null;
        });
    }

    private static IEnumerable<System.Windows.Automation.Peers.AutomationPeer> Peers(UIElement element)
    {
        var peer = System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(element);
        peer?.ResetChildrenCache();
        var children = peer?.GetChildren() ?? new List<System.Windows.Automation.Peers.AutomationPeer>();
        foreach (var child in children)
        {
            yield return child;
            child.ResetChildrenCache();
            foreach (var nested in child.GetChildren() ?? new List<System.Windows.Automation.Peers.AutomationPeer>())
                yield return nested;
        }
    }

    // The profile name is the heading of the editor. It used to be a disabled
    // TextBox, which paints its text at a disabled weight whatever its
    // Foreground says, so the name read as an empty field with a watermark.
    [TestMethod]
    public void ProfileNameReadsAsAHeadingWhenItCannotBeEditedHere()
    {
        XElement editor = Source("ProfileEditor");
        XElement box = editor.Descendants().Single(e => (string)e.Attribute(X + "Name") == "profileNameTxt");
        Assert.AreEqual("ProfileNameTxt_TextChanged", (string)box.Attribute("TextChanged"));
        Assert.IsTrue(((string)box.Attribute("Visibility")).Contains("IsEnabled"),
            "The input must give way when the name cannot be edited here.");
        XElement heading = box.Parent.Elements(Wpf + "TextBlock").Single();
        Assert.AreEqual("{Binding Text, ElementName=profileNameTxt}", (string)heading.Attribute("Text"));
        Assert.AreEqual("{StaticResource FoundationGroupTitleStyle}", (string)heading.Attribute("Style"));
        Assert.IsTrue(((string)heading.Attribute("Visibility")).Contains("InvertBoolToVisibilityConverter"));
    }

    private static XElement Source(string name)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "PureDS4.sln"))) directory = directory.Parent;
        Assert.IsNotNull(directory);
        return XElement.Load(Path.Combine(directory.FullName, "PureDS4", "DS4Forms", name + ".xaml"));
    }

    private static UserControl Load(string name, string subtree = null)
    {
        System.Reflection.Assembly.Load("WPFLocalizeExtension");
        System.Reflection.Assembly.Load(typeof(Xceed.Wpf.Toolkit.DoubleUpDown).Assembly.FullName);
        XElement source = Source(name);
        if (subtree != null)
        {
            var selected = new XElement(source.Descendants().Single(e => (string)e.Attribute(X + "Name") == subtree));
            var wrapper = new XElement(Wpf + "UserControl", source.Attributes().Where(a => a.IsNamespaceDeclaration), selected);
            source = wrapper;
        }
        string rootKind = source.Name.LocalName;
        source.Name = Wpf + "UserControl";
        source.Attribute(X + "Class")?.Remove();
        foreach (string property in new[] { "Title", "Height", "Width", "MinHeight", "MinWidth", "Style", "ResizeMode", "WindowStartupLocation", "ShowInTaskbar" }) source.Attribute(property)?.Remove();
        foreach (var resource in source.Elements(Wpf + rootKind + ".Resources").ToArray()) resource.Name = Wpf + "UserControl.Resources";
        foreach (XAttribute a in source.DescendantsAndSelf().Attributes().ToArray())
        {
            // Product event methods are not runnable in a detached XAML fixture.
            if (!a.IsNamespaceDeclaration && Regex.IsMatch(a.Value, @"^[A-Za-z]+[A-Za-z0-9]*_[A-Za-z][A-Za-z0-9_]*$") &&
                a.Name.LocalName is not "Tag" and not "Name" and not "Key") a.Remove();
        }
        // This control owns a polling timer. Live readings are a separate gate.
        foreach (var live in source.Descendants().Where(e => e.Name.LocalName == "ControllerReadingsControl").ToArray())
            live.ReplaceWith(new XElement(Wpf + "Border", live.Attributes().Where(a => a.Name == X + "Name")));
        var text = source.ToString();
        text = Regex.Replace(text, @"clr-namespace:DS4WinWPF[^"";]*", m => m.Value + ";assembly=PureDS4");
        return (UserControl)XamlReader.Parse(text);
    }

    private static void Theme(bool dark)
    {
        Application.Current.Resources.MergedDictionaries.Clear();
        foreach (string name in new[] { dark ? "DarkTheme" : "DefaultTheme", "Foundation", "BridgeShellStyles" })
            WpfTestHost.LoadDictionary($"/PureDS4;component/DS4Forms/Themes/{name}.xaml");
        Application.Current.Resources["BooleanToVisibilityConverter"] = new BooleanToVisibilityConverter();
        XElement mapStyle = new XElement(Source("../App").Descendants(Wpf + "Style").Single(e => (string)e.Attribute(X + "Key") == "NoBGHoverBtn"));
        mapStyle.SetAttributeValue(XNamespace.Xmlns + "x", X.NamespaceName);
        Application.Current.Resources["NoBGHoverBtn"] = XamlReader.Parse(mapStyle.ToString());
    }

    private static void Layout(FrameworkElement view, Size size) { view.Measure(size); view.Arrange(new Rect(size)); view.UpdateLayout(); }
    private static void Inside(FrameworkElement root, FrameworkElement child)
    {
        Rect b = child.TransformToAncestor(root).TransformBounds(new Rect(child.RenderSize));
        Assert.IsTrue(b.Left >= -1 && b.Top >= -1 && b.Right <= root.ActualWidth + 1 && b.Bottom <= root.ActualHeight + 1,
            $"{child.Name}: {b} outside {root.RenderSize}");
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
        var bitmap = new RenderTargetBitmap((int)view.ActualWidth, (int)view.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        var drawing = new DrawingVisual();
        using (var dc = drawing.RenderOpen()) dc.DrawRectangle((Brush)Application.Current.FindResource("SurfaceBaseBrush"), null, new Rect(view.RenderSize));
        bitmap.Render(drawing); bitmap.Render(view);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(directory, $"pureds4-{name}-{(dark ? "dark" : "light")}.png"));
        encoder.Save(stream);
    }

    public sealed class EditorFixture
    {
        public string ProfileName { get; set; } = "Disposable layout fixture";
        public int SelectedIndex { get; set; }
        public int ActionTypeIndex { get; set; } = 2;
        public string ActionName { get; set; } = "Example action";
        public double LSDeadZone { get; set; } = 0.15;
        public int LSOutputIndex { get; set; } = 1;
        public int RSOutputIndex { get; set; } = 1;
        public object[] Mappings { get; } = Enumerable.Range(1, 30).Select(i => (object)new
        {
            ControlName = "Control " + i, MappingName = "Action with a longer descriptive name", IsAvailableOnPhysicalController = i != 30,
            IsControllerMapListOnly = false, PhysicalControllerAvailabilityHint = "Available"
        }).ToArray();
        public object[] MacroSteps { get; } = Enumerable.Range(1, 10).Select(i => (object)new { Step = new { Name = "Key action " + i } }).ToArray();
    }
}
