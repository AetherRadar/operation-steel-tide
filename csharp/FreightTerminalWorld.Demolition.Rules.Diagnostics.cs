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
        DeploySquad(OperatorRole.Recon, SquadSessionMode.Local, "127.0.0.1");
        _hud.SetDemolitionGameplayPresentation(true);
        _hud.SetLanguage("en");
        _hud.SetDemolitionSmokeGrenades(1);
        _hud.SetStats(100.0f, 0.0f, 100.0f, 0, 0, 2);

        var rosterHidden = _hud.IsDemolitionSquadRosterHidden;
        var skillHudVisible = _hud.IsDemolitionSkillHudVisible;
        var ordersHidden = _hud.AreDemolitionSquadOrdersHidden;
        var utilityHudVisible = _hud.DemolitionUtilityHudText.Contains("4 FRAG", System.StringComparison.Ordinal)
            && _hud.DemolitionUtilityHudText.Contains("5 SMOKE", System.StringComparison.Ordinal);
        var hudIsolated = rosterHidden && skillHudVisible && ordersHidden && utilityHudVisible;
        var roleRules = DemolitionReconScanRange < 72.0f
            && InputMap.HasAction("use_class_skill")
            && InputMap.HasAction("throw_grenade")
            && InputMap.HasAction("weapon_grenade")
            && InputMap.HasAction("weapon_utility")
            && InputMap.ActionGetEvents("weapon_grenade").Count > 0
            && InputMap.ActionGetEvents("weapon_utility").Count > 0;
        var eliminationRules = DemolitionRoundRules.EliminationEndsRound(DemolitionTeam.Attackers, false)
            && DemolitionRoundRules.EliminationEndsRound(DemolitionTeam.Defenders, false)
            && !DemolitionRoundRules.EliminationEndsRound(DemolitionTeam.Attackers, true)
            && DemolitionRoundRules.EliminationEndsRound(DemolitionTeam.Defenders, true);
        var spectatorLocalized = GameLocalization.Get(
                "demolition_spectating_device",
                "zh",
                "SPECTATING  //  PLANTED DEVICE")
            .Contains("\u5df2\u5b89\u653e", System.StringComparison.Ordinal);

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
        if (mate is not null)
        {
            mate.TakeCombatDamage(9999.0f, mate.HitPoint(HitRegion.Torso), this);
            mateEliminationPosition = mate.GlobalPosition;
            var mateCollisionShapesDisabledAfterElimination =
                mate.AreDemolitionCollisionShapesDisabledForDiagnostics;
            mateEliminated = mate.IsDowned
                && mate.ReviveUsed
                && !mate.CanBeRevived
                && mate.CollisionLayer == 0
                && mate.CollisionMask == 0
                && mateCollisionShapesDisabledAfterElimination
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
            && mate.AreDemolitionCollisionShapesDisabledForDiagnostics;
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
            && _player.CollisionMask == (1 | 2)
            && !_player.DemolitionColliderDisabledForDiagnostics
            && _player.Velocity.LengthSquared() <= 0.0001f;
        var mateRestoredForNextRound = mate is not null
            && mate.IsPhysicsProcessing()
            && !mate.IsDowned
            && !mate.ReviveUsed
            && mate.CollisionLayer == 4
            && mate.CollisionMask == 1
            && mate.AreDemolitionCollisionShapesEnabledForDiagnostics
            && mate.Velocity.LengthSquared() <= 0.0001f;

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
            && !_hud.AreDemolitionSquadOrdersHidden;

        var valid = hudIsolated
            && roleRules
            && eliminationRules
            && spectatorLocalized
            && reconBoundary
            && playerEliminated
            && mateEliminated
            && playerFrozenAfterElimination
            && mateFrozenAfterElimination
            && playerRestoredForNextRound
            && mateRestoredForNextRound
            && noAllyPlayerEliminated
            && smokePresentationAligned
            && presentationRestored;
        GD.Print($"DEMOLITION_RULES_CHECK valid={valid} roster_hidden={rosterHidden} skill_hud={skillHudVisible} orders_hidden={ordersHidden} utility={utilityHudVisible} recon_range={DemolitionReconScanRange:0.0} recon_boundary={reconBoundary} inputs={roleRules} elimination_rules={eliminationRules} spectator_localized={spectatorLocalized} player_eliminated={playerEliminated} player_frozen={playerFrozenAfterElimination} player_collision={playerEliminationCollision} player_collider_disabled={playerColliderDisabled} player_reset={playerRestoredForNextRound} mate_eliminated={mateEliminated} mate_frozen={mateFrozenAfterElimination} mate_collision={mateEliminationCollision} mate_shapes_disabled={mateCollisionShapesDisabled} mate_reset={mateRestoredForNextRound} no_ally_eliminated={noAllyPlayerEliminated} no_ally_mates={noAllyMatesEliminated} smoke_aligned={smokePresentationAligned} presentation_restored={presentationRestored} round_active={_demolitionRoundActive}");
        GD.Print($"DEMOLITION_RULES_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }
}
