using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private int _specialLandmarkCount;
    private int _specialLandmarkLootCount;
    private int _specialLandmarkVerticalRouteCount;

    public int SpecialLandmarkCount => _specialLandmarkCount;
    public int SpecialLandmarkLootCount => _specialLandmarkLootCount;
    public int SpecialLandmarkVerticalRouteCount => _specialLandmarkVerticalRouteCount;

    private void BuildSpecialLandmarks(
        Godot.Material concrete,
        Godot.Material steel,
        Godot.Material steelDark,
        Godot.Material rust,
        Godot.Material yellow,
        Godot.Material corrugated)
    {
        _specialLandmarkCount = 0;
        _specialLandmarkLootCount = 0;
        _specialLandmarkVerticalRouteCount = 0;

        var paving = GroundMaterial("special_landmark_paving", new Color(0.39f, 0.42f, 0.4f), 0.9f);
        var timber = Mat("special_landmark_timber", new Color(0.34f, 0.2f, 0.1f), 0.05f, 0.82f);
        var glass = Mat("special_landmark_glass", new Color(0.16f, 0.64f, 0.68f, 0.34f), 0.22f, 0.12f);
        glass.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        glass.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        var cyan = Mat("special_landmark_cyan", new Color(0.12f, 0.72f, 0.78f), 0.44f, 0.28f, new Color(0.03f, 0.34f, 0.38f));
        var orange = Mat("special_landmark_orange", new Color(0.92f, 0.38f, 0.08f), 0.34f, 0.4f, new Color(0.44f, 0.1f, 0.02f));
        var white = Mat("special_landmark_white", new Color(0.75f, 0.78f, 0.72f), 0.16f, 0.56f);

        BuildSalvageBazaar(new Vector3(-76.0f, 0.0f, 4.0f), paving, timber, steel, steelDark, yellow, orange);
        BuildTideglassConservatory(new Vector3(113.0f, 0.0f, 9.0f), paving, concrete, steel, steelDark, glass, cyan);
        BuildTidalObservatory(new Vector3(-114.0f, 0.0f, 43.0f), paving, concrete, steel, steelDark, rust, cyan);
        BuildDrydockRepairCradle(new Vector3(77.0f, 0.0f, -151.0f), paving, concrete, steel, steelDark, rust, yellow, corrugated, orange, white);
    }

    private Node3D CreateSpecialLandmarkRoot(string name, Vector3 position, string label, Color accent)
    {
        var root = new Node3D { Name = name, Position = position };
        root.AddToGroup("special_landmark");
        _levelRoot.AddChild(root);
        _specialLandmarkCount++;
        root.AddChild(new Label3D
        {
            Name = $"{name}_Label",
            Position = new Vector3(0, 6.5f, 0),
            Text = label,
            FontSize = 22,
            OutlineSize = 6,
            Modulate = accent,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            VisibilityRangeEnd = 72.0f
        });
        return root;
    }

    private void AddSpecialLoot(
        Node3D parent,
        string name,
        Vector3 localPosition,
        LootGrade grade,
        string english,
        string chinese)
    {
        var pickup = new GradedLootPickup
        {
            Name = name,
            Position = localPosition
        };
        pickup.Configure(CreateGradedLootItem(grade), english, chinese);
        pickup.AddToGroup("special_landmark_loot");
        parent.AddChild(pickup);
        _lootSources.Add(pickup);
        _lootWorldPoints.Add(pickup.GlobalPosition);
        _specialLandmarkLootCount++;
    }

    private void AddSpecialVerticalRouteMarker(Node3D parent, string name)
    {
        var marker = new Node3D { Name = name };
        marker.AddToGroup("special_landmark_vertical_route");
        parent.AddChild(marker);
        _specialLandmarkVerticalRouteCount++;
    }

    private void AddSpecialLabel(Node3D parent, string name, Vector3 position, string text, Color color)
    {
        parent.AddChild(new Label3D
        {
            Name = name,
            Position = position,
            Text = text,
            FontSize = 13,
            OutlineSize = 4,
            Modulate = color,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            VisibilityRangeEnd = 34.0f
        });
    }

    private void AddSpecialSteps(
        Node3D parent,
        string prefix,
        Vector3 start,
        Vector3 direction,
        float width,
        float run,
        float rise,
        int steps,
        Godot.Material material)
    {
        var horizontal = direction;
        horizontal.Y = 0.0f;
        horizontal = horizontal.Normalized();
        var yaw = Mathf.Atan2(horizontal.X, horizontal.Z);
        var stepRun = run / steps;
        for (var step = 0; step < steps; step++)
        {
            var top = start.Y + rise * (step + 1);
            var height = Mathf.Max(0.16f, top - start.Y);
            var center = start + horizontal * stepRun * (step + 0.5f);
            center.Y = start.Y + height * 0.5f;
            ExpansionBox(
                parent,
                $"{prefix}_Step_{step:00}",
                center,
                new Vector3(width, height, stepRun * 1.08f),
                material,
                new Vector3(0, yaw, 0));
        }
    }

    private void AddSpecialSlopedRail(
        Node3D parent,
        string name,
        Vector3 start,
        Vector3 end,
        float thickness,
        Godot.Material material)
    {
        var delta = end - start;
        var length = delta.Length();
        if (length < 0.1f)
        {
            return;
        }
        var center = (start + end) * 0.5f;
        var angle = Mathf.Atan2(delta.Y, new Vector2(delta.Z, delta.X).Length());
        var yaw = Mathf.Atan2(delta.X, delta.Z);
        var visual = MeshBox(
            parent,
            center,
            new Vector3(thickness, thickness, length),
            material,
            new Vector3(angle, yaw, 0));
        visual.Name = name;
        RegisterMapDetailVisual(visual);
    }

    private void AddSpecialCanopy(
        Node3D parent,
        string prefix,
        Vector3 center,
        Vector2 size,
        float height,
        Godot.Material roof,
        Godot.Material post)
    {
        var halfWidth = size.X * 0.5f;
        var roofLength = Mathf.Sqrt(halfWidth * halfWidth + height * height);
        var roofAngle = Mathf.Atan2(height, halfWidth);
        for (var side = -1; side <= 1; side += 2)
        {
            var roofCenter = new Vector3(center.X + side * halfWidth * 0.5f, center.Y + height * 0.5f, center.Z);
            var visual = MeshBox(
                parent,
                roofCenter,
                new Vector3(roofLength, 0.14f, size.Y),
                roof,
                new Vector3(0, 0, -side * roofAngle));
            visual.Name = $"{prefix}_Roof_{side}";
            RegisterMapDetailVisual(visual);
        }
        foreach (var x in new[] { -halfWidth + 0.2f, halfWidth - 0.2f })
        {
            ExpansionBox(parent, $"{prefix}_Post_{x:0.0}", new Vector3(center.X + x, center.Y * 0.5f, center.Z - size.Y * 0.42f), new Vector3(0.18f, center.Y, 0.18f), post);
            ExpansionBox(parent, $"{prefix}_PostBack_{x:0.0}", new Vector3(center.X + x, center.Y * 0.5f, center.Z + size.Y * 0.42f), new Vector3(0.18f, center.Y, 0.18f), post);
        }
    }

    private void BuildSalvageBazaar(
        Vector3 position,
        Godot.Material paving,
        Godot.Material timber,
        Godot.Material steel,
        Godot.Material steelDark,
        Godot.Material yellow,
        Godot.Material orange)
    {
        var root = CreateSpecialLandmarkRoot("SalvageBazaar", position, "SALVAGE BAZAAR", new Color(1.0f, 0.66f, 0.22f));
        ExpansionBox(root, "BazaarPaving", new Vector3(0, -0.02f, 0), new Vector3(25, 0.12f, 19), paving);
        var kiosks = new[]
        {
            new Vector3(-8.0f, 0.0f, -5.0f),
            new Vector3(0.0f, 0.0f, -5.0f),
            new Vector3(8.0f, 0.0f, -5.0f),
            new Vector3(-8.0f, 0.0f, 4.5f),
            new Vector3(8.0f, 0.0f, 4.5f)
        };
        for (var index = 0; index < kiosks.Length; index++)
        {
            var kiosk = kiosks[index];
            var body = ExpansionBox(root, $"BazaarKiosk_{index:00}", kiosk + Vector3.Up * 0.62f, new Vector3(3.7f, 1.24f, 2.4f), index % 2 == 0 ? timber : steelDark);
            MeshBox(body, new Vector3(0, 0.68f, 0), new Vector3(3.95f, 0.12f, 2.58f), yellow).Name = $"BazaarKioskCounter_{index:00}";
            MeshBox(body, new Vector3(0, 1.36f, -1.08f), new Vector3(2.4f, 0.52f, 0.06f), orange).Name = $"BazaarKioskSign_{index:00}";
        }

        AddSpecialCanopy(root, "BazaarCanopy", new Vector3(0, 3.0f, 0), new Vector2(18.0f, 13.5f), 2.4f, timber, steel);
        ExpansionCylinder(root, "BazaarSignalMast", new Vector3(0, 4.8f, 0), 0.16f, 9.6f, steelDark);
        MeshBox(root, new Vector3(0, 9.75f, 0), new Vector3(2.2f, 0.18f, 0.18f), orange).Name = "BazaarSignalArm";

        ExpansionBox(root, "BazaarAuctionDeck", new Vector3(0, 2.88f, -1.6f), new Vector3(7.8f, 0.28f, 5.4f), steel);
        foreach (var x in new[] { -3.35f, 3.35f })
        {
            ExpansionBox(root, $"BazaarDeckSupport_{x:0.0}", new Vector3(x, 1.45f, -1.6f), new Vector3(0.28f, 2.9f, 0.28f), steelDark);
        }
        AddSpecialSteps(root, "BazaarDeckStair", new Vector3(-2.35f, 0.08f, 2.8f), new Vector3(0, 0, -1), 2.8f, 5.8f, 2.68f, 8, steel);
        AddSpecialVerticalRouteMarker(root, "BazaarDeckVerticalRoute");
        AddSpecialSlopedRail(root, "BazaarDeckRailL", new Vector3(-3.55f, 0.9f, 3.1f), new Vector3(-3.55f, 3.72f, -2.1f), 0.12f, orange);
        AddSpecialSlopedRail(root, "BazaarDeckRailR", new Vector3(-1.15f, 0.9f, 3.1f), new Vector3(-1.15f, 3.72f, -2.1f), 0.12f, orange);

        var loot = new (Vector3 Position, LootGrade Grade, string Name)[]
        {
            (new Vector3(-8.0f, 1.32f, -5.0f), LootGrade.Uncommon, "BazaarKioskNorthWest"),
            (new Vector3(0.0f, 1.32f, -5.0f), LootGrade.Rare, "BazaarKioskNorth"),
            (new Vector3(8.0f, 1.32f, -5.0f), LootGrade.Uncommon, "BazaarKioskNorthEast"),
            (new Vector3(-8.0f, 1.32f, 4.5f), LootGrade.Common, "BazaarKioskSouthWest"),
            (new Vector3(8.0f, 1.32f, 4.5f), LootGrade.Rare, "BazaarKioskSouthEast"),
            (new Vector3(0.0f, 3.25f, -1.6f), LootGrade.Epic, "BazaarAuctionDeck"),
            (new Vector3(0.0f, 0.3f, 7.0f), LootGrade.Legendary, "BazaarBrokerCrate")
        };
        for (var index = 0; index < loot.Length; index++)
        {
            var item = loot[index];
            AddSpecialLoot(root, $"SpecialLoot_Bazaar_{index:00}", item.Position, item.Grade, item.Name, item.Name);
        }
        AddSpecialLabel(root, "BazaarDeckLabel", new Vector3(0, 3.4f, -1.6f), "AUCTION DECK  //  UP", new Color(1.0f, 0.77f, 0.35f));
    }

    private void BuildTideglassConservatory(
        Vector3 position,
        Godot.Material paving,
        Godot.Material concrete,
        Godot.Material steel,
        Godot.Material steelDark,
        Godot.Material glass,
        Godot.Material cyan)
    {
        var root = CreateSpecialLandmarkRoot("TideglassConservatory", position, "TIDEGLASS CONSERVATORY", new Color(0.32f, 0.94f, 0.78f));
        ExpansionBox(root, "HydroPaving", new Vector3(0, -0.02f, 0), new Vector3(27, 0.12f, 21), paving);
        ExpansionBox(root, "HydroWallWest", new Vector3(-12.7f, 2.45f, 0), new Vector3(0.14f, 4.9f, 20.2f), glass);
        ExpansionBox(root, "HydroWallEast", new Vector3(12.7f, 2.45f, 0), new Vector3(0.14f, 4.9f, 20.2f), glass);
        ExpansionBox(root, "HydroWallNorth", new Vector3(0, 2.45f, -9.95f), new Vector3(25.4f, 4.9f, 0.14f), glass);
        ExpansionBox(root, "HydroWallSouthWest", new Vector3(-8.9f, 2.45f, 9.95f), new Vector3(7.6f, 4.9f, 0.14f), glass);
        ExpansionBox(root, "HydroWallSouthEast", new Vector3(8.9f, 2.45f, 9.95f), new Vector3(7.6f, 4.9f, 0.14f), glass);
        AddSpecialCanopy(root, "HydroGlassRoof", new Vector3(0, 4.8f, 0), new Vector2(25.2f, 19.8f), 2.1f, glass, steel);
        for (var x = -9.0f; x <= 9.0f; x += 6.0f)
        {
            ExpansionBox(root, $"HydroFrame_{x:0.0}", new Vector3(x, 2.6f, 0), new Vector3(0.16f, 5.2f, 0.16f), steelDark);
        }

        var planterIndex = 0;
        foreach (var x in new[] { -8.3f, -3.0f, 3.0f, 8.3f })
        {
            foreach (var z in new[] { -5.7f, 0.0f, 5.7f })
            {
                ExpansionBox(root, $"HydroPlanter_{planterIndex:00}", new Vector3(x, 0.45f, z), new Vector3(2.8f, 0.9f, 1.75f), concrete);
                MeshBox(root, new Vector3(x, 0.96f, z), new Vector3(2.45f, 0.08f, 1.4f), cyan).Name = $"HydroPlanterGlow_{planterIndex:00}";
                planterIndex++;
            }
        }
        ExpansionBox(root, "HydroMaintenanceCatwalk", new Vector3(0, 3.35f, -6.8f), new Vector3(20.5f, 0.24f, 2.2f), steel);
        AddSpecialSteps(root, "HydroCatwalkStair", new Vector3(-9.1f, 0.08f, 3.8f), new Vector3(0, 0, -1), 2.4f, 9.5f, 3.05f, 10, steel);
        AddSpecialVerticalRouteMarker(root, "HydroCatwalkVerticalRoute");
        AddSpecialSlopedRail(root, "HydroCatwalkRailL", new Vector3(-10.45f, 0.9f, 4.2f), new Vector3(-10.45f, 4.05f, -6.2f), 0.11f, cyan);
        AddSpecialSlopedRail(root, "HydroCatwalkRailR", new Vector3(-7.75f, 0.9f, 4.2f), new Vector3(-7.75f, 4.05f, -6.2f), 0.11f, cyan);

        ExpansionCylinder(root, "HydroTankWest", new Vector3(10.2f, 1.1f, -8.0f), 1.1f, 2.2f, steelDark);
        ExpansionCylinder(root, "HydroTankEast", new Vector3(10.2f, 2.8f, -8.0f), 0.75f, 1.2f, cyan);
        var loot = new (Vector3 Position, LootGrade Grade, string Name)[]
        {
            (new Vector3(-8.3f, 1.02f, -5.7f), LootGrade.Common, "HydroWestCrop"),
            (new Vector3(-3.0f, 1.02f, 0.0f), LootGrade.Uncommon, "HydroNutrientRack"),
            (new Vector3(3.0f, 1.02f, 5.7f), LootGrade.Rare, "HydroEastCrop"),
            (new Vector3(8.3f, 1.02f, -5.7f), LootGrade.Uncommon, "HydroSeedLocker"),
            (new Vector3(-5.8f, 3.72f, -6.8f), LootGrade.Epic, "HydroCatwalkWest"),
            (new Vector3(5.8f, 3.72f, -6.8f), LootGrade.Rare, "HydroCatwalkEast"),
            (new Vector3(0.0f, 0.3f, 8.3f), LootGrade.Legendary, "HydroIntakeCrate")
        };
        for (var index = 0; index < loot.Length; index++)
        {
            var item = loot[index];
            AddSpecialLoot(root, $"SpecialLoot_Hydro_{index:00}", item.Position, item.Grade, item.Name, item.Name);
        }
        AddSpecialLabel(root, "HydroCatwalkLabel", new Vector3(0, 4.0f, -6.8f), "MAINTENANCE CATWALK", new Color(0.42f, 1.0f, 0.82f));
    }

    private void BuildTidalObservatory(
        Vector3 position,
        Godot.Material paving,
        Godot.Material concrete,
        Godot.Material steel,
        Godot.Material steelDark,
        Godot.Material rust,
        Godot.Material cyan)
    {
        var root = CreateSpecialLandmarkRoot("TidalObservatory", position, "TIDAL OBSERVATORY", new Color(0.48f, 0.8f, 1.0f));
        ExpansionCylinder(root, "ObservatoryPlinth", new Vector3(0, 0.08f, 0), 11.5f, 0.16f, paving);
        for (var segment = 0; segment < 10; segment++)
        {
            var angle = Mathf.Tau * segment / 10.0f;
            var radial = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle));
            var center = radial * 9.4f + Vector3.Up * 1.55f;
            ExpansionBox(
                root,
                $"ObservatoryRingWall_{segment:00}",
                center,
                new Vector3(0.34f, 3.1f, 5.0f),
                segment % 2 == 0 ? concrete : steel,
                new Vector3(0, angle, 0));
        }
        ExpansionCylinder(root, "ObservatoryCore", new Vector3(0, 4.2f, 0), 2.45f, 8.2f, steelDark);
        ExpansionCylinder(root, "ObservatoryCoreCap", new Vector3(0, 8.45f, 0), 3.2f, 0.3f, steel);
        ExpansionCylinder(root, "ObservatoryAntenna", new Vector3(0, 13.0f, 0), 0.16f, 9.0f, rust);
        var ringVisual = new MeshInstance3D
        {
            Name = "ObservatoryDeckRing",
            Position = new Vector3(0, 5.65f, 0),
            Mesh = new TorusMesh { InnerRadius = 5.0f, OuterRadius = 6.8f, Rings = 48, RingSegments = 12 },
            MaterialOverride = steel
        };
        root.AddChild(ringVisual);
        for (var segment = 0; segment < 12; segment++)
        {
            var angle = Mathf.Tau * segment / 12.0f;
            var radial = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle));
            ExpansionBox(
                root,
                $"ObservatoryDeckCollision_{segment:00}",
                radial * 5.9f + Vector3.Up * 5.62f,
                new Vector3(0.9f, 0.18f, 3.1f),
                steel,
                new Vector3(0, angle, 0));
        }
        for (var segment = 0; segment < 12; segment++)
        {
            var angle = Mathf.Tau * segment / 12.0f;
            var radial = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle));
            ExpansionBox(root, $"ObservatoryGuard_{segment:00}", radial * 7.0f + Vector3.Up * 6.25f, new Vector3(0.12f, 1.15f, 3.5f), steelDark, new Vector3(0, angle, 0));
        }

        AddSpecialSteps(root, "ObservatoryDeckStair", new Vector3(-8.7f, 0.08f, 8.2f), new Vector3(0, 0, -1), 2.5f, 12.0f, 5.35f, 14, concrete);
        AddSpecialVerticalRouteMarker(root, "ObservatoryDeckVerticalRoute");
        AddSpecialSlopedRail(root, "ObservatoryStairRailL", new Vector3(-10.05f, 0.95f, 8.5f), new Vector3(-10.05f, 6.22f, -3.6f), 0.12f, cyan);
        AddSpecialSlopedRail(root, "ObservatoryStairRailR", new Vector3(-7.35f, 0.95f, 8.5f), new Vector3(-7.35f, 6.22f, -3.6f), 0.12f, cyan);

        foreach (var x in new[] { 3.1f, 3.9f })
        {
            ExpansionBox(root, $"ObservatoryRoofLadderRail_{x:0.0}", new Vector3(x, 7.35f, 0), new Vector3(0.12f, 3.4f, 0.12f), steelDark);
        }
        for (var rung = 0; rung < 7; rung++)
        {
            ExpansionBox(root, $"ObservatoryRoofLadderRung_{rung:00}", new Vector3(3.5f, 5.95f + rung * 0.47f, 0), new Vector3(0.9f, 0.1f, 0.16f), cyan);
        }
        AddSpecialVerticalRouteMarker(root, "ObservatoryRoofVerticalRoute");

        root.AddChild(new MeshInstance3D
        {
            Name = "ObservatoryRadarDish",
            Position = new Vector3(0, 11.4f, 0),
            Rotation = new Vector3(0.42f, 0.15f, 0),
            Mesh = new CylinderMesh { TopRadius = 0.2f, BottomRadius = 2.0f, Height = 0.55f, RadialSegments = 28 },
            MaterialOverride = cyan
        });
        var loot = new (Vector3 Position, LootGrade Grade, string Name)[]
        {
            (new Vector3(-5.8f, 0.3f, 5.0f), LootGrade.Common, "ObservatoryIntake"),
            (new Vector3(5.5f, 0.3f, 5.2f), LootGrade.Uncommon, "ObservatoryWeatherCabinet"),
            (new Vector3(-5.0f, 0.3f, -5.7f), LootGrade.Rare, "ObservatoryArchive"),
            (new Vector3(5.2f, 0.3f, -5.5f), LootGrade.Uncommon, "ObservatoryBatteryRack"),
            (new Vector3(-5.8f, 5.95f, 0.0f), LootGrade.Epic, "ObservatoryDeckWest"),
            (new Vector3(5.8f, 5.95f, 0.0f), LootGrade.Rare, "ObservatoryDeckEast"),
            (new Vector3(0.0f, 8.8f, 0.0f), LootGrade.Legendary, "ObservatoryCoreSafe")
        };
        for (var index = 0; index < loot.Length; index++)
        {
            var item = loot[index];
            AddSpecialLoot(root, $"SpecialLoot_Observatory_{index:00}", item.Position, item.Grade, item.Name, item.Name);
        }
        AddSpecialLabel(root, "ObservatoryDeckLabel", new Vector3(0, 6.5f, 7.1f), "TIDE DECK  //  LEVEL 02", new Color(0.55f, 0.86f, 1.0f));
    }

    private void BuildDrydockRepairCradle(
        Vector3 position,
        Godot.Material paving,
        Godot.Material concrete,
        Godot.Material steel,
        Godot.Material steelDark,
        Godot.Material rust,
        Godot.Material yellow,
        Godot.Material corrugated,
        Godot.Material orange,
        Godot.Material white)
    {
        var root = CreateSpecialLandmarkRoot("DrydockRepairCradle", position, "DRYDOCK REPAIR CRADLE", new Color(1.0f, 0.48f, 0.25f));
        ExpansionBox(root, "DrydockPad", new Vector3(0, -0.02f, 0), new Vector3(32, 0.12f, 26), paving);
        ExpansionBox(root, "DrydockPit", new Vector3(0, 0.25f, 0), new Vector3(12, 0.5f, 21), concrete);
        foreach (var x in new[] { -6.6f, 6.6f })
        {
            ExpansionBox(root, $"DrydockCradleRail_{x:0.0}", new Vector3(x, 0.65f, 0), new Vector3(0.45f, 1.3f, 22.0f), steelDark);
            for (var z = -8.0f; z <= 8.0f; z += 4.0f)
            {
                ExpansionBox(root, $"DrydockCradleBrace_{x:0.0}_{z:0.0}", new Vector3(x * 0.58f, 1.2f, z), new Vector3(0.34f, 3.3f, 0.34f), rust, new Vector3(0, 0, x < 0 ? -0.62f : 0.62f));
            }
        }
        ExpansionBox(root, "DrydockHullKeel", new Vector3(0, 1.25f, 0), new Vector3(2.3f, 1.1f, 18.8f), steelDark);
        ExpansionBox(root, "DrydockHullPort", new Vector3(-2.15f, 2.05f, 0), new Vector3(3.4f, 0.5f, 17.2f), white, new Vector3(0, 0, -0.42f));
        ExpansionBox(root, "DrydockHullStarboard", new Vector3(2.15f, 2.05f, 0), new Vector3(3.4f, 0.5f, 17.2f), white, new Vector3(0, 0, 0.42f));
        root.AddChild(new MeshInstance3D
        {
            Name = "DrydockHullBow",
            Position = new Vector3(0, 2.05f, -9.5f),
            Rotation = new Vector3(Mathf.Pi / 2.0f, 0, 0),
            Mesh = new CylinderMesh { TopRadius = 0.45f, BottomRadius = 3.4f, Height = 2.4f, RadialSegments = 24 },
            MaterialOverride = white
        });

        foreach (var z in new[] { -8.5f, 0.0f, 8.5f })
        {
            foreach (var x in new[] { -13.0f, 13.0f })
            {
                ExpansionBox(root, $"DrydockGantryPost_{x:0}_{z:0}", new Vector3(x, 4.7f, z), new Vector3(0.42f, 9.4f, 0.42f), steel);
            }
            ExpansionBox(root, $"DrydockGantryBeam_{z:0}", new Vector3(0, 9.25f, z), new Vector3(26.4f, 0.5f, 0.6f), yellow);
        }
        ExpansionBox(root, "DrydockCatwalkWest", new Vector3(-11.4f, 4.25f, 0), new Vector3(2.3f, 0.24f, 21.0f), steel);
        ExpansionBox(root, "DrydockCatwalkEast", new Vector3(11.4f, 4.25f, 0), new Vector3(2.3f, 0.24f, 21.0f), steel);
        foreach (var x in new[] { -12.65f, -10.15f, 10.15f, 12.65f })
        {
            ExpansionBox(root, $"DrydockCatwalkGuard_{x:0.0}", new Vector3(x, 4.9f, 0), new Vector3(0.12f, 1.2f, 21.0f), steelDark);
        }
        AddSpecialSteps(root, "DrydockWestStair", new Vector3(-11.4f, 0.08f, 11.4f), new Vector3(0, 0, -1), 2.2f, 9.5f, 4.0f, 12, steel);
        AddSpecialVerticalRouteMarker(root, "DrydockWestVerticalRoute");
        AddSpecialSlopedRail(root, "DrydockWestStairRailL", new Vector3(-12.65f, 0.95f, 11.7f), new Vector3(-12.65f, 4.85f, 2.1f), 0.11f, orange);
        AddSpecialSlopedRail(root, "DrydockWestStairRailR", new Vector3(-10.15f, 0.95f, 11.7f), new Vector3(-10.15f, 4.85f, 2.1f), 0.11f, orange);
        AddSpecialSteps(root, "DrydockEastStair", new Vector3(11.4f, 0.08f, -11.4f), new Vector3(0, 0, 1), 2.2f, 9.5f, 4.0f, 12, steel);
        AddSpecialVerticalRouteMarker(root, "DrydockEastVerticalRoute");
        AddSpecialSlopedRail(root, "DrydockEastStairRailL", new Vector3(10.15f, 0.95f, -11.7f), new Vector3(10.15f, 4.85f, -2.1f), 0.11f, orange);
        AddSpecialSlopedRail(root, "DrydockEastStairRailR", new Vector3(12.65f, 0.95f, -11.7f), new Vector3(12.65f, 4.85f, -2.1f), 0.11f, orange);
        MeshBox(root, new Vector3(0, 10.1f, 0), new Vector3(9.0f, 0.16f, 3.2f), corrugated).Name = "DrydockCraneCabRoof";

        var loot = new (Vector3 Position, LootGrade Grade, string Name)[]
        {
            (new Vector3(-7.8f, 0.3f, -8.0f), LootGrade.Uncommon, "DrydockToolCrate"),
            (new Vector3(7.8f, 0.3f, 8.0f), LootGrade.Common, "DrydockPartsBin"),
            (new Vector3(-7.8f, 0.3f, 8.0f), LootGrade.Rare, "DrydockWelderLocker"),
            (new Vector3(7.8f, 0.3f, -8.0f), LootGrade.Uncommon, "DrydockRiggingCache"),
            (new Vector3(-11.4f, 4.62f, -6.2f), LootGrade.Epic, "DrydockWestCatwalk"),
            (new Vector3(11.4f, 4.62f, 6.2f), LootGrade.Rare, "DrydockEastCatwalk"),
            (new Vector3(0.0f, 2.8f, 6.4f), LootGrade.Legendary, "DrydockHullStrongbox")
        };
        for (var index = 0; index < loot.Length; index++)
        {
            var item = loot[index];
            AddSpecialLoot(root, $"SpecialLoot_Drydock_{index:00}", item.Position, item.Grade, item.Name, item.Name);
        }
        AddSpecialLabel(root, "DrydockCatwalkLabel", new Vector3(0, 5.2f, 10.8f), "GANTRY ACCESS  //  BOTH SIDES", new Color(1.0f, 0.62f, 0.3f));
    }

    private async void ValidateSpecialLandmarks()
    {
        DisableActorsForSurvivalDiagnostics();
        await WaitFrames(4);
        var expected = new (string Name, Vector3 Position, string KeyPart)[]
        {
            ("SalvageBazaar", new Vector3(-76.0f, 0.0f, 4.0f), "BazaarAuctionDeck"),
            ("TideglassConservatory", new Vector3(113.0f, 0.0f, 9.0f), "HydroMaintenanceCatwalk"),
            ("TidalObservatory", new Vector3(-114.0f, 0.0f, 43.0f), "ObservatoryCore"),
            ("DrydockRepairCradle", new Vector3(77.0f, 0.0f, -151.0f), "DrydockHullKeel")
        };
        var present = 0;
        var collisionReady = true;
        var keyStructuresReady = true;
        var spawnClear = true;
        var nearestSpawnDistance = float.PositiveInfinity;
        foreach (var landmark in expected)
        {
            var root = _levelRoot.GetNodeOrNull<Node3D>(landmark.Name);
            if (root is null)
            {
                collisionReady = false;
                keyStructuresReady = false;
                continue;
            }
            present++;
            var staticBodies = 0;
            foreach (var child in root.GetChildren())
            {
                if (child is StaticBody3D)
                {
                    staticBodies++;
                }
            }
            collisionReady &= staticBodies >= 5;
            keyStructuresReady &= root.GetNodeOrNull(landmark.KeyPart) is not null;
            foreach (var pad in ExtractionSpawnPads.Pads)
            {
                var distance = new Vector2(landmark.Position.X - pad.X, landmark.Position.Z - pad.Z).Length();
                nearestSpawnDistance = Mathf.Min(nearestSpawnDistance, distance);
                spawnClear &= distance >= 34.0f;
            }
        }

        var lootNodes = GetTree().GetNodesInGroup("special_landmark_loot");
        var lootRegistered = true;
        var lootGrades = new HashSet<LootGrade>();
        foreach (var node in lootNodes)
        {
            if (node is not GradedLootPickup pickup || !IsInstanceValid(pickup) || !pickup.IsSearchable)
            {
                lootRegistered = false;
                continue;
            }
            lootRegistered &= _lootSources.Contains(pickup);
            if (pickup.Loot.Count > 0)
            {
                lootGrades.Add(pickup.Loot[0].Grade);
            }
        }
        var routeNodes = GetTree().GetNodesInGroup("special_landmark_vertical_route");
        var valid = SpecialLandmarkCount == expected.Length
            && present == expected.Length
            && collisionReady
            && keyStructuresReady
            && spawnClear
            && SpecialLandmarkLootCount >= 28
            && lootNodes.Count == SpecialLandmarkLootCount
            && lootRegistered
            && lootGrades.Count == Enum.GetValues<LootGrade>().Length
            && SpecialLandmarkVerticalRouteCount >= 5
            && routeNodes.Count == SpecialLandmarkVerticalRouteCount;
        GD.Print($"SPECIAL_LANDMARK_CHECK valid={valid} landmarks={SpecialLandmarkCount}/{expected.Length} present={present} collision={collisionReady} key_structures={keyStructuresReady} loot={SpecialLandmarkLootCount}/{lootNodes.Count} registered={lootRegistered} grades={lootGrades.Count} routes={SpecialLandmarkVerticalRouteCount}/{routeNodes.Count} spawn_clear={spawnClear} nearest_spawn={nearestSpawnDistance:0.0}");
        GD.Print($"SPECIAL_LANDMARK_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }

    private async void CaptureSpecialLandmarks()
    {
        DisableActorsForSurvivalDiagnostics();
        _hud.Visible = false;
        _player.ProcessMode = ProcessModeEnum.Disabled;
        var camera = new Camera3D
        {
            Name = "SpecialLandmarkCaptureCamera",
            Fov = 68.0f,
            Near = 0.05f,
            Far = 520.0f
        };
        AddChild(camera);
        camera.GlobalPosition = new Vector3(0, 215.0f, MapCenterZ);
        camera.LookAt(new Vector3(0, 0, MapCenterZ), Vector3.Forward);
        camera.MakeCurrent();
        await WaitFrames(32);
        SaveViewportImage("res://special_landmarks_overview_validation.png");

        camera.GlobalPosition = new Vector3(-54.0f, 13.0f, 24.0f);
        camera.Fov = 60.0f;
        camera.LookAt(new Vector3(-76.0f, 2.2f, 4.0f), Vector3.Up);
        await WaitFrames(20);
        SaveViewportImage("res://special_landmark_bazaar_validation.png");

        camera.GlobalPosition = new Vector3(104.0f, 13.0f, -126.0f);
        camera.LookAt(new Vector3(77.0f, 3.2f, -151.0f), Vector3.Up);
        await WaitFrames(20);
        SaveViewportImage("res://special_landmark_drydock_validation.png");

        camera.GlobalPosition = new Vector3(137.0f, 12.0f, 31.0f);
        camera.LookAt(new Vector3(113.0f, 2.8f, 9.0f), Vector3.Up);
        await WaitFrames(20);
        SaveViewportImage("res://special_landmark_tideglass_validation.png");

        camera.GlobalPosition = new Vector3(-114.0f, 14.0f, 15.0f);
        camera.LookAt(new Vector3(-114.0f, 4.5f, 43.0f), Vector3.Up);
        await WaitFrames(20);
        SaveViewportImage("res://special_landmark_observatory_validation.png");
        GD.Print($"SPECIAL_LANDMARK_CAPTURE landmarks={SpecialLandmarkCount} loot={SpecialLandmarkLootCount} routes={SpecialLandmarkVerticalRouteCount} overview=special_landmarks_overview_validation.png bazaar=special_landmark_bazaar_validation.png drydock=special_landmark_drydock_validation.png tideglass=special_landmark_tideglass_validation.png observatory=special_landmark_observatory_validation.png");
        GetTree().Quit();
    }
}
