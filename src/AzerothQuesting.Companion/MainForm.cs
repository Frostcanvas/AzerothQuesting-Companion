using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

namespace AzerothQuesting.Companion;

internal sealed class MainForm : Form
{
    private static readonly Color Background = Color.FromArgb(13, 16, 24);
    private static readonly Color Sidebar = Color.FromArgb(20, 23, 34);
    private static readonly Color Surface = Color.FromArgb(24, 28, 40);
    private static readonly Color SurfaceAlt = Color.FromArgb(30, 34, 49);
    private static readonly Color Border = Color.FromArgb(50, 56, 78);
    private static readonly Color TextPrimary = Color.FromArgb(238, 240, 248);
    private static readonly Color TextSecondary = Color.FromArgb(163, 170, 194);
    private static readonly Color Gold = Color.FromArgb(231, 181, 67);
    private static readonly Color Purple = Color.FromArgb(130, 95, 225);
    private static readonly Color Green = Color.FromArgb(78, 214, 142);

    private readonly GitHubAddonClient _github = new();
    private readonly SnapshotQueue _snapshotQueue = new();
    private readonly AddonService _addonService;
    private readonly CompanionUpdateService _updateService;
    private readonly CompanionSettings _settings;
    private readonly Service01Client _serviceClient;
    private readonly SemaphoreSlim _syncGate = new(1, 1);

    private readonly Label _clientVersionValue = CreateValueLabel("Starting...");
    private readonly Label _clientUpdateValue = CreateValueLabel("Not checked");
    private readonly Label _wowPathValue = CreateValueLabel("Detecting...");
    private readonly Label _installedAddonValue = CreateValueLabel("Waiting for WoW path");
    private readonly Label _latestAddonValue = CreateValueLabel("Not checked");
    private readonly Label _queueValue = CreateValueLabel("0");
    private readonly Label _dataValue = CreateValueLabel("0");
    private readonly Label _lastDataValue = CreateValueLabel("None this session");
    private readonly Label _uploadValue = CreateValueLabel("Local queue only");
    private readonly Label _heroStatusValue = CreateValueLabel("Detecting addon...");
    private readonly ToolStripStatusLabel _statusText = new("Starting...");
    private readonly ListBox _activityList = new();

    private readonly Button _checkUpdatesButton = CreateActionButton("Check for Updates", Gold);
    private readonly Button _installButton = CreateActionButton("Install / Update Addon", Gold);
    private readonly Button _scanButton = CreateActionButton("Scan for Observations", Purple);
    private readonly Button _syncButton = CreateActionButton("Sync Now", Purple);
    private readonly Button _channelButton = CreateActionButton("Channel: STABLE", Purple);

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
        _updateService = new CompanionUpdateService(_github);
        _serviceClient = new Service01Client(_settings);

