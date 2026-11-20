using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Xml.Linq;
using Xceed.Wpf.Toolkit;

namespace DS4WindowsTests;

/// <summary>
/// The profile editor's Axis Config, Touchpad, Gyro and Advanced pages are made of
/// property rows: label left, one control column with a shared right edge, units
/// in a fixed column after it. These checks run the real markup against a
/// disposable fixture (no profile, controller or window) and keep it that way.
/// </summary>
[TestClass]
[DoNotParallelize]
public class ProfilePropertyPageLayoutTests
{
    private static readonly (int Section, string Name, bool On, (string, object)[] Set)[] Variants =
    {
        (3, "axis-controls", false, new[] { ("LSOutputIndex", (object)1), ("RSOutputIndex", 1) }),
        (3, "axis-controls-on", true, new[] { ("LSOutputIndex", (object)1), ("RSOutputIndex", 1) }),
        (3, "axis-axial", false, new[] { ("LSOutputIndex", (object)1), ("RSOutputIndex", 1), ("LSDeadTypeIndex", 1), ("RSDeadTypeIndex", 1) }),
        (3, "axis-flick", false, new[] { ("LSOutputIndex", (object)2), ("RSOutputIndex", 2) }),
        (5, "touchpad-mouse", false, new[] { ("TouchpadOutputIndex", (object)0) }),
        (5, "touchpad-mouse-on", true, new[] { ("TouchpadOutputIndex", (object)0) }),
        (5, "touchpad-controls", false, new[] { ("TouchpadOutputIndex", (object)1) }),
        (5, "touchpad-mousejoystick", false, new[] { ("TouchpadOutputIndex", (object)2) }),
        (5, "touchpad-mousejoystick-on", true, new[] { ("TouchpadOutputIndex", (object)2) }),
        (5, "touchpad-absolute", false, new[] { ("TouchpadOutputIndex", (object)3) }),
        (6, "gyro-controls", false, new[] { ("GyroOutModeIndex", (object)0) }),
        (6, "gyro-controls-on", true, new[] { ("GyroOutModeIndex", (object)0) }),
        (6, "gyro-mouse", false, new[] { ("GyroOutModeIndex", (object)1), ("GyroMouseWeightAvgPanelVisibility", Visibility.Collapsed) }),
        (6, "gyro-mouse-on", true, new[] { ("GyroOutModeIndex", (object)1), ("GyroMouseWeightAvgPanelVisibility", Visibility.Collapsed) }),
        (6, "gyro-mousejoystick", false, new[] { ("GyroOutModeIndex", (object)2), ("GyroMouseStickWeightAvgPanelVisibility", Visibility.Collapsed) }),
        (6, "gyro-mousejoystick-on", true, new[] { ("GyroOutModeIndex", (object)2), ("GyroMouseStickWeightAvgPanelVisibility", Visibility.Collapsed) }),
        (6, "gyro-swipe", false, new[] { ("GyroOutModeIndex", (object)3) }),
        (6, "gyro-passthrough", false, new[] { ("GyroOutModeIndex", (object)4) }),
        (7, "advanced", false, Array.Empty<(string, object)>()),
        (7, "advanced-on", true, Array.Empty<(string, object)>()),
    };

    // Switches that stay visible but disabled on purpose: the program-launch switch can
    // only be turned off (Browse is what turns it on), and the controller number is
    // a read-only indicator the editor fills in.
    private static readonly string[] DisabledOnPurpose = { "launchProgCk", "useControllerUD" };

