using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public readonly record struct RefineryModelPlacement(
    string Name,
    string ScenePath,
    Vector3 Position,
    float Yaw,
    float Scale,
    Vector3 CollisionSize,
    Vector3 CollisionOffset,
    float VisibilityRange,
    bool CastShadow,
    bool HasDoorway = false,
    bool IsTallScene = false);

public readonly record struct RefineryLootPlacement(
    Vector3 Position,
    LootGrade Grade,
    string EnglishName,
    string ChineseName);

public sealed record RefineryExtractionMapLayout(
    IReadOnlyList<RefineryModelPlacement> Models,
    IReadOnlyList<Vector3> GarrisonSpawns,
    IReadOnlyList<Vector3> CoverPoints,
    IReadOnlyList<RefineryLootPlacement> LootPlacements,
    IReadOnlyList<(Vector3 Position, ValuableItemKind Kind, LootGrade Grade)> ValuablePlacements);

/// <summary>
/// Authored placement data for Blackwater Refinery. Visual buildings come from licensed
/// model packs; runtime geometry is limited to terrain, lane markings, and box proxies.
/// </summary>
public sealed class RefineryExtractionMapBuilder
{
    private const string KenneyRoot = "res://assets/models/kenney_city_kit_industrial";
    private const string BarrierPath = "res://assets/models/concrete_road_barrier/concrete_road_barrier.gltf";
    private const string CratePath = "res://assets/models/old_military_crate/old_military_crate.gltf";

    public RefineryExtractionMapLayout Build()
    {
        var models = new List<RefineryModelPlacement>
        {
            Kenney("SouthIntakeWest", "building-a.glb", new(-61, 0.02f, 42), 0.0f, 4.8f, new(2.08f, 1.47f, 1.24f)),
            Kenney("SouthIntakeEast", "building-b.glb", new(63, 0.02f, 43), Mathf.Pi, 4.7f, new(2.08f, 1.47f, 1.26f)),
            Kenney("ManifestProcessing", "building-f.glb", new(-51, 0.02f, -8), Mathf.Pi * 0.5f, 4.7f, new(1.79f, 1.93f, 1.28f)),
            Kenney("RelayProcessing", "building-r.glb", new(54, 0.02f, -9), -Mathf.Pi * 0.5f, 4.3f, new(2.48f, 1.39f, 1.27f)),
            Kenney("WestPumpHouse", "building-c.glb", new(-91, 0.02f, -72), 0.0f, 4.9f, new(1.88f, 1.25f, 2.11f)),
            Kenney("EastPumpHouse", "building-l.glb", new(92, 0.02f, -75), Mathf.Pi, 4.8f, new(2.08f, 1.92f, 1.87f)),
            Kenney("NorthCrackingWest", "building-q.glb", new(-67, 0.02f, -124), Mathf.Pi, 4.4f, new(2.14f, 0.88f, 1.77f)),
            Kenney("NorthCrackingEast", "building-e.glb", new(68, 0.02f, -125), 0.0f, 4.6f, new(1.68f, 1.65f, 1.29f)),
            Kenney("BondedStorage", "building-g.glb", new(-31, 0.02f, -164), Mathf.Pi, 4.5f, new(1.68f, 1.28f, 1.28f)),
            Kenney("TurbineHall", "building-n.glb", new(39, 0.02f, -165), 0.0f, 4.5f, new(0.98f, 1.90f, 1.42f)),
            Kenney("WestControl", "building-h.glb", new(-116, 0.02f, -22), Mathf.Pi * 0.5f, 4.5f, new(1.32f, 0.73f, 1.31f)),
            Kenney("EastControl", "building-t.glb", new(117, 0.02f, -25), -Mathf.Pi * 0.5f, 4.4f, new(1.72f, 1.01f, 1.39f)),
            Kenney("WestWorkshop", "building-j.glb", new(-118, 0.02f, -112), Mathf.Pi * 0.5f, 4.2f, new(1.58f, 1.35f, 1.45f)),
            Kenney("EastWorkshop", "building-o.glb", new(119, 0.02f, -115), -Mathf.Pi * 0.5f, 4.2f, new(1.48f, 1.38f, 1.42f)),
            Kenney("WestStack", "chimney-large.glb", new(-78, 0.02f, -101), 0.0f, 5.8f, new(1.0f, 1.70f, 1.0f), 330.0f),
            Kenney("EastStack", "chimney-large.glb", new(80, 0.02f, -103), 0.0f, 5.8f, new(1.0f, 1.70f, 1.0f), 330.0f),
            Kenney("SouthStack", "chimney-medium.glb", new(-35, 0.02f, 28), 0.0f, 5.2f, new(0.9f, 1.55f, 0.9f), 300.0f),
            Kenney("NorthStack", "chimney-medium.glb", new(36, 0.02f, -144), 0.0f, 5.2f, new(0.9f, 1.55f, 0.9f), 300.0f),
            Kenney("WestTank", "detail-tank.glb", new(-73, 0.02f, -57), Mathf.Pi * 0.5f, 5.0f, new(0.85f, 0.42f, 0.52f)),
            Kenney("EastTank", "detail-tank.glb", new(74, 0.02f, -58), -Mathf.Pi * 0.5f, 5.0f, new(0.85f, 0.42f, 0.52f)),
            Kenney("NorthWestTank", "detail-tank.glb", new(-88, 0.02f, -139), Mathf.Pi * 0.5f, 5.2f, new(0.85f, 0.42f, 0.52f)),
            Kenney("NorthEastTank", "detail-tank.glb", new(91, 0.02f, -141), -Mathf.Pi * 0.5f, 5.2f, new(0.85f, 0.42f, 0.52f))
        };

        AddProcessClusters(models);
        AddBarrierLines(models);
        AddCrateClusters(models);

        return new RefineryExtractionMapLayout(
            models,
            GarrisonSpawns(),
            CoverPoints(),
            LootPlacements(),
            ValuablePlacements());
    }

