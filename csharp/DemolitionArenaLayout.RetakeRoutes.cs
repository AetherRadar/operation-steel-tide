using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public sealed partial class DemolitionArenaLayout
{
    /// <summary>Map-authored anchors that commit a retake role to a distinct corridor.</summary>
    internal IReadOnlyList<Vector3> RetakeCorridorWaypoints(
        int siteIndex,
        DemolitionRouteIntent routeIntent)
    {
        if ((uint)siteIndex >= SitePositions.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(siteIndex));
        }
        return MapId switch
        {
            DemolitionMapCatalog.TideforgeId => TideforgeRetakeCorridor(siteIndex, routeIntent),
            DemolitionMapCatalog.HarborLocksId => HarborRetakeCorridor(siteIndex, routeIntent),
            DemolitionMapCatalog.TideglassReactorId => TideglassRetakeCorridor(siteIndex, routeIntent),
            DemolitionMapCatalog.BazaarCrossingId => BazaarRetakeCorridor(siteIndex, routeIntent),
            _ => Array.Empty<Vector3>()
        };
    }

    private IReadOnlyList<Vector3> HarborRetakeCorridor(
        int siteIndex,
        DemolitionRouteIntent routeIntent)
        => (siteIndex, routeIntent) switch
        {
            (0, DemolitionRouteIntent.WideFlank) => WorldPoints(
                new Vector3(23.0f, 0.2f, -13.0f)),
            (1, DemolitionRouteIntent.WideFlank) => WorldPoints(
                new Vector3(14.0f, 0.2f, -22.0f)),
            _ => Array.Empty<Vector3>()
        };

    private IReadOnlyList<Vector3> TideforgeRetakeCorridor(
        int siteIndex,
        DemolitionRouteIntent routeIntent)
        => (siteIndex, routeIntent) switch
        {
            (0, DemolitionRouteIntent.RearApproach) => WorldPoints(
                new(14.6f, 0.2f, -1.0f),
                new(15.0f, 0.2f, 6.0f)),
            (1, DemolitionRouteIntent.WideFlank) => WorldPoints(
                new(-4.0f, 0.2f, -35.0f),
                new(-8.0f, 0.2f, -22.0f)),
            _ => Array.Empty<Vector3>()
        };

    private IReadOnlyList<Vector3> TideglassRetakeCorridor(
        int siteIndex,
        DemolitionRouteIntent routeIntent)
        => (siteIndex, routeIntent) switch
        {
            (0, DemolitionRouteIntent.WideFlank) => WorldPoints(
                new(-28.0f, 0.2f, -28.0f),
                new(-21.5f, 0.2f, 12.0f)),
            (0, DemolitionRouteIntent.RearApproach) => WorldPoints(
                new(-7.0f, 0.2f, -34.0f),
                new(-21.5f, 0.2f, 3.0f)),
            (1, DemolitionRouteIntent.WideFlank) => WorldPoints(
                new(-25.0f, 0.2f, -5.3f),
                new(8.5f, 0.2f, -5.0f)),
            _ => Array.Empty<Vector3>()
        };

    private IReadOnlyList<Vector3> BazaarRetakeCorridor(
        int siteIndex,
        DemolitionRouteIntent routeIntent)
        => (siteIndex, routeIntent) switch
        {
            (0, DemolitionRouteIntent.RearApproach) => WorldPoints(
                new(22.2f, 0.2f, -42.0f),
                new(3.0f, 0.2f, -18.0f)),
            _ => Array.Empty<Vector3>()
        };
}
