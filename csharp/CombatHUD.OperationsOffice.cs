using Godot;

namespace OperationSteelTide;

public partial class CombatHUD
{
    private const string OperationsOfficeViewScenePath = "res://ui/OperationsOfficeView.tscn";

    private ColorRect _operationsOfficeRoot = null!;
    private ColorRect _demolitionBriefingRoot = null!;
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
    private Label _demolitionTitle = null!;
    private Label _demolitionSubtitle = null!;
    private Label _demolitionRulesTitle = null!;
    private Label _demolitionRules = null!;
    private Label _demolitionRoleTitle = null!;
    private Label _demolitionLoadoutTitle = null!;
    private Label _demolitionLoadout = null!;
    private Label _demolitionReadyStatus = null!;
    private Button _demolitionDeployButton = null!;
    private Button _demolitionBackButton = null!;
    private Button _resultOfficeButton = null!;
    private readonly Button[] _demolitionRoleButtons = new Button[3];
    private readonly Label[] _demolitionRoleNames = new Label[3];
    private readonly Label[] _demolitionRoleDetails = new Label[3];
    private OperatorRole _selectedDemolitionRole = OperatorRole.Assault;

    public bool IsOperationsOfficeVisible
        => IsInstanceValid(_operationsOfficeRoot) && _operationsOfficeRoot.Visible;
    public bool IsDemolitionBriefingVisible
        => IsInstanceValid(_demolitionBriefingRoot) && _demolitionBriefingRoot.Visible;
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
        => IsInstanceValid(_demolitionDeployButton)
        && _demolitionRoleButtons.Length == 3
        && IsInstanceValid(_demolitionRoleButtons[0]);
    public OperatorRole SelectedDemolitionRole => _selectedDemolitionRole;
    public bool OperationsOfficeLanguageReady
        => IsInstanceValid(_operationsOfficeTitle)
        && _operationsOfficeTitle.Text == Text("operations_office", "OPERATIONS OFFICE")
        && _quickStartTitle.Text == Text("operations_quick_title", "QUICK EXTRACTION")
        && _demolitionDeployButton.Text == Text("demolition_deploy", "DEPLOY DEMOLITION TEAM")
        && (!GameLocalization.IsChinese(_language)
            || (_operationsOfficeTitle.Text != "OPERATIONS OFFICE"
                && _quickStartTitle.Text != "QUICK EXTRACTION"
                && _demolitionDeployButton.Text != "DEPLOY DEMOLITION TEAM"));

