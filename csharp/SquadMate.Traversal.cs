using Godot;

namespace OperationSteelTide;

public partial class SquadMate
{
    private const float NavigationVaultMinHeight = 0.3f;
    private const float NavigationVaultMaxHeight = 1.22f;
    private const float NavigationVaultReach = 1.05f;
    private const float NavigationDropMinHeight = 0.48f;
    private const float NavigationDropMaxHeight = 2.45f;
    private const float NavigationTraversalClearance = 0.075f;
    private const float NavigationTraversalSafeMargin = 0.015f;
    private const float NavigationTraversalFirstPhase = 0.32f;
    private const float NavigationTraversalSecondPhase = 0.74f;
    private const float NavigationBodyRadius = 0.37f;
    private const float NavigationBodyHeight = 1.76f;
    private const float NavigationBodyCenterHeight = NavigationBodyHeight * 0.5f;

    private bool _navigationTraversalActive;
    private SquadTraversalKind _navigationTraversalKind = SquadTraversalKind.Walk;
    private SquadTraversalKind _lastCompletedTraversalKind = SquadTraversalKind.Walk;
    private float _navigationTraversalElapsed;
    private float _navigationTraversalDuration;
    private Vector3 _navigationTraversalStart;
    private Vector3 _navigationTraversalRise;
    private Vector3 _navigationTraversalCross;
    private Vector3 _navigationTraversalLanding;
    private Vector3 _navigationTraversalDirection;
    private int _navigationTraversalDirectedEdgeId = -1;
    private string _navigationTraversalBlocker = string.Empty;

    internal int CompletedNavigationTraversalsForDiagnostics { get; private set; }
    internal int RejectedNavigationTraversalsForDiagnostics { get; private set; }
    internal SquadTraversalKind ActiveNavigationTraversalKindForDiagnostics => _navigationTraversalActive
        ? _navigationTraversalKind
        : SquadTraversalKind.Walk;
    internal SquadTraversalKind LastCompletedNavigationTraversalKindForDiagnostics
        => _lastCompletedTraversalKind;
    internal string NavigationTraversalBlockerForDiagnostics => _navigationTraversalBlocker;

    private readonly record struct NavigationTraversalPath(
        SquadTraversalKind Kind,
        Vector3 Start,
        Vector3 Rise,
        Vector3 Cross,
        Vector3 Landing,
        Vector3 Direction,
        float Duration);

    private bool UpdateActiveNavigationTraversal(float delta)
    {
        if (!_navigationTraversalActive)
        {
            return false;
        }

        var previousProgress = NavigationTraversalProgress;
        _navigationTraversalElapsed = Mathf.Min(
            _navigationTraversalDuration,
            _navigationTraversalElapsed + Mathf.Max(0.0f, delta));
        var progress = NavigationTraversalProgress;
        var checkpoints = new[] { NavigationTraversalFirstPhase, NavigationTraversalSecondPhase, progress };
        var fromProgress = previousProgress;
        foreach (var checkpoint in checkpoints)
        {
            var toProgress = Mathf.Min(progress, checkpoint);
            if (toProgress <= fromProgress + 0.0001f)
            {
                continue;
            }
            var target = EvaluateNavigationTraversal(toProgress);
            var motion = target - GlobalPosition;
            var collision = motion.LengthSquared() > 0.000001f
                ? MoveAndCollide(
                    motion,
                    testOnly: false,
                    safeMargin: NavigationTraversalSafeMargin,
                    recoveryAsCollision: false,
                    maxCollisions: 4)
                : null;
            if (collision is not null)
            {
                CancelNavigationTraversal($"runtime:{DescribeNavigationTraversalCollision(collision)}");
                return true;
            }
            fromProgress = toProgress;
        }

        Velocity = Vector3.Zero;
        _combatMoveRequested = true;
        _combatDesiredDirection = _navigationTraversalDirection;
        if (progress >= 0.9999f)
        {
            CompleteNavigationTraversal();
        }
        return true;
    }

    private float NavigationTraversalProgress => Mathf.Clamp(
        _navigationTraversalElapsed / Mathf.Max(0.001f, _navigationTraversalDuration),
        0.0f,
        1.0f);

