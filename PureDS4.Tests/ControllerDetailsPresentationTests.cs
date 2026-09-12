using DS4Windows;
using DS4WinWPF.DS4Forms;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DS4WindowsTests;

// The real view with presentation-only fixtures: no MainWindow, production
// view model, app startup, personal profiles, capture, or controller handles.
[TestClass]
[DoNotParallelize]
public class ControllerDetailsPresentationTests
{
    [TestMethod]
    public void SelectionTransportFailureAndRemovalRemainTruthfulAtBothSizesAndThemes()
    {
        WpfTestHost.Run(() =>
        {
            foreach (bool dark in new[] { false, true })
            foreach (Size size in new[] { new Size(704, 360), new Size(1008, 540) })
            {
                LoadTheme(dark);
                var state = new DetailsFixture();
                var view = new ControllerOverviewControl { DataContext = state };
                var owner = new Window { Content = view }; // Never shown or loaded.
                Layout(view, size);
                Assert.AreEqual(Visibility.Visible, Find<StackPanel>(view, "emptyState").Visibility);
                Assert.AreEqual(Visibility.Collapsed, Find<ScrollViewer>(view, "detailsScroll").Visibility);
                foreach (bool wireless in new[] { false, true })
                foreach (bool degraded in new[] { false, true })
                {
                    state.Select(new DeviceFixture(wireless), degraded);
                    Layout(view, size);
                    Assert.AreEqual(Visibility.Collapsed, Find<StackPanel>(view, "emptyState").Visibility);
                    Assert.AreEqual(state.SelectedController.IdText, Find<TextBlock>(view, "controllerIdentity").Text);
                    Assert.AreEqual(state.CurrentProfileName, Find<TextBlock>(view, "activeProfileText").Text);
                    Assert.AreEqual(degraded ? "Unavailable" : "Xbox 360", Find<TextBlock>(view, "runtimeOutputText").Text);
                    Assert.AreEqual(state.SelectedControllerStartupDetail, Find<TextBlock>(view, "runtimeDetailText").Text);
                    Assert.AreEqual(wireless ? Visibility.Visible : Visibility.Collapsed, Find<Button>(view, "disconnectButton").Visibility);
                    Assert.AreEqual(!degraded, Find<StackPanel>(view, "audioRoutingControls").IsEnabled);
                    Assert.IsFalse(Find<StackPanel>(view, "microphoneLevels").IsEnabled);
                    Assert.AreEqual(Visibility.Visible, Find<TextBlock>(view, "microphoneAvailabilityText").Visibility);
                    Assert.AreEqual(0, state.GestureCommitCount,
                        "Selection/layout must not write the one-way game-output or audio-source settings.");
                    var content = Find<StackPanel>(view, "selectedDetails");
                    foreach (var control in Descendants<Control>(content).Where(c => c.IsVisible || c.Visibility == Visibility.Visible))
                        AssertHorizontalBounds(content, control);
                    var detail = Find<TextBlock>(view, "runtimeDetailText");
                    Assert.AreEqual(TextWrapping.Wrap, detail.TextWrapping);
                    Assert.AreEqual(TextTrimming.None, detail.TextTrimming);
                    var scroll = Find<ScrollViewer>(view, "detailsScroll");
                    Assert.IsTrue(scroll.ScrollableHeight > 0,
                        $"Properties must remain scrollable, not clipped: dark={dark}, size={size}, wireless={wireless}, degraded={degraded}.");
                    scroll.ScrollToBottom();
                    Layout(view, size);
                    Assert.IsTrue(scroll.VerticalOffset > 0);
                    if (wireless && !degraded && size.Width == 1008) SavePreview(view, dark, "-audio");
                    scroll.ScrollToTop();
                    Layout(view, size);
                    if (wireless && !degraded && size.Width == 1008) SavePreview(view, dark);
                    if (wireless && degraded && size.Width == 704) SavePreview(view, dark, "-compact");
                }
                state.Select(null, false);
                Layout(view, size);
                Assert.AreEqual(Visibility.Collapsed, Find<ScrollViewer>(view, "detailsScroll").Visibility);
                Assert.IsFalse(Find<StackPanel>(view, "selectedDetails").IsEnabled);
                Assert.AreEqual(string.Empty, Find<TextBlock>(view, "controllerIdentity").Text);
                view.DataContext = null;
                owner.Content = null;
            }
        });
    }

