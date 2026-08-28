using Godot;

namespace OperationSteelTide;

public partial class TacticalPlayer
{
    private const float LadderClimbSpeed = 2.85f;
    // Keep the capsule outside the facade while the hands reach the authored ladder line.
    private const float LadderWallOffset = 0.10f;
    private const float LadderMantleLift = 0.08f;
    private const float LadderDismountOffset = 0.28f;
    private const ulong LadderRemountCooldownMs = 280;

    private bool _isClimbingLadder;
    private bool _ladderCancelArmed;
    private ulong _ladderRemountBlockedUntil;
    private string _ladderPathBlocker = string.Empty;
    private float _ladderDistance;
    private float _ladderPathLength;
    private float _ladderBottomMountLength;
    private float _ladderVerticalLength;
    private float _ladderTopMountLength;
    private float _ladderAnimationTime;
    private float _ladderMotionAmount;
    private Vector3 _ladderBottomFeet;
    private Vector3 _ladderWallBottom;
    private Vector3 _ladderWallTop;
    private Vector3 _ladderTopFeet;
    private Vector3 _ladderOutward;

    private Node3D _ladderHandsRoot = null!;
    private Node3D _ladderLeftHand = null!;
    private Node3D _ladderRightHand = null!;
    private Node3D _ladderLeftForearm = null!;
    private Node3D _ladderRightForearm = null!;

    public bool IsClimbingLadder => _isClimbingLadder;
    public bool CanMountLadder => !_isClimbingLadder && Time.GetTicksMsec() >= _ladderRemountBlockedUntil;
    public bool HasActiveLadderCollisionForDiagnostics => CollisionLayer != 0 && !_collider.Disabled;
    public string LadderPathBlockerForDiagnostics => _ladderPathBlocker;
    public float LadderClimbProgress => _ladderPathLength <= 0.001f
        ? 0.0f
        : Mathf.Clamp(_ladderDistance / _ladderPathLength, 0.0f, 1.0f);

    private void BuildLadderViewModel()
    {
        _ladderHandsRoot = new Node3D
        {
            Name = "LadderHands",
            Position = new Vector3(0.0f, -0.12f, -0.62f),
            Visible = false
        };
        _camera.AddChild(_ladderHandsRoot);
        var glove = GloveFabric(new Color(0.105f, 0.125f, 0.105f));
        var armor = Material(new Color(0.025f, 0.032f, 0.029f), 0.18f, 0.76f);
        _ladderLeftHand = BuildTacticalHand(
            _ladderHandsRoot,
            true,
            new Vector3(-0.24f, -0.04f, -0.04f),
            new Vector3(-0.22f, -0.08f, -0.12f),
            glove,
            armor);
        _ladderRightHand = BuildTacticalHand(
            _ladderHandsRoot,
            false,
            new Vector3(0.24f, -0.25f, -0.04f),
            new Vector3(-0.22f, 0.08f, 0.12f),
            glove,
            armor);
        _ladderLeftForearm = BuildSleevedForearm(
            _ladderHandsRoot,
            new Vector3(-0.28f, -0.34f, 0.14f),
            new Vector3(-0.36f, -0.08f, -0.16f),
            glove,
            armor);
        _ladderRightForearm = BuildSleevedForearm(
            _ladderHandsRoot,
            new Vector3(0.28f, -0.55f, 0.14f),
            new Vector3(-0.36f, 0.08f, 0.16f),
            glove,
            armor);
    }

    public bool BeginLadderClimb(
        Vector3 bottomFeet,
        Vector3 topFeet,
        Vector3 outward,
        bool startAtTop = false)
    {
        if (!CanMountLadder || IsDead || IsInVehicle || UiLocked || RoleActionBlocksWeapon || MedicalActionBlocksWeapon)
        {
            return false;
        }
        if (!TryBuildLadderPath(
                bottomFeet,
                topFeet,
                outward,
                out var normalizedOutward,
                out var wallBottom,
                out var wallTop,
                out var bottomMountLength,
                out var verticalLength,
                out var topMountLength,
                out var pathLength)
            || !HasLadderTraversalClearance(
                bottomFeet,
                wallBottom,
                wallTop,
                topFeet,
                bottomMountLength,
                verticalLength,
                topMountLength,
                pathLength))
        {
            return false;
        }

        _ladderOutward = normalizedOutward;
        _ladderBottomFeet = bottomFeet;
        _ladderTopFeet = topFeet;
        _ladderWallBottom = wallBottom;
        _ladderWallTop = wallTop;
        _ladderBottomMountLength = bottomMountLength;
        _ladderVerticalLength = verticalLength;
        _ladderTopMountLength = topMountLength;
        _ladderPathLength = pathLength;
        _ladderDistance = startAtTop ? _ladderPathLength : 0.0f;
        _ladderAnimationTime = 0.0f;
        _ladderMotionAmount = 0.0f;
        _ladderCancelArmed = false;
        _isClimbingLadder = true;

        CloseMedicalWheelWithoutUse();
        CancelMedicalUse(false);
        if (_isPlating)
        {
            CancelPlate(notify: false);
        }
        CancelReload();
        _isAiming = false;
        _slideTime = 0.0f;
        _stance = PlayerStance.Standing;
        Velocity = Vector3.Zero;
        GlobalPosition = EvaluateLadderPath(_ladderDistance);
        FaceLadderWall();
        DisarmFireInput();
        DisarmMovementInput();
        SetLadderViewVisible(true);
        Hud?.ShowLocalizedMessage(
            "climb_started",
            "LADDER ENGAGED  //  W/S MOVE  SPACE/F DISMOUNT",
            new Color(1.0f, 0.68f, 0.26f));
        return true;
    }

