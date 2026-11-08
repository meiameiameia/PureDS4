using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Xml.Linq;

namespace DS4WindowsTests;

/// <summary>
/// The specialist workspaces Tools opens: the runtime log, output slots, and
/// the windows for identity, legacy import and the removal plan. Layout and
/// spacing checks against the same rules Home set, not owner acceptance.
/// </summary>
[TestClass]
[DoNotParallelize]
public class ToolsDiagnosticsPresentationTests
{
    [TestMethod]
    public void LogAndOutputSlotsFitBothThemesAndViewports()
    {
        WpfTestHost.Run(() =>
        {
            foreach (bool dark in new[] { false, true })
            foreach (Size size in PresentationHarness.Viewports)
            {
                PresentationHarness.Theme(dark);

                UserControl log = CreateLogView();
                PresentationHarness.Layout(log, size);
                PresentationHarness.Preview(log, "log-" + (int)size.Width, dark);
                AssertToolsReachable(log, "log");
                var messages = (ListView)log.FindName("logListView");
                Assert.AreEqual(ScrollUnit.Pixel, VirtualizingPanel.GetScrollUnit(messages));
                Assert.IsTrue(VirtualizingPanel.GetIsVirtualizing(messages));
                Assert.IsTrue(PresentationHarness.Descendants<ScrollViewer>(messages).Single().CanContentScroll,
                    "The table template must forward logical scrolling so pixel-scrolled rows remain virtualized.");
                var warning = PresentationHarness.Descendants<TextBlock>(messages).First(t => t.Text.StartsWith("Controller could"));
                Assert.AreEqual(TextWrapping.Wrap, warning.TextWrapping);
                Assert.IsTrue(warning.ActualHeight > warning.FontSize * 1.5, "Long warnings must be readable without opening another dialog.");
                object last = messages.Items[messages.Items.Count - 1];
                messages.ScrollIntoView(last);
                PresentationHarness.Layout(log, size);
                var lastRow = (ListViewItem)messages.ItemContainerGenerator.ContainerFromItem(last);
                Assert.IsNotNull(lastRow, "Scrolling must realize the last event, not clip it behind the fixed actions.");
                Rect lastBounds = lastRow.TransformToAncestor(messages).TransformBounds(new Rect(lastRow.RenderSize));
                Assert.IsTrue(lastBounds.Top >= -1 && lastBounds.Bottom <= messages.ActualHeight + 1);
                messages.ItemsSource = Array.Empty<object>();
                PresentationHarness.Layout(log, size);
                Assert.AreEqual(Visibility.Visible, ((FrameworkElement)log.FindName("logEmptyState")).Visibility);
                AssertToolsReachable(log, "empty log");

                UserControl slots = CreateOutputSlotsView();
                var list = (ListView)slots.FindName("currentOutDevLV");
                var state = (OutputFixture)list.DataContext;
                foreach (int selection in new[] { -1, 0, 1, 2, -1 })
                {
                    list.SelectedIndex = selection;
                    PresentationHarness.Layout(slots, size);
                    Assert.AreEqual(selection, state.SelectedIndex, "Selection remains OneWayToSource.");
                    AssertToolsReachable(slots, "output slots");
                    bool attached = selection == 0 || selection == 2;
                    Assert.AreEqual(selection >= 0 && !attached, ((Button)slots.FindName("pluginBtn")).IsEnabled);
                    Assert.AreEqual(attached, ((Button)slots.FindName("unplugBtn")).IsEnabled);
                    Assert.AreEqual(attached ? Visibility.Visible : Visibility.Collapsed,
                        ((FrameworkElement)slots.FindName("reservationPanel")).Visibility);
                    if (!attached) continue;
                    var reservation = (ComboBox)slots.FindName("reserveCombo");
                    Assert.AreEqual(state.SlotDeviceEntries[selection].ReserveChoice, reservation.SelectedIndex);
                    int otherSlotChoice = state.SlotDeviceEntries[selection == 0 ? 2 : 0].ReserveChoice;
                    reservation.SelectedIndex = 1 - reservation.SelectedIndex;
                    Assert.AreEqual(reservation.SelectedIndex, state.SlotDeviceEntries[selection].ReserveChoice);
                    Assert.AreEqual(otherSlotChoice, state.SlotDeviceEntries[selection == 0 ? 2 : 0].ReserveChoice);
                    Assert.IsTrue(((Button)slots.FindName("slotChangeAcceptBtn")).IsEnabled);
                }
            }
        });
    }

