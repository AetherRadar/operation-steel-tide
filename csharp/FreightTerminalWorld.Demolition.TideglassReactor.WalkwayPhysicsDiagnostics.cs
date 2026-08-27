using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private static (bool Ready, float SafeFraction, string Collider) TideglassWalkwayUpwardBlockReady(
        World3D world,
        DemolitionArenaLayout layout)
    {
        const float capsuleHeight = 1.75f;
        const float motionHeight = 0.8f;
        using var capsule = new CapsuleShape3D
        {
            Radius = 0.38f,
            Height = capsuleHeight
        };
        var feet = layout.Origin + new Vector3(-1.8f, 0.22f, 26.5f);
        var start = new Transform3D(
            Basis.Identity,
            feet + Vector3.Up * (capsuleHeight * 0.5f));
        using var query = new PhysicsShapeQueryParameters3D
        {
            Shape = capsule,
            Transform = start,
            Motion = Vector3.Up * motionHeight,
            CollisionMask = 1,
            CollideWithAreas = false,
            CollideWithBodies = true,
            Margin = 0.0f
        };
        var fractions = world.DirectSpaceState.CastMotion(query);
        var safeFraction = fractions.Length >= 1
            ? Mathf.Clamp(fractions[0], 0.0f, 1.0f)
            : 1.0f;

        var colliderName = "none";
        query.Transform = new Transform3D(
            Basis.Identity,
            start.Origin + query.Motion * Mathf.Min(1.0f, safeFraction + 0.02f));
        query.Motion = Vector3.Zero;
        var hits = world.DirectSpaceState.IntersectShape(query, 8);
        using var hitsBacking = hits.AsDisposable();
        for (var index = 0; index < hits.Count; index++)
        {
            using var hit = hits[index];
            using var colliderValue = hit[GodotPhysicsResultKeys.Collider];
            if (colliderValue.AsGodotObject() is not Node collider)
            {
                continue;
            }
            colliderName = collider.Name.ToString();
            if (colliderName == "CivicElevatedWalkwayAuthoredCollision")
            {
                break;
            }
        }

        var ready = safeFraction >= 0.35f
            && safeFraction <= 0.55f
            && colliderName == "CivicElevatedWalkwayAuthoredCollision";
        return (ready, safeFraction, colliderName);
    }
}
