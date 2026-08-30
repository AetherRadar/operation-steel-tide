using Godot;

namespace OperationSteelTide;

public sealed partial class DemolitionArenaBuilder
{
    private void BuildTideglassAuthoredCollision(Node3D root)
    {
        var dressing = root.GetNodeOrNull<Node3D>("DemolitionAuthoredDressing");
        if (!GodotObject.IsInstanceValid(dressing))
        {
            return;
        }

        var landmarkNames = new[]
        {
            "ConstructionGround",
            "ConstructionBuilding",
            "ConstructionCrane",
            "CivicElevatedWalkway"
        };
        foreach (var landmarkName in landmarkNames)
        {
            var landmark = dressing!.GetNodeOrNull<Node3D>(landmarkName);
            if (!GodotObject.IsInstanceValid(landmark))
            {
                continue;
            }

            var body = new StaticBody3D
            {
                Name = $"{landmarkName}AuthoredCollision",
                CollisionLayer = 1,
                CollisionMask = 0
            };
            root.AddChild(body);
            var shapes = 0;
            var meshes = landmark!.FindChildren("*", "MeshInstance3D", true, false);
            using var meshesBacking = meshes.AsDisposable();
            foreach (var child in meshes)
            {
                if (child is not MeshInstance3D mesh || mesh.Mesh?.GetFaces() is not { Length: >= 3 } faces)
                {
                    continue;
                }

                // Bake scale into the faces so physics never receives a non-uniformly scaled shape.
                var rigidTransform = new Transform3D(
                    mesh.GlobalBasis.Orthonormalized(),
                    mesh.GlobalPosition);
                var rigidInverse = rigidTransform.AffineInverse();
                var bakedFaces = new Vector3[faces.Length];
                for (var face = 0; face < faces.Length; face++)
                {
                    bakedFaces[face] = rigidInverse * mesh.ToGlobal(faces[face]);
                }
                var shape = new ConcavePolygonShape3D
                {
                    BackfaceCollision = landmarkName == "CivicElevatedWalkway"
                };
                shape.SetFaces(bakedFaces);

                var collision = new CollisionShape3D
                {
                    Name = $"Collision_{shapes + 1:00}",
                    Shape = shape
                };
                body.AddChild(collision);
                collision.GlobalTransform = rigidTransform;
                shapes++;
            }

            if (shapes == 0)
            {
                body.QueueFree();
                continue;
            }
            body.SetMeta("authored_source_model", landmarkName);
            body.SetMeta("authored_shape_count", shapes);
            _staticBodies.Add(body);
        }
    }

    private void BuildTideglassReactorLandmarks(Node3D root, DemolitionArenaLayout layout)
    {
        AddSign(root, "ArenaTitle", layout.Origin + new Vector3(0.0f, 6.4f, -54.6f), "TIDEGLASS REACTOR  //  TR-02", 0.0f, new Color(0.96f, 0.82f, 0.52f));
        AddSign(root, "ConstructionCourtSign", layout.Origin + new Vector3(-49.0f, 4.8f, 9.0f), "A  //  CONSTRUCTION COURT", Mathf.Pi * 0.5f, new Color(1.0f, 0.64f, 0.18f));
        AddSign(root, "BrickWorksSign", layout.Origin + new Vector3(53.0f, 5.2f, -14.0f), "B  //  OLD REACTOR YARD", -Mathf.Pi * 0.5f, new Color(0.82f, 0.34f, 0.18f));
        AddSign(root, "CivicCrossingSign", layout.Origin + new Vector3(0.0f, 4.4f, 0.0f), "CIVIC CROSSING", Mathf.Pi, new Color(0.76f, 0.88f, 0.86f));
    }

    private void BuildTideglassReactorCoverDetails(Node3D root, DemolitionArenaLayout layout)
    {
        AddSign(root, "SiteOfficeSign", layout.Origin + new Vector3(-17.0f, 3.4f, 6.8f), "SITE OFFICE", 0.0f, new Color(1.0f, 0.72f, 0.24f));
        AddSign(root, "MachineShopSign", layout.Origin + new Vector3(10.0f, 3.8f, 14.8f), "MACHINE SHOP", Mathf.Pi, new Color(0.62f, 0.84f, 0.76f));
        AddSign(root, "LoadingLaneSign", layout.Origin + new Vector3(18.0f, 3.7f, -5.4f), "LOADING LANE", Mathf.Pi, new Color(0.92f, 0.62f, 0.28f));
    }

    private void BuildTideglassReactorRouteGuidance(Node3D root, DemolitionArenaLayout layout)
    {
        AddFloorLabel(root, "AttackFloorLabel", layout.Origin + new Vector3(51.0f, 0.09f, 42.0f), "ATTACK", new Color(0.56f, 0.92f, 0.86f), 76);
        AddFloorLabel(root, "RouteALabel", layout.Origin + new Vector3(22.0f, 0.09f, 50.0f), "A  COURT", new Color(1.0f, 0.58f, 0.18f), 66);
        AddFloorLabel(root, "RouteMidLabel", layout.Origin + new Vector3(5.0f, 0.09f, 22.0f), "CROSSING", new Color(0.9f, 0.88f, 0.68f), 58);
        AddFloorLabel(root, "RouteBLabel", layout.Origin + new Vector3(58.0f, 0.09f, 9.0f), "B  WORKS", new Color(0.84f, 0.42f, 0.22f), 66);
        AddFloorLabel(root, "DefendFloorLabel", layout.Origin + new Vector3(-50.0f, 0.09f, -42.0f), "DEFEND", new Color(0.46f, 0.94f, 0.68f), 72);
    }

    private void BuildTideglassReactorLighting(Node3D root, DemolitionArenaLayout layout)
    {
        var positions = new[]
        {
            new Vector3(-42.0f, 8.5f, 24.0f),
            new Vector3(44.0f, 8.5f, -25.0f),
            new Vector3(-4.0f, 8.0f, 15.0f),
            new Vector3(15.0f, 8.0f, -4.0f),
            new Vector3(-24.0f, 8.0f, -34.0f),
            new Vector3(34.0f, 8.0f, 36.0f)
        };
        for (var index = 0; index < positions.Length; index++)
        {
            root.AddChild(new SpotLight3D
            {
                Name = $"TideglassFloodlight_{index + 1:00}",
                Position = layout.Origin + positions[index],
                RotationDegrees = new Vector3(-90, 0, 0),
                LightColor = index % 2 == 0
                    ? new Color(1.0f, 0.78f, 0.52f)
                    : new Color(0.72f, 0.86f, 0.8f),
                LightEnergy = 4.0f,
                SpotRange = 27.0f,
                SpotAngle = 52.0f,
                ShadowEnabled = index is 0 or 1
            });
            _visualPartCount++;
        }
    }
}
