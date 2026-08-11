using Godot;

namespace OperationSteelTide;

public partial class TacticalPlayer
{
    private const float LowObstacleVaultMinHeight = 0.3f;
    private const float LowObstacleVaultMaxHeight = 1.1f;
    private const float LowObstacleVaultReach = 0.95f;
    private const float LowObstacleVaultLandingClearance = 0.075f;
    private const float LowObstacleVaultApexClearance = 0.24f;
    private const float LowObstacleVaultRisePhase = 0.34f;
    private const float LowObstacleVaultCrossPhase = 0.76f;
    private const float LowObstacleVaultDurationMin = 0.54f;
    private const float LowObstacleVaultDurationMax = 0.78f;
    private const float LowObstacleVaultPathSafeMargin = 0.015f;

    private bool _isVaulting;
    private float _vaultElapsed;
    private float _vaultDuration;
    private Vector3 _vaultStartFeet;
    private Vector3 _vaultRiseFeet;
    private Vector3 _vaultCrossFeet;
    private Vector3 _vaultLandingFeet;
    private Vector3 _vaultDirection;
    private string _vaultPhase = "idle";
    private string _vaultPathBlocker = string.Empty;

    public int SuccessfulVaultsForDiagnostics { get; private set; }
    public float MaximumVaultHeightForDiagnostics => LowObstacleVaultMaxHeight;
    public string LastVaultResultForDiagnostics { get; private set; } = "not_attempted";
    public bool IsVaulting => _isVaulting;
    public float VaultProgress => !_isVaulting && _vaultPhase == "idle"
        ? 0.0f
        : Mathf.Clamp(_vaultElapsed / Mathf.Max(0.001f, _vaultDuration), 0.0f, 1.0f);
    public string VaultPhaseForDiagnostics => _vaultPhase;
    public string VaultPathBlockerForDiagnostics => _vaultPathBlocker;

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
        LastVaultResultForDiagnostics = "rejected:invalid_direction";
        _vaultPathBlocker = string.Empty;
        _vaultPhase = "rejected";
        if (_isVaulting)
        {
            LastVaultResultForDiagnostics = "rejected:already_vaulting";
            return false;
        }
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

