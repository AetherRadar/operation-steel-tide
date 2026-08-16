using Godot;

namespace OperationSteelTide;

internal readonly record struct PursuitContactDiagnosticState(
    ISquadCombatant? CombatTarget,
    Node3D? RawTarget,
    bool HasLastKnownTarget,
    Vector3 LastKnownTargetPosition,
    float PursuitTimer,
    float LostSightTimer,
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
            _pursuitProgressOrigin,
            _pursuitProgressTimer,
            Alerted,
            Suspicion,
            _searchingLoot,
            SquadContactsReceived);
    }

    internal bool MatchesPursuitContactStateForDiagnostics(PursuitContactDiagnosticState state)
        => CapturePursuitContactStateForDiagnostics() == state;
}
