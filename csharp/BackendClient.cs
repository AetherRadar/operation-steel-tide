using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace OperationSteelTide;

public sealed class BackendClient : IDisposable
{
    private readonly HttpClient _http;

    public BackendClient(string baseUrl = "http://127.0.0.1:8787")
    {
        _http = new HttpClient
        {
            BaseAddress = new Uri(baseUrl),
            Timeout = TimeSpan.FromMilliseconds(1500)
        };
    }

    public async Task<StartPayload?> StartSessionAsync(
        string playerId,
        string missionId,
        CancellationToken cancellationToken = default)
    {
        var request = new StartRequest(playerId, missionId, "normal");
        using var response = await _http.PostAsJsonAsync("/api/v1/sessions", request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }
        return await response.Content.ReadFromJsonAsync<StartPayload>(cancellationToken: cancellationToken);
    }

    public async Task<bool> CompleteSessionAsync(
        string sessionId,
        CompleteRequest result,
        CancellationToken cancellationToken = default)
    {
        using var response = await _http.PostAsJsonAsync(
            $"/api/v1/sessions/{Uri.EscapeDataString(sessionId)}/complete",
            result,
            cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public void Dispose() => _http.Dispose();
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
