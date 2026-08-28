using Godot;

namespace OperationSteelTide;

/// <summary>Applies the authored Jianghai old-city lighting and weather treatment.</summary>
internal sealed class JianghaiOldCityAtmosphere
{
    private Godot.Environment? _boundEnvironment;
    private Sky? _sky;
    private ProceduralSkyMaterial? _proceduralSkyMaterial;

    public void ReleaseReferences()
    {
        _boundEnvironment = null;
        _sky = null;
        _proceduralSkyMaterial = null;
    }

    public void Apply(
        bool active,
        DeploymentTimeOfDay timeOfDay,
        int qualitySetting,
        Godot.Environment? environment,
        DirectionalLight3D? sunLight,
        DirectionalLight3D? fillLight)
    {
        if (!active
            || environment is null
            || !GodotObject.IsInstanceValid(environment))
        {
            return;
        }

        var style = GetStyle(timeOfDay);
        var sky = EnsureSky(environment);
        ApplyProceduralSky(sky, style);
        environment.BackgroundMode = Godot.Environment.BGMode.Sky;
        environment.AmbientLightSource = Godot.Environment.AmbientSource.Sky;
        environment.ReflectedLightSource = Godot.Environment.ReflectionSource.Sky;
        environment.SkyRotation = Vector3.Zero;
        environment.BackgroundEnergyMultiplier = 1.0f;

        var highQuality = Mathf.Clamp(qualitySetting, 0, 2) >= 2;
        sky.ProcessMode = highQuality
            ? Sky.ProcessModeEnum.Realtime
            : Sky.ProcessModeEnum.Incremental;

        environment.AmbientLightEnergy = style.AmbientEnergy;
        environment.FogLightColor = style.FogColor;
        environment.FogLightEnergy = style.FogEnergy;
        environment.FogDensity = style.FogDensity;
        var fullDaylight = timeOfDay == DeploymentTimeOfDay.Day;
        environment.FogSkyAffect = fullDaylight ? 0.08f : 0.18f;
        environment.TonemapExposure = style.Exposure;
        SetIfSupported(
            environment,
            "ambient_light_sky_contribution",
            fullDaylight ? 0.92f : 0.55f);
        SetIfSupported(environment, "adjustment_enabled", true);
        SetIfSupported(environment, "adjustment_brightness", style.Brightness);
        SetIfSupported(environment, "adjustment_contrast", style.Contrast);
        SetIfSupported(environment, "adjustment_saturation", style.Saturation);
        SetIfSupported(environment, "volumetric_fog_enabled", highQuality);
        SetIfSupported(environment, "volumetric_fog_density", style.VolumetricDensity);
        SetIfSupported(environment, "volumetric_fog_ambient_inject", 0.30f);

        if (sunLight is not null && GodotObject.IsInstanceValid(sunLight))
        {
            sunLight.LightColor = style.SunColor;
            sunLight.LightEnergy = style.SunEnergy;
        }

        if (fillLight is not null && GodotObject.IsInstanceValid(fillLight))
        {
            fillLight.LightColor = style.FillColor;
            fillLight.LightEnergy = style.FillEnergy;
        }
    }

    private Sky EnsureSky(Godot.Environment environment)
    {
        var environmentChanged = !IsSameInstance(_boundEnvironment, environment);
        var environmentSky = environment.Sky;
        if (environmentChanged || !IsSameInstance(_sky, environmentSky))
        {
            _boundEnvironment = environment;
            _sky = IsValid(environmentSky) ? environmentSky : new Sky();
            environment.Sky = _sky;
            _proceduralSkyMaterial = _sky.SkyMaterial as ProceduralSkyMaterial;
        }

        return _sky!;
    }

    private void ApplyProceduralSky(Sky sky, JianghaiAtmosphereStyle style)
    {
        if (!IsValid(_proceduralSkyMaterial))
        {
            _proceduralSkyMaterial = new ProceduralSkyMaterial();
        }

        var skyMaterial = _proceduralSkyMaterial!;
        skyMaterial.SkyTopColor = style.SkyTop;
        skyMaterial.SkyHorizonColor = style.SkyHorizon;
        skyMaterial.SkyCurve = 0.22f;
        skyMaterial.SkyEnergyMultiplier = style.SkyEnergy;
        skyMaterial.GroundBottomColor = style.GroundBottom;
        skyMaterial.GroundHorizonColor = style.SkyHorizon;
        skyMaterial.GroundCurve = 0.18f;
        skyMaterial.GroundEnergyMultiplier = style.SkyEnergy * 0.55f;
        skyMaterial.SunAngleMax = 5.5f;
        skyMaterial.SunCurve = 0.10f;
        skyMaterial.UseDebanding = true;
        if (!IsSameInstance(sky.SkyMaterial, skyMaterial))
        {
            sky.SkyMaterial = skyMaterial;
        }
    }