    public void CancelLadderClimb(bool notify = true)
    {
        if (!_isClimbingLadder)
        {
            return;
        }
        var verticalStart = _ladderBottomMountLength;
        var verticalEnd = verticalStart + _ladderVerticalLength;
        if (_ladderDistance <= verticalStart + 0.08f)
        {
            FinishLadderClimb(exitAtTop: false, notify);
        }
        else if (_ladderDistance >= verticalEnd - 0.08f)
        {
            FinishLadderClimb(exitAtTop: true, notify);
        }
        else
        {
            FinishLadderClimbAt(
                EvaluateLadderPath(_ladderDistance) + _ladderOutward * LadderDismountOffset,
                exitAtTop: false,
                notify);
        }
    }

    public void AdvanceLadderClimbForDiagnostics(float signedDistance)
    {
        if (!_isClimbingLadder)
        {
            return;
        }
        AdvanceLadderDistance(signedDistance, notify: false);
    }

    private void UpdateLadderClimb(float delta)
    {
        Velocity = Vector3.Zero;
        if (!_ladderCancelArmed)
        {
            _ladderCancelArmed = !Input.IsActionPressed(GameInputActions.Interact);
        }
        else if (Input.IsActionJustPressed(GameInputActions.Interact))
        {
            CancelLadderClimb();
            return;
        }
        if (Input.IsActionJustPressed(GameInputActions.Jump))
        {
            CancelLadderClimb();
            return;
        }

        var move = Input.GetActionStrength(GameInputActions.MoveForward)
            - Input.GetActionStrength(GameInputActions.MoveBackward);
        _ladderMotionAmount = Mathf.Lerp(
            _ladderMotionAmount,
            Mathf.Abs(move),
            1.0f - Mathf.Exp(-delta * 12.0f));
        if (Mathf.Abs(move) > 0.05f)
        {
            AdvanceLadderDistance(move * LadderClimbSpeed * delta, notify: true);
            if (!_isClimbingLadder)
            {
                return;
            }
        }
        GlobalPosition = EvaluateLadderPath(_ladderDistance);
        FaceLadderWall();
        HasMovementIntent = Mathf.Abs(move) > 0.05f;
        _isAiming = false;
        Hud?.SetAiming(false);

        var headPosition = _head.Position;
        headPosition.Y = Mathf.Lerp(headPosition.Y, 1.57f, 1.0f - Mathf.Exp(-delta * 12.0f));
        _head.Position = headPosition;
        if (_collider.Shape is CapsuleShape3D capsule)
        {
            capsule.Height = Mathf.Lerp(capsule.Height, 1.75f, 1.0f - Mathf.Exp(-delta * 12.0f));
            var colliderPosition = _collider.Position;
            colliderPosition.Y = Mathf.Lerp(colliderPosition.Y, 0.875f, 1.0f - Mathf.Exp(-delta * 12.0f));
            _collider.Position = colliderPosition;
        }
    }

    private void AdvanceLadderDistance(float signedDistance, bool notify)
    {
        _ladderDistance = Mathf.Clamp(_ladderDistance + signedDistance, 0.0f, _ladderPathLength);
        GlobalPosition = EvaluateLadderPath(_ladderDistance);
        if (signedDistance > 0.0f && _ladderDistance >= _ladderPathLength - 0.001f)
        {
            FinishLadderClimb(exitAtTop: true, notify);
        }
        else if (signedDistance < 0.0f && _ladderDistance <= 0.001f)
        {
            FinishLadderClimb(exitAtTop: false, notify);
        }
    }

