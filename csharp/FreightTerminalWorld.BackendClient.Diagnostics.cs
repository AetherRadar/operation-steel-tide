using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using ProcessEnvironment = System.Environment;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private const string BackendClientDiagnosticBaseUrl = "http://backend-client.invalid";
    private const string BackendClientDiagnosticInstance = "backend-client-diagnostic-instance";
    private readonly bool _backendClientDiagnosticStartupIsolated =
        IsolateBackendClientDiagnosticStartup();

    private async void ValidateBackendClient()
    {
        var unsetDirectPost = false;
        var offlineZeroRequests = false;
        var mismatchHealthOnly = false;
        var matchHealthThenPost = false;
        var networkFailureSafe = false;
        var invalidJsonSafe = false;
        var exceptionType = "none";
        var startupOffline = _backendClientDiagnosticStartupIsolated
            && string.Equals(
                ProcessEnvironment.GetEnvironmentVariable(BackendClient.OfflineEnvironmentVariable),
                "1",
                StringComparison.Ordinal);
        var originalInstance = ProcessEnvironment.GetEnvironmentVariable(BackendClient.InstanceEnvironmentVariable);
        var originalOffline = ProcessEnvironment.GetEnvironmentVariable(BackendClient.OfflineEnvironmentVariable);

        try
        {
            var completion = new CompleteRequest(true, 4, 2, 20, 9, 45.0);

            var unsetHandler = new BackendClientDiagnosticHandler(BackendClientDiagnosticInstance);
            using (var unsetClient = CreateDiagnosticBackendClient(
                unsetHandler,
                expectedInstance: null,
                offline: null))
            {
                var start = await unsetClient.StartSessionAsync("unset-player", "steel-tide-terminal");
                var completed = await unsetClient.CompleteSessionAsync("diagnostic-session", completion);
                unsetDirectPost = start?.Session.Id == "diagnostic-session"
                    && completed
                    && HasRequestSequence(
                        unsetHandler,
                        "POST /api/v1/sessions",
                        "POST /api/v1/sessions/diagnostic-session/complete");
            }

            var offlineHandler = new BackendClientDiagnosticHandler(BackendClientDiagnosticInstance);
            using (var offlineClient = CreateDiagnosticBackendClient(offlineHandler, offline: true))
            {
                var start = await offlineClient.StartSessionAsync("offline-player", "steel-tide-terminal");
                var completed = await offlineClient.CompleteSessionAsync("offline-session", completion);
                offlineZeroRequests = start is null && !completed && offlineHandler.Requests.Count == 0;
            }

            var mismatchHandler = new BackendClientDiagnosticHandler("different-backend-instance");
            using (var mismatchClient = CreateDiagnosticBackendClient(mismatchHandler, offline: false))
            {
                var start = await mismatchClient.StartSessionAsync("mismatch-player", "steel-tide-terminal");
                var completed = await mismatchClient.CompleteSessionAsync("mismatch-session", completion);
                mismatchHealthOnly = start is null
                    && !completed
                    && HasRequestSequence(
                        mismatchHandler,
                        "GET /api/v1/health",
                        "GET /api/v1/health");
            }

            var matchHandler = new BackendClientDiagnosticHandler(BackendClientDiagnosticInstance);
            using (var matchClient = CreateDiagnosticBackendClient(matchHandler, offline: false))
            {
                var start = await matchClient.StartSessionAsync("match-player", "steel-tide-terminal");
                var completed = await matchClient.CompleteSessionAsync("diagnostic-session", completion);
                matchHealthThenPost = start?.Session.Id == "diagnostic-session"
                    && completed
                    && HasRequestSequence(
                        matchHandler,
                        "GET /api/v1/health",
                        "POST /api/v1/sessions",
                        "GET /api/v1/health",
                        "POST /api/v1/sessions/diagnostic-session/complete");
            }

            var networkHandler = new BackendClientDiagnosticHandler(
                BackendClientDiagnosticInstance,
                throwNetworkFailure: true);
            using (var networkClient = CreateDiagnosticBackendClient(networkHandler, offline: false))
            {
                var start = await networkClient.StartSessionAsync("network-player", "steel-tide-terminal");
                var completed = await networkClient.CompleteSessionAsync("network-session", completion);
                networkFailureSafe = start is null
                    && !completed
                    && HasRequestSequence(
                        networkHandler,
                        "GET /api/v1/health",
                        "GET /api/v1/health");
            }

            var invalidJsonHandler = new BackendClientDiagnosticHandler(
                BackendClientDiagnosticInstance,
                invalidHealthJson: true);
            using (var invalidJsonClient = CreateDiagnosticBackendClient(invalidJsonHandler, offline: false))
            {
                var start = await invalidJsonClient.StartSessionAsync("json-player", "steel-tide-terminal");
                var completed = await invalidJsonClient.CompleteSessionAsync("json-session", completion);
                invalidJsonSafe = start is null
                    && !completed
                    && HasRequestSequence(
                        invalidJsonHandler,
                        "GET /api/v1/health",
                        "GET /api/v1/health");
            }
        }
        catch (Exception exception)
        {
            exceptionType = exception.GetType().Name;
        }
        finally
        {
            ProcessEnvironment.SetEnvironmentVariable(BackendClient.InstanceEnvironmentVariable, originalInstance);
            ProcessEnvironment.SetEnvironmentVariable(BackendClient.OfflineEnvironmentVariable, originalOffline);
        }

        var valid = startupOffline
            && unsetDirectPost
            && offlineZeroRequests
            && mismatchHealthOnly
            && matchHealthThenPost
            && networkFailureSafe
            && invalidJsonSafe
            && exceptionType == "none";
        GD.Print(
            $"BACKEND_CLIENT_CHECK valid={valid} startup_offline={startupOffline} "
            + $"unset_direct={unsetDirectPost} "
            + $"offline_zero={offlineZeroRequests} "
            + $"mismatch_health_only={mismatchHealthOnly} match_health_post={matchHealthThenPost} "
            + $"network_safe={networkFailureSafe} json_safe={invalidJsonSafe} exception={exceptionType}");
        GD.Print($"BACKEND_CLIENT_PASS valid={valid}");
        QuitDiagnosticAfterSceneCleanup(valid ? 0 : 2);
    }

    private static bool IsolateBackendClientDiagnosticStartup()
    {
        if (!Array.Exists(
                OS.GetCmdlineUserArgs(),
                argument => string.Equals(argument, "--validate-backend-client", StringComparison.Ordinal)))
        {
            return false;
        }
        ProcessEnvironment.SetEnvironmentVariable(BackendClient.OfflineEnvironmentVariable, "1");
        return true;
    }

    private static BackendClient CreateDiagnosticBackendClient(
        HttpMessageHandler handler,
        string? expectedInstance = BackendClientDiagnosticInstance,
        bool? offline = false)
    {
        ProcessEnvironment.SetEnvironmentVariable(BackendClient.InstanceEnvironmentVariable, expectedInstance);
        ProcessEnvironment.SetEnvironmentVariable(
            BackendClient.OfflineEnvironmentVariable,
            offline.HasValue ? (offline.Value ? "1" : "0") : null);
        return new BackendClient(BackendClientDiagnosticBaseUrl, handler);
    }

    private static bool HasRequestSequence(
        BackendClientDiagnosticHandler handler,
        params string[] expected)
    {
        if (handler.Requests.Count != expected.Length)
        {
            return false;
        }
        for (var index = 0; index < expected.Length; index++)
        {
            if (!string.Equals(handler.Requests[index], expected[index], StringComparison.Ordinal))
            {
                return false;
            }
        }
        return true;
    }

    private sealed class BackendClientDiagnosticHandler : HttpMessageHandler
    {
        private readonly string _healthInstance;
        private readonly bool _throwNetworkFailure;
        private readonly bool _invalidHealthJson;

        public BackendClientDiagnosticHandler(
            string healthInstance,
            bool throwNetworkFailure = false,
            bool invalidHealthJson = false)
        {
            _healthInstance = healthInstance;
            _throwNetworkFailure = throwNetworkFailure;
            _invalidHealthJson = invalidHealthJson;
        }

        public List<string> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            Requests.Add($"{request.Method.Method} {path}");
            if (_throwNetworkFailure)
            {
                return Task.FromException<HttpResponseMessage>(
                    new HttpRequestException("diagnostic network failure"));
            }
            if (request.Method == HttpMethod.Get && path == "/api/v1/health")
            {
                return Task.FromResult(JsonResponse(
                    HttpStatusCode.OK,
                    _invalidHealthJson
                        ? "{invalid-json"
                        : JsonSerializer.Serialize(new
                        {
                            status = "ok",
                            service = "steel-tide-backend",
                            version = "1.0.0",
                            instance = _healthInstance
                        })));
            }
            if (request.Method == HttpMethod.Post && path == "/api/v1/sessions")
            {
                var body = "{\"session\":{\"id\":\"diagnostic-session\",\"seed\":7},"
                    + "\"mission\":{},\"profile\":{}}";
                return Task.FromResult(JsonResponse(HttpStatusCode.Created, body));
            }
            if (request.Method == HttpMethod.Post
                && path.StartsWith("/api/v1/sessions/", StringComparison.Ordinal)
                && path.EndsWith("/complete", StringComparison.Ordinal))
            {
                return Task.FromResult(JsonResponse(HttpStatusCode.OK, "{}"));
            }
            return Task.FromResult(JsonResponse(HttpStatusCode.NotFound, "{}"));
        }

        private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string body) =>
            new(statusCode)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
    }
}