    internal static void AssertToolsReachable(UserControl view, string name)
    {
        foreach (FrameworkElement element in PresentationHarness.Descendants<FrameworkElement>(view)
            .Where(e => (e is Control || e is TextBlock) && e.ActualWidth > 0 && PresentationHarness.Participates(e)))
            PresentationHarness.AssertReachable(view, element, name);
    }

    internal static UserControl CreateLogView()
    {
        UserControl view = PresentationHarness.LoadTabWorkspace("logTab");
        ((ListView)view.FindName("logListView")).ItemsSource = new ToolsFixture().LogMessages;
        return view;
    }

    [TestMethod]
    public void ReadOnlyDocumentDialogsKeepLongContentAndCloseReachableWithoutShowingWindows()
    {
        WpfTestHost.Run(() =>
        {
            foreach (bool dark in new[] { false, true })
            foreach (string name in new[] { "LogMessageDisplay", "ChangelogWindow" })
            foreach (Size size in new[] { new Size(404, 288), new Size(624, 408) })
            {
                PresentationHarness.Theme(dark);
                UserControl view = CreateDocumentView(name, true);
                if (name == "LogMessageDisplay")
                {
                    var message = (RichTextBox)view.FindName("richMessageBox");
                    Assert.IsTrue(message.IsReadOnly && message.IsDocumentEnabled);
                    Assert.AreEqual(ScrollBarVisibility.Disabled, message.HorizontalScrollBarVisibility);
                    Assert.IsFalse(string.IsNullOrEmpty(System.Windows.Automation.AutomationProperties.GetName(message)));
                }
                PresentationHarness.Layout(view, size);
                AssertToolsReachable(view, name);
                if (name == "LogMessageDisplay")
                    Assert.AreEqual(FontWeights.Normal, ((RichTextBox)view.FindName("richMessageBox")).Document.Blocks.FirstBlock.FontWeight);
                string actionName = name == "LogMessageDisplay" ? "okayButton" : "closeBtn";
                var close = (Button)view.FindName(actionName);
                Assert.IsTrue(close.IsTabStop);
                Assert.IsNotNull(close.FocusVisualStyle);
                Assert.IsFalse(close.IsDefault, "Styling must not introduce a new implicit Enter action.");
                var scroll = PresentationHarness.Descendants<ScrollViewer>(view).First(s => s.ScrollableHeight > 0);
                Assert.AreEqual(ScrollBarVisibility.Disabled, scroll.HorizontalScrollBarVisibility);
                scroll.ScrollToEnd();
                PresentationHarness.Layout(view, size);
                Assert.IsTrue(scroll.VerticalOffset > 0);
                PresentationHarness.AssertReachable(view, close, name + " after scrolling");
                PresentationHarness.Preview(view, name.ToLowerInvariant() + "-" + (int)size.Width, dark);
            }
            PresentationHarness.Theme(true);
            UserControl notes = PresentationHarness.LoadControl("ChangelogWindow");
            notes.DataContext = new { IntroText = "", Markdown = DS4Windows.UpdateAuthorityPolicy.ReleaseNotesDisabledMarkdown };
            PresentationHarness.Layout(notes, new Size(404, 288));
            Assert.AreEqual(Visibility.Collapsed, ((TextBlock)notes.FindName("captionTextBlock")).Visibility);
            AssertToolsReachable(notes, "manual release notes");
            PresentationHarness.Preview(notes, "release-notes-manual", true);
        });
    }

