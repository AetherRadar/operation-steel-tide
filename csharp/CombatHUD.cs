using System;
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
    [Signal] public delegate void LootReturnRequestedEventHandler(string itemId);
    [Signal] public delegate void BackpackUseRequestedEventHandler(string itemId);
    [Signal] public delegate void BackpackDropRequestedEventHandler(string itemId);
    [Signal] public delegate void LootWeaponSlotRequestedEventHandler(string itemId, int origin, int slot);
    [Signal] public delegate void LootClosedEventHandler();
    [Signal] public delegate void WeaponSlotRequestedEventHandler(int slot);
    [Signal] public delegate void InventoryToggleRequestedEventHandler();
    [Signal] public delegate void OperationsQuickStartRequestedEventHandler();
    [Signal] public delegate void DemolitionModeRequestedEventHandler();
    [Signal] public delegate void DemolitionDeploymentRequestedEventHandler(
        int role,
        int primaryPlatform,
        int buildTier,
        int sidearmPlatform,
        string mapId,
        int sessionMode,
        string address,
        int networkTeam);
    [Signal] public delegate void DemolitionBackRequestedEventHandler();
    [Signal] public delegate void DemolitionPurchaseRequestedEventHandler(
        string sidearmId,
        string primaryId,
        bool armorSelected,
        int grenadeCount,
        int smokeGrenadeCount);
    [Signal] public delegate void OperationsHomeRequestedEventHandler();

    private Control _gameplayHudRoot = null!;

    private Label _healthLabel = null!;
    private Label _armorLabel = null!;
    private Label _ammoLabel = null!;
    private Label _reserveLabel = null!;
    private QuickSlotBarView _quickSlotBar = null!;
    private Label _weaponModeLabel = null!;
    private bool _hasPrimary = true;
    private int _activeWeaponSlot;
    private WeaponBuild? _quickPrimaryBuild;
    private WeaponBuild? _quickSecondaryBuild;
    private WeaponBuild? _quickSidearmBuild;
    private string _quickKnifeSkinId = KnifeSkinCatalog.DefaultId;
    private int _grenadeCount;
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
    public bool IsMissionResultVisible => IsInstanceValid(_stateOverlay) && _stateOverlay.Visible;
    private ColorRect _downedBanner = null!;
    private Label _downedTitle = null!;
    private Label _downedSubtitle = null!;
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
    private ColorRect _lootOverlay = null!;
    private GridContainer _lootSourceList = null!;
    private GridContainer _backpackList = null!;
    private LootDropZone _lootSourceZone = null!;
    private LootDropZone _backpackZone = null!;
    private LootDropZone _groundDropZone = null!;
    private ScrollContainer _backpackScroll = null!;
    private LootWeaponRackView _lootWeaponRack = null!;
    private LootDropZone _helmetSlot = null!;
    private LootDropZone _armorSlot = null!;
    private LootDropZone _packSlot = null!;
    private Label _helmetSlotLabel = null!;
    private Label _armorSlotLabel = null!;
    private Label _packSlotLabel = null!;
    private Label _helmetSlotCaption = null!;
    private Label _armorSlotCaption = null!;
    private Label _packSlotCaption = null!;
    private Label _backpackItemsCaption = null!;
    private Label _backpackValueLabel = null!;
    private Label _lootTitle = null!;
    private Label _lootStats = null!;
    private Label _lootSourceCaption = null!;
    private Label _backpackCaption = null!;
    private Button _lootCloseButton = null!;
    private Button _backpackHotkeyButton = null!;
    private Label _backpackHotkeyValue = null!;
    private InventoryModelPreview _helmetPreview = null!;
    private InventoryModelPreview _armorPreview = null!;
    private InventoryModelPreview _packPreview = null!;
    private ColorRect _weaponDetailOverlay = null!;
    private Label _weaponDetailTitle = null!;
    private Label _weaponDetailStatsCaption = null!;
    private Label _weaponDetailStats = null!;
    private Label _weaponDetailPartsCaption = null!;
    private GridContainer _weaponDetailParts = null!;
    private InventoryModelPreview _weaponDetailPreview = null!;
    private WeaponBuild? _detailedWeapon;
    private IReadOnlyList<LootItem>? _shownLoot;
    private TacticalPlayer? _shownPlayer;
    private string _shownLootName = string.Empty;
    private bool _shownSourceAvailable;
    private string _lastFireMode = "AUTO";
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
        if (IsSquadLobbyVisible || IsOperationsOfficeVisible || IsDemolitionBriefingVisible || IsDemolitionBuyVisible)
        {
            return;
        }
        if (@event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.Escape)
        {
            if (IsWeaponDetailVisible)
            {
                HideWeaponDetails();
            }
            else if (IsLootVisible)
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
        var canvasRoot = new Control
        {
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        canvasRoot.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(canvasRoot);

        _gameplayHudRoot = new Control
        {
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _gameplayHudRoot.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        canvasRoot.AddChild(_gameplayHudRoot);
        var root = _gameplayHudRoot;

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

        _statusHudRoot = Panel(root, Vector2.Zero, new Vector2(StatusHudWidth, StatusHudHeight));
        var status = _statusHudRoot;
        status.AnchorLeft = 0.0f;
        status.AnchorTop = 1.0f;
        status.AnchorRight = 0.0f;
        status.AnchorBottom = 1.0f;
        status.OffsetLeft = StatusHudLeftMargin;
        status.OffsetTop = -(StatusHudBottomMargin + StatusHudHeight);
        status.OffsetRight = StatusHudRightEdge;
        status.OffsetBottom = -StatusHudBottomMargin;
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

        _weaponHudRoot = Panel(root, Vector2.Zero, new Vector2(WeaponHudWidth, WeaponHudHeight));
        var weapon = _weaponHudRoot;
        weapon.MouseFilter = Control.MouseFilterEnum.Pass;
        weapon.AnchorLeft = 1.0f;
        weapon.AnchorTop = 1.0f;
        weapon.AnchorRight = 1.0f;
        weapon.AnchorBottom = 1.0f;
        weapon.OffsetLeft = -(WeaponHudRightMargin + WeaponHudWidth);
        weapon.OffsetTop = -(WeaponHudBottomMargin + WeaponHudHeight);
        weapon.OffsetRight = -WeaponHudRightMargin;
        weapon.OffsetBottom = -WeaponHudBottomMargin;
        _ammoLabel = Label("30", 42, new Color(0.95f, 0.98f, 0.95f));
        _ammoLabel.Position = new Vector2(22, 4);
        weapon.AddChild(_ammoLabel);
        _reserveLabel = Label("/ 150", 18, new Color(0.54f, 0.65f, 0.62f));
        _reserveLabel.Position = new Vector2(78, 23);
        weapon.AddChild(_reserveLabel);
        var quickSlotScene = GD.Load<PackedScene>("res://ui/QuickSlotBarView.tscn")
            ?? throw new InvalidOperationException("Unable to load res://ui/QuickSlotBarView.tscn");
        _quickSlotBar = quickSlotScene.Instantiate<QuickSlotBarView>();
        _quickSlotBar.Position = new Vector2(118, 7);
        _quickSlotBar.SlotRequested += slot => EmitSignal(SignalName.WeaponSlotRequested, slot);
        weapon.AddChild(_quickSlotBar);
        _weaponModeLabel = PositionedLabel("M4A1   AUTO", 12, new Color(0.4f, 0.82f, 0.71f), 23, 62);
        _weaponModeLabel.Size = new Vector2(460, 22);
        _weaponModeLabel.ClipText = true;
        weapon.AddChild(_weaponModeLabel);
        BuildAmmoTierHud(weapon);

        // Bottom-right backpack control: open inventory + live total value.
        _backpackHotkeyButton = Button(
            "TAB  BACKPACK",
            Vector2.Zero,
            new Vector2(BackpackHudWidth, BackpackHudHeight));
        _backpackHotkeyButton.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
        _backpackHotkeyButton.Position = new Vector2(
            -(BackpackHudRightMargin + BackpackHudWidth),
            -BackpackHudBottomOffset);
        _backpackHotkeyButton.FocusMode = Control.FocusModeEnum.None;
        _backpackHotkeyButton.AddThemeFontSizeOverride("font_size", 13);
        _backpackHotkeyButton.Pressed += () => EmitSignal(SignalName.InventoryToggleRequested);
        root.AddChild(_backpackHotkeyButton);
        _backpackHotkeyValue = PositionedLabel("VALUE  0", 12, new Color(0.95f, 0.78f, 0.28f), 12, 28);
        _backpackHotkeyValue.Size = new Vector2(186, 18);
        _backpackHotkeyValue.MouseFilter = Control.MouseFilterEnum.Ignore;
        _backpackHotkeyButton.AddChild(_backpackHotkeyValue);

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
        _operationBanner.Position = new Vector2(-250, 176);
        _operationBanner.Size = new Vector2(500, 50);
        root.AddChild(_operationBanner);
        var bannerTween = CreateTween();
        bannerTween.TweenInterval(2.0f);
        bannerTween.TweenProperty(_operationBanner, "modulate:a", 0.0f, 1.2f);

        BuildIncomingDamageHud(root);
        BuildMedicalHud(root);
        BuildTacticalHud(root);
        BuildExtractionHud(root);
        BuildDownedBanner(root);
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
        _stateSubtitle.Position = new Vector2(-450, 7);
        _stateSubtitle.Size = new Vector2(900, 330);
        _stateOverlay.AddChild(_stateSubtitle);
        BuildPauseMenu(root);
        BuildLootOverlay(root);
        BuildSquadHud(canvasRoot);
        BuildOperationsOfficeHud(canvasRoot);
        BuildDemolitionBuyHud(canvasRoot);
        root.MoveChild(_lootOverlay, root.GetChildCount() - 1);
    }

    private void BuildDownedBanner(Control root)
    {
        _downedBanner = new ColorRect
        {
            Color = new Color(0.025f, 0.018f, 0.018f, 0.8f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false,
            ZIndex = 20
        };
        _downedBanner.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
        _downedBanner.Position = new Vector2(-330, -126);
        _downedBanner.Size = new Vector2(660, 82);
        root.AddChild(_downedBanner);
        _downedBanner.AddChild(new ColorRect
        {
            Color = new Color(0.96f, 0.2f, 0.14f, 0.95f),
            Position = Vector2.Zero,
            Size = new Vector2(5, 82),
            MouseFilter = Control.MouseFilterEnum.Ignore
        });
        _downedBanner.AddChild(new ColorRect
        {
            Color = new Color(0.96f, 0.2f, 0.14f, 0.72f),
            Position = new Vector2(5, 0),
            Size = new Vector2(655, 2),
            MouseFilter = Control.MouseFilterEnum.Ignore
        });
        _downedTitle = Label("DOWNED", 22, new Color(1.0f, 0.38f, 0.28f));
        _downedTitle.Position = new Vector2(22, 9);
        _downedTitle.Size = new Vector2(616, 30);
        _downedTitle.HorizontalAlignment = HorizontalAlignment.Center;
        _downedBanner.AddChild(_downedTitle);
        _downedSubtitle = Label("CRAWL TO COVER  //  AWAITING MEDIC", 13, new Color(0.9f, 0.88f, 0.84f));
        _downedSubtitle.Position = new Vector2(22, 43);
        _downedSubtitle.Size = new Vector2(616, 24);
        _downedSubtitle.HorizontalAlignment = HorizontalAlignment.Center;
        _downedBanner.AddChild(_downedSubtitle);
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
            ZIndex = 50,
            Visible = false
        };
        _lootOverlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(_lootOverlay);

        var panel = new ColorRect
        {
            Color = new Color(0.025f, 0.032f, 0.033f, 0.98f),
            Position = new Vector2(-900, -460),
            Size = new Vector2(1800, 920)
        };
        panel.SetAnchorsPreset(Control.LayoutPreset.Center);
        _lootOverlay.AddChild(panel);
        panel.AddChild(new ColorRect
        {
            Color = new Color(0.18f, 0.78f, 0.66f),
            Position = Vector2.Zero,
            Size = new Vector2(5, 920)
        });

        _lootTitle = PositionedLabel("FIELD INVENTORY", 25, new Color(0.83f, 0.96f, 0.91f), 38, 24);
        _lootTitle.Size = new Vector2(1530, 36);
        panel.AddChild(_lootTitle);
        _lootCloseButton = Button("CLOSE", new Vector2(1610, 22), new Vector2(160, 40));
        _lootCloseButton.Pressed += () => EmitSignal(SignalName.LootClosed);
        panel.AddChild(_lootCloseButton);
        panel.AddChild(new ColorRect
        {
            Color = new Color(0.18f, 0.32f, 0.29f, 0.9f),
            Position = new Vector2(38, 76),
            Size = new Vector2(1732, 1)
        });

        _lootSourceCaption = PositionedLabel("SEARCHED GEAR", 14, new Color(0.43f, 0.88f, 0.73f), 40, 92);
        _lootSourceCaption.Size = new Vector2(872, 26);
        panel.AddChild(_lootSourceCaption);
        _backpackCaption = PositionedLabel("EQUIPPED / BACKPACK", 14, new Color(0.43f, 0.72f, 0.96f), 1200, 92);
        _backpackCaption.Size = new Vector2(570, 26);
        panel.AddChild(_backpackCaption);

        _lootSourceZone = new LootDropZone
        {
            Target = LootDropTarget.Source,
            Position = new Vector2(32, 122),
            Size = new Vector2(880, 716)
        };
        _lootSourceZone.AddThemeStyleboxOverride("panel", LootDropZone.ZoneStyle(new Color(0.22f, 0.85f, 0.68f)));
        _lootSourceZone.Dropped += HandleLootDrop;
        panel.AddChild(_lootSourceZone);
        var sourceScroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            MouseFilter = Control.MouseFilterEnum.Pass,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        _lootSourceZone.AddChild(sourceScroll);
        _lootSourceList = new GridContainer
        {
            Columns = 3,
            MouseFilter = Control.MouseFilterEnum.Pass,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _lootSourceList.AddThemeConstantOverride("h_separation", 8);
        _lootSourceList.AddThemeConstantOverride("v_separation", 8);
        sourceScroll.AddChild(_lootSourceList);

        _lootStats = PositionedLabel("", 13, new Color(0.78f, 0.86f, 0.83f), 1200, 126);
        _lootStats.Size = new Vector2(570, 42);
        _lootStats.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        panel.AddChild(_lootStats);

        BuildLootOperatorDisplay(panel);
        var weaponRackScene = GD.Load<PackedScene>(LootWeaponRackView.ScenePath)
            ?? throw new InvalidOperationException($"Missing scene: {LootWeaponRackView.ScenePath}");
        _lootWeaponRack = weaponRackScene.Instantiate<LootWeaponRackView>();
        _lootWeaponRack.Position = new Vector2(1200, 174);
        _lootWeaponRack.Size = new Vector2(184, 296);
        _lootWeaponRack.Dropped += HandleLootDrop;
        _lootWeaponRack.WeaponDetailsRequested += ShowLootWeaponDetails;
        panel.AddChild(_lootWeaponRack);
        _helmetSlot = BuildEquipmentSlot(panel, LootDropTarget.Helmet, "HELMET", new Vector2(1610, 174), new Vector2(160, 88), new Color(0.84f, 0.66f, 0.3f), out _helmetSlotCaption, out _helmetSlotLabel, out _helmetPreview);
        _armorSlot = BuildEquipmentSlot(panel, LootDropTarget.BodyArmor, "BODY ARMOR", new Vector2(1610, 268), new Vector2(160, 96), new Color(0.35f, 0.68f, 0.94f), out _armorSlotCaption, out _armorSlotLabel, out _armorPreview);
        _packSlot = BuildEquipmentSlot(panel, LootDropTarget.BackpackGear, "BACKPACK CONTAINER", new Vector2(1610, 370), new Vector2(160, 100), new Color(0.62f, 0.55f, 0.86f), out _packSlotCaption, out _packSlotLabel, out _packPreview);

        _backpackItemsCaption = PositionedLabel("BACKPACK STORAGE", 12, new Color(0.43f, 0.72f, 0.96f), 940, 484);
        _backpackItemsCaption.Size = new Vector2(340, 22);
        panel.AddChild(_backpackItemsCaption);

        var backpackZone = new LootDropZone
        {
            Target = LootDropTarget.Backpack,
            Position = new Vector2(940, 510),
            Size = new Vector2(830, 328)
        };
        _backpackZone = backpackZone;
        _backpackZone.AddThemeStyleboxOverride("panel", LootDropZone.ZoneStyle(new Color(0.32f, 0.62f, 0.92f)));
        _backpackZone.Dropped += HandleLootDrop;
        panel.AddChild(_backpackZone);
        _backpackScroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            MouseFilter = Control.MouseFilterEnum.Pass,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        _backpackZone.AddChild(_backpackScroll);
        _backpackList = new GridContainer
        {
            Columns = 2,
            MouseFilter = Control.MouseFilterEnum.Pass,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _backpackList.AddThemeConstantOverride("h_separation", 8);
        _backpackList.AddThemeConstantOverride("v_separation", 8);
        _backpackScroll.AddChild(_backpackList);

        _groundDropZone = new LootDropZone
        {
            Target = LootDropTarget.Ground,
            Position = new Vector2(32, 842),
            Size = new Vector2(1738, 56),
            TooltipText = string.Empty
        };
        _groundDropZone.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        _groundDropZone.Dropped += HandleLootDrop;
        panel.AddChild(_groundDropZone);

        BuildWeaponDetailOverlay();
        BuildLootItemActionMenu();
    }

    private LootDropZone BuildEquipmentSlot(
        Control parent,
        LootDropTarget target,
        string title,
        Vector2 position,
        Vector2 size,
        Color accent,
        out Label caption,
        out Label content,
        out InventoryModelPreview preview)
    {
        var zone = new LootDropZone
        {
            Target = target,
            Position = position,
            Size = size
        };
        zone.AddThemeStyleboxOverride("panel", LootDropZone.ZoneStyle(accent));
        zone.Dropped += HandleLootDrop;
        parent.AddChild(zone);
        var box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        zone.AddChild(box);
        caption = Label(title, 11, accent);
        caption.MouseFilter = Control.MouseFilterEnum.Ignore;
        box.AddChild(caption);
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        box.AddChild(row);
        preview = new InventoryModelPreview
        {
            CustomMinimumSize = new Vector2(62, 62),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        preview.Configure(target switch
        {
            LootDropTarget.Helmet => InventoryPreviewKind.Helmet,
            LootDropTarget.BodyArmor => InventoryPreviewKind.BodyArmor,
            _ => InventoryPreviewKind.Backpack
        });
        row.AddChild(preview);
        content = Label("", 11, new Color(0.78f, 0.86f, 0.83f));
        content.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        content.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        content.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        content.MouseFilter = Control.MouseFilterEnum.Ignore;
        row.AddChild(content);
        return zone;
    }

    private void BuildWeaponDetailOverlay()
    {
        _weaponDetailOverlay = new ColorRect
        {
            Color = new Color(0.002f, 0.005f, 0.006f, 0.88f),
            MouseFilter = Control.MouseFilterEnum.Stop,
            Visible = false
        };
        _weaponDetailOverlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _lootOverlay.AddChild(_weaponDetailOverlay);
        var panel = new ColorRect
        {
            Color = new Color(0.026f, 0.035f, 0.035f, 0.995f),
            Position = new Vector2(-430, -275),
            Size = new Vector2(860, 550),
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        panel.SetAnchorsPreset(Control.LayoutPreset.Center);
        _weaponDetailOverlay.AddChild(panel);
        panel.AddChild(new ColorRect
        {
            Color = new Color(0.25f, 0.9f, 0.7f),
            Position = Vector2.Zero,
            Size = new Vector2(5, 550),
            MouseFilter = Control.MouseFilterEnum.Ignore
        });
        _weaponDetailTitle = PositionedLabel("WEAPON DETAILS", 24, new Color(0.84f, 0.96f, 0.91f), 32, 24);
        _weaponDetailTitle.Size = new Vector2(720, 36);
        panel.AddChild(_weaponDetailTitle);
        var close = Button("X", new Vector2(790, 20), new Vector2(42, 38));
        close.TooltipText = "CLOSE";
        close.Pressed += HideWeaponDetails;
        panel.AddChild(close);

        var previewBand = new ColorRect
        {
            Color = new Color(0.014f, 0.021f, 0.022f, 0.96f),
            Position = new Vector2(32, 76),
            Size = new Vector2(796, 148),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        panel.AddChild(previewBand);
        _weaponDetailPreview = new InventoryModelPreview
        {
            Position = new Vector2(20, 18),
            Size = new Vector2(756, 112)
        };
        _weaponDetailPreview.Configure(InventoryPreviewKind.Rifle, weapon: WeaponCatalog.StarterWeapon());
        previewBand.AddChild(_weaponDetailPreview);

        _weaponDetailStatsCaption = PositionedLabel("FINAL WEAPON STATS", 12, new Color(0.38f, 0.82f, 0.7f), 32, 242);
        _weaponDetailStatsCaption.Size = new Vector2(796, 20);
        panel.AddChild(_weaponDetailStatsCaption);
        _weaponDetailStats = PositionedLabel("", 14, new Color(0.8f, 0.88f, 0.85f), 32, 268);
        _weaponDetailStats.Size = new Vector2(796, 48);
        _weaponDetailStats.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        panel.AddChild(_weaponDetailStats);
        panel.AddChild(new ColorRect
        {
            Color = new Color(0.14f, 0.26f, 0.24f, 0.85f),
            Position = new Vector2(32, 319),
            Size = new Vector2(796, 1),
            MouseFilter = Control.MouseFilterEnum.Ignore
        });
        _weaponDetailPartsCaption = PositionedLabel("FITTED COMPONENTS", 12, new Color(0.38f, 0.72f, 0.94f), 32, 334);
        _weaponDetailPartsCaption.Size = new Vector2(796, 20);
        panel.AddChild(_weaponDetailPartsCaption);
        _weaponDetailParts = new GridContainer
        {
            Columns = 2,
            Position = new Vector2(32, 360),
            Size = new Vector2(796, 164),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _weaponDetailParts.AddThemeConstantOverride("h_separation", 12);
        _weaponDetailParts.AddThemeConstantOverride("v_separation", 5);
        panel.AddChild(_weaponDetailParts);
    }

    public bool IsWeaponDetailVisible => IsInstanceValid(_weaponDetailOverlay) && _weaponDetailOverlay.Visible;
    public WeaponPlatform? DetailedWeaponPlatformForDiagnostics => _detailedWeapon?.Platform;

    public void ShowWeaponDetails(WeaponBuild weapon)
    {
        if (!IsLootVisible)
        {
            return;
        }
        _detailedWeapon = weapon.Clone();
        var stats = weapon.Stats();
        var name = weapon.DisplayName(_language);
        _weaponDetailTitle.Text = $"{Text("weapon_details", "WEAPON DETAILS")}  //  {name}";
        _weaponDetailStatsCaption.Text = Text("final_stats", "FINAL WEAPON STATS");
        _weaponDetailPartsCaption.Text = Text("fitted_parts", "FITTED COMPONENTS");
        _weaponDetailPreview.Configure(InventoryPreviewKind.Rifle, weapon: weapon);
        _weaponDetailStats.Text = GameLocalization.IsChinese(_language)
            ? $"伤害 {stats.Damage:0}     有效射程 {stats.EffectiveRange:0}m     后坐 {stats.Recoil:0.00}     操控 {stats.Handling:0.00}\n射速 {60.0f / stats.FireInterval:0} 发/分     弹匣 {stats.MagazineSize}     枪声半径 {stats.SoundRadius:0}m"
            : $"DAMAGE {stats.Damage:0}     RANGE {stats.EffectiveRange:0}m     RECOIL {stats.Recoil:0.00}     HANDLING {stats.Handling:0.00}\nRATE {60.0f / stats.FireInterval:0} RPM     MAGAZINE {stats.MagazineSize}     REPORT RADIUS {stats.SoundRadius:0}m";

        ClearRows(_weaponDetailParts);
        foreach (var slot in Enum.GetValues<AttachmentSlot>())
        {
            var item = new VBoxContainer
            {
                CustomMinimumSize = new Vector2(388, 48),
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            item.AddThemeConstantOverride("separation", 1);
            var slotName = GameLocalization.IsChinese(_language)
                ? WeaponCatalog.SlotChinese(slot)
                : slot.ToString().ToUpperInvariant();
            var slotLabel = Label(slotName, 10, new Color(0.36f, 0.72f, 0.92f));
            slotLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
            item.AddChild(slotLabel);
            var value = Label("", 12, new Color(0.77f, 0.86f, 0.83f));
            value.MouseFilter = Control.MouseFilterEnum.Ignore;
            value.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            if (weapon.Attachments.TryGetValue(slot, out var partId))
            {
                var part = WeaponCatalog.Attachment(partId);
                var partName = GameLocalization.IsChinese(_language) ? part.ChineseName : part.Name;
                value.Text = $"{partName}  //  {part.EffectDetail(_language)}";
            }
            else
            {
                value.Text = Text("empty_slot", "EMPTY SLOT");
                value.AddThemeColorOverride("font_color", new Color(0.43f, 0.5f, 0.48f));
            }
            item.AddChild(value);
            _weaponDetailParts.AddChild(item);
        }
        _weaponDetailOverlay.Visible = true;
    }

    public void HideWeaponDetails()
    {
        _weaponDetailOverlay.Visible = false;
        _detailedWeapon = null;
    }

    public bool IsLootVisible => IsInstanceValid(_lootOverlay) && _lootOverlay.Visible;

    public void ShowLoot(string sourceName, IReadOnlyList<LootItem> items, TacticalPlayer player, bool sourceAvailable = true)
    {
        _shownLootName = sourceName;
        _shownLoot = items;
        _shownPlayer = player;
        _shownSourceAvailable = sourceAvailable;
        _lootOverlay.Visible = true;
        _weaponDetailOverlay.Visible = false;
        _detailedWeapon = null;
        RefreshLootOverlay();
    }

    public void HideLoot()
    {
        DismissLootItemActionMenu();
        _weaponDetailOverlay.Visible = false;
        _detailedWeapon = null;
        _lootOverlay.Visible = false;
        _shownLoot = null;
        _shownPlayer = null;
        _shownSourceAvailable = false;
    }

    public void RefreshLootOverlay()
    {
        if (!IsLootVisible || _shownLoot is null || _shownPlayer is null)
        {
            return;
        }
        DismissLootItemActionMenu();
        _lootTitle.Text = $"{Text("field_inventory", "FIELD INVENTORY")}  //  {_shownLootName}";
        var personalMode = !_shownSourceAvailable;
        _lootSourceCaption.Text = personalMode
            ? $"{Text("personal_backpack", "PERSONAL BACKPACK")}  {_shownPlayer.Backpack.Count}/{_shownPlayer.BackpackCapacity}"
            : Text("searched_gear", "SEARCHED GEAR");
        _backpackCaption.Text = personalMode
            ? Text("equipped_loadout", "CURRENT LOADOUT")
            : $"{Text("equipped_loadout", "CURRENT LOADOUT")}  //  {Text("backpack_container", "BACKPACK CONTAINER")}";
        _lootCloseButton.Text = Text("close", "CLOSE");
        _backpackItemsCaption.Text = $"{Text("backpack_storage", "BACKPACK STORAGE")}  {_shownPlayer.Backpack.Count}/{_shownPlayer.BackpackCapacity}";
        _lootSourceZone.Enabled = _shownSourceAvailable;
        _lootSourceZone.Visible = _shownSourceAvailable;
        _backpackItemsCaption.Visible = _shownSourceAvailable;
        _backpackZone.Position = _shownSourceAvailable ? new Vector2(940, 510) : new Vector2(32, 122);
        _backpackZone.Size = _shownSourceAvailable ? new Vector2(830, 328) : new Vector2(1138, 716);

        var primaryWeapon = _shownPlayer.PrimaryWeaponBuild;
        _lootWeaponRack.SetLoadout(
            _language,
            primaryWeapon,
            _shownPlayer.PrimaryWeaponGrade,
            _shownPlayer.SecondaryWeaponBuild,
            _shownPlayer.SecondaryWeaponGrade,
            _shownPlayer.SidearmWeaponBuild,
            _shownPlayer.SidearmWeaponGrade);
        if (primaryWeapon is not null)
        {
            var stats = primaryWeapon.Stats();
            var weaponName = primaryWeapon.DisplayName(_language);
            _lootStats.Text = GameLocalization.IsChinese(_language)
                ? $"当前主武器  {weaponName}     伤害 {stats.Damage:0}     射程 {stats.EffectiveRange:0}m     操控 {stats.Handling:0.00}"
                : $"PRIMARY  {weaponName}     DMG {stats.Damage:0}     RANGE {stats.EffectiveRange:0}m     HANDLING {stats.Handling:0.00}";
        }
        else
        {
            _lootStats.Text = Text("comparison_no_primary", "NO PRIMARY WEAPON EQUIPPED");
        }
        _helmetSlotLabel.Text = _shownPlayer.EquippedHelmet.DisplayName(_language) + "\n" + _shownPlayer.EquippedHelmet.Detail(_language);
        _armorSlotLabel.Text = _shownPlayer.EquippedBodyArmor.DisplayName(_language) + "\n" + _shownPlayer.EquippedBodyArmor.Detail(_language);
        _packSlotLabel.Text = _shownPlayer.EquippedBackpack.DisplayName(_language) + "\n" + _shownPlayer.EquippedBackpack.Detail(_language);
        _helmetPreview.Configure(InventoryPreviewKind.Helmet, _shownPlayer.EquippedHelmet);
        _armorPreview.Configure(InventoryPreviewKind.BodyArmor, _shownPlayer.EquippedBodyArmor);
        _packPreview.Configure(InventoryPreviewKind.Backpack, _shownPlayer.EquippedBackpack);
        RefreshEquippedQualityStyles();
        _lootOperatorPreview.Configure(
            InventoryPreviewKind.Operator,
            weapon: _shownPlayer.HasFireablePrimary ? _shownPlayer.EquippedWeapon : null,
            knifeSkinId: _shownPlayer.EquippedKnifeSkinId,
            role: _shownPlayer.Role,
            helmet: _shownPlayer.EquippedHelmet,
            bodyArmor: _shownPlayer.EquippedBodyArmor,
            backpack: _shownPlayer.EquippedBackpack);
        _lootOperatorCaption.Text = $"{OperatorRoles.RoleName(_shownPlayer.Role, _language)}  //  {Text("active_loadout", "ACTIVE LOADOUT")}";

        ClearRows(_lootSourceList);
        _lootSourceList.Columns = 3;
        foreach (var item in _shownLoot)
        {
            _lootSourceList.AddChild(BuildLootCard(item, LootDragOrigin.Source));
        }
        if (_shownLoot.Count == 0 && _shownSourceAvailable)
        {
            _lootSourceList.Columns = 1;
            _lootSourceList.AddChild(Label(Text("empty", "EMPTY"), 15, new Color(0.48f, 0.54f, 0.52f)));
        }

        ClearRows(_backpackList);
        _backpackList.Columns = 4;
        foreach (var item in _shownPlayer.Backpack)
        {
            _backpackList.AddChild(BuildLootCard(item, LootDragOrigin.Backpack, compact: _shownSourceAvailable));
        }
        if (_shownPlayer.Backpack.Count == 0)
        {
            _backpackList.Columns = 1;
            _backpackList.AddChild(Label(Text("backpack_empty", "BACKPACK EMPTY"), 15, new Color(0.48f, 0.54f, 0.52f)));
        }

        var totalValue = ComputeBackpackTotalValue(_shownPlayer);
        if (!IsInstanceValid(_backpackValueLabel))
        {
            _backpackValueLabel = PositionedLabel(string.Empty, 14, new Color(0.95f, 0.78f, 0.28f), 1200, 812);
            _backpackValueLabel.Size = new Vector2(570, 24);
            if (_lootOverlay.GetChildCount() > 0 && _lootOverlay.GetChild(0) is Control panel)
            {
                panel.AddChild(_backpackValueLabel);
            }
        }
        _backpackValueLabel.Position = _shownSourceAvailable ? new Vector2(1290, 484) : new Vector2(1200, 812);
        _backpackValueLabel.Size = _shownSourceAvailable ? new Vector2(480, 22) : new Vector2(570, 24);
        _backpackValueLabel.HorizontalAlignment = _shownSourceAvailable
            ? HorizontalAlignment.Right
            : HorizontalAlignment.Left;
        _backpackValueLabel.Text = GameLocalization.IsChinese(_language)
            ? $"背包总估值  {totalValue}  //  枪械/装备/弹药合计"
            : $"BACKPACK VALUE  {totalValue}  //  GUNS + GEAR + AMMO";
        UpdateBackpackHotkey(_shownPlayer);
    }

    public void UpdateBackpackHotkey(TacticalPlayer? player)
    {
        if (!IsInstanceValid(_backpackHotkeyButton))
        {
            return;
        }
        _backpackHotkeyButton.Text = Text("backpack_button", "TAB  BACKPACK");
        var value = player is null ? 0 : ComputeBackpackTotalValue(player);
        if (IsInstanceValid(_backpackHotkeyValue))
        {
            _backpackHotkeyValue.Text = GameLocalization.IsChinese(_language)
                ? $"估值  {value}"
                : $"VALUE  {value}";
        }
    }

    /// <summary>Shipped value path: all three weapon slots, equipped gear, and backpack stacks.</summary>
    public static int ComputeBackpackTotalValue(TacticalPlayer player)
    {
        var total = LootItem.TotalValue(player.Backpack);
        total += WeaponSlotValue(player.PrimaryWeaponBuild, player.PrimaryWeaponGrade);
        total += WeaponSlotValue(player.SecondaryWeaponBuild, player.SecondaryWeaponGrade);
        total += WeaponSlotValue(player.SidearmWeaponBuild, player.SidearmWeaponGrade);
        total += new LootItem
        {
            Kind = LootItemKind.Equipment,
            Equipment = player.EquippedHelmet,
            Grade = player.EquippedHelmetGrade
        }.StackValue;
        total += new LootItem
        {
            Kind = LootItemKind.Equipment,
            Equipment = player.EquippedBodyArmor,
            Grade = player.EquippedBodyArmorGrade
        }.StackValue;
        total += new LootItem
        {
            Kind = LootItemKind.Equipment,
            Equipment = player.EquippedBackpack,
            Grade = player.EquippedBackpackGrade
        }.StackValue;
        return total;
    }

    private static int WeaponSlotValue(WeaponBuild? weapon, LootGrade grade)
        => weapon is null
            ? 0
            : new LootItem
            {
                Kind = LootItemKind.Weapon,
                Weapon = weapon,
                Grade = grade
            }.StackValue;

    private void ShowLootWeaponDetails(int slot)
    {
        if (_shownPlayer is null)
        {
            return;
        }
        var weapon = (PlayerWeaponSlot)slot switch
        {
            PlayerWeaponSlot.Primary => _shownPlayer.PrimaryWeaponBuild,
            PlayerWeaponSlot.Secondary => _shownPlayer.SecondaryWeaponBuild,
            PlayerWeaponSlot.Sidearm => _shownPlayer.SidearmWeaponBuild,
            _ => null
        };
        if (weapon is not null)
        {
            ShowWeaponDetails(weapon);
        }
    }

    private Control BuildLootCard(LootItem item, LootDragOrigin origin, bool compact = false)
    {
        EquipmentSlot? slot = item.Kind == LootItemKind.Equipment && item.Equipment is not null
            ? item.Equipment.Definition.Slot
            : null;
        var card = new LootDragCard();
        card.Configure(
            item.Id,
            origin,
            item.Kind,
            slot,
            item.DisplayName(_language),
            item.Detail(_language),
            item.Grade,
            item.Weapon,
            item.Equipment,
            Text("details", "DETAILS"),
            compact,
            BuildLootComparisons(item));
        card.DetailsRequested += ShowWeaponDetails;
        card.Activated += (_, cardOrigin) => HandleLootCardActivated(item, cardOrigin, card);
        return card;
    }

    private void HandleLootDrop(string itemId, LootDragOrigin origin, LootDropTarget target)
    {
        switch (target)
        {
            case LootDropTarget.PrimaryWeapon:
            case LootDropTarget.SecondaryWeapon:
            case LootDropTarget.SidearmWeapon:
                var slot = target switch
                {
                    LootDropTarget.SecondaryWeapon => PlayerWeaponSlot.Secondary,
                    LootDropTarget.SidearmWeapon => PlayerWeaponSlot.Sidearm,
                    _ => PlayerWeaponSlot.Primary
                };
                EmitSignal(SignalName.LootWeaponSlotRequested, itemId, (int)origin, (int)slot);
                break;
            case LootDropTarget.Backpack when origin == LootDragOrigin.Source:
                EmitSignal(SignalName.LootTakeRequested, itemId);
                break;
            case LootDropTarget.Source when origin == LootDragOrigin.Backpack:
                EmitSignal(SignalName.LootReturnRequested, itemId);
                break;
            case LootDropTarget.Ground when origin == LootDragOrigin.Backpack:
                EmitSignal(SignalName.BackpackDropRequested, itemId);
                break;
            default:
                if (origin == LootDragOrigin.Source)
                {
                    EmitSignal(SignalName.LootEquipRequested, itemId);
                }
                else
                {
                    EmitSignal(SignalName.BackpackUseRequested, itemId);
                }
                break;
        }
    }

    internal bool DropLootOnWeaponSlotForDiagnostics(
        LootItem item,
        PlayerWeaponSlot slot,
        LootDragOrigin origin = LootDragOrigin.Backpack)
        => IsInstanceValid(_lootWeaponRack)
            && _lootWeaponRack.DropForDiagnostics(item, origin, slot);

    private static void ClearRows(Node parent)
    {
        var children = parent.GetChildren();
        using var childrenBacking = children.AsDisposable();
        foreach (var child in children)
        {
            parent.RemoveChild(child);
            child.QueueFree();
        }
    }

    public void SetLanguage(string language)
    {
        _language = GameLocalization.IsChinese(language) ? "zh" : "en";
        if (IsInstanceValid(_pauseMenuView))
        {
            _pauseMenuView.SetLanguage(_language);
        }
        _vitalCaption.Text = Text("vital", "VITAL");
        _armorCaption.Text = Text("armor", "PLATE");
        _operationBanner.Text = Text("operation", "OPERATION STEEL TIDE");
        if (IsInstanceValid(_backpackHotkeyButton))
        {
            _backpackHotkeyButton.Text = Text("backpack_button", "TAB  BACKPACK");
        }
        RefreshQuickSlotBar();
        RefreshSquadLanguage();
        RefreshOperationsOfficeLanguage();
        RefreshDemolitionBuyLanguage();
        RefreshMedicalLanguage();
        RefreshTacticalLanguage();
        RefreshExtractionLanguage();
        RefreshLootOverlay();
        if (IsWeaponDetailVisible && _detailedWeapon is not null)
        {
            ShowWeaponDetails(_detailedWeapon);
        }
    }

    private string Text(string key, string english) => GameLocalization.Get(key, _language, english);

    public void SetStats(float health, float armor, float stamina, int ammo, int reserve, int grenades)
    {
        _healthLabel.Text = $"{Mathf.Max(0, (int)health):000}";
        _armorLabel.Text = $"{Mathf.Max(0, (int)armor):00}";
        _staminaBar.Value = stamina;
        _ammoLabel.Text = $"{ammo:00}";
        _reserveLabel.Text = $"/ {reserve:000}";
        _grenadeCount = Mathf.Max(0, grenades);
        RefreshQuickSlotBar();
        _healthLabel.AddThemeColorOverride("font_color", health < 30 ? new Color(1.0f, 0.36f, 0.25f) : new Color(0.88f, 0.96f, 0.92f));
        if (_shownPlayer is not null)
        {
            UpdateBackpackHotkey(_shownPlayer);
        }
    }

    public void SetStaminaRecoveryState(bool recovering)
    {
        if (IsInstanceValid(_staminaBar))
        {
            _staminaBar.Modulate = recovering
                ? new Color(1.0f, 0.55f, 0.22f)
                : new Color(0.46f, 0.92f, 0.68f);
        }
    }

    public void SetBackpackValuePlayer(TacticalPlayer player) => UpdateBackpackHotkey(player);

    public void SetEquipment(
        int armorPlates,
        string fireMode,
        string weaponName = "M4A1",
        WeaponBuild? weaponBuild = null,
        bool hasPrimary = true,
        string knifeSkinId = KnifeSkinCatalog.DefaultId,
        WeaponBuild? secondaryWeaponBuild = null,
        WeaponBuild? sidearmWeaponBuild = null,
        int activeWeaponSlot = 0)
    {
        _hasPrimary = hasPrimary;
        _activeWeaponSlot = Mathf.Clamp(activeWeaponSlot, 0, 5);
        _quickPrimaryBuild = hasPrimary ? weaponBuild : null;
        _quickSecondaryBuild = secondaryWeaponBuild;
        _quickSidearmBuild = sidearmWeaponBuild;
        _quickKnifeSkinId = knifeSkinId;
        _plateReserveLabel.Text = $"x{armorPlates}";
        _lastFireMode = fireMode;
        var mode = fireMode switch
        {
            "AUTO" => Text("auto", "AUTO"),
            "SEMI" => Text("semi", "SEMI"),
            "GRENADE" => Text("quick_throw", "THROW"),
            "UTILITY" => Text("quick_deploy", "DEPLOY"),
            _ => Text("knife", "KNIFE")
        };
        var displayWeapon = fireMode == "KNIFE" ? Text("tactical_knife", "TACTICAL KNIFE") : weaponName;
        _weaponModeLabel.Text = $"{displayWeapon}   {mode}";
        RefreshQuickSlotBar();
    }

    private void RefreshQuickSlotBar()
    {
        if (!IsInstanceValid(_quickSlotBar))
        {
            return;
        }
        _quickSlotBar.SetLoadout(
            _language,
            _quickPrimaryBuild,
            _hasPrimary,
            _quickSecondaryBuild,
            _quickSidearmBuild,
            _quickKnifeSkinId,
            _grenadeCount,
            _demolitionGameplayPresentation ? _demolitionSmokeGrenades : 0,
            _activeWeaponSlot);
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

    internal bool EquipmentCancelHintVisibleForDiagnostics
        => IsInstanceValid(_equipmentRoot)
        && _equipmentRoot.Visible
        && IsInstanceValid(_equipmentLabel)
        && _equipmentLabel.Text.Contains("X", StringComparison.OrdinalIgnoreCase);

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
            _interactionBar.Visible = progress >= 0.0f;
            if (_interactionBar.Visible)
            {
                _interactionBar.Value = Mathf.Clamp(progress, 0.0f, 1.0f);
            }
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

    public void ShowHit(bool kill = false, bool headshot = false, bool armorHit = false)
    {
        if (_hitTween?.IsRunning() == true)
        {
            _hitTween.Kill();
        }
        _hitmarker.Modulate = kill
            ? new Color(1.0f, 0.22f, 0.16f, 1.0f)
            : headshot ? new Color(1.0f, 0.72f, 0.24f, 1.0f)
            : armorHit ? new Color(0.38f, 0.72f, 1.0f, 1.0f)
            : Colors.White;
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

    public void ShowDamage(float strength = 0.58f)
    {
        if (_damageTween?.IsRunning() == true)
        {
            _damageTween.Kill();
        }
        _damageMaterial.SetShaderParameter("strength", Mathf.Clamp(strength, 0.0f, 1.0f));
        _damageTween = CreateTween();
        _damageTween.TweenProperty(_damageMaterial, "shader_parameter/strength", 0.0f, 0.55f);
    }

    public void SetAiming(bool active) => _crosshair.Visible = !active;

    public void ShowRadioMessage(string message, Color color)
    {
        if (_radioMessageDiagnosticSuppressionDepth > 0)
        {
            return;
        }
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

    public void ShowResult(
        bool victory,
        IReadOnlyList<(string Team, int Value, int Rank)>? lootRanks = null,
        int extractedValue = 0,
        int wallet = 0,
        bool profileSaved = true)
    {
        HideOperationsMenus();
        _gameplayHudRoot.Visible = true;
        _downedBanner.Visible = false;
        _stateOverlay.Visible = true;
        if (victory)
        {
            _stateTitle.Text = Text("mission_complete", "MISSION COMPLETE");
            _stateTitle.AddThemeColorOverride("font_color", new Color(0.33f, 0.92f, 0.74f));
            if (lootRanks is { Count: > 0 })
            {
                var lines = new System.Text.StringBuilder();
                lines.Append(Text("extract_rank_title", "EXTRACTION LOOT RANKING"));
                foreach (var row in lootRanks)
                {
                    lines.Append('\n');
                    lines.Append($"#{row.Rank}  {row.Team}  //  {row.Value}");
                }
                lines.Append('\n');
                lines.Append(Text("extract_rank_note", "BODY BAGS EXCLUDED FROM TEAM SCORE"));
                _stateSubtitle.Text = lines.ToString();
            }
            else
            {
                _stateSubtitle.Text = Text("terminal_secured", "FREIGHT TERMINAL SECURED");
            }
            var extractionLabel = profileSaved
                ? Text("extraction_bank", "EXTRACTED VALUE BANKED")
                : Text("extraction_unbanked", "EXTRACTED VALUE NOT BANKED");
            _stateSubtitle.Text += $"\n{extractionLabel}  +{Mathf.Max(0, extractedValue)}";
            _stateSubtitle.Text += $"\n{Text("profile_balance", "NEXT DEPLOYMENT BALANCE")}  {Mathf.Max(0, wallet)}";
            if (!profileSaved)
            {
                _stateSubtitle.Text += $"\n{Text("profile_save_warning", "PROFILE SAVE FAILED  //  VALUE NOT BANKED")}";
            }
        }
        else
        {
            _stateTitle.Text = Text("operator_down", "OPERATOR DOWN");
            _stateTitle.AddThemeColorOverride("font_color", new Color(1.0f, 0.27f, 0.18f));
            _stateSubtitle.Text = Text("press_enter", "PRESS ENTER TO REDEPLOY");
        }
    }

    public void ShowDownedState(float reviveWindowSeconds = 15.0f)
    {
        SetDownedFooterSuppressed(true);
        _downedBanner.Visible = true;
        UpdateDownedState(reviveWindowSeconds);
    }

    public void UpdateDownedState(float reviveWindowSeconds)
    {
        if (!_downedBanner.Visible)
        {
            return;
        }
        _downedTitle.Text = Text("downed_title", "OPERATOR DOWNED");
        _downedSubtitle.Text = $"{Text("spectating_teammate", "SPECTATING TEAMMATE")}  //  "
            + $"{Text("downed_wait", "AWAITING MEDIC")}  {Mathf.CeilToInt(Mathf.Max(0.0f, reviveWindowSeconds))}s";
    }

    public void HideDownedState()
    {
        _downedBanner.Visible = false;
        SetDownedFooterSuppressed(false);
    }

    public bool IsDownedBannerVisible => IsInstanceValid(_downedBanner) && _downedBanner.Visible;
}
