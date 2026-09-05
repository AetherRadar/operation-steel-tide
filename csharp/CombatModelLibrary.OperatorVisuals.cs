using Godot;

namespace OperationSteelTide;

internal static partial class CombatModelLibrary
{
    private const string QuaterniusOperatorRoot = "res://assets/models/quaternius_operators";
    private const string Hy3dOperatorRoot = "res://assets/models/hy3d_operators";

    private readonly record struct OperatorVisualAssetSpec(
        string RuntimeScenePath,
        string PreviewScenePath,
        string[] RuntimeNodes,
        string[] PreviewNodes,
        bool UsesQuaterniusRig);

    // The Tencent conversion keeps the runtime armature/socket contract but
    // consolidates the legacy four-piece body into one authored skinned mesh.
    private static readonly string[] Hy3dOperatorNodes =
    {
        "QuaterniusOperator", "QuaterniusOperatorRig", "OperatorBody",
        "WeaponSocket", "BackWeaponSocket", "HeadSocket", "VestSocket",
        "BackpackSocket", "TeamPatchSocket"
    };

    private static OperatorVisualAssetSpec OperatorVisualAsset(OperatorVisualId visualId)
    {
        if (visualId == OperatorVisualId.Garrison)
        {
            return new OperatorVisualAssetSpec(
                OperatorScenePath,
                PreviewOperatorScenePath,
                OperatorNodes,
                PreviewOperatorNodes,
                UsesQuaterniusRig: false);
        }

        var slug = visualId switch
        {
            OperatorVisualId.Heron => "heron",
            OperatorVisualId.Lynx => "lynx",
            OperatorVisualId.Magpie => "magpie",
            OperatorVisualId.Jackal => "jackal",
            _ => "viper"
        };
        var hy3dPath = $"{Hy3dOperatorRoot}/{slug}.glb";
        if (ResourceLoader.Exists(hy3dPath))
        {
            return new OperatorVisualAssetSpec(
                hy3dPath,
                hy3dPath,
                Hy3dOperatorNodes,
                Hy3dOperatorNodes,
                UsesQuaterniusRig: true);
        }

        // Keep a clean-checkout fallback while the generated Tencent files
        // remain private pending redistribution/license confirmation.
        var path = $"{QuaterniusOperatorRoot}/{slug}.glb";
        return new OperatorVisualAssetSpec(
            path,
            path,
            QuaterniusOperatorNodes,
            QuaterniusOperatorNodes,
            UsesQuaterniusRig: true);
    }

    internal static bool UsesQuaterniusOperatorRig(OperatorVisualId visualId)
        => OperatorVisualAsset(visualId).UsesQuaterniusRig;

    internal static bool UsesHy3dOperator(OperatorVisualId visualId)
    {
        if (visualId == OperatorVisualId.Garrison)
        {
            return false;
        }

        var slug = visualId switch
        {
            OperatorVisualId.Heron => "heron",
            OperatorVisualId.Lynx => "lynx",
            OperatorVisualId.Magpie => "magpie",
            OperatorVisualId.Jackal => "jackal",
            _ => "viper"
        };
        return ResourceLoader.Exists($"{Hy3dOperatorRoot}/{slug}.glb");
    }
}
