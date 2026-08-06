using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private StandardMaterial3D ExpansionPbrMaterial(
        string id,
        string asset,
        Color tint,
        float metallic,
        float roughness,
        float uvScale)
    {
        if (_materials.TryGetValue(id, out var cached))
        {
            return cached;
        }
        var root = $"res://assets/textures/{asset}";
        var material = new StandardMaterial3D
        {
            AlbedoColor = tint,
            AlbedoTexture = GD.Load<Texture2D>(root + "_diff_1k.jpg"),
            NormalEnabled = true,
            NormalTexture = GD.Load<Texture2D>(root + "_normal_1k.jpg"),
            NormalScale = 0.88f,
            Metallic = metallic,
            Roughness = roughness,
            RoughnessTexture = GD.Load<Texture2D>(root + "_rough_1k.jpg"),
            Uv1Triplanar = true,
            Uv1WorldTriplanar = true,
            Uv1Scale = Vector3.One * uvScale,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic
        };
        _materials[id] = material;
        return material;
    }

    private void BuildHarborExpansion(
        Godot.Material asphalt,
        Godot.Material concrete,
        Godot.Material concreteDark,
        Godot.Material steel,
        Godot.Material steelDark,
        Godot.Material rust,
        Godot.Material yellow,
        Godot.Material white)
    {
        var gravel = GroundMaterial("gravel", new Color(0.57f, 0.55f, 0.49f), 0.94f);
        var corrugated = ExpansionPbrMaterial(
            "corrugated_iron",
            "corrugated_iron",
            new Color(0.72f, 0.76f, 0.73f),
            0.58f,
            0.66f,
            0.34f);
        BuildExpansionRoad(asphalt, concrete, yellow, white);
        BuildNorthAccessGate(concreteDark, steelDark, corrugated, yellow);
        BuildSouthOverflowYard(concrete, steelDark, corrugated);
        BuildNorthernRailYard(gravel, concrete, steel, steelDark, rust, yellow, corrugated);
        BuildMaintenanceDistrict(concrete, steel, steelDark, yellow, corrugated);
        BuildTankFarmDistrict(concrete, steel, steelDark, rust, yellow);
        BuildSeawallDistrict(concrete, steel, steelDark, yellow, white, corrugated);
        BuildExpansionCover(concreteDark);
        BuildExpansionLights();
    }

    private void BuildExpansionRoad(
        Godot.Material asphalt,
        Godot.Material concrete,
        Godot.Material yellow,
        Godot.Material white)
    {
        var road = new Node3D { Name = "HarborTransitRoad" };
        _levelRoot.AddChild(road);
        ExpansionBox(road, "NorthServiceRoad", new Vector3(0, 0.015f, -103), new Vector3(17, 0.12f, 124), asphalt);
        ExpansionBox(road, "QuayAccessRoad", new Vector3(39, 0.02f, -128), new Vector3(78, 0.12f, 14), asphalt);
        ExpansionBox(road, "RailSpurCrossing", new Vector3(-44, 0.025f, -55), new Vector3(72, 0.13f, 12), concrete);
        for (var z = -48; z >= -160; z -= 8)
        {
            MeshBox(road, new Vector3(-3.7f, 0.09f, z), new Vector3(0.14f, 0.025f, 3.4f), white);
            MeshBox(road, new Vector3(3.7f, 0.09f, z), new Vector3(0.14f, 0.025f, 3.4f), white);
        }
        for (var x = 8; x <= 76; x += 8)
        {
            MeshBox(road, new Vector3(x, 0.095f, -124.6f), new Vector3(3.4f, 0.026f, 0.14f), yellow);
            MeshBox(road, new Vector3(x, 0.095f, -131.4f), new Vector3(3.4f, 0.026f, 0.14f), yellow);
        }
        foreach (var z in new[] { -52.0f, -96.0f, -142.0f })
        {
            for (var stripe = -3; stripe <= 3; stripe++)
            {
                MeshBox(road, new Vector3(stripe * 2.25f, 0.105f, z), new Vector3(1.45f, 0.028f, 0.36f), yellow);
            }
        }
    }

    private void BuildNorthAccessGate(
        Godot.Material concrete,
        Godot.Material steel,
        Godot.Material corrugated,
        Godot.Material yellow)
    {
        var gate = new Node3D { Name = "NorthAccessGate" };
        _levelRoot.AddChild(gate);
        ExpansionBox(gate, "WestSecurityFence", new Vector3(-60, 1.45f, -46), new Vector3(100, 2.9f, 0.34f), corrugated);
        ExpansionBox(gate, "EastSecurityFence", new Vector3(60, 1.45f, -46), new Vector3(100, 2.9f, 0.34f), corrugated);
        foreach (var x in new[] { -10.5f, 10.5f })
        {
            ExpansionBox(gate, "GatePillar", new Vector3(x, 2.3f, -46), new Vector3(0.8f, 4.6f, 0.8f), concrete);
            MeshBox(gate, new Vector3(x, 4.9f, -46), new Vector3(1.25f, 0.28f, 1.25f), yellow);
        }
        ExpansionBox(gate, "GateHeader", new Vector3(0, 5.15f, -46), new Vector3(22, 0.5f, 0.65f), steel);
        MeshBox(gate, new Vector3(0, 4.78f, -45.62f), new Vector3(12.5f, 1.25f, 0.08f), concrete);
        foreach (var x in new[] { -7.5f, 7.5f })
        {
            ExpansionCylinder(gate, "GateBollard", new Vector3(x, 0.55f, -43.3f), 0.18f, 1.1f, yellow);
        }
        gate.AddChild(new OmniLight3D
        {
            Position = new Vector3(0, 4.65f, -44.8f),
            LightColor = new Color(0.68f, 0.9f, 1.0f),
            LightEnergy = 2.6f,
            OmniRange = 16.0f,
            ShadowEnabled = false
        });
    }

    private void BuildSouthOverflowYard(
        Godot.Material concrete,
        Godot.Material steel,
        Godot.Material corrugated)
    {
        var district = new Node3D { Name = "SouthOverflowYard" };
        _levelRoot.AddChild(district);
        ExpansionBox(district, "EastOverflowPad", new Vector3(76, -0.01f, 4), new Vector3(62, 0.1f, 78), concrete);
        ExpansionBox(district, "WestTrailerPad", new Vector3(-76, -0.01f, 4), new Vector3(62, 0.1f, 78), concrete);

        var blue = PaintedMetal("expansion_container_blue", new Color(0.16f, 0.38f, 0.55f));
        var red = PaintedMetal("expansion_container_red", new Color(0.57f, 0.18f, 0.12f));
        var green = PaintedMetal("expansion_container_green", new Color(0.18f, 0.42f, 0.27f));
        var gray = PaintedMetal("expansion_container_gray", new Color(0.48f, 0.51f, 0.49f));
        var containers = new (Vector3 Position, float Yaw, Godot.Material Material)[]
        {
            (new Vector3(56, 1.35f, 30), 0, blue),
            (new Vector3(70, 1.35f, 30), 0, red),
            (new Vector3(84, 1.35f, 30), 0, green),
            (new Vector3(98, 1.35f, 30), 0, gray),
            (new Vector3(58, 1.35f, 13), Mathf.Pi / 2, gray),
            (new Vector3(58, 4.05f, 13), Mathf.Pi / 2, blue),
            (new Vector3(79, 1.35f, 10), 0, green),
            (new Vector3(93, 1.35f, 7), 0.08f, red),
            (new Vector3(72, 1.35f, -10), Mathf.Pi / 2, blue),
            (new Vector3(72, 4.05f, -10), Mathf.Pi / 2, gray),
            (new Vector3(94, 1.35f, -22), 0, green),
            (new Vector3(94, 4.05f, -22), 0, red)
        };
        foreach (var item in containers)
        {
            BuildExpansionContainer(district, item.Position, item.Yaw, item.Material, steel);
        }

        BuildTruckTrailer(district, new Vector3(-93, 0, 27), 0.02f, corrugated, steel);
        BuildTruckTrailer(district, new Vector3(-75, 0, 18), -0.06f, corrugated, steel);
        BuildTruckTrailer(district, new Vector3(-56, 0, 31), 0.04f, corrugated, steel);
        BuildTruckTrailer(district, new Vector3(-92, 0, -4), 0.03f, corrugated, steel);
        BuildTruckTrailer(district, new Vector3(-66, 0, -18), -0.08f, corrugated, steel);
    }

    private void BuildNorthernRailYard(
        Godot.Material gravel,
        Godot.Material concrete,
        Godot.Material steel,
        Godot.Material steelDark,
        Godot.Material rust,
        Godot.Material yellow,
        Godot.Material corrugated)
    {
        var district = new Node3D { Name = "NorthernRailYard" };
        _levelRoot.AddChild(district);
        ExpansionBox(district, "RailBallast", new Vector3(-70, -0.005f, -108), new Vector3(78, 0.11f, 118), gravel);
        var railMaterial = Mat("rail_track", new Color(0.12f, 0.13f, 0.12f), 0.9f, 0.3f);
        var sleeperMaterial = Mat("rail_sleeper", new Color(0.22f, 0.17f, 0.11f), 0.08f, 0.92f);
        foreach (var trackX in new[] { -98.0f, -81.0f, -64.0f, -47.0f })
        {
            foreach (var offset in new[] { -0.76f, 0.76f })
            {
                MeshBox(district, new Vector3(trackX + offset, 0.105f, -108), new Vector3(0.1f, 0.14f, 112), railMaterial);
            }
            for (var z = -53.0f; z >= -163.0f; z -= 3.0f)
            {
                MeshBox(district, new Vector3(trackX, 0.06f, z), new Vector3(2.25f, 0.09f, 0.3f), sleeperMaterial);
            }
        }

        BuildRailCar(district, new Vector3(-98, 0, -82), new Color(0.22f, 0.38f, 0.42f), steelDark, rust);
        BuildRailCar(district, new Vector3(-98, 0, -111), new Color(0.48f, 0.18f, 0.1f), steelDark, rust);
        BuildRailCar(district, new Vector3(-81, 0, -136), new Color(0.18f, 0.35f, 0.24f), steelDark, rust);
        BuildRailCar(district, new Vector3(-64, 0, -76), new Color(0.42f, 0.43f, 0.4f), steelDark, rust);
        BuildRailCar(district, new Vector3(-47, 0, -121), new Color(0.2f, 0.3f, 0.44f), steelDark, rust);

        BuildRailDispatchOffice(district, concrete, steelDark, corrugated, yellow);
        ExpansionBox(district, "LoadingPlatform", new Vector3(-34, 0.55f, -94), new Vector3(9, 1.1f, 42), concrete);
        ExpansionBox(district, "LoadingCanopy", new Vector3(-34, 4.2f, -94), new Vector3(10, 0.28f, 44), corrugated);
        for (var z = -112; z <= -76; z += 9)
        {
            ExpansionBox(district, "CanopyPost", new Vector3(-38.2f, 2.1f, z), new Vector3(0.24f, 4.2f, 0.24f), steel);
            ExpansionBox(district, "CanopyPost", new Vector3(-29.8f, 2.1f, z), new Vector3(0.24f, 4.2f, 0.24f), steel);
        }
        foreach (var signal in new[] { new Vector3(-89.5f, 0, -61), new Vector3(-55.5f, 0, -151) })
        {
            ExpansionCylinder(district, "RailSignalMast", signal + Vector3.Up * 3.2f, 0.11f, 6.4f, steelDark);
            MeshBox(district, signal + new Vector3(0, 5.7f, 0), new Vector3(0.7f, 1.6f, 0.45f), steelDark);
            district.AddChild(new OmniLight3D
            {
                Position = signal + new Vector3(0, 6.05f, -0.25f),
                LightColor = new Color(0.95f, 0.22f, 0.08f),
                LightEnergy = 1.45f,
                OmniRange = 5.5f,
                ShadowEnabled = false
            });
        }
    }

    private void BuildRailDispatchOffice(
        Node3D parent,
        Godot.Material concrete,
        Godot.Material steel,
        Godot.Material corrugated,
        Godot.Material yellow)
    {
        var center = new Vector3(-96, 0, -66);
        ExpansionBox(parent, "DispatchFloor", center + new Vector3(0, 0.08f, 0), new Vector3(16, 0.16f, 14), concrete);
        ExpansionBox(parent, "DispatchRoof", center + new Vector3(0, 3.5f, 0), new Vector3(16.8f, 0.26f, 14.8f), corrugated);
        ExpansionBox(parent, "DispatchNorth", center + new Vector3(0, 1.75f, -7), new Vector3(16, 3.5f, 0.22f), corrugated);
        ExpansionBox(parent, "DispatchWest", center + new Vector3(-8, 1.75f, 0), new Vector3(0.22f, 3.5f, 14), corrugated);
        ExpansionBox(parent, "DispatchEast", center + new Vector3(8, 1.75f, 0), new Vector3(0.22f, 3.5f, 14), corrugated);
        ExpansionBox(parent, "DispatchSouthL", center + new Vector3(-5.4f, 1.75f, 7), new Vector3(5.2f, 3.5f, 0.22f), corrugated);
        ExpansionBox(parent, "DispatchSouthR", center + new Vector3(5.4f, 1.75f, 7), new Vector3(5.2f, 3.5f, 0.22f), corrugated);
        ExpansionBox(parent, "DispatchDoorHeader", center + new Vector3(0, 3.05f, 7), new Vector3(5.6f, 0.9f, 0.22f), corrugated);
        MeshBox(parent, center + new Vector3(0, 2.65f, 7.14f), new Vector3(3.5f, 0.18f, 0.05f), yellow);
        ExpansionBox(parent, "DispatchDesk", center + new Vector3(2.7f, 0.72f, -2.2f), new Vector3(3.8f, 0.14f, 1.05f), steel);
        parent.AddChild(new OmniLight3D
        {
            Position = center + new Vector3(0, 2.95f, 0),
            LightColor = new Color(0.78f, 0.9f, 1.0f),
            LightEnergy = 1.8f,
            OmniRange = 9.5f,
            ShadowEnabled = false
        });
    }

    private void BuildRailCar(
        Node3D parent,
        Vector3 position,
        Color bodyColor,
        Godot.Material trim,
        Godot.Material rust)
    {
        var bodyMaterial = Mat($"rail_car_{bodyColor.ToHtml(false)}", bodyColor, 0.55f, 0.58f);
        var car = ExpansionBox(parent, "FreightRailCar", position + new Vector3(0, 1.5f, 0), new Vector3(2.9f, 2.45f, 12.5f), bodyMaterial);
        MeshBox(car, new Vector3(0, -1.32f, 0), new Vector3(3.15f, 0.22f, 13.0f), trim);
        foreach (var z in new[] { -4.2f, 4.2f })
        {
            foreach (var x in new[] { -1.48f, 1.48f })
            {
                car.AddChild(new MeshInstance3D
                {
                    Position = new Vector3(x, -1.5f, z),
                    Rotation = new Vector3(0, 0, Mathf.Pi / 2),
                    Mesh = new CylinderMesh { TopRadius = 0.48f, BottomRadius = 0.48f, Height = 0.18f, RadialSegments = 16 },
                    MaterialOverride = trim
                });
            }
        }
        for (var z = -5.3f; z <= 5.3f; z += 1.35f)
        {
            MeshBox(car, new Vector3(-1.48f, 0, z), new Vector3(0.07f, 2.2f, 0.08f), rust);
            MeshBox(car, new Vector3(1.48f, 0, z), new Vector3(0.07f, 2.2f, 0.08f), rust);
        }
    }

    private void BuildExpansionContainer(
        Node3D parent,
        Vector3 position,
        float yaw,
        Godot.Material material,
        Godot.Material trim)
    {
        var body = ExpansionBox(parent, "OverflowContainer", position, new Vector3(6.2f, 2.65f, 2.55f), material, new Vector3(0, yaw, 0));
        foreach (var x in new[] { -2.8f, -2.1f, -1.4f, -0.7f, 0.0f, 0.7f, 1.4f, 2.1f, 2.8f })
        {
            MeshBox(body, new Vector3(x, 0, -1.286f), new Vector3(0.055f, 2.42f, 0.045f), trim);
            MeshBox(body, new Vector3(x, 0, 1.286f), new Vector3(0.055f, 2.42f, 0.045f), trim);
        }
    }

    private void BuildTruckTrailer(
        Node3D parent,
        Vector3 position,
        float yaw,
        Godot.Material bodyMaterial,
        Godot.Material trim)
    {
        var trailer = ExpansionBox(parent, "FreightTrailer", position + new Vector3(0, 1.45f, 0), new Vector3(2.8f, 2.6f, 11.5f), bodyMaterial, new Vector3(0, yaw, 0));
        MeshBox(trailer, new Vector3(0, -1.36f, 0), new Vector3(3.0f, 0.18f, 11.8f), trim);
        foreach (var z in new[] { 3.4f, 4.55f })
        {
            foreach (var x in new[] { -1.43f, 1.43f })
            {
                trailer.AddChild(new MeshInstance3D
                {
                    Position = new Vector3(x, -1.45f, z),
                    Rotation = new Vector3(0, 0, Mathf.Pi / 2),
                    Mesh = new CylinderMesh { TopRadius = 0.47f, BottomRadius = 0.47f, Height = 0.2f, RadialSegments = 16 },
                    MaterialOverride = trim
                });
            }
        }
        foreach (var x in new[] { -1.05f, 1.05f })
        {
            MeshBox(trailer, new Vector3(x, -1.55f, -4.2f), new Vector3(0.12f, 1.25f, 0.12f), trim);
        }
    }

    private void BuildMaintenanceDistrict(
        Godot.Material concrete,
        Godot.Material steel,
        Godot.Material steelDark,
        Godot.Material yellow,
        Godot.Material corrugated)
    {
        var district = new Node3D { Name = "MaintenanceDistrict" };
        _levelRoot.AddChild(district);
        var center = new Vector3(25, 0, -89);
        ExpansionBox(district, "MaintenanceApron", center + new Vector3(0, -0.005f, 0), new Vector3(34, 0.11f, 48), concrete);
        ExpansionBox(district, "HangarFloor", center + new Vector3(0, 0.08f, -1), new Vector3(30, 0.16f, 34), concrete);
        ExpansionBox(district, "HangarRoof", center + new Vector3(0, 8.0f, -1), new Vector3(31, 0.34f, 35), corrugated);
        ExpansionBox(district, "HangarNorth", center + new Vector3(0, 4.0f, -18), new Vector3(30, 8, 0.28f), corrugated);
        ExpansionBox(district, "HangarWest", center + new Vector3(-15, 4.0f, -1), new Vector3(0.28f, 8, 34), corrugated);
        ExpansionBox(district, "HangarEast", center + new Vector3(15, 4.0f, -1), new Vector3(0.28f, 8, 34), corrugated);
        ExpansionBox(district, "HangarSouthL", center + new Vector3(-10.5f, 4.0f, 16), new Vector3(9, 8, 0.28f), corrugated);
        ExpansionBox(district, "HangarSouthR", center + new Vector3(10.5f, 4.0f, 16), new Vector3(9, 8, 0.28f), corrugated);
        ExpansionBox(district, "HangarDoorHeader", center + new Vector3(0, 7.1f, 16), new Vector3(12, 1.8f, 0.28f), corrugated);

        foreach (var x in new[] { -10.5f, 10.5f })
        {
            ExpansionBox(district, "HangarFrame", center + new Vector3(x, 4.0f, 15.65f), new Vector3(0.32f, 8, 0.42f), yellow);
        }
        ExpansionBox(district, "OverheadCraneBeam", center + new Vector3(0, 6.6f, -4), new Vector3(27, 0.42f, 0.5f), yellow);
        foreach (var x in new[] { -12.3f, 12.3f })
        {
            ExpansionBox(district, "CraneRail", center + new Vector3(x, 6.2f, -1), new Vector3(0.25f, 0.25f, 29), steel);
        }
        ExpansionBox(district, "MaintenanceBench", center + new Vector3(-9, 0.82f, -10), new Vector3(6.4f, 0.18f, 1.4f), steelDark);
        ExpansionBox(district, "MaintenanceRack", center + new Vector3(9.2f, 1.25f, -12), new Vector3(4.2f, 2.5f, 0.8f), steelDark);
        foreach (var z in new[] { -8.0f, -2.0f, 4.0f })
        {
            ExpansionBox(district, "RepairStand", center + new Vector3(-3.8f, 0.55f, z), new Vector3(2.4f, 1.1f, 1.1f), steel);
        }
        foreach (var x in new[] { -8.0f, 0.0f, 8.0f })
        {
            district.AddChild(new OmniLight3D
            {
                Position = center + new Vector3(x, 7.35f, -1),
                LightColor = new Color(0.78f, 0.9f, 1.0f),
                LightEnergy = 2.15f,
                OmniRange = 12.0f,
                ShadowEnabled = false
            });
        }
    }

    private void BuildTankFarmDistrict(
        Godot.Material concrete,
        Godot.Material steel,
        Godot.Material steelDark,
        Godot.Material rust,
        Godot.Material yellow)
    {
        var district = new Node3D { Name = "TankFarmDistrict" };
        _levelRoot.AddChild(district);
        ExpansionBox(district, "TankFarmPad", new Vector3(76, 0.0f, -91), new Vector3(62, 0.12f, 70), concrete);
        ExpansionBox(district, "TankBundNorth", new Vector3(76, 0.55f, -125.5f), new Vector3(62, 1.1f, 0.5f), concrete);
        ExpansionBox(district, "TankBundWest", new Vector3(45.2f, 0.55f, -91), new Vector3(0.5f, 1.1f, 69), concrete);
        ExpansionBox(district, "TankBundEast", new Vector3(106.8f, 0.55f, -91), new Vector3(0.5f, 1.1f, 69), concrete);
        ExpansionBox(district, "TankBundSouthL", new Vector3(58, 0.55f, -56.5f), new Vector3(26, 1.1f, 0.5f), concrete);
        ExpansionBox(district, "TankBundSouthR", new Vector3(94, 0.55f, -56.5f), new Vector3(26, 1.1f, 0.5f), concrete);

        foreach (var tank in new[]
        {
            new Vector3(59, 4.5f, -75), new Vector3(87, 4.5f, -75),
            new Vector3(59, 4.5f, -108), new Vector3(87, 4.5f, -108)
        })
        {
            ExpansionCylinder(district, "StorageTank", tank, 5.6f, 9.0f, steel);
            district.AddChild(new MeshInstance3D
            {
                Position = tank + Vector3.Up * 4.55f,
                Mesh = new CylinderMesh
                {
                    TopRadius = 0.25f,
                    BottomRadius = 5.65f,
                    Height = 1.25f,
                    RadialSegments = 24
                },
                MaterialOverride = steel
            });
            foreach (var y in new[] { -3.55f, 0.0f, 3.55f })
            {
                district.AddChild(new MeshInstance3D
                {
                    Position = tank + Vector3.Up * y,
                    Mesh = new TorusMesh { InnerRadius = 5.56f, OuterRadius = 5.68f, Rings = 36, RingSegments = 8 },
                    MaterialOverride = rust
                });
            }
            MeshBox(district, tank + new Vector3(0, -0.2f, -5.72f), new Vector3(1.8f, 2.5f, 0.08f), yellow);
        }

        for (var x = 49; x <= 102; x += 7)
        {
            ExpansionBox(district, "PipeRackPost", new Vector3(x, 2.1f, -92), new Vector3(0.28f, 4.2f, 0.28f), steelDark);
        }
        foreach (var z in new[] { -90.6f, -92.0f, -93.4f })
        {
            ExpansionCylinder(district, "TransferPipe", new Vector3(75.5f, 4.15f, z), 0.19f, 54, rust, new Vector3(0, 0, Mathf.Pi / 2));
        }
        ExpansionBox(district, "TankControlShelter", new Vector3(99, 1.75f, -118), new Vector3(10, 3.5f, 8), steelDark);
        MeshBox(district, new Vector3(94, 1.9f, -118), new Vector3(0.06f, 1.1f, 3.6f), yellow);
        district.AddChild(new OmniLight3D
        {
            Position = new Vector3(99, 4.2f, -114),
            LightColor = new Color(1.0f, 0.58f, 0.24f),
            LightEnergy = 2.35f,
            OmniRange = 14.0f,
            ShadowEnabled = false
        });
    }

    private void BuildSeawallDistrict(
        Godot.Material concrete,
        Godot.Material steel,
        Godot.Material steelDark,
        Godot.Material yellow,
        Godot.Material white,
        Godot.Material corrugated)
    {
        var district = new Node3D { Name = "SeawallDistrict" };
        _levelRoot.AddChild(district);
        ExpansionBox(district, "QuayDeck", new Vector3(42, 0.015f, -148), new Vector3(134, 0.16f, 38), concrete);
        ExpansionBox(district, "QuayFace", new Vector3(42, 1.15f, -166.5f), new Vector3(134, 2.3f, 1.0f), steelDark);

        var water = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.025f, 0.11f, 0.14f, 0.92f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            Metallic = 0.72f,
            Roughness = 0.12f
        };
        district.AddChild(new MeshInstance3D
        {
            Name = "HarborWater",
            Position = new Vector3(0, -0.28f, -205),
            Mesh = new PlaneMesh { Size = new Vector2(260, 76) },
            MaterialOverride = water
        });

        BuildQuayCrane(district, new Vector3(4, 0, -151), steel, steelDark, yellow);
        var blue = PaintedMetal("quay_container_blue", new Color(0.14f, 0.35f, 0.52f));
        var red = PaintedMetal("quay_container_red", new Color(0.52f, 0.16f, 0.1f));
        var gray = PaintedMetal("quay_container_gray", new Color(0.5f, 0.52f, 0.49f));
        BuildExpansionContainer(district, new Vector3(-16, 1.45f, -139), 0, blue, steelDark);
        BuildExpansionContainer(district, new Vector3(-16, 4.15f, -139), 0, gray, steelDark);
        BuildExpansionContainer(district, new Vector3(-3, 1.45f, -137), 0, red, steelDark);
        BuildExpansionContainer(district, new Vector3(20, 1.45f, -157), Mathf.Pi / 2, gray, steelDark);
        BuildExpansionContainer(district, new Vector3(35, 1.45f, -158), 0, blue, steelDark);

        var shelterCenter = new Vector3(44, 0, -146);
        ExpansionBox(district, "SeawallShelterFloor", shelterCenter + new Vector3(0, 0.1f, 0), new Vector3(9, 0.2f, 8), concrete);
        ExpansionBox(district, "SeawallShelterRoof", shelterCenter + new Vector3(0, 3.1f, 0), new Vector3(9.6f, 0.24f, 8.6f), corrugated);
        ExpansionBox(district, "SeawallShelterNorth", shelterCenter + new Vector3(0, 1.55f, -4), new Vector3(9, 3.1f, 0.22f), corrugated);
        ExpansionBox(district, "SeawallShelterWest", shelterCenter + new Vector3(-4.5f, 1.55f, 0), new Vector3(0.22f, 3.1f, 8), corrugated);
        ExpansionBox(district, "SeawallShelterEast", shelterCenter + new Vector3(4.5f, 1.55f, 0), new Vector3(0.22f, 3.1f, 8), corrugated);
        MeshBox(district, shelterCenter + new Vector3(0, 2.78f, 4.12f), new Vector3(4.2f, 0.18f, 0.06f), yellow);

        for (var x = -20; x <= 104; x += 8)
        {
            ExpansionCylinder(district, "QuayBollard", new Vector3(x, 0.48f, -163.5f), 0.24f, 0.9f, steelDark);
        }
        for (var x = 54; x <= 102; x += 8)
        {
            MeshBox(district, new Vector3(x, 0.13f, -134), new Vector3(4.0f, 0.03f, 0.18f), white, new Vector3(0, -0.38f, 0));
            MeshBox(district, new Vector3(x, 0.14f, -136.4f), new Vector3(4.0f, 0.03f, 0.18f), yellow, new Vector3(0, 0.38f, 0));
        }
    }

    private void BuildQuayCrane(
        Node3D parent,
        Vector3 origin,
        Godot.Material steel,
        Godot.Material steelDark,
        Godot.Material yellow)
    {
        foreach (var x in new[] { -8.0f, 8.0f })
        {
            ExpansionBox(parent, "QuayCraneLeg", origin + new Vector3(x, 7.5f, 0), new Vector3(0.9f, 15, 0.9f), steelDark, new Vector3(0, 0, x < 0 ? -0.08f : 0.08f));
        }
        ExpansionBox(parent, "QuayCraneBridge", origin + new Vector3(0, 14.5f, 0), new Vector3(20, 0.8f, 1.0f), yellow);
        ExpansionBox(parent, "QuayCraneBoom", origin + new Vector3(7, 15.3f, -6), new Vector3(34, 0.5f, 0.62f), steel, new Vector3(0, 0.08f, -0.03f));
        ExpansionBox(parent, "QuayCraneCab", origin + new Vector3(-5, 12.7f, -0.7f), new Vector3(3.4f, 2.3f, 2.6f), steelDark);
        MeshBox(parent, origin + new Vector3(-5, 13.0f, -2.04f), new Vector3(2.5f, 1.1f, 0.05f), yellow);
    }

    private void BuildExpansionCover(Godot.Material concrete)
    {
        const string barrierPath = "res://assets/models/concrete_road_barrier/concrete_road_barrier.gltf";
        foreach (var group in new (Vector3 Center, float Angle, int Count)[]
        {
            (new Vector3(-19, 0.02f, -54), 0, 3),
            (new Vector3(19, 0.02f, -54), 0, 3),
            (new Vector3(-39, 0.02f, -76), Mathf.Pi / 2, 3),
            (new Vector3(-28, 0.02f, -119), 0.12f, 3),
            (new Vector3(13, 0.02f, -118), Mathf.Pi / 2, 2),
            (new Vector3(42, 0.02f, -122), 0, 3),
            (new Vector3(72, 0.02f, -126), 0, 3),
            (new Vector3(101, 0.02f, -134), Mathf.Pi / 2, 3),
            (new Vector3(55, 0.02f, -157), 0.15f, 2),
            (new Vector3(-78, 0.02f, -72), 0.25f, 3),
            (new Vector3(-58, 0.02f, -103), Mathf.Pi / 2, 3),
            (new Vector3(-17, 0.02f, -98), 0.15f, 3),
            (new Vector3(62, 0.02f, -82), Mathf.Pi / 2, 3),
            (new Vector3(87, 0.02f, -93), 0, 3),
            (new Vector3(-74, 0.02f, -148), 0, 3),
            (new Vector3(7, 0.02f, -146), Mathf.Pi / 2, 3),
            (new Vector3(82, 0.02f, -141), 0.12f, 3),
            (new Vector3(-4.5f, 0.02f, -69), 0, 2),
            (new Vector3(-4.5f, 0.02f, -103), 0, 2),
            (new Vector3(-4.5f, 0.02f, -136), 0, 2)
        })
        {
            for (var i = 0; i < group.Count; i++)
            {
                var spacing = (i - (group.Count - 1) * 0.5f) * 1.78f;
                var offset = Vector3.Right.Rotated(Vector3.Up, group.Angle) * spacing;
                ModelProp(barrierPath, group.Center + offset, group.Angle, 1.18f, new Vector3(1.55f, 0.84f, 0.64f), new Vector3(0, 0.41f, 0));
            }
        }

        const string cratePath = "res://assets/models/old_military_crate/old_military_crate.gltf";
        var cratePositions = new[]
        {
            new Vector3(-93, 0.02f, -60), new Vector3(-71, 0.02f, -105),
            new Vector3(-39, 0.02f, -144), new Vector3(14, 0.02f, -74),
            new Vector3(35, 0.02f, -108), new Vector3(51, 0.02f, -61),
            new Vector3(95, 0.02f, -119), new Vector3(30, 0.02f, -135),
            new Vector3(58, 0.02f, -154), new Vector3(96, 0.02f, -143)
        };
        for (var i = 0; i < cratePositions.Length; i++)
        {
            ModelProp(cratePath, cratePositions[i], i * 0.31f, 1.5f, new Vector3(0.82f, 0.42f, 0.68f), new Vector3(-0.06f, 0.21f, 0.1f));
            if (i % 2 == 0)
            {
                ModelProp(cratePath, cratePositions[i] + new Vector3(0.08f, 0.64f, -0.05f), -0.18f + i * 0.17f, 1.42f, new Vector3(0.82f, 0.42f, 0.68f), new Vector3(-0.06f, 0.21f, 0.1f));
            }
        }

        BuildHescoCluster(new Vector3(-84, 0, -84), 0.22f, 3);
        BuildHescoCluster(new Vector3(-43, 0, -82), Mathf.Pi / 2, 3);
        BuildHescoCluster(new Vector3(-17, 0, -115), -0.18f, 3);
        BuildHescoCluster(new Vector3(24, 0, -112), Mathf.Pi / 2, 3);
        BuildHescoCluster(new Vector3(73, 0, -90), 0.14f, 3);
        BuildHescoCluster(new Vector3(69, 0, -145), Mathf.Pi / 2, 3);
        BuildHescoCluster(new Vector3(4.6f, 0, -86), 0, 2);
        BuildHescoCluster(new Vector3(4.6f, 0, -120), 0, 2);
        BuildHescoCluster(new Vector3(4.6f, 0, -151), 0, 2);
        BuildPipeBundle(new Vector3(-52, 0, -91));
        BuildPipeBundle(new Vector3(-7, 0, -126));
        BuildPipeBundle(new Vector3(41, 0, -92));
        BuildPipeBundle(new Vector3(92, 0, -118));
        BuildServiceTruck(new Vector3(-34, 0, -106), concrete);
        BuildServiceTruck(new Vector3(80, 0, -70), concrete);
    }

    private void BuildExpansionLights()
    {
        var root = new Node3D { Name = "ExpansionLighting" };
        _levelRoot.AddChild(root);
        var pole = Mat("expansion_pole", new Color(0.055f, 0.068f, 0.068f), 0.86f, 0.3f);
        var lamp = Mat("expansion_lamp", new Color(0.78f, 0.76f, 0.62f), 0.12f, 0.22f, new Color(0.92f, 0.72f, 0.42f));
        foreach (var position in new[]
        {
            new Vector3(-99, 0, 42), new Vector3(-58, 0, 40), new Vector3(58, 0, 41), new Vector3(99, 0, 39),
            new Vector3(-24, 0, -51), new Vector3(8, 0, -62), new Vector3(-86, 0, -102), new Vector3(-42, 0, -137),
            new Vector3(9, 0, -112), new Vector3(43, 0, -58), new Vector3(101, 0, -91), new Vector3(18, 0, -148)
        })
        {
            ExpansionCylinder(root, "ExpansionLightPole", position + Vector3.Up * 5.0f, 0.1f, 10, pole);
            MeshBox(root, position + new Vector3(0, 9.82f, 0), new Vector3(0.9f, 0.2f, 0.46f), lamp);
            root.AddChild(new SpotLight3D
            {
                Position = position + new Vector3(0, 9.6f, 0),
                RotationDegrees = new Vector3(-90, 0, 0),
                LightColor = new Color(1.0f, 0.74f, 0.44f),
                LightEnergy = 4.2f,
                SpotRange = 26.0f,
                SpotAngle = 51.0f,
                ShadowEnabled = false
            });
        }
    }

    private void BuildExtraction(
        Godot.Material concrete,
        Godot.Material steel,
        Godot.Material yellow,
        Godot.Material white)
    {
        var site = new Node3D
        {
            Name = "ExtractionSite",
            Position = ExtractionPoint
        };
        _levelRoot.AddChild(site);
        ExpansionCylinder(site, "ExtractionPad", new Vector3(0, 0.14f, 0), 9.4f, 0.28f, concrete);
        ExpansionCylinder(site, "ExtractionPadCurb", new Vector3(0, 0.1f, 0), 10.0f, 0.2f, steel);
        site.MoveChild(site.GetNode("ExtractionPad"), site.GetChildCount() - 1);
        MeshBox(site, new Vector3(-2.6f, 0.31f, 0), new Vector3(0.72f, 0.035f, 6.2f), white);
        MeshBox(site, new Vector3(2.6f, 0.31f, 0), new Vector3(0.72f, 0.035f, 6.2f), white);
        MeshBox(site, new Vector3(0, 0.315f, 0), new Vector3(5.4f, 0.036f, 0.72f), white);
        foreach (var yaw in new[] { -0.72f, 0.72f })
        {
            MeshBox(site, new Vector3(0, 0.32f, 7.6f), new Vector3(0.45f, 0.038f, 4.0f), yellow, new Vector3(0, yaw, 0));
        }

        _extractionArea = new Area3D
        {
            Name = "ExtractionZone",
            Position = new Vector3(0, 0.9f, 0),
            CollisionLayer = 0,
            CollisionMask = 1,
            Monitoring = true
        };
        _extractionArea.AddChild(new CollisionShape3D
        {
            Name = "ExtractionVolume",
            Shape = new CylinderShape3D { Radius = 7.0f, Height = 2.0f }
        });
        _extractionArea.BodyEntered += OnExtractionEntered;
        site.AddChild(_extractionArea);

        var markerMaterial = new StandardMaterial3D
        {
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = new Color(0.06f, 0.92f, 0.6f, 0.26f),
            EmissionEnabled = true,
            Emission = new Color(0.06f, 0.92f, 0.6f),
            EmissionEnergyMultiplier = 3.2f
        };
        _extractionMarker = new Node3D
        {
            Name = "ActiveExtractionBeacon",
            Position = new Vector3(0, 0.34f, 0)
        };
        site.AddChild(_extractionMarker);
        foreach (var radius in new[] { 6.1f, 7.1f, 8.1f })
        {
            _extractionMarker.AddChild(new MeshInstance3D
            {
                Mesh = new TorusMesh { InnerRadius = radius, OuterRadius = radius + 0.055f, Rings = 64, RingSegments = 10 },
                MaterialOverride = markerMaterial
            });
        }
        _extractionMarker.AddChild(new MeshInstance3D
        {
            Position = new Vector3(0, 10, 0),
            Mesh = new CylinderMesh { TopRadius = 0.14f, BottomRadius = 0.32f, Height = 20, RadialSegments = 12 },
            MaterialOverride = markerMaterial
        });
        foreach (var angle in new[] { 0.0f, Mathf.Pi / 2, Mathf.Pi, Mathf.Pi * 1.5f })
        {
            var radial = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * 7.3f;
            _extractionMarker.AddChild(new MeshInstance3D
            {
                Position = radial + Vector3.Up * 0.24f,
                Mesh = new CylinderMesh { TopRadius = 0.16f, BottomRadius = 0.16f, Height = 0.28f, RadialSegments = 12 },
                MaterialOverride = markerMaterial
            });
            _extractionMarker.AddChild(new OmniLight3D
            {
                Position = radial + Vector3.Up * 0.55f,
                LightColor = new Color(0.08f, 1.0f, 0.64f),
                LightEnergy = 2.4f,
                OmniRange = 7.5f,
                ShadowEnabled = false
            });
        }
        MeshBox(_extractionMarker, new Vector3(0, 11.5f, 0), new Vector3(12, 0.16f, 0.16f), markerMaterial);

        ExpansionCylinder(site, "WindsockPole", new Vector3(10.8f, 4.2f, 1.5f), 0.09f, 8.4f, steel);
        site.AddChild(new MeshInstance3D
        {
            Position = new Vector3(11.7f, 7.9f, 1.5f),
            Rotation = new Vector3(0, 0, Mathf.Pi / 2),
            Mesh = new CylinderMesh { TopRadius = 0.14f, BottomRadius = 0.48f, Height = 1.8f, RadialSegments = 18 },
            MaterialOverride = yellow
        });
        ExpansionBox(site, "BeaconEquipmentShelter", new Vector3(-12.0f, 1.4f, 3.5f), new Vector3(4.6f, 2.8f, 5.6f), steel);
        MeshBox(site, new Vector3(-9.66f, 1.55f, 3.5f), new Vector3(0.05f, 1.55f, 2.6f), yellow);
    }

    private StaticBody3D ExpansionBox(
        Node3D parent,
        string name,
        Vector3 position,
        Vector3 size,
        Godot.Material material,
        Vector3 rotation = default)
    {
        var body = new StaticBody3D
        {
            Name = name,
            Position = position,
            Rotation = rotation,
            CollisionLayer = 1,
            CollisionMask = 0
        };
        body.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = size }, MaterialOverride = material });
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        parent.AddChild(body);
        return body;
    }

    private StaticBody3D ExpansionCylinder(
        Node3D parent,
        string name,
        Vector3 position,
        float radius,
        float height,
        Godot.Material material,
        Vector3 rotation = default)
    {
        var body = new StaticBody3D
        {
            Name = name,
            Position = position,
            Rotation = rotation,
            CollisionLayer = 1,
            CollisionMask = 0
        };
        body.AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = radius, BottomRadius = radius, Height = height, RadialSegments = 24 },
            MaterialOverride = material
        });
        body.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = radius, Height = height } });
        parent.AddChild(body);
        return body;
    }
}
