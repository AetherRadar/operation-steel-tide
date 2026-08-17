using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private enum VerticalAccessKind
    {
        Ladder,
        Stairs
    }

    private sealed record RoofAccessRoute(
        string Id,
        string Building,
        Vector3 BottomFeet,
        Vector3 TopFeet,
        Vector3 Outward,
        VerticalAccessKind Kind,
        Node3D? VisualRoot = null);

    private const float RoofAccessUseRange = 2.45f;
    private readonly List<RoofAccessRoute> _roofAccessRoutes = new();
    private Node3D? _roofAccessRoot;

    public int RoofAccessRouteCount => _roofAccessRoutes.Count;
    public int FunctionalLadderCount => _roofAccessRoutes.Count(route => route.Kind == VerticalAccessKind.Ladder);

    private void BuildRoofAccessNetwork(Godot.Material steel, Godot.Material safety)
    {
        _roofAccessRoutes.Clear();
        _roofAccessRoot = new Node3D { Name = "RoofAccessNetwork" };
        _roofAccessRoot.AddToGroup("roof_access_network");
        _levelRoot.AddChild(_roofAccessRoot);

        // Core terminal buildings and the three secure rooms.
        AddRoofLadder("WarehouseRoof", "Warehouse", new Vector3(40.05f, 0.08f, -13.0f), new Vector3(37.8f, 9.24f, -13.0f), Vector3.Right, safety);
        AddRoofLadder("ArmoryRoof", "Armory room", new Vector3(37.15f, 0.2f, -18.0f), new Vector3(35.45f, 3.34f, -18.0f), Vector3.Right, safety);
        AddRoofLadder("CustomsOfficeRoof", "Customs office", new Vector3(-38.45f, 0.08f, -11.0f), new Vector3(-36.75f, 3.34f, -11.0f), Vector3.Left, safety);
        AddRoofLadder("MaintenanceRoomRoof", "Maintenance room", new Vector3(13.0f, 0.2f, 7.25f), new Vector3(13.0f, 3.34f, 9.05f), Vector3.Forward, safety);
        AddRoofLadder("FuelCanopyRoof", "Fuel depot", new Vector3(-26.95f, 0.08f, 17.7f), new Vector3(-28.75f, 5.2f, 17.7f), Vector3.Right, safety);
        AddRoofLadder("BarracksRoof", "Barracks", new Vector3(34.45f, 0.08f, 21.0f), new Vector3(32.65f, 3.16f, 21.0f), Vector3.Right, safety);
        AddRoofLadder("PipeCatwalk", "Utility catwalk", new Vector3(5.45f, 0.08f, 27.0f), new Vector3(6.85f, 3.4f, 27.0f), Vector3.Left, safety);
        AddRoofLadder("CommandPodRoof", "Command hub", new Vector3(-5.65f, 0.08f, 14.6f), new Vector3(-3.75f, 10.48f, 14.6f), Vector3.Left, safety);
        AddRoofLadder("RadarPodRoof", "Radar spire", new Vector3(39.35f, 0.08f, 33.0f), new Vector3(37.35f, 14.68f, 33.0f), Vector3.Right, safety);

        // Expansion districts. Each enterable building has its own exterior route to the roof.
        AddRoofLadder("CustomsWarehouseRoof", "Customs warehouse", new Vector3(-70.0f, 0.08f, -23.0f), new Vector3(-68.0f, 5.82f, -23.0f), Vector3.Left, safety);
        AddRoofLadder("OpsAnnexRoof", "Operations annex", new Vector3(52.0f, 0.08f, -58.0f), new Vector3(52.0f, 10.22f, -56.0f), Vector3.Forward, safety);
        AddRoofLadder("FuelLogisticsRoof", "Fuel logistics hall", new Vector3(72.0f, 0.08f, -118.0f), new Vector3(70.0f, 5.42f, -118.0f), Vector3.Right, safety);
        AddRoofLadder("QuayStorageRoof", "Quay bonded storage", new Vector3(31.0f, 0.08f, -143.0f), new Vector3(29.0f, 5.02f, -143.0f), Vector3.Right, safety);
        AddRoofLadder("DispatchOfficeRoof", "Rail dispatch office", new Vector3(-105.25f, 0.08f, -66.0f), new Vector3(-103.45f, 3.7f, -66.0f), Vector3.Left, safety);
        AddRoofLadder("RailCanopyRoof", "Rail loading canopy", new Vector3(-40.0f, 0.08f, -101.0f), new Vector3(-38.2f, 4.42f, -101.0f), Vector3.Left, safety);
        AddRoofLadder("MaintenanceHangarRoof", "Maintenance hangar", new Vector3(8.55f, 0.08f, -100.0f), new Vector3(10.45f, 8.25f, -100.0f), Vector3.Left, safety);
        AddRoofLadder("TankControlRoof", "Tank control shelter", new Vector3(105.0f, 0.08f, -118.0f), new Vector3(103.2f, 3.58f, -118.0f), Vector3.Right, safety);
        AddRoofLadder("SeawallShelterRoof", "Seawall shelter", new Vector3(49.45f, 0.08f, -146.0f), new Vector3(47.65f, 3.3f, -146.0f), Vector3.Right, safety);
        AddRoofLadder("ExtractionShelterRoof", "Extraction equipment shelter", new Vector3(-15.15f, 0.28f, -56.5f), new Vector3(-13.75f, 2.9f, -56.5f), Vector3.Left, safety);

        // Stacked freight is intentionally playable cover rather than a dead-end skyline prop.
        AddRoofLadder("CoreContainerStack", "Core container stack", new Vector3(-12.0f, 0.08f, -24.0f), new Vector3(-13.75f, 5.48f, -24.0f), Vector3.Right, safety);
        AddRoofLadder("OverflowContainerStackA", "Overflow container stack A", new Vector3(60.2f, 0.08f, 13.0f), new Vector3(58.45f, 5.48f, 13.0f), Vector3.Right, safety);
        AddRoofLadder("OverflowContainerStackB", "Overflow container stack B", new Vector3(74.2f, 0.08f, -10.0f), new Vector3(72.45f, 5.48f, -10.0f), Vector3.Right, safety);
        AddRoofLadder("OverflowContainerStackC", "Overflow container stack C", new Vector3(98.0f, 0.08f, -22.0f), new Vector3(96.25f, 5.48f, -22.0f), Vector3.Right, safety);
        AddRoofLadder("QuayContainerStack", "Quay container stack", new Vector3(-12.0f, 0.08f, -139.0f), new Vector3(-13.75f, 5.58f, -139.0f), Vector3.Right, safety);

        // Existing landmark stairs are retained and registered as real coverage routes.
        AddStairRoute("BazaarAuctionDeck", "Salvage bazaar", new Vector3(-78.35f, 0.08f, 6.8f), new Vector3(-78.35f, 3.08f, 1.0f));
        AddStairRoute("TideglassCatwalk", "Tideglass conservatory", new Vector3(103.9f, 0.08f, 12.8f), new Vector3(103.9f, 3.55f, 3.0f));
        AddStairRoute("ObservatoryDeck", "Tidal observatory", new Vector3(-122.7f, 0.08f, 51.2f), new Vector3(-120.1f, 5.82f, 40.2f));
        AddStairRoute("DrydockWestCatwalk", "Drydock west catwalk", new Vector3(65.6f, 0.08f, -139.6f), new Vector3(65.6f, 4.45f, -149.1f));
        AddStairRoute("DrydockEastCatwalk", "Drydock east catwalk", new Vector3(88.4f, 0.08f, -162.4f), new Vector3(88.4f, 4.45f, -152.9f));

        // Close the gap between the observatory stair head and its circular deck.
        AddRoofAccessBridge(
            "ObservatoryStairLandingBridge",
            new Vector3(-122.7f, 5.68f, 39.2f),
            new Vector3(-120.1f, 5.68f, 40.2f),
            2.45f,
            steel);
        var observatoryLadderLanding = ExpansionBox(
            _roofAccessRoot,
            "ObservatoryCoreLadderLanding",
            new Vector3(-108.95f, 5.68f, 43.0f),
            new Vector3(2.0f, 0.16f, 1.4f),
            steel);
        observatoryLadderLanding.AddToGroup("roof_access_bridge");

        // Reuse the observatory's authored ladder mesh, but make it functional.
        AddRoofLadder(
            "ObservatoryCoreRoof",
            "Tidal observatory core",
            new Vector3(-109.7f, 5.78f, 43.0f),
            new Vector3(-111.15f, 8.72f, 43.0f),
            Vector3.Right,
            safety,
            buildVisual: false);
        AddRoofLadder(
            "DrydockCraneRoof",
            "Drydock crane cab",
            new Vector3(71.6f, 4.48f, -153.0f),
            new Vector3(73.3f, 10.32f, -153.0f),
            Vector3.Left,
            safety);
        AddRoofAccessBridge(
            "DrydockCraneApproachBridge",
            new Vector3(66.6f, 4.38f, -151.0f),
            new Vector3(71.6f, 4.38f, -153.0f),
            2.1f,
            steel);
        var craneRoofDeck = ExpansionBox(
            _roofAccessRoot,
            "DrydockCraneRoofAccessDeck",
            new Vector3(77.0f, 10.15f, -151.4f),
            new Vector3(9.0f, 0.14f, 4.8f),
            steel);
        craneRoofDeck.AddToGroup("roof_access_bridge");

        // All residential towers already have continuous switchback stairs through the roof slab.
        for (var index = 0; index < _residentialRooftops.Count; index++)
        {
            var bottom = index < _residentialEntrances.Count ? _residentialEntrances[index] : _residentialRooftops[index];
            var spec = ResidentialTowerSpecs[index];
            var coreZ = -Mathf.Min(spec.Footprint.Y * 0.18f, 3.6f);
            var roofExit = _residentialTowers[index].ToGlobal(new Vector3(
                0.0f,
                spec.Floors * ResidentialFloorHeight + 0.2f,
                coreZ + ResidentialStairOpeningSouthDepth + 0.75f));
            AddStairRoute($"ResidentialTower{index + 1:00}", $"Residential tower {index + 1}", bottom, roofExit);
        }
    }

    private void AddRoofLadder(
        string id,
        string building,
        Vector3 bottomFeet,
        Vector3 topFeet,
        Vector3 outward,
        Godot.Material material,
        bool buildVisual = true)
    {
        outward.Y = 0.0f;
        outward = outward.Normalized();
        Node3D? visualRoot = null;
        if (buildVisual)
        {
            visualRoot = BuildRoofLadderVisual(id, bottomFeet, topFeet, outward, material);
        }
        _roofAccessRoutes.Add(new RoofAccessRoute(
            id,
            building,
            bottomFeet,
            topFeet,
            outward,
            VerticalAccessKind.Ladder,
            visualRoot));
    }

    private void AddStairRoute(string id, string building, Vector3 bottomFeet, Vector3 topFeet)
    {
        _roofAccessRoutes.Add(new RoofAccessRoute(
            id,
            building,
            bottomFeet,
            topFeet,
            Vector3.Zero,
            VerticalAccessKind.Stairs));
    }

    private Node3D BuildRoofLadderVisual(
        string id,
        Vector3 bottomFeet,
        Vector3 topFeet,
        Vector3 outward,
        Godot.Material material)
    {
        var root = new Node3D { Name = $"RoofLadder_{id}" };
        root.AddToGroup("functional_roof_ladder");
        _roofAccessRoot!.AddChild(root);

        var lateral = new Vector3(-outward.Z, 0.0f, outward.X).Normalized();
        var ladderLine = bottomFeet - outward * 0.58f;
        var bottomY = bottomFeet.Y + 0.22f;
        var topY = topFeet.Y + 0.35f;
        var height = Mathf.Max(1.2f, topY - bottomY);
        const float rungSpacing = 0.42f;
        var rungCount = Mathf.Max(3, Mathf.FloorToInt(height / rungSpacing) + 1);
        var multiMesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            Mesh = SharedBoxMesh(Vector3.One),
            InstanceCount = rungCount + 2
        };
        var railCenter = new Vector3(ladderLine.X, (bottomY + topY) * 0.5f, ladderLine.Z);
        var railBasis = new Basis(
            lateral * 0.09f,
            Vector3.Up * height,
            outward * 0.09f);
        multiMesh.SetInstanceTransform(0, new Transform3D(railBasis, railCenter - lateral * 0.43f));
        multiMesh.SetInstanceTransform(1, new Transform3D(railBasis, railCenter + lateral * 0.43f));
        var rungBasis = new Basis(
            lateral * 0.94f,
            Vector3.Up * 0.08f,
            outward * 0.13f);
        for (var rung = 0; rung < rungCount; rung++)
        {
            var t = rungCount == 1 ? 0.0f : rung / (float)(rungCount - 1);
            var y = Mathf.Lerp(bottomY, topY, t);
            multiMesh.SetInstanceTransform(rung + 2, new Transform3D(rungBasis, new Vector3(ladderLine.X, y, ladderLine.Z)));
        }

        var visual = new MultiMeshInstance3D
        {
            Name = $"RoofLadderVisual_{id}",
            Multimesh = multiMesh,
            MaterialOverride = material,
            VisibilityRangeEnd = 96.0f,
            VisibilityRangeEndMargin = 8.0f
        };
        root.AddChild(visual);
        RegisterMapDetailVisual(visual);
        root.AddChild(new Label3D
        {
            Name = $"RoofLadderLabel_{id}",
            Position = bottomFeet - outward * 0.66f + Vector3.Up * 2.2f,
            Text = "ROOF ACCESS",
            FontSize = 12,
            OutlineSize = 4,
            Modulate = new Color(1.0f, 0.69f, 0.24f),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            VisibilityRangeEnd = 20.0f
        });
        return root;
    }

    private void AddRoofAccessBridge(
        string id,
        Vector3 from,
        Vector3 to,
        float width,
        Godot.Material material)
    {
        if (_roofAccessRoot is null)
        {
            return;
        }
        var delta = to - from;
        delta.Y = 0.0f;
        var length = delta.Length();
        var yaw = Mathf.Atan2(delta.X, delta.Z);
        var bridge = ExpansionBox(
            _roofAccessRoot,
            id,
            (from + to) * 0.5f,
            new Vector3(width, 0.18f, length + 0.25f),
            material,
            new Vector3(0.0f, yaw, 0.0f));
        bridge.AddToGroup("roof_access_bridge");
    }

    private bool TryHandleRoofAccessInteraction()
    {
        if (_player.IsClimbingLadder)
        {
            _lootSearchTarget = null;
            _player.SetSearchPose(false);
            var active = GameLocalization.Get(
                "climb_active",
                _languageSetting,
                "W / S MOVE  //  SPACE OR F DISMOUNT");
            _hud.SetInteraction(active, _player.LadderClimbProgress, true);
            return true;
        }
        if (!_player.CanMountLadder)
        {
            return false;
        }

        RoofAccessRoute? nearest = null;
        var startAtTop = false;
        var nearestDistance = RoofAccessUseRange;
        foreach (var route in _roofAccessRoutes)
        {
            if (route.Kind != VerticalAccessKind.Ladder)
            {
                continue;
            }
            var bottomDistance = _player.GlobalPosition.DistanceTo(route.BottomFeet);
            if (bottomDistance < nearestDistance && IsRoofAccessEndpointApproachable(route, startAtTop: false))
            {
                nearest = route;
                startAtTop = false;
                nearestDistance = bottomDistance;
            }
            var topDistance = _player.GlobalPosition.DistanceTo(route.TopFeet);
            if (topDistance < nearestDistance && IsRoofAccessEndpointApproachable(route, startAtTop: true))
            {
                nearest = route;
                startAtTop = true;
                nearestDistance = topDistance;
            }
        }
        if (nearest is null)
        {
            return false;
        }

        _lootSearchTarget = null;
        _player.SetSearchPose(false);
        var verb = GameLocalization.Get(
            startAtTop ? "climb_down" : "climb_up",
            _languageSetting,
            startAtTop ? "CLIMB DOWN" : "CLIMB TO ROOF");
        _hud.SetInteraction($"{verb}  //  {nearest.Building}", -1.0f, true);
        if (!_interactReleaseRequired && Input.IsActionJustPressed(GameInputActions.Interact))
        {
            _interactReleaseRequired = true;
            if (!_player.BeginLadderClimb(
                    nearest.BottomFeet,
                    nearest.TopFeet,
                    nearest.Outward,
                    startAtTop))
            {
                _hud.ShowLocalizedMessage(
                    "climb_blocked",
                    "LADDER PATH BLOCKED",
                    new Color(1.0f, 0.38f, 0.22f));
            }
        }
        return true;
    }

    private bool IsRoofAccessEndpointApproachable(RoofAccessRoute route, bool startAtTop)
    {
        var endpoint = startAtTop ? route.TopFeet : route.BottomFeet;
        var offset = _player.GlobalPosition - endpoint;
        offset.Y = 0.0f;
        var side = offset.Dot(route.Outward);
        if ((!startAtTop && side < -0.65f) || (startAtTop && side > 0.85f))
        {
            return false;
        }

        var from = _player.GlobalPosition + Vector3.Up * 1.15f;
        var to = endpoint + Vector3.Up * 1.15f;
        var distance = from.DistanceTo(to);
        if (distance < 0.2f)
        {
            return true;
        }
        return !PhysicsRaycast.TryHit(
                GetWorld3D().DirectSpaceState,
                from,
                to,
                _player.GetRid(),
                1,
                out var hit)
            || from.DistanceTo(hit.Position) >= distance - 0.22f;
    }

    private async void ValidateRoofAccess()
    {
        DisableActorsForSurvivalDiagnostics();
        await WaitFrames(5);

        var ids = _roofAccessRoutes.Select(route => route.Id).ToHashSet(StringComparer.Ordinal);
        var required = new[]
        {
            "WarehouseRoof", "FuelCanopyRoof", "BarracksRoof", "CommandPodRoof", "RadarPodRoof",
            "CustomsWarehouseRoof", "OpsAnnexRoof", "FuelLogisticsRoof", "QuayStorageRoof",
            "DispatchOfficeRoof", "RailCanopyRoof", "MaintenanceHangarRoof", "TankControlRoof",
            "SeawallShelterRoof", "BazaarAuctionDeck", "TideglassCatwalk", "ObservatoryDeck",
            "ObservatoryCoreRoof", "DrydockWestCatwalk", "DrydockEastCatwalk", "DrydockCraneRoof"
        };
        var coverageReady = required.All(ids.Contains)
            && Enumerable.Range(1, ResidentialTowerSpecs.Length)
                .All(index => ids.Contains($"ResidentialTower{index:00}"));
        var visualReady = true;
        var bottomFloorReady = true;
        var topFloorReady = true;
        var landingClearanceReady = true;
        var missingBottomFloors = new List<string>();
        var missingTopFloors = new List<string>();
        var blockedLandings = new List<string>();
        var blockedPaths = new List<string>();
        var ladderGeometryCount = 0;
        foreach (var route in _roofAccessRoutes)
        {
            if (!HasRoofAccessFloor(route.BottomFeet, 1.35f))
            {
                bottomFloorReady = false;
                missingBottomFloors.Add(route.Id);
            }
            if (!HasRoofAccessFloor(route.TopFeet, 1.35f))
            {
                topFloorReady = false;
                missingTopFloors.Add(route.Id);
            }
            if (!HasRoofAccessLandingClearance(route.TopFeet))
            {
                landingClearanceReady = false;
                blockedLandings.Add(route.Id);
            }
            if (route.Kind != VerticalAccessKind.Ladder)
            {
                continue;
            }
            if (!_player.CanTraverseLadderPath(route.BottomFeet, route.TopFeet, route.Outward))
            {
                blockedPaths.Add($"{route.Id}:{_player.LadderPathBlockerForDiagnostics}");
            }
            if (route.VisualRoot is null)
            {
                continue;
            }
            var visualChildren = route.VisualRoot.GetChildren();
            using var visualChildrenBacking = visualChildren.AsDisposable();
            var visual = visualChildren.OfType<MultiMeshInstance3D>().FirstOrDefault();
            var instanceCount = visual?.Multimesh?.InstanceCount ?? 0;
            visualReady &= visual is not null && instanceCount >= 5;
            ladderGeometryCount += instanceCount;
        }
        var pathClearanceReady = blockedPaths.Count == 0;

        var representative = _roofAccessRoutes.First(route => route.Id == "WarehouseRoof");
        _player.GlobalPosition = representative.BottomFeet;
        _player.Velocity = Vector3.Zero;
        _player.GrantFireablePrimaryForDiagnostics();
        _player.RestoreMovementInput();
        Input.ActionRelease("interact");
        Input.ActionRelease("move_forward");
        Input.ActionRelease("move_backward");
        Input.ActionRelease("move_right");
        Input.ActionRelease("fire");
        _interactReleaseRequired = false;
        await WaitFrames(2);

        Input.ActionPress("interact");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var mounted = _player.IsClimbingLadder;
        Input.ActionRelease("interact");
        await WaitFrames(2);
        var startedLow = _player.GlobalPosition.Y < representative.TopFeet.Y - 4.0f;
        var collisionActive = mounted && _player.HasActiveLadderCollisionForDiagnostics;
        var ammoBefore = _player.Ammo;
        var progressBefore = _player.LadderClimbProgress;
        Input.ActionPress("move_forward");
        for (var frame = 0; frame < 18; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
        Input.ActionRelease("move_forward");
        var climbedWithInput = _player.IsClimbingLadder
            && _player.LadderClimbProgress > progressBefore + 0.035f;
        var lateralAnchor = new Vector2(_player.GlobalPosition.X, _player.GlobalPosition.Z);
        Input.ActionPress("move_right");
        Input.ActionPress("fire");
        for (var frame = 0; frame < 8; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
        Input.ActionRelease("move_right");
        Input.ActionRelease("fire");
        var lateralBlocked = lateralAnchor.DistanceTo(new Vector2(
            _player.GlobalPosition.X,
            _player.GlobalPosition.Z)) < 0.025f;
        var fireBlocked = _player.Ammo == ammoBefore;
        var cancelY = _player.GlobalPosition.Y;
        Input.ActionPress("interact");
        await WaitFrames(2);
        Input.ActionRelease("interact");
        await WaitFrames(2);
        var cancelledWithoutRemount = !_player.IsClimbingLadder;
        var cancelledWithoutTeleport = Mathf.Abs(_player.GlobalPosition.Y - cancelY) < 0.75f
            && _player.GlobalPosition.DistanceTo(representative.TopFeet) > 2.0f;
        var collisionRestored = _player.HasActiveLadderCollisionForDiagnostics;

        var remountReadyAfterCancel = await WaitForLadderRemountForDiagnostics();
        _player.GlobalPosition = representative.BottomFeet;
        _player.Velocity = Vector3.Zero;
        var remounted = remountReadyAfterCancel && _player.BeginLadderClimb(
            representative.BottomFeet,
            representative.TopFeet,
            representative.Outward);
        Input.ActionPress("move_forward");
        for (var frame = 0; frame < 340 && _player.IsClimbingLadder; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
        Input.ActionRelease("move_forward");
        var climbedToRoof = remounted
            && !_player.IsClimbingLadder
            && _player.GlobalPosition.DistanceTo(representative.TopFeet) < 0.8f;

        var remountReadyAtTop = await WaitForLadderRemountForDiagnostics();
        var mountedAtTop = remountReadyAtTop && _player.BeginLadderClimb(
            representative.BottomFeet,
            representative.TopFeet,
            representative.Outward,
            startAtTop: true);
        Input.ActionPress("move_backward");
        for (var frame = 0; frame < 340 && _player.IsClimbingLadder; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
        Input.ActionRelease("move_backward");
        var climbedDown = mountedAtTop
            && !_player.IsClimbingLadder
            && _player.GlobalPosition.DistanceTo(representative.BottomFeet) < 0.8f;
        var downProgress = _player.LadderClimbProgress;
        var downPosition = _player.GlobalPosition;
        var downDistance = downPosition.DistanceTo(representative.BottomFeet);
        var inputFlowReady = climbedWithInput
            && lateralBlocked
            && fireBlocked
            && cancelledWithoutRemount
            && cancelledWithoutTeleport
            && climbedToRoof
            && climbedDown;

        var expectedLadders = 26;
        var valid = _roofAccessRoot is not null
            && coverageReady
            && FunctionalLadderCount >= expectedLadders
            && _roofAccessRoutes.Count >= expectedLadders + ResidentialTowerSpecs.Length + 5
            && visualReady
            && ladderGeometryCount >= 260
            && bottomFloorReady
            && topFloorReady
            && landingClearanceReady
            && pathClearanceReady
            && mounted
            && startedLow
            && collisionActive
            && collisionRestored
            && inputFlowReady;
        GD.Print($"ROOF_ACCESS_CHECK valid={valid} routes={_roofAccessRoutes.Count} ladders={FunctionalLadderCount}/{expectedLadders} residential={_residentialRoofAccessCount}/{ResidentialTowerSpecs.Length} coverage={coverageReady} visuals={visualReady} instances={ladderGeometryCount} bottom_floor={bottomFloorReady} missing_bottom={string.Join(',', missingBottomFloors)} top_floor={topFloorReady} missing_top={string.Join(',', missingTopFloors)} landing_clear={landingClearanceReady} blocked={string.Join(',', blockedLandings)} path_clear={pathClearanceReady} path_blocked={string.Join(',', blockedPaths)} mounted={mounted} collision={collisionActive}/{collisionRestored} input_up={climbedWithInput} lateral_blocked={lateralBlocked} fire_blocked={fireBlocked} cancel={cancelledWithoutRemount} no_teleport={cancelledWithoutTeleport} reached_roof={climbedToRoof} mounted_top={mountedAtTop} reached_ground={climbedDown} down_active={_player.IsClimbingLadder} down_progress={downProgress:0.000} down_pos={downPosition.X:0.00},{downPosition.Y:0.00},{downPosition.Z:0.00} down_distance={downDistance:0.00}");
        GD.Print($"ROOF_ACCESS_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }

    private async System.Threading.Tasks.Task<bool> WaitForLadderRemountForDiagnostics()
    {
        var deadline = Time.GetTicksMsec() + 1000;
        while (!_player.CanMountLadder && Time.GetTicksMsec() < deadline)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        return _player.CanMountLadder;
    }

    private bool HasRoofAccessFloor(Vector3 feet, float downDistance)
    {
        return PhysicsRaycast.HasHit(
            GetWorld3D().DirectSpaceState,
            feet + Vector3.Up * 0.65f,
            feet + Vector3.Down * downDistance,
            _player.GetRid(),
            1);
    }

    private bool HasRoofAccessLandingClearance(Vector3 feet)
    {
        return !PhysicsRaycast.HasHit(
            GetWorld3D().DirectSpaceState,
            feet + Vector3.Up * 0.18f,
            feet + Vector3.Up * 1.82f,
            _player.GetRid(),
            1);
    }

    private async void CaptureRoofAccess()
    {
        DisableActorsForSurvivalDiagnostics();
        _hud.Visible = false;
        _player.ProcessMode = ProcessModeEnum.Disabled;
        var camera = new Camera3D { Name = "RoofAccessCaptureCamera", Fov = 62.0f, Far = 420.0f };
        AddChild(camera);
        camera.GlobalPosition = new Vector3(58.0f, 24.0f, -41.0f);
        camera.LookAt(new Vector3(24.0f, 7.0f, -84.0f), Vector3.Up);
        camera.MakeCurrent();
        await WaitFrames(24);
        SaveViewportImage("res://roof_access_overview.png");

        camera.GlobalPosition = new Vector3(42.0f, 2.0f, -13.0f);
        camera.LookAt(new Vector3(39.0f, 5.0f, -13.0f), Vector3.Up);
        camera.Fov = 58.0f;
        await WaitFrames(18);
        SaveViewportImage("res://roof_access_ladder.png");
        GD.Print($"ROOF_ACCESS_CAPTURE routes={RoofAccessRouteCount} ladders={FunctionalLadderCount}");
        GetTree().Quit();
    }
}
