using System;
using Godot;

namespace OperationSteelTide;

public readonly record struct ResidentialEncounterEffects(
    Action<Vector3, float> DamagePlayer,
    Action<Vector3, float> ReportNoise,
    Action<Vector3, float> AlertEnemies,
    Func<Vector3, float, int> ScanEnemies,
    Action<ResidentialSupplyCache, int> SpawnGuards,
    Action<Vector3, string, string, Color> ShowMessage);

/// <summary>Applies the one-shot consequence attached to a residential chest plan.</summary>
public sealed class ResidentialRoomEncounterController
{
    private readonly ResidentialEncounterEffects _effects;

    public ResidentialRoomEncounterController(ResidentialEncounterEffects effects)
    {
        _effects = effects;
    }

    public void Handle(ResidentialSupplyCache cache)
    {
        var origin = cache.GlobalPosition;
        switch (cache.EventKind)
        {
            case ResidentialRoomEventKind.BoobyTrap:
                _effects.DamagePlayer(origin, 18.0f);
                _effects.ReportNoise(origin, 42.0f);
                _effects.AlertEnemies(origin, 48.0f);
                _effects.ShowMessage(
                    origin,
                    "residential_room_trap",
                    "BOOBY TRAP  //  ROOM COMPROMISED",
                    new Color(1.0f, 0.28f, 0.16f));
                break;
            case ResidentialRoomEventKind.Alarm:
                _effects.ReportNoise(origin, 58.0f);
                _effects.AlertEnemies(origin, 64.0f);
                _effects.ShowMessage(
                    origin,
                    "residential_room_alarm",
                    "ROOM ALARM  //  CONTACTS MOVING",
                    new Color(1.0f, 0.62f, 0.2f));
                break;
            case ResidentialRoomEventKind.Intel:
                var marked = _effects.ScanEnemies(origin, 58.0f);
                _effects.ShowMessage(
                    origin,
                    "residential_room_intel",
                    marked > 0 ? $"ROOM INTEL  //  {marked} CONTACTS MARKED" : "ROOM INTEL  //  NO CONTACTS",
                    new Color(0.28f, 0.9f, 0.62f));
                break;
            case ResidentialRoomEventKind.GuardAmbush:
                _effects.SpawnGuards(cache, cache.GuardCount);
                _effects.ReportNoise(origin, 52.0f);
                _effects.ShowMessage(
                    origin,
                    "residential_room_guard_ambush",
                    "GUARD AMBUSH  //  CONTACT CLOSE",
                    new Color(0.92f, 0.36f, 0.16f));
                break;
        }
    }
}
