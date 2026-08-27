namespace OperationSteelTide;

internal static partial class CombatModelLibrary
{
    private const string QuaterniusOperatorRoot = "res://assets/models/quaternius_operators";

    private readonly record struct OperatorVisualAssetSpec(
        string RuntimeScenePath,
        string PreviewScenePath,
        string[] RuntimeNodes,
        string[] PreviewNodes,
        bool UsesQuaterniusRig);

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
}
