using System;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private async void ValidateSquadDoorRescue()
    {
        var baselineProbeFree = false;
        var assigned = false;
        var doorRouteSelected = false;
        var routeCached = false;
        var glassProbeAvoided = false;
        var doorOpened = false;
        var crossedDoor = false;
        var rescued = false;
        var unregisteredDirectBlocked = false;
        var unregisteredOpeningPlanned = false;
        var unregisteredOpeningCrossed = false;
        var probeComputations = 0;
        var planReuses = 0;
        var failure = string.Empty;
        var mateFinal = Vector3.Zero;
        Node3D? fixture = null;
        InteractiveBuildingDoor? fixtureDoor = null;

        try
        {
            await WaitFrames(8);
            _missionDirector.ExitDeploymentZone();
            _missionDirector.ProcessMode = ProcessModeEnum.Disabled;
            var mate = _squadMates.FirstOrDefault(candidate =>
                IsInstanceValid(candidate) && !candidate.IsHumanProxy && !candidate.IsDowned);
            if (mate is null)
            {
                throw new InvalidOperationException("missing squad mate");
            }

            foreach (var other in _squadMates.Where(candidate =>
                         IsInstanceValid(candidate) && candidate != mate))
            {
                other.ProcessMode = ProcessModeEnum.Disabled;
                other.GlobalPosition = new Vector3(
                    660.0f + other.SquadSlot * 3.0f,
                    80.3f,
                    660.0f);
            }
            foreach (var enemy in _enemies.Where(IsInstanceValid))
            {
                enemy.ProcessMode = ProcessModeEnum.Disabled;
                enemy.GlobalPosition = new Vector3(680.0f, 80.3f, 680.0f);
            }
            foreach (var civilian in _civilians.Where(IsInstanceValid))
            {
                civilian.ProcessMode = ProcessModeEnum.Disabled;
                civilian.GlobalPosition = new Vector3(700.0f, 80.3f, 700.0f);
            }

            fixture = BuildSquadDoorRescueFixture(
                out var mateStart,
                out var playerTarget,
                out fixtureDoor);
            _refineryDoors.Add(fixtureDoor);
            await WaitFrames(5);

            ClearLeaderReviveAi();
            ResetAiReviveAbandonment();
            SetSquadLeaderTrailForDiagnostics(Array.Empty<Vector3>());
            _player.ProcessMode = ProcessModeEnum.Disabled;
            _player.GlobalPosition = playerTarget;
            _player.Velocity = Vector3.Zero;
            _player.SetHealthForDiagnostics(_player.MaxHealth);
            _player.SetReviveUsedForDiagnostics(false);
            _player.IsDead = false;

            mate.ProcessMode = ProcessModeEnum.Disabled;
            mate.GlobalPosition = mateStart;
            mate.Velocity = Vector3.Zero;
            mate.RestoreHealth(mate.MaxHealth);
            mate.ResetCombatTacticsForDiagnostics();
            mate.GrantFireablePrimaryForDiagnostics();
            mate.SetOrder(SquadOrder.Follow, mateStart);
            ClearSquadNavigation(mate);
            await WaitFrames(4);

            var baselineComputations = SquadEmergencyEgressProbeComputationsForDiagnostics;
            for (var sample = 0; sample < 120; sample++)
            {
                _ = ResolveSquadNavigationDestination(mate, playerTarget, emergency: false);
            }
            baselineProbeFree = SquadEmergencyEgressProbeComputationsForDiagnostics
                == baselineComputations;

            _player.SetHealthForDiagnostics(10.0f);
            _player.SetReviveUsedForDiagnostics(false);
            _player.TakeDamage(999.0f, _player.HitPoint(HitRegion.Torso), this);
            if (!_player.IsDead)
            {
                _player.TakeCombatDamage(999.0f, _player.HitPoint(HitRegion.Torso), this);
            }
            if (!_player.IsDead || !_localPlayerDowned)
            {
                throw new InvalidOperationException("player did not enter downed state");
            }

            mate.ProcessMode = ProcessModeEnum.Inherit;
            UpdateSquadReviveAi(1.0f / 60.0f);
            assigned = ReferenceEquals(_leaderReviver, mate) && mate.IsRevivingLeader;
            mate.ProcessMode = ProcessModeEnum.Disabled;
            _reviverNoProgressTime = SquadRescueEgressNoProgressSeconds;
            var probesBeforePlan = SquadEmergencyEgressProbeComputationsForDiagnostics;
            var doorPlansBefore = SquadEmergencyDoorPlansForDiagnostics;
            var reusesBefore = SquadEmergencyEgressPlanReusesForDiagnostics;
            var glassProbesBefore = mate.RescueGlassProbeComputationsForDiagnostics;
            var firstDirective = ResolveSquadNavigationDestination(
                mate,
                playerTarget,
                emergency: true);
            doorRouteSelected = HasPendingSquadEmergencyEgress(mate)
                && SquadEmergencyDoorPlansForDiagnostics == doorPlansBefore + 1
                && firstDirective.Required
                && firstDirective.PreciseTrail;
            for (var sample = 0; sample < 48; sample++)
            {
                _ = ResolveSquadNavigationDestination(mate, playerTarget, emergency: true);
            }
            probeComputations = SquadEmergencyEgressProbeComputationsForDiagnostics
                - probesBeforePlan;
            planReuses = SquadEmergencyEgressPlanReusesForDiagnostics - reusesBefore;
            routeCached = probeComputations == 1 && planReuses >= 48;
            glassProbeAvoided = mate.RescueGlassProbeComputationsForDiagnostics
                == glassProbesBefore;

            mate.ProcessMode = ProcessModeEnum.Inherit;
            var fixtureOrigin = fixture.GlobalPosition;
            for (var frame = 0; frame < 900 && _player.IsDead; frame++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                doorOpened |= fixtureDoor.TargetOpen || fixtureDoor.IsOpen;
                crossedDoor |= mate.GlobalPosition.Z < fixtureOrigin.Z - 0.35f;
            }
            rescued = !_player.IsDead && _player.ReviveUsed && !_localPlayerDowned;
            mateFinal = mate.GlobalPosition;

            _refineryDoors.Remove(fixtureDoor);
            fixtureDoor = null;
            fixture.QueueFree();
            await WaitFrames(4);
            fixture = BuildSquadOpenDoorwayRecoveryFixture(
                out var openingStart,
                out var openingTarget);
            mate.ProcessMode = ProcessModeEnum.Disabled;
            mate.GlobalPosition = openingStart;
            mate.Velocity = Vector3.Zero;
            SetSquadLeaderTrailForDiagnostics(Array.Empty<Vector3>());
            ClearSquadNavigation(mate);
            await WaitFrames(4);

            unregisteredDirectBlocked = !IsSquadMovementCorridorClear(
                openingStart,
                openingTarget,
                mate);
            var openPlansBefore = SquadEmergencyOpenCorridorPlansForDiagnostics;
            unregisteredOpeningPlanned = TryResolveSquadEmergencyRescueEgress(
                    mate,
                    openingTarget,
                    out var openingDirective)
                && HasPendingSquadEmergencyEgress(mate)
                && SquadEmergencyOpenCorridorPlansForDiagnostics == openPlansBefore + 1
                && openingDirective.Required
                && openingDirective.PreciseTrail;
            if (unregisteredOpeningPlanned)
            {
                mate.GlobalPosition = openingDirective.Target;
                unregisteredOpeningCrossed = mate.GlobalPosition.Z
                    < fixture.GlobalPosition.Z - 0.35f;
                _ = TryContinueSquadEmergencyRescueEgress(
                    mate,
                    openingTarget,
                    out _);
            }
            ClearSquadNavigation(mate);
        }
        catch (Exception exception)
        {
            failure = exception.GetType().Name + ":" + exception.Message;
            GD.PushError($"SQUAD_DOOR_RESCUE_EXCEPTION {failure}");
        }

        if (fixtureDoor is not null)
        {
            _refineryDoors.Remove(fixtureDoor);
        }
        var valid = baselineProbeFree
            && assigned
            && doorRouteSelected
            && routeCached
            && glassProbeAvoided
            && doorOpened
            && crossedDoor
            && rescued
            && unregisteredDirectBlocked
            && unregisteredOpeningPlanned
            && unregisteredOpeningCrossed
            && probeComputations == 1
            && string.IsNullOrEmpty(failure);
        GD.Print(
            $"SQUAD_DOOR_RESCUE_CHECK valid={valid} baseline_probe_free={baselineProbeFree} "
            + $"assigned={assigned} selected={doorRouteSelected} cache={routeCached} "
            + $"glass_avoided={glassProbeAvoided} opened={doorOpened} crossed={crossedDoor} "
            + $"rescued={rescued} unregistered_blocked={unregisteredDirectBlocked} "
            + $"unregistered_planned={unregisteredOpeningPlanned} "
            + $"unregistered_crossed={unregisteredOpeningCrossed} "
            + $"probes={probeComputations} reuses={planReuses} "
            + $"mate=({mateFinal.X:0.00},{mateFinal.Y:0.00},{mateFinal.Z:0.00}) failure={failure}");
        GD.Print($"SQUAD_DOOR_RESCUE_PASS valid={valid}");
        fixture?.QueueFree();
        QuitDiagnosticAfterSceneCleanup(valid ? 0 : 2);
    }

    private Node3D BuildSquadDoorRescueFixture(
        out Vector3 mateStart,
        out Vector3 playerTarget,
        out InteractiveBuildingDoor door)
    {
        var root = new Node3D
        {
            Name = "SquadDoorRescueDiagnostic",
            Position = new Vector3(620.0f, 80.0f, 620.0f)
        };
        AddChild(root);

        AddSquadTraversalBox(
            root,
            "DoorRescueFloor",
            new Vector3(0.0f, -0.15f, 0.0f),
            new Vector3(12.0f, 0.3f, 14.0f));
        AddSquadTraversalBox(
            root,
            "DoorRescueFacadeLeft",
            new Vector3(-2.45f, 1.5f, 0.0f),
            new Vector3(3.1f, 3.0f, 0.24f));
        AddSquadTraversalBox(
            root,
            "DoorRescueFacadeRight",
            new Vector3(2.45f, 1.5f, 0.0f),
            new Vector3(3.1f, 3.0f, 0.24f));
        AddSquadTraversalBox(
            root,
            "DoorRescueBackWall",
            new Vector3(0.0f, 1.5f, 6.0f),
            new Vector3(8.0f, 3.0f, 0.24f));
        AddSquadTraversalBox(
            root,
            "DoorRescueLeftWall",
            new Vector3(-4.0f, 1.5f, 3.0f),
            new Vector3(0.24f, 3.0f, 6.0f));
        AddSquadTraversalBox(
            root,
            "DoorRescueRightWall",
            new Vector3(4.0f, 1.5f, 3.0f),
            new Vector3(0.24f, 3.0f, 6.0f));

        var mount = new Node3D { Name = "DoorRescueDoorMount" };
        root.AddChild(mount);
        door = new InteractiveBuildingDoor { Name = "DoorRescueInteractiveDoor" };
        door.Configure(
            _refineryDoors.Count + 1,
            doorwayWidth: 1.45f,
            doorwayHeight: 2.65f,
            frontZ: 0.0f,
            visibilityRange: 35.0f,
            motionStyle: BuildingDoorMotionStyle.Hinged);
        mount.AddChild(door);

        mateStart = root.ToGlobal(new Vector3(0.0f, 0.08f, 3.2f));
        playerTarget = root.ToGlobal(new Vector3(0.0f, 0.08f, -3.0f));
        return root;
    }

    private Node3D BuildSquadOpenDoorwayRecoveryFixture(
        out Vector3 mateStart,
        out Vector3 target)
    {
        var root = new Node3D
        {
            Name = "SquadOpenDoorwayRecoveryDiagnostic",
            Position = new Vector3(720.0f, 80.0f, 720.0f)
        };
        AddChild(root);

        AddSquadTraversalBox(
            root,
            "OpenDoorwayFloor",
            new Vector3(0.0f, -0.15f, 0.0f),
            new Vector3(12.0f, 0.3f, 14.0f));
        AddSquadTraversalBox(
            root,
            "OpenDoorwayFacadeLeft",
            new Vector3(-3.15f, 1.5f, 0.0f),
            new Vector3(1.7f, 3.0f, 0.24f));
        AddSquadTraversalBox(
            root,
            "OpenDoorwayFacadeRight",
            new Vector3(1.8f, 1.5f, 0.0f),
            new Vector3(4.4f, 3.0f, 0.24f));
        AddSquadTraversalBox(
            root,
            "OpenDoorwayBackWall",
            new Vector3(0.0f, 1.5f, 6.0f),
            new Vector3(8.0f, 3.0f, 0.24f));
        AddSquadTraversalBox(
            root,
            "OpenDoorwayLeftWall",
            new Vector3(-4.0f, 1.5f, 3.0f),
            new Vector3(0.24f, 3.0f, 6.0f));
        AddSquadTraversalBox(
            root,
            "OpenDoorwayRightWall",
            new Vector3(4.0f, 1.5f, 3.0f),
            new Vector3(0.24f, 3.0f, 6.0f));

        mateStart = root.ToGlobal(new Vector3(0.0f, 0.08f, 3.2f));
        target = root.ToGlobal(new Vector3(0.0f, 0.08f, -3.0f));
        return root;
    }
}
