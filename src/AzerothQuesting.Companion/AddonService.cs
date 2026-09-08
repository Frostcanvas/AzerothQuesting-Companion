using System.Diagnostics;
using System.IO.Compression;

namespace AzerothQuesting.Companion;

internal sealed record AddonInstallResult(string Version, int MigratedSavedVariableFiles, string BackupDirectory);

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

        if (IsWowRunning())
        {
            throw new InvalidOperationException("World of Warcraft is running. Close WoW before installing or updating the addon.");
        }

        AppPaths.EnsureCreated();
        progress?.Report("Checking the latest Azeroth Questing addon package...");
        var remote = await _github.GetLatestAddonAsync(cancellationToken);

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
            var legacyAddon = Path.Combine(addOnsPath, "ZoneQuestGuide");

            BackupDirectoryIfPresent(targetAddon, Path.Combine(backupRoot, "AzerothQuesting"));
            BackupDirectoryIfPresent(legacyAddon, Path.Combine(backupRoot, "ZoneQuestGuide"));

            var migrated = MigrateSavedVariables(retailPath, progress);

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
            if (Directory.Exists(targetAddon))
            {
                Directory.Delete(targetAddon, recursive: true);
            }

            CopyDirectory(sourceAddon, targetAddon);

            var installedVersion = GetInstalledVersion(retailPath);
            if (installedVersion is null)
            {
                throw new InvalidDataException("The addon copy completed, but AzerothQuesting.toc was not found in the installed folder.");
            }

            // Remove the old addon only after the new addon has been installed and validated.
            if (Directory.Exists(legacyAddon))
            {
                Directory.Delete(legacyAddon, recursive: true);
            }

            progress?.Report($"Azeroth Questing {installedVersion} is installed.");
            return new AddonInstallResult(installedVersion, migrated, backupRoot);
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

    private static int MigrateSavedVariables(string retailPath, IProgress<string>? progress)
    {
        var accountRoot = Path.Combine(retailPath, "WTF", "Account");
        if (!Directory.Exists(accountRoot))
        {
            return 0;
        }

        var copied = 0;
        foreach (var accountDirectory in Directory.EnumerateDirectories(accountRoot))
        {
            var savedVariables = Path.Combine(accountDirectory, "SavedVariables");
            if (!Directory.Exists(savedVariables))
            {
                continue;
            }

            copied += CopyLegacyFileIfNeeded(
                Path.Combine(savedVariables, "ZoneQuestGuide.lua"),
                Path.Combine(savedVariables, "AzerothQuesting.lua"));

            copied += CopyLegacyFileIfNeeded(
                Path.Combine(savedVariables, "ZoneQuestGuide.lua.bak"),
                Path.Combine(savedVariables, "AzerothQuesting.lua.bak"));
        }

        if (copied > 0)
        {
            progress?.Report($"Migrated {copied} legacy SavedVariables file(s).");
        }

        return copied;
    }

    private static int CopyLegacyFileIfNeeded(string source, string destination)
    {
        if (!File.Exists(source) || File.Exists(destination))
        {
            return 0;
        }

        File.Copy(source, destination, overwrite: false);
        return 1;
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
