using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public sealed partial class DemolitionArenaLayout
{
    private const string KenneyIndustrialRoot = "res://assets/models/kenney_city_kit_industrial";

    private IReadOnlyList<DemolitionArenaBox> BuildHarborLocksCollisionBoxes()
    {
        return new[]
        {
            Box("ArenaFloor", new(0, -0.48f, 0), new(80, 1.0f, 112), "ground"),
            Box("NorthPerimeter", new(0, 2.5f, -55.5f), new(80, 5.0f, 1.0f), "concrete_dark"),
            Box("SouthPerimeterLeft", new(-24, 2.5f, 55.5f), new(32, 5.0f, 1.0f), "concrete_dark"),
            Box("SouthPerimeterRight", new(24, 2.5f, 55.5f), new(32, 5.0f, 1.0f), "concrete_dark"),
            Box("WestPerimeter", new(-39.5f, 2.5f, 0), new(1.0f, 5.0f, 112), "concrete_dark"),
            Box("EastPerimeter", new(39.5f, 2.5f, 0), new(1.0f, 5.0f, 112), "concrete_dark"),

            Box("SightBlockLockWest", new(-12.0f, 1.65f, 36.0f), new(1.4f, 3.3f, 17.0f), "steel_dark"),
            Box("SightBlockLockEast", new(10.0f, 1.65f, 34.0f), new(1.4f, 3.3f, 17.0f), "steel_dark"),
            Box("AttackQuayGateLeft", new(-14.4f, 1.6f, 52.0f), new(13.0f, 3.2f, 1.0f), "concrete"),
            Box("AttackQuayGateRight", new(14.4f, 1.6f, 52.0f), new(13.0f, 3.2f, 1.0f), "concrete"),

            Box("WestLockWall", new(-17.0f, 1.7f, 24.0f), new(1.0f, 3.4f, 26.0f), "concrete_dark"),
            Box("EastLockWall", new(17.0f, 1.7f, -10.0f), new(1.0f, 3.4f, 26.0f), "concrete_dark"),
            Box("MidGateWest", new(-8.0f, 1.7f, 2.0f), new(12.0f, 3.4f, 1.0f), "steel_dark"),
            Box("MidGateEast", new(8.0f, 1.7f, 2.0f), new(12.0f, 3.4f, 1.0f), "steel_dark"),
            Box("MidGateNorth", new(0, 1.7f, -2.0f), new(4.0f, 3.4f, 1.0f), "steel_dark"),
            Box("MidLockSignal", new(0, 2.9f, -1.0f), new(1.2f, 5.8f, 1.2f), "warning"),
            Box("MidPumpCore", new(0, 2.25f, -13.5f), new(8.0f, 4.5f, 8.0f), "concrete_dark", visible: false),
            Box("MidCoverWestPump", new(-6.8f, 1.35f, 17.5f), new(4.0f, 2.7f, 7.0f), "steel", visible: false),
            Box("MidCoverEastPump", new(6.8f, 1.35f, 12.0f), new(4.0f, 2.7f, 6.5f), "steel", visible: false),
            Box("MidCoverAttackWest", new(-4.8f, 1.05f, 31.0f), new(2.6f, 2.1f, 4.2f), "concrete", visible: false),
            Box("MidCoverAttackEast", new(4.6f, 1.05f, 27.0f), new(2.6f, 2.1f, 4.2f), "concrete", visible: false),
            Box("MidCoverGantryWest", new(-14.0f, 2.6f, 22.0f), new(0.8f, 5.2f, 0.8f), "steel_dark"),
            Box("MidCoverGantryEast", new(14.0f, 2.6f, 22.0f), new(0.8f, 5.2f, 0.8f), "steel_dark"),
            Box("MidCoverDefenderWest", new(-7.0f, 1.15f, -31.0f), new(4.0f, 2.3f, 5.5f), "concrete", visible: false),
            Box("MidCoverDefenderEast", new(7.0f, 1.15f, -36.0f), new(4.0f, 2.3f, 5.5f), "concrete", visible: false),
            Box("MidCoverHarborOffice", new(23.0f, 2.7f, 22.0f), new(5.0f, 5.4f, 5.5f), "concrete_dark", visible: false),
            Box("MidCoverHarborOfficeWing", new(18.8f, 1.25f, 22.0f), new(3.2f, 2.5f, 7.0f), "steel"),
            Box("MidCoverServiceBay", new(-22.0f, 1.65f, -26.0f), new(7.0f, 3.3f, 8.0f), "concrete_dark", visible: false),
            Box("MidCoverServicePump", new(-17.0f, 1.0f, -29.0f), new(2.5f, 2.0f, 3.5f), "steel"),

            Box("WestControlNorthWall", new(-36.0f, 3.0f, -1.0f), new(7.0f, 6.0f, 1.0f), "concrete"),
            Box("WestControlSouthWall", new(-36.0f, 3.0f, 43.0f), new(7.0f, 6.0f, 1.0f), "concrete"),
            Box("WestControlQuayWall", new(-38.5f, 3.0f, 21.0f), new(1.0f, 6.0f, 45.0f), "concrete_dark"),
            Box("SightBlockA1", new(-33.0f, 1.65f, 10.0f), new(12.0f, 3.3f, 1.0f), "steel"),
            Box("SightBlockA2", new(-33.0f, 1.65f, 33.0f), new(12.0f, 3.3f, 1.0f), "steel"),
            Box("WestControlPump", new(-35.5f, 2.2f, 14.0f), new(4.6f, 4.4f, 5.0f), "steel_dark"),
            Box("WestControlConsole", new(-35.5f, 1.35f, 29.0f), new(4.5f, 2.7f, 6.5f), "concrete"),

            Box("EastPumpQuayWall", new(39.5f, 3.5f, -20.0f), new(1.0f, 7.0f, 20.0f), "concrete_dark"),
            Box("EastPumpNorthWall", new(26.0f, 3.5f, -45.0f), new(26.0f, 7.0f, 1.0f), "concrete"),
            Box("EastPumpSouthLeft", new(20.0f, 3.5f, -3.0f), new(5.0f, 7.0f, 1.0f), "concrete"),
            Box("EastPumpSouthRight", new(31.0f, 3.5f, -3.0f), new(7.0f, 7.0f, 1.0f), "concrete"),
            Box("EastPumpCanopy", new(31.0f, 7.0f, -20.0f), new(18.0f, 0.4f, 26.0f), "steel"),
            Box("EastPumpMachine", new(36.5f, 1.35f, -28.0f), new(4.5f, 2.7f, 6.5f), "steel"),
            Box("EastPumpPillarNorth", new(20.0f, 3.5f, -37.0f), new(3.0f, 7.0f, 3.0f), "concrete_dark"),
            Box("EastPumpPillarSouth", new(25.0f, 3.5f, -13.0f), new(3.0f, 7.0f, 3.0f), "concrete_dark"),

            Box("DefenderQuayGateLeft", new(-11.0f, 1.65f, -47.0f), new(14.0f, 3.3f, 1.0f), "concrete"),
            Box("DefenderQuayGateRight", new(11.0f, 1.65f, -47.0f), new(14.0f, 3.3f, 1.0f), "concrete")
        };
    }

    private IReadOnlyList<DemolitionArenaBox> BuildHarborLocksDetailBoxes()
    {
        var boxes = new List<DemolitionArenaBox>
        {
            Box("AttackQuay", new(0, 0.035f, 56), new(18, 0.07f, 9), "spawn_floor"),
            Box("DefenderQuay", new(0, 0.04f, -56), new(18, 0.08f, 7.5f), "spawn_floor"),
            Box("WestLockWater", new(-20.5f, -0.07f, 1.0f), new(7.0f, 0.08f, 96.0f), "water"),
            Box("EastLockWater", new(20.5f, -0.07f, -1.0f), new(7.0f, 0.08f, 96.0f), "water"),
            Box("CentralQuay", new(0, 0.035f, 3.0f), new(13.0f, 0.07f, 90.0f), "mid_floor"),
            Box("WestControlDeck", new(-33, 0.045f, 21), new(13, 0.09f, 40), "harbor_floor"),
            Box("EastPumpDeck", new(30, 0.045f, -22), new(24, 0.09f, 40), "harbor_floor"),
            Box("LockGateStripeNorth", new(0, 0.09f, 22.0f), new(12.0f, 0.04f, 0.22f), "warning"),
            Box("LockGateStripeSouth", new(0, 0.09f, -22.0f), new(12.0f, 0.04f, 0.22f), "cyan"),
            Box("EastPumpWindowBand", new(39.54f, 4.4f, -20), new(0.05f, 1.4f, 15), "window"),
            Box("EastPumpRoofStripe", new(31, 7.23f, -22), new(15, 0.07f, 2.0f), "warning"),
            Box("HarborGantryBeam", new(0, 5.25f, 22.0f), new(28.8f, 0.5f, 0.8f), "steel"),
            Box("HarborGantryStripe", new(0, 5.54f, 22.0f), new(27.0f, 0.08f, 0.9f), "cyan")
        };
        for (var index = 0; index < 7; index++)
        {
            boxes.Add(Box($"WestQuayMarker_{index:00}", new(-15.8f, 0.05f, 35 - index * 11), new(0.16f, 0.08f, 4.8f), "warning"));
            boxes.Add(Box($"EastQuayMarker_{index:00}", new(15.8f, 0.05f, 30 - index * 11), new(0.16f, 0.08f, 4.8f), "cyan"));
        }
        return boxes;
    }

    private IReadOnlyList<DemolitionArenaProp> BuildHarborLocksProps()
    {
        return new[]
        {
            HarborBuilding("HarborWarehouseWest", "building-a.glb", new(-34.0f, 0.02f, -40.0f), 0.0f, 4.4f, new(2.08f, 1.47f, 1.24f)),
            HarborBuilding("HarborWarehouseEast", "building-b.glb", new(34.0f, 0.02f, 38.0f), Mathf.Pi, 4.4f, new(2.08f, 1.47f, 1.26f)),
            HarborBuilding("WestPumpHouse", "building-c.glb", new(-33.0f, 0.02f, -16.0f), 0.0f, 4.25f, new(1.88f, 1.25f, 2.11f)),
            HarborBuilding("EastPumpHouse", "building-l.glb", new(33.0f, 0.02f, 16.0f), Mathf.Pi, 4.25f, new(2.08f, 1.92f, 1.87f)),
            HarborBuilding("WestLockOffice", "building-h.glb", new(-23.0f, 0.02f, 39.0f), Mathf.Pi * 0.5f, 4.0f, new(1.32f, 0.73f, 1.31f)),
            HarborBuilding("EastLockOffice", "building-t.glb", new(23.0f, 0.02f, -39.0f), -Mathf.Pi * 0.5f, 4.0f, new(1.72f, 1.01f, 1.39f)),
            HarborBuilding("WestControlBlock", "building-f.glb", new(-35.0f, 0.02f, 4.0f), Mathf.Pi * 0.5f, 4.0f, new(1.79f, 1.93f, 1.28f)),
            HarborBuilding("EastControlBlock", "building-r.glb", new(34.5f, 0.02f, -39.0f), -Mathf.Pi * 0.5f, 3.8f, new(2.48f, 1.39f, 1.27f)),
            HarborBuilding("NorthServiceHall", "building-q.glb", new(-28.5f, 0.02f, 48.5f), Mathf.Pi, 3.4f, new(2.14f, 0.88f, 1.77f)),
            HarborBuilding("SouthServiceHall", "building-e.glb", new(29.5f, 0.02f, -49.0f), 0.0f, 3.5f, new(1.68f, 1.65f, 1.29f)),
            HarborBuilding("WestQuayWorkshop", "building-g.glb", new(-28.0f, 0.02f, -5.0f), Mathf.Pi, 3.7f, new(1.68f, 1.28f, 1.28f)),
            HarborBuilding("EastQuayWorkshop", "building-n.glb", new(29.5f, 0.02f, 5.0f), 0.0f, 3.7f, new(0.98f, 1.90f, 1.42f)),
            HarborBuilding("WestStack", "chimney-large.glb", new(-37.0f, 0.02f, -30.0f), 0.0f, 4.6f, new(1.0f, 1.70f, 1.0f)),
            HarborBuilding("EastStack", "chimney-large.glb", new(37.0f, 0.02f, 29.0f), 0.0f, 4.6f, new(1.0f, 1.70f, 1.0f)),
            HarborBuilding("WestFuelTank", "detail-tank.glb", new(-29.0f, 0.02f, 35.5f), Mathf.Pi * 0.5f, 4.2f, new(0.85f, 0.42f, 0.52f)),
            HarborBuilding("EastFuelTank", "detail-tank.glb", new(29.0f, 0.02f, -34.0f), -Mathf.Pi * 0.5f, 4.2f, new(0.85f, 0.42f, 0.52f)),
            HarborVisual("MidWestPumpVisual", "building-o.glb", new(-6.8f, 0.02f, 17.5f), 0.0f, 5.0f),
            HarborVisual("MidEastPumpVisual", "building-i.glb", new(6.8f, 0.02f, 12.0f), Mathf.Pi, 3.8f),
            HarborVisual("AttackWestKioskVisual", "building-j.glb", new(-4.8f, 0.02f, 31.0f), Mathf.Pi * 0.5f, 2.5f),
            HarborVisual("AttackEastKioskVisual", "building-j.glb", new(4.6f, 0.02f, 27.0f), -Mathf.Pi * 0.5f, 2.5f),
            HarborVisual("DefenderWestKioskVisual", "building-h.glb", new(-7.0f, 0.02f, -31.0f), Mathf.Pi, 3.0f),
            HarborVisual("DefenderEastKioskVisual", "building-h.glb", new(7.0f, 0.02f, -36.0f), 0.0f, 3.0f),
            HarborVisual("HarborOfficeVisual", "building-m.glb", new(23.0f, 0.02f, 22.0f), Mathf.Pi, 3.5f),
            HarborVisual("ServiceBayVisual", "building-q.glb", new(-22.0f, 0.02f, -26.0f), 0.0f, 3.3f),
            HarborVisual("CentralPumpVisual", "building-c.glb", new(0.0f, 0.02f, -13.5f), Mathf.Pi * 0.5f, 3.7f)
        };
    }

    private DemolitionArenaProp HarborBuilding(
        string name,
        string file,
        Vector3 localPosition,
        float yaw,
        float scale,
        Vector3 collisionSize)
    {
        return new DemolitionArenaProp(
            name,
            $"{KenneyIndustrialRoot}/{file}",
            World(localPosition),
            yaw,
            scale,
            collisionSize,
            new Vector3(0, collisionSize.Y * 0.5f, 0));
    }

    private DemolitionArenaProp HarborVisual(
        string name,
        string file,
        Vector3 localPosition,
        float yaw,
        float scale)
    {
        return new DemolitionArenaProp(
            name,
            $"{KenneyIndustrialRoot}/{file}",
            World(localPosition),
            yaw,
            scale,
            new Vector3(0.02f, 0.02f, 0.02f),
            new Vector3(0, 0.01f, 0));
    }

    private Vector3 HarborLocksStrategyTarget(string key) => key switch
    {
        "attack_entry_a" => World(new Vector3(-24.0f, 0.2f, 14.0f)),
        "attack_entry_b" => World(new Vector3(23.0f, 0.2f, -7.0f)),
        "attack_support_a" => World(new Vector3(-23.0f, 0.2f, 17.0f)),
        "attack_support_b" => World(new Vector3(24.0f, 0.2f, -8.0f)),
        "attack_mid_recon" => World(new Vector3(0.0f, 0.2f, 3.0f)),
        "defense_anchor_a" => World(new Vector3(-30.0f, 0.2f, 12.0f)),
        "defense_anchor_b" => World(new Vector3(31.0f, 0.2f, -33.0f)),
        "defense_mid" => World(new Vector3(0.0f, 0.2f, -8.0f)),
        "defense_rotate_a" => World(new Vector3(-12.0f, 0.2f, -16.0f)),
        "defense_rotate_b" => World(new Vector3(12.0f, 0.2f, -16.0f)),
        "retake_entry_a" => World(new Vector3(-24.0f, 0.2f, 8.0f)),
        "retake_entry_b" => World(new Vector3(24.0f, 0.2f, 0.0f)),
        "retake_cover_a" => World(new Vector3(-30.0f, 0.2f, 28.0f)),
        "retake_cover_b" => World(new Vector3(36.0f, 0.2f, -24.0f)),
        "retake_flank_a" => World(new Vector3(-24.0f, 0.2f, 14.0f)),
        "retake_flank_b" => World(new Vector3(26.0f, 0.2f, -16.0f)),
        "postplant_guard_a" => World(new Vector3(-30.0f, 0.2f, 18.0f)),
        "postplant_guard_b" => World(new Vector3(30.0f, 0.2f, -15.0f)),
        "postplant_crossfire_a" => World(new Vector3(-29.0f, 0.2f, 27.0f)),
        "postplant_crossfire_b" => World(new Vector3(30.0f, 0.2f, -27.0f)),
        "postplant_lurk_a" => World(new Vector3(-20.0f, 0.2f, 4.0f)),
        "postplant_lurk_b" => World(new Vector3(20.0f, 0.2f, 2.0f)),
        "site_a" => SitePositions[0],
        "site_b" => SitePositions[1],
        _ => Midpoint
    };
}
