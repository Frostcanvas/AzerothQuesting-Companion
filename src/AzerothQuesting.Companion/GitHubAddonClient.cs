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
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("AzerothQuestingCompanion", "0.1.4"));
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    public async Task<RemoteAddonPackage> GetLatestAddonAsync(CancellationToken cancellationToken = default)
    {
        var releaseUrl = $"https://api.github.com/repos/{Owner}/{AddonRepository}/releases/latest";
        using (var releaseResponse = await _httpClient.GetAsync(releaseUrl, cancellationToken))
        {
            if (releaseResponse.IsSuccessStatusCode)
            {
                await using var stream = await releaseResponse.Content.ReadAsStreamAsync(cancellationToken);
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                var root = document.RootElement;
                var tag = root.TryGetProperty("tag_name", out var tagValue) ? tagValue.GetString() : null;
                var version = NormalizeVersion(tag);

                if (root.TryGetProperty("assets", out var assets))
                {
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
                }
            }
        }

        var tocUrl = $"https://raw.githubusercontent.com/{Owner}/{AddonRepository}/main/AzerothQuesting.toc";
        var toc = await _httpClient.GetStringAsync(tocUrl, cancellationToken);
        var fallbackVersion = ParseTocVersion(toc) ?? "unknown";
        var sourceZip = $"https://github.com/{Owner}/{AddonRepository}/archive/refs/heads/main.zip";
        return new RemoteAddonPackage(fallbackVersion, sourceZip, false);
    }

    public async Task<RemoteCompanionPackage?> GetLatestCompanionAsync(CancellationToken cancellationToken = default)
    {
        var releaseUrl = $"https://api.github.com/repos/{Owner}/{CompanionRepository}/releases/latest";
        using var releaseResponse = await _httpClient.GetAsync(releaseUrl, cancellationToken);
        if (releaseResponse.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        releaseResponse.EnsureSuccessStatusCode();
        await using var stream = await releaseResponse.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;
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
