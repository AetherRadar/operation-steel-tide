using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private async void ValidateDemolitionNetworkRoster(
        bool host,
        DemolitionNetworkTeam clientTeam = DemolitionNetworkTeam.Alpha)
    {
        var endpoint = ResolveNetworkDiagnosticEndpoint(OS.GetCmdlineUserArgs());
        var role = host
            ? OperatorRole.Assault
            : clientTeam == DemolitionNetworkTeam.Alpha ? OperatorRole.Medic : OperatorRole.Recon;
        OnDemolitionDeploymentRequested(
            (int)role,
            (int)WeaponPlatform.M4A1,
            1,
            (int)WeaponPlatform.P226,
            DemolitionMapCatalog.TideforgeId,
            (int)(host ? SquadSessionMode.Host : SquadSessionMode.Join),
            endpoint,
            (int)(host ? DemolitionNetworkTeam.Alpha : clientTeam));

        var lobbyObserved = false;
        var rosterReady = false;
        var assigned = host;
        var deadline = Time.GetTicksMsec() + 14000;
        while (Time.GetTicksMsec() < deadline)
        {
            lobbyObserved |= _hud.IsDemolitionNetworkLobbyWaiting && !_demolitionMode;
            rosterReady = _squadNetwork.RegisteredDemolitionPlayerCount == 3
                && _squadNetwork.DemolitionPlayerCount(DemolitionNetworkTeam.Alpha) == 2
                && _squadNetwork.DemolitionPlayerCount(DemolitionNetworkTeam.Bravo) == 1
                && _hud.DemolitionNetworkLobbyPlayerCount == 3;
            assigned = host
                || _demolitionLocalNetworkTeam == clientTeam
                    && _demolitionLocalNetworkSlot
                        == (clientTeam == DemolitionNetworkTeam.Alpha ? 1 : 0);
            if (rosterReady && assigned)
            {
                break;
            }
            await ToSignal(GetTree().CreateTimer(0.1, true), SceneTreeTimer.SignalName.Timeout);
        }

        var valid = _squadNetwork.IsOnline
            && _demolitionLobbyDeployment is not null
            && !_demolitionMode
            && !_squadDeployed
            && lobbyObserved
            && rosterReady
            && assigned
            && _hud.DemolitionNetworkLobbyCanStart == host;
        GD.Print($"DEMOLITION_NETWORK_ROSTER_CHECK mode={(host ? "host" : "client")} requested_team={clientTeam} valid={valid} online={_squadNetwork.IsOnline} registered={_squadNetwork.RegisteredDemolitionPlayerCount} alpha={_squadNetwork.DemolitionPlayerCount(DemolitionNetworkTeam.Alpha)} bravo={_squadNetwork.DemolitionPlayerCount(DemolitionNetworkTeam.Bravo)} hud_players={_hud.DemolitionNetworkLobbyPlayerCount} can_start={_hud.DemolitionNetworkLobbyCanStart} lobby={lobbyObserved} assigned={assigned} team={_demolitionLocalNetworkTeam} slot={_demolitionLocalNetworkSlot} deployed={_squadDeployed}");
        GD.Print($"DEMOLITION_NETWORK_ROSTER_PASS valid={valid}");
        await ToSignal(GetTree().CreateTimer(1.0, true), SceneTreeTimer.SignalName.Timeout);
        GetTree().Quit(valid ? 0 : 2);
    }
}