        // A jump can change the stance immediately, but the capsule itself eases to
        // standing over several frames. Do not begin a vault with the shorter capsule.
        if (_collider.Shape is not CapsuleShape3D playerCapsule || playerCapsule.Height < 1.68f)
        {
            LastVaultResultForDiagnostics = "rejected:stance_transition";
            return false;
        }

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
            LastVaultResultForDiagnostics = "rejected:no_obstacle";
            return false;
        }

        var obstacle = obstacleHit["collider"].AsGodotObject();
        var obstacleShape = obstacleHit.ContainsKey("shape") ? obstacleHit["shape"].AsInt32() : -1;
        LastVaultResultForDiagnostics = $"candidate:{(obstacle as Node)?.Name ?? obstacle?.GetType().Name ?? "unknown"}";
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
                LastVaultResultForDiagnostics = $"rejected:no_top:{inset:0.00}";
                continue;
            }
            var topCollider = topHit["collider"].AsGodotObject();
            var topShape = topHit.ContainsKey("shape") ? topHit["shape"].AsInt32() : -1;
            if (topCollider != obstacle || (obstacleShape >= 0 && topShape >= 0 && topShape != obstacleShape))
            {
                LastVaultResultForDiagnostics = $"rejected:wrong_surface:{(topCollider as Node)?.Name ?? topCollider?.GetType().Name ?? "unknown"}";
                continue;
            }
            var topNormal = topHit["normal"].AsVector3();
            if (topNormal.Dot(Vector3.Up) < 0.78f)
            {
                LastVaultResultForDiagnostics = $"rejected:steep_top:{topNormal.Dot(Vector3.Up):0.00}";
                continue;
            }

            var top = topHit["position"].AsVector3();
            var lift = top.Y - feet.Y;
            if (lift < LowObstacleVaultMinHeight || lift > LowObstacleVaultMaxHeight)
            {
                LastVaultResultForDiagnostics = $"rejected:height:{lift:0.00}";
                continue;
            }

            var targetFeet = top + Vector3.Up * LowObstacleVaultLandingClearance;
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
                LastVaultResultForDiagnostics = $"rejected:landing_blocked:{(blocker as Node)?.Name ?? blocker?.GetType().Name ?? "unknown"}";
                continue;
            }

            var riseFeet = feet + Vector3.Up * (lift + LowObstacleVaultApexClearance);
            var crossFeet = targetFeet + Vector3.Up * LowObstacleVaultApexClearance;
            var path = new[] { feet, riseFeet, crossFeet, targetFeet };
            if (!ValidateVaultPath(path, out var pathBlocker))
            {
                _vaultPathBlocker = pathBlocker;
                LastVaultResultForDiagnostics = $"rejected:path_blocked:{pathBlocker}";
                continue;
            }

            _vaultStartFeet = feet;
            _vaultRiseFeet = riseFeet;
            _vaultCrossFeet = crossFeet;
            _vaultLandingFeet = targetFeet;
            _vaultDirection = movementDirection;
            _vaultDuration = Mathf.Clamp(
                0.5f + lift * 0.18f,
                LowObstacleVaultDurationMin,
                LowObstacleVaultDurationMax);
            _vaultElapsed = 0.0f;
            _vaultPhase = "rise";
            _vaultPathBlocker = string.Empty;
            _isVaulting = true;
            _isAiming = false;
            _knifeTime = 0.0f;
            if (_isReloading)
            {
                _isReloading = false;
                _reloadTime = 0.0f;
                ResetReloadRig();
            }
            DisarmFireInput();
            if (IsInstanceValid(_weaponLight))
            {
                _weaponLight.Visible = false;
            }
            Velocity = Vector3.Zero;
            HasMovementIntent = true;
            LastVaultResultForDiagnostics = $"started:{lift:0.00}";
            return true;
        }
        return false;
    }

    /// <summary>
    /// Sweeps the real player body through every straight segment of the authored
    /// vault arc without changing the live transform. This catches overhead beams,
    /// walls, and separate shapes on a shared static body before the animation starts.
    /// </summary>
    private bool ValidateVaultPath(Vector3[] points, out string blocker)
    {
        blocker = string.Empty;
        if (points.Length < 2)
        {
            blocker = "invalid_path";
            return false;
        }

        var probe = GlobalTransform;
        for (var index = 0; index < points.Length - 1; index++)
        {
            var motion = points[index + 1] - points[index];
            if (motion.LengthSquared() < 0.000001f)
            {
                continue;
            }
            probe.Origin = points[index];
            if (TestMove(
                    probe,
                    motion,
                    null,
                    LowObstacleVaultPathSafeMargin,
                    recoveryAsCollision: false,
                    maxCollisions: 4))
            {
                blocker = $"segment_{index + 1}";
                return false;
            }
        }
        return true;
    }

    private void UpdateVaultMovement(float delta)
    {
        if (!_isVaulting)
        {
            return;
        }

        var previousProgress = VaultProgress;
        _vaultElapsed = Mathf.Min(_vaultDuration, _vaultElapsed + Mathf.Max(0.0f, delta));
        var progress = VaultProgress;
        _vaultPhase = progress < LowObstacleVaultRisePhase
            ? "rise"
            : progress < LowObstacleVaultCrossPhase
                ? "cross"
                : "settle";

        // Split a frame at phase boundaries so a large frame cannot cut diagonally
        // through the corner of the swept path.
        var checkpoints = new[] { LowObstacleVaultRisePhase, LowObstacleVaultCrossPhase, progress };
        var fromProgress = previousProgress;
        foreach (var checkpoint in checkpoints)
        {
            var toProgress = Mathf.Min(progress, checkpoint);
            if (toProgress <= fromProgress + 0.0001f)
            {
                continue;
            }
            var target = EvaluateVaultPath(toProgress);
            var motion = target - GlobalPosition;
            var collision = motion.LengthSquared() > 0.000001f
                ? MoveAndCollide(
                    motion,
                    testOnly: false,
                    safeMargin: LowObstacleVaultPathSafeMargin,
                    recoveryAsCollision: false,
                    maxCollisions: 4)
                : null;
            if (collision is not null)
            {
                _vaultPathBlocker = DescribeVaultCollision(collision);
                CancelLowObstacleVault($"runtime_blocked:{_vaultPathBlocker}");
                return;
            }
            fromProgress = toProgress;
        }

        Velocity = Vector3.Zero;
        _stance = PlayerStance.Standing;
        if (_collider.Shape is CapsuleShape3D capsule)
        {
            capsule.Height = 1.75f;
            _collider.Position = new Vector3(0.0f, 0.875f, 0.0f);
        }
        _head.Position = new Vector3(0.0f, 1.57f, 0.0f);
        HasMovementIntent = true;
        if (progress >= 0.9999f)
        {
            CompleteLowObstacleVault();
        }
    }

    private Vector3 EvaluateVaultPath(float progress)
    {
        progress = Mathf.Clamp(progress, 0.0f, 1.0f);
        if (progress <= LowObstacleVaultRisePhase)
        {
            var t = Mathf.SmoothStep(0.0f, 1.0f, progress / LowObstacleVaultRisePhase);
            return _vaultStartFeet.Lerp(_vaultRiseFeet, t);
        }
        if (progress <= LowObstacleVaultCrossPhase)
        {
            var t = Mathf.SmoothStep(
                0.0f,
                1.0f,
                (progress - LowObstacleVaultRisePhase)
                    / (LowObstacleVaultCrossPhase - LowObstacleVaultRisePhase));
            return _vaultRiseFeet.Lerp(_vaultCrossFeet, t);
        }
        var settleT = Mathf.SmoothStep(
            0.0f,
            1.0f,
            (progress - LowObstacleVaultCrossPhase)
                / (1.0f - LowObstacleVaultCrossPhase));
        return _vaultCrossFeet.Lerp(_vaultLandingFeet, settleT);
    }

    private void UpdateVaultViewAnimation(float delta)
    {
        UpdateDamageKick(delta);
        _recoilPitch = Mathf.Lerp(_recoilPitch, 0.0f, SmoothFactor(14.0f, delta));
        _recoilSide = Mathf.Lerp(_recoilSide, 0.0f, SmoothFactor(14.0f, delta));
        _leanValue = Mathf.Lerp(_leanValue, 0.0f, SmoothFactor(12.0f, delta));
        _isAiming = false;

        var progress = VaultProgress;
        var lift = Mathf.Sin(progress * Mathf.Pi);
        var shoulderShift = Mathf.Sin(progress * Mathf.Pi * 2.0f);
        _head.Rotation = new Vector3(
            _pitch - lift * 0.035f + _damageKickPitch,
            0.0f,
            shoulderShift * 0.012f + _damageKickRoll);
        var cameraTarget = new Vector3(
            shoulderShift * 0.016f,
            -lift * 0.055f,
            lift * 0.026f) + _damageKickOffset;
        _camera.Position = _camera.Position.Lerp(cameraTarget, SmoothFactor(15.0f, delta));
        _camera.Fov = Mathf.Lerp(_camera.Fov, 75.0f, SmoothFactor(9.0f, delta));

        _opticReticle.Visible = false;
        _weaponLight.Visible = false;
        _weaponRoot.Visible = !_knifeEquipped && HasFireablePrimary;
        _knifeRoot.Visible = _knifeEquipped;
        if (_weaponRoot.Visible)
        {
            _weaponRoot.Position = _weaponRoot.Position.Lerp(
                new Vector3(0.5f, -0.72f, -0.42f),
                SmoothFactor(13.0f, delta));
            _weaponRoot.Rotation = _weaponRoot.Rotation.Lerp(
                new Vector3(0.48f, 0.08f, -0.3f),
                SmoothFactor(13.0f, delta));
        }
        if (_knifeRoot.Visible)
        {
            _knifeRoot.Position = _knifeRoot.Position.Lerp(
                new Vector3(0.48f, -0.7f, -0.4f),
                SmoothFactor(13.0f, delta));
            _knifeRoot.Rotation = _knifeRoot.Rotation.Lerp(
                new Vector3(0.56f, 0.08f, -0.32f),
                SmoothFactor(13.0f, delta));
        }
    }

    private void CompleteLowObstacleVault()
    {
        if (!_isVaulting)
        {
            return;
        }
        _isVaulting = false;
        _vaultPhase = "completed";
        _vaultElapsed = _vaultDuration;
        SuccessfulVaultsForDiagnostics++;
        LastVaultResultForDiagnostics = $"success:{_vaultLandingFeet.Y - _vaultStartFeet.Y:0.00}";
        var speed = Mathf.Max(2.2f, new Vector2(Velocity.X, Velocity.Z).Length());
        Velocity = new Vector3(_vaultDirection.X * speed, -0.1f, _vaultDirection.Z * speed);
        HasMovementIntent = false;
        DisarmFireInput();
        if (IsInstanceValid(_weaponLight))
        {
            _weaponLight.Visible = !_knifeEquipped && _flashlightOn;
        }
    }

    private void CancelLowObstacleVault(string reason)
    {
        if (!_isVaulting)
        {
            return;
        }
        _isVaulting = false;
        _vaultPhase = "cancelled";
        _vaultPathBlocker = reason;
        LastVaultResultForDiagnostics = $"cancelled:{reason}";
        Velocity = Vector3.Zero;
        HasMovementIntent = false;
        _isAiming = false;
        DisarmFireInput();
        if (IsInstanceValid(_weaponLight))
        {
            _weaponLight.Visible = !_knifeEquipped && _flashlightOn;
        }
    }

    public void CancelLowObstacleVaultForDiagnostics()
    {
        CancelLowObstacleVault("diagnostic_cancel");
    }

    private static string DescribeVaultCollision(KinematicCollision3D collision)
    {
        var collider = collision.GetCollider() as Node;
        return collider?.Name.ToString() ?? "collision";
    }
}
