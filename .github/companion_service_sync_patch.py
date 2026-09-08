from pathlib import Path


def read(path):
    return Path(path).read_text(encoding="utf-8")


def write(path, text):
    Path(path).write_text(text, encoding="utf-8")


def replace_once(path, old, new):
    text = read(path)
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"{path}: expected exactly one match, found {count}: {old[:140]!r}")
    write(path, text.replace(old, new, 1))


# ---------------------------------------------------------------------------
# Service01 client: make an empty outbox perform a real health check.
# ---------------------------------------------------------------------------
path = "src/AzerothQuesting.Companion/Service01Client.cs"
replace_once(
    path,
    '''        if (files.Length == 0)
        {
            return new ServiceSyncResult(0, 0, 0, 0, "Connected - nothing pending");
        }
''',
    '''        if (files.Length == 0)
        {
            await CheckStatusAsync(cancellationToken);
            return new ServiceSyncResult(0, 0, 0, 0, "Connected - nothing pending");
        }
''',
)
replace_once(
    path,
    '''    private async Task EnsureRegisteredAsync(string companionVersion, CancellationToken cancellationToken)
''',
    '''    public async Task CheckStatusAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        using var request = new HttpRequestMessage(HttpMethod.Get, BaseUrl + "/api/v1/status");
        request.Headers.UserAgent.ParseAdd("AzerothQuestingCompanion");
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, "status check", cancellationToken);
    }

    private async Task EnsureRegisteredAsync(string companionVersion, CancellationToken cancellationToken)
''',
)

