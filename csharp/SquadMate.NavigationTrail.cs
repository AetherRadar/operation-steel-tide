using Godot;

namespace OperationSteelTide;

public partial class SquadMate : ICombatMovementTrailSource
{
    private readonly CombatMovementTrail _combatMovementTrail = new();

    CombatMovementTrail ICombatMovementTrailSource.CombatMovementTrail => _combatMovementTrail;

    private void RecordCombatMovementTrail()
        => _combatMovementTrail.Record(GlobalPosition);
}
