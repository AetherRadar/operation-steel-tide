using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace OperationSteelTide;

/// <summary>
/// Isolated diagnostic scene. Samples the actual imported skin, not just bone lengths.
/// Run scenes/diagnostics/operator_presentation.tscn with --validate-operator-presentation
/// or --capture-operator-presentation. It never participates in normal gameplay.
/// </summary>
public partial class OperatorPresentationDiagnostics : Node3D
{
    private readonly record struct Edge(int A, int B, float RestLength);
    private sealed record Surface(
        MeshInstance3D Mesh, Skeleton3D Skeleton, Skin Skin, Vector3[] Vertices,
        int[] Joints, float[] Weights, int Influences, Edge[] Edges);
    private static readonly OperatorVisualId[] Visuals =
    {
        OperatorVisualId.Heron, OperatorVisualId.Lynx, OperatorVisualId.Magpie,
        OperatorVisualId.Jackal, OperatorVisualId.Viper, OperatorVisualId.Garrison
    };
    private static readonly string[] CapturePoses =
    {
        "idle", "walk", "run", "sprint", "ready_idle", "ready_walk", "ready_run",
        "aim_idle", "aim_walk", "aim_run", "prone_idle", "prone_crawl",
        "rifle_prone_idle", "rifle_prone_crawl", "downed", "death", "preview_stand"
    };