    private static RefineryModelPlacement Kenney(
        string name,
        string file,
        Vector3 position,
        float yaw,
        float scale,
        Vector3 collisionSize,
        float visibilityRange = 260.0f)
        => new(
            name,
            $"{KenneyRoot}/{file}",
            position,
            yaw,
            scale * 1.65f,
            collisionSize,
            new Vector3(0, collisionSize.Y * 0.5f, 0),
            visibilityRange,
            true,
            file.StartsWith("building-", System.StringComparison.OrdinalIgnoreCase),
            false);

    private static RefineryModelPlacement TallKenney(
        string name,
        string file,
        Vector3 position,
        float yaw,
        float scale)
    {
        var collisionSize = file switch
        {
            "building-p.glb" => new Vector3(1.82f, 1.3f, 1.48f),
            "building-q.glb" => new Vector3(2.14f, 0.88f, 1.77f),
            "building-r.glb" => new Vector3(2.48f, 1.39f, 1.27f),
            "building-t.glb" => new Vector3(1.72f, 1.01f, 1.39f),
            _ => throw new System.ArgumentOutOfRangeException(
                nameof(file),
                file,
                "Tall refinery building has no measured collision profile.")
        };
        return new RefineryModelPlacement(
            name,
            $"{KenneyRoot}/{file}",
            position,
            yaw,
            scale * 1.65f,
            collisionSize,
            new Vector3(0, collisionSize.Y * 0.5f, 0),
            360.0f,
            true,
            true,
            true);
    }

