using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public readonly record struct DemolitionArenaBox(
    string Name,
    Vector3 Center,
    Vector3 Size,
    string Material,
    Vector3 Rotation = default);

public readonly record struct DemolitionArenaProp(
    string Name,
    string ScenePath,
    Vector3 Position,
    float Yaw,
    float Scale,
    Vector3 CollisionSize,
    Vector3 CollisionOffset);

public readonly record struct DemolitionArenaMarker(
    Vector3 Position,
    string LocalizationKey,
    string EnglishName,
    Color Accent);

/// <summary>
/// Authored Tideforge arena coordinates and pure balance checks. This data type owns no
/// scene nodes, so route timing and clearance rules can be validated without a running tree.
/// </summary>
public sealed class DemolitionArenaLayout
{
    public const float MinimumPassageWidth = 2.6f;
    public const float MinimumPassageHeight = 2.45f;
    public const float MaximumSiteTravelDifference = 0.08f;

    public static readonly Vector3 WorldOrigin = new(285.0f, 0.0f, -35.0f);

    public string EnglishName => "TIDEFORGE ARENA";
    public string LocalizationKey => "demolition_arena_name";
    public Vector3 Origin { get; }
    public Vector3 AttackSpawn { get; }
    public Vector3 DefenderSpawn { get; }
    public Vector3 Midpoint { get; }
    public Rect2 WorldBounds { get; }
    public IReadOnlyList<Vector3> SitePositions { get; }
    public IReadOnlyList<Vector3> DefenderSpawns { get; }
    public IReadOnlyList<Vector3> CoverPoints { get; }
    public IReadOnlyList<DemolitionArenaBox> CollisionBoxes { get; }
    public IReadOnlyList<DemolitionArenaBox> DetailBoxes { get; }
    public IReadOnlyList<DemolitionArenaProp> Props { get; }
    public IReadOnlyList<DemolitionArenaMarker> Markers { get; }
    public IReadOnlyList<Vector3> AttackToAPath { get; }
    public IReadOnlyList<Vector3> AttackToBPath { get; }
    public IReadOnlyList<Vector3> AttackMidPath { get; }
    public IReadOnlyList<Vector3> SiteRotationPath { get; }
    public IReadOnlyList<float> CriticalPassageWidths { get; }
    public IReadOnlyList<float> CriticalPassageHeights { get; }

