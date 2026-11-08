using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using Xceed.Wpf.Toolkit;

namespace DS4WindowsTests;

/// <summary>Detached real markup and disposable fields, never controller output or real profiles.</summary>
[TestClass]
[DoNotParallelize]
public class ProfileInputPresentationTests
{
    [TestMethod]
    public void AxisTouchpadAndGyroModesKeepFieldsReachableInBothThemesAndSizes()
    {
        WpfTestHost.Run(() =>
        {
            foreach (bool dark in new[] { false, true })
            foreach (Size size in PresentationHarness.Viewports)
            foreach (int section in new[] { 3, 5, 6 })
            {
                ProfileWorkspacePresentationTests.Theme(dark);
                UserControl view = ProfileWorkspacePresentationTests.Load("ProfileEditor");
                var host = new Window { Content = view };
                try
                {
                    InputFixture fixture = Configure(view, section);
                    string page = section == 3 ? "axis" : section == 5 ? "touchpad" : "gyro";
                    var scroll = (ScrollViewer)view.FindName(page + "PropertiesScroll");
                    foreach (int mode in Enumerable.Range(0, section == 3 ? 3 : 5))
                    {
                        if (section == 3) { fixture.LSOutputIndex = mode; fixture.RSOutputIndex = mode; }
                        if (section == 5) fixture.TouchpadOutputIndex = mode;
                        if (section == 6) fixture.GyroOutModeIndex = mode;
                        foreach (int deadType in section == 3 && mode == 1 ? new[] { 0, 1 } : new[] { 0 })
                        {
                            fixture.LSDeadTypeIndex = deadType; fixture.RSDeadTypeIndex = deadType;
                            PresentationHarness.Layout(view, size);
                            Assert.AreEqual(ScrollBarVisibility.Disabled, scroll.HorizontalScrollBarVisibility);
                            foreach (Control field in PresentationHarness.Descendants<Control>(scroll)
                                .Where(c => c.ActualWidth > 0 && PresentationHarness.Participates(c)
                                    && ((FrameworkElement)scroll.Content).IsAncestorOf(c)))
                            {
                                PresentationHarness.AssertReachable(view, field, $"{page}/{mode}/{deadType} " +
                                    (field.GetBindingExpression(Selector.SelectedIndexProperty)?.ParentBinding.Path?.Path ?? ""));
                                Rect fieldBounds = Bounds(scroll, field);
                                Assert.IsTrue(fieldBounds.Left >= -1 && fieldBounds.Right <= scroll.ViewportWidth + 1,
                                    $"{page}/{mode}/{deadType}: {field.Name}/{field.GetType().Name} " +
                                    (field is DoubleUpDown ? field.GetBindingExpression(DoubleUpDown.ValueProperty)?.ParentBinding.Path?.Path :
                                     field is IntegerUpDown ? field.GetBindingExpression(IntegerUpDown.ValueProperty)?.ParentBinding.Path?.Path :
                                     field.GetBindingExpression(Selector.SelectedIndexProperty)?.ParentBinding.Path?.Path) +
                                    $" must fit the content viewport: {fieldBounds} / {scroll.ViewportWidth}.");
                            }
                            var actionsBefore = Bounds(view, (FrameworkElement)view.FindName("saveBtn"));
                            scroll.ScrollToEnd();
                            PresentationHarness.Layout(view, size);
                            Assert.AreEqual(actionsBefore, Bounds(view, (FrameworkElement)view.FindName("saveBtn")),
                                "Scrolling must not move the profile commit/close actions.");
                            foreach (string name in new[] { "applyBtn", "saveBtn", "cancelBtn" })
                            {
                                var button = (FrameworkElement)view.FindName(name);
                                Rect b = Bounds(view, button);
                                Assert.IsTrue(b.Top >= 0 && b.Bottom <= view.ActualHeight);
                            }
                            var last = PresentationHarness.Descendants<Control>(scroll)
                                .Where(c => c is Button or ComboBox or IntegerUpDown or DoubleUpDown or CheckBox)
                                .Where(c => c.ActualWidth > 0 && PresentationHarness.Participates(c))
                                .OrderBy(c => Bounds(scroll, c).Bottom).LastOrDefault();
                            if (last != null && scroll.ScrollableHeight > 0)
                                Assert.IsTrue(Bounds(scroll, last).Bottom <= scroll.ActualHeight + 1,
                                    $"{page}/{mode}: the last field must actually be revealed by scrolling to the end.");
                            scroll.ScrollToTop();
                        }
                    }
                }
                finally { host.Content = null; host.Close(); }
            }
        });
    }