    private Vector3 EvaluateNavigationTraversal(float progress)
    {
        progress = Mathf.Clamp(progress, 0.0f, 1.0f);
        if (progress <= NavigationTraversalFirstPhase)
        {
            var t = Mathf.SmoothStep(0.0f, 1.0f, progress / NavigationTraversalFirstPhase);
            return _navigationTraversalStart.Lerp(_navigationTraversalRise, t);
        }
        if (progress <= NavigationTraversalSecondPhase)
        {
            var t = Mathf.SmoothStep(
                0.0f,
                1.0f,
                (progress - NavigationTraversalFirstPhase)
                    / (NavigationTraversalSecondPhase - NavigationTraversalFirstPhase));
            return _navigationTraversalRise.Lerp(_navigationTraversalCross, t);
        }
        var settle = Mathf.SmoothStep(
            0.0f,
            1.0f,
            (progress - NavigationTraversalSecondPhase) / (1.0f - NavigationTraversalSecondPhase));
        return _navigationTraversalCross.Lerp(_navigationTraversalLanding, settle);
    }

    private bool TryBeginNavigationTraversal(SquadNavigationDirective directive)
    {
        if (_navigationTraversalActive || directive.Kind is not (SquadTraversalKind.Vault or SquadTraversalKind.Drop))
        {
            return _navigationTraversalActive;
        }
        var direction = directive.Target - GlobalPosition;
        direction.Y = 0.0f;
        var planned = directive.Kind == SquadTraversalKind.Vault
            ? TryBuildNavigationVaultPath(direction, out var path)
            : TryBuildNavigationDropPath(direction, out path);
        return planned && BeginNavigationTraversal(path, directive.DirectedEdgeId);
    }

    private bool TryBeginTraversalRecovery(Vector3 direction)
    {
        if (_navigationTraversalActive || !IsOnFloor())
        {
            return false;
        }
        if (TryBuildNavigationVaultPath(direction, out var vault))
        {
            return BeginNavigationTraversal(vault, -1);
        }
        return TryBuildNavigationDropPath(direction, out var drop)
            && BeginNavigationTraversal(drop, -1);
    }

    private bool TryBeginDropTowardDestination(Vector3 destination)
    {
        if (_navigationTraversalActive || !IsOnFloor()
            || destination.Y >= GlobalPosition.Y - NavigationDropMinHeight)
        {
            return false;
        }
        var direction = destination - GlobalPosition;
        direction.Y = 0.0f;
        if (direction.LengthSquared() < 0.08f)
        {
            direction = _lastStairNavigationDirection;
        }
        return TryBuildNavigationDropPath(direction, out var drop)
            && BeginNavigationTraversal(drop, -1);
    }

    private bool TryBeginVaultTowardDestination(Vector3 destination)
    {
        if (_navigationTraversalActive || !IsOnFloor())
        {
            return false;
        }
        var direction = destination - GlobalPosition;
        direction.Y = 0.0f;
        return TryBuildNavigationVaultPath(direction, out var vault)
            && BeginNavigationTraversal(vault, -1);
    }

