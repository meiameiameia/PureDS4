using System;

namespace DS4Windows
{
    public enum ControllerRuntimeLaneState : byte
    {
        NotRequired,
        Starting,
        Ready,
        Unavailable,
    }

    public enum VirtualOutputBlockReason : byte
    {
        None,
        PhysicalContainmentUnavailable,
        NoAvailableOutputSlot,
        OutputBindingFailed,
    }

    public enum ControllerStartupStage : byte
    {
        Disconnected,
        Connecting,
        Connected,
        CreatingVirtualController,
        ArmingAdvancedHaptics,
        StartingSpeaker,
        StartingMicrophone,
        Ready,
        Attention,
    }

    public readonly struct ControllerRuntimeSignals
    {
        public ControllerRuntimeSignals(bool physicalPresent,
            bool physicalSynced, bool physicalAlive, bool virtualRequired,
            bool virtualConnected, bool virtualTypeMatches,
            ControllerRuntimeLaneState advancedHaptics,
            ControllerRuntimeLaneState speaker,
            ControllerRuntimeLaneState microphone,
            string virtualControllerName,
            ControllerExposureMode exposureMode =
                ControllerExposureMode.ManagedVirtual,
            ControllerExposureStage exposureStage =
                ControllerExposureStage.ManagedVirtualReady,
            VirtualOutputBlockReason virtualOutputBlockReason =
                VirtualOutputBlockReason.None,
            string activeVirtualControllerName = null)
        {
            PhysicalPresent = physicalPresent;
            PhysicalSynced = physicalSynced;
            PhysicalAlive = physicalAlive;
            VirtualRequired = virtualRequired;
            VirtualConnected = virtualConnected;
            VirtualTypeMatches = virtualTypeMatches;
            AdvancedHaptics = advancedHaptics;
            Speaker = speaker;
            Microphone = microphone;
            VirtualControllerName = virtualControllerName ?? "virtual controller";
            ExposureMode = exposureMode;
            ExposureStage = exposureStage;
            VirtualOutputBlockReason = virtualOutputBlockReason;
            ActiveVirtualControllerName = activeVirtualControllerName ??
                string.Empty;
        }

        public bool PhysicalPresent { get; }
        public bool PhysicalSynced { get; }
        public bool PhysicalAlive { get; }
        public bool VirtualRequired { get; }
        public bool VirtualConnected { get; }
        public bool VirtualTypeMatches { get; }
        public ControllerRuntimeLaneState AdvancedHaptics { get; }
        public ControllerRuntimeLaneState Speaker { get; }
        public ControllerRuntimeLaneState Microphone { get; }
        public string VirtualControllerName { get; }
        public ControllerExposureMode ExposureMode { get; }
        public ControllerExposureStage ExposureStage { get; }
        public VirtualOutputBlockReason VirtualOutputBlockReason { get; }
        public string ActiveVirtualControllerName { get; }
    }

    public readonly struct ControllerStartupStatus : IEquatable<ControllerStartupStatus>
    {
        public ControllerStartupStatus(ControllerStartupStage stage,
            string title, string detail)
        {
            Stage = stage;
            Title = title ?? string.Empty;
            Detail = detail ?? string.Empty;
        }

        public ControllerStartupStage Stage { get; }
        public string Title { get; }
        public string Detail { get; }
        public bool IsReady => Stage == ControllerStartupStage.Ready;
        public bool NeedsAttention => Stage == ControllerStartupStage.Attention;

        public bool Equals(ControllerStartupStatus other) =>
            Stage == other.Stage && Title == other.Title && Detail == other.Detail;

        public override bool Equals(object obj) =>
            obj is ControllerStartupStatus other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Stage, Title, Detail);

        public static bool operator ==(ControllerStartupStatus left,
            ControllerStartupStatus right) => left.Equals(right);

