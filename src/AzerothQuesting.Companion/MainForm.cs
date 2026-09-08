using System.Diagnostics;
using System.Reflection;

namespace AzerothQuesting.Companion;

internal sealed class MainForm : Form
{
    private readonly GitHubAddonClient _github = new();
    private readonly SnapshotQueue _snapshotQueue = new();
    private readonly AddonService _addonService;
    private readonly CompanionSettings _settings;

    private readonly Label _clientVersionValue = CreateValueLabel();
    private readonly Label _wowPathValue = CreateValueLabel();
    private readonly Label _addonValue = CreateValueLabel();
    private readonly Label _dataValue = CreateValueLabel();
    private readonly Label _queueValue = CreateValueLabel();
    private readonly Label _lastDataValue = CreateValueLabel();
    private readonly Label _uploadValue = CreateValueLabel();
    private readonly ToolStripStatusLabel _statusText = new("Starting...");
    private readonly Button _installButton = new() { AutoSize = true, Text = "Install / Update Addon" };

    private SavedVariablesWatcher? _watcher;
    private NotifyIcon? _notifyIcon;
    private string? _retailPath;
    private bool _exitRequested;
    private bool _busy;

    public MainForm()
    {
        AppPaths.EnsureCreated();
        _settings = SettingsService.Load();
        _addonService = new AddonService(_github);

        Text = "Azeroth Questing Companion";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(760, 430);
        Size = new Size(900, 520);
        AutoScaleMode = AutoScaleMode.Dpi;

        BuildUi();
        BuildTrayIcon();

        Shown += async (_, _) => await InitializeAsync();
        Resize += (_, _) =>
        {
            if (WindowState == FormWindowState.Minimized)
            {
                HideToTray();
            }
        };
        FormClosing += OnFormClosing;
        FormClosed += (_, _) =>
        {
            _watcher?.Dispose();
            _notifyIcon?.Dispose();
            _github.Dispose();
        };
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18),
            ColumnCount = 1,
            RowCount = 5,
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var title = new Label
        {
            AutoSize = true,
            Font = new Font(Font.FontFamily, 18, FontStyle.Bold),
            Text = "Azeroth Questing Companion",
            Margin = new Padding(0, 0, 0, 6),
        };
        root.Controls.Add(title, 0, 0);

        var subtitle = new Label
        {
            AutoSize = true,
            Text = "Addon installer/updater and local quest-observation collector",
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(0, 0, 0, 14),
        };
        root.Controls.Add(subtitle, 0, 1);

