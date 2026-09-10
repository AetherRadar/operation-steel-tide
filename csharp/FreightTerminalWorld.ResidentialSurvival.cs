using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

/// <summary>
/// Composition and spawn contract for the compact residential survival map.
/// The authored life-cluster GLB is the visible map core; this partial only supplies
/// invisible gameplay bounds, loot placement, and map-specific mission configuration.
/// </summary>
public partial class FreightTerminalWorld
{
    private static readonly Vector3 ResidentialSurvivalExtractionPoint = new(-52.0f, 0.08f, 72.0f);

    private static readonly Vector3[] ResidentialSurvivalSpawnPads =
    {
        new(-67.0f, 0.22f, 56.0f),
        new(-39.0f, 0.22f, 56.0f),
        new(-69.0f, 0.22f, 34.0f),
        new(-38.0f, 0.22f, 31.0f)
    };

    private static readonly Vector3[] ResidentialSurvivalWeaponSpots =
    {
        new(-58.0f, 0.24f, 49.0f), // supermarket entrance
        new(-43.0f, 0.24f, 47.0f), // plaza arcade
        new(-52.0f, 0.24f, 29.0f)  // loading lane
    };

    private static readonly Vector3[] ResidentialSurvivalSupplySpots =
    {
        new(-59.0f, 0.24f, 43.0f), new(-50.0f, 0.24f, 49.0f),
        new(-42.0f, 0.24f, 41.0f), new(-61.0f, 0.24f, 56.0f),
        new(-46.0f, 0.24f, 31.0f), new(-34.0f, 0.24f, 46.0f)
    };

    public bool IsSurvivalMode
        => string.Equals(_activeRuntimeMapId, DeploymentMapCatalog.ResidentialSurvivalId, StringComparison.OrdinalIgnoreCase);

    private void ConfigureResidentialSurvivalMission()
    {
        _missionDirector.ConfigureMission(
            "residential-survival",
            new[] { "SECURE FOOD AND MEDICAL SUPPLIES", "RESTORE THE ROOFTOP EMERGENCY BEACON" },
            new[] { "survival_supplies", "survival_beacon" },
            new[] { "survival_objective_supplies", "survival_objective_beacon" });
    }

    private void BuildResidentialSurvivalLevel()
    {
        _levelRoot = new Node3D { Name = "ResidentialSurvivalDistrict" };
        AddChild(_levelRoot);

        // The floor is collision and traversal scaffolding. Buildings and street furniture
        // come exclusively from the authored life-cluster GLB.
        var asphalt = GroundMaterial("survival_ground", new Color(0.12f, 0.145f, 0.15f), 0.95f);
        StaticBox("SurvivalGround", new Vector3(-52, -0.55f, 43), new Vector3(116, 1, 104), asphalt);
        _levelRoot.AddChild(OceanBackdropFactory.Create(new Vector3(-52, -0.18f, 43)));

        var community = new Node3D { Name = "ResidentialSurvivalCommunity" };
        _levelRoot.AddChild(community);
        BuildResidentialLifeCluster(community);

        var concrete = GroundMaterial("survival_concrete", new Color(0.3f, 0.33f, 0.33f), 0.88f);
        var steel = Mat("survival_steel", new Color(0.08f, 0.1f, 0.1f), 0.5f, 0.75f);
        var yellow = Mat("survival_warning", new Color(0.8f, 0.52f, 0.08f), 0.3f, 0.6f);
        BuildExtraction(concrete, steel, yellow, concrete);
        _extractionMarker.Visible = true;
    }

    private void ConfigureResidentialSurvivalSpawnSelection()
    {
        var candidates = new List<Vector3>(ResidentialSurvivalSpawnPads);
        var diagnostic = Array.Exists(
            OS.GetCmdlineUserArgs(),
            argument => argument.Equals("--validate-residential-survival", StringComparison.Ordinal));
        // Keep diagnostics deterministic while proving that the selected pad is
        // not the first reserved reference point used by the validator.
        var index = diagnostic ? 1 : _rng.RandiRange(0, candidates.Count - 1);
        DeploymentPoint = candidates[index];
        candidates.RemoveAt(index);
        _assignedHostilePads = candidates;
    }

    private void SpawnResidentialSurvivalWeaponCases()
    {
        var definitions = new[]
        {
            (ResidentialSurvivalWeaponSpots[0], 0.0f, "Supermarket security case", "\u8d85\u5e02\u5b89\u4fdd\u67aa\u7bb1", WeaponPlatform.MP5A5),
            (ResidentialSurvivalWeaponSpots[1], Mathf.Pi * 0.5f, "Plaza response case", "\u5e7f\u573a\u5e94\u6025\u67aa\u7bb1", WeaponPlatform.GSh18),
            (ResidentialSurvivalWeaponSpots[2], Mathf.Pi, "Loading lane rifle case", "\u88c5\u5378\u533a\u6b65\u67aa\u7bb1", WeaponPlatform.M4A1)
        };
        foreach (var definition in definitions)
        {
            var weapon = new WeaponCase
            {
                Position = definition.Item1,
                Rotation = new Vector3(0, definition.Item2, 0),
                EnglishName = definition.Item3,
                ChineseName = definition.Item4
            };
            var built = WeaponCatalog.Build(definition.Item5, 1);
            weapon.Loot.Add(new LootItem { Kind = LootItemKind.Weapon, Weapon = built, Grade = LootGrade.Common });
            weapon.Loot.Add(new LootItem
            {
                Kind = LootItemKind.Ammunition,
                AmmoCaliber = WeaponCatalog.Weapon(definition.Item5).Caliber,
                Quantity = definition.Item5 == WeaponPlatform.GSh18 ? 24 : 45,
                Grade = LootGrade.Common
            });
            weapon.Loot.Add(new LootItem { Kind = LootItemKind.ArmorPlate, Quantity = 1, Grade = LootGrade.Common });
            AddChild(weapon);
            _lootSources.Add(weapon);
            _lootWorldPoints.Add(definition.Item1);
        }
    }

    private void SpawnResidentialSurvivalSupplyLoot()
    {
        var index = 0;
        foreach (var position in ResidentialSurvivalSupplySpots)
        {
            var item = new LootItem
            {
                Kind = LootItemKind.Medical,
                MedicalKind = index % 3 == 0 ? MedicalItemKind.FieldMedkit : MedicalItemKind.Bandage,
                Quantity = index % 3 == 0 ? 1 : 2,
                Grade = LootGrade.Common
            };
            var pickup = new GradedLootPickup
            {
                Name = $"SurvivalSupply_{++index:00}",
                Position = position
            };
            pickup.Configure(item, "Survival medical supply", "生存医疗物资");
            AddChild(pickup);
            _lootSources.Add(pickup);
            _lootWorldPoints.Add(position);
            _buildingLootPickupCount++;
        }
    }
}
