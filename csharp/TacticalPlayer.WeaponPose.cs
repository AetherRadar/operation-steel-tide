using Godot;

namespace OperationSteelTide;

public partial class TacticalPlayer
{
    private static readonly Vector3 HipWeaponPosition = new(0.26f, -0.30f, -0.72f);
    private static readonly Vector3 SidearmHipWeaponPosition = new(0.30f, -0.24f, -0.64f);
    private static readonly Vector3 ReloadWeaponPosition = new(0.18f, -0.23f, -0.86f);
    private static readonly Vector3 SearchWeaponStart = new(0.5f, -0.58f, -0.48f);
    private static readonly Vector3 SearchWeaponEnd = new(0.32f, -0.48f, -0.72f);

    private Vector3 WeaponViewPositionTarget()
    {
        var target = _isAiming
            ? AimWeaponPosition()
            : WeaponCatalog.IsSidearm(EquippedWeapon.Platform)
                ? SidearmHipWeaponPosition
                : HipWeaponPosition;
        if (_isReloading)
        {
            // Authored long-gun arms already occupy the full first-person
            // frame. Moving them to the generic reload mount lifts and rolls
            // both sleeve openings into view; keep the weapon at its hip mount
            // while the authored clip or M4 mechanism animation plays.
            return EquippedWeapon.Platform is WeaponPlatform.M3A1 or WeaponPlatform.M4A1
                ? HipWeaponPosition
                : ReloadWeaponPosition;
        }
        if (_searchPose > 0.0f)
        {
            return SearchWeaponStart.Lerp(SearchWeaponEnd, _searchPose);
        }
        if (_isPlating)
        {
            target += new Vector3(0.22f, -0.34f, 0.12f);
        }
        return target;
    }

    private Vector3 AimWeaponPosition()
    {
        // Keep sidearms close enough to read as substantial first-person weapons.
        // The authored two-hand rig is tucked separately, so this shorter camera
        // distance does not turn either sleeve opening into a full-screen shape.
        if (WeaponCatalog.IsSidearm(EquippedWeapon.Platform))
        {
            var pistolSightHeight = EquippedWeapon.Platform switch
            {
                WeaponPlatform.DesertEagle => 0.18f,
                WeaponPlatform.GSh18 => 0.205f,
                WeaponPlatform.P226 => 0.19f,
                WeaponPlatform.M1911 => 0.22f,
                _ => 0.20f
            };
            // Centre the compact sight picture and keep it only slightly farther
            // forward than the hip mount. The previous -0.82 depth made the gun
            // shrink as ADS started and exaggerated the apparent arm length.
            return new Vector3(
                0.035f,
                -pistolSightHeight * _weaponRoot.Scale.Y + 0.025f,
                -0.66f);
        }
        var opticPosition = IsInstanceValid(_opticRoot) && _opticRoot.Visible
            ? _opticRoot.Position
            : new Vector3(0.0f, 0.205f, 0.0f);
        return new Vector3(
            -opticPosition.X * _weaponRoot.Scale.X,
            -opticPosition.Y * _weaponRoot.Scale.Y,
            -0.55f);
    }

    private static float OpticMountHeight(WeaponPlatform platform, string? opticId)
        => (platform, opticId) switch
        {
            // The compact AK receiver and fixed rear sight rise above the legacy
            // 0.205 m mount. These platform-specific risers keep the complete
            // lower half of each sight window clear instead of merely moving the dot.
            (WeaponPlatform.AK74, "optic_micro") => 0.28f,
            (WeaponPlatform.AK74, "optic_holo") => 0.30f,
            (WeaponPlatform.AK74, _) => 0.32f,
            (WeaponPlatform.ScarL, "optic_micro") => 0.31f,
            (WeaponPlatform.ScarL, "optic_holo") => 0.32f,
            (WeaponPlatform.ScarL, _) => 0.34f,
            (WeaponPlatform.MP5A5, "optic_micro") => 0.31f,
            (WeaponPlatform.MP5A5, "optic_holo") => 0.32f,
            (WeaponPlatform.MP5A5, _) => 0.34f,
            (WeaponPlatform.M4A1, "optic_micro") => 0.167f,
            (WeaponPlatform.M4A1, "optic_holo") => 0.24f,
            (WeaponPlatform.M4A1, _) => 0.25f,
            (WeaponPlatform.AWM, "optic_scope" or "optic_7x" or "optic_sniper") => 0.38f,
            (_, "optic_scope" or "optic_7x" or "optic_sniper") => 0.225f,
            _ => 0.205f
        };

    private Vector3 WeaponViewRotationTarget()
    {
        if (_isReloading)
        {
            return EquippedWeapon.Platform is WeaponPlatform.M3A1 or WeaponPlatform.M4A1
                ? Vector3.Zero
                : new Vector3(-0.13f, 0.0f, -0.32f);
        }

        var searchPitch = _searchPose > 0.0f ? 0.34f : 0.0f;
        var searchRoll = _searchPose > 0.0f ? -0.42f : 0.0f;
        return new Vector3(
            searchPitch + _recoilPitch * 0.55f,
            0.0f,
            searchRoll + _recoilSide * 0.35f);
    }

