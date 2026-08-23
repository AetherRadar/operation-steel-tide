using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private const string RefineryDoorScenePath =
        "res://assets/models/kenney_factory_kit/door-wide-closed.glb";
    private readonly System.Collections.Generic.List<InteractiveBuildingDoor> _refineryDoors = new();

    private void BuildOldTownLandmarkDoors(
        Node3D parent,
        OldTownLandmarksResult landmarks)
    {
        AddOldTownLandmarkDoor(
            parent,
            "GrandHotelDoor",
            landmarks.HotelEntry,
            landmarks.HotelInterior);
        AddOldTownLandmarkDoor(
            parent,
            "MunicipalTreasuryDoor",
            landmarks.TreasuryEntry,
            landmarks.TreasuryInterior);
    }

    private void AddOldTownLandmarkDoor(
        Node3D parent,
        string name,
        Vector3 entry,
        Vector3 interior)
    {
        var inward = (interior - entry).Normalized();
        var outward = -inward;
        var doorPlane = entry + inward * 1.8f;
        doorPlane.Y = 0.0f;
        var mount = new Node3D
        {
            Name = $"{name}Mount",
            Position = doorPlane,
            Rotation = new Vector3(0, Mathf.Atan2(outward.X, outward.Z), 0)
        };
        mount.AddToGroup("refinery_accessible_building");
        parent.AddChild(mount);

        var door = new InteractiveBuildingDoor
        {
            Name = name
        };
        door.Configure(
            _refineryDoors.Count + 1,
            doorwayWidth: 7.6f,
            doorwayHeight: 4.3f,
            frontZ: 0.0f,
            visibilityRange: 220.0f);
        mount.AddChild(door);
        _refineryDoors.Add(door);

        var groundInward = inward;
        groundInward.Y = 0.0f;
        groundInward = groundInward.LengthSquared() > 0.01f
            ? groundInward.Normalized()
            : Vector3.Back;
        var outsidePoint = doorPlane - groundInward * 1.05f + Vector3.Up * 0.12f;
        var insidePoint = doorPlane + groundInward * 1.05f + Vector3.Up * 0.12f;
        RegisterSquadTraversalLink(
            $"refinery_door:{door.DoorId}",
            SquadTraversalKind.Walk,
            bidirectional: true,
            new[]
            {
                new Vector3(entry.X, 0.12f, entry.Z),
                outsidePoint,
                insidePoint,
                new Vector3(interior.X, 0.12f, interior.Z)
            },
            costMultiplier: 1.04f);
    }

    private bool TryHandleRefineryDoorInteraction()
    {
        if (_refineryDoors.Count == 0)
        {
            return false;
        }
        var nearest = FindNearestRefineryDoor(_player.GlobalPosition, 3.25f);
        if (nearest is null)
        {
            return false;
        }

        _lootSearchTarget = null;
        _interactionProgress = 0.0f;
        _player.SetSearchPose(false);
        _hud.SetInteraction(nearest.InteractionLabel(_languageSetting), -1.0f, true);
        if (_interactReleaseRequired || !Input.IsActionJustPressed(GameInputActions.Interact))
        {
            return true;
        }

        _interactReleaseRequired = true;
        if (IsExtractionNetworkClient)
        {
            _squadNetwork.RequestExtractionDoorToggle(nearest.DoorId);
            return true;
        }
        if (!nearest.TryToggle())
        {
            if (!nearest.IsAnimating)
            {
                _hud.ShowLocalizedMessage(
                    "door_blocked",
                    "DOOR BLOCKED  //  CLEAR THE ENTRY",
                    new Color(1.0f, 0.62f, 0.28f));
            }
            return true;
        }
        if (IsExtractionNetworkMatch && _squadNetwork.IsHost)
        {
            _squadNetwork.BroadcastExtractionDoorState(nearest.DoorId, nearest.TargetOpen);
        }
        return true;
    }

    private InteractiveBuildingDoor? FindNearestRefineryDoor(Vector3 origin, float range)
    {
        InteractiveBuildingDoor? nearest = null;
        var nearestDistanceSquared = range * range;
        for (var index = _refineryDoors.Count - 1; index >= 0; index--)
        {
            var door = _refineryDoors[index];
            if (!IsInstanceValid(door))
            {
                _refineryDoors.RemoveAt(index);
                continue;
            }
            var distanceSquared = origin.DistanceSquaredTo(door.InteractionPoint);
            if (distanceSquared >= nearestDistanceSquared)
            {
                continue;
            }
            nearest = door;
            nearestDistanceSquared = distanceSquared;
        }
        return nearest;
    }

    private InteractiveBuildingDoor? RefineryDoorById(int doorId)
        => doorId > 0 && doorId <= _refineryDoors.Count
            ? _refineryDoors[doorId - 1]
            : null;

    private void OnExtractionDoorToggleRequested(long peerId, int doorId)
    {
        if (!IsExtractionNetworkMatch || !_squadNetwork.IsHost
            || RefineryDoorById(doorId) is not { } door
            || !TryResolveExtractionDoorRequester(peerId, out var requesterPosition)
            || requesterPosition.DistanceTo(door.InteractionPoint) > 3.65f
            || !door.TryToggle())
        {
            return;
        }
        _squadNetwork.BroadcastExtractionDoorState(door.DoorId, door.TargetOpen);
    }

    private bool TryResolveExtractionDoorRequester(long peerId, out Vector3 position)
    {
        if (peerId == 1)
        {
            position = _player.GlobalPosition;
            return true;
        }
        var proxy = _squadMates.FirstOrDefault(mate =>
            IsInstanceValid(mate)
            && mate.IsHumanProxy
            && mate.NetworkPeerId == peerId);
        if (proxy is not null)
        {
            position = proxy.GlobalPosition;
            return true;
        }
        position = default;
        return false;
    }

    private void OnExtractionDoorState(int doorId, bool open)
    {
        if (!IsExtractionNetworkClient || RefineryDoorById(doorId) is not { } door)
        {
            return;
        }
        door.TrySetOpen(open, bypassClearance: true);
    }

    private void SendAllExtractionDoorStates(long peerId)
    {
        if (!IsExtractionNetworkMatch || !_squadNetwork.IsHost || peerId <= 1)
        {
            return;
        }
        foreach (var door in _refineryDoors)
        {
            if (IsInstanceValid(door))
            {
                _squadNetwork.SendExtractionDoorState(peerId, door.DoorId, door.TargetOpen);
            }
        }
    }

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
        var expectedDoorCount = _oldTownLandmarks?.EntryCount ?? 0;
        var countReady = _refineryDoors.Count == expectedDoorCount
            && expectedDoorCount == 2;
        var idsReady = _refineryDoors.Select(door => door.DoorId).Distinct().Count() == expectedDoorCount
            && _refineryDoors.Select(door => door.DoorId).OrderBy(id => id)
                .SequenceEqual(Enumerable.Range(1, expectedDoorCount));
        var authoredReady = ResourceLoader.Exists(RefineryDoorScenePath)
            && _refineryDoors.All(door => door.UsesAuthoredVisual && door.HasBoxCollision);
        var initiallyClosed = _refineryDoors.All(door => !door.IsOpen && !door.IsAnimating);
        var first = _refineryDoors.FirstOrDefault();
        if (first is null)
        {
            GD.Print("REFINERY_DOORS_CHECK valid=False reason=no_doors");
            GD.Print("REFINERY_DOORS_PASS valid=False");
            GetTree().Quit(2);
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
        var openClears = _refineryDoors.All(door => !PhysicsRaycast.HasHit(
            GetWorld3D(),
            door.OutsideProbe,
            door.InsideProbe,
            1));
        var closePromptReady = _refineryDoors.All(door =>
            door.InteractionLabel("en") == "CLOSE DOOR");

        var playerPosition = _player.GlobalPosition;
        _player.GlobalPosition = first.InteractionPoint;
        await WaitFrames(3);
        var occupiedCloseRejected = !first.TrySetOpen(false);
        var outward = (first.OutsideProbe - first.InsideProbe).Normalized();
        _player.GlobalPosition = first.OutsideProbe + outward * 4.0f;
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

        var aiLinkReady = _squadTraversalLinks.Any(link =>
            link.Source == $"refinery_door:{first.DoorId}"
            && link.Kind == SquadTraversalKind.Walk
            && link.Bidirectional);
        var aiActorPosition = first.OutsideProbe;
        var aiWaypoint = first.InsideProbe;
        var aiOpened = TryPrepareAiDoorTraversal(
            aiActorPosition,
            aiWaypoint,
            out var aiWaitingDuringMotion)
            && aiWaitingDuringMotion;
        for (var frame = 0; frame < 120 && first.IsAnimating; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        var aiContinued = TryPrepareAiDoorTraversal(
            aiActorPosition,
            aiWaypoint,
            out var aiWaitingAfterMotion)
            && first.IsOpen
            && !aiWaitingAfterMotion;
        first.SetOpenImmediate(false);
        _player.GlobalPosition = playerPosition;

        var valid = countReady && idsReady && authoredReady && initiallyClosed
            && englishPromptReady && chinesePromptReady && nearestReady
            && closedBlocks && openingStarted && opened && openClears && closePromptReady
            && occupiedCloseRejected && closingStarted && closedAgain && closedAgainBlocks
            && aiLinkReady && aiOpened && aiContinued;
        GD.Print($"REFINERY_DOORS_CHECK valid={valid} doors={_refineryDoors.Count}/{expectedDoorCount} ids={idsReady} authored={authoredReady} closed_initial={initiallyClosed} prompt_en={englishPromptReady} prompt_zh={chinesePromptReady} nearest={nearestReady} closed_block={closedBlocks} opening={openingStarted} opened={opened} angle={first.MotionAngleDegrees:0.0} open_clear={openClears} close_prompt={closePromptReady} occupied_rejected={occupiedCloseRejected} closing={closingStarted} closed_again={closedAgain} closed_block_again={closedAgainBlocks} ai_link={aiLinkReady} ai_opened={aiOpened} ai_continued={aiContinued} motions={string.Join(',', _refineryDoors.Select(door => door.CompletedMotionCount))}");
        GD.Print($"REFINERY_DOORS_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }
}
