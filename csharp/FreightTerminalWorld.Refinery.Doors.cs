using System;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private const string RefineryDoorScenePath =
        InteractiveBuildingDoor.HingedDoorScenePath;
    private const int RefineryDoorVisualPanelCount = 1;
    private const float RefineryDoorMaxAspectDistortion = 1.12f;
    private const float JianghaiLandmarkDoorVisibilityRange = 460.0f;
    private readonly System.Collections.Generic.List<InteractiveBuildingDoor> _refineryDoors = new();
    private InteractiveBuildingDoor? _clanHallDoubleGate;
    private string? _clanHallDoubleGateError;

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
        AddClanHallDoubleGate(parent);
    }

    private void AddClanHallDoubleGate(Node3D parent)
    {
        _clanHallDoubleGate = null;
        _clanHallDoubleGateError = null;
        if (!JianghaiClanHallGateContract.TryResolve(
                _jianghaiOldCityScene?.Root,
                out var gate,
                out var error))
        {
            _clanHallDoubleGateError = error;
            GD.PrintErr($"JIANGHAI_CLAN_HALL_GATE_ERROR {error}");
            if (!_diagnosticSceneLoadFallbackAllowed)
            {
                throw new InvalidOperationException(error);
            }
            return;
        }

        var mount = new Node3D { Name = "JianghaiClanHallDoubleGateMount" };
        mount.AddToGroup("refinery_accessible_building");
        parent.AddChild(mount);
        mount.GlobalTransform = new Transform3D(
            gate.Basis,
            gate.Position);

        var door = new InteractiveBuildingDoor { Name = "JianghaiClanHallDoubleGate" };
        door.Configure(
            _refineryDoors.Count + 1,
            doorwayWidth: gate.Width,
            doorwayHeight: gate.Height,
            frontZ: -JianghaiClanHallGateContract.DoorInset,
            visibilityRange: JianghaiLandmarkDoorVisibilityRange,
            motionStyle: BuildingDoorMotionStyle.DoubleHinged,
            visualScenePath: JianghaiInteriorPopulationService.LatticeDoorScenePath,
            sourceWidth: 0.8f,
            sourceHeight: 1.6f,
            hingedVisualUsesPivotOrigin: true,
            disableVisualShadows: true,
            widthFillRatio: 1.0f,
            heightFillRatio: 1.0f);
        door.SetMeta("jianghai_gate_anchor", JianghaiClanHallGateContract.AnchorName);
        door.SetMeta("jianghai_gate_width_m", gate.Width);
        door.SetMeta("jianghai_gate_height_m", gate.Height);
        mount.AddChild(door);
        _refineryDoors.Add(door);
        _clanHallDoubleGate = door;

        RegisterSquadTraversalLink(
            $"refinery_door:{door.DoorId}",
            SquadTraversalKind.Walk,
            bidirectional: true,
            new[]
            {
                JianghaiClanHallGateContract.RampTraversalPoint(gate, 4.20f),
                JianghaiClanHallGateContract.RampTraversalPoint(gate, 3.40f),
                JianghaiClanHallGateContract.RampTraversalPoint(gate, 2.60f),
                JianghaiClanHallGateContract.RampTraversalPoint(gate, 1.80f),
                JianghaiClanHallGateContract.RampTraversalPoint(gate, 1.00f),
                JianghaiClanHallGateContract.RampTraversalPoint(gate, 0.20f),
                JianghaiClanHallGateContract.RampTraversalPoint(gate, -0.72f),
                JianghaiClanHallGateContract.RampTraversalPoint(gate, -1.65f)
            },
            costMultiplier: 1.02f);
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
            doorwayWidth: 1.45f,
            doorwayHeight: 2.65f,
            frontZ: 0.0f,
            visibilityRange: JianghaiLandmarkDoorVisibilityRange,
            motionStyle: BuildingDoorMotionStyle.Hinged);
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

    private void ApplyRefineryDoorQuality(int qualityTier)
    {
        var distanceScale = JianghaiAuthoredRenderBatcher.VisibilityDistanceScale(
            qualityTier);
        foreach (var door in _refineryDoors)
        {
            if (IsInstanceValid(door))
            {
                door.ApplyVisibilityScale(distanceScale);
            }
        }
    }

    private bool TryHandleRefineryDoorInteraction(float competingInteractionDistance)
    {
        if (_refineryDoors.Count == 0)
        {
            return false;
        }
        var nearest = FindNearestRefineryDoor(
            _player.GlobalPosition,
            3.25f,
            out var nearestDoorDistance);
        if (nearest is null || nearestDoorDistance > competingInteractionDistance)
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
        => FindNearestRefineryDoor(origin, range, out _);

    private InteractiveBuildingDoor? FindNearestRefineryDoor(
        Vector3 origin,
        float range,
        out float distance)
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
        distance = nearest is null
            ? float.PositiveInfinity
            : Mathf.Sqrt(nearestDistanceSquared);
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
        door.ApplyAuthoritativeOpenState(open);
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

}
