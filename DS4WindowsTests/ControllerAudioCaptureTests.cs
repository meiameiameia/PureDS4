using System.Collections.Generic;
using System.Diagnostics;
using DS4Windows;
using DS4WinWPF.DS4Forms.ViewModels;

namespace DS4WindowsTests
{
    /// <summary>
    /// Process-loopback capture and controller audio endpoint selection.
    /// These cover the shared app-audio path that the DualShock 4 speaker
    /// route uses. They were written alongside Audio Haptics and were kept
    /// when that feature was removed.
    /// </summary>
    [TestClass]
    public class ControllerAudioCaptureTests
    {
        [TestMethod]
        public void AutomaticGameAudioLabelUsesSelectedVirtualEndpoint()
        {
            var endpoints = new List<AudioEndpointSnapshot>
            {
                new("Speakers (Wireless Controller)", "ds4", true,
                    ControllerAudioEndpointKind.DualShock4, 7),
                new("Speakers (DualSense Wireless Controller)", "ds5", true,
                    ControllerAudioEndpointKind.DualSense, 9),
            };

            string label = AudioEndpointChoiceCache
                .BuildAutomaticControllerAudioName(
                    OutContType.ViiperDualSense, 9, endpoints);

            Assert.AreEqual(
                "Game Audio (Speakers (DualSense Wireless Controller))",
                label);
        }

        [TestMethod]
        public void AutomaticGameAudioLabelFallsBackBeforeEndpointExists()
        {
            string label = AudioEndpointChoiceCache
                .BuildAutomaticControllerAudioName(
                    OutContType.ViiperDualSense, -1,
                    Array.Empty<AudioEndpointSnapshot>());

            Assert.AreEqual("Game Audio (virtual controller endpoint)", label);
        }

        [TestMethod]
        public void ManifestGameBeatsAnUnclassifiedAudioApp()
        {
            GameAudioCandidate game = AutomaticGameAudioDetector.ScoreCandidate(
                42, "game.exe", @"C:\Games\Example\game.exe", "Example",
                hasActiveAudio: true, isForeground: false,
                fullscreenDirect3D: false,
                GameDetectionEvidence.InstalledGameManifest, "Example Game");
            GameAudioCandidate app = AutomaticGameAudioDetector.ScoreCandidate(
                43, "music.exe", @"C:\Apps\music.exe", "Music",
                hasActiveAudio: true, isForeground: true,
                fullscreenDirect3D: false, GameDetectionEvidence.None,
                string.Empty);

            Assert.IsNotNull(game);
            Assert.AreEqual("Example Game", game.DisplayName);
            Assert.IsNull(app,
                "Ordinary foreground audio must never be guessed to be a game.");
        }

        [TestMethod]
        public void FullscreenDirect3DForegroundAppIsAWindowsGameCandidate()
        {
            GameAudioCandidate candidate =
                AutomaticGameAudioDetector.ScoreCandidate(42, "unknown.exe",
                    @"C:\Games\Unknown\unknown.exe", "Unknown Game",
                    hasActiveAudio: true, isForeground: true,
                    fullscreenDirect3D: true, GameDetectionEvidence.None,
                    string.Empty);

            Assert.IsNotNull(candidate);
            Assert.AreEqual(GameDetectionEvidence.FullscreenDirect3D,
                candidate.Evidence);
        }

        [TestMethod]
        public void LauncherProcessesAreNeverAutomaticGameCandidates()
        {
            GameAudioCandidate candidate =
                AutomaticGameAudioDetector.ScoreCandidate(42,
                    "EpicGamesLauncher.exe",
                    @"C:\Apps\EpicGamesLauncher.exe", "Epic",
                    hasActiveAudio: true, isForeground: true,
                    fullscreenDirect3D: true,
                    GameDetectionEvidence.WindowsGameRecord, "Epic");

            Assert.IsNull(candidate);
        }

        [TestMethod]
        public void AutomaticProcessEndpointRoundTripsControllerSlot()
        {
            string endpoint = ProcessLoopbackWaveCapture
                .BuildAutomaticEndpointId(2);

            Assert.IsTrue(ProcessLoopbackWaveCapture
                .TryParseAutomaticEndpointId(endpoint, out int slot));
            Assert.AreEqual(2, slot);
            Assert.IsFalse(ProcessLoopbackWaveCapture.TryParseEndpointId(
                endpoint, out _));
        }

