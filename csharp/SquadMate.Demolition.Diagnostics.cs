using Godot;

namespace OperationSteelTide;

internal readonly record struct DemolitionSquadTacticalDiagnosticState(
    EnemyOperator? CombatTarget,
    EnemyOperator? CombatThreat,
    float CombatThreatAge,
    float CombatMemoryRemaining,
    float CombatSightTimer,
    float CombatTargetScanTimer,
    Vector3 CombatLastKnownPosition,
    bool CombatHasSight,
    float ManeuverTimer,
    float CoverCommitment,
    float AvoidanceTimer,
    float RecoveryTimer,
    float StrafeSign,
    float FlankSide,
    Vector3 RecoveryDirection,
    Vector3 DesiredDirection,
    Vector3 ProgressOrigin,
    float ProgressTimer,
    bool MoveRequested,
    bool HasCoverPosition,
    int BurstShotsRemaining);

public partial class SquadMate
{
    internal DemolitionSquadTacticalDiagnosticState CaptureDemolitionTacticalStateForDiagnostics()
    {
        return new DemolitionSquadTacticalDiagnosticState(
            _combatTarget,
            _combatThreat,
            _combatThreatAge,
            _combatMemoryRemaining,
            _combatSightTimer,
            _combatTargetScanTimer,
            _combatLastKnownPosition,
            _combatHasSight,
            _combatManeuverTimer,
            _combatCoverCommitment,
            _combatAvoidanceTimer,
            _combatRecoveryTimer,
            _combatStrafeSign,
            _combatFlankSide,
            _combatRecoveryDirection,
            _combatDesiredDirection,
            _combatProgressOrigin,
            _combatProgressTimer,
            _combatMoveRequested,
            _combatHasCoverPosition,
            _burstShotsRemaining);
    }

    internal void RestoreDemolitionTacticalStateForDiagnostics(
        DemolitionSquadTacticalDiagnosticState state)
    {
        _combatTarget = state.CombatTarget;
        _combatThreat = state.CombatThreat;
        _combatThreatAge = state.CombatThreatAge;
        _combatMemoryRemaining = state.CombatMemoryRemaining;
        _combatSightTimer = state.CombatSightTimer;
        _combatTargetScanTimer = state.CombatTargetScanTimer;
        _combatLastKnownPosition = state.CombatLastKnownPosition;
        _combatHasSight = state.CombatHasSight;
        _combatManeuverTimer = state.ManeuverTimer;
        _combatCoverCommitment = state.CoverCommitment;
        _combatAvoidanceTimer = state.AvoidanceTimer;
        _combatRecoveryTimer = state.RecoveryTimer;
        _combatStrafeSign = state.StrafeSign;
        _combatFlankSide = state.FlankSide;
        _combatRecoveryDirection = state.RecoveryDirection;
        _combatDesiredDirection = state.DesiredDirection;
        _combatProgressOrigin = state.ProgressOrigin;
        _combatProgressTimer = state.ProgressTimer;
        _combatMoveRequested = state.MoveRequested;
        _combatHasCoverPosition = state.HasCoverPosition;
        _burstShotsRemaining = state.BurstShotsRemaining;
    }

    internal void SetDemolitionThreatForDiagnostics(
        EnemyOperator? threat,
        bool hasSight,
        float threatAge)
    {
        _combatTarget = threat;
        _combatThreat = threat;
        _combatThreatAge = threatAge;
        _combatMemoryRemaining = threat is null ? 0.0f : VisibleContactMemory;
        _combatSightTimer = 0.1f;
        _combatTargetScanTimer = 0.42f;
        _combatHasSight = threat is not null && hasSight;
        if (threat is not null)
        {
            _combatLastKnownPosition = threat.GlobalPosition;
        }
    }
}
