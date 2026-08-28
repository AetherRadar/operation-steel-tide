using Godot;

namespace OperationSteelTide;

public partial class OperationsOfficeBackdrop
{
    public float CameraOffsetForDiagnostics
        => IsInstanceValid(_camera)
            ? _camera.Position.DistanceTo(_neutralCameraPosition)
            : 0.0f;
    public Vector2 PointerForDiagnostics => _pointerCurrent;
    public Color AccentForDiagnostics => _quickLight.LightColor.Lerp(
        _demolitionLight.LightColor,
        _demolitionLight.LightEnergy / Mathf.Max(
            0.01f,
            _quickLight.LightEnergy + _demolitionLight.LightEnergy));
    public float QuickLightEnergyForDiagnostics => _quickLight.LightEnergy;
    public float DemolitionLightEnergyForDiagnostics => _demolitionLight.LightEnergy;
    public double PresentationTimeForDiagnostics => _presentationTime;
    public bool WorldEnvironmentSynchronizedForDiagnostics
        => WorldEnvironmentSynchronized;
    public bool PresentationCameraTuningReadyForDiagnostics
        => PresentationCameraTuningReady;

    public void SetPointerForDiagnostics(Vector2 normalizedPointer)
    {
        _diagnosticPointerEnabled = true;
        _diagnosticPointer = ClampPointer(normalizedPointer);
    }

    public void ClearPointerForDiagnostics()
    {
        _diagnosticPointerEnabled = false;
        _diagnosticPointer = Vector2.Zero;
    }

    public void SetPresentationFrozenForDiagnostics(bool frozen)
    {
        _diagnosticPresentationFrozen = frozen;
        foreach (var operatorVisual in _operatorVisuals)
        {
            operatorVisual.ProcessMode = _presentationActive && !frozen
                ? ProcessModeEnum.Inherit
                : ProcessModeEnum.Disabled;
        }
        if (IsInstanceValid(_aircraftVisual))
        {
            _aircraftVisual!.ProcessMode = _presentationActive && !frozen
                ? ProcessModeEnum.Inherit
                : ProcessModeEnum.Disabled;
        }
    }

    public void ResetPresentationForDiagnostics()
    {
        _presentationTime = 0.0;
        _pointerCurrent = Vector2.Zero;
        _pointerTarget = Vector2.Zero;
        _camera.Position = _neutralCameraPosition;
        _currentLookPosition = FramedLookPosition(_neutralLookAnchor);
        _camera.LookAt(ToGlobal(_currentLookPosition), Vector3.Up);
        ResetAccentLighting();
        ResetDecorativeMotion();
    }

    public void AdvancePresentationForDiagnostics(double delta)
        => AdvancePresentation(delta, readViewportPointer: false);
}
