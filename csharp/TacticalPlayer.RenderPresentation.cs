using Godot;

namespace OperationSteelTide;

/// <summary>
/// Read-only timing projection for deterministic reload frame diagnostics.
/// Gameplay time remains owned by the physics timer; this projection describes
/// the interpolation used by the most recent rendered frame.
/// </summary>
public readonly record struct ReloadRenderFrameInspection(
    float PreviousPhysicsProgress,
    float CurrentPhysicsProgress,
    float PresentationProgress,
    float PhysicsInterpolationFraction,
    ulong RenderFrame,
    bool Reloading,
    bool DiagnosticPosePinned);

public readonly record struct ReloadRenderCadenceInspection(
    float RenderRate,
    int MovingSamples,
    int RepeatedMovingSamples,
    float MaximumProgressStep,
    bool Monotonic,
    bool Valid);

public readonly record struct FirstPersonCameraCadenceInspection(
    float RenderRate,
    int MovingSamples,
    int RepeatedMovingSamples,
    float MaximumPositionStep,
    float ExpectedPositionStep,
    float ViewHeightError,
    bool ImmediateLookRotation,
    bool Monotonic,
    bool Valid);

public readonly record struct FirstPersonCameraNodeInspection(
    float FirstPositionStep,
    float SecondPositionStep,
    float ViewHeightError,
    float ImmediateLookDot,
    bool TopLevel,
    bool Valid);

public partial class TacticalPlayer
{
    private const float FirstPersonTeleportResetDistance = 1.75f;
    // The node probe runs around the map's 40 m diagnostic spawn. Single-
    // precision global positions can differ by roughly 0.015 mm across two
    // mathematically equal interpolation steps there; keep the contract well
    // below a visible pixel while allowing that coordinate quantization.
    private const float FirstPersonCameraNodeStepTolerance = 0.00005f;
    private float _previousPhysicsReloadProgress;
    private float _currentPhysicsReloadProgress;
    private float _presentationReloadProgress;
    private float _lastReloadInterpolationFraction;
    private ulong _reloadRenderFrame;
    private bool _reloadPresentationPinnedForDiagnostics;
    private bool _reloadRigResetPending;
    private Transform3D _previousPhysicsBodyTransform = Transform3D.Identity;
    private Transform3D _currentPhysicsBodyTransform = Transform3D.Identity;
    private Transform3D _physicsAimTransform = Transform3D.Identity;
    private Vector3 _cameraLocalOffset;
    private Basis _cameraLocalBasis = Basis.Identity;
    private bool _firstPersonTransformClockInitialized;
    private Vector3 _previousPhysicsKnifePosition;
    private Vector3 _currentPhysicsKnifePosition;
    private Vector3 _previousPhysicsKnifeRotation;
    private Vector3 _currentPhysicsKnifeRotation;
    private ulong _knifePoseNodeInstanceId;

    /// <summary>
    /// Reload progress sampled for first-person presentation. Unlike
    /// <see cref="ReloadProgress"/>, this advances on every rendered frame by
    /// interpolating the two authoritative physics samples.
    /// </summary>
    internal float PresentationReloadProgress
        => _isReloading ? _presentationReloadProgress : 0.0f;

    public override void _Process(double delta)
    {
        if (!IsInstanceValid(_camera) || !IsInstanceValid(_weaponRoot))
        {
            return;
        }

        var renderDelta = Mathf.Min((float)delta, 0.1f);
        UpdateReloadRenderClock();
        FlushReloadRigReset();

        if (IsDead)
        {
            UpdateDownedRenderPresentation(renderDelta);
            UpdateHeldWeaponPresentation(renderDelta);
        }
        else if (IsInVehicle)
        {
            UpdateVehicleRenderPresentation(renderDelta);
        }
        else if (_isClimbingLadder || _isVaulting)
        {
            UpdateCameraAndWeapon(renderDelta);
        }
        else if (UiLocked)
        {
            UpdateHeldWeaponPresentation(renderDelta);
        }
        else
        {
            UpdateCameraAndWeapon(renderDelta);
        }

        PresentManualFirstPersonTransform();
    }

