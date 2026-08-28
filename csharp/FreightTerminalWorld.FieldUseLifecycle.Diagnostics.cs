using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private async void ValidateFieldUseLifecycle()
    {
        DisableActorsForSurvivalDiagnostics();
        ApplyTimeOfDay(DeploymentTimeOfDay.Day);
        _missionDirector.ExitDeploymentZone();
        _player.EjectFromVehicleIfAny();
        _player.ProcessMode = ProcessModeEnum.Inherit;
        _player.ConfigureRole(OperatorRole.Assault);
        _player.GlobalPosition = new Vector3(0.0f, 0.2f, 40.0f);
        _player.Velocity = Vector3.Zero;
        _player.GrantFireablePrimaryForDiagnostics(WeaponCatalog.Build(WeaponPlatform.AK74, 0));
        ResetFieldUseLifecycleSupplies();
        await WaitFrames(4);

        _player.SetHealthForDiagnostics(30.0f);
        var medicalBeforeUiLock = _player.MedicalCount(MedicalItemKind.FieldMedkit);
        var medicalStartedBeforeUiLock = _player.TryStartMedicalUse(MedicalItemKind.FieldMedkit);
        _player.SetMedicalUsePoseForDiagnostics(MedicalItemKind.FieldMedkit, 0.42f);
        _player.UiLocked = true;
        await WaitFrames(2);
        var medicalUiLockInspection = _player.InspectFieldUsePresentationForDiagnostics();
        var medicalUiLockStateCancelled = medicalStartedBeforeUiLock
            && !_player.MedicalActionBlocksWeapon
            && _player.MedicalCount(MedicalItemKind.FieldMedkit) == medicalBeforeUiLock
            && !medicalUiLockInspection.Visible;
        _player.UiLocked = false;
        await WaitFrames(2);
        var medicalUiLockCancelled = medicalUiLockStateCancelled
            && _player.FirstPersonWeaponVisibleForDiagnostics;

        _player.SetArmorForDiagnostics(20.0f);
        var platesBeforeUiLock = _player.ArmorPlates;
        var armorBeforeUiLock = _player.Armor;
        var plateStartedBeforeUiLock = _player.TryStartFieldUse(FieldUseKind.ArmorPlate);
        _player.SetPlateUsePoseForDiagnostics(0.48f);
        _player.UiLocked = true;
        await WaitFrames(2);
        var plateUiLockInspection = _player.InspectFieldUsePresentationForDiagnostics();
        var plateInactiveAfterUiLock = !_player.IsPlateUseActiveForDiagnostics;
        var plateCountPreservedAfterUiLock = _player.ArmorPlates == platesBeforeUiLock;
        var plateArmorPreservedAfterUiLock = Mathf.IsEqualApprox(_player.Armor, armorBeforeUiLock);
        var platePresentationHiddenAfterUiLock = !plateUiLockInspection.Visible;
        var plateUiLockStateCancelled = plateStartedBeforeUiLock
            && plateInactiveAfterUiLock
            && plateCountPreservedAfterUiLock
            && plateArmorPreservedAfterUiLock
            && platePresentationHiddenAfterUiLock;
        _player.UiLocked = false;
        await WaitFrames(2);
        var plateWeaponRestoredAfterUiLock = _player.FirstPersonWeaponVisibleForDiagnostics;
        var plateUiLockCancelled = plateUiLockStateCancelled
            && plateWeaponRestoredAfterUiLock;

        var platesBeforeUiRejection = _player.ArmorPlates;
        _player.UiLocked = true;
        var plateRejectedForUiLock = !_player.TryStartFieldUse(FieldUseKind.ArmorPlate)
            && !_player.IsPlateUseActiveForDiagnostics
            && _player.ArmorPlates == platesBeforeUiRejection;
        _player.UiLocked = false;

        _player.ConfigureRole(OperatorRole.Medic);
        _player.SetArmorForDiagnostics(20.0f);
        var roleActionStarted = _player.ActivateRoleAbility(broadcast: false)
            && _player.RoleActionBlocksWeapon;
        var platesBeforeRoleRejection = _player.ArmorPlates;
        var plateRejectedForRoleAction = roleActionStarted
            && !_player.TryStartFieldUse(FieldUseKind.ArmorPlate)
            && !_player.IsPlateUseActiveForDiagnostics
            && _player.ArmorPlates == platesBeforeRoleRejection;
        _player.AdvanceRoleAbilityForDiagnostics(60.0f);
        _player.ConfigureRole(OperatorRole.Assault);

        var vehicle = new DriveableVehicle
        {
            Name = "FieldUseLifecycleDiagnosticVehicle",
            Main = this,
            Position = new Vector3(0.0f, 0.05f, 55.0f)
        };
        vehicle.Configure(
            "FIELD USE LIFECYCLE VEHICLE",
            new Color(0.18f, 0.34f, 0.24f),
            maxHealth: 260.0f);
        AddChild(vehicle);
        await WaitFrames(3);

        _player.SetArmorForDiagnostics(20.0f);
        var reloadStartedBeforeVehicle = _player.SetReloadPoseForDiagnostics(0.28f)
            && _player.IsReloading;
        var enteredForReload = vehicle.TryEnter(_player);
        await WaitFrames(2);
        var reloadCancelledOnVehicleEntry = enteredForReload && !_player.IsReloading;
        var platesBeforeVehicleRejection = _player.ArmorPlates;
        var plateRejectedInVehicle = enteredForReload
            && _player.IsInVehicle
            && !_player.TryStartFieldUse(FieldUseKind.ArmorPlate)
            && !_player.IsPlateUseActiveForDiagnostics
            && _player.ArmorPlates == platesBeforeVehicleRejection;
        vehicle.ExitDriver(forced: true);
        await WaitFrames(2);

        var demolitionLoadout = DemolitionBuyCatalog.BuildLoadout(
            DemolitionBuyCatalog.Quote(DemolitionPurchaseSelection.Empty, 0));
        _player.ApplyDemolitionRoundLoadout(
            demolitionLoadout,
            grenadeCount: 1,
            smokeGrenadeCount: 1);
        _player.GrantFireablePrimaryForDiagnostics(WeaponCatalog.Build(WeaponPlatform.AK74, 0));
        var fragSelected = _player.SelectQuickSlot(PlayerQuickSlot.FragmentationGrenade, notify: false);
        await WaitFrames(2);
        var fragVisibleBeforeVehicle = fragSelected
            && _player.HeldFragmentationGrenadeVisibleForDiagnostics;
        var enteredWithFrag = vehicle.TryEnter(_player);
        await WaitFrames(2);
        var fragHiddenInVehicle = enteredWithFrag
            && !_player.HeldFragmentationGrenadeVisibleForDiagnostics
            && !_player.HeldSmokeGrenadeVisibleForDiagnostics;
        vehicle.ExitDriver(forced: true);
        await WaitFrames(2);

        var smokeSelected = _player.SelectQuickSlot(PlayerQuickSlot.Utility, notify: false);
        await WaitFrames(2);
        var smokeVisibleBeforeVehicle = smokeSelected
            && _player.HeldSmokeGrenadeVisibleForDiagnostics;
        var enteredWithSmoke = vehicle.TryEnter(_player);
        await WaitFrames(2);
        var smokeHiddenInVehicle = enteredWithSmoke
            && !_player.HeldFragmentationGrenadeVisibleForDiagnostics
            && !_player.HeldSmokeGrenadeVisibleForDiagnostics;
        vehicle.ExitDriver(forced: true);
        await WaitFrames(2);
        _player.SelectQuickSlot(PlayerQuickSlot.Primary, notify: false);

        _player.Backpack.RemoveAll(item => item.Kind == LootItemKind.Medical);
        _player.GrantMedicalItemForDiagnostics(MedicalItemKind.FieldMedkit, 1);
        _player.SetHealthForDiagnostics(24.0f);
        var activeMedicalItem = _player.Backpack.Find(item => item.Kind == LootItemKind.Medical
            && item.MedicalKind == MedicalItemKind.FieldMedkit
            && item.Quantity > 0);
        var removedMedicalStarted = activeMedicalItem is not null
            && _player.TryStartMedicalUse(MedicalItemKind.FieldMedkit);
        _player.SetMedicalUsePoseForDiagnostics(MedicalItemKind.FieldMedkit, 0.92f);
        var activeMedicalRemoved = activeMedicalItem is not null
            && _player.TryRemoveBackpackItem(activeMedicalItem.Id, out _);
        var missingMedicalCompletionAttempted = _player.CompleteMedicalUseForDiagnostics();
        await WaitFrames(2);
        var missingMedicalInspection = _player.InspectFieldUsePresentationForDiagnostics();
        var missingMedicalCleaned = removedMedicalStarted
            && activeMedicalRemoved
            && missingMedicalCompletionAttempted
            && !_player.MedicalActionBlocksWeapon
            && _player.MedicalCount(MedicalItemKind.FieldMedkit) == 0
            && !missingMedicalInspection.Visible
            && _player.FirstPersonWeaponVisibleForDiagnostics;

        _player.GrantFireablePrimaryForDiagnostics(WeaponCatalog.Build(WeaponPlatform.AK74, 0));
        var reloadStartedBeforeDown = _player.SetReloadPoseForDiagnostics(0.36f)
            && _player.IsReloading;
        _player.ApplyExtractionNetworkHealth(0.0f, down: true, reviveUsed: false);
        await WaitFrames(2);
        var reloadCancelledOnDown = reloadStartedBeforeDown
            && _player.IsDead
            && !_player.IsReloading;
        _player.ApplyExtractionNetworkHealth(_player.MaxHealth, down: false, reviveUsed: false);
        await WaitFrames(2);

        _player.Backpack.RemoveAll(item => item.Kind == LootItemKind.Medical);
        _player.GrantMedicalItemForDiagnostics(MedicalItemKind.Adrenaline, 1);
        var adrenalineStarted = _player.TryStartMedicalUse(MedicalItemKind.Adrenaline);
        var adrenalineCompleted = _player.CompleteMedicalUseForDiagnostics();
        var adrenalineWasActive = adrenalineStarted
            && adrenalineCompleted
            && _player.AdrenalineActive;
        _player.ResetForDemolitionRound(
            new Vector3(0.0f, 0.2f, 40.0f),
            OperatorRole.Assault,
            demolitionLoadout,
            grenadeCount: 0,
            smokeGrenadeCount: 0);
        await WaitFrames(2);
        var demolitionResetClearedAdrenaline = adrenalineWasActive
            && !_player.AdrenalineActive
            && _player.AdrenalineRemaining <= 0.001f
            && !_player.MedicalActionBlocksWeapon
            && !_player.IsPlateUseActiveForDiagnostics
            && !_player.IsReloading;

        _player.GrantFireablePrimaryForDiagnostics(WeaponCatalog.Build(WeaponPlatform.AK74, 0));
        _player.GrantMedicalItemForDiagnostics(MedicalItemKind.FieldMedkit, 1);
        _player.SetHealthForDiagnostics(20.0f);
        var medicalBeforeBoarding = _player.MedicalCount(MedicalItemKind.FieldMedkit);
        var medicalStartedBeforeBoarding = _player.TryStartMedicalUse(MedicalItemKind.FieldMedkit);
        _player.SetMedicalUsePoseForDiagnostics(MedicalItemKind.FieldMedkit, 0.38f);
        var reloadStartedBeforeBoarding = _player.SetReloadPoseForDiagnostics(0.28f)
            && _player.IsReloading;
        var extractionSeat = new Node3D { Name = "FieldUseLifecycleExtractionSeat" };
        AddChild(extractionSeat);
        _player.BoardExtractionSeat(extractionSeat);
        await WaitFrames(2);
        var boardingInspection = _player.InspectFieldUsePresentationForDiagnostics();
        var extractionBoardingCancelledActions = medicalStartedBeforeBoarding
            && reloadStartedBeforeBoarding
            && _player.IsExtractionPassenger
            && _player.UiLocked
            && !_player.MedicalActionBlocksWeapon
            && !_player.IsPlateUseActiveForDiagnostics
            && !_player.IsReloading
            && _player.MedicalCount(MedicalItemKind.FieldMedkit) == medicalBeforeBoarding
            && !boardingInspection.Visible
            && !_player.FirstPersonWeaponVisibleForDiagnostics;

        var uiLifecycleValid = medicalUiLockCancelled
            && plateUiLockCancelled
            && plateRejectedForUiLock;
        var rejectionValid = plateRejectedForRoleAction && plateRejectedInVehicle;
        var vehicleLifecycleValid = reloadStartedBeforeVehicle
            && reloadCancelledOnVehicleEntry
            && fragVisibleBeforeVehicle
            && fragHiddenInVehicle
            && smokeVisibleBeforeVehicle
            && smokeHiddenInVehicle;
        var reloadLifecycleValid = reloadCancelledOnVehicleEntry
            && reloadCancelledOnDown
            && extractionBoardingCancelledActions;
        var valid = uiLifecycleValid
            && rejectionValid
            && vehicleLifecycleValid
            && missingMedicalCleaned
            && reloadLifecycleValid
            && demolitionResetClearedAdrenaline
            && extractionBoardingCancelledActions;

        GD.Print($"FIELD_USE_UI_LOCK_CHECK medical_started={medicalStartedBeforeUiLock} medical_state={medicalUiLockStateCancelled} medical_weapon={medicalUiLockCancelled} plate_started={plateStartedBeforeUiLock} plate_inactive={plateInactiveAfterUiLock} plate_count={plateCountPreservedAfterUiLock} plate_armor={plateArmorPreservedAfterUiLock} plate_hidden={platePresentationHiddenAfterUiLock} plate_weapon={plateWeaponRestoredAfterUiLock}");
        GD.Print($"FIELD_USE_LIFECYCLE_CHECK valid={valid} ui_medical={medicalUiLockCancelled} ui_plate={plateUiLockCancelled} reject_ui={plateRejectedForUiLock} reject_role={plateRejectedForRoleAction} reject_vehicle={plateRejectedInVehicle} vehicle_reload={reloadCancelledOnVehicleEntry} vehicle_frag={fragVisibleBeforeVehicle}/{fragHiddenInVehicle} vehicle_smoke={smokeVisibleBeforeVehicle}/{smokeHiddenInVehicle} missing_item={missingMedicalCleaned} down_reload={reloadCancelledOnDown} demolition_adrenaline={demolitionResetClearedAdrenaline} extraction_cancel={extractionBoardingCancelledActions}");
        GD.Print($"FIELD_USE_LIFECYCLE_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }

    private void ResetFieldUseLifecycleSupplies()
    {
        _player.Backpack.RemoveAll(item => item.Kind is LootItemKind.Medical or LootItemKind.ArmorPlate);
        _player.GrantMedicalItemForDiagnostics(MedicalItemKind.FieldMedkit, 3);
        _player.GrantMedicalItemForDiagnostics(MedicalItemKind.Adrenaline, 1);
        _player.TryStoreInBackpack(new LootItem
        {
            Kind = LootItemKind.ArmorPlate,
            Quantity = 3,
            Grade = LootGrade.Rare
        });
    }
}
