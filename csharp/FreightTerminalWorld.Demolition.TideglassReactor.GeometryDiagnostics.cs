using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private static bool TideglassPhysicalRouteClear(
        World3D world,
        IReadOnlyList<Vector3> points,
        out string blocker)
    {
        blocker = "none";
        if (points.Count < 2)
        {
            return false;
        }

        const float capsuleHeight = 1.75f;
        using var shape = new CapsuleShape3D { Radius = 0.38f, Height = capsuleHeight };
        using var query = new PhysicsShapeQueryParameters3D
        {
            Shape = shape,
            CollisionMask = 1,
            CollideWithAreas = false,
            CollideWithBodies = true,
            Margin = 0.005f
        };
        for (var segment = 0; segment < points.Count - 1; segment++)
        {
            var from = points[segment];
            var to = points[segment + 1];
            var samples = Mathf.Max(1, Mathf.CeilToInt(from.DistanceTo(to) / 0.65f));
            for (var sample = 0; sample <= samples; sample++)
            {
                var feet = from.Lerp(to, sample / (float)samples);
                query.Transform = new Transform3D(
                    Basis.Identity,
                    feet + Vector3.Up * (capsuleHeight * 0.5f + 0.04f));
                var hits = world.DirectSpaceState.IntersectShape(query, 8);
                using var hitsBacking = hits.AsDisposable();
                if (hits.Count == 0)
                {
                    continue;
                }
                using var hit = hits[0];
                using var colliderValue = hit[GodotPhysicsResultKeys.Collider];
                blocker = colliderValue.AsGodotObject() is Node collider
                    ? collider.Name.ToString()
                    : "unknown";
                return false;
            }
        }
        return true;
    }

    private async Task<(
        bool Ready,
        bool WestReady,
        int WestFrames,
        float WestGain,
        bool EastReady,
        int EastFrames,
        float EastGain)> TideglassWalkPlayerAcrossStairs(
        DemolitionArenaLayout layout)
    {
        var west = await TideglassWalkPlayerAcrossStair(
            layout,
            new Vector3(-7.25f, 0.22f, 26.5f),
            new Vector3(-3.75f, 2.35f, 26.5f));
        var east = await TideglassWalkPlayerAcrossStair(
            layout,
            new Vector3(7.25f, 0.22f, 26.5f),
            new Vector3(3.75f, 2.35f, 26.5f));
        return (
            west.Ready && east.Ready,
            west.Ready,
            west.Frames,
            west.Gain,
            east.Ready,
            east.Frames,
            east.Gain);
    }

    private async Task<(bool Ready, int Frames, float Gain)> TideglassWalkPlayerAcrossStair(
        DemolitionArenaLayout layout,
        Vector3 startPosition,
        Vector3 targetPosition)
    {
        Input.ActionRelease("move_forward");
        Input.ActionRelease("sprint");
        _player.ProcessMode = ProcessModeEnum.Inherit;
        _player.UiLocked = false;
        _player.RestoreMovementInput();
        _player.SetStaminaForDiagnostics(100.0f);
        _player.GlobalPosition = layout.Origin + startPosition;
        _player.Velocity = Vector3.Zero;
        await WaitFrames(8);

        var start = _player.GlobalPosition;
        var target = layout.Origin + targetPosition;
        var reached = false;
        var frames = 0;
        Input.ActionPress("move_forward");
        for (; frames < 240; frames++)
        {
            _player.FaceWorldPointForDiagnostics(target);
            if (!_player.HasMovementIntent && frames > 2)
            {
                _player.RestoreMovementInput();
                Input.ActionPress("move_forward");
            }
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            var delta = target - _player.GlobalPosition;
            var horizontalDistance = new Vector2(delta.X, delta.Z).Length();
            if (horizontalDistance < 0.7f && _player.GlobalPosition.Y >= layout.Origin.Y + 2.05f)
            {
                reached = true;
                break;
            }
        }
        Input.ActionRelease("move_forward");
        Input.ActionRelease("sprint");
        var gain = _player.GlobalPosition.Y - start.Y;
        var ready = reached
            && gain >= 1.75f
            && _player.GlobalPosition.Y >= layout.Origin.Y + 2.05f;
        return (ready, frames, gain);
    }

    private static bool TideglassPropCollisionTightlyFitsModel(
        Node3D root,
        DemolitionArenaProp prop)
    {
        var body = root.GetNodeOrNull<StaticBody3D>(prop.Name);
        var model = body?.GetNodeOrNull<Node3D>("Model");
        var collision = body?.GetNodeOrNull<CollisionShape3D>("Collision");
        if (!IsInstanceValid(body)
            || !IsInstanceValid(model)
            || collision?.Shape is not BoxShape3D box
            || !TideglassTryGetBounds(model!, body!, out var minimum, out var maximum))
        {
            return false;
        }

        var collisionMinimum = collision.Position - box.Size * 0.5f;
        var collisionMaximum = collision.Position + box.Size * 0.5f;
        var minimumPadding = minimum - collisionMinimum;
        var maximumPadding = collisionMaximum - maximum;
        const float coverageTolerance = -0.08f;
        const float maximumPaddingMeters = 0.22f;
        var ready = TideglassPaddingWithin(
                minimumPadding,
                coverageTolerance,
                maximumPaddingMeters)
            && TideglassPaddingWithin(
                maximumPadding,
                coverageTolerance,
                maximumPaddingMeters);
        if (!ready)
        {
            GD.Print(
                $"TIDEGLASS_TIGHT_COLLISION_CHECK prop={prop.Name} "
                + $"model={minimum}..{maximum} "
                + $"collision={collisionMinimum}..{collisionMaximum} "
                + $"padding={minimumPadding}/{maximumPadding}");
        }
        return ready;
    }

    private static bool TideglassDressingModelsInsideBounds(
        Node3D? dressingRoot,
        DemolitionArenaLayout layout,
        out string failures)
    {
        if (!IsInstanceValid(dressingRoot))
        {
            failures = "missing-root";
            return false;
        }

        var failedModels = dressingRoot!.GetChildren()
            .OfType<Node3D>()
            .Where(model => !TideglassTryGetBounds(model, null, out var minimum, out var maximum)
                || minimum.X < layout.WorldBounds.Position.X - 0.05f
                || minimum.Z < layout.WorldBounds.Position.Y - 0.05f
                || maximum.X > layout.WorldBounds.End.X + 0.05f
                || maximum.Z > layout.WorldBounds.End.Y + 0.05f)
            .Select(model => model.Name.ToString())
            .ToArray();
        failures = string.Join('|', failedModels);
        return failedModels.Length == 0;
    }

    private static bool TideglassTryGetBounds(
        Node3D model,
        Node3D? relativeTo,
        out Vector3 minimum,
        out Vector3 maximum)
    {
        minimum = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        maximum = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        var meshes = model.FindChildren("*", "MeshInstance3D", true, false);
        using var meshesBacking = meshes.AsDisposable();
        foreach (var child in meshes)
        {
            if (child is not MeshInstance3D mesh)
            {
                continue;
            }
            var meshBounds = mesh.GetAabb();
            for (var corner = 0; corner < 8; corner++)
            {
                var local = meshBounds.Position + new Vector3(
                    (corner & 1) == 0 ? 0.0f : meshBounds.Size.X,
                    (corner & 2) == 0 ? 0.0f : meshBounds.Size.Y,
                    (corner & 4) == 0 ? 0.0f : meshBounds.Size.Z);
                var world = mesh.ToGlobal(local);
                var point = relativeTo is null ? world : relativeTo.ToLocal(world);
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

    private static bool TideglassPaddingWithin(Vector3 padding, float minimum, float maximum)
        => padding.X >= minimum && padding.X <= maximum
            && padding.Y >= minimum && padding.Y <= maximum
            && padding.Z >= minimum && padding.Z <= maximum;

    private static bool TideglassPerimeterFenceReady(
        Node3D? dressingRoot,
        DemolitionArenaLayout layout)
    {
        var fence = dressingRoot?.GetNodeOrNull<Node3D>("TideglassPerimeterFence");
        if (!IsInstanceValid(fence)
            || !TideglassTryGetBounds(fence!, null, out var minimum, out var maximum))
        {
            return false;
        }

        var north = layout.CollisionBoxes.Single(box => box.Name == "NorthPerimeter");
        var south = layout.CollisionBoxes.Single(box => box.Name == "SouthPerimeter");
        var west = layout.CollisionBoxes.Single(box => box.Name == "WestPerimeter");
        var east = layout.CollisionBoxes.Single(box => box.Name == "EastPerimeter");
        return Mathf.Abs(minimum.X - west.Center.X) <= 0.1f
            && Mathf.Abs(maximum.X - east.Center.X) <= 0.1f
            && Mathf.Abs(minimum.Z - north.Center.Z) <= 0.1f
            && Mathf.Abs(maximum.Z - south.Center.Z) <= 0.1f
            && Mathf.Abs(minimum.Y - (layout.Origin.Y + 0.02f)) <= 0.05f
            && maximum.Y - minimum.Y >= 2.95f
            && maximum.Y - minimum.Y <= 3.05f;
    }

    private static bool TideglassPerimeterGatesReady(
        Node3D? dressingRoot,
        DemolitionArenaLayout layout,
        World3D world,
        out string failures)
    {
        if (!IsInstanceValid(dressingRoot))
        {
            failures = "missing-root";
            return false;
        }

        var gates = new[]
        {
            (
                Name: "EastPerimeterSecurityGate",
                CenterX: 67.5f,
                MinimumZ: -32.75f,
                MaximumZ: -22.39f,
                ViewX: 59.0f,
                RayStartX: 66.0f,
                RayEndX: 69.0f,
                Collider: "EastPerimeter"),
            (
                Name: "WestPerimeterServiceGate",
                CenterX: -67.5f,
                MinimumZ: -49.36f,
                MaximumZ: -39.22f,
                ViewX: -59.0f,
                RayStartX: -66.0f,
                RayEndX: -69.0f,
                Collider: "WestPerimeter")
        };
        var fence = dressingRoot!.GetNodeOrNull<Node3D>("TideglassPerimeterFence");
        var failedGates = new List<string>();
        foreach (var gate in gates)
        {
            var model = dressingRoot!.GetNodeOrNull<Node3D>(gate.Name);
            if (!IsInstanceValid(model)
                || !TideglassTryGetBounds(model!, null, out var minimum, out var maximum))
            {
                failedGates.Add($"{gate.Name}:missing");
                continue;
            }

            var centerZ = (gate.MinimumZ + gate.MaximumZ) * 0.5f;
            var boundsAligned = Mathf.Abs((minimum.X + maximum.X) * 0.5f - (layout.Origin.X + gate.CenterX)) <= 0.03f
                && minimum.Y <= layout.Origin.Y + 0.03f
                && maximum.Y >= layout.Origin.Y + 2.98f
                && minimum.Z <= layout.Origin.Z + gate.MinimumZ + 0.02f
                && minimum.Z >= layout.Origin.Z + gate.MinimumZ - 0.06f
                && maximum.Z >= layout.Origin.Z + gate.MaximumZ - 0.02f
                && maximum.Z <= layout.Origin.Z + gate.MaximumZ + 0.06f;
            var visibleCoverage = true;
            var fenceOpeningClear = IsInstanceValid(fence);
            var sampleHeights = new[] { 0.25f, 1.05f, 1.57f, 2.5f };
            for (var spanSample = 1; spanSample <= 9 && visibleCoverage; spanSample++)
            {
                var sampleZ = Mathf.Lerp(gate.MinimumZ, gate.MaximumZ, spanSample / 10.0f);
                foreach (var sampleHeight in sampleHeights)
                {
                    var visibleFrom = layout.Origin + new Vector3(gate.RayStartX, sampleHeight, sampleZ);
                    var visibleTo = layout.Origin + new Vector3(gate.RayEndX, sampleHeight, sampleZ);
                    if (!TideglassVisibleMeshBlocksSegment(model!, visibleFrom, visibleTo))
                    {
                        visibleCoverage = false;
                        break;
                    }
                    if (fenceOpeningClear && TideglassVisibleMeshBlocksSegment(fence!, visibleFrom, visibleTo))
                    {
                        fenceOpeningClear = false;
                    }
                }
            }
            var fenceReturnsAtEdges = IsInstanceValid(fence);
            foreach (var sampleZ in new[] { gate.MinimumZ - 0.15f, gate.MaximumZ + 0.15f })
            {
                var edgeFrom = layout.Origin + new Vector3(gate.RayStartX, 1.57f, sampleZ);
                var edgeTo = layout.Origin + new Vector3(gate.RayEndX, 1.57f, sampleZ);
                if (fenceReturnsAtEdges)
                {
                    fenceReturnsAtEdges = TideglassVisibleMeshBlocksSegment(fence!, edgeFrom, edgeTo);
                }
            }
            var fenceOpeningAligned = fenceOpeningClear && fenceReturnsAtEdges;
            var rayFrom = layout.Origin + new Vector3(gate.RayStartX, 1.57f, centerZ);
            var rayTo = layout.Origin + new Vector3(gate.RayEndX, 1.57f, centerZ);
            var collisionAligned = PhysicsRaycast.TryHit(world, rayFrom, rayTo, 1u, out var hit)
                && hit.Collider is Node collider
                && collider.Name == gate.Collider;
            var playerViewpoint = layout.Origin + new Vector3(gate.ViewX, 1.57f, centerZ);
            var viewpointClear = layout.CollisionBoxes.All(box => !TideglassPointInsideCollisionBox(playerViewpoint, box));
            var playerSideVisible = PhysicsRaycast.TryHit(world, playerViewpoint, rayTo, 1u, out var playerHit)
                && playerHit.Collider is Node playerCollider
                && playerCollider.Name == gate.Collider;
            var majorShellClear = true;
            if (gate.Name == "EastPerimeterSecurityGate")
            {
                var shell = layout.CollisionBoxes.Single(box => box.Name == "BrickFactoryShell");
                var shellMinimum = shell.Center - shell.Size * 0.5f;
                var shellMaximum = shell.Center + shell.Size * 0.5f;
                const float requiredHorizontalClearance = 0.45f;
                majorShellClear = maximum.X <= shellMinimum.X - requiredHorizontalClearance
                    || minimum.X >= shellMaximum.X + requiredHorizontalClearance
                    || maximum.Z <= shellMinimum.Z - requiredHorizontalClearance
                    || minimum.Z >= shellMaximum.Z + requiredHorizontalClearance;
            }
            if (!boundsAligned
                || !visibleCoverage
                || !collisionAligned
                || !fenceOpeningAligned
                || !viewpointClear
                || !playerSideVisible
                || !majorShellClear)
            {
                failedGates.Add(
                    $"{gate.Name}:bounds={boundsAligned}:visible={visibleCoverage}:collision={collisionAligned}"
                    + $":opening={fenceOpeningAligned}:viewpoint={viewpointClear}"
                    + $":player_visible={playerSideVisible}:shell_clear={majorShellClear}");
            }
        }
        failures = string.Join('|', failedGates);
        return failedGates.Count == 0;
    }

    private static bool TideglassPointInsideCollisionBox(Vector3 point, DemolitionArenaBox box)
    {
        var inverse = new Basis(Quaternion.FromEuler(box.Rotation)).Inverse();
        var local = inverse * (point - box.Center);
        var half = box.Size * 0.5f;
        return Mathf.Abs(local.X) <= half.X
            && Mathf.Abs(local.Y) <= half.Y
            && Mathf.Abs(local.Z) <= half.Z;
    }

    private static bool TideglassVisibleMeshBlocksSegment(
        Node3D model,
        Vector3 from,
        Vector3 to)
    {
        var meshes = model.FindChildren("*", "MeshInstance3D", true, false);
        using var meshesBacking = meshes.AsDisposable();
        foreach (var child in meshes)
        {
            if (child is not MeshInstance3D mesh
                || mesh.Mesh is null
                || !mesh.Visible
                || !mesh.IsVisibleInTree()
                || (mesh.Layers & 1u) == 0)
            {
                continue;
            }
            var faces = mesh.Mesh.GetFaces();
            for (var face = 0; face + 2 < faces.Length; face += 3)
            {
                using var intersection = Geometry3D.SegmentIntersectsTriangle(
                    from,
                    to,
                    mesh.ToGlobal(faces[face]),
                    mesh.ToGlobal(faces[face + 1]),
                    mesh.ToGlobal(faces[face + 2]));
                if (intersection.VariantType == Variant.Type.Vector3)
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static bool TideglassRoadSurfacesAligned(
        Node3D? dressingRoot,
        DemolitionArenaLayout layout,
        out string failures)
    {
        if (!IsInstanceValid(dressingRoot))
        {
            failures = "missing-root";
            return false;
        }

        var floor = layout.CollisionBoxes.Single(box => box.Name == "ArenaFloor");
        var floorTop = floor.Center.Y + floor.Size.Y * 0.5f;
        var roadNames = new[]
        {
            "TideglassRoadBase",
            "CivicRoadNorthWest",
            "CivicRoadNorth",
            "CivicRoadNorthEast",
            "CivicRoadWest",
            "CivicCrossroad",
            "CivicRoadEast",
            "CivicRoadSouthWest",
            "CivicRoadSouthEast"
        };
        var failedRoads = roadNames.Where(name =>
        {
            var road = dressingRoot!.GetNodeOrNull<Node3D>(name);
            if (!IsInstanceValid(road)
                || !TideglassTryGetBounds(road!, null, out var minimum, out var maximum))
            {
                return true;
            }
            var maximumSurfaceOffset = name == "TideglassRoadBase" ? 0.006f : 0.012f;
            return minimum.Y > floorTop + 0.001f
                || maximum.Y < floorTop - 0.006f
                || maximum.Y > floorTop + maximumSurfaceOffset;
        }).ToArray();
        failures = string.Join('|', failedRoads);
        return failedRoads.Length == 0;
    }

    private static bool TideglassTowerCollisionReady(DemolitionArenaLayout layout)
    {
        var foundation = layout.CollisionBoxes.SingleOrDefault(box => box.Name == "ConstructionTowerFoundation");
        if (foundation.Name != "ConstructionTowerFoundation"
            || (foundation.Center - layout.Origin).DistanceTo(new Vector3(-55.0f, 0.2f, 22.0f)) > 0.01f
            || !foundation.Size.IsEqualApprox(new Vector3(13.9f, 0.36f, 16.6f)))
        {
            return false;
        }

        var columns = layout.CollisionBoxes
            .Where(box => box.Name.StartsWith("ConstructionTowerColumn_", StringComparison.Ordinal))
            .OrderBy(box => box.Name, StringComparer.Ordinal)
            .ToArray();
        var expected = new[]
        {
            (Center: new Vector3(-61.75f, 23.22f, 13.9f), Height: 46.4f),
            (Center: new Vector3(-61.75f, 23.06f, 22.0f), Height: 46.08f),
            (Center: new Vector3(-61.75f, 14.26f, 30.1f), Height: 28.48f),
            (Center: new Vector3(-48.25f, 23.22f, 13.9f), Height: 46.4f),
            (Center: new Vector3(-48.25f, 23.06f, 22.0f), Height: 46.08f),
            (Center: new Vector3(-48.25f, 14.26f, 30.1f), Height: 28.48f)
        };
        if (columns.Length != expected.Length)
        {
            return false;
        }
        for (var index = 0; index < columns.Length; index++)
        {
            if ((columns[index].Center - layout.Origin).DistanceTo(expected[index].Center) > 0.01f
                || !columns[index].Size.IsEqualApprox(new Vector3(0.42f, expected[index].Height, 0.42f)))
            {
                return false;
            }
        }
        return true;
    }

}
