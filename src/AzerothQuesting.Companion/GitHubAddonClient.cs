using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;

namespace AzerothQuesting.Companion;

internal sealed record RemoteAddonPackage(string Version, string DownloadUrl, bool FromRelease);
internal sealed record RemoteCompanionPackage(string Version, string DownloadUrl, string? Digest);

internal sealed class GitHubAddonClient : IDisposable
{
    private const string Owner = "Frostcanvas";
    private const string AddonRepository = "AzerothQuesting";
    private const string CompanionRepository = "AzerothQuesting-Companion";
    private readonly HttpClient _httpClient;

    public GitHubAddonClient()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(45),
        };
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("AzerothQuestingCompanion", "0.1.8"));
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    public async Task<RemoteAddonPackage> GetLatestAddonAsync(CancellationToken cancellationToken = default)
    {
        var channel = UpdateChannelSettings.Parse(SettingsService.Load().UpdateChannel);
        var release = await GetBestAddonReleaseAsync(
            includePrereleases: channel == UpdateChannel.Beta,
            cancellationToken);

        if (release is not null)
        {
            return release;
        }

        var tocUrl = $"https://raw.githubusercontent.com/{Owner}/{AddonRepository}/main/AzerothQuesting.toc";
        var toc = await _httpClient.GetStringAsync(tocUrl, cancellationToken);
        var fallbackVersion = ParseTocVersion(toc) ?? "unknown";
        var sourceZip = $"https://github.com/{Owner}/{AddonRepository}/archive/refs/heads/main.zip";
        return new RemoteAddonPackage(fallbackVersion, sourceZip, false);
    }

    public async Task<RemoteCompanionPackage?> GetLatestCompanionAsync(CancellationToken cancellationToken = default)
    {
        var channel = UpdateChannelSettings.Parse(SettingsService.Load().UpdateChannel);
        var best = await GetBestCompanionReleaseAsync(
            includePrereleases: channel == UpdateChannel.Beta,
            cancellationToken);

        if (channel != UpdateChannel.Beta || best is null)
        {
            return best;
        }

        // Apple-style beta train behavior: once this installation is already on a
        // prerelease, an older Stable release must not be presented as the current
        // Beta-channel version. The next eligible update is a newer prerelease or
        // the same/newer base version's Stable (golden) release.
        var runningVersion = GetRunningCompanionVersion();
        if (IsPrereleaseVersion(runningVersion) && ReleaseVersionUtility.Compare(best.Version, runningVersion) < 0)
        {
            return new RemoteCompanionPackage(runningVersion, string.Empty, null);
        }

        return best;
    }

    private async Task<RemoteAddonPackage?> GetBestAddonReleaseAsync(
        bool includePrereleases,
        CancellationToken cancellationToken)
    {
        var releasesUrl = $"https://api.github.com/repos/{Owner}/{AddonRepository}/releases?per_page=100";
        using var response = await _httpClient.GetAsync(releasesUrl, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        RemoteAddonPackage? best = null;
        foreach (var release in document.RootElement.EnumerateArray())
        {
            if (IsDraft(release) || (!includePrereleases && IsPrerelease(release)))
            {
                continue;
            }

            var package = TryReadAddonRelease(release);
            if (package is null)
            {
                continue;
            }

            if (best is null || ReleaseVersionUtility.IsNewer(best.Version, package.Version))
            {
                best = package;
            }
        }

        return best;
    }

    private async Task<RemoteCompanionPackage?> GetBestCompanionReleaseAsync(
        bool includePrereleases,
        CancellationToken cancellationToken)
    {
        var releasesUrl = $"https://api.github.com/repos/{Owner}/{CompanionRepository}/releases?per_page=100";
        using var response = await _httpClient.GetAsync(releasesUrl, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        RemoteCompanionPackage? best = null;
        foreach (var release in document.RootElement.EnumerateArray())
        {
            if (IsDraft(release) || (!includePrereleases && IsPrerelease(release)))
            {
                continue;
            }

            var package = TryReadCompanionRelease(release);
            if (package is null)
            {
                continue;
            }

            if (best is null || ReleaseVersionUtility.IsNewer(best.Version, package.Version))
            {
                best = package;
            }
        }

        return best;
    }

    private static RemoteAddonPackage? TryReadAddonRelease(JsonElement root)
    {
        var tag = root.TryGetProperty("tag_name", out var tagValue) ? tagValue.GetString() : null;
        var version = ReleaseVersionUtility.Normalize(tag);
        if (!root.TryGetProperty("assets", out var assets))
        {
            return null;
        }

        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.TryGetProperty("name", out var nameValue) ? nameValue.GetString() : null;
            if (!string.Equals(name, "AzerothQuesting.zip", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var url = asset.TryGetProperty("browser_download_url", out var urlValue) ? urlValue.GetString() : null;
            if (!string.IsNullOrWhiteSpace(url))
            {
                return new RemoteAddonPackage(version ?? "unknown", url, true);
            }
        }

        return null;
    }

    private static RemoteCompanionPackage? TryReadCompanionRelease(JsonElement root)
    {
        var tag = root.TryGetProperty("tag_name", out var tagValue) ? tagValue.GetString() : null;
        var version = ReleaseVersionUtility.Normalize(tag);
        if (!root.TryGetProperty("assets", out var assets))
        {
            return null;
        }

        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.TryGetProperty("name", out var nameValue) ? nameValue.GetString() : null;
            if (!string.Equals(name, "AzerothQuestingCompanion-Setup.exe", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var url = asset.TryGetProperty("browser_download_url", out var urlValue) ? urlValue.GetString() : null;
            var digest = asset.TryGetProperty("digest", out var digestValue) ? digestValue.GetString() : null;
            if (!string.IsNullOrWhiteSpace(url))
            {
                return new RemoteCompanionPackage(version ?? "unknown", url, digest);
            }
        }

        return null;
    }

    private static string GetRunningCompanionVersion()
    {
        var assembly = typeof(GitHubAddonClient).Assembly;
        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informational))
        {
            var buildMetadata = informational.IndexOf('+');
            return buildMetadata >= 0 ? informational[..buildMetadata] : informational;
        }

        return assembly.GetName().Version?.ToString(3) ?? "unknown";
    }

    private static bool IsPrereleaseVersion(string version)
    {
        var normalized = ReleaseVersionUtility.Normalize(version);
        return !string.IsNullOrWhiteSpace(normalized) && normalized.Contains('-');
    }

    private static bool IsDraft(JsonElement release) =>
        release.TryGetProperty("draft", out var draftValue) && draftValue.ValueKind == JsonValueKind.True;

    private static bool IsPrerelease(JsonElement release) =>
        release.TryGetProperty("prerelease", out var prereleaseValue) && prereleaseValue.ValueKind == JsonValueKind.True;

    public async Task DownloadAsync(string url, string destination, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None);
        await input.CopyToAsync(output, cancellationToken);
    }

    private static string? ParseTocVersion(string toc)
    {
        foreach (var line in toc.Split('\n'))
        {
            if (line.TrimStart().StartsWith("## Version:", StringComparison.OrdinalIgnoreCase))
            {
                return line[(line.IndexOf(':') + 1)..].Trim();
            }
        }

        return null;
    }

    public void Dispose() => _httpClient.Dispose();
}
