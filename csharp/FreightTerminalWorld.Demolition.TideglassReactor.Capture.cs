using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private async void CaptureTideglassReactor()
    {
        _demolitionSelectedMapId = DemolitionMapCatalog.TideglassReactorId;
        EnsureDemolitionArenaBuilt();
        if (_demolitionArena is null)
        {
            GD.PushError("Tideglass Reactor is unavailable for capture.");
            GetTree().Quit(2);
            return;
        }

        GetTree().Paused = false;
        DisableActorsForSurvivalDiagnostics();
        _levelRoot.Visible = false;
        _operationsOfficeScene.Visible = false;
        _demolitionArena.SetActive(true);
        _hud.Visible = false;
        _player.Visible = false;
        _player.ProcessMode = ProcessModeEnum.Disabled;
        if (IsInstanceValid(_worldBoss))
        {
            _worldBoss!.Visible = false;
        }
        if (IsInstanceValid(_aircraft))
        {
            _aircraft!.Visible = false;
            _aircraft.SetPhysicsProcess(false);
        }

        var layout = _demolitionArena.Layout;
        var focusModelNames = new[]
        {
            "NorthGuildHall",
            "NorthBrickOffices",
            "NorthCustomsHouse",
            "SouthGatehouse",
            "NorthLoadingBay",
            "SouthUtilityOffice",
            "CentralServiceHall",
            "WestWindowHall",
            "ConstructionTurbineWorkshop",
            "ReactorAnnex",
            "EastShiftOffice",
            "EastInspectionOffice",
            "MidCompressorHouse",
            "ReactorBoilerWorkshop",
            "EastSwitchgearHall",
            "MidCrewCanteen",
            "CivicUtilityOffice",
            "CivicPumpHouse",
            "SouthGlassworksOffice",
            "MidControlRoom",
            "EastTransformerWorks",
            "CivicCoolingServiceHall",
            "ReactorMaintenanceDepot",
            "WestFoundryWarehouse",
            "WestFoundryInspectionAnnex",
            "NorthFreightOffice",
            "EastOperationsOffice",
            "SouthWorksOffice",
            "WestGateOffice",
            "SouthTransitOffice",
            "MidDispatchOffice",
            "SouthRegistryHouse",
            "SightBlockEastApproachOffices",
            "MidTelegraphHouse",
            "DefenderServiceBlock",
            "SouthwestWatchHouse",
            "DefenderArchiveBlock",
            "NorthFoundryTenement",
            "SightBlockConstructionSiteOffice",
            "ConstructionBuilding",
            "OldBrickReactorHall",
            "OrangeArchGateway",
            "CivicElevatedWalkway",
            "EastPerimeterSecurityGate",
            "WestPerimeterServiceGate"
        };
        foreach (var modelName in focusModelNames)
        {
            var body = _demolitionArena.Root.GetNodeOrNull<StaticBody3D>(modelName);
            var model = body?.GetNodeOrNull<Node3D>("Model")
                ?? _demolitionArena.Root.GetNodeOrNull<Node3D>("DemolitionAuthoredDressing")
                    ?.GetNodeOrNull<Node3D>(modelName);
            var minimum = Vector3.Zero;
            var maximum = Vector3.Zero;
            var hasBounds = IsInstanceValid(model)
                && TideglassTryGetBounds(model!, null, out minimum, out maximum);
            var meshes = IsInstanceValid(model)
                ? model!.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>().ToArray()
                : System.Array.Empty<MeshInstance3D>();
            var visibleMeshes = meshes.Count(mesh => mesh.Visible && mesh.IsVisibleInTree());
            var meshLayers = string.Join('|', meshes.Select(mesh => $"{mesh.Name}:{mesh.Layers}"));
            GD.Print(
                $"TIDEGLASS_CAPTURE_MODEL name={modelName} "
                + $"body_global={body?.GlobalPosition} body_local={body?.Position} "
                + $"body_rotation={body?.RotationDegrees} "
                + $"visible={model?.Visible} bounds={hasBounds} "
                + $"meshes={meshes.Length} visible_meshes={visibleMeshes} layers={meshLayers} "
                + $"minimum={(hasBounds ? minimum : Vector3.Zero)} "
                + $"maximum={(hasBounds ? maximum : Vector3.Zero)}");
        }
        var camera = new Camera3D
        {
            Name = "TideglassReactorCaptureCamera",
            Fov = 68.0f,
            Near = 0.05f,
            Far = 400.0f,
            PhysicsInterpolationMode = PhysicsInterpolationModeEnum.Off
        };
        AddChild(camera);
        camera.MakeCurrent();
        const float playerEyeHeight = 1.57f;
        const int expectedCaptureFrameCount = 54;
        const int expectedFocusedCaptureCount = 45;
        var captures = new[]
        {
            (Path: "res://tideglass_reactor_overview.png", Position: new Vector3(0.0f, 90.0f, 80.0f), Target: new Vector3(0.0f, 0.0f, -3.0f)),
            (Path: "res://tideglass_reactor_site_a.png", Position: new Vector3(-18.0f, playerEyeHeight, 24.0f), Target: new Vector3(-40.0f, 2.4f, 24.0f)),
            (Path: "res://tideglass_reactor_site_b.png", Position: new Vector3(55.0f, playerEyeHeight, -35.0f), Target: new Vector3(42.0f, 2.4f, -25.0f)),
            (Path: "res://tideglass_reactor_attacker_spawn.png", Position: new Vector3(52.0f, playerEyeHeight, 48.0f), Target: new Vector3(40.0f, 2.5f, 36.0f)),
            (Path: "res://tideglass_reactor_defender_spawn.png", Position: new Vector3(-52.0f, playerEyeHeight, -48.0f), Target: new Vector3(-34.0f, 3.0f, -34.0f)),
            (Path: "res://tideglass_reactor_route_a.png", Position: new Vector3(-12.0f, playerEyeHeight, 42.0f), Target: new Vector3(-18.0f, 2.5f, 24.0f)),
            (Path: "res://tideglass_reactor_route_b.png", Position: new Vector3(65.0f, playerEyeHeight, 43.0f), Target: new Vector3(61.0f, 2.5f, 18.0f)),
            (Path: "res://tideglass_reactor_route_mid.png", Position: new Vector3(36.0f, playerEyeHeight, 40.0f), Target: new Vector3(27.0f, 2.5f, 22.0f)),
            (Path: "res://tideglass_reactor_mid.png", Position: new Vector3(8.0f, playerEyeHeight, 14.0f), Target: new Vector3(-3.0f, 2.5f, -8.0f)),
            (Path: "res://tideglass_reactor_north_guild.png", Position: new Vector3(-45.0f, playerEyeHeight, -42.0f), Target: new Vector3(-28.6f, 4.5f, -48.3f)),
            (Path: "res://tideglass_reactor_north_offices.png", Position: new Vector3(-7.0f, playerEyeHeight, -30.0f), Target: new Vector3(-7.0f, 5.4f, -50.65f)),
            (Path: "res://tideglass_reactor_north_customs.png", Position: new Vector3(7.0f, playerEyeHeight, -37.0f), Target: new Vector3(23.0f, 5.2f, -50.25f)),
            (Path: "res://tideglass_reactor_south_gatehouse.png", Position: new Vector3(-20.0f, playerEyeHeight, 49.0f), Target: new Vector3(-39.5f, 5.0f, 48.22f)),
            (Path: "res://tideglass_reactor_north_loading_bay.png", Position: new Vector3(63.0f, playerEyeHeight, -49.0f), Target: new Vector3(49.0f, 2.2f, -49.2f)),
            (Path: "res://tideglass_reactor_south_utility_office.png", Position: new Vector3(-43.0f, playerEyeHeight, 31.0f), Target: new Vector3(-38.0f, 2.2f, 19.0f)),
            (Path: "res://tideglass_reactor_central_service_hall.png", Position: new Vector3(8.0f, playerEyeHeight, -10.0f), Target: new Vector3(-3.0f, 2.5f, -0.8f)),
            (Path: "res://tideglass_reactor_west_window_hall.png", Position: new Vector3(-42.0f, playerEyeHeight, 10.0f), Target: new Vector3(-54.0f, 4.0f, -3.5f)),
            (Path: "res://tideglass_reactor_construction_turbine_workshop.png", Position: new Vector3(-18.0f, playerEyeHeight, 27.0f), Target: new Vector3(-31.0f, 3.2f, 16.0f)),
            (Path: "res://tideglass_reactor_reactor_annex.png", Position: new Vector3(25.0f, playerEyeHeight, -35.0f), Target: new Vector3(31.0f, 2.3f, -24.85f)),
            (Path: "res://tideglass_reactor_east_shift_office.png", Position: new Vector3(66.0f, playerEyeHeight, 32.0f), Target: new Vector3(57.0f, 2.0f, 25.0f)),
            (Path: "res://tideglass_reactor_east_inspection_office.png", Position: new Vector3(65.0f, playerEyeHeight, 7.0f), Target: new Vector3(56.0f, 2.0f, 18.3f)),
            (Path: "res://tideglass_reactor_mid_compressor_house.png", Position: new Vector3(23.0f, playerEyeHeight, 11.0f), Target: new Vector3(18.5f, 3.3f, -4.5f)),
            (Path: "res://tideglass_reactor_reactor_boiler_workshop.png", Position: new Vector3(-7.0f, playerEyeHeight, -31.0f), Target: new Vector3(10.0f, 3.5f, -19.3f)),
            (Path: "res://tideglass_reactor_east_switchgear_hall.png", Position: new Vector3(38.0f, playerEyeHeight, 47.0f), Target: new Vector3(37.0f, 2.5f, 31.0f)),
            (Path: "res://tideglass_reactor_mid_crew_canteen.png", Position: new Vector3(24.0f, playerEyeHeight, 12.0f), Target: new Vector3(0.0f, 3.8f, 12.25f)),
            (Path: "res://tideglass_reactor_civic_utility_office.png", Position: new Vector3(-23.0f, playerEyeHeight, 26.0f), Target: new Vector3(-12.5f, 2.4f, 23.55f)),
            (Path: "res://tideglass_reactor_civic_pump_house.png", Position: new Vector3(-5.0f, playerEyeHeight, -28.0f), Target: new Vector3(-18.5f, 3.0f, -23.5f)),
            (Path: "res://tideglass_reactor_south_glassworks_office.png", Position: new Vector3(-7.0f, playerEyeHeight, 46.0f), Target: new Vector3(-6.0f, 2.3f, 34.5f)),
            (Path: "res://tideglass_reactor_mid_control_room.png", Position: new Vector3(-6.0f, playerEyeHeight, -10.0f), Target: new Vector3(-15.4f, 3.8f, 3.0f)),
            (Path: "res://tideglass_reactor_east_transformer_works.png", Position: new Vector3(31.0f, playerEyeHeight, 20.0f), Target: new Vector3(36.0f, 3.2f, -2.2f)),
            (Path: "res://tideglass_reactor_civic_cooling_service_hall.png", Position: new Vector3(39.0f, playerEyeHeight, 22.0f), Target: new Vector3(16.0f, 3.8f, 26.0f)),
            (Path: "res://tideglass_reactor_reactor_maintenance_depot.png", Position: new Vector3(60.0f, playerEyeHeight, -28.0f), Target: new Vector3(39.0f, 4.7f, -40.4f)),
            (Path: "res://tideglass_reactor_west_foundry_warehouse.png", Position: new Vector3(-31.0f, playerEyeHeight, -12.0f), Target: new Vector3(-31.6f, 3.5f, 4.9f)),
            (Path: "res://tideglass_reactor_west_foundry_inspection_annex.png", Position: new Vector3(-50.0f, playerEyeHeight, 10.0f), Target: new Vector3(-44.54f, 2.4f, 0.0f)),
            (Path: "res://tideglass_reactor_north_freight_office.png", Position: new Vector3(20.0f, playerEyeHeight, -17.0f), Target: new Vector3(19.2f, 2.5f, -32.4f)),
            (Path: "res://tideglass_reactor_east_operations_office.png", Position: new Vector3(58.0f, playerEyeHeight, 53.0f), Target: new Vector3(58.2f, 3.2f, 37.8f)),
            (Path: "res://tideglass_reactor_south_works_office.png", Position: new Vector3(-20.0f, playerEyeHeight, 28.0f), Target: new Vector3(-14.0f, 2.6f, 16.0f)),
            (Path: "res://tideglass_reactor_west_gate_office.png", Position: new Vector3(-62.0f, playerEyeHeight, 53.0f), Target: new Vector3(-55.0f, 3.0f, 42.5f)),
            (Path: "res://tideglass_reactor_south_transit_office.png", Position: new Vector3(23.0f, playerEyeHeight, 53.0f), Target: new Vector3(10.0f, 2.8f, 42.5f)),
            (Path: "res://tideglass_reactor_mid_dispatch_office.png", Position: new Vector3(8.0f, playerEyeHeight, -7.0f), Target: new Vector3(-1.2f, 2.9f, -17.0f)),
            (Path: "res://tideglass_reactor_south_registry.png", Position: new Vector3(-12.0f, playerEyeHeight, 47.0f), Target: new Vector3(7.0f, 4.5f, 51.0f)),
            (Path: "res://tideglass_reactor_east_approach_offices.png", Position: new Vector3(51.0f, playerEyeHeight, 42.0f), Target: new Vector3(48.0f, 4.8f, 24.0f)),
            (Path: "res://tideglass_reactor_mid_telegraph_house.png", Position: new Vector3(-2.0f, playerEyeHeight, -30.0f), Target: new Vector3(-21.0f, 7.5f, -11.8f)),
            (Path: "res://tideglass_reactor_defender_service_block.png", Position: new Vector3(-40.0f, playerEyeHeight, -34.0f), Target: new Vector3(-61.1f, 8.6f, -29.5f)),
            (Path: "res://tideglass_reactor_southwest_watch_house.png", Position: new Vector3(-23.0f, playerEyeHeight, 12.0f), Target: new Vector3(-25.9f, 6.5f, 34.2f)),
            (Path: "res://tideglass_reactor_defender_archive_block.png", Position: new Vector3(-44.0f, playerEyeHeight, 14.0f), Target: new Vector3(-42.0f, 12.5f, -14.7f)),
            (Path: "res://tideglass_reactor_north_foundry_tenement.png", Position: new Vector3(4.0f, playerEyeHeight, -22.0f), Target: new Vector3(-17.5f, 8.0f, -31.2f)),
            (Path: "res://tideglass_reactor_construction_site_office.png", Position: new Vector3(38.0f, playerEyeHeight, 54.0f), Target: new Vector3(24.5f, 2.8f, 42.6f)),
            (Path: "res://tideglass_reactor_construction_building.png", Position: new Vector3(-66.0f, playerEyeHeight, 54.0f), Target: new Vector3(-55.0f, 24.0f, 22.0f)),
            (Path: "res://tideglass_reactor_old_brick_reactor_hall.png", Position: new Vector3(66.0f, playerEyeHeight, 14.0f), Target: new Vector3(58.0f, 11.0f, -13.0f)),
            (Path: "res://tideglass_reactor_arch_gateway.png", Position: new Vector3(0.0f, playerEyeHeight, -23.0f), Target: new Vector3(0.0f, 2.0f, -31.5f)),
            (Path: "res://tideglass_reactor_civic_walkway.png", Position: new Vector3(-18.5f, playerEyeHeight, 28.0f), Target: new Vector3(0.0f, 2.0f, 26.5f)),
            (Path: "res://tideglass_reactor_east_security_gate.png", Position: new Vector3(58.0f, playerEyeHeight, -27.57f), Target: new Vector3(67.5f, 1.6f, -27.57f)),
            (Path: "res://tideglass_reactor_west_service_gate.png", Position: new Vector3(-58.0f, playerEyeHeight, -44.29f), Target: new Vector3(-67.5f, 1.6f, -44.29f))
        };
        var focusedCaptures = new Dictionary<string, string>
        {
            ["res://tideglass_reactor_north_guild.png"] = "NorthGuildHall",
            ["res://tideglass_reactor_north_offices.png"] = "NorthBrickOffices",
            ["res://tideglass_reactor_north_customs.png"] = "NorthCustomsHouse",
            ["res://tideglass_reactor_south_gatehouse.png"] = "SouthGatehouse",
            ["res://tideglass_reactor_north_loading_bay.png"] = "NorthLoadingBay",
            ["res://tideglass_reactor_south_utility_office.png"] = "SouthUtilityOffice",
            ["res://tideglass_reactor_central_service_hall.png"] = "CentralServiceHall",
            ["res://tideglass_reactor_west_window_hall.png"] = "WestWindowHall",
            ["res://tideglass_reactor_construction_turbine_workshop.png"] = "ConstructionTurbineWorkshop",
            ["res://tideglass_reactor_reactor_annex.png"] = "ReactorAnnex",
            ["res://tideglass_reactor_east_shift_office.png"] = "EastShiftOffice",
            ["res://tideglass_reactor_east_inspection_office.png"] = "EastInspectionOffice",
            ["res://tideglass_reactor_mid_compressor_house.png"] = "MidCompressorHouse",
            ["res://tideglass_reactor_reactor_boiler_workshop.png"] = "ReactorBoilerWorkshop",
            ["res://tideglass_reactor_east_switchgear_hall.png"] = "EastSwitchgearHall",
            ["res://tideglass_reactor_mid_crew_canteen.png"] = "MidCrewCanteen",
            ["res://tideglass_reactor_civic_utility_office.png"] = "CivicUtilityOffice",
            ["res://tideglass_reactor_civic_pump_house.png"] = "CivicPumpHouse",
            ["res://tideglass_reactor_south_glassworks_office.png"] = "SouthGlassworksOffice",
            ["res://tideglass_reactor_mid_control_room.png"] = "MidControlRoom",
            ["res://tideglass_reactor_east_transformer_works.png"] = "EastTransformerWorks",
            ["res://tideglass_reactor_civic_cooling_service_hall.png"] = "CivicCoolingServiceHall",
            ["res://tideglass_reactor_reactor_maintenance_depot.png"] = "ReactorMaintenanceDepot",
            ["res://tideglass_reactor_west_foundry_warehouse.png"] = "WestFoundryWarehouse",
            ["res://tideglass_reactor_west_foundry_inspection_annex.png"] = "WestFoundryInspectionAnnex",
            ["res://tideglass_reactor_north_freight_office.png"] = "NorthFreightOffice",
            ["res://tideglass_reactor_east_operations_office.png"] = "EastOperationsOffice",
            ["res://tideglass_reactor_south_works_office.png"] = "SouthWorksOffice",
            ["res://tideglass_reactor_west_gate_office.png"] = "WestGateOffice",
            ["res://tideglass_reactor_south_transit_office.png"] = "SouthTransitOffice",
            ["res://tideglass_reactor_mid_dispatch_office.png"] = "MidDispatchOffice",
            ["res://tideglass_reactor_south_registry.png"] = "SouthRegistryHouse",
            ["res://tideglass_reactor_east_approach_offices.png"] = "SightBlockEastApproachOffices",
            ["res://tideglass_reactor_mid_telegraph_house.png"] = "MidTelegraphHouse",
            ["res://tideglass_reactor_defender_service_block.png"] = "DefenderServiceBlock",
            ["res://tideglass_reactor_southwest_watch_house.png"] = "SouthwestWatchHouse",
            ["res://tideglass_reactor_defender_archive_block.png"] = "DefenderArchiveBlock",
            ["res://tideglass_reactor_north_foundry_tenement.png"] = "NorthFoundryTenement",
            ["res://tideglass_reactor_construction_site_office.png"] = "SightBlockConstructionSiteOffice",
            ["res://tideglass_reactor_construction_building.png"] = "ConstructionBuilding",
            ["res://tideglass_reactor_old_brick_reactor_hall.png"] = "OldBrickReactorHall",
            ["res://tideglass_reactor_arch_gateway.png"] = "OrangeArchGateway",
            ["res://tideglass_reactor_civic_walkway.png"] = "CivicElevatedWalkway",
            ["res://tideglass_reactor_east_security_gate.png"] = "EastPerimeterSecurityGate",
            ["res://tideglass_reactor_west_service_gate.png"] = "WestPerimeterServiceGate"
        };
        var frameValidity = new Dictionary<string, bool>();
        foreach (var capture in captures)
        {
            camera.Fov = capture.Path == "res://tideglass_reactor_construction_building.png"
                ? 80.0f
                : 68.0f;
            camera.GlobalPosition = layout.Origin + capture.Position;
            camera.LookAt(layout.Origin + capture.Target, Vector3.Up);
            await WaitFrames(18);
            var image = GetViewport().GetTexture().GetImage();
            var focused = focusedCaptures.TryGetValue(capture.Path, out var focusName);
            var focus = focused
                ? _demolitionArena.Root.FindChild(focusName!, true, false) as Node3D
                : null;
            var boundsFit = !focused || IsInstanceValid(focus)
                && TideglassCaptureBoundsFit(camera, focus!, 18.0f);
            var unobstructed = !focused || IsInstanceValid(focus)
                && TideglassCaptureFocusUnobstructed(camera, focus!, _demolitionArena.Root);
            var saved = TideglassSaveCaptureFrame(capture.Path, image);
            frameValidity[capture.Path] = boundsFit && unobstructed && saved;
            GD.Print(
                $"TIDEGLASS_CAPTURE_FRAME path={capture.Path} saved={saved} "
                + $"bounds={boundsFit} unobstructed={unobstructed}");
        }

        var cameraCurrent = GetViewport().GetCamera3D() == camera;
        var valid = cameraCurrent
            && captures.Length == expectedCaptureFrameCount
            && focusModelNames.Length == expectedFocusedCaptureCount
            && focusedCaptures.Count == expectedFocusedCaptureCount
            && frameValidity.Count == captures.Length
            && frameValidity.Values.All(frame => frame);
        var invalidFrames = string.Join('|', frameValidity
            .Where(pair => !pair.Value)
            .Select(pair => pair.Key));
        GD.Print(
            $"TIDEGLASS_REACTOR_CAPTURE valid={valid} frames={captures.Length} "
            + $"invalid={invalidFrames} paths={string.Join('|', captures.Select(capture => capture.Path))}");
        _demolitionArena.SetActive(false);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        var root = _demolitionArena.Root;
        _demolitionArena = null;
        _demolitionRoutePlanner = null;
        _demolitionSites.Clear();
        root.QueueFree();
        camera.QueueFree();
        await WaitFrames(3);
        GetTree().Quit(valid ? 0 : 2);
    }

    private static bool TideglassSaveCaptureFrame(string path, Image? image)
    {
        if (image is null || image.IsEmpty() || image.GetWidth() < 640 || image.GetHeight() < 360)
        {
            return false;
        }

        var minimumLuminance = 1.0f;
        var maximumLuminance = 0.0f;
        var stepX = Mathf.Max(1, image.GetWidth() / 24);
        var stepY = Mathf.Max(1, image.GetHeight() / 14);
        for (var y = stepY / 2; y < image.GetHeight(); y += stepY)
        {
            for (var x = stepX / 2; x < image.GetWidth(); x += stepX)
            {
                var color = image.GetPixel(x, y);
                var luminance = (color.R + color.G + color.B) / 3.0f;
                minimumLuminance = Mathf.Min(minimumLuminance, luminance);
                maximumLuminance = Mathf.Max(maximumLuminance, luminance);
            }
        }

        var saveResult = image.SavePng(path);
        var absolutePath = ProjectSettings.GlobalizePath(path);
        return saveResult == Error.Ok
            && System.IO.File.Exists(absolutePath)
            && new System.IO.FileInfo(absolutePath).Length >= 10_000
            && maximumLuminance - minimumLuminance >= 0.08f;
    }

    private static bool TideglassCaptureBoundsFit(Camera3D camera, Node3D focus, float margin)
    {
        if (!TideglassTryGetBounds(focus, null, out var minimum, out var maximum))
        {
            return false;
        }

        var viewportSize = camera.GetViewport().GetVisibleRect().Size;
        for (var corner = 0; corner < 8; corner++)
        {
            var world = minimum + new Vector3(
                (corner & 1) == 0 ? 0.0f : maximum.X - minimum.X,
                (corner & 2) == 0 ? 0.0f : maximum.Y - minimum.Y,
                (corner & 4) == 0 ? 0.0f : maximum.Z - minimum.Z);
            if (camera.IsPositionBehind(world))
            {
                return false;
            }
            var screen = camera.UnprojectPosition(world);
            if (screen.X < margin
                || screen.Y < margin
                || screen.X > viewportSize.X - margin
                || screen.Y > viewportSize.Y - margin)
            {
                return false;
            }
        }
        return true;
    }

    private static bool TideglassCaptureFocusUnobstructed(
        Camera3D camera,
        Node3D focus,
        Node3D visualRoot)
    {
        if (!TideglassTryGetBounds(focus, null, out var minimum, out var maximum))
        {
            return false;
        }

        if (focus is StaticBody3D body)
        {
            var target = (minimum + maximum) * 0.5f;
            return PhysicsRaycast.TryHit(
                camera.GetWorld3D(),
                camera.GlobalPosition,
                target,
                1u,
                out var hit)
                && hit.Collider == body;
        }

        var focusMeshNodes = focus.FindChildren("*", "MeshInstance3D", true, false);
        using var focusMeshNodesBacking = focusMeshNodes.AsDisposable();
        var focusMeshes = focusMeshNodes
            .OfType<MeshInstance3D>()
            .Where(mesh => mesh.Mesh is not null && mesh.Visible && mesh.IsVisibleInTree())
            .ToArray();
        var candidateTargets = focusMeshes
            .SelectMany(mesh =>
            {
                var faces = mesh.Mesh!.GetFaces();
                return Enumerable.Range(0, faces.Length / 3)
                    .Select(face =>
                    {
                        var offset = face * 3;
                        return (mesh.ToGlobal(faces[offset])
                            + mesh.ToGlobal(faces[offset + 1])
                            + mesh.ToGlobal(faces[offset + 2])) / 3.0f;
                    });
            })
            .Where(target => !camera.IsPositionBehind(target))
            .OrderBy(target => camera.GlobalPosition.DistanceSquaredTo(target))
            .Take(64)
            .ToArray();

        foreach (var target in candidateTargets)
        {
            var direction = camera.GlobalPosition.DirectionTo(target);
            if (!TideglassFirstVisibleMeshOnSegment(
                    visualRoot,
                    camera.GlobalPosition + direction * 0.02f,
                    target + direction * 0.04f,
                    out var firstMesh))
            {
                continue;
            }
            if (firstMesh == focus || focus.IsAncestorOf(firstMesh))
            {
                return true;
            }
        }
        return false;
    }

    private static bool TideglassFirstVisibleMeshOnSegment(
        Node3D root,
        Vector3 from,
        Vector3 to,
        out MeshInstance3D? firstMesh)
    {
        firstMesh = null;
        var closestDistanceSquared = float.PositiveInfinity;
        var meshNodes = root.FindChildren("*", "MeshInstance3D", true, false);
        using var meshNodesBacking = meshNodes.AsDisposable();
        foreach (var mesh in meshNodes.OfType<MeshInstance3D>())
        {
            if (mesh.Mesh is null
                || !mesh.Visible
                || !mesh.IsVisibleInTree()
                || (mesh.Layers & 1u) == 0)
            {
                continue;
            }
            var faces = mesh.Mesh.GetFaces();
            for (var face = 0; face + 2 < faces.Length; face += 3)
            {
                using var intersection = Geometry3D.SegmentIntersectsTriangle(
                    from,
                    to,
                    mesh.ToGlobal(faces[face]),
                    mesh.ToGlobal(faces[face + 1]),
                    mesh.ToGlobal(faces[face + 2]));
                if (intersection.VariantType != Variant.Type.Vector3)
                {
                    continue;
                }
                var distanceSquared = from.DistanceSquaredTo(intersection.AsVector3());
                if (distanceSquared >= closestDistanceSquared)
                {
                    continue;
                }
                closestDistanceSquared = distanceSquared;
                firstMesh = mesh;
            }
        }
        return firstMesh is not null;
    }
}