    private static void AddProcessClusters(List<RefineryModelPlacement> models)
    {
        models.AddRange(new[]
        {
            Kenney("SouthGateWest", "building-d.glb", new(-116, 0.02f, 58), 0.0f, 5.0f, new(1.78f, 1.2f, 1.46f)),
            Kenney("SouthGateMidWest", "building-i.glb", new(-92, 0.02f, 57), Mathf.Pi, 4.8f, new(1.62f, 1.08f, 1.38f)),
            Kenney("SouthGateMidEast", "building-k.glb", new(92, 0.02f, 56), 0.0f, 4.9f, new(1.68f, 1.14f, 1.42f)),
            Kenney("SouthGateEast", "building-m.glb", new(116, 0.02f, 58), Mathf.Pi, 5.1f, new(1.92f, 1.36f, 1.55f)),
            Kenney("ManifestAnnex", "building-p.glb", new(-76, 0.02f, 20), Mathf.Pi * 0.5f, 5.0f, new(1.82f, 1.3f, 1.48f)),
            Kenney("WestMeterHouse", "building-s.glb", new(-102, 0.02f, 16), -Mathf.Pi * 0.5f, 4.9f, new(1.64f, 1.08f, 1.44f)),
            Kenney("RelayAnnex", "building-d.glb", new(76, 0.02f, 18), -Mathf.Pi * 0.5f, 5.0f, new(1.78f, 1.2f, 1.46f)),
            Kenney("EastMeterHouse", "building-i.glb", new(102, 0.02f, 14), Mathf.Pi * 0.5f, 4.9f, new(1.62f, 1.08f, 1.38f)),
            Kenney("WestCompressor", "building-m.glb", new(-123, 0.02f, -55), Mathf.Pi * 0.5f, 5.2f, new(1.92f, 1.36f, 1.55f)),
            Kenney("WestSeparator", "building-p.glb", new(-105, 0.02f, -85), 0.0f, 5.0f, new(1.82f, 1.3f, 1.48f)),
            Kenney("EastCompressor", "building-s.glb", new(122, 0.02f, -54), -Mathf.Pi * 0.5f, 5.2f, new(1.64f, 1.08f, 1.44f)),
            Kenney("EastSeparator", "building-k.glb", new(106, 0.02f, -88), Mathf.Pi, 5.0f, new(1.68f, 1.14f, 1.42f)),
            Kenney("CrackingServiceWest", "building-a.glb", new(-70, 0.02f, -104), Mathf.Pi, 5.3f, new(2.08f, 1.47f, 1.24f)),
            Kenney("CrackingPipeWest", "building-b.glb", new(-39, 0.02f, -101), 0.0f, 5.1f, new(2.08f, 1.47f, 1.26f)),
            Kenney("CrackingPipeEast", "building-c.glb", new(42, 0.02f, -105), Mathf.Pi, 5.2f, new(1.88f, 1.25f, 2.11f)),
            Kenney("CrackingServiceEast", "building-l.glb", new(72, 0.02f, -105), 0.0f, 5.2f, new(2.08f, 1.92f, 1.87f)),
            Kenney("NorthServiceWest", "building-j.glb", new(-106, 0.02f, -151), Mathf.Pi * 0.5f, 5.1f, new(1.58f, 1.35f, 1.45f)),
            Kenney("BondedStorageAnnex", "building-h.glb", new(-72, 0.02f, -158), Mathf.Pi, 5.0f, new(1.32f, 0.73f, 1.31f)),
            Kenney("TurbineHallAnnex", "building-t.glb", new(78, 0.02f, -159), 0.0f, 5.0f, new(1.72f, 1.01f, 1.39f)),
            Kenney("NorthServiceEast", "building-o.glb", new(108, 0.02f, -151), -Mathf.Pi * 0.5f, 5.1f, new(1.48f, 1.38f, 1.42f)),
            Kenney("IntakeCorridorWest", "building-h.glb", new(-24, 0.02f, 45), Mathf.Pi * 0.5f, 4.6f, new(1.32f, 0.73f, 1.31f)),
            Kenney("IntakeCorridorEast", "building-t.glb", new(24, 0.02f, 44), -Mathf.Pi * 0.5f, 4.6f, new(1.72f, 1.01f, 1.39f)),
            Kenney("MeterCorridorWest", "building-i.glb", new(-23, 0.02f, -5), 0.0f, 4.5f, new(1.62f, 1.08f, 1.38f)),
            Kenney("MeterCorridorEast", "building-k.glb", new(23, 0.02f, -7), Mathf.Pi, 4.5f, new(1.68f, 1.14f, 1.42f)),
            Kenney("SeparatorCorridorWest", "building-d.glb", new(-51, 0.02f, -91), Mathf.Pi * 0.5f, 4.7f, new(1.78f, 1.2f, 1.46f)),
            Kenney("SeparatorCorridorEast", "building-s.glb", new(51, 0.02f, -93), -Mathf.Pi * 0.5f, 4.7f, new(1.64f, 1.08f, 1.44f)),
            Kenney("TurbineCorridorWest", "building-j.glb", new(-51, 0.02f, -146), 0.0f, 4.8f, new(1.58f, 1.35f, 1.45f)),
            Kenney("TurbineCorridorEast", "building-o.glb", new(51, 0.02f, -148), Mathf.Pi, 4.8f, new(1.48f, 1.38f, 1.42f))
        });

        // Perimeter skyline landmarks add readable vertical scale without blocking the
        // central vehicle lanes. They reuse the CC0 industrial kit and keep real entry
        // openings so these are playable structures rather than flat backdrops.
        models.AddRange(new[]
        {
            TallKenney("SkylineTowerWestSouth", "building-r.glb", new(-148, 0.02f, 52), 0.0f, 8.6f),
            TallKenney("SkylineTowerEastSouth", "building-t.glb", new(148, 0.02f, 50), Mathf.Pi, 8.4f),
            TallKenney("SkylineTowerWestMid", "building-q.glb", new(-151, 0.02f, -42), Mathf.Pi * 0.5f, 8.8f),
            TallKenney("SkylineTowerEastMid", "building-p.glb", new(151, 0.02f, -45), -Mathf.Pi * 0.5f, 8.8f),
            TallKenney("SkylineTowerWestNorth", "building-r.glb", new(-148, 0.02f, -171), Mathf.Pi, 9.2f),
            TallKenney("SkylineTowerEastNorth", "building-t.glb", new(148, 0.02f, -173), 0.0f, 9.0f)
        });

        var tankPositions = new[]
        {
            new Vector3(-127, 0.02f, 29), new Vector3(-107, 0.02f, 31), new Vector3(-87, 0.02f, 31),
            new Vector3(87, 0.02f, 30), new Vector3(108, 0.02f, 31), new Vector3(128, 0.02f, 29),
            new Vector3(-132, 0.02f, -105), new Vector3(-101, 0.02f, -108),
            new Vector3(102, 0.02f, -110), new Vector3(132, 0.02f, -106),
            new Vector3(-52, 0.02f, -116), new Vector3(53, 0.02f, -118)
        };
        for (var index = 0; index < tankPositions.Length; index++)
        {
            models.Add(Kenney(
                $"ProcessTank_{index + 1:00}",
                "detail-tank.glb",
                tankPositions[index],
                index % 2 == 0 ? Mathf.Pi * 0.5f : -Mathf.Pi * 0.5f,
                5.4f,
                new Vector3(0.85f, 0.42f, 0.52f),
                210.0f));
        }

        var stackPositions = new[]
        {
            new Vector3(-139, 0.02f, -37), new Vector3(139, 0.02f, -40),
            new Vector3(-86, 0.02f, -137), new Vector3(88, 0.02f, -139),
            new Vector3(-19, 0.02f, -168), new Vector3(20, 0.02f, -169)
        };
        for (var index = 0; index < stackPositions.Length; index++)
        {
            models.Add(Kenney(
                $"ProcessStack_{index + 1:00}",
                index < 2 ? "chimney-medium.glb" : "chimney-small.glb",
                stackPositions[index],
                0.0f,
                5.6f,
                new Vector3(0.9f, 1.55f, 0.9f),
                300.0f));
        }
    }

