using Microsoft.VisualStudio.TestTools.UnitTesting;
using PureDS4.Bootstrapper;

namespace PureDS4.Tests
{
    [TestClass]
    public class InstallerTermsTests
    {
        [DataTestMethod]
        [DataRow(null, false)]
        [DataRow("", false)]
        [DataRow("microsoft-old", false)]
        [DataRow("MICROSOFT-20260929", false)]
        [DataRow("microsoft-20260929", true)]
        public void InstallationRequiresExactReviewedConsent(string accepted, bool expected)
        {
            Assert.AreEqual(expected, InstallerTerms.CanProceed(true, accepted));
            Assert.IsTrue(InstallerTerms.CanProceed(false, accepted), "Uninstall and layout cannot require consent.");
        }

        [TestMethod]
        public void CommandLineConsentIsExplicitAndUnambiguous()
        {
            Assert.IsFalse(InstallerTerms.HasExplicitConsent(null));
            Assert.IsFalse(InstallerTerms.HasExplicitConsent(new[] { "/quiet" }));
            Assert.IsFalse(InstallerTerms.HasExplicitConsent(new[] { "--accept-microsoft-terms" }));
            Assert.IsFalse(InstallerTerms.HasExplicitConsent(new[] { "--accept-microsoft-terms=microsoft-old" }));
            Assert.IsFalse(InstallerTerms.HasExplicitConsent(new[] { InstallerTerms.Argument, InstallerTerms.Argument }));
            Assert.IsFalse(InstallerTerms.HasExplicitConsent(new[] { InstallerTerms.Argument, "--accept-microsoft-terms=false" }));
            Assert.IsTrue(InstallerTerms.HasExplicitConsent(new[] { "/quiet", InstallerTerms.Argument }));
        }

        [TestMethod]
        public void EmbeddedDocumentsAreCompleteAndMatchReviewedVendorBytes() => InstallerTerms.VerifyDocuments();
    }
}
