using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace DS4WindowsTests;

/// <summary>Real dialog markup, with disposable state and no product window/view model.</summary>
[TestClass]
[DoNotParallelize]
public class ControllerDetectionPresentationTests
{
    [TestMethod]
    public void DetectionAndDeviceOptionsRemainReachableInBothThemesAndWindowSizes()
    {
        WpfTestHost.Run(() =>
        {
            foreach (bool dark in new[] { false, true })
            foreach (Size size in new[] { new Size(704, 360), new Size(784, 440) })
            {
                PresentationHarness.Theme(dark);
                UserControl view = CreateView();
                PresentationHarness.Layout(view, size);
                foreach (Control control in PresentationHarness.Descendants<Control>(view)
                    .Where(c => c.ActualWidth > 0 && PresentationHarness.Participates(c)))
                    PresentationHarness.AssertReachable(view, control, "controller detection");
                Assert.AreEqual(Visibility.Collapsed, ((FrameworkElement)view.FindName("experimentalDs3Panel")).Visibility);
                var list = (ListBox)view.FindName("controllerListBox");
                Assert.AreEqual(2, list.Items.Count);
                list.SelectedIndex = 1;
                list.GetBindingExpression(Selector.SelectedIndexProperty).UpdateSource();
                Assert.AreEqual(1, ((DetectionFixture)view.DataContext).ControllerSelectedIndex);
            }
        });
    }

    [TestMethod]
    public void DetectionSwitchesAndDeviceOptionUpdateOnlyTheirExistingBindings()
    {
        WpfTestHost.Run(() =>
        {
            PresentationHarness.Theme(true);
            UserControl view = CreateView();
            PresentationHarness.Layout(view, new Size(784, 440));
            var fixture = (DetectionFixture)view.DataContext;
            SetToggle(view, "ds4DetectionToggle", false);
            SetToggle(view, "moonlightDetectionToggle", true);
            SetToggle(view, "verboseDetectionToggle", true);
            Assert.IsFalse(fixture.DS4DeviceOpts.Enabled);
            Assert.IsTrue(fixture.UseMoonlight);
            Assert.IsTrue(fixture.VerboseLogMessages);
            SetToggle(view, "ds4DetectionToggle", true);
            Assert.IsTrue(fixture.DS4DeviceOpts.Enabled);
            var copycat = PresentationHarness.Descendants<CheckBox>(view)
                .Single(box => box.GetBindingExpression(ToggleButton.IsCheckedProperty)?.ParentBinding.Path.Path == "Options.IsCopyCat");
            copycat.SetCurrentValue(ToggleButton.IsCheckedProperty, true);
            copycat.GetBindingExpression(ToggleButton.IsCheckedProperty).UpdateSource();
            Assert.IsTrue(fixture.Device.Options.IsCopyCat);
        });
    }

    [TestMethod]
    public void ReachabilityCheckRejectsUnscrollableClippingButAllowsRealScrolling()
    {
        WpfTestHost.Run(() =>
        {
            var canvas = new Canvas();
            var clipped = new Button { Width = 80, Height = 28 };
            Canvas.SetTop(clipped, 500);
            canvas.Children.Add(clipped);
            PresentationHarness.Layout(canvas, new Size(704, 360));
            Assert.ThrowsException<AssertFailedException>(() => PresentationHarness.AssertReachable(canvas, clipped, "negative fixture"));
            var content = new StackPanel();
            for (int i = 0; i < 20; i++) content.Children.Add(new Button { Height = 28 });
            var viewer = new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var root = new Grid(); root.Children.Add(viewer);
            PresentationHarness.Layout(root, new Size(704, 360));
            Assert.IsTrue(viewer.ScrollableHeight > 0);
            PresentationHarness.AssertReachable(root, (Button)content.Children[^1], "scrollable fixture");
            var incompleteContent = new Canvas { Height = 600 };
            var beyondExtent = new Button { Width = 80, Height = 28 };
            Canvas.SetTop(beyondExtent, 1000);
            incompleteContent.Children.Add(beyondExtent);
            viewer.Content = incompleteContent;
            PresentationHarness.Layout(root, new Size(704, 360));
            Assert.IsTrue(viewer.ScrollableHeight > 0);
            Assert.ThrowsException<AssertFailedException>(() =>
                PresentationHarness.AssertReachable(root, beyondExtent, "outside scrolling extent"));
        });
    }

    internal static UserControl CreateView()
    {
        UserControl view = PresentationHarness.LoadControl("ControllerRegisterOptionsWindow");
        var fixture = new DetectionFixture();
        view.DataContext = fixture;
        ((TabItem)view.FindName("ds4OptsTabItem")).DataContext = fixture.Device;
        return view;
    }

    private static void SetToggle(UserControl view, string name, bool enabled)
    {
        var toggle = (CheckBox)view.FindName(name);
        Assert.IsFalse(string.IsNullOrWhiteSpace(System.Windows.Automation.AutomationProperties.GetName(toggle)));
        toggle.SetCurrentValue(ToggleButton.IsCheckedProperty, enabled);
        toggle.GetBindingExpression(ToggleButton.IsCheckedProperty).UpdateSource();
    }

    public sealed class DetectionFixture
    {
        public DetectionOptions DS4DeviceOpts { get; } = new() { Enabled = true };
        public DetectionOptions DS3DeviceOpts { get; } = new();
        public bool ExperimentalDS3Build => false;
        public bool UseMoonlight { get; set; }
        public bool VerboseLogMessages { get; set; }
        public int ControllerSelectedIndex { get; set; } = 0;
        public int CurrentTabSelectedIndex { get; set; } = 1;
        public object[] CurrentInputDevices { get; } = new object[]
        {
            new { IdText = "DS4 v.2 · Bluetooth\nPreview controller" },
            new { IdText = "DS4 v.1 · USB\nA long controller name for layout verification" },
        };
        public DeviceFixture Device { get; } = new();
    }
    public sealed class DetectionOptions { public bool Enabled { get; set; } }
    public sealed class DeviceFixture
    {
        public bool Visible => true;
        public DeviceOptions Options { get; } = new();
    }
    public sealed class DeviceOptions { public bool IsCopyCat { get; set; } }
}
