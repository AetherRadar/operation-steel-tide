using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace OperationSteelTide;

/// <summary>Deterministic MAP 03 diagnostics kept separate from normal runtime assembly.</summary>
public partial class FreightTerminalWorld
{
    private enum OrbitalComplexDiagnosticKind
    {
        Map,
        Collision,
        Gameplay,
        Spawns,
        Loading,
        Atmosphere
    }

    private readonly record struct OrbitalComplexDiagnosticSnapshot(
        bool Selected,
        bool LayoutValid,
        bool SceneReady,
        bool ExtractionReady,
        bool ObjectivesReady,
        bool CollisionReady,
        bool RoutesReady,
        bool SpawnsReady,
        bool LoadingReady,
        bool AtmosphereReady,
        int AuthoredMeshCount,
        int CollisionShapeCount,
        int ObjectiveAnchorCount,
        int TraversalLinkCount,
        string Error)
    {
        public bool For(OrbitalComplexDiagnosticKind kind)
            => kind switch
            {
                OrbitalComplexDiagnosticKind.Map => Selected
                    && LayoutValid
                    && SceneReady
                    && ExtractionReady
                    && ObjectivesReady,
                OrbitalComplexDiagnosticKind.Collision => Selected
                    && LayoutValid
                    && CollisionReady,
                OrbitalComplexDiagnosticKind.Gameplay => Selected
                    && LayoutValid
                    && SceneReady
                    && ExtractionReady
                    && ObjectivesReady
                    && RoutesReady,
                OrbitalComplexDiagnosticKind.Spawns => Selected
                    && LayoutValid
                    && SpawnsReady,
                OrbitalComplexDiagnosticKind.Loading => Selected && LoadingReady,
                OrbitalComplexDiagnosticKind.Atmosphere => Selected
                    && SceneReady
                    && AtmosphereReady,
                _ => false
            };
    }

    private async void ValidateOrbitalMap()
        => await RunOrbitalComplexDiagnostic(OrbitalComplexDiagnosticKind.Map);

    private async void ValidateOrbitalCollision()
        => await RunOrbitalComplexDiagnostic(OrbitalComplexDiagnosticKind.Collision);

    private async void ValidateOrbitalGameplay()
        => await RunOrbitalComplexDiagnostic(OrbitalComplexDiagnosticKind.Gameplay);

    private async void ValidateOrbitalSpawns()
        => await RunOrbitalComplexDiagnostic(OrbitalComplexDiagnosticKind.Spawns);

    private async void ValidateOrbitalLoading()
        => await RunOrbitalComplexDiagnostic(OrbitalComplexDiagnosticKind.Loading);

    private async void ValidateOrbitalAtmosphere()
        => await RunOrbitalComplexDiagnostic(OrbitalComplexDiagnosticKind.Atmosphere);

