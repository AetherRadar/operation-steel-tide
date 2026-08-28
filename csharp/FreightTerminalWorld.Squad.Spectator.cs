using System;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private const float SquadSpectatorPivotHeight = 1.45f;
    private const float SquadSpectatorFocusHeight = 1.28f;
    private const float SquadSpectatorFocusForward = 0.58f;
    private const float SquadSpectatorCameraHeight = 2.04f;
    private const float SquadSpectatorBackDistance = 2.75f;
    private const float SquadSpectatorShoulderOffset = 0.48f;
    private const float SquadSpectatorCameraProbeRadius = 0.18f;
    private const float SquadSpectatorWallClearance = 0.08f;
    private const float SquadSpectatorMinimumArmLength = 0.72f;
    private const float SquadSpectatorMinimumHorizontalClearance = 0.72f;
    private const float SquadSpectatorAboveModelHeight = 2.32f;

    private readonly SphereShape3D _squadSpectatorCameraProbe = new()
    {
        Radius = SquadSpectatorCameraProbeRadius
    };
    private readonly Godot.Collections.Array<Rid> _squadSpectatorCameraExclusions = new();
    private Camera3D? _squadSpectatorCamera;
    private SquadMate? _spectatedMate;
    private bool _squadSpectatorCameraCollisionAdjustedForDiagnostics;

    private void BeginSquadMateView()
    {
        _spectatedMate = FindLivingSpectatorTarget();
        if (_spectatedMate is null)
        {
            return;
        }

        ActivateSquadMateView();
    }

    private void UpdateSquadSpectatorCamera()
    {
        if (_squadSpectatorCamera is null || !IsInstanceValid(_squadSpectatorCamera))
        {
            BeginSquadMateView();
            return;
        }

        if (!IsLivingSpectatorTarget(_spectatedMate))
        {
            _spectatedMate = FindLivingSpectatorTarget();
            if (_spectatedMate is not null)
            {
                AnnounceSpectatedMate();
            }
        }
        if (_spectatedMate is null)
        {
            if (!_demolitionObjectiveSpectatorActive && ShouldObservePlantedDemolitionDevice())
            {
                BeginDemolitionObjectiveView();
            }
            return;
        }
        TryHandleSquadSpectatorCycleInput(
            Input.IsActionJustPressed(GameInputActions.Aim));

        SnapSquadSpectatorCamera();
        if (!_squadSpectatorCamera.Current)
        {
            _squadSpectatorCamera.MakeCurrent();
        }
    }

    private SquadMate? FindLivingSpectatorTarget()
    {
        return _squadMates
            .Where(IsLivingSpectatorTarget)
            .OrderBy(mate => mate.GlobalPosition.DistanceSquaredTo(_player.GlobalPosition))
            .FirstOrDefault();
    }

    private SquadMate[] LivingSpectatorTargetsBySlot()
    {
        return _squadMates
            .Where(IsLivingSpectatorTarget)
            .OrderBy(mate => mate.SquadSlot)
            .ToArray();
    }

    private static bool IsLivingSpectatorTarget(SquadMate? mate)
    {
        return mate is not null
            && GodotObject.IsInstanceValid(mate)
            && !mate.IsDowned
            && !mate.IsBodyBag;
    }

    private bool TryHandleSquadSpectatorCycleInput(bool aimJustPressed)
    {
        if (!aimJustPressed || (!_localPlayerDowned && !_localPlayerEliminated))
        {
            return false;
        }

        return CycleLivingSpectatorTarget();
    }

    private bool CycleLivingSpectatorTarget()
    {
        var livingTargets = LivingSpectatorTargetsBySlot();
        if (livingTargets.Length == 0)
        {
            _spectatedMate = null;
            return false;
        }

        var currentIndex = Array.FindIndex(
            livingTargets,
            candidate => ReferenceEquals(candidate, _spectatedMate));
        var nextIndex = currentIndex < 0 ? 0 : (currentIndex + 1) % livingTargets.Length;
        var nextMate = livingTargets[nextIndex];
        if (ReferenceEquals(nextMate, _spectatedMate))
        {
            return false;
        }

        _spectatedMate = nextMate;
        ActivateSquadMateView();
        return true;
    }

    private void ActivateSquadMateView()
    {
        if (!IsLivingSpectatorTarget(_spectatedMate))
        {
            return;
        }

        var spectatorCamera = EnsureSquadSpectatorCamera();
        SnapSquadSpectatorCamera();
        spectatorCamera.MakeCurrent();
        AnnounceSpectatedMate();
    }

    private void AnnounceSpectatedMate()
    {
        var mate = _spectatedMate;
        if (mate is null || !IsLivingSpectatorTarget(mate))
        {
            return;
        }

        _hud.ShowLocalizedFormattedMessage(
            "spectating_teammate_named",
            "SPECTATING  //  {0}",
            OperatorRoles.Spec(mate.Role).Accent,
            mate.Callsign);
    }

    private Camera3D EnsureSquadSpectatorCamera()
    {
        if (_squadSpectatorCamera is not null && IsInstanceValid(_squadSpectatorCamera))
        {
            return _squadSpectatorCamera;
        }
        _squadSpectatorCamera = new Camera3D
        {
            Name = "SquadSpectatorCamera",
            Fov = 76.0f,
            Near = 0.08f,
            PhysicsInterpolationMode = PhysicsInterpolationModeEnum.Off
        };
        AddChild(_squadSpectatorCamera);
        return _squadSpectatorCamera;
    }

    private void SnapSquadSpectatorCamera()
    {
        if (_squadSpectatorCamera is null || _spectatedMate is null
            || !IsInstanceValid(_squadSpectatorCamera) || !IsInstanceValid(_spectatedMate))
        {
            return;
        }

        var yawBasis = GetSquadSpectatorYawBasis(_spectatedMate);
        var forward = -yawBasis.Z;
        var right = yawBasis.X;
        var matePosition = _spectatedMate.GlobalPosition;
        var pivot = matePosition + Vector3.Up * SquadSpectatorPivotHeight;
        var primaryPosition = matePosition
            + Vector3.Up * SquadSpectatorCameraHeight
            + yawBasis.Z * SquadSpectatorBackDistance
            + right * SquadSpectatorShoulderOffset;

        var cameraPosition = ResolveSquadSpectatorCameraPosition(
            _spectatedMate,
            pivot,
            primaryPosition,
            out var primaryArmLength,
            out var primaryBlocked);
        _squadSpectatorCameraCollisionAdjustedForDiagnostics = primaryBlocked;

        if (primaryArmLength < SquadSpectatorMinimumArmLength
            || !IsSquadSpectatorCameraOutsideMate(_spectatedMate, cameraPosition))
        {
            cameraPosition = FindSafeSquadSpectatorFallback(
                _spectatedMate,
                pivot,
                yawBasis,
                out var fallbackBlocked);
            _squadSpectatorCameraCollisionAdjustedForDiagnostics |= fallbackBlocked;
        }

        _squadSpectatorCamera.GlobalPosition = cameraPosition;
        _squadSpectatorCamera.LookAt(
            matePosition
                + Vector3.Up * SquadSpectatorFocusHeight
                + forward * SquadSpectatorFocusForward,
            Vector3.Up);
    }

    private Vector3 FindSafeSquadSpectatorFallback(
        SquadMate mate,
        Vector3 pivot,
        Basis yawBasis,
        out bool collisionAdjusted)
    {
        var forward = -yawBasis.Z;
        var right = yawBasis.X;
        var matePosition = mate.GlobalPosition;
        var candidates = new[]
        {
            matePosition
                + Vector3.Up * (SquadSpectatorCameraHeight + 0.08f)
                + yawBasis.Z * 2.42f
                - right * 0.78f,
            matePosition
                + Vector3.Up * 2.54f
                + yawBasis.Z * 1.38f,
            matePosition
                + Vector3.Up * 1.9f
                + right * 1.72f
                + yawBasis.Z * 0.92f,
            matePosition
                + Vector3.Up * 1.9f
                - right * 1.72f
                + yawBasis.Z * 0.92f,
            matePosition
                + Vector3.Up * 1.82f
                + forward * 0.98f
                + right * 0.34f
        };

        var bestPosition = CameraPositionForEmergencyFallback(mate, yawBasis);
        var bestArmLength = 0.0f;
        collisionAdjusted = false;
        for (var candidateIndex = 0; candidateIndex < candidates.Length; candidateIndex++)
        {
            var candidate = candidates[candidateIndex];
            var resolved = ResolveSquadSpectatorCameraPosition(
                mate,
                pivot,
                candidate,
                out var armLength,
                out var blocked);
            if (!IsSquadSpectatorCameraOutsideMate(mate, resolved))
            {
                continue;
            }

            var preference = candidateIndex == 0 ? 0.12f : 0.0f;
            if (armLength + preference <= bestArmLength)
            {
                continue;
            }

            bestPosition = resolved;
            bestArmLength = armLength + preference;
            collisionAdjusted |= blocked;
        }

        if (bestArmLength <= 0.0f && IsSquadSpectatorCameraOutsideMate(mate, bestPosition))
        {
            var emergencyResolved = ResolveSquadSpectatorCameraPosition(
                mate,
                pivot,
                bestPosition,
                out _,
                out var emergencyBlocked);
            collisionAdjusted |= emergencyBlocked;
            if (IsSquadSpectatorCameraOutsideMate(mate, emergencyResolved))
            {
                bestPosition = emergencyResolved;
            }
        }

        return bestPosition;
    }

    private static Vector3 CameraPositionForEmergencyFallback(SquadMate mate, Basis yawBasis)
        => mate.GlobalPosition
            + Vector3.Up * SquadSpectatorAboveModelHeight
            + yawBasis.Z * 0.52f;

    private Vector3 ResolveSquadSpectatorCameraPosition(
        SquadMate mate,
        Vector3 pivot,
        Vector3 desired,
        out float armLength,
        out bool blocked)
    {
        var motion = desired - pivot;
        var desiredLength = motion.Length();
        if (desiredLength < 0.01f)
        {
            armLength = 0.0f;
            blocked = false;
            return pivot;
        }

        _squadSpectatorCameraExclusions.Clear();
        _squadSpectatorCameraExclusions.Add(mate.GetRid());
        using var query = new PhysicsShapeQueryParameters3D
        {
            Shape = _squadSpectatorCameraProbe,
            Transform = new Transform3D(Basis.Identity, pivot),
            Motion = motion,
            CollisionMask = 1,
            CollideWithAreas = false,
            CollideWithBodies = true,
            Margin = 0.0f,
            Exclude = _squadSpectatorCameraExclusions
        };
        var fractions = GetWorld3D().DirectSpaceState.CastMotion(query);
        var safeFraction = fractions.Length >= 1
            ? Mathf.Clamp(fractions[0], 0.0f, 1.0f)
            : 1.0f;
        blocked = safeFraction < 0.999f;
        if (blocked)
        {
            safeFraction = Mathf.Max(
                0.0f,
                safeFraction - SquadSpectatorWallClearance / desiredLength);
        }

        var resolved = pivot + motion * safeFraction;
        armLength = pivot.DistanceTo(resolved);
        return resolved;
    }

    private static Basis GetSquadSpectatorYawBasis(SquadMate mate)
    {
        var sourceBasis = mate.GlobalBasis.Orthonormalized();
        var forward = -sourceBasis.Z;
        forward.Y = 0.0f;
        if (forward.LengthSquared() < 0.0001f)
        {
            return Basis.Identity;
        }

        var yaw = Mathf.Atan2(-forward.X, -forward.Z);
        return new Basis(Vector3.Up, yaw);
    }

    private static bool IsSquadSpectatorCameraOutsideMate(SquadMate mate, Vector3 cameraPosition)
    {
        var relative = cameraPosition - mate.GlobalPosition;
        var horizontalDistance = new Vector2(relative.X, relative.Z).Length();
        return horizontalDistance >= SquadSpectatorMinimumHorizontalClearance
            || relative.Y >= SquadSpectatorAboveModelHeight
            || relative.Y <= -0.28f;
    }

    private void RestoreLocalPlayerView()
    {
        _spectatedMate = null;
        _demolitionObjectiveSpectatorActive = false;
        var playerCamera = _player.GetNodeOrNull<Camera3D>("Head/CombatCamera");
        playerCamera?.MakeCurrent();
    }

    private bool IsSquadMateViewCurrent =>
        _squadSpectatorCamera is not null
        && IsInstanceValid(_squadSpectatorCamera)
        && GetViewport().GetCamera3D() == _squadSpectatorCamera
        && IsLivingSpectatorTarget(_spectatedMate);

    private bool IsLocalPlayerViewCurrent
    {
        get
        {
            var playerCamera = _player.GetNodeOrNull<Camera3D>("Head/CombatCamera");
            return playerCamera is not null && GetViewport().GetCamera3D() == playerCamera;
        }
    }

    private StaticBody3D BuildSquadSpectatorWallForDiagnostics(SquadMate mate)
    {
        var yawBasis = GetSquadSpectatorYawBasis(mate);
        var wall = new StaticBody3D
        {
            Name = $"SquadSpectatorWallDiagnostic{Time.GetTicksUsec()}",
            CollisionLayer = 1,
            CollisionMask = 0
        };
        AddChild(wall);
        wall.GlobalBasis = yawBasis;
        wall.GlobalPosition = mate.GlobalPosition
            + yawBasis.Z * 1.45f
            + Vector3.Up * 1.55f;
        wall.AddChild(new CollisionShape3D
        {
            Name = "SpectatorWallCollision",
            Shape = new BoxShape3D { Size = new Vector3(4.0f, 3.0f, 0.24f) }
        });
        return wall;
    }

    private async void CaptureSquadSpectatorFrame()
    {
        SetCaptureLanguage("en");
        await ToSignal(GetTree().CreateTimer(0.65f), SceneTreeTimer.SignalName.Timeout);
        foreach (var enemy in _enemies)
        {
            if (IsInstanceValid(enemy))
            {
                enemy.ProcessMode = ProcessModeEnum.Disabled;
            }
        }

        var mate = _squadMates
            .Where(IsLivingSpectatorTarget)
            .OrderBy(candidate => candidate.SquadSlot)
            .FirstOrDefault();
        if (mate is null)
        {
            GD.PrintErr("CAPTURE_SQUAD_SPECTATOR missing_mate=True");
            GetTree().Quit(2);
            return;
        }

        var capturePosition = new Vector3(0.0f, 0.15f, 29.5f);
        mate.GlobalPosition = capturePosition;
        mate.GlobalRotation = Vector3.Zero;
        mate.Velocity = Vector3.Zero;
        mate.SetOrder(SquadOrder.Hold, capturePosition);
        mate.ProcessMode = ProcessModeEnum.Disabled;
        mate.SetAuthoredMovementPoseForDiagnostics(0.0f, aiming: true);
        foreach (var otherMate in _squadMates)
        {
            if (!ReferenceEquals(otherMate, mate) && IsInstanceValid(otherMate))
            {
                otherMate.GlobalPosition = capturePosition + new Vector3(12.0f, 0.0f, 12.0f);
                otherMate.ProcessMode = ProcessModeEnum.Disabled;
            }
        }

        _player.GlobalPosition = capturePosition + new Vector3(1.8f, 0.0f, 2.8f);
        _player.Velocity = Vector3.Zero;
        _player.SetHealthForDiagnostics(10.0f);
        _player.SetReviveUsedForDiagnostics(false);
        _player.TakeDamage(999.0f, _player.HitPoint(HitRegion.Torso), this);
        if (!_player.IsDead)
        {
            _player.TakeCombatDamage(999.0f, _player.HitPoint(HitRegion.Torso), this);
        }

        _spectatedMate = mate;
        var camera = EnsureSquadSpectatorCamera();
        SnapSquadSpectatorCamera();
        camera.MakeCurrent();
        await ToSignal(GetTree().CreateTimer(0.45f), SceneTreeTimer.SignalName.Timeout);
        var image = GetViewport().GetTexture().GetImage();
        image.SavePng("user://squad_spectator_validation.png");
        GD.Print($"CAPTURE_SQUAD_SPECTATOR user://squad_spectator_validation.png distance={camera.GlobalPosition.DistanceTo(mate.GlobalPosition):0.00} outside={IsSquadSpectatorCameraOutsideMate(mate, camera.GlobalPosition)}");
        GetTree().Quit();
    }
}