    [TestMethod]
    public void EveryOptionIsAPropertyRowWithOneSharedRightEdge()
    {
        // Both themes and the compact, requested and wide sizes, without multiplying every pair.
        foreach (var (dark, size) in new[] { (false, new Size(672, 340)), (false, new Size(1024, 660)), (true, new Size(720, 480)) })
        foreach (var variant in Variants)
            WpfTestHost.Run(() =>
            {
                ProfileWorkspacePresentationTests.Theme(dark);
                UserControl view = ProfileWorkspacePresentationTests.Load("ProfileEditor");
                var owner = new Window { Content = view };
                string where = $"{variant.Name} {(dark ? "dark" : "light")} {size}";
                try
                {
                    view.DataContext = ProfilePropertyPageFixture.Create(variant.On, variant.Set.ToDictionary(s => s.Item1, s => s.Item2));
                    ScrollViewer scroll = ProfilePropertyPageFixture.ShowSection(view, variant.Section);
                    if (variant.Section == 5)
                        ProfilePropertyPageFixture.PlaceTouchButtonOptions(view, (int)variant.Set.Single(s => s.Item1 == "TouchpadOutputIndex").Item2);
                    // The page width follows the viewport width, which changes when the scroll bar appears.
                    for (int pass = 0; pass < 3; pass++) PresentationHarness.Layout(view, size);
                    foreach (Expander expander in PresentationHarness.Descendants<Expander>(scroll)) expander.IsExpanded = true;
                    for (int pass = 0; pass < 3; pass++) PresentationHarness.Layout(view, size);
                    var content = (FrameworkElement)scroll.Content;

                    var rows = PresentationHarness.Descendants<HeaderedContentControl>(content)
                        .Where(r => r.ActualWidth > 0 && PresentationHarness.Participates(r) && IsRow(r)).ToArray();
                    Assert.IsTrue(rows.Length >= 2, $"{where}: expected property rows.");

                    // One shared right edge for the controls of the page.
                    double[] edges = rows.Select(r => RightEdge(scroll, (FrameworkElement)r.Content)).ToArray();
                    Assert.IsTrue(edges.Max() - edges.Min() < 0.75,
                        $"{where}: control right edges differ ({edges.Min():0.0}..{edges.Max():0.0}): " + string.Join("; ", rows.Where((r, i) => edges.Max() - edges[i] > 0.75).Select(r => $"'{r.Header}' {RightEdge(scroll, (FrameworkElement)r.Content):0.0} ({r.Content.GetType().Name} {((FrameworkElement)r.Content).ActualWidth:0.0})")));

                    // No option sits outside a row.
                    foreach (Control control in PresentationHarness.Descendants<Control>(content)
                        .Where(c => c.TemplatedParent == null && c.ActualWidth > 0 && PresentationHarness.Participates(c) && IsInput(c)))
                        Assert.IsTrue(InsideRow(control), $"{where}: {control.GetType().Name} '{control.Name}' is outside a property row.");

                    // Standard widths and no colons.
                    foreach (Control box in PresentationHarness.Descendants<Control>(content)
                        .Where(c => c.TemplatedParent == null && c.ActualWidth > 0 && PresentationHarness.Participates(c) &&
                                    c is DoubleUpDown or IntegerUpDown or SByteUpDown))
                        Assert.AreEqual(96, box.ActualWidth, 0.6, $"{where}: numeric box '{box.Name}'.");
                    foreach (var row in rows)
                    {
                        if (row.Content is ComboBox combo)
                            Assert.AreEqual(196, combo.ActualWidth, 0.6, $"{where}: combo '{combo.Name}' in '{row.Header}'.");
                        string header = row.Header as string ?? string.Empty;
                        Assert.IsFalse(header.TrimEnd().EndsWith(":"), $"{where}: label '{header}' ends with a colon.");
                    }
                    foreach (Label label in PresentationHarness.Descendants<Label>(content).Where(l => l.ActualWidth > 0))
                        if (label.Content is string text)
                            Assert.IsFalse(text.TrimEnd().EndsWith(":"), $"{where}: label '{text}' ends with a colon.");

                    // Nothing clipped sideways.
                    foreach (Control control in PresentationHarness.Descendants<Control>(content)
                        .Where(c => c.ActualWidth > 0 && PresentationHarness.Participates(c)))
                    {
                        Rect b = control.TransformToAncestor(scroll).TransformBounds(new Rect(control.RenderSize));
                        Assert.IsTrue(b.Left >= -1 && b.Right <= scroll.ViewportWidth + 1,
                            $"{where}: {control.GetType().Name} '{control.Name}' {b} outside {scroll.ViewportWidth}.");
                    }

                    // Dependent options hide instead of showing greyed (base states only).
                    if (!variant.On)
                        foreach (var row in rows)
                            foreach (Control control in PresentationHarness.Descendants<Control>(row)
                                .Where(c => c.TemplatedParent == null && c.ActualWidth > 0 && PresentationHarness.Participates(c) && IsInput(c)))
                                Assert.IsTrue(control.IsEnabled || DisabledOnPurpose.Contains(control.Name),
                                    $"{where}: '{control.Name}' in '{row.Header}' is shown greyed; hide it instead.");
                }
                finally { owner.Content = null; }
            });
    }

