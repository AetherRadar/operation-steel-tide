using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private async void CaptureReloadPoseKeyframes(WeaponPlatform platform)
    {
        SetCaptureLanguage("en");
        var window = GetWindow();
        window.ContentScaleMode = Window.ContentScaleModeEnum.CanvasItems;
        window.ContentScaleAspect = Window.ContentScaleAspectEnum.Ignore;
        window.Size = new Vector2I(1280, 720);
        _player.GlobalPosition = new Vector3(0.0f, 0.2f, 40.0f);
        _player.Velocity = Vector3.Zero;
        _player.FaceWorldPointForDiagnostics(new Vector3(0.0f, 0.2f, -40.0f));
        var weapon = WeaponCatalog.Build(platform, 0);
        _player.GrantFireablePrimaryForDiagnostics(weapon);
        _player.SetAmmoGradeForDiagnostics(
            LootGrade.Common,
            weapon.Stats().MagazineSize * 2);
        _player.SetMagazineAmmoForDiagnostics(0);
        _player.SetAimingPoseForDiagnostics(false);
        await WaitFrames(6);

        var profile = FirstPersonReloadProfileCatalog.For(platform);
        var samples = new[]
        {
            (Name: "extract", Progress: (profile.ReachEnd + profile.ExtractEnd) * 0.5f),
            (Name: "insert", Progress: (profile.InsertEnd + profile.SeatEnd) * 0.5f),
            (Name: "action", Progress: (profile.SeatEnd + profile.ActionEnd) * 0.5f)
        };
        var valid = true;
        foreach (var sample in samples)
        {
            valid &= _player.SetReloadPoseForDiagnostics(
                sample.Progress,
                emptyReload: true);
            await WaitFrames(3);
            var inspection = _player.InspectAllWeaponReloadForDiagnostics();
            GD.Print(
                $"RELOAD_KEYFRAME platform={platform} stage={sample.Name} "
                + $"progress={sample.Progress:F4} "
                + $"palm={inspection.ScreenContact.LeftPalmScreen.X:F1}/"
                + $"{inspection.ScreenContact.LeftPalmScreen.Y:F1} "
                + $"wrist={inspection.ScreenContact.LeftWristScreen.X:F1}/"
                + $"{inspection.ScreenContact.LeftWristScreen.Y:F1} "
                + $"contact_residual={inspection.VisibleSupportPalm.DistanceTo(inspection.SupportTarget):F6}");
            SaveViewportImage(
                $"res://reload_keyframe_{platform.ToString().ToLowerInvariant()}_{sample.Name}.png");
        }
        _player.ClearReloadPoseForDiagnostics();
        GD.Print($"RELOAD_KEYFRAME_CAPTURE valid={valid} platform={platform}");
        GetTree().Quit(valid ? 0 : 2);
    }

    private async void CaptureReloadFrameSequence(
        WeaponPlatform platform,
        int maximumReloadFrames = 360)
    {
        SetCaptureLanguage("en");
        var window = GetWindow();
        window.ContentScaleMode = Window.ContentScaleModeEnum.CanvasItems;
        window.ContentScaleAspect = Window.ContentScaleAspectEnum.Ignore;
        window.Size = new Vector2I(960, 540);

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
                mate.GlobalPosition = new Vector3(240.0f, 80.0f, 240.0f);
            }
        }

        _player.GlobalPosition = new Vector3(0.0f, 0.2f, 40.0f);
        _player.Velocity = Vector3.Zero;
        _player.FaceWorldPointForDiagnostics(new Vector3(0.0f, 0.2f, -40.0f));
        var weapon = WeaponCatalog.Build(platform, 0);
        _player.GrantFireablePrimaryForDiagnostics(weapon);
        _player.SetAmmoGradeForDiagnostics(
            LootGrade.Common,
            weapon.Stats().MagazineSize * 2);
        _player.SetMagazineAmmoForDiagnostics(0);
        _player.SetAimingPoseForDiagnostics(false);
        _player.SetViewPitchForDiagnostics(0.0f);
        await WaitFrames(10);

        Input.ActionPress("reload");
        await WaitFrames(2);
        Input.ActionRelease("reload");

        var startWaitFrames = 0;
        while (!_player.IsReloading && startWaitFrames < 240)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            startWaitFrames++;
        }

        var frame = 0;
        var valid = _player.IsReloading;
        while (_player.IsReloading
            && frame < maximumReloadFrames)
        {
            // A movie capture must compare the arm pose, not incidental mouse
            // input or a late recoil/damage kick. Pin the diagnostic camera on
            // every rendered frame so a camera jump cannot masquerade as a
            // one-frame wrist twitch.
            _player.Velocity = Vector3.Zero;
            _player.FaceWorldPointForDiagnostics(
                new Vector3(0.0f, 0.2f, -40.0f));
            _player.SetViewPitchForDiagnostics(0.0f);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var inspection = _player.InspectAllWeaponReloadForDiagnostics();
            var contact = inspection.ScreenContact;
            var meshBounds = inspection.BodyContinuity.AnimatedMeshScreen.Bounds;
            GD.Print(
                $"RELOAD_FRAME platform={platform} frame={frame:D3} "
                + $"progress={_player.ReloadProgress:F6} "
                + $"palm={contact.LeftPalmScreen.X:F2}/{contact.LeftPalmScreen.Y:F2} "
                + $"wrist={contact.LeftWristScreen.X:F2}/{contact.LeftWristScreen.Y:F2} "
                + $"mesh={meshBounds.Position.X:F2}/{meshBounds.Position.Y:F2}/"
                + $"{meshBounds.Size.X:F2}/{meshBounds.Size.Y:F2} "
                + $"target_residual={inspection.VisibleSupportPalm.DistanceTo(inspection.SupportTarget):F6} "
                + $"primary={inspection.PrimaryMagazineVisible} "
                + $"spare={inspection.SpareMagazineVisible}");
            frame++;
        }

        valid &= frame > 30
            && (!_player.IsReloading || frame == maximumReloadFrames);
        await WaitFrames(4);
        GD.Print(
            $"RELOAD_FRAME_CAPTURE valid={valid} platform={platform} frames={frame}");
        GetTree().Quit(valid ? 0 : 2);
    }
}
