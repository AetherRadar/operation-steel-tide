using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private void ValidateReloadRenderCadence()
    {
        var cadences = TacticalPlayer
            .InspectStandardReloadRenderCadencesForDiagnostics();
        foreach (var cadence in cadences)
        {
            GD.Print(
                $"RELOAD_RENDER_CADENCE_CHECK rate={cadence.RenderRate:F0} "
                + $"valid={cadence.Valid} moving={cadence.MovingSamples} "
                + $"repeated={cadence.RepeatedMovingSamples} "
                + $"max_step={cadence.MaximumProgressStep:F6} "
                + $"monotonic={cadence.Monotonic}");
        }

        var expectedRates = new[] { 60.0f, 120.0f, 144.0f };
        var valid = cadences.Length == expectedRates.Length
            && cadences.All(cadence => cadence.Valid)
            && expectedRates.All(rate => cadences.Any(
                cadence => Mathf.IsEqualApprox(cadence.RenderRate, rate)));
        GD.Print(
            $"RELOAD_RENDER_CADENCE_PASS valid={valid} "
            + "rates=60/120/144 physics_rate=60");
        GetTree().Quit(valid ? 0 : 2);
    }
}
