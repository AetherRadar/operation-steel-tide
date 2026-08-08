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
    private readonly Button[] _roleButtons = new Button[3];
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
            Color = new Color(0.003f, 0.007f, 0.009f, 0.96f),
            MouseFilter = Control.MouseFilterEnum.Stop,
            Visible = false
        };
        _squadLobby.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(_squadLobby);

        var panel = new Control { Size = new Vector2(1040, 760) };
        panel.SetAnchorsPreset(Control.LayoutPreset.Center);
        panel.Position = new Vector2(-520, -380);
        _squadLobby.AddChild(panel);
        panel.AddChild(new ColorRect
        {
            Position = new Vector2(0, 0),
            Size = new Vector2(1040, 3),
            Color = new Color(0.22f, 0.85f, 0.69f)
        });
        _squadLobbyTitle = Label("SQUAD DEPLOYMENT", 31, new Color(0.86f, 0.96f, 0.92f));
        _squadLobbyTitle.Position = new Vector2(0, 28);
        _squadLobbyTitle.Size = new Vector2(1040, 44);
        _squadLobbyTitle.HorizontalAlignment = HorizontalAlignment.Center;
        panel.AddChild(_squadLobbyTitle);
        _squadLobbySubtitle = Label("SELECT YOUR CLASS  //  3-OPERATOR SQUAD  //  AI FILLS THE OTHER TWO", 14, new Color(0.48f, 0.65f, 0.61f));
        _squadLobbySubtitle.Position = new Vector2(0, 78);
        _squadLobbySubtitle.Size = new Vector2(1040, 24);
        _squadLobbySubtitle.HorizontalAlignment = HorizontalAlignment.Center;
        panel.AddChild(_squadLobbySubtitle);

        var group = new ButtonGroup();
        var roles = new[] { OperatorRole.Assault, OperatorRole.Medic, OperatorRole.Recon };
        for (var i = 0; i < roles.Length; i++)
        {
            var role = roles[i];
            var spec = OperatorRoles.Spec(role);
            var x = 22 + i * 340;
            var card = new ColorRect
            {
                Position = new Vector2(x, 126),
                Size = new Vector2(316, 280),
                Color = new Color(0.018f, 0.027f, 0.03f, 0.95f),
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            panel.AddChild(card);
            card.AddChild(new ColorRect
            {
                Position = Vector2.Zero,
                Size = new Vector2(316, 4),
                Color = spec.Accent,
                MouseFilter = Control.MouseFilterEnum.Ignore
            });
            var glyph = Label(role switch { OperatorRole.Medic => "+", OperatorRole.Recon => "\u25c9", _ => "\u25b2" }, 56, spec.Accent);
            glyph.Position = new Vector2(0, 22);
            glyph.Size = new Vector2(316, 70);
            glyph.HorizontalAlignment = HorizontalAlignment.Center;
            card.AddChild(glyph);
            _roleButtons[i] = Button(spec.Name, new Vector2(24, 102), new Vector2(268, 50));
            _roleButtons[i].ToggleMode = true;
            _roleButtons[i].ButtonGroup = group;
            _roleButtons[i].FocusMode = Control.FocusModeEnum.None;
            _roleButtons[i].Pressed += () => SelectSquadRole(role);
            card.AddChild(_roleButtons[i]);
            _roleDescriptions[i] = Label(spec.Description, 13, new Color(0.72f, 0.79f, 0.77f));
            _roleDescriptions[i].Position = new Vector2(24, 168);
            _roleDescriptions[i].Size = new Vector2(268, 76);
            _roleDescriptions[i].AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _roleDescriptions[i].HorizontalAlignment = HorizontalAlignment.Center;
            card.AddChild(_roleDescriptions[i]);
        }
        _roleButtons[0].ButtonPressed = true;


        _squadSessionStatus = Label("LOCAL SQUAD  //  THREE AI TEAMMATES READY", 13, new Color(0.45f, 0.86f, 0.72f));
        _squadSessionStatus.Position = new Vector2(0, 612);
        _squadSessionStatus.Size = new Vector2(1040, 24);
        _squadSessionStatus.HorizontalAlignment = HorizontalAlignment.Center;
        panel.AddChild(_squadSessionStatus);
        _localSquadButton = Button("LOCAL + AI", new Vector2(22, 648), new Vector2(250, 48));
        _localSquadButton.Pressed += () => RequestSquadDeployment(SquadSessionMode.Local);
        panel.AddChild(_localSquadButton);
        _hostSquadButton = Button("HOST LAN", new Vector2(286, 648), new Vector2(220, 48));
        _hostSquadButton.Pressed += () => RequestSquadDeployment(SquadSessionMode.Host);
        panel.AddChild(_hostSquadButton);
        _squadAddress = new LineEdit
        {
            Text = "127.0.0.1",
            PlaceholderText = "HOST ADDRESS",
            Position = new Vector2(520, 648),
            Size = new Vector2(260, 48),
            ClearButtonEnabled = true
        };
        _squadAddress.AddThemeFontSizeOverride("font_size", 15);
        panel.AddChild(_squadAddress);
        _joinSquadButton = Button("JOIN LAN", new Vector2(794, 648), new Vector2(224, 48));
        _joinSquadButton.Pressed += () => RequestSquadDeployment(SquadSessionMode.Join);
        panel.AddChild(_joinSquadButton);
        var footer = Label("H CLASS SKILL    F1 FOLLOW    F2 HOLD    F3 MOVE TO AIM POINT", 12, new Color(0.42f, 0.56f, 0.53f));
        footer.Position = new Vector2(0, 714);
        footer.Size = new Vector2(1040, 24);
        footer.HorizontalAlignment = HorizontalAlignment.Center;
        panel.AddChild(footer);
    }

    private void SelectSquadRole(OperatorRole role)
    {
        _selectedRole = role;
        var spec = OperatorRoles.Spec(role);
        _squadSessionStatus.Text = GameLocalization.IsChinese(_language)
            ? $"已选择 {spec.ChineseName}  //  可单人带 AI 或加入局域网"
            : $"{spec.Name} SELECTED  //  LOCAL AI OR LAN READY";
        _squadSessionStatus.AddThemeColorOverride("font_color", spec.Accent);
    }

    private void RequestSquadDeployment(SquadSessionMode mode)
    {
        SquadDeploymentRequested?.Invoke((int)_selectedRole, (int)mode, _squadAddress.Text.Trim());
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
        _squadLobbyTitle.Text = chinese ? "小队部署" : "SQUAD DEPLOYMENT";
        _squadLobbySubtitle.Text = chinese
            ? "\u9009\u62e9\u804c\u4e1a  //  \u7a7a\u4f4d\u81ea\u52a8\u7531 AI \u961f\u53cb\u8865\u9f50"
            : "SELECT YOUR CLASS  //  EMPTY SLOTS ARE FILLED BY AI";
        _localSquadButton.Text = chinese ? "单人 + AI" : "LOCAL + AI";
        _hostSquadButton.Text = chinese ? "创建局域网" : "HOST LAN";
        _joinSquadButton.Text = chinese ? "加入局域网" : "JOIN LAN";
        var roles = new[] { OperatorRole.Assault, OperatorRole.Medic, OperatorRole.Recon };
        for (var i = 0; i < roles.Length; i++)
        {
            _roleButtons[i].Text = OperatorRoles.RoleName(roles[i], _language);
            _roleDescriptions[i].Text = OperatorRoles.Description(roles[i], _language);
        }
        SetSquadOrder(_displayedOrder);
        RefreshClassSkillText();
    }
}
