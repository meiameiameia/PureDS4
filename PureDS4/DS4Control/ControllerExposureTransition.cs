using System;
using System.Collections.Generic;

namespace DS4Windows
{
    public enum ControllerExposureMode : byte
    {
        ManagedVirtual,
        NativePhysical,
    }

    public enum ControllerExposureStage : byte
    {
        ManagedVirtualReady,
        NativePhysicalReady,
        NeutralizingSyntheticOutputs,
        QuiescingPhysicalInput,
        RetiringVirtualOutputs,
        ReleasingPhysicalHandle,
        ExposingPhysicalController,
        ContainingPhysicalController,
        AcquiringPhysicalHandle,
        CreatingVirtualOutputs,
        RecoveryRequired,
    }

    internal readonly struct ControllerExposureOperationResult
    {
        private ControllerExposureOperationResult(bool succeeded, string error)
        {
            Succeeded = succeeded;
            Error = error ?? string.Empty;
        }

        internal bool Succeeded { get; }
        internal string Error { get; }

        internal static ControllerExposureOperationResult Success() =>
            new ControllerExposureOperationResult(true, string.Empty);

        internal static ControllerExposureOperationResult Failure(string error) =>
            new ControllerExposureOperationResult(false, error);
    }

    /// <summary>
    /// Runtime operations used by a controller exposure transition. Operations
    /// must be idempotent and converge on the state named by the method. A
    /// failure may leave partial work, so the coordinator invokes the safe
    /// inverse before publishing a restored mode. HidHide operations may alter
    /// only configuration created by the current run or an exact external rule
    /// covered by explicit session consent and a durable restore obligation.
    /// </summary>
    internal interface IControllerExposureTransitionOperations
    {
        ControllerExposureOperationResult NeutralizeSyntheticOutputs();
        ControllerExposureOperationResult QuiescePhysicalInput();
        ControllerExposureOperationResult RetireVirtualOutputs();
        ControllerExposureOperationResult ReleasePhysicalHandle();
        ControllerExposureOperationResult ReleasePhysicalContainment();
        ControllerExposureOperationResult AcquirePhysicalContainment();
        ControllerExposureOperationResult AcquirePhysicalHandle();
        ControllerExposureOperationResult ResumePhysicalInput();
        ControllerExposureOperationResult CreateVirtualOutputs();
    }

    internal readonly struct ControllerExposureStatus
    {
        internal ControllerExposureStatus(ControllerExposureMode mode,
            ControllerExposureStage stage, string detail)
        {
            Mode = mode;
            Stage = stage;
            Detail = detail ?? string.Empty;
        }

        internal ControllerExposureMode Mode { get; }
        internal ControllerExposureStage Stage { get; }
        internal string Detail { get; }
        internal bool IsReady => Stage == ControllerExposureStage.ManagedVirtualReady ||
            Stage == ControllerExposureStage.NativePhysicalReady;
        internal bool NeedsRecovery =>
            Stage == ControllerExposureStage.RecoveryRequired;
    }

    internal readonly struct ControllerExposureTransitionResult
    {
        internal ControllerExposureTransitionResult(bool succeeded,
            ControllerExposureStatus status)
        {
            Succeeded = succeeded;
            Status = status;
        }

        internal bool Succeeded { get; }
        internal ControllerExposureStatus Status { get; }
    }

    internal readonly struct ControllerExposureRecoveryResult
    {
        internal ControllerExposureRecoveryResult(bool succeeded,
            string detail)
        {
            Succeeded = succeeded;
            Detail = detail ?? string.Empty;
        }

        internal bool Succeeded { get; }
        internal string Detail { get; }
    }

    internal interface IControllerExposureRecoveryOperations
    {
        ControllerExposureOperationResult StopAndReset();
        ControllerExposureOperationResult StartManagedService();
    }

    /// <summary>
    /// Converges an uncertain exposure state through the normal service
    /// shutdown and startup paths. Recovery never clears the terminal state
    /// without first running the cleanup that restores physical containment,
    /// retires outputs, closes handles, and clears device reservations.
    /// </summary>
    internal static class ControllerExposureRecoveryWorkflow
    {
        internal static ControllerExposureRecoveryResult Recover(
            bool recoveryRequired,
            IControllerExposureRecoveryOperations operations)
        {
            if (!recoveryRequired)
            {
                return Failure(
                    "This controller does not require exposure recovery.");
            }
            if (operations == null)
            {
                throw new ArgumentNullException(nameof(operations));
            }

            ControllerExposureOperationResult stopResult = Invoke(
                operations.StopAndReset);
            if (!stopResult.Succeeded)
            {
                return Failure("Controller handling could not be stopped " +
                    $"safely. {stopResult.Error}");
            }

            ControllerExposureOperationResult startResult = Invoke(
                operations.StartManagedService);
            if (!startResult.Succeeded)
            {
                return Failure("Exposure cleanup completed, but Managed / " +
                    "Virtual could not be restored. " +
                    startResult.Error);
            }

            return new ControllerExposureRecoveryResult(true,
                "Controller handling restarted in Managed / Virtual.");
        }

