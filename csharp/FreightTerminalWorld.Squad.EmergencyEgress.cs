using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    // Legacy partial retained while rescue recovery shares the squad door, trail,
    // connector, and physics-query lifecycle. Follow-up: move it with the complete
    // squad navigation runtime behind the focused service boundary.
    private const ulong SquadEmergencyEgressProbeIntervalMilliseconds = 420;
    private const ulong SquadEmergencyEgressPlanLifetimeMilliseconds = 9000;
    private const ulong SquadEmergencyEgressFailureCooldownMilliseconds = 6000;
    private const float SquadEmergencyEgressDestinationToleranceSquared = 1.0f;
    private const float SquadEmergencyDoorSearchRange = 14.0f;
    private const float SquadEmergencyConnectorSearchRange = 16.0f;
    private const float SquadEmergencyBreadcrumbStepDistance = 3.8f;
    private const float SquadEmergencyBreadcrumbHandoffRange = 9.0f;
    private const int SquadEmergencyStructuredCorridorProbeCap = 24;
    private const int SquadEmergencyOpenCorridorProbeCap = 12;

    // Ordered around the target bearing. A successful plan is cached, so this fan
    // is sampled only after rescue progress stalls rather than on the movement path.
    private static readonly float[] SquadEmergencyEgressYawRadians =
    {
        0.0f,
        -0.3926991f,
        0.3926991f,
        -0.7853982f,
        0.7853982f,
        -1.1780972f,
        1.1780972f,
        -1.5707963f,
        1.5707963f,
        -1.9634954f,
        1.9634954f,
        -2.3561945f,
        2.3561945f,
        -2.7488936f,
        2.7488936f,
        Mathf.Pi
    };

    private enum SquadEmergencyEgressSource : byte
    {
        Door,
        AuthoredConnector,
        BreadcrumbHandoff,
        OpenCorridor
    }

    private sealed class SquadEmergencyEgressPlan
    {
        public SquadEmergencyEgressSource Source;
        public SquadNavigationDirective[] Directives = Array.Empty<SquadNavigationDirective>();
        public int Cursor;
        public Vector3 Destination;
        public ulong ExpiresMilliseconds;
        public long FailureKey;
    }

    private readonly Dictionary<ulong, SquadEmergencyEgressPlan> _squadEmergencyEgressPlans = new();
    private readonly Dictionary<ulong, ulong> _squadEmergencyEgressNextProbe = new();
    private readonly Dictionary<(ulong MateId, long Key), ulong> _squadEmergencyEgressFailures = new();

    private int _squadEmergencyEgressProbeComputationsForDiagnostics;
    private int _squadEmergencyEgressPlanReusesForDiagnostics;
    private int _squadEmergencyDoorPlansForDiagnostics;
    private int _squadEmergencyConnectorPlansForDiagnostics;
    private int _squadEmergencyBreadcrumbPlansForDiagnostics;
    private int _squadEmergencyOpenCorridorPlansForDiagnostics;

    internal int SquadEmergencyEgressProbeComputationsForDiagnostics
        => _squadEmergencyEgressProbeComputationsForDiagnostics;
    internal int SquadEmergencyEgressPlanReusesForDiagnostics
        => _squadEmergencyEgressPlanReusesForDiagnostics;
    internal int SquadEmergencyDoorPlansForDiagnostics
        => _squadEmergencyDoorPlansForDiagnostics;
    internal int SquadEmergencyOpenCorridorPlansForDiagnostics
        => _squadEmergencyOpenCorridorPlansForDiagnostics;
    internal bool HasPendingSquadEmergencyEgress(SquadMate mate)
        => IsInstanceValid(mate) && _squadEmergencyEgressPlans.ContainsKey(mate.GetInstanceId());

    private bool TryContinueSquadEmergencyRescueEgress(
        SquadMate mate,
        Vector3 destination,
        out SquadNavigationDirective directive)
    {
        if (TryContinueSquadEmergencyEgressPlan(mate, destination, out directive))
        {
            return true;
        }
        if (mate.HasPendingRescueGlassEgress
            && mate.TryResolveEmergencyGlassEgress(destination, out directive))
        {
            return true;
        }
        directive = SquadNavigationDirective.Walk(destination);
        return false;
    }

    private bool TryResolveSquadEmergencyRescueEgress(
        SquadMate mate,
        Vector3 destination,
        out SquadNavigationDirective directive)
    {
        directive = SquadNavigationDirective.Walk(mate.GlobalPosition);
        var id = mate.GetInstanceId();
        var now = Time.GetTicksMsec();
        if (_squadEmergencyEgressNextProbe.TryGetValue(id, out var nextProbe)
            && now < nextProbe)
        {
            return false;
        }

        _squadEmergencyEgressNextProbe[id] = now + SquadEmergencyEgressProbeIntervalMilliseconds;
        _squadEmergencyEgressProbeComputationsForDiagnostics++;
        var structuredProbeBudget = SquadEmergencyStructuredCorridorProbeCap;
        if (TryBuildSquadEmergencyDoorPlan(
                mate,
                destination,
                now,
                ref structuredProbeBudget,
                out var plan)
            || TryBuildSquadEmergencyConnectorPlan(
                mate,
                destination,
                now,
                ref structuredProbeBudget,
                out plan)
            || TryBuildSquadEmergencyBreadcrumbPlan(
                mate,
                destination,
                now,
                ref structuredProbeBudget,
                out plan))
        {
            ActivateSquadEmergencyEgressPlan(mate, plan);
            return TryContinueSquadEmergencyEgressPlan(mate, destination, out directive);
        }

        // A real door or authored route always wins. Glass is a constrained fallback
        // for a safe downward rescue, followed by a generic supported corridor nudge
        // so maps without breakable windows can still escape an unregistered room.
        if (mate.TryResolveEmergencyGlassEgress(destination, out directive))
        {
            return true;
        }
        var openProbeBudget = SquadEmergencyOpenCorridorProbeCap;
        if (!TryBuildSquadEmergencyOpenCorridorPlan(
                mate,
                destination,
                now,
                ref openProbeBudget,
                out plan))
        {
            return false;
        }
        ActivateSquadEmergencyEgressPlan(mate, plan);
        return TryContinueSquadEmergencyEgressPlan(mate, destination, out directive);
    }

    private bool TryContinueSquadEmergencyEgressPlan(
        SquadMate mate,
        Vector3 destination,
        out SquadNavigationDirective directive)
    {
        directive = SquadNavigationDirective.Walk(destination);
        var id = mate.GetInstanceId();
        if (!_squadEmergencyEgressPlans.TryGetValue(id, out var plan))
        {
            return false;
        }

        var now = Time.GetTicksMsec();
        if (plan.Destination.DistanceSquaredTo(destination)
                > SquadEmergencyEgressDestinationToleranceSquared
            || now >= plan.ExpiresMilliseconds)
        {
            if (now >= plan.ExpiresMilliseconds)
            {
                RecordSquadEmergencyEgressFailure(id, plan.FailureKey, now);
            }
            _squadEmergencyEgressPlans.Remove(id);
            return false;
        }

        while (plan.Cursor < plan.Directives.Length
            && SquadNavigationDirectiveReached(
                mate.GlobalPosition,
                plan.Directives,
                plan.Cursor))
        {
            var consumed = plan.Directives[plan.Cursor++];
            if (consumed.DirectedEdgeId >= 0)
            {
                ClearSquadTraversalRecoveryAttempt(mate, consumed.DirectedEdgeId);
            }
            if (consumed.Required)
            {
                break;
            }
        }
        if (plan.Cursor >= plan.Directives.Length)
        {
            _squadEmergencyEgressPlans.Remove(id);
            _squadEmergencyEgressNextProbe[id] = now + SquadEmergencyEgressProbeIntervalMilliseconds;
            return false;
        }

        _squadEmergencyEgressPlanReusesForDiagnostics++;
        directive = plan.Directives[plan.Cursor];
        return true;
    }

    private void ActivateSquadEmergencyEgressPlan(
        SquadMate mate,
        SquadEmergencyEgressPlan plan)
    {
        _squadEmergencyEgressPlans[mate.GetInstanceId()] = plan;
        switch (plan.Source)
        {
            case SquadEmergencyEgressSource.Door:
                _squadEmergencyDoorPlansForDiagnostics++;
                break;
            case SquadEmergencyEgressSource.AuthoredConnector:
                _squadEmergencyConnectorPlansForDiagnostics++;
                break;
            case SquadEmergencyEgressSource.BreadcrumbHandoff:
                _squadEmergencyBreadcrumbPlansForDiagnostics++;
                break;
            case SquadEmergencyEgressSource.OpenCorridor:
                _squadEmergencyOpenCorridorPlansForDiagnostics++;
                break;
        }
    }

    private static SquadNavigationDirective RequiredEmergencyWalk(Vector3 target)
        => new(target, SquadTraversalKind.Walk, -1, true, PreciseTrail: true);

    private static float SquadEmergencyGoalScore(Vector3 point, Vector3 destination)
    {
        var horizontal = new Vector2(point.X - destination.X, point.Z - destination.Z).Length();
        return horizontal + Mathf.Abs(point.Y - destination.Y) * 2.8f;
    }

    private Vector3 SquadEmergencyEgressBaseDirection(SquadMate mate, Vector3 destination)
    {
        var direction = destination - mate.GlobalPosition;
        direction.Y = 0.0f;
        if (direction.LengthSquared() < 0.08f && _squadLeaderTrail.Count > 0)
        {
            direction = _squadLeaderTrail[^1] - mate.GlobalPosition;
            direction.Y = 0.0f;
        }
        if (direction.LengthSquared() < 0.08f)
        {
            direction = -mate.GlobalBasis.Z;
            direction.Y = 0.0f;
        }
        return direction.LengthSquared() > 0.01f ? direction.Normalized() : Vector3.Forward;
    }

    private bool IsSquadEmergencyEgressFailureActive(ulong mateId, long key, ulong now)
    {
        var failureKey = (mateId, key);
        if (!_squadEmergencyEgressFailures.TryGetValue(failureKey, out var expires))
        {
            return false;
        }
        if (now < expires)
        {
            return true;
        }
        _squadEmergencyEgressFailures.Remove(failureKey);
        return false;
    }

    private void RecordSquadEmergencyEgressFailure(ulong mateId, long key, ulong now)
        => _squadEmergencyEgressFailures[(mateId, key)] =
            now + SquadEmergencyEgressFailureCooldownMilliseconds;

    private void RejectSquadEmergencyEgressPlan(SquadMate mate)
    {
        var id = mate.GetInstanceId();
        if (_squadEmergencyEgressPlans.Remove(id, out var plan))
        {
            var now = Time.GetTicksMsec();
            RecordSquadEmergencyEgressFailure(id, plan.FailureKey, now);
            if (plan.Cursor >= 0 && plan.Cursor < plan.Directives.Length
                && plan.Directives[plan.Cursor].DirectedEdgeId >= 0)
            {
                ReportSquadTraversalFailure(mate, plan.Directives[plan.Cursor].DirectedEdgeId);
            }
        }
        _squadEmergencyEgressNextProbe.Remove(id);
    }

    private void ResetSquadEmergencyEgressPlan(SquadMate mate)
    {
        var id = mate.GetInstanceId();
        _squadEmergencyEgressPlans.Remove(id);
        _squadEmergencyEgressNextProbe.Remove(id);
    }

    private void ResetSquadEmergencyEgressRuntime()
    {
        _squadEmergencyEgressPlans.Clear();
        _squadEmergencyEgressNextProbe.Clear();
        _squadEmergencyEgressFailures.Clear();
    }
}
