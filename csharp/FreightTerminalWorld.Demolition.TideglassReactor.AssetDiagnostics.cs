using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private static bool TideglassSolidMaterialsReady(
        Node3D root,
        Node3D? dressingRoot,
        DemolitionArenaLayout layout,
        out string failures)
    {
        var failedMaterials = new List<string>();
        var models = new List<Node3D>();
        if (IsInstanceValid(dressingRoot))
        {
            models.AddRange(dressingRoot!.GetChildren().OfType<Node3D>());
        }
        foreach (var prop in layout.Props)
        {
            var model = root.GetNodeOrNull<StaticBody3D>(prop.Name)
                ?.GetNodeOrNull<Node3D>("Model");
            if (IsInstanceValid(model))
            {
                models.Add(model!);
            }
        }

        foreach (var model in models)
        {
            var meshNodes = model.FindChildren("*", "MeshInstance3D", true, false);
            using var meshNodesBacking = meshNodes.AsDisposable();
            var meshes = meshNodes.OfType<MeshInstance3D>().ToArray();
            var surfaces = 0;
            foreach (var mesh in meshes)
            {
                var surfaceCount = mesh.Mesh?.GetSurfaceCount() ?? 0;
                for (var surface = 0; surface < surfaceCount; surface++)
                {
                    surfaces++;
                    if (mesh.GetActiveMaterial(surface) is not BaseMaterial3D material)
                    {
                        failedMaterials.Add($"{model.Name}/{mesh.Name}:surface={surface}:material=missing");
                        continue;
                    }
                    var transparent = material.AlbedoColor.A < 0.997f
                        || material.Transparency != BaseMaterial3D.TransparencyEnum.Disabled;
                    var materialName = material.ResourceName.ToString();
                    var transparencyAllowed = mesh.Name.ToString().Contains(
                            "glass",
                            StringComparison.OrdinalIgnoreCase)
                        || mesh.Name.ToString().Contains(
                            "window",
                            StringComparison.OrdinalIgnoreCase)
                        || materialName.Contains("glass", StringComparison.OrdinalIgnoreCase)
                        || materialName.Contains("window", StringComparison.OrdinalIgnoreCase);
                    if (transparent && !transparencyAllowed)
                    {
                        failedMaterials.Add(
                            $"{model.Name}/{mesh.Name}:surface={surface}:material={materialName}:alpha={material.AlbedoColor.A:0.000}");
                    }
                }
            }
            if (surfaces == 0)
            {
                failedMaterials.Add($"{model.Name}:surfaces=0");
            }
        }
        if (models.Count != layout.Props.Count + 26)
        {
            failedMaterials.Add($"models={models.Count}/{layout.Props.Count + 26}");
        }
        failures = string.Join('|', failedMaterials);
        return failedMaterials.Count == 0;
    }

    private static bool TideglassAuthoredMeshCollisionReady(Node3D arenaRoot)
    {
        var dressingRoot = arenaRoot.GetNodeOrNull<Node3D>("DemolitionAuthoredDressing");
        var landmarkNames = new[]
        {
            "ConstructionGround",
            "ConstructionBuilding",
            "ConstructionCrane",
            "CivicElevatedWalkway"
        };
        return landmarkNames.All(landmarkName =>
        {
            var body = arenaRoot.GetNodeOrNull<StaticBody3D>($"{landmarkName}AuthoredCollision");
            var source = dressingRoot?.GetNodeOrNull<Node3D>(landmarkName);
            if (!IsInstanceValid(body)
                || !IsInstanceValid(source)
                || body!.CollisionLayer != 1
                || body.CollisionMask != 0
                || body.GetMeta("authored_source_model").AsString() != landmarkName
                || body.GetMeta("authored_shape_count").AsInt32() < 1)
            {
                return false;
            }
            var shapeNodes = body.FindChildren("*", "CollisionShape3D", true, false);
            using var shapeNodesBacking = shapeNodes.AsDisposable();
            var shapes = shapeNodes.OfType<CollisionShape3D>().ToArray();
            var meshNodes = source!.FindChildren("*", "MeshInstance3D", true, false);
            using var meshNodesBacking = meshNodes.AsDisposable();
            var meshes = meshNodes
                .OfType<MeshInstance3D>()
                .Where(mesh => mesh.Mesh is not null && mesh.Mesh.GetFaces().Length >= 3)
                .ToArray();
            if (shapes.Length != body.GetMeta("authored_shape_count").AsInt32()
                || shapes.Length != meshes.Length)
            {
                return false;
            }
            for (var index = 0; index < shapes.Length; index++)
            {
                var sourceFaces = meshes[index].Mesh!.GetFaces();
                if (shapes[index].Name != $"Collision_{index + 1:00}"
                    || shapes[index].Disabled
                    || shapes[index].Shape is not ConcavePolygonShape3D)
                {
                    return false;
                }
                var concave = (ConcavePolygonShape3D)shapes[index].Shape;
                if (concave.GetFaces().Length < 3
                    || concave.GetFaces().Length != sourceFaces.Length
                    || (landmarkName == "CivicElevatedWalkway" && !concave.BackfaceCollision)
                    || !shapes[index].GlobalBasis.Scale.IsEqualApprox(Vector3.One))
                {
                    return false;
                }
                var collisionFaces = concave.GetFaces();
                for (var face = 0; face < collisionFaces.Length; face++)
                {
                    if (shapes[index].ToGlobal(collisionFaces[face]).DistanceTo(
                        meshes[index].ToGlobal(sourceFaces[face])) > 0.001f)
                    {
                        return false;
                    }
                }
            }
            return true;
        });
    }

    private static bool TideglassTreyAssembliesReady(Node3D root, out string failures)
    {
        var expected = new[]
        {
            (Name: "NorthLoadingBay", Meshes: 20, Windows: 0, Roofs: 8, Height: 3.15f),
            (Name: "SouthUtilityOffice", Meshes: 11, Windows: 1, Roofs: 2, Height: 3.3f),
            (Name: "CentralServiceHall", Meshes: 20, Windows: 2, Roofs: 8, Height: 4.2f),
            (Name: "WestWindowHall", Meshes: 21, Windows: 3, Roofs: 8, Height: 3.15f)
        };
        var failed = new List<string>();
        foreach (var item in expected)
        {
            var body = root.GetNodeOrNull<StaticBody3D>(item.Name);
            var model = body?.GetNodeOrNull<Node3D>("Model");
            if (!IsInstanceValid(body)
                || !IsInstanceValid(model)
                || !TideglassTryGetBounds(model!, body!, out var minimum, out var maximum))
            {
                failed.Add($"{item.Name}:missing");
                continue;
            }
            var meshNodes = model!.FindChildren("*", "MeshInstance3D", true, false);
            using var meshNodesBacking = meshNodes.AsDisposable();
            var meshes = meshNodes.OfType<MeshInstance3D>().ToArray();
            var names = meshes.Select(mesh => mesh.Name.ToString()).ToArray();
            var windowCount = names.Count(name => name.Contains("IndWindow", StringComparison.Ordinal));
            var roofCount = names.Count(name => name.Contains("IndRoof", StringComparison.Ordinal));
            var ready = meshes.Length == item.Meshes
                && windowCount == item.Windows
                && roofCount == item.Roofs
                && names.All(name => !name.Contains("IndWindowE", StringComparison.Ordinal))
                && names.All(name => !name.Contains("IndRoofAngled", StringComparison.Ordinal))
                && meshes.All(mesh => mesh.Visible && mesh.IsVisibleInTree() && (mesh.Layers & 1u) != 0)
                && Mathf.Abs(minimum.Y) <= 0.03f
                && Mathf.Abs(maximum.Y - item.Height) <= 0.04f;
            if (!ready)
            {
                failed.Add(
                    $"{item.Name}:meshes={meshes.Length}/{item.Meshes}"
                    + $":windows={windowCount}/{item.Windows}:roofs={roofCount}/{item.Roofs}"
                    + $":height={minimum.Y:0.000}..{maximum.Y:0.000}/{item.Height:0.000}");
            }
        }
        failures = string.Join('|', failed);
        return failed.Count == 0;
    }

    private static bool TideglassMajadroidVariantsReady(Node3D root, out string failures)
    {
        var expected = new[]
        {
            (Name: "SightBlockConstructionSiteOffice", Meshes: 1),
            (Name: "SightBlockReactorCargoContainers", Meshes: 1),
            (Name: "ConstructionTruck", Meshes: 1),
            (Name: "MidCoverConstructionSupplies", Meshes: 3)
        };
        var failed = new List<string>();
        foreach (var item in expected)
        {
            var model = root.GetNodeOrNull<StaticBody3D>(item.Name)
                ?.GetNodeOrNull<Node3D>("Model");
            if (!IsInstanceValid(model))
            {
                failed.Add($"{item.Name}:missing");
                continue;
            }
            var meshNodes = model!.FindChildren("*", "MeshInstance3D", true, false);
            using var meshNodesBacking = meshNodes.AsDisposable();
            var meshes = meshNodes.OfType<MeshInstance3D>().ToArray();
            if (meshes.Length != item.Meshes
                || meshes.Any(mesh => !mesh.Visible || !mesh.IsVisibleInTree() || (mesh.Layers & 1u) == 0))
            {
                failed.Add($"{item.Name}:meshes={meshes.Length}/{item.Meshes}");
            }
        }
        failures = string.Join('|', failed);
        return failed.Count == 0;
    }

    private static bool TideglassBrickFactoryCollisionReady(
        Node3D? dressingRoot,
        DemolitionArenaLayout layout,
        out string failure)
    {
        failure = "none";
        var model = dressingRoot?.GetNodeOrNull<Node3D>("OldBrickReactorHall");
        var shell = layout.CollisionBoxes.SingleOrDefault(box => box.Name == "BrickFactoryShell");
        if (!IsInstanceValid(model)
            || shell.Name != "BrickFactoryShell"
            || !TideglassTryGetBounds(model!, null, out var minimum, out var maximum))
        {
            failure = "missing";
            return false;
        }

        var shellMinimum = shell.Center - shell.Size * 0.5f;
        var shellMaximum = shell.Center + shell.Size * 0.5f;
        var minimumPadding = minimum - shellMinimum;
        var maximumPadding = shellMaximum - maximum;
        const float coverageTolerance = -0.08f;
        const float maximumPaddingMeters = 0.55f;
        var ready = TideglassPaddingWithin(minimumPadding, coverageTolerance, maximumPaddingMeters)
            && TideglassPaddingWithin(maximumPadding, coverageTolerance, maximumPaddingMeters);
        if (!ready)
        {
            failure = $"model={minimum}..{maximum}:shell={shellMinimum}..{shellMaximum}";
        }
        return ready;
    }

    private static bool TideglassConstructionLandmarkClearanceReady(
        Node3D? dressingRoot,
        out float clearance)
    {
        clearance = float.NegativeInfinity;
        var building = dressingRoot?.GetNodeOrNull<Node3D>("ConstructionBuilding");
        var crane = dressingRoot?.GetNodeOrNull<Node3D>("ConstructionCrane");
        if (!IsInstanceValid(building)
            || !IsInstanceValid(crane)
            || !TideglassTryGetFaceBounds(building!, out var buildingMinimum, out _)
            || !TideglassTryGetFaceBounds(crane!, out _, out var craneMaximum))
        {
            return false;
        }

        clearance = buildingMinimum.Z - craneMaximum.Z;
        return clearance >= 0.30f;
    }

    private static bool TideglassTryGetFaceBounds(
        Node3D model,
        out Vector3 minimum,
        out Vector3 maximum)
    {
        minimum = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        maximum = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        var meshNodes = model.FindChildren("*", "MeshInstance3D", true, false);
        using var meshNodesBacking = meshNodes.AsDisposable();
        foreach (var mesh in meshNodes.OfType<MeshInstance3D>())
        {
            if (mesh.Mesh is null || !mesh.Visible || !mesh.IsVisibleInTree())
            {
                continue;
            }
            foreach (var face in mesh.Mesh.GetFaces())
            {
                var point = mesh.ToGlobal(face);
                minimum = new Vector3(
                    Mathf.Min(minimum.X, point.X),
                    Mathf.Min(minimum.Y, point.Y),
                    Mathf.Min(minimum.Z, point.Z));
                maximum = new Vector3(
                    Mathf.Max(maximum.X, point.X),
                    Mathf.Max(maximum.Y, point.Y),
                    Mathf.Max(maximum.Z, point.Z));
            }
        }
        return !float.IsInfinity(minimum.X);
    }

    private static bool TideglassCollisionShellsHaveAuthoredModels(
        Node3D? dressingRoot,
        DemolitionArenaLayout layout)
    {
        if (!IsInstanceValid(dressingRoot))
        {
            return false;
        }
        var requiredModels = new[]
        {
            "ConstructionBuilding",
            "OldBrickReactorHall",
            "ConstructionCrane",
            "TideglassPerimeterFence",
            "OrangeArchGateway",
            "CivicElevatedWalkway"
        };
        if (requiredModels.Any(name =>
            dressingRoot!.GetNodeOrNull<Node3D>(name) is not { } model
            || !TideglassAuthoredModelHasMesh(model)))
        {
            return false;
        }

        return layout.CollisionBoxes.All(box => box.Name switch
        {
            "ArenaFloor" or "NorthPerimeter" or "SouthPerimeter" or "WestPerimeter" or "EastPerimeter" => true,
            "BrickFactoryShell" or "GatewayWestPillar" or "GatewayEastPillar" => true,
            _ => box.Name.StartsWith("ConstructionTower", StringComparison.Ordinal)
        });
    }
}
