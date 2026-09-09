from pathlib import Path

# Version metadata
csproj_path = Path("src/AzerothQuesting.Companion/AzerothQuesting.Companion.csproj")
csproj = csproj_path.read_text(encoding="utf-8")
csproj = csproj.replace("<Version>0.1.8-beta.2</Version>", "<Version>0.1.8-beta.3</Version>", 1)
csproj = csproj.replace("<FileVersion>0.1.8.2</FileVersion>", "<FileVersion>0.1.8.3</FileVersion>", 1)
csproj_path.write_text(csproj, encoding="utf-8")

# Dashboard response model
models_path = Path("src/AzerothQuesting.Companion/ResearchDashboardModels.cs")
models = models_path.read_text(encoding="utf-8")
old = '''    [JsonPropertyName("installations")]
    public int Installations { get; set; }

    [JsonPropertyName("active_installations_30d")]'''
new = '''    [JsonPropertyName("installations")]
    public int Installations { get; set; }

    [JsonPropertyName("connected_installations")]
    public int ConnectedInstallations { get; set; }

    [JsonPropertyName("active_installations_30d")]'''
if old not in models:
    raise SystemExit("Could not find ResearchSummary installations marker")
models = models.replace(old, new, 1)
models_path.write_text(models, encoding="utf-8")

# Service client heartbeat and export support
client_path = Path("src/AzerothQuesting.Companion/Service01Client.cs")
client = client_path.read_text(encoding="utf-8")

old_fields = '''    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);
    private bool _disposed;'''
new_fields = '''    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);
    private readonly SemaphoreSlim _heartbeatGate = new(1, 1);
    private System.Threading.Timer? _heartbeatTimer;
    private string? _heartbeatCompanionVersion;
    private bool _disposed;'''
if old_fields not in client:
    raise SystemExit("Could not find Service01Client field marker")
client = client.replace(old_fields, new_fields, 1)

sync_marker = '''        ThrowIfDisposed();
        AppPaths.EnsureCreated();

        var files = Directory'''
sync_new = '''        ThrowIfDisposed();
        AppPaths.EnsureCreated();
        ConfigureHeartbeat(companionVersion);
        await SendHeartbeatAsync(cancellationToken);

        var files = Directory'''
if sync_marker not in client:
    raise SystemExit("Could not find SyncOutboxAsync startup marker")
client = client.replace(sync_marker, sync_new, 1)

research_marker = '''        ThrowIfDisposed();
        await EnsureRegisteredAsync(companionVersion, cancellationToken);

        using var response = await SendAuthorizedGetAsync("/api/v1/research/dashboard", cancellationToken);'''
research_new = '''        ThrowIfDisposed();
        ConfigureHeartbeat(companionVersion);
        await SendHeartbeatAsync(cancellationToken);
        await EnsureRegisteredAsync(companionVersion, cancellationToken);

        using var response = await SendAuthorizedGetAsync("/api/v1/research/dashboard", cancellationToken);'''
if research_marker not in client:
    raise SystemExit("Could not find research dashboard marker")
client = client.replace(research_marker, research_new, 1)

send_get_marker = '''    private async Task<HttpResponseMessage> SendAuthorizedGetAsync(
        string relativePath,
        CancellationToken cancellationToken)'''