    private async Task RunOrbitalComplexDiagnostic(OrbitalComplexDiagnosticKind kind)
    {
        await WaitFrames(3);
        var snapshot = CaptureOrbitalComplexDiagnosticSnapshot();
        var valid = snapshot.For(kind);
        var label = kind.ToString().ToUpperInvariant();
        GD.Print(
            $"ORBITAL_{label}_CHECK valid={valid} selected={snapshot.Selected} "
            + $"layout={snapshot.LayoutValid} scene={snapshot.SceneReady} "
            + $"loading={snapshot.LoadingReady} extraction={snapshot.ExtractionReady} "
            + $"objectives={snapshot.ObjectivesReady} collision={snapshot.CollisionReady} "
            + $"routes={snapshot.RoutesReady}:{snapshot.TraversalLinkCount} "
            + $"spawns={snapshot.SpawnsReady} atmosphere={snapshot.AtmosphereReady} "
            + $"meshes={snapshot.AuthoredMeshCount} shapes={snapshot.CollisionShapeCount} "
            + $"anchors={snapshot.ObjectiveAnchorCount} error={DiagnosticToken(snapshot.Error)}");
        GD.Print($"ORBITAL_{label}_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }

    private OrbitalComplexDiagnosticSnapshot CaptureOrbitalComplexDiagnosticSnapshot()
    {
        if (!IsOrbitalComplexRuntimeMapSelected)
        {
            return new OrbitalComplexDiagnosticSnapshot(
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                0,
                0,
                0,
                0,
                "map_not_selected");
        }

        OrbitalComplexMapLayout layout;
        try
        {
            layout = OrbitalComplexRuntimeLayout;
        }
        catch (Exception exception)
        {
            return new OrbitalComplexDiagnosticSnapshot(
                true,
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                0,
                0,
                0,
                0,
                exception.Message);
        }

        var layoutValidation = OrbitalComplexLayoutValidator.Validate(layout);
        var build = _orbitalComplexRuntimeBuild;
        var sceneReady = OrbitalComplexRuntimeSceneReady;
        var authoredMeshes = build is null
            ? 0
            : CountOrbitalComplexMeshes(build.AuthoredArtRoot);
        var collisionShapes = build is null
            ? 0
            : CountOrbitalComplexCollisionShapes(build.GameplayRoot, requireShape: true);
        var objectiveAnchors = build?.ObjectiveAnchors.Count ?? 0;
        var objectivesReady = layout.Objectives.Count >= 2
            && objectiveAnchors == layout.Objectives.Count
            && _objectiveTerminals.Count >= layout.Objectives.Count;
        var extractionReady = IsInstanceValid(_extractionArea)
            && IsInstanceValid(_extractionMarker)
            && _orbitalComplexRuntimeExtractionSite is not null
            && IsInstanceValid(_orbitalComplexRuntimeExtractionSite);
        var collisionReady = build is not null
            && build.CollisionShapeCount
                >= layout.CollisionBoxes.Count + layout.Ramps.Count + layout.PowerGates.Count
            && collisionShapes >= build.CollisionShapeCount
            && build.GameplayRoot.IsInGroup(OrbitalComplexWorldAssembler.GameplayCollisionGroup);
        var routesReady = _orbitalComplexRuntimeTraversalLinkCount
                >= layout.PatrolRoutes.Count + layout.Ramps.Count
            && layout.RouteProbes.Count >= 12;
        var spawnsReady = layout.PlayerSpawnPads.Count >= 4
            && layout.RivalSpawnPads.Count >= 4
            && layout.GarrisonSpawns.Count >= 20
            && layout.QrfSpawns.Count >= 6
            && layout.BossRoute.Count >= 12
            && layout.PlayerSpawnPads.Any(pad =>
                pad.Position.DistanceSquaredTo(DeploymentPoint) <= 0.01f)
            && layout.PlayerSpawnPads.All(pad =>
                InsideOrbitalComplexRuntimeBounds(layout.Bounds, pad.Position))
            && layout.RivalSpawnPads.All(pad =>
                InsideOrbitalComplexRuntimeBounds(layout.Bounds, pad.Position));
        var loadingReady = GD.Load<PackedScene>(OrbitalComplexWorldAssembler.DefaultScenePath)
                is not null
            && string.IsNullOrEmpty(_orbitalComplexRuntimeLoadError)
            && sceneReady;
        var atmosphereReady = IsInstanceValid(_environmentRef)
            && _environmentRef.BackgroundMode == Godot.Environment.BGMode.Color
            && _environmentRef.AmbientLightSource == Godot.Environment.AmbientSource.Color
            && (_levelRoot?.GetMeta("falltide_indoor_atmosphere", false).AsBool() ?? false)
            && (build?.PresentationNodes.ContainsKey("PowerZone_Blackout") ?? false)
            && (build?.PresentationNodes.ContainsKey("PowerZone_Powered") ?? false);

        return new OrbitalComplexDiagnosticSnapshot(
            true,
            layoutValidation.Valid,
            sceneReady,
            extractionReady,
            objectivesReady,
            collisionReady,
            routesReady,
            spawnsReady,
            loadingReady,
            atmosphereReady,
            authoredMeshes,
            collisionShapes,
            objectiveAnchors,
            _orbitalComplexRuntimeTraversalLinkCount,
            _orbitalComplexRuntimeLoadError ?? string.Empty);
    }

    private static int CountOrbitalComplexMeshes(Node root)
    {
        var count = root is GeometryInstance3D ? 1 : 0;
        foreach (var child in root.GetChildren())
        {
            if (child is Node childNode)
            {
                count += CountOrbitalComplexMeshes(childNode);
            }
        }
        return count;
    }

    private static int CountOrbitalComplexCollisionShapes(Node root, bool requireShape)
    {
        var count = root is CollisionShape3D shape
            && (!requireShape || shape.Shape is not null)
            ? 1
            : 0;
        foreach (var child in root.GetChildren())
        {
            if (child is Node childNode)
            {
                count += CountOrbitalComplexCollisionShapes(childNode, requireShape);
            }
        }
        return count;
    }

    private static string DiagnosticToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "none";
        }
        var token = value.Replace(' ', '_').Replace('\r', '_').Replace('\n', '_');
        return token.Length > 120 ? token[..120] : token;
    }

    private async void CaptureOrbitalMap()
    {
        await WaitFrames(6);
        var snapshot = CaptureOrbitalComplexDiagnosticSnapshot();
        if (!snapshot.SceneReady || _orbitalComplexRuntimeBuild is null)
        {
            GD.Print("ORBITAL_CAPTURE valid=False reason=authored_scene_not_ready");
            GetTree().Quit(2);
            return;
        }

        if (IsInstanceValid(_hud))
        {
            _hud.Visible = false;
        }
        if (IsInstanceValid(_player))
        {
            _player.ProcessMode = ProcessModeEnum.Disabled;
        }

        var layout = OrbitalComplexRuntimeLayout;
        var target = layout.MinimapLandmarks
            .FirstOrDefault(landmark => landmark.Id is "stormglass" or "reactor")
            .Position;
        if (target == Vector3.Zero)
        {
            target = layout.Bounds.Center;
        }
        var camera = new Camera3D
        {
            Name = "OrbitalComplexCaptureCamera",
            Fov = 62.0f,
            Near = 0.05f,
            Far = 700.0f
        };
        AddChild(camera);
        camera.GlobalPosition = target + new Vector3(24.0f, 14.0f, 28.0f);
        camera.LookAt(target + Vector3.Up * 1.4f, Vector3.Up);
        camera.MakeCurrent();
        await WaitFrames(12);
        SaveViewportImage("res://orbital_complex_map_validation.png");
        GD.Print(
            $"ORBITAL_CAPTURE valid=True path=orbital_complex_map_validation.png "
            + $"target={target} camera={camera.GlobalPosition} meshes={snapshot.AuthoredMeshCount}");
        GetTree().Quit();
    }
}
