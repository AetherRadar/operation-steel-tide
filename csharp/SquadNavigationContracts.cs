using Godot;

namespace OperationSteelTide;

internal enum SquadTraversalKind : byte
{
    Walk,
    Step,
    Vault,
    Drop
}

[System.Flags]
internal enum SquadTraversalCapabilities : byte
{
    Walk = 1 << (int)SquadTraversalKind.Walk,
    Step = 1 << (int)SquadTraversalKind.Step,
    Vault = 1 << (int)SquadTraversalKind.Vault,
    Drop = 1 << (int)SquadTraversalKind.Drop,
    All = Walk | Step | Vault | Drop
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
    bool PreciseTrail = false)
{
    public static SquadNavigationDirective Walk(
        Vector3 target,
        bool steppedDirect = false,
        bool preciseTrail = false)
        => new(target, SquadTraversalKind.Walk, -1, false, steppedDirect, preciseTrail);
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
