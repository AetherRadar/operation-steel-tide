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
        var attackerSnapshots = new List<DemolitionAgentSnapshot>
        {
            Snapshot(
                "PLAYER",
                DemolitionTeam.Attackers,
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
            attackerSnapshots.Add(Snapshot(
                $"MATE:{mate.SquadSlot}",
                DemolitionTeam.Attackers,
                mate.Role,
                mate.Health / Mathf.Max(1.0f, mate.MaxHealth),
                SquadWeaponRange(mate),
                !mate.IsBodyBag,
                mate.IsDowned,
                mate.GlobalPosition,
                layout.Origin));
        }
        _demolitionAttackerPlan = _demolitionStrategyPlanner.Plan(
            DemolitionTeam.Attackers,
            phase,
            attackerSnapshots,
            _demolitionActiveSite);
        ApplyDemolitionSquadPlan(_demolitionAttackerPlan);

        var defenderSnapshots = new List<DemolitionAgentSnapshot>();
        foreach (var defender in _demolitionDefenders.Where(IsInstanceValid))
        {
            var range = defender.CarriedWeapon.Stats().EffectiveRange;
            var role = range >= 150.0f
                ? OperatorRole.Recon
                : range <= 95.0f ? OperatorRole.Assault : OperatorRole.Medic;
            defenderSnapshots.Add(Snapshot(
                defender.Name,
                DemolitionTeam.Defenders,
                role,
                defender.CurrentHealth / 100.0f,
                range,
                !defender.IsDead,
                false,
                defender.GlobalPosition,
                layout.Origin));
        }

        var previousDefuser = _demolitionDefuser;
        _demolitionDefenderPlan = _demolitionStrategyPlanner.Plan(
            DemolitionTeam.Defenders,
            phase,
            defenderSnapshots,
            _demolitionActiveSite);
        _demolitionDefenderAssignments.Clear();
        _demolitionDefuser = null;
        foreach (var assignment in _demolitionDefenderPlan.Assignments)
        {
            var defender = _demolitionDefenders.FirstOrDefault(candidate => IsInstanceValid(candidate)
                && candidate.Name == assignment.MemberId);
            if (defender is null)
            {
                continue;
            }
            _demolitionDefenderAssignments[defender] = assignment;
            defender.SentryMode = false;
            if (assignment.Duty == DemolitionDuty.Defuse)
            {
                _demolitionDefuser = defender;
            }
        }
        if (_demolitionDefuser != previousDefuser)
        {
            _demolitionDefuser?.ResetScriptedObjectiveNavigation();
            PlanDemolitionDefuseRoute();
            _demolitionDefuseProgress = 0.0f;
        }

        if (announce && _demolitionAttackerPlan.Assignments.Count > 0)
        {
            _hud.ShowRadioMessage(_demolitionAttackerPlan.Callout, new Color(0.35f, 0.82f, 1.0f));
        }
    }

    private void ApplyDemolitionSquadPlan(DemolitionStrategyPlan plan)
    {
        var layout = DemolitionLayout();
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
        _demolitionDefuser = _demolitionDefenders
            .Where(defender => IsInstanceValid(defender) && !defender.IsDead)
            .OrderBy(defender => defender.GlobalPosition.DistanceSquaredTo(devicePosition))
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

    public bool TryHandleDemolitionDefenderMovement(EnemyOperator defender, float delta, Node3D? combatTarget)
    {
        if (!_demolitionMode
            || !_demolitionRoundActive
            || defender.IsDead
            || !_demolitionDefenderAssignments.TryGetValue(defender, out var assignment))
        {
            return false;
        }

        var targetDistance = combatTarget is null || !IsInstanceValid(combatTarget)
            ? float.PositiveInfinity
            : defender.GlobalPosition.DistanceTo(combatTarget.GlobalPosition);
        if (_demolitionDevicePlanted
            && defender == _demolitionDefuser
            && assignment.Duty == DemolitionDuty.Defuse
            && _demolitionActiveSite >= 0)
        {
            return TryHandleDemolitionDefuserMovement(defender, delta, targetDistance);
        }
        if (targetDistance < 14.0f)
        {
            return false;
        }
        return MoveDemolitionDefenderToward(
            defender,
            DemolitionLayout().StrategyTarget(assignment.TargetKey),
            delta,
            2.0f,
            assignment.Duty is DemolitionDuty.Retake or DemolitionDuty.Flank ? 5.8f : 4.8f);
    }

    private bool TryHandleDemolitionDefuserMovement(EnemyOperator defender, float delta, float targetDistance)
    {
        if (targetDistance < 12.0f)
        {
            _demolitionDefuseProgress = Mathf.Max(0.0f, _demolitionDefuseProgress - delta * 0.35f);
            return false;
        }
        var devicePosition = DemolitionLayout().SitePositions[_demolitionActiveSite];
        var flatDevice = new Vector3(devicePosition.X, defender.GlobalPosition.Y, devicePosition.Z);
        var distance = defender.GlobalPosition.DistanceTo(flatDevice);
        if (distance > 2.15f)
        {
            if (_demolitionDefuseRoute.Length == 0)
            {
                PlanDemolitionDefuseRoute();
            }
            while (_demolitionDefuseRouteIndex < _demolitionDefuseRoute.Length - 1
                && HorizontalDistance(defender.GlobalPosition, _demolitionDefuseRoute[_demolitionDefuseRouteIndex]) < 0.85f)
            {
                _demolitionDefuseRouteIndex++;
                defender.ResetScriptedObjectiveNavigation();
            }
            var movementTarget = _demolitionDefuseRoute[
                Mathf.Clamp(_demolitionDefuseRouteIndex, 0, _demolitionDefuseRoute.Length - 1)];
            return MoveDemolitionDefenderToward(defender, movementTarget, delta, 0.72f, 5.3f);
        }

        var velocity = defender.Velocity;
        velocity.X = Mathf.MoveToward(velocity.X, 0.0f, delta * 18.0f);
        velocity.Z = Mathf.MoveToward(velocity.Z, 0.0f, delta * 18.0f);
        defender.Velocity = velocity;
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

    private static bool MoveDemolitionDefenderToward(
        EnemyOperator defender,
        Vector3 target,
        float delta,
        float stoppingDistance,
        float speed)
    {
        target.Y = defender.GlobalPosition.Y;
        var distance = HorizontalDistance(defender.GlobalPosition, target);
        var velocity = defender.Velocity;
        if (distance <= stoppingDistance)
        {
            velocity.X = Mathf.MoveToward(velocity.X, 0.0f, delta * 14.0f);
            velocity.Z = Mathf.MoveToward(velocity.Z, 0.0f, delta * 14.0f);
            defender.Velocity = velocity;
            return true;
        }

        var direction = defender.ResolveScriptedObjectiveDirection(target, delta);
        direction.Y = 0.0f;
        if (direction.LengthSquared() <= 0.01f)
        {
            return false;
        }
        direction = direction.Normalized();
        defender.LookAt(defender.GlobalPosition + direction, Vector3.Up);
        velocity.X = Mathf.MoveToward(velocity.X, direction.X * speed, delta * 12.0f);
        velocity.Z = Mathf.MoveToward(velocity.Z, direction.Z * speed, delta * 12.0f);
        defender.Velocity = velocity;
        return true;
    }
}
