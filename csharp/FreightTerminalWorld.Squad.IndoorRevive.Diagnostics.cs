using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private async void ValidateSquadIndoorRevive()
    {
        var directBlocked = false;
        var ordinaryTrailBlocked = false;
        var emergencyTrailAttached = false;
        var entranceLinkRegistered = false;
        var handoffLinkRegistered = false;
        var layeredRouteReady = false;
        var layeredEntranceSeen = false;
        var layeredStairSeen = false;
        var handoffRouteReady = false;
        var layeredHandoffSeen = false;
        var assigned = false;
        var entered = false;
        var climbed = false;
        var access = false;
        var rescued = false;
        var gridRescue = false;
        var trailRescue = false;
        var activeRouteSeen = false;
        var activeStepSeen = false;
        var maximumGridCursor = -1;
        var activeRouteState = string.Empty;
        var rescueReplans = 0;
        var recoveries = 0;
        var maximumGain = 0.0f;
        var ordinaryTrailIndex = -1;
        var emergencyTrailIndex = -1;
        var target = Vector3.Zero;
        var downedPlayerPosition = Vector3.Zero;
        var finalPlayerPosition = Vector3.Zero;
        var finalMatePosition = Vector3.Zero;
        var routeState = string.Empty;
        var failure = string.Empty;

        try
        {
            await WaitFrames(10);
            _missionDirector.ExitDeploymentZone();
            _missionDirector.ProcessMode = ProcessModeEnum.Disabled;
            var mate = _squadMates.FirstOrDefault(candidate =>
                IsInstanceValid(candidate) && !candidate.IsHumanProxy && !candidate.IsDowned);
            if (mate is null || _residentialTowers.Count == 0)
            {
                throw new InvalidOperationException("missing residential tower or squad mate");
            }

            foreach (var candidate in _squadMates.Where(candidate =>
                         IsInstanceValid(candidate) && candidate != mate))
            {
                candidate.ProcessMode = ProcessModeEnum.Disabled;
                candidate.GlobalPosition = new Vector3(
                    420.0f + candidate.SquadSlot * 3.0f,
                    0.3f,
                    420.0f);
            }
            foreach (var enemy in _enemies.Where(IsInstanceValid))
            {
                enemy.ProcessMode = ProcessModeEnum.Disabled;
                enemy.GlobalPosition = new Vector3(440.0f, 0.3f, 440.0f);
            }
            foreach (var civilian in _civilians.Where(IsInstanceValid))
            {
                civilian.ProcessMode = ProcessModeEnum.Disabled;
                civilian.GlobalPosition = new Vector3(460.0f, 0.3f, 460.0f);
            }

            var tower = _residentialTowers[0];
            var spec = ResidentialTowerSpecs[0];
            var coreZ = -Mathf.Min(spec.Footprint.Y * 0.18f, 3.6f);
            var entranceRoute = BuildResidentialEntranceNavigationRoute(tower, spec, coreZ);
            var stairRoute = BuildResidentialStairNavigationRoute(
                tower,
                floorY: 0.0f,
                coreZ,
                laneOffset: 0.0f,
                descending: false);
            target = tower.ToGlobal(new Vector3(
                0.0f,
                ResidentialFloorHeight + 0.14f,
                coreZ + ResidentialStairOpeningSouthDepth + 3.0f));
            var start = tower.ToGlobal(new Vector3(
                7.0f,
                0.12f,
                spec.Footprint.Y * 0.5f + 3.0f));
            var trail = new List<Vector3>();
            AppendIndoorReviveTrailSamples(trail, new[] { start });
            AppendIndoorReviveTrailSamples(trail, entranceRoute);
            AppendIndoorReviveTrailSamples(trail, stairRoute.Skip(1));
            AppendIndoorReviveTrailSamples(trail, new[] { target });

            _player.ProcessMode = ProcessModeEnum.Disabled;
            _player.GlobalPosition = target;
            _player.Velocity = Vector3.Zero;
            _player.SetHealthForDiagnostics(_player.MaxHealth);
            _player.SetReviveUsedForDiagnostics(false);
            _player.IsDead = false;

            mate.ProcessMode = ProcessModeEnum.Disabled;
            mate.GlobalPosition = start;
            mate.Velocity = Vector3.Zero;
            mate.ResetCombatTacticsForDiagnostics();
            mate.GrantFireablePrimaryForDiagnostics();
            mate.SetSkillCooldownForDiagnostics(999.0f);
            mate.SetOrder(SquadOrder.Follow, start);
            SetSquadLeaderTrailForDiagnostics(trail);
            await WaitFrames(4);

            directBlocked = !IsSquadMovementCorridorClear(
                mate.GlobalPosition,
                target,
                mate);
            ordinaryTrailIndex = FindLatestVisibleTrailIndex(mate, emergency: false);
            emergencyTrailIndex = FindLatestVisibleTrailIndex(mate, emergency: true);
            ordinaryTrailBlocked = ordinaryTrailIndex < 0;
            emergencyTrailAttached = emergencyTrailIndex >= 0;

            var entranceLinkId = _squadTraversalLinks.FindIndex(link =>
                link.Source == $"residential_entry:{tower.Name}");
            entranceLinkRegistered = entranceLinkId >= 0;
            var handoffLinkId = _squadTraversalLinks.FindIndex(link =>
                link.Source == $"residential_entry_handoff:{tower.Name}");
            handoffLinkRegistered = handoffLinkId >= 0;
            mate.GlobalPosition = entranceRoute[0];
            layeredRouteReady = TryPlanSquadLayeredRoute(
                mate,
                stairRoute[^1],
                new SquadNavSearchBudget(1800, 12.0),
                out var layeredDirectives,
                out _);
            mate.GlobalPosition = start;
            if (layeredRouteReady)
            {
                layeredEntranceSeen = entranceLinkId >= 0
                    && layeredDirectives.Any(directive =>
                        directive.Required
                        && directive.DirectedEdgeId / 2 == entranceLinkId);
                layeredStairSeen = layeredDirectives.Any(directive =>
                    directive.Required
                    && directive.Kind == SquadTraversalKind.Step);
                routeState = string.Join(
                    ',',
                    layeredDirectives
                        .Where(static directive => directive.Required)
                        .Select(static directive =>
                            $"{directive.Kind}:{directive.DirectedEdgeId}"));
            }
            mate.GlobalPosition = entranceRoute[Math.Max(1, entranceRoute.Count - 5)];
            handoffRouteReady = TryPlanSquadLayeredRoute(
                mate,
                stairRoute[^1],
                new SquadNavSearchBudget(1800, 12.0),
                out var handoffDirectives,
                out _);
            layeredHandoffSeen = handoffLinkId >= 0
                && handoffDirectives.Any(directive =>
                    directive.Required
                    && directive.DirectedEdgeId / 2 == handoffLinkId);
            mate.GlobalPosition = start;

            _player.TakeDamage(999.0f, _player.HitPoint(HitRegion.Torso), this);
            if (!_player.IsDead)
            {
                _player.TakeCombatDamage(999.0f, _player.HitPoint(HitRegion.Torso), this);
            }
            if (!_player.IsDead || !_localPlayerDowned)
            {
                throw new InvalidOperationException("player did not enter the downed state");
            }
            downedPlayerPosition = _player.GlobalPosition;

            var startY = mate.GlobalPosition.Y;
            mate.ProcessMode = ProcessModeEnum.Inherit;
            for (var frame = 0; frame < 1500 && _player.IsDead; frame++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                assigned |= ReferenceEquals(_leaderReviver, mate) && mate.IsRevivingLeader;
                var local = tower.ToLocal(mate.GlobalPosition);
                entered |= local.Z < spec.Footprint.Y * 0.5f - 1.0f;
                maximumGain = Mathf.Max(maximumGain, mate.GlobalPosition.Y - startY);
                climbed |= maximumGain >= ResidentialFloorHeight - 0.55f;
                access |= mate.GlobalPosition.DistanceTo(target) <= 2.3f
                    && Mathf.Abs(mate.GlobalPosition.Y - target.Y) <= 1.25f;
                if (_squadGridPaths.TryGetValue(mate.GetInstanceId(), out var activeRoute)
                    && activeRoute.Cursor >= 0
                    && activeRoute.Cursor < activeRoute.Directives.Length)
                {
                    var current = activeRoute.Directives[activeRoute.Cursor];
                    activeRouteSeen = true;
                    activeStepSeen |= current.Kind == SquadTraversalKind.Step;
                    maximumGridCursor = Math.Max(maximumGridCursor, activeRoute.Cursor);
                    activeRouteState = $"{activeRoute.Cursor}/{activeRoute.Directives.Length}:"
                        + $"{current.Kind}:{current.DirectedEdgeId}:"
                        + $"({current.Target.X:0.00},{current.Target.Y:0.00},{current.Target.Z:0.00})";
                }
            }
            rescued = !_player.IsDead
                && _player.ReviveUsed
                && !_localPlayerDowned;
            gridRescue = LeaderRescueUsedGridForDiagnostics;
            trailRescue = LeaderRescueUsedTrailForDiagnostics;
            rescueReplans = LeaderRescueReplansForDiagnostics;
            recoveries = mate.CombatStuckRecoveries;
            access |= rescued;
            finalPlayerPosition = _player.GlobalPosition;
            finalMatePosition = mate.GlobalPosition;
        }
        catch (Exception exception)
        {
            failure = exception.GetType().Name + ":" + exception.Message;
            GD.PushError($"SQUAD_INDOOR_REVIVE_EXCEPTION {failure}");
        }

        var valid = directBlocked
            && ordinaryTrailBlocked
            && emergencyTrailAttached
            && entranceLinkRegistered
            && handoffLinkRegistered
            && layeredRouteReady
            && layeredEntranceSeen
            && layeredStairSeen
            && handoffRouteReady
            && layeredHandoffSeen
            && assigned
            && entered
            && climbed
            && access
            && rescued
            && gridRescue
            && string.IsNullOrEmpty(failure);
        GD.Print(
            $"SQUAD_INDOOR_REVIVE_CHECK valid={valid} direct_blocked={directBlocked} "
            + $"ordinary_trail_blocked={ordinaryTrailBlocked} emergency_trail={emergencyTrailAttached} "
            + $"ordinary_index={ordinaryTrailIndex} emergency_index={emergencyTrailIndex} "
            + $"entry_link={entranceLinkRegistered} layered_route={layeredRouteReady} "
            + $"layered_entry={layeredEntranceSeen} layered_stair={layeredStairSeen} "
            + $"handoff_link={handoffLinkRegistered} handoff_route={handoffRouteReady} "
            + $"layered_handoff={layeredHandoffSeen} "
            + $"assigned={assigned} entered={entered} climbed={climbed} access={access} "
            + $"rescued={rescued} grid_rescue={gridRescue} gain={maximumGain:0.00} "
            + $"trail_rescue={trailRescue} active_route={activeRouteSeen} "
            + $"active_step={activeStepSeen} max_cursor={maximumGridCursor} "
            + $"active_state={activeRouteState} replans={rescueReplans} recoveries={recoveries} "
            + $"route={routeState} failure={failure}");
        GD.Print(
            $"SQUAD_INDOOR_REVIVE_POS target=({target.X:0.00},{target.Y:0.00},{target.Z:0.00}) "
            + $"downed=({downedPlayerPosition.X:0.00},{downedPlayerPosition.Y:0.00},{downedPlayerPosition.Z:0.00}) "
            + $"player=({finalPlayerPosition.X:0.00},{finalPlayerPosition.Y:0.00},{finalPlayerPosition.Z:0.00}) "
            + $"mate=({finalMatePosition.X:0.00},{finalMatePosition.Y:0.00},{finalMatePosition.Z:0.00})");
        GD.Print($"SQUAD_INDOOR_REVIVE_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }

    private static void AppendIndoorReviveTrailSamples(
        List<Vector3> trail,
        IEnumerable<Vector3> points)
    {
        foreach (var point in points)
        {
            if (trail.Count == 0)
            {
                trail.Add(point);
                continue;
            }
            var origin = trail[^1];
            var distance = origin.DistanceTo(point);
            if (distance <= 0.01f)
            {
                continue;
            }
            var samples = Mathf.Max(1, Mathf.CeilToInt(distance / 0.75f));
            for (var sample = 1; sample <= samples; sample++)
            {
                trail.Add(origin.Lerp(point, sample / (float)samples));
            }
        }
    }
}
