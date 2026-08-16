using Godot;

namespace OperationSteelTide;

internal enum SquadTraversalKind : byte
{
    Walk,
    Step,
    Vault,
    Drop
}

/// <summary>
/// One directed navigation instruction. Required instructions are portal/action
/// boundaries and must not be removed by ordinary line-of-sight shortcuts.
/// </summary>
internal readonly record struct SquadNavigationDirective(
    Vector3 Target,
    SquadTraversalKind Kind,
    int DirectedEdgeId,
    bool Required)
{
    public static SquadNavigationDirective Walk(Vector3 target)
        => new(target, SquadTraversalKind.Walk, -1, false);
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
    float Cost);