    public DemolitionArenaLayout(Vector3? origin = null)
    {
        Origin = origin ?? WorldOrigin;
        AttackSpawn = World(new Vector3(0.0f, 0.22f, 34.0f));
        DefenderSpawn = World(new Vector3(0.0f, 0.22f, -36.0f));
        Midpoint = World(new Vector3(0.0f, 0.12f, 1.0f));
        WorldBounds = new Rect2(Origin.X - 41.0f, Origin.Z - 42.0f, 82.0f, 84.0f);

        SitePositions = WorldPoints(
            new(-25.0f, 0.18f, -18.0f),
            new(25.0f, 0.18f, -18.0f));
        DefenderSpawns = WorldPoints(
            new(-30.5f, 0.22f, -32.0f),
            new(-22.0f, 0.22f, -33.0f),
            new(-11.5f, 0.22f, -38.0f),
            new(11.5f, 0.22f, -38.0f),
            new(22.0f, 0.22f, -33.0f),
            new(30.5f, 0.22f, -32.0f),
            new(0.0f, 0.22f, -37.0f));
        CoverPoints = WorldPoints(
            new(-31.2f, 0.2f, 11.0f), new(-25.8f, 0.2f, 5.0f),
            new(-31.0f, 0.2f, -15.0f), new(-19.5f, 0.2f, -19.0f),
            new(-13.8f, 0.2f, -4.0f), new(-7.0f, 0.2f, 3.5f),
            new(7.0f, 0.2f, 3.5f), new(13.8f, 0.2f, -4.0f),
            new(19.5f, 0.2f, -19.0f), new(31.0f, 0.2f, -15.0f),
            new(25.8f, 0.2f, 5.0f), new(31.2f, 0.2f, 11.0f),
            new(-11.2f, 0.2f, 21.0f), new(11.2f, 0.2f, 21.0f),
            new(-6.0f, 0.2f, -20.0f), new(6.0f, 0.2f, -20.0f));

        CollisionBoxes = BuildCollisionBoxes();
        DetailBoxes = BuildDetailBoxes();
        Props = BuildProps();
        Markers = BuildMarkers();
        AttackToAPath = WorldPoints(
            new(0, 0.2f, 34), new(0, 0.2f, 26), new(0, 0.2f, 13),
            new(-12, 0.2f, 10), new(-17, 0.2f, -2), new(-23, 0.2f, -3),
            new(-23, 0.2f, -9), new(-25, 0.2f, -18));
        AttackToBPath = WorldPoints(
            new(0, 0.2f, 34), new(0, 0.2f, 26), new(0, 0.2f, 13),
            new(12, 0.2f, 10), new(17, 0.2f, -2), new(25, 0.2f, -3),
            new(25, 0.2f, -9), new(25, 0.2f, -18));
        AttackMidPath = WorldPoints(
            new(0, 0.2f, 34), new(0, 0.2f, 26), new(0, 0.2f, 13),
            new(0, 0.2f, 1));
        SiteRotationPath = WorldPoints(
            new(-25, 0.2f, -18), new(-23, 0.2f, -9), new(-23, 0.2f, -3),
            new(-14.5f, 0.2f, 1), new(-8, 0.2f, 1), new(0, 0.2f, 1),
            new(8, 0.2f, 1), new(14.5f, 0.2f, 1),
            new(25, 0.2f, -3), new(25, 0.2f, -9), new(25, 0.2f, -18));
        CriticalPassageWidths = new[] { 3.8f, 4.2f, 4.5f, 5.2f, 6.0f };
        CriticalPassageHeights = new[] { 2.7f, 3.2f, 4.2f, 6.0f };
    }

    public float AttackToALength => PathLength(AttackToAPath);
    public float AttackToBLength => PathLength(AttackToBPath);
    public float RotationLength => PathLength(SiteRotationPath);
    public float SiteTravelDifferenceRatio
        => Mathf.Abs(AttackToALength - AttackToBLength) / Mathf.Max(AttackToALength, AttackToBLength);
    public bool HasBalancedSiteTravel => SiteTravelDifferenceRatio <= MaximumSiteTravelDifference;
    public bool HasThreeAttackRoutes => AttackToAPath.Count >= 5
        && AttackToBPath.Count >= 5
        && AttackMidPath.Count >= 4;
    public bool HasPlayerClearance
        => AllAtLeast(CriticalPassageWidths, MinimumPassageWidth)
        && AllAtLeast(CriticalPassageHeights, MinimumPassageHeight);

    public bool HasCapsuleClearance(IReadOnlyList<Vector3> route, out string blockerName)
    {
        blockerName = "none";
        if (route.Count < 2)
        {
            blockerName = "missing_route";
            return false;
        }

        for (var segment = 1; segment < route.Count; segment++)
        {
            var start = route[segment - 1];
            var end = route[segment];
            var samples = Mathf.Max(1, Mathf.CeilToInt(start.DistanceTo(end) / 0.2f));
            for (var sample = 0; sample <= samples; sample++)
            {
                var position = start.Lerp(end, sample / (float)samples);
                if (!TryFindCapsuleBlocker(position, out blockerName))
                {
                    continue;
                }
                blockerName = $"{blockerName}@{segment}:{sample}";
                return false;
            }
        }
        return true;
    }

    public Vector3 SitePosition(int index) => SitePositions[Mathf.Clamp(index, 0, SitePositions.Count - 1)];

