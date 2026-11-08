using DS4WinWPF.DS4Forms;
using DS4WinWPF.DS4Forms.ViewModels;
using DS4Windows;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.Linq;
using Xceed.Wpf.Toolkit;

namespace DS4WindowsTests;

// Detached real markup and disposable fields only. No key capture, controller
// polling, output, program launch or personal-profile persistence.
[TestClass]
[DoNotParallelize]
public class BindingMacroActionPresentationTests
{
    [TestMethod]
    [DataRow(BindingWindow.ExposeMode.Full, OutBinding.OutType.Default, X360Controls.A, 2)]
    [DataRow(BindingWindow.ExposeMode.Full, OutBinding.OutType.Key, X360Controls.A, 0)]
    [DataRow(BindingWindow.ExposeMode.Full, OutBinding.OutType.Macro, X360Controls.A, 0)]
    [DataRow(BindingWindow.ExposeMode.Full, OutBinding.OutType.Button, X360Controls.MouseUp, 0)]
    [DataRow(BindingWindow.ExposeMode.Full, OutBinding.OutType.Button, X360Controls.AbsMouseUp, 1)]
    [DataRow(BindingWindow.ExposeMode.Full, OutBinding.OutType.Button, X360Controls.AbsMouseRight, 1)]
    [DataRow(BindingWindow.ExposeMode.Full, OutBinding.OutType.Button, X360Controls.Unbound, 2)]
    [DataRow(BindingWindow.ExposeMode.Keyboard, OutBinding.OutType.Default, X360Controls.A, 0)]
    public void OutputPagesReflectTheExistingBindingWithoutWritingIt(
        BindingWindow.ExposeMode mode, OutBinding.OutType type, X360Controls output, int expected)
    {
        Assert.AreEqual(expected, BindingWindow.OutputPageIndex(mode, type, output));
    }

    [TestMethod]
    public void BindingPagesAndEmbeddedMacroKeepTheirActionsReachable()
    {
        WpfTestHost.Run(() =>
        {
            foreach (bool dark in new[] { false, true })
            foreach (Size size in new[] { new Size(624, 352), new Size(1024, 632) })
            {
                ProfileWorkspacePresentationTests.Theme(dark);
                UserControl view = CreateBinding("binding-controller");
                var host = new Window { Content = view };
                try
                {
                    var tabs = (TabControl)view.FindName("mapOptionsPanel");
                    foreach (int index in new[] { 0, 1, 2, 3 })
                    {
                        tabs.SelectedIndex = index; PresentationHarness.Layout(view, size);
                        foreach (string name in new[] { "recordMacroBtn", "regBindRadio", "shiftBindRadio", "defaultBtn", "unboundBtn" })
                            FullyInside(view, (FrameworkElement)view.FindName(name));
                        Rect footer = Bounds(view, (Button)view.FindName("defaultBtn"));
                        foreach (ScrollViewer scroll in new[] { "bindingKeyboardScroll", "bindingExtrasScroll" }
                            .Select(name => (ScrollViewer)view.FindName(name)))
                        {
                            if (!PresentationHarness.Participates(scroll) || scroll.ActualWidth == 0) continue;
                            Assert.AreEqual(0d, scroll.ScrollableWidth, 1, "No output page may require horizontal scrolling.");
                            AssertViewport(scroll);
                            scroll.ScrollToEnd(); PresentationHarness.Layout(view, size);
                            Assert.AreEqual(footer, Bounds(view, (Button)view.FindName("defaultBtn")));
                        }
                        if (index == 0)
                        {
                            var scroll = (ScrollViewer)view.FindName("bindingKeyboardScroll");
                            var keys = (Grid)view.FindName("otherKeysMouseGrid");
                            Assert.IsTrue(Bounds(scroll, keys).Bottom <= scroll.ActualHeight + 1,
                                "Compact keyboards must wrap and reveal the navigation, numpad and mouse keys at the end.");
                        }
                        if (index == 2)
                        {
                            var canvas = (Canvas)view.FindName("outputControllerCanvas");
                            FullyInside(view, canvas);
                            Assert.IsTrue(canvas.ActualHeight > 100);
                        }
                    }
                    var shift = (ComboBox)view.FindName("shiftTriggerCombo");
                    shift.Visibility = Visibility.Visible; shift.SelectedIndex = 1;
                    shift.GetBindingExpression(Selector.SelectedIndexProperty).UpdateSource();
                    Assert.AreEqual(1, ((Fields)view.DataContext).ShiftTrigger);
                    PresentationHarness.Layout(view, size); FullyInside(view, shift);
                    // Mirror the existing embedded-editor add/remove route, without
                    // constructing RecordBox or calling its live event handlers.
                    var map = (DockPanel)view.FindName("mapBindingPanel");
                    var extras = (StackPanel)view.FindName("extrasSidePanel");
                    var panel = (DockPanel)view.FindName("fullPanel");
                    UserControl macro = CreateMacro(false);
                    map.Visibility = extras.Visibility = Visibility.Collapsed;
                    panel.Children.Add(macro); PresentationHarness.Layout(view, size);
                    foreach (string name in new[] { "recordBtn", "saveBtn", "cancelBtn" })
                        FullyInside(view, (FrameworkElement)macro.FindName(name));
                    panel.Children.Remove(macro);
                    map.Visibility = extras.Visibility = Visibility.Visible;
                    tabs.SelectedIndex = 2; PresentationHarness.Layout(view, size);
                    FullyInside(view, (Button)view.FindName("defaultBtn"));
                    // Keyboard-only callers retain the same stripped-down exposure.
                    ((UIElement)view.FindName("bottomPanel")).Visibility = Visibility.Collapsed;
                    extras.Visibility = Visibility.Collapsed; tabs.SelectedIndex = 0;
                    PresentationHarness.Layout(view, size);
                    Assert.AreEqual(Visibility.Collapsed, ((TabItem)view.FindName("controllerOutputTab")).Visibility);
                    Assert.AreEqual(Visibility.Collapsed, ((TabItem)view.FindName("bindingExtrasTab")).Visibility);
                }
                finally { host.Content = null; host.Close(); }
            }
        });
    }

