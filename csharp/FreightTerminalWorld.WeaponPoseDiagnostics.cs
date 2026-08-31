using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private async void ValidateAdsAlignment(bool narrow = false, bool ultrawide = false)
    {
        if (narrow || ultrawide)
        {
            var window = GetWindow();
            window.ContentScaleMode = Window.ContentScaleModeEnum.CanvasItems;
            window.ContentScaleAspect = Window.ContentScaleAspectEnum.Ignore;
            window.Size = ultrawide
                ? new Vector2I(2048, 621)
                : new Vector2I(985, 847);
            await WaitFrames(4);
        }

        DisableActorsForSurvivalDiagnostics();
        _player.UiLocked = false;
        _player.SetSearchPose(false);
        Input.MouseMode = Input.MouseModeEnum.Captured;
        Input.ActionRelease("aim");
        Input.ActionRelease("lean_left");
        Input.ActionRelease("lean_right");
        foreach (var mate in _squadMates)
        {
            if (!IsInstanceValid(mate))
            {
                continue;
            }
            mate.ProcessMode = ProcessModeEnum.Disabled;
            mate.GlobalPosition = new Vector3(
                240.0f + mate.SquadSlot * 3.0f,
                80.0f,
                240.0f);
        }
        _missionDirector.ExitDeploymentZone();
        _player.GlobalPosition = new Vector3(0.0f, 0.2f, 40.0f);
        _player.Velocity = Vector3.Zero;
        _player.FaceWorldPointForDiagnostics(new Vector3(0.0f, 0.2f, -80.0f));
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

        var precisionBuild = WeaponCatalog.Build(WeaponPlatform.AXMC, 3);
        precisionBuild.Attachments[AttachmentSlot.Optic] = "optic_7x";
        _player.GrantFireablePrimaryForDiagnostics(precisionBuild);
        await WaitFrames(4);

        var offsets = new List<Vector2>();
        var yawResiduals = new List<float>();
        var opticClearanceSamples = new List<(
            WeaponPlatform Platform,
            string OpticId,
            TacticalPlayer.FirstPersonOpticClearanceInspection Inspection,
            Vector2 ScreenOffset,
            TacticalPlayer.OpticAxisProjectionInspection Axis)>();
        var worldOpticSamples = new List<(
            WeaponPlatform Platform,
            string OpticId,
            bool Valid)>();
        var warmupReady = false;
        for (var attempt = 0; attempt < 2 && !warmupReady; attempt++)
        {
            Input.ActionRelease("aim");
            await WaitFrames(4);
            Input.ActionPress("aim");
            warmupReady = await WaitForAimSettlement();
        }
        Input.ActionRelease("aim");
        await WaitFrames(12);

        // A cold Mono/resource load can complete after the diagnostic starts and
        // clear the first synthetic input state. Warm the real ADS path before
        // recording inherited-pose samples so a clean checkout is deterministic,
        // while still requiring the second attempt to exercise live input.
        var stancesReady = warmupReady;
        var opticSamplesSettled = true;

        await MeasureAfterInheritedPose(
            new Vector3(0.5f, -0.72f, -0.42f),
            new Vector3(0.48f, 0.08f, -0.3f),
            PlayerStance.Standing,
            string.Empty);
        await MeasureAfterInheritedPose(
            new Vector3(0.5f, -0.72f, -0.42f),
            new Vector3(0.48f, -0.08f, 0.3f),
            PlayerStance.Crouched,
            "lean_left");

        var replacement = WeaponCatalog.Build(WeaponPlatform.M24, 3);
        replacement.Attachments[AttachmentSlot.Optic] = "optic_sniper";
        _player.SeedWeaponPoseForDiagnostics(
            new Vector3(0.5f, -0.72f, -0.42f),
            new Vector3(-0.4f, 0.08f, 0.28f));
        _player.EquipFromLoot(new LootItem
        {
            Kind = LootItemKind.Weapon,
            Weapon = replacement,
            Grade = LootGrade.Epic
        });
        await MeasureCurrentPose(PlayerStance.Prone, "lean_right");

        _player.SeedWeaponPoseForDiagnostics(
            new Vector3(0.18f, -0.23f, -0.86f),
            new Vector3(-0.13f, -0.08f, -0.32f));
        _player.SetAmmoGradeForDiagnostics(LootGrade.Common, 60);
        var reloadCompleted = _player.ReloadImmediatelyForDiagnostics(0);
        await MeasureCurrentPose(PlayerStance.Standing, string.Empty, keepAiming: true);

        Input.ActionRelease("aim");
        await WaitFrames(10);
        var attachmentCompatibilitySamples = new List<(
            WeaponPlatform Platform,
            string AttachmentId,
            bool Expected,
            bool Actual)>();
        var opticAttachments = WeaponCatalog.AllAttachments
            .Where(attachment => attachment.Slot == AttachmentSlot.Optic)
            .OrderBy(attachment => attachment.Id)
            .ToArray();
        var attachmentCompatibilityValid = true;
        foreach (var weapon in WeaponCatalog.AllWeapons.OrderBy(weapon => weapon.Platform))
        {
            foreach (var attachment in opticAttachments)
            {
                var fixedScope = weapon.Platform is WeaponPlatform.M24
                    or WeaponPlatform.AXMC
                    or WeaponPlatform.AWM
                    or WeaponPlatform.VSS;
                var magnified = attachment.Id is "optic_scope"
                    or "optic_7x"
                    or "optic_sniper";
                var expected = !fixedScope || magnified;
                var actual = WeaponCatalog.CanEquipAttachment(
                    weapon.Platform,
                    attachment.Id);
                attachmentCompatibilitySamples.Add((
                    weapon.Platform,
                    attachment.Id,
                    expected,
                    actual));
                attachmentCompatibilityValid &= actual == expected;
            }
        }

        var vssCompatibilityBuild = WeaponCatalog.Build(WeaponPlatform.VSS, 3);
        vssCompatibilityBuild.Attachments[AttachmentSlot.Optic] = "optic_scope";
        _player.GrantFireablePrimaryForDiagnostics(vssCompatibilityBuild);
        await WaitFrames(4);
        var vssOpticGradeBefore = _player.EquippedAttachmentGrade(AttachmentSlot.Optic);
        var directIncoming = new LootItem
        {
            Kind = LootItemKind.Attachment,
            AttachmentId = "optic_micro",
            Grade = LootGrade.Rare
        };
        var directResult = _player.EquipFromLoot(directIncoming);
        var directPathRejected = ReferenceEquals(directResult, directIncoming)
            && _player.EquippedWeapon.Attachments.GetValueOrDefault(AttachmentSlot.Optic) == "optic_scope"
            && _player.EquippedAttachmentGrade(AttachmentSlot.Optic) == vssOpticGradeBefore;
        var slotIncoming = new LootItem
        {
            Kind = LootItemKind.Attachment,
            AttachmentId = "optic_holo",
            Grade = LootGrade.Epic
        };
        var slotResult = _player.EquipFromLootToWeaponSlot(
            slotIncoming,
            PlayerWeaponSlot.Primary);
        var slotPathRejected = ReferenceEquals(slotResult, slotIncoming)
            && _player.EquippedWeapon.Attachments.GetValueOrDefault(AttachmentSlot.Optic) == "optic_scope"
            && _player.EquippedAttachmentGrade(AttachmentSlot.Optic) == vssOpticGradeBefore;
        var incompatibleWholeWeapon = WeaponCatalog.Build(WeaponPlatform.M24, 3);
        incompatibleWholeWeapon.Attachments[AttachmentSlot.Optic] = "optic_micro";
        _player.EquipFromLootToWeaponSlot(
            new LootItem
            {
                Kind = LootItemKind.Weapon,
                Weapon = incompatibleWholeWeapon,
                Grade = LootGrade.Epic
            },
            PlayerWeaponSlot.Primary);
        await WaitFrames(4);
        var wholeWeaponPathNormalized = _player.EquippedWeapon.Platform == WeaponPlatform.M24
            && _player.EquippedWeapon.Attachments.GetValueOrDefault(AttachmentSlot.Optic)
                == "optic_sniper"
            && _player.AuthoredOpticPresentationValidForDiagnostics
            && !_player.HasVisibleAuthoredOpticGeometryForDiagnostics;
        var missingOpticWholeWeapon = WeaponCatalog.Build(WeaponPlatform.AWM, 3);
        missingOpticWholeWeapon.Attachments.Remove(AttachmentSlot.Optic);
        _player.EquipFromLootToWeaponSlot(
            new LootItem
            {
                Kind = LootItemKind.Weapon,
                Weapon = missingOpticWholeWeapon,
                Grade = LootGrade.Epic
            },
            PlayerWeaponSlot.Primary);
        await WaitFrames(4);
        var missingWholeWeaponPathNormalized = _player.EquippedWeapon.Platform
                == WeaponPlatform.AWM
            && _player.EquippedWeapon.Attachments.GetValueOrDefault(AttachmentSlot.Optic)
                == "optic_7x"
            && _player.AuthoredOpticPresentationValidForDiagnostics
            && !_player.HasVisibleAuthoredOpticGeometryForDiagnostics;
        attachmentCompatibilityValid &= directPathRejected
            && slotPathRejected
            && wholeWeaponPathNormalized
            && missingWholeWeaponPathNormalized;

        var opticClearanceMatrix = WeaponCatalog.AllWeapons
            .OrderBy(weapon => weapon.Platform)
            .SelectMany(weapon => opticAttachments
                .Where(attachment => WeaponCatalog.CanEquipAttachment(
                    weapon.Platform,
                    attachment.Id))
                .Select(attachment => (
                    Platform: weapon.Platform,
                    OpticId: attachment.Id)))
            .ToArray();
        foreach (var sample in opticClearanceMatrix)
        {
            worldOpticSamples.Add((
                sample.Platform,
                sample.OpticId,
                InspectWorldOpticPresentation(sample.Platform, sample.OpticId)));
            await MeasureOpticClearance(sample.Platform, sample.OpticId);
        }

        var maxOffset = 0.0f;
        foreach (var offset in offsets)
        {
            maxOffset = Mathf.Max(maxOffset, offset.Length());
        }
        var maxYaw = 0.0f;
        foreach (var yaw in yawResiduals)
        {
            maxYaw = Mathf.Max(maxYaw, Mathf.Abs(yaw));
        }

        const float screenTolerancePixels = 1.5f;
        const float yawToleranceRadians = 0.001f;
        const float opticAxisToleranceRadians = 0.003f;
        const float opticMountMaximumGapMeters = 0.02f;
        const float opticMountMaximumIntersectionMeters = 0.03f;
        const float ak47OpticContactToleranceMeters = 0.003f;
        var opticClearanceValid = opticSamplesSettled
            && opticClearanceSamples.Count == opticClearanceMatrix.Length;
        var worldOpticsValid = worldOpticSamples.Count == opticClearanceMatrix.Length
            && worldOpticSamples.All(sample => sample.Valid);
        foreach (var sample in opticClearanceSamples)
        {
            var contactRequired = !sample.Inspection.IntegratedOptic;
            var maximumGap = sample.Platform == WeaponPlatform.AK74
                ? ak47OpticContactToleranceMeters
                : opticMountMaximumGapMeters;
            var maximumIntersection = sample.Platform == WeaponPlatform.AK74
                ? ak47OpticContactToleranceMeters
                : opticMountMaximumIntersectionMeters;
            opticClearanceValid &= sample.Inspection.Available
                && sample.Inspection.OpticVisible
                && sample.Inspection.IronSightsClear
                && sample.Inspection.AuthoredPresentationValid
                && sample.Inspection.IntegratedApertureValid
                && sample.Inspection.ReticleDiameter is > 0.0f and <= 0.007f
                && sample.Axis.Available
                && sample.Axis.ReticleToRearPixels <= screenTolerancePixels
                && sample.Axis.RearToFrontPixels <= screenTolerancePixels
                && sample.Axis.FrontToScreenCenterPixels <= screenTolerancePixels
                && sample.Axis.AxisAngleRadians <= opticAxisToleranceRadians
                && (!contactRequired
                    || (sample.Inspection.MountGap >= -maximumIntersection
                        && sample.Inspection.MountGap <= maximumGap))
                && sample.ScreenOffset.Length() <= screenTolerancePixels;
        }
        var valid = reloadCompleted
            && stancesReady
            && offsets.Count == 4
            && maxOffset <= screenTolerancePixels
            && maxYaw <= yawToleranceRadians
            && attachmentCompatibilityValid
            && worldOpticsValid
            && opticClearanceValid;

        await WaitFrames(2);
        var layout = ultrawide ? "ultrawide" : narrow ? "narrow" : "standard";
        SaveViewportImage($"res://ads_alignment_{layout}_validation.png");
        GD.Print($"ADS_ALIGNMENT_CHECK valid={valid} layout={layout} samples={offsets.Count} stances={stancesReady} reload={reloadCompleted} max_offset_px={maxOffset:0.000} max_yaw_rad={maxYaw:0.000000} offsets={FormatOffsets(offsets)} attachment_compatibility={attachmentCompatibilityValid} attachment_compatibility_samples={FormatAttachmentCompatibilitySamples(attachmentCompatibilitySamples)} vss_rejection_paths=direct:{directPathRejected};slot:{slotPathRejected} whole_weapon_normalized={wholeWeaponPathNormalized} missing_whole_weapon_normalized={missingWholeWeaponPathNormalized} world_optics={worldOpticsValid} world_optic_samples={FormatWorldOpticSamples(worldOpticSamples)} optic_settled={opticSamplesSettled} optic_clearance={opticClearanceValid} optic_samples={FormatOpticClearanceSamples(opticClearanceSamples)}");
        GD.Print($"ADS_ALIGNMENT_PASS valid={valid}");
        Input.ActionRelease("aim");
        Input.ActionRelease("lean_left");
        Input.ActionRelease("lean_right");
        GetTree().Quit(valid ? 0 : 2);

        async System.Threading.Tasks.Task MeasureAfterInheritedPose(
            Vector3 position,
            Vector3 rotation,
            PlayerStance stance,
            string leanAction)
        {
            _player.SeedWeaponPoseForDiagnostics(position, rotation);
            await MeasureCurrentPose(stance, leanAction);
        }

        async System.Threading.Tasks.Task MeasureCurrentPose(
            PlayerStance stance,
            string leanAction,
            bool keepAiming = false)
        {
            Input.ActionRelease("aim");
            Input.ActionRelease("lean_left");
            Input.ActionRelease("lean_right");
            stancesReady &= _player.TrySetStance(stance);
            if (!string.IsNullOrEmpty(leanAction))
            {
                Input.ActionPress(leanAction);
            }
            Input.ActionPress("aim");
            stancesReady &= await WaitForAimSettlement();
            offsets.Add(_player.OpticScreenOffsetForDiagnostics());
            yawResiduals.Add(_player.WeaponRotationForDiagnostics.Y);
            if (!keepAiming)
            {
                Input.ActionRelease("aim");
                if (!string.IsNullOrEmpty(leanAction))
                {
                    Input.ActionRelease(leanAction);
                }
                for (var frame = 0; frame < 12; frame++)
                {
                    await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                }
            }
        }

        async System.Threading.Tasks.Task MeasureOpticClearance(
            WeaponPlatform platform,
            string opticId)
        {
            Input.ActionRelease("aim");
            Input.ActionRelease("lean_left");
            Input.ActionRelease("lean_right");
            var build = WeaponCatalog.Build(platform, 3);
            build.Attachments[AttachmentSlot.Optic] = opticId;
            _player.GrantFireablePrimaryForDiagnostics(build);
            for (var frame = 0; frame < 30; frame++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            }
            // Each matrix entry is a fresh presentation contract. Reset the
            // inherited viewmodel transform before exercising the real ADS
            // transition so one platform's interpolated pose cannot poison the
            // next sample.
            var sampleSettled = false;
            for (var attempt = 0; attempt < 2 && !sampleSettled; attempt++)
            {
                _player.SetAimingPoseForDiagnostics(false);
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                Input.ActionPress("aim");
                sampleSettled = await WaitForAimSettlement();
                if (!sampleSettled)
                {
                    // Window focus changes and a late cold resource load can
                    // clear synthetic input without exercising the ADS path.
                    // Re-enter from a known hip pose once; the measured sample
                    // still has to satisfy the original pixel/axis tolerances.
                    Input.ActionRelease("aim");
                    await WaitFrames(4);
                }
            }
            opticSamplesSettled &= sampleSettled;
            opticClearanceSamples.Add((
                platform,
                opticId,
                _player.InspectOpticClearanceForDiagnostics(),
                _player.OpticScreenOffsetForDiagnostics(),
                _player.InspectOpticAxisProjectionForDiagnostics()));
            Input.ActionRelease("aim");
            for (var frame = 0; frame < 24; frame++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            }
        }

        async System.Threading.Tasks.Task<bool> WaitForAimSettlement()
        {
            const int minimumFrames = 20;
            const int maximumFrames = 240;
            const int stableFramesRequired = 4;
            const float settledOffsetPixels = 0.25f;
            const float settledYawRadians = 0.0005f;
            var stableFrames = 0;
            for (var frame = 0; frame < maximumFrames; frame++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                if (frame < minimumFrames)
                {
                    continue;
                }

                var offset = _player.OpticScreenOffsetForDiagnostics();
                var yaw = Mathf.Abs(_player.WeaponRotationForDiagnostics.Y);
                stableFrames = offset.Length() <= settledOffsetPixels
                    && yaw <= settledYawRadians
                    ? stableFrames + 1
                    : 0;
                if (stableFrames >= stableFramesRequired)
                {
                    return true;
                }
            }
            return false;
        }
    }

    private bool InspectWorldOpticPresentation(WeaponPlatform platform, string opticId)
    {
        AuthoredWeaponVisual? visual = null;
        try
        {
            var build = WeaponCatalog.Build(platform, 3);
            build.Attachments[AttachmentSlot.Optic] = opticId;
            visual = CombatModelLibrary.InstantiateWeapon(platform, firstPerson: false);
            visual.Root.ProcessMode = ProcessModeEnum.Disabled;
            visual.Root.Position = new Vector3(0.0f, -100.0f, 0.0f);
            AddChild(visual.Root);
            visual.Configure(build);
            return visual.WorldOpticPresentationMatches(build);
        }
        catch (System.Exception exception)
        {
            GD.PushWarning(
                $"World optic diagnostic failed for {platform}/{opticId}: {exception.Message}");
            return false;
        }
        finally
        {
            if (visual is not null && IsInstanceValid(visual.Root))
            {
                visual.Root.GetParent()?.RemoveChild(visual.Root);
                visual.Root.Free();
            }
        }
    }

    private static string FormatOffsets(IReadOnlyList<Vector2> offsets)
    {
        var formatted = new string[offsets.Count];
        for (var index = 0; index < offsets.Count; index++)
        {
            formatted[index] = $"({offsets[index].X:0.00},{offsets[index].Y:0.00})";
        }
        return string.Join(',', formatted);
    }

    private static string FormatAttachmentCompatibilitySamples(
        IReadOnlyList<(
            WeaponPlatform Platform,
            string AttachmentId,
            bool Expected,
            bool Actual)> samples)
    {
        var formatted = new string[samples.Count];
        for (var index = 0; index < samples.Count; index++)
        {
            var sample = samples[index];
            formatted[index] = $"{sample.Platform}/{sample.AttachmentId}:expected={sample.Expected};actual={sample.Actual}";
        }
        return string.Join(',', formatted);
    }

    private static string FormatWorldOpticSamples(
        IReadOnlyList<(WeaponPlatform Platform, string OpticId, bool Valid)> samples)
        => string.Join(',', samples.Select(
            sample => $"{sample.Platform}/{sample.OpticId}:{sample.Valid}"));

    private static string FormatOpticClearanceSamples(
        IReadOnlyList<(
            WeaponPlatform Platform,
            string OpticId,
            TacticalPlayer.FirstPersonOpticClearanceInspection Inspection,
            Vector2 ScreenOffset,
            TacticalPlayer.OpticAxisProjectionInspection Axis)> samples)
    {
        var formatted = new string[samples.Count];
        for (var index = 0; index < samples.Count; index++)
        {
            var sample = samples[index];
            formatted[index] = $"{sample.Platform}/{sample.OpticId}:"
                + $"available={sample.Inspection.Available};"
                + $"visible={sample.Inspection.OpticVisible};"
                + $"irons_clear={sample.Inspection.IronSightsClear};"
                + $"authored={sample.Inspection.AuthoredPresentationValid};"
                + $"reticle={sample.Inspection.ReticleDiameter:0.000};"
                + $"integrated={sample.Inspection.IntegratedOptic};"
                + $"aperture={sample.Inspection.IntegratedApertureValid};"
                + $"glass_surfaces={sample.Inspection.IntegratedGlassSurfaceCount};"
                + $"aperture_size={sample.Inspection.IntegratedApertureSize};"
                + $"anchor_residual={sample.Inspection.IntegratedAnchorResidual:0.000000};"
                + $"top={sample.Inspection.WeaponTop:0.000};"
                + $"mount={sample.Inspection.MountHeight:0.000};"
                + $"clearance={sample.Inspection.MountClearance:0.000};"
                + $"surface={sample.Inspection.MountSurfaceHeight:0.000};"
                + $"bottom={sample.Inspection.OpticBottom:0.000};"
                + $"gap={sample.Inspection.MountGap:0.000};"
                + $"offset={sample.ScreenOffset.Length():0.000};"
                + $"axis_available={sample.Axis.Available};"
                + $"dot_rear_px={sample.Axis.ReticleToRearPixels:0.000};"
                + $"rear_front_px={sample.Axis.RearToFrontPixels:0.000};"
                + $"front_center_px={sample.Axis.FrontToScreenCenterPixels:0.000};"
                + $"axis_angle_rad={sample.Axis.AxisAngleRadians:0.000000}";
        }
        return string.Join(',', formatted);
    }
}