        private static ControllerExposureOperationResult Invoke(
            Func<ControllerExposureOperationResult> operation)
        {
            try
            {
                return operation();
            }
            catch (Exception ex)
            {
                return ControllerExposureOperationResult.Failure(
                    $"{ex.GetType().Name}: {ex.Message}");
            }
        }

        private static ControllerExposureRecoveryResult Failure(
            string detail) => new ControllerExposureRecoveryResult(false,
                detail);
    }

    internal static class ControllerExposureRecoveryPostcondition
    {
        internal static ControllerExposureOperationResult Evaluate(
            bool controllerPresent, bool outputConnected,
            OutContType actualOutputType, OutContType expectedOutputType)
        {
            if (!controllerPresent)
            {
                return ControllerExposureOperationResult.Failure(
                    "The controller did not reconnect after recovery.");
            }
            if (!outputConnected || actualOutputType.Normalize() !=
                    expectedOutputType.Normalize())
            {
                return ControllerExposureOperationResult.Failure(
                    "The controller reconnected, but its Managed / Virtual " +
                    "output did not become ready.");
            }

            return ControllerExposureOperationResult.Success();
        }
    }

    /// <summary>
    /// Serializes the safety-critical ordering between physical containment and
    /// virtual output. It has no direct dependency on HID, HidHide, or VIIPER so
    /// transition and rollback behavior can be proven without mutating a host.
    /// </summary>
    internal sealed class ControllerExposureTransitionCoordinator
    {
        private readonly object transitionLock = new object();
        private readonly object statusLock = new object();
        private ControllerExposureStatus status = new ControllerExposureStatus(
            ControllerExposureMode.ManagedVirtual,
            ControllerExposureStage.ManagedVirtualReady,
            "Physical containment and virtual output are expected.");

        internal ControllerExposureStatus Status
        {
            get
            {
                lock (statusLock)
                {
                    return status;
                }
            }
        }

        internal ControllerExposureTransitionResult TransitionTo(
            ControllerExposureMode requestedMode,
            IControllerExposureTransitionOperations operations)
        {
            if (operations == null)
            {
                throw new ArgumentNullException(nameof(operations));
            }

            lock (transitionLock)
            {
                ControllerExposureStatus current = Status;
                if (current.NeedsRecovery)
                {
                    return Failed("Recovery is required before another exposure transition.");
                }

                if (current.Mode == requestedMode && current.IsReady)
                {
                    return new ControllerExposureTransitionResult(true,
                        current);
                }

                return requestedMode == ControllerExposureMode.NativePhysical
                    ? TransitionToNativePhysical(operations)
                    : TransitionToManagedVirtual(operations);
            }
        }

        internal void ResetToManagedVirtual()
        {
            lock (transitionLock)
            {
                SetStatus(new ControllerExposureStatus(
                    ControllerExposureMode.ManagedVirtual,
                    ControllerExposureStage.ManagedVirtualReady,
                    "Physical containment and virtual output are expected."));
            }
        }

