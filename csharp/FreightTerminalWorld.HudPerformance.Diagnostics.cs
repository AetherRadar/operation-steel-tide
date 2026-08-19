using System;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private const int HudPerformanceIterations = 20_000;
    private const long HudPerformanceAllocationBudget = 32_768;
    private const long HudPerformanceManagedHeapBudget = 65_536;
    private const ulong HudPerformanceTimeBudgetMicroseconds = 1_500_000;

    private async void ValidateHudPerformance()
    {
        SetProcess(false);
        _player.ProcessMode = ProcessModeEnum.Disabled;
        foreach (var enemy in _enemies)
        {
            if (IsInstanceValid(enemy))
            {
                enemy.ProcessMode = ProcessModeEnum.Disabled;
            }
        }
        foreach (var civilian in _civilians)
        {
            if (IsInstanceValid(civilian))
            {
                civilian.ProcessMode = ProcessModeEnum.Disabled;
            }
        }
        foreach (var mate in _squadMates)
        {
            if (IsInstanceValid(mate))
            {
                mate.ProcessMode = ProcessModeEnum.Disabled;
            }
        }

        _hud.SetLanguage("en");
        _player.ClearBackpackForDiagnostics();
        _player.GrantFireablePrimaryForDiagnostics();
        _player.SetAmmoGradeForDiagnostics(LootGrade.Rare, 90);
        _player.GrantMedicalItemForDiagnostics(MedicalItemKind.Bandage, 2);
        _player.GrantMedicalItemForDiagnostics(MedicalItemKind.FieldMedkit, 1);
        _player.GrantMedicalItemForDiagnostics(MedicalItemKind.Adrenaline, 1);
        _player.TryCollectArmorPlate(LootGrade.Uncommon, 2);
        await WaitFrames(2);

        var primary = _player.PrimaryWeaponForHud;
        var secondary = _player.SecondaryWeaponForHud;
        var sidearm = _player.SidearmWeaponForHud;
        var weaponSnapshot = _player.PrimaryWeaponBuild;
        var weaponName = _player.EquippedWeapon.DisplayName("en");
        var supplies = _player.CaptureFieldSupplySnapshot();
        var reserve = _player.ReserveAmmo;
        var grenades = _player.Grenades;
        var weaponReferenceStable = primary is not null
            && ReferenceEquals(primary, _player.PrimaryWeaponForHud);
        var snapshotIsolated = primary is not null
            && weaponSnapshot is not null
            && !ReferenceEquals(primary, weaponSnapshot)
            && !ReferenceEquals(primary.Attachments, weaponSnapshot.Attachments);

        for (var index = 0; index < 512; index++)
        {
            PresentStableHudFrame(primary, secondary, sidearm, weaponName, supplies, reserve, grenades);
        }
        _hud.ResetPresentationPerformanceCountersForDiagnostics();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var managedHeapBefore = GC.GetTotalMemory(forceFullCollection: false);
        var gen0Before = GC.CollectionCount(0);
        var gen1Before = GC.CollectionCount(1);
        var gen2Before = GC.CollectionCount(2);
        var allocationBefore = GC.GetAllocatedBytesForCurrentThread();
        var started = Time.GetTicksUsec();
        var checksum = 0;
        for (var index = 0; index < HudPerformanceIterations; index++)
        {
            var frameSupplies = _player.CaptureFieldSupplySnapshot();
            var frameReserve = _player.AmmoReserveFor(_player.CurrentAmmoCaliber);
            var totalReserve = _player.TotalReserveAmmo;
            PresentStableHudFrame(
                primary,
                secondary,
                sidearm,
                weaponName,
                frameSupplies,
                frameReserve,
                grenades);
            checksum += frameSupplies.ArmorPlates + frameReserve + totalReserve;
        }
        var elapsedMicroseconds = Time.GetTicksUsec() - started;
        var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocationBefore;
        var managedHeapDelta = GC.GetTotalMemory(forceFullCollection: false) - managedHeapBefore;
        var gen0Collections = GC.CollectionCount(0) - gen0Before;
        var gen1Collections = GC.CollectionCount(1) - gen1Before;
        var gen2Collections = GC.CollectionCount(2) - gen2Before;

        var stableStatsUpdates = _hud.StatsPresentationUpdateCountForDiagnostics;
        var stableEquipmentUpdates = _hud.EquipmentPresentationUpdateCountForDiagnostics;
        var stableHeadingUpdates = _hud.HeadingPresentationUpdateCountForDiagnostics;
        var stableMedicalUpdates = _hud.MedicalPresentationUpdateCountForDiagnostics;
        var stableQuickSlotUpdates = _hud.QuickSlotPresentationUpdateCountForDiagnostics;
        var stablePresentationSuppressed = stableStatsUpdates == 0
            && stableEquipmentUpdates == 0
            && stableHeadingUpdates == 0
            && stableMedicalUpdates == 0
            && stableQuickSlotUpdates == 0;

        _hud.SetStats(72.0f, 54.0f, 86.0f, 22, reserve, grenades + 1);
        _hud.SetStaminaRecoveryState(true);
        _hud.SetEquipment(
            supplies.ArmorPlates + 1,
            "SEMI",
            weaponName,
            primary,
            hasPrimary: true,
            _player.EquippedKnifeSkinId,
            secondary,
            sidearm,
            (int)PlayerQuickSlot.Melee);
        _hud.SetMedicalInventory(
            new FieldSupplySnapshot(
                supplies.Bandages + 1,
                supplies.FieldMedkits,
                supplies.Adrenaline,
                supplies.ArmorPlates),
            adrenalineActive: true,
            adrenalineRemaining: 4.2f);
        _hud.SetHeading(136.7f);

        var changedPresentationApplied = _hud.StatsPresentationUpdateCountForDiagnostics > 0
            && _hud.EquipmentPresentationUpdateCountForDiagnostics > 0
            && _hud.HeadingPresentationUpdateCountForDiagnostics > 0
            && _hud.MedicalPresentationUpdateCountForDiagnostics > 0
            && _hud.QuickSlotPresentationUpdateCountForDiagnostics > 0;
        var allocationReady = allocatedBytes <= HudPerformanceAllocationBudget;
        var managedHeapReady = managedHeapDelta <= HudPerformanceManagedHeapBudget;
        var collectionsReady = gen0Collections == 0
            && gen1Collections == 0
            && gen2Collections == 0;
        var timeReady = elapsedMicroseconds <= HudPerformanceTimeBudgetMicroseconds;
        var reserveReady = reserve == 90 && _player.TotalReserveAmmo == 90;
        var valid = weaponReferenceStable
            && snapshotIsolated
            && stablePresentationSuppressed
            && changedPresentationApplied
            && allocationReady
            && managedHeapReady
            && collectionsReady
            && timeReady
            && reserveReady
            && checksum > 0;
        GD.Print(
            $"HUD_PERFORMANCE_CHECK valid={valid} iterations={HudPerformanceIterations} "
            + $"allocated_bytes={allocatedBytes}/{HudPerformanceAllocationBudget} "
            + $"heap_delta={managedHeapDelta}/{HudPerformanceManagedHeapBudget} "
            + $"gc_collections={gen0Collections},{gen1Collections},{gen2Collections} "
            + $"elapsed_usec={elapsedMicroseconds}/{HudPerformanceTimeBudgetMicroseconds} "
            + $"stable_updates={stableStatsUpdates},{stableEquipmentUpdates},{stableHeadingUpdates},{stableMedicalUpdates},{stableQuickSlotUpdates} "
            + $"changed_applied={changedPresentationApplied} weapon_ref={weaponReferenceStable} "
            + $"snapshot_isolated={snapshotIsolated} reserve={reserve}/{_player.TotalReserveAmmo} checksum={checksum}");
        GD.Print($"HUD_PERFORMANCE_PASS valid={valid}");
        QuitDiagnosticAfterSceneCleanup(valid ? 0 : 2);
    }

    private void PresentStableHudFrame(
        WeaponBuild? primary,
        WeaponBuild? secondary,
        WeaponBuild? sidearm,
        string weaponName,
        FieldSupplySnapshot supplies,
        int reserve,
        int grenades)
    {
        _hud.SetStats(73.0f, 55.0f, 87.5f, 23, reserve, grenades);
        _hud.SetStaminaRecoveryState(false);
        _hud.SetEquipment(
            supplies.ArmorPlates,
            "AUTO",
            weaponName,
            primary,
            hasPrimary: true,
            _player.EquippedKnifeSkinId,
            secondary,
            sidearm,
            (int)PlayerQuickSlot.Primary);
        _hud.SetMedicalInventory(supplies, adrenalineActive: false, adrenalineRemaining: 0.0f);
        _hud.SetHeading(91.25f);
    }
}
