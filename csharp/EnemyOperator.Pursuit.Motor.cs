using Godot;

namespace OperationSteelTide;

public partial class EnemyOperator
{
    private const float PursuitLadderSpeed = 2.65f;

    private readonly Godot.Collections.Array<Rid> _pursuitNavigationExclusions = new();
    private bool _pursuitNavigationMotorActive;
    private Vector3 _pursuitNavigationMotorDirection;
    private Vector3 _pursuitNavigationMotorDestination;
    private Vector3 _lastPursuitStairDirection;
    private int _pursuitRouteStallCount;
    private bool _pursuitLadderActive;
    private bool _pursuitLadderAscending;
    private int _pursuitLadderDirectedEdgeId = -1;
    private float _pursuitLadderElapsed;
    private float _pursuitLadderDuration;
    private AuthoredLadderTraversalPath _pursuitLadderPath;

    internal bool IsPursuitLadderActiveForDiagnostics => _pursuitLadderActive;
    internal int PursuitLadderTraversalsForDiagnostics { get; private set; }

    private bool UpdatePursuitLadderTraversal(
        float delta,
        SquadNavigationDirective directive)
    {
        if (!_pursuitLadderActive
            && !TryBeginPursuitLadderTraversal(directive))
        {
            return false;
        }
        if (_pursuitLadderDirectedEdgeId != directive.DirectedEdgeId)
        {
            CancelPursuitLadderTraversal();
            return false;
        }

        _pursuitLadderElapsed = Mathf.Min(
            _pursuitLadderDuration,
            _pursuitLadderElapsed + Mathf.Max(0.0f, delta));
        var progress = Mathf.Clamp(
            _pursuitLadderElapsed / Mathf.Max(0.001f, _pursuitLadderDuration),
            0.0f,
            1.0f);
        var pathProgress = _pursuitLadderAscending ? progress : 1.0f - progress;
        GlobalPosition = _pursuitLadderPath.Evaluate(_pursuitLadderPath.Length * pathProgress);
        Velocity = Vector3.Zero;
        LookAt(GlobalPosition - _pursuitLadderPath.Outward, Vector3.Up);
        _pursuitProgressOrigin = GlobalPosition;
        _pursuitProgressTimer = 0.0f;
        ResetPursuitNavigationMotorFrame();
        if (progress < 0.9999f)
        {
            return true;
        }

        CompleteActivePursuitLadderDirective(_pursuitLadderDirectedEdgeId);
        _pursuitLadderActive = false;
        _pursuitLadderAscending = false;
        _pursuitLadderDirectedEdgeId = -1;
        _pursuitLadderElapsed = 0.0f;
        _pursuitLadderDuration = 0.0f;
        _pursuitLadderPath = default;
        PursuitLadderTraversalsForDiagnostics++;
        return true;
    }

    private bool TryBeginPursuitLadderTraversal(SquadNavigationDirective directive)
    {
        var ascending = directive.Target.Y > directive.ActionOrigin.Y;
        var bottomFeet = ascending ? directive.ActionOrigin : directive.Target;
        var topFeet = ascending ? directive.Target : directive.ActionOrigin;
        if (!AuthoredLadderTraversalPath.TryCreate(
                bottomFeet,
                topFeet,
                directive.ActionOutward,
                out var path))
        {
            return false;
        }

        _pursuitLadderActive = true;
        _pursuitLadderAscending = ascending;
        _pursuitLadderDirectedEdgeId = directive.DirectedEdgeId;
        _pursuitLadderElapsed = 0.0f;
        _pursuitLadderDuration = Mathf.Max(0.65f, path.Length / PursuitLadderSpeed);
        _pursuitLadderPath = path;
        GlobalPosition = directive.ActionOrigin;
        Velocity = Vector3.Zero;
        return true;
    }

    private void CancelPursuitLadderTraversal()
    {
        _pursuitLadderActive = false;
        _pursuitLadderAscending = false;
        _pursuitLadderDirectedEdgeId = -1;
        _pursuitLadderElapsed = 0.0f;
        _pursuitLadderDuration = 0.0f;
        _pursuitLadderPath = default;
    }

    internal bool AdvancePursuitLadderForDiagnostics(
        float delta,
        Vector3 bottomFeet,
        Vector3 topFeet,
        Vector3 outward,
        bool startAtTop = false)
        => UpdatePursuitLadderTraversal(
            delta,
            new SquadNavigationDirective(
                startAtTop ? bottomFeet : topFeet,
                SquadTraversalKind.Ladder,
                DirectedEdgeId: -1,
                Required: true,
                ActionOrigin: startAtTop ? topFeet : bottomFeet,
                ActionOutward: outward));

    private bool IsPursuitCorridorClear(Vector3 point)
    {
        var offset = point - GlobalPosition;
        var horizontal = new Vector2(offset.X, offset.Z);
        var distance = horizontal.Length();
        if (distance <= 0.2f)
        {
            return Mathf.Abs(offset.Y) <= PursuitTrailAttachHeight;
        }
        if (Mathf.Abs(offset.Y) > PursuitTrailAttachHeight)
        {
            return false;
        }

        var direction2 = horizontal / distance;
        var direction = new Vector3(direction2.X, 0.0f, direction2.Y);
        var side = new Vector3(-direction.Z, 0.0f, direction.X) * 0.3f;
        PreparePursuitNavigationExclusions();
        for (var ray = 0; ray < 3; ray++)
        {
            var rayOffset = ray == 1 ? side : ray == 2 ? -side : Vector3.Zero;
            var from = GlobalPosition + Vector3.Up * 0.82f + rayOffset;
            var to = new Vector3(point.X, GlobalPosition.Y + 0.82f, point.Z) + rayOffset;
            if (PhysicsRaycast.HasHit(
                    GetWorld3D(),
                    from,
                    to,
                    _pursuitNavigationExclusions,
                    1))
            {
                return false;
            }
        }
        return true;
    }

