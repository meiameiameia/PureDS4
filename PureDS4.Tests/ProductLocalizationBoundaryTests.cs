using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Text.RegularExpressions;

namespace DS4WindowsTests
{
    /// <summary>
    /// Localized strings are the easiest place for the inherited identity to
    /// survive a rename: the English value gets corrected and twenty-five
    /// translations keep saying DS4Windows. These tests read the compiled
    /// satellite assemblies, so they check what the application would actually
    /// render rather than what the source tree happens to contain.
    ///
    /// A reference to DS4Windows is legitimate only when the string is
    /// describing the upstream product, an executable being replaced, or a
    /// compatibility identifier. Every such string is named below with its
    /// reason.
    /// </summary>
    [TestClass]
    public class ProductLocalizationBoundaryTests
    {
        private static readonly Regex InheritedBranding = new Regex(
            "DS4Windows|DS4Updater|DS4Tool",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Keys the product renders. Every shipped culture must carry PureDS4
        /// wording for these, because they appear in the running application.
        /// </summary>
        private static readonly string[] ActiveProductKeys =
        {
            "CheckUpdateStartup",
            "TurnOffDS4WindowsTemporarily",
            "AntiDeadzoneTooltip",
            "StoppedDS4Windows",
            "LanguagePackApplyRestartRequired",
            "DS4Update",
            "UACTask",
            "RunAtStartup",
            "CloseMinimize",
            "QuitOtherPrograms",
        };

        /// <summary>
        /// Narrow, documented exceptions. Each is either a legitimate
        /// description of another product, or an inherited entry that no
        /// active UI surface renders. The set must not grow: a new offender
        /// means a product-facing string regressed.
        /// </summary>
        private static readonly Dictionary<string, string> AllowedKeys =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["CustomExeNameInfo"] =
                    "Names DS4Windows.exe and InputMapper.exe as the mapper " +
                    "executables games detect. That is a description of other " +
                    "products, not this one.",

                // Inherited entries with no active binding or code path. They
                // stay because removing a key requires regenerating the
                // designer output through the .resx workflow.
                ["CannotMoveFiles"] = "Unreferenced inherited entry.",
                ["CloseDS4W"] = "Unreferenced inherited entry.",
                ["CopyComplete"] = "Unreferenced inherited entry.",
                ["DS4WindowsCannotEditHere"] = "Unreferenced inherited entry.",
                ["IfRemovingDS4Windows"] = "Unreferenced inherited entry.",
                ["PleaseDownloadUpdater"] = "Unreferenced inherited entry.",
                ["UpToDate"] = "Unreferenced inherited entry.",
                ["ViGEmPluginFailure"] = "Unreferenced inherited entry.",
                ["FutureNetNotInstalled"] = "Unreferenced inherited entry.",
                ["Net8NotInstalledWinNotice"] = "Unreferenced inherited entry.",
                ["DualSRumbleForceGenericRescale_Tip"] =
                    "Unreferenced inherited entry.",
                ["FirstLaunch.DeviceIntroText"] =
                    "Unreferenced inherited entry.",
                ["Welcome.Step5HelpText"] = "Unreferenced inherited entry.",
                ["Welcome.WinTitle"] = "Unreferenced inherited entry.",
            };

        [TestMethod]
        public void ShippedCulturesCarryPureDS4WordingForActiveStrings()
        {
            List<CultureInfo> cultures = ShippedCultures();
            Assert.IsTrue(cultures.Count > 1,
                "No satellite cultures were found to inspect.");

            List<string> failures = new List<string>();
            foreach (ResourceManager manager in ResourceManagers())
            {
                foreach (CultureInfo culture in cultures)
                {
                    foreach (string key in ActiveProductKeys)
                    {
                        string value = LocalValue(manager, culture, key);
                        if (value == null)
                        {
                            // Absent from this culture, so the corrected
                            // neutral value is what renders.
                            continue;
                        }

                        if (InheritedBranding.IsMatch(value))
                        {
                            failures.Add(
                                $"{Describe(culture)}/{key}: {value}");
                        }
                    }
                }
            }

            Assert.AreEqual(0, failures.Count,
                "Active product strings still carry inherited branding:" +
                Environment.NewLine +
                string.Join(Environment.NewLine, failures));
        }

        [TestMethod]
        public void NoUndocumentedStringCarriesInheritedBranding()
        {
            List<string> failures = new List<string>();
            foreach (ResourceManager manager in ResourceManagers())
            {
                foreach (CultureInfo culture in ShippedCultures())
                {
                    ResourceSet set = manager.GetResourceSet(culture,
                        createIfNotExists: true, tryParents: false);
                    if (set == null)
                    {
                        continue;
                    }

                    foreach (DictionaryEntry entry in set)
                    {
                        if (entry.Key is not string key ||
                            entry.Value is not string value)
                        {
                            continue;
                        }

                        if (!InheritedBranding.IsMatch(value) ||
                            AllowedKeys.ContainsKey(key))
                        {
                            continue;
                        }

                        failures.Add($"{Describe(culture)}/{key}: {value}");
                    }
                }
            }

            Assert.AreEqual(0, failures.Count,
                "Undocumented inherited branding found. Correct the string, " +
                "or record the reason in AllowedKeys:" + Environment.NewLine +
                string.Join(Environment.NewLine, failures));
        }

        [TestMethod]
        public void EveryDocumentedExceptionCarriesAReason()
        {
            foreach ((string key, string reason) in AllowedKeys)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(reason),
                    $"{key} is exempt without a recorded reason.");
            }
        }

        private static string LocalValue(ResourceManager manager,
            CultureInfo culture, string key)
        {
            ResourceSet set = manager.GetResourceSet(culture,
                createIfNotExists: true, tryParents: false);
            return set?.GetString(key);
        }

        private static string Describe(CultureInfo culture)
        {
            return string.IsNullOrEmpty(culture.Name)
                ? "(neutral)" : culture.Name;
        }

        private static IEnumerable<ResourceManager> ResourceManagers()
        {
            Assembly product = typeof(DS4Windows.ProductIdentity).Assembly;
            foreach (string typeName in new[]
            {
                "DS4WinWPF.Translations.Strings",
                "DS4WinWPF.Properties.Resources",
            })
            {
                Type type = product.GetType(typeName, throwOnError: true);
                PropertyInfo property = type.GetProperty("ResourceManager",
                    BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.Static);
                Assert.IsNotNull(property, $"{typeName} has no ResourceManager.");
                yield return (ResourceManager)property.GetValue(null);
            }
        }

        /// <summary>
        /// The neutral culture plus every culture with a satellite assembly
        /// beside the product assembly. Derived from the build output rather
        /// than a hard-coded list so a new translation is covered
        /// automatically.
        /// </summary>
        private static List<CultureInfo> ShippedCultures()
        {
            List<CultureInfo> cultures = new List<CultureInfo>
            {
                CultureInfo.InvariantCulture,
            };

            Assembly product = typeof(DS4Windows.ProductIdentity).Assembly;
            string root = Path.GetDirectoryName(product.Location);
            string satellite = product.GetName().Name + ".resources.dll";

            foreach (string directory in Directory.GetDirectories(root))
            {
                if (!File.Exists(Path.Combine(directory, satellite)))
                {
                    continue;
                }

                string name = Path.GetFileName(directory);
                CultureInfo culture;
                try
                {
                    culture = CultureInfo.GetCultureInfo(name);
                }
                catch (CultureNotFoundException)
                {
                    continue;
                }

                cultures.Add(culture);
            }

            return cultures;
        }
    }
}
