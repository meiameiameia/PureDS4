using DS4WinWPF.DS4Forms;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Xml.Linq;

namespace DS4WindowsTests;

[TestClass]
[DoNotParallelize]
public class VisualConsistencyPresentationTests
{
    [TestMethod]
    public void TrayMenusRefreshAtOpenWithoutChangingCommandRouting()
    {
        WpfTestHost.Run(() =>
        {
            PresentationHarness.Theme(false);
            var menu = new ContextMenu();
            var choice = new MenuItem { Header = "Default profile", IsCheckable = true, IsChecked = true };
            var unavailable = new MenuItem { Header = "Unavailable controller action", IsEnabled = false };
            var nested = new MenuItem { Header = "Profiles" };
            nested.Items.Add(new MenuItem { Header = "A longer profile name for another game" });
            var separator = new Separator();
            menu.Items.Add(choice); menu.Items.Add(nested); menu.Items.Add(separator); menu.Items.Add(unavailable);
            int clicks = 0;
            choice.Click += (_, _) => clicks++;
            foreach (bool dark in new[] { false, true, false })
            {
                PresentationHarness.Theme(dark);
                // Exercise the tray's actual presentation-only Opened hook;
                // opening the popup itself would require a native HWND.
                DS4WinWPF.DS4Forms.ViewModels.TrayIconViewModel.ApplyContextMenuPresentation(menu);
                PresentationHarness.Layout(menu, new Size(360, 160));
                var chrome = (Border)menu.Template.FindName("MenuChrome", menu);
                AssertBrush(chrome.Background, "SurfaceRaisedBrush");
                Assert.AreEqual(new Thickness(1), chrome.BorderThickness);
                Assert.AreEqual(new CornerRadius(4), chrome.CornerRadius);
                Assert.AreEqual(Visibility.Visible, ((TextBlock)choice.Template.FindName("CheckGlyph", choice)).Visibility);
                Assert.AreEqual(Visibility.Visible, ((System.Windows.Shapes.Path)nested.Template.FindName("SubmenuArrow", nested)).Visibility);
                AssertBrush(unavailable.Foreground, "TextDisabledBrush");
                var popup = (Popup)nested.Template.FindName("PART_Popup", nested);
                Assert.IsFalse(popup.IsOpen, "Offscreen tests must not open a native popup.");
                Assert.AreEqual("IsSubmenuOpen", BindingOperations.GetBinding(popup, Popup.IsOpenProperty).Path.Path);
                Assert.AreEqual(PlacementMode.Right, popup.Placement);
                AssertBrush(((Border)popup.Child).Background, "SurfaceRaisedBrush");
                Assert.IsNotNull(separator.Template);
                Assert.AreEqual(KeyboardNavigationMode.Cycle, KeyboardNavigation.GetDirectionalNavigation(
                    PresentationHarness.Descendants<ItemsPresenter>(menu).Single()));
                PresentationHarness.Preview(menu, "shared-tray-menu", dark);
            }
            choice.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Assert.AreEqual(1, clicks, "The shared template must not replace the product's menu command routing.");
            Assert.IsFalse(menu.IsOpen);
        });
    }

    [TestMethod]
    public void TooltipsAndProgressKeepReadableSharedSurfaces()
    {
        WpfTestHost.Run(() =>
        {
            foreach (bool dark in new[] { false, true })
            {
                PresentationHarness.Theme(dark);
                var tooltip = new ToolTip { Content = "A longer explanation for a controller setting that wraps within the shared tooltip surface." };
                PresentationHarness.Layout(tooltip, new Size(360, 80));
                AssertBrush(((Border)tooltip.Template.FindName("ToolTipChrome", tooltip)).Background, "SurfaceRaisedBrush");
                Assert.IsFalse(tooltip.IsOpen);
                PresentationHarness.Preview(tooltip, "shared-tooltip", dark);
                var progress = new ProgressBar { Minimum = 0, Maximum = 100, Value = 37 };
                var host = new Window { Content = progress };
                PresentationHarness.Layout(progress, new Size(300, 8));
                AssertBrush(progress.Foreground, "AccentBrush");
                Assert.IsNotNull(progress.Template.FindName("PART_Indicator", progress));
                Assert.AreEqual(37d, progress.Value);
                Assert.AreEqual(IntPtr.Zero, new WindowInteropHelper(host).Handle);
                host.Content = null; host.Close();
            }
        });
    }

