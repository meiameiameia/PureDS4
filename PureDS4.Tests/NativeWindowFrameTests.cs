using DS4WinWPF.DS4Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace DS4WindowsTests;

[TestClass]
[DoNotParallelize]
public class NativeWindowFrameTests
{
    [TestMethod]
    public void PaletteUsesColorRefAndDoesNotOverrideHighContrast()
    {
        var background = new SolidColorBrush(Color.FromRgb(0x12, 0x34, 0x56));
        var palette = NativeWindowFrame.ResolvePalette(background, Brushes.White, false);
        Assert.AreEqual(0x563412u, palette.CaptionColor, "DWM uses BGR COLORREF, not ARGB.");
        Assert.AreEqual(0xFFFFFFu, palette.TextColor);
        Assert.IsTrue(palette.Dark);
        Assert.IsFalse(NativeWindowFrame.ResolvePalette(Brushes.White, Brushes.Black, false).Dark);

        var accessible = NativeWindowFrame.ResolvePalette(background, Brushes.White, true);
        Assert.AreEqual(NativeWindowFrame.SystemDefaultColor, accessible.CaptionColor);
        Assert.AreEqual(NativeWindowFrame.SystemDefaultColor, accessible.TextColor);
        Assert.IsFalse(accessible.Dark);

        foreach (Brush unsupported in new Brush[]
        {
            null, Brushes.Transparent, new LinearGradientBrush(),
            new SolidColorBrush(Colors.Black) { Opacity = 0.5 },
        })
        {
            var fallback = NativeWindowFrame.ResolvePalette(unsupported, unsupported, false);
            Assert.AreEqual(NativeWindowFrame.SystemDefaultColor, fallback.CaptionColor);
            Assert.AreEqual(NativeWindowFrame.SystemDefaultColor, fallback.TextColor);
            Assert.IsFalse(fallback.Dark);
        }
    }

    [TestMethod]
    public void SharedStyleKeepsNativeChromeAndDoesNotCreateAWindowHandle()
    {
        WpfTestHost.Run(() =>
        {
            foreach (bool dark in new[] { false, true })
            {
                HomeWorkspaceTests.LoadTheme(dark);
                var window = new Window();
                window.SetResourceReference(FrameworkElement.StyleProperty, "WindowStyle");
                try
                {
                    Assert.IsTrue(NativeWindowFrame.GetIsEnabled(window));
                    Assert.IsTrue(NativeWindowFrame.IsAttached(window));
                    Assert.AreEqual(WindowStyle.SingleBorderWindow, window.WindowStyle);
                    Assert.AreEqual(ResizeMode.CanResize, window.ResizeMode);
                    Assert.IsFalse(window.AllowsTransparency);
                    Assert.AreEqual(IntPtr.Zero, new WindowInteropHelper(window).Handle,
                        "Applying colors must not activate or show a dialog.");
                    Assert.AreSame(Application.Current.FindResource("SurfaceBaseBrush"),
                        NativeWindowFrame.GetCaptionBackground(window));
                    Assert.AreSame(Application.Current.FindResource("TextPrimaryBrush"),
                        NativeWindowFrame.GetCaptionForeground(window));
                }
                finally { window.Close(); }
                Assert.IsFalse(NativeWindowFrame.IsAttached(window), "Closed windows release event subscriptions.");
                HomeWorkspaceTests.LoadTheme(!dark);
                _ = window.Style;
                Assert.IsFalse(NativeWindowFrame.IsAttached(window), "A later theme change must not reattach a closed window.");
            }
        });
    }

    [TestMethod]
    public void NativeCaptionTracksLiveThemeChangesAndRestoresDefaultsOnOptOut()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
            Assert.Inconclusive("Native caption color attributes require Windows 11; this host uses the native fallback.");

        // A hidden, plain WPF window owned by this test. Never product startup,
        // controller state, app data or changes to Windows personalization.
        WpfTestHost.Run(() =>
        {
            HomeWorkspaceTests.LoadTheme(false);
            var window = new Window();
            window.SetResourceReference(FrameworkElement.StyleProperty, "WindowStyle");
            try
            {
                IntPtr handle = new WindowInteropHelper(window).EnsureHandle();
                Assert.IsFalse(window.IsVisible);
                foreach (bool dark in new[] { false, true, false })
                {
                    HomeWorkspaceTests.LoadTheme(dark);
                    var expected = NativeWindowFrame.ResolvePalette(
                        (Brush)Application.Current.FindResource("SurfaceBaseBrush"),
                        (Brush)Application.Current.FindResource("TextPrimaryBrush"), SystemParameters.HighContrast);
                    AssertRequest(window, expected);
                    Assert.AreEqual(WindowStyle.SingleBorderWindow, window.WindowStyle);
                    Assert.IsFalse(window.IsVisible);
                }
                NativeWindowFrame.SetIsEnabled(window, false);
                Assert.IsFalse(NativeWindowFrame.IsAttached(window));
                AssertRequest(window, new(NativeWindowFrame.SystemDefaultColor, NativeWindowFrame.SystemDefaultColor, false));
                NativeWindowFrame.SetIsEnabled(window, true);
                Assert.IsTrue(NativeWindowFrame.IsAttached(window));
            }
            finally { window.Close(); }
            Assert.IsFalse(NativeWindowFrame.IsAttached(window));
        });
    }

    private static void AssertRequest(Window window, NativeWindowFrame.FramePalette expected)
    {
        // These attributes are documented setters; attempting to read them via
        // DwmGetWindowAttribute returns E_INVALIDARG on this Windows 11 host.
        // Validate the exact request and all three real setter HRESULTs. This
        // does not claim pixel/activation/Snap acceptance of a visible window.
        var request = NativeWindowFrame.LastRequest(window);
        Assert.IsTrue(request.HasValue, "Creating the native source must apply the caption palette.");
        Assert.AreEqual(expected, request.Value.Palette);
        Assert.IsTrue(request.Value.Succeeded, "DWM must accept all three native caption attribute setters.");
    }

    [TestMethod]
    public void ProgressCloseCommandChangesOnlyAnExistingTestOwnedWindow()
    {
        WpfTestHost.Run(() =>
        {
            HomeWorkspaceTests.LoadTheme(true);
            var window = new Window { ResizeMode = ResizeMode.NoResize };
            window.SetResourceReference(FrameworkElement.StyleProperty, "WindowStyle");
            try
            {
                Assert.IsFalse(NativeWindowFrame.SetCloseCommandEnabled(window, false));
                Assert.AreEqual(IntPtr.Zero, new WindowInteropHelper(window).Handle);
                IntPtr handle = new WindowInteropHelper(window).EnsureHandle();
                Assert.IsFalse(window.IsVisible);
                IntPtr menu = GetSystemMenu(handle, false);
                foreach (bool enabled in new[] { false, true })
                {
                    Assert.IsTrue(NativeWindowFrame.SetCloseCommandEnabled(window, enabled));
                    uint state = GetMenuState(menu, 0xF060, 0);
                    Assert.AreNotEqual(uint.MaxValue, state);
                    Assert.AreEqual(enabled, (state & 3) == 0);
                    Assert.IsFalse(window.IsVisible);
                }
            }
            finally { window.Close(); }
        });
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetSystemMenu(IntPtr handle, [MarshalAs(UnmanagedType.Bool)] bool revert);

    [DllImport("user32.dll")]
    private static extern uint GetMenuState(IntPtr menu, uint item, uint flags);
}
