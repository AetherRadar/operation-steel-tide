using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private const int MaximumActiveIncendiaryGrenades = 4;
    private const ulong IncendiaryDamageCooldownMsec = 350;
    private const float IncendiaryAiAvoidanceMargin = 0.8f;
    private const float IncendiaryAiMaximumVerticalSeparation = 1.75f;
    private const float IncendiaryAiEscapeNearLookAhead = 1.25f;
    private const float IncendiaryAiEscapeFarLookAhead = 2.75f;
    private const int IncendiaryAiEscapeCandidateCount = 16;
    private readonly List<IncendiaryGrenade> _activeIncendiaryGrenades = new();
    private readonly Dictionary<ulong, ulong> _incendiaryLastDamageTicksMsec = new();

    internal int ActiveIncendiaryCountForDiagnostics => _activeIncendiaryGrenades.Count;

    public void ThrowIncendiaryGrenade(Vector3 origin, Vector3 direction, Node source)
        => ThrowIncendiaryGrenade(origin, direction, source, 14.0f, 5.0f);

    public void ThrowIncendiaryGrenade(
        Vector3 origin,
        Vector3 direction,
        Node source,
        float speed,
        float loft)
    {
        var grenade = new IncendiaryGrenade
        {
            Position = origin,
            OwnerBody = source
        };
        AddChild(grenade);
        grenade.Arm(direction, speed, loft);
        NotifyHostDemolitionUtilitySpawned(
            DemolitionNetworkUtilityKind.Incendiary,
            origin,
            direction,
            source,
            speed,
            loft);
    }

    internal void RegisterActiveIncendiaryGrenade(IncendiaryGrenade grenade)
    {
        if (_activeIncendiaryGrenades.Contains(grenade))
        {
            return;
        }
        for (var index = _activeIncendiaryGrenades.Count - 1; index >= 0; index--)
        {
            if (!IsInstanceValid(_activeIncendiaryGrenades[index]))
            {
                _activeIncendiaryGrenades.RemoveAt(index);
            }
        }
        while (_activeIncendiaryGrenades.Count >= MaximumActiveIncendiaryGrenades)
        {
            var oldest = _activeIncendiaryGrenades[0];
            _activeIncendiaryGrenades.RemoveAt(0);
            if (IsInstanceValid(oldest))
            {
                oldest.QueueFree();
            }
        }
        _activeIncendiaryGrenades.Add(grenade);
    }

    internal void UnregisterActiveIncendiaryGrenade(IncendiaryGrenade grenade)
        => _activeIncendiaryGrenades.Remove(grenade);

    /// <summary>
    /// Resolves an allocation-free horizontal escape heading for AI standing in, or
    /// immediately beside, one or more active fire fields. Candidate scoring looks
    /// ahead at two distances so escaping one incendiary cannot blindly steer an
    /// operator into another overlapping field.
    /// </summary>
    internal bool TryGetIncendiaryEscapeDirection(
        Vector3 point,
        Vector3 fallbackDirection,
        out Vector3 direction)
    {
        var threatened = false;
        var weightedEscape = Vector3.Zero;
        for (var index = _activeIncendiaryGrenades.Count - 1; index >= 0; index--)
        {
            var incendiary = _activeIncendiaryGrenades[index];
            if (!IsInstanceValid(incendiary))
            {
                _activeIncendiaryGrenades.RemoveAt(index);
                continue;
            }
            if (!incendiary.IsBurning || incendiary.RemainingDuration <= 0.0f)
            {
                continue;
            }

            var offset = point - incendiary.GlobalPosition;
            if (Mathf.Abs(offset.Y) > IncendiaryAiMaximumVerticalSeparation)
            {
                continue;
            }
            offset.Y = 0.0f;
            var distance = offset.Length();
            var avoidanceRadius = IncendiaryGrenade.FireRadius + IncendiaryAiAvoidanceMargin;
            if (distance > avoidanceRadius)
            {
                continue;
            }
            threatened = true;
            if (offset.LengthSquared() > 0.001f)
            {
                var depth = 1.0f + Mathf.Clamp(
                    1.0f - distance / avoidanceRadius,
                    0.0f,
                    1.0f) * 2.0f;
                weightedEscape += offset.Normalized() * depth;
            }
        }

        if (!threatened)
        {
            direction = Vector3.Zero;
            return false;
        }

        fallbackDirection.Y = 0.0f;
        var fallback = fallbackDirection.LengthSquared() > 0.001f
            ? fallbackDirection.Normalized()
            : Vector3.Forward;
        var bestDirection = weightedEscape.LengthSquared() > 0.001f
            ? weightedEscape.Normalized()
            : fallback;
        var bestScore = ScoreIncendiaryEscapeDirection(point, bestDirection, fallback);
        for (var candidateIndex = 0;
             candidateIndex < IncendiaryAiEscapeCandidateCount;
             candidateIndex++)
        {
            var angle = Mathf.Tau * candidateIndex / IncendiaryAiEscapeCandidateCount;
            var candidate = new Vector3(Mathf.Cos(angle), 0.0f, Mathf.Sin(angle));
            var score = ScoreIncendiaryEscapeDirection(point, candidate, fallback);
            if (score > bestScore)
            {
                bestScore = score;
                bestDirection = candidate;
            }
        }

        direction = bestDirection;
        return true;
    }

    private float ScoreIncendiaryEscapeDirection(
        Vector3 point,
        Vector3 candidate,
        Vector3 fallback)
    {
        var nearPoint = point + candidate * IncendiaryAiEscapeNearLookAhead;
        var farPoint = point + candidate * IncendiaryAiEscapeFarLookAhead;
        var minimumClearance = float.PositiveInfinity;
        var weightedProgress = 0.0f;
        var activeFields = 0;
        foreach (var incendiary in _activeIncendiaryGrenades)
        {
            if (!IsInstanceValid(incendiary)
                || !incendiary.IsBurning
                || incendiary.RemainingDuration <= 0.0f)
            {
                continue;
            }

            var center = incendiary.GlobalPosition;
            if (Mathf.Abs(point.Y - center.Y) > IncendiaryAiMaximumVerticalSeparation)
            {
                continue;
            }
            var currentDistance = IncendiaryAiHorizontalDistance(point, center);
            if (currentDistance
                > IncendiaryGrenade.FireRadius
                    + IncendiaryAiAvoidanceMargin
                    + IncendiaryAiEscapeFarLookAhead)
            {
                continue;
            }

            activeFields++;
            var nearDistance = IncendiaryAiHorizontalDistance(nearPoint, center);
            var farDistance = IncendiaryAiHorizontalDistance(farPoint, center);
            var futureDistance = Mathf.Min(nearDistance, farDistance);
            var clearance = futureDistance
                - (IncendiaryGrenade.FireRadius + IncendiaryAiAvoidanceMargin);
            minimumClearance = Mathf.Min(minimumClearance, clearance);
            var importance = 1.0f + Mathf.Clamp(
                1.0f - currentDistance / (IncendiaryGrenade.FireRadius * 2.0f),
                0.0f,
                1.0f) * 2.0f;
            weightedProgress += (farDistance - currentDistance) * importance;
            if (nearDistance < IncendiaryGrenade.FireRadius)
            {
                weightedProgress -= (IncendiaryGrenade.FireRadius - nearDistance) * 18.0f;
            }
        }

        return activeFields == 0
            ? 0.0f
            : minimumClearance * 4.0f
                + weightedProgress
                + candidate.Dot(fallback) * 0.2f;
    }

    private static float IncendiaryAiHorizontalDistance(Vector3 first, Vector3 second)
        => new Vector2(first.X - second.X, first.Z - second.Z).Length();

    internal void ApplyIncendiaryDamageTick(
        Vector3 position,
        float radius,
        float damage,
        Node source,
        Node emitter)
    {
        // Multiplayer projectile replication is intentionally visual-only for now.
        // Health remains host authoritative, so clients never apply fire damage locally.
        if (!_demolitionRoundActive
            || IsDemolitionNetworkClient
            || damage <= 0.0f
            || radius <= 0.0f)
        {
            return;
        }

        foreach (var enemy in _enemies.ToArray())
        {
            if (!IsInstanceValid(enemy) || enemy.IsDead)
            {
                continue;
            }
            var distance = enemy.GlobalPosition.DistanceTo(position);
            if (distance >= radius)
            {
                continue;
            }
            var exposure = ExplosionExposureResolver.ResolveStandingTarget(
                GetWorld3D(),
                position + Vector3.Up * 0.12f,
                enemy,
                source,
                emitter);
            if (exposure.IsExposed && TryAcquireIncendiaryDamageTick(enemy))
            {
                enemy.TakeDamage(
                    damage * FireFalloff(distance, radius) * exposure.Fraction,
                    enemy.GlobalPosition + Vector3.Up * 1.0f,
                    source);
            }
        }

        if (IsInstanceValid(_player) && !_player.IsDead)
        {
            var distance = _player.GlobalPosition.DistanceTo(position);
            if (distance < radius)
            {
                var exposure = ExplosionExposureResolver.ResolveCombatant(
                    GetWorld3D(),
                    position + Vector3.Up * 0.12f,
                    _player,
                    source,
                    emitter);
                if (exposure.IsExposed && TryAcquireIncendiaryDamageTick(_player))
                {
                    _player.TakeDamage(
                        damage * FireFalloff(distance, radius) * exposure.Fraction,
                        _player.HitPoint(HitRegion.Torso),
                        source);
                }
            }
        }

        foreach (var mate in _squadMates.ToArray())
        {
            if (!IsInstanceValid(mate) || mate.IsDowned || mate.IsBodyBag)
            {
                continue;
            }
            var distance = mate.GlobalPosition.DistanceTo(position);
            if (distance >= radius)
            {
                continue;
            }
            var exposure = ExplosionExposureResolver.ResolveCombatant(
                GetWorld3D(),
                position + Vector3.Up * 0.12f,
                mate,
                source,
                emitter);
            if (exposure.IsExposed && TryAcquireIncendiaryDamageTick(mate))
            {
                mate.TakeExplosionCombatDamage(
                    damage * FireFalloff(distance, radius) * exposure.Fraction,
                    mate.HitPoint(HitRegion.Torso),
                    source);
            }
        }
    }

    private void ClearDemolitionUtilityProjectiles()
    {
        QueueFreeDemolitionUtilityGroup(FragGrenade.ActiveGroupName);
        QueueFreeDemolitionUtilityGroup(SmokeGrenade.ActiveGroupName);
        QueueFreeDemolitionUtilityGroup(IncendiaryGrenade.ActiveGroupName);
        QueueFreeDemolitionUtilityGroup(FlashbangGrenade.ActiveGroupName);
        _activeSmokeGrenades.Clear();
        _activeIncendiaryGrenades.Clear();
        _activeFlashbangGrenades.Clear();
        _replicatedFlashbangsBySpawnId.Clear();
        _incendiaryLastDamageTicksMsec.Clear();
    }

    private void QueueFreeDemolitionUtilityGroup(string groupName)
    {
        var projectiles = GetTree().GetNodesInGroup(groupName);
        using var projectilesBacking = projectiles.AsDisposable();
        foreach (var projectile in projectiles)
        {
            if (IsInstanceValid(projectile))
            {
                projectile.QueueFree();
            }
        }
    }

    private bool TryAcquireIncendiaryDamageTick(Node combatant)
    {
        var now = Time.GetTicksMsec();
        var id = combatant.GetInstanceId();
        if (_incendiaryLastDamageTicksMsec.TryGetValue(id, out var previous)
            && now - previous < IncendiaryDamageCooldownMsec)
        {
            return false;
        }
        _incendiaryLastDamageTicksMsec[id] = now;
        return true;
    }

    internal bool TryAcquireIncendiaryDamageTickForDiagnostics(Node combatant)
        => TryAcquireIncendiaryDamageTick(combatant);

    private static float FireFalloff(float distance, float radius)
        => Mathf.Lerp(
            1.0f,
            0.45f,
            Mathf.Clamp(distance / Mathf.Max(0.01f, radius), 0.0f, 1.0f));
}
