using DS4Windows;
using DS4WinWPF.DS4Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DS4WindowsTests;

[TestClass]
public class ReleaseNotificationTests
{
    private static string Metadata(string tag = "v5.1.2") =>
        "{\"tag_name\":\"" + tag + "\",\"draft\":false,\"prerelease\":false," +
        "\"html_url\":\"https://untrusted.example/update.exe\",\"body\":\"untrusted text\"}";

    [DataTestMethod]
    [DataRow("v5.1.2", "5.1.1.0", "Available")]
    [DataRow("v5.1.1", "5.1.1.0", "Current")]
    [DataRow("v5.1.1.0", "5.1.1", "Current")]
    [DataRow("v5.1.0", "5.1.1.0", "Current")]
    [DataRow("v5.1.10", "5.1.9.0", "Available")]
    [DataRow("v5.1.2-beta.1", "5.1.1.0", "Unavailable")]
    [DataRow("upstream-v5.1.2", "5.1.1.0", "Unavailable")]
    [DataRow("v5.1.2\n", "5.1.1.0", "Unavailable")]
    public async Task StableNumericVersionsAreComparedWithoutDowngrades(string tag,
        string installed, string expected)
    {
        using var fixture = new Fixture(Metadata(tag).Replace("\n", "\\n"));
        ReleaseCheckResult result = await fixture.Service.CheckAsync(installed, false, 0, default);
        Assert.AreEqual(expected, result.Status.ToString());
        Assert.AreEqual(1, fixture.Handler.Requests);
        Assert.AreEqual(ReleaseNotificationService.MetadataUri, fixture.Handler.Uri);
        Assert.AreEqual("PureDS4", fixture.Handler.UserAgent);
        Assert.AreEqual("https://github.com/meiameiameia/pureds4/releases",
            ReleaseNotificationService.ReleasePageUri.AbsoluteUri);
    }

    [DataTestMethod]
    [DataRow("{\"tag_name\":\"v5.1.2\",\"draft\":true,\"prerelease\":false}")]
    [DataRow("{\"tag_name\":\"v5.1.2\",\"draft\":false,\"prerelease\":true}")]
    [DataRow("{\"tag_name\":\"v5.1.2\"}")]
    [DataRow("{\"tag_name\":99,\"draft\":false,\"prerelease\":false}")]
    [DataRow("{\"tag_name\":\"v5.1.2\",\"draft\":\"false\",\"prerelease\":false}")]
    [DataRow("[]")]
    [DataRow("null")]
    [DataRow("not json")]
    public async Task InvalidMetadataNeverClaimsTheInstalledVersionIsCurrent(string payload)
    {
        using var fixture = new Fixture(payload);
        Assert.AreEqual(ReleaseCheckStatus.Unavailable,
            (await fixture.Service.CheckAsync("5.1.1", false, 24, default)).Status);
    }

    [DataTestMethod]
    [DataRow(302)]
    [DataRow(403)]
    [DataRow(404)]
    [DataRow(429)]
    [DataRow(500)]
    public async Task HttpFailuresAreNotPresentedAsUpToDate(int status)
    {
        using var fixture = new Fixture(Metadata());
        fixture.Handler.Status = (HttpStatusCode)status;
        Assert.AreEqual(ReleaseCheckStatus.Unavailable,
            (await fixture.Service.CheckAsync("5.1.1", false, 24, default)).Status);
        Assert.AreEqual(1, fixture.Handler.Requests);
    }

    [TestMethod]
    public async Task DisabledAutomaticCheckingDoesNotContactGitHubOrCreateCache()
    {
        using var fixture = new Fixture(Metadata());
        Assert.AreEqual(ReleaseCheckStatus.Skipped,
            (await fixture.Service.CheckAsync("5.1.1", true, 0, default)).Status);
        Assert.AreEqual(0, fixture.Handler.Requests);
        Assert.IsFalse(File.Exists(fixture.StatePath));
        Assert.AreEqual(ReleaseCheckStatus.Available,
            (await fixture.Service.CheckAsync("5.1.1", false, 0, default)).Status);
    }

