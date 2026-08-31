using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

/// <summary>
/// Retains HUD PackedScene wrappers for the lifetime of the process.
/// Godot's native resource cache can outlive a collected C# wrapper, so HUD scenes that
/// reference one another must keep a managed strong reference across scene reloads.
/// Callers run on Godot's scene-tree thread; this cache is intentionally unsynchronized.
/// </summary>
internal static class HudPackedSceneCache
{
    private static readonly Dictionary<string, PackedScene> Scenes = new(StringComparer.Ordinal);

    public static int Count => Scenes.Count;

    public static PackedScene Load(string scenePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scenePath);

        if (Scenes.TryGetValue(scenePath, out var cached)
            && GodotObject.IsInstanceValid(cached))
        {
            return cached;
        }

        var scene = GD.Load<PackedScene>(scenePath)
            ?? throw new InvalidOperationException($"Unable to load HUD scene '{scenePath}'.");
        Scenes[scenePath] = scene;
        return scene;
    }

    public static T Instantiate<T>(string scenePath)
        where T : Node
        => Load(scenePath).Instantiate<T>();

    public static bool IsCached(string scenePath)
        => !string.IsNullOrWhiteSpace(scenePath)
        && Scenes.TryGetValue(scenePath, out var scene)
        && GodotObject.IsInstanceValid(scene);
}
