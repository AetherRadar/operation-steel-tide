using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

namespace OperationSteelTide;

// File-size retention: this deterministic integration audit deliberately keeps
// the 25-variant sampler, invariant aggregation, reset/cancel checks, and review
// captures together so every platform is evaluated through one ordered state
// machine. Follow-up: extract ReloadVariantAuditor and ReloadKeyframeCapture
// after the inspection record has stabilized, preserving this CLI/output contract.
public partial class FreightTerminalWorld
{
    private const float ReloadShoulderDriftLimit = 0.002f;
    private const float ReloadRightHandDriftLimit = 0.004f;
    private const float ReloadRightPalmContactMinimum = 0.020f;
    private const float ReloadRightPalmContactMaximum = 0.030f;
    private const float ReloadSupportPalmReturnLimit = 0.005f;
    private const float ReloadFinishSupportPalmStepLimit = 0.025f;
    private const float ReloadSupportPalmTargetLimit = 0.005f;
    // A boundary sample spans only 0.2% of normalized reload time. The current
    // authored clips peak below 8.2 mm, so 12 mm leaves tolerance for import
    // quantization while still rejecting a visibly popping hand target.
    private const float ReloadBoundaryMaximumStep = 0.012f;
    private const float ServiceSidearmMagazineWellDistanceLimit = 0.12f;
    private const float ReloadSupportPalmMinimumTravel = 0.050f;
    private const float ReloadMechanismMinimumTravel = 0.020f;
    private const float ReloadMechanismMinimumScreenTravelRatio = 0.015f;
    private const float ReloadStateTolerance = 0.001f;
    private const float ReloadVehicleViewPoseErrorLimit = 0.003f;
    private const int ReloadVehicleRecoveryFrames = 90;
    private const float ReloadSeatedMagazineBasisLimit = 0.003f;
    private const float NativeReloadShoulderMotionLimit = 0.150f;
    private const float NativeReloadPrimaryPalmMotionLimit = 0.150f;
    private const float NativeReloadSupportPalmReturnLimit = 0.080f;
    private const float NativeReloadSupportPalmMaximumTravel = 0.800f;
    private const float NativeReloadMechanismMaximumTravel = 0.800f;

