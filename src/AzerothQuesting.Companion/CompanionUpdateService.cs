using System.Diagnostics;
using System.Security.Cryptography;

namespace AzerothQuesting.Companion;

internal sealed class CompanionUpdateService
{
    private readonly GitHubAddonClient _github;

    public CompanionUpdateService(GitHubAddonClient github)
    {
        _github = github;
    }

    public async Task StageAndLaunchUpdateAsync(
        RemoteCompanionPackage package,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        AppPaths.EnsureCreated();

        var updateRoot = Path.Combine(AppPaths.Root, "Updates", package.Version);
        var installerPath = Path.Combine(updateRoot, "AzerothQuestingCompanion-Setup.exe");
        Directory.CreateDirectory(updateRoot);

        progress?.Report($"Downloading Azeroth Questing Companion {package.Version} installer...");
        await _github.DownloadAsync(package.DownloadUrl, installerPath, cancellationToken);
        VerifyDigestIfPresent(installerPath, package.Digest);

        progress?.Report("Starting the Windows installer. Approve the Windows permission prompt if it appears.");

        var startInfo = new ProcessStartInfo
        {
            FileName = installerPath,
            UseShellExecute = true,
            Verb = "runas",
            WorkingDirectory = updateRoot,
        };
        startInfo.ArgumentList.Add("/VERYSILENT");
        startInfo.ArgumentList.Add("/SUPPRESSMSGBOXES");
        startInfo.ArgumentList.Add("/NORESTART");
        startInfo.ArgumentList.Add("/CLOSEAPPLICATIONS");
        startInfo.ArgumentList.Add("/RESTARTAPPLICATIONS");

        var process = Process.Start(startInfo);
        if (process is null)
        {
            throw new InvalidOperationException("Windows did not start the companion installer.");
        }
    }

    public static void CleanupStaleUpdateDirectories()
    {
        CleanupCurrentInstallerCache();
        CleanupLegacyTemporaryUpdater();
    }

    private static void CleanupCurrentInstallerCache()
    {
        var root = Path.Combine(AppPaths.Root, "Updates");
        if (!Directory.Exists(root))
        {
            return;
        }

        foreach (var directory in Directory.EnumerateDirectories(root))
        {
            try
            {
                var created = Directory.GetCreationTimeUtc(directory);
                if (DateTime.UtcNow - created > TimeSpan.FromDays(7))
                {
                    Directory.Delete(directory, recursive: true);
                }
            }
            catch
            {
                // Update cleanup is best effort and must never block startup.
            }
        }
    }

    private static void CleanupLegacyTemporaryUpdater()
    {
        var legacyRoot = Path.Combine(
            Path.GetTempPath(),
            "AzerothQuestingCompanion",
            "updates");

        try
        {
            if (Directory.Exists(legacyRoot))
            {
                Directory.Delete(legacyRoot, recursive: true);
            }
        }
        catch
        {
            // v0.1.1/v0.1.2 temporary updater files may still be locked by endpoint
            // protection or an old process. They are harmless and can be removed later.
        }
    }

    private static void VerifyDigestIfPresent(string filePath, string? digest)
    {
        if (string.IsNullOrWhiteSpace(digest) || !digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var expected = digest["sha256:".Length..].Trim();
        using var stream = File.OpenRead(filePath);
        var actual = Convert.ToHexString(SHA256.HashData(stream));
        if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The downloaded companion installer failed its SHA-256 verification.");
        }
    }
}
