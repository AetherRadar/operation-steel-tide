using Godot;

namespace OperationSteelTide;

public partial class TacticalPlayer
{
    private const float LowObstacleVaultMinHeight = 0.3f;
    private const float LowObstacleVaultMaxHeight = 1.1f;
    private const float LowObstacleVaultReach = 0.95f;

    public int SuccessfulVaultsForDiagnostics { get; private set; }
    public float MaximumVaultHeightForDiagnostics => LowObstacleVaultMaxHeight;
    public string LastVaultResultForDiagnostics { get; private set; } = "not_attempted";

    public override void _Input(InputEvent @event)
    {
        TryRearmMovementInput(@event);
    }

    private bool TryRearmMovementInput(InputEvent @event)
    {
        if (_movementInputArmed
            || UiLocked
            || IsDead
            || IsInVehicle
            || _isClimbingLadder
            || @event is not InputEventKey { Pressed: true, Echo: false } key
            || !IsMovementKey(key))
        {
            return false;
        }

        RestoreMovementInput();
        return true;
    }

    private static bool IsMovementKey(InputEventKey key)
    {
        return IsMovementKeycode(key.PhysicalKeycode)
            || IsMovementKeycode(key.Keycode);
    }

    private static bool IsMovementKeycode(Key key)
    {
        return key is Key.W
            or Key.A
            or Key.S
            or Key.D
            or Key.Up
            or Key.Down
            or Key.Left
            or Key.Right;
    }

    public bool RearmMovementFromKeyForDiagnostics(Key key, bool uiLocked = false)
    {
        var previousUiLocked = UiLocked;
        UiLocked = uiLocked;
        DisarmMovementInput();
        TryRearmMovementInput(new InputEventKey
        {
            PhysicalKeycode = key,
            Pressed = true
        });
        var rearmed = _movementInputArmed;
        UiLocked = previousUiLocked;
        RestoreMovementInput();
        return rearmed;
    }

    private bool TryVaultLowObstacle(Vector3 movementDirection)
    {
        LastVaultResultForDiagnostics = "invalid_direction";
        movementDirection.Y = 0.0f;
        if (movementDirection.LengthSquared() < 0.01f)
        {
            movementDirection = -GlobalBasis.Z;
            movementDirection.Y = 0.0f;
        }
        if (movementDirection.LengthSquared() < 0.01f)
        {
            return false;
        }
        movementDirection = movementDirection.Normalized();

        var feet = GlobalPosition;
        var exclude = new Godot.Collections.Array<Rid> { GetRid() };
        var space = GetWorld3D().DirectSpaceState;
        var obstacleQuery = PhysicsRayQueryParameters3D.Create(
            feet + Vector3.Up * 0.38f,
            feet + Vector3.Up * 0.38f + movementDirection * LowObstacleVaultReach);
        obstacleQuery.CollisionMask = 1;
        obstacleQuery.CollideWithAreas = false;
        obstacleQuery.Exclude = exclude;
        var obstacleHit = space.IntersectRay(obstacleQuery);
        if (obstacleHit.Count == 0)
        {
            LastVaultResultForDiagnostics = "no_obstacle";
            return false;
        }

        var obstacle = obstacleHit["collider"].AsGodotObject();
        LastVaultResultForDiagnostics = $"obstacle:{(obstacle as Node)?.Name ?? obstacle?.GetType().Name ?? "unknown"}";
        var obstaclePosition = obstacleHit["position"].AsVector3();
        var obstacleDistance = new Vector2(
            obstaclePosition.X - feet.X,
            obstaclePosition.Z - feet.Z).Length();
        foreach (var inset in new[] { 0.08f, 0.2f, 0.34f })
        {
            var sampleDistance = Mathf.Clamp(
                obstacleDistance + inset,
                0.34f,
                LowObstacleVaultReach);
            var sample = feet + movementDirection * sampleDistance;
            var topQuery = PhysicsRayQueryParameters3D.Create(
                sample + Vector3.Up * (LowObstacleVaultMaxHeight + 0.32f),
                sample + Vector3.Up * 0.08f);
            topQuery.CollisionMask = 1;
            topQuery.CollideWithAreas = false;
            topQuery.Exclude = exclude;
            var topHit = space.IntersectRay(topQuery);
            if (topHit.Count == 0)
            {
                LastVaultResultForDiagnostics = $"no_top:{inset:0.00}";
                continue;
            }
            var topCollider = topHit["collider"].AsGodotObject();
            if (topCollider != obstacle)
            {
                LastVaultResultForDiagnostics = $"wrong_top:{(topCollider as Node)?.Name ?? topCollider?.GetType().Name ?? "unknown"}";
                continue;
            }
            var topNormal = topHit["normal"].AsVector3();
            if (topNormal.Dot(Vector3.Up) < 0.78f)
            {
                LastVaultResultForDiagnostics = $"steep_top:{topNormal.Dot(Vector3.Up):0.00}";
                continue;
            }

            var top = topHit["position"].AsVector3();
            var lift = top.Y - feet.Y;
            if (lift < LowObstacleVaultMinHeight || lift > LowObstacleVaultMaxHeight)
            {
                LastVaultResultForDiagnostics = $"height:{lift:0.00}";
                continue;
            }

            var targetFeet = top + Vector3.Up * 0.035f;
            var clearance = new PhysicsShapeQueryParameters3D
            {
                Shape = new CapsuleShape3D { Radius = 0.38f, Height = 1.75f },
                Transform = new Transform3D(Basis.Identity, targetFeet + Vector3.Up * 0.9f),
                CollisionMask = 1,
                CollideWithAreas = false,
                CollideWithBodies = true,
                Margin = 0.005f,
                Exclude = exclude
            };
            var overlaps = space.IntersectShape(clearance, 8);
            if (overlaps.Count > 0)
            {
                var blocker = overlaps[0]["collider"].AsGodotObject();
                LastVaultResultForDiagnostics = $"blocked:{(blocker as Node)?.Name ?? blocker?.GetType().Name ?? "unknown"}";
                continue;
            }

            GlobalPosition = targetFeet;
            _stairViewOffsetY = Mathf.Clamp(_stairViewOffsetY - lift, -0.55f, 0.0f);
            var velocity = Velocity;
            var vaultSpeed = Mathf.Max(2.2f, new Vector2(velocity.X, velocity.Z).Length());
            velocity.X = movementDirection.X * vaultSpeed;
            velocity.Y = -0.1f;
            velocity.Z = movementDirection.Z * vaultSpeed;
            Velocity = velocity;
            SuccessfulVaultsForDiagnostics++;
            LastVaultResultForDiagnostics = $"success:{lift:0.00}";
            return true;
        }
        return false;
    }
}