    private void UpdateDownedRenderPresentation(float delta)
    {
        var headPosition = _head.Position;
        headPosition.Y = Mathf.Lerp(
            headPosition.Y,
            0.42f,
            SmoothFactor(10.0f, delta));
        _head.Position = headPosition;
        _camera.Fov = Mathf.Lerp(
            _camera.Fov,
            68.0f,
            SmoothFactor(6.0f, delta));
    }

    private void UpdateVehicleRenderPresentation(float delta)
    {
        var headPosition = _head.Position;
        headPosition.Y = Mathf.Lerp(headPosition.Y, 0.52f, SmoothFactor(12.0f, delta));
        headPosition.Z = Mathf.Lerp(headPosition.Z, 0.05f, SmoothFactor(12.0f, delta));
        _head.Position = headPosition;
        _head.Rotation = new Vector3(_pitch, 0.0f, 0.0f);
        _weaponRoot.Visible = IsFirearmQuickSlotSelected;
        _knifeRoot.Visible = false;
        UpdateHeldThrowableVisual();
        UpdateHeldWeaponPresentation(delta);

        // Light cabin camera sway from vehicle speed (no full weapon bob).
        var sway = Mathf.Sin(Time.GetTicksMsec() * 0.008f) * 0.004f;
        _cameraLocalOffset = new Vector3(sway, 0.0f, 0.0f);
        _camera.Fov = Mathf.Lerp(_camera.Fov, 72.0f, SmoothFactor(8.0f, delta));
    }

    /// <summary>
    /// Initializes a private one-tick transform history. This branch does not
    /// depend on the project-wide interpolation setting and remains top-level so
    /// automatic parent interpolation can never blend it a second time.
    /// </summary>
    private void InitializeFirstPersonTransformClock()
    {
        _previousPhysicsBodyTransform = GlobalTransform;
        _currentPhysicsBodyTransform = GlobalTransform;
        _cameraLocalOffset = Vector3.Zero;
        _cameraLocalBasis = Basis.Identity;
        _firstPersonTransformClockInitialized = true;
        RefreshPhysicsAimTransform();
        ResetKnifePhysicsPoseClock();
        PresentManualFirstPersonTransform(1.0f);
    }

    private void BeginFirstPersonPhysicsStep()
    {
        if (!_firstPersonTransformClockInitialized)
        {
            InitializeFirstPersonTransformClock();
        }
        _previousPhysicsBodyTransform = _currentPhysicsBodyTransform;
        RefreshPhysicsAimTransform();
    }

    private void CompleteFirstPersonPhysicsStep(float delta)
    {
        var completedTransform = GlobalTransform;
        if (_currentPhysicsBodyTransform.Origin.DistanceTo(
                completedTransform.Origin) > FirstPersonTeleportResetDistance)
        {
            // External respawn/demolition code can assign GlobalPosition without
            // going through the vehicle/ladder helpers. Never interpolate that
            // discontinuity across the screen.
            _previousPhysicsBodyTransform = completedTransform;
            _currentPhysicsBodyTransform = completedTransform;
        }
        else
        {
            _currentPhysicsBodyTransform = completedTransform;
        }
        RefreshPhysicsAimTransform();
        UpdateKnifeAuthoritative(delta);
    }

    private void ResetFirstPersonTransformInterpolation()
    {
        _previousPhysicsBodyTransform = GlobalTransform;
        _currentPhysicsBodyTransform = GlobalTransform;
        _firstPersonTransformClockInitialized = true;
        RefreshPhysicsAimTransform();
    }

