using System;
using Godot;

namespace OperationSteelTide;

internal readonly record struct DemolitionLightingInspection(
    bool Available,
    bool DaySkyActive,
    float AmbientEnergy,
    float Exposure,
    float BackgroundEnergy,
    float Brightness,
    float Contrast,
    float FogDensity,
    bool VolumetricFogEnabled,
    float SunEnergy,
    float FillEnergy)
{
    public bool Valid => Available
        && DaySkyActive
        && AmbientEnergy >= 1.05f
        && Exposure >= 1.12f
        && BackgroundEnergy >= 1.0f
        && Brightness >= 1.05f
        && Contrast <= 1.02f
        && FogDensity <= 0.001f
        && !VolumetricFogEnabled
        && SunEnergy >= 1.30f
        && FillEnergy >= 0.60f;
}

/// <summary>
/// Owns the fixed competitive lighting profile used by every demolition arena.
/// It deliberately overwrites all time-of-day-sensitive values so an extraction
/// deployment selection cannot leak dark sky, exposure, or ambient light into PvP.
/// </summary>
internal static class DemolitionLightingProfile
{
    private const float AmbientEnergy = 1.14f;
    private const float Exposure = 1.18f;
    private const float BackgroundEnergy = 1.06f;
    private const float Brightness = 1.08f;
    private const float Contrast = 1.0f;
    private const float Saturation = 1.04f;
    private const float FogDensity = 0.00065f;
    private const float SunEnergy = 1.42f;
    private const float FillEnergy = 0.68f;

    public static void Apply(
        Godot.Environment environment,
        DirectionalLight3D sunLight,
        DirectionalLight3D fillLight,
        Material daySkyMaterial,
        int qualitySetting)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(sunLight);
        ArgumentNullException.ThrowIfNull(fillLight);
        ArgumentNullException.ThrowIfNull(daySkyMaterial);

        environment.BackgroundMode = Godot.Environment.BGMode.Sky;
        environment.BackgroundEnergyMultiplier = BackgroundEnergy;
        environment.AmbientLightSource = Godot.Environment.AmbientSource.Sky;
        environment.AmbientLightEnergy = AmbientEnergy;
        environment.AmbientLightSkyContribution = 0.78f;
        environment.ReflectedLightSource = Godot.Environment.ReflectionSource.Sky;
        environment.TonemapMode = Godot.Environment.ToneMapper.Aces;
        environment.TonemapExposure = Exposure;
        environment.AdjustmentEnabled = true;
        environment.AdjustmentBrightness = Brightness;
        environment.AdjustmentContrast = Contrast;
        environment.AdjustmentSaturation = Saturation;
        environment.FogEnabled = true;
        environment.FogLightColor = new Color(0.53f, 0.63f, 0.68f);
        environment.FogLightEnergy = 0.58f;
        environment.FogDensity = FogDensity;
        environment.FogSkyAffect = 0.10f;
        environment.VolumetricFogEnabled = false;

        var sky = environment.Sky ?? new Sky();
        sky.SkyMaterial = daySkyMaterial;
        sky.ProcessMode = Mathf.Clamp(qualitySetting, 0, 2) >= 2
            ? Sky.ProcessModeEnum.Realtime
            : Sky.ProcessModeEnum.Incremental;
        environment.Sky = sky;

        sunLight.RotationDegrees = new Vector3(-46.0f, -32.0f, 0.0f);
        sunLight.LightColor = new Color(1.0f, 0.93f, 0.82f);
        sunLight.LightEnergy = SunEnergy;
        sunLight.ShadowBias = 0.055f;
        sunLight.ShadowNormalBias = 1.9f;
        sunLight.ShadowTransmittanceBias = 0.05f;
        sunLight.ShadowBlur = 0.65f;

        fillLight.RotationDegrees = new Vector3(-28.0f, 142.0f, 0.0f);
        fillLight.LightColor = new Color(0.48f, 0.62f, 0.82f);
        fillLight.LightEnergy = FillEnergy;
        fillLight.ShadowEnabled = false;
    }

    public static DemolitionLightingInspection Inspect(
        Godot.Environment? environment,
        DirectionalLight3D? sunLight,
        DirectionalLight3D? fillLight)
    {
        if (!IsValid(environment) || !IsValid(sunLight) || !IsValid(fillLight))
        {
            return default;
        }

        return new DemolitionLightingInspection(
            true,
            environment!.BackgroundMode == Godot.Environment.BGMode.Sky
                && environment.Sky?.SkyMaterial is ShaderMaterial,
            environment.AmbientLightEnergy,
            environment.TonemapExposure,
            environment.BackgroundEnergyMultiplier,
            environment.AdjustmentBrightness,
            environment.AdjustmentContrast,
            environment.FogDensity,
            environment.VolumetricFogEnabled,
            sunLight!.LightEnergy,
            fillLight!.LightEnergy);
    }

    private static bool IsValid(GodotObject? instance)
        => instance is not null && GodotObject.IsInstanceValid(instance);
}

public partial class FreightTerminalWorld
{
    private void ApplyDemolitionLighting()
    {
        if (!IsInstanceValid(_environmentRef)
            || !IsInstanceValid(_sunLight)
            || !IsInstanceValid(_fillLight))
        {
            throw new InvalidOperationException(
                "Demolition lighting requires the world environment and both directional lights.");
        }

        DemolitionLightingProfile.Apply(
            _environmentRef,
            _sunLight,
            _fillLight,
            BuildDynamicSkyMaterial(DeploymentTimeOfDay.Day),
            _qualitySetting);
    }

    internal DemolitionLightingInspection InspectDemolitionLightingForDiagnostics()
        => DemolitionLightingProfile.Inspect(_environmentRef, _sunLight, _fillLight);
}
