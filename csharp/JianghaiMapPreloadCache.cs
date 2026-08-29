using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Godot;

namespace OperationSteelTide;

internal enum JianghaiMapPreloadState
{
    Idle,
    Loading,
    Ready,
    Failed
}

internal sealed record JianghaiMapPreloadSnapshot(
    JianghaiMapPreloadState State,
    int RequestCount,
    int PollCount,
    float Progress,
    long ElapsedMilliseconds,
    string Error);

internal readonly record struct JianghaiPackedSceneAcquisition(
    bool UsedThreadedRequest,
    bool ReadyBeforeAcquire,
    long AcquireMilliseconds);

/// <summary>
/// Keeps the large Jianghai scene warm across the operations-office scene reload.
/// ResourceLoader owns background IO; this class only polls and retains the ready PackedScene.
/// </summary>
internal static class JianghaiMapPreloadCache
{
    private const int DefaultTimeoutMilliseconds = 60_000;
    private static readonly Stopwatch RequestClock = new();
    private static PackedScene? _packedScene;
    private static JianghaiMapPreloadState _state;
    private static int _requestCount;
    private static int _pollCount;
    private static float _progress;
    private static long _lastElapsedMilliseconds;
    private static bool _releaseWhenLoaded;
    private static string _error = "none";

    public static JianghaiMapPreloadSnapshot Snapshot
        => new(
            _state,
            _requestCount,
            _pollCount,
            _progress,
            RequestClock.IsRunning ? RequestClock.ElapsedMilliseconds : _lastElapsedMilliseconds,
            _error);

    public static bool Request()
    {
        if (_state == JianghaiMapPreloadState.Ready
            && _packedScene is not null
            && GodotObject.IsInstanceValid(_packedScene))
        {
            return true;
        }
        if (_state == JianghaiMapPreloadState.Loading)
        {
            _releaseWhenLoaded = false;
            return true;
        }

        _packedScene = null;
        _error = "none";
        _progress = 0.0f;
        _pollCount = 0;
        _lastElapsedMilliseconds = 0;
        _releaseWhenLoaded = false;
        var requestError = ResourceLoader.LoadThreadedRequest(
            JianghaiOldCitySceneLoader.DefaultScenePath,
            nameof(PackedScene),
            useSubThreads: true,
            ResourceLoader.CacheMode.Reuse);
        _requestCount++;
        if (requestError != Error.Ok)
        {
            _state = JianghaiMapPreloadState.Failed;
            _error = $"request_{requestError}";
            RequestClock.Reset();
            return false;
        }

        _state = JianghaiMapPreloadState.Loading;
        RequestClock.Restart();
        return true;
    }

    public static void Poll()
    {
        if (_state != JianghaiMapPreloadState.Loading)
        {
            return;
        }

        using var progress = new Godot.Collections.Array();
        var status = ResourceLoader.LoadThreadedGetStatus(
            JianghaiOldCitySceneLoader.DefaultScenePath,
            progress);
        _pollCount++;
        if (progress.Count > 0)
        {
            _progress = Mathf.Clamp((float)progress[0].AsDouble(), 0.0f, 1.0f);
        }

        switch (status)
        {
            case ResourceLoader.ThreadLoadStatus.InProgress:
                return;
            case ResourceLoader.ThreadLoadStatus.Loaded:
                var loadedScene = ResourceLoader.LoadThreadedGet(
                    JianghaiOldCitySceneLoader.DefaultScenePath) as PackedScene;
                _packedScene = _releaseWhenLoaded ? null : loadedScene;
                if (_releaseWhenLoaded)
                {
                    _state = JianghaiMapPreloadState.Idle;
                    _progress = 0.0f;
                    _error = "released";
                    _releaseWhenLoaded = false;
                    _lastElapsedMilliseconds = RequestClock.ElapsedMilliseconds;
                    RequestClock.Stop();
                    return;
                }
                _state = _packedScene is null
                    ? JianghaiMapPreloadState.Failed
                    : JianghaiMapPreloadState.Ready;
                _progress = _packedScene is null ? _progress : 1.0f;
                _error = _packedScene is null ? "loaded_resource_not_packed_scene" : "none";
                _lastElapsedMilliseconds = RequestClock.ElapsedMilliseconds;
                RequestClock.Stop();
                return;
            default:
                _state = JianghaiMapPreloadState.Failed;
                _error = $"status_{status}";
                _lastElapsedMilliseconds = RequestClock.ElapsedMilliseconds;
                RequestClock.Stop();
                return;
        }
    }

    public static async Task<bool> EnsureReadyAsync(
        SceneTree tree,
        int timeoutMilliseconds = DefaultTimeoutMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(tree);
        if (!Request())
        {
            return false;
        }

        var waitClock = Stopwatch.StartNew();
        while (_state == JianghaiMapPreloadState.Loading
            && waitClock.ElapsedMilliseconds < timeoutMilliseconds)
        {
            Poll();
            if (_state != JianghaiMapPreloadState.Loading)
            {
                break;
            }
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }

        if (_state == JianghaiMapPreloadState.Loading)
        {
            _error = "timeout";
        }
        return _state == JianghaiMapPreloadState.Ready;
    }

    public static void Release()
    {
        _packedScene = null;
        if (_state == JianghaiMapPreloadState.Loading)
        {
            _releaseWhenLoaded = true;
            _error = "release_pending";
            return;
        }

        _state = JianghaiMapPreloadState.Idle;
        _progress = 0.0f;
        _error = "released";
        _lastElapsedMilliseconds = 0;
        RequestClock.Reset();
    }

    public static PackedScene Acquire(out JianghaiPackedSceneAcquisition acquisition)
    {
        var clock = Stopwatch.StartNew();
        var readyBeforeAcquire = _state == JianghaiMapPreloadState.Ready
            && _packedScene is not null
            && GodotObject.IsInstanceValid(_packedScene);
        var usedThreadedRequest = readyBeforeAcquire || _state == JianghaiMapPreloadState.Loading;
        if (!readyBeforeAcquire)
        {
            if (_state != JianghaiMapPreloadState.Loading && !Request())
            {
                throw new InvalidOperationException(
                    $"Unable to request the Jianghai authored scene ({_error}).");
            }
            usedThreadedRequest = true;
            _packedScene = ResourceLoader.LoadThreadedGet(
                JianghaiOldCitySceneLoader.DefaultScenePath) as PackedScene;
            _state = _packedScene is null
                ? JianghaiMapPreloadState.Failed
                : JianghaiMapPreloadState.Ready;
            _progress = _packedScene is null ? _progress : 1.0f;
            _error = _packedScene is null ? "blocking_get_not_packed_scene" : "none";
            _lastElapsedMilliseconds = RequestClock.ElapsedMilliseconds;
            RequestClock.Stop();
        }

        clock.Stop();
        acquisition = new JianghaiPackedSceneAcquisition(
            usedThreadedRequest,
            readyBeforeAcquire,
            clock.ElapsedMilliseconds);
        return _packedScene
            ?? throw new InvalidOperationException(
                $"Unable to acquire the Jianghai authored scene ({_error}).");
    }
}
