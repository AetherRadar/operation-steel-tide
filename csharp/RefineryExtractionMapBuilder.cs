using System;
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
    bool HasCollision,
    bool IsTallScene,
    string District);

public readonly record struct RefineryLootPlacement(
    Vector3 Position,
    LootGrade Grade,
    string EnglishName,
    string ChineseName);

public readonly record struct RefineryHighValueZone(
    string Id,
    Vector3 Center,
    float Radius);

public readonly record struct RefineryRouteProbe(
    string Name,
    Vector3 From,
    Vector3 To);

public sealed record RefineryExtractionMapLayout(
    IReadOnlyList<RefineryModelPlacement> Models,
    IReadOnlyList<Vector3> GarrisonSpawns,
    IReadOnlyList<Vector3> CoverPoints,
    IReadOnlyList<RefineryLootPlacement> LootPlacements,
    IReadOnlyList<(Vector3 Position, ValuableItemKind Kind, LootGrade Grade)> ValuablePlacements,
    IReadOnlyList<RefineryHighValueZone> HighValueZones,
    IReadOnlyList<RefineryRouteProbe> RouteProbes,
    Vector3 RelayTerminal,
    Vector3 ManifestTerminal);

/// <summary>
/// Legacy collision and gameplay placement data for Jianghai Old City. The refinery type and map ID
/// remain stable for saves, command-line diagnostics, and extraction network messages.
/// </summary>
public sealed class RefineryExtractionMapBuilder
{
    private const string TownRoot = "res://assets/models/quaternius_downtown_city";
    public static readonly Vector3 HotelCenter = new(-86.0f, 0.0f, -124.0f);
    public static readonly Vector3 TreasuryCenter = new(86.0f, 0.0f, 4.0f);

    public RefineryExtractionMapLayout Build()
    {
        var models = new List<RefineryModelPlacement>(112);
        AddRoadNetwork(models);
        AddBuildingBlocks(models);
        AddStreetFurniture(models);

        return new RefineryExtractionMapLayout(
            models,
            GarrisonSpawns(),
            CoverPoints(),
            LootPlacements(),
            ValuablePlacements(),
            new[]
            {
                new RefineryHighValueZone("grand_hotel", HotelCenter, 20.0f),
                new RefineryHighValueZone("municipal_treasury", TreasuryCenter, 20.0f)
            },
            RouteProbes(),
            HotelCenter + new Vector3(0, 0.0f, 17.0f),
            TreasuryCenter + new Vector3(0, 0.0f, -17.0f));
    }

