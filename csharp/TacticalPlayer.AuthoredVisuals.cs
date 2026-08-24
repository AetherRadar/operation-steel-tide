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
    private bool _rifleArmsLoadAttempted;

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
    internal bool UsesAuthoredHandRigForDiagnostics
    {
        get
        {
            var arms = _authoredRifleArms;
            return EquippedWeapon.Platform != WeaponPlatform.M3A1
                && arms is not null
                && IsInstanceValid(arms.Root)
                && arms.Root.Visible;
        }
    }

    internal FirstPersonArmRigSnapshot InspectFirstPersonArmRigForDiagnostics()
    {
        var platform = EquippedWeapon.Platform;
        var pose = FirstPersonArmPoseCatalog.For(platform);
        var proceduralVisible = IsInstanceValid(_proceduralFirstPersonArms)
            && _proceduralFirstPersonArms.Visible;
        if (platform == WeaponPlatform.M3A1)
        {
            var smg = _authoredFirstPersonSmg;
            var active = smg is not null
                && IsInstanceValid(smg.Root)
                && smg.Root.Visible;
            var primaryGlobal = active
                ? smg!.PalmPosition("R_palm_039")
                : Vector3.Zero;
            var supportGlobal = active
                ? smg!.PalmPosition("L_palm_015")
                : Vector3.Zero;
            return BuildArmRigSnapshot(
                platform,
                pose,
                active,
                nativeSmgRig: true,
                proceduralVisible,
                active ? smg!.Root.GetInstanceId() : 0,
                active ? smg!.Root.Scale : Vector3.Zero,
                primaryGlobal,
                supportGlobal,
                0.0f,
                0.0f);
        }

        var arms = _authoredRifleArms;
        var authoredActive = arms is not null
            && IsInstanceValid(arms.Root)
            && arms.Root.Visible;
        var primaryPalm = authoredActive
            ? arms!.PrimaryPalmPosition
            : Vector3.Zero;
        var supportPalm = authoredActive
            ? arms!.SupportPalmPosition
            : Vector3.Zero;
        var toWeaponLocal = _weaponRoot.GlobalTransform.AffineInverse();
        var primaryLocal = toWeaponLocal * primaryPalm;
        var supportLocal = toWeaponLocal * supportPalm;
        return BuildArmRigSnapshot(
            platform,
            pose,
            authoredActive,
            nativeSmgRig: false,
            proceduralVisible,
            authoredActive ? arms!.Root.GetInstanceId() : 0,
            authoredActive ? arms!.Root.Scale : Vector3.Zero,
            primaryPalm,
            supportPalm,
            primaryLocal.DistanceTo(pose.PrimaryGrip),
            supportLocal.DistanceTo(pose.SupportGrip));
    }

    private FirstPersonArmRigSnapshot BuildArmRigSnapshot(
        WeaponPlatform platform,
        FirstPersonArmPoseDefinition pose,
        bool active,
        bool nativeSmgRig,
        bool proceduralVisible,
        ulong instanceId,
        Vector3 rootScale,
        Vector3 primaryGlobal,
        Vector3 supportGlobal,
        float primaryGripError,
        float supportGripError)
    {
        var viewportSize = _camera.GetViewport().GetVisibleRect().Size;
        var primaryScreen = active && viewportSize.X > 0.0f && viewportSize.Y > 0.0f
            ? _camera.UnprojectPosition(primaryGlobal) / viewportSize
            : Vector2.Zero;
        var supportScreen = active && viewportSize.X > 0.0f && viewportSize.Y > 0.0f
            ? _camera.UnprojectPosition(supportGlobal) / viewportSize
            : Vector2.Zero;
        var toWeaponLocal = _weaponRoot.GlobalTransform.AffineInverse();
        return new FirstPersonArmRigSnapshot(
            platform,
            pose.Kind,
            active,
            nativeSmgRig,
            proceduralVisible,
            instanceId,
            rootScale,
            toWeaponLocal * primaryGlobal,
            toWeaponLocal * supportGlobal,
            primaryGlobal,
            supportGlobal,
            primaryGripError,
            supportGripError,
            primaryScreen,
            supportScreen,
            active && _camera.IsPositionBehind(primaryGlobal),
            active && _camera.IsPositionBehind(supportGlobal));
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
            EnsureAuthoredRifleArms();
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
        var useAuthoredRifleArms = EquippedWeapon.Platform != WeaponPlatform.M3A1
            && IsInstanceValid(_authoredRifleArms?.Root);
        if (IsInstanceValid(_authoredRifleArms?.Root))
        {
            _authoredRifleArms.Root.Visible = useAuthoredRifleArms;
            if (useAuthoredRifleArms)
            {
                ApplyAuthoredArmPose();
            }
        }
        if (IsInstanceValid(_proceduralFirstPersonArms))
        {
            _proceduralFirstPersonArms.Visible = false;
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
            _ => FirstPersonArmPoseCatalog.For(EquippedWeapon.Platform).PrimaryGrip
        };
        var support = EquippedWeapon.Platform switch
        {
            WeaponPlatform.P226 or WeaponPlatform.M1911 or WeaponPlatform.GSh18
                or WeaponPlatform.DesertEagle => new Vector3(-0.035f, -0.14f + aimingDrop, -0.13f),
            WeaponPlatform.MP5A5 or WeaponPlatform.M3A1 => new Vector3(-0.035f, -0.19f, -0.42f),
            WeaponPlatform.M24 or WeaponPlatform.AXMC => new Vector3(-0.02f, -0.19f, -0.72f),
            WeaponPlatform.AWM => new Vector3(-0.02f, -0.19f, -0.86f),
            WeaponPlatform.VSS => new Vector3(-0.02f, -0.18f, -0.62f),
            _ => FirstPersonArmPoseCatalog.For(EquippedWeapon.Platform).SupportGrip
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
            authoredSmg.Root.Scale *= AuthoredSmgPresentationScale / inheritedScale;
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

    private void EnsureAuthoredRifleArms()
    {
        if (_rifleArmsLoadAttempted || IsInstanceValid(_authoredRifleArms?.Root))
        {
            return;
        }
        _rifleArmsLoadAttempted = true;
        try
        {
            var arms = CombatModelLibrary.InstantiateFirstPersonArms();
            // DJMaesen SMG-45 is authored facing +Z; the full SMG is flipped 180 for first-person.
            // Rifle arms must match the same flip, otherwise the palms face backwards (正面顺序反).
            arms.Root.RotationDegrees = new Vector3(0.0f, 180.0f, 0.0f);
            _weaponRoot.AddChild(arms.Root);
            _authoredRifleArms = arms;
            ApplyAuthoredArmPose();
        }
        catch (Exception exception)
        {
            GD.PushError($"Required authored first-person arms unavailable: {exception.Message}");
        }
    }

    private void ApplyAuthoredArmPose()
    {
        if (!IsInstanceValid(_authoredRifleArms?.Root))
        {
            return;
        }
        _authoredRifleArms.ApplyPose(FirstPersonArmPoseCatalog.For(EquippedWeapon.Platform));
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
            GD.PushError($"Required authored GSh-18 weapon unavailable: {exception.Message}");
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
