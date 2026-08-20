using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DS4WinWPF.DS4Control;

namespace DS4Windows
{
    public sealed class ControllerExposureSessionInfo
    {
        internal ControllerExposureSessionInfo(string instanceId,
            string displayName, string connection, int slotNumber,
            string modeTitle, string detail, bool canReturnToManaged,
            bool canRecover)
        {
            InstanceId = instanceId ?? string.Empty;
            DisplayName = displayName ?? "Controller";
            Connection = connection ?? string.Empty;
            SlotNumber = slotNumber;
            ModeTitle = modeTitle ?? string.Empty;
            Detail = detail ?? string.Empty;
            CanReturnToManaged = canReturnToManaged;
            CanRecover = canRecover;
        }

        public string InstanceId { get; }
        public string DisplayName { get; }
        public string Connection { get; }
        public int SlotNumber { get; }
        public string SlotText => $"Controller {SlotNumber}";
        public string ModeTitle { get; }
        public string Detail { get; }
        public bool CanReturnToManaged { get; }
        public bool CanRecover { get; }
    }

    public partial class ControlService
    {
        private sealed class ControllerExposureRuntimeSession
        {
            internal string InstanceId { get; init; } = string.Empty;
            internal string DevicePath { get; init; } = string.Empty;
            internal string DisplayName { get; init; } = string.Empty;
            internal string MacAddress { get; init; } = string.Empty;
            internal string Connection { get; init; } = string.Empty;
            internal int PreferredSlot { get; init; }
            internal OutContType OutputType { get; init; }
            internal DS4Device Device { get; set; }
            internal bool IsManagedReacquire { get; set; }
            internal bool ReacquiredProfileReady { get; set; }
            internal bool ReacquiredPostProfileSetup { get; set; }
            internal bool AllowExternalContainmentSuspension { get; init; }
            internal bool ExternalContainmentSuspended { get; set; }
        }

        private sealed class LiveControllerExposureOperations :
            IControllerExposureTransitionOperations
        {
            private readonly ControlService service;
            private readonly ControllerExposureRuntimeSession session;

            internal LiveControllerExposureOperations(ControlService service,
                ControllerExposureRuntimeSession session)
            {
                this.service = service;
                this.session = session;
            }

            public ControllerExposureOperationResult NeutralizeSyntheticOutputs() =>
                service.NeutralizeControllerExposureOutputs(session);

            public ControllerExposureOperationResult QuiescePhysicalInput() =>
                service.QuiesceControllerExposurePhysicalInput(session);

            public ControllerExposureOperationResult RetireVirtualOutputs() =>
                service.RetireControllerExposureVirtualOutputs(session);

            public ControllerExposureOperationResult ReleasePhysicalHandle() =>
                service.ReleaseControllerExposurePhysicalHandle(session);

            public ControllerExposureOperationResult ReleasePhysicalContainment() =>
                service.ReleaseControllerExposureContainment(session);

            public ControllerExposureOperationResult AcquirePhysicalContainment() =>
                service.AcquireControllerExposureContainment(session);

            public ControllerExposureOperationResult AcquirePhysicalHandle() =>
                service.AcquireControllerExposurePhysicalHandle(session);

            public ControllerExposureOperationResult ResumePhysicalInput() =>
                service.ResumeControllerExposurePhysicalInput(session);

            public ControllerExposureOperationResult CreateVirtualOutputs() =>
                service.CreateControllerExposureVirtualOutputs(session);
        }

        private sealed class LiveControllerExposureRecoveryOperations :
            IControllerExposureRecoveryOperations
        {
            private readonly ControlService service;
            private readonly ControllerExposureRuntimeSession session;

            internal LiveControllerExposureRecoveryOperations(
                ControlService service,
                ControllerExposureRuntimeSession session)
            {
                this.service = service;
                this.session = session;
            }

            public ControllerExposureOperationResult StopAndReset() =>
                service.StopCore(showlog: true, immediateUnplug: true)
                    ? ControllerExposureOperationResult.Success()
                    : ControllerExposureOperationResult.Failure(
                        "The controller service did not stop.");

            public ControllerExposureOperationResult StartManagedService()
            {
                string failure = string.Empty;
                try
                {
                    if (!service.StartCore(showlog: true) ||
                        !service.running)
                    {
                        failure = "The controller service did not restart.";
                    }
                    else
                    {
                        ControllerExposureOperationResult verification =
                            service.VerifyRecoveredManagedVirtual(session);
                        if (!verification.Succeeded)
                        {
                            failure = verification.Error;
                        }
                    }
                }
                catch (Exception ex)
                {
                    failure = $"The controller service restart failed: " +
                        $"{ex.GetType().Name}: {ex.Message}";
                }

                if (string.IsNullOrWhiteSpace(failure))
                {
                    return ControllerExposureOperationResult.Success();
                }

                try
                {
                    service.StopCore(showlog: true, immediateUnplug: true);
                    failure += " Controller handling was left stopped safely.";
                }
                catch (Exception ex)
                {
                    failure += " Cleanup after the failed restart also " +
                        $"failed: {ex.GetType().Name}: {ex.Message}";
                }

                return ControllerExposureOperationResult.Failure(failure);
            }
        }

