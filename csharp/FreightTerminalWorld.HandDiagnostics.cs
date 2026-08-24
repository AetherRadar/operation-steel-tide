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
        var firstPass = new Dictionary<WeaponPlatform, FirstPersonArmRigSnapshot>();
        var selectionValid = true;
        var gripValid = true;
        var scaleValid = true;
        var screenValid = true;
        foreach (var platform in Enum.GetValues<WeaponPlatform>())
        {
            _player.GrantFireablePrimaryForDiagnostics(WeaponCatalog.Build(platform, 0));
            await WaitFrames(8);
            var snapshot = _player.InspectFirstPersonArmRigForDiagnostics();
            firstPass[platform] = snapshot;
            selectionValid &= snapshot.Active
                && !snapshot.ProceduralVisible
                && _player.UsesAuthoredWeaponPlatformForDiagnostics(platform);
            gripValid &= snapshot.NativeSmgRig
                || snapshot.PrimaryGripError <= 0.002f && snapshot.SupportGripError <= 0.002f;
            scaleValid &= IsFinite(snapshot.RootScale)
                && snapshot.RootScale.X > 0.0f
                && Mathf.Abs(snapshot.RootScale.X - snapshot.RootScale.Y) <= 0.0001f
                && Mathf.Abs(snapshot.RootScale.X - snapshot.RootScale.Z) <= 0.0001f;
            screenValid &= IsFinite(snapshot.PrimaryPalmLocal)
                && IsFinite(snapshot.SupportPalmLocal)
                && IsOnScreen(snapshot.PrimaryScreen)
                && IsOnScreen(snapshot.SupportScreen)
                && !snapshot.PrimaryBehindCamera
                && !snapshot.SupportBehindCamera;
            results.Add(FormatArmSnapshot(snapshot));
        }

        var idempotent = true;
        foreach (var platform in Enum.GetValues<WeaponPlatform>())
        {
            _player.GrantFireablePrimaryForDiagnostics(WeaponCatalog.Build(platform, 0));
            await WaitFrames(3);
            var repeated = _player.InspectFirstPersonArmRigForDiagnostics();
            var original = firstPass[platform];
            idempotent &= repeated.InstanceId == original.InstanceId
                && repeated.RootScale.DistanceTo(original.RootScale) <= 0.0001f
                && repeated.PrimaryPalmLocal.DistanceTo(original.PrimaryPalmLocal) <= 0.002f
                && repeated.SupportPalmLocal.DistanceTo(original.SupportPalmLocal) <= 0.002f;
        }
        foreach (var line in results) GD.Print(line);
        var valid = selectionValid && gripValid && scaleValid && screenValid && idempotent;
        GD.Print($"HAND_POSE_CHECK valid={valid} samples={results.Count} selection={selectionValid} grips={gripValid} scale={scaleValid} screen={screenValid} idempotent={idempotent}");
        GD.Print($"HAND_POSE_PASS valid={valid}");
        GD.Print($"HAND_DIAGNOSTICS_DONE count={results.Count}");
        GetTree().Quit(valid ? 0 : 2);
    }

    private static bool IsFinite(Vector3 value)
        => float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);

    private static bool IsOnScreen(Vector2 value)
        => float.IsFinite(value.X)
        && float.IsFinite(value.Y)
        && value.X is >= -0.1f and <= 1.1f
        && value.Y is >= -0.1f and <= 1.1f;

    private static string FormatArmSnapshot(FirstPersonArmRigSnapshot snapshot)
        => $"{snapshot.Platform}: pose={snapshot.PoseKind} active={snapshot.Active} "
        + $"native={snapshot.NativeSmgRig} procedural={snapshot.ProceduralVisible} "
        + $"scale={snapshot.RootScale} primary_error={snapshot.PrimaryGripError:F4} "
        + $"support_error={snapshot.SupportGripError:F4} primary_screen={snapshot.PrimaryScreen} "
        + $"support_screen={snapshot.SupportScreen} behind={snapshot.PrimaryBehindCamera}/{snapshot.SupportBehindCamera}";
}
