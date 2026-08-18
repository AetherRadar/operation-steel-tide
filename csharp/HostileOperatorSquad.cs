using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

/// <summary>
/// Extraction-style rival squad: three full operators at a separated spawn pad.
/// Hostile to the player, other rival teams, and map NPCs.
/// </summary>
public sealed class HostileOperatorSquad
{
    public int TeamId { get; init; }
    public Vector3 SpawnPad { get; init; }
    public string CallsignPrefix { get; init; } = "RIVAL";
    public List<EnemyOperator> Members { get; } = new();

    public int AliveCount
    {
        get
        {
            var count = 0;
            foreach (var member in Members)
            {
                if (GodotObject.IsInstanceValid(member) && !member.IsDead)
                {
                    count++;
                }
            }
            return count;
        }
    }
}

/// <summary>
/// Edge/perimeter revive pads on the ~340×320 map. Five operator teams (player + 4 rivals)
/// each take one pad so open-fight distance at cold start is large.
/// </summary>
public static class ExtractionSpawnPads
{
    /// <summary>Map half-extents used when placing edge pads (matches FreightTerminalWorld map).</summary>
    public const float MapHalfWidth = 170.0f;
    public const float MapHalfDepth = 160.0f;
    public const float MapCenterZ = -60.0f;

    /// <summary>
    /// Eight edge/corner pads. Exactly five are used per match (1 player + 4 hostiles).
    /// Coordinates sit near the perimeter so pairwise distances stay large.
    /// </summary>
    public static readonly Vector3[] Pads =
    {
        new(-148.0f, 0.18f, MapCenterZ - 138.0f), // NW corner
        new(148.0f, 0.18f, MapCenterZ - 138.0f),  // NE corner
        new(-148.0f, 0.18f, MapCenterZ + 132.0f), // SW corner
        new(148.0f, 0.18f, MapCenterZ + 132.0f),  // SE corner
        new(0.0f, 0.18f, MapCenterZ - 155.0f),    // North apron, clear of North Quay 2
        new(0.0f, 0.18f, MapCenterZ + 140.0f),    // South mid
        new(-155.0f, 0.18f, MapCenterZ),          // West mid
        new(155.0f, 0.18f, MapCenterZ)           // East mid
    };

    /// <summary>
    /// Minimum pairwise pad distance required by gameplay + headless validators.
    /// Large enough that ContactAcquireRange (~48 m) and open-apron fights cannot start at deploy.
    /// </summary>
    public const float MinPadSeparationMeters = 110.0f;
    public const float MinPlayerHostileSeparationMeters = 130.0f;

    public const int OperatorTeamCount = 5;
    public const int HostileSquadTargetCount = 4;
    public const int SquadSize = 3;

    private static readonly Vector3[] HostileMemberOffsets =
    {
        new(-1.8f, 0.0f, 0.6f),
        new(1.8f, 0.0f, 0.6f),
        new(0.0f, 0.0f, -1.8f)
    };

    public static Vector3 FriendlyMemberPosition(Vector3 leaderPosition, Basis leaderBasis, int slot)
    {
        var lateral = slot == 1 ? -2.25f : 2.25f;
        return leaderPosition + leaderBasis.X * lateral + leaderBasis.Z * 3.2f;
    }

    public static Vector3 HostileMemberPosition(Vector3 spawnPad, int memberIndex)
        => spawnPad + HostileMemberOffsets[Mathf.Clamp(memberIndex, 0, HostileMemberOffsets.Length - 1)];

    public static float MinPairwiseDistance(IReadOnlyList<Vector3> pads)
    {
        var best = float.PositiveInfinity;
        for (var i = 0; i < pads.Count; i++)
        {
            for (var j = i + 1; j < pads.Count; j++)
            {
                var d = pads[i].DistanceTo(pads[j]);
                if (d < best)
                {
                    best = d;
                }
            }
        }
        return float.IsPositiveInfinity(best) ? 0.0f : best;
    }

    /// <summary>
    /// Pick player pad at random among edge pads, then greedily assign the farthest remaining
    /// pads to hostile squads until HostileSquadTargetCount is filled.
    /// </summary>
    public static void AssignMatchPads(
        RandomNumberGenerator rng,
        out Vector3 playerPad,
        out List<Vector3> hostilePads)
    {
        var pool = new List<Vector3>(Pads);
        var playerIndex = (int)(rng.Randi() % (uint)pool.Count);
        playerPad = pool[playerIndex];
        pool.RemoveAt(playerIndex);
        var assignedPlayerPad = playerPad;
        pool.RemoveAll(candidate => candidate.DistanceTo(assignedPlayerPad) < MinPlayerHostileSeparationMeters);

        hostilePads = new List<Vector3>(HostileSquadTargetCount);
        while (hostilePads.Count < HostileSquadTargetCount && pool.Count > 0)
        {
            var bestIndex = 0;
            var bestScore = float.NegativeInfinity;
            for (var i = 0; i < pool.Count; i++)
            {
                var candidate = pool[i];
                // Maximize distance to player and already-chosen hostile pads.
                var minToChosen = candidate.DistanceTo(playerPad);
                foreach (var existing in hostilePads)
                {
                    minToChosen = Mathf.Min(minToChosen, candidate.DistanceTo(existing));
                }
                if (minToChosen > bestScore)
                {
                    bestScore = minToChosen;
                    bestIndex = i;
                }
            }
            hostilePads.Add(pool[bestIndex]);
            pool.RemoveAt(bestIndex);
        }
    }
}

/// <summary>Shared firearm ballistics: wall geometry blocks damage (no wallbang).</summary>
public static class Ballistics
{
    /// <summary>
    /// Keep a visual muzzle from becoming a wallbang origin when the weapon model clips
    /// through thin cover. The returned point stays just before the first solid surface.
    /// </summary>
    public static Vector3 ResolveShotOrigin(
        World3D world,
        Vector3 bodyOrigin,
        Vector3 muzzleOrigin,
        Rid excludeRid)
    {
        if (world is null || bodyOrigin.DistanceSquaredTo(muzzleOrigin) < 0.0001f)
        {
            return bodyOrigin;
        }
        if (!PhysicsRaycast.TryHit(
                world,
                bodyOrigin,
                muzzleOrigin,
                excludeRid,
                uint.MaxValue,
                out var hit))
        {
            return muzzleOrigin;
        }
        var direction = bodyOrigin.DirectionTo(muzzleOrigin);
        return hit.Position - direction * 0.04f;
    }

    /// <summary>
    /// True when a ray from muzzle to aim hits the intended target node (or a child collider)
    /// before any other solid body. World walls on the default physics layer stop the shot.
    /// </summary>
    public static bool HasClearShot(World3D world, Vector3 from, Vector3 to, Node target, Rid excludeRid)
    {
        if (world is null || target is null || !GodotObject.IsInstanceValid(target))
        {
            return false;
        }
        if (!PhysicsRaycast.TryHit(
                world,
                from,
                to,
                excludeRid,
                uint.MaxValue,
                out var hit))
        {
            return false;
        }
        var collider = hit.Collider;
        if (collider == target)
        {
            return true;
        }
        if (collider is Node node)
        {
            return target == node || target.IsAncestorOf(node) || node.IsAncestorOf(target);
        }
        return false;
    }
}