    public bool IsInsideArena(Vector3 worldPosition, float margin = 0.0f)
    {
        return worldPosition.X >= WorldBounds.Position.X - margin
            && worldPosition.X <= WorldBounds.End.X + margin
            && worldPosition.Z >= WorldBounds.Position.Y - margin
            && worldPosition.Z <= WorldBounds.End.Y + margin;
    }

    public bool HasSpawnSightlineToSite(int siteIndex)
    {
        var site = SitePosition(siteIndex);
        var direction = new Vector2(site.X - AttackSpawn.X, site.Z - AttackSpawn.Z);
        if (direction.LengthSquared() <= 0.01f)
        {
            return true;
        }
        foreach (var wall in CollisionBoxes)
        {
            if (!wall.Name.StartsWith("SightBlock", StringComparison.Ordinal))
            {
                continue;
            }
            var center = new Vector2(wall.Center.X, wall.Center.Z);
            var half = new Vector2(wall.Size.X, wall.Size.Z) * 0.5f;
            if (SegmentIntersectsRect(
                    new Vector2(AttackSpawn.X, AttackSpawn.Z),
                    new Vector2(site.X, site.Z),
                    new Rect2(center - half, half * 2.0f)))
            {
                return false;
            }
        }
        return true;
    }

    private IReadOnlyList<DemolitionArenaBox> BuildCollisionBoxes()
    {
        var boxes = new List<DemolitionArenaBox>
        {
            Box("ArenaFloor", new(0, -0.48f, 0), new(80, 1.0f, 82), "ground"),
            Box("NorthPerimeter", new(0, 2.5f, -41), new(80, 5.0f, 1.0f), "concrete_dark"),
            Box("SouthPerimeterLeft", new(-24, 2.5f, 41), new(32, 5.0f, 1.0f), "concrete_dark"),
            Box("SouthPerimeterRight", new(24, 2.5f, 41), new(32, 5.0f, 1.0f), "concrete_dark"),
            Box("WestPerimeter", new(-40, 2.5f, 0), new(1.0f, 5.0f, 82), "concrete_dark"),
            Box("EastPerimeter", new(40, 2.5f, 0), new(1.0f, 5.0f, 82), "concrete_dark"),

            Box("SightBlockLeft", new(-8.8f, 1.65f, 22.5f), new(1.1f, 3.3f, 16.0f), "rust"),
            Box("SightBlockRight", new(8.8f, 1.65f, 22.5f), new(1.1f, 3.3f, 16.0f), "rust"),
            Box("SpawnGateLeft", new(-14.4f, 1.6f, 32.0f), new(13.0f, 3.2f, 1.0f), "concrete_dark"),
            Box("SpawnGateRight", new(14.4f, 1.6f, 32.0f), new(13.0f, 3.2f, 1.0f), "concrete_dark"),

            Box("WestRouteWall", new(-19.0f, 1.7f, 12.0f), new(1.0f, 3.4f, 23.0f), "steel_dark"),
            Box("EastRouteWall", new(19.0f, 1.7f, 12.0f), new(1.0f, 3.4f, 23.0f), "steel_dark"),
            Box("MidDividerLeft", new(-8.0f, 1.7f, -9.0f), new(12.0f, 3.4f, 1.0f), "steel_dark"),
            Box("MidDividerRight", new(8.0f, 1.7f, -9.0f), new(12.0f, 3.4f, 1.0f), "steel_dark"),
            Box("MidFoundryCore", new(0, 2.25f, -13.5f), new(8.0f, 4.5f, 8.0f), "rust"),

            Box("FoundryWestWall", new(-34.0f, 3.0f, -18.0f), new(1.0f, 6.0f, 24.0f), "concrete_dark"),
            Box("FoundryNorthWallLeft", new(-30.5f, 3.0f, -29.5f), new(7.0f, 6.0f, 1.0f), "concrete_dark"),
            Box("FoundryNorthWallRight", new(-19.5f, 3.0f, -29.5f), new(7.0f, 6.0f, 1.0f), "concrete_dark"),
            Box("FoundrySouthWall", new(-29.5f, 2.2f, -6.5f), new(9.0f, 4.4f, 1.0f), "concrete_dark"),
            Box("FoundrySouthReturn", new(-18.5f, 2.2f, -6.5f), new(4.0f, 4.4f, 1.0f), "concrete_dark"),
            Box("FoundryFurnace", new(-27.8f, 2.2f, -21.8f), new(4.6f, 4.4f, 5.0f), "rust"),

            Box("AssemblyEastWall", new(34.0f, 3.5f, -18.0f), new(1.0f, 7.0f, 24.0f), "steel_dark"),
            Box("AssemblyNorthWallLeft", new(19.5f, 3.5f, -29.5f), new(7.0f, 7.0f, 1.0f), "steel_dark"),
            Box("AssemblyNorthWallRight", new(30.5f, 3.5f, -29.5f), new(7.0f, 7.0f, 1.0f), "steel_dark"),
            Box("AssemblySouthLeft", new(20.0f, 3.5f, -6.5f), new(5.0f, 7.0f, 1.0f), "steel_dark"),
            Box("AssemblySouthRight", new(31.0f, 3.5f, -6.5f), new(6.0f, 7.0f, 1.0f), "steel_dark"),
            Box("AssemblyRoof", new(25.0f, 7.0f, -18.0f), new(19.0f, 0.4f, 24.0f), "steel"),
            Box("AssemblyMachine", new(28.5f, 1.35f, -18.8f), new(4.5f, 2.7f, 6.5f), "steel"),

            Box("DefenderGateLeft", new(-11.0f, 1.65f, -34.0f), new(14.0f, 3.3f, 1.0f), "concrete_dark"),
            Box("DefenderGateRight", new(11.0f, 1.65f, -34.0f), new(14.0f, 3.3f, 1.0f), "concrete_dark")
        };
        return boxes;
    }

