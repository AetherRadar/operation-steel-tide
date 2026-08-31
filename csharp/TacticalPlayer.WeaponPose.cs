using Godot;

namespace OperationSteelTide;

public partial class TacticalPlayer
{
    private static readonly Vector3 HipWeaponPosition = new(0.34f, -0.30f, -0.68f);
    private static readonly Vector3 ImpactRifleHipWeaponPosition = new(
        0.31f,
        -0.27f,
        -0.62f);
    private static readonly Vector3 PrecisionRifleHipWeaponPosition = new(
        0.30f,
        -0.29f,
        -0.66f);
    private static readonly Vector3 SidearmHipWeaponPosition = new(0.38f, -0.18f, -0.55f);
    private static readonly Vector3 SearchWeaponStart = new(0.5f, -0.58f, -0.48f);
    private static readonly Vector3 SearchWeaponEnd = new(0.32f, -0.48f, -0.72f);
    private const float HipWeaponPitch = 0.018f;
    private const float HipWeaponYaw = 0.045f;
    private const float HipWeaponRoll = -0.018f;
    private const float MicroOpticRailContactOffset = 0.070f;
    private const float HoloOpticRailContactOffset = 0.092f;
    private const float ScopeOpticRailContactOffset = 0.084f;
    private const float M4A1IntegratedMicroOpticHeight = 0.167f;
    private const float M4A1RailContactHeight =
        M4A1IntegratedMicroOpticHeight - MicroOpticRailContactOffset;
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
            // Sidearms use cropped animated forearms and remain close to their
            // normal ready pose. Long guns keep their platform-specific working
            // space offset for the larger magazine and action mechanisms.
            return WeaponCatalog.IsSidearm(EquippedWeapon.Platform)
                ? SidearmHipWeaponPosition + SidearmReloadViewPositionOffset()
                : HipWeaponPosition + PlatformReloadViewPositionOffset();
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
            if (IsInstanceValid(_opticRoot) && _opticRoot.Visible)
            {
                var sidearmOpticPosition = ActiveOpticPositionInWeaponRoot();
                return new Vector3(
                    -sidearmOpticPosition.X * _weaponRoot.Scale.X,
                    -sidearmOpticPosition.Y * _weaponRoot.Scale.Y,
                    -0.66f);
            }

