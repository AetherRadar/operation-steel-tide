using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private const float DemolitionRouteCornerTolerance = 0.3f;
    private const float DemolitionRouteWaypointStop = 0.22f;

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
            _demolitionActiveSite,
            CollectDemolitionSightings(opponentTeamSide, playerTeamSide, layout),
            strategySeed: _demolitionMatch.CurrentRound,
            siteCenters: layout.LocalSiteCoordinates,
            remainingSeconds: _demolitionRemaining);
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
            _demolitionActiveSite,
            CollectDemolitionSightings(playerTeamSide, opponentTeamSide, layout),
            strategySeed: _demolitionMatch.CurrentRound,
            siteCenters: layout.LocalSiteCoordinates,
            remainingSeconds: _demolitionRemaining);
        _demolitionAttackerPlan = opponentTeamSide == DemolitionTeam.Attackers ? opponentPlan : _demolitionAttackerPlan;
        _demolitionDefenderPlan = opponentTeamSide == DemolitionTeam.Defenders ? opponentPlan : _demolitionDefenderPlan;

        ResolveAndApplyDemolitionObjectiveChannels(opponentPlan);
        if (opponentTeamSide == DemolitionTeam.Attackers && !_demolitionDevicePlanted)
        {
            ApplyDemolitionTimePressure();
        }

        if (announce && playerPlan.Assignments.Count > 0)
        {
            _hud.ShowRadioMessage(playerPlan.Callout, new Color(0.35f, 0.82f, 1.0f));
        }
        // Performance-safe anti-idle: once round is live, clear sentry hold for all assigned demolition opponents so they walk to site instead of freezing at spawn.
        if (_demolitionRoundActive)
        {
            foreach (var opponent in _demolitionOpponents.Where(IsInstanceValid).Where(opponent => !opponent.IsDead && opponent.SentryMode))
            {
                if (_demolitionOpponentAssignments.ContainsKey(opponent))
                {
                    opponent.SentryMode = false;
                }
            }
        }
    }

    /// <summary>
    /// Team blackboard built from live firefights: any opponent locked in combat is a
    /// known position for the player team (mutual contact), and each alerted opponent's
    /// engage target is a known position for the enemy team. Fed into the planner so both
    /// sides react to contact instead of walking set pieces.
    /// </summary>
    private List<DemolitionAgentSnapshot> CollectDemolitionSightings(
        DemolitionTeam sightedSide,
        DemolitionTeam reportingSide,
        DemolitionArenaLayout layout)
    {
        var sightings = new List<DemolitionAgentSnapshot>();
        if (reportingSide == _demolitionMatch.PlayerSide)
        {
            // Player team reporting: positions of opponents currently fighting it.
            foreach (var opponent in _demolitionOpponents.Where(opponent => IsInstanceValid(opponent)
                     && !opponent.IsDead && opponent.Alerted))
            {
                sightings.Add(Snapshot(
                    $"KNOWN:{opponent.Name}",
                    sightedSide,
                    OperatorRole.Recon,
                    1.0f,
                    100.0f,
                    true,
                    false,
                    opponent.GlobalPosition,
                    layout.Origin));
            }
            return sightings;
        }
        // Enemy team reporting: engage targets of its alerted members.
        foreach (var opponent in _demolitionOpponents.Where(opponent => IsInstanceValid(opponent) && opponent.Alerted))
        {
            var target = opponent.EngageTargetNode;
            if (target is not null && IsInstanceValid(target))
            {
                sightings.Add(Snapshot(
                    $"KNOWN_P:{opponent.Name}",
                    sightedSide,
                    OperatorRole.Recon,
                    1.0f,
                    100.0f,
                    true,
                    false,
                    target.GlobalPosition,
                    layout.Origin));
            }
        }
        return sightings;
    }

    private void SelectDemolitionCarrier(DemolitionStrategyPlan opponentPlan)
    {
        ResolveAndApplyDemolitionObjectiveChannels(opponentPlan);
    }

    private void ResolveAndApplyDemolitionObjectiveChannels(DemolitionStrategyPlan opponentPlan)
    {
        var opponents = _demolitionOpponents
            .Where(IsInstanceValid)
            .ToList();
        var aliveMemberIds = opponents
            .Where(opponent => !opponent.IsDead)
            .Select(opponent => opponent.Name.ToString())
            .ToArray();
        var previousCarrier = _demolitionCarrier;
        var previousDefuser = _demolitionDefuser;
        var resolution = _demolitionObjectiveChannelCoordinator.Resolve(
            opponentPlan,
            aliveMemberIds,
            new DemolitionObjectiveChannelState(
                IsInstanceValid(previousCarrier) ? previousCarrier!.Name.ToString() : null,
                IsInstanceValid(previousDefuser) ? previousDefuser!.Name.ToString() : null,
                _demolitionEnemyPlantProgress,
                _demolitionDefuseProgress,
                _demolitionEnemyTargetSite,
                _demolitionActiveSite));

        EnemyOperator? ResolveActor(string? memberId)
            => memberId is null
                ? null
                : opponents.FirstOrDefault(opponent =>
                    !opponent.IsDead
                    && string.Equals(opponent.Name.ToString(), memberId, System.StringComparison.Ordinal));

        _demolitionOpponentAssignments.Clear();
        foreach (var assignment in resolution.Assignments)
        {
            var opponent = ResolveActor(assignment.MemberId);
            if (opponent is null)
            {
                continue;
            }
            _demolitionOpponentAssignments[opponent] = assignment;
            opponent.SentryMode = false;
        }

        var resolvedCarrier = ResolveActor(resolution.CarrierMemberId);
        var deviceObjectiveId = _demolitionDeviceLifecycle.IsGrounded
            ? _demolitionDeviceLifecycle.PickupRunnerMemberId
            : _demolitionDeviceLifecycle.IsCarried
                ? _demolitionDeviceLifecycle.CarrierMemberId
                : null;
        var deviceObjectiveEnemy = opponentPlan.Team == DemolitionTeam.Attackers
            && !_demolitionDevicePlanted
            ? ResolveDemolitionAttacker(deviceObjectiveId) as EnemyOperator
            : null;
        _demolitionCarrier = deviceObjectiveEnemy ?? resolvedCarrier;
        _demolitionDefuser = ResolveActor(resolution.DefuserMemberId);
        if (resolution.ResetPlantProgress)
        {
            _demolitionEnemyPlantProgress = 0.0f;
        }
        if (resolution.ResetDefuseProgress)
        {
            _demolitionDefuseProgress = 0.0f;
        }
        if (_demolitionCarrier != previousCarrier)
        {
            ResetDemolitionOpponentRoute(previousCarrier);
            ResetDemolitionOpponentRoute(_demolitionCarrier);
        }
        if (_demolitionDefuser != previousDefuser)
        {
            ResetDemolitionOpponentRoute(previousDefuser);
            ResetDemolitionOpponentRoute(_demolitionDefuser);
        }
        if (deviceObjectiveEnemy is not null)
        {
            if (!_demolitionOpponentAssignments.TryGetValue(deviceObjectiveEnemy, out var assignment)
                || !DemolitionObjectiveChannelCoordinator.IsCarrierDuty(assignment.Duty))
            {
                var siteIndex = Mathf.Clamp(
                    opponentPlan.PrimarySiteIndex,
                    0,
                    DemolitionLayout().SitePositions.Count - 1);
                _demolitionOpponentAssignments[deviceObjectiveEnemy] = new DemolitionAssignment(
                    deviceObjectiveEnemy.Name,
                    DemolitionDuty.Entry,
                    siteIndex,
                    siteIndex == 0 ? "attack_entry_a" : "attack_entry_b",
                    "round-assigned demolition device carrier");
            }
            if (_demolitionEnemyPlantProgress <= 0.0f)
            {
                _demolitionEnemyTargetSite = opponentPlan.PrimarySiteIndex;
            }
        }
        else if (_demolitionCarrier is not null && resolution.CarrierSiteIndex >= 0)
        {
            _demolitionEnemyTargetSite = resolution.CarrierSiteIndex;
        }
    }

    /// <summary>
    /// Clock awareness for the attacking AI: once the remaining round time barely covers
    /// the walk plus the plant, the carrier abandons the planned site and commits to the
    /// closest reachable one so the attack does not expire mid-rotation.
    /// </summary>
    private void ApplyDemolitionTimePressure()
    {
        if (_demolitionDevicePlanted
            || _demolitionEnemyPlantProgress > 0.0f
            || !IsInstanceValid(_demolitionCarrier)
            || _demolitionCarrier!.IsDead)
        {
            return;
        }
        var layout = DemolitionLayout();
        var carrierPosition = _demolitionCarrier.GlobalPosition;
        float TravelSeconds(Vector3 site)
        {
            var flat = new Vector3(site.X, carrierPosition.Y, site.Z);
            return carrierPosition.DistanceTo(flat) / 5.1f + DemolitionPlantDuration;
        }
        var planned = layout.SitePositions[Mathf.Clamp(_demolitionEnemyTargetSite, 0, layout.SitePositions.Count - 1)];
        if (TravelSeconds(planned) + 2.0f <= _demolitionRemaining)
        {
            return;
        }
        var nearest = -1;
        var nearestTravel = float.PositiveInfinity;
        for (var index = 0; index < layout.SitePositions.Count; index++)
        {
            var travel = TravelSeconds(layout.SitePositions[index]);
            if (travel + 2.0f <= _demolitionRemaining && travel < nearestTravel)
            {
                nearestTravel = travel;
                nearest = index;
            }
        }
        if (nearest >= 0 && nearest != _demolitionEnemyTargetSite)
        {
            _demolitionEnemyTargetSite = nearest;
            ResetDemolitionOpponentRoute(_demolitionCarrier);
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
            if (IsDemolitionSquadDeviceObjectiveMate(mate))
            {
                continue;
            }
            if (_demolitionSquadAssignmentTargets.TryGetValue(mate, out var current)
                && current == assignment.TargetKey)
            {
                continue;
            }
            _demolitionSquadAssignmentTargets[mate] = assignment.TargetKey;
            // Move clears the old post; once the mate arrives it converts to Hold so the
            // assignment behaves like an anchored position instead of a perpetual walk.
            mate.SetOrder(SquadOrder.Move, layout.StrategyTarget(assignment.TargetKey));
        }
    }

    /// <summary>
    /// Converts arrived Move orders into Holds so demolition teammates anchor their
    /// assigned posts and use their combat layer from cover instead of milling around.
    /// </summary>
    private static int NearestDemolitionSiteTo(Vector3 position, DemolitionArenaLayout layout)
    {
        var nearest = 0;
        var nearestDistance = float.PositiveInfinity;
        for (var index = 0; index < layout.SitePositions.Count; index++)
        {
            var site = layout.SitePositions[index];
            var flat = new Vector3(site.X, position.Y, site.Z);
            var distance = position.DistanceSquaredTo(flat);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = index;
            }
        }
        return nearest;
    }

    /// <summary>
    /// Objective hand-off, borrowed from open-source bot relay logic: when the local
    /// player who owns the bomb or the defuse role goes down mid-round, the closest able
    /// mate inherits the objective — it is ordered onto the site and channels the plant
    /// or defuse itself so the round is never lost to a dead carrier.
    /// </summary>
    private void UpdateDemolitionSquadObjectiveRelay(float delta)
    {
        if (!_demolitionRoundActive)
        {
            return;
        }
        if (TryUpdateDemolitionSquadDeviceObjective(delta))
        {
            return;
        }
        // The relay only takes over when the objective owner cannot act: the downed or
        // dead local player. A standing player keeps the bomb and the defuse role.
        if (!_localPlayerDowned && !_player.IsDead)
        {
            if (IsInstanceValid(_demolitionSquadObjectiveMate))
            {
                _demolitionSquadObjectiveMate = null;
                _demolitionSquadPlantProgress = 0.0f;
                _demolitionSquadDefuseProgress = 0.0f;
            }
            return;
        }
        var layout = DemolitionLayout();
        var side = _demolitionMatch.PlayerSide;
        var objectiveActive = !_demolitionDevicePlanted
            ? side == DemolitionTeam.Attackers
            : side == DemolitionTeam.Defenders;
        if (!objectiveActive)
        {
            return;
        }

        var siteIndex = !_demolitionDevicePlanted
            ? NearestDemolitionSiteTo(_player.GlobalPosition, layout)
            : Mathf.Clamp(_demolitionActiveSite, 0, layout.SitePositions.Count - 1);
        if (siteIndex != _demolitionSquadObjectiveSite)
        {
            _demolitionSquadObjectiveSite = siteIndex;
            _demolitionSquadObjectiveMate = null;
            _demolitionSquadPlantProgress = 0.0f;
            _demolitionSquadDefuseProgress = 0.0f;
        }
        if (!IsInstanceValid(_demolitionSquadObjectiveMate)
            || _demolitionSquadObjectiveMate!.IsDowned
            || _demolitionSquadObjectiveMate.IsBodyBag)
        {
            var site = layout.SitePositions[siteIndex];
            _demolitionSquadObjectiveMate = _squadMates
                .Where(mate => IsInstanceValid(mate) && !mate.IsDowned && !mate.IsBodyBag)
                .OrderBy(mate => mate.GlobalPosition.DistanceSquaredTo(site))
                .FirstOrDefault();
            _demolitionSquadPlantProgress = 0.0f;
            _demolitionSquadDefuseProgress = 0.0f;
        }
        var carrier = _demolitionSquadObjectiveMate;
        if (carrier is null)
        {
            return;
        }
        var destination = layout.SitePositions[siteIndex];
        var flatDestination = new Vector3(destination.X, carrier.GlobalPosition.Y, destination.Z);
        if (carrier.GlobalPosition.DistanceTo(flatDestination) > 2.2f)
        {
            if (carrier.Order != SquadOrder.Move || carrier.GlobalPosition.DistanceTo(flatDestination) > 6.0f)
            {
                carrier.SetOrder(SquadOrder.Move, destination);
            }
            return;
        }
        if (carrier.Order == SquadOrder.Move)
        {
            carrier.SetOrder(SquadOrder.Hold, carrier.GlobalPosition);
        }
        var progress = !_demolitionDevicePlanted
            ? _demolitionSquadPlantProgress += delta / DemolitionPlantDuration
            : _demolitionSquadDefuseProgress += delta / DemolitionDefuseDuration;
        if (progress >= 1.0f && !_demolitionDevicePlanted)
        {
            PlantDemolitionDevice(siteIndex, byPlayerTeam: true, carrier);
            _demolitionSquadObjectiveMate = null;
            _demolitionSquadPlantProgress = 0.0f;
        }
        else if (progress >= 1.0f)
        {
            var siteName = ((char)('A' + siteIndex)).ToString();
            FinishDemolitionRound(
                true,
                GameLocalization.Format(
                    "demolition_device_defused",
                    _languageSetting,
                    "SITE {0} DEVICE DEFUSED",
                    siteName));
        }
    }

    private void UpdateDemolitionSquadPosts()
    {
        if (!_demolitionRoundActive)
        {
            return;
        }
        foreach (var mate in _squadMates.Where(IsInstanceValid))
        {
            if (mate.Order != SquadOrder.Move
                || !_demolitionSquadAssignmentTargets.TryGetValue(mate, out var targetKey))
            {
                continue;
            }
            var target = DemolitionLayout().StrategyTarget(targetKey);
            if (mate.GlobalPosition.DistanceTo(target) <= 3.0f)
            {
                mate.SetOrder(SquadOrder.Hold, mate.GlobalPosition);
            }
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
        if (IsInstanceValid(_demolitionDefuser) && !_demolitionDefuser!.IsDead)
        {
            return;
        }
        RefreshDemolitionStrategies(false);
    }

    private void ResetDemolitionOpponentRoute(EnemyOperator? opponent)
    {
        if (opponent is null)
        {
            return;
        }
        _demolitionOpponentRoutes.Remove(opponent);
        if (IsInstanceValid(opponent))
        {
            opponent.ResetScriptedObjectiveNavigation();
        }
    }

    /// <summary>
    /// Drives every demolition enemy: defenders defuse, attackers carry and plant.
    /// </summary>
    public bool TryHandleDemolitionDefenderMovement(EnemyOperator opponent, float delta, Node3D? combatTarget)
    {
        var deviceObjectiveId = _demolitionDeviceLifecycle.IsGrounded
            ? _demolitionDeviceLifecycle.PickupRunnerMemberId
            : _demolitionDeviceLifecycle.IsCarried
                ? _demolitionDeviceLifecycle.CarrierMemberId
                : null;
        var isDeviceObjectiveEnemy = _demolitionMatch.PlayerSide == DemolitionTeam.Defenders
            && !_demolitionDevicePlanted
            && DemolitionMemberId(opponent) == deviceObjectiveId;
        if (!_demolitionMode
            || !_demolitionRoundActive
            || opponent.IsDead
            || !_demolitionOpponentAssignments.TryGetValue(opponent, out var assignment)
                && !isDeviceObjectiveEnemy)
        {
            return false;
        }
        if (isDeviceObjectiveEnemy
            && string.IsNullOrWhiteSpace(assignment.MemberId))
        {
            var siteIndex = Mathf.Clamp(
                _demolitionEnemyTargetSite,
                0,
                DemolitionLayout().SitePositions.Count - 1);
            assignment = new DemolitionAssignment(
                opponent.Name,
                DemolitionDuty.Entry,
                siteIndex,
                siteIndex == 0 ? "attack_entry_a" : "attack_entry_b",
                "live demolition device objective");
        }

        var targetDistance = ResolveDemolitionCombatTargetDistance(opponent, combatTarget);
        if (!UpdateDemolitionCombatArbitration(opponent, targetDistance))
        {
            return false;
        }
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
        return MoveDemolitionOpponentAlongRoute(
            opponent,
            DemolitionLayout().StrategyTarget(assignment.TargetKey),
            assignment.TargetKey,
            delta,
            2.0f,
            assignment.Duty is DemolitionDuty.Retake or DemolitionDuty.Flank ? 5.8f : 4.8f);
    }

    private bool MoveDemolitionOpponentAlongRoute(
        EnemyOperator opponent,
        Vector3 destination,
        string routeKey,
        float delta,
        float stoppingDistance,
        float speed)
    {
        var planner = _demolitionRoutePlanner ??= new DemolitionRoutePlanner(DemolitionLayout());
        if (!_demolitionOpponentRoutes.TryGetValue(opponent, out var cursor))
        {
            cursor = new DemolitionRouteCursor();
            _demolitionOpponentRoutes[opponent] = cursor;
        }

        if (!cursor.Matches(routeKey, destination))
        {
            ResetDemolitionRouteCursor(
                opponent,
                cursor,
                planner,
                routeKey,
                destination,
                countAsReplan: false);
        }

        cursor.Advance(
            opponent.GlobalPosition,
            DemolitionRouteCornerTolerance,
            cursor.ReachesDestination ? stoppingDistance : DemolitionRouteCornerTolerance);
        var distance = HorizontalDistance(opponent.GlobalPosition, destination);
        if (cursor.ReachesDestination && (cursor.Complete || distance <= stoppingDistance))
        {
            return MoveDemolitionOpponentToward(opponent, destination, delta, stoppingDistance, speed);
        }
        if (cursor.Complete)
        {
            StopDemolitionOpponent(opponent);
            if (cursor.ShouldRetryUnreachable(delta))
            {
                ResetDemolitionRouteCursor(
                    opponent,
                    cursor,
                    planner,
                    routeKey,
                    destination,
                    countAsReplan: true);
            }
            return true;
        }

        MoveDemolitionOpponentToward(
            opponent,
            cursor.CurrentWaypoint,
            delta,
            DemolitionRouteWaypointStop,
            speed);
        var stalled = cursor.TrackMovement(opponent.GlobalPosition, delta, movementRequested: true);
        if (stalled)
        {
            ResetDemolitionRouteCursor(
                opponent,
                cursor,
                planner,
                routeKey,
                destination,
                countAsReplan: true);
        }
        return true;
    }

    private static void ResetDemolitionRouteCursor(
        EnemyOperator opponent,
        DemolitionRouteCursor cursor,
        DemolitionRoutePlanner planner,
        string routeKey,
        Vector3 destination,
        bool countAsReplan)
    {
        var route = planner.Plan(opponent.GlobalPosition, destination);
        cursor.Reset(routeKey, opponent.GlobalPosition, destination, route, countAsReplan);
        opponent.ResetScriptedObjectiveNavigation();
    }

    private float ResolveDemolitionCombatTargetDistance(EnemyOperator opponent, Node3D? target)
    {
        if (target is null || !IsInstanceValid(target))
        {
            return float.PositiveInfinity;
        }
        var from = opponent.GlobalPosition + Vector3.Up * 1.45f;
        var to = target.GlobalPosition + Vector3.Up;
        return IsLineObscuredBySmoke(from, to)
            ? float.PositiveInfinity
            : opponent.GlobalPosition.DistanceTo(target.GlobalPosition);
    }

    /// <summary>
    /// Combat-first arbitration, the core of any competent bot: objective movement yields
    /// to the full combat layer while a hostile is inside the engage bubble, and resumes
    /// with hysteresis once the threat leaves it. A carrier or defuser mid-channel holds
    /// the channel under fire from beyond the guard range, trading damage for the
    /// objective like a planted-round defuser in Counter-Strike.
    /// </summary>
    private bool UpdateDemolitionCombatArbitration(EnemyOperator opponent, float targetDistance)
    {
        var channeling = IsDemolitionOpponentChanneling(opponent);
        if (channeling && targetDistance >= DemolitionChannelGuardRange)
        {
            return true;
        }
        var breaking = _demolitionCombatBreakoffs.Contains(opponent);
        if (targetDistance < DemolitionCombatEngageRange
            || breaking && targetDistance < DemolitionCombatResumeRange)
        {
            if (!breaking)
            {
                _demolitionCombatBreakoffs.Add(opponent);
            }
            return false;
        }
        if (breaking)
        {
            _demolitionCombatBreakoffs.Remove(opponent);
        }
        return true;
    }

    private bool IsDemolitionOpponentChanneling(EnemyOperator opponent)
        => (!_demolitionDevicePlanted
                && opponent == _demolitionCarrier
                && _demolitionEnemyPlantProgress > 0.0f)
            || (_demolitionDevicePlanted
                && opponent == _demolitionDefuser
                && _demolitionDefuseProgress > 0.0f);

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
        if (_demolitionMatch.PlayerSide == DemolitionTeam.Defenders
            && _demolitionDeviceLifecycle.IsGrounded)
        {
            var pickupRunner = ResolveDemolitionAttacker(
                _demolitionDeviceLifecycle.PickupRunnerMemberId) as EnemyOperator;
            if (opponent != pickupRunner)
            {
                return false;
            }
            if (targetDistance < DemolitionChannelGuardRange)
            {
                return false;
            }
            var pickupDistance = HorizontalDistance(
                opponent.GlobalPosition,
                _demolitionDeviceGroundPosition);
            if (pickupDistance > DemolitionDevicePickupRadius)
            {
                return MoveDemolitionOpponentAlongRoute(
                    opponent,
                    _demolitionDeviceGroundPosition,
                    "device_pickup",
                    delta,
                    DemolitionDevicePickupRadius,
                    5.1f);
            }
            if (!TryPickupDemolitionDevice(opponent))
            {
                StopDemolitionOpponent(opponent);
                return true;
            }
        }
        var lifecycleCarrier = _demolitionMatch.PlayerSide == DemolitionTeam.Defenders
            && _demolitionDeviceLifecycle.IsCarried
            ? ResolveDemolitionAttacker(_demolitionDeviceLifecycle.CarrierMemberId) as EnemyOperator
            : null;
        if (opponent != (lifecycleCarrier ?? _demolitionCarrier))
        {
            return false;
        }
        if (targetDistance < DemolitionChannelGuardRange)
        {
            // Threat inside the guard bubble: hand control to the combat layer. The
            // channel keeps its progress and resumes once the fight is won or lost.
            return false;
        }
        var layout = DemolitionLayout();
        var site = layout.SitePositions[Mathf.Clamp(_demolitionEnemyTargetSite, 0, layout.SitePositions.Count - 1)];
        var flatSite = new Vector3(site.X, opponent.GlobalPosition.Y, site.Z);
        var distance = opponent.GlobalPosition.DistanceTo(flatSite);
        if (distance > 2.15f)
        {
            return MoveDemolitionOpponentAlongRoute(
                opponent,
                site,
                $"carrier_site_{_demolitionEnemyTargetSite}",
                delta,
                2.15f,
                5.1f);
        }

        var velocity = opponent.Velocity;
        velocity.X = Mathf.MoveToward(velocity.X, 0.0f, delta * 18.0f);
        velocity.Z = Mathf.MoveToward(velocity.Z, 0.0f, delta * 18.0f);
        opponent.Velocity = velocity;
        _demolitionEnemyPlantProgress = Mathf.Min(1.0f, _demolitionEnemyPlantProgress + delta / DemolitionPlantDuration);
        if (_demolitionEnemyPlantProgress >= 1.0f)
        {
            PlantDemolitionDevice(_demolitionEnemyTargetSite, byPlayerTeam: false, opponent);
        }
        return true;
    }

    private bool TryHandleDemolitionDefuserMovement(EnemyOperator opponent, float delta, float targetDistance)
    {
        if (targetDistance < DemolitionChannelGuardRange)
        {
            // Same guard rule as the carrier: drop to the combat layer, keep the progress.
            return false;
        }
        var devicePosition = DemolitionLayout().SitePositions[_demolitionActiveSite];
        var flatDevice = new Vector3(devicePosition.X, opponent.GlobalPosition.Y, devicePosition.Z);
        var distance = opponent.GlobalPosition.DistanceTo(flatDevice);
        if (distance > 2.15f)
        {
            return MoveDemolitionOpponentAlongRoute(
                opponent,
                devicePosition,
                $"defuser_site_{_demolitionActiveSite}",
                delta,
                2.15f,
                5.3f);
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

    private static void StopDemolitionOpponent(EnemyOperator opponent)
    {
        var velocity = opponent.Velocity;
        velocity.X = 0.0f;
        velocity.Z = 0.0f;
        opponent.Velocity = velocity;
    }
}
