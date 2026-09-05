using Godot;

namespace OperationSteelTide;

public partial class CombatHUD
{
    private const string OperationsOfficeViewScenePath = "res://ui/OperationsOfficeView.tscn";
    private const string DemolitionBriefingViewScenePath = "res://ui/DemolitionBriefingView.tscn";

    private ColorRect _operationsOfficeRoot = null!;
    private DemolitionBriefingView _demolitionBriefingView = null!;
    private Label _operationsOfficeTitle = null!;
    private Label _operationsOfficeSubtitle = null!;
    private Label _operationsOfficeSection = null!;
    private Label _operationsCredits = null!;
    private Label _operationsRecord = null!;
    private Label _operationsStatus = null!;
    private Label _quickStartIndex = null!;
    private Label _quickStartTitle = null!;
    private Label _quickStartDetail = null!;
    private Label _demolitionEntryIndex = null!;
    private Label _demolitionEntryTitle = null!;
    private Label _demolitionEntryDetail = null!;
    private Label _trainingRangeIndex = null!;
    private Label _trainingRangeTitle = null!;
    private Label _trainingRangeDetail = null!;
    private Button _quickStartButton = null!;
    private Button _demolitionModeButton = null!;
    private Button _trainingRangeButton = null!;
    private Button _operationsQuitButton = null!;
    private Button _resultOfficeButton = null!;
    private OperationsOfficeFocus _operationsOfficeHoverFocus;
    private OperationsOfficeFocus _operationsOfficeKeyboardFocus;

    public bool IsOperationsOfficeVisible
        => IsInstanceValid(_operationsOfficeRoot) && _operationsOfficeRoot.Visible;
    public bool IsDemolitionBriefingVisible
        => IsInstanceValid(_demolitionBriefingView) && _demolitionBriefingView.Visible;
    public bool IsGameplayHudVisible
        => IsInstanceValid(_gameplayHudRoot) && _gameplayHudRoot.Visible;
    public bool IsExtractionCinematicUiClear
        => IsInstanceValid(_gameplayHudRoot)
        && !_gameplayHudRoot.Visible
        && IsInstanceValid(_squadRoster)
        && !_squadRoster.Visible
        && IsInstanceValid(_classSkillRoot)
        && !_classSkillRoot.Visible;
    public bool OperationsOfficeUiReady
        => IsInstanceValid(_quickStartButton)
        && IsInstanceValid(_demolitionModeButton)
        && IsInstanceValid(_trainingRangeButton)
        && IsInstanceValid(_operationsCredits)
        && IsInstanceValid(_resultOfficeButton)
        && _quickStartButton.FocusMode == Control.FocusModeEnum.All
        && _demolitionModeButton.FocusMode == Control.FocusModeEnum.All
        && _trainingRangeButton.FocusMode == Control.FocusModeEnum.All;
    public bool OperationsOfficeUsesPackedScene
        => IsInstanceValid(_operationsOfficeRoot)
        && _operationsOfficeRoot.SceneFilePath == OperationsOfficeViewScenePath
        && _operationsOfficeRoot.GetNodeOrNull<Control>("Rail/QuickStartButton/QuickStartTitle") is not null
        && _operationsOfficeRoot.GetNodeOrNull<Control>("Rail/DemolitionModeButton/DemolitionEntryTitle") is not null
        && _operationsOfficeRoot.GetNodeOrNull<Control>("Rail/TrainingRangeButton/TrainingRangeTitle") is not null;
    public bool DemolitionBriefingUiReady
        => IsInstanceValid(_demolitionBriefingView) && _demolitionBriefingView.UiReady;
    public bool DemolitionBriefingUsesPackedScene
        => IsInstanceValid(_demolitionBriefingView)
        && _demolitionBriefingView.SceneFilePath == DemolitionBriefingViewScenePath;
    public bool DemolitionBriefingIntentSignalsReady
        => IsInstanceValid(_demolitionBriefingView) && _demolitionBriefingView.IntentSignalsConnected;
    public bool DemolitionBriefingLanguageReady
        => IsInstanceValid(_demolitionBriefingView) && _demolitionBriefingView.LanguageMatches(_language);
    public bool IsDemolitionNetworkLobbyWaiting
        => IsInstanceValid(_demolitionBriefingView) && _demolitionBriefingView.IsNetworkLobbyWaiting;
    public int DemolitionNetworkLobbyPlayerCount
        => IsInstanceValid(_demolitionBriefingView) ? _demolitionBriefingView.NetworkLobbyPlayerCount : 0;
    public bool DemolitionNetworkLobbyCanStart
        => IsInstanceValid(_demolitionBriefingView) && _demolitionBriefingView.NetworkLobbyCanStart;

