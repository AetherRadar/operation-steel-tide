using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private enum ResidentialRoomArchetype
    {
        FamilyApartment,
        MedicalClinic,
        EvacuationShelter,
        MaintenanceWorkshop,
        CommunitySecurity,
        SmugglerDen,
        CommunityKitchen
    }

    private const float ResidentialFloorHeight = 3.15f;
    private const float ResidentialStairRun = 5.4f;
    private const float ResidentialStairOpeningWidth = 5.6f;
    private const float ResidentialStairOpeningNorthDepth = 5.45f;
    private const float ResidentialStairOpeningSouthDepth = 3.25f;
    private const int ResidentialStepsPerFlight = 16;
    private const float ResidentialStairTreadWidth = 1.95f;
    private const float ResidentialStairTreadThickness = 0.14f;

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
    private readonly List<BreakableGlassField> _residentialGlassFields = new();
    private readonly HashSet<ResidentialRoomArchetype> _residentialRoomArchetypes = new();
    private readonly int[] _residentialCacheCountByTower = new int[ResidentialTowerSpecs.Length];
    private int _residentialFloorCount;
    private int _residentialStairFlightCount;
    private int _residentialRoofAccessCount;
    private int _residentialSkybridgeCount;
    private int _residentialSkybridgeWindowCount;
    private int _residentialSkybridgeFrameCount;
    private int _residentialSkybridgeMarksmanCount;
    private int _residentialInfillModuleCount;
    private int _residentialStairDetailCount;

    public int ResidentialTowerCount => _residentialTowers.Count;
    public int ResidentialCivilianCount => _civilians.Count;
    public int ResidentialSpecialCivilianCount => _civilians.FindAll(civilian => civilian.IsSpecial).Count;
    public int ResidentialCacheCount => _residentialCaches.Count;
    public int ResidentialInfillModuleCount => _residentialInfillModuleCount;
    public int ResidentialStairDetailCount => _residentialStairDetailCount;
    public int ResidentialGlassPaneCount => _residentialGlassFields.Sum(field => field.PaneCount);
    public int ResidentialBrokenGlassCount => _residentialGlassFields.Sum(field => field.ShatteredCount);

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
        _residentialGlassFields.Clear();
        _residentialRoomArchetypes.Clear();
        Array.Clear(_residentialCacheCountByTower, 0, _residentialCacheCountByTower.Length);
        _residentialSkybridgeCount = 0;
        _residentialSkybridgeWindowCount = 0;
        _residentialSkybridgeFrameCount = 0;
        _residentialSkybridgeMarksmanCount = 0;
        _residentialInfillModuleCount = 0;
        _residentialStairDetailCount = 0;
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
        for (var index = 0; index < ResidentialTowerSpecs.Length; index++)
        {
            BuildResidentialTower(community, ResidentialTowerSpecs[index], index, concrete, steel, glass, trim);
        }
        BuildResidentialGapInfill(community, concrete, steel, glass, trim);
        BuildResidentialSkyLinks(community, concrete, steel, glass);
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
            community.AddChild(new Label3D
            {
                Position = new Vector3(arch.X, 5.3f, arch.Z),
                Text = arch.Name,
                FontSize = 26,
                OutlineSize = 7,
                Modulate = new Color(0.9f, 0.7f, 0.4f),
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                VisibilityRangeEnd = 70.0f
            });
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
            BuildTowerFloorSlab(tower, spec, floorY, stairCoreZ, interiorFloor, floor == 0);
            var westSlot = linkSlots is not null && linkSlots.TryGetValue(1, out var westLink) && westLink.Floors.Contains(floor) ? westLink : null;
            var eastSlot = linkSlots is not null && linkSlots.TryGetValue(0, out var eastLink) && eastLink.Floors.Contains(floor) ? eastLink : null;
            BuildTowerFloorShell(tower, spec, floor, floorY, facade, glassField, spec.Accent, westSlot, eastSlot);
            BuildTowerInterior(tower, spec, index, floor, floorY, stairCoreZ, interiorWall, wood, bedding, warmLight, westSlot, eastSlot);
            BuildTowerStairs(tower, floor, floorY, stairCoreZ, stair, warmLight);
            BuildTowerStairDetails(tower, spec, index, floor, floorY, stairCoreZ, trim, warmLight);
            _residentialFloorCount++;
        }
        glassField.Commit();
        BuildTowerFacadeDetails(tower, spec, index, spec.Accent);
        BuildTowerRoof(tower, spec, stairCoreZ, facade, steel, trim, warmLight);
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
        ExpansionBox(tower, "ResidentialFloorSlab_W", new Vector3(-(openingWidth + sideWidth) * 0.5f, floorY + 0.05f, 0), new Vector3(sideWidth, 0.12f, depth), material);
        ExpansionBox(tower, "ResidentialFloorSlab_E", new Vector3((openingWidth + sideWidth) * 0.5f, floorY + 0.05f, 0), new Vector3(sideWidth, 0.12f, depth), material);
        var northDepth = Mathf.Max(0.5f, openingNorth - northEdge);
        var southDepth = Mathf.Max(0.5f, southEdge - openingSouth);
        ExpansionBox(tower, "ResidentialFloorSlab_N", new Vector3(0, floorY + 0.05f, northEdge + northDepth * 0.5f), new Vector3(openingWidth, 0.12f, northDepth), material);
        ExpansionBox(tower, "ResidentialFloorSlab_S", new Vector3(0, floorY + 0.05f, openingSouth + southDepth * 0.5f), new Vector3(openingWidth, 0.12f, southDepth), material);
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

        var windowTint = floor % 3 == 0
            ? new Color(0.78f, 0.92f, 0.94f, 0.92f)
            : new Color(0.58f, 0.76f, 0.8f, 0.84f);
        if (floor % 4 == 0)
        {
            windowTint = windowTint.Lerp(new Color(accent.R, accent.G, accent.B, windowTint.A), 0.16f);
        }
        var windowY = floorY + 1.66f;
        for (var x = -width * 0.5f + 2.1f; x <= width * 0.5f - 2.0f; x += 3.6f)
        {
            glassField.AddPane(new Vector3(x, windowY, -depth * 0.5f - 0.105f), new Vector3(2.05f, 1.28f, 0.035f), windowTint);
            if (floor > 0 || Mathf.Abs(x) > 2.1f)
            {
                glassField.AddPane(new Vector3(x, windowY, depth * 0.5f + 0.105f), new Vector3(2.05f, 1.28f, 0.035f), windowTint);
            }
        }
        for (var z = -depth * 0.5f + 2.1f; z <= depth * 0.5f - 2.0f; z += 3.6f)
        {
            glassField.AddPane(new Vector3(-width * 0.5f - 0.105f, windowY, z), new Vector3(0.035f, 1.28f, 2.05f), windowTint);
            glassField.AddPane(new Vector3(width * 0.5f + 0.105f, windowY, z), new Vector3(0.035f, 1.28f, 2.05f), windowTint);
        }
        if (floor > 0 && floor % 3 == 0)
        {
            ExpansionBox(tower, "ResidentialBalcony", new Vector3(0, floorY + 0.18f, depth * 0.5f + 0.85f), new Vector3(Mathf.Min(8.0f, width * 0.48f), 0.14f, 1.7f), facade);
        }
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
        const float partitionHeight = 2.85f;
        var wallY = floorY + 0.1f + partitionHeight * 0.5f;
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
        var screen = Mat("residential_screen", new Color(0.08f, 0.14f, 0.2f), 0.2f, 0.25f, new Color(0.12f, 0.35f, 0.55f));
        var table = Mat("residential_table", new Color(0.24f, 0.16f, 0.1f), 0.05f, 0.78f);
        var featuredFloor = ShouldSpawnResidentialCache(spec, floor);
        foreach (var side in new[] { -1.0f, 1.0f })
        {
            var roomX = side * (corridorHalfWidth + roomWidth * 0.5f);
            var archetype = featuredFloor
                ? ResidentialRoomArchetypeFor(towerIndex, floor, side)
                : ResidentialRoomArchetype.FamilyApartment;
            _residentialRoomArchetypes.Add(archetype);
            ExpansionBox(tower, "ApartmentDivider", new Vector3(roomX, wallY, depth * 0.08f), new Vector3(roomWidth * 0.92f, partitionHeight, 0.1f), wall);
            // Living / bedroom set
            var livingZ = depth * 0.28f;
            var bedZ = -depth * 0.28f;
            MeshBox(tower, new Vector3(roomX, floorY + 0.07f, livingZ), new Vector3(roomWidth * 0.82f, 0.04f, Mathf.Min(3.2f, depth * 0.28f)), carpet);
            ExpansionBox(tower, "ApartmentSofa", new Vector3(roomX - side * 0.35f, floorY + 0.34f, livingZ + 0.35f), new Vector3(Mathf.Min(2.0f, roomWidth * 0.58f), 0.48f, 0.78f), bedding);
            ExpansionBox(tower, "ApartmentCoffeeTable", new Vector3(roomX + side * 0.45f, floorY + 0.28f, livingZ - 0.35f), new Vector3(0.95f, 0.32f, 0.55f), table);
            ExpansionBox(tower, "ApartmentTVStand", new Vector3(roomX + side * roomWidth * 0.28f, floorY + 0.35f, livingZ + 0.95f), new Vector3(1.15f, 0.5f, 0.38f), wood);
            MeshBox(tower, new Vector3(roomX + side * roomWidth * 0.28f, floorY + 0.92f, livingZ + 1.05f), new Vector3(0.95f, 0.55f, 0.08f), screen);
            ExpansionBox(tower, "ApartmentBed", new Vector3(roomX, floorY + 0.3f, bedZ), new Vector3(Mathf.Min(2.15f, roomWidth * 0.66f), 0.42f, 1.25f), wood);
            MeshBox(tower, new Vector3(roomX, floorY + 0.54f, bedZ), new Vector3(Mathf.Min(1.95f, roomWidth * 0.6f), 0.1f, 1.05f), bedding);
            ExpansionBox(tower, "ApartmentNightstand", new Vector3(roomX + side * roomWidth * 0.28f, floorY + 0.32f, bedZ + 0.75f), new Vector3(0.45f, 0.48f, 0.4f), wood);
            ExpansionBox(tower, "ApartmentWardrobe", new Vector3(roomX - side * roomWidth * 0.28f, floorY + 1.05f, bedZ - 0.15f), new Vector3(0.72f, 1.95f, 0.55f), wood);
            // Kitchenette strip against the outer wall
            var kitchenX = roomX + side * roomWidth * 0.32f;
            ExpansionBox(tower, "ApartmentCounter", new Vector3(kitchenX, floorY + 0.48f, depth * 0.05f), new Vector3(0.62f, 0.78f, 1.8f), appliance);
            ExpansionBox(tower, "ApartmentFridge", new Vector3(kitchenX, floorY + 0.95f, depth * 0.05f - 1.15f), new Vector3(0.7f, 1.75f, 0.68f), appliance);
            MeshBox(tower, new Vector3(kitchenX, floorY + 0.92f, depth * 0.05f + 0.35f), new Vector3(0.5f, 0.08f, 0.5f), table);
            // Desk / work corner
            ExpansionBox(tower, "ApartmentDesk", new Vector3(roomX - side * 0.2f, floorY + 0.4f, -depth * 0.08f), new Vector3(1.35f, 0.12f, 0.62f), table);
            ExpansionBox(tower, "ApartmentChair", new Vector3(roomX - side * 0.2f, floorY + 0.28f, -depth * 0.08f + side * 0.55f), new Vector3(0.45f, 0.45f, 0.45f), wood);
            // Extra clutter so apartments read as lived-in, not empty boxes.
            ExpansionBox(tower, "ApartmentShelf", new Vector3(roomX + side * roomWidth * 0.18f, floorY + 1.15f, -depth * 0.18f), new Vector3(0.9f, 1.7f, 0.32f), wood);
            ExpansionBox(tower, "ApartmentCrate", new Vector3(roomX - side * 0.55f, floorY + 0.28f, depth * 0.12f), new Vector3(0.55f, 0.45f, 0.55f), table);
            MeshBox(tower, new Vector3(roomX + side * 0.4f, floorY + 0.08f, -depth * 0.2f), new Vector3(0.7f, 0.05f, 0.9f), carpet);
            if (featuredFloor)
            {
                BuildResidentialRoomTheme(tower, archetype, roomX, side, roomWidth, depth, floorY);
            }
            if (side > 0.0f)
            {
                SpawnResidentialCache(
                    tower,
                    towerIndex,
                    floor,
                    archetype,
                    new Vector3(roomX - side * roomWidth * 0.18f, floorY + 0.12f, -depth * 0.18f),
                    side);
            }
            tower.AddChild(new Label3D
            {
                Name = "ApartmentPurposeSign",
                Position = new Vector3(side * (corridorHalfWidth + 0.08f), floorY + 1.55f, livingZ),
                Text = ResidentialRoomName(archetype),
                FontSize = 16,
                OutlineSize = 5,
                Modulate = ResidentialRoomColor(archetype),
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                VisibilityRangeEnd = 12.0f,
                VisibilityRangeEndMargin = 2.0f
            });
            // The single floor light below serves both rooms; per-room lights multiply too quickly across 96 floors.
        }
        // Corridor carpet runner and ceiling strip lights (mesh only).
        MeshBox(tower, new Vector3(0, floorY + 0.08f, depth * 0.05f), new Vector3(2.2f, 0.03f, depth * 0.62f), carpet);
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
        var floorLabel = new Label3D
        {
            Name = "ResidentialFloorSign",
            Position = new Vector3(0, floorY + 1.65f, coreZ + ResidentialStairOpeningSouthDepth + 1.15f),
            Text = $"{spec.BlockName}  //  FLOOR {floor + 1:00}",
            FontSize = 22,
            OutlineSize = 6,
            Modulate = spec.Accent,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            VisibilityRangeEnd = 18.0f,
            VisibilityRangeEndMargin = 3.0f
        };
        tower.AddChild(floorLabel);
    }

    private static ResidentialRoomArchetype ResidentialRoomArchetypeFor(int towerIndex, int floor, float side)
        => (ResidentialRoomArchetype)((towerIndex * 5 + floor * 2 + (side > 0.0f ? 1 : 0)) % 7);

    private static string ResidentialRoomName(ResidentialRoomArchetype archetype) => archetype switch
    {
        ResidentialRoomArchetype.MedicalClinic => "COMMUNITY CLINIC",
        ResidentialRoomArchetype.EvacuationShelter => "EVAC SHELTER",
        ResidentialRoomArchetype.MaintenanceWorkshop => "MAINTENANCE FLAT",
        ResidentialRoomArchetype.CommunitySecurity => "SECURITY POST",
        ResidentialRoomArchetype.SmugglerDen => "SEALED TENANT UNIT",
        ResidentialRoomArchetype.CommunityKitchen => "COMMUNITY KITCHEN",
        _ => "FAMILY APARTMENT"
    };

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

    private static bool ShouldSpawnResidentialCache(ResidentialTowerSpec spec, int floor)
        => floor == 0 || floor == spec.Floors / 2 || floor == Mathf.Max(1, spec.Floors - 2);

    private void SpawnResidentialCache(
        Node3D tower,
        int towerIndex,
        int floor,
        ResidentialRoomArchetype archetype,
        Vector3 localPosition,
        float side)
    {
        var kind = archetype switch
        {
            ResidentialRoomArchetype.MedicalClinic => ResidentialCacheKind.MedicalCabinet,
            ResidentialRoomArchetype.EvacuationShelter => ResidentialCacheKind.EvacuationLocker,
            ResidentialRoomArchetype.MaintenanceWorkshop => ResidentialCacheKind.WorkshopLocker,
            ResidentialRoomArchetype.CommunitySecurity => ResidentialCacheKind.SecurityArmory,
            ResidentialRoomArchetype.SmugglerDen => ResidentialCacheKind.SmugglerCache,
            ResidentialRoomArchetype.CommunityKitchen => ResidentialCacheKind.CommunityPantry,
            _ => ResidentialCacheKind.FamilyStash
        };
        var cache = new ResidentialSupplyCache
        {
            Name = $"ResidentialCache_T{towerIndex + 1:00}_F{floor + 1:00}_{kind}",
            Position = localPosition,
            Rotation = new Vector3(0, side * Mathf.Pi * 0.5f, 0)
        };
        cache.Configure(kind, towerIndex, floor, CreateResidentialCacheLoot(kind));
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
        const float landingWidth = 5.16f;
        const float landingDepth = 2.8f;
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

        // Full mid-level platform gives a standing player room to clear the first flight and turn.
        AddResidentialStairPart(
            stairCollision,
            tower,
            $"ResidentialStairLanding_F{floor}",
            new Vector3(0, floorY + halfRise - treadThickness * 0.5f, landingCenterZ),
            new Vector3(landingWidth, treadThickness, landingDepth),
            stair);

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

        // The center spine separates the flights but stops south of the open turn platform.
        var coreTop = floorY + ResidentialFloorHeight * 0.5f;
        var spineNorthZ = lowerStartZ + 0.65f;
        var spineSouthZ = upperStartZ + 0.2f;
        var shaftNorthZ = landingNorthZ - 0.06f;
        var shaftSouthZ = upperStartZ + 0.45f;
        var shaftCenterZ = (shaftNorthZ + shaftSouthZ) * 0.5f;
        var shaftSideDepth = shaftSouthZ - shaftNorthZ - 0.12f;
        AddResidentialStairPart(stairCollision, tower, $"ResidentialStairSpine_F{floor}", new Vector3(0, coreTop, (spineNorthZ + spineSouthZ) * 0.5f), new Vector3(0.24f, ResidentialFloorHeight, spineSouthZ - spineNorthZ), stair);
        AddResidentialStairPart(stairCollision, tower, $"ResidentialStairShaftN_F{floor}", new Vector3(0, coreTop, shaftNorthZ), new Vector3(5.44f, ResidentialFloorHeight, 0.12f), stair);
        AddResidentialStairPart(stairCollision, tower, $"ResidentialStairShaftW_F{floor}", new Vector3(-2.66f, coreTop, shaftCenterZ), new Vector3(0.12f, ResidentialFloorHeight, shaftSideDepth), stair);
        AddResidentialStairPart(stairCollision, tower, $"ResidentialStairShaftE_F{floor}", new Vector3(2.66f, coreTop, shaftCenterZ), new Vector3(0.12f, ResidentialFloorHeight, shaftSideDepth), stair);
        const float shaftDoorHalf = 1.1f;
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
        string name,
        Vector3 position,
        Vector3 size)
    {
        body.AddChild(new CollisionShape3D
        {
            Name = name,
            Position = position,
            Shape = new BoxShape3D { Size = size }
        });
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
        Godot.Material steel,
        Godot.Material trim,
        Godot.Material light)
    {
        var roofY = spec.Floors * ResidentialFloorHeight;
        BuildTowerFloorSlab(tower, spec, roofY, coreZ, facade, false);
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
        ExpansionBox(tower, "RooftopUtilityRoom", new Vector3(width * 0.22f, roofY + 1.5f, -depth * 0.2f), new Vector3(Mathf.Min(5.0f, width * 0.3f), 3.0f, Mathf.Min(5.2f, depth * 0.3f)), steel);
        MeshBox(tower, new Vector3(0, roofY + 0.3f, coreZ + ResidentialStairOpeningSouthDepth + 0.5f), new Vector3(2.4f, 0.05f, 0.24f), light);
        if (spec.Floors >= 9)
        {
            ExpansionCylinder(tower, "ResidentialRoofAntenna", new Vector3(0, roofY + 4.2f, 0), 0.08f, 7.5f, trim);
        }
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
        var sign = new Label3D
        {
            Position = new Vector3(0, 3.38f, depth * 0.5f + 0.2f),
            Text = spec.BlockName,
            FontSize = 28,
            OutlineSize = 8,
            Modulate = spec.Accent,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            VisibilityRangeEnd = 44.0f,
            VisibilityRangeEndMargin = 6.0f
        };
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
        var entryQuery = PhysicsRayQueryParameters3D.Create(entryFrom, entryTo);
        entryQuery.CollisionMask = 1;
        entryQuery.CollideWithAreas = false;
        var entryOpen = GetWorld3D().DirectSpaceState.IntersectRay(entryQuery).Count == 0;

        var rampSample = firstTower.ToGlobal(new Vector3(
            -1.45f,
            0.1f + ResidentialFloorHeight * 0.25f,
            firstCoreZ + ResidentialStairRun * 0.2f));
        var rampQuery = PhysicsRayQueryParameters3D.Create(rampSample + Vector3.Up * 1.8f, rampSample - Vector3.Up * 1.8f);
        rampQuery.CollisionMask = 1;
        rampQuery.CollideWithAreas = false;
        var rampHit = GetWorld3D().DirectSpaceState.IntersectRay(rampQuery);
        var rampCollider = rampHit.Count > 0 ? rampHit["collider"].AsGodotObject() as Node : null;
        var stepName = rampCollider?.Name.ToString() ?? "";
        // Prefer discrete StairStep colliders; never require a solid ramp slab.
        var stepCollision = stepName.Contains("StairStep", StringComparison.Ordinal)
            || stepName.Contains("StairLanding", StringComparison.Ordinal)
            || stepName.Contains("Stair", StringComparison.Ordinal)
            || (rampHit.Count > 0 && rampHit["position"].AsVector3().Y > 0.35f);

        // Standing doorway clearance probe (no crouch required).
        var doorProbeFrom = firstTower.ToGlobal(new Vector3(0, 1.7f, firstSpec.Footprint.Y * 0.5f + 0.8f));
        var doorProbeTo = firstTower.ToGlobal(new Vector3(0, 1.7f, firstSpec.Footprint.Y * 0.5f - 1.5f));
        var doorProbe = PhysicsRayQueryParameters3D.Create(doorProbeFrom, doorProbeTo);
        doorProbe.CollisionMask = 1;
        doorProbe.CollideWithAreas = false;
        var standingDoorClear = GetWorld3D().DirectSpaceState.IntersectRay(doorProbe).Count == 0;

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
        var hedgeCollisionCount = 0;
        foreach (var node in hedgeColliders)
        {
            if (node is not StaticBody3D hedge || !IsInstanceValid(hedge))
            {
                continue;
            }
            var axis = hedge.GlobalTransform.Basis.X.Normalized();
            var hedgeQuery = PhysicsRayQueryParameters3D.Create(
                hedge.GlobalPosition - axis * 1.5f,
                hedge.GlobalPosition + axis * 1.5f);
            hedgeQuery.CollisionMask = 1;
            hedgeQuery.CollideWithAreas = false;
            var hedgeHit = GetWorld3D().DirectSpaceState.IntersectRay(hedgeQuery);
            if (hedgeHit.Count > 0 && hedgeHit["collider"].AsGodotObject() == hedge)
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
            && stepCollision
            && playerClimbedRamp
            && hedgesSolid
            && ResidentialCivilianCount >= ResidentialTowerSpecs.Length * 3
            && ResidentialSpecialCivilianCount >= ResidentialTowerSpecs.Length * 2
            && roles.Count == Enum.GetValues<CivilianRole>().Length
            && upperFloorPopulation;
        GD.Print($"RESIDENTIAL_CHECK valid={valid} towers={ResidentialTowerCount}/{ResidentialTowerSpecs.Length} floors={_residentialFloorCount}/{expectedFloors} stair_flights={_residentialStairFlightCount} stair_details={_residentialStairDetailCount}/{expectedFloors} infill={_residentialInfillModuleCount}/{ResidentialTowerSpecs.Length * 4} entry_open={entryOpen} standing_door={standingDoorClear} step_collision={stepCollision} step_hit={stepName} player_climbed={playerClimbedRamp} climb_height={climbHeight:0.00} hedges_solid={hedgesSolid} hedge_hits={hedgeCollisionCount}/{hedgeColliders.Count} rooftops={_residentialRoofAccessCount} civilians={ResidentialCivilianCount} special={ResidentialSpecialCivilianCount} roles={roles.Count} upper_floors={upperFloorPopulation}");
        if (!valid)
        {
            GD.PushError("Residential community validation failed.");
        }
        GetTree().Quit(valid ? 0 : 2);
    }

    private async void ValidateResidentialGameplay()
    {
        foreach (var enemy in _enemies)
        {
            enemy.ProcessMode = ProcessModeEnum.Disabled;
        }
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

        var expectedCaches = ResidentialTowerSpecs.Sum(spec => spec.Floors);
        var cacheKinds = new HashSet<ResidentialCacheKind>();
        var lootKinds = new HashSet<LootItemKind>();
        var stockedFloors = new HashSet<(int Tower, int Floor)>();
        var everyCacheHasMedicine = true;
        var cachesRegistered = true;
        var cachesStocked = true;
        var reachableCaches = 0;
        foreach (var cache in _residentialCaches)
        {
            cacheKinds.Add(cache.Kind);
            stockedFloors.Add((cache.TowerIndex, cache.FloorIndex));
            cachesRegistered &= _lootSources.Contains(cache) && IsInstanceValid(cache);
            cachesStocked &= cache.IsSearchable && cache.Loot.Count >= 2;
            if (HasClearLootInteractionApproach(cache))
            {
                reachableCaches++;
            }
            var cacheHasMedicine = false;
            foreach (var item in cache.Loot)
            {
                lootKinds.Add(item.Kind);
                cacheHasMedicine |= item.Kind == LootItemKind.Medical;
            }
            everyCacheHasMedicine &= cacheHasMedicine;
        }
        var everyTowerStocked = true;
        for (var towerIndex = 0; towerIndex < _residentialCacheCountByTower.Length; towerIndex++)
        {
            everyTowerStocked &= _residentialCacheCountByTower[towerIndex] == ResidentialTowerSpecs[towerIndex].Floors;
        }
        var expectedStockedFloors = new HashSet<(int Tower, int Floor)>();
        for (var towerIndex = 0; towerIndex < ResidentialTowerSpecs.Length; towerIndex++)
        {
            for (var floor = 0; floor < ResidentialTowerSpecs[towerIndex].Floors; floor++)
            {
                expectedStockedFloors.Add((towerIndex, floor));
            }
        }
        var everyFloorStocked = stockedFloors.SetEquals(expectedStockedFloors);

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

        var valid = ResidentialCacheCount == expectedCaches
            && everyTowerStocked
            && everyFloorStocked
            && _residentialRoomArchetypes.Count == Enum.GetValues<ResidentialRoomArchetype>().Length
            && cacheKinds.Count == Enum.GetValues<ResidentialCacheKind>().Length
            && lootKinds.Count >= 6
            && lootKinds.Contains(LootItemKind.Medical)
            && everyCacheHasMedicine
            && cachesRegistered
            && cachesStocked
            && reachableCaches == expectedCaches
            && lootUiOpened
            && assistanceRoles.Count == Enum.GetValues<CivilianRole>().Length
            && medicHealed;
        GD.Print($"RESIDENTIAL_GAMEPLAY_CHECK valid={valid} room_types={_residentialRoomArchetypes.Count}/7 caches={ResidentialCacheCount}/{expectedCaches} stocked_floors={stockedFloors.Count}/{expectedCaches} reachable={reachableCaches}/{expectedCaches} cache_types={cacheKinds.Count}/7 loot_types={lootKinds.Count} every_tower={everyTowerStocked} every_floor={everyFloorStocked} registered={cachesRegistered} stocked={cachesStocked} loot_ui={lootUiOpened} assistance_roles={assistanceRoles.Count}/5 medic_healed={medicHealed}");
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
            var query = PhysicsRayQueryParameters3D.Create(from, to);
            query.Exclude = new Godot.Collections.Array<Rid> { shooter.GetRid() };
            query.CollideWithAreas = false;
            query.CollisionMask = 0xFFFFFFFF;
            var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
            if (hit.Count == 0)
            {
                return "none";
            }
            var node = hit["collider"].AsGodotObject() as Node;
            return node?.Name.ToString() ?? "unknown";
        }

        var sampleCount = 0;
        var blockedSamples = 0;
        string? firstLeak = null;
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
                var query = PhysicsRayQueryParameters3D.Create(tower.ToGlobal(localFrom), tower.ToGlobal(localTo));
                query.Exclude = new Godot.Collections.Array<Rid> { shooter.GetRid(), _player.GetRid() };
                query.CollideWithAreas = false;
                query.CollisionMask = 1;
                if (GetWorld3D().DirectSpaceState.IntersectRay(query).Count > 0)
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
        camera.GlobalPosition = clinicTower.ToGlobal(new Vector3(3.7f, 1.55f, -5.0f));
        camera.LookAt(clinicTower.ToGlobal(new Vector3(7.2f, 0.95f, -10.5f)), Vector3.Up);
        await WaitFrames(24);
        SaveViewportImage("res://residential_clinic_validation.png");

        const int shelterFloor = 4;
        var shelterY = shelterFloor * ResidentialFloorHeight;
        camera.GlobalPosition = clinicTower.ToGlobal(new Vector3(3.7f, shelterY + 1.55f, -5.0f));
        camera.LookAt(clinicTower.ToGlobal(new Vector3(7.2f, shelterY + 0.95f, -10.5f)), Vector3.Up);
        await WaitFrames(20);
        SaveViewportImage("res://residential_shelter_validation.png");

        var securityTower = _residentialTowers[2];
        camera.GlobalPosition = securityTower.ToGlobal(new Vector3(3.7f, 1.55f, -2.5f));
        camera.LookAt(securityTower.ToGlobal(new Vector3(10.5f, 1.05f, -5.5f)), Vector3.Up);
        await WaitFrames(20);
        SaveViewportImage("res://residential_security_validation.png");
        GD.Print($"RESIDENTIAL_GAMEPLAY_CAPTURE caches={ResidentialCacheCount} room_types={_residentialRoomArchetypes.Count} paths=residential_clinic_validation.png,residential_shelter_validation.png,residential_security_validation.png");
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
                ExpansionBox(bridge, "SkybridgeDeck", new Vector3(0, 0.05f, mid), new Vector3(3.5f, 0.16f, span), deck);
                ExpansionBox(bridge, "SkybridgeSillW", new Vector3(-1.69f, 0.39f, mid), new Vector3(0.14f, 0.68f, windowSpan), sill);
                ExpansionBox(bridge, "SkybridgeSillE", new Vector3(1.69f, 0.39f, mid), new Vector3(0.14f, 0.68f, windowSpan), sill);

                var bridgeTint = new Color(0.72f, 0.94f, 0.97f, 0.9f);
                bridgeGlass.AddPane(new Vector3(-1.69f, 1.76f, mid), new Vector3(0.045f, 2.08f, windowSpan), bridgeTint);
                bridgeGlass.AddPane(new Vector3(1.69f, 1.76f, mid), new Vector3(0.045f, 2.08f, windowSpan), bridgeTint);
                bridgeGlass.AddPane(new Vector3(0, 2.91f, mid), new Vector3(3.22f, 0.045f, windowSpan), bridgeTint);
                bridgeGlass.Commit();
                _residentialSkybridgeWindowCount += 3;

                MeshBox(bridge, new Vector3(-1.69f, 0.77f, mid), new Vector3(0.16f, 0.12f, windowSpan), frame).Name = "SkybridgeLowerRailW";
                MeshBox(bridge, new Vector3(1.69f, 0.77f, mid), new Vector3(0.16f, 0.12f, windowSpan), frame).Name = "SkybridgeLowerRailE";
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
