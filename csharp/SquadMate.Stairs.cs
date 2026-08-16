using Godot;

namespace OperationSteelTide;

public partial class SquadMate
{
    private Vector3 _lastStairNavigationDirection;

    internal int NavigationStepUpsForDiagnostics { get; private set; }

    private void MaintainStairNavigation(Vector3 destination, float delta)
    {
        if (_combatDesiredDirection.LengthSquared() > 0.01f)
        {
            _lastStairNavigationDirection = _combatDesiredDirection.Normalized();
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

    private void TryNavigationStepUp(Vector3 moveDirection)
    {
        if (!IsOnFloor() || moveDirection.LengthSquared() < 0.01f)
        {
            return;
        }

        const float maxStep = 0.28f;
        var forward = moveDirection.Normalized();
        var bestLift = 0.0f;
        var bestLandingY = GlobalPosition.Y;
        foreach (var distance in new[] { 0.28f, 0.42f, 0.55f })
        {
            var from = GlobalPosition + Vector3.Up * (maxStep + 0.12f) + forward * distance;
            var query = PhysicsRayQueryParameters3D.Create(
                from,
                from + Vector3.Down * (maxStep + 0.45f));
            query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
            query.CollisionMask = 1;
            query.CollideWithAreas = false;
            var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
            if (hit.Count == 0 || hit["normal"].AsVector3().Dot(Vector3.Up) < 0.96f)
            {
                continue;
            }

            var landingY = hit["position"].AsVector3().Y;
            var lift = landingY - GlobalPosition.Y;
            if (lift > bestLift && lift <= maxStep)
            {
                bestLift = lift;
                bestLandingY = landingY;
            }
        }

        if (bestLift <= 0.025f)
        {
            return;
        }

        GlobalPosition = new Vector3(
            GlobalPosition.X + forward.X * 0.035f,
            bestLandingY + 0.03f,
            GlobalPosition.Z + forward.Z * 0.035f);
        var velocity = Velocity;
        velocity.Y = 0.0f;
        Velocity = velocity;
        NavigationStepUpsForDiagnostics++;
    }
}