        var statusTable = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 7,
            Padding = new Padding(0, 4, 0, 4),
        };
        statusTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 175));
        statusTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        AddStatusRow(statusTable, 0, "Companion version", _clientVersionValue);
        AddStatusRow(statusTable, 1, "WoW Retail", _wowPathValue);
        AddStatusRow(statusTable, 2, "Azeroth Questing addon", _addonValue);
        AddStatusRow(statusTable, 3, "SavedVariables files", _dataValue);
        AddStatusRow(statusTable, 4, "Queued snapshots", _queueValue);
        AddStatusRow(statusTable, 5, "Last data activity", _lastDataValue);
        AddStatusRow(statusTable, 6, "Service01 upload", _uploadValue);

        root.Controls.Add(statusTable, 0, 2);

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Margin = new Padding(0, 12, 0, 8),
        };

        var detectButton = CreateButton("Detect WoW", async (_, _) => await DetectWowAsync());
        var browseButton = CreateButton("Browse...", async (_, _) => await BrowseForWowAsync());
        _installButton.Click += async (_, _) => await InstallAddonAsync();
        var scanButton = CreateButton("Scan / Queue Now", async (_, _) => await ScanNowAsync());
        var addonsButton = CreateButton("Open AddOns Folder", (_, _) => OpenAddOnsFolder());
        var dataButton = CreateButton("Open Companion Data", (_, _) => OpenPath(AppPaths.Root));

        buttons.Controls.Add(detectButton);
        buttons.Controls.Add(browseButton);
        buttons.Controls.Add(_installButton);
        buttons.Controls.Add(scanButton);
        buttons.Controls.Add(addonsButton);
        buttons.Controls.Add(dataButton);
        root.Controls.Add(buttons, 0, 3);

        var privacy = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(820, 0),
            Text = "Privacy: this client watches Azeroth Questing SavedVariables on disk. It does not read WoW process memory. Service01 uploading is not enabled in v0.1.0; snapshots remain local in the Outbox.",
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(0, 6, 0, 8),
        };
        root.Controls.Add(privacy, 0, 4);

        var strip = new StatusStrip();
        strip.Items.Add(_statusText);

        Controls.Add(root);
        Controls.Add(strip);

        _clientVersionValue.Text = GetClientVersion();
        _wowPathValue.Text = "Detecting...";
        _addonValue.Text = "Waiting for WoW path";
        _dataValue.Text = "0";
        _queueValue.Text = _snapshotQueue.Count.ToString();
        _lastDataValue.Text = "None this session";
        _uploadValue.Text = "Local queue only - API not connected yet";
    }

    private void BuildTrayIcon()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Show Azeroth Questing Companion", null, (_, _) => ShowFromTray());
        menu.Items.Add("Install / Update Addon", null, async (_, _) =>
        {
            ShowFromTray();
            await InstallAddonAsync();
        });
        menu.Items.Add("Open Companion Data", null, (_, _) => OpenPath(AppPaths.Root));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitApplication());

        _notifyIcon = new NotifyIcon
        {
            Text = "Azeroth Questing Companion",
            Icon = SystemIcons.Application,
            ContextMenuStrip = menu,
            Visible = true,
        };
        _notifyIcon.DoubleClick += (_, _) => ShowFromTray();
    }

    private async Task InitializeAsync()
    {
        try
        {
            var saved = _settings.WowRetailPath;
            if (WowLocator.IsRetailPath(saved))
            {
                await ConfigureRetailPathAsync(saved!);
                return;
            }

            await DetectWowAsync();
        }
        catch (Exception ex)
        {
            SetStatus($"Startup error: {ex.Message}");
        }
    }

    private async Task DetectWowAsync()
    {
        if (!TryBeginBusy("Detecting World of Warcraft Retail..."))
        {
            return;
        }

        try
        {
            var path = await Task.Run(WowLocator.FindRetailPath);
            if (path is null)
            {
                _wowPathValue.Text = "Not found - use Browse";
                _addonValue.Text = "Waiting for WoW path";
                SetStatus("WoW Retail was not found automatically. Use Browse to select the _retail_ folder.");
                return;
            }

            await ConfigureRetailPathAsync(path);
            SetStatus("WoW Retail detected. Companion watcher is running.");
        }
        finally
        {
            EndBusy();
        }
    }

    private async Task BrowseForWowAsync()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select World of Warcraft's _retail_ folder (or the World of Warcraft folder containing it).",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false,
        };

        if (!string.IsNullOrWhiteSpace(_retailPath) && Directory.Exists(_retailPath))
        {
            dialog.InitialDirectory = _retailPath;
        }

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var candidate = NormalizeRetailSelection(dialog.SelectedPath);
        if (!WowLocator.IsRetailPath(candidate))
        {
            MessageBox.Show(this, "That folder does not look like a World of Warcraft Retail installation. Select the _retail_ folder that contains Wow.exe.", "Azeroth Questing Companion", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        await ConfigureRetailPathAsync(candidate!);
        SetStatus("WoW Retail path saved. Companion watcher is running.");
    }

    private async Task ConfigureRetailPathAsync(string path)
    {
        _watcher?.Dispose();
        _watcher = null;

        _retailPath = Path.GetFullPath(path);
        _settings.WowRetailPath = _retailPath;
        SettingsService.Save(_settings);
        _wowPathValue.Text = _retailPath;

        _watcher = new SavedVariablesWatcher(_retailPath, _snapshotQueue);
        _watcher.SnapshotProcessed += WatcherOnSnapshotProcessed;
        _watcher.WatcherError += WatcherOnError;
        _watcher.Start();

        await _watcher.ScanExistingAsync();
        RefreshLocalStatus();
        await RefreshRemoteAddonStatusAsync();
    }

    private async Task RefreshRemoteAddonStatusAsync()
    {
        if (_retailPath is null)
        {
            return;
        }

        var installed = AddonService.GetInstalledVersion(_retailPath);
        _addonValue.Text = installed is null ? "Not installed" : $"Installed {installed}";

        try
        {
            var remote = await _github.GetLatestAddonAsync();
            if (installed is null)
            {
                _addonValue.Text = $"Not installed - latest {remote.Version}";
                _installButton.Text = "Install Addon";
            }
            else if (VersionsMatch(installed, remote.Version))
            {
                _addonValue.Text = $"Installed {installed} - latest";
                _installButton.Text = "Reinstall / Update Addon";
            }
            else
            {
                _addonValue.Text = $"Installed {installed} - update {remote.Version} available";
                _installButton.Text = "Update Addon";
            }
        }
        catch (Exception ex)
        {
            _addonValue.Text = installed is null
                ? $"Not installed - update check failed ({ex.Message})"
                : $"Installed {installed} - update check failed";
        }
    }

    private async Task InstallAddonAsync()
    {
        if (_retailPath is null)
        {
            MessageBox.Show(this, "Detect or browse to your WoW Retail installation first.", "Azeroth Questing Companion", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (!TryBeginBusy("Preparing addon install/update..."))
        {
            return;
        }

        _installButton.Enabled = false;
        try
        {
            var progress = new Progress<string>(SetStatus);
            var result = await _addonService.InstallOrUpdateAsync(_retailPath, progress);

            // The installer may have migrated SavedVariables, so rebuild watchers.
            await ConfigureRetailPathAsync(_retailPath);
            RefreshLocalStatus();

            var migrationText = result.MigratedSavedVariableFiles > 0
                ? $"\nMigrated {result.MigratedSavedVariableFiles} legacy SavedVariables file(s)."
                : string.Empty;

            MessageBox.Show(
                this,
                $"Azeroth Questing {result.Version} is installed.\nBackup: {result.BackupDirectory}{migrationText}",
                "Azeroth Questing Companion",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            SetStatus($"Azeroth Questing {result.Version} installed successfully.");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Azeroth Questing Companion", MessageBoxButtons.OK, MessageBoxIcon.Error);
            SetStatus($"Install/update failed: {ex.Message}");
        }
        finally
        {
            _installButton.Enabled = true;
            EndBusy();
        }
    }

    private async Task ScanNowAsync()
    {
        if (_watcher is null)
        {
            SetStatus("Detect WoW before scanning SavedVariables.");
            return;
        }

        if (!TryBeginBusy("Scanning Azeroth Questing SavedVariables..."))
        {
            return;
        }

        try
        {
            await _watcher.ScanExistingAsync();
            RefreshLocalStatus();
            SetStatus("SavedVariables scan complete. New data was queued only if its content changed.");
        }
        finally
        {
            EndBusy();
        }
    }

    private void WatcherOnSnapshotProcessed(object? sender, SnapshotQueuedEventArgs e)
    {
        SafeUi(() =>
        {
            _lastDataValue.Text = e.Queued
                ? $"{DateTime.Now:g} - new snapshot queued"
                : $"{DateTime.Now:g} - unchanged data seen";
            RefreshLocalStatus();
            if (e.Queued)
            {
                SetStatus("Azeroth Questing data changed; a deduplicated snapshot was queued locally.");
            }
        });
    }

    private void WatcherOnError(object? sender, string message)
    {
        SafeUi(() => SetStatus($"SavedVariables watcher: {message}"));
    }

    private void RefreshLocalStatus()
    {
        _queueValue.Text = _snapshotQueue.Count.ToString();
        _dataValue.Text = (_watcher?.DataFileCount ?? 0).ToString();

        if (_retailPath is not null)
        {
            var installed = AddonService.GetInstalledVersion(_retailPath);
            if (installed is not null && !_addonValue.Text.Contains("update", StringComparison.OrdinalIgnoreCase))
            {
                _addonValue.Text = $"Installed {installed}";
            }
        }
    }

    private void OpenAddOnsFolder()
    {
        if (_retailPath is null)
        {
            SetStatus("Detect WoW before opening the AddOns folder.");
            return;
        }

        var path = AddonService.GetAddOnsPath(_retailPath);
        Directory.CreateDirectory(path);
        OpenPath(path);
    }

    private static void OpenPath(string path)
    {
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true,
        });
    }

    private static string? NormalizeRetailSelection(string selectedPath)
    {
        if (WowLocator.IsRetailPath(selectedPath))
        {
            return selectedPath;
        }

        var retail = Path.Combine(selectedPath, "_retail_");
        return WowLocator.IsRetailPath(retail) ? retail : selectedPath;
    }

    private bool TryBeginBusy(string message)
    {
        if (_busy)
        {
            SetStatus("The companion is already working on another task.");
            return false;
        }

        _busy = true;
        UseWaitCursor = true;
        SetStatus(message);
        return true;
    }

    private void EndBusy()
    {
        _busy = false;
        UseWaitCursor = false;
    }

    private void SetStatus(string message)
    {
        _statusText.Text = message;
    }

    private void HideToTray()
    {
        Hide();
        ShowInTaskbar = false;
        if (_notifyIcon is not null)
        {
            _notifyIcon.ShowBalloonTip(1500, "Azeroth Questing Companion", "Still running in the notification area and watching addon data.", ToolTipIcon.Info);
        }
    }

    private void ShowFromTray()
    {
        Show();
        ShowInTaskbar = true;
        WindowState = FormWindowState.Normal;
        Activate();
    }

    private void ExitApplication()
    {
        _exitRequested = true;
        if (_notifyIcon is not null)
        {
            _notifyIcon.Visible = false;
        }
        Close();
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (!_exitRequested && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            HideToTray();
        }
    }

    private void SafeUi(Action action)
    {
        if (IsDisposed || Disposing)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(action);
        }
        else
        {
            action();
        }
    }

    private static bool VersionsMatch(string left, string right)
    {
        static string Normalize(string value) => value.Trim().TrimStart('v', 'V');
        var a = Normalize(left);
        var b = Normalize(right);
        return Version.TryParse(a, out var va) && Version.TryParse(b, out var vb)
            ? va == vb
            : string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetClientVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        return version is null ? "0.1.0" : $"{version.Major}.{version.Minor}.{version.Build}";
    }

    private static Button CreateButton(string text, EventHandler handler)
    {
        var button = new Button
        {
            AutoSize = true,
            Text = text,
            Margin = new Padding(0, 0, 8, 8),
        };
        button.Click += handler;
        return button;
    }

    private static Label CreateValueLabel() => new()
    {
        AutoSize = true,
        MaximumSize = new Size(650, 0),
        Padding = new Padding(0, 4, 0, 4),
    };

    private static void AddStatusRow(TableLayoutPanel table, int row, string labelText, Control value)
    {
        var label = new Label
        {
            AutoSize = true,
            Font = new Font(SystemFonts.MessageBoxFont, FontStyle.Bold),
            Text = labelText,
            Padding = new Padding(0, 4, 0, 4),
        };

        table.Controls.Add(label, 0, row);
        table.Controls.Add(value, 1, row);
    }
}
