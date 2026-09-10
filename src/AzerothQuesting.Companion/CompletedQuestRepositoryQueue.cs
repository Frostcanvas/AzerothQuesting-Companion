using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace AzerothQuesting.Companion;

/// <summary>
/// Builds a privacy-safe, content-addressed repository contribution from the
/// per-character AQC1 handoff. Character and realm live only in the local WoW
/// folder path and are never copied into the queued payload.
/// </summary>
internal sealed class CompletedQuestRepositoryQueue
{
    private static readonly Regex WireRegex = new(
        @"AQC1\|1\|(?:Alliance|Horde|Neutral|Unknown)\|[A-Z]+\|\d{1,3}\|\d+\|[A-Za-z0-9._-]+\|[0-9A-F~,]*",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly object _sync = new();
    private readonly HashSet<string> _queuedHashes = new(StringComparer.OrdinalIgnoreCase);

    public CompletedQuestRepositoryQueue()
    {
        AppPaths.EnsureCreated();
        LoadHistory();
    }

    public async Task<SnapshotResult?> EnqueueAsync(
        string sourcePath,
        CancellationToken cancellationToken = default)
    {
        var text = await ReadStableTextAsync(sourcePath, cancellationToken);
        var wire = FindNewestWire(text);
        if (wire is null)
        {
            return null;
        }

        var payload = BuildRepositoryPayload(wire);
        var bytes = Encoding.UTF8.GetBytes(payload);
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

        lock (_sync)
        {
            if (_queuedHashes.Contains(hash))
            {
                return new SnapshotResult(false, sourcePath, null, hash);
            }
        }

        AppPaths.EnsureCreated();
        var snapshotName = $"{DateTime.Now:yyyyMMdd-HHmmssfff}-{hash}.lua";
        var snapshotPath = Path.Combine(AppPaths.Outbox, snapshotName);
        await File.WriteAllBytesAsync(snapshotPath, bytes, cancellationToken);

        lock (_sync)
        {
            if (_queuedHashes.Add(hash))
            {
                File.AppendAllText(
                    AppPaths.CompletedQuestRepositoryHistory,
                    hash + Environment.NewLine,
                    Encoding.UTF8);
            }
        }

        return new SnapshotResult(true, sourcePath, snapshotPath, hash);
    }

    private static string? FindNewestWire(string text)
    {
        string? newest = null;
        long newestCapturedAt = long.MinValue;

        foreach (Match match in WireRegex.Matches(text))
        {
            var parts = match.Value.Split('|', 8, StringSplitOptions.None);
            if (parts.Length != 8 || !long.TryParse(parts[5], out var capturedAt))
            {
                continue;
            }

            if (newest is null || capturedAt > newestCapturedAt)
            {
                newest = match.Value;
                newestCapturedAt = capturedAt;
            }
        }

        return newest;
    }

    private static string BuildRepositoryPayload(string wire)
    {
        var parts = wire.Split('|', 8, StringSplitOptions.None);

        // A capture timestamp changes every login/reload even when quest data is
        // identical. Normalize it before hashing/queueing so an unchanged toon
        // does not create a new upload merely because WoW saved again.
        if (parts.Length == 8)
        {
            parts[5] = "0";
            wire = string.Join('|', parts);
        }

        return "AzerothQuestingCompletedQuestRepository = \"" + wire + "\"\n";
    }

    private void LoadHistory()
    {
        if (!File.Exists(AppPaths.CompletedQuestRepositoryHistory))
        {
            return;
        }

        try
        {
            foreach (var line in File.ReadLines(AppPaths.CompletedQuestRepositoryHistory))
            {
                var hash = line.Trim();
                if (hash.Length == 64 && hash.All(Uri.IsHexDigit))
                {
                    _queuedHashes.Add(hash);
                }
            }
        }
        catch (IOException)
        {
            // A missing history only causes harmless server-side duplicate
            // checking; it must never stop the Companion from starting.
        }
    }

    private static async Task<string> ReadStableTextAsync(
        string path,
        CancellationToken cancellationToken)
    {
        Exception? lastError = null;
        for (var attempt = 0; attempt < 10; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return await File.ReadAllTextAsync(path, cancellationToken);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                lastError = ex;
                await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
            }
        }

        throw new IOException(
            $"Could not read completed-quest SavedVariables after waiting for WoW to finish writing it: {path}",
            lastError);
    }
}
