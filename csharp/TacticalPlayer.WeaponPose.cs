using Godot;

namespace OperationSteelTide;

public partial class TacticalPlayer
{
    private static readonly Vector3 HipWeaponPosition = new(0.26f, -0.30f, -0.72f);
    private static readonly Vector3 ImpactRifleHipWeaponPosition = new(
        0.22f,
        -0.27f,
        -0.64f);
    private static readonly Vector3 PrecisionRifleHipWeaponPosition = new(
        0.22f,
        -0.29f,
        -0.68f);
    private static readonly Vector3 SidearmHipWeaponPosition = new(0.30f, -0.24f, -0.64f);
    private static readonly Vector3 SearchWeaponStart = new(0.5f, -0.58f, -0.48f);
    private static readonly Vector3 SearchWeaponEnd = new(0.32f, -0.48f, -0.72f);
    private float _viewmodelKickback;
    private float _viewmodelKickPitch;
    private float _viewmodelKickRoll;
    private float _viewmodelKickSide;

    private Vector3 WeaponViewPositionTarget()
    {
        var target = _isAiming
            ? AimWeaponPosition()
            : WeaponCatalog.IsSidearm(EquippedWeapon.Platform)
                ? SidearmHipWeaponPosition
                : HipWeaponPositionForCurrentPlatform();
        if (_isReloading)
        {
            // The authored arm meshes include the complete forearm and capped
            // upper sleeve. Keep their shoulder end at the camera-bottom mount
            // throughout every reload; lifting the complete rig used to expose
            // both sleeve caps as detached black circles in the centre of view.
            return WeaponCatalog.IsSidearm(EquippedWeapon.Platform)
                ? SidearmHipWeaponPosition
                : HipWeaponPosition;
        }
        if (_searchPose > 0.0f)
        {
            return SearchWeaponStart.Lerp(SearchWeaponEnd, _searchPose);
        }
        if (_isPlating)
        {
            target += new Vector3(0.22f, -0.34f, 0.12f);
        }
        return target + new Vector3(
            _viewmodelKickSide * 0.24f,
            -_viewmodelKickback * 0.08f,
            _viewmodelKickback);
    }

