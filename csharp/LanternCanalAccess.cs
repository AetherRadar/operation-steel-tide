using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

internal readonly record struct CanalAccessRoute(string Id, Vector3 Bottom, Vector3 Top, Vector3 Outward);

/// <summary>Instantiates authored egress ladders and reads their DCC-authored traversal endpoints.</summary>
internal static class LanternCanalAccess
{
    internal const string ScenePath = "res://assets/models/lantern_canal_world/canal_access.glb";

    internal static IReadOnlyList<CanalAccessRoute> Build(Node3D parent)
    {
        var scene = GD.Load<PackedScene>(ScenePath)
            ?? throw new InvalidOperationException("Missing authored canal access scene.");
        var root = scene.Instantiate<Node3D>();
        parent.AddChild(root);
        var routes = new List<CanalAccessRoute>();
        foreach (var node in root.FindChildren("CanalAccess_*", "Node3D", true, false))
        {
            var route = (Node3D)node;
            var suffix = route.Name.ToString()["CanalAccess_".Length..];
            var bottom = route.GetNode<Node3D>("BottomFeet_" + suffix).GlobalPosition;
            var top = route.GetNode<Node3D>("TopFeet_" + suffix).GlobalPosition;
            var outward = (route.GetNode<Node3D>("Outward_" + suffix).GlobalPosition - route.GlobalPosition).Normalized();
            routes.Add(new CanalAccessRoute(route.Name, bottom, top, outward));
        }
        if (routes.Count != 36) throw new InvalidOperationException($"Expected 36 canal ladders, found {routes.Count}.");
        return routes;
    }
}
