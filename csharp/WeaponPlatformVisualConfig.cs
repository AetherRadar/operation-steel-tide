using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

/// <summary>
/// Centralizes per-platform receiver and furniture color tables so TacticalPlayer,
/// EnemyOperator, and any future consumer share one authoritative palette.
/// </summary>
public static class WeaponPlatformVisualConfig
{
    public readonly record struct PlatformPalette(Color ReceiverColor, Color FurnitureColor);

    private static readonly Dictionary<WeaponPlatform, PlatformPalette> Palettes = new()
    {
        [WeaponPlatform.M4A1] = new(new Color(0.045f, 0.052f, 0.05f), new Color(0.18f, 0.17f, 0.13f)),
        [WeaponPlatform.AK74] = new(new Color(0.12f, 0.105f, 0.075f), new Color(0.24f, 0.12f, 0.055f)),
        [WeaponPlatform.ScarL] = new(new Color(0.34f, 0.29f, 0.2f), new Color(0.29f, 0.255f, 0.18f)),
        [WeaponPlatform.M24] = new(new Color(0.16f, 0.19f, 0.17f), new Color(0.18f, 0.24f, 0.16f)),
        [WeaponPlatform.AXMC] = new(new Color(0.035f, 0.14f, 0.15f), new Color(0.08f, 0.29f, 0.28f)),
        [WeaponPlatform.MP5A5] = new(new Color(0.025f, 0.032f, 0.03f), new Color(0.055f, 0.065f, 0.06f)),
        [WeaponPlatform.M3A1] = new(new Color(0.17f, 0.2f, 0.185f), new Color(0.105f, 0.12f, 0.11f)),
        [WeaponPlatform.P226] = new(new Color(0.055f, 0.06f, 0.065f), new Color(0.075f, 0.08f, 0.085f)),
        [WeaponPlatform.M1911] = new(new Color(0.16f, 0.15f, 0.13f), new Color(0.22f, 0.12f, 0.065f)),
        [WeaponPlatform.AWM] = new(new Color(0.2f, 0.22f, 0.21f), new Color(0.15f, 0.18f, 0.16f)),
        [WeaponPlatform.VSS] = new(new Color(0.075f, 0.1f, 0.075f), new Color(0.16f, 0.24f, 0.14f)),
        [WeaponPlatform.DesertEagle] = new(new Color(0.42f, 0.44f, 0.41f), new Color(0.07f, 0.075f, 0.07f)),
        [WeaponPlatform.GSh18] = new(new Color(0.045f, 0.052f, 0.05f), new Color(0.07f, 0.075f, 0.072f))
    };

    /// <summary>Default palette used when a platform has no explicit entry.</summary>
    private static readonly PlatformPalette DefaultPalette =
        new(new Color(0.045f, 0.052f, 0.05f), new Color(0.18f, 0.17f, 0.13f));

    /// <summary>Returns the canonical receiver + furniture palette for the given platform.</summary>
    public static PlatformPalette For(WeaponPlatform platform)
        => Palettes.GetValueOrDefault(platform, DefaultPalette);

    /// <summary>
    /// Simplified single-color lookup used by third-person operator builds where
    /// the receiver/furniture distinction is collapsed into a single gun material.
    /// Falls back to the receiver color.
    /// </summary>
    public static Color ThirdPersonGunColor(WeaponPlatform platform)
    {
        // Third-person enemy model uses a slightly different sub-palette to give
        // the simplified single-mesh weapon a richer field appearance. The table
        // below preserves the original EnemyOperator palette exactly.
        return platform switch
        {
            WeaponPlatform.AK74 => new Color(0.15f, 0.09f, 0.045f),
            WeaponPlatform.ScarL => new Color(0.3f, 0.25f, 0.17f),
            WeaponPlatform.M24 => new Color(0.16f, 0.21f, 0.13f),
            WeaponPlatform.AXMC => new Color(0.035f, 0.23f, 0.22f),
            WeaponPlatform.MP5A5 => new Color(0.035f, 0.045f, 0.043f),
            WeaponPlatform.M3A1 => new Color(0.18f, 0.21f, 0.19f),
            _ => new Color(0.018f, 0.023f, 0.022f)
        };
    }
}
