using Godot;

namespace OperationSteelTide;

[GlobalClass]
public partial class FlashbangOverlayView : Control
{
    internal const float ScreenOpacityScale = 1.12f;
    internal const float ScreenOpacityBias = 0.12f;
    internal const float MaximumPeakHoldSeconds = 0.32f;

    private ColorRect _wash = null!;
    private Tween? _fadeTween;
    private ulong _exposureEndsAtMsec;

    public bool UiReady { get; private set; }
    public float DisplayedAlpha => UiReady ? _wash.Color.A : 0.0f;
    public bool IsExposureVisible => UiReady && Visible && _wash.Visible;
    public float RemainingSeconds
    {
        get
        {
            var now = Time.GetTicksMsec();
            return IsExposureVisible && _exposureEndsAtMsec > now
                ? (_exposureEndsAtMsec - now) / 1000.0f
                : 0.0f;
        }
    }

    public override void _Ready()
    {
        _wash = GetNode<ColorRect>("%Wash");
        UiReady = true;
        ClearExposure();
    }

    public void ShowExposure(float intensity, float durationSeconds)
    {
        if (!UiReady || intensity <= 0.01f || durationSeconds <= 0.0f)
        {
            return;
        }

        var now = Time.GetTicksMsec();
        var requestedEnd = now + (ulong)Mathf.CeilToInt(durationSeconds * 1000.0f);
        var mergedEnd = System.Math.Max(_exposureEndsAtMsec, requestedEnd);
        var mergedAlpha = Mathf.Max(DisplayedAlpha, ResolveScreenAlpha(intensity));
        var mergedDuration = Mathf.Max(0.04f, (mergedEnd - now) / 1000.0f);

        _fadeTween?.Kill();
        _fadeTween = null;
        _exposureEndsAtMsec = mergedEnd;
        Visible = true;
        _wash.Visible = true;
        _wash.Color = new Color(
            1.0f,
            1.0f,
            1.0f,
            mergedAlpha);

        var holdSeconds = Mathf.Min(MaximumPeakHoldSeconds, mergedDuration * 0.16f);
        _fadeTween = CreateTween();
        _fadeTween.TweenInterval(holdSeconds);
        _fadeTween.TweenProperty(
                _wash,
                "color:a",
                0.0f,
                Mathf.Max(0.04f, mergedDuration - holdSeconds))
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.In);
        _fadeTween.TweenCallback(Callable.From(FinishExposure));
    }

    internal static float ResolveScreenAlpha(float intensity)
        => Mathf.Clamp(
            intensity * ScreenOpacityScale + ScreenOpacityBias,
            0.0f,
            1.0f);

    public void ClearExposure()
    {
        _fadeTween?.Kill();
        _fadeTween = null;
        _exposureEndsAtMsec = 0;
        if (!UiReady)
        {
            return;
        }

        _wash.Visible = false;
        _wash.Color = new Color(1.0f, 1.0f, 1.0f, 0.0f);
        Visible = false;
    }

    private void FinishExposure()
    {
        _fadeTween = null;
        _exposureEndsAtMsec = 0;
        if (!UiReady)
        {
            return;
        }

        _wash.Visible = false;
        _wash.Color = new Color(1.0f, 1.0f, 1.0f, 0.0f);
        Visible = false;
    }
}
