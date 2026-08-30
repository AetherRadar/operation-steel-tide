using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private async void ValidateJianghaiInteriors()
    {
        await WaitFrames(8);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

        var build = _jianghaiInteriors;
        if (build is null || _jianghaiOldCityScene is not { } authoredScene)
        {
            GD.Print("JIANGHAI_INTERIORS_CHECK valid=False reason=no_build_result_or_scene");
            GD.Print("JIANGHAI_INTERIORS_PASS valid=False");
            QuitDiagnosticAfterSceneCleanup(2);
            return;
        }

        foreach (var resident in build.Residents)
        {
            if (IsInstanceValid(resident))
            {
                resident.ProcessMode = ProcessModeEnum.Disabled;
            }
        }

        var metadataReady = build.SourceCount == JianghaiInteriorPopulationService.ExpectedRoomCount
            && build.UnexpectedSourceCount == 0
            && build.Rooms.Select(room => room.SourceName).Distinct().Count()
                == JianghaiInteriorPopulationService.ExpectedRoomCount
            && build.Rooms.All(room =>
                JianghaiInteriorPopulationService.IsExpectedSourceName(room.SourceName)
                && room.Source.HasMeta("jianghai_enterable")
                && room.Source.GetMeta("jianghai_enterable").AsBool()
                && room.Source.GetMeta("jianghai_room_archetype", string.Empty).AsString()
                    == JianghaiInteriorPopulationService.ExpectedArchetypeFor(room.SourceName)
                && room.Source.GetMeta("jianghai_door_front", string.Empty).AsString()
                    == "local_positive_z_godot"
                && room.Width >= 3.8f
                && room.Depth >= 3.8f);
        var roomCountReady = build.Rooms.Count == JianghaiInteriorPopulationService.ExpectedRoomCount;
        var alignedFacadeRooms = build.Rooms.Count(JianghaiRoomFacadeAligned);
        var facadeAlignmentReady = alignedFacadeRooms
            == JianghaiInteriorPopulationService.ExpectedRoomCount;
        var doorCountReady = build.Doors.Count == JianghaiInteriorPopulationService.ExpectedDoorCount
            && build.Doors.All(door => _refineryDoors.Contains(door));
        var doorVisualReady = ResourceLoader.Exists(
                JianghaiInteriorPopulationService.LatticeDoorScenePath)
            && build.Doors.All(door =>
                door.GetMeta("jianghai_door_visual_scene", string.Empty).AsString()
                    == JianghaiInteriorPopulationService.LatticeDoorScenePath
                && door.UsesAuthoredVisual
                && door.HasBoxCollision
                && door.MotionStyle == BuildingDoorMotionStyle.Hinged
                && door.AuthoredVisualPanelCount == 1
                && door.HasValidAuthoredVisualPanelLayout
                && door.VisualShadowsDisabled);
        var furnitureCountReady = build.Rooms.All(room =>
                room.Furniture.Count == JianghaiInteriorPopulationService.FurniturePerRoom)
            && build.Rooms.Sum(room => room.Furniture.Count)
                == JianghaiInteriorPopulationService.ExpectedRoomCount
                    * JianghaiInteriorPopulationService.FurniturePerRoom;
        var furnitureAuthoredReady = build.AuthoredFurnitureMeshCount
                >= JianghaiInteriorPopulationService.ExpectedRoomCount
                    * JianghaiInteriorPopulationService.FurniturePerRoom
            && build.Rooms.SelectMany(room => room.Furniture).All(FurnitureAuthoredReady);
        var furnitureBatchReady = build.StaticFurniturePropCount
                == JianghaiInteriorPopulationService.ExpectedStaticFurnitureCount
            && build.StaticFurnitureInstanceCount
                >= JianghaiInteriorPopulationService.ExpectedStaticFurnitureCount
            && build.StaticFurnitureBatches.Count > 0
            && build.StaticFurnitureBatches.Count
                < build.StaticFurnitureInstanceCount
            && build.StaticFurnitureBatchValidationReady
            && build.StaticFurnitureMaximumPositionError <= 0.001f
            && build.StaticFurnitureMaximumBasisError <= 0.001f
            && build.StaticFurnitureMaximumBatchRadius
                <= JianghaiInteriorFurnitureBatcher.MaximumBatchRadius
            && build.StaticFurnitureMaximumVisibilityRange
                <= JianghaiInteriorFurnitureBatcher.MaximumVisibilityRange
            && build.StaticFurnitureBatches.All(batch =>
                batch.Multimesh is { } multiMesh
                && multiMesh.InstanceCount > 0
                && batch.CastShadow == GeometryInstance3D.ShadowCastingSetting.Off
                && batch.VisibilityRangeEnd
                    >= JianghaiInteriorPopulationService.InteriorVisibilityRange
                && batch.GetMeta("jianghai_static_furniture_instance_count", 0).AsInt32()
                    == multiMesh.InstanceCount);
        var visibilityReady = build.Rooms.SelectMany(room => room.Furniture)
            .SelectMany(VisibleFurnitureMeshes)
            .All(mesh =>
                mesh.VisibilityRangeEnd >= 40.0f
                && mesh.VisibilityRangeEnd <= 48.0f
                && mesh.CastShadow == GeometryInstance3D.ShadowCastingSetting.Off);
        var shellValidation = JianghaiInteriorShellValidator.Validate(authoredScene.Root);
        var searchableReady = build.Searchables.Count
                == JianghaiInteriorPopulationService.ExpectedSearchableCount
            && build.Searchables.All(searchable =>
                searchable.VisualReady
                && searchable.IsSearchable
                && searchable.Loot.Count >= 2
                && _lootSources.Contains(searchable)
                && _lootWorldPoints.Any(point =>
                    point.DistanceSquaredTo(searchable.GlobalPosition) <= 0.001f));
        var residentsReady = build.Residents.Count
                == JianghaiInteriorPopulationService.ExpectedResidentCount
            && build.Residents.All(resident =>
                IsInstanceValid(resident)
                && resident.IsInGroup("jianghai_enterable_resident")
                && resident.UsesAuthoredVisualForDiagnostics);
        var reducedSimulationReady = IsInstanceValid(_player)
            && ShouldUseReducedCivilianSimulation(_player.GlobalPosition + Vector3.Right * 73.0f)
            && !ShouldUseReducedCivilianSimulation(_player.GlobalPosition + Vector3.Right * 71.0f);
        var traversalReady = build.TraversalLinkCount
                == JianghaiInteriorPopulationService.ExpectedDoorCount
            && build.Doors.All(door => _squadTraversalLinks.Any(link =>
                link.Source == $"jianghai_interior_door:{door.DoorId}"
                && link.Kind == SquadTraversalKind.Walk
                && link.Bidirectional))
            && build.Rooms.All(room =>
                room.TraversalLocalPoint.DistanceTo(room.ResidentLocalPoint) >= 0.65f);

        var initiallyClosed = build.Doors.All(door => !door.IsOpen && !door.IsAnimating);
        var closedBlocks = build.Doors.All(door => PhysicsRaycast.HasHit(
            GetWorld3D(),
            door.OutsideProbe,
            door.InsideProbe,
            1));
        var closedShellBlocks = build.Rooms.All(room => PhysicsRaycast.HasHit(
            GetWorld3D(),
            room.Root.ToGlobal(new Vector3(0, 1.15f, 1.10f)),
            room.Root.ToGlobal(new Vector3(0, 1.15f, -1.10f)),
            1));
        var openingStarted = build.Doors.All(door =>
            door.TrySetOpen(true, bypassClearance: true));
        for (var frame = 0; frame < 140 && build.Doors.Any(door => door.IsAnimating); frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        var opened = build.Doors.All(door =>
            door.IsOpen
            && !door.IsAnimating
            && door.MotionAngleDegrees > 85.0f);
        var openClears = build.Doors.All(door => !PhysicsRaycast.HasHit(
            GetWorld3D(),
            door.OutsideProbe,
            door.InsideProbe,
            1));
        var openShellClears = build.Rooms.All(room => !PhysicsRaycast.HasHit(
            GetWorld3D(),
            room.Root.ToGlobal(new Vector3(0, 1.15f, 1.10f)),
            room.Root.ToGlobal(new Vector3(0, 1.15f, -1.10f)),
            1));

        var originalPlayerTransform = _player.GlobalTransform;
        var originalPlayerProcessMode = _player.ProcessMode;
        _player.ProcessMode = ProcessModeEnum.Disabled;
        var lootPriorityChecks = 0;
        var lootPriorityFailures = new List<string>();
        foreach (var room in build.Rooms)
        {
            foreach (var searchable in room.Searchables)
            {
                // Compact roller shops recess their side furniture behind a real angled
                // facade connector. Test from the reachable inner aisle, not through that wall.
                var searchableLocal = room.Root.ToLocal(searchable.GlobalPosition);
                var aislePoint = room.Root.ToGlobal(new Vector3(
                    0.0f,
                    searchableLocal.Y,
                    searchableLocal.Z));
                var approach = searchable.GlobalPosition.Lerp(aislePoint, 0.44f);
                approach.Y = room.Root.GlobalPosition.Y + 0.12f;
                _player.GlobalPosition = approach;
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                var selectedLoot = FindNearestInteractiveLoot(
                    _player.GlobalPosition,
                    2.85f,
                    out var lootDistance);
                var selectedDoor = FindNearestRefineryDoor(
                    _player.GlobalPosition,
                    3.25f,
                    out var doorDistance);
                if (ReferenceEquals(selectedLoot, searchable)
                    && selectedDoor is not null
                    && lootDistance + 0.10f < doorDistance)
                {
                    lootPriorityChecks++;
                }
                else
                {
                    lootPriorityFailures.Add(
                        $"{room.SourceName}:selected="
                        + $"{(selectedLoot is null ? "none" : selectedLoot.LootNode.Name)}:"
                        + $"loot={lootDistance:0.00}:door={selectedDoor?.DoorId ?? -1}:"
                        + $"{doorDistance:0.00}:los={HasClearPlayerLootInteractionLineOfSight(searchable)}:"
                        + $"blockers={DescribePlayerLootBlockers(searchable)}");
                }
            }
        }
        var residentPriorityChecks = 0;
        foreach (var resident in build.Residents)
        {
            var room = build.Rooms.MinBy(candidate =>
                candidate.Root.GlobalPosition.DistanceSquaredTo(resident.GlobalPosition));
            if (room is null)
            {
                continue;
            }
            var approach = resident.GlobalPosition + room.Root.GlobalBasis.Z.Normalized() * 0.85f;
            approach.Y = room.Root.GlobalPosition.Y + 0.12f;
            _player.GlobalPosition = approach;
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            var selectedCivilian = FindNearestAssistableCivilian(
                _player.GlobalPosition,
                2.85f,
                out var civilianDistance);
            var selectedDoor = FindNearestRefineryDoor(
                _player.GlobalPosition,
                3.25f,
                out var doorDistance);
            if (ReferenceEquals(selectedCivilian, resident)
                && selectedDoor is not null
                && civilianDistance + 0.10f < doorDistance)
            {
                residentPriorityChecks++;
            }
        }
        _player.GlobalTransform = originalPlayerTransform;
        _player.ProcessMode = originalPlayerProcessMode;
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        var interactionPriorityReady = lootPriorityChecks
                == JianghaiInteriorPopulationService.ExpectedSearchableCount
            && residentPriorityChecks == JianghaiInteriorPopulationService.ExpectedResidentCount;
        var aiRouteChecks = 0;
        var aiLinkChecks = 0;
        var aiOpenedChecks = 0;
        var aiContinuedChecks = 0;
        var routeMate = _squadMates.FirstOrDefault(mate =>
            IsInstanceValid(mate) && !mate.IsDowned && !mate.IsBodyBag);
        if (routeMate is not null && build.Rooms.Count > 0)
        {
            var originalMateTransform = routeMate.GlobalTransform;
            var originalMateProcessMode = routeMate.ProcessMode;
            var originalMateVelocity = routeMate.Velocity;
            routeMate.ProcessMode = ProcessModeEnum.Disabled;
            foreach (var room in build.Rooms)
            {
                var linkId = _squadTraversalLinks.FindIndex(link =>
                    link.Source == $"jianghai_interior_door:{room.Door.DoorId}");
                var destination = room.Root.ToGlobal(room.TraversalLocalPoint);
                routeMate.GlobalPosition = room.OutsidePoint;
                routeMate.Velocity = Vector3.Zero;
                room.Door.SetOpenImmediate(false);
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                var routeReady = TryPlanSquadLayeredRoute(
                    routeMate,
                    destination,
                    new SquadNavSearchBudget(1800, 12.0),
                    out var directives,
                    out _);
                if (routeReady)
                {
                    aiRouteChecks++;
                }
                if (routeReady
                    && linkId >= 0
                    && directives.Any(directive =>
                        directive.Required && directive.DirectedEdgeId / 2 == linkId))
                {
                    aiLinkChecks++;
                }
                if (TryPrepareAiDoorTraversal(
                        room.OutsidePoint,
                        destination,
                        out var waitingDuringMotion)
                    && waitingDuringMotion)
                {
                    aiOpenedChecks++;
                }
                for (var frame = 0; frame < 140 && room.Door.IsAnimating; frame++)
                {
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                }
                if (TryPrepareAiDoorTraversal(
                        room.OutsidePoint,
                        destination,
                        out var waitingAfterMotion)
                    && room.Door.IsOpen
                    && !waitingAfterMotion)
                {
                    aiContinuedChecks++;
                }
            }
            routeMate.GlobalTransform = originalMateTransform;
            routeMate.ProcessMode = originalMateProcessMode;
            routeMate.Velocity = originalMateVelocity;
        }
        var aiRouteReady = aiRouteChecks == JianghaiInteriorPopulationService.ExpectedRoomCount;
        var aiLinkSeen = aiLinkChecks == JianghaiInteriorPopulationService.ExpectedRoomCount;
        var aiOpened = aiOpenedChecks == JianghaiInteriorPopulationService.ExpectedRoomCount;
        var aiContinued = aiContinuedChecks == JianghaiInteriorPopulationService.ExpectedRoomCount;
        foreach (var door in build.Doors)
        {
            door.SetOpenImmediate(false);
        }

        var valid = metadataReady
            && roomCountReady
            && facadeAlignmentReady
            && doorCountReady
            && doorVisualReady
            && furnitureCountReady
            && furnitureAuthoredReady
            && furnitureBatchReady
            && visibilityReady
            && shellValidation.Valid
            && searchableReady
            && residentsReady
            && reducedSimulationReady
            && traversalReady
            && aiRouteReady
            && aiLinkSeen
            && aiOpened
            && aiContinued
            && initiallyClosed
            && closedBlocks
            && closedShellBlocks
            && openingStarted
            && opened
            && openClears
            && openShellClears
            && interactionPriorityReady;
        GD.Print(
            $"JIANGHAI_INTERIORS_CHECK valid={valid} "
            + $"sources={build.SourceCount}/{JianghaiInteriorPopulationService.ExpectedRoomCount} "
            + $"unexpected_sources={build.UnexpectedSourceCount} "
            + $"rooms={build.Rooms.Count}/{JianghaiInteriorPopulationService.ExpectedRoomCount} "
            + $"facades={alignedFacadeRooms}/{JianghaiInteriorPopulationService.ExpectedRoomCount} "
            + $"doors={build.Doors.Count}/{JianghaiInteriorPopulationService.ExpectedDoorCount} "
            + $"door_visual={doorVisualReady} furniture={build.Rooms.Sum(room => room.Furniture.Count)}/"
            + $"{JianghaiInteriorPopulationService.ExpectedRoomCount * JianghaiInteriorPopulationService.FurniturePerRoom} "
            + $"furniture_meshes={build.AuthoredFurnitureMeshCount} "
            + $"furniture_batches={build.StaticFurnitureBatches.Count}:"
            + $"{build.StaticFurnitureInstanceCount}:"
            + $"{build.StaticFurniturePropCount}/{JianghaiInteriorPopulationService.ExpectedStaticFurnitureCount}:"
            + $"{furnitureBatchReady}:errors={build.StaticFurnitureMaximumPositionError:0.000000}:"
            + $"{build.StaticFurnitureMaximumBasisError:0.000000}:"
            + $"radius={build.StaticFurnitureMaximumBatchRadius:0.00}/"
            + $"{JianghaiInteriorFurnitureBatcher.MaximumBatchRadius:0.00}:"
            + $"range={build.StaticFurnitureMaximumVisibilityRange:0.00}/"
            + $"{JianghaiInteriorFurnitureBatcher.MaximumVisibilityRange:0.00} "
            + $"visibility={visibilityReady} "
            + $"liners={shellValidation.ShellCount}/{JianghaiInteriorPopulationService.ExpectedRoomCount}:"
            + $"shared={shellValidation.SharedMeshCount}:batched={shellValidation.BatchedShellCount}:"
            + $"closed={shellValidation.ClosedDirectionCount}/"
            + $"{JianghaiInteriorPopulationService.ExpectedRoomCount * 5}:"
            + $"opaque={shellValidation.OpaqueSurfaceCount}:triangles={shellValidation.TriangleCount}:"
            + $"{shellValidation.Valid}:{shellValidation.Failure} "
            + $"loot={build.Searchables.Count}/{JianghaiInteriorPopulationService.ExpectedSearchableCount}:{searchableReady} "
            + $"residents={build.Residents.Count}/{JianghaiInteriorPopulationService.ExpectedResidentCount}:{residentsReady} "
            + $"reduced_sim={reducedSimulationReady} traversal={build.TraversalLinkCount}/"
            + $"{JianghaiInteriorPopulationService.ExpectedDoorCount}:{traversalReady}:"
            + $"route={aiRouteReady}:{aiLinkSeen}:door={aiOpened}:{aiContinued} "
            + $"route_checks={aiRouteChecks}/{JianghaiInteriorPopulationService.ExpectedRoomCount}:"
            + $"{aiLinkChecks}/{JianghaiInteriorPopulationService.ExpectedRoomCount} "
            + $"door_checks={aiOpenedChecks}/{JianghaiInteriorPopulationService.ExpectedRoomCount}:"
            + $"{aiContinuedChecks}/{JianghaiInteriorPopulationService.ExpectedRoomCount} "
            + $"closed={initiallyClosed}:{closedBlocks} opening={openingStarted} opened={opened} open_clear={openClears} "
            + $"shell={closedShellBlocks}:{openShellClears} "
            + $"priority={lootPriorityChecks}/{JianghaiInteriorPopulationService.ExpectedSearchableCount}:"
            + $"{residentPriorityChecks}/{JianghaiInteriorPopulationService.ExpectedResidentCount}:"
            + $"{(lootPriorityFailures.Count == 0 ? "none" : string.Join('|', lootPriorityFailures))}");
        GD.Print($"JIANGHAI_INTERIORS_PASS valid={valid}");
        QuitDiagnosticAfterSceneCleanup(valid ? 0 : 2);
    }

    private bool JianghaiRoomFacadeAligned(JianghaiInteriorRoom room)
    {
        if (!JianghaiGameplayCollisionContract.TryGetEnterableRoom(
                room.SourceName,
                out var contract)
            || Mathf.Abs(room.FrontInset - contract.FrontInset) > 0.011f
            || !InteriorMetaApproximately(
                room.Source,
                "jianghai_door_front_inset_m",
                contract.FrontInset)
            || !InteriorMetaApproximately(
                room.Source,
                "jianghai_collision_width_m",
                contract.CollisionWidth)
            || !InteriorMetaApproximately(
                room.Source,
                "jianghai_collision_depth_m",
                contract.CollisionDepth)
            || !InteriorMetaApproximately(
                room.Source,
                "jianghai_collision_height_m",
                contract.CollisionHeight)
            || !InteriorMetaApproximately(
                room.Source,
                "jianghai_collision_facade_width_m",
                contract.FacadeWidth)
            || !InteriorMetaApproximately(
                room.Source,
                "jianghai_collision_side_half_width_m",
                contract.SideHalfWidth)
            || !InteriorMetaApproximately(
                room.Source,
                "jianghai_collision_side_front_inset_m",
                contract.SideFrontInset)
            || !InteriorMetaApproximately(
                room.Source,
                "jianghai_collision_side_rear_inset_m",
                contract.SideRearInset)
            || _jianghaiGameplayCollision is not { } gameplay)
        {
            return false;
        }

        var bounds = room.Source.GetAabb();
        var visualFront = room.Source.GlobalTransform * new Vector3(
            bounds.GetCenter().X,
            bounds.Position.Y,
            bounds.End.Z);
        var outward = room.Source.GlobalBasis.Z;
        outward.Y = 0.0f;
        outward = outward.Normalized();
        var expectedDoor = visualFront - outward * contract.FrontInset;
        if (room.Root.GlobalPosition.DistanceTo(expectedDoor) > 0.035f
            || room.Root.GlobalBasis.Z.Normalized().Dot(outward) < 0.999f)
        {
            return false;
        }

        var shapes = gameplay.Body.FindChildren(
            "*",
            "CollisionShape3D",
            recursive: true,
            owned: false);
        using var shapesBacking = shapes.AsDisposable();
        foreach (var child in shapes)
        {
            if (child is not CollisionShape3D shape
                || shape.GetMeta("gameplay_source_node", string.Empty).AsString()
                    != room.SourceName
                || shape.GetMeta("gameplay_proxy_role", string.Empty).AsString()
                    != "front_lintel")
            {
                continue;
            }
            var doorwayCenter = shape.GetMeta(
                "gameplay_doorway_center",
                shape.GlobalPosition).AsVector3();
            var horizontalError = new Vector2(
                doorwayCenter.X - room.Root.GlobalPosition.X,
                doorwayCenter.Z - room.Root.GlobalPosition.Z).Length();
            return horizontalError <= 0.08f;
        }
        return false;
    }

    private static bool InteriorMetaApproximately(Node node, string key, float expected)
        => node.HasMeta(key)
            && Mathf.Abs(node.GetMeta(key).AsSingle() - expected) <= 0.011f;

}
