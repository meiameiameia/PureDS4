using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace DS4WinWPF.DS4Forms;

/// <summary>
/// Colors the system-owned caption, without replacing its buttons, hit testing,
/// resize border or Snap behavior. Unsupported systems retain native defaults.
/// </summary>
public static class NativeWindowFrame
{
    internal const uint SystemDefaultColor = 0xFFFFFFFF;
    internal const int ImmersiveDarkModeAttribute = 20;
    internal const int CaptionColorAttribute = 35;
    internal const int TextColorAttribute = 36;

    private static readonly ConditionalWeakTable<Window, FrameSession> Sessions = new();
    private static readonly DependencyProperty WasClosedProperty =
        DependencyProperty.RegisterAttached("WasClosed", typeof(bool), typeof(NativeWindowFrame), new PropertyMetadata(false));
    private static readonly DependencyProperty LastRequestProperty =
        DependencyProperty.RegisterAttached("LastRequest", typeof(FrameRequest?), typeof(NativeWindowFrame), new PropertyMetadata(null));

    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached("IsEnabled", typeof(bool), typeof(NativeWindowFrame),
            new PropertyMetadata(false, EnabledChanged));

    public static readonly DependencyProperty CaptionBackgroundProperty =
        DependencyProperty.RegisterAttached("CaptionBackground", typeof(Brush), typeof(NativeWindowFrame),
            new PropertyMetadata(null, PaletteChanged));

    public static readonly DependencyProperty CaptionForegroundProperty =
        DependencyProperty.RegisterAttached("CaptionForeground", typeof(Brush), typeof(NativeWindowFrame),
            new PropertyMetadata(null, PaletteChanged));

    public static void SetIsEnabled(DependencyObject target, bool value) => target.SetValue(IsEnabledProperty, value);
    public static bool GetIsEnabled(DependencyObject target) => (bool)target.GetValue(IsEnabledProperty);
    public static void SetCaptionBackground(DependencyObject target, Brush value) => target.SetValue(CaptionBackgroundProperty, value);
    public static Brush GetCaptionBackground(DependencyObject target) => (Brush)target.GetValue(CaptionBackgroundProperty);
    public static void SetCaptionForeground(DependencyObject target, Brush value) => target.SetValue(CaptionForegroundProperty, value);
    public static Brush GetCaptionForeground(DependencyObject target) => (Brush)target.GetValue(CaptionForegroundProperty);

    private static void EnabledChanged(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        if (target is not Window window) return;
        if (Sessions.TryGetValue(window, out FrameSession previous))
        {
            previous.Detach(restoreDefaults: true);
            Sessions.Remove(window);
        }
        if ((bool)args.NewValue && !(bool)window.GetValue(WasClosedProperty))
        {
            var session = new FrameSession(window);
            Sessions.Add(window, session);
            session.Attach();
        }
    }

    private static void PaletteChanged(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        if (target is Window window && Sessions.TryGetValue(window, out FrameSession session))
            session.Refresh();
    }

    internal static bool IsAttached(Window window) => Sessions.TryGetValue(window, out _);
    internal static FrameRequest? LastRequest(Window window) => (FrameRequest?)window.GetValue(LastRequestProperty);