    [TestMethod]
    public void ExistingDataEntryContractsAndActionsArePreserved()
    {
        WpfTestHost.Run(() =>
        {
            LoadTheme(false);
            var state = new DetailsFixture();
            state.Select(new DeviceFixture(false), false);
            var view = new ControllerOverviewControl { DataContext = state };
            Layout(view, new Size(1008, 540));
            foreach (var (name, path, maximum) in new[] {
                ("feedbackSlider", "HapticStrengthPercent", 100d),
                ("speakerSlider", "SpeakerVolumePercent", 100d),
                ("headphoneSlider", "HeadphoneVolumePercent", 100d),
                ("bassSlider", "SpeakerBassBoostDb", 6d),
                ("microphoneSlider", "MicrophoneVolumePercent", 100d) })
            {
                var slider = Find<Slider>(view, name);
                Binding binding = BindingOperations.GetBinding(slider, RangeBase.ValueProperty);
                Assert.AreEqual(path, binding.Path.Path);
                Assert.AreEqual(BindingMode.TwoWay, binding.Mode);
                Assert.AreEqual(UpdateSourceTrigger.PropertyChanged, binding.UpdateSourceTrigger);
                Assert.AreEqual(220, binding.Delay);
                Assert.AreEqual(0d, slider.Minimum);
                Assert.AreEqual(maximum, slider.Maximum);
                Assert.IsFalse(string.IsNullOrEmpty(System.Windows.Automation.AutomationProperties.GetName(slider)));
            }
            Assert.IsTrue(Find<Slider>(view, "bassSlider").IsSnapToTickEnabled);
            state.HeadsetOnlyAudio = true;
            state.Notify();
            Layout(view, new Size(1008, 540));
            Assert.IsFalse(Find<Slider>(view, "speakerSlider").IsEnabled);
            Assert.IsTrue(Find<Slider>(view, "headphoneSlider").IsEnabled);
            foreach (string name in new[] { "outputControllerCombo", "controllerAudioSourceCombo" })
                Assert.AreEqual(BindingMode.OneWay, BindingOperations.GetBinding(Find<ComboBox>(view, name), Selector.SelectedValueProperty).Mode);
            foreach (string name in new[] { "compressionCombo", "noiseCombo" })
                Assert.AreEqual(BindingMode.TwoWay, BindingOperations.GetBinding(Find<ComboBox>(view, name), Selector.SelectedIndexProperty).Mode);
            foreach (var toggle in Descendants<CheckBox>(view))
                Assert.AreEqual(BindingMode.TwoWay, BindingOperations.GetBinding(toggle, ToggleButton.IsCheckedProperty).Mode);
            // Stored microphone input can still be turned off when capability is
            // lost. Level controls remain unavailable; no new runtime policy here.
            state.MicrophoneInputEnabled = true;
            state.Notify();
            Layout(view, new Size(1008, 540));
            Assert.IsTrue(Find<CheckBox>(view, "microphoneToggle").IsEnabled);
            Assert.IsFalse(Find<StackPanel>(view, "microphoneLevels").IsEnabled);
            int edit = 0, home = 0, lightbar = 0, disconnect = 0;
            view.EditProfileRequested += (_, _) => edit++;
            view.ControllerDetailsRequested += (_, _) => home++;
            view.LightbarRequested += (_, _) => lightbar++;
            view.DisconnectRequested += (_, _) => disconnect++;
            foreach (var button in Descendants<Button>(Find<StackPanel>(view, "selectedDetails")))
                if (button.Content is string label && new[] { "Edit active profile", "Back to Home", "Lightbar controls" }.Contains(label))
                    button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.AreEqual(1, edit); Assert.AreEqual(1, home); Assert.AreEqual(1, lightbar);
            Assert.AreSame(Find<Button>(view, "lightbarButton"), view.LightbarMenuAnchor);
            state.Select(new DeviceFixture(true), false);
            Layout(view, new Size(1008, 540));
            Find<Button>(view, "disconnectButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.AreEqual(1, disconnect);
            view.DataContext = null;
        });
    }

    [TestMethod]
    public void SharedSliderRetainsNativeTrackCommandsAndStableDisabledGeometry()
    {
        WpfTestHost.Run(() =>
        {
            foreach (bool dark in new[] { false, true })
            {
                LoadTheme(dark);
                var slider = new Slider { Style = (Style)Application.Current.FindResource("FoundationHorizontalSliderStyle"), Minimum = 0, Maximum = 6, Value = 2, TickFrequency = 1, IsSnapToTickEnabled = true, TickPlacement = TickPlacement.BottomRight };
                Layout(slider, new Size(280, 28));
                var track = (Track)slider.Template.FindName("PART_Track", slider);
                Assert.IsNotNull(track);
                Assert.AreEqual(6d, track.Maximum);
                Assert.AreEqual(Slider.DecreaseLarge, track.DecreaseRepeatButton.Command);
                Assert.AreEqual(Slider.IncreaseLarge, track.IncreaseRepeatButton.Command);
                Assert.IsNotNull(slider.FocusVisualStyle);
                Assert.AreEqual(Visibility.Visible, ((TickBar)slider.Template.FindName("Ticks", slider)).Visibility);
                Slider.IncreaseSmall.Execute(null, slider);
                Assert.AreEqual(3d, slider.Value);
                Slider.DecreaseSmall.Execute(null, slider);
                Assert.AreEqual(2d, slider.Value);
                var thumbSize = track.Thumb.RenderSize;
                slider.IsEnabled = false;
                Layout(slider, new Size(280, 28));
                Assert.AreEqual(thumbSize, track.Thumb.RenderSize);
            }
        });
    }

    private static T Find<T>(FrameworkElement view, string name) where T : FrameworkElement => (T)view.FindName(name);
    private static void Layout(FrameworkElement view, Size size) { view.Measure(size); view.Arrange(new Rect(size)); view.UpdateLayout(); }
    private static void LoadTheme(bool dark)
    {
        Application.Current.Resources.MergedDictionaries.Clear();
        foreach (string name in new[] { dark ? "DarkTheme" : "DefaultTheme", "Foundation", "BridgeShellStyles" })
            WpfTestHost.LoadDictionary($"/PureDS4;component/DS4Forms/Themes/{name}.xaml");
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
    private static void AssertHorizontalBounds(FrameworkElement root, FrameworkElement control)
    {
        Rect bounds = control.TransformToAncestor(root).TransformBounds(new Rect(control.RenderSize));
        Assert.IsTrue(bounds.Left >= -1 && bounds.Right <= root.ActualWidth + 1, $"{control.Name}/{control.GetType().Name}: {bounds} outside {root.ActualWidth}");
    }
    private static void SavePreview(FrameworkElement view, bool dark, string suffix = "")
    {
        string directory = Environment.GetEnvironmentVariable("PUREDS4_VISUAL_PREVIEW_DIRECTORY");
        if (string.IsNullOrEmpty(directory)) return;
        Assert.IsTrue(Directory.Exists(directory));
        var bitmap = new RenderTargetBitmap((int)view.ActualWidth, (int)view.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        var drawing = new DrawingVisual();
        using (var context = drawing.RenderOpen())
        {
            var bounds = new Rect(view.RenderSize);
            context.DrawRectangle((Brush)Application.Current.FindResource("SurfaceBaseBrush"), null, bounds);
        }
        bitmap.Render(drawing);
        // Render the arranged UserControl directly. A VisualBrush can schedule
        // extra layout for a detached view and perturb later scroll assertions.
        bitmap.Render(view);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(directory, $"pureds4-details-wpf-{(dark ? "dark" : "light")}{suffix}.png"));
        encoder.Save(stream);
    }

    public sealed class DetailsFixture : INotifyPropertyChanged
    {
        public DeviceFixture SelectedController { get; private set; }
        public bool HasSelectedController => SelectedController != null;
        public bool Degraded { get; private set; }
        public int GestureCommitCount { get; private set; }
        public string CurrentProfileName => SelectedController?.Wireless == true ? "Bluetooth profile with a deliberately long descriptive name" : "USB profile";
        public string SelectedControllerConnection => SelectedController?.Wireless == true ? "Bluetooth" : "USB";
        public string SelectedControllerBattery => "Charging unavailable";
        public string SelectedControllerLatency => "4 ms · 250 Hz";
        public string SelectedRuntimeOutputControllerName => Degraded ? "Unavailable" : "Xbox 360";
        public string SelectedControllerStartupTitle => Degraded ? "Needs attention" : "Ready";
        public string SelectedControllerStartupDetail => Degraded ? "Game output is unavailable. Open game output settings to inspect the missing prerequisites and recovery options before starting a game." : "Physical input, virtual pad, and enabled media lanes are stable.";
        public ControllerStartupStage SelectedControllerStartupStage => Degraded ? ControllerStartupStage.Attention : ControllerStartupStage.Ready;
        public bool SelectedControllerSupportsAudio => !Degraded;
        public bool SelectedControllerIsWireless => SelectedController?.Wireless == true;
        public object[] OutputControllerChoices { get; } = { new { Name = "Xbox 360", Type = OutContType.X360 }, new { Name = "DualShock 4", Type = OutContType.DS4 } };
        public OutContType SelectedOutputController { get => OutContType.DS4; set => GestureCommitCount++; }
        public object[] ControllerAudioSourceChoices { get; } = { new { Name = "Default audio source with a long endpoint name", EndpointId = "fixture" } };
        public string ControllerAudioSourceId { get => "fixture"; set => GestureCommitCount++; }
        public int HapticStrengthPercent { get; set; } = 75;
        public int SpeakerVolumePercent { get; set; } = 50;
        public int HeadphoneVolumePercent { get; set; } = 100;
        public int SpeakerBassBoostDb { get; set; } = 2;
        public int MicrophoneVolumePercent { get; set; } = 80;
        public int SpeakerCompressionIndex { get; set; }
        public int MicrophoneNoiseSuppressionIndex { get; set; }
        public bool SpeakerOutputEnabled { get; set; } = true;
        public bool HeadsetOnlyAudio { get; set; }
        public bool MicrophoneInputEnabled { get; set; }
        public bool CanChangeMicrophoneInput => MicrophoneInputEnabled;
        public bool MicrophoneLevelControlsEnabled => false;
        public bool ShowMicrophoneAvailabilityMessage => true;
        public string MicrophoneAvailabilityText => "The active game output does not support microphone input. The saved option can still be turned off.";
        public bool NvidiaNoiseSuppressionAvailable => false;
        public string NvidiaNoiseSuppressionAvailability => "Unavailable";
        public event PropertyChangedEventHandler PropertyChanged;
        public void Select(DeviceFixture device, bool degraded) { SelectedController = device; Degraded = degraded; Notify(); }
        public void Notify() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }
    public sealed class DeviceFixture
    {
        public DeviceFixture(bool wireless) { Wireless = wireless; }
        public bool Wireless { get; }
        public string IdText => Wireless ? "DualShock 4 · 2 (Bluetooth fixture)" : "DualShock 4 · 1 (USB fixture)";
        public string FeedbackControlLabel => "Rumble strength";
        public string ControllerAudioHeader => "Controller audio";
        public string ControllerAudioDescription => "Stream audio to the controller speaker or connected headset.";
        public string MicrophoneToggleLabel => "Enable headset microphone";
        public string MicrophoneDescription => "Use a compatible headset microphone connected to the controller.";
    }
}