    private void PreparePursuitNavigationExclusions()
    {
        _pursuitNavigationExclusions.Clear();
        _pursuitNavigationExclusions.Add(GetRid());
        if (AssignedCombatTargetNode() is CollisionObject3D target
            && IsInstanceValid(target))
        {
            _pursuitNavigationExclusions.Add(target.GetRid());
        }
    }

    private Vector3 MaintainPursuitStairDirection(Vector3 direction, Vector3 destination)
    {
        direction.Y = 0.0f;
        if (direction.LengthSquared() > 0.01f)
        {
            _lastPursuitStairDirection = direction.Normalized();
            return _lastPursuitStairDirection;
        }
        if (destination.Y - GlobalPosition.Y > 0.34f
            && _lastPursuitStairDirection.LengthSquared() > 0.01f)
        {
            return _lastPursuitStairDirection;
        }
        return Vector3.Zero;
    }

    private void PreparePursuitNavigationMotor(Vector3 direction, Vector3 destination)
    {
        _pursuitNavigationMotorActive = direction.LengthSquared() > 0.01f;
        _pursuitNavigationMotorDirection = direction;
        _pursuitNavigationMotorDestination = destination;
    }

    private void ResetPursuitNavigationMotorFrame()
    {
        _pursuitNavigationMotorActive = false;
        _pursuitNavigationMotorDirection = Vector3.Zero;
        _pursuitNavigationMotorDestination = GlobalPosition;
    }

    private void TryPursuitNavigationStepUp()
    {
        if (!_pursuitNavigationMotorActive
            || !IsOnFloor()
            || _pursuitNavigationMotorDirection.LengthSquared() < 0.01f
            || _pursuitNavigationMotorDestination.Y < GlobalPosition.Y - 0.08f)
        {
            return;
        }

        var forward = _pursuitNavigationMotorDirection.Normalized();
        PreparePursuitNavigationExclusions();
        if (!TryFindPursuitStepLanding(forward, 0.28f, out var landing)
            && !TryFindPursuitStepLanding(forward, 0.42f, out landing)
            && !TryFindPursuitStepLanding(forward, 0.55f, out landing))
        {
            return;
        }

        GlobalPosition = landing;
        var velocity = Velocity;
        velocity.Y = 0.0f;
        Velocity = velocity;
        PursuitNavigationStepUpsForDiagnostics++;
    }

    private bool TryFindPursuitStepLanding(
        Vector3 forward,
        float distance,
        out Vector3 landing)
    {
        const float maximumStep = 0.28f;
        landing = default;
        var from = GlobalPosition + Vector3.Up * (maximumStep + 0.12f) + forward * distance;
        if (!PhysicsRaycast.TryHit(
                GetWorld3D(),
                from,
                from + Vector3.Down * (maximumStep + 0.45f),
                _pursuitNavigationExclusions,
                1,
                out var hit)
            || hit.Normal.Dot(Vector3.Up) < 0.96f)
        {
            return false;
        }

        var lift = hit.Position.Y - GlobalPosition.Y;
        if (lift is <= 0.025f or > maximumStep)
        {
            return false;
        }
        var landingDistance = Mathf.Max(0.12f, distance - 0.08f);
        landing = new Vector3(
            GlobalPosition.X + forward.X * landingDistance,
            hit.Position.Y + 0.035f,
            GlobalPosition.Z + forward.Z * landingDistance);

        var liftedTransform = GlobalTransform;
        liftedTransform.Origin += Vector3.Up * (lift + 0.04f);
        var horizontalMotion = landing - liftedTransform.Origin;
        horizontalMotion.Y = 0.0f;
        return !TestMove(
            liftedTransform,
            horizontalMotion,
            null,
            0.01f,
            recoveryAsCollision: false,
            maxCollisions: 4);
    }

    internal bool WouldWorldMovementCollideForDiagnostics(Vector3 motion)
        => TestMove(
            GlobalTransform,
            motion,
            null,
            0.01f,
            recoveryAsCollision: false,
            maxCollisions: 4);

    private void RecoverPursuitNavigationRoute()
    {
        _pursuitRouteStallCount++;
        PursuitRouteRecoveriesForDiagnostics++;
        if (_pursuitRouteStallCount == 1)
        {
            if (_pursuitTargetTrail is not null
                && _pursuitTrailCursor > _pursuitTargetTrail.OldestSequence)
            {
                _pursuitTrailCursor--;
                return;
            }
            if (_pursuitStaticRouteCursor > 0)
            {
                _pursuitStaticRouteCursor--;
                return;
            }
        }

        InvalidatePursuitTrailRoute();
        InvalidateStaticPursuitRoute();
        _pursuitNextTrailAttachMilliseconds = 0;
        _pursuitNextStaticPlanMilliseconds = 0;
        _avoidanceSide *= -1.0f;
        _avoidanceHoldTimer = 0.75f;
    }
}
