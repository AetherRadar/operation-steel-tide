using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public interface ILootSource
{
    Node3D LootNode { get; }
    List<LootItem> Loot { get; }
    bool IsSearchable { get; }
    float SearchDuration { get; }
    string DisplayName(string language);
    void OnSearched();
}

/// <summary>A loot source that conceals its contents until its opening interaction completes.</summary>
public interface IOpenableLootSource
{
    bool IsOpened { get; }
}

/// <summary>Permanent KIA remains for a squadmate after their revive budget is spent.</summary>
[GlobalClass]
public partial class SquadBodyBag : StaticBody3D, ILootSource, IOpenableLootSource
{
    public string EnglishName { get; set; } = "Operator body bag";
    public string ChineseName { get; set; } = "干员遗体袋";
    public List<LootItem> Loot { get; } = new();
    public Node3D LootNode => this;
    public bool IsSearchable => true;
    public float SearchDuration => 0.85f;
    public bool IsOpened => _tagged;
    internal bool ClosedVisualReady => !_tagged
        && IsInstanceValid(_flap)
        && Mathf.Abs(_flap.Rotation.X) <= 0.01f
        && FlapPartCountForDiagnostics >= 2;
    internal bool OpenVisualReady => _tagged
        && IsInstanceValid(_flap)
        && Mathf.Abs(_flap.Rotation.X + 1.08f) <= 0.03f
        && FlapPartCountForDiagnostics >= 2;
    internal int FlapPartCountForDiagnostics => IsInstanceValid(_flap)
        ? _flap.GetChildCount()
        : 0;

    private bool _tagged;
    private Node3D _flap = null!;

    public override void _Ready()
    {
        CollisionLayer = 1;
        CollisionMask = 0;
        AddToGroup("squad_body_bags");
        AddChild(new CollisionShape3D
        {
            Position = new Vector3(0.0f, 0.22f, 0.0f),
            Shape = new BoxShape3D { Size = new Vector3(1.35f, 0.38f, 0.62f) }
        });
        BuildBag();
        AddChild(new Label3D
        {
            Position = new Vector3(0.0f, 0.72f, 0.0f),
            Text = "BODY BAG  //  F LOOT",
            FontSize = 16,
            OutlineSize = 5,
            Modulate = new Color(0.85f, 0.35f, 0.28f),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            VisibilityRangeEnd = 18.0f
        });
    }

    public string DisplayName(string language) => GameLocalization.IsChinese(language) ? ChineseName : EnglishName;

    public void OnSearched()
    {
        if (_tagged)
        {
            return;
        }
        _tagged = true;
        CreateTween()
            .SetProcessMode(Tween.TweenProcessMode.Idle)
            .SetPauseMode(Tween.TweenPauseMode.Process)
            .TweenProperty(_flap, "rotation:x", -1.08f, 0.36f)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.Out);
    }

    private void BuildBag()
    {
        var fabric = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.08f, 0.1f, 0.09f),
            Roughness = 0.92f,
            Metallic = 0.05f
        };
        var stripe = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.55f, 0.08f, 0.05f),
            Roughness = 0.7f,
            EmissionEnabled = true,
            Emission = new Color(0.45f, 0.05f, 0.02f),
            EmissionEnergyMultiplier = 0.6f
        };
        var zipper = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.35f, 0.38f, 0.36f),
            Metallic = 0.75f,
            Roughness = 0.35f
        };
        // Main bag body (reads as a crate/box silhouette from a distance).
        AddChild(new MeshInstance3D
        {
            Position = new Vector3(0.0f, 0.2f, 0.0f),
            Mesh = new BoxMesh { Size = new Vector3(1.3f, 0.32f, 0.58f) },
            MaterialOverride = fabric
        });
        _flap = new Node3D
        {
            Name = "BodyBagFlap",
            Position = new Vector3(0.0f, 0.38f, 0.24f)
        };
        AddChild(_flap);
        _flap.AddChild(new MeshInstance3D
        {
            Name = "BodyBagFlapPanel",
            Position = new Vector3(0.0f, 0.0f, -0.24f),
            Mesh = new BoxMesh { Size = new Vector3(1.22f, 0.06f, 0.5f) },
            MaterialOverride = fabric
        });
        // Hazard stripe + zipper line so it is obviously not a living operator.
        AddChild(new MeshInstance3D
        {
            Position = new Vector3(0.0f, 0.28f, 0.3f),
            Mesh = new BoxMesh { Size = new Vector3(1.28f, 0.08f, 0.04f) },
            MaterialOverride = stripe
        });
        _flap.AddChild(new MeshInstance3D
        {
            Name = "BodyBagFlapZipper",
            Position = new Vector3(0.0f, -0.02f, -0.24f),
            Mesh = new BoxMesh { Size = new Vector3(1.1f, 0.03f, 0.04f) },
            MaterialOverride = zipper
        });
        AddChild(new MeshInstance3D
        {
            Position = new Vector3(0.55f, 0.22f, 0.0f),
            Mesh = new BoxMesh { Size = new Vector3(0.08f, 0.18f, 0.5f) },
            MaterialOverride = stripe
        });
    }
}

