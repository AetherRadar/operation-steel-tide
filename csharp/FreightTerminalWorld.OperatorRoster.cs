using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private OperatorLootSignalService? _operatorLootSignals;
    internal OperatorLootScanResult LastOperatorLootScanForDiagnostics { get; private set; }

    public void PerformLootScan(ISquadCombatant source, Vector3 origin)
    {
        _operatorLootSignals ??= new OperatorLootSignalService(_levelRoot, () => _languageSetting);
        var result = _operatorLootSignals.Reveal(_lootSources, origin);
        LastOperatorLootScanForDiagnostics = result;
        var color = OperatorRoles.Spec(OperatorRole.Scavenger).Accent;
        if (result.RevealedCount == 0)
        {
            _hud.ShowLocalizedMessage(
                "fortune_finder_empty",
                "FORTUNE FINDER  //  NO SEARCHABLE LOOT IN RANGE",
                color);
        }
        else
        {
            _hud.ShowRadioMessage(GameLocalization.Format(
                "fortune_finder_result",
                _languageSetting,
                "FORTUNE FINDER  //  {0} CACHES MARKED  //  VALUE {1}",
                result.RevealedCount,
                result.TotalValue), color);
        }
        SpawnRoleActivationPulse(origin + Vector3.Up, color, 38.0f);
    }
}
