using System;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    // Route searches are deliberately serialized.  A 220 ms slot keeps a burst of
    // alerted operators from stacking several multi-millisecond searches in one
    // render interval; trail following and the direct-path fast lane cover the
    // intervening frames.
    private const ulong OperatorPursuitPlanIntervalMilliseconds = 220;
    private const int OperatorPursuitExpansionCap = 1800;
    private const double OperatorPursuitPlanBudgetMilliseconds = 5.0;
    private const float OperatorPursuitDirectRouteMaximumDistance = 72.0f;
    private const float OperatorPursuitDirectRouteMaximumHeight = 0.72f;

    private ulong _nextOperatorPursuitPlanMilliseconds;
    private int _operatorPursuitPlanAttempts;
    private int _operatorPursuitPlanSuccesses;

    internal bool TryPlanOperatorPursuitRoute(
        EnemyOperator navigator,
        Vector3 destination,
        out SquadNavigationDirective[] route)
    {
        route = Array.Empty<SquadNavigationDirective>();
        if (!IsInstanceValid(navigator))
        {
            return false;
        }

        // Most pursuit destinations are on the same floor and have no intervening
        // wall.  Three inexpensive body-height rays avoid allocating the layered
        // portal graph for that common case while retaining collision-safe movement.
        if (TryBuildDirectOperatorPursuitRoute(navigator, destination, out route))
        {
            return true;
        }

        if (_squadTraversalLinks.Count == 0)
        {
            return false;
        }

        var now = Time.GetTicksMsec();
        if (now < _nextOperatorPursuitPlanMilliseconds)
        {
            return false;
        }
        _nextOperatorPursuitPlanMilliseconds = now
            + OperatorPursuitPlanIntervalMilliseconds
            + navigator.GetInstanceId() % 47UL;
        _operatorPursuitPlanAttempts++;

        var budget = new SquadNavSearchBudget(
            OperatorPursuitExpansionCap,
            OperatorPursuitPlanBudgetMilliseconds);
        if (!TryPlanSquadLayeredRoute(
                navigator,
                destination,
                budget,
                SquadTraversalCapabilities.Walk
                    | SquadTraversalCapabilities.Step
                    | SquadTraversalCapabilities.Ladder,
                out var planned,
                out _)
            || planned.Length == 0)
        {
            return false;
        }

        route = planned;
        _operatorPursuitPlanSuccesses++;
        return true;
    }

    private bool TryBuildDirectOperatorPursuitRoute(
        EnemyOperator navigator,
        Vector3 destination,
        out SquadNavigationDirective[] route)
    {
        route = Array.Empty<SquadNavigationDirective>();
        var offset = destination - navigator.GlobalPosition;
        if (Mathf.Abs(offset.Y) > OperatorPursuitDirectRouteMaximumHeight)
        {
            return false;
        }

        var horizontal = new Vector2(offset.X, offset.Z);
        var distanceSquared = horizontal.LengthSquared();
        if (distanceSquared < 0.16f
            || distanceSquared > OperatorPursuitDirectRouteMaximumDistance
                * OperatorPursuitDirectRouteMaximumDistance)
        {
            return false;
        }

        var direction = horizontal.Normalized();
        var side = new Vector3(-direction.Y, 0.0f, direction.X) * 0.3f;
        var exclude = navigator.GetRid();
        // The destination is often the player. Exclude that body as well so the
        // endpoint itself does not turn an unobstructed pursuit lane into an A* job.
        var targetExclude = IsInstanceValid(_player) ? _player.GetRid() : exclude;
        var mask = 1u | BreakableGlassField.MovementCollisionLayer;
        var fromBase = navigator.GlobalPosition + Vector3.Up * 0.82f;
        var toBase = destination + Vector3.Up * 0.82f;
        for (var ray = 0; ray < 3; ray++)
        {
            var offsetSide = ray switch
            {
                1 => side,
                2 => -side,
                _ => Vector3.Zero
            };
            if (PhysicsRaycast.HasHit(
                    GetWorld3D(),
                    fromBase + offsetSide,
                    toBase + offsetSide,
                    exclude,
                    targetExclude,
                    mask))
            {
                return false;
            }
        }

        route = new[] { SquadNavigationDirective.Walk(destination) };
        return true;
    }

    internal (int Attempts, int Successes) OperatorPursuitPlanCountsForDiagnostics
        => (_operatorPursuitPlanAttempts, _operatorPursuitPlanSuccesses);

    internal void ResetOperatorPursuitPlanCountsForDiagnostics()
    {
        _operatorPursuitPlanAttempts = 0;
        _operatorPursuitPlanSuccesses = 0;
        _nextOperatorPursuitPlanMilliseconds = 0;
    }
}
