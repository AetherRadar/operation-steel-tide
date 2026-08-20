using System;
using System.Collections.Generic;
using System.Linq;
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

internal sealed class AuthoredGsh18Visual
{
    public AuthoredGsh18Visual(Node3D root)
    {
        Root = root;
    }

    public Node3D Root { get; }
}

internal sealed class AuthoredDesertEagleVisual
{
    public AuthoredDesertEagleVisual(Node3D root)
    {
        Root = root;
    }

    public Node3D Root { get; }
}

internal sealed class AuthoredPreviewOperatorVisual
{
    public AuthoredPreviewOperatorVisual(Node3D root)
    {
        Root = root;
    }

    public Node3D Root { get; }
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
    public Color TeamColorForDiagnostics { get; private set; }
    public Color GearTintForDiagnostics { get; private set; }
    public int GearOverlayCountForDiagnostics { get; private set; }

    public void SetTeamColor(Color color)
    {
        TeamColorForDiagnostics = color;
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

    public void SetFactionAppearance(Color patchColor, Color gearTint)
    {
        SetTeamColor(patchColor);
        GearTintForDiagnostics = gearTint;
        GearOverlayCountForDiagnostics = 0;
        var gearOverlay = new StandardMaterial3D
        {
            AlbedoColor = gearTint,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            Metallic = 0.08f,
            Roughness = 0.72f
        };
        foreach (var part in new[] { Helmet, Vest, Backpack })
        {
            foreach (var mesh in CombatModelLibrary.MeshesBelow(part))
            {
                mesh.MaterialOverlay = gearOverlay;
                GearOverlayCountForDiagnostics++;
            }
        }
    }
}

internal readonly record struct CombatModelInspection(
    bool Loaded,
    bool RequiredNodes,
    int MeshCount,
    int MaterialCount,
    Vector3 Size);

internal static class CombatModelLibrary
{
    internal const string WeaponScenePath = "res://assets/models/steel_tide_m4a1/steel_tide_m4a1.glb";
    internal const string OperatorScenePath = "res://assets/models/steel_tide_operator/steel_tide_operator.glb";
    internal const string PreviewOperatorScenePath = "res://assets/models/bamen_military_soldier/bamen_military_soldier.glb";
    internal const string Gsh18ScenePath = "res://assets/models/tastytony_gsh18/low-poly_gsh-18.glb";
    internal const string DesertEagleScenePath = "res://assets/models/elizion_desert_eagle/desert_eagle.glb";

    private const float Gsh18FirstPersonLength = 0.64f;
    private const float Gsh18PreviewLength = 0.78f;
    internal const float Gsh18FirstPersonPresentationScale = 2.8f;
    internal const float Gsh18PreviewPresentationScale = 8.0f;
    private const float DesertEagleFirstPersonLength = 0.82f;
    private const float DesertEaglePreviewLength = 1.05f;
    private const float OperatorPreviewHeight = 2.55f;
    private static readonly Vector3 PreviewOperatorSourceSize = new(1.3053f, 2.1079f, 0.4252f);
    private static readonly Vector3 PreviewOperatorSourceCenter = new(0.0f, 1.04885f, 0.0258f);

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

    private static readonly string[] PreviewOperatorNodes =
    {
        "BamenMilitarySoldier", "BamenMilitarySoldierRig", "BamenMilitarySoldierMesh"
    };

    private static readonly string[] Gsh18Nodes =
    {
        "Armature", "Skeleton3D"
    };

