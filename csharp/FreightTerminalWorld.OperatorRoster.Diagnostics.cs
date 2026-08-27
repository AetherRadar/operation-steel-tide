using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private async void ValidateOperatorRoster()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        var roles = OperatorRoles.ExtractionRoles;
        var playerVisuals = roles.Select(role => OperatorRoles.Spec(role).VisualId).ToArray();
        var identityReady = roles.Length == 5
            && roles.Select(OperatorRoles.Callsign).Distinct().Count() == roles.Length
            && roles.Select(role => OperatorRoles.SkillName(role, "en")).Distinct().Count() == roles.Length
            && playerVisuals.Distinct().Count() == roles.Length
            && playerVisuals.All(visual => visual != OperatorVisualId.Garrison)
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

        var rosterModelsReady = true;
        var visualReports = new List<string>(roles.Length);
        foreach (var role in roles)
        {
            var visualId = OperatorRoles.Spec(role).VisualId;
            var inspection = CombatModelLibrary.InspectOperator(visualId);
            AuthoredOperatorVisual? runtimeVisual = null;
            AuthoredPreviewOperatorVisual? previewVisual = null;
            var animationCount = 0;
            var transitions = Array.Empty<string>();
            var rifleFits = Array.Empty<OperatorRifleFitInspection>();
            var previewIdle = false;
            var previewWeapon = false;
            try
            {
                var rifle = WeaponCatalog.Build(WeaponPlatform.AK74, 0);
                runtimeVisual = CombatModelLibrary.InstantiateOperator(visualId, rifle);
                AddChild(runtimeVisual.Root);
                var animator = new AuthoredOperatorAnimator(runtimeVisual);
                animationCount = animator.AnimationCount;
                runtimeVisual.SetWeaponReadied(true);
                var sampledTransitions = new List<string>(4);
                var sampledFits = new List<OperatorRifleFitInspection>(4);
                var samples = new (float Speed, bool Aiming)[]
                {
                    (0.0f, false), (1.8f, false), (3.4f, false), (1.8f, true)
                };
                foreach (var sample in samples)
                {
                    animator.Update(1.0f, sample.Speed, true, false, false, sample.Aiming, false, false, false);
                    runtimeVisual.AnimationPlayer.Advance(0.2);
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    sampledTransitions.Add(animator.CurrentAnimation);
                    sampledFits.Add(runtimeVisual.InspectRifleFit());
                }
                transitions = sampledTransitions.ToArray();
                rifleFits = sampledFits.ToArray();

                previewVisual = CombatModelLibrary.InstantiatePreviewOperator(visualId, rifle);
                AddChild(previewVisual.Root);
                previewIdle = CombatModelLibrary.RequireAnimationPlayer(previewVisual.Root)
                    .AssignedAnimation == "idle";
                previewWeapon = previewVisual.HasWeapon;
            }
            finally
            {
                runtimeVisual?.Root.QueueFree();
                previewVisual?.Root.QueueFree();
            }

            var movementFits = rifleFits.Select((fit, index) =>
                index < transitions.Length
                && (transitions[index].StartsWith("ready_", StringComparison.Ordinal)
                    ? fit.PrimaryHandDistance <= 0.025f
                        && fit.SupportHandDistance <= 0.16f
                        && fit.HandSeparation >= 0.22f
                        && fit.MuzzleOffset.Z <= -0.38f
                    : fit.Valid)).ToArray();
            var modelReady = inspection.Loaded
                && inspection.RequiredNodes
                && inspection.MeshCount >= 4
                && inspection.MaterialCount >= 4
                && inspection.VertexCount >= 20_000
                && inspection.TriangleCount is >= 40_000 and <= 105_000
                && inspection.Size.Y > 0.5f
                && animationCount >= 25
                && transitions.SequenceEqual(new[]
                {
                    "ready_idle", "ready_walk", "ready_run", "aim_walk"
                })
                && rifleFits.Length == transitions.Length
                && movementFits.All(valid => valid)
                && previewIdle
                && previewWeapon;
            rosterModelsReady &= modelReady;
            visualReports.Add(
                $"{role}:{visualId}:ok={modelReady}:meshes={inspection.MeshCount}:materials={inspection.MaterialCount}:"
                + $"vertices={inspection.VertexCount}:triangles={inspection.TriangleCount}:"
                + $"animations={animationCount}:preview={previewIdle}/{previewWeapon}:"
                + $"fit={string.Join(',', movementFits)}");
        }

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
                .Count() == playerVisuals.Length
            && rivals.All(enemy => playerVisuals.Contains(enemy.OperatorVisual)
                && enemy.AuthoredVisualIdForDiagnostics == enemy.OperatorVisual);

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
            && rosterModelsReady
            && fixedGarrison
            && rivalVariety
            && lootSkillReady
            && locksmithReady
            && uiReady;
        GD.Print(
            $"OPERATOR_ROSTER_CHECK roles={roles.Length} identity={identityReady} localization={localizationReady} "
            + $"random_player_roles={randomPlayerRoles} random_ai={randomAiValid} "
            + $"unique_visuals={playerVisuals.Distinct().Count()} roster_models={rosterModelsReady} "
            + $"visuals={string.Join(';', visualReports)} "
            + $"garrison={garrison.Length} fixed_garrison={fixedGarrison} rivals={rivals.Length} rival_variety={rivalVariety} "
            + $"scavenger_capacity={scavengerCapacity}/{assaultCapacity} scavenger_search={scavengerSearch:F2} "
            + $"loot_revealed={LastOperatorLootScanForDiagnostics.RevealedCount} loot_value={LastOperatorLootScanForDiagnostics.TotalValue} "
            + $"locksmith_search={locksmithPassive:F2}/{locksmithActive:F2} ui_cards={_hud.OperatorRoleCardCountForDiagnostics}");
        GD.Print($"OPERATOR_ROSTER_PASS valid={valid}");
        QuitDiagnosticAfterSceneCleanup(valid ? 0 : 2);
    }

    private async void CaptureOperatorRosterFrame()
    {
        var canvas = new CanvasLayer { Layer = 120 };
        AddChild(canvas);
        var background = new ColorRect
        {
            Color = new Color(0.006f, 0.012f, 0.014f, 1.0f)
        };
        background.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        canvas.AddChild(background);

        var title = new Label
        {
            Text = "OPERATION STEEL TIDE  //  UNIQUE OPERATOR ROSTER",
            Position = new Vector2(34.0f, 22.0f),
            Size = new Vector2(1600.0f, 42.0f)
        };
        title.AddThemeFontSizeOverride("font_size", 26);
        title.AddThemeColorOverride("font_color", new Color(0.35f, 0.92f, 0.78f));
        background.AddChild(title);

        var row = new HBoxContainer
        {
            Position = new Vector2(28.0f, 76.0f),
            Size = new Vector2(1864.0f, 962.0f)
        };
        row.AddThemeConstantOverride("separation", 14);
        background.AddChild(row);
        var weapons = new[]
        {
            WeaponPlatform.M4A1,
            WeaponPlatform.MP5A5,
            WeaponPlatform.VSS,
            WeaponPlatform.AK74,
            WeaponPlatform.P226
        };

        for (var index = 0; index < OperatorRoles.ExtractionRoles.Length; index++)
        {
            var role = OperatorRoles.ExtractionRoles[index];
            var spec = OperatorRoles.Spec(role);
            var panel = new ColorRect
            {
                CustomMinimumSize = new Vector2(360.0f, 940.0f),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                Color = new Color(0.012f, 0.026f, 0.029f, 1.0f)
            };
            row.AddChild(panel);
            panel.AddChild(new ColorRect
            {
                Position = Vector2.Zero,
                Size = new Vector2(360.0f, 5.0f),
                Color = spec.Accent
            });

            var name = new Label
            {
                Text = $"{spec.Callsign}  //  {spec.Name}",
                Position = new Vector2(14.0f, 18.0f),
                Size = new Vector2(330.0f, 34.0f)
            };
            name.AddThemeFontSizeOverride("font_size", 19);
            name.AddThemeColorOverride("font_color", spec.Accent.Lightened(0.18f));
            panel.AddChild(name);

            var skill = new Label
            {
                Text = spec.SkillName,
                Position = new Vector2(14.0f, 50.0f),
                Size = new Vector2(330.0f, 24.0f)
            };
            skill.AddThemeFontSizeOverride("font_size", 12);
            skill.AddThemeColorOverride("font_color", new Color(0.72f, 0.82f, 0.79f));
            panel.AddChild(skill);

            var preview = new InventoryModelPreview
            {
                Position = new Vector2(8.0f, 82.0f),
                Size = new Vector2(344.0f, 792.0f)
            };
            preview.Configure(
                InventoryPreviewKind.Operator,
                weapon: WeaponCatalog.Build(weapons[index], 0),
                role: role);
            panel.AddChild(preview);

            var footer = new Label
            {
                Text = $"VISUAL  {spec.VisualId}  //  HIGH-DETAIL AUTHORED CC0",
                Position = new Vector2(14.0f, 892.0f),
                Size = new Vector2(330.0f, 25.0f),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            footer.AddThemeFontSizeOverride("font_size", 11);
            footer.AddThemeColorOverride("font_color", new Color(0.47f, 0.62f, 0.58f));
            panel.AddChild(footer);
        }

        await ToSignal(GetTree().CreateTimer(1.1f), SceneTreeTimer.SignalName.Timeout);
        var image = GetViewport().GetTexture().GetImage();
        image.SavePng("user://operator_roster_validation.png");
        GD.Print("CAPTURE_OPERATOR_ROSTER user://operator_roster_validation.png");
        GetTree().Quit();
    }
}