# ---------------------------------------------------------------------------
# Main dashboard: Service01 auto-sync, manual sync, and opt-out control.
# ---------------------------------------------------------------------------
path = "src/AzerothQuesting.Companion/MainForm.cs"
replace_once(
    path,
    '''    private readonly AddonService _addonService;
    private readonly CompanionUpdateService _updateService;
    private readonly CompanionSettings _settings;
''',
    '''    private readonly AddonService _addonService;
    private readonly CompanionUpdateService _updateService;
    private readonly CompanionSettings _settings;
    private readonly Service01Client _serviceClient;
    private readonly SemaphoreSlim _syncGate = new(1, 1);
''',
)
replace_once(
    path,
    '''    private readonly Button _checkUpdatesButton = CreateActionButton("Check for Updates", Gold);
    private readonly Button _installButton = CreateActionButton("Install / Update Addon", Gold);
    private readonly Button _scanButton = CreateActionButton("Scan for Observations", Purple);
''',
    '''    private readonly Button _checkUpdatesButton = CreateActionButton("Check for Updates", Gold);
    private readonly Button _installButton = CreateActionButton("Install / Update Addon", Gold);
    private readonly Button _scanButton = CreateActionButton("Scan for Observations", Purple);
    private readonly Button _syncButton = CreateActionButton("Sync Now", Purple);
''',
)
replace_once(
    path,
    '''        _settings = SettingsService.Load();
        _addonService = new AddonService(_github);
        _updateService = new CompanionUpdateService(_github);
''',
    '''        _settings = SettingsService.Load();
        _addonService = new AddonService(_github);
        _updateService = new CompanionUpdateService(_github);
        _serviceClient = new Service01Client(_settings);
''',
)
replace_once(
    path,
    '''            _watcher?.Dispose();
            _notifyIcon?.Dispose();
            _github.Dispose();
''',
    '''            _watcher?.Dispose();
            _notifyIcon?.Dispose();
            _serviceClient.Dispose();
            _syncGate.Dispose();
            _github.Dispose();
''',
)
replace_once(
    path,
    '''        _clientVersionValue.Text = GetClientVersion();
        _uploadValue.Text = "Service01 not connected yet";
''',
    '''        _clientVersionValue.Text = GetClientVersion();
        _uploadValue.Text = _settings.ServiceSyncEnabled
            ? "Waiting for Service01"
            : "Sync disabled";
''',
)
replace_once(
    path,
    '''        layout.Controls.Add(CreateNavButton("Sync", async (_, _) => await ScanNowAsync()), 0, 3);
''',
    '''        layout.Controls.Add(CreateNavButton("Sync", async (_, _) => await SyncNowAsync(scanFirst: true)), 0, 3);
''',
)
replace_once(
    path,
    '''        _checkUpdatesButton.Click += async (_, _) => await CheckForUpdatesAsync();
        _installButton.Click += async (_, _) => await InstallAddonAsync();
        _scanButton.Click += async (_, _) => await ScanNowAsync();
''',
    '''        _checkUpdatesButton.Click += async (_, _) => await CheckForUpdatesAsync();
        _installButton.Click += async (_, _) => await InstallAddonAsync();
        _scanButton.Click += async (_, _) => await ScanNowAsync();
        _syncButton.Click += async (_, _) => await SyncNowAsync(scanFirst: true);
''',
)
replace_once(
    path,
    '''        bar.Controls.Add(_checkUpdatesButton);
        bar.Controls.Add(_installButton);
        bar.Controls.Add(_scanButton);
        bar.Controls.Add(repair);
''',
    '''        bar.Controls.Add(_checkUpdatesButton);
        bar.Controls.Add(_installButton);
        bar.Controls.Add(_scanButton);
        bar.Controls.Add(_syncButton);
        bar.Controls.Add(repair);
''',
)
replace_once(
    path,
    '''        cards.Controls.Add(CreateCard("Pending Observations", _queueValue, "Waiting for Service01"), 0, 1);
''',
    '''        cards.Controls.Add(CreateCard("Pending Observations", _queueValue, "Local upload queue"), 0, 1);
''',
)
replace_once(
    path,
    '''        cards.Controls.Add(CreateCard("Sync Status", _uploadValue, "Upload API"), 3, 1);
''',
    '''        cards.Controls.Add(CreateCard("Sync Status", _uploadValue, "Service01 API"), 3, 1);
''',
)
replace_once(
    path,
    '''            RowCount = 4,
''',
    '''            RowCount = 5,
''',
)
replace_once(
    path,
    '''        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
''',
    '''        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
''',
)
replace_once(
    path,
    '''        var data = CreateSecondaryButton("Open Companion Data");
        data.Click += (_, _) => OpenPath(AppPaths.Root);
        buttons.Controls.Add(detect);
        buttons.Controls.Add(browse);
        buttons.Controls.Add(addOns);
        buttons.Controls.Add(data);
        layout.Controls.Add(buttons, 0, 2);

        layout.Controls.Add(new Label
''',
    '''        var data = CreateSecondaryButton("Open Companion Data");
        data.Click += (_, _) => OpenPath(AppPaths.Root);
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
            Text = $"Sync Pending Observations to Service01 ({_serviceClient.BaseUrl})",
            Font = new Font("Segoe UI", 9, FontStyle.Regular),
            ForeColor = TextPrimary,
            BackColor = Surface,
            Margin = new Padding(0, 2, 0, 8),
        };
        serviceSync.CheckedChanged += async (_, _) =>
        {
            _settings.ServiceSyncEnabled = serviceSync.Checked;
            SettingsService.Save(_settings);
            _uploadValue.Text = serviceSync.Checked ? "Waiting for Service01" : "Sync disabled";
            if (serviceSync.Checked)
            {
                await TrySyncPendingAsync(quiet: true);
            }
        };
        layout.Controls.Add(serviceSync, 0, 3);

        layout.Controls.Add(new Label
''',
)
replace_once(
    path,
    '''            Text = "Privacy: the companion reads only Azeroth Questing files and SavedVariables on disk. It does not inspect WoW process memory. Service01 uploading is not enabled yet, so observations remain in the local Outbox.",
''',
    '''            Text = "Privacy: the companion reads only Azeroth Questing files and SavedVariables on disk. It does not inspect WoW process memory. When Service01 sync is enabled, only Azeroth Questing observation data is uploaded; disable the checkbox above to keep Pending Observations local.",
''',
)
replace_once(
    path,
    '''        }, 0, 3);
''',
    '''        }, 0, 4);
''',
)
replace_once(
    path,
    '''        menu.Items.Add("Install / Update Addon", null, async (_, _) =>
        {
            ShowFromTray();
            await InstallAddonAsync();
        });
        menu.Items.Add("Open Companion Data", null, (_, _) => OpenPath(AppPaths.Root));
''',
    '''        menu.Items.Add("Install / Update Addon", null, async (_, _) =>
        {
            ShowFromTray();
            await InstallAddonAsync();
        });
        menu.Items.Add("Sync Pending Observations", null, async (_, _) =>
        {
            ShowFromTray();
            await SyncNowAsync(scanFirst: true);
        });
        menu.Items.Add("Open Companion Data", null, (_, _) => OpenPath(AppPaths.Root));
''',
)
replace_once(
    path,
    '''        await _watcher.ScanExistingAsync();
        RefreshLocalStatus();
        await RefreshRemoteAddonStatusAsync();
''',
    '''        await _watcher.ScanExistingAsync();
        RefreshLocalStatus();
        await RefreshRemoteAddonStatusAsync();
        await TrySyncPendingAsync(quiet: true);
''',
)
replace_once(
    path,
    '''            await _watcher.ScanExistingAsync();
            RefreshLocalStatus();
            SetStatus("SavedVariables scan complete. New observations were queued only when the data changed.");
            AddActivity("Manual SavedVariables scan completed.");
''',
    '''            await _watcher.ScanExistingAsync();
            RefreshLocalStatus();
            if (_settings.ServiceSyncEnabled)
            {
                await TrySyncPendingAsync(quiet: true);
            }
            SetStatus("SavedVariables scan complete. New observations were queued only when the data changed.");
            AddActivity("Manual SavedVariables scan completed.");
''',
)
replace_once(
    path,
    '''    private void WatcherOnSnapshotProcessed(object? sender, SnapshotQueuedEventArgs e)
''',
    '''    private async Task SyncNowAsync(bool scanFirst = false)
    {
        if (!TryBeginBusy("Synchronizing Pending Observations with Service01..."))
        {
            return;
        }

        try
        {
            if (!_settings.ServiceSyncEnabled)
            {
                _uploadValue.Text = "Sync disabled";
                SetStatus("Service01 synchronization is disabled. Enable it in the World of Warcraft section first.");
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
                        $"Service01 sync completed: {result.UploadedObservations} structured observation(s), "
                        + $"{result.DuplicateObservations} duplicate(s), {result.UploadedSnapshots} pending file(s) cleared.");
                }
            });
            return result;
        }
        catch (Exception ex)
        {
            SafeUi(() =>
            {
                _uploadValue.Text = "Service01 unavailable";
                if (!quiet)
                {
                    SetStatus($"Service01 sync failed: {ex.Message}");
                    AddActivity($"Service01 sync failed: {ex.Message}");
                    MessageBox.Show(
                        this,
                        $"Pending Observations were kept locally.\n\n{ex.Message}",
                        "Azeroth Questing Service01 Sync",
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
''',
)
replace_once(
    path,
    '''            if (e.Queued)
            {
                SetStatus("Azeroth Questing data changed; a new pending observation was queued locally.");
                AddActivity("New Azeroth Questing observation queued.");
            }
''',
    '''            if (e.Queued)
            {
                SetStatus("Azeroth Questing data changed; a new pending observation was queued locally.");
                AddActivity("New Azeroth Questing observation queued.");
                if (_settings.ServiceSyncEnabled)
                {
                    _ = TrySyncPendingAsync(quiet: true);
                }
            }
''',
)
replace_once(
    path,
    '''        return version is null ? "0.1.2" : $"{version.Major}.{version.Minor}.{version.Build}";
''',
    '''        return version is null ? "0.1.5" : $"{version.Major}.{version.Minor}.{version.Build}";
''',
)

