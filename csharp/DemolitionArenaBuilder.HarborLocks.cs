using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public sealed partial class DemolitionArenaBuilder
{
    private void BuildHarborLocksLandmarks(
        Node3D root,
        DemolitionArenaLayout layout,
        IReadOnlyDictionary<string, StandardMaterial3D> materials)
    {
        AddSign(root, "ArenaTitle", layout.Origin + new Vector3(0, 5.2f, -41.0f), "HARBOR LOCKS  //  HL-02", 0, new Color(0.34f, 0.88f, 1.0f));
        AddSign(root, "WestControlSign", layout.Origin + new Vector3(-41.0f, 4.6f, -26.4f), "A  //  WEST CONTROL", 0, new Color(1.0f, 0.58f, 0.16f));
        AddSign(root, "EastPumpSign", layout.Origin + new Vector3(41.0f, 4.8f, 26.4f), "B  //  EAST PUMP", Mathf.Pi, new Color(0.36f, 0.86f, 1.0f));
        AddSign(root, "LockGateSign", layout.Origin + new Vector3(0.0f, 4.5f, 4.42f), "LOCK 02  //  CONTROL BRIDGE", Mathf.Pi, new Color(0.72f, 0.92f, 0.94f));

        for (var index = 0; index < 5; index++)
        {
            var x = -9.0f + index * 4.5f;
            AddVisualBox(
                root,
                new DemolitionArenaBox(
                    $"HarborGantryRib_{index + 1:00}",
                    layout.Origin + new Vector3(x, 6.4f, 0.0f),
                    new Vector3(0.24f, 2.2f, 0.24f),
                    "steel"),
                materials["steel"]);
        }
    }

    private void BuildHarborLocksCoverDetails(
        Node3D root,
        DemolitionArenaLayout layout,
        IReadOnlyDictionary<string, StandardMaterial3D> materials)
    {
        for (var index = 0; index < 5; index++)
        {
            AddStaticCylinder(
                root,
                $"HarborBollardNorth_{index + 1:00}",
                layout.Origin + new Vector3(-44.0f + index * 22.0f, 0.65f, -9.2f),
                0.28f,
                0.38f,
                1.3f,
                materials["steel_dark"]);
            AddStaticCylinder(
                root,
                $"HarborBollardSouth_{index + 1:00}",
                layout.Origin + new Vector3(-44.0f + index * 22.0f, 0.65f, 9.2f),
                0.28f,
                0.38f,
                1.3f,
                materials["steel_dark"]);
        }
        AddSign(root, "WestQuaySign", layout.Origin + new Vector3(-50.8f, 2.7f, 0.0f), "WEST SERVICE BRIDGE", Mathf.Pi * 0.5f, new Color(1.0f, 0.68f, 0.22f));
        AddSign(root, "EastQuaySign", layout.Origin + new Vector3(50.8f, 2.7f, 0.0f), "EAST SERVICE BRIDGE", -Mathf.Pi * 0.5f, new Color(0.32f, 0.88f, 1.0f));
    }

    private void BuildHarborLocksRouteGuidance(Node3D root, DemolitionArenaLayout layout)
    {
        AddFloorLabel(root, "AttackFloorLabel", layout.Origin + new Vector3(-33.0f, 0.09f, 35.0f), "HARBOR ENTRY", new Color(0.56f, 0.92f, 0.86f), 68);
        AddFloorLabel(root, "RouteALabel", layout.Origin + new Vector3(-43.0f, 0.09f, 24.0f), "A CONTROL  ^", new Color(1.0f, 0.58f, 0.18f), 58);
        AddFloorLabel(root, "RouteMidLabel", layout.Origin + new Vector3(-18.0f, 0.09f, 18.0f), "LOCK BRIDGE", new Color(0.9f, 0.88f, 0.68f), 54);
        AddFloorLabel(root, "RouteBLabel", layout.Origin + new Vector3(17.0f, 0.09f, 24.0f), "B PUMPS  >", new Color(0.28f, 0.82f, 0.96f), 58);
        AddFloorLabel(root, "DefendFloorLabel", layout.Origin + new Vector3(32.0f, 0.09f, -35.0f), "MAINTENANCE YARD", new Color(0.46f, 0.94f, 0.68f), 64);
    }
}
