using SharpOSC;
using System;
using System.Globalization;
using System.Threading;

namespace DS4Windows
{
    public enum OscInputKind
    {
        BatteryRequest,
        Press,
        Stick,
        Trigger,
    }

    public readonly struct OscInputCommand
    {
        public int ControllerIndex { get; }
        public OscInputKind Kind { get; }
        public string Control { get; }
        public byte Value { get; }
        public byte SecondValue { get; }

        internal OscInputCommand(int controllerIndex, OscInputKind kind,
            string control, byte value = 0, byte secondValue = 0)
        {
            ControllerIndex = controllerIndex;
            Kind = kind;
            Control = control;
            Value = value;
            SecondValue = secondValue;
        }

        public void ApplyTo(DS4State state)
        {
            if (state == null || Kind == OscInputKind.BatteryRequest)
                return;

            if (Kind == OscInputKind.Press)
            {
                bool pressed = Value == 1;
                switch (Control)
                {
                    case "cross": state.Cross = pressed; break;
                    case "square": state.Square = pressed; break;
                    case "circle": state.Circle = pressed; break;
                    case "triangle": state.Triangle = pressed; break;
                    case "r1": state.R1 = pressed; break;
                    case "r2": state.R2 = pressed ? byte.MaxValue : (byte)0; break;
                    case "r3": state.R3 = pressed; break;
                    case "l1": state.L1 = pressed; break;
                    case "l2": state.L2 = pressed ? byte.MaxValue : (byte)0; break;
                    case "l3": state.L3 = pressed; break;
                    case "dpadup":
                    case "dup": state.DpadUp = pressed; break;
                    case "dpaddown":
                    case "ddown": state.DpadDown = pressed; break;
                    case "dpadleft":
                    case "dleft": state.DpadLeft = pressed; break;
                    case "dpadright":
                    case "dright": state.DpadRight = pressed; break;
                    case "options": state.Options = pressed; break;
                    case "share": state.Share = pressed; break;
                }
            }
            else if (Kind == OscInputKind.Stick)
            {
                switch (Control)
                {
                    case "lx": state.LX = Value; break;
                    case "ly": state.LY = Value; break;
                    case "rx": state.RX = Value; break;
                    case "ry": state.RY = Value; break;
                    case "left": state.LX = Value; state.LY = SecondValue; break;
                    case "right": state.RX = Value; state.RY = SecondValue; break;
                }
            }
            else if (Kind == OscInputKind.Trigger)
            {
                if (Control == "l2") state.L2 = Value;
                else if (Control == "r2") state.R2 = Value;
            }
        }
    }

    public static class OscInputPacketPolicy
    {
        public static bool TryParse(OscPacket packet, bool interpretMonitoring,
            int controllerLimit, out OscInputCommand command)
        {
            command = default;
            if (packet is not OscMessage message ||
                string.IsNullOrEmpty(message.Address) ||
                message.Arguments == null || controllerLimit <= 0)
                return false;

            string[] path = message.Address.Split('/');
            if (path.Length < 4 || path[0] != string.Empty ||
                path[1] != "ds4windows")
                return false;

            bool monitoring = path[2] == "monitor";
            if (monitoring && (!interpretMonitoring || path.Length != 5))
                return false;
            if (!monitoring && path.Length != 4 && path.Length != 5)
                return false;

            string indexText = monitoring ? path[3] : path[2];
            if (!int.TryParse(indexText, NumberStyles.None,
                    CultureInfo.InvariantCulture, out int index) ||
                index < 0 || index >= controllerLimit)
                return false;

            string control = path.Length == 5 ? path[4] : string.Empty;
            string category = monitoring ? MonitoringCategory(control) : path[3];
            if (category == "battery")
            {
                if ((!monitoring && path.Length != 4) ||
                    message.Arguments.Count != 0)
                    return false;
                command = new OscInputCommand(index, OscInputKind.BatteryRequest,
                    "battery");
                return true;
            }

            if (path.Length != 5 || string.IsNullOrEmpty(control))
                return false;

            if (category == "press")
            {
                if (!IsPressControl(control) || message.Arguments.Count != 1 ||
                    !TryNumber(message.Arguments[0], out double value) ||
                    (value != 0 && value != 1))
                    return false;
                command = new OscInputCommand(index, OscInputKind.Press,
                    control, (byte)value);
                return true;
            }

            if (category == "stick")
            {
                if (IsSingleStickControl(control) &&
                    message.Arguments.Count == 1 &&
                    TryByte(message.Arguments[0], false, out byte value))
                {
                    command = new OscInputCommand(index, OscInputKind.Stick,
                        control, value);
                    return true;
                }
                if ((control == "left" || control == "right") &&
                    message.Arguments.Count == 2 &&
                    TryByte(message.Arguments[0], true, out byte x) &&
                    TryByte(message.Arguments[1], true, out byte y))
                {
                    command = new OscInputCommand(index, OscInputKind.Stick,
                        control, x, y);
                    return true;
                }
                return false;
            }

            if (category == "trigger" &&
                (control == "l2" || control == "r2") &&
                message.Arguments.Count == 1 &&
                TryByte(message.Arguments[0], false, out byte trigger))
            {
                command = new OscInputCommand(index, OscInputKind.Trigger,
                    control, trigger);
                return true;
            }
            return false;
        }

        public static bool ShouldLogMalformed(ref long nextLogTick,
            long nowTick, long intervalTicks)
        {
            if (intervalTicks <= 0)
                return false;
            long next = Interlocked.Read(ref nextLogTick);
            return nowTick >= next &&
                Interlocked.CompareExchange(ref nextLogTick,
                    nowTick + intervalTicks, next) == next;
        }

        private static string MonitoringCategory(string control)
        {
            if (control == "battery") return "battery";
            if (control == "l2" || control == "r2") return "trigger";
            if (IsSingleStickControl(control)) return "stick";
            return "press";
        }

        private static bool IsSingleStickControl(string control) =>
            control == "lx" || control == "ly" ||
            control == "rx" || control == "ry";

        private static bool IsPressControl(string control) =>
            control == "cross" || control == "square" ||
            control == "circle" || control == "triangle" ||
            control == "r1" || control == "r2" || control == "r3" ||
            control == "l1" || control == "l2" || control == "l3" ||
            control == "dpadup" || control == "dup" ||
            control == "dpaddown" || control == "ddown" ||
            control == "dpadleft" || control == "dleft" ||
            control == "dpadright" || control == "dright" ||
            control == "options" || control == "share";

        private static bool TryByte(object argument, bool normalized,
            out byte result)
        {
            result = 0;
            if (!TryNumber(argument, out double value) ||
                value < 0 || value > (normalized ? 1 : byte.MaxValue))
                return false;
            result = (byte)Math.Round(normalized ? value * byte.MaxValue : value,
                MidpointRounding.ToEven);
            return true;
        }

        private static bool TryNumber(object argument, out double value)
        {
            value = 0;
            switch (argument)
            {
                case bool boolean: value = boolean ? 1 : 0; break;
                case sbyte number: value = number; break;
                case byte number: value = number; break;
                case short number: value = number; break;
                case ushort number: value = number; break;
                case int number: value = number; break;
                case uint number: value = number; break;
                case long number: value = number; break;
                case ulong number: value = number; break;
                case float number: value = number; break;
                case double number: value = number; break;
                case decimal number: value = (double)number; break;
                case string text when double.TryParse(text, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out double parsed):
                    value = parsed;
                    break;
                default: return false;
            }
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
