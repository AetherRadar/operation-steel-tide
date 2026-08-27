using System;
using System.Collections.Generic;

namespace OperationSteelTide;

/// <summary>Pure deterministic roster selection shared by local play and diagnostics.</summary>
public static class OperatorRosterRules
{
    public static OperatorRole RandomPlayerRole(ulong seed)
    {
        var roles = OperatorRoles.ExtractionRoles;
        return roles[StableIndex(seed, roles.Length)];
    }

    public static IReadOnlyList<OperatorRole> SelectAiRoles(
        OperatorRole playerRole,
        ulong seed,
        int count = 2)
    {
        var candidates = new List<OperatorRole>(OperatorRoles.ExtractionRoles.Length - 1);
        foreach (var role in OperatorRoles.ExtractionRoles)
        {
            if (role != playerRole)
            {
                candidates.Add(role);
            }
        }

        var state = seed == 0 ? 0x9E3779B97F4A7C15UL : seed;
        for (var index = candidates.Count - 1; index > 0; index--)
        {
            state = Mix(state + (ulong)index);
            var swap = (int)(state % (ulong)(index + 1));
            (candidates[index], candidates[swap]) = (candidates[swap], candidates[index]);
        }
        return candidates.GetRange(0, Math.Min(count, candidates.Count));
    }

    public static OperatorVisualId RivalVisual(ulong seed)
        => StableIndex(seed, 2) == 0
            ? OperatorVisualId.Garrison
            : OperatorVisualId.FemaleFieldOperator;

    private static int StableIndex(ulong seed, int count)
        => (int)(Mix(seed == 0 ? 0xD1B54A32D192ED03UL : seed) % (ulong)count);

    private static ulong Mix(ulong value)
    {
        value ^= value >> 30;
        value *= 0xBF58476D1CE4E5B9UL;
        value ^= value >> 27;
        value *= 0x94D049BB133111EBUL;
        return value ^ value >> 31;
    }
}
