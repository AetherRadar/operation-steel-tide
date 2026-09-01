using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private async void CaptureReloadPoseKeyframes(WeaponPlatform platform)
    {
        SetCaptureLanguage("en");
        var window = GetWindow();
        window.ContentScaleMode = Window.ContentScaleModeEnum.CanvasItems;
        window.ContentScaleAspect = Window.ContentScaleAspectEnum.Ignore;
        window.Size = new Vector2I(1280, 720);
        _player.GlobalPosition = new Vector3(0.0f, 0.2f, 40.0f);
        _player.Velocity = Vector3.Zero;
        _player.FaceWorldPointForDiagnostics(new Vector3(0.0f, 0.2f, -40.0f));
        var weapon = WeaponCatalog.Build(platform, 0);
        _player.GrantFireablePrimaryForDiagnostics(weapon);
        _player.SetAmmoGradeForDiagnostics(
            LootGrade.Common,
            weapon.Stats().MagazineSize * 2);
        _player.SetMagazineAmmoForDiagnostics(0);
        _player.SetAimingPoseForDiagnostics(false);
        await WaitFrames(6);

        var profile = FirstPersonReloadProfileCatalog.For(platform);
        var samples = new[]
        {
            (Name: "extract", Progress: (profile.ReachEnd + profile.ExtractEnd) * 0.5f),
            (Name: "insert", Progress: (profile.InsertEnd + profile.SeatEnd) * 0.5f),
            (Name: "action", Progress: (profile.SeatEnd + profile.ActionEnd) * 0.5f)
        };
        var valid = true;
        var repeatProgress = (profile.AcquireEnd + profile.InsertEnd) * 0.5f;
        valid &= _player.SetReloadPoseForDiagnostics(
            repeatProgress,
            emptyReload: true);
        var firstRepeat = _player.InspectAllWeaponReloadForDiagnostics();
        valid &= _player.SetReloadPoseForDiagnostics(
            repeatProgress,
            emptyReload: true);
        var secondRepeat = _player.InspectAllWeaponReloadForDiagnostics();
        var repeatValid = ReloadInspectionsEquivalent(firstRepeat, secondRepeat);
        valid &= repeatValid;
        GD.Print(
            $"RELOAD_KEYFRAME_REPEAT platform={platform} valid={repeatValid} "
            + ReloadInspectionDifferenceSummary(firstRepeat, secondRepeat));
        foreach (var sample in samples)
        {
            valid &= _player.SetReloadPoseForDiagnostics(
                sample.Progress,
                emptyReload: true);
            await WaitFrames(3);
            var inspection = _player.InspectAllWeaponReloadForDiagnostics();
            GD.Print(
                $"RELOAD_KEYFRAME platform={platform} stage={sample.Name} "
                + $"progress={sample.Progress:F4} "
                + $"palm={inspection.ScreenContact.LeftPalmScreen.X:F1}/"
                + $"{inspection.ScreenContact.LeftPalmScreen.Y:F1} "
                + $"wrist={inspection.ScreenContact.LeftWristScreen.X:F1}/"
                + $"{inspection.ScreenContact.LeftWristScreen.Y:F1} "
                + $"primary_grip={inspection.ScreenContact.PrimaryMagazineGripScreen.X:F1}/"
                + $"{inspection.ScreenContact.PrimaryMagazineGripScreen.Y:F1} "
                + $"spare_grip={inspection.ScreenContact.SpareMagazineGripScreen.X:F1}/"
                + $"{inspection.ScreenContact.SpareMagazineGripScreen.Y:F1} "
                + $"action_grip={inspection.ScreenContact.ActionGripScreen.X:F1}/"
                + $"{inspection.ScreenContact.ActionGripScreen.Y:F1} "
                + $"contact_residual={inspection.VisibleSupportPalm.DistanceTo(inspection.SupportTarget):F6}");
            SaveViewportImage(
                $"res://reload_keyframe_{platform.ToString().ToLowerInvariant()}_{sample.Name}.png");
        }
        _player.ClearReloadPoseForDiagnostics();
        GD.Print($"RELOAD_KEYFRAME_CAPTURE valid={valid} platform={platform}");
        GetTree().Quit(valid ? 0 : 2);
    }

    private async void CaptureReloadFrameSequence(
        WeaponPlatform platform,
        int maximumReloadFrames = 360)
    {
        // Keep capture length deterministic in uncapped/headless runs. At
        // 120 Hz the default 360-frame budget covers the longest 2.15 s clip
        // while also exercising interpolated frames between 60 Hz physics
        // samples.
        Engine.MaxFps = 120;
        DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
        SetCaptureLanguage("en");
        var window = GetWindow();
        window.ContentScaleMode = Window.ContentScaleModeEnum.CanvasItems;
        window.ContentScaleAspect = Window.ContentScaleAspectEnum.Ignore;
        window.Size = new Vector2I(960, 540);

        foreach (var enemy in _enemies)
        {
            if (IsInstanceValid(enemy))
            {
                enemy.ProcessMode = ProcessModeEnum.Disabled;
            }
        }
        foreach (var mate in _squadMates)
        {
            if (IsInstanceValid(mate))
            {
                mate.ProcessMode = ProcessModeEnum.Disabled;
                mate.GlobalPosition = new Vector3(240.0f, 80.0f, 240.0f);
            }
        }

        _player.GlobalPosition = new Vector3(0.0f, 0.2f, 40.0f);
        _player.Velocity = Vector3.Zero;
        _player.FaceWorldPointForDiagnostics(new Vector3(0.0f, 0.2f, -40.0f));
        var weapon = WeaponCatalog.Build(platform, 0);
        _player.GrantFireablePrimaryForDiagnostics(weapon);
        _player.SetAmmoGradeForDiagnostics(
            LootGrade.Common,
            weapon.Stats().MagazineSize * 2);
        _player.SetMagazineAmmoForDiagnostics(0);
        _player.SetAimingPoseForDiagnostics(false);
        _player.SetViewPitchForDiagnostics(0.0f);
        await WaitFrames(10);

        var profile = FirstPersonReloadProfileCatalog.For(platform);
        var sidearmCapture = WeaponCatalog.IsSidearm(platform);
        var checkpoints = new[]
        {
            0.0f,
            profile.ReachEnd,
            (profile.ReachEnd + profile.ExtractEnd) * 0.5f,
            profile.ExtractEnd,
            (profile.ExtractEnd + profile.StowEnd) * 0.5f,
            profile.StowEnd,
            (profile.StowEnd + profile.AcquireEnd) * 0.5f,
            profile.AcquireEnd,
            (profile.AcquireEnd + profile.InsertEnd) * 0.5f,
            profile.InsertEnd,
            (profile.InsertEnd + profile.SeatEnd) * 0.5f,
            profile.SeatEnd,
            (profile.SeatEnd + profile.ActionEnd) * 0.5f,
            profile.ActionEnd,
            1.0f
        };
        var pendingFrames = new List<(Image Image, string Path)>();
        await ToSignal(
            RenderingServer.Singleton,
            RenderingServer.SignalName.FramePostDraw);
        var readyInspection = _player.InspectAllWeaponReloadForDiagnostics();
        var readyContact = readyInspection.ScreenContact;
        var readyHandsValid = ReloadCaptureHandsValid(
            readyContact,
            sidearmCapture);
        var readyStartValid = !readyInspection.Reloading
            && !readyInspection.AnimatedRootActive
            && !readyInspection.AnimatedMeshActive
            && readyInspection.StaticArmsActive
            && readyInspection.WeaponActive
            && readyHandsValid;
        QueueReloadSequenceFrame(
            pendingFrames,
            $"res://reload_sequence_"
                + $"{platform.ToString().ToLowerInvariant()}_00_0.000.png");

        // Keep the synthetic key held until a physics tick has actually
        // consumed it. Releasing after an arbitrary two render frames made the
        // first captured sample depend on render cadence and could skip the
        // 0.0 checkpoint entirely.
        Input.ActionPress("reload");
        var startWaitFrames = 0;
        while (!_player.IsReloading && startWaitFrames < 240)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            startWaitFrames++;
        }
        Input.ActionRelease("reload");

        var frame = 0;
        var valid = _player.IsReloading && readyStartValid;
        var captureStartedTicks = Time.GetTicksMsec();
        // Checkpoint zero is the verified ready pose captured immediately
        // before the reload input. Runtime samples begin at ReachEnd and no
        // longer poison overshoot with an unavoidable first physics step.
        var checkpointIndex = 1;
        var maximumPalmScreenStepRatio = 0.0f;
        var maximumHandScreenStepRatio = 0.0f;
        var previousContact = readyContact;
        var previousContactAvailable = readyHandsValid;
        var clipPlaybackValid = true;
        var presentationMonotonic = true;
        var renderFramesAdvance = true;
        var repeatedMovingSamples = 0;
        var movingSamples = 0;
        var maximumPresentationProgressStep = 0.0f;
        var maximumCheckpointOvershoot = 0.0f;
        var previousPresentationProgress = -1.0f;
        var previousRenderFrame = 0UL;
        var firstPresentationProgress = float.PositiveInfinity;
        var runtimeLayerValidity = true;
        var runtimeHandAvailability = true;
        var runtimeHandProjectionValidity = true;
        var runtimeBodyContinuityValidity = true;
        var requiresPhysicalContactValidation = !WeaponCatalog.IsSidearm(platform)
            && platform != WeaponPlatform.M3A1;
        var physicalContactValidity = true;
        var physicalContactSamples = 0;
        var magazineContactSamples = 0;
        var actionContactSamples = 0;
        var maximumPhysicalPalmResidual = 0.0f;
        var maximumPhysicalTargetResidual = 0.0f;
        var lastReloadingInspection = default(AllWeaponReloadInspection);
        var lastReloadingInspectionAvailable = false;
        while (_player.IsReloading
            && frame < maximumReloadFrames)
        {
            // A movie capture must compare the arm pose, not incidental mouse
            // input or a late recoil/damage kick. Pin the diagnostic camera on
            // every rendered frame so a camera jump cannot masquerade as a
            // one-frame wrist twitch.
            _player.Velocity = Vector3.Zero;
            _player.FaceWorldPointForDiagnostics(
                new Vector3(0.0f, 0.2f, -40.0f));
            _player.SetViewPitchForDiagnostics(0.0f);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(
                RenderingServer.Singleton,
                RenderingServer.SignalName.FramePostDraw);
            if (!_player.IsReloading)
            {
                break;
            }
            var inspection = _player.InspectAllWeaponReloadForDiagnostics();
            var renderFrame = _player.InspectReloadRenderFrameForDiagnostics();
            var contact = inspection.ScreenContact;
            var meshBounds = inspection.BodyContinuity.AnimatedMeshScreen.Bounds;
            var handsAvailable = contact.RightPalmAvailable
                && contact.LeftPalmAvailable
                && (sidearmCapture
                    || (contact.RightWristAvailable
                        && contact.LeftWristAvailable));
            var handsValid = ReloadCaptureHandsValid(contact, sidearmCapture);
            var layerValid = inspection.AnimatedRootActive
                && inspection.AnimatedMeshActive
                && !inspection.StaticArmsActive;
            var bodyContinuous = ReloadBodyContinuityValid(inspection);
            runtimeLayerValidity &= layerValid;
            runtimeHandAvailability &= handsAvailable;
            runtimeHandProjectionValidity &= handsValid;
            runtimeBodyContinuityValidity &= bodyContinuous;
            if (handsValid && previousContactAvailable)
            {
                maximumPalmScreenStepRatio = Mathf.Max(
                    maximumPalmScreenStepRatio,
                    previousContact.LeftPalmScreen.DistanceTo(
                        contact.LeftPalmScreen)
                        / contact.ScreenSize.Y);
                maximumHandScreenStepRatio = Mathf.Max(
                    maximumHandScreenStepRatio,
                    ReloadCaptureMaximumHandScreenStep(
                        previousContact,
                        contact,
                        sidearmCapture));
            }
            previousContactAvailable = handsValid;
            previousContact = contact;
            if (frame == 0)
            {
                firstPresentationProgress = renderFrame.PresentationProgress;
            }
            if (previousPresentationProgress >= 0.0f)
            {
                var progressStep = renderFrame.PresentationProgress
                    - previousPresentationProgress;
                presentationMonotonic &= progressStep >= -0.000001f;
                maximumPresentationProgressStep = Mathf.Max(
                    maximumPresentationProgressStep,
                    progressStep);
                if (previousPresentationProgress > 0.0f
                    && previousPresentationProgress < 0.98f)
                {
                    movingSamples++;
                    if (progressStep <= 0.000001f)
                    {
                        repeatedMovingSamples++;
                    }
                }
                renderFramesAdvance &= renderFrame.RenderFrame
                    > previousRenderFrame;
            }
            previousPresentationProgress = renderFrame.PresentationProgress;
            previousRenderFrame = renderFrame.RenderFrame;
            lastReloadingInspection = inspection;
            lastReloadingInspectionAvailable = true;
            var expectedClip = profile.ClipName(emptyReload: true);
            clipPlaybackValid &= string.Equals(
                    _player.PresentedReloadClipForDiagnostics,
                    expectedClip,
                    System.StringComparison.Ordinal)
                && Mathf.Abs(
                    _player.PresentedReloadClipProgressForDiagnostics
                        - renderFrame.PresentationProgress) <= 0.001f;
            if (requiresPhysicalContactValidation
                && TryReloadCapturePhysicalTarget(
                    profile,
                    inspection,
                    renderFrame.PresentationProgress,
                    out var physicalTarget,
                    out var targetAvailable,
                    out var targetBehindCamera,
                    out var targetMeshAvailable,
                    out var actionTarget))
            {
                physicalContactSamples++;
                if (actionTarget)
                {
                    actionContactSamples++;
                }
                else
                {
                    magazineContactSamples++;
                }
                var physicalPalmResidual = inspection.VisibleSupportPalm
                    .DistanceTo(physicalTarget);
                // SupportTarget is the reach-clamped IK request. Comparing it
                // with the actual moving magazine/handle catches a scale error
                // that a palm-to-clamped-target residual alone cannot see.
                var physicalTargetResidual = inspection.SupportTarget
                    .DistanceTo(physicalTarget);
                maximumPhysicalPalmResidual = Mathf.Max(
                    maximumPhysicalPalmResidual,
                    physicalPalmResidual);
                maximumPhysicalTargetResidual = Mathf.Max(
                    maximumPhysicalTargetResidual,
                    physicalTargetResidual);
                physicalContactValidity &= targetAvailable
                    && !targetBehindCamera
                    && targetMeshAvailable
                    && physicalPalmResidual <= ReloadSupportPalmTargetLimit
                    && physicalTargetResidual <= ReloadSupportPalmTargetLimit;
            }
            // Save at most one image per rendered frame. A contact sequence
            // with several filenames pointing at one image can hide a jump.
            if (checkpointIndex < checkpoints.Length - 1
                && renderFrame.PresentationProgress + 0.0001f
                    >= checkpoints[checkpointIndex])
            {
                maximumCheckpointOvershoot = Mathf.Max(
                    maximumCheckpointOvershoot,
                    renderFrame.PresentationProgress
                        - checkpoints[checkpointIndex]);
                // GPU readback stalls the live render loop and turns this
                // cadence audit into a screenshot-performance benchmark.
                // Record checkpoint passage now, then render the review PNGs
                // from deterministic pinned poses after runtime completes.
                checkpointIndex++;
            }
            GD.Print(
                $"RELOAD_FRAME platform={platform} frame={frame:D3} "
                + $"logic_progress={_player.ReloadProgress:F6} "
                + $"presentation_progress={renderFrame.PresentationProgress:F6} "
                + $"clip_progress={_player.PresentedReloadClipProgressForDiagnostics:F6} "
                + $"palm={contact.LeftPalmScreen.X:F2}/{contact.LeftPalmScreen.Y:F2} "
                + $"wrist={contact.LeftWristScreen.X:F2}/{contact.LeftWristScreen.Y:F2} "
                + $"mesh={meshBounds.Position.X:F2}/{meshBounds.Position.Y:F2}/"
                + $"{meshBounds.Size.X:F2}/{meshBounds.Size.Y:F2} "
                + $"target_residual={inspection.VisibleSupportPalm.DistanceTo(inspection.SupportTarget):F6} "
                + $"layers={layerValid} hands={handsValid} "
                + $"body={bodyContinuous} "
                + $"primary={inspection.PrimaryMagazineVisible} "
                + $"spare={inspection.SpareMagazineVisible}");
            frame++;
        }

        var captureFinishedTicks = Time.GetTicksMsec();
        var captureElapsedSeconds = Mathf.Max(
            0.001f,
            (captureFinishedTicks - captureStartedTicks) / 1000.0f);
        var effectiveFps = frame / captureElapsedSeconds;
        var completedBeforeBudget = !_player.IsReloading
            && frame < maximumReloadFrames;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(
            RenderingServer.Singleton,
            RenderingServer.SignalName.FramePostDraw);
        var runtimeReset = _player.InspectAllWeaponReloadForDiagnostics();
        var transitionPalmStep = float.PositiveInfinity;
        var transitionShoulderStep = float.PositiveInfinity;
        var transitionWristScreenStep = float.PositiveInfinity;
        var runtimeLayerTransitionValid = false;
        if (lastReloadingInspectionAvailable
            && runtimeReset.ScreenContact.ScreenSize.Y > 0.0f
            && (sidearmCapture
                ? lastReloadingInspection.ScreenContact.LeftPalmAvailable
                    && lastReloadingInspection.ScreenContact.RightPalmAvailable
                    && runtimeReset.ScreenContact.LeftPalmAvailable
                    && runtimeReset.ScreenContact.RightPalmAvailable
                    && !lastReloadingInspection.ScreenContact.LeftPalmBehindCamera
                    && !lastReloadingInspection.ScreenContact.RightPalmBehindCamera
                    && !runtimeReset.ScreenContact.LeftPalmBehindCamera
                    && !runtimeReset.ScreenContact.RightPalmBehindCamera
                : lastReloadingInspection.ScreenContact.LeftWristAvailable
                    && lastReloadingInspection.ScreenContact.RightWristAvailable
                    && runtimeReset.ScreenContact.LeftWristAvailable
                    && runtimeReset.ScreenContact.RightWristAvailable
                    && !lastReloadingInspection.ScreenContact.LeftWristBehindCamera
                    && !lastReloadingInspection.ScreenContact.RightWristBehindCamera
                    && !runtimeReset.ScreenContact.LeftWristBehindCamera
                    && !runtimeReset.ScreenContact.RightWristBehindCamera))
        {
            transitionPalmStep = Mathf.Max(
                lastReloadingInspection.LeftPalm.DistanceTo(
                    runtimeReset.LeftPalm),
                lastReloadingInspection.RightPalm.DistanceTo(
                    runtimeReset.RightPalm));
            transitionShoulderStep = sidearmCapture
                ? 0.0f
                : Mathf.Max(
                    lastReloadingInspection.LeftShoulder.DistanceTo(
                        runtimeReset.LeftShoulder),
                    lastReloadingInspection.RightShoulder.DistanceTo(
                        runtimeReset.RightShoulder));
            var lastLeftScreen = sidearmCapture
                ? lastReloadingInspection.ScreenContact.LeftPalmScreen
                : lastReloadingInspection.ScreenContact.LeftWristScreen;
            var lastRightScreen = sidearmCapture
                ? lastReloadingInspection.ScreenContact.RightPalmScreen
                : lastReloadingInspection.ScreenContact.RightWristScreen;
            var resetLeftScreen = sidearmCapture
                ? runtimeReset.ScreenContact.LeftPalmScreen
                : runtimeReset.ScreenContact.LeftWristScreen;
            var resetRightScreen = sidearmCapture
                ? runtimeReset.ScreenContact.RightPalmScreen
                : runtimeReset.ScreenContact.RightWristScreen;
            transitionWristScreenStep = Mathf.Max(
                    lastLeftScreen.DistanceTo(resetLeftScreen),
                    lastRightScreen.DistanceTo(resetRightScreen))
                / runtimeReset.ScreenContact.ScreenSize.Y;
            runtimeLayerTransitionValid =
                lastReloadingInspection.AnimatedRootActive
                && lastReloadingInspection.AnimatedMeshActive
                && !lastReloadingInspection.StaticArmsActive
                && !runtimeReset.AnimatedRootActive
                && !runtimeReset.AnimatedMeshActive
                && runtimeReset.StaticArmsActive;
        }
        var runtimeResetTransitionValid = completedBeforeBudget
            && lastReloadingInspectionAvailable
            && runtimeLayerTransitionValid
            && transitionPalmStep <= (sidearmCapture ? 0.080f : 0.030f)
            && transitionShoulderStep <= 0.030f
            && transitionWristScreenStep <= (sidearmCapture ? 0.25f : 0.030f);
        QueueReloadSequenceFrame(
            pendingFrames,
            $"res://reload_sequence_"
                + $"{platform.ToString().ToLowerInvariant()}_runtime_idle_reset.png");

        if (completedBeforeBudget
            && checkpointIndex == checkpoints.Length - 1)
        {
            var endpointClipValid = true;
            for (var captureIndex = 1;
                 captureIndex < checkpoints.Length;
                 captureIndex++)
            {
                valid &= _player.SetReloadPoseForDiagnostics(
                    checkpoints[captureIndex],
                    emptyReload: true);
                await WaitFrames(2);
                if (captureIndex == checkpoints.Length - 1)
                {
                    endpointClipValid &= string.Equals(
                            _player.PresentedReloadClipForDiagnostics,
                            profile.ClipName(emptyReload: true),
                            System.StringComparison.Ordinal)
                        && Mathf.Abs(
                            _player.PresentedReloadClipProgressForDiagnostics
                                - 1.0f)
                            <= 0.001f;
                }
                QueueReloadSequenceFrame(
                    pendingFrames,
                    $"res://reload_sequence_"
                        + $"{platform.ToString().ToLowerInvariant()}_"
                        + $"{captureIndex:D2}_"
                        + $"{checkpoints[captureIndex]:F3}.png");
            }
            valid &= endpointClipValid;
            checkpointIndex++;
        }
        _player.ClearReloadPoseForDiagnostics();
        await WaitFrames(3);
        var reset = _player.InspectAllWeaponReloadForDiagnostics();
        var resetValid = !reset.Reloading
            && !reset.AnimatedRootActive
            && !reset.AnimatedMeshActive
            && reset.StaticArmsActive
            && reset.PrimaryMagazineVisible
            && !reset.SpareMagazineVisible;
        QueueReloadSequenceFrame(
            pendingFrames,
            $"res://reload_sequence_"
                + $"{platform.ToString().ToLowerInvariant()}_idle_reset.png");
        var savedFrames = SaveReloadSequenceFrames(pendingFrames);
        var expectedImages = checkpoints.Length + 2;

        // Viewport readback and the production terminal scene can run below
        // the requested 120 Hz on diagnostic hosts. The separate deterministic
        // reload-render-cadence audit proves 60/120/144 Hz interpolation; this
        // movie pass instead keeps bounded low-rate sampling so it can still
        // reject a near-plane hand explosion while producing review images.
        valid &= frame > 10
            && clipPlaybackValid
            && checkpointIndex == checkpoints.Length
            && completedBeforeBudget
            && presentationMonotonic
            && renderFramesAdvance
            && firstPresentationProgress >= 0.0f
            && firstPresentationProgress <= 0.08f
            && movingSamples > 10
            && repeatedMovingSamples == 0
            && maximumPresentationProgressStep <= 0.08f
            && maximumCheckpointOvershoot <= 0.10f
            && maximumPalmScreenStepRatio <= 0.25f
            && maximumHandScreenStepRatio <= 0.25f
            && runtimeLayerValidity
            && runtimeHandAvailability
            && runtimeHandProjectionValidity
            && runtimeBodyContinuityValidity
            && (!requiresPhysicalContactValidation
                || (physicalContactValidity
                    && physicalContactSamples >= 12
                    && magazineContactSamples >= 6
                    && actionContactSamples >= 3))
            && runtimeResetTransitionValid
            && resetValid
            && pendingFrames.Count == expectedImages
            && savedFrames == expectedImages;
        await WaitFrames(4);
        GD.Print(
            $"RELOAD_FRAME_CAPTURE valid={valid} platform={platform} "
            + $"frames={frame} saved={checkpointIndex}/{checkpoints.Length} "
            + $"clip_playback={clipPlaybackValid} "
            + $"completed={completedBeforeBudget} "
            + $"monotonic={presentationMonotonic} "
            + $"render_frames_advance={renderFramesAdvance} "
            + $"moving={movingSamples} repeated={repeatedMovingSamples} "
            + $"ticks={captureStartedTicks}/{captureFinishedTicks} "
            + $"elapsed={captureElapsedSeconds:F3} effective_fps={effectiveFps:F1} "
            + $"max_progress_step={maximumPresentationProgressStep:F6} "
            + $"max_checkpoint_overshoot={maximumCheckpointOvershoot:F6} "
            + $"max_palm_screen_step={maximumPalmScreenStepRatio:F6} "
            + $"max_hand_screen_step={maximumHandScreenStepRatio:F6} "
            + $"first_progress={firstPresentationProgress:F6} "
            + $"ready_start={readyStartValid} "
            + $"runtime_layers={runtimeLayerValidity} "
            + $"runtime_hand_availability={runtimeHandAvailability} "
            + $"runtime_hand_projection={runtimeHandProjectionValidity} "
            + $"runtime_body={runtimeBodyContinuityValidity} "
            + $"physical_contact={physicalContactValidity} "
            + $"physical_samples={physicalContactSamples}/"
            + $"{magazineContactSamples}/{actionContactSamples} "
            + $"physical_palm_residual={maximumPhysicalPalmResidual:F6} "
            + $"physical_target_residual={maximumPhysicalTargetResidual:F6} "
            + $"transition_palm={transitionPalmStep:F6} "
            + $"transition_shoulder={transitionShoulderStep:F6} "
            + $"transition_wrist_screen={transitionWristScreenStep:F6} "
            + $"transition_layers={runtimeLayerTransitionValid} "
            + $"reset={resetValid} images={savedFrames}/{expectedImages}");
        GetTree().Quit(valid ? 0 : 2);
    }

    private static bool ReloadCaptureHandsValid(
        ReloadScreenContactInspection contact,
        bool sidearm)
        => ReloadCaptureContactValid(
                contact.RightPalmAvailable,
                contact.RightPalmBehindCamera,
                contact.RightPalmScreen,
                contact.ScreenSize)
            && ReloadCaptureContactValid(
                contact.LeftPalmAvailable,
                contact.LeftPalmBehindCamera,
                contact.LeftPalmScreen,
                contact.ScreenSize)
            && (sidearm
                || ReloadCaptureContactValid(
                    contact.RightWristAvailable,
                    contact.RightWristBehindCamera,
                    contact.RightWristScreen,
                    contact.ScreenSize)
                    && ReloadCaptureContactValid(
                        contact.LeftWristAvailable,
                        contact.LeftWristBehindCamera,
                        contact.LeftWristScreen,
                        contact.ScreenSize));

    private static bool ReloadCaptureContactValid(
        bool available,
        bool behindCamera,
        Vector2 point,
        Vector2 screenSize)
    {
        if (!available
            || behindCamera
            || screenSize.X <= 0.0f
            || screenSize.Y <= 0.0f
            || !float.IsFinite(point.X)
            || !float.IsFinite(point.Y))
        {
            return false;
        }

        // A point extremely close to the near plane projects far outside the
        // viewport even when Camera3D does not classify it as behind. The
        // generous two-screen guard keeps ordinary cropped wrists legal while
        // rejecting that near-plane explosion deterministically.
        return point.X >= screenSize.X * -2.0f
            && point.X <= screenSize.X * 3.0f
            && point.Y >= screenSize.Y * -2.0f
            && point.Y <= screenSize.Y * 3.0f;
    }

    private static float ReloadCaptureMaximumHandScreenStep(
        ReloadScreenContactInspection previous,
        ReloadScreenContactInspection current,
        bool sidearm)
    {
        if (current.ScreenSize.Y <= 0.0f)
        {
            return float.PositiveInfinity;
        }

        var palmStep = Mathf.Max(
            previous.RightPalmScreen.DistanceTo(current.RightPalmScreen),
            previous.LeftPalmScreen.DistanceTo(current.LeftPalmScreen));
        if (sidearm)
        {
            return palmStep / current.ScreenSize.Y;
        }

        return Mathf.Max(
            palmStep,
            Mathf.Max(
                previous.RightWristScreen.DistanceTo(current.RightWristScreen),
                previous.LeftWristScreen.DistanceTo(current.LeftWristScreen)))
            / current.ScreenSize.Y;
    }

    private static bool TryReloadCapturePhysicalTarget(
        FirstPersonReloadProfile profile,
        AllWeaponReloadInspection inspection,
        float progress,
        out Vector3 target,
        out bool targetAvailable,
        out bool targetBehindCamera,
        out bool targetMeshAvailable,
        out bool actionTarget)
    {
        const float boundaryMargin = 0.005f;
        target = Vector3.Zero;
        targetAvailable = false;
        targetBehindCamera = true;
        targetMeshAvailable = false;
        actionTarget = false;

        if (profile.Mechanism == FirstPersonReloadMechanism.InternalMagazine)
        {
            if (progress <= profile.ReachEnd + boundaryMargin
                || progress >= profile.SeatEnd - boundaryMargin)
            {
                return false;
            }

            target = inspection.SpareMagazineGrip;
            targetAvailable = inspection.SpareMagazineGripAvailable;
            targetBehindCamera = inspection.ScreenContact
                .SpareMagazineGripBehindCamera;
            targetMeshAvailable = inspection.SpareMagazineGeometry
                && inspection.SpareMagazineVisible
                && inspection.ScreenContact.SpareMagazineScreen.Available;
            return true;
        }

        if (progress > profile.ReachEnd + boundaryMargin
            && progress < profile.StowEnd - boundaryMargin)
        {
            target = inspection.PrimaryMagazineGrip;
            targetAvailable = inspection.PrimaryMagazineGripAvailable;
            targetBehindCamera = inspection.ScreenContact
                .PrimaryMagazineGripBehindCamera;
            targetMeshAvailable = inspection.PrimaryMagazineGeometry
                && inspection.PrimaryMagazineVisible
                && inspection.ScreenContact.PrimaryMagazineScreen.Available;
            return true;
        }

        if (progress > profile.AcquireEnd + boundaryMargin
            && progress < profile.SeatEnd - boundaryMargin)
        {
            target = inspection.SpareMagazineGrip;
            targetAvailable = inspection.SpareMagazineGripAvailable;
            targetBehindCamera = inspection.ScreenContact
                .SpareMagazineGripBehindCamera;
            targetMeshAvailable = inspection.SpareMagazineGeometry
                && inspection.SpareMagazineVisible
                && inspection.ScreenContact.SpareMagazineScreen.Available;
            return true;
        }

        var actionSpan = profile.ActionEnd - profile.SeatEnd;
        var actionStartRatio = profile.Mechanism
                == FirstPersonReloadMechanism.HkSlapMagazine
            ? 0.34f
            : 0.40f;
        var actionEndRatio = profile.Mechanism
                == FirstPersonReloadMechanism.HkSlapMagazine
            ? 0.58f
            : 0.95f;
        if (!profile.UsesAction(emptyReload: true)
            || progress <= profile.SeatEnd + actionSpan * actionStartRatio
            || progress >= profile.SeatEnd + actionSpan * actionEndRatio)
        {
            return false;
        }

        actionTarget = true;
        target = inspection.ActionGrip;
        targetAvailable = inspection.ActionGripAvailable;
        targetBehindCamera = inspection.ScreenContact.ActionGripBehindCamera;
        targetMeshAvailable = inspection.ActionGeometry
            && inspection.ScreenContact.ActionScreen.Available;
        return true;
    }

    private void QueueReloadSequenceFrame(
        ICollection<(Image Image, string Path)> pendingFrames,
        string path)
    {
        if (DisplayServer.GetName() == "headless")
        {
            return;
        }
        var image = GetViewport().GetTexture().GetImage();
        if (image is not null && !image.IsEmpty())
        {
            pendingFrames.Add((image, path));
        }
    }

    private static int SaveReloadSequenceFrames(
        IEnumerable<(Image Image, string Path)> pendingFrames)
    {
        var saved = 0;
        foreach (var frame in pendingFrames)
        {
            if (frame.Image.SavePng(frame.Path) == Error.Ok)
            {
                saved++;
            }
        }
        return saved;
    }
}
