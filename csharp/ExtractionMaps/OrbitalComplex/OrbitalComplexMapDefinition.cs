using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

/// <summary>
/// Deterministic gameplay topology for the Falltide Recovery Array. Godot
/// coordinates are used throughout: X/Z are the bunker plan and Y is the
/// vertical level (service deck -15.6, reactor pit -33, catwalk -2.4).
/// The GLB owns visible architecture; this contract owns encounter logic.
/// </summary>
public static class OrbitalComplexMapDefinition
{
    public const string MapId = "orbital_complex";
    public const string BreakerObjectiveId = "reroute_breaker_bus";
    public const string QuarantineObjectiveId = "purge_quarantine_archive";
    public const string BreakerObjectiveEnglishName = "STABILIZE THE STORM-GRID BREAKERS";
    public const string QuarantineObjectiveEnglishName = "AUTHORIZE THE QUARANTINE RELEASE";
    public const string BreakerObjectiveLocalizationKey = "falltide_objective_breakers";
    public const string QuarantineObjectiveLocalizationKey = "falltide_objective_quarantine";
    public const string BreakerObjectiveChineseName = "\u7a33\u5b9a\u98ce\u66b4\u7535\u7f51\u65ad\u8def\u5668";
    public const string QuarantineObjectiveChineseName = "\u6388\u6743\u89e3\u9664\u68c0\u75ab\u5c01\u9501";
    public const float WidthMeters = 340.0f;
    public const float DepthMeters = 320.0f;
    public const float CenterZ = -60.0f;
    public const float MinimumY = -34.0f;
    public const float MaximumY = 24.0f;
    public const float BlackwaterSurfaceY = -28.25f;

    public static readonly Vector3 StormglassArrayCenter = new(0.0f, -15.6f, -34.0f);
    public static readonly Vector3 DryDockCenter = new(0.0f, -31.8f, -34.0f);
    public static readonly Vector3 BreakerYardCenter = new(-100.0f, -15.6f, -6.0f);
    public static readonly Vector3 QuarantineArchiveCenter = new(100.0f, -15.6f, -6.0f);
    public static readonly Vector3 TideGateCenter = new(0.0f, -15.6f, -194.0f);
    public static readonly Vector3 IntakeCausewayCenter = new(0.0f, -15.6f, 78.0f);
    public static readonly Vector3 CathodeWellCenter = new(0.0f, -15.6f, -126.0f);
    public static readonly Vector3 DataOssuaryCenter = new(133.0f, -15.6f, -126.0f);
    public static readonly Vector3 UndertowSumpCenter = new(-112.0f, -15.6f, 42.0f);

    public static bool IsInBlackwaterSwimVolume(Vector3 position)
        => IsInsideBlackwaterFootprint(position)
            && position.Y >= -33.0f
            && position.Y <= BlackwaterSurfaceY + 1.1f;

    public static bool IsInsideBlackwaterFootprint(Vector3 position)
    {
        var localX = Mathf.Abs(position.X);
        var localZ = Mathf.Abs(position.Z + 34.0f);
        if (localX <= 25.0f && localZ <= 26.0f)
        {
            return true;
        }

        // Match the rounded DCC pool corners rather than treating the dry rim
        // as an invisible rectangular swimming box.
        var cornerX = Mathf.Max(0.0f, localX - 18.0f);
        var cornerZ = Mathf.Max(0.0f, localZ - 19.0f);
        return cornerX * cornerX + cornerZ * cornerZ <= 7.0f * 7.0f;
    }

