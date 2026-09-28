using DS4Windows;

namespace DS4WindowsTests
{
    [TestClass]
    public class ControllerExposureTransitionTests
    {
        [DataTestMethod]
        [DataRow(true, false, true,
            VirtualOutputBlockReason.OutputBindingFailed, true)]
        [DataRow(true, true, false,
            VirtualOutputBlockReason.None, true)]
        [DataRow(true, false, true,
            VirtualOutputBlockReason.None, false)]
        [DataRow(true, false, false,
            VirtualOutputBlockReason.OutputBindingFailed, false)]
        [DataRow(false, false, true,
            VirtualOutputBlockReason.OutputBindingFailed, false)]
        public void NativeExposurePreflightAllowsOnlyProvenLiveOrFailedOutput(
            bool profileRequestsVirtualOutput, bool liveVirtualOutput,
            bool noPrimaryOutput, VirtualOutputBlockReason blockReason,
            bool expected)
        {
            Assert.AreEqual(expected, ControllerExposureEntryPolicy.
                CanAttemptNativePhysical(profileRequestsVirtualOutput,
                    liveVirtualOutput, noPrimaryOutput, blockReason));
        }

        [TestMethod]
        public void DefaultsToManagedVirtualReady()
        {
            ControllerExposureTransitionCoordinator coordinator = new();

            Assert.AreEqual(ControllerExposureMode.ManagedVirtual,
                coordinator.Status.Mode);
            Assert.AreEqual(ControllerExposureStage.ManagedVirtualReady,
                coordinator.Status.Stage);
            Assert.IsTrue(coordinator.Status.IsReady);
        }

        [TestMethod]
        public void NativeTransitionRetiresOutputBeforeReleasingAndExposingPhysical()
        {
            RecordingOperations operations = new();
            ControllerExposureTransitionCoordinator coordinator = new();

            ControllerExposureTransitionResult result = coordinator.TransitionTo(
                ControllerExposureMode.NativePhysical, operations);

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(ControllerExposureMode.NativePhysical,
                result.Status.Mode);
            Assert.AreEqual(ControllerExposureStage.NativePhysicalReady,
                result.Status.Stage);
            Assert.AreEqual(
                "The physical controller is released to Windows; no PureDS4 virtual output is active. Launch the game after switching.",
                result.Status.Detail);
            CollectionAssert.AreEqual(new[]
            {
                "NeutralizeSyntheticOutputs",
                "QuiescePhysicalInput",
                "RetireVirtualOutputs",
                "ReleasePhysicalHandle",
                "ReleaseOwnedPhysicalContainment",
            }, operations.Calls);
        }

        [TestMethod]
        public void ManagedTransitionContainsPhysicalBeforeOpeningAndCreatingOutput()
        {
            RecordingOperations operations = new();
            ControllerExposureTransitionCoordinator coordinator = NativeCoordinator();
            operations.Calls.Clear();

            ControllerExposureTransitionResult result = coordinator.TransitionTo(
                ControllerExposureMode.ManagedVirtual, operations);

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(ControllerExposureMode.ManagedVirtual,
                result.Status.Mode);
            CollectionAssert.AreEqual(new[]
            {
                "AcquirePhysicalContainment",
                "AcquirePhysicalHandle",
                "CreateVirtualOutputs",
            }, operations.Calls);
        }

        [TestMethod]
        public void TransitionToCurrentModeIsIdempotent()
        {
            RecordingOperations operations = new();
            ControllerExposureTransitionCoordinator coordinator = new();

            ControllerExposureTransitionResult result = coordinator.TransitionTo(
                ControllerExposureMode.ManagedVirtual, operations);

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(0, operations.Calls.Count);
        }

        [TestMethod]
        public void FailedPhysicalReleaseRestoresManagedVirtualOutput()
        {
            RecordingOperations operations = new();
            operations.Fail("ReleasePhysicalHandle", "handle busy");
            ControllerExposureTransitionCoordinator coordinator = new();

            ControllerExposureTransitionResult result = coordinator.TransitionTo(
                ControllerExposureMode.NativePhysical, operations);

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(ControllerExposureMode.ManagedVirtual,
                result.Status.Mode);
            Assert.AreEqual(ControllerExposureStage.ManagedVirtualReady,
                result.Status.Stage);
            CollectionAssert.AreEqual(new[]
            {
                "NeutralizeSyntheticOutputs",
                "QuiescePhysicalInput",
                "RetireVirtualOutputs",
                "ReleasePhysicalHandle",
                "AcquirePhysicalContainment",
                "AcquirePhysicalHandle",
                "ResumePhysicalInput",
                "CreateVirtualOutputs",
            }, operations.Calls);
        }