    public override async void _Ready()
    {
        var arguments = OS.GetCmdlineUserArgs();
        var capture = arguments.Contains("--capture-operator-presentation");
        if (!capture && !arguments.Contains("--validate-operator-presentation"))
        {
            GD.PushError("The operator presentation fixture requires an explicit diagnostic argument.");
            GetTree().Quit(2);
            return;
        }
        var failures = new List<string>();
        var sampleCount = 0;
        var pistolSamples = 0;
        var maxExcess = 0.0f;
        var selected = arguments.FirstOrDefault(argument => argument.StartsWith("--operator-visual=", StringComparison.Ordinal));
        var visuals = selected is null ? Visuals : new[] { Enum.Parse<OperatorVisualId>(selected.Split('=')[1], ignoreCase: true) };
        try
        {
            Camera3D? camera = capture ? CreateStage() : null;
            foreach (var id in visuals)
            {
                var visual = CombatModelLibrary.InstantiateOperator(id,
                    WeaponCatalog.Build(WeaponPlatform.M4A1, 0));
                AddChild(visual.Root);
                visual.SetWeaponReadied(true);
                var animator = CheckMovingActions(id, visual, failures);
                var surfaces = ReadSurfaces(visual.Root, id,
                    arguments.Contains("--dump-operator-bind"));
                if (surfaces.Count == 0)
                    throw new InvalidOperationException($"{id}: no skinned body surfaces.");
                var poses = visual.AnimationPlayer.GetAnimationList()
                    .Where(name => name != "RESET").ToArray();
                if (poses.Length != animator.AnimationCount + 1)
                    throw new InvalidOperationException($"{id}: expected gameplay actions plus preview_stand, found {poses.Length}.");
                foreach (var pose in poses)
                {
                    var holding = pose.StartsWith("ready_", StringComparison.Ordinal)
                        || pose.StartsWith("aim_", StringComparison.Ordinal)
                        || pose.StartsWith("rifle_", StringComparison.Ordinal);
                    visual.SetWeaponReadied(holding);
                    visual.SetWeaponVisible(holding);
                    var animation = visual.AnimationPlayer.GetAnimation(pose)
                        ?? throw new InvalidOperationException($"{id}: missing {pose}.");
                    foreach (var phase in new[] { 0.0, 0.25, 0.5, 0.75, 0.99 })
                    {
                        visual.AnimationPlayer.Play(pose, 0.0);
                        visual.AnimationPlayer.Seek(animation.Length * phase, update: true);
                        visual.AnimationPlayer.Pause();
                        visual.RefreshWeaponPose();
                        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                        var maximum = 0.0f;
                        var torn = 0;
                        var min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
                        var max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
                        foreach (var surface in surfaces)
                        {
                            var points = DeformedVertices(surface);
                            foreach (var point in points)
                            {
                                min = min.Min(point);
                                max = max.Max(point);
                                if (!point.IsFinite())
                                    throw new InvalidOperationException($"{id}:{pose}: nonfinite skin.");
                            }
                            foreach (var edge in surface.Edges)
                            {
                                var length = points[edge.A].DistanceTo(points[edge.B]);
                                var excess = length - edge.RestLength;
                                maximum = Mathf.Max(maximum, excess);
                                // Nearby garment vertices must not open into visible spikes.
                                if (edge.RestLength >= 0.020f && edge.RestLength < 0.035f
                                    && excess > 0.035f && length > edge.RestLength * 4)
                                    torn++;
                            }
                        }
                        maxExcess = Mathf.Max(maxExcess, maximum);
                        sampleCount++;
                        var settled = pose == "downed" || pose.Contains("prone", StringComparison.Ordinal)
                            || pose == "death" && phase >= 0.99;
                        var height = max.Y - min.Y;
                        var grounded = !settled || height <= 0.70f && min.Y >= -0.06f && min.Y <= 0.13f;
                        if (torn > 0 || !grounded)
                            failures.Add($"{id}:{pose}:{phase:F2}:torn={torn}:height={height:F3}:floor={min.Y:F3}");
                        GD.Print($"OPERATOR_SKIN_SAMPLE visual={id} pose={pose} phase={phase:F2} torn={torn} max_excess={maximum:F4} height={height:F3} floor={min.Y:F3}");
                        var oppositeStep = phase == 0.75 && (pose.EndsWith("walk", StringComparison.Ordinal)
                            || pose.EndsWith("run", StringComparison.Ordinal) || pose == "sprint");
                        if (camera is not null && CapturePoses.Contains(pose)
                            && (phase == 0.25 || oppositeStep || settled && phase >= 0.99))
                            await Capture(camera, id, pose, phase, min, max);
                    }
                }
                visual.Root.QueueFree();
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                pistolSamples += await CheckPistols(id, camera, failures);
            }
        }
        catch (Exception exception)
        {
            failures.Add(exception.Message);
            GD.PushError(exception.ToString());
        }
        var valid = failures.Count == 0 && sampleCount == visuals.Length * 57 * 5
            && pistolSamples == visuals.Length * 4 * 6 * 3;
        GD.Print($"OPERATOR_PRESENTATION_CHECK visuals={visuals.Length} samples={sampleCount} pistol_samples={pistolSamples} max_edge_excess={maxExcess:F4} failure_count={failures.Count} first_failures={string.Join('|', failures.Take(16))}");
        GD.Print($"OPERATOR_PRESENTATION_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }

    private static AuthoredOperatorAnimator CheckMovingActions(OperatorVisualId id, AuthoredOperatorVisual visual, List<string> failures)
    {
        var animator = new AuthoredOperatorAnimator(visual);
        var skeleton = Descendants<Skeleton3D>(visual.Root).Single();
        var foot = Enumerable.Range(0, skeleton.GetBoneCount()).Single(index =>
            skeleton.GetBoneName(index).ToString().EndsWith("RightFoot", StringComparison.Ordinal));
        var hand = Enumerable.Range(0, skeleton.GetBoneCount()).Single(index =>
            skeleton.GetBoneName(index).ToString().EndsWith("RightHand", StringComparison.Ordinal));
        var prefix = visual.UsesPistolCarryPose ? "pistol_" : string.Empty;
        var initialRoot = visual.Root.Transform;
        animator.Update(0.25f, 3f, true, false, false, false, false, false, false);
        var beforeShot = skeleton.GetBoneGlobalPose(foot).Origin;
        animator.PlayAction("shoot", 0.22f);
        animator.Update(0.06f, 3f, true, false, false, false, false, false, false);
        var layeredHand = skeleton.GetBoneGlobalPose(hand);
        var movingShot = animator.CurrentAnimation == prefix + "ready_run"
            && animator.UpperBodyAction == prefix + "shoot" && animator.UpperBodyActionPosition > 0
            && beforeShot.DistanceTo(skeleton.GetBoneGlobalPose(foot).Origin) > 0.01f;
        // Re-evaluate the same gait instant without its combat layer to prove
        // that the shot visibly affects the hand as well as naming a clip.
        visual.AnimationPlayer.Advance(0.0);
        visual.RefreshWeaponPose();
        var gaitHand = skeleton.GetBoneGlobalPose(hand);
        var visibleRecoil = layeredHand.Origin.DistanceTo(gaitHand.Origin) > 0.003f
            || !layeredHand.Basis.IsEqualApprox(gaitHand.Basis);
        var firstMovingShotTime = animator.UpperBodyActionPosition;
        animator.PlayAction("shoot", 0.22f);
        animator.Update(0.02f, 3f, true, false, false, false, false, false, false);
        var movingRetrigger = animator.UpperBodyActionPosition > 0.0
            && animator.UpperBodyActionPosition < firstMovingShotTime * 0.5;
        animator.Update(0.3f, 3f, true, false, false, false, false, false, false);
        animator.Update(0.02f, 3f, true, false, false, false, false, false, false);
        var afterShot = skeleton.GetBoneGlobalPose(hand);
        visual.AnimationPlayer.Advance(0.0);
        visual.RefreshWeaponPose();
        var recoilReleased = animator.UpperBodyAction.Length == 0
            && afterShot.IsEqualApprox(skeleton.GetBoneGlobalPose(hand));
        animator.SetRestingPose(true);
        animator.PlayAction("shoot", 0.22f);
        animator.Update(0.1f, 0f, true, false, false, false, false, false, false);
        var firstStandingShotTime = visual.AnimationPlayer.CurrentAnimationPosition;
        animator.PlayAction("shoot", 0.22f);
        animator.Update(0.02f, 0f, true, false, false, false, false, false, false);
        var standingRetrigger = visual.AnimationPlayer.CurrentAnimationPosition > 0.0
            && visual.AnimationPlayer.CurrentAnimationPosition < firstStandingShotTime * 0.5;
        animator.SetRestingPose(true);
        animator.Update(0.18f, 3f, true, false, false, false, false, false, false);
        var beforeReload = skeleton.GetBoneGlobalPose(foot).Origin;
        animator.PlayAction("reload", 2f);
        animator.Update(0.18f, 3f, true, false, false, false, false, false, false);
        var movingReload = animator.CurrentAnimation == prefix + "ready_run"
            && animator.UpperBodyAction == prefix + "reload"
            && beforeReload.DistanceTo(skeleton.GetBoneGlobalPose(foot).Origin) > 0.01f;
        animator.Update(0.25f, 0.7f, true, true, false, false, false, false, false);
        animator.PlayAction("shoot", 0.22f);
        var proneWins = animator.CurrentAnimation == (visual.UsesPistolCarryPose ? "pistol_" : "rifle_") + "prone_crawl"
            && animator.UpperBodyAction.Length == 0;
        animator.Update(0.5f, 0, false, false, false, false, true, false, false);
        var downedWins = animator.CurrentAnimation == "downed" && animator.UpperBodyAction.Length == 0;
        var unchangedRoot = visual.Root.Transform.IsEqualApprox(initialRoot);
        animator.SetRestingPose(true);
        var valid = movingShot && visibleRecoil && movingRetrigger && standingRetrigger
            && recoilReleased && movingReload && proneWins && downedWins && unchangedRoot;
        GD.Print($"OPERATOR_MOVING_ACTION_CHECK visual={id} family={prefix} moving_shot={movingShot} recoil={visibleRecoil} moving_retrigger={movingRetrigger} standing_retrigger={standingRetrigger} recoil_released={recoilReleased} moving_reload={movingReload} prone_priority={proneWins} downed_priority={downedWins} authored_root={unchangedRoot}");
        if (!valid) failures.Add($"{id}:moving-action-layer");
        return animator;
    }

    private async Task<int> CheckPistols(OperatorVisualId id, Camera3D? camera, List<string> failures)
    {
        var count = 0;
        foreach (var platform in new[] { WeaponPlatform.P226, WeaponPlatform.M1911, WeaponPlatform.DesertEagle, WeaponPlatform.GSh18 })
        {
            var visual = CombatModelLibrary.InstantiateOperator(id, WeaponCatalog.Build(platform, 0));
            AddChild(visual.Root);
            visual.SetWeaponReadied(true);
            var animator = CheckMovingActions(id, visual, failures);
            var surfaces = ReadSurfaces(visual.Root, id);
            foreach (var (speed, aiming, prone) in new[]
            {
                (0f, false, false), (0f, true, false), (1.3f, false, false),
                (2.8f, true, false), (0f, true, true), (0.7f, true, true)
            })
            {
                animator.Update(0.02f, speed, true, prone, false, aiming, false, false, false);
                var pose = animator.CurrentAnimation;
                foreach (var phase in new[] { 0.0, 0.37, 0.8 })
                {
                    // Sample the authored clip itself, after transition selection;
                    // a 20 ms state update is still inside the 160 ms idle blend.
                    visual.AnimationPlayer.Play(pose, customBlend: 0.0);
                    visual.AnimationPlayer.Seek(visual.AnimationPlayer.GetAnimation(pose).Length * phase, update: true);
                    visual.AnimationPlayer.Pause();
                    visual.RefreshWeaponPose();
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    var fit = visual.InspectPistolGrip();
                    var torn = 0;
                    var min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
                    var max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
                    foreach (var surface in surfaces)
                    {
                        var points = DeformedVertices(surface);
                        foreach (var point in points)
                        {
                            min = min.Min(point);
                            max = max.Max(point);
                        }
                        foreach (var edge in surface.Edges)
                        {
                            var length = points[edge.A].DistanceTo(points[edge.B]);
                            if (edge.RestLength >= 0.020f && edge.RestLength < 0.035f
                                && length - edge.RestLength > 0.035f && length > edge.RestLength * 4)
                                torn++;
                        }
                    }
                    var height = max.Y - min.Y;
                    var grounded = !prone || height <= 0.70f && min.Y >= -0.06f && min.Y <= 0.13f;
                    var proneClip = !prone || pose == (speed > 0 ? "pistol_prone_crawl" : "pistol_prone_idle");
                    var valid = grounded && proneClip && fit.Available && fit.AuthoredClip && fit.AuthoredSocket
                        && fit.PrimaryContactDistance <= 0.025f
                        && fit.PalmSeparation is >= 0.025f and <= 0.11f
                        && fit.RightElbowAngle is >= 45 and <= 178
                        && fit.LeftElbowAngle is >= 45 and <= 178 && torn == 0;
                    if (!valid) failures.Add($"{id}:{platform}:{pose}:{phase:F2}:grip={fit}:torn={torn}:height={height:F3}:floor={min.Y:F3}");
                    GD.Print($"OPERATOR_PISTOL_SAMPLE visual={id} weapon={platform} pose={pose} phase={phase:F2} valid={valid} torn={torn} contact={fit.PrimaryContactDistance:F4} separation={fit.PalmSeparation:F4} height={height:F3} floor={min.Y:F3}");
                    count++;
                    if (camera is not null && platform == WeaponPlatform.GSh18 && phase == 0.37)
                    {
                        await Capture(camera, id, pose, phase, min, max);
                        await CaptureDetail(camera, id, pose + "_hands", visual.WeaponSocket.GlobalPosition, new Vector3(0.50f, 0.18f, -0.65f));
                    }
                    else if (camera is not null && id == OperatorVisualId.Viper && phase == 0.37
                        && pose is "pistol_ready_idle" or "pistol_aim_idle")
                    {
                        await CaptureDetail(camera, id, platform + "_" + pose + "_hands",
                            visual.WeaponSocket.GlobalPosition, new Vector3(0.50f, 0.18f, -0.65f));
                    }
                }
            }
            visual.Root.QueueFree();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        return count;
    }

    private static IEnumerable<T> Descendants<T>(Node node) where T : Node
    {
        if (node is T match) yield return match;
        foreach (var child in node.GetChildren())
            foreach (var nested in Descendants<T>(child)) yield return nested;
    }

    private static List<Surface> ReadSurfaces(Node root, OperatorVisualId visualId, bool dumpBind = false)
    {
        var result = new List<Surface>();
        foreach (var mesh in Descendants<MeshInstance3D>(root))
        {
            if (mesh.Skin is not { } skin || mesh.Mesh is not { } geometry) continue;
            var skeleton = mesh.GetNode<Skeleton3D>(mesh.Skeleton);
            for (var index = 0; index < geometry.GetSurfaceCount(); index++)
            {
                var arrays = geometry.SurfaceGetArrays(index);
                var vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
                var joints = arrays[(int)Mesh.ArrayType.Bones].AsInt32Array();
                var weights = arrays[(int)Mesh.ArrayType.Weights].AsFloat32Array();
                var indices = arrays[(int)Mesh.ArrayType.Index].AsInt32Array();
                if (vertices.Length == 0 || joints.Length == 0 || weights.Length != joints.Length
                    || joints.Length % vertices.Length != 0 || joints.Length / vertices.Length is not (4 or 8))
                    throw new InvalidOperationException($"{mesh.Name}: invalid imported skin influence arrays.");
                var pairs = new HashSet<(int, int)>();
                for (var i = 0; i < indices.Length; i += 3)
                {
                    for (var side = 0; side < 3; side++)
                    {
                        var a = indices[i + side];
                        var b = indices[i + (side + 1) % 3];
                        pairs.Add(a < b ? (a, b) : (b, a));
                    }
                }
                var audited = visualId == OperatorVisualId.Magpie
                    ? new HashSet<(int, int)> { (35917, 35918), (42242, 42247) }
                    : new HashSet<(int, int)>();
                var edges = pairs.Where(pair => !audited.Contains(pair)).Select(pair => new Edge(pair.Item1, pair.Item2,
                    (mesh.GlobalTransform * vertices[pair.Item1]).DistanceTo(mesh.GlobalTransform * vertices[pair.Item2]))).ToArray();
                var surface = new Surface(mesh, skeleton, skin, vertices, joints, weights,
                    joints.Length / vertices.Length, edges);
                var rest = DeformedVertices(surface, useRest: true);
                var maxError = 0.0f;
                var worstVertex = -1;
                for (var vertex = 0; vertex < rest.Length; vertex++)
                {
                    var error = rest[vertex].DistanceTo(mesh.GlobalTransform * vertices[vertex]);
                    if (error > maxError)
                    {
                        maxError = error;
                        worstVertex = vertex;
                    }
                }
                if (dumpBind)
                {
                    GD.Print($"OPERATOR_BIND_DUMP mesh={mesh.Name} skeleton={skeleton.Name} vertices={vertices.Length} "
                        + $"binds={skin.GetBindCount()} max_error={maxError:F6} worst_vertex={worstVertex} "
                        + $"mesh_transform={mesh.GlobalTransform} skeleton_transform={skeleton.GlobalTransform}");
                    if (worstVertex >= 0)
                    {
                        GD.Print($"OPERATOR_BIND_DUMP_VERTEX authored={mesh.GlobalTransform * vertices[worstVertex]} "
                            + $"reconstructed={rest[worstVertex]}");
                        for (var influence = 0; influence < joints.Length / vertices.Length; influence++)
                        {
                            var slot = worstVertex * (joints.Length / vertices.Length) + influence;
                            var weight = weights[slot];
                            if (weight <= 0) continue;
                            var bind = joints[slot];
                            var bindName = skin.GetBindName(bind);
                            var bone = string.IsNullOrEmpty(bindName.ToString())
                                ? skin.GetBindBone(bind) : skeleton.FindBone(bindName);
                            GD.Print($"OPERATOR_BIND_DUMP_INFLUENCE slot={bind} weight={weight:F8} name={bindName} bone={bone} "
                                + $"direct_bone_name={(bind >= 0 && bind < skeleton.GetBoneCount() ? skeleton.GetBoneName(bind) : "<out-of-range>")} "
                                + $"rest={skeleton.GetBoneGlobalRest(bone)} bind_pose={skin.GetBindPose(bind)} "
                                + $"rest_bind={skeleton.GetBoneGlobalRest(bone) * skin.GetBindPose(bind)}");
                        }
                    }
                }
                if (maxError > 0.003f)
                    throw new InvalidOperationException($"{mesh.Name}: skin bind does not reconstruct the authored rest mesh (max_error={maxError:F6}).");
                result.Add(surface);
            }
        }
        return result;
    }

    private static Vector3[] DeformedVertices(Surface surface, bool useRest = false)
    {
        var transforms = new Transform3D[surface.Skin.GetBindCount()];
        for (var bind = 0; bind < transforms.Length; bind++)
        {
            var name = surface.Skin.GetBindName(bind);
            var bone = string.IsNullOrEmpty(name.ToString())
                ? surface.Skin.GetBindBone(bind) : surface.Skeleton.FindBone(name);
            if (bone < 0) throw new InvalidOperationException($"Missing skin joint {name}.");
            transforms[bind] = surface.Skeleton.GlobalTransform
                * (useRest ? surface.Skeleton.GetBoneGlobalRest(bone) : surface.Skeleton.GetBoneGlobalPose(bone))
                * surface.Skin.GetBindPose(bind);
        }
        var result = new Vector3[surface.Vertices.Length];
        for (var vertex = 0; vertex < result.Length; vertex++)
        {
            var position = Vector3.Zero;
            var weightSum = 0.0f;
            for (var influence = 0; influence < surface.Influences; influence++)
            {
                var slot = vertex * surface.Influences + influence;
                var weight = surface.Weights[slot];
                if (weight <= 0) continue;
                position += (transforms[surface.Joints[slot]] * surface.Vertices[vertex]) * weight;
                weightSum += weight;
            }
            if (Mathf.Abs(weightSum - 1) > 0.015f)
                throw new InvalidOperationException($"Unnormalized skin weight {weightSum:F4}.");
            result[vertex] = surface.Mesh.GlobalTransform * position;
        }
        return result;
    }

    private Camera3D CreateStage()
    {
        AddChild(new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color,
                BackgroundColor = new Color(0.085f, 0.105f, 0.13f),
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = Colors.White,
                AmbientLightEnergy = 0.6f,
                TonemapMode = Godot.Environment.ToneMapper.Filmic
            }
        });
        AddChild(new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-45, -25, 0), LightEnergy = 1.5f,
            ShadowEnabled = true
        });
        // Diagnostic floor establishes contact and scale; never used as game art.
        AddChild(new MeshInstance3D
        {
            Mesh = new PlaneMesh { Size = new Vector2(12, 12) },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.24f, 0.26f, 0.28f), Roughness = 1 }
        });
        var camera = new Camera3D { Current = true, Fov = 43, Near = 0.03f };
        AddChild(camera);
        return camera;
    }

    private async Task Capture(Camera3D camera, OperatorVisualId id, string pose, double phase, Vector3 min, Vector3 max)
    {
        var target = (min + max) * 0.5f;
        camera.Position = target + new Vector3(1.75f, 0.45f, -2.7f);
        camera.LookAt(target);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var directory = ProjectSettings.GlobalizePath("res://logs/operator-presentation");
        Directory.CreateDirectory(directory);
        var ignore = Path.Combine(directory, ".gdignore");
        if (!File.Exists(ignore)) File.WriteAllText(ignore, string.Empty);
        var path = Path.Combine(directory, $"{id.ToString().ToLowerInvariant()}_{pose}_{phase:F2}.png");
        using var image = GetViewport().GetTexture().GetImage();
        if (image.SavePng(path) != Error.Ok) throw new IOException($"Failed to capture {path}.");
        GD.Print($"OPERATOR_PRESENTATION_CAPTURE path={path}");
        if (id == OperatorVisualId.Lynx && pose == "ready_idle")
            await CaptureDetail(camera, id, "hair_back", new Vector3(0, max.Y - 0.35f, 0.12f), new Vector3(0.65f, 0.1f, 0.95f));
    }

    private async Task CaptureDetail(Camera3D camera, OperatorVisualId id, string label, Vector3 target, Vector3 offset)
    {
        camera.Position = target + offset;
        camera.LookAt(target);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var path = ProjectSettings.GlobalizePath($"res://logs/operator-presentation/{id.ToString().ToLowerInvariant()}_{label}.png");
        using var image = GetViewport().GetTexture().GetImage();
        if (image.SavePng(path) != Error.Ok) throw new IOException($"Failed to capture {path}.");
        GD.Print($"OPERATOR_PRESENTATION_CAPTURE path={path} draw_calls={Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame)} video_memory={Performance.GetMonitor(Performance.Monitor.RenderVideoMemUsed)}");
    }
}