    [TestMethod]
    public async Task DailyThrottleSurvivesRestartAndShowsCachedAvailableRelease()
    {
        using var fixture = new Fixture(Metadata());
        await fixture.Service.CheckAsync("5.1.1", true, 1, default);
        fixture.Now += TimeSpan.FromHours(23);
        using var restarted = fixture.NewService();
        Assert.AreEqual(ReleaseCheckStatus.Available,
            (await restarted.CheckAsync("5.1.1", true, 1, default)).Status);
        Assert.AreEqual(1, fixture.Handler.Requests);
        Assert.AreEqual(ReleaseCheckStatus.Skipped,
            (await restarted.CheckAsync("5.1.2", true, 1, default)).Status);
        fixture.Now += TimeSpan.FromHours(1);
        await restarted.CheckAsync("5.1.1", true, 1, default);
        Assert.AreEqual(2, fixture.Handler.Requests);
    }

    [TestMethod]
    public async Task FailedCheckIsThrottledAcrossRestartButManualRetryWorks()
    {
        using var fixture = new Fixture(Metadata());
        fixture.Handler.Status = HttpStatusCode.TooManyRequests;
        await fixture.Service.CheckAsync("5.1.1", true, 24, default);
        using var restarted = fixture.NewService();
        Assert.AreEqual(ReleaseCheckStatus.Skipped,
            (await restarted.CheckAsync("5.1.1", true, 24, default)).Status);
        Assert.AreEqual(1, fixture.Handler.Requests);
        fixture.Handler.Status = HttpStatusCode.OK;
        Assert.AreEqual(ReleaseCheckStatus.Available,
            (await restarted.CheckAsync("5.1.1", false, 24, default)).Status);
        Assert.AreEqual(2, fixture.Handler.Requests);
    }

    [TestMethod]
    public async Task ReadOnlyCacheDisablesAutomaticTrafficButNotManualChecking()
    {
        using var fixture = new Fixture(Metadata());
        using var noCache = fixture.NewService(Path.Combine(fixture.Folder, "missing", "state.json"));
        Assert.AreEqual(ReleaseCheckStatus.Unavailable,
            (await noCache.CheckAsync("5.1.1", true, 24, default)).Status);
        Assert.AreEqual(0, fixture.Handler.Requests);
        Assert.AreEqual(ReleaseCheckStatus.Available,
            (await noCache.CheckAsync("5.1.1", false, 24, default)).Status);
        Assert.AreEqual(1, fixture.Handler.Requests);
    }

    [TestMethod]
    public async Task MalformedLocalCacheDoesNotBreakStartup()
    {
        using var fixture = new Fixture(Metadata());
        File.WriteAllText(fixture.StatePath, "{bad");
        using var restarted = fixture.NewService();
        Assert.AreEqual(ReleaseCheckStatus.Available,
            (await restarted.CheckAsync("5.1.1", true, 24, default)).Status);
        Assert.AreEqual(1, fixture.Handler.Requests);
    }

    [TestMethod]
    public async Task FutureCacheTimestampDoesNotSuppressUpdatesForever()
    {
        using var fixture = new Fixture(Metadata());
        await fixture.Service.CheckAsync("5.1.1", true, 24, default);
        fixture.Now -= TimeSpan.FromDays(10);
        using var restarted = fixture.NewService();
        await restarted.CheckAsync("5.1.1", true, 24, default);
        Assert.AreEqual(2, fixture.Handler.Requests);
    }

    [TestMethod]
    public async Task OfflineAndTimeoutChecksAreBoundedFailures()
    {
        using var fixture = new Fixture(Metadata());
        fixture.Handler.Work = _ => throw new HttpRequestException("offline");
        Assert.AreEqual(ReleaseCheckStatus.Unavailable,
            (await fixture.Service.CheckAsync("5.1.1", false, 24, default)).Status);
        fixture.Handler.Work = async token => await Task.Delay(Timeout.InfiniteTimeSpan, token);
        using var shortTimeout = fixture.NewService(timeout: TimeSpan.FromMilliseconds(30));
        Assert.AreEqual(ReleaseCheckStatus.Unavailable,
            (await shortTimeout.CheckAsync("5.1.1", false, 24, default)).Status);
    }