        Text = "Azeroth Questing Companion";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1040, 650);
        Size = new Size(1280, 760);
        BackColor = Background;
        ForeColor = TextPrimary;
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
            _serviceClient.Dispose();
            _syncGate.Dispose();
            _github.Dispose();
        };
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        TryEnableDarkTitleBar();
    }

    private void BuildUi()
    {
        var shell = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Background,
            ColumnCount = 2,
            RowCount = 1,
            Padding = Padding.Empty,
            Margin = Padding.Empty,
        };
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        shell.Controls.Add(BuildSidebar(), 0, 0);
        shell.Controls.Add(BuildMainArea(), 1, 0);
        Controls.Add(shell);

        _clientVersionValue.Text = GetClientVersion();
        _uploadValue.Text = _settings.ServiceSyncEnabled
            ? "Waiting for Azeroth Questing Server"
            : "Sync disabled";
        UpdateChannelButtonText();
    }

    private Control BuildSidebar()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Sidebar,
            Padding = new Padding(12),
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 9,
            BackColor = Sidebar,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 86));
        for (var i = 1; i <= 6; i++)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        }
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));

        var brand = new Label
        {
            Dock = DockStyle.Fill,
            Text = "AQ\nAzeroth Questing",
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            ForeColor = Gold,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 4, 0, 4),
        };
        layout.Controls.Add(brand, 0, 0);

        layout.Controls.Add(CreateNavButton("Home", (_, _) => SetStatus("Home dashboard ready."), active: true), 0, 1);
        layout.Controls.Add(CreateNavButton("Addon", (_, _) => _installButton.Focus()), 0, 2);
        layout.Controls.Add(CreateNavButton("Sync", async (_, _) => await SyncNowAsync(scanFirst: true)), 0, 3);
        layout.Controls.Add(CreateNavButton("Data", (_, _) => OpenResearchData()), 0, 4);
        layout.Controls.Add(CreateNavButton("Settings", async (_, _) => await BrowseForWowAsync()), 0, 5);
        layout.Controls.Add(CreateNavButton("Logs", (_, _) => OpenPath(AppPaths.Root)), 0, 6);

        var footer = new Label
        {
            Dock = DockStyle.Fill,
            Text = "FOR A GREATER AZEROTH",
            Font = new Font("Segoe UI", 8, FontStyle.Bold),
            ForeColor = TextSecondary,
            TextAlign = ContentAlignment.MiddleCenter,
        };
        layout.Controls.Add(footer, 0, 8);
        panel.Controls.Add(layout);
        return panel;
    }

    private Control BuildMainArea()
    {
        var main = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Background,
        };
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        main.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));

        main.Controls.Add(BuildTopBar(), 0, 0);
        main.Controls.Add(BuildDashboardBody(), 0, 1);

        var strip = new StatusStrip
        {
            Dock = DockStyle.Fill,
            BackColor = Sidebar,
            ForeColor = Green,
            SizingGrip = false,
            RenderMode = ToolStripRenderMode.System,
        };
        strip.Items.Add(_statusText);
        main.Controls.Add(strip, 0, 2);
        return main;
    }

    private Control BuildTopBar()
    {
        var bar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Sidebar,
            Padding = new Padding(14, 10, 10, 8),
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
        };

        _checkUpdatesButton.Click += async (_, _) => await CheckForUpdatesAsync();
        _installButton.Click += async (_, _) => await InstallAddonAsync();
        _scanButton.Click += async (_, _) => await ScanNowAsync();
        _syncButton.Click += async (_, _) => await SyncNowAsync(scanFirst: true);
        _channelButton.Click += async (_, _) => await ToggleUpdateChannelAsync();
        var repair = CreateActionButton("Repair Addon", Purple);
        repair.Click += async (_, _) => await InstallAddonAsync();

        bar.Controls.Add(_checkUpdatesButton);
        bar.Controls.Add(_installButton);
        bar.Controls.Add(_scanButton);
        bar.Controls.Add(_syncButton);
        bar.Controls.Add(_channelButton);
        bar.Controls.Add(repair);
        return bar;
    }

    private Control BuildDashboardBody()
    {
        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Background,
            Padding = new Padding(16),
            ColumnCount = 2,
            RowCount = 1,
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 74));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 26));

        var center = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Background,
            ColumnCount = 1,
            RowCount = 3,
            Margin = new Padding(0, 0, 10, 0),
        };
        center.RowStyles.Add(new RowStyle(SizeType.Absolute, 155));
        center.RowStyles.Add(new RowStyle(SizeType.Absolute, 265));
        center.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        center.Controls.Add(BuildHeroPanel(), 0, 0);
        center.Controls.Add(BuildCardsPanel(), 0, 1);
        center.Controls.Add(BuildInfoPanel(), 0, 2);

        body.Controls.Add(center, 0, 0);
        body.Controls.Add(BuildActivityPanel(), 1, 0);
        return body;
    }

    private Control BuildHeroPanel()
    {
        var hero = CreateSurfacePanel();
        hero.Margin = new Padding(0, 0, 0, 12);

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(22, 16, 18, 16),
            BackColor = Surface,
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 68));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32));

        var text = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Surface,
        };
        text.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "Azeroth Questing",
            Font = new Font("Segoe UI", 22, FontStyle.Bold),
            ForeColor = TextPrimary,
            Margin = new Padding(0, 0, 0, 4),
        });
        text.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "Your journey. Automatically recorded.",
            Font = new Font("Segoe UI", 11, FontStyle.Regular),
            ForeColor = Gold,
            Margin = new Padding(0, 0, 0, 8),
        });
        text.Controls.Add(new Label
        {
            AutoSize = true,
            MaximumSize = new Size(620, 0),
            Text = "Install and update the addon, watch its SavedVariables, and queue quest research data without reading World of Warcraft process memory.",
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
            ForeColor = TextSecondary,
        });

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Surface,
            Padding = new Padding(8, 6, 0, 0),
        };
        _heroStatusValue.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        _heroStatusValue.ForeColor = Green;
        _heroStatusValue.MaximumSize = new Size(260, 44);
        var heroInstall = CreateActionButton("Install / Update Addon", Gold);
        heroInstall.AutoSize = false;
        heroInstall.Size = new Size(210, 40);
        heroInstall.Click += async (_, _) => await InstallAddonAsync();
        actions.Controls.Add(_heroStatusValue);
        actions.Controls.Add(heroInstall);

        table.Controls.Add(text, 0, 0);
        table.Controls.Add(actions, 1, 0);
        hero.Controls.Add(table);
        return hero;
    }

    private Control BuildCardsPanel()
    {
        var cards = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 2,
            BackColor = Background,
            Margin = Padding.Empty,
        };
        for (var i = 0; i < 4; i++)
        {
            cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        }
        cards.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        cards.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

        cards.Controls.Add(CreateCard("Companion Version", _clientVersionValue, "Installed client"), 0, 0);
        cards.Controls.Add(CreateCard("Client Update", _clientUpdateValue, "GitHub Releases"), 1, 0);
        cards.Controls.Add(CreateCard("Installed Addon", _installedAddonValue, "AzerothQuesting"), 2, 0);
        cards.Controls.Add(CreateCard("Latest Addon", _latestAddonValue, "GitHub"), 3, 0);
        cards.Controls.Add(CreateCard("Pending Observations", _queueValue, "Local upload queue"), 0, 1);
        cards.Controls.Add(CreateCard("SavedVariables Files", _dataValue, "Currently watched"), 1, 1);
        cards.Controls.Add(CreateCard("Last Data Activity", _lastDataValue, "Disk watcher"), 2, 1);
        cards.Controls.Add(CreateCard("Sync Status", _uploadValue, "Azeroth Questing Server"), 3, 1);
        return cards;
    }

    private Control BuildInfoPanel()
    {
        var panel = CreateSurfacePanel();
        panel.Margin = new Padding(0, 12, 0, 0);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(18, 12, 18, 12),
            BackColor = Surface,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        layout.Controls.Add(CreateSectionTitle("World of Warcraft Retail"), 0, 0);
        _wowPathValue.MaximumSize = new Size(850, 42);
        layout.Controls.Add(_wowPathValue, 0, 1);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            BackColor = Surface,
            Margin = new Padding(0, 8, 0, 8),
        };
        var detect = CreateSecondaryButton("Detect WoW");
        detect.Click += async (_, _) => await DetectWowAsync();
        var browse = CreateSecondaryButton("Browse...");
        browse.Click += async (_, _) => await BrowseForWowAsync();
        var addOns = CreateSecondaryButton("Open AddOns Folder");
        addOns.Click += (_, _) => OpenAddOnsFolder();
        var data = CreateSecondaryButton("View Submitted Data");
        data.Click += (_, _) => OpenResearchData();
        var syncNow = CreateSecondaryButton("Sync Now");
        syncNow.Click += async (_, _) => await SyncNowAsync(scanFirst: true);
        buttons.Controls.Add(detect);
        buttons.Controls.Add(browse);
        buttons.Controls.Add(addOns);
        buttons.Controls.Add(data);
        buttons.Controls.Add(syncNow);
        layout.Controls.Add(buttons, 0, 2);

        var serviceSync = new CheckBox
        {
            AutoSize = true,
            Checked = _settings.ServiceSyncEnabled,
            Text = "Sync Pending Observations to Azeroth Questing Server",
            Font = new Font("Segoe UI", 9, FontStyle.Regular),
            ForeColor = TextPrimary,
            BackColor = Surface,
            Margin = new Padding(0, 2, 0, 8),
        };
        serviceSync.CheckedChanged += async (_, _) =>
        {
            _settings.ServiceSyncEnabled = serviceSync.Checked;
            SettingsService.Save(_settings);
            _uploadValue.Text = serviceSync.Checked ? "Waiting for Azeroth Questing Server" : "Sync disabled";
            if (serviceSync.Checked)
            {
                await TrySyncPendingAsync(quiet: true);
            }
        };
        layout.Controls.Add(serviceSync, 0, 3);

        layout.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            MaximumSize = new Size(850, 0),
            Text = "Privacy: the companion reads only Azeroth Questing files and SavedVariables on disk. It does not inspect WoW process memory. When Azeroth Questing Server sync is enabled, only Azeroth Questing observation data is uploaded; disable the checkbox above to keep Pending Observations local.",
            Font = new Font("Segoe UI", 9, FontStyle.Regular),
            ForeColor = TextSecondary,
        }, 0, 4);

        panel.Controls.Add(layout);
        return panel;
    }

    private Control BuildActivityPanel()
    {
        var panel = CreateSurfacePanel();
        panel.Margin = new Padding(0);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(14),
            BackColor = Surface,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(CreateSectionTitle("Recent Activity"), 0, 0);

        _activityList.Dock = DockStyle.Fill;
        _activityList.BackColor = Surface;
        _activityList.ForeColor = TextSecondary;
        _activityList.BorderStyle = BorderStyle.None;
        _activityList.Font = new Font("Segoe UI", 9, FontStyle.Regular);
        _activityList.IntegralHeight = false;
        layout.Controls.Add(_activityList, 0, 1);
        panel.Controls.Add(layout);
        return panel;
    }

    private void BuildTrayIcon()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Show Azeroth Questing Companion", null, (_, _) => ShowFromTray());
        menu.Items.Add("Check for Updates", null, async (_, _) =>
        {
            ShowFromTray();
            await CheckForUpdatesAsync();
        });
        menu.Items.Add("Install / Update Addon", null, async (_, _) =>
        {
            ShowFromTray();
            await InstallAddonAsync();
        });
        menu.Items.Add("Sync Pending Observations", null, async (_, _) =>
        {
            ShowFromTray();
            await SyncNowAsync(scanFirst: true);
        });
        menu.Items.Add("View Submitted Data", null, (_, _) => OpenResearchData());
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
            AddActivity($"Companion {GetClientVersion()} started.");
            var saved = _settings.WowRetailPath;
            if (WowLocator.IsRetailPath(saved))
            {
                await ConfigureRetailPathAsync(saved!);
            }
            else
            {
                await DetectWowAsync();
            }

            await RefreshCompanionUpdateStatusAsync();
        }
        catch (Exception ex)
        {
            SetStatus($"Startup error: {ex.Message}");
            AddActivity($"Startup error: {ex.Message}");
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
                _installedAddonValue.Text = "WoW not detected";
                _installButton.Enabled = false;
                SetStatus("WoW Retail was not found automatically. Use Browse to select the _retail_ folder.");
                AddActivity("WoW Retail was not found automatically.");
                return;
            }

            await ConfigureRetailPathAsync(path);
            SetStatus("WoW Retail detected. Companion watcher is running.");
            AddActivity("WoW Retail detected.");
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
        AddActivity("WoW Retail path changed.");
    }

    private async Task ConfigureRetailPathAsync(string path)
    {
        _watcher?.Dispose();
        _watcher = null;

        _retailPath = Path.GetFullPath(path);
        _settings.WowRetailPath = _retailPath;
        SettingsService.Save(_settings);
        _wowPathValue.Text = _retailPath;
        _installButton.Enabled = true;
        _scanButton.Enabled = true;

        _watcher = new SavedVariablesWatcher(_retailPath, _snapshotQueue);
        _watcher.SnapshotProcessed += WatcherOnSnapshotProcessed;
        _watcher.WatcherError += WatcherOnError;
        _watcher.Start();

        await _watcher.ScanExistingAsync();
        RefreshLocalStatus();
        await RefreshRemoteAddonStatusAsync();
        await TrySyncPendingAsync(quiet: true);
    }

    private void UpdateChannelButtonText()
    {
        var channel = UpdateChannelSettings.Parse(_settings.UpdateChannel);
        _channelButton.Text = $"Channel: {UpdateChannelSettings.DisplayName(channel).ToUpperInvariant()}";
    }

    private async Task ToggleUpdateChannelAsync()
    {
        if (_busy)
        {
            SetStatus("The companion is already working on another task.");
            return;
        }

        var current = UpdateChannelSettings.Parse(_settings.UpdateChannel);
        var next = current == UpdateChannel.Beta ? UpdateChannel.Stable : UpdateChannel.Beta;
        _settings.UpdateChannel = UpdateChannelSettings.Serialize(next);
        SettingsService.Save(_settings);
        UpdateChannelButtonText();

        var display = UpdateChannelSettings.DisplayName(next);
        SetStatus($"Update channel changed to {display}. Checking releases...");
        AddActivity($"Update channel changed to {display}.");

        await RefreshCompanionUpdateStatusAsync();
        await RefreshRemoteAddonStatusAsync();
    }

    private async Task RefreshRemoteAddonStatusAsync()
    {
        try
        {
            var remote = await _github.GetLatestAddonAsync();
            ApplyAddonRemoteStatus(remote);
        }
        catch (Exception ex)
        {
            var installed = _retailPath is null ? null : AddonService.GetInstalledVersion(_retailPath);
            _installedAddonValue.Text = installed is null ? "Not installed" : installed;
            _latestAddonValue.Text = "Check failed";
            _heroStatusValue.Text = "Addon update check failed";
            _heroStatusValue.ForeColor = TextSecondary;
            SetStatus($"Addon update check failed: {ex.Message}");
        }
    }

    private async Task RefreshCompanionUpdateStatusAsync()
    {
        try
        {
            var remote = await _github.GetLatestCompanionAsync();
            if (remote is null)
            {
                _clientUpdateValue.Text = "No public release yet";
                return;
            }

            _clientUpdateValue.Text = IsNewerVersion(GetClientVersion(), remote.Version)
                ? $"{remote.Version} available"
                : $"Up to date ({remote.Version})";
        }
        catch
        {
            _clientUpdateValue.Text = "Check failed";
        }
    }

    private async Task CheckForUpdatesAsync()
    {
        if (!TryBeginBusy("Checking companion and addon updates..."))
        {
            return;
        }

        _checkUpdatesButton.Enabled = false;
        try
        {
            AddActivity("Checking for companion and addon updates...");

            RemoteCompanionPackage? companion = null;
            RemoteAddonPackage? addon = null;
            string? companionError = null;
            string? addonError = null;

            try
            {
                companion = await _github.GetLatestCompanionAsync();
            }
            catch (Exception ex)
            {
                companionError = ex.Message;
            }

            try
            {
                addon = await _github.GetLatestAddonAsync();
                ApplyAddonRemoteStatus(addon);
            }
            catch (Exception ex)
            {
                addonError = ex.Message;
            }

            var currentClient = GetClientVersion();
            var clientUpdateAvailable = companion is not null && IsNewerVersion(currentClient, companion.Version);
            if (companion is null)
            {
                _clientUpdateValue.Text = companionError is null ? "No public release yet" : "Check failed";
            }
            else if (clientUpdateAvailable)
            {
                _clientUpdateValue.Text = $"{companion.Version} available";
            }
            else
            {
                _clientUpdateValue.Text = $"Up to date ({companion.Version})";
            }

            var installedAddon = _retailPath is null ? null : AddonService.GetInstalledVersion(_retailPath);
            var addonSummary = addonError is not null
                ? $"Addon: update check failed ({addonError})"
                : addon is null
                    ? "Addon: update status unavailable"
                    : _retailPath is null
                        ? $"Addon: latest {addon.Version}; detect WoW to compare the installed version"
                        : installedAddon is null
                            ? $"Addon: not installed; latest {addon.Version}"
                            : VersionsMatch(installedAddon, addon.Version)
                                ? $"Addon: {installedAddon} is up to date"
                                : IsNewerVersion(installedAddon, addon.Version)
                                    ? $"Addon: update {addon.Version} is available (installed {installedAddon})"
                                    : $"Addon: installed {installedAddon} is newer than the selected channel release {addon.Version}";

            if (clientUpdateAvailable && companion is not null)
            {
                SetStatus($"Companion {companion.Version} found. Downloading and restarting automatically...");
                AddActivity($"Companion update {companion.Version} found.");
                var progress = new Progress<string>(SetStatus);
                await _updateService.StageAndLaunchUpdateAsync(companion, progress);
                AddActivity($"Companion {companion.Version} staged; restarting.");
                ExitForUpdate();
                return;
            }

            var clientSummary = companionError is not null
                ? $"Companion: update check failed ({companionError})"
                : companion is null
                    ? $"Companion: {currentClient}; no public release package is available yet"
                    : $"Companion: {currentClient} is up to date";

            SetStatus("Update check complete.");
            AddActivity("Update check complete.");
            MessageBox.Show(
                this,
                $"{clientSummary}\n\n{addonSummary}\n\nUse Update Addon if an addon update is available.",
                "Azeroth Questing Updates",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Azeroth Questing Companion", MessageBoxButtons.OK, MessageBoxIcon.Error);
            SetStatus($"Update failed: {ex.Message}");
            AddActivity($"Update failed: {ex.Message}");
        }
        finally
        {
            if (!IsDisposed && !Disposing)
            {
                _checkUpdatesButton.Enabled = true;
                EndBusy();
            }
        }
    }

    private void ApplyAddonRemoteStatus(RemoteAddonPackage remote)
    {
        _latestAddonValue.Text = remote.Version;

        if (_retailPath is null)
        {
            _installedAddonValue.Text = "WoW not detected";
            _heroStatusValue.Text = $"Latest addon: {remote.Version}";
            _heroStatusValue.ForeColor = TextSecondary;
            _installButton.Enabled = false;
            return;
        }

        var installed = AddonService.GetInstalledVersion(_retailPath);
        if (installed is null)
        {
            _installedAddonValue.Text = "Not installed";
            _heroStatusValue.Text = $"Addon not installed - latest {remote.Version}";
            _heroStatusValue.ForeColor = Gold;
            _installButton.Text = "Install Addon";
        }
        else if (VersionsMatch(installed, remote.Version))
        {
            _installedAddonValue.Text = installed;
            _heroStatusValue.Text = $"Addon installed - {installed} is current";
            _heroStatusValue.ForeColor = Green;
            _installButton.Text = "Reinstall Addon";
        }
        else if (IsNewerVersion(installed, remote.Version))
        {
            _installedAddonValue.Text = installed;
            _heroStatusValue.Text = $"Addon update {remote.Version} available";
            _heroStatusValue.ForeColor = Gold;
            _installButton.Text = "Update Addon";
        }
        else
        {
            _installedAddonValue.Text = installed;
            _heroStatusValue.Text = $"Installed addon {installed} is newer than channel version {remote.Version}";
            _heroStatusValue.ForeColor = Green;
            _installButton.Text = "Install Channel Version";
        }

        _installButton.Enabled = true;
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
            await RefreshRemoteAddonStatusAsync();
            RefreshLocalStatus();

            MessageBox.Show(
                this,
                $"Azeroth Questing {result.Version} is installed.\n\nBackup: {result.BackupDirectory}",
                "Azeroth Questing Companion",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            SetStatus($"Azeroth Questing {result.Version} installed successfully.");
            AddActivity($"Addon {result.Version} installed or repaired.");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Azeroth Questing Companion", MessageBoxButtons.OK, MessageBoxIcon.Error);
            SetStatus($"Install/update failed: {ex.Message}");
            AddActivity($"Addon update failed: {ex.Message}");
        }
        finally
        {
            _installButton.Enabled = _retailPath is not null;
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
            if (_settings.ServiceSyncEnabled)
            {
                await TrySyncPendingAsync(quiet: true);
            }
            SetStatus("SavedVariables scan complete. New observations were queued only when the data changed.");
            AddActivity("Manual SavedVariables scan completed.");
        }
        finally
        {
            EndBusy();
        }
    }

    private async Task SyncNowAsync(bool scanFirst = false)
    {
        if (!TryBeginBusy("Synchronizing Pending Observations with the Azeroth Questing Server..."))
        {
            return;
        }

        try
        {
            if (!_settings.ServiceSyncEnabled)
            {
                _uploadValue.Text = "Sync disabled";
                SetStatus("Azeroth Questing Server synchronization is disabled. Enable it in the World of Warcraft section first.");
                return;
            }

            if (scanFirst && _watcher is not null)
            {
                await _watcher.ScanExistingAsync();
                RefreshLocalStatus();
            }

            var result = await TrySyncPendingAsync(quiet: false);
            if (result is not null)
            {
                SetStatus(result.Status);
            }
        }
        finally
        {
            EndBusy();
        }
    }

    private async Task<ServiceSyncResult?> TrySyncPendingAsync(bool quiet)
    {
        if (!_settings.ServiceSyncEnabled)
        {
            SafeUi(() => _uploadValue.Text = "Sync disabled");
            return null;
        }

        await _syncGate.WaitAsync();
        try
        {
            var result = await _serviceClient.SyncOutboxAsync(GetClientVersion());
            SafeUi(() =>
            {
                _uploadValue.Text = result.Status;
                RefreshLocalStatus();
                if (result.UploadedSnapshots > 0)
                {
                    AddActivity(
                        $"Azeroth Questing Server sync completed: {result.UploadedObservations} structured observation(s), "
                        + $"{result.DuplicateObservations} duplicate(s), {result.UploadedSnapshots} pending file(s) cleared.");
                }
            });
            return result;
        }
        catch (Exception ex)
        {
            SafeUi(() =>
            {
                _uploadValue.Text = "Azeroth Questing Server unavailable";
                if (!quiet)
                {
                    SetStatus($"Azeroth Questing Server sync failed: {ex.Message}");
                    AddActivity($"Azeroth Questing Server sync failed: {ex.Message}");
                    MessageBox.Show(
                        this,
                        $"Pending Observations were kept locally.\n\n{ex.Message}",
                        "Azeroth Questing Server Sync",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
            });
            return null;
        }
        finally
        {
            _syncGate.Release();
        }
    }

    private void WatcherOnSnapshotProcessed(object? sender, SnapshotQueuedEventArgs e)
    {
        SafeUi(() =>
        {
            _lastDataValue.Text = e.Queued
                ? $"{DateTime.Now:g} - queued"
                : $"{DateTime.Now:g} - unchanged";
            RefreshLocalStatus();
            if (e.Queued)
            {
                SetStatus("Azeroth Questing data changed; a new pending observation was queued locally.");
                AddActivity("New Azeroth Questing observation queued.");
                if (_settings.ServiceSyncEnabled)
                {
                    _ = TrySyncPendingAsync(quiet: true);
                }
            }
        });
    }

    private void WatcherOnError(object? sender, string message)
    {
        SafeUi(() =>
        {
            SetStatus($"SavedVariables watcher: {message}");
            AddActivity($"Watcher error: {message}");
        });
    }

    private void RefreshLocalStatus()
    {
        _queueValue.Text = _snapshotQueue.Count.ToString();
        _dataValue.Text = (_watcher?.DataFileCount ?? 0).ToString();
    }

    private void OpenResearchData()
    {
        if (!_settings.ServiceSyncEnabled)
        {
            MessageBox.Show(
                this,
                "Azeroth Questing Server synchronization is disabled. Enable it before viewing submitted research data.",
                "Azeroth Questing Submitted Data",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        using var viewer = new DataViewerForm(_serviceClient, GetClientVersion());
        viewer.ShowDialog(this);
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

    private void AddActivity(string message)
    {
        var entry = $"{DateTime.Now:HH:mm}  {message}";
        _activityList.Items.Insert(0, entry);
        while (_activityList.Items.Count > 40)
        {
            _activityList.Items.RemoveAt(_activityList.Items.Count - 1);
        }
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

    private void ExitForUpdate()
    {
        _exitRequested = true;
        if (_notifyIcon is not null)
        {
            _notifyIcon.Visible = false;
        }
        Close();
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
        return ReleaseVersionUtility.Matches(left, right);
    }

    private static bool IsNewerVersion(string current, string remote)
    {
        return ReleaseVersionUtility.IsNewer(current, remote);
    }

    private static string GetClientVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
        {
            var displayVersion = informational.Split('+', 2)[0].Trim();
            if (ReleaseVersionUtility.TryParse(displayVersion, out _))
            {
                return displayVersion;
            }
        }

        var version = assembly.GetName().Version;
        return version is null ? "0.1.8-beta.3" : $"{version.Major}.{version.Minor}.{version.Build}";
    }

    private static Panel CreateSurfacePanel() => new()
    {
        Dock = DockStyle.Fill,
        BackColor = Surface,
        BorderStyle = BorderStyle.FixedSingle,
    };

    private static Panel CreateCard(string title, Label value, string subtitle)
    {
        var card = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(5),
            Padding = new Padding(14, 12, 12, 10),
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Surface,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(new Label
        {
            AutoSize = true,
            Text = title,
            Font = new Font("Segoe UI", 9, FontStyle.Regular),
            ForeColor = TextSecondary,
        }, 0, 0);

        value.Font = new Font("Segoe UI", 13, FontStyle.Bold);
        value.ForeColor = TextPrimary;
        value.Dock = DockStyle.Fill;
        value.TextAlign = ContentAlignment.MiddleLeft;
        value.MaximumSize = new Size(260, 54);
        layout.Controls.Add(value, 0, 1);
        layout.Controls.Add(new Label
        {
            AutoSize = true,
            Text = subtitle,
            Font = new Font("Segoe UI", 8, FontStyle.Regular),
            ForeColor = TextSecondary,
        }, 0, 2);
        card.Controls.Add(layout);
        return card;
    }

    private static Label CreateValueLabel(string text) => new()
    {
        AutoSize = true,
        Text = text,
        Font = new Font("Segoe UI", 10, FontStyle.Regular),
        ForeColor = TextPrimary,
        Padding = new Padding(0, 2, 0, 2),
    };

    private static Label CreateSectionTitle(string text) => new()
    {
        AutoSize = true,
        Text = text,
        Font = new Font("Segoe UI", 11, FontStyle.Bold),
        ForeColor = TextPrimary,
        Padding = new Padding(0, 0, 0, 4),
    };

    private static Button CreateActionButton(string text, Color accent)
    {
        var button = new Button
        {
            AutoSize = true,
            Text = text,
            FlatStyle = FlatStyle.Flat,
            BackColor = SurfaceAlt,
            ForeColor = TextPrimary,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            Padding = new Padding(10, 5, 10, 5),
            Margin = new Padding(0, 0, 9, 0),
            Cursor = Cursors.Hand,
        };
        button.FlatAppearance.BorderColor = accent;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(42, 47, 66);
        return button;
    }

    private static Button CreateSecondaryButton(string text)
    {
        var button = new Button
        {
            AutoSize = true,
            Text = text,
            FlatStyle = FlatStyle.Flat,
            BackColor = SurfaceAlt,
            ForeColor = TextPrimary,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
            Padding = new Padding(7, 3, 7, 3),
            Margin = new Padding(0, 0, 8, 0),
            Cursor = Cursors.Hand,
        };
        button.FlatAppearance.BorderColor = Border;
        return button;
    }

    private static Button CreateNavButton(string text, EventHandler handler, bool active = false)
    {
        var button = new Button
        {
            Dock = DockStyle.Fill,
            Text = text,
            TextAlign = ContentAlignment.MiddleLeft,
            FlatStyle = FlatStyle.Flat,
            BackColor = active ? Color.FromArgb(42, 35, 74) : Sidebar,
            ForeColor = active ? Gold : TextPrimary,
            Font = new Font("Segoe UI", 10, active ? FontStyle.Bold : FontStyle.Regular),
            Padding = new Padding(14, 0, 0, 0),
            Margin = new Padding(0, 2, 0, 2),
            Cursor = Cursors.Hand,
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(37, 40, 56);
        button.Click += handler;
        return button;
    }

    private void TryEnableDarkTitleBar()
    {
        try
        {
            var enabled = 1;
            if (DwmSetWindowAttribute(Handle, 20, ref enabled, sizeof(int)) != 0)
            {
                DwmSetWindowAttribute(Handle, 19, ref enabled, sizeof(int));
            }
        }
        catch
        {
            // Older Windows versions may not support immersive dark title bars.
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int valueSize);
}
