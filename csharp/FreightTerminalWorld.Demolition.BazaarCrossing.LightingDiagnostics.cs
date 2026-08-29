using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private readonly record struct BazaarInteriorLightingCheck(
        bool Ready,
        int LightCount,
        string Failures);

    private static BazaarInteriorLightingCheck BazaarInteriorLightingReady(
        Node3D arenaRoot,
        DemolitionArenaLayout layout)
    {
        var expectedPositions = new[]
        {
            new Vector3(-55.0f, 3.0f, -18.0f),
            new Vector3(-52.0f, 2.8f, -27.0f),
            new Vector3(-37.5f, 3.0f, -16.0f),
            new Vector3(38.0f, 3.2f, -18.0f),
            new Vector3(46.0f, 3.2f, -18.0f),
            new Vector3(55.0f, 3.2f, -18.0f),
            new Vector3(0.0f, 3.0f, -15.5f),
            new Vector3(-3.0f, 3.0f, -1.0f),
            new Vector3(3.0f, 3.0f, 12.0f),
            new Vector3(-3.0f, 3.0f, 27.0f),
            new Vector3(0.0f, 3.0f, -18.0f),
            new Vector3(-17.0f, 2.8f, -47.0f),
            new Vector3(17.0f, 2.8f, -47.0f),
            new Vector3(-40.0f, 2.8f, -38.0f),
            new Vector3(40.0f, 2.8f, -38.0f),
            new Vector3(-56.0f, 3.2f, 3.1f),
            new Vector3(56.0f, 3.2f, 2.5f),
            new Vector3(-6.0f, 3.2f, 41.85f)
        };
        var failures = new List<string>();
        var metadataCount = arenaRoot.HasMeta("bazaar_interior_practical_count")
            ? arenaRoot.GetMeta("bazaar_interior_practical_count").AsInt32()
            : -1;
        if (metadataCount != expectedPositions.Length)
        {
            failures.Add($"metadata-{metadataCount}/{expectedPositions.Length}");
        }

        var lightCount = 0;
        for (var index = 0; index < expectedPositions.Length; index++)
        {
            var name = $"BazaarInteriorPractical{index:00}";
            var light = arenaRoot.GetNodeOrNull<OmniLight3D>(name);
            if (!IsInstanceValid(light))
            {
                failures.Add($"missing-{name}");
                continue;
            }
            lightCount++;
            var expectedPosition = layout.Origin + expectedPositions[index];
            var colorReady = Mathf.IsEqualApprox(light!.LightColor.R, 1.0f)
                && Mathf.IsEqualApprox(light.LightColor.G, 0.74f)
                && Mathf.IsEqualApprox(light.LightColor.B, 0.52f);
            if (!light.Position.IsEqualApprox(expectedPosition)
                || !colorReady
                || !Mathf.IsEqualApprox(light.LightEnergy, 1.8f)
                || !Mathf.IsEqualApprox(light.OmniRange, 10.5f)
                || light.ShadowEnabled
                || !light.DistanceFadeEnabled
                || !Mathf.IsEqualApprox(light.DistanceFadeBegin, 48.0f)
                || !Mathf.IsEqualApprox(light.DistanceFadeLength, 16.0f))
            {
                failures.Add($"contract-{name}");
            }
        }

        return new BazaarInteriorLightingCheck(
            failures.Count == 0 && lightCount == expectedPositions.Length,
            lightCount,
            string.Join('|', failures));
    }
}
