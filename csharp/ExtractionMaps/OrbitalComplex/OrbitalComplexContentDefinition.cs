using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

internal static class OrbitalComplexContentDefinition
{
    // Two low-risk lockers sit beside the intake spawn; the remaining cases
    // climb through breaker/archive security and end in the reactor vault.
    public static IReadOnlyList<OrbitalComplexWeaponCasePlacement> WeaponCases() => new[]
    {
        Case("intake_sidearm_alpha", new(-12, -15.35f, 72), 0, WeaponPlatform.P226, 0, LootGrade.Common, OrbitalComplexLootRisk.OuterRing, "Intake emergency sidearm locker", "\u8fdb\u6c34\u53e3\u5e94\u6025\u526f\u6b66\u5668\u67dc"),
        Case("intake_response_case", new(14, -15.35f, 68), Mathf.Pi, WeaponPlatform.M3A1, 0, LootGrade.Uncommon, OrbitalComplexLootRisk.OuterRing, "Intake response case", "\u8fdb\u6c34\u53e3\u5e94\u6025\u6b66\u5668\u7bb1"),
        Case("breaker_guard_case", new(-88, -15.35f, -8), -Mathf.Pi * 0.5f, WeaponPlatform.AK74, 1, LootGrade.Rare, OrbitalComplexLootRisk.ObjectiveDistrict, "Breaker-yard guard case", "\u65ad\u8def\u5668\u5385\u8b66\u536b\u7bb1"),
        Case("breaker_supervisor_vault", new(-108, -15.35f, -23), Mathf.Pi, WeaponPlatform.M4A1, 2, LootGrade.Epic, OrbitalComplexLootRisk.ObjectiveDistrict, "Breaker supervisor vault", "\u65ad\u8def\u5668\u4e3b\u7ba1\u6b66\u5668\u5e93"),
        Case("archive_security_case", new(88, -15.35f, -10), Mathf.Pi * 0.5f, WeaponPlatform.MP5A5, 1, LootGrade.Rare, OrbitalComplexLootRisk.ObjectiveDistrict, "Archive security case", "\u6863\u6848\u533a\u5b89\u4fdd\u7bb1"),
        Case("archive_response_armory", new(108, -15.35f, -24), 0, WeaponPlatform.ScarL, 2, LootGrade.Epic, OrbitalComplexLootRisk.ObjectiveDistrict, "Quarantine response armory", "\u68c0\u75ab\u5e94\u6025\u519b\u68b0\u5e93"),
        Case("calibration_catwalk_case", new(-48, -2.35f, -34), Mathf.Pi * 0.5f, WeaponPlatform.M24, 2, LootGrade.Epic, OrbitalComplexLootRisk.ObjectiveDistrict, "Calibration catwalk marksman case", "\u6821\u51c6\u6808\u9053\u5c04\u624b\u7bb1"),
        Case("cathode_maintenance_case", new(-31, -15.35f, -126), 0, WeaponPlatform.VSS, 1, LootGrade.Rare, OrbitalComplexLootRisk.ObjectiveDistrict, "Cathode maintenance case", "\u9634\u6781\u4e95\u7ef4\u4fee\u7bb1"),
        Case("ossuary_memory_case", new(148, -15.35f, -126), Mathf.Pi, WeaponPlatform.MP5A5, 2, LootGrade.Epic, OrbitalComplexLootRisk.ObjectiveDistrict, "Ossuary memory aisle case", "\u6570\u636e\u7070\u5806\u8bb0\u5fc6\u7bb1"),
        Case("reactor_black_recovery_case", new(0, -15.35f, -25), Mathf.Pi, WeaponPlatform.AWM, 2, LootGrade.Legendary, OrbitalComplexLootRisk.StormglassLockdown, "Reactor black recovery case", "\u53cd\u5e94\u5806\u9ed1\u8272\u56de\u6536\u7bb1"),
        Case("reactor_ceramic_case", new(0, -15.35f, -43), 0, WeaponPlatform.M4A1, 2, LootGrade.Legendary, OrbitalComplexLootRisk.StormglassLockdown, "Ceramic vault weapon case", "\u9676\u74f7\u9632\u62a4\u6b66\u5668\u7bb1")
    };

