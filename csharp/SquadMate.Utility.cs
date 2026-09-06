using Godot;

namespace OperationSteelTide;

public partial class SquadMate
{
    private int _demolitionUtilityRound = -1;
    private int _demolitionFragGrenades;
    private int _demolitionSmokeGrenades;
    private int _demolitionIncendiaryGrenades;
    private int _demolitionFlashbangGrenades;

    internal Vector3 DemolitionUtilityThrowOrigin
        => IsInstanceValid(_muzzle)
            ? _muzzle.GlobalPosition
            : GlobalPosition + Vector3.Up * 1.35f;
    internal int DemolitionFragGrenadesForDiagnostics => _demolitionFragGrenades;
    internal int DemolitionSmokeGrenadesForDiagnostics => _demolitionSmokeGrenades;
    internal int DemolitionIncendiaryGrenadesForDiagnostics => _demolitionIncendiaryGrenades;
    internal int DemolitionFlashbangGrenadesForDiagnostics => _demolitionFlashbangGrenades;

    internal void EnsureDemolitionUtilityInventory(
        int round,
        int teamIndex,
        int roundFunds,
        WeaponBuild? weapon)
    {
        if (_demolitionUtilityRound == round)
        {
            return;
        }
        _demolitionUtilityRound = round;
        var inventory = DemolitionBotUtilityBudgetPlanner.Plan(
            round,
            teamIndex,
            roundFunds,
            weapon);
        _demolitionFragGrenades = inventory.FragmentationGrenades;
        _demolitionSmokeGrenades = inventory.SmokeGrenades;
        _demolitionIncendiaryGrenades = inventory.IncendiaryGrenades;
        _demolitionFlashbangGrenades = inventory.FlashbangGrenades;
    }

    internal bool HasDemolitionUtility(DemolitionAiUtilityKind kind)
        => kind switch
        {
            DemolitionAiUtilityKind.Fragmentation => _demolitionFragGrenades > 0,
            DemolitionAiUtilityKind.Smoke => _demolitionSmokeGrenades > 0,
            DemolitionAiUtilityKind.Incendiary => _demolitionIncendiaryGrenades > 0,
            DemolitionAiUtilityKind.Flashbang => _demolitionFlashbangGrenades > 0,
            _ => false
        };

    internal bool TryGetVisibleDemolitionUtilityContact(
        out Node3D? target,
        out Vector3 targetPosition)
    {
        target = _combatTarget;
        targetPosition = target?.GlobalPosition ?? Vector3.Zero;
        if (!_combatHasSight
            || target is not EnemyOperator enemy
            || !IsInstanceValid(enemy)
            || enemy.IsDead)
        {
            return false;
        }
        targetPosition += Vector3.Up * 0.9f;
        return true;
    }

    internal bool ConsumeDemolitionUtility(DemolitionAiUtilityKind kind)
    {
        if (!HasDemolitionUtility(kind))
        {
            return false;
        }
        switch (kind)
        {
            case DemolitionAiUtilityKind.Fragmentation:
                _demolitionFragGrenades--;
                break;
            case DemolitionAiUtilityKind.Smoke:
                _demolitionSmokeGrenades--;
                break;
            case DemolitionAiUtilityKind.Incendiary:
                _demolitionIncendiaryGrenades--;
                break;
            case DemolitionAiUtilityKind.Flashbang:
                _demolitionFlashbangGrenades--;
                break;
        }
        _burstShotsRemaining = 0;
        _weaponCooldown = Mathf.Max(_weaponCooldown, 0.8f);
        _authoredOperatorAnimator?.PlayAction("throw", 0.78f, 1.0f);
        return true;
    }
}