    private void PresentManualFirstPersonTransform(float? fractionOverride = null)
    {
        if (!_firstPersonTransformClockInitialized
            || !IsInstanceValid(_head)
            || !IsInstanceValid(_camera))
        {
            return;
        }

        var fraction = fractionOverride ?? Mathf.Clamp(
            (float)Engine.GetPhysicsInterpolationFraction(),
            0.0f,
            1.0f);
        var body = InterpolateFirstPersonBodyTransform(
            _previousPhysicsBodyTransform,
            _currentPhysicsBodyTransform,
            fraction);

        // Mouse yaw is intentionally not delayed by the one-tick position
        // interpolation. In a vehicle the yaw belongs to the moving seat, so its
        // sampled basis remains authoritative while pitch still updates instantly.
        body.Basis = IsInVehicle
            ? body.Basis.Orthonormalized()
            : GlobalTransform.Basis.Orthonormalized();
        var headLocal = _head.Transform;
        var cameraLocal = new Transform3D(_cameraLocalBasis, _cameraLocalOffset);
        _camera.GlobalTransform = body * headLocal * cameraLocal;
    }

    private void RefreshPhysicsAimTransform()
    {
        if (!IsInstanceValid(_head))
        {
            return;
        }

        var body = GlobalTransform;
        var viewRotation = IsInVehicle
            ? new Vector3(_pitch, 0.0f, 0.0f)
            : new Vector3(
                _pitch + _recoilPitch + _damageKickPitch,
                _recoilSide * 0.32f,
                _recoilSide * 0.24f + _leanValue * 0.13f + _damageKickRoll);
        var authoritativeEyePosition = IsInVehicle
            ? new Vector3(0.0f, 0.52f, 0.05f)
            : _head.Position;
        var headLocal = new Transform3D(
            Basis.FromEuler(viewRotation),
            authoritativeEyePosition);
        _physicsAimTransform = body
            * headLocal
            * new Transform3D(_cameraLocalBasis, Vector3.Zero);
    }

    /// <summary>
    /// Gameplay-space eye transform at the current authoritative body position.
    /// Physics queries, projectiles, melee sweeps, and interaction rays must use
    /// this instead of the render-only top-level Camera3D transform.
    /// </summary>
    internal Transform3D CaptureAuthoritativeViewTransform()
    {
        RefreshPhysicsAimTransform();
        return _physicsAimTransform;
    }

    private void ResetKnifePhysicsPoseClock()
    {
        if (!IsInstanceValid(_knifeRoot))
        {
            _knifePoseNodeInstanceId = 0;
            return;
        }
        _knifePoseNodeInstanceId = _knifeRoot.GetInstanceId();
        _previousPhysicsKnifePosition = _knifeRoot.Position;
        _currentPhysicsKnifePosition = _knifeRoot.Position;
        _previousPhysicsKnifeRotation = _knifeRoot.Rotation;
        _currentPhysicsKnifeRotation = _knifeRoot.Rotation;
    }

    private void UpdateKnifeAuthoritative(float delta)
    {
        if (IsDead
            || IsInVehicle
            || _isClimbingLadder
            || _isVaulting
            || UiLocked
            || !IsInstanceValid(_knifeRoot))
        {
            return;
        }
        if (_knifePoseNodeInstanceId != _knifeRoot.GetInstanceId())
        {
            ResetKnifePhysicsPoseClock();
        }

        // Restore the last physics pose before collision/sweep work. Render
        // interpolation must never feed a fractional pose back into gameplay.
        // The authored blade is a Camera child, so temporarily compose it from
        // the authoritative eye while resolving the sweep, then restore the
        // render camera before leaving the physics tick. The real camera never
        // remains snapped to a physics endpoint.
        var savedRenderCamera = _camera.GlobalTransform;
        try
        {
            _camera.GlobalTransform = _physicsAimTransform;
            _knifeRoot.Position = _currentPhysicsKnifePosition;
            _knifeRoot.Rotation = _currentPhysicsKnifeRotation;
            _previousPhysicsKnifePosition = _currentPhysicsKnifePosition;
            _previousPhysicsKnifeRotation = _currentPhysicsKnifeRotation;
            _knifeTime = Mathf.Max(0.0f, _knifeTime - delta);
            UpdateKnifeAnimation(delta);
            _currentPhysicsKnifePosition = _knifeRoot.Position;
            _currentPhysicsKnifeRotation = _knifeRoot.Rotation;
        }
        finally
        {
            _camera.GlobalTransform = savedRenderCamera;
        }
    }

