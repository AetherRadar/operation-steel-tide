using Godot;

namespace OperationSteelTide;

public partial class CombatHUD
{
    private const string DemolitionBuyViewScenePath = "res://ui/DemolitionBuyView.tscn";
    private DemolitionBuyView _demolitionBuyView = null!;
    private bool _demolitionGameplayPresentation;
    private int _demolitionSmokeGrenades;

    public bool IsDemolitionBuyVisible
        => IsInstanceValid(_demolitionBuyView) && _demolitionBuyView.Visible;
    public bool DemolitionBuyUiReady
        => IsInstanceValid(_demolitionBuyView) && _demolitionBuyView.UiReady;
    public bool DemolitionBuyUsesPackedScene
        => IsInstanceValid(_demolitionBuyView)
        && _demolitionBuyView.SceneFilePath == DemolitionBuyViewScenePath;
    public bool DemolitionBuyIntentSignalsReady
        => IsInstanceValid(_demolitionBuyView) && _demolitionBuyView.IntentSignalsConnected;
    public bool DemolitionBuyLanguageReady
        => IsInstanceValid(_demolitionBuyView) && _demolitionBuyView.LanguageMatches(_language);
    public DemolitionPurchaseSelection DemolitionBuySelection
        => IsInstanceValid(_demolitionBuyView)
            ? _demolitionBuyView.CurrentSelection
            : DemolitionPurchaseSelection.Empty;
    public DemolitionPurchaseQuote DemolitionBuyQuote
        => IsInstanceValid(_demolitionBuyView)
            ? _demolitionBuyView.CurrentQuote
            : DemolitionBuyCatalog.Quote(DemolitionPurchaseSelection.Empty, 0);
    public int DemolitionBuyDisplayedFunds
        => IsInstanceValid(_demolitionBuyView) ? _demolitionBuyView.DisplayedFunds : 0;
    public float DemolitionBuyDisplayedSeconds
        => IsInstanceValid(_demolitionBuyView) ? _demolitionBuyView.DisplayedSeconds : 0.0f;
    public bool IsDemolitionSidearmOfferEnabled(string id)
        => IsInstanceValid(_demolitionBuyView) && _demolitionBuyView.IsSidearmOfferEnabled(id);
    public bool IsDemolitionPrimaryOfferEnabled(string id)
        => IsInstanceValid(_demolitionBuyView) && _demolitionBuyView.IsPrimaryOfferEnabled(id);
    public bool IsDemolitionSquadRosterHidden
        => _demolitionGameplayPresentation && IsInstanceValid(_squadRoster) && !_squadRoster.Visible;
    public bool IsDemolitionSkillHudVisible
        => _demolitionGameplayPresentation && IsInstanceValid(_classSkillRoot) && _classSkillRoot.Visible;
    public bool AreDemolitionSquadOrdersHidden
        => _demolitionGameplayPresentation
        && IsInstanceValid(_squadOrderLabel)
        && !_squadOrderLabel.Visible
        && System.Array.TrueForAll(_orderButtons, button => IsInstanceValid(button) && !button.Visible);
    public string DemolitionUtilityHudText
        => IsInstanceValid(_quickSlotBar)
            ? $"4 {_quickSlotBar.SlotText(3)}  //  5 {_quickSlotBar.SlotText(4)}"
            : string.Empty;
    public bool QuickSlotUiReady
        => IsInstanceValid(_quickSlotBar) && _quickSlotBar.UiReady;
    public bool QuickSlotUsesPackedScene
        => IsInstanceValid(_quickSlotBar)
        && _quickSlotBar.SceneFilePath == "res://ui/QuickSlotBarView.tscn";
    public bool QuickSlotIntentSignalsReady
        => IsInstanceValid(_quickSlotBar) && _quickSlotBar.IntentSignalsConnected;
    public int VisibleQuickSlotCount
        => IsInstanceValid(_quickSlotBar) ? _quickSlotBar.VisibleSlotCount : 0;
    public int ActiveQuickSlot
        => IsInstanceValid(_quickSlotBar) ? _quickSlotBar.ActiveSlot : -1;
    public bool IsQuickSlotVisible(int slot)
        => IsInstanceValid(_quickSlotBar) && _quickSlotBar.IsSlotVisible(slot);
    public string QuickSlotText(int slot)
        => IsInstanceValid(_quickSlotBar) ? _quickSlotBar.SlotText(slot) : string.Empty;

