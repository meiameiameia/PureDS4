using DS4Windows;
using Microsoft.VisualStudio.TestTools.UnitTesting;
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
        public void HostIdentityDoesNotCollideWithDS4Windows()
        {
            // PureDS4 has to be installable next to DS4Windows. Anything
            // both products could claim must be on a PureDS4 name.
            Assert.AreEqual("PureDS4", ProductIdentity.DataFolderName);
            StringAssert.StartsWith(ProductIdentity.IpcClassNameMapName,
                "PureDS4_");
            StringAssert.StartsWith(ProductIdentity.IpcResultDataMapName,
                "PureDS4_");
            StringAssert.StartsWith(ProductIdentity.IpcResultReadyEventName,
                "PureDS4_");

            // The inherited single-instance handle would make each product
            // treat the other as an existing instance of itself.
            Assert.AreNotEqual("{a52b5b20-d9ee-4f32-8518-307fa14aa0c6}",
                ProductIdentity.SingleInstanceEventName);
        }

        [TestMethod]
        public void HidHideBlacklistLockStaysSharedAcrossProducts()
        {
            // Deliberately not a PureDS4 name. The HidHide blacklist is one
            // shared resource, so both products must serialise against the
            // same machine-wide lock; per-product locks would let them
            // mutate the list at the same time.
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