    public static OrbitalComplexMapLayout Build(ulong sharedWorldSeed)
    {
        return new OrbitalComplexMapLayout(
            sharedWorldSeed,
            new OrbitalComplexMapBounds(
                new Rect2(-WidthMeters * 0.5f, CenterZ - DepthMeters * 0.5f,
                    WidthMeters, DepthMeters),
                MinimumY,
                MaximumY),
            PlayerSpawnPads(), RivalSpawnPads(), GarrisonSpawns(), PatrolRoutes(),
            CoverPoints(), QrfSpawns(), BossRoute(), ObjectiveOrder(sharedWorldSeed),
            new OrbitalComplexExtractionDefinition(
                "north_tide_gate", new Vector3(0.0f, -15.6f, -211.0f), 7.0f,
                "North tide-gate recovery rail", "\u5317\u4fa7\u6f6e\u95f8\u56de\u6536\u8f68\u9053"),
            OrbitalComplexContentDefinition.WeaponCases(),
            OrbitalComplexContentDefinition.GradedLoot(),
            OrbitalComplexContentDefinition.Valuables(),
            OrbitalComplexContentDefinition.Explosives(), MinimapLandmarks(),
            RouteProbes(), CollisionBoxes(), Ramps(), PowerGates());
    }

    private static IReadOnlyList<OrbitalComplexObjectiveDefinition> ObjectiveOrder(
        ulong sharedWorldSeed)
    {
        var breaker = new OrbitalComplexObjectiveDefinition(
            BreakerObjectiveId, BreakerObjectiveEnglishName,
            BreakerObjectiveChineseName, "breaker_yard",
            BreakerYardCenter, -Mathf.Pi * 0.5f,
            OrbitalComplexVerticalLayer.ServiceDeck, "falltide_breaker_bus_rerouted");
        var archive = new OrbitalComplexObjectiveDefinition(
            QuarantineObjectiveId, QuarantineObjectiveEnglishName,
            QuarantineObjectiveChineseName, "quarantine_archive",
            QuarantineArchiveCenter, Mathf.Pi * 0.5f,
            OrbitalComplexVerticalLayer.ServiceDeck, "falltide_archive_purged");
        return (sharedWorldSeed & 1UL) == 0UL
            ? new[] { breaker, archive } : new[] { archive, breaker };
    }

    private static IReadOnlyList<OrbitalComplexSpawnPad> PlayerSpawnPads() => new[]
    {
        // Keep the whole squad in the north intake pocket.  The old 78–84 m
        // pads put operators inside the first garrison's 34 m acquisition
        // bubble as soon as deployment protection ended.  Moving the pocket
        // ten metres up the intake spine preserves the compact squad layout
        // while giving players a quiet first loot decision and a covered route
        // toward either objective district.
        PlayerPad("intake_alpha", new Vector3(-4.5f, -15.42f, 94.0f)),
        PlayerPad("intake_bravo", new Vector3(4.5f, -15.42f, 94.0f)),
        PlayerPad("intake_charlie", new Vector3(-4.5f, -15.42f, 88.0f)),
        PlayerPad("intake_delta", new Vector3(4.5f, -15.42f, 88.0f))
    };

    private static OrbitalComplexSpawnPad PlayerPad(string id, Vector3 position)
        // Face the sealed south intake bay on deployment.  The former target
        // pointed straight down the 60 m intake spine toward the first
        // garrison, making the opening read as an immediate firing lane.
        => new(id, position, new Vector3(0.0f, -15.42f, 104.0f),
            OrbitalComplexVerticalLayer.ServiceDeck);

    private static IReadOnlyList<OrbitalComplexSpawnPad> RivalSpawnPads() => new[]
    {
        RivalPad("northwest_silo", new Vector3(-146.0f, -15.42f, -198.0f)),
        RivalPad("northeast_silo", new Vector3(146.0f, -15.42f, -198.0f)),
        RivalPad("west_coolant", new Vector3(-150.0f, -15.42f, -92.0f)),
        RivalPad("east_coolant", new Vector3(150.0f, -15.42f, -92.0f))
    };

    private static OrbitalComplexSpawnPad RivalPad(string id, Vector3 position)
        => new(id, position, new Vector3(0.0f, -15.42f, -34.0f),
            OrbitalComplexVerticalLayer.ServiceDeck);

