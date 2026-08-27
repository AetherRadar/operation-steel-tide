using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private async void ValidateIndustrialInteriors()
    {
        await WaitFrames(8);
        for (var frame = 0; frame < 3; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }

        var build = _industrialInteriors;
        if (build is null)
        {
            GD.Print("INDUSTRIAL_INTERIORS_CHECK valid=False build=false");
            GD.Print("INDUSTRIAL_INTERIORS_PASS valid=False");
            GetTree().Quit(2);
            return;
        }

        var roomCountReady = build.Rooms.Count == 23;
        var industrialDoorCountReady = build.Doors.Count == 63;
        var totalDoorCountReady = _refineryDoors.Count == 74;
        var sceneAssetsReady = FreightIndustrialBuildingCatalog.Placements
            .Select(placement => FreightIndustrialBuildingCatalog.ScenePath(placement.ModelId))
            .Distinct()
            .Count(path => ResourceLoader.Exists(path)) == 13;
        var styleCounts = build.Doors
            .GroupBy(door => door.MotionStyle)
            .ToDictionary(group => group.Key, group => group.Count());
        var styleReady = styleCounts.GetValueOrDefault(BuildingDoorMotionStyle.Hinged) > 0
            && styleCounts.GetValueOrDefault(BuildingDoorMotionStyle.Overhead) > 0
            && styleCounts.Values.Sum() == 63;
        var authoredReady = build.AuthoredBuildingCount == 23
            && build.LandmarkCount == 4
            && build.PalettedBuildingCount == 23
            && build.CollisionShapeCount >= 46
            && build.AuthoredInteriorModelCount == 46
            && build.Doors.All(door => door.UsesAuthoredVisual && door.HasBoxCollision);
        var anchorsReady = build.Rooms.All(room => room.LocalBounds.HasPoint(
            new Vector2(room.ContentLocalPoint.X, room.ContentLocalPoint.Z)));

        foreach (var door in build.Doors)
        {
            door.SetOpenImmediate(false);
        }
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        var closedDoorBlocks = build.Doors.All(door => PhysicsRaycast.HasHit(
            GetWorld3D(),
            door.OutsideProbe,
            door.InsideProbe,
            1));
        foreach (var door in build.Doors)
        {
            door.SetOpenImmediate(true);
        }
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        var openDoorClears = build.Doors.All(door => !PhysicsRaycast.HasHit(
            GetWorld3D(),
            door.OutsideProbe,
            door.InsideProbe,
            1));
        foreach (var door in build.Doors)
        {
            door.SetOpenImmediate(false);
        }
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        var wallSamples = 0;
        var wallBlocks = 0;
        var wallLeaks = new List<string>();
        var leakingRoomIndices = new HashSet<int>();
        var floorBlocks = 0;
        var ceilingBlocks = 0;
        foreach (var room in build.Rooms)
        {
            var roomRays = IndustrialRoomWallRays(room);
            for (var rayIndex = 0; rayIndex < roomRays.Length; rayIndex++)
            {
                var ray = roomRays[rayIndex];
                wallSamples++;
                if (PhysicsRaycast.TryHit(
                        GetWorld3D(),
                        ray.From,
                        ray.To,
                        1,
                        out var wallHit)
                    && IsIndustrialRoomCollider(room, wallHit.Collider))
                {
                    wallBlocks++;
                }
                else
                {
                    leakingRoomIndices.Add(room.Index);
                    wallLeaks.Add(
                        $"{room.Index + 1}:{room.ModelId}:{rayIndex}:{(wallHit.Collider as Node)?.Name}");
                }
            }
            var center = room.ContentWorldPoint;
            if (PhysicsRaycast.HasHit(
                    GetWorld3D(),
                    center + Vector3.Up * 1.8f,
                    center + Vector3.Down * 0.6f,
                    1))
            {
                floorBlocks++;
            }
            if (PhysicsRaycast.HasHit(
                    GetWorld3D(),
                    center + Vector3.Up * 1.2f,
                    center + Vector3.Up * 28.0f,
                    1))
            {
                ceilingBlocks++;
            }
        }
        var ballisticReady = wallSamples == build.Rooms.Count * 12
            && wallBlocks == wallSamples
            && floorBlocks == build.Rooms.Count
            && ceilingBlocks == build.Rooms.Count;
        var wallLeakSummary = wallLeaks.Count == 0 ? "none" : string.Join(',', wallLeaks);
        var relocationSuggestions = ballisticReady
            ? "none"
            : string.Join(';', leakingRoomIndices
                .OrderBy(index => index)
                .Select(index =>
                {
                    var room = build.Rooms[index];
                    var suggestion = FindIndustrialRelocationSuggestion(room);
                    return suggestion is Vector3 point
                        ? $"{index + 1}:{point.X:0},{point.Z:0}"
                        : $"{index + 1}:none";
                }));

        var repeatedPlans = FreightIndustrialRoomContentPlanner.Plan(
            _industrialInteriorSeed,
            build.Rooms);
        var deterministicPlans = _industrialRoomContentPlans.Count == repeatedPlans.Count
            && _industrialRoomContentPlans.Zip(repeatedPlans).All(pair =>
                pair.First.Room.Index == pair.Second.Room.Index
                && pair.First.Kind == pair.Second.Kind
                && pair.First.GuardCount == pair.Second.GuardCount
                && pair.First.Roll == pair.Second.Roll);
        var cachePlanCount = _industrialRoomContentPlans.Count(plan =>
            plan.Kind == FreightIndustrialRoomContentKind.SupplyCache);
        var guardRoomCount = _industrialRoomContentPlans.Count(plan =>
            plan.Kind == FreightIndustrialRoomContentKind.RestingSoldiers);
        var emptyRoomCount = _industrialRoomContentPlans.Count(plan =>
            plan.Kind == FreightIndustrialRoomContentKind.Empty);
        var guardPlanCount = _industrialRoomContentPlans.Sum(plan => plan.GuardCount);
        var expectedEmptyRoomCount = build.Rooms.Count
            - FreightIndustrialRoomContentPlanner.SupplyCacheRoomCount
            - FreightIndustrialRoomContentPlanner.RestingSoldierRoomCount;
        var contentReady = deterministicPlans
            && cachePlanCount == FreightIndustrialRoomContentPlanner.SupplyCacheRoomCount
            && guardRoomCount == FreightIndustrialRoomContentPlanner.RestingSoldierRoomCount
            && emptyRoomCount == expectedEmptyRoomCount
            && _industrialInteriorCaches.Count == cachePlanCount
            && _industrialInteriorCaches.All(cache =>
                IsInstanceValid(cache)
                && cache.IsSearchable
                && cache.NeutralVisualReady
                && _lootSources.Contains(cache))
            && _industrialInteriorGuards.Count == guardPlanCount
            && _industrialInteriorGuards.All(guard =>
                IsInstanceValid(guard)
                && guard.SentryMode
                && !guard.IsDead);

        var firstRoom = build.Rooms[0];
        var firstDoorway = firstRoom.Doorways[0];
        firstDoorway.Door.SetOpenImmediate(false);
        var aiOpened = TryPrepareAiDoorTraversal(
            firstDoorway.OutsidePoint,
            firstRoom.ContentWorldPoint,
            out var aiWaiting)
            && aiWaiting
            && firstDoorway.Door.TargetOpen;
        firstDoorway.Door.SetOpenImmediate(true);
        var aiContinued = TryPrepareAiDoorTraversal(
            firstDoorway.OutsidePoint,
            firstRoom.ContentWorldPoint,
            out var aiStillWaiting)
            && !aiStillWaiting;
        foreach (var door in build.Doors)
        {
            door.SetOpenImmediate(false);
        }

        var valid = roomCountReady
            && industrialDoorCountReady
            && totalDoorCountReady
            && sceneAssetsReady
            && styleReady
            && authoredReady
            && anchorsReady
            && closedDoorBlocks
            && openDoorClears
            && ballisticReady
            && contentReady
            && aiOpened
            && aiContinued;
        GD.Print($"INDUSTRIAL_INTERIORS_CHECK valid={valid} rooms={build.Rooms.Count}/23 doors={build.Doors.Count}/63 total_doors={_refineryDoors.Count}/74 hinged={styleCounts.GetValueOrDefault(BuildingDoorMotionStyle.Hinged)} overhead={styleCounts.GetValueOrDefault(BuildingDoorMotionStyle.Overhead)} scenes={build.ScenePaths.Count}/13 authored={authoredReady} anchors={anchorsReady} closed_block={closedDoorBlocks} open_clear={openDoorClears} walls={wallBlocks}/{wallSamples} leaks={wallLeakSummary} suggestions={relocationSuggestions} floors={floorBlocks}/{build.Rooms.Count} ceilings={ceilingBlocks}/{build.Rooms.Count} cache_rooms={cachePlanCount}/{FreightIndustrialRoomContentPlanner.SupplyCacheRoomCount} guard_rooms={guardRoomCount}/{FreightIndustrialRoomContentPlanner.RestingSoldierRoomCount} empty_rooms={emptyRoomCount}/{expectedEmptyRoomCount} guards={_industrialInteriorGuards.Count}/{guardPlanCount} deterministic={deterministicPlans} ai_opened={aiOpened} ai_continued={aiContinued}");
        GD.Print($"INDUSTRIAL_INTERIORS_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }

    private static (Vector3 From, Vector3 To)[] IndustrialRoomWallRays(
        FreightIndustrialRoom room)
    {
        var rays = new (Vector3 From, Vector3 To)[12];
        var localOrigin = new Vector3(
            room.ContentLocalPoint.X,
            1.20f,
            room.ContentLocalPoint.Z);
        for (var index = 0; index < rays.Length; index++)
        {
            var angle = Mathf.Tau * index / rays.Length;
            var direction = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            rays[index] = (
                room.Root.ToGlobal(localOrigin),
                room.Root.ToGlobal(localOrigin + direction * 32.0f));
        }
        return rays;
    }

    private static bool IsIndustrialRoomCollider(
        FreightIndustrialRoom room,
        GodotObject? collider)
    {
        var node = collider as Node;
        while (node is not null)
        {
            if (ReferenceEquals(node, room.Root))
            {
                return true;
            }
            node = node.GetParent();
        }
        return false;
    }

    private Vector3? FindIndustrialRelocationSuggestion(FreightIndustrialRoom room)
    {
        var origin = room.Root.GlobalPosition;
        var offsets = new List<Vector2>();
        for (var z = -72; z <= 72; z += 6)
        {
            for (var x = -72; x <= 72; x += 6)
            {
                if (x * x + z * z >= 36)
                {
                    offsets.Add(new Vector2(x, z));
                }
            }
        }
        foreach (var offset in offsets.OrderBy(value => value.LengthSquared()))
        {
            var candidate = new Vector3(origin.X + offset.X, 0.02f, origin.Z + offset.Y);
            if (candidate.X < -158.0f || candidate.X > 158.0f
                || candidate.Z < -210.0f || candidate.Z > 88.0f)
            {
                continue;
            }
            if (IndustrialPlacementVolumeIsClear(room, candidate))
            {
                return candidate;
            }
        }
        return null;
    }

    private bool IndustrialPlacementVolumeIsClear(
        FreightIndustrialRoom room,
        Vector3 candidateRootPosition)
    {
        var bounds = room.LocalBounds;
        using var shape = new BoxShape3D
        {
            Size = new Vector3(bounds.Size.X + 1.0f, 2.25f, bounds.Size.Y + 1.0f)
        };
        var yaw = room.Root.GlobalRotation.Y;
        var center = bounds.GetCenter();
        var localCenter = new Vector3(center.X, 1.42f, center.Y);
        var worldCenter = candidateRootPosition + localCenter.Rotated(Vector3.Up, yaw);
        using var query = new PhysicsShapeQueryParameters3D
        {
            Shape = shape,
            Transform = new Transform3D(new Basis(Vector3.Up, yaw), worldCenter),
            CollisionMask = 1,
            CollideWithAreas = false,
            CollideWithBodies = true,
            Margin = 0.02f
        };
        var hits = GetWorld3D().DirectSpaceState.IntersectShape(query, 48);
        using var hitsBacking = hits.AsDisposable();
        for (var index = 0; index < hits.Count; index++)
        {
            using var hit = hits[index];
            using var colliderValue = hit[GodotPhysicsResultKeys.Collider];
            if (colliderValue.AsGodotObject() is StaticBody3D)
            {
                return false;
            }
        }
        return true;
    }

    private async void CaptureIndustrialInteriors()
    {
        if (_industrialInteriors is null)
        {
            GD.Print("INDUSTRIAL_INTERIORS_CAPTURE valid=False reason=missing_build");
            GetTree().Quit(2);
            return;
        }
        foreach (var enemy in _enemies)
        {
            enemy.ProcessMode = ProcessModeEnum.Disabled;
            enemy.Visible = false;
        }
        foreach (var mate in _squadMates)
        {
            mate.ProcessMode = ProcessModeEnum.Disabled;
            mate.Visible = false;
        }
        foreach (var civilian in _civilians)
        {
            civilian.ProcessMode = ProcessModeEnum.Disabled;
            civilian.Visible = false;
        }
        _player.ProcessMode = ProcessModeEnum.Disabled;
        _player.Visible = false;
        _hud.Visible = false;

        var captureLight = new DirectionalLight3D
        {
            Name = "IndustrialInteriorCaptureLight",
            RotationDegrees = new Vector3(-54, -32, 0),
            LightEnergy = 0.75f,
            ShadowEnabled = true
        };
        AddChild(captureLight);
        var camera = new Camera3D
        {
            Name = "IndustrialInteriorCaptureCamera",
            Fov = 62.0f,
            Far = 420.0f
        };
        AddChild(camera);
        camera.MakeCurrent();

        var exteriorRoom = _industrialInteriors.Rooms.First(room =>
            room.ModelId == "building-a");
        var captureDoor = exteriorRoom.Doorways.First(doorway =>
            doorway.Door.MotionStyle == BuildingDoorMotionStyle.Hinged);
        foreach (var doorway in exteriorRoom.Doorways)
        {
            doorway.Door.SetOpenImmediate(false);
        }
        var outward = (captureDoor.OutsidePoint - captureDoor.InsidePoint).Normalized();
        var doorwayCenter = exteriorRoom.Doorways
            .Select(doorway => doorway.Door.InteractionPoint)
            .Aggregate(Vector3.Zero, (sum, point) => sum + point)
            / exteriorRoom.Doorways.Count;
        camera.Fov = 64.0f;
        camera.GlobalPosition = doorwayCenter + outward * 11.0f + Vector3.Up * 2.1f;
        camera.LookAt(doorwayCenter + Vector3.Up * 1.5f, Vector3.Up);
        await WaitFrames(14);
        SaveViewportImage("res://industrial_door_closed_validation.png");
        foreach (var doorway in exteriorRoom.Doorways)
        {
            doorway.Door.SetOpenImmediate(true);
        }
        await WaitFrames(8);
        SaveViewportImage("res://industrial_door_open_validation.png");

        var cachePlan = _industrialRoomContentPlans
            .Where(plan => plan.Kind == FreightIndustrialRoomContentKind.SupplyCache)
            .OrderByDescending(plan =>
                plan.Room.LocalBounds.Size.X * plan.Room.LocalBounds.Size.Y)
            .ThenBy(plan => plan.Room.Index)
            .First();
        var cache = _industrialInteriorCaches.First(item =>
            item.Name.ToString() == $"IndustrialSupplyCache_{cachePlan.Room.Index + 1:00}");
        foreach (var doorway in cachePlan.Room.Doorways)
        {
            doorway.Door.SetOpenImmediate(true);
        }
        cache.OnSearched();
        camera.Fov = 58.0f;
        var cacheTarget = cache.GlobalPosition + Vector3.Up * 0.55f;
        var cacheDoorway = cachePlan.Room.Doorways
            .OrderBy(doorway => doorway.InsidePoint.DistanceSquaredTo(
                cachePlan.Room.ContentWorldPoint))
            .First();
        var cacheDoorOutward = (cacheDoorway.OutsidePoint - cacheDoorway.InsidePoint)
            .Normalized();
        camera.GlobalPosition = FindIndustrialCaptureCamera(
            cachePlan.Room,
            cacheTarget,
            cache,
            cacheDoorway.OutsidePoint + cacheDoorOutward * 1.8f + Vector3.Up * 1.42f);
        camera.LookAt(cacheTarget, Vector3.Up);
        await WaitFrames(18);
        SaveViewportImage("res://industrial_cache_room_validation.png");

        var guardPlan = _industrialRoomContentPlans
            .Where(plan => plan.Kind == FreightIndustrialRoomContentKind.RestingSoldiers)
            .OrderByDescending(plan =>
                plan.Room.LocalBounds.Size.X * plan.Room.LocalBounds.Size.Y)
            .ThenBy(plan => plan.Room.Index)
            .First();
        var captureGuards = _industrialInteriorGuards
            .Where(guard => guard.Name.ToString().StartsWith(
                $"INDUSTRIAL_REST_GUARD_{guardPlan.Room.Index + 1:00}_",
                StringComparison.Ordinal))
            .ToArray();
        foreach (var guard in captureGuards)
        {
            guard.Visible = true;
        }
        foreach (var doorway in guardPlan.Room.Doorways)
        {
            doorway.Door.SetOpenImmediate(true);
        }
        camera.Fov = 58.0f;
        var guardTarget = guardPlan.Room.GuardWorldPointA + Vector3.Up * 0.95f;
        var guardDoorway = guardPlan.Room.Doorways
            .OrderBy(doorway => doorway.InsidePoint.DistanceSquaredTo(guardTarget))
            .First();
        var guardDoorOutward = (guardDoorway.OutsidePoint - guardDoorway.InsidePoint)
            .Normalized();
        camera.GlobalPosition = FindIndustrialCaptureCamera(
            guardPlan.Room,
            guardTarget,
            captureGuards.FirstOrDefault(),
            guardDoorway.InsidePoint + guardDoorOutward * 2.95f + Vector3.Up * 1.42f);
        camera.LookAt(guardTarget, Vector3.Up);
        await WaitFrames(16);
        SaveViewportImage("res://industrial_guard_room_validation.png");
        GD.Print($"INDUSTRIAL_INTERIORS_CAPTURE valid=True rooms={_industrialInteriors.Rooms.Count} doors={_industrialInteriors.Doors.Count} cache={cache.Name} guards={captureGuards.Length} paths=industrial_door_closed_validation.png,industrial_door_open_validation.png,industrial_cache_room_validation.png,industrial_guard_room_validation.png");
        GetTree().Quit();
    }

    private Vector3 FindIndustrialCaptureCamera(
        FreightIndustrialRoom room,
        Vector3 targetWorld,
        Node? target,
        Vector3 fallback)
    {
        var targetLocal = room.Root.ToLocal(targetWorld);
        var targetGround = new Vector2(targetLocal.X, targetLocal.Z);
        var safeBounds = room.LocalBounds.Grow(-0.72f);
        foreach (var radius in new[] { 4.2f, 3.5f, 2.8f, 2.2f })
        {
            for (var step = 0; step < 16; step++)
            {
                var angle = Mathf.Tau * step / 16.0f;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var candidateGround = targetGround + direction * radius;
                if (!safeBounds.HasPoint(candidateGround))
                {
                    continue;
                }

                var candidate = room.Root.ToGlobal(new Vector3(
                    candidateGround.X,
                    1.52f,
                    candidateGround.Y));
                if (!PhysicsRaycast.TryHit(
                        GetWorld3D(),
                        candidate,
                        targetWorld,
                        1,
                        out var hit)
                    || ColliderBelongsTo(target, hit.Collider))
                {
                    return candidate;
                }
            }
        }
        return fallback;
    }

    private static bool ColliderBelongsTo(Node? target, GodotObject? collider)
    {
        if (target is null || collider is not Node node)
        {
            return false;
        }
        while (node is not null)
        {
            if (ReferenceEquals(node, target))
            {
                return true;
            }
            node = node.GetParent();
        }
        return false;
    }
}