        private ControllerExposureTransitionResult TransitionToNativePhysical(
            IControllerExposureTransitionOperations operations)
        {
            ControllerExposureOperationResult result = Run(
                ControllerExposureStage.NeutralizingSyntheticOutputs,
                "Releasing mapped keyboard, mouse, and synthetic output state.",
                operations.NeutralizeSyntheticOutputs);
            if (!result.Succeeded)
            {
                return RestoreReady(ControllerExposureMode.ManagedVirtual,
                    "Managed mode was preserved because synthetic output could not be neutralized.",
                    result.Error);
            }

            result = Run(
                ControllerExposureStage.QuiescingPhysicalInput,
                "Quiescing the physical input reader before output teardown.",
                operations.QuiescePhysicalInput);
            if (!result.Succeeded)
            {
                return RestoreReady(ControllerExposureMode.ManagedVirtual,
                    "Managed mode was preserved because the physical input reader could not be quiesced.",
                    result.Error);
            }

            result = Run(
                ControllerExposureStage.RetiringVirtualOutputs,
                "Retiring every game-visible virtual output.",
                operations.RetireVirtualOutputs);
            if (!result.Succeeded)
            {
                return RollBackToManaged(operations, result.Error,
                    acquirePhysicalHandle: false);
            }

            result = Run(ControllerExposureStage.ReleasingPhysicalHandle,
                "Releasing the physical controller from DS4Windows.",
                operations.ReleasePhysicalHandle);
            if (!result.Succeeded)
            {
                return RollBackToManaged(operations, result.Error,
                    acquirePhysicalHandle: true);
            }

            result = Run(ControllerExposureStage.ExposingPhysicalController,
                "Suspending the exact consented physical containment rule.",
                operations.ReleasePhysicalContainment);
            if (!result.Succeeded)
            {
                return RollBackToManaged(operations, result.Error,
                    acquirePhysicalHandle: true);
            }

            ControllerExposureStatus ready = new ControllerExposureStatus(
                ControllerExposureMode.NativePhysical,
                ControllerExposureStage.NativePhysicalReady,
                "The physical controller is released to Windows; no Reworked virtual output is active. Launch the game after switching.");
            SetStatus(ready);
            return new ControllerExposureTransitionResult(true, ready);
        }

        private ControllerExposureTransitionResult TransitionToManagedVirtual(
            IControllerExposureTransitionOperations operations)
        {
            ControllerExposureOperationResult result = Run(
                ControllerExposureStage.ContainingPhysicalController,
                "Restoring physical containment before creating output.",
                operations.AcquirePhysicalContainment);
            if (!result.Succeeded)
            {
                return RollBackToNative(operations, result.Error,
                    retireVirtualOutputs: false,
                    releasePhysicalHandle: false);
            }

            result = Run(ControllerExposureStage.AcquiringPhysicalHandle,
                "Opening the contained physical controller for DS4Windows.",
                operations.AcquirePhysicalHandle);
            if (!result.Succeeded)
            {
                return RollBackToNative(operations, result.Error,
                    retireVirtualOutputs: false,
                    releasePhysicalHandle: true);
            }

            result = Run(ControllerExposureStage.CreatingVirtualOutputs,
                "Creating the requested virtual controller output.",
                operations.CreateVirtualOutputs);
            if (!result.Succeeded)
            {
                return RollBackToNative(operations, result.Error,
                    retireVirtualOutputs: true,
                    releasePhysicalHandle: true);
            }

            ControllerExposureStatus ready = new ControllerExposureStatus(
                ControllerExposureMode.ManagedVirtual,
                ControllerExposureStage.ManagedVirtualReady,
                "The physical controller is contained and virtual output is ready.");
            SetStatus(ready);
            return new ControllerExposureTransitionResult(true, ready);
        }

        private ControllerExposureTransitionResult RollBackToManaged(
            IControllerExposureTransitionOperations operations,
            string transitionError, bool acquirePhysicalHandle)
        {
            List<string> rollbackErrors = new List<string>();
            ControllerExposureOperationResult containmentResult =
                Invoke(operations.AcquirePhysicalContainment);
            AddRollbackError(rollbackErrors, "physical containment",
                containmentResult);
            if (!containmentResult.Succeeded)
            {
                return RequireRecovery(transitionError, rollbackErrors);
            }

            if (acquirePhysicalHandle)
            {
                ControllerExposureOperationResult acquireResult =
                    Invoke(operations.AcquirePhysicalHandle);
                AddRollbackError(rollbackErrors, "physical handle",
                    acquireResult);
                if (!acquireResult.Succeeded)
                {
                    return RequireRecovery(transitionError, rollbackErrors);
                }
            }

            ControllerExposureOperationResult resumeResult =
                Invoke(operations.ResumePhysicalInput);
            AddRollbackError(rollbackErrors, "physical input reader",
                resumeResult);
            if (!resumeResult.Succeeded)
            {
                return RequireRecovery(transitionError, rollbackErrors);
            }

            ControllerExposureOperationResult outputResult =
                Invoke(operations.CreateVirtualOutputs);
            AddRollbackError(rollbackErrors, "virtual output", outputResult);
            if (rollbackErrors.Count > 0)
            {
                return RequireRecovery(transitionError, rollbackErrors);
            }

            return RestoreReady(ControllerExposureMode.ManagedVirtual,
                "Managed mode was restored after the exposure transition failed.",
                transitionError);
        }

