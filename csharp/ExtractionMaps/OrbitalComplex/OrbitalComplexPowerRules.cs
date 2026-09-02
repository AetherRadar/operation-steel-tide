using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public enum OrbitalComplexPowerMode
{
    Blackout,
    EmergencyPower,
    FullReroute
}

public enum OrbitalComplexAlarmState
{
    Off,
    EmergencyBeacon,
    VaultBreach
}

public enum OrbitalComplexResponseActivationHint
{
    Dormant,
    QrfWestApproach,
    QrfEastApproach,
    QrfAndBossActive
}

public readonly record struct OrbitalComplexDistrictLightPresentation(
    string District,
    Color Color,
    float Energy);

public sealed record OrbitalComplexPresentationState(
    float DishRotationSpeedRadiansPerSecond,
    IReadOnlyList<OrbitalComplexDistrictLightPresentation> DistrictLights,
    OrbitalComplexAlarmState AlarmState,
    float TideGateOpeningFraction,
    float VaultDoorOpeningFraction,
    OrbitalComplexResponseActivationHint ResponseHint,
    bool QrfActivationRecommended,
    bool BossActivationRecommended);

public sealed record OrbitalComplexPowerState(
    int ObjectiveStage,
    OrbitalComplexPowerMode Mode,
    bool ExtractionEnabled,
    float ExtractionHoldSeconds,
    bool UpperBypassOpen,
    bool VaultOpen,
    OrbitalComplexPresentationState Presentation);

/// <summary>
/// Authoritative Falltide stage projection. No timers or local random source participate in this
/// state; equal objective stage and shared seed always produce the same gameplay and presentation.
/// </summary>
public static class OrbitalComplexPowerRules
{
    public const int MaximumObjectiveStage = 2;

    public static OrbitalComplexPowerState Derive(int objectiveStage, ulong sharedWorldSeed)
    {
        var stage = Math.Clamp(objectiveStage, 0, MaximumObjectiveStage);
        var dishDirection = (sharedWorldSeed & 2UL) == 0UL ? 1.0f : -1.0f;
        return stage switch
        {
            0 => new OrbitalComplexPowerState(
                stage,
                OrbitalComplexPowerMode.Blackout,
                false,
                0.0f,
                false,
                false,
                Presentation(
                    dishDirection * 0.04f,
                    OrbitalComplexAlarmState.Off,
                    0.0f,
                    0.0f,
                    OrbitalComplexResponseActivationHint.Dormant,
                    qrf: false,
                    boss: false,
                    BlackoutLights())),
            1 => new OrbitalComplexPowerState(
                stage,
                OrbitalComplexPowerMode.EmergencyPower,
                true,
                OrbitalComplexExtractionStrategy.EmergencyPowerCountdownSeconds,
                true,
                false,
                Presentation(
                    dishDirection * 0.18f,
                    OrbitalComplexAlarmState.EmergencyBeacon,
                    0.62f,
                    0.0f,
                    (sharedWorldSeed & 4UL) == 0UL
                        ? OrbitalComplexResponseActivationHint.QrfWestApproach
                        : OrbitalComplexResponseActivationHint.QrfEastApproach,
                    qrf: true,
                    boss: false,
                    EmergencyLights())),
            _ => new OrbitalComplexPowerState(
                stage,
                OrbitalComplexPowerMode.FullReroute,
                true,
                OrbitalComplexExtractionStrategy.FullPowerCountdownSeconds,
                true,
                true,
                Presentation(
                    dishDirection * 0.42f,
                    OrbitalComplexAlarmState.VaultBreach,
                    1.0f,
                    1.0f,
                    OrbitalComplexResponseActivationHint.QrfAndBossActive,
                    qrf: true,
                    boss: true,
                    FullPowerLights()))
        };
    }

    public static bool IsGateOpen(
        OrbitalComplexPowerGateDefinition gate,
        OrbitalComplexPowerState state)
        => state.ObjectiveStage >= gate.OpensAtObjectiveStage;

    private static OrbitalComplexPresentationState Presentation(
        float dishSpeed,
        OrbitalComplexAlarmState alarm,
        float tideGateFraction,
        float vaultDoorFraction,
        OrbitalComplexResponseActivationHint response,
        bool qrf,
        bool boss,
        IReadOnlyList<OrbitalComplexDistrictLightPresentation> lights)
        => new(dishSpeed, lights, alarm, tideGateFraction, vaultDoorFraction,
            response, qrf, boss);

    private static IReadOnlyList<OrbitalComplexDistrictLightPresentation> BlackoutLights()
        => new[]
        {
            Light("BreakerYard", new Color(0.62f, 0.08f, 0.04f), 0.22f),
            Light("QuarantineArchive", new Color(0.08f, 0.24f, 0.34f), 0.12f),
            Light("StormglassArray", new Color(0.04f, 0.16f, 0.22f), 0.08f),
            Light("TideGate", new Color(0.58f, 0.07f, 0.03f), 0.16f)
        };

    private static IReadOnlyList<OrbitalComplexDistrictLightPresentation> EmergencyLights()
        => new[]
        {
            Light("BreakerYard", new Color(0.22f, 0.95f, 0.62f), 1.8f),
            Light("QuarantineArchive", new Color(1.0f, 0.48f, 0.12f), 1.35f),
            Light("StormglassArray", new Color(0.12f, 0.62f, 1.0f), 1.1f),
            Light("TideGate", new Color(1.0f, 0.42f, 0.08f), 1.65f)
        };

    private static IReadOnlyList<OrbitalComplexDistrictLightPresentation> FullPowerLights()
        => new[]
        {
            Light("BreakerYard", new Color(0.28f, 0.9f, 0.72f), 2.2f),
            Light("QuarantineArchive", new Color(0.45f, 0.72f, 1.0f), 2.0f),
            Light("StormglassArray", new Color(1.0f, 0.18f, 0.08f), 2.6f),
            Light("TideGate", new Color(0.18f, 1.0f, 0.58f), 2.4f)
        };

    private static OrbitalComplexDistrictLightPresentation Light(
        string district,
        Color color,
        float energy)
        => new(district, color, energy);
}