        [TestMethod]
        public void InstalledCatalogMatchesExactWindowsRecordsAndGameRoots()
        {
            InstalledGameCatalog catalog = InstalledGameCatalog.FromEntries(
                new[]
                {
                    (@"C:\Recorded\game.exe", "Recorded Game",
                        GameDetectionEvidence.WindowsGameRecord),
                    (@"C:\Steam\common\Example", "Manifest Game",
                        GameDetectionEvidence.InstalledGameManifest),
                });

            Assert.AreEqual(GameDetectionEvidence.WindowsGameRecord,
                catalog.Match(@"C:\Recorded\game.exe", "game", string.Empty,
                    out string recordedName));
            Assert.AreEqual("Recorded Game", recordedName);
            Assert.AreEqual(GameDetectionEvidence.WindowsGameRecord,
                catalog.Match(string.Empty, "game.exe", string.Empty,
                    out string executableName));
            Assert.AreEqual("Recorded Game", executableName);
            Assert.AreEqual(GameDetectionEvidence.InstalledGameManifest,
                catalog.Match(@"C:\Steam\common\Example\bin\game.exe",
                    "game", string.Empty, out string manifestName));
            Assert.AreEqual("Manifest Game", manifestName);
            Assert.AreEqual(GameDetectionEvidence.InstalledGameManifest,
                catalog.Match(string.Empty, "unknown.exe",
                    "Manifest Game - DirectX 12", out string windowName));
            Assert.AreEqual("Manifest Game", windowName);
        }

        [TestMethod]
        public void AppCaptureUsesReferenceEventDrivenFlags()
        {
            NAudio.CoreAudioApi.AudioClientStreamFlags flags =
                ProcessLoopbackWaveCapture.CaptureStreamFlags;

            Assert.IsTrue((flags &
                NAudio.CoreAudioApi.AudioClientStreamFlags.Loopback) != 0);
            Assert.IsTrue((flags &
                NAudio.CoreAudioApi.AudioClientStreamFlags.EventCallback) != 0);
            Assert.IsTrue((flags &
                NAudio.CoreAudioApi.AudioClientStreamFlags.AutoConvertPcm) != 0);
            Assert.IsTrue((flags &
                NAudio.CoreAudioApi.AudioClientStreamFlags.SrcDefaultQuality) != 0);
        }

        [TestMethod]
        public void AppCaptureUsesReferenceProcessLoopbackFormat()
        {
            using var capture = new ProcessLoopbackWaveCapture(1);

            Assert.AreEqual(48000, capture.WaveFormat.SampleRate);
            Assert.AreEqual(2, capture.WaveFormat.Channels);
            Assert.AreEqual(32, capture.WaveFormat.BitsPerSample);
            Assert.AreEqual(NAudio.Wave.WaveFormatEncoding.IeeeFloat,
                capture.WaveFormat.Encoding);
        }

        [TestMethod]
        public void AudibleProcessedRouteRecoversOnlyAfterCallbackStall()
        {
            long threshold = Stopwatch.Frequency *
                ProcessLoopbackWaveCapture.ProcessedRouteStallMilliseconds /
                1000;

            Assert.IsFalse(ProcessLoopbackWaveCapture
                .ShouldRecoverProcessedRoute(
                    100, 100 + threshold - 1, targetRouteAudible: true));
            Assert.IsFalse(ProcessLoopbackWaveCapture
                .ShouldRecoverProcessedRoute(
                    100, 100 + threshold, targetRouteAudible: false));
            Assert.IsTrue(ProcessLoopbackWaveCapture
                .ShouldRecoverProcessedRoute(
                    100, 100 + threshold, targetRouteAudible: true));
        }

        [TestMethod]
        public void ProcessLoopbackRecoversFromMissingPacketsEvenWhenSilent()
        {
            long threshold = Stopwatch.Frequency *
                ProcessLoopbackWaveCapture.ProcessLoopbackStallMilliseconds /
                1000;

            Assert.IsFalse(ProcessLoopbackWaveCapture
                .ShouldRecoverProcessLoopback(100,
                    100 + threshold - 1));
            Assert.IsTrue(ProcessLoopbackWaveCapture
                .ShouldRecoverProcessLoopback(100,
                    100 + threshold));
            Assert.IsFalse(ProcessLoopbackWaveCapture
                .ShouldRecoverProcessLoopback(0,
                    100 + threshold));
        }

        [TestMethod]
        public void LiveProcessCaptureCanActivateApplicationLoopback()
        {
            string processIdText = Environment.GetEnvironmentVariable(
                "DS4W_TEST_PROCESS_LOOPBACK_PID");
            if (!int.TryParse(processIdText, out int processId) ||
                processId <= 0)
            {
                Assert.Inconclusive(
                    "Set DS4W_TEST_PROCESS_LOOPBACK_PID for this opt-in integration test.");
            }

            using var capture = new ProcessLoopbackWaveCapture(processId,
                followExclusiveRenderRoute: true);
            using var received = new ManualResetEventSlim(false);
            capture.DataAvailable += (_, eventArgs) =>
            {
                if (eventArgs.BytesRecorded > 0)
                {
                    received.Set();
                }
            };
            capture.StartRecording();
            Assert.IsTrue(capture.CurrentProcessId > 0);
            Assert.IsTrue(received.Wait(TimeSpan.FromSeconds(3)),
                "The selected application produced no captured PCM.");
        }