    [TestMethod]
    public void InputModesAndSettingsUpdateExistingBindingsWithoutChangingCommitTiming()
    {
        WpfTestHost.Run(() =>
        {
            ProfileWorkspacePresentationTests.Theme(true);
            var view = ProfileWorkspacePresentationTests.Load("ProfileEditor");
            var host = new Window { Content = view };
            try
            {
                InputFixture fixture = Configure(view, 3);
                PresentationHarness.Layout(view, PresentationHarness.Viewports[1]);
                var dead = Field<DoubleUpDown>(view, "LSDeadZone", DoubleUpDown.ValueProperty);
                Assert.AreEqual(UpdateSourceTrigger.LostFocus, dead.GetBindingExpression(DoubleUpDown.ValueProperty).ParentBinding.UpdateSourceTrigger);
                dead.SetCurrentValue(DoubleUpDown.ValueProperty, 0.27d);
                Assert.AreEqual(0.15d, fixture.LSDeadZone, "Changing the UI must not bypass LostFocus commit timing.");
                dead.GetBindingExpression(DoubleUpDown.ValueProperty).UpdateSource();
                Assert.AreEqual(0.27d, fixture.LSDeadZone);
                Select(view, "LSOutputIndex", 2); Assert.AreEqual(2, fixture.LSOutputIndex);
                Select(view, "LSOutputIndex", 1); Assert.AreEqual(1, fixture.LSOutputIndex);
                Assert.AreEqual(0.27d, Field<DoubleUpDown>(view, "LSDeadZone", DoubleUpDown.ValueProperty).Value);
                ConfigureSection(view, 5);
                PresentationHarness.Layout(view, PresentationHarness.Viewports[1]);
                var slide = (CheckBox)view.FindName("touchSlideToggle");
                slide.SetCurrentValue(ToggleButton.IsCheckedProperty, false);
                slide.GetBindingExpression(ToggleButton.IsCheckedProperty).UpdateSource();
                Assert.IsFalse(fixture.TouchSenExists);
                Assert.IsFalse(Field<IntegerUpDown>(view, "TouchSens", IntegerUpDown.ValueProperty).IsEnabled);
                slide.SetCurrentValue(ToggleButton.IsCheckedProperty, true);
                slide.GetBindingExpression(ToggleButton.IsCheckedProperty).UpdateSource();
                Assert.IsTrue(Field<IntegerUpDown>(view, "TouchSens", IntegerUpDown.ValueProperty).IsEnabled);
                var rotation = Field<DoubleUpDown>(view, "TouchRelMouseRotation", DoubleUpDown.ValueProperty);
                rotation.SetCurrentValue(DoubleUpDown.ValueProperty, -90d);
                rotation.GetBindingExpression(DoubleUpDown.ValueProperty).UpdateSource();
                Assert.AreEqual(-90d, fixture.TouchRelMouseRotation);
                Select(view, "TouchpadOutputIndex", 3);
                var maxX = Field<IntegerUpDown>(view, "TouchAbsMouseMaxZoneX", IntegerUpDown.ValueProperty);
                maxX.SetCurrentValue(IntegerUpDown.ValueProperty, 85);
                maxX.GetBindingExpression(IntegerUpDown.ValueProperty).UpdateSource();
                Assert.AreEqual(85, fixture.TouchAbsMouseMaxZoneX);
                Select(view, "TouchpadOutputIndex", 0);
                Assert.AreEqual(-90d, Field<DoubleUpDown>(view, "TouchRelMouseRotation", DoubleUpDown.ValueProperty).Value);
                ConfigureSection(view, 6);
                Select(view, "GyroOutModeIndex", 1);
                var sensitivity = Field<IntegerUpDown>(view, "GyroSensitivity", IntegerUpDown.ValueProperty);
                sensitivity.SetCurrentValue(IntegerUpDown.ValueProperty, 140);
                sensitivity.GetBindingExpression(IntegerUpDown.ValueProperty).UpdateSource();
                Assert.AreEqual(140, fixture.GyroSensitivity);
                Select(view, "GyroOutModeIndex", 3);
                var swipe = Field<IntegerUpDown>(view, "GyroSwipeDeadZoneX", IntegerUpDown.ValueProperty);
                swipe.SetCurrentValue(IntegerUpDown.ValueProperty, 600);
                swipe.GetBindingExpression(IntegerUpDown.ValueProperty).UpdateSource();
                Assert.AreEqual(600, fixture.GyroSwipeDeadZoneX);
                Select(view, "GyroOutModeIndex", 1);
                Assert.AreEqual(140, Field<IntegerUpDown>(view, "GyroSensitivity", IntegerUpDown.ValueProperty).Value);
            }
            finally { host.Content = null; host.Close(); }
        });
    }

