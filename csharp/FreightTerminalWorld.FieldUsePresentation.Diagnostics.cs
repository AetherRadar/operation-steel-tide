using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private async void ValidateFieldUsePresentation()
    {
        DisableActorsForSurvivalDiagnostics();
        ApplyTimeOfDay(DeploymentTimeOfDay.Day);
        _player.GlobalPosition = new Vector3(0.0f, 0.2f, 40.0f);
        _player.Velocity = Vector3.Zero;
        _player.FaceWorldPointForDiagnostics(new Vector3(0.0f, 0.2f, -40.0f));
        _player.GrantFireablePrimaryForDiagnostics(WeaponCatalog.Build(WeaponPlatform.AK74, 0));
        _player.Backpack.RemoveAll(item => item.Kind is LootItemKind.Medical or LootItemKind.ArmorPlate);
        _player.GrantMedicalItemForDiagnostics(MedicalItemKind.Bandage, 2);
        _player.GrantMedicalItemForDiagnostics(MedicalItemKind.FieldMedkit, 2);
        _player.GrantMedicalItemForDiagnostics(MedicalItemKind.Adrenaline, 2);
        _player.TryStoreInBackpack(new LootItem
        {
            Kind = LootItemKind.ArmorPlate,
            Quantity = 2,
            Grade = LootGrade.Rare
        });
        await WaitFrames(6);

        var asset = CombatModelLibrary.InspectFieldUseProps();
        var assetValid = asset.Loaded
            && asset.ContractValid
            && asset.MeshCount >= 12
            && asset.MaterialCount >= 5
            && asset.BoundsSize.X >= 0.12f
            && asset.BoundsSize.Y >= 0.12f
            && asset.BoundsSize.Z >= 0.05f
            && asset.BoundsSize.Length() <= 3.0f;
        var weaponInitiallyVisible = _player.FirstPersonWeaponVisibleForDiagnostics;
        var medicalValid = true;
        var medicalSamples = 0;
        var maxPrimaryResidual = 0.0f;
        var maxSupportResidual = 0.0f;
        var maxPrimaryAngle = 0.0f;
        var maxSupportAngle = 0.0f;
        foreach (var medical in new[]
        {
            (MedicalItemKind.Bandage, FirstPersonFieldUsePresentationKind.Bandage),
            (MedicalItemKind.FieldMedkit, FirstPersonFieldUsePresentationKind.FieldMedkit),
            (MedicalItemKind.Adrenaline, FirstPersonFieldUsePresentationKind.Adrenaline)
        })
        {
            _player.SetHealthForDiagnostics(20.0f);
            var started = _player.TryStartMedicalUse(medical.Item1);
            medicalValid &= started && !_player.FirstPersonWeaponVisibleForDiagnostics;
            foreach (var progress in new[] { 0.12f, 0.38f, 0.68f, 0.90f })
            {
                var posed = _player.SetMedicalUsePoseForDiagnostics(medical.Item1, progress);
                var inspection = _player.InspectFieldUsePresentationForDiagnostics();
                var correctProp = medical.Item1 switch
                {
                    MedicalItemKind.FieldMedkit => inspection.KitVisible
                        && (progress < 0.28f || inspection.GauzeVisible)
                        && !inspection.InjectorVisible
                        && !inspection.PlateVisible
                        && !inspection.CarrierVisible,
                    MedicalItemKind.Adrenaline => inspection.InjectorVisible
                        && !inspection.KitVisible
                        && !inspection.PlateVisible
                        && !inspection.CarrierVisible,
                    _ => inspection.GauzeVisible
                        && !inspection.KitVisible
                        && !inspection.InjectorVisible
                        && !inspection.PlateVisible
                        && !inspection.CarrierVisible
                };
                maxPrimaryResidual = Mathf.Max(maxPrimaryResidual, inspection.PrimaryGripResidual);
                maxSupportResidual = Mathf.Max(maxSupportResidual, inspection.SupportGripResidual);
                maxPrimaryAngle = Mathf.Max(maxPrimaryAngle, inspection.PrimaryGripAngleResidual);
                maxSupportAngle = Mathf.Max(maxSupportAngle, inspection.SupportGripAngleResidual);
                var stagedMotion = medical.Item1 != MedicalItemKind.FieldMedkit
                    || progress switch
                    {
                        < 0.28f => inspection.LidOpenAngle < 0.35f,
                        < 0.78f => inspection.LidOpenAngle > 0.80f
                            && (progress < 0.58f || inspection.GauzeTravel > 0.10f),
                        _ => inspection.LidOpenAngle < 0.35f
                            && inspection.GauzeTravel > 0.08f
                    };
                medicalValid &= posed
                    && inspection.Loaded
                    && inspection.Visible
                    && inspection.Kind == medical.Item2
                    && inspection.ArmsVisible
                    && correctProp
                    && stagedMotion
                    && inspection.PrimaryGripResidual <= 0.015f
                    && inspection.SupportGripResidual <= 0.015f
                    && inspection.PrimaryGripAngleResidual <= 0.01f
                    && inspection.SupportGripAngleResidual <= 0.01f;
                medicalSamples++;
            }
            _player.ClearFieldUsePoseForDiagnostics();
            medicalValid &= !_player.InspectFieldUsePresentationForDiagnostics().Visible;
        }

        _player.SetHealthForDiagnostics(20.0f);
        var naturalMedicalStarted = _player.TryStartMedicalUse(MedicalItemKind.FieldMedkit);
        _player.SetMedicalUsePoseForDiagnostics(MedicalItemKind.FieldMedkit, 0.90f);
        var naturalMedicalCompleted = _player.CompleteMedicalUseForDiagnostics();
        await WaitFrames(2);
        var naturalMedicalRestored = naturalMedicalStarted
            && naturalMedicalCompleted
            && !_player.InspectFieldUsePresentationForDiagnostics().Visible
            && _player.FirstPersonWeaponVisibleForDiagnostics;
        medicalValid &= naturalMedicalRestored;

        _player.SetArmorForDiagnostics(20.0f);
        var plateStarted = _player.TryStartFieldUse(FieldUseKind.ArmorPlate);
        var plateValid = plateStarted && !_player.FirstPersonWeaponVisibleForDiagnostics;
        var plateSamples = 0;
        foreach (var progress in new[] { 0.15f, 0.48f, 0.72f, 0.92f })
        {
            var posed = _player.SetPlateUsePoseForDiagnostics(progress);
            var inspection = _player.InspectFieldUsePresentationForDiagnostics();
            maxPrimaryResidual = Mathf.Max(maxPrimaryResidual, inspection.PrimaryGripResidual);
            maxSupportResidual = Mathf.Max(maxSupportResidual, inspection.SupportGripResidual);
            maxPrimaryAngle = Mathf.Max(maxPrimaryAngle, inspection.PrimaryGripAngleResidual);
            maxSupportAngle = Mathf.Max(maxSupportAngle, inspection.SupportGripAngleResidual);
            var stagedMotion = progress < 0.80f
                ? inspection.PlateTravel > 0.015f && inspection.FlapOpenAngle > 0.80f
                : inspection.PlateTravel > 0.015f && inspection.FlapOpenAngle < 0.20f;
            plateValid &= posed
                && inspection.Loaded
                && inspection.Visible
                && inspection.Kind == FirstPersonFieldUsePresentationKind.ArmorPlate
                && inspection.PlateVisible
                && inspection.CarrierVisible
                && inspection.ArmsVisible
                && !inspection.KitVisible
                && !inspection.GauzeVisible
                && !inspection.InjectorVisible
                && stagedMotion
                && inspection.PrimaryGripResidual <= 0.015f
                && inspection.SupportGripResidual <= 0.015f
                && inspection.PrimaryGripAngleResidual <= 0.01f
                && inspection.SupportGripAngleResidual <= 0.01f;
            plateSamples++;
        }
        _player.ClearFieldUsePoseForDiagnostics();
        var naturalPlateStarted = _player.TryStartFieldUse(FieldUseKind.ArmorPlate);
        _player.SetPlateUsePoseForDiagnostics(0.92f);
        var naturalPlateCompleted = _player.CompletePlateUseForDiagnostics();
        await WaitFrames(3);
        var cancelledCleanly = !_player.InspectFieldUsePresentationForDiagnostics().Visible;
        var weaponRestored = _player.FirstPersonWeaponVisibleForDiagnostics;
        var naturalPlateRestored = naturalPlateStarted
            && naturalPlateCompleted
            && cancelledCleanly
            && weaponRestored;
        plateValid &= naturalPlateRestored;
        var valid = assetValid
            && weaponInitiallyVisible
            && medicalValid
            && plateValid
            && cancelledCleanly
            && weaponRestored;
        GD.Print($"FIELD_USE_PRESENTATION_CHECK valid={valid} asset={assetValid} loaded={asset.Loaded} contract={asset.ContractValid} meshes={asset.MeshCount} materials={asset.MaterialCount} bounds={asset.BoundsSize} medical={medicalValid} medical_samples={medicalSamples} medical_complete={naturalMedicalRestored} plate={plateValid} plate_samples={plateSamples} plate_complete={naturalPlateRestored} weapon_initial={weaponInitiallyVisible} weapon_restored={weaponRestored} primary_residual={maxPrimaryResidual:0.0000} support_residual={maxSupportResidual:0.0000} primary_angle={maxPrimaryAngle:0.0000} support_angle={maxSupportAngle:0.0000}");
        GD.Print($"FIELD_USE_PRESENTATION_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }

    private async void CaptureFieldUsePresentation()
    {
        var window = GetWindow();
        window.ContentScaleMode = Window.ContentScaleModeEnum.CanvasItems;
        window.ContentScaleAspect = Window.ContentScaleAspectEnum.Ignore;
        window.Size = new Vector2I(1920, 1080);
        Input.MouseMode = Input.MouseModeEnum.Visible;
        DisableActorsForSurvivalDiagnostics();
        ApplyTimeOfDay(DeploymentTimeOfDay.Day);
        _player.GlobalPosition = new Vector3(0.0f, 0.2f, 40.0f);
        _player.Velocity = Vector3.Zero;
        _player.FaceWorldPointForDiagnostics(new Vector3(0.0f, 0.2f, -40.0f));
        _player.SetViewPitchForDiagnostics(-0.18f);
        _player.GrantFireablePrimaryForDiagnostics(WeaponCatalog.Build(WeaponPlatform.AK74, 0));
        _player.Backpack.RemoveAll(item => item.Kind is LootItemKind.Medical or LootItemKind.ArmorPlate);
        _player.GrantMedicalItemForDiagnostics(MedicalItemKind.FieldMedkit, 2);
        _player.TryStoreInBackpack(new LootItem
        {
            Kind = LootItemKind.ArmorPlate,
            Quantity = 2,
            Grade = LootGrade.Rare
        });
        _player.SetHealthForDiagnostics(20.0f);
        _player.SetArmorForDiagnostics(20.0f);
        await WaitFrames(8);
        _player.ProcessMode = ProcessModeEnum.Disabled;

        _player.TryStartMedicalUse(MedicalItemKind.FieldMedkit);
        foreach (var frame in new[]
        {
            (0.12f, "field_use_medkit_draw_validation.png"),
            (0.38f, "field_use_medkit_open_validation.png"),
            (0.68f, "field_use_medkit_apply_validation.png"),
            (0.90f, "field_use_medkit_stow_validation.png")
        })
        {
            _player.SetMedicalUsePoseForDiagnostics(MedicalItemKind.FieldMedkit, frame.Item1);
            await WaitFrames(4);
            SaveViewportImage($"res://{frame.Item2}");
        }
        _player.ClearFieldUsePoseForDiagnostics();

        _player.TryStartFieldUse(FieldUseKind.ArmorPlate);
        foreach (var frame in new[]
        {
            (0.15f, "field_use_armor_draw_validation.png"),
            (0.48f, "field_use_armor_align_validation.png"),
            (0.72f, "field_use_armor_insert_validation.png"),
            (0.92f, "field_use_armor_lock_validation.png")
        })
        {
            _player.SetPlateUsePoseForDiagnostics(frame.Item1);
            await WaitFrames(4);
            SaveViewportImage($"res://{frame.Item2}");
        }
        _player.ClearFieldUsePoseForDiagnostics();

        window.Size = new Vector2I(2048, 621);
        await WaitFrames(5);
        _player.TryStartMedicalUse(MedicalItemKind.FieldMedkit);
        _player.SetMedicalUsePoseForDiagnostics(MedicalItemKind.FieldMedkit, 0.68f);
        await WaitFrames(4);
        SaveViewportImage("res://field_use_medkit_apply_ultrawide_validation.png");
        _player.ClearFieldUsePoseForDiagnostics();
        _player.TryStartFieldUse(FieldUseKind.ArmorPlate);
        _player.SetPlateUsePoseForDiagnostics(0.72f);
        await WaitFrames(4);
        SaveViewportImage("res://field_use_armor_insert_ultrawide_validation.png");
        GD.Print("FIELD_USE_PRESENTATION_CAPTURE done");
        GetTree().Quit(0);
    }
}
