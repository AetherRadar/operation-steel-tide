namespace OperationSteelTide;

/// <summary>
/// Pure rules for the local extraction downed/revive lifecycle.
/// </summary>
public static class SquadReviveRules
{
    /// <summary>
    /// A second down ends an offline/all-AI extraction immediately. Network
    /// clients wait for the host-authored mission outcome instead.
    /// </summary>
    public static bool ShouldFailExtractionOnSecondDown(
        bool demolitionMode,
        bool extractionNetworkClient,
        bool playerReviveUsed,
        bool allAiSquad)
        => !demolitionMode
            && !extractionNetworkClient
            && playerReviveUsed
            && allAiSquad;
}
