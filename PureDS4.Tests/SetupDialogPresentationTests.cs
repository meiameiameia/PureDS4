using DS4WinWPF.DS4Forms;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;

namespace DS4WindowsTests;

[TestClass]
[DoNotParallelize]
public class SetupDialogPresentationTests
{
    [TestMethod]
    public void PromptStatesKeepActionsFixedAndDetailsReachable()
    {
        WpfTestHost.Run(() =>
        {
            foreach (bool dark in new[] { false, true })
            foreach (Size size in new[] { new Size(504, 312), new Size(604, 492) })
            foreach (string state in new[] { "setup-ready", "setup-required", "setup-repair", "setup-conflict", "setup-existing" })
            {
                PresentationHarness.Theme(dark);
                UserControl view = CreateView(state);
                var scroll = (ScrollViewer)view.FindName("promptScroll");
                var details = (Expander)view.FindName("technicalDetailsExpander");
                foreach (bool expanded in new[] { false, true, false })
                {
                    details.IsExpanded = expanded;
                    PresentationHarness.Layout(view, size);
                    var toggle = (ToggleButton)details.Template.FindName("HeaderSite", details);
                    Assert.AreEqual(expanded, toggle.IsChecked);
                    toggle.SetCurrentValue(ToggleButton.IsCheckedProperty, !expanded);
                    Assert.AreEqual(!expanded, details.IsExpanded, "Disclosure must work through its native keyboard-operable toggle.");
                    details.IsExpanded = expanded;
                    PresentationHarness.Layout(view, size);
                    Rect actions = Bounds(view, (Border)view.FindName("promptActions"));
                    Inside(view, (Button)view.FindName("notNowButton"));
                    Inside(view, (Button)view.FindName("installButton"));
                    var portable = (Button)view.FindName("installPortableButton");
                    if (portable.Visibility == Visibility.Visible) Inside(view, portable);
                    Assert.AreEqual(0d, scroll.ScrollableWidth, 1);
                    foreach (string name in new[] { "headingText", "summaryText", "requirementsText", "portableWarningPanel" })
                    {
                        var element = (FrameworkElement)view.FindName(name);
                        if (PresentationHarness.Participates(element)) PresentationHarness.AssertReachable(view, element, state);
                    }
                    if (expanded) PresentationHarness.AssertReachable(view, (TextBlock)view.FindName("statusText"), state);
                    scroll.ScrollToEnd(); PresentationHarness.Layout(view, size);
                    Assert.AreEqual(actions, Bounds(view, (Border)view.FindName("promptActions")));
                    scroll.ScrollToTop();
                }
                var suppress = (CheckBox)view.FindName("suppressPromptCheck");
                Assert.AreEqual(state == "setup-ready" ? Visibility.Visible : Visibility.Collapsed, suppress.Visibility);
                if (suppress.Visibility == Visibility.Visible)
                {
                    suppress.IsChecked = true;
                    Assert.AreEqual(suppress.Content, System.Windows.Automation.AutomationProperties.GetName(suppress));
                }
                Assert.IsTrue(((Button)view.FindName("notNowButton")).IsCancel);
                Assert.IsFalse(((Button)view.FindName("installButton")).IsDefault,
                    "Styling must not make Enter implicitly request a repair.");
                details.IsEnabled = false;
                PresentationHarness.Layout(view, size);
                var disabledHeader = (ToggleButton)details.Template.FindName("HeaderSite", details);
                Assert.IsFalse(disabledHeader.IsEnabled);
                Assert.AreSame(view.FindResource("TextDisabledBrush"), disabledHeader.Foreground);
            }
        });
    }

    [TestMethod]
    public void ExposureAndLongMessagesRetainTheirFixedConfirmationActions()
    {
        WpfTestHost.Run(() =>
        {
            foreach (bool dark in new[] { false, true })
            foreach (string page in new[] { "exposure", "app-warning" })
            {
                PresentationHarness.Theme(dark);
                UserControl view = CreateView(page);
                Size size = new(404, 252);
                PresentationHarness.Layout(view, size);
                var scroll = (ScrollViewer)view.FindName(page == "exposure" ? "exposureScroll" : "messageScroll");
                var buttons = PresentationHarness.Descendants<Button>(view).ToArray();
                Assert.AreEqual(2, buttons.Length);
                foreach (Button button in buttons) Inside(view, button);
                Rect primary = Bounds(view, buttons.Last());
                Assert.AreEqual(0d, scroll.ScrollableWidth, 1);
                scroll.ScrollToEnd(); PresentationHarness.Layout(view, size);
                Assert.AreEqual(primary, Bounds(view, buttons.Last()));
                if (page == "exposure")
                {
                    Assert.IsTrue(buttons[0].IsCancel);
                    Assert.IsTrue(buttons[1].IsDefault);
                }
                else Inside(scroll, (TextBlock)view.FindName("messageText"), checkTop: false);
            }
        });
    }