    private static void AddRoadNetwork(List<RefineryModelPlacement> models)
    {
        const float roadScale = 1.6f;
        const float roadStep = 9.6f;
        const float hotelStreetZ = -98.4f;
        const float treasuryStreetZ = -21.6f;
        var index = 0;

        for (var z = -112.8f; z >= -208.8f; z -= roadStep)
        {
            models.Add(Road(
                $"VictoryStreetNorth_{index++:00}",
                "Street_2Lane.gltf",
                new Vector3(0, 0.2f, z),
                Mathf.Pi * 0.5f,
                roadScale));
        }
        index = 0;
        for (var z = -7.2f; z <= 88.8f; z += roadStep)
        {
            models.Add(Road(
                $"VictoryStreetSouth_{index++:00}",
                "Street_2Lane.gltf",
                new Vector3(0, 0.2f, z),
                Mathf.Pi * 0.5f,
                roadScale));
        }

        foreach (var (streetName, streetZ) in new[]
                 {
                     ("HotelStreet", hotelStreetZ),
                     ("TreasuryStreet", treasuryStreetZ)
                 })
        {
            index = 0;
            for (var x = 14.4f; x <= 158.4f; x += roadStep)
            {
                models.Add(Road(
                    $"{streetName}East_{index:00}",
                    "Street_2Lane.gltf",
                    new Vector3(x, 0.2f, streetZ),
                    0.0f,
                    roadScale));
                models.Add(Road(
                    $"{streetName}West_{index++:00}",
                    "Street_2Lane.gltf",
                    new Vector3(-x, 0.2f, streetZ),
                    0.0f,
                    roadScale));
            }
            models.Add(Road(
                $"{streetName}VictoryJunction",
                "Street_4WayIntersection.gltf",
                new Vector3(0, 0.2f, streetZ),
                0.0f,
                0.78f));
        }

        var plazaIndex = 0;
        foreach (var position in new[]
                 {
                     new Vector3(0, 0.44f, -60.0f),
                     new Vector3(28.8f, 0.44f, -60.0f),
                     new Vector3(0, 0.44f, -31.2f),
                     new Vector3(28.8f, 0.44f, -31.2f)
                 })
        {
            models.Add(Road(
                $"FoundersPlaza_{plazaIndex++:00}",
                "Street_Asphalt_9x9.gltf",
                position,
                0.0f,
                3.2f));
        }

        index = 0;
        foreach (var z in new[]
                 {
                     -204.0f, -184.8f, -165.6f, -146.4f, -127.2f,
                     -2.4f, 16.8f, 36.0f, 55.2f, 74.4f
                 })
        {
            models.Add(Road(
                $"VictoryLaneMark_{index++:00}",
                "Decal_BrokenLine_Straight.gltf",
                new Vector3(0, 0.225f, z),
                Mathf.Pi * 0.5f,
                1.0f));
        }

        foreach (var streetZ in new[] { hotelStreetZ, treasuryStreetZ })
        {
            index = 0;
            for (var x = -148.8f; x <= 148.8f; x += 19.2f)
            {
                if (Mathf.Abs(x) < 12.0f)
                {
                    continue;
                }
                models.Add(Road(
                    $"CrossStreetMark_{streetZ:0}_{index++:00}",
                    "Decal_BrokenLine_Straight.gltf",
                    new Vector3(x, 0.225f, streetZ),
                    0.0f,
                    1.0f));
            }
        }

        models.Add(Road("GrandHotelCrosswalk", "Decal_Crosswalk.gltf",
            new Vector3(-86, 0.23f, hotelStreetZ), 0.0f, 1.45f));
        models.Add(Road("TreasuryCrosswalk", "Decal_Crosswalk.gltf",
            new Vector3(86, 0.23f, treasuryStreetZ), 0.0f, 1.45f));
    }