        [TestMethod]
        public void FailedVirtualRetirementResumesReaderBeforeRestoringOutput()
        {
            RecordingOperations operations = new();
            operations.Fail("RetireVirtualOutputs", "output busy");
            ControllerExposureTransitionCoordinator coordinator = new();

            ControllerExposureTransitionResult result = coordinator.TransitionTo(
                ControllerExposureMode.NativePhysical, operations);

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(ControllerExposureMode.ManagedVirtual,
                result.Status.Mode);
            CollectionAssert.AreEqual(new[]
            {
                "NeutralizeSyntheticOutputs",
                "QuiescePhysicalInput",
                "RetireVirtualOutputs",
                "AcquirePhysicalContainment",
                "ResumePhysicalInput",
                "CreateVirtualOutputs",
            }, operations.Calls);
        }

        [TestMethod]
        public void FailedExposureReacquiresPhysicalBeforeRestoringVirtualOutput()
        {
            RecordingOperations operations = new();
            operations.Fail("ReleaseOwnedPhysicalContainment", "driver busy");
            ControllerExposureTransitionCoordinator coordinator = new();

            ControllerExposureTransitionResult result = coordinator.TransitionTo(
                ControllerExposureMode.NativePhysical, operations);

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(ControllerExposureMode.ManagedVirtual,
                result.Status.Mode);
            CollectionAssert.AreEqual(new[]
            {
                "NeutralizeSyntheticOutputs",
                "QuiescePhysicalInput",
                "RetireVirtualOutputs",
                "ReleasePhysicalHandle",
                "ReleaseOwnedPhysicalContainment",
                "AcquirePhysicalContainment",
                "AcquirePhysicalHandle",
                "ResumePhysicalInput",
                "CreateVirtualOutputs",
            }, operations.Calls);
        }

        [TestMethod]
        public void ManagedContainmentFailureNeverCreatesVirtualOutput()
        {
            RecordingOperations operations = new();
            ControllerExposureTransitionCoordinator coordinator = NativeCoordinator();
            operations.Calls.Clear();
            operations.Fail("AcquirePhysicalContainment", "HidHide unavailable");

            ControllerExposureTransitionResult result = coordinator.TransitionTo(
                ControllerExposureMode.ManagedVirtual, operations);

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(ControllerExposureMode.NativePhysical,
                result.Status.Mode);
            CollectionAssert.AreEqual(new[]
            {
                "AcquirePhysicalContainment",
                "ReleaseOwnedPhysicalContainment",
            }, operations.Calls);
        }

        [TestMethod]
        public void FailedPhysicalAcquireReturnsToNative()
        {
            RecordingOperations operations = new();
            ControllerExposureTransitionCoordinator coordinator = NativeCoordinator();
            operations.Calls.Clear();
            operations.Fail("AcquirePhysicalHandle", "device unavailable");

            ControllerExposureTransitionResult result = coordinator.TransitionTo(
                ControllerExposureMode.ManagedVirtual, operations);

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(ControllerExposureMode.NativePhysical,
                result.Status.Mode);
            CollectionAssert.AreEqual(new[]
            {
                "AcquirePhysicalContainment",
                "AcquirePhysicalHandle",
                "ReleasePhysicalHandle",
                "ReleaseOwnedPhysicalContainment",
            }, operations.Calls);
        }

        [TestMethod]
        public void FailedVirtualCreationRemovesPartialOutputBeforeReturningNative()
        {
            RecordingOperations operations = new();
            ControllerExposureTransitionCoordinator coordinator = NativeCoordinator();
            operations.Calls.Clear();
            operations.Fail("CreateVirtualOutputs", "VIIPER unavailable");

            ControllerExposureTransitionResult result = coordinator.TransitionTo(
                ControllerExposureMode.ManagedVirtual, operations);

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(ControllerExposureMode.NativePhysical,
                result.Status.Mode);
            CollectionAssert.AreEqual(new[]
            {
                "AcquirePhysicalContainment",
                "AcquirePhysicalHandle",
                "CreateVirtualOutputs",
                "QuiescePhysicalInput",
                "RetireVirtualOutputs",
                "ReleasePhysicalHandle",
                "ReleaseOwnedPhysicalContainment",
            }, operations.Calls);
        }

