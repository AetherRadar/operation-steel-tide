using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private bool _demolitionNetworkActionReceivedForDiagnostics;
    private bool _demolitionNetworkActionAppliedForDiagnostics;
    private float _demolitionNetworkActionDistanceForDiagnostics = -1.0f;

    private bool IsDemolitionActorDamagedForDiagnostics(int actorId)
    {
        var team = DemolitionActorTeam(actorId);
        var slot = DemolitionActorSlot(actorId);
        if (team == DemolitionNetworkTeam.Alpha)
        {
            if (slot == 0)
            {
                return _player.Health < _player.MaxHealth;
            }
            var mate = _squadMates.FirstOrDefault(candidate => IsInstanceValid(candidate)
                && candidate.SquadSlot == slot);
            return mate is not null && mate.Health < mate.MaxHealth;
        }
        var opponent = _demolitionOpponents.FirstOrDefault(candidate => IsInstanceValid(candidate)
            && candidate.NetworkId == actorId);
        return opponent is not null && opponent.CurrentHealth < opponent.MaxHealth;
    }

    private async void ValidateDemolitionNetworkSession(
        bool host,
        DemolitionNetworkTeam clientTeam = DemolitionNetworkTeam.Bravo)
    {
        var mode = host ? SquadSessionMode.Host : SquadSessionMode.Join;
        var team = host ? DemolitionNetworkTeam.Alpha : clientTeam;
        var clientSlot = clientTeam == DemolitionNetworkTeam.Alpha ? 1 : 0;
        var clientActorId = DemolitionActorId(clientTeam, clientSlot);
        var clientShotTargetId = DemolitionActorId(
            clientTeam == DemolitionNetworkTeam.Alpha
                ? DemolitionNetworkTeam.Bravo
                : DemolitionNetworkTeam.Alpha,
            0);
        OnDemolitionDeploymentRequested(
            (int)(host ? OperatorRole.Assault : OperatorRole.Recon),
            (int)WeaponPlatform.M4A1,
            1,
            (int)WeaponPlatform.P226,
            DemolitionMapCatalog.TideforgeId,
            (int)mode,
            "127.0.0.1",
            (int)team);
        await ToSignal(GetTree().CreateTimer(3.0f), SceneTreeTimer.SignalName.Timeout);

        if (host)
        {
            _demolitionBuyPhaseActive = false;
            _demolitionRoundActive = true;
            _player.UiLocked = false;
            ApplyDemolitionNetworkDamage(
                clientActorId,
                18.0f,
                _player.GlobalPosition + Vector3.Forward * 4.0f,
                _player);
            _squadNetwork.BroadcastShot(
                _player.GlobalPosition + Vector3.Up,
                _player.GlobalPosition + Vector3.Forward * 4.0f,
                clientActorId,
                18.0f);
            if (clientTeam == DemolitionNetworkTeam.Bravo)
            {
                ForceDemolitionDeviceCarrierForDiagnostics(_player);
                PlantDemolitionDevice(0, byPlayerTeam: true, _player);
            }
            else
            {
                var remoteCarrier = _squadMates.FirstOrDefault(mate => IsInstanceValid(mate)
                    && mate.IsHumanProxy && mate.SquadSlot == clientSlot);
                if (remoteCarrier is not null)
                {
                    ForceDemolitionDeviceCarrierForDiagnostics(remoteCarrier);
                }
            }
        }
        else
        {
            _squadNetwork.BroadcastShot(
                _player.GlobalPosition + Vector3.Up,
                _player.GlobalPosition + Vector3.Forward * 4.0f,
                clientShotTargetId,
                18.0f);
        }

        if (host)
        {
            await ToSignal(GetTree().CreateTimer(5.0f), SceneTreeTimer.SignalName.Timeout);
        }
        else
        {
            if (clientTeam == DemolitionNetworkTeam.Alpha)
            {
                var carrierDeadline = Time.GetTicksMsec() + 3000;
                while (_networkDeviceCarrierActorId != LocalDemolitionActorId
                    && Time.GetTicksMsec() < carrierDeadline)
                {
                    await ToSignal(GetTree().CreateTimer(0.1f), SceneTreeTimer.SignalName.Timeout);
                }
                _player.GlobalPosition = DemolitionLayout().SitePositions[0];
                await ToSignal(GetTree().CreateTimer(0.75f), SceneTreeTimer.SignalName.Timeout);
                _squadNetwork.RequestDemolitionAction(DemolitionNetworkAction.Plant, 0);
            }
            else
            {
                var plantDeadline = Time.GetTicksMsec() + 3000;
                while ((!_demolitionDevicePlanted || _demolitionActiveSite < 0)
                    && Time.GetTicksMsec() < plantDeadline)
                {
                    await ToSignal(GetTree().CreateTimer(0.1f), SceneTreeTimer.SignalName.Timeout);
                }
                if (_demolitionDevicePlanted && _demolitionActiveSite >= 0)
                {
                    _player.GlobalPosition = DemolitionLayout().SitePositions[_demolitionActiveSite];
                    await ToSignal(GetTree().CreateTimer(0.75f), SceneTreeTimer.SignalName.Timeout);
                    _squadNetwork.RequestDemolitionAction(
                        DemolitionNetworkAction.Defuse, _demolitionActiveSite);
                }
            }
            await ToSignal(GetTree().CreateTimer(2.0f), SceneTreeTimer.SignalName.Timeout);
        }

        var assigned = host
            ? _demolitionLocalNetworkTeam == DemolitionNetworkTeam.Alpha
                && _demolitionLocalNetworkSlot == 0
            : _demolitionLocalNetworkTeam == clientTeam
                && _demolitionLocalNetworkSlot == clientSlot;
        var teamPopulation = clientTeam == DemolitionNetworkTeam.Alpha
            ? DemolitionNetworkFriendlyHumanCount >= 2
            : DemolitionNetworkOpponentHumanCount >= 1;
        var remoteRepresentation = clientTeam == DemolitionNetworkTeam.Alpha
            ? _squadMates.Any(mate => IsInstanceValid(mate) && mate.IsHumanProxy)
            : host
                ? _remoteDemolitionOpponents.Values.Any(IsInstanceValid)
                : _demolitionOpponents.Any(opponent => IsInstanceValid(opponent)
                    && opponent.IsNetworkProxy
                    && DemolitionActorTeam(opponent.NetworkId) == DemolitionNetworkTeam.Alpha);
        var damageRelayed = host
            ? IsDemolitionActorDamagedForDiagnostics(clientActorId)
                && IsDemolitionActorDamagedForDiagnostics(clientShotTargetId)
            : _player.Health < _player.MaxHealth;
        var objectiveSynchronized = clientTeam == DemolitionNetworkTeam.Alpha
            ? _demolitionDevicePlanted && _demolitionActiveSite == 0
            : _demolitionMatch.OpponentScore >= 1;
        var valid = _squadNetwork.IsOnline
            && _squadNetwork.ConnectedPeerCount == 1
            && assigned
            && teamPopulation
            && remoteRepresentation
            && damageRelayed
            && objectiveSynchronized;
        GD.Print($"DEMOLITION_NETWORK_CHECK mode={(host ? "host" : "client")} requested_team={clientTeam} online={_squadNetwork.IsOnline} peers={_squadNetwork.ConnectedPeerCount} team={_demolitionLocalNetworkTeam} slot={_demolitionLocalNetworkSlot} humans={DemolitionNetworkHumanCount} friendly_humans={DemolitionNetworkFriendlyHumanCount} opponent_humans={DemolitionNetworkOpponentHumanCount} remote_representation={remoteRepresentation} damage={damageRelayed} objective={objectiveSynchronized} score={_demolitionMatch.PlayerScore}:{_demolitionMatch.OpponentScore} planted={_demolitionDevicePlanted} site={_demolitionActiveSite} action_received={_demolitionNetworkActionReceivedForDiagnostics} action_applied={_demolitionNetworkActionAppliedForDiagnostics} action_distance={_demolitionNetworkActionDistanceForDiagnostics:0.00}");
        GD.Print($"DEMOLITION_NETWORK_PASS valid={valid}");
        if (!host)
        {
            await ToSignal(GetTree().CreateTimer(2.5f), SceneTreeTimer.SignalName.Timeout);
        }
        GetTree().Quit(valid ? 0 : 2);
    }
}
