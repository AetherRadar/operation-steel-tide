using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace OperationSteelTide;

/// <summary>Moves an imported operator through the world and measures planted-foot travel.</summary>
public partial class OperatorLocomotionDiagnostics : Node3D
{
    private const float Step = 1.0f / 60.0f;
    private readonly record struct Sample(double Time, Vector3 Actor, Vector3 Left, Vector3 Right);
    private readonly record struct Result(int Contacts, float MedianSlip, float P90Slip, float MaxDrift, float Lift);
    private static readonly OperatorVisualId[] Roles =
    {
        OperatorVisualId.Heron, OperatorVisualId.Lynx, OperatorVisualId.Magpie,
        OperatorVisualId.Jackal, OperatorVisualId.Viper, OperatorVisualId.Garrison
    };

    public override async void _Ready()
    {
        var arguments = OS.GetCmdlineUserArgs();
        if (!arguments.Contains("--validate-operator-locomotion"))
        {
            GD.PushError("Operator locomotion requires --validate-operator-locomotion.");
            GetTree().Quit(2);
            return;
        }
        var failures = new List<string>();
        var cases = 0;
        try
        {
            var selection = arguments.FirstOrDefault(value => value.StartsWith("--operator-role=", StringComparison.Ordinal))?
                .Split('=')[1] ?? "jackal";
            var roles = selection.Equals("all", StringComparison.OrdinalIgnoreCase)
                ? Roles : new[] { Enum.Parse<OperatorVisualId>(selection, ignoreCase: true) };
            var capture = arguments.Contains("--capture-operator-locomotion");
            if (capture && DisplayServer.GetName() == "headless")
                throw new InvalidOperationException("Locomotion frame capture requires a rendering display.");
            var camera = GetNode<Camera3D>("Camera3D");
            foreach (var role in roles)
            {
                foreach (var family in new[] { "unarmed", "rifle", "pistol" })
                {
                    foreach (var (gait, speed) in new[] { ("walk", 1.4f), ("run", 3.0f), ("sprint", 4.6f) })
                    {
                        await CheckCase(role, family, gait, speed, camera, capture, failures);
                        cases++;
                    }
                }
            }
            if (cases != roles.Length * 9)
                failures.Add($"Expected {roles.Length * 9} motion cases, got {cases}.");
        }
        catch (Exception exception)
        {
            failures.Add(exception.Message);
            GD.PushError(exception.ToString());
        }
        var valid = failures.Count == 0 && cases > 0;
        GD.Print($"OPERATOR_LOCOMOTION_CHECK cases={cases} failures={failures.Count} details={string.Join('|', failures.Take(16))}");
        GD.Print($"OPERATOR_LOCOMOTION_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }

    private async Task CheckCase(OperatorVisualId role, string family, string gait, float speed,
        Camera3D camera, bool capture, List<string> failures)
    {
        var actor = new Node3D { Name = $"Moving{role}{family}{gait}" };
        AddChild(actor);
        var armed = family != "unarmed";
        var weapon = WeaponCatalog.Build(family == "pistol" ? WeaponPlatform.P226 : WeaponPlatform.M4A1, 0);
        var visual = CombatModelLibrary.InstantiateOperator(role, weapon);
        actor.AddChild(visual.Root);
        visual.SetWeaponReadied(armed);
        visual.SetWeaponVisible(armed);
        var animator = new AuthoredOperatorAnimator(visual);
        var skeleton = Descendants<Skeleton3D>(visual.Root).Single();
        var left = RequiredBone(skeleton, "LeftFoot");
        var right = RequiredBone(skeleton, "RightFoot");
        animator.SetRestingPose(armed);
        animator.Update(0.0f, speed, armed, false, false, false, false, false, false);
        var expected = family switch
        {
            "unarmed" => gait,
            "rifle" => "ready_" + gait,
            "pistol" => "pistol_ready_" + gait,
            _ => throw new InvalidOperationException($"Unknown locomotion family {family}.")
        };
        if (animator.CurrentAnimation != expected)
            throw new InvalidOperationException($"{role}:{family}:{gait}: selected {animator.CurrentAnimation}.");
        var period = visual.AnimationPlayer.GetAnimation(expected).Length / visual.AnimationPlayer.SpeedScale;
        var warmup = Math.Max(16, (int)Math.Ceiling(period / Step));
        var frames = Math.Max(36, (int)Math.Ceiling(period * 2.0 / Step));
        var samples = new List<Sample>(frames + 1);
        var captureFrames = Enumerable.Range(0, 12)
            .Select(index => (int)Math.Round(index * Math.Min(frames - 1, period / Step) / 12.0)).ToHashSet();
        var folder = ProjectSettings.GlobalizePath($"res://logs/operator-locomotion/{role.ToString().ToLowerInvariant()}");
        Directory.CreateDirectory(folder);
        for (var frame = -warmup; frame <= frames; frame++)
        {
            var before = actor.GlobalPosition;
            actor.GlobalPosition += Vector3.Forward * speed * Step;
            var displacement = actor.GlobalPosition - before;
            animator.Update(Step, new Vector2(displacement.X, displacement.Z).Length() / Step,
                armed, false, false, false, false, false, false);
            var target = actor.GlobalPosition + Vector3.Up;
            camera.Position = target + new Vector3(2.7f, 1.0f, -3.7f);
            camera.LookAt(target, Vector3.Up);
            if (frame >= 0)
            {
                samples.Add(new Sample(frame * Step, actor.GlobalPosition,
                    skeleton.GlobalTransform * skeleton.GetBoneGlobalPose(left).Origin,
                    skeleton.GlobalTransform * skeleton.GetBoneGlobalPose(right).Origin));
            }
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (capture && captureFrames.Contains(frame))
            {
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                var path = Path.Combine(folder, $"{family}_{gait}_{frame:D3}.png");
                var error = GetViewport().GetTexture().GetImage().SavePng(path);
                if (error != Error.Ok)
                    throw new InvalidOperationException($"Could not save locomotion frame {path}: {error}.");
            }
        }
        var leftResult = Measure(samples, speed, leftFoot: true);
        var rightResult = Measure(samples, speed, leftFoot: false);
        var valid = new[] { leftResult, rightResult }.All(result =>
            result.Contacts >= 4 && result.MedianSlip <= 0.35f && result.MaxDrift <= 0.18f && result.Lift >= 0.025f);
        GD.Print($"OPERATOR_FOOT_CONTACT_CHECK role={role} family={family} gait={gait} speed={speed:F3} "
            + $"period={period:F3} left_contacts={leftResult.Contacts} right_contacts={rightResult.Contacts} "
            + $"left_slip={leftResult.MedianSlip:F3} right_slip={rightResult.MedianSlip:F3} "
            + $"left_p90={leftResult.P90Slip:F3} right_p90={rightResult.P90Slip:F3} "
            + $"left_drift={leftResult.MaxDrift:F3} right_drift={rightResult.MaxDrift:F3} "
            + $"left_lift={leftResult.Lift:F3} right_lift={rightResult.Lift:F3} valid={valid}");
        if (!valid)
            failures.Add($"{role}:{family}:{gait}:foot-contact");
        var rows = new List<string> { "time,actor_z,left_x,left_y,left_z,right_x,right_y,right_z" };
        rows.AddRange(samples.Select(sample => string.Join(',', new[]
        {
            sample.Time, sample.Actor.Z, sample.Left.X, sample.Left.Y, sample.Left.Z,
            sample.Right.X, sample.Right.Y, sample.Right.Z
        }.Select(value => value.ToString("F6", CultureInfo.InvariantCulture)))));
        File.WriteAllLines(Path.Combine(folder, $"{family}_{gait}.csv"), rows);
        actor.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private static Result Measure(IReadOnlyList<Sample> samples, float speed, bool leftFoot)
    {
        Vector3 Foot(Sample sample) => leftFoot ? sample.Left : sample.Right;
        var min = samples.Min(sample => Foot(sample).Y - sample.Actor.Y);
        var max = samples.Max(sample => Foot(sample).Y - sample.Actor.Y);
        var ratios = new List<float>();
        Vector3? stanceStart = null;
        var drift = 0.0f;
        for (var index = 1; index < samples.Count; index++)
        {
            var before = samples[index - 1];
            var after = samples[index];
            var a = Foot(before);
            var b = Foot(after);
            var localTravel = (b - after.Actor) - (a - before.Actor);
            var planted = a.Y - before.Actor.Y <= min + 0.04f && b.Y - after.Actor.Y <= min + 0.04f
                && localTravel.Z > 0.0001f;
            if (!planted)
            {
                stanceStart = null;
                continue;
            }
            // A planted foot travels backwards in actor space and stays near
            // one world position while the independently moved actor advances.
            var travel = b - a;
            ratios.Add(new Vector2(travel.X, travel.Z).Length() / (speed * Step));
            stanceStart ??= a;
            var offset = b - stanceStart.Value;
            drift = Mathf.Max(drift, new Vector2(offset.X, offset.Z).Length());
        }
        ratios.Sort();
        return ratios.Count == 0
            ? new Result(0, float.PositiveInfinity, float.PositiveInfinity, 0, max - min)
            : new Result(ratios.Count, ratios[ratios.Count / 2], ratios[(int)((ratios.Count - 1) * .9)], drift, max - min);
    }

    private static int RequiredBone(Skeleton3D skeleton, string name)
    {
        var index = skeleton.FindBone(name);
        return index >= 0 ? index : throw new InvalidOperationException($"Locomotion requires bone {name}.");
    }

    private static IEnumerable<T> Descendants<T>(Node root) where T : Node
    {
        if (root is T match) yield return match;
        foreach (var child in root.GetChildren())
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
    }
}