    public void SetDemolitionNetworkConnectionPending(bool pending, string status)
    {
        if (IsInstanceValid(_demolitionBriefingView))
        {
            _demolitionBriefingView.SetNetworkConnectionPending(pending, status);
        }
    }

    public void SetDemolitionNetworkLobbyWaiting(
        bool host,
        int players,
        int capacity,
        bool canStart,
        string status)
    {
        if (IsInstanceValid(_demolitionBriefingView))
        {
            _demolitionBriefingView.SetNetworkLobbyWaiting(
                host, players, capacity, canStart, status);
        }
    }

    public void ClearDemolitionNetworkLobbyWaiting()
    {
        if (IsInstanceValid(_demolitionBriefingView))
        {
            _demolitionBriefingView.ClearNetworkLobbyWaiting();
        }
    }
    public OperatorRole SelectedDemolitionRole
        => IsInstanceValid(_demolitionBriefingView)
            ? _demolitionBriefingView.SelectedRole
            : OperatorRole.Assault;
    public WeaponPlatform SelectedDemolitionPrimary
        => IsInstanceValid(_demolitionBriefingView)
            ? _demolitionBriefingView.SelectedPrimaryPlatform
            : WeaponPlatform.M4A1;
    public int SelectedDemolitionBuildTier
        => IsInstanceValid(_demolitionBriefingView)
            ? _demolitionBriefingView.SelectedBuildTier
            : 1;
    public WeaponPlatform SelectedDemolitionSidearm
        => IsInstanceValid(_demolitionBriefingView)
            ? _demolitionBriefingView.SelectedSidearmPlatform
            : WeaponPlatform.P226;
    public string SelectedDemolitionMapId
        => IsInstanceValid(_demolitionBriefingView)
            ? _demolitionBriefingView.SelectedMapId
            : DemolitionMapCatalog.BazaarCrossingId;
    public SquadSessionMode SelectedDemolitionSessionMode
        => IsInstanceValid(_demolitionBriefingView)
            ? _demolitionBriefingView.SelectedSessionMode
            : SquadSessionMode.Local;
    public DemolitionNetworkTeam SelectedDemolitionNetworkTeam
        => IsInstanceValid(_demolitionBriefingView)
            ? _demolitionBriefingView.SelectedNetworkTeam
            : DemolitionNetworkTeam.Alpha;
    public int DemolitionMapOptionCount
        => IsInstanceValid(_demolitionBriefingView) ? _demolitionBriefingView.MapOptionCount : 0;
    public string BrowsedDemolitionMapId
        => IsInstanceValid(_demolitionBriefingView)
            ? _demolitionBriefingView.BrowsedMapId
            : DemolitionMapCatalog.BazaarCrossingId;
    public int BrowsedDemolitionMapIndex
        => IsInstanceValid(_demolitionBriefingView) ? _demolitionBriefingView.BrowsedMapIndex : 0;
    public bool BrowsedDemolitionMapAvailable
        => IsInstanceValid(_demolitionBriefingView) && _demolitionBriefingView.IsBrowsedMapAvailable;
    public bool DemolitionBriefingDeployEnabled
        => IsInstanceValid(_demolitionBriefingView) && _demolitionBriefingView.IsDeployEnabled;
    public bool OperationsOfficeLanguageReady
        => IsInstanceValid(_operationsOfficeTitle)
        && _operationsOfficeSection.Text == Text(
            "operations_section",
            "SPECIAL OPERATIONS CENTER  //  EAST WING")
        && _operationsOfficeTitle.Text == Text("operations_office", "OPERATIONS OFFICE")
        && _operationsOfficeSubtitle.Text == Text("operations_subtitle", "OPERATION STEEL TIDE")
        && _quickStartIndex.Text == Text("operations_quick_index", "01  //  EXTRACTION")
        && _quickStartTitle.Text == Text("operations_quick_title", "QUICK EXTRACTION")
        && _quickStartDetail.Text == Text(
            "operations_quick_detail",
            "ROLE  →  OBJECTIVES  →  LOOT  →  EXTRACT")
        && _demolitionEntryIndex.Text == Text("operations_demolition_index", "02  //  DEMOLITION")
        && _demolitionEntryTitle.Text == Text("operations_demolition_title", "DEMOLITION")
        && _demolitionEntryDetail.Text == Text(
            "operations_demolition_detail",
            "BUY  →  PLANT / DEFUSE  →  WIN 13 ROUNDS")
        && _trainingRangeIndex.Text == Text("operations_training_index", "03  //  TRAINING RANGE")
        && _trainingRangeTitle.Text == Text("operations_training_title", "TRAINING RANGE")
        && _trainingRangeDetail.Text == Text(
            "operations_training_detail",
            "ALL GUNS  →  INFINITE AMMO  →  LIVE BOTS")
        && _operationsStatus.Text == Text(
            "operations_status_ready",
            "FIELD TEAM STANDING BY  //  HELIPAD CLEAR")
        && _operationsCredits.Text.StartsWith(
            Text("operations_available_funds", "AVAILABLE FUNDS"),
            System.StringComparison.Ordinal)
        && _operationsRecord.Text.Contains(
            Text("operations_extractions", "EXTRACTIONS"),
            System.StringComparison.Ordinal)
        && _operationsRecord.Text.Contains(
            Text("operations_lifetime_value", "LIFETIME VALUE"),
            System.StringComparison.Ordinal)
        && _operationsQuitButton.Text == Text("operations_exit", "EXIT TO DESKTOP")
        && _quickStartButton.Text == _quickStartTitle.Text
        && _demolitionModeButton.Text == _demolitionEntryTitle.Text
        && _quickStartButton.TooltipText == _quickStartTitle.Text
        && _demolitionModeButton.TooltipText == _demolitionEntryTitle.Text
        && _trainingRangeButton.Text == _trainingRangeTitle.Text
        && _trainingRangeButton.TooltipText == _trainingRangeTitle.Text
        && DemolitionBriefingLanguageReady
        && (!GameLocalization.IsChinese(_language)
            || (_operationsOfficeTitle.Text != "OPERATIONS OFFICE"
                && _quickStartTitle.Text != "QUICK EXTRACTION"
                && DemolitionBriefingLanguageReady));

