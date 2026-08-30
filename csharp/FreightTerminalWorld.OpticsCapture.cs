using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private async Task PrepareOpticCaptureScene()
    {
        foreach (var enemy in _enemies)
        {
            enemy.ProcessMode = ProcessModeEnum.Disabled;
        }
        foreach (var mate in _squadMates.Where(IsInstanceValid))
        {
            mate.ProcessMode = ProcessModeEnum.Disabled;
            mate.GlobalPosition = new Vector3(
                240.0f + mate.SquadSlot * 3.0f,
                80.0f,
                240.0f);
        }
        _missionDirector.ExitDeploymentZone();
        _player.UiLocked = false;
        _player.GlobalPosition = new Vector3(0.0f, 0.2f, 40.0f);
        _player.Velocity = Vector3.Zero;
        _player.FaceWorldPointForDiagnostics(new Vector3(0.0f, 0.2f, -80.0f));
        Input.ActionRelease("aim");
        await WaitOpticPhysicsFrames(12);
    }

    private async Task WaitOpticPhysicsFrames(int count)
    {
        for (var frame = 0; frame < count; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
    }

    private async void CaptureAdsFrame()
    {
        await PrepareOpticCaptureScene();
        _player.GrantFireablePrimaryForDiagnostics();
        await WaitOpticPhysicsFrames(30);
        Input.ActionPress("aim");
        await WaitOpticPhysicsFrames(90);
        await WaitFrames(2);
        SaveViewportImage("res://ads_validation.png");
        GD.Print(
            $"ADS_CHECK aiming={_player.IsAiming} ammo={_player.Ammo} "
            + $"offset={_player.OpticScreenOffsetForDiagnostics()} phase={_missionPhase}");
        Input.ActionRelease("aim");
        GetTree().Quit();
    }

    private async void CaptureOpticsFrames(bool narrow = false, bool ultrawide = false)
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
        await PrepareOpticCaptureScene();
        var optics = new[]
        {
            (Id: "optic_micro", File: "m4a1_optic_micro_validation.png", Platform: WeaponPlatform.M4A1),
            (Id: "optic_holo", File: "m4a1_optic_holo_validation.png", Platform: WeaponPlatform.M4A1),
            (Id: "optic_scope", File: "m4a1_optic_scope_validation.png", Platform: WeaponPlatform.M4A1),
            (Id: "optic_micro", File: "ak74_optic_micro_validation.png", Platform: WeaponPlatform.AK74),
            (Id: "optic_holo", File: "ak74_optic_holo_validation.png", Platform: WeaponPlatform.AK74),
            (Id: "optic_scope", File: "ak74_optic_scope_validation.png", Platform: WeaponPlatform.AK74),
            (Id: "optic_micro", File: "scarl_optic_micro_validation.png", Platform: WeaponPlatform.ScarL),
            (Id: "optic_holo", File: "scarl_optic_holo_validation.png", Platform: WeaponPlatform.ScarL),
            (Id: "optic_scope", File: "scarl_optic_scope_validation.png", Platform: WeaponPlatform.ScarL),
            (Id: "optic_micro", File: "mp5a5_optic_micro_validation.png", Platform: WeaponPlatform.MP5A5),
            (Id: "optic_holo", File: "mp5a5_optic_holo_validation.png", Platform: WeaponPlatform.MP5A5),
            (Id: "optic_micro", File: "m3a1_optic_micro_validation.png", Platform: WeaponPlatform.M3A1),
            (Id: "optic_holo", File: "m3a1_optic_holo_validation.png", Platform: WeaponPlatform.M3A1),
            (Id: "optic_micro", File: "p226_optic_micro_validation.png", Platform: WeaponPlatform.P226),
            (Id: "optic_holo", File: "p226_optic_holo_validation.png", Platform: WeaponPlatform.P226),
            (Id: "optic_micro", File: "m1911_optic_micro_validation.png", Platform: WeaponPlatform.M1911),
            (Id: "optic_holo", File: "m1911_optic_holo_validation.png", Platform: WeaponPlatform.M1911),
            (Id: "optic_micro", File: "gsh18_optic_micro_validation.png", Platform: WeaponPlatform.GSh18),
            (Id: "optic_holo", File: "gsh18_optic_holo_validation.png", Platform: WeaponPlatform.GSh18),
            (Id: "optic_micro", File: "desert_eagle_optic_micro_validation.png", Platform: WeaponPlatform.DesertEagle),
            (Id: "optic_holo", File: "desert_eagle_optic_holo_validation.png", Platform: WeaponPlatform.DesertEagle),
            (Id: "optic_scope", File: "vss_optic_scope_validation.png", Platform: WeaponPlatform.VSS),
            (Id: "optic_7x", File: "optic_7x_validation.png", Platform: WeaponPlatform.AXMC),
            (Id: "optic_7x", File: "awm_optic_7x_validation.png", Platform: WeaponPlatform.AWM),
            (Id: "optic_sniper", File: "optic_sniper_validation.png", Platform: WeaponPlatform.M24)
        };
        var layoutSuffix = ultrawide
            ? "_ultrawide"
            : narrow
                ? "_narrow"
                : string.Empty;
        foreach (var optic in optics)
        {
            var build = WeaponCatalog.Build(optic.Platform, 3);
            build.Attachments[AttachmentSlot.Optic] = optic.Id;
            _player.GrantFireablePrimaryForDiagnostics(build);
            await WaitOpticPhysicsFrames(30);
            await WaitFrames(2);
            var captureFile = optic.File.Replace(
                "_validation",
                $"{layoutSuffix}_validation");
            SaveViewportImage("res://" + captureFile);
            Input.ActionPress("aim");
            await WaitOpticPhysicsFrames(90);
            await WaitFrames(2);
            SaveViewportImage(
                "res://" + captureFile.Replace("_validation", "_ads_validation"));
            var aiming = _player.IsAiming;
            var inspection = _player.InspectOpticClearanceForDiagnostics();
            var offset = _player.OpticScreenOffsetForDiagnostics();
            Input.ActionRelease("aim");
            await WaitOpticPhysicsFrames(24);
            GD.Print(
                $"OPTIC_CHECK id={optic.Id} platform={optic.Platform} "
                + $"visible={inspection.OpticVisible} aiming={aiming} "
                + $"authored={inspection.AuthoredPresentationValid} "
                + $"integrated={inspection.IntegratedOptic} offset={offset.Length():F3} "
                + $"handling={_player.CurrentWeaponStats.Handling:0.00}");
        }
        GetTree().Quit();
    }
}
