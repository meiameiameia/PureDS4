using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using Xceed.Wpf.Toolkit;

namespace DS4WindowsTests;

// Actual markup with disposable fields; no output, audio, presets or file operations.
[TestClass]
[DoNotParallelize]
public class AdvancedProfilePresentationTests
{
    [TestMethod]
    public void AdvancedDependenciesAndExpandedSettingsStayInsideThePropertiesViewport()
    {
        WpfTestHost.Run(() =>
        {
            foreach (bool dark in new[] { false, true })
            foreach (Size size in PresentationHarness.Viewports)
            {
                ProfileWorkspacePresentationTests.Theme(dark);
                UserControl view = ProfileWorkspacePresentationTests.Load("ProfileEditor");
                var host = new Window { Content = view };
                try
                {
                    AdvancedFixture fixture = Configure(view);
                    var scroll = (ScrollViewer)view.FindName("advancedPropertiesScroll");
                    foreach (bool expanded in new[] { false, true })
                    {
                        fixture.ShowPlayStationControllerSettings = expanded;
                        PresentationHarness.Layout(view, size);
                        foreach (Expander expander in PresentationHarness.Descendants<Expander>(scroll))
                            expander.IsExpanded = expanded;
                        PresentationHarness.Layout(view, size);
                        AssertContentBounds(view, scroll, "Advanced");
                        Rect actions = Bounds(view, (Button)view.FindName("saveBtn"));
                        scroll.ScrollToEnd(); PresentationHarness.Layout(view, size);
                        Assert.AreEqual(actions, Bounds(view, (Button)view.FindName("saveBtn")));
                        if (expanded)
                        {
                            var snap = Field<CheckBox>(scroll, "AbsSnapCenter", ToggleButton.IsCheckedProperty);
                            Assert.IsTrue(Bounds(scroll, snap).Bottom <= scroll.ActualHeight + 1,
                                "The last expanded setting must be visible at the end of the scroll extent.");
                        }
                        scroll.ScrollToTop();
                    }
                    fixture.ShowPlayStationControllerSettings = true;
                    PresentationHarness.Layout(view, size);
                    var output = (ComboBox)view.FindName("outConTypeCombo");
                    var disable = Field<CheckBox>(scroll, "DInputOnly", ToggleButton.IsCheckedProperty);
                    SetCheck(disable, true); Assert.IsTrue(fixture.DInputOnly); Assert.IsFalse(output.IsEnabled);
                    SetCheck(disable, false); Assert.IsTrue(output.IsEnabled);
                    var stream = Field<CheckBox>(scroll, "DualSenseEnableSpeakerOutput", ToggleButton.IsCheckedProperty);
                    var headset = Field<CheckBox>(scroll, "DualSenseHeadsetOnlyAudio", ToggleButton.IsCheckedProperty);
                    SetCheck(stream, false); Assert.IsFalse(headset.IsEnabled);
                    SetCheck(stream, true); Assert.IsTrue(headset.IsEnabled);
                    var idle = Field<CheckBox>(scroll, "IdleDisconnectExists", ToggleButton.IsCheckedProperty);
                    var minutes = (IntegerUpDown)view.FindName("idleDisconnectUD");
                    SetCheck(idle, false); Assert.IsFalse(minutes.IsEnabled);
                    SetCheck(idle, true); Assert.IsTrue(minutes.IsEnabled);
                    var debounce = (IntegerUpDown)view.FindName("Debouncing");
                    debounce.SetCurrentValue(IntegerUpDown.ValueProperty, 650);
                    debounce.GetBindingExpression(IntegerUpDown.ValueProperty).UpdateSource();
                    Assert.AreEqual(650, fixture.DebouncingMs);
                    var offset = Field<DoubleUpDown>(scroll, "ButtonMouseOffset", DoubleUpDown.ValueProperty);
                    Assert.AreEqual(UpdateSourceTrigger.LostFocus,
                        offset.GetBindingExpression(DoubleUpDown.ValueProperty).ParentBinding.UpdateSourceTrigger);
                    offset.SetCurrentValue(DoubleUpDown.ValueProperty, 0.125d);
                    Assert.AreEqual(0d, fixture.ButtonMouseOffset);
                    offset.GetBindingExpression(DoubleUpDown.ValueProperty).UpdateSource();
                    Assert.AreEqual(0.125d, fixture.ButtonMouseOffset);
                    // Selection edits the fixture only; nothing applies a real output or profile.
                    output.SetCurrentValue(Selector.SelectedIndexProperty, 0);
                    output.GetBindingExpression(Selector.SelectedIndexProperty).UpdateSource();
                    Assert.AreEqual(0, fixture.TempControllerIndex);
                    output.SetCurrentValue(Selector.SelectedIndexProperty, 1);
                    output.GetBindingExpression(Selector.SelectedIndexProperty).UpdateSource();
                    Assert.AreEqual(1, fixture.TempControllerIndex);
                }
                finally { host.Content = null; host.Close(); }
            }
        });
    }

