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

        var files = Directory
            .EnumerateFiles(AppPaths.Outbox, "*.lua", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (files.Length == 0)
        {
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
            var observations = ExtractWireObservations(text);

            if (observations.Count > 0)
            {
                for (var offset = 0; offset < observations.Count; offset += BatchSize)
                {
                    var batch = observations.Skip(offset).Take(BatchSize).ToArray();
                    var addonVersion = batch.LastOrDefault()?.AddonVersion;
                    var response = await SendBatchWithRegistrationRetryAsync(
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
                // v0.2.31 and older snapshots do not contain AQO1 records. Keep a
                // raw, SHA-256 checked compatibility path so those already-queued
                // observations are not discarded during the sync upgrade.
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
            // exists on Service01. Create a new anonymous installation identity
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
                $"Service01 registration failed ({(int)response.StatusCode} {response.ReasonPhrase}): {TrimForError(body)}");
        }

        var registration = JsonSerializer.Deserialize<RegistrationResponse>(body, _jsonOptions)
            ?? throw new InvalidDataException("Service01 returned an empty registration response.");

        if (registration.InstallationId == Guid.Empty || string.IsNullOrWhiteSpace(registration.Token))
        {
            throw new InvalidDataException("Service01 registration response did not contain a valid installation ID/token.");
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

    private async Task<BatchResponse> ReadBatchResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Service01 observation upload failed ({(int)response.StatusCode} {response.ReasonPhrase}): {TrimForError(body)}");
        }

        return JsonSerializer.Deserialize<BatchResponse>(body, _jsonOptions)
            ?? throw new InvalidDataException("Service01 returned an empty observation response.");
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
            $"Service01 {operation} failed ({(int)response.StatusCode} {response.ReasonPhrase}): {TrimForError(body)}");
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
            ? "http://10.0.10.246:8766"
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
            if (parts.Length != 13 || parts[0] != "AQO1")
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

            observations[parts[1]] = new QuestObservationRequest
            {
                Key = parts[1],
                Source = parts[2],
                QuestId = questId,
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
        }

        return observations.Values.OrderBy(item => item.ObservedAt).ThenBy(item => item.Key, StringComparer.Ordinal).ToList();
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
        _httpClient.Dispose();
    }

    [GeneratedRegex(@"AQO1\|[A-Za-z0-9._:|\-]+", RegexOptions.CultureInvariant)]
    private static partial Regex WireRecordRegex();

    [GeneratedRegex(@"^[A-Z]+$", RegexOptions.CultureInvariant)]
    private static partial Regex ClassFileRegex();

    [GeneratedRegex(@"^[A-Za-z0-9._:-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex ObservationKeyRegex();

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