    private static void AddBarrierLines(List<RefineryModelPlacement> models)
    {
        var placements = new (Vector3 Position, float Yaw)[]
        {
            (new(-18, 0.02f, 28), 0.08f), (new(-14, 0.02f, 28.4f), 0.08f),
            (new(17, 0.02f, 14), -0.1f), (new(21, 0.02f, 13.6f), -0.1f),
            (new(-37, 0.02f, -43), Mathf.Pi * 0.5f), (new(-37.4f, 0.02f, -47), Mathf.Pi * 0.5f),
            (new(38, 0.02f, -84), Mathf.Pi * 0.5f), (new(38.4f, 0.02f, -88), Mathf.Pi * 0.5f),
            (new(-20, 0.02f, -133), 0.0f), (new(-16, 0.02f, -133), 0.0f),
            (new(18, 0.02f, -181), 0.0f), (new(22, 0.02f, -181), 0.0f)
        };
        for (var index = 0; index < placements.Length; index++)
        {
            models.Add(new RefineryModelPlacement(
                $"RefineryBarrier_{index + 1:00}",
                BarrierPath,
                placements[index].Position,
                placements[index].Yaw,
                1.18f,
                new Vector3(1.55f, 0.84f, 0.64f),
                new Vector3(0, 0.41f, 0),
                110.0f,
                index < 4));
        }
    }

