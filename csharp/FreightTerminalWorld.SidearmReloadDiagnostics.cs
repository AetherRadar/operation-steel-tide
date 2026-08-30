using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private const float SidearmReloadMaximumBoneStepRadians = 0.65f;
    private const float SidearmReloadMaximumJointStepMeters = 0.21f;
    private const float SidearmReloadMaximumPalmStepMeters = 0.15f;
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

        foreach (var platform in platforms)
        {
            _player.GrantFireablePrimaryForDiagnostics(WeaponCatalog.Build(platform, 0));
            var magazineSize = _player.EquippedWeapon.Stats().MagazineSize;
            _player.SetAmmoGradeForDiagnostics(LootGrade.Common, magazineSize * 2);
            _player.SetMagazineAmmoForDiagnostics(0);
            _player.SetAimingPoseForDiagnostics(false);
            Input.ActionRelease(GameInputActions.Reload);
            await WaitFrames(3);

            var idle = _player.InspectSidearmReloadForDiagnostics();
            var idleHand = _player.InspectAuthoredHandPoseForDiagnostics();
            Input.ActionPress(GameInputActions.Reload);
            await WaitFrames(2);
            Input.ActionRelease(GameInputActions.Reload);
            var inputStarted = _player.IsReloading;
            _player.ProcessMode = ProcessModeEnum.Disabled;
            _player.SetReloadPoseForDiagnostics(0.0f, emptyReload: true);
            var baseline = _player.InspectAllWeaponReloadForDiagnostics();

            var poseSet = true;
            var armsActive = true;
            var primaryGripFixed = true;
            var supportTracks = true;
            var visibleMotion = true;
            var viewMotion = true;
            var mechanismStages = true;
            var insertionReadable = true;
            var extractionReadable = true;
            var actionReadable = true;
            var bodyContinuous = true;
            var animationContinuous = true;
            var samplesValid = true;
            var maximumGripResidual = 0.0f;
            var maximumSupportDistance = 0.0f;
            var minimumArmMotion = float.PositiveInfinity;
            var minimumViewMotion = float.PositiveInfinity;
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
                    || inspection.ScreenContact.InsertionReadable;
                var sampleExtractionReadable = sample.Stage != "extract"
                    || inspection.ScreenContact.ExtractionReadable;
                var sampleActionReadable = sample.Stage != "action"
                    || inspection.ScreenContact.ActionReadable;
                var sampleBodyContinuous = ReloadBodyContinuityValid(inspection);
                var sampleValid = set
                    && inspection.AnimatedRootActive
                    && inspection.AnimatedMeshActive
                    && !inspection.StaticArmsActive
                    && inspection.Reloading
                    && Mathf.Abs(inspection.Progress - sample.Progress) <= 0.02f
                    && gripResidual <= 0.004f
                    && supportDistance <= 0.005f
                    && armMotion >= 0.050f
                    && targetMotion >= 0.02f
                    && inspection.PrimaryMagazineGeometry
                    && inspection.SpareMagazineGeometry
                    && inspection.ActionGeometry
                    && sampleBodyContinuous
                    && expectedMechanism
                    && sampleExtractionReadable
                    && sampleInsertionReadable
                    && sampleActionReadable;
                samplesValid &= sampleValid;
                poseSet &= set;
                armsActive &= inspection.AnimatedRootActive
                    && inspection.AnimatedMeshActive
                    && !inspection.StaticArmsActive;
                primaryGripFixed &= gripResidual <= 0.004f;
                supportTracks &= supportDistance <= 0.005f;
                visibleMotion &= armMotion >= 0.050f;
                viewMotion &= targetMotion >= 0.02f;
                mechanismStages &= expectedMechanism;
                insertionReadable &= sampleInsertionReadable;
                extractionReadable &= sampleExtractionReadable;
                actionReadable &= sampleActionReadable;
                bodyContinuous &= sampleBodyContinuous;
                maximumGripResidual = Mathf.Max(
                    maximumGripResidual,
                    gripResidual);
                maximumSupportDistance = Mathf.Max(
                    maximumSupportDistance,
                    supportDistance);
                minimumArmMotion = Mathf.Min(minimumArmMotion, armMotion);
                minimumViewMotion = Mathf.Min(minimumViewMotion, targetMotion);
                GD.Print(
                    $"SIDEARM_RELOAD_SAMPLE platform={platform} "
                    + $"stage={sample.Stage} progress={sample.Progress:F2} "
                    + $"valid={sampleValid} arms={inspection.AnimatedRootActive} "
                    + $"reloading={inspection.Reloading} actual_progress={inspection.Progress:F4} "
                    + $"grip_residual={gripResidual:F6} "
                    + $"support_distance={supportDistance:F6} "
                    + $"arm_motion={armMotion:F6} view_motion={targetMotion:F6} "
                    + $"primary_mag={inspection.PrimaryMagazineVisible} "
                    + $"spare_mag={inspection.SpareMagazineVisible} "
                    + $"slide_travel={actionTravel:F6} "
                    + $"extract_screen={sampleExtractionReadable} "
                    + $"insert_screen={sampleInsertionReadable} "
                    + $"action_screen={sampleActionReadable} "
                    + $"palm_y={inspection.ScreenContact.LeftPalmYRatio:F3} "
                    + $"mag_grip_y={inspection.ScreenContact.SpareMagazineGripYRatio:F3} "
                    + $"action_grip_y={inspection.ScreenContact.ActionGripYRatio:F3} "
                    + $"body_r={ReloadArmChainSummary(
                        inspection.BodyContinuity.RightArm,
                        inspection.BodyContinuity.ScreenSize)} "
                    + $"body_l={ReloadArmChainSummary(
                        inspection.BodyContinuity.LeftArm,
                        inspection.BodyContinuity.ScreenSize)}");
                if (sample.Stage == "insert")
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
                _player.SetReloadPoseForDiagnostics(0.0f, emptyReload);
                var previousPose = _player
                    .InspectAnimatedReloadLeftArmPoseForDiagnostics();
                var maximumShoulderStep = 0.0f;
                var maximumElbowStep = 0.0f;
                var maximumWristStep = 0.0f;
                var maximumPalmStep = 0.0f;
                var maximumElbowPositionStep = 0.0f;
                var maximumWristPositionStep = 0.0f;
                var maximumPalmPositionStep = 0.0f;
                var maximumStepProgress = 0.0f;
                var continuityFrames = 0;
                var continuitySamples = 0;
                var poseUnavailableWhileReloading = !previousPose.Available;
                while (_player.IsReloading
                    && continuityFrames < SidearmReloadMaximumContinuityFrames)
                {
                    continuityFrames++;
                    _player.AdvanceVehicleReloadPresentationForDiagnostics(
                        1.0f / 60.0f);
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
                var continuityValid = !poseUnavailableWhileReloading
                    && !_player.IsReloading
                    && continuitySamples >= SidearmReloadMinimumContinuityFrames
                    && maximumBasisStep
                        <= SidearmReloadMaximumBoneStepRadians
                    && maximumJointPositionStep
                        <= SidearmReloadMaximumJointStepMeters
                    && maximumPalmPositionStep
                        <= SidearmReloadMaximumPalmStepMeters;
                animationContinuous &= continuityValid;
                GD.Print(
                    $"SIDEARM_RELOAD_CONTINUITY platform={platform} "
                    + $"variant={(emptyReload ? "empty" : "tactical")} "
                    + $"valid={continuityValid} frames={continuityFrames} "
                    + $"samples={continuitySamples} progress={maximumStepProgress:F4} "
                    + $"shoulder_basis_step={maximumShoulderStep:F6} "
                    + $"elbow_basis_step={maximumElbowStep:F6} "
                    + $"wrist_basis_step={maximumWristStep:F6} "
                    + $"palm_basis_step={maximumPalmStep:F6} "
                    + $"elbow_position_step={maximumElbowPositionStep:F6} "
                    + $"wrist_position_step={maximumWristPositionStep:F6} "
                    + $"palm_position_step={maximumPalmPositionStep:F6}");
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
            var platformValid = idleHand.Valid
                && inputStarted
                && poseSet
                && armsActive
                && primaryGripFixed
                && supportTracks
                && visibleMotion
                && viewMotion
                && mechanismStages
                && samplesValid
                && bodyContinuous
                && extractionReadable
                && insertionReadable
                && actionReadable
                && animationContinuous
                && ammoCompleted;
            valid &= platformValid;
            results.Add(
                $"platform={platform} valid={platformValid} input_started={inputStarted} "
                + $"pose_set={poseSet} arms={armsActive} primary_grip={primaryGripFixed} "
                + $"support_tracks={supportTracks} visible_motion={visibleMotion} "
                + $"view_motion={viewMotion} mechanism_stages={mechanismStages} "
                + $"samples={samplesValid} body_continuous={bodyContinuous} "
                + $"animation_continuous={animationContinuous} "
                + $"extract_screen={extractionReadable} "
                + $"insert_screen={insertionReadable} "
                + $"action_screen={actionReadable} "
                + $"ammo_completed={ammoCompleted} ammo={_player.Ammo} "
                + $"reserve={_player.ReserveAmmo} max_grip_residual={maximumGripResidual:F6} "
                + $"max_support_distance={maximumSupportDistance:F6} "
                + $"min_arm_motion={minimumArmMotion:F6} "
                + $"min_view_motion={minimumViewMotion:F6}");
        }

        foreach (var result in results)
        {
            GD.Print($"SIDEARM_RELOAD_PLATFORM {result}");
        }
        GD.Print(
            $"SIDEARM_RELOAD_CHECK valid={valid} physical_r={reloadKeyBound} "
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

}
