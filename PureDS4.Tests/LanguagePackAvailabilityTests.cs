using DS4Windows;
using DS4WinWPF.DS4Forms.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace DS4WindowsTests
{
    /// <summary>
    /// The language selector used to list a culture only when a satellite
    /// assembly existed as a file on disk. A self-contained single-file
    /// publish bundles those assemblies inside the executable, so the
    /// installed build had no per-culture directory to find and offered only
    /// English while shipping twenty-three translations.
    ///
    /// The enumeration now asks the runtime to resolve each satellite
    /// assembly. That is the same resolver the application already uses to
    /// render localized text, and it is bundle-aware, so it answers correctly
    /// in a loose build and in a single-file one. These tests pin that
    /// behavior to the resolver rather than to the filesystem.
    /// </summary>
    [TestClass]
    public class LanguagePackAvailabilityTests
    {
        private static Assembly ProductAssembly =>
            typeof(ProductIdentity).Assembly;

        [TestMethod]
        public void DevelopmentLayoutOffersEveryShippedTranslation()
        {
            IReadOnlyList<LangPackItem> languages =
                LanguagePackViewModel.EnumerateAvailableLanguages(
                    ProductAssembly);

            Assert.AreEqual(string.Empty, languages[0].Name,
                "The invariant English entry must come first.");
            Assert.IsTrue(languages.Count > 1,
                "No translations were offered at all.");

            // Every reported culture is one the runtime can actually load.
            foreach (LangPackItem item in languages.Skip(1))
            {
                CultureInfo culture = CultureInfo.GetCultureInfo(item.Name);
                Assert.IsNotNull(ProductAssembly.GetSatelliteAssembly(culture),
                    $"{item.Name} was offered without loadable resources.");
                Assert.IsFalse(string.IsNullOrWhiteSpace(item.NativeName),
                    $"{item.Name} was offered without a display name.");
            }
        }

        [TestMethod]
        public void OfferedLanguagesMatchWhatTheRuntimeCanResolve()
        {
            // The single-file guarantee reduces to this: the offered set is
            // exactly the set the satellite resolver accepts. A filesystem
            // walk cannot satisfy that, because the bundled layout has no
            // per-culture directories to walk.
            HashSet<string> offered = LanguagePackViewModel
                .EnumerateAvailableLanguages(ProductAssembly)
                .Skip(1)
                .Select(item => item.Name)
                .ToHashSet(StringComparer.Ordinal);

            HashSet<string> resolvable = CultureInfo
                .GetCultures(CultureTypes.AllCultures)
                .Where(culture => !string.IsNullOrEmpty(culture.Name))
                .Where(CanResolveSatellite)
                .Select(culture => culture.Name)
                .ToHashSet(StringComparer.Ordinal);

            CollectionAssert.AreEquivalent(resolvable.ToList(),
                offered.ToList());
        }

        [TestMethod]
        public void DirectoriesWithoutProductResourcesAreNotOffered()
        {
            // The build output also contains directories that are not our
            // translations: runtime identifier folders, and satellite folders
            // belonging to dependencies. Listing directories would offer them.
            HashSet<string> offered = LanguagePackViewModel
                .EnumerateAvailableLanguages(ProductAssembly)
                .Select(item => item.Name)
                .ToHashSet(StringComparer.Ordinal);

            string root = Path.GetDirectoryName(ProductAssembly.Location);
            string satellite = ProductAssembly.GetName().Name + ".resources.dll";

            List<string> foreignDirectories = Directory.GetDirectories(root)
                .Where(directory =>
                    !File.Exists(Path.Combine(directory, satellite)))
                .Select(Path.GetFileName)
                .ToList();

            Assert.IsTrue(foreignDirectories.Count > 0,
                "Expected the build output to contain unrelated directories.");

            foreach (string directory in foreignDirectories)
            {
                Assert.IsFalse(offered.Contains(directory),
                    $"{directory} is not a PureDS4 translation.");
            }
        }

        [TestMethod]
        public void AnAssemblyWithoutTranslationsOffersOnlyEnglish()
        {
            // Missing-culture fallback. Nothing is invented for an assembly
            // that ships no satellites.
            IReadOnlyList<LangPackItem> languages =
                LanguagePackViewModel.EnumerateAvailableLanguages(
                    typeof(LanguagePackAvailabilityTests).Assembly);

            Assert.AreEqual(1, languages.Count);
            Assert.AreEqual(string.Empty, languages[0].Name);
        }

        [TestMethod]
        public void InheritedCultureIdentifierMistakesAreCorrected()
        {
            // Two translations shipped under identifiers that did not name the
            // language they contained. "idn" is not a culture at all, so the
            // Indonesian text produced no satellite and was unreachable. "se"
            // is Northern Sami, so the Swedish text shipped under the wrong
            // language. Both now use their real identifiers.
            HashSet<string> offered = LanguagePackViewModel
                .EnumerateAvailableLanguages(ProductAssembly)
                .Select(item => item.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            Assert.IsTrue(offered.Contains("id"),
                "Indonesian must be reachable under its real identifier.");
            Assert.IsTrue(offered.Contains("sv"),
                "Swedish must be reachable under its real identifier.");

            Assert.IsFalse(offered.Contains("idn"),
                "idn is not a culture and must produce no resources.");
            Assert.IsFalse(offered.Contains("se"),
                "se is Northern Sami and no such translation is shipped.");

            Assert.AreEqual("Indonesian",
                CultureInfo.GetCultureInfo("id").EnglishName);
            Assert.AreEqual("Swedish",
                CultureInfo.GetCultureInfo("sv").EnglishName);
        }

        [TestMethod]
        public void CulturesWithoutAShippedTranslationAreNotOffered()
        {
            HashSet<string> offered = LanguagePackViewModel
                .EnumerateAvailableLanguages(ProductAssembly)
                .Select(item => item.Name)
                .ToHashSet(StringComparer.Ordinal);

            foreach (string absent in new[] { "af-ZA", "is-IS", "sw-KE", "se" })
            {
                Assert.IsFalse(offered.Contains(absent),
                    $"{absent} has no translation and must not be offered.");
            }
        }

        [TestMethod]
        public void SelectingALanguageRecordsItForTheNextStart()
        {
            string previousUseLang = Global.UseLang;
            CultureInfo previousUiCulture =
                Thread.CurrentThread.CurrentUICulture;
            try
            {
                LanguagePackViewModel model = new LanguagePackViewModel();
                Scan(model);

                int index = model.LangPackList.FindIndex(
                    item => item.Name == "de");
                Assert.IsTrue(index > 0,
                    "German is shipped and must be selectable.");

                Global.UseLang = string.Empty;
                model.SelectedIndex = index;
                Assert.IsTrue(model.ChangeLanguagePack(),
                    "Choosing a different language must report a change.");
                Assert.AreEqual("de", Global.UseLang);

                // Re-selecting the same language is not a change, so a restart
                // is not requested for nothing.
                Assert.IsFalse(model.ChangeLanguagePack());
            }
            finally
            {
                Global.UseLang = previousUseLang;
                Thread.CurrentThread.CurrentUICulture = previousUiCulture;
            }
        }

        [TestMethod]
        public void AStoredLanguageIsPreselectedOnTheNextStart()
        {
            CultureInfo previousUiCulture =
                Thread.CurrentThread.CurrentUICulture;
            try
            {
                // Startup applies the stored language to the UI culture before
                // the settings page scans, which is how the saved choice comes
                // back selected after a restart.
                Thread.CurrentThread.CurrentUICulture =
                    CultureInfo.GetCultureInfo("de");

                LanguagePackViewModel model = new LanguagePackViewModel();
                Scan(model);

                Assert.AreEqual("de",
                    model.LangPackList[model.SelectedIndex].Name);
            }
            finally
            {
                Thread.CurrentThread.CurrentUICulture = previousUiCulture;
            }
        }

        [TestMethod]
        public void AnUnavailableStoredLanguageFallsBackToEnglish()
        {
            CultureInfo previousUiCulture =
                Thread.CurrentThread.CurrentUICulture;
            try
            {
                // A profile carrying a language whose translation is no longer
                // shipped must land on the invariant entry, not on a stale
                // index into the list.
                Thread.CurrentThread.CurrentUICulture =
                    CultureInfo.GetCultureInfo("af-ZA");

                LanguagePackViewModel model = new LanguagePackViewModel();
                Scan(model);

                Assert.AreEqual(0, model.SelectedIndex);
                Assert.AreEqual(string.Empty,
                    model.LangPackList[model.SelectedIndex].Name);
            }
            finally
            {
                Thread.CurrentThread.CurrentUICulture = previousUiCulture;
            }
        }

        private static bool CanResolveSatellite(CultureInfo culture)
        {
            try
            {
                return ProductAssembly.GetSatelliteAssembly(culture) != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void Scan(LanguagePackViewModel model)
        {
            using ManualResetEventSlim finished = new ManualResetEventSlim();
            model.ScanFinished += (_, _) => finished.Set();
            model.ScanForLangPacks();
            Assert.IsTrue(finished.Wait(TimeSpan.FromSeconds(30)),
                "The language scan did not finish.");
        }
    }
}