    private static void AddCrateClusters(List<RefineryModelPlacement> models)
    {
        var placements = new[]
        {
            new Vector3(-42, 0.02f, -2), new Vector3(43, 0.02f, -20),
            new Vector3(-74, 0.02f, -80), new Vector3(72, 0.02f, -91),
            new Vector3(-55, 0.02f, -139), new Vector3(56, 0.02f, -146),
            new Vector3(-101, 0.02f, 12), new Vector3(103, 0.02f, 9),
            new Vector3(-23, 0.02f, 54), new Vector3(26, 0.02f, 56)
        };
        for (var index = 0; index < placements.Length; index++)
        {
            models.Add(new RefineryModelPlacement(
                $"RefineryCrate_{index + 1:00}",
                CratePath,
                placements[index],
                index * 0.37f,
                1.55f,
                new Vector3(0.82f, 0.42f, 0.68f),
                new Vector3(-0.06f, 0.21f, 0.1f),
                72.0f,
                false));
        }
    }

    private static IReadOnlyList<Vector3> GarrisonSpawns() => new[]
    {
        new Vector3(-28, 0.15f, 24), new Vector3(27, 0.15f, 22),
        new Vector3(-33, 0.15f, -8), new Vector3(35, 0.15f, -12),
        new Vector3(-73, 0.15f, -55), new Vector3(72, 0.15f, -61),
        new Vector3(-107, 0.15f, -82), new Vector3(109, 0.15f, -87),
        new Vector3(-52, 0.15f, -116), new Vector3(51, 0.15f, -119),
        new Vector3(-12, 0.15f, -142), new Vector3(14, 0.15f, -151),
        new Vector3(-17, 0.15f, -101), new Vector3(18, 0.15f, -103),
        new Vector3(-17, 0.15f, -136), new Vector3(18, 0.15f, -138),
        new Vector3(-104, 0.15f, -151), new Vector3(107, 0.15f, -154),
        new Vector3(-85, 0.15f, 23), new Vector3(87, 0.15f, 20)
    };

    private static IReadOnlyList<Vector3> CoverPoints() => new[]
    {
        new Vector3(-20, 0, 28), new Vector3(-14, 0, 29), new Vector3(17, 0, 14), new Vector3(22, 0, 13),
        new Vector3(-43, 0, -2), new Vector3(43, 0, -20), new Vector3(-37, 0, -44), new Vector3(-37, 0, -49),
        new Vector3(38, 0, -83), new Vector3(38, 0, -89), new Vector3(-74, 0, -80), new Vector3(72, 0, -91),
        new Vector3(-55, 0, -139), new Vector3(56, 0, -146), new Vector3(-20, 0, -133), new Vector3(-15, 0, -133),
        new Vector3(18, 0, -181), new Vector3(23, 0, -181), new Vector3(-101, 0, 12), new Vector3(103, 0, 9),
        new Vector3(-23, 0, 54), new Vector3(26, 0, 56), new Vector3(-74, 0, -58), new Vector3(75, 0, -59),
        new Vector3(-88, 0, -140), new Vector3(91, 0, -142), new Vector3(-13, 0, -48), new Vector3(14, 0, -73),
        new Vector3(-18, 0, -101), new Vector3(18, 0, -103), new Vector3(-18, 0, -124),
        new Vector3(18, 0, -126), new Vector3(-18, 0, -145), new Vector3(18, 0, -147),
        new Vector3(-84, 0, -40), new Vector3(84, 0, -42)
    };

