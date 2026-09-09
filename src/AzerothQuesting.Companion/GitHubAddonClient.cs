using System.Net;
using System.Net.Http.Headers;
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
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("AzerothQuestingCompanion", "0.1.7"));
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    public async Task<RemoteAddonPackage> GetLatestAddonAsync(CancellationToken cancellationToken = default)
    {
        var channel = UpdateChannelSettings.Parse(SettingsService.Load().UpdateChannel);
        var release = channel == UpdateChannel.Beta
            ? await GetAddonFromReleaseListAsync(cancellationToken)
            : await GetStableAddonReleaseAsync(cancellationToken);

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
        return channel == UpdateChannel.Beta
            ? await GetCompanionFromReleaseListAsync(cancellationToken)
            : await GetStableCompanionReleaseAsync(cancellationToken);
    }

    private async Task<RemoteAddonPackage?> GetStableAddonReleaseAsync(CancellationToken cancellationToken)
    {
        var releaseUrl = $"https://api.github.com/repos/{Owner}/{AddonRepository}/releases/latest";
        using var response = await _httpClient.GetAsync(releaseUrl, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return TryReadAddonRelease(document.RootElement);
    }

    private async Task<RemoteAddonPackage?> GetAddonFromReleaseListAsync(CancellationToken cancellationToken)
    {
        var releasesUrl = $"https://api.github.com/repos/{Owner}/{AddonRepository}/releases?per_page=30";
        using var response = await _httpClient.GetAsync(releasesUrl, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        foreach (var release in document.RootElement.EnumerateArray())
        {
            if (IsDraft(release))
            {
                continue;
            }

            var package = TryReadAddonRelease(release);
            if (package is not null)
            {
                return package;
            }
        }
        return null;
    }

    private async Task<RemoteCompanionPackage?> GetStableCompanionReleaseAsync(CancellationToken cancellationToken)
    {
        var releaseUrl = $"https://api.github.com/repos/{Owner}/{CompanionRepository}/releases/latest";
        using var response = await _httpClient.GetAsync(releaseUrl, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return TryReadCompanionRelease(document.RootElement);
    }

    private async Task<RemoteCompanionPackage?> GetCompanionFromReleaseListAsync(CancellationToken cancellationToken)
    {
        var releasesUrl = $"https://api.github.com/repos/{Owner}/{CompanionRepository}/releases?per_page=30";
        using var response = await _httpClient.GetAsync(releasesUrl, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        foreach (var release in document.RootElement.EnumerateArray())
        {
            if (IsDraft(release))
            {
                continue;
            }

            var package = TryReadCompanionRelease(release);
            if (package is not null)
            {
                return package;
            }
        }
        return null;
    }

    private static RemoteAddonPackage? TryReadAddonRelease(JsonElement root)
    {
        var tag = root.TryGetProperty("tag_name", out var tagValue) ? tagValue.GetString() : null;
        var version = NormalizeVersion(tag);
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
        var version = NormalizeVersion(tag);
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

    private static bool IsDraft(JsonElement release) =>
        release.TryGetProperty("draft", out var draftValue) && draftValue.ValueKind == JsonValueKind.True;

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

    private static string? NormalizeVersion(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        value = value.Trim();
        if (value.StartsWith("companion-v", StringComparison.OrdinalIgnoreCase))
        {
            value = value["companion-v".Length..];
        }
        else if (value.StartsWith('v') || value.StartsWith('V'))
        {
            value = value[1..];
        }

        return value;
    }

    public void Dispose() => _httpClient.Dispose();
}