    private void BuildOperationsOfficeHud(Control root)
    {
        BuildOperationsOfficeMenu(root);
        BuildDemolitionBriefingView(root);
        EnsureTrainingRangeSetupView();
        BuildResultOfficeAction();
    }

    private void BuildOperationsOfficeMenu(Control root)
    {
        _operationsOfficeRoot = HudPackedSceneCache.Instantiate<ColorRect>(
            OperationsOfficeViewScenePath);
        root.AddChild(_operationsOfficeRoot);

        var rail = _operationsOfficeRoot.GetNode<Control>("Rail");
        _operationsOfficeSection = rail.GetNode<Label>("OperationsOfficeSection");
        _operationsOfficeTitle = rail.GetNode<Label>("OperationsOfficeTitle");
        _operationsOfficeSubtitle = rail.GetNode<Label>("OperationsOfficeSubtitle");
        _operationsCredits = rail.GetNode<Label>("OperationsCredits");
        _operationsRecord = rail.GetNode<Label>("OperationsRecord");
        _quickStartButton = rail.GetNode<Button>("QuickStartButton");
        _quickStartIndex = _quickStartButton.GetNode<Label>("QuickStartIndex");
        _quickStartTitle = _quickStartButton.GetNode<Label>("QuickStartTitle");
        _quickStartDetail = _quickStartButton.GetNode<Label>("QuickStartDetail");
        _demolitionModeButton = rail.GetNode<Button>("DemolitionModeButton");
        _demolitionEntryIndex = _demolitionModeButton.GetNode<Label>("DemolitionEntryIndex");
        _demolitionEntryTitle = _demolitionModeButton.GetNode<Label>("DemolitionEntryTitle");
        _demolitionEntryDetail = _demolitionModeButton.GetNode<Label>("DemolitionEntryDetail");
        _trainingRangeButton = rail.GetNode<Button>("TrainingRangeButton");
        _trainingRangeIndex = _trainingRangeButton.GetNode<Label>("TrainingRangeIndex");
        _trainingRangeTitle = _trainingRangeButton.GetNode<Label>("TrainingRangeTitle");
        _trainingRangeDetail = _trainingRangeButton.GetNode<Label>("TrainingRangeDetail");
        _operationsStatus = rail.GetNode<Label>("OperationsStatus");
        _operationsQuitButton = rail.GetNode<Button>("OperationsQuitButton");

        _quickStartButton.Pressed += () => EmitSignal(SignalName.OperationsQuickStartRequested);
        _demolitionModeButton.Pressed += () => EmitSignal(SignalName.DemolitionModeRequested);
        _trainingRangeButton.Pressed += () => EmitSignal(SignalName.TrainingRangeRequested);
        _operationsQuitButton.Pressed += () => EmitSignal(SignalName.QuitRequested);
        BindOperationsFocus(_quickStartButton, OperationsOfficeFocus.QuickExtraction);
        BindOperationsFocus(_demolitionModeButton, OperationsOfficeFocus.Demolition);
        BindOperationsFocus(_trainingRangeButton, OperationsOfficeFocus.Neutral);
    }

