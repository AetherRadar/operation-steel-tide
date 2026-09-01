using Godot;

namespace OperationSteelTide;

internal readonly record struct PursuitContactDiagnosticState(
    ISquadCombatant? CombatTarget,
    Node3D? RawTarget,
    bool HasLastKnownTarget,
    Vector3 LastKnownTargetPosition,
    float PursuitTimer,
    float LostSightTimer,
    ulong ConfirmedPursuitTargetId,
    Vector3 ConfirmedCombatContactPosition,
    float ConfirmedCombatContactTimer,
    ulong RecentDamageThreatTargetId,
    float RecentDamageThreatTimer,
    Vector3 PursuitProgressOrigin,
    float PursuitProgressTimer,
    bool Alerted,
    float Suspicion,
    bool SearchingLoot,
    int SquadContactsReceived);

public partial class EnemyOperator
{
    internal bool BypassesPlayerProtectionForDiagnostics { get; private set; }
    internal bool SuppressesContactSharingForDiagnostics { get; private set; }
    internal int TargetAcquisitionCountForDiagnostics { get; private set; }
    internal int TargetCandidateEvaluationCountForDiagnostics { get; private set; }
    internal int LineOfSightProbeCountForDiagnostics { get; private set; }
    internal int ContactShareRequestCountForDiagnostics { get; private set; }

    internal void ResetCrowdPerformanceCountersForDiagnostics()
    {
        TargetAcquisitionCountForDiagnostics = 0;
        TargetCandidateEvaluationCountForDiagnostics = 0;
        LineOfSightProbeCountForDiagnostics = 0;
        ContactShareRequestCountForDiagnostics = 0;
    }

    /// <summary>
    /// Seeds one isolated combat probe and resets only tactical transients. Subsequent target
    /// acquisition, movement, ballistics, weapon cadence, and shot accounting use production AI.
    /// </summary>
    internal void ConfigureCombatProbeForDiagnostics(
        ulong seed,
        Vector3 investigatePosition,
        bool bypassPlayerProtection,
        bool suppressContactSharing)
    {
        BypassesPlayerProtectionForDiagnostics = bypassPlayerProtection;
        SuppressesContactSharingForDiagnostics = suppressContactSharing;
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

    internal PursuitContactDiagnosticState CapturePursuitContactStateForDiagnostics()
    {
        return new PursuitContactDiagnosticState(
            _combatTarget,
            _rawTarget,
            _hasLastKnownTarget,
            _lastKnownTargetPosition,
            _pursuitTimer,
            _lostSightTimer,
            _confirmedPursuitTargetId,
            _confirmedCombatContactPosition,
            _confirmedCombatContactTimer,
            _recentDamageThreatTargetId,
            _recentDamageThreatTimer,
            _pursuitProgressOrigin,
            _pursuitProgressTimer,
            Alerted,
            Suspicion,
            _searchingLoot,
            SquadContactsReceived);
    }

    internal bool MatchesPursuitContactStateForDiagnostics(PursuitContactDiagnosticState state)
        => CapturePursuitContactStateForDiagnostics() == state;

    internal void AdvancePursuitTimersForDiagnostics(float delta)
        => UpdatePursuitTimers(Mathf.Max(0.0f, delta));
}