heartbeat_block = '''    public async Task<string> ExportCollectedQuestsCsvAsync(
        string companionVersion,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ConfigureHeartbeat(companionVersion);
        await SendHeartbeatAsync(cancellationToken);
        await EnsureRegisteredAsync(companionVersion, cancellationToken);

        using var response = await SendAuthorizedGetAsync("/api/v1/research/export/quests.csv", cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            ClearRegistration();
            await EnsureRegisteredAsync(companionVersion, cancellationToken);
            using var retry = await SendAuthorizedGetAsync("/api/v1/research/export/quests.csv", cancellationToken);
            return await ReadTextExportAsync(retry, cancellationToken);
        }

        return await ReadTextExportAsync(response, cancellationToken);
    }

    private void ConfigureHeartbeat(string companionVersion)
    {
        _heartbeatCompanionVersion = companionVersion;
        _heartbeatTimer ??= new System.Threading.Timer(
            _ => _ = RunHeartbeatAsync(),
            null,
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(5));
    }

    private async Task RunHeartbeatAsync()
    {
        if (_disposed || !_settings.ServiceSyncEnabled || string.IsNullOrWhiteSpace(_heartbeatCompanionVersion))
        {
            return;
        }

        try
        {
            await SendHeartbeatAsync(CancellationToken.None);
        }
        catch
        {
            // Heartbeats are best-effort. Normal sync and dashboard requests
            // still surface server errors to the player.
        }
    }

    private async Task SendHeartbeatAsync(CancellationToken cancellationToken)
    {
        if (!await _heartbeatGate.WaitAsync(0, cancellationToken))
        {
            return;
        }

        try
        {
            var companionVersion = _heartbeatCompanionVersion;
            if (string.IsNullOrWhiteSpace(companionVersion))
            {
                return;
            }

            await EnsureRegisteredAsync(companionVersion, cancellationToken);
            var request = new HeartbeatRequest
            {
                CompanionVersion = companionVersion,
                AddonVersion = GetInstalledAddonVersion(),
                UpdateChannel = UpdateChannelSettings.Serialize(UpdateChannelSettings.Parse(_settings.UpdateChannel)),
            };

            using var response = await PostJsonAsync(
                "/api/v1/installations/heartbeat",
                request,
                _settings.InstallationToken,
                cancellationToken);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                ClearRegistration();
                await EnsureRegisteredAsync(companionVersion, cancellationToken);
                using var retry = await PostJsonAsync(
                    "/api/v1/installations/heartbeat",
                    request,
                    _settings.InstallationToken,
                    cancellationToken);
                await EnsureSuccessAsync(retry, "heartbeat", cancellationToken);
                return;
            }

            await EnsureSuccessAsync(response, "heartbeat", cancellationToken);
        }
        finally
        {
            _heartbeatGate.Release();
        }
    }

    private string? GetInstalledAddonVersion()
    {
        var retailPath = _settings.WowRetailPath;
        if (!WowLocator.IsRetailPath(retailPath))
        {
            return null;
        }

        return AddonService.GetInstalledVersion(retailPath!);
    }

    private async Task<string> ReadTextExportAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Azeroth Questing server collected quest export failed ({(int)response.StatusCode} {response.ReasonPhrase}): {TrimForError(body)}");
        }

        return body;
    }

    private async Task<HttpResponseMessage> SendAuthorizedGetAsync(
        string relativePath,
        CancellationToken cancellationToken)'''
if send_get_marker not in client:
    raise SystemExit("Could not find SendAuthorizedGetAsync marker")
client = client.replace(send_get_marker, heartbeat_block, 1)

registration_class_marker = '''    private sealed class RegistrationRequest
    {'''
heartbeat_request_class = '''    private sealed class HeartbeatRequest
    {
        [JsonPropertyName("companion_version")]
        public string? CompanionVersion { get; set; }

        [JsonPropertyName("addon_version")]
        public string? AddonVersion { get; set; }

        [JsonPropertyName("update_channel")]
        public string? UpdateChannel { get; set; }
    }

    private sealed class RegistrationRequest
    {'''
if registration_class_marker not in client:
    raise SystemExit("Could not find RegistrationRequest class marker")
client = client.replace(registration_class_marker, heartbeat_request_class, 1)

old_dispose = '''        _disposed = true;
        _httpClient.Dispose();'''
new_dispose = '''        _disposed = true;
        _heartbeatTimer?.Dispose();
        _heartbeatGate.Dispose();
        _httpClient.Dispose();'''
if old_dispose not in client:
    raise SystemExit("Could not find Service01Client Dispose marker")
client = client.replace(old_dispose, new_dispose, 1)
client_path.write_text(client, encoding="utf-8")