    [TestMethod]
    public void ExistingInternalMenuMarkupUsesSharedNestedAndCheckedTemplates()
    {
        WpfTestHost.Run(() =>
        {
            foreach (bool dark in new[] { false, true })
            {
                PresentationHarness.Theme(dark);
                foreach (string screen in new[] { "AutoProfiles", "ProfileEditor", "RecordBox" })
                {
                    XElement source = PresentationHarness.Source(screen);
                    int index = 0;
                    foreach (XElement markup in source.Descendants(PresentationHarness.Wpf + "ContextMenu"))
                    {
                        var wrapper = new XElement(PresentationHarness.Wpf + "UserControl",
                            source.Attributes().Where(a => a.IsNamespaceDeclaration || a.Name.LocalName == "Ignorable"),
                            new XElement(PresentationHarness.Wpf + "UserControl.ContextMenu", new XElement(markup)));
                        UserControl owner = PresentationHarness.Parse(wrapper);
                        ContextMenu menu = owner.ContextMenu;
                        // The gyro menus are populated dynamically when editing;
                        // representative entries exercise their same implicit style.
                        if (menu.Items.Count == 0) menu.Items.Add(new MenuItem { Header = "Cross", IsCheckable = true, IsChecked = true });
                        PresentationHarness.Layout(menu, new Size(400, 430));
                        AssertBrush(((Border)menu.Template.FindName("MenuChrome", menu)).Background, "SurfaceRaisedBrush");
                        foreach (MenuItem item in menu.Items.OfType<MenuItem>())
                        {
                            item.ApplyTemplate();
                            Assert.IsNotNull(item.Template.FindName("MenuItemChrome", item), screen + ": " + item.Header);
                            Assert.IsFalse(((Popup)item.Template.FindName("PART_Popup", item)).IsOpen);
                        }
                        PresentationHarness.Preview(menu, "internal-menu-" + screen + "-" + index++, dark);
                    }
                }
            }
        });
    }

    [TestMethod]
    public void InstallerPagesUseSharedControlsWithoutStartingTheInstaller()
    {
        WpfTestHost.Run(() =>
        {
            foreach (bool dark in new[] { false, true })
            foreach (Size size in new[] { new Size(668, 510), new Size(728, 570) })
            foreach (string page in new[] { "DetectingPage", "ConfirmationPage", "ProgressPage", "CompletePage", "RestartPage", "FailurePage" })
            {
                PresentationHarness.Theme(dark);
                XElement source = XElement.Load(Path.Combine(RepositoryRoot(), "installer", "PureDS4.Bootstrapper", "InstallerWindow.xaml"));
                source.Name = PresentationHarness.Wpf + "UserControl";
                source.Attribute(PresentationHarness.X + "Class")?.Remove();
                source.Element(PresentationHarness.Wpf + "Window.Resources").Name = PresentationHarness.Wpf + "UserControl.Resources";
                foreach (XAttribute attribute in source.Descendants().Attributes())
                {
                    if (attribute.Value.StartsWith("/PureDS4.Bootstrapper;component/"))
                        attribute.Value = attribute.Value.Replace("/PureDS4.Bootstrapper;component/", "/PureDS4;component/")
                            .Replace("PaletteLight.xaml", dark ? "PaletteDark.xaml" : "PaletteLight.xaml");
                    if (attribute.Name.LocalName == "Source" && attribute.Value == "AppIcon.png")
                        attribute.Value = "/PureDS4;component/Resources/AppIcon.png";
                }
                UserControl view = PresentationHarness.Parse(source);
                foreach (string name in new[] { "DetectingPage", "ConfirmationPage", "ProgressPage", "CompletePage", "RestartPage", "FailurePage" })
                    ((Grid)view.FindName(name)).Visibility = name == page ? Visibility.Visible : Visibility.Collapsed;
                ((TextBlock)view.FindName("ModeDescription")).Text = "An existing PureDS4 installation was found. Profiles and settings will be preserved.";
                ((TextBlock)view.FindName("ModeTitle")).Text = "Update PureDS4";
                foreach (string name in new[] { "ViiperStatus", "UsbipStatus", "HidHideStatus" })
                    ((TextBlock)view.FindName(name)).Text = "Ready";
                ((TextBlock)view.FindName("FailureMessage")).Text = string.Join(" ", Enumerable.Repeat("Disposable long error details explaining why setup could not complete safely.", 40));
                PresentationHarness.Layout(view, size);
                foreach (Button button in PresentationHarness.Descendants<Button>(view).Where(PresentationHarness.Participates))
                    PresentationHarness.AssertReachable(view, button, page);
                foreach (ScrollViewer scroll in PresentationHarness.Descendants<ScrollViewer>(view).Where(PresentationHarness.Participates))
                    Assert.AreEqual(0d, scroll.ScrollableWidth, 1, page);
                if (page == "ConfirmationPage")
                {
                    Assert.IsFalse(((CheckBox)view.FindName("MicrosoftTermsCheckBox")).IsChecked.Value);
                    Assert.IsFalse(((Button)view.FindName("ActionButton")).IsEnabled);
                    PresentationHarness.AssertReachable(view, (CheckBox)view.FindName("MicrosoftTermsCheckBox"), page);
                }
                if (page == "FailurePage")
                    PresentationHarness.AssertReachable(view, (TextBlock)view.FindName("FailureMessage"), page);
                if (page == "CompletePage")
                    Assert.AreEqual(((SolidColorBrush)view.FindResource("StateSuccessBrush")).Color,
                        ((SolidColorBrush)((TextBlock)view.FindName("CompleteStatusGlyph")).Foreground).Color);
                PresentationHarness.Preview(view, "installer-" + page + "-" + (int)size.Width, dark);
            }
        });
    }

