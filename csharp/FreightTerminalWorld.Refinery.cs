using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private RefineryExtractionMapLayout? _refineryLayout;
    private int _refineryAuthoredModelCount;
    private int _refineryCollisionProxyCount;
    private int _refineryTallSceneCount;
    private OldTownLandmarksResult? _oldTownLandmarks;
    private readonly OldTownLandmarksBuilder _oldTownLandmarksBuilder = new();
    private readonly HashSet<string> _refineryModelScenes = new();
    private readonly HashSet<string> _oldTownDistricts = new();

    private bool IsBlackwaterRefineryMap
        => _activeRuntimeMapId == DeploymentMapCatalog.BlackwaterRefineryId;

    private RefineryExtractionMapLayout RefineryLayout
        => _refineryLayout ??= new RefineryExtractionMapBuilder().Build();

    private void BuildBlackwaterRefineryLevel()
    {
        _levelRoot = new Node3D { Name = "SaintMaraisOldTown" };
        AddChild(_levelRoot);

        var asphalt = GroundMaterial("asphalt", new Color(0.34f, 0.37f, 0.38f), 0.91f);
        var concrete = GroundMaterial("concrete", new Color(0.57f, 0.58f, 0.54f), 0.86f);
        var oldStone = Mat("old_town_stone", new Color(0.22f, 0.235f, 0.22f), 0.04f, 0.94f);
        var iron = Mat("old_town_iron", new Color(0.075f, 0.09f, 0.085f), 0.72f, 0.38f);
        var lamp = Mat(
            "old_town_lamp",
            new Color(0.72f, 0.6f, 0.34f),
            0.18f,
            0.35f,
            new Color(1.0f, 0.65f, 0.28f));
        var yellow = Mat("old_town_warning", new Color(0.78f, 0.52f, 0.07f), 0.16f, 0.58f);
        var white = Mat("old_town_marking", new Color(0.76f, 0.77f, 0.72f), 0.02f, 0.8f);

        StaticBox("OldTownGround", new Vector3(0, -0.55f, MapCenterZ), new Vector3(MapWidthMeters, 1, MapDepthMeters), asphalt);
        _levelRoot.AddChild(OceanBackdropFactory.Create(new Vector3(0, -0.18f, MapCenterZ)));
        BuildOldTownPerimeter(oldStone);
        BuildRefineryModelAssembly();
        _oldTownLandmarks = _oldTownLandmarksBuilder.Build(_levelRoot);
        BuildOldTownLandmarkDoors(_levelRoot, _oldTownLandmarks);
        if (_oldTownLandmarks.RooftopRoute.Count >= 2)
        {
            RegisterSquadTraversalLink(
                "old_town_market_rooftop",
                SquadTraversalKind.Walk,
                bidirectional: true,
                _oldTownLandmarks.RooftopRoute,
                costMultiplier: 1.08f);
        }
        BuildOldTownLighting(iron, lamp);
        BuildOldTownSigns();
        BuildRefineryMissionTerminals();
        BuildExtraction(concrete, iron, yellow, white);
        SpawnDriveableVehicle(
            new Vector3(-0.5f, 0, 72.0f),
            "OLD TOWN RESPONSE TRUCK",
            new Color(0.18f, 0.27f, 0.24f),
            yaw: 0.0f,
            maxHealth: 190.0f);
        _extractionMarker.Visible = true;
    }

    private void BuildOldTownPerimeter(Godot.Material stone)
    {
        var north = MapCenterZ - MapDepthMeters * 0.5f;
        var south = MapCenterZ + MapDepthMeters * 0.5f;
        var side = MapWidthMeters * 0.5f;
        StaticBox("OldTownNorthBoundary", new Vector3(0, 1.8f, north), new Vector3(MapWidthMeters, 4.2f, 1.0f), stone);
        StaticBox("OldTownWestBoundary", new Vector3(-side, 1.8f, MapCenterZ), new Vector3(1.0f, 4.2f, MapDepthMeters), stone);
        StaticBox("OldTownEastBoundary", new Vector3(side, 1.8f, MapCenterZ), new Vector3(1.0f, 4.2f, MapDepthMeters), stone);
        StaticBox("OldTownSouthBoundaryWest", new Vector3(-90, 1.8f, south), new Vector3(160, 4.2f, 1.0f), stone);
        StaticBox("OldTownSouthBoundaryEast", new Vector3(90, 1.8f, south), new Vector3(160, 4.2f, 1.0f), stone);
    }

    private void BuildRefineryModelAssembly()
    {
        _refineryAuthoredModelCount = 0;
        _refineryCollisionProxyCount = 0;
        _refineryTallSceneCount = 0;
        _refineryDoors.Clear();
        _refineryModelScenes.Clear();
        _oldTownDistricts.Clear();

        foreach (var placement in RefineryLayout.Models)
        {
            var modelRoot = placement.HasCollision
                ? ModelProp(
                    placement.ScenePath,
                    placement.Position,
                    placement.Yaw,
                    placement.Scale,
                    placement.CollisionSize,
                    placement.CollisionOffset,
                    placement.VisibilityRange,
                    placement.CastShadow)
                : AddOldTownVisualModel(placement);
            OldTownLandmarksBuilder.ConfigureImportedModel(
                modelRoot,
                placement.VisibilityRange,
                placement.CastShadow);
            modelRoot.Name = placement.Name;
            modelRoot.AddToGroup("refinery_authored_model");
            modelRoot.AddToGroup("old_town_authored_model");
            if (placement.HasCollision)
            {
                modelRoot.AddToGroup("old_town_collision_proxy");
                _refineryCollisionProxyCount++;
            }
            if (placement.IsTallScene)
            {
                modelRoot.AddToGroup("refinery_tall_scene");
                _refineryTallSceneCount++;
            }
            _refineryAuthoredModelCount++;
            _refineryModelScenes.Add(placement.ScenePath);
            _oldTownDistricts.Add(placement.District);
        }
    }

    private Node3D AddOldTownVisualModel(RefineryModelPlacement placement)
    {
        var root = new Node3D
        {
            Name = placement.Name,
            Position = placement.Position,
            Rotation = new Vector3(0, placement.Yaw, 0)
        };
        if (!_modelScenes.TryGetValue(placement.ScenePath, out var scene))
        {
            scene = GD.Load<PackedScene>(placement.ScenePath);
            if (scene is not null)
            {
                _modelScenes[placement.ScenePath] = scene;
            }
        }
        if (scene?.Instantiate() is Node3D model)
        {
            model.Scale = Vector3.One * placement.Scale;
            ConfigureAuthoredMapModel(model, placement.VisibilityRange, placement.CastShadow);
            root.AddChild(model);
        }
        _levelRoot.AddChild(root);
        return root;
    }

    private void BuildRefineryMissionTerminals()
    {
        BuildObjectiveTerminal(
            "GrandHotelSecurityTerminal",
            RefineryLayout.RelayTerminal,
            Mathf.Pi,
            relay: true);
        BuildObjectiveTerminal(
            "MunicipalTreasuryManifestTerminal",
            RefineryLayout.ManifestTerminal,
            0.0f,
            relay: false);
    }

    private void BuildOldTownLighting(Godot.Material iron, Godot.Material lamp)
    {
        var positions = new[]
        {
            new Vector3(-112, 0, 62), new Vector3(112, 0, 62),
            new Vector3(-28, 0, 38), new Vector3(28, 0, 38),
            new Vector3(-28, 0, -36), new Vector3(28, 0, -36),
            new Vector3(-108, 0, -88), new Vector3(108, 0, -88),
            new Vector3(-95, 0, -101), new Vector3(95, 0, -19),
            new Vector3(-28, 0, -144), new Vector3(28, 0, -144)
        };
        for (var index = 0; index < positions.Length; index++)
        {
            var position = positions[index];
            StaticCylinder("OldTownLampPost", position + Vector3.Up * 3.6f, 0.075f, 7.2f, iron);
            var fixture = MeshBox(
                _levelRoot,
                position + new Vector3(0, 7.05f, 0),
                new Vector3(0.52f, 0.24f, 0.52f),
                lamp);
            fixture.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            _levelRoot.AddChild(new OmniLight3D
            {
                Name = $"OldTownLamp_{index + 1:00}",
                Position = position + new Vector3(0, 6.75f, 0),
                LightColor = new Color(1.0f, 0.67f, 0.36f),
                LightEnergy = 2.2f,
                OmniRange = 17.0f,
                ShadowEnabled = false
            });
        }
    }

    private void BuildOldTownSigns()
    {
        AddRefinerySign(
            "OldTownTitle",
            new Vector3(0, 7.0f, 84),
            "SAINT MARAIS OLD TOWN  //  OT-02",
            0.0f,
            new Color(1.0f, 0.72f, 0.3f));
        AddRefinerySign(
            "HotelDistrictSign",
            new Vector3(-86, 5.7f, -96),
            "GRAND HOTEL  //  HIGH VALUE",
            0.0f,
            new Color(0.95f, 0.48f, 0.25f));
        AddRefinerySign(
            "TreasuryDistrictSign",
            new Vector3(86, 5.7f, -24),
            "MUNICIPAL TREASURY  //  HIGH VALUE",
            Mathf.Pi,
            new Color(0.95f, 0.48f, 0.25f));
        AddRefinerySign(
            "MarketDistrictSign",
            new Vector3(0, 6.3f, -109),
            "NORTH MARKET  //  ROOFTOP ROUTE",
            0.0f,
            new Color(0.42f, 0.88f, 1.0f));
    }

    private void AddRefinerySign(string name, Vector3 position, string text, float yaw, Color color)
    {
        _levelRoot.AddChild(new Label3D
        {
            Name = name,
            Position = position,
            Rotation = new Vector3(0, yaw, 0),
            Text = text,
            FontSize = 42,
            Modulate = color,
            OutlineSize = 8,
            VisibilityRangeEnd = 170.0f
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
