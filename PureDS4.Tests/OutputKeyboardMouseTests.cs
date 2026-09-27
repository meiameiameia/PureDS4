using DS4Windows.DS4Control;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace DS4WindowsTests;

/// <summary>
/// Mapped keyboard and mouse output goes through SendInput and nothing else.
/// The upstream FakerInput path was removed with its kernel driver, its two
/// bundled installers and its wrapper binaries; this guards the decision so a
/// future change has to be deliberate rather than accidental.
/// </summary>
[TestClass]
public class OutputKeyboardMouseTests
{
    [TestMethod]
    public void EveryRequestResolvesToSendInput()
    {
        foreach (string requested in new[]
            { VirtualKBMFactory.DEFAULT_IDENTIFIER, SendInputHandler.IDENTIFIER, "fakerinput", "anything" })
        {
            VirtualKBMBase handler = VirtualKBMFactory.DetermineHandler(requested);

            Assert.IsInstanceOfType(handler, typeof(SendInputHandler), requested);
            Assert.AreEqual(SendInputHandler.IDENTIFIER, handler.GetIdentifier());
            Assert.IsInstanceOfType(VirtualKBMFactory.GetMappingInstance(requested),
                typeof(SendInputMapping), requested);
        }
    }

    [TestMethod]
    public void OnlySendInputIsAcceptedOnTheCommandLine()
    {
        // -virtualkbm used to take a second handler name. Accepting one now
        // would promise an output path that no longer exists.
        Assert.IsTrue(VirtualKBMFactory.IsValidHandler(SendInputHandler.IDENTIFIER));
        Assert.IsFalse(VirtualKBMFactory.IsValidHandler("fakerinput"));
        Assert.AreEqual(SendInputHandler.IDENTIFIER,
            VirtualKBMFactory.GetFallbackHandlerIdentifier());
    }

    [TestMethod]
    public void TheHandlerConnectsWithoutADriver()
    {
        VirtualKBMBase handler = VirtualKBMFactory.GetFallbackHandler();

        Assert.IsTrue(handler.Connect(), "SendInput needs nothing installed.");
        Assert.IsTrue(handler.Disconnect());
    }

    [TestMethod]
    public void MouseWheelSendsOnlyRequestedAxesWithOneInputStructureSize()
    {
        uint count = 0;
        int structureSize = 0;
        SendInputHandler.INPUT[] events = null;
        int calls = 0;
        var handler = new SendInputHandler((requested, inputs, size) =>
        {
            calls++;
            count = requested;
            events = inputs;
            structureSize = size;
            return requested;
        }, (_, _) => Assert.Fail("A complete send must not report failure."));

        handler.PerformMouseWheelEvent(0, 0);
        Assert.AreEqual(0, calls, "No native call for zero movement.");

        handler.PerformMouseWheelEvent(120, 0);
        Assert.AreEqual(1, calls);
        AssertWheelEvent(1, 0, SendInputHandler.MOUSEEVENTF_WHEEL, 120);

        handler.PerformMouseWheelEvent(0, -120);
        Assert.AreEqual(2, calls);
        AssertWheelEvent(1, 0, SendInputHandler.MOUSEEVENTF_HWHEEL, -120);

        handler.PerformMouseWheelEvent(-240, 360);
        Assert.AreEqual(3, calls);
        AssertWheelEvent(2, 0, SendInputHandler.MOUSEEVENTF_WHEEL, -240);
        AssertWheelEvent(2, 1, SendInputHandler.MOUSEEVENTF_HWHEEL, 360);

        void AssertWheelEvent(uint expectedCount, int index, uint flags,
            int signedWheelData)
        {
            Assert.AreEqual(expectedCount, count);
            Assert.AreEqual((int)expectedCount, events.Length);
            Assert.AreEqual(Marshal.SizeOf<SendInputHandler.INPUT>(),
                structureSize, "cbSize must describe one INPUT.");
            Assert.AreEqual(SendInputHandler.INPUT_MOUSE, events[index].Type);
            Assert.AreEqual(flags, events[index].Data.Mouse.Flags);
            Assert.AreEqual(signedWheelData,
                unchecked((int)events[index].Data.Mouse.MouseData));
        }
    }

    [TestMethod]
    public void MouseWheelReportsZeroAndPartialSendsOnceUntilRecovery()
    {
        var results = new Queue<uint>(new uint[] { 0, 1, 2, 1 });
        var failures = new List<(uint Requested, uint Sent)>();
        var handler = new SendInputHandler((requested, inputs, size) =>
            results.Dequeue(), (requested, sent) =>
                failures.Add((requested, sent)));

        for (int i = 0; i < 4; i++)
        {
            handler.PerformMouseWheelEvent(120, -120);
        }

        Assert.AreEqual(2, failures.Count);
        Assert.AreEqual((2u, 0u), failures[0]);
        Assert.AreEqual((2u, 1u), failures[1]);
    }
}