    private void BuildDemolitionBriefingView(Control root)
    {
        _demolitionBriefingView = HudPackedSceneCache.Instantiate<DemolitionBriefingView>(
            DemolitionBriefingViewScenePath);
        root.AddChild(_demolitionBriefingView);
        _demolitionBriefingView.BackRequested += () => EmitSignal(SignalName.DemolitionBackRequested);
        _demolitionBriefingView.DeployRequested += (
            role, primary, build, sidearm, mapId, sessionMode, address, networkTeam) => EmitSignal(
            SignalName.DemolitionDeploymentRequested,
            role,
            primary,
            build,
            sidearm,
            mapId,
            sessionMode,
            address,
            networkTeam);
    }

    private void BuildResultOfficeAction()
    {
        _resultOfficeButton = Button("RETURN TO OPERATIONS OFFICE", new Vector2(-180, 274), new Vector2(360, 48));
        _resultOfficeButton.SetAnchorsPreset(Control.LayoutPreset.Center);
        _resultOfficeButton.AddThemeFontSizeOverride("font_size", 14);
        _resultOfficeButton.Pressed += () => EmitSignal(SignalName.OperationsHomeRequested);
        _stateOverlay.AddChild(_resultOfficeButton);
    }

    private void SelectDemolitionRole(OperatorRole role)
    {
        if (IsInstanceValid(_demolitionBriefingView))
        {
            _demolitionBriefingView.SelectRole(role);
        }
    }