    [TestMethod]
    public async Task CancellationAndConcurrentChecksDoNotStartExtraRequests()
    {
        using var fixture = new Fixture(Metadata());
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Handler.Work = async token =>
        {
            entered.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
        };
        using var cancellation = new CancellationTokenSource();
        Task<ReleaseCheckResult> pending = fixture.Service.CheckAsync("5.1.1", false, 24, cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.AreEqual(ReleaseCheckStatus.Skipped,
            (await fixture.Service.CheckAsync("5.1.1", false, 24, default)).Status);
        Assert.AreEqual(1, fixture.Handler.Requests);
        cancellation.Cancel();
        try { await pending; Assert.Fail("Closing must cancel the pending check."); }
        catch (OperationCanceledException) { }
    }

    [TestMethod]
    public async Task OversizedResponseIsRejectedWithAndWithoutContentLength()
    {
        using var fixture = new Fixture(new string(' ', ReleaseNotificationService.MaximumResponseBytes + 1));
        Assert.AreEqual(ReleaseCheckStatus.Unavailable,
            (await fixture.Service.CheckAsync("5.1.1", false, 24, default)).Status);
        fixture.Handler.UnknownLength = true;
        Assert.AreEqual(ReleaseCheckStatus.Unavailable,
            (await fixture.Service.CheckAsync("5.1.1", false, 24, default)).Status);
    }

    [TestMethod]
    [DoNotParallelize]
    public void NotificationBannerCanBeDismissedAndOnlyRequestsAnActionOnClick()
    {
        WpfTestHost.Run(() =>
        {
            WpfTestHost.LoadApplicationThemes(replaceExisting: true);
            var banner = new StatusBanner
            {
                Title = "PureDS4 5.1.2 is available",
                Message = "Download and installation stay manual.",
                ActionLabel = "Open release page", ShowAction = true, IsOpen = true,
            };
            int actions = 0;
            banner.ActionRequested += (_, _) => actions++;
            banner.Measure(new Size(704, 120));
            banner.Arrange(new Rect(0, 0, 704, banner.DesiredSize.Height));
            Assert.AreEqual(0, actions);
            Button action = Descendants(banner).OfType<Button>()
                .Single(b => Equals(b.Content, "Open release page"));
            action.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.AreEqual(1, actions);
            Button dismiss = Descendants(banner).OfType<Button>().Single(b => !ReferenceEquals(b, action));
            dismiss.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.IsFalse(banner.IsOpen);
            Assert.AreEqual(1, actions);
        });
    }

    [TestMethod]
    public async Task RelativeCachePathCannotWriteToTheWorkingDirectory()
    {
        using var fixture = new Fixture(Metadata());
        using var service = fixture.NewService("relative-release-check.json");
        Assert.AreEqual(ReleaseCheckStatus.Unavailable,
            (await service.CheckAsync("5.1.1", true, 24, default)).Status);
        Assert.AreEqual(0, fixture.Handler.Requests);
    }

    private static System.Collections.Generic.IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, index);
            yield return child;
            foreach (DependencyObject nested in Descendants(child)) yield return nested;
        }
    }

    private sealed class Fixture : IDisposable
    {
        public string Folder { get; } = Path.Combine(Path.GetTempPath(), "PureDS4-release-tests-" + Guid.NewGuid().ToString("N"));
        public string StatePath => Path.Combine(Folder, "release-check.json");
        public DateTimeOffset Now = new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);
        public Handler Handler { get; }
        private readonly HttpClient client;
        public ReleaseNotificationService Service { get; }
        public Fixture(string payload)
        {
            Directory.CreateDirectory(Folder);
            Handler = new Handler(payload);
            client = new HttpClient(Handler);
            Service = NewService();
        }
        public ReleaseNotificationService NewService(string path = null, TimeSpan? timeout = null) =>
            new(client, path ?? StatePath, () => Now, timeout);
        public void Dispose()
        {
            client.Dispose();
            Directory.Delete(Folder, recursive: true);
        }
    }

    private sealed class Handler(string payload) : HttpMessageHandler
    {
        public int Requests;
        public Uri Uri;
        public string UserAgent;
        public HttpStatusCode Status = HttpStatusCode.OK;
        public Func<CancellationToken, Task> Work;
        public bool UnknownLength;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Interlocked.Increment(ref Requests);
            Uri = request.RequestUri;
            UserAgent = request.Headers.UserAgent.ToString();
            if (Work != null) await Work(token);
            HttpContent content = UnknownLength
                ? new NoLengthContent(payload)
                : new StringContent(payload);
            if (UnknownLength) content.Headers.ContentLength = null;
            return new HttpResponseMessage(Status) { Content = content };
        }
    }

    private sealed class NoLengthContent(string payload) : HttpContent
    {
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext context) =>
            stream.WriteAsync(Encoding.UTF8.GetBytes(payload)).AsTask();
        protected override Task<Stream> CreateContentReadStreamAsync() =>
            Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes(payload)));
    }
}
