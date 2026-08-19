using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private const long ExtractionNetworkDiagnosticSeed = 78045123L;
    private const string ExtractionNetworkDiagnosticItemId = "extraction-network-diagnostic-item";

    private static ulong _extractionNetworkDiagnosticRuntimeId;
    private static bool _extractionNetworkDiagnosticLobbyWaited;
    private static bool _extractionNetworkDiagnosticMutationApplied;

    private async void ValidateExtractionNetworkSession(bool host)
    {
        if (!_squadNetwork.ExtractionMatchStarted || DeploymentMapRuntime.CurrentWorldSeed == 0)
        {
            await ValidateExtractionNetworkLobby(host);
            return;
        }

        await ValidateExtractionNetworkWorld(host);
    }

    private async Task ValidateExtractionNetworkLobby(bool host)
    {
        _extractionNetworkDiagnosticRuntimeId = _squadNetwork.GetInstanceId();
        _extractionNetworkDiagnosticLobbyWaited = false;
        _extractionNetworkDiagnosticMutationApplied = false;

        var endpoint = ResolveNetworkDiagnosticEndpoint(OS.GetCmdlineUserArgs());
        _hud.ShowSquadLobby(host
            ? "EXTRACTION NETWORK HOST VALIDATION"
            : "EXTRACTION NETWORK CLIENT VALIDATION");
        _hud.SetDeploymentMapSelection(DeploymentMapCatalog.FreightTerminalId);
        OnSquadDeploymentRequested(
            (int)(host ? OperatorRole.Assault : OperatorRole.Recon),
            (int)(host ? SquadSessionMode.Host : SquadSessionMode.Join),
            endpoint);

        var deadline = Time.GetTicksMsec() + 25000;
        if (host)
        {
            while (Time.GetTicksMsec() < deadline
                && _squadNetwork.RegisteredExtractionPlayerCount < 2)
            {
                await ToSignal(GetTree().CreateTimer(0.1f), SceneTreeTimer.SignalName.Timeout);
            }
            if (!GodotObject.IsInstanceValid(this))
            {
                return;
            }

            _extractionNetworkDiagnosticLobbyWaited =
                _squadNetwork.RegisteredExtractionPlayerCount >= 2
                && _networkLobbyDeployment is not null
                && !_squadDeployed
                && !_squadNetwork.ExtractionMatchStarted;
            await ToSignal(GetTree().CreateTimer(0.45f), SceneTreeTimer.SignalName.Timeout);
            if (!GodotObject.IsInstanceValid(this))
            {
                return;
            }
            _extractionNetworkDiagnosticLobbyWaited &= !_squadDeployed
                && !_squadNetwork.ExtractionMatchStarted;
            if (!_extractionNetworkDiagnosticLobbyWaited
                || !_squadNetwork.TryStartExtractionMatch(ExtractionNetworkDiagnosticSeed))
            {
                FailExtractionNetworkDiagnostic(host, "host_lobby_start_failed");
            }
            return;
        }

        while (Time.GetTicksMsec() < deadline
            && (!_squadNetwork.IsOnline
                || _squadNetwork.LocalExtractionSlot <= 0
                || _networkLobbyDeployment is null))
        {
            await ToSignal(GetTree().CreateTimer(0.1f), SceneTreeTimer.SignalName.Timeout);
        }
        if (!GodotObject.IsInstanceValid(this))
        {
            return;
        }
        _extractionNetworkDiagnosticLobbyWaited = _squadNetwork.IsOnline
            && _squadNetwork.LocalExtractionSlot == 1
            && _networkLobbyDeployment is not null
            && !_squadDeployed
            && !_squadNetwork.ExtractionMatchStarted;
        if (!_extractionNetworkDiagnosticLobbyWaited)
        {
            FailExtractionNetworkDiagnostic(host, "client_lobby_wait_failed");
            return;
        }

        while (Time.GetTicksMsec() < deadline && !_squadNetwork.ExtractionMatchStarted)
        {
            await ToSignal(GetTree().CreateTimer(0.1f), SceneTreeTimer.SignalName.Timeout);
        }
        if (GodotObject.IsInstanceValid(this) && !_squadNetwork.ExtractionMatchStarted)
        {
            FailExtractionNetworkDiagnostic(host, "client_start_timeout");
        }
    }

    private async Task ValidateExtractionNetworkWorld(bool host)
    {
        var deadline = Time.GetTicksMsec() + 30000;
        while (Time.GetTicksMsec() < deadline && !_squadDeployed)
        {
            await ToSignal(GetTree().CreateTimer(0.1f), SceneTreeTimer.SignalName.Timeout);
        }
        if (!GodotObject.IsInstanceValid(this) || !_squadDeployed)
        {
            if (GodotObject.IsInstanceValid(this))
            {
                FailExtractionNetworkDiagnostic(host, "world_deployment_timeout");
            }
            return;
        }

        var clientPosition = DeploymentPoint + new Vector3(4.0f, 0.1f, -4.0f);
        if (!host)
        {
            _player.GlobalPosition = clientPosition;
            _player.Velocity = Vector3.Zero;
            _player.SetPhysicsProcess(false);
        }

        if (host)
        {
            while (Time.GetTicksMsec() < deadline
                && (_squadNetwork.ExtractionWorldReadyPlayerCount < 2
                    || !TryFindExtractionDiagnosticRemote(clientPosition, out _)))
            {
                await ToSignal(GetTree().CreateTimer(0.1f), SceneTreeTimer.SignalName.Timeout);
            }
            if (!TryApplyExtractionDiagnosticAuthorityMutation(clientPosition))
            {
                FailExtractionNetworkDiagnostic(host, "host_authority_mutation_failed");
                return;
            }
        }

        while (Time.GetTicksMsec() < deadline && !ExtractionNetworkDiagnosticStateReady(host))
        {
            await ToSignal(GetTree().CreateTimer(0.1f), SceneTreeTimer.SignalName.Timeout);
        }

        var valid = ExtractionNetworkDiagnosticStateReady(host);
        var rootNetwork = GetTree().Root.GetNodeOrNull<SquadNetwork>("SquadNetworkRuntime");
        var persistentRuntime = ReferenceEquals(rootNetwork, _squadNetwork)
            && _squadNetwork.GetInstanceId() == _extractionNetworkDiagnosticRuntimeId
            && GetTree().Root.GetChildren().OfType<SquadNetwork>().Count() == 1;
        valid &= persistentRuntime
            && _extractionNetworkDiagnosticLobbyWaited
            && _extractionWorldLaunchWaitObserved
            && _extractionWorldLaunchPauseObserved
            && !_extractionWorldLaunchPending
            && _squadNetwork.ExtractionWorldLaunchStarted
            && (host || _squadNetwork.ExtractionWorldBootstrapReceived)
            && DeploymentMapRuntime.CurrentWorldSeed == ExtractionNetworkDiagnosticSeed
            && _squadNetwork.ExtractionWorldSeed == ExtractionNetworkDiagnosticSeed
            && _squadNetwork.ExtractionMatchStarted
            && _squadNetwork.ConnectedPeerCount == 1
            && (!host || _squadNetwork.LastExtractionWorldChunkCount > 1);

        GD.Print($"EXTRACTION_NETWORK_CHECK mode={(host ? "host" : "client")} valid={valid} lobby_waited={_extractionNetworkDiagnosticLobbyWaited} launch_waited={_extractionWorldLaunchWaitObserved} launch_paused={_extractionWorldLaunchPauseObserved} bootstrap={_squadNetwork.ExtractionWorldBootstrapReceived} launch_started={_squadNetwork.ExtractionWorldLaunchStarted} launch_pending={_extractionWorldLaunchPending} persistent={persistentRuntime} online={_squadNetwork.IsOnline} peers={_squadNetwork.ConnectedPeerCount} slot={_extractionLocalSquadSlot} seed={DeploymentMapRuntime.CurrentWorldSeed} sequence={_lastExtractionWorldSequence} chunks={_squadNetwork.LastExtractionWorldChunkCount} ready_peers={_squadNetwork.ExtractionWorldReadyPlayerCount} enemies={_extractionNetworkEnemies.Count} squad={ActiveSquadCount} ai={AiSquadCount} objective={_objectiveStage} loot_marker={HasExtractionDiagnosticLootItem()} mutation={_extractionNetworkDiagnosticMutationApplied}");
        GD.Print($"EXTRACTION_NETWORK_PASS valid={valid}");
        if (host)
        {
            await ToSignal(GetTree().CreateTimer(4.0f), SceneTreeTimer.SignalName.Timeout);
        }
        GetTree().Quit(valid ? 0 : 2);
    }

    private bool TryApplyExtractionDiagnosticAuthorityMutation(Vector3 clientPosition)
    {
        if (_extractionNetworkDiagnosticMutationApplied)
        {
            return true;
        }
        if (!_squadNetwork.IsHost
            || _squadNetwork.ExtractionWorldReadyPlayerCount < 2
            || !TryFindExtractionDiagnosticRemote(clientPosition, out var remote))
        {
            return false;
        }

        var enemy = _extractionNetworkEnemies.Values
            .Where(candidate => IsInstanceValid(candidate) && !candidate.IsDead)
            .OrderBy(candidate => candidate.NetworkId)
            .FirstOrDefault();
        var ai = _squadMates.FirstOrDefault(candidate => IsInstanceValid(candidate)
            && candidate.SquadSlot == 2
            && !candidate.IsHumanProxy);
        var loot = _extractionLootSources.OrderBy(pair => pair.Key).FirstOrDefault();
        if (enemy is null || ai is null || loot.Value is null
            || !IsInstanceValid(loot.Value.LootNode))
        {
            return false;
        }

        _player.GlobalPosition = DeploymentPoint + new Vector3(-4.0f, 0.1f, -4.0f);
        _player.Velocity = Vector3.Zero;
        _player.SetPhysicsProcess(false);
        ai.GlobalPosition = DeploymentPoint + new Vector3(0.0f, 0.1f, -7.0f);
        ai.Velocity = Vector3.Zero;
        ai.SetPhysicsProcess(false);
        enemy.TakeDamage(18.0f, enemy.GlobalPosition + Vector3.Up, _player);
        remote.TakeCombatDamage(18.0f, remote.GlobalPosition + Vector3.Up, null);
        CompleteCurrentObjective();
        loot.Value.Loot.Clear();
        loot.Value.Loot.Add(new LootItem
        {
            Id = ExtractionNetworkDiagnosticItemId,
            Kind = LootItemKind.ArmorPlate,
            Grade = LootGrade.Rare,
            Quantity = 1
        });
        loot.Value.OnSearched();
        _squadNetwork.BroadcastExtractionLootState(
            CaptureExtractionLootSourceState(loot.Key, loot.Value, granted: false));
        BroadcastExtractionWorldSnapshot();
        _squadNetwork.BroadcastExtractionMissionState(CaptureExtractionMissionState());
        _extractionNetworkDiagnosticMutationApplied = true;
        return true;
    }

    private bool ExtractionNetworkDiagnosticStateReady(bool host)
    {
        var enemy = _extractionNetworkEnemies.Values
            .Where(candidate => IsInstanceValid(candidate))
            .OrderBy(candidate => candidate.NetworkId)
            .FirstOrDefault();
        var ai = _squadMates.FirstOrDefault(candidate => IsInstanceValid(candidate)
            && candidate.SquadSlot == 2);
        var expectedAiPosition = DeploymentPoint + new Vector3(0.0f, 0.1f, -7.0f);
        var expectedHostPosition = DeploymentPoint + new Vector3(-4.0f, 0.1f, -4.0f);
        var enemyAuthority = enemy is not null && enemy.CurrentHealth < enemy.MaxHealth;
        var aiAuthority = ai is not null
            && !ai.IsHumanProxy
            && ai.GlobalPosition.DistanceTo(expectedAiPosition) < 0.75f;
        var missionAuthority = _objectiveStage >= 1;
        var lootAuthority = HasExtractionDiagnosticLootItem();
        if (host)
        {
            return _extractionNetworkDiagnosticMutationApplied
                && enemyAuthority
                && aiAuthority
                && missionAuthority
                && lootAuthority
                && TryFindExtractionDiagnosticRemote(
                    DeploymentPoint + new Vector3(4.0f, 0.1f, -4.0f),
                    out var remote)
                && remote.Health < remote.MaxHealth;
        }

        var hostProxy = _squadMates.FirstOrDefault(candidate => IsInstanceValid(candidate)
            && candidate.SquadSlot == 0
            && candidate.IsHumanProxy);
        var proxiesAuthoritative = _extractionNetworkEnemies.Values
            .Where(IsInstanceValid)
            .All(candidate => candidate.IsNetworkProxy && !candidate.IsPhysicsProcessing());
        return _lastExtractionWorldSequence >= 0
            && _player.Health < _player.MaxHealth
            && enemyAuthority
            && aiAuthority
            && missionAuthority
            && lootAuthority
            && hostProxy is not null
            && hostProxy.GlobalPosition.DistanceTo(expectedHostPosition) < 0.75f
            && proxiesAuthoritative
            && _missionDirector.ProcessMode == ProcessModeEnum.Disabled;
    }

    private bool TryFindExtractionDiagnosticRemote(Vector3 expectedPosition, out SquadMate remote)
    {
        remote = _squadMates.FirstOrDefault(candidate => IsInstanceValid(candidate)
            && candidate.IsHumanProxy
            && candidate.NetworkPeerId != 1)!;
        return remote is not null && remote.GlobalPosition.DistanceTo(expectedPosition) < 0.75f;
    }

    private bool HasExtractionDiagnosticLootItem()
        => _extractionLootSources.Values.Any(source => IsInstanceValid(source.LootNode)
            && source.Loot.Any(item => item.Id == ExtractionNetworkDiagnosticItemId));

    private void FailExtractionNetworkDiagnostic(bool host, string reason)
    {
        GD.Print($"EXTRACTION_NETWORK_CHECK mode={(host ? "host" : "client")} valid=False reason={reason} online={_squadNetwork.IsOnline} peers={_squadNetwork.ConnectedPeerCount} lobby_waited={_extractionNetworkDiagnosticLobbyWaited} match={_squadNetwork.ExtractionMatchStarted} slot={_squadNetwork.LocalExtractionSlot} runtime={_squadNetwork.GetInstanceId()}");
        GD.Print("EXTRACTION_NETWORK_PASS valid=False");
        GetTree().Quit(2);
    }
}
