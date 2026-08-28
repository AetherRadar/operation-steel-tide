namespace OperationSteelTide;

/// <summary>
/// Owns deployment access gates that can be changed independently from saved reputation progress.
/// </summary>
public static class DeploymentAccessPolicy
{
    /// <summary>
    /// Reputation still accumulates and grants perks while this gate is disabled.
    /// Set this to true to restore reputation requirements for extraction deployments.
    /// </summary>
    public static bool ReputationRestrictionsEnabled => false;

    public static bool IsReputationLocked(int currentLevel, int requiredLevel)
        => ReputationRestrictionsEnabled && currentLevel < requiredLevel;
}