    private bool TryBuildNavigationVaultPath(Vector3 direction, out NavigationTraversalPath path)
    {
        path = default;
        if (!TryNormalizeNavigationDirection(ref direction) || !IsOnFloor())
        {
            return false;
        }

        var feet = GlobalPosition;
        var exclude = new Godot.Collections.Array<Rid> { GetRid() };
        var space = GetWorld3D().DirectSpaceState;
        var obstacleQuery = PhysicsRayQueryParameters3D.Create(
            feet + Vector3.Up * 0.38f,
            feet + Vector3.Up * 0.38f + direction * NavigationVaultReach);
        obstacleQuery.CollisionMask = 1;
        obstacleQuery.CollideWithAreas = false;
        obstacleQuery.Exclude = exclude;
        var obstacleHit = space.IntersectRay(obstacleQuery);
        if (obstacleHit.Count == 0)
        {
            return false;
        }

        var obstacle = obstacleHit["collider"].AsGodotObject();
        var obstacleShape = obstacleHit.ContainsKey("shape") ? obstacleHit["shape"].AsInt32() : -1;
        var obstaclePosition = obstacleHit["position"].AsVector3();
        var obstacleDistance = new Vector2(
            obstaclePosition.X - feet.X,
            obstaclePosition.Z - feet.Z).Length();
        foreach (var inset in new[] { 0.08f, 0.2f, 0.34f })
        {
            var sampleDistance = Mathf.Clamp(
                obstacleDistance + inset,
                0.34f,
                NavigationVaultReach);
            var sample = feet + direction * sampleDistance;
            var topQuery = PhysicsRayQueryParameters3D.Create(
                sample + Vector3.Up * (NavigationVaultMaxHeight + 0.32f),
                sample + Vector3.Up * 0.08f);
            topQuery.CollisionMask = 1;
            topQuery.CollideWithAreas = false;
            topQuery.Exclude = exclude;
            var topHit = space.IntersectRay(topQuery);
            if (topHit.Count == 0)
            {
                continue;
            }
            var topCollider = topHit["collider"].AsGodotObject();
            var topShape = topHit.ContainsKey("shape") ? topHit["shape"].AsInt32() : -1;
            var topNormal = topHit["normal"].AsVector3();
            if (topCollider != obstacle
                || obstacleShape >= 0 && topShape >= 0 && topShape != obstacleShape
                || topNormal.Dot(Vector3.Up) < 0.78f)
            {
                continue;
            }
            var top = topHit["position"].AsVector3();
            var lift = top.Y - feet.Y;
            if (lift < NavigationVaultMinHeight || lift > NavigationVaultMaxHeight)
            {
                continue;
            }
            var landing = top + Vector3.Up * NavigationTraversalClearance;
            var rise = feet + Vector3.Up * (lift + 0.24f);
            var cross = landing + Vector3.Up * 0.24f;
            var candidate = new NavigationTraversalPath(
                SquadTraversalKind.Vault,
                feet,
                rise,
                cross,
                landing,
                direction,
                Mathf.Clamp(0.5f + lift * 0.18f, 0.54f, 0.8f));
            if (HasNavigationLandingClearance(landing)
                && ValidateNavigationTraversalPath(candidate, out _))
            {
                path = candidate;
                return true;
            }
        }
        return false;
    }

    private bool TryBuildNavigationDropPath(Vector3 direction, out NavigationTraversalPath path)
    {
        path = default;
        if (!TryNormalizeNavigationDirection(ref direction) || !IsOnFloor())
        {
            return false;
        }
        var feet = GlobalPosition;
        var exclude = new Godot.Collections.Array<Rid> { GetRid() };
        var space = GetWorld3D().DirectSpaceState;
        var guardQuery = PhysicsRayQueryParameters3D.Create(
            feet + Vector3.Up * 0.62f,
            feet + Vector3.Up * 0.62f + direction * 0.82f);
        guardQuery.CollisionMask = 1;
        guardQuery.CollideWithAreas = false;
        guardQuery.Exclude = exclude;
        if (space.IntersectRay(guardQuery).Count > 0)
        {
            return false;
        }

        foreach (var distance in new[] { 0.78f, 1.02f, 1.26f })
        {
            var sample = feet + direction * distance;
            var landingQuery = PhysicsRayQueryParameters3D.Create(
                sample + Vector3.Up * 0.55f,
                sample + Vector3.Down * (NavigationDropMaxHeight + 0.35f));
            landingQuery.CollisionMask = 1;
            landingQuery.CollideWithAreas = false;
            landingQuery.Exclude = exclude;
            var landingHit = space.IntersectRay(landingQuery);
            if (landingHit.Count == 0 || landingHit["normal"].AsVector3().Dot(Vector3.Up) < 0.78f)
            {
                continue;
            }
            var landingSurface = landingHit["position"].AsVector3();
            var drop = feet.Y - landingSurface.Y;
            if (drop < NavigationDropMinHeight || drop > NavigationDropMaxHeight)
            {
                continue;
            }
            var landing = landingSurface + Vector3.Up * NavigationTraversalClearance;
            var rise = feet + direction * 0.68f + Vector3.Up * 0.18f;
            var cross = new Vector3(landing.X, feet.Y + 0.08f, landing.Z);
            var candidate = new NavigationTraversalPath(
                SquadTraversalKind.Drop,
                feet,
                rise,
                cross,
                landing,
                direction,
                Mathf.Clamp(0.58f + drop * 0.12f, 0.64f, 0.9f));
            if (HasNavigationLandingClearance(landing)
                && ValidateNavigationTraversalPath(candidate, out _))
            {
                path = candidate;
                return true;
            }
        }
        return false;
    }

    private static bool TryNormalizeNavigationDirection(ref Vector3 direction)
    {
        direction.Y = 0.0f;
        if (direction.LengthSquared() < 0.01f)
        {
            return false;
        }
        direction = direction.Normalized();
        return true;
    }

