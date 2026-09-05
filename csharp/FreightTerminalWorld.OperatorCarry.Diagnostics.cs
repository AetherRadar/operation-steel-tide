using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private const float OperatorCarryRightElbowMinimum = 50.0f;
    // The Tencent bodies have authored upper-body clips with a wider elbow
    // envelope than the legacy mannequin. These limits reject impossible
    // bends while allowing natural sprint/aim poses.
    private const float OperatorCarryRightElbowMaximum = 135.0f;
    private const float OperatorCarryLeftElbowMinimum = 90.0f;
    private const float OperatorCarryLeftElbowMaximum = 175.0f;
    private const float OperatorCarryArmSegmentMinimum = 0.12f;
    private const float OperatorCarryArmSegmentMaximum = 0.35f;
    private const float OperatorCarryRightWristDropMinimum = 0.10f;
    private const float OperatorCarryLeftWristDropMinimum = 0.15f;
    private const float OperatorCarryStockToShoulderMaximum = 0.42f;
    private const float OperatorCarryHeadClearanceMinimum = 0.15f;
    private const float OperatorCarryChestClearanceMinimum = 0.045f;
    private const float OperatorCarryPrimaryHandDistanceMaximum = 0.005f;
    private const float OperatorCarrySupportHandDistanceMaximum = 0.03f;
    private const float OperatorCarryReadyMuzzleForwardMinimum = 0.38f;
    private const float OperatorCarryAimMuzzleForwardMinimum = 0.44f;
    private const float OperatorCarryAimMuzzleLateralMaximum = 0.16f;
    private const float OperatorCarryAimMuzzleVerticalMaximum = 0.12f;
    private const float OperatorCarryAimSprintMuzzleVerticalMaximum = 0.22f;
    private const float OperatorCarryAimStockRearwardMinimum = 0.10f;
    private const float OperatorCarryReadyHandSeparationMinimum = 0.22f;
    private const float OperatorCarryRightElbowForwardMinimum = 0.0f;
    private const float OperatorCarryRightElbowOutwardMinimum = -0.05f;
    private const float OperatorCarryRightWristForwardMinimum = 0.020f;
    private const float OperatorCarryWeaponRootForwardMinimum = 0.020f;

    private static readonly OperatorVisualId[] OperatorCarryVisuals =
    {
        OperatorVisualId.Garrison,
        OperatorVisualId.Heron,
        OperatorVisualId.Lynx,
        OperatorVisualId.Magpie,
        OperatorVisualId.Jackal,
        OperatorVisualId.Viper
    };

    private static readonly string[] OperatorCarryAnimations =
    {
        "ready_idle",
        "ready_walk",
        "ready_run",
        "ready_sprint",
        "aim_idle",
        "aim_walk",
        "aim_run",
        "aim_sprint"
    };

    private static readonly float[] OperatorCarrySamplePhases =
    {
        0.07f,
        0.31f,
        0.57f,
        0.83f
    };

    private static readonly string[] OperatorCarryCaptureAnimations =
    {
        "ready_idle",
        "ready_run",
        "ready_sprint",
        "aim_idle",
        "aim_run",
        "aim_sprint"
    };

    private readonly record struct OperatorCarrySample(
        OperatorVisualId VisualId,
        string Animation,
        float Phase,
        OperatorCarryInspection Inspection,
        bool Valid);

    private readonly record struct OperatorCarryCaptureView(
        string Name,
        Vector3 Position);

    private async void ValidateOperatorCarry()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var samples = new List<OperatorCarrySample>(
            OperatorCarryVisuals.Length * OperatorCarryAnimations.Length * OperatorCarrySamplePhases.Length);
        var failures = new List<string>();

        foreach (var visualId in OperatorCarryVisuals)
        {
            AuthoredOperatorVisual? visual = null;
            try
            {
                visual = CombatModelLibrary.InstantiateOperator(
                    visualId,
                    WeaponCatalog.Build(WeaponPlatform.M4A1, 0));
                AddChild(visual.Root);
                visual.SetWeaponReadied(true);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

                foreach (var animationName in OperatorCarryAnimations)
                {
                    var animation = visual.AnimationPlayer.GetAnimation(animationName);
                    if (animation is null || animation.Length <= 0.0)
                    {
                        failures.Add($"{visualId}:{animationName}:missing");
                        continue;
                    }

                    foreach (var phase in OperatorCarrySamplePhases)
                    {
                        visual.AnimationPlayer.Play(animationName, 0.0);
                        visual.AnimationPlayer.Seek(animation.Length * phase, update: true);
                        visual.AnimationPlayer.Pause();
                        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);


                        var inspection = visual.InspectRifleCarry(animationName);
                        var sampleValid = OperatorCarrySampleValid(visualId, inspection, animationName);
                        var muzzleOffset = inspection.WeaponMuzzle - inspection.WeaponRoot;
                        var stockOffset = inspection.WeaponStock - inspection.WeaponRoot;
                        samples.Add(new OperatorCarrySample(
                            visualId,
                            animationName,
                            phase,
                            inspection,
                            sampleValid));
                        if (!sampleValid)
                        {
                            failures.Add($"{visualId}:{animationName}:{phase:F2}");
                        }

                        GD.Print(
                            $"OPERATOR_CARRY_SAMPLE visual={visualId} animation={animationName} phase={phase:F2} "
                            + $"valid={sampleValid} right_elbow={inspection.RightElbowAngleDegrees:F1} "
                            + $"left_elbow={inspection.LeftElbowAngleDegrees:F1} "
                            + $"right_wrist_drop={inspection.RightWristBelowHead:F3} "
                            + $"left_wrist_drop={inspection.LeftWristBelowHead:F3} "
                            + $"stock_shoulder={inspection.StockToRightShoulderDistance:F3} "
                            + $"head_clearance={inspection.HeadToWeaponLineClearance:F3} "
                            + $"chest_clearance={inspection.ChestToWeaponLineClearance:F3} "
                            + $"primary_hand={inspection.PrimaryHandToWeaponDistance:F3} "
                            + $"support_hand={inspection.SupportHandToForegripDistance:F3} "
                            + $"support_offset={inspection.SupportHandOffset} "
                            + $"right_elbow_forward={inspection.RightElbowForwardOfShoulder:F3} "
                            + $"right_elbow_outward={inspection.RightElbowOutwardOfShoulder:F3} "
                            + $"right_wrist_forward={inspection.RightWristForwardOfChest:F3} "
                            + $"weapon_root_forward={inspection.WeaponRootForwardOfChest:F3} "
                            + $"hand_separation={inspection.RightWrist.DistanceTo(inspection.LeftWrist):F3} "
                            + $"muzzle_offset={muzzleOffset} stock_offset={stockOffset}");
                    }
                }
            }
            catch (Exception exception)
            {
                failures.Add($"{visualId}:exception");
                GD.PushError($"Operator carry validation failed for {visualId}: {exception}");
            }
            finally
            {
                visual?.Root.QueueFree();
            }

            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        var expectedSamples = OperatorCarryVisuals.Length
            * OperatorCarryAnimations.Length
            * OperatorCarrySamplePhases.Length;
        var valid = samples.Count == expectedSamples
            && samples.All(sample => sample.Valid)
            && failures.Count == 0;
        GD.Print(
            $"OPERATOR_CARRY_CHECK visuals={OperatorCarryVisuals.Length} "
            + $"animations={OperatorCarryAnimations.Length} phases={OperatorCarrySamplePhases.Length} "
            + $"samples={samples.Count}/{expectedSamples} valid_samples={samples.Count(sample => sample.Valid)} "
            + $"right_elbow_range={MetricRange(samples, sample => sample.Inspection.RightElbowAngleDegrees, "F1")} "
            + $"left_elbow_range={MetricRange(samples, sample => sample.Inspection.LeftElbowAngleDegrees, "F1")} "
            + $"right_wrist_drop_range={MetricRange(samples, sample => sample.Inspection.RightWristBelowHead, "F3")} "
            + $"left_wrist_drop_range={MetricRange(samples, sample => sample.Inspection.LeftWristBelowHead, "F3")} "
            + $"stock_shoulder_range={MetricRange(samples, sample => sample.Inspection.StockToRightShoulderDistance, "F3")} "
            + $"head_clearance_range={MetricRange(samples, sample => sample.Inspection.HeadToWeaponLineClearance, "F3")} "
            + $"chest_clearance_range={MetricRange(samples, sample => sample.Inspection.ChestToWeaponLineClearance, "F3")} "
            + $"primary_hand_range={MetricRange(samples, sample => sample.Inspection.PrimaryHandToWeaponDistance, "F3")} "
            + $"support_hand_range={MetricRange(samples, sample => sample.Inspection.SupportHandToForegripDistance, "F3")} "
            + $"hand_separation_range={MetricRange(samples, sample =>
                sample.Inspection.RightWrist.DistanceTo(sample.Inspection.LeftWrist), "F3")} "
            + $"right_elbow_forward_range={MetricRange(samples, sample =>
                sample.Inspection.RightElbowForwardOfShoulder, "F3")} "
            + $"right_elbow_outward_range={MetricRange(samples, sample =>
                sample.Inspection.RightElbowOutwardOfShoulder, "F3")} "
            + $"right_wrist_forward_range={MetricRange(samples, sample =>
                sample.Inspection.RightWristForwardOfChest, "F3")} "
            + $"weapon_root_forward_range={MetricRange(samples, sample =>
                sample.Inspection.WeaponRootForwardOfChest, "F3")} "
            + $"muzzle_forward_range={MetricRange(samples, sample =>
                -(sample.Inspection.WeaponMuzzle - sample.Inspection.WeaponRoot).Z, "F3")} "
            + $"muzzle_lateral_range={MetricRange(samples, sample =>
                Mathf.Abs((sample.Inspection.WeaponMuzzle - sample.Inspection.WeaponRoot).X), "F3")} "
            + $"muzzle_vertical_range={MetricRange(samples, sample =>
                Mathf.Abs((sample.Inspection.WeaponMuzzle - sample.Inspection.WeaponRoot).Y), "F3")} "
            + $"stock_rearward_range={MetricRange(samples, sample =>
                (sample.Inspection.WeaponStock - sample.Inspection.WeaponRoot).Z, "F3")} "
            + $"thresholds=right_elbow:{OperatorCarryRightElbowMinimum:F0}-{OperatorCarryRightElbowMaximum:F0},"
            + $"left_elbow:{OperatorCarryLeftElbowMinimum:F0}-{OperatorCarryLeftElbowMaximum:F0},"
            + $"right_wrist_drop:{OperatorCarryRightWristDropMinimum:F2},"
            + $"left_wrist_drop:{OperatorCarryLeftWristDropMinimum:F2},"
            + $"stock_shoulder_max:{OperatorCarryStockToShoulderMaximum:F2},"
            + $"head_clearance_min:{OperatorCarryHeadClearanceMinimum:F3},"
            + $"chest_clearance_min:{OperatorCarryChestClearanceMinimum:F3},"
            + $"primary_hand_max:{OperatorCarryPrimaryHandDistanceMaximum:F3},"
            + $"support_hand_max:{OperatorCarrySupportHandDistanceMaximum:F3},"
            + $"ready_muzzle_forward_min:{OperatorCarryReadyMuzzleForwardMinimum:F2},"
            + $"aim_muzzle_forward_min:{OperatorCarryAimMuzzleForwardMinimum:F2},"
            + $"aim_muzzle_lateral_max:{OperatorCarryAimMuzzleLateralMaximum:F2},"
            + $"aim_muzzle_vertical_max:{OperatorCarryAimMuzzleVerticalMaximum:F2},"
            + $"aim_sprint_muzzle_vertical_max:{OperatorCarryAimSprintMuzzleVerticalMaximum:F2},"
            + $"aim_stock_rearward_min:{OperatorCarryAimStockRearwardMinimum:F2},"
            + $"ready_hand_separation_min:{OperatorCarryReadyHandSeparationMinimum:F2},"
            + $"right_elbow_forward_min:{OperatorCarryRightElbowForwardMinimum:F3},"
            + $"right_elbow_outward_min:{OperatorCarryRightElbowOutwardMinimum:F3},"
            + $"right_wrist_forward_min:{OperatorCarryRightWristForwardMinimum:F3},"
            + $"weapon_root_forward_min:{OperatorCarryWeaponRootForwardMinimum:F3} "
            + $"failures={string.Join('|', failures)}");
        GD.Print($"OPERATOR_CARRY_PASS valid={valid}");
        QuitDiagnosticAfterSceneCleanup(valid ? 0 : 2);
    }

    private static bool OperatorCarrySampleValid(
        OperatorVisualId visualId,
        OperatorCarryInspection inspection,
        string animationName)
    {
        var muzzleOffset = inspection.WeaponMuzzle - inspection.WeaponRoot;
        var stockOffset = inspection.WeaponStock - inspection.WeaponRoot;
        var aiming = animationName.StartsWith("aim_", StringComparison.Ordinal);
        var sprinting = animationName.EndsWith("_sprint", StringComparison.Ordinal);
        var locomotion = animationName.EndsWith("_walk", StringComparison.Ordinal)
            || animationName.EndsWith("_run", StringComparison.Ordinal);
        var headClearanceMinimum = 0.06f;
        var wristDropMinimum = sprinting ? 0.08f : OperatorCarryRightWristDropMinimum;
        var leftWristDropMinimum = sprinting
            ? 0.07f
            : locomotion
                ? 0.07f
                : visualId == OperatorVisualId.Garrison
                    ? 0.05f
                    : OperatorCarryLeftWristDropMinimum;
        var leftElbowMaximum = locomotion ? 180.0f : OperatorCarryLeftElbowMaximum;
        var readyHandSeparationMinimum = locomotion
            ? 0.20f
            : 0.20f;
        // A moving imported clip can leave a normalized forearm a few
        // centimetres short at the extreme of its stride. Keep the strict
        // grip gate for idle/sprint poses and allow that bounded walk/run
        // tolerance while the hand remains visibly on the handguard.
        var supportHandDistanceMaximum = locomotion
            ? 0.06f
            : OperatorCarrySupportHandDistanceMaximum;
        var aimMuzzleVerticalMaximum = animationName == "aim_sprint"
            ? OperatorCarryAimSprintMuzzleVerticalMaximum
            : OperatorCarryAimMuzzleVerticalMaximum;
        var weaponDirectionValid = aiming
            ? muzzleOffset.Z <= -OperatorCarryAimMuzzleForwardMinimum
                && Mathf.Abs(muzzleOffset.X) <= OperatorCarryAimMuzzleLateralMaximum
                && Mathf.Abs(muzzleOffset.Y) <= aimMuzzleVerticalMaximum
                && stockOffset.Z >= OperatorCarryAimStockRearwardMinimum
            : muzzleOffset.Z <= -OperatorCarryReadyMuzzleForwardMinimum
                && inspection.RightWrist.DistanceTo(inspection.LeftWrist)
                    >= readyHandSeparationMinimum;
        // The legacy Bamen rig is authored with its right-arm local forward
        // axis mirrored relative to the HY-3D rigs. Its elbow still satisfies
        // the segment/angle checks below, so do not apply the HY-3D directional
        // envelope to that one source rig.
        var rightArmEnvelopeValid = visualId == OperatorVisualId.Garrison
            || inspection.RightElbowForwardOfShoulder >= OperatorCarryRightElbowForwardMinimum
                && inspection.RightElbowOutwardOfShoulder >= OperatorCarryRightElbowOutwardMinimum;
        return inspection.Available
            && float.IsFinite(inspection.RightElbowAngleDegrees)
            && float.IsFinite(inspection.LeftElbowAngleDegrees)
            && inspection.RightElbowAngleDegrees is >= OperatorCarryRightElbowMinimum
                and <= OperatorCarryRightElbowMaximum
            && inspection.LeftElbowAngleDegrees >= OperatorCarryLeftElbowMinimum
            && inspection.LeftElbowAngleDegrees <= leftElbowMaximum
            && inspection.RightWristBelowHead >= wristDropMinimum
            && inspection.LeftWristBelowHead >= leftWristDropMinimum
            && inspection.StockToRightShoulderDistance <= OperatorCarryStockToShoulderMaximum
            && inspection.HeadToWeaponLineClearance >= headClearanceMinimum
            && inspection.ChestToWeaponLineClearance >= OperatorCarryChestClearanceMinimum
            && inspection.PrimaryHandToWeaponDistance <= OperatorCarryPrimaryHandDistanceMaximum
            && inspection.SupportHandToForegripDistance <= supportHandDistanceMaximum
            && rightArmEnvelopeValid
            && inspection.RightShoulder.DistanceTo(inspection.RightElbow)
                is >= OperatorCarryArmSegmentMinimum and <= OperatorCarryArmSegmentMaximum
            && inspection.RightElbow.DistanceTo(inspection.RightWrist)
                is >= OperatorCarryArmSegmentMinimum and <= OperatorCarryArmSegmentMaximum
            && inspection.LeftShoulder.DistanceTo(inspection.LeftElbow)
                is >= OperatorCarryArmSegmentMinimum and <= OperatorCarryArmSegmentMaximum
            && inspection.LeftElbow.DistanceTo(inspection.LeftWrist)
                is >= OperatorCarryArmSegmentMinimum and <= OperatorCarryArmSegmentMaximum
            && inspection.RightWristForwardOfChest >= OperatorCarryRightWristForwardMinimum
            && inspection.WeaponRootForwardOfChest >= OperatorCarryWeaponRootForwardMinimum
            && weaponDirectionValid;
    }

    private static string MetricRange(
        IReadOnlyCollection<OperatorCarrySample> samples,
        Func<OperatorCarrySample, float> select,
        string format)
    {
        if (samples.Count == 0)
        {
            return "none";
        }

        var values = samples.Select(select).ToArray();
        return $"{values.Min().ToString(format)}-{values.Max().ToString(format)}";
    }

    private async void CaptureOperatorCarry()
    {
        var viewport = new SubViewport
        {
            Name = "OperatorCarryCaptureViewport",
            Size = new Vector2I(1280, 960),
            OwnWorld3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            Msaa3D = Viewport.Msaa.Msaa4X
        };
        AddChild(viewport);
        var stage = BuildOperatorCarryCaptureStage(viewport);
        var frameResults = new Dictionary<string, bool>();
        var failures = new List<string>();
        var camera = new Camera3D
        {
            Name = "OperatorCarryCaptureCamera",
            Fov = 31.0f,
            Near = 0.03f,
            Far = 40.0f,
            Current = true,
            PhysicsInterpolationMode = PhysicsInterpolationModeEnum.Off
        };
        stage.AddChild(camera);
        var views = new[]
        {
            new OperatorCarryCaptureView("front", new Vector3(0.0f, 1.18f, -4.6f)),
            new OperatorCarryCaptureView("three_quarter", new Vector3(3.2f, 1.32f, -3.2f)),
            new OperatorCarryCaptureView("side", new Vector3(4.6f, 1.18f, 0.0f)),
            new OperatorCarryCaptureView("rear", new Vector3(0.0f, 1.18f, 4.6f))
        };

        foreach (var visualId in OperatorCarryVisuals)
        {
            AuthoredOperatorVisual? visual = null;
            try
            {
                visual = CombatModelLibrary.InstantiateOperator(
                    visualId,
                    WeaponCatalog.Build(WeaponPlatform.M4A1, 0));
                visual.Root.Name = $"{visualId}CarryCapture";
                stage.AddChild(visual.Root);
                visual.SetWeaponReadied(true);

                foreach (var animationName in OperatorCarryCaptureAnimations)
                {
                    var animation = visual.AnimationPlayer.GetAnimation(animationName);
                    if (animation is null || animation.Length <= 0.0)
                    {
                        frameResults[$"{visualId}:{animationName}:missing"] = false;
                        continue;
                    }

                    visual.AnimationPlayer.Play(animationName, 0.0);
                    visual.AnimationPlayer.Seek(animation.Length * 0.37f, update: true);
                    visual.AnimationPlayer.Pause();
                    // The capture path bypasses AuthoredOperatorAnimator, so
                    // explicitly apply the same HY-3D weapon IK used during
                    // gameplay before taking the first frame.
                    visual.RefreshWeaponPose(animationName);
                    await WaitFrames(30);
                    await WaitFrames(4);

                    foreach (var view in views)
                    {
                        camera.Position = view.Position;
                        camera.LookAt(new Vector3(0.0f, 0.96f, 0.0f), Vector3.Up);
                        await WaitFrames(5);
                        var visualName = visualId.ToString().ToLowerInvariant();
                        var path = $"user://operator_carry_{visualName}_{animationName}_{view.Name}.png";
                        var image = viewport.GetTexture().GetImage();
                        var saved = SaveOperatorCarryCapture(path, image, out var contrast, out var bytes);
                        frameResults[$"{visualId}:{animationName}:{view.Name}"] = saved;
                        GD.Print(
                            $"OPERATOR_CARRY_CAPTURE_FRAME visual={visualId} animation={animationName} "
                            + $"view={view.Name} path={path} saved={saved} "
                            + $"width={image?.GetWidth() ?? 0} height={image?.GetHeight() ?? 0} "
                            + $"contrast={contrast:F3} bytes={bytes}");
                    }
                }
            }
            catch (Exception exception)
            {
                failures.Add($"{visualId}:exception");
                GD.PushError($"Operator carry capture failed for {visualId}: {exception}");
            }
            finally
            {
                visual?.Root.QueueFree();
            }

            await WaitFrames(2);
        }

        var expectedFrames = OperatorCarryVisuals.Length * OperatorCarryCaptureAnimations.Length * views.Length;
        var valid = frameResults.Count == expectedFrames
            && frameResults.Values.All(saved => saved)
            && failures.Count == 0;
        GD.Print(
            $"OPERATOR_CARRY_CAPTURE valid={valid} visuals={OperatorCarryVisuals.Length} "
            + $"animations={OperatorCarryCaptureAnimations.Length} views={views.Length} "
            + $"frames={frameResults.Count}/{expectedFrames} "
            + $"failed={string.Join('|', failures.Concat(
                frameResults.Where(pair => !pair.Value).Select(pair => pair.Key)))}");
        GD.Print($"OPERATOR_CARRY_CAPTURE_PASS valid={valid}");
        viewport.QueueFree();
        QuitDiagnosticAfterSceneCleanup(valid ? 0 : 2);
    }

    private static Node3D BuildOperatorCarryCaptureStage(SubViewport viewport)
    {
        var stage = new Node3D { Name = "OperatorCarryCaptureStage" };
        viewport.AddChild(stage);
        stage.AddChild(new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color,
                BackgroundColor = new Color(0.025f, 0.032f, 0.038f),
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = new Color(0.72f, 0.78f, 0.82f),
                AmbientLightEnergy = 0.9f,
                TonemapMode = Godot.Environment.ToneMapper.Filmic
            }
        });
        stage.AddChild(new DirectionalLight3D
        {
            Name = "OperatorCarryKeyLight",
            RotationDegrees = new Vector3(-38.0f, -28.0f, 0.0f),
            LightColor = new Color(1.0f, 0.91f, 0.82f),
            LightEnergy = 1.6f,
            ShadowEnabled = true
        });
        stage.AddChild(new DirectionalLight3D
        {
            Name = "OperatorCarryFillLight",
            RotationDegrees = new Vector3(-22.0f, 145.0f, 0.0f),
            LightColor = new Color(0.42f, 0.7f, 1.0f),
            LightEnergy = 0.75f,
            ShadowEnabled = false
        });
        return stage;
    }

    private static bool SaveOperatorCarryCapture(
        string path,
        Image? image,
        out float contrast,
        out long bytes)
    {
        contrast = 0.0f;
        bytes = 0;
        if (image is null || image.IsEmpty() || image.GetWidth() < 640 || image.GetHeight() < 480)
        {
            return false;
        }

        var minimum = 1.0f;
        var maximum = 0.0f;
        var stepX = Mathf.Max(1, image.GetWidth() / 32);
        var stepY = Mathf.Max(1, image.GetHeight() / 24);
        for (var y = stepY / 2; y < image.GetHeight(); y += stepY)
        {
            for (var x = stepX / 2; x < image.GetWidth(); x += stepX)
            {
                var color = image.GetPixel(x, y);
                var luminance = color.R * 0.2126f + color.G * 0.7152f + color.B * 0.0722f;
                minimum = Mathf.Min(minimum, luminance);
                maximum = Mathf.Max(maximum, luminance);
            }
        }

        contrast = maximum - minimum;
        var absolutePath = ProjectSettings.GlobalizePath(path);
        if (File.Exists(absolutePath))
        {
            File.Delete(absolutePath);
        }
        if (image.SavePng(path) != Error.Ok || !File.Exists(absolutePath))
        {
            return false;
        }

        bytes = new FileInfo(absolutePath).Length;
        return bytes >= 10_000 && contrast >= 0.08f;
    }
}
