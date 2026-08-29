using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private async void CaptureRefineryMap()
    {
        var previousQualitySetting = _qualitySetting;
        var performanceReady = false;
        try
        {
            ApplyQuality(2);
            ApplyTimeOfDay(DeploymentTimeOfDay.Dusk);
            foreach (var enemy in _enemies)
            {
                if (IsInstanceValid(enemy))
                {
                    enemy.ProcessMode = ProcessModeEnum.Disabled;
                    enemy.Visible = false;
                }
            }
            foreach (var squadMate in _squadMates)
            {
                if (IsInstanceValid(squadMate))
                {
                    squadMate.ProcessMode = ProcessModeEnum.Disabled;
                    squadMate.Visible = false;
                }
            }
            foreach (var vehicle in _vehicles)
            {
                if (IsInstanceValid(vehicle))
                {
                    vehicle.ProcessMode = ProcessModeEnum.Disabled;
                    vehicle.Visible = false;
                }
            }
            if (IsInstanceValid(_extractionMarker))
            {
                _extractionMarker.Visible = false;
                HideGeometryRecursive(_extractionMarker);
            }
            if (_extractionAircraft is not null && IsInstanceValid(_extractionAircraft))
            {
                _extractionAircraft.Visible = false;
            }
            _player.Visible = false;
            _hud.Visible = false;
            var camera = new Camera3D
            {
                Name = "OldTownCaptureCamera",
                Fov = 52.0f,
                Far = 1400.0f
            };
            AddChild(camera);
            camera.GlobalPosition = new Vector3(17.5f, 6.0f, -117.0f);
            camera.LookAt(new Vector3(1.5f, 4.8f, -128.2f), Vector3.Up);
            camera.Fov = 35.0f;
            camera.MakeCurrent();
            await WaitFrames(2);
            ApplyTimeOfDay(DeploymentTimeOfDay.Dusk);
            await WaitFrames(16);
            performanceReady = PrintRefineryRenderingSnapshot("overview");
            SaveViewportImage("res://refinery_map_validation.png");

            ApplyTimeOfDay(DeploymentTimeOfDay.Day);
            camera.GlobalPosition = new Vector3(0.0f, 86.0f, 155.0f);
            camera.LookAt(new Vector3(0.0f, 3.0f, -60.0f), Vector3.Up);
            camera.Fov = 64.0f;
            await WaitFrames(14);
            performanceReady &= PrintRefineryRenderingSnapshot("mountain_valley_aerial");
            SaveViewportImage("res://jianghai_valley_validation.png");

            camera.GlobalPosition = new Vector3(112.0f, 1.65f, 86.0f);
            camera.LookAt(new Vector3(18.0f, 11.0f, 300.0f), Vector3.Up);
            camera.Fov = 68.0f;
            await WaitFrames(10);
            performanceReady &= PrintRefineryRenderingSnapshot("mountain_valley_player_south");
            SaveViewportImage("res://jianghai_valley_player_south_validation.png");

            camera.GlobalPosition = new Vector3(-118.0f, 1.65f, -207.0f);
            camera.LookAt(new Vector3(-25.0f, 12.0f, -340.0f), Vector3.Up);
            camera.Fov = 68.0f;
            await WaitFrames(10);
            performanceReady &= PrintRefineryRenderingSnapshot("mountain_valley_player_north");
            SaveViewportImage("res://jianghai_valley_player_north_validation.png");

            camera.GlobalPosition = new Vector3(205.0f, 3.2f, 145.0f);
            camera.LookAt(new Vector3(157.0f, -1.2f, 92.0f), Vector3.Up);
            camera.Fov = 58.0f;
            await WaitFrames(10);
            performanceReady &= PrintRefineryRenderingSnapshot("perimeter_ground_scan");
            SaveViewportImage("res://refinery_ground_validation.png");

            ApplyTimeOfDay(DeploymentTimeOfDay.Dusk);
            camera.GlobalPosition = new Vector3(-3.2f, 1.65f, 32.6f);
            camera.LookAt(new Vector3(-11.7f, 1.85f, 28.5f), Vector3.Up);
            camera.Fov = 40.0f;
            await WaitFrames(14);
            performanceReady &= PrintRefineryRenderingSnapshot("street_life");
            SaveViewportImage("res://jianghai_street_life_validation.png");
            camera.GlobalPosition = new Vector3(-71.5f, 1.75f, -103.5f);
            camera.LookAt(new Vector3(-85.5f, 2.55f, -116.5f), Vector3.Up);
            camera.Fov = 39.0f;
            await WaitFrames(10);
            performanceReady &= PrintRefineryRenderingSnapshot("guangchang_pawnshop");
            SaveViewportImage("res://refinery_hall_validation.png");
            camera.GlobalPosition = new Vector3(105.0f, 2.20f, -13.0f);
            camera.LookAt(new Vector3(97.0f, 2.60f, -2.0f), Vector3.Up);
            camera.Fov = 38.0f;
            await WaitFrames(10);
            performanceReady &= PrintRefineryRenderingSnapshot("red_star_factory");
            SaveViewportImage("res://refinery_wonders_validation.png");
            camera.GlobalPosition = new Vector3(13.5f, 5.55f, -124.2f);
            camera.LookAt(new Vector3(4.5f, 5.0f, -127.0f), Vector3.Up);
            camera.Fov = 34.0f;
            await WaitFrames(10);
            performanceReady &= PrintRefineryRenderingSnapshot("market_footbridge");
            SaveViewportImage("res://old_town_rooftop_validation.png");
            camera.GlobalPosition = new Vector3(-122.0f, 2.0f, -174.0f);
            camera.LookAt(new Vector3(-68.0f, 4.0f, -194.0f), Vector3.Up);
            camera.Fov = 40.0f;
            await WaitFrames(10);
            performanceReady &= PrintRefineryRenderingSnapshot("north_ward_density");
            SaveViewportImage("res://jianghai_density_validation.png");
            ApplyTimeOfDay(DeploymentTimeOfDay.Day);
            camera.GlobalPosition = new Vector3(36.0f, 10.0f, 32.0f);
            camera.LookAt(new Vector3(0.0f, 3.0f, -55.0f), Vector3.Up);
            camera.Fov = 40.0f;
            await WaitFrames(14);
            performanceReady &= PrintRefineryRenderingSnapshot("daylight_overview");
            SaveViewportImage("res://jianghai_day_validation.png");
            var captureRoom = _jianghaiInteriors?.Rooms.FirstOrDefault();
            var captureDoor = captureRoom?.Door;
            var doorCaptureReady = captureRoom is not null && captureDoor is not null;
            performanceReady &= doorCaptureReady;
            if (captureRoom is not null && captureDoor is not null)
            {
                captureDoor.SetOpenImmediate(false);
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                var outward = (captureDoor.OutsideProbe - captureDoor.InsideProbe).Normalized();
                camera.GlobalPosition = captureDoor.InteractionPoint + outward * 7.0f + Vector3.Up * 1.7f;
                camera.LookAt(captureDoor.InteractionPoint + Vector3.Up * 0.6f, Vector3.Up);
                camera.Fov = 56.0f;
                await WaitFrames(8);
                SaveViewportImage("res://refinery_door_closed_validation.png");
                var doorTransitionStarted = captureDoor.TrySetOpen(
                    true,
                    bypassClearance: true);
                for (var frame = 0; frame < 120 && captureDoor.IsAnimating; frame++)
                {
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                }
                await WaitFrames(4);
                doorCaptureReady = doorTransitionStarted
                    && captureDoor.IsOpen
                    && !captureDoor.IsAnimating
                    && captureDoor.MotionAngleDegrees > 85.0f;
                performanceReady &= doorCaptureReady;
                SaveViewportImage("res://refinery_door_open_validation.png");
                camera.GlobalPosition = captureRoom.Root.ToGlobal(
                    new Vector3(0.0f, 1.55f, -0.95f));
                camera.LookAt(
                    captureRoom.Root.ToGlobal(new Vector3(
                        0.0f,
                        0.92f,
                        -Mathf.Min(captureRoom.Depth * 0.66f, 3.4f))),
                    Vector3.Up);
                camera.Fov = 62.0f;
                await WaitFrames(8);
                SaveViewportImage("res://jianghai_interior_validation.png");
                captureDoor.SetOpenImmediate(false);
            }
            GD.Print($"REFINERY_MAP_CAPTURE valid={performanceReady} map_id={DeploymentMapCatalog.BlackwaterRefineryId} identity=jianghai_old_city time=dusk+day authored_meshes={_jianghaiOldCityScene?.MeshInstanceCount ?? 0} authored_surfaces={_jianghaiOldCityScene?.SurfaceCount ?? 0} doors={_refineryDoors.Count} door_transition={doorCaptureReady} paths=refinery_map_validation.png,jianghai_valley_validation.png,jianghai_valley_player_south_validation.png,jianghai_valley_player_north_validation.png,refinery_ground_validation.png,jianghai_street_life_validation.png,refinery_hall_validation.png,refinery_wonders_validation.png,old_town_rooftop_validation.png,jianghai_density_validation.png,jianghai_day_validation.png,refinery_door_closed_validation.png,refinery_door_open_validation.png,jianghai_interior_validation.png");
        }
        finally
        {
            ApplyQuality(previousQualitySetting);
        }
        QuitDiagnosticAfterSceneCleanup(performanceReady ? 0 : 2);
    }

    private static bool PrintRefineryRenderingSnapshot(string view)
    {
        var drawCalls = Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame);
        var objects = Performance.GetMonitor(Performance.Monitor.RenderTotalObjectsInFrame);
        var primitives = Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame);
        var videoMemoryMb = Performance.GetMonitor(Performance.Monitor.RenderVideoMemUsed) / (1024.0 * 1024.0);
        var textureMemoryMb = Performance.GetMonitor(Performance.Monitor.RenderTextureMemUsed) / (1024.0 * 1024.0);
        var withinBudget = drawCalls <= 2400
            && objects <= 2200
            && primitives <= 10_500_000
            && videoMemoryMb <= 1536.0
            && textureMemoryMb <= 1152.0;
        GD.Print($"REFINERY_RENDER_CHECK valid={withinBudget} view={view} draw_calls={drawCalls:0}/2400 objects={objects:0}/2200 primitives={primitives:0}/10500000 video_mem_mb={videoMemoryMb:0.0}/1536 texture_mem_mb={textureMemoryMb:0.0}/1152");
        return withinBudget;
    }
}