        private readonly object controllerExposureSessionLock = new object();
        private readonly Dictionary<string, ControllerExposureRuntimeSession>
            controllerExposureSessions = new Dictionary<string,
                ControllerExposureRuntimeSession>(
                    StringComparer.OrdinalIgnoreCase);
        private readonly SemaphoreSlim controllerExposureTransitionGate =
            new SemaphoreSlim(1, 1);
        private static readonly TimeSpan ControllerExposureInputStopTimeout =
            TimeSpan.FromSeconds(5);

        internal event EventHandler ControllerExposureSessionsChanged;

        internal IReadOnlyList<ControllerExposureSessionInfo>
            GetControllerExposureSessions()
        {
            lock (controllerExposureSessionLock)
            {
                return controllerExposureSessions.Values
                    .OrderBy(session => session.PreferredSlot)
                    .Select(CreateControllerExposureSessionInfo)
                    .ToArray();
            }
        }

        internal async Task<ControllerExposureTransitionResult>
            SetControllerExposureModeAsync(int controllerIndex,
                ControllerExposureMode requestedMode,
                bool allowExternalContainmentSuspension = false)
        {
            await controllerExposureTransitionGate.WaitAsync()
                .ConfigureAwait(false);
            try
            {
                return await Task.Run(() =>
                {
                    lock (serviceLifecycleLock)
                    {
                        return SetControllerExposureModeCore(controllerIndex,
                            requestedMode,
                            allowExternalContainmentSuspension);
                    }
                }).ConfigureAwait(false);
            }
            finally
            {
                controllerExposureTransitionGate.Release();
            }
        }

        internal async Task<ControllerExposureTransitionResult>
            SetControllerExposureModeAsync(string instanceId,
                ControllerExposureMode requestedMode)
        {
            await controllerExposureTransitionGate.WaitAsync()
                .ConfigureAwait(false);
            try
            {
                return await Task.Run(() =>
                {
                    lock (serviceLifecycleLock)
                    {
                        return SetControllerExposureModeCore(instanceId,
                            requestedMode);
                    }
                }).ConfigureAwait(false);
            }
            finally
            {
                controllerExposureTransitionGate.Release();
            }
        }

        internal async Task<ControllerExposureRecoveryResult>
            RecoverControllerExposureAsync(string instanceId)
        {
            if (!await controllerExposureTransitionGate.WaitAsync(0)
                    .ConfigureAwait(false))
            {
                return new ControllerExposureRecoveryResult(false,
                    "Another controller exposure transition is still in progress.");
            }

            try
            {
                return await Task.Run(() =>
                {
                    lock (serviceLifecycleLock)
                    {
                        return RecoverControllerExposureCore(instanceId);
                    }
                }).ConfigureAwait(false);
            }
            finally
            {
                controllerExposureTransitionGate.Release();
            }
        }

        private ControllerExposureRecoveryResult
            RecoverControllerExposureCore(string instanceId)
        {
            ControllerExposureRuntimeSession session;
            lock (controllerExposureSessionLock)
            {
                controllerExposureSessions.TryGetValue(instanceId ??
                    string.Empty, out session);
            }

            if (session == null || session.PreferredSlot < 0 ||
                session.PreferredSlot >= controllerExposureTransitions.Length)
            {
                return new ControllerExposureRecoveryResult(false,
                    "The controller recovery session is no longer available.");
            }

            ControllerExposureStatus status =
                controllerExposureTransitions[session.PreferredSlot].Status;
            StartupDiag($"Controller exposure recovery requested " +
                $"instance={session.InstanceId} slot={session.PreferredSlot} " +
                $"stage={status.Stage}");
            ControllerExposureRecoveryResult result =
                ControllerExposureRecoveryWorkflow.Recover(
                    status.NeedsRecovery,
                    new LiveControllerExposureRecoveryOperations(this,
                        session));
            LogDebug(result.Succeeded
                ? $"Recovered {session.DisplayName} to Managed / Virtual."
                : $"Could not recover {session.DisplayName}: {result.Detail}",
                !result.Succeeded);
            return result;
        }

