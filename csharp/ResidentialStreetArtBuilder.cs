using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

internal sealed record ResidentialStreetArtBuildResult(
    int ExpectedPlacementCount, int AuthoredPlacementCount, int MissingPlacementCount,
    int MissingSourceCount, int FallbackPlacementCount, IReadOnlyCollection<string> ScenePaths);

internal readonly record struct ResidentialStreetArtPlacement(
    string Name, string Kind, string Style, int Index, string ScenePath,
    Vector3 Position, float YawDegrees, float TargetHeight);

/// <summary>
/// Places authored residential street furniture while keeping collision semantic and invisible.
/// Missing assets never become primitive placeholder art or invisible obstacles.
/// </summary>
internal sealed class ResidentialStreetArtBuilder
{
    public const string LampScenePath = "res://assets/models/polyhaven_residential_street/street_lamp_01/street_lamp_01_1k.gltf";
    public const string CleanBinScenePath = "res://assets/models/polyhaven_residential_street/metal_trash_can/metal_trash_can_clean.glb";
    public const string RustBinScenePath = "res://assets/models/polyhaven_residential_street/metal_trash_can/metal_trash_can_rust.glb";
    public const string CoffeeCartScenePath = "res://assets/models/polyhaven_residential_street/CoffeeCart_01/CoffeeCart_01_1k.gltf";
    public const string WoodenCrateScenePath = "res://assets/models/polyhaven_residential_street/wooden_crate_01/wooden_crate_01_1k.gltf";
    public const string PlasticCrateScenePath = "res://assets/models/polyhaven_residential_street/plastic_crate_01/plastic_crate_01_1k.gltf";
    public const string WickerBasketScenePath = "res://assets/models/polyhaven_residential_street/wicker_basket_01/wicker_basket_01_1k.gltf";
    public const string FallbackCrateScenePath = "res://assets/models/old_military_crate/old_military_crate.gltf";

    public const int ExpectedLampCount = 26;
    public const int ExpectedBinCount = 5;
    public const int ExpectedMarketCount = 8;
    public const int ExpectedPlacementCount = ExpectedLampCount + ExpectedBinCount + ExpectedMarketCount;

    private static readonly IReadOnlyList<ResidentialStreetArtPlacement> Placements = CreatePlacements();

    private readonly Dictionary<string, PackedScene> _scenes = new(StringComparer.Ordinal);
    private readonly HashSet<string> _failedScenePaths = new(StringComparer.Ordinal);

    public static IReadOnlyList<ResidentialStreetArtPlacement> ExpectedPlacements => Placements;

    public ResidentialStreetArtBuildResult Build(Node3D parent)
    {
        var root = new Node3D { Name = "ResidentialStreetAuthoredArt" };
        parent.AddChild(root);

        var authoredCount = 0;
        var missingCount = 0;
        var fallbackCount = 0;
        var scenePaths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var placement in Placements)
        {
            if (TryAddPlacement(root, placement, scenePaths, out var usedFallback))
            {
                authoredCount++;
                fallbackCount += usedFallback ? 1 : 0;
            }
            else
            {
                missingCount++;
            }
        }