    private void UpdateKnifeRenderPresentation()
    {
        if (!IsInstanceValid(_knifeRoot)
            || _knifePoseNodeInstanceId != _knifeRoot.GetInstanceId())
        {
            ResetKnifePhysicsPoseClock();
        }
        if (!IsInstanceValid(_knifeRoot))
        {
            return;
        }

        var fraction = Mathf.Clamp(
            (float)Engine.GetPhysicsInterpolationFraction(),
            0.0f,
            1.0f);
        _knifeRoot.Position = _previousPhysicsKnifePosition.Lerp(
            _currentPhysicsKnifePosition,
            fraction);
        _knifeRoot.Rotation = _previousPhysicsKnifeRotation.Lerp(
            _currentPhysicsKnifeRotation,
            fraction);
    }

    private static Transform3D InterpolateFirstPersonBodyTransform(
        Transform3D previous,
        Transform3D current,
        float fraction)
        => previous.InterpolateWith(current, Mathf.Clamp(fraction, 0.0f, 1.0f));

    internal bool UsesManualFirstPersonTransformForDiagnostics
        => IsInstanceValid(_camera)
            && _camera.TopLevel
            && _camera.PhysicsInterpolationMode == PhysicsInterpolationModeEnum.Off;

    internal Transform3D PhysicsAimTransformForDiagnostics
        => CaptureAuthoritativeViewTransform();

    /// <summary>
    /// Exercises the real top-level Camera3D with synthetic consecutive physics
    /// endpoints. This complements the cadence simulation by proving that the
    /// production presentation path actually writes distinct global positions.
    /// </summary>
    internal FirstPersonCameraNodeInspection
        InspectFirstPersonCameraNodeForDiagnostics()
    {
        if (!IsInstanceValid(_head) || !IsInstanceValid(_camera))
        {
            return default;
        }

        var savedPrevious = _previousPhysicsBodyTransform;
        var savedCurrent = _currentPhysicsBodyTransform;
        var savedInitialized = _firstPersonTransformClockInitialized;
        var savedHeadTransform = _head.Transform;
        var savedCameraOffset = _cameraLocalOffset;
        var savedCameraBasis = _cameraLocalBasis;
        var savedCameraGlobal = _camera.GlobalTransform;

        var start = GlobalTransform;
        var travel = start.Basis.X.Normalized() * 0.24f;
        var end = start;
        end.Origin += travel;
        _previousPhysicsBodyTransform = start;
        _currentPhysicsBodyTransform = end;
        _firstPersonTransformClockInitialized = true;
        _head.Position = Vector3.Up * 1.57f;
        _head.Rotation = Vector3.Zero;
        _cameraLocalOffset = Vector3.Zero;
        _cameraLocalBasis = Basis.Identity;

        PresentManualFirstPersonTransform(0.25f);
        var first = _camera.GlobalPosition;
        PresentManualFirstPersonTransform(0.5f);
        var second = _camera.GlobalPosition;
        PresentManualFirstPersonTransform(0.75f);
        var third = _camera.GlobalPosition;
        var firstStep = first.DistanceTo(second);
        var secondStep = second.DistanceTo(third);
        var bodyAtHalf = InterpolateFirstPersonBodyTransform(start, end, 0.5f);
        var expectedEyeAtHalf = bodyAtHalf.Origin
            + GlobalTransform.Basis.Orthonormalized() * (Vector3.Up * 1.57f);
        var viewHeightError = second.DistanceTo(expectedEyeAtHalf);

        _head.Rotation = new Vector3(0.31f, 0.0f, 0.0f);
        PresentManualFirstPersonTransform(0.5f);
        var expectedForward = -(
            GlobalTransform.Basis.Orthonormalized()
            * _head.Basis).Z.Normalized();
        var immediateLookDot = (-_camera.GlobalBasis.Z).Normalized()
            .Dot(expectedForward);
        var valid = _camera.TopLevel
            && firstStep > 0.0001f
            && secondStep > 0.0001f
            && Mathf.Abs(firstStep - secondStep)
                <= FirstPersonCameraNodeStepTolerance
            && viewHeightError <= 0.00001f
            && immediateLookDot >= 0.9999f;

        _previousPhysicsBodyTransform = savedPrevious;
        _currentPhysicsBodyTransform = savedCurrent;
        _firstPersonTransformClockInitialized = savedInitialized;
        _head.Transform = savedHeadTransform;
        _cameraLocalOffset = savedCameraOffset;
        _cameraLocalBasis = savedCameraBasis;
        _camera.GlobalTransform = savedCameraGlobal;
        RefreshPhysicsAimTransform();
        return new FirstPersonCameraNodeInspection(
            firstStep,
            secondStep,
            viewHeightError,
            immediateLookDot,
            _camera.TopLevel,
            valid);
    }

