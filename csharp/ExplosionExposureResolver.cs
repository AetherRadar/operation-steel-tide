using Godot;

namespace OperationSteelTide;

internal readonly record struct ExplosionExposure(
    float Fraction,
    int VisibleSamples,
    int TotalSamples)
{
    public bool IsExposed => VisibleSamples > 0;
}

/// <summary>
/// Samples blast visibility against authored world collision. A doorway or window can
/// expose part of an operator, while a continuous wall or roof blocks every sample.
/// </summary>
internal static class ExplosionExposureResolver
{
    private const uint WorldCollisionMask = 1;
    private const float RayOriginNudge = 0.035f;
    private const float LateralSampleOffset = 0.24f;

    public static ExplosionExposure Resolve(
        World3D world,
        Vector3 blastOrigin,
        Node3D target,
        Node? source,
        Vector3 lowerPoint,
        Vector3 torsoPoint,
        Vector3 upperPoint,
        Node? blastEmitter = null)
    {
        var towardTarget = torsoPoint - blastOrigin;
        var horizontal = new Vector3(towardTarget.X, 0.0f, towardTarget.Z);
        var lateral = horizontal.LengthSquared() > 0.0001f
            ? new Vector3(-horizontal.Z, 0.0f, horizontal.X).Normalized()
            : Vector3.Right;
        var lateralOffset = lateral * LateralSampleOffset;

        var exclusions = BuildExclusions(target, source, blastEmitter);
        try
        {
            var visible = 0;
            visible += IsSampleVisible(world, blastOrigin, lowerPoint, exclusions) ? 1 : 0;
            visible += IsSampleVisible(world, blastOrigin, torsoPoint, exclusions) ? 1 : 0;
            visible += IsSampleVisible(world, blastOrigin, upperPoint, exclusions) ? 1 : 0;
            visible += IsSampleVisible(world, blastOrigin, torsoPoint - lateralOffset, exclusions) ? 1 : 0;
            visible += IsSampleVisible(world, blastOrigin, torsoPoint + lateralOffset, exclusions) ? 1 : 0;
            return new ExplosionExposure(visible / 5.0f, visible, 5);
        }
        finally
        {
            exclusions.AsDisposable().Dispose();
        }
    }

    public static ExplosionExposure ResolveCombatant(
        World3D world,
        Vector3 blastOrigin,
        ISquadCombatant target,
        Node? source,
        Node? blastEmitter = null)
        => Resolve(
            world,
            blastOrigin,
            target.CombatNode,
            source,
            target.HitPoint(HitRegion.Limbs),
            target.HitPoint(HitRegion.Torso),
            target.HitPoint(HitRegion.Head),
            blastEmitter);

    public static ExplosionExposure ResolveStandingTarget(
        World3D world,
        Vector3 blastOrigin,
        Node3D target,
        Node? source,
        Node? blastEmitter = null)
    {
        var prone = target is EnemyOperator { IsProne: true };
        return Resolve(
            world,
            blastOrigin,
            target,
            source,
            target.GlobalPosition + Vector3.Up * (prone ? 0.18f : 0.42f),
            target.GlobalPosition + Vector3.Up * (prone ? 0.4f : 1.02f),
            target.GlobalPosition + Vector3.Up * (prone ? 0.68f : 1.58f),
            blastEmitter);
    }

    public static ExplosionExposure ResolveLowTarget(
        World3D world,
        Vector3 blastOrigin,
        Node3D target,
        Node? source,
        Node? blastEmitter = null)
        => Resolve(
            world,
            blastOrigin,
            target,
            source,
            target.GlobalPosition + Vector3.Up * 0.15f,
            target.GlobalPosition + Vector3.Up * 0.55f,
            target.GlobalPosition + Vector3.Up * 0.98f,
            blastEmitter);

    private static bool IsSampleVisible(
        World3D world,
        Vector3 blastOrigin,
        Vector3 samplePoint,
        Godot.Collections.Array<Rid> exclusions)
    {
        var distance = blastOrigin.DistanceTo(samplePoint);
        if (distance <= RayOriginNudge)
        {
            return true;
        }

        var rayStart = blastOrigin.MoveToward(samplePoint, Mathf.Min(RayOriginNudge, distance * 0.25f));
        return !PhysicsRaycast.HasHit(
            world,
            rayStart,
            samplePoint,
            exclusions,
            WorldCollisionMask,
            hitFromInside: true);
    }

    private static Godot.Collections.Array<Rid> BuildExclusions(
        Node3D target,
        Node? source,
        Node? blastEmitter)
    {
        var exclusions = new Godot.Collections.Array<Rid>();
        AddCollisionRid(exclusions, target);
        if (!ReferenceEquals(source, target))
        {
            AddCollisionRid(exclusions, source);
        }
        if (!ReferenceEquals(blastEmitter, target) && !ReferenceEquals(blastEmitter, source))
        {
            AddCollisionRid(exclusions, blastEmitter);
        }
        return exclusions;
    }

    private static void AddCollisionRid(Godot.Collections.Array<Rid> exclusions, Node? node)
    {
        if (node is not CollisionObject3D collisionObject || !GodotObject.IsInstanceValid(collisionObject))
        {
            return;
        }
        var rid = collisionObject.GetRid();
        if (rid.IsValid && !exclusions.Contains(rid))
        {
            exclusions.Add(rid);
        }
    }
}
