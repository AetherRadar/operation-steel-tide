using System;
using System.Collections.Generic;
using System.Linq;

namespace OperationSteelTide;

internal enum FreightIndustrialRoomContentKind
{
    Empty,
    RestingSoldiers,
    SupplyCache
}

internal sealed record FreightIndustrialRoomContentPlan(
    FreightIndustrialRoom Room,
    FreightIndustrialRoomContentKind Kind,
    int GuardCount,
    uint Roll);

/// <summary>Produces a seeded random distribution while keeping every match tactically varied.</summary>
internal static class FreightIndustrialRoomContentPlanner
{
    internal const int SupplyCacheRoomCount = 8;
    internal const int RestingSoldierRoomCount = 3;

    public static IReadOnlyList<FreightIndustrialRoomContentPlan> Plan(
        int seed,
        IReadOnlyList<FreightIndustrialRoom> rooms)
    {
        var ranked = rooms
            .Select(room => (Room: room, Roll: StableRoll(seed, room)))
            .OrderBy(item => item.Roll)
            .ThenBy(item => item.Room.Index)
            .ToArray();
        var plans = new FreightIndustrialRoomContentPlan[rooms.Count];
        for (var rank = 0; rank < ranked.Length; rank++)
        {
            var item = ranked[rank];
            var kind = rank < Math.Min(SupplyCacheRoomCount, ranked.Length)
                ? FreightIndustrialRoomContentKind.SupplyCache
                : rank < Math.Min(SupplyCacheRoomCount + RestingSoldierRoomCount, ranked.Length)
                    ? FreightIndustrialRoomContentKind.RestingSoldiers
                    : FreightIndustrialRoomContentKind.Empty;
            var guardCount = kind == FreightIndustrialRoomContentKind.RestingSoldiers
                ? 1
                : 0;
            plans[item.Room.Index] = new FreightIndustrialRoomContentPlan(
                item.Room,
                kind,
                guardCount,
                item.Roll);
        }
        return plans;
    }

    private static uint StableRoll(int seed, FreightIndustrialRoom room)
    {
        var hash = 2166136261u ^ unchecked((uint)seed);
        foreach (var character in room.Name)
        {
            hash ^= character;
            hash *= 16777619u;
        }
        hash ^= unchecked((uint)room.Index * 0x9E3779B9u);
        hash *= 16777619u;
        return hash;
    }
}
