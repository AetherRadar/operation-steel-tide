using System;
using System.IO;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private OperatorProfileStore _operatorProfileStore = null!;
    private string? _diagnosticProfilePath;
    private int _deploymentBaselineValue = -1;
    private bool _deploymentPurchaseCommitted;
    private bool _extractionValueCommitted;

    private void InitializeOperatorProgression()
    {
        var args = OS.GetCmdlineUserArgs();
        var isolatedRun = Array.Exists(args, value =>
            value.StartsWith("--validate", StringComparison.Ordinal)
            || value.StartsWith("--capture", StringComparison.Ordinal));
        if (isolatedRun)
        {
            _diagnosticProfilePath = Path.Combine(
                OS.GetUserDataDir(),
                $"operator_profile_runtime_validation_{System.Environment.ProcessId}.json");
            TryDeleteProfile(_diagnosticProfilePath);
        }
        _operatorProfileStore = new OperatorProfileStore(_diagnosticProfilePath);
    }

    private void CleanupOperatorProgression()
    {
        if (!string.IsNullOrEmpty(_diagnosticProfilePath))
        {
            TryDeleteProfile(_diagnosticProfilePath);
            TryDeleteProfile(_diagnosticProfilePath + ".tmp");
        }
    }

    private bool TryCommitSelectedDeployment()
    {
        if (_deploymentPurchaseCommitted)
        {
            return true;
        }
        if (!_operatorProfileStore.TryCommitDeployment(
                _hud.SelectedDeploymentLoadout,
                out var loadout,
                out var failure))
        {
            _hud.ShowDeploymentPurchaseError(failure);
            return false;
        }

        _player.ApplyDeploymentLoadout(loadout);
        _deploymentPurchaseCommitted = true;
        _deploymentBaselineValue = CombatHUD.ComputeBackpackTotalValue(_player);
        _hud.SetOperatorProfile(_operatorProfileStore.Profile);
        return true;
    }

    private void EnsureDeploymentBaseline()
    {
        if (_deploymentBaselineValue < 0)
        {
            _deploymentBaselineValue = CombatHUD.ComputeBackpackTotalValue(_player);
        }
    }

    /// <summary>Each completed objective adds 15% to the extraction payout; no objectives, no bonus.</summary>
    private float ObjectiveExtractionMultiplier()
    {
        if (_objectiveTerminals.Count == 0)
        {
            return 1.0f;
        }
        var completed = Mathf.Clamp(_objectiveStage, 0, _objectiveTerminals.Count);
        return 1.0f + 0.15f * completed;
    }

    private (int ExtractedValue, int Wallet, bool Saved) CommitExtractionValue()
    {
        if (_extractionValueCommitted)
        {
            return (0, _operatorProfileStore.Profile.Credits, true);
        }

        EnsureDeploymentBaseline();
        var currentValue = CombatHUD.ComputeBackpackTotalValue(_player);
        var extractedValue = Math.Max(
            0,
            Mathf.RoundToInt((currentValue - _deploymentBaselineValue) * ObjectiveExtractionMultiplier()));
        var saved = _operatorProfileStore.CreditExtraction(extractedValue);
        if (saved)
        {
            _extractionValueCommitted = true;
            _hud.SetOperatorProfile(_operatorProfileStore.Profile);
        }
        return (extractedValue, _operatorProfileStore.Profile.Credits, saved);
    }

    private async void ValidateProgressionFlow()
    {
        await WaitFrames(2);
        var profilePath = Path.Combine(OS.GetUserDataDir(), "operator_profile_validation.json");
        TryDeleteProfile(profilePath);
        TryDeleteProfile(profilePath + ".tmp");

        var selection = new DeploymentLoadoutSelection("m24", "heavy", LootGrade.Rare, 60);
        var store = new OperatorProfileStore(profilePath);
        var purchase = store.TryCommitDeployment(selection, out var loadout, out _);
        var expectedCredits = OperatorProfileStore.StartingCredits - loadout.TotalCost;
        var persistedStore = new OperatorProfileStore(profilePath);
        var persisted = purchase
            && persistedStore.Profile.Credits == expectedCredits
            && persistedStore.Profile.DeploymentCount == 1
            && persistedStore.Profile.LastWeaponId == "m24"
            && persistedStore.Profile.LastAmmoQuantity == 60;

        _player.ApplyDeploymentLoadout(loadout);
        var expectedWeaponGrade = LootGrades.FromTier(DeploymentCatalog.Weapon(selection.WeaponId).BuildTier);
        var playerEquipped = _player.HasFireablePrimary
            && _player.EquippedWeapon.Platform == WeaponPlatform.M24
            && _player.EquippedWeaponGrade == expectedWeaponGrade
            && _player.CurrentAmmoGrade == LootGrade.Rare
            && _player.AmmoReserveFor(_player.CurrentAmmoCaliber, LootGrade.Rare) == loadout.ReserveAmmo;
        var squadAiArmed = _squadMates
            .Where(mate => IsInstanceValid(mate) && !mate.IsHumanProxy)
            .All(mate => mate.HasFireablePrimary);
        var rivalAiArmed = _hostileSquads
            .SelectMany(squad => squad.Members)
            .Where(member => IsInstanceValid(member) && !member.IsDead)
            .All(member => member.HasFireablePrimary);
        var ammoTiers = AmmoTiers.DamageMultiplier(LootGrade.Legendary) > AmmoTiers.DamageMultiplier(LootGrade.Common)
            && AmmoTiers.ArmorPenetration(LootGrade.Legendary) > AmmoTiers.ArmorPenetration(LootGrade.Common);
        var minimap = _hud.MinimapLandmarkCount >= 8;

        var balanceBeforeExtract = persistedStore.Profile.Credits;
        var extractSaved = persistedStore.CreditExtraction(2500);
        var extractedStore = new OperatorProfileStore(profilePath);
        extractSaved = extractSaved
            && extractedStore.Profile.Credits == balanceBeforeExtract + 2500
            && extractedStore.Profile.LifetimeExtractedValue == 2500
            && extractedStore.Profile.SuccessfulExtractions == 1;
        var expensive = new DeploymentLoadoutSelection("m24", "heavy", LootGrade.Legendary);
        var insufficientRejected = !extractedStore.TryCommitDeployment(expensive, out _, out var failure)
            && failure == "insufficient_credits";

        var valid = purchase && persisted && playerEquipped && squadAiArmed && rivalAiArmed
            && ammoTiers && minimap && extractSaved && insufficientRejected;
        GD.Print($"PROGRESSION_CHECK valid={valid} purchase={purchase} persisted={persisted} player_equipped={playerEquipped} weapon_grade={_player.EquippedWeaponGrade}/{expectedWeaponGrade} squad_ai_armed={squadAiArmed} rival_ai_armed={rivalAiArmed} ammo_tiers={ammoTiers} minimap={minimap} landmarks={_hud.MinimapLandmarkCount} extract_saved={extractSaved} insufficient_rejected={insufficientRejected}");
        GD.Print($"PROGRESSION_PASS valid={valid}");
        TryDeleteProfile(profilePath);
        TryDeleteProfile(profilePath + ".tmp");
        QuitDiagnosticAfterSceneCleanup(valid ? 0 : 2);
    }

    private async void ValidateDeploymentUi()
    {
        await WaitFrames(2);
        _hud.ApplyDeploymentPresetForDiagnostics("recruit");
        await WaitFrames(1);

        var starterSelection = _hud.SelectedDeploymentLoadout;
        var starterOffer = DeploymentCatalog.Weapon(starterSelection.WeaponId);
        var starterLoadout = DeploymentCatalog.Resolve(starterSelection);
        var starterArmor = EquipmentCatalog.Definition(starterLoadout.BodyArmorId);
        var standardArmor = EquipmentCatalog.Definition(DeploymentCatalog.ArmorKit("standard").BodyArmorId);
        var paidSmg = WeaponCatalog.Build(WeaponPlatform.MP5A5, 1).Stats();
        var starterPresetSelected = _hud.ActiveDeploymentPresetId == "recruit";
        var freeStarter = starterSelection.WeaponId == "m3a1"
            && starterSelection.ArmorId == "patrol"
            && starterSelection.AmmoGrade == LootGrade.Common
            && starterSelection.AmmoQuantity == 60
            && starterLoadout.TotalCost == 0
            && starterLoadout.ReserveAmmo == 60
            && DeploymentCatalog.AmmoCost(starterOffer, LootGrade.Common, 60) == 0;
        var weakStarter = starterLoadout.Weapon is not null
            && starterLoadout.Weapon.Platform == WeaponPlatform.M3A1
            && starterLoadout.Weapon.Stats().Damage < paidSmg.Damage
            && starterLoadout.Weapon.Stats().EffectiveRange < paidSmg.EffectiveRange
            && starterArmor.Protection < standardArmor.Protection;
        var starterUpgradesPriced = DeploymentCatalog.AmmoCost(starterOffer, LootGrade.Common, 90) > 0
            && DeploymentCatalog.AmmoCost(starterOffer, LootGrade.Uncommon, 60) > 0;
        _player.ApplyDeploymentLoadout(starterLoadout);
        var starterEquipped = _player.HasFireablePrimary
            && _player.EquippedWeapon.Platform == WeaponPlatform.M3A1
            && _player.EquippedBodyArmor.DefinitionId == "armor_patrol"
            && _player.CurrentAmmoGrade == LootGrade.Common
            && _player.ReserveAmmo == 60;
        var starterWeaponGrade = _player.EquippedWeaponGrade == LootGrades.FromTier(starterOffer.BuildTier);

        _hud.ApplyDeploymentPresetForDiagnostics("overwatch");
        await WaitFrames(1);

        var selection = _hud.SelectedDeploymentLoadout;
        var uiReady = _hud.DeploymentUiReady;
        var presetCount = _hud.DeploymentPresetCount == 5;
        var weaponCount = DeploymentCatalog.Weapons.Count >= 7;
        var armorCount = DeploymentCatalog.Armor.Count == 3;
        var ammoPackCount = _hud.DeploymentAmmoPackCount == 4;
        var presetSelected = _hud.ActiveDeploymentPresetId == "overwatch";
        var loadoutSelected = selection.WeaponId == "m24"
            && selection.ArmorId == "standard"
            && selection.AmmoGrade == LootGrade.Epic
            && selection.AmmoQuantity == 60;
        var selectedLoadout = DeploymentCatalog.Resolve(selection);
        var expectedCost = selectedLoadout.TotalCost;
        var cost = _hud.DeploymentSelectedCost == expectedCost;
        var projectedBalance = _hud.DeploymentProjectedBalance == OperatorProfileStore.StartingCredits - expectedCost;
        _player.ApplyDeploymentLoadout(selectedLoadout);
        var selectedOffer = DeploymentCatalog.Weapon(selection.WeaponId);
        var selectedWeaponGrade = _player.EquippedWeaponGrade == LootGrades.FromTier(selectedOffer.BuildTier);
        var quantityPricing = DeploymentCatalog.AmmoPrice(LootGrade.Rare, AmmoCaliber.Rifle, 30)
            < DeploymentCatalog.AmmoPrice(LootGrade.Rare, AmmoCaliber.Rifle, 60)
            && DeploymentCatalog.AmmoPrice(LootGrade.Rare, AmmoCaliber.Rifle, 60)
            < DeploymentCatalog.AmmoPrice(LootGrade.Rare, AmmoCaliber.Rifle, 90)
            && DeploymentCatalog.AmmoPrice(LootGrade.Rare, AmmoCaliber.Rifle, 90)
            < DeploymentCatalog.AmmoPrice(LootGrade.Rare, AmmoCaliber.Rifle, 180);
        var gradePricing = DeploymentCatalog.AmmoPrice(LootGrade.Common, AmmoCaliber.Rifle, 90)
            < DeploymentCatalog.AmmoPrice(LootGrade.Uncommon, AmmoCaliber.Rifle, 90)
            && DeploymentCatalog.AmmoPrice(LootGrade.Uncommon, AmmoCaliber.Rifle, 90)
            < DeploymentCatalog.AmmoPrice(LootGrade.Rare, AmmoCaliber.Rifle, 90)
            && DeploymentCatalog.AmmoPrice(LootGrade.Rare, AmmoCaliber.Rifle, 90)
            < DeploymentCatalog.AmmoPrice(LootGrade.Epic, AmmoCaliber.Rifle, 90)
            && DeploymentCatalog.AmmoPrice(LootGrade.Epic, AmmoCaliber.Rifle, 90)
            < DeploymentCatalog.AmmoPrice(LootGrade.Legendary, AmmoCaliber.Rifle, 90);
        var mapCatalog = _hud.DeploymentMapCount == 3
            && _hud.SelectedDeploymentMapId == DeploymentMapCatalog.FreightTerminalId
            && _hud.DeploymentMapAvailable;
        _hud.ApplyDeploymentMapForDiagnostics(DeploymentMapCatalog.BlackwaterRefineryId);
        var refineryMapAccepted = _hud.SelectedDeploymentMapId == DeploymentMapCatalog.BlackwaterRefineryId
            && _hud.DeploymentMapAvailable;
        _hud.ApplyDeploymentMapForDiagnostics("orbital_complex");
        var lockedMapRejected = _hud.SelectedDeploymentMapId == DeploymentMapCatalog.BlackwaterRefineryId;
        var valid = uiReady && presetCount && weaponCount && armorCount && ammoPackCount && presetSelected
            && loadoutSelected && cost && projectedBalance && quantityPricing && gradePricing
            && mapCatalog && refineryMapAccepted && lockedMapRejected && starterPresetSelected && freeStarter
            && weakStarter && starterUpgradesPriced && starterEquipped
            && starterWeaponGrade && selectedWeaponGrade;

        GD.Print($"DEPLOYMENT_UI_CHECK valid={valid} ui_ready={uiReady} preset_count={_hud.DeploymentPresetCount} weapon_count={DeploymentCatalog.Weapons.Count} armor_count={DeploymentCatalog.Armor.Count} ammo_pack_count={_hud.DeploymentAmmoPackCount} preset_selected={presetSelected} loadout_selected={loadoutSelected} quantity={selection.AmmoQuantity} quantity_pricing={quantityPricing} grade_pricing={gradePricing} weapon_grades={starterWeaponGrade}/{selectedWeaponGrade} map_count={_hud.DeploymentMapCount} selected_map={_hud.SelectedDeploymentMapId} map_available={_hud.DeploymentMapAvailable} refinery_map={refineryMapAccepted} locked_map_rejected={lockedMapRejected} starter_preset={starterPresetSelected} starter_free={freeStarter} starter_weak={weakStarter} starter_upgrades_priced={starterUpgradesPriced} starter_equipped={starterEquipped} starter_damage={starterLoadout.Weapon?.Stats().Damage:0} starter_armor={starterArmor.Protection * 100.0f:0} cost={_hud.DeploymentSelectedCost} projected_balance={_hud.DeploymentProjectedBalance}");
        GD.Print($"DEPLOYMENT_UI_PASS valid={valid}");
        QuitDiagnosticAfterSceneCleanup(valid ? 0 : 2);
    }

    private static void TryDeleteProfile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception)
        {
            GD.PushWarning($"Operator profile cleanup failed: {exception.Message}");
        }
    }
}
