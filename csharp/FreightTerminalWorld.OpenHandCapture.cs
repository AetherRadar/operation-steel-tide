using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private async void CaptureOpenHandValidation()
    {
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
        GD.Print("OPEN_HAND_CAPTURE done");
        GetTree().Quit(0);
    }
}