    private Vector3 EvaluateLadderPath(float distance)
        => EvaluateLadderPath(
            distance,
            _ladderBottomFeet,
            _ladderWallBottom,
            _ladderWallTop,
            _ladderTopFeet,
            _ladderBottomMountLength,
            _ladderVerticalLength,
            _ladderTopMountLength,
            _ladderPathLength);

    private static Vector3 EvaluateLadderPath(
        float distance,
        Vector3 bottomFeet,
        Vector3 wallBottom,
        Vector3 wallTop,
        Vector3 topFeet,
        float bottomMountLength,
        float verticalLength,
        float topMountLength,
        float pathLength)
    {
        distance = Mathf.Clamp(distance, 0.0f, pathLength);
        if (distance <= bottomMountLength)
        {
            var t = bottomMountLength <= 0.001f ? 1.0f : distance / bottomMountLength;
            return bottomFeet.Lerp(wallBottom, Mathf.SmoothStep(0.0f, 1.0f, t));
        }
        distance -= bottomMountLength;
        if (distance <= verticalLength)
        {
            var t = verticalLength <= 0.001f ? 1.0f : distance / verticalLength;
            return wallBottom.Lerp(wallTop, t);
        }
        distance -= verticalLength;
        var mantle = topMountLength <= 0.001f ? 1.0f : distance / topMountLength;
        mantle = Mathf.SmoothStep(0.0f, 1.0f, mantle);
        var position = wallTop.Lerp(topFeet, mantle);
        position.Y += Mathf.Sin(mantle * Mathf.Pi) * 0.22f;
        return position;
    }

    public bool CanTraverseLadderPath(Vector3 bottomFeet, Vector3 topFeet, Vector3 outward)
    {
        return TryBuildLadderPath(
                bottomFeet,
                topFeet,
                outward,
                out _,
                out var wallBottom,
                out var wallTop,
                out var bottomMountLength,
                out var verticalLength,
                out var topMountLength,
                out var pathLength)
            && HasLadderTraversalClearance(
                bottomFeet,
                wallBottom,
                wallTop,
                topFeet,
                bottomMountLength,
                verticalLength,
                topMountLength,
                pathLength);
    }

    private static bool TryBuildLadderPath(
        Vector3 bottomFeet,
        Vector3 topFeet,
        Vector3 outward,
        out Vector3 normalizedOutward,
        out Vector3 wallBottom,
        out Vector3 wallTop,
        out float bottomMountLength,
        out float verticalLength,
        out float topMountLength,
        out float pathLength)
    {
        outward.Y = 0.0f;
        normalizedOutward = outward.LengthSquared() < 0.25f ? Vector3.Zero : outward.Normalized();
        wallBottom = bottomFeet - normalizedOutward * LadderWallOffset;
        wallTop = new Vector3(
            wallBottom.X,
            Mathf.Max(bottomFeet.Y + 0.8f, topFeet.Y + LadderMantleLift),
            wallBottom.Z);
        bottomMountLength = bottomFeet.DistanceTo(wallBottom);
        verticalLength = wallBottom.DistanceTo(wallTop);
        topMountLength = wallTop.DistanceTo(topFeet);
        pathLength = bottomMountLength + verticalLength + topMountLength;
        return normalizedOutward != Vector3.Zero && topFeet.Y - bottomFeet.Y >= 1.2f;
    }

    private bool HasLadderTraversalClearance(
        Vector3 bottomFeet,
        Vector3 wallBottom,
        Vector3 wallTop,
        Vector3 topFeet,
        float bottomMountLength,
        float verticalLength,
        float topMountLength,
        float pathLength)
    {
        _ladderPathBlocker = string.Empty;
        if (_collider.Shape is not CapsuleShape3D playerCapsule || pathLength <= 0.001f)
        {
            return true;
        }

        var clearanceHeight = Mathf.Max(1.4f, playerCapsule.Height - 0.1f);
        using var clearance = new CapsuleShape3D
        {
            Radius = Mathf.Max(0.26f, playerCapsule.Radius - 0.06f),
            Height = clearanceHeight
        };
        var exclude = new Godot.Collections.Array<Rid> { GetRid() };
        using var excludeBacking = exclude.AsDisposable();
        using var query = new PhysicsShapeQueryParameters3D
        {
            Shape = clearance,
            CollisionMask = 1,
            CollideWithAreas = false,
            CollideWithBodies = true,
            Margin = 0.005f,
            Exclude = exclude
        };
        var sampleCount = Mathf.Max(4, Mathf.CeilToInt(pathLength / 0.38f));
        for (var sample = 0; sample <= sampleCount; sample++)
        {
            var distance = pathLength * sample / sampleCount;
            var feet = EvaluateLadderPath(
                distance,
                bottomFeet,
                wallBottom,
                wallTop,
                topFeet,
                bottomMountLength,
                verticalLength,
                topMountLength,
                pathLength);
            query.Transform = new Transform3D(
                Basis.Identity,
                feet + Vector3.Up * (clearanceHeight * 0.5f + 0.045f));
            var hits = GetWorld3D().DirectSpaceState.IntersectShape(query, 16);
            using var hitsBacking = hits.AsDisposable();
            for (var index = 0; index < hits.Count; index++)
            {
                using var hit = hits[index];
                using var colliderValue = hit[GodotPhysicsResultKeys.Collider];
                var collider = colliderValue.AsGodotObject() as Node;
                if (collider?.IsInGroup("roof_access_ladder_geometry") == true)
                {
                    continue;
                }
                _ladderPathBlocker = $"{collider?.Name ?? "unknown"}@{distance:0.00}";
                return false;
            }
        }
        return true;
    }

