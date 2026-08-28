using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private async void ValidateRefineryAtmosphere()
    {
        await WaitFrames(4);
        if (!IsBlackwaterRefineryMap
            || !IsInstanceValid(_environmentRef)
            || !IsInstanceValid(_sunLight)
            || !IsInstanceValid(_fillLight))
        {
            GD.Print("REFINERY_ATMOSPHERE_CHECK valid=False reason=missing_environment");
            GD.Print("REFINERY_ATMOSPHERE_PASS valid=False");
            QuitDiagnosticAfterSceneCleanup(2);
            return;
        }

        var originalTime = _deploymentTimeOfDay;
        ApplyTimeOfDay(DeploymentTimeOfDay.Day);
        await WaitFrames(3);
        var daySky = _environmentRef.Sky?.SkyMaterial as ProceduralSkyMaterial;
        var daySkyTop = daySky?.SkyTopColor ?? Colors.Black;
        var daySkyHorizon = daySky?.SkyHorizonColor ?? Colors.Black;
        var daySkyEnergy = daySky?.SkyEnergyMultiplier ?? 0.0f;
        var dayAmbient = _environmentRef.AmbientLightEnergy;
        var dayFog = _environmentRef.FogDensity;
        var dayExposure = _environmentRef.TonemapExposure;
        var daySun = _sunLight.LightEnergy;
        var dayFill = _fillLight.LightEnergy;
        var dayEnvironmentReady = _environmentRef.BackgroundMode == Godot.Environment.BGMode.Sky
            && _environmentRef.AmbientLightSource == Godot.Environment.AmbientSource.Sky
            && _environmentRef.ReflectedLightSource == Godot.Environment.ReflectionSource.Sky
            && _environmentRef.AdjustmentEnabled
            && _environmentRef.AdjustmentBrightness >= 1.00f;
        var dayReady = daySky is not null
            && daySkyTop.B >= 0.55f
            && daySkyHorizon.R >= 0.44f
            && daySkyEnergy >= 0.94f
            && _environmentRef.BackgroundEnergyMultiplier >= 0.99f
            && dayAmbient >= 1.10f
            && dayFog <= 0.0011f
            && _environmentRef.FogSkyAffect <= 0.10f
            && dayExposure >= 0.96f
            && daySun >= 1.00f
            && dayFill >= 0.60f
            && dayEnvironmentReady;

        ApplyTimeOfDay(DeploymentTimeOfDay.Dusk);
        await WaitFrames(3);
        var duskReady = _environmentRef.Sky?.SkyMaterial is PanoramaSkyMaterial
            && _environmentRef.AmbientLightEnergy < 0.85f
            && _sunLight.LightEnergy < 0.80f;

        ApplyTimeOfDay(originalTime);
        var valid = dayReady && duskReady;
        GD.Print(
            $"REFINERY_ATMOSPHERE_CHECK valid={valid} day={dayReady} "
            + $"environment={dayEnvironmentReady} "
            + $"sky_top={daySkyTop} sky_horizon={daySkyHorizon} "
            + $"sky_energy={daySkyEnergy:0.00} "
            + $"ambient={dayAmbient:0.00} "
            + $"fog={dayFog:0.00000} "
            + $"exposure={dayExposure:0.00} "
            + $"sun={daySun:0.00} fill={dayFill:0.00} "
            + $"dusk_panorama={duskReady}");
        GD.Print($"REFINERY_ATMOSPHERE_PASS valid={valid}");
        QuitDiagnosticAfterSceneCleanup(valid ? 0 : 2);
    }
}