    public void ShowOperationsOffice(string status = "FIELD TEAM STANDING BY  //  HELIPAD CLEAR")
    {
        SetTrainingRangeGameplayInputEnabled(false);
        HideDemolitionBuy();
        SetDemolitionGameplayPresentation(false);
        _squadLobby.Visible = false;
        _demolitionBriefingView.Visible = false;
        _operationsOfficeRoot.Visible = true;
        _gameplayHudRoot.Visible = false;
        _classSkillRoot.Visible = false;
        _squadRoster.Visible = false;
        _operationsStatus.Text = Text("operations_status_ready", status);
        RefreshOperationsOfficeProfile();
        RefreshOperationsOfficeLanguage();
        _operationsOfficeKeyboardFocus = OperationsOfficeFocus.QuickExtraction;
        _quickStartButton.GrabFocus();
        RefreshOperationsBackdropFocus();
    }

    public void ShowDemolitionBriefing()
    {
        SetTrainingRangeGameplayInputEnabled(false);
        HideDemolitionBuy();
        _squadLobby.Visible = false;
        _operationsOfficeRoot.Visible = false;
        _demolitionBriefingView.Visible = true;
        _gameplayHudRoot.Visible = false;
        _classSkillRoot.Visible = false;
        _squadRoster.Visible = false;
        ResetOperationsBackdropFocus();
        SelectDemolitionRole(SelectedDemolitionRole);
        RefreshOperationsOfficeLanguage();
    }

    public void HideOperationsMenus()
    {
        if (IsInstanceValid(_operationsOfficeRoot))
        {
            _operationsOfficeRoot.Visible = false;
        }
        ResetOperationsBackdropFocus();
        if (IsInstanceValid(_demolitionBriefingView))
        {
            _demolitionBriefingView.Visible = false;
        }
        HideDemolitionBuy();
    }

    public void ShowTrainingRangeGameplay(string status)
    {
        SetTrainingRangeGameplayInputEnabled(true);
        HideOperationsMenus();
        _squadLobby.Visible = false;
        _gameplayHudRoot.Visible = true;
        KeepTrainingRangeOverlaysHidden();
        _operationBanner.Text = status;
        _operationBanner.Modulate = Colors.White;
        _operationBanner.Visible = true;
        if (IsInstanceValid(_trainingRangeStatsLabel))
        {
            _trainingRangeStatsLabel.Visible = true;
        }
    }

    public void HideTrainingRangeGameplay()
    {
        SetTrainingRangeGameplayInputEnabled(false);
        HideTrainingRangeSetup();
        _operationBanner.Visible = false;
        if (IsInstanceValid(_trainingRangeStatsLabel))
        {
            _trainingRangeStatsLabel.Visible = false;
        }
        if (IsInstanceValid(_medicalStatusRoot))
        {
            _medicalStatusRoot.Visible = true;
        }
        if (IsInstanceValid(_backpackHotkeyButton))
        {
            _backpackHotkeyButton.Visible = true;
        }
    }

    /// <summary>
    /// Extraction and squad refreshes can run while the standalone range is active.
    /// Keep their mission-only overlays out of the live-fire presentation every
    /// frame so a stale squad roster or extraction countdown cannot reappear.
    /// </summary>
    public void KeepTrainingRangeOverlaysHidden()
    {
        if (IsInstanceValid(_squadRoster))
        {
            _squadRoster.Visible = false;
        }
        if (IsInstanceValid(_extractionRoot))
        {
            _extractionRoot.Visible = false;
        }
        if (IsInstanceValid(_classSkillRoot))
        {
            _classSkillRoot.Visible = false;
        }
        if (IsInstanceValid(_medicalStatusRoot))
        {
            _medicalStatusRoot.Visible = false;
        }
        if (IsInstanceValid(_backpackHotkeyButton))
        {
            _backpackHotkeyButton.Visible = false;
        }
    }

    public void SetExtractionCinematicVisible(bool active)
    {
        _gameplayHudRoot.Visible = !active;
        _squadRoster.Visible = !active;
        _classSkillRoot.Visible = !active;
    }

