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

    private static bool TideglassFindConstructionOfficeDoor(
        World3D world,
        DemolitionArenaLayout layout,
        out Vector3 outside,
        out Vector3 inside,
        out string report)
    {
        var attempts = 0;
        var lastBlocker = "none";
        for (var heightIndex = 0; heightIndex <= 5; heightIndex++)
        {
            var feetHeight = 2.74f + heightIndex * 0.06f;
            for (var crossIndex = 0; crossIndex <= 18; crossIndex++)
            {
                var cross = 0.25f + crossIndex * 0.10f;
                var candidateOutside = layout.Origin + new Vector3(26.28f, feetHeight, 42.6f + cross);
                var candidateInside = layout.Origin + new Vector3(25.18f, feetHeight, 42.6f + cross);
                attempts++;
                if (!TideglassPhysicalRouteClear(
                        world,
                        new[] { candidateOutside, candidateInside, candidateOutside },
                        out lastBlocker))
                {
                    continue;
                }
                outside = candidateOutside;
                inside = candidateInside;
                report = $"attempts={attempts}:height={feetHeight:0.00}:cross={cross:0.00}";
                return true;
            }
        }
        outside = Vector3.Zero;
        inside = Vector3.Zero;
        report = $"attempts={attempts}:blocker={lastBlocker}";
        return false;
    }

    private async Task<(
        bool Ready,
        bool WestReady,
        int WestFrames,
        float WestGain,
        bool EastReady,
        int EastFrames,
        float EastGain,
        bool SiteOfficeReady,
        int SiteOfficeFrames,
        float SiteOfficeGain,
        bool SiteOfficeDoorReady,
        int SiteOfficeDoorFrames,
        bool SiteOfficeExitReady,
        int SiteOfficeExitFrames,
        float SiteOfficeExitDrop,
        bool FoundryDoorReady,
        int FoundryDoorFrames,
        bool TowerStairsReady,
        int TowerStairFrames,
        float TowerStairGain)> TideglassWalkPlayerAcrossStairs(
        DemolitionArenaLayout layout,
        Node3D? dressingRoot,
        Vector3 officeOutside,
        Vector3 officeInside)
    {
        var west = await TideglassWalkPlayerAcrossStair(
            layout,
            new Vector3(-7.25f, 0.22f, 26.5f),
            new Vector3(-3.75f, 2.35f, 26.5f));
        var east = await TideglassWalkPlayerAcrossStair(
            layout,
            new Vector3(7.25f, 0.22f, 26.5f),
            new Vector3(3.75f, 2.35f, 26.5f));
        var siteOffice = await TideglassWalkPlayerAcrossStair(
            layout,
            new Vector3(30.7f, 0.22f, 46.1f),
            new Vector3(26.8f, 2.75f, 46.1f),
            minimumDestinationHeight: 2.35f,
            minimumGain: 2.05f,
            horizontalTolerance: 0.85f,
            maximumFrames: 300);
        var siteOfficeDoor = siteOffice.Ready
            ? await TideglassWalkPlayerWaypoints(
                new[]
                {
                    layout.Origin + new Vector3(26.55f, 2.75f, 44.35f),
                    officeOutside,
                    officeInside,
                    officeOutside,
                    layout.Origin + new Vector3(26.8f, 2.75f, 46.1f)
                },
                horizontalTolerance: 0.58f,
                maximumFramesPerWaypoint: 220)
            : (Ready: false, Frames: 0, Gain: 0.0f);
        var siteOfficeExit = siteOfficeDoor.Ready
            ? await TideglassWalkPlayerDownStair(
                layout,
                new Vector3(30.7f, 0.22f, 46.1f),
                maximumDestinationHeight: 0.75f,
                minimumDrop: 1.75f,
                horizontalTolerance: 0.9f,
                maximumFrames: 300)
            : (Ready: false, Frames: 0, Drop: 0.0f);
        var foundry = layout.Props.Single(prop => prop.Name == "NorthFoundryTenement");
        var foundryDoor = await TideglassWalkPlayerRoute(
            foundry.Position + new Vector3(0, 0.22f, 2.65f),
            new[]
            {
                foundry.Position + new Vector3(0, 0.70f, 1.10f),
                foundry.Position + new Vector3(0, 1.03f, 0.25f),
                foundry.Position + new Vector3(0, 1.03f, -2.50f),
                foundry.Position + new Vector3(0, 1.03f, 0.25f),
                foundry.Position + new Vector3(0, 0.22f, 2.65f)
            },
            horizontalTolerance: 0.55f,
            maximumFramesPerWaypoint: 220);
        var constructionBuilding = dressingRoot?.GetNodeOrNull<Node3D>("ConstructionBuilding");
        var towerStart = IsInstanceValid(constructionBuilding)
            ? constructionBuilding!.ToGlobal(new Vector3(2.30f, 0.22f, 4.50f))
            : Vector3.Zero;
        var towerWaypoints = IsInstanceValid(constructionBuilding)
            ? new[]
            {
                constructionBuilding!.ToGlobal(new Vector3(2.30f, 1.50f, 2.88f)),
                constructionBuilding.ToGlobal(new Vector3(2.30f, 1.50f, 1.78f)),
                constructionBuilding.ToGlobal(new Vector3(4.15f, 1.50f, 1.78f)),
                constructionBuilding.ToGlobal(new Vector3(4.15f, 2.78f, 4.45f)),
                constructionBuilding.ToGlobal(new Vector3(4.15f, 2.78f, 5.25f)),
                constructionBuilding.ToGlobal(new Vector3(2.30f, 2.78f, 5.25f)),
                constructionBuilding.ToGlobal(new Vector3(2.30f, 4.06f, 2.88f)),
                constructionBuilding.ToGlobal(new Vector3(2.30f, 4.06f, 1.78f)),
                constructionBuilding.ToGlobal(new Vector3(4.15f, 4.06f, 1.78f)),
                constructionBuilding.ToGlobal(new Vector3(4.15f, 5.34f, 4.45f))
            }
            : Array.Empty<Vector3>();
        var towerStairs = towerWaypoints.Length > 0
            ? await TideglassWalkPlayerRoute(
                towerStart,
                towerWaypoints,
                horizontalTolerance: 0.46f,
                maximumFramesPerWaypoint: 260,
                verticalTolerance: 0.28f)
            : (Ready: false, Frames: 0, Gain: 0.0f);
        var towerStairsReady = towerStairs.Ready && towerStairs.Gain >= 4.65f;
        return (
            west.Ready
                && east.Ready
                && siteOffice.Ready
                && siteOfficeDoor.Ready
                && siteOfficeExit.Ready
                && foundryDoor.Ready
                && towerStairsReady,
            west.Ready,
            west.Frames,
            west.Gain,
            east.Ready,
            east.Frames,
            east.Gain,
            siteOffice.Ready,
            siteOffice.Frames,
            siteOffice.Gain,
            siteOfficeDoor.Ready,
            siteOfficeDoor.Frames,
            siteOfficeExit.Ready,
            siteOfficeExit.Frames,
            siteOfficeExit.Drop,
            foundryDoor.Ready,
            foundryDoor.Frames,
            towerStairsReady,
            towerStairs.Frames,
            towerStairs.Gain);
    }

    private async Task<(bool Ready, int Frames, float Gain)> TideglassWalkPlayerRoute(
        Vector3 start,
        IReadOnlyList<Vector3> waypoints,
        float horizontalTolerance,
        int maximumFramesPerWaypoint,
        float verticalTolerance = 0.72f)
    {
        Input.ActionRelease("move_forward");
        Input.ActionRelease("sprint");
        _player.ProcessMode = ProcessModeEnum.Inherit;
        _player.UiLocked = false;
        _player.RestoreMovementInput();
        _player.SetStaminaForDiagnostics(100.0f);
        _player.GlobalPosition = start;
        _player.Velocity = Vector3.Zero;
        await WaitFrames(8);
        var settledStart = _player.GlobalPosition;
        var traversal = await TideglassWalkPlayerWaypoints(
            waypoints,
            horizontalTolerance,
            maximumFramesPerWaypoint,
            verticalTolerance);
        return (
            traversal.Ready,
            traversal.Frames,
            _player.GlobalPosition.Y - settledStart.Y);
    }

    private async Task<(bool Ready, int Frames, float Gain)> TideglassWalkPlayerWaypoints(
        IReadOnlyList<Vector3> waypoints,
        float horizontalTolerance,
        int maximumFramesPerWaypoint,
        float verticalTolerance = 0.72f)
    {
        if (waypoints.Count == 0)
        {
            return (false, 0, 0.0f);
        }
        var startHeight = _player.GlobalPosition.Y;
        var totalFrames = 0;
        for (var waypointIndex = 0; waypointIndex < waypoints.Count; waypointIndex++)
        {
            var target = waypoints[waypointIndex];
            var reached = false;
            Input.ActionPress("move_forward");
            for (var frame = 0; frame < maximumFramesPerWaypoint; frame++)
            {
                totalFrames++;
                _player.FaceWorldPointForDiagnostics(target);
                if (!_player.HasMovementIntent && frame > 2)
                {
                    _player.RestoreMovementInput();
                    Input.ActionPress("move_forward");
                }
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                var delta = target - _player.GlobalPosition;
                if (new Vector2(delta.X, delta.Z).Length() < horizontalTolerance
                    && Mathf.Abs(delta.Y) < verticalTolerance)
                {
                    reached = true;
                    break;
                }
            }
            Input.ActionRelease("move_forward");
            if (!reached)
            {
                GD.Print(
                    $"TIDEGLASS_WAYPOINT_FAIL index={waypointIndex + 1}/{waypoints.Count} "
                    + $"target={target} player={_player.GlobalPosition} "
                    + $"horizontal={new Vector2(
                        target.X - _player.GlobalPosition.X,
                        target.Z - _player.GlobalPosition.Z).Length():0.000} "
                    + $"vertical={Mathf.Abs(target.Y - _player.GlobalPosition.Y):0.000}");
                return (false, totalFrames, _player.GlobalPosition.Y - startHeight);
            }
        }
        Input.ActionRelease("move_forward");
        Input.ActionRelease("sprint");
        return (true, totalFrames, _player.GlobalPosition.Y - startHeight);
    }

    private async Task<(bool Ready, int Frames, float Gain)> TideglassWalkPlayerAcrossStair(
        DemolitionArenaLayout layout,
        Vector3 startPosition,
        Vector3 targetPosition,
        float minimumDestinationHeight = 2.05f,
        float minimumGain = 1.75f,
        float horizontalTolerance = 0.7f,
        int maximumFrames = 240)
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
        for (; frames < maximumFrames; frames++)
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
            if (horizontalDistance < horizontalTolerance
                && _player.GlobalPosition.Y >= layout.Origin.Y + minimumDestinationHeight)
            {
                reached = true;
                break;
            }
        }
        Input.ActionRelease("move_forward");
        Input.ActionRelease("sprint");
        var gain = _player.GlobalPosition.Y - start.Y;
        var ready = reached
            && gain >= minimumGain
            && _player.GlobalPosition.Y >= layout.Origin.Y + minimumDestinationHeight;
        return (ready, frames, gain);
    }

    private async Task<(bool Ready, int Frames, float Drop)> TideglassWalkPlayerDownStair(
        DemolitionArenaLayout layout,
        Vector3 targetPosition,
        float maximumDestinationHeight,
        float minimumDrop,
        float horizontalTolerance,
        int maximumFrames)
    {
        Input.ActionRelease("move_forward");
        Input.ActionRelease("sprint");
        _player.ProcessMode = ProcessModeEnum.Inherit;
        _player.UiLocked = false;
        _player.RestoreMovementInput();
        _player.SetStaminaForDiagnostics(100.0f);
        _player.Velocity = Vector3.Zero;
        await WaitFrames(3);

        var start = _player.GlobalPosition;
        var target = layout.Origin + targetPosition;
        var reached = false;
        var frames = 0;
        Input.ActionPress("move_forward");
        for (; frames < maximumFrames; frames++)
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
            if (horizontalDistance < horizontalTolerance
                && _player.GlobalPosition.Y <= layout.Origin.Y + maximumDestinationHeight)
            {
                reached = true;
                break;
            }
        }
        Input.ActionRelease("move_forward");
        Input.ActionRelease("sprint");
        var drop = start.Y - _player.GlobalPosition.Y;
        var ready = reached
            && drop >= minimumDrop
            && _player.GlobalPosition.Y <= layout.Origin.Y + maximumDestinationHeight;
        return (ready, frames, drop);
    }

    private static bool TideglassConstructionOfficeRoomsSealed(
        World3D world,
        DemolitionArenaLayout layout,
        out string blockers)
    {
        var office = layout.Props.First(prop => prop.Name == "SightBlockConstructionSiteOffice");
        var roomBlockers = new List<string>();
        for (var pieceIndex = 0;
             pieceIndex < office.AuthoredSolidCollisionPieceCount;
             pieceIndex++)
        {
            TideglassPropPieceWorld(
                office,
                office.CollisionPieceAt(pieceIndex),
                out var center,
                out var basis,
                out var half);
            var verticalExtent = Mathf.Abs(basis.X.Y) * half.X
                + Mathf.Abs(basis.Y.Y) * half.Y
                + Mathf.Abs(basis.Z.Y) * half.Z;
            var feet = new Vector3(center.X, center.Y - verticalExtent + 0.2f, center.Z);
            var sealedRoom = !TideglassPhysicalRouteClear(
                world,
                new[] { feet, feet + Vector3.Right * 0.05f },
                out var blocker);
            roomBlockers.Add($"{pieceIndex + 1}:{blocker}");
            if (!sealedRoom || blocker != office.Name)
            {
                blockers = string.Join('|', roomBlockers);
                return false;
            }
        }

        blockers = string.Join('|', roomBlockers);
        return office.AuthoredSolidCollisionPieceCount == 2;
    }

    private static bool TideglassPropsSeparated(
        IReadOnlyList<DemolitionArenaProp> props,
        out string overlapPair)
    {
        for (var first = 0; first < props.Count; first++)
        {
            for (var second = first + 1; second < props.Count; second++)
            {
                for (var firstPiece = 0; firstPiece < props[first].CollisionPieceCount; firstPiece++)
                {
                    for (var secondPiece = 0; secondPiece < props[second].CollisionPieceCount; secondPiece++)
                    {
                        if (!TideglassPropPiecesOverlap(
                                props[first],
                                props[first].CollisionPieceAt(firstPiece),
                                props[second],
                                props[second].CollisionPieceAt(secondPiece),
                                0.25f))
                        {
                            continue;
                        }
                        overlapPair = $"{props[first].Name}[{firstPiece + 1}]|"
                            + $"{props[second].Name}[{secondPiece + 1}]";
                        return false;
                    }
                }
            }
        }
        overlapPair = "none";
        return true;
    }

    private static bool TideglassPropInsideBounds(
        DemolitionArenaLayout layout,
        DemolitionArenaProp prop)
    {
        for (var pieceIndex = 0; pieceIndex < prop.CollisionPieceCount; pieceIndex++)
        {
            var bounds = TideglassPropPieceBounds(prop, prop.CollisionPieceAt(pieceIndex), 0.25f);
            if (bounds.Position.X < layout.WorldBounds.Position.X
                || bounds.Position.Y < layout.WorldBounds.Position.Y
                || bounds.End.X > layout.WorldBounds.End.X
                || bounds.End.Y > layout.WorldBounds.End.Y)
            {
                return false;
            }
        }
        return true;
    }

    private static bool TideglassPointOverlapsProp(
        Vector3 point,
        float radius,
        DemolitionArenaProp prop)
    {
        for (var pieceIndex = 0; pieceIndex < prop.CollisionPieceCount; pieceIndex++)
        {
            if (TideglassPropPieceBounds(
                    prop,
                    prop.CollisionPieceAt(pieceIndex),
                    radius).HasPoint(new Vector2(point.X, point.Z)))
            {
                return true;
            }
        }
        return false;
    }

    private static bool TideglassPropPiecesOverlap(
        DemolitionArenaProp firstProp,
        DemolitionArenaPropCollisionBox firstPiece,
        DemolitionArenaProp secondProp,
        DemolitionArenaPropCollisionBox secondPiece,
        float margin)
    {
        TideglassPropPieceWorld(firstProp, firstPiece, out var firstCenter, out var firstBasis, out var firstHalf);
        TideglassPropPieceWorld(secondProp, secondPiece, out var secondCenter, out var secondBasis, out var secondHalf);
        var firstVerticalExtent = Mathf.Abs(firstBasis.X.Y) * firstHalf.X
            + Mathf.Abs(firstBasis.Y.Y) * firstHalf.Y
            + Mathf.Abs(firstBasis.Z.Y) * firstHalf.Z;
        var secondVerticalExtent = Mathf.Abs(secondBasis.X.Y) * secondHalf.X
            + Mathf.Abs(secondBasis.Y.Y) * secondHalf.Y
            + Mathf.Abs(secondBasis.Z.Y) * secondHalf.Z;
        if (firstCenter.Y + firstVerticalExtent <= secondCenter.Y - secondVerticalExtent
            || secondCenter.Y + secondVerticalExtent <= firstCenter.Y - firstVerticalExtent)
        {
            return false;
        }
        return TideglassPropPieceBounds(firstProp, firstPiece, margin)
            .Intersects(TideglassPropPieceBounds(secondProp, secondPiece, margin));
    }

    private static Rect2 TideglassPropPieceBounds(
        DemolitionArenaProp prop,
        DemolitionArenaPropCollisionBox piece,
        float margin)
    {
        TideglassPropPieceWorld(prop, piece, out var center, out var basis, out var half);
        var extentX = Mathf.Abs(basis.X.X) * half.X
            + Mathf.Abs(basis.Y.X) * half.Y
            + Mathf.Abs(basis.Z.X) * half.Z
            + margin;
        var extentZ = Mathf.Abs(basis.X.Z) * half.X
            + Mathf.Abs(basis.Y.Z) * half.Y
            + Mathf.Abs(basis.Z.Z) * half.Z
            + margin;
        return new Rect2(
            new Vector2(center.X - extentX, center.Z - extentZ),
            new Vector2(extentX * 2.0f, extentZ * 2.0f));
    }

    private static void TideglassPropPieceWorld(
        DemolitionArenaProp prop,
        DemolitionArenaPropCollisionBox piece,
        out Vector3 center,
        out Basis basis,
        out Vector3 half)
    {
        var propBasis = new Basis(Vector3.Up, prop.Yaw);
        center = prop.Position + propBasis * (piece.Offset * prop.Scale);
        basis = propBasis * new Basis(Quaternion.FromEuler(piece.Rotation));
        half = piece.Size * prop.Scale * 0.5f;
    }

    private static bool TideglassPropCollisionMatchesDefinition(
        Node3D root,
        DemolitionArenaProp prop,
        out int authoredTriangles,
        out string failure)
    {
        authoredTriangles = 0;
        failure = "none";
        var body = root.GetNodeOrNull<StaticBody3D>(prop.Name);
        var model = body?.GetNodeOrNull<Node3D>("Model");
        if (!IsInstanceValid(body) || !IsInstanceValid(model))
        {
            failure = "missing-body-or-model";
            return false;
        }

        var collisions = body!.GetChildren()
            .OfType<CollisionShape3D>()
            .OrderBy(collision => collision.Name.ToString(), StringComparer.Ordinal)
            .ToArray();
        if (!body.HasMeta("prop_collision_mode")
            || body.GetMeta("prop_collision_mode").AsString() != prop.CollisionMode.ToString())
        {
            failure = "mode-metadata";
            return false;
        }
        if (!body.HasMeta("analytical_collision_piece_count")
            || body.GetMeta("analytical_collision_piece_count").AsInt32() != prop.CollisionPieceCount)
        {
            failure = "piece-metadata";
            return false;
        }
        var expectedSupplementalPieces = prop.AuthoredSolidCollisionPieceCount;
        if (!body.HasMeta("supplemental_collision_piece_count")
            || body.GetMeta("supplemental_collision_piece_count").AsInt32()
                != expectedSupplementalPieces)
        {
            failure = "supplemental-metadata";
            return false;
        }

        if (prop.CollisionMode == DemolitionArenaPropCollisionMode.AuthoredConcave)
        {
            var authoredCollision = collisions.SingleOrDefault(collision =>
                collision.Name == "Collision");
            if (collisions.Length != 1 + expectedSupplementalPieces
                || authoredCollision?.Shape is not ConcavePolygonShape3D concave)
            {
                failure = "authored-shape";
                return false;
            }
            var collisionFaces = concave.GetFaces();
            authoredTriangles = collisionFaces.Length / 3;
            var sourceFaceCount = 0;
            var meshes = model!.FindChildren("*", "MeshInstance3D", true, false);
            using var meshesBacking = meshes.AsDisposable();
            foreach (var child in meshes)
            {
                if (child is MeshInstance3D mesh && mesh.Mesh is not null)
                {
                    sourceFaceCount += mesh.Mesh.GetFaces().Length;
                }
            }
            if (collisionFaces.Length < 3
                || collisionFaces.Length != sourceFaceCount
                || concave.BackfaceCollision != prop.AuthoredBackfaceCollision
                || !authoredCollision.Position.IsEqualApprox(Vector3.Zero)
                || !authoredCollision.Rotation.IsEqualApprox(Vector3.Zero))
            {
                failure = "authored-geometry";
                return false;
            }
            if (!body.HasMeta("authored_collision_triangle_count")
                || body.GetMeta("authored_collision_triangle_count").AsInt32() != authoredTriangles
                || !body.HasMeta("authored_collision_backface")
                || body.GetMeta("authored_collision_backface").AsBool()
                    != prop.AuthoredBackfaceCollision)
            {
                failure = "authored-metadata";
                return false;
            }
            for (var pieceIndex = 0; pieceIndex < expectedSupplementalPieces; pieceIndex++)
            {
                var piece = prop.CollisionPieceAt(pieceIndex);
                var collision = collisions.SingleOrDefault(candidate =>
                    candidate.Name == $"SolidCollision_{pieceIndex + 1:00}");
                if (collision?.Shape is not BoxShape3D box
                    || !box.Size.IsEqualApprox(piece.Size * prop.Scale)
                    || !collision.Position.IsEqualApprox(piece.Offset * prop.Scale)
                    || !collision.Rotation.IsEqualApprox(piece.Rotation))
                {
                    failure = $"supplemental-box-{pieceIndex + 1}";
                    return false;
                }
            }
            return true;
        }

        if (collisions.Length != prop.CollisionPieceCount)
        {
            failure = $"box-count-{collisions.Length}";
            return false;
        }
        for (var pieceIndex = 0; pieceIndex < prop.CollisionPieceCount; pieceIndex++)
        {
            var piece = prop.CollisionPieceAt(pieceIndex);
            var collision = collisions[pieceIndex];
            var expectedName = prop.CollisionPieceCount == 1
                ? "Collision"
                : $"Collision_{pieceIndex + 1:00}";
            if (collision.Name != expectedName
                || collision.Shape is not BoxShape3D box
                || !box.Size.IsEqualApprox(piece.Size * prop.Scale)
                || !collision.Position.IsEqualApprox(piece.Offset * prop.Scale)
                || !collision.Rotation.IsEqualApprox(piece.Rotation))
            {
                failure = $"box-{pieceIndex + 1}";
                return false;
            }
        }

        if (prop.CollisionMode == DemolitionArenaPropCollisionMode.BoundsBox
            && (!HarborPropCollisionCoversModel(root, prop)
                || !TideglassPropCollisionTightlyFitsModel(root, prop)))
        {
            failure = "bounds-fit";
            return false;
        }
        return true;
    }

    private static bool TideglassCollisionProfilesReady(
        DemolitionArenaLayout layout,
        int authoredTriangles,
        out string failures)
    {
        var failed = new List<string>();
        var expectedBounds = new HashSet<string>(StringComparer.Ordinal)
        {
            "MidCoverDumpster",
            "MidCoverMachine",
            "MidCoverCivicPlanter",
            "MidCoverGenerator",
            "DefenderCourtyardGenerator",
            "ConstructionLaneGenerator",
            "EastSiteGenerator",
            "SouthPerimeterGenerator",
            "MidCoverConcreteBarrier",
            "ConstructionSupplyCrate"
        };
        var expectedCompound = new HashSet<string>(StringComparer.Ordinal)
        {
            "NorthBrickOffices",
            "NorthCustomsHouse",
            "SouthGatehouse",
            "EastInspectionOffice",
            "SightBlockEastApproachOffices",
            "DefenderServiceBlock",
            "SouthwestWatchHouse",
            "NorthFoundryTenement",
            "SouthGlassworksOffice",
            "WestFoundryInspectionAnnex",
            "NorthFreightOffice",
            "WestGateOffice"
        };
        var expectedAuthored = new HashSet<string>(StringComparer.Ordinal)
        {
            "SightBlockConstructionSiteOffice",
            "SightBlockReactorCargoContainers",
            "ConstructionTruck",
            "MidCoverConstructionSupplies",
            "MidCoverHopper",
            "MidCoverRoadBarrier",
            "ReactorPipeManifold",
            "CrossingTrafficLight"
        };
        var actualBounds = layout.Props
            .Where(prop => prop.CollisionMode == DemolitionArenaPropCollisionMode.BoundsBox)
            .Select(prop => prop.Name)
            .ToHashSet(StringComparer.Ordinal);
        var actualCompound = layout.Props
            .Where(prop => prop.CollisionMode == DemolitionArenaPropCollisionMode.CompoundBoxes)
            .Select(prop => prop.Name)
            .ToHashSet(StringComparer.Ordinal);
        var actualAuthored = layout.Props
            .Where(prop => prop.CollisionMode == DemolitionArenaPropCollisionMode.AuthoredConcave)
            .Select(prop => prop.Name)
            .ToHashSet(StringComparer.Ordinal);
        if (!actualBounds.SetEquals(expectedBounds))
        {
            failed.Add($"bounds={string.Join(',', actualBounds.OrderBy(name => name))}");
        }
        if (!actualCompound.SetEquals(expectedCompound)
            || layout.Props.Any(prop => prop.CollisionMode == DemolitionArenaPropCollisionMode.CompoundBoxes
                && prop.CollisionPieceCount < 2))
        {
            failed.Add($"compound={string.Join(',', actualCompound.OrderBy(name => name))}");
        }
        if (!actualAuthored.SetEquals(expectedAuthored)
            || layout.Props.Any(prop => prop.CollisionMode == DemolitionArenaPropCollisionMode.AuthoredConcave
                && !prop.AuthoredBackfaceCollision))
        {
            failed.Add($"authored={string.Join(',', actualAuthored.OrderBy(name => name))}");
        }
        if (layout.Props.Any(prop => TideglassDensityIsMajorBuilding(prop)
            && prop.CollisionMode == DemolitionArenaPropCollisionMode.BoundsBox))
        {
            failed.Add("major-bounds");
        }
        if (layout.Props.Any(prop => Enumerable.Range(0, prop.CollisionPieceCount)
            .Select(prop.CollisionPieceAt)
            .Any(piece => piece.Size.X <= 0.0f || piece.Size.Y <= 0.0f || piece.Size.Z <= 0.0f)))
        {
            failed.Add("invalid-piece");
        }
        if (authoredTriangles <= 0 || authoredTriangles > 5000)
        {
            failed.Add($"triangle-budget={authoredTriangles}");
        }
        failures = string.Join(';', failed);
        return failed.Count == 0;
    }

    private static bool TideglassCollisionSilhouettesReady(
        Node3D root,
        DemolitionArenaLayout layout,
        out string failures)
    {
        var failed = new List<string>();
        var sampleHeights = new[] { 0.4f, 1.0f, 1.6f };
        const float maximumCollisionOutset = 0.3f;
        // Shallow roofs, trim and railings may remain non-solid; full missing walls still fail this guard.
        const float maximumCollisionInset = 1.25f;
        foreach (var prop in layout.Props.Where(prop =>
                     prop.CollisionMode is DemolitionArenaPropCollisionMode.FootprintBox
                         or DemolitionArenaPropCollisionMode.CompoundBoxes))
        {
            var body = root.GetNodeOrNull<StaticBody3D>(prop.Name);
            var model = body?.GetNodeOrNull<Node3D>("Model");
            if (!IsInstanceValid(body) || !IsInstanceValid(model))
            {
                failed.Add($"{prop.Name}:missing");
                continue;
            }
            var collisionInset = prop.Name == "NorthFoundryTenement"
                ? 1.75f // The decorative fire-escape rail projects beyond the five walkable concrete steps.
                : maximumCollisionInset;
            foreach (var sampleHeight in sampleHeights)
            {
                if (!TideglassTryGetCollisionSliceBounds(
                        prop,
                        sampleHeight,
                        out var collisionMinimum,
                        out var collisionMaximum)
                    || !TideglassTryGetVisibleSliceBounds(
                        body!,
                        model!,
                        sampleHeight,
                        out var visibleMinimum,
                        out var visibleMaximum))
                {
                    failed.Add($"{prop.Name}@{sampleHeight:0.0}:missing-slice");
                    continue;
                }
                if (collisionMinimum.X < visibleMinimum.X - maximumCollisionOutset
                    || collisionMinimum.Y < visibleMinimum.Y - maximumCollisionOutset
                    || collisionMaximum.X > visibleMaximum.X + maximumCollisionOutset
                    || collisionMaximum.Y > visibleMaximum.Y + maximumCollisionOutset
                    || collisionMinimum.X > visibleMinimum.X + collisionInset
                    || collisionMinimum.Y > visibleMinimum.Y + collisionInset
                    || collisionMaximum.X < visibleMaximum.X - collisionInset
                    || collisionMaximum.Y < visibleMaximum.Y - collisionInset)
                {
                    failed.Add(
                        $"{prop.Name}@{sampleHeight:0.0}:"
                        + $"collision={collisionMinimum}>{collisionMaximum}:"
                        + $"visual={visibleMinimum}>{visibleMaximum}");
                }
            }
        }
        failures = string.Join('|', failed);
        return failed.Count == 0;
    }

    private static bool TideglassTryGetCollisionSliceBounds(
        DemolitionArenaProp prop,
        float sampleHeight,
        out Vector2 minimum,
        out Vector2 maximum)
    {
        minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        for (var pieceIndex = 0; pieceIndex < prop.CollisionPieceCount; pieceIndex++)
        {
            var piece = prop.CollisionPieceAt(pieceIndex);
            var center = piece.Offset * prop.Scale;
            var basis = new Basis(Quaternion.FromEuler(piece.Rotation));
            var half = piece.Size * prop.Scale * 0.5f;
            var extentX = Mathf.Abs(basis.X.X) * half.X
                + Mathf.Abs(basis.Y.X) * half.Y
                + Mathf.Abs(basis.Z.X) * half.Z;
            var extentY = Mathf.Abs(basis.X.Y) * half.X
                + Mathf.Abs(basis.Y.Y) * half.Y
                + Mathf.Abs(basis.Z.Y) * half.Z;
            var extentZ = Mathf.Abs(basis.X.Z) * half.X
                + Mathf.Abs(basis.Y.Z) * half.Y
                + Mathf.Abs(basis.Z.Z) * half.Z;
            if (sampleHeight < center.Y - extentY - 0.01f
                || sampleHeight > center.Y + extentY + 0.01f)
            {
                continue;
            }
            minimum = new Vector2(
                Mathf.Min(minimum.X, center.X - extentX),
                Mathf.Min(minimum.Y, center.Z - extentZ));
            maximum = new Vector2(
                Mathf.Max(maximum.X, center.X + extentX),
                Mathf.Max(maximum.Y, center.Z + extentZ));
        }
        return !float.IsInfinity(minimum.X);
    }

    private static bool TideglassTryGetVisibleSliceBounds(
        Node3D body,
        Node3D model,
        float sampleHeight,
        out Vector2 minimum,
        out Vector2 maximum)
    {
        minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        var meshes = model.FindChildren("*", "MeshInstance3D", true, false);
        using var meshesBacking = meshes.AsDisposable();
        foreach (var child in meshes)
        {
            if (child is not MeshInstance3D mesh
                || mesh.Mesh is null
                || !mesh.Visible
                || !mesh.IsVisibleInTree())
            {
                continue;
            }
            var meshToBody = body.GlobalTransform.AffineInverse() * mesh.GlobalTransform;
            var faces = mesh.Mesh.GetFaces();
            for (var faceIndex = 0; faceIndex + 2 < faces.Length; faceIndex += 3)
            {
                var a = meshToBody * faces[faceIndex];
                var b = meshToBody * faces[faceIndex + 1];
                var c = meshToBody * faces[faceIndex + 2];
                TideglassAccumulateSliceEdge(
                    a,
                    b,
                    sampleHeight,
                    ref minimum,
                    ref maximum);
                TideglassAccumulateSliceEdge(
                    b,
                    c,
                    sampleHeight,
                    ref minimum,
                    ref maximum);
                TideglassAccumulateSliceEdge(
                    c,
                    a,
                    sampleHeight,
                    ref minimum,
                    ref maximum);
            }
        }
        return !float.IsInfinity(minimum.X);
    }

    private static void TideglassAccumulateSliceEdge(
        Vector3 from,
        Vector3 to,
        float sampleHeight,
        ref Vector2 minimum,
        ref Vector2 maximum)
    {
        const float tolerance = 0.001f;
        var fromOffset = from.Y - sampleHeight;
        var toOffset = to.Y - sampleHeight;
        if (Mathf.Abs(fromOffset) <= tolerance)
        {
            TideglassAccumulateSlicePoint(from, ref minimum, ref maximum);
        }
        if (Mathf.Abs(toOffset) <= tolerance)
        {
            TideglassAccumulateSlicePoint(to, ref minimum, ref maximum);
        }
        if (fromOffset * toOffset >= 0.0f || Mathf.Abs(to.Y - from.Y) <= tolerance)
        {
            return;
        }
        var weight = (sampleHeight - from.Y) / (to.Y - from.Y);
        TideglassAccumulateSlicePoint(from.Lerp(to, weight), ref minimum, ref maximum);
    }

    private static void TideglassAccumulateSlicePoint(
        Vector3 point,
        ref Vector2 minimum,
        ref Vector2 maximum)
    {
        minimum = new Vector2(
            Mathf.Min(minimum.X, point.X),
            Mathf.Min(minimum.Y, point.Z));
        maximum = new Vector2(
            Mathf.Max(maximum.X, point.X),
            Mathf.Max(maximum.Y, point.Z));
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
            || (foundation.Center - layout.Origin).DistanceTo(new Vector3(-55.0f, 0.06f, 22.0f)) > 0.01f
            || !foundation.Size.IsEqualApprox(new Vector3(13.9f, 0.12f, 16.6f)))
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
