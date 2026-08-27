using Godot;

namespace OperationSteelTide;

/// <summary>Applies the authored Jianghai old-city lighting and weather treatment.</summary>
internal sealed class JianghaiOldCityAtmosphere
{
    private Godot.Environment? _boundEnvironment;
    private Sky? _sky;
    private ProceduralSkyMaterial? _skyMaterial;

    public void ReleaseReferences()
    {
        _boundEnvironment = null;
        _sky = null;
        _skyMaterial = null;
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
        var skyMaterial = EnsureSkyMaterial(environment);
        skyMaterial.SkyTopColor = style.SkyTop;
        skyMaterial.SkyHorizonColor = style.SkyHorizon;
        skyMaterial.SkyCurve = 0.22f;
        skyMaterial.SkyEnergyMultiplier = style.SkyEnergy;
        skyMaterial.GroundBottomColor = style.GroundBottom;
        skyMaterial.GroundHorizonColor = style.GroundHorizon;
        skyMaterial.GroundCurve = 0.18f;
        skyMaterial.GroundEnergyMultiplier = style.SkyEnergy * 0.55f;
        skyMaterial.SunAngleMax = 5.5f;
        skyMaterial.SunCurve = 0.10f;
        skyMaterial.UseDebanding = true;

        var highQuality = Mathf.Clamp(qualitySetting, 0, 2) >= 2;
        _sky!.ProcessMode = highQuality
            ? Sky.ProcessModeEnum.Realtime
            : Sky.ProcessModeEnum.Incremental;

        environment.AmbientLightEnergy = style.AmbientEnergy;
        environment.FogLightColor = style.FogColor;
        environment.FogLightEnergy = style.FogEnergy;
        environment.FogDensity = style.FogDensity;
        environment.FogSkyAffect = 0.42f;
        environment.TonemapExposure = style.Exposure;
        SetIfSupported(environment, "adjustment_enabled", true);
        SetIfSupported(environment, "adjustment_brightness", style.Brightness);
        SetIfSupported(environment, "adjustment_contrast", style.Contrast);
        SetIfSupported(environment, "adjustment_saturation", style.Saturation);
        SetIfSupported(environment, "volumetric_fog_enabled", highQuality);
        SetIfSupported(environment, "volumetric_fog_density", style.VolumetricDensity);
        SetIfSupported(environment, "volumetric_fog_ambient_inject", 0.42f);

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

    private ProceduralSkyMaterial EnsureSkyMaterial(Godot.Environment environment)
    {
        var environmentChanged = !IsSameInstance(_boundEnvironment, environment);
        var environmentSky = environment.Sky;
        if (environmentChanged || !IsSameInstance(_sky, environmentSky))
        {
            _boundEnvironment = environment;
            _sky = IsValid(environmentSky) ? environmentSky : new Sky();
            environment.Sky = _sky;
            _skyMaterial = _sky.SkyMaterial as ProceduralSkyMaterial;
        }

        if (!IsValid(_skyMaterial))
        {
            _skyMaterial = new ProceduralSkyMaterial();
        }

        if (!IsSameInstance(_sky!.SkyMaterial, _skyMaterial))
        {
            _sky.SkyMaterial = _skyMaterial;
        }

        return _skyMaterial!;
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
                new Color(0.040f, 0.080f, 0.140f),
                new Color(0.20f, 0.21f, 0.24f),
                new Color(0.025f, 0.035f, 0.050f),
                new Color(0.10f, 0.11f, 0.13f),
                new Color(0.22f, 0.20f, 0.23f),
                new Color(1.0f, 0.34f, 0.16f),
                new Color(0.14f, 0.19f, 0.31f),
                0.78f,
                0.58f,
                0.34f,
                0.0034f,
                0.93f,
                0.96f,
                1.10f,
                1.02f,
                0.0068f,
                0.48f,
                0.14f),
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
                new Color(0.12f, 0.22f, 0.32f),
                new Color(0.38f, 0.45f, 0.48f),
                new Color(0.055f, 0.075f, 0.085f),
                new Color(0.20f, 0.25f, 0.26f),
                new Color(0.34f, 0.42f, 0.43f),
                new Color(0.78f, 0.86f, 0.90f),
                new Color(0.28f, 0.39f, 0.50f),
                0.94f,
                0.98f,
                0.52f,
                0.0024f,
                0.98f,
                0.98f,
                1.08f,
                0.90f,
                0.0048f,
                0.84f,
                0.25f)
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
