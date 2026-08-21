using Godot;

namespace OperationSteelTide;

public partial class EnemyOperator : ICombatMovementTrailSource
{
    private readonly CombatMovementTrail _combatMovementTrail = new();

    CombatMovementTrail ICombatMovementTrailSource.CombatMovementTrail => _combatMovementTrail;

    private void RecordCombatMovementTrail()
        => _combatMovementTrail.Record(GlobalPosition);
}