        private ControllerExposureOperationResult
            VerifyRecoveredManagedVirtual(
                ControllerExposureRuntimeSession session)
        {
            int recoveredIndex = -1;
            for (int index = 0; index < DS4Controllers.Length; index++)
            {
                DS4Device candidate = DS4Controllers[index];
                string candidateId = Global.GetInstanceIdFromDevicePath(
                    candidate?.HidDevice?.DevicePath ?? string.Empty);
                if (string.Equals(candidateId, session.InstanceId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    recoveredIndex = index;
                    break;
                }
            }

            bool controllerPresent = recoveredIndex >= 0;
            ViiperOutDevice output = controllerPresent
                ? outputDevices[recoveredIndex] as ViiperOutDevice : null;
            OutContType actualOutputType = controllerPresent
                ? Global.activeOutDevType[recoveredIndex]
                : OutContType.None;
            return ControllerExposureRecoveryPostcondition.Evaluate(
                controllerPresent, output?.IsRuntimeConnected == true,
                actualOutputType, session.OutputType);
        }

        private ControllerExposureTransitionResult
            SetControllerExposureModeCore(int controllerIndex,
                ControllerExposureMode requestedMode,
                bool allowExternalContainmentSuspension)
        {
            if (requestedMode != ControllerExposureMode.NativePhysical)
            {
                return ExposureRequestFailure(controllerIndex,
                    "Use the native-session action to return this controller to Managed / Virtual.");
            }

            if (!running)
            {
                return ExposureRequestFailure(controllerIndex,
                    "Start DS4Windows before changing controller exposure.");
            }

            if (!Global.hidHideInstalled)
            {
                return ExposureRequestFailure(controllerIndex,
                    "Native Physical requires HidHide so Managed / Virtual can roll back safely if the transition fails.");
            }

            if (!CanManageControllerExposureContainment())
            {
                return ExposureRequestFailure(controllerIndex,
                    "HidHide configuration access is unavailable. Relaunch this disposable build as administrator so containment can be verified.");
            }

            if (controllerIndex < 0 ||
                controllerIndex >= CURRENT_DS4_CONTROLLER_LIMIT)
            {
                return ExposureRequestFailure(controllerIndex,
                    "The selected controller slot is invalid.");
            }

            DS4Device device = DS4Controllers[controllerIndex];
            if (device == null || device.IsRemoving || !device.PrimaryDevice)
            {
                return ExposureRequestFailure(controllerIndex,
                    "The selected primary controller is no longer available.");
            }

            if (Global.getDInputOnly(controllerIndex) ||
                outputDevices[controllerIndex] is not ViiperOutDevice viiper ||
                !viiper.IsRuntimeConnected)
            {
                return ExposureRequestFailure(controllerIndex,
                    "Native Physical requires an active Managed / Virtual controller to retire safely.");
            }

            string devicePath = device.HidDevice?.DevicePath ?? string.Empty;
            string instanceId = Global.GetInstanceIdFromDevicePath(devicePath);
            if (string.IsNullOrWhiteSpace(instanceId))
            {
                return ExposureRequestFailure(controllerIndex,
                    "Windows did not provide a stable identity for the physical controller.");
            }

            lock (controllerExposureSessionLock)
            {
                if (controllerExposureSessions.ContainsKey(instanceId))
                {
                    return ExposureRequestFailure(controllerIndex,
                        "An exposure transition is already active for this controller.");
                }
            }

            ControllerExposureRuntimeSession session = new()
            {
                InstanceId = instanceId,
                DevicePath = devicePath,
                DisplayName = device.DisplayName,
                MacAddress = device.MacAddress,
                Connection = device.ConnectionType switch
                {
                    ConnectionType.BT => "Bluetooth",
                    ConnectionType.USB => "USB",
                    ConnectionType.SONYWA => "Sony wireless adapter",
                    _ => device.ConnectionType.ToString(),
                },
                PreferredSlot = controllerIndex,
                OutputType = Global.OutContType[controllerIndex].Normalize(),
                Device = device,
                ReacquiredProfileReady = true,
                ReacquiredPostProfileSetup = true,
                AllowExternalContainmentSuspension =
                    allowExternalContainmentSuspension,
            };

            lock (controllerExposureSessionLock)
            {
                controllerExposureSessions.Add(instanceId, session);
            }

            ControllerExposureTransitionCoordinator coordinator =
                controllerExposureTransitions[controllerIndex];
            ControllerExposureTransitionResult result = coordinator.TransitionTo(
                requestedMode,
                new LiveControllerExposureOperations(this, session));

            if (!result.Succeeded &&
                result.Status.Mode == ControllerExposureMode.ManagedVirtual &&
                !result.Status.NeedsRecovery)
            {
                RemoveControllerExposureSession(session);
            }
            else
            {
                NotifyControllerExposureSessionsChanged();
            }

            LogControllerExposureResult(session, result);
            return result;
        }

        private ControllerExposureTransitionResult
            SetControllerExposureModeCore(string instanceId,
                ControllerExposureMode requestedMode)
        {
            if (requestedMode != ControllerExposureMode.ManagedVirtual)
            {
                return ExposureRequestFailure(-1,
                    "The native-session action can only return to Managed / Virtual.");
            }

            ControllerExposureRuntimeSession session;
            lock (controllerExposureSessionLock)
            {
                controllerExposureSessions.TryGetValue(instanceId ??
                    string.Empty, out session);
            }

            if (session == null)
            {
                return ExposureRequestFailure(-1,
                    "The native controller session is no longer available.");
            }

            if (!running)
            {
                return ExposureRequestFailure(session.PreferredSlot,
                    "Start DS4Windows before returning this controller to Managed / Virtual.");
            }

            session.IsManagedReacquire = true;
            session.ReacquiredProfileReady = false;
            session.ReacquiredPostProfileSetup = false;
            ControllerExposureTransitionCoordinator coordinator =
                controllerExposureTransitions[session.PreferredSlot];
            ControllerExposureTransitionResult result = coordinator.TransitionTo(
                requestedMode,
                new LiveControllerExposureOperations(this, session));

            if (result.Succeeded)
            {
                RemoveControllerExposureSession(session);
            }
            else
            {
                session.IsManagedReacquire = false;
                NotifyControllerExposureSessionsChanged();
            }

            LogControllerExposureResult(session, result);
            return result;
        }

        private ControllerExposureTransitionResult ExposureRequestFailure(
            int controllerIndex, string detail)
        {
            ControllerExposureStatus status = controllerIndex >= 0 &&
                controllerIndex < controllerExposureTransitions.Length
                ? controllerExposureTransitions[controllerIndex].Status
                : new ControllerExposureStatus(
                    ControllerExposureMode.ManagedVirtual,
                    ControllerExposureStage.ManagedVirtualReady, detail);
            status = new ControllerExposureStatus(status.Mode, status.Stage,
                detail);
            return new ControllerExposureTransitionResult(false, status);
        }

        private static bool CanManageControllerExposureContainment()
        {
            try
            {
                using HidHideAPIDevice hidHideDevice =
                    new HidHideAPIDevice();
                return hidHideDevice.IsOpen();
            }
            catch
            {
                return false;
            }
        }

        private ControllerExposureSessionInfo
            CreateControllerExposureSessionInfo(
                ControllerExposureRuntimeSession session)
        {
            ControllerExposureStatus status =
                controllerExposureTransitions[session.PreferredSlot].Status;
            string title = status.NeedsRecovery
                ? "RECOVERY REQUIRED"
                : status.Mode == ControllerExposureMode.NativePhysical
                    ? "NATIVE PHYSICAL"
                    : "MANAGED / VIRTUAL";
            string detail = status.NeedsRecovery
                ? status.Detail + " Select Recover controller to safely " +
                    "restart controller handling for all connected " +
                    "controllers and restore Managed / Virtual."
                : status.Detail;
            return new ControllerExposureSessionInfo(session.InstanceId,
                session.DisplayName, session.Connection,
                session.PreferredSlot + 1, title, detail,
                status.Mode == ControllerExposureMode.NativePhysical &&
                status.IsReady, status.NeedsRecovery);
        }

        private void LogControllerExposureResult(
            ControllerExposureRuntimeSession session,
            ControllerExposureTransitionResult result)
        {
            string message = result.Succeeded
                ? $"{session.DisplayName} is now " +
                    (result.Status.Mode == ControllerExposureMode.NativePhysical
                        ? "Native Physical. Launch the game after switching."
                        : "Managed / Virtual.")
                : $"Could not change {session.DisplayName} exposure: " +
                    result.Status.Detail;
            LogDebug(message, !result.Succeeded);
        }

        private void RemoveControllerExposureSession(
            ControllerExposureRuntimeSession session)
        {
            NativePhysicalDeviceRegistry.Unregister(session.InstanceId,
                session.DevicePath);
            lock (controllerExposureSessionLock)
            {
                controllerExposureSessions.Remove(session.InstanceId);
            }
            NotifyControllerExposureSessionsChanged();
        }

        private void NotifyControllerExposureSessionsChanged() =>
            ControllerExposureSessionsChanged?.Invoke(this, EventArgs.Empty);

        private ControllerExposureOperationResult
            NeutralizeControllerExposureOutputs(
                ControllerExposureRuntimeSession session)
        {
            int index = session.PreferredSlot;
            if (Global.GetSASteeringWheelEmulationAxis(index) !=
                SASteeringWheelEmulationAxisType.None)
            {
                return ControllerExposureOperationResult.Failure(
                    "Native Physical is blocked while VJoy steering output is configured because that route cannot yet be retired atomically.");
            }

            Mapping.Commit(index);
            return ControllerExposureOperationResult.Success();
        }

        private ControllerExposureOperationResult
            QuiesceControllerExposurePhysicalInput(
                ControllerExposureRuntimeSession session)
        {
            DS4Device device = FindControllerExposureDevice(session);
            if (device == null)
            {
                return ControllerExposureOperationResult.Failure(
                    "The physical controller was lost before its input reader could be quiesced.");
            }

            return device.QuiesceForNativeExposure(
                    ControllerExposureInputStopTimeout, out string error)
                ? ControllerExposureOperationResult.Success()
                : ControllerExposureOperationResult.Failure(error);
        }

        private ControllerExposureOperationResult
            RetireControllerExposureVirtualOutputs(
                ControllerExposureRuntimeSession session)
        {
            int index = session.PreferredSlot;
            DS4Device device = session.Device;
            session.ReacquiredPostProfileSetup = false;
            OutputDevice compatibilityOutput = Volatile.Read(
                ref gameBarCompatibilityOutputDevices[index]);
            DeactivateGameBarCompatibilityOutput(index);
            if (compatibilityOutput != null &&
                outputslotMan.GetOutSlotDevice(compatibilityOutput) != null)
            {
                outputslotMan.DeferredRemoval(compatibilityOutput, -1,
                    outputDevices, true);
            }

            dualSenseAudioPassthrough.Stop(index);
            dualShock4AudioPassthrough.Stop(index);
            dualSenseMicrophonePassthrough.Stop();
            audioHapticsService.Stop(index);
            DisconnectPlayStationFeatureOutput(index);

            OutputDevice primaryOutput = outputDevices[index];
            if (primaryOutput != null)
            {
                if (device == null)
                {
                    return ControllerExposureOperationResult.Failure(
                        "The physical controller identity was lost before its virtual output could be retired.");
                }

                try
                {
                    primaryOutput.ResetState();
                }
                catch (Exception ex)
                {
                    StartupDiag($"Controller exposure neutral reset failed index={index}: {ex.Message}");
                }

                UnplugOutDev(index, device, force: true);
                if (outputslotMan.GetOutSlotDevice(primaryOutput) != null)
                {
                    outputslotMan.DeferredRemoval(primaryOutput, index,
                        outputDevices, true);
                }
            }

            bool featureOutputRetired;
            lock (playStationFeatureOutputLock)
            {
                featureOutputRetired =
                    playStationFeatureOutputDevices[index] == null;
            }

            bool retired = outputDevices[index] == null &&
                Volatile.Read(ref gameBarCompatibilityOutputDevices[index]) ==
                    null &&
                Volatile.Read(ref gameBarCompatibilityRoutingActive[index]) ==
                    0 &&
                featureOutputRetired &&
                (primaryOutput == null ||
                    outputslotMan.GetOutSlotDevice(primaryOutput) == null) &&
                (compatibilityOutput == null ||
                    outputslotMan.GetOutSlotDevice(compatibilityOutput) == null);
            return retired
                ? ControllerExposureOperationResult.Success()
                : ControllerExposureOperationResult.Failure(
                    "A game-visible virtual output remained active.");
        }

        private ControllerExposureOperationResult
            ReleaseControllerExposurePhysicalHandle(
                ControllerExposureRuntimeSession session)
        {
            NativePhysicalDeviceRegistry.Register(session.InstanceId,
                session.DevicePath);
            DS4Device device = FindControllerExposureDevice(session);
            if (device == null)
            {
                session.Device = null;
                return ControllerExposureOperationResult.Success();
            }

            StartupDiag($"Controller exposure physical release begin " +
                $"instance={session.InstanceId} mac={session.MacAddress}");
            bool inputStopped = device.ReleaseForNativeExposure(
                ControllerExposureInputStopTimeout, out string releaseError);
            StartupDiag($"Controller exposure physical release end " +
                $"instance={session.InstanceId} inputStopped={inputStopped}");
            if (!inputStopped)
            {
                return ControllerExposureOperationResult.Failure(releaseError);
            }

            session.Device = null;
            if (FindControllerExposureDevice(session) != null ||
                IsControllerInstanceOpen(session.InstanceId))
            {
                return ControllerExposureOperationResult.Failure(
                    "DS4Windows could not prove that its physical HID handle was released.");
            }

            return ControllerExposureOperationResult.Success();
        }

        private ControllerExposureOperationResult
            ReleaseControllerExposureContainment(
                ControllerExposureRuntimeSession session)
        {
            if (!Global.hidHideInstalled)
            {
                return ControllerExposureOperationResult.Failure(
                    "HidHide is unavailable, so physical exposure could not be changed safely.");
            }

            try
            {
                using HidHideAPIDevice hidHideDevice = new HidHideAPIDevice();
                if (!hidHideDevice.IsOpen())
                {
                    return ControllerExposureOperationResult.Failure(
                        "HidHide could not be opened.");
                }

                if (!MigrateHidHideSessionEntriesToPersistent(hidHideDevice,
                        out string migrationError))
                {
                    return ControllerExposureOperationResult.Failure(
                        migrationError);
                }

                bool owned;
                lock (hidHideSessionLock)
                {
                    owned = hidHidePersistentManagedInstanceIds.Contains(
                        session.InstanceId);
                }

                List<string> current = hidHideDevice.GetBlacklist()
                    .Where(item => !string.IsNullOrWhiteSpace(item)).ToList();
                if (!owned)
                {
                    if (!HidHideOwnershipPolicy.Contains(current,
                            session.InstanceId))
                    {
                        return ControllerExposureOperationResult.Success();
                    }

                    return TrySuspendExternalContainment(hidHideDevice,
                            session, out string suspensionError)
                        ? ControllerExposureOperationResult.Success()
                        : ControllerExposureOperationResult.Failure(
                            suspensionError);
                }

                HidHideBlacklistMutationResult mutation =
                    HidHideBlacklistMutationGateway.Mutate(hidHideDevice,
                        blacklist => HidHideBlacklistMutationGateway.RemoveExact(
                            blacklist, session.InstanceId));
                if (!mutation.Succeeded)
                {
                    return ControllerExposureOperationResult.Failure(
                        mutation.Error);
                }

                HidHideOwnershipJournal journal =
                    GetHidHideOwnershipJournal();
                if (!journal.CompleteTransientRun(
                        new[] { session.InstanceId },
                        activeStateRestored: false))
                {
                    HidHideBlacklistMutationResult rollback =
                        HidHideBlacklistMutationGateway.Mutate(hidHideDevice,
                            blacklist =>
                                HidHideBlacklistMutationGateway.AddExact(
                                    blacklist, session.InstanceId));
                    return ControllerExposureOperationResult.Failure(
                        rollback.Succeeded
                            ? "The HidHide ownership record could not be updated; physical exposure was rolled back."
                            : "HidHide ownership recovery is required after its record and device state diverged.");
                }

                lock (hidHideSessionLock)
                {
                    hidHidePersistentManagedInstanceIds.Remove(
                        session.InstanceId);
                    if (!journal.IsTransientRunInProgress)
                    {
                        hidHideTransientRunStarted = false;
                        hidHideActiveStateBeforeManagedSession = null;
                        hidHideBaselineBlacklist = null;
                    }
                }
                UpdateHidHideAttributes();
                return ControllerExposureOperationResult.Success();
            }
            catch (Exception ex)
            {
                return ControllerExposureOperationResult.Failure(
                    $"HidHide release failed: {ex.Message}");
            }
        }

        private bool MigrateHidHideSessionEntriesToPersistent(
            HidHideAPIDevice hidHideDevice, out string error)
        {
            List<string> sessionIds;
            List<string> persistentIds;
            lock (hidHideSessionLock)
            {
                sessionIds = hidHideSessionManagedInstanceIds.ToList();
                persistentIds = hidHidePersistentManagedInstanceIds.ToList();
            }

            if (sessionIds.Count == 0)
            {
                error = string.Empty;
                return true;
            }

            HidHideOwnershipJournal journal = GetHidHideOwnershipJournal();
            List<string> recorded = new List<string>();
            HidHideSessionMigrationPlan plan = default;
            HidHideBlacklistMutationResult mutation =
                HidHideBlacklistMutationGateway.Mutate(hidHideDevice,
                    current =>
                    {
                        plan = HidHideSessionMigrationPolicy.Create(current,
                            sessionIds, persistentIds);
                        if (!plan.CanMigrate)
                        {
                            throw new InvalidOperationException(plan.Error);
                        }
                        return current.Concat(plan.EntriesToAdd).ToList();
                    },
                    () =>
                    {
                        foreach (string instanceId in plan.EntriesToAdd)
                        {
                            if (!journal.RecordPersistentBlacklistEntry(
                                    instanceId))
                            {
                                journal.CompleteTransientRun(recorded,
                                    activeStateRestored: false);
                                return false;
                            }
                            recorded.Add(instanceId);
                        }
                        return true;
                    });
            if (!mutation.Succeeded)
            {
                bool rolledBack = recorded.Count == 0 ||
                    HidHideBlacklistMutationGateway.Mutate(hidHideDevice,
                        current => HidHideBlacklistMutationGateway.RemoveExact(
                            current, recorded)).Succeeded;
                if (rolledBack)
                {
                    journal.CompleteTransientRun(recorded,
                        activeStateRestored: false);
                }
                error = rolledBack
                    ? $"HidHide session containment could not be migrated safely: {mutation.Error}"
                    : "HidHide recovery is required after session migration failed.";
                return false;
            }

            if (!hidHideDevice.ClearSessionBlacklist())
            {
                bool rolledBack = recorded.Count == 0 ||
                    HidHideBlacklistMutationGateway.Mutate(hidHideDevice,
                        current => HidHideBlacklistMutationGateway.RemoveExact(
                            current, recorded)).Succeeded;
                if (rolledBack)
                {
                    journal.CompleteTransientRun(recorded,
                        activeStateRestored: false);
                }
                error = rolledBack
                    ? "HidHide session containment could not be released and was preserved."
                    : "HidHide recovery is required after session migration rollback failed.";
                return false;
            }

            lock (hidHideSessionLock)
            {
                hidHideSessionManagedInstanceIds.ExceptWith(sessionIds);
                hidHidePersistentManagedInstanceIds.UnionWith(sessionIds);
            }
            error = string.Empty;
            return true;
        }

        private ControllerExposureOperationResult
            AcquireControllerExposureContainment(
                ControllerExposureRuntimeSession session)
        {
            if (!Global.hidHideInstalled)
            {
                return ControllerExposureOperationResult.Failure(
                    "Managed / Virtual requires HidHide containment.");
            }

            if (session.ExternalContainmentSuspended)
            {
                try
                {
                    using HidHideAPIDevice hidHideDevice =
                        new HidHideAPIDevice();
                    if (!hidHideDevice.IsOpen())
                    {
                        return ControllerExposureOperationResult.Failure(
                            "HidHide could not be opened while restoring the external controller rule.");
                    }

                    HidHideOwnershipJournal journal =
                        GetHidHideOwnershipJournal();
                    if (!TryRestoreExternalContainmentSuspension(journal,
                            hidHideDevice, session.InstanceId,
                            out string restoreError))
                    {
                        return ControllerExposureOperationResult.Failure(
                            restoreError);
                    }
                    session.ExternalContainmentSuspended = false;
                }
                catch (Exception ex)
                {
                    return ControllerExposureOperationResult.Failure(
                        $"External HidHide containment restore failed: {ex.Message}");
                }
            }

            return EnsureHidHideForInstance(session.InstanceId,
                    session.DisplayName, preferPersistent: true)
                ? ControllerExposureOperationResult.Success()
                : ControllerExposureOperationResult.Failure(
                    "DS4Windows could not acquire HidHide containment for the physical controller.");
        }

        private ControllerExposureOperationResult
            AcquireControllerExposurePhysicalHandle(
                ControllerExposureRuntimeSession session)
        {
            DS4Device existing = FindControllerExposureDevice(session);
            if (existing != null)
            {
                session.Device = existing;
                return ControllerExposureOperationResult.Success();
            }

            if (DS4Controllers[session.PreferredSlot] != null)
            {
                return ControllerExposureOperationResult.Failure(
                    $"Controller slot {session.PreferredSlot + 1} is occupied; the original slot was preserved.");
            }

            NativePhysicalDeviceRegistry.Unregister(session.InstanceId,
                session.DevicePath);
            session.IsManagedReacquire = true;
            session.ReacquiredProfileReady = false;
            session.ReacquiredPostProfileSetup = false;
            HotPlug();
            DS4Device device = FindControllerExposureDevice(session);
            if (device == null)
            {
                NativePhysicalDeviceRegistry.Register(session.InstanceId,
                    session.DevicePath);
                return ControllerExposureOperationResult.Failure(
                    "The physical controller could not be reopened in its original slot.");
            }

            session.Device = device;
            return ControllerExposureOperationResult.Success();
        }

        private ControllerExposureOperationResult
            ResumeControllerExposurePhysicalInput(
                ControllerExposureRuntimeSession session)
        {
            DS4Device device = session.Device ??
                FindControllerExposureDevice(session);
            if (device == null)
            {
                return ControllerExposureOperationResult.Failure(
                    "The physical controller was unavailable while restoring its input reader.");
            }

            return device.ResumeAfterNativeExposureQuiesce()
                ? ControllerExposureOperationResult.Success()
                : ControllerExposureOperationResult.Failure(
                    "The physical input reader could not be resumed during rollback.");
        }

        private ControllerExposureOperationResult
            CreateControllerExposureVirtualOutputs(
                ControllerExposureRuntimeSession session)
        {
            int index = session.PreferredSlot;
            DS4Device device = session.Device ??
                FindControllerExposureDevice(session);
            if (device == null || !session.ReacquiredProfileReady)
            {
                return ControllerExposureOperationResult.Failure(
                    "The controller profile was not ready after reacquiring the physical controller.");
            }

            if (Global.getDInputOnly(index))
            {
                return ControllerExposureOperationResult.Failure(
                    "The active profile no longer requests a virtual controller.");
            }

            PluginOutDev(index, device, session.OutputType);
            ViiperOutDevice output = outputDevices[index] as ViiperOutDevice;
            if (output?.IsRuntimeConnected != true ||
                Global.activeOutDevType[index].Normalize() !=
                    session.OutputType)
            {
                return ControllerExposureOperationResult.Failure(
                    "The requested VIIPER virtual controller did not become ready.");
            }

            if (!session.ReacquiredPostProfileSetup)
            {
                if (device.PrimaryDevice && device.OutputMapGyro)
                {
                    TouchPadOn(index, device);
                }
                CheckProfileOptions(index, device);
                SetupInitialHookEvents(index, device);
                session.ReacquiredPostProfileSetup = true;
            }

            return ControllerExposureOperationResult.Success();
        }

        private DS4Device FindControllerExposureDevice(
            ControllerExposureRuntimeSession session)
        {
            DS4Device candidate = DS4Controllers[session.PreferredSlot];
            if (candidate == null)
            {
                return null;
            }

            string candidateId = Global.GetInstanceIdFromDevicePath(
                candidate.HidDevice?.DevicePath ?? string.Empty);
            return string.Equals(candidateId, session.InstanceId,
                StringComparison.OrdinalIgnoreCase) ? candidate : null;
        }

        private static bool IsControllerInstanceOpen(string instanceId) =>
            DS4Devices.getDS4Controllers().Any(device => string.Equals(
                Global.GetInstanceIdFromDevicePath(
                    device.HidDevice?.DevicePath ?? string.Empty),
                instanceId, StringComparison.OrdinalIgnoreCase));

        private int GetControllerExposurePreferredSlot(DS4Device device)
        {
            string instanceId = Global.GetInstanceIdFromDevicePath(
                device?.HidDevice?.DevicePath ?? string.Empty);
            lock (controllerExposureSessionLock)
            {
                return controllerExposureSessions.TryGetValue(instanceId,
                        out ControllerExposureRuntimeSession session) &&
                    session.IsManagedReacquire
                    ? session.PreferredSlot : -1;
            }
        }

        private bool IsControllerExposureSlotReserved(int slot,
            DS4Device candidate)
        {
            string candidateId = Global.GetInstanceIdFromDevicePath(
                candidate?.HidDevice?.DevicePath ?? string.Empty);
            lock (controllerExposureSessionLock)
            {
                ControllerExposureRuntimeSession reservation =
                    controllerExposureSessions.Values.FirstOrDefault(
                        session => session.PreferredSlot == slot);
                return reservation != null &&
                    (!reservation.IsManagedReacquire ||
                     !string.Equals(reservation.InstanceId, candidateId,
                         StringComparison.OrdinalIgnoreCase));
            }
        }

        private bool ShouldDeferControllerExposureProfileSetup(int index,
            DS4Device device)
        {
            string instanceId = Global.GetInstanceIdFromDevicePath(
                device?.HidDevice?.DevicePath ?? string.Empty);
            lock (controllerExposureSessionLock)
            {
                return controllerExposureSessions.TryGetValue(instanceId,
                        out ControllerExposureRuntimeSession session) &&
                    session.IsManagedReacquire &&
                    session.PreferredSlot == index;
            }
        }

        private void RecordControllerExposureProfileReady(int index,
            DS4Device device, bool ready)
        {
            string instanceId = Global.GetInstanceIdFromDevicePath(
                device?.HidDevice?.DevicePath ?? string.Empty);
            lock (controllerExposureSessionLock)
            {
                if (controllerExposureSessions.TryGetValue(instanceId,
                        out ControllerExposureRuntimeSession session) &&
                    session.PreferredSlot == index)
                {
                    session.ReacquiredProfileReady = ready;
                    session.Device = device;
                }
            }
        }

        private void ResetControllerExposureSessionsForServiceStop()
        {
            NativePhysicalDeviceRegistry.Clear();
            lock (controllerExposureSessionLock)
            {
                controllerExposureSessions.Clear();
            }
            foreach (ControllerExposureTransitionCoordinator coordinator in
                controllerExposureTransitions)
            {
                coordinator.ResetToManagedVirtual();
            }
            NotifyControllerExposureSessionsChanged();
        }
    }
}
