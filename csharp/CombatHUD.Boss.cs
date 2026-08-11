using Godot;

namespace OperationSteelTide;

public partial class CombatHUD
{
    public bool MinimapWorldBossVisible => IsInstanceValid(_minimap) && _minimap.WorldBossVisible;

    public void SetMinimapWorldBoss(Vector3 position, bool active)
    {
        if (IsInstanceValid(_minimap))
        {
            _minimap.SetWorldBoss(position, active);
        }
    }
}