    public static IReadOnlyList<OrbitalComplexLootPlacement> GradedLoot() => new[]
    {
        Loot("intake_medical", new(-24, -15.35f, 76), LootGrade.Common, OrbitalComplexLootRisk.OuterRing, "Intake first-aid cache", "\u8fdb\u6c34\u53e3\u6025\u6551\u7269\u8d44"),
        Loot("intake_toolbox", new(24, -15.35f, 75), LootGrade.Common, OrbitalComplexLootRisk.OuterRing, "Intake maintenance toolbox", "\u8fdb\u6c34\u53e3\u7ef4\u4fee\u5de5\u5177\u7bb1"),
        Loot("pump_spares", new(-62, -15.35f, 34), LootGrade.Uncommon, OrbitalComplexLootRisk.OuterRing, "Coolant pump spare parts", "\u51b7\u5374\u6cf5\u7ec4\u5907\u4ef6"),
        Loot("canteen_stock", new(58, -15.35f, 44), LootGrade.Uncommon, OrbitalComplexLootRisk.OuterRing, "Crew canteen stock", "\u8239\u5458\u98df\u5802\u50a8\u5907"),
        Loot("west_service_cache", new(-140, -15.35f, -82), LootGrade.Common, OrbitalComplexLootRisk.OuterRing, "West coolant tunnel cache", "\u897f\u51b7\u5374\u96a7\u9053\u7269\u8d44"),
        Loot("east_service_cache", new(140, -15.35f, -84), LootGrade.Common, OrbitalComplexLootRisk.OuterRing, "East coolant tunnel cache", "\u4e1c\u51b7\u5374\u96a7\u9053\u7269\u8d44"),
        Loot("drydock_crane_tools", new(-20, -32.35f, -24), LootGrade.Uncommon, OrbitalComplexLootRisk.OuterRing, "Dry-dock crane tools", "\u5e72\u575e\u8d77\u91cd\u673a\u5de5\u5177"),
        Loot("drydock_recovery_kit", new(20, -32.35f, -46), LootGrade.Uncommon, OrbitalComplexLootRisk.OuterRing, "Capsule recovery kit", "\u8fd4\u56de\u8231\u56de\u6536\u5957\u4ef6"),
        Loot("breaker_bus_spares", new(-90, -15.35f, -5), LootGrade.Rare, OrbitalComplexLootRisk.ObjectiveDistrict, "Breaker bus spare assembly", "\u65ad\u8def\u5668\u6bcd\u7ebf\u5907\u4ef6"),
        Loot("breaker_control_cache", new(-104, -15.35f, 10), LootGrade.Rare, OrbitalComplexLootRisk.ObjectiveDistrict, "Breaker control cache", "\u65ad\u8def\u5668\u63a7\u5236\u7269\u8d44"),
        Loot("breaker_superconductor", new(-108, -15.35f, -16), LootGrade.Epic, OrbitalComplexLootRisk.ObjectiveDistrict, "Storm-rated superconductor", "\u6297\u98ce\u66b4\u8d85\u5bfc\u4f53"),
        Loot("archive_sample_case", new(90, -15.35f, -6), LootGrade.Rare, OrbitalComplexLootRisk.ObjectiveDistrict, "Quarantine sample case", "\u68c0\u75ab\u6837\u672c\u7bb1"),
        Loot("archive_cipher_rack", new(104, -15.35f, 11), LootGrade.Rare, OrbitalComplexLootRisk.ObjectiveDistrict, "Archive cipher rack", "\u6863\u6848\u5bc6\u7801\u67b6"),
        Loot("archive_flight_recorder", new(108, -15.35f, -18), LootGrade.Epic, OrbitalComplexLootRisk.ObjectiveDistrict, "Sealed flight-recorder case", "\u5bc6\u5c01\u98de\u884c\u8bb0\u5f55\u5668\u7bb1"),
        Loot("catwalk_optics", new(46, -2.35f, -34), LootGrade.Rare, OrbitalComplexLootRisk.ObjectiveDistrict, "Calibration optics case", "\u6821\u51c6\u5149\u5b66\u4eea\u5668\u7bb1"),
        Loot("catwalk_telemetry", new(0, -2.35f, -88), LootGrade.Epic, OrbitalComplexLootRisk.ObjectiveDistrict, "Live telemetry buffer", "\u5b9e\u65f6\u9065\u6d4b\u7f13\u5b58"),
        Loot("cathode_coolant_coil", new(-34, -15.35f, -126), LootGrade.Rare, OrbitalComplexLootRisk.ObjectiveDistrict, "Cathode coolant coil", "\u9634\u6781\u4e95\u51b7\u5374\u7ebf\u5708"),
        Loot("cathode_pressure_gauge", new(34, -15.35f, -126), LootGrade.Epic, OrbitalComplexLootRisk.ObjectiveDistrict, "Cathode pressure gauge", "\u9634\u6781\u4e95\u538b\u529b\u8868"),
        Loot("ossuary_memory_shard", new(133, -15.35f, -145), LootGrade.Epic, OrbitalComplexLootRisk.ObjectiveDistrict, "Quarantine memory shard", "\u68c0\u75ab\u8bb0\u5fc6\u788e\u7247"),
        Loot("undertow_pump_key", new(-112, -15.35f, 47), LootGrade.Rare, OrbitalComplexLootRisk.ObjectiveDistrict, "Undertow pump key", "\u6697\u6f6e\u6cf5\u7ec4\u94a5\u5319"),
        Loot("reactor_lens", new(-8, -15.35f, -30), LootGrade.Legendary, OrbitalComplexLootRisk.StormglassLockdown, "Stormglass phased lens", "\u98ce\u66b4\u73bb\u7483\u76f8\u63a7\u955c\u7247"),
        Loot("reactor_guidance_core", new(8, -15.35f, -38), LootGrade.Legendary, OrbitalComplexLootRisk.StormglassLockdown, "Recovered guidance core", "\u56de\u6536\u8231\u5bfc\u822a\u6838\u5fc3"),
        Loot("tide_gate_tools", new(30, -15.35f, -178), LootGrade.Uncommon, OrbitalComplexLootRisk.OuterRing, "Tide-gate actuator tools", "\u6f6e\u95f8\u6267\u884c\u5668\u5de5\u5177")
    };

