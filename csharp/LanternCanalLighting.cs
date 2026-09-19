using System;
using Godot;

namespace OperationSteelTide;

/// <summary>Owns the city import's light budget and the canal deployment atmosphere.</summary>
internal static class LanternCanalLighting
{
    private const float ImportedPointEnergyScale = 0.02f;
    private const float MaximumPointEnergy = 2.2f;
    private const float MaximumPointRange = 8.0f;

    /// <summary>Configures a newly instantiated authored city exactly once, before it is shown.</summary>
    public static void ConfigureCity(Node3D city)
    {
        ArgumentNullException.ThrowIfNull(city);
        ConfigureNode(city, 0);
    }

    private static void ConfigureNode(Node node, int depth)
    {
        if (node is GeometryInstance3D geometry)
        {
            // The city needs its authored roofs and walls to occlude direct sun.
            // Culling and the world light's bounded shadow distance control cost.
            geometry.CastShadow = GeometryInstance3D.ShadowCastingSetting.On;
            geometry.VisibilityRangeEnd = depth <= 2 ? 260.0f : 190.0f;
        }

        if (node is DirectionalLight3D directional)
        {
            // The GLB's preview sun (2.5) and sky fill (0.7) otherwise stack with
            // the world lights and remain fully lit even during night deployment.
            directional.Visible = false;
        }
        else if (node is OmniLight3D point)
        {
            // The glTF import transfers 18–115 directly into Godot light energy.
            // Retain the authored relative lamp strengths without flooding rooms.
            point.LightEnergy = Mathf.Clamp(
                point.LightEnergy * ImportedPointEnergyScale, 0.35f, MaximumPointEnergy);
            point.OmniRange = Mathf.Min(point.OmniRange, MaximumPointRange);
            point.DistanceFadeEnabled = true;
            point.DistanceFadeBegin = 36.0f;
            point.DistanceFadeLength = 12.0f;
            point.ShadowEnabled = false;
        }

        foreach (var child in node.GetChildren())
        {
            ConfigureNode(child, depth + 1);
        }
    }

    public static void Apply(
        DeploymentTimeOfDay timeOfDay,
        int qualitySetting,
        Godot.Environment environment,
        DirectionalLight3D sunLight,
        DirectionalLight3D fillLight)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(sunLight);
        ArgumentNullException.ThrowIfNull(fillLight);

        var lighting = timeOfDay switch
        {
            DeploymentTimeOfDay.Night => new Lighting(0.24f, 0.36f, 0.90f, 0.06f, 0.0012f, 0.0018f),
            DeploymentTimeOfDay.Dusk => new Lighting(0.48f, 0.22f, 0.90f, 0.08f, 0.0010f, 0.0018f),
            DeploymentTimeOfDay.Dawn => new Lighting(0.40f, 0.18f, 0.86f, 0.07f, 0.0011f, 0.0020f),
            _ => new Lighting(0.95f, 0.52f, 0.90f, 0.10f, 0.00075f, 0.0011f)
        };
        var night = timeOfDay == DeploymentTimeOfDay.Night;
        var day = timeOfDay == DeploymentTimeOfDay.Day;
        var quality = Mathf.Clamp(qualitySetting, 0, 2);

        environment.BackgroundEnergyMultiplier = day ? 0.90f : 1.0f;
        // The near-black night sky is a background, not a usable irradiance source.
        // A restrained cool ambient term preserves walkways under the deep eaves.
        environment.AmbientLightSource = night
            ? Godot.Environment.AmbientSource.Color
            : Godot.Environment.AmbientSource.Sky;
        environment.AmbientLightColor = night ? new Color(0.38f, 0.44f, 0.56f) : Colors.White;
        environment.AmbientLightSkyContribution = night ? 0.0f : 0.75f;
        environment.AmbientLightEnergy = lighting.AmbientEnergy;
        environment.ReflectedLightSource = Godot.Environment.ReflectionSource.Sky;
        environment.TonemapMode = Godot.Environment.ToneMapper.Aces;
        environment.TonemapExposure = lighting.Exposure;
        environment.AdjustmentEnabled = true;
        environment.AdjustmentBrightness = night ? 0.96f : 1.0f;
        environment.AdjustmentContrast = night ? 1.02f : 1.06f;
        environment.AdjustmentSaturation = night ? 0.92f : 1.0f;
        environment.FogDensity = lighting.FogDensity;
        environment.FogLightEnergy = night ? 0.08f : 0.30f;
        environment.FogSkyAffect = 0.08f;
        environment.VolumetricFogEnabled = quality >= 2;
        environment.VolumetricFogDensity = lighting.VolumetricDensity;
        environment.VolumetricFogAmbientInject = 0.20f;
        environment.GlowEnabled = quality >= 1;
        environment.GlowIntensity = 0.35f;
        environment.GlowBloom = 0.02f;

        sunLight.LightEnergy = lighting.SunEnergy;
        if (day)
        {
            sunLight.LightColor = new Color(1.0f, 0.95f, 0.87f);
        }
        else if (night)
        {
            sunLight.LightColor = new Color(0.58f, 0.68f, 0.84f);
        }
        sunLight.DirectionalShadowMaxDistance = quality switch
        {
            0 => 60.0f,
            1 => 90.0f,
            _ => 120.0f
        };
        fillLight.LightEnergy = lighting.FillEnergy;
        if (day)
        {
            fillLight.LightColor = new Color(0.55f, 0.66f, 0.82f);
        }
        else if (night)
        {
            fillLight.LightColor = new Color(0.32f, 0.40f, 0.54f);
        }
    }

    private readonly record struct Lighting(
        float SunEnergy,
        float AmbientEnergy,
        float Exposure,
        float FillEnergy,
        float FogDensity,
        float VolumetricDensity);
}
