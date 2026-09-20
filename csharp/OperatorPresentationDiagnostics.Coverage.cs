using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class OperatorPresentationDiagnostics
{
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
