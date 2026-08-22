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
        groundResolved = PhysicsRaycast.TryHit(
            GetWorld3D().DirectSpaceState,
            from,
            to,
            aircraftRid,
            1,
            out var hit);
        return groundResolved
            ? hit.Position + Vector3.Up * 0.03f
            : new Vector3(x, 0.05f, z);
    }

    /// <summary>
    /// Weighted crash-drop manifest. Guarantees one high-tier weapon, an epic-grade
    /// equipment piece, medical supplies, a legendary valuable, and a service sidearm;
    /// the remaining slots roll from weighted pools so no two crashes pay out alike.
    /// High-tier ammunition appears only here and in the secured loot rooms.
    /// </summary>
    private List<LootItem> CreateAircraftSupplyDropLoot()
    {
        var loot = new List<LootItem>
        {
            new()
            {
                Kind = LootItemKind.Weapon,
                Weapon = WeaponCatalog.Build(_rng.Randf() switch
                {
                    < 0.30f => WeaponPlatform.AWM,
                    < 0.72f => WeaponPlatform.VSS,
                    _ => WeaponPlatform.ScarL
                }, _rng.Randf() < 0.34f ? 3 : 2),
                Grade = _rng.Randf() < 0.30f ? LootGrade.Legendary : LootGrade.Epic
            },
            new()
            {
                Kind = LootItemKind.Weapon,
                Weapon = WeaponCatalog.Build(WeaponPlatform.GSh18, 1),
                Grade = LootGrade.Rare
            },
            new()
            {
                Kind = LootItemKind.Equipment,
                Equipment = EquipmentCatalog.Create(_rng.Randf() switch
                {
                    < 0.55f => "armor_heavy",
                    < 0.8f => "pack_heavy",
                    _ => "helmet_heavy"
                }),
                Grade = LootGrade.Epic
            },
            new()
            {
                Kind = LootItemKind.Medical,
                MedicalKind = _rng.Randf() < 0.55f ? MedicalItemKind.FieldMedkit : MedicalItemKind.Adrenaline,
                Quantity = _rng.RandiRange(1, 2),
                Grade = LootGrade.Rare
            },
            new()
            {
                Kind = LootItemKind.Valuable,
                ValuableKind = _rng.Randf() < 0.5f
                    ? ValuableItemKind.EncryptedDrive
                    : ValuableItems.SelectForGrade(LootGrade.Legendary, _rng.RandiRange(0, 99)),
                Quantity = 1,
                Grade = LootGrade.Legendary
            }
        };
        for (var ammoRoll = 0; ammoRoll < 2; ammoRoll++)
        {
            var grade = _rng.Randf() switch
            {
                < 0.20f => LootGrade.Legendary,
                < 0.62f => LootGrade.Epic,
                _ => LootGrade.Rare
            };
            var caliber = grade >= LootGrade.Epic
                ? _rng.Randf() switch
                {
                    < 0.4f => AmmoCaliber.Magnum338,
                    < 0.75f => AmmoCaliber.Sniper,
                    _ => AmmoCaliber.Rifle
                }
                : _rng.Randf() switch
                {
                    < 0.5f => AmmoCaliber.Rifle,
                    < 0.8f => AmmoCaliber.Smg,
                    _ => AmmoCaliber.Pistol
                };
            loot.Add(new LootItem
            {
                Kind = LootItemKind.Ammunition,
                AmmoCaliber = caliber,
                Quantity = caliber is AmmoCaliber.Magnum338 or AmmoCaliber.Sniper
                    ? _rng.RandiRange(8, 20)
                    : _rng.RandiRange(40, 80),
                Grade = grade
            });
        }
        if (_rng.Randf() < 0.4f)
        {
            loot.Add(new LootItem
            {
                Kind = LootItemKind.Weapon,
                Weapon = WeaponCatalog.Build(
                    _rng.Randf() < 0.5f ? WeaponPlatform.DesertEagle : WeaponPlatform.MP5A5,
                    1),
                Grade = LootGrade.Rare
            });
        }
        if (_rng.Randf() < 0.45f)
        {
            loot.Add(new LootItem
            {
                Kind = LootItemKind.Medical,
                MedicalKind = MedicalItemKind.Adrenaline,
                Quantity = 1,
                Grade = LootGrade.Epic
            });
        }
        loot.Add(new LootItem
        {
            Kind = LootItemKind.ArmorPlate,
            Quantity = _rng.RandiRange(2, 3),
            Grade = _rng.Randf() < 0.5f ? LootGrade.Epic : LootGrade.Rare
        });
        return loot;
    }
}
