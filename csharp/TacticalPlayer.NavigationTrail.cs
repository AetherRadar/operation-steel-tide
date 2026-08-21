using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public partial class TacticalPlayer : ICombatMovementTrailSource
{
    private readonly CombatMovementTrail _combatMovementTrail = new();

    CombatMovementTrail ICombatMovementTrailSource.CombatMovementTrail => _combatMovementTrail;

    private void RecordCombatMovementTrail()
        => _combatMovementTrail.Record(GlobalPosition);

    internal void SetCombatMovementTrailForDiagnostics(IReadOnlyList<Vector3> points)
        => _combatMovementTrail.SetForDiagnostics(points);
}