    internal static UserControl CreateDocumentView(string name, bool longContent)
    {
        // Detached markup only: the real release-note constructor starts loading.
        UserControl view = PresentationHarness.LoadControl(name);
        string text = string.Join("\n\n", Enumerable.Range(1, longContent ? 48 : 2).Select(i =>
            $"Diagnostic paragraph {i}. This fictitious text exercises wrapping and scrolling without reading any personal logs or connecting a controller."));
        if (name == "LogMessageDisplay")
        {
            var message = (RichTextBox)view.FindName("richMessageBox");
            message.Document = new MdXaml.Markdown().Transform(text);
            message.Document.SetResourceReference(FrameworkContentElement.StyleProperty, "FoundationDocumentStyle");
            message.Document.TextAlignment = TextAlignment.Left;
        }
        else view.DataContext = new
        {
            IntroText = longContent ? "Fictitious release-note content for a layout check." : "",
            Markdown = longContent ? text : DS4Windows.UpdateAuthorityPolicy.ReleaseNotesDisabledMarkdown,
        };
        return view;
    }

    [TestMethod]
    public void LogMessageConstructionKeepsFormattingAndLinkCommandWithoutCreatingNativeWindow()
    {
        WpfTestHost.Run(() =>
        {
            PresentationHarness.Theme(true);
            var dialog = new DS4WinWPF.DS4Forms.LogMessageDisplay(
                "**Diagnostic event**\n\nSee https://example.invalid/report/ for details.");
            try
            {
                Assert.AreEqual(IntPtr.Zero, new WindowInteropHelper(dialog).Handle);
                var message = (RichTextBox)dialog.FindName("richMessageBox");
                Assert.AreEqual(TextAlignment.Left, message.Document.TextAlignment);
                Assert.AreEqual(Application.Current.FindResource("TypeUiFontFamily"), message.Document.FontFamily);
                Assert.AreEqual(Application.Current.FindResource("TextPrimaryBrush"), message.Document.Foreground);
                StringAssert.Contains(new TextRange(message.Document.ContentStart, message.Document.ContentEnd).Text, "Diagnostic event");
                Assert.IsTrue(message.IsReadOnly && message.IsDocumentEnabled);
                Assert.IsTrue(message.CommandBindings.Cast<System.Windows.Input.CommandBinding>()
                    .Any(b => b.Command == System.Windows.Input.NavigationCommands.GoToPage));
                PresentationHarness.Theme(false);
                Assert.AreEqual(Application.Current.FindResource("TextPrimaryBrush"), message.Document.Foreground,
                    "Changing theme must also recolor an existing log document.");
                // Never execute the link command, copy text or show the dialog.
            }
            finally { dialog.Close(); }
        });
    }

    internal static UserControl CreateOutputSlotsView()
    {
        UserControl view = PresentationHarness.LoadControl("OutputSlotManagerControl");
        var list = (ListView)view.FindName("currentOutDevLV");
        var toolbar = (FrameworkElement)view.FindName("actionToolbar");
        var panel = (FrameworkElement)view.FindName("reservationPanel");
        var state = new OutputFixture();
        list.DataContext = state;
        toolbar.DataContext = state;
        panel.DataContext = null;
        // Match the production selection/context boundary, without creating
        // ControlService, OutputSlotManager or a virtual device.
        list.SelectionChanged += (_, _) => panel.DataContext = list.SelectedItem;
        list.SelectedIndex = 0;
        return view;
    }

