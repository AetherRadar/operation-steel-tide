using Godot;

namespace OperationSteelTide;

/// <summary>
/// Keeps distant, non-combat operators from running the complete tactical motor at
/// the full physics frequency.  Operators close to the player, alerted, or
/// pursuing a remembered contact always remain on the full-rate path.
/// </summary>
public partial class EnemyOperator
{
    private const float ReducedSimulationStepSeconds = 0.1f;
    private const float ReducedSimulationMaximumDeltaSeconds = 0.12f;
    private const float ReducedSimulationAccumulatorLimitSeconds = 0.4f;
    private const float ReducedSimulationLodCheckIntervalSeconds = 0.35f;

    private float _simulationAccumulator;
    private float _lodCheckTimer;
    private bool _reducedSimulation;

    internal bool ReducedSimulationActiveForDiagnostics => _reducedSimulation;
    internal int ReducedSimulationSkippedFramesForDiagnostics { get; private set; }
    internal int ReducedSimulationStepCountForDiagnostics { get; private set; }

    /// <summary>
    /// Returns the amount of simulation time to consume this frame.  The caller can
    /// safely return when this is false; no tactical timers or movement are advanced
    /// until the next accumulated step.
    /// </summary>
    private bool TryGetSimulationDelta(float frameDelta, out float simulationDelta)
    {
        simulationDelta = 0.0f;
        frameDelta = Mathf.Clamp(frameDelta, 0.0f, 0.25f);

        var reducedEligible = CanUseReducedSimulation();
        if (!reducedEligible)
        {
            // State changes (damage, shared contact, or a nearby player) wake an
            // operator on this very frame instead of waiting for the next LOD poll.
            _simulationAccumulator = 0.0f;
            _lodCheckTimer = ReducedSimulationLodCheckIntervalSeconds;
            _reducedSimulation = false;
            simulationDelta = frameDelta;
            return frameDelta > 0.0f;
        }

        // A short accumulator preserves timer semantics while reducing expensive
        // target queries, raycasts, and route preparation to roughly 10 Hz.
        _simulationAccumulator = Mathf.Min(
            ReducedSimulationAccumulatorLimitSeconds,
            _simulationAccumulator + frameDelta);
        _lodCheckTimer -= frameDelta;

        if (_lodCheckTimer <= 0.0f)
        {
            _lodCheckTimer = ReducedSimulationLodCheckIntervalSeconds;
            _reducedSimulation = Main?.ShouldUseReducedEnemySimulation(GlobalPosition) == true
                && IsLowPriorityForReducedSimulation();
        }

        if (!_reducedSimulation)
        {
            simulationDelta = Mathf.Min(
                ReducedSimulationMaximumDeltaSeconds,
                _simulationAccumulator);
            _simulationAccumulator = 0.0f;
            return simulationDelta > 0.0f;
        }

        if (_simulationAccumulator < ReducedSimulationStepSeconds)
        {
            ReducedSimulationSkippedFramesForDiagnostics++;
            return false;
        }

        simulationDelta = Mathf.Min(
            ReducedSimulationMaximumDeltaSeconds,
            _simulationAccumulator);
        _simulationAccumulator = 0.0f;
        ReducedSimulationStepCountForDiagnostics++;
        return simulationDelta > 0.0f;
    }

    private bool CanUseReducedSimulation()
        => !IsDead
            && !IsNetworkProxy
            && !IsHumanProxy
            && !IsWorldBoss
            && IsLowPriorityForReducedSimulation()
            && Main?.ShouldUseReducedEnemySimulation(GlobalPosition) == true;

    private bool IsLowPriorityForReducedSimulation()
        => IsOnFloor()
            && !Alerted
            && !IsPursuing
            && EngageTargetNode is null
            && _hitStun <= 0.0f;

    internal void ResetReducedSimulationCountersForDiagnostics()
    {
        ReducedSimulationSkippedFramesForDiagnostics = 0;
        ReducedSimulationStepCountForDiagnostics = 0;
    }
}
