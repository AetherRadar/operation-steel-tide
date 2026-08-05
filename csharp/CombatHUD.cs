using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

[GlobalClass]
public partial class CombatHUD : CanvasLayer
{
    [Signal] public delegate void PauseRequestedEventHandler();
    [Signal] public delegate void RestartRequestedEventHandler();
    [Signal] public delegate void QuitRequestedEventHandler();
    [Signal] public delegate void SensitivityChangedEventHandler(float value);
    [Signal] public delegate void QualityChangedEventHandler(int index);
    [Signal] public delegate void FullscreenChangedEventHandler(bool active);
    [Signal] public delegate void LanguageChangedEventHandler(string language);
    [Signal] public delegate void LootTakeRequestedEventHandler(string itemId);
    [Signal] public delegate void LootEquipRequestedEventHandler(string itemId);
    [Signal] public delegate void BackpackUseRequestedEventHandler(string itemId);
    [Signal] public delegate void LootClosedEventHandler();

    private Label _healthLabel = null!;
    private Label _armorLabel = null!;
    private Label _ammoLabel = null!;
    private Label _reserveLabel = null!;
    private Label _grenadeLabel = null!;
    private Label _weaponModeLabel = null!;
    private Label _plateReserveLabel = null!;
    private Label _vitalCaption = null!;
    private Label _armorCaption = null!;
    private Label _objectiveLabel = null!;
    private Label _enemiesLabel = null!;
    private ProgressBar _staminaBar = null!;
    private ShaderMaterial _damageMaterial = null!;
    private Control _hitmarker = null!;
    private Control _crosshair = null!;
    private ColorRect _stateOverlay = null!;
    private Label _stateTitle = null!;
    private Label _stateSubtitle = null!;
    private Label _compassLabel = null!;
    private Label _phaseLabel = null!;
    private Label _alertLabel = null!;
    private Label _radioLabel = null!;
    private Label _operationBanner = null!;
    private Control _interactionRoot = null!;
    private Label _interactionLabel = null!;
    private ProgressBar _interactionBar = null!;
    private Control _equipmentRoot = null!;
    private Label _equipmentLabel = null!;
    private ProgressBar _equipmentBar = null!;
    private ColorRect _pauseOverlay = null!;
    private HSlider _sensitivitySlider = null!;
    private Label _sensitivityValue = null!;
    private OptionButton _qualitySelect = null!;
    private CheckButton _fullscreenToggle = null!;
    private OptionButton _languageSelect = null!;
    private Label _pauseTitle = null!;
    private Label _pauseOperation = null!;
    private Label _sensitivityCaption = null!;
    private Label _qualityCaption = null!;
    private Label _languageCaption = null!;
    private Button _resumeButton = null!;
    private Button _restartButton = null!;
    private Button _quitButton = null!;
    private Label _buildLabel = null!;
    private ColorRect _lootOverlay = null!;
    private VBoxContainer _lootSourceList = null!;
    private VBoxContainer _backpackList = null!;
    private Label _lootTitle = null!;
    private Label _lootStats = null!;
    private Label _lootSourceCaption = null!;
    private Label _backpackCaption = null!;
    private Button _lootCloseButton = null!;
    private IReadOnlyList<LootItem>? _shownLoot;
    private TacticalPlayer? _shownPlayer;
    private string _shownLootName = string.Empty;
    private Tween? _hitTween;
    private Tween? _damageTween;
    private Tween? _crosshairTween;
    private Tween? _radioTween;
    private string _language = "en";
    public string CurrentLanguage => _language;

