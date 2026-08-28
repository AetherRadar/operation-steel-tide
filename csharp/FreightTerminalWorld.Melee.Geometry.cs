using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private bool RemoteMeleeGeometryValid(
        long peerId,
        int swingSequence,
        Node3D shooter,
        Node3D target,
        Vector3 reportedOrigin,
        Vector3 reportedHitPoint,
        KnifeSkinDefinition definition)
    {
        var shooterPosition = MeleeAttackerPosition(shooter);
        var canonicalOrigin = MeleeAttackerPoint(shooter, reportedOrigin);
        if (!_demolitionMode && !IsExtractionNetworkMatch)
        {
            return RemoteLanMeleeGeometryValid(
                shooter,
                reportedOrigin,
                reportedHitPoint,
                definition,
                canonicalOrigin);
        }
        var canonicalHitPoint = MeleeTargetPoint(target);
        var toTarget = MeleeTargetPosition(target) - shooterPosition;
        var forward = MeleeAttackerForward(shooter);
        toTarget.Y = 0.0f;
        forward.Y = 0.0f;
        if (canonicalOrigin.DistanceTo(reportedOrigin) > 1.5f
            || canonicalOrigin.DistanceTo(canonicalHitPoint) > definition.Reach + 0.85f
            || canonicalHitPoint.DistanceTo(reportedHitPoint) > 1.2f
            || toTarget.LengthSquared() <= 0.01f
            || forward.LengthSquared() <= 0.01f
            || forward.Normalized().Dot(toTarget.Normalized()) < -0.2f)
        {
            return false;
        }
        return RemoteMeleeLineClear(
            peerId,
            swingSequence,
            shooter,
            target,
            canonicalOrigin,
            canonicalHitPoint);
    }

    private bool RemoteLanMeleeGeometryValid(
        Node3D shooter,
        Vector3 reportedOrigin,
        Vector3 reportedHitPoint,
        KnifeSkinDefinition definition,
        Vector3 canonicalOrigin)
    {
        var strike = reportedHitPoint - reportedOrigin;
        var forward = MeleeAttackerForward(shooter);
        strike.Y = 0.0f;
        forward.Y = 0.0f;
        if (canonicalOrigin.DistanceTo(reportedOrigin) > 1.5f
            || reportedOrigin.DistanceTo(reportedHitPoint) > definition.Reach + 0.85f
            || strike.LengthSquared() <= 0.01f
            || forward.LengthSquared() <= 0.01f
            || forward.Normalized().Dot(strike.Normalized()) < -0.2f)
        {
            return false;
        }
        return RemoteLanMeleeStaticLineClear(
            shooter,
            reportedOrigin,
            reportedHitPoint);
    }

    private bool RemoteLanMeleeStaticLineClear(
        Node3D shooter,
        Vector3 origin,
        Vector3 hitPoint)
    {
        var exclude = new Godot.Collections.Array<Rid>();
        using var excludeBacking = exclude.AsDisposable();
        if (shooter is CollisionObject3D collisionShooter)
        {
            exclude.Add(collisionShooter.GetRid());
        }
        for (var attempt = 0; attempt < 12; attempt++)
        {
            if (!PhysicsRaycast.TryHit(
                    GetWorld3D(),
                    origin,
                    hitPoint,
                    exclude,
                    uint.MaxValue,
                    out var hit))
            {
                return true;
            }
            if (hit.Collider?.GetType() == typeof(StaticBody3D))
            {
                return false;
            }
            if (hit.Collider is CollisionObject3D localDynamicBody)
            {
                exclude.Add(localDynamicBody.GetRid());
                continue;
            }
            return false;
        }
        return false;
    }

    private bool RemoteMeleeLineClear(
        long peerId,
        int swingSequence,
        Node3D shooter,
        Node3D target,
        Vector3 origin,
        Vector3 hitPoint)
    {
        var exclude = new Godot.Collections.Array<Rid>();
        using var excludeBacking = exclude.AsDisposable();
        if (shooter is CollisionObject3D collisionShooter)
        {
            exclude.Add(collisionShooter.GetRid());
        }
        if (target is CollisionObject3D collisionTarget)
        {
            exclude.Add(collisionTarget.GetRid());
        }
        var key = new RemoteMeleeSwingKey(peerId, swingSequence);
        if (_remoteMeleeSwings.TryGetValue(key, out var swing))
        {
            foreach (var acceptedTargetId in swing.TargetIds)
            {
                if (ResolveMeleeTarget(acceptedTargetId) is CollisionObject3D acceptedTarget)
                {
                    exclude.Add(acceptedTarget.GetRid());
                }
            }
        }
        return !PhysicsRaycast.TryHit(
                GetWorld3D(),
                origin,
                hitPoint,
                exclude,
                uint.MaxValue,
                out _);
    }

    private Node3D? ResolveMeleeTarget(int targetId)
    {
        if (!_demolitionMode)
        {
            return _enemies.FirstOrDefault(enemy => IsInstanceValid(enemy)
                && enemy.NetworkId == targetId);
        }
        var team = DemolitionActorTeam(targetId);
        var slot = DemolitionActorSlot(targetId);
        return team == _demolitionLocalNetworkTeam
            ? slot == _demolitionLocalNetworkSlot
                ? _player
                : _squadMates.FirstOrDefault(mate => IsInstanceValid(mate)
                    && mate.SquadSlot == slot)
            : _demolitionOpponents.FirstOrDefault(enemy => IsInstanceValid(enemy)
                && enemy.NetworkId == targetId);
    }

    private static Vector3 MeleeTargetPoint(Node3D target)
        => target switch
        {
            TacticalPlayer player => player.HitPoint(HitRegion.Torso),
            SquadMate mate => mate.NetworkAuthoritativePosition
                + Vector3.Up * (mate.IsDowned ? 0.42f : 1.08f),
            EnemyOperator enemy => enemy.GlobalPosition
                + Vector3.Up * (enemy.IsProne ? 0.44f : 0.9f),
            _ => target.GlobalPosition + Vector3.Up * 0.75f
        };

    private static Vector3 MeleeTargetPosition(Node3D target)
        => target is SquadMate mate
            ? mate.NetworkAuthoritativePosition
            : target.GlobalPosition;

    private static Vector3 MeleeTargetForward(Node3D target)
    {
        if (target is not SquadMate mate)
        {
            return -target.GlobalBasis.Z;
        }
        return -(new Basis(Vector3.Up, mate.NetworkAuthoritativeRotation.Y)).Z;
    }

    private static Vector3 MeleeAttackerPoint(Node3D shooter, Vector3 reportedOrigin)
    {
        if (shooter is TacticalPlayer player)
        {
            return player.HitPoint(HitRegion.Head);
        }
        var shooterPosition = MeleeAttackerPosition(shooter);
        var reportedHeight = reportedOrigin.Y - shooterPosition.Y;
        var allowedHeights = new[] { 0.62f, 1.16f, 1.57f };
        var height = allowedHeights.MinBy(candidate => Mathf.Abs(candidate - reportedHeight));
        return shooterPosition + Vector3.Up * height;
    }

    private static Vector3 MeleeAttackerPosition(Node3D shooter)
        => shooter is SquadMate mate
            ? mate.NetworkAuthoritativePosition
            : shooter.GlobalPosition;

    private static Vector3 MeleeAttackerForward(Node3D shooter)
    {
        if (shooter is not SquadMate mate)
        {
            return -shooter.GlobalBasis.Z;
        }
        return -(new Basis(Vector3.Up, mate.NetworkAuthoritativeRotation.Y)).Z;
    }
}
