using Godot;

namespace OperationSteelTide;

public partial class CombatHUD
{
    private const string DemolitionBuyViewScenePath = "res://ui/DemolitionBuyView.tscn";
    private const string DemolitionRoundResultViewScenePath = "res://ui/DemolitionRoundResultView.tscn";
    private const string DemolitionTeamStatusViewScenePath = "res://ui/DemolitionTeamStatusView.tscn";
    private DemolitionBuyView _demolitionBuyView = null!;
    private DemolitionRoundResultView _demolitionRoundResultView = null!;
    private DemolitionTeamStatusView _demolitionTeamStatusView = null!;
    private bool _demolitionGameplayPresentation;
    private int _demolitionSmokeGrenades;
    private int _demolitionIncendiaryGrenades;
    private int _demolitionFlashbangGrenades;
    private DemolitionUtilityType _demolitionSelectedUtility = DemolitionUtilityType.Smoke;
    private int _radioMessageDiagnosticSuppressionDepth;

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
    public bool IsDemolitionRoundResultVisible
        => IsInstanceValid(_demolitionRoundResultView) && _demolitionRoundResultView.Visible;
    public bool DemolitionRoundResultUiReady
        => IsInstanceValid(_demolitionRoundResultView) && _demolitionRoundResultView.UiReady;
    public bool DemolitionRoundResultUsesPackedScene
        => IsInstanceValid(_demolitionRoundResultView)
        && _demolitionRoundResultView.SceneFilePath == DemolitionRoundResultViewScenePath;
    public bool DemolitionRoundResultLanguageReady
        => IsInstanceValid(_demolitionRoundResultView)
        && _demolitionRoundResultView.LanguageMatches(_language);
    public bool DemolitionRoundResultVictory
        => IsInstanceValid(_demolitionRoundResultView) && _demolitionRoundResultView.Victory;
    public bool IsDemolitionTeamStatusVisible
        => IsInstanceValid(_demolitionTeamStatusView) && _demolitionTeamStatusView.Visible;
    public bool DemolitionTeamStatusUiReady
        => IsInstanceValid(_demolitionTeamStatusView) && _demolitionTeamStatusView.UiReady;
    public bool DemolitionTeamStatusUsesPackedScene
        => IsInstanceValid(_demolitionTeamStatusView)
        && _demolitionTeamStatusView.SceneFilePath == DemolitionTeamStatusViewScenePath;
    public bool DemolitionTeamStatusLanguageReady
        => IsInstanceValid(_demolitionTeamStatusView)
        && _demolitionTeamStatusView.LanguageMatches(_language);
    public int DemolitionFriendlyStatusCount
        => IsInstanceValid(_demolitionTeamStatusView)
            ? _demolitionTeamStatusView.FriendlyCount
            : 0;
    public int DemolitionEnemyStatusCount
        => IsInstanceValid(_demolitionTeamStatusView)
            ? _demolitionTeamStatusView.EnemyCount
            : 0;
    public int DemolitionLocalPlayerMarkerCount
        => IsInstanceValid(_demolitionTeamStatusView)
            ? _demolitionTeamStatusView.LocalPlayerMarkerCount
            : 0;
    public int DemolitionDeviceMarkerCount
        => IsInstanceValid(_demolitionTeamStatusView)
            ? _demolitionTeamStatusView.DeviceMarkerCount
            : 0;
    public int DemolitionOutStatusCount
        => IsInstanceValid(_demolitionTeamStatusView)
            ? _demolitionTeamStatusView.OutCount
            : 0;
    public string DemolitionTeamStatusScoreText
        => IsInstanceValid(_demolitionTeamStatusView)
            ? _demolitionTeamStatusView.ScoreText
            : string.Empty;
    public string DemolitionTeamStatusTimerText
        => IsInstanceValid(_demolitionTeamStatusView)
            ? _demolitionTeamStatusView.TimerText
            : string.Empty;
    public string DemolitionTeamStatusPhaseText
        => IsInstanceValid(_demolitionTeamStatusView)
            ? _demolitionTeamStatusView.PhaseText
            : string.Empty;
    public string DemolitionRoundResultTitle
        => IsInstanceValid(_demolitionRoundResultView)
            ? _demolitionRoundResultView.TitleText
            : string.Empty;
    public string DemolitionRoundResultScore
        => IsInstanceValid(_demolitionRoundResultView)
            ? _demolitionRoundResultView.ScoreText
            : string.Empty;
    public float DemolitionRoundResultSeconds
        => IsInstanceValid(_demolitionRoundResultView)
            ? _demolitionRoundResultView.DisplayedSeconds
            : 0.0f;
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
        && System.Array.TrueForAll(_orderButtons, button => IsInstanceValid(button) && !button.Visible)
        && IsInstanceValid(_fireStanceButton)
        && !_fireStanceButton.Visible;
    public bool AreDemolitionLegacyTopLabelsHidden
        => _demolitionGameplayPresentation
        && IsInstanceValid(_objectiveLabel) && !_objectiveLabel.Visible
        && IsInstanceValid(_enemiesLabel) && !_enemiesLabel.Visible
        && IsInstanceValid(_phaseLabel) && !_phaseLabel.Visible
        && IsInstanceValid(_compassLabel) && !_compassLabel.Visible
        && IsInstanceValid(_alertLabel) && !_alertLabel.Visible;
    public string DemolitionUtilityHudText
        => IsInstanceValid(_quickSlotBar)
            ? $"5 {_quickSlotBar.SlotText(4)}  //  6 {_quickSlotBar.SlotText(5)}"
            : string.Empty;
    public bool QuickSlotUiReady
        => IsInstanceValid(_quickSlotBar) && _quickSlotBar.UiReady;
    public bool QuickSlotUsesPackedScene
        => IsInstanceValid(_quickSlotBar)
        && _quickSlotBar.SceneFilePath == "res://ui/QuickSlotBarView.tscn";
    public bool QuickSlotIntentSignalsReady
        => IsInstanceValid(_quickSlotBar) && _quickSlotBar.IntentSignalsConnected;
    internal bool QuickSlotsWithinViewportForDiagnostics
        => IsInstanceValid(_quickSlotBar)
        && _quickSlotBar.VisibleSlotsWithinViewportForDiagnostics;
    public int VisibleQuickSlotCount
        => IsInstanceValid(_quickSlotBar) ? _quickSlotBar.VisibleSlotCount : 0;
    public int ActiveQuickSlot
        => IsInstanceValid(_quickSlotBar) ? _quickSlotBar.ActiveSlot : -1;
    public bool IsQuickSlotVisible(int slot)
        => IsInstanceValid(_quickSlotBar) && _quickSlotBar.IsSlotVisible(slot);
    public string QuickSlotText(int slot)
        => IsInstanceValid(_quickSlotBar) ? _quickSlotBar.SlotText(slot) : string.Empty;

