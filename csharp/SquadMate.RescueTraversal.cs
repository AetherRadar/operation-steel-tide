using Godot;

namespace OperationSteelTide;

public partial class SquadMate
{
    private const ulong RescueGlassProbeIntervalMilliseconds = 420;
    private const float RescueGlassMinimumDropHeight = 1.35f;
    private const float RescueGlassMaximumDropHeight = 4.25f;
    private const float RescueGlassMaximumTargetDistance = 20.0f;
    private const float RescueGlassProbeDistance = 12.0f;
    private const float RescueGlassApproachOffset = 0.72f;
    private const float RescueGlassCommitDistance = 0.86f;
    private const float RescueGlassLandingOffset = 1.28f;
    private const float RescueGlassBarrierMatchDistance = 0.46f;
    private const float RescueGlassTargetAccessRange = 8.0f;
    private const float RescueGlassProbeHeight = 1.78f;

    // The direct target bearing is checked first. Wider bearings are only reached
    // when the target-facing facade has no usable pane, keeping the normal probe
    // count very small while still allowing a trapped mate to find another wall.
    private static readonly float[] RescueGlassProbeYawRadians =
    {
        0.0f,
        -0.28f,
        0.28f,
        -0.55f,
        0.55f,
        -0.82f,
        0.82f,
        -1.18f,
        1.18f,
        -1.57f,
        1.57f,
        Mathf.Pi
    };

    private sealed class RescueGlassEgressPlan
    {
        public required BreakableGlassField Glass;
        public int ShapeIndex;
        public Vector3 GlassPosition;
        public Vector3 GlassNormal;
        public Vector3 Direction;
        public Vector3 Approach;
        public Vector3 Landing;
        public Vector3 Destination;
    }

    private RescueGlassEgressPlan? _rescueGlassEgressPlan;
    private ulong _rescueGlassNextProbeMilliseconds;
    private bool _navigationTraversalBypassesWindowBarrier;

    internal int RescueGlassProbeComputationsForDiagnostics { get; private set; }
    internal int RescueGlassProbeThrottlesForDiagnostics { get; private set; }
    internal int RescueGlassPlanReusesForDiagnostics { get; private set; }
    internal int RescueGlassShattersForDiagnostics { get; private set; }
    internal bool HasPendingRescueGlassEgress => _rescueGlassEgressPlan is not null;
    internal bool IsRescueGlassTraversalForDiagnostics
        => _navigationTraversalActive && _navigationTraversalBypassesWindowBarrier;

    internal bool TryResolveEmergencyGlassEgress(
        Vector3 destination,
        out SquadNavigationDirective directive)
    {
        directive = SquadNavigationDirective.Walk(GlobalPosition);
        if (!HasActiveReviveTarget
            || _navigationTraversalActive
            || !IsOnFloor()
            || GlobalPosition.Y - destination.Y < RescueGlassMinimumDropHeight
            || GlobalPosition.DistanceSquaredTo(destination)
                > RescueGlassMaximumTargetDistance * RescueGlassMaximumTargetDistance)
        {
            return false;
        }

        var now = Time.GetTicksMsec();
        var plan = _rescueGlassEgressPlan;
        if (plan is not null
            && (!IsInstanceValid(plan.Glass)
                || plan.Destination.DistanceSquaredTo(destination) > 1.0f))
        {
            ResetEmergencyGlassEgressPlan();
            plan = null;
        }

        if (plan is null)
        {
            if (now < _rescueGlassNextProbeMilliseconds)
            {
                RescueGlassProbeThrottlesForDiagnostics++;
                return false;
            }
            _rescueGlassNextProbeMilliseconds = now + RescueGlassProbeIntervalMilliseconds;
            RescueGlassProbeComputationsForDiagnostics++;
            if (!TryBuildRescueGlassEgressPlan(destination, out plan))
            {
                return false;
            }
            _rescueGlassEgressPlan = plan;
        }
        else
        {
            RescueGlassPlanReusesForDiagnostics++;
        }

        if (HorizontalDistanceSquared(GlobalPosition, plan.Approach)
            > RescueGlassCommitDistance * RescueGlassCommitDistance)
        {
            directive = SquadNavigationDirective.Walk(plan.Approach, preciseTrail: true);
            return true;
        }

        if (!TryCommitRescueGlassEgress(plan))
        {
            ResetEmergencyGlassEgressPlan();
            return false;
        }

        directive = SquadNavigationDirective.Walk(GlobalPosition);
        _rescueGlassEgressPlan = null;
        return true;
    }

    private bool TryBuildRescueGlassEgressPlan(
        Vector3 destination,
        out RescueGlassEgressPlan plan)
    {
        plan = null!;
        var baseDirection = destination - GlobalPosition;
        baseDirection.Y = 0.0f;
        if (baseDirection.LengthSquared() < 0.08f)
        {
            baseDirection = _combatDesiredDirection.LengthSquared() > 0.08f
                ? _combatDesiredDirection
                : -GlobalBasis.Z;
            baseDirection.Y = 0.0f;
        }
        if (baseDirection.LengthSquared() < 0.01f)
        {
            baseDirection = Vector3.Forward;
        }
        baseDirection = baseDirection.Normalized();

        foreach (var yaw in RescueGlassProbeYawRadians)
        {
            var direction = baseDirection.Rotated(Vector3.Up, yaw).Normalized();
            if (TryBuildRescueGlassEgressPlanAlongDirection(
                    destination,
                    direction,
                    out plan))
            {
                return true;
            }
        }
        return false;
    }

