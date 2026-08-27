using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private async void CaptureOpenHandValidation()
    {
        Input.MouseMode = Input.MouseModeEnum.Visible;
        foreach (var enemy in _enemies) if (IsInstanceValid(enemy)) enemy.ProcessMode = ProcessModeEnum.Disabled;
        foreach (var mate in _squadMates) if (IsInstanceValid(mate)) mate.ProcessMode = ProcessModeEnum.Disabled;
        _missionDirector.ExitDeploymentZone();
        _player.GlobalPosition = new Vector3(0, 0.2f, 40.0f);
        _player.Velocity = Vector3.Zero;
        _player.FaceWorldPointForDiagnostics(new Vector3(0, 0.2f, -40.0f));
        await WaitFrames(6);
        var platform = WeaponPlatform.ScarL;
        _player.GrantFireablePrimaryForDiagnostics(WeaponCatalog.Build(platform, 0));
        await WaitFrames(8);
        SaveViewportImage("res://open_hand_scarl_validation.png");
        platform = WeaponPlatform.M4A1;
        _player.GrantFireablePrimaryForDiagnostics(WeaponCatalog.Build(platform, 0));
        await WaitFrames(8);
        SaveViewportImage("res://open_hand_m4a1_validation.png");
        platform = WeaponPlatform.AK74;
        _player.GrantFireablePrimaryForDiagnostics(WeaponCatalog.Build(platform, 0));
        await WaitFrames(8);
        SaveViewportImage("res://open_hand_ak74_validation.png");
        platform = WeaponPlatform.M3A1;
        _player.GrantFireablePrimaryForDiagnostics(WeaponCatalog.Build(platform, 0));
        await WaitFrames(8);
        SaveViewportImage("res://open_hand_m3a1_validation.png");
        _player.SetViewPitchForDiagnostics(-0.68f);
        await WaitFrames(4);
        SaveViewportImage("res://open_hand_m3a1_downward_validation.png");
        _player.SetViewPitchForDiagnostics(0.0f);
        Input.ActionPress("aim");
        await WaitFrames(90);
        SaveViewportImage("res://open_hand_m3a1_ads_validation.png");
        Input.ActionRelease("aim");
        await WaitFrames(12);
        _player.SetViewPitchForDiagnostics(-0.68f);
        foreach (var (progress, path) in new[]
        {
            (0.18f, "res://open_hand_m3a1_reload_early_validation.png"),
            (0.46f, "res://open_hand_m3a1_reload_validation.png"),
            (0.78f, "res://open_hand_m3a1_reload_late_validation.png")
        })
        {
            _player.SetReloadPoseForDiagnostics(progress);
            await WaitFrames(4);
            SaveViewportImage(path);
        }
        _player.ClearReloadPoseForDiagnostics();
        _player.SetViewPitchForDiagnostics(0.0f);
        platform = WeaponPlatform.P226;
        _player.GrantFireablePrimaryForDiagnostics(WeaponCatalog.Build(platform, 0));
        await WaitFrames(8);
        SaveViewportImage("res://open_hand_p226_validation.png");
        platform = WeaponPlatform.DesertEagle;
        _player.GrantFireablePrimaryForDiagnostics(WeaponCatalog.Build(platform, 0));
        await WaitFrames(8);
        SaveViewportImage("res://open_hand_desert_eagle_validation.png");
        GD.Print("OPEN_HAND_CAPTURE done");
        GetTree().Quit(0);
    }
}
