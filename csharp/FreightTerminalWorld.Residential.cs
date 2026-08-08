using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private const float ResidentialFloorHeight = 3.15f;
    private const float ResidentialStairRun = 5.4f;
    private const float ResidentialStairOpeningWidth = 5.6f;
    private const float ResidentialStairOpeningNorthDepth = 5.45f;
    private const float ResidentialStairOpeningSouthDepth = 3.25f;
    private const int ResidentialStepsPerFlight = 8;

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
        new(9, 10, new[] { 3, 6 }),
        new(2, 3, new[] { 2, 4 }),
        new(3, 4, new[] { 2, 4 })
    };

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
    private int _residentialFloorCount;
    private int _residentialStairFlightCount;
    private int _residentialRoofAccessCount;

    public int ResidentialTowerCount => _residentialTowers.Count;
    public int ResidentialCivilianCount => _civilians.Count;
    public int ResidentialSpecialCivilianCount => _civilians.FindAll(civilian => civilian.IsSpecial).Count;

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

        var facade = Mat(
            $"residential_facade_{index % 5}",
            new Color(
                Mathf.Lerp(0.22f, spec.Accent.R, 0.4f),
                Mathf.Lerp(0.24f, spec.Accent.G, 0.4f),
                Mathf.Lerp(0.23f, spec.Accent.B, 0.4f)),
            0.03f,
            0.88f);
        var interiorWall = Mat("residential_interior_wall", new Color(0.63f, 0.65f, 0.6f), 0.01f, 0.92f);
        var interiorFloor = Mat("residential_interior_floor", new Color(0.31f, 0.29f, 0.24f), 0.02f, 0.78f);
        var stair = Mat("residential_stair", new Color(0.39f, 0.42f, 0.4f), 0.12f, 0.76f);
        var warmLight = Mat("residential_warm_light", new Color(0.95f, 0.7f, 0.38f), 0.02f, 0.35f, new Color(0.95f, 0.55f, 0.22f));
        var wood = Mat("residential_wood", new Color(0.31f, 0.18f, 0.1f), 0.0f, 0.82f);
        var bedding = Mat("residential_bedding", new Color(0.22f, 0.38f, 0.45f), 0.0f, 0.9f);
        var stairCoreZ = -Mathf.Min(spec.Footprint.Y * 0.18f, 3.6f);
        var linkSlots = _residentialLinkSlots.TryGetValue(index, out var slotDict) ? slotDict : null;

        BuildTowerCourtyard(tower, spec, facade, steel, warmLight);
        for (var floor = 0; floor < spec.Floors; floor++)
        {
            var floorY = floor * ResidentialFloorHeight;
            BuildTowerFloorSlab(tower, spec, floorY, stairCoreZ, interiorFloor, floor == 0);
            var westSlot = linkSlots is not null && linkSlots.TryGetValue(1, out var westLink) && westLink.Floors.Contains(floor) ? westLink : null;
            var eastSlot = linkSlots is not null && linkSlots.TryGetValue(0, out var eastLink) && eastLink.Floors.Contains(floor) ? eastLink : null;
            BuildTowerFloorShell(tower, spec, floor, floorY, facade, glass, spec.Accent, westSlot, eastSlot);
            BuildTowerInterior(tower, spec, floor, floorY, stairCoreZ, interiorWall, wood, bedding, warmLight, westSlot, eastSlot);
            BuildTowerStairs(tower, floor, floorY, stairCoreZ, stair, trim, warmLight);
            _residentialFloorCount++;
        }
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
        Godot.Material glass,
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

        var windowMaterial = Mat(
            $"residential_window_{floor % 3}",
            floor % 3 == 0 ? new Color(0.12f, 0.22f, 0.25f) : new Color(0.08f, 0.15f, 0.18f),
            0.58f,
            0.18f,
            floor % 4 == 0 ? accent * 0.08f : default);
        var windowY = floorY + 1.66f;
        for (var x = -width * 0.5f + 2.1f; x <= width * 0.5f - 2.0f; x += 3.6f)
        {
            MeshBox(tower, new Vector3(x, windowY, -depth * 0.5f - 0.105f), new Vector3(2.05f, 1.28f, 0.035f), windowMaterial);
            if (floor > 0 || Mathf.Abs(x) > 2.1f)
            {
                MeshBox(tower, new Vector3(x, windowY, depth * 0.5f + 0.105f), new Vector3(2.05f, 1.28f, 0.035f), windowMaterial);
            }
        }
        for (var z = -depth * 0.5f + 2.1f; z <= depth * 0.5f - 2.0f; z += 3.6f)
        {
            MeshBox(tower, new Vector3(-width * 0.5f - 0.105f, windowY, z), new Vector3(0.035f, 1.28f, 2.05f), windowMaterial);
            MeshBox(tower, new Vector3(width * 0.5f + 0.105f, windowY, z), new Vector3(0.035f, 1.28f, 2.05f), windowMaterial);
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
        foreach (var side in new[] { -1.0f, 1.0f })
        {
            var roomX = side * (corridorHalfWidth + roomWidth * 0.5f);
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
            // Room purpose placard
            var purpose = ((floor + (side > 0 ? 1 : 0)) % 4) switch
            {
                0 => "LIVING QUARTERS",
                1 => "FAMILY UNIT",
                2 => "STAGING ROOM",
                _ => "MED BAY ANNEX"
            };
            tower.AddChild(new Label3D
            {
                Name = "ApartmentPurposeSign",
                Position = new Vector3(side * (corridorHalfWidth + 0.08f), floorY + 1.55f, livingZ),
                Text = purpose,
                FontSize = 16,
                OutlineSize = 5,
                Modulate = spec.Accent,
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                VisibilityRangeEnd = 12.0f,
                VisibilityRangeEndMargin = 2.0f
            });
            // No per-room OmniLight — residential towers were spawning hundreds of lights (major FPS hit).
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
            Position = new Vector3(0, floorY + 2.72f, depth * 0.1f),
            LightColor = new Color(1.0f, 0.78f, 0.52f),
            LightEnergy = 0.7f,
            OmniRange = 9.0f,
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
        Godot.Material rail,
        Godot.Material light)
    {
        // Thin tread plates only (no floor-to-top filled pillars = no z-fighting flicker).
        // More steps / shallow rise (~0.10 m) so capsules climb with FloorSnap + step-up assist.
        var halfRise = ResidentialFloorHeight * 0.5f;
        const int steps = 16;
        var stepRise = halfRise / steps;
        var stepRun = ResidentialStairRun / steps;
        const float treadWidth = 1.95f;
        var treadDepth = stepRun * 1.08f;
        const float treadThickness = 0.14f;
        var lowerStartZ = coreZ - ResidentialStairRun * 0.5f;
        var upperStartZ = coreZ + ResidentialStairRun * 0.5f;
        const float landingWidth = 5.16f;
        const float landingDepth = 2.8f;
        var landingSouthZ = lowerStartZ + 0.2f;
        var landingNorthZ = landingSouthZ - landingDepth;
        var landingCenterZ = (landingNorthZ + landingSouthZ) * 0.5f;

        // Lower flight (west) — each step is a single thin plate at its top height.
        for (var step = 0; step < steps; step++)
        {
            var topY = floorY + stepRise * (step + 1);
            var z = upperStartZ - stepRun * (step + 0.5f);
            ExpansionBox(
                tower,
                $"ResidentialStairStep_L{floor}_{step}",
                new Vector3(-1.45f, topY - treadThickness * 0.5f, z),
                new Vector3(treadWidth, treadThickness, treadDepth),
                stair);
        }

        // Full mid-level platform gives a standing player room to clear the first flight and turn.
        ExpansionBox(
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
            ExpansionBox(
                tower,
                $"ResidentialStairStep_U{floor}_{step}",
                new Vector3(1.45f, topY - treadThickness * 0.5f, z),
                new Vector3(treadWidth, treadThickness, treadDepth),
                stair);
        }

        // The center spine separates the flights but stops south of the open turn platform.
        var coreTop = floorY + ResidentialFloorHeight * 0.5f;
        var spineNorthZ = lowerStartZ + 0.65f;
        var spineSouthZ = upperStartZ + 0.2f;
        var shaftNorthZ = landingNorthZ - 0.06f;
        var shaftSouthZ = upperStartZ + 0.45f;
        var shaftCenterZ = (shaftNorthZ + shaftSouthZ) * 0.5f;
        var shaftSideDepth = shaftSouthZ - shaftNorthZ - 0.12f;
        ExpansionBox(tower, $"ResidentialStairSpine_F{floor}", new Vector3(0, coreTop, (spineNorthZ + spineSouthZ) * 0.5f), new Vector3(0.24f, ResidentialFloorHeight, spineSouthZ - spineNorthZ), stair);
        ExpansionBox(tower, $"ResidentialStairShaftN_F{floor}", new Vector3(0, coreTop, shaftNorthZ), new Vector3(5.44f, ResidentialFloorHeight, 0.12f), stair);
        ExpansionBox(tower, $"ResidentialStairShaftW_F{floor}", new Vector3(-2.66f, coreTop, shaftCenterZ), new Vector3(0.12f, ResidentialFloorHeight, shaftSideDepth), stair);
        ExpansionBox(tower, $"ResidentialStairShaftE_F{floor}", new Vector3(2.66f, coreTop, shaftCenterZ), new Vector3(0.12f, ResidentialFloorHeight, shaftSideDepth), stair);
        const float shaftDoorHalf = 1.1f;
        var shaftSideWidth = 2.72f - shaftDoorHalf;
        ExpansionBox(tower, $"ResidentialStairShaftSW_F{floor}", new Vector3(-(shaftDoorHalf + shaftSideWidth * 0.5f), floorY + 0.1f + 1.275f, upperStartZ + 0.45f), new Vector3(shaftSideWidth, 2.55f, 0.12f), stair);
        ExpansionBox(tower, $"ResidentialStairShaftSE_F{floor}", new Vector3(shaftDoorHalf + shaftSideWidth * 0.5f, floorY + 0.1f + 1.275f, upperStartZ + 0.45f), new Vector3(shaftSideWidth, 2.55f, 0.12f), stair);
        ExpansionBox(tower, $"ResidentialStairShaftSH_F{floor}", new Vector3(0, floorY + 0.1f + 2.55f + (ResidentialFloorHeight - 2.55f) * 0.5f, upperStartZ + 0.45f), new Vector3(shaftDoorHalf * 2.0f, Mathf.Max(0.12f, ResidentialFloorHeight - 2.55f), 0.12f), stair);
        MeshBox(
            tower,
            new Vector3(0, floorY + halfRise + 0.95f, landingNorthZ + 0.08f),
            new Vector3(5.0f, 0.08f, 0.08f),
            rail);
        MeshBox(tower, new Vector3(0, floorY + halfRise + 1.35f, landingNorthZ + 0.08f), new Vector3(1.8f, 0.04f, 0.16f), light);
        _residentialStairFlightCount += 2;
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
        foreach (var x in new[] { -width * 0.34f, width * 0.34f })
        {
            ExpansionBox(tower, "CourtyardPlanter", new Vector3(x, 0.34f, depth * 0.5f + 1.75f), new Vector3(2.2f, 0.58f, 1.1f), planter);
            tower.AddChild(new MeshInstance3D
            {
                Position = new Vector3(x, 1.02f, depth * 0.5f + 1.75f),
                Mesh = new SphereMesh { Radius = 0.72f, Height = 1.15f, RadialSegments = 12, Rings = 6 },
                MaterialOverride = foliage
            });
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
            && _residentialRoofAccessCount == ResidentialTowerSpecs.Length
            && _residentialEntrances.Count == ResidentialTowerSpecs.Length
            && _residentialRooftops.Count == ResidentialTowerSpecs.Length
            && entryOpen
            && standingDoorClear
            && stepCollision
            && playerClimbedRamp
            && ResidentialCivilianCount >= ResidentialTowerSpecs.Length * 3
            && ResidentialSpecialCivilianCount >= ResidentialTowerSpecs.Length * 2
            && roles.Count == Enum.GetValues<CivilianRole>().Length
            && upperFloorPopulation;
        GD.Print($"RESIDENTIAL_CHECK valid={valid} towers={ResidentialTowerCount}/{ResidentialTowerSpecs.Length} floors={_residentialFloorCount}/{expectedFloors} stair_flights={_residentialStairFlightCount} entry_open={entryOpen} standing_door={standingDoorClear} step_collision={stepCollision} step_hit={stepName} player_climbed={playerClimbedRamp} climb_height={climbHeight:0.00} rooftops={_residentialRoofAccessCount} civilians={ResidentialCivilianCount} special={ResidentialSpecialCivilianCount} roles={roles.Count} upper_floors={upperFloorPopulation}");
        if (!valid)
        {
            GD.PushError("Residential community validation failed.");
        }
        GetTree().Quit(valid ? 0 : 2);
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
        var shell = Mat("residential_skybridge_shell", new Color(0.42f, 0.46f, 0.47f), 0.05f, 0.85f);
        var deck = Mat("residential_skybridge_deck", new Color(0.3f, 0.31f, 0.3f), 0.08f, 0.8f);
        var glow = Mat("residential_skybridge_light", new Color(0.95f, 0.8f, 0.5f), 0.02f, 0.4f, new Color(0.95f, 0.6f, 0.25f));
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
                var span = length + 1.2f;
                var mid = length * 0.5f;
                ExpansionBox(bridge, "SkybridgeDeck", new Vector3(0, 0.05f, mid), new Vector3(2.6f, 0.12f, span), deck);
                ExpansionBox(bridge, "SkybridgeRoof", new Vector3(0, 2.92f, mid), new Vector3(2.7f, 0.12f, span), shell);
                ExpansionBox(bridge, "SkybridgeWallW", new Vector3(-1.32f, 0.85f, mid), new Vector3(0.08f, 1.5f, span - 1.8f), shell);
                ExpansionBox(bridge, "SkybridgeWallE", new Vector3(1.32f, 0.85f, mid), new Vector3(0.08f, 1.5f, span - 1.8f), shell);
                MeshBox(bridge, new Vector3(-1.32f, 2.1f, mid), new Vector3(0.05f, 1.0f, span * 0.8f), glass);
                MeshBox(bridge, new Vector3(1.32f, 2.1f, mid), new Vector3(0.05f, 1.0f, span * 0.8f), glass);
                for (var z = 1.5f; z < length - 1.0f; z += 6.0f)
                {
                    MeshBox(bridge, new Vector3(0, 2.8f, z), new Vector3(1.8f, 0.05f, 0.22f), glow);
                }
            }
        }
    }
}
