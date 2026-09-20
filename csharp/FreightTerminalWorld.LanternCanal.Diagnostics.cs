using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private readonly record struct LanternCanalFloorProbe(
        string Name, string Source, Vector3 Surface, bool SettlePlayer = false,
        float MaximumFeetClearance = 0.08f);

    // Independent measurements from the v2.5 authored mesh catch both a misplaced
    // collider and an asset-coordinate regression; runtime collision data is not reused.
    private static readonly LanternCanalFloorProbe[] LanternCanalFloorProbes =
    {
        new("deployment_street", "00_Ground_and_quays", new(-68.0f, 1.12f, 76.0f), true),
        new("extraction_approach", "00_Ground_and_quays", new(-68.0f, 1.12f, -76.0f)),
        new("starter_case_approach", "00_Ground_and_quays", new(-61.0f, 1.12f, 80.0f)),
        new("south_street", "00_Ground_and_quays", new(-12.0f, 1.12f, 76.0f)),
        new("east_street", "00_Ground_and_quays", new(42.0f, 1.12f, 42.0f)),
        new("hunter_quay_start", "00_Ground_and_quays", new(68.0f, 1.12f, -80.0f)),
        new("hunter_quay_end", "00_Ground_and_quays", new(68.0f, 1.12f, -96.0f)),
        new("ceramics_entry", "00_Ground_and_quays", new(12.0f, 1.12f, 66.0f)),
        new("silk_entry", "00_Ground_and_quays", new(12.0f, 1.12f, -23.0f)),
        // The capsule contacts neighboring arched planks above the center ray;
        // allow that geometric clearance plus the existing player safe margin.
        new("bridge_center", "31_CanalFootbridge_01", new(0.0f, 2.9f, 81.0f), true, 0.14f),
        new("bridge_slope", "31_CanalFootbridge_01", new(-6.0f, 2.173f, 81.0f)),
        new("ceramics_ground", "10_House_E_01", new(18.0f, 1.255f, 67.0f), true),
        new("ceramics_first_floor", "80_V25_Circulation_ceramics", new(19.0f, 5.105f, 68.9f), true),
        new("ceramics_second_floor", "80_V25_Circulation_ceramics", new(19.0f, 8.905f, 68.9f)),
        new("silk_ground", "10_House_E_05", new(18.0f, 1.255f, -21.0f), true),
        new("silk_first_floor", "80_V25_Circulation_silk", new(19.0f, 5.105f, -19.1f))
    };

    private async void ValidateLanternCanal()
    {
        var valid = false;
        try
        {
            PrepareLanternCanalDiagnosticActors();
            await WaitFrames(3);
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            var workshopReady = IsInstanceValid(_lanternCanalScene)
                && FindLanternCanalNode(_lanternCanalScene, "10_House_E_01") is not null
                && FindLanternCanalNode(_lanternCanalScene, "10_House_E_05") is not null;
            var meshCount = CountLanternCanalMeshes(_lanternCanalScene);
            var lightCount = CountLanternCanalLights(_lanternCanalScene);
            var visibleMountainCount = CountVisibleLanternCanalMeshes(_lanternCanalBackdrop);
            using var completeCityFile = FileAccess.Open(LanternCanalScenePath, FileAccess.ModeFlags.Read);
            var completeCityBytes = completeCityFile?.GetLength() ?? 0;
            var completeCityReady = completeCityBytes is > 0 and < 330_000_000
                && meshCount >= 180;
            var backdropReady = IsInstanceValid(_lanternCanalBackdrop)
                && _lanternCanalMountainMeshCount >= 12
                && visibleMountainCount == _lanternCanalMountainMeshCount;
            var collisionReady = _lanternCanalCollisionCount >= 30;
            var gameplayReady = _lanternCanalObjectiveCount == 2 && _lanternCanalLootCount == 9
                && _lootSources.Count == 9 && _barrels.Count == 0
                && IsInstanceValid(_worldBoss) && _worldBoss!.GlobalPosition.Y >= 1.12f
                && ActiveWorldBossPatrolRoute == LanternCanalWorldBossPatrolRoute;
            var spawnReady = DeploymentPoint == LanternCanalDeploymentPoint
                && ExtractionPoint == LanternCanalExtractionPoint
                && DeploymentPoint.DistanceTo(ExtractionPoint) > 140.0f
                && DeploymentPoint.Y >= 1.12f && DeploymentPoint.Y < 1.6f;
            var floorReady = true;
            var floorCount = 0;
            var settledCount = 0;
            foreach (var probe in LanternCanalFloorProbes)
            {
                var matched = ValidateLanternCanalFloor(probe);
                floorReady &= matched;
                floorCount += matched ? 1 : 0;
                if (probe.SettlePlayer)
                {
                    var settled = await SettleLanternCanalPlayer(probe.Surface, probe.MaximumFeetClearance);
                    floorReady &= settled;
                    settledCount += settled ? 1 : 0;
                    GD.Print($"LANTERN_CANAL_PLAYER name={probe.Name} valid={settled} feet={_player.GlobalPosition.Y:0.000} surface={probe.Surface.Y:0.000} eye={_player.DiagnosticCameraPosition.Y:0.000} on_floor={_player.IsOnFloor()}");
                }
            }
            var canalOpen = !PhysicsRaycast.HasHit(GetWorld3D(),
                new Vector3(0.0f, 1.7f, 60.0f), new Vector3(0.0f, 0.4f, 60.0f), _player.GetRid(), 1);
            var lightingReady = ValidateLanternCanalLighting(out var lightingSummary);
            valid = workshopReady && completeCityReady && backdropReady && meshCount >= 12
                && lightCount >= 10 && collisionReady && gameplayReady && spawnReady
                && floorReady && canalOpen && lightingReady;
            GD.Print($"LANTERN_CANAL_CHECK valid={valid} workshop={workshopReady} complete_city={completeCityReady} bytes={completeCityBytes} backdrop_mountains={backdropReady} mountain_meshes={_lanternCanalMountainMeshCount} visible_mountains={visibleMountainCount} meshes={meshCount} lights={lightCount} collision={_lanternCanalCollisionCount} objectives={_lanternCanalObjectiveCount} loot={_lanternCanalLootCount} spawn={spawnReady} aligned_floors={floorCount}/{LanternCanalFloorProbes.Length} settled_players={settledCount}/5 canal_open={canalOpen} lighting={lightingReady}:{lightingSummary}");
        }
        catch (Exception exception)
        {
            GD.PrintErr($"LANTERN_CANAL_CHECK valid=False error={exception.Message}");
        }
        GD.Print($"LANTERN_CANAL_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }

    private void PrepareLanternCanalDiagnosticActors()
    {
        foreach (var enemy in _enemies)
        {
            if (IsInstanceValid(enemy))
            {
                enemy.ProcessMode = ProcessModeEnum.Disabled;
                enemy.Visible = false;
                enemy.CollisionLayer = 0;
                enemy.CollisionMask = 0;
            }
        }
        foreach (var mate in _squadMates)
        {
            if (IsInstanceValid(mate))
            {
                mate.ProcessMode = ProcessModeEnum.Disabled;
                mate.Visible = false;
                mate.CollisionLayer = 0;
                mate.CollisionMask = 0;
            }
        }
        Input.MouseMode = Input.MouseModeEnum.Visible;
        foreach (var action in new[] { "move_left", "move_right", "move_forward", "move_backward", "jump", "sprint", "crouch", "prone", "fire" })
        {
            Input.ActionRelease(action);
        }
        _player.UiLocked = false;
        _player.TrySetStance(PlayerStance.Standing);
    }

    private bool ValidateLanternCanalFloor(LanternCanalFloorProbe probe)
    {
        var authoredY = FindLanternCanalVisibleFloor(probe);
        var hit = PhysicsRaycast.TryHit(GetWorld3D(),
            probe.Surface + Vector3.Up * 0.35f, probe.Surface + Vector3.Down * 0.35f,
            _player.GetRid(), 1, out var floor);
        var matched = hit && float.IsFinite(authoredY)
            && Mathf.Abs(authoredY - probe.Surface.Y) <= 0.035f
            && Mathf.Abs(floor.Position.Y - authoredY) <= 0.035f
            && floor.Normal.Y >= 0.7f;
        GD.Print($"LANTERN_CANAL_FLOOR name={probe.Name} valid={matched} authored={authoredY:0.000} expected={probe.Surface.Y:0.000} physics={(hit ? floor.Position.Y : float.NaN):0.000} normal={(hit ? floor.Normal.Y : 0.0f):0.000}");
        return matched;
    }

    private float FindLanternCanalVisibleFloor(LanternCanalFloorProbe probe)
    {
        var source = FindLanternCanalNode(_lanternCanalScene, probe.Source);
        if (source is null)
        {
            return float.NaN;
        }
        var meshes = new List<MeshInstance3D>();
        CollectLanternCanalMeshes(source, meshes);
        var highest = float.NegativeInfinity;
        foreach (var mesh in meshes)
        {
            if (!mesh.IsVisibleInTree() || mesh.Mesh is null)
            {
                continue;
            }
            // Barycentric XZ sampling measures the visible mesh without querying
            // the collision builder, so matching errors cannot mask one another.
            var faces = mesh.Mesh.GetFaces();
            var transform = mesh.GlobalTransform;
            for (var index = 0; index + 2 < faces.Length; index += 3)
            {
                var first = transform * faces[index];
                var second = transform * faces[index + 1];
                var third = transform * faces[index + 2];
                var denominator = (second.Z - third.Z) * (first.X - third.X)
                    + (third.X - second.X) * (first.Z - third.Z);
                if (Mathf.Abs(denominator) < 0.000001f)
                {
                    continue;
                }
                var firstWeight = ((second.Z - third.Z) * (probe.Surface.X - third.X)
                    + (third.X - second.X) * (probe.Surface.Z - third.Z)) / denominator;
                var secondWeight = ((third.Z - first.Z) * (probe.Surface.X - third.X)
                    + (first.X - third.X) * (probe.Surface.Z - third.Z)) / denominator;
                var thirdWeight = 1.0f - firstWeight - secondWeight;
                if (firstWeight < -0.00001f || secondWeight < -0.00001f || thirdWeight < -0.00001f)
                {
                    continue;
                }
                var y = firstWeight * first.Y + secondWeight * second.Y + thirdWeight * third.Y;
                if (Mathf.Abs(y - probe.Surface.Y) <= 0.35f)
                {
                    highest = Mathf.Max(highest, y);
                }
            }
        }
        return highest;
    }

    private static void CollectLanternCanalMeshes(Node root, List<MeshInstance3D> meshes)
    {
        if (root is MeshInstance3D mesh)
        {
            meshes.Add(mesh);
        }
        foreach (var child in root.GetChildren())
        {
            CollectLanternCanalMeshes(child, meshes);
        }
    }

    private async Task<bool> SettleLanternCanalPlayer(Vector3 surface, float maximumFeetClearance = 0.08f)
    {
        _player.ProcessMode = ProcessModeEnum.Inherit;
        _player.UiLocked = false;
        _player.GlobalPosition = surface + Vector3.Up * 0.45f;
        _player.Velocity = Vector3.Zero;
        _player.TrySetStance(PlayerStance.Standing);
        _player.FaceWorldPointForDiagnostics(surface + Vector3.Forward * 25.0f);
        for (var frame = 0; frame < 90; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            if (frame >= 20 && _player.IsOnFloor() && Mathf.Abs(_player.Velocity.Y) < 0.05f)
            {
                break;
            }
        }
        await WaitFrames(3);
        var feetError = _player.GlobalPosition.Y - surface.Y;
        var eyeHeight = _player.DiagnosticCameraPosition.Y - surface.Y;
        var valid = _player.IsOnFloor() && feetError >= -0.025f && feetError <= maximumFeetClearance
            && eyeHeight >= 1.45f && eyeHeight <= 1.72f;
        GD.Print($"LANTERN_CANAL_SETTLE valid={valid} surface={surface} feet={_player.GlobalPosition} feet_error={feetError:0.000} eye={_player.DiagnosticCameraPosition.Y:0.000} eye_height={eyeHeight:0.000} on_floor={_player.IsOnFloor()}");
        return valid;
    }

    private bool ValidateLanternCanalLighting(out string summary)
    {
        var importedDirectionalCount = 0;
        var enabledImportedDirectionals = 0;
        var shadowCasterCount = 0;
        var geometryCount = 0;
        var localLightsReady = true;
        InspectLanternCanalLighting(_lanternCanalScene!, ref importedDirectionalCount,
            ref enabledImportedDirectionals, ref shadowCasterCount, ref geometryCount, ref localLightsReady);
        ApplyTimeOfDay(DeploymentTimeOfDay.Day);
        var dayReady = _environmentRef.TonemapExposure <= 0.95f
            && _environmentRef.AmbientLightEnergy <= 0.55f
            && _sunLight.LightEnergy <= 1.0f && _fillLight.LightEnergy <= 0.12f;
        ApplyTimeOfDay(DeploymentTimeOfDay.Night);
        var nightReady = _sunLight.LightEnergy is >= 0.15f and <= 0.30f
            && _environmentRef.TonemapExposure <= 0.95f
            && _environmentRef.AmbientLightSource == Godot.Environment.AmbientSource.Color
            && _environmentRef.AmbientLightEnergy is >= 0.25f and <= 0.40f;
        ApplyTimeOfDay(DeploymentTimeOfDay.Day);
        summary = $"imported_suns={enabledImportedDirectionals}/{importedDirectionalCount},shadows={shadowCasterCount}/{geometryCount},local={localLightsReady},day={dayReady},night={nightReady}";
        return importedDirectionalCount >= 1 && enabledImportedDirectionals == 0
            && geometryCount > 0 && shadowCasterCount == geometryCount
            && localLightsReady && dayReady && nightReady;
    }

    private static void InspectLanternCanalLighting(Node root,
        ref int directionalCount, ref int enabledDirectionals, ref int shadowCasters,
        ref int geometries, ref bool localLightsReady)
    {
        if (root is DirectionalLight3D directional)
        {
            directionalCount++;
            enabledDirectionals += directional.Visible && directional.LightEnergy > 0.0f ? 1 : 0;
        }
        if (root is OmniLight3D local)
        {
            localLightsReady &= local.LightEnergy <= 2.2f && local.OmniRange <= 8.0f
                && local.DistanceFadeEnabled;
        }
        if (root is GeometryInstance3D geometry)
        {
            geometries++;
            shadowCasters += geometry.CastShadow == GeometryInstance3D.ShadowCastingSetting.On ? 1 : 0;
        }
        foreach (var child in root.GetChildren())
        {
            InspectLanternCanalLighting(child, ref directionalCount, ref enabledDirectionals,
                ref shadowCasters, ref geometries, ref localLightsReady);
        }
    }

    private async void CaptureLanternCanal()
    {
        var valid = false;
        try
        {
            PrepareLanternCanalDiagnosticActors();
            ApplyQuality(2);
            ApplyTimeOfDay(DeploymentTimeOfDay.Day);
            _hud.Visible = false;
            var camera = new Camera3D { Name = "LanternCanalReviewCamera", Fov = 76.0f, Far = 420.0f };
            AddChild(camera);
            var street = new Vector3(-12.0f, 1.12f, 76.0f);
            valid = await SettleLanternCanalPlayer(street);
            camera.GlobalPosition = _player.DiagnosticCameraPosition;
            camera.LookAt(new Vector3(-12.0f, 2.45f, 35.0f), Vector3.Up);
            camera.MakeCurrent();
            await WaitFrames(24);
            SaveViewportImage("res://lantern_canal_validation.png");
            PrintLanternCanalRenderingSnapshot("lantern_canal_validation.png");

            _player.GrantFireablePrimaryForDiagnostics(WeaponCatalog.Build(WeaponPlatform.M4A1, 1));
            _player.FaceWorldPointForDiagnostics(new Vector3(-12.0f, 1.12f, 35.0f));
            _player.GetNode<Camera3D>("Head/CombatCamera").MakeCurrent();
            await WaitFrames(12);
            _player.ProcessMode = ProcessModeEnum.Disabled;
            _player.AimCameraAtWorldPointForDiagnostics(new Vector3(-12.0f, 2.35f, 35.0f));
            _hud.Visible = true;
            await WaitFrames(8);
            SaveViewportImage("res://lantern_canal_player_day_validation.png");
            PrintLanternCanalRenderingSnapshot("lantern_canal_player_day_validation.png");
            _hud.Visible = false;

            ApplyTimeOfDay(DeploymentTimeOfDay.Dusk);
            camera.MakeCurrent();
            await WaitFrames(20);
            SaveViewportImage("res://lantern_canal_dusk_validation.png");
            PrintLanternCanalRenderingSnapshot("lantern_canal_dusk_validation.png");
            ApplyTimeOfDay(DeploymentTimeOfDay.Night);
            await WaitFrames(20);
            SaveViewportImage("res://lantern_canal_night_validation.png");
            PrintLanternCanalRenderingSnapshot("lantern_canal_night_validation.png");
            ApplyTimeOfDay(DeploymentTimeOfDay.Day);
            valid &= await SettleLanternCanalPlayer(new Vector3(0.0f, 2.9f, 81.0f), 0.14f);
            camera.GlobalPosition = _player.DiagnosticCameraPosition;
            camera.LookAt(new Vector3(0.0f, 2.8f, 50.0f), Vector3.Up);
            await WaitFrames(20);
            SaveViewportImage("res://lantern_canal_bridge_validation.png");
            PrintLanternCanalRenderingSnapshot("lantern_canal_bridge_validation.png");

            valid &= await SettleLanternCanalPlayer(new Vector3(12.0f, 1.12f, 66.0f));
            camera.GlobalPosition = _player.DiagnosticCameraPosition;
            camera.LookAt(new Vector3(22.0f, 3.0f, 67.0f), Vector3.Up);
            await WaitFrames(20);
            SaveViewportImage("res://lantern_canal_workshop_validation.png");
            PrintLanternCanalRenderingSnapshot("lantern_canal_workshop_validation.png");
            valid &= await SettleLanternCanalPlayer(new Vector3(18.0f, 1.255f, 67.0f));
            camera.GlobalPosition = _player.DiagnosticCameraPosition;
            camera.LookAt(new Vector3(24.0f, 2.8f, 69.0f), Vector3.Up);
            await WaitFrames(20);
            SaveViewportImage("res://lantern_canal_interior_validation.png");
            PrintLanternCanalRenderingSnapshot("lantern_canal_interior_validation.png");
            GD.Print($"LANTERN_CANAL_CAPTURE_CHECK valid={valid} grounded_cameras={valid} paths=lantern_canal_validation.png,lantern_canal_player_day_validation.png,lantern_canal_dusk_validation.png,lantern_canal_night_validation.png,lantern_canal_bridge_validation.png,lantern_canal_workshop_validation.png,lantern_canal_interior_validation.png");
        }
        catch (Exception exception)
        {
            valid = false;
            GD.PrintErr($"LANTERN_CANAL_CAPTURE_CHECK valid=False error={exception.Message}");
        }
        GD.Print($"LANTERN_CANAL_CAPTURE_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }

    private static void PrintLanternCanalRenderingSnapshot(string image)
    {
        var drawCalls = Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame);
        var objects = Performance.GetMonitor(Performance.Monitor.RenderTotalObjectsInFrame);
        var primitives = Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame);
        var videoMemoryMb = Performance.GetMonitor(Performance.Monitor.RenderVideoMemUsed) / (1024.0 * 1024.0);
        var textureMemoryMb = Performance.GetMonitor(Performance.Monitor.RenderTextureMemUsed) / (1024.0 * 1024.0);
        GD.Print($"LANTERN_CANAL_RENDER image={image} draw_calls={drawCalls:0} objects={objects:0} primitives={primitives:0} video_mb={videoMemoryMb:0.0} texture_mb={textureMemoryMb:0.0}");
    }

    private static int CountLanternCanalMeshes(Node? root)
    {
        if (root is null || !GodotObject.IsInstanceValid(root))
        {
            return 0;
        }
        var count = 0;
        foreach (var child in root.GetChildren())
        {
            if (child is MeshInstance3D)
            {
                count++;
            }
            if (child is Node node)
            {
                count += CountLanternCanalMeshes(node);
            }
        }
        return count;
    }

    private static int CountVisibleLanternCanalMeshes(Node? root)
    {
        if (root is null || !GodotObject.IsInstanceValid(root))
        {
            return 0;
        }
        var count = 0;
        foreach (var child in root.GetChildren())
        {
            if (child is MeshInstance3D mesh && mesh.Visible && mesh.Mesh is not null)
            {
                count++;
            }
            if (child is Node node)
            {
                count += CountVisibleLanternCanalMeshes(node);
            }
        }
        return count;
    }

    private static int CountLanternCanalLights(Node? root)
    {
        if (root is null || !GodotObject.IsInstanceValid(root))
        {
            return 0;
        }
        var count = 0;
        foreach (var child in root.GetChildren())
        {
            if (child is Light3D)
            {
                count++;
            }
            if (child is Node node)
            {
                count += CountLanternCanalLights(node);
            }
        }
        return count;
    }

    private static Node? FindLanternCanalNode(Node? root, string requiredName)
    {
        if (root is null || !GodotObject.IsInstanceValid(root))
        {
            return null;
        }
        foreach (var child in root.GetChildren())
        {
            if (child.Name.ToString().Equals(requiredName, StringComparison.Ordinal))
            {
                return child;
            }
            if (child is Node childNode && FindLanternCanalNode(childNode, requiredName) is Node match)
            {
                return match;
            }
        }
        return null;
    }
}