    [TestMethod]
    public void ProfileHelpersKeepLongContentScrollableAndActionsOutsideIt()
    {
        WpfTestHost.Run(() =>
        {
            foreach (bool dark in new[] { false, true })
            foreach (bool compact in new[] { true, false })
            foreach (string page in new[] { "preset-intro", "preset", "rename", "duplicate", "calibration" })
            {
                ProfileWorkspacePresentationTests.Theme(dark);
                UserControl view = CreateHelper(page, true);
                var host = new Window { Content = view };
                try
                {
                    Size size = page.StartsWith("preset") ? new Size(compact ? 448 : 568, compact ? 240 : 300) :
                        page == "rename" ? new Size(compact ? 344 : 464, compact ? 136 : 166) :
                        page == "duplicate" ? new Size(compact ? 320 : 760, 138) : new Size(444, 276);
                    PresentationHarness.Layout(view, size);
                    var buttons = PresentationHarness.Descendants<Button>(view)
                        .Where(b => b.ActualWidth > 0 && PresentationHarness.Participates(b)).ToArray();
                    foreach (Button button in buttons)
                    {
                        PresentationHarness.AssertReachable(view, button, page);
                        Rect b = Bounds(view, button);
                        Assert.IsTrue(b.Top >= -1 && b.Bottom <= view.ActualHeight + 1, page + ": footer actions must be visible.");
                    }
                    var scroll = PresentationHarness.Descendants<ScrollViewer>(view)
                        .FirstOrDefault(s => s.Name.EndsWith("Scroll") && s.ActualWidth > 0 && PresentationHarness.Participates(s));
                    if (scroll != null)
                    {
                        Rect[] before = buttons.Select(b => Bounds(view, b)).ToArray();
                        AssertContentBounds(view, scroll, page);
                        scroll.ScrollToEnd(); PresentationHarness.Layout(view, size);
                        CollectionAssert.AreEqual(before, buttons.Select(b => Bounds(view, b)).ToArray());
                    }
                    if (page == "preset")
                    {
                        var fixture = (HelperFixture)view.DataContext;
                        var choice = (ComboBox)view.FindName("presetComboBox");
                        Assert.AreEqual(2, choice.Items.Count);
                        choice.SetCurrentValue(Selector.SelectedIndexProperty, 1);
                        choice.GetBindingExpression(Selector.SelectedIndexProperty).UpdateSource();
                        Assert.AreEqual(1, fixture.PresetIndex);
                        fixture.PresetDisplayOutputCont = false; PresentationHarness.Layout(view, size);
                        Assert.AreEqual(Visibility.Collapsed, ((StackPanel)view.FindName("outputContStackPanel")).Visibility);
                        fixture.PresetDisplayOutputCont = true; PresentationHarness.Layout(view, size);
                        Assert.AreEqual(Visibility.Visible, ((StackPanel)view.FindName("outputContStackPanel")).Visibility);
                    }
                    if (page == "rename")
                    {
                        var name = (TextBox)view.FindName("profileNameTxt");
                        name.SetCurrentValue(TextBox.TextProperty, "A changed disposable name");
                        name.GetBindingExpression(TextBox.TextProperty).UpdateSource();
                        Assert.AreEqual("A changed disposable name", ((HelperFixture)view.DataContext).ProfileName);
                    }
                    if (page == "duplicate")
                    {
                        var name = (TextBox)view.FindName("profileTxt");
                        string text = name.Text;
                        name.Text = ""; PresentationHarness.Layout(view, size);
                        name.Text = text; Assert.AreEqual(text, name.Text);
                        // Keep the selected source visible while its inline copy
                        // editor is open, rather than consuming the whole table.
                        var browser = ProfileWorkspacePresentationTests.Load("MainWindow", "profilesBrowserPanel");
                        var bar = (Border)browser.FindName("dupBoxBar");
                        bar.Child = CreateHelper("duplicate"); bar.Visibility = Visibility.Visible;
                        var list = (ListBox)browser.FindName("profilesListBox");
                        list.ItemsSource = new[] { new { Name = "Default", GameOutputDisplay = "Xbox 360", ModifiedDisplay = "Preview" } };
                        list.SelectedIndex = 0;
                        host.Content = browser;
                        PresentationHarness.Layout(browser, new Size(compact ? 704 : 1008, compact ? 268 : 448));
                        var row = (FrameworkElement)list.ItemContainerGenerator.ContainerFromIndex(0);
                        Assert.IsNotNull(row);
                        var rowsScroll = (ScrollViewer)list.Template.FindName("ProfileRowsScroll", list);
                        Rect rowBounds = Bounds(rowsScroll, row);
                        Assert.IsTrue(rowBounds.Top >= -1 && rowBounds.Bottom <= rowsScroll.ActualHeight + 1,
                            "Opening inline duplication must leave the selected source profile visible.");
                    }
                    if (page == "calibration")
                    {
                        var save = (Button)view.FindName("saveButton");
                        Assert.IsFalse(save.IsEnabled);
                        Rect before = Bounds(view, save);
                        save.IsEnabled = true; PresentationHarness.Layout(view, size);
                        Assert.AreEqual(before, Bounds(view, save), "Save readiness must not move the calibration actions.");
                    }
                }
                finally { host.Content = null; host.Close(); }
            }
        });
    }

