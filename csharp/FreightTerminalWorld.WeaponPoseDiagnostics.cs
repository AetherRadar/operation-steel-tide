using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private async void ValidateAdsAlignment()
    {
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
            Vector2 ScreenOffset)>();
        var stancesReady = true;
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
        var vssAttachmentCompatibilitySamples = new List<(
            string AttachmentId,
            bool Expected,
            bool Actual)>();
        var vssAttachmentCompatibilityMatrix = new[]
        {
            (AttachmentId: "optic_micro", Expected: false),
            (AttachmentId: "optic_holo", Expected: false),
            (AttachmentId: "optic_scope", Expected: true),
            (AttachmentId: "optic_7x", Expected: true),
            (AttachmentId: "optic_sniper", Expected: true)
        };
        var vssAttachmentCompatibilityValid = true;
        foreach (var sample in vssAttachmentCompatibilityMatrix)
        {
            var actual = WeaponCatalog.CanEquipAttachment(
                WeaponPlatform.VSS,
                sample.AttachmentId);
            vssAttachmentCompatibilitySamples.Add((
                sample.AttachmentId,
                sample.Expected,
                actual));
            vssAttachmentCompatibilityValid &= actual == sample.Expected;
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
        vssAttachmentCompatibilityValid &= directPathRejected && slotPathRejected;

        var opticClearanceMatrix = new[]
        {
            (Platform: WeaponPlatform.M4A1, OpticId: "optic_micro"),
            (Platform: WeaponPlatform.M4A1, OpticId: "optic_holo"),
            (Platform: WeaponPlatform.M4A1, OpticId: "optic_scope"),
            (Platform: WeaponPlatform.AK74, OpticId: "optic_micro"),
            (Platform: WeaponPlatform.AK74, OpticId: "optic_holo"),
            (Platform: WeaponPlatform.AK74, OpticId: "optic_scope"),
            (Platform: WeaponPlatform.ScarL, OpticId: "optic_micro"),
            (Platform: WeaponPlatform.ScarL, OpticId: "optic_holo"),
            (Platform: WeaponPlatform.ScarL, OpticId: "optic_scope"),
            (Platform: WeaponPlatform.MP5A5, OpticId: "optic_micro"),
            (Platform: WeaponPlatform.MP5A5, OpticId: "optic_holo"),
            (Platform: WeaponPlatform.VSS, OpticId: "optic_scope")
        };
        foreach (var sample in opticClearanceMatrix)
        {
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
        const float opticMountMaximumGapMeters = 0.02f;
        const float opticMountMaximumIntersectionMeters = 0.03f;
        var opticClearanceValid = opticSamplesSettled
            && opticClearanceSamples.Count == opticClearanceMatrix.Length;
        foreach (var sample in opticClearanceSamples)
        {
            var contactRequired = !sample.Inspection.IntegratedOptic;
            opticClearanceValid &= sample.Inspection.Available
                && sample.Inspection.OpticVisible
                && sample.Inspection.IronSightsClear
                && sample.Inspection.AuthoredPresentationValid
                && sample.Inspection.IntegratedApertureValid
                && sample.Inspection.ReticleDiameter is > 0.0f and <= 0.007f
                && (!contactRequired
                    || sample.Inspection.MountGap
                        is >= -opticMountMaximumIntersectionMeters
                        and <= opticMountMaximumGapMeters)
                && sample.ScreenOffset.Length() <= screenTolerancePixels;
        }
        var valid = reloadCompleted
            && stancesReady
            && offsets.Count == 4
            && maxOffset <= screenTolerancePixels
            && maxYaw <= yawToleranceRadians
            && vssAttachmentCompatibilityValid
            && opticClearanceValid;

        await WaitFrames(2);
        SaveViewportImage("res://ads_alignment_validation.png");
        GD.Print($"ADS_ALIGNMENT_CHECK valid={valid} samples={offsets.Count} stances={stancesReady} reload={reloadCompleted} max_offset_px={maxOffset:0.000} max_yaw_rad={maxYaw:0.000000} offsets={FormatOffsets(offsets)} vss_compatibility={vssAttachmentCompatibilityValid} vss_compatibility_samples={FormatAttachmentCompatibilitySamples(vssAttachmentCompatibilitySamples)} vss_rejection_paths=direct:{directPathRejected};slot:{slotPathRejected} optic_settled={opticSamplesSettled} optic_clearance={opticClearanceValid} optic_samples={FormatOpticClearanceSamples(opticClearanceSamples)}");
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
            var build = WeaponCatalog.Build(platform, 3);
            build.Attachments[AttachmentSlot.Optic] = opticId;
            _player.GrantFireablePrimaryForDiagnostics(build);
            for (var frame = 0; frame < 6; frame++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            }
            Input.ActionPress("aim");
            opticSamplesSettled &= await WaitForAimSettlement();
            opticClearanceSamples.Add((
                platform,
                opticId,
                _player.InspectOpticClearanceForDiagnostics(),
                _player.OpticScreenOffsetForDiagnostics()));
            Input.ActionRelease("aim");
            for (var frame = 0; frame < 12; frame++)
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
            string AttachmentId,
            bool Expected,
            bool Actual)> samples)
    {
        var formatted = new string[samples.Count];
        for (var index = 0; index < samples.Count; index++)
        {
            var sample = samples[index];
            formatted[index] = $"{sample.AttachmentId}:expected={sample.Expected};actual={sample.Actual}";
        }
        return string.Join(',', formatted);
    }

    private static string FormatOpticClearanceSamples(
        IReadOnlyList<(
            WeaponPlatform Platform,
            string OpticId,
            TacticalPlayer.FirstPersonOpticClearanceInspection Inspection,
            Vector2 ScreenOffset)> samples)
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
                + $"offset={sample.ScreenOffset.Length():0.000}";
        }
        return string.Join(',', formatted);
    }
}
