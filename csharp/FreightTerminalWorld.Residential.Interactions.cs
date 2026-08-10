using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private static List<LootItem> CreateResidentialFurnitureLoot(
        ResidentialFurnitureKind kind,
        ResidentialRoomArchetype archetype,
        int towerIndex,
        int floor,
        float side)
    {
        var selector = towerIndex * 17 + floor * 5 + (side > 0.0f ? 1 : 0);
        var loot = new List<LootItem>();
        switch (kind)
        {
            case ResidentialFurnitureKind.Refrigerator:
                loot.Add(new LootItem
                {
                    Kind = LootItemKind.Valuable,
                    ValuableKind = ValuableItemKind.CannedCoffee,
                    Quantity = 2,
                    Grade = LootGrade.Common
                });
                loot.Add(new LootItem
                {
                    Kind = LootItemKind.Medical,
                    MedicalKind = MedicalItemKind.Bandage,
                    Quantity = 1,
                    Grade = LootGrade.Common
                });
                break;
            case ResidentialFurnitureKind.Wardrobe:
                loot.Add(new LootItem
                {
                    Kind = LootItemKind.ArmorPlate,
                    Quantity = 1,
                    Grade = LootGrade.Uncommon
                });
                loot.Add(new LootItem
                {
                    Kind = LootItemKind.Valuable,
                    ValuableKind = ValuableItemKind.DesignerPerfume,
                    Quantity = 1,
                    Grade = LootGrade.Rare
                });
                break;
            case ResidentialFurnitureKind.DeskDrawers:
                loot.Add(new LootItem
                {
                    Kind = LootItemKind.Ammunition,
                    AmmoCaliber = AmmoCaliber.Rifle,
                    Quantity = 12 + Mathf.PosMod(selector, 3) * 6,
                    Grade = LootGrade.Common
                });
                loot.Add(new LootItem
                {
                    Kind = LootItemKind.Valuable,
                    ValuableKind = ValuableItemKind.SmartPhone,
                    Quantity = 1,
                    Grade = LootGrade.Uncommon
                });
                break;
            default:
                loot.Add(new LootItem
                {
                    Kind = LootItemKind.Valuable,
                    ValuableKind = ValuableItems.SelectForGrade(LootGrade.Uncommon, selector),
                    Quantity = 1,
                    Grade = LootGrade.Uncommon
                });
                loot.Add(new LootItem
                {
                    Kind = LootItemKind.Medical,
                    MedicalKind = MedicalItemKind.Bandage,
                    Quantity = 1,
                    Grade = LootGrade.Common
                });
                break;
        }

        switch (archetype)
        {
            case ResidentialRoomArchetype.MedicalClinic:
                loot.Add(new LootItem
                {
                    Kind = LootItemKind.Medical,
                    MedicalKind = MedicalItemKind.FieldMedkit,
                    Quantity = 1,
                    Grade = LootGrade.Rare
                });
                break;
            case ResidentialRoomArchetype.EvacuationShelter:
                loot.Add(new LootItem
                {
                    Kind = LootItemKind.Ammunition,
                    AmmoCaliber = AmmoCaliber.Rifle,
                    Quantity = 24,
                    Grade = LootGrade.Uncommon
                });
                break;
            case ResidentialRoomArchetype.MaintenanceWorkshop:
                loot.Add(new LootItem
                {
                    Kind = LootItemKind.Attachment,
                    AttachmentId = "grip_vertical",
                    Grade = LootGrade.Uncommon
                });
                break;
            case ResidentialRoomArchetype.CommunitySecurity:
                loot.Add(new LootItem
                {
                    Kind = LootItemKind.Ammunition,
                    AmmoCaliber = AmmoCaliber.Smg,
                    Quantity = 30,
                    Grade = LootGrade.Rare
                });
                break;
            case ResidentialRoomArchetype.SmugglerDen:
                loot.Add(new LootItem
                {
                    Kind = LootItemKind.Valuable,
                    ValuableKind = ValuableItemKind.EncryptedDrive,
                    Quantity = 1,
                    Grade = LootGrade.Epic
                });
                loot.Add(new LootItem
                {
                    Kind = LootItemKind.Attachment,
                    AttachmentId = "muzzle_suppressor",
                    Grade = LootGrade.Epic
                });
                break;
            case ResidentialRoomArchetype.CommunityKitchen:
                loot.Add(new LootItem
                {
                    Kind = LootItemKind.Medical,
                    MedicalKind = MedicalItemKind.Adrenaline,
                    Quantity = 1,
                    Grade = LootGrade.Uncommon
                });
                break;
        }
        return loot;
    }

    private void OnResidentialFurnitureSearched(ResidentialSearchableFurniture furniture)
    {
        if (furniture.EventKind == ResidentialRoomEventKind.None)
        {
            return;
        }

        _residentialFurnitureEventCount++;
        var accent = furniture.EventKind switch
        {
            ResidentialRoomEventKind.BoobyTrap => new Color(1.0f, 0.28f, 0.16f),
            ResidentialRoomEventKind.Alarm => new Color(1.0f, 0.62f, 0.2f),
            _ => new Color(0.28f, 0.9f, 0.62f)
        };
        switch (furniture.EventKind)
        {
            case ResidentialRoomEventKind.BoobyTrap:
                _player.TakeDamage(18.0f, _player.HitPoint(HitRegion.Torso), this);
                ReportGunshot(furniture.GlobalPosition, 42.0f);
                _hud.ShowLocalizedMessage(
                    "residential_room_trap",
                    "BOOBY TRAP  //  ROOM COMPROMISED",
                    accent);
                AlertEnemiesNear(furniture.GlobalPosition, 48.0f);
                break;
            case ResidentialRoomEventKind.Alarm:
                ReportGunshot(furniture.GlobalPosition, 58.0f);
                _hud.ShowLocalizedMessage(
                    "residential_room_alarm",
                    "ROOM ALARM  //  CONTACTS MOVING",
                    accent);
                AlertEnemiesNear(furniture.GlobalPosition, 64.0f);
                break;
            case ResidentialRoomEventKind.Intel:
                var marked = 0;
                foreach (var enemy in _enemies)
                {
                    if (!IsInstanceValid(enemy) || enemy.IsDead || enemy.GlobalPosition.DistanceTo(furniture.GlobalPosition) > 58.0f)
                    {
                        continue;
                    }
                    enemy.SetScanned(12.0f);
                    marked++;
                }
                _hud.ShowLocalizedMessage(
                    "residential_room_intel",
                    marked > 0 ? $"ROOM INTEL  //  {marked} CONTACTS MARKED" : "ROOM INTEL  //  NO CONTACTS",
                    accent);
                break;
        }
    }

    private void AlertEnemiesNear(Vector3 origin, float radius)
    {
        foreach (var enemy in _enemies)
        {
            if (IsInstanceValid(enemy) && !enemy.IsDead && enemy.GlobalPosition.DistanceTo(origin) <= radius)
            {
                enemy.SetAlerted(origin);
            }
        }
    }
}
