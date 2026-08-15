using Godot;

namespace OperationSteelTide;

public partial class EnemyOperator
{
    /// <summary>
    /// Seeds one isolated combat probe and resets only tactical transients. Subsequent target
    /// acquisition, movement, ballistics, weapon cadence, and shot accounting use production AI.
    /// </summary>
    internal void ConfigureCombatProbeForDiagnostics(ulong seed, Vector3 investigatePosition)
    {
        _rng.Seed = seed;
        ResetTacticalStateForDiagnostics();
        _rng.Seed = seed;
        _strafeSign = (seed & 1UL) == 0UL ? -1.0f : 1.0f;
        _repathTimer = 4.0f;
        _patrolTimer = 4.0f;
        _stanceDecisionTimer = 4.0f;
        Velocity = Vector3.Zero;
        SetAlerted(investigatePosition);
        _fireTimer = 0.0f;
    }
}
