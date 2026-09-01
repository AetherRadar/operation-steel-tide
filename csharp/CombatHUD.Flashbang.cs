using Godot;

namespace OperationSteelTide;

public partial class CombatHUD
{
    private const string FlashbangOverlayViewScenePath = "res://ui/FlashbangOverlayView.tscn";
    private FlashbangOverlayView _flashbangOverlayView = null!;

    internal bool FlashbangOverlayUsesPackedScene
        => IsInstanceValid(_flashbangOverlayView)
            && _flashbangOverlayView.SceneFilePath == FlashbangOverlayViewScenePath;

    internal bool FlashbangOverlayUiReady
        => IsInstanceValid(_flashbangOverlayView) && _flashbangOverlayView.UiReady;

    internal bool IsFlashbangOverlayVisible
        => IsInstanceValid(_flashbangOverlayView) && _flashbangOverlayView.IsExposureVisible;

    internal float FlashbangOverlayAlphaForDiagnostics
        => IsInstanceValid(_flashbangOverlayView) ? _flashbangOverlayView.DisplayedAlpha : 0.0f;

    internal float FlashbangOverlayRemainingForDiagnostics
        => IsInstanceValid(_flashbangOverlayView) ? _flashbangOverlayView.RemainingSeconds : 0.0f;

    private void BuildFlashbangOverlay(Control root)
    {
        _flashbangOverlayView = HudPackedSceneCache.Instantiate<FlashbangOverlayView>(
            FlashbangOverlayViewScenePath);
        root.AddChild(_flashbangOverlayView);
    }

    public void ShowFlashbangExposure(float intensity, float durationSeconds)
        => _flashbangOverlayView.ShowExposure(intensity, durationSeconds);

    public void ClearFlashbangExposure()
        => _flashbangOverlayView.ClearExposure();
}
