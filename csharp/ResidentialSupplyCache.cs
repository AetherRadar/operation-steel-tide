using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public enum ResidentialCacheKind
{
    FamilyStash,
    MedicalCabinet,
    EvacuationLocker,
    WorkshopLocker,
    SecurityArmory,
    SmugglerCache,
    CommunityPantry
}

[GlobalClass]
public partial class ResidentialSupplyCache : StaticBody3D, ILootSource
{
    private static readonly Dictionary<Vector3, BoxMesh> SharedBoxMeshes = new();

    public ResidentialCacheKind Kind { get; private set; }
    public int TowerIndex { get; private set; }
    public int FloorIndex { get; private set; }
    public List<LootItem> Loot { get; } = new();
    public Node3D LootNode => this;
    public bool IsSearchable => true;
    public float SearchDuration => 0.65f;

    private Node3D _door = null!;
    private Label3D _label = null!;
    private string _language = "en";
    private bool _opened;
    private int _partCounter;

    public void Configure(ResidentialCacheKind kind, int towerIndex, int floorIndex, IEnumerable<LootItem> loot)
    {
        Kind = kind;
        TowerIndex = towerIndex;
        FloorIndex = floorIndex;
        Loot.Clear();
        Loot.AddRange(loot);
    }

    public void SetLanguage(string language)
    {
        _language = GameLocalization.IsChinese(language) ? "zh" : "en";
        if (IsInstanceValid(_label))
        {
            _label.Text = CacheLabelText();
        }
    }

    public override void _Ready()
    {
        CollisionLayer = 1;
        CollisionMask = 0;
        AddToGroup("residential_caches");
        BuildCache();
    }

    public string DisplayName(string language)
    {
        var key = Kind switch
        {
            ResidentialCacheKind.MedicalCabinet => "residential_cache_medical",
            ResidentialCacheKind.EvacuationLocker => "residential_cache_evac",
            ResidentialCacheKind.WorkshopLocker => "residential_cache_workshop",
            ResidentialCacheKind.SecurityArmory => "residential_cache_security",
            ResidentialCacheKind.SmugglerCache => "residential_cache_smuggler",
            ResidentialCacheKind.CommunityPantry => "residential_cache_pantry",
            _ => "residential_cache_family"
        };
        var english = Kind switch
        {
            ResidentialCacheKind.MedicalCabinet => "Community medical cabinet",
            ResidentialCacheKind.EvacuationLocker => "Evacuation supply locker",
            ResidentialCacheKind.WorkshopLocker => "Maintenance tool locker",
            ResidentialCacheKind.SecurityArmory => "Community security armory",
            ResidentialCacheKind.SmugglerCache => "Concealed contraband cache",
            ResidentialCacheKind.CommunityPantry => "Community pantry reserve",
            _ => "Resident emergency stash"
        };
        return GameLocalization.Get(key, language, english);
    }

