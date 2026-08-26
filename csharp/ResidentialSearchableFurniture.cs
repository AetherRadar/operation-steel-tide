using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public enum ResidentialFurnitureKind
{
    Nightstand,
    Refrigerator,
    Wardrobe,
    DeskDrawers
}

[GlobalClass]
public partial class ResidentialSearchableFurniture : StaticBody3D, ILootSource
{
    private static readonly Dictionary<Vector3, BoxMesh> SharedBoxMeshes = new();

    internal static void ReleaseSharedResources()
    {
        SharedBoxMeshes.Clear();
        ResidentialAuthoredPropLibrary.ReleaseSharedResources();
    }

    public event Action<ResidentialSearchableFurniture>? FirstSearched;

    public ResidentialFurnitureKind Kind { get; private set; }
    public ResidentialRoomEventKind EventKind { get; private set; }
    public int TowerIndex { get; private set; }
    public int FloorIndex { get; private set; }
    public int RoomSide { get; private set; }
    public bool EventTriggered { get; private set; }
    public List<LootItem> Loot { get; } = new();
    public Node3D LootNode => this;
    public bool IsSearchable => Loot.Count > 0;
    public float SearchDuration => Kind == ResidentialFurnitureKind.Wardrobe ? 0.9f : 0.65f;
    public bool VisualReady => IsInstanceValid(_movingPart) && _partCounter > 0;

    private Node3D _movingPart = null!;
    private Vector3 _openRotation;
    private Vector3 _openOffset;
    private int _partCounter;

    public void Configure(
        ResidentialFurnitureKind kind,
        ResidentialRoomEventKind eventKind,
        int towerIndex,
        int floorIndex,
        int roomSide,
        IEnumerable<LootItem> loot)
    {
        Kind = kind;
        EventKind = eventKind;
        TowerIndex = towerIndex;
        FloorIndex = floorIndex;
        RoomSide = roomSide;
        Loot.Clear();
        Loot.AddRange(loot);
    }

    public override void _Ready()
    {
        CollisionLayer = 1;
        CollisionMask = 0;
        AddToGroup("residential_searchable_furniture");
        BuildFurniture();
    }

    public string DisplayName(string language)
    {
        var key = Kind switch
        {
            ResidentialFurnitureKind.Refrigerator => "residential_furniture_fridge",
            ResidentialFurnitureKind.Wardrobe => "residential_furniture_wardrobe",
            ResidentialFurnitureKind.DeskDrawers => "residential_furniture_desk",
            _ => "residential_furniture_nightstand"
        };
        var english = Kind switch
        {
            ResidentialFurnitureKind.Refrigerator => "Apartment refrigerator",
            ResidentialFurnitureKind.Wardrobe => "Resident wardrobe",
            ResidentialFurnitureKind.DeskDrawers => "Desk drawers",
            _ => "Bedside cabinet"
        };
        return GameLocalization.Get(key, language, english);
    }

    public void OnSearched()
    {
        if (EventTriggered)
        {
            return;
        }

        EventTriggered = true;
        var tween = CreateTween().SetParallel(true);
        if (_openRotation != Vector3.Zero)
        {
            tween.TweenProperty(_movingPart, "rotation", _openRotation, 0.32f)
                .SetTrans(Tween.TransitionType.Quad)
                .SetEase(Tween.EaseType.Out);
        }
        if (_openOffset != Vector3.Zero)
        {
            tween.TweenProperty(_movingPart, "position", _movingPart.Position + _openOffset, 0.28f)
                .SetTrans(Tween.TransitionType.Quad)
                .SetEase(Tween.EaseType.Out);
        }
        FirstSearched?.Invoke(this);
    }

