using Godot;

namespace OperationSteelTide;

public partial class OperationsOfficeBackdrop
{
    private const float OfficeTargetCombinedExposure = 1.0f;
    private const float OfficeMinimumExposureMultiplier = 0.88f;
    private const float OfficeMaximumExposureMultiplier = 1.4f;
    private const float DayAmbientEnergyReference = 0.86f;
    private const float DayAmbientFillEnergy = 0.86f;
    private const float NightAmbientFillEnergy = 2.5f;
    private const float DayNeutralKeyEnergy = 0.58f;
    private const float NightNeutralKeyEnergy = 1.9f;

    private Godot.Environment? _cameraEnvironment;
    private Godot.Environment? _worldEnvironmentSource;
    private CameraAttributesPractical _cameraAttributes = null!;
    private float _ambientPresentationEnergy = DayAmbientFillEnergy;
    private float _neutralPresentationEnergy = DayNeutralKeyEnergy;
    private int _qualitySetting = 2;

    private bool WorldEnvironmentSynchronized
    {
        get
        {
            var worldEnvironment = GetWorld3D().Environment;
            return IsInstanceValid(worldEnvironment)
                && IsInstanceValid(_worldEnvironmentSource)
                && _worldEnvironmentSource!.GetInstanceId() == worldEnvironment.GetInstanceId()
                && IsInstanceValid(_cameraEnvironment)
                && _camera.Environment == _cameraEnvironment
                && EnvironmentLightingMatches(worldEnvironment, _cameraEnvironment!)
                && _cameraEnvironment!.BackgroundMode == Godot.Environment.BGMode.Sky
                && _cameraEnvironment.AmbientLightSource == Godot.Environment.AmbientSource.Sky
                && _cameraEnvironment.ReflectedLightSource
                    == Godot.Environment.ReflectionSource.Sky
                && IsInstanceValid(_cameraEnvironment.Sky)
                && _cameraEnvironment.Sky == worldEnvironment.Sky;
        }
    }

    private bool PresentationCameraTuningReady
        => _camera.PhysicsInterpolationMode == PhysicsInterpolationModeEnum.Off
        && _camera.KeepAspect == Camera3D.KeepAspectEnum.Height
        && Mathf.IsEqualApprox(_camera.Fov, 48.0f)
        && _camera.Attributes == _cameraAttributes
        && IsInstanceValid(_worldEnvironmentSource)
        && Mathf.IsEqualApprox(
            _cameraAttributes.ExposureMultiplier,
            ResolveOfficeExposureMultiplier(_worldEnvironmentSource!))
        && !_cameraAttributes.AutoExposureEnabled;

    public void ApplyQuality(int quality)
    {
        _qualitySetting = Mathf.Clamp(quality, 0, 2);
        SynchronizeCameraEnvironment(force: true);
        ApplyCameraEnvironmentQuality();
        _quickLight.ShadowEnabled = _qualitySetting >= 1;
        _demolitionLight.ShadowEnabled = _qualitySetting >= 1;
    }

    private void ConfigureCameraEnvironment()
    {
        // Preserve the world's dynamic sky, reflections, fog and time-of-day grading.
        // Camera attributes and local fills keep the interior readable without
        // flattening the world's day, dusk, dawn, and night grading.
        _camera.Environment = null;
        _cameraAttributes = new CameraAttributesPractical
        {
            ExposureMultiplier = 1.0f,
            AutoExposureEnabled = false
        };
        _camera.Attributes = _cameraAttributes;
        SynchronizeCameraEnvironment(force: true);
    }

    private void SynchronizeCameraEnvironment(bool force = false)
    {
        var worldEnvironment = GetWorld3D().Environment;
        if (!IsInstanceValid(worldEnvironment))
        {
            _worldEnvironmentSource = null;
            _cameraEnvironment = null;
            _camera.Environment = null;
            return;
        }
        UpdateInteriorLightCompensation(worldEnvironment);
        if (!force
            && IsInstanceValid(_worldEnvironmentSource)
            && _worldEnvironmentSource!.GetInstanceId() == worldEnvironment.GetInstanceId()
            && IsInstanceValid(_cameraEnvironment)
            && EnvironmentLightingMatches(worldEnvironment, _cameraEnvironment!))
        {
            return;
        }

        _worldEnvironmentSource = worldEnvironment;
        _cameraEnvironment = worldEnvironment.Duplicate(deep: false) as Godot.Environment;
        if (!IsInstanceValid(_cameraEnvironment))
        {
            _camera.Environment = null;
            return;
        }

        // Keep the Sky resource shared so shader/material and radiance changes made by
        // FreightTerminalWorld remain visible without rebuilding the office environment.
        _cameraEnvironment!.Sky = worldEnvironment.Sky;
        _cameraEnvironment.Set("glow_enabled", true);
        _cameraEnvironment.Set("glow_intensity", 0.38f);
        _cameraEnvironment.Set("glow_bloom", 0.04f);
        _cameraEnvironment.Set("ssao_enabled", true);
        _cameraEnvironment.Set("ssao_radius", 1.35f);
        _cameraEnvironment.Set("ssao_intensity", 1.8f);
        _camera.Environment = _cameraEnvironment;
        ApplyCameraEnvironmentQuality();
    }