    private static IReadOnlyList<Vector3> GarrisonSpawns() => new Vector3[]
    {
        new(-122, -15.42f, -2), new(-112, -15.42f, -22), new(-91, -15.42f, -25),
        new(-80, -15.42f, 10), new(122, -15.42f, -2), new(112, -15.42f, -24),
        new(91, -15.42f, -27), new(80, -15.42f, 11),
        // Hold the intake garrison one room deeper than the deployment
        // pocket.  This creates a readable first contact (the side aisles at
        // roughly 50 m) instead of an immediate spawn-door firefight.
        new(-28, -15.42f, 44), new(28, -15.42f, 44), new(-27, -15.42f, 24),
        new(27, -15.42f, 24), new(-31, -15.42f, -16), new(31, -15.42f, -16),
        new(-24, -15.42f, -54), new(24, -15.42f, -54),
        new(-18, -32.35f, -20), new(18, -32.35f, -20),
        new(-18, -32.35f, -48), new(18, -32.35f, -48),
        // CharacterBody3D spawn positions are feet-level.  The catwalk deck
        // collision top is Y=-2.375, so keep these roots on the same surface
        // as the patrol route and loot anchors instead of burying their feet
        // 0.175 m into the deck.
        new(-70, -2.35f, -34), new(70, -2.35f, -34),
        new(-43, -2.35f, -88), new(43, -2.35f, -88),
        new(-31, -15.42f, -178), new(31, -15.42f, -178),
        // The second-pass Cathode Well and Data Ossuary give the north
        // transition its own close-range garrison instead of leaving it as a
        // purely decorative run between the objective districts.
        new(-27, -15.42f, -108), new(27, -15.42f, -144),
        new(118, -15.42f, -108), new(148, -15.42f, -146),
        new(-142, -15.42f, 42), new(-86, -15.42f, 42)
    };

    private static IReadOnlyList<OrbitalComplexPatrolRoute> PatrolRoutes() => new[]
    {
        Route("breaker_perimeter", OrbitalComplexVerticalLayer.ServiceDeck,
            new(-128, -15.35f, 12), new(-78, -15.35f, 12),
            new(-78, -15.35f, -28), new(-128, -15.35f, -28)),
        Route("archive_perimeter", OrbitalComplexVerticalLayer.ServiceDeck,
            new(128, -15.35f, 12), new(78, -15.35f, 12),
            new(78, -15.35f, -30), new(128, -15.35f, -30)),
        Route("intake_spine", OrbitalComplexVerticalLayer.ServiceDeck,
            // Keep patrol traffic out of the sealed deployment pocket.  The
            // former z=88 waypoints let intake garrison path directly into
            // the player spawn before the first cover decision.
            new(-30, -15.35f, 62), new(30, -15.35f, 62),
            new(30, -15.35f, 22), new(-30, -15.35f, 22)),
        Route("reactor_ring", OrbitalComplexVerticalLayer.ServiceDeck,
            new(-32, -15.35f, -18), new(0, -15.35f, -6),
            new(32, -15.35f, -18), new(30, -15.35f, -55),
            new(0, -15.35f, -68), new(-30, -15.35f, -55)),
        Route("drydock_lower", OrbitalComplexVerticalLayer.DryDock,
            new(-22, -32.35f, -18), new(22, -32.35f, -18),
            new(22, -32.35f, -52), new(-22, -32.35f, -52)),
        Route("calibration_catwalk", OrbitalComplexVerticalLayer.Catwalk,
            new(-78, -2.35f, -34), new(-42, -2.35f, -34),
            new(-42, -2.35f, -88), new(-78, -2.35f, -88)),
        Route("tide_gate_watch", OrbitalComplexVerticalLayer.ServiceDeck,
            new(-42, -15.35f, -164), new(-20, -15.35f, -194),
            new(20, -15.35f, -194), new(42, -15.35f, -164)),
        Route("cathode_well_ring", OrbitalComplexVerticalLayer.ServiceDeck,
            new(-30, -15.35f, -104), new(-30, -15.35f, -148),
            new(30, -15.35f, -148), new(30, -15.35f, -104)),
        Route("ossuary_memory_aisle", OrbitalComplexVerticalLayer.ServiceDeck,
            new(118, -15.35f, -102), new(150, -15.35f, -102),
            new(150, -15.35f, -150), new(118, -15.35f, -150)),
        Route("undertow_pump_lane", OrbitalComplexVerticalLayer.ServiceDeck,
            new(-150, -15.35f, 44), new(-118, -15.35f, 30),
            new(-82, -15.35f, 44), new(-118, -15.35f, 58))
    };

