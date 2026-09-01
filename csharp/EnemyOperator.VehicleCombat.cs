using Godot;

namespace OperationSteelTide;

public partial class EnemyOperator
{
    private const float OccupiedVehicleDetectionRange = 72.0f;
    private const float OccupiedVehicleAwarenessRange = 58.0f;
    private const float OccupiedVehicleThreatScoreBonus = 900.0f;

    private static bool TryResolveDrivenVehicle(
        TacticalPlayer player,
        out DriveableVehicle? vehicle)
    {
        vehicle = player.CurrentVehicle;
        return vehicle is not null
            && GodotObject.IsInstanceValid(vehicle)
            && !vehicle.IsDestroyed
            && vehicle.IsDrivenBy(player);
    }

    private bool TryResolveOccupiedVehicleTarget(out DriveableVehicle? vehicle)
    {
        if (_combatTarget is TacticalPlayer player
            && TryResolveDrivenVehicle(player, out vehicle))
        {
            return true;
        }

        vehicle = null;
        return false;
    }

    private float CurrentTargetDetectionRange()
        => TryResolveOccupiedVehicleTarget(out _)
            ? Mathf.Max(DetectionRange, OccupiedVehicleDetectionRange)
            : DetectionRange;

    private bool HasOccupiedVehicleAwareness(float distance)
        => distance <= OccupiedVehicleAwarenessRange
            && TryResolveOccupiedVehicleTarget(out _);

    private static float CandidateAcquireRangeSquared(Node3D candidate, float defaultRangeSquared)
        => candidate is TacticalPlayer player && TryResolveDrivenVehicle(player, out _)
            ? Mathf.Max(
                defaultRangeSquared,
                OccupiedVehicleDetectionRange * OccupiedVehicleDetectionRange)
            : defaultRangeSquared;

    private static float OccupiedVehicleTargetBias(Node3D candidate)
        => candidate is TacticalPlayer player && TryResolveDrivenVehicle(player, out _)
            ? -OccupiedVehicleThreatScoreBonus
            : 0.0f;

    private Node3D? CurrentBallisticTargetNode()
        => TryResolveOccupiedVehicleTarget(out var vehicle)
            ? vehicle
            : _combatTarget?.CombatNode ?? _rawTarget;

    private void FireAtOccupiedVehicle(DriveableVehicle vehicle, float distance)
    {
        if (!HasFireablePrimary
            || !CanFireDuringFlashbang
            || vehicle.IsDestroyed
            || !vehicle.HasDriver)
        {
            return;
        }

        BeginMuzzleFlash();
        var stats = CarriedWeapon.Stats();
        _fireTimer = _rng.RandfRange(stats.FireInterval * 1.8f, stats.FireInterval * 3.6f)
            * (IsWorldBoss ? WorldBossFireCadenceMultiplier : 1.0f)
            * FlashbangFireCadenceMultiplier;
        var rangeFactor = Mathf.Clamp(stats.EffectiveRange / 150.0f, 0.7f, 1.25f);
        var baseAccuracy = IsWorldBoss ? 0.96f : IsRivalSquad ? 0.94f : 0.88f;
        var accuracy = Mathf.Clamp(
            (IsProne ? baseAccuracy + 0.02f : baseAccuracy) - distance * 0.004f / rangeFactor,
            0.58f,
            0.97f) * FlashbangAccuracyMultiplier;
        var aimPoint = vehicle.HostileAimPoint(GlobalPosition);
        var shotOrigin = ResolveBallisticShotOrigin();
        if (BreakableGlassField.TryShatterAlongRay(
            GetWorld3D(),
            shotOrigin,
            aimPoint,
            stats.Damage * 0.4f,
            shotOrigin.DirectionTo(aimPoint),
            out var glassHitPosition))
        {
            Main?.SpawnTracer(shotOrigin, glassHitPosition, CurrentTracerColor);
            return;
        }

        var clear = Ballistics.HasClearShot(GetWorld3D(), shotOrigin, aimPoint, vehicle, GetRid());
        if (clear && _rng.Randf() < accuracy)
        {
            vehicle.TakeDamage(
                stats.Damage * _rng.RandfRange(0.32f, 0.48f),
                aimPoint,
                this);
        }
        else if (!clear)
        {
            if (PhysicsRaycast.TryHit(
                    GetWorld3D(),
                    shotOrigin,
                    aimPoint,
                    GetRid(),
                    uint.MaxValue,
                    out var hit))
            {
                aimPoint = hit.Position;
            }
        }
        else
        {
            aimPoint += Scatter() * 0.28f;
        }
        Main?.SpawnTracer(shotOrigin, aimPoint, CurrentTracerColor);
    }

    internal bool TargetsOccupiedVehicleForDiagnostics(DriveableVehicle vehicle)
        => TryResolveOccupiedVehicleTarget(out var current)
            && ReferenceEquals(current, vehicle);

    internal bool HasCurrentTargetLineOfSightForDiagnostics()
        => HasLineOfSight();
}
