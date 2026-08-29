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
        bool StrictTopdown = false);

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
                62.0f),
            new BazaarCaptureFrame(
                "res://bazaar_crossing_site_a.png",
                new Vector3(-49.0f, eye, -5.0f),
                new Vector3(-43.0f, 1.1f, -22.0f),
                68.0f),
            new BazaarCaptureFrame(
                "res://bazaar_crossing_site_b.png",
                new Vector3(49.0f, eye, -5.0f),
                new Vector3(43.0f, 1.1f, -22.0f),
                68.0f),
            new BazaarCaptureFrame(
                "res://bazaar_crossing_mid.png",
                new Vector3(0.0f, eye, 18.0f),
                new Vector3(0.0f, 2.6f, 0.0f),
                70.0f),
            new BazaarCaptureFrame(
                "res://bazaar_crossing_a_gallery.png",
                new Vector3(-59.0f, 3.2f + eye, -13.0f),
                new Vector3(-54.0f, 3.8f, -25.0f),
                68.0f),
            new BazaarCaptureFrame(
                "res://bazaar_crossing_mid_bridge.png",
                new Vector3(-10.0f, 3.2f + eye, 0.0f),
                new Vector3(10.0f, 3.8f, 0.0f),
                68.0f),
            new BazaarCaptureFrame(
                "res://bazaar_crossing_b_balcony.png",
                new Vector3(59.0f, 2.8f + eye, -15.0f),
                new Vector3(54.0f, 3.4f, -26.0f),
                68.0f),
            new BazaarCaptureFrame(
                "res://bazaar_crossing_a_gallery_south_stair.png",
                new Vector3(-59.0f, eye, 2.0f),
                new Vector3(-59.0f, 3.3f, -11.0f),
                66.0f),
            new BazaarCaptureFrame(
                "res://bazaar_crossing_a_gallery_east_stair.png",
                new Vector3(-39.0f, eye, -27.0f),
                new Vector3(-52.0f, 3.3f, -27.0f),
                66.0f),
            new BazaarCaptureFrame(
                "res://bazaar_crossing_mid_west_stair.png",
                new Vector3(-25.0f, eye, 0.0f),
                new Vector3(-12.0f, 3.3f, 0.0f),
                66.0f),
            new BazaarCaptureFrame(
                "res://bazaar_crossing_mid_east_stair.png",
                new Vector3(25.0f, eye, 0.0f),
                new Vector3(12.0f, 3.3f, 0.0f),
                66.0f),
            new BazaarCaptureFrame(
                "res://bazaar_crossing_b_balcony_south_stair.png",
                new Vector3(59.0f, eye, -2.5f),
                new Vector3(59.0f, 2.9f, -14.0f),
                66.0f),
            new BazaarCaptureFrame(
                "res://bazaar_crossing_b_balcony_west_stair.png",
                new Vector3(40.5f, eye, -27.0f),
                new Vector3(52.0f, 2.9f, -27.0f),
                66.0f),
            new BazaarCaptureFrame(
                "res://bazaar_crossing_attack_spawn.png",
                new Vector3(0.0f, 0.22f + eye, 49.0f),
                new Vector3(0.0f, 1.4f, 31.0f),
                70.0f),
            new BazaarCaptureFrame(
                "res://bazaar_crossing_defender_spawn.png",
                new Vector3(0.0f, 0.22f + eye, -49.0f),
                new Vector3(0.0f, 1.4f, -30.0f),
                70.0f)
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
            var framingReady = !frame.StrictTopdown
                || BazaarTopdownContainsArena(camera, layout, 20.0f);
            var saved = BazaarSaveCaptureFrame(frame.Path, image);
            validity[frame.Path] = framingReady && saved;
            GD.Print(
                $"BAZAAR_CAPTURE_FRAME path={frame.Path} saved={saved} framing={framingReady} "
                + $"position={camera.GlobalPosition} target={layout.Origin + frame.Target}");
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

    private static bool BazaarSaveCaptureFrame(string path, Image? image)
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
