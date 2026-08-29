using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private readonly record struct BazaarActorLegResult(
        bool Reached,
        bool RouteReady,
        bool HeightReady,
        bool PhysicsReady,
        int Frames,
        int LongestStall,
        float HeightDelta)
    {
        public bool Ready => Reached
            && RouteReady
            && HeightReady
            && PhysicsReady
            && LongestStall < 90;
    }

    private readonly record struct BazaarActorRoundTripResult(
        string Actor,
        string Stair,
        BazaarActorLegResult Up,
        BazaarActorLegResult Down)
    {
        public bool Ready => Up.Ready && Down.Ready;
        public string MachineSummary => $"{Actor}:{Stair}:"
            + $"{Up.Reached}/{Down.Reached}:"
            + $"{Up.Frames}/{Down.Frames}:"
            + $"{Up.HeightDelta:0.00}/{-Down.HeightDelta:0.00}:"
            + $"stall{Up.LongestStall}/{Down.LongestStall}"
            + $":physics{Up.PhysicsReady}/{Down.PhysicsReady}";
    }

    private readonly record struct BazaarAiTraversalResult(
        bool Ready,
        string Failures,
        string Summary);

    private static bool BazaarVerticalPlannerReady(
        DemolitionArenaLayout layout,
        DemolitionRoutePlanner planner,
        out string failures)
    {
        var failed = new List<string>();
        var stairs = BazaarDetailedStairRuns(layout);
        foreach (var route in BazaarElevatedRoutes(layout))
        {
            var target = route.Points
                .Where(point => point.Y >= route.Points.Max(candidate => candidate.Y) - 0.05f)
                .ElementAt(route.Points.Count(point =>
                    point.Y >= route.Points.Max(candidate => candidate.Y) - 0.05f) / 2);
            var starts = new[]
            {
                (Name: "attack", Point: layout.AttackSpawn),
                (Name: "defense", Point: layout.DefenderSpawn),
                (Name: "ground", Point: BazaarOrdinaryGroundStart(layout, route.Name, stairs))
            };
            var platformStairs = stairs
                .Where(stair => stair.Platform == route.Name)
                .ToArray();
            foreach (var start in starts)
            {
                var planned = planner.Plan(start.Point, target);
                if (!BazaarRouteEntersViaRampLowEndpoint(
                        start.Point,
                        planned,
                        platformStairs,
                        layout.Origin.Y))
                {
                    failed.Add($"entry-{route.Name}-{start.Name}-{planned.ReachesDestination}-{planned.Waypoints.Count}");
                }
            }
        }

        foreach (var stair in stairs)
        {
            var middleIndex = stair.Points.Count / 2;
            var middle = stair.Points[middleIndex];
            var upward = planner.Plan(middle, stair.High);
            var downward = planner.Plan(middle, stair.Low);
            if (!BazaarRouteFollowsRamp(upward, stair.Points, middleIndex, 1))
            {
                failed.Add($"mid-up-{stair.Name}-{upward.ReachesDestination}-{upward.Waypoints.Count}");
            }
            if (!BazaarRouteFollowsRamp(downward, stair.Points, middleIndex, -1))
            {
                failed.Add($"mid-down-{stair.Name}-{downward.ReachesDestination}-{downward.Waypoints.Count}");
            }

            var firstDirection = stair.Points[1] - stair.Low;
            // Probe both halves from inside the 3.04 m guarded channel. A 1.0 m
            // offset preserves more than a player radius to either inner rail face.
            var side = new Vector3(-firstDirection.Z, 0.0f, firstDirection.X).Normalized() * 1.0f;
            var sideCenter = stair.Points[1];
            sideCenter.Y = layout.Origin.Y + 0.2f;
            foreach (var probe in new[]
                     {
                         (Name: "left", Point: sideCenter + side),
                         (Name: "right", Point: sideCenter - side)
                     })
            {
                var sideRoute = planner.Plan(probe.Point, stair.High);
                if (!BazaarRouteEntersViaRampLowEndpoint(
                        probe.Point,
                        sideRoute,
                        new[] { stair },
                        layout.Origin.Y))
                {
                    failed.Add(
                        $"side-entry-{stair.Name}-{probe.Name}-{sideRoute.ReachesDestination}-{sideRoute.Waypoints.Count}");
                }
            }
        }

        var cursorStart = layout.AuxiliaryPaths[2][0];
        var cursorTarget = layout.AuxiliaryPaths[2]
            .OrderByDescending(point => point.Y)
            .First();
        var directVerticalRoute = new DemolitionRouteResult(
            new[] { cursorTarget },
            true,
            cursorStart.DistanceTo(cursorTarget));
        var cursor = new DemolitionRouteCursor();
        cursor.Reset("bazaar-vertical", cursorStart, cursorTarget, directVerticalRoute, false);
        var wrongHeight = new Vector3(cursorTarget.X, cursorStart.Y, cursorTarget.Z);
        if (cursor.Advance(wrongHeight, 0.7f, 1.15f)
            || cursor.Matches("bazaar-vertical", wrongHeight)
            || cursor.WaypointIndex != 0)
        {
            failed.Add("cursor-flattened-height");
        }
        failures = string.Join('|', failed);
        return failed.Count == 0;
    }

    private static IReadOnlyList<(string Name, string Platform, IReadOnlyList<Vector3> Points,
        Vector3 Low, Vector3 High)> BazaarDetailedStairRuns(DemolitionArenaLayout layout)
    {
        var runs = new List<(string, string, IReadOnlyList<Vector3>, Vector3, Vector3)>(6);
        foreach (var route in BazaarElevatedRoutes(layout))
        {
            var top = route.Points.Max(point => point.Y);
            var plateau = route.Points
                .Select((point, index) => (point, index))
                .Where(entry => entry.point.Y >= top - 0.05f)
                .ToArray();
            var first = route.Points.Take(plateau[0].index + 1).ToArray();
            var second = route.Points.Skip(plateau[^1].index).Reverse().ToArray();
            runs.Add(($"{route.Name}-entry-1", route.Name, first, first[0], first[^1]));
            runs.Add(($"{route.Name}-entry-2", route.Name, second, second[0], second[^1]));
        }
        return runs;
    }

    private static Vector3 BazaarOrdinaryGroundStart(
        DemolitionArenaLayout layout,
        string platform,
        IReadOnlyList<(string Name, string Platform, IReadOnlyList<Vector3> Points,
            Vector3 Low, Vector3 High)> stairs)
    {
        var lows = stairs.Where(stair => stair.Platform == platform).Select(stair => stair.Low).ToArray();
        return BazaarGroundRoutes(layout)
            .SelectMany(route => route.Points)
            .Where(point => point.Y <= layout.Origin.Y + 0.35f)
            .Where(point => lows.Min(low => BazaarHorizontalDistance(point, low)) is >= 3.0f and <= 28.0f)
            .OrderBy(point => lows.Min(low => BazaarHorizontalDistance(point, low)))
            .First();
    }

    private static bool BazaarRouteEntersViaRampLowEndpoint(
        Vector3 start,
        DemolitionRouteResult planned,
        IReadOnlyList<(string Name, string Platform, IReadOnlyList<Vector3> Points,
            Vector3 Low, Vector3 High)> stairs,
        float groundY)
    {
        if (!planned.ReachesDestination || planned.Waypoints.Count < 2)
        {
            return false;
        }
        var points = new[] { start }.Concat(planned.Waypoints).ToArray();
        var firstElevated = Array.FindIndex(points, point => point.Y > groundY + 0.55f);
        if (firstElevated <= 0)
        {
            return false;
        }
        foreach (var stair in stairs)
        {
            var lowIndex = Array.FindLastIndex(
                points,
                firstElevated - 1,
                firstElevated,
                point => point.DistanceSquaredTo(stair.Low) <= 0.18f * 0.18f);
            if (lowIndex >= 0
                && lowIndex + 1 < points.Length
                && points[lowIndex + 1].DistanceSquaredTo(stair.Points[1]) <= 0.18f * 0.18f)
            {
                return true;
            }
        }
        return false;
    }

    private static bool BazaarRouteFollowsRamp(
        DemolitionRouteResult route,
        IReadOnlyList<Vector3> ramp,
        int startIndex,
        int direction)
    {
        if (!route.ReachesDestination || route.Waypoints.Count == 0)
        {
            return false;
        }
        var expected = startIndex + direction;
        foreach (var waypoint in route.Waypoints)
        {
            if (expected < 0 || expected >= ramp.Count
                || waypoint.DistanceSquaredTo(ramp[expected]) > 0.18f * 0.18f)
            {
                return false;
            }
            expected += direction;
        }
        return direction > 0 ? expected == ramp.Count : expected < 0;
    }

    private async Task<BazaarAiTraversalResult> BazaarAiRouteDirectivesReady(
        DemolitionArenaLayout layout)
    {
        const float delta = 1.0f / 60.0f;
        var stairs = BazaarDetailedStairRuns(layout);
        _player.ProcessMode = ProcessModeEnum.Disabled;
        _player.GlobalPosition = layout.AttackSpawn;
        _player.Velocity = Vector3.Zero;

        var squadResults = new List<BazaarActorRoundTripResult>(stairs.Count);
        var mate = new SquadMate { Name = "BazaarNavigationSquadProbe" };
        mate.Configure(this, _player, 1, OperatorRole.Assault, "RAMP-PROBE", false, 0);
        AddChild(mate);
        mate.SetPhysicsProcess(false);
        mate.SetProcess(false);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        var squadBodyReady = mate.IsInsideTree()
            && mate.ProcessMode != ProcessModeEnum.Disabled
            && mate.CollisionLayer == 4
            && mate.CollisionMask == 1;
        var squadStepsBefore = mate.BazaarRoutePhysicsStepsForDiagnostics;
        foreach (var stair in stairs)
        {
            ClearDemolitionSquadRoute(mate);
            ClearDemolitionSquadRouteFallback(mate);
            mate.GlobalPosition = stair.Low;
            mate.Velocity = Vector3.Zero;
            mate.ResetCombatTacticsForDiagnostics();
            await BazaarSettleSquadMate(mate, delta);
            var up = await BazaarWalkSquadLeg(mate, stair.High, true, delta);
            ClearDemolitionSquadRoute(mate);
            ClearDemolitionSquadRouteFallback(mate);
            mate.ResetCombatTacticsForDiagnostics();
            var down = await BazaarWalkSquadLeg(mate, stair.Low, false, delta);
            squadResults.Add(new BazaarActorRoundTripResult("squad", stair.Name, up, down));
        }
        var squadStepCount = mate.BazaarRoutePhysicsStepsForDiagnostics - squadStepsBefore;
        ClearDemolitionSquadRoute(mate);
        ClearDemolitionSquadRouteFallback(mate);
        mate.QueueFree();
        await WaitFrames(3);
        mate = null!;

        var enemyResults = new List<BazaarActorRoundTripResult>(stairs.Count);
        var enemy = new EnemyOperator
        {
            Name = "BazaarNavigationEnemyProbe",
            Player = _player,
            Main = this,
            MissionDirector = _missionDirector,
            NetworkId = int.MaxValue - 7,
            SimulationSeed = 4606
        };
        AddChild(enemy);
        enemy.SetPhysicsProcess(false);
        enemy.SetProcess(false);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        var enemyBodyReady = enemy.IsInsideTree()
            && enemy.ProcessMode != ProcessModeEnum.Disabled
            && enemy.CollisionLayer == 2
            && enemy.CollisionMask == 1;
        var enemyStepsBefore = enemy.BazaarRoutePhysicsStepsForDiagnostics;
        foreach (var stair in stairs)
        {
            _demolitionOpponentRoutes.Remove(enemy);
            enemy.GlobalPosition = stair.Low;
            enemy.Velocity = Vector3.Zero;
            await BazaarSettleEnemy(enemy, delta);
            var up = await BazaarWalkEnemyLeg(
                enemy, stair.High, true, $"bazaar-enemy-{stair.Name}-up", delta);
            _demolitionOpponentRoutes.Remove(enemy);
            var down = await BazaarWalkEnemyLeg(
                enemy, stair.Low, false, $"bazaar-enemy-{stair.Name}-down", delta);
            enemyResults.Add(new BazaarActorRoundTripResult("enemy", stair.Name, up, down));
        }
        var enemyStepCount = enemy.BazaarRoutePhysicsStepsForDiagnostics - enemyStepsBefore;
        _demolitionOpponentRoutes.Remove(enemy);
        enemy.QueueFree();
        await WaitFrames(3);
        enemy = null!;

        var actorResults = squadResults.Concat(enemyResults).ToArray();
        var expectedSquadSteps = squadResults.Sum(result => result.Up.Frames + result.Down.Frames);
        var expectedEnemySteps = enemyResults.Sum(result => result.Up.Frames + result.Down.Frames);
        var ready = squadBodyReady && enemyBodyReady
            && squadResults.Count == stairs.Count
            && enemyResults.Count == stairs.Count
            && actorResults.All(result => result.Ready)
            && squadStepCount >= expectedSquadSteps
            && enemyStepCount >= expectedEnemySteps;
        var failureItems = actorResults
            .Where(result => !result.Ready)
            .Select(result => result.MachineSummary)
            .ToList();
        if (!squadBodyReady)
        {
            failureItems.Add("squad-not-physical-body");
        }
        if (!enemyBodyReady)
        {
            failureItems.Add("enemy-not-physical-body");
        }
        if (squadStepCount < expectedSquadSteps)
        {
            failureItems.Add($"squad-move-and-slide-{squadStepCount}/{expectedSquadSteps}");
        }
        if (enemyStepCount < expectedEnemySteps)
        {
            failureItems.Add($"enemy-move-and-slide-{enemyStepCount}/{expectedEnemySteps}");
        }
        return new BazaarAiTraversalResult(
            ready,
            string.Join('|', failureItems),
            $"{string.Join(';', actorResults.Select(result => result.MachineSummary))};"
                + $"bodies{squadBodyReady}/{enemyBodyReady};steps{squadStepCount}/{enemyStepCount}");
    }

    private async Task BazaarSettleSquadMate(SquadMate mate, float delta)
    {
        for (var frame = 0; frame < 8; frame++)
        {
            mate.StepBazaarRoutePhysicsForDiagnostics(
                SquadNavigationDirective.Walk(mate.GlobalPosition, preciseTrail: true), delta);
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
    }

    private async Task BazaarSettleEnemy(EnemyOperator enemy, float delta)
    {
        for (var frame = 0; frame < 8; frame++)
        {
            enemy.StepBazaarRoutePhysicsForDiagnostics(delta);
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
    }

    private async Task<BazaarActorLegResult> BazaarWalkSquadLeg(
        SquadMate mate,
        Vector3 target,
        bool ascending,
        float delta)
    {
        var start = mate.GlobalPosition;
        var previous = start;
        var longestStall = 0;
        var stall = 0;
        var routeReady = true;
        var stepsBefore = mate.BazaarRoutePhysicsStepsForDiagnostics;
        var reached = false;
        var frames = 0;
        for (; frames < 420; frames++)
        {
            if (!TryResolveDemolitionSquadNavigation(mate, target, out var directive)
                || !_demolitionSquadRoutes.TryGetValue(mate, out var cursor)
                || !cursor.ReachesDestination)
            {
                routeReady = false;
                break;
            }
            mate.StepBazaarRoutePhysicsForDiagnostics(directive, delta);
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            BazaarTrackActorStall(mate.GlobalPosition, previous, target, ref stall, ref longestStall);
            previous = mate.GlobalPosition;
            if (BazaarActorReached(mate.GlobalPosition, target, ascending))
            {
                reached = true;
                break;
            }
        }
        var heightDelta = mate.GlobalPosition.Y - start.Y;
        var expected = Mathf.Abs(target.Y - start.Y);
        var heightReady = ascending
            ? heightDelta >= expected - 0.75f
            : -heightDelta >= expected - 0.75f;
        return new BazaarActorLegResult(
            reached, routeReady, heightReady,
            mate.BazaarRoutePhysicsStepsForDiagnostics > stepsBefore,
            frames, longestStall, heightDelta);
    }

    private async Task<BazaarActorLegResult> BazaarWalkEnemyLeg(
        EnemyOperator enemy,
        Vector3 target,
        bool ascending,
        string routeKey,
        float delta)
    {
        var start = enemy.GlobalPosition;
        var previous = start;
        var longestStall = 0;
        var stall = 0;
        var routeReady = true;
        var stepsBefore = enemy.BazaarRoutePhysicsStepsForDiagnostics;
        var reached = false;
        var frames = 0;
        for (; frames < 420; frames++)
        {
            if (!MoveDemolitionOpponentAlongRoute(enemy, target, routeKey, delta, 0.65f, 4.8f)
                || !_demolitionOpponentRoutes.TryGetValue(enemy, out var cursor)
                || !cursor.ReachesDestination)
            {
                routeReady = false;
                break;
            }
            enemy.StepBazaarRoutePhysicsForDiagnostics(delta);
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            BazaarTrackActorStall(enemy.GlobalPosition, previous, target, ref stall, ref longestStall);
            previous = enemy.GlobalPosition;
            if (BazaarActorReached(enemy.GlobalPosition, target, ascending))
            {
                reached = true;
                break;
            }
        }
        var heightDelta = enemy.GlobalPosition.Y - start.Y;
        var expected = Mathf.Abs(target.Y - start.Y);
        var heightReady = ascending
            ? heightDelta >= expected - 0.75f
            : -heightDelta >= expected - 0.75f;
        return new BazaarActorLegResult(
            reached, routeReady, heightReady,
            enemy.BazaarRoutePhysicsStepsForDiagnostics > stepsBefore,
            frames, longestStall, heightDelta);
    }

    private static bool BazaarActorReached(Vector3 position, Vector3 target, bool ascending)
        => BazaarHorizontalDistance(position, target) < 0.95f
            && (ascending ? position.Y >= target.Y - 0.62f : position.Y <= target.Y + 0.62f);

    private static void BazaarTrackActorStall(
        Vector3 position,
        Vector3 previous,
        Vector3 target,
        ref int stall,
        ref int longestStall)
    {
        stall = BazaarHorizontalDistance(position, target) > 1.1f
                && position.DistanceSquaredTo(previous) < 0.012f * 0.012f
            ? stall + 1
            : 0;
        longestStall = Math.Max(longestStall, stall);
    }

    private static float BazaarHorizontalDistance(Vector3 left, Vector3 right)
        => new Vector2(left.X - right.X, left.Z - right.Z).Length();
}