    internal void SeedWeaponPoseForDiagnostics(Vector3 position, Vector3 rotation)
    {
        _weaponRoot.Position = position;
        _weaponRoot.Rotation = rotation;
    }

    internal Vector3 WeaponRotationForDiagnostics => _weaponRoot.Rotation;

    internal Vector2 OpticScreenOffsetForDiagnostics()
    {
        var viewportCenter = _camera.GetViewport().GetVisibleRect().Size * 0.5f;
        return _camera.UnprojectPosition(_opticReticle.GlobalPosition) - viewportCenter;
    }

    internal FirstPersonOpticClearanceInspection InspectOpticClearanceForDiagnostics()
    {
        AuthoredWeaponVisual? visual = EquippedWeapon.Platform == WeaponPlatform.M4A1
            ? IsInstanceValid(_authoredPrimaryWeapon?.Root) ? _authoredPrimaryWeapon : null
            : _authoredPlatformWeapons.TryGetValue(EquippedWeapon.Platform, out var platformVisual)
                && IsInstanceValid(platformVisual.Root)
                    ? platformVisual
                    : null;
        if (visual is null || !IsInstanceValid(_opticRoot) || !_opticRoot.Visible)
        {
            return default;
        }

        var weaponTop = float.NegativeInfinity;
        var weaponRootInverse = _weaponRoot.GlobalTransform.AffineInverse();
        foreach (var node in visual.Root.FindChildren(
                     "*",
                     nameof(MeshInstance3D),
                     recursive: true,
                     owned: false))
        {
            if (node is not MeshInstance3D mesh
                || mesh.Mesh is null
                || !mesh.IsVisibleInTree()
                || IsNodeBelow(mesh, visual.OpticMount))
            {
                continue;
            }

            var bounds = mesh.GetAabb();
            var toWeaponRoot = weaponRootInverse * mesh.GlobalTransform;
            for (var endpoint = 0; endpoint < 8; endpoint++)
            {
                weaponTop = Mathf.Max(
                    weaponTop,
                    (toWeaponRoot * bounds.GetEndpoint(endpoint)).Y);
            }
        }

        if (!float.IsFinite(weaponTop))
        {
            return default;
        }

        var m4IronSightsClear = EquippedWeapon.Platform != WeaponPlatform.M4A1
            || (visual.RearIronSight is { Visible: false }
                && visual.FrontIronSight is { Visible: false });
        var authoredPresentationValid = EquippedWeapon.Platform == WeaponPlatform.M4A1
            ? AuthoredM4AttachmentPresentationValidForDiagnostics
            : AuthoredOpticPresentationValidForDiagnostics;
        var reticleBounds = _opticReticle.Mesh?.GetAabb() ?? new Aabb();
        var reticleDiameter = Mathf.Max(reticleBounds.Size.X, reticleBounds.Size.Y);
        EquippedWeapon.Attachments.TryGetValue(AttachmentSlot.Optic, out var opticId);
        var integratedOptic = WeaponUsesIntegratedOptic(
            EquippedWeapon.Platform,
            opticId);
        var integratedGeometryVisible = integratedOptic
            && (EquippedWeapon.Platform == WeaponPlatform.VSS
                ? visual.IntegratedOpticPresentationValid
                : EquippedWeapon.Platform == WeaponPlatform.M4A1
                    && visual.OpticMount.Visible);
        var vssAperture = EquippedWeapon.Platform == WeaponPlatform.VSS
                && integratedOptic
            ? visual.IntegratedOpticInspection
            : default;
        var integratedApertureValid = EquippedWeapon.Platform != WeaponPlatform.VSS
            || !integratedOptic
            || vssAperture.Valid;
        return new FirstPersonOpticClearanceInspection(
            true,
            weaponTop,
            _opticRoot.Position.Y,
            _opticRoot.Position.Y - weaponTop,
            _opticRoot.Visible
                && (integratedGeometryVisible
                    || (!integratedOptic
                        && HasVisibleAuthoredOpticGeometryForDiagnostics)),
            m4IronSightsClear,
            authoredPresentationValid,
            reticleDiameter,
            integratedOptic,
            integratedApertureValid,
            vssAperture.GlassSurfaceCount,
            vssAperture.RearApertureSize,
            EquippedWeapon.Platform != WeaponPlatform.VSS || !integratedOptic
                ? 0.0f
                : vssAperture.Valid
                ? (visual.Root.GlobalTransform.AffineInverse()
                    * visual.OpticReticleAnchor.GlobalPosition)
                    .DistanceTo(vssAperture.RearApertureCenter)
                : float.PositiveInfinity);
    }

    private static bool IsNodeBelow(Node node, Node ancestor)
    {
        for (var current = node; current is not null; current = current.GetParent())
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }
        }
        return false;
    }

    internal readonly record struct FirstPersonOpticClearanceInspection(
        bool Available,
        float WeaponTop,
        float MountHeight,
        float MountClearance,
        bool OpticVisible,
        bool IronSightsClear,
        bool AuthoredPresentationValid,
        float ReticleDiameter,
        bool IntegratedOptic,
        bool IntegratedApertureValid,
        int IntegratedGlassSurfaceCount,
        Vector2 IntegratedApertureSize,
        float IntegratedAnchorResidual);
}
