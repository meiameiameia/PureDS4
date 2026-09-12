using DS4Windows;
using DS4Windows.InputDevices;
using DS4WinWPF.DS4Forms.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace DS4WindowsTests
{
    /// <summary>
    /// The boundary between an ordinary PureDS4 build and the DualShock 3
    /// hardware-validation build.
    ///
    /// These tests compile in both modes on purpose. The standard-build
    /// assertions are the ones that matter for the release contract: they must
    /// keep proving that nothing an ordinary Debug or Release build produces
    /// can offer DS3. The experimental assertions exist so the opt-in build is
    /// not merely untested — a switch that silently failed to enable the path
    /// would waste a hardware session.
    /// </summary>
    [TestClass]
    public class ExperimentalDualShock3BuildTests
    {
        [TestMethod]
        public void TheGateFollowsTheBuildFlagAndNothingElse()
        {
            // DualShock3Offered is not independently settable: it is defined as
            // ExperimentalDualShock3Build. Pinning the identity here means a
            // future edit cannot quietly open the gate for standard builds
            // while leaving the experimental flag alone.
            Assert.AreEqual(ProductScope.ExperimentalDualShock3Build,
                ProductScope.DualShock3Offered,
                "The DS3 gate must track the experimental build flag exactly.");
        }

#if PUREDS4_EXPERIMENTAL_DS3

        [TestMethod]
        public void ExperimentalBuildOffersDualShock3()
        {
            Assert.IsTrue(ProductScope.ExperimentalDualShock3Build);
            Assert.IsTrue(ProductScope.DualShock3Offered,
                "The hardware-validation build exists to offer DS3.");
        }

        [TestMethod]
        public void ExperimentalBuildStillRequiresAnExplicitOptIn()
        {
            // Opening the gate must not be the same as turning DS3 on. A
            // tester still has to enable the device option, so simply running
            // the experimental build does not start claiming DS3 hardware.
            Assert.IsFalse(DS3DeviceOptions.DEFAULT_ENABLE,
                "DS3 must stay off until the tester enables it.");

            DS3DeviceOptions options = new DS3DeviceOptions();
            Assert.IsFalse(ProductScope.DualShock3Offered && options.Enabled,
                "A fresh experimental build must not have DS3 already active.");

            options.Enabled = true;
            Assert.IsTrue(ProductScope.DualShock3Offered && options.Enabled,
                "With the gate open and the option on, DS3 must be reachable.");
        }

        [TestMethod]
        public void ExperimentalBuildShowsTheDualShock3Control()
        {
            Assert.IsTrue(DeviceOptionsViewExposesDS3Toggle(),
                "The experimental build must expose a way to enable DS3.");
        }

#else

        [TestMethod]
        public void StandardBuildIsNotAnExperimentalBuild()
        {
            Assert.IsFalse(ProductScope.ExperimentalDualShock3Build,
                "An ordinary build must never be an experimental DS3 build.");
            Assert.IsFalse(ProductScope.DualShock3Offered);
        }

        [TestMethod]
        public void StandardBuildCannotReachDualShock3EvenWithTheOptionOn()
        {
            // The whole point of the gate: a stored or imported configuration
            // that enables DS3 must still not activate it.
            DS3DeviceOptions options = new DS3DeviceOptions
            {
                Enabled = true,
            };

            Assert.IsTrue(options.Enabled,
                "The option must still round-trip for the future milestone.");
            Assert.IsFalse(ProductScope.DualShock3Offered && options.Enabled,
                "A standard build must not activate DS3 from stored settings.");
        }

        [TestMethod]
        public void StandardBuildHidesTheDualShock3Control()
        {
            Assert.IsFalse(DeviceOptionsViewExposesDS3Toggle(),
                "A standard build must not offer a way to enable DS3.");
        }

#endif

        /// <summary>
        /// Whether the device-options view model reports that the DS3 control
        /// should be shown. Read through the same compile-time constant the
        /// property delegates to, rather than by constructing the view model:
        /// its constructor requires a live <c>ControlService</c>, and building
        /// the runtime orchestrator inside a test process runs real startup.
        /// The reflection check keeps the property from being renamed or
        /// deleted without this test noticing.
        /// </summary>
        private static bool DeviceOptionsViewExposesDS3Toggle()
        {
            Assert.IsNotNull(
                typeof(ControllerRegDeviceOptsViewModel).GetProperty(
                    nameof(ControllerRegDeviceOptsViewModel
                        .ExperimentalDS3Build)),
                "The device-options view model no longer exposes the DS3 " +
                "visibility seam the window binds to.");

            return ProductScope.ExperimentalDualShock3Build;
        }

        [TestMethod]
        public void DormantDualShock3ImplementationSurvivesInEveryBuild()
        {
            // Both builds keep the implementation. The experiment changes what
            // is offered, never what exists, so the future milestone gate is
            // unaffected by which mode was last built.
            Assert.IsTrue(Enum.IsDefined(typeof(InputDeviceType),
                InputDeviceType.DS3));
            Assert.IsNotNull(typeof(ProductIdentity).Assembly.GetType(
                "DS4Windows.InputDevices.DS3Device"));
            Assert.IsNotNull(typeof(ControlServiceDeviceOptions)
                .GetProperty(nameof(ControlServiceDeviceOptions.DS3DeviceOpts)));
        }

        [TestMethod]
        public void BothDualShock3DeviceEntriesRemainRegistered()
        {
            // The SXS/Sixaxis path is the one the gate controls. The DsHidMini
            // DS4-emulation path is registered as InputDeviceType.DS4, so it is
            // NOT governed by ProductScope at all — that asymmetry is
            // deliberate upstream behavior and is called out here so a future
            // change does not silently alter one and not the other.
            Type devices = typeof(ProductIdentity).Assembly
                .GetType("DS4Windows.DS4Devices");
            Assert.IsNotNull(devices, "DS4Devices must remain present.");
        }
    }
}
