using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private const float SidearmReloadMaximumBoneStepRadians = 0.08f;
    private const float SidearmReloadMaximumJointStepMeters = 0.04f;
    private const float SidearmReloadMaximumPalmStepMeters = 0.03f;
    private const float SidearmReloadMaximumPalmScreenStepRatio = 0.03f;
    private const float SidearmReloadMaximumPalmTransitionRatio = 0.05f;
    private const float SidearmReloadMaximumEndpointBasisErrorRadians = 0.035f;
    // The approved sidearm presentation is intentionally compact: the hand
    // only has to clear and reseat the magazine while the pistol stays close
    // to camera.  Require visible motion without forcing a long forearm sweep.
    private const float SidearmReloadMinimumExchangeArmMotion = 0.012f;
    private const float SidearmReloadMinimumActionArmMotion = 0.008f;
    private const float SidearmReloadMinimumViewMotion = 0.008f;
    private const float SidearmReloadMaximumArmMotion = 0.18f;
    private const float SidearmReloadMaximumViewMotion = 0.20f;
    private const int SidearmReloadMinimumContinuityFrames = 100;
    private const int SidearmReloadMaximumContinuityFrames = 180;

    private async void ValidateSidearmReloadDiagnostics()
    {
        var window = GetWindow();
        window.ContentScaleMode = Window.ContentScaleModeEnum.CanvasItems;
        window.ContentScaleAspect = Window.ContentScaleAspectEnum.Ignore;
        window.Size = new Vector2I(1280, 720);
        _player.GlobalPosition = new Vector3(0.0f, 0.2f, 40.0f);
        _player.Velocity = Vector3.Zero;
        _player.FaceWorldPointForDiagnostics(new Vector3(0.0f, 0.2f, -40.0f));
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
                mate.GlobalPosition = new Vector3(240.0f, 80.0f, 240.0f);
            }
        }
        await WaitFrames(6);

        var reloadKeyBound = ReloadActionUsesPhysicalR();
        var valid = reloadKeyBound;
        var results = new List<string>();
        var platforms = new[]
        {
            WeaponPlatform.P226,
            WeaponPlatform.M1911,
            WeaponPlatform.GSh18,
            WeaponPlatform.DesertEagle
        };

        // Exercise the real action-to-StartReload path once. Repeating a
        // synthetic just-pressed edge inside the same long async diagnostic is
        // engine-frame dependent; platform animation samples below remain
        // deterministic after this input-routing contract is proven.
        var inputProbe = WeaponCatalog.Build(WeaponPlatform.P226, 0);
        _player.GrantFireablePrimaryForDiagnostics(inputProbe);
        _player.SetAmmoGradeForDiagnostics(
            LootGrade.Common,
            inputProbe.Stats().MagazineSize * 2);
        _player.SetMagazineAmmoForDiagnostics(0);
        _player.SetAimingPoseForDiagnostics(false);
        Input.ActionRelease(GameInputActions.Reload);
        await WaitFrames(3);
        Input.ActionPress(GameInputActions.Reload);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        Input.ActionRelease(GameInputActions.Reload);
        var inputReloadRouteValid = _player.IsReloading;
        _player.CancelReloadForDiagnostics();
        await WaitFrames(2);

        foreach (var platform in platforms)
        {
            _player.GrantFireablePrimaryForDiagnostics(WeaponCatalog.Build(platform, 0));
            var magazineSize = _player.EquippedWeapon.Stats().MagazineSize;
            _player.SetAmmoGradeForDiagnostics(LootGrade.Common, magazineSize * 2);
            _player.SetMagazineAmmoForDiagnostics(0);
            _player.SetAimingPoseForDiagnostics(false);
            Input.ActionRelease(GameInputActions.Reload);
            await WaitFrames(3);
            _player.FaceWorldPointForDiagnostics(new Vector3(0.0f, 0.2f, -40.0f));
            await WaitFrames(1);

            var idle = _player.InspectSidearmReloadForDiagnostics();
            var reloadStarted = _player.SetReloadPoseForDiagnostics(
                0.0f,
                emptyReload: true)
                && _player.IsReloading;
            _player.ProcessMode = ProcessModeEnum.Disabled;
            var baseline = _player.InspectAllWeaponReloadForDiagnostics();

            var poseSet = true;
            var armsActive = true;
            var reloadLayersValid = true;
            var primaryGripFixed = true;
            var supportTracks = true;
            var visibleMotion = true;
            var viewMotion = true;
            var mechanismStages = true;
            var insertionReadable = true;
            var extractionReadable = true;
            var actionRetreatValid = true;
            var bodyContinuous = true;
            var animationContinuous = true;
            var palmScreenContinuous = true;
            var endpointPoseContinuous = true;
            var samplesValid = true;
            var maximumGripResidual = 0.0f;
            var maximumSupportDistance = 0.0f;
            var minimumArmMotion = float.PositiveInfinity;
            var minimumViewMotion = float.PositiveInfinity;
            var maximumArmMotion = 0.0f;
            var maximumViewMotion = 0.0f;
            var maximumPalmScreenStepRatio = 0.0f;
            var maximumPalmTransitionRatio = 0.0f;
            var maximumEndpointWristCycleError = 0.0f;
            var maximumEndpointPalmCycleError = 0.0f;
            var profile = FirstPersonReloadProfileCatalog.For(platform);
            var samples = new[]
            {
                (Progress: (profile.ReachEnd + profile.ExtractEnd) * 0.5f,
                    Stage: "extract"),
                (Progress: (profile.InsertEnd + profile.SeatEnd) * 0.5f,
                    Stage: "insert"),
                (Progress: (profile.SeatEnd + profile.ActionEnd) * 0.5f,
                    Stage: "action")
            };
            foreach (var sample in samples)
            {
                var set = _player.SetReloadPoseForDiagnostics(
                    sample.Progress,
                    emptyReload: true);
                await WaitFrames(2);
                var inspection = _player.InspectAllWeaponReloadForDiagnostics();
                var viewInspection = _player.InspectSidearmReloadForDiagnostics();
                var armMotion = inspection.LeftPalm.DistanceTo(baseline.LeftPalm);
                var requiredArmMotion = sample.Stage == "action"
                    ? SidearmReloadMinimumActionArmMotion
                    : SidearmReloadMinimumExchangeArmMotion;
                var targetMotion = viewInspection.ReloadViewTarget.DistanceTo(
                        idle.ReloadViewTarget)
                    + viewInspection.ReloadRotationTarget.DistanceTo(
                        idle.ReloadRotationTarget);
                var supportDistance = inspection.LeftPalm.DistanceTo(
                    inspection.SupportTarget);
                var gripResidual = inspection.RightGrip.DistanceTo(
                    inspection.PrimaryGrip);
                var actionTravel = inspection.ActionPosition.DistanceTo(
                    baseline.ActionPosition);
                var magazineScreen = sample.Stage == "extract"
                    ? inspection.ScreenContact.PrimaryMagazineScreen
                    : inspection.ScreenContact.SpareMagazineScreen;
                var expectedMechanism = sample.Stage switch
                {
                    "extract" => inspection.PrimaryMagazineVisible
                        && !inspection.SpareMagazineVisible,
                    "insert" => !inspection.PrimaryMagazineVisible
                        && inspection.SpareMagazineVisible,
                    _ => !inspection.PrimaryMagazineVisible
                        && inspection.SpareMagazineVisible
                        && actionTravel >= 0.025f
                };
                var sampleInsertionReadable = sample.Stage != "insert"
                    || SidearmReloadInsertionReadable(inspection.ScreenContact);
                var sampleExtractionReadable = sample.Stage != "extract"
                    || SidearmReloadExtractionReadable(inspection.ScreenContact);
                var sampleActionRetreat = sample.Stage != "action"
                    || SidearmReloadSupportRetreatReadable(
                        inspection.ScreenContact);
                var sampleBodyContinuous = ReloadBodyContinuityValid(inspection);
                var sampleLayersValid = CompactReloadLayerVisibilityValid(inspection);
                var sampleValid = set
                    && inspection.AnimatedRootActive
                    && inspection.AnimatedMeshActive
                    && sampleLayersValid
                    && inspection.Reloading
                    && Mathf.Abs(inspection.Progress - sample.Progress) <= 0.02f
                    && gripResidual <= 0.004f
                    && supportDistance <= 0.005f
                    && armMotion >= requiredArmMotion
                    && armMotion <= SidearmReloadMaximumArmMotion
                    && targetMotion >= SidearmReloadMinimumViewMotion
                    && targetMotion <= SidearmReloadMaximumViewMotion
                    && inspection.PrimaryMagazineGeometry
                    && inspection.SpareMagazineGeometry
                    && inspection.ActionGeometry
                    && sampleBodyContinuous
                    && expectedMechanism
                    && sampleExtractionReadable
                    && sampleInsertionReadable
                    && sampleActionRetreat;
                samplesValid &= sampleValid;
                poseSet &= set;
                armsActive &= inspection.AnimatedRootActive
                    && inspection.AnimatedMeshActive;
                reloadLayersValid &= sampleLayersValid;
                primaryGripFixed &= gripResidual <= 0.004f;
                supportTracks &= supportDistance <= 0.005f;
                visibleMotion &= armMotion >= requiredArmMotion;
                visibleMotion &= armMotion <= SidearmReloadMaximumArmMotion;
                viewMotion &= targetMotion >= SidearmReloadMinimumViewMotion
                    && targetMotion <= SidearmReloadMaximumViewMotion;
                mechanismStages &= expectedMechanism;
                insertionReadable &= sampleInsertionReadable;
                extractionReadable &= sampleExtractionReadable;
                actionRetreatValid &= sampleActionRetreat;
                bodyContinuous &= sampleBodyContinuous;
                maximumGripResidual = Mathf.Max(
                    maximumGripResidual,
                    gripResidual);
                maximumSupportDistance = Mathf.Max(
                    maximumSupportDistance,
                    supportDistance);
                minimumArmMotion = Mathf.Min(minimumArmMotion, armMotion);
                minimumViewMotion = Mathf.Min(minimumViewMotion, targetMotion);
                maximumArmMotion = Mathf.Max(maximumArmMotion, armMotion);
                maximumViewMotion = Mathf.Max(maximumViewMotion, targetMotion);
                GD.Print(
                    $"SIDEARM_RELOAD_SAMPLE platform={platform} "
                    + $"stage={sample.Stage} progress={sample.Progress:F2} "
                    + $"valid={sampleValid} arms={inspection.AnimatedRootActive} "
                    + $"layers={sampleLayersValid} "
                    + $"reloading={inspection.Reloading} actual_progress={inspection.Progress:F4} "
                    + $"grip_residual={gripResidual:F6} "
                    + $"support_distance={supportDistance:F6} "
                    + $"arm_motion={armMotion:F6}/{requiredArmMotion:F3} "
                    + $"view_motion={targetMotion:F6} "
                    + $"primary_mag={inspection.PrimaryMagazineVisible} "
                    + $"spare_mag={inspection.SpareMagazineVisible} "
                    + $"slide_travel={actionTravel:F6} "
                    + $"extract_screen={sampleExtractionReadable} "
                    + $"insert_screen={sampleInsertionReadable} "
                    + $"action_retreat={sampleActionRetreat} "
                    + $"right_palm={inspection.ScreenContact.RightPalmScreen.X:F1},"
                    + $"{inspection.ScreenContact.RightPalmScreen.Y:F1} "
                    + $"palm={inspection.ScreenContact.LeftPalmScreen.X:F1},"
                    + $"{inspection.ScreenContact.LeftPalmScreen.Y:F1} "
                    + $"mag_grip_y={(sample.Stage == "extract"
                        ? inspection.ScreenContact.PrimaryMagazineGripYRatio
                        : inspection.ScreenContact.SpareMagazineGripYRatio):F3} "
                    + $"mag_grip_x={(sample.Stage == "extract"
                        ? inspection.ScreenContact.PrimaryMagazineGripScreen.X
                        : inspection.ScreenContact.SpareMagazineGripScreen.X):F1} "
                    + $"mag_mesh={magazineScreen.Available}:"
                    + $"{magazineScreen.Bounds.Position.X:F1},"
                    + $"{magazineScreen.Bounds.Position.Y:F1}/"
                    + $"{magazineScreen.Bounds.Size.X:F1},"
                    + $"{magazineScreen.Bounds.Size.Y:F1} "
                    + $"forearms={inspection.BodyContinuity.AnimatedMeshScreen.Available}:"
                    + $"{inspection.BodyContinuity.AnimatedMeshScreen.Bounds.Position.X:F1},"
                    + $"{inspection.BodyContinuity.AnimatedMeshScreen.Bounds.Position.Y:F1}/"
                    + $"{inspection.BodyContinuity.AnimatedMeshScreen.Bounds.Size.X:F1},"
                    + $"{inspection.BodyContinuity.AnimatedMeshScreen.Bounds.Size.Y:F1} "
                    + $"action_grip_y={inspection.ScreenContact.ActionGripYRatio:F3} "
                    + $"body_r={ReloadArmChainSummary(
                        inspection.BodyContinuity.RightArm,
                        inspection.BodyContinuity.ScreenSize)} "
                    + $"body_l={ReloadArmChainSummary(
                        inspection.BodyContinuity.LeftArm,
                        inspection.BodyContinuity.ScreenSize)}");
                if (sample.Stage == "extract")
                {
                    SaveViewportImage(
                        $"res://sidearm_reload_{platform.ToString().ToLowerInvariant()}_extract_validation.png");
                }
                else if (sample.Stage == "insert")
                {
                    SaveViewportImage(
                        $"res://sidearm_reload_{platform.ToString().ToLowerInvariant()}_validation.png");
                }
                else if (sample.Stage == "action")
                {
                    SaveViewportImage(
                        $"res://sidearm_reload_{platform.ToString().ToLowerInvariant()}_empty_action_validation.png");
                }
            }

            foreach (var emptyReload in new[] { false, true })
            {
                _player.ClearReloadPoseForDiagnostics();
                var beforeReload = _player
                    .InspectAllWeaponReloadForDiagnostics();
                _player.SetReloadPoseForDiagnostics(0.0f, emptyReload);
                var reloadStart = _player
                    .InspectAllWeaponReloadForDiagnostics();
                var entryEndpointPose = _player
                    .InspectSidearmReloadEndpointPoseForDiagnostics();
                var lastReloadingEndpointPose = entryEndpointPose;
                var entryPalmTransition = SidearmReloadPalmScreenStepRatio(
                    beforeReload.ScreenContact,
                    reloadStart.ScreenContact);
                var previousPose = _player
                    .InspectAnimatedReloadLeftArmPoseForDiagnostics();
                var previousScreenContact = reloadStart.ScreenContact;
                var maximumShoulderStep = 0.0f;
                var maximumElbowStep = 0.0f;
                var maximumWristStep = 0.0f;
                var maximumPalmStep = 0.0f;
                var maximumElbowPositionStep = 0.0f;
                var maximumWristPositionStep = 0.0f;
                var maximumPalmPositionStep = 0.0f;
                var maximumStepProgress = 0.0f;
                var maximumPositionStepProgress = 0.0f;
                var maximumPositionStepFrom = Vector3.Zero;
                var maximumPositionStepTo = Vector3.Zero;
                var maximumPositionSupportTarget = Vector3.Zero;
                var continuityFrames = 0;
                var continuitySamples = 0;
                var poseUnavailableWhileReloading = !previousPose.Available;
                var screenContactUnavailable = !float.IsFinite(
                    entryPalmTransition)
                    || !SidearmReloadVisiblePalmRegionValid(
                        reloadStart.ScreenContact);
                var exitPalmTransition = float.PositiveInfinity;
                maximumPalmTransitionRatio = Mathf.Max(
                    maximumPalmTransitionRatio,
                    float.IsFinite(entryPalmTransition)
                        ? entryPalmTransition
                        : 0.0f);
                while (_player.IsReloading
                    && continuityFrames < SidearmReloadMaximumContinuityFrames)
                {
                    continuityFrames++;
                    _player.AdvanceVehicleReloadPresentationForDiagnostics(
                        1.0f / 60.0f);
                    var currentInspection = _player
                        .InspectAllWeaponReloadForDiagnostics();
                    if (currentInspection.Reloading)
                    {
                        lastReloadingEndpointPose = _player
                            .InspectSidearmReloadEndpointPoseForDiagnostics();
                        screenContactUnavailable |=
                            !SidearmReloadVisiblePalmRegionValid(
                                currentInspection.ScreenContact);
                    }
                    var screenStep = SidearmReloadPalmScreenStepRatio(
                        previousScreenContact,
                        currentInspection.ScreenContact);
                    if (currentInspection.Reloading)
                    {
                        screenContactUnavailable |= !float.IsFinite(screenStep);
                        if (float.IsFinite(screenStep))
                        {
                            maximumPalmScreenStepRatio = Mathf.Max(
                                maximumPalmScreenStepRatio,
                                screenStep);
                        }
                        previousScreenContact = currentInspection.ScreenContact;
                    }
                    else
                    {
                        exitPalmTransition = screenStep;
                        screenContactUnavailable |= !float.IsFinite(screenStep);
                        maximumPalmTransitionRatio = Mathf.Max(
                            maximumPalmTransitionRatio,
                            float.IsFinite(screenStep) ? screenStep : 0.0f);
                    }
                    var currentPose = _player
                        .InspectAnimatedReloadLeftArmPoseForDiagnostics();
                    if (!previousPose.Available || !currentPose.Available)
                    {
                        poseUnavailableWhileReloading |= _player.IsReloading;
                        previousPose = currentPose;
                        continue;
                    }

                    var shoulderStep = SidearmBoneBasisStep(
                        previousPose.Shoulder.Basis,
                        currentPose.Shoulder.Basis);
                    var elbowStep = SidearmBoneBasisStep(
                        previousPose.Elbow.Basis,
                        currentPose.Elbow.Basis);
                    var wristStep = SidearmBoneBasisStep(
                        previousPose.Wrist.Basis,
                        currentPose.Wrist.Basis);
                    var palmStep = SidearmBoneBasisStep(
                        previousPose.Palm.Basis,
                        currentPose.Palm.Basis);
                    var elbowPositionStep = previousPose.Elbow.Origin.DistanceTo(
                        currentPose.Elbow.Origin);
                    var wristPositionStep = previousPose.Wrist.Origin.DistanceTo(
                        currentPose.Wrist.Origin);
                    var palmPositionStep = previousPose.Palm.Origin.DistanceTo(
                        currentPose.Palm.Origin);
                    var largestBasisStep = Mathf.Max(
                        Mathf.Max(shoulderStep, elbowStep),
                        Mathf.Max(wristStep, palmStep));
                    if (largestBasisStep > Mathf.Max(
                            Mathf.Max(maximumShoulderStep, maximumElbowStep),
                            Mathf.Max(maximumWristStep, maximumPalmStep)))
                    {
                        maximumStepProgress = _player.ReloadProgress;
                    }
                    var largestPositionStep = Mathf.Max(
                        Mathf.Max(elbowPositionStep, wristPositionStep),
                        palmPositionStep);
                    if (largestPositionStep > Mathf.Max(
                            Mathf.Max(
                                maximumElbowPositionStep,
                                maximumWristPositionStep),
                            maximumPalmPositionStep))
                    {
                        maximumPositionStepProgress = _player.ReloadProgress;
                        maximumPositionStepFrom = previousPose.Palm.Origin;
                        maximumPositionStepTo = currentPose.Palm.Origin;
                        maximumPositionSupportTarget = currentInspection.SupportTarget;
                    }
                    maximumShoulderStep = Mathf.Max(
                        maximumShoulderStep,
                        shoulderStep);
                    maximumElbowStep = Mathf.Max(maximumElbowStep, elbowStep);
                    maximumWristStep = Mathf.Max(maximumWristStep, wristStep);
                    maximumPalmStep = Mathf.Max(maximumPalmStep, palmStep);
                    maximumElbowPositionStep = Mathf.Max(
                        maximumElbowPositionStep,
                        elbowPositionStep);
                    maximumWristPositionStep = Mathf.Max(
                        maximumWristPositionStep,
                        wristPositionStep);
                    maximumPalmPositionStep = Mathf.Max(
                        maximumPalmPositionStep,
                        palmPositionStep);
                    previousPose = currentPose;
                    continuitySamples++;
                }
                var maximumBasisStep = Mathf.Max(
                    Mathf.Max(maximumShoulderStep, maximumElbowStep),
                    Mathf.Max(maximumWristStep, maximumPalmStep));
                var maximumJointPositionStep = Mathf.Max(
                    maximumElbowPositionStep,
                    maximumWristPositionStep);
                var endpointWristBasisError = entryEndpointPose.Available
                    && lastReloadingEndpointPose.Available
                    ? SidearmBoneBasisStep(
                        entryEndpointPose.Wrist.Basis,
                        lastReloadingEndpointPose.Wrist.Basis)
                    : float.PositiveInfinity;
                var endpointPalmBasisError = entryEndpointPose.Available
                    && lastReloadingEndpointPose.Available
                    ? SidearmBoneBasisStep(
                        entryEndpointPose.Palm.Basis,
                        lastReloadingEndpointPose.Palm.Basis)
                    : float.PositiveInfinity;
                var endpointPoseValid = float.IsFinite(
                        endpointWristBasisError)
                    && float.IsFinite(endpointPalmBasisError)
                    && endpointWristBasisError
                        <= SidearmReloadMaximumEndpointBasisErrorRadians
                    && endpointPalmBasisError
                        <= SidearmReloadMaximumEndpointBasisErrorRadians;
                maximumEndpointWristCycleError = Mathf.Max(
                    maximumEndpointWristCycleError,
                    float.IsFinite(endpointWristBasisError)
                        ? endpointWristBasisError
                        : 0.0f);
                maximumEndpointPalmCycleError = Mathf.Max(
                    maximumEndpointPalmCycleError,
                    float.IsFinite(endpointPalmBasisError)
                        ? endpointPalmBasisError
                        : 0.0f);
                var continuityValid = !poseUnavailableWhileReloading
                    && !_player.IsReloading
                    && continuitySamples >= SidearmReloadMinimumContinuityFrames
                    && maximumBasisStep
                        <= SidearmReloadMaximumBoneStepRadians
                    && maximumJointPositionStep
                        <= SidearmReloadMaximumJointStepMeters
                    && maximumPalmPositionStep
                        <= SidearmReloadMaximumPalmStepMeters
                    && !screenContactUnavailable
                    && maximumPalmScreenStepRatio
                        <= SidearmReloadMaximumPalmScreenStepRatio
                    && entryPalmTransition
                        <= SidearmReloadMaximumPalmTransitionRatio
                    && exitPalmTransition
                        <= SidearmReloadMaximumPalmTransitionRatio
                    && endpointPoseValid;
                animationContinuous &= continuityValid;
                palmScreenContinuous &= !screenContactUnavailable
                    && maximumPalmScreenStepRatio
                        <= SidearmReloadMaximumPalmScreenStepRatio
                    && entryPalmTransition
                        <= SidearmReloadMaximumPalmTransitionRatio
                    && exitPalmTransition
                        <= SidearmReloadMaximumPalmTransitionRatio;
                endpointPoseContinuous &= endpointPoseValid;
                GD.Print(
                    $"SIDEARM_RELOAD_CONTINUITY platform={platform} "
                    + $"variant={(emptyReload ? "empty" : "tactical")} "
                    + $"valid={continuityValid} frames={continuityFrames} "
                    + $"samples={continuitySamples} basis_progress={maximumStepProgress:F4} "
                    + $"position_progress={maximumPositionStepProgress:F4} "
                    + $"shoulder_basis_step={maximumShoulderStep:F6} "
                    + $"elbow_basis_step={maximumElbowStep:F6} "
                    + $"wrist_basis_step={maximumWristStep:F6} "
                    + $"palm_basis_step={maximumPalmStep:F6} "
                    + $"elbow_position_step={maximumElbowPositionStep:F6} "
                    + $"wrist_position_step={maximumWristPositionStep:F6} "
                    + $"palm_position_step={maximumPalmPositionStep:F6} "
                    + $"palm_screen_step={maximumPalmScreenStepRatio:F6} "
                    + $"entry_screen_step={entryPalmTransition:F6} "
                    + $"exit_screen_step={exitPalmTransition:F6} "
                    + $"endpoint_pose={endpointPoseValid} "
                    + $"entry_endpoint_available={entryEndpointPose.Available} "
                    + $"exit_endpoint_available={lastReloadingEndpointPose.Available} "
                    + $"wrist_cycle_basis_error={endpointWristBasisError:F6} "
                    + $"palm_cycle_basis_error={endpointPalmBasisError:F6} "
                    + $"position_from={maximumPositionStepFrom.X:F4},"
                    + $"{maximumPositionStepFrom.Y:F4},"
                    + $"{maximumPositionStepFrom.Z:F4} "
                    + $"position_to={maximumPositionStepTo.X:F4},"
                    + $"{maximumPositionStepTo.Y:F4},"
                    + $"{maximumPositionStepTo.Z:F4} "
                    + $"support_target={maximumPositionSupportTarget.X:F4},"
                    + $"{maximumPositionSupportTarget.Y:F4},"
                    + $"{maximumPositionSupportTarget.Z:F4}");
            }

            _player.ProcessMode = ProcessModeEnum.Inherit;
            _player.ClearReloadPoseForDiagnostics();
            _player.SetAmmoGradeForDiagnostics(LootGrade.Common, magazineSize * 2);
            var reserveBefore = _player.ReserveAmmo;
            var completed = _player.ReloadImmediatelyForDiagnostics(0);
            var ammoCompleted = completed
                && !_player.IsReloading
                && _player.Ammo == magazineSize
                && _player.ReserveAmmo == reserveBefore - magazineSize;
            var platformValid = inputReloadRouteValid
                && reloadStarted
                && poseSet
                && armsActive
                && reloadLayersValid
                && primaryGripFixed
                && supportTracks
                && visibleMotion
                && viewMotion
                && mechanismStages
                && samplesValid
                && bodyContinuous
                && extractionReadable
                && insertionReadable
                && actionRetreatValid
                && animationContinuous
                && palmScreenContinuous
                && endpointPoseContinuous
                && ammoCompleted;
            valid &= platformValid;
            results.Add(
                $"platform={platform} valid={platformValid} "
                + $"input_reload_route={inputReloadRouteValid} "
                + $"reload_started={reloadStarted} "
                + $"pose_set={poseSet} arms={armsActive} primary_grip={primaryGripFixed} "
                + $"layers={reloadLayersValid} "
                + $"support_tracks={supportTracks} visible_motion={visibleMotion} "
                + $"view_motion={viewMotion} mechanism_stages={mechanismStages} "
                + $"samples={samplesValid} body_continuous={bodyContinuous} "
                + $"animation_continuous={animationContinuous} "
                + $"palm_screen_continuous={palmScreenContinuous} "
                + $"endpoint_pose_continuous={endpointPoseContinuous} "
                + $"extract_screen={extractionReadable} "
                + $"insert_screen={insertionReadable} "
                + $"action_retreat={actionRetreatValid} "
                + $"ammo_completed={ammoCompleted} ammo={_player.Ammo} "
                + $"reserve={_player.ReserveAmmo} max_grip_residual={maximumGripResidual:F6} "
                + $"max_support_distance={maximumSupportDistance:F6} "
                + $"max_palm_screen_step={maximumPalmScreenStepRatio:F6} "
                + $"max_palm_transition={maximumPalmTransitionRatio:F6} "
                + $"max_endpoint_wrist_cycle_error={maximumEndpointWristCycleError:F6} "
                + $"max_endpoint_palm_cycle_error={maximumEndpointPalmCycleError:F6} "
                + $"min_arm_motion={minimumArmMotion:F6} "
                + $"max_arm_motion={maximumArmMotion:F6} "
                + $"min_view_motion={minimumViewMotion:F6} "
                + $"max_view_motion={maximumViewMotion:F6}");
        }

        foreach (var result in results)
        {
            GD.Print($"SIDEARM_RELOAD_PLATFORM {result}");
        }
        GD.Print(
            $"SIDEARM_RELOAD_CHECK valid={valid} physical_r={reloadKeyBound} "
            + $"input_reload_route={inputReloadRouteValid} "
            + $"platforms={results.Count}");
        GD.Print($"SIDEARM_RELOAD_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }

    private static bool ReloadActionUsesPhysicalR()
    {
        foreach (var inputEvent in InputMap.ActionGetEvents(GameInputActions.Reload))
        {
            if (inputEvent is InputEventKey key
                && (key.PhysicalKeycode == Key.R || key.Keycode == Key.R))
            {
                return true;
            }
        }
        return false;
    }

    private static float SidearmBoneBasisStep(Basis previous, Basis current)
        => previous.Orthonormalized()
            .GetRotationQuaternion()
            .AngleTo(current.Orthonormalized().GetRotationQuaternion());

    private static float SidearmReloadPalmScreenStepRatio(
        ReloadScreenContactInspection previous,
        ReloadScreenContactInspection current)
    {
        if (!previous.LeftPalmAvailable
            || !previous.RightPalmAvailable
            || !current.LeftPalmAvailable
            || !current.RightPalmAvailable
            || previous.LeftPalmBehindCamera
            || previous.RightPalmBehindCamera
            || current.LeftPalmBehindCamera
            || current.RightPalmBehindCamera
            || previous.ScreenSize.Y <= 0.0f
            || current.ScreenSize.Y <= 0.0f)
        {
            return float.PositiveInfinity;
        }

        var screenHeight = Mathf.Min(
            previous.ScreenSize.Y,
            current.ScreenSize.Y);
        return Mathf.Max(
                previous.LeftPalmScreen.DistanceTo(current.LeftPalmScreen),
                previous.RightPalmScreen.DistanceTo(current.RightPalmScreen))
            / screenHeight;
    }

    private static bool SidearmReloadVisiblePalmRegionValid(
        ReloadScreenContactInspection contact)
    {
        if (!contact.LeftPalmAvailable
            || !contact.RightPalmAvailable
            || contact.LeftPalmBehindCamera
            || contact.RightPalmBehindCamera
            || contact.ScreenSize.X <= 0.0f
            || contact.ScreenSize.Y <= 0.0f)
        {
            return false;
        }

        var minimum = new Vector2(
            contact.ScreenSize.X * -0.05f,
            contact.ScreenSize.Y * 0.20f);
        var maximum = new Vector2(
            contact.ScreenSize.X * 1.05f,
            contact.ScreenSize.Y * 1.08f);
        return contact.LeftPalmScreen.X >= minimum.X
            && contact.LeftPalmScreen.X <= maximum.X
            && contact.LeftPalmScreen.Y >= minimum.Y
            && contact.LeftPalmScreen.Y <= maximum.Y
            && contact.RightPalmScreen.X >= minimum.X
            && contact.RightPalmScreen.X <= maximum.X
            && contact.RightPalmScreen.Y >= minimum.Y
            && contact.RightPalmScreen.Y <= maximum.Y;
    }

}