    private static JianghaiAtmosphereStyle GetStyle(DeploymentTimeOfDay timeOfDay)
        => timeOfDay switch
        {
            DeploymentTimeOfDay.Night => new JianghaiAtmosphereStyle(
                new Color(0.001f, 0.003f, 0.009f),
                new Color(0.008f, 0.014f, 0.026f),
                new Color(0.002f, 0.003f, 0.005f),
                new Color(0.010f, 0.014f, 0.020f),
                new Color(0.025f, 0.038f, 0.060f),
                new Color(0.20f, 0.27f, 0.40f),
                new Color(0.08f, 0.12f, 0.22f),
                0.34f,
                0.12f,
                0.08f,
                0.0046f,
                0.72f,
                0.82f,
                1.16f,
                0.80f,
                0.0075f,
                0.06f,
                0.04f),
            DeploymentTimeOfDay.Dusk => new JianghaiAtmosphereStyle(
                new Color(0.015f, 0.050f, 0.115f),
                new Color(0.085f, 0.170f, 0.225f),
                new Color(0.018f, 0.035f, 0.060f),
                new Color(0.075f, 0.125f, 0.160f),
                new Color(0.075f, 0.110f, 0.145f),
                new Color(1.000f, 0.560f, 0.320f),
                new Color(0.120f, 0.250f, 0.420f),
                0.62f,
                0.78f,
                0.15f,
                0.00120f,
                0.95f,
                0.99f,
                1.10f,
                1.10f,
                0.0024f,
                0.62f,
                0.32f),
            DeploymentTimeOfDay.Dawn => new JianghaiAtmosphereStyle(
                new Color(0.022f, 0.038f, 0.062f),
                new Color(0.15f, 0.12f, 0.12f),
                new Color(0.012f, 0.017f, 0.023f),
                new Color(0.068f, 0.068f, 0.074f),
                new Color(0.18f, 0.17f, 0.18f),
                new Color(1.0f, 0.55f, 0.31f),
                new Color(0.18f, 0.24f, 0.34f),
                0.62f,
                0.40f,
                0.26f,
                0.0034f,
                0.82f,
                0.82f,
                1.14f,
                0.92f,
                0.0070f,
                0.46f,
                0.11f),
            _ => new JianghaiAtmosphereStyle(
                new Color(0.12f, 0.36f, 0.68f),
                new Color(0.55f, 0.72f, 0.86f),
                new Color(0.09f, 0.12f, 0.14f),
                new Color(0.40f, 0.48f, 0.52f),
                new Color(0.55f, 0.68f, 0.76f),
                new Color(0.98f, 0.90f, 0.78f),
                new Color(0.55f, 0.66f, 0.82f),
                1.00f,
                1.18f,
                0.38f,
                0.0010f,
                0.98f,
                1.02f,
                0.98f,
                1.04f,
                0.0022f,
                1.03f,
                0.62f)
        };

    private static void SetIfSupported(GodotObject target, string propertyName, Variant value)
    {
        foreach (var property in target.GetPropertyList())
        {
            if (property["name"].AsString() != propertyName)
            {
                continue;
            }

            target.Set(propertyName, value);
            return;
        }
    }

    private static bool IsValid(GodotObject? instance)
        => instance is not null && GodotObject.IsInstanceValid(instance);

    private static bool IsSameInstance(GodotObject? left, GodotObject? right)
        => IsValid(left)
            && IsValid(right)
            && left!.GetInstanceId() == right!.GetInstanceId();

    private readonly record struct JianghaiAtmosphereStyle(
        Color SkyTop,
        Color SkyHorizon,
        Color GroundBottom,
        Color GroundHorizon,
        Color FogColor,
        Color SunColor,
        Color FillColor,
        float SkyEnergy,
        float AmbientEnergy,
        float FogEnergy,
        float FogDensity,
        float Exposure,
        float Brightness,
        float Contrast,
        float Saturation,
        float VolumetricDensity,
        float SunEnergy,
        float FillEnergy);
}
