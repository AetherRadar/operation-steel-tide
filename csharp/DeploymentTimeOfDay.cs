using Godot;

namespace OperationSteelTide;

/// <summary>Deploy-time time of day: same map, different light, detection, and mood.</summary>
public enum DeploymentTimeOfDay
{
    Day = 0,
    Dusk = 1,
    Night = 2,
    Dawn = 3
}

public readonly record struct TimeOfDayStyle(
    Vector3 SunRotationDegrees,
    Color SunColor,
    float SunEnergy,
    float AmbientEnergy,
    Color FogColor,
    float FogEnergy,
    float FogDensity,
    float Exposure,
    float DetectionMultiplier);

public static class TimeOfDayStyles
{
    public static readonly TimeOfDayStyle Day = new(
        new Vector3(-48.0f, -28.0f, 0.0f),
        new Color(1.0f, 0.9f, 0.72f),
        1.25f,
        0.86f,
        new Color(0.46f, 0.58f, 0.62f),
        0.46f,
        0.00125f,
        1.10f,
        1.0f);

    public static readonly TimeOfDayStyle Dusk = new(
        new Vector3(-11.0f, -64.0f, 0.0f),
        new Color(1.0f, 0.55f, 0.28f),
        0.88f,
        0.6f,
        new Color(0.52f, 0.38f, 0.34f),
        0.56f,
        0.00165f,
        1.04f,
        0.92f);

    public static readonly TimeOfDayStyle Night = new(
        new Vector3(-38.0f, 152.0f, 0.0f),
        new Color(0.52f, 0.62f, 0.85f),
        0.34f,
        0.3f,
        new Color(0.13f, 0.17f, 0.24f),
        0.3f,
        0.0019f,
        0.94f,
        0.74f);

    public static readonly TimeOfDayStyle Dawn = new(
        new Vector3(-8.0f, 62.0f, 0.0f),
        new Color(0.95f, 0.82f, 0.66f),
        0.8f,
        0.54f,
        new Color(0.48f, 0.53f, 0.6f),
        0.44f,
        0.00145f,
        1.06f,
        0.88f);

    public static TimeOfDayStyle Style(DeploymentTimeOfDay timeOfDay) => timeOfDay switch
    {
        DeploymentTimeOfDay.Dusk => Dusk,
        DeploymentTimeOfDay.Night => Night,
        DeploymentTimeOfDay.Dawn => Dawn,
        _ => Day
    };

    public static string DisplayName(DeploymentTimeOfDay timeOfDay, string language)
    {
        var chinese = GameLocalization.IsChinese(language);
        return timeOfDay switch
        {
            DeploymentTimeOfDay.Dusk => chinese ? "\u9ec4\u660f" : "DUSK",
            DeploymentTimeOfDay.Night => chinese ? "\u591c\u95f4" : "NIGHT",
            DeploymentTimeOfDay.Dawn => chinese ? "\u62c2\u6653" : "DAWN",
            _ => chinese ? "\u767d\u663c" : "DAY"
        };
    }
}
