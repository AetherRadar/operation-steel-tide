using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public enum MeleeWeaponStyle
{
    TacticalKnife,
    ZhanmaDao,
    TianxuanDao
}

internal readonly record struct MeleePose(Vector3 Position, Vector3 Rotation);

internal readonly record struct MeleeAttackDefinition(
    string Id,
    float Duration,
    float HitProgress,
    float DamageMultiplier,
    MeleePose Windup,
    MeleePose FollowThrough,
    int SweepSamples,
    int MaxTargets);

internal readonly record struct MeleePresentationProfile(
    MeleePose Rest,
    MeleePose DrawStart,
    MeleePose DrawFlourish,
    float DrawDuration,
    float PresentationScale,
    float ArmPresentationScale);

internal static class MeleeAttackCatalog
{
    public const float ComboWindowDuration = 0.72f;

    private static readonly IReadOnlyDictionary<MeleeWeaponStyle, MeleeAttackDefinition[]> Attacks =
        new Dictionary<MeleeWeaponStyle, MeleeAttackDefinition[]>
        {
            [MeleeWeaponStyle.TacticalKnife] =
            [
                Attack(
                    "knife_crosscut", 0.58f, 0.42f, 1.0f,
                    new(0.46f, -0.34f, -0.48f), new(0.2f, 0.18f, 0.56f),
                    new(-0.08f, 0.08f, -0.86f), new(0.48f, -0.82f, -0.58f),
                    4, 1),
                Attack(
                    "knife_reverse", 0.56f, 0.4f, 0.96f,
                    new(-0.22f, -0.18f, -0.62f), new(0.08f, -0.52f, -0.62f),
                    new(0.36f, 0.02f, -0.88f), new(-0.44f, 0.72f, 0.55f),
                    4, 1),
                Attack(
                    "knife_thrust", 0.62f, 0.48f, 1.12f,
                    new(0.31f, -0.31f, -0.37f), new(-0.25f, 0.08f, 0.18f),
                    new(0.04f, -0.08f, -1.04f), new(0.02f, -0.08f, -0.04f),
                    3, 1)
            ],
            [MeleeWeaponStyle.ZhanmaDao] =
            [
                Attack(
                    "zhanma_heavy_crosscut", 0.88f, 0.47f, 1.2f,
                    new(0.55f, -0.4f, -0.36f), new(0.28f, 0.34f, 0.94f),
                    new(-0.34f, 0.12f, -1.02f), new(0.78f, -1.12f, -0.84f),
                    7, 2),
                Attack(
                    "zhanma_rising_return", 0.82f, 0.44f, 1.08f,
                    new(-0.38f, -0.18f, -0.72f), new(0.24f, -0.64f, -0.82f),
                    new(0.42f, 0.02f, -0.94f), new(-0.54f, 0.94f, 0.7f),
                    7, 2),
                Attack(
                    "zhanma_overhead_breaker", 1.02f, 0.53f, 1.38f,
                    new(0.04f, 0.18f, -0.3f), new(-1.02f, 0.02f, 0.08f),
                    new(0.02f, -0.16f, -1.15f), new(1.12f, 0.02f, -0.02f),
                    6, 3)
            ],
            [MeleeWeaponStyle.TianxuanDao] =
            [
                Attack(
                    "tianxuan_flashcut", 0.66f, 0.4f, 1.04f,
                    new(0.48f, -0.28f, -0.45f), new(0.18f, 0.32f, 0.78f),
                    new(-0.28f, 0.08f, -1.0f), new(0.62f, -1.0f, -0.76f),
                    7, 2),
                Attack(
                    "tianxuan_moon_return", 0.62f, 0.38f, 0.98f,
                    new(-0.3f, -0.12f, -0.7f), new(0.18f, -0.58f, -0.7f),
                    new(0.4f, 0.0f, -0.96f), new(-0.42f, 0.96f, 0.66f),
                    7, 2),
                Attack(
                    "tianxuan_starfall", 0.74f, 0.46f, 1.18f,
                    new(0.14f, 0.14f, -0.38f), new(-0.86f, -0.24f, 0.26f),
                    new(-0.02f, -0.12f, -1.12f), new(0.98f, 0.28f, -0.2f),
                    7, 2)
            ]
        };

    private static readonly IReadOnlyDictionary<MeleeWeaponStyle, MeleePresentationProfile> Presentations =
        new Dictionary<MeleeWeaponStyle, MeleePresentationProfile>
        {
            [MeleeWeaponStyle.TacticalKnife] = new(
                new(new Vector3(0.32f, -0.36f, -0.46f), new Vector3(0.68f, 0.42f, 0.82f)),
                new(new Vector3(0.56f, -0.68f, -0.2f), new Vector3(-0.72f, 0.4f, 1.16f)),
                new(new Vector3(0.08f, -0.16f, -0.58f), new Vector3(0.92f, 0.62f, -0.54f)),
                0.46f,
                0.96f,
                0.74f),
            [MeleeWeaponStyle.ZhanmaDao] = new(
                new(new Vector3(0.34f, -0.36f, -0.48f), new Vector3(0.78f, 0.48f, 0.82f)),
                new(new Vector3(0.62f, -0.8f, -0.08f), new Vector3(-1.04f, 0.52f, 1.3f)),
                new(new Vector3(0.04f, -0.12f, -0.6f), new Vector3(1.02f, 0.68f, -0.48f)),
                0.72f,
                0.78f,
                0.74f),
            [MeleeWeaponStyle.TianxuanDao] = new(
                new(new Vector3(0.34f, -0.36f, -0.48f), new Vector3(0.76f, 0.55f, 0.78f)),
                new(new Vector3(0.58f, -0.76f, -0.12f), new Vector3(-0.92f, 0.58f, 1.22f)),
                new(new Vector3(-0.02f, -0.08f, -0.62f), new Vector3(0.94f, 0.82f, -0.72f)),
                0.62f,
                0.75f,
                0.74f)
        };

    public static MeleeAttackDefinition AttackFor(MeleeWeaponStyle style, int comboIndex)
    {
        var attacks = Attacks[style];
        return attacks[Math.Abs(comboIndex) % attacks.Length];
    }

    public static int AttackCount(MeleeWeaponStyle style) => Attacks[style].Length;

    public static MeleePresentationProfile PresentationFor(MeleeWeaponStyle style)
        => Presentations[style];

    private static MeleeAttackDefinition Attack(
        string id,
        float duration,
        float hitProgress,
        float damageMultiplier,
        Vector3 windupPosition,
        Vector3 windupRotation,
        Vector3 followPosition,
        Vector3 followRotation,
        int sweepSamples,
        int maxTargets)
        => new(
            id,
            duration,
            hitProgress,
            damageMultiplier,
            new MeleePose(windupPosition, windupRotation),
            new MeleePose(followPosition, followRotation),
            sweepSamples,
            maxTargets);
}
