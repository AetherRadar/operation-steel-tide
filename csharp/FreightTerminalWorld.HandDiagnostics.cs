using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private async void ValidateHandDiagnostics(bool narrow = false)
    {
        if (narrow)
        {
            var window = GetWindow();
            window.ContentScaleAspect = Window.ContentScaleAspectEnum.Ignore;
            window.Size = new Vector2I(985, 847);
        }
        await WaitFrames(4);
        // Move player to open area away from walls for clear view
        _player.GlobalPosition = new Vector3(0, 0.2f, 40.0f);
        _player.Velocity = Vector3.Zero;
        _player.FaceWorldPointForDiagnostics(new Vector3(0, 0.2f, -40.0f));
        foreach (var enemy in _enemies) if (IsInstanceValid(enemy)) enemy.ProcessMode = ProcessModeEnum.Disabled;
        foreach (var mate in _squadMates) if (IsInstanceValid(mate)) mate.GlobalPosition = new Vector3(240, 80, 240);
        await WaitFrames(6);

        var results = new List<string>();
        var posesValid = true;
        var authoredRigValid = true;
        var servicePistolCorrectionValid = true;
        var platforms = Enum.GetValues<WeaponPlatform>();
        var proceduralArms = _player.GetNodeOrNull<Node3D>(
            "Camera3D/WeaponRoot/ProceduralFirstPersonArms");
        foreach (var platform in platforms)
        {
            _player.GrantFireablePrimaryForDiagnostics(WeaponCatalog.Build(platform, 0));
            await WaitFrames(8);
            var proceduralVisible = IsInstanceValid(proceduralArms)
                && proceduralArms.Visible;
            var weaponVisible = _player.UsesAuthoredWeaponPlatformForDiagnostics(platform);
            posesValid &= !proceduralVisible && weaponVisible;
            if (platform == WeaponPlatform.M3A1)
            {
                var nativeRigValid = _player.WeaponHandPoseValidForDiagnostics;
                posesValid &= nativeRigValid;
                results.Add(
                    $"{platform}: nativeRig={nativeRigValid} "
                    + $"proceduralVisible={proceduralVisible} weaponVisible={weaponVisible}");
                continue;
            }

            var handInspection = _player.InspectAuthoredHandPoseForDiagnostics();
            var arms = _player.ActiveAuthoredArmsForDiagnostics;
            if (arms is null || !IsInstanceValid(arms.Root))
            {
                posesValid = false;
                authoredRigValid = false;
                results.Add($"{platform}: authoredRig=false weaponVisible={weaponVisible}");
                continue;
            }

            var rootVisible = arms.Root.IsVisibleInTree();
            var rightVisible = arms.RightArm.IsVisibleInTree();
            var leftVisible = arms.LeftArm.IsVisibleInTree();
            authoredRigValid &= rootVisible
                && rightVisible
                && leftVisible
                && _player.UsesAuthoredHandRigForDiagnostics;
            var realigned = _player.RealignAuthoredHandsForDiagnostics();
            var idempotent = handInspection.RootTransform.Origin.DistanceTo(realigned.Origin) <= 0.0001f
                && handInspection.RootTransform.Basis.X.DistanceTo(realigned.Basis.X) <= 0.0001f
                && handInspection.RootTransform.Basis.Y.DistanceTo(realigned.Basis.Y) <= 0.0001f
                && handInspection.RootTransform.Basis.Z.DistanceTo(realigned.Basis.Z) <= 0.0001f;
            posesValid &= handInspection.Valid && idempotent;
            if (platform is WeaponPlatform.P226 or WeaponPlatform.M1911 or WeaponPlatform.GSh18)
            {
                servicePistolCorrectionValid &= handInspection.SupportArmCorrection
                    <= TacticalPlayer.MaxServicePistolSupportArmCorrection;
            }

            var weaponRootInverse = _player.WeaponRootGlobalTransformForDiagnostics.AffineInverse();
            var weaponRoot = _player.ActiveAuthoredWeaponRootForDiagnostics;
            var weaponBounds = weaponRoot is not null
                ? AuthoredArmWorldBounds(weaponRoot)
                : new Aabb();
            var foregrip = _player.ActiveAuthoredForegripForDiagnostics;
            var muzzle = _player.ActiveAuthoredMuzzleForDiagnostics;
            var rightPalmLocal = weaponRootInverse * handInspection.RightPalm;
            var leftPalmLocal = weaponRootInverse * handInspection.LeftPalm;
            var rightGripLocal = weaponRootInverse * arms.RightGripFrame.GlobalPosition;
            var leftGripLocal = weaponRootInverse * arms.LeftGripFrame.GlobalPosition;
            var foregripLocal = foregrip is not null
                ? weaponRootInverse * foregrip.GlobalPosition
                : Vector3.Zero;
            var muzzleLocal = muzzle is not null
                ? weaponRootInverse * muzzle.GlobalPosition
                : Vector3.Zero;
            results.Add(
                $"{platform}: handValid={handInspection.Valid} idempotent={idempotent} "
                + $"proceduralVisible={proceduralVisible} authoredVisible=({rootVisible},{rightVisible},{leftVisible}) "
                + $"weaponVisible={weaponVisible} gripResidual=({handInspection.GripResidual:F4},{handInspection.SupportGripResidual:F4}) "
                + $"supportArmCorrection={handInspection.SupportArmCorrection:F4} "
                + $"surfaceDistance=({handInspection.PrimarySurfaceDistance:F4},{handInspection.SupportSurfaceDistance:F4}) "
                + $"surfaceOffset=({handInspection.PrimarySurfaceOffset},{handInspection.SupportSurfaceOffset}) "
                + $"palms=({rightPalmLocal},{leftPalmLocal}) grips=({rightGripLocal},{leftGripLocal}) "
                + $"foregrip={foregripLocal} muzzle={muzzleLocal} weaponBounds=({weaponBounds.Position},{weaponBounds.End})");
        }
        _player.GrantFireablePrimaryForDiagnostics(
            WeaponCatalog.Build(WeaponPlatform.M3A1, 0));
        await WaitFrames(8);
        var reloadPoseSet = _player.SetReloadPoseForDiagnostics(0.46f);
        await WaitFrames(4);
        var smgArmBounds = _player.SmgArmBoundsSizeForDiagnostics;
        // Z is the sleeve reach and Y captures its camera-facing depth after
        // Godot's Y-up conversion. The first correction reached 0.0315 by
        // stretching a thin open cut; the full upper-arm continuation must
        // remain both longer and materially deeper than that profile.
        var smgSleeveReach = smgArmBounds.Z >= 0.04f;
        var smgSleeveVolume = smgArmBounds.Y >= 0.006f;
        var smgReloadPresentation = reloadPoseSet
            && _player.SmgReloadPresentationValidForDiagnostics;
        _player.ClearReloadPoseForDiagnostics();
        foreach (var line in results) GD.Print(line);
        var valid = posesValid
            && authoredRigValid
            && servicePistolCorrectionValid
            && smgSleeveReach
            && smgSleeveVolume
            && smgReloadPresentation
            && results.Count == platforms.Length;
        GD.Print(
            $"HAND_POSE_CHECK valid={valid} procedural_pose={posesValid} "
            + $"authored_rig={authoredRigValid} smg_sleeve_reach={smgSleeveReach} "
            + $"service_pistol_correction={servicePistolCorrectionValid} "
            + $"smg_sleeve_volume={smgSleeveVolume} "
            + $"smg_arm_bounds={smgArmBounds} "
            + $"smg_reload_presentation={smgReloadPresentation} "
            + $"samples={results.Count} viewport={(narrow ? "985x847" : "default")}");
        GD.Print($"HAND_POSE_PASS valid={valid}");
        GD.Print($"HAND_DIAGNOSTICS_DONE count={results.Count}");
        GetTree().Quit(valid ? 0 : 2);
    }

    private static Aabb AuthoredArmWorldBounds(Node3D root)
    {
        var hasBounds = false;
        var bounds = new Aabb();
        foreach (var mesh in CombatModelLibrary.MeshesBelow(root))
        {
            var local = mesh.Mesh?.GetAabb() ?? new Aabb();
            for (var x = 0; x <= 1; x++)
            {
                for (var y = 0; y <= 1; y++)
                {
                    for (var z = 0; z <= 1; z++)
                    {
                        var point = local.Position + new Vector3(
                            local.Size.X * x,
                            local.Size.Y * y,
                            local.Size.Z * z);
                        var worldPoint = mesh.GlobalTransform * point;
                        if (!hasBounds)
                        {
                            bounds = new Aabb(worldPoint, Vector3.Zero);
                            hasBounds = true;
                        }
                        else
                        {
                            bounds = bounds.Expand(worldPoint);
                        }
                    }
                }
            }
        }
        return bounds;
    }
}
