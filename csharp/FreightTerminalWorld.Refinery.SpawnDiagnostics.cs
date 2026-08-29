using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private bool ValidateJianghaiDeploymentGeometry(
        out int checkedPositions,
        out string firstBlocker)
    {
        var positions = new List<(string Name, Vector3 Position)>
        {
            ("player", DeploymentPoint)
        };
        for (var slot = 1; slot <= 2; slot++)
        {
            positions.Add((
                $"friendly_{slot}",
                ExtractionSpawnPads.FriendlyMemberPosition(
                    DeploymentPoint,
                    _player.GlobalBasis,
                    slot)));
        }
        for (var padIndex = 0; padIndex < JianghaiExtractionSpawnLayout.HostilePads.Count; padIndex++)
        {
            var pad = JianghaiExtractionSpawnLayout.HostilePads[padIndex];
            for (var member = 0; member < ExtractionSpawnPads.SquadSize; member++)
            {
                positions.Add((
                    $"hostile_{padIndex + 1}_{member + 1}",
                    ExtractionSpawnPads.HostileMemberPosition(pad, member)));
            }
        }

        checkedPositions = 0;
        firstBlocker = "none";
        using var capsule = new CapsuleShape3D { Radius = 0.39f, Height = 1.78f };
        foreach (var entry in positions)
        {
            checkedPositions++;
            if (TryFindExtractionSpawnBlocker(entry.Position, capsule, out var blocker))
            {
                firstBlocker = $"{entry.Name}:blocked:{blocker}";
                return false;
            }
            if (!PhysicsRaycast.TryHit(
                    GetWorld3D().DirectSpaceState,
                    entry.Position + Vector3.Up * 0.5f,
                    entry.Position + Vector3.Down * 1.0f,
                    1,
                    out _))
            {
                firstBlocker = $"{entry.Name}:no_ground";
                return false;
            }
        }

        return checkedPositions == 15;
    }
}
