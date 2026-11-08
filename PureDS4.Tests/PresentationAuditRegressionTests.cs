using DS4WinWPF.DS4Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;

namespace DS4WindowsTests;

// Behavior of real presentation controls with fictional state. No MainWindow,
// native HWND, user profiles or hardware actions are created by these tests.
[TestClass]
[DoNotParallelize]
public class PresentationAuditRegressionTests
{
    [TestMethod]
    public void ToolPagesKeepTheirParentCueAndBackReturnsToTools()
    {
        WpfTestHost.Run(() =>
        {
            foreach (bool dark in new[] { false, true })
            foreach (string page in new[] { "autoprofiles", "diagnostics", "outputslots", "log" })
            {
                PresentationHarness.Theme(dark);
                Window window = NativeWindowPreviewTests.CreateWindow(page, true);
                try
                {
                    var shell = (UserControl)window.Content;
                    PresentationHarness.Layout(shell, new Size(704, 430));
                    var tabs = (TabControl)shell.FindName("mainTabCon");
                    var tools = (TabItem)shell.FindName("toolsTab");
                    Assert.IsFalse(tools.IsSelected, "The actual tool page must own selected content.");
                    Assert.IsTrue(PrimaryNavigation.GetIsSectionActive(tools));
                    Assert.AreEqual(Visibility.Visible,
                        ((Border)tools.Template.FindName("SelectionIndicator", tools)).Visibility);
                    var back = PresentationHarness.Descendants<Button>(shell)
                        .Single(b => Equals(b.Content, "‹ Tools"));
                    PresentationHarness.AssertReachable(shell, back, page + " back action");
                    back.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    PresentationHarness.Layout(shell, shell.RenderSize);
                    Assert.AreSame(tools, tabs.SelectedItem);
                    Assert.IsFalse(PrimaryNavigation.GetIsSectionActive(tools), "The child section cue must clear.");
                    tabs.SelectedItem = shell.FindName("controllersTab");
                    PresentationHarness.Layout(shell, shell.RenderSize);
                    Assert.IsFalse(PrimaryNavigation.GetIsSectionActive(tools));
                    Assert.AreEqual(IntPtr.Zero, new WindowInteropHelper(window).Handle);
                }
                finally { window.Close(); }
            }
        });
    }

    [TestMethod]
    public void NativeAndRecoveryCardsKeepOnlyTheirApplicableAction()
    {
        WpfTestHost.Run(() =>
        {
            foreach (bool dark in new[] { false, true })
            foreach (bool recovery in new[] { false, true })
            {
                PresentationHarness.Theme(dark);
                Window window = NativeWindowPreviewTests.CreateWindow(recovery ? "home-recovery" : "home-native", true);
                try
                {
                    var shell = (UserControl)window.Content;
                    PresentationHarness.Layout(shell, new Size(704, 430));
                    var home = (Grid)((TabItem)((TabControl)shell.FindName("mainTabCon")).SelectedItem).Content;
                    Assert.AreEqual(Visibility.Collapsed, ((Border)home.FindName("homeInspector")).Visibility);
                    var sessions = (ItemsControl)home.FindName("nativePhysicalSessionsList");
                    Assert.AreEqual(1, sessions.Items.Count);
                    var actions = PresentationHarness.Descendants<Button>(sessions).ToArray();
                    Assert.AreEqual(recovery ? "Recover controller" : "Use game output",
                        actions.Single(b => b.Visibility == Visibility.Visible).Content);
                    Assert.AreEqual(1, actions.Count(b => b.Visibility == Visibility.Collapsed));
                    foreach (Button button in actions.Where(b => b.Visibility == Visibility.Visible))
                    {
                        PresentationHarness.AssertReachable(shell, button, "physical session action");
                        Assert.AreEqual("disposable-preview", button.Tag);
                    }
                    foreach (TextBlock text in PresentationHarness.Descendants<TextBlock>(sessions).Where(t => t.ActualWidth > 0))
                        PresentationHarness.AssertReachable(shell, text, "physical session copy");
                }
                finally { window.Close(); }
            }
        });
    }