    private void BuildDemolitionBuyHud(Control root)
    {
        var scene = GD.Load<PackedScene>(DemolitionBuyViewScenePath)
            ?? throw new System.InvalidOperationException($"Unable to load {DemolitionBuyViewScenePath}");
        _demolitionBuyView = scene.Instantiate<DemolitionBuyView>();
        root.AddChild(_demolitionBuyView);
        _demolitionBuyView.PurchaseRequested += (
            sidearmId,
            primaryId,
            armorSelected,
            grenadeCount,
            smokeGrenadeCount) =>
            EmitSignal(
                SignalName.DemolitionPurchaseRequested,
                sidearmId,
                primaryId,
                armorSelected,
                grenadeCount,
                smokeGrenadeCount);
    }

    public void SetDemolitionGameplayPresentation(bool active)
    {
        _demolitionGameplayPresentation = active;
        RefreshQuickSlotBar();
        if (IsInstanceValid(_squadRoster))
        {
            _squadRoster.Visible = !active;
        }
        if (IsInstanceValid(_classSkillRoot))
        {
            _classSkillRoot.Visible = true;
            RefreshFooterLayout();
        }
        if (IsInstanceValid(_squadOrderLabel))
        {
            _squadOrderLabel.Visible = !active;
        }
        foreach (var button in _orderButtons)
        {
            if (IsInstanceValid(button))
            {
                button.Visible = !active;
            }
        }
    }

    public void SetDemolitionSmokeGrenades(int count)
    {
        _demolitionSmokeGrenades = Mathf.Max(0, count);
        RefreshQuickSlotBar();
    }

    public void PressQuickSlotForDiagnostics(int slot)
        => _quickSlotBar.PressSlotForDiagnostics(slot);

    public void ShowDemolitionBuy(DemolitionBuySnapshot snapshot)
    {
        HideOperationsMenus();
        _gameplayHudRoot.Visible = true;
        _stateOverlay.Visible = false;
        _demolitionBuyView.SetLanguage(_language);
        _demolitionBuyView.BeginRound(snapshot);
    }

    public void UpdateDemolitionBuy(DemolitionBuySnapshot snapshot)
    {
        if (IsInstanceValid(_demolitionBuyView))
        {
            _demolitionBuyView.SetSnapshot(snapshot);
        }
    }

    public void HideDemolitionBuy()
    {
        if (IsInstanceValid(_demolitionBuyView))
        {
            _demolitionBuyView.Visible = false;
        }
    }

    public void SubmitDemolitionBuyTimeout()
    {
        if (IsInstanceValid(_demolitionBuyView))
        {
            _demolitionBuyView.SubmitTimeoutSelection();
        }
    }

    public void SelectDemolitionBuySidearmForDiagnostics(string id)
        => _demolitionBuyView.SelectSidearmForDiagnostics(id);

    public void SelectDemolitionBuyPrimaryForDiagnostics(string id)
        => _demolitionBuyView.SelectPrimaryForDiagnostics(id);

    public void SetDemolitionBuyArmorForDiagnostics(bool selected)
        => _demolitionBuyView.SetArmorForDiagnostics(selected);

    public void SetDemolitionBuyGrenadesForDiagnostics(int count)
        => _demolitionBuyView.SetGrenadesForDiagnostics(count);

    public void SetDemolitionBuySmokeGrenadesForDiagnostics(int count)
        => _demolitionBuyView.SetSmokeGrenadesForDiagnostics(count);

    public void PressDemolitionBuyConfirmForDiagnostics()
        => _demolitionBuyView.PressConfirmForDiagnostics();

    private void RefreshDemolitionBuyLanguage()
    {
        if (IsInstanceValid(_demolitionBuyView))
        {
            _demolitionBuyView.SetLanguage(_language);
        }
    }
}