    public static IReadOnlyList<OrbitalComplexValuablePlacement> Valuables() => new[]
    {
        Valuable("intake_coffee", new(-29, -15.35f, 64), ValuableItemKind.CannedCoffee, LootGrade.Common, OrbitalComplexLootRisk.OuterRing),
        Valuable("dock_tools", new(23, -32.35f, -21), ValuableItemKind.HandToolSet, LootGrade.Uncommon, OrbitalComplexLootRisk.OuterRing),
        Valuable("west_phone", new(-142, -15.35f, -86), ValuableItemKind.SmartPhone, LootGrade.Uncommon, OrbitalComplexLootRisk.OuterRing),
        Valuable("east_watch", new(143, -15.35f, -95), ValuableItemKind.Wristwatch, LootGrade.Uncommon, OrbitalComplexLootRisk.OuterRing),
        Valuable("breaker_camera", new(-84, -15.35f, -14), ValuableItemKind.VintageCamera, LootGrade.Rare, OrbitalComplexLootRisk.ObjectiveDistrict),
        Valuable("breaker_coin", new(-111, -15.35f, -2), ValuableItemKind.CollectorCoin, LootGrade.Epic, OrbitalComplexLootRisk.ObjectiveDistrict),
        Valuable("archive_gpu", new(84, -15.35f, -16), ValuableItemKind.GraphicsCard, LootGrade.Rare, OrbitalComplexLootRisk.ObjectiveDistrict),
        Valuable("archive_drive", new(111, -15.35f, -4), ValuableItemKind.EncryptedDrive, LootGrade.Epic, OrbitalComplexLootRisk.ObjectiveDistrict),
        Valuable("catwalk_drive", new(50, -2.35f, -34), ValuableItemKind.EncryptedDrive, LootGrade.Epic, OrbitalComplexLootRisk.ObjectiveDistrict),
        Valuable("cathode_transducer", new(-9, -15.35f, -126), ValuableItemKind.TideHunterTransponder, LootGrade.Epic, OrbitalComplexLootRisk.ObjectiveDistrict),
        Valuable("ossuary_black_box", new(142, -15.35f, -139), ValuableItemKind.EncryptedDrive, LootGrade.Epic, OrbitalComplexLootRisk.ObjectiveDistrict),
        Valuable("undertow_ceramic_core", new(-104, -15.35f, 42), ValuableItemKind.GraphicsCard, LootGrade.Rare, OrbitalComplexLootRisk.ObjectiveDistrict),
        Valuable("reactor_clock", new(-5, -15.35f, -28), ValuableItemKind.AntiqueClock, LootGrade.Legendary, OrbitalComplexLootRisk.StormglassLockdown),
        Valuable("reactor_transponder", new(5, -15.35f, -40), ValuableItemKind.TideHunterTransponder, LootGrade.Legendary, OrbitalComplexLootRisk.StormglassLockdown)
    };