    [TestMethod]
    public void OfflineLicenseReaderKeepsCompleteTextZoomAndCloseReachable()
    {
        WpfTestHost.Run(() =>
        {
            foreach (bool dark in new[] { false, true })
            foreach (string name in new[] { "DotNet-LICENSE.txt", "WindowsSdk-LICENSE.rtf", "COPYING" })
            {
                PresentationHarness.Theme(dark);
                using var source = PureDS4.Bootstrapper.InstallerTerms.OpenDocument(name);
                var document = new System.Windows.Documents.FlowDocument();
                if (name.EndsWith(".rtf"))
                    new System.Windows.Documents.TextRange(document.ContentStart, document.ContentEnd).Load(source, DataFormats.Rtf);
                else
                {
                    using var reader = new StreamReader(source);
                    document.Blocks.Add(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(reader.ReadToEnd())));
                }
                string text = new System.Windows.Documents.TextRange(document.ContentStart, document.ContentEnd).Text;
                var resources = new ResourceDictionary();
                foreach (string resource in new[] { dark ? "PaletteDark" : "PaletteLight", "PresentationControls" })
                    resources.MergedDictionaries.Add(new ResourceDictionary
                    {
                        Source = new Uri("/PureDS4;component/DS4Forms/Themes/" + resource + ".xaml", UriKind.Relative)
                    });
                Window window = PureDS4.Bootstrapper.InstallerTermsView.Create(document, name, resources);
                try
                {
                    var content = (FrameworkElement)window.Content;
                    foreach (Size size in new[] { new Size(448, 292), new Size(688, 512) })
                    {
                        PresentationHarness.Layout(content, size);
                        var close = PresentationHarness.Descendants<Button>(content).Single(b => Equals(b.Content, "Close"));
                        PresentationHarness.AssertReachable(content, close, "terms");
                        Assert.IsTrue(close.IsCancel);
                        var viewer = PresentationHarness.Descendants<FlowDocumentScrollViewer>(content).Single();
                        Assert.IsTrue(viewer.IsToolBarVisible, "Retain the existing license zoom controls.");
                        AssertBrush(viewer.Background, "SurfaceBaseBrush");
                        Assert.AreEqual(text, new System.Windows.Documents.TextRange(document.ContentStart, document.ContentEnd).Text);
                    }
                    Assert.AreEqual(IntPtr.Zero, new WindowInteropHelper(window).Handle);
                    PresentationHarness.Preview(content, "installer-terms-" + name, dark);
                }
                finally { window.Close(); }
            }
        });
    }

    [TestMethod]
    public void MigratedConfirmationDefaultsAreExplicitAndDoNotCreateAWindowHandle()
    {
        WpfTestHost.Run(() =>
        {
            PresentationHarness.Theme(true);
            foreach (MessageBoxResult result in new[] { MessageBoxResult.Yes, MessageBoxResult.No })
            {
                var dialog = (Window)typeof(AppDialog).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null,
                    new[] { typeof(string), typeof(string), typeof(MessageBoxButton), typeof(MessageBoxImage), typeof(MessageBoxResult) }, null)
                    .Invoke(new object[] { "Disposable confirmation", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question, result });
                try
                {
                    Assert.AreEqual(IntPtr.Zero, new WindowInteropHelper(dialog).Handle);
                    var choices = ((Panel)dialog.FindName("buttonPanel")).Children.Cast<Button>().ToArray();
                    Assert.AreEqual(result.ToString(), choices.Single(b => b.IsDefault).Content);
                    Assert.AreEqual(2, choices.Length);
                }
                finally { dialog.Close(); }
            }
        });
    }

    private static void AssertBrush(Brush actual, string key) =>
        Assert.AreEqual(((SolidColorBrush)Application.Current.FindResource(key)).Color, ((SolidColorBrush)actual).Color, key);

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "PureDS4.sln"))) directory = directory.Parent;
        Assert.IsNotNull(directory);
        return directory.FullName;
    }
}
