using Godot;

namespace OperationSteelTide;

/// <summary>
/// Presents demolition briefing state supplied through <see cref="SetLanguage"/> and emits
/// role, deployment, and back intent. The view never mutates world or progression state.
/// </summary>
[GlobalClass]
public partial class DemolitionBriefingView : ColorRect
{
    [Signal] public delegate void BackRequestedEventHandler();
    [Signal] public delegate void DeployRequestedEventHandler(
        int role,
        int primaryPlatform,
        int buildTier,
        int sidearmPlatform);

    private Label _title = null!;
    private Label _subtitle = null!;
    private Label _rulesTitle = null!;
    private Label _rules = null!;
    private Label _roleTitle = null!;
    private Label _loadoutTitle = null!;
    private Label _primaryLabel = null!;
    private Label _buildLabel = null!;
    private Label _sidearmLabel = null!;
    private Label _loadout = null!;
    private Label _readyStatus = null!;
    private Label _intelCaption = null!;
    private Label _arenaName = null!;
    private Label _arenaProfile = null!;
    private Button _backButton = null!;
    private Button _deployButton = null!;
    private OptionButton _primaryOption = null!;
    private OptionButton _buildOption = null!;
    private OptionButton _sidearmOption = null!;
    private readonly Button[] _roleButtons = new Button[3];
    private readonly Label[] _roleNames = new Label[3];
    private readonly Label[] _roleDetails = new Label[3];
    private OperatorRole _selectedRole = OperatorRole.Assault;
    private string _language = "en";

    public OperatorRole SelectedRole => _selectedRole;
    public WeaponPlatform SelectedPrimaryPlatform => (WeaponPlatform)_primaryOption.GetSelectedId();
    public int SelectedBuildTier => (int)_buildOption.GetSelectedId();
    public WeaponPlatform SelectedSidearmPlatform => (WeaponPlatform)_sidearmOption.GetSelectedId();
    public bool UiReady
        => IsInstanceValid(_title)
        && IsInstanceValid(_arenaName)
        && IsInstanceValid(_backButton)
        && IsInstanceValid(_deployButton)
        && IsInstanceValid(_primaryOption)
        && IsInstanceValid(_buildOption)
        && IsInstanceValid(_sidearmOption)
        && IsInstanceValid(_roleButtons[0])
        && IsInstanceValid(_roleButtons[1])
        && IsInstanceValid(_roleButtons[2]);
    public bool IntentSignalsConnected
        => HasConnections(SignalName.BackRequested)
        && HasConnections(SignalName.DeployRequested)
        && _backButton.HasConnections(BaseButton.SignalName.Pressed)
        && _deployButton.HasConnections(BaseButton.SignalName.Pressed)
        && _primaryOption.HasConnections(OptionButton.SignalName.ItemSelected)
        && _buildOption.HasConnections(OptionButton.SignalName.ItemSelected)
        && _sidearmOption.HasConnections(OptionButton.SignalName.ItemSelected)
        && _roleButtons[0].HasConnections(BaseButton.SignalName.Pressed)
        && _roleButtons[1].HasConnections(BaseButton.SignalName.Pressed)
        && _roleButtons[2].HasConnections(BaseButton.SignalName.Pressed);

    public override void _Ready()
    {
        BindNodes();
        ConnectIntentSignals();
        SetLanguage(_language);
        SelectRole(_selectedRole);
    }

    public void SetLanguage(string language)
    {
        _language = GameLocalization.IsChinese(language) ? "zh" : "en";
        _title.Text = Text("demolition_title", "DEMOLITION BRIEFING");
        _subtitle.Text = Text("demolition_subtitle", "TIDEFORGE ARENA  //  ATTACKING ELEMENT");
        _rulesTitle.Text = Text("demolition_rules_title", "ENGAGEMENT RULES");
        _rules.Text = Text(
            "demolition_rules",
            "12 REGULATION ROUNDS  //  6:6 ENTERS WIN-BY-TWO OVERTIME\nPLANT AT SITE A OR B  //  HOLD F\nELIMINATION OR OBJECTIVE TIMEOUT ALSO ENDS THE ROUND");
        _roleTitle.Text = Text("demolition_select_role", "SELECT OPERATOR");
        _loadoutTitle.Text = Text("demolition_loadout_title", "CUSTOM MATCH LOADOUT");
        _primaryLabel.Text = Text("demolition_primary", "PRIMARY");
        _buildLabel.Text = Text("demolition_build", "BUILD");
        _sidearmLabel.Text = Text("demolition_sidearm", "SIDEARM");
        PopulateLoadoutOptions();
        RefreshLoadoutSummary();
        _intelCaption.Text = Text("demolition_arena_selected", "SELECTED BATTLESPACE");
        _arenaName.Text = Text("demolition_arena_name", "TIDEFORGE ARENA");
        _arenaProfile.Text = Text(
            "demolition_arena_profile",
            "THREE ATTACK ROUTES  //  MID ROTATION\nA  OPEN FOUNDRY YARD  //  B  ENCLOSED ASSEMBLY HALL");
        _backButton.Text = Text("demolition_back", "BACK");
        _deployButton.Text = Text("demolition_deploy", "DEPLOY DEMOLITION TEAM");
        var roles = new[] { OperatorRole.Assault, OperatorRole.Medic, OperatorRole.Recon };
        for (var index = 0; index < roles.Length; index++)
        {
            var role = roles[index];
            _roleNames[index].Text = OperatorRoles.RoleName(role, _language);
            _roleDetails[index].Text = $"{OperatorRoles.SkillName(role, _language)}\n{OperatorRoles.Description(role, _language)}";
        }
        SelectRole(_selectedRole);
    }