    private void BuildFurniture()
    {
        var size = Kind switch
        {
            ResidentialFurnitureKind.Refrigerator => new Vector3(0.72f, 1.78f, 0.68f),
            ResidentialFurnitureKind.Wardrobe => new Vector3(0.78f, 1.94f, 0.58f),
            ResidentialFurnitureKind.DeskDrawers => new Vector3(0.76f, 0.64f, 0.56f),
            _ => new Vector3(0.5f, 0.5f, 0.44f)
        };
        var center = Vector3.Up * (size.Y * 0.5f);
        AddChild(new CollisionShape3D
        {
            Name = "FurnitureCollision",
            Position = center,
            Shape = new BoxShape3D { Size = size }
        });

        Node3D? authoredModel = null;
        if (ResidentialAuthoredPropLibrary.TryCreateVisual(
                ResidentialAuthoredPropLibrary.PathFor(Kind),
                size,
                out var model,
                out var meshCount))
        {
            authoredModel = model;
            _partCounter += meshCount;
            SetMeta("residential_authored_furniture", ResidentialAuthoredPropLibrary.PathFor(Kind));
        }

        var shellColor = Kind switch
        {
            ResidentialFurnitureKind.Refrigerator => new Color(0.42f, 0.45f, 0.43f),
            ResidentialFurnitureKind.Wardrobe => new Color(0.24f, 0.16f, 0.1f),
            ResidentialFurnitureKind.DeskDrawers => new Color(0.2f, 0.14f, 0.09f),
            _ => new Color(0.27f, 0.18f, 0.11f)
        };
        var shell = Material(shellColor, Kind == ResidentialFurnitureKind.Refrigerator ? 0.5f : 0.05f, 0.74f);
        var trim = Material(new Color(0.055f, 0.062f, 0.06f), 0.7f, 0.32f);

        _movingPart = new Node3D
        {
            Name = "FurnitureDoor",
            Position = new Vector3(-size.X * 0.5f, size.Y * 0.52f, -size.Z * 0.51f)
        };
        AddChild(_movingPart);

        if (authoredModel is not null)
        {
            if (Kind == ResidentialFurnitureKind.DeskDrawers || Kind == ResidentialFurnitureKind.Nightstand)
            {
                _movingPart.Position = new Vector3(0, size.Y * 0.67f, -size.Z * 0.51f);
                _openOffset = new Vector3(0, 0, -0.28f);
            }
            else
            {
                _openRotation = new Vector3(0, -0.22f, 0);
            }
            authoredModel.Position += center - _movingPart.Position;
            _movingPart.AddChild(authoredModel);
        }
        else if (Kind == ResidentialFurnitureKind.DeskDrawers || Kind == ResidentialFurnitureKind.Nightstand)
        {
            _movingPart.Position = new Vector3(0, size.Y * 0.67f, -size.Z * 0.51f);
            Part(this, SharedBox(size), center, shell);
            Part(_movingPart, SharedBox(new Vector3(size.X - 0.08f, size.Y * 0.28f, 0.045f)), Vector3.Zero, shell);
            Part(_movingPart, SharedBox(new Vector3(0.16f, 0.045f, 0.055f)), new Vector3(0, 0, -0.04f), trim);
            _openOffset = new Vector3(0, 0, -0.28f);
        }
        else
        {
            Part(this, SharedBox(size), center, shell);
            Part(
                _movingPart,
                SharedBox(new Vector3(size.X - 0.07f, size.Y - 0.1f, 0.045f)),
                new Vector3(size.X * 0.5f, 0, 0),
                shell);
            Part(
                _movingPart,
                SharedBox(new Vector3(0.055f, size.Y * 0.34f, 0.06f)),
                new Vector3(size.X - 0.14f, 0, -0.04f),
                trim);
            _openRotation = new Vector3(0, -1.22f, 0);
        }

        if (EventKind == ResidentialRoomEventKind.None)
        {
            return;
        }

        var clueColor = EventKind switch
        {
            ResidentialRoomEventKind.BoobyTrap => new Color(0.82f, 0.08f, 0.035f),
            ResidentialRoomEventKind.Alarm => new Color(0.92f, 0.52f, 0.08f),
            _ => new Color(0.22f, 0.72f, 0.42f)
        };
        var clue = Material(clueColor, 0.2f, 0.42f, true);
        Part(
            this,
            SharedBox(EventKind == ResidentialRoomEventKind.BoobyTrap
                ? new Vector3(size.X * 0.48f, 0.025f, 0.025f)
                : new Vector3(0.055f, 0.055f, 0.035f)),
            new Vector3(0, Mathf.Min(size.Y - 0.12f, 0.72f), -size.Z * 0.55f),
            clue);
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
            EmissionEnergyMultiplier = emission ? 0.75f : 1.0f
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
            Name = $"FurniturePart_{_partCounter++:00}",
            Mesh = mesh,
            Position = position,
            MaterialOverride = material
        };
        parent.AddChild(part);
        return part;
    }
}
