using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private const float DemolitionSquadRouteReuseDistance = 2.4f;
    private const float DemolitionSquadRouteIntermediateTolerance = 0.7f;
    private const float DemolitionSquadRouteFinalTolerance = 1.15f;
    private const float DemolitionSquadRouteTickSeconds = 1.0f / 60.0f;

    private readonly Dictionary<SquadMate, DemolitionRouteCursor> _demolitionSquadRoutes = new();

    internal int DemolitionSquadRoutePlansForDiagnostics { get; private set; }
    internal int DemolitionSquadRouteReusesForDiagnostics { get; private set; }

    private bool TryResolveDemolitionSquadNavigation(
        SquadMate mate,
        Vector3 destination,
        out SquadNavigationDirective directive)
    {
        directive = SquadNavigationDirective.Walk(mate.GlobalPosition);
        if (_demolitionRoutePlanner is null
            || !(_demolitionArena?.Active ?? false)
            || !DemolitionLayout().IsInsideArena(mate.GlobalPosition, margin: 4.0f)
            || !DemolitionLayout().IsInsideArena(destination, margin: 4.0f))
        {
            return false;
        }

        const string routeKey = "squad_demolition";
        if (!_demolitionSquadRoutes.TryGetValue(mate, out var cursor)
            || !cursor.MatchesWithin(routeKey, destination, DemolitionSquadRouteReuseDistance))
        {
            cursor = new DemolitionRouteCursor();
            _demolitionSquadRoutes[mate] = cursor;
            PlanDemolitionSquadRoute(cursor, mate, destination, routeKey, countAsReplan: false);
        }
        else
        {
            DemolitionSquadRouteReusesForDiagnostics++;
        }

        if (cursor.TrackMovement(
                mate.GlobalPosition,
                DemolitionSquadRouteTickSeconds,
                movementRequested: !cursor.Complete))
        {
            PlanDemolitionSquadRoute(cursor, mate, destination, routeKey, countAsReplan: true);
        }

        cursor.Advance(
            mate.GlobalPosition,
            DemolitionSquadRouteIntermediateTolerance,
            DemolitionSquadRouteFinalTolerance);
        if (cursor.Complete)
        {
            if (cursor.ReachesDestination)
            {
                directive = SquadNavigationDirective.Walk(destination);
                return true;
            }

            if (cursor.ShouldRetryUnreachable(DemolitionSquadRouteTickSeconds))
            {
                PlanDemolitionSquadRoute(cursor, mate, destination, routeKey, countAsReplan: true);
                cursor.Advance(
                    mate.GlobalPosition,
                    DemolitionSquadRouteIntermediateTolerance,
                    DemolitionSquadRouteFinalTolerance);
            }
            if (cursor.Complete)
            {
                // The planner returned a safe frontier, so do not push the capsule into
                // a known blocker while waiting for the next bounded retry.
                directive = SquadNavigationDirective.Walk(mate.GlobalPosition);
                return true;
            }
        }

        directive = SquadNavigationDirective.Walk(cursor.CurrentWaypoint);
        return true;
    }

    private void PlanDemolitionSquadRoute(
        DemolitionRouteCursor cursor,
        SquadMate mate,
        Vector3 destination,
        string routeKey,
        bool countAsReplan)
    {
        if (_demolitionRoutePlanner is null)
        {
            return;
        }
        var route = _demolitionRoutePlanner.Plan(mate.GlobalPosition, destination);
        cursor.Reset(routeKey, mate.GlobalPosition, destination, route, countAsReplan);
        DemolitionSquadRoutePlansForDiagnostics++;
    }

    private void ClearDemolitionSquadRoute(SquadMate mate)
    {
        _demolitionSquadRoutes.Remove(mate);
    }

    private void ClearDemolitionSquadRoutes()
    {
        _demolitionSquadRoutes.Clear();
    }
}
