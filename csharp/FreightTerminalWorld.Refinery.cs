using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private RefineryExtractionMapLayout? _refineryLayout;
    private int _refineryCollisionProxyCount;
    private OldTownLandmarksResult? _oldTownLandmarks;
    private JianghaiOldCitySceneLoadResult? _jianghaiOldCityScene;
    private JianghaiGameplayCollisionResult? _jianghaiGameplayCollision;
    private bool _diagnosticSceneLoadFallbackAllowed;
    private bool _jianghaiDetailedSceneInspection;
    private string? _jianghaiOldCitySceneLoadError;
    private string? _jianghaiGameplayCollisionError;
    private readonly OldTownLandmarksBuilder _oldTownLandmarksBuilder = new();
    private readonly JianghaiGameplayCollisionBuilder _jianghaiCollisionBuilder = new();
    private readonly JianghaiOldCitySceneLoader _jianghaiOldCitySceneLoader = new(
        JianghaiOldCitySceneLoader.DefaultScenePath);
    private readonly JianghaiOldCityAtmosphere _jianghaiOldCityAtmosphere = new();
    private readonly HashSet<string> _oldTownDistricts = new();
    private int _oldTownInteriorResidentCount;

    private bool IsBlackwaterRefineryMap
        => _activeRuntimeMapId == DeploymentMapCatalog.BlackwaterRefineryId;

    private RefineryExtractionMapLayout RefineryLayout
        => _refineryLayout ??= new RefineryExtractionMapBuilder().Build();

    private void BuildBlackwaterRefineryLevel()
    {
        _levelRoot = new Node3D { Name = "SaintMaraisOldTown" };
        AddChild(_levelRoot);
        _jianghaiOldCitySceneLoadError = null;
        _jianghaiGameplayCollisionError = null;
        _jianghaiGameplayCollision = null;
        try
        {
            _jianghaiOldCityScene = _jianghaiOldCitySceneLoader.LoadOnce(
                _levelRoot,
                _jianghaiDetailedSceneInspection);
        }
        catch (Exception exception) when (_diagnosticSceneLoadFallbackAllowed)
        {
            _jianghaiOldCitySceneLoadError = exception.Message;
            GD.PrintErr($"REFINERY_AUTHORED_SCENE_ERROR {_jianghaiOldCitySceneLoadError}");
        }
        try
        {
            _jianghaiGameplayCollision = _jianghaiCollisionBuilder.Build(
                RefineryLayout,
                _jianghaiOldCityScene?.Root,
                _levelRoot);
        }
        catch (Exception exception)
        {
            _jianghaiGameplayCollisionError = exception.Message;
            GD.PrintErr($"REFINERY_GAMEPLAY_COLLISION_ERROR {_jianghaiGameplayCollisionError}");
            try
            {
                _jianghaiGameplayCollision = _jianghaiCollisionBuilder.BuildPlacementFallback(
                    RefineryLayout,
                    _levelRoot);
                GD.PrintErr(
                    "REFINERY_GAMEPLAY_COLLISION_FALLBACK placement_boxes="
                    + _jianghaiGameplayCollision.CollisionShapeCount);
            }
            catch (Exception fallbackException)
            {
                GD.PrintErr(
                    $"REFINERY_GAMEPLAY_COLLISION_FALLBACK_ERROR {fallbackException.Message}");
            }
        }

        var concrete = GroundMaterial("concrete", new Color(0.57f, 0.58f, 0.54f), 0.86f);
        var iron = Mat("old_town_iron", new Color(0.075f, 0.09f, 0.085f), 0.72f, 0.38f);
        var yellow = Mat("old_town_warning", new Color(0.78f, 0.52f, 0.07f), 0.16f, 0.58f);
        var white = Mat("old_town_marking", new Color(0.76f, 0.77f, 0.72f), 0.02f, 0.8f);

        AddInvisibleCollisionBox(
            "OldTownGround",
            new Vector3(0, -0.55f, MapCenterZ),
            new Vector3(MapWidthMeters, 1, MapDepthMeters));
        BuildOldTownPerimeter();
        BuildRefineryModelAssembly();
        _oldTownLandmarks = _oldTownLandmarksBuilder.BuildGameplayScaffolding(_levelRoot);
        BuildOldTownLandmarkDoors(_levelRoot, _oldTownLandmarks);
        BuildJianghaiResidentialInteriors();
        SpawnOldTownInteriorResidents();
        if (_oldTownLandmarks.RooftopRoute.Count >= 2)
        {
            RegisterSquadTraversalLink(
                "old_town_market_rooftop",
                SquadTraversalKind.Walk,
                bidirectional: true,
                _oldTownLandmarks.RooftopRoute,
                costMultiplier: 1.08f);
        }
        BuildOldTownLighting();
        BuildRefineryMissionTerminals();
        BuildExtraction(concrete, iron, yellow, white);
        HideLegacyExtractionArt();
        SpawnDriveableVehicle(
            new Vector3(-0.5f, 0, 72.0f),
            "JIANGHAI RESPONSE TRUCK",
            new Color(0.18f, 0.27f, 0.24f),
            yaw: 0.0f,
            maxHealth: 190.0f);
        _extractionMarker.Visible = true;
        var diagnosticArguments = OS.GetCmdlineUserArgs();
        var retainAuthoredSources = Array.Exists(
            diagnosticArguments,
            argument => argument is "--validate-refinery-map"
                or "--validate-jianghai-interiors");
        if (!retainAuthoredSources)
        {
            _jianghaiOldCitySceneLoader.ReleaseBatchedSourceNodes();
        }
    }

    private void BuildOldTownPerimeter()
    {
        var north = MapCenterZ - MapDepthMeters * 0.5f;
        var south = MapCenterZ + MapDepthMeters * 0.5f;
        var side = MapWidthMeters * 0.5f;
        AddInvisibleCollisionBox(
            "OldTownNorthBoundary",
            new Vector3(0, 1.8f, north),
            new Vector3(MapWidthMeters, 4.2f, 1.0f));
        AddInvisibleCollisionBox(
            "OldTownWestBoundary",
            new Vector3(-side, 1.8f, MapCenterZ),
            new Vector3(1.0f, 4.2f, MapDepthMeters));
        AddInvisibleCollisionBox(
            "OldTownEastBoundary",
            new Vector3(side, 1.8f, MapCenterZ),
            new Vector3(1.0f, 4.2f, MapDepthMeters));
        AddInvisibleCollisionBox(
            "OldTownSouthBoundaryWest",
            new Vector3(-90, 1.8f, south),
            new Vector3(160, 4.2f, 1.0f));
        AddInvisibleCollisionBox(
            "OldTownSouthBoundaryEast",
            new Vector3(90, 1.8f, south),
            new Vector3(160, 4.2f, 1.0f));
    }

    private void BuildRefineryModelAssembly()
    {
        _refineryCollisionProxyCount = _jianghaiGameplayCollision?.CollisionShapeCount ?? 0;
        _refineryDoors.Clear();
        _oldTownDistricts.Clear();

        foreach (var placement in RefineryLayout.Models)
        {
            _oldTownDistricts.Add(placement.District);
        }
    }

    private void BuildRefineryMissionTerminals()
    {
        BuildObjectiveTerminal(
            "GrandHotelSecurityTerminal",
            RefineryLayout.RelayTerminal,
            Mathf.Pi,
            relay: true,
            authoredCollisionSize: new Vector3(0.86f, 2.05f, 0.84f));
        BuildObjectiveTerminal(
            "MunicipalTreasuryManifestTerminal",
            RefineryLayout.ManifestTerminal,
            0.0f,
            relay: false,
            authoredCollisionSize: new Vector3(0.86f, 2.05f, 0.84f));
        HideLegacyScaffoldVisuals(_levelRoot.GetNode<Node3D>("GrandHotelSecurityTerminal"));
        HideLegacyScaffoldVisuals(_levelRoot.GetNode<Node3D>("MunicipalTreasuryManifestTerminal"));
    }

    private void BuildOldTownLighting()
    {
        var positions = new[]
        {
            new Vector3(-13.5f, 0, 78.0f), new Vector3(13.5f, 0, 52.0f),
            new Vector3(-13.5f, 0, 22.0f), new Vector3(13.5f, 0, -9.0f),
            new Vector3(-15.0f, 0, -46.0f), new Vector3(15.0f, 0, -74.0f),
            new Vector3(-13.5f, 0, -112.0f), new Vector3(13.5f, 0, -151.0f),
            new Vector3(-13.5f, 0, -190.0f), new Vector3(-73.0f, 0, -99.0f),
            new Vector3(73.0f, 0, -20.0f)
        };
        for (var index = 0; index < positions.Length; index++)
        {
            var position = positions[index];
            _levelRoot.AddChild(new OmniLight3D
            {
                Name = $"OldTownLamp_{index + 1:00}",
                Position = position + new Vector3(0.65f, 4.3f, 0),
                LightColor = new Color(1.0f, 0.67f, 0.36f),
                LightEnergy = 2.0f,
                OmniRange = 14.0f,
                ShadowEnabled = false
            });
        }

        var landmarkAccents = new[]
        {
            ("PawnshopCourt", new Vector3(-86.0f, 5.0f, -119.0f), new Color(1.0f, 0.28f, 0.09f), 4.8f, 15.0f),
            ("PawnshopGate", new Vector3(-86.0f, 3.8f, -109.0f), new Color(1.0f, 0.48f, 0.18f), 3.8f, 11.0f),
            ("FactoryLoading", new Vector3(86.0f, 5.2f, -2.0f), new Color(1.0f, 0.32f, 0.10f), 4.6f, 16.0f),
            ("MarketWest", new Vector3(-13.0f, 6.2f, -126.0f), new Color(1.0f, 0.35f, 0.10f), 3.6f, 11.0f),
            ("MarketEast", new Vector3(13.0f, 6.2f, -126.0f), new Color(1.0f, 0.35f, 0.10f), 3.6f, 11.0f)
        };
        foreach (var accent in landmarkAccents)
        {
            _levelRoot.AddChild(new OmniLight3D
            {
                Name = $"OldTownAccent_{accent.Item1}",
                Position = accent.Item2,
                LightColor = accent.Item3,
                LightEnergy = accent.Item4,
                OmniRange = accent.Item5,
                ShadowEnabled = false
            });
        }
    }

    private void ApplyJianghaiOldCityAtmosphere(DeploymentTimeOfDay timeOfDay)
    {
        _jianghaiOldCityAtmosphere.Apply(
            IsBlackwaterRefineryMap,
            timeOfDay,
            _qualitySetting,
            _environmentRef,
            _sunLight,
            _fillLight);
    }

    private StaticBody3D AddInvisibleCollisionBox(
        string name,
        Vector3 position,
        Vector3 size,
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
        body.AddChild(new CollisionShape3D
        {
            Name = "Collision",
            Shape = new BoxShape3D { Size = size }
        });
        _levelRoot.AddChild(body);
        return body;
    }

    private void HideLegacyExtractionArt()
    {
        var site = _levelRoot.GetNode<Node3D>("ExtractionSite");
        var children = site.GetChildren();
        using var childrenBacking = children.AsDisposable();
        foreach (var child in children)
        {
            if (child is not Node childNode
                || childNode == _extractionArea
                || childNode == _extractionMarker)
            {
                continue;
            }

            HideLegacyScaffoldVisuals(childNode);
        }
    }

    private static void HideLegacyScaffoldVisuals(Node root)
    {
        root.AddToGroup("refinery_legacy_visual_scaffold");
        HideGeometryRecursive(root);
    }

    private static void RetainLegacyLandmarkCollisionOnly(Node3D root)
    {
        root.AddToGroup("refinery_legacy_visual_scaffold");
        var children = root.GetChildren();
        using var childrenBacking = children.AsDisposable();
        foreach (var child in children)
        {
            if (child is not Node childNode || childNode is CollisionObject3D)
            {
                continue;
            }

            HideGeometryRecursive(childNode);
            childNode.QueueFree();
        }
    }

    private static void HideGeometryRecursive(Node node)
    {
        if (node is GeometryInstance3D geometry)
        {
            geometry.Visible = false;
        }
        else if (node is Light3D light)
        {
            light.Visible = false;
        }

        var children = node.GetChildren();
        using var childrenBacking = children.AsDisposable();
        foreach (var child in children)
        {
            if (child is Node childNode)
            {
                HideGeometryRecursive(childNode);
            }
        }
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
