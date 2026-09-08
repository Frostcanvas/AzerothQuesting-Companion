using System.Security.Cryptography;

namespace AzerothQuesting.Companion;

internal sealed record SnapshotResult(bool Queued, string SourcePath, string? SnapshotPath, string Hash);

internal sealed class SnapshotQueue
{
    public SnapshotQueue()
    {
        AppPaths.EnsureCreated();
    }

    public int Count => Directory.Exists(AppPaths.Outbox)
        ? Directory.EnumerateFiles(AppPaths.Outbox, "*.lua", SearchOption.TopDirectoryOnly).Count()
        : 0;

    public async Task<SnapshotResult> EnqueueAsync(string sourcePath, CancellationToken cancellationToken = default)
    {
        var bytes = await ReadStableFileAsync(sourcePath, cancellationToken);
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

        AppPaths.EnsureCreated();
        var existing = Directory
            .EnumerateFiles(AppPaths.Outbox, $"*-{hash}.lua", SearchOption.TopDirectoryOnly)
            .FirstOrDefault();

        if (existing is not null)
        {
            return new SnapshotResult(false, sourcePath, existing, hash);
        }

        var snapshotName = $"{DateTime.Now:yyyyMMdd-HHmmssfff}-{hash}.lua";
        var snapshotPath = Path.Combine(AppPaths.Outbox, snapshotName);
        await File.WriteAllBytesAsync(snapshotPath, bytes, cancellationToken);
        return new SnapshotResult(true, sourcePath, snapshotPath, hash);
    }

    private static async Task<byte[]> ReadStableFileAsync(string path, CancellationToken cancellationToken)
    {
        Exception? lastError = null;

        for (var attempt = 0; attempt < 10; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                return await File.ReadAllBytesAsync(path, cancellationToken);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                lastError = ex;
                await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
            }
        }

        throw new IOException($"Could not read SavedVariables file after waiting for WoW to finish writing it: {path}", lastError);
    }
}
