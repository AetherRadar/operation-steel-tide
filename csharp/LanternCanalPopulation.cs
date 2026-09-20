using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

/// <summary>Creates a bounded civilian population on the authored dry quays.</summary>
internal static class LanternCanalPopulation
{
    public static IReadOnlyList<CivilianNpc> Spawn(FreightTerminalWorld world, string language)
    {
        var residents = new List<CivilianNpc>(8);
        var homes = new Vector3[]
        {
            new(-68, 1.4f, 62), new(-68, 1.4f, 30), new(-68, 1.4f, -10), new(-68, 1.4f, -38),
            new(68, 1.4f, 56), new(68, 1.4f, 26), new(68, 1.4f, -14), new(68, 1.4f, -86)
        };
        for (var index = 0; index < homes.Length; index++)
        {
            var resident = new CivilianNpc { Name = $"LanternCanalResident{index + 1}" };
            resident.Configure(world, index % 4 == 0 ? CivilianRole.UtilityWorker : CivilianRole.Resident,
                index, 0, Transform3D.Identity, homes[index], new Vector2(.8f, 2.0f));
            resident.UseAuthoredVisual(index % 2 == 0 ? OperatorVisualId.Heron : OperatorVisualId.Magpie);
            world.AddChild(resident);
            resident.SetLanguage(language);
            residents.Add(resident);
        }
        return residents;
    }
}