    [TestMethod]
    public void SwitchesAndSubRowsStayNamedAndNested()
    {
        WpfTestHost.Run(() =>
        {
            ProfileWorkspacePresentationTests.Theme(false);
            UserControl view = ProfileWorkspacePresentationTests.Load("ProfileEditor");
            var owner = new Window { Content = view };
            try
            {
                view.DataContext = ProfilePropertyPageFixture.Create(true, new Dictionary<string, object> { ["LSOutputIndex"] = 1, ["RSOutputIndex"] = 1 });
                ScrollViewer scroll = ProfilePropertyPageFixture.ShowSection(view, 3);
                PresentationHarness.Layout(view, new Size(720, 480));
                var switches = PresentationHarness.Descendants<CheckBox>(scroll).Where(c => c.TemplatedParent == null && c.ActualWidth > 0).ToArray();
                Assert.IsTrue(switches.Length >= 8);
                foreach (CheckBox box in switches)
                {
                    Assert.AreSame(Application.Current.FindResource("FoundationToggleSwitchStyle"), box.Style,
                        "On/off options use the shared switch.");
                    Assert.IsFalse(string.IsNullOrWhiteSpace(System.Windows.Automation.AutomationProperties.GetName(box)),
                        "A switch has no text of its own, so it needs an automation name.");
                }
                var subRows = PresentationHarness.Descendants<HeaderedContentControl>(scroll)
                    .Where(r => r.Style == Application.Current.FindResource("FoundationPropertySubRowControlStyle")).ToArray();
                Assert.IsTrue(subRows.Length >= 6, "Dependent options are nested sub-rows.");
            }
            finally { owner.Content = null; }
        });
    }

    [TestMethod]
    public void MarkupKeepsEveryNamedElementHandlerAndLeavesNoLegacyGrid()
    {
        string markup = File.ReadAllText(Path.Combine(RepoRoot(), "PureDS4", "DS4Forms", "ProfileEditor.xaml"));
        foreach (string name in PinnedNames)
            Assert.IsTrue(markup.Contains($"x:Name=\"{name}\""), $"x:Name=\"{name}\" must stay in the profile editor.");
        string properties = markup.Substring(markup.IndexOf("x:Name=\"profileSettingsTabCon\"", StringComparison.Ordinal));
        Assert.IsFalse(properties.Contains("gridRowHeight"), "The legacy row grid is gone.");
        Assert.IsFalse(Regex.IsMatch(properties, "ContentStringFormat=\"\\{\\}\\{0\\}:\""), "Property labels carry no trailing colon.");
        foreach (string file in new[] { "ProfileEditor", "AxialStickUserControl", "TouchButtonUserControl" })
        {
            XElement source = PresentationHarness.Source(file);
            foreach (XElement row in source.Descendants(PresentationHarness.Wpf + "HeaderedContentControl"))
                Assert.IsFalse(((string)row.Attribute("Header") ?? "").TrimEnd().EndsWith(":"), $"{file}: row label ends with a colon.");
        }
        foreach (string handler in new[] { "CustomEditorBtn_Click", "StickOuterBindButton_Click", "CalibrateStick_OnClick", "FrictionUD_ValueChanged",
            "TouchDisInvertBtn_Click", "TouchDisInvertMenuItem_Click", "SwipeControlsButton_Click", "TiltControlsButton_Click",
            "GyroControlsTrigBtn_Click", "GyroMouseTrigBtn_Click", "GyroMouseStickTrigBtn_Click", "GyroSwipeTrigBtn_Click",
            "GyroSwipeControlsBtn_Click", "GyroCalibration_Click", "SteeringWheelEmulationCalibrateBtn_Click", "RumbleTestBtn_Click",
            "LaunchProgBrowseBtn_Click", "UseControllerReadoutCk_Click" })
            Assert.IsTrue(markup.Contains("\"" + handler + "\""), $"Handler {handler} must stay wired.");
    }

    private static bool IsRow(HeaderedContentControl r) =>
        r.Style == Application.Current.FindResource("FoundationPropertyRowControlStyle") ||
        r.Style == Application.Current.FindResource("FoundationPropertySubRowControlStyle");

    private static bool IsInput(Control c) =>
        c is ButtonBase or ComboBox or TextBoxBase or Slider or DoubleUpDown or IntegerUpDown or SByteUpDown;

