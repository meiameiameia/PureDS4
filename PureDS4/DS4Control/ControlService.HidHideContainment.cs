using System;
using System.Collections.Generic;
using System.Linq;
using DS4WinWPF.DS4Control;

namespace DS4Windows
{
    public partial class ControlService
    {
        internal HidHideRecoveryPreview InspectPersistentHidHideRecovery()
        {
            lock (serviceLifecycleLock)
            {
                if (running)
                {
                    return new HidHideRecoveryPreview(null, null, null,
                        false, null,
                        "Stop controller handling before HidHide recovery.");
                }
                if (!Global.hidHideInstalled)
                {
                    return new HidHideRecoveryPreview(null, null, null,
                        false, null, "HidHide is not installed or available.");
                }

                try
                {
                    HidHideOwnershipJournal journal =
                        GetHidHideOwnershipJournal(
                            restoreExternalContainment: false);
                    using HidHideAPIDevice device = new HidHideAPIDevice();
                    return device.IsOpen()
                        ? HidHidePersistentRecovery.Inspect(device, journal,
                            device.GetActiveState)
                        : new HidHideRecoveryPreview(null, null, null,
                            false, null,
                            "The HidHide control device could not be opened.");
                }
                catch (Exception ex)
                {
                    return new HidHideRecoveryPreview(null, null, null,
                        false, null, "HidHide recovery inspection failed: " +
                            ex.Message);
                }
            }
        }

        internal string CompletePersistentHidHideRecovery(
            HidHideRecoveryPreview preview)
        {
            lock (serviceLifecycleLock)
            {
                if (running)
                {
                    return "Stop controller handling before HidHide recovery.";
                }
                if (!Global.hidHideInstalled)
                {
                    return "HidHide is not installed or available.";
                }
                lock (hidHideSessionLock)
                {
                    if (hidHideSessionManagedInstanceIds.Count > 0)
                    {
                        return "Session-only HidHide entries remain; exit " +
                            "PureDS4 before retrying recovery.";
                    }
                }

                try
                {
                    HidHideOwnershipJournal journal =
                        GetHidHideOwnershipJournal(
                            restoreExternalContainment: false);
                    using HidHideAPIDevice device = new HidHideAPIDevice();
                    if (!device.IsOpen())
                    {
                        return "The HidHide control device could not be opened.";
                    }

                    string error = HidHidePersistentRecovery.Complete(
                        device, journal, preview,
                        readActiveState: device.GetActiveState);
                    if (string.IsNullOrEmpty(error))
                    {
                        lock (hidHideSessionLock)
                        {
                            hidHidePersistentManagedInstanceIds.ExceptWith(
                                preview.Pending);
                            hidHideActiveStateBeforeManagedSession = null;
                            hidHideBaselineBlacklist = null;
                            hidHideTransientRunStarted = false;
                        }
                        StartupDiag("Verified exact HidHide recovery entries " +
                            "absent and completed their ownership record");
                    }
                    return error;
                }
                catch (Exception ex)
                {
                    return "HidHide recovery failed: " + ex.Message;
                }
            }
        }

