using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private StandardMaterial3D Mat(
        string id,
        Color color,
        float metallic = 0.0f,
        float roughness = 0.75f,
        Color emission = default)
    {
        if (_materials.TryGetValue(id, out var cached))
        {
            return cached;
        }
        var material = new StandardMaterial3D
        {
            AlbedoColor = color,
            Metallic = metallic,
            Roughness = roughness
        };
        if (emission != default)
        {
            material.EmissionEnabled = true;
            material.Emission = emission;
            material.EmissionEnergyMultiplier = 2.4f;
        }
        _materials[id] = material;
        return material;
    }

    private StandardMaterial3D GroundMaterial(string id, Color baseColor, float roughness)
    {
        if (_materials.TryGetValue(id, out var cached))
        {
            return cached;
        }
        var asset = id switch
        {
            "asphalt" => "asphalt_03",
            "gravel" => "gravel_embedded_concrete",
            _ => "concrete_floor"
        };
        var root = $"res://assets/textures/{asset}";
        var material = new StandardMaterial3D
        {
            AlbedoColor = baseColor,
            AlbedoTexture = GD.Load<Texture2D>(root + "_diff_1k.jpg"),
            NormalEnabled = true,
            NormalTexture = GD.Load<Texture2D>(root + "_normal_1k.jpg"),
            NormalScale = 0.75f,
            Roughness = roughness,
            RoughnessTexture = GD.Load<Texture2D>(root + "_rough_1k.jpg"),
            Uv1Triplanar = true,
            Uv1WorldTriplanar = true,
            Uv1Scale = Vector3.One * 0.24f,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic
        };
        _materials[id] = material;
        return material;
    }

    private StandardMaterial3D PaintedMetal(string id, Color tint)
    {
        if (_materials.TryGetValue(id, out var cached))
        {
            return cached;
        }
        const string root = "res://assets/textures/rusty_painted_metal";
        var material = new StandardMaterial3D
        {
            AlbedoColor = tint,
            AlbedoTexture = GD.Load<Texture2D>(root + "_diff_1k.jpg"),
            NormalEnabled = true,
            NormalTexture = GD.Load<Texture2D>(root + "_normal_1k.jpg"),
            NormalScale = 0.9f,
            Roughness = 0.58f,
            RoughnessTexture = GD.Load<Texture2D>(root + "_rough_1k.jpg"),
            Metallic = 0.55f,
            Uv1Triplanar = true,
            Uv1WorldTriplanar = true,
            Uv1Scale = Vector3.One * 0.45f,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic
        };
        _materials[id] = material;
        return material;
    }

    private void BuildEnvironment()
    {
        _environmentRef = new Environment
        {
            BackgroundMode = Environment.BGMode.Sky,
            AmbientLightSource = Environment.AmbientSource.Sky,
            AmbientLightEnergy = 0.86f,
            ReflectedLightSource = Environment.ReflectionSource.Sky,
            TonemapMode = Environment.ToneMapper.Aces,
            TonemapExposure = 1.1f,
            FogEnabled = true,
            FogLightColor = new Color(0.46f, 0.58f, 0.62f),
            FogLightEnergy = 0.46f,
            FogDensity = 0.00125f,
            FogHeight = -1.5f,
            FogHeightDensity = 0.035f,
            FogSkyAffect = 0.16f
        };
        var skyMaterial = new ProceduralSkyMaterial
        {
            SkyTopColor = new Color(0.055f, 0.16f, 0.28f),
            SkyHorizonColor = new Color(0.46f, 0.61f, 0.68f),
            GroundBottomColor = new Color(0.035f, 0.045f, 0.045f),
            GroundHorizonColor = new Color(0.25f, 0.32f, 0.31f),
            SunAngleMax = 5.0f,
            SunCurve = 0.08f
        };
        _environmentRef.Sky = new Sky { SkyMaterial = skyMaterial };
        SetIfSupported(_environmentRef, "glow_enabled", true);
        SetIfSupported(_environmentRef, "ssao_enabled", true);
        SetIfSupported(_environmentRef, "ssao_radius", 2.1f);
        SetIfSupported(_environmentRef, "ssao_intensity", 1.65f);
        SetIfSupported(_environmentRef, "ssil_enabled", true);
        SetIfSupported(_environmentRef, "ssr_enabled", true);
        SetIfSupported(_environmentRef, "volumetric_fog_enabled", true);
        SetIfSupported(_environmentRef, "volumetric_fog_density", 0.0032f);
        SetIfSupported(_environmentRef, "volumetric_fog_ambient_inject", 0.35f);
        SetIfSupported(_environmentRef, "adjustment_enabled", true);
        SetIfSupported(_environmentRef, "adjustment_brightness", 1.03f);
        SetIfSupported(_environmentRef, "adjustment_contrast", 1.05f);
        SetIfSupported(_environmentRef, "adjustment_saturation", 1.12f);
        AddChild(new WorldEnvironment { Environment = _environmentRef });

        _sunLight = new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-48, -28, 0),
            LightColor = new Color(1.0f, 0.9f, 0.72f),
            LightEnergy = 1.25f,
            ShadowEnabled = true,
            DirectionalShadowMaxDistance = 240.0f,
            DirectionalShadowMode = DirectionalLight3D.ShadowMode.Parallel4Splits
        };
        AddChild(_sunLight);
        AddChild(new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-25, 145, 0),
            LightColor = new Color(0.3f, 0.48f, 0.72f),
            LightEnergy = 0.34f,
            ShadowEnabled = false
        });
        AddDust();
    }

    private static void SetIfSupported(GodotObject target, string propertyName, Variant value)
    {
        foreach (var property in target.GetPropertyList())
        {
            if (property["name"].AsString() == propertyName)
            {
                target.Set(propertyName, value);
                return;
            }
        }
    }

    private void AddDust()
    {
        var process = new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
            EmissionBoxExtents = new Vector3(108, 7, 108),
            Gravity = new Vector3(0.08f, 0.025f, 0.04f),
            InitialVelocityMin = 0.03f,
            InitialVelocityMax = 0.12f,
            ScaleMin = 0.03f,
            ScaleMax = 0.09f
        };
        var quad = new QuadMesh
        {
            Size = new Vector2(0.04f, 0.04f),
            Material = new StandardMaterial3D
            {
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
                AlbedoColor = new Color(0.74f, 0.67f, 0.54f, 0.11f)
            }
        };
        AddChild(new GpuParticles3D
        {
            Amount = 220,
            Lifetime = 9.0,
            VisibilityAabb = new Aabb(new Vector3(-112, -2, -112), new Vector3(224, 20, 224)),
            ProcessMaterial = process,
            DrawPass1 = quad,
            Position = new Vector3(0, 4, MapCenterZ)
        });
    }

    private void BuildLevel()
    {
        _levelRoot = new Node3D { Name = "FreightTerminal" };
        AddChild(_levelRoot);
        var asphalt = GroundMaterial("asphalt", new Color(0.45f, 0.49f, 0.5f), 0.88f);
        var concrete = GroundMaterial("concrete", new Color(0.66f, 0.67f, 0.61f), 0.82f);
        var concreteDark = Mat("concrete_dark", new Color(0.16f, 0.175f, 0.17f), 0.05f, 0.87f);
        var steel = Mat("steel", new Color(0.115f, 0.14f, 0.14f), 0.78f, 0.32f);
        var steelDark = Mat("steel_dark", new Color(0.035f, 0.048f, 0.05f), 0.83f, 0.28f);
        var rust = Mat("rust", new Color(0.31f, 0.105f, 0.055f), 0.48f, 0.73f);
        var yellow = Mat("warning", new Color(0.74f, 0.51f, 0.055f), 0.22f, 0.55f);
        var white = Mat("marking", new Color(0.72f, 0.75f, 0.69f), 0.05f, 0.72f);

        StaticBox("Ground", new Vector3(0, -0.55f, MapCenterZ), new Vector3(MapWidthMeters, 1, MapDepthMeters), asphalt);
        StaticBox("NorthPerimeter", new Vector3(0, 1.25f, -170), new Vector3(MapWidthMeters, 3, 1), concreteDark);
        StaticBox("WestPerimeter", new Vector3(-110, 1.25f, MapCenterZ), new Vector3(1, 3, MapDepthMeters), concreteDark);
        StaticBox("EastPerimeter", new Vector3(110, 1.25f, MapCenterZ), new Vector3(1, 3, MapDepthMeters), concreteDark);
        StaticBox("SouthPerimeterL", new Vector3(-58, 1.25f, 50), new Vector3(104, 3, 1), concreteDark);
        StaticBox("SouthPerimeterR", new Vector3(58, 1.25f, 50), new Vector3(104, 3, 1), concreteDark);
        for (var x = -35; x <= 35; x += 10)
        {
            MeshBox(_levelRoot, new Vector3(x, 0.012f, 5), new Vector3(0.12f, 0.025f, 7), white);
        }
        foreach (var z in new[] { -31.0f, -12.0f, 26.0f })
        {
            MeshBox(_levelRoot, new Vector3(0, 0.018f, z), new Vector3(1.2f, 0.03f, 0.12f), yellow);
            for (var x = -36; x <= 36; x += 4)
            {
                MeshBox(_levelRoot, new Vector3(x, 0.019f, z), new Vector3(1.8f, 0.031f, 0.13f), yellow);
            }
        }

        BuildWarehouse(concrete, steel, steelDark, yellow);
        var interiorWall = Mat("loot_room_wall", new Color(0.38f, 0.41f, 0.39f), 0.04f, 0.9f);
        var interiorTrim = Mat("loot_room_trim", new Color(0.15f, 0.18f, 0.18f), 0.55f, 0.48f);
        BuildLootRooms(concrete, interiorWall, interiorTrim, yellow);
        BuildContainerYard(steelDark);
        BuildCraneAndPipes(steel, steelDark, rust, yellow);
        BuildSecurityCheckpoint(concrete, steelDark, yellow);
        BuildFuelDepot(concrete, steel, steelDark, rust, yellow);
        BuildBarracks(concrete, interiorWall, interiorTrim, yellow);
        BuildCover(concreteDark);
        BuildHarborExpansion(asphalt, concrete, concreteDark, steel, steelDark, rust, yellow, white);
        BuildBackground(concreteDark, steel);
        AddPuddles();
        AddLightPoles();
        BuildMissionTerminals();
        BuildExtraction(concrete, steelDark, yellow, white);
        _extractionMarker.Visible = false;
    }

    private void BuildLootRooms(Godot.Material floor, Godot.Material wall, Godot.Material trim, Godot.Material marker)
    {
        BuildSecureRoom("ArmoryRoom", new Vector3(33.0f, 0, -18.0f), new Vector2(6.6f, 6.2f), floor, wall, trim, marker);
        BuildSecureRoom("CustomsOffice", new Vector3(-34.0f, 0, -11.0f), new Vector2(7.2f, 6.4f), floor, wall, trim, marker);
        BuildSecureRoom("MaintenanceRoom", new Vector3(13.0f, 0, 11.5f), new Vector2(6.0f, 6.8f), floor, wall, trim, marker);
    }

    private void BuildSecureRoom(
        string name,
        Vector3 center,
        Vector2 size,
        Godot.Material floor,
        Godot.Material wall,
        Godot.Material trim,
        Godot.Material marker)
    {
        const float height = 3.2f;
        const float thickness = 0.18f;
        const float doorway = 1.65f;
        StaticBox(name + "Floor", center + new Vector3(0, 0.05f, 0), new Vector3(size.X, 0.12f, size.Y), floor);
        StaticBox(name + "Roof", center + new Vector3(0, height, 0), new Vector3(size.X, 0.16f, size.Y), wall);
        StaticBox(name + "North", center + new Vector3(0, height * 0.5f, -size.Y * 0.5f), new Vector3(size.X, height, thickness), wall);
        StaticBox(name + "West", center + new Vector3(-size.X * 0.5f, height * 0.5f, 0), new Vector3(thickness, height, size.Y), wall);
        StaticBox(name + "East", center + new Vector3(size.X * 0.5f, height * 0.5f, 0), new Vector3(thickness, height, size.Y), wall);
        var segmentWidth = (size.X - doorway) * 0.5f;
        StaticBox(name + "SouthL", center + new Vector3(-(doorway + segmentWidth) * 0.5f, height * 0.5f, size.Y * 0.5f), new Vector3(segmentWidth, height, thickness), wall);
        StaticBox(name + "SouthR", center + new Vector3((doorway + segmentWidth) * 0.5f, height * 0.5f, size.Y * 0.5f), new Vector3(segmentWidth, height, thickness), wall);
        StaticBox(name + "DoorHeader", center + new Vector3(0, 2.75f, size.Y * 0.5f), new Vector3(doorway, 0.9f, thickness), wall);
        MeshBox(_levelRoot, center + new Vector3(0, 2.55f, size.Y * 0.5f + 0.11f), new Vector3(1.2f, 0.16f, 0.04f), marker);
        var lightColor = name switch
        {
            "ArmoryRoom" => new Color(0.72f, 0.86f, 1.0f),
            "MaintenanceRoom" => new Color(1.0f, 0.72f, 0.42f),
            _ => new Color(0.88f, 0.94f, 0.88f)
        };
        _levelRoot.AddChild(new OmniLight3D
        {
            Position = center + new Vector3(0, 2.78f, -size.Y * 0.12f),
            LightColor = lightColor,
            LightEnergy = 2.15f,
            OmniRange = Mathf.Max(size.X, size.Y) * 1.08f,
            ShadowEnabled = false
        });
        _levelRoot.AddChild(new OmniLight3D
        {
            Position = center + new Vector3(0, 2.72f, size.Y * 0.28f),
            LightColor = lightColor.Lerp(Colors.White, 0.35f),
            LightEnergy = 0.72f,
            OmniRange = Mathf.Max(size.X, size.Y) * 0.72f,
            ShadowEnabled = false
        });
        var lamp = new StandardMaterial3D
        {
            AlbedoColor = lightColor.Lerp(Colors.White, 0.42f),
            EmissionEnabled = true,
            Emission = lightColor,
            EmissionEnergyMultiplier = 0.32f,
            Roughness = 0.38f
        };
        MeshBox(_levelRoot, center + new Vector3(0, 3.0f, 0), new Vector3(1.5f, 0.06f, 0.48f), lamp);
        BuildSecureRoomProps(name, center, size);
    }

    private void BuildSecureRoomProps(string name, Vector3 center, Vector2 size)
    {
        var frame = Mat("room_fixture", new Color(0.105f, 0.125f, 0.12f), 0.68f, 0.46f);
        var rackPanel = Mat("room_rack_panel", new Color(0.3f, 0.325f, 0.3f), 0.02f, 0.92f);
        var rackRifle = Mat("room_rack_rifle", new Color(0.28f, 0.245f, 0.17f), 0.42f, 0.52f);
        var worktop = Mat("room_worktop", new Color(0.24f, 0.25f, 0.22f), 0.28f, 0.68f);
        var caseGreen = Mat("room_case_green", new Color(0.16f, 0.22f, 0.15f), 0.14f, 0.78f);
        var folder = Mat("room_folder", new Color(0.54f, 0.39f, 0.11f), 0.02f, 0.86f);
        var backZ = center.Z - size.Y * 0.5f + 0.28f;

        if (name == "ArmoryRoom")
        {
            MeshBox(_levelRoot, new Vector3(center.X, 1.45f, backZ), new Vector3(size.X - 0.7f, 2.35f, 0.08f), rackPanel);
            for (var x = -2.25f; x <= 2.25f; x += 0.75f)
            {
                MeshBox(_levelRoot, center + new Vector3(x, 1.5f, -size.Y * 0.5f + 0.34f), new Vector3(0.035f, 2.05f, 0.025f), worktop);
            }
            for (var x = -2.45f; x <= 2.45f; x += 0.35f)
            {
                for (var y = 0.42f; y <= 2.42f; y += 0.34f)
                {
                    MeshBox(_levelRoot, center + new Vector3(x, y, -size.Y * 0.5f + 0.335f), new Vector3(0.045f, 0.045f, 0.018f), frame);
                }
            }
            for (var row = 0; row < 3; row++)
            {
                var rackPosition = center + new Vector3(-0.35f + row * 0.2f, 0.78f + row * 0.62f, -size.Y * 0.5f + 0.36f);
                MeshBox(_levelRoot, rackPosition, new Vector3(3.55f, 0.1f, 0.11f), rackRifle, new Vector3(0, 0, -0.04f + row * 0.04f));
                MeshBox(_levelRoot, rackPosition + new Vector3(1.45f, -0.18f, 0.0f), new Vector3(0.7f, 0.22f, 0.13f), rackRifle);
                MeshBox(_levelRoot, rackPosition + new Vector3(-1.72f, 0.0f, 0.0f), new Vector3(0.52f, 0.055f, 0.07f), rackRifle);
            }
            StaticBox("ArmoryBench", center + new Vector3(size.X * 0.28f, 0.58f, -size.Y * 0.32f), new Vector3(2.25f, 0.12f, 0.7f), worktop);
            for (var x = -0.72f; x <= 0.72f; x += 0.72f)
            {
                MeshBox(_levelRoot, center + new Vector3(size.X * 0.28f + x, 0.34f, -size.Y * 0.32f), new Vector3(0.56f, 0.38f, 0.52f), caseGreen);
            }
            return;
        }

        if (name == "CustomsOffice")
        {
            StaticBox("CustomsDesk", center + new Vector3(1.65f, 0.72f, -1.55f), new Vector3(2.4f, 0.12f, 0.82f), worktop);
            MeshBox(_levelRoot, center + new Vector3(1.65f, 0.36f, -1.55f), new Vector3(1.92f, 0.6f, 0.62f), frame);
            for (var y = 0.44f; y <= 1.72f; y += 0.43f)
            {
                MeshBox(_levelRoot, center + new Vector3(-size.X * 0.38f, y, -size.Y * 0.32f), new Vector3(0.72f, 0.36f, 0.5f), frame);
                MeshBox(_levelRoot, center + new Vector3(-size.X * 0.38f, y, -size.Y * 0.32f - 0.26f), new Vector3(0.34f, 0.025f, 0.2f), folder);
            }
            return;
        }

        StaticBox("MaintenanceBench", center + new Vector3(0, 0.76f, -size.Y * 0.33f), new Vector3(3.8f, 0.14f, 0.78f), worktop);
        for (var x = -1.55f; x <= 1.55f; x += 0.62f)
        {
            MeshBox(_levelRoot, center + new Vector3(x, 1.48f, -size.Y * 0.5f + 0.22f), new Vector3(0.08f, 0.72f, 0.12f), caseGreen);
        }
        MeshBox(_levelRoot, center + new Vector3(-2.35f, 1.2f, 0.35f), new Vector3(0.72f, 1.82f, 0.58f), frame);
    }

    private void BuildMissionTerminals()
    {
        BuildObjectiveTerminal("RelayTerminal", new Vector3(35.5f, 0, -10), Mathf.Pi / 2, true);
        BuildObjectiveTerminal("ManifestTerminal", new Vector3(-31, 0, -7), -Mathf.Pi / 2, false);
    }

    private void BuildObjectiveTerminal(string nodeName, Vector3 position, float yaw, bool relay)
    {
        var terminal = new Node3D
        {
            Name = nodeName,
            Position = position,
            Rotation = new Vector3(0, yaw, 0)
        };
        _levelRoot.AddChild(terminal);
        var shell = Mat("terminal_shell", new Color(0.045f, 0.06f, 0.061f), 0.72f, 0.3f);
        var trim = Mat("terminal_trim", new Color(0.22f, 0.24f, 0.21f), 0.8f, 0.26f);
        MeshBox(terminal, new Vector3(0, 0.5f, 0), new Vector3(0.82f, 1, 0.42f), shell);
        var body = new StaticBody3D
        {
            Position = new Vector3(0, 0.5f, 0),
            CollisionLayer = 1,
            CollisionMask = 0
        };
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(0.82f, 1, 0.42f) } });
        terminal.AddChild(body);
        MeshBox(terminal, new Vector3(0, 1.05f, -0.08f), new Vector3(0.96f, 0.16f, 0.48f), trim);
        var screen = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.9f, 0.28f, 0.045f),
            EmissionEnabled = true,
            Emission = new Color(0.95f, 0.18f, 0.025f),
            EmissionEnergyMultiplier = 1.8f,
            Roughness = 0.2f
        };
        MeshBox(terminal, new Vector3(0, 1.32f, -0.25f), new Vector3(0.68f, 0.38f, 0.035f), screen);
        var statusLight = new OmniLight3D
        {
            Position = new Vector3(0, 1.5f, -0.34f),
            LightColor = new Color(1.0f, 0.18f, 0.04f),
            LightEnergy = 0.85f,
            OmniRange = 1.6f
        };
        terminal.AddChild(statusLight);
        if (relay)
        {
            MeshBox(terminal, new Vector3(0, 2.15f, 0.08f), new Vector3(0.09f, 1.7f, 0.09f), trim);
            MeshBox(terminal, new Vector3(0, 3.0f, 0.08f), new Vector3(0.72f, 0.08f, 0.08f), trim);
        }
        _objectiveTerminals.Add(terminal);
        _objectiveScreens.Add(screen);
        _objectiveLights.Add(statusLight);
    }

    private void BuildWarehouse(Godot.Material concrete, Godot.Material steel, Godot.Material dark, Godot.Material yellow)
    {
        StaticBox("WarehouseFloor", new Vector3(23.5f, 0.08f, -5), new Vector3(31, 0.18f, 42), concrete);
        StaticBox("WarehouseEast", new Vector3(39, 4.5f, -5), new Vector3(0.6f, 9, 42), dark);
        StaticBox("WarehouseNorth", new Vector3(23.5f, 4.5f, -26), new Vector3(31, 9, 0.6f), dark);
        StaticBox("WarehouseSouthA", new Vector3(33.5f, 4.5f, 16), new Vector3(11, 9, 0.6f), dark);
        StaticBox("WarehouseSouthB", new Vector3(16, 4.5f, 16), new Vector3(10, 9, 0.6f), dark);
        StaticBox("WarehouseRoof", new Vector3(23.5f, 9, -5), new Vector3(31, 0.35f, 42), steel);
        for (var z = -23; z < 15; z += 6)
        {
            StaticBox("WarehouseColumn", new Vector3(8.2f, 3.2f, z), new Vector3(0.55f, 6.4f, 0.55f), steel);
            MeshBox(_levelRoot, new Vector3(23.5f, 8.65f, z), new Vector3(30.5f, 0.18f, 0.3f), yellow);
        }
        foreach (var z in new[] { -18.0f, -3.0f, 11.0f })
        {
            StaticBox("WarehouseCrate", new Vector3(28, 0.65f, z), new Vector3(4, 1.3f, 2.3f), Mat("crate", new Color(0.25f, 0.19f, 0.11f), 0.05f, 0.85f));
        }
        StaticBox("LoadingDock", new Vector3(9.7f, 0.8f, -7), new Vector3(4, 1.6f, 21), concrete);
        StaticBox("LoadingRamp", new Vector3(7.7f, 0.35f, 4.5f), new Vector3(4.2f, 0.4f, 5), concrete, new Vector3(0, 0, Mathf.DegToRad(-8)));
    }

    private void BuildContainerYard(Godot.Material dark)
    {
        var blue = PaintedMetal("container_blue", new Color(0.22f, 0.52f, 0.68f));
        var red = PaintedMetal("container_red", new Color(0.68f, 0.22f, 0.14f));
        var green = PaintedMetal("container_green", new Color(0.24f, 0.56f, 0.36f));
        var gray = PaintedMetal("container_gray", new Color(0.65f, 0.68f, 0.65f));
        Container(new Vector3(-16, 1.35f, -24), Vector3.Zero, blue, dark);
        Container(new Vector3(-16, 4.05f, -24), Vector3.Zero, red, dark);
        Container(new Vector3(-27, 1.35f, -17), new Vector3(0, Mathf.Pi / 2, 0), green, dark);
        Container(new Vector3(-27, 4.05f, -17), new Vector3(0, Mathf.Pi / 2, 0), gray, dark);
        Container(new Vector3(-18, 1.35f, -5), new Vector3(0, 0.12f, 0), red, dark);
        Container(new Vector3(-29, 1.35f, 3), new Vector3(0, Mathf.Pi / 2, 0), blue, dark);
        Container(new Vector3(-15, 1.35f, 12), new Vector3(0, Mathf.Pi / 2, 0), gray, dark);
        Container(new Vector3(-15, 4.05f, 12), new Vector3(0, Mathf.Pi / 2, 0), green, dark);
        Container(new Vector3(-28, 1.35f, 24), new Vector3(0, 0.08f, 0), red, dark);
    }

    private void Container(Vector3 position, Vector3 rotation, Godot.Material material, Godot.Material trim)
    {
        var body = StaticBox("CargoContainer", position, new Vector3(6.2f, 2.65f, 2.55f), material, rotation);
        foreach (var x in new[] { -2.8f, -2.1f, -1.4f, -0.7f, 0.0f, 0.7f, 1.4f, 2.1f, 2.8f })
        {
            MeshBox(body, new Vector3(x, 0, -1.286f), new Vector3(0.055f, 2.42f, 0.045f), trim);
            MeshBox(body, new Vector3(x, 0, 1.286f), new Vector3(0.055f, 2.42f, 0.045f), trim);
        }
        foreach (var z in new[] { -1.22f, 1.22f })
        {
            MeshBox(body, new Vector3(-3.12f, 0, z), new Vector3(0.08f, 2.62f, 0.09f), trim);
            MeshBox(body, new Vector3(3.12f, 0, z), new Vector3(0.08f, 2.62f, 0.09f), trim);
        }
    }

    private void BuildCraneAndPipes(Godot.Material steel, Godot.Material dark, Godot.Material rust, Godot.Material yellow)
    {
        StaticBox("CraneLegL", new Vector3(-34, 7, -30), new Vector3(1.1f, 14, 1.1f), rust, new Vector3(0, 0, -0.12f));
        StaticBox("CraneLegR", new Vector3(-20, 7, -30), new Vector3(1.1f, 14, 1.1f), rust, new Vector3(0, 0, 0.12f));
        StaticBox("CraneBeam", new Vector3(-27, 13.5f, -30), new Vector3(19, 0.75f, 0.9f), yellow);
        StaticBox("CraneArm", new Vector3(-18, 14.2f, -30), new Vector3(25, 0.45f, 0.5f), steel, new Vector3(0, 0, -0.05f));
        foreach (var x in new[] { -8.0f, 3.0f, 14.0f, 25.0f })
        {
            StaticBox("PipeSupport", new Vector3(x, 2.1f, 28), new Vector3(0.35f, 4.2f, 0.35f), dark);
            StaticBox("PipeSupport", new Vector3(x, 2.1f, 33), new Vector3(0.35f, 4.2f, 0.35f), dark);
        }
        foreach (var z in new[] { 28.0f, 30.5f, 33.0f })
        {
            StaticCylinder("UtilityPipe", new Vector3(8.5f, 4.2f, z), 0.23f, 34, steel, new Vector3(0, 0, Mathf.Pi / 2));
        }
        StaticBox("Catwalk", new Vector3(22, 3.2f, 27), new Vector3(32, 0.25f, 2), steel);
        for (var x = 7; x < 39; x += 3)
        {
            MeshBox(_levelRoot, new Vector3(x, 4, 26.1f), new Vector3(0.08f, 1.5f, 0.08f), yellow);
            MeshBox(_levelRoot, new Vector3(x, 4, 27.9f), new Vector3(0.08f, 1.5f, 0.08f), yellow);
        }
        MeshBox(_levelRoot, new Vector3(22, 4.45f, 26.1f), new Vector3(32, 0.08f, 0.08f), yellow);
        MeshBox(_levelRoot, new Vector3(22, 4.45f, 27.9f), new Vector3(32, 0.08f, 0.08f), yellow);
    }

    private void BuildSecurityCheckpoint(
        Godot.Material concrete,
        Godot.Material dark,
        Godot.Material yellow)
    {
        var booth = Mat("checkpoint_booth", new Color(0.32f, 0.38f, 0.35f), 0.32f, 0.66f);
        var glass = Mat("checkpoint_glass", new Color(0.11f, 0.27f, 0.3f), 0.72f, 0.12f);
        StaticBox("CheckpointIslandL", new Vector3(-7.2f, 0.2f, 33.0f), new Vector3(8.2f, 0.4f, 1.25f), concrete);
        StaticBox("CheckpointIslandR", new Vector3(7.2f, 0.2f, 33.0f), new Vector3(8.2f, 0.4f, 1.25f), concrete);
        StaticBox("CheckpointPostL", new Vector3(-3.35f, 2.0f, 30.0f), new Vector3(0.32f, 4.0f, 0.32f), dark);
        StaticBox("CheckpointPostR", new Vector3(3.35f, 2.0f, 30.0f), new Vector3(0.32f, 4.0f, 0.32f), dark);
        StaticBox("CheckpointHeader", new Vector3(0, 3.85f, 30.0f), new Vector3(7.0f, 0.32f, 0.42f), yellow);
        StaticBox("CheckpointBooth", new Vector3(7.1f, 1.25f, 31.3f), new Vector3(3.3f, 2.5f, 3.0f), booth);
        MeshBox(_levelRoot, new Vector3(5.42f, 1.55f, 31.3f), new Vector3(0.04f, 0.78f, 1.55f), glass);
        MeshBox(_levelRoot, new Vector3(7.1f, 3.18f, 31.3f), new Vector3(3.8f, 0.18f, 3.5f), dark);
        for (var side = -1; side <= 1; side += 2)
        {
            StaticBox(
                "CheckpointBarrier",
                new Vector3(side * 8.6f, 0.62f, 29.5f),
                new Vector3(6.0f, 0.28f, 0.22f),
                yellow,
                new Vector3(0, 0, side * 0.08f));
        }
        _levelRoot.AddChild(new OmniLight3D
        {
            Position = new Vector3(0, 3.55f, 29.7f),
            LightColor = new Color(0.72f, 0.9f, 1.0f),
            LightEnergy = 1.8f,
            OmniRange = 9.0f,
            ShadowEnabled = false
        });
    }

    private void BuildFuelDepot(
        Godot.Material concrete,
        Godot.Material steel,
        Godot.Material dark,
        Godot.Material rust,
        Godot.Material yellow)
    {
        var center = new Vector3(-34.0f, 0, 14.0f);
        StaticBox("FuelDepotPad", center + new Vector3(0, 0.08f, 0), new Vector3(13.5f, 0.16f, 10.5f), concrete);
        foreach (var x in new[] { -37.2f, -31.4f })
        {
            StaticCylinder("FuelTank", new Vector3(x, 2.25f, 12.6f), 1.85f, 4.5f, steel);
            MeshBox(_levelRoot, new Vector3(x, 4.55f, 12.6f), new Vector3(3.25f, 0.18f, 0.22f), yellow);
            StaticCylinder("FuelValve", new Vector3(x, 1.0f, 10.6f), 0.18f, 1.0f, rust, new Vector3(Mathf.Pi / 2, 0, 0));
        }
        foreach (var x in new[] { -40.0f, -28.0f })
        {
            StaticBox("FuelCanopyPost", new Vector3(x, 2.5f, 17.7f), new Vector3(0.3f, 5.0f, 0.3f), dark);
        }
        StaticBox("FuelCanopy", new Vector3(-34.0f, 5.0f, 17.7f), new Vector3(12.4f, 0.28f, 3.8f), dark);
        for (var z = 9; z <= 19; z += 2)
        {
            MeshBox(_levelRoot, new Vector3(-40.7f, 1.05f, z), new Vector3(0.08f, 2.1f, 1.15f), yellow);
        }
        StaticBox("FuelPipe", new Vector3(-34.2f, 0.8f, 17.0f), new Vector3(7.0f, 0.22f, 0.22f), rust);
        _levelRoot.AddChild(new OmniLight3D
        {
            Position = center + new Vector3(0, 4.65f, 3.7f),
            LightColor = new Color(1.0f, 0.68f, 0.32f),
            LightEnergy = 2.2f,
            OmniRange = 10.5f,
            ShadowEnabled = false
        });
    }

    private void BuildBarracks(
        Godot.Material floor,
        Godot.Material wall,
        Godot.Material trim,
        Godot.Material marker)
    {
        var center = new Vector3(25.0f, 0, 21.5f);
        const float width = 17.0f;
        const float depth = 7.4f;
        const float height = 3.0f;
        StaticBox("BarracksFloor", center + new Vector3(0, 0.06f, 0), new Vector3(width, 0.12f, depth), floor);
        StaticBox("BarracksRoof", center + new Vector3(0, height, 0), new Vector3(width, 0.18f, depth), wall);
        StaticBox("BarracksNorth", center + new Vector3(0, height * 0.5f, -depth * 0.5f), new Vector3(width, height, 0.18f), wall);
        StaticBox("BarracksWest", center + new Vector3(-width * 0.5f, height * 0.5f, 0), new Vector3(0.18f, height, depth), wall);
        StaticBox("BarracksEast", center + new Vector3(width * 0.5f, height * 0.5f, 0), new Vector3(0.18f, height, depth), wall);
        StaticBox("BarracksSouthL", center + new Vector3(-5.15f, height * 0.5f, depth * 0.5f), new Vector3(6.7f, height, 0.18f), wall);
        StaticBox("BarracksSouthR", center + new Vector3(5.15f, height * 0.5f, depth * 0.5f), new Vector3(6.7f, height, 0.18f), wall);
        StaticBox("BarracksDoorHeader", center + new Vector3(0, 2.68f, depth * 0.5f), new Vector3(3.6f, 0.64f, 0.18f), wall);
        foreach (var x in new[] { -2.8f, 2.8f })
        {
            StaticBox("BarracksPartitionA", center + new Vector3(x, 1.5f, -2.6f), new Vector3(0.14f, 3.0f, 2.2f), wall);
            StaticBox("BarracksPartitionB", center + new Vector3(x, 1.5f, 2.15f), new Vector3(0.14f, 3.0f, 3.1f), wall);
        }
        var bunk = Mat("barracks_bunk", new Color(0.13f, 0.18f, 0.16f), 0.65f, 0.48f);
        var bedding = Mat("barracks_bedding", new Color(0.28f, 0.34f, 0.27f), 0.02f, 0.96f);
        foreach (var x in new[] { -6.0f, 0.0f, 6.0f })
        {
            StaticBox("BarracksBunk", center + new Vector3(x, 0.42f, -2.45f), new Vector3(2.2f, 0.16f, 0.82f), bunk);
            MeshBox(_levelRoot, center + new Vector3(x, 0.56f, -2.45f), new Vector3(2.0f, 0.12f, 0.7f), bedding);
            StaticBox("BarracksLocker", center + new Vector3(x + 0.95f, 0.85f, 1.9f), new Vector3(0.72f, 1.7f, 0.62f), trim);
        }
        MeshBox(_levelRoot, center + new Vector3(0, 2.56f, depth * 0.5f + 0.11f), new Vector3(2.5f, 0.15f, 0.04f), marker);
        foreach (var x in new[] { -5.5f, 0.0f, 5.5f })
        {
            _levelRoot.AddChild(new OmniLight3D
            {
                Position = center + new Vector3(x, 2.65f, 0),
                LightColor = new Color(0.84f, 0.93f, 0.86f),
                LightEnergy = 1.25f,
                OmniRange = 5.0f,
                ShadowEnabled = false
            });
        }
    }

    private void BuildCover(Godot.Material _)
    {
        const string barrierPath = "res://assets/models/concrete_road_barrier/concrete_road_barrier.gltf";
        var groups = new (Vector3 Center, float Angle, int Count)[]
        {
            (new Vector3(-4, 0.02f, 21), 0, 2),
            (new Vector3(4, 0.02f, 10), Mathf.Pi / 2, 2),
            (new Vector3(-4, 0.02f, -4), 0.1f, 3),
            (new Vector3(-4, 0.02f, -20), Mathf.Pi / 2, 3),
            (new Vector3(3, 0.02f, -31), -0.08f, 3),
            (new Vector3(17, 0.02f, 9), 0, 2)
        };
        foreach (var group in groups)
        {
            for (var i = 0; i < group.Count; i++)
            {
                var spacing = (i - (group.Count - 1) * 0.5f) * 1.78f;
                var offset = Vector3.Right.Rotated(Vector3.Up, group.Angle) * spacing;
                ModelProp(barrierPath, group.Center + offset, group.Angle, 1.18f, new Vector3(1.55f, 0.84f, 0.64f), new Vector3(0, 0.41f, 0));
            }
        }

        const string cratePath = "res://assets/models/old_military_crate/old_military_crate.gltf";
        var positions = new[] { new Vector3(-8, 0.02f, 29), new Vector3(7, 0.02f, 20), new Vector3(20, 0.02f, 4), new Vector3(19, 0.02f, -17), new Vector3(-8, 0.02f, -31) };
        for (var i = 0; i < positions.Length; i++)
        {
            ModelProp(cratePath, positions[i], i * 0.37f, 1.55f, new Vector3(0.82f, 0.42f, 0.68f), new Vector3(-0.06f, 0.21f, 0.1f));
            if (i % 2 == 0)
            {
                ModelProp(cratePath, positions[i] + new Vector3(0.08f, 0.65f, 0.02f), -0.28f + i * 0.2f, 1.42f, new Vector3(0.82f, 0.42f, 0.68f), new Vector3(-0.06f, 0.21f, 0.1f));
            }
        }
    }

    private void BuildBackground(Godot.Material concrete, Godot.Material steel)
    {
        foreach (var item in new (Vector3 Position, Vector3 Size)[]
        {
            (new Vector3(-128, 10, -28), new Vector3(24, 20, 46)),
            (new Vector3(132, 14, -72), new Vector3(27, 28, 54)),
            (new Vector3(-78, 10, -190), new Vector3(42, 20, 25)),
            (new Vector3(2, 14, -194), new Vector3(54, 28, 28)),
            (new Vector3(92, 9, -191), new Vector3(34, 18, 26))
        })
        {
            MeshBox(_levelRoot, item.Position, item.Size, concrete);
        }
        foreach (var x in new[] { -126.0f, 126.0f })
        {
            StaticCylinder("BackgroundTank", new Vector3(x, 7, -18), 6.5f, 14, steel);
        }
    }

    private void AddPuddles()
    {
        var material = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.055f, 0.095f, 0.105f, 0.48f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            Metallic = 0.7f,
            Roughness = 0.08f
        };
        foreach (var item in new (Vector3 Position, Vector2 Size)[]
        {
            (new Vector3(-5, 0.012f, 15), new Vector2(7, 3)),
            (new Vector3(13, 0.012f, -12), new Vector2(5, 2.3f)),
            (new Vector3(-24, 0.012f, 32), new Vector2(8, 3.2f))
        })
        {
            _levelRoot.AddChild(new MeshInstance3D
            {
                Mesh = new PlaneMesh { Size = item.Size },
                Position = item.Position,
                MaterialOverride = material
            });
        }
    }

    private void AddLightPoles()
    {
        var pole = Mat("pole", new Color(0.07f, 0.085f, 0.085f), 0.85f, 0.32f);
        var lamp = Mat("lamp", new Color(0.82f, 0.78f, 0.61f), 0.1f, 0.2f, new Color(0.95f, 0.75f, 0.42f));
        foreach (var position in new[] { new Vector3(-36, 0, 36), new Vector3(2, 0, 31), new Vector3(35, 0, 32), new Vector3(-7, 0, -12), new Vector3(5, 0, -36) })
        {
            StaticCylinder("LightPole", position + Vector3.Up * 4.5f, 0.09f, 9, pole);
            MeshBox(_levelRoot, position + new Vector3(0, 8.85f, 0), new Vector3(0.85f, 0.18f, 0.42f), lamp);
            _levelRoot.AddChild(new SpotLight3D
            {
                Position = position + new Vector3(0, 8.65f, 0),
                RotationDegrees = new Vector3(-90, 0, 0),
                LightColor = new Color(1.0f, 0.73f, 0.42f),
                LightEnergy = 5.0f,
                SpotRange = 23.0f,
                SpotAngle = 48.0f,
                ShadowEnabled = true
            });
        }
    }

    private StaticBody3D StaticBox(string name, Vector3 position, Vector3 size, Godot.Material material, Vector3 rotation = default)
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
        _levelRoot.AddChild(body);
        return body;
    }

    private StaticBody3D StaticCylinder(string name, Vector3 position, float radius, float height, Godot.Material material, Vector3 rotation = default)
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
            Mesh = new CylinderMesh { TopRadius = radius, BottomRadius = radius, Height = height, RadialSegments = 18 },
            MaterialOverride = material
        });
        body.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = radius, Height = height } });
        _levelRoot.AddChild(body);
        return body;
    }

    private static MeshInstance3D MeshBox(Node parent, Vector3 position, Vector3 size, Godot.Material material, Vector3 rotation = default)
    {
        var mesh = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = size },
            Position = position,
            Rotation = rotation,
            MaterialOverride = material
        };
        parent.AddChild(mesh);
        return mesh;
    }

    private StaticBody3D ModelProp(string path, Vector3 position, float yaw, float scale, Vector3 collisionSize, Vector3 collisionOffset)
    {
        var body = new StaticBody3D
        {
            Name = System.IO.Path.GetFileNameWithoutExtension(path),
            Position = position,
            Rotation = new Vector3(0, yaw, 0),
            CollisionLayer = 1,
            CollisionMask = 0
        };
        var scene = GD.Load<PackedScene>(path);
        if (scene?.Instantiate() is Node3D model)
        {
            model.Scale = Vector3.One * scale;
            body.AddChild(model);
        }
        body.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = collisionSize * scale },
            Position = collisionOffset * scale
        });
        _levelRoot.AddChild(body);
        return body;
    }
}
