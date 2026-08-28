using Godot;

namespace OperationSteelTide;

public enum PlayerQuickSlot
{
    Primary = 0,
    Secondary = 1,
    Sidearm = 2,
    Melee = 3,
    FragmentationGrenade = 4,
    Utility = 5
}

public partial class TacticalPlayer
{
    private PlayerQuickSlot _activeQuickSlot = PlayerQuickSlot.Primary;
    private PlayerQuickSlot _returnQuickSlot = PlayerQuickSlot.Primary;

    public PlayerQuickSlot ActiveQuickSlot => _activeQuickSlot;
    public bool IsFirearmQuickSlotSelected
        => _activeQuickSlot is PlayerQuickSlot.Primary or PlayerQuickSlot.Secondary or PlayerQuickSlot.Sidearm;
    public bool IsThrowableQuickSlotSelected
        => _activeQuickSlot is PlayerQuickSlot.FragmentationGrenade or PlayerQuickSlot.Utility;

    public bool SelectQuickSlot(PlayerQuickSlot slot, bool notify = true)
    {
        if (_isPlating || MedicalActionBlocksWeapon || IsDead)
        {
            return false;
        }
        switch (slot)
        {
            case PlayerQuickSlot.Primary:
                return ActivateWeaponSlot(PlayerWeaponSlot.Primary, notify);
            case PlayerQuickSlot.Secondary:
                return ActivateWeaponSlot(PlayerWeaponSlot.Secondary, notify);
            case PlayerQuickSlot.Sidearm:
                return ActivateWeaponSlot(PlayerWeaponSlot.Sidearm, notify);
            case PlayerQuickSlot.Melee:
                return ActivateWeaponSlot(PlayerWeaponSlot.Melee, notify);
            case PlayerQuickSlot.FragmentationGrenade when Grenades > 0:
                SelectThrowableSlot(slot, notify);
                return true;
            case PlayerQuickSlot.Utility when SmokeGrenades > 0:
                SelectThrowableSlot(slot, notify);
                return true;
            default:
                if (notify)
                {
                    Hud?.ShowLocalizedMessage("quick_slot_empty", "SLOT EMPTY", new Color(1.0f, 0.48f, 0.25f));
                }
                return false;
        }
    }

    private void SelectThrowableSlot(PlayerQuickSlot slot, bool notify)
    {
        if (_activeQuickSlot is PlayerQuickSlot.Primary or PlayerQuickSlot.Secondary or PlayerQuickSlot.Sidearm or PlayerQuickSlot.Melee)
        {
            _returnQuickSlot = _activeQuickSlot;
            StoreActiveFirearmState();
        }
        CancelReloadForQuickSlot();
        _activeQuickSlot = slot;
        _knifeEquipped = false;
        _isAiming = false;
        CancelMeleeAction();
        UpdateHeldItemVisibility();
        if (!notify)
        {
            return;
        }
        var message = slot == PlayerQuickSlot.FragmentationGrenade
            ? ("frag_grenade_ready", "FRAG GRENADE READY")
            : ("utility_ready", "UTILITY ITEM READY");
        Hud?.ShowLocalizedMessage(message.Item1, message.Item2, new Color(0.95f, 0.72f, 0.28f));
    }

    private void CancelReloadForQuickSlot()
    {
        if (!_isReloading)
        {
            return;
        }
        _isReloading = false;
        _reloadTime = 0.0f;
        ResetReloadRig();
    }

    private void ReturnFromThrowableSlot()
    {
        var target = (PlayerWeaponSlot)Mathf.Clamp((int)_returnQuickSlot, 0, (int)PlayerWeaponSlot.Melee);
        if (!ActivateWeaponSlot(target, true))
        {
            ActivateWeaponSlot(PreferredFirearmOrMelee(), true);
        }
    }

    private void UpdateHeldItemVisibility()
    {
        var firearmVisible = IsFirearmQuickSlotSelected && !RoleActionBlocksWeapon && !MedicalActionBlocksWeapon;
        var knifeVisible = _activeQuickSlot == PlayerQuickSlot.Melee && !RoleActionBlocksWeapon && !MedicalActionBlocksWeapon;
        if (IsInstanceValid(_weaponRoot))
        {
            _weaponRoot.Visible = firearmVisible;
        }
        if (IsInstanceValid(_knifeRoot))
        {
            _knifeRoot.Visible = knifeVisible;
        }
        if (IsInstanceValid(_weaponLight))
        {
            _weaponLight.Visible = firearmVisible && _flashlightOn;
        }
        UpdateHeldThrowableVisual();
    }

    private bool ThrowSelectedQuickSlot()
    {
        return _activeQuickSlot switch
        {
            PlayerQuickSlot.FragmentationGrenade => ThrowGrenade(),
            PlayerQuickSlot.Utility => ThrowSmokeGrenade(),
            _ => false
        };
    }

    internal bool UseSelectedQuickSlotForDiagnostics()
        => ThrowSelectedQuickSlot();

    private void OnThrowableConsumed()
    {
        ActivateWeaponSlot(PreferredFirearmOrMelee(), true);
    }
}
