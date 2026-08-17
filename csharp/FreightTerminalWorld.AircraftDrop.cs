using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private readonly List<AircraftSupplyDrop> _aircraftSupplyDrops = new();

    public AircraftSupplyDrop SpawnAircraftSupplyDrop(Vector3 crashPosition, Rid aircraftRid)
    {
        var dropPosition = ResolveAircraftSupplyDropPosition(crashPosition, aircraftRid, out var groundResolved);
        var drop = new AircraftSupplyDrop
        {
            Name = $"AircraftSupplyDrop_{_aircraftSupplyDrops.Count + 1:00}",
            Position = dropPosition
        };
        drop.Configure(CreateAircraftSupplyDropLoot(), _languageSetting, groundResolved);
        AddChild(drop);
        _aircraftSupplyDrops.Add(drop);
        _lootSources.Add(drop);
        _lootWorldPoints.Add(drop.GlobalPosition);
        if (IsInstanceValid(_hud))
        {
            _hud.ShowLocalizedMessage(
                "aircraft_supply_marked",
                "AIRCRAFT DOWN  //  SUPPLY DROP MARKED",
                new Color(1.0f, 0.48f, 0.12f));
        }
        return drop;
    }

    private Vector3 ResolveAircraftSupplyDropPosition(Vector3 crashPosition, Rid aircraftRid, out bool groundResolved)
    {
        var x = Mathf.Clamp(crashPosition.X, -MapWidthMeters * 0.5f + 4.0f, MapWidthMeters * 0.5f - 4.0f);
        var minimumZ = MapCenterZ - MapDepthMeters * 0.5f + 4.0f;
        var maximumZ = MapCenterZ + MapDepthMeters * 0.5f - 4.0f;
        var z = Mathf.Clamp(crashPosition.Z, minimumZ, maximumZ);
        var from = new Vector3(x, 90.0f, z);
        var to = new Vector3(x, -6.0f, z);
        var query = PhysicsRayQueryParameters3D.Create(from, to);
        query.CollisionMask = 1;
        query.CollideWithAreas = false;
        query.CollideWithBodies = true;
        query.Exclude = new Godot.Collections.Array<Rid> { aircraftRid };
        var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
        groundResolved = hit.Count > 0;
        return groundResolved
            ? hit["position"].AsVector3() + Vector3.Up * 0.03f
            : new Vector3(x, 0.05f, z);
    }

    private static List<LootItem> CreateAircraftSupplyDropLoot()
    {
        return new List<LootItem>
        {
            new()
            {
                Kind = LootItemKind.Weapon,
                Weapon = WeaponCatalog.Build(WeaponPlatform.AWM, 3),
                Grade = LootGrade.Legendary
            },
            new()
            {
                Kind = LootItemKind.Weapon,
                Weapon = WeaponCatalog.Build(WeaponPlatform.VSS, 2),
                Grade = LootGrade.Epic
            },
            new()
            {
                Kind = LootItemKind.Weapon,
                Weapon = WeaponCatalog.Build(WeaponPlatform.DesertEagle, 1),
                Grade = LootGrade.Rare
            },
            new()
            {
                Kind = LootItemKind.Weapon,
                Weapon = WeaponCatalog.Build(WeaponPlatform.GSh18, 1),
                Grade = LootGrade.Rare
            },
            new()
            {
                Kind = LootItemKind.Ammunition,
                AmmoCaliber = AmmoCaliber.Magnum338,
                Quantity = 24,
                Grade = LootGrade.Rare
            },
            new()
            {
                Kind = LootItemKind.Ammunition,
                AmmoCaliber = AmmoCaliber.Rifle,
                Quantity = 72,
                Grade = LootGrade.Rare
            },
            new()
            {
                Kind = LootItemKind.Ammunition,
                AmmoCaliber = AmmoCaliber.Pistol,
                Quantity = 35,
                Grade = LootGrade.Rare
            },
            new()
            {
                Kind = LootItemKind.Equipment,
                Equipment = EquipmentCatalog.Create("armor_heavy"),
                Grade = LootGrade.Epic
            },
            new()
            {
                Kind = LootItemKind.Medical,
                MedicalKind = MedicalItemKind.FieldMedkit,
                Quantity = 2,
                Grade = LootGrade.Rare
            },
            new()
            {
                Kind = LootItemKind.Medical,
                MedicalKind = MedicalItemKind.Adrenaline,
                Quantity = 1,
                Grade = LootGrade.Epic
            },
            new()
            {
                Kind = LootItemKind.ArmorPlate,
                Quantity = 3,
                Grade = LootGrade.Rare
            },
            new()
            {
                Kind = LootItemKind.Valuable,
                ValuableKind = ValuableItemKind.EncryptedDrive,
                Quantity = 1,
                Grade = LootGrade.Legendary
            }
        };
    }
}