    private static IReadOnlyList<RefineryLootPlacement> LootPlacements() => new[]
    {
        Loot(new(-43, 0.2f, -2), LootGrade.Rare, "Manifest intake cache", "\u6e05\u5355\u5165\u5e93\u7269\u8d44"),
        Loot(new(43, 0.2f, -20), LootGrade.Rare, "Relay service cache", "\u4e2d\u7ee7\u7ef4\u4fee\u7269\u8d44"),
        Loot(new(-74, 0.2f, -80), LootGrade.Uncommon, "West pump toolbox", "\u897f\u6cf5\u7ad9\u5de5\u5177\u7bb1"),
        Loot(new(72, 0.2f, -91), LootGrade.Uncommon, "East pump toolbox", "\u4e1c\u6cf5\u7ad9\u5de5\u5177\u7bb1"),
        Loot(new(-55, 0.2f, -139), LootGrade.Epic, "Cracking unit strongbox", "\u88c2\u89e3\u5355\u5143\u91cd\u5323"),
        Loot(new(56, 0.2f, -146), LootGrade.Rare, "Turbine parts locker", "\u6da1\u8f6e\u96f6\u4ef6\u67dc"),
        Loot(new(-101, 0.2f, 12), LootGrade.Uncommon, "West control stash", "\u897f\u63a7\u5236\u5ba4\u85cf\u5305"),
        Loot(new(103, 0.2f, 9), LootGrade.Rare, "East control safe", "\u4e1c\u63a7\u5236\u5ba4\u4fdd\u9669\u7bb1"),
        Loot(new(-23, 0.2f, 54), LootGrade.Common, "Intake yard supply", "\u8fdb\u6599\u573a\u8865\u7ed9"),
        Loot(new(26, 0.2f, 56), LootGrade.Uncommon, "Loading office cache", "\u88c5\u5378\u529e\u516c\u5ba4\u7269\u8d44"),
        Loot(new(-111, 0.2f, -111), LootGrade.Rare, "West workshop locker", "\u897f\u7ef4\u4fee\u95f4\u50a8\u7269\u67dc"),
        Loot(new(111, 0.2f, -114), LootGrade.Rare, "East workshop locker", "\u4e1c\u7ef4\u4fee\u95f4\u50a8\u7269\u67dc"),
        Loot(new(-30, 0.2f, -176), LootGrade.Epic, "Bonded storage case", "\u4fdd\u7a0e\u5e93\u7269\u8d44\u7bb1"),
        Loot(new(40, 0.2f, -177), LootGrade.Legendary, "Turbine master safe", "\u6da1\u8f6e\u4e3b\u4fdd\u9669\u7bb1"),
        Loot(new(-12, 0.2f, -48), LootGrade.Common, "Extraction approach bag", "\u64a4\u79bb\u8fdb\u573a\u7269\u8d44"),
        Loot(new(14, 0.2f, -73), LootGrade.Uncommon, "Pad service kit", "\u505c\u673a\u576a\u7ef4\u4fee\u5305"),
        Loot(new(-18, 0.2f, -112), LootGrade.Rare, "Cracking hall line cache", "\u88c2\u89e3\u5382\u7ebf\u7269\u8d44"),
        Loot(new(18, 0.2f, -132), LootGrade.Epic, "Cracking hall supervisor case", "\u88c2\u89e3\u5382\u76d1\u7ba1\u7bb1"),
        Loot(new(-78, 0.2f, -34), LootGrade.Epic, "Cyclone sanctum cache", "\u65cb\u6d41\u5723\u6240\u7269\u8d44\u7bb1"),
        Loot(new(78, 0.2f, -36), LootGrade.Epic, "Reactor crown case", "\u53cd\u5e94\u5806\u51a0\u5854\u7269\u8d44\u7bb1")
    };

    private static RefineryLootPlacement Loot(
        Vector3 position,
        LootGrade grade,
        string english,
        string chinese)
        => new(position, grade, english, chinese);

    private static IReadOnlyList<(Vector3, ValuableItemKind, LootGrade)> ValuablePlacements() => new[]
    {
        (new Vector3(-96, 0.2f, 37), ValuableItemKind.HandToolSet, LootGrade.Common),
        (new Vector3(97, 0.2f, 35), ValuableItemKind.Wristwatch, LootGrade.Uncommon),
        (new Vector3(-62, 0.2f, -95), ValuableItemKind.GraphicsCard, LootGrade.Rare),
        (new Vector3(64, 0.2f, -101), ValuableItemKind.EncryptedDrive, LootGrade.Epic),
        (new Vector3(-21, 0.2f, -158), ValuableItemKind.CollectorCoin, LootGrade.Epic),
        (new Vector3(29, 0.2f, -168), ValuableItemKind.GoldJewelry, LootGrade.Legendary),
        (new Vector3(-128, 0.2f, -58), ValuableItemKind.VintageCamera, LootGrade.Rare),
        (new Vector3(130, 0.2f, -64), ValuableItemKind.DesignerPerfume, LootGrade.Rare),
        (new Vector3(-27, 0.2f, -143), ValuableItemKind.EncryptedDrive, LootGrade.Epic),
        (new Vector3(27, 0.2f, -103), ValuableItemKind.GraphicsCard, LootGrade.Rare),
        (new Vector3(-73, 0.2f, -40), ValuableItemKind.CollectorCoin, LootGrade.Legendary),
        (new Vector3(73, 0.2f, -42), ValuableItemKind.EncryptedDrive, LootGrade.Legendary)
    };
}
