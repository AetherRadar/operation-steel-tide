using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private async void ValidateSquadSustainment()
    {
        var healed = false;
        var repaired = false;
        var damageInterrupted = false;
        var threatBlocked = false;
        var armorProtected = false;
        var sourceReserved = false;
        var duplicateReservationBlocked = false;
        var bestWeaponSelected = false;
        var equipmentUpgraded = false;
        var baselineKitNotInjected = false;
        var ammoGradePreserved = false;
        var finiteAmmoDepleted = false;
        var fieldValueConserved = false;
        var recoveredSwapReturned = false;
        var suppliesRecovered = false;
        var medicalGradePreserved = false;
        var zeroQuantityRejected = false;
        var recoveredValueTracked = false;
        var deathLootPreserved = false;
        var realBodyBagPreserved = false;
        var scanBudgeted = false;
        var pureRules = false;
        var failure = string.Empty;
        SquadBodyBag? bag = null;

        try
        {
            await WaitFrames(8);
            DisableActorsForSurvivalDiagnostics();
            _missionDirector.ExitDeploymentZone();
            var mates = _squadMates
                .Where(mate => IsInstanceValid(mate) && !mate.IsHumanProxy && !mate.IsDowned)
                .OrderBy(mate => mate.SquadSlot)
                .Take(2)
                .ToArray();
            var hostile = _enemies.FirstOrDefault(enemy => IsInstanceValid(enemy) && !enemy.IsDead);
            if (mates.Length < 2 || hostile is null)
            {
                throw new InvalidOperationException("missing sustainment actors");
            }
            var mate = mates[0];
            var reserveProbe = mates[1];
            var arena = new Vector3(820.0f, 80.3f, 820.0f);
            mate.GlobalPosition = arena;
            reserveProbe.GlobalPosition = arena + Vector3.Right * 0.4f;
            hostile.GlobalPosition = arena + Vector3.Forward * 40.0f;

            mate.SetSustainmentStateForDiagnostics(24.0f, 1.0f, bandages: 1, plates: 0);
            var bandagesBefore = mate.BandagesForDiagnostics;
            var healthBefore = mate.Health;
            var healStarted = mate.AdvanceSustainmentForDiagnostics(0.05f)
                && mate.SustainmentActionForDiagnostics == SquadSustainmentActionKind.Heal;
            mate.CompleteSustainmentActionForDiagnostics();
            healed = healStarted
                && mate.Health > healthBefore + 20.0f
                && mate.BandagesForDiagnostics == bandagesBefore - 1;

            mate.SetSustainmentStateForDiagnostics(
                mate.MaxHealth,
                armorRatio: 0.12f,
                bandages: 0,
                plates: 1);
            var armorBefore = mate.ArmorRatio;
            var repairStarted = mate.AdvanceSustainmentForDiagnostics(0.05f)
                && mate.SustainmentActionForDiagnostics == SquadSustainmentActionKind.RepairArmor;
            mate.CompleteSustainmentActionForDiagnostics();
            repaired = repairStarted
                && mate.ArmorRatio > armorBefore + 0.2f
                && mate.ArmorPlatesForDiagnostics == 0;

            mate.SetSustainmentStateForDiagnostics(24.0f, 1.0f, bandages: 1, plates: 0);
            var interruptStarted = mate.AdvanceSustainmentForDiagnostics(0.05f)
                && mate.SustainmentActionForDiagnostics == SquadSustainmentActionKind.Heal;
            var bandagesAtInterrupt = mate.BandagesForDiagnostics;
            mate.TakeExplosionCombatDamage(
                4.0f,
                mate.HitPoint(HitRegion.Torso),
                this);
            damageInterrupted = interruptStarted
                && mate.SustainmentActionForDiagnostics == SquadSustainmentActionKind.None
                && mate.BandagesForDiagnostics == bandagesAtInterrupt;

            mate.SetSustainmentStateForDiagnostics(24.0f, 1.0f, bandages: 1, plates: 0);
            hostile.GlobalPosition = mate.GlobalPosition + Vector3.Forward * 6.0f;
            var threatHeldMovement = mate.AdvanceSustainmentForDiagnostics(0.05f, hostile);
            threatBlocked = !threatHeldMovement
                && mate.SustainmentActionForDiagnostics == SquadSustainmentActionKind.None
                && mate.BandagesForDiagnostics == 1;
            hostile.GlobalPosition = arena + Vector3.Forward * 40.0f;

            mate.SetSustainmentStateForDiagnostics(
                mate.MaxHealth,
                armorRatio: 1.0f,
                bandages: 0,
                plates: 0);
            var protectedHealthBefore = mate.Health;
            mate.TakeExplosionCombatDamage(
                20.0f,
                mate.HitPoint(HitRegion.Torso),
                this);
            var protectedHealthLoss = protectedHealthBefore - mate.Health;
            armorProtected = protectedHealthLoss > 0.0f
                && protectedHealthLoss < 20.0f
                && mate.ArmorRatio < 1.0f;

            mate.SetSustainmentStateForDiagnostics(
                mate.MaxHealth,
                armorRatio: 1.0f,
                bandages: 0,
                plates: 0);
            mate.SetCarriedWeaponForDiagnostics(WeaponCatalog.Build(WeaponPlatform.M3A1, 0));
            mate.GrantFireablePrimaryForDiagnostics();
            reserveProbe.SetSustainmentStateForDiagnostics(
                reserveProbe.MaxHealth,
                armorRatio: 1.0f,
                bandages: 0,
                plates: 0);
            reserveProbe.GrantFireablePrimaryForDiagnostics();

            var weakWeapon = WeaponCatalog.Build(WeaponPlatform.M3A1, 0);
            var strongWeapon = WeaponCatalog.Build(WeaponPlatform.M4A1, 0);
            pureRules = SquadSustainmentRules.IsWeaponUpgrade(
                weakWeapon,
                LootGrade.Common,
                LootGrade.Common,
                strongWeapon,
                LootGrade.Epic,
                LootGrade.Epic,
                mate.Role)
                && SquadSustainmentRules.IsEquipmentUpgrade(
                    EquipmentCatalog.Create("armor_patrol"),
                    LootGrade.Uncommon,
                    EquipmentCatalog.Create("armor_heavy"),
                    LootGrade.Epic);

            bag = new SquadBodyBag
            {
                Name = "SquadSustainmentDiagnosticBag",
                Position = arena + Vector3.Right * 1.0f
            };
            var strongCaliber = WeaponCatalog.Weapon(strongWeapon.Platform).Caliber;
            // Put a one-round premium stack before the weapon and a larger common
            // stack after it to cover grade choice and both index-shift directions.
            bag.Loot.Add(new LootItem
            {
                Kind = LootItemKind.Ammunition,
                AmmoCaliber = strongCaliber,
                Quantity = 1,
                Grade = LootGrade.Epic
            });
            bag.Loot.Add(new LootItem
            {
                Kind = LootItemKind.Weapon,
                Weapon = weakWeapon,
                Grade = LootGrade.Common
            });
            bag.Loot.Add(new LootItem
            {
                Kind = LootItemKind.Weapon,
                Weapon = strongWeapon,
                Grade = LootGrade.Rare
            });
            bag.Loot.Add(new LootItem
            {
                Kind = LootItemKind.Ammunition,
                AmmoCaliber = strongCaliber,
                Quantity = 30,
                Grade = LootGrade.Common
            });
            bag.Loot.Add(new LootItem
            {
                Kind = LootItemKind.Equipment,
                Equipment = EquipmentCatalog.Create("helmet_heavy"),
                Grade = LootGrade.Epic
            });
            bag.Loot.Add(new LootItem
            {
                Kind = LootItemKind.Equipment,
                Equipment = EquipmentCatalog.Create("armor_heavy"),
                Grade = LootGrade.Epic
            });
            bag.Loot.Add(new LootItem
            {
                Kind = LootItemKind.Equipment,
                Equipment = EquipmentCatalog.Create("pack_heavy"),
                Grade = LootGrade.Epic
            });
            bag.Loot.Add(new LootItem
            {
                Kind = LootItemKind.Medical,
                MedicalKind = MedicalItemKind.FieldMedkit,
                Quantity = 2,
                Grade = LootGrade.Rare
            });
            bag.Loot.Add(new LootItem
            {
                Kind = LootItemKind.ArmorPlate,
                Quantity = 2,
                Grade = LootGrade.Rare
            });
            bag.Loot.Add(new LootItem
            {
                Kind = LootItemKind.Medical,
                MedicalKind = MedicalItemKind.Adrenaline,
                Quantity = 0,
                Grade = LootGrade.Legendary
            });
            var fieldValueBeforeLoot = LootItem.TotalValue(bag.Loot);
            AddChild(bag);
            _lootSources.Add(bag);
            await WaitFrames(3);

            sourceReserved = TryReserveBestSquadSustainmentSource(
                    mate,
                    24.0f,
                    out var reserved)
                && ReferenceEquals(reserved, bag);
            duplicateReservationBlocked = !TryReserveBestSquadSustainmentSource(
                reserveProbe,
                24.0f,
                out _);
            reserveProbe.EquipWeaponFromLoot(
                strongWeapon,
                LootGrade.Epic,
                LootGrade.Rare,
                recoveredAmmoQuantity: 1);
            var magazineBeforeShot = reserveProbe.MagazineRemainingForDiagnostics;
            var finalPremiumShotGrade = reserveProbe.CommitFiredPrimaryRoundForDiagnostics();
            finiteAmmoDepleted = reserveProbe.RecoveredAmmoQuantityForDiagnostics == 0
                && reserveProbe.AmmoGrade == LootGrade.Common
                && finalPremiumShotGrade == LootGrade.Epic
                && reserveProbe.MagazineRemainingForDiagnostics == magazineBeforeShot - 1;
            reserveProbe.GrantFireablePrimaryForDiagnostics();
            var lootApplied = sourceReserved && TryMateTakeSustainmentLoot(mate, bag);
            ReleaseSquadSustainmentSource(mate, bag);
            bestWeaponSelected = lootApplied
                && mate.CarriedWeapon.Platform == WeaponPlatform.M4A1
                && mate.CarriedWeaponGrade == LootGrade.Rare;
            ammoGradePreserved = mate.AmmoGrade == LootGrade.Epic
                && mate.RecoveredAmmoQuantityForDiagnostics == 1
                && bag.Loot.Any(item =>
                    item.Kind == LootItemKind.Ammunition
                    && item.AmmoCaliber
                        == strongCaliber
                    && item.Grade == LootGrade.Common
                    && item.Quantity == 30);
            equipmentUpgraded = mate.EquippedHelmet.DefinitionId == "helmet_heavy"
                && mate.EquippedBodyArmor.DefinitionId == "armor_heavy"
                && mate.EquippedBackpack.DefinitionId == "pack_heavy";
            baselineKitNotInjected = bag.Loot.Count(item =>
                    item.Kind == LootItemKind.Weapon) == 1
                && !bag.Loot.Any(item => item.Kind == LootItemKind.Equipment);
            fieldValueConserved = LootItem.TotalValue(bag.Loot)
                    + mate.RecoveredSustainmentValue
                == fieldValueBeforeLoot;

            LootItem? followUpWeaponItem = null;
            var followUpUtility = 0.0f;
            foreach (var platform in Enum.GetValues<WeaponPlatform>())
            {
                if (WeaponCatalog.IsSidearm(platform))
                {
                    continue;
                }
                for (var tier = 0; tier <= 2; tier++)
                {
                    var candidate = new LootItem
                    {
                        Kind = LootItemKind.Weapon,
                        Weapon = WeaponCatalog.Build(platform, tier),
                        Grade = LootGrade.Legendary
                    };
                    var utility = mate.EvaluateSustainmentWeaponUtility(
                        candidate,
                        LootGrade.Common);
                    if (utility > followUpUtility)
                    {
                        followUpUtility = utility;
                        followUpWeaponItem = candidate;
                    }
                }
            }
            if (followUpWeaponItem?.Weapon is not null)
            {
                bag.Loot.Add(followUpWeaponItem);
                var followUpCaliber = WeaponCatalog.Weapon(
                    followUpWeaponItem.Weapon.Platform).Caliber;
                bag.Loot.Add(new LootItem
                {
                    Kind = LootItemKind.Ammunition,
                    AmmoCaliber = followUpCaliber,
                    Quantity = 24,
                    Grade = LootGrade.Rare
                });
                var swapReserved = TryReserveBestSquadSustainmentSource(
                        mate,
                        24.0f,
                        out var swapSource)
                    && ReferenceEquals(swapSource, bag);
                var swapApplied = swapReserved && TryMateTakeSustainmentLoot(mate, bag);
                ReleaseSquadSustainmentSource(mate, bag);
                recoveredSwapReturned = swapApplied
                    && mate.CarriedWeaponGrade == LootGrade.Legendary
                    && mate.AmmoGrade == LootGrade.Rare
                    && mate.RecoveredAmmoQuantityForDiagnostics == 24
                    && bag.Loot.Any(item =>
                        item.Kind == LootItemKind.Weapon
                        && item.Weapon?.Platform == WeaponPlatform.M4A1
                        && item.Grade == LootGrade.Rare)
                    && bag.Loot.Any(item =>
                        item.Kind == LootItemKind.Ammunition
                        && item.Grade == LootGrade.Epic
                        && item.Quantity == 1)
                    && bag.Loot.Any(item =>
                        item.Kind == LootItemKind.Ammunition
                        && item.Grade == LootGrade.Common
                        && item.Quantity == 30);
            }
            suppliesRecovered = mate.SustainmentSupplyCount > 0;
            recoveredValueTracked = mate.RecoveredSustainmentValue > 0;
            var preserved = new List<LootItem>();
            mate.AppendSustainmentLoot(preserved);
            medicalGradePreserved = preserved.Any(item =>
                item.Kind == LootItemKind.Medical
                && item.MedicalKind == MedicalItemKind.FieldMedkit
                && item.Grade == LootGrade.Rare
                && item.Quantity == 2);
            zeroQuantityRejected = !preserved.Any(item =>
                    item.Kind == LootItemKind.Medical
                    && item.MedicalKind == MedicalItemKind.Adrenaline)
                && bag.Loot.Any(item =>
                    item.Kind == LootItemKind.Medical
                    && item.MedicalKind == MedicalItemKind.Adrenaline
                    && item.Quantity == 0);
            deathLootPreserved = preserved.Any(item =>
                    item.Kind == LootItemKind.Weapon
                    && item.Grade == LootGrade.Legendary)
                && preserved.Count(item => item.Kind == LootItemKind.Equipment) == 3
                && preserved.Any(item => item.Kind is LootItemKind.Medical or LootItemKind.ArmorPlate)
                && preserved.Any(item =>
                    item.Kind == LootItemKind.Ammunition
                    && item.Grade == LootGrade.Rare
                    && item.Quantity == 24);

            _lootSources.Remove(bag);
            bag.QueueFree();
            bag = null;
            await WaitFrames(2);
            mate.GlobalPosition = arena;
            mate.SetSustainmentStateForDiagnostics(
                mate.MaxHealth,
                armorRatio: 1.0f,
                bandages: 0,
                plates: 0);
            var scansBefore = mate.SustainmentSourceScanCountForDiagnostics;
            for (var frame = 0; frame < 240; frame++)
            {
                mate.AdvanceSustainmentForDiagnostics(1.0f / 60.0f);
            }
            var scans = mate.SustainmentSourceScanCountForDiagnostics - scansBefore;
            scanBudgeted = scans is >= 2 and <= 4;

            var finalRecovered = new List<LootItem>();
            mate.AppendSustainmentLoot(finalRecovered);
            var expectedRecoveredValue = LootItem.TotalValue(finalRecovered);
            var actualBagPosition = mate.GlobalPosition;
            mate.ConvertToBodyBag();
            var actualBag = _lootSources
                .OfType<SquadBodyBag>()
                .FirstOrDefault(source =>
                    IsInstanceValid(source)
                    && source.GlobalPosition.DistanceTo(actualBagPosition) < 1.0f);
            if (actualBag is not null)
            {
                var recoveredBagItems = actualBag.Loot.Skip(1).ToArray();
                realBodyBagPreserved = LootItem.TotalValue(recoveredBagItems)
                        == expectedRecoveredValue
                    && recoveredBagItems.Any(item => item.Kind == LootItemKind.Weapon)
                    && recoveredBagItems.Count(item => item.Kind == LootItemKind.Equipment) == 3
                    && actualBag.Loot.Any(item =>
                        item.Kind == LootItemKind.Ammunition
                        && item.Grade == LootGrade.Common
                        && item.Quantity == 30)
                    && recoveredBagItems.Any(item =>
                        item.Kind == LootItemKind.Ammunition
                        && item.Grade == LootGrade.Rare
                        && item.Quantity == 24);
            }
        }
        catch (Exception exception)
        {
            failure = exception.GetType().Name + ":" + exception.Message;
            GD.PushError($"SQUAD_SUSTAINMENT_EXCEPTION {failure}");
        }

        if (bag is not null)
        {
            _lootSources.Remove(bag);
            bag.QueueFree();
        }
        var valid = healed
            && repaired
            && damageInterrupted
            && threatBlocked
            && armorProtected
            && sourceReserved
            && duplicateReservationBlocked
            && bestWeaponSelected
            && ammoGradePreserved
            && finiteAmmoDepleted
            && fieldValueConserved
            && equipmentUpgraded
            && baselineKitNotInjected
            && recoveredSwapReturned
            && suppliesRecovered
            && medicalGradePreserved
            && zeroQuantityRejected
            && recoveredValueTracked
            && deathLootPreserved
            && realBodyBagPreserved
            && scanBudgeted
            && pureRules
            && string.IsNullOrEmpty(failure);
        GD.Print(
            $"SQUAD_SUSTAINMENT_CHECK valid={valid} healed={healed} repaired={repaired} "
            + $"damage_interrupt={damageInterrupted} threat_block={threatBlocked} "
            + $"armor_protected={armorProtected} reserved={sourceReserved} "
            + $"duplicate_blocked={duplicateReservationBlocked} weapon_upgrade={bestWeaponSelected} "
            + $"ammo_grade={ammoGradePreserved} finite_ammo={finiteAmmoDepleted} "
            + $"value_conserved={fieldValueConserved} equipment_upgrade={equipmentUpgraded} "
            + $"baseline_not_injected={baselineKitNotInjected} "
            + $"recovered_swap={recoveredSwapReturned} "
            + $"supplies={suppliesRecovered} medical_grade={medicalGradePreserved} "
            + $"zero_quantity={zeroQuantityRejected} recovered_value={recoveredValueTracked} "
            + $"death_loot={deathLootPreserved} real_bodybag={realBodyBagPreserved} "
            + $"scan_budget={scanBudgeted} pure_rules={pureRules} "
            + $"failure={failure}");
        GD.Print($"SQUAD_SUSTAINMENT_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }
}