    // Only an already-created, caller-owned window. Cosmetic command state
    // never replaces the operation's OnClosing guard or creates an HWND.
    internal static bool SetCloseCommandEnabled(Window window, bool enabled)
    {
        IntPtr handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero) return false;
        IntPtr menu = GetSystemMenu(handle, false);
        if (menu == IntPtr.Zero) return false;
        // SC_CLOSE, MF_BYCOMMAND + MF_ENABLED/MF_GRAYED. Redraw as required:
        // https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-enablemenuitem
        return EnableMenuItem(menu, 0xF060, enabled ? 0u : 1u) != -1 && DrawMenuBar(handle);
    }

    internal static FramePalette ResolvePalette(Brush background, Brush foreground, bool highContrast)
    {
        if (highContrast) return new(SystemDefaultColor, SystemDefaultColor, false);
        uint caption = ColorRef(background);
        uint text = ColorRef(foreground);
        bool dark = background is SolidColorBrush solid && caption != SystemDefaultColor &&
            (0.2126 * solid.Color.R + 0.7152 * solid.Color.G + 0.0722 * solid.Color.B) < 128;
        return new(caption, text, dark);
    }

    private static uint ColorRef(Brush brush) => brush is SolidColorBrush { Opacity: 1 } solid && solid.Color.A == 255
        ? (uint)(solid.Color.R | solid.Color.G << 8 | solid.Color.B << 16)
        : SystemDefaultColor;

    internal readonly record struct FramePalette(uint CaptionColor, uint TextColor, bool Dark);
    internal readonly record struct FrameRequest(FramePalette Palette, bool Succeeded);

    private sealed class FrameSession(Window window)
    {
        private bool listeningForSystemChanges;
        private bool detached;

        public void Attach()
        {
            window.SourceInitialized += SourceInitialized;
            window.Closed += Closed;
            Refresh();
        }

        private void SourceInitialized(object sender, EventArgs args) => Refresh();

        public void Refresh()
        {
            if (detached || !OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)) return;
            // Do not create a handle: styles may be applied to an unshown dialog.
            IntPtr handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero) return;
            if (!listeningForSystemChanges)
            {
                SystemParameters.StaticPropertyChanged += SystemChanged;
                listeningForSystemChanges = true;
            }
            Apply(window, handle, ResolvePalette(GetCaptionBackground(window),
                GetCaptionForeground(window), SystemParameters.HighContrast));
        }

        private void SystemChanged(object sender, PropertyChangedEventArgs args)
        {
            if (!string.IsNullOrEmpty(args.PropertyName) && args.PropertyName != nameof(SystemParameters.HighContrast)) return;
            if (window.Dispatcher.CheckAccess()) Refresh();
            else if (!window.Dispatcher.HasShutdownStarted) window.Dispatcher.BeginInvoke((Action)Refresh);
        }

        private void Closed(object sender, EventArgs args)
        {
            window.SetValue(WasClosedProperty, true);
            Detach(restoreDefaults: false);
            Sessions.Remove(window);
        }

        public void Detach(bool restoreDefaults)
        {
            detached = true;
            window.SourceInitialized -= SourceInitialized;
            window.Closed -= Closed;
            if (listeningForSystemChanges)
            {
                SystemParameters.StaticPropertyChanged -= SystemChanged;
                listeningForSystemChanges = false;
            }
            if (restoreDefaults && OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
            {
                IntPtr handle = new WindowInteropHelper(window).Handle;
                if (handle != IntPtr.Zero) Apply(window, handle, new(SystemDefaultColor, SystemDefaultColor, false));
            }
        }
    }

    private static void Apply(Window window, IntPtr handle, FramePalette palette)
    {
        // These documented attributes are supported on Windows 11 (build 22000+).
        // A failed cosmetic request leaves the OS frame usable; it is not a
        // controller/setup error. Keep the native activation/resize border.
        int dark = palette.Dark ? 1 : 0;
        uint caption = palette.CaptionColor;
        uint text = palette.TextColor;
        int darkResult = DwmSetWindowAttribute(handle, ImmersiveDarkModeAttribute, ref dark, sizeof(int));
        int captionResult = DwmSetWindowAttribute(handle, CaptionColorAttribute, ref caption, sizeof(uint));
        int textResult = DwmSetWindowAttribute(handle, TextColorAttribute, ref text, sizeof(uint));
        window.SetValue(LastRequestProperty, new FrameRequest(palette,
            darkResult == 0 && captionResult == 0 && textResult == 0));
    }

    [DllImport("dwmapi.dll", EntryPoint = "DwmSetWindowAttribute")]
    private static extern int DwmSetWindowAttribute(IntPtr handle, int attribute, ref int value, int size);

    [DllImport("dwmapi.dll", EntryPoint = "DwmSetWindowAttribute")]
    private static extern int DwmSetWindowAttribute(IntPtr handle, int attribute, ref uint value, int size);

    [DllImport("user32.dll")]
    private static extern IntPtr GetSystemMenu(IntPtr handle, [MarshalAs(UnmanagedType.Bool)] bool revert);

    [DllImport("user32.dll")]
    private static extern int EnableMenuItem(IntPtr menu, uint item, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DrawMenuBar(IntPtr handle);
}