    private void BuildOperationsOfficeHud(Control root)
    {
        BuildOperationsOfficeMenu(root);
        BuildDemolitionBriefing(root);
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

    private void BuildDemolitionBriefing(Control root)
    {
        _demolitionBriefingRoot = new ColorRect
        {
            Name = "DemolitionBriefing",
            Color = new Color(0.002f, 0.005f, 0.006f, 0.48f),
            MouseFilter = Control.MouseFilterEnum.Stop,
            Visible = false
        };
        _demolitionBriefingRoot.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(_demolitionBriefingRoot);

        var band = new ColorRect
        {
            Position = new Vector2(0, 0),
            Size = new Vector2(760, 1080),
            Color = new Color(0.008f, 0.012f, 0.014f, 0.95f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _demolitionBriefingRoot.AddChild(band);
        band.AddChild(new ColorRect
        {
            Position = new Vector2(759, 0),
            Size = new Vector2(1, 1080),
            Color = new Color(1.0f, 0.52f, 0.18f, 0.82f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        });
        _demolitionTitle = Label("DEMOLITION BRIEFING", 32, new Color(1.0f, 0.9f, 0.78f));
        _demolitionTitle.Position = new Vector2(42, 48);
        _demolitionTitle.Size = new Vector2(650, 44);
        band.AddChild(_demolitionTitle);
        _demolitionSubtitle = Label("FREIGHT TERMINAL  //  ATTACKING ELEMENT", 12, new Color(0.78f, 0.55f, 0.34f));
        _demolitionSubtitle.Position = new Vector2(44, 96);
        _demolitionSubtitle.Size = new Vector2(650, 22);
        band.AddChild(_demolitionSubtitle);

        _demolitionRulesTitle = Label("ENGAGEMENT RULES", 12, new Color(0.48f, 0.72f, 0.64f));
        _demolitionRulesTitle.Position = new Vector2(44, 154);
        _demolitionRulesTitle.Size = new Vector2(300, 22);
        band.AddChild(_demolitionRulesTitle);
        _demolitionRules = Label(
            "PLANT AT SITE A OR B  //  HOLD F\nDEFEND THE DEVICE UNTIL DETONATION\nELIMINATION ALSO ENDS THE ROUND  //  NO LOOT BANKING",
            14,
            new Color(0.76f, 0.84f, 0.81f));
        _demolitionRules.Position = new Vector2(44, 184);
        _demolitionRules.Size = new Vector2(660, 92);
        _demolitionRules.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _demolitionRules.AddThemeConstantOverride("line_spacing", 6);
        band.AddChild(_demolitionRules);

        _demolitionRoleTitle = Label("SELECT OPERATOR", 12, new Color(0.48f, 0.72f, 0.64f));
        _demolitionRoleTitle.Position = new Vector2(44, 304);
        _demolitionRoleTitle.Size = new Vector2(300, 22);
        band.AddChild(_demolitionRoleTitle);
        var roleGroup = new ButtonGroup();
        var roles = new[] { OperatorRole.Assault, OperatorRole.Medic, OperatorRole.Recon };
        for (var i = 0; i < roles.Length; i++)
        {
            var role = roles[i];
            var spec = OperatorRoles.Spec(role);
            var button = DeploymentSegment(new Vector2(44 + i * 226, 340), new Vector2(210, 118), spec.Accent);
            button.ToggleMode = true;
            button.ButtonGroup = roleGroup;
            button.FocusMode = Control.FocusModeEnum.None;
            button.Pressed += () => SelectDemolitionRole(role);
            band.AddChild(button);
            _demolitionRoleButtons[i] = button;
            _demolitionRoleNames[i] = Label(spec.Name, 15, spec.Accent.Lightened(0.12f));
            _demolitionRoleNames[i].Position = new Vector2(14, 14);
            _demolitionRoleNames[i].Size = new Vector2(180, 24);
            _demolitionRoleNames[i].MouseFilter = Control.MouseFilterEnum.Ignore;
            button.AddChild(_demolitionRoleNames[i]);
            _demolitionRoleDetails[i] = Label(spec.SkillName, 10, new Color(0.58f, 0.7f, 0.66f));
            _demolitionRoleDetails[i].Position = new Vector2(14, 47);
            _demolitionRoleDetails[i].Size = new Vector2(180, 52);
            _demolitionRoleDetails[i].AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _demolitionRoleDetails[i].MouseFilter = Control.MouseFilterEnum.Ignore;
            button.AddChild(_demolitionRoleDetails[i]);
        }
        _demolitionRoleButtons[0].ButtonPressed = true;

        _demolitionLoadoutTitle = Label("REGULATION LOADOUT", 12, new Color(0.48f, 0.72f, 0.64f));
        _demolitionLoadoutTitle.Position = new Vector2(44, 504);
        _demolitionLoadoutTitle.Size = new Vector2(300, 22);
        band.AddChild(_demolitionLoadoutTitle);
        _demolitionLoadout = Label(
            "M4A1 STANDARD  //  COMMON 5.56  x120\nSTANDARD CARRIER  //  PATROL HELMET\n2 FRAG  //  2 PLATES  //  CLASS SKILL ENABLED",
            14,
            new Color(0.82f, 0.88f, 0.85f));
        _demolitionLoadout.Position = new Vector2(44, 536);
        _demolitionLoadout.Size = new Vector2(660, 86);
        _demolitionLoadout.AddThemeConstantOverride("line_spacing", 5);
        band.AddChild(_demolitionLoadout);
        _demolitionReadyStatus = Label("FAIR PLAY KIT  //  EXTRACTION WALLET UNAFFECTED", 12, new Color(1.0f, 0.67f, 0.31f));
        _demolitionReadyStatus.Position = new Vector2(44, 664);
        _demolitionReadyStatus.Size = new Vector2(660, 24);
        band.AddChild(_demolitionReadyStatus);

        _demolitionBackButton = Button("BACK", new Vector2(44, 958), new Vector2(190, 50));
        _demolitionBackButton.Pressed += () => EmitSignal(SignalName.DemolitionBackRequested);
        band.AddChild(_demolitionBackButton);
        _demolitionDeployButton = Button("DEPLOY DEMOLITION TEAM", new Vector2(250, 958), new Vector2(454, 50));
        _demolitionDeployButton.AddThemeColorOverride("font_color", new Color(0.08f, 0.035f, 0.01f));
        _demolitionDeployButton.AddThemeStyleboxOverride(
            "normal",
            FlatStyle(new Color(1.0f, 0.58f, 0.22f), new Color(1.0f, 0.8f, 0.48f), 2));
        _demolitionDeployButton.Pressed += () => EmitSignal(
            SignalName.DemolitionDeploymentRequested,
            (int)_selectedDemolitionRole);
        band.AddChild(_demolitionDeployButton);
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
        _selectedDemolitionRole = role;
        for (var i = 0; i < _demolitionRoleButtons.Length; i++)
        {
            _demolitionRoleButtons[i].SetPressedNoSignal(i == (int)role);
        }
        var spec = OperatorRoles.Spec(role);
        _demolitionReadyStatus.Text = GameLocalization.Format(
            "demolition_role_ready",
            _language,
            "{0} READY  //  REGULATION KIT  //  EXTRACTION WALLET UNAFFECTED",
            OperatorRoles.RoleName(role, _language));
        _demolitionReadyStatus.AddThemeColorOverride("font_color", spec.Accent);
    }

    public void ShowOperationsOffice(string status = "FIELD TEAM STANDING BY  //  HELIPAD CLEAR")
    {
        _squadLobby.Visible = false;
        _demolitionBriefingRoot.Visible = false;
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
        _squadLobby.Visible = false;
        _operationsOfficeRoot.Visible = false;
        _demolitionBriefingRoot.Visible = true;
        _gameplayHudRoot.Visible = false;
        _classSkillRoot.Visible = false;
        _squadRoster.Visible = false;
        SelectDemolitionRole(_selectedDemolitionRole);
        RefreshOperationsOfficeLanguage();
    }

    public void HideOperationsMenus()
    {
        if (IsInstanceValid(_operationsOfficeRoot))
        {
            _operationsOfficeRoot.Visible = false;
        }
        if (IsInstanceValid(_demolitionBriefingRoot))
        {
            _demolitionBriefingRoot.Visible = false;
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

    public void PressDemolitionRoleForDiagnostics(OperatorRole role)
    {
        var roleIndex = (int)role;
        if (roleIndex >= 0 && roleIndex < _demolitionRoleButtons.Length)
        {
            PressButtonForDiagnostics(_demolitionRoleButtons[roleIndex]);
        }
    }

    public void PressDemolitionBackForDiagnostics()
        => PressButtonForDiagnostics(_demolitionBackButton);

    public void PressDemolitionDeployForDiagnostics()
        => PressButtonForDiagnostics(_demolitionDeployButton);

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
        _demolitionEntryDetail.Text = Text("operations_demolition_detail", "FIXED KIT  //  PLANT OR DEFUSE  //  A / B SITES");
        _operationsQuitButton.Text = Text("operations_exit", "EXIT TO DESKTOP");
        _demolitionTitle.Text = Text("demolition_title", "DEMOLITION BRIEFING");
        _demolitionSubtitle.Text = Text("demolition_subtitle", "FREIGHT TERMINAL  //  ATTACKING ELEMENT");
        _demolitionRulesTitle.Text = Text("demolition_rules_title", "ENGAGEMENT RULES");
        _demolitionRules.Text = Text(
            "demolition_rules",
            "PLANT AT SITE A OR B  //  HOLD F\nDEFEND THE DEVICE UNTIL DETONATION\nELIMINATION ALSO ENDS THE ROUND  //  NO LOOT BANKING");
        _demolitionRoleTitle.Text = Text("demolition_select_role", "SELECT OPERATOR");
        _demolitionLoadoutTitle.Text = Text("demolition_loadout_title", "REGULATION LOADOUT");
        _demolitionLoadout.Text = Text(
            "demolition_loadout",
            "M4A1 STANDARD  //  COMMON 5.56  x120\nSTANDARD CARRIER  //  PATROL HELMET\n2 FRAG  //  2 PLATES  //  CLASS SKILL ENABLED");
        _demolitionBackButton.Text = Text("demolition_back", "BACK");
        _demolitionDeployButton.Text = Text("demolition_deploy", "DEPLOY DEMOLITION TEAM");
        _resultOfficeButton.Text = Text("operations_return", "RETURN TO OPERATIONS OFFICE");
        var roles = new[] { OperatorRole.Assault, OperatorRole.Medic, OperatorRole.Recon };
        for (var i = 0; i < roles.Length; i++)
        {
            _demolitionRoleNames[i].Text = OperatorRoles.RoleName(roles[i], _language);
            _demolitionRoleDetails[i].Text = $"{OperatorRoles.SkillName(roles[i], _language)}\n{OperatorRoles.Description(roles[i], _language)}";
        }
        RefreshOperationsOfficeProfile();
        SelectDemolitionRole(_selectedDemolitionRole);
    }
}