    private static OrbitalComplexPatrolRoute Route(
        string id, OrbitalComplexVerticalLayer layer, params Vector3[] waypoints)
        => new(id, waypoints, true, layer);

    private static IReadOnlyList<Vector3> CoverPoints() => new Vector3[]
    {
        new(-136, -15.1f, 20), new(-120, -15.1f, 20), new(-96, -15.1f, 20),
        new(-82, -15.1f, -16), new(-120, -15.1f, -18), new(-92, -15.1f, -32),
        new(136, -15.1f, 20), new(120, -15.1f, 20), new(96, -15.1f, 20),
        new(82, -15.1f, -18), new(120, -15.1f, -20), new(92, -15.1f, -34),
        new(-38, -15.1f, 76), new(38, -15.1f, 76), new(-38, -15.1f, 42),
        new(38, -15.1f, 42), new(-38, -15.1f, 10), new(38, -15.1f, 10),
        new(-38, -15.1f, -24), new(38, -15.1f, -24), new(-38, -15.1f, -58),
        new(38, -15.1f, -58), new(-26, -32.0f, -18), new(26, -32.0f, -18),
        new(-26, -32.0f, -50), new(26, -32.0f, -50), new(-72, -2.0f, -34),
        new(-48, -2.0f, -34), new(48, -2.0f, -34), new(72, -2.0f, -34),
        new(-72, -2.0f, -88), new(-48, -2.0f, -88), new(48, -2.0f, -88),
        new(72, -2.0f, -88), new(-34, -15.1f, -176), new(34, -15.1f, -176),
        new(-20, -15.1f, -204), new(20, -15.1f, -204),
        // The north detour is intentionally cover-rich: the Cathode Well ring
        // and the Ossuary aisle are exposed sightline breaks, not dead-end art.
        new(-34, -15.1f, -108), new(34, -15.1f, -108),
        new(-34, -15.1f, -144), new(34, -15.1f, -144),
        new(112, -15.1f, -108), new(154, -15.1f, -108),
        new(112, -15.1f, -144), new(154, -15.1f, -144),
        // A low pump lane under the west service run gives the southern
        // approach a third angle into the reactor spine.
        new(-150, -15.1f, 42), new(-118, -15.1f, 30),
        new(-86, -15.1f, 42), new(-118, -15.1f, 58)
    };

    private static IReadOnlyList<Vector3> QrfSpawns() => new Vector3[]
    {
        new(-42, -15.3f, -207), new(-33, -15.3f, -199),
        new(42, -15.3f, -207), new(33, -15.3f, -199),
        new(-154, -15.3f, -112), new(-150, -15.3f, -98),
        new(154, -15.3f, -112), new(150, -15.3f, -98)
    };

    private static IReadOnlyList<Vector3> BossRoute() => new Vector3[]
    {
        new(-58, -15.25f, 18), new(-32, -15.25f, 4), new(0, -15.25f, -6),
        new(34, -15.25f, 4), new(60, -15.25f, -18), new(64, -15.25f, -54),
        new(54, -15.25f, -84), new(28, -15.25f, -106), new(0, -15.25f, -116),
        new(-30, -15.25f, -106), new(-56, -15.25f, -84), new(-64, -15.25f, -54),
        new(-36, -15.25f, -30), new(0, -15.25f, -22), new(36, -15.25f, -34),
        new(0, -32.0f, -34)
    };

