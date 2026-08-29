using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private async void ValidateHandDiagnostics(bool narrow = false, bool ultrawide = false)
    {
        var requestedWindowSize = ultrawide
            ? new Vector2I(2048, 621)
            : narrow
                ? new Vector2I(985, 847)
                : new Vector2I(1280, 720);
        // The headless display driver otherwise exposes a 64x64 or zero-sized
        // surface, making every screen-projection assertion fail without ever
        // exercising the real pose. Give all variants a deterministic render
        // target; narrow and ultrawide still retain their distinct aspect gates.
        var window = GetWindow();
        window.ContentScaleMode = Window.ContentScaleModeEnum.CanvasItems;
        window.ContentScaleAspect = Window.ContentScaleAspectEnum.Ignore;
        window.Size = requestedWindowSize;
        await WaitFrames(4);
        var layoutValid = GetWindow().Size == requestedWindowSize;
        Input.ActionRelease("aim");
        // Move player to open area away from walls for clear view
        _player.GlobalPosition = new Vector3(0, 0.2f, 40.0f);
        _player.Velocity = Vector3.Zero;
        _player.FaceWorldPointForDiagnostics(new Vector3(0, 0.2f, -40.0f));
        foreach (var enemy in _enemies) if (IsInstanceValid(enemy)) enemy.ProcessMode = ProcessModeEnum.Disabled;
        foreach (var mate in _squadMates) if (IsInstanceValid(mate)) mate.GlobalPosition = new Vector3(240, 80, 240);
        await WaitFrames(6);

        var results = new List<string>();
        var posesValid = true;
        var authoredRigValid = true;
        var servicePistolCorrectionValid = true;
        var sidearmPresentationValid = true;
        var longGunPresentationValid = true;
        var platforms = Enum.GetValues<WeaponPlatform>();
        var proceduralArms = _player.GetNodeOrNull<Node3D>(
            "Camera3D/WeaponRoot/ProceduralFirstPersonArms");
        foreach (var platform in platforms)
        {
            _player.GrantFireablePrimaryForDiagnostics(WeaponCatalog.Build(platform, 0));
            _player.SetAimingPoseForDiagnostics(false);
            await WaitFrames(4);
            var proceduralVisible = IsInstanceValid(proceduralArms)
                && proceduralArms.Visible;
            var weaponVisible = _player.UsesAuthoredWeaponPlatformForDiagnostics(platform);
            posesValid &= !proceduralVisible && weaponVisible;
            if (platform == WeaponPlatform.M3A1)
            {
                var nativeRigValid = _player.WeaponHandPoseValidForDiagnostics;
                posesValid &= nativeRigValid;
                results.Add(
                    $"{platform}: nativeRig={nativeRigValid} "
                    + $"proceduralVisible={proceduralVisible} weaponVisible={weaponVisible}");
                continue;
            }

            var handInspection = _player.InspectAuthoredHandPoseForDiagnostics();
            var arms = _player.ActiveAuthoredArmsForDiagnostics;
            if (arms is null || !IsInstanceValid(arms.Root))
            {
                posesValid = false;
                authoredRigValid = false;
                results.Add($"{platform}: authoredRig=false weaponVisible={weaponVisible}");
                continue;
            }

            var rootVisible = arms.Root.IsVisibleInTree();
            var rightVisible = arms.RightArm.IsVisibleInTree();
            var leftVisible = arms.LeftArm.IsVisibleInTree();
            authoredRigValid &= rootVisible
                && rightVisible
                && leftVisible
                && _player.UsesAuthoredHandRigForDiagnostics;
            var realigned = _player.RealignAuthoredHandsForDiagnostics();
            var idempotent = handInspection.RootTransform.Origin.DistanceTo(realigned.Origin) <= 0.0001f
                && handInspection.RootTransform.Basis.X.DistanceTo(realigned.Basis.X) <= 0.0001f
                && handInspection.RootTransform.Basis.Y.DistanceTo(realigned.Basis.Y) <= 0.0001f
                && handInspection.RootTransform.Basis.Z.DistanceTo(realigned.Basis.Z) <= 0.0001f;
            posesValid &= handInspection.Valid && idempotent;
            if (platform is WeaponPlatform.P226 or WeaponPlatform.M1911 or WeaponPlatform.GSh18)
            {
                servicePistolCorrectionValid &= handInspection.SupportArmCorrection
                    <= TacticalPlayer.MaxServicePistolSupportArmCorrection;
            }

            var weaponRootInverse = _player.WeaponRootGlobalTransformForDiagnostics.AffineInverse();
            var weaponRoot = _player.ActiveAuthoredWeaponRootForDiagnostics;
            var weaponBounds = weaponRoot is not null
                ? AuthoredArmWorldBounds(weaponRoot)
                : new Aabb();
            var foregrip = _player.ActiveAuthoredForegripForDiagnostics;
            var muzzle = _player.ActiveAuthoredMuzzleForDiagnostics;
            var rightPalmLocal = weaponRootInverse * handInspection.RightPalm;
            var leftPalmLocal = weaponRootInverse * handInspection.LeftPalm;
            var rightGripLocal = weaponRootInverse * arms.RightGripFrame.GlobalPosition;
            var leftGripLocal = weaponRootInverse * arms.LeftGripFrame.GlobalPosition;
            var foregripLocal = foregrip is not null
                ? weaponRootInverse * foregrip.GlobalPosition
                : Vector3.Zero;
            var muzzleLocal = muzzle is not null
                ? weaponRootInverse * muzzle.GlobalPosition
                : Vector3.Zero;
            var sidearmHip = default(SidearmPresentationInspection);
            var sidearmAds = default(SidearmPresentationInspection);
            var weaponPresentation = _player.InspectWeaponPresentationForDiagnostics();
            var screenSize = new Vector2(GetWindow().Size.X, GetWindow().Size.Y);
            var weaponScreenWidthRatio = screenSize.X > 0.0f
                ? weaponPresentation.Bounds.Size.X / screenSize.X
                : 0.0f;
            var weaponScreenHeightRatio = screenSize.Y > 0.0f
                ? weaponPresentation.Bounds.Size.Y / screenSize.Y
                : 0.0f;
            var weaponScreenAreaRatio = screenSize.X > 0.0f && screenSize.Y > 0.0f
                ? weaponPresentation.Bounds.Size.X * weaponPresentation.Bounds.Size.Y
                    / (screenSize.X * screenSize.Y)
                : 0.0f;
            var longGunReadable = true;
            if (WeaponCatalog.IsSidearm(platform))
            {
                sidearmHip = _player.InspectSidearmPresentationForDiagnostics();
                Input.ActionPress("aim");
                _player.SetAimingPoseForDiagnostics(true);
                await WaitFrames(4);
                sidearmAds = _player.InspectSidearmPresentationForDiagnostics();
                Input.ActionRelease("aim");
                _player.SetAimingPoseForDiagnostics(false);
                await WaitFrames(2);
                sidearmPresentationValid &= sidearmHip.Valid && sidearmAds.Valid;
            }
            else
            {
                longGunReadable = weaponPresentation.Available
                    && weaponPresentation.ProjectedVertexCount >= 100
                    && weaponScreenWidthRatio >= 0.13f
                    && weaponScreenHeightRatio >= 0.34f
                    && weaponScreenAreaRatio >= 0.05f;
                longGunPresentationValid &= longGunReadable;
            }
            if (!narrow
                && !ultrawide
                && platform is WeaponPlatform.AK74 or WeaponPlatform.ScarL)
            {
                SaveViewportImage($"res://weapon_{platform.ToString().ToLowerInvariant()}_validation.png");
            }
            results.Add(
                $"{platform}: handValid={handInspection.Valid} idempotent={idempotent} "
                + $"proceduralVisible={proceduralVisible} authoredVisible=({rootVisible},{rightVisible},{leftVisible}) "
                + $"weaponVisible={weaponVisible} gripResidual=({handInspection.GripResidual:F4},{handInspection.SupportGripResidual:F4}) "
                + $"supportArmCorrection={handInspection.SupportArmCorrection:F4} "
                + $"surfaceDistance=({handInspection.PrimarySurfaceDistance:F4},{handInspection.SupportSurfaceDistance:F4}) "
                + $"surfaceOffset=({handInspection.PrimarySurfaceOffset},{handInspection.SupportSurfaceOffset}) "
                + $"palms=({rightPalmLocal},{leftPalmLocal}) grips=({rightGripLocal},{leftGripLocal}) "
                + $"foregrip={foregripLocal} muzzle={muzzleLocal} weaponBounds=({weaponBounds.Position},{weaponBounds.End}) "
                + $"weaponScreen=({weaponPresentation.Bounds.Position},{weaponPresentation.Bounds.End}) "
                + $"weaponScreenWidthH={weaponPresentation.WidthViewportHeights(GetWindow().Size.Y):F4} "
                + $"weaponScreenHeightH={weaponPresentation.HeightViewportHeights(GetWindow().Size.Y):F4} "
                + $"weaponScreenAreaH2={weaponPresentation.AreaViewportHeightsSquared(GetWindow().Size.Y):F4} "
                + $"weaponScreenWidthRatio={weaponScreenWidthRatio:F4} "
                + $"weaponScreenHeightRatio={weaponScreenHeightRatio:F4} "
                + $"weaponScreenAreaRatio={weaponScreenAreaRatio:F4} "
                + $"weaponScreenValid={longGunReadable} "
                + SidearmScreenMetrics("sidearm_hip", sidearmHip)
                + SidearmScreenMetrics("sidearm_ads", sidearmAds));
        }
        _player.GrantFireablePrimaryForDiagnostics(
            WeaponCatalog.Build(WeaponPlatform.M3A1, 0));
        await WaitFrames(8);
        var reloadPoseSet = _player.SetReloadPoseForDiagnostics(0.46f);
        await WaitFrames(4);
        var smgArmBounds = _player.SmgArmBoundsSizeForDiagnostics;
        var smgWeaponBounds = _player.SmgWeaponBoundsSizeForDiagnostics;
        // Z is the sleeve reach and Y captures its camera-facing depth after
        // Godot's Y-up conversion. The first correction reached 0.0315 by
        // stretching a thin open cut; the full upper-arm continuation must
        // remain both longer and materially deeper than that profile.
        var smgSleeveReach = smgArmBounds.Z >= 0.04f;
        var smgSleeveVolume = smgArmBounds.Y >= 0.0069f;
        var smgWeaponSize = smgWeaponBounds.Z >= 1.22f;
        var smgReloadPresentation = reloadPoseSet
            && _player.SmgReloadPresentationValidForDiagnostics;
        _player.ClearReloadPoseForDiagnostics();

        _player.GrantFireablePrimaryForDiagnostics(
            WeaponCatalog.Build(WeaponPlatform.AK74, 0));
        await WaitFrames(8);
        var akIdle = _player.InspectAuthoredPlatformReloadForDiagnostics();
        var akReloadPoseSet = true;
        var akReloadArmsActive = true;
        var akReloadPrimaryGripFixed = true;
        var akReloadSupportTracks = true;
        var akReloadMechanismState = true;
        var akReloadLeftArmMoves = true;
        var akReloadSleevesContinuous = true;
        var akReloadSleevesAtBottom = true;
        var akReloadViewStable = true;
        var akReloadIdempotent = true;
        var akReloadMaximumGripResidual = 0.0f;
        var akReloadMaximumSupportDistance = 0.0f;
        var akReloadMaximumMagazineDistance = 0.0f;
        var akReloadMinimumLeftArmMotion = float.PositiveInfinity;
        var akReloadSampleCount = 0;
        foreach (var progress in new[] { 0.28f, 0.44f, 0.64f })
        {
            var poseSet = _player.SetReloadPoseForDiagnostics(progress);
            var first = _player.InspectAuthoredPlatformReloadForDiagnostics();
            var repeatedPoseSet = _player.SetReloadPoseForDiagnostics(progress);
            var repeated = _player.InspectAuthoredPlatformReloadForDiagnostics();
            var primaryGripFixed = first.RightGripResidual <= 0.004f
                && repeated.RightGripResidual <= 0.004f
                && first.RightGrip.DistanceTo(akIdle.RightGrip) <= 0.002f
                && HandTransformsMatch(
                    akIdle.RightArmTransform,
                    first.RightArmTransform);
            var supportTracks = first.SupportTargetDistance <= 0.002f
                && repeated.SupportTargetDistance <= 0.002f
                && first.ActiveMagazineDistance <= 0.11f
                && repeated.ActiveMagazineDistance <= 0.11f;
            var expectedPrimaryVisible = progress < 0.50f;
            var mechanismState = first.PrimaryMagazineVisible == expectedPrimaryVisible
                && first.SpareMagazineVisible == !expectedPrimaryVisible
                && first.SeparateMagazineNodes
                && first.VisibleMagazineMechanism
                && first.PrimaryMagazinePosition.DistanceTo(first.SpareMagazinePosition) >= 0.03f;
            var leftArmMotion = HandBasisDelta(
                    akIdle.LeftArmTransform.Basis,
                    first.LeftArmTransform.Basis)
                + akIdle.LeftArmTransform.Origin.DistanceTo(
                    first.LeftArmTransform.Origin);
            var sleevesContinuous = first.LeftSleeveWristLength is >= 0.045f and <= 0.28f
                && first.RightSleeveWristLength is >= 0.045f and <= 0.28f
                && Mathf.Abs(first.LeftSleeveWristLength - akIdle.LeftSleeveWristLength) <= 0.001f
                && Mathf.Abs(first.RightSleeveWristLength - akIdle.RightSleeveWristLength) <= 0.001f;
            var viewStable = first.ReloadViewTarget.DistanceTo(
                    new Vector3(0.34f, -0.30f, -0.68f)) <= 0.001f
                && first.ReloadRotationTarget.Length() <= 0.001f;
            var idempotent = HandTransformsMatch(
                    first.RightArmTransform,
                    repeated.RightArmTransform)
                && HandTransformsMatch(
                    first.LeftArmTransform,
                    repeated.LeftArmTransform)
                && first.RightGrip.DistanceTo(repeated.RightGrip) <= 0.0001f
                && first.LeftGrip.DistanceTo(repeated.LeftGrip) <= 0.0001f
                && first.SupportTarget.DistanceTo(repeated.SupportTarget) <= 0.0001f
                && first.PrimaryMagazinePosition.DistanceTo(
                    repeated.PrimaryMagazinePosition) <= 0.0001f
                && first.SpareMagazinePosition.DistanceTo(
                    repeated.SpareMagazinePosition) <= 0.0001f;
            var sampleValid = poseSet
                && repeatedPoseSet
                && first.AuthoredArmsActive
                && primaryGripFixed
                && supportTracks
                && mechanismState
                && leftArmMotion >= 0.05f
                && sleevesContinuous
                && first.SleevesReachFrameBottom
                && viewStable
                && idempotent;
            akReloadPoseSet &= poseSet && repeatedPoseSet;
            akReloadArmsActive &= first.AuthoredArmsActive;
            akReloadPrimaryGripFixed &= primaryGripFixed;
            akReloadSupportTracks &= supportTracks;
            akReloadMechanismState &= mechanismState;
            akReloadLeftArmMoves &= leftArmMotion >= 0.05f;
            akReloadSleevesContinuous &= sleevesContinuous;
            akReloadSleevesAtBottom &= first.SleevesReachFrameBottom;
            akReloadViewStable &= viewStable;
            akReloadIdempotent &= idempotent;
            akReloadMaximumGripResidual = Mathf.Max(
                akReloadMaximumGripResidual,
                first.RightGripResidual);
            akReloadMaximumSupportDistance = Mathf.Max(
                akReloadMaximumSupportDistance,
                first.SupportTargetDistance);
            akReloadMaximumMagazineDistance = Mathf.Max(
                akReloadMaximumMagazineDistance,
                first.ActiveMagazineDistance);
            akReloadMinimumLeftArmMotion = Mathf.Min(
                akReloadMinimumLeftArmMotion,
                leftArmMotion);
            akReloadSampleCount++;
            GD.Print(
                $"AK_RELOAD_ARM_SAMPLE progress={progress:F2} valid={sampleValid} "
                + $"authored_active={first.AuthoredArmsActive} "
                + $"right_grip_residual={first.RightGripResidual:F6} "
                + $"support_target_distance={first.SupportTargetDistance:F6} "
                + $"active_magazine_distance={first.ActiveMagazineDistance:F6} "
                + $"primary_magazine_visible={first.PrimaryMagazineVisible} "
                + $"spare_magazine_visible={first.SpareMagazineVisible} "
                + $"visible_magazine_mechanism={first.VisibleMagazineMechanism} "
                + $"left_arm_motion={leftArmMotion:F6} "
                + $"sleeves_bottom={first.SleevesReachFrameBottom} "
                + $"view_stable={viewStable} idempotent={idempotent}");
        }
        _player.ClearReloadPoseForDiagnostics();
        var akReset = _player.InspectAuthoredPlatformReloadForDiagnostics();
        var akReloadReset = akReset.AuthoredArmsActive
            && akReset.PrimaryMagazineVisible
            && !akReset.SpareMagazineVisible
            && HandTransformsMatch(
                akIdle.RightArmTransform,
                akReset.RightArmTransform)
            && HandTransformsMatch(
                akIdle.LeftArmTransform,
                akReset.LeftArmTransform)
            && akIdle.RightGrip.DistanceTo(akReset.RightGrip) <= 0.0001f
            && akIdle.LeftGrip.DistanceTo(akReset.LeftGrip) <= 0.0001f;
        var akReloadValid = akIdle.AuthoredArmsActive
            && akReloadPoseSet
            && akReloadArmsActive
            && akReloadPrimaryGripFixed
            && akReloadSupportTracks
            && akReloadMechanismState
            && akReloadLeftArmMoves
            && akReloadSleevesContinuous
            && akReloadSleevesAtBottom
            && akReloadViewStable
            && akReloadIdempotent
            && akReloadReset
            && akReloadSampleCount == 3;
        GD.Print(
            $"AK_RELOAD_ARM_CHECK valid={akReloadValid} samples={akReloadSampleCount} "
            + $"pose_set={akReloadPoseSet} authored_active={akReloadArmsActive} "
            + $"primary_grip_fixed={akReloadPrimaryGripFixed} "
            + $"support_tracks={akReloadSupportTracks} "
            + $"mechanism_state={akReloadMechanismState} "
            + $"left_arm_moves={akReloadLeftArmMoves} "
            + $"sleeves_continuous={akReloadSleevesContinuous} "
            + $"sleeves_bottom={akReloadSleevesAtBottom} "
            + $"view_stable={akReloadViewStable} "
            + $"idempotent={akReloadIdempotent} reset={akReloadReset} "
            + $"max_grip_residual={akReloadMaximumGripResidual:F6} "
            + $"max_support_distance={akReloadMaximumSupportDistance:F6} "
            + $"max_magazine_distance={akReloadMaximumMagazineDistance:F6} "
            + $"min_left_arm_motion={akReloadMinimumLeftArmMotion:F6}");
        GD.Print($"AK_RELOAD_ARM_PASS valid={akReloadValid}");

        _player.GrantFireablePrimaryForDiagnostics(
            WeaponCatalog.Build(WeaponPlatform.M4A1, 0));
        await WaitFrames(8);
        var m4IdleArm = _player.InspectAuthoredM4ReloadArmForDiagnostics();
        var m4IdleHandValid = _player.InspectAuthoredHandPoseForDiagnostics().Valid;
        var m4ReloadPoseSet = true;
        var m4ReloadAuthoredArmActive = true;
        var m4ReloadTargetClose = true;
        var m4ReloadMagazineClose = true;
        var m4ReloadMagazineState = true;
        var m4ReloadPivotMotion = true;
        var m4ReloadSleeveContinuity = true;
        var m4ReloadIdempotent = true;
        var m4ReloadSampleCount = 0;
        var m4ReloadMaximumTargetDistance = 0.0f;
        var m4ReloadMaximumMagazineDistance = 0.0f;
        var m4ReloadMinimumPivotDelta = float.PositiveInfinity;
        var m4ReloadMaximumWristDelta = 0.0f;
        foreach (var progress in new[] { 0.46f, 0.63f })
        {
            var poseSet = _player.SetM4ReloadPoseForDiagnostics(progress);
            var first = _player.InspectAuthoredM4ReloadArmForDiagnostics();
            var repeatedPoseSet = _player.SetM4ReloadPoseForDiagnostics(progress);
            var repeated = _player.InspectAuthoredM4ReloadArmForDiagnostics();
            var authoredArmActive = first.AuthoredArmActive
                && first.LeftArmVisible
                && first.LeftGripFrameActive;
            var targetDistance = Mathf.Max(
                first.SupportTargetDistance,
                repeated.SupportTargetDistance);
            var magazineDistance = Mathf.Max(
                first.ActiveMagazineDistance,
                repeated.ActiveMagazineDistance);
            var magazineState = !first.PrimaryMagazineVisible
                && first.SpareMagazineVisible
                && first.SeparateMagazineNodes
                && first.PrimaryMagazinePosition.DistanceTo(first.SpareMagazinePosition) >= 0.03f;
            var pivotDelta = HandBasisDelta(
                m4IdleArm.LeftArmTransform.Basis,
                first.LeftArmTransform.Basis);
            var wristDelta = Mathf.Abs(
                first.SleeveWristLength - m4IdleArm.SleeveWristLength);
            var sleeveContinuous = first.SleeveWristLength is >= 0.045f and <= 0.28f
                && wristDelta <= 0.001f;
            var idempotent = HandTransformsMatch(
                    first.LeftArmTransform,
                    repeated.LeftArmTransform)
                && first.LeftGrip.DistanceTo(repeated.LeftGrip) <= 0.0001f
                && first.SupportTarget.DistanceTo(repeated.SupportTarget) <= 0.0001f
                && first.SpareMagazinePosition.DistanceTo(repeated.SpareMagazinePosition) <= 0.0001f
                && first.PrimaryMagazineVisible == repeated.PrimaryMagazineVisible
                && first.SpareMagazineVisible == repeated.SpareMagazineVisible;
            var sampleValid = poseSet
                && repeatedPoseSet
                && authoredArmActive
                && targetDistance <= 0.002f
                && magazineDistance <= 0.08f
                && magazineState
                && pivotDelta >= 0.08f
                && sleeveContinuous
                && idempotent;
            m4ReloadPoseSet &= poseSet && repeatedPoseSet;
            m4ReloadAuthoredArmActive &= authoredArmActive;
            m4ReloadTargetClose &= targetDistance <= 0.002f;
            m4ReloadMagazineClose &= magazineDistance <= 0.08f;
            m4ReloadMagazineState &= magazineState;
            m4ReloadPivotMotion &= pivotDelta >= 0.08f;
            m4ReloadSleeveContinuity &= sleeveContinuous;
            m4ReloadIdempotent &= idempotent;
            m4ReloadSampleCount++;
            m4ReloadMaximumTargetDistance = Mathf.Max(
                m4ReloadMaximumTargetDistance,
                targetDistance);
            m4ReloadMaximumMagazineDistance = Mathf.Max(
                m4ReloadMaximumMagazineDistance,
                magazineDistance);
            m4ReloadMinimumPivotDelta = Mathf.Min(
                m4ReloadMinimumPivotDelta,
                pivotDelta);
            m4ReloadMaximumWristDelta = Mathf.Max(
                m4ReloadMaximumWristDelta,
                wristDelta);
            GD.Print(
                $"M4_RELOAD_ARM_SAMPLE progress={progress:F2} valid={sampleValid} "
                + $"authored_active={authoredArmActive} left_arm_visible={first.LeftArmVisible} "
                + $"left_grip_active={first.LeftGripFrameActive} "
                + $"grip_target_distance={targetDistance:F6} "
                + $"active_magazine_distance={magazineDistance:F6} "
                + $"primary_magazine_visible={first.PrimaryMagazineVisible} "
                + $"spare_magazine_visible={first.SpareMagazineVisible} "
                + $"magazines_separate={magazineState} pivot_delta={pivotDelta:F6} "
                + $"wrist_delta={wristDelta:F6} idempotent={idempotent}");
        }
        var m4ReloadBoundaryContinuity = true;
        var m4ReloadBoundarySampleCount = 0;
        var m4ReloadMaximumGripStep = 0.0f;
        var m4ReloadMaximumBasisStep = 0.0f;
        foreach (var sample in new[]
        {
            (Boundary: 0.43f, Before: 0.42f, After: 0.44f),
            (Boundary: 0.78f, Before: 0.77f, After: 0.79f)
        })
        {
            var beforePoseSet = _player.SetM4ReloadPoseForDiagnostics(sample.Before);
            var before = _player.InspectAuthoredM4ReloadArmForDiagnostics();
            var afterPoseSet = _player.SetM4ReloadPoseForDiagnostics(sample.After);
            var after = _player.InspectAuthoredM4ReloadArmForDiagnostics();
            var gripStep = before.LeftGrip.DistanceTo(after.LeftGrip);
            var basisStep = HandBasisDelta(
                before.LeftArmTransform.Basis,
                after.LeftArmTransform.Basis);
            var targetDistance = Mathf.Max(
                before.SupportTargetDistance,
                after.SupportTargetDistance);
            var authoredArmActive = before.AuthoredArmActive
                && after.AuthoredArmActive
                && before.LeftGripFrameActive
                && after.LeftGripFrameActive;
            var mechanismTransition = sample.Boundary < 0.5f
                ? before.PrimaryMagazineVisible
                    && !before.SpareMagazineVisible
                    && !after.PrimaryMagazineVisible
                    && after.SpareMagazineVisible
                : !before.PrimaryMagazineVisible
                    && before.SpareMagazineVisible
                    && after.PrimaryMagazineVisible
                    && !after.SpareMagazineVisible;
            var continuous = beforePoseSet
                && afterPoseSet
                && authoredArmActive
                && mechanismTransition
                && targetDistance <= 0.002f
                && gripStep <= 0.02f
                && basisStep <= 0.16f;
            m4ReloadBoundaryContinuity &= continuous;
            m4ReloadBoundarySampleCount++;
            m4ReloadMaximumGripStep = Mathf.Max(m4ReloadMaximumGripStep, gripStep);
            m4ReloadMaximumBasisStep = Mathf.Max(m4ReloadMaximumBasisStep, basisStep);
            GD.Print(
                $"M4_RELOAD_ARM_CONTINUITY_SAMPLE boundary={sample.Boundary:F2} "
                + $"before={sample.Before:F2} after={sample.After:F2} valid={continuous} "
                + $"authored_active={authoredArmActive} mechanism_transition={mechanismTransition} "
                + $"grip_step={gripStep:F6} basis_step={basisStep:F6} "
                + $"max_target_distance={targetDistance:F6}");
        }
        _player.ClearM4ReloadPoseForDiagnostics();
        var m4ResetArm = _player.InspectAuthoredM4ReloadArmForDiagnostics();
        var m4ResetHandValid = _player.InspectAuthoredHandPoseForDiagnostics().Valid;
        _player.ClearM4ReloadPoseForDiagnostics();
        var m4RepeatedResetArm = _player.InspectAuthoredM4ReloadArmForDiagnostics();
        var m4ResetOriginDistance = m4IdleArm.LeftArmTransform.Origin.DistanceTo(
            m4ResetArm.LeftArmTransform.Origin);
        var m4ResetBasisDelta = HandBasisDelta(
            m4IdleArm.LeftArmTransform.Basis,
            m4ResetArm.LeftArmTransform.Basis);
        var m4ReloadReset = m4ResetArm.AuthoredArmActive
            && m4ResetArm.PrimaryMagazineVisible
            && !m4ResetArm.SpareMagazineVisible
            && m4ResetHandValid
            && HandTransformsMatch(
                m4IdleArm.LeftArmTransform,
                m4ResetArm.LeftArmTransform)
            && HandTransformsMatch(
                m4ResetArm.LeftArmTransform,
                m4RepeatedResetArm.LeftArmTransform)
            && m4ResetArm.LeftGrip.DistanceTo(m4RepeatedResetArm.LeftGrip) <= 0.0001f;
        var m4ReloadValid = m4IdleArm.AuthoredArmActive
            && m4IdleHandValid
            && m4ReloadPoseSet
            && m4ReloadAuthoredArmActive
            && m4ReloadTargetClose
            && m4ReloadMagazineClose
            && m4ReloadMagazineState
            && m4ReloadPivotMotion
            && m4ReloadSleeveContinuity
            && m4ReloadIdempotent
            && m4ReloadBoundaryContinuity
            && m4ReloadReset
            && m4ReloadSampleCount == 2
            && m4ReloadBoundarySampleCount == 2;
        GD.Print(
            $"M4_RELOAD_ARM_CHECK valid={m4ReloadValid} samples={m4ReloadSampleCount} "
            + $"pose_set={m4ReloadPoseSet} authored_left_active={m4ReloadAuthoredArmActive} "
            + $"target_close={m4ReloadTargetClose} magazine_close={m4ReloadMagazineClose} "
            + $"magazine_state={m4ReloadMagazineState} pivot_motion={m4ReloadPivotMotion} "
            + $"sleeve_continuity={m4ReloadSleeveContinuity} "
            + $"idempotent={m4ReloadIdempotent} "
            + $"boundary_continuity={m4ReloadBoundaryContinuity} "
            + $"boundary_samples={m4ReloadBoundarySampleCount} reset={m4ReloadReset} "
            + $"idle_hand={m4IdleHandValid} reset_hand={m4ResetHandValid} "
            + $"max_target_distance={m4ReloadMaximumTargetDistance:F6} "
            + $"max_magazine_distance={m4ReloadMaximumMagazineDistance:F6} "
            + $"min_pivot_delta={m4ReloadMinimumPivotDelta:F6} "
            + $"max_wrist_delta={m4ReloadMaximumWristDelta:F6} "
            + $"max_grip_step={m4ReloadMaximumGripStep:F6} "
            + $"max_basis_step={m4ReloadMaximumBasisStep:F6} "
            + $"reset_origin_distance={m4ResetOriginDistance:F6} "
            + $"reset_basis_delta={m4ResetBasisDelta:F6}");
        GD.Print($"M4_RELOAD_ARM_PASS valid={m4ReloadValid}");
        foreach (var line in results) GD.Print(line);
        var valid = posesValid
            && authoredRigValid
            && servicePistolCorrectionValid
            && sidearmPresentationValid
            && longGunPresentationValid
            && smgSleeveReach
            && smgSleeveVolume
            && smgWeaponSize
            && smgReloadPresentation
            && akReloadValid
            && m4ReloadValid
            && layoutValid
            && results.Count == platforms.Length;
        GD.Print(
            $"HAND_POSE_CHECK valid={valid} procedural_pose={posesValid} "
            + $"authored_rig={authoredRigValid} smg_sleeve_reach={smgSleeveReach} "
            + $"service_pistol_correction={servicePistolCorrectionValid} "
            + $"sidearm_presentation={sidearmPresentationValid} "
            + $"long_gun_presentation={longGunPresentationValid} "
            + $"smg_sleeve_volume={smgSleeveVolume} "
            + $"smg_arm_bounds={smgArmBounds} "
            + $"smg_weapon_size={smgWeaponSize} smg_weapon_bounds={smgWeaponBounds} "
            + $"smg_reload_presentation={smgReloadPresentation} "
            + $"ak_reload_arm={akReloadValid} "
            + $"m4_reload_arm={m4ReloadValid} "
            + $"layout_valid={layoutValid} samples={results.Count} "
            + $"requested_window={(ultrawide || narrow ? requestedWindowSize.ToString() : "default")} "
            + $"window_size={GetWindow().Size} "
            + $"logical_viewport={GetViewport().GetVisibleRect().Size}");
        GD.Print($"HAND_POSE_PASS valid={valid}");
        GD.Print($"HAND_DIAGNOSTICS_DONE count={results.Count}");
        GetTree().Quit(valid ? 0 : 2);
    }

    private static Aabb AuthoredArmWorldBounds(Node3D root)
    {
        var hasBounds = false;
        var bounds = new Aabb();
        foreach (var mesh in CombatModelLibrary.MeshesBelow(root))
        {
            var local = mesh.Mesh?.GetAabb() ?? new Aabb();
            for (var x = 0; x <= 1; x++)
            {
                for (var y = 0; y <= 1; y++)
                {
                    for (var z = 0; z <= 1; z++)
                    {
                        var point = local.Position + new Vector3(
                            local.Size.X * x,
                            local.Size.Y * y,
                            local.Size.Z * z);
                        var worldPoint = mesh.GlobalTransform * point;
                        if (!hasBounds)
                        {
                            bounds = new Aabb(worldPoint, Vector3.Zero);
                            hasBounds = true;
                        }
                        else
                        {
                            bounds = bounds.Expand(worldPoint);
                        }
                    }
                }
            }
        }
        return bounds;
    }

    private static string SidearmScreenMetrics(
        string label,
        SidearmPresentationInspection inspection)
    {
        var viewportHeight = inspection.ScreenSize.Y;
        return $"{label}_valid={inspection.Valid} "
            + $"{label}_settled={inspection.Settled} "
            + $"{label}_sleeves_bottom={inspection.SleevesReachBottom} "
            + $"{label}_support_shape={inspection.SupportArmShapeValid} "
            + $"{label}_weapon_readable={inspection.WeaponReadable} "
            + $"{label}_logical_viewport={inspection.LogicalViewportSize} "
            + $"{label}_screen={inspection.ScreenSize} "
            + $"{label}_right_bounds=({inspection.RightArm.Bounds.Position},{inspection.RightArm.Bounds.End}) "
            + $"{label}_right_bottom_gap={inspection.RightArm.BottomGapRatio:F4} "
            + $"{label}_right_bottom_span_h={inspection.RightArm.BottomSpanViewportHeights:F4} "
            + $"{label}_right_bottom_vertices={inspection.RightArm.BottomBandVertexCount} "
            + $"{label}_right_projected_vertices={inspection.RightArm.ProjectedVertexCount} "
            + $"{label}_left_bounds=({inspection.LeftArm.Bounds.Position},{inspection.LeftArm.Bounds.End}) "
            + $"{label}_left_bottom_gap={inspection.LeftArm.BottomGapRatio:F4} "
            + $"{label}_left_bottom_span_h={inspection.LeftArm.BottomSpanViewportHeights:F4} "
            + $"{label}_left_bottom_vertices={inspection.LeftArm.BottomBandVertexCount} "
            + $"{label}_left_projected_vertices={inspection.LeftArm.ProjectedVertexCount} "
            + $"{label}_left_width_h={inspection.LeftArm.WidthViewportHeights(viewportHeight):F4} "
            + $"{label}_left_height_h={inspection.LeftArm.HeightViewportHeights(viewportHeight):F4} "
            + $"{label}_left_aspect={inspection.LeftArm.PixelAspect:F4} "
            + $"{label}_weapon_bounds=({inspection.Weapon.Bounds.Position},{inspection.Weapon.Bounds.End}) "
            + $"{label}_weapon_width_h={inspection.Weapon.WidthViewportHeights(viewportHeight):F4} "
            + $"{label}_weapon_height_h={inspection.Weapon.HeightViewportHeights(viewportHeight):F4} "
            + $"{label}_weapon_area_h2={inspection.Weapon.AreaViewportHeightsSquared(viewportHeight):F4} "
            + $"{label}_weapon_projected_vertices={inspection.Weapon.ProjectedVertexCount} ";
    }

    private static float HandBasisDelta(Basis left, Basis right)
        => left.X.DistanceTo(right.X)
            + left.Y.DistanceTo(right.Y)
            + left.Z.DistanceTo(right.Z);

    private static bool HandTransformsMatch(Transform3D left, Transform3D right)
        => left.Origin.DistanceTo(right.Origin) <= 0.0001f
            && HandBasisDelta(left.Basis, right.Basis) <= 0.0003f;
}
