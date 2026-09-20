using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class OperatorPresentationDiagnostics
{
    private static void CheckCombatPosture(OperatorVisualId id, AuthoredOperatorVisual visual, List<string> failures)
    {
        var skeleton = Descendants<Skeleton3D>(visual.Root).First();
        var hips = skeleton.FindBone("Hips");
        var hands = new[] { skeleton.FindBone("LeftHand"), skeleton.FindBone("RightHand") };
        void Pose(string name, double phase)
        {
            visual.AnimationPlayer.Play(name, 0);
            visual.AnimationPlayer.Seek(visual.AnimationPlayer.GetAnimation(name).Length * phase, update: true);
            visual.AnimationPlayer.Pause();
        }
        Pose("aim_idle", 0);
        var standingHeight = skeleton.GetBoneGlobalPose(hips).Origin.Y;
        var minimumRatio = 1.0f;
        foreach (var pose in new[] { "run", "sprint", "aim_run", "ready_run" })
        {
            for (var sample = 0; sample <= 24; sample++)
            {
                Pose(pose, sample / 24.0);
                minimumRatio = Mathf.Min(minimumRatio, skeleton.GetBoneGlobalPose(hips).Origin.Y / standingHeight);
            }
        }
        var maximumTravel = 0.0f;
        var maximumEndpoint = 0.0f;
        foreach (var prefix in new[] { "", "pistol_" })
        {
            Pose(prefix + "aim_idle", 0);
            var reference = hands.Select(bone => skeleton.GetBoneGlobalPose(bone).Origin).ToArray();
            for (var sample = 0; sample <= 24; sample++)
            {
                Pose(prefix + "shoot", sample / 24.0);
                for (var index = 0; index < hands.Length; index++)
                {
                    var travel = skeleton.GetBoneGlobalPose(hands[index]).Origin.DistanceTo(reference[index]);
                    maximumTravel = Mathf.Max(maximumTravel, travel);
                    if (sample is 0 or 24) maximumEndpoint = Mathf.Max(maximumEndpoint, travel);
                }
            }
        }
        var distinctGuard = id != OperatorVisualId.Garrison
            || Descendants<MeshInstance3D>(visual.Root).Any(mesh => mesh.Name.ToString() == "GarrisonHelmet");
        var valid = minimumRatio >= .90f && maximumTravel is > .003f and < .045f
            && maximumEndpoint < .01f && distinctGuard;
        GD.Print($"OPERATOR_COMBAT_POSTURE_CHECK visual={id} hips_ratio={minimumRatio:F3} recoil_travel={maximumTravel:F4} recoil_endpoint={maximumEndpoint:F4} identity={distinctGuard} valid={valid}");
        if (!valid) failures.Add($"{id}:combat-posture");
    }

    private static void CheckMagpieArmCoverage(List<Surface> surfaces, List<string> failures)
    {
        // A normalized skin and correct grip socket can still have no forearm.
        // Sample actual rest-surface coverage along each anatomical forearm.
        var skeleton = surfaces[0].Skeleton;
        var points = surfaces.SelectMany(surface => DeformedVertices(surface, useRest: true)).ToArray();
        foreach (var side in new[] { "Left", "Right" })
        {
            Vector3 RestPosition(string suffix)
            {
                var bone = Enumerable.Range(0, skeleton.GetBoneCount()).Single(index =>
                    skeleton.GetBoneName(index).ToString().EndsWith(side + suffix, StringComparison.Ordinal));
                return skeleton.GlobalTransform * skeleton.GetBoneGlobalRest(bone).Origin;
            }
            var elbow = RestPosition("ForeArm");
            var wrist = RestPosition("Hand");
            foreach (var fraction in new[] { .25f, .5f, .75f })
            {
                var center = elbow.Lerp(wrist, fraction);
                var count = points.Count(point => point.DistanceSquaredTo(center) < .05f * .05f);
                var valid = count >= 24;
                GD.Print($"OPERATOR_ARM_COVERAGE_CHECK visual=Magpie side={side} fraction={fraction:F2} vertices={count} valid={valid}");
                if (!valid) failures.Add($"Magpie:{side}:missing-forearm-surface:{fraction:F2}:vertices={count}");
            }
        }
    }
}