    private static void AddBuildingBlocks(List<RefineryModelPlacement> models)
    {
        AddBuildingColumn(models, "VictoryWestNorth", -17.0f, Mathf.Pi * 0.5f,
            (-146.0f, "Building_Large_2.gltf", 0.94f),
            (-168.0f, "Building_Medium_2_001.gltf", 0.96f),
            (-190.0f, "Building_Small_1.gltf", 1.02f));
        AddBuildingColumn(models, "VictoryEastNorth", 17.0f, -Mathf.Pi * 0.5f,
            (-146.0f, "Building_Small_1.gltf", 1.02f),
            (-168.0f, "Building_Large_2.gltf", 0.92f),
            (-190.0f, "Building_Medium_2_001.gltf", 0.96f));
        AddBuildingColumn(models, "VictoryWestSouth", -17.0f, Mathf.Pi * 0.5f,
            (4.0f, "Building_Small_1.gltf", 1.0f),
            (26.0f, "Building_Medium_2_001.gltf", 0.94f),
            (48.0f, "Building_Large_2.gltf", 0.92f),
            (70.0f, "Building_Small_1.gltf", 1.04f));
        AddBuildingColumn(models, "VictoryEastSouth", 17.0f, -Mathf.Pi * 0.5f,
            (4.0f, "Building_Medium_2_001.gltf", 0.94f),
            (26.0f, "Building_Small_1.gltf", 1.02f),
            (48.0f, "Building_Medium_2_001.gltf", 0.96f),
            (70.0f, "Building_Large_2.gltf", 0.90f));

        AddBuildingRow(models, "HotelStreetNorth", -112.0f, 0.0f,
            (-145.0f, "Building_Small_1.gltf", 0.98f),
            (-125.0f, "Building_Medium_2_001.gltf", 0.94f),
            (-50.0f, "Building_Large_2.gltf", 0.92f),
            (50.0f, "Building_Medium_2_001.gltf", 0.94f),
            (72.0f, "Building_Small_1.gltf", 1.02f),
            (94.0f, "Building_Large_2.gltf", 0.90f),
            (117.0f, "Building_Medium_2_001.gltf", 0.94f),
            (141.0f, "Building_Small_1.gltf", 1.02f));
        AddBuildingRow(models, "HotelStreetSouth", -85.0f, Mathf.Pi,
            (-145.0f, "Building_Medium_2_001.gltf", 0.94f),
            (-122.0f, "Building_Small_1.gltf", 1.02f),
            (-96.0f, "Building_Large_2.gltf", 0.90f),
            (-68.0f, "Building_Medium_2_001.gltf", 0.94f),
            (68.0f, "Building_Small_1.gltf", 1.02f),
            (96.0f, "Building_Large_2.gltf", 0.90f),
            (122.0f, "Building_Medium_2_001.gltf", 0.94f),
            (145.0f, "Building_Small_1.gltf", 1.02f));
        AddBuildingRow(models, "TreasuryStreetNorth", -35.0f, 0.0f,
            (-145.0f, "Building_Small_1.gltf", 1.02f),
            (-122.0f, "Building_Medium_2_001.gltf", 0.94f),
            (-96.0f, "Building_Large_2.gltf", 0.90f),
            (-68.0f, "Building_Small_1.gltf", 1.02f),
            (68.0f, "Building_Medium_2_001.gltf", 0.94f),
            (96.0f, "Building_Large_2.gltf", 0.90f),
            (122.0f, "Building_Small_1.gltf", 1.02f),
            (145.0f, "Building_Medium_2_001.gltf", 0.94f));
        AddBuildingRow(models, "TreasuryStreetSouth", -8.5f, Mathf.Pi,
            (-145.0f, "Building_Medium_2_001.gltf", 0.94f),
            (-122.0f, "Building_Small_1.gltf", 1.02f),
            (-96.0f, "Building_Large_2.gltf", 0.90f),
            (-70.0f, "Building_Medium_2_001.gltf", 0.94f),
            (-48.0f, "Building_Small_1.gltf", 1.02f),
            (48.0f, "Building_Medium_2_001.gltf", 0.94f),
            (120.0f, "Building_Large_2.gltf", 0.90f),
            (145.0f, "Building_Small_1.gltf", 1.02f));

        AddBuildingColumn(models, "founders_plaza", -34.0f, Mathf.Pi * 0.5f,
            (-78.0f, "Building_Medium_2_001.gltf", 0.94f),
            (-59.0f, "Building_Small_1.gltf", 1.0f),
            (-40.0f, "Building_Medium_2_001.gltf", 0.94f));
        AddBuildingColumn(models, "FoundersPlazaEast", 34.0f, -Mathf.Pi * 0.5f,
            (-78.0f, "Building_Small_1.gltf", 1.0f),
            (-59.0f, "Building_Medium_2_001.gltf", 0.94f),
            (-40.0f, "Building_Small_1.gltf", 1.0f));

        AddBuildingRow(models, "NorthGate", -187.0f, 0.0f,
            (-126.0f, "Building_Small_1.gltf", 1.0f),
            (-103.0f, "Building_Medium_2_001.gltf", 0.94f),
            (-78.0f, "Building_Large_2.gltf", 0.90f),
            (-52.0f, "Building_Small_1.gltf", 1.02f),
            (52.0f, "Building_Medium_2_001.gltf", 0.94f),
            (78.0f, "Building_Large_2.gltf", 0.90f),
            (103.0f, "Building_Small_1.gltf", 1.02f),
            (126.0f, "Building_Medium_2_001.gltf", 0.94f));
        AddBuildingRow(models, "SouthGate", 58.0f, Mathf.Pi,
            (-126.0f, "Building_Medium_2_001.gltf", 0.94f),
            (-103.0f, "Building_Small_1.gltf", 1.02f),
            (-78.0f, "Building_Large_2.gltf", 0.90f),
            (-52.0f, "Building_Medium_2_001.gltf", 0.94f),
            (52.0f, "Building_Small_1.gltf", 1.02f),
            (78.0f, "Building_Large_2.gltf", 0.90f),
            (103.0f, "Building_Medium_2_001.gltf", 0.94f),
            (126.0f, "Building_Small_1.gltf", 1.02f));
        AddBuildingColumn(models, "WestWard", -135.0f, Mathf.Pi * 0.5f,
            (-155.0f, "Building_Medium_2_001.gltf", 0.94f),
            (-132.0f, "Building_Small_1.gltf", 1.02f),
            (14.0f, "Building_Large_2.gltf", 0.90f),
            (36.0f, "Building_Medium_2_001.gltf", 0.94f));
        AddBuildingColumn(models, "EastWard", 135.0f, -Mathf.Pi * 0.5f,
            (-155.0f, "Building_Small_1.gltf", 1.02f),
            (-132.0f, "Building_Medium_2_001.gltf", 0.94f),
            (14.0f, "Building_Small_1.gltf", 1.02f),
            (36.0f, "Building_Large_2.gltf", 0.90f));

        models.Add(Building(
            "GrandHotelAnchor",
            "Building_Large_2.gltf",
            new Vector3(-86, 0.02f, -139),
            0.0f,
            1.02f,
            "grand_hotel"));
        models.Add(Building(
            "TreasuryAnchor",
            "Building_Large_2.gltf",
            new Vector3(86, 0.02f, 19),
            Mathf.Pi,
            1.02f,
            "municipal_treasury"));
    }

