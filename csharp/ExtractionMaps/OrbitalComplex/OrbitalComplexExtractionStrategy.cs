namespace OperationSteelTide;

/// <summary>
/// Pure extraction rules for the Falltide Recovery Array tide gate.
/// The gate remains offline until one objective restores emergency power;
/// completing both objectives unlocks the accelerated full-power cycle.
/// </summary>
public sealed class OrbitalComplexExtractionStrategy
{
    public const float EmergencyPowerCountdownSeconds = 18.0f;
    public const float FullPowerCountdownSeconds = 9.0f;

    public bool CanExtract(int objectiveStage)
        => objectiveStage >= 1;

    public float CountdownSeconds(int objectiveStage)
        => objectiveStage switch
        {
            >= 2 => FullPowerCountdownSeconds,
            >= 1 => EmergencyPowerCountdownSeconds,
            _ => 0.0f
        };

    public bool TransportReady(int objectiveStage)
        => CanExtract(objectiveStage);

    public string StatusLocalizationKey(int objectiveStage)
        => objectiveStage switch
        {
            >= 2 => "falltide_extract_full",
            >= 1 => "falltide_extract_emergency",
            _ => "falltide_extract_locked"
        };
}