    [TestMethod]
    public void MacroStatesPreserveExplicitStepEditsAndFixedRecordSaveCancel()
    {
        WpfTestHost.Run(() =>
        {
            foreach (bool dark in new[] { false, true })
            foreach (Size size in new[] { new Size(604, 352), new Size(824, 512) })
            {
                ProfileWorkspacePresentationTests.Theme(dark);
                UserControl view = CreateMacro(false);
                var host = new Window { Content = view };
                try
                {
                    var list = (ListBox)view.FindName("macroListBox");
                    var options = (ScrollViewer)view.FindName("macroOptionsScroll");
                    var fields = (Fields)view.DataContext;
                    foreach (bool recording in new[] { false, true, false })
                    {
                        ConfigureRecording(view, recording);
                        PresentationHarness.Layout(view, size);
                        foreach (string name in new[] { "recordBtn", "saveBtn", "cancelBtn", "clearStepsBtn" })
                            FullyInside(view, (FrameworkElement)view.FindName(name));
                        Assert.AreEqual(!recording, ((Button)view.FindName("saveBtn")).IsEnabled);
                        Assert.AreEqual(!recording, ((Button)view.FindName("cancelBtn")).IsEnabled);
                        Assert.AreEqual(!recording, list.IsEnabled);
                        Assert.IsTrue(((Button)view.FindName("recordBtn")).IsEnabled, "Stop must remain reachable while recording.");
                        Rect record = Bounds(view, (Button)view.FindName("recordBtn"));
                        Rect save = Bounds(view, (Button)view.FindName("saveBtn"));
                        AssertViewport(options); options.ScrollToEnd(); PresentationHarness.Layout(view, size);
                        Assert.AreEqual(record, Bounds(view, (Button)view.FindName("recordBtn")));
                        Assert.AreEqual(save, Bounds(view, (Button)view.FindName("saveBtn")));
                        if (recording) FullyInside(options, (Button)view.FindName("changeLightBtn"));
                        options.ScrollToTop();
                    }
                    list.SelectedIndex = list.Items.Count - 1; list.ScrollIntoView(list.SelectedItem);
                    view.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
                    PresentationHarness.Layout(view, size);
                    var last = (ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(list.Items.Count - 1);
                    FullyInside(list, last);
                    last.ContentTemplate = (DataTemplate)view.FindResource("EditTemplate");
                    PresentationHarness.Layout(view, size);
                    var wait = PresentationHarness.Descendants<IntegerUpDown>(last).Single();
                    Assert.AreEqual(UpdateSourceTrigger.Explicit, wait.GetBindingExpression(IntegerUpDown.ValueProperty).ParentBinding.UpdateSourceTrigger);
                    wait.SetCurrentValue(IntegerUpDown.ValueProperty, 750);
                    Assert.AreEqual(250, ((StepFields)list.SelectedItem).DisplayValue);
                    wait.GetBindingExpression(IntegerUpDown.ValueProperty).UpdateSource();
                    Assert.AreEqual(750, ((StepFields)list.SelectedItem).DisplayValue);
                    last.ContentTemplate = (DataTemplate)view.FindResource("EditRumbleTemplate");
                    PresentationHarness.Layout(view, size);
                    var rumble = PresentationHarness.Descendants<IntegerUpDown>(last).ToArray();
                    Assert.AreEqual(2, rumble.Length);
                    foreach (var value in rumble) { FullyInside(list, value); Assert.AreEqual(255, value.Maximum); }
                    var mode = (ComboBox)view.FindName("macroModeCombo");
                    mode.SelectedIndex = 1; mode.GetBindingExpression(Selector.SelectedIndexProperty).UpdateSource();
                    Assert.AreEqual(1, fields.MacroModeIndex);
                    var delays = (CheckBox)view.FindName("recordDelaysCk");
                    delays.SetCurrentValue(ToggleButton.IsCheckedProperty, false);
                    delays.GetBindingExpression(ToggleButton.IsCheckedProperty).UpdateSource();
                    Assert.IsFalse(fields.RecordDelays);
                    Assert.AreEqual(delays.Content, System.Windows.Automation.AutomationProperties.GetName(delays),
                        "The caption hidden by the switch template must remain its accessible name.");
                    var preset = (Button)view.FindName("loadPresetBtn");
                    Assert.AreEqual(2, preset.ContextMenu.Items.Count);
                }
                finally { host.Content = null; host.Close(); }
            }
        });
    }

    [TestMethod]
    public void SpecialActionTypesAndUnloadTriggersFitInsideTheRealEditor()
    {
        WpfTestHost.Run(() =>
        {
            foreach (bool dark in new[] { false, true })
            foreach (Size size in new[] { new Size(704, 268), new Size(1008, 548) })
            {
                ProfileWorkspacePresentationTests.Theme(dark);
                UserControl editor = ProfileWorkspacePresentationTests.Load("ProfileEditor");
                var host = new Window { Content = editor };
                try
                {
                    UserControl view = ConfigureActionShell(editor, 1);
                    var options = (ScrollViewer)view.FindName("actionPropertiesScroll");
                    var types = (ComboBox)view.FindName("actionTypeCombo");
                    var tabs = (TabControl)view.FindName("actionTypeTabControl");
                    PresentationHarness.Layout(editor, size);
                    var sectionHeader = (Border)editor.FindName("profileSectionHeader");
                    Assert.AreEqual(Visibility.Collapsed, sectionHeader.Visibility,
                        "The inline form must not lose compact space to a duplicate section heading.");
                    foreach (int index in Enumerable.Range(0, 10))
                    {
                        types.SetCurrentValue(Selector.SelectedIndexProperty, index);
                        types.GetBindingExpression(Selector.SelectedIndexProperty).UpdateSource();
                        PresentationHarness.Layout(editor, size);
                        Assert.AreEqual(index, tabs.SelectedIndex);
                        foreach (string name in new[] { "saveBtn", "cancelBtn" })
                            FullyInside(editor, (FrameworkElement)view.FindName(name));
                        foreach (string name in new[] { "actionNameTxt", "actionTypeCombo" })
                            PresentationHarness.AssertReachable(editor, (FrameworkElement)view.FindName(name), "Action editor");
                        var body = (ScrollViewer)view.FindName("actionEditorScroll");
                        AssertViewport(body);
                        AssertViewport(options);
                        Rect save = Bounds(editor, (Button)view.FindName("saveBtn"));
                        options.ScrollToEnd(); PresentationHarness.Layout(editor, size);
                        Assert.AreEqual(save, Bounds(editor, (Button)view.FindName("saveBtn")));
                        body.ScrollToEnd(); PresentationHarness.Layout(editor, size);
                        Assert.AreEqual(save, Bounds(editor, (Button)view.FindName("saveBtn")));
                        body.ScrollToTop();
                        options.ScrollToTop();
                        Assert.IsTrue(((ScrollViewer)PresentationHarness.Descendants<ScrollViewer>((ListView)view.FindName("triggersListView")).First()).ScrollableHeight > 0);
                    }
                    var triggers = (ListView)view.FindName("triggersListView");
                    var unload = (ListView)view.FindName("unloadTriggersListView");
                    foreach (bool unloading in new[] { true, false })
                    {
                        triggers.Visibility = unloading ? Visibility.Collapsed : Visibility.Visible;
                        unload.Visibility = unloading ? Visibility.Visible : Visibility.Collapsed;
                        editor.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
                        PresentationHarness.Layout(editor, size);
                        ListView visible = unloading ? unload : triggers;
                        visible.SelectedIndex = visible.Items.Count - 1;
                        visible.ScrollIntoView(visible.SelectedItem);
                        editor.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
                        PresentationHarness.Layout(editor, size);
                        Assert.IsTrue(visible.ActualHeight > 40,
                            "The selected trigger list needs a usable viewport: " + visible.RenderSize +
                            "; pane=" + ((Grid)view.FindName("actionTriggerPane")).RenderSize + "; editor=" + view.RenderSize);
                        PresentationHarness.AssertReachable(editor, visible, "Trigger list");
                        FullyInside(visible, (ListViewItem)visible.SelectedItem);
                        Assert.IsTrue(PresentationHarness.Descendants<CheckBox>(visible)
                            .Where(c => c.Tag?.ToString() is "Mute" or "Capture" or "SideL" or "SideR" or "Function Left" or "Function Right")
                            .All(c => !PresentationHarness.Participates(c)));
                    }
                    var actionName = (TextBox)view.FindName("actionNameTxt");
                    Assert.IsTrue(actionName.GetBindingExpression(TextBox.TextProperty).ParentBinding.ValidatesOnNotifyDataErrors);
                    ((TabControl)editor.FindName("sidebarTabControl")).SelectedIndex = 0;
                    PresentationHarness.Layout(editor, size);
                    Assert.AreEqual(Visibility.Visible, sectionHeader.Visibility);
                    ((TabControl)editor.FindName("sidebarTabControl")).SelectedIndex = 1;
                    ((UIElement)editor.FindName("baseSpeActPanel")).Visibility = Visibility.Visible;
                    ((DockPanel)editor.FindName("specialActionDockPanel")).Children.Remove(view);
                    PresentationHarness.Layout(editor, size);
                    Assert.AreEqual(Visibility.Visible, sectionHeader.Visibility,
                        "Returning to the action list must restore its section heading.");
                }
                finally { host.Content = null; host.Close(); }
            }
        });
    }

    [TestMethod]
    public void SwipeRotationAndLightbarSequenceKeepLongListsAndBoundEditsReachable()
    {
        WpfTestHost.Run(() =>
        {
            foreach (bool dark in new[] { false, true })
            foreach (bool compact in new[] { true, false })
            foreach (string page in new[] { "swipe", "lightbar-sequence" })
            {
                PresentationHarness.Theme(dark);
                UserControl view = CreateProfileHelper(page);
                Size size = page == "swipe" ? new Size(compact ? 404 : 504, compact ? 388 : 528) :
                    new Size(compact ? 604 : 744, compact ? 388 : 488);
                PresentationHarness.Layout(view, size);
                var list = (ListBox)view.FindName(page == "swipe" ? "profilesListBox" : "MacroListBox");
                foreach (string action in page == "swipe" ? new[] { "selectAllBtn", "clearAllBtn", "cancelBtn", "okBtn" } :
                    new[] { "addColorBtn", "deleteColorBtn", "clearColorsBtn", "saveBtn" })
                    FullyInside(view, (Button)view.FindName(action));
                var scroll = PresentationHarness.Descendants<ScrollViewer>(list).First();
                Assert.AreEqual(ScrollBarVisibility.Disabled, scroll.HorizontalScrollBarVisibility);
                Assert.IsTrue(scroll.CanContentScroll && VirtualizingPanel.GetIsVirtualizing(list));
                Assert.AreEqual(ScrollUnit.Pixel, VirtualizingPanel.GetScrollUnit(list));
                Assert.AreEqual(0d, scroll.ScrollableWidth, 1);
                list.SelectedIndex = list.Items.Count - 1;
                list.ScrollIntoView(list.SelectedItem);
                view.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
                PresentationHarness.Layout(view, size);
                var last = (ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(list.Items.Count - 1);
                FullyInside(list, last);
                if (page == "swipe")
                {
                    var check = PresentationHarness.Descendants<CheckBox>(last).Single();
                    check.SetCurrentValue(ToggleButton.IsCheckedProperty, true);
                    check.GetBindingExpression(ToggleButton.IsCheckedProperty).UpdateSource();
                    Assert.IsTrue(((SwipeProfileItem)list.SelectedItem).IsAllowed);
                    Assert.AreEqual(((SwipeProfileItem)list.SelectedItem).Name,
                        System.Windows.Automation.AutomationProperties.GetName(check));
                    Assert.AreEqual(TextWrapping.Wrap, ((TextBlock)check.Content).TextWrapping);
                    Assert.AreEqual(FontWeights.Normal, ((TextBlock)check.Content).FontWeight);
                }
                else
                {
                    var fields = (SequenceFields)view.DataContext;
                    Assert.AreEqual(FontWeights.Normal, PresentationHarness.Descendants<TextBlock>(last).First().FontWeight);
                    var interval = (UIntegerUpDown)view.FindName("intervalUpDown");
                    interval.SetCurrentValue(UIntegerUpDown.ValueProperty, (uint?)750);
                    interval.GetBindingExpression(UIntegerUpDown.ValueProperty).UpdateSource();
                    Assert.AreEqual(750u, fields.CurrentInterval);
                    var trigger = (ComboBox)view.FindName("TriggerCombo");
                    trigger.SelectedItem = LightbarMacroTrigger.Release;
                    trigger.GetBindingExpression(Selector.SelectedItemProperty).UpdateSource();
                    Assert.AreEqual(LightbarMacroTrigger.Release, fields.LightbarMacro.Trigger);
                    var cancel = (CheckBox)view.FindName("cancelCurrentCk");
                    cancel.SetCurrentValue(ToggleButton.IsCheckedProperty, true);
                    cancel.GetBindingExpression(ToggleButton.IsCheckedProperty).UpdateSource();
                    Assert.IsTrue(fields.LightbarMacro.CancelCurrent);
                    var options = (ScrollViewer)view.FindName("lightbarMacroOptionsScroll");
                    AssertViewport(options);
                    Rect save = Bounds(view, (Button)view.FindName("saveBtn"));
                    Rect add = Bounds(view, (Button)view.FindName("addColorBtn"));
                    options.ScrollToEnd(); PresentationHarness.Layout(view, size);
                    Assert.AreEqual(save, Bounds(view, (Button)view.FindName("saveBtn")));
                    Assert.AreEqual(add, Bounds(view, (Button)view.FindName("addColorBtn")));
                }
                // Empty collections must keep the same actions and layout.
                if (page == "swipe") ((SwipeProfilesEditorViewModel)view.DataContext).Items.Clear();
                else ((SequenceFields)view.DataContext).LightbarMacro.Macro.Clear();
                PresentationHarness.Layout(view, size);
                Assert.AreEqual(0, list.Items.Count);
                FullyInside(view, (Button)view.FindName(page == "swipe" ? "okBtn" : "saveBtn"));
            }
        });
    }

    internal static UserControl CreateProfileHelper(string page)
    {
        UserControl view = PresentationHarness.LoadControl(page == "swipe" ? "SwipeProfilesEditor" : "LightbarMacroCreator");
        if (page == "swipe")
        {
            var fields = new SwipeProfilesEditorViewModel();
            for (int i = 1; i <= 48; i++) fields.Items.Add(new SwipeProfileItem
            {
                Name = i % 3 == 0 ? $"Profile {i} — long game name with a custom touchpad layout for strategy games" : $"Profile {i}",
                IsAllowed = i % 2 == 0,
            });
            view.DataContext = fields;
        }
        else
        {
            var fields = new SequenceFields();
            for (int i = 1; i <= 48; i++)
                fields.LightbarMacro.Macro.Add(new LightbarMacroElement(new DS4Color(80, 160, 220), (uint)(i * 100)));
            view.DataContext = fields;
            ((ComboBox)view.FindName("TriggerCombo")).ItemsSource = Enum.GetValues<LightbarMacroTrigger>();
        }
        return view;
    }

    public class SequenceFields
    {
        public LightbarMacro LightbarMacro { get; } = new();
        public uint CurrentInterval { get; set; } = 250;
        public string CurrentColorString => "#FF50A0DC";
    }

    internal static UserControl CreateBinding(string page)
    {
        var view = ProfileWorkspacePresentationTests.Load("BindingWindow");
        view.DataContext = new Fields();
        ((ComboBox)view.FindName("shiftTriggerCombo")).Visibility = Visibility.Hidden;
        ((TabControl)view.FindName("mapOptionsPanel")).SelectedIndex = page == "binding-keyboard" ? 0 : page == "binding-extras" ? 3 : 2;
        ((UIElement)view.FindName("highlightLb")).Visibility = Visibility.Collapsed;
        return view;
    }

    internal static UserControl CreateMacro(bool recording)
    {
        var view = ProfileWorkspacePresentationTests.Load("RecordBox");
        view.DataContext = new Fields();
        ConfigureRecording(view, recording); return view;
    }

    internal static void ConfigureRecording(UserControl view, bool recording)
    {
        ((Button)view.FindName("recordBtn")).Content = recording ? "Stop" : "Record";
        foreach (string name in new[] { "macroListBox", "recordDelaysCk", "saveBtn", "cancelBtn", "loadPresetBtn", "savePresetBtn", "macroModeCombo", "clearStepsBtn", "addWaitTimeBtn" })
            ((UIElement)view.FindName(name)).IsEnabled = !recording;
        foreach (string name in new[] { "mouseButtonsPanel", "extraConPanel" })
            ((UIElement)view.FindName(name)).Visibility = recording ? Visibility.Visible : Visibility.Hidden;
    }

    internal static UserControl ConfigureActionShell(UserControl editor, int index)
    {
        editor.DataContext = new ProfileWorkspacePresentationTests.EditorFixture();
        ((ListBox)editor.FindName("sectionNavigationList")).SelectedIndex = 1;
        ((TabControl)editor.FindName("sidebarTabControl")).SelectedIndex = 1;
        ((UIElement)editor.FindName("profileSettingsTabCon")).Visibility = Visibility.Collapsed;
        ((UIElement)editor.FindName("baseSpeActPanel")).Visibility = Visibility.Collapsed;
        var view = ProfileWorkspacePresentationTests.Load("SpecialActionEditor");
        foreach (TabItem tab in ((TabControl)view.FindName("actionTypeTabControl")).Items)
            tab.Visibility = Visibility.Collapsed; // Same header hiding as the real constructor.
        view.DataContext = new Fields { ActionTypeIndex = index };
        ((DockPanel)editor.FindName("specialActionDockPanel")).Children.Add(view);
        return view;
    }

    private static Rect Bounds(FrameworkElement root, FrameworkElement child) =>
        child.TransformToAncestor(root).TransformBounds(new Rect(child.RenderSize));
    private static void FullyInside(FrameworkElement root, FrameworkElement child)
    {
        Rect rect = Bounds(root, child);
        Assert.IsTrue(rect.Left >= -1 && rect.Top >= -1 && rect.Right <= root.ActualWidth + 1 && rect.Bottom <= root.ActualHeight + 1,
            child.Name + ": " + rect + " outside " + root.RenderSize);
    }
    private static void AssertViewport(ScrollViewer scroll)
    {
        if (scroll.Content is not FrameworkElement content) return;
        foreach (Control control in PresentationHarness.Descendants<Control>(content).Where(c => c.ActualWidth > 0 && PresentationHarness.Participates(c)))
        {
            // Nested viewports own their own scrolling. Virtualized item hosts
            // expose a viewport-sized presenter, not their complete extent;
            // list-end reveal is exercised separately above.
            bool nestedViewport = false;
            for (DependencyObject parent = VisualTreeHelper.GetParent(control); parent != null && parent != scroll; parent = VisualTreeHelper.GetParent(parent))
                if (parent is ScrollViewer) { nestedViewport = true; break; }
            if (nestedViewport) continue;
            Rect rect = Bounds(content, control);
            Assert.IsTrue(rect.Left >= -1 && rect.Right <= scroll.ViewportWidth + 1,
                control.Name + "/" + control.GetType().Name + ": " + rect + " outside viewport " + scroll.ViewportWidth);
            PresentationHarness.AssertReachable((FrameworkElement)VisualTreeHelper.GetParent(scroll), control, "Options");
        }
    }

    public class Fields : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private int actionTypeIndex;
        public int ActionTypeIndex { get => actionTypeIndex; set { actionTypeIndex = value; PropertyChanged?.Invoke(this, new(nameof(ActionTypeIndex))); } }
        public string ActionName { get; set; } = "Strategy game shortcuts with a longer action name";
        public int ShiftTrigger { get; set; }
        public string DefaultBtnString => "Default";
        public Visibility MacroLbVisible => Visibility.Hidden;
        public bool Toggle { get; set; }
        public bool HasScanCode { get; set; }
        public LightbarFields LightbarMacro { get; } = new();
        public int HeavyRumble { get; set; } = 120;
        public int LightRumble { get; set; } = 60;
        public int ExtrasColorR { get; set; } = 120;
        public int ExtrasColorG { get; set; } = 170;
        public int ExtrasColorB { get; set; } = 220;
        public bool UseExtrasColor { get; set; } = true;
        public int FlashRate { get; set; } = 4;
        public bool UseMouseSens { get; set; }
        public int MouseSens { get; set; } = 25;
        public object[] MacroSteps { get; } = Enumerable.Range(1, 40).Select(i => (object)new StepFields { Step = new { Name = i == 40 ? "Wait 250 ms" : "Key action " + i + " with a descriptive macro step name that needs wrapping" } }).ToArray();
        public int MacroStepIndex { get; set; } = -1;
        public bool RecordDelays { get; set; } = true;
        public int MacroModeIndex { get; set; }
        public bool UseScanCode { get; set; }
        public string Macrostring => "Recorded sequence with keyboard, delay and lightbar steps";
        public decimal Delay { get; set; } = 0.5m;
        public decimal HoldInterval { get; set; } = 0.5m;
        public string Filepath => @"C:\Example\Games\A program with a long descriptive name.exe";
        public string Arguments { get; set; } = "--example";
        public bool UnloadEnabled => true;
        public bool AutoUntrigger { get; set; }
        public Visibility ShowToggleControls => Visibility.Visible;
        public string DescribeText => "A selected key with a longer description";
        public int PressReleaseIndex { get; set; }
        public bool Notification { get; set; } = true;
        public bool Lightbar { get; set; } = true;
        public Color EmptyColor => Colors.Red;
        public Color FullColor => Colors.LimeGreen;
        public string TapMacroText => "Select tap macro";
        public string HoldMacroText => "Select hold macro";
        public string DoubleTapMacroText => "Select double-tap macro";
    }
    public class StepFields
    {
        public object Step { get; set; }
        public int DisplayValue { get; set; } = 250;
        public int RumbleHeavy { get; set; } = 100;
        public int RumbleLight { get; set; } = 50;
    }
    public class LightbarFields { public bool Active { get; set; } }
}
