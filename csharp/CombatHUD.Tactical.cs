using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public partial class CombatHUD
{
    private TacticalMinimap _minimap = null!;
    private VBoxContainer _combatFeed = null!;
    private Label _ammoTierLabel = null!;
    private LootGrade _displayedAmmoGrade = LootGrade.Common;

    public string LastKnockdownName { get; private set; } = string.Empty;
    public int MinimapLandmarkCount => IsInstanceValid(_minimap) ? _minimap.LandmarkCount : 0;
    public Vector2 MinimapPlayerPosition => IsInstanceValid(_minimap) ? _minimap.PlayerMapPosition : Vector2.Zero;

    private void BuildTacticalHud(Control root)
    {
        _minimap = new TacticalMinimap
        {
            Position = new Vector2(28, 72),
            Size = new Vector2(250, 202)
        };
        root.AddChild(_minimap);

        _combatFeed = new VBoxContainer
        {
            Size = new Vector2(340, 180),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _combatFeed.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        _combatFeed.Position = new Vector2(-370, 74);
        _combatFeed.AddThemeConstantOverride("separation", 6);
        root.AddChild(_combatFeed);
    }

    private void BuildAmmoTierHud(Control weaponPanel)
    {
        _ammoTierLabel = Label("T1", 12, AmmoTiers.Color(LootGrade.Common));
        _ammoTierLabel.Position = new Vector2(276, 65);
        _ammoTierLabel.Size = new Vector2(50, 20);
        _ammoTierLabel.HorizontalAlignment = HorizontalAlignment.Right;
        _ammoTierLabel.TooltipText = Text("ammo_tier_tooltip", "LOADED AMMUNITION TIER");
        weaponPanel.AddChild(_ammoTierLabel);
    }

    public void ConfigureMinimap(Rect2 worldBounds, IReadOnlyList<TacticalMapLandmark> landmarks)
    {
        _minimap.Configure(worldBounds, landmarks);
    }

    public void SetMinimapPlayer(Vector3 position, float headingDegrees)
    {
        _minimap.SetPlayerState(position, headingDegrees);
    }

    public void SetAmmoTier(LootGrade grade)
    {
        _displayedAmmoGrade = grade;
        if (!IsInstanceValid(_ammoTierLabel))
        {
            return;
        }
        _ammoTierLabel.Text = $"T{(int)grade + 1}";
        _ammoTierLabel.AddThemeColorOverride("font_color", AmmoTiers.Color(grade));
        _ammoTierLabel.TooltipText = $"{Text("ammo_tier_tooltip", "LOADED AMMUNITION TIER")}  //  {AmmoTiers.DisplayName(grade, _language)}";
    }

    public void ShowKnockdown(string operatorName, string sourceName = "YOU")
    {
        LastKnockdownName = operatorName;
        var entry = new ColorRect
        {
            CustomMinimumSize = new Vector2(340, 44),
            Color = new Color(0.035f, 0.055f, 0.052f, 0.94f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        entry.AddChild(new ColorRect
        {
            Size = new Vector2(3, 44),
            Color = new Color(1.0f, 0.42f, 0.2f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        });
        var label = Label(string.Empty, 13, new Color(0.94f, 0.97f, 0.94f));
        label.Position = new Vector2(14, 4);
        label.Size = new Vector2(314, 34);
        label.VerticalAlignment = VerticalAlignment.Center;
        label.ClipText = true;
        label.Text = GameLocalization.IsChinese(_language)
            ? $"{sourceName}  //  \u51fb\u5012  {operatorName}"
            : $"{sourceName}  //  KNOCKED  {operatorName}";
        entry.AddChild(label);
        _combatFeed.AddChild(entry);
        if (_combatFeed.GetChildCount() > 4)
        {
            _combatFeed.GetChild(0).QueueFree();
        }

        entry.Modulate = new Color(1, 1, 1, 0);
        var tween = CreateTween();
        tween.TweenProperty(entry, "modulate:a", 1.0f, 0.12f);
        tween.TweenInterval(3.4f);
        tween.TweenProperty(entry, "modulate:a", 0.0f, 0.48f);
        tween.TweenCallback(Callable.From(entry.QueueFree));
    }

    private void RefreshTacticalLanguage()
    {
        if (IsInstanceValid(_minimap))
        {
            _minimap.SetLanguage(_language);
        }
        SetAmmoTier(_displayedAmmoGrade);
    }
}
