using Godot;

namespace OperationSteelTide;

public partial class CombatHUD
{
    private static readonly Vector2 CrosshairAnchorOffset = new(-1.0f, -1.0f);

    public void PulseCrosshair(
        float shotImpact = 1.0f,
        float horizontalRecoil = 0.0f)
    {
        if (_crosshairTween?.IsRunning() == true)
        {
            _crosshairTween.Kill();
        }

        var intensity = Mathf.Clamp((shotImpact - 0.55f) / 1.35f, 0.0f, 1.0f);
        var kickPixels = Mathf.Lerp(2.4f, 4.8f, intensity);
        var lateralPixels = Mathf.Clamp(
            horizontalRecoil * 150.0f,
            -4.5f,
            4.5f);
        var restPosition = CrosshairRestPositionInParent();
        _crosshair.Position = restPosition
            + new Vector2(lateralPixels, -kickPixels);
        _crosshair.Scale = Vector2.One * Mathf.Lerp(1.72f, 2.12f, intensity);
        _crosshair.Rotation = Mathf.Clamp(
            -horizontalRecoil * 3.4f,
            -0.09f,
            0.09f);

        var recoveryDuration = Mathf.Lerp(0.14f, 0.19f, intensity);
        _crosshairTween = CreateTween()
            .SetProcessMode(Tween.TweenProcessMode.Physics);
        _crosshairTween.TweenProperty(
                _crosshair,
                "position",
                restPosition,
                recoveryDuration)
            .SetTrans(Tween.TransitionType.Expo)
            .SetEase(Tween.EaseType.Out);
        _crosshairTween.Parallel().TweenProperty(
                _crosshair,
                "scale",
                Vector2.One,
                recoveryDuration)
            .SetTrans(Tween.TransitionType.Expo)
            .SetEase(Tween.EaseType.Out);
        _crosshairTween.Parallel().TweenProperty(
                _crosshair,
                "rotation",
                0.0f,
                recoveryDuration)
            .SetTrans(Tween.TransitionType.Expo)
            .SetEase(Tween.EaseType.Out);
    }

    internal CrosshairShotFeedbackInspection InspectCrosshairShotFeedbackForDiagnostics()
        => IsInstanceValid(_crosshair)
            ? new CrosshairShotFeedbackInspection(
                true,
                _crosshair.Position - CrosshairRestPositionInParent(),
                _crosshair.Scale,
                _crosshair.Rotation)
            : default;

    internal void ResetCrosshairShotFeedbackForDiagnostics()
    {
        if (_crosshairTween?.IsRunning() == true)
        {
            _crosshairTween.Kill();
        }
        _crosshair.Position = CrosshairRestPositionInParent();
        _crosshair.Scale = Vector2.One;
        _crosshair.Rotation = 0.0f;
    }

    private Vector2 CrosshairRestPositionInParent()
        => _crosshair.GetParent() is Control parent
            ? parent.Size * 0.5f + CrosshairAnchorOffset
            : CrosshairAnchorOffset;
}

internal readonly record struct CrosshairShotFeedbackInspection(
    bool Available,
    Vector2 Offset,
    Vector2 Scale,
    float Rotation);
