using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private readonly record struct BazaarStairRun(
        string Name,
        Vector3 Low,
        Vector3 High);

    private readonly record struct BazaarWalkResult(
        string Name,
        bool Ascended,
        bool Descended,
        int AscendFrames,
        int DescendFrames,
        float AscendGain,
        float DescendLoss)
    {
        public bool Ready => Ascended && Descended;
    }

    private static IReadOnlyList<(string Name, IReadOnlyList<Vector3> Points)>
        BazaarGroundRoutes(DemolitionArenaLayout layout)
    {
        return new[]
        {
            ("attack-a", layout.AttackToAPath),
            ("attack-b", layout.AttackToBPath),
            ("attack-mid", layout.AttackMidPath),
            ("defense-a", layout.DefenderToAPath),
            ("defense-b", layout.DefenderToBPath),
            ("site-rotation", layout.SiteRotationPath),
            ("north-back-market", layout.AuxiliaryPaths[0])
        };
    }

    private static IReadOnlyList<(string Name, IReadOnlyList<Vector3> Points)>
        BazaarElevatedRoutes(DemolitionArenaLayout layout)
    {
        return new[]
        {
            ("a-gallery", layout.AuxiliaryPaths[1]),
            ("mid-mezzanine", layout.AuxiliaryPaths[2]),
            ("b-balcony", layout.AuxiliaryPaths[3])
        };
    }

    private static IReadOnlyList<Vector3> BazaarBServiceRoute(
        DemolitionArenaLayout layout)
        => new[]
        {
            layout.Origin + new Vector3(49.8f, 0.2f, 6.2f),
            layout.Origin + new Vector3(53.0f, 0.2f, 6.2f),
            layout.Origin + new Vector3(53.0f, 0.2f, 4.8f),
            layout.Origin + new Vector3(55.2f, 0.2f, 4.8f),
            layout.Origin + new Vector3(55.6f, 0.2f, 2.0f)
        };

    private static bool BazaarBServiceGeometryReady(
        DemolitionArenaLayout layout,
        out string failures)
    {
        var failed = new List<string>();
        var boxes = layout.CollisionBoxes.ToDictionary(box => box.Name, StringComparer.Ordinal);
        var exactContracts = new[]
        {
            (Name: "WallEastServicePocketClosure", Center: new Vector3(56.0f, 4.0f, 10.0f), Size: new Vector3(8.0f, 8.0f, 0.42f)),
            (Name: "CoverB_ServiceCounter", Center: new Vector3(58.5f, 0.58f, 3.7f), Size: new Vector3(0.62f, 1.16f, 4.6f))
        };
        foreach (var contract in exactContracts)
        {
            if (!boxes.TryGetValue(contract.Name, out var box))
            {
                failed.Add($"missing-{contract.Name}");
                continue;
            }
            var localCenter = box.Center - layout.Origin;
            if (localCenter.DistanceTo(contract.Center) > 0.002f
                || box.Size.DistanceTo(contract.Size) > 0.002f)
            {
                failed.Add($"contract-{contract.Name}-{localCenter}-{box.Size}");
            }
        }

        var obsoleteReturnBoxes = boxes.Values
            .Where(box => box.Name.StartsWith(
                "WallEastApproachSightReturn", StringComparison.Ordinal))
            .Select(box => box.Name)
            .ToArray();
        if (obsoleteReturnBoxes.Length > 0)
        {
            failed.Add($"obsolete-return-{string.Join(',', obsoleteReturnBoxes)}");
        }

        failures = string.Join('|', failed);
        return failed.Count == 0;
    }

    private static bool BazaarOpenApproachStairEntriesReady(
        DemolitionArenaLayout layout,
        out string failures)
    {
        var failed = new List<string>();
        var obsoleteVestibuleBoxes = layout.CollisionBoxes
            .Where(box => box.Name.Contains("SouthStairVestibule", StringComparison.Ordinal))
            .Select(box => box.Name)
            .ToArray();
        if (obsoleteVestibuleBoxes.Length > 0)
        {
            failed.Add($"obsolete-{string.Join(',', obsoleteVestibuleBoxes)}");
        }

        var entries = new[]
        {
            (Name: "a", CenterX: -56.0f, WallZ: -4.0f, ApproachZ: 7.2f),
            (Name: "b", CenterX: 56.0f, WallZ: -6.0f, ApproachZ: 6.8f),
            (Name: "mid", CenterX: -6.0f, WallZ: 34.0f, ApproachZ: 46.2f)
        };
        const float clearHalfWidth = 2.2f;
        foreach (var entry in entries)
        {
            var minimumZ = MathF.Min(entry.WallZ + 0.35f, entry.ApproachZ);
            var maximumZ = MathF.Max(entry.WallZ + 0.35f, entry.ApproachZ);
            var blockers = layout.CollisionBoxes.Where(box =>
            {
                var halfSize = box.Size * 0.5f;
                var overlapsWidth = box.Center.X + halfSize.X > entry.CenterX - clearHalfWidth
                    && box.Center.X - halfSize.X < entry.CenterX + clearHalfWidth;
                var overlapsDepth = box.Center.Z + halfSize.Z > minimumZ
                    && box.Center.Z - halfSize.Z < maximumZ;
                var overlapsPlayerHeight = box.Center.Y + halfSize.Y > 0.2f
                    && box.Center.Y - halfSize.Y < 2.6f;
                return overlapsWidth && overlapsDepth && overlapsPlayerHeight;
            }).Select(box => box.Name).ToArray();
            if (blockers.Length > 0)
            {
                failed.Add($"{entry.Name}-{string.Join(',', blockers)}");
            }
        }

        failures = string.Join('|', failed);
        return failed.Count == 0;
    }

    private static bool BazaarDetachedFoyerCollisionRemoved(
        DemolitionArenaLayout layout)
        => !layout.CollisionBoxes.Any(box =>
            box.Name.StartsWith("WallAttackFoyerWestSightBaffle", StringComparison.Ordinal)
            || box.Name.StartsWith("WallAttackFoyerEastSightBaffle", StringComparison.Ordinal));

    private static bool BazaarDetachedFoyerBafflesRemoved(
        Node3D? dressingRoot,
        DemolitionArenaLayout layout)
    {
        if (!BazaarDetachedFoyerCollisionRemoved(layout))
        {
            return false;
        }
        var model = dressingRoot?.GetNodeOrNull<Node3D>("BazaarCrossingAuthoredEnvironment");
        return IsInstanceValid(model)
            && model!.FindChild("Bazaar_Mid_WestSouthFrontageBaffle*", true, false) is null
            && model.FindChild("Bazaar_Mid_EastSouthFrontageBaffle*", true, false) is null;
    }

    private static IReadOnlyList<BazaarStairRun> BazaarStairRuns(
        DemolitionArenaLayout layout)
    {
        var runs = new List<BazaarStairRun>(6);
        foreach (var route in BazaarElevatedRoutes(layout))
        {
            var maximumY = route.Points.Max(point => point.Y);
            var plateau = route.Points
                .Select((point, index) => (Point: point, Index: index))
                .Where(entry => entry.Point.Y >= maximumY - 0.05f)
                .ToArray();
            if (plateau.Length == 0)
            {
                continue;
            }
            runs.Add(new BazaarStairRun(
                $"{route.Name}-entry-1",
                route.Points[0],
                plateau[0].Point));
            runs.Add(new BazaarStairRun(
                $"{route.Name}-entry-2",
                route.Points[^1],
                plateau[^1].Point));
        }
        return runs;
    }

    private static bool BazaarPhysicalRouteClear(
        World3D world,
        IReadOnlyList<Vector3> points,
        out string blocker)
        => TideglassPhysicalRouteClear(world, points, out blocker);

    private static bool BazaarSightlineBlocked(
        World3D world,
        Vector3 from,
        Vector3 to)
        => PhysicsRaycast.HasHit(
            world,
            from + Vector3.Up * 1.57f,
            to + Vector3.Up * 1.57f,
            1u);

    private static bool BazaarPointsSeparated(
        IReadOnlyList<Vector3> points,
        float minimumDistance)
    {
        for (var left = 0; left < points.Count; left++)
        {
            for (var right = left + 1; right < points.Count; right++)
            {
                if (points[left].DistanceTo(points[right]) < minimumDistance)
                {
                    return false;
                }
            }
        }
        return true;
    }

    private static bool BazaarTraversalGeometryReady(
        DemolitionArenaLayout layout,
        out string failures)
    {
        var boxes = layout.TraversalBoxes;
        var decks = boxes.Where(box => box.Name.EndsWith("Deck", StringComparison.Ordinal)).ToArray();
        var ramps = boxes.Where(box => box.Name.EndsWith("Ramp", StringComparison.Ordinal)).ToArray();
        var guardRails = layout.CollisionBoxes
            .Where(box => box.Name.StartsWith("GuardRail", StringComparison.Ordinal))
            .ToArray();
        var failed = new List<string>();
        if (boxes.Count != 9 || decks.Length != 3 || ramps.Length != 6)
        {
            failed.Add($"counts-{boxes.Count}-{decks.Length}-{ramps.Length}");
        }
        if (guardRails.Length != 15
            || boxes.Any(box => box.Name.StartsWith("GuardRail", StringComparison.Ordinal)))
        {
            failed.Add($"guardrail-separation-{guardRails.Length}");
        }
        if (boxes.Any(box => box.Visible))
        {
            failed.Add("visible-traversal-box");
        }
        if (layout.CollisionBoxes.Any(box => box.Visible))
        {
            failed.Add("visible-collision-box");
        }
        if (layout.CriticalPassageWidths.Any(width => width < 2.8f))
        {
            failed.Add("narrow-passage");
        }
        foreach (var ramp in ramps)
        {
            var width = Mathf.Min(ramp.Size.X, ramp.Size.Z);
            var angle = Mathf.Max(Mathf.Abs(ramp.Rotation.X), Mathf.Abs(ramp.Rotation.Z));
            if (width < 2.8f || Mathf.RadToDeg(angle) > 18.0f + 0.05f)
            {
                failed.Add(ramp.Name);
            }
        }
        var elevated = BazaarElevatedRoutes(layout);
        if (elevated.Count != 3 || BazaarStairRuns(layout).Count != 6)
        {
            failed.Add("missing-elevated-route");
        }
        foreach (var route in elevated)
        {
            var minimumY = route.Points.Min(point => point.Y);
            var maximumY = route.Points.Max(point => point.Y);
            var startsLow = route.Points[0].Y <= minimumY + 0.05f;
            var endsLow = route.Points[^1].Y <= minimumY + 0.05f;
            if (!startsLow || !endsLow || maximumY - minimumY < 2.5f)
            {
                failed.Add(route.Name);
            }
        }
        failures = string.Join('|', failed);
        return failed.Count == 0;
    }

    private async Task<IReadOnlyList<BazaarWalkResult>> BazaarWalkAllStairs(
        DemolitionArenaLayout layout)
    {
        var results = new List<BazaarWalkResult>(6);
        foreach (var run in BazaarStairRuns(layout))
        {
            var ascent = await BazaarWalkPlayer(run.Low, run.High, ascending: true);
            var descent = await BazaarWalkPlayer(run.High, run.Low, ascending: false);
            results.Add(new BazaarWalkResult(
                run.Name,
                ascent.Ready,
                descent.Ready,
                ascent.Frames,
                descent.Frames,
                ascent.HeightDelta,
                -descent.HeightDelta));
        }
        return results;
    }

    private async Task<(bool Ready, int Frames, float HeightDelta)> BazaarWalkPlayer(
        Vector3 start,
        Vector3 target,
        bool ascending)
    {
        Input.ActionRelease("move_forward");
        Input.ActionRelease("sprint");
        _player.ProcessMode = ProcessModeEnum.Inherit;
        _player.UiLocked = false;
        _player.RestoreMovementInput();
        _player.SetStaminaForDiagnostics(100.0f);
        _player.GlobalPosition = start;
        _player.Velocity = Vector3.Zero;
        await WaitFrames(6);

        var settledStart = _player.GlobalPosition;
        var reached = false;
        var frames = 0;
        Input.ActionPress("move_forward");
        for (; frames < 210; frames++)
        {
            _player.FaceWorldPointForDiagnostics(target);
            if (!_player.HasMovementIntent && frames > 2)
            {
                _player.RestoreMovementInput();
                Input.ActionPress("move_forward");
            }
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            var delta = target - _player.GlobalPosition;
            var horizontal = new Vector2(delta.X, delta.Z).Length();
            var heightReached = ascending
                ? _player.GlobalPosition.Y >= target.Y - 0.55f
                : _player.GlobalPosition.Y <= target.Y + 0.55f;
            if (horizontal < 0.9f && heightReached)
            {
                reached = true;
                break;
            }
        }
        Input.ActionRelease("move_forward");
        Input.ActionRelease("sprint");
        var heightDelta = _player.GlobalPosition.Y - settledStart.Y;
        var expectedHeightChange = Mathf.Abs(target.Y - start.Y);
        var ready = reached && (ascending
            ? heightDelta >= expectedHeightChange - 0.75f
            : -heightDelta >= expectedHeightChange - 0.75f);
        return (ready, frames, heightDelta);
    }

    private static bool BazaarAuthoredVisualsReady(
        Node3D? dressingRoot,
        DemolitionArenaLayout layout,
        out string failures,
        out int visibleMeshCount)
    {
        visibleMeshCount = 0;
        var failed = new List<string>();
        var model = dressingRoot?.GetNodeOrNull<Node3D>("BazaarCrossingAuthoredEnvironment");
        if (!IsInstanceValid(model)
            || !TideglassTryGetBounds(model!, null, out var minimum, out var maximum))
        {
            failures = "missing-model-or-bounds";
            return false;
        }
        var insideBounds = minimum.X >= layout.WorldBounds.Position.X - 0.2f
            && minimum.Z >= layout.WorldBounds.Position.Y - 0.2f
            && maximum.X <= layout.WorldBounds.End.X + 0.2f
            && maximum.Z <= layout.WorldBounds.End.Y + 0.2f;
        var coversArena = maximum.X - minimum.X >= 130.0f
            && maximum.Z - minimum.Z >= 106.0f
            && maximum.Y - minimum.Y >= 6.0f;
        if (!insideBounds || !coversArena)
        {
            failed.Add($"bounds-{minimum}-{maximum}");
        }

        var meshNodes = model!.FindChildren("*", "MeshInstance3D", true, false);
        using var meshNodesBacking = meshNodes.AsDisposable();
        var meshes = meshNodes.OfType<MeshInstance3D>().ToArray();
        foreach (var meshInstance in meshes)
        {
            if (meshInstance.Mesh is null || !meshInstance.Visible || !meshInstance.IsVisibleInTree())
            {
                continue;
            }
            visibleMeshCount++;
            if (meshInstance.Mesh is not ArrayMesh arrayMesh || arrayMesh.GetSurfaceCount() == 0)
            {
                failed.Add($"{meshInstance.Name}-not-array-mesh");
                continue;
            }
            for (var surface = 0; surface < arrayMesh.GetSurfaceCount(); surface++)
            {
                var material = meshInstance.GetActiveMaterial(surface)
                    ?? arrayMesh.SurfaceGetMaterial(surface);
                using var arrays = arrayMesh.SurfaceGetArrays(surface);
                var vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
                var uvs = arrays[(int)Mesh.ArrayType.TexUV].AsVector2Array();
                if (material is null || vertices.Length == 0 || uvs.Length != vertices.Length)
                {
                    failed.Add($"{meshInstance.Name}-surface-{surface}");
                }
            }
        }

        var traversalContracts = new[]
        {
            (Art: "Bazaar_A_Gallery_Deck", Box: "TraversalAGalleryDeck", Stair: (string?)null),
            (Art: "Bazaar_A_Gallery_South_Stair", Box: "TraversalAGallerySouthRamp", Stair: "a-gallery-entry-1"),
            (Art: "Bazaar_A_Gallery_Rear_Stair", Box: "TraversalAGalleryRearRamp", Stair: "a-gallery-entry-2"),
            (Art: "Bazaar_Mid_Mezzanine_Deck", Box: "TraversalMidMezzanineDeck", Stair: (string?)null),
            (Art: "Bazaar_Mid_Mezzanine_South_Stair", Box: "TraversalMidMezzanineSouthRamp", Stair: "mid-mezzanine-entry-1"),
            (Art: "Bazaar_Mid_Mezzanine_North_Stair", Box: "TraversalMidMezzanineNorthRamp", Stair: "mid-mezzanine-entry-2"),
            (Art: "Bazaar_B_Balcony_Deck", Box: "TraversalBBalconyDeck", Stair: (string?)null),
            (Art: "Bazaar_B_Balcony_South_Stair", Box: "TraversalBBalconySouthRamp", Stair: "b-balcony-entry-1"),
            (Art: "Bazaar_B_Balcony_Rear_Stair", Box: "TraversalBBalconyRearRamp", Stair: "b-balcony-entry-2")
        };
        var detailedStairs = BazaarDetailedStairRuns(layout);
        foreach (var contract in traversalContracts)
        {
            var matches = meshes.Where(mesh => mesh.Name == contract.Art).ToArray();
            var traversal = layout.TraversalBoxes.SingleOrDefault(box => box.Name == contract.Box);
            if (matches.Length != 1
                || string.IsNullOrEmpty(traversal.Name)
                || !BazaarTryMeshBounds(matches.FirstOrDefault(), out var artMin, out var artMax))
            {
                failed.Add($"missing-alignment-{contract.Art}-{matches.Length}");
                continue;
            }
            BazaarBoxBounds(traversal, out var boxMin, out var boxMax);
            if (!BazaarBoundsWithin(artMin, artMax, boxMin, boxMax, 0.14f))
            {
                failed.Add($"aabb-{contract.Art}-{artMin}-{artMax}-{boxMin}-{boxMax}");
            }
            if (contract.Stair is null)
            {
                continue;
            }
            var stair = detailedStairs.Single(run => run.Name == contract.Stair);
            var widthAxisIsX = traversal.Size.X < traversal.Size.Z;
            var artWidth = widthAxisIsX ? artMax.X - artMin.X : artMax.Z - artMin.Z;
            var collisionWidth = Mathf.Min(traversal.Size.X, traversal.Size.Z);
            var runAxisIsX = Mathf.Abs(stair.High.X - stair.Low.X)
                > Mathf.Abs(stair.High.Z - stair.Low.Z);
            var endpointsAligned = BazaarEndpointOnArtBoundary(
                    stair.Low, artMin, artMax, runAxisIsX)
                && BazaarEndpointOnArtBoundary(stair.High, artMin, artMax, runAxisIsX);
            if (Mathf.Abs(artWidth - collisionWidth) > 0.12f
                || !endpointsAligned
                || Mathf.Abs(artMax.Y - boxMax.Y) > 0.12f)
            {
                failed.Add($"stair-{contract.Art}-w{artWidth:0.00}/{collisionWidth:0.00}-e{endpointsAligned}-top{artMax.Y:0.00}/{boxMax.Y:0.00}");
            }
        }

        var texturedSurfaces = traversalContracts.Select(entry => entry.Art).Concat(new[]
        {
            "BazaarGroundAuthoredMesh",
            "Bazaar_Attacker_Foyer_Paving",
            "Bazaar_A_Approach_Paving",
            "Bazaar_Mid_Approach_Paving",
            "Bazaar_B_Approach_Paving",
            "Bazaar_Defender_Spawn_Paving",
            "Bazaar_A_InteriorFloor",
            "Bazaar_B_InteriorFloor",
            "Bazaar_B_ServicePassage_Floor",
            "Bazaar_A_SouthStair_OpenForecourt",
            "Bazaar_B_SouthStair_OpenForecourt",
            "Bazaar_Mid_SouthStair_OpenForecourt",
            "Bazaar_B_WarehouseRoof",
            "Bazaar_Mid_NorthConnector_Roof",
            "Bazaar_Mid_NorthTeaHall_Roof",
            "Bazaar_Mid_CenterProduceHall_Roof",
            "Bazaar_Mid_SouthCarpetHall_Roof"
        }).ToArray();
        var roadSurfaces = new HashSet<string>(texturedSurfaces.Where(name =>
            name == "BazaarGroundAuthoredMesh"
            || name.EndsWith("Paving", StringComparison.Ordinal)));
        foreach (var surfaceName in texturedSurfaces)
        {
            var mesh = meshes.SingleOrDefault(candidate => candidate.Name == surfaceName);
            if (mesh?.Mesh is not ArrayMesh arrayMesh)
            {
                failed.Add($"missing-textured-{surfaceName}");
                continue;
            }
            var hasTexturedSurface = false;
            for (var surface = 0; surface < arrayMesh.GetSurfaceCount(); surface++)
            {
                var material = mesh.GetActiveMaterial(surface)
                    ?? arrayMesh.SurfaceGetMaterial(surface);
                if (material is not BaseMaterial3D baseMaterial)
                {
                    if (roadSurfaces.Contains(surfaceName))
                    {
                        failed.Add($"road-pbr-{surfaceName}-{surface}-material");
                    }
                    continue;
                }
                var hasPrimaryTextures = BazaarMaterialHasPrimaryTextures(baseMaterial);
                hasTexturedSurface |= hasPrimaryTextures;
                if (!hasPrimaryTextures && !BazaarScalarPbrReady(baseMaterial))
                {
                    failed.Add($"scalar-pbr-{surfaceName}-{surface}");
                }
                if (roadSurfaces.Contains(surfaceName)
                    && (baseMaterial.Metallic > 0.18f || baseMaterial.Roughness < 0.65f))
                {
                    failed.Add($"road-pbr-{surfaceName}-{surface}-{baseMaterial.Metallic:0.00}-{baseMaterial.Roughness:0.00}");
                }
            }
            if (!hasTexturedSurface)
            {
                failed.Add($"texture-{surfaceName}");
            }
        }
        if (visibleMeshCount is < 500 or > 800)
        {
            failed.Add($"visible-mesh-count-{visibleMeshCount}/500-800");
        }
        failures = string.Join('|', failed.Take(24));
        return failed.Count == 0;
    }

    private static bool BazaarTryMeshBounds(
        MeshInstance3D? mesh,
        out Vector3 minimum,
        out Vector3 maximum)
    {
        minimum = new(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        maximum = new(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        if (!IsInstanceValid(mesh) || mesh!.Mesh is null)
        {
            return false;
        }
        var bounds = mesh.GetAabb();
        for (var corner = 0; corner < 8; corner++)
        {
            var local = bounds.Position + new Vector3(
                (corner & 1) == 0 ? 0.0f : bounds.Size.X,
                (corner & 2) == 0 ? 0.0f : bounds.Size.Y,
                (corner & 4) == 0 ? 0.0f : bounds.Size.Z);
            var point = mesh.ToGlobal(local);
            minimum = minimum.Min(point);
            maximum = maximum.Max(point);
        }
        return true;
    }

    private static void BazaarBoxBounds(
        DemolitionArenaBox box,
        out Vector3 minimum,
        out Vector3 maximum)
    {
        minimum = new(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        maximum = new(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        var basis = Basis.FromEuler(box.Rotation);
        for (var corner = 0; corner < 8; corner++)
        {
            var local = new Vector3(
                (corner & 1) == 0 ? -box.Size.X * 0.5f : box.Size.X * 0.5f,
                (corner & 2) == 0 ? -box.Size.Y * 0.5f : box.Size.Y * 0.5f,
                (corner & 4) == 0 ? -box.Size.Z * 0.5f : box.Size.Z * 0.5f);
            var point = box.Center + basis * local;
            minimum = minimum.Min(point);
            maximum = maximum.Max(point);
        }
    }

    private static bool BazaarBoundsWithin(
        Vector3 artMin,
        Vector3 artMax,
        Vector3 boxMin,
        Vector3 boxMax,
        float tolerance)
    {
        var minimumDelta = (artMin - boxMin).Abs();
        var maximumDelta = (artMax - boxMax).Abs();
        return Mathf.Max(
            Mathf.Max(minimumDelta.X, minimumDelta.Y),
            Mathf.Max(minimumDelta.Z, Mathf.Max(
                Mathf.Max(maximumDelta.X, maximumDelta.Y), maximumDelta.Z))) <= tolerance;
    }

    private static bool BazaarEndpointOnArtBoundary(
        Vector3 endpoint,
        Vector3 minimum,
        Vector3 maximum,
        bool runAxisIsX)
    {
        var axisDistance = runAxisIsX
            ? Mathf.Min(Mathf.Abs(endpoint.X - minimum.X), Mathf.Abs(endpoint.X - maximum.X))
            : Mathf.Min(Mathf.Abs(endpoint.Z - minimum.Z), Mathf.Abs(endpoint.Z - maximum.Z));
        var crossCenter = runAxisIsX
            ? (minimum.Z + maximum.Z) * 0.5f
            : (minimum.X + maximum.X) * 0.5f;
        var cross = runAxisIsX ? endpoint.Z : endpoint.X;
        return axisDistance <= 0.12f && Mathf.Abs(cross - crossCenter) <= 0.12f;
    }

    private static bool BazaarMaterialHasPrimaryTextures(BaseMaterial3D material)
        => material.AlbedoTexture is Texture2D
            && material.NormalTexture is Texture2D;

    private static bool BazaarScalarPbrReady(BaseMaterial3D material)
        => float.IsFinite(material.Metallic)
            && float.IsFinite(material.Roughness)
            && material.Metallic is >= 0.0f and <= 1.0f
            && material.Roughness is >= 0.08f and <= 1.0f;

}
