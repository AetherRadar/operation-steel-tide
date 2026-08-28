using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private async void CaptureOpenHandValidation(bool narrow = false, bool ultrawide = false)
    {
        if (narrow || ultrawide)
        {
            var window = GetWindow();
            window.ContentScaleMode = Window.ContentScaleModeEnum.CanvasItems;
            window.ContentScaleAspect = Window.ContentScaleAspectEnum.Ignore;
            window.Size = ultrawide
                ? new Vector2I(2048, 621)
                : new Vector2I(985, 847);
            await WaitFrames(4);
        }
        Input.MouseMode = Input.MouseModeEnum.Visible;
        var suffix = ultrawide
            ? "_ultrawide_validation.png"
            : narrow
                ? "_narrow_validation.png"
                : "_validation.png";
        foreach (var enemy in _enemies) if (IsInstanceValid(enemy)) enemy.ProcessMode = ProcessModeEnum.Disabled;
        foreach (var mate in _squadMates) if (IsInstanceValid(mate)) mate.ProcessMode = ProcessModeEnum.Disabled;
        _missionDirector.ExitDeploymentZone();
        _player.GlobalPosition = new Vector3(0, 0.2f, 40.0f);
        _player.Velocity = Vector3.Zero;
        _player.FaceWorldPointForDiagnostics(new Vector3(0, 0.2f, -40.0f));
        await WaitFrames(6);
        var captures = new (WeaponPlatform Platform, string FileStem)[]
        {
            (WeaponPlatform.M4A1, "m4a1"),
            (WeaponPlatform.AK74, "ak74"),
            (WeaponPlatform.ScarL, "scarl"),
            (WeaponPlatform.MP5A5, "mp5a5"),
            (WeaponPlatform.M3A1, "m3a1"),
            (WeaponPlatform.VSS, "vss"),
            (WeaponPlatform.M24, "m24"),
            (WeaponPlatform.AXMC, "axmc"),
            (WeaponPlatform.AWM, "awm"),
            (WeaponPlatform.P226, "p226"),
            (WeaponPlatform.M1911, "m1911"),
            (WeaponPlatform.GSh18, "gsh18"),
            (WeaponPlatform.DesertEagle, "desert_eagle")
        };
        foreach (var capture in captures)
        {
            _player.GrantFireablePrimaryForDiagnostics(
                WeaponCatalog.Build(capture.Platform, 0));
            await WaitFrames(8);
            SaveViewportImage($"res://open_hand_{capture.FileStem}{suffix}");
        }

        _player.SetViewPitchForDiagnostics(-0.68f);
        var downwardSidearmCaptures = new (WeaponPlatform Platform, string FileStem)[]
        {
            (WeaponPlatform.P226, "p226"),
            (WeaponPlatform.M1911, "m1911"),
            (WeaponPlatform.GSh18, "gsh18"),
            (WeaponPlatform.DesertEagle, "desert_eagle")
        };
        foreach (var capture in downwardSidearmCaptures)
        {
            _player.GrantFireablePrimaryForDiagnostics(
                WeaponCatalog.Build(capture.Platform, 0));
            await WaitFrames(8);
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            await WaitFrames(1);
            SaveViewportImage($"res://open_hand_{capture.FileStem}_downward{suffix}");
        }
        _player.SetViewPitchForDiagnostics(0.0f);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

        _player.GrantFireablePrimaryForDiagnostics(
            WeaponCatalog.Build(WeaponPlatform.M3A1, 0));
        await WaitFrames(8);
        _player.SetViewPitchForDiagnostics(-0.68f);
        await WaitFrames(4);
        SaveViewportImage($"res://open_hand_m3a1_downward{suffix}");
        foreach (var (progress, fileStem) in new[]
        {
            (0.18f, "m3a1_reload_early"),
            (0.46f, "m3a1_reload"),
            (0.78f, "m3a1_reload_late")
        })
        {
            _player.SetReloadPoseForDiagnostics(progress);
            await WaitFrames(4);
            SaveViewportImage($"res://open_hand_{fileStem}{suffix}");
        }
        _player.ClearReloadPoseForDiagnostics();
        _player.SetViewPitchForDiagnostics(0.0f);

        var aimedCaptures = new (WeaponPlatform Platform, string FileStem)[]
        {
            (WeaponPlatform.M3A1, "m3a1"),
            (WeaponPlatform.M4A1, "m4a1"),
            (WeaponPlatform.AK74, "ak74"),
            (WeaponPlatform.AWM, "awm"),
            (WeaponPlatform.P226, "p226"),
            (WeaponPlatform.M1911, "m1911"),
            (WeaponPlatform.GSh18, "gsh18"),
            (WeaponPlatform.DesertEagle, "desert_eagle")
        };
        _player.UiLocked = false;
        Input.MouseMode = Input.MouseModeEnum.Captured;
        Input.ActionRelease("aim");
        foreach (var capture in aimedCaptures)
        {
            _player.GrantFireablePrimaryForDiagnostics(
                WeaponCatalog.Build(capture.Platform, 0));
            await WaitFrames(8);
            Input.ActionPress("aim");
            for (var frame = 0; frame < 90; frame++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            }
            SaveViewportImage($"res://open_hand_{capture.FileStem}_ads{suffix}");
            Input.ActionRelease("aim");
            for (var frame = 0; frame < 12; frame++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            }
        }
        GD.Print("OPEN_HAND_CAPTURE done");
        GetTree().Quit(0);
    }
}