            var pistolSightHeight = EquippedWeapon.Platform switch
            {
                WeaponPlatform.DesertEagle => 0.17f,
                WeaponPlatform.GSh18 => 0.195f,
                WeaponPlatform.P226 => 0.182f,
                WeaponPlatform.M1911 => 0.200f,
                _ => 0.19f
            };
            var sidearmAdsX = EquippedWeapon.Platform == WeaponPlatform.DesertEagle
                ? 0.085f
                : 0.050f;
            // Keep the pistol close to the screen and push it a touch farther
            // right, so ADS reads like a tighter ready-up rather than a full
            // arm extension. This also shortens the transition distance from
            // the hip pose, which reduces visible sleeve sweep on ultrawide.
            return new Vector3(
                sidearmAdsX,
                -pistolSightHeight * _weaponRoot.Scale.Y + 0.025f,
                -0.54f);
        }
        var opticPosition = IsInstanceValid(_opticRoot) && _opticRoot.Visible
            ? ActiveOpticPositionInWeaponRoot()
            : new Vector3(0.0f, 0.205f, 0.0f);
        return new Vector3(
            -opticPosition.X * _weaponRoot.Scale.X,
            -opticPosition.Y * _weaponRoot.Scale.Y,
            -0.55f);
    }

    private Vector3 ActiveOpticPositionInWeaponRoot()
    {
        if (!IsInstanceValid(_opticReticle))
        {
            return _opticRoot.Position;
        }

        // Use the final gameplay dot rather than the mount origin as the ADS
        // coordinate source. This absorbs authored parent transforms and any
        // non-zero aperture marker offset without adding a per-frame search.
        return _opticRoot.Transform * _opticReticle.Position;
    }

    private static float OpticMountHeight(WeaponPlatform platform, string? opticId)
        => (platform, opticId) switch
        {
            (WeaponPlatform.ScarL, "optic_micro") => 0.33f,
            (WeaponPlatform.ScarL, "optic_holo") => 0.34f,
            (WeaponPlatform.ScarL, _) => 0.36f,
            (WeaponPlatform.MP5A5, "optic_micro") => 0.325f,
            (WeaponPlatform.MP5A5, "optic_holo") => 0.335f,
            (WeaponPlatform.MP5A5, _) => 0.355f,
            (WeaponPlatform.M3A1, "optic_micro") => 0.170f,
            (WeaponPlatform.M3A1, "optic_holo") => 0.192f,
            (WeaponPlatform.M3A1, _) => 0.184f,
            (WeaponPlatform.M4A1, _) => M4A1OpticMountHeight(opticId),
            (WeaponPlatform.AWM, "optic_scope" or "optic_7x" or "optic_sniper") => 0.38f,
            (_, "optic_scope" or "optic_7x" or "optic_sniper") => 0.225f,
            _ => 0.205f
        };

    private static float M4A1OpticMountHeight(string? opticId)
        => opticId switch
        {
            "optic_micro" => M4A1IntegratedMicroOpticHeight,
            "optic_holo" => M4A1RailContactHeight + HoloOpticRailContactOffset,
            "optic_scope" or "optic_7x" or "optic_sniper"
                => M4A1RailContactHeight + ScopeOpticRailContactOffset,
            _ => M4A1IntegratedMicroOpticHeight
        };

    private static float AuthoredOpticRailContactOffset(string? opticId)
        => CombatModelLibrary.AuthoredOpticRailContactOffset(opticId);

    private Vector3 WeaponViewRotationTarget()
    {
        if (_isReloading)
        {
            // Reload motion belongs to the support arm and weapon mechanisms.
            // Rolling the shared root also rolls the right hand off the pistol
            // grip and turns the sleeve openings toward the camera.
            return WeaponCatalog.IsSidearm(EquippedWeapon.Platform)
                ? SidearmReloadViewRotation()
                : Vector3.Zero;
        }

        var searchPitch = _searchPose > 0.0f ? 0.34f : 0.0f;
        var searchRoll = _searchPose > 0.0f ? -0.42f : 0.0f;
        var useRightBiasedHipPose = !_isAiming && _searchPose <= 0.0f;
        return new Vector3(
            searchPitch
                + (useRightBiasedHipPose ? HipWeaponPitch : 0.0f)
                + _recoilPitch * 0.25f
                + _viewmodelKickPitch,
            useRightBiasedHipPose ? HipWeaponYaw : 0.0f,
            searchRoll
                + (useRightBiasedHipPose ? HipWeaponRoll : 0.0f)
                + _recoilSide * 0.22f
                + _viewmodelKickRoll);
    }

    private void UpdateWeaponViewPose(float delta, float handling)
    {
        var targetPosition = WeaponViewPositionTarget();
        var positionResponse = _isAiming
            ? 7.5f + handling * 6.0f
            : 6.0f + handling * 3.0f;
        _weaponRoot.Position = _weaponRoot.Position.Lerp(
            targetPosition,
            SmoothFactor(positionResponse, delta));
        var weaponRotation = _weaponRoot.Rotation;
        if (_isAiming)
        {
            // Vault and ladder poses can carry a temporary yaw; ADS must begin
            // on the optic axis.
            weaponRotation.Y = 0.0f;
        }
        _weaponRoot.Rotation = weaponRotation.Lerp(
            WeaponViewRotationTarget(),
            SmoothFactor(9.0f, delta));
    }

    private void UpdateHeldWeaponPresentation(float delta)
    {
        UpdateWeaponViewPose(delta, EquippedWeapon.Stats().Handling);
        ApplyProceduralHandPose();
        UpdateReloadAnimation();
        SyncAuthoredPrimaryWeapon();
        UpdateAuthoredM4ReloadSupportArm();
    }

    internal void AdvanceVehicleReloadPresentationForDiagnostics(float delta)
    {
        UpdateReloadTimer(delta);
        UpdateHeldWeaponPresentation(delta);
    }

    internal float WeaponViewTargetPoseErrorForDiagnostics
        => _weaponRoot.Position.DistanceTo(WeaponViewPositionTarget())
            + _weaponRoot.Rotation.DistanceTo(WeaponViewRotationTarget());

    private void UpdateViewmodelShotImpulse(float delta)
    {
        _viewmodelKickback = Mathf.Lerp(
            _viewmodelKickback,
            0.0f,
            SmoothFactor(14.0f, delta));
        _viewmodelKickPitch = Mathf.Lerp(
            _viewmodelKickPitch,
            0.0f,
            SmoothFactor(11.5f, delta));
        _viewmodelKickRoll = Mathf.Lerp(
            _viewmodelKickRoll,
            0.0f,
            SmoothFactor(15.0f, delta));
        _viewmodelKickSide = Mathf.Lerp(
            _viewmodelKickSide,
            0.0f,
            SmoothFactor(16.0f, delta));
    }

    private void ApplyViewmodelShotImpulse(float recoil, float stanceRecoil)
    {
        var carryScale = SoundLab.FirstPersonShotImpactScale(
            EquippedWeapon.Platform);
        var aimScale = _isAiming ? 0.66f : 1.0f;
        var strength = Mathf.Sqrt(Mathf.Max(0.25f, recoil))
            * stanceRecoil
            * RoleRecoilMultiplier
            * carryScale
            * aimScale;
        var kickback = _rng.RandfRange(0.072f, 0.088f) * strength;
        var pitch = _rng.RandfRange(0.036f, 0.048f) * strength;
        var side = _rng.RandfRange(-0.022f, 0.022f) * strength;
        var roll = -side * _rng.RandfRange(0.72f, 1.05f);
        _viewmodelKickback = Mathf.Min(0.24f, _viewmodelKickback + kickback);
        _viewmodelKickPitch = Mathf.Max(-0.20f, _viewmodelKickPitch - pitch);
        _viewmodelKickSide = Mathf.Clamp(_viewmodelKickSide + side, -0.065f, 0.065f);
        _viewmodelKickRoll = Mathf.Clamp(_viewmodelKickRoll + roll, -0.09f, 0.09f);

        // Apply most of the first-frame impulse immediately. The tracked values
        // above hold the pose briefly and then return independently from camera
        // recoil, so the firearm reads as a physical object reacting in the hands.
        _weaponRoot.Position += new Vector3(
            side * 0.16f,
            -kickback * 0.05f,
            kickback * 0.74f);
        _weaponRoot.Rotation += new Vector3(
            -pitch * 0.72f,
            0.0f,
            roll * 0.58f);
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
            _recoilPitch,
            _recoilSide,
            _weaponRoot.Position,
            _weaponRoot.Rotation,
            _head.Rotation,
            _muzzleBloom.Visible,
            _muzzleFlash.LightEnergy);

    internal void SeedWeaponPoseForDiagnostics(Vector3 position, Vector3 rotation)
    {
        _weaponRoot.Position = position;
        _weaponRoot.Rotation = rotation;
    }

    internal void SeedWeaponFeedbackForDiagnostics(ulong seed)
        => _rng.Seed = seed;

    internal void ResetWeaponFeedbackForDiagnostics()
    {
        _recoilPitch = 0.0f;
        _recoilSide = 0.0f;
        ResetViewmodelShotImpulse();
        SetAimingPoseForDiagnostics(false);
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
        var screenScale = DiagnosticScreenScale(out var screenSize);
        return _camera.UnprojectPosition(_opticReticle.GlobalPosition) * screenScale
            - screenSize * 0.5f;
    }

    internal FirstPersonOpticClearanceInspection InspectOpticClearanceForDiagnostics()
    {
        AuthoredWeaponVisual? visual = EquippedWeapon.Platform == WeaponPlatform.M4A1
            ? IsInstanceValid(_authoredPrimaryWeapon?.Root) ? _authoredPrimaryWeapon : null
            : _authoredPlatformWeapons.TryGetValue(EquippedWeapon.Platform, out var platformVisual)
                && IsInstanceValid(platformVisual.Root)
                    ? platformVisual
                    : null;
        var weaponGeometryRoot = visual?.Root;
        if (weaponGeometryRoot is null
            && EquippedWeapon.Platform == WeaponPlatform.M3A1
            && IsInstanceValid(_authoredFirstPersonSmg?.WeaponBody))
        {
            weaponGeometryRoot = _authoredFirstPersonSmg.WeaponBody;
        }
        if (weaponGeometryRoot is null
            || !IsInstanceValid(_opticRoot)
            || !_opticRoot.Visible)
        {
            return default;
        }

        var weaponTop = float.NegativeInfinity;
        var weaponRootInverse = _weaponRoot.GlobalTransform.AffineInverse();
        if (weaponGeometryRoot is MeshInstance3D rootMesh)
        {
            AccumulateWeaponMesh(rootMesh);
        }
        foreach (var mesh in CombatModelLibrary.MeshesBelow(weaponGeometryRoot))
        {
            AccumulateWeaponMesh(mesh);
        }

        if (!float.IsFinite(weaponTop))
        {
            return default;
        }

        var hasDedicatedIronSights = visual is not null
            && (IsInstanceValid(visual.RearIronSight)
                || IsInstanceValid(visual.FrontIronSight)
                || IsInstanceValid(visual.IronSightGeometry));
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
            && (WeaponCatalog.HasFixedIntegratedScope(EquippedWeapon.Platform)
                ? visual?.IntegratedOpticPresentationValid == true
                : EquippedWeapon.Platform == WeaponPlatform.M4A1
                    && visual?.OpticMount.Visible == true);
        var integratedAperture = WeaponCatalog.HasFixedIntegratedScope(
                EquippedWeapon.Platform)
            && integratedOptic
            ? visual?.IntegratedOpticInspection ?? default
            : default;
        var integratedApertureValid = WeaponCatalog.HasFixedIntegratedScope(
                EquippedWeapon.Platform)
            ? !integratedOptic || integratedAperture.Valid
            : EquippedWeapon.Platform != WeaponPlatform.M4A1
                || !integratedOptic
                || visual?.IntegratedM4OpticAxisValid == true;
        var mountSurfaceHeight = weaponTop;
        if (EquippedWeapon.Platform == WeaponPlatform.M4A1 && visual is not null)
        {
            var integratedAnchor = weaponRootInverse
                * visual.OpticReticleAnchor.GlobalPosition;
            mountSurfaceHeight = integratedAnchor.Y - MicroOpticRailContactOffset;
        }
        else if (visual?.OpticRailContact is { } opticRailContact
            && IsInstanceValid(opticRailContact))
        {
            mountSurfaceHeight = (weaponRootInverse
                * opticRailContact.GlobalPosition).Y;
        }
        var opticBottom = _opticRoot.Position.Y
            - AuthoredOpticRailContactOffset(opticId);
        const float weldedIronSightClearanceTolerance = 0.001f;
        var ironSightsClear = hasDedicatedIronSights
            ? (!GodotObject.IsInstanceValid(visual!.RearIronSight)
                    || visual.RearIronSight!.Visible == false)
                && (!GodotObject.IsInstanceValid(visual.FrontIronSight)
                    || visual.FrontIronSight!.Visible == false)
                && (!GodotObject.IsInstanceValid(visual.IronSightGeometry)
                    || visual.IronSightGeometry!.Visible == false)
            : integratedOptic
                || opticBottom >= weaponTop - weldedIronSightClearanceTolerance;
        return new FirstPersonOpticClearanceInspection(
            true,
            weaponTop,
            _opticRoot.Position.Y,
            _opticRoot.Position.Y - weaponTop,
            mountSurfaceHeight,
            opticBottom,
            opticBottom - mountSurfaceHeight,
            _opticRoot.Visible
                && (integratedGeometryVisible
                    || (!integratedOptic
                        && HasVisibleAuthoredOpticGeometryForDiagnostics)),
            ironSightsClear,
            authoredPresentationValid,
            reticleDiameter,
            integratedOptic,
            integratedApertureValid,
            integratedAperture.GlassSurfaceCount,
            integratedAperture.RearApertureSize,
            !WeaponCatalog.HasFixedIntegratedScope(EquippedWeapon.Platform)
                || !integratedOptic
                ? 0.0f
                : integratedAperture.Valid
                ? (visual!.Root.GlobalTransform.AffineInverse()
                    * visual.OpticReticleAnchor.GlobalPosition)
                    .DistanceTo(integratedAperture.RearApertureCenter)
                : float.PositiveInfinity);

        void AccumulateWeaponMesh(MeshInstance3D mesh)
        {
            if (mesh.Mesh is null
                || !mesh.IsVisibleInTree()
                || visual is not null && IsNodeBelow(mesh, visual.OpticMount))
            {
                return;
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
    }

    internal OpticAxisProjectionInspection InspectOpticAxisProjectionForDiagnostics()
    {
        if (!IsInstanceValid(_camera)
            || !IsInstanceValid(_opticRoot)
            || !IsInstanceValid(_opticReticle)
            || !_opticRoot.Visible)
        {
            return default;
        }

        AuthoredWeaponVisual? visual = EquippedWeapon.Platform == WeaponPlatform.M4A1
            ? IsInstanceValid(_authoredPrimaryWeapon?.Root) ? _authoredPrimaryWeapon : null
            : _authoredPlatformWeapons.TryGetValue(EquippedWeapon.Platform, out var platformVisual)
                && IsInstanceValid(platformVisual.Root)
                    ? platformVisual
                    : null;

        EquippedWeapon.Attachments.TryGetValue(AttachmentSlot.Optic, out var opticId);
        var integrated = WeaponUsesIntegratedOptic(EquippedWeapon.Platform, opticId);
        var reticleWorld = _opticReticle.GlobalPosition;
        Vector3 rearWorld;
        Vector3 frontWorld;
        if (integrated
            && WeaponCatalog.HasFixedIntegratedScope(EquippedWeapon.Platform))
        {
            var aperture = visual?.IntegratedOpticInspection ?? default;
            if (!aperture.Valid)
            {
                return default;
            }

            rearWorld = visual!.Root.GlobalTransform * aperture.RearApertureCenter;
            frontWorld = visual.Root.GlobalTransform * aperture.FrontApertureCenter;
        }
        else if (integrated)
        {
            if (visual?.OpticRearApertureAnchor is not { } rearAnchor
                || visual.OpticFrontApertureAnchor is not { } frontAnchor
                || !IsInstanceValid(rearAnchor)
                || !IsInstanceValid(frontAnchor))
            {
                return default;
            }
            rearWorld = rearAnchor.GlobalPosition;
            frontWorld = frontAnchor.GlobalPosition;
        }
        else
        {
            if (_authoredOptics.ActiveRearApertureAnchor is not { } rearAnchor
                || _authoredOptics.ActiveFrontApertureAnchor is not { } frontAnchor
                || !IsInstanceValid(rearAnchor)
                || !IsInstanceValid(frontAnchor))
            {
                return default;
            }
            rearWorld = rearAnchor.GlobalPosition;
            frontWorld = frontAnchor.GlobalPosition;
        }

        var axis = frontWorld - rearWorld;
        if (axis.LengthSquared() <= 0.000001f)
        {
            return default;
        }

        var screenScale = DiagnosticScreenScale(out var screenSize);
        var viewportCenter = screenSize * 0.5f;
        var reticleProjection = _camera.UnprojectPosition(reticleWorld) * screenScale;
        var rearProjection = _camera.UnprojectPosition(rearWorld) * screenScale;
        var frontProjection = _camera.UnprojectPosition(frontWorld) * screenScale;
        var firingDirection = -_camera.GlobalBasis.Z.Normalized();
        return new OpticAxisProjectionInspection(
            true,
            integrated,
            reticleProjection,
            rearProjection,
            frontProjection,
            viewportCenter,
            reticleProjection.DistanceTo(rearProjection),
            rearProjection.DistanceTo(frontProjection),
            frontProjection.DistanceTo(viewportCenter),
            axis.Normalized().AngleTo(firingDirection));
    }

    private Vector2 DiagnosticScreenScale(out Vector2 screenSize)
    {
        var logicalSize = _camera.GetViewport().GetVisibleRect().Size;
        var windowSize = GetWindow().Size;
        screenSize = new Vector2(windowSize.X, windowSize.Y);
        if (logicalSize.X <= 0.0f
            || logicalSize.Y <= 0.0f
            || screenSize.X <= 0.0f
            || screenSize.Y <= 0.0f)
        {
            screenSize = logicalSize;
            return Vector2.One;
        }
        return new Vector2(
            screenSize.X / logicalSize.X,
            screenSize.Y / logicalSize.Y);
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
        float MountSurfaceHeight,
        float OpticBottom,
        float MountGap,
        bool OpticVisible,
        bool IronSightsClear,
        bool AuthoredPresentationValid,
        float ReticleDiameter,
        bool IntegratedOptic,
        bool IntegratedApertureValid,
        int IntegratedGlassSurfaceCount,
        Vector2 IntegratedApertureSize,
        float IntegratedAnchorResidual);

    internal readonly record struct OpticAxisProjectionInspection(
        bool Available,
        bool IntegratedOptic,
        Vector2 ReticleProjection,
        Vector2 RearApertureProjection,
        Vector2 FrontApertureProjection,
        Vector2 ViewportCenter,
        float ReticleToRearPixels,
        float RearToFrontPixels,
        float FrontToScreenCenterPixels,
        float AxisAngleRadians);
}

internal readonly record struct ViewmodelShotImpulseInspection(
    float Kickback,
    float Pitch,
    float Roll,
    float Side,
    float CameraPitch,
    float CameraSide,
    Vector3 ViewPosition,
    Vector3 ViewRotation,
    Vector3 HeadRotation,
    bool MuzzleBloomVisible,
    float MuzzleLightEnergy);