        private ControllerExposureTransitionResult RollBackToNative(
            IControllerExposureTransitionOperations operations,
            string transitionError, bool retireVirtualOutputs,
            bool releasePhysicalHandle)
        {
            List<string> rollbackErrors = new List<string>();
            if (retireVirtualOutputs)
            {
                ControllerExposureOperationResult quiesceResult =
                    Invoke(operations.QuiescePhysicalInput);
                AddRollbackError(rollbackErrors, "physical input reader",
                    quiesceResult);
                if (!quiesceResult.Succeeded)
                {
                    return RequireRecovery(transitionError, rollbackErrors);
                }

                ControllerExposureOperationResult retireResult =
                    Invoke(operations.RetireVirtualOutputs);
                AddRollbackError(rollbackErrors, "partial virtual output",
                    retireResult);
                if (!retireResult.Succeeded)
                {
                    return RequireRecovery(transitionError, rollbackErrors);
                }
            }

            if (releasePhysicalHandle)
            {
                ControllerExposureOperationResult releaseResult =
                    Invoke(operations.ReleasePhysicalHandle);
                AddRollbackError(rollbackErrors, "physical handle",
                    releaseResult);
                if (!releaseResult.Succeeded)
                {
                    return RequireRecovery(transitionError, rollbackErrors);
                }
            }

            AddRollbackError(rollbackErrors, "physical containment",
                Invoke(operations.ReleasePhysicalContainment));
            if (rollbackErrors.Count > 0)
            {
                return RequireRecovery(transitionError, rollbackErrors);
            }

            return RestoreReady(ControllerExposureMode.NativePhysical,
                "Native mode was restored after the managed transition failed.",
                transitionError);
        }

        private ControllerExposureTransitionResult RestoreReady(
            ControllerExposureMode restoredMode, string detail,
            string transitionError)
        {
            ControllerExposureStage stage = restoredMode ==
                ControllerExposureMode.ManagedVirtual
                ? ControllerExposureStage.ManagedVirtualReady
                : ControllerExposureStage.NativePhysicalReady;
            ControllerExposureStatus ready = new ControllerExposureStatus(
                restoredMode, stage,
                Combine(detail, transitionError));
            SetStatus(ready);
            return new ControllerExposureTransitionResult(false, ready);
        }

        private ControllerExposureTransitionResult RequireRecovery(
            string transitionError, IReadOnlyCollection<string> rollbackErrors)
        {
            ControllerExposureStatus current = Status;
            ControllerExposureStatus recovery = new ControllerExposureStatus(
                current.Mode,
                ControllerExposureStage.RecoveryRequired,
                Combine(transitionError,
                    $"Rollback failed: {string.Join("; ", rollbackErrors)}"));
            SetStatus(recovery);
            return new ControllerExposureTransitionResult(false, recovery);
        }

        private ControllerExposureTransitionResult Failed(string detail)
        {
            ControllerExposureStatus current = Status;
            ControllerExposureStatus failedStatus = new ControllerExposureStatus(
                current.Mode, current.Stage, detail);
            return new ControllerExposureTransitionResult(false, failedStatus);
        }

        private ControllerExposureOperationResult Run(
            ControllerExposureStage stage, string detail,
            Func<ControllerExposureOperationResult> operation)
        {
            ControllerExposureStatus current = Status;
            SetStatus(new ControllerExposureStatus(current.Mode, stage,
                detail));
            return Invoke(operation);
        }

        private void SetStatus(ControllerExposureStatus value)
        {
            lock (statusLock)
            {
                status = value;
            }
        }

        private static ControllerExposureOperationResult Invoke(
            Func<ControllerExposureOperationResult> operation)
        {
            try
            {
                return operation();
            }
            catch (Exception ex)
            {
                return ControllerExposureOperationResult.Failure(
                    $"{ex.GetType().Name}: {ex.Message}");
            }
        }

        private static void AddRollbackError(ICollection<string> errors,
            string operationName, ControllerExposureOperationResult result)
        {
            if (!result.Succeeded)
            {
                errors.Add($"{operationName}: {result.Error}");
            }
        }

        private static string Combine(string first, string second)
        {
            if (string.IsNullOrWhiteSpace(first)) return second ?? string.Empty;
            if (string.IsNullOrWhiteSpace(second)) return first;
            return $"{first} {second}";
        }
    }
}