    public void OnSearched()
    {
        if (_opened)
        {
            return;
        }
        _opened = true;
        CreateTween()
            .TweenProperty(_door, "rotation:y", -1.35f, 0.32f)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.Out);
    }

    private void BuildCache()
    {
        var accent = Kind switch
        {
            ResidentialCacheKind.MedicalCabinet => new Color(0.28f, 0.88f, 0.58f),
            ResidentialCacheKind.EvacuationLocker => new Color(0.95f, 0.58f, 0.18f),
            ResidentialCacheKind.WorkshopLocker => new Color(0.95f, 0.72f, 0.16f),
            ResidentialCacheKind.SecurityArmory => new Color(0.28f, 0.58f, 0.82f),
            ResidentialCacheKind.SmugglerCache => new Color(0.72f, 0.3f, 0.22f),
            ResidentialCacheKind.CommunityPantry => new Color(0.45f, 0.68f, 0.36f),
            _ => new Color(0.62f, 0.52f, 0.4f)
        };
        var tall = Kind is ResidentialCacheKind.MedicalCabinet
            or ResidentialCacheKind.EvacuationLocker
            or ResidentialCacheKind.WorkshopLocker
            or ResidentialCacheKind.SecurityArmory
            or ResidentialCacheKind.CommunityPantry;
        var size = tall ? new Vector3(1.05f, 1.45f, 0.52f) : new Vector3(1.18f, 0.62f, 0.72f);
        var center = new Vector3(0, size.Y * 0.5f, 0);
        AddChild(new CollisionShape3D
        {
            Name = "CacheCollision",
            Position = center,
            Shape = new BoxShape3D { Size = size }
        });

        var shell = Material(accent * 0.62f, 0.42f, 0.58f);
        var trim = Material(new Color(0.055f, 0.065f, 0.062f), 0.75f, 0.34f);
        var glow = Material(accent, 0.08f, 0.32f, true);
        Part(this, SharedBox(size), center, shell);
        Part(this, SharedBox(new Vector3(size.X + 0.05f, 0.07f, size.Z + 0.04f)), new Vector3(0, size.Y + 0.02f, 0), trim);

        _door = new Node3D
        {
            Name = "CacheDoor",
            Position = new Vector3(-size.X * 0.5f, size.Y * 0.5f, -size.Z * 0.51f)
        };
        AddChild(_door);
        Part(
            _door,
            SharedBox(new Vector3(size.X - 0.08f, size.Y - 0.1f, 0.055f)),
            new Vector3(size.X * 0.5f, 0, 0),
            shell);
        Part(
            _door,
            SharedBox(new Vector3(0.12f, 0.18f, 0.07f)),
            new Vector3(size.X - 0.17f, 0, -0.03f),
            trim);

        if (Kind == ResidentialCacheKind.MedicalCabinet)
        {
            Part(_door, SharedBox(new Vector3(0.38f, 0.08f, 0.065f)), new Vector3(size.X * 0.5f, 0, -0.04f), glow);
            Part(_door, SharedBox(new Vector3(0.08f, 0.38f, 0.065f)), new Vector3(size.X * 0.5f, 0, -0.045f), glow);
        }
        else
        {
            Part(_door, SharedBox(new Vector3(size.X * 0.58f, 0.08f, 0.065f)), new Vector3(size.X * 0.5f, size.Y * 0.2f, -0.04f), glow);
        }

        _label = new Label3D
        {
            Name = "CacheLabel",
            Position = new Vector3(0, size.Y + 0.28f, 0),
            Text = CacheLabelText(),
            FontSize = 15,
            OutlineSize = 5,
            Modulate = accent,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            VisibilityRangeEnd = 16.0f
        };
        _label.AddToGroup("residential_localized_labels");
        AddChild(_label);
    }

    private string CacheLabelText()
    {
        var key = Kind switch
        {
            ResidentialCacheKind.MedicalCabinet => "residential_cache_label_medical",
            ResidentialCacheKind.EvacuationLocker => "residential_cache_label_evac",
            ResidentialCacheKind.WorkshopLocker => "residential_cache_label_tools",
            ResidentialCacheKind.SecurityArmory => "residential_cache_label_security",
            ResidentialCacheKind.SmugglerCache => "residential_cache_label_concealed",
            ResidentialCacheKind.CommunityPantry => "residential_cache_label_reserve",
            _ => "residential_cache_label_stash"
        };
        var english = Kind switch
        {
            ResidentialCacheKind.MedicalCabinet => "MEDICAL",
            ResidentialCacheKind.EvacuationLocker => "EVAC SUPPLY",
            ResidentialCacheKind.WorkshopLocker => "TOOLS",
            ResidentialCacheKind.SecurityArmory => "SECURITY",
            ResidentialCacheKind.SmugglerCache => "CONCEALED",
            ResidentialCacheKind.CommunityPantry => "RESERVE",
            _ => "STASH"
        };
        return GameLocalization.Get(key, _language, english);
    }

    private static StandardMaterial3D Material(Color color, float metallic, float roughness, bool emission = false)
    {
        return new StandardMaterial3D
        {
            AlbedoColor = color,
            Metallic = metallic,
            Roughness = roughness,
            EmissionEnabled = emission,
            Emission = emission ? color : Colors.Black,
            EmissionEnergyMultiplier = emission ? 1.35f : 1.0f
        };
    }

    private static BoxMesh SharedBox(Vector3 size)
    {
        if (!SharedBoxMeshes.TryGetValue(size, out var mesh))
        {
            mesh = new BoxMesh { Size = size };
            SharedBoxMeshes[size] = mesh;
        }
        return mesh;
    }

    private MeshInstance3D Part(Node parent, PrimitiveMesh mesh, Vector3 position, Godot.Material material)
    {
        var part = new MeshInstance3D
        {
            Name = $"CachePart_{_partCounter++:00}",
            Mesh = mesh,
            Position = position,
            MaterialOverride = material
        };
        parent.AddChild(part);
        return part;
    }
}
