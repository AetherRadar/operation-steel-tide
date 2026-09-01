using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private const float DemolitionUtilityDecisionInterval = 0.52f;
    private const float DemolitionUtilityTeamCooldown = 5.6f;
    private float _demolitionFriendlyUtilityDecisionRemaining;
    private float _demolitionOpponentUtilityDecisionRemaining;
    private float _demolitionFriendlyUtilityCooldown;
    private float _demolitionOpponentUtilityCooldown;
    private int _demolitionUtilityRuntimeRound = -1;

    internal int DemolitionAiUtilityDecisionsForDiagnostics { get; private set; }
    internal int DemolitionAiUtilityThrowsForDiagnostics { get; private set; }
    internal string DemolitionAiLastUtilityReasonForDiagnostics { get; private set; } = string.Empty;
    private void UpdateDemolitionUtilityAi(float delta)
    {
        if (!_demolitionMode
            || !_demolitionRoundActive
            || _missionEnded
            || IsDemolitionNetworkClient)
        {
            return;
        }
        if (_demolitionUtilityRuntimeRound != _demolitionMatch.CurrentRound)
        {
            ResetDemolitionUtilityRuntimeForRound();
        }

        _demolitionFriendlyUtilityCooldown = Mathf.Max(
            0.0f,
            _demolitionFriendlyUtilityCooldown - delta);
        _demolitionOpponentUtilityCooldown = Mathf.Max(
            0.0f,
            _demolitionOpponentUtilityCooldown - delta);
        _demolitionFriendlyUtilityDecisionRemaining -= delta;
        _demolitionOpponentUtilityDecisionRemaining -= delta;

        if (_demolitionFriendlyUtilityDecisionRemaining <= 0.0f)
        {
            _demolitionFriendlyUtilityDecisionRemaining += DemolitionUtilityDecisionInterval;
            if (_demolitionFriendlyUtilityCooldown <= 0.0f && TryUseFriendlyDemolitionUtility())
            {
                _demolitionFriendlyUtilityCooldown = DemolitionUtilityTeamCooldown;
            }
        }
        if (_demolitionOpponentUtilityDecisionRemaining <= 0.0f)
        {
            _demolitionOpponentUtilityDecisionRemaining += DemolitionUtilityDecisionInterval;
            if (_demolitionOpponentUtilityCooldown <= 0.0f && TryUseOpponentDemolitionUtility())
            {
                _demolitionOpponentUtilityCooldown = DemolitionUtilityTeamCooldown;
            }
        }
    }

    private void ResetDemolitionUtilityRuntimeForRound()
    {
        _demolitionUtilityRuntimeRound = _demolitionMatch.CurrentRound;
        _demolitionFriendlyUtilityDecisionRemaining = 0.24f;
        _demolitionOpponentUtilityDecisionRemaining = 0.48f;
        _demolitionFriendlyUtilityCooldown = 1.25f;
        _demolitionOpponentUtilityCooldown = 1.65f;
    }

    private bool TryUseFriendlyDemolitionUtility()
    {
        var localTeam = LocalDemolitionSide;
        var plan = localTeam == DemolitionTeam.Attackers
            ? _demolitionAttackerPlan
            : _demolitionDefenderPlan;
        for (var index = 0; index < _squadMates.Count; index++)
        {
            var mate = _squadMates[index];
            if (!IsInstanceValid(mate)
                || mate.IsDowned
                || mate.IsBodyBag
                || mate.IsHumanProxy)
            {
                continue;
            }
            var plannedWeapon = DemolitionBotLoadoutPlanner.BuildForSlot(
                _demolitionFriendlyBotRoundFunds,
                mate.SquadSlot);
            mate.EnsureDemolitionUtilityInventory(
                _demolitionMatch.CurrentRound,
                mate.SquadSlot,
                _demolitionFriendlyBotRoundFunds,
                plannedWeapon);
            var assignment = FindUtilityAssignment(plan, $"MATE:{mate.SquadSlot}");
            var objective = ResolveUtilityObjective(assignment);
            var hasContact = mate.TryGetVisibleDemolitionUtilityContact(
                out _,
                out var contactPosition);
            var decision = DemolitionUtilityPlanner.Plan(BuildUtilityContext(
                localTeam,
                assignment,
                mate,
                objective,
                contactPosition,
                hasContact,
                mate == _demolitionSquadObjectiveMate,
                mate.HasDemolitionUtility(DemolitionAiUtilityKind.Fragmentation),
                mate.HasDemolitionUtility(DemolitionAiUtilityKind.Smoke),
                mate.HasDemolitionUtility(DemolitionAiUtilityKind.Incendiary),
                mate.HasDemolitionUtility(DemolitionAiUtilityKind.Flashbang)));
            DemolitionAiUtilityDecisionsForDiagnostics++;
            if (decision.Kind == DemolitionAiUtilityKind.None
                || !TryExecuteDemolitionUtility(
                    mate,
                    mate.DemolitionUtilityThrowOrigin,
                    decision,
                    () => mate.ConsumeDemolitionUtility(decision.Kind)))
            {
                continue;
            }
            return true;
        }
        return false;
    }

    private bool TryUseOpponentDemolitionUtility()
    {
        var opponentTeam = DemolitionOtherSide(LocalDemolitionSide);
        for (var index = 0; index < _demolitionOpponents.Count; index++)
        {
            var opponent = _demolitionOpponents[index];
            if (!IsInstanceValid(opponent) || opponent.IsDead || opponent.IsHumanProxy)
            {
                continue;
            }
            var actorSlot = opponent.NetworkId >= DemolitionAlphaActorBase
                ? DemolitionActorSlot(opponent.NetworkId)
                : index;
            actorSlot = Mathf.Clamp(actorSlot, 0, DemolitionSquadSize - 1);
            var plannedWeapon = actorSlot < _demolitionOpponentRoundWeapons.Count
                ? _demolitionOpponentRoundWeapons[actorSlot]
                : DemolitionBotLoadoutPlanner.BuildForSlot(
                    _demolitionOpponentBotRoundFunds,
                    actorSlot);
            opponent.EnsureDemolitionUtilityInventory(
                _demolitionMatch.CurrentRound,
                actorSlot,
                _demolitionOpponentBotRoundFunds,
                plannedWeapon);
            _demolitionOpponentAssignments.TryGetValue(opponent, out var assignment);
            var objective = ResolveUtilityObjective(assignment);
            var hasContact = opponent.TryGetVisibleDemolitionUtilityContact(
                out _,
                out var contactPosition);
            var objectiveChannelOwner = opponent == _demolitionCarrier
                || opponent == _demolitionDefuser;
            var decision = DemolitionUtilityPlanner.Plan(BuildUtilityContext(
                opponentTeam,
                assignment,
                opponent,
                objective,
                contactPosition,
                hasContact,
                objectiveChannelOwner,
                opponent.HasDemolitionUtility(DemolitionAiUtilityKind.Fragmentation),
                opponent.HasDemolitionUtility(DemolitionAiUtilityKind.Smoke),
                opponent.HasDemolitionUtility(DemolitionAiUtilityKind.Incendiary),
                opponent.HasDemolitionUtility(DemolitionAiUtilityKind.Flashbang)));
            DemolitionAiUtilityDecisionsForDiagnostics++;
            if (decision.Kind == DemolitionAiUtilityKind.None
                || !TryExecuteDemolitionUtility(
                    opponent,
                    opponent.DemolitionUtilityThrowOrigin,
                    decision,
                    () => opponent.ConsumeDemolitionUtility(decision.Kind)))
            {
                continue;
            }
            return true;
        }
        return false;
    }

    private DemolitionUtilityContext BuildUtilityContext(
        DemolitionTeam team,
        DemolitionAssignment assignment,
        Node3D actor,
        Vector3 objective,
        Vector3 contactPosition,
        bool hasContact,
        bool objectiveChannelOwner,
        bool hasFragmentation,
        bool hasSmoke,
        bool hasIncendiary,
        bool hasFlashbang)
    {
        var phase = _demolitionDevicePlanted
            ? DemolitionStrategyPhase.PostPlant
            : DemolitionStrategyPhase.Opening;
        return new DemolitionUtilityContext(
            team,
            phase,
            assignment.Duty,
            actor.GlobalPosition,
            objective,
            contactPosition,
            hasContact,
            objectiveChannelOwner,
            hasFragmentation,
            hasSmoke,
            hasIncendiary,
            IsUtilityFriendlySafe(team, contactPosition, 9.5f),
            IsUtilityFriendlySafe(
                team,
                hasContact ? contactPosition : objective,
                IncendiaryGrenade.FireRadius + 1.25f),
            _demolitionRemaining,
            hasFlashbang,
            IsFlashbangFriendlySafe(
                team,
                actor,
                hasContact ? contactPosition : objective));
    }

    private bool TryExecuteDemolitionUtility(
        Node3D actor,
        Vector3 origin,
        DemolitionUtilityDecision decision,
        System.Func<bool> consume)
    {
        if (!CanThrowDemolitionUtility(actor, origin, decision.TargetPosition, out var direction, out var loft)
            || !consume())
        {
            return false;
        }
        switch (decision.Kind)
        {
            case DemolitionAiUtilityKind.Fragmentation:
                ThrowGrenade(origin, direction, actor, 14.0f, loft);
                break;
            case DemolitionAiUtilityKind.Smoke:
                ThrowSmokeGrenade(origin, direction, actor, 14.0f, loft);
                break;
            case DemolitionAiUtilityKind.Incendiary:
                ThrowIncendiaryGrenade(origin, direction, actor, 14.0f, loft);
                break;
            case DemolitionAiUtilityKind.Flashbang:
                ThrowFlashbangGrenade(origin, direction, actor, 14.0f, loft);
                break;
            default:
                return false;
        }
        DemolitionAiUtilityThrowsForDiagnostics++;
        DemolitionAiLastUtilityReasonForDiagnostics = decision.Reason;
        return true;
    }

    private bool CanThrowDemolitionUtility(
        Node3D actor,
        Vector3 origin,
        Vector3 target,
        out Vector3 direction,
        out float loft)
    {
        var offset = target - origin;
        offset.Y = 0.0f;
        var distance = offset.Length();
        direction = distance > 0.01f ? offset / distance : -actor.GlobalBasis.Z;
        loft = Mathf.Clamp(distance / 2.86f, 2.4f, 7.4f);
        if (distance is < 4.5f or > 29.0f || actor is not CollisionObject3D collisionActor)
        {
            return false;
        }

        var apex = origin.Lerp(target + Vector3.Up * 0.2f, 0.52f)
            + Vector3.Up * Mathf.Clamp(distance * 0.17f, 2.0f, 4.8f);
        if (PhysicsRaycast.HasHit(
            GetWorld3D(),
            origin,
            apex,
            collisionActor.GetRid(),
            1))
        {
            return false;
        }
        if (!PhysicsRaycast.TryHit(
            GetWorld3D(),
            apex,
            target + Vector3.Up * 0.12f,
            collisionActor.GetRid(),
            1,
            out var landingHit))
        {
            return true;
        }
        return landingHit.Position.DistanceTo(target) <= 1.4f;
    }

    private bool IsUtilityFriendlySafe(DemolitionTeam team, Vector3 target, float radius)
    {
        if (team == LocalDemolitionSide)
        {
            if (IsInstanceValid(_player)
                && !_player.IsDead
                && _player.GlobalPosition.DistanceTo(target) < radius)
            {
                return false;
            }
            foreach (var mate in _squadMates)
            {
                if (IsInstanceValid(mate)
                    && !mate.IsDowned
                    && !mate.IsBodyBag
                    && mate.GlobalPosition.DistanceTo(target) < radius)
                {
                    return false;
                }
            }
            return true;
        }
        foreach (var opponent in _demolitionOpponents)
        {
            if (IsInstanceValid(opponent)
                && !opponent.IsDead
                && opponent.GlobalPosition.DistanceTo(target) < radius)
            {
                return false;
            }
        }
        return true;
    }

    private bool IsFlashbangFriendlySafe(
        DemolitionTeam team,
        Node3D thrower,
        Vector3 detonationPoint)
    {
        if (FlashbangWouldDisruptFriendly(thrower, detonationPoint))
        {
            return false;
        }

        if (team == LocalDemolitionSide)
        {
            if (IsInstanceValid(_player)
                && _player != thrower
                && !_player.IsDead
                && FlashbangWouldDisruptFriendly(_player, detonationPoint))
            {
                return false;
            }
            foreach (var mate in _squadMates)
            {
                if (IsInstanceValid(mate)
                    && mate != thrower
                    && !mate.IsDowned
                    && !mate.IsBodyBag
                    && FlashbangWouldDisruptFriendly(mate, detonationPoint))
                {
                    return false;
                }
            }
            return true;
        }

        foreach (var opponent in _demolitionOpponents)
        {
            if (IsInstanceValid(opponent)
                && opponent != thrower
                && !opponent.IsDead
                && FlashbangWouldDisruptFriendly(opponent, detonationPoint))
            {
                return false;
            }
        }
        return true;
    }

    private static bool FlashbangWouldDisruptFriendly(Node3D friendly, Vector3 detonationPoint)
    {
        var viewOrigin = friendly is IFlashbangTarget target
            ? target.FlashbangViewOrigin
            : friendly.GlobalPosition + Vector3.Up * 1.45f;
        var viewForward = friendly is IFlashbangTarget flashbangTarget
            ? flashbangTarget.FlashbangViewForward
            : -friendly.GlobalBasis.Z;
        return !IsPredictedFlashbangExposureSafe(viewOrigin, viewForward, detonationPoint);
    }

    internal static bool IsPredictedFlashbangExposureSafe(
        Vector3 viewOrigin,
        Vector3 viewForward,
        Vector3 detonationPoint)
        => FlashbangExposureResolver.Resolve(
                detonationPoint,
                viewOrigin,
                viewForward,
                hasLineOfSight: true)
            .Intensity < 0.16f;

    private Vector3 ResolveUtilityObjective(DemolitionAssignment assignment)
    {
        var layout = DemolitionLayout();
        if (_demolitionDevicePlanted && _demolitionActiveSite >= 0)
        {
            return layout.SitePositions[Mathf.Clamp(
                _demolitionActiveSite,
                0,
                layout.SitePositions.Count - 1)];
        }
        if (!string.IsNullOrWhiteSpace(assignment.TargetKey))
        {
            return layout.StrategyTarget(assignment.TargetKey);
        }
        return layout.SitePositions[Mathf.Clamp(
            assignment.SiteIndex,
            0,
            layout.SitePositions.Count - 1)];
    }

    private static DemolitionAssignment FindUtilityAssignment(
        DemolitionStrategyPlan? plan,
        string memberId)
    {
        if (plan is not null)
        {
            foreach (var assignment in plan.Assignments)
            {
                if (assignment.MemberId == memberId)
                {
                    return assignment;
                }
            }
        }
        return new DemolitionAssignment(memberId, DemolitionDuty.Support, 0, string.Empty, "fallback");
    }

}
