using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DS4Windows.Tests
{
    // These tests exercise the production orchestration seam with a simulated
    // dialog callback. They never start App, VIIPER, drivers or an installer.
    [TestClass]
    public class ViiperSetupPromptSessionTests
    {
        [DataTestMethod]
        [DataRow(false, false, false, false, true)]
        [DataRow(false, false, false, true, true)]
        [DataRow(false, false, true, false, false)]
        [DataRow(false, false, true, true, true)]
        [DataRow(false, true, false, false, false)]
        [DataRow(false, true, false, true, true)]
        [DataRow(false, true, true, false, false)]
        [DataRow(false, true, true, true, true)]
        [DataRow(true, false, false, false, false)]
        [DataRow(true, false, false, true, true)]
        [DataRow(true, false, true, false, false)]
        [DataRow(true, false, true, true, true)]
        [DataRow(true, true, false, false, false)]
        [DataRow(true, true, false, true, true)]
        [DataRow(true, true, true, false, false)]
        [DataRow(true, true, true, true, true)]
        public void PromptMatrixPreservesReadinessAndManualRepair(bool ready,
            bool suppressed, bool alreadyShown, bool forced, bool expectedPrompt)
        {
            var session = new ViiperSetupPromptSession();
            if (alreadyShown)
                Assert.IsTrue(session.TryBeginPrompt(false, false, false));
            var status = HealthyStatus();
            status.ServerRunning = ready;
            int prompts = 0;
            bool result = ViiperSetupManager.EnsureReadyWithPromptCore(status,
                session, suppressed, forced, () => { prompts++; return status.Ready; });
            Assert.AreEqual(ready, result, "Notification preferences cannot change readiness.");
            Assert.AreEqual(expectedPrompt ? 1 : 0, prompts);
        }

        [TestMethod]
        public void ContinuingWithoutOutputDoesNotPromptAgainDuringLateChecks()
        {
            var session = new ViiperSetupPromptSession();
            var status = HealthyStatus();
            status.ServerRunning = false;
            int prompts = 0;
            // App startup, MainWindow.LateChecks and a later automatic call.
            for (int i = 0; i < 3; i++)
                Assert.IsFalse(ViiperSetupManager.EnsureReadyWithPromptCore(status,
                    session, false, false, () => { prompts++; return false; }));
            Assert.AreEqual(1, prompts);
        }

        [TestMethod]
        public void HealthyAndSuppressedChecksDoNotConsumeTheFirstAutomaticPrompt()
        {
            var session = new ViiperSetupPromptSession();
            var status = HealthyStatus();
            Func<bool> unexpected = () => throw new AssertFailedException("No automatic dialog expected.");
            Assert.IsTrue(ViiperSetupManager.EnsureReadyWithPromptCore(status,
                session, false, false, unexpected));
            status.ServerRunning = false;
            Assert.IsFalse(ViiperSetupManager.EnsureReadyWithPromptCore(status,
                session, true, false, unexpected));
            int prompts = 0;
            for (int i = 0; i < 2; i++)
                Assert.IsFalse(ViiperSetupManager.EnsureReadyWithPromptCore(status,
                    session, false, false, () => { prompts++; return false; }));
            Assert.AreEqual(1, prompts, "Re-enabling the preference can show the first prompt.");
        }

        [TestMethod]
        public void ManualRepairCanReturnPendingEvenWhenCurrentOutputIsHealthy()
        {
            var session = new ViiperSetupPromptSession();
            Assert.IsTrue(session.TryBeginPrompt(false, false, false));
            int prompts = 0;
            Assert.IsFalse(ViiperSetupManager.EnsureReadyWithPromptCore(HealthyStatus(),
                session, true, true, () => { prompts++; return false; }));
            Assert.AreEqual(1, prompts, "Manual setup overrides suppression and the session latch.");
        }

        [DataTestMethod]
        [DataRow("missing-viiper")]
        [DataRow("viiper-mismatch")]
        [DataRow("missing-usbip")]
        [DataRow("usbip-executable")]
        [DataRow("usbip-driver")]
        [DataRow("usbip-runtime")]
        [DataRow("foreign-process")]
        [DataRow("citrix-conflict")]
        [DataRow("server-stopped")]
        public void SuppressedDialogsStillRejectEveryUnhealthyPrerequisite(string fault)
        {
            var status = UnhealthyStatus(fault);
            Assert.IsFalse(status.Ready);
            string detail = status.DisplayText;
            Assert.IsFalse(ViiperSetupManager.EnsureReadyWithPromptCore(status,
                new ViiperSetupPromptSession(), true, false,
                () => throw new AssertFailedException("The user disabled automatic setup dialogs.")));
            Assert.IsFalse(status.Ready);
            Assert.AreEqual(detail, status.DisplayText, "The failure remains available in diagnostics.");
            Assert.IsTrue(DS4WinWPF.DS4Forms.MainWindow.ShouldShowGameOutputWarning(true, status.Ready),
                "Suppressing automatic dialogs must not suppress the unavailable-output warning.");
        }

        [TestMethod]
        public void ADialogCallbackCannotMakeUnverifiedOutputReady()
        {
            var status = UnhealthyStatus("viiper-mismatch");
            Assert.IsTrue(ViiperSetupManager.RequiresVerifiedViiperUpdate(status));
            Assert.IsFalse(ViiperSetupManager.EnsureReadyWithPromptCore(status,
                new ViiperSetupPromptSession(), false, true, () => true));
        }

        [TestMethod]
        public void ConcurrentAutomaticChecksClaimOnlyOneDialog()
        {
            var session = new ViiperSetupPromptSession();
            var status = UnhealthyStatus("server-stopped");
            int prompts = 0;
            Parallel.For(0, 32, _ =>
                Assert.IsFalse(ViiperSetupManager.EnsureReadyWithPromptCore(status,
                    session, false, false, () => { Interlocked.Increment(ref prompts); return false; })));
            Assert.AreEqual(1, prompts);
        }

        [TestMethod]
        public void ANewSessionOrVerifiedRepairAllowsOneNewAutomaticPrompt()
        {
            var status = UnhealthyStatus("server-stopped");
            int prompts = 0;
            Func<bool> dismiss = () => { prompts++; return false; };
            var session = new ViiperSetupPromptSession();
            Assert.IsFalse(ViiperSetupManager.EnsureReadyWithPromptCore(status, session, false, false, dismiss));
            Assert.IsFalse(ViiperSetupManager.EnsureReadyWithPromptCore(status, session, false, false, dismiss));
            Assert.AreEqual(1, prompts);
            status.ServerRunning = true;
            Assert.IsTrue(status.Ready);
            session.Reset(); // Production resets only after verified installer success.
            status.ServerRunning = false;
            Assert.IsFalse(ViiperSetupManager.EnsureReadyWithPromptCore(status, session, false, false, dismiss));
            Assert.AreEqual(2, prompts);
            Assert.IsFalse(ViiperSetupManager.EnsureReadyWithPromptCore(status,
                new ViiperSetupPromptSession(), false, false, dismiss));
            Assert.AreEqual(3, prompts);
            Assert.IsFalse(ViiperSetupManager.EnsureReadyWithPromptCore(status,
                new ViiperSetupPromptSession(), true, false, dismiss));
            Assert.AreEqual(3, prompts, "The saved preference still applies to a new process session.");
        }

        [TestMethod]
        public void InvalidInputsNeverInvokeTheDialog()
        {
            var status = HealthyStatus();
            var session = new ViiperSetupPromptSession();
            int prompts = 0;
            Func<bool> callback = () => { prompts++; return true; };
            Assert.ThrowsException<ArgumentNullException>(() =>
                ViiperSetupManager.EnsureReadyWithPromptCore(null, session, false, false, callback));
            Assert.ThrowsException<ArgumentNullException>(() =>
                ViiperSetupManager.EnsureReadyWithPromptCore(status, null, false, false, callback));
            Assert.ThrowsException<ArgumentNullException>(() =>
                ViiperSetupManager.EnsureReadyWithPromptCore(status, session, false, false, null));
            Assert.AreEqual(0, prompts);
            Assert.IsTrue(session.TryBeginPrompt(false, false, false));
        }

        private static ViiperPrerequisiteStatus HealthyStatus() => new()
        {
            ViiperInstalled = true, ViiperPackageCurrent = true, ServerRunning = true,
            UsbipInstalled = true, UsbipExecutableSafe = true, UsbipDriverFilesSafe = true,
            UsbipRuntimeReady = true, ViiperStartupTaskReady = true,
        };

        private static ViiperPrerequisiteStatus UnhealthyStatus(string fault)
        {
            var status = HealthyStatus();
            switch (fault)
            {
                case "missing-viiper": status.ViiperInstalled = false; break;
                case "viiper-mismatch": status.ViiperPackageCurrent = false; break;
                case "missing-usbip": status.UsbipInstalled = false; break;
                case "usbip-executable": status.UsbipExecutableSafe = false; break;
                case "usbip-driver": status.UsbipDriverFilesSafe = false; break;
                case "usbip-runtime": status.UsbipRuntimeReady = false; break;
                case "foreign-process": status.ViiperProcessConflict = true; break;
                case "citrix-conflict": status.CitrixUsbMonitorConflict = true; break;
                case "server-stopped": status.ServerRunning = false; break;
                default: throw new ArgumentOutOfRangeException(nameof(fault));
            }
            return status;
        }
    }
}
