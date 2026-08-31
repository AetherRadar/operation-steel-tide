using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public sealed partial class DemolitionArenaLayout
{
    private const string KenneyFactoryRoot = "res://assets/models/kenney_factory_kit";
    private const string KenneyRoadsRoot = "res://assets/models/kenney_city_kit_roads";
    private const string OldMilitaryCrateRoot = "res://assets/models/old_military_crate";
    private const string MajadroidConstructionRoot = "res://assets/models/majadroid_construction_site";
    private const string PolyHavenBarrierRoot = "res://assets/models/concrete_road_barrier";
    private const string QuaterniusBuildingsRoot = "res://assets/models/quaternius_buildings_pack";
    private const string QuaterniusDowntownRoot = "res://assets/models/quaternius_downtown_city";
    private const string TreyModularIndustrialRoot = "res://assets/models/trey_modular_industrial";

    private IReadOnlyList<DemolitionArenaBox> BuildTideglassReactorCollisionBoxes()
    {
        var boxes = new List<DemolitionArenaBox>
        {
            Box("ArenaFloor", new(0, -0.48f, 0), new(136, 1.0f, 112), "ground", visible: false),
            Box("NorthPerimeter", new(0, 1.5f, -55.5f), new(136, 3.0f, 1.0f), "concrete_dark", visible: false),
            Box("SouthPerimeter", new(0, 1.5f, 55.5f), new(136, 3.0f, 1.0f), "concrete_dark", visible: false),
            Box("WestPerimeter", new(-67.5f, 1.5f, 0), new(1.0f, 3.0f, 112), "concrete_dark", visible: false),
            Box("EastPerimeter", new(67.5f, 1.5f, 0), new(1.0f, 3.0f, 112), "concrete_dark", visible: false),

            // The open construction tower keeps its sightlines: only its floor and visible column grid collide.
            Box("ConstructionTowerFoundation", new(-55.0f, 0.06f, 22.0f), new(13.9f, 0.12f, 16.6f), "concrete", visible: false),
            Box("BrickFactoryShell", new(58.0f, 10.52f, -12.25f), new(12.5f, 21.0f, 15.5f), "rust", visible: false),
            Box("GatewayWestPillar", new(-2.575f, 2.02f, -31.5f), new(0.85f, 4.0f, 0.75f), "rust", visible: false),
            Box("GatewayEastPillar", new(2.575f, 2.02f, -31.5f), new(0.85f, 4.0f, 0.75f), "rust", visible: false)
        };

        var towerColumnXs = new[] { -61.75f, -48.25f };
        var towerColumnRows = new[]
        {
            (Z: 13.9f, Height: 46.4f),
            (Z: 22.0f, Height: 46.08f),
            (Z: 30.1f, Height: 28.48f)
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
                new(-42.174f, 0.92f, -10.174f),
                new(6.3f, 1.8f, 6.3f),
                "warning",
                rotation: new Vector3(0, Mathf.DegToRad(-12.0f), 0),
                visible: false),
            Box(
                "ConstructionSouthHillNavigation",
                new(-42.71f, 0.45f, 8.38f),
                new(7.14f, 0.9f, 5.76f),
                "ground",
                visible: false),
            Box(
                "ConstructionNorthHillNavigation",
                new(-53.84f, 0.63f, 31.65f),
                new(6.87f, 1.26f, 12.35f),
                "ground",
                visible: false),
            Box(
                "ConstructionTowerNavigationFootprint",
                new(-55.0f, 0.92f, 22.0f),
                new(13.9f, 1.8f, 16.6f),
                "warning",
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
            TideglassAuthoredProp("NorthGuildHall", QuaterniusBuildingsRoot, "building1-large.glb", new(-28.6f, 0.02f, -48.3f), 90.0f, 1.8f, new(7.6f, 4.671f, 2.4f), new(0, 2.3355f, 0), DemolitionArenaPropCollisionMode.FootprintBox),
            TideglassAuthoredProp("NorthBrickOffices", QuaterniusBuildingsRoot, "building3-big.glb", new(-7.0f, 0.02f, -50.65f), 0.0f, 1.9f, new(4.4f, 5.677f, 4.1f), new(0, 2.8385f, 0), DemolitionArenaPropCollisionMode.CompoundBoxes, TideglassPorticoOfficeCollision(4.4f)),
            TideglassAuthoredProp("NorthCustomsHouse", QuaterniusBuildingsRoot, "building4.glb", new(23.0f, 0.02f, -50.25f), 90.0f, 1.9f, new(4.38f, 5.487f, 3.58f), new(0, 2.7435f, 0), DemolitionArenaPropCollisionMode.CompoundBoxes, TideglassCustomsHouseCollision()),
            TideglassAuthoredProp("SouthGatehouse", QuaterniusBuildingsRoot, "house2.glb", new(-39.5f, 0.02f, 48.22f), 180.0f, 4.2f, new(3.30f, 2.926f, 2.35f), new(-0.02f, 1.463f, -0.085f), DemolitionArenaPropCollisionMode.CompoundBoxes, TideglassGatehouseCollision()),
            TideglassIndustrial("NorthLoadingBay", "loading-bay.glb", new(52.5f, 0.02f, -49.2f), Mathf.Pi * 0.5f, 1.3f, new(8.0f, 3.1f, 4.0f), new(0, 1.55f, 0)),
            TideglassIndustrial("SouthUtilityOffice", "utility-office.glb", new(-38.0f, 0.02f, 17.0f), Mathf.Pi * 0.5f, 1.3f, new(4.0f, 3.1f, 2.0f), new(0, 1.55f, 0)),
            TideglassIndustrial("CentralServiceHall", "sawtooth-service-hall.glb", new(-3.0f, 0.02f, -0.8f), 0.0f, 1.5f, new(8.0f, 3.1f, 4.0f), new(0, 1.55f, 0)),
            TideglassIndustrial("WestWindowHall", "window-hall.glb", new(-54.0f, 0.02f, -3.5f), Mathf.Pi * 0.5f, 2.5f, new(8.0f, 3.0f, 4.0f), new(0, 1.5f, 0)),
            TideglassIndustrial("ConstructionTurbineWorkshop", "turbine-workshop.glb", new(-31.0f, 0.02f, 16.0f), 0.0f, 1.0f, new(8.0f, 4.5f, 6.0f), new(0, 2.25f, 0)),
            TideglassIndustrial("ReactorAnnex", "reactor-annex.glb", new(31.0f, 0.02f, -24.85f), 0.0f, 1.4f, new(10.0f, 3.1f, 6.0f), new(0, 1.55f, 0)),
            TideglassIndustrial("EastShiftOffice", "shift-office.glb", new(57.0f, 0.02f, 25.0f), 0.0f, 1.0f, new(6.0f, 3.5f, 4.0f), new(0, 1.75f, 0)),
            TideglassIndustrial("EastInspectionOffice", "inspection-office.glb", new(56.0f, 0.02f, 18.3f), 0.0f, 1.0f, new(6.0f, 3.1f, 4.0f), new(0, 1.55f, -0.65f), DemolitionArenaPropCollisionMode.CompoundBoxes, TideglassInspectionOfficeCollision()),
            TideglassIndustrial("MidCompressorHouse", "compressor-house.glb", new(18.5f, 0.02f, -4.5f), Mathf.Pi * 0.5f, 2.0f, new(8.0f, 3.3f, 6.0f), new(0, 1.65f, 0)),
            TideglassIndustrial("ReactorBoilerWorkshop", "boiler-workshop.glb", new(10.0f, 0.02f, -19.3f), Mathf.Pi * 0.5f, 1.25f, new(10.0f, 6.1f, 6.0f), new(0, 3.05f, 0)),
            TideglassIndustrial("EastSwitchgearHall", "switchgear-hall.glb", new(37.0f, 0.02f, 31.0f), 0.0f, 1.0f, new(8.0f, 4.5f, 6.0f), new(0, 2.25f, 0)),
            TideglassIndustrial("MidCrewCanteen", "crew-canteen.glb", new(0.0f, 0.02f, 12.25f), Mathf.Pi * 0.5f, 2.1f, new(8.0f, 3.5f, 6.0f), new(0, 1.75f, 0)),
            TideglassIndustrial("CivicUtilityOffice", "utility-office.glb", new(-12.5f, 0.02f, 23.55f), -Mathf.Pi * 0.5f, 1.3f, new(4.0f, 3.1f, 2.0f), new(0, 1.55f, 0)),
            TideglassIndustrial("CivicPumpHouse", "pump-house.glb", new(-18.5f, 0.02f, -23.5f), Mathf.Pi * 0.5f, 1.3f, new(6.0f, 4.5f, 6.0f), new(0, 2.25f, 0)),
            TideglassIndustrial("SouthGlassworksOffice", "glassworks-office.glb", new(-6.0f, 0.02f, 34.5f), 0.0f, 1.2f, new(10.0f, 3.5f, 8.0f), new(0, 1.75f, -0.65f), DemolitionArenaPropCollisionMode.CompoundBoxes, TideglassGlassworksOfficeCollision()),
            TideglassIndustrial("MidControlRoom", "control-room.glb", new(-15.4f, 0.02f, 3.0f), 0.0f, 1.2f, new(8.0f, 6.1f, 8.0f), new(0, 3.05f, 0)),
            TideglassIndustrial("EastTransformerWorks", "transformer-works.glb", new(36.2f, 0.02f, -2.2f), 0.0f, 1.7f, new(12.0f, 3.5f, 8.0f), new(0, 1.75f, 0)),
            TideglassIndustrial("CivicCoolingServiceHall", "cooling-service-hall.glb", new(16.0f, 0.02f, 26.0f), Mathf.Pi * 0.5f, 1.6f, new(12.0f, 4.5f, 8.0f), new(0, 2.25f, 0)),
            TideglassIndustrial("ReactorMaintenanceDepot", "maintenance-depot.glb", new(39.0f, 0.02f, -40.4f), 0.0f, 2.0f, new(10.0f, 4.5f, 6.0f), new(0, 2.25f, 0)),
            TideglassIndustrial("WestFoundryWarehouse", "foundry-warehouse.glb", new(-31.6f, 0.02f, 4.9f), 0.0f, 1.5f, new(12.0f, 4.5f, 8.0f), new(0, 2.25f, 0)),
            TideglassIndustrial("WestFoundryInspectionAnnex", "inspection-office.glb", new(-44.54f, 0.02f, 0.0f), 0.0f, 1.12f, new(6.0f, 3.1f, 4.0f), new(0, 1.55f, -0.65f), DemolitionArenaPropCollisionMode.CompoundBoxes, TideglassInspectionOfficeCollision()),
            TideglassIndustrial("NorthFreightOffice", "inspection-office.glb", new(19.2f, 0.02f, -32.4f), Mathf.Pi * 0.5f, 1.5f, new(6.0f, 3.1f, 4.0f), new(0, 1.55f, -0.65f), DemolitionArenaPropCollisionMode.CompoundBoxes, TideglassInspectionOfficeCollision()),
            TideglassIndustrial("EastOperationsOffice", "shift-office.glb", new(58.2f, 0.02f, 37.8f), 0.0f, 1.8f, new(6.0f, 3.5f, 4.0f), new(0, 1.75f, 0)),
            TideglassIndustrial("SouthWorksOffice", "shift-office.glb", new(-14.0f, 0.02f, 16.0f), 0.0f, 1.4f, new(6.0f, 3.5f, 4.0f), new(0, 1.75f, 0)),
            TideglassIndustrial("WestGateOffice", "inspection-office.glb", new(-55.0f, 0.02f, 42.5f), 0.0f, 1.8f, new(6.0f, 3.1f, 4.0f), new(0, 1.55f, -0.65f), DemolitionArenaPropCollisionMode.CompoundBoxes, TideglassInspectionOfficeCollision()),
            TideglassIndustrial("SouthTransitOffice", "shift-office.glb", new(10.0f, 0.02f, 42.5f), 0.0f, 1.5f, new(6.0f, 3.5f, 4.0f), new(0, 1.75f, 0)),
            TideglassIndustrial("MidDispatchOffice", "shift-office.glb", new(-1.2f, 0.02f, -17.0f), 0.0f, 1.6f, new(6.0f, 3.5f, 4.0f), new(0, 1.75f, 0)),

            TideglassAuthoredProp("SouthRegistryHouse", QuaterniusBuildingsRoot, "building1-small.glb", new(7.0f, 0.02f, 51.0f), 180.0f, 1.9f, new(3.36f, 4.663f, 2.41f), new(0, 2.3315f, 0.125f), DemolitionArenaPropCollisionMode.FootprintBox),
            TideglassAuthoredProp("SightBlockEastApproachOffices", QuaterniusBuildingsRoot, "building2-large.glb", new(48.0f, 0.02f, 24.0f), 0.0f, 1.6f, new(5.40f, 5.918f, 2.08f), new(0, 2.959f, 0), DemolitionArenaPropCollisionMode.CompoundBoxes, TideglassEastApproachOfficeCollision()),
            TideglassAuthoredProp("MidTelegraphHouse", QuaterniusBuildingsRoot, "building2-small.glb", new(-21.0f, 0.02f, -11.8f), 90.0f, 3.0f, new(3.37f, 4.968f, 2.33f), new(0, 2.484f, 0), DemolitionArenaPropCollisionMode.FootprintBox),
            TideglassAuthoredProp("DefenderServiceBlock", QuaterniusBuildingsRoot, "building3-small.glb", new(-61.1f, 0.02f, -29.5f), 90.0f, 3.0f, new(2.86f, 5.677f, 4.10f), new(0, 2.8385f, 0), DemolitionArenaPropCollisionMode.CompoundBoxes, TideglassPorticoOfficeCollision(2.86f)),
            TideglassAuthoredProp("SouthwestWatchHouse", QuaterniusBuildingsRoot, "house1.glb", new(-25.9f, 0.02f, 34.2f), 180.0f, 4.2f, new(2.10f, 3.179f, 3.62f), new(0, 1.5895f, 0), DemolitionArenaPropCollisionMode.CompoundBoxes, TideglassWatchHouseCollision()),
            TideglassAuthoredProp("DefenderArchiveBlock", QuaterniusDowntownRoot, "Building_Medium_2_001.gltf", new(-42.0f, 0.02f, -14.7f), 0.0f, 1.0f, new(14.0f, 25.01f, 12.0f), new(0.0f, 12.5f, -6.0f), DemolitionArenaPropCollisionMode.FootprintBox),
            TideglassAuthoredProp("NorthFoundryTenement", QuaterniusDowntownRoot, "Building_Small_1.gltf", new(-17.5f, 0.02f, -31.2f), 0.0f, 1.0f, new(12.0f, 17.03f, 12.0f), new(-1.0f, 8.5f, -6.0f), DemolitionArenaPropCollisionMode.CompoundBoxes, TideglassFoundryTenementCollision()),
            TideglassAuthoredProp("SightBlockConstructionSiteOffice", MajadroidConstructionRoot, "containers-office.glb", new(24.5f, 0.02f, 42.6f), 0.0f, 1.0f, new(11.45f, 5.76f, 8.11f), new(0, 2.88f, 0), DemolitionArenaPropCollisionMode.AuthoredConcave, TideglassConstructionOfficeAnalyticalCollision(), authoredBackfaceCollision: true, authoredSolidCollisionPieceCount: 2),
            TideglassAuthoredProp("SightBlockReactorCargoContainers", MajadroidConstructionRoot, "containers-cargo.glb", new(52.0f, 0.02f, 5.0f), 90.0f, 0.82f, new(3.0f, 2.6f, 8.4f), new(0, 1.3f, 0), DemolitionArenaPropCollisionMode.AuthoredConcave, TideglassCargoContainersAnalyticalCollision(), authoredBackfaceCollision: true),
            TideglassAuthoredProp("ConstructionTruck", MajadroidConstructionRoot, "concrete-truck-red.glb", new(-63.5f, 0.02f, 32.0f), 90.0f, 1.05f, new(3.13f, 3.82f, 7.18f), new(0, 1.9075f, 0), DemolitionArenaPropCollisionMode.AuthoredConcave, TideglassConstructionTruckAnalyticalCollision(), authoredBackfaceCollision: true),

            TideglassAuthoredProp("MidCoverConstructionSupplies", MajadroidConstructionRoot, "construction-materials.glb", new(-5.0f, 0.02f, 22.7f), 90.0f, 1.0f, new(1.65f, 1.32f, 8.75f), new(0, 0.6025f, 0), DemolitionArenaPropCollisionMode.AuthoredConcave, TideglassConstructionMaterialsAnalyticalCollision(), authoredBackfaceCollision: true),
            TideglassAuthoredProp("MidCoverDumpster", KenneyRoadsRoot, "dumpster.glb", new(-13.0f, 0.02f, -10.0f), -18.0f, 6.0f, new(0.295f, 0.225f, 0.39f), new(0.0075f, 0.1047f, 0)),
            TideglassAuthoredProp("MidCoverMachine", KenneyFactoryRoot, "machine.glb", new(46.5f, 0.02f, 41.0f), 15.0f, 2.0f, new(1.3f, 1.4f, 1.6f), new(0, 0.6499f, 0)),
            TideglassAuthoredProp("MidCoverHopper", KenneyFactoryRoot, "hopper-high-round.glb", new(17.0f, 0.02f, 9.0f), -12.0f, 1.8f, new(1.2f, 1.58f, 1.2f), new(0, 0.75f, 0), DemolitionArenaPropCollisionMode.AuthoredConcave, authoredBackfaceCollision: true),
            TideglassAuthoredProp("MidCoverCivicPlanter", QuaterniusDowntownRoot, "Prop_Planter_Single.gltf", new(52.0f, 0.02f, 9.2f), 90.0f, 2.3f, new(2.0f, 0.6f, 2.0f), new(0, 0.3f, 0)),
            TideglassAuthoredProp("MidCoverRoadBarrier", KenneyRoadsRoot, "construction-barrier.glb", new(7.0f, 0.02f, -11.0f), 90.0f, 10.0f, new(0.15f, 0.14f, 0.24f), new(0, 0.065f, 0), DemolitionArenaPropCollisionMode.AuthoredConcave, authoredBackfaceCollision: true),
            TideglassAuthoredProp("MidCoverGenerator", KenneyFactoryRoot, "machine-window.glb", new(2.5f, 0.02f, -39.0f), 20.0f, 2.0f, new(1.3f, 1.38f, 1.6f), new(0, 0.6449f, 0)),
            TideglassAuthoredProp("DefenderCourtyardGenerator", KenneyFactoryRoot, "machine-window.glb", new(-40.0f, 0.02f, -35.3f), -15.0f, 2.0f, new(1.3f, 1.38f, 1.6f), new(0, 0.6449f, 0)),
            TideglassAuthoredProp("ConstructionLaneGenerator", KenneyFactoryRoot, "machine-window.glb", new(-42.5f, 0.02f, 30.3f), -20.0f, 2.0f, new(1.3f, 1.38f, 1.6f), new(0, 0.6449f, 0)),
            TideglassAuthoredProp("EastSiteGenerator", KenneyFactoryRoot, "machine-window.glb", new(57.0f, 0.02f, -24.6f), 40.0f, 2.0f, new(1.3f, 1.38f, 1.6f), new(0, 0.6449f, 0)),
            TideglassAuthoredProp("SouthPerimeterGenerator", KenneyFactoryRoot, "machine-window.glb", new(27.0f, 0.02f, 52.0f), 20.0f, 2.0f, new(1.3f, 1.38f, 1.6f), new(0, 0.6449f, 0)),
            TideglassAuthoredProp("MidCoverConcreteBarrier", PolyHavenBarrierRoot, "concrete_road_barrier.gltf", new(-25.0f, 0.02f, 13.2f), 90.0f, 1.7f, new(1.55f, 0.84f, 0.64f), new(0, 0.41f, 0)),
            TideglassAuthoredProp("ConstructionSupplyCrate", OldMilitaryCrateRoot, "old_military_crate.gltf", new(-37.0f, 0.02f, 32.0f), 12.0f, 2.5f, new(1.8154f, 0.3009f, 0.9791f), new(-0.0053f, 0.1505f, -0.1809f)),
            TideglassAuthoredProp("ReactorPipeManifold", KenneyFactoryRoot, "pipe-large-bend.glb", new(-46.0f, 0.02f, -39.0f), -90.0f, 1.6f, new(1.9f, 1.0002f, 1.9f), new(0.05f, 0.4999f, -0.45f), DemolitionArenaPropCollisionMode.AuthoredConcave, authoredBackfaceCollision: true),
            TideglassAuthoredProp("CrossingTrafficLight", KenneyRoadsRoot, "traffic-light.glb", new(8.0f, 0.02f, -28.0f), 0.0f, 7.5f, new(0.13f, 0.53f, 0.1f), new(-0.0138f, 0.2575f, 0), DemolitionArenaPropCollisionMode.AuthoredConcave, authoredBackfaceCollision: true)
        };
    }

    private static IReadOnlyList<DemolitionArenaPropCollisionBox> TideglassInspectionOfficeCollision()
        => new[]
        {
            new DemolitionArenaPropCollisionBox(new(6.0f, 3.1f, 4.0f), new(0, 1.55f, -0.65f)),
            new DemolitionArenaPropCollisionBox(new(0.36f, 2.8f, 0.36f), new(-0.8f, 1.4f, 2.7f)),
            new DemolitionArenaPropCollisionBox(new(0.36f, 2.8f, 0.36f), new(0.8f, 1.4f, 2.7f))
        };

    private static IReadOnlyList<DemolitionArenaPropCollisionBox> TideglassGlassworksOfficeCollision()
        => new[]
        {
            new DemolitionArenaPropCollisionBox(new(10.0f, 3.5f, 8.0f), new(0, 1.75f, -0.65f)),
            new DemolitionArenaPropCollisionBox(new(0.36f, 2.8f, 0.36f), new(-0.9f, 1.4f, 4.7f)),
            new DemolitionArenaPropCollisionBox(new(0.36f, 2.8f, 0.36f), new(0.9f, 1.4f, 4.7f))
        };

    private static IReadOnlyList<DemolitionArenaPropCollisionBox> TideglassPorticoOfficeCollision(
        float shellWidth)
        => new[]
        {
            new DemolitionArenaPropCollisionBox(new(shellWidth, 5.677f, 3.70f), new(0, 2.8385f, -0.20f)),
            new DemolitionArenaPropCollisionBox(new(0.55f, 1.25f, 0.10f), new(0, 0.625f, 1.82f)),
            new DemolitionArenaPropCollisionBox(new(0.14f, 1.24f, 0.14f), new(-0.344f, 0.81f, 1.95f)),
            new DemolitionArenaPropCollisionBox(new(0.14f, 1.24f, 0.14f), new(0.344f, 0.81f, 1.95f)),
            new DemolitionArenaPropCollisionBox(new(0.75f, 0.12f, 0.42f), new(0, 0.06f, 1.84f))
        };

    private static IReadOnlyList<DemolitionArenaPropCollisionBox> TideglassCustomsHouseCollision()
        => new[]
        {
            new DemolitionArenaPropCollisionBox(new(1.60f, 5.487f, 3.58f), new(0, 2.7435f, 0)),
            new DemolitionArenaPropCollisionBox(new(1.00f, 5.487f, 2.60f), new(-1.30f, 2.7435f, 0)),
            new DemolitionArenaPropCollisionBox(new(1.00f, 5.487f, 2.60f), new(1.30f, 2.7435f, 0)),
            new DemolitionArenaPropCollisionBox(new(0.39f, 5.487f, 2.04f), new(-1.995f, 2.7435f, 0)),
            new DemolitionArenaPropCollisionBox(new(0.39f, 5.487f, 2.04f), new(1.995f, 2.7435f, 0))
        };

    private static IReadOnlyList<DemolitionArenaPropCollisionBox> TideglassGatehouseCollision()
        => new[]
        {
            new DemolitionArenaPropCollisionBox(new(3.30f, 2.926f, 2.35f), new(-0.02f, 1.463f, -0.085f)),
            new DemolitionArenaPropCollisionBox(new(0.85f, 0.10f, 0.45f), new(0.08f, 0.05f, 1.315f))
        };

    private static IReadOnlyList<DemolitionArenaPropCollisionBox> TideglassEastApproachOfficeCollision()
        => new[]
        {
            new DemolitionArenaPropCollisionBox(new(5.40f, 4.718f, 2.08f), new(0, 3.559f, 0)),
            new DemolitionArenaPropCollisionBox(new(3.20f, 1.20f, 2.08f), new(0, 0.60f, 0)),
            new DemolitionArenaPropCollisionBox(new(0.20f, 1.20f, 2.08f), new(-2.60f, 0.60f, 0)),
            new DemolitionArenaPropCollisionBox(new(0.20f, 1.20f, 2.08f), new(2.60f, 0.60f, 0))
        };

    private static IReadOnlyList<DemolitionArenaPropCollisionBox> TideglassWatchHouseCollision()
        => new[]
        {
            new DemolitionArenaPropCollisionBox(new(2.10f, 3.179f, 2.95f), new(0, 1.5895f, -0.335f)),
            new DemolitionArenaPropCollisionBox(new(0.70f, 0.12f, 0.28f), new(0, 0.09f, 1.26f)),
            new DemolitionArenaPropCollisionBox(
                new(0.70f, 0.06f, 0.72f),
                new(0, 0.075f, 1.535f),
                new(0.21f, 0, 0)),
            new DemolitionArenaPropCollisionBox(new(0.08f, 0.70f, 0.08f), new(-0.68f, 0.50f, 1.62f)),
            new DemolitionArenaPropCollisionBox(new(0.08f, 0.70f, 0.08f), new(0.68f, 0.50f, 1.62f))
        };

    private static IReadOnlyList<DemolitionArenaPropCollisionBox> TideglassConstructionOfficeAnalyticalCollision()
        => new[]
        {
            new DemolitionArenaPropCollisionBox(new(3.0f, 2.6f, 7.0f), new(-4.225f, 1.856f, -0.556f)),
            // Stop the lower-room seal below the upper landing so the player capsule
            // cannot catch its invisible top edge while crossing the authored doorway.
            new DemolitionArenaPropCollisionBox(new(7.0f, 1.9f, 3.0f), new(0.775f, 1.506f, 1.444f)),
            new DemolitionArenaPropCollisionBox(new(7.0f, 2.6f, 3.0f), new(-2.225f, 4.456f, 1.444f))
        };

    private static IReadOnlyList<DemolitionArenaPropCollisionBox> TideglassConstructionMaterialsAnalyticalCollision()
        => new[]
        {
            new DemolitionArenaPropCollisionBox(new(1.0f, 1.2f, 1.0f), new(0.007f, 0.605f, 3.845f)),
            new DemolitionArenaPropCollisionBox(new(1.17f, 1.04f, 2.0f), new(-0.04f, 0.52f, -3.347f)),
            new DemolitionArenaPropCollisionBox(new(1.51f, 0.8f, 4.91f), new(0, 0.4f, 0.247f))
        };

    private static IReadOnlyList<DemolitionArenaPropCollisionBox> TideglassCargoContainersAnalyticalCollision()
        => new[]
        {
            new DemolitionArenaPropCollisionBox(new(3.0f, 2.6f, 7.0f), new(0, 1.3f, 0.7f))
        };

    private static IReadOnlyList<DemolitionArenaPropCollisionBox> TideglassConstructionTruckAnalyticalCollision()
        => new[]
        {
            new DemolitionArenaPropCollisionBox(new(2.5f, 1.15f, 6.7f), new(0, 0.58f, 0)),
            new DemolitionArenaPropCollisionBox(new(2.5f, 2.2f, 2.1f), new(0, 1.55f, 2.5f)),
            new DemolitionArenaPropCollisionBox(new(2.8f, 2.7f, 3.8f), new(0, 2.15f, -0.55f))
        };

    private static IReadOnlyList<DemolitionArenaPropCollisionBox> TideglassFoundryTenementCollision()
        => new[]
        {
            // The source facade has a real stair-served doorway at local X=0, +Z.
            // Build a hollow perimeter around it instead of sealing the whole building.
            new DemolitionArenaPropCollisionBox(new(12.0f, 17.03f, 0.50f), new(-1.0f, 8.5f, -11.75f)),
            new DemolitionArenaPropCollisionBox(new(0.50f, 17.03f, 11.50f), new(-6.75f, 8.5f, -6.0f)),
            new DemolitionArenaPropCollisionBox(new(0.50f, 17.03f, 11.50f), new(4.75f, 8.5f, -6.0f)),
            new DemolitionArenaPropCollisionBox(new(5.95f, 17.03f, 0.50f), new(-4.025f, 8.5f, -0.25f)),
            new DemolitionArenaPropCollisionBox(new(3.95f, 17.03f, 0.50f), new(3.025f, 8.5f, -0.25f)),
            new DemolitionArenaPropCollisionBox(new(2.10f, 14.00f, 0.50f), new(0, 10.03f, -0.25f)),
            new DemolitionArenaPropCollisionBox(new(11.50f, 0.20f, 11.25f), new(-1.0f, 0.90f, -6.12f)),
            new DemolitionArenaPropCollisionBox(new(2.0f, 1.00f, 0.40f), new(0, 0.50f, 0.20f)),
            new DemolitionArenaPropCollisionBox(new(2.0f, 0.80f, 0.40f), new(0, 0.40f, 0.60f)),
            new DemolitionArenaPropCollisionBox(new(2.0f, 0.60f, 0.40f), new(0, 0.30f, 1.00f)),
            new DemolitionArenaPropCollisionBox(new(2.0f, 0.40f, 0.40f), new(0, 0.20f, 1.40f)),
            new DemolitionArenaPropCollisionBox(new(2.0f, 0.20f, 0.40f), new(0, 0.10f, 1.80f))
        };

    private IReadOnlyList<Vector3> BuildTideglassReactorAttackToAPath() => WorldPoints(
        new(52.0f, 0.2f, 48.0f), new(40.0f, 0.2f, 48.0f),
        new(25.0f, 0.2f, 48.5f), new(16.0f, 0.2f, 47.0f),
        new(3.0f, 0.2f, 47.0f), new(-12.0f, 0.2f, 42.0f),
        new(-15.0f, 0.2f, 39.0f), new(-16.0f, 0.2f, 35.0f),
        new(-18.0f, 0.2f, 24.0f),
        new(-31.0f, 0.2f, 23.0f), new(-40.0f, 0.2f, 24.0f));

    private IReadOnlyList<Vector3> BuildTideglassReactorAttackToBPath() => WorldPoints(
        new(52.0f, 0.2f, 48.0f), new(65.5f, 0.2f, 43.0f),
        new(65.5f, 0.2f, 34.0f), new(65.5f, 0.2f, 22.0f),
        new(63.0f, 0.2f, 12.0f), new(65.0f, 0.2f, 7.0f),
        new(57.0f, 0.2f, 2.0f), new(49.0f, 0.2f, -5.0f),
        new(49.0f, 0.2f, -10.0f), new(49.0f, 0.2f, -18.0f),
        new(47.0f, 0.2f, -23.0f), new(42.0f, 0.2f, -25.0f));

    private IReadOnlyList<Vector3> BuildTideglassReactorAttackMidPath() => WorldPoints(
        new(52.0f, 0.2f, 48.0f), new(44.0f, 0.2f, 46.0f),
        new(36.0f, 0.2f, 40.0f), new(31.0f, 0.2f, 35.0f),
        new(29.0f, 0.2f, 28.0f), new(27.0f, 0.2f, 22.0f),
        new(24.0f, 0.2f, 15.0f), new(14.0f, 0.2f, 10.0f),
        new(8.5f, 0.2f, 8.0f), new(8.5f, 0.2f, 5.0f),
        new(8.5f, 0.2f, -5.0f), new(2.0f, 0.2f, -10.0f));

    private IReadOnlyList<Vector3> BuildTideglassReactorDefenderToAPath() => WorldPoints(
        new(-52.0f, 0.2f, -48.0f), new(-53.0f, 0.2f, -41.0f),
        new(-53.0f, 0.2f, -34.0f), new(-53.0f, 0.2f, -24.0f),
        new(-61.0f, 0.2f, -18.0f), new(-62.0f, 0.2f, -12.0f),
        new(-62.0f, 0.2f, -4.0f), new(-62.0f, 0.2f, 2.0f),
        new(-62.0f, 0.2f, 8.0f), new(-54.0f, 0.2f, 9.0f),
        new(-48.0f, 0.2f, 10.0f),
        new(-44.0f, 0.2f, 18.0f),
        new(-40.0f, 0.2f, 24.0f));

    private IReadOnlyList<Vector3> BuildTideglassReactorDefenderToBPath() => WorldPoints(
        new(-52.0f, 0.2f, -48.0f), new(-43.0f, 0.2f, -44.0f),
        new(-35.0f, 0.2f, -34.0f), new(-29.0f, 0.2f, -30.0f),
        new(-28.0f, 0.2f, -28.0f), new(-27.0f, 0.2f, -10.0f),
        new(-25.0f, 0.2f, -5.3f), new(-16.0f, 0.2f, -5.3f),
        new(-8.0f, 0.2f, -5.3f), new(-8.0f, 0.2f, -11.5f),
        new(-8.0f, 0.2f, -24.0f), new(-7.0f, 0.2f, -28.0f),
        new(-7.0f, 0.2f, -32.0f), new(-5.0f, 0.2f, -35.0f),
        new(0.0f, 0.2f, -36.0f), new(8.0f, 0.2f, -35.0f),
        new(13.0f, 0.2f, -38.0f), new(26.0f, 0.2f, -38.0f),
        new(27.0f, 0.2f, -32.0f), new(40.5f, 0.2f, -29.0f),
        new(42.0f, 0.2f, -25.0f));

    private IReadOnlyList<Vector3> BuildTideglassReactorSiteRotationPath() => WorldPoints(
        new(-40.0f, 0.2f, 24.0f), new(-34.0f, 0.2f, 24.0f),
        new(-26.0f, 0.2f, 23.0f), new(-23.0f, 0.2f, 16.0f),
        new(-21.5f, 0.2f, 12.0f), new(-21.5f, 0.2f, 9.0f),
        new(-21.5f, 0.2f, 3.0f), new(-21.5f, 0.2f, -5.3f),
        new(-16.0f, 0.2f, -5.3f),
        new(-7.0f, 0.2f, -5.3f), new(-8.0f, 0.2f, -11.5f),
        new(-8.0f, 0.2f, -24.0f), new(-7.0f, 0.2f, -29.0f),
        new(-7.0f, 0.2f, -34.0f), new(0.0f, 0.2f, -34.0f),
        new(7.0f, 0.2f, -36.0f),
        new(13.0f, 0.2f, -38.0f), new(26.0f, 0.2f, -38.0f),
        new(27.0f, 0.2f, -32.0f), new(40.5f, 0.2f, -29.0f),
        new(42.0f, 0.2f, -25.0f));

    private DemolitionArenaProp TideglassIndustrial(
        string name,
        string file,
        Vector3 localPosition,
        float yaw,
        float scale,
        Vector3 collisionSize,
        Vector3 collisionOffset,
        DemolitionArenaPropCollisionMode collisionMode = DemolitionArenaPropCollisionMode.FootprintBox,
        IReadOnlyList<DemolitionArenaPropCollisionBox>? collisionPieces = null)
    {
        return new DemolitionArenaProp(
            name,
            $"{TreyModularIndustrialRoot}/{file}",
            World(localPosition),
            yaw,
            scale,
            collisionSize,
            collisionOffset,
            collisionMode,
            collisionPieces);
    }

    private DemolitionArenaProp TideglassAuthoredProp(
        string name,
        string root,
        string file,
        Vector3 localPosition,
        float yawDegrees,
        float scale,
        Vector3 collisionSize,
        Vector3 collisionOffset,
        DemolitionArenaPropCollisionMode collisionMode = DemolitionArenaPropCollisionMode.BoundsBox,
        IReadOnlyList<DemolitionArenaPropCollisionBox>? collisionPieces = null,
        bool authoredBackfaceCollision = false,
        int authoredSolidCollisionPieceCount = 0)
    {
        return new DemolitionArenaProp(
            name,
            $"{root}/{file}",
            World(localPosition),
            Mathf.DegToRad(yawDegrees),
            scale,
            collisionSize,
            collisionOffset,
            collisionMode,
            collisionPieces,
            authoredBackfaceCollision,
            authoredSolidCollisionPieceCount);
    }

    private Vector3 TideglassReactorStrategyTarget(string key) => key switch
    {
        "attack_entry_a" => World(new Vector3(-35.0f, 0.2f, 27.0f)),
        "attack_entry_b" => World(new Vector3(46.0f, 0.2f, -10.0f)),
        "attack_support_a" => World(new Vector3(-18.0f, 0.2f, 24.0f)),
        "attack_support_b" => World(new Vector3(49.0f, 0.2f, 2.0f)),
        "attack_mid_recon" => World(new Vector3(7.0f, 0.2f, -5.0f)),
        "defense_anchor_a" => World(new Vector3(-46.0f, 0.2f, 16.0f)),
        "defense_anchor_b" => World(new Vector3(42.0f, 0.2f, -30.0f)),
        "defense_mid" => World(new Vector3(-7.0f, 0.2f, -8.0f)),
        "defense_rotate_a" => World(new Vector3(-21.5f, 0.2f, 2.0f)),
        "defense_rotate_b" => World(new Vector3(20.0f, 0.2f, -21.0f)),
        "retake_entry_a" => World(new Vector3(-44.0f, 0.2f, 18.0f)),
        "retake_entry_b" => World(new Vector3(40.0f, 0.2f, -28.0f)),
        "retake_cover_a" => World(new Vector3(-46.0f, 0.2f, 32.0f)),
        "retake_cover_b" => World(new Vector3(53.0f, 0.2f, -31.0f)),
        "retake_flank_a" => World(new Vector3(-62.0f, 0.2f, 8.0f)),
        "retake_flank_b" => World(new Vector3(56.0f, 0.2f, -1.0f)),
        "postplant_guard_a" => World(new Vector3(-46.0f, 0.2f, 29.0f)),
        "postplant_guard_b" => World(new Vector3(45.0f, 0.2f, -17.0f)),
        "postplant_crossfire_a" => World(new Vector3(-31.0f, 0.2f, 24.0f)),
        "postplant_crossfire_b" => World(new Vector3(42.0f, 0.2f, -18.0f)),
        "postplant_lurk_a" => World(new Vector3(-18.0f, 0.2f, 31.0f)),
        "postplant_lurk_b" => World(new Vector3(12.0f, 0.2f, -34.0f)),
        "site_a" => SitePositions[0],
        "site_b" => SitePositions[1],
        _ => Midpoint
    };
}
