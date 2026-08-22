using DS4Windows;
using DS4WinWPF.DS4Forms.ViewModels;
using System.Reflection;
using System.Threading.Tasks;

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
        public async Task IndependentReleaseNotesDoNotQueryUpstream()
        {
            Assert.IsFalse(UpdateAuthorityPolicy.ProductReleaseNotesEnabled);
            Assert.AreEqual(
                UpdateAuthorityPolicy.ReleaseNotesDisabledMarkdown,
                await Changelog.GetChangelogMarkdown(allVersions: true));
            Assert.AreEqual(0,
                (await Changelog.GetChangelog(allVersions: true)).Count);
            StringAssert.Contains(Changelog.GITHUB_RELEASES_API_URI,
                "meiameiameia/ds4windows-reworked");
            Assert.IsFalse(Changelog.GITHUB_RELEASES_API_URI.Contains(
                "hbashton", System.StringComparison.OrdinalIgnoreCase));
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