        public static bool operator !=(ControllerStartupStatus left,
            ControllerStartupStatus right) => !left.Equals(right);
    }

    public static class ControllerRuntimeStatusPolicy
    {
        public static ControllerStartupStatus Evaluate(
            ControllerRuntimeSignals signals)
        {
            if (!signals.PhysicalPresent)
            {
                return new ControllerStartupStatus(
                    ControllerStartupStage.Disconnected, "Disconnected",
                    "No physical controller is assigned to this slot.");
            }

            if (!signals.PhysicalSynced || !signals.PhysicalAlive)
            {
                return new ControllerStartupStatus(
                    ControllerStartupStage.Connecting, "Connecting",
                    "Waiting for a recent input report from the physical controller.");
            }

            if (signals.VirtualRequired && !signals.VirtualConnected &&
                signals.VirtualOutputBlockReason !=
                    VirtualOutputBlockReason.None)
            {
                return new ControllerStartupStatus(
                    ControllerStartupStage.Attention,
                    "Virtual output unavailable",
                    DescribeVirtualOutputBlock(
                        signals.VirtualOutputBlockReason));
            }

            if (signals.VirtualRequired && !signals.VirtualConnected)
            {
                return new ControllerStartupStatus(
                    ControllerStartupStage.CreatingVirtualController,
                    "Connected",
                    $"Creating the virtual {signals.VirtualControllerName} pad.");
            }

            if (signals.VirtualRequired && !signals.VirtualTypeMatches)
            {
                return new ControllerStartupStatus(
                    ControllerStartupStage.CreatingVirtualController,
                    "Connected",
                    $"Switching to the virtual {signals.VirtualControllerName} pad.");
            }

            ControllerStartupStatus laneStatus = EvaluateLane(
                signals.AdvancedHaptics,
                ControllerStartupStage.ArmingAdvancedHaptics,
                "Arming haptics", "advanced haptics lane");
            if (laneStatus.Stage != ControllerStartupStage.Ready)
            {
                return laneStatus;
            }

            laneStatus = EvaluateLane(signals.Speaker,
                ControllerStartupStage.StartingSpeaker,
                "Starting speaker", "controller speaker and headset audio");
            if (laneStatus.Stage != ControllerStartupStage.Ready)
            {
                return laneStatus;
            }

            laneStatus = EvaluateLane(signals.Microphone,
                ControllerStartupStage.StartingMicrophone,
                "Starting microphone", "controller microphone");
            if (laneStatus.Stage != ControllerStartupStage.Ready)
            {
                return laneStatus;
            }

            string detail = signals.VirtualRequired
                ? "Physical input was detected and the virtual output backend is connected. Game response is not verified."
                : "Physical input was detected. Game response is not verified.";
            return new ControllerStartupStatus(ControllerStartupStage.Ready,
                "Ready", detail);
        }

        private static ControllerStartupStatus EvaluateLane(
            ControllerRuntimeLaneState state, ControllerStartupStage stage,
            string startingTitle, string laneName)
        {
            return state switch
            {
                ControllerRuntimeLaneState.Starting =>
                    new ControllerStartupStatus(stage, startingTitle,
                        $"Waiting for the {laneName} to become stable."),
                ControllerRuntimeLaneState.Unavailable =>
                    new ControllerStartupStatus(ControllerStartupStage.Attention,
                        "Needs attention",
                        $"The enabled {laneName} could not be armed."),
                _ => new ControllerStartupStatus(
                    ControllerStartupStage.Ready, "Ready", string.Empty),
            };
        }

        private static string DescribeVirtualOutputBlock(
            VirtualOutputBlockReason reason)
        {
            return reason switch
            {
                VirtualOutputBlockReason.PhysicalContainmentUnavailable =>
                    "Virtual output was not created because physical-controller protection is unavailable.",
                VirtualOutputBlockReason.NoAvailableOutputSlot =>
                    "Virtual output was not created because no output slot is available.",
                VirtualOutputBlockReason.OutputBindingFailed =>
                    "Virtual output was not created because the output could not be bound to this controller.",
                _ => "Virtual output was not created.",
            };
        }
    }
}
