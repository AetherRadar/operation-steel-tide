using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public partial class CombatHUD
{
    private readonly Dictionary<string, Button> _deploymentWeaponButtons = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Label> _deploymentWeaponNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Label> _deploymentWeaponDetails = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Button> _deploymentArmorButtons = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Label> _deploymentArmorNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Label> _deploymentArmorDetails = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<LootGrade, Button> _deploymentAmmoButtons = new();
    private readonly Dictionary<int, Button> _deploymentAmmoQuantityButtons = new();
    private readonly Dictionary<string, Button> _deploymentMapButtons = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Button> _deploymentPresetButtons = new(StringComparer.OrdinalIgnoreCase);
    private readonly ProgressBar[] _deploymentMetricBars = new ProgressBar[3];
    private readonly Label[] _deploymentMetricLabels = new Label[3];
    private Label _deploymentCreditsLabel = null!;
    private Label _deploymentExtractedLabel = null!;
    private Label _deploymentRankLabel = null!;
    private Button _deploymentThreatButton = null!;
    private Button _deploymentTimeButton = null!;
    private DeploymentThreatLevel _selectedThreatLevel = DeploymentThreatLevel.Standard;
    private DeploymentTimeOfDay _selectedTimeOfDay = DeploymentTimeOfDay.Day;
    private Label _deploymentCostLabel = null!;
    private Label _deploymentStatusLabel = null!;
    private Label _deploymentMapCaption = null!;
    private Label _deploymentMapStatusLabel = null!;
    private Label _deploymentPresetCaption = null!;
    private Label _deploymentWeaponCaption = null!;
    private Label _deploymentArmorCaption = null!;
    private Label _deploymentAmmoCaption = null!;
    private Label _deploymentAmmoQuantityCaption = null!;
    private Label _deploymentOperatorName = null!;
    private Label _deploymentOperatorSkill = null!;
    private Label _deploymentGearReadout = null!;
    private Label _deploymentCombatReadout = null!;
    private InventoryModelPreview _deploymentOperatorPreview = null!;
    private OperatorProfileData _displayedProfile = new();
    private string _selectedWeaponId = "m3a1";
    private string _selectedArmorId = "patrol";
    private LootGrade _selectedAmmoGrade = LootGrade.Common;
    private int _selectedAmmoQuantity = 60;
    private string _selectedDeploymentMapId = DeploymentMapCatalog.FreightTerminalId;
    private string _deploymentError = string.Empty;

    public DeploymentLoadoutSelection SelectedDeploymentLoadout
        => new(_selectedWeaponId, _selectedArmorId, _selectedAmmoGrade, _selectedAmmoQuantity);

    public bool DeploymentUiReady => IsInstanceValid(_deploymentOperatorPreview)
        && _deploymentWeaponButtons.Count == DeploymentCatalog.Weapons.Count
        && _deploymentArmorButtons.Count == DeploymentCatalog.Armor.Count
        && _deploymentAmmoButtons.Count == Enum.GetValues<LootGrade>().Length
        && _deploymentAmmoQuantityButtons.Count == DeploymentCatalog.AmmoPacks.Count
        && _deploymentMapButtons.Count == DeploymentMapCatalog.Maps.Count;
    public int DeploymentPresetCount => _deploymentPresetButtons.Count;
    public int DeploymentAmmoPackCount => _deploymentAmmoQuantityButtons.Count;
    public int SelectedDeploymentAmmoQuantity => _selectedAmmoQuantity;
    public int DeploymentSelectedCost => DeploymentCatalog.Resolve(SelectedDeploymentLoadout).TotalCost;
    public int DeploymentProjectedBalance => _displayedProfile.Credits - DeploymentSelectedCost;
    public string ActiveDeploymentPresetId => MatchingPresetId();
    public string SelectedDeploymentMapId => _selectedDeploymentMapId;
    public int DeploymentMapCount => _deploymentMapButtons.Count;
    public int DeploymentRankLevel => OperatorReputation.LevelForPoints(_displayedProfile.ReputationPoints);
    public DeploymentThreatLevel SelectedDeploymentThreatLevel => _selectedThreatLevel;
    public DeploymentTimeOfDay SelectedDeploymentTimeOfDay => _selectedTimeOfDay;
    public void SetDeploymentTimeForDiagnostics(DeploymentTimeOfDay timeOfDay)
    {
        _selectedTimeOfDay = timeOfDay;
        RefreshDeploymentStore();
        EmitSignal(SignalName.DeploymentTimeOfDayChanged, (int)_selectedTimeOfDay);
    }
    public bool IsDeploymentThreatLocked(DeploymentThreatLevel level)
        => DeploymentRankLevel < ThreatLevels.RequiredReputationLevel(level);
    public void SetDeploymentThreatForDiagnostics(DeploymentThreatLevel level)
    {
        if (IsDeploymentThreatLocked(level))
        {
            return;
        }
        _selectedThreatLevel = level;
        RefreshDeploymentStore();
    }
    public bool IsDeploymentWeaponLocked(string weaponId)
        => DeploymentRankLevel < OperatorReputation.RequiredLevelForWeapon(weaponId);
    public bool IsDeploymentAmmoGradeLocked(LootGrade grade)
        => DeploymentRankLevel < OperatorReputation.RequiredLevelForAmmoGrade(grade);
    public bool DeploymentMapAvailable => IsMapSelectable(_selectedDeploymentMapId);

    private bool IsMapSelectable(string mapId)
        => DeploymentMapCatalog.IsAvailable(mapId)
            && DeploymentRankLevel >= OperatorReputation.RequiredLevelForMap(mapId);

    private void BuildDeploymentStore(Control panel)
    {
        BuildDeploymentHeader(panel);
        BuildOperatorPreview(panel);
        BuildDeploymentMarket(panel);
        RefreshDeploymentStore();
    }

    private void BuildDeploymentHeader(Control panel)
    {
        _deploymentMapCaption = DeploymentCaption("DEPLOYMENT MAP", new Vector2(340, 7), new Vector2(290, 18));
        panel.AddChild(_deploymentMapCaption);
        var mapGroup = new ButtonGroup();
        for (var index = 0; index < DeploymentMapCatalog.Maps.Count; index++)
        {
            var map = DeploymentMapCatalog.Maps[index];
            var accent = map.Available
                ? new Color(0.32f, 0.86f, 0.69f)
                : new Color(0.3f, 0.38f, 0.37f);
            var button = DeploymentSegment(new Vector2(340 + index * 99, 27), new Vector2(94, 37), accent);
            button.ToggleMode = true;
            button.ButtonGroup = mapGroup;
            button.Disabled = !map.Available;
            button.FocusMode = Control.FocusModeEnum.None;
            button.Pressed += () => SelectDeploymentMap(map.Id);
            button.TooltipText = $"{map.Code}  //  {map.EnglishName}\n{map.EnglishSubtitle}";
            panel.AddChild(button);
            _deploymentMapButtons[map.Id] = button;
        }
        _deploymentMapStatusLabel = Label(string.Empty, 9, new Color(0.5f, 0.68f, 0.63f));
        _deploymentMapStatusLabel.Position = new Vector2(340, 65);
        _deploymentMapStatusLabel.Size = new Vector2(290, 14);
        _deploymentMapStatusLabel.ClipText = true;
        _deploymentMapStatusLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
        panel.AddChild(_deploymentMapStatusLabel);

        _deploymentCreditsLabel = Label("BALANCE  18000", 15, new Color(1.0f, 0.76f, 0.25f));
        _deploymentCreditsLabel.Position = new Vector2(648, 18);
        _deploymentCreditsLabel.Size = new Vector2(174, 24);
        panel.AddChild(_deploymentCreditsLabel);

        _deploymentExtractedLabel = Label("EXTRACTED  0", 12, new Color(0.52f, 0.71f, 0.66f));
        _deploymentExtractedLabel.Position = new Vector2(825, 20);
        _deploymentExtractedLabel.Size = new Vector2(175, 22);
        panel.AddChild(_deploymentExtractedLabel);

        _deploymentRankLabel = Label(string.Empty, 9, new Color(0.66f, 0.82f, 0.72f));
        _deploymentRankLabel.Position = new Vector2(825, 42);
        _deploymentRankLabel.Size = new Vector2(175, 16);
        _deploymentRankLabel.ClipText = true;
        _deploymentRankLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
        panel.AddChild(_deploymentRankLabel);

        _deploymentThreatButton = Button(string.Empty, new Vector2(825, 66), new Vector2(175, 16));
        _deploymentThreatButton.FocusMode = Control.FocusModeEnum.None;
        _deploymentThreatButton.AddThemeFontSizeOverride("font_size", 9);
        _deploymentThreatButton.AddThemeColorOverride("font_color", new Color(0.95f, 0.78f, 0.5f));
        _deploymentThreatButton.AddThemeColorOverride("font_hover_color", new Color(1.0f, 0.9f, 0.7f));
        _deploymentThreatButton.Pressed += CycleDeploymentThreat;
        panel.AddChild(_deploymentThreatButton);

        _deploymentCostLabel = Label("KIT VALUE  5100", 15, new Color(0.72f, 0.94f, 0.84f));
        _deploymentCostLabel.Position = new Vector2(998, 18);
        _deploymentCostLabel.Size = new Vector2(162, 24);
        _deploymentCostLabel.HorizontalAlignment = HorizontalAlignment.Right;
        panel.AddChild(_deploymentCostLabel);

        _deploymentStatusLabel = Label(string.Empty, 11, new Color(0.5f, 0.68f, 0.63f));
        _deploymentStatusLabel.Position = new Vector2(648, 44);
        _deploymentStatusLabel.Size = new Vector2(512, 20);
        _deploymentStatusLabel.HorizontalAlignment = HorizontalAlignment.Right;
        panel.AddChild(_deploymentStatusLabel);

        _deploymentTimeButton = Button(string.Empty, new Vector2(648, 66), new Vector2(170, 16));
        _deploymentTimeButton.FocusMode = Control.FocusModeEnum.None;
        _deploymentTimeButton.AddThemeFontSizeOverride("font_size", 9);
        _deploymentTimeButton.AddThemeColorOverride("font_color", new Color(0.62f, 0.74f, 0.94f));
        _deploymentTimeButton.AddThemeColorOverride("font_hover_color", new Color(0.8f, 0.88f, 1.0f));
        _deploymentTimeButton.Pressed += () =>
        {
            _selectedTimeOfDay = (DeploymentTimeOfDay)(((int)_selectedTimeOfDay + 1) % 4);
            ClearDeploymentError();
            RefreshDeploymentStore();
            EmitSignal(SignalName.DeploymentTimeOfDayChanged, (int)_selectedTimeOfDay);
        };
        panel.AddChild(_deploymentTimeButton);
    }

    private void BuildOperatorPreview(Control panel)
    {
        var shell = new ColorRect
        {
            Position = new Vector2(234, 88),
            Size = new Vector2(400, 468),
            Color = new Color(0.008f, 0.015f, 0.016f, 0.91f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        panel.AddChild(shell);
        shell.AddChild(new ColorRect
        {
            Size = new Vector2(400, 3),
            Color = new Color(0.32f, 0.84f, 0.7f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        });
        shell.AddChild(new ColorRect
        {
            Position = new Vector2(0, 338),
            Size = new Vector2(400, 130),
            Color = new Color(0.006f, 0.011f, 0.012f, 0.68f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        });

        _deploymentOperatorPreview = new InventoryModelPreview
        {
            Position = new Vector2(4, 4),
            Size = new Vector2(392, 456)
        };
        _deploymentOperatorPreview.Configure(InventoryPreviewKind.Operator);
        shell.AddChild(_deploymentOperatorPreview);

        _deploymentOperatorName = Label("ASSAULT // STRIKE TEAM", 21, new Color(0.92f, 0.97f, 0.94f));
        _deploymentOperatorName.Position = new Vector2(18, 16);
        _deploymentOperatorName.Size = new Vector2(364, 30);
        shell.AddChild(_deploymentOperatorName);
        _deploymentOperatorSkill = Label("COMBAT OVERDRIVE", 11, new Color(1.0f, 0.58f, 0.2f));
        _deploymentOperatorSkill.Position = new Vector2(18, 46);
        _deploymentOperatorSkill.Size = new Vector2(364, 22);
        shell.AddChild(_deploymentOperatorSkill);

        _deploymentGearReadout = DeploymentReadout(shell, new Vector2(12, 344), new Vector2(150, 90));
        _deploymentCombatReadout = DeploymentReadout(shell, new Vector2(238, 344), new Vector2(150, 90));
        BuildDeploymentMetrics(shell);
    }

    private void BuildDeploymentMetrics(Control parent)
    {
        var colors = new[]
        {
            new Color(1.0f, 0.57f, 0.2f),
            new Color(0.3f, 0.75f, 1.0f),
            new Color(0.32f, 0.92f, 0.62f)
        };
        for (var index = 0; index < 3; index++)
        {
            var x = 13 + index * 128;
            _deploymentMetricLabels[index] = Label(string.Empty, 9, new Color(0.62f, 0.72f, 0.68f));
            _deploymentMetricLabels[index].Position = new Vector2(x, 438);
            _deploymentMetricLabels[index].Size = new Vector2(116, 16);
            parent.AddChild(_deploymentMetricLabels[index]);
            _deploymentMetricBars[index] = new ProgressBar
            {
                Position = new Vector2(x, 456),
                Size = new Vector2(116, 4),
                MinValue = 0,
                MaxValue = 100,
                ShowPercentage = false,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            _deploymentMetricBars[index].AddThemeStyleboxOverride("background", FlatStyle(new Color(0.08f, 0.1f, 0.095f), Colors.Transparent));
            _deploymentMetricBars[index].AddThemeStyleboxOverride("fill", FlatStyle(colors[index], Colors.Transparent));
            parent.AddChild(_deploymentMetricBars[index]);
        }
    }

    private void BuildDeploymentMarket(Control panel)
    {
        var market = new ColorRect
        {
            Position = new Vector2(650, 88),
            Size = new Vector2(530, 468),
            Color = new Color(0.009f, 0.016f, 0.017f, 0.94f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        panel.AddChild(market);
        market.AddChild(new ColorRect
        {
            Size = new Vector2(3, 468),
            Color = new Color(0.96f, 0.68f, 0.2f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        });

        _deploymentPresetCaption = DeploymentCaption("QUICK KITS", new Vector2(16, 8), new Vector2(180, 18));
        market.AddChild(_deploymentPresetCaption);
        var presetGroup = new ButtonGroup();
        for (var index = 0; index < DeploymentCatalog.Presets.Count; index++)
        {
            var preset = DeploymentCatalog.Presets[index];
            var button = DeploymentSegment(new Vector2(16 + index * 100, 29), new Vector2(94, 34), new Color(0.31f, 0.78f, 0.66f));
            button.ToggleMode = true;
            button.ButtonGroup = presetGroup;
            button.Pressed += () => ApplyDeploymentPreset(preset.Id);
            market.AddChild(button);
            _deploymentPresetButtons[preset.Id] = button;
        }

        _deploymentWeaponCaption = DeploymentCaption("PRIMARY MARKET", new Vector2(16, 70), new Vector2(220, 18));
        market.AddChild(_deploymentWeaponCaption);
        var weaponGroup = new ButtonGroup();
        for (var index = 0; index < DeploymentCatalog.Weapons.Count; index++)
        {
            var offer = DeploymentCatalog.Weapons[index];
            var position = new Vector2(16 + index % 3 * 168, 91 + index / 3 * 55);
            BuildWeaponOfferCard(market, offer, position, weaponGroup);
        }

        _deploymentArmorCaption = DeploymentCaption("PROTECTION", new Vector2(16, 258), new Vector2(220, 18));
        market.AddChild(_deploymentArmorCaption);
        var armorGroup = new ButtonGroup();
        var armorSpacing = DeploymentCatalog.Armor.Count == 4 ? 128 : 168;
        for (var index = 0; index < DeploymentCatalog.Armor.Count; index++)
        {
            var offer = DeploymentCatalog.Armor[index];
            BuildArmorOfferCard(market, offer, new Vector2(16 + index * armorSpacing, 278), armorGroup);
        }

        _deploymentAmmoCaption = DeploymentCaption("AMMUNITION GRADE  //  PRICE", new Vector2(16, 338), new Vector2(260, 18));
        market.AddChild(_deploymentAmmoCaption);
        var ammoGroup = new ButtonGroup();
        foreach (var grade in Enum.GetValues<LootGrade>())
        {
            var color = AmmoTiers.Color(grade);
            var button = DeploymentSegment(new Vector2(16 + (int)grade * 99, 357), new Vector2(91, 39), color);
            button.ToggleMode = true;
            button.ButtonGroup = ammoGroup;
            button.AddThemeColorOverride("font_pressed_color", color);
            button.Pressed += () =>
            {
                _selectedAmmoGrade = grade;
                ClearDeploymentError();
                RefreshDeploymentStore();
            };
            market.AddChild(button);
            _deploymentAmmoButtons[grade] = button;
        }

        _deploymentAmmoQuantityCaption = DeploymentCaption("AMMO COUNT  //  PRICE", new Vector2(16, 401), new Vector2(260, 18));
        market.AddChild(_deploymentAmmoQuantityCaption);
        var quantityGroup = new ButtonGroup();
        foreach (var pack in DeploymentCatalog.AmmoPacks)
        {
            var button = DeploymentSegment(new Vector2(16 + _deploymentAmmoQuantityButtons.Count * 123, 421), new Vector2(117, 38), new Color(0.35f, 0.72f, 1.0f));
            button.ToggleMode = true;
            button.ButtonGroup = quantityGroup;
            button.FocusMode = Control.FocusModeEnum.None;
            button.Pressed += () =>
            {
                _selectedAmmoQuantity = pack.Quantity;
                ClearDeploymentError();
                RefreshDeploymentStore();
            };
            button.TooltipText = Text(pack.LocalizationKey, pack.EnglishName);
            market.AddChild(button);
            _deploymentAmmoQuantityButtons[pack.Quantity] = button;
        }
    }

    private void BuildWeaponOfferCard(Control parent, DeploymentWeaponOffer offer, Vector2 position, ButtonGroup group)
    {
        var accent = offer.Platform switch
        {
            WeaponPlatform.M24 => new Color(1.0f, 0.68f, 0.2f),
            WeaponPlatform.MP5A5 => new Color(0.3f, 0.76f, 1.0f),
            WeaponPlatform.M3A1 => new Color(0.56f, 0.78f, 0.7f),
            WeaponPlatform.AK74 => new Color(0.84f, 0.68f, 0.34f),
            WeaponPlatform.ScarL => new Color(0.75f, 0.86f, 0.42f),
            WeaponPlatform.M4A1 => new Color(0.31f, 0.9f, 0.64f),
            _ => new Color(0.58f, 0.64f, 0.62f)
        };
        var button = DeploymentSegment(position, new Vector2(163, 52), accent);
        button.ToggleMode = true;
        button.ButtonGroup = group;
        button.Pressed += () =>
        {
            _selectedWeaponId = offer.Id;
            _selectedAmmoQuantity = offer.Platform is null
                ? 0
                : DeploymentCatalog.NormalizeAmmoQuantity(_selectedAmmoQuantity, offer.ReserveAmmo);
            ClearDeploymentError();
            RefreshDeploymentStore();
        };
        parent.AddChild(button);
        _deploymentWeaponButtons[offer.Id] = button;

        var preview = new InventoryModelPreview
        {
            Position = new Vector2(4, 4),
            Size = new Vector2(43, 44)
        };
        preview.Configure(
            offer.Platform is null ? InventoryPreviewKind.Knife : InventoryPreviewKind.Rifle,
            weapon: offer.Platform is null ? null : WeaponCatalog.Build(offer.Platform.Value, offer.BuildTier));
        button.AddChild(preview);

        var name = Label(string.Empty, 9, accent);
        name.Position = new Vector2(50, 4);
        name.Size = new Vector2(108, 17);
        name.ClipText = true;
        name.MouseFilter = Control.MouseFilterEnum.Ignore;
        button.AddChild(name);
        _deploymentWeaponNames[offer.Id] = name;
        var detail = Label(string.Empty, 8, new Color(0.61f, 0.7f, 0.67f));
        detail.Position = new Vector2(50, 21);
        detail.Size = new Vector2(108, 27);
        detail.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        detail.MouseFilter = Control.MouseFilterEnum.Ignore;
        button.AddChild(detail);
        _deploymentWeaponDetails[offer.Id] = detail;
    }

    private void BuildArmorOfferCard(Control parent, DeploymentArmorOffer offer, Vector2 position, ButtonGroup group)
    {
        var accent = offer.Id switch
        {
            "heavy" => new Color(1.0f, 0.62f, 0.22f),
            "patrol" => new Color(0.55f, 0.77f, 0.7f),
            "nvg" => new Color(0.42f, 0.95f, 0.42f),
            _ => new Color(0.36f, 0.76f, 1.0f)
        };
        var width = DeploymentCatalog.Armor.Count == 4 ? 122 : 163;
        var button = DeploymentSegment(position, new Vector2(width, 54), accent);
        button.ToggleMode = true;
        button.ButtonGroup = group;
        button.Pressed += () =>
        {
            _selectedArmorId = offer.Id;
            ClearDeploymentError();
            RefreshDeploymentStore();
        };
        parent.AddChild(button);
        _deploymentArmorButtons[offer.Id] = button;

        var isCompactArmor = DeploymentCatalog.Armor.Count == 4;
        var preview = new InventoryModelPreview
        {
            Position = new Vector2(5, 4),
            Size = isCompactArmor ? new Vector2(36, 34) : new Vector2(48, 46)
        };
        preview.Configure(InventoryPreviewKind.BodyArmor, equipment: EquipmentCatalog.Create(offer.BodyArmorId));
        button.AddChild(preview);
        var namePosX = isCompactArmor ? 44 : 57;
        var innerWidth = width - namePosX - 5;
        var name = Label(string.Empty, 9, accent);
        name.Position = new Vector2(namePosX, 5);
        name.Size = new Vector2(innerWidth, 18);
        name.ClipText = true;
        name.MouseFilter = Control.MouseFilterEnum.Ignore;
        button.AddChild(name);
        _deploymentArmorNames[offer.Id] = name;
        var detail = Label(string.Empty, 8, new Color(0.61f, 0.7f, 0.67f));
        detail.Position = new Vector2(namePosX, 24);
        detail.Size = new Vector2(innerWidth, 26);
        detail.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        detail.MouseFilter = Control.MouseFilterEnum.Ignore;
        button.AddChild(detail);
        _deploymentArmorDetails[offer.Id] = detail;
    }

    private static Label DeploymentReadout(Control parent, Vector2 position, Vector2 size)
    {
        var background = new ColorRect
        {
            Position = position,
            Size = size,
            Color = new Color(0.015f, 0.025f, 0.025f, 0.88f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        parent.AddChild(background);
        background.AddChild(new ColorRect
        {
            Size = new Vector2(2, size.Y),
            Color = new Color(0.34f, 0.78f, 0.67f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        });
        var label = Label(string.Empty, 9, new Color(0.68f, 0.78f, 0.74f));
        label.Position = new Vector2(10, 7);
        label.Size = new Vector2(size.X - 16, size.Y - 12);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        label.MouseFilter = Control.MouseFilterEnum.Ignore;
        background.AddChild(label);
        return label;
    }

    private static Label DeploymentCaption(string text, Vector2 position, Vector2 size)
    {
        var label = Label(text, 10, new Color(0.45f, 0.64f, 0.59f));
        label.Position = position;
        label.Size = size;
        label.MouseFilter = Control.MouseFilterEnum.Ignore;
        return label;
    }

    private static Button DeploymentSegment(Vector2 position, Vector2 size, Color accent)
    {
        var button = Button(string.Empty, position, size);
        button.FocusMode = Control.FocusModeEnum.None;
        button.AddThemeFontSizeOverride("font_size", 10);
        button.AddThemeColorOverride("font_color", new Color(0.72f, 0.8f, 0.77f));
        button.AddThemeColorOverride("font_hover_color", new Color(0.92f, 0.98f, 0.95f));
        button.AddThemeColorOverride("font_pressed_color", accent);
        button.AddThemeStyleboxOverride("normal", FlatStyle(new Color(0.035f, 0.05f, 0.049f), new Color(0.11f, 0.16f, 0.15f)));
        button.AddThemeStyleboxOverride("hover", FlatStyle(new Color(0.055f, 0.075f, 0.07f), accent.Darkened(0.25f)));
        button.AddThemeStyleboxOverride("pressed", FlatStyle(new Color(0.055f, 0.085f, 0.076f), accent, 2));
        button.AddThemeStyleboxOverride("hover_pressed", FlatStyle(new Color(0.065f, 0.1f, 0.09f), accent, 2));
        return button;
    }

    private static StyleBoxFlat FlatStyle(Color background, Color border, int borderWidth = 1)
    {
        return new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border,
            BorderWidthLeft = borderWidth,
            BorderWidthTop = borderWidth,
            BorderWidthRight = borderWidth,
            BorderWidthBottom = borderWidth,
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
            CornerRadiusBottomLeft = 3,
            CornerRadiusBottomRight = 3
        };
    }

    public void SetOperatorProfile(OperatorProfileData profile)
    {
        _displayedProfile = profile.Clone();
        var weapon = DeploymentCatalog.Weapon(profile.LastWeaponId);
        _selectedWeaponId = weapon.Id;
        _selectedArmorId = DeploymentCatalog.ArmorKit(profile.LastArmorId).Id;
        _selectedAmmoGrade = profile.LastAmmoGrade;
        _selectedAmmoQuantity = weapon.Platform is null
            ? 0
            : DeploymentCatalog.NormalizeAmmoQuantity(profile.LastAmmoQuantity, weapon.ReserveAmmo);
        RefreshDeploymentStore();
        RefreshOperationsOfficeProfile();
    }

    public void ShowDeploymentPurchaseError(string reason)
    {
        _deploymentError = reason switch
        {
            "insufficient_credits" => Text("loadout_insufficient", "INSUFFICIENT BALANCE  //  SELECT A CHEAPER KIT"),
            "reputation_locked" => Text("loadout_reputation_locked", "REPUTATION LOCKED  //  RAISE OPERATOR LEVEL FIRST"),
            _ => Text("loadout_save_failed", "PROFILE SAVE FAILED  //  DEPLOYMENT CANCELLED")
        };
        RefreshDeploymentStore();
    }

    public void ApplyDeploymentPresetForDiagnostics(string id) => ApplyDeploymentPreset(id);

    private void ApplyDeploymentPreset(string id)
    {
        var preset = DeploymentCatalog.Preset(id);
        _selectedWeaponId = preset.WeaponId;
        _selectedArmorId = preset.ArmorId;
        _selectedAmmoGrade = preset.AmmoGrade;
        _selectedAmmoQuantity = preset.WeaponId == "none"
            ? 0
            : DeploymentCatalog.NormalizeAmmoQuantity(preset.AmmoQuantity, DeploymentCatalog.Weapon(preset.WeaponId).ReserveAmmo);
        ClearDeploymentError();
        RefreshDeploymentStore();
    }

    public void SetDeploymentMapSelection(string id) => SelectDeploymentMap(id);

    public void ApplyDeploymentMapForDiagnostics(string id) => SetDeploymentMapSelection(id);

    private void SelectDeploymentMap(string id)
    {
        var map = DeploymentMapCatalog.Resolve(id);
        if (!IsMapSelectable(map.Id))
        {
            return;
        }
        _selectedDeploymentMapId = map.Id;
        ClearDeploymentError();
        RefreshDeploymentStore();
    }

    private void CycleDeploymentThreat()
    {
        for (var step = 1; step <= 2; step++)
        {
            var candidate = (DeploymentThreatLevel)(((int)_selectedThreatLevel + step) % 3);
            if (!IsDeploymentThreatLocked(candidate))
            {
                _selectedThreatLevel = candidate;
                ClearDeploymentError();
                RefreshDeploymentStore();
                return;
            }
        }
    }

    private void RefreshDeploymentThreat()
    {
        if (!IsInstanceValid(_deploymentThreatButton))
        {
            return;
        }
        if (IsDeploymentThreatLocked(_selectedThreatLevel))
        {
            _selectedThreatLevel = DeploymentThreatLevel.Standard;
        }
        var payoutPercent = Mathf.RoundToInt((ThreatLevels.PayoutMultiplier(_selectedThreatLevel) - 1.0f) * 100.0f);
        var chinese = GameLocalization.IsChinese(_language);
        _deploymentThreatButton.Text = chinese
            ? $"\u5a01\u80c1  {ThreatLevels.DisplayName(_selectedThreatLevel, _language)}  //  \u7ed3\u7b97 +{payoutPercent}%"
            : $"THREAT  {ThreatLevels.DisplayName(_selectedThreatLevel, _language)}  //  PAYOUT +{payoutPercent}%";
        var detectionPercent = Mathf.RoundToInt((ThreatLevels.DetectionMultiplier(_selectedThreatLevel) - 1.0f) * 100.0f);
        var accuracyPercent = Mathf.RoundToInt(ThreatLevels.AccuracyBonus(_selectedThreatLevel) * 100.0f);
        _deploymentThreatButton.TooltipText = chinese
            ? $"\u4fa6\u6d4b +{detectionPercent}%  \u7cbe\u5ea6 +{accuracyPercent}%  \u5feb\u901f\u53cd\u5e94\u90e8\u961f\u66f4\u9891  //  \u5347\u7ea7\u5a01\u80c1\u9700\u58f0\u671b L{ThreatLevels.RequiredReputationLevel(DeploymentThreatLevel.Elevated)}  \u6700\u9ad8\u5a01\u80c1\u9700 L{ThreatLevels.RequiredReputationLevel(DeploymentThreatLevel.Maximum)}"
            : $"DETECTION +{detectionPercent}%  AIM +{accuracyPercent}%  FASTER QRF  //  ELEVATED NEEDS REP L{ThreatLevels.RequiredReputationLevel(DeploymentThreatLevel.Elevated)}  MAXIMUM NEEDS L{ThreatLevels.RequiredReputationLevel(DeploymentThreatLevel.Maximum)}";
    }

    private void ClearDeploymentError() => _deploymentError = string.Empty;

    private string MatchingPresetId()
    {
        foreach (var preset in DeploymentCatalog.Presets)
        {
            if (preset.WeaponId == _selectedWeaponId
                && preset.ArmorId == _selectedArmorId
                && preset.AmmoGrade == _selectedAmmoGrade
                && preset.AmmoQuantity == _selectedAmmoQuantity)
            {
                return preset.Id;
            }
        }
        return string.Empty;
    }

    private void RefreshDeploymentStore()
    {
        if (!IsInstanceValid(_deploymentCreditsLabel))
        {
            return;
        }
        var selected = DeploymentCatalog.Resolve(SelectedDeploymentLoadout);
        var armorOffer = DeploymentCatalog.ArmorKit(_selectedArmorId);
        var chinese = GameLocalization.IsChinese(_language);
        var projectedBalance = _displayedProfile.Credits - selected.TotalCost;
        _deploymentCreditsLabel.Text = chinese
            ? $"\u4f59\u989d  {_displayedProfile.Credits}"
            : $"BALANCE  {_displayedProfile.Credits}";
        _deploymentExtractedLabel.Text = chinese
            ? $"\u5386\u53f2\u64a4\u79bb  {_displayedProfile.LifetimeExtractedValue}"
            : $"EXTRACTED  {_displayedProfile.LifetimeExtractedValue}";
        _deploymentRankLabel.Text = DeploymentRankText(chinese);
        RefreshDeploymentThreat();
        if (IsInstanceValid(_deploymentTimeButton))
        {
            var style = TimeOfDayStyles.Style(_selectedTimeOfDay);
            var detectionPercent = Mathf.RoundToInt((style.DetectionMultiplier - 1.0f) * 100.0f);
            _deploymentTimeButton.Text = chinese
                ? $"TIME  {TimeOfDayStyles.DisplayName(_selectedTimeOfDay, _language)}  //  \u4fa6\u6d4b {detectionPercent:+#;-#;0}%"
                : $"TIME  {TimeOfDayStyles.DisplayName(_selectedTimeOfDay, _language)}  //  DETECTION {detectionPercent:+#;-#;0}%";
            _deploymentTimeButton.TooltipText = chinese
                ? "\u540c\u4e00\u5f20\u5730\u56fe\u7684\u56db\u79cd\u5149\u7167\u4e0e\u4fa6\u6d4b\u73af\u5883  //  \u591c\u6218\u9700\u8981\u624b\u7535\u7b52"
                : "FOUR LIGHTING AND DETECTION MOODS FOR THE SAME MAP  //  NIGHT FAVORS THE FLASHLIGHT";
        }
        _deploymentCostLabel.Text = chinese
            ? $"\u6574\u5907\u4ef7\u503c  {selected.TotalCost}"
            : $"KIT VALUE  {selected.TotalCost}";
        _deploymentCostLabel.AddThemeColorOverride(
            "font_color",
            projectedBalance >= 0 ? new Color(0.72f, 0.94f, 0.84f) : new Color(1.0f, 0.34f, 0.22f));
        _deploymentStatusLabel.Text = string.IsNullOrEmpty(_deploymentError)
            ? chinese
                ? $"\u51fa\u51fb\u540e\u4f59\u989d  {Mathf.Max(0, projectedBalance)}  //  {RiskLabel(selected.TotalCost, true)}"
                : $"BALANCE AFTER DEPLOYMENT  {Mathf.Max(0, projectedBalance)}  //  {RiskLabel(selected.TotalCost, false)}"
            : _deploymentError;
        _deploymentStatusLabel.AddThemeColorOverride(
            "font_color",
            string.IsNullOrEmpty(_deploymentError) ? new Color(0.5f, 0.68f, 0.63f) : new Color(1.0f, 0.42f, 0.25f));

        var matchingPreset = MatchingPresetId();
        foreach (var preset in DeploymentCatalog.Presets)
        {
            var button = _deploymentPresetButtons[preset.Id];
            var presetLocked = DeploymentRankLevel < OperatorReputation.RequiredLevelForPreset(preset);
            button.SetPressedNoSignal(preset.Id == matchingPreset);
            button.Disabled = presetLocked;
            button.Text = presetLocked
                ? $"{Text(preset.LocalizationKey, preset.EnglishName)}  //  L{OperatorReputation.RequiredLevelForPreset(preset)}"
                : Text(preset.LocalizationKey, preset.EnglishName);
        }
        var selectedMap = DeploymentMapCatalog.Resolve(_selectedDeploymentMapId);
        foreach (var map in DeploymentMapCatalog.Maps)
        {
            var button = _deploymentMapButtons[map.Id];
            var mapLocked = !IsMapSelectable(map.Id);
            button.SetPressedNoSignal(map.Id == selectedMap.Id);
            button.Disabled = mapLocked;
            button.Text = DeploymentMapButtonText(map);
            var lockHint = !map.Available
                ? string.Empty
                : $"REQUIRES REP L{OperatorReputation.RequiredLevelForMap(map.Id)}\n";
            button.TooltipText = $"{map.Code}  //  {Text(map.LocalizationKey, map.EnglishName)}\n{lockHint}{Text(map.SubtitleLocalizationKey, map.EnglishSubtitle)}";
        }
        _deploymentMapStatusLabel.Text = $"{selectedMap.Code}  //  {Text(selectedMap.LocalizationKey, selectedMap.EnglishName)}  //  {Text(selectedMap.SubtitleLocalizationKey, selectedMap.EnglishSubtitle)}";
        if (IsInstanceValid(_squadLobbySubtitle))
        {
            _squadLobbySubtitle.Text = GameLocalization.IsChinese(_language)
                ? $"\u7a81\u51fb\u5c0f\u961f\u6574\u5907  //  {Text(selectedMap.LocalizationKey, selectedMap.EnglishName)}"
                : $"STRIKE TEAM PREPARATION  //  {Text(selectedMap.LocalizationKey, selectedMap.EnglishName)}";
        }
        foreach (var offer in DeploymentCatalog.Weapons)
        {
            var selectedOffer = string.Equals(offer.Id, _selectedWeaponId, StringComparison.OrdinalIgnoreCase);
            var weaponLocked = IsDeploymentWeaponLocked(offer.Id);
            _deploymentWeaponButtons[offer.Id].Disabled = weaponLocked;
            _deploymentWeaponButtons[offer.Id].SetPressedNoSignal(selectedOffer);
            _deploymentWeaponNames[offer.Id].Text = Text(offer.LocalizationKey, offer.EnglishName);
            _deploymentWeaponNames[offer.Id].AddThemeColorOverride(
                "font_color",
                selectedOffer ? new Color(0.9f, 1.0f, 0.95f) : new Color(0.62f, 0.75f, 0.7f));
            _deploymentWeaponDetails[offer.Id].Text = WeaponOfferDetail(offer, chinese, weaponLocked);
            _deploymentWeaponButtons[offer.Id].TooltipText = $"{Text(offer.LocalizationKey, offer.EnglishName)}\n{_deploymentWeaponDetails[offer.Id].Text}";
        }
        foreach (var offer in DeploymentCatalog.Armor)
        {
            var selectedOffer = string.Equals(offer.Id, _selectedArmorId, StringComparison.OrdinalIgnoreCase);
            var armorLocked = DeploymentRankLevel < OperatorReputation.RequiredLevelForArmor(offer.Id);
            _deploymentArmorButtons[offer.Id].Disabled = armorLocked;
            _deploymentArmorButtons[offer.Id].SetPressedNoSignal(selectedOffer);
            _deploymentArmorNames[offer.Id].Text = Text(offer.LocalizationKey, offer.EnglishName);
            _deploymentArmorNames[offer.Id].AddThemeColorOverride(
                "font_color",
                selectedOffer ? new Color(0.9f, 1.0f, 0.95f) : new Color(0.62f, 0.75f, 0.7f));
            _deploymentArmorDetails[offer.Id].Text = ArmorOfferDetail(offer, chinese, armorLocked);
            _deploymentArmorButtons[offer.Id].TooltipText = $"{Text(offer.LocalizationKey, offer.EnglishName)}\n{_deploymentArmorDetails[offer.Id].Text}";
        }
        var selectedWeaponOffer = DeploymentCatalog.Weapon(_selectedWeaponId);
        foreach (var grade in Enum.GetValues<LootGrade>())
        {
            var button = _deploymentAmmoButtons[grade];
            var gradeLocked = IsDeploymentAmmoGradeLocked(grade);
            button.SetPressedNoSignal(grade == _selectedAmmoGrade);
            button.Disabled = gradeLocked;
            var price = selected.Weapon is null
                ? 0
                : DeploymentCatalog.AmmoCost(selectedWeaponOffer, grade, selected.ReserveAmmo);
            button.Text = gradeLocked
                ? $"T{(int)grade + 1}\nL{OperatorReputation.RequiredLevelForAmmoGrade(grade)}"
                : selected.Weapon is null
                    ? $"T{(int)grade + 1}\n--"
                    : $"T{(int)grade + 1}\n{price}";
            button.TooltipText = AmmoTiers.DisplayName(grade, _language);
        }
        foreach (var pack in DeploymentCatalog.AmmoPacks)
        {
            var button = _deploymentAmmoQuantityButtons[pack.Quantity];
            button.SetPressedNoSignal(pack.Quantity == _selectedAmmoQuantity);
            button.Disabled = selected.Weapon is null;
            var price = selected.Weapon is null
                ? 0
                : DeploymentCatalog.AmmoCost(selectedWeaponOffer, _selectedAmmoGrade, pack.Quantity);
            var label = Text(pack.LocalizationKey, pack.EnglishName);
            button.Text = selected.Weapon is null ? $"{label}\n--" : $"{label}\n{price}";
            button.TooltipText = selected.Weapon is null
                ? Text("ammo_requires_weapon", "SELECT A PRIMARY WEAPON FIRST")
                : $"{label}  //  {price} CREDITS";
        }
        RefreshDeploymentPreview(selected, armorOffer, chinese);
        RefreshSquadDeployAction();
    }

    private void RefreshDeploymentPreview(DeploymentLoadout loadout, DeploymentArmorOffer armorOffer, bool chinese)
    {
        var role = OperatorRoles.Spec(_selectedRole);
        _deploymentOperatorName.Text = $"{OperatorRoles.RoleName(_selectedRole, _language)}  //  {Text("deployment_strike_team", "STRIKE TEAM")}";
        _deploymentOperatorName.AddThemeColorOverride("font_color", role.Accent.Lightened(0.2f));
        _deploymentOperatorSkill.Text = $"{OperatorRoles.SkillName(_selectedRole, _language)}  //  {OperatorRoles.Description(_selectedRole, _language)}";
        _deploymentOperatorSkill.AddThemeColorOverride("font_color", role.Accent);
        _deploymentOperatorSkill.ClipText = true;

        var helmet = EquipmentCatalog.Create(loadout.HelmetId);
        var bodyArmor = EquipmentCatalog.Create(loadout.BodyArmorId);
        var backpack = EquipmentCatalog.Create(loadout.BackpackId);
        _deploymentOperatorPreview.Configure(
            InventoryPreviewKind.Operator,
            weapon: loadout.Weapon,
            role: _selectedRole,
            helmet: helmet,
            bodyArmor: bodyArmor,
            backpack: backpack);

        var primaryName = loadout.Weapon?.DisplayName(_language) ?? Text("loadout_knife_only", "TACTICAL KNIFE");
        _deploymentGearReadout.Text = chinese
            ? $"\u9632\u62a4\u914d\u7f6e\n{helmet.DisplayName(_language)}\n{bodyArmor.DisplayName(_language)}\n{backpack.DisplayName(_language)}"
            : $"PROTECTION\n{helmet.DisplayName(_language)}\n{bodyArmor.DisplayName(_language)}\n{backpack.DisplayName(_language)}";
        if (loadout.Weapon is null)
        {
            _deploymentCombatReadout.Text = chinese
                ? $"\u6218\u6597\u914d\u7f6e\n{primaryName}\n\u65e0\u5907\u7528\u5f39\u836f\n\u8f7b\u88c5\u641c\u7d22"
                : $"COMBAT LOADOUT\n{primaryName}\nNO RESERVE AMMO\nLIGHT SCAVENGE";
        }
        else
        {
            var stats = loadout.Weapon.Stats();
            var caliber = WeaponCatalog.AmmoDisplayName(WeaponCatalog.Weapon(loadout.Weapon.Platform).Caliber, _language);
            _deploymentCombatReadout.Text = chinese
                ? $"\u6218\u6597\u914d\u7f6e\n{primaryName}\n{caliber}\n{stats.Damage:0} \u4f24\u5bb3 / {loadout.ReserveAmmo} \u5907\u5f39"
                : $"COMBAT LOADOUT\n{primaryName}\n{caliber}\n{stats.Damage:0} DMG / {loadout.ReserveAmmo} RESERVE";
        }

        var firepower = loadout.Weapon is null
            ? 12.0f
            : Mathf.Clamp(loadout.Weapon.Stats().Damage * 0.9f + 2.8f / loadout.Weapon.Stats().FireInterval, 0.0f, 100.0f);
        var protection = armorOffer.Id switch
        {
            "heavy" => 88.0f,
            "patrol" => 32.0f,
            _ => 57.0f
        };
        var mobility = armorOffer.Id switch
        {
            "heavy" => 56.0f,
            "patrol" => 92.0f,
            _ => 83.0f
        };
        mobility += loadout.Weapon?.Platform switch
        {
            WeaponPlatform.M24 => -10.0f,
            WeaponPlatform.MP5A5 => 7.0f,
            WeaponPlatform.M3A1 => 10.0f,
            null => 10.0f,
            _ => 0.0f
        };
        SetDeploymentMetric(0, chinese ? "\u706b\u529b" : "FIREPOWER", firepower);
        SetDeploymentMetric(1, chinese ? "\u9632\u62a4" : "PROTECTION", protection);
        SetDeploymentMetric(2, chinese ? "\u673a\u52a8" : "MOBILITY", Mathf.Clamp(mobility, 0.0f, 100.0f));
    }

    private void SetDeploymentMetric(int index, string name, float value)
    {
        _deploymentMetricLabels[index].Text = $"{name}  {Mathf.RoundToInt(value)}";
        _deploymentMetricBars[index].Value = value;
    }

    private string WeaponOfferDetail(DeploymentWeaponOffer offer, bool chinese, bool locked)
    {
        if (offer.Platform is null)
        {
            return chinese ? "\u96f6\u6210\u672c\n\u8fd1\u6218\u641c\u7d22" : "ZERO COST\nMELEE SCAVENGE";
        }
        var build = WeaponCatalog.Build(offer.Platform.Value, offer.BuildTier);
        var stats = build.Stats();
        if (locked)
        {
            var required = OperatorReputation.RequiredLevelForWeapon(offer.Id);
            return chinese
                ? $"{stats.Damage:0}\u4f24\u5bb3  {stats.MagazineSize}\u53d1\n\u58f0\u671b\u9501\u5b9a  //  \u9700 L{required}"
                : $"{stats.Damage:0} DMG  {stats.MagazineSize} RD\nREP LOCKED  //  NEEDS L{required}";
        }
        var ammoPrice = DeploymentCatalog.AmmoCost(offer, _selectedAmmoGrade, _selectedAmmoQuantity);
        return chinese
            ? $"{stats.Damage:0}\u4f24\u5bb3  {stats.MagazineSize}\u53d1\n\u67aa {offer.Price}  \u5f39\u836f {ammoPrice}"
            : $"{stats.Damage:0} DMG  {stats.MagazineSize} RD\nGUN {offer.Price}  AMMO {ammoPrice}";
    }

    private string DeploymentRankText(bool chinese)
    {
        var points = _displayedProfile.ReputationPoints;
        var level = OperatorReputation.LevelForPoints(points);
        if (level >= OperatorReputation.MaxLevel)
        {
            return chinese ? $"\u58f0\u671b L{level}  //  \u5df2\u6ee1\u7ea7" : $"REP L{level}  //  MAX";
        }
        var start = OperatorReputation.PointsForLevel(level);
        var end = OperatorReputation.PointsForLevel(level + 1);
        return chinese
            ? $"\u58f0\u671b L{level}  //  {points - start}/{end - start} \u5347\u7ea7"
            : $"REP L{level}  //  {points - start}/{end - start} TO L{level + 1}";
    }

    private string DeploymentMapButtonText(DeploymentMapOffer map)
    {
        var code = map.Code.Replace("MAP ", string.Empty, StringComparison.Ordinal);
        if (map.Available && IsMapSelectable(map.Id))
        {
            return $"{code}\n{Text(map.LocalizationKey, map.EnglishName)}";
        }
        if (map.Available)
        {
            return $"{code}\nL{OperatorReputation.RequiredLevelForMap(map.Id)}";
        }
        return $"{code}\n{Text("map_locked_short", "LOCKED")}";
    }

    private string ArmorOfferDetail(DeploymentArmorOffer offer, bool chinese, bool locked)
    {
        var armor = EquipmentCatalog.Create(offer.BodyArmorId).Definition;
        var pack = EquipmentCatalog.Create(offer.BackpackId).Definition;
        if (locked)
        {
            var required = OperatorReputation.RequiredLevelForArmor(offer.Id);
            return chinese
                ? $"{armor.Protection * 100:0}% \u9632\u62a4\n\u58f0\u671b\u9501\u5b9a  //  \u9700 L{required}"
                : $"{armor.Protection * 100:0}% ARMOR\nREP LOCKED  //  NEEDS L{required}";
        }
        return chinese
            ? $"{armor.Protection * 100:0}% \u9632\u62a4  +{pack.CapacityBonus} \u5bb9\u91cf  //  {offer.Price}"
            : $"{armor.Protection * 100:0}% ARMOR  +{pack.CapacityBonus} CAP  //  {offer.Price}";
    }

    private string RiskLabel(int cost, bool chinese)
    {
        if (cost <= 1000)
        {
            return chinese ? "\u4f4e\u98ce\u9669" : "LOW RISK";
        }
        if (cost <= 8000)
        {
            return chinese ? "\u4e2d\u7b49\u98ce\u9669" : "MEDIUM RISK";
        }
        return chinese ? "\u9ad8\u98ce\u9669" : "HIGH RISK";
    }

    private void RefreshDeploymentLanguage()
    {
        if (!IsInstanceValid(_deploymentWeaponCaption))
        {
            return;
        }
        var chinese = GameLocalization.IsChinese(_language);
        _deploymentPresetCaption.Text = chinese ? "\u5feb\u901f\u6574\u5907" : "QUICK KITS";
        _deploymentWeaponCaption.Text = chinese ? "\u4e3b\u6b66\u5668\u5e02\u573a" : "PRIMARY MARKET";
        _deploymentArmorCaption.Text = chinese ? "\u9632\u62a4\u914d\u7f6e" : "PROTECTION";
        _deploymentAmmoCaption.Text = chinese ? "\u5f39\u836f\u7b49\u7ea7  //  \u4ef7\u683c" : "AMMUNITION GRADE  //  PRICE";
        _deploymentAmmoQuantityCaption.Text = chinese ? "\u5f39\u836f\u6570\u91cf  //  \u4ef7\u683c" : "AMMO COUNT  //  PRICE";
        _deploymentMapCaption.Text = chinese ? "\u51fa\u51fb\u5730\u56fe" : "DEPLOYMENT MAP";
        RefreshDeploymentStore();
    }
}
