using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DS4WindowsTests;

[TestClass]
[DoNotParallelize]
public class ControlCaptionPresentationTests
{
    [TestMethod]
    public void ButtonCaptionsUseOneOpticalBaselineAcrossThemesHeightsAndStates()
    {
        WpfTestHost.Run(() =>
        {
            foreach (bool dark in new[] { false, true })
            {
                HomeWorkspaceTests.LoadTheme(dark);
                var panel = new StackPanel { Orientation = Orientation.Horizontal };
                panel.SetResourceReference(Panel.BackgroundProperty, "SurfaceBaseBrush");
                foreach (string style in new[] { "FoundationButtonStyle", "FoundationPrimaryButtonStyle" })
                foreach (int height in new[] { 28, 40 })
                foreach (bool enabled in new[] { true, false })
                {
                    var button = new Button
                    {
                        Content = height == 28 ? "Edit profile" : "_Save",
                        Style = (Style)Application.Current.FindResource(style),
                        Height = height,
                        Width = 100,
                        Margin = new Thickness(4),
                        IsEnabled = enabled,
                    };
                    panel.Children.Add(button);
                }
                Layout(panel, new Size(864, 56));
                foreach (Button button in panel.Children)
                {
                    var caption = Descendants<TextBlock>(button).Single();
                    Rect bounds = caption.TransformToAncestor(button).TransformBounds(new Rect(caption.RenderSize));
                    Assert.AreEqual(button.ActualHeight / 2 - 1, bounds.Top + bounds.Height / 2, 0.75,
                        "The text line remains centered with the shared one-DIP optical correction.");
                    Assert.IsTrue(bounds.Top >= 1 && bounds.Bottom <= button.ActualHeight - 1,
                        "Caption correction must not clip the label.");
                    Assert.AreEqual(((SolidColorBrush)button.Foreground).Color, ((SolidColorBrush)caption.Foreground).Color);
                    Assert.AreEqual(1d, caption.Opacity, "Disabled labels use the control's contrast-tested color.");
                }
                Preview(panel, dark);
            }
        });
    }

    [TestMethod]
    public void Ds4SilhouetteHasRecognizableCutoutsWithoutExternalArtwork()
    {
        WpfTestHost.Run(() =>
        {
            HomeWorkspaceTests.LoadTheme(true);
            var geometry = (Geometry)Application.Current.FindResource("DualShock4SilhouetteGeometry");
            Assert.IsTrue(geometry.FillContains(new Point(4, 25)), "Left grip.");
            Assert.IsTrue(geometry.FillContains(new Point(44, 25)), "Right grip.");
            foreach (Point cutout in new[] { new Point(24, 9), new Point(11, 11), new Point(17, 16), new Point(31, 16) })
                Assert.IsFalse(geometry.FillContains(cutout), "Touchpad, d-pad and symmetric sticks remain cutouts.");
        });
    }

    private static void Layout(FrameworkElement view, Size size)
    {
        view.Measure(size);
        view.Arrange(new Rect(size));
        view.UpdateLayout();
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T typed) yield return typed;
            foreach (T nested in Descendants<T>(child)) yield return nested;
        }
    }

    private static void Preview(FrameworkElement view, bool dark)
    {
        string directory = Environment.GetEnvironmentVariable("PUREDS4_VISUAL_PREVIEW_DIRECTORY");
        if (string.IsNullOrEmpty(directory)) return;
        Assert.IsTrue(Directory.Exists(directory));
        var bitmap = new RenderTargetBitmap((int)view.ActualWidth, (int)view.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(view);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(directory, $"pureds4-captions-{(dark ? "dark" : "light")}.png"));
        encoder.Save(stream);
    }
}