    private bool TryBuildRescueGlassEgressPlanAlongDirection(
        Vector3 destination,
        Vector3 probeDirection,
        out RescueGlassEgressPlan plan)
    {
        plan = null!;
        var feet = GlobalPosition;
        var glassRayFrom = feet + Vector3.Up * RescueGlassProbeHeight;
        var glassRayTo = glassRayFrom + probeDirection * RescueGlassProbeDistance;
        if (!PhysicsRaycast.TryHit(
                GetWorld3D(),
                glassRayFrom,
                glassRayTo,
                BreakableGlassField.GlassCollisionLayer,
                out var glassHit,
                collideWithAreas: true,
                collideWithBodies: false)
            || glassHit.Collider is not BreakableGlassField glass
            || !IsInstanceValid(glass))
        {
            return false;
        }

        var direction = glassHit.Position - feet;
        direction.Y = 0.0f;
        if (direction.LengthSquared() < 0.25f)
        {
            return false;
        }
        direction = direction.Normalized();
        var approach = glassHit.Position - direction * RescueGlassApproachOffset;
        approach.Y = feet.Y;
        var exclude = NavigationProbeExclusions();
        var barrierRayFrom = feet + Vector3.Up * RescueGlassProbeHeight;
        var barrierRayTo = glassHit.Position + direction * 0.28f;
        if (!PhysicsRaycast.TryHit(
                GetWorld3D(),
                barrierRayFrom,
                barrierRayTo,
                exclude,
                1,
                out var barrierHit)
            || barrierHit.Position.DistanceSquaredTo(glassHit.Position)
                > RescueGlassBarrierMatchDistance * RescueGlassBarrierMatchDistance
            || TestMove(
                GlobalTransform,
                approach - feet,
                null,
                NavigationTraversalSafeMargin,
                recoveryAsCollision: false,
                maxCollisions: 4))
        {
            return false;
        }

        var landingSample = glassHit.Position + direction * RescueGlassLandingOffset;
        landingSample.Y = feet.Y + 0.55f;
        if (!PhysicsRaycast.TryHit(
                GetWorld3D(),
                landingSample,
                landingSample + Vector3.Down * (RescueGlassMaximumDropHeight + 0.9f),
                exclude,
                1,
                out var landingHit)
            || landingHit.Normal.Dot(Vector3.Up) < 0.78f)
        {
            return false;
        }

        var dropHeight = feet.Y - landingHit.Position.Y;
        var landing = landingHit.Position + Vector3.Up * NavigationTraversalClearance;
        var upperOutside = new Vector3(landing.X, feet.Y, landing.Z);
        if (dropHeight < RescueGlassMinimumDropHeight
            || dropHeight > RescueGlassMaximumDropHeight
            || !HasNavigationLandingClearance(landing)
            || !HasNavigationLandingClearance(upperOutside)
            || HorizontalDistanceSquared(landing, destination)
                > RescueGlassTargetAccessRange * RescueGlassTargetAccessRange
            || PhysicsRaycast.HasHit(
                GetWorld3D(),
                landing + Vector3.Up * 0.78f,
                destination + Vector3.Up * 0.62f,
                exclude,
                1))
        {
            return false;
        }

        plan = new RescueGlassEgressPlan
        {
            Glass = glass,
            ShapeIndex = glassHit.Shape,
            GlassPosition = glassHit.Position,
            GlassNormal = glassHit.Normal,
            Direction = direction,
            Approach = approach,
            Landing = landing,
            Destination = destination
        };
        return true;
    }

    private bool TryCommitRescueGlassEgress(RescueGlassEgressPlan plan)
    {
        if (!IsInstanceValid(plan.Glass) || !IsOnFloor())
        {
            return false;
        }

        var feet = GlobalPosition;
        var horizontalDistance = new Vector2(
            plan.GlassPosition.X - feet.X,
            plan.GlassPosition.Z - feet.Z).Length();
        if (horizontalDistance > RescueGlassApproachOffset + RescueGlassCommitDistance)
        {
            return false;
        }

        var newlyShattered = plan.Glass.TryShatterShape(
            plan.ShapeIndex,
            plan.GlassPosition,
            plan.GlassNormal,
            plan.Direction,
            30.0f,
            spawnEffects: true);
        if (!newlyShattered && !plan.Glass.IsShapeShattered(plan.ShapeIndex))
        {
            return false;
        }
        if (newlyShattered)
        {
            RescueGlassShattersForDiagnostics++;
        }

        var riseTravel = Mathf.Clamp(horizontalDistance - 0.38f, 0.08f, 0.48f);
        var rise = feet + plan.Direction * riseTravel + Vector3.Up * 0.22f;
        var cross = new Vector3(plan.Landing.X, feet.Y + 0.08f, plan.Landing.Z);
        var dropHeight = feet.Y - plan.Landing.Y;
        var path = new NavigationTraversalPath(
            SquadTraversalKind.Drop,
            feet,
            rise,
            cross,
            plan.Landing,
            plan.Direction,
            Mathf.Clamp(0.68f + dropHeight * 0.1f, 0.82f, 1.12f));
        _navigationTraversalBypassesWindowBarrier = true;
        if (BeginNavigationTraversal(path, -1))
        {
            return true;
        }
        _navigationTraversalBypassesWindowBarrier = false;
        return false;
    }

    internal void ResetEmergencyGlassEgressPlan()
    {
        _rescueGlassEgressPlan = null;
        _rescueGlassNextProbeMilliseconds = 0;
    }

    private static float HorizontalDistanceSquared(Vector3 from, Vector3 to)
    {
        var x = to.X - from.X;
        var z = to.Z - from.Z;
        return x * x + z * z;
    }
}