    private static IReadOnlyList<OrbitalComplexMinimapLandmark> MinimapLandmarks() => new[]
    {
        Landmark("intake", IntakeCausewayCenter, "minimap_falltide_intake_causeway", "INTAKE CAUSEWAY", new(0.36f, 0.82f, 1.0f), OrbitalComplexVerticalLayer.ServiceDeck),
        Landmark("dry_dock", DryDockCenter, "minimap_falltide_impact_drydock", "CAPSULE DRY DOCK", new(0.34f, 0.76f, 0.92f), OrbitalComplexVerticalLayer.DryDock),
        Landmark("breaker", BreakerYardCenter, "minimap_falltide_breaker_yard", "BREAKER YARD", new(1.0f, 0.55f, 0.18f), OrbitalComplexVerticalLayer.ServiceDeck),
        Landmark("archive", QuarantineArchiveCenter, "minimap_falltide_quarantine_archive", "QUARANTINE ARCHIVE", new(0.72f, 0.42f, 1.0f), OrbitalComplexVerticalLayer.ServiceDeck),
        Landmark("stormglass", StormglassArrayCenter, "minimap_falltide_stormglass_array", "STORMGLASS REACTOR HALL", new(1.0f, 0.28f, 0.16f), OrbitalComplexVerticalLayer.ServiceDeck),
        Landmark("catwalk", new Vector3(0, -2.35f, -88), "minimap_falltide_calibration_catwalk", "CALIBRATION CATWALK", new(0.46f, 0.9f, 1.0f), OrbitalComplexVerticalLayer.Catwalk),
        Landmark("west_service", new Vector3(-148, -15.6f, -92), "minimap_falltide_west_service", "WEST COOLANT TUNNEL", new(0.62f, 0.72f, 0.76f), OrbitalComplexVerticalLayer.ServiceDeck),
        Landmark("east_service", new Vector3(148, -15.6f, -92), "minimap_falltide_east_service", "EAST COOLANT TUNNEL", new(0.47f, 0.86f, 0.63f), OrbitalComplexVerticalLayer.ServiceDeck),
        Landmark("tide_gate", TideGateCenter, "minimap_falltide_tide_gate", "NORTH TIDE GATE", new(0.32f, 0.95f, 0.66f), OrbitalComplexVerticalLayer.ServiceDeck),
        Landmark("recovery_rail", new Vector3(0, -15.6f, -211), "minimap_extract", "RECOVERY RAIL", new(0.32f, 0.95f, 0.66f), OrbitalComplexVerticalLayer.ServiceDeck),
        Landmark("cathode_well", CathodeWellCenter, "minimap_falltide_cathode_well", "CATHODE WELL", new(0.18f, 0.88f, 0.94f), OrbitalComplexVerticalLayer.ServiceDeck),
        Landmark("data_ossuary", DataOssuaryCenter, "minimap_falltide_data_ossuary", "DATA OSSUARY", new(0.72f, 0.54f, 1.0f), OrbitalComplexVerticalLayer.ServiceDeck),
        Landmark("undertow_sump", UndertowSumpCenter, "minimap_falltide_undertow_sump", "UNDERTOW SUMP", new(0.10f, 0.72f, 0.82f), OrbitalComplexVerticalLayer.ServiceDeck),
        Landmark("blackwater_pool", new Vector3(0, -28.25f, -34), "minimap_falltide_blackwater_pool", "BLACKWATER POOL", new(0.10f, 0.50f, 0.64f), OrbitalComplexVerticalLayer.DryDock)
    };

    private static OrbitalComplexMinimapLandmark Landmark(
        string id, Vector3 position, string key, string name, Color color,
        OrbitalComplexVerticalLayer layer)
        => new(id, position, key, name, color, layer);

