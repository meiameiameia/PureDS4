using DS4Windows;
using SharpOSC;

namespace DS4WindowsTests
{
    [TestClass]
    public class OscInputPacketPolicyTests
    {
        private static bool Parse(OscPacket packet, out OscInputCommand command,
            bool monitoring = false) =>
            OscInputPacketPolicy.TryParse(packet, monitoring, 8, out command);

        [TestMethod]
        public void AppliesValidButtonStickAndTriggerCommands()
        {
            DS4State state = new DS4State();

            Assert.IsTrue(Parse(new OscMessage("/ds4windows/2/press/cross", 1),
                out OscInputCommand press));
            Assert.AreEqual(2, press.ControllerIndex);
            press.ApplyTo(state);
            Assert.IsTrue(state.Cross);

            Assert.IsTrue(Parse(new OscMessage("/ds4windows/2/press/cross", 0),
                out press));
            press.ApplyTo(state);
            Assert.IsFalse(state.Cross);

            Assert.IsTrue(Parse(new OscMessage("/ds4windows/2/stick/left", 0.0f, 1.0f),
                out OscInputCommand stick));
            stick.ApplyTo(state);
            Assert.AreEqual((byte)0, state.LX);
            Assert.AreEqual((byte)255, state.LY);

            Assert.IsTrue(Parse(new OscMessage("/ds4windows/2/stick/rx", 127),
                out stick));
            stick.ApplyTo(state);
            Assert.AreEqual((byte)127, state.RX);

            Assert.IsTrue(Parse(new OscMessage("/ds4windows/2/trigger/r2", 201),
                out OscInputCommand trigger));
            trigger.ApplyTo(state);
            Assert.AreEqual((byte)201, state.R2);
        }

        [TestMethod]
        public void MonitoringAndBatteryCommandsKeepTheirOriginalMeaning()
        {
            Assert.IsTrue(Parse(new OscMessage("/ds4windows/monitor/7/cross", 1),
                out OscInputCommand monitoredPress, monitoring: true));
            Assert.AreEqual(7, monitoredPress.ControllerIndex);
            Assert.AreEqual(OscInputKind.Press, monitoredPress.Kind);

            Assert.IsTrue(Parse(new OscMessage("/ds4windows/monitor/0/l2", 52),
                out OscInputCommand monitoredTrigger, monitoring: true));
            Assert.AreEqual(OscInputKind.Trigger, monitoredTrigger.Kind);

            Assert.IsTrue(Parse(new OscMessage("/ds4windows/0/battery"),
                out OscInputCommand battery));
            Assert.AreEqual(OscInputKind.BatteryRequest, battery.Kind);

            Assert.IsFalse(Parse(new OscMessage("/ds4windows/monitor/0/cross", 1),
                out _, monitoring: false));
        }

        [TestMethod]
        public void RejectsWrongPacketAddressAndControllerIndex()
        {
            OscPacket[] invalid =
            {
                null,
                new OscBundle(0, new[] { new OscMessage("/ds4windows/0/press/cross", 1) }),
                new OscMessage(""),
                new OscMessage("/"),
                new OscMessage("/ds4windows"),
                new OscMessage("/ds4windows/0"),
                new OscMessage("/ds4windows/0/press"),
                new OscMessage("/ds4windows/0/press/cross/extra", 1),
                new OscMessage("/foreign/0/press/cross", 1),
                new OscMessage("/ds4windows/-1/press/cross", 1),
                new OscMessage("/ds4windows/8/press/cross", 1),
                new OscMessage("/ds4windows/2147483648/press/cross", 1),
                new OscMessage("/ds4windows/0/press/unknown", 1),
                new OscMessage("/ds4windows/0/battery/extra"),
            };
            foreach (OscPacket packet in invalid)
                Assert.IsFalse(Parse(packet, out _), packet?.ToString());

            OscMessage missingFields = new OscMessage("/ds4windows/0/press/cross", 1);
            missingFields.Address = null;
            Assert.IsFalse(Parse(missingFields, out _));
            missingFields.Address = "/ds4windows/0/press/cross";
            missingFields.Arguments = null;
            Assert.IsFalse(Parse(missingFields, out _));
        }

        [TestMethod]
        public void RejectsMissingWrongAndOutOfRangeArgumentsWithoutStateChange()
        {
            OscPacket[] invalid =
            {
                new OscMessage("/ds4windows/0/press/cross"),
                new OscMessage("/ds4windows/0/press/cross", 2),
                new OscMessage("/ds4windows/0/press/cross", new object()),
                new OscMessage("/ds4windows/0/stick/lx", -1),
                new OscMessage("/ds4windows/0/stick/lx", 256),
                new OscMessage("/ds4windows/0/stick/lx", float.NaN),
                new OscMessage("/ds4windows/0/stick/left", 0.5f),
                new OscMessage("/ds4windows/0/stick/left", 0.5f, 1.01f),
                new OscMessage("/ds4windows/0/stick/right", double.PositiveInfinity, 0.5),
                new OscMessage("/ds4windows/0/trigger/r2"),
                new OscMessage("/ds4windows/0/trigger/r2", -0.1f),
                new OscMessage("/ds4windows/0/trigger/r2", "not a number"),
                new OscMessage("/ds4windows/0/battery", 1),
            };
            DS4State state = new DS4State { Cross = true, LX = 77, R2 = 42 };
            foreach (OscPacket packet in invalid)
            {
                Assert.IsFalse(Parse(packet, out OscInputCommand command));
                command.ApplyTo(state);
            }
            Assert.IsTrue(state.Cross);
            Assert.AreEqual((byte)77, state.LX);
            Assert.AreEqual((byte)42, state.R2);
        }

        [TestMethod]
        public void MalformedPacketLoggingIsBoundedByMonotonicTime()
        {
            long next = 0;
            Assert.IsTrue(OscInputPacketPolicy.ShouldLogMalformed(ref next, 100, 10));
            Assert.IsFalse(OscInputPacketPolicy.ShouldLogMalformed(ref next, 109, 10));
            Assert.IsTrue(OscInputPacketPolicy.ShouldLogMalformed(ref next, 110, 10));
            Assert.IsFalse(OscInputPacketPolicy.ShouldLogMalformed(ref next, 200, 0));
        }

        [TestMethod]
        public void RandomMalformedMessagesNeverEscapeOrModifyControllerState()
        {
            Random random = new Random(4021);
            string[] segments = { "", "ds4windows", "monitor", "press", "stick",
                "trigger", "cross", "left", "battery", "-1", "8", "bad" };
            object[] values = { null, new object(), -1, 0, 1, 256,
                float.NaN, double.PositiveInfinity, "n/a" };
            DS4State state = new DS4State { Cross = true, LX = 77, R2 = 42 };

            for (int i = 0; i < 1000; i++)
            {
                int segmentCount = random.Next(1, 8);
                string address = "/";
                for (int j = 0; j < segmentCount; j++)
                    address += segments[random.Next(segments.Length)] + "/";
                int argumentCount = random.Next(0, 4);
                object[] arguments = new object[argumentCount];
                for (int j = 0; j < argumentCount; j++)
                    arguments[j] = values[random.Next(values.Length)];

                OscMessage packet = new OscMessage(address, arguments);
                Assert.IsFalse(Parse(packet, out OscInputCommand command,
                    monitoring: true));
                command.ApplyTo(state);
            }

            Assert.IsTrue(state.Cross);
            Assert.AreEqual((byte)77, state.LX);
            Assert.AreEqual((byte)42, state.R2);
        }
    }
}
