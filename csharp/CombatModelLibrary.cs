using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

internal sealed class AuthoredWeaponVisual
{
    public AuthoredWeaponVisual(Node3D root)
    {
        Root = root;
        Magazine = CombatModelLibrary.RequireNode(root, "Magazine");
        SpareMagazine = CombatModelLibrary.RequireNode(root, "SpareMagazine");
        ChargingHandle = CombatModelLibrary.RequireNode(root, "ChargingHandle");
        Stock = CombatModelLibrary.RequireNode(root, "Stock");
        Foregrip = CombatModelLibrary.RequireNode(root, "Foregrip");
        MuzzleDevice = CombatModelLibrary.RequireNode(root, "MuzzleDevice");
        Suppressor = CombatModelLibrary.RequireNode(root, "Suppressor");
        OpticMount = CombatModelLibrary.RequireNode(root, "OpticMount");
    }

    public Node3D Root { get; }
    public Node3D Magazine { get; }
    public Node3D SpareMagazine { get; }
    public Node3D ChargingHandle { get; }
    public Node3D Stock { get; }
    public Node3D Foregrip { get; }
    public Node3D MuzzleDevice { get; }
    public Node3D Suppressor { get; }
    public Node3D OpticMount { get; }

    public void Configure(WeaponBuild build)
    {
        var suppressed = build.Attachments.TryGetValue(AttachmentSlot.Muzzle, out var muzzleId)
            && muzzleId == "muzzle_suppressor";
        var hasForegrip = build.Attachments.ContainsKey(AttachmentSlot.Grip);
        MuzzleDevice.Visible = !suppressed;
        Suppressor.Visible = suppressed;
        Foregrip.Visible = hasForegrip;
        OpticMount.Visible = build.Attachments.ContainsKey(AttachmentSlot.Optic);
    }

    public void SyncMechanisms(Node3D magazine, Node3D spareMagazine, Node3D chargingHandle)
    {
        Magazine.Transform = magazine.Transform;
        Magazine.Visible = magazine.Visible;
        SpareMagazine.Transform = spareMagazine.Transform;
        SpareMagazine.Visible = spareMagazine.Visible;
        ChargingHandle.Transform = chargingHandle.Transform;
    }
}

internal sealed class AuthoredOperatorVisual
{
    public AuthoredOperatorVisual(Node3D root)
    {
        Root = root;
        LeftLegRig = CombatModelLibrary.RequireNode(root, "LeftLegRig");
        RightLegRig = CombatModelLibrary.RequireNode(root, "RightLegRig");
        Helmet = CombatModelLibrary.RequireNode(root, "Helmet");
        Vest = CombatModelLibrary.RequireNode(root, "Vest");
        Backpack = CombatModelLibrary.RequireNode(root, "Backpack");
        TeamPatch = CombatModelLibrary.RequireNode(root, "TeamPatch");
    }

    public Node3D Root { get; }
    public Node3D LeftLegRig { get; }
    public Node3D RightLegRig { get; }
    public Node3D Helmet { get; }
    public Node3D Vest { get; }
    public Node3D Backpack { get; }
    public Node3D TeamPatch { get; }

    public void SetTeamColor(Color color)
    {
        var patchMaterial = new StandardMaterial3D
        {
            AlbedoColor = color,
            Metallic = 0.16f,
            Roughness = 0.3f,
            EmissionEnabled = true,
            Emission = color.Darkened(0.35f),
            EmissionEnergyMultiplier = 1.7f
        };
        foreach (var mesh in CombatModelLibrary.MeshesBelow(TeamPatch))
        {
            mesh.MaterialOverride = patchMaterial;
        }
    }
}

internal readonly record struct CombatModelInspection(
    bool Loaded,
    bool RequiredNodes,
    int MeshCount,
    Vector3 Size);

internal static class CombatModelLibrary
{
    internal const string WeaponScenePath = "res://assets/models/steel_tide_m4a1/steel_tide_m4a1.glb";
    internal const string OperatorScenePath = "res://assets/models/steel_tide_operator/steel_tide_operator.glb";

    private static readonly string[] WeaponNodes =
    {
        "SteelTideM4A1", "Magazine", "SpareMagazine", "ChargingHandle",
        "Stock", "Foregrip", "MuzzleDevice", "Suppressor", "OpticMount"
    };

    private static readonly string[] OperatorNodes =
    {
        "SteelTideOperator", "LeftLegRig", "RightLegRig", "Helmet",
        "Vest", "Backpack", "TeamPatch"
    };