# Data viewer online count and CSV export button
viewer_path = Path("src/AzerothQuesting.Companion/DataViewerForm.cs")
viewer = viewer_path.read_text(encoding="utf-8")
viewer = viewer.replace(
    '    private readonly Button _refreshButton = CreateActionButton("Refresh", Gold);',
    '    private readonly Button _refreshButton = CreateActionButton("Refresh", Gold);\n    private readonly Button _exportButton = CreateActionButton("Export Collected Quests", Gold);',
    1,
)
viewer = viewer.replace(
    '''            ColumnCount = 2,
            RowCount = 1,
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));''',
    '''            ColumnCount = 3,
            RowCount = 1,
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));''',
    1,
)
old_header_actions = '''        _refreshButton.Click += async (_, _) => await RefreshAsync();
        _refreshButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;

        panel.Controls.Add(title, 0, 0);
        panel.Controls.Add(_refreshButton, 1, 0);'''
new_header_actions = '''        _exportButton.Click += async (_, _) => await ExportCollectedQuestsAsync();
        _exportButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _exportButton.Margin = new Padding(0, 0, 8, 0);
        _refreshButton.Click += async (_, _) => await RefreshAsync();
        _refreshButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;

        panel.Controls.Add(title, 0, 0);
        panel.Controls.Add(_exportButton, 1, 0);
        panel.Controls.Add(_refreshButton, 2, 0);'''
if old_header_actions not in viewer:
    raise SystemExit("Could not find DataViewer header actions marker")
viewer = viewer.replace(old_header_actions, new_header_actions, 1)
viewer = viewer.replace(
    'CreateMetricCard("Active Installations", _activeInstallations, "Seen by the server in the last 30 days")',
    'CreateMetricCard("Companions Online", _activeInstallations, "Heartbeat seen in the last 10 minutes")',
    1,
)
viewer = viewer.replace(
    '_activeInstallations.Text = data.Summary.ActiveInstallations30Days.ToString("N0");',
    '_activeInstallations.Text = data.Summary.ConnectedInstallations.ToString("N0");',
    1,
)

apply_marker = '''    private void ApplyData(ResearchDashboardData data)
    {'''
export_method = '''    private async Task ExportCollectedQuestsAsync()
    {
        if (_loading)
        {
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Title = "Export Azeroth Questing Collected Quests",
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            DefaultExt = "csv",
            AddExtension = true,
            FileName = $"AzerothQuesting-Collected-Quests-{DateTime.Now:yyyyMMdd-HHmmss}.csv",
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _exportButton.Enabled = false;
        _status.ForeColor = TextSecondary;
        _status.Text = "Exporting every quest currently collected by Azeroth Questing research...";
        try
        {
            var csv = await _serviceClient.ExportCollectedQuestsCsvAsync(_companionVersion);
            await File.WriteAllTextAsync(dialog.FileName, csv);
            _status.ForeColor = Green;
            _status.Text = $"Collected quest export saved to {dialog.FileName}";
        }
        catch (Exception ex)
        {
            _status.ForeColor = Gold;
            _status.Text = $"Could not export collected quests: {ex.Message}";
        }
        finally
        {
            _exportButton.Enabled = true;
        }
    }

    private void ApplyData(ResearchDashboardData data)
    {'''
if apply_marker not in viewer:
    raise SystemExit("Could not find DataViewer ApplyData marker")
viewer = viewer.replace(apply_marker, export_method, 1)
viewer_path.write_text(viewer, encoding="utf-8")

# Fallback displayed version in MainForm
main_form_path = Path("src/AzerothQuesting.Companion/MainForm.cs")
main_form = main_form_path.read_text(encoding="utf-8")
main_form = main_form.replace('return version is null ? "0.1.8-beta.2"', 'return version is null ? "0.1.8-beta.3"', 1)
main_form_path.write_text(main_form, encoding="utf-8")