    private static void AddStreetFurniture(List<RefineryModelPlacement> models)
    {
        var planters = new[]
        {
            new Vector3(-18, 0.02f, -72), new Vector3(18, 0.02f, -72),
            new Vector3(-18, 0.02f, -48), new Vector3(18, 0.02f, -48),
            new Vector3(-102, 0.02f, -96), new Vector3(-70, 0.02f, -96),
            new Vector3(70, 0.02f, -24), new Vector3(102, 0.02f, -24)
        };
        for (var index = 0; index < planters.Length; index++)
        {
            models.Add(Prop(
                $"TownPlanter_{index + 1:00}",
                index % 2 == 0 ? "Sidewalk_Planter.gltf" : "Prop_Planter_Single.gltf",
                planters[index],
                index % 2 == 0 ? 0.0f : Mathf.Pi * 0.5f,
                1.0f,
                index % 2 == 0 ? new Vector3(1.97f, 0.51f, 1.77f) : new Vector3(2.0f, 0.6f, 2.0f),
                "street_furniture"));
        }

        var bollards = new[]
        {
            new Vector3(-24, 0.02f, -68), new Vector3(-24, 0.02f, -60), new Vector3(-24, 0.02f, -52),
            new Vector3(24, 0.02f, -68), new Vector3(24, 0.02f, -60), new Vector3(24, 0.02f, -52),
            new Vector3(-96, 0.02f, -104), new Vector3(-76, 0.02f, -104),
            new Vector3(76, 0.02f, -16), new Vector3(96, 0.02f, -16)
        };
        for (var index = 0; index < bollards.Length; index++)
        {
            models.Add(Prop(
                $"TownBollard_{index + 1:00}",
                "Prop_Bollard.gltf",
                bollards[index],
                0.0f,
                1.2f,
                new Vector3(0.22f, 0.89f, 0.23f),
                "street_furniture"));
        }

        foreach (var (position, yaw, name) in new[]
                 {
                     (new Vector3(-32, 0.03f, -92), 0.0f, "CanalWestManhole"),
                     (new Vector3(34, 0.03f, -28), 0.0f, "GardenEastManhole"),
                     (new Vector3(-5, 0.03f, -118), 0.0f, "NorthManhole"),
                     (new Vector3(5, 0.03f, 10), 0.0f, "SouthManhole")
                 })
        {
            models.Add(Road(name, "Prop_ManholeCover.gltf", position, yaw, 1.0f));
        }
    }

