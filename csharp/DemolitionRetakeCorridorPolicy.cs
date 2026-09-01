using Godot;

namespace OperationSteelTide;

/// <summary>
/// Pure route-cost policy that keeps a deterministic team retake from collapsing onto
/// one shortest path. Alternate roles diverge through the middle of the route and may
/// converge again at their objective post.
/// </summary>
internal static class DemolitionRetakeCorridorPolicy
{
    private const float DirectCorridorHalfWidth = 5.0f;
    private const float WideFlankOffset = 13.0f;
    private const float RearApproachOffset = 16.0f;
    private const float DirectPenaltyPerMeter = 1.8f;
    private const float AlternatePenaltyPerMeter = 2.4f;

    public static float SegmentPenalty(
        Vector3 from,
        Vector3 to,
        Vector3 routeOrigin,
        Vector3 destination,
        Vector3 arenaMidpoint,
        int siteIndex,
        DemolitionRouteIntent routeIntent)
    {
        var axis = new Vector2(
            destination.X - routeOrigin.X,
            destination.Z - routeOrigin.Z);
        var axisLengthSquared = axis.LengthSquared();
        if (axisLengthSquared <= 1.0f)
        {
            return 0.0f;
        }

        var segmentMidpoint = (from + to) * 0.5f;
        var relative = new Vector2(
            segmentMidpoint.X - routeOrigin.X,
            segmentMidpoint.Z - routeOrigin.Z);
        var progress = Mathf.Clamp(relative.Dot(axis) / axisLengthSquared, 0.0f, 1.0f);
        var corridorFocus = 4.0f * progress * (1.0f - progress);
        if (corridorFocus <= 0.01f)
        {
            return 0.0f;
        }

        var axisDirection = axis.Normalized();
        var lateralAxis = new Vector2(-axisDirection.Y, axisDirection.X);
        var lateralOffset = relative.Dot(lateralAxis);
        var segmentLength = new Vector2(to.X - from.X, to.Z - from.Z).Length();
        var segmentWeight = Mathf.Clamp(segmentLength / 8.0f, 0.25f, 1.0f);
        if (routeIntent == DemolitionRouteIntent.DirectRetake)
        {
            var excess = Mathf.Max(0.0f, Mathf.Abs(lateralOffset) - DirectCorridorHalfWidth);
            return excess * DirectPenaltyPerMeter * corridorFocus * segmentWeight;
        }

        var outward = new Vector2(
            destination.X - arenaMidpoint.X,
            destination.Z - arenaMidpoint.Z);
        var outwardProjection = lateralAxis.Dot(outward);
        var outwardSide = Mathf.Abs(outwardProjection) > 0.1f
            ? Mathf.Sign(outwardProjection)
            : siteIndex == 0 ? -1.0f : 1.0f;
        var preferredSide = routeIntent == DemolitionRouteIntent.WideFlank
            ? outwardSide
            : -outwardSide;
        var preferredOffset = routeIntent == DemolitionRouteIntent.WideFlank
            ? WideFlankOffset
            : RearApproachOffset;
        var desiredOffset = preferredSide * preferredOffset * corridorFocus;
        return Mathf.Abs(lateralOffset - desiredOffset)
            * AlternatePenaltyPerMeter
            * corridorFocus
            * segmentWeight;
    }
}
