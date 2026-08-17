using System;
using Godot;

namespace OperationSteelTide;

/// <summary>
/// Presents demolition role and map-pool state. The view emits deployment intent and
/// never mutates world, economy, or progression state.
/// </summary>
[GlobalClass]
public partial class DemolitionBriefingView : ColorRect
{
    [Signal] public delegate void BackRequestedEventHandler();
    [Signal] public delegate void DeployRequestedEventHandler(
        int role,
        int primaryPlatform,
        int buildTier,
        int sidearmPlatform,
        string mapId,
        int sessionMode,
        string address,
        int networkTeam);

    private Label _title = null!;
    private Label _subtitle = null!;
    private Label _rulesTitle = null!;
    private Label _rules = null!;
    private Label _roleTitle = null!;
    private Label _mapTitle = null!;
    private Label _mapCode = null!;
    private Label _mapPosition = null!;
    private Label _mapName = null!;
    private Label _mapSubtitle = null!;
    private Label _mapStatus = null!;
    private Label _readyStatus = null!;
    private Label _intelCaption = null!;
    private Label _arenaName = null!;
    private Label _arenaProfile = null!;
    private Button _previousMapButton = null!;
    private Button _nextMapButton = null!;
    private Button _backButton = null!;
    private Button _deployButton = null!;
    private Button _localButton = null!;
    private Button _hostButton = null!;
    private Button _joinButton = null!;
    private Button _alphaButton = null!;
    private Button _bravoButton = null!;
    private LineEdit _address = null!;
    private Label _sessionTitle = null!;
    private Label _teamTitle = null!;
    private readonly Button[] _roleButtons = new Button[3];
    private readonly Label[] _roleNames = new Label[3];
    private readonly Label[] _roleDetails = new Label[3];
    private OperatorRole _selectedRole = OperatorRole.Assault;
    private string _selectedMapId = DemolitionMapCatalog.TideforgeId;
    private int _browsedMapIndex;
    private string _language = "en";
    private SquadSessionMode _sessionMode = SquadSessionMode.Local;
    private DemolitionNetworkTeam _networkTeam = DemolitionNetworkTeam.Alpha;

    public OperatorRole SelectedRole => _selectedRole;
    public WeaponPlatform SelectedPrimaryPlatform => WeaponPlatform.M4A1;
    public int SelectedBuildTier => 1;
    public WeaponPlatform SelectedSidearmPlatform => WeaponPlatform.P226;
    public string SelectedMapId => _selectedMapId;
    public string BrowsedMapId => BrowsedMap.Id;
    public int BrowsedMapIndex => _browsedMapIndex;
    public bool IsBrowsedMapAvailable => BrowsedMap.Available;
    public int MapOptionCount => DemolitionMapCatalog.Maps.Count;
    public SquadSessionMode SelectedSessionMode => _sessionMode;
    public DemolitionNetworkTeam SelectedNetworkTeam => _networkTeam;
    public string NetworkAddress => _address.Text.Trim();
    public bool IsDeployEnabled => IsInstanceValid(_deployButton) && !_deployButton.Disabled;
    public bool UiReady
        => IsInstanceValid(_title)
        && IsInstanceValid(_mapCode)
        && IsInstanceValid(_mapName)
        && IsInstanceValid(_previousMapButton)
        && IsInstanceValid(_nextMapButton)
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
        && _previousMapButton.HasConnections(BaseButton.SignalName.Pressed)
        && _nextMapButton.HasConnections(BaseButton.SignalName.Pressed)
        && _roleButtons[0].HasConnections(BaseButton.SignalName.Pressed)
        && _roleButtons[1].HasConnections(BaseButton.SignalName.Pressed)
        && _roleButtons[2].HasConnections(BaseButton.SignalName.Pressed);

    private DemolitionMapOffer BrowsedMap => DemolitionMapCatalog.Maps[_browsedMapIndex];

