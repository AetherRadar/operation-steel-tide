using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private async void ValidateSquadTactics()
    {
        await WaitFrames(8);
        EnsureAiSquadFill();
        _missionDirector.ExitDeploymentZone();

        var mates = _squadMates
            .Where(mate => IsInstanceValid(mate) && !mate.IsHumanProxy)
            .Take(2)
            .ToArray();
        var rivalA = _hostileSquads
            .FirstOrDefault(squad => squad.TeamId == 1)?
            .Members.FirstOrDefault(member => IsInstanceValid(member) && !member.IsDead);
        var rivalB = _hostileSquads
            .FirstOrDefault(squad => squad.TeamId == 2)?
            .Members.FirstOrDefault(member => IsInstanceValid(member) && !member.IsDead);
        var garrison = _enemies.FirstOrDefault(enemy =>
            IsInstanceValid(enemy)
            && !enemy.IsDead
            && !enemy.IsRivalSquad
            && !enemy.IsWorldBoss);
        if (mates.Length != 2 || rivalA is null || rivalB is null || garrison is null)
        {
            GD.Print("SQUAD_TACTICS_CHECK valid=False reason=missing_actors");
            GD.Print("SQUAD_TACTICS_PASS valid=False");
            GetTree().Quit(2);
            return;
        }

        foreach (var enemy in _enemies.Where(IsInstanceValid))
        {
            enemy.ProcessMode = ProcessModeEnum.Disabled;
            enemy.GlobalPosition = new Vector3(260.0f + enemy.NetworkId, 80.3f, 260.0f);
        }
        foreach (var mate in _squadMates.Where(IsInstanceValid))
        {
            mate.ProcessMode = ProcessModeEnum.Disabled;
            mate.GlobalPosition = new Vector3(230.0f + mate.SquadSlot * 3.0f, 80.3f, 230.0f);
        }
        _player.ProcessMode = ProcessModeEnum.Disabled;

        var priorityOrigin = new Vector3(6.0f, 0.3f, 52.0f);
        rivalA.GlobalPosition = priorityOrigin;
        rivalB.GlobalPosition = priorityOrigin + new Vector3(0.0f, 0.0f, 11.0f);
        _player.GlobalPosition = priorityOrigin + new Vector3(7.0f, 0.0f, 0.0f);
        rivalA.GrantFireablePrimaryForDiagnostics();
        rivalB.GrantFireablePrimaryForDiagnostics();
        rivalA.ConfigureCombatProbeForDiagnostics(
            7101UL,
            rivalB.GlobalPosition,
            bypassPlayerProtection: true,
            suppressContactSharing: true);
        rivalB.ProcessMode = ProcessModeEnum.Disabled;
        rivalA.ProcessMode = ProcessModeEnum.Inherit;
        InvalidateCombatTargetIndex();
        var rivalPriority = false;
        for (var frame = 0; frame < 72 && !rivalA.IsDead && !rivalB.IsDead; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            rivalPriority |= rivalA.EngageTargetNode == rivalB;
        }

        rivalA.ProcessMode = ProcessModeEnum.Disabled;
        rivalA.ResetTacticalStateForDiagnostics();
        garrison.ResetTacticalStateForDiagnostics();
        garrison.GrantFireablePrimaryForDiagnostics();
        garrison.GlobalPosition = priorityOrigin;
        rivalA.GlobalPosition = priorityOrigin + new Vector3(0.0f, 0.0f, 11.0f);
        _player.GlobalPosition = priorityOrigin + new Vector3(7.0f, 0.0f, 0.0f);
        garrison.ConfigureCombatProbeForDiagnostics(
            7201UL,
            rivalA.GlobalPosition,
            bypassPlayerProtection: true,
            suppressContactSharing: true);
        garrison.ProcessMode = ProcessModeEnum.Inherit;
        InvalidateCombatTargetIndex();
        var garrisonPriority = false;
        for (var frame = 0; frame < 72 && !garrison.IsDead && !rivalA.IsDead; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            garrisonPriority |= garrison.EngageTargetNode == rivalA;
        }

        garrison.ProcessMode = ProcessModeEnum.Disabled;
        rivalA.ResetTacticalStateForDiagnostics();
        rivalA.GrantFireablePrimaryForDiagnostics();
        rivalA.SentryMode = true;
        var duelOrigin = new Vector3(0.0f, 0.3f, 52.0f);
        rivalA.GlobalPosition = duelOrigin + new Vector3(0.0f, 0.0f, 15.0f);
        rivalA.ConfigureCombatProbeForDiagnostics(
            7301UL,
            duelOrigin,
            bypassPlayerProtection: true,
            suppressContactSharing: true);
        _player.GlobalPosition = new Vector3(210.0f, 80.3f, 210.0f);
        for (var index = 0; index < mates.Length; index++)
        {
            var mate = mates[index];
            var position = duelOrigin + new Vector3(index == 0 ? -2.2f : 2.2f, 0.0f, index * 0.6f);
            mate.GlobalPosition = position;
            mate.Velocity = Vector3.Zero;
            mate.ResetCombatTacticsForDiagnostics();
            mate.GrantFireablePrimaryForDiagnostics();
            mate.SetOrder(SquadOrder.Hold, position);
            mate.ProcessMode = ProcessModeEnum.Inherit;
        }
        rivalA.ProcessMode = ProcessModeEnum.Inherit;
        InvalidateCombatTargetIndex();
        for (var frame = 0; frame < 720 && !rivalA.IsDead; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            if (mates.All(mate => mate.IsBodyBag || mate.IsDowned))
            {
                break;
            }
        }

        var readyMates = mates.Count(mate => !mate.IsBodyBag && !mate.IsDowned);
        var matesFired = mates.Sum(mate => mate.CombatShotsFired);
        var twoVersusOne = rivalA.IsDead && readyMates >= 1 && matesFired > 0;
        var garrisonColor = garrison.AuthoredTeamColorForDiagnostics;
        var rivalColor = rivalA.AuthoredTeamColorForDiagnostics;
        var authoredFactions = garrison.UsesAuthoredOperatorForDiagnostics
            && rivalA.UsesAuthoredOperatorForDiagnostics
            && garrison.AuthoredGearOverlayCountForDiagnostics >= 3
            && rivalA.AuthoredGearOverlayCountForDiagnostics >= 3
            && ColorDistance(garrisonColor, rivalColor) > 0.55f;
        var valid = rivalPriority && garrisonPriority && twoVersusOne && authoredFactions;
        GD.Print(
            $"SQUAD_TACTICS_CHECK valid={valid} rival_priority={rivalPriority} "
            + $"garrison_priority={garrisonPriority} two_vs_one={twoVersusOne} "
            + $"enemy_dead={rivalA.IsDead} ready_mates={readyMates} mate_shots={matesFired} "
            + $"authored_factions={authoredFactions} garrison_color={garrisonColor} rival_color={rivalColor}");
        GD.Print($"SQUAD_TACTICS_PASS valid={valid}");
        QuitDiagnosticAfterSceneCleanup(valid ? 0 : 2);
    }
}