    public override void _Ready()
    {
        ProcessMode = Node.ProcessModeEnum.Always;
        Layer = 20;
        BuildHud();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.Escape)
        {
            if (IsLootVisible)
            {
                EmitSignal(SignalName.LootClosed);
            }
            else
            {
                EmitSignal(SignalName.PauseRequested);
            }
            GetViewport().SetInputAsHandled();
        }
    }

    private static Label Label(string text, int size, Color? color = null)
    {
        var label = new Label
        {
            Text = text
        };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color ?? Colors.White);
        label.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.85f));
        label.AddThemeConstantOverride("shadow_offset_x", 2);
        label.AddThemeConstantOverride("shadow_offset_y", 2);
        return label;
    }

    private static ColorRect Panel(Control parent, Vector2 position, Vector2 size)
    {
        var panel = new ColorRect
        {
            Position = position,
            Size = size,
            Color = new Color(0.015f, 0.022f, 0.025f, 0.68f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        parent.AddChild(panel);
        var accent = new ColorRect
        {
            Position = Vector2.Zero,
            Size = new Vector2(3, size.Y),
            Color = new Color(0.18f, 0.78f, 0.66f, 0.9f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        panel.AddChild(accent);
        return panel;
    }

    private void BuildHud()
    {
        var root = new Control
        {
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);

        var damageOverlay = new ColorRect
        {
            Color = Colors.White,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        damageOverlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var shader = new Shader
        {
            Code = """
                shader_type canvas_item;
                uniform float strength = 0.0;
                void fragment() {
                    float edge = smoothstep(0.24, 0.72, distance(UV, vec2(0.5)));
                    COLOR = vec4(0.58, 0.01, 0.0, edge * strength);
                }
                """
        };
        _damageMaterial = new ShaderMaterial { Shader = shader };
        damageOverlay.Material = _damageMaterial;
        root.AddChild(damageOverlay);

        var topStrip = new ColorRect
        {
            Color = new Color(0.01f, 0.016f, 0.018f, 0.72f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        topStrip.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        topStrip.OffsetBottom = 52;
        root.AddChild(topStrip);

        _objectiveLabel = Label("SECURE THE FREIGHT TERMINAL", 17, new Color(0.72f, 0.95f, 0.89f));
        _objectiveLabel.Position = new Vector2(30, 15);
        _objectiveLabel.Size = new Vector2(640, 24);
        _objectiveLabel.ClipText = true;
        topStrip.AddChild(_objectiveLabel);

        _enemiesLabel = Label("HOSTILES  08", 16, new Color(1.0f, 0.48f, 0.3f));
        _enemiesLabel.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        _enemiesLabel.Position = new Vector2(-165, 15);
        topStrip.AddChild(_enemiesLabel);
        _phaseLabel = Label("DEPLOYMENT  12   LOCAL", 14, new Color(0.3f, 0.88f, 0.7f));
        _phaseLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _phaseLabel.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
        _phaseLabel.Position = new Vector2(-150, 16);
        _phaseLabel.Size = new Vector2(300, 22);
        topStrip.AddChild(_phaseLabel);

        _compassLabel = Label("W      285      NW", 14, new Color(0.82f, 0.86f, 0.84f));
        _compassLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _compassLabel.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
        _compassLabel.Position = new Vector2(-110, 65);
        _compassLabel.Size = new Vector2(220, 24);
        root.AddChild(_compassLabel);
        _alertLabel = Label("UNDETECTED", 12, new Color(0.42f, 0.75f, 0.65f));
        _alertLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _alertLabel.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
        _alertLabel.Position = new Vector2(-100, 91);
        _alertLabel.Size = new Vector2(200, 22);
        root.AddChild(_alertLabel);

        _radioLabel = Label(string.Empty, 15, new Color(0.92f, 0.96f, 0.93f));
        _radioLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _radioLabel.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
        _radioLabel.Position = new Vector2(-310, 128);
        _radioLabel.Size = new Vector2(620, 28);
        _radioLabel.Visible = false;
        root.AddChild(_radioLabel);

        var status = Panel(root, Vector2.Zero, new Vector2(245, 92));
        status.AnchorLeft = 0.0f;
        status.AnchorTop = 1.0f;
        status.AnchorRight = 0.0f;
        status.AnchorBottom = 1.0f;
        status.OffsetLeft = 30;
        status.OffsetTop = -124;
        status.OffsetRight = 275;
        status.OffsetBottom = -32;
        _healthLabel = Label("100", 31, new Color(0.88f, 0.96f, 0.92f));
        _healthLabel.Position = new Vector2(22, 10);
        status.AddChild(_healthLabel);
        _vitalCaption = PositionedLabel("VITAL", 11, new Color(0.46f, 0.61f, 0.57f), 22, 51);
        status.AddChild(_vitalCaption);
        _armorLabel = Label("75", 31, new Color(0.4f, 0.74f, 1.0f));
        _armorLabel.Position = new Vector2(108, 10);
        status.AddChild(_armorLabel);
        _armorCaption = PositionedLabel("PLATE", 11, new Color(0.43f, 0.58f, 0.64f), 108, 51);
        status.AddChild(_armorCaption);
        _plateReserveLabel = PositionedLabel("x2", 12, new Color(0.5f, 0.76f, 0.94f), 174, 50);
        status.AddChild(_plateReserveLabel);
        _staminaBar = new ProgressBar
        {
            Position = new Vector2(22, 72),
            Size = new Vector2(196, 5),
            ShowPercentage = false,
            MaxValue = 100,
            Value = 100
        };
        status.AddChild(_staminaBar);

        var weapon = Panel(root, Vector2.Zero, new Vector2(246, 92));
        weapon.AnchorLeft = 1.0f;
        weapon.AnchorTop = 1.0f;
        weapon.AnchorRight = 1.0f;
        weapon.AnchorBottom = 1.0f;
        weapon.OffsetLeft = -276;
        weapon.OffsetTop = -124;
        weapon.OffsetRight = -30;
        weapon.OffsetBottom = -32;
        _ammoLabel = Label("30", 42, new Color(0.95f, 0.98f, 0.95f));
        _ammoLabel.Position = new Vector2(22, 4);
        weapon.AddChild(_ammoLabel);
        _reserveLabel = Label("/ 150", 18, new Color(0.54f, 0.65f, 0.62f));
        _reserveLabel.Position = new Vector2(78, 23);
        weapon.AddChild(_reserveLabel);
        _weaponModeLabel = PositionedLabel("M4A1   AUTO", 12, new Color(0.4f, 0.82f, 0.71f), 23, 62);
        _weaponModeLabel.Size = new Vector2(118, 22);
        weapon.AddChild(_weaponModeLabel);
        _grenadeLabel = Label("FRAG  x2", 13, new Color(0.78f, 0.83f, 0.8f));
        _grenadeLabel.Position = new Vector2(148, 61);
        weapon.AddChild(_grenadeLabel);

        _crosshair = new Control();
        _crosshair.SetAnchorsPreset(Control.LayoutPreset.Center);
        _crosshair.Position = new Vector2(-1, -1);
        root.AddChild(_crosshair);
        foreach (var data in new[] { new Vector4(-13, 0, 7, 2), new Vector4(7, 0, 7, 2), new Vector4(0, -13, 2, 7), new Vector4(0, 7, 2, 7) })
        {
            _crosshair.AddChild(new ColorRect
            {
                Position = new Vector2(data.X, data.Y),
                Size = new Vector2(data.Z, data.W),
                Color = new Color(0.86f, 0.94f, 0.9f, 0.82f),
                MouseFilter = Control.MouseFilterEnum.Ignore
            });
        }

        _hitmarker = new Control();
        _hitmarker.SetAnchorsPreset(Control.LayoutPreset.Center);
        _hitmarker.Modulate = new Color(1, 1, 1, 0);
        root.AddChild(_hitmarker);
        foreach (var data in new[] { new Vector4(-11, -11, 7, 2), new Vector4(5, -11, 7, 2), new Vector4(-11, 9, 7, 2), new Vector4(5, 9, 7, 2) })
        {
            var mark = new ColorRect
            {
                Position = new Vector2(data.X, data.Y),
                Size = new Vector2(data.Z, data.W),
                Rotation = data.X * data.Y > 0 ? Mathf.Pi / 4 : -Mathf.Pi / 4,
                Color = Colors.White,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            _hitmarker.AddChild(mark);
        }

        _interactionRoot = new Control
        {
            Position = new Vector2(-170, 76),
            Size = new Vector2(340, 56),
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _interactionRoot.SetAnchorsPreset(Control.LayoutPreset.Center);
        root.AddChild(_interactionRoot);
        _interactionLabel = Label("F   DISABLE RELAY", 15, new Color(0.86f, 0.96f, 0.91f));
        _interactionLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _interactionLabel.Size = new Vector2(340, 28);
        _interactionRoot.AddChild(_interactionLabel);
        _interactionBar = new ProgressBar
        {
            Position = new Vector2(50, 34),
            Size = new Vector2(240, 5),
            MinValue = 0,
            MaxValue = 1,
            ShowPercentage = false
        };
        _interactionRoot.AddChild(_interactionBar);

        _equipmentRoot = new Control
        {
            Position = new Vector2(-160, 150),
            Size = new Vector2(320, 48),
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _equipmentRoot.SetAnchorsPreset(Control.LayoutPreset.Center);
        root.AddChild(_equipmentRoot);
        _equipmentLabel = Label("APPLYING ARMOR", 14, new Color(0.58f, 0.82f, 1.0f));
        _equipmentLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _equipmentLabel.Size = new Vector2(320, 24);
        _equipmentRoot.AddChild(_equipmentLabel);
        _equipmentBar = new ProgressBar
        {
            Position = new Vector2(55, 31),
            Size = new Vector2(210, 5),
            MinValue = 0,
            MaxValue = 1,
            ShowPercentage = false
        };
        _equipmentRoot.AddChild(_equipmentBar);

        _operationBanner = Label("OPERATION STEEL TIDE", 30, new Color(0.86f, 0.96f, 0.92f));
        _operationBanner.HorizontalAlignment = HorizontalAlignment.Center;
        _operationBanner.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
        _operationBanner.Position = new Vector2(-250, 145);
        _operationBanner.Size = new Vector2(500, 50);
        root.AddChild(_operationBanner);
        var bannerTween = CreateTween();
        bannerTween.TweenInterval(2.0f);
        bannerTween.TweenProperty(_operationBanner, "modulate:a", 0.0f, 1.2f);

        _stateOverlay = new ColorRect
        {
            Color = new Color(0.005f, 0.009f, 0.011f, 0.86f),
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        _stateOverlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(_stateOverlay);
        _stateTitle = Label("MISSION COMPLETE", 44, new Color(0.33f, 0.92f, 0.74f));
        _stateTitle.HorizontalAlignment = HorizontalAlignment.Center;
        _stateTitle.SetAnchorsPreset(Control.LayoutPreset.Center);
        _stateTitle.Position = new Vector2(-350, -70);
        _stateTitle.Size = new Vector2(700, 60);
        _stateOverlay.AddChild(_stateTitle);
        _stateSubtitle = Label("FREIGHT TERMINAL SECURED", 17, new Color(0.68f, 0.75f, 0.72f));
        _stateSubtitle.HorizontalAlignment = HorizontalAlignment.Center;
        _stateSubtitle.SetAnchorsPreset(Control.LayoutPreset.Center);
        _stateSubtitle.Position = new Vector2(-350, 7);
        _stateSubtitle.Size = new Vector2(700, 40);
        _stateOverlay.AddChild(_stateSubtitle);
        BuildPauseMenu(root);
        BuildLootOverlay(root);
    }

    private static Label PositionedLabel(string text, int size, Color color, float x, float y)
    {
        var label = Label(text, size, color);
        label.Position = new Vector2(x, y);
        return label;
    }

    private static Button Button(string text, Vector2 position, Vector2 size)
    {
        var button = new Button
        {
            Text = text,
            Position = position,
            Size = size
        };
        button.AddThemeFontSizeOverride("font_size", 16);
        button.AddThemeColorOverride("font_color", new Color(0.82f, 0.9f, 0.87f));
        button.AddThemeColorOverride("font_hover_color", new Color(0.19f, 0.9f, 0.69f));
        return button;
    }

    private void BuildLootOverlay(Control root)
    {
        _lootOverlay = new ColorRect
        {
            Color = new Color(0.004f, 0.007f, 0.008f, 0.91f),
            MouseFilter = Control.MouseFilterEnum.Stop,
            Visible = false
        };
        _lootOverlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(_lootOverlay);

        var panel = new ColorRect
        {
            Color = new Color(0.025f, 0.032f, 0.033f, 0.98f),
            Position = new Vector2(-580, -350),
            Size = new Vector2(1160, 700)
        };
        panel.SetAnchorsPreset(Control.LayoutPreset.Center);
        _lootOverlay.AddChild(panel);
        panel.AddChild(new ColorRect
        {
            Color = new Color(0.18f, 0.78f, 0.66f),
            Position = Vector2.Zero,
            Size = new Vector2(5, 700)
        });

        _lootTitle = PositionedLabel("FIELD INVENTORY", 25, new Color(0.83f, 0.96f, 0.91f), 38, 24);
        _lootTitle.Size = new Vector2(820, 36);
        panel.AddChild(_lootTitle);
        _lootCloseButton = Button("CLOSE", new Vector2(970, 22), new Vector2(150, 40));
        _lootCloseButton.Pressed += () => EmitSignal(SignalName.LootClosed);
        panel.AddChild(_lootCloseButton);
        panel.AddChild(new ColorRect
        {
            Color = new Color(0.18f, 0.32f, 0.29f, 0.9f),
            Position = new Vector2(38, 76),
            Size = new Vector2(1082, 1)
        });

        _lootSourceCaption = PositionedLabel("SEARCHED GEAR", 14, new Color(0.43f, 0.88f, 0.73f), 40, 92);
        _lootSourceCaption.Size = new Vector2(510, 26);
        panel.AddChild(_lootSourceCaption);
        _backpackCaption = PositionedLabel("EQUIPPED / BACKPACK", 14, new Color(0.43f, 0.72f, 0.96f), 610, 92);
        _backpackCaption.Size = new Vector2(510, 26);
        panel.AddChild(_backpackCaption);

        var sourceScroll = new ScrollContainer
        {
            Position = new Vector2(40, 126),
            Size = new Vector2(510, 520),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        panel.AddChild(sourceScroll);
        _lootSourceList = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _lootSourceList.AddThemeConstantOverride("separation", 8);
        sourceScroll.AddChild(_lootSourceList);

        _lootStats = PositionedLabel("", 15, new Color(0.78f, 0.86f, 0.83f), 610, 126);
        _lootStats.Size = new Vector2(510, 116);
        _lootStats.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        panel.AddChild(_lootStats);

        var backpackScroll = new ScrollContainer
        {
            Position = new Vector2(610, 252),
            Size = new Vector2(510, 394),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        panel.AddChild(backpackScroll);
        _backpackList = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _backpackList.AddThemeConstantOverride("separation", 8);
        backpackScroll.AddChild(_backpackList);
    }

    public bool IsLootVisible => IsInstanceValid(_lootOverlay) && _lootOverlay.Visible;

    public void ShowLoot(string sourceName, IReadOnlyList<LootItem> items, TacticalPlayer player)
    {
        _shownLootName = sourceName;
        _shownLoot = items;
        _shownPlayer = player;
        _lootOverlay.Visible = true;
        RefreshLootOverlay();
    }

    public void HideLoot()
    {
        _lootOverlay.Visible = false;
        _shownLoot = null;
        _shownPlayer = null;
    }

    public void RefreshLootOverlay()
    {
        if (!IsLootVisible || _shownLoot is null || _shownPlayer is null)
        {
            return;
        }
        _lootTitle.Text = $"{Text("field_inventory", "FIELD INVENTORY")}  //  {_shownLootName}";
        _lootSourceCaption.Text = Text("searched_gear", "SEARCHED GEAR");
        _backpackCaption.Text = $"{Text("equipped_backpack", "EQUIPPED / BACKPACK")}  {_shownPlayer.Backpack.Count}/{_shownPlayer.BackpackCapacity}";
        _lootCloseButton.Text = Text("close", "CLOSE");

        var stats = _shownPlayer.CurrentWeaponStats;
        var weaponName = _shownPlayer.EquippedWeapon.DisplayName(_language);
        var partNames = new List<string>();
        foreach (var partId in _shownPlayer.EquippedWeapon.Attachments.Values)
        {
            var part = WeaponCatalog.Attachment(partId);
            partNames.Add(GameLocalization.IsChinese(_language) ? part.ChineseName : part.Name);
        }
        _lootStats.Text = GameLocalization.IsChinese(_language)
            ? $"当前主武器  {weaponName}\n伤害 {stats.Damage:0}   有效射程 {stats.EffectiveRange:0}m   后坐 {stats.Recoil:0.00}   操控 {stats.Handling:0.00}\n零件  {System.String.Join(" / ", partNames)}"
            : $"EQUIPPED  {weaponName}\nDMG {stats.Damage:0}   RANGE {stats.EffectiveRange:0}m   RECOIL {stats.Recoil:0.00}   HANDLING {stats.Handling:0.00}\nPARTS  {System.String.Join(" / ", partNames)}";

        ClearRows(_lootSourceList);
        foreach (var item in _shownLoot)
        {
            _lootSourceList.AddChild(BuildLootRow(item, false));
        }
        if (_shownLoot.Count == 0)
        {
            _lootSourceList.AddChild(Label(Text("empty", "EMPTY"), 15, new Color(0.48f, 0.54f, 0.52f)));
        }

        ClearRows(_backpackList);
        foreach (var item in _shownPlayer.Backpack)
        {
            _backpackList.AddChild(BuildLootRow(item, true));
        }
        if (_shownPlayer.Backpack.Count == 0)
        {
            _backpackList.AddChild(Label(Text("backpack_empty", "BACKPACK EMPTY"), 15, new Color(0.48f, 0.54f, 0.52f)));
        }
    }

    private Control BuildLootRow(LootItem item, bool backpackItem)
    {
        var row = new HBoxContainer { CustomMinimumSize = new Vector2(490, 72) };
        var info = Label($"{item.DisplayName(_language)}\n{item.Detail(_language)}", 13, new Color(0.82f, 0.89f, 0.86f));
        info.CustomMinimumSize = new Vector2(backpackItem ? 380 : 295, 68);
        info.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        info.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        row.AddChild(info);
        if (!backpackItem)
        {
            var take = new Button { Text = Text("take", "TAKE"), CustomMinimumSize = new Vector2(78, 44) };
            take.Pressed += () => EmitSignal(SignalName.LootTakeRequested, item.Id);
            row.AddChild(take);
        }
        var action = new Button
        {
            Text = backpackItem
                ? Text("use_install", "USE / INSTALL")
                : item.Kind is LootItemKind.Weapon or LootItemKind.Attachment
                    ? Text("equip", "EQUIP")
                    : Text("use", "USE"),
            CustomMinimumSize = new Vector2(backpackItem ? 110 : 92, 44)
        };
        action.Pressed += () => EmitSignal(
            backpackItem ? SignalName.BackpackUseRequested : SignalName.LootEquipRequested,
            item.Id);
        row.AddChild(action);
        return row;
    }

    private static void ClearRows(Node parent)
    {
        foreach (var child in parent.GetChildren())
        {
            parent.RemoveChild(child);
            child.QueueFree();
        }
    }

    private void BuildPauseMenu(Control root)
    {
        _pauseOverlay = new ColorRect
        {
            Color = new Color(0.004f, 0.008f, 0.01f, 0.93f),
            MouseFilter = Control.MouseFilterEnum.Stop,
            Visible = false
        };
        _pauseOverlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(_pauseOverlay);
        var content = new Control
        {
            Position = new Vector2(-260, -270),
            Size = new Vector2(520, 540)
        };
        content.SetAnchorsPreset(Control.LayoutPreset.Center);
        _pauseOverlay.AddChild(content);
        _pauseTitle = Label("TACTICAL PAUSE", 38, new Color(0.82f, 0.94f, 0.9f));
        _pauseTitle.Position = Vector2.Zero;
        _pauseTitle.Size = new Vector2(520, 54);
        _pauseTitle.HorizontalAlignment = HorizontalAlignment.Center;
        content.AddChild(_pauseTitle);
        _pauseOperation = Label("OPERATION STEEL TIDE", 13, new Color(0.34f, 0.71f, 0.62f));
        _pauseOperation.Position = new Vector2(0, 52);
        _pauseOperation.Size = new Vector2(520, 24);
        _pauseOperation.HorizontalAlignment = HorizontalAlignment.Center;
        content.AddChild(_pauseOperation);
        content.AddChild(new ColorRect
        {
            Position = new Vector2(40, 94),
            Size = new Vector2(440, 1),
            Color = new Color(0.2f, 0.38f, 0.34f, 0.7f)
        });
        _sensitivityCaption = PositionedLabel("LOOK SENSITIVITY", 13, new Color(0.56f, 0.66f, 0.63f), 40, 125);
        content.AddChild(_sensitivityCaption);
        _sensitivityValue = PositionedLabel("1.00", 13, new Color(0.37f, 0.86f, 0.7f), 424, 125);
        content.AddChild(_sensitivityValue);
        _sensitivitySlider = new HSlider
        {
            Position = new Vector2(40, 154),
            Size = new Vector2(440, 28),
            MinValue = 0.45,
            MaxValue = 2.0,
            Step = 0.05,
            Value = 1.0
        };
        _sensitivitySlider.ValueChanged += value =>
        {
            _sensitivityValue.Text = $"{value:0.00}";
            EmitSignal(SignalName.SensitivityChanged, (float)value);
        };
        content.AddChild(_sensitivitySlider);
        _qualityCaption = PositionedLabel("RENDER QUALITY", 13, new Color(0.56f, 0.66f, 0.63f), 40, 205);
        content.AddChild(_qualityCaption);
        _qualitySelect = new OptionButton { Position = new Vector2(270, 194), Size = new Vector2(210, 38) };
        _qualitySelect.AddItem("Performance");
        _qualitySelect.AddItem("Balanced");
        _qualitySelect.AddItem("Cinematic");
        _qualitySelect.Selected = 2;
        _qualitySelect.ItemSelected += index => EmitSignal(SignalName.QualityChanged, (int)index);
        content.AddChild(_qualitySelect);

        _languageCaption = PositionedLabel("LANGUAGE", 13, new Color(0.56f, 0.66f, 0.63f), 40, 255);
        content.AddChild(_languageCaption);
        _languageSelect = new OptionButton { Position = new Vector2(270, 244), Size = new Vector2(210, 38) };
        _languageSelect.AddItem("English");
        _languageSelect.AddItem("中文");
        _languageSelect.ItemSelected += index =>
        {
            var language = index == 1 ? "zh" : "en";
            SetLanguage(language);
            EmitSignal(SignalName.LanguageChanged, language);
        };
        content.AddChild(_languageSelect);
        _fullscreenToggle = new CheckButton
        {
            Text = "FULLSCREEN",
            Position = new Vector2(35, 292),
            Size = new Vector2(445, 38)
        };
        _fullscreenToggle.Toggled += active => EmitSignal(SignalName.FullscreenChanged, active);
        content.AddChild(_fullscreenToggle);
        _resumeButton = Button("RESUME OPERATION", new Vector2(40, 342), new Vector2(440, 46));
        _resumeButton.Pressed += () => EmitSignal(SignalName.PauseRequested);
        content.AddChild(_resumeButton);
        _restartButton = Button("REDEPLOY", new Vector2(40, 401), new Vector2(210, 44));
        _restartButton.Pressed += () => EmitSignal(SignalName.RestartRequested);
        content.AddChild(_restartButton);
        _quitButton = Button("EXIT TO DESKTOP", new Vector2(270, 401), new Vector2(210, 44));
        _quitButton.Pressed += () => EmitSignal(SignalName.QuitRequested);
        content.AddChild(_quitButton);
        _buildLabel = PositionedLabel("FORWARD+  /  BUILD 0.6", 11, new Color(0.32f, 0.4f, 0.38f), 40, 477);
        _buildLabel.Size = new Vector2(440, 22);
        _buildLabel.HorizontalAlignment = HorizontalAlignment.Center;
        content.AddChild(_buildLabel);
    }

    public void SetPauseVisible(bool active) => _pauseOverlay.Visible = active;

    public void SetSettings(float sensitivity, int quality, bool fullscreen, string language)
    {
        _sensitivitySlider.SetValueNoSignal(sensitivity);
        _sensitivityValue.Text = $"{sensitivity:0.00}";
        _qualitySelect.Select(Mathf.Clamp(quality, 0, 2));
        _fullscreenToggle.SetPressedNoSignal(fullscreen);
        SetLanguage(language);
        _languageSelect.Select(GameLocalization.IsChinese(language) ? 1 : 0);
    }

    public void SetLanguage(string language)
    {
        _language = GameLocalization.IsChinese(language) ? "zh" : "en";
        if (_languageSelect != null)
        {
            _languageSelect.Select(_language == "zh" ? 1 : 0);
        }
        _vitalCaption.Text = Text("vital", "VITAL");
        _armorCaption.Text = Text("armor", "PLATE");
        _operationBanner.Text = Text("operation", "OPERATION STEEL TIDE");
        _pauseTitle.Text = Text("pause_title", "TACTICAL PAUSE");
        _pauseOperation.Text = Text("operation", "OPERATION STEEL TIDE");
        _sensitivityCaption.Text = Text("look_sensitivity", "LOOK SENSITIVITY");
        _qualityCaption.Text = Text("render_quality", "RENDER QUALITY");
        _languageCaption.Text = Text("language", "LANGUAGE");
        _fullscreenToggle.Text = Text("fullscreen", "FULLSCREEN");
        _resumeButton.Text = Text("resume", "RESUME OPERATION");
        _restartButton.Text = Text("redeploy", "REDEPLOY");
        _quitButton.Text = Text("exit", "EXIT TO DESKTOP");
        _qualitySelect.SetItemText(0, Text("performance", "Performance"));
        _qualitySelect.SetItemText(1, Text("balanced", "Balanced"));
        _qualitySelect.SetItemText(2, Text("cinematic", "Cinematic"));
        RefreshLootOverlay();
    }

    private string Text(string key, string english) => GameLocalization.Get(key, _language, english);

    public void SetStats(float health, float armor, float stamina, int ammo, int reserve, int grenades)
    {
        _healthLabel.Text = $"{Mathf.Max(0, (int)health):000}";
        _armorLabel.Text = $"{Mathf.Max(0, (int)armor):00}";
        _staminaBar.Value = stamina;
        _ammoLabel.Text = $"{ammo:00}";
        _reserveLabel.Text = $"/ {reserve:000}";
        _grenadeLabel.Text = $"{Text("grenade", "FRAG")}  x{grenades}";
        _healthLabel.AddThemeColorOverride("font_color", health < 30 ? new Color(1.0f, 0.36f, 0.25f) : new Color(0.88f, 0.96f, 0.92f));
    }

    public void SetEquipment(int armorPlates, string fireMode, string weaponName = "M4A1")
    {
        _plateReserveLabel.Text = $"x{armorPlates}";
        var mode = fireMode switch
        {
            "AUTO" => Text("auto", "AUTO"),
            "SEMI" => Text("semi", "SEMI"),
            _ => Text("knife", "KNIFE")
        };
        var displayWeapon = fireMode == "KNIFE" ? Text("tactical_knife", "TACTICAL KNIFE") : weaponName;
        _weaponModeLabel.Text = $"{displayWeapon}   {mode}";
    }

    public void SetEquipmentAction(string action, float progress, bool active)
    {
        _equipmentRoot.Visible = active;
        if (!active)
        {
            return;
        }
        _equipmentLabel.Text = action;
        _equipmentBar.Value = Mathf.Clamp(progress, 0.0f, 1.0f);
    }

    public void SetEnemyCount(int count)
    {
        _enemiesLabel.Text = $"{Text("hostiles", "HOSTILES")}  {count:00}";
        if (count == 0)
        {
            _enemiesLabel.AddThemeColorOverride("font_color", new Color(0.32f, 0.92f, 0.7f));
        }
    }

    public void SetObjective(string value) => _objectiveLabel.Text = value;

    public void SetInteraction(string action, float progress, bool active)
    {
        _interactionRoot.Visible = active;
        if (active)
        {
            _interactionLabel.Text = $"F   {action}";
            _interactionBar.Value = Mathf.Clamp(progress, 0.0f, 1.0f);
        }
    }

    public void SetHeading(float degrees)
    {
        var directions = new[] { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
        var normalized = Mathf.PosMod(degrees, 360.0f);
        var index = (int)Mathf.Round(normalized / 45.0f) % 8;
        _compassLabel.Text = $"{directions[(index + 7) % 8]}      {normalized:000}      {directions[index]}";
    }

    public void SetMissionPhase(string phase, float remaining, bool online)
    {
        var network = online ? Text("online", "ONLINE") : Text("local", "LOCAL");
        var phaseText = GameLocalization.Phase(phase, _language);
        if (phase == "DEPLOYMENT")
        {
            var countdown = remaining > 0.0f ? Mathf.CeilToInt(remaining).ToString("00") : Text("ready", "READY");
            _phaseLabel.Text = $"{phaseText}  {countdown}   {network}";
            _phaseLabel.AddThemeColorOverride("font_color", new Color(0.3f, 0.88f, 0.7f));
        }
        else if (phase == "INFILTRATION")
        {
            _phaseLabel.Text = $"{phaseText}   {network}";
            _phaseLabel.AddThemeColorOverride("font_color", new Color(0.45f, 0.82f, 0.72f));
        }
        else
        {
            _phaseLabel.Text = $"{phaseText}   {network}";
            _phaseLabel.AddThemeColorOverride("font_color", phase is "CONTACT" or "COMBAT" ? new Color(1.0f, 0.46f, 0.25f) : new Color(0.45f, 0.84f, 0.7f));
        }
    }

    public void SetAlert(float value, string phase)
    {
        if (phase is "CONTACT" or "COMBAT")
        {
            _alertLabel.Text = Text("alerted", "ALERTED");
            _alertLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.28f, 0.17f));
        }
        else if (value >= 65.0f)
        {
            _alertLabel.Text = Text("suspicion_high", "SUSPICION  HIGH");
            _alertLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.58f, 0.19f));
        }
        else if (value >= 18.0f)
        {
            _alertLabel.Text = $"{Text("suspicion", "SUSPICION")}  {(int)value:00}";
            _alertLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.78f, 0.28f));
        }
        else
        {
            _alertLabel.Text = Text("undetected", "UNDETECTED");
            _alertLabel.AddThemeColorOverride("font_color", new Color(0.42f, 0.75f, 0.65f));
        }
    }

    public void ShowHit(bool kill = false)
    {
        if (_hitTween?.IsRunning() == true)
        {
            _hitTween.Kill();
        }
        _hitmarker.Modulate = kill ? new Color(1.0f, 0.22f, 0.16f, 1.0f) : Colors.White;
        _hitTween = CreateTween();
        _hitTween.TweenProperty(_hitmarker, "modulate:a", 0.0f, 0.22f);
    }

    public void PulseCrosshair()
    {
        if (_crosshairTween?.IsRunning() == true)
        {
            _crosshairTween.Kill();
        }
        _crosshair.Scale = Vector2.One * 1.55f;
        _crosshairTween = CreateTween();
        _crosshairTween.TweenProperty(_crosshair, "scale", Vector2.One, 0.12f)
            .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
    }

    public void ShowDamage()
    {
        if (_damageTween?.IsRunning() == true)
        {
            _damageTween.Kill();
        }
        _damageMaterial.SetShaderParameter("strength", 0.58f);
        _damageTween = CreateTween();
        _damageTween.TweenProperty(_damageMaterial, "shader_parameter/strength", 0.0f, 0.55f);
    }

    public void SetAiming(bool active) => _crosshair.Visible = !active;

    public void ShowRadioMessage(string message, Color color)
    {
        if (_radioTween?.IsRunning() == true)
        {
            _radioTween.Kill();
        }
        _radioLabel.Text = message;
        _radioLabel.AddThemeColorOverride("font_color", color);
        _radioLabel.Modulate = Colors.White;
        _radioLabel.Visible = true;
        _radioTween = CreateTween();
        _radioTween.TweenInterval(3.2f);
        _radioTween.TweenProperty(_radioLabel, "modulate:a", 0.0f, 0.65f);
        _radioTween.TweenCallback(Callable.From(() => _radioLabel.Visible = false));
    }

    public void ShowLocalizedMessage(string key, string english, Color color)
    {
        ShowRadioMessage(Text(key, english), color);
    }

    public void SetEquipmentActionLocalized(string key, string english, float progress, bool active)
    {
        SetEquipmentAction(active ? Text(key, english) : string.Empty, progress, active);
    }

    public void ShowResult(bool victory)
    {
        _stateOverlay.Visible = true;
        if (victory)
        {
            _stateTitle.Text = Text("mission_complete", "MISSION COMPLETE");
            _stateTitle.AddThemeColorOverride("font_color", new Color(0.33f, 0.92f, 0.74f));
            _stateSubtitle.Text = Text("terminal_secured", "FREIGHT TERMINAL SECURED");
        }
        else
        {
            _stateTitle.Text = Text("operator_down", "OPERATOR DOWN");
            _stateTitle.AddThemeColorOverride("font_color", new Color(1.0f, 0.27f, 0.18f));
            _stateSubtitle.Text = Text("press_enter", "PRESS ENTER TO REDEPLOY");
        }
    }
}