# ---------------------------------------------------------------------------
# Version metadata.
# ---------------------------------------------------------------------------
path = "src/AzerothQuesting.Companion/AzerothQuesting.Companion.csproj"
text = read(path)
for old, new in [
    ("<Version>0.1.4</Version>", "<Version>0.1.5</Version>"),
    ("<FileVersion>0.1.4.0</FileVersion>", "<FileVersion>0.1.5.0</FileVersion>"),
    ("<AssemblyVersion>0.1.4.0</AssemblyVersion>", "<AssemblyVersion>0.1.5.0</AssemblyVersion>"),
]:
    if text.count(old) != 1:
        raise SystemExit(f"{path}: missing expected version marker {old}")
    text = text.replace(old, new, 1)
write(path, text)

# ---------------------------------------------------------------------------
# README and privacy policy now describe enabled, opt-out Service01 sync.
# ---------------------------------------------------------------------------
path = "README.md"
text = read(path)
text = text.replace("## Development version - v0.1.4", "## Development version - v0.1.5", 1)
text = text.replace(
    "- Queues deduplicated **Pending Observations** under `%LOCALAPPDATA%\\AzerothQuesting\\Companion\\Outbox` until Service01 accepts uploads.\n",
    "- Queues deduplicated **Pending Observations** under `%LOCALAPPDATA%\\AzerothQuesting\\Companion\\Outbox` and synchronizes them to the Service01 Azeroth Questing API when synchronization is enabled.\n- Uses per-installation bearer registration; no shared Service01 secret is embedded in the public companion.\n- Uploads new v0.2.32+ structured quest observations with quest/map/evidence, faction, class, level, completion, source, and timestamps; older already-queued snapshots use the compatibility raw endpoint.\n",
    1,
)
text = text.replace(
    "The Service01 upload API is still a backend step. Pending observations remain local until that API exists and can be validated.\n",
    "Service01 synchronization targets `http://10.0.10.246:8766` on the FrostLabs LAN. The dashboard includes a **Sync Pending Observations to Service01** checkbox and **Sync Now** action. If Service01 is unavailable, Pending Observations remain in the local Outbox and are retried later. Internet-facing synchronization is not enabled; a future public endpoint must use HTTPS and abuse controls.\n",
    1,
)
text = text.replace(
    "Beginning with v0.1.4, the installer targets",
    "Beginning with v0.1.4, the installer targets",
    1,
)
text = text.replace(
    "The current companion does not upload Pending Observations to Service01. When Service01 uploading is added, the privacy policy and user controls will be updated before that feature is enabled.\n",
    "Beginning with development v0.1.5, Service01 synchronization can upload Pending Observations to the private FrostLabs API. Synchronization is enabled by default for the FrostLabs LAN build and can be disabled at any time from the dashboard; failed uploads remain local for retry. See `PRIVACY.md` for the exact observation fields.\n",
    1,
)
write(path, text)

