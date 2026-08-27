using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public sealed partial class DemolitionArenaLayout
{
    private const string KenneyFactoryRoot = "res://assets/models/kenney_factory_kit";
    private const string KenneyRoadsRoot = "res://assets/models/kenney_city_kit_roads";
    private const string MajadroidConstructionRoot = "res://assets/models/majadroid_construction_site";
    private const string PolyHavenBarrierRoot = "res://assets/models/concrete_road_barrier";
    private const string QuaterniusBuildingsRoot = "res://assets/models/quaternius_buildings_pack";
    private const string QuaterniusDowntownRoot = "res://assets/models/quaternius_downtown_city";
    private const string TreyModularIndustrialRoot = "res://assets/models/trey_modular_industrial";

    private IReadOnlyList<DemolitionArenaBox> BuildTideglassReactorCollisionBoxes()
    {
        var boxes = new List<DemolitionArenaBox>
        {
            Box("ArenaFloor", new(0, -0.48f, 0), new(112, 1.0f, 96), "ground", visible: false),
            Box("NorthPerimeter", new(0, 1.5f, -47.5f), new(112, 3.0f, 1.0f), "concrete_dark", visible: false),
            Box("SouthPerimeter", new(0, 1.5f, 47.5f), new(112, 3.0f, 1.0f), "concrete_dark", visible: false),
            Box("WestPerimeter", new(-55.5f, 1.5f, 0), new(1.0f, 3.0f, 96), "concrete_dark", visible: false),
            Box("EastPerimeter", new(55.5f, 1.5f, 0), new(1.0f, 3.0f, 96), "concrete_dark", visible: false),

            // The open construction tower keeps its sightlines: only its floor and visible column grid collide.
            Box("ConstructionTowerFoundation", new(-45.0f, 0.2f, 18.0f), new(13.9f, 0.36f, 16.6f), "concrete", visible: false),
            Box("BrickFactoryShell", new(49.0f, 10.52f, -9.0f), new(12.5f, 21.0f, 15.5f), "rust", visible: false),
            Box("GatewayWestPillar", new(-2.575f, 2.02f, -31.5f), new(0.85f, 4.0f, 0.75f), "rust", visible: false),
            Box("GatewayEastPillar", new(2.575f, 2.02f, -31.5f), new(0.85f, 4.0f, 0.75f), "rust", visible: false)
        };

        var towerColumnXs = new[] { -51.75f, -38.25f };
        var towerColumnRows = new[]
        {
            (Z: 9.9f, Height: 46.4f),
            (Z: 18.0f, Height: 46.08f),
            (Z: 26.1f, Height: 28.48f)
        };
        var towerColumnIndex = 0;
        foreach (var x in towerColumnXs)
        {
            foreach (var row in towerColumnRows)
            {
                towerColumnIndex++;
                boxes.Add(Box(
                    $"ConstructionTowerColumn_{towerColumnIndex:00}",
                    new Vector3(x, 0.02f + row.Height * 0.5f, row.Z),
                    new Vector3(0.42f, row.Height, 0.42f),
                    "warning",
                    visible: false));
            }
        }

        return boxes;
    }

    private static IReadOnlyList<DemolitionArenaBox> BuildTideglassReactorDetailBoxes()
        => new DemolitionArenaBox[0];

    private IReadOnlyList<DemolitionArenaBox> BuildTideglassReactorNavigationBoxes()
    {
        var boxes = new List<DemolitionArenaBox>
        {
            Box(
                "CraneNavigationFootprint",
                new(-32.174f, 0.92f, -14.174f),
                new(6.3f, 1.8f, 6.3f),
                "warning",
                rotation: new Vector3(0, Mathf.DegToRad(-12.0f), 0),
                visible: false),
            Box(
                "ConstructionSouthHillNavigation",
                new(-32.71f, 0.45f, 4.38f),
                new(7.14f, 0.9f, 5.76f),
                "ground",
                visible: false),
            Box(
                "ConstructionNorthHillNavigation",
                new(-43.84f, 0.63f, 27.65f),
                new(6.87f, 1.26f, 12.35f),
                "ground",
                visible: false)
        };

        var pillarXs = new[] { -3.45f, -1.15f, 1.15f, 3.45f };
        var pillarZs = new[] { 25.6375f, 27.3625f };
        var pillarIndex = 0;
        foreach (var x in pillarXs)
        {
            foreach (var z in pillarZs)
            {
                pillarIndex++;
                boxes.Add(Box(
                    $"CivicWalkwayPillarNavigation_{pillarIndex:00}",
                    new Vector3(x, 1.745f, z),
                    new Vector3(0.46f, 3.45f, 0.46f),
                    "steel_dark",
                    visible: false));
            }
        }
        boxes.Add(Box(
            "CivicWalkwayWestStairNavigation",
            new Vector3(-5.75f, 1.17f, 26.5f),
            new Vector3(2.3f, 2.3f, 2.3f),
            "steel",
            visible: false));
        boxes.Add(Box(
            "CivicWalkwayEastStairNavigation",
            new Vector3(5.75f, 1.17f, 26.5f),
            new Vector3(2.3f, 2.3f, 2.3f),
            "steel",
            visible: false));
        return boxes;
    }

    private IReadOnlyList<DemolitionArenaProp> BuildTideglassReactorProps()
    {
        return new[]
        {
            TideglassAuthoredProp("NorthGuildHall", QuaterniusBuildingsRoot, "building1-large.glb", new(-20.0f, 0.02f, -40.0f), 0.0f, 1.8f, new(7.991f, 4.671f, 2.744f), new(0, 2.3355f, 0)),
            TideglassAuthoredProp("NorthBrickOffices", QuaterniusBuildingsRoot, "building3-big.glb", new(0.0f, 0.02f, -40.0f), 0.0f, 1.9f, new(4.705f, 5.677f, 4.391f), new(0, 2.8385f, 0)),
            TideglassAuthoredProp("NorthCustomsHouse", QuaterniusBuildingsRoot, "building4.glb", new(20.0f, 0.02f, -40.0f), 0.0f, 1.9f, new(4.644f, 5.487f, 3.856f), new(0, 2.7435f, 0)),
            TideglassAuthoredProp("SouthGatehouse", QuaterniusBuildingsRoot, "house2.glb", new(-28.0f, 0.02f, 41.0f), 180.0f, 2.4f, new(3.644f, 2.926f, 3.084f), new(0, 1.463f, 0)),
            TideglassIndustrial("NorthLoadingBay", "loading-bay.glb", new(40.0f, 0.02f, -42.0f), 0.0f, 1.05f, new(8.1f, 3.1f, 4.5f), new(0, 1.55f, 0)),
            TideglassIndustrial("SouthUtilityOffice", "utility-office.glb", new(-8.0f, 0.02f, 42.0f), Mathf.Pi, 1.1f, new(4.45f, 3.1f, 2.42f), new(0, 1.55f, 0)),
            TideglassIndustrial("CentralServiceHall", "sawtooth-service-hall.glb", new(0.0f, 0.02f, 0.0f), 0.0f, 1.4f, new(8.1f, 3.1f, 4.42f), new(0, 1.55f, 0)),
            TideglassIndustrial("WestWindowHall", "window-hall.glb", new(-52.0f, 0.02f, 34.5f), Mathf.Pi * 0.5f, 1.05f, new(8.1f, 3.1f, 4.42f), new(0, 1.55f, 0)),

            TideglassAuthoredProp("SightBlockConstructionSiteOffice", MajadroidConstructionRoot, "containers-office.glb", new(20.0f, 0.02f, 33.0f), 0.0f, 0.75f, new(11.65f, 5.35f, 8.3f), new(0, 2.6f, 0)),
            TideglassAuthoredProp("SightBlockReactorCargoContainers", MajadroidConstructionRoot, "containers-cargo.glb", new(36.0f, 0.02f, 14.0f), 90.0f, 0.82f, new(3.15f, 2.75f, 8.55f), new(0, 1.3f, 0)),
            TideglassAuthoredProp("ConstructionTruck", MajadroidConstructionRoot, "concrete-truck-red.glb", new(-35.0f, 0.02f, 30.0f), 12.0f, 1.05f, new(3.25f, 3.9f, 7.3f), new(0, 1.9075f, 0)),

            TideglassAuthoredProp("MidCoverConstructionSupplies", MajadroidConstructionRoot, "construction-materials.glb", new(-12.0f, 0.02f, 18.0f), 90.0f, 1.0f, new(1.65f, 1.32f, 8.75f), new(0, 0.6025f, 0)),
            TideglassAuthoredProp("MidCoverDumpster", KenneyRoadsRoot, "dumpster.glb", new(-13.0f, 0.02f, -10.0f), -18.0f, 6.0f, new(0.295f, 0.225f, 0.39f), new(0.0075f, 0.1047f, 0)),
            TideglassAuthoredProp("MidCoverMachine", KenneyFactoryRoot, "machine.glb", new(8.0f, 0.02f, 17.0f), 15.0f, 2.0f, new(1.3f, 1.4f, 1.6f), new(0, 0.6499f, 0)),
            TideglassAuthoredProp("MidCoverHopper", KenneyFactoryRoot, "hopper-high-round.glb", new(17.0f, 0.02f, 9.0f), -12.0f, 1.8f, new(1.2f, 1.58f, 1.2f), new(0, 0.75f, 0)),
            TideglassAuthoredProp("MidCoverCivicPlanter", QuaterniusDowntownRoot, "Prop_Planter_Single.gltf", new(17.0f, 0.02f, -3.0f), 90.0f, 2.3f, new(2.0f, 0.6f, 2.0f), new(0, 0.3f, 0)),
            TideglassAuthoredProp("MidCoverRoadBarrier", KenneyRoadsRoot, "construction-barrier.glb", new(7.0f, 0.02f, -11.0f), 90.0f, 10.0f, new(0.15f, 0.14f, 0.24f), new(0, 0.065f, 0)),
            TideglassAuthoredProp("MidCoverGenerator", KenneyFactoryRoot, "machine-window.glb", new(-8.0f, 0.02f, -17.0f), 20.0f, 2.0f, new(1.3f, 1.38f, 1.6f), new(0, 0.6449f, 0)),
            TideglassAuthoredProp("MidCoverConcreteBarrier", PolyHavenBarrierRoot, "concrete_road_barrier.gltf", new(-21.0f, 0.02f, 7.0f), 90.0f, 1.7f, new(1.55f, 0.84f, 0.64f), new(0, 0.41f, 0)),
            TideglassAuthoredProp("CrossingTrafficLight", KenneyRoadsRoot, "traffic-light.glb", new(8.0f, 0.02f, -24.0f), 0.0f, 7.5f, new(0.13f, 0.53f, 0.1f), new(-0.0138f, 0.2575f, 0))
        };
    }

    private IReadOnlyList<Vector3> BuildTideglassReactorAttackToAPath() => WorldPoints(
        new(40.0f, 0.2f, 40.0f), new(31.0f, 0.2f, 31.0f),
        new(28.0f, 0.2f, 25.0f), new(13.0f, 0.2f, 24.0f),
        new(7.0f, 0.2f, 29.3f), new(-7.0f, 0.2f, 29.3f), new(-16.0f, 0.2f, 29.0f),
        new(-23.0f, 0.2f, 24.0f), new(-31.0f, 0.2f, 18.0f));

    private IReadOnlyList<Vector3> BuildTideglassReactorAttackToBPath() => WorldPoints(
        new(40.0f, 0.2f, 40.0f), new(45.0f, 0.2f, 42.0f), new(50.0f, 0.2f, 40.0f),
        new(51.0f, 0.2f, 28.0f), new(49.0f, 0.2f, 15.0f),
        new(45.0f, 0.2f, 4.0f), new(41.5f, 0.2f, -1.0f), new(41.5f, 0.2f, -7.0f),
        new(36.0f, 0.2f, -12.0f), new(31.0f, 0.2f, -18.0f));

    private IReadOnlyList<Vector3> BuildTideglassReactorAttackMidPath() => WorldPoints(
        new(40.0f, 0.2f, 40.0f), new(31.0f, 0.2f, 31.0f),
        new(24.0f, 0.2f, 24.0f), new(16.0f, 0.2f, 22.0f),
        new(8.0f, 0.2f, 22.0f), new(0.0f, 0.2f, 20.0f),
        new(3.0f, 0.2f, 13.0f), new(8.0f, 0.2f, 7.0f),
        new(8.0f, 0.2f, -2.0f));

    private IReadOnlyList<Vector3> BuildTideglassReactorDefenderToAPath() => WorldPoints(
        new(-40.0f, 0.2f, -40.0f), new(-32.0f, 0.2f, -31.0f),
        new(-27.0f, 0.2f, -20.0f), new(-28.0f, 0.2f, -7.0f),
        new(-27.0f, 0.2f, 5.0f), new(-31.0f, 0.2f, 18.0f));

    private IReadOnlyList<Vector3> BuildTideglassReactorDefenderToBPath() => WorldPoints(
        new(-40.0f, 0.2f, -40.0f), new(-31.0f, 0.2f, -30.0f),
        new(-18.0f, 0.2f, -28.0f), new(-5.0f, 0.2f, -29.0f),
        new(8.0f, 0.2f, -30.0f), new(18.0f, 0.2f, -27.0f),
        new(25.0f, 0.2f, -24.0f), new(31.0f, 0.2f, -18.0f));

    private IReadOnlyList<Vector3> BuildTideglassReactorSiteRotationPath() => WorldPoints(
        new(-31.0f, 0.2f, 18.0f), new(-25.0f, 0.2f, 12.0f),
        new(-24.0f, 0.2f, 2.0f), new(-14.0f, 0.2f, -2.0f),
        new(-4.0f, 0.2f, -5.0f), new(2.0f, 0.2f, -15.0f),
        new(14.0f, 0.2f, -19.0f), new(22.0f, 0.2f, -20.0f),
        new(31.0f, 0.2f, -18.0f));

    private DemolitionArenaProp TideglassIndustrial(
        string name,
        string file,
        Vector3 localPosition,
        float yaw,
        float scale,
        Vector3 collisionSize,
        Vector3 collisionOffset)
    {
        return new DemolitionArenaProp(
            name,
            $"{TreyModularIndustrialRoot}/{file}",
            World(localPosition),
            yaw,
            scale,
            collisionSize,
            collisionOffset);
    }

    private DemolitionArenaProp TideglassAuthoredProp(
        string name,
        string root,
        string file,
        Vector3 localPosition,
        float yawDegrees,
        float scale,
        Vector3 collisionSize,
        Vector3 collisionOffset)
    {
        return new DemolitionArenaProp(
            name,
            $"{root}/{file}",
            World(localPosition),
            Mathf.DegToRad(yawDegrees),
            scale,
            collisionSize,
            collisionOffset);
    }

    private Vector3 TideglassReactorStrategyTarget(string key) => key switch
    {
        "attack_entry_a" => World(new Vector3(-24.0f, 0.2f, 24.0f)),
        "attack_entry_b" => World(new Vector3(39.0f, 0.2f, -11.0f)),
        "attack_support_a" => World(new Vector3(-18.0f, 0.2f, 28.0f)),
        "attack_support_b" => World(new Vector3(40.0f, 0.2f, -6.0f)),
        "attack_mid_recon" => World(new Vector3(8.0f, 0.2f, -2.0f)),
        "defense_anchor_a" => World(new Vector3(-34.0f, 0.2f, 12.0f)),
        "defense_anchor_b" => World(new Vector3(34.0f, 0.2f, -22.0f)),
        "defense_mid" => World(new Vector3(-3.0f, 0.2f, -7.0f)),
        "defense_rotate_a" => World(new Vector3(-24.0f, 0.2f, 2.0f)),
        "defense_rotate_b" => World(new Vector3(18.0f, 0.2f, -20.0f)),
        "retake_entry_a" => World(new Vector3(-27.0f, 0.2f, 8.0f)),
        "retake_entry_b" => World(new Vector3(25.0f, 0.2f, -24.0f)),
        "retake_cover_a" => World(new Vector3(-31.0f, 0.2f, 28.0f)),
        "retake_cover_b" => World(new Vector3(38.0f, 0.2f, -25.0f)),
        "retake_flank_a" => World(new Vector3(-43.0f, 0.2f, 7.0f)),
        "retake_flank_b" => World(new Vector3(40.0f, 0.2f, -3.0f)),
        "postplant_guard_a" => World(new Vector3(-34.0f, 0.2f, 22.0f)),
        "postplant_guard_b" => World(new Vector3(34.0f, 0.2f, -15.0f)),
        "postplant_crossfire_a" => World(new Vector3(-25.0f, 0.2f, 24.0f)),
        "postplant_crossfire_b" => World(new Vector3(25.0f, 0.2f, -24.0f)),
        "postplant_lurk_a" => World(new Vector3(-15.0f, 0.2f, 28.0f)),
        "postplant_lurk_b" => World(new Vector3(18.0f, 0.2f, -28.0f)),
        "site_a" => SitePositions[0],
        "site_b" => SitePositions[1],
        _ => Midpoint
    };
}