        [TestMethod]
        public void LiveProcessCaptureDeliversAudiblePcm()
        {
            string processIdText = Environment.GetEnvironmentVariable(
                "DS4W_TEST_PROCESS_LOOPBACK_PID");
            if (!int.TryParse(processIdText, out int processId) ||
                processId <= 0)
            {
                Assert.Inconclusive(
                    "Set DS4W_TEST_PROCESS_LOOPBACK_PID for this opt-in integration test.");
            }

            using var capture = new ProcessLoopbackWaveCapture(processId);
            using var audible = new ManualResetEventSlim(false);
            long callbackCount = 0;
            long silentCallbackCount = 0;
            capture.DataAvailable += (_, eventArgs) =>
            {
                Interlocked.Increment(ref callbackCount);
                bool hasAudibleSample = false;
                for (int offset = 0; offset + sizeof(float) <=
                        eventArgs.BytesRecorded; offset += sizeof(float))
                {
                    float sample = BitConverter.ToSingle(eventArgs.Buffer,
                        offset);
                    if (float.IsFinite(sample) && Math.Abs(sample) > 0.0001f)
                    {
                        hasAudibleSample = true;
                        break;
                    }
                }

                if (hasAudibleSample)
                {
                    audible.Set();
                }
                else
                {
                    Interlocked.Increment(ref silentCallbackCount);
                }
            };

            capture.StartRecording();
            Assert.IsTrue(audible.Wait(TimeSpan.FromSeconds(5)),
                $"The selected application produced only silence across " +
                $"{callbackCount} callbacks ({silentCallbackCount} silent).");
        }

        [TestMethod]
        public void LiveProcessCaptureKeepsDeliveringApplicationAudio()
        {
            string processIdText = Environment.GetEnvironmentVariable(
                "DS4W_TEST_PROCESS_LOOPBACK_PID");
            if (!int.TryParse(processIdText, out int processId) ||
                processId <= 0)
            {
                Assert.Inconclusive(
                    "Set DS4W_TEST_PROCESS_LOOPBACK_PID for this opt-in integration test.");
            }

            using var capture = new ProcessLoopbackWaveCapture(processId,
                followExclusiveRenderRoute: true);
            using var received = new ManualResetEventSlim(false);
            long callbackCount = 0;
            long lastCallback = 0;
            long maximumGap = 0;
            capture.DataAvailable += (_, eventArgs) =>
            {
                if (eventArgs.BytesRecorded <= 0)
                {
                    return;
                }

                long now = Stopwatch.GetTimestamp();
                long previous = Interlocked.Exchange(ref lastCallback, now);
                if (previous > 0)
                {
                    long gap = now - previous;
                    long observed;
                    do
                    {
                        observed = Volatile.Read(ref maximumGap);
                        if (gap <= observed) break;
                    }
                    while (Interlocked.CompareExchange(ref maximumGap, gap,
                        observed) != observed);
                }
                Interlocked.Increment(ref callbackCount);
                received.Set();
            };

            capture.StartRecording();
            Assert.IsTrue(received.Wait(TimeSpan.FromSeconds(3)),
                "The selected application produced no captured PCM.");
            Thread.Sleep(TimeSpan.FromSeconds(5));

            long now = Stopwatch.GetTimestamp();
            long last = Volatile.Read(ref lastCallback);
            double lastAgeMilliseconds = (now - last) * 1000.0 /
                Stopwatch.Frequency;
            double maximumGapMilliseconds =
                Volatile.Read(ref maximumGap) * 1000.0 /
                Stopwatch.Frequency;
            Assert.IsTrue(Interlocked.Read(ref callbackCount) >= 50,
                $"Only {callbackCount} callbacks arrived during live capture.");
            Assert.IsTrue(lastAgeMilliseconds < 1000.0,
                $"Live capture stopped {lastAgeMilliseconds:F1} ms before validation.");
            Assert.IsTrue(maximumGapMilliseconds < 1500.0,
                $"Live capture stalled for {maximumGapMilliseconds:F1} ms.");
        }

        [TestMethod]
        public void AppCaptureDoesNotClimbIntoADifferentExecutableParent()
        {
            int currentProcessId = Environment.ProcessId;

            Assert.AreEqual(currentProcessId,
                ProcessLoopbackWaveCapture.ResolveCaptureRootProcessId(
                    currentProcessId));
            Assert.AreEqual(0,
                ProcessLoopbackWaveCapture.ResolveCaptureRootProcessId(0));
        }
    }
}
