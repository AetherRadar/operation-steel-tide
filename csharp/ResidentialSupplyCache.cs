using System;
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
public partial class ResidentialSupplyCache : StaticBody3D, ILootSource, IDeferredLootSource
{
    public const string NeutralModelPath = "res://assets/models/old_military_crate/old_military_crate.gltf";

    private static readonly Dictionary<Vector3, BoxMesh> SharedFallbackMeshes = new();
    private static PackedScene? _sharedChestScene;
    private static ArrayMesh? _sharedClosedMesh;
    private static ArrayMesh? _sharedBodyMesh;
    private static Mesh? _sharedLidMesh;
    private static Transform3D _sharedLidTransform = Transform3D.Identity;
    private static int _sharedVisiblePartCount;

    public event Action<ResidentialSupplyCache>? FirstOpened;

    public ResidentialCacheKind Kind { get; private set; }
    public int TowerIndex { get; private set; }
    public int FloorIndex { get; private set; }
    public ResidentialRoomId? RoomId { get; private set; }
    public ResidentialRoomArchetype Archetype { get; private set; }
    public ResidentialRoomEventKind EventKind { get; private set; }
    public int GuardCount { get; private set; }
    public LootGrade? RevealedGrade { get; private set; }
    public int ResolutionCount { get; private set; }
    public int OpenEventCount { get; private set; }
    public List<LootItem> Loot { get; } = new();
    public Node3D LootNode => this;
    public bool ContentsResolved { get; private set; }
    public bool MayContainWeapon => !ContentsResolved
        || Loot.Exists(item => item.Kind == LootItemKind.Weapon && item.Weapon is not null);
    public bool IsSearchable => !ContentsResolved || Loot.Count > 0;
    public float SearchDuration => 0.65f;
    public bool NeutralVisualReady => IsInstanceValid(_visualRoot) && VisibleModelPartCount > 0;
    public int VisibleModelPartCount { get; private set; }
    public bool HasVisibleLootHint => GetNodeOrNull<Label3D>("CacheLabel") is not null
        || GetNodeOrNull<Light3D>("CacheGlow") is not null;

    private ResidentialChestPlan? _plan;
    private Node3D _visualRoot = null!;
    private Node3D _lid = null!;
    private MeshInstance3D? _closedVisual;
    private float _closedLidRotationX;
    private bool _opened;

    private readonly record struct ImportedCratePart(
        Mesh Mesh,
        Transform3D Transform,
        bool IsLid);

    internal static void ReleaseSharedResources()
    {
        SharedFallbackMeshes.Clear();
        _sharedChestScene = null;
        _sharedClosedMesh = null;
        _sharedBodyMesh = null;
        _sharedLidMesh = null;
        _sharedLidTransform = Transform3D.Identity;
        _sharedVisiblePartCount = 0;
    }

    public void Configure(
        ResidentialCacheKind kind,
        int towerIndex,
        int floorIndex,
        IEnumerable<LootItem> loot)
    {
        Kind = kind;
        TowerIndex = towerIndex;
        FloorIndex = floorIndex;
        RoomId = null;
        Archetype = ResidentialRoomArchetype.FamilyApartment;
        EventKind = ResidentialRoomEventKind.None;
        GuardCount = 0;
        RevealedGrade = null;
        ResolutionCount = 0;
        OpenEventCount = 0;
        _plan = null;
        _opened = false;
        Loot.Clear();
        Loot.AddRange(loot);
        ContentsResolved = true;
    }

    public void ConfigureRoom(ResidentialChestPlan plan)
    {
        _plan = plan;
        Kind = plan.CacheKind;
        TowerIndex = plan.RoomId.TowerIndex;
        FloorIndex = plan.RoomId.FloorIndex;
        RoomId = plan.RoomId;
        Archetype = plan.Archetype;
        EventKind = plan.EventKind;
        GuardCount = plan.GuardCount;
        RevealedGrade = null;
        ResolutionCount = 0;
        OpenEventCount = 0;
        _opened = false;
        Loot.Clear();
        ContentsResolved = false;
    }