    private static void AddBuildingRow(
        List<RefineryModelPlacement> models,
        string prefix,
        float z,
        float yaw,
        params (float X, string File, float Scale)[] entries)
    {
        for (var index = 0; index < entries.Length; index++)
        {
            var entry = entries[index];
            models.Add(Building($"{prefix}_{index + 1:00}", entry.File, new Vector3(entry.X, 0.02f, z), yaw, entry.Scale, prefix));
        }
    }

    private static void AddBuildingColumn(
        List<RefineryModelPlacement> models,
        string prefix,
        float x,
        float yaw,
        params (float Z, string File, float Scale)[] entries)
    {
        for (var index = 0; index < entries.Length; index++)
        {
            var entry = entries[index];
            models.Add(Building($"{prefix}_{index + 1:00}", entry.File, new Vector3(x, 0.02f, entry.Z), yaw, entry.Scale, prefix));
        }
    }

    private static RefineryModelPlacement Building(
        string name,
        string file,
        Vector3 position,
        float yaw,
        float scale,
        string district)
    {
        var (size, offset) = file switch
        {
            "Building_Small_1.gltf" => (new Vector3(12.46f, 17.03f, 14.54f), new Vector3(-1.0f, 8.5f, -4.96f)),
            "Building_Medium_2_001.gltf" => (new Vector3(15.06f, 25.01f, 13.06f), new Vector3(0.0f, 12.5f, -5.96f)),
            "Building_Large_2.gltf" => (new Vector3(20.64f, 28.0f, 16.64f), new Vector3(1.0f, 14.0f, -8.0f)),
            _ => throw new ArgumentOutOfRangeException(nameof(file), file, "Unknown old-town building profile.")
        };
        return new RefineryModelPlacement(
            name,
            $"{TownRoot}/{file}",
            position,
            yaw,
            scale,
            size,
            offset,
            330.0f,
            true,
            true,
            true,
            district);
    }

    private static RefineryModelPlacement Road(
        string name,
        string file,
        Vector3 position,
        float yaw,
        float scale)
        => new(
            name,
            $"{TownRoot}/{file}",
            position,
            yaw,
            scale,
            Vector3.Zero,
            Vector3.Zero,
            230.0f,
            false,
            false,
            false,
            "road");

    private static RefineryModelPlacement Prop(
        string name,
        string file,
        Vector3 position,
        float yaw,
        float scale,
        Vector3 size,
        string district)
        => new(
            name,
            $"{TownRoot}/{file}",
            position,
            yaw,
            scale,
            size,
            new Vector3(0, size.Y * 0.5f, 0),
            120.0f,
            false,
            true,
            false,
            district);

    private static IReadOnlyList<Vector3> GarrisonSpawns() => new[]
    {
        new Vector3(-92, 0.15f, -101), new Vector3(-80, 0.15f, -104),
        new Vector3(-98, 0.15f, -122), new Vector3(-74, 0.15f, -133),
        new Vector3(92, 0.15f, -19), new Vector3(78, 0.15f, -16),
        new Vector3(98, 0.15f, 4), new Vector3(74, 0.15f, 13),
        new Vector3(-22, 0.15f, -77), new Vector3(22, 0.15f, -45),
        new Vector3(-39, 0.15f, -88), new Vector3(41, 0.15f, -32),
        new Vector3(-115, 0.15f, -71), new Vector3(116, 0.15f, -102),
        new Vector3(-43, 0.15f, -166), new Vector3(45, 0.15f, -170),
        new Vector3(-104, 0.15f, 27), new Vector3(105, 0.15f, 38),
        new Vector3(-14, 0.15f, -121), new Vector3(14, 0.15f, -119)
    };

