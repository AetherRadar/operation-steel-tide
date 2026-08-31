using Godot;

namespace OperationSteelTide;

internal enum SquadTraversalKind : byte
{
    Walk,
    Step,
    Vault,
    Drop,
    Ladder
}

[System.Flags]
internal enum SquadTraversalCapabilities : byte
{
    Walk = 1 << (int)SquadTraversalKind.Walk,
    Step = 1 << (int)SquadTraversalKind.Step,
    Vault = 1 << (int)SquadTraversalKind.Vault,
    Drop = 1 << (int)SquadTraversalKind.Drop,
    Ladder = 1 << (int)SquadTraversalKind.Ladder,
    All = Walk | Step | Vault | Drop | Ladder
}

/// <summary>
/// One directed navigation instruction. Required instructions are portal/action
/// boundaries and must not be removed by ordinary line-of-sight shortcuts.
/// </summary>
internal readonly record struct SquadNavigationDirective(
    Vector3 Target,
    SquadTraversalKind Kind,
    int DirectedEdgeId,
    bool Required,
    bool SteppedDirect = false,
    bool PreciseTrail = false,
    Vector3 ActionOrigin = default,
    Vector3 ActionOutward = default)
{
    public static SquadNavigationDirective Walk(
        Vector3 target,
        bool steppedDirect = false,
        bool preciseTrail = false)
        => new(target, SquadTraversalKind.Walk, -1, false, steppedDirect, preciseTrail);
}

/// <summary>
/// Pure, allocation-free path data for an authored exterior ladder. The world
/// validates each ladder once when it is built; an actor only evaluates this
/// three-segment path while its current navigation directive is Ladder.
/// </summary>
internal readonly record struct AuthoredLadderTraversalPath(
    Vector3 BottomFeet,
    Vector3 WallBottom,
    Vector3 WallTop,
    Vector3 TopFeet,
    Vector3 Outward,
    float BottomMountLength,
    float VerticalLength,
    float TopMountLength,
    float Length)
{
    private const float WallOffset = 0.10f;
    private const float MantleLift = 0.08f;

    public static bool TryCreate(
        Vector3 bottomFeet,
        Vector3 topFeet,
        Vector3 outward,
        out AuthoredLadderTraversalPath path)
    {
        path = default;
        outward.Y = 0.0f;
        if (outward.LengthSquared() < 0.25f || topFeet.Y - bottomFeet.Y < 1.2f)
        {
            return false;
        }

        outward = outward.Normalized();
        var wallBottom = bottomFeet - outward * WallOffset;
        var wallTop = new Vector3(
            wallBottom.X,
            Mathf.Max(bottomFeet.Y + 0.8f, topFeet.Y + MantleLift),
            wallBottom.Z);
        var bottomMountLength = bottomFeet.DistanceTo(wallBottom);
        var verticalLength = wallBottom.DistanceTo(wallTop);
        var topMountLength = wallTop.DistanceTo(topFeet);
        var length = bottomMountLength + verticalLength + topMountLength;
        if (length <= 0.001f)
        {
            return false;
        }

        path = new AuthoredLadderTraversalPath(
            bottomFeet,
            wallBottom,
            wallTop,
            topFeet,
            outward,
            bottomMountLength,
            verticalLength,
            topMountLength,
            length);
        return true;
    }

    public Vector3 Evaluate(float distance)
    {
        distance = Mathf.Clamp(distance, 0.0f, Length);
        if (distance <= BottomMountLength)
        {
            var amount = BottomMountLength <= 0.001f ? 1.0f : distance / BottomMountLength;
            return BottomFeet.Lerp(WallBottom, Mathf.SmoothStep(0.0f, 1.0f, amount));
        }

        distance -= BottomMountLength;
        if (distance <= VerticalLength)
        {
            var amount = VerticalLength <= 0.001f ? 1.0f : distance / VerticalLength;
            return WallBottom.Lerp(WallTop, amount);
        }

        distance -= VerticalLength;
        var mantle = TopMountLength <= 0.001f ? 1.0f : distance / TopMountLength;
        mantle = Mathf.SmoothStep(0.0f, 1.0f, mantle);
        var position = WallTop.Lerp(TopFeet, mantle);
        position.Y += Mathf.Sin(mantle * Mathf.Pi) * 0.22f;
        return position;
    }
}

/// <summary>
/// Sparse authored connection between two walkable height bands. The forward
/// point array includes both endpoints and every non-skippable action checkpoint.
/// </summary>
internal readonly record struct SquadTraversalLink(
    int Id,
    string Source,
    SquadTraversalKind Kind,
    bool Bidirectional,
    Vector3[] ForwardPoints,
    float Cost,
    SquadNavigationDirective[] ForwardDirectives,
    SquadNavigationDirective[] ReverseDirectives);