        [TestMethod]
        public void RollbackFailureRequiresRecoveryAndBlocksFurtherTransitions()
        {
            RecordingOperations operations = new();
            operations.Fail("ReleasePhysicalHandle", "handle busy");
            operations.Fail("CreateVirtualOutputs", "VIIPER unavailable");
            ControllerExposureTransitionCoordinator coordinator = new();

            ControllerExposureTransitionResult result = coordinator.TransitionTo(
                ControllerExposureMode.NativePhysical, operations);
            int callsAfterFailure = operations.Calls.Count;
            ControllerExposureTransitionResult retry = coordinator.TransitionTo(
                ControllerExposureMode.NativePhysical, operations);

            Assert.IsFalse(result.Succeeded);
            Assert.IsTrue(result.Status.NeedsRecovery);
            Assert.IsFalse(retry.Succeeded);
            Assert.AreEqual(callsAfterFailure, operations.Calls.Count);
        }

        [TestMethod]
        public void FailedManagedRollbackHandleDoesNotAttemptVirtualOutput()
        {
            RecordingOperations operations = new();
            operations.Fail("ReleaseOwnedPhysicalContainment", "driver busy");
            operations.Fail("AcquirePhysicalHandle", "device unavailable");
            ControllerExposureTransitionCoordinator coordinator = new();

            ControllerExposureTransitionResult result = coordinator.TransitionTo(
                ControllerExposureMode.NativePhysical, operations);

            Assert.IsTrue(result.Status.NeedsRecovery);
            CollectionAssert.AreEqual(new[]
            {
                "NeutralizeSyntheticOutputs",
                "QuiescePhysicalInput",
                "RetireVirtualOutputs",
                "ReleasePhysicalHandle",
                "ReleaseOwnedPhysicalContainment",
                "AcquirePhysicalContainment",
                "AcquirePhysicalHandle",
            }, operations.Calls);
        }

        [TestMethod]
        public void FailedManagedRollbackContainmentNeverOpensPhysicalOrVirtual()
        {
            RecordingOperations operations = new();
            operations.Fail("ReleaseOwnedPhysicalContainment", "driver busy");
            operations.Fail("AcquirePhysicalContainment", "cannot re-hide");
            ControllerExposureTransitionCoordinator coordinator = new();

            ControllerExposureTransitionResult result = coordinator.TransitionTo(
                ControllerExposureMode.NativePhysical, operations);

            Assert.IsTrue(result.Status.NeedsRecovery);
            CollectionAssert.AreEqual(new[]
            {
                "NeutralizeSyntheticOutputs",
                "QuiescePhysicalInput",
                "RetireVirtualOutputs",
                "ReleasePhysicalHandle",
                "ReleaseOwnedPhysicalContainment",
                "AcquirePhysicalContainment",
            }, operations.Calls);
        }

        [TestMethod]
        public void ServiceResetReturnsCoordinatorToManagedDefault()
        {
            RecordingOperations operations = new();
            operations.Fail("ReleasePhysicalHandle", "handle busy");
            operations.Fail("CreateVirtualOutputs", "output unavailable");
            ControllerExposureTransitionCoordinator coordinator = new();
            ControllerExposureTransitionResult failed =
                coordinator.TransitionTo(
                    ControllerExposureMode.NativePhysical, operations);
            Assert.IsTrue(failed.Status.NeedsRecovery);

            coordinator.ResetToManagedVirtual();

            Assert.AreEqual(ControllerExposureMode.ManagedVirtual,
                coordinator.Status.Mode);
            Assert.AreEqual(ControllerExposureStage.ManagedVirtualReady,
                coordinator.Status.Stage);
        }

