using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DS4WinWPF.DS4Forms;

namespace DS4WindowsTests;

/// <summary>
/// The Controller Readings page: one calm empty state until a report arrives, and a
/// layout that fits the editor horizontally at the compact and requested sizes, in
/// both themes. Uses the real control with a fake state; its timer never starts.
/// </summary>
[TestClass]
[DoNotParallelize]
public class ControllerReadingsLayoutTests
{
    [TestMethod]
    public void EmptyStateShowsUntilAReportArrivesAndAfterTheControllerLeaves()
    {
        WpfTestHost.Run(() =>
        {
            (UserControl view, ControllerReadingsControl readings, Window owner) = ControllerReadingsPreviewTests.Open(false, false);
            try
            {
                var empty = (FrameworkElement)readings.FindName("readingsEmptyState");
                var content = (FrameworkElement)readings.FindName("readingsContent");
                Assert.AreEqual(Visibility.Visible, empty.Visibility);
                Assert.AreEqual(Visibility.Collapsed, content.Visibility);

                ControllerReadingsPreviewTests.ShowFakeState(readings);
                Assert.AreEqual(Visibility.Collapsed, empty.Visibility);
                Assert.AreEqual(Visibility.Visible, content.Visibility);
                Assert.AreEqual("Controller 1", ((Label)readings.FindName("inputContNum")).Content);
                Assert.AreEqual("USB", ((Label)readings.FindName("readingsTransportLb")).Content);

                readings.ShowNoController();
                Assert.AreEqual(Visibility.Visible, empty.Visibility);
                Assert.AreEqual(Visibility.Collapsed, content.Visibility);
            }
            finally { owner.Content = null; }
        });
    }

    [TestMethod]
    public void LiveReadingsFitWithVerticalScrollOnly()
    {
        foreach (bool dark in new[] { false, true })
        foreach (Size size in new[] { new Size(720, 480), new Size(1024, 660) })
            WpfTestHost.Run(() =>
            {
                (UserControl view, ControllerReadingsControl readings, Window owner) = ControllerReadingsPreviewTests.Open(dark, true);
                try
                {
                    for (int pass = 0; pass < 3; pass++) PresentationHarness.Layout(view, size);
                    var scroll = (ScrollViewer)readings.FindName("readingsScroll");
                    var content = (FrameworkElement)scroll.Content;
                    Assert.IsTrue(scroll.ExtentWidth <= scroll.ViewportWidth + 0.5,
                        $"{size} {(dark ? "dark" : "light")}: content {scroll.ExtentWidth:0} wider than {scroll.ViewportWidth:0}.");
                    foreach (FrameworkElement element in PresentationHarness.Descendants<FrameworkElement>(content)
                                 .Where(e => e.ActualWidth > 0 && PresentationHarness.Participates(e) && e is TextBlock or Label or Slider or Canvas))
                    {
                        Rect bounds = element.TransformToAncestor(content).TransformBounds(new Rect(element.RenderSize));
                        Assert.IsTrue(bounds.Left >= -0.5 && bounds.Right <= content.ActualWidth + 0.5,
                            $"{size} {(dark ? "dark" : "light")}: {element.GetType().Name} '{element.Name}' {bounds} outside {content.ActualWidth:0}.");
                    }
                }
                finally { owner.Content = null; }
            });
    }
}