    internal static FirstPersonCameraCadenceInspection[]
        InspectStandardFirstPersonCameraCadencesForDiagnostics()
        => new[]
        {
            InspectFirstPersonCameraCadence(60.0f),
            InspectFirstPersonCameraCadence(120.0f),
            InspectFirstPersonCameraCadence(144.0f)
        };

    private static FirstPersonCameraCadenceInspection
        InspectFirstPersonCameraCadence(float renderRate)
    {
        const float physicsRate = 60.0f;
        const float speed = 6.0f;
        const float duration = 1.0f;
        const float viewHeight = 1.57f;
        var renderSamples = Mathf.CeilToInt(renderRate * duration);
        var previousCamera = new Vector3(0.0f, viewHeight, 0.0f);
        var movingSamples = 0;
        var repeatedMovingSamples = 0;
        var maximumStep = 0.0f;
        var monotonic = true;
        var viewHeightError = 0.0f;

        for (var sample = 1; sample <= renderSamples; sample++)
        {
            var renderTime = sample / renderRate;
            var physicsTime = renderTime * physicsRate;
            var completedTick = Mathf.FloorToInt(physicsTime + 0.000001f);
            var fraction = physicsTime - Mathf.Floor(physicsTime);
            var previousTick = Mathf.Max(0, completedTick - 1);
            var previous = new Transform3D(
                Basis.Identity,
                Vector3.Right * (previousTick * speed / physicsRate));
            var current = new Transform3D(
                Basis.Identity,
                Vector3.Right * (completedTick * speed / physicsRate));
            var body = completedTick == 0
                ? current
                : InterpolateFirstPersonBodyTransform(previous, current, fraction);
            var camera = body * new Transform3D(
                Basis.Identity,
                Vector3.Up * viewHeight);
            var step = camera.Origin.X - previousCamera.X;
            monotonic &= step >= -0.000001f;
            viewHeightError = Mathf.Max(
                viewHeightError,
                Mathf.Abs(camera.Origin.Y - body.Origin.Y - viewHeight));
            if (renderTime > 1.0f / physicsRate + 0.000001f)
            {
                movingSamples++;
                if (step <= 0.000001f)
                {
                    repeatedMovingSamples++;
                }
                maximumStep = Mathf.Max(maximumStep, step);
            }
            previousCamera = camera.Origin;
        }

        var expectedStep = speed / renderRate;
        var instantYaw = 0.47f;
        var instantBasis = Basis.FromEuler(new Vector3(0.0f, instantYaw, 0.0f));
        var immediateLookRotation = Mathf.Abs(
            instantBasis.GetEuler().Y - instantYaw) <= 0.0001f;
        var valid = movingSamples > 0
            && repeatedMovingSamples == 0
            && monotonic
            && maximumStep <= expectedStep + 0.00001f
            && viewHeightError <= 0.00001f
            && immediateLookRotation;
        return new FirstPersonCameraCadenceInspection(
            renderRate,
            movingSamples,
            repeatedMovingSamples,
            maximumStep,
            expectedStep,
            viewHeightError,
            immediateLookRotation,
            monotonic,
            valid);
    }

