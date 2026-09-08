using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;

namespace AzerothQuesting.Companion;

internal sealed class CompanionUpdateService
{
    private const string UpdateSwitch = "--apply-update";
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
        var targetExe = Environment.ProcessPath ?? Application.ExecutablePath;
        if (string.IsNullOrWhiteSpace(targetExe) || !File.Exists(targetExe))
        {
            throw new InvalidOperationException("The companion could not determine its current executable path.");
        }

        var updateRoot = Path.Combine(
            Path.GetTempPath(),
            "AzerothQuestingCompanion",
            "updates",
            Guid.NewGuid().ToString("N"));
        var zipPath = Path.Combine(updateRoot, "AzerothQuestingCompanion-win-x64.zip");
        var extractPath = Path.Combine(updateRoot, "extract");
        var helperExe = Path.Combine(updateRoot, "AzerothQuestingCompanion.Updater.exe");

        Directory.CreateDirectory(updateRoot);

        progress?.Report($"Downloading Azeroth Questing Companion {package.Version}...");
        await _github.DownloadAsync(package.DownloadUrl, zipPath, cancellationToken);
        VerifyDigestIfPresent(zipPath, package.Digest);

        progress?.Report("Preparing companion update...");
        Directory.CreateDirectory(extractPath);
        ZipFile.ExtractToDirectory(zipPath, extractPath, overwriteFiles: true);

        var stagedExe = Directory
            .EnumerateFiles(extractPath, "AzerothQuestingCompanion.exe", SearchOption.AllDirectories)
            .FirstOrDefault();
        if (stagedExe is null)
        {
            throw new InvalidDataException("The companion update package did not contain AzerothQuestingCompanion.exe.");
        }

        // A running Windows executable cannot safely overwrite itself. Copy the current
        // companion to a temporary helper, then let that helper replace and restart us.
        File.Copy(targetExe, helperExe, overwrite: true);

        var startInfo = new ProcessStartInfo
        {
            FileName = helperExe,
            UseShellExecute = true,
            WorkingDirectory = updateRoot,
        };
        startInfo.ArgumentList.Add(UpdateSwitch);
        startInfo.ArgumentList.Add(Environment.ProcessId.ToString());
        startInfo.ArgumentList.Add(stagedExe);
        startInfo.ArgumentList.Add(targetExe);

        if (!CanWriteDirectory(Path.GetDirectoryName(targetExe)!))
        {
            startInfo.Verb = "runas";
        }

        progress?.Report("Starting the companion updater. The app will restart automatically.");
        var process = Process.Start(startInfo);
        if (process is null)
        {
            throw new InvalidOperationException("Windows did not start the companion updater.");
        }
    }

    public static bool TryRunUpdaterMode(string[] args)
    {
        if (args.Length < 4 || !string.Equals(args[0], UpdateSwitch, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!int.TryParse(args[1], out var originalProcessId) || originalProcessId <= 0)
        {
            ShowUpdaterError("The companion updater received an invalid process ID.");
            return true;
        }

        var stagedExe = args[2];
        var targetExe = args[3];

        try
        {
            WaitForProcessExit(originalProcessId);
            ReplaceExecutableWithRetry(stagedExe, targetExe);

            Process.Start(new ProcessStartInfo
            {
                FileName = targetExe,
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(targetExe) ?? Environment.CurrentDirectory,
            });
        }
        catch (Exception ex)
        {
            ShowUpdaterError($"The companion update could not be applied.\n\n{ex.Message}");
        }

        return true;
    }

    public static void CleanupStaleUpdateDirectories()
    {
        var root = Path.Combine(Path.GetTempPath(), "AzerothQuestingCompanion", "updates");
        if (!Directory.Exists(root))
        {
            return;
        }

        foreach (var directory in Directory.EnumerateDirectories(root))
        {
            try
            {
                var created = Directory.GetCreationTimeUtc(directory);
                if (DateTime.UtcNow - created > TimeSpan.FromDays(1))
                {
                    Directory.Delete(directory, recursive: true);
                }
            }
            catch
            {
                // Stale update cleanup is best effort and must never block startup.
            }
        }
    }

    private static void WaitForProcessExit(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            if (!process.WaitForExit(60_000))
            {
                throw new TimeoutException("The old companion process did not exit within 60 seconds.");
            }
        }
        catch (ArgumentException)
        {
            // The process already exited before the updater attached to it.
        }
    }

    private static void ReplaceExecutableWithRetry(string stagedExe, string targetExe)
    {
        if (!File.Exists(stagedExe))
        {
            throw new FileNotFoundException("The staged companion executable is missing.", stagedExe);
        }

        Exception? lastError = null;
        for (var attempt = 0; attempt < 20; attempt++)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(targetExe)!);
                File.Copy(stagedExe, targetExe, overwrite: true);
                return;
            }
            catch (IOException ex)
            {
                lastError = ex;
            }
            catch (UnauthorizedAccessException ex)
            {
                lastError = ex;
            }

            Thread.Sleep(500);
        }

        throw new IOException("Windows kept the companion executable locked and the update could not be applied.", lastError);
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
            throw new InvalidDataException("The downloaded companion update failed its SHA-256 verification.");
        }
    }

    private static bool CanWriteDirectory(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            var probe = Path.Combine(directory, $".aqc-write-{Guid.NewGuid():N}.tmp");
            File.WriteAllText(probe, "test");
            File.Delete(probe);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void ShowUpdaterError(string message)
    {
        MessageBox.Show(
            message,
            "Azeroth Questing Companion Updater",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }
}
