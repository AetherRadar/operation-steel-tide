using Godot;

namespace OperationSteelTide;

public partial class TacticalPlayer
{
    private static readonly Vector3 HipWeaponPosition = new(0.34f, -0.33f, -0.58f);
    private static readonly Vector3 ReloadWeaponPosition = new(0.18f, -0.23f, -0.86f);
    private static readonly Vector3 SearchWeaponStart = new(0.5f, -0.58f, -0.48f);
    private static readonly Vector3 SearchWeaponEnd = new(0.32f, -0.48f, -0.72f);

    private Vector3 WeaponViewPositionTarget()
    {
        var target = _isAiming
            ? AimWeaponPosition()
            : HipWeaponPosition;
        if (_isReloading)
        {
            return ReloadWeaponPosition;
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
        // Pistols were filling the screen and sitting too high — lower and push back
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
            // Keep the sight picture close enough to the camera that a nearby
            // wall cannot swallow the entire pistol when ADS starts.  The old
            // -0.94 depth pushed short sidearms through cover and made them
            // appear to vanish while aiming.
            return new Vector3(
                0.12f,
                -pistolSightHeight * _weaponRoot.Scale.Y - 0.02f,
                -0.82f);
        }
        var sightHeight = IsInstanceValid(_opticRoot) && _opticRoot.Visible
            ? _opticRoot.Position.Y
            : 0.205f;
        return new Vector3(0.0f, -sightHeight * _weaponRoot.Scale.Y, -0.55f);
    }

    private Vector3 WeaponViewRotationTarget()
    {
        if (_isReloading)
        {
            return new Vector3(-0.13f, 0.0f, -0.32f);
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
}