    public override void _Ready()
    {
        BindNodes();
        ConnectIntentSignals();
        SetLanguage(_language);
        SelectRole(_selectedRole);
        SelectMap(_selectedMapId);
        SelectSessionMode(_sessionMode);
    }

    public void SetLanguage(string language)
    {
        _language = GameLocalization.IsChinese(language) ? "zh" : "en";
        _title.Text = Text("demolition_title", "DEMOLITION BRIEFING");
        _subtitle.Text = Text("demolition_subtitle", "5 V 5  //  ATTACK THEN DEFEND");
        _rulesTitle.Text = Text("demolition_rules_title", "ENGAGEMENT RULES");
        _rules.Text = Text(
            "demolition_rules",
            "MR12  //  FIRST TO 13 WINS  //  SIDES SWAP AFTER ROUND 12\n"
            + "6:6 HALFTIME  //  12:12 ENTERS WIN-BY-TWO OVERTIME\n"
            + "EACH ROUND OPENS WITH A 15 SECOND BUY PHASE\n"
            + "BOTH SQUADS START WITH $800  //  EXTRACTION WALLET UNAFFECTED");
        _roleTitle.Text = Text("demolition_select_role", "SELECT OPERATOR");
        _mapTitle.Text = Text("demolition_map_title", "BATTLESPACE  //  12-MAP POOL");
        _intelCaption.Text = Text("demolition_arena_selected", "BATTLESPACE INTELLIGENCE");
        _backButton.Text = Text("demolition_back", "BACK");
        _deployButton.Text = Text("demolition_deploy", "DEPLOY DEMOLITION TEAM");
        _sessionTitle.Text = GameLocalization.IsChinese(_language) ? "\u8054\u673a\u4f1a\u8bdd" : "LAN SESSION";
        _teamTitle.Text = GameLocalization.IsChinese(_language) ? "\u52a0\u5165\u9635\u8425" : "JOIN TEAM";
        _localButton.Text = GameLocalization.IsChinese(_language) ? "\u672c\u5730 + AI" : "LOCAL + AI";
        _hostButton.Text = GameLocalization.IsChinese(_language) ? "\u521b\u5efa\u623f\u95f4" : "HOST";
        _joinButton.Text = GameLocalization.IsChinese(_language) ? "\u52a0\u5165\u623f\u95f4" : "JOIN";
        _alphaButton.Text = GameLocalization.IsChinese(_language) ? "ALPHA  //  \u5148\u653b" : "ALPHA  //  ATTACK FIRST";
        _bravoButton.Text = GameLocalization.IsChinese(_language) ? "BRAVO  //  \u5148\u5b88" : "BRAVO  //  DEFEND FIRST";
        _address.PlaceholderText = GameLocalization.IsChinese(_language)
            ? "\u4e3b\u673a\u5730\u5740\u6216\u5730\u5740:\u7aef\u53e3"
            : "HOST OR HOST:PORT";
        var roles = new[] { OperatorRole.Assault, OperatorRole.Medic, OperatorRole.Recon };
        for (var index = 0; index < roles.Length; index++)
        {
            var role = roles[index];
            _roleNames[index].Text = OperatorRoles.RoleName(role, _language);
            _roleDetails[index].Text = $"{OperatorRoles.SkillName(role, _language)}\n{OperatorRoles.Description(role, _language)}";
        }
        SelectRole(_selectedRole);
        RefreshMap();
        SelectSessionMode(_sessionMode);
    }

    public void SelectRole(OperatorRole role)
    {
        _selectedRole = role;
        for (var index = 0; index < _roleButtons.Length; index++)
        {
            _roleButtons[index].SetPressedNoSignal(index == (int)role);
        }
        _readyStatus.Text = GameLocalization.Format(
            "demolition_role_ready",
            _language,
            "{0} READY  //  BUY LOADOUT IN EACH ROUND  //  EXTRACTION WALLET UNAFFECTED",
            OperatorRoles.RoleName(role, _language));
        _readyStatus.AddThemeColorOverride("font_color", OperatorRoles.Spec(role).Accent);
    }

