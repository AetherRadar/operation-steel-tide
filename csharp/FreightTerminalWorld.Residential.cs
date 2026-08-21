using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private const float ResidentialFloorHeight = 3.15f;
    private const float ResidentialStairRun = 5.4f;
    private const float ResidentialStairOpeningWidth = 5.6f;
    private const float ResidentialStairOpeningNorthDepth = 5.45f;
    private const float ResidentialStairOpeningSouthDepth = 3.25f;
    private const int ResidentialStepsPerFlight = 16;
    private const float ResidentialStairTreadWidth = 1.95f;
    private const float ResidentialStairTreadThickness = 0.14f;
    private const float ResidentialStairLandingWidth = 4.96f;
    private const float ResidentialStairLandingDepth = 1.8f;
    private const int ResidentialStairLandingSlatCount = 8;

    private readonly record struct ResidentialTowerSpec(
        Vector3 Position,
        Vector2 Footprint,
        int Floors,
        string BlockName,
        Color Accent);

    private static readonly ResidentialTowerSpec[] ResidentialTowerSpecs =
    {
        new(new Vector3(-128, 0, -28), new Vector2(24, 46), 6, "HARBOR COURT A", new Color(0.34f, 0.58f, 0.62f)),
        new(new Vector3(132, 0, -72), new Vector2(27, 54), 9, "HARBOR COURT B", new Color(0.62f, 0.42f, 0.28f)),
        new(new Vector3(-78, 0, -190), new Vector2(42, 25), 6, "NORTH QUAY 1", new Color(0.45f, 0.55f, 0.34f)),
        new(new Vector3(2, 0, -194), new Vector2(54, 28), 9, "NORTH QUAY 2", new Color(0.35f, 0.48f, 0.68f)),
        new(new Vector3(92, 0, -191), new Vector2(34, 26), 6, "NORTH QUAY 3", new Color(0.64f, 0.46f, 0.34f)),
        new(new Vector3(-138, 0, -118), new Vector2(18, 24), 11, "WEST GATE TOWER", new Color(0.42f, 0.62f, 0.56f)),
        new(new Vector3(139, 0, -153), new Vector2(22, 26), 13, "EAST GATE TOWER", new Color(0.56f, 0.48f, 0.68f)),
        new(new Vector3(-82, 0, 75), new Vector2(32, 25), 7, "SOUTH COURT 1", new Color(0.5f, 0.61f, 0.42f)),
        new(new Vector3(-25, 0, 79), new Vector2(24, 22), 10, "SOUTH COURT 2", new Color(0.62f, 0.45f, 0.38f)),
        new(new Vector3(35, 0, 77), new Vector2(38, 26), 7, "SOUTH COURT 3", new Color(0.35f, 0.55f, 0.65f)),
        new(new Vector3(90, 0, 72), new Vector2(22, 20), 12, "SOUTH COURT 4", new Color(0.58f, 0.54f, 0.35f))
    };

    private readonly record struct ResidentialSkyLink(int From, int To, int[] Floors);

    private static readonly ResidentialSkyLink[] ResidentialSkyLinks =
    {
        new(7, 8, new[] { 2, 5 }),
        new(8, 9, new[] { 2, 5 }),
        new(9, 10, new[] { 2, 6 }),
        new(2, 3, new[] { 2, 4 }),
        new(3, 4, new[] { 2, 4 }),
        new(7, 0, new[] { 2, 4 }),
        new(0, 5, new[] { 2, 4 }),
        new(5, 2, new[] { 2, 5 }),
        new(4, 6, new[] { 2, 4 }),
        new(6, 1, new[] { 2, 6 }),
        new(1, 10, new[] { 2, 5 })
    };

    private static readonly string[] ResidentialEnglishLabelTokens =
    {
        "COMMUNITY", "CLINIC", "EVAC", "SHELTER", "MAINTENANCE", "FLAT", "SECURITY", "POST",
        "SEALED", "TENANT", "UNIT", "KITCHEN", "FAMILY", "APARTMENT", "FLOOR", "EXIT", "HARBOR",
        "COURT", "NORTH", "QUAY", "SOUTH", "WEST", "EAST", "GATE", "TOWER", "MEDICAL", "SUPPLY",
        "TOOLS", "CONCEALED", "RESERVE", "STASH", "RELAY", "HOLD", "UPLINK", "ONLINE", "ROOF",
        "CACHE", "LOCKED", "UNLOCKED", "EVACUEE", "VOLUNTEER", "GUARD", "UTILITY", "WORKER",
        "RESIDENT", "ASSISTED", "SHELTERING", "BODY", "LOOT"
    };

    private readonly record struct ResidentialSniperPost(Vector3 Position, Vector3 FacingTarget);
    private readonly record struct ResidentialSkybridgeSightline(int BridgeIndex, Vector3 From, Vector3 To);

    private static int ResidentialLinkSide(ResidentialTowerSpec from, ResidentialTowerSpec to)
    {
        var yaw = Mathf.Atan2(-from.Position.X, MapCenterZ - from.Position.Z);
        var world = to.Position - from.Position;
        var localX = world.X * Mathf.Cos(yaw) - world.Z * Mathf.Sin(yaw);
        var localZ = world.X * Mathf.Sin(yaw) + world.Z * Mathf.Cos(yaw);
        if (Mathf.Abs(localX) >= Mathf.Abs(localZ))
        {
            return localX > 0 ? 0 : 1;
        }
        return localZ > 0 ? 2 : 3;
    }

    private sealed class LinkSlot
    {
        public readonly HashSet<int> Floors = new();
        public float DoorZ;
    }

    private readonly Dictionary<int, Dictionary<int, LinkSlot>> _residentialLinkSlots = new();

    private static float ResidentialLinkDoorZ(ResidentialTowerSpec spec, ResidentialTowerSpec other, int side)
    {
        var yaw = Mathf.Atan2(-spec.Position.X, MapCenterZ - spec.Position.Z);
        var lx = new Vector2(Mathf.Cos(yaw), -Mathf.Sin(yaw));
        var lz = new Vector2(Mathf.Sin(yaw), Mathf.Cos(yaw));
        var world = other.Position - spec.Position;
        var d2 = new Vector2(world.X, world.Z).Normalized();
        var crossLX = lx.X * d2.Y - lx.Y * d2.X;
        var crossLZ = lz.X * d2.Y - lz.Y * d2.X;
        var z = side switch
        {
            0 => -(spec.Footprint.X * 0.5f) * crossLX / crossLZ,
            1 => (spec.Footprint.X * 0.5f) * crossLX / crossLZ,
            _ => spec.Footprint.Y * 0.16f
        };
        var coreZ = -Mathf.Min(spec.Footprint.Y * 0.18f, 3.6f);
        var southStart = coreZ + ResidentialStairOpeningSouthDepth + 0.55f;
        var southEnd = spec.Footprint.Y * 0.5f - 0.35f;
        // Keep the doorway in the furniture-free band of the apartment strip.
        var furnMin = spec.Footprint.Y * 0.15f + 0.6f;
        var furnMax = spec.Footprint.Y * 0.24f - 0.6f;
        return Mathf.Clamp(z, Mathf.Max(southStart + 1.2f, furnMin), Mathf.Min(southEnd - 1.2f, furnMax));
    }

    private readonly List<Node3D> _residentialTowers = new();
    private readonly List<CivilianNpc> _civilians = new();
    private readonly List<Vector3> _residentialEntrances = new();
    private readonly List<Vector3> _residentialRooftops = new();
    private readonly List<ResidentialSniperPost> _residentialSniperPosts = new();
    private readonly List<ResidentialSkybridgeSightline> _residentialSkybridgeSightlines = new();
    private readonly List<ResidentialSupplyCache> _residentialCaches = new();
    private readonly List<ResidentialSearchableFurniture> _residentialFurniture = new();
    private readonly List<BreakableGlassField> _residentialGlassFields = new();
    private readonly HashSet<ResidentialRoomArchetype> _residentialRoomArchetypes = new();
    private readonly List<Action<string>> _residentialLanguageRefreshers = new();
    private readonly int[] _residentialCacheCountByTower = new int[ResidentialTowerSpecs.Length];
    private ResidentialRoomEncounterController? _residentialEncounterController;
    private uint _residentialLootSeedSalt;
    private int _residentialFloorCount;
    private int _residentialStairFlightCount;
    private int _residentialRoofAccessCount;
    private int _residentialSkybridgeCount;
    private int _residentialSkybridgeWindowCount;
    private int _residentialSkybridgeFrameCount;
    private int _residentialSkybridgeMarksmanCount;
    private int _residentialInfillModuleCount;
    private int _residentialStairDetailCount;
    private int _residentialFurnitureEventCount;
    private int _residentialChestEventCount;
    private int _residentialGuardAmbushSpawnCount;

    public int ResidentialTowerCount => _residentialTowers.Count;
    public int ResidentialCivilianCount => _civilians.Count;
    public int ResidentialSpecialCivilianCount => _civilians.FindAll(civilian => civilian.IsSpecial).Count;
    public int ResidentialCacheCount => _residentialCaches.Count;
    public int ResidentialFurnitureCount => _residentialFurniture.Count;
    public int ResidentialFurnitureEventCount => _residentialFurnitureEventCount;
    public int ResidentialInfillModuleCount => _residentialInfillModuleCount;
    public int ResidentialStairDetailCount => _residentialStairDetailCount;
    public int ResidentialGlassPaneCount => _residentialGlassFields.Sum(field => field.PaneCount);
    public int ResidentialBrokenGlassCount => _residentialGlassFields.Sum(field => field.ShatteredCount);

    private Label3D RegisterResidentialLocalizedLabel(Label3D label, Func<string, string> textProvider)
    {
        label.AddToGroup("residential_localized_labels");
        RegisterResidentialLanguageRefresher(language =>
        {
            if (IsInstanceValid(label))
            {
                label.Text = textProvider(language);
            }
        });
        return label;
    }

    private void RegisterResidentialLanguageRefresher(Action<string> refresher)
    {
        _residentialLanguageRefreshers.Add(refresher);
        refresher(_languageSetting);
    }

    private void RefreshResidentialLocalization()
    {
        foreach (var refresher in _residentialLanguageRefreshers)
        {
            refresher(_languageSetting);
        }
    }

    private void BuildResidentialCommunity(
        Godot.Material concrete,
        Godot.Material steel,
        Godot.Material glass,
        Godot.Material trim)
    {
        var community = new Node3D { Name = "ResidentialCommunity" };
        _levelRoot.AddChild(community);
        BuildResidentialRoads(community);
        _residentialLinkSlots.Clear();
        _residentialSniperPosts.Clear();
        _residentialSkybridgeSightlines.Clear();
        _residentialCaches.Clear();
        _residentialFurniture.Clear();
        _residentialRelayStations.Clear();
        _relayCaches.Clear();
        _relayLootMarkers.Clear();
        _residentialGlassFields.Clear();
        _residentialRoomArchetypes.Clear();
        _residentialTowerArtResults.Clear();
        _residentialTowerArtBuilder = new ResidentialTowerArtBuilder();
        _residentialFurnitureEventCount = 0;
        _residentialChestEventCount = 0;
        _residentialGuardAmbushSpawnCount = 0;
        _residentialEncounterController = CreateResidentialRoomEncounterController();
        var diagnosticArgs = OS.GetCmdlineUserArgs();
        _residentialLootSeedSalt = diagnosticArgs.Contains("--validate-residential-gameplay")
            || diagnosticArgs.Contains("--validate-medical")
            || diagnosticArgs.Contains("--validate-loot-variety")
            ? 0x5e71c4a3u
            : _rng.Randi();
        Array.Clear(_residentialCacheCountByTower, 0, _residentialCacheCountByTower.Length);
        _residentialSkybridgeCount = 0;
        _residentialSkybridgeWindowCount = 0;
        _residentialSkybridgeFrameCount = 0;
        _residentialSkybridgeMarksmanCount = 0;
        _residentialInfillModuleCount = 0;
        _residentialStairDetailCount = 0;
        _relayActivationCount = 0;
        _relayActivationInterruptedCount = 0;
        _relayLastEnemyRevealCount = 0;
        _relayLastLootRevealCount = 0;
        foreach (var link in ResidentialSkyLinks)
        {
            var sideFrom = ResidentialLinkSide(ResidentialTowerSpecs[link.From], ResidentialTowerSpecs[link.To]);
            var sideTo = ResidentialLinkSide(ResidentialTowerSpecs[link.To], ResidentialTowerSpecs[link.From]);
            var zFrom = ResidentialLinkDoorZ(ResidentialTowerSpecs[link.From], ResidentialTowerSpecs[link.To], sideFrom);
            var zTo = ResidentialLinkDoorZ(ResidentialTowerSpecs[link.To], ResidentialTowerSpecs[link.From], sideTo);
            if (!_residentialLinkSlots.TryGetValue(link.From, out var fromDict))
            {
                _residentialLinkSlots[link.From] = fromDict = new Dictionary<int, LinkSlot>();
            }
            if (!_residentialLinkSlots.TryGetValue(link.To, out var toDict))
            {
                _residentialLinkSlots[link.To] = toDict = new Dictionary<int, LinkSlot>();
            }
            if (!fromDict.TryGetValue(sideFrom, out var fromSlot))
            {
                fromDict[sideFrom] = fromSlot = new LinkSlot { DoorZ = zFrom };
            }
            if (!toDict.TryGetValue(sideTo, out var toSlot))
            {
                toDict[sideTo] = toSlot = new LinkSlot { DoorZ = zTo };
            }
            foreach (var floor in link.Floors)
            {
                fromSlot.Floors.Add(floor);
                toSlot.Floors.Add(floor);
            }
        }
        PlanResidentialSkybridgeAccesses();
        for (var index = 0; index < ResidentialTowerSpecs.Length; index++)
        {
            BuildResidentialTower(community, ResidentialTowerSpecs[index], index, concrete, steel, glass, trim);
        }
        BuildResidentialGapInfill(community, concrete, steel, glass, trim);
        BuildResidentialSkyLinks(community, concrete, steel, glass);
        BuildResidentialSkybridgeAccessStairs();
    }

    private void BuildResidentialRoads(Node3D community)
    {
        var road = GroundMaterial("residential_road", new Color(0.34f, 0.37f, 0.38f), 0.92f);
        var sidewalk = Mat("residential_sidewalk", new Color(0.51f, 0.53f, 0.49f), 0.02f, 0.9f);
        var marking = Mat("residential_road_marking", new Color(0.82f, 0.76f, 0.55f), 0.02f, 0.66f);
        foreach (var segment in new (string Name, Vector3 Position, Vector3 Size)[]
        {
            ("NorthResidentialBoulevard", new Vector3(0, 0.015f, -193), new Vector3(318, 0.12f, 14)),
            ("SouthResidentialBoulevard", new Vector3(0, 0.015f, 76), new Vector3(244, 0.12f, 14)),
            ("WestResidentialBoulevard", new Vector3(-136, 0.016f, -61), new Vector3(14, 0.12f, 270)),
            ("EastResidentialBoulevard", new Vector3(138, 0.016f, -73), new Vector3(14, 0.12f, 294))
        })
        {
            ExpansionBox(community, segment.Name, segment.Position, segment.Size, road);
        }
        foreach (var segment in new (Vector3 Position, Vector3 Size)[]
        {
            (new Vector3(0, 0.085f, -184.8f), new Vector3(318, 0.08f, 2.0f)),
            (new Vector3(0, 0.085f, -201.2f), new Vector3(318, 0.08f, 2.0f)),
            (new Vector3(0, 0.085f, 67.8f), new Vector3(244, 0.08f, 2.0f)),
            (new Vector3(0, 0.085f, 84.2f), new Vector3(244, 0.08f, 2.0f)),
            (new Vector3(-144.2f, 0.086f, -61), new Vector3(2.0f, 0.08f, 270)),
            (new Vector3(-127.8f, 0.086f, -61), new Vector3(2.0f, 0.08f, 270)),
            (new Vector3(129.8f, 0.086f, -73), new Vector3(2.0f, 0.08f, 294)),
            (new Vector3(146.2f, 0.086f, -73), new Vector3(2.0f, 0.08f, 294))
        })
        {
            MeshBox(community, segment.Position, segment.Size, sidewalk);
        }
        for (var x = -150.0f; x <= 150.0f; x += 9.0f)
        {
            MeshBox(community, new Vector3(x, 0.105f, -193), new Vector3(4.4f, 0.025f, 0.16f), marking);
        }
        for (var x = -112.0f; x <= 112.0f; x += 9.0f)
        {
            MeshBox(community, new Vector3(x, 0.105f, 76), new Vector3(4.4f, 0.025f, 0.16f), marking);
        }

        // Parked civilian / utility vehicles along the residential ring.
        SpawnDriveableVehicle(new Vector3(-118, 0, -193), "COURT VAN", new Color(0.42f, 0.28f, 0.18f), yaw: 0.08f, maxHealth: 150.0f);
        SpawnDriveableVehicle(new Vector3(48, 0, -193), "QUAY PICKUP", new Color(0.22f, 0.34f, 0.48f), yaw: -0.04f, maxHealth: 165.0f);
        SpawnDriveableVehicle(new Vector3(138, 0, -40), "GATE TRUCK", new Color(0.5f, 0.42f, 0.2f), yaw: Mathf.Pi * 0.5f, maxHealth: 190.0f);
        SpawnDriveableVehicle(new Vector3(-136, 0, 20), "SOUTH UTILITY", new Color(0.3f, 0.4f, 0.28f), yaw: -Mathf.Pi * 0.5f, maxHealth: 175.0f);
        SpawnDriveableVehicle(new Vector3(12, 0, 76), "COURT SEDAN", new Color(0.18f, 0.2f, 0.24f), yaw: Mathf.Pi, maxHealth: 140.0f);

        // Street life so the ring reads inhabited: lamps, market stalls, bins, cable trays, gate arches.
        var lampPost = Mat("residential_lamp_post", new Color(0.16f, 0.17f, 0.18f), 0.4f, 0.6f);
        var lampHead = Mat("residential_lamp_head", new Color(0.98f, 0.85f, 0.55f), 0.1f, 0.4f, new Color(1.0f, 0.75f, 0.35f));
        var stallWood = Mat("residential_stall", new Color(0.55f, 0.35f, 0.2f), 0.05f, 0.85f);
        var stallCanopy = Mat("residential_stall_canopy", new Color(0.25f, 0.45f, 0.5f), 0.05f, 0.8f);
        var binMat = Mat("residential_bin", new Color(0.2f, 0.35f, 0.22f), 0.1f, 0.7f);
        foreach (var x in new[] { -140f, -100f, -60f, -20f, 20f, 60f, 100f })
        {
            foreach (var z in new[] { -201.2f, -184.8f })
            {
                MeshBox(community, new Vector3(x, 1.6f, z), new Vector3(0.12f, 3.2f, 0.12f), lampPost);
                MeshBox(community, new Vector3(x, 3.25f, z), new Vector3(0.5f, 0.12f, 0.24f), lampHead);
            }
        }
        foreach (var x in new[] { -110f, -70f, -30f, 10f, 50f, 90f })
        {
            foreach (var z in new[] { 67.8f, 84.2f })
            {
                MeshBox(community, new Vector3(x, 1.6f, z), new Vector3(0.12f, 3.2f, 0.12f), lampPost);
                MeshBox(community, new Vector3(x, 3.25f, z), new Vector3(0.5f, 0.12f, 0.24f), lampHead);
            }
        }
        foreach (var stall in new (float X, float Z)[]
        {
            (-98, -181.5f), (-52, -181.5f), (-6, -181.5f), (44, -181.5f),
            (-66, 70.5f), (-12, 70.5f), (28, 70.5f), (74, 70.5f)
        })
        {
            ExpansionBox(community, "MarketStall", new Vector3(stall.X, 0.5f, stall.Z), new Vector3(2.4f, 0.8f, 1.1f), stallWood);
            MeshBox(community, new Vector3(stall.X, 1.9f, stall.Z), new Vector3(2.7f, 0.08f, 1.5f), stallCanopy);
            MeshBox(community, new Vector3(stall.X - 1.1f, 0.95f, stall.Z - 0.4f), new Vector3(0.08f, 1.9f, 0.08f), stallWood);
            MeshBox(community, new Vector3(stall.X + 1.1f, 0.95f, stall.Z - 0.4f), new Vector3(0.08f, 1.9f, 0.08f), stallWood);
            ExpansionBox(community, "MarketCrate", new Vector3(stall.X + 1.8f, 0.35f, stall.Z), new Vector3(0.6f, 0.5f, 0.6f), binMat);
        }
        foreach (var x in new[] { -80f, -30f, 20f, 70f })
        {
            MeshBox(community, new Vector3(x, 5.4f, -193), new Vector3(0.18f, 0.5f, 22f), lampPost);
            MeshBox(community, new Vector3(x, 2.7f, -202.5f), new Vector3(0.18f, 5.4f, 0.18f), lampPost);
            MeshBox(community, new Vector3(x, 2.7f, -183.5f), new Vector3(0.18f, 5.4f, 0.18f), lampPost);
            MeshBox(community, new Vector3(x, 5.4f, 76), new Vector3(0.18f, 0.5f, 22f), lampPost);
            MeshBox(community, new Vector3(x, 2.7f, 66.5f), new Vector3(0.18f, 5.4f, 0.18f), lampPost);
            MeshBox(community, new Vector3(x, 2.7f, 85.5f), new Vector3(0.18f, 5.4f, 0.18f), lampPost);
        }
        foreach (var pos in new Vector3[]
        {
            new(-52, 0.45f, 60), new(8, 0.45f, 62), new(62, 0.45f, 58), new(-40, 0.45f, -175), new(40, 0.45f, -177)
        })
        {
            ExpansionBox(community, "AlleyBin", pos, new Vector3(1.6f, 0.9f, 0.9f), binMat);
        }
        foreach (var arch in new (float X, float Z, string Name)[] { (-45, 76, "SOUTH COURT"), (2, -193, "NORTH QUAY") })
        {
            MeshBox(community, new Vector3(arch.X, 2.6f, arch.Z - 8.6f), new Vector3(0.5f, 5.2f, 0.5f), lampPost);
            MeshBox(community, new Vector3(arch.X, 2.6f, arch.Z + 8.6f), new Vector3(0.5f, 5.2f, 0.5f), lampPost);
            MeshBox(community, new Vector3(arch.X, 5.3f, arch.Z), new Vector3(0.7f, 0.9f, 18.2f), lampPost);
            var districtName = arch.Name;
            community.AddChild(RegisterResidentialLocalizedLabel(new Label3D
            {
                Name = $"ResidentialDistrictSign_{districtName.Replace(" ", string.Empty)}",
                Position = new Vector3(arch.X, 5.3f, arch.Z),
                FontSize = 26,
                OutlineSize = 7,
                Modulate = new Color(0.9f, 0.7f, 0.4f),
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                VisibilityRangeEnd = 70.0f
            }, language => ResidentialBlockName(districtName, language)));
        }
    }

    private void BuildResidentialTower(
        Node3D community,
        ResidentialTowerSpec spec,
        int index,
        Godot.Material concrete,
        Godot.Material steel,
        Godot.Material glass,
        Godot.Material trim)
    {
        var towardCenter = new Vector3(-spec.Position.X, 0, MapCenterZ - spec.Position.Z).Normalized();
        var yaw = Mathf.Atan2(towardCenter.X, towardCenter.Z);
        var tower = new Node3D
        {
            Name = $"ResidentialTower_{index + 1:00}",
            Position = spec.Position,
            Rotation = new Vector3(0, yaw, 0)
        };
        var diversityProfile = ResidentialTowerDiversityPlan.ForTower(index);
        tower.SetMeta("residential_profile_signature", diversityProfile.Signature);
        tower.SetMeta("residential_facade_style", diversityProfile.Facade.ToString());
        tower.SetMeta("residential_roof_style", diversityProfile.Roof.ToString());
        tower.SetMeta("residential_tower_use", diversityProfile.Use.ToString());
        community.AddChild(tower);
        _residentialTowers.Add(tower);

        var facade = ExpansionPbrMaterial(
            $"residential_facade_pbr_{index % 5}",
            "concrete_floor",
            new Color(
                Mathf.Lerp(0.56f, spec.Accent.R, 0.18f),
                Mathf.Lerp(0.58f, spec.Accent.G, 0.18f),
                Mathf.Lerp(0.57f, spec.Accent.B, 0.18f)),
            0.03f,
            0.88f,
            0.3f);
        var interiorWall = Mat("residential_interior_wall", new Color(0.63f, 0.65f, 0.6f), 0.01f, 0.92f);
        var interiorFloor = Mat("residential_interior_floor", new Color(0.31f, 0.29f, 0.24f), 0.02f, 0.78f);
        var stair = Mat("residential_stair", new Color(0.39f, 0.42f, 0.4f), 0.12f, 0.76f);
        var warmLight = Mat("residential_warm_light", new Color(0.95f, 0.7f, 0.38f), 0.02f, 0.35f, new Color(0.95f, 0.55f, 0.22f));
        var wood = Mat("residential_wood", new Color(0.31f, 0.18f, 0.1f), 0.0f, 0.82f);
        var bedding = Mat("residential_bedding", new Color(0.22f, 0.38f, 0.45f), 0.0f, 0.9f);
        var windowGlass = Mat("residential_breakable_glass", new Color(0.3f, 0.62f, 0.68f, 0.32f), 0.62f, 0.08f);
        windowGlass.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        windowGlass.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        windowGlass.VertexColorUseAsAlbedo = true;
        var windowFrame = Mat("residential_window_frame", new Color(0.13f, 0.17f, 0.17f), 0.68f, 0.3f);
        var windowRecess = Mat("residential_window_recess", new Color(0.018f, 0.027f, 0.029f), 0.05f, 0.96f);
        var glassField = new BreakableGlassField { Name = $"ResidentialGlass_T{index + 1:00}" };
        tower.AddChild(glassField);
        glassField.Configure(windowGlass, windowFrame, windowRecess, 112.0f);
        _residentialGlassFields.Add(glassField);
        var stairCoreZ = -Mathf.Min(spec.Footprint.Y * 0.18f, 3.6f);
        var linkSlots = _residentialLinkSlots.TryGetValue(index, out var slotDict) ? slotDict : null;

        BuildTowerCourtyard(tower, spec, facade, steel, warmLight);
        for (var floor = 0; floor < spec.Floors; floor++)
        {
            var floorY = floor * ResidentialFloorHeight;
            BuildTowerFloorSlab(tower, spec, floor, floorY, stairCoreZ, interiorFloor, floor == 0);
            var westSlot = linkSlots is not null && linkSlots.TryGetValue(1, out var westLink) && westLink.Floors.Contains(floor) ? westLink : null;
            var eastSlot = linkSlots is not null && linkSlots.TryGetValue(0, out var eastLink) && eastLink.Floors.Contains(floor) ? eastLink : null;
            BuildTowerFloorShell(tower, spec, index, floor, floorY, facade, glassField, spec.Accent, westSlot, eastSlot);
            BuildTowerInterior(tower, spec, index, floor, floorY, stairCoreZ, interiorWall, wood, bedding, warmLight, westSlot, eastSlot);
            BuildTowerStairs(tower, floor, floorY, stairCoreZ, stair, warmLight);
            BuildTowerStairDetails(tower, spec, index, floor, floorY, stairCoreZ, trim, warmLight);
            _residentialFloorCount++;
        }
        glassField.Commit();
        BuildTowerFacadeDetails(tower, spec, index, spec.Accent);
        BuildTowerRoof(tower, spec, stairCoreZ, facade, trim, warmLight);
        BuildResidentialAuthoredDressing(tower, spec, index);
        _residentialRoofAccessCount++;

        var basis = Basis.FromEuler(new Vector3(0, yaw, 0));
        var towerTransform = new Transform3D(basis, spec.Position);
        _residentialEntrances.Add(towerTransform * new Vector3(0, 0.18f, spec.Footprint.Y * 0.5f + 1.4f));
        _residentialRooftops.Add(towerTransform * new Vector3(0, spec.Floors * ResidentialFloorHeight + 0.2f, 0));
        SpawnResidentialOccupants(towerTransform, spec, index);
    }

    private void BuildTowerFloorSlab(
        Node3D tower,
        ResidentialTowerSpec spec,
        int floor,
        float floorY,
        float coreZ,
        Godot.Material material,
        bool groundFloor)
    {
        var width = spec.Footprint.X;
        var depth = spec.Footprint.Y;
        var openingWidth = Mathf.Min(ResidentialStairOpeningWidth, width - 5.0f);
        var sideWidth = (width - openingWidth) * 0.5f;
        var northEdge = -depth * 0.5f;
        var southEdge = depth * 0.5f;
        var openingNorth = coreZ - ResidentialStairOpeningNorthDepth;
        var openingSouth = coreZ + ResidentialStairOpeningSouthDepth;
        // The asymmetric four-panel opening preserves standing headroom over both flights
        // and the deeper north turn platform without opening the rest of the floor.
        var northDepth = Mathf.Max(0.5f, openingNorth - northEdge);
        var southDepth = Mathf.Max(0.5f, southEdge - openingSouth);
        foreach (var panel in new (string Side, Vector3 Position, Vector3 Size)[]
        {
            ("W", new Vector3(-(openingWidth + sideWidth) * 0.5f, floorY + 0.05f, 0), new Vector3(sideWidth, 0.12f, depth)),
            ("E", new Vector3((openingWidth + sideWidth) * 0.5f, floorY + 0.05f, 0), new Vector3(sideWidth, 0.12f, depth)),
            ("N", new Vector3(0, floorY + 0.05f, northEdge + northDepth * 0.5f), new Vector3(openingWidth, 0.12f, northDepth)),
            ("S", new Vector3(0, floorY + 0.05f, openingSouth + southDepth * 0.5f), new Vector3(openingWidth, 0.12f, southDepth))
        })
        {
            var slab = ExpansionBox(tower, $"ResidentialFloorSlab_F{floor:00}_{panel.Side}", panel.Position, panel.Size, material);
            slab.AddToGroup("residential_floor_slabs");
        }
        // Do not fill the stair channel: the opening follows the full switchback shaft.
        if (groundFloor)
        {
            // Keep lobby mats only outside the stair well so stepped stairs stay clear.
            var lobbyPadDepth = Mathf.Max(0.8f, southDepth - 0.35f);
            ExpansionBox(
                tower,
                "ResidentialLobbyFloor",
                new Vector3(0, floorY + 0.015f, openingSouth + lobbyPadDepth * 0.5f + 0.2f),
                new Vector3(openingWidth * 0.92f, 0.03f, lobbyPadDepth),
                material);
        }
    }

    private void BuildTowerFloorShell(
        Node3D tower,
        ResidentialTowerSpec spec,
        int towerIndex,
        int floor,
        float floorY,
        Godot.Material facade,
        BreakableGlassField glassField,
        Color accent,
        LinkSlot? linkWest,
        LinkSlot? linkEast)
    {
        var width = spec.Footprint.X;
        var depth = spec.Footprint.Y;
        const float wallThickness = 0.2f;
        const float wallHeight = 3.0f;
        var wallCenterY = floorY + 0.1f + wallHeight * 0.5f;
        ExpansionBox(tower, "ResidentialNorthWall", new Vector3(0, wallCenterY, -depth * 0.5f), new Vector3(width, wallHeight, wallThickness), facade);
        BuildSideWall(tower, -width * 0.5f, wallCenterY, depth, floorY, wallHeight, wallThickness, facade, linkWest?.DoorZ, "ResidentialWestWall");
        BuildSideWall(tower, width * 0.5f, wallCenterY, depth, floorY, wallHeight, wallThickness, facade, linkEast?.DoorZ, "ResidentialEastWall");
        if (floor == 0)
        {
            // Tall clear opening so a standing capsule (1.75m + headroom) never forces crouch.
            const float doorWidth = 3.8f;
            const float doorHeight = 2.85f;
            var sideWidth = (width - doorWidth) * 0.5f;
            ExpansionBox(tower, "ResidentialSouthWall", new Vector3(-(doorWidth + sideWidth) * 0.5f, wallCenterY, depth * 0.5f), new Vector3(sideWidth, wallHeight, wallThickness), facade);
            ExpansionBox(tower, "ResidentialSouthWall", new Vector3((doorWidth + sideWidth) * 0.5f, wallCenterY, depth * 0.5f), new Vector3(sideWidth, wallHeight, wallThickness), facade);
            var headerHeight = Mathf.Max(0.12f, wallHeight - doorHeight);
            ExpansionBox(tower, "ResidentialEntryHeader", new Vector3(0, floorY + 0.1f + doorHeight + headerHeight * 0.5f, depth * 0.5f), new Vector3(doorWidth, headerHeight, wallThickness), facade);
            // Keep the threshold flush with the lobby floor so the doorway is not a ledge.
            ExpansionBox(tower, "ResidentialEntryThreshold", new Vector3(0, floorY + 0.04f, depth * 0.5f + 0.12f), new Vector3(doorWidth - 0.2f, 0.08f, 0.55f), facade);
        }
        else
        {
            ExpansionBox(tower, "ResidentialSouthWall", new Vector3(0, wallCenterY, depth * 0.5f), new Vector3(width, wallHeight, wallThickness), facade);
        }

        BuildResidentialFacadePattern(tower, spec, towerIndex, floor, floorY, glassField, accent);
    }

    private void BuildSideWall(
        Node3D tower,
        float x,
        float wallCenterY,
        float depth,
        float floorY,
        float wallHeight,
        float wallThickness,
        Godot.Material facade,
        float? openZ,
        string name)
    {
        if (openZ is null)
        {
            ExpansionBox(tower, name, new Vector3(x, wallCenterY, 0), new Vector3(wallThickness, wallHeight, depth), facade);
            return;
        }
        const float doorWidth = 3.6f;
        const float doorHeight = 2.6f;
        var doorCenter = openZ.Value;
        var northLen = doorCenter - doorWidth * 0.5f + depth * 0.5f;
        var southLen = depth * 0.5f - (doorCenter + doorWidth * 0.5f);
        ExpansionBox(tower, name + "_N", new Vector3(x, wallCenterY, -depth * 0.5f + northLen * 0.5f), new Vector3(wallThickness, wallHeight, northLen), facade);
        ExpansionBox(tower, name + "_S", new Vector3(x, wallCenterY, doorCenter + doorWidth * 0.5f + southLen * 0.5f), new Vector3(wallThickness, wallHeight, southLen), facade);
        var headerHeight = Mathf.Max(0.12f, wallHeight - doorHeight);
        ExpansionBox(tower, name + "_H", new Vector3(x, floorY + 0.1f + doorHeight + headerHeight * 0.5f, doorCenter), new Vector3(wallThickness, headerHeight, doorWidth), facade);
    }

    private void BuildTowerInterior(
        Node3D tower,
        ResidentialTowerSpec spec,
        int towerIndex,
        int floor,
        float floorY,
        float coreZ,
        Godot.Material wall,
        Godot.Material wood,
        Godot.Material bedding,
        Godot.Material light,
        LinkSlot? linkWest,
        LinkSlot? linkEast)
    {
        var width = spec.Footprint.X;
        var depth = spec.Footprint.Y;
        const float corridorHalfWidth = 2.9f;
        var northStart = -depth * 0.5f + 0.35f;
        var northEnd = coreZ - ResidentialStairOpeningNorthDepth - 0.55f;
        var southStart = coreZ + ResidentialStairOpeningSouthDepth + 0.55f;
        var southEnd = depth * 0.5f - 0.35f;
        foreach (var x in new[] { -corridorHalfWidth, corridorHalfWidth })
        {
            var linkSlot = x < 0 ? linkWest : linkEast;
            var southDoor = linkSlot is not null ? linkSlot.DoorZ : Mathf.Lerp(southStart, southEnd, 0.58f);
            BuildApartmentWallWithDoor(tower, x, floorY, northStart, northEnd, Mathf.Lerp(northStart, northEnd, 0.45f), wall);
            BuildApartmentWallWithDoor(tower, x, floorY, southStart, southEnd, southDoor, wall);
        }
        var roomWidth = Mathf.Max(2.6f, (width - corridorHalfWidth * 2.0f) * 0.5f);
        var carpet = Mat("residential_carpet", new Color(0.28f, 0.18f, 0.14f), 0.0f, 0.94f);
        var appliance = Mat("residential_appliance", new Color(0.55f, 0.58f, 0.56f), 0.62f, 0.35f);
        var screen = Mat("residential_screen", new Color(0.035f, 0.045f, 0.048f), 0.08f, 0.52f);
        var table = Mat("residential_table", new Color(0.24f, 0.16f, 0.1f), 0.05f, 0.78f);
        var featuredFloor = IsResidentialFeaturedFloor(spec, floor);
        var (layoutRoot, floorLayout) = CreateResidentialFloorLayoutRoot(tower, towerIndex, floor, spec.Floors);
        foreach (var side in new[] { -1.0f, 1.0f })
        {
            var roomX = side * (corridorHalfWidth + roomWidth * 0.5f);
            var archetype = featuredFloor
                ? ResidentialRoomArchetypeFor(towerIndex, floor, side)
                : ResidentialRoomArchetype.FamilyApartment;
            _residentialRoomArchetypes.Add(archetype);
            var furnitureKind = ResidentialFurnitureKindFor(towerIndex, floor, side, archetype, featuredFloor);
            BuildResidentialLayoutPartitions(
                layoutRoot,
                towerIndex,
                floor,
                side,
                roomX,
                roomWidth,
                depth,
                floorY,
                floorLayout,
                wall);
            var anchors = BuildResidentialLayoutFurnishings(
                layoutRoot,
                towerIndex,
                floor,
                side,
                roomX,
                roomWidth,
                depth,
                floorY,
                floorLayout,
                furnitureKind,
                wood,
                bedding,
                carpet,
                appliance,
                screen,
                table);
            SpawnResidentialFurniture(
                tower,
                towerIndex,
                floor,
                side,
                archetype,
                featuredFloor,
                furnitureKind,
                roomX,
                roomWidth,
                depth,
                floorY,
                anchors.BedZ,
                anchors.KitchenX);
            var cacheX = roomX + side * roomWidth * 0.38f;
            SpawnResidentialCache(
                tower,
                towerIndex,
                floor,
                side,
                ResidentialRoomZone.North,
                archetype,
                new Vector3(cacheX, floorY + 0.1f, -depth * 0.35f));
            SpawnResidentialCache(
                tower,
                towerIndex,
                floor,
                side,
                ResidentialRoomZone.South,
                archetype,
                new Vector3(cacheX, floorY + 0.1f, depth * 0.35f));
            if (featuredFloor)
            {
                BuildResidentialRoomTheme(layoutRoot, archetype, roomX, side, roomWidth, depth, floorY);
            }
            tower.AddChild(RegisterResidentialLocalizedLabel(new Label3D
            {
                Name = $"ApartmentPurposeSign_T{towerIndex + 1:00}_F{floor + 1:00}_{(side < 0 ? "W" : "E")}",
                Position = new Vector3(side * (corridorHalfWidth + 0.08f), floorY + 1.55f, depth * 0.28f),
                FontSize = 16,
                OutlineSize = 5,
                Modulate = ResidentialRoomColor(archetype),
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                VisibilityRangeEnd = 12.0f,
                VisibilityRangeEndMargin = 2.0f
            }, language => ResidentialRoomName(archetype, language)));
            // The single floor light below serves both rooms; per-room lights multiply too quickly across 96 floors.
        }
        // Keep the corridor runner on the occupied floor bands. One long strip crossed the
        // switchback opening and appeared as a red plate above every stair flight.
        foreach (var segment in new (string Side, float Start, float End)[]
        {
            ("N", northStart, northEnd),
            ("S", southStart, southEnd)
        })
        {
            var segmentDepth = segment.End - segment.Start;
            if (segmentDepth <= 0.1f)
            {
                continue;
            }
            var runner = MeshBox(
                tower,
                new Vector3(0, floorY + 0.08f, segment.Start + segmentDepth * 0.5f),
                new Vector3(2.2f, 0.03f, segmentDepth),
                carpet);
            runner.Name = $"ResidentialCorridorRunner_F{floor:00}_{segment.Side}";
            runner.AddToGroup("residential_corridor_runners");
        }
        // Corridor ceiling strip lights (mesh only).
        // Lived-in corridor dressing so each floor reads as a home, not a shell.
        var vending = Mat("residential_vending", new Color(0.72f, 0.18f, 0.16f), 0.3f, 0.4f, new Color(0.9f, 0.3f, 0.2f));
        var plantPot = Mat("residential_corridor_pot", new Color(0.24f, 0.27f, 0.23f), 0.05f, 0.86f);
        var foliage = Mat("residential_corridor_foliage", new Color(0.13f, 0.31f, 0.18f), 0.0f, 0.92f);
        ExpansionBox(tower, "CorridorVending", new Vector3(corridorHalfWidth - 0.45f, floorY + 0.85f, southStart + 1.2f), new Vector3(0.7f, 1.5f, 0.8f), vending);
        ExpansionBox(tower, "CorridorNoticeBoard", new Vector3(-corridorHalfWidth + 0.08f, floorY + 1.5f, southStart + 2.2f), new Vector3(0.06f, 1.1f, 1.6f), wood);
        ExpansionBox(tower, "CorridorPlantPot", new Vector3(-corridorHalfWidth + 0.55f, floorY + 0.3f, southStart + 0.7f), new Vector3(0.5f, 0.4f, 0.5f), plantPot);
        MeshBox(tower, new Vector3(-corridorHalfWidth + 0.55f, floorY + 0.85f, southStart + 0.7f), new Vector3(0.55f, 0.7f, 0.55f), foliage);
        ExpansionBox(tower, "CorridorCrateStack", new Vector3(corridorHalfWidth - 0.55f, floorY + 0.35f, northEnd - 0.8f), new Vector3(0.8f, 0.5f, 0.8f), table);
        MeshBox(tower, new Vector3(0, floorY + 2.88f, depth * 0.27f), new Vector3(2.6f, 0.045f, 0.22f), light);
        MeshBox(tower, new Vector3(0, floorY + 2.88f, -depth * 0.18f), new Vector3(2.2f, 0.045f, 0.18f), light);
        // One hall light per floor only.
        tower.AddChild(new OmniLight3D
        {
            Name = "ResidentialHallLight",
            Position = new Vector3(0, floorY + 2.72f, featuredFloor ? -depth * 0.18f : depth * 0.1f),
            LightColor = new Color(1.0f, 0.78f, 0.52f),
            LightEnergy = featuredFloor ? 1.45f : 0.7f,
            OmniRange = featuredFloor ? 16.0f : 9.0f,
            ShadowEnabled = false,
            DistanceFadeEnabled = true,
            DistanceFadeBegin = 36.0f,
            DistanceFadeLength = 14.0f
        });
        var floorLabel = RegisterResidentialLocalizedLabel(new Label3D
        {
            Name = "ResidentialFloorSign",
            Position = new Vector3(0, floorY + 1.65f, coreZ + ResidentialStairOpeningSouthDepth + 1.15f),
            FontSize = 22,
            OutlineSize = 6,
            Modulate = spec.Accent,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            VisibilityRangeEnd = 18.0f,
            VisibilityRangeEndMargin = 3.0f
        }, language => $"{ResidentialBlockName(spec.BlockName, language)}  //  {GameLocalization.Get("residential_floor", language, "FLOOR")} {floor + 1:00}");
        tower.AddChild(floorLabel);
    }

    private static ResidentialRoomArchetype ResidentialRoomArchetypeFor(int towerIndex, int floor, float side)
        => (ResidentialRoomArchetype)((towerIndex * 5 + floor * 2 + (side > 0.0f ? 1 : 0)) % 7);

    private static string ResidentialRoomName(ResidentialRoomArchetype archetype, string language)
    {
        var key = archetype switch
        {
            ResidentialRoomArchetype.MedicalClinic => "residential_room_clinic",
            ResidentialRoomArchetype.EvacuationShelter => "residential_room_evac_shelter",
            ResidentialRoomArchetype.MaintenanceWorkshop => "residential_room_maintenance",
            ResidentialRoomArchetype.CommunitySecurity => "residential_room_security",
            ResidentialRoomArchetype.SmugglerDen => "residential_room_sealed_unit",
            ResidentialRoomArchetype.CommunityKitchen => "residential_room_kitchen",
            _ => "residential_room_family"
        };
        var english = archetype switch
        {
            ResidentialRoomArchetype.MedicalClinic => "COMMUNITY CLINIC",
            ResidentialRoomArchetype.EvacuationShelter => "EVAC SHELTER",
            ResidentialRoomArchetype.MaintenanceWorkshop => "MAINTENANCE FLAT",
            ResidentialRoomArchetype.CommunitySecurity => "SECURITY POST",
            ResidentialRoomArchetype.SmugglerDen => "SEALED TENANT UNIT",
            ResidentialRoomArchetype.CommunityKitchen => "COMMUNITY KITCHEN",
            _ => "FAMILY APARTMENT"
        };
        return GameLocalization.Get(key, language, english);
    }

    private static string ResidentialBlockName(string blockName, string language)
    {
        var key = blockName switch
        {
            "HARBOR COURT A" => "residential_block_harbor_a",
            "HARBOR COURT B" => "residential_block_harbor_b",
            "NORTH QUAY 1" => "residential_block_north_1",
            "NORTH QUAY 2" => "residential_block_north_2",
            "NORTH QUAY 3" => "residential_block_north_3",
            "WEST GATE TOWER" => "residential_block_west_tower",
            "EAST GATE TOWER" => "residential_block_east_tower",
            "SOUTH COURT 1" => "residential_block_south_1",
            "SOUTH COURT 2" => "residential_block_south_2",
            "SOUTH COURT 3" => "residential_block_south_3",
            "SOUTH COURT 4" => "residential_block_south_4",
            "SOUTH COURT" => "residential_district_south",
            "NORTH QUAY" => "residential_district_north",
            _ => string.Empty
        };
        return string.IsNullOrEmpty(key) ? blockName : GameLocalization.Get(key, language, blockName);
    }

    private static Color ResidentialRoomColor(ResidentialRoomArchetype archetype) => archetype switch
    {
        ResidentialRoomArchetype.MedicalClinic => new Color(0.3f, 0.94f, 0.62f),
        ResidentialRoomArchetype.EvacuationShelter => new Color(1.0f, 0.65f, 0.22f),
        ResidentialRoomArchetype.MaintenanceWorkshop => new Color(0.95f, 0.78f, 0.2f),
        ResidentialRoomArchetype.CommunitySecurity => new Color(0.34f, 0.68f, 1.0f),
        ResidentialRoomArchetype.SmugglerDen => new Color(0.92f, 0.34f, 0.24f),
        ResidentialRoomArchetype.CommunityKitchen => new Color(0.54f, 0.82f, 0.38f),
        _ => new Color(0.82f, 0.72f, 0.56f)
    };

    private static ResidentialFurnitureKind ResidentialFurnitureKindFor(
        int towerIndex,
        int floor,
        float side,
        ResidentialRoomArchetype archetype,
        bool featuredFloor)
    {
        if (featuredFloor && archetype == ResidentialRoomArchetype.SmugglerDen)
        {
            return ResidentialFurnitureKind.Wardrobe;
        }
        if (featuredFloor && archetype == ResidentialRoomArchetype.CommunitySecurity)
        {
            return ResidentialFurnitureKind.DeskDrawers;
        }
        var selector = towerIndex * 7 + floor * 3 + (side > 0.0f ? 1 : 0);
        return (ResidentialFurnitureKind)Mathf.PosMod(selector, Enum.GetValues<ResidentialFurnitureKind>().Length);
    }

    private void SpawnResidentialFurniture(
        Node3D tower,
        int towerIndex,
        int floor,
        float side,
        ResidentialRoomArchetype archetype,
        bool featuredFloor,
        ResidentialFurnitureKind kind,
        float roomX,
        float roomWidth,
        float depth,
        float floorY,
        float bedZ,
        float kitchenX)
    {
        var localPosition = kind switch
        {
            ResidentialFurnitureKind.Refrigerator => new Vector3(kitchenX, floorY + 0.08f, depth * 0.05f - 1.15f),
            ResidentialFurnitureKind.Wardrobe => new Vector3(roomX - side * roomWidth * 0.28f, floorY + 0.06f, bedZ - 0.15f),
            ResidentialFurnitureKind.DeskDrawers => new Vector3(roomX - side * 0.2f, floorY + 0.08f, -depth * 0.08f - side * 0.72f),
            _ => new Vector3(roomX + side * roomWidth * 0.28f, floorY + 0.08f, bedZ + 0.75f)
        };
        var eventKind = ResidentialRoomEventKind.None;
        var furniture = new ResidentialSearchableFurniture
        {
            Name = $"ResidentialFurniture_T{towerIndex + 1:00}_F{floor + 1:00}_S{(side > 0.0f ? 1 : 0)}_{kind}",
            Position = localPosition,
            Rotation = new Vector3(0, side < 0.0f ? 0.0f : Mathf.Pi, 0)
        };
        furniture.Configure(
            kind,
            eventKind,
            towerIndex,
            floor,
            side > 0.0f ? 1 : -1,
            CreateResidentialFurnitureLoot(kind, archetype, towerIndex, floor, side));
        furniture.FirstSearched += OnResidentialFurnitureSearched;
        tower.AddChild(furniture);
        _residentialFurniture.Add(furniture);
        _lootSources.Add(furniture);
        _lootWorldPoints.Add(furniture.GlobalPosition);
    }

    private void BuildResidentialRoomTheme(
        Node3D tower,
        ResidentialRoomArchetype archetype,
        float roomX,
        float side,
        float roomWidth,
        float depth,
        float floorY)
    {
        var outerX = roomX + side * roomWidth * 0.27f;
        var innerX = roomX - side * roomWidth * 0.2f;
        var northZ = -depth * 0.31f;
        var southZ = depth * 0.31f;
        var accent = ResidentialRoomColor(archetype);
        var accentMat = Mat($"residential_theme_{archetype}", accent * 0.65f, 0.08f, 0.72f);
        var dark = Mat("residential_theme_dark", new Color(0.07f, 0.085f, 0.085f), 0.55f, 0.44f);
        var pale = Mat("residential_theme_pale", new Color(0.66f, 0.7f, 0.66f), 0.08f, 0.82f);
        var glow = Mat($"residential_theme_glow_{archetype}", accent, 0.04f, 0.3f, accent * 0.8f);

        MeshBox(
            tower,
            new Vector3(roomX, floorY + 2.82f, -depth * 0.18f),
            new Vector3(Mathf.Min(2.8f, roomWidth * 0.62f), 0.045f, 0.22f),
            glow);
        MeshBox(
            tower,
            new Vector3(roomX, floorY + 0.075f, -depth * 0.18f),
            new Vector3(Mathf.Min(2.65f, roomWidth * 0.58f), 0.035f, 0.58f),
            accentMat);

        switch (archetype)
        {
            case ResidentialRoomArchetype.MedicalClinic:
                ExpansionBox(tower, "ClinicCotN", new Vector3(innerX, floorY + 0.34f, northZ), new Vector3(1.9f, 0.45f, 0.72f), pale);
                ExpansionBox(tower, "ClinicCotS", new Vector3(innerX, floorY + 0.34f, southZ), new Vector3(1.9f, 0.45f, 0.72f), pale);
                MeshBox(tower, new Vector3(innerX, floorY + 0.61f, northZ), new Vector3(1.72f, 0.08f, 0.58f), accentMat);
                MeshBox(tower, new Vector3(innerX, floorY + 0.61f, southZ), new Vector3(1.72f, 0.08f, 0.58f), accentMat);
                ExpansionBox(tower, "ClinicScreen", new Vector3(outerX, floorY + 1.05f, northZ + 1.0f), new Vector3(0.08f, 1.8f, 1.65f), accentMat);
                MeshBox(tower, new Vector3(outerX, floorY + 1.42f, southZ - 0.8f), new Vector3(0.08f, 0.48f, 0.48f), glow);
                break;
            case ResidentialRoomArchetype.EvacuationShelter:
                foreach (var z in new[] { northZ, southZ })
                {
                    ExpansionBox(tower, "EvacBunkLow", new Vector3(innerX, floorY + 0.28f, z), new Vector3(1.95f, 0.34f, 0.72f), dark);
                    ExpansionBox(tower, "EvacBunkHigh", new Vector3(innerX, floorY + 1.28f, z), new Vector3(1.95f, 0.2f, 0.72f), dark);
                    MeshBox(tower, new Vector3(innerX, floorY + 0.5f, z), new Vector3(1.78f, 0.08f, 0.58f), accentMat);
                    MeshBox(tower, new Vector3(innerX, floorY + 1.43f, z), new Vector3(1.78f, 0.08f, 0.58f), accentMat);
                }
                ExpansionBox(tower, "EvacLuggage", new Vector3(outerX, floorY + 0.35f, northZ + 1.1f), new Vector3(0.82f, 0.62f, 0.52f), accentMat);
                ExpansionBox(tower, "EvacRationStack", new Vector3(outerX, floorY + 0.4f, southZ - 1.0f), new Vector3(0.9f, 0.72f, 0.72f), dark);
                break;
            case ResidentialRoomArchetype.MaintenanceWorkshop:
                ExpansionBox(tower, "WorkshopBenchN", new Vector3(innerX, floorY + 0.48f, northZ), new Vector3(2.2f, 0.78f, 0.72f), dark);
                ExpansionBox(tower, "WorkshopBenchS", new Vector3(innerX, floorY + 0.48f, southZ), new Vector3(2.2f, 0.78f, 0.72f), dark);
                ExpansionBox(tower, "WorkshopLocker", new Vector3(outerX, floorY + 1.0f, northZ + 1.0f), new Vector3(0.75f, 1.8f, 0.58f), accentMat);
                for (var pipe = -1; pipe <= 1; pipe++)
                {
                    MeshBox(tower, new Vector3(innerX + pipe * 0.34f, floorY + 1.18f, southZ - 0.1f), new Vector3(0.16f, 0.16f, 1.65f), pale);
                }
                MeshBox(tower, new Vector3(outerX, floorY + 1.55f, southZ - 0.9f), new Vector3(0.08f, 0.72f, 0.72f), glow);
                break;
            case ResidentialRoomArchetype.CommunitySecurity:
                ExpansionBox(tower, "SecurityDesk", new Vector3(innerX, floorY + 0.48f, southZ - 0.45f), new Vector3(2.15f, 0.78f, 0.72f), dark);
                for (var screenIndex = -1; screenIndex <= 1; screenIndex++)
                {
                    MeshBox(tower, new Vector3(innerX + screenIndex * 0.62f, floorY + 1.16f, southZ - 0.72f), new Vector3(0.5f, 0.4f, 0.06f), glow);
                }
                ExpansionBox(tower, "SecurityLocker", new Vector3(outerX, floorY + 1.0f, northZ), new Vector3(0.78f, 1.8f, 0.65f), accentMat);
                ExpansionBox(tower, "SecurityShieldRack", new Vector3(innerX, floorY + 0.8f, northZ + 0.9f), new Vector3(1.6f, 1.35f, 0.18f), dark);
                break;
            case ResidentialRoomArchetype.SmugglerDen:
                foreach (var offset in new[] { -0.75f, 0.0f, 0.75f })
                {
                    ExpansionBox(tower, "ContrabandCrate", new Vector3(innerX + offset, floorY + 0.36f, northZ), new Vector3(0.62f, 0.62f, 0.62f), dark);
                }
                ExpansionBox(tower, "SmugglerWorkbench", new Vector3(innerX, floorY + 0.5f, southZ), new Vector3(2.25f, 0.82f, 0.7f), accentMat);
                MeshBox(tower, new Vector3(innerX, floorY + 0.96f, southZ), new Vector3(1.7f, 0.06f, 0.5f), glow);
                MeshBox(tower, new Vector3(outerX, floorY + 1.3f, northZ + 1.1f), new Vector3(0.08f, 0.8f, 1.4f), accentMat);
                break;
            case ResidentialRoomArchetype.CommunityKitchen:
                ExpansionBox(tower, "KitchenIslandN", new Vector3(innerX, floorY + 0.52f, northZ), new Vector3(2.25f, 0.86f, 0.82f), pale);
                ExpansionBox(tower, "KitchenIslandS", new Vector3(innerX, floorY + 0.52f, southZ), new Vector3(2.25f, 0.86f, 0.82f), pale);
                ExpansionBox(tower, "KitchenColdStore", new Vector3(outerX, floorY + 1.0f, northZ + 1.0f), new Vector3(0.86f, 1.82f, 0.72f), accentMat);
                foreach (var z in new[] { -depth * 0.12f, depth * 0.18f })
                {
                    ExpansionBox(tower, "KitchenDiningTable", new Vector3(innerX, floorY + 0.42f, z), new Vector3(1.75f, 0.12f, 0.76f), dark);
                }
                break;
            default:
                ExpansionBox(tower, "FamilyDiningTable", new Vector3(innerX, floorY + 0.42f, southZ - 0.85f), new Vector3(1.65f, 0.12f, 0.82f), accentMat);
                ExpansionBox(tower, "FamilyToyChest", new Vector3(outerX, floorY + 0.3f, northZ + 1.0f), new Vector3(0.75f, 0.48f, 0.58f), accentMat);
                MeshBox(tower, new Vector3(outerX, floorY + 1.42f, southZ - 0.9f), new Vector3(0.08f, 0.68f, 0.9f), glow);
                break;
        }
    }

    private static bool IsResidentialFeaturedFloor(ResidentialTowerSpec spec, int floor)
        => floor == 0 || floor == spec.Floors / 2 || floor == Mathf.Max(1, spec.Floors - 2);

    private void SpawnResidentialCache(
        Node3D tower,
        int towerIndex,
        int floor,
        float side,
        ResidentialRoomZone zone,
        ResidentialRoomArchetype archetype,
        Vector3 localPosition)
    {
        var roomId = new ResidentialRoomId(towerIndex, floor, side > 0.0f ? 1 : -1, zone);
        var plan = ResidentialRoomLootRules.Plan(roomId, archetype, _residentialLootSeedSalt);
        var cache = new ResidentialSupplyCache
        {
            Name = $"ResidentialCache_T{towerIndex + 1:00}_F{floor + 1:00}_S{(side > 0.0f ? 1 : 0)}_{zone}",
            Position = localPosition,
            Rotation = new Vector3(0, side * Mathf.Pi * 0.5f, 0)
        };
        cache.ConfigureRoom(plan);
        cache.FirstOpened += OnResidentialCacheFirstOpened;
        tower.AddChild(cache);
        _residentialCaches.Add(cache);
        _residentialCacheCountByTower[towerIndex]++;
        _lootSources.Add(cache);
        _lootWorldPoints.Add(cache.GlobalPosition);
    }

    private static List<LootItem> CreateResidentialCacheLoot(ResidentialCacheKind kind)
    {
        var loot = new List<LootItem>();
        switch (kind)
        {
            case ResidentialCacheKind.MedicalCabinet:
                loot.Add(new LootItem { Kind = LootItemKind.Medical, MedicalKind = MedicalItemKind.Bandage, Quantity = 3, Grade = LootGrade.Uncommon });
                loot.Add(new LootItem { Kind = LootItemKind.Medical, MedicalKind = MedicalItemKind.FieldMedkit, Quantity = 2, Grade = LootGrade.Rare });
                loot.Add(new LootItem { Kind = LootItemKind.Medical, MedicalKind = MedicalItemKind.Adrenaline, Quantity = 1, Grade = LootGrade.Epic });
                break;
            case ResidentialCacheKind.EvacuationLocker:
                loot.Add(new LootItem { Kind = LootItemKind.Ammunition, AmmoCaliber = AmmoCaliber.Rifle, Quantity = 36, Grade = LootGrade.Uncommon });
                loot.Add(new LootItem { Kind = LootItemKind.Equipment, Equipment = EquipmentCatalog.Create("pack_assault"), Grade = LootGrade.Rare });
                loot.Add(new LootItem { Kind = LootItemKind.Medical, MedicalKind = MedicalItemKind.Bandage, Quantity = 2, Grade = LootGrade.Common });
                break;
            case ResidentialCacheKind.WorkshopLocker:
                loot.Add(new LootItem { Kind = LootItemKind.Attachment, AttachmentId = "grip_vertical", Grade = LootGrade.Rare });
                loot.Add(new LootItem { Kind = LootItemKind.Attachment, AttachmentId = "muzzle_brake", Grade = LootGrade.Uncommon });
                loot.Add(new LootItem { Kind = LootItemKind.Medical, MedicalKind = MedicalItemKind.Bandage, Quantity = 1, Grade = LootGrade.Common });
                break;
            case ResidentialCacheKind.SecurityArmory:
                loot.Add(new LootItem { Kind = LootItemKind.Weapon, Weapon = WeaponCatalog.Build(WeaponPlatform.MP5A5, 1), Grade = LootGrade.Rare });
                loot.Add(new LootItem { Kind = LootItemKind.Ammunition, AmmoCaliber = AmmoCaliber.Smg, Quantity = 60, Grade = LootGrade.Uncommon });
                loot.Add(new LootItem { Kind = LootItemKind.Medical, MedicalKind = MedicalItemKind.Adrenaline, Quantity = 1, Grade = LootGrade.Rare });
                break;
            case ResidentialCacheKind.SmugglerCache:
                loot.Add(new LootItem { Kind = LootItemKind.Attachment, AttachmentId = "muzzle_suppressor", Grade = LootGrade.Epic });
                loot.Add(new LootItem { Kind = LootItemKind.Ammunition, AmmoCaliber = AmmoCaliber.Sniper, Quantity = 18, Grade = LootGrade.Rare });
                loot.Add(new LootItem { Kind = LootItemKind.Medical, MedicalKind = MedicalItemKind.Adrenaline, Quantity = 2, Grade = LootGrade.Epic });
                break;
            case ResidentialCacheKind.CommunityPantry:
                loot.Add(new LootItem { Kind = LootItemKind.Ammunition, AmmoCaliber = AmmoCaliber.Rifle, Quantity = 42, Grade = LootGrade.Common });
                loot.Add(new LootItem { Kind = LootItemKind.ArmorPlate, Quantity = 1, Grade = LootGrade.Uncommon });
                loot.Add(new LootItem { Kind = LootItemKind.Medical, MedicalKind = MedicalItemKind.FieldMedkit, Quantity = 1, Grade = LootGrade.Uncommon });
                break;
            default:
                loot.Add(new LootItem { Kind = LootItemKind.Ammunition, AmmoCaliber = AmmoCaliber.Rifle, Quantity = 24, Grade = LootGrade.Common });
                loot.Add(new LootItem { Kind = LootItemKind.ArmorPlate, Quantity = 1, Grade = LootGrade.Uncommon });
                loot.Add(new LootItem { Kind = LootItemKind.Medical, MedicalKind = MedicalItemKind.Bandage, Quantity = 1, Grade = LootGrade.Common });
                break;
        }
        return loot;
    }

    private void BuildApartmentWallWithDoor(
        Node3D tower,
        float x,
        float floorY,
        float startZ,
        float endZ,
        float doorCenterZ,
        Godot.Material wall)
    {
        var segmentLength = endZ - startZ;
        if (segmentLength < 1.8f)
        {
            return;
        }
        const float wallHeight = 2.85f;
        const float doorHeight = 2.55f;
        // Keep the doorway narrower than the available wall segment so Clamp min/max stay ordered.
        var doorWidth = Mathf.Min(1.7f, Mathf.Max(1.1f, segmentLength - 0.9f));
        var minDoorStart = startZ + 0.25f;
        var maxDoorStart = endZ - doorWidth - 0.25f;
        if (maxDoorStart < minDoorStart)
        {
            // Segment too tight for a framed door — solid wall only.
            var solidY = floorY + 0.1f + wallHeight * 0.5f;
            ExpansionBox(tower, "ApartmentCorridorWall", new Vector3(x, solidY, (startZ + endZ) * 0.5f), new Vector3(0.12f, wallHeight, segmentLength), wall);
            return;
        }
        var doorStart = Mathf.Clamp(doorCenterZ - doorWidth * 0.5f, minDoorStart, maxDoorStart);
        var doorEnd = doorStart + doorWidth;
        var firstLength = doorStart - startZ;
        var secondLength = endZ - doorEnd;
        var wallCenterY = floorY + 0.1f + wallHeight * 0.5f;
        if (firstLength > 0.12f)
        {
            ExpansionBox(tower, "ApartmentCorridorWall", new Vector3(x, wallCenterY, startZ + firstLength * 0.5f), new Vector3(0.12f, wallHeight, firstLength), wall);
        }
        if (secondLength > 0.12f)
        {
            ExpansionBox(tower, "ApartmentCorridorWall", new Vector3(x, wallCenterY, doorEnd + secondLength * 0.5f), new Vector3(0.12f, wallHeight, secondLength), wall);
        }
        var headerHeight = Mathf.Max(0.12f, wallHeight - doorHeight);
        ExpansionBox(
            tower,
            "ApartmentDoorHeader",
            new Vector3(x, floorY + 0.1f + doorHeight + headerHeight * 0.5f, doorStart + doorWidth * 0.5f),
            new Vector3(0.12f, headerHeight, doorWidth),
            wall);
    }

    private void BuildTowerStairs(
        Node3D tower,
        int floor,
        float floorY,
        float coreZ,
        Godot.Material stair,
        Godot.Material light)
    {
        // Keep discrete treads for capsule and ballistic collisions, but register them on one
        // static body per floor instead of one body per part. Across 96 floors this removes
        // thousands of broadphase bodies without changing the walkable surfaces.
        var halfRise = ResidentialFloorHeight * 0.5f;
        const int steps = ResidentialStepsPerFlight;
        var stepRise = halfRise / steps;
        var stepRun = ResidentialStairRun / steps;
        const float treadWidth = ResidentialStairTreadWidth;
        var treadDepth = stepRun * 1.08f;
        const float treadThickness = ResidentialStairTreadThickness;
        var lowerStartZ = coreZ - ResidentialStairRun * 0.5f;
        var upperStartZ = coreZ + ResidentialStairRun * 0.5f;
        const float landingWidth = ResidentialStairLandingWidth;
        const float landingDepth = ResidentialStairLandingDepth;
        var landingSouthZ = lowerStartZ + 0.2f;
        var landingNorthZ = landingSouthZ - landingDepth;
        var landingCenterZ = (landingNorthZ + landingSouthZ) * 0.5f;
        var stairCollision = new StaticBody3D
        {
            Name = $"ResidentialStairCollision_F{floor}",
            CollisionLayer = 1,
            CollisionMask = 0
        };
        tower.AddChild(stairCollision);

        var stepMultiMesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            Mesh = SharedBoxMesh(new Vector3(treadWidth, treadThickness, treadDepth)),
            InstanceCount = steps * 2
        };
        var stepVisual = new MultiMeshInstance3D
        {
            Name = $"ResidentialStairSteps_F{floor}",
            Position = new Vector3(0, floorY, 0),
            Multimesh = stepMultiMesh,
            MaterialOverride = stair,
            VisibilityRangeEnd = 82.0f,
            VisibilityRangeEndMargin = 8.0f
        };
        tower.AddChild(stepVisual);
        RegisterMapDetailVisual(stepVisual);
        var visualIndex = 0;

        // Lower flight (west) -- each step remains an independent collision surface.
        for (var step = 0; step < steps; step++)
        {
            var topY = floorY + stepRise * (step + 1);
            var z = upperStartZ - stepRun * (step + 0.5f);
            var position = new Vector3(-1.45f, topY - treadThickness * 0.5f, z);
            AddResidentialStairCollision(
                stairCollision,
                $"ResidentialStairStep_L{floor}_{step}",
                position,
                new Vector3(treadWidth, treadThickness, treadDepth));
            stepMultiMesh.SetInstanceTransform(
                visualIndex++,
                new Transform3D(Basis.Identity, new Vector3(position.X, position.Y - floorY, position.Z)));
        }

        // Keep one compact continuous collider for reliable turning, while the visible
        // surface is built as open slats by BuildTowerStairDetails.
        AddResidentialStairCollision(
            stairCollision,
            $"ResidentialStairLanding_F{floor}",
            new Vector3(0, floorY + halfRise - treadThickness * 0.5f, landingCenterZ),
            new Vector3(landingWidth, treadThickness, landingDepth));

        // Upper flight (east).
        for (var step = 0; step < steps; step++)
        {
            var topY = floorY + halfRise + stepRise * (step + 1);
            var z = lowerStartZ + stepRun * (step + 0.5f);
            var position = new Vector3(1.45f, topY - treadThickness * 0.5f, z);
            AddResidentialStairCollision(
                stairCollision,
                $"ResidentialStairStep_U{floor}_{step}",
                position,
                new Vector3(treadWidth, treadThickness, treadDepth));
            stepMultiMesh.SetInstanceTransform(
                visualIndex++,
                new Transform3D(Basis.Identity, new Vector3(position.X, position.Y - floorY, position.Z)));
        }

        // Keep the switchback center open. Thin guard colliders follow the visible inner
        // handrails, preventing falls without restoring the full-height plate between flights.
        var coreTop = floorY + ResidentialFloorHeight * 0.5f;
        const float innerGuardHeight = 0.84f;
        var innerGuardEdge = ResidentialStairTreadWidth * 0.5f + 0.015f;
        var innerGuardLength = Mathf.Sqrt(ResidentialStairRun * ResidentialStairRun + halfRise * halfRise) + 0.08f;
        var innerGuardAngle = Mathf.Atan2(halfRise, ResidentialStairRun);
        foreach (var guard in new (string Side, float X, float BaseY, float Angle)[]
        {
            ("L", -1.45f + innerGuardEdge, floorY, innerGuardAngle),
            ("U", 1.45f - innerGuardEdge, floorY + halfRise, -innerGuardAngle)
        })
        {
            var surfaceMidY = guard.BaseY + halfRise * 0.5f + stepRise * 0.5f;
            AddResidentialStairCollision(
                stairCollision,
                $"ResidentialStairInnerGuard_{guard.Side}{floor}",
                new Vector3(guard.X, surfaceMidY + innerGuardHeight * 0.5f + 0.08f, coreZ),
                new Vector3(0.1f, innerGuardHeight, innerGuardLength),
                new Vector3(guard.Angle, 0, 0));
        }
        var shaftNorthZ = landingNorthZ - 0.06f;
        var shaftSouthZ = upperStartZ + 0.45f;
        var shaftCenterZ = (shaftNorthZ + shaftSouthZ) * 0.5f;
        var shaftSideDepth = shaftSouthZ - shaftNorthZ - 0.12f;
        AddResidentialStairPart(stairCollision, tower, $"ResidentialStairShaftN_F{floor}", new Vector3(0, coreTop, shaftNorthZ), new Vector3(5.44f, ResidentialFloorHeight, 0.12f), stair);
        AddResidentialStairPart(stairCollision, tower, $"ResidentialStairShaftW_F{floor}", new Vector3(-2.66f, coreTop, shaftCenterZ), new Vector3(0.12f, ResidentialFloorHeight, shaftSideDepth), stair);
        AddResidentialStairPart(stairCollision, tower, $"ResidentialStairShaftE_F{floor}", new Vector3(2.66f, coreTop, shaftCenterZ), new Vector3(0.12f, ResidentialFloorHeight, shaftSideDepth), stair);
        // The corridor opening serves both switchback flights. A centered 2.2 m door hid
        // half of each 1.95 m tread run, so leave only structural jambs at the shaft edges.
        const float shaftDoorHalf = 2.6f;
        var shaftSideWidth = 2.72f - shaftDoorHalf;
        AddResidentialStairPart(stairCollision, tower, $"ResidentialStairShaftSW_F{floor}", new Vector3(-(shaftDoorHalf + shaftSideWidth * 0.5f), floorY + 0.1f + 1.275f, upperStartZ + 0.45f), new Vector3(shaftSideWidth, 2.55f, 0.12f), stair);
        AddResidentialStairPart(stairCollision, tower, $"ResidentialStairShaftSE_F{floor}", new Vector3(shaftDoorHalf + shaftSideWidth * 0.5f, floorY + 0.1f + 1.275f, upperStartZ + 0.45f), new Vector3(shaftSideWidth, 2.55f, 0.12f), stair);
        AddResidentialStairPart(stairCollision, tower, $"ResidentialStairShaftSH_F{floor}", new Vector3(0, floorY + 0.1f + 2.55f + (ResidentialFloorHeight - 2.55f) * 0.5f, upperStartZ + 0.45f), new Vector3(shaftDoorHalf * 2.0f, Mathf.Max(0.12f, ResidentialFloorHeight - 2.55f), 0.12f), stair);
        var landingLight = MeshBox(tower, new Vector3(0, floorY + halfRise + 1.35f, landingNorthZ + 0.08f), new Vector3(1.8f, 0.04f, 0.16f), light);
        landingLight.Name = $"ResidentialStairLandingLight_F{floor}";
        RegisterMapDetailVisual(landingLight);
        _residentialStairFlightCount += 2;
    }

    private static void AddResidentialStairCollision(
        StaticBody3D body,
        string _,
        Vector3 position,
        Vector3 size,
        Vector3 rotation = default)
    {
        var owner = body.CreateShapeOwner(body);
        body.ShapeOwnerSetTransform(
            owner,
            new Transform3D(Basis.FromEuler(rotation), position));
        body.ShapeOwnerAddShape(owner, SharedBoxShape(size));
    }

    private void AddResidentialStairPart(
        StaticBody3D collisionBody,
        Node3D visualParent,
        string name,
        Vector3 position,
        Vector3 size,
        Godot.Material material)
    {
        AddResidentialStairCollision(collisionBody, name, position, size);
        var visual = MeshBox(visualParent, position, size, material);
        visual.Name = name + "_Visual";
        RegisterMapDetailVisual(visual);
    }

    private void BuildTowerRoof(
        Node3D tower,
        ResidentialTowerSpec spec,
        float coreZ,
        Godot.Material facade,
        Godot.Material trim,
        Godot.Material light)
    {
        var roofY = spec.Floors * ResidentialFloorHeight;
        BuildTowerFloorSlab(tower, spec, spec.Floors, roofY, coreZ, facade, false);
        var width = spec.Footprint.X;
        var depth = spec.Footprint.Y;
        foreach (var rail in new (Vector3 Position, Vector3 Size)[]
        {
            (new Vector3(0, roofY + 0.72f, -depth * 0.5f), new Vector3(width, 1.25f, 0.1f)),
            (new Vector3(0, roofY + 0.72f, depth * 0.5f), new Vector3(width, 1.25f, 0.1f)),
            (new Vector3(-width * 0.5f, roofY + 0.72f, 0), new Vector3(0.1f, 1.25f, depth)),
            (new Vector3(width * 0.5f, roofY + 0.72f, 0), new Vector3(0.1f, 1.25f, depth))
        })
        {
            MeshBox(tower, rail.Position, rail.Size, trim);
        }
        MeshBox(tower, new Vector3(0, roofY + 0.3f, coreZ + ResidentialStairOpeningSouthDepth + 0.5f), new Vector3(2.4f, 0.05f, 0.24f), light);
    }

    private void BuildTowerCourtyard(
        Node3D tower,
        ResidentialTowerSpec spec,
        Godot.Material facade,
        Godot.Material steel,
        Godot.Material light)
    {
        var width = spec.Footprint.X;
        var depth = spec.Footprint.Y;
        var paving = Mat("residential_courtyard", new Color(0.48f, 0.49f, 0.45f), 0.01f, 0.92f);
        var planter = Mat("residential_planter", new Color(0.24f, 0.27f, 0.23f), 0.05f, 0.86f);
        var foliage = Mat("residential_foliage", new Color(0.13f, 0.31f, 0.18f), 0.0f, 0.92f);
        ExpansionBox(tower, "ResidentialCourtyard", new Vector3(0, 0.035f, 0), new Vector3(width + 6.0f, 0.1f, depth + 6.0f), paving);
        ExpansionBox(tower, "ResidentialEntryCanopy", new Vector3(0, 3.15f, depth * 0.5f + 1.25f), new Vector3(5.8f, 0.18f, 2.7f), steel);
        MeshBox(tower, new Vector3(0, 3.0f, depth * 0.5f + 1.38f), new Vector3(3.8f, 0.12f, 0.18f), light);
        var sign = RegisterResidentialLocalizedLabel(new Label3D
        {
            Name = $"ResidentialEntrySign_{spec.BlockName.Replace(" ", string.Empty)}",
            Position = new Vector3(0, 3.38f, depth * 0.5f + 0.2f),
            FontSize = 28,
            OutlineSize = 8,
            Modulate = spec.Accent,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            VisibilityRangeEnd = 44.0f,
            VisibilityRangeEndMargin = 6.0f
        }, language => ResidentialBlockName(spec.BlockName, language));
        tower.AddChild(sign);
        var planterIndex = 0;
        foreach (var x in new[] { -width * 0.34f, width * 0.34f })
        {
            ExpansionBox(tower, $"CourtyardPlanter_{planterIndex}", new Vector3(x, 0.34f, depth * 0.5f + 1.75f), new Vector3(2.2f, 0.58f, 1.1f), planter);
            tower.AddChild(new MeshInstance3D
            {
                Name = $"CourtyardHedgeVisual_{planterIndex}",
                Position = new Vector3(x, 1.02f, depth * 0.5f + 1.75f),
                Mesh = new SphereMesh { Radius = 0.72f, Height = 1.15f, RadialSegments = 12, Rings = 6 },
                MaterialOverride = foliage
            });
            var hedgeCollider = new StaticBody3D
            {
                Name = $"CourtyardHedgeCollider_{planterIndex}",
                Position = new Vector3(x, 1.0f, depth * 0.5f + 1.75f),
                CollisionLayer = 1,
                CollisionMask = 0
            };
            hedgeCollider.AddToGroup("courtyard_hedge_colliders");
            hedgeCollider.AddChild(new CollisionShape3D
            {
                Shape = new BoxShape3D { Size = new Vector3(1.72f, 1.18f, 0.84f) }
            });
            tower.AddChild(hedgeCollider);
            planterIndex++;
        }
        MeshBox(tower, new Vector3(-width * 0.28f, 0.52f, depth * 0.5f + 2.15f), new Vector3(2.5f, 0.14f, 0.52f), facade);
        MeshBox(tower, new Vector3(width * 0.28f, 0.52f, depth * 0.5f + 2.15f), new Vector3(2.5f, 0.14f, 0.52f), facade);
    }

    private void SpawnResidentialOccupants(Transform3D towerTransform, ResidentialTowerSpec spec, int towerIndex)
    {
        var roles = new[]
        {
            CivilianRole.VolunteerMedic,
            CivilianRole.CommunityGuard,
            CivilianRole.UtilityWorker,
            CivilianRole.Evacuee
        };
        var occupants = new List<(int Floor, CivilianRole Role, float Side)>
        {
            (0, CivilianRole.Resident, -1.0f),
            (Mathf.Clamp(spec.Floors / 2, 1, spec.Floors - 1), roles[towerIndex % roles.Length], 1.0f),
            (Mathf.Max(1, spec.Floors - 2), CivilianRole.Evacuee, -1.0f)
        };
        if (spec.Floors >= 9)
        {
            occupants.Add((Mathf.Clamp(spec.Floors / 3, 1, spec.Floors - 1), CivilianRole.Resident, 1.0f));
        }
        foreach (var occupant in occupants)
        {
            var roomCenterX = occupant.Side * Mathf.Max(4.2f, spec.Footprint.X * 0.25f);
            var roomCenterZ = spec.Footprint.Y * 0.24f;
            var localPosition = new Vector3(roomCenterX, occupant.Floor * ResidentialFloorHeight + 0.14f, roomCenterZ);
            var civilian = new CivilianNpc
            {
                Name = $"Civilian_T{towerIndex + 1:00}_F{occupant.Floor + 1:00}_{occupant.Role}"
            };
            civilian.Configure(
                this,
                occupant.Role,
                towerIndex,
                occupant.Floor,
                towerTransform,
                localPosition,
                new Vector2(Mathf.Min(1.8f, spec.Footprint.X * 0.08f), Mathf.Min(2.1f, spec.Footprint.Y * 0.08f)));
            RegisterResidentialLanguageRefresher(civilian.SetLanguage);
            _levelRoot.AddChild(civilian);
            _civilians.Add(civilian);
        }
    }

    private async void ValidateResidentialCommunity()
    {
        foreach (var enemy in _enemies)
        {
            enemy.ProcessMode = ProcessModeEnum.Disabled;
        }
        foreach (var civilian in _civilians)
        {
            if (IsInstanceValid(civilian))
            {
                civilian.ProcessMode = ProcessModeEnum.Disabled;
            }
        }
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        var expectedFloors = 0;
        foreach (var spec in ResidentialTowerSpecs)
        {
            expectedFloors += spec.Floors;
        }

        var firstTower = _residentialTowers[0];
        var firstSpec = ResidentialTowerSpecs[0];
        var firstCoreZ = -Mathf.Min(firstSpec.Footprint.Y * 0.18f, 3.6f);
        var entryFrom = firstTower.ToGlobal(new Vector3(0, 1.2f, firstSpec.Footprint.Y * 0.5f + 1.2f));
        var entryTo = firstTower.ToGlobal(new Vector3(0, 1.2f, firstSpec.Footprint.Y * 0.5f - 2.2f));
        var entryOpen = !PhysicsRaycast.HasHit(
            GetWorld3D().DirectSpaceState,
            entryFrom,
            entryTo,
            1);

        var rampSample = firstTower.ToGlobal(new Vector3(
            -1.45f,
            0.1f + ResidentialFloorHeight * 0.25f,
            firstCoreZ + ResidentialStairRun * 0.2f));
        var hasRampHit = PhysicsRaycast.TryHit(
            GetWorld3D().DirectSpaceState,
            rampSample + Vector3.Up * 1.8f,
            rampSample - Vector3.Up * 1.8f,
            1,
            out var rampHit);
        var rampCollider = hasRampHit ? rampHit.Collider as Node : null;
        var stepName = rampCollider?.Name.ToString() ?? "";
        // Prefer discrete StairStep colliders; never require a solid ramp slab.
        var stepCollision = stepName.Contains("StairStep", StringComparison.Ordinal)
            || stepName.Contains("StairLanding", StringComparison.Ordinal)
            || stepName.Contains("Stair", StringComparison.Ordinal)
            || (hasRampHit && rampHit.Position.Y > 0.35f);

        // Standing doorway clearance probe (no crouch required).
        var doorProbeFrom = firstTower.ToGlobal(new Vector3(0, 1.7f, firstSpec.Footprint.Y * 0.5f + 0.8f));
        var doorProbeTo = firstTower.ToGlobal(new Vector3(0, 1.7f, firstSpec.Footprint.Y * 0.5f - 1.5f));
        var standingDoorClear = !PhysicsRaycast.HasHit(
            GetWorld3D().DirectSpaceState,
            doorProbeFrom,
            doorProbeTo,
            1);

        var stairDoorClearSamples = 0;
        var stairDoorSupportSamples = 0;
        var expectedStairDoorClearSamples = expectedFloors * 4;
        var expectedStairDoorSupportSamples = expectedFloors * 2;
        foreach (var (tower, spec) in _residentialTowers.Zip(ResidentialTowerSpecs))
        {
            var coreZ = -Mathf.Min(spec.Footprint.Y * 0.18f, 3.6f);
            for (var floor = 0; floor < spec.Floors; floor++)
            {
                var floorY = floor * ResidentialFloorHeight;
                foreach (var sampleX in new[] { -2.345f, -0.555f, 0.555f, 2.345f })
                {
                    if (!PhysicsRaycast.TryHit(
                            GetWorld3D().DirectSpaceState,
                            tower.ToGlobal(new Vector3(
                                sampleX,
                                floorY + 1.35f,
                                coreZ + ResidentialStairRun * 0.5f + 1.2f)),
                            tower.ToGlobal(new Vector3(
                                sampleX,
                                floorY + 1.35f,
                                coreZ + ResidentialStairRun * 0.5f + 0.1f)),
                            1,
                            out _))
                    {
                        stairDoorClearSamples++;
                    }
                }

                foreach (var flightX in new[] { -1.45f, 1.45f })
                {
                    if (PhysicsRaycast.TryHit(
                            GetWorld3D().DirectSpaceState,
                            tower.ToGlobal(new Vector3(
                                flightX,
                                floorY + 0.55f,
                                coreZ + ResidentialStairRun * 0.5f - 0.05f)),
                            tower.ToGlobal(new Vector3(
                                flightX,
                                floorY - 0.45f,
                                coreZ + ResidentialStairRun * 0.5f - 0.05f)),
                            1,
                            out _))
                    {
                        stairDoorSupportSamples++;
                    }
                }
            }
        }
        var stairDoorClear = stairDoorClearSamples == expectedStairDoorClearSamples;
        var stairDoorSupported = stairDoorSupportSamples == expectedStairDoorSupportSamples;
        var stairWallPanels = _levelRoot.FindChildren(
            "ResidentialStairWallPanels_*",
            "MultiMeshInstance3D",
            recursive: true,
            owned: false);
        using var stairWallPanelsBacking = stairWallPanels.AsDisposable();
        var stairWallPanelsAbsent = stairWallPanels.Count == 0;

        // Face into the lower flight and walk forward along body yaw (no mid-flight teleports).
        _player.GlobalPosition = firstTower.ToGlobal(new Vector3(
            -1.45f,
            0.25f,
            firstCoreZ + ResidentialStairRun * 0.5f - 0.25f));
        var climbTarget = firstTower.ToGlobal(new Vector3(-1.45f, ResidentialFloorHeight * 0.5f + 0.25f, firstCoreZ - ResidentialStairRun * 0.2f));
        _player.FaceWorldPointForDiagnostics(climbTarget);
        _player.RestoreMovementInput();
        for (var frame = 0; frame < 10; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
        var climbStartY = _player.GlobalPosition.Y;
        Input.ActionPress("move_forward");
        Input.ActionPress("sprint");
        for (var frame = 0; frame < 400; frame++)
        {
            if (frame % 5 == 0)
            {
                _player.FaceWorldPointForDiagnostics(climbTarget);
            }
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
        Input.ActionRelease("sprint");
        Input.ActionRelease("move_forward");
        var climbHeight = _player.GlobalPosition.Y - climbStartY;
        var playerClimbedRamp = climbHeight > 0.70f;

        var hedgeColliders = GetTree().GetNodesInGroup("courtyard_hedge_colliders");
        using var hedgeCollidersBacking = hedgeColliders.AsDisposable();
        var hedgeCollisionCount = 0;
        foreach (var node in hedgeColliders)
        {
            if (node is not StaticBody3D hedge || !IsInstanceValid(hedge))
            {
                continue;
            }
            var axis = hedge.GlobalTransform.Basis.X.Normalized();
            if (PhysicsRaycast.TryHit(
                    GetWorld3D().DirectSpaceState,
                    hedge.GlobalPosition - axis * 1.5f,
                    hedge.GlobalPosition + axis * 1.5f,
                    1,
                    out var hedgeHit)
                && hedgeHit.Collider == hedge)
            {
                hedgeCollisionCount++;
            }
        }
        var hedgesSolid = hedgeColliders.Count == ResidentialTowerSpecs.Length * 2
            && hedgeCollisionCount == hedgeColliders.Count;

        var roles = new HashSet<CivilianRole>();
        var upperFloorPopulation = false;
        foreach (var civilian in _civilians)
        {
            roles.Add(civilian.Role);
            upperFloorPopulation |= civilian.FloorIndex >= 4;
        }
        var valid = ResidentialTowerCount == ResidentialTowerSpecs.Length
            && _residentialFloorCount == expectedFloors
            && _residentialStairFlightCount == expectedFloors * 2
            && _residentialStairDetailCount == expectedFloors
            && _residentialInfillModuleCount == ResidentialTowerSpecs.Length * 4
            && _residentialRoofAccessCount == ResidentialTowerSpecs.Length
            && _residentialEntrances.Count == ResidentialTowerSpecs.Length
            && _residentialRooftops.Count == ResidentialTowerSpecs.Length
            && entryOpen
            && standingDoorClear
            && stairDoorClear
            && stairDoorSupported
            && stairWallPanelsAbsent
            && stepCollision
            && playerClimbedRamp
            && hedgesSolid
            && ResidentialCivilianCount >= ResidentialTowerSpecs.Length * 3
            && ResidentialSpecialCivilianCount >= ResidentialTowerSpecs.Length * 2
            && roles.Count == Enum.GetValues<CivilianRole>().Length
            && upperFloorPopulation;
        GD.Print($"RESIDENTIAL_CHECK valid={valid} towers={ResidentialTowerCount}/{ResidentialTowerSpecs.Length} floors={_residentialFloorCount}/{expectedFloors} stair_flights={_residentialStairFlightCount} stair_details={_residentialStairDetailCount}/{expectedFloors} stair_panels_absent={stairWallPanelsAbsent} infill={_residentialInfillModuleCount}/{ResidentialTowerSpecs.Length * 4} entry_open={entryOpen} standing_door={standingDoorClear} stair_door_clear={stairDoorClear} stair_door_samples={stairDoorClearSamples}/{expectedStairDoorClearSamples} stair_door_supported={stairDoorSupported} stair_support_samples={stairDoorSupportSamples}/{expectedStairDoorSupportSamples} step_collision={stepCollision} step_hit={stepName} player_climbed={playerClimbedRamp} climb_height={climbHeight:0.00} hedges_solid={hedgesSolid} hedge_hits={hedgeCollisionCount}/{hedgeColliders.Count} rooftops={_residentialRoofAccessCount} civilians={ResidentialCivilianCount} special={ResidentialSpecialCivilianCount} roles={roles.Count} upper_floors={upperFloorPopulation}");
        if (!valid)
        {
            GD.PushError("Residential community validation failed.");
        }
        GD.Print($"RESIDENTIAL_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }

    private (int Count, int Expected, bool Chinese, Label3D? EnglishLeak) CheckResidentialLocalization()
    {
        var labelNodes = GetTree().GetNodesInGroup("residential_localized_labels");
        using var labelNodesBacking = labelNodes.AsDisposable();
        var labels = labelNodes
            .OfType<Label3D>()
            .Where(IsInstanceValid)
            .ToList();
        var expected = ResidentialTowerSpecs.Sum(spec => spec.Floors) * 4
            + ResidentialTowerSpecs.Length
            + 2
            + ResidentialCivilianCount
            + ResidentialRelayStationCount * 2;
        var chinese = labels.All(label =>
            label.Text.Any(character => character >= '\u3400' && character <= '\u9fff'));
        var englishLeak = labels.FirstOrDefault(label => ResidentialEnglishLabelTokens.Any(token =>
            label.Text.Contains(token, StringComparison.OrdinalIgnoreCase)));
        return (labels.Count, expected, chinese, englishLeak);
    }

    private async void ValidateResidentialLocalization()
    {
        foreach (var enemy in _enemies)
        {
            enemy.ProcessMode = ProcessModeEnum.Disabled;
        }
        SetLanguage("zh");
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

        var (count, expected, chinese, englishLeak) = CheckResidentialLocalization();
        var encounterKeys = new[]
        {
            "residential_room_trap",
            "residential_room_alarm",
            "residential_room_intel",
            "residential_room_guard_ambush"
        };
        var encountersLocalized = encounterKeys.All(key =>
            GameLocalization.Get(key, "zh", key).Any(character => character >= '\u3400' && character <= '\u9fff'));
        var valid = count == expected && chinese && englishLeak is null && encountersLocalized;
        GD.Print($"RESIDENTIAL_LOCALIZATION_CHECK valid={valid} labels={count}/{expected} chinese={chinese} no_english={englishLeak is null} encounters={encountersLocalized} leak={englishLeak?.Text ?? "none"}");
        GD.Print($"RESIDENTIAL_LOCALIZATION_PASS valid={valid}");
        QuitDiagnosticAfterSceneCleanup(valid ? 0 : 2);
    }

    private async void ValidateResidentialGameplay()
    {
        foreach (var enemy in _enemies)
        {
            enemy.ProcessMode = ProcessModeEnum.Disabled;
        }
        await WaitFrames(4);

        static string LootFingerprint(IEnumerable<LootItem> items) => string.Join(
            ";",
            items.Select(item => $"{item.Kind}:{item.Grade}:{item.Quantity}:{item.Weapon?.Platform}:{item.AttachmentId}:{item.Equipment?.DefinitionId}:{item.AmmoCaliber}:{item.MedicalKind}:{item.ValuableKind}"));

        static int RuntimeNodeCount(Node root)
        {
            var count = 1;
            var children = root.GetChildren();
            using var childrenBacking = children.AsDisposable();
            foreach (var child in children)
            {
                if (child is Node childNode)
                {
                    count += RuntimeNodeCount(childNode);
                }
            }
            return count;
        }

        static HashSet<Node> RuntimeNodes(Node root)
        {
            var nodes = new HashSet<Node> { root };
            var children = root.GetChildren();
            using var childrenBacking = children.AsDisposable();
            foreach (var child in children)
            {
                if (child is Node childNode)
                {
                    nodes.UnionWith(RuntimeNodes(childNode));
                }
            }
            return nodes;
        }

        var expectedCaches = ResidentialTowerSpecs.Sum(spec => spec.Floors * 4);
        var expectedFurniture = ResidentialTowerSpecs.Sum(spec => spec.Floors * 2);
        var cacheKinds = new HashSet<ResidentialCacheKind>();
        var cacheGrades = new HashSet<LootGrade>();
        var roomEvents = new HashSet<ResidentialRoomEventKind>();
        var lootKinds = new HashSet<LootItemKind>();
        var roomIds = new HashSet<ResidentialRoomId>();
        var expectedRoomIds = new HashSet<ResidentialRoomId>();
        for (var towerIndex = 0; towerIndex < ResidentialTowerSpecs.Length; towerIndex++)
        {
            for (var floor = 0; floor < ResidentialTowerSpecs[towerIndex].Floors; floor++)
            {
                foreach (var side in new[] { -1, 1 })
                {
                    expectedRoomIds.Add(new ResidentialRoomId(towerIndex, floor, side, ResidentialRoomZone.North));
                    expectedRoomIds.Add(new ResidentialRoomId(towerIndex, floor, side, ResidentialRoomZone.South));
                }
            }
        }

        var cachesRegistered = true;
        var cachesInitiallySealed = true;
        var cachesResolved = true;
        var deterministicLoot = true;
        var noReroll = true;
        var neutralVisuals = true;
        var noVisibleHints = true;
        var sealedWeaponHints = true;
        var visiblePartCount = -1;
        var reachableCaches = 0;
        var unreachableCaches = new List<string>();
        var damageRequests = 0;
        var noiseRequests = 0;
        var alertRequests = 0;
        var scanRequests = 0;
        var guardRequests = 0;
        var messageRequests = 0;
        var expectedDamageRequests = 0;
        var expectedNoiseRequests = 0;
        var expectedAlertRequests = 0;
        var expectedScanRequests = 0;
        var expectedGuardRequests = 0;
        var expectedMessageRequests = 0;
        var cacheNodesBeforeOpen = _residentialCaches.Sum(cache => RuntimeNodeCount(cache));
        var sceneNodesBeforeOpen = RuntimeNodeCount(this);
        var productionController = _residentialEncounterController;
        _residentialEncounterController = new ResidentialRoomEncounterController(new ResidentialEncounterEffects(
            (_, _) => damageRequests++,
            (_, _) => noiseRequests++,
            (_, _) => alertRequests++,
            (_, _) =>
            {
                scanRequests++;
                return 0;
            },
            (_, count) => guardRequests += count,
            (_, _, _, _) => messageRequests++));
        _residentialChestEventCount = 0;
        foreach (var cache in _residentialCaches)
        {
            cacheKinds.Add(cache.Kind);
            roomEvents.Add(cache.EventKind);
            cachesRegistered &= _lootSources.Contains(cache) && IsInstanceValid(cache);
            cachesInitiallySealed &= !cache.ContentsResolved
                && cache.Loot.Count == 0
                && cache.ResolutionCount == 0
                && cache.OpenEventCount == 0;
            sealedWeaponHints &= cache.MayContainWeapon;
            neutralVisuals &= cache.NeutralVisualReady;
            noVisibleHints &= !cache.HasVisibleLootHint;
            if (visiblePartCount < 0)
            {
                visiblePartCount = cache.VisibleModelPartCount;
            }
            neutralVisuals &= cache.VisibleModelPartCount == visiblePartCount;
            if (cache.RoomId is ResidentialRoomId roomId)
            {
                roomIds.Add(roomId);
            }
            else
            {
                cachesRegistered = false;
                continue;
            }
            if (HasClearLootInteractionApproach(cache))
            {
                reachableCaches++;
            }
            else
            {
                unreachableCaches.Add(cache.Name);
            }

            var expectedPlan = ResidentialRoomLootRules.Plan(roomId, cache.Archetype, _residentialLootSeedSalt);
            var expectedResolution = ResidentialRoomLootRules.Resolve(expectedPlan);
            cache.OnSearched();
            var firstFingerprint = LootFingerprint(cache.Loot);
            deterministicLoot &= cache.RevealedGrade == expectedResolution.Grade
                && firstFingerprint == LootFingerprint(expectedResolution.Items);
            cache.OnSearched();
            noReroll &= firstFingerprint == LootFingerprint(cache.Loot)
                && cache.ResolutionCount == 1
                && cache.OpenEventCount == 1;
            cachesResolved &= cache.ContentsResolved && cache.Loot.Count is >= 2 and <= 4;
            if (cache.RevealedGrade is LootGrade revealedGrade)
            {
                cacheGrades.Add(revealedGrade);
            }
            foreach (var item in cache.Loot)
            {
                lootKinds.Add(item.Kind);
            }
            if (cache.EventKind != ResidentialRoomEventKind.None)
            {
                expectedMessageRequests++;
            }
            switch (cache.EventKind)
            {
                case ResidentialRoomEventKind.BoobyTrap:
                    expectedDamageRequests++;
                    expectedNoiseRequests++;
                    expectedAlertRequests++;
                    break;
                case ResidentialRoomEventKind.Alarm:
                    expectedNoiseRequests++;
                    expectedAlertRequests++;
                    break;
                case ResidentialRoomEventKind.Intel:
                    expectedScanRequests++;
                    break;
                case ResidentialRoomEventKind.GuardAmbush:
                    expectedNoiseRequests++;
                    expectedGuardRequests += cache.GuardCount;
                    break;
            }
        }
        var openVisualDeadline = Time.GetTicksMsec() + 2500UL;
        while (_residentialCaches.Any(cache => !cache.OpenVisualReady)
            && Time.GetTicksMsec() < openVisualDeadline)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        var cacheNodesAfterOpen = _residentialCaches.Sum(cache => RuntimeNodeCount(cache));
        var sceneNodesAfterOpen = RuntimeNodeCount(this);
        var cacheNodesStable = cacheNodesAfterOpen == cacheNodesBeforeOpen;
        var sceneNodesStable = sceneNodesAfterOpen == sceneNodesBeforeOpen;
        var openedVisualsReady = _residentialCaches.All(cache => cache.OpenVisualReady);
        var openedFeedbackReady = _residentialCaches.All(cache => cache.OpenFeedbackReady);
        var openedNodeBudgetMet = sceneNodesAfterOpen < 40000;
        _residentialEncounterController = productionController;

        var guardProbeExclude = new Godot.Collections.Array<Rid> { _player.GetRid() };
        using var guardProbeExcludeBacking = guardProbeExclude.AsDisposable();
        ResidentialSupplyCache? realGuardCache = null;
        var realGuardTarget = Vector3.Zero;
        var realGuardExpectedPositions = new List<Vector3>();
        var guardGeometryFailures = new List<string>();
        var guardCacheRouteLeaks = new List<string>();
        var guardSpawnPointsSafe = 0;
        var guardRoutesReady = 0;
        var guardCachesReady = 0;
        var guardCacheClearancesBlocked = 0;
        var guardCacheRouteProbes = 0;
        var guardAmbushCaches = _residentialCaches
            .Where(cache => cache.EventKind == ResidentialRoomEventKind.GuardAmbush && cache.GuardCount > 0)
            .OrderByDescending(cache => cache.GuardCount)
            .ThenBy(cache => cache.Archetype == ResidentialRoomArchetype.FamilyApartment ? 0 : 1)
            .ThenBy(cache => cache.TowerIndex)
            .ThenBy(cache => cache.FloorIndex)
            .ThenBy(cache => cache.Name.ToString(), StringComparer.Ordinal)
            .ToList();
        var guardSpawnPointsChecked = guardAmbushCaches.Sum(cache => cache.GuardCount);
        foreach (var candidate in guardAmbushCaches)
        {
            using var guardPlanner = CreateResidentialGuardSpawnPlanner(candidate, guardProbeExclude);
            if (!guardPlanner.TryGroundPosition(candidate.GlobalPosition, out _))
            {
                guardCacheClearancesBlocked++;
            }
            var crossCacheStart = candidate.ToGlobal(new Vector3(-1.45f, 0.05f, 0.0f));
            var crossCacheEnd = candidate.ToGlobal(new Vector3(1.45f, 0.05f, 0.0f));
            if (guardPlanner.TryGroundPosition(crossCacheStart, out var groundedCrossStart)
                && guardPlanner.TryGroundPosition(crossCacheEnd, out var groundedCrossEnd))
            {
                guardCacheRouteProbes++;
                if (guardPlanner.HasRoute(groundedCrossStart, groundedCrossEnd))
                {
                    guardCacheRouteLeaks.Add(candidate.Name.ToString());
                }
            }
            var layoutReady = guardPlanner.TryFindLayout(candidate.GuardCount, out var layout);
            var selectedTarget = layoutReady ? layout.Target : Vector3.Zero;
            var expectedPositions = layoutReady
                ? layout.SpawnPositions.ToList()
                : new List<Vector3>();
            var safePositions = expectedPositions.Count(position =>
                guardPlanner.TryGroundPosition(position, out var grounded)
                && grounded.DistanceTo(position) <= 0.03f);
            guardSpawnPointsSafe += safePositions;
            var readyRoutes = expectedPositions.Count(start =>
                guardPlanner.HasRoute(start, selectedTarget));
            guardRoutesReady += readyRoutes;
            var cacheGeometryReady = layoutReady
                && expectedPositions.Count == candidate.GuardCount
                && safePositions == candidate.GuardCount
                && readyRoutes == candidate.GuardCount;
            if (!cacheGeometryReady)
            {
                guardGeometryFailures.Add(
                    $"{candidate.Name}:layout={layoutReady}:safe={safePositions}/{candidate.GuardCount}:route={readyRoutes}/{candidate.GuardCount}");
                continue;
            }

            guardCachesReady++;
            if (realGuardCache is null)
            {
                realGuardCache = candidate;
                realGuardTarget = selectedTarget;
                realGuardExpectedPositions = expectedPositions;
            }
        }
        var farPreferredTargetBounded = false;
        if (guardAmbushCaches.Count > 0)
        {
            var farTargetCache = guardAmbushCaches[0];
            using var farTargetPlanner = CreateResidentialGuardSpawnPlanner(farTargetCache, guardProbeExclude);
            var farTargetLayout = farTargetPlanner.Plan(
                farTargetCache.GuardCount,
                farTargetCache.GlobalPosition + Vector3.Right * 1000.0f);
            farPreferredTargetBounded = farTargetLayout.UsesResolvedGeometry
                && farTargetLayout.SpawnPositions.Length == farTargetCache.GuardCount
                && farTargetLayout.Target.DistanceTo(farTargetCache.GlobalPosition) <= 6.0f;
        }
        var guardAmbushGeometryReady = guardAmbushCaches.Count > 0
            && guardCachesReady == guardAmbushCaches.Count
            && guardSpawnPointsSafe == guardSpawnPointsChecked
            && guardRoutesReady == guardSpawnPointsChecked
            && guardCacheClearancesBlocked == guardAmbushCaches.Count
            && guardCacheRouteProbes > 0
            && guardCacheRouteLeaks.Count == 0
            && farPreferredTargetBounded;

        var realGuardExpected = realGuardCache?.GuardCount ?? 0;
        var realGuardSpawned = 0;
        var realGuardPositionsExact = false;
        var realGuardSpawnSafe = false;
        var realGuardRoutesReady = false;
        var realGuardGrounded = false;
        var realGuardsAlerted = false;
        var realGuardsArmed = false;
        var realGuardsFixedWeapon = false;
        var realGuardsTargetPlayer = false;
        var realGuardsUseProductionTargetEnumeration = false;
        var realGuardEnumerationReturnedPlayer = false;
        var realGuardsContactSharingSuppressed = false;
        var realGuardExistingEnemyTacticsPreserved = false;
        var realGuardsBallisticClear = false;
        var realGuardBallisticBlockers = "not_checked";
        var realGuardsMoved = false;
        var realGuardMinimumMovement = 0.0f;
        var realGuardsFired = false;
        var realGuardShotsFired = 0;
        var realGuardPlayerCollisionReady = false;
        var realGuardMissionStatePreserved = false;
        var realGuardMissionStateDetails = "not_checked";
        var realGuardsAttackReady = false;
        var realGuardGlassStateRestored = false;
        var realGuardsCleaned = false;
        var realGuardRemainingInstances = -1;
        var realGuardEnemyLeaks = -1;
        var realGuardLootLeaks = -1;
        var realGuardSceneNodesAfterCleanup = -1;
        var realGuardExtraNodes = "not_checked";
        if (realGuardCache is not null)
        {
            var enemiesBeforeGuardProbe = new HashSet<EnemyOperator>(_enemies);
            var enemyPursuitStatesBeforeGuardProbe = enemiesBeforeGuardProbe
                .Where(IsInstanceValid)
                .ToDictionary(
                    enemy => enemy,
                    enemy => enemy.CapturePursuitContactStateForDiagnostics());
            var sceneNodesBeforeGuardProbe = RuntimeNodeCount(this);
            var sceneNodeSetBeforeGuardProbe = RuntimeNodes(this);
            var glassStatesBeforeGuardProbe = _residentialGlassFields
                .Where(field => IsInstanceValid(field))
                .Select(field => (
                    Field: field,
                    Snapshot: field.CaptureStateForDiagnostics()))
                .ToList();
            var networkIdBeforeGuardProbe = _nextEnemyNetworkId;
            var ambushCountBeforeGuardProbe = _residentialGuardAmbushSpawnCount;
            var enemyCountBeforeGuardProbe = _enemiesRemaining;
            var killsBeforeGuardProbe = _kills;
            var playerPositionBeforeGuardProbe = _player.GlobalPosition;
            var playerVelocityBeforeGuardProbe = _player.Velocity;
            var playerProcessModeBeforeGuardProbe = _player.ProcessMode;
            var playerHealthBeforeGuardProbe = _player.Health;
            var playerArmorBeforeGuardProbe = _player.Armor;
            var playerHelmetDurabilityBeforeGuardProbe = _player.EquippedHelmet.Durability;
            var playerWasDeadBeforeGuardProbe = _player.IsDead;
            var playerUiLockedBeforeGuardProbe = _player.UiLocked;
            var playerReviveUsedBeforeGuardProbe = _player.ReviveUsed;
            var playerStanceBeforeGuardProbe = _player.Stance;
            var playerCollisionLayerBeforeGuardProbe = _player.CollisionLayer;
            var playerCollisionMaskBeforeGuardProbe = _player.CollisionMask;
            var playerWasProcessing = _player.IsProcessing();
            var playerWasPhysicsProcessing = _player.IsPhysicsProcessing();
            var squadStatesBeforeGuardProbe = _squadMates
                .Where(mate => IsInstanceValid(mate))
                .Select(mate => (
                    Mate: mate,
                    Position: mate.GlobalPosition,
                    Velocity: mate.Velocity,
                    ProcessMode: mate.ProcessMode,
                    Processing: mate.IsProcessing(),
                    PhysicsProcessing: mate.IsPhysicsProcessing()))
                .ToList();
            var externalActors = new HashSet<Node>();
            if (IsInstanceValid(_aircraft))
            {
                externalActors.Add(_aircraft!);
            }
            if (IsInstanceValid(_extractionAircraft))
            {
                externalActors.Add(_extractionAircraft!);
            }
            externalActors.UnionWith(_barrels.Where(barrel => IsInstanceValid(barrel)));
            externalActors.UnionWith(_vehicles.Where(vehicle => IsInstanceValid(vehicle)));
            externalActors.UnionWith(enemiesBeforeGuardProbe.Where(enemy => IsInstanceValid(enemy)));
            externalActors.UnionWith(_civilians.Where(civilian => IsInstanceValid(civilian)));
            externalActors.UnionWith(sceneNodeSetBeforeGuardProbe.Where(node => node is FragGrenade));
            var aircraftShellNodes = GetTree().GetNodesInGroup("aircraft_shells");
            using var aircraftShellNodesBacking = aircraftShellNodes.AsDisposable();
            foreach (var node in aircraftShellNodes)
            {
                if (node is Node shell && IsInstanceValid(shell))
                {
                    externalActors.Add(shell);
                }
            }
            var externalActorStatesBeforeGuardProbe = externalActors
                .Select(actor => (
                    Actor: actor,
                    ProcessMode: actor.ProcessMode,
                    Processing: actor.IsProcessing(),
                    PhysicsProcessing: actor.IsPhysicsProcessing()))
                .ToList();
            var worldWasProcessing = IsProcessing();
            var directorProcessModeBeforeGuardProbe = _missionDirector.ProcessMode;
            var directorPhaseBeforeGuardProbe = _missionDirector.CurrentPhase();
            var directorProtectionBeforeGuardProbe = _missionDirector.IsDeploymentProtected();
            var worldMissionPhaseBeforeGuardProbe = _missionPhase;
            var spawnedGuards = new List<EnemyOperator>();
            try
            {
                SetProcess(false);
                _missionDirector.ProcessMode = ProcessModeEnum.Disabled;
                foreach (var squadState in squadStatesBeforeGuardProbe)
                {
                    squadState.Mate.ProcessMode = ProcessModeEnum.Disabled;
                }
                foreach (var actorState in externalActorStatesBeforeGuardProbe)
                {
                    actorState.Actor.ProcessMode = ProcessModeEnum.Disabled;
                    actorState.Actor.SetProcess(false);
                    actorState.Actor.SetPhysicsProcess(false);
                }
                _player.ProcessMode = ProcessModeEnum.Inherit;
                _player.SetProcess(false);
                _player.SetPhysicsProcess(false);
                _player.CollisionLayer = 1;
                _player.CollisionMask = 1 | 2;
                _player.GlobalPosition = realGuardTarget;
                _player.Velocity = Vector3.Zero;
                _player.SetHealthForDiagnostics(_player.MaxHealth);
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                realGuardPlayerCollisionReady = _player.HasActiveLadderCollisionForDiagnostics;

                SpawnResidentialGuardAmbush(
                    realGuardCache,
                    realGuardExpected,
                    WeaponCatalog.Build(WeaponPlatform.M4A1, 0));
                spawnedGuards = _enemies
                    .Where(enemy => !enemiesBeforeGuardProbe.Contains(enemy))
                    .ToList();
                realGuardSpawned = spawnedGuards.Count;
                realGuardPositionsExact = realGuardSpawned == realGuardExpected
                    && spawnedGuards.Select((guard, index) =>
                            guard.GlobalPosition.DistanceTo(realGuardExpectedPositions[index]) <= 0.02f)
                        .All(exact => exact);
                using var realGuardPlanner = CreateResidentialGuardSpawnPlanner(
                    realGuardCache,
                    guardProbeExclude);
                realGuardSpawnSafe = realGuardExpectedPositions.All(position =>
                    realGuardPlanner.TryGroundPosition(position, out var grounded)
                    && grounded.DistanceTo(position) <= 0.03f);
                realGuardRoutesReady = realGuardExpectedPositions.All(position =>
                    realGuardPlanner.HasRoute(position, realGuardTarget));
                var guardInitialPositions = spawnedGuards
                    .Select(guard => guard.GlobalPosition)
                    .ToList();
                var guardMaximumMovements = new float[spawnedGuards.Count];
                var guardInitialShots = spawnedGuards
                    .Select(guard => guard.AttackShotsFired)
                    .ToList();
                var guardObservedShots = guardInitialShots.ToArray();
                var guardShotBallisticClear = new bool[spawnedGuards.Count];
                var guardBallisticDetails = spawnedGuards
                    .Select(guard => $"{guard.Name}:no_shot")
                    .ToArray();

                (bool Clear, string Detail) GuardBallisticAtCurrentPosition(EnemyOperator guard)
                {
                    if (!IsInstanceValid(guard))
                    {
                        return (false, "invalid");
                    }
                    var playerAimPoint = _player.HitPoint(HitRegion.Torso);
                    var origin = guard.ResolvedShotOriginForDiagnostics;
                    var direction = origin.DirectionTo(playerAimPoint);
                    var rayEnd = playerAimPoint + direction * 0.9f;
                    if (IsLineObscuredBySmoke(origin, playerAimPoint))
                    {
                        return (false, "smoke");
                    }

                    var clear = Ballistics.HasClearShot(
                        GetWorld3D(),
                        origin,
                        rayEnd,
                        _player,
                        guard.GetRid());
                    if (clear)
                    {
                        return (true, "none");
                    }

                    if (!PhysicsRaycast.TryHit(
                            GetWorld3D().DirectSpaceState,
                            origin,
                            rayEnd,
                            guard.GetRid(),
                            0xFFFFFFFF,
                            out var hit))
                    {
                        return (false, "no_target");
                    }
                    var collider = hit.Collider;
                    var blocker = collider is Node node
                        ? node.Name.ToString()
                        : collider?.GetType().Name ?? "unknown";
                    return (false, blocker);
                }

                for (var index = 0; index < spawnedGuards.Count; index++)
                {
                    var guard = spawnedGuards[index];
                    guard.MissionDirector = null;
                    guard.ConfigureCombatProbeForDiagnostics(
                        0x5EED_4100UL + (ulong)index,
                        realGuardTarget,
                        bypassPlayerProtection: true,
                        suppressContactSharing: true);
                }
                realGuardsUseProductionTargetEnumeration = spawnedGuards.Count == realGuardExpected
                    && spawnedGuards.All(guard => ReferenceEquals(guard.Main, this));
                realGuardEnumerationReturnedPlayer = realGuardsUseProductionTargetEnumeration
                    && spawnedGuards.All(guard => EnumerateHostileTargetsFor(guard)
                        .Any(candidate => ReferenceEquals(candidate, _player)));
                realGuardsContactSharingSuppressed = spawnedGuards.Count == realGuardExpected
                    && spawnedGuards.All(guard => guard.SuppressesContactSharingForDiagnostics);

                for (var frame = 0; frame < 90; frame++)
                {
                    await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                    _player.SetHealthForDiagnostics(_player.MaxHealth);
                    for (var index = 0; index < spawnedGuards.Count; index++)
                    {
                        var guard = spawnedGuards[index];
                        if (!IsInstanceValid(guard))
                        {
                            continue;
                        }
                        var start = guardInitialPositions[index];
                        var movement = new Vector2(
                            guard.GlobalPosition.X - start.X,
                            guard.GlobalPosition.Z - start.Z).Length();
                        guardMaximumMovements[index] = Mathf.Max(
                            guardMaximumMovements[index],
                            movement);
                        if (guard.AttackShotsFired <= guardObservedShots[index])
                        {
                            continue;
                        }
                        guardObservedShots[index] = guard.AttackShotsFired;
                        var ballistic = GuardBallisticAtCurrentPosition(guard);
                        guardShotBallisticClear[index] |= ballistic.Clear;
                        if (ballistic.Clear || !guardShotBallisticClear[index])
                        {
                            guardBallisticDetails[index] = $"{guard.Name}:{ballistic.Detail}";
                        }
                    }
                }
                realGuardMinimumMovement = guardMaximumMovements.Length > 0
                    ? guardMaximumMovements.Min()
                    : 0.0f;
                realGuardsMoved = guardMaximumMovements.Length == realGuardExpected
                    && guardMaximumMovements.All(distance => distance >= 0.12f);
                var guardShotDeltas = spawnedGuards.Select((guard, index) =>
                    guard.AttackShotsFired - guardInitialShots[index]).ToList();
                realGuardShotsFired = guardShotDeltas.Sum();
                realGuardsFired = guardShotDeltas.Count == realGuardExpected
                    && guardShotDeltas.All(shots => shots > 0);
                realGuardsBallisticClear = guardShotBallisticClear.Length == realGuardExpected
                    && guardShotBallisticClear.All(clear => clear);
                realGuardBallisticBlockers = string.Join(",", guardBallisticDetails);
                realGuardGrounded = spawnedGuards.All(guard =>
                    IsInstanceValid(guard) && guard.IsOnFloor());
                realGuardsAlerted = spawnedGuards.All(guard =>
                    IsInstanceValid(guard) && guard.Alerted && guard.Suspicion >= 99.0f);
                realGuardsArmed = spawnedGuards.All(guard =>
                    IsInstanceValid(guard) && guard.HasFireablePrimary);
                realGuardsFixedWeapon = spawnedGuards.All(guard =>
                    IsInstanceValid(guard) && guard.CarriedWeapon.Platform == WeaponPlatform.M4A1);
                realGuardsTargetPlayer = spawnedGuards.All(guard =>
                    IsInstanceValid(guard) && ReferenceEquals(guard.EngageTargetNode, _player));
                realGuardMissionStatePreserved = _missionDirector.CurrentPhase() == directorPhaseBeforeGuardProbe
                    && _missionDirector.IsDeploymentProtected() == directorProtectionBeforeGuardProbe
                    && _missionPhase == worldMissionPhaseBeforeGuardProbe;
                realGuardMissionStateDetails =
                    $"{directorPhaseBeforeGuardProbe}>{_missionDirector.CurrentPhase()}:"
                    + $"{directorProtectionBeforeGuardProbe}>{_missionDirector.IsDeploymentProtected()}:"
                    + $"{worldMissionPhaseBeforeGuardProbe}>{_missionPhase}";
                realGuardsAttackReady = realGuardsArmed
                    && realGuardsFixedWeapon
                    && realGuardsTargetPlayer
                    && realGuardsBallisticClear
                    && realGuardsMoved
                    && realGuardsFired;
            }
            finally
            {
                foreach (var guard in spawnedGuards)
                {
                    _enemies.Remove(guard);
                    _lootSources.Remove(guard);
                    if (!IsInstanceValid(guard))
                    {
                        continue;
                    }
                    guard.ProcessMode = ProcessModeEnum.Disabled;
                    guard.Eliminated -= OnEnemyEliminated;
                    guard.Free();
                }
                _nextEnemyNetworkId = networkIdBeforeGuardProbe;
                _residentialGuardAmbushSpawnCount = ambushCountBeforeGuardProbe;
                _enemiesRemaining = enemyCountBeforeGuardProbe;
                _kills = killsBeforeGuardProbe;
                _player.GlobalPosition = playerPositionBeforeGuardProbe;
                _player.Velocity = playerVelocityBeforeGuardProbe;
                _player.CollisionLayer = playerCollisionLayerBeforeGuardProbe;
                _player.CollisionMask = playerCollisionMaskBeforeGuardProbe;
                _player.ProcessMode = playerProcessModeBeforeGuardProbe;
                _player.SetProcess(playerWasProcessing);
                _player.SetPhysicsProcess(playerWasPhysicsProcessing);
                _player.SetHealthForDiagnostics(playerHealthBeforeGuardProbe);
                _player.SetArmorForDiagnostics(playerArmorBeforeGuardProbe);
                _player.EquippedHelmet.Durability = playerHelmetDurabilityBeforeGuardProbe;
                _player.IsDead = playerWasDeadBeforeGuardProbe;
                _player.UiLocked = playerUiLockedBeforeGuardProbe;
                _player.SetReviveUsedForDiagnostics(playerReviveUsedBeforeGuardProbe);
                _player.TrySetStance(playerStanceBeforeGuardProbe);
                foreach (var squadState in squadStatesBeforeGuardProbe)
                {
                    if (!IsInstanceValid(squadState.Mate))
                    {
                        continue;
                    }
                    squadState.Mate.GlobalPosition = squadState.Position;
                    squadState.Mate.Velocity = squadState.Velocity;
                    squadState.Mate.ProcessMode = squadState.ProcessMode;
                    squadState.Mate.SetProcess(squadState.Processing);
                    squadState.Mate.SetPhysicsProcess(squadState.PhysicsProcessing);
                }
                foreach (var glassState in glassStatesBeforeGuardProbe)
                {
                    if (IsInstanceValid(glassState.Field))
                    {
                        glassState.Field.RestoreStateForDiagnostics(glassState.Snapshot);
                    }
                }
                realGuardGlassStateRestored = glassStatesBeforeGuardProbe.Count == _residentialGlassFields.Count
                    && glassStatesBeforeGuardProbe.All(glassState =>
                        IsInstanceValid(glassState.Field)
                        && glassState.Field.MatchesStateForDiagnostics(glassState.Snapshot));
                realGuardExistingEnemyTacticsPreserved = enemyPursuitStatesBeforeGuardProbe.All(pair =>
                    IsInstanceValid(pair.Key)
                    && pair.Key.MatchesPursuitContactStateForDiagnostics(pair.Value));
                realGuardRemainingInstances = spawnedGuards.Count(guard => IsInstanceValid(guard));
                realGuardEnemyLeaks = spawnedGuards.Count(guard => _enemies.Contains(guard));
                realGuardLootLeaks = spawnedGuards.Count(guard => _lootSources.Contains(guard));
                realGuardSceneNodesAfterCleanup = RuntimeNodeCount(this);
                realGuardExtraNodes = string.Join(',', RuntimeNodes(this)
                    .Where(node => !sceneNodeSetBeforeGuardProbe.Contains(node))
                    .Take(24)
                    .Select(node => node.Name.ToString()));
                realGuardsCleaned = realGuardRemainingInstances == 0
                    && realGuardLootLeaks == 0
                    && enemiesBeforeGuardProbe.SetEquals(_enemies)
                    && realGuardSceneNodesAfterCleanup == sceneNodesBeforeGuardProbe;
                foreach (var actorState in externalActorStatesBeforeGuardProbe)
                {
                    if (!IsInstanceValid(actorState.Actor))
                    {
                        continue;
                    }
                    actorState.Actor.ProcessMode = actorState.ProcessMode;
                    actorState.Actor.SetProcess(actorState.Processing);
                    actorState.Actor.SetPhysicsProcess(actorState.PhysicsProcessing);
                }
                _missionDirector.ProcessMode = directorProcessModeBeforeGuardProbe;
                SetProcess(worldWasProcessing);
                if (IsInstanceValid(_hud))
                {
                    _hud.SetEnemyCount(_enemiesRemaining);
                }
            }
        }

        var everyTowerStocked = true;
        for (var towerIndex = 0; towerIndex < _residentialCacheCountByTower.Length; towerIndex++)
        {
            var spec = ResidentialTowerSpecs[towerIndex];
            var expectedTowerCaches = spec.Floors * 4;
            everyTowerStocked &= _residentialCacheCountByTower[towerIndex] == expectedTowerCaches;
        }
        var everyRoomStocked = roomIds.SetEquals(expectedRoomIds);
        var eventEffectsExact = damageRequests == expectedDamageRequests
            && noiseRequests == expectedNoiseRequests
            && alertRequests == expectedAlertRequests
            && scanRequests == expectedScanRequests
            && guardRequests == expectedGuardRequests
            && messageRequests == expectedMessageRequests
            && _residentialChestEventCount == expectedMessageRequests;

        var furnitureKinds = new HashSet<ResidentialFurnitureKind>();
        var furnitureRegistered = true;
        var furnitureStocked = true;
        var reachableFurniture = 0;
        var furnitureReachability = new Dictionary<ResidentialFurnitureKind, (int Total, int Reachable)>();
        foreach (var furniture in _residentialFurniture)
        {
            furnitureKinds.Add(furniture.Kind);
            furnitureRegistered &= _lootSources.Contains(furniture) && IsInstanceValid(furniture);
            furnitureStocked &= furniture.IsSearchable && furniture.Loot.Count >= 2 && furniture.SearchDuration >= 0.6f;
            var reachable = HasClearLootInteractionApproach(furniture);
            if (reachable)
            {
                reachableFurniture++;
            }
            furnitureReachability.TryGetValue(furniture.Kind, out var reachStats);
            furnitureReachability[furniture.Kind] = (reachStats.Total + 1, reachStats.Reachable + (reachable ? 1 : 0));
        }

        var lootUiOpened = false;
        if (_residentialCaches.Count > 0)
        {
            var firstCache = _residentialCaches[0];
            _player.GlobalPosition = firstCache.GlobalPosition + Vector3.Up * 0.2f + Vector3.Forward * 1.4f;
            OpenLoot(firstCache);
            lootUiOpened = _hud.IsLootVisible && ReferenceEquals(_openLootSource, firstCache);
            CloseLoot();
        }

        var assistanceRoles = new HashSet<CivilianRole>();
        _player.SetHealthForDiagnostics(35.0f);
        var healthBefore = _player.Health;
        foreach (var role in Enum.GetValues<CivilianRole>())
        {
            var civilian = _civilians.Find(candidate => candidate.Role == role && candidate.CanOfferAssistance);
            if (civilian is null)
            {
                continue;
            }
            if (role == CivilianRole.UtilityWorker && _vehicles.Count > 0)
            {
                var vehicle = _vehicles[0];
                vehicle.GlobalPosition = civilian.GlobalPosition + new Vector3(2.0f, 0.0f, 0.0f);
                vehicle.TakeDamage(70.0f, vehicle.GlobalPosition + Vector3.Up, this);
            }
            if (civilian.TryProvideAssistance(_player))
            {
                assistanceRoles.Add(role);
            }
        }
        var medicHealed = _player.Health > healthBefore;
        var realGuardValidation = guardAmbushGeometryReady
            && realGuardExpected > 0
            && realGuardSpawned == realGuardExpected
            && realGuardPositionsExact
            && realGuardSpawnSafe
            && realGuardRoutesReady
            && realGuardGrounded
            && realGuardsAlerted
            && realGuardsMoved
            && realGuardsFired
            && realGuardPlayerCollisionReady
            && realGuardMissionStatePreserved
            && realGuardsUseProductionTargetEnumeration
            && realGuardEnumerationReturnedPlayer
            && realGuardsContactSharingSuppressed
            && realGuardExistingEnemyTacticsPreserved
            && realGuardsAttackReady
            && realGuardGlassStateRestored
            && realGuardsCleaned;

        var valid = ResidentialCacheCount == expectedCaches
            && ResidentialFurnitureCount == expectedFurniture
            && everyTowerStocked
            && everyRoomStocked
            && _residentialRoomArchetypes.Count == Enum.GetValues<ResidentialRoomArchetype>().Length
            && cacheKinds.Count == Enum.GetValues<ResidentialCacheKind>().Length
            && cacheGrades.Count == Enum.GetValues<LootGrade>().Length
            && roomEvents.Count == Enum.GetValues<ResidentialRoomEventKind>().Length
            && lootKinds.Count >= 6
            && lootKinds.Contains(LootItemKind.Medical)
            && cachesRegistered
            && cachesInitiallySealed
            && cachesResolved
            && deterministicLoot
            && noReroll
            && neutralVisuals
            && openedVisualsReady
            && openedFeedbackReady
            && cacheNodesStable
            && sceneNodesStable
            && openedNodeBudgetMet
            && noVisibleHints
            && sealedWeaponHints
            && reachableCaches == expectedCaches
            && eventEffectsExact
            && realGuardValidation
            && furnitureKinds.Count == Enum.GetValues<ResidentialFurnitureKind>().Length
            && furnitureRegistered
            && furnitureStocked
            && reachableFurniture == expectedFurniture
            && lootUiOpened
            && assistanceRoles.Count == Enum.GetValues<CivilianRole>().Length
            && medicHealed;
        var furnitureReachabilityText = string.Join(",", furnitureReachability.Select(pair => $"{pair.Key}={pair.Value.Reachable}/{pair.Value.Total}"));
        var guardGeometryFailureText = guardGeometryFailures.Count == 0
            ? "none"
            : string.Join('|', guardGeometryFailures);
        var guardCacheRouteLeakText = guardCacheRouteLeaks.Count == 0
            ? "none"
            : string.Join('|', guardCacheRouteLeaks);
        GD.Print($"RESIDENTIAL_GAMEPLAY_CHECK valid={valid} room_types={_residentialRoomArchetypes.Count}/7 caches={ResidentialCacheCount}/{expectedCaches} unique_rooms={roomIds.Count}/{expectedRoomIds.Count} cache_reachable={reachableCaches}/{expectedCaches} unreachable={string.Join(',', unreachableCaches)} cache_types={cacheKinds.Count}/7 grades={cacheGrades.Count}/5 loot_types={lootKinds.Count} events={roomEvents.Count}/5 guards={guardRequests}/{expectedGuardRequests} event_effects={eventEffectsExact} guard_cache_geometry={guardCachesReady}/{guardAmbushCaches.Count} guard_spawn_geometry={guardSpawnPointsSafe}/{guardSpawnPointsChecked} guard_route_geometry={guardRoutesReady}/{guardSpawnPointsChecked} guard_cache_clearance={guardCacheClearancesBlocked}/{guardAmbushCaches.Count} guard_cross_cache={guardCacheRouteProbes}:{guardCacheRouteLeakText} guard_far_target={farPreferredTargetBounded} guard_geometry_failures={guardGeometryFailureText} real_guards={realGuardSpawned}/{realGuardExpected} guard_positions={realGuardPositionsExact} guard_safe={realGuardSpawnSafe} guard_routes={realGuardRoutesReady} guard_grounded={realGuardGrounded} guard_alerted={realGuardsAlerted} guard_armed={realGuardsArmed} guard_fixed_weapon={realGuardsFixedWeapon} guard_target={realGuardsTargetPlayer} guard_target_main={realGuardsUseProductionTargetEnumeration} guard_target_enumerated_player={realGuardEnumerationReturnedPlayer} guard_contact_share_suppressed={realGuardsContactSharingSuppressed} guard_enemy_tactics_preserved={realGuardExistingEnemyTacticsPreserved} guard_ballistic={realGuardsBallisticClear} guard_blockers={realGuardBallisticBlockers} guard_moved={realGuardsMoved} guard_move_min={realGuardMinimumMovement:0.00} guard_fired={realGuardsFired} guard_shots={realGuardShotsFired} guard_player_collision={realGuardPlayerCollisionReady} guard_mission_preserved={realGuardMissionStatePreserved} guard_mission_state={realGuardMissionStateDetails} guard_attack_ready={realGuardsAttackReady} guard_glass_restored={realGuardGlassStateRestored} guard_cleanup={realGuardsCleaned} guard_cleanup_instances={realGuardRemainingInstances} guard_cleanup_enemies={realGuardEnemyLeaks} guard_cleanup_loot={realGuardLootLeaks} guard_cleanup_nodes={sceneNodesBeforeOpen}->{realGuardSceneNodesAfterCleanup} guard_cleanup_extra={realGuardExtraNodes} every_tower={everyTowerStocked} every_room={everyRoomStocked} registered={cachesRegistered} sealed={cachesInitiallySealed} resolved={cachesResolved} deterministic={deterministicLoot} no_reroll={noReroll} neutral_visual={neutralVisuals} opened_visual={openedVisualsReady} open_feedback={openedFeedbackReady} visible_parts={visiblePartCount} cache_nodes={cacheNodesBeforeOpen}->{cacheNodesAfterOpen} cache_nodes_stable={cacheNodesStable} scene_nodes={sceneNodesBeforeOpen}->{sceneNodesAfterOpen} scene_nodes_stable={sceneNodesStable} opened_node_budget={openedNodeBudgetMet} no_hints={noVisibleHints} ai_hint={sealedWeaponHints} furniture={ResidentialFurnitureCount}/{expectedFurniture} furniture_types={furnitureKinds.Count}/4 furniture_registered={furnitureRegistered} furniture_stocked={furnitureStocked} furniture_reachable={reachableFurniture}/{expectedFurniture} furniture_by_kind={furnitureReachabilityText} loot_ui={lootUiOpened} assistance_roles={assistanceRoles.Count}/5 medic_healed={medicHealed}");
        GD.Print($"RESIDENTIAL_GAMEPLAY_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }

    private async void ValidateResidentialCover()
    {
        var shooter = _enemies.Find(enemy => IsInstanceValid(enemy) && !enemy.IsDead);
        if (shooter is null)
        {
            GD.Print("RESIDENTIAL_COVER_CHECK valid=False reason=missing_shooter");
            GD.Print("RESIDENTIAL_COVER_PASS valid=False");
            GetTree().Quit(2);
            return;
        }
        foreach (var enemy in _enemies)
        {
            enemy.ProcessMode = ProcessModeEnum.Disabled;
        }
        foreach (var mate in _squadMates)
        {
            mate.ProcessMode = ProcessModeEnum.Disabled;
        }
        foreach (var civilian in _civilians)
        {
            civilian.ProcessMode = ProcessModeEnum.Disabled;
        }
        // Keep the player's collision body registered so ballistic rays can hit it,
        // while disabling movement/input updates for deterministic positioning.
        _player.ProcessMode = ProcessModeEnum.Inherit;
        _player.SetProcess(false);
        _player.SetPhysicsProcess(false);
        _missionDirector.ExitDeploymentZone();
        shooter.GrantFireablePrimaryForDiagnostics();
        shooter.ResetTacticalStateForDiagnostics();
        shooter.ProcessMode = ProcessModeEnum.Disabled;
        await WaitFrames(3);

        string FirstShotHit(Vector3 from, Vector3 to)
        {
            if (!PhysicsRaycast.TryHit(
                    GetWorld3D().DirectSpaceState,
                    from,
                    to,
                    shooter.GetRid(),
                    0xFFFFFFFF,
                    out var hit))
            {
                return "none";
            }
            var node = hit.Collider as Node;
            return node?.Name.ToString() ?? "unknown";
        }

        var sampleCount = 0;
        var blockedSamples = 0;
        string? firstLeak = null;
        var sampleExclude = new Godot.Collections.Array<Rid> { shooter.GetRid(), _player.GetRid() };
        using var sampleExcludeBacking = sampleExclude.AsDisposable();
        for (var towerIndex = 0; towerIndex < ResidentialTowerSpecs.Length; towerIndex++)
        {
            var tower = _residentialTowers[towerIndex];
            var spec = ResidentialTowerSpecs[towerIndex];
            var halfWidth = spec.Footprint.X * 0.5f;
            var halfDepth = spec.Footprint.Y * 0.5f;
            var fractions = new[] { -0.4f, 0.0f, 0.4f };

            bool LinkDoorContains(int side, int floor, float z)
            {
                return _residentialLinkSlots.TryGetValue(towerIndex, out var sides)
                    && sides.TryGetValue(side, out var slot)
                    && slot.Floors.Contains(floor)
                    && Mathf.Abs(z - slot.DoorZ) < 2.05f;
            }

            void Sample(Vector3 localFrom, Vector3 localTo, string label)
            {
                sampleCount++;
                if (PhysicsRaycast.HasHit(
                        GetWorld3D().DirectSpaceState,
                        tower.ToGlobal(localFrom),
                        tower.ToGlobal(localTo),
                        sampleExclude,
                        1))
                {
                    blockedSamples++;
                }
                else
                {
                    firstLeak ??= $"T{towerIndex + 1:00}_{label}";
                }
            }

            for (var floor = 0; floor < spec.Floors; floor++)
            {
                var y = floor * ResidentialFloorHeight + 1.3f;
                foreach (var fraction in fractions)
                {
                    var x = spec.Footprint.X * fraction;
                    Sample(
                        new Vector3(x, y, -halfDepth - 1.4f),
                        new Vector3(x, y, -halfDepth + 0.65f),
                        $"F{floor + 1:00}_N_{fraction:0.0}");
                    if (floor > 0 || Mathf.Abs(fraction) > 0.01f)
                    {
                        Sample(
                            new Vector3(x, y, halfDepth + 1.4f),
                            new Vector3(x, y, halfDepth - 0.65f),
                            $"F{floor + 1:00}_S_{fraction:0.0}");
                    }

                    var z = spec.Footprint.Y * fraction;
                    if (!LinkDoorContains(1, floor, z))
                    {
                        Sample(
                            new Vector3(-halfWidth - 1.4f, y, z),
                            new Vector3(-halfWidth + 0.65f, y, z),
                            $"F{floor + 1:00}_W_{fraction:0.0}");
                    }
                    if (!LinkDoorContains(0, floor, z))
                    {
                        Sample(
                            new Vector3(halfWidth + 1.4f, y, z),
                            new Vector3(halfWidth - 0.65f, y, z),
                            $"F{floor + 1:00}_E_{fraction:0.0}");
                    }
                }
            }
        }

        // Reproduce the former exploit: the body stays outside while the long gun muzzle
        // extends through the 0.2 m north facade into the room.
        var testTower = _residentialTowers[0];
        var testSpec = ResidentialTowerSpecs[0];
        var northWall = -testSpec.Footprint.Y * 0.5f;
        _player.SetHealthForDiagnostics(100.0f);
        _player.GlobalPosition = testTower.ToGlobal(new Vector3(0, 0.18f, northWall + 1.6f));
        shooter.GlobalPosition = testTower.ToGlobal(new Vector3(0, 0.18f, northWall - 0.48f));
        shooter.LookAt(new Vector3(_player.GlobalPosition.X, shooter.GlobalPosition.Y, _player.GlobalPosition.Z), Vector3.Up);
        await WaitFrames(2);
        var wallAim = _player.HitPoint(HitRegion.Torso);
        var rawWallHit = FirstShotHit(shooter.RawMuzzlePositionForDiagnostics, wallAim);
        var safeWallHit = FirstShotHit(shooter.ResolvedShotOriginForDiagnostics, wallAim);
        var rawMuzzleClear = Ballistics.HasClearShot(
            GetWorld3D(),
            shooter.RawMuzzlePositionForDiagnostics,
            wallAim,
            _player,
            shooter.GetRid());
        var originClamped = shooter.RawMuzzlePositionForDiagnostics.DistanceTo(shooter.ResolvedShotOriginForDiagnostics) > 0.08f;
        var guardedBlocked = !shooter.HasClearBallisticPath(_player, wallAim);
        var wallHealthBefore = _player.Health;
        var wallArmorBefore = _player.Armor;
        _player.TakeCombatDamage(48.0f, wallAim, shooter);
        var wallNoDamage = Mathf.IsEqualApprox(_player.Health, wallHealthBefore)
            && Mathf.IsEqualApprox(_player.Armor, wallArmorBefore);

        // The same authoritative damage entry must still accept a genuinely open shot.
        var open = new Vector3(0.0f, 45.0f, -60.0f);
        shooter.GlobalPosition = open;
        _player.GlobalPosition = open + new Vector3(0.0f, 0.0f, 12.0f);
        shooter.LookAt(new Vector3(_player.GlobalPosition.X, shooter.GlobalPosition.Y, _player.GlobalPosition.Z), Vector3.Up);
        _player.SetHealthForDiagnostics(100.0f);
        await WaitFrames(2);
        var openAim = _player.HitPoint(HitRegion.Torso);
        var openHit = FirstShotHit(shooter.ResolvedShotOriginForDiagnostics, openAim);
        var openClear = shooter.HasClearBallisticPath(_player, openAim);
        var openHealthBefore = _player.Health;
        _player.TakeCombatDamage(24.0f, openAim, shooter);
        var openDamaged = _player.Health < openHealthBefore - 0.01f;

        var facadesBlocked = sampleCount >= 900 && blockedSamples == sampleCount;
        var valid = facadesBlocked
            && rawMuzzleClear
            && originClamped
            && guardedBlocked
            && wallNoDamage
            && openClear
            && openDamaged;
        GD.Print($"RESIDENTIAL_COVER_CHECK valid={valid} facades={blockedSamples}/{sampleCount} leak={firstLeak ?? "none"} raw_muzzle_leaked={rawMuzzleClear} raw_hit={rawWallHit} safe_hit={safeWallHit} origin_clamped={originClamped} guarded_blocked={guardedBlocked} wall_no_damage={wallNoDamage} open_clear={openClear} open_hit={openHit} open_damaged={openDamaged} player_layer={_player.CollisionLayer}");
        GD.Print($"RESIDENTIAL_COVER_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }

    private async void CaptureResidentialGameplay()
    {
        foreach (var enemy in _enemies)
        {
            enemy.ProcessMode = ProcessModeEnum.Disabled;
        }
        foreach (var mate in _squadMates)
        {
            mate.ProcessMode = ProcessModeEnum.Disabled;
        }
        foreach (var civilian in _civilians)
        {
            civilian.ProcessMode = ProcessModeEnum.Disabled;
        }
        _player.ProcessMode = ProcessModeEnum.Disabled;
        _player.Visible = false;
        _hud.Visible = false;

        var camera = new Camera3D
        {
            Name = "ResidentialGameplayCamera",
            Fov = 72.0f,
            Far = 420.0f
        };
        AddChild(camera);
        camera.MakeCurrent();

        var clinicTower = _residentialTowers[0];
        var openedCaptureCache = _residentialCaches.FirstOrDefault(cache =>
                cache.EventKind == ResidentialRoomEventKind.None
                && cache.Archetype == ResidentialRoomArchetype.MedicalClinic)
            ?? _residentialCaches.First(cache => cache.EventKind == ResidentialRoomEventKind.None);
        openedCaptureCache.OnSearched();
        camera.Fov = 58.0f;
        camera.GlobalPosition = openedCaptureCache.ToGlobal(new Vector3(-2.4f, 1.45f, -3.1f));
        camera.LookAt(openedCaptureCache.GlobalPosition + Vector3.Up * 0.55f, Vector3.Up);
        var openCaptureDeadline = Time.GetTicksMsec() + 2500UL;
        while (!openedCaptureCache.OpenVisualReady && Time.GetTicksMsec() < openCaptureDeadline)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        SaveViewportImage("res://residential_clinic_validation.png");

        const int shelterFloor = 4;
        var shelterY = shelterFloor * ResidentialFloorHeight;
        camera.Fov = 72.0f;
        camera.GlobalPosition = clinicTower.ToGlobal(new Vector3(3.7f, shelterY + 1.55f, -5.0f));
        camera.LookAt(clinicTower.ToGlobal(new Vector3(7.2f, shelterY + 0.95f, -10.5f)), Vector3.Up);
        await WaitFrames(20);
        SaveViewportImage("res://residential_shelter_validation.png");

        var securityTower = _residentialTowers[2];
        camera.GlobalPosition = securityTower.ToGlobal(new Vector3(3.7f, 1.55f, -2.5f));
        camera.LookAt(securityTower.ToGlobal(new Vector3(10.5f, 1.05f, -5.5f)), Vector3.Up);
        await WaitFrames(20);
        SaveViewportImage("res://residential_security_validation.png");
        GD.Print($"RESIDENTIAL_GAMEPLAY_CAPTURE caches={ResidentialCacheCount} room_types={_residentialRoomArchetypes.Count} opened_cache={openedCaptureCache.Name} opened_visual={openedCaptureCache.OpenVisualReady} paths=residential_clinic_validation.png,residential_shelter_validation.png,residential_security_validation.png");
        GetTree().Quit();
    }

    private async void CaptureResidentialCommunity()
    {
        foreach (var enemy in _enemies)
        {
            enemy.ProcessMode = ProcessModeEnum.Disabled;
        }
        foreach (var mate in _squadMates)
        {
            mate.ProcessMode = ProcessModeEnum.Disabled;
        }
        foreach (var civilian in _civilians)
        {
            civilian.ProcessMode = ProcessModeEnum.Disabled;
        }
        _player.ProcessMode = ProcessModeEnum.Disabled;
        _hud.Visible = false;
        var camera = new Camera3D
        {
            Name = "ResidentialValidationCamera",
            Fov = 60.0f,
            Far = 620.0f
        };
        AddChild(camera);
        camera.GlobalPosition = new Vector3(0, 49.0f, 18.0f);
        camera.LookAt(new Vector3(12.0f, 13.0f, 76.0f), Vector3.Up);
        camera.MakeCurrent();
        await WaitFrames(32);
        SaveViewportImage("res://residential_exterior_validation.png");

        const int towerIndex = 8;
        var tower = _residentialTowers[towerIndex];
        var spec = ResidentialTowerSpecs[towerIndex];
        var coreZ = -Mathf.Min(spec.Footprint.Y * 0.18f, 3.6f);
        camera.GlobalPosition = tower.ToGlobal(new Vector3(0, 1.62f, spec.Footprint.Y * 0.5f - 2.4f));
        camera.LookAt(tower.ToGlobal(new Vector3(0, 1.38f, coreZ)), Vector3.Up);
        camera.Fov = 69.0f;
        await WaitFrames(20);
        SaveViewportImage("res://residential_interior_validation.png");

        var occupiedFloor = spec.Floors / 2;
        var occupiedSouthStart = coreZ + ResidentialStairOpeningSouthDepth + 0.55f;
        var occupiedSouthEnd = spec.Footprint.Y * 0.5f - 0.35f;
        var occupiedDoorZ = Mathf.Lerp(occupiedSouthStart, occupiedSouthEnd, 0.58f);
        var capturedOccupant = _civilians.Find(civilian => civilian.TowerIndex == towerIndex
            && civilian.FloorIndex == occupiedFloor
            && civilian.Role == CivilianRole.VolunteerMedic);
        if (capturedOccupant is not null)
        {
            capturedOccupant.GlobalPosition = tower.ToGlobal(new Vector3(
                4.35f,
                occupiedFloor * ResidentialFloorHeight + 0.14f,
                occupiedDoorZ));
            capturedOccupant.LookAt(
                tower.ToGlobal(new Vector3(0, occupiedFloor * ResidentialFloorHeight + 0.14f, occupiedDoorZ)),
                Vector3.Up);
        }
        camera.GlobalPosition = tower.ToGlobal(new Vector3(0, occupiedFloor * ResidentialFloorHeight + 1.62f, occupiedDoorZ));
        camera.LookAt(tower.ToGlobal(new Vector3(spec.Footprint.X * 0.27f, occupiedFloor * ResidentialFloorHeight + 1.25f, spec.Footprint.Y * 0.24f)), Vector3.Up);
        camera.Fov = 72.0f;
        await WaitFrames(20);
        SaveViewportImage("res://residential_occupants_validation.png");

        var roofY = spec.Floors * ResidentialFloorHeight;
        camera.GlobalPosition = tower.ToGlobal(new Vector3(0, roofY + 7.5f, spec.Footprint.Y * 0.72f));
        camera.LookAt(tower.ToGlobal(new Vector3(0, roofY + 0.6f, 0)), Vector3.Up);
        camera.Fov = 62.0f;
        await WaitFrames(20);
        SaveViewportImage("res://residential_rooftop_validation.png");
        GD.Print($"RESIDENTIAL_CAPTURE towers={ResidentialTowerCount} floors={_residentialFloorCount} civilians={ResidentialCivilianCount} paths=residential_exterior_validation.png,residential_interior_validation.png,residential_occupants_validation.png,residential_rooftop_validation.png");
        GetTree().Quit();
    }

    private async void CaptureResidentialSkyLinks()
    {
        foreach (var enemy in _enemies)
        {
            enemy.ProcessMode = ProcessModeEnum.Disabled;
        }
        foreach (var mate in _squadMates)
        {
            mate.ProcessMode = ProcessModeEnum.Disabled;
        }
        foreach (var civilian in _civilians)
        {
            civilian.ProcessMode = ProcessModeEnum.Disabled;
        }
        _player.ProcessMode = ProcessModeEnum.Disabled;
        _player.Visible = false;
        _hud.Visible = false;

        var link = ResidentialSkyLinks[5];
        const int floor = 2;
        var floorY = floor * ResidentialFloorHeight;
        var specA = ResidentialTowerSpecs[link.From];
        var specB = ResidentialTowerSpecs[link.To];
        var towerA = _residentialTowers[link.From];
        var towerB = _residentialTowers[link.To];
        var sideA = ResidentialLinkSide(specA, specB);
        var sideB = ResidentialLinkSide(specB, specA);
        var doorZA = _residentialLinkSlots[link.From][sideA].DoorZ;
        var doorZB = _residentialLinkSlots[link.To][sideB].DoorZ;
        var worldA = towerA.ToGlobal(ResidentialLinkAnchor(specA, sideA, floorY, doorZA));
        var worldB = towerB.ToGlobal(ResidentialLinkAnchor(specB, sideB, floorY, doorZB));
        var direction = worldA.DirectionTo(worldB);
        direction.Y = 0.0f;
        direction = direction.Normalized();
        var lateral = new Vector3(direction.Z, 0.0f, -direction.X);
        var midpoint = (worldA + worldB) * 0.5f;

        var camera = new Camera3D
        {
            Name = "SkylinkValidationCamera",
            Fov = 67.0f,
            Far = 620.0f
        };
        AddChild(camera);
        camera.GlobalPosition = worldA + direction * 4.0f + Vector3.Up * 1.58f;
        camera.LookAt(worldB + Vector3.Up * 1.42f, Vector3.Up);
        camera.MakeCurrent();
        await WaitFrames(24);
        SaveViewportImage("res://skylink_interior_validation.png");

        camera.GlobalPosition = midpoint + lateral * 24.0f + Vector3.Up * 13.5f;
        camera.LookAt(midpoint + Vector3.Up * 1.4f, Vector3.Up);
        camera.Fov = 61.0f;
        await WaitFrames(24);
        SaveViewportImage("res://skylink_exterior_validation.png");
        GD.Print($"SKYLINK_CAPTURE bridges={_residentialSkybridgeCount} windows={_residentialSkybridgeWindowCount} frames={_residentialSkybridgeFrameCount} marksmen={_residentialSkybridgeMarksmanCount} paths=skylink_interior_validation.png,skylink_exterior_validation.png");
        GetTree().Quit();
    }

    private static Vector3 ResidentialLinkAnchor(ResidentialTowerSpec spec, int side, float floorY, float doorZ)
    {
        const float inset = 0.4f;
        return side switch
        {
            0 => new Vector3(spec.Footprint.X * 0.5f - inset, floorY + 0.05f, doorZ),
            1 => new Vector3(-spec.Footprint.X * 0.5f + inset, floorY + 0.05f, doorZ),
            2 => new Vector3(0, floorY + 0.05f, spec.Footprint.Y * 0.5f - inset),
            _ => new Vector3(0, floorY + 0.05f, -spec.Footprint.Y * 0.5f + inset)
        };
    }

    private void BuildResidentialSkyLinks(
        Node3D community,
        Godot.Material concrete,
        Godot.Material steel,
        Godot.Material glass)
    {
        var frame = Mat("residential_skybridge_frame", new Color(0.24f, 0.3f, 0.31f), 0.7f, 0.34f);
        var sill = Mat("residential_skybridge_sill", new Color(0.37f, 0.42f, 0.42f), 0.18f, 0.68f);
        var deck = Mat("residential_skybridge_deck", new Color(0.25f, 0.27f, 0.27f), 0.14f, 0.76f);
        var window = Mat("residential_skybridge_window", new Color(0.055f, 0.24f, 0.29f, 0.28f), 0.16f, 0.08f);
        window.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        window.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        window.VertexColorUseAsAlbedo = true;
        var glow = Mat("residential_skybridge_light", new Color(0.82f, 0.92f, 0.91f), 0.08f, 0.28f, new Color(0.32f, 0.88f, 0.92f));
        foreach (var link in ResidentialSkyLinks)
        {
            var towerA = _residentialTowers[link.From];
            var towerB = _residentialTowers[link.To];
            var specA = ResidentialTowerSpecs[link.From];
            var specB = ResidentialTowerSpecs[link.To];
            var sideA = ResidentialLinkSide(specA, specB);
            var sideB = ResidentialLinkSide(specB, specA);
            var doorZA = _residentialLinkSlots[link.From][sideA].DoorZ;
            var doorZB = _residentialLinkSlots[link.To][sideB].DoorZ;
            foreach (var floor in link.Floors)
            {
                var floorY = floor * ResidentialFloorHeight;
                var worldA = towerA.ToGlobal(ResidentialLinkAnchor(specA, sideA, floorY, doorZA));
                var worldB = towerB.ToGlobal(ResidentialLinkAnchor(specB, sideB, floorY, doorZB));
                var delta = worldB - worldA;
                delta.Y = 0.0f;
                var length = delta.Length();
                var bridge = new Node3D
                {
                    Name = $"Skybridge_{link.From}_{link.To}_F{floor}",
                    Position = worldA,
                    Rotation = new Vector3(0, Mathf.Atan2(delta.X, delta.Z), 0)
                };
                community.AddChild(bridge);
                var bridgeGlass = new BreakableGlassField { Name = "SkybridgeBreakableGlass" };
                bridge.AddChild(bridgeGlass);
                bridgeGlass.Configure(window, frame, null, 135.0f);
                _residentialGlassFields.Add(bridgeGlass);
                var span = length + 1.4f;
                var mid = length * 0.5f;
                var windowSpan = Mathf.Max(1.0f, length - 1.1f);
                var startAccessSill = floor == 2
                    && HasResidentialSkybridgeAccessEndpoint(link.From, link.To)
                    ? ResidentialSkybridgeAccessSillSide(link.From, link.To, delta.Normalized(), doorZA)
                    : 0;
                var endAccessSill = floor == 2
                    && HasResidentialSkybridgeAccessEndpoint(link.To, link.From)
                    ? ResidentialSkybridgeAccessSillSide(link.To, link.From, delta.Normalized(), doorZB)
                    : 0;
                const float accessOpening = 4.0f;
                var westStart = startAccessSill == -1 ? accessOpening : 0.55f;
                var westEnd = length - (endAccessSill == -1 ? accessOpening : 0.55f);
                var eastStart = startAccessSill == 1 ? accessOpening : 0.55f;
                var eastEnd = length - (endAccessSill == 1 ? accessOpening : 0.55f);
                var westSpan = Mathf.Max(1.0f, westEnd - westStart);
                var eastSpan = Mathf.Max(1.0f, eastEnd - eastStart);
                var westMid = (westStart + westEnd) * 0.5f;
                var eastMid = (eastStart + eastEnd) * 0.5f;
                ExpansionBox(bridge, "SkybridgeDeck", new Vector3(0, 0.05f, mid), new Vector3(3.5f, 0.16f, span), deck);
                ExpansionBox(bridge, "SkybridgeSillW", new Vector3(-1.69f, 0.39f, westMid), new Vector3(0.14f, 0.68f, westSpan), sill);
                ExpansionBox(bridge, "SkybridgeSillE", new Vector3(1.69f, 0.39f, eastMid), new Vector3(0.14f, 0.68f, eastSpan), sill);

                var bridgeTint = new Color(0.72f, 0.94f, 0.97f, 0.9f);
                bridgeGlass.AddPane(new Vector3(-1.69f, 1.76f, westMid), new Vector3(0.045f, 2.08f, westSpan), bridgeTint);
                bridgeGlass.AddPane(new Vector3(1.69f, 1.76f, eastMid), new Vector3(0.045f, 2.08f, eastSpan), bridgeTint);
                bridgeGlass.AddPane(new Vector3(0, 2.91f, mid), new Vector3(3.22f, 0.045f, windowSpan), bridgeTint);
                bridgeGlass.Commit();
                _residentialSkybridgeWindowCount += 3;

                MeshBox(bridge, new Vector3(-1.69f, 0.77f, westMid), new Vector3(0.16f, 0.12f, westSpan), frame).Name = "SkybridgeLowerRailW";
                MeshBox(bridge, new Vector3(1.69f, 0.77f, eastMid), new Vector3(0.16f, 0.12f, eastSpan), frame).Name = "SkybridgeLowerRailE";
                MeshBox(bridge, new Vector3(-1.58f, 2.89f, mid), new Vector3(0.16f, 0.16f, span), frame).Name = "SkybridgeRoofRailW";
                MeshBox(bridge, new Vector3(1.58f, 2.89f, mid), new Vector3(0.16f, 0.16f, span), frame).Name = "SkybridgeRoofRailE";
                MeshBox(bridge, new Vector3(0, 3.0f, mid), new Vector3(0.18f, 0.18f, span), frame).Name = "SkybridgeRoofSpine";

                var frameIndex = 0;
                for (var z = 0.65f; z < length - 0.35f; z += 7.5f)
                {
                    MeshBox(bridge, new Vector3(-1.69f, 1.8f, z), new Vector3(0.15f, 2.28f, 0.15f), frame).Name = $"SkybridgeRibW_{frameIndex}";
                    MeshBox(bridge, new Vector3(1.69f, 1.8f, z), new Vector3(0.15f, 2.28f, 0.15f), frame).Name = $"SkybridgeRibE_{frameIndex}";
                    MeshBox(bridge, new Vector3(0, 2.91f, z), new Vector3(3.45f, 0.15f, 0.18f), frame).Name = $"SkybridgeRibRoof_{frameIndex}";
                    if (z + 7.1f < length)
                    {
                        MeshBox(bridge, new Vector3(-1.7f, 1.79f, z + 3.55f), new Vector3(0.07f, 0.09f, 7.42f), frame, new Vector3(0.29f, 0, 0)).Name = $"SkybridgeBraceW_{frameIndex}";
                        MeshBox(bridge, new Vector3(1.7f, 1.79f, z + 3.55f), new Vector3(0.07f, 0.09f, 7.42f), frame, new Vector3(-0.29f, 0, 0)).Name = $"SkybridgeBraceE_{frameIndex}";
                    }
                    _residentialSkybridgeFrameCount++;
                    frameIndex++;
                }
                for (var z = 3.8f; z < length - 1.2f; z += 8.0f)
                {
                    MeshBox(bridge, new Vector3(0, 2.78f, z), new Vector3(1.65f, 0.045f, 0.28f), glow).Name = $"SkybridgeLight_{z:0}";
                }

                foreach (var sightlineX in new[] { -0.82f, 0.82f })
                {
                    _residentialSkybridgeSightlines.Add(new ResidentialSkybridgeSightline(
                        _residentialSkybridgeCount,
                        bridge.ToGlobal(new Vector3(sightlineX, 1.52f, 2.2f)),
                        bridge.ToGlobal(new Vector3(sightlineX, 1.52f, length - 2.2f))));
                }
                if (floor == 2 && length > 44.0f && _residentialSniperPosts.Count < 6)
                {
                    var fromEnd = _residentialSniperPosts.Count % 2 == 0;
                    var postZ = length * (fromEnd ? 0.28f : 0.72f);
                    var postX = fromEnd ? -0.68f : 0.68f;
                    var facingZ = fromEnd ? length - 1.8f : 1.8f;
                    _residentialSniperPosts.Add(new ResidentialSniperPost(
                        bridge.ToGlobal(new Vector3(postX, 0.2f, postZ)),
                        bridge.ToGlobal(new Vector3(-postX, 0.2f, facingZ))));
                }
                _residentialSkybridgeCount++;
            }
        }
    }
}
