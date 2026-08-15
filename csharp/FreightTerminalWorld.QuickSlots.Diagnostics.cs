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
                DemolitionBuyCatalog.P226Id,
                string.Empty,
                false,
                1,
                1),
            5000);
        _hud.SetDemolitionGameplayPresentation(true);
        _player.ApplyDemolitionRoundLoadout(DemolitionBuyCatalog.BuildLoadout(quote), 1, 1);
        await WaitFrames(4);

        var sceneReady = _hud.QuickSlotUiReady
            && _hud.QuickSlotUsesPackedScene
            && _hud.QuickSlotIntentSignalsReady;
        var inputReady = HasQuickSlotKey("weapon_grenade", (Key)52)
            && HasQuickSlotKey("weapon_utility", (Key)53)
            && HasQuickSlotKey("weapon_primary", (Key)49)
            && HasQuickSlotKey("weapon_secondary", (Key)50)
            && HasQuickSlotKey("weapon_melee", (Key)51);
        var initialVisibility = !_hud.IsQuickSlotVisible(0)
            && _hud.IsQuickSlotVisible(1)
            && _hud.IsQuickSlotVisible(2)
            && _hud.IsQuickSlotVisible(3)
            && _hud.IsQuickSlotVisible(4)
            && _hud.VisibleQuickSlotCount == 4;

        _hud.PressQuickSlotForDiagnostics(3);
        await WaitFrames(2);
        var fragSelected = _player.ActiveQuickSlot == PlayerQuickSlot.FragmentationGrenade
            && _hud.ActiveQuickSlot == (int)PlayerQuickSlot.FragmentationGrenade;
        var fragUsed = _player.UseSelectedQuickSlotForDiagnostics();
        await WaitFrames(2);
        var fragConsumed = fragUsed
            && _player.Grenades == 0
            && _player.ActiveQuickSlot == PlayerQuickSlot.Secondary
            && !_hud.IsQuickSlotVisible(3)
            && _hud.IsQuickSlotVisible(4);

        _hud.SetLanguage("zh");
        await WaitFrames(2);
        var expectedUtilityName = GameLocalization.Get("smoke_grenade", "zh", "SMOKE");
        var localized = _hud.QuickSlotText(4).Contains(expectedUtilityName, System.StringComparison.Ordinal);
        _hud.PressQuickSlotForDiagnostics(4);
        await WaitFrames(2);
        var utilitySelected = _player.ActiveQuickSlot == PlayerQuickSlot.Utility
            && _hud.ActiveQuickSlot == (int)PlayerQuickSlot.Utility;
        var utilityUsed = _player.UseSelectedQuickSlotForDiagnostics();
        await WaitFrames(2);
        var utilityConsumed = utilityUsed
            && _player.SmokeGrenades == 0
            && _player.ActiveQuickSlot == PlayerQuickSlot.Secondary
            && !_hud.IsQuickSlotVisible(4)
            && _hud.VisibleQuickSlotCount == 2;
        var activeBeforeEmptySelection = _player.ActiveQuickSlot;
        var emptyBlocked = !_player.SelectQuickSlot(PlayerQuickSlot.Utility, false)
            && _player.ActiveQuickSlot == activeBeforeEmptySelection;

        var valid = sceneReady
            && inputReady
            && initialVisibility
            && fragSelected
            && fragConsumed
            && localized
            && utilitySelected
            && utilityConsumed
            && emptyBlocked;
        GD.Print($"QUICK_SLOTS_CHECK valid={valid} scene={sceneReady} inputs={inputReady} initial={initialVisibility} frag_selected={fragSelected} frag_consumed={fragConsumed} localized={localized} utility_selected={utilitySelected} utility_consumed={utilityConsumed} empty_blocked={emptyBlocked} visible={_hud.VisibleQuickSlotCount} active={_player.ActiveQuickSlot}");
        GD.Print($"QUICK_SLOTS_PASS valid={valid}");
        QuitDiagnosticAfterSceneCleanup(valid ? 0 : 2);
    }

    private static bool HasQuickSlotKey(string action, Key physicalKey)
    {
        return InputMap.HasAction(action)
            && InputMap.ActionGetEvents(action)
                .OfType<InputEventKey>()
                .Any(input => input.PhysicalKeycode == physicalKey);
    }
}
