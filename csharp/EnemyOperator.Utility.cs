using Godot;

namespace OperationSteelTide;

public partial class EnemyOperator
{
    private int _demolitionUtilityRound = -1;
    private int _demolitionFragGrenades;
    private int _demolitionSmokeGrenades;
    private int _demolitionIncendiaryGrenades;
    private int _demolitionFlashbangGrenades;

    internal Vector3 DemolitionUtilityThrowOrigin
        => RawMuzzlePosition;
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
        target = EngageTargetNode;
        targetPosition = target?.GlobalPosition ?? Vector3.Zero;
        if (!Alerted || target is null || !IsInstanceValid(target))
        {
            return false;
        }
        targetPosition += Vector3.Up * 0.9f;
        return HasClearBallisticPath(target, targetPosition);
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
        _fireTimer = Mathf.Max(_fireTimer, 0.8f);
        return true;
    }
}
