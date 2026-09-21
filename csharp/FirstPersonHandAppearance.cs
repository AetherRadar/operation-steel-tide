using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

// Selects Blender-authored material variants without changing shared meshes.
internal static class FirstPersonHandAppearance
{
    private static readonly Dictionary<OperatorRole, (Material Sleeve, Material Glove)> Palettes = new();

    public static void Apply(Node3D root, OperatorRole role)
    {
        var palette = GetPalette(role);
        ApplyBelow(root);
        root.SetMeta("hands_role", (int)role);

        void ApplyBelow(Node node)
        {
            if (node is MeshInstance3D { Mesh: not null } mesh)
            {
                for (var surface = 0; surface < mesh.Mesh.GetSurfaceCount(); surface++)
                {
                    var name = mesh.Mesh.SurfaceGetMaterial(surface)?.ResourceName ?? string.Empty;
                    if (name.StartsWith("HandsSleeve", StringComparison.Ordinal))
                    {
                        mesh.SetSurfaceOverrideMaterial(surface, palette.Sleeve);
                    }
                    else if (name.StartsWith("HandsGlove", StringComparison.Ordinal))
                    {
                        mesh.SetSurfaceOverrideMaterial(surface, palette.Glove);
                    }
                }
            }
            foreach (var child in node.GetChildren())
            {
                ApplyBelow(child);
            }
        }
    }

    private static (Material Sleeve, Material Glove) GetPalette(OperatorRole role)
    {
        if (Palettes.TryGetValue(role, out var palette))
        {
            return palette;
        }
        var scene = GD.Load<PackedScene>("res://assets/models/djmaesen_smg45/hands_palette.glb")
            ?? throw new InvalidOperationException("Required authored hand palette is missing.");
        var root = scene.Instantiate<Node3D>();
        try
        {
            foreach (var item in Enum.GetValues<OperatorRole>())
            {
                var mesh = root.GetNode<MeshInstance3D>(item.ToString());
                Material? sleeve = null;
                Material? glove = null;
                for (var surface = 0; surface < mesh.Mesh.GetSurfaceCount(); surface++)
                {
                    var material = mesh.Mesh.SurfaceGetMaterial(surface);
                    if (material.ResourceName == item + "Sleeve") sleeve = material;
                    if (material.ResourceName == item + "Glove") glove = material;
                }
                Palettes.Add(item, (sleeve ?? throw new InvalidOperationException($"Missing {item} sleeve."),
                    glove ?? throw new InvalidOperationException($"Missing {item} glove.")));
            }
        }
        finally
        {
            root.Free();
        }
        return Palettes[role];
    }
}