    public void PressOperationsQuickStartForDiagnostics()
        => PressButtonForDiagnostics(_quickStartButton);

    public void PressDemolitionModeForDiagnostics()
        => PressButtonForDiagnostics(_demolitionModeButton);

    public void PressTrainingRangeForDiagnostics()
        => PressButtonForDiagnostics(_trainingRangeButton);

    public void FocusOperationsModeForDiagnostics(OperationsOfficeFocus focus)
    {
        switch (focus)
        {
            case OperationsOfficeFocus.QuickExtraction:
                _quickStartButton.GrabFocus();
                break;
            case OperationsOfficeFocus.Demolition:
                _demolitionModeButton.GrabFocus();
                break;
        }
    }

    public void PressDemolitionRoleForDiagnostics(OperatorRole role)
        => _demolitionBriefingView.PressRoleForDiagnostics(role);

    public void PressDemolitionBackForDiagnostics()
        => _demolitionBriefingView.PressBackForDiagnostics();

    public void PressDemolitionDeployForDiagnostics()
        => _demolitionBriefingView.PressDeployForDiagnostics();

    public void SelectDemolitionLoadoutForDiagnostics(
        WeaponPlatform primary,
        int buildTier,
        WeaponPlatform sidearm)
        => _demolitionBriefingView.SelectLoadoutForDiagnostics(primary, buildTier, sidearm);

    public bool PressDemolitionMapForDiagnostics(string mapId)
        => _demolitionBriefingView.SelectMap(mapId);

    public void SelectDemolitionNetworkForDiagnostics(
        SquadSessionMode sessionMode,
        DemolitionNetworkTeam team,
        string address = "127.0.0.1")
        => _demolitionBriefingView.SelectNetworkForDiagnostics(sessionMode, team, address);

    public void PressPreviousDemolitionMapForDiagnostics()
        => _demolitionBriefingView.PressPreviousMapForDiagnostics();

    public void PressNextDemolitionMapForDiagnostics()
        => _demolitionBriefingView.PressNextMapForDiagnostics();

    private static void PressButtonForDiagnostics(Button button)
    {
        if (IsInstanceValid(button))
        {
            button.EmitSignal(Godot.Button.SignalName.Pressed);
        }
    }

    private void BindOperationsFocus(Button button, OperationsOfficeFocus focus)
    {
        button.MouseEntered += () =>
        {
            _operationsOfficeHoverFocus = focus;
            RefreshOperationsBackdropFocus();
        };
        button.MouseExited += () =>
        {
            if (_operationsOfficeHoverFocus == focus)
            {
                _operationsOfficeHoverFocus = OperationsOfficeFocus.Neutral;
                RefreshOperationsBackdropFocus();
            }
        };
        button.FocusEntered += () =>
        {
            _operationsOfficeHoverFocus = OperationsOfficeFocus.Neutral;
            _operationsOfficeKeyboardFocus = focus;
            RefreshOperationsBackdropFocus();
        };
        button.FocusExited += () =>
        {
            if (_operationsOfficeKeyboardFocus == focus)
            {
                _operationsOfficeKeyboardFocus = OperationsOfficeFocus.Neutral;
                RefreshOperationsBackdropFocus();
            }
        };
    }

    private void ResetOperationsBackdropFocus()
    {
        _operationsOfficeHoverFocus = OperationsOfficeFocus.Neutral;
        _operationsOfficeKeyboardFocus = OperationsOfficeFocus.Neutral;
        EmitSignal(
            SignalName.OperationsBackdropFocusChanged,
            (int)OperationsOfficeFocus.Neutral);
    }

    private void RefreshOperationsBackdropFocus()
    {
        var focus = _operationsOfficeHoverFocus != OperationsOfficeFocus.Neutral
            ? _operationsOfficeHoverFocus
            : _operationsOfficeKeyboardFocus;
        EmitSignal(SignalName.OperationsBackdropFocusChanged, (int)focus);
    }

