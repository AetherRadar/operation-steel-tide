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
    private Label _quickStartTitle = null!;
    private Label _quickStartDetail = null!;
    private Label _demolitionEntryTitle = null!;
    private Label _demolitionEntryDetail = null!;
    private Button _quickStartButton = null!;
    private Button _demolitionModeButton = null!;
    private Button _operationsQuitButton = null!;
    private Button _resultOfficeButton = null!;

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
        && IsInstanceValid(_operationsCredits)
        && IsInstanceValid(_resultOfficeButton);
    public bool OperationsOfficeUsesPackedScene
        => IsInstanceValid(_operationsOfficeRoot)
        && _operationsOfficeRoot.SceneFilePath == OperationsOfficeViewScenePath
        && _operationsOfficeRoot.GetNodeOrNull<Control>("Rail/QuickStartButton/QuickStartTitle") is not null
        && _operationsOfficeRoot.GetNodeOrNull<Control>("Rail/DemolitionModeButton/DemolitionEntryTitle") is not null;
    public bool DemolitionBriefingUiReady
        => IsInstanceValid(_demolitionBriefingView) && _demolitionBriefingView.UiReady;
    public bool DemolitionBriefingUsesPackedScene
        => IsInstanceValid(_demolitionBriefingView)
        && _demolitionBriefingView.SceneFilePath == DemolitionBriefingViewScenePath;
    public bool DemolitionBriefingIntentSignalsReady
        => IsInstanceValid(_demolitionBriefingView) && _demolitionBriefingView.IntentSignalsConnected;
    public bool DemolitionBriefingLanguageReady
        => IsInstanceValid(_demolitionBriefingView) && _demolitionBriefingView.LanguageMatches(_language);
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
            : DemolitionMapCatalog.TideforgeId;
    public int DemolitionMapOptionCount
        => IsInstanceValid(_demolitionBriefingView) ? _demolitionBriefingView.MapOptionCount : 0;
    public string BrowsedDemolitionMapId
        => IsInstanceValid(_demolitionBriefingView)
            ? _demolitionBriefingView.BrowsedMapId
            : DemolitionMapCatalog.TideforgeId;
    public int BrowsedDemolitionMapIndex
        => IsInstanceValid(_demolitionBriefingView) ? _demolitionBriefingView.BrowsedMapIndex : 0;
    public bool BrowsedDemolitionMapAvailable
        => IsInstanceValid(_demolitionBriefingView) && _demolitionBriefingView.IsBrowsedMapAvailable;
    public bool DemolitionBriefingDeployEnabled
        => IsInstanceValid(_demolitionBriefingView) && _demolitionBriefingView.IsDeployEnabled;
    public bool OperationsOfficeLanguageReady
        => IsInstanceValid(_operationsOfficeTitle)
        && _operationsOfficeTitle.Text == Text("operations_office", "OPERATIONS OFFICE")
        && _quickStartTitle.Text == Text("operations_quick_title", "QUICK EXTRACTION")
        && DemolitionBriefingLanguageReady
        && (!GameLocalization.IsChinese(_language)
            || (_operationsOfficeTitle.Text != "OPERATIONS OFFICE"
                && _quickStartTitle.Text != "QUICK EXTRACTION"
                && DemolitionBriefingLanguageReady));

    private void BuildOperationsOfficeHud(Control root)
    {
        BuildOperationsOfficeMenu(root);
        BuildDemolitionBriefingView(root);
        BuildResultOfficeAction();
    }

    private void BuildOperationsOfficeMenu(Control root)
    {
        var scene = GD.Load<PackedScene>(OperationsOfficeViewScenePath)
            ?? throw new System.InvalidOperationException($"Unable to load {OperationsOfficeViewScenePath}");
        _operationsOfficeRoot = scene.Instantiate<ColorRect>();
        root.AddChild(_operationsOfficeRoot);

        var rail = _operationsOfficeRoot.GetNode<Control>("Rail");
        _operationsOfficeSection = rail.GetNode<Label>("OperationsOfficeSection");
        _operationsOfficeTitle = rail.GetNode<Label>("OperationsOfficeTitle");
        _operationsOfficeSubtitle = rail.GetNode<Label>("OperationsOfficeSubtitle");
        _operationsCredits = rail.GetNode<Label>("OperationsCredits");
        _operationsRecord = rail.GetNode<Label>("OperationsRecord");
        _quickStartButton = rail.GetNode<Button>("QuickStartButton");
        _quickStartTitle = _quickStartButton.GetNode<Label>("QuickStartTitle");
        _quickStartDetail = _quickStartButton.GetNode<Label>("QuickStartDetail");
        _demolitionModeButton = rail.GetNode<Button>("DemolitionModeButton");
        _demolitionEntryTitle = _demolitionModeButton.GetNode<Label>("DemolitionEntryTitle");
        _demolitionEntryDetail = _demolitionModeButton.GetNode<Label>("DemolitionEntryDetail");
        _operationsStatus = rail.GetNode<Label>("OperationsStatus");
        _operationsQuitButton = rail.GetNode<Button>("OperationsQuitButton");

        _quickStartButton.Pressed += () => EmitSignal(SignalName.OperationsQuickStartRequested);
        _demolitionModeButton.Pressed += () => EmitSignal(SignalName.DemolitionModeRequested);
        _operationsQuitButton.Pressed += () => EmitSignal(SignalName.QuitRequested);
    }

    private void BuildDemolitionBriefingView(Control root)
    {
        var scene = GD.Load<PackedScene>(DemolitionBriefingViewScenePath)
            ?? throw new System.InvalidOperationException($"Unable to load {DemolitionBriefingViewScenePath}");
        _demolitionBriefingView = scene.Instantiate<DemolitionBriefingView>();
        root.AddChild(_demolitionBriefingView);
        _demolitionBriefingView.BackRequested += () => EmitSignal(SignalName.DemolitionBackRequested);
        _demolitionBriefingView.DeployRequested += (role, primary, build, sidearm, mapId) => EmitSignal(
            SignalName.DemolitionDeploymentRequested,
            role,
            primary,
            build,
            sidearm,
            mapId);
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
        HideDemolitionBuy();
        SetDemolitionGameplayPresentation(false);
        _squadLobby.Visible = false;
        _demolitionBriefingView.Visible = false;
        _operationsOfficeRoot.Visible = true;
        _gameplayHudRoot.Visible = false;
        _classSkillRoot.Visible = false;
        _squadRoster.Visible = false;
        _operationsStatus.Text = status;
        RefreshOperationsOfficeProfile();
        RefreshOperationsOfficeLanguage();
    }

    public void ShowDemolitionBriefing()
    {
        HideDemolitionBuy();
        _squadLobby.Visible = false;
        _operationsOfficeRoot.Visible = false;
        _demolitionBriefingView.Visible = true;
        _gameplayHudRoot.Visible = false;
        _classSkillRoot.Visible = false;
        _squadRoster.Visible = false;
        SelectDemolitionRole(SelectedDemolitionRole);
        RefreshOperationsOfficeLanguage();
    }

    public void HideOperationsMenus()
    {
        if (IsInstanceValid(_operationsOfficeRoot))
        {
            _operationsOfficeRoot.Visible = false;
        }
        if (IsInstanceValid(_demolitionBriefingView))
        {
            _demolitionBriefingView.Visible = false;
        }
        HideDemolitionBuy();
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

    public void ShowDemolitionResult(bool victory, string reason)
    {
        HideOperationsMenus();
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
        _quickStartTitle.Text = Text("operations_quick_title", "QUICK EXTRACTION");
        _quickStartDetail.Text = Text("operations_quick_detail", "ENTER LOADOUT  //  SQUAD UP  //  LOOT AND EXTRACT");
        _demolitionEntryTitle.Text = Text("operations_demolition_title", "DEMOLITION");
        _demolitionEntryDetail.Text = Text("operations_demolition_detail", "5 V 5  //  FIRST TO 13  //  12-MAP POOL");
        _operationsQuitButton.Text = Text("operations_exit", "EXIT TO DESKTOP");
        _demolitionBriefingView.SetLanguage(_language);
        _resultOfficeButton.Text = Text("operations_return", "RETURN TO OPERATIONS OFFICE");
        RefreshOperationsOfficeProfile();
        SelectDemolitionRole(SelectedDemolitionRole);
    }
}
