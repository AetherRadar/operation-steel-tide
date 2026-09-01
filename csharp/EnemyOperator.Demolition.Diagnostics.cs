using Godot;

namespace OperationSteelTide;

public partial class EnemyOperator
{
    internal readonly record struct DemolitionPlantRuntimeDiagnosticState(
        bool Alerted,
        float Suspicion,
        Vector3 PatrolTarget,
        float FireTimer,
        bool HasLastKnownTarget,
        Vector3 LastKnownTargetPosition,
        float PursuitTimer,
        float LostSightTimer,
        Vector3 PursuitProgressOrigin,
        float PursuitProgressTimer,
        ulong RandomState);

    internal readonly record struct ScriptedObjectiveNavigationDiagnosticState(
        bool Avoiding,
        float Side,
        Vector3 ProgressOrigin,
        float ProgressTimer);

    internal DemolitionPlantRuntimeDiagnosticState CaptureDemolitionPlantRuntimeForDiagnostics()
        => new(
            Alerted,
            Suspicion,
            _patrolTarget,
            _fireTimer,
            _hasLastKnownTarget,
            _lastKnownTargetPosition,
            _pursuitTimer,
            _lostSightTimer,
            _pursuitProgressOrigin,
            _pursuitProgressTimer,
            _rng.State);

    internal void RestoreDemolitionPlantRuntimeForDiagnostics(
        DemolitionPlantRuntimeDiagnosticState state)
    {
        Alerted = state.Alerted;
        Suspicion = state.Suspicion;
        _patrolTarget = state.PatrolTarget;
        _fireTimer = state.FireTimer;
        _hasLastKnownTarget = state.HasLastKnownTarget;
        _lastKnownTargetPosition = state.LastKnownTargetPosition;
        _pursuitTimer = state.PursuitTimer;
        _lostSightTimer = state.LostSightTimer;
        _pursuitProgressOrigin = state.PursuitProgressOrigin;
        _pursuitProgressTimer = state.PursuitProgressTimer;
        _rng.State = state.RandomState;
    }

    internal ScriptedObjectiveNavigationDiagnosticState CaptureScriptedObjectiveNavigationForDiagnostics()
        => new(
            _scriptedObjectiveAvoiding,
            _scriptedObjectiveSide,
            _scriptedObjectiveProgressOrigin,
            _scriptedObjectiveProgressTimer);

    internal void RestoreScriptedObjectiveNavigationForDiagnostics(
        ScriptedObjectiveNavigationDiagnosticState state)
    {
        _scriptedObjectiveAvoiding = state.Avoiding;
        _scriptedObjectiveSide = state.Side;
        _scriptedObjectiveProgressOrigin = state.ProgressOrigin;
        _scriptedObjectiveProgressTimer = state.ProgressTimer;
    }

    internal void SetCombatPostureForDiagnostics(EnemyCombatPosture posture)
    {
        _seekingCover = false;
        _inCover = false;
        switch (posture)
        {
            case EnemyCombatPosture.Prone:
                SetProne(true);
                _proneTimer = 1.0f;
                break;
            case EnemyCombatPosture.Crouched:
                if (!TrySetPronePosture(false, CrouchedColliderHeight))
                {
                    return;
                }
                SetCombatCrouched(true);
                _combatStanceHoldRemaining = 1.0f;
                break;
            default:
                _ = TryStandForCombatMovement();
                break;
        }
        UpdateAuthoredStanceCollider();
    }

    internal bool TryStandForDiagnostics()
        => TryStandForCombatMovement();

    internal bool TryStartCombatJumpAttackForDiagnostics(
        EnemyCombatJumpContext context,
        Vector3 direction)
        => TryStartCombatJumpAttack(
            context with { CooldownRemaining = _combatJumpCooldown },
            direction);

    internal void ApplyFlashbangMovementForDiagnostics(float delta)
        => ApplyFlashbangMovement(Mathf.Max(0.0f, delta));

    internal void AdvanceCombatMovementTimersForDiagnostics(float delta)
        => UpdateCombatMovementTimers(Mathf.Max(0.0f, delta));
}