    public void SetLanguage(string language)
    {
        _ = language;
    }

    public override void _Ready()
    {
        CollisionLayer = 1;
        CollisionMask = 0;
        AddToGroup("residential_caches");
        BuildCache();
    }

    public string DisplayName(string language)
        => GameLocalization.Get("residential_cache_family", language, "Residential supply chest");

    public void OnSearched()
    {
        ResolveContents();
        if (_opened)
        {
            return;
        }

        _opened = true;
        PrepareImportedOpenVisual();
        if (IsInstanceValid(_lid))
        {
            CreateTween()
                .TweenProperty(_lid, "rotation:x", _closedLidRotationX - 1.18f, 0.38f)
                .SetTrans(Tween.TransitionType.Quad)
                .SetEase(Tween.EaseType.Out);
        }
        OpenEventCount++;
        FirstOpened?.Invoke(this);
    }

    private void ResolveContents()
    {
        if (ContentsResolved)
        {
            return;
        }
        if (_plan is not ResidentialChestPlan plan)
        {
            ContentsResolved = true;
            return;
        }

        var resolution = ResidentialRoomLootRules.Resolve(plan);
        Loot.Clear();
        Loot.AddRange(resolution.Items);
        RevealedGrade = resolution.Grade;
        ContentsResolved = true;
        ResolutionCount++;
    }

    private void BuildCache()
    {
        AddChild(new CollisionShape3D
        {
            Name = "CacheCollision",
            Position = new Vector3(0.0f, 0.29f, 0.0f),
            Shape = new BoxShape3D { Size = new Vector3(1.18f, 0.58f, 0.78f) }
        });

        if (EnsureSharedImportedMeshes())
        {
            _closedVisual = new MeshInstance3D
            {
                Name = "NeutralMilitaryChest",
                Mesh = _sharedClosedMesh,
                Position = new Vector3(0.53f, 0.02f, 0.0f),
                Scale = Vector3.One * 1.06f,
                VisibilityRangeEnd = 52.0f,
                VisibilityRangeEndMargin = 6.0f
            };
            _visualRoot = _closedVisual;
            VisibleModelPartCount = _sharedVisiblePartCount;
            AddChild(_closedVisual);
            return;
        }

        BuildFallbackChest();
    }

    private static bool EnsureSharedImportedMeshes()
    {
        if (_sharedClosedMesh is not null
            && _sharedBodyMesh is not null
            && _sharedLidMesh is not null
            && _sharedVisiblePartCount > 0)
        {
            return true;
        }

        _sharedChestScene ??= GD.Load<PackedScene>(NeutralModelPath);
        if (_sharedChestScene?.Instantiate() is not Node3D model)
        {
            return false;
        }

        try
        {
            var parts = new List<ImportedCratePart>(5);
            CollectImportedParts(model, Transform3D.Identity, parts);
            _sharedVisiblePartCount = parts.Count;
            _sharedClosedMesh = CombineImportedParts(parts, includeLid: true);
            _sharedBodyMesh = CombineImportedParts(parts, includeLid: false);
            foreach (var part in parts)
            {
                if (!part.IsLid)
                {
                    continue;
                }
                _sharedLidMesh = part.Mesh;
                _sharedLidTransform = part.Transform;
                break;
            }
            return _sharedClosedMesh is not null
                && _sharedBodyMesh is not null
                && _sharedLidMesh is not null
                && _sharedVisiblePartCount > 0;
        }
        finally
        {
            model.Free();
        }
    }

    private static void CollectImportedParts(
        Node parent,
        Transform3D parentTransform,
        List<ImportedCratePart> parts)
    {
        foreach (var child in parent.GetChildren())
        {
            if (child is not Node3D child3D)
            {
                CollectImportedParts(child, parentTransform, parts);
                continue;
            }
            var transform = parentTransform * child3D.Transform;
            var name = child3D.Name.ToString();
            if (child3D is MeshInstance3D { Mesh: not null } mesh
                && name.EndsWith("_a", StringComparison.OrdinalIgnoreCase))
            {
                parts.Add(new ImportedCratePart(
                    mesh.Mesh,
                    transform,
                    name.Equals("old_military_crate_lid_a", StringComparison.OrdinalIgnoreCase)));
            }
            CollectImportedParts(child3D, transform, parts);
        }
    }

