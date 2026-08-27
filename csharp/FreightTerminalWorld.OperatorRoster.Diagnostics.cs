using System;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private async void ValidateOperatorRoster()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        var roles = OperatorRoles.ExtractionRoles;
        var identityReady = roles.Length == 5
            && roles.Select(OperatorRoles.Callsign).Distinct().Count() == roles.Length
            && roles.Select(role => OperatorRoles.SkillName(role, "en")).Distinct().Count() == roles.Length
            && roles.Count(role => OperatorRoles.Spec(role).VisualId == OperatorVisualId.FemaleFieldOperator) >= 2
            && roles.Count(role => OperatorRoles.Spec(role).BackpackCapacityBonus > 0
                || OperatorRoles.Spec(role).SearchDurationMultiplier < 1.0f) >= 3;
        var localizationReady = roles.All(role =>
            !string.Equals(OperatorRoles.RoleName(role, "en"), OperatorRoles.RoleName(role, "zh"), StringComparison.Ordinal)
            && !string.Equals(OperatorRoles.SkillName(role, "en"), OperatorRoles.SkillName(role, "zh"), StringComparison.Ordinal));

        var randomPlayerRoles = Enumerable.Range(1, 64)
            .Select(seed => OperatorRosterRules.RandomPlayerRole((ulong)seed))
            .Distinct()
            .Count();
        var randomAiValid = roles.All(player => Enumerable.Range(1, 16).All(seed =>
        {
            var assigned = OperatorRosterRules.SelectAiRoles(player, (ulong)seed);
            return assigned.Count == 2
                && assigned.Distinct().Count() == 2
                && assigned.All(role => role != player);
        }));

        AuthoredOperatorVisual? femaleVisual = null;
        AuthoredPreviewOperatorVisual? femalePreview = null;
        var femaleInspection = CombatModelLibrary.InspectOperator(OperatorVisualId.FemaleFieldOperator);
        var femaleAnimations = 0;
        var femaleTransitions = Array.Empty<string>();
        var femaleRifleFit = default(OperatorRifleFitInspection);
        var femaleRifleFits = Array.Empty<OperatorRifleFitInspection>();
        var femalePreviewIdle = false;
        try
        {
            femaleVisual = CombatModelLibrary.InstantiateOperator(
                OperatorVisualId.FemaleFieldOperator,
                WeaponCatalog.Build(WeaponPlatform.AK74, 0));
            AddChild(femaleVisual.Root);
            var animator = new AuthoredOperatorAnimator(femaleVisual);
            femaleAnimations = animator.AnimationCount;
            femaleVisual.SetWeaponReadied(true);
            var sampled = new System.Collections.Generic.List<string>();
            var sampledFits = new System.Collections.Generic.List<OperatorRifleFitInspection>();
            animator.Update(1.0f, 0.0f, true, false, false, false, false, false, false);
            femaleVisual.AnimationPlayer.Advance(0.2);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            sampled.Add(animator.CurrentAnimation);
            sampledFits.Add(femaleVisual.InspectRifleFit());
            animator.Update(1.0f, 1.8f, true, false, false, false, false, false, false);
            femaleVisual.AnimationPlayer.Advance(0.2);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            sampled.Add(animator.CurrentAnimation);
            sampledFits.Add(femaleVisual.InspectRifleFit());
            animator.Update(1.0f, 3.4f, true, false, false, false, false, false, false);
            femaleVisual.AnimationPlayer.Advance(0.2);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            sampled.Add(animator.CurrentAnimation);
            sampledFits.Add(femaleVisual.InspectRifleFit());
            animator.Update(1.0f, 1.8f, true, false, false, true, false, false, false);
            femaleVisual.AnimationPlayer.Advance(0.2);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            sampled.Add(animator.CurrentAnimation);
            sampledFits.Add(femaleVisual.InspectRifleFit());
            femaleTransitions = sampled.ToArray();
            femaleRifleFits = sampledFits.ToArray();
            femaleRifleFit = femaleVisual.InspectRifleFit();

            femalePreview = CombatModelLibrary.InstantiatePreviewOperator(
                OperatorVisualId.FemaleFieldOperator);
            AddChild(femalePreview.Root);
            femalePreviewIdle = CombatModelLibrary.RequireAnimationPlayer(femalePreview.Root)
                .AssignedAnimation == "idle";
        }
        finally
        {
            femaleVisual?.Root.QueueFree();
            femalePreview?.Root.QueueFree();
        }
        var femaleMovementFitValid = femaleRifleFits.Select((fit, index) =>
            index < femaleTransitions.Length
            && (femaleTransitions[index].StartsWith("ready_", StringComparison.Ordinal)
                ? fit.PrimaryHandDistance <= 0.025f
                    && fit.SupportHandDistance <= 0.16f
                    && fit.HandSeparation >= 0.22f
                    && fit.MuzzleOffset.Z <= -0.38f
                : fit.Valid)).ToArray();
        var femaleModelReady = femaleInspection.Loaded
            && femaleInspection.RequiredNodes
            && femaleInspection.MeshCount >= 4
            && femaleInspection.MaterialCount >= 4
            && femaleInspection.Size.Y > 0.5f
            && femaleAnimations >= 25
            && femaleTransitions.SequenceEqual(new[]
            {
                "ready_idle", "ready_walk", "ready_run", "aim_walk"
            })
            && femaleRifleFits.Length == femaleTransitions.Length
            && femaleMovementFitValid.All(valid => valid)
            && femaleRifleFit.Valid
            && femalePreviewIdle;

        var garrison = _enemies.Where(IsInstanceValid)
            .Where(enemy => !enemy.IsWorldBoss && !enemy.IsRivalSquad)
            .ToArray();
        var rivals = _enemies.Where(IsInstanceValid)
            .Where(enemy => enemy.IsRivalSquad)
            .ToArray();
        var fixedGarrison = garrison.Length > 0
            && garrison.All(enemy => enemy.OperatorVisual == OperatorVisualId.Garrison
                && enemy.AuthoredVisualIdForDiagnostics == OperatorVisualId.Garrison);
        var rivalVariety = rivals.Length > 0
            && Enumerable.Range(1, 64)
                .Select(seed => OperatorRosterRules.RivalVisual((ulong)seed))
                .Distinct()
                .Count() == 2
            && rivals.All(enemy => enemy.OperatorVisual is OperatorVisualId.Garrison
                or OperatorVisualId.FemaleFieldOperator);

        _player.ConfigureRole(OperatorRole.Assault);
        var assaultCapacity = _player.BackpackCapacity;
        _player.ConfigureRole(OperatorRole.Scavenger);
        var scavengerCapacity = _player.BackpackCapacity;
        var scavengerSearch = _player.RoleSearchDurationMultiplier;
        var nearbyLoot = _lootSources.First(source => source.IsSearchable
            && IsInstanceValid(source.LootNode)
            && source.Loot.Count > 0);
        _player.GlobalPosition = nearbyLoot.LootNode.GlobalPosition;
        var scavengerActivated = _player.ActivateRoleAbility(broadcast: false);
        _player.AdvanceRoleAbilityForDiagnostics(0.7f);
        var lootSkillReady = scavengerActivated
            && LastOperatorLootScanForDiagnostics.RevealedCount > 0
            && LastOperatorLootScanForDiagnostics.TotalValue > 0
            && scavengerCapacity >= assaultCapacity + 4
            && scavengerSearch <= 0.73f;

        _player.ConfigureRole(OperatorRole.Locksmith);
        var locksmithPassive = _player.RoleSearchDurationMultiplier;
        var locksmithActivated = _player.ActivateRoleAbility(broadcast: false);
        var locksmithActive = _player.RoleSearchDurationMultiplier;
        var locksmithReady = locksmithActivated
            && _player.BackpackCapacity >= assaultCapacity + 2
            && locksmithPassive <= 0.79f
            && locksmithActive <= 0.26f;

        var uiReady = _hud.OperatorRoleCardCountForDiagnostics == roles.Length;
        var valid = identityReady
            && localizationReady
            && randomPlayerRoles == roles.Length
            && randomAiValid
            && femaleModelReady
            && fixedGarrison
            && rivalVariety
            && lootSkillReady
            && locksmithReady
            && uiReady;
        var femaleMovementFit = string.Join(',', femaleRifleFits.Select((fit, index) =>
            $"{femaleTransitions[index]}:{femaleMovementFitValid[index]}:support={fit.SupportHandDistance:F3}:"
            + $"muzzle={fit.MuzzleOffset}:stock={fit.StockOffset}"));
        GD.Print(
            $"OPERATOR_ROSTER_CHECK roles={roles.Length} identity={identityReady} localization={localizationReady} "
            + $"random_player_roles={randomPlayerRoles} random_ai={randomAiValid} "
            + $"female_loaded={femaleInspection.Loaded} female_nodes={femaleInspection.RequiredNodes} "
            + $"female_meshes={femaleInspection.MeshCount} female_materials={femaleInspection.MaterialCount} "
            + $"female_size={femaleInspection.Size} female_animations={femaleAnimations} "
            + $"female_transitions={string.Join('>', femaleTransitions)} female_rifle_fit={femaleRifleFit.Valid} "
            + $"female_movement_fit={femaleMovementFit} "
            + $"female_primary={femaleRifleFit.PrimaryHandDistance:F3} "
            + $"female_primary_offset={femaleRifleFit.PrimaryHandOffset} "
            + $"female_primary_rotation={femaleRifleFit.PrimaryHandRotation} "
            + $"female_support={femaleRifleFit.SupportHandDistance:F3} "
            + $"female_support_offset={femaleRifleFit.SupportHandOffset} "
            + $"female_support_target={femaleRifleFit.SupportHandTargetOffset} "
            + $"female_muzzle={femaleRifleFit.MuzzleOffset} female_stock={femaleRifleFit.StockOffset} "
            + $"female_preview_idle={femalePreviewIdle} "
            + $"garrison={garrison.Length} fixed_garrison={fixedGarrison} rivals={rivals.Length} rival_variety={rivalVariety} "
            + $"scavenger_capacity={scavengerCapacity}/{assaultCapacity} scavenger_search={scavengerSearch:F2} "
            + $"loot_revealed={LastOperatorLootScanForDiagnostics.RevealedCount} loot_value={LastOperatorLootScanForDiagnostics.TotalValue} "
            + $"locksmith_search={locksmithPassive:F2}/{locksmithActive:F2} ui_cards={_hud.OperatorRoleCardCountForDiagnostics}");
        GD.Print($"OPERATOR_ROSTER_PASS valid={valid}");
        QuitDiagnosticAfterSceneCleanup(valid ? 0 : 2);
    }
}
