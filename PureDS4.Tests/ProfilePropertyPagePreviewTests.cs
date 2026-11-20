using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DS4WindowsTests;

/// <summary>
/// Opt-in offscreen PNGs of every profile editor page, scrolled through, in both
/// themes and two sizes. Set PUREDS4_VISUAL_PREVIEW_DIRECTORY to an existing
/// disposable folder; without it this test does nothing. It renders the real
/// markup against a disposable fixture: no window is shown, no app, service,
/// controller or profile is touched. Layout rules are asserted separately.
/// </summary>
[TestClass]
[DoNotParallelize]
public class ProfilePropertyPagePreviewTests
{
    private static readonly Size[] Sizes = { new(1024, 660), new(720, 480) };

    private sealed record Variant(int Section, string Name, bool On, params (string Key, object Value)[] Set);

    private static readonly Variant[] Variants =
    {
        new(0, "mapping", false), new(1, "actions", false), new(2, "readings", false),
        new(3, "axis-controls", false, ("LSOutputIndex", 1), ("RSOutputIndex", 1)),
        new(3, "axis-controls-on", true, ("LSOutputIndex", 1), ("RSOutputIndex", 1)),
        new(3, "axis-axial", false, ("LSOutputIndex", 1), ("RSOutputIndex", 1), ("LSDeadTypeIndex", 1), ("RSDeadTypeIndex", 1)),
        new(3, "axis-flick", false, ("LSOutputIndex", 2), ("RSOutputIndex", 2)),
        new(4, "lightbar", false), new(4, "lightbar-on", true),
        new(5, "touchpad-mouse", false, ("TouchpadOutputIndex", 0)),
        new(5, "touchpad-mouse-on", true, ("TouchpadOutputIndex", 0)),
        new(5, "touchpad-controls", false, ("TouchpadOutputIndex", 1)),
        new(5, "touchpad-mousejoystick", false, ("TouchpadOutputIndex", 2)),
        new(5, "touchpad-mousejoystick-on", true, ("TouchpadOutputIndex", 2)),
        new(5, "touchpad-absolute", false, ("TouchpadOutputIndex", 3)),
        new(5, "touchpad-passthrough", false, ("TouchpadOutputIndex", 4)),
        new(6, "gyro-controls", false, ("GyroOutModeIndex", 0)),
        new(6, "gyro-controls-on", true, ("GyroOutModeIndex", 0)),
        new(6, "gyro-mouse", false, ("GyroOutModeIndex", 1), ("GyroMouseWeightAvgPanelVisibility", Visibility.Collapsed)),
        new(6, "gyro-mouse-on", true, ("GyroOutModeIndex", 1), ("GyroMouseWeightAvgPanelVisibility", Visibility.Collapsed)),
        new(6, "gyro-mousejoystick", false, ("GyroOutModeIndex", 2), ("GyroMouseStickWeightAvgPanelVisibility", Visibility.Collapsed)),
        new(6, "gyro-mousejoystick-on", true, ("GyroOutModeIndex", 2), ("GyroMouseStickWeightAvgPanelVisibility", Visibility.Collapsed)),
        new(6, "gyro-swipe", false, ("GyroOutModeIndex", 3)),
        new(6, "gyro-passthrough", false, ("GyroOutModeIndex", 4)),
        new(7, "advanced", false), new(7, "advanced-on", true),
    };

    [TestMethod]
    public void RenderProfileEditorPagePreviews()
    {
        string root = Environment.GetEnvironmentVariable("PUREDS4_VISUAL_PREVIEW_DIRECTORY");
        if (string.IsNullOrEmpty(root)) return;
        Assert.IsTrue(Directory.Exists(root), "Prepare the disposable preview folder first.");
        foreach (bool dark in new[] { false, true })
        foreach (Size size in Sizes)
        foreach (Variant variant in Variants)
            WpfTestHost.Run(() =>
            {
                ProfileWorkspacePresentationTests.Theme(dark);
                UserControl view = ProfileWorkspacePresentationTests.Load("ProfileEditor");
                var owner = new Window { Content = view };
                try
                {
                    view.DataContext = variant.Section < 3
                        ? new ProfileWorkspacePresentationTests.EditorFixture()
                        : ProfilePropertyPageFixture.Create(variant.On,
                            variant.Set.ToDictionary(s => s.Key, s => s.Value));
                    ScrollViewer scroll = ProfilePropertyPageFixture.ShowSection(view, variant.Section);
                    if (variant.Section == 5)
                        ProfilePropertyPageFixture.PlaceTouchButtonOptions(view,
                            (int)variant.Set.Single(s => s.Key == "TouchpadOutputIndex").Value);
                    string folder = Path.Combine(root, $"{(int)size.Width}x{(int)size.Height}", dark ? "dark" : "light");
                    Directory.CreateDirectory(folder);
                    string stem = Path.Combine(folder, $"{variant.Section}-{variant.Name}");
                    PresentationHarness.Layout(view, size);
                    // The "-on" variants exist to show dependent rows; one tall image is enough.
                    if (!variant.Name.EndsWith("-on"))
                    {
                        Save(view, stem + "-scroll1");
                        for (int frame = 2; scroll != null && frame <= 12 &&
                             scroll.VerticalOffset < scroll.ScrollableHeight - 0.5; frame++)
                        {
                            scroll.ScrollToVerticalOffset(Math.Min(scroll.ScrollableHeight,
                                scroll.VerticalOffset + scroll.ViewportHeight * 0.85));
                            PresentationHarness.Layout(view, size);
                            Save(view, stem + $"-scroll{frame}");
                        }
                        scroll?.ScrollToTop();
                    }
                    if (scroll != null)
                    {
                        // The whole page in one image, at the same width.
                        double top = scroll.TransformToAncestor(view).Transform(new Point()).Y;
                        PresentationHarness.Layout(view, new Size(size.Width, Math.Max(size.Height, top + scroll.ExtentHeight + 12)));
                        PresentationHarness.Layout(view, new Size(size.Width, Math.Max(size.Height, top + scroll.ExtentHeight + 12)));
                        Save(view, stem + "-full");
                    }
                }
                finally { owner.Content = null; }
            });
    }

    private static void Save(FrameworkElement view, string path)
    {
        var bitmap = new RenderTargetBitmap(Math.Max(1, (int)view.ActualWidth), Math.Max(1, (int)view.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        var drawing = new DrawingVisual();
        using (var dc = drawing.RenderOpen())
            dc.DrawRectangle((Brush)Application.Current.FindResource("SurfaceBaseBrush"), null, new Rect(view.RenderSize));
        bitmap.Render(drawing);
        bitmap.Render(view);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path + ".png");
        encoder.Save(stream);
    }
}
