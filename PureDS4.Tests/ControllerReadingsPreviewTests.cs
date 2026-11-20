using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DS4Windows;
using DS4WinWPF.DS4Forms;

namespace DS4WindowsTests;

/// <summary>
/// Opt-in offscreen PNGs of the Controller Readings page inside the profile editor,
/// with no controller and with a fake live state, in both themes and two sizes.
/// Set PUREDS4_VISUAL_PREVIEW_DIRECTORY to an existing disposable folder; without it
/// this test does nothing. The real control is used, but its timer is never started:
/// no window, app, service or controller is touched.
/// </summary>
[TestClass]
[DoNotParallelize]
public class ControllerReadingsPreviewTests
{
    private static readonly Size[] Sizes = { new(1024, 660), new(720, 480) };

    [TestMethod]
    public void RenderControllerReadingsPreviews()
    {
        string root = Environment.GetEnvironmentVariable("PUREDS4_VISUAL_PREVIEW_DIRECTORY");
        if (string.IsNullOrEmpty(root)) return;
        Assert.IsTrue(Directory.Exists(root), "Prepare the disposable preview folder first.");
        foreach (bool dark in new[] { false, true })
        foreach (Size size in Sizes)
        foreach (bool live in new[] { false, true })
            WpfTestHost.Run(() =>
            {
                (UserControl view, ControllerReadingsControl readings, Window owner) = Open(dark, live);
                try
                {
                    string folder = Path.Combine(root, $"{(int)size.Width}x{(int)size.Height}", dark ? "dark" : "light");
                    Directory.CreateDirectory(folder);
                    string stem = Path.Combine(folder, live ? "readings-live" : "readings-empty");
                    for (int pass = 0; pass < 3; pass++) PresentationHarness.Layout(view, size);
                    if (live)
                    {
                        // A second frame, as the timer would draw: the trigger output marker needs the bar's measured width.
                        ShowFakeState(readings);
                        PresentationHarness.Layout(view, size);
                    }
                    var scroll = (ScrollViewer)readings.FindName("readingsScroll");
                    Save(view, stem + "-scroll1");
                    for (int frame = 2; frame <= 6 && scroll.VerticalOffset < scroll.ScrollableHeight - 0.5; frame++)
                    {
                        scroll.ScrollToVerticalOffset(Math.Min(scroll.ScrollableHeight,
                            scroll.VerticalOffset + scroll.ViewportHeight * 0.85));
                        PresentationHarness.Layout(view, size);
                        Save(view, stem + $"-scroll{frame}");
                    }
                }
                finally { owner.Content = null; }
            });
    }

    /// <summary>The profile editor markup with the real readings control on its page.</summary>
    internal static (UserControl View, ControllerReadingsControl Readings, Window Owner) Open(bool dark, bool live)
    {
        ProfileWorkspacePresentationTests.Theme(dark);
        UserControl view = ProfileWorkspacePresentationTests.Load("ProfileEditor");
        var owner = new Window { Content = view };
        view.DataContext = new ProfileWorkspacePresentationTests.EditorFixture();
        var tab = (TabItem)view.FindName("contReadingsTab");
        var readings = new ControllerReadingsControl();
        tab.Content = readings;
        tab.IsEnabled = true;
        readings.LsDeadX = readings.LsDeadY = 0.12;
        readings.RsDeadX = readings.RsDeadY = 0.08;
        readings.L2Dead = 0.1;
        readings.R2Dead = 0.05;
        readings.SixAxisXDead = readings.SixAxisZDead = 0.15;
        ProfilePropertyPageFixture.ShowSection(view, 2);
        if (live) ShowFakeState(readings);
        return (view, readings, owner);
    }

    internal static void ShowFakeState(ControllerReadingsControl readings)
    {
        var input = new DS4State { LX = 182, LY = 96, RX = 120, RY = 140, L2 = 140, R2 = 255, Battery = 80 };
        input.Motion = new SixAxis(0, 0, 0, 0, 0, 0, 0.0)
        {
            gyroYawFull = 2400, gyroPitchFull = -1300, gyroRollFull = 600,
            accelX = 34, accelY = -12, accelZ = -58,
        };
        input.TrackPadTouch0.X = 1120;
        input.TrackPadTouch0.Y = 410;
        var output = new DS4State { LX = 190, LY = 88, RX = 128, RY = 128, L2 = 128, R2 = 255 };
        output.Motion = new SixAxis(0, 0, 0, 0, 0, 0, 0.0) { outputAccelX = 22, outputAccelZ = -48 };
        readings.UseDevice(0, 0);
        readings.SetPreviewState(input, output);
        readings.ShowReadings(4.02, 20, 0, ConnectionType.USB);
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
