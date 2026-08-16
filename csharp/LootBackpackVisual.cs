using Godot;

namespace OperationSteelTide;

/// <summary>Neutral tactical backpack presentation used for searchable operator remains.</summary>
[GlobalClass]
public partial class LootBackpackVisual : Node3D
{
    public bool IsOpened { get; private set; }
    public bool VisualReady => IsInstanceValid(_flap) && _partCount >= 12;
    public bool OpenVisualReady => IsOpened
        && IsInstanceValid(_flap)
        && Mathf.Abs(_flap.Rotation.X - OpenFlapRotation) <= 0.03f;
    public float FlapRotationForDiagnostics => IsInstanceValid(_flap) ? _flap.Rotation.X : float.NaN;

    private const float OpenFlapRotation = 1.72f;

    private Node3D _flap = null!;
    private int _partCount;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        BuildBackpack();
    }

    public void Open()
    {
        if (IsOpened)
        {
            return;
        }

        IsOpened = true;
        if (!IsInstanceValid(_flap))
        {
            return;
        }
        CreateTween()
            .SetProcessMode(Tween.TweenProcessMode.Idle)
            .SetPauseMode(Tween.TweenPauseMode.Process)
            .TweenProperty(_flap, "rotation:x", OpenFlapRotation, 0.42f)
            .SetTrans(Tween.TransitionType.Back)
            .SetEase(Tween.EaseType.Out);
    }

    private void BuildBackpack()
    {
        var fabric = Material(new Color(0.105f, 0.125f, 0.105f), 0.04f, 0.92f);
        var reinforced = Material(new Color(0.055f, 0.066f, 0.06f), 0.16f, 0.78f);
        var webbing = Material(new Color(0.18f, 0.16f, 0.105f), 0.02f, 0.88f);
        var hardware = Material(new Color(0.13f, 0.15f, 0.14f), 0.78f, 0.3f);
        var lining = Material(new Color(0.035f, 0.04f, 0.035f), 0.02f, 0.96f);

        Part(this, new BoxMesh { Size = new Vector3(0.7f, 0.78f, 0.34f) }, new Vector3(0.0f, 0.43f, 0.0f), fabric);
        Part(this, new BoxMesh { Size = new Vector3(0.62f, 0.58f, 0.06f) }, new Vector3(0.0f, 0.48f, -0.19f), lining);
        Part(this, new BoxMesh { Size = new Vector3(0.18f, 0.44f, 0.22f) }, new Vector3(-0.4f, 0.36f, 0.02f), fabric);
        Part(this, new BoxMesh { Size = new Vector3(0.18f, 0.44f, 0.22f) }, new Vector3(0.4f, 0.36f, 0.02f), fabric);
        Part(this, new BoxMesh { Size = new Vector3(0.54f, 0.22f, 0.16f) }, new Vector3(0.0f, 0.14f, -0.16f), reinforced);

        foreach (var x in new[] { -0.24f, 0.24f })
        {
            Part(this, new BoxMesh { Size = new Vector3(0.075f, 0.76f, 0.055f) }, new Vector3(x, 0.44f, 0.2f), webbing);
            Part(this, new BoxMesh { Size = new Vector3(0.1f, 0.08f, 0.07f) }, new Vector3(x, 0.27f, -0.24f), hardware);
        }

        foreach (var y in new[] { 0.24f, 0.48f, 0.7f })
        {
            Part(this, new BoxMesh { Size = new Vector3(0.6f, 0.045f, 0.045f) }, new Vector3(0.0f, y, -0.225f), webbing);
        }

        Part(this, new TorusMesh
        {
            InnerRadius = 0.08f,
            OuterRadius = 0.12f,
            Rings = 12,
            RingSegments = 8
        }, new Vector3(0.0f, 0.91f, 0.03f), webbing, new Vector3(Mathf.Pi * 0.5f, 0.0f, 0.0f));

        _flap = new Node3D
        {
            Name = "LootBackpackFlap",
            Position = new Vector3(0.0f, 0.82f, -0.19f)
        };
        AddChild(_flap);
        Part(_flap, new BoxMesh { Size = new Vector3(0.64f, 0.5f, 0.085f) }, new Vector3(0.0f, -0.25f, -0.02f), fabric);
        Part(_flap, new BoxMesh { Size = new Vector3(0.5f, 0.18f, 0.06f) }, new Vector3(0.0f, -0.27f, -0.075f), reinforced);
        Part(_flap, new BoxMesh { Size = new Vector3(0.08f, 0.24f, 0.055f) }, new Vector3(0.0f, -0.31f, -0.11f), webbing);
        Part(_flap, new BoxMesh { Size = new Vector3(0.13f, 0.09f, 0.065f) }, new Vector3(0.0f, -0.38f, -0.145f), hardware);
    }

    private MeshInstance3D Part(
        Node parent,
        PrimitiveMesh mesh,
        Vector3 position,
        Godot.Material material,
        Vector3 rotation = default)
    {
        var part = new MeshInstance3D
        {
            Name = $"LootBackpackPart_{_partCount++:00}",
            Mesh = mesh,
            Position = position,
            Rotation = rotation,
            MaterialOverride = material
        };
        parent.AddChild(part);
        return part;
    }

    private static StandardMaterial3D Material(Color color, float metallic, float roughness)
    {
        return new StandardMaterial3D
        {
            AlbedoColor = color,
            Metallic = metallic,
            Roughness = roughness
        };
    }
}