    internal static AdvancedFixture Configure(UserControl view)
    {
        var fixture = new AdvancedFixture(); view.DataContext = fixture;
        ((TextBox)view.FindName("profileNameTxt")).Text = "Default";
        ((TextBox)view.FindName("profileNameTxt")).IsEnabled = false;
        ((ListBox)view.FindName("sectionNavigationList")).SelectedIndex = 7;
        ((TabControl)view.FindName("sidebarTabControl")).Visibility = Visibility.Collapsed;
        var settings = (TabControl)view.FindName("profileSettingsTabCon");
        settings.Visibility = Visibility.Visible; settings.SelectedIndex = 4;
        return fixture;
    }

    internal static UserControl CreateHelper(string page, bool longText = false)
    {
        string source = page.StartsWith("preset") ? "PresetOptionWindow" : page == "rename" ? "RenameProfileWindow" :
            page == "duplicate" ? "DupBox" : "StickCalibrationWindow";
        UserControl view = ProfileWorkspacePresentationTests.Load(source);
        view.DataContext = new HelperFixture(longText);
        if (page.StartsWith("preset"))
        {
            var tabs = (TabControl)view.FindName("screensTabControl");
            foreach (TabItem item in tabs.Items) item.Visibility = Visibility.Collapsed;
            tabs.SelectedIndex = page == "preset" ? 1 : 0;
        }
        if (page == "duplicate") ((TextBox)view.FindName("profileTxt")).Text = "Default copy" + (longText ? new string('x', 90) : "");
        if (page == "calibration" && longText)
        {
            ((TextBlock)view.FindName("headingText")).Text = "Measure where the right stick rests";
            ((TextBlock)view.FindName("statusText")).Text = string.Join(" ", Enumerable.Repeat("The controller is no longer connected. Keep the controller still before measuring again.", 5));
        }
        return view;
    }

    private static void SetCheck(CheckBox box, bool value)
    {
        box.SetCurrentValue(ToggleButton.IsCheckedProperty, value);
        box.GetBindingExpression(ToggleButton.IsCheckedProperty).UpdateSource();
    }

    private static T Field<T>(FrameworkElement view, string path, DependencyProperty property) where T : FrameworkElement =>
        PresentationHarness.Descendants<T>(view).Single(c => c.GetBindingExpression(property)?.ParentBinding.Path?.Path == path);

    private static Rect Bounds(FrameworkElement root, FrameworkElement child) =>
        child.TransformToAncestor(root).TransformBounds(new Rect(child.RenderSize));

    private static void AssertContentBounds(FrameworkElement view, ScrollViewer scroll, string page)
    {
        foreach (Control field in PresentationHarness.Descendants<Control>(scroll)
            .Where(c => c.ActualWidth > 0 && PresentationHarness.Participates(c) && ((FrameworkElement)scroll.Content).IsAncestorOf(c)))
        {
            PresentationHarness.AssertReachable(view, field, page);
            Rect b = Bounds(scroll, field);
            Assert.IsTrue(b.Left >= -1 && b.Right <= scroll.ViewportWidth + 1,
                $"{page}: {field.Name}/{field.GetType().Name} exceeds the content viewport: {b} / {scroll.ViewportWidth}.");
        }
    }