path = "PRIVACY.md"
text = read(path)
old = '''## Network communication

The current version does not upload Pending Observations to Service01. Pending Observations remain on the local computer.

The companion does access GitHub to check for and download official Azeroth Questing Companion and Azeroth Questing addon updates.

If Service01 synchronization is added in a future version, this policy and the installer/application controls will be updated before that feature is enabled. Any automatic transfer of observation data will be documented and provided with an appropriate user control or opt-out consistent with the SignPath Foundation open-source signing requirements.
'''
new = '''## Network communication

Beginning with development version 0.1.5, the companion can synchronize Pending Observations to the private FrostLabs Service01 Azeroth Questing API. The default FrostLabs LAN endpoint is `http://10.0.10.246:8766`. The dashboard provides a **Sync Pending Observations to Service01** checkbox and a manual **Sync Now** action. Turning the checkbox off keeps new Pending Observations local.

For Azeroth Questing v0.2.32 and newer, structured uploads can include only addon-produced research fields: observation key, local/anonymous-peer source, quest ID, map ID, evidence type (`seen`, `available`, `offered`, `accepted`, `active`, or `turnedIn`), faction, World of Warcraft class ID/token, character level, quest completion state, observation timestamp, addon version, and companion version. The intended upload does not include character name, realm, BattleTag, guild, chat, party-member identity, GUID, screenshots, or gameplay recordings.

The Service01 API assigns a random per-installation bearer token. The public companion does not contain a shared server secret. Service01 stores only the SHA-256 hash of the bearer token. If synchronization fails, the companion keeps the Pending Observation locally instead of deleting it.

Older Pending Observation files created before the structured v0.2.32 handoff can be sent through a compatibility raw SavedVariables endpoint so existing queued research is not silently discarded. Those files are stored as data and are not executed as Lua by the server.

The companion also accesses GitHub to check for and download official Azeroth Questing Companion and Azeroth Questing addon updates.
'''
if old not in text:
    raise SystemExit("PRIVACY.md: expected old network section not found")
write(path, text.replace(old, new, 1))

# ---------------------------------------------------------------------------
# Development changelog.
# ---------------------------------------------------------------------------
path = "CHANGELOG.md"
text = read(path)
header = "# Changelog\n\n"
if not text.startswith(header):
    raise SystemExit("Unexpected companion changelog header")
entry = '''## 0.1.5 - September 8, 2026

- Added Service01 synchronization for Pending Observations using the private Azeroth Questing API on the FrostLabs LAN.
- Added anonymous per-installation registration so each client receives its own bearer token; no shared backend secret is embedded in the public companion.
- Added structured v0.2.32+ observation uploads for quest/map/evidence, faction, class ID/token, level, completion, source, timestamp, and addon/client versions.
- Kept compatibility upload support for already-queued pre-v0.2.32 SavedVariables snapshots so existing research is not silently discarded.
- Added automatic retry behavior: files are deleted from the local Outbox only after Service01 accepts them; unavailable/failed uploads remain local.
- Added **Sync Now** controls to the dashboard, sidebar, and tray menu plus a **Sync Pending Observations to Service01** opt-out checkbox.
- Changed the Sync Status card to report the Service01 API state rather than the previous placeholder.
- Updated the privacy policy and README before enabling network transfer behavior.

The v0.1.5 Service01 client code has not yet been tested against the live Services01 host or on the player's Windows installation. The GitHub development build must compile successfully, then the Service01 API must be deployed and health-checked before end-to-end synchronization can be considered working. Public signed publication still waits for SignPath Foundation approval/configuration; the latest public release remains v0.1.3.

'''
write(path, header + entry + text[len(header):])
