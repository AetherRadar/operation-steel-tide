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
                // authored rifle arms
                var fAuthArms = type.GetField("_authoredRifleArms", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var authArms = fAuthArms?.GetValue(_player) as AuthoredFirstPersonArmsVisual;
                if (authArms != null && IsInstanceValid(authArms.Root))
                {
                    var authVisible = authArms.Root.Visible;
                    var rPalm = authArms.PalmPosition("R_palm_039");
                    var lPalm = authArms.PalmPosition("L_palm_015");
                    var rPalmScreen = cam != null ? cam.UnprojectPosition(rPalm) : Vector2.Zero;
                    var lPalmScreen = cam != null ? cam.UnprojectPosition(lPalm) : Vector2.Zero;
                    var rBehind = cam != null ? cam.IsPositionBehind(rPalm) : false;
                    var lBehind = cam != null ? cam.IsPositionBehind(lPalm) : false;
                    results.Add($"{platform}: procArmsVis={procArmsVisible} authArmsVis={authVisible} rightVis={rightHandVisible} leftVis={leftHandVisible} supHandPos={supportHandPos} supHandScreen={supHandScreen} behind={supHandBehind} rightScreen={rightHandScreen} rightBehind={rightHandBehind} rPalm={rPalm} rScreen={rPalmScreen} rBehind={rBehind} lPalm={lPalm} lScreen={lPalmScreen} lBehind={lBehind} weaponVis={_player.UsesAuthoredWeaponPlatformForDiagnostics(platform)}");
                    continue;
                }
            }
            catch (Exception e) { results.Add($"{platform}: exception {e.Message}"); continue; }
            results.Add($"{platform}: procArmsVis={procArmsVisible} rightVis={rightHandVisible} leftVis={leftHandVisible} supHandPos={supportHandPos} weaponVis={_player.UsesAuthoredWeaponPlatformForDiagnostics(platform)}");
        }
        foreach (var line in results) GD.Print(line);
        var valid = posesValid && authoredRigValid;
        GD.Print($"HAND_POSE_CHECK valid={valid} procedural_pose={posesValid} authored_rig={authoredRigValid} samples={results.Count}");
        GD.Print($"HAND_POSE_PASS valid={valid}");
        GD.Print($"HAND_DIAGNOSTICS_DONE count={results.Count}");
        GetTree().Quit(valid ? 0 : 2);
    }
}
