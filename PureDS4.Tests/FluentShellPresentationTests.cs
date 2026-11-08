using DS4WinWPF.DS4Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DS4WindowsTests;

/// <summary>
/// Actual WPF resources and Home markup, with disposable state and no product
/// startup, hardware, tray icon or user settings. Renders are not installed-app
/// screenshots and do not establish native DPI or physical keyboard acceptance.
/// </summary>
[TestClass]
[DoNotParallelize]
public class FluentShellPresentationTests
{
    [TestMethod]
    public void SectionNavigationUsesTheSameShortCueWithoutConfusingSelectionAndFocus()
    {
        WpfTestHost.Run(() =>
        {
            foreach (bool dark in new[] { false, true })
            {
                HomeWorkspaceTests.LoadTheme(dark);
                var list = new ListBox
                {
                    ItemContainerStyle = (Style)Application.Current.FindResource("FoundationNavigationListItemStyle"),
                    SelectedIndex = 0,
                };
                var first = new ListBoxItem { Content = "General" };
                var second = new ListBoxItem { Content = "Game output" };
                list.Items.Add(first);
                list.Items.Add(second);
                list.SelectedIndex = 0;
                Layout(list, new Size(180, 240));
                first.ApplyTemplate();
                second.ApplyTemplate();
                Layout(list, new Size(180, 240));
                var cue = (Border)first.Template.FindName("SelectionIndicator", first);
                var focus = (Border)first.Template.FindName("KeyboardFocusRing", first);
                Assert.AreEqual(Visibility.Visible, cue.Visibility);
                Assert.AreEqual(4d, cue.ActualWidth);
                Assert.AreEqual(first.ActualHeight - 8, cue.ActualHeight, 0.1);
                Assert.IsTrue(cue.ActualHeight / first.ActualHeight >= 0.65);
                Assert.AreEqual(Visibility.Collapsed, focus.Visibility);
                Inside(first, cue);
                Size original = first.RenderSize;
                list.SelectedIndex = 1;
                Layout(list, new Size(180, 240));
                Assert.AreEqual(original, first.RenderSize);
                Assert.AreEqual(Visibility.Collapsed, cue.Visibility);
                Assert.AreEqual(Visibility.Visible,
                    ((Border)second.Template.FindName("SelectionIndicator", second)).Visibility);
                second.IsEnabled = false;
                Assert.AreEqual(ColorOf("TextDisabledBrush"), ((SolidColorBrush)second.Foreground).Color);
                Assert.AreEqual(1d, second.Opacity);
            }
        });
    }

    [TestMethod]
    public void NavigationKeepsSelectionFocusAndDisabledStatesSeparate()
    {
        WpfTestHost.Run(() =>
        {
            foreach (bool dark in new[] { false, true })
            {
                HomeWorkspaceTests.LoadTheme(dark);
                var tabs = Navigation(new Grid());
                Layout(tabs, new Size(704, 360));
                var home = (TabItem)tabs.Items[0];
                var profiles = (TabItem)tabs.Items[1];
                var selected = Part(home, "SelectionIndicator");
                var focus = Part(home, "KeyboardFocusRing");
                Assert.AreEqual(Visibility.Visible, selected.Visibility);
                Assert.AreEqual(Part(home, "TabChrome").ActualWidth - 24, selected.ActualWidth, 0.1);
                Assert.IsTrue(selected.ActualWidth / Part(home, "TabChrome").ActualWidth >= 0.6);
                Assert.AreEqual(3d, selected.ActualHeight);
                Inside(home, selected);
                Assert.AreEqual(Visibility.Collapsed, focus.Visibility,
                    "Selection is not keyboard focus.");
                double width = home.ActualWidth;
                double height = home.ActualHeight;
                tabs.SelectedIndex = 1;
                Layout(tabs, new Size(704, 360));
                Assert.AreEqual(Visibility.Collapsed, selected.Visibility);
                Assert.AreEqual(Visibility.Visible, Part(profiles, "SelectionIndicator").Visibility);
                Assert.AreEqual(width, home.ActualWidth, 0.01);
                Assert.AreEqual(height, home.ActualHeight, 0.01);
                var keyboardTrigger = home.Template.Triggers.OfType<Trigger>()
                    .Single(t => t.Property == UIElement.IsKeyboardFocusedProperty);
                Assert.IsTrue(keyboardTrigger.Setters.OfType<Setter>()
                    .Any(s => s.TargetName == "KeyboardFocusRing" && Equals(s.Value, Visibility.Visible)));
                profiles.IsEnabled = false;
                Layout(tabs, new Size(704, 360));
                Assert.AreEqual(ColorOf("TextDisabledBrush"), ((SolidColorBrush)profiles.Foreground).Color);
                Assert.AreEqual(1d, profiles.Opacity,
                    "Disabled labels use a tested color, not faded page content.");
            }
        });
    }

