using System;
using System.Collections.Generic;

namespace OperationSteelTide;

public enum ResidentialRoomArchetype
{
    FamilyApartment,
    MedicalClinic,
    EvacuationShelter,
    MaintenanceWorkshop,
    CommunitySecurity,
    SmugglerDen,
    CommunityKitchen
}

public enum ResidentialRoomZone
{
    North,
    South
}

public enum ResidentialRoomEventKind
{
    None,
    Alarm,
    BoobyTrap,
    Intel,
    GuardAmbush
}

public readonly record struct ResidentialRoomId(
    int TowerIndex,
    int FloorIndex,
    int Side,
    ResidentialRoomZone Zone);

public readonly record struct ResidentialChestPlan(
    ResidentialRoomId RoomId,
    ResidentialRoomArchetype Archetype,
    ResidentialCacheKind CacheKind,
    ResidentialRoomEventKind EventKind,
    uint Seed,
    int GuardCount);

public sealed record ResidentialChestResolution(
    LootGrade Grade,
    IReadOnlyList<LootItem> Items);

/// <summary>Deterministic residential chest planning and first-open loot resolution.</summary>
public static class ResidentialRoomLootRules
{
    public static ResidentialChestPlan Plan(
        ResidentialRoomId roomId,
        ResidentialRoomArchetype archetype,
        uint matchSalt)
    {
        var seed = SeedFor(roomId, matchSalt);
        var eventRoll = (int)((seed >> 12) % 100u);
        var eventKind = eventRoll switch
        {
            < 5 => ResidentialRoomEventKind.GuardAmbush,
            < 10 => ResidentialRoomEventKind.Alarm,
            < 15 => ResidentialRoomEventKind.BoobyTrap,
            < 21 => ResidentialRoomEventKind.Intel,
            _ => ResidentialRoomEventKind.None
        };
        return new ResidentialChestPlan(
            roomId,
            archetype,
            CacheKind(archetype),
            eventKind,
            seed,
            eventKind == ResidentialRoomEventKind.GuardAmbush ? 1 + (int)(seed % 2u) : 0);
    }

    public static ResidentialChestResolution Resolve(ResidentialChestPlan plan)
    {
        var random = new StableRandom(plan.Seed);
        var grade = RollGrade(plan.CacheKind, ref random);
        var count = 2 + random.Next(3);
        var items = new List<LootItem>(count);
        for (var index = 0; index < count; index++)
        {
            items.Add(CreateItem(plan.CacheKind, grade, index, ref random));
        }
        return new ResidentialChestResolution(grade, items);
    }

    public static uint SeedFor(ResidentialRoomId roomId, uint matchSalt)
    {
        var hash = 2166136261u;
        Mix(ref hash, unchecked((int)matchSalt));
        Mix(ref hash, roomId.TowerIndex + 1);
        Mix(ref hash, roomId.FloorIndex + 1);
        Mix(ref hash, roomId.Side > 0 ? 2 : 1);
        Mix(ref hash, (int)roomId.Zone + 1);
        return hash == 0 ? 0x9e3779b9u : hash;
    }

    private static ResidentialCacheKind CacheKind(ResidentialRoomArchetype archetype) => archetype switch
    {
        ResidentialRoomArchetype.MedicalClinic => ResidentialCacheKind.MedicalCabinet,
        ResidentialRoomArchetype.EvacuationShelter => ResidentialCacheKind.EvacuationLocker,
        ResidentialRoomArchetype.MaintenanceWorkshop => ResidentialCacheKind.WorkshopLocker,
        ResidentialRoomArchetype.CommunitySecurity => ResidentialCacheKind.SecurityArmory,
        ResidentialRoomArchetype.SmugglerDen => ResidentialCacheKind.SmugglerCache,
        ResidentialRoomArchetype.CommunityKitchen => ResidentialCacheKind.CommunityPantry,
        _ => ResidentialCacheKind.FamilyStash
    };

    private static LootGrade RollGrade(ResidentialCacheKind kind, ref StableRandom random)
    {
        var roll = random.Next(1000);
        var grade = roll switch
        {
            < 450 => LootGrade.Common,
            < 750 => LootGrade.Uncommon,
            < 910 => LootGrade.Rare,
            < 980 => LootGrade.Epic,
            _ => LootGrade.Legendary
        };
        if (kind is ResidentialCacheKind.SecurityArmory or ResidentialCacheKind.SmugglerCache
            && random.Next(100) < 34)
        {
            grade = (LootGrade)Math.Min((int)LootGrade.Legendary, (int)grade + 1);
        }
        return grade;
    }