    private static IReadOnlyList<Vector3> CoverPoints() => new[]
    {
        new Vector3(-19, 0, -72), new Vector3(19, 0, -72), new Vector3(-19, 0, -48), new Vector3(19, 0, -48),
        new Vector3(-24, 0, -68), new Vector3(-24, 0, -52), new Vector3(24, 0, -68), new Vector3(24, 0, -52),
        new Vector3(-102, 0, -96), new Vector3(-70, 0, -96), new Vector3(70, 0, -24), new Vector3(102, 0, -24),
        new Vector3(-98, 0, -108), new Vector3(-74, 0, -108), new Vector3(-98, 0, -136), new Vector3(-74, 0, -136),
        new Vector3(74, 0, -10), new Vector3(98, 0, -10), new Vector3(74, 0, 18), new Vector3(98, 0, 18),
        new Vector3(-42, 0, -88), new Vector3(42, 0, -88), new Vector3(-42, 0, -32), new Vector3(42, 0, -32),
        new Vector3(-118, 0, -91), new Vector3(118, 0, -29), new Vector3(-118, 0, -29), new Vector3(118, 0, -91),
        new Vector3(-31, 0, -121), new Vector3(31, 0, -121), new Vector3(-31, 0, -119), new Vector3(31, 0, -119)
    };

    private static IReadOnlyList<RefineryLootPlacement> LootPlacements() => new[]
    {
        Loot(new(-91, 0.2f, -122), LootGrade.Legendary, "Guangchang Pawnshop master safe", "\u5e7f\u660c\u5f53\u94fa\u4e3b\u4fdd\u9669\u67dc"),
        Loot(new(-80, 0.2f, -128), LootGrade.Epic, "Pawnshop jewelry case", "\u5f53\u94fa\u73e0\u5b9d\u7bb1"),
        Loot(new(-94, 0.2f, -113), LootGrade.Epic, "Pawnshop counter strongbox", "\u5f53\u94fa\u67dc\u53f0\u91cd\u5323"),
        Loot(new(-76, 0.2f, -115), LootGrade.Rare, "Pawnshop service cache", "\u5f53\u94fa\u540e\u573a\u7269\u8d44"),
        Loot(new(91, 0.2f, 4), LootGrade.Legendary, "Red Star Electronics secure vault", "\u7ea2\u661f\u7535\u5b50\u5382\u4fdd\u5bc6\u67dc"),
        Loot(new(80, 0.2f, 9), LootGrade.Epic, "Factory payroll case", "\u7535\u5b50\u5382\u5de5\u8d44\u7bb1"),
        Loot(new(94, 0.2f, -5), LootGrade.Epic, "Archive cipher locker", "\u6863\u6848\u5bc6\u7801\u67dc"),
        Loot(new(76, 0.2f, -7), LootGrade.Rare, "Clerk security cache", "\u804c\u5458\u5b89\u4fdd\u7269\u8d44"),
        Loot(new(-17, 0.2f, -74), LootGrade.Rare, "Jianghai market lockbox", "\u6c5f\u6d77\u5e02\u96c6\u9501\u7bb1"),
        Loot(new(18, 0.2f, -47), LootGrade.Uncommon, "Jianghai Square vendor crate", "\u6c5f\u6d77\u5e7f\u573a\u5546\u8d29\u7269\u8d44"),
        Loot(new(-42, 0.2f, -88), LootGrade.Rare, "West Arcade pharmacy cabinet", "\u897f\u5173\u9a91\u697c\u836f\u623f\u67dc"),
        Loot(new(42, 0.2f, -32), LootGrade.Rare, "Factory Row electronics cache", "\u7ea2\u661f\u5382\u8857\u7535\u5b50\u7269\u8d44"),
        Loot(new(-116, 0.2f, -70), LootGrade.Uncommon, "West alley toolbox", "\u897f\u5df7\u5de5\u5177\u7bb1"),
        Loot(new(116, 0.2f, -101), LootGrade.Uncommon, "East alley supply", "\u4e1c\u5df7\u8865\u7ed9"),
        Loot(new(-44, 0.2f, -166), LootGrade.Rare, "North gate luggage", "\u5317\u95e8\u884c\u674e"),
        Loot(new(44, 0.2f, 36), LootGrade.Uncommon, "South clinic bag", "\u5357\u533a\u8bca\u6240\u5305"),
        Loot(new(-14, 4.5f, -126), LootGrade.Epic, "Rooftop courier case", "\u5c4b\u9876\u901f\u9012\u7bb1"),
        Loot(new(14, 4.5f, -126), LootGrade.Rare, "Rooftop observer cache", "\u5c4b\u9876\u89c2\u6d4b\u7269\u8d44")
    };

