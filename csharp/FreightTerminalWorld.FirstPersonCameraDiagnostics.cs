using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private void ValidateFirstPersonCameraInterpolation()
    {
        var cadences = TacticalPlayer
            .InspectStandardFirstPersonCameraCadencesForDiagnostics();
        foreach (var cadence in cadences)
        {
            GD.Print(
                $"FIRST_PERSON_CAMERA_CHECK rate={cadence.RenderRate:F0} "
                + $"valid={cadence.Valid} moving={cadence.MovingSamples} "
                + $"repeated={cadence.RepeatedMovingSamples} "
                + $"max_step={cadence.MaximumPositionStep:F6} "
                + $"expected_step={cadence.ExpectedPositionStep:F6} "
                + $"view_height_error={cadence.ViewHeightError:F6} "
                + $"instant_look={cadence.ImmediateLookRotation} "
                + $"monotonic={cadence.Monotonic}");
        }

        var expectedRates = new[] { 60.0f, 120.0f, 144.0f };
        var globalInterpolationEnabled = ProjectSettings.GetSetting(
            "physics/common/physics_interpolation",
            false).AsBool();
        var manualBranchReady = IsInstanceValid(_player)
            && _player.UsesManualFirstPersonTransformForDiagnostics;
        var actualCamera = IsInstanceValid(_player)
            ? _player.InspectFirstPersonCameraNodeForDiagnostics()
            : default;
        GD.Print(
            $"FIRST_PERSON_CAMERA_NODE_CHECK valid={actualCamera.Valid} "
            + $"step_a={actualCamera.FirstPositionStep:F6} "
            + $"step_b={actualCamera.SecondPositionStep:F6} "
            + $"view_height_error={actualCamera.ViewHeightError:F6} "
            + $"instant_look_dot={actualCamera.ImmediateLookDot:F6} "
            + $"top_level={actualCamera.TopLevel}");
        var localViewHeight = IsInstanceValid(_player)
            && _player.ViewHeight is > 0.35f and < 1.8f;
        var authoritativeAimReady = IsInstanceValid(_player)
            && _player.PhysicsAimTransformForDiagnostics.Basis
                .Determinant() > 0.99f;
        var valid = cadences.Length == expectedRates.Length
            && cadences.All(cadence => cadence.Valid)
            && expectedRates.All(rate => cadences.Any(
                cadence => Mathf.IsEqualApprox(cadence.RenderRate, rate)))
            && manualBranchReady
            && actualCamera.Valid
            && localViewHeight
            && authoritativeAimReady;
        GD.Print(
            $"FIRST_PERSON_CAMERA_PASS valid={valid} "
            + "rates=60/120/144 physics_rate=60 "
            + $"global_auto={globalInterpolationEnabled} "
            + $"manual_branch={manualBranchReady} "
            + $"local_view_height={localViewHeight} "
            + $"authoritative_aim={authoritativeAimReady}");
        GetTree().Quit(valid ? 0 : 2);
    }
}