    private static IReadOnlyList<OrbitalComplexRouteProbe> RouteProbes() => new[]
    {
        Probe("intake_to_reactor", new(0, -14.4f, 88), new(0, -14.4f, -6), 3.5f, 0, OrbitalComplexVerticalLayer.ServiceDeck),
        Probe("west_service_lane", new(-146, -14.4f, 45), new(-142, -14.4f, -190), 3.2f, 0, OrbitalComplexVerticalLayer.ServiceDeck),
        Probe("east_service_lane", new(146, -14.4f, 45), new(142, -14.4f, -190), 3.2f, 0, OrbitalComplexVerticalLayer.ServiceDeck),
        Probe("breaker_approach", new(-70, -14.4f, 14), new(-104, -14.4f, -22), 2.8f, 0, OrbitalComplexVerticalLayer.ServiceDeck),
        Probe("archive_approach", new(70, -14.4f, 14), new(104, -14.4f, -22), 2.8f, 0, OrbitalComplexVerticalLayer.ServiceDeck),
        Probe("drydock_west_access", new(-32, -31.2f, -22), new(-32, -14.0f, -34), 2.4f, 0, OrbitalComplexVerticalLayer.DryDock),
        Probe("drydock_east_access", new(32, -31.2f, -46), new(32, -14.0f, -34), 2.4f, 0, OrbitalComplexVerticalLayer.DryDock),
        Probe("catwalk_west_access", new(-88, -14.0f, -34), new(-76, -2.0f, -34), 2.2f, 0, OrbitalComplexVerticalLayer.Catwalk),
        Probe("catwalk_east_access", new(88, -14.0f, -34), new(76, -2.0f, -34), 2.2f, 0, OrbitalComplexVerticalLayer.Catwalk),
        Probe("upper_bypass", new(-8, -1.8f, -88), new(8, -1.8f, -88), 2.4f, 1, OrbitalComplexVerticalLayer.Catwalk),
        // The capsule-bay shell occupies the centerline immediately south of
        // the gate.  Keep the AI's approach honest: it uses the two side
        // maintenance aisles, then converges once the stage-driven gate opens.
        Probe("tide_gate_west_passage", new(-12, -14.4f, -181), new(-12, -14.4f, -205), 3.0f, 1, OrbitalComplexVerticalLayer.ServiceDeck),
        Probe("tide_gate_east_passage", new(12, -14.4f, -181), new(12, -14.4f, -205), 3.0f, 1, OrbitalComplexVerticalLayer.ServiceDeck),
        Probe("breaker_power_bus", new(-118, -14.4f, -2), new(-84, -14.4f, -2), 2.6f, 1, OrbitalComplexVerticalLayer.ServiceDeck),
        Probe("stormglass_vault", new(0, -14.4f, -20), new(0, -14.4f, -52), 2.6f, 2, OrbitalComplexVerticalLayer.ServiceDeck),
        Probe("reactor_pit_crossing", new(-20, -31.2f, -34), new(20, -31.2f, -34), 2.6f, 2, OrbitalComplexVerticalLayer.DryDock),
        Probe("cathode_well_crossing", new(-28, -14.4f, -126), new(28, -14.4f, -126), 3.2f, 1, OrbitalComplexVerticalLayer.ServiceDeck),
        Probe("ossuary_memory_aisle", new(116, -14.4f, -126), new(151, -14.4f, -126), 2.8f, 1, OrbitalComplexVerticalLayer.ServiceDeck),
        Probe("undertow_pump_lane", new(-150, -14.4f, 42), new(-82, -14.4f, 42), 3.0f, 0, OrbitalComplexVerticalLayer.ServiceDeck)
    };

    private static OrbitalComplexRouteProbe Probe(
        string id, Vector3 from, Vector3 to, float clearance, int stage,
        OrbitalComplexVerticalLayer layer)
        => new(id, from, to, clearance, stage, layer);

