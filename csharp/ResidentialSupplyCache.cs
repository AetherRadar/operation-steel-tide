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
    private const int ImportedOpenAnimationFrameCount = 7;

    private static readonly Dictionary<Vector3, BoxMesh> SharedFallbackMeshes = new();
    private static PackedScene? _sharedChestScene;
    private static ArrayMesh? _sharedClosedMesh;
    private static ArrayMesh? _sharedOpenedMesh;
    private static ArrayMesh[] _sharedOpenAnimationMeshes = Array.Empty<ArrayMesh>();
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
    internal bool OpenVisualReady => _opened
        && ((_closedVisual is not null
                && IsInstanceValid(_closedVisual)
                && ReferenceEquals(_closedVisual.Mesh, _sharedOpenedMesh))
            || (_closedVisual is null
                && IsInstanceValid(_lid)
                && Mathf.Abs(_lid.Rotation.X - (_closedLidRotationX - 1.18f)) <= 0.02f));
    internal bool OpenFeedbackReady => _openFeedbackStarted;
    public int VisibleModelPartCount { get; private set; }
    public bool HasVisibleLootHint => GetNodeOrNull<Label3D>("CacheLabel") is not null
        || GetNodeOrNull<Light3D>("CacheGlow") is not null;

    private ResidentialChestPlan? _plan;
    private Node3D _visualRoot = null!;
    private Node3D _lid = null!;
    private MeshInstance3D? _closedVisual;
    private float _closedLidRotationX;
    private bool _opened;
    private bool _openFeedbackStarted;

    private readonly record struct ImportedCratePart(
        string Name,
        Mesh Mesh,
        Transform3D Transform);

    internal static void ReleaseSharedResources()
    {
        SharedFallbackMeshes.Clear();
        _sharedChestScene = null;
        _sharedClosedMesh = null;
        _sharedOpenedMesh = null;
        _sharedOpenAnimationMeshes = Array.Empty<ArrayMesh>();
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
        _openFeedbackStarted = false;
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
        _openFeedbackStarted = false;
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
        PlayOpenFeedback();
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
            && _sharedOpenedMesh is not null
            && _sharedOpenAnimationMeshes.Length == ImportedOpenAnimationFrameCount
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
            var closedParts = new List<ImportedCratePart>(5);
            var openedParts = new List<ImportedCratePart>(5);
            CollectImportedParts(model, Transform3D.Identity, "_a", closedParts);
            CollectImportedParts(model, Transform3D.Identity, "_b", openedParts);
            if (closedParts.Count == 0
                || openedParts.Count != closedParts.Count
                || !TryFindVariantAnchor(closedParts, "old_military_crate_a", out var closedAnchor)
                || !TryFindVariantAnchor(openedParts, "old_military_crate_b", out var openedAnchor))
            {
                return false;
            }

            var animationMeshes = BuildImportedOpenAnimationMeshes(
                closedParts,
                openedParts,
                closedAnchor - openedAnchor);
            if (animationMeshes is null)
            {
                return false;
            }

            _sharedVisiblePartCount = closedParts.Count;
            _sharedOpenAnimationMeshes = animationMeshes;
            _sharedClosedMesh = animationMeshes[0];
            _sharedOpenedMesh = animationMeshes[^1];
            return true;
        }
        finally
        {
            model.Free();
        }
    }

    private static void CollectImportedParts(
        Node parent,
        Transform3D parentTransform,
        string variantSuffix,
        List<ImportedCratePart> parts)
    {
        foreach (var child in parent.GetChildren())
        {
            if (child is not Node3D child3D)
            {
                CollectImportedParts(child, parentTransform, variantSuffix, parts);
                continue;
            }
            var transform = parentTransform * child3D.Transform;
            var name = child3D.Name.ToString();
            if (child3D is MeshInstance3D { Mesh: not null } mesh
                && name.EndsWith(variantSuffix, StringComparison.OrdinalIgnoreCase))
            {
                parts.Add(new ImportedCratePart(
                    name,
                    mesh.Mesh,
                    transform));
            }
            CollectImportedParts(child3D, transform, variantSuffix, parts);
        }
    }

    private static bool TryFindVariantAnchor(
        IReadOnlyList<ImportedCratePart> parts,
        string anchorName,
        out Vector3 origin)
    {
        foreach (var part in parts)
        {
            if (part.Name.Equals(anchorName, StringComparison.OrdinalIgnoreCase))
            {
                origin = part.Transform.Origin;
                return true;
            }
        }
        origin = Vector3.Zero;
        return false;
    }

    private static ArrayMesh[]? BuildImportedOpenAnimationMeshes(
        IReadOnlyList<ImportedCratePart> closedParts,
        IReadOnlyList<ImportedCratePart> openedParts,
        Vector3 alignmentOffset)
    {
        var openedByName = new Dictionary<string, ImportedCratePart>(StringComparer.OrdinalIgnoreCase);
        foreach (var openedPart in openedParts)
        {
            if (!openedByName.TryAdd(VariantBaseName(openedPart.Name), openedPart))
            {
                return null;
            }
        }

        var partPairs = new List<(ImportedCratePart Closed, ImportedCratePart Opened)>(closedParts.Count);
        foreach (var closedPart in closedParts)
        {
            if (!openedByName.TryGetValue(VariantBaseName(closedPart.Name), out var openedPart))
            {
                return null;
            }
            partPairs.Add((closedPart, openedPart));
        }

        var meshes = new ArrayMesh[ImportedOpenAnimationFrameCount];
        for (var frame = 0; frame < meshes.Length; frame++)
        {
            var progress = frame / (float)(meshes.Length - 1);
            var mesh = CombineImportedAnimationFrame(partPairs, alignmentOffset, progress);
            if (mesh is null)
            {
                return null;
            }
            meshes[frame] = mesh;
        }
        return meshes;
    }

    private static string VariantBaseName(string name)
        => name.Length > 2 ? name[..^2] : name;

    private static ArrayMesh? CombineImportedAnimationFrame(
        IReadOnlyList<(ImportedCratePart Closed, ImportedCratePart Opened)> partPairs,
        Vector3 alignmentOffset,
        float progress)
    {
        var surface = new SurfaceTool();
        Godot.Material? material = null;
        var appended = 0;
        var alignment = new Transform3D(Basis.Identity, alignmentOffset);
        foreach (var (closedPart, openedPart) in partPairs)
        {
            var openedTransform = alignment * openedPart.Transform;
            var transform = closedPart.Transform.InterpolateWith(openedTransform, progress);
            var mesh = progress >= 0.999f ? openedPart.Mesh : closedPart.Mesh;
            for (var surfaceIndex = 0; surfaceIndex < mesh.GetSurfaceCount(); surfaceIndex++)
            {
                surface.AppendFrom(mesh, surfaceIndex, transform);
                material ??= mesh.SurfaceGetMaterial(surfaceIndex);
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
            || _sharedOpenAnimationMeshes.Length != ImportedOpenAnimationFrameCount)
        {
            return;
        }

        _closedVisual.Name = "NeutralMilitaryChestOpen";
        _visualRoot = _closedVisual;
    }

    private void PlayOpenFeedback()
    {
        if (_closedVisual is not null
            && IsInstanceValid(_closedVisual)
            && _sharedOpenAnimationMeshes.Length == ImportedOpenAnimationFrameCount)
        {
            var opening = CreateTween();
            for (var frame = 1; frame < _sharedOpenAnimationMeshes.Length; frame++)
            {
                var frameMesh = _sharedOpenAnimationMeshes[frame];
                opening.TweenInterval(0.055f);
                opening.TweenCallback(Callable.From(() =>
                {
                    if (IsInstanceValid(_closedVisual))
                    {
                        _closedVisual.Mesh = frameMesh;
                    }
                }));
            }

            var restPosition = _closedVisual.Position;
            var restRotation = _closedVisual.Rotation;
            var restScale = _closedVisual.Scale;
            _closedVisual.Position = restPosition + Vector3.Up * 0.08f;
            _closedVisual.Rotation = restRotation + new Vector3(-0.045f, 0.0f, 0.0f);
            _closedVisual.Scale = new Vector3(
                restScale.X * 0.97f,
                restScale.Y * 1.08f,
                restScale.Z * 1.04f);

            var rebound = CreateTween().SetParallel();
            rebound.TweenProperty(_closedVisual, "position", restPosition, 0.22f)
                .SetTrans(Tween.TransitionType.Back)
                .SetEase(Tween.EaseType.Out);
            rebound.TweenProperty(_closedVisual, "rotation", restRotation, 0.24f)
                .SetTrans(Tween.TransitionType.Back)
                .SetEase(Tween.EaseType.Out);
            rebound.TweenProperty(_closedVisual, "scale", restScale, 0.26f)
                .SetTrans(Tween.TransitionType.Back)
                .SetEase(Tween.EaseType.Out);
            _openFeedbackStarted = true;
            return;
        }

        if (!IsInstanceValid(_lid))
        {
            return;
        }

        CreateTween()
            .TweenProperty(_lid, "rotation:x", _closedLidRotationX - 1.18f, 0.38f)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.Out);
        _openFeedbackStarted = true;
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