    [TestMethod]
    public void AppDialogButtonSetsKeepExistingDefaultsWithoutShowingOrRunningAnything()
    {
        WpfTestHost.Run(() =>
        {
            PresentationHarness.Theme(true);
            // The application's four existing button sets, not the additional
            // native MessageBox variants introduced by newer WPF versions.
            foreach (MessageBoxButton buttons in new[] { MessageBoxButton.OK, MessageBoxButton.OKCancel,
                MessageBoxButton.YesNo, MessageBoxButton.YesNoCancel })
            {
                MessageBoxResult expected = buttons == MessageBoxButton.YesNo ? MessageBoxResult.No :
                    buttons == MessageBoxButton.OK ? MessageBoxResult.OK : MessageBoxResult.Cancel;
                Window dialog = CreateMessageDialog(buttons, expected);
                try
                {
                    Assert.AreEqual(IntPtr.Zero, new WindowInteropHelper(dialog).Handle);
                    var choices = ((Panel)dialog.FindName("buttonPanel")).Children.Cast<Button>().ToArray();
                    Assert.AreEqual(buttons == MessageBoxButton.YesNoCancel ? 3 : buttons == MessageBoxButton.OK ? 1 : 2, choices.Length);
                    Assert.AreEqual(expected.ToString(), choices.Single(b => b.IsDefault).Content);
                    if (expected != MessageBoxResult.OK) Assert.AreEqual(expected.ToString(), choices.Single(b => b.IsCancel).Content);
                }
                finally { dialog.Close(); }
            }
        });
    }

    [TestMethod]
    public void LongProgressStatusScrollsWithoutHidingTheOperationNotice()
    {
        WpfTestHost.Run(() =>
        {
            foreach (bool dark in new[] { false, true })
            {
                PresentationHarness.Theme(dark);
                UserControl view = CreateView("progress-attention");
                var phase = (TextBlock)view.FindName("phaseText");
                phase.Text = string.Join(" ", Enumerable.Repeat("Disposable long verification status.", 16));
                Size size = new(424, 244);
                PresentationHarness.Layout(view, size);
                var scroll = (ScrollViewer)view.FindName("progressScroll");
                var notice = (TextBlock)view.FindName("detailText");
                Inside(view, notice);
                Rect original = Bounds(view, notice);
                PresentationHarness.AssertReachable(view, phase, "progress status");
                Assert.AreEqual(0d, scroll.ScrollableWidth, 1);
                scroll.ScrollToEnd(); PresentationHarness.Layout(view, size);
                Assert.AreEqual(original, Bounds(view, notice));
            }
        });
    }