        private bool TrySuspendExternalContainment(
            IHidHideBlacklistDevice hidHideDevice,
            ControllerExposureRuntimeSession session, out string error)
        {
            if (!session.AllowExternalContainmentSuspension)
            {
                error = "The controller is hidden by external HidHide " +
                    "configuration. Explicit session consent is required " +
                    "before that exact rule can be suspended.";
                return false;
            }

            HidHideOwnershipJournal journal = GetHidHideOwnershipJournal();
            if (!journal.IsReliable || journal.RecoveryRequired ||
                journal.ExternalContainmentSuspensions.Any(
                    record => string.Equals(record.InstanceId,
                        session.InstanceId,
                        StringComparison.OrdinalIgnoreCase)))
            {
                error = "An earlier HidHide restore obligation must be " +
                    "resolved before exposing this controller.";
                return false;
            }

            bool intentRecorded = false;
            bool activeStateObserved;
            bool inverseStateObserved;
            try
            {
                if (hidHideDevice is not HidHideAPIDevice liveDevice)
                {
                    activeStateObserved = true;
                    inverseStateObserved = false;
                }
                else
                {
                    if (!liveDevice.TryGetActiveState(
                            out activeStateObserved) ||
                        !liveDevice.TryGetWhiteListInverseState(
                            out inverseStateObserved))
                    {
                        error = "HidHide active/inverse safety state could " +
                            "not be read.";
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                error = $"HidHide safety state could not be recorded: {ex.Message}";
                return false;
            }

            HidHideBlacklistMutationResult mutation =
                HidHideBlacklistMutationGateway.Mutate(hidHideDevice,
                    current => HidHideBlacklistMutationGateway.RemoveExact(
                        current, session.InstanceId),
                    () =>
                    {
                        intentRecorded = journal.
                            RecordExternalContainmentSuspensionIntent(
                                session.InstanceId, activeStateObserved,
                                inverseStateObserved);
                        return intentRecorded;
                    });

            if (mutation.Succeeded && !mutation.Changed)
            {
                error = string.Empty;
                return true;
            }

            if (mutation.Succeeded && mutation.Changed &&
                journal.MarkExternalContainmentSuspended(session.InstanceId))
            {
                session.ExternalContainmentSuspended = true;
                error = string.Empty;
                return true;
            }

            string mutationError = mutation.Succeeded
                ? "The HidHide suspension record could not be finalized."
                : mutation.Error;
            bool restoreNeeded = intentRecorded &&
                !HidHideBlacklistMutationGateway.Contains(mutation.After,
                    session.InstanceId);
            if (!restoreNeeded && intentRecorded && !mutation.WriteAttempted)
            {
                journal.CompleteExternalContainmentSuspension(
                    session.InstanceId);
                error = mutationError;
                return false;
            }

            string restoreError = string.Empty;
            if (intentRecorded && TryRestoreExternalContainmentSuspension(
                    journal, hidHideDevice, session.InstanceId,
                    out restoreError))
            {
                error = mutationError + " External containment was restored.";
                return false;
            }

            if (intentRecorded)
            {
                journal.MarkExternalContainmentRecoveryRequired(
                    session.InstanceId);
            }
            error = mutationError + " Recovery is required before virtual " +
                "output can be used safely." +
                (string.IsNullOrWhiteSpace(restoreError)
                    ? string.Empty : $" {restoreError}");
            return false;
        }

        private bool TryRestoreExternalContainmentSuspension(
            HidHideOwnershipJournal journal,
            IHidHideBlacklistDevice hidHideDevice, string instanceId,
            out string error)
        {
            HidHideBlacklistMutationResult mutation =
                HidHideBlacklistMutationGateway.Mutate(hidHideDevice,
                    current => HidHideBlacklistMutationGateway.AddExact(
                        current, instanceId));
            if (!mutation.Succeeded ||
                !HidHideBlacklistMutationGateway.Contains(mutation.After,
                    instanceId))
            {
                journal.MarkExternalContainmentRecoveryRequired(instanceId);
                error = string.IsNullOrWhiteSpace(mutation.Error)
                    ? "The external HidHide rule was not restored."
                    : mutation.Error;
                return false;
            }

            if (!journal.CompleteExternalContainmentSuspension(instanceId))
            {
                journal.MarkExternalContainmentRecoveryRequired(instanceId);
                error = "The external HidHide rule was restored, but its " +
                    "recovery record could not be cleared.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private bool TryRestoreExternalContainmentSuspensions(
            HidHideOwnershipJournal journal, bool startupRecovery)
        {
            IReadOnlyCollection<HidHideExternalSuspensionInfo> pending =
                journal?.ExternalContainmentSuspensions ??
                Array.Empty<HidHideExternalSuspensionInfo>();
            if (pending.Count == 0)
            {
                return true;
            }
            if (!Global.hidHideInstalled)
            {
                return false;
            }

            try
            {
                using HidHideAPIDevice hidHideDevice = new HidHideAPIDevice();
                if (!hidHideDevice.IsOpen())
                {
                    return false;
                }

                bool restoredAll = true;
                foreach (HidHideExternalSuspensionInfo suspension in pending)
                {
                    if (TryRestoreExternalContainmentSuspension(journal,
                            hidHideDevice, suspension.InstanceId,
                            out string error))
                    {
                        StartupDiag($"Restored external HidHide containment " +
                            $"for {suspension.InstanceId}" +
                            (startupRecovery ? " during startup recovery" :
                                " before shutdown"));
                    }
                    else
                    {
                        restoredAll = false;
                        StartupDiag($"External HidHide containment restore " +
                            $"failed for {suspension.InstanceId}: {error}");
                    }
                }
                return restoredAll;
            }
            catch (Exception ex)
            {
                StartupDiag($"External HidHide containment recovery failed: " +
                    ex.Message);
                return false;
            }
        }
    }
}
