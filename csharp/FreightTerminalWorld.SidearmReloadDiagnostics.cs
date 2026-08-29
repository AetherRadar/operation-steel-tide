using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private async void ValidateSidearmReloadDiagnostics()
    {
        var window = GetWindow();
        window.ContentScaleMode = Window.ContentScaleModeEnum.CanvasItems;
        window.ContentScaleAspect = Window.ContentScaleAspectEnum.Ignore;
        window.Size = new Vector2I(1280, 720);
        _player.GlobalPosition = new Vector3(0.0f, 0.2f, 40.0f);
        _player.Velocity = Vector3.Zero;
        _player.FaceWorldPointForDiagnostics(new Vector3(0.0f, 0.2f, -40.0f));
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
                mate.GlobalPosition = new Vector3(240.0f, 80.0f, 240.0f);
            }
        }
        await WaitFrames(6);

        var reloadKeyBound = ReloadActionUsesPhysicalR();
        var valid = reloadKeyBound;
        var results = new List<string>();
        var platforms = new[]
        {
            WeaponPlatform.P226,
            WeaponPlatform.M1911,
            WeaponPlatform.GSh18,
            WeaponPlatform.DesertEagle
        };

        foreach (var platform in platforms)
        {
            _player.GrantFireablePrimaryForDiagnostics(WeaponCatalog.Build(platform, 0));
            var magazineSize = _player.EquippedWeapon.Stats().MagazineSize;
            _player.SetAmmoGradeForDiagnostics(LootGrade.Common, magazineSize * 2);
            _player.SetMagazineAmmoForDiagnostics(0);
            _player.SetAimingPoseForDiagnostics(false);
            Input.ActionRelease(GameInputActions.Reload);
            await WaitFrames(3);

            var idle = _player.InspectSidearmReloadForDiagnostics();
            Input.ActionPress(GameInputActions.Reload);
            await WaitFrames(2);
            Input.ActionRelease(GameInputActions.Reload);
            var inputStarted = _player.IsReloading;
            _player.ProcessMode = ProcessModeEnum.Disabled;

            var poseSet = true;
            var armsActive = true;
            var primaryGripFixed = true;
            var supportTracks = true;
            var visibleMotion = true;
            var viewMotion = true;
            var mechanismStages = true;
            var maximumGripResidual = 0.0f;
            var maximumSupportDistance = 0.0f;
            var minimumArmMotion = float.PositiveInfinity;
            var minimumViewMotion = float.PositiveInfinity;
            var samples = new[] { 0.28f, 0.64f, 0.88f };
            foreach (var progress in samples)
            {
                var set = _player.SetReloadPoseForDiagnostics(progress);
                var inspection = _player.InspectSidearmReloadForDiagnostics();
                var armMotion = SidearmReloadTransformDelta(
                    idle.LeftArmTransform,
                    inspection.LeftArmTransform);
                var targetMotion = inspection.ReloadViewTarget.DistanceTo(idle.ReloadViewTarget)
                    + inspection.ReloadRotationTarget.DistanceTo(idle.ReloadRotationTarget);
                var expectedMechanism = progress < 0.43f
                    ? inspection.PrimaryMagazineVisible && !inspection.SpareMagazineVisible
                    : progress < 0.78f
                        ? !inspection.PrimaryMagazineVisible && inspection.SpareMagazineVisible
                        : inspection.PrimaryMagazineVisible
                            && !inspection.SpareMagazineVisible
                            && inspection.SlideTravel >= 0.025f;
                var sampleValid = set
                    && inspection.AuthoredArmsActive
                    && inspection.Reloading
                    && Mathf.Abs(inspection.Progress - progress) <= 0.02f
                    && inspection.RightGripResidual <= 0.002f
                    && inspection.SupportTargetDistance <= 0.002f
                    && armMotion >= 0.035f
                    && targetMotion >= 0.02f
                    && expectedMechanism;
                poseSet &= set;
                armsActive &= inspection.AuthoredArmsActive;
                primaryGripFixed &= inspection.RightGripResidual <= 0.002f;
                supportTracks &= inspection.SupportTargetDistance <= 0.002f;
                visibleMotion &= armMotion >= 0.035f;
                viewMotion &= targetMotion >= 0.02f;
                mechanismStages &= expectedMechanism;
                maximumGripResidual = Mathf.Max(
                    maximumGripResidual,
                    inspection.RightGripResidual);
                maximumSupportDistance = Mathf.Max(
                    maximumSupportDistance,
                    inspection.SupportTargetDistance);
                minimumArmMotion = Mathf.Min(minimumArmMotion, armMotion);
                minimumViewMotion = Mathf.Min(minimumViewMotion, targetMotion);
                GD.Print(
                    $"SIDEARM_RELOAD_SAMPLE platform={platform} progress={progress:F2} "
                    + $"valid={sampleValid} arms={inspection.AuthoredArmsActive} "
                    + $"reloading={inspection.Reloading} actual_progress={inspection.Progress:F4} "
                    + $"grip_residual={inspection.RightGripResidual:F6} "
                    + $"support_distance={inspection.SupportTargetDistance:F6} "
                    + $"arm_motion={armMotion:F6} view_motion={targetMotion:F6} "
                    + $"primary_mag={inspection.PrimaryMagazineVisible} "
                    + $"spare_mag={inspection.SpareMagazineVisible} "
                    + $"slide_travel={inspection.SlideTravel:F6}");
                if (Mathf.Abs(progress - 0.64f) <= 0.001f)
                {
                    SaveViewportImage(
                        $"res://sidearm_reload_{platform.ToString().ToLowerInvariant()}_validation.png");
                }
            }

            _player.ProcessMode = ProcessModeEnum.Inherit;
            _player.ClearReloadPoseForDiagnostics();
            _player.SetAmmoGradeForDiagnostics(LootGrade.Common, magazineSize * 2);
            var reserveBefore = _player.ReserveAmmo;
            var completed = _player.ReloadImmediatelyForDiagnostics(0);
            var ammoCompleted = completed
                && !_player.IsReloading
                && _player.Ammo == magazineSize
                && _player.ReserveAmmo == reserveBefore - magazineSize;
            var platformValid = idle.AuthoredArmsActive
                && inputStarted
                && poseSet
                && armsActive
                && primaryGripFixed
                && supportTracks
                && visibleMotion
                && viewMotion
                && mechanismStages
                && ammoCompleted;
            valid &= platformValid;
            results.Add(
                $"platform={platform} valid={platformValid} input_started={inputStarted} "
                + $"pose_set={poseSet} arms={armsActive} primary_grip={primaryGripFixed} "
                + $"support_tracks={supportTracks} visible_motion={visibleMotion} "
                + $"view_motion={viewMotion} mechanism_stages={mechanismStages} "
                + $"ammo_completed={ammoCompleted} ammo={_player.Ammo} "
                + $"reserve={_player.ReserveAmmo} max_grip_residual={maximumGripResidual:F6} "
                + $"max_support_distance={maximumSupportDistance:F6} "
                + $"min_arm_motion={minimumArmMotion:F6} "
                + $"min_view_motion={minimumViewMotion:F6}");
        }

        foreach (var result in results)
        {
            GD.Print($"SIDEARM_RELOAD_PLATFORM {result}");
        }
        GD.Print(
            $"SIDEARM_RELOAD_CHECK valid={valid} physical_r={reloadKeyBound} "
            + $"platforms={results.Count}");
        GD.Print($"SIDEARM_RELOAD_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }

    private static bool ReloadActionUsesPhysicalR()
    {
        foreach (var inputEvent in InputMap.ActionGetEvents(GameInputActions.Reload))
        {
            if (inputEvent is InputEventKey key
                && (key.PhysicalKeycode == Key.R || key.Keycode == Key.R))
            {
                return true;
            }
        }
        return false;
    }

    private static float SidearmReloadTransformDelta(Transform3D idle, Transform3D current)
        => idle.Origin.DistanceTo(current.Origin)
            + idle.Basis.X.DistanceTo(current.Basis.X)
            + idle.Basis.Y.DistanceTo(current.Basis.Y)
            + idle.Basis.Z.DistanceTo(current.Basis.Z);
}