    private static LootItem CreateItem(
        ResidentialCacheKind kind,
        LootGrade grade,
        int index,
        ref StableRandom random)
    {
        var roll = random.Next(100);
        if (index == 0)
        {
            roll = kind switch
            {
                ResidentialCacheKind.MedicalCabinet => 74,
                ResidentialCacheKind.SecurityArmory => 5,
                ResidentialCacheKind.SmugglerCache => 17,
                ResidentialCacheKind.WorkshopLocker => 38,
                ResidentialCacheKind.CommunityPantry => 83,
                _ => roll
            };
        }

        if (roll < 14)
        {
            var platform = grade switch
            {
                LootGrade.Legendary => random.Next(100) < 55 ? WeaponPlatform.AWM : WeaponPlatform.M24,
                LootGrade.Epic => random.Next(100) < 48 ? WeaponPlatform.VSS : WeaponPlatform.ScarL,
                LootGrade.Rare when random.Next(100) < 22 => WeaponPlatform.DesertEagle,
                LootGrade.Rare when random.Next(100) < 54 => WeaponPlatform.MP5A5,
                _ => WeaponPlatform.M4A1
            };
            var tier = grade >= LootGrade.Legendary ? 2 : grade >= LootGrade.Rare ? 1 : 0;
            return new LootItem { Kind = LootItemKind.Weapon, Weapon = WeaponCatalog.Build(platform, tier), Grade = grade };
        }
        if (roll < 30)
        {
            var caliberRoll = random.Next(100);
            var caliber = grade >= LootGrade.Epic
                ? caliberRoll < 24 ? AmmoCaliber.Magnum338 : AmmoCaliber.Sniper
                : caliberRoll < 18 ? AmmoCaliber.Pistol : caliberRoll < 46 ? AmmoCaliber.Smg : AmmoCaliber.Rifle;
            return new LootItem
            {
                Kind = LootItemKind.Ammunition,
                AmmoCaliber = caliber,
                Quantity = caliber is AmmoCaliber.Sniper or AmmoCaliber.Magnum338
                    ? 10 + (int)grade * 3
                    : 24 + (int)grade * 12,
                Grade = grade
            };
        }
        if (roll < 47)
        {
            var attachment = grade >= LootGrade.Epic
                ? "muzzle_suppressor"
                : grade >= LootGrade.Rare ? "optic_scope" : "grip_vertical";
            return new LootItem { Kind = LootItemKind.Attachment, AttachmentId = attachment, Grade = grade };
        }
        if (roll < 59)
        {
            var equipmentId = grade >= LootGrade.Epic
                ? (random.Next(2) == 0 ? "armor_heavy" : "pack_heavy")
                : (random.Next(2) == 0 ? "armor_carrier" : "pack_assault");
            return new LootItem
            {
                Kind = LootItemKind.Equipment,
                Equipment = EquipmentCatalog.Create(equipmentId),
                Grade = grade
            };
        }
        if (roll < 70)
        {
            return new LootItem
            {
                Kind = LootItemKind.ArmorPlate,
                Quantity = grade >= LootGrade.Rare ? 2 : 1,
                Grade = grade
            };
        }
        if (roll < 86)
        {
            var medicalKind = grade >= LootGrade.Epic
                ? MedicalItemKind.Adrenaline
                : grade >= LootGrade.Rare ? MedicalItemKind.FieldMedkit : MedicalItemKind.Bandage;
            return new LootItem
            {
                Kind = LootItemKind.Medical,
                MedicalKind = medicalKind,
                Quantity = grade >= LootGrade.Rare ? 2 : 1,
                Grade = grade
            };
        }
        return new LootItem
        {
            Kind = LootItemKind.Valuable,
            ValuableKind = ValuableItems.SelectForGrade(grade, random.Next(int.MaxValue)),
            Quantity = 1,
            Grade = grade
        };
    }

    private static void Mix(ref uint hash, int value)
    {
        hash ^= unchecked((uint)value);
        hash *= 16777619u;
    }

    private struct StableRandom
    {
        private uint _state;

        public StableRandom(uint seed)
        {
            _state = seed == 0 ? 0x9e3779b9u : seed;
        }

        public int Next(int upperExclusive)
        {
            _state ^= _state << 13;
            _state ^= _state >> 17;
            _state ^= _state << 5;
            return (int)(_state % (uint)Math.Max(1, upperExclusive));
        }
    }
}