    private void BuildDemolitionHud(Control root)
    {
        BuildDemolitionBuyHud(root);
        _demolitionTeamStatusView = HudPackedSceneCache.Instantiate<DemolitionTeamStatusView>(
            DemolitionTeamStatusViewScenePath);
        root.AddChild(_demolitionTeamStatusView);
        _demolitionRoundResultView = HudPackedSceneCache.Instantiate<DemolitionRoundResultView>(
            DemolitionRoundResultViewScenePath);
        root.AddChild(_demolitionRoundResultView);
    }

    private void BuildDemolitionBuyHud(Control root)
    {
        _demolitionBuyView = HudPackedSceneCache.Instantiate<DemolitionBuyView>(
            DemolitionBuyViewScenePath);
        root.AddChild(_demolitionBuyView);
        _demolitionBuyView.PurchaseRequested += (
            sidearmId,
            primaryId,
            armorSelected,
            grenadeCount,
            smokeGrenadeCount,
            incendiaryGrenadeCount) =>
            EmitSignal(
                SignalName.DemolitionPurchaseRequested,
                sidearmId,
                primaryId,
                armorSelected,
                grenadeCount,
                smokeGrenadeCount,
                incendiaryGrenadeCount);
        _demolitionBuyView.PurchaseRequestedWithFlash += (
            sidearmId,
            primaryId,
            armorSelected,
            grenadeCount,
            smokeGrenadeCount,
            incendiaryGrenadeCount,
            flashbangGrenadeCount) =>
            EmitSignal(
                SignalName.DemolitionPurchaseRequestedWithFlash,
                sidearmId,
                primaryId,
                armorSelected,
                grenadeCount,
                smokeGrenadeCount,
                incendiaryGrenadeCount,
                flashbangGrenadeCount);
    }

    public void SetDemolitionGameplayPresentation(bool active)
    {
        _demolitionGameplayPresentation = active;
        if (IsInstanceValid(_demolitionTeamStatusView))
        {
            _demolitionTeamStatusView.Visible = active;
        }
        if (!active && IsInstanceValid(_demolitionRoundResultView))
        {
            _demolitionRoundResultView.HideResult();
        }
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
        RefreshSquadCommandPresentationVisibility();
        CanvasItem[] legacyTopLabels =
        {
            _objectiveLabel,
            _enemiesLabel,
            _phaseLabel,
            _compassLabel,
            _alertLabel
        };
        foreach (var label in legacyTopLabels)
        {
            if (IsInstanceValid(label))
            {
                label.Visible = !active;
            }
        }
    }