    [TestMethod]
    public void CompactNoticeKeepsActionAndDismissWithoutHidingRecoveryNotice()
    {
        WpfTestHost.Run(() =>
        {
            PresentationHarness.Theme(false);
            var notice = DS4Windows.ElevationNotice.Evaluate(false);
            var compact = new StatusBanner { IsOpen = true, IsCompact = true, Title = notice.Title,
                Message = notice.Message, ActionLabel = notice.ActionLabel, ShowAction = true };
            var recovery = new StatusBanner { IsOpen = true, State = StatusVisualState.Recovery,
                Title = "Recovery required", Message = "Controller protection could not be restored.",
                ActionLabel = "Recover controller", ShowAction = true };
            var stack = new StackPanel(); stack.Children.Add(recovery); stack.Children.Add(compact);
            stack.Measure(new Size(704, double.PositiveInfinity));
            stack.Arrange(new Rect(new Point(), stack.DesiredSize)); stack.UpdateLayout();
            Assert.IsTrue(compact.ActualHeight < recovery.ActualHeight);
            Assert.AreEqual(Visibility.Visible, recovery.Visibility);
            Assert.AreEqual(notice.Message, PresentationHarness.Descendants<TextBlock>(compact)
                .Single(t => t.Text == notice.Message).Text);
            bool invoked = false; compact.ActionRequested += (_, _) => invoked = true;
            var action = PresentationHarness.Descendants<Button>(compact).Single(b => Equals(b.Content, notice.ActionLabel));
            PresentationHarness.AssertReachable(stack, action, "compact notice action");
            action.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Assert.IsTrue(invoked);
            PresentationHarness.Descendants<Button>(compact).Single(b => Equals(b.Content, "×"))
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.IsFalse(compact.IsOpen); Assert.IsTrue(recovery.IsOpen);
        });
    }

    [TestMethod]
    public void ThemeSwitchKeepsNativeScrollingSelectionAndGeometry()
    {
        WpfTestHost.Run(() =>
        {
            PresentationHarness.Theme(false);
            var list = new ListBox { ItemsSource = Enumerable.Range(0, 60).Select(i => "Profile " + i).ToArray(),
                SelectedIndex = 4 };
            var owner = new Window { Content = list };
            try
            {
            Size? previous = null;
            foreach (bool dark in new[] { false, true, false })
            {
                PresentationHarness.Theme(dark);
                PresentationHarness.Layout(list, new Size(220, 150));
                var scroll = PresentationHarness.Descendants<ScrollViewer>(list).Single();
                Assert.AreEqual(Visibility.Collapsed, scroll.ComputedHorizontalScrollBarVisibility);
                Assert.AreEqual(Visibility.Visible, scroll.ComputedVerticalScrollBarVisibility);
                scroll.ScrollToEnd(); PresentationHarness.Layout(list, list.RenderSize);
                Assert.AreEqual(scroll.ScrollableHeight, scroll.VerticalOffset);
                scroll.ScrollToHome(); PresentationHarness.Layout(list, list.RenderSize);
                Assert.AreEqual(4, list.SelectedIndex);
                var bar = PresentationHarness.Descendants<ScrollBar>(list).Single(b => b.Orientation == Orientation.Vertical);
                Assert.AreEqual(12d, bar.ActualWidth);
                if (previous != null) Assert.AreEqual(previous.Value, bar.RenderSize);
                previous = bar.RenderSize;
                foreach (Orientation orientation in new[] { Orientation.Vertical, Orientation.Horizontal })
                {
                    var standalone = new ScrollBar { Orientation = orientation, Maximum = 100, LargeChange = 10, Value = 50, ViewportSize = 20 };
                    // Unlike the scrollbar already inside the list, this isolated
                    // control has no Window resource ancestry.
                    standalone.SetResourceReference(FrameworkElement.StyleProperty, "FoundationScrollBarStyle");
                    PresentationHarness.Layout(standalone, orientation == Orientation.Vertical ? new Size(12, 160) : new Size(160, 12));
                    var track = (Track)standalone.Template.FindName("PART_Track", standalone);
                    Assert.AreEqual(orientation == Orientation.Vertical, track.IsDirectionReversed);
                    track.IncreaseRepeatButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    // Execute the native command explicitly: RaiseEvent does not invoke ButtonBase.OnClick.
                    var command = orientation == Orientation.Vertical ? ScrollBar.PageDownCommand : ScrollBar.PageRightCommand;
                    command.Execute(null, standalone); Assert.AreEqual(60d, standalone.Value);
                    Assert.AreEqual(orientation == Orientation.Vertical ? new Thickness(3, 1, 3, 1) : new Thickness(1, 3, 1, 3),
                        ((Border)track.Thumb.Template.FindName("Grip", track.Thumb)).Margin);
                }
            }
            }
            finally { owner.Close(); }
        });
    }
}