    public void SelectRole(OperatorRole role)
    {
        _selectedRole = role;
        for (var index = 0; index < _roleButtons.Length; index++)
        {
            _roleButtons[index].SetPressedNoSignal(index == (int)role);
        }
        var spec = OperatorRoles.Spec(role);
        _readyStatus.Text = GameLocalization.Format(
            "demolition_role_ready",
            _language,
            "{0} READY  //  CUSTOM KIT  //  EXTRACTION WALLET UNAFFECTED",
            OperatorRoles.RoleName(role, _language));
        _readyStatus.AddThemeColorOverride("font_color", spec.Accent);
    }

    public bool LanguageMatches(string language)
    {
        var normalized = GameLocalization.IsChinese(language) ? "zh" : "en";
        return _language == normalized
            && _title.Text == GameLocalization.Get("demolition_title", normalized, "DEMOLITION BRIEFING")
            && _arenaName.Text == GameLocalization.Get("demolition_arena_name", normalized, "TIDEFORGE ARENA")
            && _deployButton.Text == GameLocalization.Get("demolition_deploy", normalized, "DEPLOY DEMOLITION TEAM")
            && _roleNames[(int)OperatorRole.Medic].Text == OperatorRoles.RoleName(OperatorRole.Medic, normalized)
            && _primaryLabel.Text == GameLocalization.Get("demolition_primary", normalized, "PRIMARY")
            && _sidearmLabel.Text == GameLocalization.Get("demolition_sidearm", normalized, "SIDEARM");
    }

    public void PressRoleForDiagnostics(OperatorRole role)
    {
        var index = (int)role;
        if (index >= 0 && index < _roleButtons.Length)
        {
            _roleButtons[index].EmitSignal(BaseButton.SignalName.Pressed);
        }
    }

    public void PressBackForDiagnostics() => _backButton.EmitSignal(BaseButton.SignalName.Pressed);

    public void PressDeployForDiagnostics() => _deployButton.EmitSignal(BaseButton.SignalName.Pressed);

    public void SelectLoadoutForDiagnostics(
        WeaponPlatform primary,
        int buildTier,
        WeaponPlatform sidearm)
    {
        SelectOptionById(_primaryOption, (int)primary);
        SelectOptionById(_buildOption, Mathf.Clamp(buildTier, 0, 2));
        SelectOptionById(_sidearmOption, (int)sidearm);
        RefreshLoadoutSummary();
    }

    private void BindNodes()
    {
        var band = GetNode<Control>("Band");
        _title = band.GetNode<Label>("Title");
        _subtitle = band.GetNode<Label>("Subtitle");
        _rulesTitle = band.GetNode<Label>("RulesTitle");
        _rules = band.GetNode<Label>("Rules");
        _roleTitle = band.GetNode<Label>("RoleTitle");
        _loadoutTitle = band.GetNode<Label>("LoadoutTitle");
        _primaryLabel = band.GetNode<Label>("PrimaryLabel");
        _buildLabel = band.GetNode<Label>("BuildLabel");
        _sidearmLabel = band.GetNode<Label>("SidearmLabel");
        _primaryOption = band.GetNode<OptionButton>("PrimaryOption");
        _buildOption = band.GetNode<OptionButton>("BuildOption");
        _sidearmOption = band.GetNode<OptionButton>("SidearmOption");
        _loadout = band.GetNode<Label>("Loadout");
        _readyStatus = band.GetNode<Label>("ReadyStatus");
        _backButton = band.GetNode<Button>("BackButton");
        _deployButton = band.GetNode<Button>("DeployButton");
        var roles = band.GetNode<Control>("Roles");
        BindRole(roles, 0, "AssaultButton");
        BindRole(roles, 1, "MedicButton");
        BindRole(roles, 2, "ReconButton");
        var arenaIntel = GetNode<Control>("ArenaIntel");
        _intelCaption = arenaIntel.GetNode<Label>("IntelCaption");
        _arenaName = arenaIntel.GetNode<Label>("ArenaName");
        _arenaProfile = arenaIntel.GetNode<Label>("ArenaProfile");
    }

