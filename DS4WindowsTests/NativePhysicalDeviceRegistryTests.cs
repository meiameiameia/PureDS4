using DS4Windows;

namespace DS4WindowsTests
{
    [TestClass]
    public class NativePhysicalDeviceRegistryTests
    {
        [TestCleanup]
        public void Cleanup() => NativePhysicalDeviceRegistry.Clear();

        [TestMethod]
        public void RegisteredIdentityExcludesExactPathAndInstance()
        {
            const string instanceId = @"HID\VID_054C&PID_09CC\NATIVE";
            const string devicePath = @"\\?\hid#native-path";

            NativePhysicalDeviceRegistry.Register(instanceId, devicePath);

            Assert.IsTrue(NativePhysicalDeviceRegistry.ContainsInstance(
                instanceId.ToLowerInvariant()));
            Assert.IsTrue(NativePhysicalDeviceRegistry.IsExcluded(
                devicePath.ToUpperInvariant()));
        }

        [TestMethod]
        public void UnregisterAndClearReleaseOnlyRecordedExclusions()
        {
            NativePhysicalDeviceRegistry.Register(@"HID\ONE", "path-one");
            NativePhysicalDeviceRegistry.Register(@"HID\TWO", "path-two");

            NativePhysicalDeviceRegistry.Unregister(@"hid\one", "PATH-ONE");

            Assert.IsFalse(NativePhysicalDeviceRegistry.ContainsInstance(
                @"HID\ONE"));
            Assert.IsTrue(NativePhysicalDeviceRegistry.ContainsInstance(
                @"HID\TWO"));

            NativePhysicalDeviceRegistry.Clear();
            Assert.IsFalse(NativePhysicalDeviceRegistry.ContainsInstance(
                @"HID\TWO"));
        }
    }
}
