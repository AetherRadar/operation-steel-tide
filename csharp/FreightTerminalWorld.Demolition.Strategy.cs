using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private void RefreshDemolitionStrategies(bool announce)
    {
        if (!_demolitionRoundActive && !announce)
        {
            return;
        }
        _demolitionStrategyRemaining = DemolitionStrategyRefreshDuration;
        var phase = _demolitionDevicePlanted
            ? DemolitionStrategyPhase.PostPlant
            : DemolitionStrategyPhase.Opening;
        var layout = DemolitionLayout();
        var playerSide = _demolitionMatch.PlayerSide;

        var playerTeamSide = playerSide;
        var opponentTeamSide = DemolitionOtherSide(playerSide);
        var playerSnapshots = new List<DemolitionAgentSnapshot>
        {
            Snapshot(
                "PLAYER",
                playerTeamSide,
                _player.Role,
                _player.Health / Mathf.Max(1.0f, _player.MaxHealth),
                _player.CurrentWeaponStats.EffectiveRange,
                !_player.IsDead,
                _player.IsDead,
                _player.GlobalPosition,
                layout.Origin)
        };
        foreach (var mate in _squadMates.Where(IsInstanceValid))
        {
            playerSnapshots.Add(Snapshot(
                $"MATE:{mate.SquadSlot}",
                playerTeamSide,
                mate.Role,
                mate.Health / Mathf.Max(1.0f, mate.MaxHealth),
                SquadWeaponRange(mate),
                !mate.IsBodyBag,
                mate.IsDowned,
                mate.GlobalPosition,
                layout.Origin));
        }
        var playerPlan = _demolitionStrategyPlanner.Plan(
            playerTeamSide,
            phase,
            playerSnapshots,
            _demolitionActiveSite);
        _demolitionAttackerPlan = playerTeamSide == DemolitionTeam.Attackers ? playerPlan : _demolitionAttackerPlan;
        _demolitionDefenderPlan = playerTeamSide == DemolitionTeam.Defenders ? playerPlan : _demolitionDefenderPlan;
        ApplyDemolitionSquadPlan(playerPlan, layout);

        var opponentSnapshots = new List<DemolitionAgentSnapshot>();
        foreach (var opponent in _demolitionOpponents.Where(IsInstanceValid))
        {
            var range = opponent.CarriedWeapon.Stats().EffectiveRange;
            var role = range >= 150.0f
                ? OperatorRole.Recon
                : range <= 95.0f ? OperatorRole.Assault : OperatorRole.Medic;
            opponentSnapshots.Add(Snapshot(
                opponent.Name,
                opponentTeamSide,
                role,
                opponent.CurrentHealth / 100.0f,
                range,
                !opponent.IsDead,
                false,
                opponent.GlobalPosition,
                layout.Origin));
        }
        var opponentPlan = _demolitionStrategyPlanner.Plan(
            opponentTeamSide,
            phase,
            opponentSnapshots,
            _demolitionActiveSite);
        _demolitionAttackerPlan = opponentTeamSide == DemolitionTeam.Attackers ? opponentPlan : _demolitionAttackerPlan;
        _demolitionDefenderPlan = opponentTeamSide == DemolitionTeam.Defenders ? opponentPlan : _demolitionDefenderPlan;

        var previousDefuser = _demolitionDefuser;
        var previousCarrier = _demolitionCarrier;
        _demolitionOpponentAssignments.Clear();
        _demolitionDefuser = null;
        _demolitionCarrier = null;
        foreach (var assignment in opponentPlan.Assignments)
        {
            var opponent = _demolitionOpponents.FirstOrDefault(candidate => IsInstanceValid(candidate)
                && candidate.Name == assignment.MemberId);
            if (opponent is null)
            {
                continue;
            }
            _demolitionOpponentAssignments[opponent] = assignment;
            opponent.SentryMode = false;
            if (assignment.Duty == DemolitionDuty.Defuse)
            {
                _demolitionDefuser = opponent;
            }
        }
        if (opponentTeamSide == DemolitionTeam.Attackers && !_demolitionDevicePlanted)
        {
            SelectDemolitionCarrier(opponentPlan);
        }
        if (_demolitionDefuser != previousDefuser)
        {
            _demolitionDefuser?.ResetScriptedObjectiveNavigation();
            PlanDemolitionDefuseRoute();
            _demolitionDefuseProgress = 0.0f;
        }
        if (_demolitionCarrier != previousCarrier)
        {
            _demolitionCarrier?.ResetScriptedObjectiveNavigation();
            _demolitionCarrierRoute = System.Array.Empty<Vector3>();
            _demolitionCarrierRouteIndex = 0;
            _demolitionEnemyPlantProgress = 0.0f;
        }

        if (announce && playerPlan.Assignments.Count > 0)
        {
            _hud.ShowRadioMessage(playerPlan.Callout, new Color(0.35f, 0.82f, 1.0f));
        }
    }

    private void SelectDemolitionCarrier(DemolitionStrategyPlan opponentPlan)
    {
        var entry = opponentPlan.Assignments.FirstOrDefault(assignment =>
            assignment.Duty is DemolitionDuty.Entry or DemolitionDuty.Support or DemolitionDuty.Recon);
        if (entry.MemberId is null)
        {
            _demolitionCarrier = null;
            return;
        }
        _demolitionCarrier = _demolitionOpponents.FirstOrDefault(candidate =>
            IsInstanceValid(candidate) && candidate.Name == entry.MemberId && !candidate.IsDead);
        if (_demolitionCarrier is not null && opponentPlan.PrimarySiteIndex >= 0)
        {
            _demolitionEnemyTargetSite = opponentPlan.PrimarySiteIndex;
        }
    }

    private void ApplyDemolitionSquadPlan(DemolitionStrategyPlan plan, DemolitionArenaLayout layout)
    {
        foreach (var assignment in plan.Assignments)
        {
            if (!assignment.MemberId.StartsWith("MATE:", System.StringComparison.Ordinal))
            {
                continue;
            }
            if (!int.TryParse(assignment.MemberId.Substring(5), out var slot))
            {
                continue;
            }
            var mate = _squadMates.FirstOrDefault(candidate => IsInstanceValid(candidate)
                && candidate.SquadSlot == slot
                && !candidate.IsDowned
                && !candidate.IsBodyBag);
            if (mate is null)
            {
                continue;
            }
            if (_demolitionSquadAssignmentTargets.TryGetValue(mate, out var current)
                && current == assignment.TargetKey)
            {
                continue;
            }
            _demolitionSquadAssignmentTargets[mate] = assignment.TargetKey;
            mate.SetOrder(SquadOrder.Move, layout.StrategyTarget(assignment.TargetKey));
        }
    }

    private static DemolitionAgentSnapshot Snapshot(
        string memberId,
        DemolitionTeam team,
        OperatorRole role,
        float healthRatio,
        float weaponRange,
        bool alive,
        bool downed,
        Vector3 position,
        Vector3 origin)
    {
        return new DemolitionAgentSnapshot(
            memberId,
            team,
            role,
            Mathf.Clamp(healthRatio, 0.0f, 1.0f),
            weaponRange,
            alive,
            downed,
            position.X - origin.X,
            position.Z - origin.Z);
    }

    private static float SquadWeaponRange(SquadMate mate) => mate.Role switch
    {
        OperatorRole.Recon => 165.0f,
        OperatorRole.Assault => 92.0f,
        _ => 118.0f
    };

    private void SelectDemolitionDefuser()
    {
        if (!_demolitionDevicePlanted || _demolitionActiveSite < 0)
        {
            _demolitionDefuser = null;
            return;
        }
        if (IsInstanceValid(_demolitionDefuser) && !_demolitionDefuser!.IsDead)
        {
            return;
        }
        RefreshDemolitionStrategies(false);
        if (IsInstanceValid(_demolitionDefuser) && !_demolitionDefuser!.IsDead)
        {
            return;
        }
        var devicePosition = DemolitionLayout().SitePositions[_demolitionActiveSite];
        _demolitionDefuser = _demolitionOpponents
            .Where(opponent => IsInstanceValid(opponent) && !opponent.IsDead)
            .OrderBy(opponent => opponent.GlobalPosition.DistanceSquaredTo(devicePosition))
            .FirstOrDefault();
        _demolitionDefuser?.ResetScriptedObjectiveNavigation();
        PlanDemolitionDefuseRoute();
        _demolitionDefuseProgress = 0.0f;
    }

    private void PlanDemolitionDefuseRoute()
    {
        _demolitionDefuseRoute = System.Array.Empty<Vector3>();
        _demolitionDefuseRouteIndex = 0;
        if (!IsInstanceValid(_demolitionDefuser) || _demolitionActiveSite < 0)
        {
            return;
        }

        var destination = DemolitionLayout().SitePositions[_demolitionActiveSite];
        if (_demolitionDefuser!.IsScriptedObjectiveCorridorClear(destination))
        {
            _demolitionDefuseRoute = new[] { destination };
            return;
        }

        var start = _demolitionDefuser.GlobalPosition;
        var forward = destination - start;
        forward.Y = 0.0f;
        forward = forward.Normalized();
        var side = new Vector3(-forward.Z, 0.0f, forward.X);
        var bestLength = float.PositiveInfinity;
        foreach (var sideSign in new[] { 1.0f, -1.0f })
        {
            foreach (var lateral in new[] { 2.5f, 3.5f, 4.5f, 5.5f })
            {
                foreach (var forwardOffset in new[] { 0.0f, 2.0f, 4.0f })
                {
                    var waypoint = start + side * (sideSign * lateral) + forward * forwardOffset;
                    waypoint.Y = start.Y;
                    if (!_demolitionDefuser.IsScriptedObjectiveCorridorClear(waypoint))
                    {
                        continue;
                    }

                    var oldPosition = _demolitionDefuser.GlobalPosition;
                    _demolitionDefuser.GlobalPosition = waypoint;
                    var destinationClear = _demolitionDefuser.IsScriptedObjectiveCorridorClear(destination);
                    _demolitionDefuser.GlobalPosition = oldPosition;
                    if (!destinationClear)
                    {
                        continue;
                    }

                    var length = HorizontalDistance(start, waypoint) + HorizontalDistance(waypoint, destination);
                    if (length < bestLength)
                    {
                        bestLength = length;
                        _demolitionDefuseRoute = new[] { waypoint, destination };
                    }
                }
            }
        }

        if (_demolitionDefuseRoute.Length == 0)
        {
            _demolitionDefuseRoute = new[] { destination };
        }
    }

    /// <summary>Drives every demolition enemy: defenders defuse, attackers carry and plant.</summary>
    public bool TryHandleDemolitionDefenderMovement(EnemyOperator opponent, float delta, Node3D? combatTarget)
    {
        if (!_demolitionMode
            || !_demolitionRoundActive
            || opponent.IsDead
            || !_demolitionOpponentAssignments.TryGetValue(opponent, out var assignment))
        {
            return false;
        }

        var targetDistance = combatTarget is null || !IsInstanceValid(combatTarget)
            ? float.PositiveInfinity
            : opponent.GlobalPosition.DistanceTo(combatTarget.GlobalPosition);
        if (_demolitionMatch.PlayerSide == DemolitionTeam.Attackers)
        {
            if (_demolitionDevicePlanted
                && opponent == _demolitionDefuser
                && assignment.Duty == DemolitionDuty.Defuse
                && _demolitionActiveSite >= 0)
            {
                return TryHandleDemolitionDefuserMovement(opponent, delta, targetDistance);
            }
        }
        else if (TryHandleDemolitionAttackerMovement(opponent, delta, targetDistance, assignment))
        {
            return true;
        }
        if (targetDistance < 14.0f)
        {
            return false;
        }
        return MoveDemolitionOpponentToward(
            opponent,
            DemolitionLayout().StrategyTarget(assignment.TargetKey),
            delta,
            2.0f,
            assignment.Duty is DemolitionDuty.Retake or DemolitionDuty.Flank ? 5.8f : 4.8f);
    }

    private bool TryHandleDemolitionAttackerMovement(
        EnemyOperator opponent,
        float delta,
        float targetDistance,
        DemolitionAssignment assignment)
    {
        if (_demolitionDevicePlanted)
        {
            return false;
        }
        if (opponent != _demolitionCarrier)
        {
            return false;
        }
        if (targetDistance < 12.0f)
        {
            _demolitionEnemyPlantProgress = Mathf.Max(0.0f, _demolitionEnemyPlantProgress - delta * 0.35f);
            return true;
        }
        var layout = DemolitionLayout();
        var site = layout.SitePositions[Mathf.Clamp(_demolitionEnemyTargetSite, 0, layout.SitePositions.Count - 1)];
        var flatSite = new Vector3(site.X, opponent.GlobalPosition.Y, site.Z);
        var distance = opponent.GlobalPosition.DistanceTo(flatSite);
        if (distance > 2.15f)
        {
            if (_demolitionCarrierRoute.Length == 0)
            {
                _demolitionCarrierRoute = new[] { site };
            }
            while (_demolitionCarrierRouteIndex < _demolitionCarrierRoute.Length - 1
                && HorizontalDistance(opponent.GlobalPosition, _demolitionCarrierRoute[_demolitionCarrierRouteIndex]) < 0.85f)
            {
                _demolitionCarrierRouteIndex++;
                opponent.ResetScriptedObjectiveNavigation();
            }
            var movementTarget = _demolitionCarrierRoute[
                Mathf.Clamp(_demolitionCarrierRouteIndex, 0, _demolitionCarrierRoute.Length - 1)];
            MoveDemolitionOpponentToward(opponent, movementTarget, delta, 0.72f, 5.1f);
            return true;
        }

        var velocity = opponent.Velocity;
        velocity.X = Mathf.MoveToward(velocity.X, 0.0f, delta * 18.0f);
        velocity.Z = Mathf.MoveToward(velocity.Z, 0.0f, delta * 18.0f);
        opponent.Velocity = velocity;
        _demolitionEnemyPlantProgress = Mathf.Min(1.0f, _demolitionEnemyPlantProgress + delta / DemolitionPlantDuration);
        if (_demolitionEnemyPlantProgress >= 1.0f)
        {
            PlantDemolitionDevice(_demolitionEnemyTargetSite, byPlayerTeam: false);
        }
        return true;
    }

    private bool TryHandleDemolitionDefuserMovement(EnemyOperator opponent, float delta, float targetDistance)
    {
        if (targetDistance < 12.0f)
        {
            _demolitionDefuseProgress = Mathf.Max(0.0f, _demolitionDefuseProgress - delta * 0.35f);
            return false;
        }
        var devicePosition = DemolitionLayout().SitePositions[_demolitionActiveSite];
        var flatDevice = new Vector3(devicePosition.X, opponent.GlobalPosition.Y, devicePosition.Z);
        var distance = opponent.GlobalPosition.DistanceTo(flatDevice);
        if (distance > 2.15f)
        {
            if (_demolitionDefuseRoute.Length == 0)
            {
                PlanDemolitionDefuseRoute();
            }
            while (_demolitionDefuseRouteIndex < _demolitionDefuseRoute.Length - 1
                && HorizontalDistance(opponent.GlobalPosition, _demolitionDefuseRoute[_demolitionDefuseRouteIndex]) < 0.85f)
            {
                _demolitionDefuseRouteIndex++;
                opponent.ResetScriptedObjectiveNavigation();
            }
            var movementTarget = _demolitionDefuseRoute[
                Mathf.Clamp(_demolitionDefuseRouteIndex, 0, _demolitionDefuseRoute.Length - 1)];
            return MoveDemolitionOpponentToward(opponent, movementTarget, delta, 0.72f, 5.3f);
        }

        var velocity = opponent.Velocity;
        velocity.X = Mathf.MoveToward(velocity.X, 0.0f, delta * 18.0f);
        velocity.Z = Mathf.MoveToward(velocity.Z, 0.0f, delta * 18.0f);
        opponent.Velocity = velocity;
        _demolitionDefuseProgress = Mathf.Min(1.0f, _demolitionDefuseProgress + delta / DemolitionDefuseDuration);
        if (_demolitionDefuseProgress >= 1.0f)
        {
            var siteName = ((char)('A' + _demolitionActiveSite)).ToString();
            FinishDemolitionRound(
                false,
                GameLocalization.Format(
                    "demolition_device_defused",
                    _languageSetting,
                    "SITE {0} DEVICE DEFUSED",
                    siteName));
        }
        return true;
    }

    private static bool MoveDemolitionOpponentToward(
        EnemyOperator opponent,
        Vector3 target,
        float delta,
        float stoppingDistance,
        float speed)
    {
        target.Y = opponent.GlobalPosition.Y;
        var distance = HorizontalDistance(opponent.GlobalPosition, target);
        var velocity = opponent.Velocity;
        if (distance <= stoppingDistance)
        {
            velocity.X = Mathf.MoveToward(velocity.X, 0.0f, delta * 14.0f);
            velocity.Z = Mathf.MoveToward(velocity.Z, 0.0f, delta * 14.0f);
            opponent.Velocity = velocity;
            return true;
        }

        var direction = opponent.ResolveScriptedObjectiveDirection(target, delta);
        direction.Y = 0.0f;
        if (direction.LengthSquared() <= 0.01f)
        {
            return false;
        }
        direction = direction.Normalized();
        opponent.LookAt(opponent.GlobalPosition + direction, Vector3.Up);
        velocity.X = Mathf.MoveToward(velocity.X, direction.X * speed, delta * 12.0f);
        velocity.Z = Mathf.MoveToward(velocity.Z, direction.Z * speed, delta * 12.0f);
        opponent.Velocity = velocity;
        return true;
    }
}
