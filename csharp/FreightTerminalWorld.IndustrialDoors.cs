using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private const string IndustrialDoorScenePath =
        "res://assets/models/kenney_factory_kit/door-wide-closed.glb";

    private int _industrialAuthoredLandmarkCount;
    private int _industrialAuthoredDressingCount;
    private int _industrialAuthoredDressingSceneCount;
    private int _industrialWeatheredBuildingCount;

    private void BuildFreightAuthoredLandmarks()
    {
        _industrialAuthoredLandmarkCount = 0;
        _industrialWeatheredBuildingCount = 0;
        var palette = new FreightIndustrialPalette();
        var landmarks = new[]
        {
            (
                Name: "WestRailAdministrationTower",
                File: "building-q.glb",
                Position: new Vector3(-128, 0.02f, -48),
                Yaw: 0.0f,
                Scale: 8.0f,
                CollisionSize: new Vector3(2.14f, 0.88f, 1.77f),
                CollisionOffset: new Vector3(-0.228f, 0.44f, 0.011f)),
            (
                Name: "EastTankProcessTower",
                File: "building-l.glb",
                Position: new Vector3(128, 0.02f, -48),
                Yaw: Mathf.Pi,
                Scale: 7.8f,
                CollisionSize: new Vector3(2.08f, 1.92f, 1.87f),
                CollisionOffset: new Vector3(-0.422f, 0.96f, 0.224f)),
            (
                Name: "SouthFreightControlTower",
                File: "building-a.glb",
                Position: new Vector3(-125, 0.02f, 34),
                Yaw: 0.0f,
                Scale: 8.2f,
                CollisionSize: new Vector3(2.08f, 1.47f, 1.24f),
                CollisionOffset: new Vector3(0, 0.735f, 0)),
            (
                Name: "NorthQuaySignalTower",
                File: "building-r.glb",
                Position: new Vector3(38, 0.02f, -192),
                Yaw: Mathf.Pi,
                Scale: 7.4f,
                CollisionSize: new Vector3(2.48f, 1.39f, 1.27f),
                CollisionOffset: new Vector3(0, 0.695f, 0))
        };

        foreach (var landmark in landmarks)
        {
            var body = ModelProp(
                $"res://assets/models/kenney_city_kit_industrial/{landmark.File}",
                landmark.Position,
                landmark.Yaw,
                landmark.Scale,
                landmark.CollisionSize,
                landmark.CollisionOffset,
                visibilityRange: 320.0f,
                castShadow: true,
                hasDoorway: false);
            body.Name = landmark.Name;
            body.AddToGroup("freight_authored_landmark");
            if (palette.Apply(body) > 0)
            {
                _industrialWeatheredBuildingCount++;
            }
            _industrialAuthoredLandmarkCount++;
        }

        var dressing = new FreightTerminalArtDressingBuilder(palette).Build(_levelRoot);
        _industrialAuthoredDressingCount = dressing.AuthoredModelCount;
        _industrialAuthoredDressingSceneCount = dressing.ScenePaths.Count;
        _industrialWeatheredBuildingCount += dressing.PalettedBuildingCount;
    }

    private void BuildFreightTerminalDoors()
    {
        AddIndustrialDoor(
            _levelRoot,
            "MaintenanceHangarDoor",
            new Vector3(25.0f, 0, -72.86f),
            11.6f,
            6.0f,
            visibilityRange: 280.0f);
    }

    private void AddIndustrialDoor(
        Node3D parent,
        string name,
        Vector3 position,
        float doorwayWidth,
        float doorwayHeight,
        float visibilityRange)
    {
        var mount = new Node3D
        {
            Name = $"{name}Mount",
            Position = position
        };
        mount.AddToGroup("freight_terminal_accessible_building");
        parent.AddChild(mount);

        var door = new InteractiveBuildingDoor
        {
            Name = name
        };
        door.Configure(
            _refineryDoors.Count + 1,
            doorwayWidth,
            doorwayHeight,
            frontZ: 0.0f,
            visibilityRange: visibilityRange);
        mount.AddChild(door);
        _refineryDoors.Add(door);
    }

    private async void ValidateFreightTerminalDoors()
    {
        await WaitFrames(6);
        var expectedDoorCount = 11;
        var countReady = _refineryDoors.Count == expectedDoorCount;
        var idsReady = _refineryDoors.Select(door => door.DoorId).Distinct().Count() == expectedDoorCount
            && _refineryDoors.Select(door => door.DoorId).OrderBy(id => id)
                .SequenceEqual(Enumerable.Range(1, expectedDoorCount));
        var authoredReady = ResourceLoader.Exists(IndustrialDoorScenePath)
            && _refineryDoors.All(door => door.UsesAuthoredVisual && door.HasBoxCollision);
        var initiallyClosed = _refineryDoors.All(door => !door.IsOpen && !door.IsAnimating);
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
            door.IsOpen && !door.IsAnimating && door.MotionAngleDegrees > 85.0f);
        var openClears = _refineryDoors.All(door => !PhysicsRaycast.HasHit(
            GetWorld3D(),
            door.OutsideProbe,
            door.InsideProbe,
            1));

        var first = _refineryDoors.FirstOrDefault();
        var playerPosition = _player.GlobalPosition;
        var occupiedCloseRejected = false;
        if (first is not null)
        {
            _player.GlobalPosition = first.InteractionPoint;
            await WaitFrames(3);
            occupiedCloseRejected = !first.TrySetOpen(false);
            _player.GlobalPosition = first.OutsideProbe + first.OutsideProbe.DirectionTo(first.InsideProbe) * -4.0f;
            await WaitFrames(3);
        }

        var closingStarted = _refineryDoors.All(door =>
            door.TrySetOpen(false, bypassClearance: true));
        for (var frame = 0; frame < 120 && _refineryDoors.Any(door => door.IsAnimating); frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        var closedAgain = _refineryDoors.All(door =>
            !door.IsOpen && !door.IsAnimating && Mathf.Abs(door.MotionAngleDegrees) < 0.5f);
        var closedAgainBlocks = _refineryDoors.All(door => PhysicsRaycast.HasHit(
            GetWorld3D(),
            door.OutsideProbe,
            door.InsideProbe,
            1));
        _player.GlobalPosition = playerPosition;

        var valid = countReady && idsReady && authoredReady && initiallyClosed
            && closedBlocks && openingStarted && opened && openClears
            && occupiedCloseRejected && closingStarted && closedAgain && closedAgainBlocks;
        GD.Print($"FREIGHT_TERMINAL_DOORS_CHECK valid={valid} doors={_refineryDoors.Count}/{expectedDoorCount} ids={idsReady} authored={authoredReady} closed_initial={initiallyClosed} closed_block={closedBlocks} opening={openingStarted} opened={opened} open_clear={openClears} occupied_rejected={occupiedCloseRejected} closing={closingStarted} closed_again={closedAgain} closed_block_again={closedAgainBlocks} landmarks={_industrialAuthoredLandmarkCount}");
        GD.Print($"FREIGHT_TERMINAL_DOORS_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }
}