    private static Rect Bounds(FrameworkElement root, FrameworkElement child) =>
        child.TransformToAncestor(root).TransformBounds(new Rect(child.RenderSize));

    private static T Field<T>(UserControl view, string path, DependencyProperty property) where T : FrameworkElement =>
        PresentationHarness.Descendants<T>(view).Single(c => c.GetBindingExpression(property)?.ParentBinding.Path?.Path == path);

    private static void Select(UserControl view, string path, int index)
    {
        PresentationHarness.Layout(view, PresentationHarness.Viewports[1]);
        var combo = Field<ComboBox>(view, path, Selector.SelectedIndexProperty);
        combo.SetCurrentValue(Selector.SelectedIndexProperty, index);
        combo.GetBindingExpression(Selector.SelectedIndexProperty).UpdateSource();
        PresentationHarness.Layout(view, PresentationHarness.Viewports[1]);
        var pages = PresentationHarness.Descendants<TabControl>(view)
            .Single(tab => tab.GetBindingExpression(Selector.SelectedIndexProperty)?.ParentBinding.Path?.Path == path);
        Assert.AreEqual(index, pages.SelectedIndex, "Mode selection must route the real content tabs.");
    }

    internal static InputFixture Configure(UserControl view, int section)
    {
        var fixture = new InputFixture(); view.DataContext = fixture;
        var name = (TextBox)view.FindName("profileNameTxt"); name.Text = "Default"; name.IsEnabled = false;
        ConfigureSection(view, section);
        return fixture;
    }

    private static void ConfigureSection(UserControl view, int section)
    {
        ((ListBox)view.FindName("sectionNavigationList")).SelectedIndex = section;
        ((TabControl)view.FindName("sidebarTabControl")).Visibility = Visibility.Collapsed;
        var settings = (TabControl)view.FindName("profileSettingsTabCon");
        settings.Visibility = Visibility.Visible; settings.SelectedIndex = section - 3;
    }

    public sealed class InputFixture : ProfileWorkspacePresentationTests.EditorFixture, INotifyPropertyChanged
    {
        private int lsMode = 1, rsMode = 1, lsDeadType, rsDeadType, touchMode, gyroMode;
        private bool slide = true;
        public override int LSOutputIndex { get => lsMode; set { lsMode = value; Changed(nameof(LSOutputIndex)); } }
        public override int RSOutputIndex { get => rsMode; set { rsMode = value; Changed(nameof(RSOutputIndex)); } }
        public int LSDeadTypeIndex { get => lsDeadType; set { lsDeadType = value; Changed(nameof(LSDeadTypeIndex)); } }
        public int RSDeadTypeIndex { get => rsDeadType; set { rsDeadType = value; Changed(nameof(RSDeadTypeIndex)); } }
        public int TouchpadOutputIndex { get => touchMode; set { touchMode = value; Changed(nameof(TouchpadOutputIndex)); } }
        public int GyroOutModeIndex { get => gyroMode; set { gyroMode = value; Changed(nameof(GyroOutModeIndex)); } }
        public bool TouchSenExists { get => slide; set { slide = value; Changed(nameof(TouchSenExists)); } }
        public int TouchSens { get; set; } = 100;
        public bool TouchTapExists { get; set; }
        public int TouchTap { get; set; }
        public bool TouchScrollExists { get; set; }
        public int TouchScroll { get; set; }
        public bool TouchJitter { get; set; } = true;
        public bool TouchDoubleTap { get; set; }
        public bool LowerRightTouchRMB { get; set; }
        public bool TouchpadClickPassthru { get; set; }
        public double TouchRelMouseRotation { get; set; }
        public double TouchRelMouseMinThreshold { get; set; } = 1;
        public int TouchInvertIndex { get; set; }
        public int TouchAbsMouseMaxZoneX { get; set; } = 100;
        public int TouchAbsMouseMaxZoneY { get; set; } = 100;
        public bool TouchAbsMouseSnapCenter { get; set; }
        public int GyroSensitivity { get; set; } = 100;
        public int GyroMouseDeadZone { get; set; } = 10;
        public int GyroSwipeDeadZoneX { get; set; } = 300;
        public int GyroSwipeDeadZoneY { get; set; } = 300;
        public int GyroSwipeDelayTime { get; set; } = 100;
        public double GyroMouseMinThreshold { get; set; } = 1;
        public double LSMaxZone { get; set; } = 1;
        public double LSAntiDeadZone { get; set; } = 0.2;
        public double LSMaxOutput { get; set; } = 1;
        public double LSVerticalScale { get; set; } = 1;
        public double LSSens { get; set; } = 1;
        public string MappingName { get; set; } = "A longer assigned action name to verify wrapping in the available column";
        public event PropertyChangedEventHandler PropertyChanged;
        private void Changed(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
