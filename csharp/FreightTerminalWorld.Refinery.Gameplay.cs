using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private static readonly Vector3[] RefineryWorldBossPatrolRoute =
    {
        new(-132, 0.2f, 40), new(-86, 0.2f, 31), new(-24, 0.2f, 28),
        new(24, 0.2f, 28), new(92, 0.2f, 22), new(132, 0.2f, -28),
        new(118, 0.2f, -92), new(84, 0.2f, -112), new(28, 0.2f, -146),
        new(-28, 0.2f, -146), new(-86, 0.2f, -112), new(-118, 0.2f, -92),
        new(-96, 0.2f, -28), new(-24, 0.2f, -60), new(24, 0.2f, -60)
    };

    private IReadOnlyList<Vector3> ActiveWorldBossPatrolRoute
        => IsBlackwaterRefineryMap ? RefineryWorldBossPatrolRoute : WorldBossPatrolRoute;

    private void SpawnRefineryWeaponCases()
    {
        var definitions = new[]
        {
            new RefineryWeaponCaseDefinition(
                new Vector3(-91, 0.02f, -122), 0.0f,
                "Guangchang Pawnshop security armory", "\u5e7f\u660c\u5f53\u94fa\u5b89\u4fdd\u519b\u68b0\u5e93",
                WeaponCatalog.Build(WeaponPlatform.M4A1, 2),
                new[] { "optic_holo", "mag_extended" }, new[] { "armor_heavy" }, "knife_zhanma"),
            new RefineryWeaponCaseDefinition(
                new Vector3(-77, 0.02f, -116), Mathf.Pi,
                "Pawnshop counter response case", "\u5f53\u94fa\u67dc\u53f0\u5e94\u6025\u7bb1",
                WeaponCatalog.Build(WeaponPlatform.MP5A5, 2),
                new[] { "optic_micro", "muzzle_suppressor" }, new[] { "helmet_light" }, "knife_crimson"),
            new RefineryWeaponCaseDefinition(
                new Vector3(91, 0.02f, 4), Mathf.Pi,
                "Red Star Electronics tactical vault", "\u7ea2\u661f\u7535\u5b50\u5382\u6218\u672f\u67dc",
                WeaponCatalog.Build(WeaponPlatform.ScarL, 2),
                new[] { "optic_scope", "stock_precision" }, new[] { "armor_heavy", "pack_heavy" }, "knife_tianxuan"),
            new RefineryWeaponCaseDefinition(
                new Vector3(77, 0.02f, -4), 0.0f,
                "Factory guard locker", "\u7535\u5b50\u5382\u8b66\u536b\u67dc",
                WeaponCatalog.Build(WeaponPlatform.AK74, 2),
                new[] { "muzzle_brake", "grip_vertical" }, new[] { "helmet_heavy" }, "knife_arctic"),
            new RefineryWeaponCaseDefinition(
                new Vector3(-14, 4.45f, -126), Mathf.Pi * 0.5f,
                "Old City footbridge marksman case", "\u65e7\u57ce\u5929\u6865\u5e02\u96c6\u5c04\u624b\u7bb1",
                WeaponCatalog.Build(WeaponPlatform.M24, 2),
                new[] { "optic_scope", "muzzle_suppressor" }, new[] { "pack_assault" }, string.Empty),
            new RefineryWeaponCaseDefinition(
                new Vector3(18, 0.02f, -47), -Mathf.Pi * 0.5f,
                "Jianghai Square response case", "\u6c5f\u6d77\u5e7f\u573a\u5e94\u6025\u7bb1",
                WeaponCatalog.Build(WeaponPlatform.GSh18, 1),
                new[] { "optic_micro" }, new[] { "armor_carrier" }, string.Empty)
        };
        foreach (var definition in definitions)
        {
            SpawnRefineryWeaponCase(definition);
        }
    }

    private void SpawnRefineryWeaponCase(RefineryWeaponCaseDefinition definition)
    {
        var weaponCase = new WeaponCase
        {
            Position = definition.Position,
            Rotation = new Vector3(0, definition.Rotation, 0),
            EnglishName = definition.EnglishName,
            ChineseName = definition.ChineseName
        };
        weaponCase.Loot.Add(new LootItem
        {
            Kind = LootItemKind.Weapon,
            Weapon = definition.Weapon,
            Grade = LootGrades.FromTier(definition.Weapon.Attachments.Count >= 5 ? 2 : 1)
        });
        foreach (var part in definition.Parts)
        {
            weaponCase.Loot.Add(new LootItem
            {
                Kind = LootItemKind.Attachment,
                AttachmentId = part,
                Grade = LootGrade.Rare
            });
        }
        foreach (var equipmentId in definition.Equipment)
        {
            weaponCase.Loot.Add(new LootItem
            {
                Kind = LootItemKind.Equipment,
                Equipment = EquipmentCatalog.Create(equipmentId),
                Grade = equipmentId.Contains("heavy") ? LootGrade.Epic : LootGrade.Rare
            });
        }
        weaponCase.Loot.Add(new LootItem
        {
            Kind = LootItemKind.Ammunition,
            AmmoCaliber = WeaponCatalog.Weapon(definition.Weapon.Platform).Caliber,
            Quantity = definition.Weapon.Platform == WeaponPlatform.M24 ? 24 : 75,
            Grade = LootGrade.Uncommon
        });
        if (!string.IsNullOrEmpty(definition.KnifeSkin))
        {
            weaponCase.Loot.Add(new LootItem
            {
                Kind = LootItemKind.KnifeSkin,
                KnifeSkinId = definition.KnifeSkin,
                Grade = LootGrade.Epic
            });
        }
        weaponCase.Loot.Add(new LootItem { Kind = LootItemKind.ArmorPlate, Grade = LootGrade.Uncommon });
        AddChild(weaponCase);
        _lootSources.Add(weaponCase);
        _lootWorldPoints.Add(definition.Position);
    }

    private void SpawnRefineryGradedLoot()
    {
        var index = 0;
        foreach (var placement in RefineryLayout.LootPlacements)
        {
            var pickup = new GradedLootPickup
            {
                Name = $"OldTownLoot_{++index:000}",
                Position = placement.Position
            };
            pickup.Configure(
                CreateGradedLootItem(placement.Grade),
                placement.EnglishName,
                placement.ChineseName);
            AddChild(pickup);
            _lootSources.Add(pickup);
            _lootWorldPoints.Add(placement.Position);
            _buildingLootPickupCount++;
        }
    }

    private void SpawnRefineryValuableLoot()
    {
        var index = 0;
        foreach (var placement in RefineryLayout.ValuablePlacements)
        {
            var item = new LootItem
            {
                Kind = LootItemKind.Valuable,
                ValuableKind = placement.Kind,
                Grade = placement.Grade
            };
            var pickup = new GradedLootPickup
            {
                Name = $"OldTownValuable_{++index:00}_{placement.Kind}",
                Position = placement.Position
            };
            pickup.Configure(
                item,
                ValuableItems.DisplayName(placement.Kind, "en"),
                ValuableItems.DisplayName(placement.Kind, "zh"));
            AddChild(pickup);
            _lootSources.Add(pickup);
            _lootWorldPoints.Add(placement.Position);
        }
    }

    private void SpawnOldTownInteriorResidents()
    {
        _oldTownInteriorResidentCount = 0;
        var residents = new (Vector3 Position, CivilianRole Role, Vector2 Roam, OperatorVisualId Visual)[]
        {
            (new Vector3(-85.4f, 0.14f, -119.5f), CivilianRole.Resident, new Vector2(0.8f, 1.0f), OperatorVisualId.Magpie),
            (new Vector3(-90.5f, 0.14f, -126.0f), CivilianRole.VolunteerMedic, new Vector2(1.2f, 1.4f), OperatorVisualId.Heron),
            (new Vector3(85.4f, 0.14f, -1.0f), CivilianRole.UtilityWorker, new Vector2(0.8f, 1.0f), OperatorVisualId.Jackal),
            (new Vector3(90.0f, 0.14f, 1.5f), CivilianRole.CommunityGuard, new Vector2(1.2f, 1.3f), OperatorVisualId.Viper)
        };
        for (var index = 0; index < residents.Length; index++)
        {
            var placement = residents[index];
            var civilian = new CivilianNpc
            {
                Name = $"JianghaiInteriorResident_{index + 1:00}"
            };
            civilian.UseAuthoredVisual(placement.Visual);
            civilian.Configure(
                this,
                placement.Role,
                100 + index,
                0,
                Transform3D.Identity,
                placement.Position,
                placement.Roam);
            RegisterResidentialLanguageRefresher(civilian.SetLanguage);
            _levelRoot.AddChild(civilian);
            civilian.AddToGroup("jianghai_interior_resident");
            _civilians.Add(civilian);
            _oldTownInteriorResidentCount++;
        }
    }

    private void ConfigureRefineryMinimap()
    {
        var landmarks = new List<TacticalMapLandmark>
        {
            new(DeploymentPoint, "minimap_deploy", "DEPLOY", new Color(0.36f, 0.82f, 1.0f)),
            new(ExtractionPoint, "minimap_extract", "EXTRACT", new Color(0.32f, 0.95f, 0.66f)),
            new(RefineryExtractionMapBuilder.HotelCenter, "minimap_old_town_hotel", "GUANGCHANG PAWNSHOP", new Color(1.0f, 0.45f, 0.2f)),
            new(RefineryExtractionMapBuilder.TreasuryCenter, "minimap_old_town_treasury", "RED STAR ELECTRONICS", new Color(1.0f, 0.45f, 0.2f)),
            new(new Vector3(0, 0, -60), "minimap_old_town_plaza", "JIANGHAI SQUARE", new Color(0.95f, 0.73f, 0.3f)),
            new(new Vector3(0, 0, -126), "minimap_old_town_rooftop", "MARKET FOOTBRIDGE", new Color(0.45f, 0.85f, 1.0f)),
            new(new Vector3(-43, 0, -92), "minimap_old_town_canal", "WEST ARCADE", new Color(0.4f, 0.74f, 1.0f)),
            new(new Vector3(43, 0, -28), "minimap_old_town_garden", "RED STAR FACTORY ROW", new Color(0.48f, 0.9f, 0.55f)),
            new(new Vector3(0, 0, -184), "minimap_old_town_north_gate", "RIVER WHARF", new Color(0.82f, 0.78f, 0.7f)),
            new(new Vector3(0, 0, 48), "minimap_old_town_south_gate", "SOUTH GATE", new Color(0.82f, 0.78f, 0.7f))
        };
        _hud.ConfigureMinimap(new Rect2(-170, -220, MapWidthMeters, MapDepthMeters), landmarks);
        _hud.SetMinimapPlayer(_player.GlobalPosition, 0.0f);
    }

    private sealed record RefineryWeaponCaseDefinition(
        Vector3 Position,
        float Rotation,
        string EnglishName,
        string ChineseName,
        WeaponBuild Weapon,
        string[] Parts,
        string[] Equipment,
        string KnifeSkin);
}