    [TestMethod]
    public void ExistingControlsRespondToThemeChangesWithoutLosingSelection()
    {
        WpfTestHost.Run(() =>
        {
            HomeWorkspaceTests.LoadTheme(false);
            var state = new HomeWorkspaceTests.HomeFixture(1);
            Grid home = HomeWorkspaceTests.LoadHome();
            TabControl tabs = Navigation(home);
            var owner = new Window { Content = tabs, DataContext = state };
            try
            {
                var list = (ListView)home.FindName("controllerLV");
                foreach (bool dark in new[] { false, true, false })
                {
                    HomeWorkspaceTests.LoadTheme(dark);
                    Layout(tabs, new Size(1008, 540));
                    Assert.AreSame(state.ControllerCol[0], list.SelectedItem);
                    Assert.AreEqual(ColorOf("SurfaceBaseBrush"), ((SolidColorBrush)list.Background).Color);
                    var row = Descendants<ListViewItem>(list).Single();
                    var chrome = (Border)row.Template.FindName("Chrome", row);
                    Assert.AreEqual(ColorOf("SurfaceSelectedBrush"), ((SolidColorBrush)chrome.Background).Color);
                    var button = Descendants<Button>(home).First();
                    Assert.AreEqual(ColorOf("SurfaceRaisedBrush"), ((SolidColorBrush)button.Background).Color);
                    Assert.AreEqual(ColorOf("TextPrimaryBrush"), ((SolidColorBrush)button.Foreground).Color);
                }
            }
            finally { owner.Content = null; }
        });
    }

