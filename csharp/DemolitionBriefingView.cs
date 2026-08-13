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
    [Signal] public delegate void DeployRequestedEventHandler(int role);

    private Label _title = null!;
    private Label _subtitle = null!;
    private Label _rulesTitle = null!;
    private Label _rules = null!;
    private Label _roleTitle = null!;
    private Label _loadoutTitle = null!;
    private Label _loadout = null!;
    private Label _readyStatus = null!;
    private Label _intelCaption = null!;
    private Label _arenaName = null!;
    private Label _arenaProfile = null!;
    private Button _backButton = null!;
    private Button _deployButton = null!;
    private readonly Button[] _roleButtons = new Button[3];
    private readonly Label[] _roleNames = new Label[3];
    private readonly Label[] _roleDetails = new Label[3];
    private OperatorRole _selectedRole = OperatorRole.Assault;
    private string _language = "en";

    public OperatorRole SelectedRole => _selectedRole;
    public bool UiReady
        => IsInstanceValid(_title)
        && IsInstanceValid(_arenaName)
        && IsInstanceValid(_backButton)
        && IsInstanceValid(_deployButton)
        && IsInstanceValid(_roleButtons[0])
        && IsInstanceValid(_roleButtons[1])
        && IsInstanceValid(_roleButtons[2]);
    public bool IntentSignalsConnected
        => HasConnections(SignalName.BackRequested)
        && HasConnections(SignalName.DeployRequested)
        && _backButton.HasConnections(BaseButton.SignalName.Pressed)
        && _deployButton.HasConnections(BaseButton.SignalName.Pressed)
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
            "PLANT AT SITE A OR B  //  HOLD F\nDEFEND THE DEVICE UNTIL DETONATION\nELIMINATION ALSO ENDS THE ROUND  //  NO LOOT BANKING");
        _roleTitle.Text = Text("demolition_select_role", "SELECT OPERATOR");
        _loadoutTitle.Text = Text("demolition_loadout_title", "REGULATION LOADOUT");
        _loadout.Text = Text(
            "demolition_loadout",
            "M4A1 STANDARD  //  COMMON 5.56  x120\nSTANDARD CARRIER  //  PATROL HELMET\n2 FRAG  //  2 PLATES  //  CLASS SKILL ENABLED");
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
            "{0} READY  //  REGULATION KIT  //  EXTRACTION WALLET UNAFFECTED",
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
            && _roleNames[(int)OperatorRole.Medic].Text == OperatorRoles.RoleName(OperatorRole.Medic, normalized);
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

    private void BindNodes()
    {
        var band = GetNode<Control>("Band");
        _title = band.GetNode<Label>("Title");
        _subtitle = band.GetNode<Label>("Subtitle");
        _rulesTitle = band.GetNode<Label>("RulesTitle");
        _rules = band.GetNode<Label>("Rules");
        _roleTitle = band.GetNode<Label>("RoleTitle");
        _loadoutTitle = band.GetNode<Label>("LoadoutTitle");
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
        _backButton.Pressed += () => EmitSignal(SignalName.BackRequested);
        _deployButton.Pressed += () => EmitSignal(SignalName.DeployRequested, (int)_selectedRole);
    }

    private string Text(string key, string english) => GameLocalization.Get(key, _language, english);
}