    public void ShowDemolitionResult(bool victory, string reason)
    {
        HideOperationsMenus();
        HideDemolitionRoundResult();
        _gameplayHudRoot.Visible = true;
        _downedBanner.Visible = false;
        _stateOverlay.Visible = true;
        _stateTitle.Text = victory
            ? Text("demolition_success", "DEMOLITION SUCCESS")
            : Text("demolition_failed", "DEMOLITION FAILED");
        _stateTitle.AddThemeColorOverride(
            "font_color",
            victory ? new Color(1.0f, 0.68f, 0.28f) : new Color(1.0f, 0.28f, 0.18f));
        _stateSubtitle.Text = reason
            + "\n"
            + Text("demolition_result_economy", "REGULATION KIT RECOVERED  //  EXTRACTION WALLET UNCHANGED");
    }

    private void RefreshOperationsOfficeProfile()
    {
        if (!IsInstanceValid(_operationsCredits))
        {
            return;
        }
        _operationsCredits.Text = $"{Text("operations_available_funds", "AVAILABLE FUNDS")}  {_displayedProfile.Credits}";
        _operationsRecord.Text = $"{Text("operations_extractions", "EXTRACTIONS")}  {_displayedProfile.SuccessfulExtractions}  //  "
            + $"{Text("operations_lifetime_value", "LIFETIME VALUE")}  {_displayedProfile.LifetimeExtractedValue}";
    }

    private void RefreshOperationsOfficeLanguage()
    {
        if (!IsInstanceValid(_operationsOfficeTitle))
        {
            return;
        }
        _operationsOfficeSection.Text = Text("operations_section", "SPECIAL OPERATIONS CENTER  //  EAST WING");
        _operationsOfficeTitle.Text = Text("operations_office", "OPERATIONS OFFICE");
        _operationsOfficeSubtitle.Text = Text("operations_subtitle", "OPERATION STEEL TIDE");
        _quickStartIndex.Text = Text("operations_quick_index", "01  //  EXTRACTION");
        _quickStartTitle.Text = Text("operations_quick_title", "QUICK EXTRACTION");
        _quickStartDetail.Text = Text("operations_quick_detail", "ROLE  →  OBJECTIVES  →  LOOT  →  EXTRACT");
        _demolitionEntryIndex.Text = Text("operations_demolition_index", "02  //  DEMOLITION");
        _demolitionEntryTitle.Text = Text("operations_demolition_title", "DEMOLITION");
        _demolitionEntryDetail.Text = Text("operations_demolition_detail", "BUY  →  PLANT / DEFUSE  →  WIN 13 ROUNDS");
        _trainingRangeIndex.Text = Text("operations_training_index", "03  //  TRAINING RANGE");
        _trainingRangeTitle.Text = Text("operations_training_title", "TRAINING RANGE");
        _trainingRangeDetail.Text = Text("operations_training_detail", "ALL GUNS  →  INFINITE AMMO  →  LIVE BOTS");
        _operationsQuitButton.Text = Text("operations_exit", "EXIT TO DESKTOP");
        _operationsStatus.Text = Text(
            "operations_status_ready",
            "FIELD TEAM STANDING BY  //  HELIPAD CLEAR");
        _quickStartButton.Text = _quickStartTitle.Text;
        _demolitionModeButton.Text = _demolitionEntryTitle.Text;
        _quickStartButton.TooltipText = _quickStartTitle.Text;
        _demolitionModeButton.TooltipText = _demolitionEntryTitle.Text;
        _trainingRangeButton.Text = _trainingRangeTitle.Text;
        _trainingRangeButton.TooltipText = _trainingRangeTitle.Text;
        _demolitionBriefingView.SetLanguage(_language);
        _resultOfficeButton.Text = Text("operations_return", "RETURN TO OPERATIONS OFFICE");
        RefreshOperationsOfficeProfile();
        SelectDemolitionRole(SelectedDemolitionRole);
    }
}
