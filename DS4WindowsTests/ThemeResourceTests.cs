using Microsoft.VisualStudio.TestTools.UnitTesting;
using DS4WinWPF.DS4Forms;
using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DS4WindowsTests
{
    [TestClass]
    [DoNotParallelize]
    public class ThemeResourceTests
    {
        [TestMethod]
        public void DefaultThemeLoadsBridgeShellStylesOnFreshConfiguration()
        {
            Exception failure = null;
            Thread thread = new Thread(() =>
            {
                try
                {
                    var application = new Application();
                    var defaultTheme = new ResourceDictionary();
                    application.Resources.MergedDictionaries.Add(defaultTheme);
                    defaultTheme.Source = new Uri(
                        "/PureDS4;component/DS4Forms/Themes/DefaultTheme.xaml",
                        UriKind.Relative);

                    var foundation = new ResourceDictionary();
                    application.Resources.MergedDictionaries.Add(foundation);
                    foundation.Source = new Uri(
                        "/PureDS4;component/DS4Forms/Themes/Foundation.xaml",
                        UriKind.Relative);

                    var bridgeStyles = new ResourceDictionary();
                    application.Resources.MergedDictionaries.Add(bridgeStyles);
                    bridgeStyles.Source = new Uri(
                        "/PureDS4;component/DS4Forms/Themes/BridgeShellStyles.xaml",
                        UriKind.Relative);

                    Assert.IsNotNull(application.TryFindResource(
                        "BridgePrimaryButtonStyle"));
                    Assert.IsNotNull(application.TryFindResource(
                        "BridgeSecondaryButtonStyle"));
                    Assert.IsNotNull(application.TryFindResource(
                        "BridgeProfileComboBoxStyle"));
                    Assert.IsNotNull(application.TryFindResource(
                        "BridgeDescribedCheckBoxStyle"));
                    AssertFoundationResources(application, "light");
                    Assert.IsNotNull(new StatusChip());
                    Assert.IsNotNull(new StatusBanner());
                    Assert.IsNotNull(new BusyOverlay());
                    Assert.IsNotNull(new ControllerExposureDialog());
                    Assert.IsNotNull(new About());
                    Assert.IsNotNull(new OutputSlotManagerControl());

                    // This is the same construction path used by MainWindow
                    // on a clean install. It must not depend on a converter
                    // that exists only in DarkTheme or in a parent window.
                    var overview = new ControllerOverviewControl();
                    Assert.IsNotNull(overview.Resources[
                        "InverseBoolConverter"]);

                    var repairPrompt = new ViiperSetupPrompt(
                        "usbip-win2 0.9.7.8 must be replaced with supported 0.9.7.7",
                        null, verifiedUpdateRequired: true,
                        usbipReplacementRequired: true,
                        mandatoryRepairRequired: true);
                    Assert.AreEqual("Game output needs repair",
                        ((TextBlock)repairPrompt.FindName(
                            "headingText")).Text);
                    Assert.AreEqual("Repair game output",
                        ((Button)repairPrompt.FindName(
                            "installButton")).Content);
                    Assert.AreEqual(Visibility.Collapsed,
                        ((CheckBox)repairPrompt.FindName(
                            "suppressPromptCheck")).Visibility);
                    Assert.IsFalse(repairPrompt.ExitApplicationRequested,
                        "The settings UI must remain available in degraded mode while virtual output fails closed.");

                    var missingPrompt = new ViiperSetupPrompt(
                        "VIIPER and usbip-win2 need setup", null,
                        mandatoryRepairRequired: true);
                    Assert.AreEqual("Set up game output",
                        ((TextBlock)missingPrompt.FindName(
                            "headingText")).Text);
                    Assert.AreEqual("Continue without game output",
                        ((Button)missingPrompt.FindName(
                            "notNowButton")).Content);
                    Assert.IsFalse(missingPrompt.ExitApplicationRequested,
                        "Missing prerequisites must leave repair and diagnostics accessible.");

                    // The repair progress surface is created before the
                    // elevated setup host runs. Its clean-install XAML must
                    // therefore resolve using only application theme assets.
                    var setupProgress = new ViiperSetupProgress(
                        System.IO.Path.Combine(
                            System.IO.Path.GetTempPath(),
                            "ds4windows-missing-setup-log.txt"));
                    Assert.AreEqual("Setting up game output",
                        ((TextBlock)setupProgress.FindName(
                            "headingText")).Text);
                    Assert.AreEqual("Preparing verified package...",
                        ((TextBlock)setupProgress.FindName(
                            "phaseText")).Text);

                    application.Resources.MergedDictionaries.Clear();
                    LoadDictionary(application,
                        "/PureDS4;component/DS4Forms/Themes/DarkTheme.xaml");
                    LoadDictionary(application,
                        "/PureDS4;component/DS4Forms/Themes/Foundation.xaml");
                    LoadDictionary(application,
                        "/PureDS4;component/DS4Forms/Themes/BridgeShellStyles.xaml");
                    AssertFoundationResources(application, "dark");
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(15)),
                "Theme resource loading did not finish.");
            if (failure != null)
            {
                Assert.Fail(failure.ToString());
            }
        }

        private static void LoadDictionary(Application application, string source)
        {
            var dictionary = new ResourceDictionary();
            application.Resources.MergedDictionaries.Add(dictionary);
            dictionary.Source = new Uri(source, UriKind.Relative);
        }

        private static void AssertFoundationResources(Application application,
            string themeName)
        {
            string[] styleKeys =
            {
                "FoundationFocusVisualStyle",
                "FoundationButtonStyle",
                "FoundationPrimaryButtonStyle",
                "FoundationCheckBoxStyle",
                "FoundationComboBoxStyle",
                "FoundationTextBoxStyle",
                "FoundationPropertyGroupStyle",
                "FoundationGroupBoxStyle",
                "FoundationTableListItemStyle",
                "FoundationToolbarButtonStyle",
            };

            foreach (string key in styleKeys)
            {
                Assert.IsNotNull(application.TryFindResource(key),
                    $"{themeName} theme is missing {key}.");
            }

            AssertContrast(application, themeName,
                "SurfaceBaseBrush", "SurfaceRaisedBrush", 1.25);
            AssertContrast(application, themeName,
                "SurfaceHoverBrush", "SurfaceRaisedBrush", 1.25);
            AssertContrast(application, themeName,
                "SurfaceSelectedBrush", "SurfaceRaisedBrush", 1.25);

            // Table and navigation rows paint SurfaceRaised and swap to hover or
            // selected on interaction, so row text has to stay legible on both of
            // those fills, not just on the resting one.
            AssertContrast(application, themeName,
                "TextPrimaryBrush", "SurfaceHoverBrush", 4.5);
            AssertContrast(application, themeName,
                "TextSecondaryBrush", "SurfaceHoverBrush", 4.5);
            AssertContrast(application, themeName,
                "TextPrimaryBrush", "SurfaceSelectedBrush", 4.5);
            AssertContrast(application, themeName,
                "TextSecondaryBrush", "SurfaceSelectedBrush", 4.5);
            AssertContrast(application, themeName,
                "BorderDefaultBrush", "SurfaceBaseBrush", 3.0);
            AssertContrast(application, themeName,
                "BorderDefaultBrush", "SurfaceRaisedBrush", 3.0);
            AssertContrast(application, themeName,
                "TextPrimaryBrush", "SurfaceRaisedBrush", 4.5);
            AssertContrast(application, themeName,
                "TextSecondaryBrush", "SurfaceRaisedBrush", 4.5);
            AssertContrast(application, themeName,
                "TextDisabledBrush", "SurfaceRaisedBrush", 3.0);
            AssertContrast(application, themeName,
                "AccentForegroundBrush", "AccentBrush", 4.5);
            AssertContrast(application, themeName,
                "AccentForegroundBrush", "AccentHoverBrush", 4.5);
            AssertContrast(application, themeName,
                "AccentForegroundBrush", "AccentPressedBrush", 4.5);

            string[] stateKeys =
            {
                "StateNeutralBrush",
                "StateSuccessBrush",
                "StateWarningBrush",
                "StateErrorBrush",
                "StateRecoveryBrush",
            };

            foreach (string key in stateKeys)
            {
                AssertContrast(application, themeName,
                    key, "SurfaceBaseBrush", 4.5);
                AssertContrast(application, themeName,
                    key, "SurfaceRaisedBrush", 4.5);
            }
        }

        private static void AssertContrast(Application application,
            string themeName, string foregroundKey, string backgroundKey,
            double minimum)
        {
            SolidColorBrush foreground = GetBrush(application, foregroundKey,
                themeName);
            SolidColorBrush background = GetBrush(application, backgroundKey,
                themeName);
            double contrast = ContrastRatio(foreground.Color, background.Color);
            Assert.IsTrue(contrast >= minimum,
                $"{themeName} {foregroundKey}/{backgroundKey} contrast " +
                $"was {contrast:F2}; expected at least {minimum:F2}.");
        }

        private static SolidColorBrush GetBrush(Application application,
            string key, string themeName)
        {
            object resource = application.TryFindResource(key);
            Assert.IsInstanceOfType(resource, typeof(SolidColorBrush),
                $"{themeName} theme is missing solid brush {key}.");
            return (SolidColorBrush)resource;
        }

        private static double ContrastRatio(Color first, Color second)
        {
            double firstLuminance = RelativeLuminance(first);
            double secondLuminance = RelativeLuminance(second);
            double lighter = Math.Max(firstLuminance, secondLuminance);
            double darker = Math.Min(firstLuminance, secondLuminance);
            return (lighter + 0.05) / (darker + 0.05);
        }

        private static double RelativeLuminance(Color color)
        {
            return 0.2126 * LinearChannel(color.R) +
                0.7152 * LinearChannel(color.G) +
                0.0722 * LinearChannel(color.B);
        }

        private static double LinearChannel(byte value)
        {
            double channel = value / 255.0;
            return channel <= 0.04045
                ? channel / 12.92
                : Math.Pow((channel + 0.055) / 1.055, 2.4);
        }
    }
}
