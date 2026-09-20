using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Godot;

namespace OperationSteelTide;

/// <summary>Keeps the selected canal assets ready across the lobby scene reload.</summary>
internal static class LanternCanalPreloadCache
{
    internal const string CityPath = "res://assets/models/lantern_canal_world/lantern_canal_runtime.glb";
    internal const string MountainsPath = "res://assets/models/lantern_canal_world/lantern_mountains.glb";
    private static readonly string[] Paths = { CityPath, MountainsPath };
    private static readonly Dictionary<string, PackedScene> Ready = new();
    private static readonly HashSet<string> Requested = new();
    internal static int YieldedFrames { get; private set; }
    internal static long LoadMilliseconds { get; private set; }

    public static void Request()
    {
        foreach (var path in Paths)
        {
            if (Ready.ContainsKey(path) || Requested.Contains(path)) continue;
            var error = ResourceLoader.LoadThreadedRequest(path, nameof(PackedScene), useSubThreads: false);
            if (error != Error.Ok)
                throw new InvalidOperationException($"Canal asset request failed: {path}: {error}");
            Requested.Add(path);
        }
    }

    public static async Task<bool> EnsureReadyAsync(SceneTree tree)
    {
        var clock = Stopwatch.StartNew();
        Request();
        while (Ready.Count < Paths.Length)
        {
            foreach (var path in Paths)
            {
                if (Ready.ContainsKey(path)) continue;
                var status = ResourceLoader.LoadThreadedGetStatus(path);
                if (status == ResourceLoader.ThreadLoadStatus.Loaded)
                {
                    Ready.Add(path, ResourceLoader.LoadThreadedGet(path) as PackedScene
                        ?? throw new InvalidOperationException($"Canal asset is not a scene: {path}"));
                    Requested.Remove(path);
                }
                else if (status != ResourceLoader.ThreadLoadStatus.InProgress)
                {
                    throw new InvalidOperationException($"Canal asset load failed: {path}: {status}");
                }
            }
            if (Ready.Count == Paths.Length) break;
            if (clock.ElapsedMilliseconds > 120_000)
                throw new TimeoutException("Canal asset preload exceeded two minutes.");
            YieldedFrames++;
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }
        LoadMilliseconds = clock.ElapsedMilliseconds;
        return true;
    }

    public static PackedScene Acquire(string path)
        => Ready.TryGetValue(path, out var scene) ? scene
            : throw new InvalidOperationException($"Canal asset was not preloaded: {path}");

    public static void ReleaseReady() => Ready.Clear();
}
