namespace OperationSteelTide;

/// <summary>
/// A sealed source whose contents do not exist until the source is searched.
/// Potential-content hints let unarmed AI choose a source without revealing its roll.
/// </summary>
public interface IDeferredLootSource
{
    bool ContentsResolved { get; }
    bool MayContainWeapon { get; }
}
