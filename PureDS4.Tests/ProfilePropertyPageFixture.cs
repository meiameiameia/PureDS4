using System.Dynamic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Xml.Linq;

namespace DS4WindowsTests;

/// <summary>
/// A disposable data context for the profile editor's property pages. It is
/// derived from the markup itself: every top-level binding the pages declare
/// gets a harmless value of the type its target expects, so the real numeric
/// boxes, switches, combo boxes and mode tabs render populated without a profile,
/// a controller, <c>Global</c> or any live state.
/// </summary>
internal static class ProfilePropertyPageFixture
{
    private static readonly Regex Path = new(@"^\{Binding\s+(?:Path=)?([A-Za-z_]\w*)", RegexOptions.Compiled);

    /// <summary>Paths that mean "this part of the UI exists", not "this option is on".</summary>
    private static bool AlwaysTrue(string path) =>
        Regex.IsMatch(path, @"^(Show|Can|Display)|Available$|Visible$") && !path.Contains("Legacy");

    internal static ExpandoObject Create(bool optionsOn, IDictionary<string, object> overrides = null)
    {
        var fixture = new ExpandoObject();
        IDictionary<string, object> values = fixture;
        foreach (XElement element in PresentationHarness.Source("ProfileEditor").Descendants())
        foreach (XAttribute attribute in element.Attributes())
        {
            Match m = Path.Match(attribute.Value);
            if (!m.Success || Regex.IsMatch(attribute.Value, "ElementName|Source=|RelativeSource")) continue;
            string path = m.Groups[1].Value;
            if (values.ContainsKey(path)) continue;
            string tag = element.Name.LocalName;
            string name = attribute.Name.LocalName;
            bool converted = attribute.Value.Contains("Converter");
            object value = name switch
            {
                "IsChecked" or "IsEnabled" or "IsExpanded" => AlwaysTrue(path) || optionsOn,
                "Visibility" when converted => AlwaysTrue(path) || optionsOn,
                "Visibility" => Visibility.Visible,
                "SelectedIndex" => 0,
                "ItemsSource" => Choices(),
                "Value" => Number(tag, element),
                _ => null,
            };
            if (value != null) values[path] = value;
        }
        // Text the real view model computes; without it the previews would show blank captions.
        values["ControllerAudioHeader"] = "Controller audio";
        values["ControllerAudioDescription"] = "Streams audio to the controller speaker and headset jack, and sets how it is processed.";
        values["ControllerMicrophoneToggleLabel"] = "Controller microphone";
        values["ControllerMicrophoneDescription"] = "Use the controller's headset microphone as an input.";
        values["ControllerMicrophoneAvailabilityText"] = "Direct controller microphone input requires a Bluetooth DualShock 4.";
        values["LaunchProgramName"] = "game.exe";
        values["OutputMouseSpeed"] = 250;
        values["MouseOffsetSpeed"] = 0.5;
        foreach (var pair in overrides ?? new Dictionary<string, object>()) values[pair.Key] = pair.Value;
        return fixture;
    }

    private static object[] Choices() => new object[]
    {
        new { Name = "Speakers (sample audio device)", DisplayName = "Standard", ChoiceValue = 0, EndpointId = "sample" },
        new { Name = "Headphones (another sample device)", DisplayName = "Alternative", ChoiceValue = 1, EndpointId = "sample2" },
    };

    private static object Number(string tag, XElement element)
    {
        double min = Attr(element, "Minimum") ?? 0, max = Attr(element, "Maximum") ?? 100;
        double typical = max <= 1 ? (min > 0 ? min : 0.25)
            : min > 0 && min <= 1 ? 1
            : Math.Max(min, Math.Min(max, 10));
        return tag switch
        {
            "DoubleUpDown" or "Slider" => typical,
            "IntegerUpDown" => (int)typical,
            "SByteUpDown" => (sbyte)Math.Max(-127, Math.Min(127, typical)),
            _ => null,
        };
    }

    private static double? Attr(XElement element, string name) =>
        double.TryParse((string)element.Attribute(name), System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out double v) ? v : null;

    public sealed class ButtonModeFixture
    {
        public object[] TouchButtonModes { get; } = Choices();
        public int CurrentMode { get; set; }
    }

    /// <summary>
    /// The editor's code-behind drops the touchpad button-mode options into one of
    /// four host ContentControls. Do the same with the real control, detached.
    /// </summary>
    internal static void PlaceTouchButtonOptions(UserControl view, int touchpadOutputIndex)
    {
        string host = touchpadOutputIndex switch
        {
            0 => "touchContentControl1", 1 => "touchContentControl2",
            2 => "touchContentControl4", 3 => "touchContentControl3", _ => null,
        };
        if (host == null) return;
        UserControl options = PresentationHarness.LoadControl("TouchButtonUserControl");
        options.DataContext = new ButtonModeFixture();
        ((ContentControl)view.FindName(host)).Content = options;
    }

    /// <summary>The scroll viewer that carries a property page, by the section it belongs to.</summary>
    internal static readonly string[] ScrollNames =
    {
        "controllerMapScroll", "SpecialActionRowsScroll", null, "axisPropertiesScroll",
        "lightbarScroll", "touchpadPropertiesScroll", "gyroPropertiesScroll", "advancedPropertiesScroll",
    };

    /// <summary>Show one of the editor's eight sections without the production selection handlers.</summary>
    internal static ScrollViewer ShowSection(UserControl view, int section)
    {
        var nav = (ListBox)view.FindName("sectionNavigationList");
        var sidebar = (TabControl)view.FindName("sidebarTabControl");
        var settings = (TabControl)view.FindName("profileSettingsTabCon");
        foreach (string name in new[] { "picBoxHover", "picBoxHover2" })
            ((UIElement)view.FindName(name)).Visibility = Visibility.Hidden;
        foreach (string name in new[] { "muteConBtn", "captureConBtn", "fnlConBtn", "fnrConBtn", "blpConBtn", "brpConBtn" })
            ((UIElement)view.FindName(name)).Visibility = Visibility.Collapsed;
        ((System.Windows.Controls.Image)view.FindName("controllerDiagram")).Source = new System.Windows.Media.Imaging.BitmapImage(
            new Uri("pack://application:,,,/PureDS4;component/Resources/DualShock 4 Controller.png"));
        foreach (Button target in ((Canvas)view.FindName("conCanvas")).Children.OfType<Button>()) target.Content = null;
        var profileName = (TextBox)view.FindName("profileNameTxt");
        profileName.Text = "Default";
        profileName.IsEnabled = false;
        nav.SelectedIndex = section;
        sidebar.Visibility = section < 3 ? Visibility.Visible : Visibility.Collapsed;
        settings.Visibility = section >= 3 ? Visibility.Visible : Visibility.Collapsed;
        if (section < 3) sidebar.SelectedIndex = section; else settings.SelectedIndex = section - 3;
        view.UpdateLayout();
        string scrollName = ScrollNames[section];
        return scrollName == null ? null : view.FindName(scrollName) as ScrollViewer;
    }
}