    private Vector3 HipWeaponPositionForCurrentPlatform()
        => EquippedWeapon.Platform switch
        {
            WeaponPlatform.M4A1 or WeaponPlatform.M3A1 => HipWeaponPosition,
            WeaponPlatform.M24 or WeaponPlatform.AXMC or WeaponPlatform.AWM
                => PrecisionRifleHipWeaponPosition,
            _ => ImpactRifleHipWeaponPosition
        };

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
            (WeaponPlatform.AK74, "optic_micro") => 0.29f,
            (WeaponPlatform.AK74, "optic_holo") => 0.31f,
            (WeaponPlatform.AK74, _) => 0.33f,
            (WeaponPlatform.ScarL, "optic_micro") => 0.33f,
            (WeaponPlatform.ScarL, "optic_holo") => 0.34f,
            (WeaponPlatform.ScarL, _) => 0.36f,
            (WeaponPlatform.MP5A5, "optic_micro") => 0.325f,
            (WeaponPlatform.MP5A5, "optic_holo") => 0.335f,
            (WeaponPlatform.MP5A5, _) => 0.355f,
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
            // Reload motion belongs to the support arm and weapon mechanisms.
            // Rolling the shared root also rolls the right hand off the pistol
            // grip and turns the sleeve openings toward the camera.
            return Vector3.Zero;
        }

        var searchPitch = _searchPose > 0.0f ? 0.34f : 0.0f;
        var searchRoll = _searchPose > 0.0f ? -0.42f : 0.0f;
        return new Vector3(
            searchPitch + _recoilPitch * 0.25f + _viewmodelKickPitch,
            0.0f,
            searchRoll + _recoilSide * 0.22f + _viewmodelKickRoll);
    }

    private void UpdateViewmodelShotImpulse(float delta)
    {
        _viewmodelKickback = Mathf.Lerp(
            _viewmodelKickback,
            0.0f,
            SmoothFactor(16.0f, delta));
        _viewmodelKickPitch = Mathf.Lerp(
            _viewmodelKickPitch,
            0.0f,
            SmoothFactor(13.0f, delta));
        _viewmodelKickRoll = Mathf.Lerp(
            _viewmodelKickRoll,
            0.0f,
            SmoothFactor(18.0f, delta));
        _viewmodelKickSide = Mathf.Lerp(
            _viewmodelKickSide,
            0.0f,
            SmoothFactor(19.0f, delta));
    }

    private void ApplyViewmodelShotImpulse(float recoil, float stanceRecoil)
    {
        var carryScale = EquippedWeapon.Platform switch
        {
            WeaponPlatform.MP5A5 or WeaponPlatform.P226 or WeaponPlatform.GSh18 => 0.9f,
            WeaponPlatform.M24 or WeaponPlatform.AXMC or WeaponPlatform.AWM
                or WeaponPlatform.DesertEagle => 1.08f,
            _ => 1.0f
        };
        var aimScale = _isAiming ? 0.58f : 1.0f;
        var strength = Mathf.Sqrt(Mathf.Max(0.25f, recoil))
            * stanceRecoil
            * RoleRecoilMultiplier
            * carryScale
            * aimScale;
        var kickback = _rng.RandfRange(0.054f, 0.068f) * strength;
        var pitch = _rng.RandfRange(0.026f, 0.036f) * strength;
        var side = _rng.RandfRange(-0.018f, 0.018f) * strength;
        var roll = -side * _rng.RandfRange(0.72f, 1.05f);
        _viewmodelKickback = Mathf.Min(0.19f, _viewmodelKickback + kickback);
        _viewmodelKickPitch = Mathf.Max(-0.16f, _viewmodelKickPitch - pitch);
        _viewmodelKickSide = Mathf.Clamp(_viewmodelKickSide + side, -0.055f, 0.055f);
        _viewmodelKickRoll = Mathf.Clamp(_viewmodelKickRoll + roll, -0.075f, 0.075f);

        // Apply most of the first-frame impulse immediately. The tracked values
        // above hold the pose briefly and then return independently from camera
        // recoil, so the firearm reads as a physical object reacting in the hands.
        _weaponRoot.Position += new Vector3(
            side * 0.12f,
            -kickback * 0.035f,
            kickback * 0.62f);
        _weaponRoot.Rotation += new Vector3(
            -pitch * 0.58f,
            0.0f,
            roll * 0.46f);
    }

    private void ResetViewmodelShotImpulse()
    {
        _viewmodelKickback = 0.0f;
        _viewmodelKickPitch = 0.0f;
        _viewmodelKickRoll = 0.0f;
        _viewmodelKickSide = 0.0f;
    }

    internal ViewmodelShotImpulseInspection InspectViewmodelShotImpulseForDiagnostics()
        => new(
            _viewmodelKickback,
            _viewmodelKickPitch,
            _viewmodelKickRoll,
            _viewmodelKickSide,
            _muzzleBloom.Visible,
            _muzzleFlash.LightEnergy);

    internal void SeedWeaponPoseForDiagnostics(Vector3 position, Vector3 rotation)
    {
        _weaponRoot.Position = position;
        _weaponRoot.Rotation = rotation;
    }

    internal void SetAimingPoseForDiagnostics(bool aiming)
    {
        _isAiming = aiming;
        _weaponRoot.Position = WeaponViewPositionTarget();
        _weaponRoot.Rotation = WeaponViewRotationTarget();
        ApplyProceduralHandPose();
        SyncAuthoredPrimaryWeapon();
        _opticReticle.Visible = aiming && IsFirearmQuickSlotSelected;
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

internal readonly record struct ViewmodelShotImpulseInspection(
    float Kickback,
    float Pitch,
    float Roll,
    float Side,
    bool MuzzleBloomVisible,
    float MuzzleLightEnergy);
