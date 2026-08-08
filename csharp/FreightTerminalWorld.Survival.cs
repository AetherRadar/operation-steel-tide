using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private async void ValidateMedicalSystem()
    {
        DisableActorsForSurvivalDiagnostics();
        await WaitFrames(3);
        _player.Backpack.RemoveAll(item => item.Kind == LootItemKind.Medical);
        _player.GrantMedicalItemForDiagnostics(MedicalItemKind.Bandage, 2);
        _player.GrantMedicalItemForDiagnostics(MedicalItemKind.FieldMedkit, 2);
        _player.GrantMedicalItemForDiagnostics(MedicalItemKind.Adrenaline, 2);

        var wheelOpened = _hud.OpenMedicalWheel(_player) && _hud.IsMedicalWheelVisible;
        _hud.SelectMedicalWheelForDiagnostics(MedicalItemKind.FieldMedkit);
        var wheelClicked = _hud.ConfirmMedicalWheelForDiagnostics();
        var wheelChoice = _hud.TryTakeMedicalWheelConfirmation(out var selected)
            && selected == MedicalItemKind.FieldMedkit
            && !_hud.IsMedicalWheelVisible;

        _player.SetHealthForDiagnostics(24.0f);
        var medkitBefore = _player.MedicalCount(MedicalItemKind.FieldMedkit);
        var medkitStarted = _player.TryStartMedicalUse(MedicalItemKind.FieldMedkit);
        var medkitBlockedWeapon = _player.MedicalActionBlocksWeapon && _player.MedicalUseProgress < 0.05f;
        var medkitCompleted = _player.CompleteMedicalUseForDiagnostics();
        var medkitHealed = _player.Health > 90.0f
            && _player.MedicalCount(MedicalItemKind.FieldMedkit) == medkitBefore - 1;

        _player.SetHealthForDiagnostics(45.0f);
        _player.SetStaminaForDiagnostics(7.0f);
        var adrenalineBefore = _player.MedicalCount(MedicalItemKind.Adrenaline);
        var adrenalineStarted = _player.TryStartMedicalUse(MedicalItemKind.Adrenaline);
        var adrenalineCompleted = _player.CompleteMedicalUseForDiagnostics();
        var adrenalineApplied = _player.AdrenalineActive
            && _player.AdrenalineRemaining > 13.8f
            && _player.Stamina >= 99.0f
            && _player.Health > 45.0f
            && _player.MedicalCount(MedicalItemKind.Adrenaline) == adrenalineBefore - 1;

        var beforeStack = _player.MedicalCount(MedicalItemKind.Bandage);
        var stackedA = _player.TryStoreInBackpack(new LootItem
        {
            Kind = LootItemKind.Medical,
            MedicalKind = MedicalItemKind.Bandage,
            Quantity = 2,
            Grade = LootGrade.Uncommon
        });
        var stackedB = _player.TryStoreInBackpack(new LootItem
        {
            Kind = LootItemKind.Medical,
            MedicalKind = MedicalItemKind.Bandage,
            Quantity = 3,
            Grade = LootGrade.Uncommon
        });
        var stackingWorked = stackedA && stackedB
            && _player.MedicalCount(MedicalItemKind.Bandage) == beforeStack + 5;

        var cacheMedicalCount = 0;
        var allCachesCarryMedicine = true;
        var medicalKinds = new HashSet<MedicalItemKind>();
        foreach (var cache in _residentialCaches)
        {
            var cacheHasMedicine = false;
            foreach (var item in cache.Loot)
            {
                if (item.Kind != LootItemKind.Medical)
                {
                    continue;
                }
                cacheHasMedicine = true;
                cacheMedicalCount++;
                medicalKinds.Add(item.MedicalKind);
            }
            allCachesCarryMedicine &= cacheHasMedicine;
        }

        var wheelAction = InputMap.HasAction("medical_wheel");
        var wheelKeyBound = false;
        if (wheelAction)
        {
            foreach (var inputEvent in InputMap.ActionGetEvents("medical_wheel"))
            {
                if (inputEvent is InputEventKey key && key.PhysicalKeycode == Key.B)
                {
                    wheelKeyBound = true;
                    break;
                }
            }
        }

        var valid = wheelOpened
            && wheelClicked
            && wheelChoice
            && medkitStarted
            && medkitBlockedWeapon
            && medkitCompleted
            && medkitHealed
            && adrenalineStarted
            && adrenalineCompleted
            && adrenalineApplied
            && stackingWorked
            && allCachesCarryMedicine
            && medicalKinds.Count == Enum.GetValues<MedicalItemKind>().Length
            && wheelAction
            && wheelKeyBound;
        GD.Print($"MEDICAL_CHECK valid={valid} wheel_open={wheelOpened} clicked={wheelClicked} choice={wheelChoice} medkit_started={medkitStarted} hands_busy={medkitBlockedWeapon} medkit_healed={medkitHealed} adrenaline_started={adrenalineStarted} adrenaline_active={adrenalineApplied} stacking={stackingWorked} cache_medicine={cacheMedicalCount} all_caches={allCachesCarryMedicine} medicine_types={medicalKinds.Count}/3 input_action={wheelAction} key_b={wheelKeyBound}");
        GD.Print($"MEDICAL_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }

    private async void ValidateHitFeedback()
    {
        DisableActorsForSurvivalDiagnostics();
        var attacker = _enemies.Find(enemy => IsInstanceValid(enemy) && !enemy.IsDead);
        if (attacker is null)
        {
            GD.Print("HIT_FEEDBACK_CHECK valid=False reason=missing_attacker");
            GD.Print("HIT_FEEDBACK_PASS valid=False");
            GetTree().Quit(2);
            return;
        }
        _missionDirector.ExitDeploymentZone();
        _player.GlobalPosition = new Vector3(0.0f, 44.0f, -62.0f);
        _player.Rotation = Vector3.Zero;
        attacker.GlobalPosition = _player.GlobalPosition + Vector3.Right * 9.0f;
        await WaitFrames(2);

        _player.GrantMedicalItemForDiagnostics(MedicalItemKind.Bandage, 1);
        _player.SetHealthForDiagnostics(82.0f);
        var medicalStarted = _player.TryStartMedicalUse(MedicalItemKind.Bandage);
        var healthBefore = _player.Health;
        _player.TakeDamage(26.0f, _player.HitPoint(HitRegion.Torso), attacker);
        var damageApplied = _player.Health < healthBefore;
        var rightAngle = _player.IncomingDamageAngleForDiagnostics(attacker);
        var directionCorrect = Mathf.Abs(rightAngle - Mathf.Pi * 0.5f) < 0.08f
            && Mathf.Abs(_hud.LastIncomingAngle - Mathf.Pi * 0.5f) < 0.08f;
        var hudDetailed = _hud.IsIncomingDamageVisible
            && _hud.LastIncomingDamage > 0.0f
            && _hud.LastIncomingRegion == HitRegion.Torso
            && _hud.LastIncomingSource == "ENEMY OPERATOR";
        var cameraImpact = _player.DamageKickMagnitude > 0.04f;
        var medicalInterrupted = medicalStarted && !_player.MedicalActionBlocksWeapon;
        var valid = damageApplied && directionCorrect && hudDetailed && cameraImpact && medicalInterrupted;
        GD.Print($"HIT_FEEDBACK_CHECK valid={valid} damaged={damageApplied} amount={_hud.LastIncomingDamage:0.0} right_angle={rightAngle:0.000} direction={directionCorrect} hud={hudDetailed} region={_hud.LastIncomingRegion} source={_hud.LastIncomingSource.Replace(' ', '_')} camera_kick={_player.DamageKickMagnitude:0.000} medical_interrupted={medicalInterrupted}");
        GD.Print($"HIT_FEEDBACK_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }

    private async void ValidateResidentialDensity()
    {
        DisableActorsForSurvivalDiagnostics();
        await WaitFrames(3);
        var expectedFloors = 0;
        foreach (var spec in ResidentialTowerSpecs)
        {
            expectedFloors += spec.Floors;
        }
        var infillNodes = GetTree().GetNodesInGroup("residential_infill");
        var stairDetailNodes = GetTree().GetNodesInGroup("residential_stair_details");
        var uniqueNames = new HashSet<string>();
        var infillCollisionCorrect = true;
        foreach (var node in infillNodes)
        {
            if (node is not StaticBody3D body)
            {
                infillCollisionCorrect = false;
                continue;
            }
            uniqueNames.Add(body.Name);
            infillCollisionCorrect &= body.CollisionLayer == 1;
        }
        var firstTower = _residentialTowers[0];
        var firstSpec = ResidentialTowerSpecs[0];
        var entryRay = PhysicsRayQueryParameters3D.Create(
            firstTower.ToGlobal(new Vector3(0, 1.65f, firstSpec.Footprint.Y * 0.5f + 2.8f)),
            firstTower.ToGlobal(new Vector3(0, 1.65f, firstSpec.Footprint.Y * 0.5f - 2.0f)));
        entryRay.CollisionMask = 1;
        entryRay.CollideWithAreas = false;
        var entryClear = GetWorld3D().DirectSpaceState.IntersectRay(entryRay).Count == 0;
        var expectedModules = ResidentialTowerSpecs.Length * 4;
        var valid = _residentialInfillModuleCount == expectedModules
            && infillNodes.Count == expectedModules
            && uniqueNames.Count == expectedModules
            && infillCollisionCorrect
            && _residentialStairDetailCount == expectedFloors
            && stairDetailNodes.Count == expectedFloors
            && entryClear;
        GD.Print($"RESIDENTIAL_DENSITY_CHECK valid={valid} modules={_residentialInfillModuleCount}/{expectedModules} grouped={infillNodes.Count} unique={uniqueNames.Count} collision={infillCollisionCorrect} stair_details={_residentialStairDetailCount}/{expectedFloors} grouped_stairs={stairDetailNodes.Count} entry_clear={entryClear}");
        GD.Print($"RESIDENTIAL_DENSITY_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }

    private async void CaptureMedicalWheel()
    {
        DisableActorsForSurvivalDiagnostics();
        _player.ProcessMode = ProcessModeEnum.Disabled;
        _player.GrantMedicalItemForDiagnostics(MedicalItemKind.Bandage, 2);
        _player.GrantMedicalItemForDiagnostics(MedicalItemKind.FieldMedkit, 2);
        _player.GrantMedicalItemForDiagnostics(MedicalItemKind.Adrenaline, 2);
        _hud.SetLanguage("zh");
        _hud.OpenMedicalWheel(_player);
        _hud.SelectMedicalWheelForDiagnostics(MedicalItemKind.Adrenaline);
        await WaitFrames(8);
        SaveViewportImage("res://medical_wheel_validation.png");
        GD.Print($"MEDICAL_WHEEL_CAPTURE path=medical_wheel_validation.png {_hud.MedicalWheelLayoutForDiagnostics()}");
        GetTree().Quit();
    }

    private async void CaptureHitFeedback()
    {
        DisableActorsForSurvivalDiagnostics();
        var attacker = _enemies.Find(enemy => IsInstanceValid(enemy) && !enemy.IsDead);
        if (attacker is null)
        {
            GetTree().Quit(2);
            return;
        }
        _missionDirector.ExitDeploymentZone();
        _player.GlobalPosition = new Vector3(0.0f, 44.0f, -62.0f);
        _player.Rotation = Vector3.Zero;
        attacker.GlobalPosition = _player.GlobalPosition + Vector3.Left * 8.0f;
        _player.SetHealthForDiagnostics(68.0f);
        _player.TakeDamage(34.0f, _player.HitPoint(HitRegion.Torso), attacker);
        await WaitFrames(3);
        SaveViewportImage("res://hit_feedback_validation.png");
        GD.Print("HIT_FEEDBACK_CAPTURE path=hit_feedback_validation.png");
        GetTree().Quit();
    }

    private async void CaptureResidentialStairDetails()
    {
        DisableActorsForSurvivalDiagnostics();
        _hud.Visible = false;
        _player.ProcessMode = ProcessModeEnum.Disabled;
        const int towerIndex = 0;
        var tower = _residentialTowers[towerIndex];
        var spec = ResidentialTowerSpecs[towerIndex];
        var coreZ = -Mathf.Min(spec.Footprint.Y * 0.18f, 3.6f);
        var camera = new Camera3D
        {
            Name = "ResidentialStairDetailCamera",
            Fov = 74.0f,
            Near = 0.05f,
            Far = 160.0f
        };
        AddChild(camera);
        camera.GlobalPosition = tower.ToGlobal(new Vector3(-1.45f, 1.12f, coreZ + ResidentialStairRun * 0.58f));
        camera.LookAt(tower.ToGlobal(new Vector3(-1.45f, 1.86f, coreZ - ResidentialStairRun * 0.22f)), Vector3.Up);
        camera.MakeCurrent();
        await WaitFrames(28);
        SaveViewportImage("res://residential_stair_density_validation.png");
        GD.Print($"RESIDENTIAL_STAIR_CAPTURE details={_residentialStairDetailCount} infill={_residentialInfillModuleCount} path=residential_stair_density_validation.png");
        GetTree().Quit();
    }

    private void DisableActorsForSurvivalDiagnostics()
    {
        foreach (var enemy in _enemies)
        {
            if (IsInstanceValid(enemy))
            {
                enemy.ProcessMode = ProcessModeEnum.Disabled;
            }
        }
        foreach (var mate in _squadMates)
        {
            if (IsInstanceValid(mate))
            {
                mate.ProcessMode = ProcessModeEnum.Disabled;
            }
        }
        foreach (var civilian in _civilians)
        {
            if (IsInstanceValid(civilian))
            {
                civilian.ProcessMode = ProcessModeEnum.Disabled;
            }
        }
    }
}
