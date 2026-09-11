using System.Diagnostics;
using System.IO.Compression;

namespace AzerothQuesting.Companion;

internal sealed record AddonInstallResult(string Version, string BackupDirectory);

internal sealed class AddonService
{
    private readonly GitHubAddonClient _github;

    public AddonService(GitHubAddonClient github)
    {
        _github = github;
    }

    public static string GetAddOnsPath(string retailPath) => Path.Combine(retailPath, "Interface", "AddOns");

    public static string GetAddonPath(string retailPath) => Path.Combine(GetAddOnsPath(retailPath), "AzerothQuesting");

    public static string? GetInstalledVersion(string retailPath)
    {
        var tocPath = Path.Combine(GetAddonPath(retailPath), "AzerothQuesting.toc");
        if (!File.Exists(tocPath))
        {
            return null;
        }

        try
        {
            foreach (var line in File.ReadLines(tocPath))
            {
                if (line.TrimStart().StartsWith("## Version:", StringComparison.OrdinalIgnoreCase))
                {
                    return line[(line.IndexOf(':') + 1)..].Trim();
                }
            }
        }
        catch
        {
            // Status checks should not crash the companion.
        }

        return "unknown";
    }

    public static bool IsWowRunning()
    {
        foreach (var name in new[] { "Wow", "WowT", "WowClassic", "WowClassicT" })
        {
            Process[] processes = [];
            try
            {
                processes = Process.GetProcessesByName(name);
                if (processes.Length > 0)
                {
                    return true;
                }
            }
            finally
            {
                foreach (var process in processes)
                {
                    process.Dispose();
                }
            }
        }

        return false;
    }

    public async Task<AddonInstallResult> InstallOrUpdateAsync(
        string retailPath,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!WowLocator.IsRetailPath(retailPath))
        {
            throw new InvalidOperationException("The selected folder is not a valid World of Warcraft Retail installation.");
        }

        var wowWasRunning = IsWowRunning();
        if (wowWasRunning)
        {
            progress?.Report("World of Warcraft is running. Updating the addon files on disk now; use /reload or relog after the update to load the new version.");
        }

        AppPaths.EnsureCreated();
        progress?.Report("Checking the latest Azeroth Questing addon package...");
        var remote = await _github.GetLatestAddonAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(remote.DownloadUrl))
        {
            throw new InvalidOperationException(
                $"Azeroth Questing {remote.Version} is already on the Beta train. " +
                "No newer Beta or golden Stable release is available yet.");
        }

        var workRoot = Path.Combine(Path.GetTempPath(), "AzerothQuestingCompanion", Guid.NewGuid().ToString("N"));
        var zipPath = Path.Combine(workRoot, "AzerothQuesting.zip");
        var extractPath = Path.Combine(workRoot, "extract");
        Directory.CreateDirectory(workRoot);

        try
        {
            progress?.Report($"Downloading Azeroth Questing {remote.Version}...");
            await _github.DownloadAsync(remote.DownloadUrl, zipPath, cancellationToken);

            progress?.Report("Preparing addon backup...");
            var backupRoot = Path.Combine(AppPaths.Backups, DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            Directory.CreateDirectory(backupRoot);

            var addOnsPath = GetAddOnsPath(retailPath);
            Directory.CreateDirectory(addOnsPath);
            var targetAddon = Path.Combine(addOnsPath, "AzerothQuesting");
            BackupDirectoryIfPresent(targetAddon, Path.Combine(backupRoot, "AzerothQuesting"));

            progress?.Report("Installing addon files...");
            Directory.CreateDirectory(extractPath);
            ZipFile.ExtractToDirectory(zipPath, extractPath, overwriteFiles: true);

            var toc = Directory
                .EnumerateFiles(extractPath, "AzerothQuesting.toc", SearchOption.AllDirectories)
                .FirstOrDefault();

            if (toc is null)
            {
                throw new InvalidDataException("The downloaded package did not contain AzerothQuesting.toc.");
            }

            var sourceAddon = Path.GetDirectoryName(toc)!;
            var stagedAddon = Path.Combine(addOnsPath, $".AzerothQuesting.update-{Guid.NewGuid():N}");
            try
            {
                CopyDirectory(sourceAddon, stagedAddon);
                InstallStagedDirectory(stagedAddon, targetAddon, progress);
            }
            finally
            {
                TryDeleteDirectory(stagedAddon);
            }

            var installedVersion = GetInstalledVersion(retailPath);
            if (installedVersion is null)
            {
                throw new InvalidDataException("The addon copy completed, but AzerothQuesting.toc was not found in the installed folder.");
            }

            progress?.Report(wowWasRunning
                ? $"Azeroth Questing {installedVersion} is updated on disk. Use /reload or relog in WoW to load it."
                : $"Azeroth Questing {installedVersion} is installed.");
            return new AddonInstallResult(installedVersion, backupRoot);
        }
        finally
        {
            try
            {
                if (Directory.Exists(workRoot))
                {
                    Directory.Delete(workRoot, recursive: true);
                }
            }
            catch
            {
                // Temporary cleanup can be retried by Windows later.
            }
        }
    }

    private static void InstallStagedDirectory(
        string stagedAddon,
        string targetAddon,
        IProgress<string>? progress)
    {
        var addOnsPath = Path.GetDirectoryName(targetAddon)
            ?? throw new InvalidOperationException("Could not determine the WoW AddOns folder.");
        var previousAddon = Path.Combine(addOnsPath, $".AzerothQuesting.previous-{Guid.NewGuid():N}");
        var targetMoved = false;

        try
        {
            if (Directory.Exists(targetAddon))
            {
                Directory.Move(targetAddon, previousAddon);
                targetMoved = true;
            }

            Directory.Move(stagedAddon, targetAddon);
            TryDeleteDirectory(previousAddon);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            if (!Directory.Exists(targetAddon) && targetMoved && Directory.Exists(previousAddon))
            {
                try
                {
                    Directory.Move(previousAddon, targetAddon);
                }
                catch (Exception restoreEx)
                {
                    throw new IOException(
                        "The addon update could not swap folders and the previous addon folder could not be restored automatically. " +
                        "Use the Companion backup before retrying.",
                        new AggregateException(ex, restoreEx));
                }
            }

            progress?.Report("Windows could not swap the addon folder atomically; updating the addon files in place instead...");
            CopyDirectory(stagedAddon, targetAddon);
            TryDeleteDirectory(previousAddon);
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch
        {
            // The normal timestamped backup is already retained. A temporary
            // swap folder can be cleaned up by Windows or a later maintenance pass.
        }
    }

    private static void BackupDirectoryIfPresent(string source, string destination)
    {
        if (Directory.Exists(source))
        {
            CopyDirectory(source, destination);
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);

        foreach (var directory in Directory.EnumerateDirectories(source))
        {
            var name = Path.GetFileName(directory);
            if (name is ".git" or ".github" or "package" or "artifacts")
            {
                continue;
            }

            CopyDirectory(directory, Path.Combine(destination, name));
        }

        foreach (var file in Directory.EnumerateFiles(source))
        {
            var name = Path.GetFileName(file);
            if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            File.Copy(file, Path.Combine(destination, name), overwrite: true);
        }
    }
}