    private IReadOnlyList<DemolitionArenaBox> BuildDetailBoxes()
    {
        var boxes = new List<DemolitionArenaBox>
        {
            Box("AttackApron", new(0, 0.035f, 34), new(18, 0.07f, 9), "spawn_floor"),
            Box("AttackBorderNorth", new(0, 0.08f, 29.6f), new(18, 0.05f, 0.16f), "warning"),
            Box("AttackBorderSouth", new(0, 0.08f, 38.4f), new(18, 0.05f, 0.16f), "warning"),
            Box("AttackBorderLeft", new(-8.9f, 0.08f, 34), new(0.16f, 0.05f, 8.8f), "warning"),
            Box("AttackBorderRight", new(8.9f, 0.08f, 34), new(0.16f, 0.05f, 8.8f), "warning"),
            Box("MidLaneSurface", new(0, 0.04f, 4.0f), new(6.2f, 0.08f, 26), "mid_floor"),
            Box("MidGuideLeft", new(-3.0f, 0.09f, 4.0f), new(0.12f, 0.04f, 25), "marking"),
            Box("MidGuideRight", new(3.0f, 0.09f, 4.0f), new(0.12f, 0.04f, 25), "marking"),
            Box("FoundryFloor", new(-25, 0.045f, -18), new(17, 0.09f, 22), "foundry_floor"),
            Box("FoundryCanopyWest", new(-31.7f, 5.8f, -18), new(3.6f, 0.3f, 22), "rust"),
            Box("FoundryCanopyNorth", new(-24.5f, 5.8f, -27.2f), new(10.8f, 0.3f, 3.6f), "rust"),
            Box("AssemblyFloor", new(25, 0.045f, -18), new(18, 0.09f, 23), "assembly_floor"),
            Box("AssemblyRoofStripe", new(25, 7.23f, -18), new(15, 0.07f, 2.0f), "warning"),
            Box("AssemblyWindowBand", new(34.54f, 4.4f, -18), new(0.05f, 1.4f, 15), "window"),
            Box("DefenderApron", new(0, 0.04f, -36), new(18, 0.08f, 7.5f), "spawn_floor"),
            Box("DefenderBorder", new(0, 0.09f, -32.3f), new(18, 0.04f, 0.16f), "cyan"),
            Box("MidPipeRackTop", new(0, 5.8f, -1), new(16, 0.35f, 1.2f), "steel"),
            Box("MidPipeRackLeft", new(-7.2f, 2.9f, -1), new(0.45f, 5.8f, 0.45f), "steel"),
            Box("MidPipeRackRight", new(7.2f, 2.9f, -1), new(0.45f, 5.8f, 0.45f), "steel"),
            Box("DefenderSignBeam", new(0, 4.5f, -34), new(8.0f, 0.3f, 0.5f), "warning")
        };
        for (var index = 0; index < 6; index++)
        {
            boxes.Add(Box($"WestLaneStripe_{index}", new(-28.5f, 0.04f, 25 - index * 6), new(0.18f, 0.08f, 3.2f), "warning"));
            boxes.Add(Box($"EastLaneStripe_{index}", new(28.5f, 0.04f, 25 - index * 6), new(0.18f, 0.08f, 3.2f), "warning"));
        }
        return boxes;
    }

