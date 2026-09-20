using System;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private async void ValidateCanalAccess()
    {
        var valid = false;
        try
        {
            PrepareLanternCanalDiagnosticActors();
            _player.ProcessMode = ProcessModeEnum.Inherit;
            _player.UiLocked = false;
            _player.RestoreMovementInput();
            await WaitFrames(3);
            var routes = _roofAccessRoutes.Where(route => route.Id.StartsWith("CanalAccess_", StringComparison.Ordinal)).ToArray();
            var paths = routes.Length == 36;
            foreach (var route in routes)
            {
                var clear = _player.CanTraverseLadderPath(route.BottomFeet, route.TopFeet, route.Outward);
                var bottomFloor = HasRoofAccessFloor(route.BottomFeet, .5f);
                var topFloor = HasRoofAccessFloor(route.TopFeet, .5f);
                paths &= clear && bottomFloor && topFloor;
                GD.Print($"CANAL_ACCESS_PATH id={route.Id} clear={clear} floors={bottomFloor}/{topFloor} blocker={_player.LadderPathBlockerForDiagnostics} bottom={route.BottomFeet} top={route.TopFeet}");
            }
            var climbed = true;
            foreach (var route in routes.Where((_, index) => index is 0 or 1 or 12 or 18))
            {
                _player.GlobalPosition = route.BottomFeet;
                _player.Velocity = Vector3.Zero;
                _player.FaceWorldPointForDiagnostics(route.BottomFeet - route.Outward * 5);
                await WaitForLadderRemountForDiagnostics();
                if (route == routes[0])
                {
                    await WaitFrames(8);
                    SaveViewportImage("res://logs/canal_ladder_water.png");
                }
                foreach (var down in new[] { false, true })
                {
                    await WaitForLadderRemountForDiagnostics();
                    var mounted = _player.BeginLadderClimb(route.BottomFeet, route.TopFeet, route.Outward, down);
                    Input.ActionPress(down ? "move_backward" : "move_forward");
                    for (var frame = 0; frame < 260 && _player.IsClimbingLadder; frame++)
                        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                    Input.ActionRelease(down ? "move_backward" : "move_forward");
                    await ToSignal(GetTree().CreateTimer(.4), SceneTreeTimer.SignalName.Timeout);
                    var target = down ? route.BottomFeet : route.TopFeet;
                    var reached = mounted && !_player.IsClimbingLadder && _player.IsOnFloor()
                        && _player.GlobalPosition.DistanceTo(target) < .65f;
                    climbed &= reached;
                    GD.Print($"CANAL_ACCESS_CLIMB id={route.Id} down={down} mounted={mounted} reached={reached} feet={_player.GlobalPosition}");
                    if (!down && route == routes[0]) SaveViewportImage("res://logs/canal_ladder_ashore.png");
                }
            }
            valid = paths && climbed;
            GD.Print($"CANAL_ACCESS_CHECK routes={routes.Length} paths={paths} climbed={climbed} valid={valid}");
        }
        catch (Exception exception) { GD.PrintErr($"CANAL_ACCESS_CHECK error={exception}"); }
        finally
        {
            Input.ActionRelease("move_forward");
            Input.ActionRelease("move_backward");
        }
        GD.Print($"CANAL_ACCESS_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }
}
