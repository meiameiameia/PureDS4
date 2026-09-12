using DS4Windows;
using DS4WinWPF.DS4Forms;
using DS4WinWPF;
using DS4WinWPF.DS4Control.DTOXml;
using DS4WinWPF.DS4Forms.ViewModels;
using DS4WinWPF.DS4Forms.ViewModels.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace DS4WindowsTests
{
    /// <summary>
    /// The first public release supports the DualShock 4 only. DualShock 3 is
    /// a planned milestone whose setup path depends on DsHidMini, on whatever
    /// ScpToolkit or ScpTools state a machine already carries, and on genuine
    /// hardware nobody has exercised yet.
    ///
    /// The implementation, the device option and the DS3 tests are all still
    /// present on purpose. What these tests pin is that no ordinary surface
    /// offers DS3 or reads as though it works, and that a stored setting
    /// cannot quietly switch it on.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class FirstReleaseScopeTests
    {
        [TestMethod]
        public void ReleaseScopeIsDualShock4Only()
        {
#if !PUREDS4_EXPERIMENTAL_DS3
            // Guarded rather than deleted: this is the assertion that proves a
            // standard build does not offer DS3, so it must keep running in
            // every ordinary Debug and Release build. The experimental build
            // asserts the opposite in ExperimentalDualShock3BuildTests.
            Assert.IsFalse(ProductScope.DualShock3Offered);
#endif
            Assert.AreEqual("DualShock 4",
                ProductScope.SupportedControllerSummary);
            StringAssert.Contains(ProductScope.BroaderAlternativeUrl,
                "hbashton/DS4Windows");
        }

        [TestMethod]
        public void AStoredDS3SettingCannotEnableAnUnvalidatedPath()
        {
            // The option still round-trips, so the future DS3 gate keeps its
            // configuration surface. It just cannot take effect in a release
            // that has never been validated against the hardware.
            DS3DeviceOptions options = new DS3DeviceOptions();
            Assert.IsFalse(DS3DeviceOptions.DEFAULT_ENABLE,
                "DualShock 3 must be off unless the user opts in.");

            options.Enabled = true;
            Assert.IsTrue(options.Enabled,
                "The stored option must still round-trip for the future gate.");

#if !PUREDS4_EXPERIMENTAL_DS3
            Assert.IsFalse(ProductScope.DualShock3Offered && options.Enabled,
                "A stored setting must not activate DualShock 3 support.");
#endif
        }

        [TestMethod]
        public void DormantDualShock3ImplementationIsRetained()
        {
            // Postponed, not deleted. If these disappear, the future DS3 gate
            // has lost the work it was going to build on.
            Assert.IsTrue(Enum.IsDefined(
                typeof(DS4Windows.InputDevices.InputDeviceType),
                DS4Windows.InputDevices.InputDeviceType.DS3));
            Assert.IsNotNull(typeof(ControlServiceDeviceOptions)
                .GetProperty("DS3DeviceOpts"));
            Assert.IsNotNull(typeof(ProductIdentity).Assembly.GetType(
                "DS4Windows.InputDevices.DS3Device"));
        }

        [TestMethod]
        public void FirstRunDoesNotOfferDualShock3()
        {
            RunOnUiThread(() =>
            {
                FirstLaunchUtilWindow window = new FirstLaunchUtilWindow(
                    new ControlServiceDeviceOptions());

                AssertNoDualShock3Claim(CollectText(window), "first run");

                Assert.IsFalse(Descendants(window).OfType<CheckBox>()
                    .Any(box => box.Name != null &&
                        box.Name.Contains("ds3",
                            StringComparison.OrdinalIgnoreCase)),
                    "First run still exposes a DualShock 3 toggle.");
            });
        }

        [TestMethod]
        public void AboutDescribesADualShock4Product()
        {
            RunOnUiThread(() =>
            {
                About about = new About();
                string text = string.Join("\n", CollectText(about));

                StringAssert.Contains(text, "DualShock 4");

                // DualShock 3 may only appear as an unclaimed future
                // milestone, never as something this release supports.
                foreach (string line in CollectText(about)
                    .Where(value => value.Contains("DualShock 3",
                        StringComparison.OrdinalIgnoreCase)))
                {
                    Assert.IsTrue(
                        line.Contains("planned",
                            StringComparison.OrdinalIgnoreCase) ||
                        line.Contains("not supported",
                            StringComparison.OrdinalIgnoreCase),
                        "About presents DualShock 3 without marking it " +
                        $"unsupported: {line}");
                }

                StringAssert.Contains(text, "DS4Windows instead");
            });
        }

        [TestMethod]
        public void AutoProfilesDeviceSelectorCannotOfferDualShock3()
        {
            // Auto Profiles is an ordinary surface and was still listing DS3.
            // A rule naming a device this release cannot detect could never
            // match, so offering it would be a promise the product cannot keep.
            string previousDataPath = Global.appdatapath;
            string root = Path.Combine(Path.GetTempPath(), "PureDS4Tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            List<EnumChoiceSelection<AutoProfileDeviceOption>> options;
            try
            {
                // The holder reads its rule file from the configuration
                // directory. Point it at an empty one so this stays a test of
                // the selector, not of whatever is installed on this machine.
                Global.appdatapath = root;
                options = new AutoProfilesViewModel(new AutoProfileHolder(),
                    new ProfileList()).DeviceOptionList;
            }
            finally
            {
                Global.appdatapath = previousDataPath;
                Directory.Delete(root, true);
            }

#if PUREDS4_EXPERIMENTAL_DS3
            // The hardware-validation build deliberately restores the DS3 rule
            // so a tester can bind a profile to the controller under test.
            CollectionAssert.AreEqual(
                new[] { "Any", "DS4", "DS3" },
                options.Select(option => option.DisplayName).ToArray());
#else
            CollectionAssert.AreEqual(
                new[] { "Any", "DS4" },
                options.Select(option => option.DisplayName).ToArray());

            Assert.IsFalse(
                options.Any(option =>
                    option.ChoiceValue == AutoProfileDeviceOption.DS3),
                "Auto Profiles still offers a DualShock 3 rule.");
#endif
        }

        [TestMethod]
        public void AStoredDualShock3AutoProfileRuleStillRoundTrips()
        {
            // The option survives for the DS3 milestone, and an imported rule
            // keeps its value rather than being rewritten behind the user.
            Assert.IsTrue(Enum.IsDefined(typeof(AutoProfileDeviceOption),
                AutoProfileDeviceOption.DS3));

            AutoProfileEntity rule = new AutoProfileEntity("*game.exe",
                string.Empty)
            {
                DeviceOption = AutoProfileDeviceOption.DS3,
            };
            Assert.AreEqual(AutoProfileDeviceOption.DS3, rule.DeviceOption);

            // Its matching logic is intact but inert: this release never
            // detects a DS3, so the rule simply never fires.
            Assert.IsTrue(rule.IsDeviceMatch(
                DS4Windows.InputDevices.InputDeviceType.DS3));
            Assert.IsFalse(rule.IsDeviceMatch(
                DS4Windows.InputDevices.InputDeviceType.DS4));

            AutoProfileEntrySerializer dto = new AutoProfileEntrySerializer
            {
                Device = AutoProfileDeviceOption.DS3,
            };
            Assert.IsTrue(dto.ShouldSerializeDevice(),
                "A DualShock 3 rule must still be written back to disk.");
        }

        [TestMethod]
        public void OfflineMappingHintDoesNotPromiseDualShock3()
        {
            Assert.AreEqual("a DualShock 4",
                ControllerUiCapabilities.For(null)
                    .MappingAvailabilityScopeName);
        }

        private static void AssertNoDualShock3Claim(
            IEnumerable<string> lines, string surface)
        {
            foreach (string line in lines)
            {
                Assert.IsFalse(
                    line.Contains("DualShock 3",
                        StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("DsHidMini",
                        StringComparison.OrdinalIgnoreCase),
                    $"The {surface} surface still mentions DualShock 3: {line}");
            }
        }

        private static IEnumerable<DependencyObject> Descendants(
            DependencyObject root)
        {
            foreach (object child in LogicalTreeHelper.GetChildren(root))
            {
                if (child is not DependencyObject node)
                {
                    continue;
                }

                yield return node;
                foreach (DependencyObject descendant in Descendants(node))
                {
                    yield return descendant;
                }
            }
        }

        private static List<string> CollectText(DependencyObject root)
        {
            List<string> values = new List<string>();
            foreach (DependencyObject node in Descendants(root))
            {
                switch (node)
                {
                    case TextBlock textBlock when
                        !string.IsNullOrWhiteSpace(textBlock.Text):
                        values.Add(textBlock.Text);
                        break;
                    case HeaderedContentControl headered when
                        headered.Header is string header:
                        values.Add(header);
                        break;
                    case ContentControl content when
                        content.Content is string label:
                        values.Add(label);
                        break;
                }

                if (node is FrameworkElement element)
                {
                    if (element.ToolTip is string tip)
                    {
                        values.Add(tip);
                    }

                    string automationName =
                        AutomationProperties.GetName(element);
                    if (!string.IsNullOrWhiteSpace(automationName))
                    {
                        values.Add(automationName);
                    }
                }
            }

            return values;
        }

        private static void RunOnUiThread(Action body)
        {
            WpfTestHost.Run(() =>
            {
                WpfTestHost.LoadApplicationThemes(replaceExisting: true);
                body();
            });
        }
    }
}
