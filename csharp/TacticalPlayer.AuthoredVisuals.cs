using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public partial class TacticalPlayer
{
    private const float AuthoredSmgPresentationScale = 0.72f;
    private static readonly Vector3 AuthoredSmgCameraPosition = new(0.34f, -0.45f, -0.72f);

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
        var rightWrist = arms.RightWristFrame.GlobalPosition;
        var leftWrist = arms.LeftWristFrame.GlobalPosition;
        var rightPalmInWeaponRoot = _weaponRoot.GlobalTransform.AffineInverse() * rightPalm;
        var leftPalmInWeaponRoot = _weaponRoot.GlobalTransform.AffineInverse() * leftPalm;
        var pose = FirstPersonArmPoseCatalog.For(EquippedWeapon.Platform);
        var gripResidual = rightPalmInWeaponRoot.DistanceTo(pose.PrimaryGrip);
        var supportGripResidual = leftPalmInWeaponRoot.DistanceTo(pose.SupportGrip);
        var palmSeparation = rightPalm.DistanceTo(leftPalm);
        var localPalmSeparation = rightPalmInWeaponRoot.DistanceTo(leftPalmInWeaponRoot);
        var rightWristLength = rightPalm.DistanceTo(rightWrist);
        var leftWristLength = leftPalm.DistanceTo(leftWrist);
        var worldScale = arms.Root.GlobalTransform.Basis.Scale.Abs();
        var determinant = arms.Root.GlobalTransform.Basis.Determinant();
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
            && worldScale.DistanceTo(new Vector3(worldScale.X, worldScale.X, worldScale.X)) <= 0.002f;
        var wristContinuity = rightWristLength is >= 0.045f and <= 0.28f
            && leftWristLength is >= 0.045f and <= 0.28f;
        var valid = gripResidual <= 0.004f
            && supportGripResidual <= 0.06f
            && Mathf.Abs(localPalmSeparation - pose.PrimaryGrip.DistanceTo(pose.SupportGrip)) <= 0.01f
            && scaleValid
            && determinant > 0.01f
            && wristContinuity
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
            leftPalm);
    }

    internal Transform3D RealignAuthoredHandsForDiagnostics()
    {
        AlignAuthoredArmsToWeapon();
        return ActiveAuthoredArms()?.Root.Transform ?? Transform3D.Identity;
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
            PrepareAuthoredArmsPresentation(arms);
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
            PrepareAuthoredArmsPresentation(arms);
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
            PrepareAuthoredArmsPresentation(arms);
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

        // The Blender exports already use the camera-facing handedness. Apply only
        // the weapon-root translation and preserve authored scale; an extra 180° Y
        // flip mirrors the palms and makes the forearms curl back toward the camera.
        var pose = FirstPersonArmPoseCatalog.For(EquippedWeapon.Platform);
        var rightPalm = arms.RightPalmTransformInRoot.Origin;
        var leftPalm = arms.LeftPalmTransformInRoot.Origin;
        var current = leftPalm - rightPalm;
        var target = pose.SupportGrip - pose.PrimaryGrip;
        if (current.LengthSquared() <= 0.0001f || target.LengthSquared() <= 0.0001f)
        {
            return;
        }

        // Match each weapon-family palm span with a uniform scale. This preserves
        // the authored proportions while keeping the support palm on the weapon;
        // a single scale for every platform makes compact guns and long rifles
        // miss their front grip by a visible amount.
        var scale = target.Length() / current.Length();
        var rotation = new Quaternion(current.Normalized(), target.Normalized());
        if (Mathf.Abs(pose.RollDegrees) > 0.01f)
        {
            var roll = new Quaternion(
                target.Normalized(),
                Mathf.DegToRad(pose.RollDegrees));
            rotation = roll * rotation;
        }
        var basis = new Basis(rotation).Scaled(Vector3.One * scale);
        arms.Root.Transform = new Transform3D(
            basis,
            pose.PrimaryGrip - basis * rightPalm);
    }

    private void PrepareAuthoredArmsPresentation(AuthoredFirstPersonArmsVisual arms)
    {
        var inheritedScale = Mathf.Max(0.0001f, _weaponRoot.Scale.X);
        arms.Root.Scale = Vector3.One / inheritedScale;
    }

    private static Vector3 FirstPersonRightHandAnchor(WeaponPlatform platform)
        => FirstPersonArmPoseCatalog.For(platform).PrimaryGrip;

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
    Vector3 LeftPalm);
