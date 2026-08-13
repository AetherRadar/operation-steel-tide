using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private void ConfigureTacticalMinimap()
    {
        var landmarks = new List<TacticalMapLandmark>
        {
            new(DeploymentPoint, "minimap_deploy", "DEPLOY", new Color(0.36f, 0.82f, 1.0f)),
            new(ExtractionPoint, "minimap_extract", "EXTRACT", new Color(0.32f, 0.95f, 0.66f)),
            new(new Vector3(35.5f, 0, -10.0f), "minimap_relay", "RELAY", new Color(1.0f, 0.5f, 0.2f)),
            new(new Vector3(-31.0f, 0, -7.0f), "minimap_manifest", "MANIFEST", new Color(0.98f, 0.72f, 0.24f)),
            new(new Vector3(23.5f, 0, -5.0f), "minimap_warehouse", "WAREHOUSE", new Color(0.66f, 0.72f, 0.7f)),
            new(new Vector3(35.0f, 0, 33.0f), "minimap_radar", "RADAR", new Color(0.82f, 0.45f, 1.0f)),
            new(new Vector3(-62.0f, 0, -116.0f), "minimap_residential", "RESIDENTIAL", new Color(0.42f, 0.72f, 1.0f)),
            new(new Vector3(99.0f, 0, -114.0f), "minimap_command", "COMMAND", new Color(1.0f, 0.34f, 0.3f)),
            new(new Vector3(-76.0f, 0, 4.0f), "minimap_bazaar", "SALVAGE MARKET", new Color(0.96f, 0.64f, 0.2f)),
            new(new Vector3(113.0f, 0, 9.0f), "minimap_hydro", "TIDEGLASS", new Color(0.35f, 0.9f, 0.62f)),
            new(new Vector3(-114.0f, 0, 43.0f), "minimap_observatory", "TIDE OBSERVATORY", new Color(0.44f, 0.74f, 1.0f)),
            new(new Vector3(77.0f, 0, -151.0f), "minimap_drydock", "DRYDOCK", new Color(0.95f, 0.44f, 0.24f))
        };
        _hud.ConfigureMinimap(new Rect2(-170.0f, -220.0f, MapWidthMeters, MapDepthMeters), landmarks);
        _hud.SetMinimapPlayer(_player.GlobalPosition, 0.0f);
    }

    private void ConfigureDemolitionMinimap()
    {
        if (_demolitionArena is null)
        {
            return;
        }
        var landmarks = new List<TacticalMapLandmark>();
        foreach (var marker in _demolitionArena.Layout.Markers)
        {
            landmarks.Add(new TacticalMapLandmark(
                marker.Position,
                marker.LocalizationKey,
                marker.EnglishName,
                marker.Accent));
        }
        _hud.ConfigureMinimap(_demolitionArena.Layout.WorldBounds, landmarks);
        _hud.SetMinimapWorldBoss(Vector3.Zero, false);
        _hud.SetMinimapPlayer(_player.GlobalPosition, 0.0f);
    }

    private async void ValidateTacticalHud()
    {
        foreach (var enemy in _enemies)
        {
            if (IsInstanceValid(enemy))
            {
                enemy.ProcessMode = ProcessModeEnum.Disabled;
            }
        }
        foreach (var mate in _squadMates)
        {
            if (IsInstanceValid(mate))
            {
                mate.ProcessMode = ProcessModeEnum.Disabled;
            }
        }
        await WaitFrames(2);

        _player.GlobalPosition = new Vector3(-18.0f, 44.0f, -82.0f);
        _hud.SetMinimapPlayer(_player.GlobalPosition, 42.0f);
        _player.SetAmmoGradeForDiagnostics(LootGrade.Epic, 90);
        var minimapReady = _hud.MinimapLandmarkCount >= 8
            && _hud.MinimapPlayerPosition.X > 0.0f
            && _hud.MinimapPlayerPosition.Y > 0.0f;
        var ammoTiersFunctional = _player.CurrentAmmoGrade == LootGrade.Epic
            && AmmoTiers.DamageMultiplier(LootGrade.Legendary) > AmmoTiers.DamageMultiplier(LootGrade.Common)
            && AmmoTiers.ArmorPenetration(LootGrade.Legendary) > AmmoTiers.ArmorPenetration(LootGrade.Common);

        var knockTarget = _enemies.Find(enemy => IsInstanceValid(enemy) && !enemy.IsDead);
        var knockdownVisible = false;
        if (knockTarget is not null)
        {
            var expectedName = knockTarget.OperatorCallsign(_languageSetting);
            knockTarget.TakeDamage(999.0f, knockTarget.GlobalPosition + Vector3.Up, _player, 0.5f);
            knockdownVisible = _hud.LastKnockdownName == expectedName;
        }

        var valid = minimapReady && ammoTiersFunctional && knockdownVisible;
        GD.Print($"TACTICAL_HUD_CHECK valid={valid} minimap={minimapReady} landmarks={_hud.MinimapLandmarkCount} ammo_tiers={ammoTiersFunctional} loaded_grade={_player.CurrentAmmoGrade} knockdown={knockdownVisible}");
        GD.Print($"TACTICAL_HUD_PASS valid={valid}");
        await WaitFrames(180);
        GetTree().Quit(valid ? 0 : 2);
    }

    private async void CaptureTacticalHud()
    {
        foreach (var enemy in _enemies)
        {
            if (IsInstanceValid(enemy))
            {
                enemy.ProcessMode = ProcessModeEnum.Disabled;
            }
        }
        foreach (var mate in _squadMates)
        {
            if (IsInstanceValid(mate))
            {
                mate.ProcessMode = ProcessModeEnum.Disabled;
            }
        }
        _hud.SetLanguage("zh");
        _player.GlobalPosition = new Vector3(-18.0f, 0.2f, -82.0f);
        _hud.SetMinimapPlayer(_player.GlobalPosition, 42.0f);
        _hud.SetAmmoTier(LootGrade.Epic);
        _hud.ShowKnockdown("WOLF-2", "\u4f60");
        await WaitFrames(8);
        SaveViewportImage("res://tactical_hud_validation.png");
        GD.Print("TACTICAL_HUD_CAPTURE path=tactical_hud_validation.png");
        GetTree().Quit();
    }
}
