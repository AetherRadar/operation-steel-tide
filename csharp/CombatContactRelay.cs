using System.Collections.Generic;

namespace OperationSteelTide;

internal sealed class CombatContactRelay
{
    private const ulong MinimumBroadcastIntervalPhysicsFrames = 12;

    private readonly Dictionary<(int TeamId, ulong TargetId), ulong> _nextBroadcastFrame = new();

    public int BroadcastCount { get; private set; }
    public int RecipientVisitCount { get; private set; }

    public bool TryBeginBroadcast(int teamId, ulong targetId, ulong physicsFrame)
    {
        if (_nextBroadcastFrame.Count > 128)
        {
            PruneExpiredEntries(physicsFrame);
        }
        var key = (teamId, targetId);
        if (_nextBroadcastFrame.TryGetValue(key, out var nextFrame) && physicsFrame < nextFrame)
        {
            return false;
        }

        _nextBroadcastFrame[key] = physicsFrame + MinimumBroadcastIntervalPhysicsFrames;
        BroadcastCount++;
        return true;
    }

    public void RecordRecipientVisit()
    {
        RecipientVisitCount++;
    }

    public void ResetDiagnostics()
    {
        BroadcastCount = 0;
        RecipientVisitCount = 0;
    }

    public void InvalidateTeam(int teamId)
    {
        List<(int TeamId, ulong TargetId)>? staleKeys = null;
        foreach (var key in _nextBroadcastFrame.Keys)
        {
            if (key.TeamId != teamId)
            {
                continue;
            }
            staleKeys ??= new List<(int TeamId, ulong TargetId)>();
            staleKeys.Add(key);
        }
        if (staleKeys is null)
        {
            return;
        }
        foreach (var key in staleKeys)
        {
            _nextBroadcastFrame.Remove(key);
        }
    }

    private void PruneExpiredEntries(ulong physicsFrame)
    {
        List<(int TeamId, ulong TargetId)>? staleKeys = null;
        foreach (var entry in _nextBroadcastFrame)
        {
            if (entry.Value > physicsFrame)
            {
                continue;
            }
            staleKeys ??= new List<(int TeamId, ulong TargetId)>();
            staleKeys.Add(entry.Key);
        }
        if (staleKeys is null)
        {
            return;
        }
        foreach (var key in staleKeys)
        {
            _nextBroadcastFrame.Remove(key);
        }
    }
}
