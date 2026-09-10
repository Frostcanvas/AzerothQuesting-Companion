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

internal sealed class CompletedQuestDataEventArgs : EventArgs
{
    public CompletedQuestDataEventArgs(string sourcePath, string character, string realm, int questCount, bool changed)
    {
        SourcePath = sourcePath;
        Character = character;
        Realm = realm;
        QuestCount = questCount;
        Changed = changed;
    }

    public string SourcePath { get; }
    public string Character { get; }
    public string Realm { get; }
    public int QuestCount { get; }
    public bool Changed { get; }
}

internal sealed class SavedVariablesWatcher : IDisposable
{
    private readonly string _retailPath;
    private readonly SnapshotQueue _queue;
    private readonly CompletedQuestStore _completedQuestStore;
    private readonly CompletedQuestRepositoryQueue _completedQuestRepositoryQueue = new();
    private readonly List<FileSystemWatcher> _watchers = [];
    private readonly Dictionary<string, CancellationTokenSource> _debounce = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _sync = new();
    private bool _disposed;

    public SavedVariablesWatcher(string retailPath, SnapshotQueue queue, CompletedQuestStore completedQuestStore)
    {
        _retailPath = retailPath;
        _queue = queue;
        _completedQuestStore = completedQuestStore;
    }

    public event EventHandler<SnapshotQueuedEventArgs>? SnapshotProcessed;
    public event EventHandler<CompletedQuestDataEventArgs>? CompletedQuestDataProcessed;
    public event EventHandler<string>? WatcherError;

    public int DataFileCount => EnumerateResearchDataFiles().Count();

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
            var watcher = new FileSystemWatcher(accountDirectory)
            {
                Filter = "AzerothQuesting.lua",
                IncludeSubdirectories = true,
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
        foreach (var file in EnumerateResearchDataFiles())
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ProcessResearchFileAsync(file, cancellationToken);
        }

        foreach (var file in EnumerateCompletedQuestFiles())
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ProcessCompletedQuestFileAsync(file, cancellationToken);
        }
    }

    private IEnumerable<string> EnumerateResearchDataFiles()
    {
        var accountRoot = Path.Combine(_retailPath, "WTF", "Account");
        if (!Directory.Exists(accountRoot))
        {
            return [];
        }

        var files = new List<string>();
        foreach (var accountDirectory in Directory.EnumerateDirectories(accountRoot))
        {
            var path = Path.Combine(accountDirectory, "SavedVariables", "AzerothQuesting.lua");
            if (File.Exists(path))
            {
                files.Add(path);
            }
        }

        return files;
    }

    private IEnumerable<string> EnumerateCompletedQuestFiles()
    {
        var accountRoot = Path.Combine(_retailPath, "WTF", "Account");
        if (!Directory.Exists(accountRoot))
        {
            return [];
        }

        var files = new List<string>();
        foreach (var accountDirectory in Directory.EnumerateDirectories(accountRoot))
        {
            try
            {
                files.AddRange(Directory
                    .EnumerateFiles(accountDirectory, "AzerothQuesting.lua", SearchOption.AllDirectories)
                    .Where(path => CompletedQuestStore.TryGetCharacterIdentity(path, out _, out _)));
            }
            catch (UnauthorizedAccessException)
            {
                // Keep scanning the remaining accounts.
            }
            catch (IOException)
            {
                // A transient WoW write can be retried on the next scan/change event.
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

    private async Task ProcessPathAsync(string path, CancellationToken cancellationToken)
    {
        if (CompletedQuestStore.TryGetCharacterIdentity(path, out _, out _))
        {
            await ProcessCompletedQuestFileAsync(path, cancellationToken);
        }
        else
        {
            await ProcessResearchFileAsync(path, cancellationToken);
        }
    }

    private async Task ProcessResearchFileAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _queue.EnqueueAsync(path, cancellationToken);
            SnapshotProcessed?.Invoke(this, new SnapshotQueuedEventArgs(path, result.Queued, result.Hash));
        }
        catch (Exception ex)
        {
            WatcherError?.Invoke(this, ex.Message);
        }
    }

    private async Task ProcessCompletedQuestFileAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _completedQuestStore.ImportFileAsync(path, cancellationToken);
            if (!result.Found)
            {
                return;
            }

            CompletedQuestDataProcessed?.Invoke(
                this,
                new CompletedQuestDataEventArgs(path, result.Character, result.Realm, result.QuestCount, result.Changed));

            // The repository payload contains only the addon's identity-free AQC1
            // wire data. Character and realm stay in the local folder path and
            // are deliberately not copied into the upload queue.
            var repositoryResult = await _completedQuestRepositoryQueue.EnqueueAsync(path, cancellationToken);
            if (repositoryResult is not null)
            {
                SnapshotProcessed?.Invoke(
                    this,
                    new SnapshotQueuedEventArgs(path, repositoryResult.Queued, repositoryResult.Hash));
            }
        }
        catch (Exception ex)
        {
            WatcherError?.Invoke(this, ex.Message);
        }
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
                await ProcessPathAsync(path, cts.Token);
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