    public bool SelectMap(string mapId)
    {
        for (var index = 0; index < DemolitionMapCatalog.Maps.Count; index++)
        {
            var offer = DemolitionMapCatalog.Maps[index];
            if (!string.Equals(offer.Id, mapId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            _browsedMapIndex = index;
            if (offer.Available)
            {
                _selectedMapId = offer.Id;
            }
            RefreshMap();
            return offer.Available;
        }
        return false;
    }

    public void BrowseMap(int direction)
    {
        var count = DemolitionMapCatalog.Maps.Count;
        _browsedMapIndex = (_browsedMapIndex + Math.Sign(direction) + count) % count;
        if (BrowsedMap.Available)
        {
            _selectedMapId = BrowsedMap.Id;
        }
        RefreshMap();
    }

    public bool LanguageMatches(string language)
    {
        var normalized = GameLocalization.IsChinese(language) ? "zh" : "en";
        return _language == normalized
            && _title.Text == GameLocalization.Get("demolition_title", normalized, "DEMOLITION BRIEFING")
            && _deployButton.Text == GameLocalization.Get("demolition_deploy", normalized, "DEPLOY DEMOLITION TEAM")
            && _roleNames[(int)OperatorRole.Medic].Text == OperatorRoles.RoleName(OperatorRole.Medic, normalized)
            && _mapName.Text == GameLocalization.Get(BrowsedMap.LocalizationKey, normalized, BrowsedMap.EnglishName);
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

    public void PressPreviousMapForDiagnostics() => _previousMapButton.EmitSignal(BaseButton.SignalName.Pressed);

    public void PressNextMapForDiagnostics() => _nextMapButton.EmitSignal(BaseButton.SignalName.Pressed);

    public void PressMapForDiagnostics(string mapId) => SelectMap(mapId);

    public void SelectNetworkForDiagnostics(
        SquadSessionMode sessionMode,
        DemolitionNetworkTeam team,
        string address = "127.0.0.1")
    {
        _address.Text = address;
        SelectSessionMode(sessionMode);
        SelectNetworkTeam(team);
    }

    public void SelectLoadoutForDiagnostics(WeaponPlatform primary, int buildTier, WeaponPlatform sidearm)
    {
        // Kept as a compatibility hook; demolition weapons are now purchased in-round.
    }

    private void BindNodes()
    {
        var band = GetNode<Control>("Band");
        _title = band.GetNode<Label>("Title");
        _subtitle = band.GetNode<Label>("Subtitle");
        _rulesTitle = band.GetNode<Label>("RulesTitle");
        _rules = band.GetNode<Label>("Rules");
        _roleTitle = band.GetNode<Label>("RoleTitle");
        _mapTitle = band.GetNode<Label>("MapTitle");
        var carousel = band.GetNode<Control>("MapCarousel");
        _previousMapButton = carousel.GetNode<Button>("PreviousMapButton");
        _nextMapButton = carousel.GetNode<Button>("NextMapButton");
        _mapCode = carousel.GetNode<Label>("MapCode");
        _mapPosition = carousel.GetNode<Label>("MapPosition");
        _mapName = carousel.GetNode<Label>("MapName");
        _mapSubtitle = carousel.GetNode<Label>("MapSubtitle");
        _mapStatus = carousel.GetNode<Label>("MapStatus");
        _readyStatus = band.GetNode<Label>("ReadyStatus");
        _backButton = band.GetNode<Button>("BackButton");
        _deployButton = band.GetNode<Button>("DeployButton");
        _sessionTitle = band.GetNode<Label>("SessionTitle");
        _teamTitle = band.GetNode<Label>("TeamTitle");
        _localButton = band.GetNode<Button>("LocalButton");
        _hostButton = band.GetNode<Button>("HostButton");
        _joinButton = band.GetNode<Button>("JoinButton");
        _address = band.GetNode<LineEdit>("Address");
        _alphaButton = band.GetNode<Button>("AlphaButton");
        _bravoButton = band.GetNode<Button>("BravoButton");
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
        _previousMapButton.Pressed += () => BrowseMap(-1);
        _nextMapButton.Pressed += () => BrowseMap(1);
        _backButton.Pressed += () => EmitSignal(SignalName.BackRequested);
        _deployButton.Pressed += EmitDeploymentIntent;
        _localButton.Pressed += () => SelectSessionMode(SquadSessionMode.Local);
        _hostButton.Pressed += () => SelectSessionMode(SquadSessionMode.Host);
        _joinButton.Pressed += () => SelectSessionMode(SquadSessionMode.Join);
        _alphaButton.Pressed += () => SelectNetworkTeam(DemolitionNetworkTeam.Alpha);
        _bravoButton.Pressed += () => SelectNetworkTeam(DemolitionNetworkTeam.Bravo);
    }

    private void EmitDeploymentIntent()
    {
        if (!BrowsedMap.Available)
        {
            return;
        }
        EmitSignal(
            SignalName.DeployRequested,
            (int)_selectedRole,
            (int)SelectedPrimaryPlatform,
            SelectedBuildTier,
            (int)SelectedSidearmPlatform,
            _selectedMapId,
            (int)_sessionMode,
            _address.Text.Trim(),
            (int)_networkTeam);
    }

    private void SelectSessionMode(SquadSessionMode mode)
    {
        _sessionMode = mode;
        _localButton.SetPressedNoSignal(mode == SquadSessionMode.Local);
        _hostButton.SetPressedNoSignal(mode == SquadSessionMode.Host);
        _joinButton.SetPressedNoSignal(mode == SquadSessionMode.Join);
        _address.Editable = mode == SquadSessionMode.Join;
        _address.Modulate = mode == SquadSessionMode.Join ? Colors.White : new Color(0.42f, 0.48f, 0.46f);
        _alphaButton.Disabled = mode != SquadSessionMode.Join;
        _bravoButton.Disabled = mode != SquadSessionMode.Join;
        if (mode != SquadSessionMode.Join)
        {
            SelectNetworkTeam(DemolitionNetworkTeam.Alpha);
        }
    }

    private void SelectNetworkTeam(DemolitionNetworkTeam team)
    {
        _networkTeam = _sessionMode == SquadSessionMode.Join ? team : DemolitionNetworkTeam.Alpha;
        _alphaButton.SetPressedNoSignal(_networkTeam == DemolitionNetworkTeam.Alpha);
        _bravoButton.SetPressedNoSignal(_networkTeam == DemolitionNetworkTeam.Bravo);
    }

    private void RefreshMap()
    {
        if (!IsInstanceValid(_mapName))
        {
            return;
        }
        var offer = BrowsedMap;
        var state = offer.Available
            ? Text("demolition_map_available", "AVAILABLE")
            : Text("demolition_map_locked", "IN DEVELOPMENT");
        _mapCode.Text = offer.Code;
        _mapPosition.Text = $"{_browsedMapIndex + 1:00} / {DemolitionMapCatalog.Maps.Count:00}";
        _mapName.Text = Text(offer.LocalizationKey, offer.EnglishName);
        _mapSubtitle.Text = Text(offer.SubtitleLocalizationKey, offer.EnglishSubtitle);
        _mapStatus.Text = state;
        _mapStatus.AddThemeColorOverride(
            "font_color",
            offer.Available ? new Color(0.38f, 0.92f, 0.66f) : new Color(0.92f, 0.46f, 0.25f));
        _deployButton.Disabled = !offer.Available;
        _arenaName.Text = _mapName.Text;
        _arenaProfile.Text = offer.Available
            ? Text(offer.ProfileLocalizationKey, offer.EnglishProfile)
            : _mapSubtitle.Text + "\n" + Text("demolition_map_locked_detail", "NOT YET AVAILABLE FOR DEPLOYMENT");
    }

    private string Text(string key, string english) => GameLocalization.Get(key, _language, english);
}