    private static ArrayMesh? CombineImportedParts(
        IReadOnlyList<ImportedCratePart> parts,
        bool includeLid)
    {
        var surface = new SurfaceTool();
        Godot.Material? material = null;
        var appended = 0;
        foreach (var part in parts)
        {
            if (!includeLid && part.IsLid)
            {
                continue;
            }
            for (var surfaceIndex = 0; surfaceIndex < part.Mesh.GetSurfaceCount(); surfaceIndex++)
            {
                surface.AppendFrom(part.Mesh, surfaceIndex, part.Transform);
                material ??= part.Mesh.SurfaceGetMaterial(surfaceIndex);
                appended++;
            }
        }
        if (appended == 0)
        {
            surface.Dispose();
            return null;
        }
        surface.SetMaterial(material);
        var combined = surface.Commit();
        surface.Dispose();
        return combined;
    }

    private void PrepareImportedOpenVisual()
    {
        if (_closedVisual is null
            || !IsInstanceValid(_closedVisual)
            || _sharedBodyMesh is null
            || _sharedLidMesh is null)
        {
            return;
        }

        var openRoot = new Node3D
        {
            Name = "NeutralMilitaryChestOpen",
            Transform = _closedVisual.Transform
        };
        AddChild(openRoot);
        openRoot.AddChild(new MeshInstance3D
        {
            Name = "NeutralMilitaryChestBody",
            Mesh = _sharedBodyMesh,
            VisibilityRangeEnd = 52.0f,
            VisibilityRangeEndMargin = 6.0f
        });
        _lid = new MeshInstance3D
        {
            Name = "NeutralMilitaryChestLid",
            Mesh = _sharedLidMesh,
            Transform = _sharedLidTransform,
            VisibilityRangeEnd = 52.0f,
            VisibilityRangeEndMargin = 6.0f
        };
        openRoot.AddChild(_lid);
        _closedLidRotationX = _lid.Rotation.X;
        _closedVisual.QueueFree();
        _closedVisual = null;
        _visualRoot = openRoot;
    }

    private void BuildFallbackChest()
    {
        _visualRoot = new Node3D { Name = "NeutralMilitaryChestFallback" };
        AddChild(_visualRoot);
        var shell = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.19f, 0.22f, 0.16f),
            Metallic = 0.28f,
            Roughness = 0.76f
        };
        var trim = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.055f, 0.062f, 0.052f),
            Metallic = 0.72f,
            Roughness = 0.38f
        };
        AddFallbackPart(_visualRoot, new Vector3(1.12f, 0.46f, 0.72f), new Vector3(0, 0.24f, 0), shell);
        _lid = new Node3D
        {
            Name = "NeutralChestLid",
            Position = new Vector3(0, 0.49f, 0.34f)
        };
        _visualRoot.AddChild(_lid);
        AddFallbackPart(_lid, new Vector3(1.16f, 0.12f, 0.76f), new Vector3(0, 0, -0.34f), shell);
        AddFallbackPart(_visualRoot, new Vector3(0.22f, 0.12f, 0.05f), new Vector3(0, 0.31f, -0.39f), trim);
        _closedLidRotationX = 0.0f;
    }

    private void AddFallbackPart(Node parent, Vector3 size, Vector3 position, Godot.Material material)
    {
        if (!SharedFallbackMeshes.TryGetValue(size, out var mesh))
        {
            mesh = new BoxMesh { Size = size };
            SharedFallbackMeshes[size] = mesh;
        }
        parent.AddChild(new MeshInstance3D
        {
            Name = $"NeutralChestPart_{VisibleModelPartCount:00}",
            Mesh = mesh,
            Position = position,
            MaterialOverride = material
        });
        VisibleModelPartCount++;
    }
}
