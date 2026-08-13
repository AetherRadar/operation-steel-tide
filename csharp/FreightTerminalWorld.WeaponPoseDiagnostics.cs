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

        var precisionBuild = WeaponCatalog.Build(WeaponPlatform.AXMC, 3);
        precisionBuild.Attachments[AttachmentSlot.Optic] = "optic_7x";
        _player.GrantFireablePrimaryForDiagnostics(precisionBuild);
        await WaitFrames(4);

        var offsets = new List<Vector2>();
        var yawResiduals = new List<float>();
        var stancesReady = true;

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
        var valid = reloadCompleted
            && stancesReady
            && offsets.Count == 4
            && maxOffset <= screenTolerancePixels
            && maxYaw <= yawToleranceRadians;

        await WaitFrames(2);
        SaveViewportImage("res://ads_alignment_validation.png");
        GD.Print($"ADS_ALIGNMENT_CHECK valid={valid} samples={offsets.Count} stances={stancesReady} reload={reloadCompleted} max_offset_px={maxOffset:0.000} max_yaw_rad={maxYaw:0.000000} offsets={FormatOffsets(offsets)}");
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
            for (var frame = 0; frame < 90; frame++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            }
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
}
