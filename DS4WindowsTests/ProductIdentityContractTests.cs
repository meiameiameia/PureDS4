using DS4Windows;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;

namespace DS4WindowsTests
{
    [TestClass]
    public class ProductIdentityContractTests
    {
        [TestMethod]
        public void PublicIdentityUsesReworkedAliasAndRepository()
        {
            Assert.AreEqual("DS4Windows Reworked", ProductIdentity.Name);
            Assert.AreEqual("meiameiameia", ProductIdentity.Publisher);
            Assert.AreEqual(
                "https://github.com/meiameiameia/ds4windows-reworked",
                ProductIdentity.RepositoryUrl);
            Assert.AreEqual(ProductIdentity.RepositoryUrl + "/issues",
                ProductIdentity.IssuesUrl);
        }

        [TestMethod]
        public void PublicIdentityPreservesExecutableCompatibility()
        {
            Assembly assembly = typeof(ProductIdentity).Assembly;

            Assert.AreEqual("DS4Windows", assembly.GetName().Name);
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
