using DS4Windows;
using DS4WinWPF.DS4Forms.ViewModels;
using System.Reflection;

namespace DS4WindowsTests
{
    [TestClass]
    public class UpdateAuthorityPolicyTests
    {
        [TestMethod]
        public void IndependentProductUpdateAuthorityIsDisabled()
        {
            Assert.IsFalse(UpdateAuthorityPolicy.ProductUpdatesEnabled);
            Assert.IsFalse(Changelog.CheckNewerReleaseExists(
                out string releaseTag, allowCached: false));
            Assert.AreEqual(string.Empty, releaseTag);
        }

        [TestMethod]
        public void RuntimeNoLongerExposesInheritedUpdaterMutationMethods()
        {
            const BindingFlags callable = BindingFlags.Public |
                BindingFlags.NonPublic | BindingFlags.Instance;

            Assert.IsNull(typeof(MainWindowsViewModel).GetMethod(
                "RunUpdaterCheck", callable));
            Assert.IsNull(typeof(MainWindowsViewModel).GetMethod(
                "LauchDS4Updater", callable));
            Assert.IsNull(typeof(MainWindowsViewModel).GetMethod(
                "DownloadUpstreamVersionInfo", callable));
            Assert.IsNull(typeof(Util).GetMethod(
                "ElevatedCopyUpdater", BindingFlags.Public |
                    BindingFlags.NonPublic | BindingFlags.Static));
        }
    }
}
