using Godot;

namespace OperationSteelTide;

/// <summary>
/// Displays one read-only demolition roster member. The parent view supplies the
/// member snapshot, allegiance, and language; this card emits no gameplay intent.
/// </summary>
[GlobalClass]
public partial class DemolitionTeamMemberCard : PanelContainer
{
    public const string ScenePath = "res://ui/DemolitionTeamMemberCard.tscn";

    private static readonly Color FriendlyColor = new(0.27f, 0.9f, 0.82f);
    private static readonly Color EnemyColor = new(1.0f, 0.35f, 0.29f);
    private static readonly Color DeviceColor = new(1.0f, 0.72f, 0.12f);
    private ColorRect _accent = null!;
    private ColorRect _portraitBackdrop = null!;
    private TextureRect _portrait = null!;
    private Label _role = null!;
    private Label _status = null!;
    private Label _name = null!;
    private Label _youBadge = null!;
    private Label _deviceBadge = null!;
    private DemolitionTeamStatusMember _member;
    private string _language = "en";
    private bool _friendly;
    private bool _hasMember;

    public bool UiReady
        => IsInstanceValid(_accent)
        && IsInstanceValid(_portraitBackdrop)
        && IsInstanceValid(_portrait)
        && IsInstanceValid(_role)
        && IsInstanceValid(_status)
        && IsInstanceValid(_name)
        && IsInstanceValid(_youBadge)
        && IsInstanceValid(_deviceBadge);
    public bool IsLocalPlayer => _hasMember && _member.IsLocalPlayer;
    public bool HasDevice => _hasMember && _member.HasDevice;
    public bool IsAlive => _hasMember && _member.Alive;
    public string DisplayedName => IsInstanceValid(_name) ? _name.Text : string.Empty;

    public override void _Ready()
    {
        BindNodes();
        Refresh();
    }

    public void Apply(DemolitionTeamStatusMember member, bool friendly, string language)
    {
        _member = member;
        _friendly = friendly;
        _language = GameLocalization.IsChinese(language) ? "zh" : "en";
        _hasMember = true;
        Refresh();
    }

    public bool LanguageMatches(string language)
    {
        if (!_hasMember || !UiReady)
        {
            return false;
        }
        var normalized = GameLocalization.IsChinese(language) ? "zh" : "en";
        var expectedDevice = GameLocalization.Get(
            "demolition_device_badge",
            normalized,
            "DEVICE");
        return _language == normalized
            && (!_member.HasDevice || _deviceBadge.Text == expectedDevice);
    }

    private void BindNodes()
    {
        var content = GetNode<Control>("Content");
        _accent = content.GetNode<ColorRect>("Accent");
        _portraitBackdrop = content.GetNode<ColorRect>("PortraitBackdrop");
        _portrait = content.GetNode<TextureRect>("Portrait");
        _role = content.GetNode<Label>("Role");
        _status = content.GetNode<Label>("Status");
        _name = content.GetNode<Label>("Name");
        _youBadge = content.GetNode<Label>("YouBadge");
        _deviceBadge = content.GetNode<Label>("DeviceBadge");
    }

    private void Refresh()
    {
        if (!_hasMember || !UiReady)
        {
            return;
        }

        var teamColor = _friendly ? FriendlyColor : EnemyColor;
        var accentColor = _member.HasDevice ? DeviceColor : teamColor;
        _accent.Color = accentColor;
        _portraitBackdrop.Color = new Color(
            teamColor.R * 0.16f,
            teamColor.G * 0.16f,
            teamColor.B * 0.16f,
            0.96f);
        _portrait.SelfModulate = _member.Alive
            ? teamColor
            : new Color(0.31f, 0.34f, 0.34f);
        _role.Text = RoleGlyph(_member.Role);
        _role.AddThemeColorOverride("font_color", teamColor);
        _status.Text = GameLocalization.Get(
            _member.Alive ? "demolition_team_ready" : "demolition_team_out",
            _language,
            _member.Alive ? "READY" : "OUT");
        _status.AddThemeColorOverride(
            "font_color",
            _member.Alive ? new Color(0.72f, 0.82f, 0.79f) : EnemyColor);
        _name.Text = _member.DisplayName;
        _youBadge.Visible = _member.IsLocalPlayer;
        _youBadge.Text = GameLocalization.Get("you", _language, "YOU");
        _deviceBadge.Visible = _member.HasDevice;
        _deviceBadge.Text = GameLocalization.Get(
            "demolition_device_badge",
            _language,
            "DEVICE");
        Modulate = _member.Alive ? Colors.White : new Color(0.48f, 0.5f, 0.5f, 0.78f);
    }

    private static string RoleGlyph(OperatorRole role)
        => role switch
        {
            OperatorRole.Medic => "+",
            OperatorRole.Recon => "◎",
            OperatorRole.Scavenger => "◆",
            OperatorRole.Locksmith => "⌁",
            _ => "▲"
        };
}
