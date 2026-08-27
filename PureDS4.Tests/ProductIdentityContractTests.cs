using DS4Windows;
using DS4WinWPF;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Reflection;

namespace DS4WindowsTests
{
    [TestClass]
    public class ProductIdentityContractTests
    {
        [TestMethod]
        public void PublicIdentityUsesPureDS4NameAndRepository()
        {
            Assert.AreEqual("PureDS4", ProductIdentity.Name);
            Assert.AreEqual("meiameiameia", ProductIdentity.Publisher);
            Assert.AreEqual(
                "https://github.com/meiameiameia/pureds4",
                ProductIdentity.RepositoryUrl);
            Assert.AreEqual(ProductIdentity.RepositoryUrl + "/issues",
                ProductIdentity.IssuesUrl);
        }

        [TestMethod]
        public void EveryOwnedNameCarriesThePureDS4Identity()
        {
            // PureDS4 replaces DS4Windows rather than running beside it, but
            // it still has to own every name it claims. A shared name makes an
            // installer upgrade over the wrong product, makes ownership
            // impossible to audit, and lets a half-finished replacement leave
            // two products competing for the same files and handles.
            foreach (string owned in new[]
            {
                ProductIdentity.Name,
                ProductIdentity.DataFolderName,
                ProductIdentity.LanguageAssemblyName,
                ProductIdentity.StartupShortcutFileName,
                ProductIdentity.StartupTaskName,
                ProductIdentity.ViiperStartupTaskName,
                ProductIdentity.SetupStagingFolderName,
                ProductIdentity.IpcClassNameMapName,
                ProductIdentity.IpcResultDataMapName,
                ProductIdentity.IpcResultReadyEventName,
                ProductIdentity.IpcResultDataSingleTaskMutexName,
            })
            {
                StringAssert.Contains(owned, "PureDS4",
                    owned + " is not on a PureDS4 identity.");
                Assert.IsFalse(owned.Contains("DS4Windows",
                        StringComparison.OrdinalIgnoreCase),
                    owned + " still carries the inherited DS4Windows name.");
            }
        }

        [TestMethod]
        public void OwnedStorageAndTaskNamesAreTheExpectedValues()
        {
            Assert.AreEqual("PureDS4", ProductIdentity.DataFolderName);
            Assert.AreEqual("PureDS4.resources.dll",
                ProductIdentity.LanguageAssemblyName);
            Assert.AreEqual("PureDS4.lnk",
                ProductIdentity.StartupShortcutFileName);
            Assert.AreEqual("RunPureDS4", ProductIdentity.StartupTaskName);
            Assert.AreEqual("RunPureDS4VIIPER",
                ProductIdentity.ViiperStartupTaskName);
            Assert.AreEqual("PureDS4.Setup",
                ProductIdentity.SetupStagingFolderName);
        }

        [TestMethod]
        public void LanguageAssemblyNameMatchesTheSatelliteAssembliesBuilt()
        {
            // The satellite assemblies are named after the assembly, so this
            // constant silently empties the language-pack list whenever the
            // two disagree.
            Assembly assembly = typeof(ProductIdentity).Assembly;
            Assert.AreEqual(assembly.GetName().Name + ".resources.dll",
                ProductIdentity.LanguageAssemblyName);
            Assert.AreEqual(ProductIdentity.LanguageAssemblyName,
                Global.LANGUAGE_ASSEMBLY_NAME);
        }

        [TestMethod]
        public void IpcIdentitiesDoNotCollideWithDS4Windows()
        {
            StringAssert.StartsWith(ProductIdentity.IpcClassNameMapName,
                "PureDS4_");
            StringAssert.StartsWith(ProductIdentity.IpcResultDataMapName,
                "PureDS4_");
            StringAssert.StartsWith(ProductIdentity.IpcResultReadyEventName,
                "PureDS4_");
            StringAssert.StartsWith(
                ProductIdentity.IpcResultDataSingleTaskMutexName, "PureDS4_");

            // The inherited single-instance handle would make each product
            // treat the other as an existing instance of itself.
            Assert.AreNotEqual("{a52b5b20-d9ee-4f32-8518-307fa14aa0c6}",
                ProductIdentity.SingleInstanceEventName);
        }

        [TestMethod]
        public void StartupShortcutIsTheOnlyShortcutTheApplicationOwns()
        {
            // An ordinary startup preference must not read, adopt or delete
            // the shortcut of DS4Windows or of an earlier DS4Windows Reworked
            // install. Removing those belongs to an explicit replacement flow.
            Assert.AreEqual(ProductIdentity.StartupShortcutFileName,
                Path.GetFileName(StartupMethods.lnkpath));
            Assert.AreEqual(
                Environment.GetFolderPath(Environment.SpecialFolder.Startup),
                Path.GetDirectoryName(StartupMethods.lnkpath));

            foreach (FieldInfo field in typeof(StartupMethods).GetFields(
                BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Static))
            {
                if (field.GetValue(null) is not string value)
                {
                    continue;
                }

                Assert.IsFalse(value.Contains("DS4Windows",
                        StringComparison.OrdinalIgnoreCase),
                    field.Name + " still references a DS4Windows-owned name.");
            }
        }

        [TestMethod]
        public void HidHideBlacklistLockStaysSharedAcrossProducts()
        {
            // Deliberately not a PureDS4 name. The HidHide blacklist is one
            // shared resource, so any product that mutates it must serialise
            // against the same machine-wide lock; a per-product lock would let
            // PureDS4 and a not-yet-removed DS4Windows mutate the list at the
            // same time.
            Assert.AreEqual(
                @"Global\DS4Windows-Reworked-HidHide-Blacklist",
                ProductIdentity.HidHideBlacklistMutexName);
        }

        [TestMethod]
        public void PublicIdentityPreservesExecutableCompatibility()
        {
            Assembly assembly = typeof(ProductIdentity).Assembly;

            Assert.AreEqual("PureDS4", assembly.GetName().Name);
            Assert.AreEqual(new System.Version(5, 1, 0, 0),
                assembly.GetName().Version);
            Assert.AreEqual(ProductIdentity.Name,
                assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product);
            Assert.AreEqual(ProductIdentity.Name,
                assembly.GetCustomAttribute<AssemblyTitleAttribute>()?.Title);
            Assert.AreEqual(ProductIdentity.Publisher,
                assembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company);
            Assert.AreEqual("5.1.0-beta.1",
                assembly.GetCustomAttribute<
                    AssemblyInformationalVersionAttribute>()?.InformationalVersion);
        }
    }
}
