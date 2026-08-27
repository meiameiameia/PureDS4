using DS4WinWPF;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NLog.Config;
using NLog.Targets;
using NLog.Targets.Wrappers;
using System;
using System.IO;
using System.Reflection;

namespace DS4Windows.Tests
{
    [TestClass]
    public class StartupHardeningTests
    {
        [TestMethod]
        public void StartupFailureReporterWritesBeforeNLogExists()
        {
            string root = Path.Combine(Path.GetTempPath(),
                "DS4WindowsTests", Guid.NewGuid().ToString("N"));
            try
            {
                string path = StartupFailureReporter.Write(
                    new InvalidOperationException("startup sentinel"),
                    "test bootstrap", root);

                Assert.AreEqual(Path.Combine(root, "Logs",
                    "startup_failure.log"), path);
                string contents = File.ReadAllText(path);
                StringAssert.Contains(contents, "test bootstrap");
                StringAssert.Contains(contents, "startup sentinel");
                StringAssert.Contains(
                    StartupFailureReporter.BuildUserMessage(path), path);
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        [TestMethod]
        public void LoggerTargetDiscoveryAcceptsDirectFileTarget()
        {
            LoggingConfiguration configuration = new();
            FileTarget expected = new("logfile");
            configuration.AddTarget(expected);

            Assert.AreSame(expected,
                LoggerHolder.FindFileTarget(configuration));
        }

        [TestMethod]
        public void LoggerTargetDiscoveryUnwrapsAsyncTarget()
        {
            LoggingConfiguration configuration = new();
            FileTarget expected = new("inner-file");
            AsyncTargetWrapper wrapper = new(expected)
            {
                Name = "logfile",
            };
            configuration.AddTarget(wrapper);

            Assert.AreSame(expected,
                LoggerHolder.FindFileTarget(configuration));
        }

        [TestMethod]
        public void MissingLoggerConfigurationIsRecoverable()
        {
            Assert.IsNull(LoggerHolder.FindFileTarget(null));
            Assert.IsNull(LoggerHolder.FindFileTarget(
                new LoggingConfiguration()));
        }

        [TestMethod]
        public void RuntimeDoesNotExposeScheduledTaskMutationHelpers()
        {
            const BindingFlags callable = BindingFlags.Public |
                BindingFlags.NonPublic | BindingFlags.Static;

            Assert.IsNull(typeof(StartupMethods).GetMethod(
                "RetargetExistingTaskToCurrentExecutable", callable));
            Assert.IsNull(typeof(StartupMethods).GetMethod(
                "WriteTaskEntry", callable));
            Assert.IsNull(typeof(StartupMethods).GetMethod(
                "DeleteTaskEntry", callable));
            Assert.IsNull(typeof(ViiperSetupManager).GetMethod(
                "TryRunStartupTaskRegistrationHelper", callable));
            Assert.IsNull(typeof(ViiperSetupManager).GetMethod(
                "RefreshSelectedStartupTaskOnLaunch", callable));
        }

        [TestMethod]
        public void StartupShortcutUsesProductDisplayName()
        {
            Assert.AreEqual(ProductIdentity.StartupShortcutFileName,
                Path.GetFileName(StartupMethods.lnkpath));
        }

        [TestMethod]
        public void RuntimeDoesNotAdoptAnotherProductsStartupShortcut()
        {
            // Reading, migrating or deleting the DS4Windows or DS4Windows
            // Reworked Startup shortcut made an ordinary preference change
            // mutate another product. That belongs to an explicit,
            // owner-visible replacement flow, not to this class.
            const BindingFlags callable = BindingFlags.Public |
                BindingFlags.NonPublic | BindingFlags.Static;

            Assert.IsNull(typeof(StartupMethods).GetMethod(
                "MigrateLegacyStartProgEntry", callable));
            Assert.IsNull(typeof(StartupMethods).GetField(
                "legacyLnkPath", callable));
            Assert.IsNull(typeof(StartupMethods).GetField(
                "LegacyStartupShortcutName", callable));
        }
    }
}