        [TestMethod]
        public void FailedPartialOutputRetirementDoesNotExposePhysical()
        {
            RecordingOperations operations = new();
            ControllerExposureTransitionCoordinator coordinator = NativeCoordinator();
            operations.Calls.Clear();
            operations.Fail("CreateVirtualOutputs", "VIIPER unavailable");
            operations.Fail("RetireVirtualOutputs", "virtual output still visible");

            ControllerExposureTransitionResult result = coordinator.TransitionTo(
                ControllerExposureMode.ManagedVirtual, operations);

            Assert.IsTrue(result.Status.NeedsRecovery);
            CollectionAssert.AreEqual(new[]
            {
                "AcquirePhysicalContainment",
                "AcquirePhysicalHandle",
                "CreateVirtualOutputs",
                "QuiescePhysicalInput",
                "RetireVirtualOutputs",
            }, operations.Calls);
        }

        [TestMethod]
        public void OperationExceptionUsesTheSameRollbackPath()
        {
            RecordingOperations operations = new();
            operations.Throw("ReleasePhysicalHandle",
                new InvalidOperationException("lost device"));
            ControllerExposureTransitionCoordinator coordinator = new();

            ControllerExposureTransitionResult result = coordinator.TransitionTo(
                ControllerExposureMode.NativePhysical, operations);

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(ControllerExposureMode.ManagedVirtual,
                result.Status.Mode);
            StringAssert.Contains(result.Status.Detail, "InvalidOperationException");
        }

        [TestMethod]
        public void SyntheticOutputMustNeutralizeBeforeAnyOwnershipChange()
        {
            RecordingOperations operations = new();
            operations.Fail("NeutralizeSyntheticOutputs", "keys still pressed");
            ControllerExposureTransitionCoordinator coordinator = new();

            ControllerExposureTransitionResult result = coordinator.TransitionTo(
                ControllerExposureMode.NativePhysical, operations);

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(ControllerExposureMode.ManagedVirtual,
                result.Status.Mode);
            CollectionAssert.AreEqual(new[] { "NeutralizeSyntheticOutputs" },
                operations.Calls);
        }

        [TestMethod]
        public void InputMustQuiesceBeforeVirtualOutputIsRetired()
        {
            RecordingOperations operations = new();
            operations.Fail("QuiescePhysicalInput", "reader busy");
            ControllerExposureTransitionCoordinator coordinator = new();

            ControllerExposureTransitionResult result = coordinator.TransitionTo(
                ControllerExposureMode.NativePhysical, operations);

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(ControllerExposureMode.ManagedVirtual,
                result.Status.Mode);
            CollectionAssert.AreEqual(new[]
            {
                "NeutralizeSyntheticOutputs",
                "QuiescePhysicalInput",
            }, operations.Calls);
        }

        [TestMethod]
        public void StatusReadDoesNotWaitForInProgressTransitionOperation()
        {
            using ManualResetEventSlim operationEntered = new(false);
            using ManualResetEventSlim releaseOperation = new(false);
            RecordingOperations operations = new();
            operations.On("ReleasePhysicalHandle", () =>
            {
                operationEntered.Set();
                if (!releaseOperation.Wait(TimeSpan.FromSeconds(5)))
                {
                    throw new TimeoutException(
                        "The test did not release the transition operation.");
                }
            });
            ControllerExposureTransitionCoordinator coordinator = new();
            Task<ControllerExposureTransitionResult> transition = Task.Run(() =>
                coordinator.TransitionTo(
                    ControllerExposureMode.NativePhysical, operations));

            Assert.IsTrue(operationEntered.Wait(TimeSpan.FromSeconds(2)),
                "The transition did not reach the blocking operation.");
            try
            {
                ControllerExposureStatus? observedStatus = null;
                Exception statusReadFailure = null;
                using ManualResetEventSlim statusReadCompleted = new(false);
                Thread statusReader = new(() =>
                {
                    try
                    {
                        observedStatus = coordinator.Status;
                    }
                    catch (Exception ex)
                    {
                        statusReadFailure = ex;
                    }
                    finally
                    {
                        statusReadCompleted.Set();
                    }
                })
                {
                    IsBackground = true,
                };
                statusReader.Start();
                Assert.IsTrue(statusReadCompleted.Wait(TimeSpan.FromSeconds(2)),
                    "Reading transition status waited for the active operation and can deadlock the UI dispatcher.");
                Assert.IsNull(statusReadFailure);
                Assert.AreEqual(
                    ControllerExposureStage.ReleasingPhysicalHandle,
                    observedStatus.Value.Stage);
                Assert.IsTrue(statusReader.Join(TimeSpan.FromSeconds(2)),
                    "The completed status reader did not exit.");
            }
            finally
            {
                releaseOperation.Set();
            }

            Assert.IsTrue(transition.Wait(TimeSpan.FromSeconds(2)),
                "The transition did not finish after the operation was released.");
            Assert.IsTrue(transition.Result.Succeeded);
        }

