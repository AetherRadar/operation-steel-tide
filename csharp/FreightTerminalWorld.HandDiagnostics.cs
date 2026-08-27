using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private async void ValidateHandDiagnostics()
    {
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
        foreach (var platform in Enum.GetValues<WeaponPlatform>())
        {
            _player.GrantFireablePrimaryForDiagnostics(WeaponCatalog.Build(platform, 0));
            await WaitFrames(8);
            posesValid &= _player.WeaponHandPoseValidForDiagnostics;
            authoredRigValid &= platform == WeaponPlatform.M3A1 || _player.UsesAuthoredHandRigForDiagnostics;
            var handInspection = _player.InspectAuthoredHandPoseForDiagnostics();
            // Ensure arms are refreshed
            var useAuthoredM4 = platform == WeaponPlatform.M4A1;
            var useAuthoredSmg = platform == WeaponPlatform.M3A1;
            var useAuthoredPlatform = platform is not WeaponPlatform.M4A1 and not WeaponPlatform.M3A1 and not WeaponPlatform.GSh18 and not WeaponPlatform.DesertEagle;
            var procArmsVisible = IsInstanceValid(_player.GetNodeOrNull<Node3D>("Camera3D/WeaponRoot/ProceduralFirstPersonArms")) ? _player.GetNodeOrNull<Node3D>("Camera3D/WeaponRoot/ProceduralFirstPersonArms")?.Visible : false;
            // Try to get support hand global pos via reflection (private fields)
            var supportHandPos = Vector3.Zero;
            var supportForearmPos = Vector3.Zero;
            var rightHandVisible = false;
            var leftHandVisible = false;
            try
            {
                var type = typeof(TacticalPlayer);
                var fArms = type.GetField("_proceduralFirstPersonArms", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var fSupportHand = type.GetField("_supportHand", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var fSupportForearm = type.GetField("_supportForearm", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var fCamera = type.GetField("_camera", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var fArmsNode = fArms?.GetValue(_player) as Node3D;
                var supHand = fSupportHand?.GetValue(_player) as Node3D;
                var supFore = fSupportForearm?.GetValue(_player) as Node3D;
                var cam = fCamera?.GetValue(_player) as Camera3D;
                if (fArmsNode != null) procArmsVisible = fArmsNode.Visible;
                Vector2 supHandScreen = Vector2.Zero;
                Vector2 rightHandScreen = Vector2.Zero;
                bool supHandBehind = false;
                bool rightHandBehind = false;
                if (supHand != null) { supportHandPos = supHand.GlobalPosition; leftHandVisible = supHand.Visible; if (cam != null) { var pos = cam.UnprojectPosition(supportHandPos); supHandScreen = pos; supHandBehind = cam.IsPositionBehind(supportHandPos); } }
                if (supFore != null) supportForearmPos = supFore.GlobalPosition;
                // right hand is sibling of support hand, find by name
                Node3D? rightNode = null;
                if (fArmsNode != null)
                {
                    rightNode = fArmsNode.GetNodeOrNull<Node3D>("RightTacticalHand");
                    if (rightNode != null) { rightHandVisible = rightNode.Visible; if (cam != null) { var rp = rightNode.GlobalPosition; rightHandScreen = cam.UnprojectPosition(rp); rightHandBehind = cam.IsPositionBehind(rp); } }
                }
                // authored arms (the active pose may be rifle, service pistol, or large pistol)
                var authArms = _player.ActiveAuthoredArmsForDiagnostics;
                if (authArms != null && IsInstanceValid(authArms.Root))
                {
                    var authVisible = authArms.Root.Visible;
                    var armBounds = AuthoredArmWorldBounds(authArms.Root);
                    var weaponRoot = _player.ActiveAuthoredWeaponRootForDiagnostics;
                    var weaponBounds = weaponRoot is not null
                        ? AuthoredArmWorldBounds(weaponRoot)
                        : new Aabb();
                    var weaponRootInverse = _player.WeaponRootGlobalTransformForDiagnostics.AffineInverse();
                    var weaponLocalTransform = weaponRoot is not null
                        ? weaponRootInverse * weaponRoot.GlobalTransform
                        : Transform3D.Identity;
                    var foregrip = _player.ActiveAuthoredForegripForDiagnostics;
                    var muzzle = _player.ActiveAuthoredMuzzleForDiagnostics;
                    var foregripLocal = foregrip is not null
                        ? weaponRootInverse * foregrip.GlobalTransform
                        : Transform3D.Identity;
                    var muzzleLocal = muzzle is not null
                        ? weaponRootInverse * muzzle.GlobalTransform
                        : Transform3D.Identity;
                    var rightPalmLocal = weaponRootInverse * authArms.RightPalmFrame.GlobalTransform;
                    var leftPalmLocal = weaponRootInverse * authArms.LeftPalmFrame.GlobalTransform;
                    var rPalm = authArms.RightPalmFrame.GlobalPosition;
                    var lPalm = authArms.LeftPalmFrame.GlobalPosition;
                    var rStaticLocal = authArms.RightPalmTransformInRoot.Origin;
                    var lStaticLocal = authArms.LeftPalmTransformInRoot.Origin;
                    var rPalmScreen = cam != null ? cam.UnprojectPosition(rPalm) : Vector2.Zero;
                    var lPalmScreen = cam != null ? cam.UnprojectPosition(lPalm) : Vector2.Zero;
                    var rBehind = cam != null ? cam.IsPositionBehind(rPalm) : false;
                    var lBehind = cam != null ? cam.IsPositionBehind(lPalm) : false;
                    results.Add($"{platform}: procArmsVis={procArmsVisible} authArmsVis={authVisible} rightVis={rightHandVisible} leftVis={leftHandVisible} supHandPos={supportHandPos} supHandScreen={supHandScreen} behind={supHandBehind} rightScreen={rightHandScreen} rightBehind={rightHandBehind} rPalm={rPalm} rStaticLocal={rStaticLocal} rPalmLocal={rightPalmLocal.Origin} rScreen={rPalmScreen} rBehind={rBehind} lPalm={lPalm} lStaticLocal={lStaticLocal} lPalmLocal={leftPalmLocal.Origin} lScreen={lPalmScreen} lBehind={lBehind} gripResidual={handInspection.GripResidual:F4} supportResidual={handInspection.SupportGripResidual:F4} palmSeparation={handInspection.PalmSeparation:F4} wristLengths=({handInspection.RightWristLength:F4},{handInspection.LeftWristLength:F4}) scale={handInspection.RootScale} determinant={handInspection.RootDeterminant:F3} rootBasis={handInspection.RootTransform.Basis} armBounds=({armBounds.Position},{armBounds.End}) weaponLocal=({weaponLocalTransform.Origin}) weaponBasis=({weaponLocalTransform.Basis}) weaponBounds=({weaponBounds.Position},{weaponBounds.End}) foregripLocal=({foregripLocal.Origin}) muzzleLocal=({muzzleLocal.Origin}) handValid={handInspection.Valid} weaponVis={_player.UsesAuthoredWeaponPlatformForDiagnostics(platform)}");
                    continue;
                }
            }
            catch (Exception e) { results.Add($"{platform}: exception {e.Message}"); continue; }
            results.Add($"{platform}: procArmsVis={procArmsVisible} rightVis={rightHandVisible} leftVis={leftHandVisible} supHandPos={supportHandPos} weaponVis={_player.UsesAuthoredWeaponPlatformForDiagnostics(platform)}");
        }
        _player.GrantFireablePrimaryForDiagnostics(
            WeaponCatalog.Build(WeaponPlatform.M3A1, 0));
        await WaitFrames(8);
        var reloadPoseSet = _player.SetReloadPoseForDiagnostics(0.46f);
        await WaitFrames(4);
        var smgArmBounds = _player.SmgArmBoundsSizeForDiagnostics;
        // The long axis is Z after Godot's Y-up conversion. In the imported
        // SMG root scale the original short sleeves measured about 0.021; the
        // corrected shoulder continuation must exceed 0.030.
        var smgSleeveReach = smgArmBounds.Z >= 0.03f;
        var smgReloadPresentation = reloadPoseSet
            && _player.SmgReloadPresentationValidForDiagnostics;
        _player.ClearReloadPoseForDiagnostics();
        foreach (var line in results) GD.Print(line);
        var valid = posesValid
            && authoredRigValid
            && smgSleeveReach
            && smgReloadPresentation;
        GD.Print(
            $"HAND_POSE_CHECK valid={valid} procedural_pose={posesValid} "
            + $"authored_rig={authoredRigValid} smg_sleeve_reach={smgSleeveReach} "
            + $"smg_arm_bounds={smgArmBounds} "
            + $"smg_reload_presentation={smgReloadPresentation} samples={results.Count}");
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