    private void FaceLadderWall()
    {
        var towardWall = -_ladderOutward;
        Rotation = new Vector3(0.0f, Mathf.Atan2(-towardWall.X, -towardWall.Z), 0.0f);
        _pitch = Mathf.Clamp(_pitch, -0.55f, 0.42f);
    }

    private void FinishLadderClimb(bool exitAtTop, bool notify)
    {
        var exit = exitAtTop ? _ladderTopFeet : _ladderBottomFeet;
        FinishLadderClimbAt(exit, exitAtTop, notify);
    }

    private void FinishLadderClimbAt(Vector3 exit, bool exitAtTop, bool notify)
    {
        _isClimbingLadder = false;
        _ladderDistance = exitAtTop ? _ladderPathLength : 0.0f;
        _ladderRemountBlockedUntil = Time.GetTicksMsec() + LadderRemountCooldownMs;
        GlobalPosition = exit;
        Velocity = Vector3.Zero;
        HasMovementIntent = false;
        SetLadderViewVisible(false);
        DisarmFireInput();
        DisarmMovementInput();
        if (notify && !IsDead)
        {
            Hud?.ShowLocalizedMessage(
                exitAtTop ? "climb_roof_reached" : "climb_dismounted",
                exitAtTop ? "ROOF ACCESS REACHED" : "LADDER DISMOUNTED",
                new Color(0.52f, 0.92f, 0.7f));
        }
    }

    private void SetLadderViewVisible(bool active)
    {
        if (IsInstanceValid(_weaponRoot))
        {
            _weaponRoot.Visible = !active && HasActiveFirearm;
        }
        if (IsInstanceValid(_knifeRoot))
        {
            _knifeRoot.Visible = !active && _knifeEquipped;
        }
        if (IsInstanceValid(_ladderHandsRoot))
        {
            _ladderHandsRoot.Visible = active;
        }
        if (IsInstanceValid(_weaponLight))
        {
            _weaponLight.Visible = !active && IsFirearmQuickSlotSelected && _flashlightOn;
        }
    }

    private void UpdateLadderViewAnimation(float delta)
    {
        _ladderAnimationTime += delta * Mathf.Lerp(1.4f, 6.2f, _ladderMotionAmount);
        var cycle = Mathf.Sin(_ladderAnimationTime);
        var reach = cycle * 0.15f * Mathf.Lerp(0.25f, 1.0f, _ladderMotionAmount);
        _head.Rotation = new Vector3(_pitch, 0.0f, cycle * 0.006f * _ladderMotionAmount);
        _camera.Position = new Vector3(
            cycle * 0.012f * _ladderMotionAmount,
            Mathf.Abs(cycle) * 0.014f * _ladderMotionAmount,
            0.0f);
        _camera.Fov = Mathf.Lerp(_camera.Fov, 74.0f, 1.0f - Mathf.Exp(-delta * 8.0f));
        _ladderHandsRoot.Position = _ladderHandsRoot.Position.Lerp(
            new Vector3(0.0f, -0.12f, -0.62f),
            1.0f - Mathf.Exp(-delta * 10.0f));
        _ladderLeftHand.Position = new Vector3(-0.24f, -0.04f + reach, -0.04f - Mathf.Abs(reach) * 0.3f);
        _ladderRightHand.Position = new Vector3(0.24f, -0.25f - reach, -0.04f - Mathf.Abs(reach) * 0.3f);
        _ladderLeftForearm.Position = new Vector3(-0.28f, -0.34f + reach, 0.14f);
        _ladderRightForearm.Position = new Vector3(0.28f, -0.55f - reach, 0.14f);
        _weaponRoot.Visible = false;
        _knifeRoot.Visible = false;
        _ladderHandsRoot.Visible = true;
        _opticReticle.Visible = false;
    }
}
