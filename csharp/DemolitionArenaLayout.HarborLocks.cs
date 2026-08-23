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
            Box("ArenaFloor", new(0, -0.48f, 0), new(116, 1.0f, 84), "ground"),
            Box("NorthPerimeter", new(0, 2.5f, -41.5f), new(116, 5.0f, 1.0f), "concrete_dark"),
            Box("SouthPerimeter", new(0, 2.5f, 41.5f), new(116, 5.0f, 1.0f), "concrete_dark"),
            Box("WestPerimeter", new(-57.5f, 2.5f, 0), new(1.0f, 5.0f, 84), "concrete_dark"),
            Box("EastPerimeter", new(57.5f, 2.5f, 0), new(1.0f, 5.0f, 84), "concrete_dark"),

            // Visible sightline annexes replace invisible air walls: they extend the
            // authored buildings so spawn-to-site vision is blocked by readable geometry.
            Box("SightBlockWestTurbine", new(-42.3f, 2.5f, -3.0f), new(13.7f, 5.0f, 15.3f), "concrete_dark", visible: true),
            Box("SightBlockSouthAnnex", new(-8.0f, 2.5f, 33.0f), new(17.5f, 5.0f, 9.0f), "concrete_dark", visible: true),

            Box("WestLockGate", new(-31.0f, 1.7f, 0.0f), new(16.0f, 3.4f, 0.8f), "steel_dark"),
            Box("EastLockGate", new(31.0f, 1.7f, 0.0f), new(16.0f, 3.4f, 0.8f), "steel_dark"),
            Box("WestBridgeAbutmentNorth", new(-20.0f, 1.25f, -4.2f), new(4.0f, 2.5f, 1.0f), "concrete"),
            Box("WestBridgeAbutmentSouth", new(-20.0f, 1.25f, 4.2f), new(4.0f, 2.5f, 1.0f), "concrete"),
            Box("EastBridgeAbutmentNorth", new(20.0f, 1.25f, -4.2f), new(4.0f, 2.5f, 1.0f), "concrete"),
            Box("EastBridgeAbutmentSouth", new(20.0f, 1.25f, 4.2f), new(4.0f, 2.5f, 1.0f), "concrete")
        };
    }

    private IReadOnlyList<DemolitionArenaBox> BuildHarborLocksDetailBoxes()
    {
        var boxes = new List<DemolitionArenaBox>
        {
            Box("AttackHarborEntry", new(-33.0f, 0.035f, 35.0f), new(20.0f, 0.07f, 11.0f), "spawn_floor"),
            Box("DefenderMaintenanceEntry", new(32.0f, 0.04f, -35.0f), new(20.0f, 0.08f, 11.0f), "spawn_floor"),
            Box("NorthLockWater", new(0.0f, 0.04f, -5.4f), new(104.0f, 0.08f, 5.8f), "water"),
            Box("SouthLockWater", new(0.0f, 0.04f, 5.4f), new(104.0f, 0.08f, 5.8f), "water"),
            Box("CentralControlBridge", new(0.0f, 0.085f, 0.0f), new(22.0f, 0.09f, 9.0f), "mid_floor"),
            Box("WestServiceBridge", new(-43.0f, 0.085f, 0.0f), new(12.0f, 0.09f, 9.0f), "harbor_floor"),
            Box("EastServiceBridge", new(43.0f, 0.085f, 0.0f), new(12.0f, 0.09f, 9.0f), "harbor_floor"),
            Box("WestControlYard", new(-41.0f, 0.09f, -20.0f), new(18.0f, 0.1f, 16.0f), "harbor_floor"),
            Box("EastPumpYard", new(41.0f, 0.09f, 20.0f), new(18.0f, 0.1f, 16.0f), "harbor_floor"),
            Box("NorthFreightYard", new(-16.0f, 0.09f, -27.0f), new(16.0f, 0.1f, 12.0f), "harbor_floor"),
            Box("SouthFreightYard", new(15.0f, 0.09f, 28.0f), new(16.0f, 0.1f, 12.0f), "harbor_floor"),
            Box("NorthDispatchYard", new(-27.5f, 0.09f, -28.0f), new(9.0f, 0.1f, 9.0f), "harbor_floor"),
            Box("NorthSecurityYard", new(-3.0f, 0.09f, -27.5f), new(8.0f, 0.1f, 8.0f), "harbor_floor"),
            Box("SouthSecurityYard", new(30.0f, 0.09f, 29.0f), new(8.0f, 0.1f, 8.0f), "harbor_floor"),
            Box("CustomsWorkshopYard", new(-32.0f, 0.09f, 9.0f), new(10.0f, 0.1f, 10.0f), "harbor_floor"),
            Box("QuayWorkshopYard", new(51.0f, 0.09f, 8.0f), new(9.0f, 0.1f, 16.0f), "harbor_floor"),
            Box("CentralBridgeNorthRail", new(0.0f, 0.22f, -4.28f), new(22.0f, 0.22f, 0.16f), "warning"),
            Box("CentralBridgeSouthRail", new(0.0f, 0.22f, 4.28f), new(22.0f, 0.22f, 0.16f), "cyan"),
            Box("WestGateStripe", new(-31.0f, 3.5f, 0.44f), new(15.0f, 0.08f, 0.12f), "warning"),
            Box("EastGateStripe", new(31.0f, 3.5f, -0.44f), new(15.0f, 0.08f, 0.12f), "cyan")
        };
        for (var index = 0; index < 8; index++)
        {
            boxes.Add(Box($"NorthQuayMarker_{index:00}", new(-48.0f + index * 13.5f, 0.1f, -9.0f), new(5.0f, 0.04f, 0.18f), "warning"));
            boxes.Add(Box($"SouthQuayMarker_{index:00}", new(-48.0f + index * 13.5f, 0.1f, 9.0f), new(5.0f, 0.04f, 0.18f), "cyan"));
        }
        return boxes;
    }

    private IReadOnlyList<DemolitionArenaProp> BuildHarborLocksProps()
    {
        return new[]
        {
            HarborBuilding("WestControlHall", "building-a.glb", new(-43.0f, 0.02f, -33.0f), 0.0f, 8.5f, new(2.08f, 1.47f, 1.24f)),
            HarborBuilding("WestTurbineHall", "building-c.glb", new(-43.0f, 0.02f, -4.0f), 0.0f, 8.0f, new(1.88f, 1.25f, 2.11f), new(0.096f, 0.625f, 0.143f)),
            HarborBuilding("NorthFreightHall", "building-q.glb", new(-16.0f, 0.02f, -32.0f), 0.0f, 7.8f, new(2.14f, 0.88f, 1.77f), new(-0.228f, 0.44f, 0.011f)),
            HarborBuilding("NorthAdministration", "building-h.glb", new(8.0f, 0.02f, -33.0f), Mathf.Pi, 7.5f, new(1.32f, 0.73f, 1.31f), new(-0.581f, 0.365f, 0.279f)),
            HarborBuilding("EastBoilerHall", "building-l.glb", new(43.0f, 0.02f, -14.0f), Mathf.Pi, 8.0f, new(2.08f, 1.92f, 1.87f), new(-0.422f, 0.96f, 0.224f)),
            HarborBuilding("EastPumpHall", "building-b.glb", new(42.0f, 0.02f, 33.0f), Mathf.Pi, 8.5f, new(2.08f, 1.47f, 1.26f)),
            HarborBuilding("SouthFreightHall", "building-e.glb", new(15.0f, 0.02f, 33.0f), 0.0f, 7.8f, new(1.68f, 1.65f, 1.29f), new(0.0f, 0.825f, 0.249f)),
            HarborBuilding("SouthControlAnnex", "building-r.glb", new(-8.0f, 0.02f, 33.0f), 0.0f, 7.8f, new(2.48f, 1.39f, 1.27f)),

            HarborBuilding("MidCoverWestOffice", "building-h.glb", new(-23.0f, 0.02f, -14.0f), 0.0f, 7.0f, new(1.32f, 0.73f, 1.31f), new(-0.581f, 0.365f, 0.279f)),
            HarborBuilding("MidCoverEastOffice", "building-t.glb", new(23.0f, 0.02f, 18.0f), Mathf.Pi * 0.5f, 7.0f, new(1.72f, 1.01f, 1.39f)),
            HarborBuilding("MidCoverNorthPump", "building-f.glb", new(-3.0f, 0.02f, -16.0f), 0.0f, 7.0f, new(1.79f, 1.93f, 1.28f), new(-0.453f, 0.965f, 0.254f)),
            HarborBuilding("MidCoverSouthPump", "building-n.glb", new(3.0f, 0.02f, 16.0f), Mathf.Pi, 7.0f, new(0.98f, 1.90f, 1.42f), new(-0.452f, 0.95f, 0.56f)),
            HarborBuilding("MidCoverTankNorthWest", "detail-tank.glb", new(-14.0f, 0.02f, -5.0f), 0.0f, 6.2f, new(0.85f, 0.42f, 0.52f)),
            HarborBuilding("MidCoverTankNorthEast", "detail-tank.glb", new(13.0f, 0.02f, -6.0f), Mathf.Pi * 0.5f, 6.2f, new(0.85f, 0.42f, 0.52f)),
            HarborBuilding("MidCoverTankSouthWest", "detail-tank.glb", new(-12.0f, 0.02f, 7.0f), Mathf.Pi * 0.5f, 4.5f, new(0.85f, 0.42f, 0.52f)),
            HarborBuilding("MidCoverTankSouthEast", "detail-tank.glb", new(10.0f, 0.02f, 0.0f), 0.0f, 6.2f, new(0.85f, 0.42f, 0.52f)),

            HarborBuilding("WestControlStack", "chimney-large.glb", new(-53.0f, 0.02f, 33.0f), 0.0f, 7.8f, new(1.0f, 1.70f, 1.0f)),
            HarborBuilding("EastPumpStack", "chimney-large.glb", new(53.0f, 0.02f, -33.0f), 0.0f, 7.8f, new(1.0f, 1.70f, 1.0f)),

            HarborBuilding("NorthDispatchOffice", "building-j.glb", new(-27.5f, 0.02f, -33.0f), 0.0f, 6.0f, new(1.04f, 0.86f, 1.32f), new(-0.435f, 0.431f, 0.274f)),
            HarborBuilding("NorthSecurityOffice", "building-j.glb", new(-3.0f, 0.02f, -32.5f), Mathf.Pi, 6.8f, new(1.04f, 0.86f, 1.32f), new(-0.435f, 0.431f, 0.274f)),
            HarborBuilding("SouthSecurityOffice", "building-j.glb", new(30.0f, 0.02f, 34.0f), 0.0f, 6.8f, new(1.04f, 0.86f, 1.32f), new(-0.435f, 0.431f, 0.274f)),
            HarborBuilding("WestCustomsWorkshop", "building-g.glb", new(-32.0f, 0.02f, 13.0f), Mathf.Pi, 7.0f, new(1.68f, 1.28f, 1.28f)),
            HarborBuilding("WestServiceOffice", "building-j.glb", new(-18.0f, 0.02f, 9.5f), Mathf.Pi * 0.5f, 4.5f, new(1.04f, 0.86f, 1.32f), new(-0.435f, 0.431f, 0.274f)),
            HarborBuilding("EastQuayWorkshop", "building-g.glb", new(51.0f, 0.02f, 5.0f), 0.0f, 7.0f, new(1.68f, 1.28f, 1.28f)),
            HarborBuilding("EastServiceOffice", "building-j.glb", new(51.0f, 0.02f, 17.5f), Mathf.Pi, 6.8f, new(1.04f, 0.86f, 1.32f), new(-0.435f, 0.431f, 0.274f))
        };
    }

    private IReadOnlyList<Vector3> BuildHarborLocksAttackToAPath() => WorldPoints(
        new(-32.0f, 0.2f, 35.0f), new(-40.0f, 0.2f, 27.0f),
        new(-51.0f, 0.2f, 18.0f), new(-52.0f, 0.2f, 6.0f),
        new(-53.0f, 0.2f, -8.0f), new(-50.0f, 0.2f, -20.0f),
        new(-41.0f, 0.2f, -20.0f));

    private IReadOnlyList<Vector3> BuildHarborLocksAttackToBPath() => WorldPoints(
        new(-32.0f, 0.2f, 35.0f), new(-25.0f, 0.2f, 28.0f),
        new(-18.0f, 0.2f, 22.0f), new(-5.0f, 0.2f, 22.0f),
        new(8.0f, 0.2f, 24.0f), new(22.0f, 0.2f, 26.0f),
        new(32.0f, 0.2f, 24.0f), new(41.0f, 0.2f, 20.0f));

    private IReadOnlyList<Vector3> BuildHarborLocksAttackMidPath() => WorldPoints(
        new(-32.0f, 0.2f, 35.0f), new(-26.0f, 0.2f, 27.0f),
        new(-18.0f, 0.2f, 20.0f), new(-10.0f, 0.2f, 12.0f),
        new(-4.0f, 0.2f, 4.0f), new(0.0f, 0.2f, 0.0f));

    private IReadOnlyList<Vector3> BuildHarborLocksDefenderToAPath() => WorldPoints(
        new(32.0f, 0.2f, -35.0f), new(24.0f, 0.2f, -25.0f),
        new(14.0f, 0.2f, -22.0f), new(2.0f, 0.2f, -23.0f),
        new(-10.0f, 0.2f, -24.0f), new(-22.0f, 0.2f, -23.0f),
        new(-32.0f, 0.2f, -21.0f), new(-41.0f, 0.2f, -20.0f));

    private IReadOnlyList<Vector3> BuildHarborLocksDefenderToBPath() => WorldPoints(
        new(32.0f, 0.2f, -35.0f), new(24.0f, 0.2f, -26.0f),
        new(23.0f, 0.2f, -13.0f), new(17.0f, 0.2f, -10.0f),
        new(16.5f, 0.2f, -1.0f), new(16.5f, 0.2f, 6.5f),
        new(30.0f, 0.2f, 9.0f),
        new(35.0f, 0.2f, 14.0f), new(41.0f, 0.2f, 20.0f));

    private IReadOnlyList<Vector3> BuildHarborLocksSiteRotationPath() => WorldPoints(
        new(-41.0f, 0.2f, -20.0f), new(-32.0f, 0.2f, -21.0f),
        new(-22.0f, 0.2f, -22.0f), new(-12.0f, 0.2f, -22.0f),
        new(-5.0f, 0.2f, -24.0f), new(4.0f, 0.2f, -23.0f),
        new(12.0f, 0.2f, -18.0f), new(19.0f, 0.2f, -9.0f),
        new(28.0f, 0.2f, -2.0f), new(41.0f, 0.2f, -2.0f),
        new(41.0f, 0.2f, 8.0f),
        new(37.0f, 0.2f, 15.0f), new(41.0f, 0.2f, 20.0f));

    private DemolitionArenaProp HarborBuilding(
        string name,
        string file,
        Vector3 localPosition,
        float yaw,
        float scale,
        Vector3 collisionSize,
        Vector3? collisionOffset = null)
    {
        return new DemolitionArenaProp(
            name,
            $"{KenneyIndustrialRoot}/{file}",
            World(localPosition),
            yaw,
            scale,
            collisionSize,
            collisionOffset ?? new Vector3(0, collisionSize.Y * 0.5f, 0));
    }

    private Vector3 HarborLocksStrategyTarget(string key) => key switch
    {
        "attack_entry_a" => World(new Vector3(-48.0f, 0.2f, -14.0f)),
        "attack_entry_b" => World(new Vector3(33.0f, 0.2f, 23.0f)),
        "attack_support_a" => World(new Vector3(-46.0f, 0.2f, -17.0f)),
        "attack_support_b" => World(new Vector3(31.0f, 0.2f, 26.0f)),
        "attack_mid_recon" => World(new Vector3(-4.0f, 0.2f, 3.0f)),
        "defense_anchor_a" => World(new Vector3(-35.0f, 0.2f, -24.0f)),
        "defense_anchor_b" => World(new Vector3(35.0f, 0.2f, 24.0f)),
        "defense_mid" => World(new Vector3(5.0f, 0.2f, -2.0f)),
        "defense_rotate_a" => World(new Vector3(-13.0f, 0.2f, -23.0f)),
        "defense_rotate_b" => World(new Vector3(28.0f, 0.2f, -2.0f)),
        "retake_entry_a" => World(new Vector3(-32.0f, 0.2f, -21.0f)),
        "retake_entry_b" => World(new Vector3(32.0f, 0.2f, 18.0f)),
        "retake_cover_a" => World(new Vector3(-48.0f, 0.2f, -25.0f)),
        "retake_cover_b" => World(new Vector3(48.0f, 0.2f, 25.0f)),
        "retake_flank_a" => World(new Vector3(-50.0f, 0.2f, -13.0f)),
        "retake_flank_b" => World(new Vector3(50.0f, 0.2f, 14.0f)),
        "postplant_guard_a" => World(new Vector3(-39.0f, 0.2f, -19.0f)),
        "postplant_guard_b" => World(new Vector3(39.0f, 0.2f, 19.0f)),
        "postplant_crossfire_a" => World(new Vector3(-34.0f, 0.2f, -25.0f)),
        "postplant_crossfire_b" => World(new Vector3(34.0f, 0.2f, 25.0f)),
        "postplant_lurk_a" => World(new Vector3(-48.0f, 0.2f, -13.0f)),
        "postplant_lurk_b" => World(new Vector3(48.0f, 0.2f, 13.0f)),
        "site_a" => SitePositions[0],
        "site_b" => SitePositions[1],
        _ => Midpoint
    };
}
