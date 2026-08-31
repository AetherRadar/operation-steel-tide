using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private async void ValidateQuickSlots()
    {
        foreach (var enemy in _enemies)
        {
            enemy.ProcessMode = ProcessModeEnum.Disabled;
        }

        var quote = DemolitionBuyCatalog.Quote(
            new DemolitionPurchaseSelection(
                DemolitionBuyCatalog.Gsh18Id,
                string.Empty,
                false,
                1,
                1,
                1),
            5000);
        _hud.SetDemolitionGameplayPresentation(true);
        _player.ApplyDemolitionRoundLoadout(DemolitionBuyCatalog.BuildLoadout(quote), 1, 1, 1);
        _player.EquipFromLoot(new LootItem
        {
            Kind = LootItemKind.Weapon,
            Weapon = WeaponCatalog.Build(WeaponPlatform.AWM, 3),
            Grade = LootGrade.Legendary
        });
        await WaitFrames(2);
        var awmSignatureParts = _player.WeaponSignaturePartCountForDiagnostics;
        _player.EquipFromLoot(new LootItem
        {
            Kind = LootItemKind.Weapon,
            Weapon = WeaponCatalog.Build(WeaponPlatform.VSS, 2),
            Grade = LootGrade.Epic
        });
        await WaitFrames(4);
        var vssSignatureParts = _player.WeaponSignaturePartCountForDiagnostics;

        var sceneReady = _hud.QuickSlotUiReady
            && _hud.QuickSlotUsesPackedScene
            && _hud.QuickSlotIntentSignalsReady;
        var inputReady = HasQuickSlotKey("weapon_primary", (Key)49)
            && HasQuickSlotKey("weapon_secondary", (Key)50)
            && HasQuickSlotKey("weapon_sidearm", (Key)51)
            && HasQuickSlotKey("weapon_melee", (Key)52)
            && HasQuickSlotKey("weapon_grenade", (Key)53)
            && HasQuickSlotKey("weapon_utility", (Key)54);
        var weaponSlotsReady = _player.HasFireablePrimary
            && _player.HasSecondaryWeapon
            && _player.HasSidearmWeapon
            && _player.PrimaryWeaponBuild?.Platform == WeaponPlatform.AWM
            && _player.SecondaryWeaponPlatform == WeaponPlatform.VSS
            && _player.SidearmWeaponPlatform == WeaponPlatform.GSh18;
        var uniqueLongGunModels = awmSignatureParts == 4
            && vssSignatureParts == 3
            && WeaponCatalog.Weapon(WeaponPlatform.AWM).BarrelLength
                != WeaponCatalog.Weapon(WeaponPlatform.VSS).BarrelLength;
        var initialVisibility = Enumerable.Range(0, 6).All(_hud.IsQuickSlotVisible)
            && _hud.VisibleQuickSlotCount == 6;

        var gsh18Selected = _player.SelectQuickSlot(PlayerQuickSlot.Sidearm, false);
        await WaitFrames(2);
        var gsh18Ready = gsh18Selected
            && _player.ActiveWeaponSlot == PlayerWeaponSlot.Sidearm
            && _player.SidearmWeaponPlatform == WeaponPlatform.GSh18
            && _player.UsesAuthoredGsh18ForDiagnostics
            && _player.UsesGsh18ReportForDiagnostics;
        SaveViewportImage("res://gsh18_first_person_validation.png");
        Input.ActionPress("aim");
        await WaitFrames(38);
        var gsh18Aiming = _player.IsAiming;
        SaveViewportImage("res://gsh18_ads_validation.png");
        Input.ActionRelease("aim");
        await WaitFrames(12);
        OpenPersonalBackpack();
        await WaitFrames(5);
        SaveViewportImage("res://gsh18_preview_validation.png");
        CloseLoot();
        await WaitFrames(3);
        _player.SelectQuickSlot(PlayerQuickSlot.Primary, false);

        _hud.PressQuickSlotForDiagnostics((int)PlayerQuickSlot.FragmentationGrenade);
        await WaitFrames(4);
        var fragSelected = _player.ActiveQuickSlot == PlayerQuickSlot.FragmentationGrenade
            && _hud.ActiveQuickSlot == (int)PlayerQuickSlot.FragmentationGrenade
            && _player.HeldFragmentationGrenadeVisibleForDiagnostics
            && _player.HeldFragmentationGrenadeMeshCountForDiagnostics >= 12;
        var fragUsed = _player.UseSelectedQuickSlotForDiagnostics();
        await WaitFrames(2);
        var fragConsumed = fragUsed
            && _player.Grenades == 0
            && _player.ActiveQuickSlot == PlayerQuickSlot.Primary
            && !_hud.IsQuickSlotVisible((int)PlayerQuickSlot.FragmentationGrenade)
            && _hud.IsQuickSlotVisible((int)PlayerQuickSlot.Utility);

        _hud.SetLanguage("zh");
        await WaitFrames(2);
        var expectedUtilityName = GameLocalization.Get("smoke_grenade", "zh", "SMOKE");
        var expectedIncendiaryName = GameLocalization.Get("incendiary_grenade", "zh", "FIRE");
        var localized = _hud.QuickSlotText((int)PlayerQuickSlot.Utility)
                .Contains(expectedUtilityName, System.StringComparison.Ordinal)
            && _hud.QuickSlotText((int)PlayerQuickSlot.Utility)
                .Contains(expectedIncendiaryName, System.StringComparison.Ordinal);
        _hud.PressQuickSlotForDiagnostics((int)PlayerQuickSlot.Utility);
        await WaitFrames(4);
        var utilitySelected = _player.ActiveQuickSlot == PlayerQuickSlot.Utility
            && _hud.ActiveQuickSlot == (int)PlayerQuickSlot.Utility
            && _player.HeldSmokeGrenadeVisibleForDiagnostics
            && _player.HeldSmokeGrenadeMeshCountForDiagnostics >= 7;
        _hud.PressQuickSlotForDiagnostics((int)PlayerQuickSlot.Utility);
        await WaitFrames(2);
        var incendiaryCycled = _player.ActiveQuickSlot == PlayerQuickSlot.Utility
            && _player.SelectedDemolitionUtility == DemolitionUtilityType.Incendiary
            && _player.HeldIncendiaryGrenadeVisibleForDiagnostics
            && _player.HeldIncendiaryGrenadeMeshCountForDiagnostics >= 5;
        _hud.PressQuickSlotForDiagnostics((int)PlayerQuickSlot.Utility);
        await WaitFrames(2);
        var smokeCycledBack = _player.SelectedDemolitionUtility == DemolitionUtilityType.Smoke
            && _player.HeldSmokeGrenadeVisibleForDiagnostics;
        var utilityUsed = _player.UseSelectedQuickSlotForDiagnostics();
        await WaitFrames(2);
        var smokeConsumed = utilityUsed
            && _player.SmokeGrenades == 0
            && _player.ActiveQuickSlot == PlayerQuickSlot.Primary
            && _hud.IsQuickSlotVisible((int)PlayerQuickSlot.Utility);
        _hud.PressQuickSlotForDiagnostics((int)PlayerQuickSlot.Utility);
        await WaitFrames(2);
        var incendiarySelected = _player.ActiveQuickSlot == PlayerQuickSlot.Utility
            && _player.SelectedDemolitionUtility == DemolitionUtilityType.Incendiary
            && _player.HeldIncendiaryGrenadeVisibleForDiagnostics;
        var incendiaryUsed = _player.UseSelectedQuickSlotForDiagnostics();
        await WaitFrames(2);
        var incendiaryConsumed = incendiaryUsed
            && _player.IncendiaryGrenades == 0
            && _player.ActiveQuickSlot == PlayerQuickSlot.Primary
            && !_hud.IsQuickSlotVisible((int)PlayerQuickSlot.Utility)
            && _hud.VisibleQuickSlotCount == 4;

        var fragProbe = new FragGrenade { Position = new Vector3(0, 40, 0) };
        AddChild(fragProbe);
        fragProbe.Arm(Vector3.Forward);
        fragProbe._PhysicsProcess(4.0);
        var fragAirborneSafe = !fragProbe.HasTouchedGround
            && !fragProbe.FuseStarted
            && !fragProbe.IsQueuedForDeletion();
        fragProbe.BeginGroundFuseForDiagnostics();
        fragProbe._PhysicsProcess(0.6);
        var fragGroundDetonated = fragProbe.HasTouchedGround && fragProbe.IsQueuedForDeletion();

        var smokeProbe = new SmokeGrenade { Position = new Vector3(2, 40, 0) };
        AddChild(smokeProbe);
        smokeProbe.Arm(Vector3.Forward);
        smokeProbe._PhysicsProcess(4.0);
        var smokeAirborneSafe = !smokeProbe.HasTouchedGround
            && !smokeProbe.FuseStarted
            && !smokeProbe.IsDeployed;
        smokeProbe.BeginGroundFuseForDiagnostics();
        smokeProbe._PhysicsProcess(0.4);
        var smokeGroundDeployed = smokeProbe.HasTouchedGround && smokeProbe.IsDeployed;

        var incendiaryProbe = new IncendiaryGrenade { Position = new Vector3(4, 40, 0) };
        AddChild(incendiaryProbe);
        incendiaryProbe.Arm(Vector3.Forward);
        incendiaryProbe._PhysicsProcess(4.0);
        var incendiaryAirborneSafe = !incendiaryProbe.HasTouchedGround
            && !incendiaryProbe.FuseStarted
            && !incendiaryProbe.IsBurning;
        incendiaryProbe.BeginGroundFuseForDiagnostics();
        incendiaryProbe._PhysicsProcess(0.5);
        var incendiaryGroundIgnited = incendiaryProbe.HasTouchedGround
            && incendiaryProbe.IsBurning
            && incendiaryProbe.ParticleEmitterCount == 1;

        _player.EquipFromLoot(new LootItem
        {
            Kind = LootItemKind.Weapon,
            Weapon = WeaponCatalog.Build(WeaponPlatform.DesertEagle, 1),
            Grade = LootGrade.Rare
        });
        await WaitFrames(3);
        var desertEagleReady = _player.ActiveWeaponSlot == PlayerWeaponSlot.Sidearm
            && _player.SidearmWeaponPlatform == WeaponPlatform.DesertEagle
            && _player.SecondaryWeaponPlatform == WeaponPlatform.VSS
            && _player.WeaponSignaturePartCountForDiagnostics == 0
            && _player.UsesAuthoredDesertEagleForDiagnostics
            && _player.UsesDesertEagleReportForDiagnostics;
        SaveViewportImage("res://desert_eagle_first_person_validation.png");
        var activeBeforeEmptySelection = _player.ActiveQuickSlot;
        var emptyBlocked = !_player.SelectQuickSlot(PlayerQuickSlot.Utility, false)
            && _player.ActiveQuickSlot == activeBeforeEmptySelection;

        OpenPersonalBackpack();
        await WaitFrames(5);
        var rackReady = _hud.IsLootVisible
            && _hud.LootWeaponRackReady
            && _hud.LootWeaponRackUsesPackedScene
            && _hud.LootVisibleWeaponSlotCount == 3
            && _hud.LootWeaponPlatformForSlot(PlayerWeaponSlot.Primary) == WeaponPlatform.AWM
            && _hud.LootWeaponPlatformForSlot(PlayerWeaponSlot.Secondary) == WeaponPlatform.VSS
            && _hud.LootWeaponPlatformForSlot(PlayerWeaponSlot.Sidearm) == WeaponPlatform.DesertEagle
            && _hud.LootEquippedGradeStylesConsistent;
        SaveViewportImage("res://desert_eagle_preview_validation.png");
        var rackChinese = _hud.LootWeaponCaptionForSlot(PlayerWeaponSlot.Secondary)
                .Contains(GameLocalization.Get("secondary_weapon", "zh", "SECONDARY WEAPON"), System.StringComparison.Ordinal)
            && _hud.LootWeaponCaptionForSlot(PlayerWeaponSlot.Sidearm)
                .Contains(GameLocalization.Get("sidearm_weapon", "zh", "SIDEARM"), System.StringComparison.Ordinal);
        _hud.SetLanguage("en");
        await WaitFrames(2);
        var rackEnglish = _hud.LootWeaponCaptionForSlot(PlayerWeaponSlot.Secondary)
                .Contains("SECONDARY WEAPON", System.StringComparison.Ordinal)
            && _hud.LootWeaponCaptionForSlot(PlayerWeaponSlot.Sidearm)
                .Contains("SIDEARM", System.StringComparison.Ordinal);
        _hud.PressLootWeaponDetailsForDiagnostics(PlayerWeaponSlot.Secondary);
        await WaitFrames(2);
        var rackDetailIntent = _hud.IsWeaponDetailVisible
            && _hud.DetailedWeaponPlatformForDiagnostics == WeaponPlatform.VSS;
        var rackValue = CombatHUD.ComputeBackpackTotalValue(_player);
        var expectedRackValue = LootItem.TotalValue(_player.Backpack)
            + WeaponValue(_player.PrimaryWeaponBuild, _player.PrimaryWeaponGrade)
            + WeaponValue(_player.SecondaryWeaponBuild, _player.SecondaryWeaponGrade)
            + WeaponValue(_player.SidearmWeaponBuild, _player.SidearmWeaponGrade)
            + EquipmentValue(_player.EquippedHelmet, _player.EquippedHelmetGrade)
            + EquipmentValue(_player.EquippedBodyArmor, _player.EquippedBodyArmorGrade)
            + EquipmentValue(_player.EquippedBackpack, _player.EquippedBackpackGrade);
        var rackValueReady = rackValue == expectedRackValue;
        CloseLoot();
        var redeployQuote = DemolitionBuyCatalog.Quote(
            new DemolitionPurchaseSelection(
                DemolitionBuyCatalog.P226Id,
                DemolitionBuyCatalog.M4A1Id,
                false,
                0,
                0),
            5000);
        _player.ApplyDeploymentLoadout(
            DemolitionBuyCatalog.BuildLoadout(redeployQuote),
            includeEmergencySupplies: false);
        var redeployClearsSecondary = _player.PrimaryWeaponBuild?.Platform == WeaponPlatform.M4A1
            && !_player.HasSecondaryWeapon
            && _player.SidearmWeaponPlatform == WeaponPlatform.P226
            && _player.ActiveWeaponSlot == PlayerWeaponSlot.Primary;

        var valid = sceneReady
            && inputReady
            && weaponSlotsReady
            && uniqueLongGunModels
            && initialVisibility
            && gsh18Ready
            && gsh18Aiming
            && fragSelected
            && fragConsumed
            && localized
            && utilitySelected
            && incendiaryCycled
            && smokeCycledBack
            && smokeConsumed
            && incendiarySelected
            && incendiaryConsumed
            && fragAirborneSafe
            && fragGroundDetonated
            && smokeAirborneSafe
            && smokeGroundDeployed
            && incendiaryAirborneSafe
            && incendiaryGroundIgnited
            && desertEagleReady
            && emptyBlocked
            && rackReady
            && rackChinese
            && rackEnglish
            && rackDetailIntent
            && rackValueReady
            && redeployClearsSecondary;
        GD.Print($"QUICK_SLOTS_CHECK valid={valid} scene={sceneReady} inputs={inputReady} weapon_slots={weaponSlotsReady} unique_models={uniqueLongGunModels} initial={initialVisibility} gsh18={gsh18Ready} frag_selected={fragSelected} frag_consumed={fragConsumed} localized={localized} utility_selected={utilitySelected} utility_cycle={incendiaryCycled && smokeCycledBack} smoke_consumed={smokeConsumed} incendiary_selected={incendiarySelected} incendiary_consumed={incendiaryConsumed} frag_air_safe={fragAirborneSafe} frag_ground={fragGroundDetonated} smoke_air_safe={smokeAirborneSafe} smoke_ground={smokeGroundDeployed} incendiary_air_safe={incendiaryAirborneSafe} incendiary_ground={incendiaryGroundIgnited} deagle={desertEagleReady} empty_blocked={emptyBlocked} rack={rackReady} rack_zh={rackChinese} rack_en={rackEnglish} rack_detail={rackDetailIntent} rack_value={rackValueReady}/{rackValue} redeploy_clear={redeployClearsSecondary} visible={_hud.VisibleQuickSlotCount} active={_player.ActiveQuickSlot}");
        GD.Print($"QUICK_SLOTS_PASS valid={valid}");
        QuitDiagnosticAfterSceneCleanup(valid ? 0 : 2);
    }

    private static bool HasQuickSlotKey(string action, Key physicalKey)
    {
        using var actionName = new StringName(action);
        if (!InputMap.HasAction(actionName))
        {
            return false;
        }
        var events = InputMap.ActionGetEvents(actionName);
        using var eventsBacking = events.AsDisposable();
        return events.OfType<InputEventKey>()
            .Any(input => input.PhysicalKeycode == physicalKey);
    }

    private static int WeaponValue(WeaponBuild? weapon, LootGrade grade)
        => weapon is null
            ? 0
            : new LootItem
            {
                Kind = LootItemKind.Weapon,
                Weapon = weapon,
                Grade = grade
            }.StackValue;

    private static int EquipmentValue(EquipmentItem equipment, LootGrade grade)
        => new LootItem
        {
            Kind = LootItemKind.Equipment,
            Equipment = equipment,
            Grade = grade
        }.StackValue;
}
