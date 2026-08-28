using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private readonly record struct MeleeRuntimeDiagnostic(
        bool SlotEquipped,
        bool Authored,
        bool CorrectStyle,
        bool HandPoseValid,
        bool ThreeAttackSequence,
        bool TrailVisible,
        bool BladeSweep,
        int MaximumSweepRays,
        string AttackSequence);

    private async void ValidateMelee()
        => await RunMeleeDiagnostic();

    private async void CaptureMelee()
        => await RunMeleeDiagnostic();

    private async Task RunMeleeDiagnostic()
    {
        var definitionsValid = false;
        var modelsValid = false;
        var lootPolicyValid = false;
        var authorityValid = false;
        var tacticalRuntime = default(MeleeRuntimeDiagnostic);
        var zhanmaRuntime = default(MeleeRuntimeDiagnostic);
        var tianxuanRuntime = default(MeleeRuntimeDiagnostic);
        var combatRuntime = default(MeleeCombatDiagnostic);
        var tacticalInspection = default(MeleeModelInspection);
        var zhanmaInspection = default(MeleeModelInspection);
        var tianxuanInspection = default(MeleeModelInspection);
        var failure = string.Empty;

        try
        {
            foreach (var enemy in _enemies)
            {
                enemy.ProcessMode = ProcessModeEnum.Disabled;
            }
            foreach (var mate in _squadMates)
            {
                if (IsInstanceValid(mate))
                {
                    mate.ProcessMode = ProcessModeEnum.Disabled;
                    mate.GlobalPosition = new Vector3(
                        240.0f + mate.SquadSlot * 3.0f,
                        80.0f,
                        240.0f);
                }
            }

            SetCaptureLanguage("en");
            _player.UiLocked = false;
            _player.IsDead = false;
            _player.GlobalPosition = new Vector3(0.0f, 0.2f, 40.0f);
            _player.Velocity = Vector3.Zero;
            _player.FaceWorldPointForDiagnostics(new Vector3(0.0f, 0.2f, -40.0f));
            await WaitFrames(4);

            var tactical = KnifeSkinCatalog.Definition(KnifeSkinCatalog.DefaultId);
            var zhanma = KnifeSkinCatalog.Definition("knife_zhanma");
            var tianxuan = KnifeSkinCatalog.Definition("knife_tianxuan");
            definitionsValid = ValidateMeleeDefinitions(tactical, zhanma, tianxuan);

            tacticalInspection = CombatModelLibrary.InspectMelee(tactical);
            zhanmaInspection = CombatModelLibrary.InspectMelee(zhanma);
            tianxuanInspection = CombatModelLibrary.InspectMelee(tianxuan);
            modelsValid = ValidateMeleeModel(tacticalInspection, 0.24f, 0.28f)
                && ValidateMeleeModel(zhanmaInspection, 0.92f, 0.95f)
                && ValidateMeleeModel(tianxuanInspection, 0.98f, 1.02f);

            var lootCapabilities = LootInteractionPolicy.GetBackpackMenuCapabilities(
                LootItemKind.KnifeSkin);
            lootPolicyValid = lootCapabilities.CanEquip && lootCapabilities.CanDrop;

            _player.SelectQuickSlot(PlayerQuickSlot.Melee, notify: false);
            await WaitFrames(3);
            tacticalRuntime = await InspectMeleeRuntime(
                tactical,
                "res://melee_tactical_ready_validation.png",
                "res://melee_tactical_slash_validation.png");
            zhanmaRuntime = await InspectMeleeRuntime(
                zhanma,
                "res://melee_zhanma_ready_validation.png",
                "res://melee_zhanma_slash_validation.png");
            tianxuanRuntime = await InspectMeleeRuntime(
                tianxuan,
                "res://melee_tianxuan_ready_validation.png",
                "res://melee_tianxuan_slash_validation.png");
            combatRuntime = await ValidateMeleeCombatSemantics();
            authorityValid = ValidateRemoteMeleeAuthorityForDiagnostics();
        }
        catch (Exception exception)
        {
            failure = $"{exception.GetType().Name}:{exception.Message}";
            GD.PushError($"Melee validation failed: {exception}");
        }

        var runtimeValid = RuntimeMeleeValid(tacticalRuntime)
            && RuntimeMeleeValid(zhanmaRuntime)
            && RuntimeMeleeValid(tianxuanRuntime);
        var valid = string.IsNullOrEmpty(failure)
            && definitionsValid
            && modelsValid
            && lootPolicyValid
            && authorityValid
            && runtimeValid
            && combatRuntime.Valid;
        GD.Print(
            $"MELEE_CHECK valid={valid} definitions={definitionsValid} models={modelsValid} "
            + $"loot_equip={lootPolicyValid} "
            + $"authority={authorityValid} "
            + $"tactical={FormatMeleeInspection(tacticalInspection)} "
            + $"zhanma={FormatMeleeInspection(zhanmaInspection)} "
            + $"tianxuan={FormatMeleeInspection(tianxuanInspection)} "
            + $"tactical_runtime={FormatMeleeRuntime(tacticalRuntime)} "
            + $"zhanma_runtime={FormatMeleeRuntime(zhanmaRuntime)} "
            + $"tianxuan_runtime={FormatMeleeRuntime(tianxuanRuntime)} "
            + $"combat={FormatMeleeCombat(combatRuntime)} "
            + $"failure={(string.IsNullOrEmpty(failure) ? "none" : failure.Replace(' ', '_'))}");
        GD.Print($"MELEE_PASS valid={valid}");
        QuitDiagnosticAfterSceneCleanup(valid ? 0 : 2);
    }

    private async Task<MeleeRuntimeDiagnostic> InspectMeleeRuntime(
        KnifeSkinDefinition definition,
        string readyCapturePath,
        string slashCapturePath)
    {
        var previousId = _player.EquippedKnifeSkinId;
        if (string.Equals(previousId, definition.Id, StringComparison.OrdinalIgnoreCase))
        {
            var stagingId = string.Equals(
                definition.Id,
                KnifeSkinCatalog.DefaultId,
                StringComparison.OrdinalIgnoreCase)
                    ? "knife_crimson"
                    : KnifeSkinCatalog.DefaultId;
            _player.EquipFromLoot(new LootItem
            {
                Kind = LootItemKind.KnifeSkin,
                KnifeSkinId = stagingId,
                Grade = LootGrade.Rare
            });
            previousId = _player.EquippedKnifeSkinId;
        }
        var loot = new LootItem
        {
            Kind = LootItemKind.KnifeSkin,
            KnifeSkinId = definition.Id,
            Grade = LootGrade.Legendary
        };
        var replacement = _player.EquipFromLootToWeaponSlot(loot, PlayerWeaponSlot.Melee);
        var slotEquipped = !ReferenceEquals(replacement, loot)
            && replacement?.Kind == LootItemKind.KnifeSkin
            && string.Equals(replacement.KnifeSkinId, previousId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(
                _player.EquippedKnifeSkinId,
                definition.Id,
                StringComparison.OrdinalIgnoreCase);

        // Keep the remaining visual/action checks useful while the slot policy is being
        // diagnosed. SlotEquipped remains false if the production route rejected the item.
        if (!slotEquipped)
        {
            _player.EquipFromLoot(loot);
        }
        _player.SelectQuickSlot(PlayerQuickSlot.Melee, notify: false);
        await WaitFrames(52);

        var authored = _player.UsesAuthoredMeleeForDiagnostics;
        var correctStyle = _player.MeleeStyleForDiagnostics == definition.Style;
        var handPoseValid = _player.MeleeHandPoseMatchesDefinitionForDiagnostics;
        SaveViewportImage(readyCapturePath);

        var attackSequence = new List<int>(3);
        var trailAttackCount = 0;
        var bladeSweepAttackCount = 0;
        var maximumSweepRays = 0;
        if (await StartAndWaitForMeleeAttack(0, 120))
        {
            attackSequence.Add(0);
            trailAttackCount += await WaitForMeleeTrail(100) ? 1 : 0;
            bladeSweepAttackCount += _player.MeleeBladeSweepResolvedForDiagnostics ? 1 : 0;
            maximumSweepRays = Math.Max(
                maximumSweepRays,
                _player.MeleeSweepRayCountForDiagnostics);
            SaveViewportImage(slashCapturePath);
        }
        if (await QueueAndWaitForNextMeleeAttack(0, 1, 120))
        {
            attackSequence.Add(1);
            trailAttackCount += await WaitForMeleeTrail(100) ? 1 : 0;
            bladeSweepAttackCount += _player.MeleeBladeSweepResolvedForDiagnostics ? 1 : 0;
            maximumSweepRays = Math.Max(
                maximumSweepRays,
                _player.MeleeSweepRayCountForDiagnostics);
        }
        if (await QueueAndWaitForNextMeleeAttack(1, 2, 120))
        {
            attackSequence.Add(2);
            trailAttackCount += await WaitForMeleeTrail(100) ? 1 : 0;
            bladeSweepAttackCount += _player.MeleeBladeSweepResolvedForDiagnostics ? 1 : 0;
            maximumSweepRays = Math.Max(
                maximumSweepRays,
                _player.MeleeSweepRayCountForDiagnostics);
        }

        var threeAttackSequence = attackSequence.Count == 3
            && attackSequence[0] == 0
            && attackSequence[1] == 1
            && attackSequence[2] == 2;
        var trailVisible = threeAttackSequence && trailAttackCount == 3;
        var bladeSweep = threeAttackSequence
            && bladeSweepAttackCount == 3
            && maximumSweepRays is > 0 and <= 1600;
        return new MeleeRuntimeDiagnostic(
            slotEquipped,
            authored,
            correctStyle,
            handPoseValid,
            threeAttackSequence,
            trailVisible,
            bladeSweep,
            maximumSweepRays,
            string.Join('>', attackSequence));
    }

    private async Task<bool> WaitForMeleeAttack(int expectedIndex, int maximumFrames)
    {
        for (var frame = 0; frame < maximumFrames; frame++)
        {
            if (_player.MeleeAttackActiveForDiagnostics
                && _player.MeleeAttackIndexForDiagnostics == expectedIndex)
            {
                return true;
            }
            await WaitFrames(1);
        }
        return false;
    }

    private async Task<bool> StartAndWaitForMeleeAttack(int expectedIndex, int maximumFrames)
    {
        for (var frame = 0; frame < maximumFrames; frame++)
        {
            if (_player.MeleeAttackActiveForDiagnostics
                && _player.MeleeAttackIndexForDiagnostics == expectedIndex)
            {
                return true;
            }
            if (!_player.MeleeAttackActiveForDiagnostics)
            {
                _player.StartMeleeAttackForDiagnostics();
            }
            await WaitFrames(1);
        }
        return false;
    }

    private async Task<bool> WaitForMeleeTrail(int maximumFrames)
    {
        for (var frame = 0; frame < maximumFrames; frame++)
        {
            if (_player.MeleeAttackActiveForDiagnostics
                && _player.MeleeTrailSampleCountForDiagnostics >= 4
                && _player.MeleeBladeSweepResolvedForDiagnostics)
            {
                return true;
            }
            await WaitFrames(1);
        }
        return false;
    }

    private async Task<bool> QueueAndWaitForNextMeleeAttack(
        int currentIndex,
        int nextIndex,
        int maximumFrames)
    {
        for (var frame = 0; frame < maximumFrames; frame++)
        {
            if (_player.MeleeAttackActiveForDiagnostics
                && _player.MeleeAttackIndexForDiagnostics == nextIndex)
            {
                return true;
            }
            if (!_player.MeleeAttackActiveForDiagnostics
                || _player.MeleeAttackIndexForDiagnostics == currentIndex)
            {
                _player.StartMeleeAttackForDiagnostics();
            }
            await WaitFrames(1);
        }
        return false;
    }

    private static bool ValidateMeleeDefinitions(
        KnifeSkinDefinition tactical,
        KnifeSkinDefinition zhanma,
        KnifeSkinDefinition tianxuan)
    {
        var attackIds = new HashSet<string>(StringComparer.Ordinal);
        var attackProfilesValid = true;
        foreach (var style in new[] { MeleeWeaponStyle.ZhanmaDao, MeleeWeaponStyle.TianxuanDao })
        {
            attackProfilesValid &= MeleeAttackCatalog.AttackCount(style) == 3;
            for (var index = 0; index < 3; index++)
            {
                var attack = MeleeAttackCatalog.AttackFor(style, index);
                attackProfilesValid &= !string.IsNullOrWhiteSpace(attack.Id)
                    && attackIds.Add(attack.Id)
                    && attack.Duration is >= 0.55f and <= 1.1f
                    && attack.HitProgress is > 0.2f and < 0.7f
                    && attack.DamageMultiplier > 0.8f
                    && attack.SweepSamples >= 6
                    && attack.MaxTargets >= 2;
            }
        }

        return zhanma.Style == MeleeWeaponStyle.ZhanmaDao
            && tianxuan.Style == MeleeWeaponStyle.TianxuanDao
            && tactical.Style == MeleeWeaponStyle.TacticalKnife
            && zhanma.TwoHanded
            && tianxuan.TwoHanded
            && !tactical.TwoHanded
            && zhanma.Reach > tactical.Reach
            && tianxuan.Reach > tactical.Reach
            && zhanma.BaseDamage > tactical.BaseDamage
            && tianxuan.BaseDamage > tactical.BaseDamage
            && !string.Equals(zhanma.LocalizationKey, tianxuan.LocalizationKey, StringComparison.Ordinal)
            && !string.Equals(
                CombatModelLibrary.MeleeScenePath(zhanma.Style),
                CombatModelLibrary.MeleeScenePath(tianxuan.Style),
                StringComparison.Ordinal)
            && attackProfilesValid;
    }

    private static bool ValidateMeleeModel(
        MeleeModelInspection inspection,
        float minimumBladeLength,
        float maximumBladeLength)
        => inspection.Loaded
        && inspection.RequiredNodes
        && inspection.AuthoredMeshes
        && inspection.MeshCount >= 4
        && inspection.MaterialCount >= 4
        && inspection.TriangleCount is >= 3000 and <= 35000
        && inspection.Size.X > 0.02f
        && inspection.Size.Y > 0.02f
        && inspection.Size.Z > 0.02f
        && inspection.BladeLength >= minimumBladeLength
        && inspection.BladeLength <= maximumBladeLength;

    private static bool RuntimeMeleeValid(MeleeRuntimeDiagnostic runtime)
        => runtime.SlotEquipped
        && runtime.Authored
        && runtime.CorrectStyle
        && runtime.HandPoseValid
        && runtime.ThreeAttackSequence
        && runtime.TrailVisible
        && runtime.BladeSweep;

    private static string FormatMeleeInspection(MeleeModelInspection inspection)
        => $"loaded:{inspection.Loaded};markers:{inspection.RequiredNodes};authored:{inspection.AuthoredMeshes};"
            + $"meshes:{inspection.MeshCount};materials:{inspection.MaterialCount};"
            + $"triangles:{inspection.TriangleCount};length:{inspection.BladeLength:0.000};size:{inspection.Size}";

    private static string FormatMeleeRuntime(MeleeRuntimeDiagnostic runtime)
        => $"slot:{runtime.SlotEquipped};authored:{runtime.Authored};style:{runtime.CorrectStyle};"
            + $"hand_pose:{runtime.HandPoseValid};attacks:{runtime.AttackSequence};"
            + $"combo:{runtime.ThreeAttackSequence};trail:{runtime.TrailVisible};"
            + $"blade_sweep:{runtime.BladeSweep};rays:{runtime.MaximumSweepRays}";
}
