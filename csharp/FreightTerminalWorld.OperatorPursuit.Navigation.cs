using System;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private const ulong OperatorPursuitPlanIntervalMilliseconds = 140;
    private const int OperatorPursuitExpansionCap = 1800;
    private const double OperatorPursuitPlanBudgetMilliseconds = 5.0;

    private ulong _nextOperatorPursuitPlanMilliseconds;
    private int _operatorPursuitPlanAttempts;
    private int _operatorPursuitPlanSuccesses;

    internal bool TryPlanOperatorPursuitRoute(
        EnemyOperator navigator,
        Vector3 destination,
        out SquadNavigationDirective[] route)
    {
        route = Array.Empty<SquadNavigationDirective>();
        if (!IsInstanceValid(navigator) || _squadTraversalLinks.Count == 0)
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
                SquadTraversalCapabilities.Walk | SquadTraversalCapabilities.Step,
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

    internal (int Attempts, int Successes) OperatorPursuitPlanCountsForDiagnostics
        => (_operatorPursuitPlanAttempts, _operatorPursuitPlanSuccesses);

    internal void ResetOperatorPursuitPlanCountsForDiagnostics()
    {
        _operatorPursuitPlanAttempts = 0;
        _operatorPursuitPlanSuccesses = 0;
        _nextOperatorPursuitPlanMilliseconds = 0;
    }
}