        root.SetMeta("expected_placement_count", ExpectedPlacementCount);
        root.SetMeta("authored_placement_count", authoredCount);
        root.SetMeta("missing_placement_count", missingCount);
        root.SetMeta("missing_source_count", _failedScenePaths.Count);
        root.SetMeta("fallback_placement_count", fallbackCount);
        return new ResidentialStreetArtBuildResult(
            ExpectedPlacementCount,
            authoredCount,
            missingCount,
            _failedScenePaths.Count,
            fallbackCount,
            scenePaths);
    }

    private bool TryAddPlacement(
        Node3D root,
        ResidentialStreetArtPlacement placement,
        HashSet<string> allScenePaths,
        out bool usedFallback)
    {
        usedFallback = false;
        var model = new Node3D { Name = "Model" };
        var placementScenePaths = new HashSet<string>(StringComparer.Ordinal);
        var primaryReady = placement.Kind == "market_loading"
            ? TryBuildMarketModel(model, placement, placementScenePaths, out usedFallback)
            : TryAddNormalizedModel(
                model,
                placement.ScenePath,
                placement.TargetHeight,
                Vector3.Zero,
                0.0f,
                "AuthoredPrimary",
                placementScenePaths);
        if (!primaryReady || !TryGetBounds(model, out var bounds))
        {
            FreeTemporaryNode(model);
            return false;
        }

        var body = new StaticBody3D
        {
            Name = placement.Name,
            Position = placement.Position,
            RotationDegrees = new Vector3(0, placement.YawDegrees, 0),
            CollisionLayer = 1,
            CollisionMask = 0
        };
        body.AddToGroup("residential_street_art");
        body.SetMeta("scene_path", usedFallback ? FallbackCrateScenePath : placement.ScenePath);
        body.SetMeta("kind", placement.Kind);
        body.SetMeta("style", placement.Style);
        body.SetMeta("index", placement.Index);
        body.SetMeta("used_fallback", usedFallback);
        var sortedPaths = new List<string>(placementScenePaths);
        sortedPaths.Sort(StringComparer.Ordinal);
        body.SetMeta("scene_paths", string.Join('|', sortedPaths));
        body.SetMeta("model_bounds_min_y", bounds.Position.Y);
        body.SetMeta("model_bounds_size", bounds.Size);
        body.AddChild(model);
        body.AddChild(CreateCollision(placement, bounds));
        root.AddChild(body);
        allScenePaths.UnionWith(placementScenePaths);
        return true;
    }

    private bool TryBuildMarketModel(
        Node3D model,
        ResidentialStreetArtPlacement placement,
        HashSet<string> scenePaths,
        out bool usedFallback)
    {
        var primaryYaw = placement.Style == "mixed_supply"
            ? (placement.Index % 4 == 0 ? 8.0f : -8.0f)
            : 0.0f;
        usedFallback = !TryAddNormalizedModel(
            model,
            placement.ScenePath,
            placement.TargetHeight,
            Vector3.Zero,
            primaryYaw,
            "AuthoredStall",
            scenePaths);
        if (usedFallback)
        {
            return TryAddNormalizedModel(
                model,
                FallbackCrateScenePath,
                0.7f,
                new Vector3(-0.32f, 0, 0),
                placement.Index % 2 == 0 ? -8.0f : 8.0f,
                "AuthoredFallbackCrates",
                scenePaths);
        }

        var side = (placement.Index / 2) % 2 == 0 ? 1.0f : -1.0f;
        if (placement.Style == "coffee_cart")
        {
            var crateReady = TryAddNormalizedModel(
                model,
                WoodenCrateScenePath,
                0.35f,
                new Vector3(1.35f * side, 0, 0.42f),
                14.0f * side,
                "AuthoredWoodenCrate",
                scenePaths);
            var basketReady = TryAddNormalizedModel(
                model,
                WickerBasketScenePath,
                0.117f,
                new Vector3(0.95f * side, 0, -0.62f),
                -18.0f * side,
                "AuthoredWickerBasket",
                scenePaths);
            return crateReady && basketReady;
        }
        var plasticReady = TryAddNormalizedModel(
            model,
            PlasticCrateScenePath,
            0.264f,
            new Vector3(0.62f * side, 0, 0.22f),
            10.0f * side,
            "AuthoredPlasticCrate",
            scenePaths);
        var supplyBasketReady = TryAddNormalizedModel(
            model,
            WickerBasketScenePath,
            0.117f,
            new Vector3(-0.3f * side, 0, -0.55f),
            -16.0f * side,
            "AuthoredWickerBasket",
            scenePaths);
        return plasticReady && supplyBasketReady;
    }

    private bool TryAddNormalizedModel(
        Node3D parent,
        string scenePath,
        float targetHeight,
        Vector3 offset,
        float yawDegrees,
        string name,
        HashSet<string> scenePaths)
    {
        if (!TryLoadScene(scenePath, out var scene))
        {
            return false;
        }

        Node? temporary = null;
        try
        {
            temporary = scene.Instantiate();
            if (temporary is not Node3D authored)
            {
                throw new InvalidOperationException("Authored street model root must be Node3D.");
            }
            if (ContainsPrimitiveMesh(authored) || !TryGetBounds(authored, out var sourceBounds))
            {
                throw new InvalidOperationException(
                    "Authored street model must contain bounded non-primitive mesh geometry.");
            }

            var uniformScale = targetHeight / sourceBounds.Size.Y;
            if (!float.IsFinite(uniformScale) || uniformScale <= 0.0f)
            {
                throw new InvalidOperationException("Authored street model produced an invalid uniform scale.");
            }

            var radians = Mathf.DegToRad(yawDegrees);
            var yaw = new Basis(Vector3.Up, radians);
            var center = sourceBounds.GetCenter();
            var horizontalCenter = new Vector3(center.X, 0, center.Z) * uniformScale;
            authored.Name = name;
            authored.SetMeta("source_height_m", sourceBounds.Size.Y);
            authored.SetMeta("target_height_m", targetHeight);
            authored.SetMeta("uniform_scale", uniformScale);
            authored.Scale = Vector3.One * uniformScale;
            authored.RotationDegrees = new Vector3(0, yawDegrees, 0);
            authored.Position = offset
                - yaw * horizontalCenter
                + Vector3.Up * (-sourceBounds.Position.Y * uniformScale);
            ConfigureVisuals(authored);
            parent.AddChild(authored);
            temporary = null;
            scenePaths.Add(scenePath);
            return true;
        }
        catch (Exception exception)
        {
            RememberFailure(scenePath, exception);
            return false;
        }
        finally
        {
            FreeTemporaryNode(temporary);
        }
    }

    private bool TryLoadScene(string path, out PackedScene scene)
    {
        if (_failedScenePaths.Contains(path))
        {
            scene = null!;
            return false;
        }
        if (_scenes.TryGetValue(path, out scene!))
        {
            return true;
        }

        try
        {
            scene = GD.Load<PackedScene>(path);
            if (scene is null)
            {
                throw new InvalidOperationException("Resource loader returned no PackedScene.");
            }
            _scenes[path] = scene;
            return true;
        }
        catch (Exception exception)
        {
            RememberFailure(path, exception);
            scene = null!;
            return false;
        }
    }

    private void RememberFailure(string path, Exception exception)
    {
        _scenes.Remove(path);
        if (!_failedScenePaths.Add(path))
        {
            return;
        }
        GD.PushError(
            $"Residential authored street model unavailable: {path} "
            + $"({exception.GetType().Name}: {exception.Message})");
    }

    private static CollisionShape3D CreateCollision(ResidentialStreetArtPlacement placement, Aabb bounds)
    {
        if (placement.Kind == "lamp")
        {
            var radius = Mathf.Clamp(Mathf.Min(bounds.Size.X, bounds.Size.Z) * 0.28f, 0.09f, 0.2f);
            return new CollisionShape3D
            {
                Name = "Collision",
                Position = new Vector3(0, bounds.GetCenter().Y, 0),
                Shape = new CylinderShape3D
                {
                    Height = Mathf.Max(bounds.Size.Y, 0.2f),
                    Radius = radius
                }
            };
        }

        var size = placement.Kind == "market_loading"
            ? bounds.Size * new Vector3(
                0.9f,
                placement.Style == "coffee_cart" ? 0.52f : 0.94f,
                0.82f)
            : bounds.Size * new Vector3(0.88f, 0.96f, 0.88f);
        return new CollisionShape3D
        {
            Name = "Collision",
            Position = new Vector3(bounds.GetCenter().X, size.Y * 0.5f, bounds.GetCenter().Z),
            Shape = new BoxShape3D { Size = size.Max(Vector3.One * 0.08f) }
        };
    }

    private static bool TryGetBounds(Node3D root, out Aabb bounds)
    {
        var minimum = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        var maximum = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        var meshCount = 0;
        AccumulateBounds(root, Transform3D.Identity, ref minimum, ref maximum, ref meshCount);
        bounds = meshCount == 0 ? new Aabb() : new Aabb(minimum, maximum - minimum);
        return meshCount > 0
            && bounds.Size.X > 0.001f
            && bounds.Size.Y > 0.001f
            && bounds.Size.Z > 0.001f;
    }

    private static void AccumulateBounds(
        Node3D node,
        Transform3D parentTransform,
        ref Vector3 minimum,
        ref Vector3 maximum,
        ref int meshCount)
    {
        var transform = parentTransform * node.Transform;
        if (node is MeshInstance3D { Mesh: not null } mesh)
        {
            meshCount++;
            var meshBounds = mesh.Mesh.GetAabb();
            for (var corner = 0; corner < 8; corner++)
            {
                var local = meshBounds.Position + new Vector3(
                    (corner & 1) == 0 ? 0 : meshBounds.Size.X,
                    (corner & 2) == 0 ? 0 : meshBounds.Size.Y,
                    (corner & 4) == 0 ? 0 : meshBounds.Size.Z);
                var point = transform * local;
                minimum = minimum.Min(point);
                maximum = maximum.Max(point);
            }
        }

        var children = node.GetChildren();
        using var childrenBacking = children.AsDisposable();
        foreach (var child in children)
        {
            if (child is Node3D child3D)
            {
                AccumulateBounds(child3D, transform, ref minimum, ref maximum, ref meshCount);
            }
        }
    }

    private static bool ContainsPrimitiveMesh(Node node)
    {
        if (node is MeshInstance3D { Mesh: PrimitiveMesh })
        {
            return true;
        }
        var children = node.GetChildren();
        using var childrenBacking = children.AsDisposable();
        foreach (var child in children)
        {
            if (child is Node childNode && ContainsPrimitiveMesh(childNode))
            {
                return true;
            }
        }
        return false;
    }

    private static void ConfigureVisuals(Node node)
    {
        if (node is GeometryInstance3D visual)
        {
            visual.CastShadow = GeometryInstance3D.ShadowCastingSetting.On;
            visual.VisibilityRangeEnd = 240.0f;
            visual.VisibilityRangeEndMargin = 18.0f;
        }
        var children = node.GetChildren();
        using var childrenBacking = children.AsDisposable();
        foreach (var child in children)
        {
            if (child is Node childNode)
            {
                ConfigureVisuals(childNode);
            }
        }
    }

    private static void FreeTemporaryNode(Node? node)
    {
        if (GodotObject.IsInstanceValid(node))
        {
            node!.Free();
        }
    }

    private static IReadOnlyList<ResidentialStreetArtPlacement> CreatePlacements()
    {
        var placements = new List<ResidentialStreetArtPlacement>(ExpectedPlacementCount);
        var lampIndex = 0;
        foreach (var x in new[] { -140f, -100f, -60f, -20f, 20f, 60f, 100f })
        {
            foreach (var z in new[] { -201.2f, -184.8f })
            {
                lampIndex++;
                placements.Add(Placement("Lamp", "lamp", lampIndex, LampScenePath, x, 0.13f, z, 3.87f));
            }
        }
        foreach (var x in new[] { -110f, -70f, -30f, 10f, 50f, 90f })
        {
            foreach (var z in new[] { 67.8f, 84.2f })
            {
                lampIndex++;
                placements.Add(Placement("Lamp", "lamp", lampIndex, LampScenePath, x, 0.13f, z, 3.87f));
            }
        }

        var binPositions = new[]
        {
            new Vector3(-52, 0, 60), new Vector3(8, 0, 62), new Vector3(62, 0, 58),
            new Vector3(-40, 0, -175), new Vector3(40, 0, -177)
        };
        for (var index = 0; index < binPositions.Length; index++)
        {
            var clean = index % 2 == 0;
            placements.Add(new ResidentialStreetArtPlacement(
                $"ResidentialStreetBin_{index + 1:00}", "bin", clean ? "clean" : "rust",
                index + 1, clean ? CleanBinScenePath : RustBinScenePath,
                binPositions[index] + Vector3.Up * 0.02f, index * 37.0f, 0.906f));
        }

        var markets = new (float X, float Z)[]
        {
            (-98, -181.5f), (-52, -181.5f), (-6, -181.5f), (44, -181.5f),
            (-66, 70.5f), (-12, 70.5f), (28, 70.5f), (74, 70.5f)
        };
        for (var index = 0; index < markets.Length; index++)
        {
            var market = markets[index];
            var coffeeCart = index % 2 == 0;
            placements.Add(new ResidentialStreetArtPlacement(
                $"ResidentialMarketLoading_{index + 1:00}", "market_loading",
                coffeeCart ? "coffee_cart" : "mixed_supply", index + 1,
                coffeeCart ? CoffeeCartScenePath : WoodenCrateScenePath,
                new Vector3(market.X, index < 4 ? 0.02f : 0.08f, market.Z),
                index < 4 ? 180.0f : 0.0f, coffeeCart ? 1.718f : 0.35f));
        }
        return placements;
    }

    private static ResidentialStreetArtPlacement Placement(
        string name,
        string kind,
        int index,
        string path,
        float x,
        float y,
        float z,
        float height)
        => new(
            $"ResidentialStreet{name}_{index:00}",
            kind,
            kind,
            index,
            path,
            new Vector3(x, y, z),
            0,
            height);
}