    private IReadOnlyList<DemolitionArenaProp> BuildProps()
    {
        const string barrier = "res://assets/models/concrete_road_barrier/concrete_road_barrier.gltf";
        const string crate = "res://assets/models/old_military_crate/old_military_crate.gltf";
        var props = new List<DemolitionArenaProp>();
        var barrierPositions = new[]
        {
            new Vector3(-30.5f, 0.02f, 11.0f), new(-24.5f, 0.02f, 4.5f),
            new(30.5f, 0.02f, 11.0f), new(24.5f, 0.02f, 4.5f),
            new(-12.5f, 0.02f, -3.5f), new(12.5f, 0.02f, -3.5f)
        };
        for (var index = 0; index < barrierPositions.Length; index++)
        {
            props.Add(new DemolitionArenaProp(
                $"ArenaBarrier_{index + 1:00}", barrier, World(barrierPositions[index]),
                index % 2 == 0 ? 0.12f : Mathf.Pi * 0.5f, 1.18f,
                new Vector3(1.55f, 0.84f, 0.64f), new Vector3(0, 0.41f, 0)));
        }
        var cratePositions = new[]
        {
            new Vector3(-29.0f, 0.02f, -14.5f), new(-20.5f, 0.02f, -21.0f),
            new(20.0f, 0.02f, -14.0f), new(30.0f, 0.02f, -23.0f),
            new(-10.5f, 0.02f, 20.5f), new(10.5f, 0.02f, 20.5f),
            new(-5.8f, 0.02f, -20.5f), new(5.8f, 0.02f, -20.5f)
        };
        for (var index = 0; index < cratePositions.Length; index++)
        {
            props.Add(new DemolitionArenaProp(
                $"ArenaCrate_{index + 1:00}", crate, World(cratePositions[index]),
                index * 0.31f, 1.5f,
                new Vector3(0.82f, 0.42f, 0.68f), new Vector3(-0.06f, 0.21f, 0.1f)));
        }
        return props;
    }

    private IReadOnlyList<DemolitionArenaMarker> BuildMarkers()
    {
        return new[]
        {
            new DemolitionArenaMarker(AttackSpawn, "demolition_minimap_attack", "ATTACK", new Color(0.34f, 0.8f, 1.0f)),
            new DemolitionArenaMarker(Midpoint, "demolition_minimap_mid", "MID", new Color(0.88f, 0.82f, 0.56f)),
            new DemolitionArenaMarker(SitePositions[0], "demolition_minimap_a", "A", new Color(1.0f, 0.46f, 0.17f)),
            new DemolitionArenaMarker(SitePositions[1], "demolition_minimap_b", "B", new Color(1.0f, 0.46f, 0.17f)),
            new DemolitionArenaMarker(DefenderSpawn, "demolition_minimap_defend", "DEFEND", new Color(0.4f, 0.92f, 0.64f))
        };
    }

