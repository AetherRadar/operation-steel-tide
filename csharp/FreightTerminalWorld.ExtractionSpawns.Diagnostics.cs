using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private readonly record struct ExtractionSpawnGeometryCheck(
        bool Clear,
        int CheckedPositions,
        string Blockers);

    private ExtractionSpawnGeometryCheck InspectExtractionSpawnGeometry()
    {
        using var clearanceShape = new CapsuleShape3D
        {
            Radius = 0.39f,
            Height = 1.78f
        };
        var blockers = new List<string>();
        var checkedPositions = 0;

        foreach (var pad in ExtractionSpawnPads.Pads)
        {
            Inspect("leader", pad);
            for (var slot = 1; slot <= 2; slot++)
            {
                Inspect(
                    $"friendly-{slot}",
                    ExtractionSpawnPads.FriendlyMemberPosition(pad, Basis.Identity, slot));
            }
            for (var member = 0; member < ExtractionSpawnPads.SquadSize; member++)
            {
                Inspect(
                    $"hostile-{member + 1}",
                    ExtractionSpawnPads.HostileMemberPosition(pad, member));
            }
        }

        return new ExtractionSpawnGeometryCheck(
            blockers.Count == 0,
            checkedPositions,
            string.Join(';', blockers));

        void Inspect(string role, Vector3 position)
        {
            checkedPositions++;
            if (!TryFindExtractionSpawnBlocker(position, clearanceShape, out var blocker))
            {
                return;
            }
            blockers.Add(
                $"{role}@({position.X:0.0},{position.Y:0.0},{position.Z:0.0}):{blocker}");
        }
    }

    private bool TryFindExtractionSpawnBlocker(
        Vector3 feet,
        CapsuleShape3D clearanceShape,
        out string blocker)
    {
        using var query = new PhysicsShapeQueryParameters3D
        {
            Shape = clearanceShape,
            Transform = new Transform3D(Basis.Identity, feet + Vector3.Up * 0.9f),
            CollisionMask = 1,
            CollideWithAreas = false,
            CollideWithBodies = true,
            Margin = 0.005f
        };
        var hits = GetWorld3D().DirectSpaceState.IntersectShape(query, 32);
        using var hitsBacking = hits.AsDisposable();
        for (var index = 0; index < hits.Count; index++)
        {
            using var hit = hits[index];
            using var colliderValue = hit[GodotPhysicsResultKeys.Collider];
            if (colliderValue.AsGodotObject() is not StaticBody3D body)
            {
                continue;
            }
            blocker = body.Name.ToString();
            return true;
        }
        blocker = string.Empty;
        return false;
    }
}
