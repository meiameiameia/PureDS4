using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
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

                UserControl log = PresentationHarness.LoadTabWorkspace("logTab");
                var logOwner = new Window { Content = log, DataContext = new ToolsFixture() };
                ((ListView)log.FindName("logListView")).ItemsSource = new ToolsFixture().LogMessages;
                PresentationHarness.Layout(log, size);
                PresentationHarness.Preview(log, "log-" + (int)size.Width, dark);
                foreach (Control control in PresentationHarness.Descendants<Control>(log)
                    .Where(c => c.ActualWidth > 0 && PresentationHarness.Participates(c)))
                    PresentationHarness.AssertInside(log, control, "log");
                logOwner.Content = null;

                UserControl slots = PresentationHarness.LoadControl("OutputSlotManagerControl");
                var slotsOwner = new Window { Content = slots, DataContext = new ToolsFixture() };
                slots.DataContext = new ToolsFixture();
                PresentationHarness.Layout(slots, size);
                PresentationHarness.Preview(slots, "outputslots-" + (int)size.Width, dark);
                foreach (Control control in PresentationHarness.Descendants<Control>(slots)
                    .Where(c => c.ActualWidth > 0 && PresentationHarness.Participates(c)))
                    PresentationHarness.AssertInside(slots, control, "output slots");
                slotsOwner.Content = null;
            }
        });
    }

    [TestMethod]
    public void ToolsWindowsFitBothThemes()
    {
        WpfTestHost.Run(() =>
        {
            foreach (bool dark in new[] { false, true })
            {
                PresentationHarness.Theme(dark);
                foreach (string name in new[] { "About", "LegacyProfileImportWindow", "LegacyRemovalPlanWindow" })
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
    public void InputDiagnosticsFitsBothThemesAndViewports()
    {
        WpfTestHost.Run(() =>
        {
            foreach (bool dark in new[] { false, true })
            foreach (Size size in PresentationHarness.Viewports)
            {
                PresentationHarness.Theme(dark);
                UserControl view = PresentationHarness.LoadControl("InputDiagnosticsControl");
                var owner = new Window { Content = view, DataContext = new DiagnosticsFixture() };
                PresentationHarness.Layout(view, size);
                PresentationHarness.Preview(view, "diagnostics-" + (int)size.Width, dark);
                foreach (Control control in PresentationHarness.Descendants<Control>(view)
                    .Where(c => c.ActualWidth > 0 && PresentationHarness.Participates(c)))
                    PresentationHarness.AssertInside(view, control, "input diagnostics");
                owner.Content = null;
            }
        });
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

        Assert.AreEqual(home, (string)PresentationHarness.Workspace(main, "logTab").Attribute("Margin"),
            "The runtime log must share Home's workspace inset.");

        XElement slots = PresentationHarness.Source("OutputSlotManagerControl")
            .Elements(PresentationHarness.Wpf + "Grid").Single();
        Assert.AreEqual(home, (string)slots.Attribute("Margin"),
            "Output slots must share Home's workspace inset.");
    }

    /// <summary>Disposable presentation state; never live slots or real logs.</summary>
    public sealed class ToolsFixture
    {
        public object[] SlotDeviceEntries { get; } = Enumerable.Range(1, 4).Select(i => (object)new
        {
            InputSlotDisplayString = i == 1 ? "1: DS4 v.2 (USB)" : "Unbound",
            CurrentType = i == 1 ? "Xbox 360" : "Empty",
            DesiredType = i == 1 ? "Xbox 360" : "Empty",
            XInputSlotNum = i.ToString(),
            DisplayXInputSlotNum = i == 1,
            BoundInput = i == 1,
        }).ToArray();

        public int SelectedIndex { get; set; }
        public bool PluginEnabled { get; } = true;
        public bool UnpluginEnabled { get; } = true;
        public bool Dirty { get; } = true;
        public int ReserveChoice { get; set; }
        public Visibility SidePanelVisibility { get; } = Visibility.Visible;

        public object[] LogMessages { get; } = Enumerable.Range(1, 14).Select(i => (object)new
        {
            Datetime = new DateTime(2026, 9, 17, 19, 7, i % 60),
            Severity = i % 5 == 0 ? "Warning" : "Info",
            Warning = i % 5 == 0,
            Message = i % 5 == 0
                ? "Controller could not be hidden from games, so no game output was created"
                : "Diagnostic event " + i + " with a longer description that has to wrap or be trimmed",
        }).ToArray();

        public bool FullTabsEnabled { get; } = true;
        public bool ViewEnabled { get; } = true;
    }
}