    private DemolitionArenaBox Box(string name, Vector3 center, Vector3 size, string material, Vector3 rotation = default)
        => new(name, World(center), size, material, rotation);

    private Vector3 World(Vector3 local) => Origin + local;

    private IReadOnlyList<Vector3> WorldPoints(params Vector3[] points)
    {
        var world = new Vector3[points.Length];
        for (var index = 0; index < points.Length; index++)
        {
            world[index] = World(points[index]);
        }
        return world;
    }

    private static float PathLength(IReadOnlyList<Vector3> points)
    {
        var length = 0.0f;
        for (var index = 1; index < points.Count; index++)
        {
            length += points[index - 1].DistanceTo(points[index]);
        }
        return length;
    }

    private bool TryFindCapsuleBlocker(Vector3 feetPosition, out string blockerName)
    {
        const float radius = 0.38f;
        const float centerOffset = 0.9f;
        const float halfHeight = 0.875f;
        var capsuleBottom = feetPosition.Y + centerOffset - halfHeight;
        var capsuleTop = feetPosition.Y + centerOffset + halfHeight;
        foreach (var box in CollisionBoxes)
        {
            var half = box.Size * 0.5f;
            if (box.Center.Y + half.Y < capsuleBottom || box.Center.Y - half.Y > capsuleTop)
            {
                continue;
            }
            var local = new Basis(Quaternion.FromEuler(box.Rotation)).Inverse() * (feetPosition - box.Center);
            if (Mathf.Abs(local.X) <= half.X + radius && Mathf.Abs(local.Z) <= half.Z + radius)
            {
                blockerName = box.Name;
                return true;
            }
        }

        foreach (var prop in Props)
        {
            var yaw = new Basis(Vector3.Up, prop.Yaw);
            var half = prop.CollisionSize * prop.Scale * 0.5f;
            var center = prop.Position + yaw * (prop.CollisionOffset * prop.Scale);
            if (center.Y + half.Y < capsuleBottom || center.Y - half.Y > capsuleTop)
            {
                continue;
            }
            var local = yaw.Inverse() * (feetPosition - center);
            if (Mathf.Abs(local.X) <= half.X + radius && Mathf.Abs(local.Z) <= half.Z + radius)
            {
                blockerName = prop.Name;
                return true;
            }
        }

        blockerName = "none";
        return false;
    }

    private static bool AllAtLeast(IReadOnlyList<float> values, float minimum)
    {
        for (var index = 0; index < values.Count; index++)
        {
            if (values[index] < minimum)
            {
                return false;
            }
        }
        return true;
    }

    private static bool SegmentIntersectsRect(Vector2 start, Vector2 end, Rect2 rect)
    {
        var direction = end - start;
        var minimum = 0.0f;
        var maximum = 1.0f;
        return Clip(-direction.X, start.X - rect.Position.X, ref minimum, ref maximum)
            && Clip(direction.X, rect.End.X - start.X, ref minimum, ref maximum)
            && Clip(-direction.Y, start.Y - rect.Position.Y, ref minimum, ref maximum)
            && Clip(direction.Y, rect.End.Y - start.Y, ref minimum, ref maximum);
    }

    private static bool Clip(float denominator, float numerator, ref float minimum, ref float maximum)
    {
        if (Mathf.IsZeroApprox(denominator))
        {
            return numerator >= 0.0f;
        }
        var value = numerator / denominator;
        if (denominator < 0.0f)
        {
            if (value > maximum)
            {
                return false;
            }
            minimum = Mathf.Max(minimum, value);
        }
        else
        {
            if (value < minimum)
            {
                return false;
            }
            maximum = Mathf.Min(maximum, value);
        }
        return true;
    }
}
