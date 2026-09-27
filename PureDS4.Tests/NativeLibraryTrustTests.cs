using DS4Windows;

namespace DS4WindowsTests
{
    [TestClass]
    public class NativeLibraryTrustTests
    {
        [TestMethod]
        public void ApplicationOwnedPathNeverUsesCurrentDirectoryOrTraversal()
        {
            string root = Path.Combine(Path.GetTempPath(), "trusted-app");

            Assert.AreEqual(Path.GetFullPath(Path.Combine(root,
                    "rnnoise.dll")),
                NativeLibraryTrust.GetApplicationOwnedPath(
                    "rnnoise.dll", root));
            Assert.ThrowsException<ArgumentException>(() =>
                NativeLibraryTrust.GetApplicationOwnedPath(
                    @"..\rnnoise.dll", root));
        }

        [TestMethod]
        public void BundledApplicationNativeLibrariesMatchPinnedHashes()
        {
            string rnnoisePath = NativeLibraryTrust.GetApplicationOwnedPath(
                "rnnoise.dll");

            Assert.IsTrue(NativeLibraryTrust.HasExpectedSha256(rnnoisePath,
                NativeLibraryTrust.RnnoiseSha256), rnnoisePath);
        }

        [TestMethod]
        public void NvidiaDiscoveryUsesProtectedVendorRootsOnly()
        {
            IReadOnlyList<string> roots = NvidiaAudioNoiseSuppressor.
                GetTrustedRuntimeRoots(@"C:\Program Files",
                    @"C:\Program Files (x86)");

            Assert.AreEqual(4, roots.Count);
            Assert.IsTrue(roots.All(root =>
                root.StartsWith(@"C:\Program Files",
                    StringComparison.OrdinalIgnoreCase)));
            Assert.IsFalse(roots.Any(root => root.Contains(
                "NVIDIA_MAXINE_AFX_SDK_DIR",
                StringComparison.OrdinalIgnoreCase)));
        }
    }
}
