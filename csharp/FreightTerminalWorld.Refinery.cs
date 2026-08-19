using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private RefineryExtractionMapLayout? _refineryLayout;
    private int _refineryAuthoredModelCount;
    private int _refineryCollisionProxyCount;
    private int _refineryAccessibleBuildingCount;
    private int _refineryTallSceneCount;
    private int _refineryEntryBeaconCount;
    private RefineryFactoryDistrictResult? _refineryFactoryDistrict;
    private readonly RefineryFactoryDistrictBuilder _refineryFactoryDistrictBuilder = new();
    private RefineryWonderLandmarksResult? _refineryWonderLandmarks;
    private readonly RefineryWonderLandmarksBuilder _refineryWonderLandmarksBuilder = new();
    private readonly HashSet<string> _refineryModelScenes = new();

    private bool IsBlackwaterRefineryMap
        => _activeRuntimeMapId == DeploymentMapCatalog.BlackwaterRefineryId;

    private RefineryExtractionMapLayout RefineryLayout
        => _refineryLayout ??= new RefineryExtractionMapBuilder().Build();

    private void BuildBlackwaterRefineryLevel()
    {
        _levelRoot = new Node3D { Name = "BlackwaterRefinery" };
        AddChild(_levelRoot);

        var asphalt = GroundMaterial("asphalt", new Color(0.36f, 0.39f, 0.39f), 0.9f);
        var concrete = GroundMaterial("concrete", new Color(0.58f, 0.6f, 0.56f), 0.84f);
        var concreteDark = Mat("refinery_concrete_dark", new Color(0.13f, 0.15f, 0.145f), 0.08f, 0.88f);
        var steel = Mat("refinery_steel", new Color(0.085f, 0.105f, 0.1f), 0.8f, 0.32f);
        var yellow = Mat("refinery_warning", new Color(0.82f, 0.54f, 0.055f), 0.18f, 0.52f);
        var white = Mat("refinery_marking", new Color(0.76f, 0.79f, 0.74f), 0.02f, 0.78f);

        StaticBox("RefineryGround", new Vector3(0, -0.55f, MapCenterZ), new Vector3(MapWidthMeters, 1, MapDepthMeters), asphalt);
        _levelRoot.AddChild(OceanBackdropFactory.Create(new Vector3(0, -0.18f, MapCenterZ)));
        BuildRefineryPerimeter(concreteDark);
        BuildRefineryRoadMarkings(yellow, white);
        BuildRefineryModelAssembly();
        _refineryFactoryDistrict = _refineryFactoryDistrictBuilder.Build(_levelRoot);
        _refineryWonderLandmarks = _refineryWonderLandmarksBuilder.Build(_levelRoot);
        BuildRefineryLighting(steel, yellow);
        BuildRefinerySigns();
        BuildMissionTerminals();
        BuildExtraction(concrete, steel, yellow, white);
        SpawnDriveableVehicle(
            new Vector3(-0.5f, 0, 72.0f),
            "REFINERY RESPONSE TRUCK",
            new Color(0.22f, 0.3f, 0.25f),
            yaw: 0.0f,
            maxHealth: 190.0f);
        _extractionMarker.Visible = true;
    }

    private void BuildRefineryPerimeter(Godot.Material concrete)
    {
        var north = MapCenterZ - MapDepthMeters * 0.5f;
        var south = MapCenterZ + MapDepthMeters * 0.5f;
        var side = MapWidthMeters * 0.5f;
        StaticBox("RefineryNorthPerimeter", new Vector3(0, 1.6f, north), new Vector3(MapWidthMeters, 3.8f, 1.2f), concrete);
        StaticBox("RefineryWestPerimeter", new Vector3(-side, 1.6f, MapCenterZ), new Vector3(1.2f, 3.8f, MapDepthMeters), concrete);
        StaticBox("RefineryEastPerimeter", new Vector3(side, 1.6f, MapCenterZ), new Vector3(1.2f, 3.8f, MapDepthMeters), concrete);
        StaticBox("RefinerySouthPerimeterLeft", new Vector3(-91, 1.6f, south), new Vector3(158, 3.8f, 1.2f), concrete);
        StaticBox("RefinerySouthPerimeterRight", new Vector3(91, 1.6f, south), new Vector3(158, 3.8f, 1.2f), concrete);

        foreach (var x in new[] { -132.0f, -46.0f, 46.0f, 132.0f })
        {
            StaticBox("RefineryNorthBund", new Vector3(x, 0.55f, -188), new Vector3(28, 1.1f, 0.65f), concrete);
        }
        foreach (var z in new[] { 12.0f, -108.0f })
        {
            StaticBox("RefineryWestBund", new Vector3(-140, 0.55f, z), new Vector3(0.65f, 1.1f, 38), concrete);
            StaticBox("RefineryEastBund", new Vector3(140, 0.55f, z), new Vector3(0.65f, 1.1f, 38), concrete);
        }
    }

    private void BuildRefineryRoadMarkings(Godot.Material yellow, Godot.Material white)
    {
        for (var z = -198.0f; z <= 88.0f; z += 11.0f)
        {
            AddRefineryMarking(new Vector3(-5.4f, 0.015f, z), new Vector3(0.16f, 0.03f, 5.4f), white);
            AddRefineryMarking(new Vector3(5.4f, 0.015f, z), new Vector3(0.16f, 0.03f, 5.4f), white);
        }
        for (var x = -158.0f; x <= 158.0f; x += 11.0f)
        {
            AddRefineryMarking(new Vector3(x, 0.017f, -53.5f), new Vector3(5.4f, 0.032f, 0.16f), yellow);
            AddRefineryMarking(new Vector3(x, 0.017f, -66.5f), new Vector3(5.4f, 0.032f, 0.16f), yellow);
        }
        foreach (var x in new[] { -54.0f, 54.0f })
        {
            for (var z = -172.0f; z <= 57.0f; z += 14.0f)
            {
                AddRefineryMarking(new Vector3(x, 0.016f, z), new Vector3(0.12f, 0.031f, 6.4f), yellow);
            }
        }
    }

    private static void ConfigureRefineryMarking(MeshInstance3D marking)
    {
        marking.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        marking.VisibilityRangeEnd = 96.0f;
        marking.VisibilityRangeEndMargin = 10.0f;
    }

    private void AddRefineryMarking(Vector3 position, Vector3 size, Godot.Material material)
    {
        var marking = MeshBox(_levelRoot, position, size, material);
        ConfigureRefineryMarking(marking);
    }

    private void BuildRefineryModelAssembly()
    {
        _refineryAuthoredModelCount = 0;
        _refineryCollisionProxyCount = 0;
        _refineryAccessibleBuildingCount = 0;
        _refineryTallSceneCount = 0;
        _refineryEntryBeaconCount = 0;
        _refineryModelScenes.Clear();
        var entryMaterial = Mat(
            "refinery_entry_beacon",
            new Color(0.08f, 0.44f, 0.31f),
            0.25f,
            0.42f,
            new Color(0.12f, 0.95f, 0.56f));
        foreach (var placement in RefineryLayout.Models)
        {
            var body = ModelProp(
                placement.ScenePath,
                placement.Position,
                placement.Yaw,
                placement.Scale,
                placement.CollisionSize,
                placement.CollisionOffset,
                placement.VisibilityRange,
                placement.CastShadow,
                placement.HasDoorway);
            body.Name = placement.Name;
            body.AddToGroup("refinery_authored_model");
            if (placement.HasDoorway)
            {
                body.AddToGroup("refinery_accessible_building");
                AddRefineryEntryBeacon(body, placement, entryMaterial);
                _refineryAccessibleBuildingCount++;
            }
            if (placement.IsTallScene)
            {
                body.AddToGroup("refinery_tall_scene");
                _refineryTallSceneCount++;
            }
            _refineryAuthoredModelCount++;
            _refineryCollisionProxyCount++;
            _refineryModelScenes.Add(placement.ScenePath);
        }
    }

    private void AddRefineryEntryBeacon(
        StaticBody3D building,
        RefineryModelPlacement placement,
        Godot.Material material)
    {
        var scaledSize = placement.CollisionSize * placement.Scale;
        var doorway = _authoredBuildingCollisionPlanner.DoorwayMetrics(scaledSize);
        var frontZ = scaledSize.Z * 0.5f + 0.06f;
        var root = new Node3D { Name = "EntryBeacon" };
        root.AddToGroup("refinery_entry_beacon");
        building.AddChild(root);
        ConfigureRefineryEntryBeaconMesh(MeshBox(
            root,
            new Vector3(-doorway.Width * 0.5f, doorway.Height * 0.5f, frontZ),
            new Vector3(0.07f, doorway.Height, 0.06f),
            material));
        ConfigureRefineryEntryBeaconMesh(MeshBox(
            root,
            new Vector3(doorway.Width * 0.5f, doorway.Height * 0.5f, frontZ),
            new Vector3(0.07f, doorway.Height, 0.06f),
            material));
        ConfigureRefineryEntryBeaconMesh(MeshBox(
            root,
            new Vector3(0, doorway.Height, frontZ),
            new Vector3(doorway.Width, 0.07f, 0.06f),
            material));
        _refineryEntryBeaconCount++;
    }

    private static void ConfigureRefineryEntryBeaconMesh(MeshInstance3D mesh)
    {
        mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        mesh.VisibilityRangeEnd = 180.0f;
        mesh.VisibilityRangeEndMargin = 14.0f;
    }

    private void BuildRefineryLighting(Godot.Material steel, Godot.Material lamp)
    {
        var positions = new[]
        {
            new Vector3(-92, 0, 61), new Vector3(92, 0, 60),
            new Vector3(-22, 0, 17), new Vector3(24, 0, 16),
            new Vector3(-116, 0, -62), new Vector3(116, 0, -65),
            new Vector3(-54, 0, -106), new Vector3(55, 0, -109),
            new Vector3(-110, 0, -169), new Vector3(111, 0, -170)
        };
        for (var index = 0; index < positions.Length; index++)
        {
            var position = positions[index];
            StaticCylinder("RefineryLightPole", position + Vector3.Up * 4.5f, 0.09f, 9.0f, steel);
            var fixture = MeshBox(_levelRoot, position + new Vector3(0, 8.85f, 0), new Vector3(0.9f, 0.18f, 0.44f), lamp);
            fixture.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            _levelRoot.AddChild(new SpotLight3D
            {
                Name = $"RefineryLight_{index + 1:00}",
                Position = position + new Vector3(0, 8.6f, 0),
                RotationDegrees = new Vector3(-90, 0, 0),
                LightColor = new Color(1.0f, 0.72f, 0.42f),
                LightEnergy = 4.0f,
                SpotRange = 24.0f,
                SpotAngle = 49.0f,
                ShadowEnabled = false
            });
        }
    }

    private void BuildRefinerySigns()
    {
        AddRefinerySign("RefineryTitle", new Vector3(0, 6.2f, 84), "BLACKWATER REFINERY  //  BR-02", 0.0f, new Color(1.0f, 0.68f, 0.22f));
        AddRefinerySign("ManifestSign", new Vector3(-41, 4.4f, -3), "MANIFEST INTAKE", Mathf.Pi * 0.5f, new Color(0.96f, 0.66f, 0.2f));
        AddRefinerySign("RelaySign", new Vector3(42, 4.4f, -12), "RELAY PROCESSING", -Mathf.Pi * 0.5f, new Color(0.3f, 0.9f, 0.7f));
        AddRefinerySign("CrackingSign", new Vector3(0, 5.2f, -132), "CRACKING YARDS  //  NORTH", 0.0f, new Color(0.46f, 0.82f, 1.0f));
    }

    private void AddRefinerySign(string name, Vector3 position, string text, float yaw, Color color)
    {
        _levelRoot.AddChild(new Label3D
        {
            Name = name,
            Position = position,
            Rotation = new Vector3(0, yaw, 0),
            Text = text,
            FontSize = 46,
            Modulate = color,
            OutlineSize = 8,
            VisibilityRangeEnd = 150.0f
        });
    }

    private void ResumePendingExtractionDeployment()
    {
        if (!DeploymentMapRuntime.TryConsumePending(_activeRuntimeMapId, out var deployment))
        {
            return;
        }

        _hud.SetDeploymentMapSelection(deployment.MapId);
        OnOperationsQuickStartRequested();
        if (deployment.SessionMode == SquadSessionMode.Join && !_squadNetwork.IsOnline)
        {
            BeginPendingExtractionJoin(deployment);
            return;
        }
        if (!TryCommitPendingDeployment(deployment.Loadout))
        {
            return;
        }

        _activeDeploymentMapId = deployment.MapId;
        _extractionLocalSquadSlot = deployment.SquadSlot;
        DeploySquad(deployment.Role, deployment.SessionMode, deployment.Address);
        if (_squadNetwork.ExtractionMatchStarted)
        {
            _squadNetwork.NotifyExtractionWorldReady();
        }
    }

    private bool TryCommitPendingDeployment(DeploymentLoadoutSelection selection)
    {
        if (_deploymentPurchaseCommitted)
        {
            return true;
        }
        if (!_operatorProfileStore.TryCommitDeployment(selection, out var loadout, out var failure))
        {
            _hud.ShowDeploymentPurchaseError(failure);
            return false;
        }

        _player.ApplyDeploymentLoadout(loadout);
        _deploymentPurchaseCommitted = true;
        _deploymentBaselineValue = CombatHUD.ComputeBackpackTotalValue(_player);
        _hud.SetOperatorProfile(_operatorProfileStore.Profile);
        return true;
    }
}
