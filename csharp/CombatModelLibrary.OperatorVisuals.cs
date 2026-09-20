using System;
using Godot;

namespace OperationSteelTide;

internal static partial class CombatModelLibrary
{
    private const string Hy3dOperatorRoot = "res://assets/models/hy3d_operators";
    private const string EnemyOperatorRoot = "res://assets/models/enemy_operator";

    private readonly record struct OperatorVisualAssetSpec(
        string ScenePath,
        string[] RequiredNodes);

    // The Tencent conversion keeps the runtime armature/socket contract but
    // consolidates the legacy four-piece body into one authored skinned mesh.
    private static readonly string[] Hy3dOperatorNodes =
    {
        "AuthoredOperatorPresentation",
        "QuaterniusOperator", "QuaterniusOperatorRig", "OperatorBody",
        "WeaponSocket", "BackWeaponSocket", "HeadSocket", "VestSocket",
        "BackpackSocket", "TeamPatchSocket"
    };

    private static OperatorVisualAssetSpec OperatorVisualAsset(OperatorVisualId visualId)
    {
        var path = visualId switch
        {
            OperatorVisualId.Garrison => $"{EnemyOperatorRoot}/enemy_operator.glb",
            OperatorVisualId.Viper => $"{Hy3dOperatorRoot}/viper.glb",
            OperatorVisualId.Heron => $"{Hy3dOperatorRoot}/heron.glb",
            OperatorVisualId.Lynx => $"{Hy3dOperatorRoot}/lynx.glb",
            OperatorVisualId.Magpie => $"{Hy3dOperatorRoot}/magpie.glb",
            OperatorVisualId.Jackal => $"{Hy3dOperatorRoot}/jackal.glb",
            _ => throw new ArgumentOutOfRangeException(nameof(visualId), visualId, "Unknown authored operator.")
        };
        return new OperatorVisualAssetSpec(path, Hy3dOperatorNodes);
    }

}
