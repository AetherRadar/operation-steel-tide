using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OperationSteelTide;

/// <summary>
/// Deterministic skeletal-scale/deformation gate for the authored HY-3D operators.
/// This intentionally does not inspect gameplay state or weapon fitting; it samples
/// every shipped clip at fixed phases and verifies the major body segments remain
/// finite and within human-scale bounds after Godot evaluates the animation.
/// </summary>
public partial class FreightTerminalWorld
{
    private static readonly OperatorVisualId[] OperatorDeformationVisuals =
    {
        OperatorVisualId.Heron,
        OperatorVisualId.Lynx,
        OperatorVisualId.Magpie,
        OperatorVisualId.Jackal,
        OperatorVisualId.Viper
    };

    private static readonly string[] OperatorDeformationAnimations =
    {
        "idle", "walk", "run", "sprint", "crouch_idle", "crouch_walk",
        "ready_idle", "ready_walk", "ready_run", "ready_sprint",
        "ready_crouch_idle", "ready_crouch_walk",
        "aim_walk", "aim_run", "aim_sprint", "aim_crouch_idle", "aim_crouch_walk",
        "prone_idle", "prone_crawl", "aim_idle", "hit", "death", "downed",
        "revive_kneel", "revived"
    };

    private static readonly float[] OperatorDeformationPhases = { 0.07f, 0.31f, 0.57f, 0.83f };

    private readonly record struct OperatorDeformationSample(
        OperatorVisualId VisualId,
        string Animation,
        float Phase,
        float BodyLength,
        float UpperArm,
        float Forearm,
        float Thigh,
        float Shin,
        bool Finite,
        bool InScale);

    private async void ValidateOperatorDeformation()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var samples = new List<OperatorDeformationSample>(
            OperatorDeformationVisuals.Length * OperatorDeformationAnimations.Length * OperatorDeformationPhases.Length);
        var failures = new List<string>();

