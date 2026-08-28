using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public partial class TacticalPlayer
{
    // This compatibility partial stays above 800 lines while authored weapon and
    // hand rigs share TacticalPlayer's private lifecycle. Follow-up: extract the
    // per-platform presentation controllers once that lifecycle has a stable API.
    private const float AuthoredSmgPresentationScale = 0.72f;
    private const float AuthoredArmPresentationScale = 0.72f;
    internal const float MaxServicePistolSupportArmCorrection = 0.03f;
    private static readonly Vector3 AuthoredSmgCameraPosition = new(0.34f, -0.45f, -0.72f);
    private static readonly Vector3 M4ReloadMagazineGripOffset = new(-0.06f, 0.08f, -0.02f);

    private Node3D _proceduralWeaponVisual = null!;
    private Node3D _proceduralFirstPersonArms = null!;
    private AuthoredWeaponVisual _authoredPrimaryWeapon = null!;
    private readonly Dictionary<WeaponPlatform, AuthoredWeaponVisual> _authoredPlatformWeapons = new();
    private AuthoredFirstPersonSmgVisual _authoredFirstPersonSmg = null!;
    private AuthoredFirstPersonArmsVisual _authoredRifleArms = null!;
    private AuthoredFirstPersonArmsVisual _authoredPistolServiceArms = null!;
    private AuthoredFirstPersonArmsVisual _authoredPistolLargeArms = null!;
    private bool _rifleArmsLoadAttempted;
    private bool _pistolServiceArmsLoadAttempted;
    private bool _pistolLargeArmsLoadAttempted;

    // Anchors in authored M4A1 (weapon-root local) coordinates.
    private static readonly Vector3 RifleGripAnchor = new(0.0f, -0.15f, -0.05f);
    private static readonly Vector3 RifleForegripAnchor = new(0.0f, -0.17f, -0.58f);
    private AuthoredGsh18Visual _authoredGsh18Weapon = null!;
    private AuthoredDesertEagleVisual _authoredDesertEagleWeapon = null!;
    private bool _smg45LoadAttempted;
    private bool _gsh18LoadAttempted;
    private bool _desertEagleLoadAttempted;

    internal bool UsesAuthoredPrimaryWeaponForDiagnostics
        => IsInstanceValid(_authoredPrimaryWeapon?.Root)
        && EquippedWeapon.Platform == WeaponPlatform.M4A1;
    internal bool AuthoredM4AttachmentPresentationValidForDiagnostics
    {
        get
        {
            if (!UsesAuthoredPrimaryWeaponForDiagnostics
                || !IsInstanceValid(_muzzle)
                || !IsInstanceValid(_opticRoot)
                || !IsInstanceValid(_opticReticle))
            {
                return false;
            }

            var suppressed = EquippedWeapon.Attachments.TryGetValue(
                    AttachmentSlot.Muzzle,
                    out var muzzleId)
                && muzzleId == "muzzle_suppressor";
            var hasOptic = EquippedWeapon.Attachments.ContainsKey(AttachmentSlot.Optic);
            var expectedMuzzleTip = suppressed
                ? _authoredPrimaryWeapon.SuppressorTip
                : _authoredPrimaryWeapon.MuzzleDeviceTip;
            var authoredVisibility = _authoredPrimaryWeapon.MuzzleDevice.Visible == !suppressed
                && _authoredPrimaryWeapon.Suppressor.Visible == suppressed
                && _authoredPrimaryWeapon.OpticMount.Visible == hasOptic;
            var proceduralOpticsHidden = !_reflexSightModel.Visible
                && !_holoSightModel.Visible
                && !_scopeSightModel.Visible;
            var muzzleAligned = _muzzle.GlobalPosition.DistanceTo(
                expectedMuzzleTip.GlobalPosition) <= 0.001f;
            var opticAligned = !hasOptic
                || (_opticRoot.Visible
                    && _opticRoot.GlobalPosition.DistanceTo(
                        _authoredPrimaryWeapon.OpticReticleAnchor.GlobalPosition) <= 0.001f
                    && _opticReticle.GlobalPosition.DistanceTo(
                        _authoredPrimaryWeapon.OpticReticleAnchor.GlobalPosition) <= 0.001f);
            return authoredVisibility
                && proceduralOpticsHidden
                && muzzleAligned
                && opticAligned;
        }
    }
    internal bool UsesAuthoredGsh18ForDiagnostics
        => IsInstanceValid(_authoredGsh18Weapon?.Root)
        && EquippedWeapon.Platform == WeaponPlatform.GSh18
        && _authoredGsh18Weapon.Root.Visible;
    internal bool UsesAuthoredDesertEagleForDiagnostics
        => IsInstanceValid(_authoredDesertEagleWeapon?.Root)
        && EquippedWeapon.Platform == WeaponPlatform.DesertEagle
        && _authoredDesertEagleWeapon.Root.Visible;
    internal bool UsesAuthoredWeaponPlatformForDiagnostics(WeaponPlatform platform)
        => platform switch
        {
            WeaponPlatform.M4A1 => UsesAuthoredPrimaryWeaponForDiagnostics
                && _authoredPrimaryWeapon.Root.Visible,
            WeaponPlatform.M3A1 => IsInstanceValid(_authoredFirstPersonSmg?.Root)
                && EquippedWeapon.Platform == WeaponPlatform.M3A1
                && _authoredFirstPersonSmg.Root.Visible,
            WeaponPlatform.GSh18 => UsesAuthoredGsh18ForDiagnostics,
            WeaponPlatform.DesertEagle => UsesAuthoredDesertEagleForDiagnostics,
            _ => _authoredPlatformWeapons.TryGetValue(platform, out var visual)
                && IsInstanceValid(visual.Root)
                && EquippedWeapon.Platform == platform
                && visual.Root.Visible
        };
    internal ulong AuthoredWeaponInstanceIdForDiagnostics(WeaponPlatform platform)
        => platform == WeaponPlatform.M3A1
            && IsInstanceValid(_authoredFirstPersonSmg?.Root)
                ? _authoredFirstPersonSmg.Root.GetInstanceId()
                : _authoredPlatformWeapons.TryGetValue(platform, out var visual)
                    && IsInstanceValid(visual.Root)
                    ? visual.Root.GetInstanceId()
                    : 0;
    internal bool WeaponHandPoseValidForDiagnostics
    {
        get
        {
            if (EquippedWeapon.Platform == WeaponPlatform.M3A1)
            {
                return IsInstanceValid(_authoredFirstPersonSmg?.Root)
                    && _authoredFirstPersonSmg.Root.Visible;
            }

            return InspectAuthoredHandPoseForDiagnostics().Valid;
        }
    }
    internal bool UsesAuthoredHandRigForDiagnostics
        => EquippedWeapon.Platform != WeaponPlatform.M3A1
            && ActiveAuthoredArms() is { } arms
            && IsInstanceValid(arms.Root)
            && arms.Root.Visible;

    internal bool SmgReloadPresentationValidForDiagnostics
        => EquippedWeapon.Platform == WeaponPlatform.M3A1
            && _isReloading
            && IsInstanceValid(_authoredFirstPersonSmg?.Root)
            && _authoredFirstPersonSmg.Root.Visible
            && _authoredFirstPersonSmg.Arms.Visible
            && _authoredFirstPersonSmg.WeaponBody.Visible
            && WeaponViewPositionTarget().DistanceTo(HipWeaponPosition) <= 0.001f
            && WeaponViewRotationTarget().Length() <= 0.001f;

    internal Vector3 SmgArmBoundsSizeForDiagnostics
        => IsInstanceValid(_authoredFirstPersonSmg?.Root)
            ? _authoredFirstPersonSmg.ArmBoundsSizeInRoot()
            : Vector3.Zero;

    internal Vector3 SmgWeaponBoundsSizeForDiagnostics
        => IsInstanceValid(_authoredFirstPersonSmg?.Root)
            ? _authoredFirstPersonSmg.WeaponBoundsSizeInRoot()
            : Vector3.Zero;

    internal AuthoredFirstPersonArmsVisual? ActiveAuthoredArmsForDiagnostics
        => ActiveAuthoredArms();

    internal Node3D? ActiveAuthoredWeaponRootForDiagnostics
        => EquippedWeapon.Platform switch
        {
            WeaponPlatform.M4A1 when IsInstanceValid(_authoredPrimaryWeapon?.Root)
                => _authoredPrimaryWeapon.Root,
            WeaponPlatform.M3A1 when IsInstanceValid(_authoredFirstPersonSmg?.WeaponBody)
                => _authoredFirstPersonSmg.WeaponBody,
            WeaponPlatform.GSh18 when IsInstanceValid(_authoredGsh18Weapon?.Root)
                => _authoredGsh18Weapon.Root,
            WeaponPlatform.DesertEagle when IsInstanceValid(_authoredDesertEagleWeapon?.Root)
                => _authoredDesertEagleWeapon.Root,
            _ when _authoredPlatformWeapons.TryGetValue(EquippedWeapon.Platform, out var visual)
                && IsInstanceValid(visual.Root)
                => visual.Root,
            _ => null
        };

    internal Node3D? ActiveAuthoredForegripForDiagnostics
        => EquippedWeapon.Platform == WeaponPlatform.M4A1
            ? _authoredPrimaryWeapon?.Foregrip
            : _authoredPlatformWeapons.TryGetValue(EquippedWeapon.Platform, out var visual)
                ? visual.Foregrip
                : null;

    internal Node3D? ActiveAuthoredMuzzleForDiagnostics
        => EquippedWeapon.Platform == WeaponPlatform.M4A1
            ? _authoredPrimaryWeapon?.MuzzleDevice
            : _authoredPlatformWeapons.TryGetValue(EquippedWeapon.Platform, out var visual)
                ? visual.MuzzleDevice
                : null;

    internal Transform3D WeaponRootGlobalTransformForDiagnostics => _weaponRoot.GlobalTransform;

    internal FirstPersonHandPoseInspection InspectAuthoredHandPoseForDiagnostics()
    {
        var arms = ActiveAuthoredArms();
        if (arms is null || !IsInstanceValid(arms.Root) || !arms.Root.Visible)
        {
            return default;
        }

        var rightPalm = arms.RightPalmFrame.GlobalPosition;
        var leftPalm = arms.LeftPalmFrame.GlobalPosition;
        var rightGrip = arms.RightGripFrame.GlobalPosition;
        var leftGrip = arms.LeftGripFrame.GlobalPosition;
        var rightWrist = arms.RightWristFrame.GlobalPosition;
        var leftWrist = arms.LeftWristFrame.GlobalPosition;
        var weaponRootInverse = _weaponRoot.GlobalTransform.AffineInverse();
        var rightGripInWeaponRoot = weaponRootInverse * rightGrip;
        var leftGripInWeaponRoot = weaponRootInverse * leftGrip;
        var pose = FirstPersonArmPoseCatalog.For(EquippedWeapon.Platform);
        var gripResidual = rightGripInWeaponRoot.DistanceTo(pose.PrimaryGrip);
        var supportGripResidual = leftGripInWeaponRoot.DistanceTo(pose.SupportGrip);
        var palmSeparation = rightPalm.DistanceTo(leftPalm);
        var localGripSeparation = rightGripInWeaponRoot.DistanceTo(leftGripInWeaponRoot);
        var rightWristLength = rightPalm.DistanceTo(rightWrist);
        var leftWristLength = leftPalm.DistanceTo(leftWrist);
        var worldScale = arms.Root.GlobalTransform.Basis.Scale.Abs();
        var supportWorldScale = arms.LeftArm.GlobalTransform.Basis.Scale.Abs();
        var supportArmCorrection = (
            arms.Root.GlobalTransform.Basis * arms.LeftArm.Position).Length();
        var determinant = arms.Root.GlobalTransform.Basis.Determinant();
        var weaponRoot = ActiveAuthoredWeaponRootForDiagnostics;
        var primaryContact = InspectVisibleMeshSurface(weaponRoot, rightPalm);
        var supportContact = InspectVisibleMeshSurface(weaponRoot, leftPalm);
        var primarySurfaceOffset = weaponRootInverse.Basis * primaryContact.Offset;
        var supportSurfaceOffset = weaponRootInverse.Basis * supportContact.Offset;
        var viewportSize = _camera.GetViewport().GetVisibleRect().Size;
        var rightScreen = _camera.UnprojectPosition(rightPalm);
        var leftScreen = _camera.UnprojectPosition(leftPalm);
        var screenValid = viewportSize.X > 0.0f
            && viewportSize.Y > 0.0f
            && !_camera.IsPositionBehind(rightPalm)
            && !_camera.IsPositionBehind(leftPalm)
            && rightScreen.X >= -viewportSize.X * 0.15f
            && rightScreen.X <= viewportSize.X * 1.15f
            && rightScreen.Y >= -viewportSize.Y * 0.15f
            && rightScreen.Y <= viewportSize.Y * 1.15f
            && leftScreen.X >= -viewportSize.X * 0.15f
            && leftScreen.X <= viewportSize.X * 1.15f
            && leftScreen.Y >= -viewportSize.Y * 0.15f
            && leftScreen.Y <= viewportSize.Y * 1.15f;
        var presentationZoneValid = rightScreen.Y <= viewportSize.Y * 0.92f
            && leftScreen.Y <= viewportSize.Y * 0.92f;
        var scaleValid = worldScale.X > 0.35f
            && worldScale.X < 2.2f
            && worldScale.DistanceTo(new Vector3(worldScale.X, worldScale.X, worldScale.X)) <= 0.002f
            && supportWorldScale.DistanceTo(worldScale) <= 0.002f;
        var wristContinuity = rightWristLength is >= 0.045f and <= 0.28f
            && leftWristLength is >= 0.045f and <= 0.28f;
        var supportArmCorrectionValid = EquippedWeapon.Platform
            is not (WeaponPlatform.P226 or WeaponPlatform.M1911 or WeaponPlatform.GSh18)
            || supportArmCorrection <= MaxServicePistolSupportArmCorrection;
        var valid = gripResidual <= 0.004f
            && supportGripResidual <= 0.004f
            && Mathf.Abs(localGripSeparation - pose.PrimaryGrip.DistanceTo(pose.SupportGrip)) <= 0.01f
            && scaleValid
            && determinant > 0.01f
            && wristContinuity
            && supportArmCorrectionValid
            && primaryContact.Distance <= 0.10f
            && supportContact.Distance <= 0.10f
            && screenValid
            && presentationZoneValid;
        return new FirstPersonHandPoseInspection(
            valid,
            gripResidual,
            supportGripResidual,
            palmSeparation,
            rightWristLength,
            leftWristLength,
            worldScale,
            determinant,
            arms.Root.Transform,
            rightPalm,
            leftPalm,
            primaryContact.Distance,
            supportContact.Distance,
            primarySurfaceOffset,
            supportSurfaceOffset,
            supportArmCorrection);
    }

    internal Transform3D RealignAuthoredHandsForDiagnostics()
    {
        AlignAuthoredArmsToWeapon();
        return ActiveAuthoredArms()?.Root.Transform ?? Transform3D.Identity;
    }

    internal AuthoredM4ReloadArmInspection InspectAuthoredM4ReloadArmForDiagnostics()
    {
        var arms = ActiveAuthoredArms();
        if (EquippedWeapon.Platform != WeaponPlatform.M4A1
            || arms is null
            || !IsInstanceValid(arms.Root)
            || !IsInstanceValid(_authoredPrimaryWeapon?.Root))
        {
            return default;
        }

        var leftGrip = arms.LeftGripFrame.GlobalPosition;
        var supportTarget = M4ReloadSupportTargetGlobal();
        var primaryMagazinePosition = _authoredPrimaryWeapon.Magazine.GlobalPosition;
        var spareMagazinePosition = _authoredPrimaryWeapon.SpareMagazine.GlobalPosition;
        return new AuthoredM4ReloadArmInspection(
            arms.Root.IsVisibleInTree()
                && arms.LeftArm.IsVisibleInTree()
                && arms.LeftGripFrame.IsVisibleInTree()
                && UsesAuthoredHandRigForDiagnostics,
            arms.LeftArm.IsVisibleInTree(),
            arms.LeftGripFrame.IsVisibleInTree(),
            _authoredPrimaryWeapon.Magazine.IsVisibleInTree(),
            _authoredPrimaryWeapon.SpareMagazine.IsVisibleInTree(),
            _authoredPrimaryWeapon.Magazine.GetInstanceId()
                != _authoredPrimaryWeapon.SpareMagazine.GetInstanceId(),
            leftGrip,
            supportTarget,
            primaryMagazinePosition,
            spareMagazinePosition,
            leftGrip.DistanceTo(supportTarget),
            leftGrip.DistanceTo(spareMagazinePosition),
            arms.LeftPalmFrame.GlobalPosition.DistanceTo(arms.LeftWristFrame.GlobalPosition),
            arms.LeftArm.Transform);
    }

    private static (float Distance, Vector3 Offset) InspectVisibleMeshSurface(
        Node3D? root,
        Vector3 point)
    {
        if (root is null || !IsInstanceValid(root))
        {
            return (float.PositiveInfinity, Vector3.Zero);
        }

        var closestSquared = float.PositiveInfinity;
        var closestPoint = point;
        foreach (var meshInstance in CombatModelLibrary.MeshesBelow(root))
        {
            if (!meshInstance.IsVisibleInTree() || meshInstance.Mesh is not { } mesh)
            {
                continue;
            }

            var faces = mesh.GetFaces();
            for (var index = 0; index + 2 < faces.Length; index += 3)
            {
                var a = meshInstance.GlobalTransform * faces[index];
                var b = meshInstance.GlobalTransform * faces[index + 1];
                var c = meshInstance.GlobalTransform * faces[index + 2];
                var candidate = ClosestPointOnTriangle(point, a, b, c);
                var distanceSquared = point.DistanceSquaredTo(candidate);
                if (float.IsFinite(distanceSquared))
                {
                    if (distanceSquared < closestSquared)
                    {
                        closestSquared = distanceSquared;
                        closestPoint = candidate;
                    }
                }
            }
        }
        return (Mathf.Sqrt(closestSquared), closestPoint - point);
    }

    private static Vector3 ClosestPointOnTriangle(
        Vector3 point,
        Vector3 a,
        Vector3 b,
        Vector3 c)
    {
        var ab = b - a;
        var ac = c - a;
        var ap = point - a;
        if (ab.Cross(ac).LengthSquared() <= 0.0000000001f)
        {
            var abPoint = ClosestPointOnSegment(point, a, b);
            var bcPoint = ClosestPointOnSegment(point, b, c);
            var caPoint = ClosestPointOnSegment(point, c, a);
            var closest = abPoint;
            if (point.DistanceSquaredTo(bcPoint) < point.DistanceSquaredTo(closest))
            {
                closest = bcPoint;
            }
            if (point.DistanceSquaredTo(caPoint) < point.DistanceSquaredTo(closest))
            {
                closest = caPoint;
            }
            return closest;
        }

        var d1 = ab.Dot(ap);
        var d2 = ac.Dot(ap);
        if (d1 <= 0.0f && d2 <= 0.0f)
        {
            return a;
        }

        var bp = point - b;
        var d3 = ab.Dot(bp);
        var d4 = ac.Dot(bp);
        if (d3 >= 0.0f && d4 <= d3)
        {
            return b;
        }

        var vc = d1 * d4 - d3 * d2;
        if (vc <= 0.0f && d1 >= 0.0f && d3 <= 0.0f)
        {
            var projection = d1 / (d1 - d3);
            return a + projection * ab;
        }

        var cp = point - c;
        var d5 = ab.Dot(cp);
        var d6 = ac.Dot(cp);
        if (d6 >= 0.0f && d5 <= d6)
        {
            return c;
        }

        var vb = d5 * d2 - d1 * d6;
        if (vb <= 0.0f && d2 >= 0.0f && d6 <= 0.0f)
        {
            var projection = d2 / (d2 - d6);
            return a + projection * ac;
        }

        var va = d3 * d6 - d5 * d4;
        if (va <= 0.0f && d4 - d3 >= 0.0f && d5 - d6 >= 0.0f)
        {
            var projection = (d4 - d3) / ((d4 - d3) + (d5 - d6));
            return b + projection * (c - b);
        }

        var divisor = va + vb + vc;
        if (Mathf.Abs(divisor) <= 0.0000000001f)
        {
            return ClosestPointOnSegment(point, a, b);
        }

        var denominator = 1.0f / divisor;
        var v = vb * denominator;
        var w = vc * denominator;
        return a + ab * v + ac * w;
    }

    private static Vector3 ClosestPointOnSegment(
        Vector3 point,
        Vector3 start,
        Vector3 end)
    {
        var segment = end - start;
        var lengthSquared = segment.LengthSquared();
        if (lengthSquared <= 0.0000000001f)
        {
            return start;
        }

        var amount = Mathf.Clamp((point - start).Dot(segment) / lengthSquared, 0.0f, 1.0f);
        return start + segment * amount;
    }

    private void BuildAuthoredPrimaryWeapon()
    {
        try
        {
            var authoredWeapon = CombatModelLibrary.InstantiateWeapon(firstPerson: true);
            _weaponRoot.AddChild(authoredWeapon.Root);
            _authoredPrimaryWeapon = authoredWeapon;
        }
        catch (Exception exception)
        {
            GD.PushWarning($"Authored primary weapon unavailable; retaining procedural visual: {exception.Message}");
        }
        RefreshAuthoredPrimaryWeapon();
    }

    private void RefreshAuthoredPrimaryWeapon()
    {
        var useAuthoredM4 = EquippedWeapon.Platform == WeaponPlatform.M4A1;
        var wantsAuthoredSmg = EquippedWeapon.Platform == WeaponPlatform.M3A1;
        var useAuthoredSmg = wantsAuthoredSmg;
        var useAuthoredPlatform = EquippedWeapon.Platform is not WeaponPlatform.M4A1
            and not WeaponPlatform.M3A1
            and not WeaponPlatform.GSh18
            and not WeaponPlatform.DesertEagle;
        var useAuthoredGsh18 = EquippedWeapon.Platform == WeaponPlatform.GSh18;
        var wantsAuthoredDesertEagle = EquippedWeapon.Platform == WeaponPlatform.DesertEagle;
        var useAuthoredDesertEagle = wantsAuthoredDesertEagle;
        if (EquippedWeapon.Platform != WeaponPlatform.M3A1)
        {
            EnsureAuthoredArmsForPlatform();
        }
        if (useAuthoredGsh18)
        {
            EnsureAuthoredGsh18Weapon();
        }
        if (useAuthoredSmg)
        {
            EnsureAuthoredFirstPersonSmg();
        }
        if (useAuthoredDesertEagle)
        {
            EnsureAuthoredDesertEagleWeapon();
        }
        if (useAuthoredPlatform)
        {
            EnsureAuthoredPlatformWeapon(EquippedWeapon.Platform);
        }
        if (IsInstanceValid(_authoredPrimaryWeapon?.Root))
        {
            _authoredPrimaryWeapon.Root.Visible = useAuthoredM4;
            if (useAuthoredM4)
            {
                _authoredPrimaryWeapon.Configure(EquippedWeapon);
                SyncAuthoredPrimaryWeapon();
            }
        }
        else
        {
            useAuthoredM4 = false;
        }
        if (IsInstanceValid(_authoredFirstPersonSmg?.Root))
        {
            _authoredFirstPersonSmg.Root.Visible = useAuthoredSmg;
            if (useAuthoredSmg)
            {
                _authoredFirstPersonSmg.SyncMechanisms();
                _authoredFirstPersonSmg.SetReloadProgress(_isReloading ? ReloadProgress : 0.0f);
            }
        }
        else
        {
            useAuthoredSmg = false;
        }
        if (IsInstanceValid(_authoredGsh18Weapon?.Root))
        {
            _authoredGsh18Weapon.Root.Visible = useAuthoredGsh18;
        }
        else
        {
            useAuthoredGsh18 = false;
        }
        if (IsInstanceValid(_authoredDesertEagleWeapon?.Root))
        {
            _authoredDesertEagleWeapon.Root.Visible = useAuthoredDesertEagle;
        }
        else
        {
            useAuthoredDesertEagle = false;
        }
        foreach (var pair in _authoredPlatformWeapons)
        {
            var active = useAuthoredPlatform && pair.Key == EquippedWeapon.Platform;
            pair.Value.Root.Visible = active;
            if (active)
            {
                pair.Value.Configure(EquippedWeapon);
                SyncAuthoredPlatformWeapon();
            }
        }
        useAuthoredPlatform &= _authoredPlatformWeapons.TryGetValue(
            EquippedWeapon.Platform,
            out var activePlatformWeapon)
            && IsInstanceValid(activePlatformWeapon.Root);
        var activeAuthoredArms = ActiveAuthoredArms();
        SetAuthoredArmsVisible(
            _authoredRifleArms,
            ReferenceEquals(activeAuthoredArms, _authoredRifleArms));
        SetAuthoredArmsVisible(
            _authoredPistolServiceArms,
            ReferenceEquals(activeAuthoredArms, _authoredPistolServiceArms));
        SetAuthoredArmsVisible(
            _authoredPistolLargeArms,
            ReferenceEquals(activeAuthoredArms, _authoredPistolLargeArms));
        var useAuthoredArms = activeAuthoredArms is not null
            && IsInstanceValid(activeAuthoredArms.Root);
        if (useAuthoredArms)
        {
            AlignAuthoredArmsToWeapon();
        }
        if (IsInstanceValid(_proceduralFirstPersonArms))
        {
            _proceduralFirstPersonArms.Visible = !useAuthoredSmg && !useAuthoredArms;
        }
        _proceduralWeaponVisual.Visible = !useAuthoredM4
            && !useAuthoredSmg
            && !useAuthoredGsh18
            && !wantsAuthoredDesertEagle
            && !useAuthoredPlatform;
        ApplyProceduralHandPose();
    }

    private void ApplyProceduralHandPose()
    {
        if (!IsInstanceValid(_primaryHand)
            || !IsInstanceValid(_primaryForearm)
            || !IsInstanceValid(_supportHand)
            || !IsInstanceValid(_supportForearm))
        {
            return;
        }

        // Every platform gets its own grip and support point. The old fixed M4 pose
        // left the support hand floating on pistols and short/long guns.
        var aimingDrop = _isAiming && WeaponCatalog.IsSidearm(EquippedWeapon.Platform) ? -0.045f : 0.0f;
        var primary = EquippedWeapon.Platform switch
        {
            WeaponPlatform.P226 or WeaponPlatform.M1911 or WeaponPlatform.GSh18
                or WeaponPlatform.DesertEagle => new Vector3(0.10f, -0.135f + aimingDrop, 0.02f),
            WeaponPlatform.MP5A5 or WeaponPlatform.M3A1 => new Vector3(0.11f, -0.18f, -0.08f),
            WeaponPlatform.M24 or WeaponPlatform.AXMC or WeaponPlatform.AWM => new Vector3(0.0f, -0.17f, 0.02f),
            _ => RifleGripAnchor
        };
        var support = EquippedWeapon.Platform switch
        {
            WeaponPlatform.P226 or WeaponPlatform.M1911 or WeaponPlatform.GSh18
                or WeaponPlatform.DesertEagle => new Vector3(-0.035f, -0.14f + aimingDrop, -0.13f),
            WeaponPlatform.MP5A5 or WeaponPlatform.M3A1 => new Vector3(-0.035f, -0.19f, -0.42f),
            WeaponPlatform.M24 or WeaponPlatform.AXMC => new Vector3(-0.02f, -0.19f, -0.72f),
            WeaponPlatform.AWM => new Vector3(-0.02f, -0.19f, -0.86f),
            WeaponPlatform.VSS => new Vector3(-0.02f, -0.18f, -0.62f),
            _ => RifleForegripAnchor
        };

        _primaryHand.Position = primary;
        _primaryHand.Rotation = WeaponCatalog.IsSidearm(EquippedWeapon.Platform)
            ? new Vector3(-0.16f, 0.08f, -0.14f)
            : new Vector3(-0.12f, 0.05f, -0.18f);
        _supportHand.Position = support;
        _supportHand.Rotation = WeaponCatalog.IsSidearm(EquippedWeapon.Platform)
            ? new Vector3(0.16f, -0.04f, 0.18f)
            : new Vector3(0.2f, 0.0f, 0.05f);
        _primaryForearm.Position = primary + new Vector3(0.08f, -0.25f, 0.09f);
        _primaryForearm.Rotation = new Vector3(-0.18f, 0.05f, -0.3f);
        _supportForearm.Position = support + new Vector3(-0.09f, -0.24f, 0.1f);
        _supportForearm.Rotation = new Vector3(0.22f, 0.05f, -0.28f);
    }

    private void EnsureAuthoredFirstPersonSmg()
    {
        if (_smg45LoadAttempted || IsInstanceValid(_authoredFirstPersonSmg?.Root))
        {
            return;
        }
        _smg45LoadAttempted = true;
        try
        {
            var authoredSmg = CombatModelLibrary.InstantiateFirstPersonSmg45();
            var inheritedScale = Mathf.Max(0.0001f, _weaponRoot.Scale.X);
            authoredSmg.Root.Scale = Vector3.One * (AuthoredSmgPresentationScale / inheritedScale);
            authoredSmg.Root.Position =
                (AuthoredSmgCameraPosition - _weaponRoot.Position) / inheritedScale;
            authoredSmg.Root.RotationDegrees = new Vector3(0.0f, 180.0f, 0.0f);
            _weaponRoot.AddChild(authoredSmg.Root);
            _authoredFirstPersonSmg = authoredSmg;
            _authoredFirstPersonSmg.SetReloadProgress(0.0f);
        }
        catch (Exception exception)
        {
            GD.PushError($"Required authored SMG-45 first-person model unavailable: {exception.Message}");
        }
    }

    private void EnsureAuthoredPlatformWeapon(WeaponPlatform platform)
    {
        if (_authoredPlatformWeapons.TryGetValue(platform, out var existing)
            && IsInstanceValid(existing.Root))
        {
            return;
        }
        try
        {
            var authoredWeapon = CombatModelLibrary.InstantiateWeapon(platform, firstPerson: true);
            authoredWeapon.Configure(EquippedWeapon);
            _weaponRoot.AddChild(authoredWeapon.Root);
            _authoredPlatformWeapons[platform] = authoredWeapon;
        }
        catch (Exception exception)
        {
            GD.PushError($"Required authored {platform} weapon unavailable: {exception.Message}");
        }
    }

    private void EnsureAuthoredArmsForPlatform()
    {
        if (EquippedWeapon.Platform == WeaponPlatform.DesertEagle)
        {
            EnsureAuthoredPistolLargeArms();
            return;
        }
        if (WeaponCatalog.IsSidearm(EquippedWeapon.Platform))
        {
            EnsureAuthoredPistolServiceArms();
            return;
        }
        EnsureAuthoredRifleArms();
    }

    private void EnsureAuthoredRifleArms()
    {
        if (_rifleArmsLoadAttempted || IsInstanceValid(_authoredRifleArms?.Root))
        {
            return;
        }
        _rifleArmsLoadAttempted = true;
        try
        {
            var arms = CombatModelLibrary.InstantiateFirstPersonRifleArms();
            _weaponRoot.AddChild(arms.Root);
            _authoredRifleArms = arms;
        }
        catch (Exception exception)
        {
            GD.PushWarning($"Authored rifle arms unavailable; retaining procedural hands: {exception.Message}");
        }
    }

    private void EnsureAuthoredPistolServiceArms()
    {
        if (_pistolServiceArmsLoadAttempted
            || IsInstanceValid(_authoredPistolServiceArms?.Root))
        {
            return;
        }
        _pistolServiceArmsLoadAttempted = true;
        try
        {
            var arms = CombatModelLibrary.InstantiateFirstPersonPistolServiceArms();
            _weaponRoot.AddChild(arms.Root);
            _authoredPistolServiceArms = arms;
        }
        catch (Exception exception)
        {
            GD.PushWarning($"Authored pistol arms unavailable; retaining procedural hands: {exception.Message}");
        }
    }

    private void EnsureAuthoredPistolLargeArms()
    {
        if (_pistolLargeArmsLoadAttempted
            || IsInstanceValid(_authoredPistolLargeArms?.Root))
        {
            return;
        }
        _pistolLargeArmsLoadAttempted = true;
        try
        {
            var arms = CombatModelLibrary.InstantiateFirstPersonPistolLargeArms();
            _weaponRoot.AddChild(arms.Root);
            _authoredPistolLargeArms = arms;
        }
        catch (Exception exception)
        {
            GD.PushWarning($"Authored large-pistol arms unavailable; retaining procedural hands: {exception.Message}");
        }
    }

    private AuthoredFirstPersonArmsVisual? ActiveAuthoredArms()
        => EquippedWeapon.Platform switch
        {
            WeaponPlatform.M3A1 => null,
            WeaponPlatform.DesertEagle
                when IsInstanceValid(_authoredPistolLargeArms?.Root)
                => _authoredPistolLargeArms,
            _ when WeaponCatalog.IsSidearm(EquippedWeapon.Platform)
                && IsInstanceValid(_authoredPistolServiceArms?.Root)
                => _authoredPistolServiceArms,
            _ when IsInstanceValid(_authoredRifleArms?.Root)
                => _authoredRifleArms,
            _ => null
        };

    private static void SetAuthoredArmsVisible(
        AuthoredFirstPersonArmsVisual? arms,
        bool visible)
    {
        if (arms is not null && GodotObject.IsInstanceValid(arms.Root))
        {
            arms.Root.Visible = visible;
        }
    }

    private void AlignAuthoredArmsToWeapon()
    {
        var arms = ActiveAuthoredArms();
        if (arms is null || !IsInstanceValid(arms.Root))
        {
            return;
        }

        var pose = FirstPersonArmPoseCatalog.For(EquippedWeapon.Platform);
        var inheritedScale = Mathf.Max(0.0001f, _weaponRoot.Scale.X);
        arms.RightArm.Transform = Transform3D.Identity;
        arms.LeftArm.Transform = Transform3D.Identity;
        arms.RightArm.Visible = true;
        arms.LeftArm.Visible = true;
        var targetFrame = new Transform3D(
            new Basis(Vector3.Up, Mathf.Pi).Scaled(
                Vector3.One * (AuthoredArmPresentationScale / inheritedScale)),
            pose.PrimaryGrip);
        arms.Root.Transform = targetFrame * arms.RightGripTransformInRoot.AffineInverse();
        var supportTargetInArms = arms.Root.Transform.AffineInverse() * pose.SupportGrip;
        var supportGripInArms = arms.MarkerTransformInRoot(arms.LeftGripFrame).Origin;
        arms.LeftArm.Position += supportTargetInArms - supportGripInArms;
    }

    private void UpdateAuthoredM4ReloadSupportArm()
    {
        if (!_isReloading
            || EquippedWeapon.Platform != WeaponPlatform.M4A1
            || ActiveAuthoredArms() is not { } arms
            || !IsInstanceValid(arms.Root)
            || !arms.Root.Visible
            || !IsInstanceValid(_authoredPrimaryWeapon?.Root))
        {
            return;
        }

        // Start from the authored two-hand hold every frame so fixed diagnostic
        // samples and normal playback produce the same pose. Swing the complete
        // arm around its authored node pivot before applying the small reach
        // correction that puts the visible grip marker on the magazine handoff.
        // Because the sleeve, hand, wrist and markers share LeftArm as their
        // parent, this keeps the authored sleeve continuous throughout the move.
        AlignAuthoredArmsToWeapon();
        var restTransform = arms.LeftArm.Transform;
        var restGripInArms = arms.MarkerTransformInRoot(arms.LeftGripFrame).Origin;
        var targetInArms = arms.Root.GlobalTransform.AffineInverse()
            * M4ReloadSupportTargetGlobal();
        var restReach = restGripInArms - restTransform.Origin;
        var targetReach = targetInArms - restTransform.Origin;
        if (restReach.LengthSquared() > 0.000001f
            && targetReach.LengthSquared() > 0.000001f)
        {
            var pivotSwing = new Quaternion(restReach.Normalized(), targetReach.Normalized());
            arms.LeftArm.Transform = new Transform3D(
                new Basis(pivotSwing) * restTransform.Basis,
                restTransform.Origin);
        }

        var rotatedGripInArms = arms.MarkerTransformInRoot(arms.LeftGripFrame).Origin;
        arms.LeftArm.Position += targetInArms - rotatedGripInArms;
    }

    private Vector3 M4ReloadSupportTargetGlobal()
        => IsInstanceValid(_supportHand)
            ? _supportHand.GlobalPosition
            : Vector3.Zero;

    private void ResetAuthoredM4ReloadSupportArm()
    {
        if (EquippedWeapon.Platform == WeaponPlatform.M4A1)
        {
            AlignAuthoredArmsToWeapon();
        }
    }

    private void EnsureAuthoredDesertEagleWeapon()
    {
        if (_desertEagleLoadAttempted || IsInstanceValid(_authoredDesertEagleWeapon?.Root))
        {
            return;
        }
        _desertEagleLoadAttempted = true;
        try
        {
            var authoredWeapon = CombatModelLibrary.InstantiateDesertEagle(firstPerson: true);
            authoredWeapon.Root.Position = new Vector3(0.0f, -0.04f, -0.02f);
            _weaponRoot.AddChild(authoredWeapon.Root);
            _authoredDesertEagleWeapon = authoredWeapon;
        }
        catch (Exception exception)
        {
            GD.PushError($"Required authored Desert Eagle unavailable: {exception.Message}");
        }
    }

    private void EnsureAuthoredGsh18Weapon()
    {
        if (_gsh18LoadAttempted || IsInstanceValid(_authoredGsh18Weapon?.Root))
        {
            return;
        }
        _gsh18LoadAttempted = true;
        try
        {
            var authoredWeapon = CombatModelLibrary.InstantiateGsh18(firstPerson: true);
            authoredWeapon.Root.Position = new Vector3(0.0f, -0.04f, -0.16f);
            _weaponRoot.AddChild(authoredWeapon.Root);
            _authoredGsh18Weapon = authoredWeapon;
        }
        catch (Exception exception)
        {
            GD.PushWarning($"Authored GSh-18 unavailable; retaining procedural visual: {exception.Message}");
        }
    }

    private void SyncAuthoredPrimaryWeapon()
    {
        if (EquippedWeapon.Platform == WeaponPlatform.M4A1
            && IsInstanceValid(_authoredPrimaryWeapon?.Root))
        {
            _authoredPrimaryWeapon.SyncMechanisms(_magazine, _spareMagazine, _chargingHandle);
            _muzzle.GlobalTransform = _authoredPrimaryWeapon.ActiveMuzzleTip.GlobalTransform;
            if (_authoredPrimaryWeapon.OpticMount.Visible && _opticRoot.Visible)
            {
                _opticRoot.GlobalPosition = _authoredPrimaryWeapon.OpticReticleAnchor.GlobalPosition;
                _opticReticle.Position = Vector3.Zero;
            }
        }
        if (EquippedWeapon.Platform == WeaponPlatform.M3A1
            && IsInstanceValid(_authoredFirstPersonSmg?.Root))
        {
            _authoredFirstPersonSmg.SyncMechanisms();
            _authoredFirstPersonSmg.SetReloadProgress(_isReloading ? ReloadProgress : 0.0f);
            _muzzle.GlobalTransform = _authoredFirstPersonSmg.Muzzle.GlobalTransform;
        }
        SyncAuthoredPlatformWeapon();
    }

    private void SyncAuthoredPlatformWeapon()
    {
        if (!_authoredPlatformWeapons.TryGetValue(EquippedWeapon.Platform, out var authoredWeapon)
            || !IsInstanceValid(authoredWeapon.Root))
        {
            return;
        }
        authoredWeapon.SyncMechanismState(_magazine, _spareMagazine, _chargingHandle);
    }
}

internal readonly record struct FirstPersonHandPoseInspection(
    bool Valid,
    float GripResidual,
    float SupportGripResidual,
    float PalmSeparation,
    float RightWristLength,
    float LeftWristLength,
    Vector3 RootScale,
    float RootDeterminant,
    Transform3D RootTransform,
    Vector3 RightPalm,
    Vector3 LeftPalm,
    float PrimarySurfaceDistance,
    float SupportSurfaceDistance,
    Vector3 PrimarySurfaceOffset,
    Vector3 SupportSurfaceOffset,
    float SupportArmCorrection);

internal readonly record struct AuthoredM4ReloadArmInspection(
    bool AuthoredArmActive,
    bool LeftArmVisible,
    bool LeftGripFrameActive,
    bool PrimaryMagazineVisible,
    bool SpareMagazineVisible,
    bool SeparateMagazineNodes,
    Vector3 LeftGrip,
    Vector3 SupportTarget,
    Vector3 PrimaryMagazinePosition,
    Vector3 SpareMagazinePosition,
    float SupportTargetDistance,
    float ActiveMagazineDistance,
    float SleeveWristLength,
    Transform3D LeftArmTransform);