    private static IReadOnlyList<OrbitalComplexCollisionBox> CollisionBoxes() => new[]
    {
        Box("service_floor_west", new(-100, -16.3f, -60), new(140, 1, 320), Vector3.Zero, "service_deck"),
        Box("service_floor_east", new(100, -16.3f, -60), new(140, 1, 320), Vector3.Zero, "service_deck"),
        Box("service_floor_south", new(0, -16.3f, 47), new(60, 1, 106), Vector3.Zero, "service_deck"),
        Box("service_floor_north", new(0, -16.3f, -136), new(60, 1, 168), Vector3.Zero, "service_deck"),
        Box("reactor_pit_floor", new(0, -33.4f, -34), new(54, 1.2f, 62), Vector3.Zero, "dry_dock"),
        Box("reactor_west_pedestal", new(-27, -11.5f, -34), new(7, 8, 40), Vector3.Zero, "equipment_bank"),
        Box("reactor_east_pedestal", new(27, -11.5f, -34), new(7, 8, 40), Vector3.Zero, "equipment_bank"),
        // The authored breaker/archive modules now receive per-assembly concave
        // collision in the world assembler.  Do not retain the old solid hall
        // proxies here: they sealed the objective terminals inside their rooms.
        // The two pressure bulkheads are split around a deliberate central
        // opening; the stage-driven gate shape owns that opening's lock state.
        Box("intake_bulkhead_west", new(-16.0f, -11.5f, 77), new(22, 8, 8), Vector3.Zero, "building_shell"),
        Box("intake_bulkhead_east", new(16.0f, -11.5f, 77), new(22, 8, 8), Vector3.Zero, "building_shell"),
        // Leave a 28 m service aperture so the side aisles can reach the gate
        // without clipping the capsule bay.  The stage-driven gate collision
        // spans that aperture and remains the authority for locking it.
        Box("tide_gate_bulkhead_west", new(-20.5f, -11.5f, -194), new(13, 8, 8), Vector3.Zero, "building_shell"),
        Box("tide_gate_bulkhead_east", new(20.5f, -11.5f, -194), new(13, 8, 8), Vector3.Zero, "building_shell"),
        Box("catwalk_west", new(-58, -2.6f, -34), new(56, 0.45f, 3.6f), Vector3.Zero, "catwalk_deck"),
        Box("catwalk_east", new(58, -2.6f, -34), new(56, 0.45f, 3.6f), Vector3.Zero, "catwalk_deck"),
        Box("catwalk_north", new(0, -2.6f, -88), new(116, 0.45f, 3.6f), Vector3.Zero, "catwalk_deck"),
        Box("catwalk_west_spine", new(-94, -2.6f, -116), new(3.6f, 0.45f, 56), Vector3.Zero, "catwalk_deck"),
        Box("catwalk_east_spine", new(94, -2.6f, -116), new(3.6f, 0.45f, 56), Vector3.Zero, "catwalk_deck"),
        // Multi-level detention galleries inside the open Stormglass atrium.
        // They are deliberately offset outside the reactor ring so the lower
        // yard remains a continuous combat loop.
        Box("detention_gallery_west_mid", new(-103, -4.45f, -31), new(6.2f, 0.45f, 146), Vector3.Zero, "detention_gallery"),
        Box("detention_gallery_east_mid", new(103, -4.45f, -31), new(6.2f, 0.45f, 146), Vector3.Zero, "detention_gallery"),
        Box("detention_gallery_west_upper", new(-103, 6.65f, -31), new(6.2f, 0.45f, 146), Vector3.Zero, "detention_gallery"),
        Box("detention_gallery_east_upper", new(103, 6.65f, -31), new(6.2f, 0.45f, 146), Vector3.Zero, "detention_gallery"),
        // The bunker shell is rendered by the authored GLB.  These hidden
        // gameplay volumes deliberately span the complete declared vertical
        // envelope so a player cannot climb above the roof or fall below the
        // reactor pit when the camera crosses a seam in the imported mesh.
        Box("north_boundary", new(0, -5.0f, -220), new(340, 60, 1), Vector3.Zero, "boundary"),
        Box("south_boundary", new(0, -5.0f, 100), new(340, 60, 1), Vector3.Zero, "boundary"),
        Box("west_boundary", new(-170, -5.0f, -60), new(1, 60, 320), Vector3.Zero, "boundary"),
        Box("east_boundary", new(170, -5.0f, -60), new(1, 60, 320), Vector3.Zero, "boundary"),
        // The bunker roof is intentionally split around three open courts:
        // south intake, the Stormglass atrium, and the north recovery apron.
        // Keeping these as separate volumes mirrors the authored DCC roof
        // panels and gives the map a real indoor/outdoor cadence.
        Box("bunker_ceiling_south_west", new(-132.5f, 24.5f, 85), new(75, 1, 32), Vector3.Zero, "ceiling"),
        Box("bunker_ceiling_south_east", new(132.5f, 24.5f, 85), new(75, 1, 32), Vector3.Zero, "ceiling"),
        Box("bunker_ceiling_transition", new(0, 24.5f, 59), new(340, 1, 18), Vector3.Zero, "ceiling"),
        Box("bunker_ceiling_atrium_west", new(-141, 24.5f, -31), new(58, 1, 162), Vector3.Zero, "ceiling"),
        Box("bunker_ceiling_atrium_east", new(141, 24.5f, -31), new(58, 1, 162), Vector3.Zero, "ceiling"),
        Box("bunker_ceiling_mid_north", new(0, 24.5f, -151), new(340, 1, 78), Vector3.Zero, "ceiling"),
        Box("bunker_ceiling_north_west", new(-145, 24.5f, -205), new(50, 1, 30), Vector3.Zero, "ceiling"),
        Box("bunker_ceiling_north_east", new(145, 24.5f, -205), new(50, 1, 30), Vector3.Zero, "ceiling"),
        Box("reactor_pit_west_rim", new(-29, -17.5f, -34), new(4, 3.4f, 66), Vector3.Zero, "pit_rim"),
        Box("reactor_pit_east_rim", new(29, -17.5f, -34), new(4, 3.4f, 66), Vector3.Zero, "pit_rim"),
        // Dry rim around the lower blackwater pool.  The shallow step is high
        // enough to stand on after rising, but leaves the central water volume
        // open for the authored swimming shortcut.
        Box("blackwater_rim_north", new(0, -28.25f, -62.0f), new(52, 0.6f, 4), Vector3.Zero, "blackwater_rim"),
        Box("blackwater_rim_south", new(0, -28.25f, -6.0f), new(52, 0.6f, 4), Vector3.Zero, "blackwater_rim"),
        Box("blackwater_rim_west", new(-27.0f, -28.25f, -34.0f), new(4, 0.6f, 52), Vector3.Zero, "blackwater_rim"),
        Box("blackwater_rim_east", new(27.0f, -28.25f, -34.0f), new(4, 0.6f, 52), Vector3.Zero, "blackwater_rim")
    };

