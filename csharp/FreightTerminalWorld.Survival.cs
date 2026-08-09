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
        _player.Backpack.RemoveAll(item => item.Kind is LootItemKind.Medical or LootItemKind.ArmorPlate);
        _player.GrantMedicalItemForDiagnostics(MedicalItemKind.Bandage, 2);
        _player.GrantMedicalItemForDiagnostics(MedicalItemKind.FieldMedkit, 2);
        _player.GrantMedicalItemForDiagnostics(MedicalItemKind.Adrenaline, 2);
        var plateStored = _player.TryStoreInBackpack(new LootItem
        {
            Kind = LootItemKind.ArmorPlate,
            Quantity = 1,
            Grade = LootGrade.Rare
        });
        var plateStacks = 0;
        foreach (var item in _player.Backpack)
        {
            if (item.Kind == LootItemKind.ArmorPlate)
            {
                plateStacks++;
            }
        }
        var plateInBackpack = plateStored
            && plateStacks == 1
            && _player.ArmorPlates == 1
            && _player.FieldUseCount(FieldUseKind.ArmorPlate) == 1;

        var wheelOpened = _hud.OpenMedicalWheel(_player) && _hud.IsMedicalWheelVisible;
        _hud.SelectMedicalWheelForDiagnostics(MedicalItemKind.FieldMedkit);
        var wheelClicked = _hud.ConfirmMedicalWheelForDiagnostics();
        var wheelChoice = _hud.TryTakeMedicalWheelConfirmation(out var selected)
            && selected == FieldUseKind.FieldMedkit
            && !_hud.IsMedicalWheelVisible;

        var plateWheelOpened = _hud.OpenMedicalWheel(_player) && _hud.IsMedicalWheelVisible;
        _hud.SelectMedicalWheelForDiagnostics(FieldUseKind.ArmorPlate);
        var plateWheelClicked = _hud.ConfirmMedicalWheelForDiagnostics();
        var plateWheelChoice = _hud.TryTakeMedicalWheelConfirmation(out var plateSelected)
            && plateSelected == FieldUseKind.ArmorPlate
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

        _player.SetArmorForDiagnostics(20.0f);
        var armorBefore = _player.Armor;
        var platesBefore = _player.ArmorPlates;
        var plateStarted = _player.TryStartFieldUse(FieldUseKind.ArmorPlate);
        var plateHeldUntilCompletion = plateStarted
            && _player.IsPlateUseActiveForDiagnostics
            && _player.ArmorPlates == platesBefore;
        var plateCompleted = _player.CompletePlateUseForDiagnostics();
        var plateRepaired = plateCompleted
            && _player.Armor >= armorBefore + ArmorPlateSupplies.RepairFraction(LootGrade.Rare) * 100.0f - 0.1f
            && _player.ArmorPlates == platesBefore - 1;
        var emptyPlateBlocked = !_player.TryStartFieldUse(FieldUseKind.ArmorPlate);

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
            && plateWheelOpened
            && plateWheelClicked
            && plateWheelChoice
            && plateInBackpack
            && medkitStarted
            && medkitBlockedWeapon
            && medkitCompleted
            && medkitHealed
            && adrenalineStarted
            && adrenalineCompleted
            && adrenalineApplied
            && plateStarted
            && plateHeldUntilCompletion
            && plateRepaired
            && emptyPlateBlocked
            && stackingWorked
            && allCachesCarryMedicine
            && medicalKinds.Count == Enum.GetValues<MedicalItemKind>().Length
            && wheelAction
            && wheelKeyBound;
        GD.Print($"MEDICAL_CHECK valid={valid} wheel_open={wheelOpened} clicked={wheelClicked} choice={wheelChoice} plate_wheel={plateWheelOpened && plateWheelClicked && plateWheelChoice} plate_backpack={plateInBackpack} plate_started={plateStarted} plate_held={plateHeldUntilCompletion} plate_repaired={plateRepaired} plate_empty_blocked={emptyPlateBlocked} armor={armorBefore:0.0}->{_player.Armor:0.0} medkit_started={medkitStarted} hands_busy={medkitBlockedWeapon} medkit_healed={medkitHealed} adrenaline_started={adrenalineStarted} adrenaline_active={adrenalineApplied} stacking={stackingWorked} cache_medicine={cacheMedicalCount} all_caches={allCachesCarryMedicine} medicine_types={medicalKinds.Count}/3 input_action={wheelAction} key_b={wheelKeyBound}");
        GD.Print($"MEDICAL_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }

    private async void ValidateStaminaRecovery()
    {
        DisableActorsForSurvivalDiagnostics();
        _player.ProcessMode = ProcessModeEnum.Disabled;
        await WaitFrames(2);

        _player.SetStaminaForDiagnostics(2.0f);
        var initiallyReady = !_player.SprintRecoveryRequired;
        _player.AdvanceStaminaForDiagnostics(0.2f, true);
        var exhausted = _player.Stamina <= 0.01f && _player.SprintRecoveryRequired;

        _player.AdvanceStaminaForDiagnostics(0.4f, false);
        var delayLocked = _player.Stamina <= 0.01f && _player.SprintRecoveryRequired;

        _player.AdvanceStaminaForDiagnostics(0.4f, false);
        var delayCompletedWithoutRecovery = _player.Stamina <= 0.01f && _player.SprintRecoveryRequired;

        _player.AdvanceStaminaForDiagnostics(1.7f, false);
        var thresholdLocked = _player.Stamina < _player.SprintRecoveryThresholdForDiagnostics
            && _player.SprintRecoveryRequired;

        _player.AdvanceStaminaForDiagnostics(0.3f, false);
        var recovered = _player.Stamina >= _player.SprintRecoveryThresholdForDiagnostics
            && !_player.SprintRecoveryRequired;
        var staminaBeforeResume = _player.Stamina;
        _player.AdvanceStaminaForDiagnostics(0.1f, true);
        var sprintResumed = _player.Stamina < staminaBeforeResume && !_player.SprintRecoveryRequired;

        var valid = initiallyReady
            && exhausted
            && delayLocked
            && delayCompletedWithoutRecovery
            && thresholdLocked
            && recovered
            && sprintResumed;
        GD.Print($"STAMINA_CHECK valid={valid} initially_ready={initiallyReady} exhausted={exhausted} delay_locked={delayLocked} delay_no_regen={delayCompletedWithoutRecovery} threshold_locked={thresholdLocked} recovered={recovered} sprint_resumed={sprintResumed} threshold={_player.SprintRecoveryThresholdForDiagnostics:0.0} stamina={_player.Stamina:0.0}");
        GD.Print($"STAMINA_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }

    private async void ValidateLootVariety()
    {
        DisableActorsForSurvivalDiagnostics();
        await WaitFrames(4);

        var expectedKinds = Enum.GetValues<ValuableItemKind>().Length;
        var expectedGrades = Enum.GetValues<LootGrade>().Length;
        var definitionKinds = new HashSet<ValuableItemKind>();
        var definitionGrades = new HashSet<LootGrade>();
        var localizedDefinitions = true;
        var minimumValueByGrade = new int[expectedGrades];
        var maximumValueByGrade = new int[expectedGrades];
        for (var index = 0; index < minimumValueByGrade.Length; index++)
        {
            minimumValueByGrade[index] = int.MaxValue;
        }

        foreach (var definition in ValuableItems.All)
        {
            definitionKinds.Add(definition.Kind);
            definitionGrades.Add(definition.NativeGrade);
            var englishName = ValuableItems.DisplayName(definition.Kind, "en");
            var chineseName = ValuableItems.DisplayName(definition.Kind, "zh");
            localizedDefinitions &= !string.IsNullOrWhiteSpace(englishName)
                && !string.IsNullOrWhiteSpace(chineseName)
                && !string.Equals(englishName, chineseName, StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(ValuableItems.Detail(definition.Kind, "zh"));
            var item = new LootItem
            {
                Kind = LootItemKind.Valuable,
                ValuableKind = definition.Kind,
                Grade = definition.NativeGrade
            };
            var gradeIndex = (int)definition.NativeGrade;
            minimumValueByGrade[gradeIndex] = Math.Min(minimumValueByGrade[gradeIndex], item.UnitValue);
            maximumValueByGrade[gradeIndex] = Math.Max(maximumValueByGrade[gradeIndex], item.UnitValue);
        }

        var separatedGradeValues = true;
        for (var index = 1; index < expectedGrades; index++)
        {
            separatedGradeValues &= minimumValueByGrade[index] > maximumValueByGrade[index - 1];
        }

        var worldKinds = new HashSet<ValuableItemKind>();
        var worldGrades = new HashSet<LootGrade>();
        var valuableWorldPickups = 0;
        var worldVisualsReady = true;
        var nativeGradesMatch = true;
        foreach (var source in _lootSources)
        {
            foreach (var item in source.Loot)
            {
                if (item.Kind != LootItemKind.Valuable)
                {
                    continue;
                }
                valuableWorldPickups++;
                worldKinds.Add(item.ValuableKind);
                worldGrades.Add(item.Grade);
                nativeGradesMatch &= item.Grade == ValuableItems.Definition(item.ValuableKind).NativeGrade;
                worldVisualsReady &= source is GradedLootPickup pickup && pickup.VisualReady;
            }
        }

        _player.Backpack.RemoveAll(item => item.Kind == LootItemKind.Valuable);
        var stackA = _player.TryStoreInBackpack(new LootItem
        {
            Kind = LootItemKind.Valuable,
            ValuableKind = ValuableItemKind.SmartPhone,
            Quantity = 1,
            Grade = LootGrade.Uncommon
        });
        var stackB = _player.TryStoreInBackpack(new LootItem
        {
            Kind = LootItemKind.Valuable,
            ValuableKind = ValuableItemKind.SmartPhone,
            Quantity = 2,
            Grade = LootGrade.Uncommon
        });
        var matchingStacks = 0;
        var stackedQuantity = 0;
        foreach (var item in _player.Backpack)
        {
            if (item.Kind == LootItemKind.Valuable
                && item.ValuableKind == ValuableItemKind.SmartPhone
                && item.Grade == LootGrade.Uncommon)
            {
                matchingStacks++;
                stackedQuantity += item.Quantity;
            }
        }
        var stackingWorked = stackA && stackB && matchingStacks == 1 && stackedQuantity == 3;

        var valid = definitionKinds.Count == expectedKinds
            && definitionGrades.Count == expectedGrades
            && localizedDefinitions
            && separatedGradeValues
            && worldKinds.Count == expectedKinds
            && worldGrades.Count == expectedGrades
            && valuableWorldPickups >= expectedKinds
            && nativeGradesMatch
            && worldVisualsReady
            && stackingWorked;
        GD.Print($"LOOT_VARIETY_CHECK valid={valid} definitions={definitionKinds.Count}/{expectedKinds} grades={definitionGrades.Count}/{expectedGrades} localized={localizedDefinitions} value_bands={separatedGradeValues} world_kinds={worldKinds.Count}/{expectedKinds} world_grades={worldGrades.Count}/{expectedGrades} world_pickups={valuableWorldPickups} native_grades={nativeGradesMatch} visuals={worldVisualsReady} stacking={stackingWorked}");
        GD.Print($"LOOT_VARIETY_PASS valid={valid}");
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
        var stairNosingBatches = 0;
        var stringerBatches = 0;
        var fasciaEndBatches = 0;
        var fasciaSideBatches = 0;
        var landingGuardRailBatches = 0;
        var landingGuardPostBatches = 0;
        var fittedStairInstances = true;
        var obsoleteColumnDetailsAbsent = true;
        var fullHeightTreadFacesAbsent = true;
        var solidLandingGuardPanelsAbsent = true;
        var openTreadNosings = true;
        foreach (var child in firstTower.GetChildren())
        {
            var name = child.Name.ToString();
            obsoleteColumnDetailsAbsent &= !name.StartsWith("ResidentialStairRisers_", StringComparison.Ordinal)
                && !name.StartsWith("ResidentialStairLandingPosts_", StringComparison.Ordinal);
            fullHeightTreadFacesAbsent &= !name.StartsWith("ResidentialStairTreadFaces_", StringComparison.Ordinal);
            solidLandingGuardPanelsAbsent &= !name.StartsWith("ResidentialStairLandingGuardPanels_", StringComparison.Ordinal);
            if (child is not MultiMeshInstance3D batch || batch.Multimesh is null)
            {
                continue;
            }
            if (name.StartsWith("ResidentialStairNosings_", StringComparison.Ordinal))
            {
                stairNosingBatches++;
                fittedStairInstances &= batch.Multimesh.InstanceCount == ResidentialStepsPerFlight * 2;
                openTreadNosings &= batch.Multimesh.Mesh is BoxMesh box && box.Size.Y <= 0.05f;
            }
            else if (name.StartsWith("ResidentialStairStringers_", StringComparison.Ordinal))
            {
                stringerBatches++;
                fittedStairInstances &= batch.Multimesh.InstanceCount == 4;
            }
            else if (name.StartsWith("ResidentialStairLandingFasciaEnds_", StringComparison.Ordinal))
            {
                fasciaEndBatches++;
                fittedStairInstances &= batch.Multimesh.InstanceCount == 2;
            }
            else if (name.StartsWith("ResidentialStairLandingFasciaSides_", StringComparison.Ordinal))
            {
                fasciaSideBatches++;
                fittedStairInstances &= batch.Multimesh.InstanceCount == 2;
            }
            else if (name.StartsWith("ResidentialStairLandingGuardRails_", StringComparison.Ordinal))
            {
                landingGuardRailBatches++;
                fittedStairInstances &= batch.Multimesh.InstanceCount == 2;
            }
            else if (name.StartsWith("ResidentialStairLandingGuardPosts_", StringComparison.Ordinal))
            {
                landingGuardPostBatches++;
                fittedStairInstances &= batch.Multimesh.InstanceCount == 7;
            }
        }
        var fittedStairStructure = stairNosingBatches == firstSpec.Floors
            && stringerBatches == firstSpec.Floors
            && fasciaEndBatches == firstSpec.Floors
            && fasciaSideBatches == firstSpec.Floors
            && landingGuardRailBatches == firstSpec.Floors
            && landingGuardPostBatches == firstSpec.Floors
            && fittedStairInstances
            && obsoleteColumnDetailsAbsent
            && fullHeightTreadFacesAbsent
            && solidLandingGuardPanelsAbsent
            && openTreadNosings;
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
            && fittedStairStructure
            && entryClear;
        GD.Print($"RESIDENTIAL_DENSITY_CHECK valid={valid} modules={_residentialInfillModuleCount}/{expectedModules} grouped={infillNodes.Count} unique={uniqueNames.Count} collision={infillCollisionCorrect} stair_details={_residentialStairDetailCount}/{expectedFloors} grouped_stairs={stairDetailNodes.Count} nosings={stairNosingBatches}/{firstSpec.Floors} open_treads={openTreadNosings} old_face_panels_absent={fullHeightTreadFacesAbsent} stringers={stringerBatches}/{firstSpec.Floors} fascia={fasciaEndBatches}+{fasciaSideBatches}/{firstSpec.Floors * 2} guard_rails={landingGuardRailBatches}/{firstSpec.Floors} guard_posts={landingGuardPostBatches}/{firstSpec.Floors} solid_guard_panels_absent={solidLandingGuardPanelsAbsent} old_columns_absent={obsoleteColumnDetailsAbsent} fitted_stairs={fittedStairStructure} entry_clear={entryClear}");
        GD.Print($"RESIDENTIAL_DENSITY_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }

    private async void CaptureMedicalWheel()
    {
        DisableActorsForSurvivalDiagnostics();
        _player.ProcessMode = ProcessModeEnum.Disabled;
        _player.Backpack.RemoveAll(item => item.Kind is LootItemKind.Medical or LootItemKind.ArmorPlate);
        _player.GrantMedicalItemForDiagnostics(MedicalItemKind.Bandage, 2);
        _player.GrantMedicalItemForDiagnostics(MedicalItemKind.FieldMedkit, 2);
        _player.GrantMedicalItemForDiagnostics(MedicalItemKind.Adrenaline, 2);
        _player.TryStoreInBackpack(new LootItem { Kind = LootItemKind.ArmorPlate, Quantity = 1, Grade = LootGrade.Epic });
        _hud.SetLanguage("zh");
        _hud.OpenMedicalWheel(_player);
        _hud.SelectMedicalWheelForDiagnostics(FieldUseKind.ArmorPlate);
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