    private static readonly string[] DesertEagleNodes =
    {
        "RootNode", "Frame_low", "Slide_low", "Magazine_low"
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

    public static AuthoredPreviewOperatorVisual InstantiatePreviewOperator()
    {
        var source = InstantiateRequired(PreviewOperatorScenePath, PreviewOperatorNodes);
        source.Position = -PreviewOperatorSourceCenter;
        var wrapper = new Node3D
        {
            Name = "AuthoredPreviewOperatorVisual",
            Scale = Vector3.One * (OperatorPreviewHeight / PreviewOperatorSourceSize.Y)
        };
        wrapper.AddChild(source);
        return new AuthoredPreviewOperatorVisual(wrapper);
    }

    public static AuthoredGsh18Visual InstantiateGsh18(bool firstPerson)
    {
        var source = InstantiateRequired(Gsh18ScenePath, Gsh18Nodes);
        RemoveStagingNode(source, "Lamp");
        RemoveStagingNode(source, "Camera");
        var sourceBounds = ComputeBounds(source);
        if (sourceBounds.MeshCount == 0 || sourceBounds.Size.Y <= 0.001f)
        {
            source.Free();
            throw new InvalidOperationException("GSh-18 model has no usable geometry bounds.");
        }

        source.Position = -sourceBounds.Center;
        var targetLength = firstPerson ? Gsh18FirstPersonLength : Gsh18PreviewLength;
        var wrapper = new Node3D
        {
            Name = "AuthoredGsh18Visual",
            RotationDegrees = new Vector3(-90.0f, 0.0f, 0.0f),
            Scale = Vector3.One * (targetLength / sourceBounds.Size.Y)
        };
        wrapper.AddChild(source);
        if (firstPerson)
        {
            foreach (var geometry in GeometryBelow(wrapper))
            {
                geometry.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            }
        }
        return new AuthoredGsh18Visual(wrapper);
    }

    public static AuthoredDesertEagleVisual InstantiateDesertEagle(bool firstPerson)
    {
        var source = InstantiateRequired(DesertEagleScenePath, DesertEagleNodes);
        var sourceBounds = ComputeBounds(source);
        if (sourceBounds.MeshCount == 0 || sourceBounds.Size.Z <= 0.001f)
        {
            source.Free();
            throw new InvalidOperationException("Desert Eagle model has no usable geometry bounds.");
        }

        source.Position = -sourceBounds.Center;
        var targetLength = firstPerson ? DesertEagleFirstPersonLength : DesertEaglePreviewLength;
        var wrapper = new Node3D
        {
            Name = "AuthoredDesertEagleVisual",
            RotationDegrees = new Vector3(0.0f, 180.0f, 0.0f),
            Scale = Vector3.One * (targetLength / sourceBounds.Size.Z)
        };
        wrapper.AddChild(source);
        if (firstPerson)
        {
            foreach (var geometry in GeometryBelow(wrapper))
            {
                geometry.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            }
        }
        return new AuthoredDesertEagleVisual(wrapper);
    }

    public static CombatModelInspection InspectWeapon()
        => Inspect(WeaponScenePath, WeaponNodes);

    public static CombatModelInspection InspectOperator()
        => Inspect(OperatorScenePath, OperatorNodes);

    public static CombatModelInspection InspectPreviewOperator()
    {
        Node3D? root = null;
        try
        {
            root = InstantiatePreviewOperator().Root;
            var scale = OperatorPreviewHeight / PreviewOperatorSourceSize.Y;
            return new CombatModelInspection(
                true,
                true,
                MeshesBelow(root).Count(),
                CountMaterials(root),
                PreviewOperatorSourceSize * scale);
        }
        catch
        {
            return new CombatModelInspection(false, false, 0, 0, Vector3.Zero);
        }
        finally
        {
            root?.Free();
        }
    }

    public static CombatModelInspection InspectGsh18()
    {
        Node3D? root = null;
        try
        {
            root = InstantiateGsh18(firstPerson: false).Root;
            var bounds = ComputeBounds(root);
            return new CombatModelInspection(
                true,
                true,
                bounds.MeshCount,
                CountMaterials(root),
                bounds.Size);
        }
        catch
        {
            return new CombatModelInspection(false, false, 0, 0, Vector3.Zero);
        }
        finally
        {
            root?.Free();
        }
    }

    public static CombatModelInspection InspectDesertEagle()
    {
        Node3D? root = null;
        try
        {
            root = InstantiateDesertEagle(firstPerson: false).Root;
            var bounds = ComputeBounds(root);
            return new CombatModelInspection(
                true,
                true,
                bounds.MeshCount,
                CountMaterials(root),
                bounds.Size);
        }
        catch
        {
            return new CombatModelInspection(false, false, 0, 0, Vector3.Zero);
        }
        finally
        {
            root?.Free();
        }
    }

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
            return new CombatModelInspection(false, false, 0, 0, Vector3.Zero);
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
            return new CombatModelInspection(
                true,
                required,
                bounds.MeshCount,
                CountMaterials(root),
                bounds.Size);
        }
        finally
        {
            root.Free();
        }
    }

    private static int CountMaterials(Node root)
    {
        var materialCount = 0;
        foreach (var meshInstance in MeshesBelow(root))
        {
            if (meshInstance.MaterialOverride is not null)
            {
                materialCount += Mathf.Max(1, meshInstance.Mesh?.GetSurfaceCount() ?? 0);
                continue;
            }
            if (meshInstance.Mesh is not { } mesh)
            {
                continue;
            }
            for (var surface = 0; surface < mesh.GetSurfaceCount(); surface++)
            {
                if (mesh.SurfaceGetMaterial(surface) is not null)
                {
                    materialCount++;
                }
            }
        }
        return materialCount;
    }

    private static (int MeshCount, Vector3 Size, Vector3 Center) ComputeBounds(Node3D root)
    {
        var minimum = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        var maximum = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        var meshCount = 0;
        AccumulateBounds(root, Transform3D.Identity, ref minimum, ref maximum, ref meshCount);
        return meshCount == 0
            ? (0, Vector3.Zero, Vector3.Zero)
            : (meshCount, maximum - minimum, (minimum + maximum) * 0.5f);
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
        var children = node.GetChildren();
        using var childrenBacking = children.AsDisposable();
        foreach (var child in children)
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

    private static void RemoveStagingNode(Node3D root, string name)
    {
        var node = FindNode(root, name);
        node?.Free();
    }

    private static IEnumerable<GeometryInstance3D> GeometryBelow(Node root)
    {
        var children = root.GetChildren();
        using var childrenBacking = children.AsDisposable();
        foreach (var child in children)
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