        [TestMethod]
        public void FailedReaderResumeDoesNotRecreateVirtualOutput()
        {
            RecordingOperations operations = new();
            operations.Fail("ReleasePhysicalHandle", "handle busy");
            operations.Fail("ResumePhysicalInput", "reader unavailable");
            ControllerExposureTransitionCoordinator coordinator = new();

            ControllerExposureTransitionResult result = coordinator.TransitionTo(
                ControllerExposureMode.NativePhysical, operations);

            Assert.IsTrue(result.Status.NeedsRecovery);
            CollectionAssert.AreEqual(new[]
            {
                "NeutralizeSyntheticOutputs",
                "QuiescePhysicalInput",
                "RetireVirtualOutputs",
                "ReleasePhysicalHandle",
                "AcquirePhysicalContainment",
                "AcquirePhysicalHandle",
                "ResumePhysicalInput",
            }, operations.Calls);
        }

        [TestMethod]
        public void RecoveryWorkflowStopsBeforeRestartingManagedService()
        {
            RecordingRecoveryOperations operations = new();

            ControllerExposureRecoveryResult result =
                ControllerExposureRecoveryWorkflow.Recover(
                    recoveryRequired: true, operations);

            Assert.IsTrue(result.Succeeded);
            CollectionAssert.AreEqual(new[]
            {
                "StopAndReset",
                "StartManagedService",
            }, operations.Calls);
        }

        [TestMethod]
        public void RecoveryWorkflowRefusesHealthyStateWithoutMutation()
        {
            RecordingRecoveryOperations operations = new();

            ControllerExposureRecoveryResult result =
                ControllerExposureRecoveryWorkflow.Recover(
                    recoveryRequired: false, operations);

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(0, operations.Calls.Count);
            StringAssert.Contains(result.Detail, "does not require");
        }

        [TestMethod]
        public void RecoveryWorkflowDoesNotRestartAfterStopFailure()
        {
            RecordingRecoveryOperations operations = new();
            operations.Fail("StopAndReset", "cleanup blocked");

            ControllerExposureRecoveryResult result =
                ControllerExposureRecoveryWorkflow.Recover(
                    recoveryRequired: true, operations);

            Assert.IsFalse(result.Succeeded);
            CollectionAssert.AreEqual(new[] { "StopAndReset" },
                operations.Calls);
            StringAssert.Contains(result.Detail, "cleanup blocked");
        }

        [TestMethod]
        public void RecoveryWorkflowReportsSafeStopWhenRestartFails()
        {
            RecordingRecoveryOperations operations = new();
            operations.Fail("StartManagedService", "backend unavailable");

            ControllerExposureRecoveryResult result =
                ControllerExposureRecoveryWorkflow.Recover(
                    recoveryRequired: true, operations);

            Assert.IsFalse(result.Succeeded);
            CollectionAssert.AreEqual(new[]
            {
                "StopAndReset",
                "StartManagedService",
            }, operations.Calls);
            StringAssert.Contains(result.Detail.ToLowerInvariant(),
                "cleanup completed");
            StringAssert.Contains(result.Detail, "backend unavailable");
        }

        [TestMethod]
        public void RecoveryPostconditionRequiresOriginalController()
        {
            ControllerExposureOperationResult result =
                ControllerExposureRecoveryPostcondition.Evaluate(
                    controllerPresent: false, outputConnected: false,
                    OutContType.None, OutContType.X360);

            Assert.IsFalse(result.Succeeded);
            StringAssert.Contains(result.Error, "did not reconnect");
        }

        [TestMethod]
        public void RecoveryPostconditionRequiresConnectedVirtualOutput()
        {
            ControllerExposureOperationResult result =
                ControllerExposureRecoveryPostcondition.Evaluate(
                    controllerPresent: true, outputConnected: false,
                    OutContType.X360, OutContType.X360);

            Assert.IsFalse(result.Succeeded);
            StringAssert.Contains(result.Error, "did not become ready");
        }

