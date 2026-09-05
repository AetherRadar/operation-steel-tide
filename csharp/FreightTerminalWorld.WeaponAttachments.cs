using System;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private void DetachWeaponOpticFromSlot(int slotValue)
    {
        if (LocalPlayerCannotInteract
            || !Enum.IsDefined(typeof(PlayerWeaponSlot), slotValue))
        {
            return;
        }

        var slot = (PlayerWeaponSlot)slotValue;
        if (_player.TryDetachOpticToBackpack(slot))
        {
            RefreshLootView();
        }
    }
}