    private void UpdateInteriorLightCompensation(Godot.Environment worldEnvironment)
    {
        _cameraAttributes.ExposureMultiplier = ResolveOfficeExposureMultiplier(worldEnvironment);
        var daylightRatio = Mathf.Clamp(
            worldEnvironment.AmbientLightEnergy / DayAmbientEnergyReference,
            0.0f,
            1.0f);
        var darkness = 1.0f - daylightRatio;
        _ambientPresentationEnergy = Mathf.Lerp(
            DayAmbientFillEnergy,
            NightAmbientFillEnergy,
            darkness);
        _neutralPresentationEnergy = Mathf.Lerp(
            DayNeutralKeyEnergy,
            NightNeutralKeyEnergy,
            darkness);
        _neutralKeyLight.LightEnergy = _neutralPresentationEnergy;
    }

    private static float ResolveOfficeExposureMultiplier(Godot.Environment worldEnvironment)
        => Mathf.Clamp(
            OfficeTargetCombinedExposure / Mathf.Max(0.1f, worldEnvironment.TonemapExposure),
            OfficeMinimumExposureMultiplier,
            OfficeMaximumExposureMultiplier);

    private void ApplyCameraEnvironmentQuality()
    {
        if (!IsInstanceValid(_cameraEnvironment))
        {
            return;
        }
        _cameraEnvironment!.Set("glow_enabled", _qualitySetting >= 1);
        _cameraEnvironment.Set("ssao_enabled", _qualitySetting >= 1);
    }

    private static bool EnvironmentLightingMatches(
        Godot.Environment source,
        Godot.Environment cameraEnvironment)
        => source.BackgroundMode == cameraEnvironment.BackgroundMode
        && source.Sky == cameraEnvironment.Sky
        && source.AmbientLightSource == cameraEnvironment.AmbientLightSource
        && ColorsMatch(source.AmbientLightColor, cameraEnvironment.AmbientLightColor)
        && Mathf.IsEqualApprox(source.AmbientLightEnergy, cameraEnvironment.AmbientLightEnergy)
        && source.ReflectedLightSource == cameraEnvironment.ReflectedLightSource
        && source.TonemapMode == cameraEnvironment.TonemapMode
        && Mathf.IsEqualApprox(source.TonemapExposure, cameraEnvironment.TonemapExposure)
        && source.FogEnabled == cameraEnvironment.FogEnabled
        && ColorsMatch(source.FogLightColor, cameraEnvironment.FogLightColor)
        && Mathf.IsEqualApprox(source.FogLightEnergy, cameraEnvironment.FogLightEnergy)
        && Mathf.IsEqualApprox(source.FogDensity, cameraEnvironment.FogDensity)
        && Mathf.IsEqualApprox(source.FogHeight, cameraEnvironment.FogHeight)
        && Mathf.IsEqualApprox(source.FogHeightDensity, cameraEnvironment.FogHeightDensity)
        && Mathf.IsEqualApprox(source.FogSkyAffect, cameraEnvironment.FogSkyAffect)
        && source.AdjustmentEnabled == cameraEnvironment.AdjustmentEnabled
        && Mathf.IsEqualApprox(source.AdjustmentBrightness, cameraEnvironment.AdjustmentBrightness)
        && Mathf.IsEqualApprox(source.AdjustmentContrast, cameraEnvironment.AdjustmentContrast)
        && Mathf.IsEqualApprox(source.AdjustmentSaturation, cameraEnvironment.AdjustmentSaturation)
        && Mathf.IsEqualApprox(
            source.VolumetricFogDensity,
            cameraEnvironment.VolumetricFogDensity);

    private static bool ColorsMatch(Color first, Color second)
        => Mathf.IsEqualApprox(first.R, second.R)
        && Mathf.IsEqualApprox(first.G, second.G)
        && Mathf.IsEqualApprox(first.B, second.B)
        && Mathf.IsEqualApprox(first.A, second.A);
}
