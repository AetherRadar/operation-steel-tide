using Godot;

namespace OperationSteelTide;

internal readonly record struct DemolitionSquadTacticalDiagnosticState(
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
}
