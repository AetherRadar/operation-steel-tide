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
        AddSign(root, "ArenaTitle", layout.Origin + new Vector3(0, 5.2f, -54.6f), "HARBOR LOCKS  //  HL-02", 0, new Color(0.34f, 0.88f, 1.0f));
        AddSign(root, "WestControlSign", layout.Origin + new Vector3(-27.0f, 4.6f, 10.4f), "A  //  WEST CONTROL", Mathf.Pi, new Color(1.0f, 0.58f, 0.16f));
        AddSign(root, "EastPumpSign", layout.Origin + new Vector3(25.0f, 4.8f, -3.4f), "B  //  EAST PUMP", Mathf.Pi, new Color(0.36f, 0.86f, 1.0f));
        AddSign(root, "LockGateSign", layout.Origin + new Vector3(0.0f, 4.5f, 21.54f), "LOCK 02  //  GATE CONTROL", 0, new Color(0.72f, 0.92f, 0.94f));

        for (var index = 0; index < 5; index++)
        {
            var x = -12.0f + index * 6.0f;
            AddVisualBox(
                root,
                new DemolitionArenaBox(
                    $"HarborGantryRib_{index + 1:00}",
                    layout.Origin + new Vector3(x, 6.4f, 22.0f),
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
        for (var index = 0; index < 4; index++)
        {
            AddStaticCylinder(
                root,
                $"HarborBollardWest_{index + 1:00}",
                layout.Origin + new Vector3(-15.4f, 0.65f, 31.0f - index * 17.0f),
                0.28f,
                0.38f,
                1.3f,
                materials["steel_dark"]);
            AddStaticCylinder(
                root,
                $"HarborBollardEast_{index + 1:00}",
                layout.Origin + new Vector3(15.4f, 0.65f, 26.0f - index * 17.0f),
                0.28f,
                0.38f,
                1.3f,
                materials["steel_dark"]);
        }
        AddSign(root, "WestQuaySign", layout.Origin + new Vector3(-22.0f, 2.7f, -21.92f), "WEST QUAY  //  SERVICE", Mathf.Pi, new Color(1.0f, 0.68f, 0.22f));
        AddSign(root, "EastQuaySign", layout.Origin + new Vector3(23.0f, 4.2f, 19.18f), "EAST QUAY  //  PUMPS", 0, new Color(0.32f, 0.88f, 1.0f));
    }

    private void BuildHarborLocksRouteGuidance(Node3D root, DemolitionArenaLayout layout)
    {
        AddFloorLabel(root, "AttackFloorLabel", layout.Origin + new Vector3(0, 0.09f, 43.0f), "ATTACK QUAY", new Color(0.56f, 0.92f, 0.86f), 68);
        AddFloorLabel(root, "RouteALabel", layout.Origin + new Vector3(-6.0f, 0.09f, 36.0f), "<  A CONTROL", new Color(1.0f, 0.58f, 0.18f), 58);
        AddFloorLabel(root, "RouteMidLabel", layout.Origin + new Vector3(0, 0.09f, 34.0f), "LOCK", new Color(0.9f, 0.88f, 0.68f), 54);
        AddFloorLabel(root, "RouteBLabel", layout.Origin + new Vector3(6.0f, 0.09f, 36.0f), "B PUMPS  >", new Color(0.28f, 0.82f, 0.96f), 58);
        AddFloorLabel(root, "DefendFloorLabel", layout.Origin + new Vector3(0, 0.09f, -43.0f), "DEFEND QUAY", new Color(0.46f, 0.94f, 0.68f), 64);
    }
}