    private static OrbitalComplexCollisionBox Box(
        string id, Vector3 position, Vector3 size, Vector3 rotation, string purpose)
        => new(id, position, size, rotation, purpose);

    private static IReadOnlyList<OrbitalComplexRampDefinition> Ramps() => new[]
    {
        new OrbitalComplexRampDefinition(
            "drydock_west_ramp", new(-34, -22.75f, -31), new(4.5f, 1.4f, 20.4f),
            new(-0.943f, 0, 0), new(-34, -31.0f, -25), new(-34, -14.5f, -37),
            OrbitalComplexVerticalLayer.ServiceDeck),
        new OrbitalComplexRampDefinition(
            "drydock_east_ramp", new(34, -22.75f, -37), new(4.5f, 1.4f, 20.4f),
            new(0.943f, 0, 0), new(34, -31.0f, -43), new(34, -14.5f, -31),
            OrbitalComplexVerticalLayer.ServiceDeck),
        new OrbitalComplexRampDefinition(
            "catwalk_west_ramp", new(-82, -8.5f, -34), new(17.0f, 1.4f, 4.5f),
            new(0, 0, 0.785f), new(-88, -14.5f, -34), new(-76, -2.5f, -34),
            OrbitalComplexVerticalLayer.Catwalk),
        new OrbitalComplexRampDefinition(
            "catwalk_east_ramp", new(82, -8.5f, -34), new(17.0f, 1.4f, 4.5f),
            new(0, 0, -0.785f), new(88, -14.5f, -34), new(76, -2.5f, -34),
            OrbitalComplexVerticalLayer.Catwalk)
    };

    private static IReadOnlyList<OrbitalComplexPowerGateDefinition> PowerGates() => new[]
    {
        new OrbitalComplexPowerGateDefinition(
            "upper_catwalk_bypass", "UpperBypassBarrier", new(-82, -2.8f, -34),
            new(1, 6, 4), Vector3.Zero, 1, true),
        new OrbitalComplexPowerGateDefinition(
            "north_tide_gate", "TideGateLeft", new(0, -15.3f, -194),
            new(28, 5, 1), Vector3.Zero, 1, false),
        new OrbitalComplexPowerGateDefinition(
            "stormglass_vault", "VaultDoorLeft", new(0, -15.0f, -52),
            new(10, 4.5f, 1), Vector3.Zero, 2, false)
    };
}
