using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Xceed.Wpf.Toolkit;

namespace DS4WindowsTests;

/// <summary>
/// Actual editor markup with disposable bindings, not the live editor/model.
/// These checks cover layout and field updates, not HID colour preview or persistence.
/// </summary>
[TestClass]
[DoNotParallelize]
public class LightbarPresentationTests
{
    [TestMethod]
    public void LightbarFieldsAlignAndRemainReachableIncludingAtTheBottomOfThePage()
    {
        WpfTestHost.Run(() =>
        {
            foreach (bool dark in new[] { false, true })
            foreach (Size size in PresentationHarness.Viewports)
            {
                ProfileWorkspacePresentationTests.Theme(dark);
                var view = ProfileWorkspacePresentationTests.Load("ProfileEditor");
                var host = new Window { Content = view };
                try
                {
                    Configure(view);
                    PresentationHarness.Layout(view, size);
                    var scroll = (ScrollViewer)view.FindName("lightbarScroll");
                    foreach (bool batteryColour in new[] { false, true })
                    {
                        // Mirrors only visibility changed by the unchanged click
                        // handler; the fixture must never construct the live editor.
                        ((GroupBox)view.FindName("emptyColorGB")).Visibility = batteryColour ? Visibility.Visible : Visibility.Collapsed;
                        PresentationHarness.Layout(view, size);
                        foreach (FrameworkElement field in PresentationHarness.Descendants<FrameworkElement>(scroll)
                            .Where(c => c.ActualWidth > 0 && PresentationHarness.Participates(c) &&
                                (c is Slider or ComboBox or IntegerUpDown or Button or CheckBox or TextBlock)))
                            PresentationHarness.AssertReachable(view, field, "Lightbar");
                        Rect Bounds(string name) => ((FrameworkElement)view.FindName(name))
                            .TransformToAncestor(view).TransformBounds(new Rect(((FrameworkElement)view.FindName(name)).RenderSize));
                        foreach (string name in new[] { "mainGreenSlider", "mainBlueSlider" })
                        {
                            Assert.AreEqual(Bounds("mainRedSlider").Left, Bounds(name).Left, 0.1);
                            Assert.AreEqual(Bounds("mainRedSlider").Right, Bounds(name).Right, 0.1);
                        }
                        foreach (var pair in new[] { ("mainRedSlider", "mainRedValue"), ("mainGreenSlider", "mainGreenValue"), ("mainBlueSlider", "mainBlueValue") })
                            Assert.IsTrue(Bounds(pair.Item1).Right <= Bounds(pair.Item2).Left + 0.1,
                                "Channel values must not overlap their sliders.");
                        Assert.AreEqual(ScrollBarVisibility.Disabled, scroll.HorizontalScrollBarVisibility);
                        Assert.IsTrue(scroll.ScrollableHeight > 0, "Bottom options require real vertical scrolling.");
                        scroll.ScrollToEnd();
                        PresentationHarness.Layout(view, size);
                        var last = (Slider)view.FindName("rainbowLightSlider");
                        Rect lastBounds = last.TransformToAncestor(scroll).TransformBounds(new Rect(last.RenderSize));
                        Assert.IsTrue(lastBounds.Top >= 0 && lastBounds.Bottom <= scroll.ActualHeight,
                            "Scrolling to the end must actually reveal the last field.");
                        foreach (string name in new[] { "applyBtn", "saveBtn", "cancelBtn" })
                        {
                            Rect button = Bounds(name);
                            Assert.IsTrue(button.Top >= 0 && button.Bottom <= view.ActualHeight);
                            Assert.IsTrue(button.Bottom < scroll.TransformToAncestor(view).Transform(new Point()).Y,
                                "Profile actions stay outside the scrolling properties.");
                        }
                        scroll.ScrollToTop();
                    }
                }
                finally { host.Content = null; host.Close(); }
            }
        });
    }

