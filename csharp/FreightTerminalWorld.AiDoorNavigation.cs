using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private const float AiDoorApproachRange = 3.8f;
    private const float AiDoorLateralPadding = 0.7f;

    /// <summary>
    /// Opens the authored door intersected by an actor's next navigation segment.
    /// The building door list is persistent and bounded, so this index scan
    /// stays allocation-free without a scene-tree lookup on the physics hot path.
    /// </summary>
    internal bool TryPrepareAiDoorTraversal(
        Vector3 actorPosition,
        Vector3 nextWaypoint,
        out bool waiting)
    {
        waiting = false;
        if (IsExtractionNetworkClient)
        {
            return false;
        }

        for (var index = 0; index < _refineryDoors.Count; index++)
        {
            var door = _refineryDoors[index];
            if (!IsInstanceValid(door)
                || !SegmentTargetsRefineryDoor(door, actorPosition, nextWaypoint))
            {
                continue;
            }

            if (!door.IsOpen && !door.TargetOpen && !door.IsAnimating
                && door.TrySetOpen(true, bypassClearance: true)
                && IsExtractionNetworkMatch
                && _squadNetwork.IsHost)
            {
                _squadNetwork.BroadcastExtractionDoorState(door.DoorId, door.TargetOpen);
            }

            waiting = !door.IsOpen;
            return true;
        }
        return false;
    }

    private static bool SegmentTargetsRefineryDoor(
        InteractiveBuildingDoor door,
        Vector3 actorPosition,
        Vector3 nextWaypoint)
    {
        var center = door.InteractionPoint;
        center.Y = actorPosition.Y;
        var normal = door.InsideProbe - door.OutsideProbe;
        normal.Y = 0.0f;
        if (normal.LengthSquared() <= 0.01f)
        {
            return false;
        }
        normal = normal.Normalized();

        var actorOffset = actorPosition - center;
        var waypointOffset = nextWaypoint - center;
        actorOffset.Y = 0.0f;
        waypointOffset.Y = 0.0f;
        var actorSide = actorOffset.Dot(normal);
        var waypointSide = waypointOffset.Dot(normal);
        var crossesPlane = actorSide * waypointSide <= 0.0f
            || actorSide < 0.0f && Mathf.Abs(waypointSide) <= AiDoorApproachRange;
        if (!crossesPlane
            || actorOffset.LengthSquared() > AiDoorApproachRange * AiDoorApproachRange)
        {
            return false;
        }

        var tangent = new Vector3(-normal.Z, 0.0f, normal.X);
        var actorLateral = Mathf.Abs(actorOffset.Dot(tangent));
        var waypointLateral = Mathf.Abs(waypointOffset.Dot(tangent));
        return Mathf.Min(actorLateral, waypointLateral)
            <= door.WidthForNavigation + AiDoorLateralPadding;
    }
}