[GlobalClass]
public partial class WeaponCase : StaticBody3D, ILootSource, IOpenableLootSource
{
    public string EnglishName { get; set; } = "Weapon case";
    public string ChineseName { get; set; } = "武器箱";
    public List<LootItem> Loot { get; } = new();
    public Node3D LootNode => this;
    public bool IsSearchable => true;
    public float SearchDuration => 0.9f;
    public bool IsOpened => _opened;
    public bool OpenVisualReady => _opened
        && IsInstanceValid(_lid)
        && Mathf.Abs(_lid.Rotation.X + 1.45f) <= 0.03f;

    private Node3D _lid = null!;
    private bool _opened;

    public override void _Ready()
    {
        CollisionLayer = 1;
        CollisionMask = 0;
        AddChild(new CollisionShape3D
        {
            Position = new Vector3(0, 0.34f, 0),
            Shape = new BoxShape3D { Size = new Vector3(1.22f, 0.68f, 0.58f) }
        });
        BuildCase();
    }

    public string DisplayName(string language) => GameLocalization.IsChinese(language) ? ChineseName : EnglishName;

    public void OnSearched()
    {
        if (_opened)
        {
            return;
        }
        _opened = true;
        CreateTween()
            .SetProcessMode(Tween.TweenProcessMode.Idle)
            .SetPauseMode(Tween.TweenPauseMode.Process)
            .TweenProperty(_lid, "rotation:x", -1.45f, 0.38f)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.Out);
    }

    private void BuildCase()
    {
        var shell = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.12f, 0.16f, 0.11f),
            Metallic = 0.34f,
            Roughness = 0.72f
        };
        var trim = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.035f, 0.043f, 0.04f),
            Metallic = 0.82f,
            Roughness = 0.3f
        };
        AddChild(new MeshInstance3D
        {
            Position = new Vector3(0, 0.28f, 0),
            Mesh = new BoxMesh { Size = new Vector3(1.18f, 0.5f, 0.55f) },
            MaterialOverride = shell
        });
        _lid = new Node3D { Position = new Vector3(0, 0.57f, 0.27f) };
        AddChild(_lid);
        _lid.AddChild(new MeshInstance3D
        {
            Position = new Vector3(0, 0, -0.27f),
            Mesh = new BoxMesh { Size = new Vector3(1.2f, 0.12f, 0.58f) },
            MaterialOverride = shell
        });
        for (var side = -1; side <= 1; side += 2)
        {
            AddChild(new MeshInstance3D
            {
                Position = new Vector3(side * 0.48f, 0.29f, -0.285f),
                Mesh = new BoxMesh { Size = new Vector3(0.13f, 0.28f, 0.04f) },
                MaterialOverride = trim
            });
        }
        AddChild(new MeshInstance3D
        {
            Position = new Vector3(0, 0.32f, -0.3f),
            Mesh = new BoxMesh { Size = new Vector3(0.24f, 0.1f, 0.055f) },
            MaterialOverride = trim
        });
    }
}
