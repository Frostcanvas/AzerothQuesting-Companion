namespace AzerothQuesting.Companion;

internal sealed class SnapshotQueuedEventArgs : EventArgs
{
    public SnapshotQueuedEventArgs(string sourcePath, bool queued, string hash)
    {
        SourcePath = sourcePath;
        Queued = queued;
        Hash = hash;
    }

    public string SourcePath { get; }
    public bool Queued { get; }
    public string Hash { get; }
}

internal sealed class SavedVariablesWatcher : IDisposable
{
    private readonly string _retailPath;
    private readonly SnapshotQueue _queue;
    private readonly List<FileSystemWatcher> _watchers = [];
    private readonly Dictionary<string, CancellationTokenSource> _debounce = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _sync = new();
    private bool _disposed;

    public SavedVariablesWatcher(string retailPath, SnapshotQueue queue)
    {
        _retailPath = retailPath;
        _queue = queue;
    }

    public event EventHandler<SnapshotQueuedEventArgs>? SnapshotProcessed;
    public event EventHandler<string>? WatcherError;

    public int DataFileCount => EnumerateDataFiles().Count();

    public void Start()
    {
        ThrowIfDisposed();
        StopWatchers();

        var accountRoot = Path.Combine(_retailPath, "WTF", "Account");
        if (!Directory.Exists(accountRoot))
        {
            return;
        }

        foreach (var accountDirectory in Directory.EnumerateDirectories(accountRoot))
        {
            var savedVariables = Path.Combine(accountDirectory, "SavedVariables");
            if (!Directory.Exists(savedVariables))
            {
                continue;
            }

            var watcher = new FileSystemWatcher(savedVariables)
            {
                Filter = "AzerothQuesting.lua",
                IncludeSubdirectories = false,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true,
            };

            watcher.Changed += OnChanged;
            watcher.Created += OnChanged;
            watcher.Renamed += OnRenamed;
            watcher.Error += OnError;
            _watchers.Add(watcher);
        }
    }

    public async Task ScanExistingAsync(CancellationToken cancellationToken = default)
    {
        foreach (var file in EnumerateDataFiles())
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var result = await _queue.EnqueueAsync(file, cancellationToken);
                SnapshotProcessed?.Invoke(this, new SnapshotQueuedEventArgs(file, result.Queued, result.Hash));
            }
            catch (Exception ex)
            {
                WatcherError?.Invoke(this, ex.Message);
            }
        }
    }

    private IEnumerable<string> EnumerateDataFiles()
    {
        var accountRoot = Path.Combine(_retailPath, "WTF", "Account");
        if (!Directory.Exists(accountRoot))
        {
            return [];
        }

        var files = new List<string>();
        foreach (var accountDirectory in Directory.EnumerateDirectories(accountRoot))
        {
            var savedVariables = Path.Combine(accountDirectory, "SavedVariables");
            if (!Directory.Exists(savedVariables))
            {
                continue;
            }

            var path = Path.Combine(savedVariables, "AzerothQuesting.lua");
            if (File.Exists(path))
            {
                files.Add(path);
            }
        }

        return files;
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        if (IsTrackedFile(e.FullPath))
        {
            Debounce(e.FullPath);
        }
    }

    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        if (IsTrackedFile(e.FullPath))
        {
            Debounce(e.FullPath);
        }
    }

    private void OnError(object sender, ErrorEventArgs e)
    {
        WatcherError?.Invoke(this, e.GetException().Message);
    }

    private static bool IsTrackedFile(string path)
    {
        return string.Equals(
            Path.GetFileName(path),
            "AzerothQuesting.lua",
            StringComparison.OrdinalIgnoreCase);
    }

    private void Debounce(string path)
    {
        CancellationTokenSource cts;
        lock (_sync)
        {
            if (_debounce.Remove(path, out var previous))
            {
                previous.Cancel();
                previous.Dispose();
            }

            cts = new CancellationTokenSource();
            _debounce[path] = cts;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(2), cts.Token);
                var result = await _queue.EnqueueAsync(path, cts.Token);
                SnapshotProcessed?.Invoke(this, new SnapshotQueuedEventArgs(path, result.Queued, result.Hash));
            }
            catch (OperationCanceledException)
            {
                // A newer file event replaced this one.
            }
            catch (Exception ex)
            {
                WatcherError?.Invoke(this, ex.Message);
            }
            finally
            {
                lock (_sync)
                {
                    if (_debounce.TryGetValue(path, out var current) && ReferenceEquals(current, cts))
                    {
                        _debounce.Remove(path);
                    }
                }

                cts.Dispose();
            }
        });
    }

    private void StopWatchers()
    {
        foreach (var watcher in _watchers)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }
        _watchers.Clear();
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        StopWatchers();

        lock (_sync)
        {
            foreach (var cts in _debounce.Values)
            {
                cts.Cancel();
                cts.Dispose();
            }
            _debounce.Clear();
        }
    }
}
