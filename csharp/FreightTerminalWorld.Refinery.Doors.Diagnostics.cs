using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private async void ValidateRefineryDoors()
    {
        await WaitFrames(6);
        foreach (var mate in _squadMates)
        {
            if (IsInstanceValid(mate))
            {
                mate.ProcessMode = ProcessModeEnum.Disabled;
            }
        }
        foreach (var enemy in _enemies)
        {
            if (IsInstanceValid(enemy))
            {
                enemy.ProcessMode = ProcessModeEnum.Disabled;
            }
        }
        var expectedDoorCount = (_oldTownLandmarks?.EntryCount ?? 0)
            + JianghaiInteriorPopulationService.ExpectedDoorCount
            + 1;
        var countReady = _refineryDoors.Count == expectedDoorCount
            && expectedDoorCount == 15;
        var idsReady = _refineryDoors.Select(door => door.DoorId).Distinct().Count() == expectedDoorCount
            && _refineryDoors.Select(door => door.DoorId).OrderBy(id => id)
                .SequenceEqual(Enumerable.Range(1, expectedDoorCount));
        var authoredReady = ResourceLoader.Exists(RefineryDoorScenePath)
            && ResourceLoader.Exists(JianghaiInteriorPopulationService.LatticeDoorScenePath)
            && _refineryDoors.All(door => door.UsesAuthoredVisual
                && door.HasRenderableAuthoredVisualGeometry
                && door.AuthoredRenderableGeometryCount > 0
                && door.HasBoxCollision);
        var styleReady = _refineryDoors.Count(door =>
                door.MotionStyle == BuildingDoorMotionStyle.Hinged) == expectedDoorCount - 1
            && _refineryDoors.Count(door =>
                door.MotionStyle == BuildingDoorMotionStyle.DoubleHinged) == 1;
        var doubleLeafReady = _clanHallDoubleGate is { } projectedGate
            && projectedGate.LeafCount == 2
            && projectedGate.LeafCollisionCount == 2
            && projectedGate.AuthoredVisualPanelCount == 2;
        var visibilityReady = ValidateRefineryDoorVisibilityQuality(
            out var visibilitySummary);
        var panelLayoutReady = countReady && _refineryDoors.All(door =>
            door.HasValidAuthoredVisualPanelLayout
            && door.AuthoredVisualPanelCount == (
                door.MotionStyle == BuildingDoorMotionStyle.DoubleHinged
                    ? 2
                    : RefineryDoorVisualPanelCount)
            && door.MaxAuthoredVisualAspectDistortion
                <= (door.MotionStyle == BuildingDoorMotionStyle.DoubleHinged
                    ? 1.60f
                    : door.IsInGroup("jianghai_enterable_door")
                        ? 1.30f
                        : RefineryDoorMaxAspectDistortion));
        var invalidPanelLayouts = string.Join(
            ',',
            _refineryDoors.Where(door =>
                    !door.HasValidAuthoredVisualPanelLayout)
                .Select(door => door.Name));
        var maxAspectDistortion = _refineryDoors.Count > 0
            ? _refineryDoors.Max(door => door.MaxAuthoredVisualAspectDistortion)
            : float.PositiveInfinity;
        var initiallyClosed = _refineryDoors.All(door => !door.IsOpen && !door.IsAnimating);
        var first = _refineryDoors.FirstOrDefault();
        if (first is null)
        {
            GD.Print("REFINERY_DOORS_CHECK valid=False reason=no_doors");
            GD.Print("REFINERY_DOORS_PASS valid=False");
            QuitDiagnosticAfterSceneCleanup(2);
            return;
        }

        var englishPromptReady = first.InteractionLabel("en") == "OPEN DOOR";
        var chinesePromptReady = first.InteractionLabel("zh") == "\u5f00\u95e8";
        var nearestReady = ReferenceEquals(
            FindNearestRefineryDoor(first.InteractionPoint, 0.5f),
            first);
        var closedBlocks = _refineryDoors.All(door => PhysicsRaycast.HasHit(
            GetWorld3D(),
            door.OutsideProbe,
            door.InsideProbe,
            1));
        var doubleClosedSamples = _clanHallDoubleGate is { } closedGate
            && ValidateDoorRaySamples(closedGate, expectBlocked: true, out var closedGateSamples)
            && closedGateSamples == 3;
        var collisionProbeEnemy = _enemies.FirstOrDefault(enemy =>
            IsInstanceValid(enemy) && enemy.IsInsideTree());
        var collisionProbeEnemyTransform = collisionProbeEnemy?.GlobalTransform ?? Transform3D.Identity;
        var enemyClosedDoorBlockCount = 0;
        var enemyWallBlockCount = 0;
        if (collisionProbeEnemy is not null)
        {
            // Disabled process mode removes a CharacterBody3D from its physics space,
            // so keep this probe registered while suppressing its gameplay updates.
            collisionProbeEnemy.ProcessMode = ProcessModeEnum.Inherit;
            collisionProbeEnemy.SetProcess(false);
            collisionProbeEnemy.SetPhysicsProcess(false);
            foreach (var door in _refineryDoors)
            {
                var enemyOutside = door.OutsideProbe;
                enemyOutside.Y = door.ThresholdPoint.Y;
                var enemyInside = door.InsideProbe;
                enemyInside.Y = door.ThresholdPoint.Y;
                collisionProbeEnemy.GlobalPosition = enemyOutside;
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                if (collisionProbeEnemy.WouldWorldMovementCollideForDiagnostics(
                        enemyInside - enemyOutside))
                {
                    enemyClosedDoorBlockCount++;
                }

                var traversal = enemyInside - enemyOutside;
                var tangent = new Vector3(traversal.Z, 0, -traversal.X).Normalized();
                foreach (var side in new[] { -1.0f, 1.0f })
                {
                    var facadeOffset = door.WidthForNavigation * 1.65f * side;
                    var wallOutside = enemyOutside + tangent * facadeOffset;
                    var wallInside = enemyInside + tangent * facadeOffset;
                    collisionProbeEnemy.GlobalPosition = wallOutside;
                    await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                    if (collisionProbeEnemy.WouldWorldMovementCollideForDiagnostics(
                            wallInside - wallOutside))
                    {
                        enemyWallBlockCount++;
                    }
                }
            }
        }
        var enemyClosedDoorBlocks = enemyClosedDoorBlockCount == _refineryDoors.Count;
        var expectedEnemyWallBlocks = _refineryDoors.Count * 2;
        var enemyWallsBlock = enemyWallBlockCount == expectedEnemyWallBlocks;

        var openingStarted = _refineryDoors.All(door =>
            door.TrySetOpen(true, bypassClearance: true));
        for (var frame = 0; frame < 120 && _refineryDoors.Any(door => door.IsAnimating); frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        var opened = _refineryDoors.All(door =>
            door.IsOpen
            && !door.IsAnimating
            && door.MotionAngleDegrees > 85.0f
            && door.CompletedMotionCount == 1);
        var doubleAnglesReady = _clanHallDoubleGate is { } openedGate
            && openedGate.LeftLeafAngleDegrees > 85.0f
            && openedGate.RightLeafAngleDegrees < -85.0f
            && Mathf.Abs(
                openedGate.LeftLeafAngleDegrees + openedGate.RightLeafAngleDegrees) < 0.5f;
        var openClears = _refineryDoors.All(door => !PhysicsRaycast.HasHit(
            GetWorld3D(),
            door.OutsideProbe,
            door.InsideProbe,
            1));
        var doubleOpenSamples = _clanHallDoubleGate is { } openGate
            && ValidateDoorRaySamples(openGate, expectBlocked: false, out var openGateSamples)
            && openGateSamples == 3;
        var enemyOpenDoorClearCount = 0;
        if (collisionProbeEnemy is not null)
        {
            foreach (var door in _refineryDoors)
            {
                var enemyOutside = door.OutsideProbe;
                enemyOutside.Y = door.ThresholdPoint.Y;
                var enemyInside = door.InsideProbe;
                enemyInside.Y = door.ThresholdPoint.Y;
                collisionProbeEnemy.GlobalPosition = enemyOutside;
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                if (!collisionProbeEnemy.WouldWorldMovementCollideForDiagnostics(
                        enemyInside - enemyOutside))
                {
                    enemyOpenDoorClearCount++;
                }
            }
            collisionProbeEnemy.GlobalTransform = collisionProbeEnemyTransform;
            collisionProbeEnemy.ProcessMode = ProcessModeEnum.Disabled;
        }
        var enemyOpenDoorClears = enemyOpenDoorClearCount == _refineryDoors.Count;
        var closePromptReady = _refineryDoors.All(door =>
            door.InteractionLabel("en") == "CLOSE DOOR");

        var playerPosition = _player.GlobalPosition;
        var occupiedDoor = _clanHallDoubleGate ?? first;
        var occupiedTraversal = occupiedDoor.InsideProbe - occupiedDoor.OutsideProbe;
        occupiedTraversal.Y = 0.0f;
        occupiedTraversal = occupiedTraversal.Normalized();
        var occupiedTangent = new Vector3(
            occupiedTraversal.Z,
            0.0f,
            -occupiedTraversal.X).Normalized();
        var occupiedCloseRejectCount = 0;
        foreach (var occupiedPoint in new[]
        {
            occupiedDoor.ThresholdPoint,
            occupiedDoor.ThresholdPoint
                + occupiedTangent * (occupiedDoor.WidthForNavigation * 0.28f)
                + occupiedTraversal * (occupiedDoor.WidthForNavigation * 0.24f),
            occupiedDoor.ThresholdPoint
                - occupiedTangent * (occupiedDoor.WidthForNavigation * 0.28f)
                + occupiedTraversal * (occupiedDoor.WidthForNavigation * 0.24f)
        })
        {
            _player.GlobalPosition = occupiedPoint;
            await WaitFrames(3);
            if (!occupiedDoor.TrySetOpen(false))
            {
                occupiedCloseRejectCount++;
            }
        }
        var occupiedCloseRejected = occupiedCloseRejectCount == 3;
        var outward = (occupiedDoor.OutsideProbe - occupiedDoor.InsideProbe).Normalized();
        _player.GlobalPosition = occupiedDoor.OutsideProbe + outward * 4.0f;
        await WaitFrames(3);
        var closingStarted = _refineryDoors.All(door =>
            door.TrySetOpen(false, bypassClearance: true));
        for (var frame = 0; frame < 120 && _refineryDoors.Any(door => door.IsAnimating); frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        await WaitFrames(3);
        var closedAgain = _refineryDoors.All(door =>
            !door.IsOpen
            && !door.IsAnimating
            && Mathf.Abs(door.MotionAngleDegrees) < 0.5f
            && door.CompletedMotionCount == 2);
        var closedAgainBlocks = _refineryDoors.All(door => PhysicsRaycast.HasHit(
            GetWorld3D(),
            door.OutsideProbe,
            door.InsideProbe,
            1));

        var aiDoor = _clanHallDoubleGate ?? first;
        var aiLinks = _squadTraversalLinks.Where(link =>
                link.Source == $"refinery_door:{aiDoor.DoorId}")
            .ToArray();
        var aiSupportedPoints = 0;
        var aiMaximumHeightStep = 0.0f;
        var aiRampSummary = $"link_count_{aiLinks.Length}";
        var aiRampReady = aiLinks.Length == 1
            && ValidateClanHallTraversalLink(
                aiLinks[0],
                out aiSupportedPoints,
                out aiMaximumHeightStep,
                out aiRampSummary);
        var aiLinkReady = aiRampReady
            && aiLinks[0].Kind == SquadTraversalKind.Walk
            && aiLinks[0].Bidirectional;
        var aiActorPosition = aiDoor.OutsideProbe;
        var aiWaypoint = aiDoor.InsideProbe;
        var aiOpened = TryPrepareAiDoorTraversal(
            aiActorPosition,
            aiWaypoint,
            out var aiWaitingDuringMotion)
            && aiWaitingDuringMotion;
        for (var frame = 0; frame < 120 && aiDoor.IsAnimating; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        var aiContinued = TryPrepareAiDoorTraversal(
            aiActorPosition,
            aiWaypoint,
            out var aiWaitingAfterMotion)
            && aiDoor.IsOpen
            && !aiWaitingAfterMotion;
        var authoritativeInitialMotionCount = aiDoor.CompletedMotionCount;
        var authoritativeTransitionStarted = aiDoor.TrySetOpen(
            false,
            bypassClearance: true);
        aiDoor.ApplyAuthoritativeOpenState(true);
        var authoritativeQueued = authoritativeTransitionStarted
            && aiDoor.IsAnimating
            && !aiDoor.TargetOpen
            && aiDoor.HasQueuedAuthoritativeState
            && aiDoor.QueuedAuthoritativeOpen;
        for (var frame = 0; frame < 180 && aiDoor.IsAnimating; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        var authoritativeConverged = authoritativeQueued
            && aiDoor.IsOpen
            && aiDoor.TargetOpen
            && !aiDoor.IsAnimating
            && !aiDoor.HasQueuedAuthoritativeState
            && aiDoor.CompletedMotionCount == authoritativeInitialMotionCount + 2
            && aiDoor.MotionAngleDegrees > 85.0f;
        var motionCountsReady = aiDoor.CompletedMotionCount == 5
            && _refineryDoors.Where(door => !ReferenceEquals(door, aiDoor))
                .All(door => door.CompletedMotionCount == 2);
        aiDoor.SetOpenImmediate(false);
        _player.GlobalPosition = playerPosition;

        var valid = countReady && idsReady && authoredReady && styleReady && doubleLeafReady
            && visibilityReady && panelLayoutReady
            && initiallyClosed
            && englishPromptReady && chinesePromptReady && nearestReady
            && closedBlocks && doubleClosedSamples && enemyClosedDoorBlocks && enemyWallsBlock
            && openingStarted && opened && doubleAnglesReady
            && openClears && doubleOpenSamples && enemyOpenDoorClears && closePromptReady
            && occupiedCloseRejected && closingStarted && closedAgain && closedAgainBlocks
            && aiLinkReady && aiOpened && aiContinued
            && authoritativeConverged && motionCountsReady;
        GD.Print($"REFINERY_DOORS_CHECK valid={valid} doors={_refineryDoors.Count}/{expectedDoorCount} ids={idsReady} authored={authoredReady}:geometry={string.Join(',', _refineryDoors.Select(door => door.AuthoredRenderableGeometryCount))} styles={styleReady}:hinged={_refineryDoors.Count(door => door.MotionStyle == BuildingDoorMotionStyle.Hinged)}:double={_refineryDoors.Count(door => door.MotionStyle == BuildingDoorMotionStyle.DoubleHinged)} gate_error={_clanHallDoubleGateError ?? "none"} leaves={_clanHallDoubleGate?.LeafCount ?? 0}:collisions={_clanHallDoubleGate?.LeafCollisionCount ?? 0} visibility={visibilityReady}:{visibilitySummary} panels={string.Join(',', _refineryDoors.Select(door => door.AuthoredVisualPanelCount))} panel_layout={panelLayoutReady}:invalid={invalidPanelLayouts}:{_clanHallDoubleGate?.DoubleHingedLayoutDiagnostic ?? "none"} aspect_distortion_max={maxAspectDistortion:0.000} closed_initial={initiallyClosed} prompt_en={englishPromptReady} prompt_zh={chinesePromptReady} nearest={nearestReady} closed_block={closedBlocks}:double_samples={doubleClosedSamples}:3 enemy_closed_block={enemyClosedDoorBlocks}:{enemyClosedDoorBlockCount}/{_refineryDoors.Count} enemy_wall_block={enemyWallsBlock}:{enemyWallBlockCount}/{expectedEnemyWallBlocks} opening={openingStarted} opened={opened} gate_angles={_clanHallDoubleGate?.LeftLeafAngleDegrees ?? 0.0f:0.0}/{_clanHallDoubleGate?.RightLeafAngleDegrees ?? 0.0f:0.0}:{doubleAnglesReady} open_clear={openClears}:double_samples={doubleOpenSamples}:3 enemy_open_clear={enemyOpenDoorClears}:{enemyOpenDoorClearCount}/{_refineryDoors.Count} close_prompt={closePromptReady} occupied_rejected={occupiedCloseRejected}:{occupiedCloseRejectCount}/3 closing={closingStarted} closed_again={closedAgain} closed_block_again={closedAgainBlocks} ai_link={aiLinkReady}:ramp={aiRampSummary}:support={aiSupportedPoints}/8:max_dy={aiMaximumHeightStep:0.000} ai_opened={aiOpened} ai_continued={aiContinued} authoritative_queue={authoritativeQueued}:converged={authoritativeConverged}:motions={authoritativeInitialMotionCount}->{aiDoor.CompletedMotionCount} motion_counts={motionCountsReady}:{string.Join(',', _refineryDoors.Select(door => door.CompletedMotionCount))}");
        GD.Print($"REFINERY_DOORS_PASS valid={valid}");
        QuitDiagnosticAfterSceneCleanup(valid ? 0 : 2);
    }

    private bool ValidateDoorRaySamples(
        InteractiveBuildingDoor door,
        bool expectBlocked,
        out int checkedSamples)
    {
        checkedSamples = 0;
        var traversal = door.InsideProbe - door.OutsideProbe;
        traversal.Y = 0.0f;
        if (traversal.LengthSquared() < 0.01f)
        {
            return false;
        }
        var tangent = new Vector3(traversal.Z, 0.0f, -traversal.X).Normalized();
        foreach (var offset in new[]
        {
            -door.WidthForNavigation * 0.26f,
            0.0f,
            door.WidthForNavigation * 0.26f
        })
        {
            var blocked = PhysicsRaycast.HasHit(
                GetWorld3D(),
                door.OutsideProbe + tangent * offset,
                door.InsideProbe + tangent * offset,
                1);
            if (blocked != expectBlocked)
            {
                return false;
            }
            checkedSamples++;
        }
        return true;
    }

    private bool ValidateClanHallTraversalLink(
        SquadTraversalLink link,
        out int supportedPoints,
        out float maximumHeightStep,
        out string summary)
    {
        supportedPoints = 0;
        maximumHeightStep = 0.0f;
        summary = "missing_gate_contract";
        if (!JianghaiClanHallGateContract.TryResolve(
                _jianghaiOldCityScene?.Root,
                out var gate,
                out var gateError))
        {
            summary = gateError;
            return false;
        }

        var outwardDistances = new[]
        {
            4.20f,
            3.40f,
            2.60f,
            1.80f,
            1.00f,
            0.20f,
            -0.72f,
            -1.65f
        };
        if (link.ForwardPoints.Length != outwardDistances.Length)
        {
            summary = $"point_count_{link.ForwardPoints.Length}";
            return false;
        }

        var expectedPoints = outwardDistances.Select(distance =>
                JianghaiClanHallGateContract.RampTraversalPoint(gate, distance))
            .ToArray();
        var positionsReady = link.ForwardPoints.Zip(expectedPoints).All(pair =>
            pair.First.DistanceTo(pair.Second) <= 0.025f);
        var monotonicHeight = true;
        for (var index = 1; index < link.ForwardPoints.Length; index++)
        {
            var heightStep = link.ForwardPoints[index].Y - link.ForwardPoints[index - 1].Y;
            maximumHeightStep = Mathf.Max(maximumHeightStep, Mathf.Abs(heightStep));
            monotonicHeight &= heightStep >= -0.01f && Mathf.Abs(heightStep) <= 0.55f;
        }

        var exclusions = BuildRefineryLaneExclusions();
        using var exclusionsBacking = exclusions.AsDisposable();
        var space = GetWorld3D().DirectSpaceState;
        foreach (var point in link.ForwardPoints)
        {
            if (PhysicsRaycast.TryHit(
                    space,
                    point + Vector3.Up * 0.35f,
                    point + Vector3.Down * 0.55f,
                    exclusions,
                    1,
                    out var support)
                && support.Normal.Dot(Vector3.Up) >= 0.80f
                && point.Y - support.Position.Y is >= 0.05f and <= 0.30f)
            {
                supportedPoints++;
            }
        }

        summary = positionsReady && monotonicHeight
            ? "linear_supported"
            : $"positions_{positionsReady}:monotonic_{monotonicHeight}";
        return positionsReady && monotonicHeight
            && supportedPoints == link.ForwardPoints.Length;
    }

    private bool ValidateRefineryDoorVisibilityQuality(out string summary)
    {
        var originalTier = _qualitySetting;
        var tierReady = true;
        var clanRanges = new float[3];
        var landmarkDoors = _refineryDoors.Where(door =>
                !door.IsInGroup("jianghai_enterable_door"))
            .ToArray();
        var batchedEnterableCount = _jianghaiOldCityScene?.Root.GetMeta(
            "jianghai_batched_enterable_source_count",
            -1).AsInt32() ?? -1;
        var baseRangesReady = batchedEnterableCount == 0
            && landmarkDoors.Length == 3
            && landmarkDoors.All(door => Mathf.Abs(
                door.AuthoredBaseVisibilityRange
                - JianghaiLandmarkDoorVisibilityRange) <= 0.01f)
            && _jianghaiInteriors is { } interiors
            && interiors.Rooms.Count == JianghaiInteriorPopulationService.ExpectedRoomCount
            && interiors.Rooms.All(room =>
                room.Source.Layers != 0
                && !JianghaiAuthoredRenderBatcher.HasBatchedSourceMarker(room.Source)
                && Mathf.Abs(
                        room.Door.AuthoredBaseVisibilityRange
                        - JianghaiAuthoredRenderBatcher.CreateQualityPolicy(
                            room.Source).BaseVisibilityRange)
                    <= 0.01f);
        try
        {
            for (var tier = 0; tier <= 2; tier++)
            {
                _jianghaiOldCitySceneLoader.ApplyQuality(tier);
                ApplyRefineryDoorQuality(tier);
                var scale = JianghaiAuthoredRenderBatcher.VisibilityDistanceScale(tier);
                tierReady &= _refineryDoors.All(door =>
                    Mathf.Abs(
                        door.AuthoredEffectiveVisibilityRange
                        - door.AuthoredBaseVisibilityRange * scale) <= 0.01f
                    && door.HasAppliedAuthoredVisibilityRange);
                if (_jianghaiInteriors is { } currentInteriors)
                {
                    tierReady &= currentInteriors.Rooms.All(room =>
                    {
                        var effectiveRange = room.Door.AuthoredEffectiveVisibilityRange;
                        return Mathf.Abs(
                                effectiveRange - room.Source.VisibilityRangeEnd) <= 0.01f
                            && Mathf.Abs(
                                room.Source.VisibilityRangeEndMargin
                                - Mathf.Min(28.0f, effectiveRange * 0.12f)) <= 0.01f
                            && room.Source.VisibilityRangeFadeMode
                                == GeometryInstance3D.VisibilityRangeFadeModeEnum.Self;
                    });
                }
                clanRanges[tier] = _clanHallDoubleGate?
                    .AuthoredEffectiveVisibilityRange ?? -1.0f;
            }
        }
        finally
        {
            _jianghaiOldCitySceneLoader.ApplyQuality(originalTier);
            ApplyRefineryDoorQuality(originalTier);
        }

        var restored = _refineryDoors.All(door => Mathf.Abs(
                door.AuthoredEffectiveVisibilityRange
                - door.AuthoredBaseVisibilityRange
                    * JianghaiAuthoredRenderBatcher.VisibilityDistanceScale(originalTier))
            <= 0.01f);
        summary = $"base={baseRangesReady}:batched_enterable={batchedEnterableCount}:tiers={tierReady}:clan="
            + $"{clanRanges[0]:0.0}/{clanRanges[1]:0.0}/{clanRanges[2]:0.0}"
            + $":restored={restored}:tier={originalTier}";
        return baseRangesReady && tierReady && restored
            && Mathf.Abs(clanRanges[0] - 312.8f) <= 0.01f
            && Mathf.Abs(clanRanges[1] - 386.4f) <= 0.01f
            && Mathf.Abs(clanRanges[2] - 460.0f) <= 0.01f;
    }
}