        foreach (var visualId in OperatorDeformationVisuals)
        {
            AuthoredOperatorVisual? visual = null;
            try
            {
                // No weapon is needed for this gate: keeping the sample focused on
                // the imported skin and skeleton avoids coupling it to carry IK.
                visual = CombatModelLibrary.InstantiateOperator(
                    visualId,
                    weaponBuild: null,
                    attachDefaultWeapon: false);
                AddChild(visual.Root);
                var skeleton = FindOperatorSkeleton(visual.Root);
                if (skeleton is null)
                {
                    failures.Add($"{visualId}:missing-skeleton");
                    continue;
                }

                var bones = ResolveDeformationBones(skeleton);
                if (!bones.IsComplete)
                {
                    failures.Add($"{visualId}:missing-major-bones");
                    continue;
                }
                var restHips = BonePosition(skeleton, bones.Hips);
                var restHead = BonePosition(skeleton, bones.Head);
                GD.Print(
                    $"OPERATOR_DEFORMATION_SKELETON visual={visualId} "
                    + $"global_scale={skeleton.GlobalTransform.Basis.Scale} "
                    + $"hips={restHips} head={restHead} "
                    + $"hips_head={restHips.DistanceTo(restHead):F3}");

                foreach (var animationName in OperatorDeformationAnimations)
                {
                    var animation = visual.AnimationPlayer.GetAnimation(animationName);
                    if (animation is null || animation.Length <= 0.0)
                    {
                        failures.Add($"{visualId}:{animationName}:missing");
                        continue;
                    }

                    foreach (var phase in OperatorDeformationPhases)
                    {
                        visual.AnimationPlayer.Play(animationName, 0.0);
                        visual.AnimationPlayer.Seek(animation.Length * phase, update: true);
                        visual.AnimationPlayer.Pause();
                        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

                        var sample = MeasureDeformation(visualId, animationName, phase, skeleton, bones);
                        samples.Add(sample);
                        if (!sample.Finite || !sample.InScale)
                        {
                            failures.Add($"{visualId}:{animationName}:{phase:F2}");
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                failures.Add($"{visualId}:exception");
                GD.PushError($"Operator deformation validation failed for {visualId}: {exception}");
            }
            finally
            {
                visual?.Root.QueueFree();
            }

            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        var expectedSamples = OperatorDeformationVisuals.Length
            * OperatorDeformationAnimations.Length * OperatorDeformationPhases.Length;
        var valid = samples.Count == expectedSamples
            && samples.All(sample => sample.Finite && sample.InScale)
            && failures.Count == 0;
        GD.Print(
            $"OPERATOR_DEFORMATION_CHECK visuals={OperatorDeformationVisuals.Length} "
            + $"animations={OperatorDeformationAnimations.Length} phases={OperatorDeformationPhases.Length} "
            + $"samples={samples.Count}/{expectedSamples} finite={samples.Count(s => s.Finite)} "
            + $"in_scale={samples.Count(s => s.InScale)} "
            + $"body_length_range={DeformationMetricRange(samples, s => s.BodyLength, "F3")} "
            + $"upper_arm_range={DeformationMetricRange(samples, s => s.UpperArm, "F3")} "
            + $"forearm_range={DeformationMetricRange(samples, s => s.Forearm, "F3")} "
            + $"thigh_range={DeformationMetricRange(samples, s => s.Thigh, "F3")} "
            + $"shin_range={DeformationMetricRange(samples, s => s.Shin, "F3")} "
            + "bounds=body:0.28-0.85,segments:0.12-0.65 "
            + $"failures={string.Join('|', failures)}");
        GD.Print($"OPERATOR_DEFORMATION_PASS valid={valid}");
        QuitDiagnosticAfterSceneCleanup(valid ? 0 : 2);
    }

    private static Skeleton3D? FindOperatorSkeleton(Node root)
    {
        if (root is Skeleton3D skeleton)
        {
            return skeleton;
        }
        foreach (var child in root.GetChildren())
        {
            if (child is Node node && FindOperatorSkeleton(node) is { } nested)
            {
                return nested;
            }
        }
        return null;
    }

    private readonly record struct DeformationBones(
        int Hips, int Head, int RightArm, int RightForeArm, int RightHand,
        int LeftArm, int LeftForeArm, int LeftHand,
        int RightUpLeg, int RightLeg, int RightFoot, int LeftUpLeg, int LeftLeg, int LeftFoot)
    {
        public bool IsComplete => Hips >= 0 && Head >= 0
            && RightArm >= 0 && RightForeArm >= 0 && RightHand >= 0
            && LeftArm >= 0 && LeftForeArm >= 0 && LeftHand >= 0
            && RightUpLeg >= 0 && RightLeg >= 0 && RightFoot >= 0
            && LeftUpLeg >= 0 && LeftLeg >= 0 && LeftFoot >= 0;
    }

    private static DeformationBones ResolveDeformationBones(Skeleton3D skeleton)
        => new(
            ResolveBone(skeleton, "Hips"),
            ResolveBone(skeleton, "Head"),
            ResolveBone(skeleton, "RightArm"),
            ResolveBone(skeleton, "RightForeArm"),
            ResolveBone(skeleton, "RightHand"),
            ResolveBone(skeleton, "LeftArm"),
            ResolveBone(skeleton, "LeftForeArm"),
            ResolveBone(skeleton, "LeftHand"),
            ResolveBone(skeleton, "RightUpLeg"),
            ResolveBone(skeleton, "RightLeg"),
            ResolveBone(skeleton, "RightFoot"),
            ResolveBone(skeleton, "LeftUpLeg"),
            ResolveBone(skeleton, "LeftLeg"),
            ResolveBone(skeleton, "LeftFoot"));

    private static int ResolveBone(Skeleton3D skeleton, string suffix)
    {
        for (var index = 0; index < skeleton.GetBoneCount(); index++)
        {
            if (skeleton.GetBoneName(index).ToString().EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }
        return -1;
    }

    private static OperatorDeformationSample MeasureDeformation(
        OperatorVisualId visualId,
        string animation,
        float phase,
        Skeleton3D skeleton,
        DeformationBones bones)
    {
        var hips = BonePosition(skeleton, bones.Hips);
        var head = BonePosition(skeleton, bones.Head);
        var rightArm = BonePosition(skeleton, bones.RightArm);
        var rightForeArm = BonePosition(skeleton, bones.RightForeArm);
        var rightHand = BonePosition(skeleton, bones.RightHand);
        var leftArm = BonePosition(skeleton, bones.LeftArm);
        var leftForeArm = BonePosition(skeleton, bones.LeftForeArm);
        var leftHand = BonePosition(skeleton, bones.LeftHand);
        var rightUpLeg = BonePosition(skeleton, bones.RightUpLeg);
        var rightLeg = BonePosition(skeleton, bones.RightLeg);
        var rightFoot = BonePosition(skeleton, bones.RightFoot);
        var leftUpLeg = BonePosition(skeleton, bones.LeftUpLeg);
        var leftLeg = BonePosition(skeleton, bones.LeftLeg);
        var leftFoot = BonePosition(skeleton, bones.LeftFoot);

        var bodyLength = hips.DistanceTo(head);
        var upperArm = (rightArm.DistanceTo(rightForeArm) + leftArm.DistanceTo(leftForeArm)) * 0.5f;
        var forearm = (rightForeArm.DistanceTo(rightHand) + leftForeArm.DistanceTo(leftHand)) * 0.5f;
        var thigh = (rightUpLeg.DistanceTo(rightLeg) + leftUpLeg.DistanceTo(leftLeg)) * 0.5f;
        var shin = (rightLeg.DistanceTo(rightFoot) + leftLeg.DistanceTo(leftFoot)) * 0.5f;
        var finite = IsFinite(bodyLength, upperArm, forearm, thigh, shin)
            && IsFinite(hips, head, rightArm, rightForeArm, rightHand, leftArm, leftForeArm, leftHand,
                rightUpLeg, rightLeg, rightFoot, leftUpLeg, leftLeg, leftFoot);
        var inScale = bodyLength is >= 0.28f and <= 0.85f
            && IsSegmentInScale(upperArm) && IsSegmentInScale(forearm)
            && IsSegmentInScale(thigh) && IsSegmentInScale(shin)
            && IsSegmentInScale(rightArm.DistanceTo(rightForeArm))
            && IsSegmentInScale(leftArm.DistanceTo(leftForeArm))
            && IsSegmentInScale(rightForeArm.DistanceTo(rightHand))
            && IsSegmentInScale(leftForeArm.DistanceTo(leftHand))
            && IsSegmentInScale(rightUpLeg.DistanceTo(rightLeg))
            && IsSegmentInScale(leftUpLeg.DistanceTo(leftLeg))
            && IsSegmentInScale(rightLeg.DistanceTo(rightFoot))
            && IsSegmentInScale(leftLeg.DistanceTo(leftFoot));
        return new OperatorDeformationSample(
            visualId, animation, phase, bodyLength, upperArm, forearm, thigh, shin, finite, inScale);
    }

    private static Vector3 BonePosition(Skeleton3D skeleton, int index)
        => (skeleton.GlobalTransform * skeleton.GetBoneGlobalPose(index)).Origin;

    private static bool IsSegmentInScale(float value)
        => float.IsFinite(value) && value >= 0.12f && value <= 0.65f;

    private static bool IsFinite(params float[] values)
        => values.All(float.IsFinite);

    private static bool IsFinite(params Vector3[] values)
        => values.All(value => float.IsFinite(value.X)
            && float.IsFinite(value.Y) && float.IsFinite(value.Z));

    private static string DeformationMetricRange(
        IReadOnlyCollection<OperatorDeformationSample> samples,
        Func<OperatorDeformationSample, float> select,
        string format)
    {
        if (samples.Count == 0)
        {
            return "none";
        }
        var values = samples.Select(select).ToArray();
        return $"{values.Min().ToString(format)}-{values.Max().ToString(format)}";
    }
}