    [TestMethod]
    public void ShellAndHomeFitTogetherWithBannersAndMultipleControllers()
    {
        WpfTestHost.Run(() =>
        {
            foreach (bool dark in new[] { false, true })
            foreach (Size size in new[] { new Size(704, 430), new Size(1008, 620) })
            foreach (int count in new[] { 0, 1, 4 })
            foreach (bool degraded in new[] { false, true })
            {
                HomeWorkspaceTests.LoadTheme(dark);
                var state = new HomeWorkspaceTests.HomeFixture(count) { Degraded = degraded };
                Grid home = HomeWorkspaceTests.LoadHome();
                var body = new DockPanel();
                body.SetResourceReference(Panel.BackgroundProperty, "SurfaceBaseBrush");
                var banner = new StatusBanner
                {
                    IsOpen = true,
                    State = degraded ? StatusVisualState.Warning : StatusVisualState.Neutral,
                    Title = degraded ? "Game output is unavailable" : "Some actions need administrator rights",
                    Message = degraded ? "Open game output settings to review setup and recovery options."
                        : "Game output may still work. Setup and protection changes may need elevation.",
                    ActionLabel = degraded ? "Open game output settings" : "Restart as administrator",
                    ShowAction = true,
                };
                DockPanel.SetDock(banner, Dock.Top);
                body.Children.Add(banner);
                TabControl tabs = Navigation(home);
                body.Children.Add(tabs);
                var owner = new Window { Content = body, DataContext = state };
                try
                {
                    Layout(body, size);
                    Inside(body, banner);
                    Inside(body, tabs);
                    var bannerBorder = (Border)banner.FindName("BannerBorder");
                    Rect bannerBounds = bannerBorder.TransformToAncestor(body).TransformBounds(new Rect(bannerBorder.RenderSize));
                    Rect headerBounds = Part((TabItem)tabs.Items[0], "TabChrome").TransformToAncestor(body)
                        .TransformBounds(new Rect(Part((TabItem)tabs.Items[0], "TabChrome").RenderSize));
                    Assert.AreEqual(8d, bannerBounds.Top, 0.1, "Banner inset below native chrome.");
                    Assert.AreEqual(bannerBounds.Top, headerBounds.Top - bannerBounds.Bottom, 0.1,
                        "The gap below the banner must match the gap above it.");
                    var inspector = (Border)home.FindName("homeInspector");
                    Assert.AreEqual(count > 0 ? Visibility.Visible : Visibility.Collapsed, inspector.Visibility);
                    if (count > 0)
                    {
                        Inside(body, inspector);
                        foreach (Button button in Descendants<Button>(inspector)
                            .Where(b => b.Visibility == Visibility.Visible)) Inside(body, button);
                    }
                    foreach (TabItem tab in tabs.Items) Inside(body, Part(tab, "TabChrome"));
                    Assert.IsTrue(home.TransformToAncestor(tabs).Transform(new Point()).Y < 80,
                        "Home begins below navigation instead of floating in the middle of a tall window.");
                    if (count == 1 && !degraded)
                        Preview(body, $"pureds4-fluent-shell-{(int)size.Width}-{(dark ? "dark" : "light")}.png",
                            Part((TabItem)tabs.Items[0], "SelectionIndicator"));
                }
                finally { owner.Content = null; }
            }
        });
    }

    [TestMethod]
    public void CompactBannerCentersItsGlyphWhileExpandedBannerKeepsHeadingAlignment()
    {
        WpfTestHost.Run(() =>
        {
            foreach (bool dark in new[] { false, true })
            foreach (double width in new[] { 704d, 1008d })
            {
                HomeWorkspaceTests.LoadTheme(dark);
                var notice = DS4Windows.ElevationNotice.Evaluate(false);
                var banner = new StatusBanner
                {
                    IsOpen = true, Title = notice.Title, Message = notice.Message,
                    ActionLabel = notice.ActionLabel, ShowAction = true,
                };
                var stack = new StackPanel();
                stack.Children.Add(banner);
                var glyph = (TextBlock)banner.FindName("StateGlyph");
                var row = (Grid)((Border)banner.FindName("BannerBorder")).Child;
                foreach (bool compact in new[] { true, false, true })
                {
                    banner.IsCompact = compact;
                    stack.Measure(new Size(width, double.PositiveInfinity));
                    stack.Arrange(new Rect(new Point(), stack.DesiredSize));
                    stack.UpdateLayout();
                    double glyphTop = glyph.TransformToAncestor(row).Transform(new Point()).Y;
                    double expectedTop = compact ? (row.ActualHeight - glyph.ActualHeight) / 2 : 0;
                    Assert.AreEqual(expectedTop, glyphTop, 0.5,
                        $"Banner glyph alignment: compact={compact}, dark={dark}, width={width}.");
                    Inside(row, glyph);
                    if (compact)
                    {
                        var message = Descendants<TextBlock>(banner).Single(t => t.Text == notice.Message);
                        var action = Descendants<Button>(banner).Single(b => Equals(b.Content, notice.ActionLabel));
                        var caption = Descendants<TextBlock>(action).Single();
                        double messageCenter = message.TransformToAncestor(row)
                            .Transform(new Point(0, message.ActualHeight / 2)).Y;
                        double captionCenter = caption.TransformToAncestor(row)
                            .Transform(new Point(0, caption.ActualHeight / 2)).Y;
                        Assert.AreEqual(captionCenter, messageCenter, 0.5,
                            "The compact notice and its action caption share the same optical center.");
                    }
                }
                PresentationHarness.Preview(banner, $"administrator-banner-{(int)width}", dark);
            }
        });
    }

