using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private async void ValidateDemolitionLighting()
    {
        await WaitFrames(4);
        _deploymentTimeOfDay = DeploymentTimeOfDay.Night;
        ApplyTimeOfDay(_deploymentTimeOfDay);
        var inheritedAmbient = _environmentRef.AmbientLightEnergy;
        var inheritedExposure = _environmentRef.TonemapExposure;
        var darkSourceConfirmed = inheritedAmbient <= 0.10f
            && inheritedExposure <= 0.80f;

        _demolitionSelectedMapId = DemolitionMapCatalog.TideforgeId;
        PrepareDemolitionBattlefield();
        await WaitFrames(8);
        var initial = InspectDemolitionLightingForDiagnostics();

        // Applying graphics settings may update shared fog and sky resources;
        // demolition must restore its competitive profile immediately afterward.
        _environmentRef.VolumetricFogEnabled = true;
        ApplyQuality(_qualitySetting);
        var afterQualityChange = InspectDemolitionLightingForDiagnostics();

        // A late deployment time signal must update the saved extraction choice
        // without replacing the active competitive lighting profile.
        _deploymentTimeOfDay = DeploymentTimeOfDay.Dusk;
        ApplyTimeOfDay(_deploymentTimeOfDay);
        var isolated = InspectDemolitionLightingForDiagnostics();
        var profileStable = initial.Valid
            && afterQualityChange.Valid
            && isolated.Valid
            && Mathf.IsEqualApprox(initial.AmbientEnergy, isolated.AmbientEnergy)
            && Mathf.IsEqualApprox(initial.Exposure, isolated.Exposure)
            && Mathf.IsEqualApprox(initial.SunEnergy, isolated.SunEnergy)
            && Mathf.IsEqualApprox(initial.FillEnergy, isolated.FillEnergy);
        var valid = darkSourceConfirmed
            && _demolitionMode
            && _demolitionArena?.Active == true
            && profileStable;

        GD.Print(
            $"DEMOLITION_LIGHTING_CHECK valid={valid} dark_source={darkSourceConfirmed} "
            + $"source_ambient={inheritedAmbient:F3} source_exposure={inheritedExposure:F3} "
            + $"profile={initial.Valid} isolated={profileStable} day_sky={isolated.DaySkyActive} "
            + $"ambient={isolated.AmbientEnergy:F3} exposure={isolated.Exposure:F3} "
            + $"background={isolated.BackgroundEnergy:F3} brightness={isolated.Brightness:F3} "
            + $"contrast={isolated.Contrast:F3} fog={isolated.FogDensity:F5} "
            + $"volumetric_fog={isolated.VolumetricFogEnabled} "
            + $"sun={isolated.SunEnergy:F3} fill={isolated.FillEnergy:F3} "
            + $"selected_time={_deploymentTimeOfDay}");
        GD.Print($"DEMOLITION_LIGHTING_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }
}
