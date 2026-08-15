using System;
using Godot;

namespace OperationSteelTide;

public partial class TacticalPlayer
{
    private Node3D _proceduralWeaponVisual = null!;
    private AuthoredWeaponVisual _authoredPrimaryWeapon = null!;

    internal bool UsesAuthoredPrimaryWeaponForDiagnostics
        => IsInstanceValid(_authoredPrimaryWeapon?.Root)
        && EquippedWeapon.Platform == WeaponPlatform.M4A1;

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
            _proceduralWeaponVisual.Visible = true;
            return;
        }
        RefreshAuthoredPrimaryWeapon();
    }

    private void RefreshAuthoredPrimaryWeapon()
    {
        if (!IsInstanceValid(_authoredPrimaryWeapon?.Root))
        {
            _proceduralWeaponVisual.Visible = true;
            return;
        }
        var useAuthoredM4 = EquippedWeapon.Platform == WeaponPlatform.M4A1;
        _authoredPrimaryWeapon.Root.Visible = useAuthoredM4;
        _proceduralWeaponVisual.Visible = !useAuthoredM4;
        if (useAuthoredM4)
        {
            _authoredPrimaryWeapon.Configure(EquippedWeapon);
            SyncAuthoredPrimaryWeapon();
        }
    }

    private void SyncAuthoredPrimaryWeapon()
    {
        if (!IsInstanceValid(_authoredPrimaryWeapon?.Root)
            || EquippedWeapon.Platform != WeaponPlatform.M4A1)
        {
            return;
        }
        _authoredPrimaryWeapon.SyncMechanisms(_magazine, _spareMagazine, _chargingHandle);
    }
}