    private static bool InsideRow(DependencyObject element)
    {
        for (DependencyObject p = System.Windows.Media.VisualTreeHelper.GetParent(element); p != null; p = System.Windows.Media.VisualTreeHelper.GetParent(p))
            if (p is HeaderedContentControl h && IsRow(h)) return true;
        return false;
    }

    private static double RightEdge(FrameworkElement root, FrameworkElement child) =>
        child.TransformToAncestor(root).TransformBounds(new Rect(child.RenderSize)).Right;

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "PureDS4.sln"))) directory = directory.Parent;
        Assert.IsNotNull(directory);
        return directory.FullName;
    }

    private static readonly string[] PinnedNames =
    {
        "advancedPropertiesScroll", "advancedToggle1", "advancedToggle10", "advancedToggle11", "advancedToggle2", "advancedToggle4", "advancedToggle5", "advancedToggle6",
        "advancedToggle7", "advancedToggle8", "advancedToggle9", "axialLSStickControl", "axialRSStickControl", "axisPropertiesScroll", "btPollRateCombo", "chargingColorBtn",
        "chargingTypeCombo", "colorByBatteryPerCk", "colorGB", "Debouncing", "emptyColorGB", "euroBeta", "euroFilterOptionsPanel", "euroMinCutoff",
        "flashAtInput", "flashColorBtn", "flashTypeCombo", "frictionUD", "gyroCalBtn", "gyroCalBtn1", "gyroCalBtn2", "gyroCalBtn3",
        "gyroCalBtn4", "gyroControlsPanel", "gyroControlsTrigBtn", "gyroDirSwipePanel", "gyroMouseJoystickPanel", "gyroMouseOneEuroPanel", "gyroMousePanel", "gyroMouseSmoothMethodCombo",
        "gyroMouseStickOneEuroPanel", "gyroMouseStickSmoothMethodCombo", "gyroMouseStickTrigBtn", "gyroMouseStickWeightedAvgPanel", "gyroMouseTrigBtn", "gyroMouseWeightedAvgPanel", "gyroOutModeCombo", "gyroPropertiesScroll",
        "gyroSwipeDownBtn", "gyroSwipeDownLb", "gyroSwipeLeftBtn", "gyroSwipeLeftLb", "gyroSwipeRightBtn", "gyroSwipeRightLb", "gyroSwipeTrigBtn", "gyroSwipeUpBtn",
        "gyroSwipeUpLb", "gyroXLb", "gyroXNBtn", "gyroXNLb", "gyroXPBtn", "gyroZNBtn", "gyroZNLb", "gyroZPBtn",
        "gyroZPLb", "heavyRumbleTestBtn", "idleDisconnectUD", "l2CustomEditorBtn", "launchProgBrowseBtn", "launchProgCk", "launchProgIconBtn", "lightbarModeCombo",
        "lightbarModeTabs", "lightbarScroll", "lightRumbleTestBtn", "lowBlueSlider", "lowColorBtn", "lowGreenSlider", "lowRedSlider", "lsCustomEditorBtn",
        "lsOuterBindLb", "mainBlueSlider", "mainBlueValue", "mainGreenSlider", "mainGreenValue", "mainRedSlider", "mainRedValue", "outConTypeCombo",
        "outputControllerHintText", "passthruPanel", "r2CustomEditorBtn", "rainbowBtn", "rainbowCycleInput", "rainbowLightSlider", "rsCustomEditorBtn", "rsOuterBindLb",
        "RumbleAutostopTimeIUD", "RumbleBoostIUD", "steeringWheelEmulationCalibrateBtn", "swipeDownBtn", "swipeDownLb", "swipeLeftBtn", "swipeLeftLb", "swipeRightBtn",
        "swipeRightLb", "swipeUpBtn", "swipeUpLb", "sxCustomEditorBtn", "szCustomEditorBtn", "touchClickPassthroughToggle", "touchContentControl1", "touchContentControl2",
        "touchContentControl3", "touchContentControl4", "touchDisInvertBtn", "touchDoubleTapToggle", "touchJitterToggle", "touchMouseJoystickPanel", "touchMouseStickOneEuroPanel", "touchOutputTabControl",
        "touchpadOutModeCombo", "touchpadPropertiesScroll", "touchRightClickToggle", "touchScrollToggle", "touchSlideToggle", "touchStickFrictionUD", "touchTapToggle", "useControllerReadoutCk",
        "useControllerUD",
    };
}