    [TestMethod]
    public void LightbarBindingsPreserveChannelsModesNumericLimitsAndRainbowState()
    {
        WpfTestHost.Run(() =>
        {
            ProfileWorkspacePresentationTests.Theme(true);
            var view = ProfileWorkspacePresentationTests.Load("ProfileEditor");
            var host = new Window { Content = view };
            try
            {
                var fixture = Configure(view);
                PresentationHarness.Layout(view, PresentationHarness.Viewports[1]);
                void SliderValue(string name, double value)
                {
                    var slider = (Slider)view.FindName(name);
                    slider.SetCurrentValue(RangeBase.ValueProperty, value);
                    slider.GetBindingExpression(RangeBase.ValueProperty).UpdateSource();
                    Assert.IsFalse(string.IsNullOrWhiteSpace(AutomationProperties.GetName(slider)));
                }
                SliderValue("mainRedSlider", 255); SliderValue("mainGreenSlider", 0); SliderValue("mainBlueSlider", 97);
                Assert.AreEqual(255, fixture.MainColorR); Assert.AreEqual(0, fixture.MainColorG); Assert.AreEqual(97, fixture.MainColorB);
                Assert.AreEqual("255", ((TextBlock)view.FindName("mainRedValue")).Text);
                SliderValue("lowRedSlider", 43); SliderValue("lowGreenSlider", 21); SliderValue("lowBlueSlider", 9);
                Assert.AreEqual(43, fixture.LowColorR); Assert.AreEqual(21, fixture.LowColorG); Assert.AreEqual(9, fixture.LowColorB);
                var flash = (IntegerUpDown)view.FindName("flashAtInput");
                Assert.AreEqual(-10, flash.Minimum); Assert.AreEqual(100, flash.Maximum); Assert.AreEqual(10, flash.Increment);
                foreach (int threshold in new[] { -10, 100 })
                {
                    flash.SetCurrentValue(IntegerUpDown.ValueProperty, threshold);
                    flash.GetBindingExpression(IntegerUpDown.ValueProperty).UpdateSource();
                    Assert.AreEqual(threshold, fixture.FlashAt);
                }
                var alert = (ComboBox)view.FindName("flashTypeCombo");
                alert.SetCurrentValue(Selector.SelectedIndexProperty, 1);
                alert.GetBindingExpression(Selector.SelectedIndexProperty).UpdateSource();
                Assert.AreEqual(1, fixture.FlashTypeIndex);
                var charging = (ComboBox)view.FindName("chargingTypeCombo");
                charging.SetCurrentValue(Selector.SelectedIndexProperty, 3);
                charging.GetBindingExpression(Selector.SelectedIndexProperty).UpdateSource();
                Assert.AreEqual(3, fixture.ChargingType);
                Assert.AreEqual(Visibility.Visible, ((Button)view.FindName("chargingColorBtn")).Visibility);
                var battery = (CheckBox)view.FindName("colorByBatteryPerCk");
                battery.SetCurrentValue(ToggleButton.IsCheckedProperty, true);
                battery.GetBindingExpression(ToggleButton.IsCheckedProperty).UpdateSource();
                Assert.IsTrue(fixture.ColorBatteryPercent);
                battery.Content = "Dim by Battery %";
                Assert.AreEqual("Dim by Battery %", AutomationProperties.GetName(battery));
                var cycle = (IntegerUpDown)view.FindName("rainbowCycleInput");
                Assert.AreEqual(0, cycle.Minimum); Assert.AreEqual(60, cycle.Maximum); Assert.AreEqual(1, cycle.Increment);
                Assert.IsFalse(cycle.IsEnabled);
                Assert.AreEqual("Turn on", ((Button)view.FindName("rainbowBtn")).Content);
                fixture.Rainbow = 5;
                Assert.IsTrue(cycle.IsEnabled);
                Assert.AreEqual("Turn off", ((Button)view.FindName("rainbowBtn")).Content);
                cycle.SetCurrentValue(IntegerUpDown.ValueProperty, 60);
                cycle.GetBindingExpression(IntegerUpDown.ValueProperty).UpdateSource();
                Assert.AreEqual(60d, fixture.Rainbow);
                SliderValue("rainbowLightSlider", 40);
                Assert.AreEqual(40d, fixture.MaxSatRainbow);
                fixture.Rainbow = 0;
                Assert.IsFalse(((Slider)view.FindName("rainbowLightSlider")).IsEnabled);
                Assert.AreEqual("Turn on", ((Button)view.FindName("rainbowBtn")).Content);
                var mode = (ComboBox)view.FindName("lightbarModeCombo");
                mode.SetCurrentValue(Selector.SelectedIndexProperty, 1);
                mode.GetBindingExpression(Selector.SelectedIndexProperty).UpdateSource();
                Assert.AreEqual(1, fixture.LightbarModeIndex);
                var modes = (TabControl)view.FindName("lightbarModeTabs");
                Assert.AreEqual(1, modes.SelectedIndex);
                PresentationHarness.Layout(view, PresentationHarness.Viewports[1]);
                Assert.IsInstanceOfType(modes.SelectedContent, typeof(TextBlock));
                Assert.IsFalse(PresentationHarness.Descendants<Slider>(modes).Any(),
                    "Passthrough must unmount the normal-mode property controls, not merely leave them enabled off-screen.");
                mode.SetCurrentValue(Selector.SelectedIndexProperty, 0);
                mode.GetBindingExpression(Selector.SelectedIndexProperty).UpdateSource();
                PresentationHarness.Layout(view, PresentationHarness.Viewports[1]);
                Assert.IsTrue(PresentationHarness.Descendants<Slider>(modes).Contains((Slider)view.FindName("mainRedSlider")));
                Assert.AreEqual(255d, ((Slider)view.FindName("mainRedSlider")).Value);
                Assert.AreEqual(100, ((IntegerUpDown)view.FindName("flashAtInput")).Value);
            }
            finally { host.Content = null; host.Close(); }
        });
    }