    private void BeginReloadPresentationClock()
    {
        // Keep ordinary viewmodel mutations on the render path. A previous
        // cancel can queue a reset in the same physics tick that starts this
        // reload; _Process flushes that reset immediately before presenting
        // progress zero. Only an unprocessed diagnostic node needs a synchronous
        // reset here.
        if (!IsProcessing())
        {
            FlushReloadRigReset();
        }
        _previousPhysicsReloadProgress = 0.0f;
        _currentPhysicsReloadProgress = 0.0f;
        _presentationReloadProgress = 0.0f;
        _lastReloadInterpolationFraction = 0.0f;
        _reloadPresentationPinnedForDiagnostics = false;
    }

    private void CaptureReloadPhysicsStep(float previousProgress)
    {
        _previousPhysicsReloadProgress = Mathf.Clamp(previousProgress, 0.0f, 1.0f);
        _currentPhysicsReloadProgress = Mathf.Clamp(ReloadProgress, 0.0f, 1.0f);
    }

    private void QueueReloadPresentationReset()
    {
        _reloadPresentationPinnedForDiagnostics = false;
        _previousPhysicsReloadProgress = 0.0f;
        _currentPhysicsReloadProgress = 0.0f;
        _presentationReloadProgress = 0.0f;
        _lastReloadInterpolationFraction = 0.0f;
        _reloadRigResetPending = true;
    }

    private void FlushReloadRigReset()
    {
        if (!_reloadRigResetPending)
        {
            return;
        }

        _reloadRigResetPending = false;
        var preserveActiveVariant = _isReloading;
        var activeReloadStartedEmpty = _reloadStartedEmpty;
        ResetReloadRig();
        if (preserveActiveVariant)
        {
            // A cancel and a new StartReload can occur in one physics tick.
            // Reset the old mechanism before drawing the new pose without
            // erasing whether the newly equipped weapon started empty.
            _reloadStartedEmpty = activeReloadStartedEmpty;
        }
    }

    private void UpdateReloadRenderClock()
    {
        _reloadRenderFrame++;
        if (!_isReloading)
        {
            _presentationReloadProgress = 0.0f;
            _lastReloadInterpolationFraction = 0.0f;
            return;
        }
        if (_reloadPresentationPinnedForDiagnostics)
        {
            return;
        }
        if (UiLocked && !IsInVehicle)
        {
            // Loot and backpack screens pause the on-foot physics path before
            // UpdateReloadTimer. Collapse both interpolation endpoints to the
            // paused authoritative value; otherwise Godot's fraction restarts
            // every physics tick and the hand rocks backward and forward
            // between two stale samples while the UI is open.
            var pausedProgress = Mathf.Clamp(ReloadProgress, 0.0f, 1.0f);
            _previousPhysicsReloadProgress = pausedProgress;
            _currentPhysicsReloadProgress = pausedProgress;
            _presentationReloadProgress = pausedProgress;
            _lastReloadInterpolationFraction = 1.0f;
            return;
        }

        _lastReloadInterpolationFraction = Mathf.Clamp(
            (float)Engine.GetPhysicsInterpolationFraction(),
            0.0f,
            1.0f);
        _presentationReloadProgress = InterpolateReloadProgress(
            _previousPhysicsReloadProgress,
            _currentPhysicsReloadProgress,
            _lastReloadInterpolationFraction);
    }

    private void PinReloadPresentationForDiagnostics(float progress)
    {
        var normalized = Mathf.Clamp(progress, 0.0f, 1.0f);
        _previousPhysicsReloadProgress = normalized;
        _currentPhysicsReloadProgress = normalized;
        _presentationReloadProgress = normalized;
        _lastReloadInterpolationFraction = 1.0f;
        _reloadPresentationPinnedForDiagnostics = true;
        _reloadRigResetPending = false;
    }

