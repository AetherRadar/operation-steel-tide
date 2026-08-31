using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private async void ValidateDemolitionRules()
    {
        await WaitFrames(3);
        _demolitionMode = true;
        _demolitionRoundActive = true;
        EnsureDemolitionArenaBuilt();
        _demolitionArena?.SetActive(true);
        DeploySquad(OperatorRole.Recon, SquadSessionMode.Local, "127.0.0.1");
        SpawnDemolitionOpponents();
        foreach (var rulesOpponent in _demolitionOpponents.Where(IsInstanceValid))
        {
            rulesOpponent.ProcessMode = ProcessModeEnum.Disabled;
        }
        _hud.SetDemolitionGameplayPresentation(true);
        _hud.SetLanguage("en");
        _hud.SetDemolitionSmokeGrenades(1);
        _hud.SetStats(100.0f, 0.0f, 100.0f, 0, 0, 2);

        var rosterHidden = _hud.IsDemolitionSquadRosterHidden;
        var skillHudVisible = _hud.IsDemolitionSkillHudVisible;
        var ordersHidden = _hud.AreDemolitionSquadOrdersHidden;
        var utilityHudVisible = _hud.DemolitionUtilityHudText.Contains("5 FRAG", System.StringComparison.Ordinal)
            && _hud.DemolitionUtilityHudText.Contains("6 [SMOKE", System.StringComparison.Ordinal)
            && _hud.DemolitionUtilityHudText.Contains("FIRE", System.StringComparison.Ordinal);
        var demolitionFooterSeparated = _hud.FooterHudRuntimeSeparatedForDiagnostics;
        var hudIsolated = rosterHidden
            && skillHudVisible
            && ordersHidden
            && utilityHudVisible
            && demolitionFooterSeparated;
        var grenadeEvents = InputMap.ActionGetEvents(GameInputActions.WeaponGrenade);
        using var grenadeEventsBacking = grenadeEvents.AsDisposable();
        var utilityEvents = InputMap.ActionGetEvents(GameInputActions.WeaponUtility);
        using var utilityEventsBacking = utilityEvents.AsDisposable();
        var roleRules = DemolitionReconScanRange < 72.0f
            && InputMap.HasAction(GameInputActions.UseClassSkill)
            && InputMap.HasAction(GameInputActions.ThrowGrenade)
            && InputMap.HasAction(GameInputActions.WeaponGrenade)
            && InputMap.HasAction(GameInputActions.WeaponUtility)
            && grenadeEvents.Count > 0
            && utilityEvents.Count > 0;
        var eliminationRules = DemolitionRoundRules.EliminationEndsRound(DemolitionTeam.Attackers, false)
            && DemolitionRoundRules.EliminationEndsRound(DemolitionTeam.Defenders, false)
            && !DemolitionRoundRules.EliminationEndsRound(DemolitionTeam.Attackers, true)
            && DemolitionRoundRules.EliminationEndsRound(DemolitionTeam.Defenders, true);
        var deviceLifecycle = new DemolitionDeviceLifecycle();
        deviceLifecycle.BeginGrounded();
        var playerCanBeSelected = deviceLifecycle.AssignRandomPickupRunner(
            new[] { "player", "mate-a", "mate-b" },
            selectionToken: 0) == "player";
        deviceLifecycle.BeginGrounded();
        var assignedRunner = deviceLifecycle.AssignRandomPickupRunner(
            new[] { "player", "mate-a", "mate-b" },
            selectionToken: 4);
        var wrongPickupRejected = !deviceLifecycle.TryPickup("player");
        var assignedPickupAccepted = deviceLifecycle.TryPickup("mate-a");
        var carriedSnapshot = deviceLifecycle.Capture();
        var dropTransferred = deviceLifecycle.TryDrop("mate-a", "mate-b")
            && deviceLifecycle.TryPickup("mate-b");
        var plantAccepted = deviceLifecycle.TryPlant("mate-b");
        var detonationAccepted = deviceLifecycle.TryDetonate();
        deviceLifecycle.Restore(carriedSnapshot);
        var snapshotRestored = deviceLifecycle.IsCarried
            && deviceLifecycle.CarrierMemberId == "mate-a"
            && deviceLifecycle.PickupRunnerMemberId is null;
        deviceLifecycle.Clear();
        var deviceLifecycleValid = playerCanBeSelected
            && assignedRunner == "mate-a"
            && wrongPickupRejected
            && assignedPickupAccepted
            && dropTransferred
            && plantAccepted
            && detonationAccepted
            && snapshotRestored
            && deviceLifecycle.Phase == DemolitionDevicePhase.Inactive;
        var spectatorLocalized = GameLocalization.Get(
                "demolition_spectating_device",
                "zh",
                "SPECTATING  //  PLANTED DEVICE")
            .Contains("\u5df2\u5b89\u653e", System.StringComparison.Ordinal);
        var eliminatedDeviceSpectatorLocalized = GameLocalization.Get(
                "demolition_squad_eliminated_device_active",
                "zh",
                "SQUAD ELIMINATED  //  DEVICE STILL ACTIVE")
            .Contains("\u88c5\u7f6e\u4ecd\u5728\u8fd0\u884c", System.StringComparison.Ordinal)
            && GameLocalization.Format(
                    "demolition_squad_eliminated_device_objective",
                    "zh",
                    "SQUAD ELIMINATED  //  DEVICE ACTIVE AT {0}  //  {1:00.0}s{2}",
                    "A",
                    12.0f,
                    string.Empty)
                .Contains("A \u70b9\u88c5\u7f6e\u8fd0\u884c\u4e2d", System.StringComparison.Ordinal);

        var reconTargets = _enemies
            .Where(enemy => IsInstanceValid(enemy) && !enemy.IsDead && !enemy.IsScanned)
            .Take(2)
            .ToArray();
        var reconBoundary = reconTargets.Length == 2;
        if (reconBoundary)
        {
            var scanOrigin = new Vector3(188.0f, 1.0f, 188.0f);
            reconTargets[0].GlobalPosition = scanOrigin + Vector3.Right * (DemolitionReconScanRange - 1.0f);
            reconTargets[1].GlobalPosition = scanOrigin + Vector3.Left * (DemolitionReconScanRange + 1.0f);
            PerformReconScan(_player, scanOrigin);
            reconBoundary = reconTargets[0].IsScanned && !reconTargets[1].IsScanned;
        }

        var scoreBefore = _demolitionMatch.PlayerScore;
        _player.TakeDamage(9999.0f, _player.HitPoint(HitRegion.Torso), this);
        var playerEliminationPosition = _player.GlobalPosition;
        var playerColliderDisabledAfterElimination = _player.DemolitionColliderDisabledForDiagnostics;
        var playerEliminated = _player.IsDead
            && _player.ReviveUsed
            && !_player.CanBeRevived
            && _player.CollisionLayer == 0
            && _player.CollisionMask == 0
            && playerColliderDisabledAfterElimination
            && _localPlayerEliminated
            && !_localPlayerDowned
            && !_hud.IsDownedBannerVisible
            && !_player.TryReceiveRevive(50.0f)
            && _demolitionRoundActive
            && _demolitionMatch.PlayerScore == scoreBefore;

        var mate = _squadMates.FirstOrDefault(candidate => IsInstanceValid(candidate) && !candidate.IsDowned);
        var mateEliminationPosition = Vector3.Zero;
        var mateEliminated = false;
        var mateWasRunningBeforeElimination = false;
        if (mate is not null)
        {
            mate.Velocity = Vector3.Right * 4.0f;
            mate.SetAuthoredMovementPoseForDiagnostics(4.0f);
            mateWasRunningBeforeElimination = mate.UsesAuthoredOperatorForDiagnostics
                && mate.AuthoredAnimationForDiagnostics.Contains(
                    "run",
                    System.StringComparison.Ordinal);
            mate.SetHoldFire(true);
            mate.TakeCombatDamage(9999.0f, mate.HitPoint(HitRegion.Torso), this);
            mateEliminationPosition = mate.GlobalPosition;
            var mateCollisionShapesDisabledAfterElimination =
                mate.AreDemolitionCollisionShapesDisabledForDiagnostics;
            mateEliminated = mateWasRunningBeforeElimination
                && mate.IsDowned
                && mate.ReviveUsed
                && !mate.CanBeRevived
                && mate.CollisionLayer == 0
                && mate.CollisionMask == 0
                && mateCollisionShapesDisabledAfterElimination
                && mate.IsDemolitionEliminatedPoseForDiagnostics
                && mate.DemolitionNameplateShowsEliminatedForDiagnostics
                && !mate.TryReceiveRevive(50.0f)
                && !mate.IsBodyBag;
        }
        await WaitFrames(6);
        var playerFrozenAfterElimination = !_player.IsPhysicsProcessing()
            && _player.GlobalPosition.DistanceTo(playerEliminationPosition) <= 0.01f
            && _player.Velocity.LengthSquared() <= 0.0001f
            && _player.DemolitionColliderDisabledForDiagnostics;
        var mateFrozenAfterElimination = mate is not null
            && !mate.IsPhysicsProcessing()
            && mate.GlobalPosition.DistanceTo(mateEliminationPosition) <= 0.01f
            && mate.Velocity.LengthSquared() <= 0.0001f
            && mate.AreDemolitionCollisionShapesDisabledForDiagnostics
            && mate.IsDemolitionEliminatedPoseForDiagnostics;
        var playerEliminationCollision = $"{_player.CollisionLayer}/{_player.CollisionMask}";
        var playerColliderDisabled = _player.DemolitionColliderDisabledForDiagnostics;
        var mateEliminationCollision = mate is null
            ? "missing"
            : $"{mate.CollisionLayer}/{mate.CollisionMask}";
        var mateCollisionShapesDisabled = mate is not null
            && mate.AreDemolitionCollisionShapesDisabledForDiagnostics;

        var resetQuote = DemolitionBuyCatalog.Quote(DemolitionPurchaseSelection.Empty, 0);
        var resetLoadout = DemolitionBuyCatalog.BuildLoadout(resetQuote);
        _localPlayerDowned = false;
        _localPlayerEliminated = false;
        _player.ResetForDemolitionRound(
            playerEliminationPosition,
            _player.Role,
            resetLoadout,
            grenadeCount: 0,
            smokeGrenadeCount: 0);
        mate?.ResetForDemolitionRound(mateEliminationPosition);
        var playerRestoredForNextRound = _player.IsPhysicsProcessing()
            && !_player.IsDead
            && _player.CollisionLayer == 1
            && _player.CollisionMask
                == (1u | 2u | BreakableGlassField.MovementCollisionLayer)
            && !_player.DemolitionColliderDisabledForDiagnostics
            && _player.Velocity.LengthSquared() <= 0.0001f;
        var mateRestoredForNextRound = mate is not null
            && mate.IsPhysicsProcessing()
            && !mate.IsDowned
            && !mate.ReviveUsed
            && !mate.HoldFireActive
            && mate.CollisionLayer == 4
            && mate.CollisionMask
                == (1u | BreakableGlassField.MovementCollisionLayer)
            && mate.AreDemolitionCollisionShapesEnabledForDiagnostics
            && mate.Velocity.LengthSquared() <= 0.0001f;

        // Exercise the real planted-device branch from living actors through permanent
        // elimination, spectator hand-off, and the persistent objective HUD. Attackers
        // remain out of the round while the already-planted device continues to resolve.
        var plantedWipeScore = _demolitionMatch.PlayerScore;
        var plantedWipeStarted = ForceDemolitionDeviceCarrierForDiagnostics(_player);
        PlantDemolitionDevice(0, byPlayerTeam: true, _player);
        plantedWipeStarted &= _demolitionDevicePlanted && _demolitionActiveSite == 0;
        var plantedWipeMates = _squadMates
            .Where(candidate => IsInstanceValid(candidate))
            .ToArray();
        foreach (var plantedMate in plantedWipeMates)
        {
            if (!plantedMate.IsDowned && !plantedMate.IsBodyBag)
            {
                plantedMate.TakeCombatDamage(
                    9999.0f,
                    plantedMate.HitPoint(HitRegion.Torso),
                    this);
            }
        }
        _player.SetHealthForDiagnostics(_player.MaxHealth);
        _player.TakeDamage(9999.0f, _player.HitPoint(HitRegion.Torso), this);
        var plantedWipeFuseBefore = _demolitionRemaining;
        UpdateDemolitionRound(0.1f);
        var expectedPlantedWipeObjective = GameLocalization.Format(
            "demolition_squad_eliminated_device_objective",
            _languageSetting,
            "SQUAD ELIMINATED  //  DEVICE ACTIVE AT {0}  //  {1:00.0}s{2}",
            "A",
            _demolitionRemaining,
            string.Empty);
        var plantedWipeDeviceContinues = plantedWipeStarted
            && plantedWipeMates.Length > 0
            && plantedWipeMates.All(candidate => candidate.IsDowned && candidate.ReviveUsed)
            && IsLocalDemolitionSquadEliminated()
            && _player.IsDead
            && _player.ReviveUsed
            && _demolitionRoundActive
            && _demolitionDevicePlanted
            && _demolitionMatch.PlayerScore == plantedWipeScore
            && _demolitionObjectiveSpectatorActive
            && _demolitionRemaining < plantedWipeFuseBefore
            && _hud.DemolitionRadioShowsSquadEliminatedForDiagnostics
            && string.Equals(
                _hud.DemolitionObjectiveTextForDiagnostics,
                expectedPlantedWipeObjective,
                System.StringComparison.Ordinal);
        GD.Print($"DEMOLITION_PLANTED_WIPE_CHECK valid={plantedWipeDeviceContinues} started={plantedWipeStarted} mates={plantedWipeMates.Length} mates_eliminated={plantedWipeMates.All(candidate => candidate.IsDowned && candidate.ReviveUsed)} squad_eliminated={IsLocalDemolitionSquadEliminated()} player_dead={_player.IsDead} revive_used={_player.ReviveUsed} round_active={_demolitionRoundActive} planted={_demolitionDevicePlanted} score={_demolitionMatch.PlayerScore}/{plantedWipeScore} spectator={_demolitionObjectiveSpectatorActive} fuse={plantedWipeFuseBefore:0.00}->{_demolitionRemaining:0.00} radio={_hud.DemolitionRadioShowsSquadEliminatedForDiagnostics} objective={_hud.DemolitionObjectiveTextForDiagnostics}");

        _squadHoldFire = true;
        ClearDemolitionDevice();
        ResetDemolitionSquad();
        _demolitionRoundActive = true;
        var demolitionFireStanceReset = !_squadHoldFire
            && _squadMates
                .Where(IsInstanceValid)
                .All(candidate => !candidate.HoldFireActive);

        // Deterministic last-operator branch: eliminate every teammate first so the
        // player cannot enter spectator mode and must take the hard round-finish path.
        var noAllyMates = _squadMates
            .Where(candidate => IsInstanceValid(candidate))
            .ToArray();
        var noAllyMatesEliminated = noAllyMates.Length > 0;
        foreach (var noAllyMate in noAllyMates)
        {
            if (!noAllyMate.IsDowned && !noAllyMate.IsBodyBag)
            {
                noAllyMate.TakeCombatDamage(
                    9999.0f,
                    noAllyMate.HitPoint(HitRegion.Torso),
                    this);
            }
            noAllyMatesEliminated &= noAllyMate.IsDowned
                && noAllyMate.ReviveUsed
                && noAllyMate.CollisionLayer == 0
                && noAllyMate.CollisionMask == 0
                && noAllyMate.AreDemolitionCollisionShapesDisabledForDiagnostics;
        }

        _player.SetHealthForDiagnostics(_player.MaxHealth);
        var noAllyEliminationPosition = _player.GlobalPosition;
        _player.TakeDamage(9999.0f, _player.HitPoint(HitRegion.Torso), this);
        await WaitFrames(3);
        var noAllyPlayerEliminated = noAllyMatesEliminated
            && _player.IsDead
            && _player.ReviveUsed
            && !_player.CanBeRevived
            && !_localPlayerDowned
            && !_localPlayerEliminated
            && !_demolitionRoundActive
            && !_player.IsPhysicsProcessing()
            && _player.GlobalPosition.DistanceTo(noAllyEliminationPosition) <= 0.01f
            && _player.Velocity.LengthSquared() <= 0.0001f
            && _player.CollisionLayer == 0
            && _player.CollisionMask == 0
            && _player.DemolitionColliderDisabledForDiagnostics;

        var smokePresentationAligned = Mathf.Abs(SmokeGrenade.CloudRadius - SmokeGrenade.VisualCoverageRadius) <= 0.1f;
        _hud.ShowOperationsOffice();
        var presentationRestored = !_hud.IsDemolitionSquadRosterHidden
            && !_hud.IsDemolitionSkillHudVisible
            && !_hud.AreDemolitionSquadOrdersHidden
            && _hud.FooterHudRuntimeSeparatedForDiagnostics;

        var valid = hudIsolated
            && roleRules
            && eliminationRules
            && deviceLifecycleValid
            && spectatorLocalized
            && eliminatedDeviceSpectatorLocalized
            && reconBoundary
            && playerEliminated
            && mateEliminated
            && playerFrozenAfterElimination
            && mateFrozenAfterElimination
            && playerRestoredForNextRound
            && mateRestoredForNextRound
            && plantedWipeDeviceContinues
            && demolitionFireStanceReset
            && noAllyPlayerEliminated
            && smokePresentationAligned
            && presentationRestored;
        GD.Print($"DEMOLITION_RULES_CHECK valid={valid} roster_hidden={rosterHidden} skill_hud={skillHudVisible} orders_hidden={ordersHidden} utility={utilityHudVisible} footer_separated={demolitionFooterSeparated} recon_range={DemolitionReconScanRange:0.0} recon_boundary={reconBoundary} inputs={roleRules} elimination_rules={eliminationRules} device_lifecycle={deviceLifecycleValid} spectator_localized={spectatorLocalized} eliminated_device_localized={eliminatedDeviceSpectatorLocalized} player_eliminated={playerEliminated} player_frozen={playerFrozenAfterElimination} player_collision={playerEliminationCollision} player_collider_disabled={playerColliderDisabled} player_reset={playerRestoredForNextRound} mate_running_before_elimination={mateWasRunningBeforeElimination} mate_eliminated={mateEliminated} mate_frozen={mateFrozenAfterElimination} mate_collision={mateEliminationCollision} mate_shapes_disabled={mateCollisionShapesDisabled} mate_reset={mateRestoredForNextRound} planted_wipe_continues={plantedWipeDeviceContinues} fire_stance_reset={demolitionFireStanceReset} no_ally_eliminated={noAllyPlayerEliminated} no_ally_mates={noAllyMatesEliminated} smoke_aligned={smokePresentationAligned} presentation_restored={presentationRestored} round_active={_demolitionRoundActive}");
        GD.Print($"DEMOLITION_RULES_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }
}
