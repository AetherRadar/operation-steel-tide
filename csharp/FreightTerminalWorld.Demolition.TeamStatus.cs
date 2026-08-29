using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private void UpdateDemolitionTeamStatusHud()
    {
        if (!_demolitionMode || !IsInstanceValid(_hud))
        {
            return;
        }

        var carrier = ResolveDemolitionTeamStatusCarrier();
        var friendly = new List<DemolitionTeamStatusMember>(DemolitionSquadSize)
        {
            new(
                "PLAYER",
                OperatorRoles.Spec(_player.Role).Callsign,
                _player.Role,
                !_player.IsDead && !_localPlayerDowned && !_localPlayerEliminated,
                IsLocalPlayer: true,
                HasDevice: ReferenceEquals(carrier, _player))
        };
        foreach (var mate in _squadMates
                     .Where(IsInstanceValid)
                     .OrderBy(mate => mate.SquadSlot)
                     .Take(DemolitionSquadSize - 1))
        {
            friendly.Add(new DemolitionTeamStatusMember(
                $"MATE:{mate.SquadSlot}",
                mate.Callsign,
                mate.Role,
                !mate.IsDowned && !mate.IsBodyBag,
                IsLocalPlayer: false,
                HasDevice: ReferenceEquals(carrier, mate)));
        }

        var enemy = new List<DemolitionTeamStatusMember>(DemolitionSquadSize);
        var orderedOpponents = _demolitionOpponents
            .Where(IsInstanceValid)
            .OrderBy(opponent => opponent.NetworkId)
            .ThenBy(opponent => opponent.Name.ToString(), System.StringComparer.Ordinal)
            .Take(DemolitionSquadSize)
            .ToArray();
        for (var index = 0; index < orderedOpponents.Length; index++)
        {
            var opponent = orderedOpponents[index];
            enemy.Add(new DemolitionTeamStatusMember(
                $"ENEMY:{opponent.NetworkId}:{index}",
                GameLocalization.Format(
                    "demolition_enemy_slot",
                    _languageSetting,
                    "ENEMY {0}",
                    index + 1),
                DemolitionTeamStatusRole(opponent, index),
                !opponent.IsDead,
                IsLocalPlayer: false,
                HasDevice: ReferenceEquals(carrier, opponent)));
        }

        _hud.SetDemolitionTeamStatus(new DemolitionTeamStatusSnapshot(
            friendly,
            enemy,
            LocalDemolitionSide,
            DemolitionTeamStatusPhaseNow(),
            LocalDemolitionScore,
            OpposingDemolitionScore,
            _demolitionMatch.CurrentRound,
            DemolitionTeamStatusSecondsRemaining(),
            _demolitionMatch.IsOvertime));
    }

    private Node3D? ResolveDemolitionTeamStatusCarrier()
    {
        if (_demolitionDevicePlanted)
        {
            return null;
        }
        if (IsDemolitionNetworkClient)
        {
            return _networkDevicePhase == DemolitionDevicePhase.Carried
                ? DemolitionActorForId(_networkDeviceCarrierActorId)
                : null;
        }
        return _demolitionDeviceLifecycle.IsCarried
            ? ResolveDemolitionAttacker(_demolitionDeviceLifecycle.CarrierMemberId)
            : null;
    }

    private DemolitionTeamStatusPhase DemolitionTeamStatusPhaseNow()
    {
        if (_demolitionBuyPhaseActive)
        {
            return DemolitionTeamStatusPhase.Buy;
        }
        if (_demolitionDevicePlanted)
        {
            return DemolitionTeamStatusPhase.DeviceActive;
        }
        return _demolitionRoundActive
            ? DemolitionTeamStatusPhase.Live
            : DemolitionTeamStatusPhase.Intermission;
    }

    private float DemolitionTeamStatusSecondsRemaining()
    {
        if (_demolitionBuyPhaseActive)
        {
            return _demolitionBuyRemaining;
        }
        if (_demolitionRoundActive)
        {
            return _demolitionRemaining;
        }
        return _demolitionIntermissionRemaining;
    }

    private static OperatorRole DemolitionTeamStatusRole(EnemyOperator opponent, int index)
    {
        if (opponent.IsHumanProxy)
        {
            return opponent.NetworkRole;
        }
        var roles = OperatorRoles.CombatRoles;
        return roles[index % roles.Length];
    }
}