    private void AdvanceReloadPresentationForDiagnostics()
    {
        _reloadPresentationPinnedForDiagnostics = false;
        _presentationReloadProgress = _currentPhysicsReloadProgress;
        _lastReloadInterpolationFraction = 1.0f;
    }

    private static float InterpolateReloadProgress(
        float previousProgress,
        float currentProgress,
        float interpolationFraction)
        => Mathf.Lerp(
            Mathf.Clamp(previousProgress, 0.0f, 1.0f),
            Mathf.Clamp(currentProgress, 0.0f, 1.0f),
            Mathf.Clamp(interpolationFraction, 0.0f, 1.0f));

    internal static float InterpolateReloadProgressForDiagnostics(
        float previousProgress,
        float currentProgress,
        float interpolationFraction)
        => InterpolateReloadProgress(
            previousProgress,
            currentProgress,
            interpolationFraction);

    internal ReloadRenderFrameInspection InspectReloadRenderFrameForDiagnostics()
        => new(
            _previousPhysicsReloadProgress,
            _currentPhysicsReloadProgress,
            PresentationReloadProgress,
            _lastReloadInterpolationFraction,
            _reloadRenderFrame,
            _isReloading,
            _reloadPresentationPinnedForDiagnostics);

    /// <summary>
    /// Pure cadence contract used by diagnostics to prove that a 60 Hz
    /// authoritative timer produces a new linear presentation sample at 60,
    /// 120, and 144 Hz. The one physics-tick render delay is intentional and
    /// matches Godot's interpolation model.
    /// </summary>
    internal static ReloadRenderCadenceInspection[]
        InspectStandardReloadRenderCadencesForDiagnostics()
        => new[]
        {
            InspectReloadRenderCadence(60.0f),
            InspectReloadRenderCadence(120.0f),
            InspectReloadRenderCadence(144.0f)
        };

    private static ReloadRenderCadenceInspection InspectReloadRenderCadence(
        float renderRate)
    {
        const float physicsRate = 60.0f;
        const float duration = 1.0f;
        var physicsProgressStep = 1.0f / (physicsRate * duration);
        var renderSamples = Mathf.CeilToInt(renderRate * duration);
        var previous = 0.0f;
        var movingSamples = 0;
        var repeatedMovingSamples = 0;
        var maximumStep = 0.0f;
        var monotonic = true;

        for (var sample = 1; sample <= renderSamples; sample++)
        {
            var renderTime = sample / renderRate;
            var physicsTime = renderTime * physicsRate;
            var completedTick = Mathf.FloorToInt(physicsTime + 0.000001f);
            var interpolationFraction = physicsTime - Mathf.Floor(physicsTime);
            var previousPhysics = Mathf.Max(0, completedTick - 1)
                * physicsProgressStep;
            var currentPhysics = completedTick * physicsProgressStep;
            var presentation = completedTick == 0
                ? 0.0f
                : InterpolateReloadProgress(
                    previousPhysics,
                    currentPhysics,
                    interpolationFraction);
            var step = presentation - previous;
            monotonic &= step >= -0.000001f;
            if (renderTime > 1.0f / physicsRate + 0.000001f)
            {
                movingSamples++;
                if (step <= 0.000001f)
                {
                    repeatedMovingSamples++;
                }
                maximumStep = Mathf.Max(maximumStep, step);
            }
            previous = presentation;
        }

        var expectedMaximumStep = 1.0f / (renderRate * duration);
        var valid = monotonic
            && movingSamples > 0
            && repeatedMovingSamples == 0
            && maximumStep <= expectedMaximumStep + 0.00001f;
        return new ReloadRenderCadenceInspection(
            renderRate,
            movingSamples,
            repeatedMovingSamples,
            maximumStep,
            monotonic,
            valid);
    }
}
