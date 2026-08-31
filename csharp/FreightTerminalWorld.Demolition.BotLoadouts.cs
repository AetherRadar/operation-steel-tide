using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private readonly List<WeaponBuild?> _demolitionOpponentRoundWeapons = new();
    private int _demolitionAuthoritativeWeaponLoadoutRound;
    private int _demolitionAlphaWeaponLoadout;
    private int _demolitionBravoWeaponLoadout;

    internal IReadOnlyList<WeaponPlatform?> DemolitionOpponentPlatformsForDiagnostics
        => _demolitionOpponentRoundWeapons
            .Select(build => build?.Platform)
            .ToArray();

    private void PlanDemolitionOpponentRoundWeapons(int funds)
    {
        _demolitionOpponentRoundWeapons.Clear();
        _demolitionOpponentRoundWeapons.AddRange(
            DemolitionBotLoadoutPlanner.Plan(funds, DemolitionSquadSize));
    }

    private WeaponBuild? DemolitionOpponentRoundWeaponForSlot(int slot)
    {
        var safeSlot = System.Math.Clamp(slot, 0, DemolitionSquadSize - 1);
        return safeSlot < _demolitionOpponentRoundWeapons.Count
            ? _demolitionOpponentRoundWeapons[safeSlot]?.Clone()
            : DemolitionBotLoadoutPlanner.BuildForSlot(
                _demolitionOpponentEconomy.Funds,
                safeSlot);
    }

    private void ResetDemolitionAuthoritativeWeaponLoadouts()
    {
        _demolitionAuthoritativeWeaponLoadoutRound = 0;
        _demolitionAlphaWeaponLoadout = 0;
        _demolitionBravoWeaponLoadout = 0;
    }

    private int CaptureDemolitionTeamWeaponLoadout(DemolitionNetworkTeam team)
        => DemolitionBotLoadoutNetworkCodec.EncodePlatforms(
            DemolitionWeaponPlatformForSlot(team, 0),
            DemolitionWeaponPlatformForSlot(team, 1),
            DemolitionWeaponPlatformForSlot(team, 2),
            DemolitionWeaponPlatformForSlot(team, 3),
            DemolitionWeaponPlatformForSlot(team, 4));

    private WeaponPlatform? DemolitionWeaponPlatformForSlot(
        DemolitionNetworkTeam team,
        int slot)
    {
        if (team == _demolitionLocalNetworkTeam)
        {
            if (slot == _demolitionLocalNetworkSlot)
            {
                return DemolitionWeaponPlatformForActor(_player);
            }
            for (var index = 0; index < _squadMates.Count; index++)
            {
                var mate = _squadMates[index];
                if (IsInstanceValid(mate) && mate.SquadSlot == slot)
                {
                    return DemolitionWeaponPlatformForActor(mate);
                }
            }
            return null;
        }

        var actorId = DemolitionActorId(team, slot);
        for (var index = 0; index < _demolitionOpponents.Count; index++)
        {
            var opponent = _demolitionOpponents[index];
            if (IsInstanceValid(opponent) && opponent.NetworkId == actorId)
            {
                return DemolitionWeaponPlatformForActor(opponent);
            }
        }
        return null;
    }

    private static WeaponPlatform? DemolitionWeaponPlatformForActor(Node3D? actor)
    {
        if (actor is TacticalPlayer player)
        {
            var stablePlayerWeapon = player.HasActiveFirearm
                ? player.EquippedWeapon
                : player.PrimaryWeaponForHud
                    ?? player.SecondaryWeaponForHud
                    ?? player.SidearmWeaponForHud;
            return stablePlayerWeapon?.Platform;
        }
        return actor switch
        {
            SquadMate mate when mate.HasFireablePrimary => mate.CarriedWeapon.Platform,
            EnemyOperator enemy when enemy.HasFireablePrimary => enemy.CarriedWeapon.Platform,
            _ => null
        };
    }

    private bool ApplyDemolitionAuthoritativeWeaponLoadouts(
        int round,
        int alphaWeaponLoadout,
        int bravoWeaponLoadout)
    {
        if (round < 1
            || !DemolitionBotLoadoutNetworkCodec.IsValid(alphaWeaponLoadout)
            || !DemolitionBotLoadoutNetworkCodec.IsValid(bravoWeaponLoadout))
        {
            return false;
        }
        var changed = _demolitionAuthoritativeWeaponLoadoutRound != round
            || _demolitionAlphaWeaponLoadout != alphaWeaponLoadout
            || _demolitionBravoWeaponLoadout != bravoWeaponLoadout;
        _demolitionAuthoritativeWeaponLoadoutRound = round;
        _demolitionAlphaWeaponLoadout = alphaWeaponLoadout;
        _demolitionBravoWeaponLoadout = bravoWeaponLoadout;
        if (!changed)
        {
            return false;
        }

        var opposingPacked = _demolitionLocalNetworkTeam == DemolitionNetworkTeam.Alpha
            ? bravoWeaponLoadout
            : alphaWeaponLoadout;
        _demolitionOpponentRoundWeapons.Clear();
        _demolitionOpponentRoundWeapons.AddRange(
            DemolitionBotLoadoutNetworkCodec.Decode(opposingPacked));
        return true;
    }

    private void RefreshDemolitionAuthoritativeWeaponLoadouts()
    {
        if (_demolitionAuthoritativeWeaponLoadoutRound != _demolitionMatch.CurrentRound)
        {
            return;
        }

        var localPacked = _demolitionLocalNetworkTeam == DemolitionNetworkTeam.Alpha
            ? _demolitionAlphaWeaponLoadout
            : _demolitionBravoWeaponLoadout;
        foreach (var mate in _squadMates.Where(IsInstanceValid))
        {
            if (mate.SquadSlot == _demolitionLocalNetworkSlot || mate.IsBodyBag)
            {
                continue;
            }
            var desired = DemolitionBotLoadoutNetworkCodec.WeaponForSlot(
                localPacked,
                mate.SquadSlot);
            if (!DemolitionActorWeaponMatches(mate, desired))
            {
                mate.ConfigureDemolitionRoundLoadout(desired);
            }
        }

        foreach (var opponent in _demolitionOpponents.Where(IsInstanceValid).ToArray())
        {
            if (opponent.IsDead)
            {
                continue;
            }
            var slot = DemolitionActorSlot(opponent.NetworkId);
            var desired = DemolitionOpponentRoundWeaponForSlot(slot);
            if (!DemolitionActorWeaponMatches(opponent, desired))
            {
                ReplaceDemolitionOpponentProxyWeapon(opponent, slot, desired);
            }
        }
    }

    private static bool DemolitionActorWeaponMatches(Node3D actor, WeaponBuild? desired)
        => actor switch
        {
            SquadMate mate => desired is null
                ? !mate.HasFireablePrimary
                : mate.HasFireablePrimary && mate.CarriedWeapon.Platform == desired.Platform,
            EnemyOperator enemy => desired is null
                ? !enemy.HasFireablePrimary
                : enemy.HasFireablePrimary && enemy.CarriedWeapon.Platform == desired.Platform,
            _ => false
        };

    private void ReplaceDemolitionOpponentProxyWeapon(
        EnemyOperator existing,
        int slot,
        WeaponBuild? desired)
    {
        var team = DemolitionActorTeam(existing.NetworkId);
        var peerId = existing.NetworkPeerId;
        var role = existing.NetworkRole;
        var human = existing.IsHumanProxy;
        var position = existing.GlobalPosition;
        var rotation = existing.Rotation;
        var health = existing.CurrentHealth;
        var processMode = existing.ProcessMode;
        var remoteKeys = _remoteDemolitionOpponents
            .Where(pair => ReferenceEquals(pair.Value, existing))
            .Select(pair => pair.Key)
            .ToArray();

        _lootSources.Remove(existing);
        _demolitionOpponents.Remove(existing);
        _enemies.Remove(existing);
        existing.ProcessMode = ProcessModeEnum.Disabled;
        existing.QueueFree();

        var replacement = SpawnDemolitionOpponentAtSlot(slot, team);
        if (desired is null)
        {
            replacement.ApplyColdStartUnarmed();
        }
        replacement.ConfigureNetworkProxy(
            peerId != 0 ? peerId : -replacement.NetworkId,
            role,
            human);
        replacement.SetRemoteNetworkState(role, position, rotation, health, dead: false);
        replacement.ProcessMode = processMode;
        foreach (var remoteKey in remoteKeys)
        {
            _remoteDemolitionOpponents[remoteKey] = replacement;
        }
    }
}
