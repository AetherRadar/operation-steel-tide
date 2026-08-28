using System;
using Godot;

namespace OperationSteelTide;

public partial class TacticalPlayer
{
    private AuthoredOpticsVisual _authoredOptics = null!;

    private static bool WeaponUsesIntegratedOptic(
        WeaponPlatform platform,
        string? opticId)
        => (platform == WeaponPlatform.M4A1 && opticId == "optic_micro")
            // VSS micro/holo attachments are rejected by WeaponCatalog. Treat
            // any legacy or externally restored VSS optic state as integrated
            // as a defensive fallback so its inseparable authored PSO scope can
            // never be stacked with a second visible housing.
            || (platform == WeaponPlatform.VSS && opticId is not null);

    private void InitializeAuthoredOptics()
    {
        try
        {
            _authoredOptics = CombatModelLibrary.InstantiateAuthoredOptics(firstPerson: true);
            _authoredOptics.Root.Position = Vector3.Zero;
            _authoredOptics.Root.Rotation = Vector3.Zero;
            _authoredOptics.Root.Scale = Vector3.One;
            _opticRoot.AddChild(_authoredOptics.Root);
        }
        catch (Exception exception)
        {
            GD.PushError($"Required authored optic set unavailable: {exception.Message}");
            throw new InvalidOperationException(
                "First-person authored optics are a required production asset.",
                exception);
        }
    }

    private bool RefreshAuthoredOpticPresentation(
        string? opticId,
        bool weaponOwnsAuthoredOptic)
    {
        if (!IsInstanceValid(_authoredOptics?.Root))
        {
            throw new InvalidOperationException(
                "Required first-person authored optic set was not initialized.");
        }

        var showExternalModel = _opticRoot.Visible && !weaponOwnsAuthoredOptic;
        var visible = _authoredOptics.Configure(opticId, showExternalModel);
        if (visible && _authoredOptics.ActiveReticleAnchor is { } reticleAnchor)
        {
            _opticReticle.Position = _opticRoot.GlobalTransform.AffineInverse()
                * reticleAnchor.GlobalPosition;
        }
        return visible;
    }

    internal bool AuthoredOpticPresentationValidForDiagnostics
    {
        get
        {
            if (!IsInstanceValid(_authoredOptics?.Root)
                || !IsInstanceValid(_opticRoot)
                || !IsInstanceValid(_opticReticle))
            {
                return false;
            }

            var hasOptic = EquippedWeapon.Attachments.TryGetValue(
                AttachmentSlot.Optic,
                out var opticId);
            var weaponOwnsAuthoredOptic = hasOptic
                && WeaponUsesIntegratedOptic(EquippedWeapon.Platform, opticId);
            var externalExpected = hasOptic && !weaponOwnsAuthoredOptic;
            var legacyHidden = !_reflexSightModel.Visible
                && !_holoSightModel.Visible
                && !_scopeSightModel.Visible;
            var reticleAligned = externalExpected
                ? (_authoredOptics.ActiveReticleAnchor is { } reticleAnchor
                    && _opticReticle.GlobalPosition.DistanceTo(
                        reticleAnchor.GlobalPosition) <= 0.001f)
                : !weaponOwnsAuthoredOptic
                    || _opticReticle.Position.DistanceTo(Vector3.Zero) <= 0.001f;
            var integratedPresentationValid = true;
            if (EquippedWeapon.Platform == WeaponPlatform.VSS
                && weaponOwnsAuthoredOptic)
            {
                integratedPresentationValid = _authoredPlatformWeapons.TryGetValue(
                        WeaponPlatform.VSS,
                        out var vssVisual)
                    && IsInstanceValid(vssVisual.Root)
                    && vssVisual.IntegratedOpticPresentationValid
                    && _opticRoot.GlobalPosition.DistanceTo(
                        vssVisual.OpticReticleAnchor.GlobalPosition) <= 0.001f
                    && _opticReticle.GlobalPosition.DistanceTo(
                        vssVisual.OpticReticleAnchor.GlobalPosition) <= 0.001f;
            }
            return legacyHidden
                && _authoredOptics.PresentationMatches(opticId, externalExpected)
                && reticleAligned
                && integratedPresentationValid;
        }
    }

    internal bool HasVisibleAuthoredOpticGeometryForDiagnostics
        => IsInstanceValid(_authoredOptics?.Root)
            && _authoredOptics.ActiveGeometryVisible;
}
