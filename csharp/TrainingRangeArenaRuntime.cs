using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

/// <summary>
/// Owns the self-contained training-range scene and its collision lifecycle.
///
/// The range is deliberately built below its own root at a remote world origin.  It
/// never reuses the extraction or demolition level geometry, so switching into the
/// range cannot change the playable collision of either production mode.
/// </summary>
public sealed class TrainingRangeArenaRuntime
{
    private readonly List<StaticBody3D> _collisionBodies;
    private readonly List<Node3D> _authoredModels;

    public Node3D Root { get; }
    public Vector3 Origin { get; }
    public Vector3 PlayerSpawn { get; }
    public IReadOnlyList<TrainingRangeBotProfile> BotProfiles { get; }
    public IReadOnlyList<Vector3> BotSpawns { get; }
    public IReadOnlyList<TrainingRangeStation> Stations { get; }
    public bool Active { get; private set; }
    public int CollisionBodyCount => _collisionBodies.Count;
    public int AuthoredModelCount => _authoredModels.Count;
    public int BotSpawnCount => BotSpawns.Count;

    internal TrainingRangeArenaRuntime(
        Node3D root,
        Vector3 origin,
        Vector3 playerSpawn,
        IReadOnlyList<TrainingRangeBotProfile> botProfiles,
        IReadOnlyList<TrainingRangeStation> stations,
        List<StaticBody3D> collisionBodies,
        List<Node3D> authoredModels)
    {
        Root = root;
        Origin = origin;
        PlayerSpawn = playerSpawn;
        BotProfiles = botProfiles;
        var botSpawns = new List<Vector3>(botProfiles.Count);
        foreach (var profile in botProfiles)
        {
            botSpawns.Add(profile.Position);
        }
        BotSpawns = botSpawns.AsReadOnly();
        Stations = stations;
        _collisionBodies = collisionBodies;
        _authoredModels = authoredModels;
        SetActive(false);
    }

    /// <summary>Shows the range and enables only its world collision layer.</summary>
    public void SetActive(bool active)
    {
        Active = active;
        if (GodotObject.IsInstanceValid(Root))
        {
            Root.Visible = active;
            Root.ProcessMode = active
                ? Node.ProcessModeEnum.Inherit
                : Node.ProcessModeEnum.Disabled;
        }

        var collisionLayer = active ? 1u : 0u;
        foreach (var body in _collisionBodies)
        {
            if (!GodotObject.IsInstanceValid(body))
            {
                continue;
            }
            body.CollisionLayer = collisionLayer;
            body.CollisionMask = 0;
        }
    }

    public bool Owns(Node node)
        => GodotObject.IsInstanceValid(Root) && (node == Root || Root.IsAncestorOf(node));

    public Vector3 BotSpawn(int index)
        => BotSpawns[Mathf.Clamp(index, 0, BotSpawns.Count - 1)];

    public TrainingRangeBotProfile BotProfile(int index)
        => BotProfiles[Mathf.Clamp(index, 0, BotProfiles.Count - 1)];

    public bool IsStationInRange(Vector3 worldPosition, TrainingRangeStationKind kind, float extraRadius = 0.0f)
    {
        foreach (var station in Stations)
        {
            if (station.Kind != kind)
            {
                continue;
            }
            var delta = worldPosition - station.Position;
            delta.Y = 0.0f;
            var radius = station.Radius + extraRadius;
            if (delta.LengthSquared() <= radius * radius)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Returns whether all arena collision bodies still follow the active world-layer
    /// contract.  Diagnostics use this to catch accidentally leaked map collision.
    /// </summary>
    public bool CollisionLifecycleIsValid()
    {
        var expectedLayer = Active ? 1u : 0u;
        foreach (var body in _collisionBodies)
        {
            if (!GodotObject.IsInstanceValid(body)
                || body.CollisionLayer != expectedLayer
                || body.CollisionMask != 0)
            {
                return false;
            }
        }
        return true;
    }
}

public enum TrainingRangeStationKind
{
    Weapon,
    Ammunition,
    BotControl
}

/// <summary>Stable target configuration for one lane in the dedicated range.</summary>
public readonly record struct TrainingRangeBotProfile(
    int Index,
    Vector3 Position,
    OperatorVisualId Visual,
    string DisplayName,
    float RespawnDelaySeconds = 0.85f);

/// <summary>Interaction anchor exposed to the HUD/world interaction layer.</summary>
public readonly record struct TrainingRangeStation(
    string Id,
    TrainingRangeStationKind Kind,
    Vector3 Position,
    float Radius,
    string DisplayName);
