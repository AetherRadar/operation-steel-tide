using System;
using Godot;

namespace OperationSteelTide;

internal sealed class AuthoredMeleeVisual
{
    public AuthoredMeleeVisual(Node3D root, KnifeSkinDefinition definition)
    {
        Root = root;
        GripPrimary = CombatModelLibrary.RequireNode(root, "GripPrimary");
        GripSupport = CombatModelLibrary.RequireNode(root, "GripSupport");
        BladeBase = CombatModelLibrary.RequireNode(root, "BladeBase");
        BladeTip = CombatModelLibrary.RequireNode(root, "BladeTip");
        ConfigureMaterials(definition);
    }

    public Node3D Root { get; }
    public Node3D GripPrimary { get; }
    public Node3D GripSupport { get; }
    public Node3D BladeBase { get; }
    public Node3D BladeTip { get; }

    private void ConfigureMaterials(KnifeSkinDefinition definition)
    {
        foreach (var mesh in CombatModelLibrary.MeshesBelow(Root))
        {
            var surfaceCount = mesh.Mesh?.GetSurfaceCount() ?? 0;
            for (var surface = 0; surface < surfaceCount; surface++)
            {
                var imported = mesh.Mesh!.SurfaceGetMaterial(surface);
                var materialName = imported?.ResourceName.ToString() ?? string.Empty;
                var identity = $"{mesh.Name}:{materialName}";
                StandardMaterial3D? replacement = null;
                if (identity.Contains("TintBlade", StringComparison.OrdinalIgnoreCase))
                {
                    replacement = Material(definition.BladeColor, 0.92f, 0.2f);
                }
                else if (identity.Contains("TintEdge", StringComparison.OrdinalIgnoreCase))
                {
                    replacement = Material(definition.EdgeColor, 0.96f, 0.11f);
                }
                else if (identity.Contains("TintGrip", StringComparison.OrdinalIgnoreCase))
                {
                    replacement = Material(definition.GripColor, 0.08f, 0.72f);
                }
                else if (identity.Contains("TintAccent", StringComparison.OrdinalIgnoreCase))
                {
                    replacement = Material(definition.EdgeColor, 0.7f, 0.2f);
                    if (definition.Style == MeleeWeaponStyle.TianxuanDao)
                    {
                        replacement.EmissionEnabled = true;
                        replacement.Emission = definition.EdgeColor;
                        replacement.EmissionEnergyMultiplier = 2.4f;
                    }
                }
                if (replacement is not null)
                {
                    mesh.SetSurfaceOverrideMaterial(surface, replacement);
                }
            }
        }
    }

    private static StandardMaterial3D Material(Color color, float metallic, float roughness)
        => new()
        {
            AlbedoColor = color,
            Metallic = metallic,
            Roughness = roughness
        };
}

internal readonly record struct MeleeModelInspection(
    bool Loaded,
    bool RequiredNodes,
    bool AuthoredMeshes,
    int MeshCount,
    int MaterialCount,
    int TriangleCount,
    Vector3 Size,
    float BladeLength);

internal static partial class CombatModelLibrary
{
    internal const string TacticalKnifeScenePath =
        "res://assets/models/steel_tide_melee/tactical_knife.glb";
    internal const string ZhanmaDaoScenePath =
        "res://assets/models/steel_tide_melee/zhanma_dao.glb";
    internal const string TianxuanDaoScenePath =
        "res://assets/models/steel_tide_melee/tianxuan_dao.glb";

    private static readonly string[] RequiredMeleeNodes =
    {
        "GripPrimary", "GripSupport", "BladeBase", "BladeTip"
    };

    public static AuthoredMeleeVisual InstantiateMelee(KnifeSkinDefinition definition)
    {
        var path = MeleeScenePath(definition.Style);
        var root = InstantiateRequired(path, RequiredMeleeNodes);
        root.Name = $"Authored{definition.Style}Visual";
        foreach (var mesh in MeshesBelow(root))
        {
            mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        }
        return new AuthoredMeleeVisual(root, definition);
    }

    public static MeleeModelInspection InspectMelee(KnifeSkinDefinition definition)
    {
        AuthoredMeleeVisual? visual = null;
        try
        {
            visual = InstantiateMelee(definition);
            var meshCount = 0;
            var materialCount = 0;
            var triangleCount = 0;
            var authoredMeshes = true;
            var minimum = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            var maximum = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
            foreach (var mesh in MeshesBelow(visual.Root))
            {
                if (mesh.Mesh is null)
                {
                    continue;
                }
                meshCount++;
                materialCount += mesh.Mesh.GetSurfaceCount();
                authoredMeshes &= mesh.Mesh is not PrimitiveMesh;
                triangleCount += mesh.Mesh.GetFaces().Length / 3;
                var transform = TransformBelow(visual.Root, mesh);
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
            var bladeBase = TransformBelow(visual.Root, visual.BladeBase).Origin;
            var bladeTip = TransformBelow(visual.Root, visual.BladeTip).Origin;
            var bladeLength = bladeBase.DistanceTo(bladeTip);
            return new MeleeModelInspection(
                true,
                true,
                authoredMeshes,
                meshCount,
                materialCount,
                triangleCount,
                meshCount > 0 ? maximum - minimum : Vector3.Zero,
                bladeLength);
        }
        catch (Exception exception)
        {
            GD.PushError($"Required authored melee model unavailable: {exception.Message}");
            return new MeleeModelInspection(false, false, false, 0, 0, 0, Vector3.Zero, 0.0f);
        }
        finally
        {
            visual?.Root.Free();
        }
    }

    public static string MeleeScenePath(MeleeWeaponStyle style)
        => style switch
        {
            MeleeWeaponStyle.ZhanmaDao => ZhanmaDaoScenePath,
            MeleeWeaponStyle.TianxuanDao => TianxuanDaoScenePath,
            _ => TacticalKnifeScenePath
        };

    private static Transform3D TransformBelow(Node3D root, Node3D descendant)
    {
        var transform = Transform3D.Identity;
        Node3D? current = descendant;
        while (current is not null && current != root)
        {
            transform = current.Transform * transform;
            current = current.GetParent() as Node3D;
        }
        if (current != root)
        {
            throw new InvalidOperationException(
                $"Node {descendant.Name} is not below authored melee root {root.Name}.");
        }
        return transform;
    }
}
