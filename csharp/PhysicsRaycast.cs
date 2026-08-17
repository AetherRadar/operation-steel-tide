using Godot;

namespace OperationSteelTide;

internal readonly record struct PhysicsRaycastHit(
    GodotObject? Collider,
    Vector3 Position,
    Vector3 Normal,
    int Shape);

internal static class GodotPhysicsResultKeys
{
    public static readonly Variant Collider = "collider";
    public static readonly Variant Normal = "normal";
    public static readonly Variant Position = "position";
    public static readonly Variant Shape = "shape";
}

internal static class PhysicsRaycast
{
    [System.ThreadStatic]
    private static PhysicsRayQueryParameters3D? _threadQuery;

    [System.ThreadStatic]
    private static Godot.Collections.Array<Rid>? _threadEmptyExclude;

    [System.ThreadStatic]
    private static Godot.Collections.Array<Rid>? _threadSingleExclude;

    public static bool HasHit(
        World3D world,
        Vector3 from,
        Vector3 to,
        Rid excludeRid,
        uint collisionMask,
        bool collideWithAreas = false,
        bool collideWithBodies = true)
        => HasHit(
            world.DirectSpaceState,
            from,
            to,
            excludeRid,
            collisionMask,
            collideWithAreas,
            collideWithBodies);

    public static bool HasHit(
        PhysicsDirectSpaceState3D space,
        Vector3 from,
        Vector3 to,
        Rid excludeRid,
        uint collisionMask,
        bool collideWithAreas = false,
        bool collideWithBodies = true)
    {
        var exclude = _threadSingleExclude ??= new Godot.Collections.Array<Rid>();
        exclude.Clear();
        exclude.Add(excludeRid);
        return HasHit(
            space,
            from,
            to,
            exclude,
            collisionMask,
            collideWithAreas,
            collideWithBodies);
    }

    public static bool HasHit(
        World3D world,
        Vector3 from,
        Vector3 to,
        uint collisionMask,
        bool collideWithAreas = false,
        bool collideWithBodies = true)
        => HasHit(
            world.DirectSpaceState,
            from,
            to,
            collisionMask,
            collideWithAreas,
            collideWithBodies);

    public static bool HasHit(
        PhysicsDirectSpaceState3D space,
        Vector3 from,
        Vector3 to,
        uint collisionMask,
        bool collideWithAreas = false,
        bool collideWithBodies = true)
        => HasHit(
            space,
            from,
            to,
            ThreadEmptyExclude(),
            collisionMask,
            collideWithAreas,
            collideWithBodies);

    public static bool HasHit(
        World3D world,
        Vector3 from,
        Vector3 to,
        Godot.Collections.Array<Rid> exclude,
        uint collisionMask,
        bool collideWithAreas = false,
        bool collideWithBodies = true)
        => HasHit(
            world.DirectSpaceState,
            from,
            to,
            exclude,
            collisionMask,
            collideWithAreas,
            collideWithBodies);

    public static bool HasHit(
        PhysicsDirectSpaceState3D space,
        Vector3 from,
        Vector3 to,
        Godot.Collections.Array<Rid> exclude,
        uint collisionMask,
        bool collideWithAreas = false,
        bool collideWithBodies = true)
    {
        var query = _threadQuery ??= new PhysicsRayQueryParameters3D();
        query.From = from;
        query.To = to;
        query.CollisionMask = collisionMask;
        query.Exclude = exclude;
        query.CollideWithAreas = collideWithAreas;
        query.CollideWithBodies = collideWithBodies;
        try
        {
            using var result = space.IntersectRay(query);
            return result.Count > 0;
        }
        finally
        {
            query.Exclude = ThreadEmptyExclude();
        }
    }

    public static bool TryHit(
        World3D world,
        Vector3 from,
        Vector3 to,
        Rid excludeRid,
        uint collisionMask,
        out PhysicsRaycastHit hit,
        bool collideWithAreas = false,
        bool collideWithBodies = true)
        => TryHit(
            world.DirectSpaceState,
            from,
            to,
            excludeRid,
            collisionMask,
            out hit,
            collideWithAreas,
            collideWithBodies);