    public sealed class AdvancedFixture : ProfileWorkspacePresentationTests.EditorFixture, INotifyPropertyChanged
    {
        private bool direct, idle = true, audio = true, playStation;
        public bool DInputOnly { get => direct; set { direct = value; Changed(nameof(DInputOnly)); } }
        public bool IdleDisconnectExists { get => idle; set { idle = value; Changed(nameof(IdleDisconnectExists)); } }
        public bool DualSenseEnableSpeakerOutput { get => audio; set { audio = value; Changed(nameof(DualSenseEnableSpeakerOutput)); } }
        public bool ShowPlayStationControllerSettings { get => playStation; set { playStation = value; Changed(nameof(ShowPlayStationControllerSettings)); } }
        public bool ShowControllerAudioSettings => true;
        public string ControllerAudioHeader => "Controller audio";
        public string ControllerAudioDescription => "Disposable audio settings, without opening endpoints or sending audio.";
        public bool DualSenseHeadsetOnlyAudio { get; set; }
        public int DualSenseSpeakerVolume { get; set; } = 100;
        public int DualSenseSpeakerCompressionIndex { get; set; }
        public int DualSenseSpeakerBassBoost { get; set; }
        public int DualSenseHeadphoneVolume { get; set; } = 80;
        public bool ShowLegacyMicrophoneRouting => false;
        public bool CanChangeControllerMicrophoneInput => false;
        public bool ControllerMicrophoneLevelControlsEnabled => false;
        public bool ShowControllerMicrophoneAvailabilityMessage => true;
        public string ControllerMicrophoneToggleLabel => "Controller microphone";
        public string ControllerMicrophoneDescription => "Unavailable in this disposable fixture.";
        public string ControllerMicrophoneAvailabilityText => "Controller microphone input is not available in this preview.";
        public int DualSenseMicrophoneVolume { get; set; }
        public int DualSenseMicrophoneNoiseSuppressionIndex { get; set; }
        public bool DualSenseEnableMicrophonePassthrough { get; set; }
        public object[] AudioCaptureEndpointChoices => new[] { new { Name = "A long audio endpoint display name to check bounded selection", EndpointId = "fixture" } };
        public string DualSenseAudioCaptureEndpointId { get; set; } = "fixture";
        public int TempControllerIndex { get; set; } = 1;
        public object[] OutputDS4TriggerModeOptions => new[] { new { DisplayName = "Analog", ChoiceValue = 0 } };
        public int OutputDS4TriggerMode { get; set; }
        public int DebouncingMs { get; set; }
        public int RumbleBoost { get; set; } = 100;
        public int RumbleAutostopTime { get; set; }
        public bool InverseRumbleMotors { get; set; }
        public bool UseControllerReadout { get; set; }
        public int ButtonMouseSensitivity { get; set; } = 25;
        public int ButtonMouseVerticalScale { get; set; } = 100;
        public double ButtonMouseOffset { get; set; }
        public int OutputMouseSpeed => 250;
        public double MouseOffsetSpeed => 0;
        public bool MouseAcceleration { get; set; }
        public bool EnableTouchpadToggle { get; set; }
        public bool EnableOutputDataToDS4 { get; set; } = true;
        public bool GameBarControllerCompatibility { get; set; }
        public bool LaunchProgramExists { get; set; }
        public string LaunchProgramName => "Long program name for a disposable preview.exe";
        public int IdleDisconnect { get; set; } = 5;
        public int TempBTPollRateIndex { get; set; } = 4;
        public double AbsWidth { get; set; } = 1;
        public double AbsHeight { get; set; } = 1;
        public double AbsXCenter { get; set; } = 0.5;
        public double AbsYCenter { get; set; } = 0.5;
        public double AbsAntiRadius { get; set; }
        public bool AbsSnapCenter { get; set; }
        public event PropertyChangedEventHandler PropertyChanged;
        private void Changed(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public sealed class HelperFixture : INotifyPropertyChanged
    {
        private int index;
        private bool output = true;
        public HelperFixture(bool longText)
        {
            ProfileName = "Default copy" + (longText ? new string('x', 90) : "");
            PresetDescription = longText ? string.Join(" ", Enumerable.Repeat("A longer description to verify scrolling without moving the confirmation actions.", 20)) :
                "A disposable preset description. This preview does not apply settings to any profile.";
        }
        public string ProfileName { get; set; }
        public object[] PresetsList => new[] { new { Name = "Gamepad — preview" }, new { Name = "A longer preset name for the layout fixture" } };
        public object[] OutputChoices => new[] { new { DisplayName = "Xbox 360", ChoiceValue = 0 }, new { DisplayName = "DualShock 4", ChoiceValue = 1 } };
        public int ControllerChoice { get; set; }
        public int PresetIndex { get => index; set { index = value; Changed(nameof(PresetIndex)); } }
        public bool PresetDisplayOutputCont { get => output; set { output = value; Changed(nameof(PresetDisplayOutputCont)); } }
        public string PresetDescription { get; set; }
        public event PropertyChangedEventHandler PropertyChanged;
        private void Changed(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
