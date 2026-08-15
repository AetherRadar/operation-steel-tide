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
        var playerEliminated = _player.IsDead
            && _player.ReviveUsed
            && !_player.CanBeRevived
            && _player.CollisionLayer == 0
            && _player.CollisionMask == 0
            && _localPlayerEliminated
            && !_localPlayerDowned
            && !_hud.IsDownedBannerVisible
            && !_player.TryReceiveRevive(50.0f)
            && _demolitionRoundActive
            && _demolitionMatch.PlayerScore == scoreBefore;

        var mate = _squadMates.FirstOrDefault(candidate => IsInstanceValid(candidate) && !candidate.IsDowned);
        var mateEliminated = false;
        if (mate is not null)
        {
            mate.TakeCombatDamage(9999.0f, mate.HitPoint(HitRegion.Torso), this);
            mateEliminated = mate.IsDowned
                && mate.ReviveUsed
                && !mate.CanBeRevived
                && mate.CollisionLayer == 0
                && mate.CollisionMask == 0
                && !mate.TryReceiveRevive(50.0f)
                && !mate.IsBodyBag;
        }

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
            && smokePresentationAligned
            && presentationRestored;
        GD.Print($"DEMOLITION_RULES_CHECK valid={valid} roster_hidden={rosterHidden} skill_hud={skillHudVisible} orders_hidden={ordersHidden} utility={utilityHudVisible} recon_range={DemolitionReconScanRange:0.0} recon_boundary={reconBoundary} inputs={roleRules} elimination_rules={eliminationRules} spectator_localized={spectatorLocalized} player_eliminated={playerEliminated} player_collision={_player.CollisionLayer}/{_player.CollisionMask} mate_eliminated={mateEliminated} smoke_aligned={smokePresentationAligned} presentation_restored={presentationRestored} round_active={_demolitionRoundActive}");
        GD.Print($"DEMOLITION_RULES_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }
}