    public static bool TryHit(
        PhysicsDirectSpaceState3D space,
        Vector3 from,
        Vector3 to,
        Rid excludeRid,
        uint collisionMask,
        out PhysicsRaycastHit hit,
        bool collideWithAreas = false,
        bool collideWithBodies = true)
    {
        var exclude = _threadSingleExclude ??= new Godot.Collections.Array<Rid>();
        exclude.Clear();
        exclude.Add(excludeRid);
        return TryHit(
            space,
            from,
            to,
            exclude,
            collisionMask,
            out hit,
            collideWithAreas,
            collideWithBodies);
    }

    public static bool TryHit(
        World3D world,
        Vector3 from,
        Vector3 to,
        uint collisionMask,
        out PhysicsRaycastHit hit,
        bool collideWithAreas = false,
        bool collideWithBodies = true)
        => TryHit(
            world.DirectSpaceState,
            from,
            to,
            collisionMask,
            out hit,
            collideWithAreas,
            collideWithBodies);

    public static bool TryHit(
        PhysicsDirectSpaceState3D space,
        Vector3 from,
        Vector3 to,
        uint collisionMask,
        out PhysicsRaycastHit hit,
        bool collideWithAreas = false,
        bool collideWithBodies = true)
    {
        var exclude = ThreadEmptyExclude();
        return TryHit(
            space,
            from,
            to,
            exclude,
            collisionMask,
            out hit,
            collideWithAreas,
            collideWithBodies);
    }

    public static bool TryHit(
        World3D world,
        Vector3 from,
        Vector3 to,
        Godot.Collections.Array<Rid> exclude,
        uint collisionMask,
        out PhysicsRaycastHit hit,
        bool collideWithAreas = false,
        bool collideWithBodies = true)
        => TryHit(
            world.DirectSpaceState,
            from,
            to,
            exclude,
            collisionMask,
            out hit,
            collideWithAreas,
            collideWithBodies);

    public static bool TryHit(
        PhysicsDirectSpaceState3D space,
        Vector3 from,
        Vector3 to,
        Godot.Collections.Array<Rid> exclude,
        uint collisionMask,
        out PhysicsRaycastHit hit,
        bool collideWithAreas = false,
        bool collideWithBodies = true)
    {
        var query = _threadQuery ??= new PhysicsRayQueryParameters3D();
        query.From = from;
        query.To = to;
        query.CollisionMask = collisionMask;
        query.Exclude = exclude;
        query.CollideWithAreas = collideWithAreas;
        query.CollideWithBodies = collideWithBodies;
        try
        {
            using var result = space.IntersectRay(query);
            if (result.Count == 0)
            {
                hit = default;
                return false;
            }

            using var colliderValue = result[GodotPhysicsResultKeys.Collider];
            using var positionValue = result[GodotPhysicsResultKeys.Position];
            using var normalValue = result[GodotPhysicsResultKeys.Normal];
            var shape = -1;
            if (result.TryGetValue(GodotPhysicsResultKeys.Shape, out var shapeValue))
            {
                using (shapeValue)
                {
                    shape = shapeValue.AsInt32();
                }
            }
            hit = new PhysicsRaycastHit(
                colliderValue.AsGodotObject(),
                positionValue.AsVector3(),
                normalValue.AsVector3(),
                shape);
            return true;
        }
        finally
        {
            query.Exclude = ThreadEmptyExclude();
        }
    }

    private static Godot.Collections.Array<Rid> ThreadEmptyExclude()
        => _threadEmptyExclude ??= new Godot.Collections.Array<Rid>();
}

internal static class GodotCollectionOwnership
{
    public static Godot.Collections.Array AsDisposable<[MustBeVariant] T>(
        this Godot.Collections.Array<T> array)
        => (Godot.Collections.Array)array;
}

internal static class PhysicsShapeProbe
{
    public static bool HasCollision(
        World3D world,
        PhysicsShapeQueryParameters3D query,
        int maximumResults)
        => HasCollision(world.DirectSpaceState, query, maximumResults);

    public static bool HasCollision(
        PhysicsDirectSpaceState3D space,
        PhysicsShapeQueryParameters3D query,
        int maximumResults)
    {
        var results = space.IntersectShape(query, maximumResults);
        using var resultsBacking = results.AsDisposable();
        return results.Count > 0;
    }
}