    [TestMethod]
    public void ProgressCompletionAndPromptDismissalStayLocalToTheirWindows()
    {
        WpfTestHost.Run(() =>
        {
            PresentationHarness.Theme(true);
            foreach (bool success in new[] { false, true })
            {
                // No real log or process: the constructor/Finish only observe
                // this proven-absent disposable path, and Finish stops its timer.
                string absentLog = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "PureDS4-ui-" + Guid.NewGuid() + ".log");
                Assert.IsFalse(System.IO.File.Exists(absentLog));
                var progress = new ViiperSetupProgress(absentLog);
                progress.Finish(success);
                Assert.AreEqual(IntPtr.Zero, new WindowInteropHelper(progress).Handle);
                Assert.IsFalse(((ProgressBar)progress.FindName("setupProgress")).IsIndeterminate);
                Assert.AreEqual(success ? 100d : 0d, ((ProgressBar)progress.FindName("setupProgress")).Value);
                Assert.AreSame(Application.Current.FindResource(success ? "StateSuccessBrush" : "StateErrorBrush"),
                    ((TextBlock)progress.FindName("statusGlyph")).Foreground);
            }
            var prompt = new ViiperSetupPrompt("Disposable setup status", null, mandatoryRepairRequired: true);
            prompt.Close();
            Assert.AreEqual(ViiperSetupPromptDecision.NotNow, prompt.Decision);
            Assert.IsFalse(prompt.ExitApplicationRequested);
        });
    }

    internal static UserControl CreateView(string page)
    {
        string name = page.StartsWith("setup") ? "ViiperSetupPrompt" : page.StartsWith("progress") ? "ViiperSetupProgress" :
            page == "exposure" ? "ControllerExposureDialog" : "AppDialog";
        UserControl view = PresentationHarness.LoadControl(name);
        if (page.StartsWith("setup"))
        {
            var state = ViiperSetupPrompt.CreatePresentation(page == "setup-conflict", page == "setup-repair", false, page != "setup-ready");
            ((TextBlock)view.FindName("headingText")).Text = state.Heading;
            ((TextBlock)view.FindName("summaryText")).Text = state.Summary;
            ((TextBlock)view.FindName("requirementsHeadingText")).Text = state.RequirementsHeading;
            ((TextBlock)view.FindName("requirementsText")).Text = state.Requirements;
            ((Button)view.FindName("installButton")).Content = state.PrimaryAction;
            ((Button)view.FindName("notNowButton")).Content = state.ContinueAction;
            ((Button)view.FindName("installPortableButton")).Content = state.PortableAction;
            ((Button)view.FindName("installPortableButton")).Visibility = state.ShowPortableAction ? Visibility.Visible : Visibility.Collapsed;
            ((UIElement)view.FindName("portableWarningPanel")).Visibility = state.ShowPortableAction ? Visibility.Visible : Visibility.Collapsed;
            ((CheckBox)view.FindName("suppressPromptCheck")).Visibility = state.ShowSuppressPrompt ? Visibility.Visible : Visibility.Collapsed;
            ((TextBlock)view.FindName("statusText")).Text = "VIIPER elevated launcher needs repair.\n" +
                "Disposable diagnostic text with a deliberately long component path: C:\\Example\\Game-output components\\A longer verification description\\service.exe";
            if (page == "setup-existing")
            {
                ((UIElement)view.FindName("existingViiperPanel")).Visibility = Visibility.Visible;
                ((TextBlock)view.FindName("existingViiperPathText")).Text = @"C:\Example\Legacy component\VIIPER.exe";
            }
        }
        else if (page.StartsWith("progress"))
        {
            bool ready = page == "progress-ready", failed = page == "progress-attention";
            ((TextBlock)view.FindName("headingText")).Text = ready ? "Game output is ready" : failed ? "Game output setup needs attention" : "Setting up game output";
            ((TextBlock)view.FindName("phaseText")).Text = ready ? "Setup completed successfully." : failed ? "Setup stopped safely before verification completed." : "Verifying the PureDS4 package...";
            ((TextBlock)view.FindName("statusGlyph")).Text = ready ? "✓" : failed ? "!" : "↻";
            ((TextBlock)view.FindName("statusGlyph")).SetResourceReference(TextBlock.ForegroundProperty, ready ? "StateSuccessBrush" : failed ? "StateErrorBrush" : "StateRecoveryBrush");
            ((TextBlock)view.FindName("detailText")).Text = ready ? "PureDS4 will continue automatically." : failed ? "A detailed error and the diagnostic log will appear next." : "Keep this window open. Setup will finish automatically.";
            ((ProgressBar)view.FindName("setupProgress")).IsIndeterminate = !ready && !failed;
            ((ProgressBar)view.FindName("setupProgress")).Value = ready ? 100 : 0;
        }
        else if (page == "app-warning")
        {
            ((TextBlock)view.FindName("headingText")).Text = "Game output needs attention";
            ((TextBlock)view.FindName("messageText")).Text = string.Join("\n\n", Enumerable.Repeat(
                "Disposable diagnostic detail: game output could not start. Your profiles remain available. Review the setup information before attempting a repair.", 12));
            ((TextBlock)view.FindName("stateGlyph")).Text = "⚠";
            ((TextBlock)view.FindName("stateGlyph")).SetResourceReference(TextBlock.ForegroundProperty, "StateWarningBrush");
            var panel = (Panel)view.FindName("buttonPanel");
            foreach (string label in new[] { "Cancel", "OK" })
                panel.Children.Add(new Button { Content = label, MinWidth = 86, Margin = new(8, 0, 0, 0),
                    IsCancel = label == "Cancel", IsDefault = label == "Cancel",
                    Style = (Style)view.FindResource(label == "OK" ? "FoundationPrimaryButtonStyle" : "FoundationButtonStyle") });
        }
        return view;
    }

    private static Window CreateMessageDialog(MessageBoxButton buttons, MessageBoxResult defaultResult) =>
        (Window)typeof(AppDialog).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null,
            new[] { typeof(string), typeof(string), typeof(MessageBoxButton), typeof(MessageBoxImage), typeof(MessageBoxResult) }, null)
            .Invoke(new object[] { "Disposable message", "PureDS4 UI check", buttons, MessageBoxImage.Warning, defaultResult });

    private static Rect Bounds(FrameworkElement root, FrameworkElement child) =>
        child.TransformToAncestor(root).TransformBounds(new Rect(child.RenderSize));

    private static void Inside(FrameworkElement root, FrameworkElement child, bool checkTop = true)
    {
        Rect bounds = Bounds(root, child);
        Assert.IsTrue(bounds.Left >= -1 && bounds.Right <= root.ActualWidth + 1 &&
            (!checkTop || bounds.Top >= -1) && bounds.Bottom <= root.ActualHeight + 1,
            child.Name + ": " + bounds + " outside " + root.RenderSize);
    }
}
