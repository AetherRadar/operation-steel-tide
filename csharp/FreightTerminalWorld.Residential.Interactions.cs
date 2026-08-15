using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private ResidentialRoomEncounterController CreateResidentialRoomEncounterController()
        => new(new ResidentialEncounterEffects(
            DamagePlayerNearResidentialEvent,
            ReportGunshot,
            AlertEnemiesNear,
            ScanEnemiesNear,
            SpawnResidentialGuardAmbush,
            ShowResidentialEncounterMessage));

    private void OnResidentialCacheFirstOpened(ResidentialSupplyCache cache)
    {
        if (cache.EventKind != ResidentialRoomEventKind.None)
        {
            _residentialChestEventCount++;
        }
        _residentialEncounterController?.Handle(cache);
    }

    private void DamagePlayerNearResidentialEvent(Vector3 origin, float damage)
    {
        if (IsInstanceValid(_player) && !_player.IsDead && _player.GlobalPosition.DistanceTo(origin) <= 3.2f)
        {
            _player.TakeDamage(damage, _player.HitPoint(HitRegion.Torso), this);
        }
    }

    private int ScanEnemiesNear(Vector3 origin, float radius)
    {
        var marked = 0;
        foreach (var enemy in _enemies)
        {
            if (!IsInstanceValid(enemy) || enemy.IsDead || enemy.GlobalPosition.DistanceTo(origin) > radius)
            {
                continue;
            }
            enemy.SetScanned(12.0f);
            marked++;
        }
        return marked;
    }

    private void SpawnResidentialGuardAmbush(ResidentialSupplyCache cache, int count)
        => SpawnResidentialGuardAmbush(cache, count, initialWeapon: null);

    private void SpawnResidentialGuardAmbush(
        ResidentialSupplyCache cache,
        int count,
        WeaponBuild? initialWeapon)
    {
        var guardExclude = new Godot.Collections.Array<Rid>();
        if (IsInstanceValid(_player))
        {
            guardExclude.Add(_player.GetRid());
        }
        var planner = CreateResidentialGuardSpawnPlanner(cache, guardExclude);
        var preferredTarget = IsInstanceValid(_player)
            ? _player.GlobalPosition
            : cache.GlobalPosition;
        var layout = planner.Plan(count, preferredTarget);

        for (var index = 0; index < count; index++)
        {
            var spawnPosition = layout.SpawnPositions[index];
            var guard = SpawnEnemy(
                spawnPosition,
                alerted: true,
                teamId: 0,
                initialWeapon: initialWeapon);
            guard.Name = $"RESIDENTIAL_GUARD_T{cache.TowerIndex + 1:00}_F{cache.FloorIndex + 1:00}_{_residentialGuardAmbushSpawnCount + 1:00}";
            _residentialGuardAmbushSpawnCount++;
        }
        _enemiesRemaining = _enemies.Count(enemy => IsInstanceValid(enemy) && !enemy.IsDead);
        if (IsInstanceValid(_hud))
        {
            _hud.SetEnemyCount(_enemiesRemaining);
        }
    }

    private ResidentialGuardSpawnPlanner CreateResidentialGuardSpawnPlanner(
        ResidentialSupplyCache cache,
        IEnumerable<Rid> additionalExclude)
        => new(
            GetWorld3D().DirectSpaceState,
            cache.GlobalTransform,
            cache.GetRid(),
            additionalExclude);

    private void ShowResidentialEncounterMessage(
        Vector3 origin,
        string localizationKey,
        string english,
        Color color)
    {
        if (IsInstanceValid(_player) && _player.GlobalPosition.DistanceTo(origin) <= 12.0f)
        {
            _hud.ShowLocalizedMessage(localizationKey, english, color);
        }
    }

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
