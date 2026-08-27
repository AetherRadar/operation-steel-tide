using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private int _industrialAuthoredLandmarkCount;
    private int _industrialAuthoredDressingCount;
    private int _industrialAuthoredDressingSceneCount;
    private int _industrialWeatheredBuildingCount;

    private void BuildFreightAuthoredLandmarks()
    {
        _industrialAuthoredLandmarkCount = 0;
        _industrialWeatheredBuildingCount = 0;
        var palette = new FreightIndustrialPalette();
        _industrialInteriors = new FreightIndustrialInteriorBuilder(palette).Build(
            _levelRoot,
            _refineryDoors.Count + 1);
        _refineryDoors.AddRange(_industrialInteriors.Doors);
        _industrialAuthoredLandmarkCount = _industrialInteriors.LandmarkCount;
        _industrialWeatheredBuildingCount = _industrialInteriors.PalettedBuildingCount;
        _industrialInteriorSeed = unchecked((int)_rng.Randi());
        _industrialRoomContentPlans = FreightIndustrialRoomContentPlanner.Plan(
            _industrialInteriorSeed,
            _industrialInteriors.Rooms);

        var dressing = new FreightTerminalArtDressingBuilder(palette).Build(_levelRoot);
        _industrialAuthoredDressingCount = dressing.AuthoredModelCount
            + _industrialInteriors.AuthoredBuildingCount;
        _industrialAuthoredDressingSceneCount = dressing.ScenePaths
            .Concat(_industrialInteriors.ScenePaths)
            .Distinct()
            .Count();
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
        foreach (var civilian in _civilians)
        {
            if (IsInstanceValid(civilian))
            {
                civilian.ProcessMode = ProcessModeEnum.Disabled;
            }
        }
        const int expectedDoorCount = 74;
        var countReady = _refineryDoors.Count == expectedDoorCount;
        var idsReady = _refineryDoors.Select(door => door.DoorId).Distinct().Count() == expectedDoorCount
            && _refineryDoors.Select(door => door.DoorId).OrderBy(id => id)
                .SequenceEqual(Enumerable.Range(1, expectedDoorCount));
        var authoredReady = ResourceLoader.Exists(InteractiveBuildingDoor.OverheadDoorScenePath)
            && ResourceLoader.Exists(InteractiveBuildingDoor.HingedDoorScenePath)
            && _refineryDoors.All(door => door.UsesAuthoredVisual && door.HasBoxCollision);
        var initiallyClosed = _refineryDoors.All(door => !door.IsOpen && !door.IsAnimating);
        var closedBlocks = _refineryDoors.All(door => PhysicsRaycast.HasHit(
            GetWorld3D(),
            door.OutsideProbe,
            door.InsideProbe,
            1));

        var openingStarted = _refineryDoors.All(door =>
            door.TrySetOpen(true, bypassClearance: true));
        var openingDeadline = Time.GetTicksMsec() + 3000UL;
        while (_refineryDoors.Any(door => door.IsAnimating)
            && Time.GetTicksMsec() < openingDeadline)
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
        var blockedOpenDoors = _refineryDoors
            .Select(door => PhysicsRaycast.TryHit(
                    GetWorld3D(),
                    door.OutsideProbe,
                    door.InsideProbe,
                    1,
                    out var hit)
                ? $"{door.DoorId}:{door.Name}:{door.MotionStyle}:{(hit.Collider as Node)?.Name}"
                : string.Empty)
            .Where(value => value.Length > 0)
            .ToArray();

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
        var closingDeadline = Time.GetTicksMsec() + 3000UL;
        while (_refineryDoors.Any(door => door.IsAnimating)
            && Time.GetTicksMsec() < closingDeadline)
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
        GD.Print($"FREIGHT_TERMINAL_DOORS_CHECK valid={valid} doors={_refineryDoors.Count}/{expectedDoorCount} ids={idsReady} authored={authoredReady} closed_initial={initiallyClosed} closed_block={closedBlocks} opening={openingStarted} opened={opened} open_clear={openClears} blocked_open={string.Join(',', blockedOpenDoors)} occupied_rejected={occupiedCloseRejected} closing={closingStarted} closed_again={closedAgain} closed_block_again={closedAgainBlocks} landmarks={_industrialAuthoredLandmarkCount}");
        GD.Print($"FREIGHT_TERMINAL_DOORS_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }
}