    private void BindRole(Control root, int index, string name)
    {
        _roleButtons[index] = root.GetNode<Button>(name);
        _roleNames[index] = _roleButtons[index].GetNode<Label>("RoleName");
        _roleDetails[index] = _roleButtons[index].GetNode<Label>("RoleDetail");
    }

    private void ConnectIntentSignals()
    {
        _roleButtons[(int)OperatorRole.Assault].Pressed += () => SelectRole(OperatorRole.Assault);
        _roleButtons[(int)OperatorRole.Medic].Pressed += () => SelectRole(OperatorRole.Medic);
        _roleButtons[(int)OperatorRole.Recon].Pressed += () => SelectRole(OperatorRole.Recon);
        _primaryOption.ItemSelected += _ => RefreshLoadoutSummary();
        _buildOption.ItemSelected += _ => RefreshLoadoutSummary();
        _sidearmOption.ItemSelected += _ => RefreshLoadoutSummary();
        _backButton.Pressed += () => EmitSignal(SignalName.BackRequested);
        _deployButton.Pressed += () => EmitSignal(
            SignalName.DeployRequested,
            (int)_selectedRole,
            (int)SelectedPrimaryPlatform,
            SelectedBuildTier,
            (int)SelectedSidearmPlatform);
    }

    private void PopulateLoadoutOptions()
    {
        var selectedPrimary = _primaryOption.ItemCount > 0
            ? (int)_primaryOption.GetSelectedId()
            : (int)WeaponPlatform.M4A1;
        var selectedBuild = _buildOption.ItemCount > 0 ? (int)_buildOption.GetSelectedId() : 1;
        var selectedSidearm = _sidearmOption.ItemCount > 0
            ? (int)_sidearmOption.GetSelectedId()
            : (int)WeaponPlatform.P226;

        _primaryOption.Clear();
        foreach (var platform in new[]
        {
            WeaponPlatform.M4A1,
            WeaponPlatform.AK74,
            WeaponPlatform.MP5A5,
            WeaponPlatform.ScarL
        })
        {
            _primaryOption.AddItem(WeaponName(platform), (int)platform);
        }
        _buildOption.Clear();
        _buildOption.AddItem(Text("demolition_build_cqb", "CQB"), 0);
        _buildOption.AddItem(Text("demolition_build_standard", "STANDARD"), 1);
        _buildOption.AddItem(Text("demolition_build_precision", "PRECISION"), 2);
        _sidearmOption.Clear();
        _sidearmOption.AddItem(WeaponName(WeaponPlatform.P226), (int)WeaponPlatform.P226);
        _sidearmOption.AddItem(WeaponName(WeaponPlatform.M1911), (int)WeaponPlatform.M1911);
        SelectOptionById(_primaryOption, selectedPrimary);
        SelectOptionById(_buildOption, selectedBuild);
        SelectOptionById(_sidearmOption, selectedSidearm);
    }

    private void RefreshLoadoutSummary()
    {
        if (_primaryOption.ItemCount == 0 || _sidearmOption.ItemCount == 0)
        {
            return;
        }
        var primary = WeaponName(SelectedPrimaryPlatform);
        var sidearm = WeaponName(SelectedSidearmPlatform);
        var build = _buildOption.GetItemText(_buildOption.Selected);
        var reserve = WeaponCatalog.Weapon(SelectedPrimaryPlatform).Caliber == AmmoCaliber.Smg ? 150 : 120;
        _loadout.Text = GameLocalization.Format(
            "demolition_custom_loadout",
            _language,
            "{0}  //  {1} BUILD  //  PRIMARY x{3}\n{2}  //  60 PISTOL ROUNDS  //  KEYS 1 / 2 / 3\nSTANDARD CARRIER  //  2 FRAG  //  CLASS SKILL ENABLED",
            primary,
            build,
            sidearm,
            reserve);
    }

    private string WeaponName(WeaponPlatform platform)
    {
        var definition = WeaponCatalog.Weapon(platform);
        return GameLocalization.IsChinese(_language)
            ? GameLocalization.Get(definition.LocalizationKey, _language, definition.ChineseName)
            : definition.Name;
    }

    private static void SelectOptionById(OptionButton option, int id)
    {
        for (var index = 0; index < option.ItemCount; index++)
        {
            if (option.GetItemId(index) == id)
            {
                option.Select(index);
                return;
            }
        }
        option.Select(0);
    }

    private string Text(string key, string english) => GameLocalization.Get(key, _language, english);
}