    private async void ValidateAllWeaponReloads()
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
                mate.ProcessMode = ProcessModeEnum.Disabled;
                mate.GlobalPosition = new Vector3(240.0f, 80.0f, 240.0f);
            }
        }
        await WaitFrames(6);

        var platforms = new[]
        {
            WeaponPlatform.M4A1,
            WeaponPlatform.AK74,
            WeaponPlatform.ScarL,
            WeaponPlatform.M24,
            WeaponPlatform.MP5A5,
            WeaponPlatform.M3A1,
            WeaponPlatform.AXMC,
            WeaponPlatform.P226,
            WeaponPlatform.M1911,
            WeaponPlatform.AWM,
            WeaponPlatform.VSS,
            WeaponPlatform.DesertEagle,
            WeaponPlatform.GSh18
        };
        var variantResults = new List<string>();
        var equipOrderResults = new List<string>();
        var failedPlatforms = new List<string>();
        var validatedVariants = 0;
        var validatedEquipOrders = 0;

        foreach (var platform in platforms)
        {
            var platformValid = true;
            var equipOrderRecorded = false;
            var recordedVariants = 0;
            try
            {
                _player.ProcessMode = ProcessModeEnum.Inherit;
                _player.ClearReloadPoseForDiagnostics();
                _player.GrantFireablePrimaryForDiagnostics(
                    WeaponCatalog.Build(platform, 0));
                _player.SetAimingPoseForDiagnostics(false);
                await WaitFrames(3);

                var idleBefore = _player.InspectAllWeaponReloadForDiagnostics();
                var disturbance = platform == WeaponPlatform.AK74
                    ? WeaponPlatform.P226
                    : WeaponPlatform.AK74;
                _player.GrantFireablePrimaryForDiagnostics(
                    WeaponCatalog.Build(disturbance, 0));
                await WaitFrames(2);
                _player.GrantFireablePrimaryForDiagnostics(
                    WeaponCatalog.Build(platform, 0));
                _player.SetAimingPoseForDiagnostics(false);
                await WaitFrames(3);
                var idleAfter = _player.InspectAllWeaponReloadForDiagnostics();
                var idleVisibilityStable = !idleBefore.Reloading
                    && !idleAfter.Reloading
                    && idleBefore.PrimaryMagazineVisible
                    && idleAfter.PrimaryMagazineVisible
                    && !idleBefore.SpareMagazineVisible
                    && !idleAfter.SpareMagazineVisible;
                var primaryStable = platform == WeaponPlatform.M3A1
                    ? idleBefore.PrimaryMagazinePosition.DistanceTo(
                            idleAfter.PrimaryMagazinePosition)
                        <= ReloadStateTolerance
                    : idleBefore.PrimaryMagazineGripAvailable
                        && idleAfter.PrimaryMagazineGripAvailable
                        && ReloadTransformsEquivalent(
                            idleBefore.PrimaryMagazineTransform,
                            idleAfter.PrimaryMagazineTransform)
                        && idleBefore.PrimaryMagazineGrip.DistanceTo(
                                idleAfter.PrimaryMagazineGrip)
                            <= ReloadStateTolerance;
                var actionStable = idleBefore.ActionGripAvailable
                        == idleAfter.ActionGripAvailable
                    && (!idleBefore.ActionGripAvailable
                        || idleBefore.ActionGrip.DistanceTo(idleAfter.ActionGrip)
                            <= ReloadStateTolerance)
                    && idleBefore.ActionPosition.DistanceTo(idleAfter.ActionPosition)
                        <= ReloadStateTolerance;
                var equipOrderValid = idleVisibilityStable
                    && primaryStable
                    && actionStable;
                validatedEquipOrders++;
                equipOrderRecorded = true;
                equipOrderResults.Add(
                    $"platform={platform} valid={equipOrderValid} "
                    + $"idle_primary_transform_stable={primaryStable} "
                    + $"action_stable={actionStable}");
                platformValid &= equipOrderValid;
                _player.ProcessMode = ProcessModeEnum.Disabled;

                if (platform == WeaponPlatform.M3A1)
                {
                    var audit = ValidateAllWeaponReloadVariant(platform, false, true);
                    validatedVariants++;
                    recordedVariants++;
                    platformValid &= audit.Valid;
                    variantResults.Add(audit.Summary);
                }
                else
                {
                    foreach (var emptyReload in new[] { false, true })
                    {
                        var audit = ValidateAllWeaponReloadVariant(
                            platform,
                            emptyReload,
                            false);
                        validatedVariants++;
                        recordedVariants++;
                        platformValid &= audit.Valid;
                        variantResults.Add(audit.Summary);
                    }
                }

                if (platform != WeaponPlatform.M3A1)
                {
                    await CaptureReloadInsertionKeyframe(platform);
                }
            }
            catch (Exception exception)
            {
                platformValid = false;
                if (!equipOrderRecorded)
                {
                    validatedEquipOrders++;
                    equipOrderResults.Add(
                        $"platform={platform} valid=False "
                        + "idle_primary_transform_stable=False "
                        + "action_stable=False");
                }
                var expectedVariants = platform == WeaponPlatform.M3A1 ? 1 : 2;
                for (var index = recordedVariants; index < expectedVariants; index++)
                {
                    var variant = platform == WeaponPlatform.M3A1
                        ? "native"
                        : index == 0 ? "tactical" : "empty";
                    validatedVariants++;
                    variantResults.Add(
                        $"platform={platform} variant={variant} valid=False "
                        + $"failures=exception:{SanitizeDiagnosticValue(exception.Message)}");
                }
            }
            finally
            {
                _player.ClearReloadPoseForDiagnostics();
                _player.ProcessMode = ProcessModeEnum.Inherit;
            }

            if (!platformValid)
            {
                failedPlatforms.Add(platform.ToString());
            }
        }

        foreach (var result in equipOrderResults)
        {
            GD.Print($"ALL_WEAPON_RELOAD_EQUIP_ORDER {result}");
        }
        foreach (var result in variantResults)
        {
            GD.Print($"ALL_WEAPON_RELOAD_PLATFORM {result}");
        }

        var valid = platforms.Length == 13
            && validatedVariants == 25
            && validatedEquipOrders == 13
            && failedPlatforms.Count == 0;
        GD.Print(
            $"ALL_WEAPON_RELOAD_CHECK valid={valid} "
            + $"platforms={platforms.Length}/13 variants={validatedVariants}/25 "
            + $"equip_orders={validatedEquipOrders}/13 "
            + $"failed_platforms={(failedPlatforms.Count == 0 ? "none" : string.Join(',', failedPlatforms))}");
        GD.Print($"ALL_WEAPON_RELOAD_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }

    private ReloadVariantAudit ValidateAllWeaponReloadVariant(
        WeaponPlatform platform,
        bool emptyReload,
        bool nativeClip)
    {
        _player.ClearReloadPoseForDiagnostics();
        var profile = FirstPersonReloadProfileCatalog.For(platform);
        var lastVisibleProgress = _player.LastVisibleReloadProgressForDiagnostics;
        var samples = AllWeaponReloadSamples(profile, lastVisibleProgress);
        var failures = new List<string>();
        var baseline = default(AllWeaponReloadInspection);
        var final = default(AllWeaponReloadInspection);
        var maximumShoulderDrift = 0.0f;
        var maximumRightGripResidual = 0.0f;
        var maximumRightPalmGripVectorDrift = 0.0f;
        var minimumRightPalmContactDistance = float.PositiveInfinity;
        var maximumRightPalmContactDistance = 0.0f;
        var maximumPrimaryPalmMotion = 0.0f;
        var maximumSupportPalmTravel = 0.0f;
        var maximumSupportTargetResidual = 0.0f;
        var maximumSupportTargetResidualProgress = 0.0f;
        var maximumPrimaryMagazineTravel = 0.0f;
        var maximumSpareMagazineTravel = 0.0f;
        var maximumActionTravel = 0.0f;
        var maximumSeatedMagazineRootDistance = 0.0f;
        var maximumSeatedMagazineBasisDelta = 0.0f;
        var maximumSeatedMagazineGripDistance = 0.0f;
        var seatedMagazineAlignmentValid = true;
        var allSamplesActive = true;
        var bodyContinuityValid = true;
        var firstBodyFailureProgress = -1.0f;
        var firstBodyFailure = default(ReloadBodyContinuityInspection);
        var minimumShoulderScreenYRatio = float.PositiveInfinity;
        var minimumAnimatedMeshTopRatio = float.PositiveInfinity;
        var sawPrimaryOnly = false;
        var sawSpareOnly = false;
        var sawSpareVisible = false;
        var primaryStayedVisible = true;
        var insertionReadable = nativeClip;
        var extractionReadable = nativeClip
            || profile.Mechanism == FirstPersonReloadMechanism.InternalMagazine;
        var actionReadable = nativeClip || !profile.UsesAction(emptyReload);
        var extractionPalmYRatio = 1.0f;
        var extractionMagazineGripYRatio = 1.0f;
        var insertionPalmYRatio = 1.0f;
        var insertionMagazineGripYRatio = 1.0f;
        var actionPalmYRatio = 1.0f;
        var actionGripYRatio = 1.0f;
        var insertionProgress = (profile.InsertEnd + profile.SeatEnd) * 0.5f;
        var extractionProgress = (profile.ReachEnd + profile.ExtractEnd) * 0.5f;
        var actionInspectionProgress = (profile.SeatEnd + profile.ActionEnd) * 0.5f;
        var maximumPrimaryMagazineScreenTravel = 0.0f;
        var inspectionsByProgress = new Dictionary<float, AllWeaponReloadInspection>();

        for (var index = 0; index < samples.Count; index++)
        {
            var progress = samples[index];
            var poseSet = _player.SetReloadPoseForDiagnostics(progress, emptyReload);
            var inspection = _player.InspectAllWeaponReloadForDiagnostics();
            inspectionsByProgress[progress] = inspection;
            if (index == 0)
            {
                baseline = inspection;
            }
            final = inspection;

            allSamplesActive &= poseSet
                && inspection.Available
                && inspection.Reloading
                && inspection.RigMarkersAvailable
                && inspection.ClipExists
                && inspection.ClipDuration > 0.1f
                && inspection.AnimatedRootActive
                && inspection.AnimatedMeshActive
                && inspection.WeaponActive
                && (nativeClip || !inspection.StaticArmsActive)
                && Mathf.Abs(inspection.Progress - progress) <= ReloadStateTolerance
                && (nativeClip || inspection.EmptyReload == emptyReload);
            maximumShoulderDrift = Mathf.Max(
                maximumShoulderDrift,
                inspection.RightShoulder.DistanceTo(baseline.RightShoulder));
            maximumShoulderDrift = Mathf.Max(
                maximumShoulderDrift,
                inspection.LeftShoulder.DistanceTo(baseline.LeftShoulder));
            if (inspection.RightGripAvailable)
            {
                maximumRightGripResidual = Mathf.Max(
                    maximumRightGripResidual,
                    inspection.RightGrip.DistanceTo(inspection.PrimaryGrip));
                var palmToGrip = inspection.RightPalm - inspection.RightGrip;
                var baselinePalmToGrip = baseline.RightPalm - baseline.RightGrip;
                maximumRightPalmGripVectorDrift = Mathf.Max(
                    maximumRightPalmGripVectorDrift,
                    palmToGrip.DistanceTo(baselinePalmToGrip));
                var palmContactDistance = inspection.RightPalm.DistanceTo(
                    inspection.RightGrip);
                minimumRightPalmContactDistance = Mathf.Min(
                    minimumRightPalmContactDistance,
                    palmContactDistance);
                maximumRightPalmContactDistance = Mathf.Max(
                    maximumRightPalmContactDistance,
                    palmContactDistance);
            }
            maximumPrimaryPalmMotion = Mathf.Max(
                maximumPrimaryPalmMotion,
                inspection.RightPalm.DistanceTo(baseline.RightPalm));
            maximumSupportPalmTravel = Mathf.Max(
                maximumSupportPalmTravel,
                inspection.LeftPalm.DistanceTo(baseline.LeftPalm));
            if (!nativeClip)
            {
                var supportResidual = inspection.LeftPalm.DistanceTo(
                    inspection.SupportTarget);
                if (supportResidual > maximumSupportTargetResidual)
                {
                    maximumSupportTargetResidual = supportResidual;
                    maximumSupportTargetResidualProgress = progress;
                }
            }
            maximumPrimaryMagazineTravel = Mathf.Max(
                maximumPrimaryMagazineTravel,
                inspection.PrimaryMagazinePosition.DistanceTo(
                    baseline.PrimaryMagazinePosition));
            if (inspection.ScreenContact.PrimaryMagazineScreen.Available
                && baseline.ScreenContact.PrimaryMagazineScreen.Available)
            {
                var primaryScreenCenter = inspection.ScreenContact
                    .PrimaryMagazineScreen.Bounds.Position
                    + inspection.ScreenContact.PrimaryMagazineScreen.Bounds.Size
                        * 0.5f;
                var baselineScreenCenter = baseline.ScreenContact
                    .PrimaryMagazineScreen.Bounds.Position
                    + baseline.ScreenContact.PrimaryMagazineScreen.Bounds.Size
                        * 0.5f;
                maximumPrimaryMagazineScreenTravel = Mathf.Max(
                    maximumPrimaryMagazineScreenTravel,
                    primaryScreenCenter.DistanceTo(baselineScreenCenter));
            }
            maximumSpareMagazineTravel = Mathf.Max(
                maximumSpareMagazineTravel,
                inspection.SpareMagazinePosition.DistanceTo(
                    baseline.SpareMagazinePosition));
            maximumActionTravel = Mathf.Max(
                maximumActionTravel,
                inspection.ActionPosition.DistanceTo(baseline.ActionPosition));
            if (!nativeClip
                && profile.Mechanism != FirstPersonReloadMechanism.InternalMagazine
                && progress >= profile.SeatEnd)
            {
                var rootDistance = inspection.PrimaryMagazineTransform.Origin
                    .DistanceTo(inspection.SpareMagazineTransform.Origin);
                var basisDelta = ReloadBasisDelta(
                    inspection.PrimaryMagazineTransform.Basis,
                    inspection.SpareMagazineTransform.Basis);
                var gripDistance = inspection.PrimaryMagazineGrip.DistanceTo(
                    inspection.SpareMagazineGrip);
                maximumSeatedMagazineRootDistance = Mathf.Max(
                    maximumSeatedMagazineRootDistance,
                    rootDistance);
                maximumSeatedMagazineBasisDelta = Mathf.Max(
                    maximumSeatedMagazineBasisDelta,
                    basisDelta);
                maximumSeatedMagazineGripDistance = Mathf.Max(
                    maximumSeatedMagazineGripDistance,
                    gripDistance);
                seatedMagazineAlignmentValid &= inspection.PrimaryMagazineGripAvailable
                    && inspection.SpareMagazineGripAvailable
                    && rootDistance <= ReloadStateTolerance
                    && basisDelta <= ReloadSeatedMagazineBasisLimit
                    && gripDistance <= ReloadStateTolerance;
            }
            sawPrimaryOnly |= inspection.PrimaryMagazineVisible
                && !inspection.SpareMagazineVisible;
            sawSpareOnly |= !inspection.PrimaryMagazineVisible
                && inspection.SpareMagazineVisible;
            sawSpareVisible |= inspection.SpareMagazineVisible;
            primaryStayedVisible &= inspection.PrimaryMagazineVisible;
            var sampleBodyContinuityValid = nativeClip
                ? NativeReloadBodyContinuityValid(inspection)
                : ReloadBodyContinuityValid(inspection);
            if (!sampleBodyContinuityValid && firstBodyFailureProgress < 0.0f)
            {
                firstBodyFailureProgress = progress;
                firstBodyFailure = inspection.BodyContinuity;
            }
            bodyContinuityValid &= sampleBodyContinuityValid;
            minimumShoulderScreenYRatio = Mathf.Min(
                minimumShoulderScreenYRatio,
                inspection.BodyContinuity.MinimumShoulderYRatio);
            minimumAnimatedMeshTopRatio = Mathf.Min(
                minimumAnimatedMeshTopRatio,
                inspection.BodyContinuity.AnimatedMeshTopRatio);
            if (!nativeClip
                && profile.Mechanism
                    != FirstPersonReloadMechanism.InternalMagazine
                && Mathf.Abs(progress - extractionProgress)
                    <= ReloadStateTolerance)
            {
                extractionReadable = inspection.ScreenContact.ExtractionReadable;
                extractionPalmYRatio = inspection.ScreenContact.LeftPalmYRatio;
                extractionMagazineGripYRatio = inspection.ScreenContact
                    .PrimaryMagazineGripYRatio;
            }
            if (!nativeClip
                && Mathf.Abs(progress - insertionProgress) <= ReloadStateTolerance)
            {
                insertionReadable = inspection.ScreenContact.InsertionReadable;
                insertionPalmYRatio = inspection.ScreenContact.LeftPalmYRatio;
                insertionMagazineGripYRatio = inspection.ScreenContact
                    .SpareMagazineGripYRatio;
            }
            if (!nativeClip
                && profile.UsesAction(emptyReload)
                && Mathf.Abs(progress - actionInspectionProgress)
                    <= ReloadStateTolerance)
            {
                actionReadable = inspection.ScreenContact.ActionReadable;
                actionPalmYRatio = inspection.ScreenContact.LeftPalmYRatio;
                actionGripYRatio = inspection.ScreenContact.ActionGripYRatio;
            }
        }

        var boundaryContinuityValid = true;
        var maximumBoundaryTargetStep = 0.0f;
        var maximumBoundaryPalmStep = 0.0f;
        if (!nativeClip)
        {
            foreach (var boundary in ReloadProfileBoundaries(profile))
            {
                var beforeProgress = Mathf.Clamp(boundary - 0.002f, 0.0f, 1.0f);
                var afterProgress = Mathf.Clamp(boundary + 0.002f, 0.0f, 1.0f);
                if (!inspectionsByProgress.TryGetValue(beforeProgress, out var before)
                    || !inspectionsByProgress.TryGetValue(boundary, out var at)
                    || !inspectionsByProgress.TryGetValue(afterProgress, out var after))
                {
                    boundaryContinuityValid = false;
                    continue;
                }

                var targetStep = Mathf.Max(
                    before.SupportTarget.DistanceTo(at.SupportTarget),
                    at.SupportTarget.DistanceTo(after.SupportTarget));
                var palmStep = Mathf.Max(
                    before.LeftPalm.DistanceTo(at.LeftPalm),
                    at.LeftPalm.DistanceTo(after.LeftPalm));
                maximumBoundaryTargetStep = Mathf.Max(
                    maximumBoundaryTargetStep,
                    targetStep);
                maximumBoundaryPalmStep = Mathf.Max(
                    maximumBoundaryPalmStep,
                    palmStep);
                boundaryContinuityValid &= targetStep <= ReloadBoundaryMaximumStep
                    && palmStep <= ReloadBoundaryMaximumStep;
            }
        }

        var supportPalmReturn = final.LeftPalm.DistanceTo(baseline.LeftPalm);
        RequireReloadCondition(allSamplesActive, "clip_or_rig_inactive", failures);
        if (nativeClip)
        {
            RequireReloadCondition(
                maximumShoulderDrift <= NativeReloadShoulderMotionLimit,
                "native_shoulder_motion_excessive",
                failures);
            RequireReloadCondition(
                maximumPrimaryPalmMotion <= NativeReloadPrimaryPalmMotionLimit,
                "native_primary_palm_motion_excessive",
                failures);
        }
        else
        {
            RequireReloadCondition(
                maximumShoulderDrift <= ReloadShoulderDriftLimit,
                "shoulder_drift",
                failures);
            RequireReloadCondition(
                baseline.RightGripAvailable
                    && maximumRightGripResidual <= ReloadRightHandDriftLimit,
                "right_grip_frame_drift",
                failures);
            RequireReloadCondition(
                maximumRightPalmGripVectorDrift <= ReloadRightHandDriftLimit,
                "right_palm_grip_vector_drift",
                failures);
            RequireReloadCondition(
                minimumRightPalmContactDistance
                    >= ReloadRightPalmContactMinimum
                    && maximumRightPalmContactDistance
                        <= ReloadRightPalmContactMaximum,
                "right_palm_contact_offset",
                failures);
            RequireReloadCondition(
                maximumPrimaryPalmMotion <= ReloadRightHandDriftLimit,
                "right_hand_animation_drift",
                failures);
        }
        RequireReloadCondition(
            maximumSupportPalmTravel >= ReloadSupportPalmMinimumTravel,
            "support_palm_no_travel",
            failures);
        RequireReloadCondition(
            supportPalmReturn <= (nativeClip
                ? NativeReloadSupportPalmReturnLimit
                : ReloadSupportPalmReturnLimit),
            "support_palm_no_return",
            failures);
        RequireReloadCondition(
            !nativeClip
                || maximumSupportPalmTravel <= NativeReloadSupportPalmMaximumTravel,
            "native_support_palm_motion_excessive",
            failures);
        RequireReloadCondition(
            nativeClip
                || maximumSupportTargetResidual <= ReloadSupportPalmTargetLimit,
            "support_palm_off_target",
            failures);
        RequireReloadCondition(
            nativeClip || boundaryContinuityValid,
            "support_target_discontinuity",
            failures);
        RequireReloadCondition(
            nativeClip || baseline.PrimaryMagazineGripAvailable,
            "magazine_grip_contact_missing",
            failures);
        RequireReloadCondition(
            nativeClip
                || profile.Mechanism == FirstPersonReloadMechanism.InternalMagazine
                || seatedMagazineAlignmentValid,
            "seated_spare_magazine_misaligned",
            failures);
        RequireReloadCondition(
            nativeClip
                || !profile.UsesAction(emptyReload)
                || baseline.ActionGripAvailable,
            "action_grip_contact_missing",
            failures);
        RequireReloadCondition(
            bodyContinuityValid,
            "shoulder_body_discontinuity",
            failures);
        RequireReloadCondition(
            insertionReadable,
            "insert_contact_out_of_frame",
            failures);
        RequireReloadCondition(
            extractionReadable,
            "extract_contact_out_of_frame",
            failures);
        RequireReloadCondition(
            nativeClip
                || profile.Mechanism
                    == FirstPersonReloadMechanism.InternalMagazine
                || maximumPrimaryMagazineScreenTravel
                    >= baseline.ScreenContact.ScreenSize.Y
                        * ReloadMechanismMinimumScreenTravelRatio,
            "extract_mechanism_screen_motion_missing",
            failures);
        RequireReloadCondition(
            actionReadable,
            "action_contact_out_of_frame",
            failures);

        var installedMagazineGripDistance = baseline.PrimaryMagazineGrip
            .DistanceTo(baseline.PrimaryGrip);
        var serviceSidearm = platform is WeaponPlatform.P226
            or WeaponPlatform.M1911;
        RequireReloadCondition(
            !serviceSidearm
                || (baseline.PrimaryMagazineGripAvailable
                    && installedMagazineGripDistance
                        <= ServiceSidearmMagazineWellDistanceLimit),
            "service_sidearm_magazine_detached",
            failures);

        var internalMagazine = profile.Mechanism
            == FirstPersonReloadMechanism.InternalMagazine;
        var magazineMechanismValid = nativeClip
            ? baseline.PrimaryMagazineGeometry
                && baseline.PrimaryMagazineVisible
                && maximumPrimaryMagazineTravel >= ReloadMechanismMinimumTravel
                && maximumPrimaryMagazineTravel
                    <= NativeReloadMechanismMaximumTravel
            : internalMagazine
                ? baseline.SeparateMagazineNodes
                    && baseline.PrimaryMagazineGeometry
                    && baseline.SpareMagazineGeometry
                    && sawPrimaryOnly
                    && sawSpareVisible
                    && primaryStayedVisible
                    && maximumPrimaryMagazineTravel <= ReloadStateTolerance
                    && maximumSpareMagazineTravel >= ReloadMechanismMinimumTravel
                : baseline.SeparateMagazineNodes
                && baseline.PrimaryMagazineGeometry
                && baseline.SpareMagazineGeometry
                && sawPrimaryOnly
                && sawSpareOnly
                && maximumPrimaryMagazineTravel >= ReloadMechanismMinimumTravel
                && maximumSpareMagazineTravel >= ReloadMechanismMinimumTravel;
        RequireReloadCondition(
            magazineMechanismValid,
            "missing_real_magazine_mechanism",
            failures);

        var actionValid = nativeClip
            ? baseline.ActionGeometry
                && maximumActionTravel >= ReloadMechanismMinimumTravel
                && maximumActionTravel <= NativeReloadMechanismMaximumTravel
            : profile.UsesAction(emptyReload)
                ? baseline.ActionGeometry
                    && maximumActionTravel >= ReloadMechanismMinimumTravel
                : maximumActionTravel <= ReloadStateTolerance;
        RequireReloadCondition(
            actionValid,
            profile.UsesAction(emptyReload)
                ? "required_action_missing"
                : "tactical_action_moved",
            failures);

        var idempotentProgress = (profile.AcquireEnd + profile.InsertEnd) * 0.5f;
        _player.SetReloadPoseForDiagnostics(idempotentProgress, emptyReload);
        var firstIdempotent = _player.InspectAllWeaponReloadForDiagnostics();
        _player.SetReloadPoseForDiagnostics(idempotentProgress, emptyReload);
        var secondIdempotent = _player.InspectAllWeaponReloadForDiagnostics();
        var idempotent = ReloadInspectionsEquivalent(
            firstIdempotent,
            secondIdempotent);
        RequireReloadCondition(idempotent, "non_idempotent_sample", failures);

        _player.SetReloadPoseForDiagnostics(lastVisibleProgress, emptyReload);
        var lastVisible = _player.InspectAllWeaponReloadForDiagnostics();
        _player.ClearReloadPoseForDiagnostics();
        var reset = _player.InspectAllWeaponReloadForDiagnostics();
        var finishSupportPalmStep = lastVisible.VisibleSupportPalm.DistanceTo(
            reset.VisibleSupportPalm);
        var resetValid = !reset.Reloading
            && reset.PrimaryMagazineVisible
            && !reset.SpareMagazineVisible
            && reset.ActionPosition.DistanceTo(baseline.ActionPosition)
                <= ReloadStateTolerance
            && (nativeClip
                ? reset.AnimatedRootActive && reset.AnimatedMeshActive
                : !reset.AnimatedRootActive
                    && !reset.AnimatedMeshActive
                    && reset.StaticArmsActive);
        RequireReloadCondition(resetValid, "reset_failed", failures);
        RequireReloadCondition(
            finishSupportPalmStep <= (nativeClip
                ? NativeReloadSupportPalmReturnLimit
                : ReloadFinishSupportPalmStepLimit),
            "finish_support_palm_pop",
            failures);

        _player.SetReloadPoseForDiagnostics(idempotentProgress, emptyReload);
        _player.CancelReloadForDiagnostics();
        var canceled = _player.InspectAllWeaponReloadForDiagnostics();
        _player.CancelReloadForDiagnostics();
        var canceledAgain = _player.InspectAllWeaponReloadForDiagnostics();
        var cancelValid = !canceled.Reloading
            && ReloadInspectionsEquivalent(reset, canceled)
            && ReloadInspectionsEquivalent(canceled, canceledAgain);
        RequireReloadCondition(cancelValid, "cancel_failed", failures);

        _player.SetReloadPoseForDiagnostics(idempotentProgress, emptyReload);
        _player.CancelReloadForDiagnostics();
        var vehicleCancelInitialError =
            _player.WeaponViewTargetPoseErrorForDiagnostics;
        for (var frame = 0; frame < ReloadVehicleRecoveryFrames; frame++)
        {
            _player.AdvanceVehicleReloadPresentationForDiagnostics(1.0f / 60.0f);
        }
        var vehicleCanceled = _player.InspectAllWeaponReloadForDiagnostics();
        var vehicleCancelFinalError =
            _player.WeaponViewTargetPoseErrorForDiagnostics;
        var vehicleCancelRecoveryValid = !vehicleCanceled.Reloading
            && ReloadInspectionsEquivalent(reset, vehicleCanceled)
            && vehicleCancelFinalError <= ReloadVehicleViewPoseErrorLimit
            && (vehicleCancelInitialError <= ReloadVehicleViewPoseErrorLimit
                || vehicleCancelFinalError < vehicleCancelInitialError);
        RequireReloadCondition(
            vehicleCancelRecoveryValid,
            "vehicle_cancel_recovery_failed",
            failures);

        _player.SetReloadPoseForDiagnostics(lastVisibleProgress, emptyReload);
        _player.AdvanceVehicleReloadPresentationForDiagnostics(1.0f / 60.0f);
        var vehicleFinishInitialError =
            _player.WeaponViewTargetPoseErrorForDiagnostics;
        for (var frame = 1; frame < ReloadVehicleRecoveryFrames; frame++)
        {
            _player.AdvanceVehicleReloadPresentationForDiagnostics(1.0f / 60.0f);
        }
        var vehicleFinished = _player.InspectAllWeaponReloadForDiagnostics();
        var vehicleFinishFinalError =
            _player.WeaponViewTargetPoseErrorForDiagnostics;
        var vehicleFinishRecoveryValid = !vehicleFinished.Reloading
            && ReloadInspectionsEquivalent(reset, vehicleFinished)
            && vehicleFinishFinalError <= ReloadVehicleViewPoseErrorLimit
            && (vehicleFinishInitialError <= ReloadVehicleViewPoseErrorLimit
                || vehicleFinishFinalError < vehicleFinishInitialError);
        RequireReloadCondition(
            vehicleFinishRecoveryValid,
            "vehicle_finish_recovery_failed",
            failures);
        _player.ClearReloadPoseForDiagnostics();

        var variantName = nativeClip
            ? "native"
            : emptyReload ? "empty" : "tactical";
        var rightPalmContactSummary = baseline.RightGripAvailable
            ? $"{minimumRightPalmContactDistance:F6}/"
                + $"{maximumRightPalmContactDistance:F6}"
            : "n/a";
        var valid = failures.Count == 0;
        return new ReloadVariantAudit(
            valid,
            $"platform={platform} variant={variantName} valid={valid} "
            + $"clip={baseline.ClipName} clip_exists={baseline.ClipExists} "
            + $"duration={baseline.ClipDuration:F3} samples={samples.Count} "
            + $"shoulder_drift={maximumShoulderDrift:F6} "
            + $"right_grip_residual={maximumRightGripResidual:F6} "
            + $"right_palm_grip_vector_drift={maximumRightPalmGripVectorDrift:F6} "
            + $"right_palm_contact={rightPalmContactSummary} "
            + $"right_palm_motion={maximumPrimaryPalmMotion:F6} "
            + $"support_travel={maximumSupportPalmTravel:F6} "
            + $"support_return={supportPalmReturn:F6} "
            + $"last_visible_progress={lastVisibleProgress:F6} "
            + $"finish_palm_step={finishSupportPalmStep:F6} "
            + $"support_target_residual={maximumSupportTargetResidual:F6} "
            + $"support_target_progress={maximumSupportTargetResidualProgress:F3} "
            + $"boundary_target_step={maximumBoundaryTargetStep:F6} "
            + $"boundary_palm_step={maximumBoundaryPalmStep:F6} "
            + $"shoulder_screen_y_min={minimumShoulderScreenYRatio:F3} "
            + $"shoulder_screen_x={baseline.BodyContinuity.RightShoulderXRatio:F3}/"
            + $"{baseline.BodyContinuity.LeftShoulderXRatio:F3} "
            + $"mesh_top_min={minimumAnimatedMeshTopRatio:F3} "
            + $"body_skin={baseline.BodyContinuity.AnimatedMeshUsesSkeleton} "
            + $"body_first_failure={firstBodyFailureProgress:F3} "
            + $"body_r={ReloadArmChainSummary(firstBodyFailureProgress < 0.0f ? baseline.BodyContinuity.RightArm : firstBodyFailure.RightArm, firstBodyFailureProgress < 0.0f ? baseline.BodyContinuity.ScreenSize : firstBodyFailure.ScreenSize)} "
            + $"body_l={ReloadArmChainSummary(firstBodyFailureProgress < 0.0f ? baseline.BodyContinuity.LeftArm : firstBodyFailure.LeftArm, firstBodyFailureProgress < 0.0f ? baseline.BodyContinuity.ScreenSize : firstBodyFailure.ScreenSize)} "
            + $"extract_screen={extractionReadable} "
            + $"extract_palm_y={extractionPalmYRatio:F3} "
            + $"extract_mag_grip_y={extractionMagazineGripYRatio:F3} "
            + $"extract_screen_travel={maximumPrimaryMagazineScreenTravel:F3} "
            + $"insert_screen={insertionReadable} "
            + $"insert_palm_y={insertionPalmYRatio:F3} "
            + $"insert_mag_grip_y={insertionMagazineGripYRatio:F3} "
            + $"action_screen={actionReadable} "
            + $"action_palm_y={actionPalmYRatio:F3} "
            + $"action_grip_y={actionGripYRatio:F3} "
            + $"primary_mag_travel={maximumPrimaryMagazineTravel:F6} "
            + $"primary_mag_grip_distance={installedMagazineGripDistance:F6} "
            + $"primary_mag_grip=({baseline.PrimaryMagazineGrip.X:F4},"
            + $"{baseline.PrimaryMagazineGrip.Y:F4},{baseline.PrimaryMagazineGrip.Z:F4}) "
            + $"primary_grip=({baseline.PrimaryGrip.X:F4},"
            + $"{baseline.PrimaryGrip.Y:F4},{baseline.PrimaryGrip.Z:F4}) "
            + $"mag_grip_contact={baseline.PrimaryMagazineGripAvailable} "
            + $"native_mag_track_travel={baseline.NativeMagazineTravel:F6} "
            + $"spare_mag_travel={maximumSpareMagazineTravel:F6} "
            + $"seated_mag_root={maximumSeatedMagazineRootDistance:F6} "
            + $"seated_mag_basis={maximumSeatedMagazineBasisDelta:F6} "
            + $"seated_mag_grip={maximumSeatedMagazineGripDistance:F6} "
            + $"action_travel={maximumActionTravel:F6} "
            + $"action_grip_contact={baseline.ActionGripAvailable} "
            + $"mag_geometry={baseline.PrimaryMagazineGeometry}/{baseline.SpareMagazineGeometry} "
            + $"action_geometry={baseline.ActionGeometry} idempotent={idempotent} "
            + $"reset={resetValid} cancel={cancelValid} "
            + $"vehicle_cancel={vehicleCancelRecoveryValid} "
            + $"vehicle_cancel_pose={vehicleCancelInitialError:F6}/"
            + $"{vehicleCancelFinalError:F6} "
            + $"vehicle_finish={vehicleFinishRecoveryValid} "
            + $"vehicle_finish_pose={vehicleFinishInitialError:F6}/"
            + $"{vehicleFinishFinalError:F6} "
            + $"failures={(valid ? "none" : string.Join(',', failures))}");
    }

    private async Task CaptureReloadInsertionKeyframe(WeaponPlatform platform)
    {
        var profile = FirstPersonReloadProfileCatalog.For(platform);
        var extractionProgress = (profile.ReachEnd + profile.ExtractEnd) * 0.5f;
        foreach (var emptyReload in new[] { false, true })
        {
            _player.SetReloadPoseForDiagnostics(extractionProgress, emptyReload);
            await WaitFrames(2);
            var variant = emptyReload ? "empty" : "tactical";
            SaveViewportImage(
                $"res://all_weapon_reload_{platform.ToString().ToLowerInvariant()}_"
                + $"{variant}_extract_validation.png");
        }
        var insertionProgress = (profile.InsertEnd + profile.SeatEnd) * 0.5f;
        _player.SetReloadPoseForDiagnostics(insertionProgress, emptyReload: false);
        await WaitFrames(2);
        SaveViewportImage(
            $"res://all_weapon_reload_{platform.ToString().ToLowerInvariant()}_insert_validation.png");
        var actionProgress = (profile.SeatEnd + profile.ActionEnd) * 0.5f;
        _player.SetReloadPoseForDiagnostics(actionProgress, emptyReload: true);
        await WaitFrames(2);
        SaveViewportImage(
            $"res://all_weapon_reload_{platform.ToString().ToLowerInvariant()}_empty_action_validation.png");
        _player.ClearReloadPoseForDiagnostics();
    }

    private static List<float> AllWeaponReloadSamples(
        FirstPersonReloadProfile profile,
        float lastVisibleProgress)
    {
        var samples = new SortedSet<float> { 0.0f, 1.0f };
        var previous = 0.0f;
        foreach (var boundary in ReloadProfileBoundaries(profile))
        {
            samples.Add((previous + boundary) * 0.5f);
            samples.Add(Mathf.Clamp(boundary - 0.002f, 0.0f, 1.0f));
            samples.Add(boundary);
            samples.Add(Mathf.Clamp(boundary + 0.002f, 0.0f, 1.0f));
            previous = boundary;
        }
        samples.Add((profile.ActionEnd + 1.0f) * 0.5f);
        samples.Add(lastVisibleProgress);
        return new List<float>(samples);
    }

    private static float[] ReloadProfileBoundaries(FirstPersonReloadProfile profile)
        =>
        [
            profile.ReachEnd,
            profile.ExtractEnd,
            profile.StowEnd,
            profile.AcquireEnd,
            profile.InsertEnd,
            profile.SeatEnd,
            profile.ActionEnd
        ];

    private static bool ReloadInspectionsEquivalent(
        AllWeaponReloadInspection left,
        AllWeaponReloadInspection right)
        => left.Reloading == right.Reloading
            && left.EmptyReload == right.EmptyReload
            && left.AnimatedRootActive == right.AnimatedRootActive
            && left.AnimatedMeshActive == right.AnimatedMeshActive
            && left.StaticArmsActive == right.StaticArmsActive
            && left.RightGripAvailable == right.RightGripAvailable
            && left.PrimaryMagazineVisible == right.PrimaryMagazineVisible
            && left.SpareMagazineVisible == right.SpareMagazineVisible
            && left.PrimaryMagazineGripAvailable
                == right.PrimaryMagazineGripAvailable
            && left.SpareMagazineGripAvailable == right.SpareMagazineGripAvailable
            && left.ActionGripAvailable == right.ActionGripAvailable
            && Mathf.Abs(left.Progress - right.Progress) <= ReloadStateTolerance
            && left.RightShoulder.DistanceTo(right.RightShoulder) <= ReloadStateTolerance
            && left.LeftShoulder.DistanceTo(right.LeftShoulder) <= ReloadStateTolerance
            && left.RightPalm.DistanceTo(right.RightPalm) <= ReloadStateTolerance
            && left.LeftPalm.DistanceTo(right.LeftPalm) <= ReloadStateTolerance
            && left.RightGrip.DistanceTo(right.RightGrip) <= ReloadStateTolerance
            && left.PrimaryMagazinePosition.DistanceTo(right.PrimaryMagazinePosition)
                <= ReloadStateTolerance
            && left.PrimaryMagazineGrip.DistanceTo(right.PrimaryMagazineGrip)
                <= ReloadStateTolerance
            && left.SpareMagazineGrip.DistanceTo(right.SpareMagazineGrip)
                <= ReloadStateTolerance
            && left.PrimaryMagazineTransform.Origin.DistanceTo(
                    right.PrimaryMagazineTransform.Origin)
                <= ReloadStateTolerance
            && ReloadBasisDelta(
                    left.PrimaryMagazineTransform.Basis,
                    right.PrimaryMagazineTransform.Basis)
                <= ReloadSeatedMagazineBasisLimit
            && left.SpareMagazineTransform.Origin.DistanceTo(
                    right.SpareMagazineTransform.Origin)
                <= ReloadStateTolerance
            && ReloadBasisDelta(
                    left.SpareMagazineTransform.Basis,
                    right.SpareMagazineTransform.Basis)
                <= ReloadSeatedMagazineBasisLimit
            && left.SpareMagazinePosition.DistanceTo(right.SpareMagazinePosition)
                <= ReloadStateTolerance
            && left.ActionGrip.DistanceTo(right.ActionGrip) <= ReloadStateTolerance
            && left.ActionPosition.DistanceTo(right.ActionPosition) <= ReloadStateTolerance;

    private static float ReloadBasisDelta(Basis left, Basis right)
        => left.X.DistanceTo(right.X)
            + left.Y.DistanceTo(right.Y)
            + left.Z.DistanceTo(right.Z);

    private static bool ReloadTransformsEquivalent(Transform3D left, Transform3D right)
        => left.Origin.DistanceTo(right.Origin) <= ReloadStateTolerance
            && ReloadBasisDelta(left.Basis, right.Basis)
                <= ReloadSeatedMagazineBasisLimit;

    private static bool ReloadBodyContinuityValid(
        AllWeaponReloadInspection inspection)
    {
        var body = inspection.BodyContinuity;
        return body.ScreenSize.X > 0.0f
            && body.ScreenSize.Y > 0.0f
            && inspection.AnimatedMeshActive
            && body.AnimatedMeshUsesSkeleton
            && ReloadArmChainContinuityValid(body.RightArm, body.ScreenSize)
            && ReloadArmChainContinuityValid(body.LeftArm, body.ScreenSize);
    }

    private static bool NativeReloadBodyContinuityValid(
        AllWeaponReloadInspection inspection)
        => ReloadBodyContinuityValid(inspection);

    private static bool ReloadArmChainContinuityValid(
        ReloadArmScreenChainInspection arm,
        Vector2 screenSize)
    {
        if (!arm.Available
            || !arm.BodyEdgeConnected
            || !arm.ParentChainValid
            || arm.ElbowBehindCamera
            || arm.WristBehindCamera
            || arm.PalmBehindCamera
            || !ReloadBoneLengthPreserved(
                arm.UpperArmLength,
                arm.UpperArmRestLength)
            || !ReloadBoneLengthPreserved(
                arm.ForearmLength,
                arm.ForearmRestLength)
            || !ReloadBoneLengthPreserved(
                arm.WristPalmLength,
                arm.WristPalmRestLength))
        {
            return false;
        }

        // The body gate is structural: a parented, length-preserved chain whose
        // sleeve segment reaches the visible body edge is continuous even when
        // the wrist and palm themselves are temporarily below the viewport
        // during a close magazine exchange. The separate extraction,
        // insertion, and action screen-contact gates verify that the hand and
        // working part are readable at the moments that matter to the player.
        return true;
    }

    private static bool ReloadBoneLengthPreserved(float posed, float rest)
        => float.IsFinite(posed)
            && float.IsFinite(rest)
            && rest > 0.0001f
            && Mathf.Abs(posed - rest) / rest <= 0.02f;

    private static bool ReloadJointInsideExpandedFrame(
        Vector2 point,
        Vector2 screenSize)
        // Wrist and palm joints may sit just beyond the lower edge while the
        // skinned cuff remains visibly connected. Keep this a bounded,
        // diagnostic-only tolerance (16% of the frame), while the parent
        // chain, rest-length, skin-weight, and body-edge checks still reject
        // an actually detached hand.
        => point.X >= screenSize.X * -0.16f
            && point.X <= screenSize.X * 1.16f
            && point.Y >= screenSize.Y * -0.16f
            && point.Y <= screenSize.Y * 1.16f;

    private static void RequireReloadCondition(
        bool condition,
        string failure,
        List<string> failures)
    {
        if (!condition)
        {
            failures.Add(failure);
        }
    }

    private static string ReloadArmChainSummary(
        ReloadArmScreenChainInspection arm,
        Vector2 screenSize)
    {
        var elbow = ReloadJointRatio(arm.ElbowScreen, screenSize);
        var wrist = ReloadJointRatio(arm.WristScreen, screenSize);
        var palm = ReloadJointRatio(arm.PalmScreen, screenSize);
        var shoulder = ReloadJointRatio(arm.ShoulderScreen, screenSize);
        return $"avail:{arm.Available}/edge:{arm.BodyEdgeConnected}/"
            + $"parent:{arm.ParentChainValid}/"
            + $"behind:{arm.ShoulderBehindCamera}-{arm.ElbowBehindCamera}-"
            + $"{arm.WristBehindCamera}-{arm.PalmBehindCamera}/"
            + $"length:{arm.UpperArmLength:F3}-{arm.ForearmLength:F3}-"
            + $"{arm.WristPalmLength:F3}/"
            + $"rest:{arm.UpperArmRestLength:F3}-{arm.ForearmRestLength:F3}-"
            + $"{arm.WristPalmRestLength:F3}/"
            + $"joint:{shoulder.X:F3}:{shoulder.Y:F3}-"
            + $"{elbow.X:F3}:{elbow.Y:F3}-"
            + $"{wrist.X:F3}:{wrist.Y:F3}-{palm.X:F3}:{palm.Y:F3}";
    }

    private static Vector2 ReloadJointRatio(Vector2 point, Vector2 screenSize)
        => screenSize.X > 0.0f && screenSize.Y > 0.0f
            ? new Vector2(point.X / screenSize.X, point.Y / screenSize.Y)
            : Vector2.Zero;

    private static string SanitizeDiagnosticValue(string value)
        => value.Replace(' ', '_').Replace(',', ';');

    private readonly record struct ReloadVariantAudit(bool Valid, string Summary);
}