        [TestMethod]
        public void RecoveryPostconditionRequiresRequestedOutputType()
        {
            ControllerExposureOperationResult result =
                ControllerExposureRecoveryPostcondition.Evaluate(
                    controllerPresent: true, outputConnected: true,
                    OutContType.DS4, OutContType.X360);

            Assert.IsFalse(result.Succeeded);
        }

        [TestMethod]
        public void RecoveryPostconditionAcceptsReadyManagedVirtualState()
        {
            ControllerExposureOperationResult result =
                ControllerExposureRecoveryPostcondition.Evaluate(
                    controllerPresent: true, outputConnected: true,
                    OutContType.X360, OutContType.X360);

            Assert.IsTrue(result.Succeeded);
        }

        private static ControllerExposureTransitionCoordinator NativeCoordinator()
        {
            ControllerExposureTransitionCoordinator coordinator = new();
            ControllerExposureTransitionResult result = coordinator.TransitionTo(
                ControllerExposureMode.NativePhysical, new RecordingOperations());
            Assert.IsTrue(result.Succeeded);
            return coordinator;
        }

        private sealed class RecordingOperations :
            IControllerExposureTransitionOperations
        {
            private readonly Dictionary<string, string> failures =
                new(StringComparer.Ordinal);
            private readonly Dictionary<string, Exception> exceptions =
                new(StringComparer.Ordinal);
            private readonly Dictionary<string, Action> actions =
                new(StringComparer.Ordinal);

            internal List<string> Calls { get; } = new();

            internal void Fail(string operation, string error) =>
                failures[operation] = error;

            internal void Throw(string operation, Exception exception) =>
                exceptions[operation] = exception;

            internal void On(string operation, Action action) =>
                actions[operation] = action;

            public ControllerExposureOperationResult RetireVirtualOutputs() =>
                Run(nameof(RetireVirtualOutputs));

            public ControllerExposureOperationResult NeutralizeSyntheticOutputs() =>
                Run(nameof(NeutralizeSyntheticOutputs));

            public ControllerExposureOperationResult QuiescePhysicalInput() =>
                Run(nameof(QuiescePhysicalInput));

            public ControllerExposureOperationResult ReleasePhysicalHandle() =>
                Run(nameof(ReleasePhysicalHandle));

            public ControllerExposureOperationResult ReleasePhysicalContainment() =>
                Run("ReleaseOwnedPhysicalContainment");

            public ControllerExposureOperationResult AcquirePhysicalContainment() =>
                Run(nameof(AcquirePhysicalContainment));

            public ControllerExposureOperationResult AcquirePhysicalHandle() =>
                Run(nameof(AcquirePhysicalHandle));

            public ControllerExposureOperationResult ResumePhysicalInput() =>
                Run(nameof(ResumePhysicalInput));

            public ControllerExposureOperationResult CreateVirtualOutputs() =>
                Run(nameof(CreateVirtualOutputs));

            private ControllerExposureOperationResult Run(string operation)
            {
                Calls.Add(operation);
                if (actions.TryGetValue(operation, out Action action))
                {
                    action();
                }
                if (exceptions.TryGetValue(operation, out Exception exception))
                {
                    throw exception;
                }

                return failures.TryGetValue(operation, out string error)
                    ? ControllerExposureOperationResult.Failure(error)
                    : ControllerExposureOperationResult.Success();
            }
        }

        private sealed class RecordingRecoveryOperations :
            IControllerExposureRecoveryOperations
        {
            private readonly Dictionary<string, string> failures =
                new(StringComparer.Ordinal);

            internal List<string> Calls { get; } = new();

            internal void Fail(string operation, string error) =>
                failures[operation] = error;

            public ControllerExposureOperationResult StopAndReset() =>
                Run(nameof(StopAndReset));

            public ControllerExposureOperationResult StartManagedService() =>
                Run(nameof(StartManagedService));

            private ControllerExposureOperationResult Run(string operation)
            {
                Calls.Add(operation);
                return failures.TryGetValue(operation, out string error)
                    ? ControllerExposureOperationResult.Failure(error)
                    : ControllerExposureOperationResult.Success();
            }
        }
    }
}
