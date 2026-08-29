using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

/// <summary>Jianghai-specific deployment geometry aimed down the rebuilt south-gate avenue.</summary>
internal static class JianghaiExtractionSpawnLayout
{
    public static readonly Vector3 PlayerPad = new(8.0f, 0.18f, 48.0f);
    public static readonly Vector3 PlayerLookTarget = new(0.0f, 1.6f, -60.0f);
    public static readonly IReadOnlyList<Vector3> HostilePads = new[]
    {
        new Vector3(-148.0f, 0.18f, -198.0f),
        new Vector3(148.0f, 0.18f, -198.0f),
        new Vector3(-148.0f, 0.18f, 72.0f),
        new Vector3(148.0f, 0.18f, 72.0f)
    };

    public static float PlayerYaw
    {
        get
        {
            var direction = PlayerLookTarget - PlayerPad;
            return Mathf.Atan2(-direction.X, -direction.Z);
        }
    }
}
