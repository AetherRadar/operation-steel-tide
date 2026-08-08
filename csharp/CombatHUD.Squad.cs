using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public partial class CombatHUD
{
    public event Action<int, int, string>? SquadDeploymentRequested;
    public event Action<int>? SquadOrderRequested;

    public bool IsSquadLobbyVisible => IsInstanceValid(_squadLobby) && _squadLobby.Visible;

    private ColorRect _squadLobby = null!;
    private Label _squadLobbyTitle = null!;
    private Label _squadLobbySubtitle = null!;
    private Label _squadSessionStatus = null!;
    private LineEdit _squadAddress = null!;
    private Button _localSquadButton = null!;
    private Button _hostSquadButton = null!;
    private Button _joinSquadButton = null!;
    private Button _deploySquadButton = null!;
    private Label _roleCaption = null!;
    private readonly Button[] _roleButtons = new Button[3];
    private readonly Label[] _roleNameLabels = new Label[3];
    private readonly Label[] _roleSkillLabels = new Label[3];
    private readonly Label[] _roleDescriptions = new Label[3];
    private Control _squadRoster = null!;
    private Label _squadRosterTitle = null!;
    private readonly Label[] _squadMemberLabels = new Label[3];
    private readonly Label[] _squadSkillLabels = new Label[3];
    private readonly ProgressBar[] _squadHealthBars = new ProgressBar[3];
    private Control _classSkillRoot = null!;
    private Label _classSkillLabel = null!;
    private ProgressBar _classSkillBar = null!;
    private Label _squadOrderLabel = null!;
    private readonly Button[] _orderButtons = new Button[3];
    private OperatorRole _selectedRole = OperatorRole.Assault;
    private SquadSessionMode _selectedSessionMode = SquadSessionMode.Local;
    private OperatorRole _displayedRole = OperatorRole.Assault;
    private SquadOrder _displayedOrder = SquadOrder.Follow;
    private float _displayedCooldown;
    private float _displayedCooldownMax = 1.0f;
    private bool _displayedSkillActive;
    private bool _displayedSkillAction;

    private void BuildSquadHud(Control root)
    {
        BuildSquadRoster(root);
        BuildClassSkillHud(root);
        BuildSquadLobby(root);
    }

    private void BuildSquadRoster(Control root)
    {
        _squadRoster = new Control
        {
            Position = new Vector2(28, 286),
            Size = new Vector2(250, 158),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        root.AddChild(_squadRoster);
        var background = new ColorRect
        {
            Color = new Color(0.01f, 0.018f, 0.02f, 0.78f),
            Size = new Vector2(250, 158),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _squadRoster.AddChild(background);
        background.AddChild(new ColorRect
        {
            Color = new Color(0.22f, 0.82f, 0.68f, 0.9f),
            Size = new Vector2(3, 158),
            MouseFilter = Control.MouseFilterEnum.Ignore
        });
        _squadRosterTitle = Label("SQUAD  //  3 OPERATORS", 12, new Color(0.45f, 0.88f, 0.74f));
        _squadRosterTitle.Position = new Vector2(16, 10);
        _squadRosterTitle.Size = new Vector2(218, 20);
        _squadRoster.AddChild(_squadRosterTitle);
        for (var i = 0; i < 3; i++)
        {
            var y = 38 + i * 38;
            _squadMemberLabels[i] = Label($"{i + 1}  --", 12, new Color(0.68f, 0.76f, 0.73f));
            _squadMemberLabels[i].Position = new Vector2(16, y);
            _squadMemberLabels[i].Size = new Vector2(158, 20);
            _squadMemberLabels[i].ClipText = true;
            _squadRoster.AddChild(_squadMemberLabels[i]);
            _squadSkillLabels[i] = Label("H READY", 10, new Color(0.45f, 0.88f, 0.74f));
            _squadSkillLabels[i].Position = new Vector2(174, y + 1);
            _squadSkillLabels[i].Size = new Vector2(60, 18);
            _squadSkillLabels[i].HorizontalAlignment = HorizontalAlignment.Right;
            _squadRoster.AddChild(_squadSkillLabels[i]);
            _squadHealthBars[i] = new ProgressBar
            {
                Position = new Vector2(16, y + 22),
                Size = new Vector2(218, 4),
                MinValue = 0,
                MaxValue = 100,
                Value = 100,
                ShowPercentage = false,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            _squadRoster.AddChild(_squadHealthBars[i]);
        }
    }

    private void BuildClassSkillHud(Control root)
    {
        _classSkillRoot = new Control
        {
            Size = new Vector2(430, 92),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _classSkillRoot.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
        _classSkillRoot.Position = new Vector2(-215, -122);
        root.AddChild(_classSkillRoot);
        _classSkillRoot.AddChild(new ColorRect
        {
            Color = new Color(0.008f, 0.014f, 0.016f, 0.82f),
            Size = new Vector2(430, 46),
            MouseFilter = Control.MouseFilterEnum.Ignore
        });
        _classSkillLabel = Label("H  COMBAT OVERDRIVE  //  READY", 13, OperatorRoles.Spec(_displayedRole).Accent);
        _classSkillLabel.Position = new Vector2(15, 8);
        _classSkillLabel.Size = new Vector2(400, 20);
        _classSkillLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _classSkillRoot.AddChild(_classSkillLabel);
        _classSkillBar = new ProgressBar
        {
            Position = new Vector2(26, 33),
            Size = new Vector2(378, 4),
            MinValue = 0,
            MaxValue = 1,
            Value = 1,
            ShowPercentage = false,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _classSkillRoot.AddChild(_classSkillBar);

        _squadOrderLabel = Label("F1 FOLLOW    F2 HOLD    F3 MOVE", 12, new Color(0.73f, 0.81f, 0.78f));
        _squadOrderLabel.Position = new Vector2(0, 53);
        _squadOrderLabel.Size = new Vector2(430, 20);
        _squadOrderLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _classSkillRoot.AddChild(_squadOrderLabel);
        var buttonGroup = new ButtonGroup();
        for (var i = 0; i < 3; i++)
        {
            var order = (SquadOrder)i;
            var button = Button($"F{i + 1}", new Vector2(82 + i * 92, 73), new Vector2(82, 26));
            button.ToggleMode = true;
            button.ButtonGroup = buttonGroup;
            button.FocusMode = Control.FocusModeEnum.None;
            button.AddThemeFontSizeOverride("font_size", 11);
            button.Pressed += () => SquadOrderRequested?.Invoke((int)order);
            _classSkillRoot.AddChild(button);
            _orderButtons[i] = button;
        }
        _orderButtons[0].ButtonPressed = true;
    }

    private void BuildSquadLobby(Control root)
    {
        _squadLobby = new ColorRect
        {
            Color = new Color(0.002f, 0.006f, 0.007f, 0.985f),
            MouseFilter = Control.MouseFilterEnum.Stop,
            Visible = false
        };
        _squadLobby.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(_squadLobby);

        var panel = new Control
        {
            Size = new Vector2(1180, 680),
            Scale = Vector2.One * 1.5f
        };
        panel.SetAnchorsPreset(Control.LayoutPreset.Center);
        panel.Position = new Vector2(-885, -510);
        _squadLobby.AddChild(panel);
        panel.AddChild(new ColorRect
        {
            Size = new Vector2(1180, 3),
            Color = new Color(0.22f, 0.85f, 0.69f)
        });
        panel.AddChild(new ColorRect
        {
            Position = new Vector2(0, 72),
            Size = new Vector2(1180, 1),
            Color = new Color(0.13f, 0.22f, 0.2f, 0.8f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        });
        _squadLobbyTitle = Label("OPERATION LOADOUT", 28, new Color(0.86f, 0.96f, 0.92f));
        _squadLobbyTitle.Position = new Vector2(18, 13);
        _squadLobbyTitle.Size = new Vector2(470, 36);
        panel.AddChild(_squadLobbyTitle);
        _squadLobbySubtitle = Label("STRIKE TEAM PREPARATION  //  HARBOR EXCLUSION ZONE", 11, new Color(0.48f, 0.65f, 0.61f));
        _squadLobbySubtitle.Position = new Vector2(20, 48);
        _squadLobbySubtitle.Size = new Vector2(520, 20);
        panel.AddChild(_squadLobbySubtitle);

        var roleRail = new ColorRect
        {
            Position = new Vector2(0, 88),
            Size = new Vector2(218, 468),
            Color = new Color(0.008f, 0.014f, 0.016f, 0.94f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        panel.AddChild(roleRail);
        roleRail.AddChild(new ColorRect
        {
            Size = new Vector2(3, 468),
            Color = new Color(0.32f, 0.84f, 0.7f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        });
        _roleCaption = Label("SELECT OPERATOR", 10, new Color(0.45f, 0.64f, 0.59f));
        _roleCaption.Position = new Vector2(14, 12);
        _roleCaption.Size = new Vector2(190, 18);
        roleRail.AddChild(_roleCaption);

        var group = new ButtonGroup();
        var roles = new[] { OperatorRole.Assault, OperatorRole.Medic, OperatorRole.Recon };
        for (var i = 0; i < roles.Length; i++)
        {
            var role = roles[i];
            var spec = OperatorRoles.Spec(role);
            _roleButtons[i] = DeploymentSegment(new Vector2(12, 38 + i * 132), new Vector2(194, 120), spec.Accent);
            _roleButtons[i].ToggleMode = true;
            _roleButtons[i].ButtonGroup = group;
            _roleButtons[i].FocusMode = Control.FocusModeEnum.None;
            _roleButtons[i].Pressed += () => SelectSquadRole(role);
            roleRail.AddChild(_roleButtons[i]);

            var glyph = Label(role switch { OperatorRole.Medic => "+", OperatorRole.Recon => "\u25c9", _ => "\u25b2" }, 28, spec.Accent);
            glyph.Position = new Vector2(12, 8);
            glyph.Size = new Vector2(34, 36);
            glyph.HorizontalAlignment = HorizontalAlignment.Center;
            glyph.MouseFilter = Control.MouseFilterEnum.Ignore;
            _roleButtons[i].AddChild(glyph);
            _roleNameLabels[i] = Label(spec.Name, 14, spec.Accent.Lightened(0.15f));
            _roleNameLabels[i].Position = new Vector2(54, 10);
            _roleNameLabels[i].Size = new Vector2(128, 22);
            _roleNameLabels[i].MouseFilter = Control.MouseFilterEnum.Ignore;
            _roleButtons[i].AddChild(_roleNameLabels[i]);
            _roleSkillLabels[i] = Label(spec.SkillName, 9, new Color(0.55f, 0.68f, 0.64f));
            _roleSkillLabels[i].Position = new Vector2(54, 34);
            _roleSkillLabels[i].Size = new Vector2(128, 18);
            _roleSkillLabels[i].ClipText = true;
            _roleSkillLabels[i].MouseFilter = Control.MouseFilterEnum.Ignore;
            _roleButtons[i].AddChild(_roleSkillLabels[i]);
            _roleDescriptions[i] = Label(spec.Description, 9, new Color(0.68f, 0.77f, 0.74f));
            _roleDescriptions[i].Position = new Vector2(14, 61);
            _roleDescriptions[i].Size = new Vector2(166, 50);
            _roleDescriptions[i].AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _roleDescriptions[i].MouseFilter = Control.MouseFilterEnum.Ignore;
            _roleButtons[i].AddChild(_roleDescriptions[i]);
        }
        _roleButtons[0].ButtonPressed = true;
        BuildDeploymentStore(panel);

        var sessionBand = new ColorRect
        {
            Position = new Vector2(0, 572),
            Size = new Vector2(1180, 88),
            Color = new Color(0.007f, 0.013f, 0.014f, 0.96f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        panel.AddChild(sessionBand);
        sessionBand.AddChild(new ColorRect
        {
            Size = new Vector2(1180, 1),
            Color = new Color(0.18f, 0.3f, 0.27f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        });
        _squadSessionStatus = Label("LOCAL STRIKE TEAM  //  2 AI OPERATORS READY", 11, new Color(0.45f, 0.86f, 0.72f));
        _squadSessionStatus.Position = new Vector2(16, 8);
        _squadSessionStatus.Size = new Vector2(820, 20);
        sessionBand.AddChild(_squadSessionStatus);

        var sessionGroup = new ButtonGroup();
        _localSquadButton = DeploymentSegment(new Vector2(16, 36), new Vector2(128, 38), new Color(0.32f, 0.9f, 0.68f));
        _localSquadButton.Text = "LOCAL + AI";
        _localSquadButton.ToggleMode = true;
        _localSquadButton.ButtonGroup = sessionGroup;
        _localSquadButton.Pressed += () => SelectSessionMode(SquadSessionMode.Local);
        sessionBand.AddChild(_localSquadButton);
        _hostSquadButton = DeploymentSegment(new Vector2(150, 36), new Vector2(128, 38), new Color(0.3f, 0.74f, 1.0f));
        _hostSquadButton.Text = "HOST LAN";
        _hostSquadButton.ToggleMode = true;
        _hostSquadButton.ButtonGroup = sessionGroup;
        _hostSquadButton.Pressed += () => SelectSessionMode(SquadSessionMode.Host);
        sessionBand.AddChild(_hostSquadButton);
        _joinSquadButton = DeploymentSegment(new Vector2(284, 36), new Vector2(128, 38), new Color(0.3f, 0.74f, 1.0f));
        _joinSquadButton.Text = "JOIN LAN";
        _joinSquadButton.ToggleMode = true;
        _joinSquadButton.ButtonGroup = sessionGroup;
        _joinSquadButton.Pressed += () => SelectSessionMode(SquadSessionMode.Join);
        sessionBand.AddChild(_joinSquadButton);
        _localSquadButton.ButtonPressed = true;

        _squadAddress = new LineEdit
        {
            Text = "127.0.0.1",
            PlaceholderText = "HOST ADDRESS",
            Position = new Vector2(424, 36),
            Size = new Vector2(236, 38),
            ClearButtonEnabled = true
        };
        _squadAddress.AddThemeFontSizeOverride("font_size", 12);
        sessionBand.AddChild(_squadAddress);
        _deploySquadButton = Button("\u25b6  CONFIRM KIT & DEPLOY", new Vector2(866, 20), new Vector2(294, 56));
        _deploySquadButton.AddThemeFontSizeOverride("font_size", 15);
        _deploySquadButton.AddThemeColorOverride("font_color", new Color(0.03f, 0.08f, 0.065f));
        _deploySquadButton.AddThemeColorOverride("font_hover_color", new Color(0.01f, 0.04f, 0.03f));
        _deploySquadButton.AddThemeStyleboxOverride("normal", FlatStyle(new Color(0.33f, 0.88f, 0.69f), new Color(0.56f, 1.0f, 0.82f), 2));
        _deploySquadButton.AddThemeStyleboxOverride("hover", FlatStyle(new Color(0.48f, 0.98f, 0.79f), Colors.White, 2));
        _deploySquadButton.AddThemeStyleboxOverride("pressed", FlatStyle(new Color(0.23f, 0.72f, 0.55f), new Color(0.56f, 1.0f, 0.82f), 2));
        _deploySquadButton.Pressed += RequestSquadDeployment;
        sessionBand.AddChild(_deploySquadButton);
        SelectSessionMode(SquadSessionMode.Local);
    }

    private void SelectSquadRole(OperatorRole role)
    {
        _selectedRole = role;
        var spec = OperatorRoles.Spec(role);
        _squadSessionStatus.Text = GameLocalization.IsChinese(_language)
            ? $"\u5df2\u9009\u62e9 {spec.ChineseName}  //  \u53ef\u5355\u4eba\u5e26 AI \u6216\u52a0\u5165\u5c40\u57df\u7f51"
            : $"{spec.Name} SELECTED  //  LOCAL AI OR LAN READY";
        _squadSessionStatus.AddThemeColorOverride("font_color", spec.Accent);
        RefreshDeploymentStore();
    }

    private void SelectSessionMode(SquadSessionMode mode)
    {
        _selectedSessionMode = mode;
        _localSquadButton.SetPressedNoSignal(mode == SquadSessionMode.Local);
        _hostSquadButton.SetPressedNoSignal(mode == SquadSessionMode.Host);
        _joinSquadButton.SetPressedNoSignal(mode == SquadSessionMode.Join);
        _squadAddress.Editable = mode != SquadSessionMode.Local;
        _squadAddress.Modulate = mode == SquadSessionMode.Local
            ? new Color(0.42f, 0.48f, 0.46f)
            : Colors.White;
        _squadSessionStatus.Text = mode switch
        {
            SquadSessionMode.Host => GameLocalization.IsChinese(_language)
                ? "\u521b\u5efa\u5c40\u57df\u7f51\u5c0f\u961f  //  \u7a7a\u4f4d\u7531 AI \u8865\u9f50"
                : "HOST LAN STRIKE TEAM  //  AI FILLS OPEN SLOTS",
            SquadSessionMode.Join => GameLocalization.IsChinese(_language)
                ? "\u52a0\u5165\u5c40\u57df\u7f51\u5c0f\u961f  //  \u8f93\u5165\u4e3b\u673a\u5730\u5740"
                : "JOIN LAN STRIKE TEAM  //  ENTER HOST ADDRESS",
            _ => GameLocalization.IsChinese(_language)
                ? "\u672c\u5730\u7a81\u51fb\u5c0f\u961f  //  2 \u540d AI \u5e72\u5458\u5df2\u5c31\u7eea"
                : "LOCAL STRIKE TEAM  //  2 AI OPERATORS READY"
        };
        RefreshSquadDeployAction();
    }

    private void RequestSquadDeployment()
    {
        SquadDeploymentRequested?.Invoke((int)_selectedRole, (int)_selectedSessionMode, _squadAddress.Text.Trim());
    }

    private void RefreshSquadDeployAction()
    {
        if (!IsInstanceValid(_deploySquadButton))
        {
            return;
        }
        var affordable = DeploymentProjectedBalance >= 0;
        _deploySquadButton.Disabled = !affordable;
        _deploySquadButton.Text = GameLocalization.IsChinese(_language)
            ? affordable
                ? $"\u25b6  \u786e\u8ba4\u6574\u5907\u5e76\u90e8\u7f72  //  {DeploymentSelectedCost}"
                : "\u4f59\u989d\u4e0d\u8db3  //  \u8c03\u6574\u6574\u5907"
            : affordable
                ? $"\u25b6  CONFIRM KIT & DEPLOY  //  {DeploymentSelectedCost}"
                : "INSUFFICIENT BALANCE  //  ADJUST KIT";
    }

    public void ShowSquadLobby(string status = "LOCAL SQUAD")
    {
        _squadLobby.Visible = true;
        _squadSessionStatus.Text = status;
        _classSkillRoot.Visible = false;
        _squadRoster.Visible = false;
    }

    public void HideSquadLobby()
    {
        _squadLobby.Visible = false;
        _classSkillRoot.Visible = true;
        _squadRoster.Visible = true;
    }

    public void SetSquadStatus(string status)
    {
        _squadSessionStatus.Text = status;
        var source = status.Contains("HOST", StringComparison.OrdinalIgnoreCase)
            ? "HOST"
            : status.Contains("CONNECT", StringComparison.OrdinalIgnoreCase) ? "LAN" : "LOCAL + AI";
        if (GameLocalization.IsChinese(_language))
        {
            source = source switch { "HOST" => "\u4e3b\u673a", "LAN" => "\u5c40\u57df\u7f51", _ => "\u672c\u5730 + AI" };
            _squadRosterTitle.Text = $"\u5c0f\u961f  //  {source}";
        }
        else
        {
            _squadRosterTitle.Text = $"SQUAD  //  {source}";
        }
    }

    public void SetSquadRoster(IReadOnlyList<SquadMemberView> members)
    {
        for (var i = 0; i < _squadMemberLabels.Length; i++)
        {
            if (i >= members.Count)
            {
                _squadMemberLabels[i].Text = $"{i + 1}  EMPTY";
                _squadMemberLabels[i].AddThemeColorOverride("font_color", new Color(0.36f, 0.43f, 0.41f));
                _squadSkillLabels[i].Text = string.Empty;
                _squadHealthBars[i].Value = 0;
                continue;
            }
            var member = members[i];
            var role = OperatorRoles.RoleName(member.Role, _language);
            var source = GameLocalization.IsChinese(_language)
                ? i == 0 ? "\u4f60" : member.IsHuman ? "\u771f\u4eba" : "AI"
                : i == 0 ? "YOU" : member.IsHuman ? "NET" : "AI";
            var health = Mathf.Max(0, Mathf.RoundToInt(member.Health));
            var down = member.IsDown ? (GameLocalization.IsChinese(_language) ? " 倒地" : " DOWN") : string.Empty;
            _squadMemberLabels[i].Text = $"{i + 1} {member.Callsign} {role} {source} {health}{down}";
            _squadMemberLabels[i].AddThemeColorOverride("font_color",
                member.IsDown ? new Color(1.0f, 0.32f, 0.22f) : OperatorRoles.Spec(member.Role).Accent);
            _squadSkillLabels[i].Text = member.SkillCooldown <= 0.05f
                ? "H READY"
                : $"H {Mathf.CeilToInt(member.SkillCooldown)}s";
            _squadSkillLabels[i].AddThemeColorOverride(
                "font_color",
                member.SkillCooldown <= 0.05f
                    ? OperatorRoles.Spec(member.Role).Accent
                    : new Color(0.52f, 0.59f, 0.57f));
            _squadHealthBars[i].MaxValue = Mathf.Max(1.0f, member.MaxHealth);
            _squadHealthBars[i].Value = member.Health;
        }
    }

    public void SetClassSkill(OperatorRole role, float cooldown, float cooldownMax, bool active, bool action)
    {
        _displayedRole = role;
        _displayedCooldown = cooldown;
        _displayedCooldownMax = Mathf.Max(0.01f, cooldownMax);
        _displayedSkillActive = active;
        _displayedSkillAction = action;
        RefreshClassSkillText();
    }

    public void SetSquadOrder(SquadOrder order)
    {
        _displayedOrder = order;
        for (var i = 0; i < _orderButtons.Length; i++)
        {
            _orderButtons[i].SetPressedNoSignal(i == (int)order);
        }
        var orderName = OperatorRoles.OrderName(order, _language);
        _squadOrderLabel.Text = GameLocalization.IsChinese(_language)
            ? $"当前命令  //  {orderName}    F1 跟随  F2 戒备  F3 移动"
            : $"ORDER  //  {orderName}    F1 FOLLOW  F2 HOLD  F3 MOVE";
    }

    private void RefreshClassSkillText()
    {
        if (!IsInstanceValid(_classSkillLabel))
        {
            return;
        }
        var name = OperatorRoles.SkillName(_displayedRole, _language);
        var state = _displayedSkillAction
            ? (GameLocalization.IsChinese(_language) ? "施放中" : "DEPLOYING")
            : _displayedSkillActive
                ? (GameLocalization.IsChinese(_language) ? "生效中" : "ACTIVE")
                : _displayedCooldown <= 0.01f
                    ? (GameLocalization.IsChinese(_language) ? "就绪" : "READY")
                    : $"{_displayedCooldown:0.0}s";
        _classSkillLabel.Text = $"H  {name}  //  {state}";
        _classSkillLabel.AddThemeColorOverride("font_color", OperatorRoles.Spec(_displayedRole).Accent);
        _classSkillBar.Value = _displayedCooldown <= 0.01f
            ? 1.0f
            : 1.0f - Mathf.Clamp(_displayedCooldown / _displayedCooldownMax, 0.0f, 1.0f);
    }

    private void RefreshSquadLanguage()
    {
        if (!IsInstanceValid(_squadLobbyTitle))
        {
            return;
        }
        var chinese = GameLocalization.IsChinese(_language);
        _squadLobbyTitle.Text = chinese ? "\u884c\u52a8\u6574\u5907" : "OPERATION LOADOUT";
        _squadLobbySubtitle.Text = chinese
            ? "\u7a81\u51fb\u5c0f\u961f\u6574\u5907  //  \u6e2f\u533a\u5c01\u9501\u533a"
            : "STRIKE TEAM PREPARATION  //  HARBOR EXCLUSION ZONE";
        _roleCaption.Text = chinese ? "\u9009\u62e9\u5e72\u5458" : "SELECT OPERATOR";
        _localSquadButton.Text = chinese ? "\u672c\u5730 + AI" : "LOCAL + AI";
        _hostSquadButton.Text = chinese ? "\u521b\u5efa\u5c40\u57df\u7f51" : "HOST LAN";
        _joinSquadButton.Text = chinese ? "\u52a0\u5165\u5c40\u57df\u7f51" : "JOIN LAN";
        _squadAddress.PlaceholderText = chinese ? "\u4e3b\u673a\u5730\u5740" : "HOST ADDRESS";
        var roles = new[] { OperatorRole.Assault, OperatorRole.Medic, OperatorRole.Recon };
        for (var i = 0; i < roles.Length; i++)
        {
            _roleButtons[i].Text = string.Empty;
            _roleNameLabels[i].Text = OperatorRoles.RoleName(roles[i], _language);
            _roleSkillLabels[i].Text = OperatorRoles.SkillName(roles[i], _language);
            _roleDescriptions[i].Text = OperatorRoles.Description(roles[i], _language);
        }
        SelectSessionMode(_selectedSessionMode);
        SetSquadOrder(_displayedOrder);
        RefreshClassSkillText();
        RefreshDeploymentLanguage();
        RefreshSquadDeployAction();
    }
}
