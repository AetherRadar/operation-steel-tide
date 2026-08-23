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
        AddSign(root, "WestControlSign", layout.Origin + new Vector3(-41.0f, 5.2f, -26.4f), "A  //  WEST CONTROL", 0, new Color(1.0f, 0.58f, 0.16f));
        AddSign(root, "EastPumpSign", layout.Origin + new Vector3(41.0f, 5.4f, 26.4f), "B  //  EAST PUMP", Mathf.Pi, new Color(0.36f, 0.86f, 1.0f));
        AddSign(root, "LockGateSign", layout.Origin + new Vector3(0.0f, 4.5f, 4.42f), "LOCK 02  //  CONTROL BRIDGE", Mathf.Pi, new Color(0.72f, 0.92f, 0.94f));
        // Per-building purpose signs: large, billboard, readable at 40m+
        AddSign(root, "WestTurbineSign", layout.Origin + new Vector3(-43.0f, 4.8f, 0.0f), "TURBINE HALL", 0, new Color(0.9f, 0.72f, 0.2f));
        AddSign(root, "NorthFreightSign", layout.Origin + new Vector3(-16.0f, 4.9f, -24.0f), "NORTH FREIGHT", 0, new Color(0.82f, 0.82f, 0.84f));
        AddSign(root, "NorthAdminSign", layout.Origin + new Vector3(8.0f, 4.4f, -27.0f), "ADMIN", Mathf.Pi, new Color(0.55f, 0.82f, 1.0f));
        AddSign(root, "EastBoilerSign", layout.Origin + new Vector3(43.0f, 4.7f, -8.0f), "BOILER HOUSE", Mathf.Pi, new Color(1.0f, 0.42f, 0.2f));
        AddSign(root, "SouthFreightSign", layout.Origin + new Vector3(15.0f, 4.7f, 27.0f), "SOUTH FREIGHT", Mathf.Pi, new Color(0.88f, 0.76f, 0.42f));
        AddSign(root, "SouthAnnexSign", layout.Origin + new Vector3(-8.0f, 4.6f, 27.0f), "CONTROL ANNEX", 0, new Color(0.62f, 0.88f, 0.62f));

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
        // Yard markings to show building extent and mitigate tiny-house silhouette
        AddVisualBox(root, new DemolitionArenaBox("WestControlYardStripe", layout.Origin + new Vector3(-43.0f, 0.11f, -33.0f), new Vector3(14.0f, 0.04f, 0.22f), "warning"), materials["warning"]);
        AddVisualBox(root, new DemolitionArenaBox("EastPumpYardStripe", layout.Origin + new Vector3(42.0f, 0.11f, 33.0f), new Vector3(14.0f, 0.04f, 0.22f), "cyan"), materials["cyan"]);
        AddVisualBox(root, new DemolitionArenaBox("SouthAnnexYardStripe", layout.Origin + new Vector3(-8.0f, 0.11f, 33.0f), new Vector3(12.0f, 0.04f, 0.22f), "warning"), materials["warning"]);
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
        // Industrial dressing around mid cover to differentiate function and add readable volume
        AddStaticCylinder(root, "MidNorthPumpPipe", layout.Origin + new Vector3(-3.0f, 1.8f, -12.0f), 0.18f, 0.18f, 6.0f, materials["rust"], new Vector3(0, 0, Mathf.Pi * 0.5f));
        AddStaticCylinder(root, "MidSouthPumpPipe", layout.Origin + new Vector3(3.0f, 1.8f, 12.0f), 0.18f, 0.18f, 6.0f, materials["steel"], new Vector3(0, 0, Mathf.Pi * 0.5f));
        // Small-building yard props: pallets/crates/fences to show occupancy not toy
        AddVisualBox(root, new DemolitionArenaBox("DispatchPalletStack", layout.Origin + new Vector3(-29.0f, 0.55f, -28.0f), new Vector3(2.2f, 1.1f, 1.5f), "steel"), materials["steel_dark"]);
        AddVisualBox(root, new DemolitionArenaBox("CustomsFence", layout.Origin + new Vector3(-32.0f, 1.0f, 8.0f), new Vector3(8.0f, 0.12f, 0.12f), "warning"), materials["warning"]);
        AddVisualBox(root, new DemolitionArenaBox("QuayContainer", layout.Origin + new Vector3(51.0f, 0.9f, 8.0f), new Vector3(6.0f, 1.8f, 2.4f), "steel"), materials["steel_dark"]);
        AddStaticCylinder(root, "TurbineExhaust", layout.Origin + new Vector3(-43.0f, 4.2f, -4.0f), 0.45f, 0.55f, 4.5f, materials["rust"]);
        AddStaticCylinder(root, "BoilerExhaust", layout.Origin + new Vector3(43.0f, 4.6f, -14.0f), 0.5f, 0.6f, 5.0f, materials["steel"]);
    }

    private void BuildHarborLocksRouteGuidance(Node3D root, DemolitionArenaLayout layout)
    {
        AddFloorLabel(root, "AttackFloorLabel", layout.Origin + new Vector3(-33.0f, 0.09f, 35.0f), "HARBOR ENTRY", new Color(0.56f, 0.92f, 0.86f), 88);
        AddFloorLabel(root, "RouteALabel", layout.Origin + new Vector3(-43.0f, 0.09f, 24.0f), "A CONTROL  ^", new Color(1.0f, 0.58f, 0.18f), 78);
        AddFloorLabel(root, "RouteMidLabel", layout.Origin + new Vector3(-18.0f, 0.09f, 18.0f), "LOCK BRIDGE", new Color(0.9f, 0.88f, 0.68f), 72);
        AddFloorLabel(root, "RouteBLabel", layout.Origin + new Vector3(17.0f, 0.09f, 24.0f), "B PUMPS  >", new Color(0.28f, 0.82f, 0.96f), 78);
        AddFloorLabel(root, "DefendFloorLabel", layout.Origin + new Vector3(32.0f, 0.09f, -35.0f), "MAINTENANCE YARD", new Color(0.46f, 0.94f, 0.68f), 84);
    }
}
