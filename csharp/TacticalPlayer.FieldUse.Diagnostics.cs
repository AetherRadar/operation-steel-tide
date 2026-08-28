using Godot;

namespace OperationSteelTide;

public partial class TacticalPlayer
{
    internal bool SetMedicalUsePoseForDiagnostics(MedicalItemKind kind, float progress)
    {
        if (!MedicalActionBlocksWeapon || _medicalActionKind != kind)
        {
            return false;
        }
        var clamped = Mathf.Clamp(progress, 0.0f, 0.995f);
        _medicalActionRemaining = _medicalActionDuration * (1.0f - clamped);
        SetMedicalDeviceVisibility();
        return true;
    }

    internal bool SetPlateUsePoseForDiagnostics(float progress)
    {
        if (!_isPlating)
        {
            return false;
        }
        var clamped = Mathf.Clamp(progress, 0.0f, 0.995f);
        _plateTime = _plateDuration * (1.0f - clamped);
        SetMedicalDeviceVisibility();
        return true;
    }

    internal FieldUsePresentationInspection InspectFieldUsePresentationForDiagnostics()
        => _fieldUsePresentation?.Inspect()
            ?? new FieldUsePresentationInspection(
                false,
                false,
                FirstPersonFieldUsePresentationKind.Bandage,
                0.0f,
                false,
                false,
                false,
                false,
                false,
                false,
                float.PositiveInfinity,
                float.PositiveInfinity,
                float.PositiveInfinity,
                float.PositiveInfinity,
                0.0f,
                0.0f,
                0.0f,
                0.0f);

    internal bool FirstPersonWeaponVisibleForDiagnostics
        => IsInstanceValid(_weaponRoot) && _weaponRoot.Visible;

    internal void ClearFieldUsePoseForDiagnostics()
    {
        CancelFieldUse(false);
    }
}