    [TestMethod]
    public void PlugDialogFitsBothThemesAndPreservesExplicitChoices()
    {
        WpfTestHost.Run(() =>
        {
            foreach (bool dark in new[] { false, true })
            foreach (Size size in new[] { new Size(404, 318), new Size(464, 318) })
            {
                PresentationHarness.Theme(dark);
                UserControl view = PresentationHarness.LoadControl("PluginOutDevWindow");
                PresentationHarness.Layout(view, size);
                AssertToolsReachable(view, "plug output slot");
                Assert.AreEqual(0, ((ComboBox)view.FindName("devTypeCombo")).SelectedIndex);
                Assert.AreEqual(0, ((ComboBox)view.FindName("reserveTypeCombo")).SelectedIndex);
                Assert.IsTrue(((ComboBox)view.FindName("devTypeCombo")).ActualHeight >= 28);
                Assert.IsTrue(((ComboBox)view.FindName("reserveTypeCombo")).ActualHeight >= 28);
                Assert.IsFalse(((Button)view.FindName("acceptBtn")).IsDefault);
            }
            // The dialog constructor only builds controls. Accept/Cancel select
            // a result; no output service is involved until the caller acts.
            for (int type = 0; type < 2; type++)
            for (int reserve = 0; reserve < 2; reserve++)
            {
                var dialog = new DS4WinWPF.DS4Forms.PluginOutDevWindow();
                try
                {
                    ((ComboBox)dialog.FindName("devTypeCombo")).SelectedIndex = type;
                    ((ComboBox)dialog.FindName("reserveTypeCombo")).SelectedIndex = reserve;
                    ((Button)dialog.FindName("acceptBtn")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Assert.AreEqual(MessageBoxResult.OK, dialog.Result);
                    Assert.AreEqual(type == 0 ? DS4Windows.OutContType.ViiperDS4 : DS4Windows.OutContType.ViiperX360, dialog.ContType);
                    Assert.AreEqual((DS4WinWPF.DS4Control.OutSlotDevice.ReserveStatus)reserve, dialog.ReserveType);
                }
                finally { dialog.Close(); }
            }
            var cancelled = new DS4WinWPF.DS4Forms.PluginOutDevWindow();
            try
            {
                ((Button)cancelled.FindName("cancelBtn")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.AreEqual(MessageBoxResult.Cancel, cancelled.Result);
                Assert.AreEqual(DS4Windows.OutContType.None, cancelled.ContType);
            }
            finally { cancelled.Close(); }
        });
    }

    public sealed class OutputFixture : INotifyPropertyChanged
    {
        public SlotFixture[] SlotDeviceEntries { get; } = new[]
        {
            new SlotFixture { InputSlotDisplayString = "1 (DS4 v2 — Bluetooth)", CurrentType = "Xbox 360", BoundInput = true },
            new SlotFixture(),
            new SlotFixture { CurrentType = "DS4", ReserveChoice = 1 },
            new SlotFixture(),
        };
        private int selectedIndex = -1;
        public int SelectedIndex
        {
            get => selectedIndex;
            set
            {
                selectedIndex = value;
                foreach (string name in new[] { nameof(SelectedIndex), nameof(PluginEnabled), nameof(UnpluginEnabled), nameof(SidePanelVisibility) })
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            }
        }
        public bool UnpluginEnabled => SelectedIndex == 0 || SelectedIndex == 2;
        public bool PluginEnabled => SelectedIndex >= 0 && !UnpluginEnabled;
        public Visibility SidePanelVisibility => UnpluginEnabled ? Visibility.Visible : Visibility.Collapsed;
        public event PropertyChangedEventHandler PropertyChanged;
    }

    public sealed class SlotFixture : INotifyPropertyChanged
    {
        public string InputSlotDisplayString { get; init; } = "";
        public string CurrentType { get; init; } = "Empty";
        public string DesiredType => ReserveChoice == 1 ? CurrentType : "Dynamic";
        public string XInputSlotNum => "";
        public bool DisplayXInputSlotNum => false; // The VIIPER view does not expose an XInput index.
        public bool BoundInput { get; init; }
        public bool Dirty { get; private set; }
        private int reserveChoice;
        public int ReserveChoice
        {
            get => reserveChoice;
            set
            {
                if (reserveChoice == value) return;
                reserveChoice = value;
                Dirty = true;
                foreach (string name in new[] { nameof(ReserveChoice), nameof(Dirty), nameof(DesiredType) })
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            }
        }
        public event PropertyChangedEventHandler PropertyChanged;
    }

    [TestMethod]
    public void ToolsWindowsFitBothThemes()
    {
        WpfTestHost.Run(() =>
        {
            foreach (bool dark in new[] { false, true })
            {
                PresentationHarness.Theme(dark);
                foreach (string name in new[] { "LegacyProfileImportWindow", "LegacyRemovalPlanWindow" })
                {
                    UserControl view = PresentationHarness.LoadControl(name);
                    var owner = new Window { Content = view, DataContext = new ToolsFixture() };
                    var size = new Size(680, 460);
                    PresentationHarness.Layout(view, size);
                    PresentationHarness.Preview(view, name.ToLowerInvariant(), dark);
                    foreach (Control control in PresentationHarness.Descendants<Control>(view)
                        .Where(c => c.ActualWidth > 0 && PresentationHarness.Participates(c)))
                        PresentationHarness.AssertInside(view, control, name);
                    owner.Content = null;
                }
            }
        });
    }

    [TestMethod]
    public void AboutSectionsKeepCreditsAndCloseReachableInBothThemesAndWindowSizes()
    {
        WpfTestHost.Run(() =>
        {
            foreach (bool dark in new[] { false, true })
            foreach (Size size in new[] { new Size(624, 448), new Size(744, 568) })
            {
                PresentationHarness.Theme(dark);
                UserControl view = CreateAboutView();
                var tabs = (TabControl)view.FindName("aboutTabs");
                foreach (int index in new[] { 0, 1, 2, 0 })
                {
                    tabs.SelectedIndex = index;
                    PresentationHarness.Layout(view, size);
                    Assert.AreSame(((TabItem)tabs.Items[index]).Content, tabs.SelectedContent);
                    foreach (FrameworkElement element in PresentationHarness.Descendants<FrameworkElement>(view)
                        .Where(e => (e is Control || e is TextBlock || e is Image) && e.ActualWidth > 0 && PresentationHarness.Participates(e)))
                        PresentationHarness.AssertReachable(view, element, "About");
                    var close = (Button)view.FindName("aboutCloseBtn");
                    Assert.IsTrue(close.IsCancel && close.IsTabStop);
                    Assert.IsNotNull(close.FocusVisualStyle);
                    var selected = (TabItem)tabs.SelectedItem;
                    var cue = (Border)selected.Template.FindName("SelectionIndicator", selected);
                    Assert.AreEqual(Visibility.Visible, cue.Visibility);
                    var body = PresentationHarness.Descendants<TextBlock>(view).First(t =>
                        t.Text.StartsWith(index == 0 ? "PureDS4 is" : index == 1 ? "Shortcuts depend" : "PureDS4 builds"));
                    Assert.AreEqual(FontWeights.Normal, body.FontWeight,
                        "Selected-tab emphasis must not turn body paragraphs into bold text.");
                    string scrollName = index == 0 ? "overviewScroll" : index == 1 ? "shortcutsScroll" : "licensesScroll";
                    var scroll = (ScrollViewer)view.FindName(scrollName);
                    Assert.AreEqual(ScrollBarVisibility.Disabled, scroll.HorizontalScrollBarVisibility);
                    scroll.ScrollToEnd();
                    PresentationHarness.Layout(view, size);
                    PresentationHarness.AssertReachable(view, close, "About footer after scrolling");
                }
            }
        });
    }

    internal static UserControl CreateAboutView(int section = 0)
    {
        UserControl view = PresentationHarness.LoadControl("About");
        ((TextBlock)view.FindName("versionText")).Text = "UI preview — not a release build";
        ((TabControl)view.FindName("aboutTabs")).SelectedIndex = section;
        return view;
    }

    [TestMethod]
    public void InputDiagnosticsFitsBothThemesAndViewports()
    {
        WpfTestHost.Run(() =>
        {
            foreach (bool dark in new[] { false, true })
            foreach (Size size in PresentationHarness.Viewports)
            foreach (int count in new[] { 0, 1, 2, 4 })
            {
                PresentationHarness.Theme(dark);
                UserControl view = CreateDiagnosticsView(count, longText: true);
                PresentationHarness.Layout(view, size);
                Assert.AreEqual(count, ((ItemsControl)view.FindName("controllerList")).Items.Count);
                Assert.AreEqual(count == 0 ? Visibility.Visible : Visibility.Collapsed,
                    ((FrameworkElement)view.FindName("diagnosticsEmptyState")).Visibility);
                foreach (FrameworkElement element in PresentationHarness.Descendants<FrameworkElement>(view)
                    .Where(c => (c is Control || c is TextBlock) && c.ActualWidth > 0 && PresentationHarness.Participates(c)))
                    PresentationHarness.AssertReachable(view, element, "input diagnostics");
                foreach (string name in new[] { "copyReportBtn", "resetMeasurementsBtn" })
                {
                    var button = (Button)view.FindName(name);
                    Assert.IsTrue(button.IsTabStop);
                    Assert.IsNotNull(button.FocusVisualStyle);
                    Assert.IsFalse(string.IsNullOrEmpty(System.Windows.Automation.AutomationProperties.GetName(button)));
                }
                var scroll = (ScrollViewer)view.FindName("diagnosticsScroll");
                if (count == 4) Assert.IsTrue(scroll.ScrollableHeight > 0);
                scroll.ScrollToEnd();
                PresentationHarness.Layout(view, size);
                if (count > 0)
                {
                    var list = (ItemsControl)view.FindName("controllerList");
                    var entries = (DS4WinWPF.DS4Forms.ViewModels.InputDiagnosticsEntry[])list.ItemsSource;
                    entries[^1].Rate = "123 Hz";
                    PresentationHarness.Layout(view, size);
                    Assert.IsTrue(PresentationHarness.Descendants<TextBlock>(view).Any(t => t.Text == "123 Hz"),
                        "Live measurement notifications must still reach the real template.");
                }
            }
        });
    }

    internal static UserControl CreateDiagnosticsView(int count = 2, bool longText = false)
    {
        UserControl view = PresentationHarness.LoadControl("InputDiagnosticsControl");
        if (!longText && count == 2) view.DataContext = new DiagnosticsFixture();
        else view.DataContext = new
        {
            EmptyStateVisibility = count == 0 ? Visibility.Visible : Visibility.Collapsed,
            Controllers = Enumerable.Range(1, count).Select(i => new DS4WinWPF.DS4Forms.ViewModels.InputDiagnosticsEntry
            {
                Title = $"Controller {i}: DualShock 4 with a deliberately long controller name for layout verification",
                Connection = i % 2 == 0 ? "Bluetooth" : "USB",
                Verdict = "Measuring",
                VerdictState = DS4WinWPF.DS4Forms.StatusVisualState.Neutral,
                Explanation = "Measuring report timing. A longer diagnostic explanation must remain readable at the smallest window size without covering the measurement fields or hiding actions.",
                Rate = "250 Hz", TypicalInterval = "4.0 ms", P95Interval = "4.4 ms", P99Interval = "8.1 ms",
                WorstInterval = "28.1 ms", Stalls = "26", LostReports = "104 (1.58%)",
                Window = "8,192 reports over 33 seconds, with a longer measurement description",
                HostNote = "The controller kept cadence, but Windows delivered reports late. This additional note must wrap rather than disappear beyond the viewport.",
                HostNoteVisibility = Visibility.Visible,
            }).ToArray(),
        };
        return view;
    }

    /// <summary>A healthy cable beside the Bluetooth fault we actually measured.</summary>
    public sealed class DiagnosticsFixture
    {
        public Visibility EmptyStateVisibility { get; } = Visibility.Collapsed;

        public object[] Controllers { get; } =
        {
            new
            {
                Slot = 1,
                Title = "Controller 1: DS4 v.2",
                Identity = "DS4 v.2",
                Connection = "USB",
                Verdict = "Steady",
                VerdictState = DS4WinWPF.DS4Forms.StatusVisualState.Success,
                Explanation = "Every report arrived on time, about 250 per second.",
                Rate = "250 Hz",
                TypicalInterval = "4.0 ms",
                P95Interval = "4.4 ms",
                P99Interval = "4.4 ms",
                WorstInterval = "4.5 ms",
                Stalls = "0",
                LostReports = "0 (0.00%)",
                Window = "8,192 reports over 33 s",
                HostNote = "",
                HostNoteVisibility = Visibility.Collapsed,
            },
            new
            {
                Slot = 2,
                Title = "Controller 2: DS4 v.2",
                Identity = "DS4 v.2",
                Connection = "Bluetooth",
                Verdict = "Frequent stalls",
                VerdictState = DS4WinWPF.DS4Forms.StatusVisualState.Error,
                Explanation = "About 1.6% of reports never arrived. The worst gap was 28 ms, " +
                    "around 7 times the usual 4.0 ms. On a wireless link this is usually " +
                    "interference or the Bluetooth adapter.",
                Rate = "246 Hz",
                TypicalInterval = "4.0 ms",
                P95Interval = "4.4 ms",
                P99Interval = "8.1 ms",
                WorstInterval = "28.1 ms",
                Stalls = "26",
                LostReports = "104 (1.58%)",
                Window = "8,192 reports over 33 s",
                HostNote = "The controller kept cadence, but Windows delivered reports late: " +
                    "worst 15.0 ms. Background load or a busy USB port usually causes this.",
                HostNoteVisibility = Visibility.Visible,
            },
        };
    }

    /// <summary>
    /// Every specialist workspace keeps Home's inset, like Settings and Tools.
    /// </summary>
    [TestMethod]
    public void SpecialistWorkspacesKeepHomesInset()
    {
        XElement main = PresentationHarness.Source("MainWindow");
        string home = (string)main.Descendants()
            .Single(e => (string)e.Attribute(PresentationHarness.X + "Name") == "homeWorkspace")
            .Attribute("Margin");

        Assert.AreEqual(home, (string)main.Descendants()
            .Single(e => (string)e.Attribute(PresentationHarness.X + "Name") == "runtimeLogWorkspace").Attribute("Margin"),
            "The runtime log must share Home's workspace inset.");

        XElement slots = PresentationHarness.Source("OutputSlotManagerControl")
            .Elements(PresentationHarness.Wpf + "Grid").Single();
        Assert.AreEqual(home, (string)slots.Attribute("Margin"),
            "Output slots must share Home's workspace inset.");
    }

    /// <summary>Disposable presentation state; never live slots or real logs.</summary>
    public sealed class ToolsFixture
    {
        public object[] LogMessages { get; } = Enumerable.Range(1, 14).Select(i => (object)new
        {
            Datetime = new DateTime(2026, 9, 17, 19, 7, i % 60),
            Severity = i == 1 || i % 5 == 0 ? "Warning" : "Info",
            Warning = i == 1 || i % 5 == 0,
            Message = i == 1 || i % 5 == 0
                ? "Controller could not be hidden from games, so no game output was created. Check controller protection in Settings before trying again. No controller settings were changed. Review game output setup and protection status before retrying the connection."
                : "Diagnostic event " + i + " with a longer description that has to wrap or be trimmed",
        }).ToArray();

        public bool FullTabsEnabled { get; } = true;
        public bool ViewEnabled { get; } = true;
    }
}
