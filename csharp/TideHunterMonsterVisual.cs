using System;
using System.Linq;
using Godot;

namespace OperationSteelTide;

internal sealed class TideHunterMonsterVisual
{
    private static readonly string[] RequiredAnimations = { "idle", "walk", "run" };
    private readonly MeshInstance3D[] _meshes;
    private string _currentAnimation = string.Empty;

    public TideHunterMonsterVisual(Node3D root)
    {
        Root = root;
        AnimationPlayer = CombatModelLibrary.RequireAnimationPlayer(root);
        _meshes = CombatModelLibrary.MeshesBelow(root).ToArray();
        if (_meshes.Length != 1)
        {
            throw new InvalidOperationException(
                $"Tide Hunter monster must contain one coherent skinned mesh; found {_meshes.Length}.");
        }
        foreach (var animationName in RequiredAnimations)
        {
            if (!AnimationPlayer.HasAnimation(animationName))
            {
                throw new InvalidOperationException(
                    $"Tide Hunter monster is missing animation {animationName}.");
            }
            var animation = AnimationPlayer.GetAnimation(animationName);
            if (animation is not null)
            {
                animation.LoopMode = Animation.LoopModeEnum.Linear;
            }
        }
        Play("idle", 1.0f, immediate: true);
    }

    public Node3D Root { get; }
    public AnimationPlayer AnimationPlayer { get; }
    public string CurrentAnimation => _currentAnimation;
    public int MeshCount => _meshes.Length;
    public int AnimationCount => RequiredAnimations.Length;
    public bool DeathStarted { get; private set; }

    public void Update(float speed)
    {
        if (DeathStarted)
        {
            return;
        }
        if (speed <= 0.16f)
        {
            Play("idle", 1.0f);
        }
        else if (speed < 2.75f)
        {
            Play("walk", Mathf.Clamp(speed / 2.1f, 0.72f, 1.35f));
        }
        else
        {
            Play("run", Mathf.Clamp(speed / 3.6f, 0.78f, 1.4f));
        }
    }

    public void SetPhase(int phase)
    {
        StandardMaterial3D? overlay = phase switch
        {
            2 => PhaseOverlay(new Color(0.05f, 0.82f, 0.66f, 0.10f)),
            3 => PhaseOverlay(new Color(1.0f, 0.12f, 0.045f, 0.16f)),
            _ => null
        };
        foreach (var mesh in _meshes)
        {
            mesh.MaterialOverlay = overlay;
        }
    }

    public Tween BeginDeath(bool fallLeft)
    {
        DeathStarted = true;
        AnimationPlayer.Stop(keepState: true);
        var tween = Root.CreateTween().SetParallel(true);
        tween.TweenProperty(Root, "rotation:z", fallLeft ? -Mathf.Pi * 0.48f : Mathf.Pi * 0.48f, 0.58f)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.In);
        tween.TweenProperty(Root, "position:y", 0.10f, 0.58f)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.In);
        return tween;
    }

    private void Play(string name, float speed, bool immediate = false)
    {
        if (!immediate && _currentAnimation == name)
        {
            AnimationPlayer.SpeedScale = speed;
            return;
        }
        _currentAnimation = name;
        AnimationPlayer.SpeedScale = speed;
        AnimationPlayer.Play(name, immediate ? 0.0 : 0.18);
    }

    private static StandardMaterial3D PhaseOverlay(Color color)
        => new()
        {
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            AlbedoColor = color,
            Metallic = 0.0f,
            Roughness = 0.68f,
            EmissionEnabled = true,
            Emission = new Color(color.R, color.G, color.B),
            EmissionEnergyMultiplier = 0.6f
        };
}

internal static class TideHunterMonsterLibrary
{
    internal const string ScenePath =
        "res://assets/models/tide_hunter_monster/tide_hunter_monster.glb";
    private const float PresentationHeight = 2.32f;
    private const float SourceHeight = 1.87f;

    public static TideHunterMonsterVisual Instantiate()
    {
        var scene = GD.Load<PackedScene>(ScenePath)
            ?? throw new InvalidOperationException($"Required Tide Hunter model could not load: {ScenePath}");
        var source = scene.Instantiate<Node3D>();
        RequireNode(source, "TideHunterMonster");
        RequireNode(source, "TideHunterRig");
        RequireNode(source, "TideHunterMesh");
        var presentation = new Node3D
        {
            Name = "TideHunterPresentation",
            RotationDegrees = new Vector3(0.0f, 180.0f, 0.0f),
            Scale = Vector3.One * (PresentationHeight / SourceHeight)
        };
        presentation.AddChild(source);
        var wrapper = new Node3D { Name = "AuthoredTideHunterMonster" };
        wrapper.AddChild(presentation);
        return new TideHunterMonsterVisual(wrapper);
    }

    private static void RequireNode(Node3D root, string name)
    {
        if (root.Name != name && root.FindChild(name, recursive: true, owned: false) is null)
        {
            root.Free();
            throw new InvalidOperationException($"Tide Hunter model is missing required node {name}.");
        }
    }
}
