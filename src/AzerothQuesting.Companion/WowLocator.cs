using Microsoft.Win32;

namespace AzerothQuesting.Companion;

internal static class WowLocator
{
    public static string? FindRetailPath()
    {
        var candidates = new List<string>();
        candidates.AddRange(GetRegistryCandidates());

        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

        AddCandidate(candidates, Path.Combine(programFilesX86, "World of Warcraft", "_retail_"));
        AddCandidate(candidates, Path.Combine(programFiles, "World of Warcraft", "_retail_"));

        foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady))
        {
            AddCandidate(candidates, Path.Combine(drive.RootDirectory.FullName, "World of Warcraft", "_retail_"));
            AddCandidate(candidates, Path.Combine(drive.RootDirectory.FullName, "Games", "World of Warcraft", "_retail_"));
            AddCandidate(candidates, Path.Combine(drive.RootDirectory.FullName, "Program Files (x86)", "World of Warcraft", "_retail_"));
            AddCandidate(candidates, Path.Combine(drive.RootDirectory.FullName, "Program Files", "World of Warcraft", "_retail_"));
        }

        return candidates
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(IsRetailPath);
    }

    public static string? FindForeverPath(string? retailPath)
    {
        var retailParent = retailPath is null ? null : Directory.GetParent(retailPath);
        var candidates = new[]
        {
            retailParent is null ? null : Path.Combine(retailParent.FullName, "_forever_"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "World of Warcraft", "_forever_"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "World of Warcraft", "_forever_"),
        };
        return candidates.FirstOrDefault(IsForeverPath);
    }

    public static bool IsClientPath(string? path, WowGameClient client)
    {
        return !string.IsNullOrWhiteSpace(path)
            && string.Equals(Path.GetFileName(Path.TrimEndingDirectorySeparator(path)), client.Folder, StringComparison.OrdinalIgnoreCase)
            && Directory.Exists(path)
            && (Directory.EnumerateFiles(path, "Wow*.exe").Any()
                || Directory.Exists(Path.Combine(path, "WTF", "Account")));
    }

    public static bool IsRetailPath(string? path) => IsClientPath(path, WowGameClient.All[0]);

    public static bool IsForeverPath(string? path) => IsClientPath(path, WowGameClient.All[1]);

    public static bool IsSupportedPath(string? path) => WowGameClient.All.Any(client => IsClientPath(path, client));

    public static WowGameClient? GetClient(string? path) =>
        WowGameClient.All.FirstOrDefault(client => IsClientPath(path, client));

    public static string? FindClientPath(WowGameClient client, IEnumerable<string> knownPaths)
    {
        var candidates = knownPaths
            .Select(path => Directory.GetParent(Path.TrimEndingDirectorySeparator(path))?.FullName)
            .Where(parent => parent is not null)
            .Select(parent => Path.Combine(parent!, client.Folder))
            .Concat(GetRegistryCandidates().Select(path => Path.Combine(Path.GetDirectoryName(path)!, client.Folder)))
            .Concat(new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "World of Warcraft", client.Folder),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "World of Warcraft", client.Folder),
            });
        return candidates.FirstOrDefault(path => IsClientPath(path, client));
    }

    private static IEnumerable<string> GetRegistryCandidates()
    {
        var results = new List<string>();
        var locations = new[]
        {
            (RegistryHive.LocalMachine, RegistryView.Registry64, @"SOFTWARE\Blizzard Entertainment\World of Warcraft"),
            (RegistryHive.LocalMachine, RegistryView.Registry32, @"SOFTWARE\Blizzard Entertainment\World of Warcraft"),
            (RegistryHive.CurrentUser, RegistryView.Registry64, @"SOFTWARE\Blizzard Entertainment\World of Warcraft"),
            (RegistryHive.CurrentUser, RegistryView.Registry32, @"SOFTWARE\Blizzard Entertainment\World of Warcraft"),
        };

        foreach (var (hive, view, keyPath) in locations)
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                using var key = baseKey.OpenSubKey(keyPath);
                var installPath = key?.GetValue("InstallPath") as string;
                if (string.IsNullOrWhiteSpace(installPath))
                {
                    continue;
                }

                AddCandidate(results, installPath);
                AddCandidate(results, Path.Combine(installPath, "_retail_"));
                var parent = Directory.GetParent(installPath)?.FullName;
                if (!string.IsNullOrWhiteSpace(parent))
                {
                    AddCandidate(results, Path.Combine(parent, "_retail_"));
                }
            }
            catch
            {
                // Registry discovery is best-effort. Common path scanning still runs.
            }
        }

        return results;
    }

    private static void AddCandidate(ICollection<string> candidates, string? path)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            candidates.Add(Path.GetFullPath(path));
        }
    }
}
