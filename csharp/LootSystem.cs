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

[GlobalClass]
public partial class WeaponCase : StaticBody3D, ILootSource
{
    public string EnglishName { get; set; } = "Weapon case";
    public string ChineseName { get; set; } = "武器箱";
    public List<LootItem> Loot { get; } = new();
    public Node3D LootNode => this;
    public bool IsSearchable => true;
    public float SearchDuration => 0.9f;

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
