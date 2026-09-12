using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace AzerothQuesting.Companion;

internal sealed record ServiceSyncResult(
    int UploadedSnapshots,
    int UploadedObservations,
    int DuplicateObservations,
    int RemainingSnapshots,
    string Status);

internal sealed partial class Service01Client : IDisposable
{
    private const int BatchSize = 500;
    private readonly HttpClient _httpClient;
    private readonly CompanionSettings _settings;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);
    private readonly SemaphoreSlim _heartbeatGate = new(1, 1);
    private System.Threading.Timer? _heartbeatTimer;
    private string? _heartbeatCompanionVersion;
    private bool _disposed;

    public Service01Client(CompanionSettings settings)
    {
        _settings = settings;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15),
        };
    }

    public string BaseUrl => NormalizeBaseUrl(_settings.ServiceBaseUrl);

    public async Task<ServiceSyncResult> SyncOutboxAsync(
        string companionVersion,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        AppPaths.EnsureCreated();
        ConfigureHeartbeat(companionVersion);
        await SendHeartbeatAsync(cancellationToken);

        var files = Directory
            .EnumerateFiles(AppPaths.Outbox, "*.lua", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (files.Length == 0)
        {
            await CheckStatusAsync(cancellationToken);
            return new ServiceSyncResult(0, 0, 0, 0, "Connected - nothing pending");
        }

        await EnsureRegisteredAsync(companionVersion, cancellationToken);

        var uploadedSnapshots = 0;
        var uploadedObservations = 0;
        var duplicates = 0;

        foreach (var path in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var text = await File.ReadAllTextAsync(path, cancellationToken);
            var questObservations = ExtractWireObservations(text);
            var mapObservations = ExtractMapWireObservations(text);

            if (questObservations.Count > 0 || mapObservations.Count > 0)
            {
                for (var offset = 0; offset < questObservations.Count; offset += BatchSize)
                {
                    var batch = questObservations.Skip(offset).Take(BatchSize).ToArray();
                    var addonVersion = batch.LastOrDefault()?.AddonVersion;
                    var response = await SendBatchWithRegistrationRetryAsync(
                        batch,
                        addonVersion,
                        companionVersion,
                        cancellationToken);
                    uploadedObservations += response.Accepted;
                    duplicates += response.Duplicates;
                }

                for (var offset = 0; offset < mapObservations.Count; offset += BatchSize)
                {
                    var batch = mapObservations.Skip(offset).Take(BatchSize).ToArray();
                    var addonVersion = batch.LastOrDefault()?.AddonVersion;
                    var response = await SendMapBatchWithRegistrationRetryAsync(
                        batch,
                        addonVersion,
                        companionVersion,
                        cancellationToken);
                    uploadedObservations += response.Accepted;
                    duplicates += response.Duplicates;
                }
            }
            else
            {
                // Older snapshots may not contain structured AQO/AQM records. Keep a
                // raw, SHA-256 checked compatibility path so already-queued data is
                // not discarded during research protocol upgrades.
                await SendRawSnapshotWithRegistrationRetryAsync(
                    text,
                    companionVersion,
                    cancellationToken);
            }

            File.Delete(path);
            uploadedSnapshots++;
        }

        var remaining = Directory.Exists(AppPaths.Outbox)
            ? Directory.EnumerateFiles(AppPaths.Outbox, "*.lua", SearchOption.TopDirectoryOnly).Count()
            : 0;

        var status = uploadedObservations > 0
            ? $"Connected - uploaded {uploadedObservations} observation{(uploadedObservations == 1 ? string.Empty : "s")}" 
            : "Connected - snapshots synchronized";

        return new ServiceSyncResult(
            uploadedSnapshots,
            uploadedObservations,
            duplicates,
            remaining,
            status);
    }

    public async Task CheckStatusAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        using var request = new HttpRequestMessage(HttpMethod.Get, BaseUrl + "/api/v1/status");
        request.Headers.UserAgent.ParseAdd("AzerothQuestingCompanion");
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, "status check", cancellationToken);
    }

    public async Task<ResearchDashboardData> GetResearchDashboardAsync(
        string companionVersion,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ConfigureHeartbeat(companionVersion);
        await SendHeartbeatAsync(cancellationToken);
        await EnsureRegisteredAsync(companionVersion, cancellationToken);

        using var response = await SendAuthorizedGetAsync("/api/v1/research/dashboard", cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            ClearRegistration();
            await EnsureRegisteredAsync(companionVersion, cancellationToken);
            using var retry = await SendAuthorizedGetAsync("/api/v1/research/dashboard", cancellationToken);
            return await ReadResearchDashboardAsync(retry, cancellationToken);
        }

        return await ReadResearchDashboardAsync(response, cancellationToken);
    }

    public async Task<string> ExportCollectedQuestsCsvAsync(
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
        CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, BaseUrl + relativePath);
        request.Headers.UserAgent.ParseAdd("AzerothQuestingCompanion");
        if (!string.IsNullOrWhiteSpace(_settings.InstallationToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.InstallationToken);
        }

        try
        {
            return await _httpClient.SendAsync(request, cancellationToken);
        }
        finally
        {
            request.Dispose();
        }
    }

    private async Task<ResearchDashboardData> ReadResearchDashboardAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Azeroth Questing server data request failed ({(int)response.StatusCode} {response.ReasonPhrase}): {TrimForError(body)}");
        }

        return JsonSerializer.Deserialize<ResearchDashboardData>(body, _jsonOptions)
            ?? throw new InvalidDataException("Azeroth Questing server returned an empty research dashboard response.");
    }

    private async Task EnsureRegisteredAsync(string companionVersion, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_settings.InstallationToken))
        {
            return;
        }

        if (!Guid.TryParse(_settings.ClientInstanceId, out var clientInstanceId))
        {
            clientInstanceId = Guid.NewGuid();
            _settings.ClientInstanceId = clientInstanceId.ToString("D");
            SettingsService.Save(_settings);
        }

        var request = new RegistrationRequest
        {
            ClientInstanceId = clientInstanceId,
            CompanionVersion = companionVersion,
        };

        using var response = await PostJsonAsync(
            "/api/v1/installations/register",
            request,
            bearerToken: null,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            // The local token was lost but this client-instance ID already
            // exists on the server. Create a new anonymous installation identity
            // rather than needing a shared recovery secret.
            clientInstanceId = Guid.NewGuid();
            _settings.ClientInstanceId = clientInstanceId.ToString("D");
            request.ClientInstanceId = clientInstanceId;
            SettingsService.Save(_settings);

            using var retry = await PostJsonAsync(
                "/api/v1/installations/register",
                request,
                bearerToken: null,
                cancellationToken);
            await AcceptRegistrationResponseAsync(retry, cancellationToken);
            return;
        }

        await AcceptRegistrationResponseAsync(response, cancellationToken);
    }

    private async Task AcceptRegistrationResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Azeroth Questing server registration failed ({(int)response.StatusCode} {response.ReasonPhrase}): {TrimForError(body)}");
        }

        var registration = JsonSerializer.Deserialize<RegistrationResponse>(body, _jsonOptions)
            ?? throw new InvalidDataException("Azeroth Questing server returned an empty registration response.");

        if (registration.InstallationId == Guid.Empty || string.IsNullOrWhiteSpace(registration.Token))
        {
            throw new InvalidDataException("Azeroth Questing server registration response did not contain a valid installation ID/token.");
        }

        _settings.InstallationId = registration.InstallationId.ToString("D");
        _settings.InstallationToken = registration.Token;
        SettingsService.Save(_settings);
    }

    private async Task<BatchResponse> SendBatchWithRegistrationRetryAsync(
        IReadOnlyCollection<QuestObservationRequest> observations,
        string? addonVersion,
        string companionVersion,
        CancellationToken cancellationToken)
    {
        var request = new BatchRequest
        {
            AddonVersion = addonVersion,
            CompanionVersion = companionVersion,
            Observations = observations.ToArray(),
        };

        using var response = await PostJsonAsync(
            "/api/v1/observations/batch",
            request,
            _settings.InstallationToken,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            ClearRegistration();
            await EnsureRegisteredAsync(companionVersion, cancellationToken);
            using var retry = await PostJsonAsync(
                "/api/v1/observations/batch",
                request,
                _settings.InstallationToken,
                cancellationToken);
            return await ReadBatchResponseAsync(retry, cancellationToken);
        }

        return await ReadBatchResponseAsync(response, cancellationToken);
    }

    private async Task<BatchResponse> SendMapBatchWithRegistrationRetryAsync(
        IReadOnlyCollection<MapObservationRequest> observations,
        string? addonVersion,
        string companionVersion,
        CancellationToken cancellationToken)
    {
        var request = new MapBatchRequest
        {
            AddonVersion = addonVersion,
            CompanionVersion = companionVersion,
            Observations = observations.ToArray(),
        };

        using var response = await PostJsonAsync(
            "/api/v1/maps/batch",
            request,
            _settings.InstallationToken,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            ClearRegistration();
            await EnsureRegisteredAsync(companionVersion, cancellationToken);
            using var retry = await PostJsonAsync(
                "/api/v1/maps/batch",
                request,
                _settings.InstallationToken,
                cancellationToken);
            return await ReadBatchResponseAsync(retry, cancellationToken);
        }

        return await ReadBatchResponseAsync(response, cancellationToken);
    }

    private async Task<BatchResponse> ReadBatchResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Azeroth Questing server observation upload failed ({(int)response.StatusCode} {response.ReasonPhrase}): {TrimForError(body)}");
        }

        return JsonSerializer.Deserialize<BatchResponse>(body, _jsonOptions)
            ?? throw new InvalidDataException("Azeroth Questing server returned an empty observation response.");
    }

    private async Task SendRawSnapshotWithRegistrationRetryAsync(
        string payload,
        string companionVersion,
        CancellationToken cancellationToken)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
        var request = new RawSnapshotRequest
        {
            ContentSha256 = hash,
            SourceSchema = "azerothquesting-savedvariables-v1",
            CompanionVersion = companionVersion,
            Payload = payload,
        };

        using var response = await PostJsonAsync(
            "/api/v1/observations",
            request,
            _settings.InstallationToken,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            ClearRegistration();
            await EnsureRegisteredAsync(companionVersion, cancellationToken);
            using var retry = await PostJsonAsync(
                "/api/v1/observations",
                request,
                _settings.InstallationToken,
                cancellationToken);
            await EnsureSuccessAsync(retry, "raw snapshot upload", cancellationToken);
            return;
        }

        await EnsureSuccessAsync(response, "raw snapshot upload", cancellationToken);
    }

    private async Task<HttpResponseMessage> PostJsonAsync<T>(
        string relativePath,
        T value,
        string? bearerToken,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(value, _jsonOptions);
        var request = new HttpRequestMessage(HttpMethod.Post, BaseUrl + relativePath)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        request.Headers.UserAgent.ParseAdd("AzerothQuestingCompanion");
        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }

        try
        {
            return await _httpClient.SendAsync(request, cancellationToken);
        }
        finally
        {
            request.Dispose();
        }
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        string operation,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new HttpRequestException(
            $"Azeroth Questing server {operation} failed ({(int)response.StatusCode} {response.ReasonPhrase}): {TrimForError(body)}");
    }

    private void ClearRegistration()
    {
        _settings.InstallationId = null;
        _settings.InstallationToken = null;
        SettingsService.Save(_settings);
    }

    private static string NormalizeBaseUrl(string? value)
    {
        value = string.IsNullOrWhiteSpace(value)
            ? SettingsService.DefaultServiceBaseUrl
            : value.Trim();
        return value.TrimEnd('/');
    }

    private static List<QuestObservationRequest> ExtractWireObservations(string snapshot)
    {
        var observations = new Dictionary<string, QuestObservationRequest>(StringComparer.Ordinal);

        foreach (Match match in WireRecordRegex().Matches(snapshot))
        {
            var wire = match.Value;
            var parts = wire.Split('|');
            var isAqo1 = parts.Length == 13 && parts[0] == "AQO1";
            var isAqo2 = parts.Length == 14 && parts[0] == "AQO2";
            if (!isAqo1 && !isAqo2)
            {
                continue;
            }

            if (!int.TryParse(parts[3], out var questId) || questId <= 0
                || !int.TryParse(parts[4], out var mapId) || mapId <= 0
                || !int.TryParse(parts[7], out var classId) || classId < 0 || classId > 30
                || !int.TryParse(parts[9], out var level) || level < 0 || level > 255
                || !long.TryParse(parts[11], out var observedAt) || observedAt <= 0)
            {
                continue;
            }

            if (parts[2] is not ("local" or "peer")
                || parts[5] is not ("seen" or "available" or "offered" or "accepted" or "active" or "turnedIn")
                || parts[6] is not ("Alliance" or "Horde" or "Neutral")
                || (parts[10] != "0" && parts[10] != "1")
                || !ClassFileRegex().IsMatch(parts[8])
                || !ObservationKeyRegex().IsMatch(parts[1]))
            {
                continue;
            }

            DateTimeOffset observedTime;
            try
            {
                observedTime = DateTimeOffset.FromUnixTimeSeconds(observedAt);
            }
            catch (ArgumentOutOfRangeException)
            {
                continue;
            }

            string? questName = null;
            if (isAqo2 && !TryDecodeQuestName(parts[13], out questName))
            {
                continue;
            }

            var candidate = new QuestObservationRequest
            {
                Key = parts[1],
                Source = parts[2],
                QuestId = questId,
                QuestName = questName,
                MapId = mapId,
                Evidence = parts[5],
                Faction = parts[6],
                ClassId = classId,
                ClassFile = parts[8],
                Level = level,
                Completed = parts[10] == "1",
                ObservedAt = observedTime,
                AddonVersion = parts[12],
            };

            if (!observations.TryGetValue(parts[1], out var existing)
                || (!string.IsNullOrWhiteSpace(candidate.QuestName)
                    && string.IsNullOrWhiteSpace(existing.QuestName)))
            {
                observations[parts[1]] = candidate;
            }
        }

        return observations.Values.OrderBy(item => item.ObservedAt).ThenBy(item => item.Key, StringComparer.Ordinal).ToList();
    }

    private static List<MapObservationRequest> ExtractMapWireObservations(string snapshot)
    {
        var observations = new Dictionary<string, MapObservationRequest>(StringComparer.Ordinal);

        foreach (Match match in MapWireRecordRegex().Matches(snapshot))
        {
            var parts = match.Value.Split('|');
            if (parts.Length != 17 || parts[0] != "AQM1")
            {
                continue;
            }

            if (!int.TryParse(parts[2], out var mapId) || mapId <= 0
                || !int.TryParse(parts[4], out var parentMapId) || parentMapId < 0
                || !int.TryParse(parts[5], out var mapType) || mapType < 0 || mapType > 255
                || !int.TryParse(parts[8], out var difficultyId) || difficultyId < 0
                || !int.TryParse(parts[9], out var instanceId) || instanceId < 0
                || !int.TryParse(parts[11], out var classId) || classId < 0 || classId > 30
                || !int.TryParse(parts[13], out var level) || level < 0 || level > 255
                || !long.TryParse(parts[15], out var observedAt) || observedAt <= 0)
            {
                continue;
            }

            if (parts[10] is not ("Alliance" or "Horde" or "Neutral")
                || !ClassFileRegex().IsMatch(parts[12])
                || !ObservationKeyRegex().IsMatch(parts[1])
                || !InstanceTypeRegex().IsMatch(parts[7]))
            {
                continue;
            }

            DateTimeOffset observedTime;
            try
            {
                observedTime = DateTimeOffset.FromUnixTimeSeconds(observedAt);
            }
            catch (ArgumentOutOfRangeException)
            {
                continue;
            }

            if (!TryDecodeQuestName(parts[3], out var mapName)
                || !TryDecodeQuestName(parts[6], out var instanceName)
                || !TryDecodeQuestName(parts[14], out var phase))
            {
                continue;
            }

            observations[parts[1]] = new MapObservationRequest
            {
                Key = parts[1],
                MapId = mapId,
                MapName = mapName,
                ParentMapId = parentMapId,
                MapType = mapType,
                InstanceName = instanceName,
                InstanceType = parts[7],
                DifficultyId = difficultyId,
                InstanceId = instanceId,
                Faction = parts[10],
                ClassId = classId,
                ClassFile = parts[12],
                Level = level,
                Phase = phase,
                ObservedAt = observedTime,
                AddonVersion = parts[16],
            };
        }

        return observations.Values
            .OrderBy(item => item.ObservedAt)
            .ThenBy(item => item.Key, StringComparer.Ordinal)
            .ToList();
    }

    private static bool TryDecodeQuestName(string value, out string? questName)
    {
        questName = null;
        if (string.IsNullOrEmpty(value))
        {
            return true;
        }
        if ((value.Length % 2) != 0 || !HexStringRegex().IsMatch(value))
        {
            return false;
        }

        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromHexString(value));
            if (decoded.Length > 512)
            {
                return false;
            }
            questName = string.IsNullOrWhiteSpace(decoded) ? null : decoded;
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string TrimForError(string value)
    {
        value = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return value.Length <= 300 ? value : value[..300] + "...";
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
        _heartbeatTimer?.Dispose();
        _heartbeatGate.Dispose();
        _httpClient.Dispose();
    }

    [GeneratedRegex(@"AQO[12]\|[A-Za-z0-9._:|\-]+", RegexOptions.CultureInvariant)]
    private static partial Regex WireRecordRegex();

    [GeneratedRegex(@"AQM1\|[A-Za-z0-9._:|\-]+", RegexOptions.CultureInvariant)]
    private static partial Regex MapWireRecordRegex();

    [GeneratedRegex(@"^[A-Za-z_]+$", RegexOptions.CultureInvariant)]
    private static partial Regex InstanceTypeRegex();

    [GeneratedRegex(@"^[A-Z]+$", RegexOptions.CultureInvariant)]
    private static partial Regex ClassFileRegex();

    [GeneratedRegex(@"^[A-Za-z0-9._:-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex ObservationKeyRegex();

    [GeneratedRegex(@"^[0-9A-Fa-f]+$", RegexOptions.CultureInvariant)]
    private static partial Regex HexStringRegex();

    private sealed class HeartbeatRequest
    {
        [JsonPropertyName("companion_version")]
        public string? CompanionVersion { get; set; }

        [JsonPropertyName("addon_version")]
        public string? AddonVersion { get; set; }

        [JsonPropertyName("update_channel")]
        public string? UpdateChannel { get; set; }
    }

    private sealed class RegistrationRequest
    {
        [JsonPropertyName("client_instance_id")]
        public Guid ClientInstanceId { get; set; }

        [JsonPropertyName("companion_version")]
        public string? CompanionVersion { get; set; }

        [JsonPropertyName("addon_version")]
        public string? AddonVersion { get; set; }
    }

    private sealed class RegistrationResponse
    {
        [JsonPropertyName("installation_id")]
        public Guid InstallationId { get; set; }

        [JsonPropertyName("token")]
        public string Token { get; set; } = string.Empty;
    }

    private sealed class RawSnapshotRequest
    {
        [JsonPropertyName("content_sha256")]
        public string ContentSha256 { get; set; } = string.Empty;

        [JsonPropertyName("source_schema")]
        public string SourceSchema { get; set; } = string.Empty;

        [JsonPropertyName("companion_version")]
        public string? CompanionVersion { get; set; }

        [JsonPropertyName("payload")]
        public string Payload { get; set; } = string.Empty;
    }

    private sealed class BatchRequest
    {
        [JsonPropertyName("addon_version")]
        public string? AddonVersion { get; set; }

        [JsonPropertyName("companion_version")]
        public string? CompanionVersion { get; set; }

        [JsonPropertyName("observations")]
        public QuestObservationRequest[] Observations { get; set; } = [];
    }

    private sealed class QuestObservationRequest
    {
        [JsonPropertyName("key")]
        public string Key { get; set; } = string.Empty;

        [JsonPropertyName("source")]
        public string Source { get; set; } = "local";

        [JsonPropertyName("quest_id")]
        public int QuestId { get; set; }

        [JsonPropertyName("quest_name")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? QuestName { get; set; }

        [JsonPropertyName("map_id")]
        public int MapId { get; set; }

        [JsonPropertyName("evidence")]
        public string Evidence { get; set; } = string.Empty;

        [JsonPropertyName("faction")]
        public string Faction { get; set; } = string.Empty;

        [JsonPropertyName("class_id")]
        public int ClassId { get; set; }

        [JsonPropertyName("class_file")]
        public string ClassFile { get; set; } = string.Empty;

        [JsonPropertyName("level")]
        public int Level { get; set; }

        [JsonPropertyName("completed")]
        public bool Completed { get; set; }

        [JsonPropertyName("observed_at")]
        public DateTimeOffset ObservedAt { get; set; }

        [JsonIgnore]
        public string AddonVersion { get; set; } = string.Empty;
    }

    private sealed class MapBatchRequest
    {
        [JsonPropertyName("addon_version")]
        public string? AddonVersion { get; set; }

        [JsonPropertyName("companion_version")]
        public string? CompanionVersion { get; set; }

        [JsonPropertyName("observations")]
        public MapObservationRequest[] Observations { get; set; } = [];
    }

    private sealed class MapObservationRequest
    {
        [JsonPropertyName("key")]
        public string Key { get; set; } = string.Empty;

        [JsonPropertyName("map_id")]
        public int MapId { get; set; }

        [JsonPropertyName("map_name")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? MapName { get; set; }

        [JsonPropertyName("parent_map_id")]
        public int ParentMapId { get; set; }

        [JsonPropertyName("map_type")]
        public int MapType { get; set; }

        [JsonPropertyName("instance_name")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? InstanceName { get; set; }

        [JsonPropertyName("instance_type")]
        public string InstanceType { get; set; } = "none";

        [JsonPropertyName("difficulty_id")]
        public int DifficultyId { get; set; }

        [JsonPropertyName("instance_id")]
        public int InstanceId { get; set; }

        [JsonPropertyName("faction")]
        public string Faction { get; set; } = string.Empty;

        [JsonPropertyName("class_id")]
        public int ClassId { get; set; }

        [JsonPropertyName("class_file")]
        public string ClassFile { get; set; } = string.Empty;

        [JsonPropertyName("level")]
        public int Level { get; set; }

        [JsonPropertyName("phase")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Phase { get; set; }

        [JsonPropertyName("observed_at")]
        public DateTimeOffset ObservedAt { get; set; }

        [JsonIgnore]
        public string AddonVersion { get; set; } = string.Empty;
    }

    private sealed class BatchResponse
    {
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("accepted")]
        public int Accepted { get; set; }

        [JsonPropertyName("duplicates")]
        public int Duplicates { get; set; }
    }
}
