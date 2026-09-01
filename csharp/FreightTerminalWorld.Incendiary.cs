using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private const int MaximumActiveIncendiaryGrenades = 4;
    private const ulong IncendiaryDamageCooldownMsec = 350;
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
