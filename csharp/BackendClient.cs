using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace OperationSteelTide;

public sealed class BackendClient : IDisposable
{
    internal const string InstanceEnvironmentVariable = "STEEL_TIDE_BACKEND_INSTANCE";
    internal const string OfflineEnvironmentVariable = "STEEL_TIDE_BACKEND_OFFLINE";

    private readonly HttpClient _http;
    private readonly string? _expectedInstance;
    private readonly bool _offline;

    public BackendClient(string baseUrl = "http://127.0.0.1:8787")
        : this(baseUrl, new HttpClientHandler())
    {
    }

    internal BackendClient(string baseUrl, HttpMessageHandler handler)
        : this(
            baseUrl,
            handler,
            Environment.GetEnvironmentVariable(InstanceEnvironmentVariable),
            string.Equals(
                Environment.GetEnvironmentVariable(OfflineEnvironmentVariable),
                "1",
                StringComparison.Ordinal))
    {
    }

    private BackendClient(
        string baseUrl,
        HttpMessageHandler handler,
        string? expectedInstance,
        bool offline)
    {
        _http = new HttpClient(handler, disposeHandler: true)
        {
            BaseAddress = new Uri(baseUrl),
            Timeout = TimeSpan.FromMilliseconds(1500)
        };
        _expectedInstance = expectedInstance;
        _offline = offline;
    }

    public async Task<StartPayload?> StartSessionAsync(
        string playerId,
        string missionId,
        CancellationToken cancellationToken = default)
    {
        if (!await CanWriteAsync(cancellationToken))
        {
            return null;
        }

        try
        {
            var request = new StartRequest(playerId, missionId, "normal");
            using var response = await _http.PostAsJsonAsync("/api/v1/sessions", request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }
            return await response.Content.ReadFromJsonAsync<StartPayload>(cancellationToken: cancellationToken);
        }
        catch (Exception exception) when (IsSafeOfflineFailure(exception))
        {
            return null;
        }
    }

    public async Task<bool> CompleteSessionAsync(
        string sessionId,
        CompleteRequest result,
        CancellationToken cancellationToken = default)
    {
        if (!await CanWriteAsync(cancellationToken))
        {
            return false;
        }

        try
        {
            using var response = await _http.PostAsJsonAsync(
                $"/api/v1/sessions/{Uri.EscapeDataString(sessionId)}/complete",
                result,
                cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception exception) when (IsSafeOfflineFailure(exception))
        {
            return false;
        }
    }

    public void Dispose() => _http.Dispose();

    private async Task<bool> CanWriteAsync(CancellationToken cancellationToken)
    {
        if (_offline)
        {
            return false;
        }
        if (string.IsNullOrEmpty(_expectedInstance))
        {
            return true;
        }

        try
        {
            using var response = await _http.GetAsync("/api/v1/health", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }
            var health = await response.Content.ReadFromJsonAsync<HealthPayload>(
                cancellationToken: cancellationToken);
            return health is not null
                && string.Equals(health.Status, "ok", StringComparison.Ordinal)
                && string.Equals(health.Service, "steel-tide-backend", StringComparison.Ordinal)
                && string.Equals(health.Instance, _expectedInstance, StringComparison.Ordinal);
        }
        catch (Exception exception) when (IsSafeOfflineFailure(exception))
        {
            return false;
        }
    }

    private static bool IsSafeOfflineFailure(Exception exception) =>
        exception is HttpRequestException
            or IOException
            or JsonException
            or NotSupportedException
            or OperationCanceledException;

    private sealed class HealthPayload
    {
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("service")]
        public string Service { get; set; } = string.Empty;

        [JsonPropertyName("instance")]
        public string Instance { get; set; } = string.Empty;
    }
}

public sealed record StartRequest(
    [property: JsonPropertyName("playerId")] string PlayerId,
    [property: JsonPropertyName("missionId")] string MissionId,
    [property: JsonPropertyName("difficulty")] string Difficulty);

public sealed record CompleteRequest(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("kills")] int Kills,
    [property: JsonPropertyName("headshots")] int Headshots,
    [property: JsonPropertyName("shotsFired")] int ShotsFired,
    [property: JsonPropertyName("shotsHit")] int ShotsHit,
    [property: JsonPropertyName("durationSeconds")] double DurationSeconds);

public sealed class StartPayload
{
    [JsonPropertyName("session")]
    public SessionPayload Session { get; set; } = new();

    [JsonPropertyName("mission")]
    public MissionPayload Mission { get; set; } = new();

    [JsonPropertyName("profile")]
    public ProfilePayload Profile { get; set; } = new();
}

public sealed class SessionPayload
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("seed")]
    public long Seed { get; set; }
}

public sealed class MissionPayload
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "steel-tide-terminal";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "Operation Steel Tide";

    [JsonPropertyName("objectives")]
    public List<string> Objectives { get; set; } = new();

    [JsonPropertyName("enemyCount")]
    public int EnemyCount { get; set; } = 9;

    [JsonPropertyName("spawnProtectionSeconds")]
    public int SpawnProtectionSeconds { get; set; } = 12;

    [JsonPropertyName("baseDetectionRange")]
    public int BaseDetectionRange { get; set; } = 34;

    [JsonPropertyName("reinforcementThreshold")]
    public int ReinforcementThreshold { get; set; } = 70;
}

public sealed class ProfilePayload
{
    [JsonPropertyName("level")]
    public int Level { get; set; } = 1;

    [JsonPropertyName("credits")]
    public int Credits { get; set; } = 2500;
}