    [TestMethod]
    public void ColourPreviewAndEffectControlsStillUseTheirExistingHandlers()
    {
        var source = PresentationHarness.Source("ProfileEditor");
        foreach (var pair in new[]
        {
            ("lowColorBtn", "LowColorBtn_Click"), ("flashColorBtn", "FlashColorBtn_Click"),
            ("chargingColorBtn", "ChargingColorBtn_Click"), ("rainbowBtn", "RainbowBtn_Click"),
            ("colorByBatteryPerCk", "ColorByBatteryPerCk_Click")
        })
            Assert.AreEqual(pair.Item2, (string)source.Descendants()
                .Single(e => (string)e.Attribute(PresentationHarness.X + "Name") == pair.Item1).Attribute("Click"));
    }

    internal static LightbarFixture Configure(UserControl view)
    {
        var fixture = new LightbarFixture();
        view.DataContext = fixture;
        ((ListBox)view.FindName("sectionNavigationList")).SelectedIndex = 4;
        ((TabControl)view.FindName("sidebarTabControl")).Visibility = Visibility.Collapsed;
        var settings = (TabControl)view.FindName("profileSettingsTabCon");
        settings.Visibility = Visibility.Visible; settings.SelectedIndex = 1;
        ((GroupBox)view.FindName("emptyColorGB")).Visibility = Visibility.Collapsed;
        var name = (TextBox)view.FindName("profileNameTxt");
        name.Text = "Default"; name.IsEnabled = false;
        return fixture;
    }

    public sealed class LightbarFixture : ProfileWorkspacePresentationTests.EditorFixture, INotifyPropertyChanged
    {
        private int red = 70, green = 145, blue = 210;
        private int mode;
        private int chargingType;
        private double rainbow;
        public int MainColorR { get => red; set { red = value; Changed(nameof(MainColorR)); } }
        public int MainColorG { get => green; set { green = value; Changed(nameof(MainColorG)); } }
        public int MainColorB { get => blue; set { blue = value; Changed(nameof(MainColorB)); } }
        public string MainColorRString => "#FF460000";
        public string MainColorGString => "#FF009100";
        public string MainColorBString => "#FF0000D2";
        public int LowColorR { get; set; } = 255;
        public int LowColorG { get; set; } = 35;
        public int LowColorB { get; set; } = 40;
        public string LowColor => "#FFFF2328";
        public string LowColorRString => "#FFFF0000";
        public string LowColorGString => "#FF002300";
        public string LowColorBString => "#FF000028";
        public string FlashColor => "#FFFFB24A";
        public string ChargingColor => "#FF4691D2";
        public int LightbarModeIndex { get => mode; set { mode = value; Changed(nameof(LightbarModeIndex)); } }
        public bool ColorBatteryPercent { get; set; }
        public int FlashTypeIndex { get; set; }
        public int FlashAt { get; set; } = 20;
        public int ChargingType { get => chargingType; set { chargingType = value; Changed(nameof(ChargingColorVisible)); } }
        public Visibility ChargingColorVisible => chargingType == 3 ? Visibility.Visible : Visibility.Hidden;
        public double Rainbow { get => rainbow; set { rainbow = value; Changed(nameof(Rainbow)); Changed(nameof(RainbowExists)); } }
        public bool RainbowExists => Rainbow != 0;
        public double MaxSatRainbow { get; set; } = 85;
        public event PropertyChangedEventHandler PropertyChanged;
        private void Changed(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