    public static IReadOnlyList<OrbitalComplexExplosivePlacement> Explosives() => new[]
    {
        Explosive("breaker_oil_a", new(-78, -15.35f, -4), 1.0f, "breaker_chain"),
        Explosive("breaker_oil_b", new(-82, -15.35f, -10), 1.0f, "breaker_chain"),
        Explosive("breaker_oil_c", new(-81, -15.35f, -16), 1.1f, "breaker_chain"),
        Explosive("archive_decon_a", new(78, -15.35f, -12), 0.9f, "archive_chain"),
        Explosive("archive_decon_b", new(82, -15.35f, -18), 0.9f, "archive_chain"),
        Explosive("dock_fuel_a", new(-23, -32.35f, -39), 1.15f, "dock_chain"),
        Explosive("dock_fuel_b", new(-19, -32.35f, -44), 1.15f, "dock_chain"),
        Explosive("north_actuator_a", new(-26, -15.35f, -178), 1.0f, "tide_gate_chain"),
        Explosive("north_actuator_b", new(26, -15.35f, -178), 1.0f, "tide_gate_chain"),
        Explosive("south_pump_fuel", new(-55, -15.35f, 37), 0.85f, "south_pump")
    };

    private static OrbitalComplexWeaponCasePlacement Case(
        string id, Vector3 position, float yaw, WeaponPlatform platform, int tier,
        LootGrade grade, OrbitalComplexLootRisk risk, string english, string chinese)
        => new(id, position, yaw, platform, tier, grade, risk, english, chinese);

    private static OrbitalComplexLootPlacement Loot(
        string id, Vector3 position, LootGrade grade, OrbitalComplexLootRisk risk,
        string english, string chinese)
        => new(id, position, grade, risk, english, chinese);

    private static OrbitalComplexValuablePlacement Valuable(
        string id, Vector3 position, ValuableItemKind kind, LootGrade grade,
        OrbitalComplexLootRisk risk)
        => new(id, position, kind, grade, risk);

    private static OrbitalComplexExplosivePlacement Explosive(
        string id, Vector3 position, float scale, string group)
        => new(id, position, scale, group);
}