    public static AuthoredWeaponVisual InstantiateWeapon(bool firstPerson)
    {
        var root = InstantiateRequired(WeaponScenePath, WeaponNodes);
        root.Name = "AuthoredM4A1Visual";
        if (firstPerson)
        {
            foreach (var geometry in GeometryBelow(root))
            {
                geometry.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            }
        }
        return new AuthoredWeaponVisual(root);
    }

    public static AuthoredOperatorVisual InstantiateOperator()
    {
        var root = InstantiateRequired(OperatorScenePath, OperatorNodes);
        root.Name = "AuthoredOperatorVisual";
        return new AuthoredOperatorVisual(root);
    }

    public static CombatModelInspection InspectWeapon()
        => Inspect(WeaponScenePath, WeaponNodes);

    public static CombatModelInspection InspectOperator()
        => Inspect(OperatorScenePath, OperatorNodes);

    internal static Node3D RequireNode(Node3D root, string name)
    {
        var node = FindNode(root, name);
        return node ?? throw new InvalidOperationException(
            $"Combat model {root.Name} is missing required node {name}.");
    }

    internal static IEnumerable<MeshInstance3D> MeshesBelow(Node root)
    {
        foreach (var geometry in GeometryBelow(root))
        {
            if (geometry is MeshInstance3D mesh)
            {
                yield return mesh;
            }
        }
    }

    private static Node3D InstantiateRequired(string path, IReadOnlyList<string> requiredNodes)
    {
        var scene = GD.Load<PackedScene>(path)
            ?? throw new InvalidOperationException($"Required combat model could not load: {path}");
        var root = scene.Instantiate<Node3D>();
        foreach (var nodeName in requiredNodes)
        {
            if (FindNode(root, nodeName) is null)
            {
                root.Free();
                throw new InvalidOperationException(
                    $"Required combat model {path} is missing node {nodeName}.");
            }
        }
        return root;
    }

    private static CombatModelInspection Inspect(string path, IReadOnlyList<string> requiredNodes)
    {
        var scene = GD.Load<PackedScene>(path);
        if (scene is null)
        {
            return new CombatModelInspection(false, false, 0, Vector3.Zero);
        }
        var root = scene.Instantiate<Node3D>();
        try
        {
            var required = true;
            foreach (var nodeName in requiredNodes)
            {
                required &= FindNode(root, nodeName) is not null;
            }
            var bounds = ComputeBounds(root);
            return new CombatModelInspection(true, required, bounds.MeshCount, bounds.Size);
        }
        finally
        {
            root.Free();
        }
    }

    private static (int MeshCount, Vector3 Size) ComputeBounds(Node3D root)
    {
        var minimum = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        var maximum = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        var meshCount = 0;
        AccumulateBounds(root, Transform3D.Identity, ref minimum, ref maximum, ref meshCount);
        return meshCount == 0
            ? (0, Vector3.Zero)
            : (meshCount, maximum - minimum);
    }

    private static void AccumulateBounds(
        Node3D node,
        Transform3D parentTransform,
        ref Vector3 minimum,
        ref Vector3 maximum,
        ref int meshCount)
    {
        var transform = parentTransform * node.Transform;
        if (node is MeshInstance3D { Mesh: not null } mesh)
        {
            meshCount++;
            var bounds = mesh.Mesh.GetAabb();
            for (var x = 0; x <= 1; x++)
            {
                for (var y = 0; y <= 1; y++)
                {
                    for (var z = 0; z <= 1; z++)
                    {
                        var local = bounds.Position + new Vector3(
                            bounds.Size.X * x,
                            bounds.Size.Y * y,
                            bounds.Size.Z * z);
                        var point = transform * local;
                        minimum = minimum.Min(point);
                        maximum = maximum.Max(point);
                    }
                }
            }
        }
        foreach (var child in node.GetChildren())
        {
            if (child is Node3D child3D)
            {
                AccumulateBounds(child3D, transform, ref minimum, ref maximum, ref meshCount);
            }
        }
    }

    private static Node3D? FindNode(Node3D root, string name)
    {
        if (root.Name == name)
        {
            return root;
        }
        return root.FindChild(name, recursive: true, owned: false) as Node3D;
    }

    private static IEnumerable<GeometryInstance3D> GeometryBelow(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is GeometryInstance3D geometry)
            {
                yield return geometry;
            }
            foreach (var descendant in GeometryBelow(child))
            {
                yield return descendant;
            }
        }
    }
}
