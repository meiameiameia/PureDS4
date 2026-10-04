using System;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace DS4Windows;

internal enum ReleaseCheckStatus { Available, Current, Unavailable, Skipped }

internal sealed record ReleaseCheckResult(ReleaseCheckStatus Status, string Tag = "");

/// <summary>
/// Reads public release metadata only. Never downloads assets, follows a
/// server-provided URL, renders release Markdown, or starts an updater.
/// </summary>
internal sealed class ReleaseNotificationService : IDisposable
{
    internal const int MaximumResponseBytes = 256 * 1024;
    internal static readonly Uri MetadataUri = new(ProductIdentity.ReleasesApiUrl + "/latest");
    internal static readonly Uri ReleasePageUri = new(ProductIdentity.RepositoryUrl + "/releases");
    private static readonly Regex VersionTag = new(@"\Av?(\d+\.\d+\.\d+(?:\.\d+)?)\z",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private readonly HttpClient client;
    private readonly bool ownsClient;
    private readonly string statePath;
    private readonly Func<DateTimeOffset> clock;
    private readonly TimeSpan timeout;
    private DateTimeOffset lastAttempt;
    private string cachedTag = "";
    private int checking;
    private bool stateLoaded;

    internal ReleaseNotificationService(HttpClient client, string statePath,
        Func<DateTimeOffset> clock = null, TimeSpan? timeout = null, bool ownsClient = false)
    {
        this.client = client;
        this.statePath = statePath;
        this.clock = clock ?? (() => DateTimeOffset.UtcNow);
        this.timeout = timeout ?? TimeSpan.FromSeconds(10);
        this.ownsClient = ownsClient;
    }

    internal static ReleaseNotificationService Create(string statePath) => new(
        new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
        { Timeout = System.Threading.Timeout.InfiniteTimeSpan }, statePath, ownsClient: true);

    internal async Task<ReleaseCheckResult> CheckAsync(string installedVersion,
        bool automatic, int checkEveryHours, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (automatic && checkEveryHours <= 0)
            return new(ReleaseCheckStatus.Skipped);
        if (!TryVersion(installedVersion, out Version current))
            return new(ReleaseCheckStatus.Unavailable);
        if (Interlocked.CompareExchange(ref checking, 1, 0) != 0)
            return new(ReleaseCheckStatus.Skipped);

        try
        {
            if (!stateLoaded) { ReadState(); stateLoaded = true; }
            DateTimeOffset now = clock();
            TimeSpan interval = TimeSpan.FromHours(Math.Clamp(checkEveryHours, 24, 24 * 30));
            // Small backward clock adjustments do not trigger repeated requests.
            // A wildly future/corrupt timestamp must not suppress checks forever.
            if (automatic && lastAttempt != default && now - lastAttempt < interval &&
                lastAttempt - now <= interval)
                return IsNewer(cachedTag, current)
                    ? new(ReleaseCheckStatus.Available, cachedTag)
                    : new(ReleaseCheckStatus.Skipped);

            lastAttempt = now;
            // If a portable folder cannot persist the throttle, automatic checks
            // stay quiet rather than contacting GitHub on every restart. A user
            // can still explicitly request a manual check.
            if (!WriteState() && automatic)
                return new(ReleaseCheckStatus.Unavailable);

            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(timeout);
            using var request = new HttpRequestMessage(HttpMethod.Get, MetadataUri);
            request.Headers.UserAgent.ParseAdd(ProductIdentity.Name);
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            using HttpResponseMessage response = await client.SendAsync(request,
                HttpCompletionOption.ResponseHeadersRead, deadline.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode ||
                response.Content.Headers.ContentLength > MaximumResponseBytes)
                return new(ReleaseCheckStatus.Unavailable);

            using Stream body = await response.Content.ReadAsStreamAsync(deadline.Token).ConfigureAwait(false);
            using var bounded = new MemoryStream();
            byte[] buffer = new byte[4096];
            int count;
            while ((count = await body.ReadAsync(buffer, deadline.Token).ConfigureAwait(false)) != 0)
            {
                if (bounded.Length + count > MaximumResponseBytes)
                    return new(ReleaseCheckStatus.Unavailable);
                bounded.Write(buffer, 0, count);
            }
            using JsonDocument document = JsonDocument.Parse(bounded.ToArray());
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("draft", out JsonElement draft) || draft.ValueKind != JsonValueKind.False ||
                !root.TryGetProperty("prerelease", out JsonElement prerelease) || prerelease.ValueKind != JsonValueKind.False ||
                !root.TryGetProperty("tag_name", out JsonElement tag) || tag.ValueKind != JsonValueKind.String ||
                !TryVersion(tag.GetString(), out _))
                return new(ReleaseCheckStatus.Unavailable);

            cachedTag = tag.GetString();
            WriteState();
            return IsNewer(cachedTag, current)
                ? new(ReleaseCheckStatus.Available, cachedTag)
                : new(ReleaseCheckStatus.Current);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { return new(ReleaseCheckStatus.Unavailable); }
        catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
        { throw new OperationCanceledException(cancellationToken); }
        catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException)
        { return new(ReleaseCheckStatus.Unavailable); }
        finally { Volatile.Write(ref checking, 0); }
    }

    internal static bool TryVersion(string tag, out Version version)
    {
        version = null;
        if (tag == null || tag.Length > 64) return false;
        Match match = VersionTag.Match(tag);
        if (!match.Success || !Version.TryParse(match.Groups[1].Value, out Version parsed)) return false;
        version = new Version(parsed.Major, parsed.Minor, parsed.Build, Math.Max(0, parsed.Revision));
        return true;
    }

    private static bool IsNewer(string tag, Version current) =>
        TryVersion(tag, out Version version) && version > current;

    private void ReadState()
    {
        try
        {
            if (!Path.IsPathFullyQualified(statePath) || !File.Exists(statePath) ||
                new FileInfo(statePath).Length > 1024) return;
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(statePath));
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return;
            if (root.TryGetProperty("lastAttemptUtc", out JsonElement date) &&
                date.ValueKind == JsonValueKind.String &&
                DateTimeOffset.TryParseExact(date.GetString(), "O", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out DateTimeOffset attempted)) lastAttempt = attempted;
            if (root.TryGetProperty("tag", out JsonElement tag) && tag.ValueKind == JsonValueKind.String &&
                TryVersion(tag.GetString(), out _)) cachedTag = tag.GetString();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { }
    }

    private bool WriteState()
    {
        if (!Path.IsPathFullyQualified(statePath)) return false;
        string temporary = statePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            // Only the update-check cache is written; never settings or profiles.
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, new
                {
                    lastAttemptUtc = lastAttempt.ToString("O", CultureInfo.InvariantCulture),
                    tag = cachedTag,
                });
                stream.Flush(flushToDisk: true);
            }
            if (File.Exists(statePath)) File.Replace(temporary, statePath, null);
            else File.Move(temporary, statePath);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }

    public void Dispose() { if (ownsClient) client.Dispose(); }
}
