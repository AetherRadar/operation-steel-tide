using System;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private readonly record struct AiLadderDiagnosticResult(
        bool AuthoredLinksReady,
        int LinkCount,
        bool FriendlyRouteUp,
        bool FriendlyRouteDown,
        bool EnemyRouteUp,
        bool EnemyRouteDown,
        bool FriendlyApproachUp,
        bool FriendlyApproachDown,
        bool EnemyApproachUp,
        bool EnemyApproachDown,
        bool FriendlyClimbedUp,
        bool FriendlyClimbedDown,
        bool EnemyClimbedUp,
        bool EnemyClimbedDown,
        bool EnemyStoodUp,
        bool EnemyStoodDown,
        bool EnemyReleasedUp,
        bool EnemyReleasedDown)
    {
        public bool Valid => AuthoredLinksReady
            && FriendlyRouteUp
            && FriendlyRouteDown
            && EnemyRouteUp
            && EnemyRouteDown
            && FriendlyApproachUp
            && FriendlyApproachDown
            && EnemyApproachUp
            && EnemyApproachDown
            && FriendlyClimbedUp
            && FriendlyClimbedDown
            && EnemyClimbedUp
            && EnemyClimbedDown
            && EnemyStoodUp
            && EnemyStoodDown
            && EnemyReleasedUp
            && EnemyReleasedDown;
    }

    private async void ValidateAiLadders()
    {
        DisableActorsForSurvivalDiagnostics();
        await WaitFrames(5);
        var representative = _roofAccessRoutes.FirstOrDefault(route =>
            route.Id == "WarehouseRoof");
        var result = representative is null
            ? default
            : RunAiLadderDiagnostics(representative);
        GD.Print(
            $"AI_LADDER_CHECK valid={result.Valid} links={result.AuthoredLinksReady}:{result.LinkCount} "
            + $"friendly_route={result.FriendlyRouteUp}/{result.FriendlyRouteDown} "
            + $"enemy_route={result.EnemyRouteUp}/{result.EnemyRouteDown} "
            + $"friendly_approach={result.FriendlyApproachUp}/{result.FriendlyApproachDown} "
            + $"enemy_approach={result.EnemyApproachUp}/{result.EnemyApproachDown} "
            + $"friendly_climb={result.FriendlyClimbedUp}/{result.FriendlyClimbedDown} "
            + $"enemy_climb={result.EnemyClimbedUp}/{result.EnemyClimbedDown} "
            + $"enemy_standing={result.EnemyStoodUp}/{result.EnemyStoodDown} "
            + $"enemy_released={result.EnemyReleasedUp}/{result.EnemyReleasedDown}");
        GD.Print($"AI_LADDER_PASS valid={result.Valid}");
        QuitDiagnosticAfterSceneCleanup(result.Valid ? 0 : 2);
    }

    private AiLadderDiagnosticResult RunAiLadderDiagnostics(RoofAccessRoute representative)
    {
        var ladderLinks = _squadTraversalLinks
            .Where(link => link.Source.StartsWith("roof_ladder:", StringComparison.Ordinal))
            .ToArray();
        var authoredLinksReady = ladderLinks.Length == FunctionalLadderCount
            && ladderLinks.All(link =>
                link.Kind == SquadTraversalKind.Ladder
                && link.Bidirectional
                && link.ForwardDirectives.Length >= 2
                && link.ReverseDirectives.Length >= 2
                && link.ForwardDirectives[^1].Kind == SquadTraversalKind.Ladder
                && link.ReverseDirectives[^1].Kind == SquadTraversalKind.Ladder
                && link.ForwardDirectives[^1].ActionOrigin.DistanceSquaredTo(link.ForwardPoints[0])
                    <= 0.0001f
                && link.ReverseDirectives[^1].ActionOrigin.DistanceSquaredTo(link.ForwardPoints[^1])
                    <= 0.0001f);
        var friendlyRouteUp = false;
        var friendlyRouteDown = false;
        var enemyRouteUp = false;
        var enemyRouteDown = false;
        var friendlyApproachUp = false;
        var friendlyApproachDown = false;
        var enemyApproachUp = false;
        var enemyApproachDown = false;
        var friendlyClimbedUp = false;
        var friendlyClimbedDown = false;
        var enemyClimbedUp = false;
        var enemyClimbedDown = false;
        var enemyStoodUp = false;
        var enemyStoodDown = false;
        var enemyReleasedUp = false;
        var enemyReleasedDown = false;
        var mate = _squadMates.FirstOrDefault(candidate => IsInstanceValid(candidate));
        var enemy = _enemies.FirstOrDefault(candidate => IsInstanceValid(candidate));
        _player.ProcessMode = ProcessModeEnum.Disabled;
        _player.GlobalPosition = new Vector3(250.0f, 50.0f, 250.0f);
        if (mate is not null && enemy is not null)
        {
            mate.ResetCombatTacticsForDiagnostics();
            mate.ResetNavigationTraversalForDiagnostics();
            enemy.ResetTacticalStateForDiagnostics();
            enemy.ProcessMode = ProcessModeEnum.Disabled;
            enemy.SentryMode = false;
            mate.GlobalPosition = representative.BottomFeet;
            friendlyRouteUp = TryPlanSquadLayeredRoute(
                    mate,
                    representative.TopFeet,
                    1800,
                    out var friendlyUpPlan,
                    out _)
                && friendlyUpPlan.Any(static directive =>
                    directive.Kind == SquadTraversalKind.Ladder);
            mate.GlobalPosition = representative.TopFeet;
            friendlyRouteDown = TryPlanSquadLayeredRoute(
                    mate,
                    representative.BottomFeet,
                    1800,
                    out var friendlyDownPlan,
                    out _)
                && friendlyDownPlan.Any(static directive =>
                    directive.Kind == SquadTraversalKind.Ladder);

            enemy.GlobalPosition = representative.BottomFeet;
            ResetOperatorPursuitPlanCountsForDiagnostics();
            enemyRouteUp = TryPlanOperatorPursuitRoute(
                    enemy,
                    representative.TopFeet,
                    out var enemyUpPlan)
                && enemyUpPlan.Any(static directive =>
                    directive.Kind == SquadTraversalKind.Ladder);
            enemy.GlobalPosition = representative.TopFeet;
            ResetOperatorPursuitPlanCountsForDiagnostics();
            enemyRouteDown = TryPlanOperatorPursuitRoute(
                    enemy,
                    representative.BottomFeet,
                    out var enemyDownPlan)
                && enemyDownPlan.Any(static directive =>
                    directive.Kind == SquadTraversalKind.Ladder);

            // Offset both endpoints beyond authored snap distance. These plans
            // must use the explicit cross-floor terminal connector path rather
            // than succeeding only because the actor was placed on the portal.
            var lateral = new Vector3(
                -representative.Outward.Z,
                0.0f,
                representative.Outward.X);
            var bottomApproach = representative.BottomFeet
                + lateral * 1.2f;
            var topApproach = representative.TopFeet
                + lateral * 1.2f;
            mate.GlobalPosition = bottomApproach;
            var friendlyApproachUpPlanned = TryPlanSquadLayeredRoute(
                    mate,
                    topApproach,
                    1800,
                    out var friendlyApproachUpPlan,
                    out _);
            friendlyApproachUp = friendlyApproachUpPlanned
                && friendlyApproachUpPlan.Any(static directive =>
                    directive.Kind == SquadTraversalKind.Ladder);
            mate.GlobalPosition = topApproach;
            friendlyApproachDown = TryPlanSquadLayeredRoute(
                    mate,
                    bottomApproach,
                    1800,
                    out var friendlyApproachDownPlan,
                    out _)
                && friendlyApproachDownPlan.Any(static directive =>
                    directive.Kind == SquadTraversalKind.Ladder);

            enemy.GlobalPosition = bottomApproach;
            ResetOperatorPursuitPlanCountsForDiagnostics();
            var enemyApproachUpPlanned = TryPlanOperatorPursuitRoute(
                    enemy,
                    topApproach,
                    out var enemyApproachUpPlan);
            enemyApproachUp = enemyApproachUpPlanned
                && enemyApproachUpPlan.Any(static directive =>
                    directive.Kind == SquadTraversalKind.Ladder);
            enemy.GlobalPosition = topApproach;
            ResetOperatorPursuitPlanCountsForDiagnostics();
            enemyApproachDown = TryPlanOperatorPursuitRoute(
                    enemy,
                    bottomApproach,
                    out var enemyApproachDownPlan)
                && enemyApproachDownPlan.Any(static directive =>
                    directive.Kind == SquadTraversalKind.Ladder);

            mate.GlobalPosition = representative.BottomFeet;
            mate.Velocity = Vector3.Zero;
            var friendlyTraversalCount = mate.CompletedNavigationTraversalsForDiagnostics;
            var friendlyUpStarted = mate.BeginNavigationLadderForDiagnostics(
                representative.BottomFeet,
                representative.TopFeet,
                representative.Outward);
            for (var frame = 0;
                 frame < 480
                    && mate.CompletedNavigationTraversalsForDiagnostics == friendlyTraversalCount;
                 frame++)
            {
                mate.AdvanceNavigationTraversalForDiagnostics(1.0f / 60.0f);
            }
            friendlyClimbedUp = friendlyUpStarted
                && mate.CompletedNavigationTraversalsForDiagnostics > friendlyTraversalCount
                && mate.LastCompletedNavigationTraversalKindForDiagnostics
                    == SquadTraversalKind.Ladder
                && mate.GlobalPosition.DistanceTo(representative.TopFeet) <= 0.05f;

            friendlyTraversalCount = mate.CompletedNavigationTraversalsForDiagnostics;
            var friendlyDownStarted = mate.BeginNavigationLadderForDiagnostics(
                representative.BottomFeet,
                representative.TopFeet,
                representative.Outward,
                startAtTop: true);
            for (var frame = 0;
                 frame < 480
                    && mate.CompletedNavigationTraversalsForDiagnostics == friendlyTraversalCount;
                 frame++)
            {
                mate.AdvanceNavigationTraversalForDiagnostics(1.0f / 60.0f);
            }
            friendlyClimbedDown = friendlyDownStarted
                && mate.CompletedNavigationTraversalsForDiagnostics > friendlyTraversalCount
                && mate.LastCompletedNavigationTraversalKindForDiagnostics
                    == SquadTraversalKind.Ladder
                && mate.GlobalPosition.DistanceTo(representative.BottomFeet) <= 0.05f;

            enemy.GlobalPosition = representative.BottomFeet;
            enemy.Velocity = Vector3.Zero;
            enemy.SetProne(true);
            var enemyUpStartedProne = enemy.IsProne;
            var enemyUpPostureClean = enemyUpStartedProne;
            var enemyUpTraversalSeen = false;
            var enemyTraversalCount = enemy.PursuitLadderTraversalsForDiagnostics;
            for (var frame = 0;
                 frame < 480
                    && enemy.PursuitLadderTraversalsForDiagnostics == enemyTraversalCount;
                 frame++)
            {
                var advanced = enemy.AdvancePursuitLadderForDiagnostics(
                    1.0f / 60.0f,
                    representative.BottomFeet,
                    representative.TopFeet,
                    representative.Outward);
                if (advanced)
                {
                    enemyUpPostureClean &= !enemy.IsProne;
                    enemyUpTraversalSeen |= enemy.IsPursuitLadderActiveForDiagnostics;
                }
            }
            enemyStoodUp = enemyUpStartedProne
                && enemyUpTraversalSeen
                && enemyUpPostureClean;
            enemyReleasedUp = enemyUpTraversalSeen
                && !enemy.IsPursuitLadderActiveForDiagnostics;
            enemyClimbedUp = enemy.PursuitLadderTraversalsForDiagnostics > enemyTraversalCount
                && enemy.GlobalPosition.DistanceTo(representative.TopFeet) <= 0.05f;

            enemy.SetProne(true);
            var enemyDownStartedProne = enemy.IsProne;
            var enemyDownPostureClean = enemyDownStartedProne;
            var enemyDownTraversalSeen = false;
            enemyTraversalCount = enemy.PursuitLadderTraversalsForDiagnostics;
            for (var frame = 0;
                 frame < 480
                    && enemy.PursuitLadderTraversalsForDiagnostics == enemyTraversalCount;
                 frame++)
            {
                var advanced = enemy.AdvancePursuitLadderForDiagnostics(
                    1.0f / 60.0f,
                    representative.BottomFeet,
                    representative.TopFeet,
                    representative.Outward,
                    startAtTop: true);
                if (advanced)
                {
                    enemyDownPostureClean &= !enemy.IsProne;
                    enemyDownTraversalSeen |= enemy.IsPursuitLadderActiveForDiagnostics;
                }
            }
            enemyStoodDown = enemyDownStartedProne
                && enemyDownTraversalSeen
                && enemyDownPostureClean;
            enemyReleasedDown = enemyDownTraversalSeen
                && !enemy.IsPursuitLadderActiveForDiagnostics;
            enemyClimbedDown = enemy.PursuitLadderTraversalsForDiagnostics > enemyTraversalCount
                && enemy.GlobalPosition.DistanceTo(representative.BottomFeet) <= 0.05f;

            mate.GlobalPosition = new Vector3(410.0f, 0.3f, 410.0f);
            enemy.GlobalPosition = new Vector3(430.0f, 0.3f, 430.0f);
        }

        return new AiLadderDiagnosticResult(
            authoredLinksReady,
            ladderLinks.Length,
            friendlyRouteUp,
            friendlyRouteDown,
            enemyRouteUp,
            enemyRouteDown,
            friendlyApproachUp,
            friendlyApproachDown,
            enemyApproachUp,
            enemyApproachDown,
            friendlyClimbedUp,
            friendlyClimbedDown,
            enemyClimbedUp,
            enemyClimbedDown,
            enemyStoodUp,
            enemyStoodDown,
            enemyReleasedUp,
            enemyReleasedDown);
    }
}
