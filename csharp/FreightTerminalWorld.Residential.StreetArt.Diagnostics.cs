using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private async void ValidateResidentialStreetArt()
    {
        PrepareResidentialStreetArtDiagnosticScene(hidePlayer: false);
        await WaitFrames(4);

        var groupedNodes = GetTree().GetNodesInGroup("residential_street_art");
        using var groupedNodesBacking = groupedNodes.AsDisposable();
        var bodies = groupedNodes.OfType<StaticBody3D>().Where(IsInstanceValid).ToList();
        var expectedByName = ResidentialStreetArtBuilder.ExpectedPlacements
            .ToDictionary(placement => placement.Name, StringComparer.Ordinal);
        var names = bodies.Select(body => body.Name.ToString()).ToList();
        var namesReady = names.Count == names.Distinct(StringComparer.Ordinal).Count()
            && names.All(name => !name.StartsWith("@", StringComparison.Ordinal))
            && names.All(expectedByName.ContainsKey);

        var exactPlacements = namesReady && bodies.All(body =>
        {
            var expected = expectedByName[body.Name.ToString()];
            return body.Position.DistanceTo(expected.Position) <= 0.002f
                && Mathf.Abs(body.RotationDegrees.Y - expected.YawDegrees) <= 0.002f;
        });
        var metadataReady = namesReady && bodies.All(body =>
        {
            var expected = expectedByName[body.Name.ToString()];
            return body.HasMeta("scene_path")
                && body.GetMeta("scene_path").AsString() == expected.ScenePath
                && body.GetMeta("kind", string.Empty).AsString() == expected.Kind
                && body.GetMeta("style", string.Empty).AsString() == expected.Style
                && body.GetMeta("index", -1).AsInt32() == expected.Index
                && body.GetMeta("scene_paths", string.Empty).AsString()
                    == ResidentialStreetArtExpectedPaths(expected)
                && !body.GetMeta("used_fallback", true).AsBool();
        });

        var modelsReady = true;
        var authoredOnly = true;
        var visualsConfigured = true;
        var grounded = true;
        var boundsReady = true;
        var scalesReady = true;
        var collisionsReady = true;
        foreach (var body in bodies)
        {
            var model = body.GetNodeOrNull<Node3D>("Model");
            var meshCount = 0;
            if (model is null)
            {
                modelsReady = false;
                continue;
            }

            InspectResidentialStreetArtModel(
                model,
                ref meshCount,
                ref authoredOnly,
                ref visualsConfigured);
            modelsReady &= meshCount > 0;
            if (!TryGetResidentialStreetArtBounds(model, out var bounds))
            {
                grounded = false;
                boundsReady = false;
                collisionsReady = false;
                continue;
            }
            var expected = expectedByName[body.Name.ToString()];
            grounded &= Mathf.Abs(bounds.Position.Y) <= 0.025f
                && Mathf.Abs(body.Position.Y - expected.Position.Y) <= 0.002f;
            boundsReady &= body.GetMeta("model_bounds_size", Vector3.Zero).AsVector3()
                    .DistanceTo(bounds.Size) <= 0.01f
                && Mathf.Abs(body.GetMeta("model_bounds_min_y", -1.0f).AsSingle() - bounds.Position.Y) <= 0.01f
                && Mathf.Abs(bounds.Size.Y - expected.TargetHeight) <= 0.025f;
            scalesReady &= ResidentialStreetArtScalesReady(model, expected);
            collisionsReady &= ResidentialStreetArtCollisionReady(body, bounds, expected);
        }

        var kindCounts = bodies
            .GroupBy(body => body.GetMeta("kind", string.Empty).AsString())
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var countsReady = bodies.Count == ResidentialStreetArtBuilder.ExpectedPlacementCount
            && kindCounts.GetValueOrDefault("lamp") == ResidentialStreetArtBuilder.ExpectedLampCount
            && kindCounts.GetValueOrDefault("bin") == ResidentialStreetArtBuilder.ExpectedBinCount
            && kindCounts.GetValueOrDefault("market_loading") == ResidentialStreetArtBuilder.ExpectedMarketCount;
        var resultReady = _residentialStreetArtResult is
        {
            AuthoredPlacementCount: ResidentialStreetArtBuilder.ExpectedPlacementCount,
            MissingPlacementCount: 0,
            MissingSourceCount: 0,
            FallbackPlacementCount: 0
        };

        var requiredScenePaths = new HashSet<string>(StringComparer.Ordinal)
        {
            ResidentialStreetArtBuilder.LampScenePath,
            ResidentialStreetArtBuilder.CleanBinScenePath,
            ResidentialStreetArtBuilder.RustBinScenePath,
            ResidentialStreetArtBuilder.CoffeeCartScenePath,
            ResidentialStreetArtBuilder.WoodenCrateScenePath,
            ResidentialStreetArtBuilder.PlasticCrateScenePath,
            ResidentialStreetArtBuilder.WickerBasketScenePath
        };
        var pathsReady = _residentialStreetArtResult is not null
            && requiredScenePaths.SetEquals(_residentialStreetArtResult.ScenePaths);
        var roadMaterialReady = _materials.TryGetValue("residential_road", out var roadMaterial)
            && roadMaterial.AlbedoTexture?.ResourcePath.Contains("/asphalt_03_diff_1k.jpg", StringComparison.Ordinal) == true
            && roadMaterial.AlbedoTexture.ResourcePath.Contains("concrete_floor", StringComparison.Ordinal) == false;
        var primitiveStreetMaterialsAbsent = new[]
        {
            "residential_lamp_post", "residential_lamp_head", "residential_stall",
            "residential_stall_canopy", "residential_bin"
        }.All(id => !_materials.ContainsKey(id));
        var surfaceClear = ResidentialStreetArtProductionSurfaceClear(bodies);
        var signsSupported = ResidentialStreetArtSignsSupported(bodies);

        var valid = countsReady
            && resultReady
            && namesReady
            && exactPlacements
            && metadataReady
            && modelsReady
            && authoredOnly
            && visualsConfigured
            && grounded
            && boundsReady
            && scalesReady
            && collisionsReady
            && pathsReady
            && roadMaterialReady
            && primitiveStreetMaterialsAbsent
            && surfaceClear
            && signsSupported;
        GD.Print(
            $"RESIDENTIAL_STREET_ART_CHECK valid={valid} bodies={bodies.Count}/{ResidentialStreetArtBuilder.ExpectedPlacementCount} "
            + $"lamps={kindCounts.GetValueOrDefault("lamp")}/{ResidentialStreetArtBuilder.ExpectedLampCount} "
            + $"bins={kindCounts.GetValueOrDefault("bin")}/{ResidentialStreetArtBuilder.ExpectedBinCount} "
            + $"markets={kindCounts.GetValueOrDefault("market_loading")}/{ResidentialStreetArtBuilder.ExpectedMarketCount} "
            + $"missing={_residentialStreetArtResult?.MissingPlacementCount ?? -1} "
            + $"missing_sources={_residentialStreetArtResult?.MissingSourceCount ?? -1} "
            + $"fallbacks={_residentialStreetArtResult?.FallbackPlacementCount ?? -1} "
            + $"names={namesReady} exact={exactPlacements} metadata={metadataReady} models={modelsReady} "
            + $"authored_only={authoredOnly} visuals={visualsConfigured} grounded={grounded} "
            + $"bounds={boundsReady} scales={scalesReady} "
            + $"collision={collisionsReady} paths={pathsReady} asphalt={roadMaterialReady} "
            + $"legacy_primitives_absent={primitiveStreetMaterialsAbsent} surface_clear={surfaceClear} "
            + $"signs_supported={signsSupported}");
        GD.Print($"RESIDENTIAL_STREET_ART_PASS valid={valid}");
        QuitDiagnosticAfterSceneCleanup(valid ? 0 : 2);
    }

    private async void CaptureResidentialStreetArt()
    {
        PrepareResidentialStreetArtDiagnosticScene(hidePlayer: true);
        var lineupReady = StageResidentialStreetArtCaptureLineup();
        var camera = new Camera3D
        {
            Name = "ResidentialStreetArtCamera",
            Fov = 55.0f,
            Far = 180.0f
        };
        AddChild(camera);
        camera.GlobalPosition = new Vector3(-52.0f, 1.68f, 50.0f);
        camera.LookAt(new Vector3(-51.5f, 1.3f, 64.0f), Vector3.Up);
        camera.MakeCurrent();
        await WaitFrames(24);
        const string capturePath = "res://residential_street_art_validation.png";
        SaveViewportImage(capturePath);
        GD.Print(
            $"RESIDENTIAL_STREET_ART_CAPTURE path={capturePath} height=player "
            + $"layout=authored_scale_lineup ready={lineupReady}");
        QuitDiagnosticAfterSceneCleanup(0);
    }

    private void PrepareResidentialStreetArtDiagnosticScene(bool hidePlayer)
    {
        foreach (var enemy in _enemies)
        {
            enemy.ProcessMode = ProcessModeEnum.Disabled;
        }
        foreach (var mate in _squadMates)
        {
            mate.ProcessMode = ProcessModeEnum.Disabled;
        }
        foreach (var civilian in _civilians)
        {
            civilian.ProcessMode = ProcessModeEnum.Disabled;
        }
        _player.ProcessMode = ProcessModeEnum.Disabled;
        if (hidePlayer)
        {
            _player.Visible = false;
            _hud.Visible = false;
            foreach (var vehicle in _vehicles)
            {
                vehicle.ProcessMode = ProcessModeEnum.Disabled;
                vehicle.Visible = false;
            }
        }
    }

    private bool StageResidentialStreetArtCaptureLineup()
    {
        var root = _levelRoot.GetNodeOrNull<Node3D>(
            "ResidentialCommunity/ResidentialStreetAuthoredArt");
        if (root is null)
        {
            return false;
        }
        var lineup = new (string Name, Vector3 Position, float Yaw)[]
        {
            ("ResidentialStreetLamp_17", new Vector3(-58, 0.13f, 64), 90),
            ("ResidentialMarketLoading_05", new Vector3(-54.5f, 0.02f, 64), 180),
            ("ResidentialStreetBin_01", new Vector3(-51.5f, 0.02f, 62), -18),
            ("ResidentialStreetBin_02", new Vector3(-49, 0.02f, 62), 16),
            ("ResidentialMarketLoading_06", new Vector3(-46.5f, 0.02f, 64), 0)
        };
        foreach (var item in lineup)
        {
            if (root.GetNodeOrNull<Node3D>(item.Name) is not { } body)
            {
                return false;
            }
            body.GlobalPosition = item.Position;
            body.GlobalRotationDegrees = new Vector3(0, item.Yaw, 0);
        }
        return true;
    }

    private static void InspectResidentialStreetArtModel(
        Node node,
        ref int meshCount,
        ref bool authoredOnly,
        ref bool visualsConfigured)
    {
        if (node is MeshInstance3D mesh)
        {
            meshCount++;
            authoredOnly &= mesh.Mesh is not PrimitiveMesh;
        }
        if (node is GeometryInstance3D visual)
        {
            visualsConfigured &= visual.CastShadow == GeometryInstance3D.ShadowCastingSetting.On
                && visual.VisibilityRangeEnd >= 200.0f
                && visual.VisibilityRangeEndMargin > 0.0f;
        }
        var children = node.GetChildren();
        using var childrenBacking = children.AsDisposable();
        foreach (var child in children)
        {
            if (child is Node childNode)
            {
                InspectResidentialStreetArtModel(
                    childNode,
                    ref meshCount,
                    ref authoredOnly,
                    ref visualsConfigured);
            }
        }
    }

    private static bool ResidentialStreetArtCollisionReady(
        StaticBody3D body,
        Aabb bounds,
        ResidentialStreetArtPlacement expected)
    {
        if (body.CollisionLayer != 1
            || body.CollisionMask != 0
            || body.GetNodeOrNull<CollisionShape3D>("Collision") is not { Disabled: false } collision)
        {
            return false;
        }
        return collision.Shape switch
        {
            BoxShape3D box => expected.Kind != "lamp"
                && box.Size.X > 0.05f && box.Size.Y > 0.05f && box.Size.Z > 0.05f
                && box.Size.X <= bounds.Size.X + 0.02f
                && box.Size.Y <= bounds.Size.Y + 0.02f
                && box.Size.Z <= bounds.Size.Z + 0.02f
                && Mathf.Abs(box.Size.Y - bounds.Size.Y * ResidentialStreetArtCollisionHeightScale(expected)) <= 0.02f
                && Mathf.Abs(collision.Position.Y - box.Size.Y * 0.5f) <= 0.02f
                && new Vector2(collision.Position.X, collision.Position.Z).DistanceTo(
                    new Vector2(bounds.GetCenter().X, bounds.GetCenter().Z)) <= 0.02f,
            CylinderShape3D cylinder => expected.Kind == "lamp"
                && Mathf.Abs(cylinder.Height - bounds.Size.Y) <= 0.02f
                && cylinder.Radius is >= 0.09f and <= 0.2f
                && Mathf.Abs(collision.Position.Y - bounds.GetCenter().Y) <= 0.02f,
            _ => false
        };
    }

    private static float ResidentialStreetArtCollisionHeightScale(ResidentialStreetArtPlacement placement)
        => placement.Style == "coffee_cart" ? 0.52f : placement.Style == "mixed_supply" ? 0.94f : 0.96f;

    private static bool ResidentialStreetArtScalesReady(
        Node3D model,
        ResidentialStreetArtPlacement expected)
    {
        var authoredChildren = model.GetChildren().OfType<Node3D>().ToList();
        return authoredChildren.Count > 0 && authoredChildren.All(child =>
        {
            var targetHeight = child.GetMeta("target_height_m", -1.0f).AsSingle();
            var sourceHeight = child.GetMeta("source_height_m", -1.0f).AsSingle();
            var uniformScale = child.GetMeta("uniform_scale", -1.0f).AsSingle();
            var expectedHeight = child.Name.ToString() switch
            {
                "AuthoredPrimary" or "AuthoredStall" => expected.TargetHeight,
                "AuthoredWoodenCrate" => 0.35f,
                "AuthoredPlasticCrate" => 0.264f,
                "AuthoredWickerBasket" => 0.117f,
                _ => -1.0f
            };
            return expectedHeight > 0.0f
                && Mathf.Abs(targetHeight - expectedHeight) <= 0.003f
                && Mathf.Abs(sourceHeight * uniformScale - targetHeight) <= 0.004f
                && Mathf.Abs(child.Scale.X - child.Scale.Y) <= 0.0001f
                && Mathf.Abs(child.Scale.Y - child.Scale.Z) <= 0.0001f;
        });
    }

    private static string ResidentialStreetArtExpectedPaths(ResidentialStreetArtPlacement placement)
    {
        var paths = new List<string> { placement.ScenePath };
        if (placement.Style == "coffee_cart")
        {
            paths.Add(ResidentialStreetArtBuilder.WoodenCrateScenePath);
            paths.Add(ResidentialStreetArtBuilder.WickerBasketScenePath);
        }
        else if (placement.Style == "mixed_supply")
        {
            paths.Add(ResidentialStreetArtBuilder.PlasticCrateScenePath);
            paths.Add(ResidentialStreetArtBuilder.WickerBasketScenePath);
        }
        paths.Sort(StringComparer.Ordinal);
        return string.Join('|', paths);
    }

    private static bool ResidentialStreetArtSignsSupported(IEnumerable<StaticBody3D> bodies)
    {
        var anchors = bodies
            .SelectMany(body => body.GetChildren().OfType<Node3D>())
            .Where(node => node.Name.ToString().StartsWith("ResidentialDistrictSignAnchor_", StringComparison.Ordinal))
            .ToList();
        return anchors.Count == 2
            && anchors.All(anchor =>
                anchor.GetMeta("district_sign_layout", string.Empty).AsString() == "authored_lamp_banner"
                && anchor.Position.X > 0.1f
                && anchor.GetParent() is StaticBody3D support
                && support.GetMeta("kind", string.Empty).AsString() == "lamp"
                && anchor.GetNodeOrNull<Label3D>("DistrictLabel") is
                {
                    Visible: true,
                    Billboard: BaseMaterial3D.BillboardModeEnum.Disabled,
                    HorizontalAlignment: HorizontalAlignment.Left
                });
    }

    private bool ResidentialStreetArtProductionSurfaceClear(IEnumerable<StaticBody3D> bodies)
    {
        var road = _levelRoot.GetNodeOrNull<StaticBody3D>(
            "ResidentialCommunity/SouthResidentialBoulevard");
        var collision = road?.GetChildren().OfType<CollisionShape3D>().SingleOrDefault();
        if (collision?.Shape is not BoxShape3D roadShape)
        {
            return false;
        }

        var roadMinimum = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        var roadMaximum = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        for (var corner = 0; corner < 8; corner++)
        {
            var local = -roadShape.Size * 0.5f + new Vector3(
                (corner & 1) == 0 ? 0 : roadShape.Size.X,
                (corner & 2) == 0 ? 0 : roadShape.Size.Y,
                (corner & 4) == 0 ? 0 : roadShape.Size.Z);
            var point = collision.ToGlobal(local);
            roadMinimum = roadMinimum.Min(point);
            roadMaximum = roadMaximum.Max(point);
        }

        var southMarkets = bodies.Where(body =>
            body.GetMeta("kind", string.Empty).AsString() == "market_loading"
            && body.GetMeta("index", -1).AsInt32() is >= 5 and <= 8).ToList();
        const float surfaceTolerance = 0.002f;
        return southMarkets.Count == 4 && southMarkets.All(body =>
        {
            var position = body.GlobalPosition;
            return position.X >= roadMinimum.X && position.X <= roadMaximum.X
                && position.Z >= roadMinimum.Z && position.Z <= roadMaximum.Z
                && position.Y >= roadMaximum.Y - surfaceTolerance
                && body.GetNodeOrNull<Node3D>("Model") is { } model
                && TryGetResidentialStreetArtWorldBottom(model, out var modelBottom)
                && modelBottom >= roadMaximum.Y - surfaceTolerance;
        });
    }

    private static bool TryGetResidentialStreetArtWorldBottom(Node3D root, out float bottom)
    {
        var minimum = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        var maximum = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        var meshCount = 0;
        var parentTransform = root.GetParent() is Node3D parent
            ? parent.GlobalTransform
            : Transform3D.Identity;
        AccumulateResidentialStreetArtBounds(
            root,
            parentTransform,
            ref minimum,
            ref maximum,
            ref meshCount);
        bottom = minimum.Y;
        return meshCount > 0;
    }

    private static bool TryGetResidentialStreetArtBounds(Node3D root, out Aabb bounds)
    {
        var minimum = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        var maximum = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        var meshCount = 0;
        AccumulateResidentialStreetArtBounds(
            root,
            Transform3D.Identity,
            ref minimum,
            ref maximum,
            ref meshCount);
        bounds = meshCount == 0 ? new Aabb() : new Aabb(minimum, maximum - minimum);
        return meshCount > 0;
    }

    private static void AccumulateResidentialStreetArtBounds(
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
                AccumulateResidentialStreetArtBounds(
                    child3D,
                    transform,
                    ref minimum,
                    ref maximum,
                    ref meshCount);
            }
        }
    }
}