    private static RefineryLootPlacement Loot(Vector3 position, LootGrade grade, string english, string chinese)
        => new(position, grade, english, chinese);

    private static IReadOnlyList<(Vector3, ValuableItemKind, LootGrade)> ValuablePlacements() => new[]
    {
        (new Vector3(-88, 0.2f, -121), ValuableItemKind.AntiqueClock, LootGrade.Legendary),
        (new Vector3(-82, 0.2f, -130), ValuableItemKind.GoldJewelry, LootGrade.Legendary),
        (new Vector3(-95, 0.2f, -116), ValuableItemKind.CollectorCoin, LootGrade.Epic),
        (new Vector3(-77, 0.2f, -116), ValuableItemKind.DesignerPerfume, LootGrade.Rare),
        (new Vector3(88, 0.2f, 1), ValuableItemKind.EncryptedDrive, LootGrade.Epic),
        (new Vector3(82, 0.2f, 10), ValuableItemKind.GoldJewelry, LootGrade.Legendary),
        (new Vector3(95, 0.2f, 6), ValuableItemKind.CollectorCoin, LootGrade.Epic),
        (new Vector3(77, 0.2f, -4), ValuableItemKind.Wristwatch, LootGrade.Rare),
        (new Vector3(-38, 0.2f, -89), ValuableItemKind.VintageCamera, LootGrade.Rare),
        (new Vector3(38, 0.2f, -31), ValuableItemKind.GraphicsCard, LootGrade.Rare),
        (new Vector3(-112, 0.2f, 20), ValuableItemKind.CeramicTeaSet, LootGrade.Common),
        (new Vector3(112, 0.2f, -158), ValuableItemKind.HandToolSet, LootGrade.Uncommon)
    };

    private static IReadOnlyList<RefineryRouteProbe> RouteProbes() => new[]
    {
        new RefineryRouteProbe("victory_truck_center_mid", new Vector3(-0.5f, 1.1f, 88), new Vector3(-0.5f, 1.1f, -212)),
        new RefineryRouteProbe("victory_truck_left_low", new Vector3(-2.0f, 0.45f, 88), new Vector3(-2.0f, 0.45f, -212)),
        new RefineryRouteProbe("victory_truck_right_low", new Vector3(1.0f, 0.45f, 88), new Vector3(1.0f, 0.45f, -212)),
        new RefineryRouteProbe("victory_truck_left_mid", new Vector3(-2.0f, 1.4f, 88), new Vector3(-2.0f, 1.4f, -212)),
        new RefineryRouteProbe("victory_truck_right_mid", new Vector3(1.0f, 1.4f, 88), new Vector3(1.0f, 1.4f, -212)),
        new RefineryRouteProbe("victory_truck_left_high", new Vector3(-2.0f, 2.6f, 88), new Vector3(-2.0f, 2.6f, -212)),
        new RefineryRouteProbe("victory_truck_center_high", new Vector3(-0.5f, 2.6f, 88), new Vector3(-0.5f, 2.6f, -212)),
        new RefineryRouteProbe("victory_truck_right_high", new Vector3(1.0f, 2.6f, 88), new Vector3(1.0f, 2.6f, -212)),
        new RefineryRouteProbe("victory_west_lane", new Vector3(-4.2f, 1.1f, 88), new Vector3(-4.2f, 1.1f, -212)),
        new RefineryRouteProbe("victory_east_lane", new Vector3(4.2f, 1.1f, 88), new Vector3(4.2f, 1.1f, -212)),
        new RefineryRouteProbe("hotel_entry", new Vector3(-86, 1.1f, -98.4f), new Vector3(-86, 1.1f, -111)),
        new RefineryRouteProbe("treasury_entry", new Vector3(86, 1.1f, -21.6f), new Vector3(86, 1.1f, -9)),
        new RefineryRouteProbe("hotel_street", new Vector3(-160, 1.1f, -98.4f), new Vector3(160, 1.1f, -98.4f)),
        new RefineryRouteProbe("treasury_street", new Vector3(-160, 1.1f, -21.6f), new Vector3(160, 1.1f, -21.6f))
    };
}