    private static TabControl Navigation(FrameworkElement home)
    {
        var tabs = new TabControl
        {
            Style = (Style)Application.Current.FindResource("BridgeMainTabControlStyle"),
        };
        string[] labels = { "Home", "Profiles", "Settings", "Tools" };
        string[] glyphs = { "\uE7FC", "\uE8A5", "\uE713", "\uE90F" };
        for (int i = 0; i < labels.Length; i++)
            tabs.Items.Add(new TabItem { Header = labels[i], Tag = glyphs[i], Content = i == 0 ? home : new Grid() });
        return tabs;
    }

    private static Border Part(TabItem tab, string name) => (Border)tab.Template.FindName(name, tab);
    private static Color ColorOf(string key) => ((SolidColorBrush)Application.Current.FindResource(key)).Color;
    private static void Layout(FrameworkElement view, Size size)
    {
        view.Measure(size);
        view.Arrange(new Rect(size));
        view.UpdateLayout();
    }

    private static void Inside(FrameworkElement root, FrameworkElement child)
    {
        Rect bounds = child.TransformToAncestor(root).TransformBounds(new Rect(child.RenderSize));
        Assert.IsTrue(bounds.Left >= -1 && bounds.Top >= -1 &&
            bounds.Right <= root.ActualWidth + 1 && bounds.Bottom <= root.ActualHeight + 1,
            $"{child.Name}/{child.GetType().Name}: {bounds} outside {root.RenderSize}");
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, i);
            if (child is T typed) yield return typed;
            foreach (T nested in Descendants<T>(child)) yield return nested;
        }
    }

    private static void Preview(FrameworkElement view, string name, Border selectedCue)
    {
        string directory = Environment.GetEnvironmentVariable("PUREDS4_VISUAL_PREVIEW_DIRECTORY");
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(view.ActualWidth),
            (int)Math.Ceiling(view.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(view);
        if (!string.IsNullOrEmpty(directory))
        {
            Assert.IsTrue(Directory.Exists(directory));
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(Path.Combine(directory, name));
            encoder.Save(stream);
        }
        // The render assertion is unconditional; requesting PNGs only controls
        // whether previews are saved, never whether the selection cue is tested.
        Point point = selectedCue.TransformToAncestor(view).Transform(
            new Point(selectedCue.ActualWidth / 2, selectedCue.ActualHeight / 2));
        var pixel = new byte[4];
        bitmap.CopyPixels(new Int32Rect((int)point.X, (int)point.Y, 1, 1), pixel, 4, 0);
        Color expected = ((SolidColorBrush)selectedCue.Background).Color;
        string ancestors = string.Empty;
        for (DependencyObject node = selectedCue; node != null; node = VisualTreeHelper.GetParent(node))
            if (node is FrameworkElement element)
                ancestors += $" {element.GetType().Name}/{element.Name}: size={element.RenderSize}, clip={VisualTreeHelper.GetClip(element)?.Bounds}, opacity={element.Opacity}, visibility={element.Visibility};";
        Assert.IsTrue(Math.Abs(pixel[0] - expected.B) <= 2 && Math.Abs(pixel[1] - expected.G) <= 2 &&
            Math.Abs(pixel[2] - expected.R) <= 2,
            $"Selected navigation cue at {point} must render: pixel #{pixel[2]:X2}{pixel[1]:X2}{pixel[0]:X2}, expected {expected}.{ancestors}");
    }
}