# Changelog
changelog_path = Path("CHANGELOG.md")
changelog = changelog_path.read_text(encoding="utf-8")
entry = '''## 0.1.8 Beta 3 - September 8, 2026

- Added a lightweight authenticated Companion heartbeat every five minutes while Azeroth Questing Server synchronization is enabled, allowing the research dashboard to show **Companions Online** as installations seen within the last 10 minutes.
- Heartbeats report only the existing anonymous installation identity plus Companion version, installed addon version, Stable/Beta update channel, and last-seen time; they do not add character, account, Windows-user, or gameplay identity data.
- Added **Export Collected Quests** to the Submitted Research Data viewer. The export saves every distinct quest ID currently collected by Azeroth Questing research as CSV with observed maps, factions, evidence types, classes, observation counts, installation counts, completion evidence, and first/last receive times.
- Clarified that the collected-quest CSV is not a complete catalog of every quest shipped in World of Warcraft; it contains the complete set observed by the Azeroth Questing research system.
- Bumped the development Companion version to `0.1.8-beta.3` and requires Azeroth Questing Server API `0.2.2` for heartbeat/online counts and collected-quest export.

This beta still needs testing on the player's Windows installation and against the live Azeroth Questing Server API `0.2.2`. A successful GitHub Actions build confirms compilation/packaging only and does not count as live Windows, server, or in-game testing. The WoW addon itself was not changed for this feature.

'''
if "## 0.1.8 Beta 3" not in changelog:
    changelog = changelog.replace("# Changelog\n\n", "# Changelog\n\n" + entry, 1)
changelog_path.write_text(changelog, encoding="utf-8")

# README
readme_path = Path("README.md")
readme = readme_path.read_text(encoding="utf-8")
readme = readme.replace("## Development version - v0.1.8-beta.2", "## Development version - v0.1.8-beta.3", 1)
viewer_bullet = "- Shows submitted research data in a privacy-preserving viewer with aggregate counts, recent anonymous observations, class evidence, and active addon/Companion version counts."
viewer_replacement = viewer_bullet + "\n- Sends a lightweight authenticated heartbeat every five minutes while server synchronization is enabled so the viewer can report Companions Online using a 10-minute activity window.\n- Exports every distinct quest ID collected by Azeroth Questing research to CSV from the Submitted Research Data viewer."
if viewer_bullet not in readme:
    raise SystemExit("Could not find README viewer bullet")
readme = readme.replace(viewer_bullet, viewer_replacement, 1)
readme_path.write_text(readme, encoding="utf-8")

# Privacy documentation
privacy_path = Path("PRIVACY.md")
privacy = privacy_path.read_text(encoding="utf-8")
anchor = "The companion also accesses GitHub to check for and download official Azeroth Questing Companion and Azeroth Questing addon updates."
privacy_addition = anchor + "\n\nWhile Azeroth Questing Server synchronization is enabled, the Companion sends a lightweight authenticated heartbeat every five minutes. The heartbeat updates the anonymous installation's last-seen time and may include Companion version, installed addon version, and Stable/Beta update channel so the research dashboard can report active-version counts and **Companions Online**. It does not include character name, account/BattleTag, Windows username, screenshots, chat, gameplay activity, or IP history. A Companion counts as online when the server has received a heartbeat within the last 10 minutes; this is an activity window rather than a permanent network connection."
if anchor not in privacy:
    raise SystemExit("Could not find privacy GitHub anchor")
privacy = privacy.replace(anchor, privacy_addition, 1)
viewer_anchor = "The Companion can use its per-installation bearer token to read a privacy-preserving research summary from the Azeroth Questing Server. The viewer may show aggregate observation and quest counts, this installation's own contribution count, recent anonymous structured observations, class-evidence summaries, and active addon/Companion version counts."
viewer_add = viewer_anchor + " The viewer can also export every distinct quest ID currently collected by the research system as CSV."
if viewer_anchor not in privacy:
    raise SystemExit("Could not find privacy viewer paragraph")
privacy = privacy.replace(viewer_anchor, viewer_add, 1)
privacy_path.write_text(privacy, encoding="utf-8")
