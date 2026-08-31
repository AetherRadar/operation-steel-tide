using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public sealed partial class DemolitionArenaBuilder
{
    private static void AddPropCollision(
        StaticBody3D body,
        Node3D? model,
        DemolitionArenaProp definition)
    {
        var authoredCollision = definition.CollisionMode == DemolitionArenaPropCollisionMode.AuthoredConcave
            && model is not null
            && TryAddAuthoredPropCollision(body, model, definition.AuthoredBackfaceCollision);
        if (!authoredCollision)
        {
            AddPropCollisionBoxes(body, definition);
        }
        else if (definition.AddAnalyticalCollisionToAuthored)
        {
            // Keep authored traversal surfaces such as ramps while sealing thin, closed shells
            // with stable volumes so a moving player cannot tunnel into a non-playable interior.
            AddPropCollisionBoxes(body, definition, "SolidCollision");
        }

        var effectiveMode = authoredCollision
            ? DemolitionArenaPropCollisionMode.AuthoredConcave
            : definition.CollisionMode == DemolitionArenaPropCollisionMode.AuthoredConcave
                ? definition.CollisionPieceCount > 1
                    ? DemolitionArenaPropCollisionMode.CompoundBoxes
                    : DemolitionArenaPropCollisionMode.BoundsBox
                : definition.CollisionMode;
        body.SetMeta("prop_collision_mode", effectiveMode.ToString());
        body.SetMeta("analytical_collision_piece_count", definition.CollisionPieceCount);
        body.SetMeta(
            "supplemental_collision_piece_count",
            authoredCollision && definition.AddAnalyticalCollisionToAuthored
                ? definition.CollisionPieceCount
                : 0);
    }

    private static void AddPropCollisionBoxes(
        StaticBody3D body,
        DemolitionArenaProp definition,
        string namePrefix = "Collision")
    {
        for (var index = 0; index < definition.CollisionPieceCount; index++)
        {
            var piece = definition.CollisionPieceAt(index);
            body.AddChild(new CollisionShape3D
            {
                Name = definition.CollisionPieceCount == 1
                    ? namePrefix
                    : $"{namePrefix}_{index + 1:00}",
                Position = piece.Offset * definition.Scale,
                Rotation = piece.Rotation,
                Shape = new BoxShape3D { Size = piece.Size * definition.Scale }
            });
        }
    }

    private static bool TryAddAuthoredPropCollision(
        StaticBody3D body,
        Node3D model,
        bool backfaceCollision)
    {
        var bakedFaces = new List<Vector3>();
        if (model is MeshInstance3D rootMesh)
        {
            AppendAuthoredPropFaces(body, rootMesh, bakedFaces);
        }
        var meshes = model.FindChildren("*", "MeshInstance3D", true, false);
        using var meshesBacking = meshes.AsDisposable();
        foreach (var child in meshes)
        {
            if (child is MeshInstance3D mesh)
            {
                AppendAuthoredPropFaces(body, mesh, bakedFaces);
            }
        }

        if (bakedFaces.Count < 3)
        {
            return false;
        }

        var shape = new ConcavePolygonShape3D
        {
            BackfaceCollision = backfaceCollision
        };
        shape.SetFaces(bakedFaces.ToArray());
        body.AddChild(new CollisionShape3D
        {
            Name = "Collision",
            Shape = shape
        });
        body.SetMeta("authored_collision_triangle_count", bakedFaces.Count / 3);
        body.SetMeta("authored_collision_backface", backfaceCollision);
        return true;
    }

    private static void AppendAuthoredPropFaces(
        StaticBody3D body,
        MeshInstance3D mesh,
        ICollection<Vector3> bakedFaces)
    {
        if (mesh.Mesh?.GetFaces() is not { Length: >= 3 } faces)
        {
            return;
        }

        var meshToBody = body.GlobalTransform.AffineInverse() * mesh.GlobalTransform;
        for (var faceIndex = 0; faceIndex < faces.Length; faceIndex++)
        {
            bakedFaces.Add(meshToBody * faces[faceIndex]);
        }
    }
}
