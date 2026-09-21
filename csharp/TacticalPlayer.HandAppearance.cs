using Godot;

namespace OperationSteelTide;

public partial class TacticalPlayer
{
    private void RefreshFirstPersonHandAppearance()
    {
        foreach (var root in new[]
        {
            _authoredRifleArms?.Root, _authoredPistolServiceArms?.Root,
            _authoredPistolLargeArms?.Root, _authoredFirstPersonSmg?.Root,
            _meleeArms?.Root, _ladderHandsRoot
        })
        {
            if (IsInstanceValid(root)) FirstPersonHandAppearance.Apply(root!, Role);
        }
        _fieldUsePresentation?.SetRole(Role);
    }
}