    public void SetDemolitionSmokeGrenades(int count)
    {
        _demolitionSmokeGrenades = Mathf.Max(0, count);
        RefreshQuickSlotBar();
    }

    public void SetDemolitionUtilities(
        int smokeGrenades,
        int incendiaryGrenades,
        DemolitionUtilityType selectedUtility)
        => SetDemolitionUtilities(
            smokeGrenades,
            incendiaryGrenades,
            0,
            selectedUtility);

    public void SetDemolitionUtilities(
        int smokeGrenades,
        int incendiaryGrenades,
        int flashbangGrenades,
        DemolitionUtilityType selectedUtility)
    {
        _demolitionSmokeGrenades = Mathf.Max(0, smokeGrenades);
        _demolitionIncendiaryGrenades = Mathf.Max(0, incendiaryGrenades);
        _demolitionFlashbangGrenades = Mathf.Max(0, flashbangGrenades);
        _demolitionSelectedUtility = selectedUtility;
        RefreshQuickSlotBar();
    }

    public void SetDemolitionTeamStatus(DemolitionTeamStatusSnapshot snapshot)
    {
        if (!IsInstanceValid(_demolitionTeamStatusView))
        {
            return;
        }
        _demolitionTeamStatusView.SetLanguage(_language);
        _demolitionTeamStatusView.SetSnapshot(snapshot);
    }

    public void PressQuickSlotForDiagnostics(int slot)
        => _quickSlotBar.PressSlotForDiagnostics(slot);

    internal void BeginRadioMessageSuppressionForDiagnostics()
        => _radioMessageDiagnosticSuppressionDepth++;

    internal void EndRadioMessageSuppressionForDiagnostics()
    {
        if (_radioMessageDiagnosticSuppressionDepth > 0)
        {
            _radioMessageDiagnosticSuppressionDepth--;
        }
    }

    internal string DemolitionObjectiveTextForDiagnostics
        => IsInstanceValid(_objectiveLabel) ? _objectiveLabel.Text : string.Empty;

    internal bool DemolitionRadioShowsSquadEliminatedForDiagnostics
        => IsInstanceValid(_radioLabel)
            && _radioLabel.Visible
            && _radioLabel.Text.Contains("SQUAD ELIMINATED", System.StringComparison.Ordinal);

    public void ShowDemolitionBuy(DemolitionBuySnapshot snapshot)
    {
        HideOperationsMenus();
        _gameplayHudRoot.Visible = true;
        _stateOverlay.Visible = false;
        HideDemolitionRoundResult();
        _demolitionBuyView.SetLanguage(_language);
        _demolitionBuyView.BeginRound(snapshot);
    }

    public void ShowDemolitionRoundResult(
        bool victory,
        string reason,
        int playerScore,
        int opponentScore,
        float secondsRemaining)
    {
        HideDemolitionBuy();
        _gameplayHudRoot.Visible = true;
        _demolitionRoundResultView.SetLanguage(_language);
        _demolitionRoundResultView.ShowResult(
            victory,
            reason,
            playerScore,
            opponentScore,
            secondsRemaining);
    }

    public void UpdateDemolitionRoundResult(float secondsRemaining)
    {
        if (IsInstanceValid(_demolitionRoundResultView)
            && _demolitionRoundResultView.Visible)
        {
            _demolitionRoundResultView.UpdateCountdown(secondsRemaining);
        }
    }

    public void HideDemolitionRoundResult()
    {
        if (IsInstanceValid(_demolitionRoundResultView))
        {
            _demolitionRoundResultView.HideResult();
        }
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

    public void SetDemolitionBuyIncendiaryGrenadesForDiagnostics(int count)
        => _demolitionBuyView.SetIncendiaryGrenadesForDiagnostics(count);

    public void SetDemolitionBuyFlashbangGrenadesForDiagnostics(int count)
        => _demolitionBuyView.SetFlashbangGrenadesForDiagnostics(count);

    public void PressDemolitionBuyConfirmForDiagnostics()
        => _demolitionBuyView.PressConfirmForDiagnostics();

    private void RefreshDemolitionBuyLanguage()
    {
        if (IsInstanceValid(_demolitionBuyView))
        {
            _demolitionBuyView.SetLanguage(_language);
        }
        if (IsInstanceValid(_demolitionRoundResultView))
        {
            _demolitionRoundResultView.SetLanguage(_language);
        }
        if (IsInstanceValid(_demolitionTeamStatusView))
        {
            _demolitionTeamStatusView.SetLanguage(_language);
        }
    }
}
