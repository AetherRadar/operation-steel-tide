using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private readonly record struct BazaarCaptureFrame(
        string Path,
        Vector3 Position,
        Vector3 Target,
        float Fov,
        bool StrictTopdown = false,
        bool RequireReadableInterior = false,
        bool RequireClearTarget = true,
        IReadOnlyList<Vector3>? RequiredClearTargets = null);

    private readonly record struct BazaarCaptureImageResult(
        bool Saved,
        float MinimumLuminance,
        float MaximumLuminance,
        float MeanLuminance,
        float LowerQuartileLuminance,
        float VeryDarkSampleFraction)
    {
        public float DynamicRange => MaximumLuminance - MinimumLuminance;

        public bool InteriorReadable => MeanLuminance >= 0.16f
            && LowerQuartileLuminance >= 0.09f
            && VeryDarkSampleFraction <= 0.15f;
    }

    private async void CaptureBazaarCrossing()
    {
        _demolitionSelectedMapId = DemolitionMapCatalog.BazaarCrossingId;
        EnsureDemolitionArenaBuilt();
        if (_demolitionArena is null)
        {
            GD.PushError("Bazaar Crossing is unavailable for capture.");
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
        var camera = new Camera3D
        {
            Name = "BazaarCrossingCaptureCamera",
            Fov = 68.0f,
            Near = 0.05f,
            Far = 420.0f,
            PhysicsInterpolationMode = PhysicsInterpolationModeEnum.Off
        };
        AddChild(camera);
        camera.MakeCurrent();

        const float eye = 1.57f;
        var frames = new[]
        {
            new BazaarCaptureFrame(
                "res://bazaar_crossing_topdown.png",
                new Vector3(0.0f, 120.0f, 0.0f),
                Vector3.Zero,
                64.0f,
                StrictTopdown: true),
            new BazaarCaptureFrame(
                "res://bazaar_crossing_oblique.png",
                new Vector3(0.0f, 86.0f, 94.0f),
                new Vector3(0.0f, 1.0f, -4.0f),
                62.0f,
                RequireClearTarget: false),
            new BazaarCaptureFrame(
                "res://bazaar_crossing_site_a.png",
                new Vector3(-46.0f, eye, -15.0f),
                new Vector3(-46.0f, 1.1f, -21.0f),
                68.0f,
                RequireReadableInterior: true),
            new BazaarCaptureFrame(
                "res://bazaar_crossing_a_rear_interior.png",
                new Vector3(-56.0f, eye, -26.0f),
                new Vector3(-56.0f, 1.2f, -18.0f),
                68.0f,
                RequireReadableInterior: true),
            new BazaarCaptureFrame(
                "res://bazaar_crossing_site_b.png",
                new Vector3(43.0f, eye, -10.0f),
                new Vector3(50.0f, 1.1f, -21.0f),
                68.0f,
                RequireReadableInterior: true),
            new BazaarCaptureFrame(
                "res://bazaar_crossing_b_stockroom.png",
                new Vector3(56.0f, eye, -24.0f),
                new Vector3(47.0f, 1.2f, -23.4f),
                68.0f,
                RequireReadableInterior: true),
            new BazaarCaptureFrame(
                "res://bazaar_crossing_mid_s_bend.png",
                new Vector3(0.0f, eye, 30.0f),
                new Vector3(1.0f, 1.4f, 19.0f),
                72.0f,
                RequireReadableInterior: true),
            new BazaarCaptureFrame(
                "res://bazaar_crossing_mid_north_connector.png",
                new Vector3(-5.0f, eye, -10.0f),
                new Vector3(8.0f, 1.4f, -21.0f),
                70.0f,
                RequireReadableInterior: true),
            new BazaarCaptureFrame(
                "res://bazaar_crossing_back_market_west.png",
                new Vector3(-28.0f, eye, -41.0f),
                new Vector3(-44.4f, 1.4f, -37.5f),
                70.0f,
                RequireReadableInterior: true),
            new BazaarCaptureFrame(
                "res://bazaar_crossing_back_market_east.png",
                new Vector3(28.0f, eye, -41.0f),
                new Vector3(36.0f, 1.4f, -37.5f),
                70.0f,
                RequireReadableInterior: true),
            new BazaarCaptureFrame(
                "res://bazaar_crossing_a_gallery.png",
                new Vector3(-56.0f, 3.6f + eye, -10.5f),
                new Vector3(-56.0f, 3.8f, -24.0f),
                68.0f,
                RequireReadableInterior: true),
            new BazaarCaptureFrame(
                "res://bazaar_crossing_mid_mezzanine.png",
                new Vector3(-6.0f, 3.2f + eye, 29.5f),
                new Vector3(-6.0f, 4.2f, 22.0f),
                68.0f,
                RequireReadableInterior: true),
            new BazaarCaptureFrame(
                "res://bazaar_crossing_b_balcony.png",
                new Vector3(56.0f, 3.4f + eye, -10.5f),
                new Vector3(56.0f, 3.6f, -24.0f),
                68.0f,
                RequireReadableInterior: true),
            new BazaarCaptureFrame(
                "res://bazaar_crossing_a_gallery_south_stair.png",
                new Vector3(-52.8f, eye, 8.0f),
                new Vector3(-56.0f, 2.9f, -4.0f),
                72.0f,
                RequireReadableInterior: true),
            new BazaarCaptureFrame(
                "res://bazaar_crossing_a_gallery_rear_stair.png",
                new Vector3(-40.5f, eye, -27.0f),
                new Vector3(-53.0f, 3.7f, -27.0f),
                66.0f,
                RequireReadableInterior: true),
            new BazaarCaptureFrame(
                "res://bazaar_crossing_mid_south_stair.png",
                new Vector3(-2.8f, eye, 47.0f),
                new Vector3(-6.0f, 2.7f, 35.0f),
                72.0f,
                RequireReadableInterior: true),
            new BazaarCaptureFrame(
                "res://bazaar_crossing_mid_spawn_stair_clearance.png",
                new Vector3(-9.3f, eye, 44.0f),
                new Vector3(-9.3f, 1.45f, 39.5f),
                76.0f,
                RequireReadableInterior: true),
            new BazaarCaptureFrame(
                "res://bazaar_crossing_mid_upper_connection.png",
                new Vector3(-6.0f, 3.2f + eye, 30.0f),
                new Vector3(-6.0f, 4.0f, 17.5f),
                70.0f,
                RequireReadableInterior: true),
            new BazaarCaptureFrame(
                "res://bazaar_crossing_mid_north_stair.png",
                new Vector3(-6.0f, eye, 6.0f),
                new Vector3(-6.0f, 3.3f, 17.0f),
                66.0f,
                RequireReadableInterior: true),
            new BazaarCaptureFrame(
                "res://bazaar_crossing_b_balcony_south_stair.png",
                new Vector3(52.8f, eye, 7.5f),
                new Vector3(56.0f, 2.8f, -5.0f),
                72.0f,
                RequireReadableInterior: true),
            new BazaarCaptureFrame(
                "res://bazaar_crossing_b_balcony_rear_stair.png",
                new Vector3(41.0f, eye, -27.0f),
                new Vector3(53.0f, 3.5f, -27.0f),
                66.0f,
                RequireReadableInterior: true),
            new BazaarCaptureFrame(
                "res://bazaar_crossing_attack_spawn.png",
                new Vector3(0.0f, 0.22f + eye, 49.0f),
                new Vector3(0.0f, 1.4f, 31.0f),
                70.0f),
            new BazaarCaptureFrame(
                "res://bazaar_crossing_defender_spawn.png",
                new Vector3(0.0f, 0.22f + eye, -52.0f),
                new Vector3(-6.0f, 1.4f, -44.6f),
                70.0f),
            new BazaarCaptureFrame(
                "res://bazaar_crossing_a_rear_three_portals.png",
                new Vector3(-46.0f, eye, -15.0f),
                new Vector3(-46.0f, eye, -24.2f),
                80.0f,
                RequireReadableInterior: true,
                RequiredClearTargets: new[]
                {
                    new Vector3(-56.0f, eye, -24.2f),
                    new Vector3(-46.0f, eye, -24.2f),
                    new Vector3(-38.0f, eye, -24.2f)
                })
        };

        var validity = new Dictionary<string, bool>();
        foreach (var frame in frames)
        {
            camera.Fov = frame.Fov;
            camera.GlobalPosition = layout.Origin + frame.Position;
            camera.LookAt(
                layout.Origin + frame.Target,
                frame.StrictTopdown ? Vector3.Forward : Vector3.Up);
            await WaitFrames(18);
            var image = GetViewport().GetTexture().GetImage();
            var target = layout.Origin + frame.Target;
            var framingBlocker = "none";
            var framingReady = frame.StrictTopdown
                ? BazaarTopdownContainsArena(camera, layout, 20.0f)
                : BazaarPerspectiveFrameReady(
                    GetWorld3D(),
                    camera,
                    target,
                    frame.RequireClearTarget,
                    20.0f,
                    out framingBlocker);
            var result = BazaarSaveCaptureFrame(frame.Path, image);
            var readabilityReady = !frame.RequireReadableInterior || result.InteriorReadable;
            var clearTargetsReady = BazaarCaptureClearTargetsReady(
                GetWorld3D(),
                camera.GlobalPosition,
                layout,
                frame.RequiredClearTargets,
                out var clearTargetBlockers);
            validity[frame.Path] = framingReady
                && result.Saved
                && readabilityReady
                && clearTargetsReady;
            GD.Print(
                $"BAZAAR_CAPTURE_FRAME path={frame.Path} saved={result.Saved} framing={framingReady} "
                + $"readable={readabilityReady} mean={result.MeanLuminance:0.000} "
                + $"p25={result.LowerQuartileLuminance:0.000} "
                + $"dark075={result.VeryDarkSampleFraction:0.000} range={result.DynamicRange:0.000} "
                + $"blocker={framingBlocker} clear_targets={clearTargetsReady} "
                + $"clear_target_blockers={clearTargetBlockers} "
                + $"position={camera.GlobalPosition} target={target}");
        }

        var valid = GetViewport().GetCamera3D() == camera
            && validity.Count == frames.Length
            && validity.Values.All(ready => ready);
        var invalid = string.Join('|', validity
            .Where(pair => !pair.Value)
            .Select(pair => pair.Key));
        GD.Print(
            $"BAZAAR_CROSSING_CAPTURE valid={valid} frames={frames.Length} invalid={invalid} "
            + $"paths={string.Join('|', frames.Select(frame => frame.Path))}");

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

    private static bool BazaarCaptureClearTargetsReady(
        World3D world,
        Vector3 cameraPosition,
        DemolitionArenaLayout layout,
        IReadOnlyList<Vector3>? localTargets,
        out string blockers)
    {
        if (localTargets is null || localTargets.Count == 0)
        {
            blockers = "none";
            return true;
        }

        var blockedTargets = new List<string>();
        foreach (var localTarget in localTargets)
        {
            if (!PhysicsRaycast.TryHit(
                    world,
                    cameraPosition,
                    layout.Origin + localTarget,
                    1u,
                    out var hit))
            {
                continue;
            }

            var collider = hit.Collider is Node node ? node.Name.ToString() : "unknown";
            blockedTargets.Add($"{localTarget.X:0.0},{localTarget.Y:0.0},{localTarget.Z:0.0}:{collider}");
        }

        blockers = blockedTargets.Count == 0 ? "none" : string.Join('|', blockedTargets);
        return blockedTargets.Count == 0;
    }

    private static BazaarCaptureImageResult BazaarSaveCaptureFrame(string path, Image? image)
    {
        if (image is null || image.IsEmpty() || image.GetWidth() < 640 || image.GetHeight() < 360)
        {
            return default;
        }
        var minimumLuminance = 1.0f;
        var maximumLuminance = 0.0f;
        var totalLuminance = 0.0f;
        var luminances = new List<float>();
        var stepX = Mathf.Max(1, image.GetWidth() / 24);
        var stepY = Mathf.Max(1, image.GetHeight() / 14);
        var sampleMinimumX = Mathf.FloorToInt(image.GetWidth() * 0.1f);
        var sampleMaximumX = Mathf.CeilToInt(image.GetWidth() * 0.9f);
        var sampleMinimumY = Mathf.FloorToInt(image.GetHeight() * 0.1f);
        var sampleMaximumY = Mathf.CeilToInt(image.GetHeight() * 0.9f);
        for (var y = sampleMinimumY; y < sampleMaximumY; y += stepY)
        {
            for (var x = sampleMinimumX; x < sampleMaximumX; x += stepX)
            {
                var color = image.GetPixel(x, y);
                var luminance = (color.R + color.G + color.B) / 3.0f;
                minimumLuminance = Mathf.Min(minimumLuminance, luminance);
                maximumLuminance = Mathf.Max(maximumLuminance, luminance);
                totalLuminance += luminance;
                luminances.Add(luminance);
            }
        }
        luminances.Sort();
        var sampleCount = luminances.Count;
        var lowerQuartile = sampleCount > 0
            ? luminances[Mathf.FloorToInt((sampleCount - 1) * 0.25f)]
            : 0.0f;
        var veryDarkSamples = luminances.Count(luminance => luminance < 0.075f);
        var saveResult = image.SavePng(path);
        var absolutePath = ProjectSettings.GlobalizePath(path);
        var saved = saveResult == Error.Ok
            && System.IO.File.Exists(absolutePath)
            && new System.IO.FileInfo(absolutePath).Length >= 10_000
            && maximumLuminance - minimumLuminance >= 0.08f;
        return new BazaarCaptureImageResult(
            saved,
            minimumLuminance,
            maximumLuminance,
            sampleCount > 0 ? totalLuminance / sampleCount : 0.0f,
            lowerQuartile,
            sampleCount > 0 ? veryDarkSamples / (float)sampleCount : 1.0f);
    }

    private static bool BazaarPerspectiveFrameReady(
        World3D world,
        Camera3D camera,
        Vector3 target,
        bool requireClearTarget,
        float margin,
        out string blocker)
    {
        blocker = "none";
        if (camera.IsPositionBehind(target))
        {
            return false;
        }
        var distance = camera.GlobalPosition.DistanceTo(target);
        var viewport = camera.GetViewport().GetVisibleRect().Size;
        var screen = camera.UnprojectPosition(target);
        var forward = -camera.GlobalBasis.Z.Normalized();
        var direction = camera.GlobalPosition.DirectionTo(target);
        PhysicsRaycastHit hit = default;
        var targetClear = !requireClearTarget
            || !PhysicsRaycast.TryHit(
                world,
                camera.GlobalPosition,
                target,
                1u,
                out hit);
        if (!targetClear)
        {
            blocker = hit.Collider is Node node ? node.Name : "unknown";
        }
        return targetClear
            && distance is >= 4.0f and <= 180.0f
            && forward.Dot(direction) >= 0.995f
            && screen.X >= margin
            && screen.Y >= margin
            && screen.X <= viewport.X - margin
            && screen.Y <= viewport.Y - margin;
    }

    private static bool BazaarTopdownContainsArena(
        Camera3D camera,
        DemolitionArenaLayout layout,
        float margin)
    {
        var viewport = camera.GetViewport().GetVisibleRect().Size;
        var corners = new[]
        {
            new Vector3(layout.WorldBounds.Position.X, layout.Origin.Y, layout.WorldBounds.Position.Y),
            new Vector3(layout.WorldBounds.End.X, layout.Origin.Y, layout.WorldBounds.Position.Y),
            new Vector3(layout.WorldBounds.Position.X, layout.Origin.Y, layout.WorldBounds.End.Y),
            new Vector3(layout.WorldBounds.End.X, layout.Origin.Y, layout.WorldBounds.End.Y)
        };
        return corners.All(corner =>
        {
            if (camera.IsPositionBehind(corner))
            {
                return false;
            }
            var screen = camera.UnprojectPosition(corner);
            return screen.X >= margin
                && screen.Y >= margin
                && screen.X <= viewport.X - margin
                && screen.Y <= viewport.Y - margin;
        });
    }
}
