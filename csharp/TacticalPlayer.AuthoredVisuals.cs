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
        var isRifleNeedingAuthoredArms = EquippedWeapon.Platform is WeaponPlatform.M4A1
            or WeaponPlatform.AK74 or WeaponPlatform.ScarL or WeaponPlatform.M24
            or WeaponPlatform.AXMC or WeaponPlatform.AWM or WeaponPlatform.VSS
            or WeaponPlatform.MP5A5;
        if (useAuthoredM4 || isRifleNeedingAuthoredArms)
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
        var useAuthoredRifleArms = isRifleNeedingAuthoredArms
            && IsInstanceValid(_authoredRifleArms?.Root);
        if (IsInstanceValid(_authoredRifleArms?.Root))
        {
            _authoredRifleArms.Root.Visible = isRifleNeedingAuthoredArms;
            if (isRifleNeedingAuthoredArms)
            {
                AlignRifleArmsToWeapon();
            }
        }
        if (IsInstanceValid(_proceduralFirstPersonArms))
        {
            _proceduralFirstPersonArms.Visible = !useAuthoredSmg && !useAuthoredRifleArms;
        }
        _proceduralWeaponVisual.Visible = !useAuthoredM4
            && !useAuthoredSmg
            && !useAuthoredGsh18
            && !wantsAuthoredDesertEagle
            && !useAuthoredPlatform;
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
            AlignRifleArmsToWeapon();
        }
        catch (Exception exception)
        {
            GD.PushWarning($"Authored rifle arms unavailable; retaining procedural hands: {exception.Message}");
        }
    }

    private void AlignRifleArmsToWeapon()
    {
        if (!IsInstanceValid(_authoredRifleArms?.Root))
        {
            return;
        }
        var toLocal = _weaponRoot.GlobalTransform.AffineInverse();
        var right = toLocal * _authoredRifleArms.PalmPosition("R_palm_039");
        var left = toLocal * _authoredRifleArms.PalmPosition("L_palm_015");
        var current = left - right;
        // Use weapon-specific fore-grip anchor so MP5A5/M24 etc. don't stretch to M4A1's -0.58
        var target = GetRifleForegripAnchorForCurrentWeapon() - RifleGripAnchor;
        if (current.LengthSquared() < 0.0001f || target.LengthSquared() < 0.0001f)
        {
            return;
        }
        var scale = target.Length() / current.Length();
        var fromNorm = current.Normalized();
        var toNorm = target.Normalized();
        var axis = fromNorm.Cross(toNorm);
        var angle = fromNorm.AngleTo(toNorm);
        Basis basis;
        if (axis.LengthSquared() < 0.0001f)
        {
            if (fromNorm.Dot(toNorm) > 0.0f)
            {
                basis = Basis.Identity.Scaled(Vector3.One * scale);
                angle = 0.0f;
            }
            else
            {
                axis = Vector3.Up;
                angle = Mathf.Pi;
                var quat = new Quaternion(axis, angle);
                basis = new Basis(quat).Scaled(Vector3.One * scale);
            }
        }
        else
        {
            var quat = new Quaternion(axis.Normalized(), angle);
            basis = new Basis(quat).Scaled(Vector3.One * scale);
        }
        _authoredRifleArms.Root.Basis = basis;
        _authoredRifleArms.Root.Position = RifleGripAnchor - basis * right;
        var toLocalAfter = _weaponRoot.GlobalTransform.AffineInverse();
        var residual = (toLocalAfter * _authoredRifleArms.PalmPosition("R_palm_039") - RifleGripAnchor).Length();
        var yaw = Mathf.Atan2(toNorm.X, -toNorm.Z) - Mathf.Atan2(fromNorm.X, -fromNorm.Z);
        GD.Print($"RIFLE_ARMS_ALIGN grip_residual={residual:F4} scale={scale:F3} angle_deg={Mathf.RadToDeg(angle):F1} yaw_deg={Mathf.RadToDeg(yaw):F1}");
    }

    private Vector3 GetRifleForegripAnchorForCurrentWeapon()
    {
        // Keep M4A1's authored anchor, adjust others by barrel length so the support hand doesn't over-stretch
        var platform = EquippedWeapon.Platform;
        var baseAnchor = RifleForegripAnchor;
        var extraForward = platform switch
        {
            WeaponPlatform.AK74 => -0.04f,
            WeaponPlatform.ScarL => -0.05f,
            WeaponPlatform.M24 => -0.12f,
            WeaponPlatform.AXMC => -0.18f,
            WeaponPlatform.AWM => -0.20f,
            WeaponPlatform.VSS => -0.06f,
            WeaponPlatform.MP5A5 => 0.08f,
            _ => 0.0f
        };
        return baseAnchor + new Vector3(0, 0, extraForward);
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
