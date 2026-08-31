using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OperationSteelTide;

/// <summary>
/// A direct weapon drop backed by the same authored model used in combat.
/// It is intentionally passive: no per-frame processing, animation, or scene scan.
/// </summary>
[GlobalClass]
public partial class DroppedWeaponPickup : Area3D, ILootSource
{
    private const float FieldPresentationScale = 0.72f;
    private static readonly StandardMaterial3D SharedHighlightMaterial = new()
    {
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        AlbedoColor = new Color(1.0f, 0.62f, 0.12f, 0.09f),
        EmissionEnabled = true,
        Emission = new Color(1.0f, 0.43f, 0.08f),
        EmissionEnergyMultiplier = 0.18f
    };

    private Node3D? _weaponVisual;
    private Label3D? _pickupLabel;

    public List<LootItem> Loot { get; } = new();
    public Node3D LootNode => this;
    public bool IsSearchable => WeaponItem() is not null;
    public float SearchDuration => 0.0f;
    public bool UsesAuthoredWeaponVisualForDiagnostics
        => IsInstanceValid(_weaponVisual)
            && CombatModelLibrary.MeshesBelow(_weaponVisual!).Any();
    public WeaponPlatform? PlatformForDiagnostics => WeaponItem()?.Weapon?.Platform;
    public bool HasBlockingCollisionForDiagnostics
        => CollisionLayer != 0
            || CollisionMask != 0
            || GetChildren().OfType<CollisionShape3D>().Any();
    public int DemolitionRound { get; private set; }
    public int DropId { get; private set; } = -1;
    public int Revision { get; private set; }

    public void Configure(LootItem item)
    {
        if (item.Kind != LootItemKind.Weapon || item.Weapon is null)
        {
            throw new System.ArgumentException("Dropped weapon pickup requires a weapon loot item.", nameof(item));
        }

        Loot.Clear();
        Loot.Add(item);
        if (IsInsideTree())
        {
            RefreshWeaponPresentation();
        }
    }

    public override void _Ready()
    {
        // Interaction is resolved explicitly by distance and a world-geometry LOS ray.
        // Keeping this Area shape-less and off every physics layer prevents a dropped
        // rifle from becoming a movement, bullet, or grenade blocker.
        CollisionLayer = 0;
        CollisionMask = 0;
        Monitoring = false;
        Monitorable = false;
        AddToGroup("dropped_weapons");
        _pickupLabel = new Label3D
        {
            Name = "PickupLabel",
            Position = new Vector3(0.0f, 0.52f, 0.0f),
            FontSize = 15,
            OutlineSize = 5,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            Modulate = new Color(1.0f, 0.78f, 0.22f),
            VisibilityRangeEnd = 20.0f
        };
        AddChild(_pickupLabel);
        RefreshWeaponPresentation();
        SetProcess(false);
        SetPhysicsProcess(false);
    }

    public void ConfigureNetworkIdentity(int round, int dropId, int revision)
    {
        DemolitionRound = Mathf.Max(0, round);
        DropId = dropId;
        Revision = Mathf.Max(0, revision);
    }

    public void AdvanceRevision()
        => Revision = Revision == int.MaxValue ? int.MaxValue : Revision + 1;

    public string DisplayName(string language)
    {
        var weapon = WeaponItem()?.Weapon;
        if (weapon is null)
        {
            return "Collected weapon";
        }
        var definition = WeaponCatalog.Weapon(weapon.Platform);
        var name = GameLocalization.IsChinese(language)
            ? GameLocalization.Get(definition.LocalizationKey, language, definition.ChineseName)
            : definition.Name;
        return GameLocalization.Format(
            "demolition_dropped_weapon",
            language,
            "Dropped weapon  {0}",
            name);
    }

    public void OnSearched()
    {
        // Direct demolition pickups equip immediately instead of opening the backpack UI.
    }

    public void RefreshWeaponPresentation()
    {
        if (IsInstanceValid(_weaponVisual))
        {
            _weaponVisual!.Free();
            _weaponVisual = null;
        }

        var item = WeaponItem();
        var build = item?.Weapon;
        if (build is null)
        {
            Visible = false;
            if (IsInstanceValid(_pickupLabel))
            {
                _pickupLabel!.Visible = false;
            }
            return;
        }

        var visual = CombatModelLibrary.InstantiateWeapon(build.Platform, firstPerson: false);
        visual.Configure(build);
        _weaponVisual = visual.Root;
        _weaponVisual.Name = "AuthoredDroppedWeapon";
        // The world model has an authored platform-specific normalization. Apply one
        // shared field scale before measuring bounds so every platform rests on the
        // floor at a readable but believable dropped-gun size.
        _weaponVisual.Scale *= FieldPresentationScale;
        var bounds = CombatModelLibrary.ComputeBounds(_weaponVisual);
        _weaponVisual.Position = new Vector3(
            -bounds.Center.X,
            Mathf.Max(0.04f, bounds.Size.Y * 0.5f - bounds.Center.Y + 0.04f),
            -bounds.Center.Z);
        _weaponVisual.RotationDegrees = new Vector3(0.0f, 18.0f, 0.0f);
        foreach (var mesh in CombatModelLibrary.MeshesBelow(_weaponVisual))
        {
            mesh.MaterialOverlay = SharedHighlightMaterial;
        }
        AddChild(_weaponVisual);

        Visible = true;
        if (IsInstanceValid(_pickupLabel))
        {
            _pickupLabel!.Text = $"F  //  {WeaponCatalog.Weapon(build.Platform).Name}";
            _pickupLabel.Visible = true;
        }
    }

    private LootItem? WeaponItem()
        => Loot.Find(item => item.Kind == LootItemKind.Weapon && item.Weapon is not null);
}
