using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private bool _demolitionNetworkActionReceivedForDiagnostics;
    private bool _demolitionNetworkActionAppliedForDiagnostics;
    private float _demolitionNetworkActionDistanceForDiagnostics = -1.0f;

    private Node3D? DemolitionActorForDiagnostics(int actorId)
        => DemolitionActorForId(actorId);

    private bool IsDemolitionActorDamagedForDiagnostics(int actorId)
    {
        var actor = DemolitionActorForDiagnostics(actorId);
        if (actor is TacticalPlayer player)
        {
            return player.Health < player.MaxHealth;
        }
        if (actor is SquadMate mate)
        {
            return mate.Health < mate.MaxHealth;
        }
        return actor is EnemyOperator opponent && opponent.CurrentHealth < opponent.MaxHealth;
    }

    private float DemolitionActorHealthForDiagnostics(int actorId)
    {
        var actor = DemolitionActorForDiagnostics(actorId);
        return actor switch
        {
            TacticalPlayer player => player.Health,
            SquadMate mate => mate.Health,
            EnemyOperator opponent => opponent.CurrentHealth,
            _ => -1.0f
        };
    }

    private async void ValidateDemolitionNetworkJoinRejection(bool mapMismatch)
    {
        var endpoint = ResolveNetworkDiagnosticEndpoint(OS.GetCmdlineUserArgs());
        OnDemolitionDeploymentRequested(
            (int)OperatorRole.Medic,
            (int)WeaponPlatform.M4A1,
            1,
            (int)WeaponPlatform.P226,
            mapMismatch ? DemolitionMapCatalog.HarborLocksId : DemolitionMapCatalog.TideforgeId,
            (int)SquadSessionMode.Join,
            endpoint,
            (int)DemolitionNetworkTeam.Bravo);
        var observedConnection = false;
        var deadline = Time.GetTicksMsec() + 8000;
        while (Time.GetTicksMsec() < deadline)
        {
            observedConnection |= _squadNetwork.IsOnline
                || _demolitionLobbyDeployment is not null
                || _hud.IsDemolitionNetworkLobbyWaiting;
            if (observedConnection
                && !_squadNetwork.IsOnline
                && !_demolitionJoinPending
                && _demolitionLobbyDeployment is null)
            {
                break;
            }
            await ToSignal(GetTree().CreateTimer(0.1, true), SceneTreeTimer.SignalName.Timeout);
        }
        var valid = observedConnection
            && _demolitionJoinRejectionCode == (mapMismatch ? -2 : -3)
            && !_squadNetwork.IsOnline
            && !_demolitionJoinPending
            && _demolitionLobbyDeployment is null
            && !_demolitionMode
            && !_squadDeployed
            && _squadNetwork.IsLanRoomBrowsingRequested;
        var kind = mapMismatch ? "map_mismatch" : "late_join";
        GD.Print($"DEMOLITION_NETWORK_REJECTION_CHECK kind={kind} valid={valid} observed_connection={observedConnection} rejection={_demolitionJoinRejectionCode} online={_squadNetwork.IsOnline} pending={_demolitionJoinPending} lobby={_demolitionLobbyDeployment is not null} deployed={_squadDeployed} browsing={_squadNetwork.IsLanRoomBrowsingRequested}");
        GD.Print($"DEMOLITION_NETWORK_REJECTION_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
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
        var endpoint = ResolveNetworkDiagnosticEndpoint(OS.GetCmdlineUserArgs());
        void RequestDeployment()
            => OnDemolitionDeploymentRequested(
                (int)(host ? OperatorRole.Assault : OperatorRole.Medic),
                (int)WeaponPlatform.M4A1,
                1,
                (int)WeaponPlatform.P226,
                DemolitionMapCatalog.TideforgeId,
                (int)mode,
                endpoint,
                (int)team);

        RequestDeployment();

        var lobbyWaitDeadline = Time.GetTicksMsec() + 12000;
        var lobbyObserved = false;
        while (Time.GetTicksMsec() < lobbyWaitDeadline)
        {
            lobbyObserved |= _hud.IsDemolitionNetworkLobbyWaiting && !_demolitionMode;
            if (host && _squadNetwork.RegisteredDemolitionPlayerCount >= 2)
            {
                await ToSignal(GetTree().CreateTimer(0.6, true), SceneTreeTimer.SignalName.Timeout);
                break;
            }
            if (!host && _squadNetwork.DemolitionMatchStarted)
            {
                break;
            }
            await ToSignal(GetTree().CreateTimer(0.1, true), SceneTreeTimer.SignalName.Timeout);
        }

        var hostStillInLobby = host
            && !_demolitionMode
            && _hud.IsDemolitionNetworkLobbyWaiting
            && _squadNetwork.RegisteredDemolitionPlayerCount >= 2;
        if (hostStillInLobby)
        {
            RequestDeployment();
        }

        var deploymentDeadline = Time.GetTicksMsec() + 10000;
        while (Time.GetTicksMsec() < deploymentDeadline
            && (!_demolitionMode || !_squadDeployed))
        {
            await ToSignal(GetTree().CreateTimer(0.1, true), SceneTreeTimer.SignalName.Timeout);
        }

        var assigned = host
            ? _demolitionLocalNetworkTeam == DemolitionNetworkTeam.Alpha
                && _demolitionLocalNetworkSlot == 0
            : _demolitionLocalNetworkTeam == clientTeam
                && _demolitionLocalNetworkSlot == clientSlot;
        var deployedTogether = _demolitionMode && _squadDeployed
            && _squadNetwork.DemolitionMatchStarted;

        var buyDeadline = Time.GetTicksMsec() + 9000;
        while (Time.GetTicksMsec() < buyDeadline
            && (!_demolitionBuyPhaseActive || _demolitionNetworkPhase != DemolitionNetworkPhase.Buy))
        {
            await ToSignal(GetTree().CreateTimer(0.1, true), SceneTreeTimer.SignalName.Timeout);
        }
        var openingBuy = _demolitionBuyPhaseActive
            && _demolitionNetworkPhase == DemolitionNetworkPhase.Buy;
        if (openingBuy)
        {
            OnDemolitionPurchaseRequested(
                DemolitionBuyCatalog.P226Id,
                string.Empty,
                false,
                0,
                0);
        }

        var liveDeadline = Time.GetTicksMsec() + 9000;
        while (Time.GetTicksMsec() < liveDeadline
            && (!_demolitionRoundActive || _demolitionNetworkPhase != DemolitionNetworkPhase.Live))
        {
            await ToSignal(GetTree().CreateTimer(0.1, true), SceneTreeTimer.SignalName.Timeout);
        }
        var openingLive = _demolitionRoundActive
            && _demolitionNetworkPhase == DemolitionNetworkPhase.Live;
        var openingEconomyIndependent = _demolitionPlayerEconomy.Funds
                == DemolitionEconomy.StartingFunds
                    - DemolitionBuyCatalog.Sidearm(DemolitionBuyCatalog.P226Id)!.Price
            && (!host
                || _demolitionRemoteEconomies.Count == _squadNetwork.RegisteredDemolitionPlayerCount - 1
                    && _demolitionRemoteEconomies.Values.All(economy =>
                        economy.Funds == _demolitionPlayerEconomy.Funds));

        var clientRepresentationDeadline = Time.GetTicksMsec() + 4000;
        while (Time.GetTicksMsec() < clientRepresentationDeadline
            && !IsInstanceValid(DemolitionActorForDiagnostics(clientActorId)))
        {
            await ToSignal(GetTree().CreateTimer(0.1, true), SceneTreeTimer.SignalName.Timeout);
        }
        var clientActor = DemolitionActorForDiagnostics(clientActorId);
        var targetActor = DemolitionActorForDiagnostics(clientShotTargetId);
        var clientAuthorityProtected = true;
        if (!host && targetActor is EnemyOperator targetProxy)
        {
            var targetHealth = targetProxy.CurrentHealth;
            targetProxy.TakeDamage(
                18.0f,
                targetProxy.GlobalPosition + Vector3.Up * 0.9f,
                _player);
            var playerHealth = _player.Health;
            _player.TakeDamage(
                18.0f,
                _player.GlobalPosition + Vector3.Up * 0.9f,
                targetProxy);
            clientAuthorityProtected = Mathf.IsEqualApprox(targetProxy.CurrentHealth, targetHealth)
                && Mathf.IsEqualApprox(_player.Health, playerHealth);
        }
        var clientRoundAuthorityProtected = true;
        if (!host && openingLive)
        {
            var roundBeforeClientFinish = _demolitionMatch.CurrentRound;
            var alphaScoreBeforeClientFinish = _demolitionMatch.PlayerScore;
            var bravoScoreBeforeClientFinish = _demolitionMatch.OpponentScore;
            FinishDemolitionRound(false, "CLIENT AUTHORITY PROBE");
            clientRoundAuthorityProtected = _demolitionRoundActive
                && _demolitionNetworkPhase == DemolitionNetworkPhase.Live
                && _demolitionMatch.CurrentRound == roundBeforeClientFinish
                && _demolitionMatch.PlayerScore == alphaScoreBeforeClientFinish
                && _demolitionMatch.OpponentScore == bravoScoreBeforeClientFinish;
        }
        var damageRelayed = false;
        if (host && openingLive)
        {
            if (IsInstanceValid(clientActor))
            {
                var end = clientActor!.GlobalPosition + Vector3.Up * 0.9f;
                ApplyDemolitionNetworkDamage(
                    clientActorId,
                    18.0f,
                    end,
                    _player);
                _squadNetwork.BroadcastShot(
                    _player.GlobalPosition + Vector3.Up,
                    end,
                    clientActorId,
                    18.0f);
            }
        }
        else if (IsInstanceValid(targetActor))
        {
            var origin = _player.GlobalPosition + Vector3.Up * 0.9f;
            var end = targetActor!.GlobalPosition + Vector3.Up * 0.9f;
            _squadNetwork.BroadcastShot(origin, end, clientShotTargetId, 18.0f);
        }
        await ToSignal(GetTree().CreateTimer(1.0, true), SceneTreeTimer.SignalName.Timeout);
        damageRelayed = host
            ? IsDemolitionActorDamagedForDiagnostics(clientActorId)
                && IsDemolitionActorDamagedForDiagnostics(clientShotTargetId)
            : _player.Health < _player.MaxHealth;
        var damagedClientHealth = DemolitionActorHealthForDiagnostics(clientActorId);
        if (!host && openingLive)
        {
            _squadNetwork.BroadcastAbility(
                OperatorRole.Medic,
                _player.GlobalPosition + Vector3.Up * 0.9f,
                Vector3.Up);
        }
        await ToSignal(GetTree().CreateTimer(1.3, true), SceneTreeTimer.SignalName.Timeout);
        var medicAbilitySynchronized = damagedClientHealth >= 0.0f
            && DemolitionActorHealthForDiagnostics(clientActorId) > damagedClientHealth + 0.5f;

        var objectiveSynchronized = false;
        if (host && openingLive)
        {
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
        else if (!host && openingLive && clientTeam == DemolitionNetworkTeam.Alpha)
        {
            var carrierDeadline = Time.GetTicksMsec() + 4000;
            while (_networkDeviceCarrierActorId != LocalDemolitionActorId
                && Time.GetTicksMsec() < carrierDeadline)
            {
                await ToSignal(GetTree().CreateTimer(0.1, true), SceneTreeTimer.SignalName.Timeout);
            }
            _player.GlobalPosition = DemolitionLayout().SitePositions[0];
            await ToSignal(GetTree().CreateTimer(0.5, true), SceneTreeTimer.SignalName.Timeout);
            _squadNetwork.RequestDemolitionAction(DemolitionNetworkAction.Plant, 0);
        }
        else if (!host && openingLive)
        {
            var plantDeadline = Time.GetTicksMsec() + 4000;
            while ((!_demolitionDevicePlanted || _demolitionActiveSite < 0)
                && Time.GetTicksMsec() < plantDeadline)
            {
                await ToSignal(GetTree().CreateTimer(0.1, true), SceneTreeTimer.SignalName.Timeout);
            }
            if (_demolitionDevicePlanted && _demolitionActiveSite >= 0)
            {
                _player.GlobalPosition = DemolitionLayout().SitePositions[_demolitionActiveSite];
                await ToSignal(GetTree().CreateTimer(0.5, true), SceneTreeTimer.SignalName.Timeout);
                _squadNetwork.RequestDemolitionAction(
                    DemolitionNetworkAction.Defuse,
                    _demolitionActiveSite);
            }
        }

        await ToSignal(GetTree().CreateTimer(1.5, true), SceneTreeTimer.SignalName.Timeout);
        objectiveSynchronized = clientTeam == DemolitionNetworkTeam.Alpha
            ? _demolitionDevicePlanted && _demolitionActiveSite == 0
            : _demolitionMatch.OpponentScore >= 1;

        if (host && _demolitionRoundActive)
        {
            FinishDemolitionRound(
                clientTeam == DemolitionNetworkTeam.Bravo,
                "NETWORK DIAGNOSTIC ROUND COMPLETE");
        }

        var intermissionDeadline = Time.GetTicksMsec() + 7000;
        while (Time.GetTicksMsec() < intermissionDeadline
            && _demolitionNetworkPhase != DemolitionNetworkPhase.Intermission)
        {
            await ToSignal(GetTree().CreateTimer(0.1, true), SceneTreeTimer.SignalName.Timeout);
        }
        var intermissionObserved = _demolitionNetworkPhase == DemolitionNetworkPhase.Intermission;
        if (host)
        {
            _demolitionIntermissionRemaining = Mathf.Min(_demolitionIntermissionRemaining, 1.0f);
        }

        var secondBuyDeadline = Time.GetTicksMsec() + 7000;
        while (Time.GetTicksMsec() < secondBuyDeadline
            && _demolitionNetworkPhase != DemolitionNetworkPhase.Buy)
        {
            await ToSignal(GetTree().CreateTimer(0.1, true), SceneTreeTimer.SignalName.Timeout);
        }
        var secondBuy = _demolitionNetworkPhase == DemolitionNetworkPhase.Buy
            && _demolitionMatch.CurrentRound >= 2;
        if (secondBuy)
        {
            OnDemolitionPurchaseRequested(
                DemolitionBuyCatalog.P226Id,
                string.Empty,
                false,
                0,
                0);
        }

        var secondLiveDeadline = Time.GetTicksMsec() + 7000;
        while (Time.GetTicksMsec() < secondLiveDeadline
            && _demolitionNetworkPhase != DemolitionNetworkPhase.Live)
        {
            await ToSignal(GetTree().CreateTimer(0.1, true), SceneTreeTimer.SignalName.Timeout);
        }
        var secondLive = _demolitionNetworkPhase == DemolitionNetworkPhase.Live
            && _demolitionMatch.CurrentRound >= 2
            && _demolitionRoundActive;
        var nextRoundFundsAdvanced = _demolitionPlayerEconomy.Funds
                > DemolitionEconomy.StartingFunds
                    - DemolitionBuyCatalog.Sidearm(DemolitionBuyCatalog.P226Id)!.Price
            && (!host || _demolitionRemoteEconomies.Values.All(economy =>
                economy.Funds > DemolitionEconomy.StartingFunds
                    - DemolitionBuyCatalog.Sidearm(DemolitionBuyCatalog.P226Id)!.Price));
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
        var connectedBeforeDisconnect = _squadNetwork.ConnectedPeerCount == 1;
        var valid = _squadNetwork.IsOnline
            && connectedBeforeDisconnect
            && lobbyObserved
            && (!host || hostStillInLobby)
            && assigned
            && deployedTogether
            && openingBuy
            && openingLive
            && openingEconomyIndependent
            && intermissionObserved
            && secondBuy
            && secondLive
            && nextRoundFundsAdvanced
            && teamPopulation
            && remoteRepresentation
            && clientAuthorityProtected
            && clientRoundAuthorityProtected
            && damageRelayed
            && medicAbilitySynchronized
            && objectiveSynchronized;
        if (!host)
        {
            GD.Print($"DEMOLITION_NETWORK_CHECK mode=client requested_team={clientTeam} online={_squadNetwork.IsOnline} peers={_squadNetwork.ConnectedPeerCount} team={_demolitionLocalNetworkTeam} slot={_demolitionLocalNetworkSlot} humans={DemolitionNetworkHumanCount} friendly_humans={DemolitionNetworkFriendlyHumanCount} opponent_humans={DemolitionNetworkOpponentHumanCount} lobby_observed={lobbyObserved} assigned={assigned} deployed_together={deployedTogether} opening_buy={openingBuy} opening_live={openingLive} independent_funds={openingEconomyIndependent} intermission={intermissionObserved} second_buy={secondBuy} second_live={secondLive} next_round_funds={nextRoundFundsAdvanced} funds={_demolitionPlayerEconomy.Funds} remote_representation={remoteRepresentation} client_authority={clientAuthorityProtected} round_authority={clientRoundAuthorityProtected} damage={damageRelayed} medic_sync={medicAbilitySynchronized} objective={objectiveSynchronized} score={_demolitionMatch.PlayerScore}:{_demolitionMatch.OpponentScore} phase={_demolitionNetworkPhase} round={_demolitionMatch.CurrentRound}");
            GD.Print($"DEMOLITION_NETWORK_PASS valid={valid}");
            await ToSignal(GetTree().CreateTimer(0.5, true), SceneTreeTimer.SignalName.Timeout);
            GetTree().Quit(valid ? 0 : 2);
            return;
        }

        var disconnectDeadline = Time.GetTicksMsec() + 6000;
        while (_squadNetwork.ConnectedPeerCount > 0
            && Time.GetTicksMsec() < disconnectDeadline)
        {
            await ToSignal(GetTree().CreateTimer(0.1, true), SceneTreeTimer.SignalName.Timeout);
        }
        var aiReplacement = clientTeam == DemolitionNetworkTeam.Alpha
            ? _squadMates.Any(mate => IsInstanceValid(mate)
                && mate.SquadSlot == clientSlot
                && !mate.IsHumanProxy)
            : _demolitionOpponents.Any(opponent => IsInstanceValid(opponent)
                && opponent.NetworkId == clientActorId
                && !opponent.IsHumanProxy);
        var disconnectRecovered = _squadNetwork.ConnectedPeerCount == 0
            && DemolitionNetworkHumanCount == 1
            && aiReplacement;
        valid &= disconnectRecovered;
        GD.Print($"DEMOLITION_NETWORK_CHECK mode=host requested_team={clientTeam} online={_squadNetwork.IsOnline} registered={_squadNetwork.RegisteredDemolitionPlayerCount} lobby_players={_hud.DemolitionNetworkLobbyPlayerCount} lobby_can_start={_hud.DemolitionNetworkLobbyCanStart} peers_before={(connectedBeforeDisconnect ? 1 : 0)} peers_after={_squadNetwork.ConnectedPeerCount} team={_demolitionLocalNetworkTeam} slot={_demolitionLocalNetworkSlot} humans={DemolitionNetworkHumanCount} lobby_observed={lobbyObserved} lobby_held={hostStillInLobby} assigned={assigned} deployed_together={deployedTogether} opening_buy={openingBuy} opening_live={openingLive} independent_funds={openingEconomyIndependent} intermission={intermissionObserved} second_buy={secondBuy} second_live={secondLive} next_round_funds={nextRoundFundsAdvanced} funds={_demolitionPlayerEconomy.Funds} remote_representation={remoteRepresentation} damage={damageRelayed} medic_sync={medicAbilitySynchronized} objective={objectiveSynchronized} disconnect_recovered={disconnectRecovered} ai_replacement={aiReplacement} score={_demolitionMatch.PlayerScore}:{_demolitionMatch.OpponentScore} phase={_demolitionNetworkPhase} round={_demolitionMatch.CurrentRound} action_received={_demolitionNetworkActionReceivedForDiagnostics} action_applied={_demolitionNetworkActionAppliedForDiagnostics} action_distance={_demolitionNetworkActionDistanceForDiagnostics:0.00}");
        GD.Print($"DEMOLITION_NETWORK_PASS valid={valid}");
        await ToSignal(GetTree().CreateTimer(0.5, true), SceneTreeTimer.SignalName.Timeout);
        GetTree().Quit(valid ? 0 : 2);
    }
}
