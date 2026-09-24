using Godot;

namespace OperationSteelTide;

// Selects Blender-authored material variants without changing shared meshes.
internal static class FirstPersonHandAppearance
{
    public static void Apply(Node3D root, OperatorRole role)
    {
        // Role-specific mesh/material variants are authored in Blender and
        // selected before this call. Keep the metadata for diagnostics and
        // future presentation systems without mutating the imported asset.
        root.SetMeta("hands_role", (int)role);
    }
}
