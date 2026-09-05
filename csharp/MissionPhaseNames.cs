namespace OperationSteelTide;

/// <summary>
/// Canonical mission phase names across extraction, demolition, training, and diagnostic scenarios.
/// Centralizes string literals to eliminate typos and establish a unified domain vocabulary.
/// </summary>
public static class MissionPhaseNames
{
    public const string Deployment = "DEPLOYMENT";
    public const string Infiltration = "INFILTRATION";
    public const string Contact = "CONTACT";
    public const string Combat = "COMBAT";
    public const string Extraction = "EXTRACTION";
    public const string Complete = "COMPLETE";
    public const string Demolition = "DEMOLITION";
    public const string TrainingRange = "TRAINING_RANGE";

    public static bool IsHostilePhase(string phase) =>
        phase is Contact or Combat;
}
