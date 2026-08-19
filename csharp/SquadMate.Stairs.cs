using Godot;

namespace OperationSteelTide;

public partial class SquadMate
{
    private Vector3 _lastStairNavigationDirection;
    private readonly CapsuleShape3D _navigationStepClearanceShape = new()
    {
        Radius = NavigationBodyRadius,
        Height = NavigationBodyHeight
    };

    internal int NavigationStepUpsForDiagnostics { get; private set; }

    private void MaintainStairNavigation(Vector3 destination, float delta)
    {
        var pathDirection = _combatPathDirection.LengthSquared() > 0.01f
            ? _combatPathDirection
            : _combatDesiredDirection;
        if (pathDirection.LengthSquared() > 0.01f)
        {
            _lastStairNavigationDirection = pathDirection.Normalized();
            return;
        }
        if (destination.Y - GlobalPosition.Y <= 0.42f
            || _lastStairNavigationDirection.LengthSquared() <= 0.01f)
        {
            return;
        }

        _combatDesiredDirection = _lastStairNavigationDirection;
        _combatMoveRequested = true;
        var speed = 3.2f * OperatorRoles.Spec(Role).MovementMultiplier;
        var velocity = Velocity;
        velocity.X = Mathf.MoveToward(
            velocity.X,
            _lastStairNavigationDirection.X * speed,
            delta * 15.0f);
        velocity.Z = Mathf.MoveToward(
            velocity.Z,
            _lastStairNavigationDirection.Z * speed,
            delta * 15.0f);
        Velocity = velocity;
    }

    private void TryNavigationStepUp(Vector3 moveDirection, Vector3 destination)
    {
        // Do not snap onto the adjacent upper flight while descending through a switchback.
        if (_requiredStepRecoveryActive
            || !IsOnFloor()
            || moveDirection.LengthSquared() < 0.01f
            || destination.Y < GlobalPosition.Y - 0.08f)
        {
            return;
        }

        const float maxStep = 0.28f;
        var forward = moveDirection.Normalized();
        var exclude = BuildNavigationStepExclusions();
        using var excludeBacking = exclude.AsDisposable();
        var selectedLift = 0.0f;
        var selectedLanding = GlobalPosition;
        foreach (var distance in new[] { 0.28f, 0.42f, 0.55f })
        {
            var from = GlobalPosition + Vector3.Up * (maxStep + 0.12f) + forward * distance;
            if (!PhysicsRaycast.TryHit(
                    GetWorld3D(),
                    from,
                    from + Vector3.Down * (maxStep + 0.45f),
                    exclude,
                    1,
                    out var hit)
                || hit.Normal.Dot(Vector3.Up) < 0.96f)
            {
                continue;
            }

            var landingY = hit.Position.Y;
            var lift = landingY - GlobalPosition.Y;
            var landingDistance = Mathf.Max(0.12f, distance - 0.08f);
            var landing = new Vector3(
                GlobalPosition.X + forward.X * landingDistance,
                landingY + NavigationTraversalClearance,
                GlobalPosition.Z + forward.Z * landingDistance);
            if (lift > 0.025f
                && lift <= maxStep
                && HasNavigationStepClearance(landing, exclude))
            {
                selectedLift = lift;
                selectedLanding = landing;
                break;
            }
        }

        if (selectedLift <= 0.025f)
        {
            return;
        }

        GlobalPosition = selectedLanding;
        var velocity = Velocity;
        velocity.Y = 0.0f;
        Velocity = velocity;
        NavigationStepUpsForDiagnostics++;
    }

    private Godot.Collections.Array<Rid> BuildNavigationStepExclusions()
    {
        var exclude = new Godot.Collections.Array<Rid> { GetRid() };
        if (IsInstanceValid(Leader))
        {
            exclude.Add(Leader.GetRid());
        }
        if (ActiveReviveTargetNode is CollisionObject3D target
            && IsInstanceValid(target)
            && !ReferenceEquals(target, Leader))
        {
            exclude.Add(target.GetRid());
        }
        return exclude;
    }

    private bool HasNavigationStepClearance(
        Vector3 landing,
        Godot.Collections.Array<Rid> exclude)
    {
        using var query = new PhysicsShapeQueryParameters3D
        {
            Shape = _navigationStepClearanceShape,
            Transform = new Transform3D(
                Basis.Identity,
                landing + Vector3.Up * NavigationBodyCenterHeight),
            CollisionMask = 1,
            CollideWithAreas = false,
            CollideWithBodies = true,
            Margin = 0.01f,
            Exclude = exclude
        };
        var hits = GetWorld3D().DirectSpaceState.IntersectShape(query, 4);
        using var hitsBacking = hits.AsDisposable();
        return hits.Count == 0;
    }
}