    private bool HasNavigationLandingClearance(Vector3 landing)
    {
        var query = new PhysicsShapeQueryParameters3D
        {
            Shape = new CapsuleShape3D { Radius = NavigationBodyRadius, Height = NavigationBodyHeight },
            Transform = new Transform3D(
                Basis.Identity,
                landing + Vector3.Up * NavigationBodyCenterHeight),
            CollisionMask = 1,
            CollideWithAreas = false,
            CollideWithBodies = true,
            Margin = 0.005f,
            Exclude = new Godot.Collections.Array<Rid> { GetRid() }
        };
        return GetWorld3D().DirectSpaceState.IntersectShape(query, 8).Count == 0;
    }

    private bool ValidateNavigationTraversalPath(NavigationTraversalPath path, out string blocker)
    {
        blocker = string.Empty;
        var points = new[] { path.Start, path.Rise, path.Cross, path.Landing };
        var probe = GlobalTransform;
        for (var index = 0; index < points.Length - 1; index++)
        {
            var motion = points[index + 1] - points[index];
            if (motion.LengthSquared() < 0.000001f)
            {
                continue;
            }
            probe.Origin = points[index];
            if (!TestMove(
                    probe,
                    motion,
                    null,
                    NavigationTraversalSafeMargin,
                    recoveryAsCollision: false,
                    maxCollisions: 4))
            {
                continue;
            }
            blocker = $"segment_{index + 1}";
            return false;
        }
        return true;
    }

    private bool BeginNavigationTraversal(NavigationTraversalPath path, int directedEdgeId)
    {
        if (_navigationTraversalActive)
        {
            return false;
        }
        _navigationTraversalKind = path.Kind;
        _navigationTraversalStart = path.Start;
        _navigationTraversalRise = path.Rise;
        _navigationTraversalCross = path.Cross;
        _navigationTraversalLanding = path.Landing;
        _navigationTraversalDirection = path.Direction;
        _navigationTraversalDuration = path.Duration;
        _navigationTraversalElapsed = 0.0f;
        _navigationTraversalDirectedEdgeId = directedEdgeId;
        _navigationTraversalBlocker = string.Empty;
        _navigationTraversalActive = true;
        Velocity = Vector3.Zero;
        ResetMovementProgress();
        return true;
    }

    private void CompleteNavigationTraversal()
    {
        _navigationTraversalActive = false;
        _lastCompletedTraversalKind = _navigationTraversalKind;
        CompletedNavigationTraversalsForDiagnostics++;
        Velocity = new Vector3(
            _navigationTraversalDirection.X * 2.4f,
            -0.1f,
            _navigationTraversalDirection.Z * 2.4f);
        _navigationTraversalDirectedEdgeId = -1;
        ResetMovementProgress();
    }

    private void CancelNavigationTraversal(string blocker)
    {
        _navigationTraversalActive = false;
        _navigationTraversalBlocker = blocker;
        RejectedNavigationTraversalsForDiagnostics++;
        Velocity = Vector3.Zero;
        if (_navigationTraversalDirectedEdgeId >= 0 && IsInstanceValid(Main))
        {
            Main.ReportSquadTraversalFailure(this, _navigationTraversalDirectedEdgeId);
        }
        _navigationTraversalDirectedEdgeId = -1;
        RequestNavigationRecovery(forceEscape: true);
    }

    private static string DescribeNavigationTraversalCollision(KinematicCollision3D collision)
    {
        var collider = collision.GetCollider() as Node;
        return collider?.Name.ToString() ?? "unknown";
    }

    private void CancelNavigationTraversal()
    {
        _navigationTraversalActive = false;
        _navigationTraversalDirectedEdgeId = -1;
        _navigationTraversalBlocker = string.Empty;
    }

    internal bool BeginNavigationVaultForDiagnostics(Vector3 direction)
        => TryBuildNavigationVaultPath(direction, out var path)
            && BeginNavigationTraversal(path, -1);

    internal bool BeginNavigationDropForDiagnostics(Vector3 direction)
        => TryBuildNavigationDropPath(direction, out var path)
            && BeginNavigationTraversal(path, -1);

    internal bool CanPlanNavigationDropForDiagnostics(Vector3 direction)
        => TryBuildNavigationDropPath(direction, out _);
}
