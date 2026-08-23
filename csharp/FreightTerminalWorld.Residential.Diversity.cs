using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private readonly List<ResidentialTowerArtResult> _residentialTowerArtResults = new();
    private ResidentialTowerArtBuilder? _residentialTowerArtBuilder;

    public int ResidentialAuthoredDressingCount
        => _residentialTowerArtResults.Sum(result => result.AuthoredModelCount);

    private readonly record struct ResidentialRoomAnchors(float BedZ, float KitchenX);

    private (Node3D Root, ResidentialFloorLayout Layout) CreateResidentialFloorLayoutRoot(
        Node3D tower,
        int towerIndex,
        int floor,
        int floorCount)
    {
        var layout = ResidentialTowerDiversityPlan.LayoutFor(towerIndex, floor, floorCount);
        var root = new Node3D
        {
            Name = $"ResidentialFloorLayout_T{towerIndex + 1:00}_F{floor + 1:00}"
        };
        root.AddToGroup("residential_floor_layouts");
        root.SetMeta("residential_tower_index", towerIndex);
        root.SetMeta("residential_floor_index", floor);
        root.SetMeta("residential_layout", layout.ToString());
        tower.AddChild(root);
        return (root, layout);
    }

    private void BuildResidentialLayoutPartitions(
        Node3D parent,
        int towerIndex,
        int floor,
        float side,
        float roomX,
        float roomWidth,
        float depth,
        float floorY,
        ResidentialFloorLayout layout,
        Godot.Material wall)
    {
        const float height = 2.85f;
        var wallY = floorY + 0.1f + height * 0.5f;
        var prefix = $"LayoutPartition_T{towerIndex + 1:00}_F{floor + 1:00}_{(side < 0 ? "W" : "E")}";

        void Add(string suffix, Vector3 position, Vector3 size)
        {
            ExpansionBox(parent, $"{prefix}_{suffix}", position, size, wall);
        }

        switch (layout)
        {
            case ResidentialFloorLayout.OffsetApartment:
                Add("OffsetDivider", new Vector3(roomX, wallY, -depth * 0.04f), new Vector3(roomWidth * 0.92f, height, 0.1f));
                Add("ReturnWall", new Vector3(roomX + side * roomWidth * 0.28f, wallY, depth * 0.14f), new Vector3(0.1f, height, depth * 0.27f));
                break;
            case ResidentialFloorLayout.OpenLoft:
                Add("LoftScreen", new Vector3(roomX + side * roomWidth * 0.24f, wallY, depth * 0.02f), new Vector3(roomWidth * 0.38f, height, 0.1f));
                break;
            case ResidentialFloorLayout.ClinicWard:
                Add("ClinicDivider", new Vector3(roomX + side * roomWidth * 0.2f, wallY, -depth * 0.02f), new Vector3(roomWidth * 0.5f, height, 0.1f));
                Add("ClinicBayNorth", new Vector3(roomX + side * roomWidth * 0.32f, wallY, -depth * 0.25f), new Vector3(0.1f, height, depth * 0.17f));
                Add("ClinicBaySouth", new Vector3(roomX + side * roomWidth * 0.32f, wallY, depth * 0.25f), new Vector3(0.1f, height, depth * 0.17f));
                break;
            case ResidentialFloorLayout.ShelterDormitory:
                Add("DormLockerScreen", new Vector3(roomX + side * roomWidth * 0.34f, floorY + 0.72f, 0), new Vector3(0.12f, 1.25f, depth * 0.24f));
                break;
            case ResidentialFloorLayout.SecuritySuite:
                AddResidentialTransverseWallWithDoor(
                    parent,
                    prefix,
                    roomX,
                    roomWidth,
                    depth * 0.1f,
                    floorY,
                    height,
                    wall);
                Add("SecurityReturn", new Vector3(roomX - side * roomWidth * 0.3f, wallY, -depth * 0.08f), new Vector3(0.1f, height, depth * 0.24f));
                break;
            case ResidentialFloorLayout.CommunityKitchen:
                Add("KitchenDividerOuter", new Vector3(roomX + side * roomWidth * 0.34f, wallY, -depth * 0.03f), new Vector3(roomWidth * 0.22f, height, 0.1f));
                Add("KitchenDividerInner", new Vector3(roomX - side * roomWidth * 0.35f, wallY, -depth * 0.03f), new Vector3(roomWidth * 0.2f, height, 0.1f));
                break;
            case ResidentialFloorLayout.WorkshopLoft:
                Add("WorkshopCage", new Vector3(roomX + side * roomWidth * 0.34f, wallY, -depth * 0.12f), new Vector3(0.1f, height, depth * 0.34f));
                Add("WorkshopScreen", new Vector3(roomX + side * roomWidth * 0.18f, wallY, depth * 0.09f), new Vector3(roomWidth * 0.32f, height, 0.1f));
                break;
            default:
                Add("FamilyDivider", new Vector3(roomX, wallY, depth * 0.08f), new Vector3(roomWidth * 0.92f, height, 0.1f));
                break;
        }
    }

    private void AddResidentialTransverseWallWithDoor(
        Node3D parent,
        string prefix,
        float roomX,
        float roomWidth,
        float z,
        float floorY,
        float height,
        Godot.Material wall)
    {
        const float doorWidth = 1.55f;
        var roomMin = roomX - roomWidth * 0.46f;
        var roomMax = roomX + roomWidth * 0.46f;
        var doorStart = roomX - doorWidth * 0.5f;
        var doorEnd = roomX + doorWidth * 0.5f;
        var wallY = floorY + 0.1f + height * 0.5f;
        var westLength = doorStart - roomMin;
        var eastLength = roomMax - doorEnd;
        ExpansionBox(
            parent,
            $"{prefix}_SecurityDividerW",
            new Vector3(roomMin + westLength * 0.5f, wallY, z),
            new Vector3(westLength, height, 0.1f),
            wall);
        ExpansionBox(
            parent,
            $"{prefix}_SecurityDividerE",
            new Vector3(doorEnd + eastLength * 0.5f, wallY, z),
            new Vector3(eastLength, height, 0.1f),
            wall);
        ExpansionBox(
            parent,
            $"{prefix}_SecurityDividerHeader",
            new Vector3(roomX, floorY + 2.78f, z),
            new Vector3(doorWidth, 0.24f, 0.1f),
            wall);
    }

    private ResidentialRoomAnchors BuildResidentialLayoutFurnishings(
        Node3D parent,
        int towerIndex,
        int floor,
        float side,
        float roomX,
        float roomWidth,
        float depth,
        float floorY,
        ResidentialFloorLayout layout,
        ResidentialFurnitureKind furnitureKind,
        Godot.Material wood,
        Godot.Material bedding,
        Godot.Material carpet,
        Godot.Material appliance,
        Godot.Material screen,
        Godot.Material table)
    {
        var prefix = $"LayoutFixture_T{towerIndex + 1:00}_F{floor + 1:00}_{(side < 0 ? "W" : "E")}";
        var bedZ = -depth * 0.28f;
        var kitchenX = roomX + side * roomWidth * 0.32f;

        StaticBody3D Solid(string suffix, Vector3 position, Vector3 size, Godot.Material material)
            => ExpansionBox(parent, $"{prefix}_{suffix}", position, size, material);

        void Visual(string suffix, Vector3 position, Vector3 size, Godot.Material material)
        {
            var visual = MeshBox(parent, position, size, material);
            visual.Name = $"{prefix}_{suffix}";
        }

        switch (layout)
        {
            case ResidentialFloorLayout.OffsetApartment:
            {
                var livingZ = depth * 0.27f;
                bedZ = -depth * 0.31f;
                Visual("LivingRug", new Vector3(roomX + side * 0.24f, floorY + 0.07f, livingZ), new Vector3(roomWidth * 0.7f, 0.04f, Mathf.Min(3.4f, depth * 0.25f)), carpet);
                var sofa = Solid("CornerSofa", new Vector3(roomX - side * roomWidth * 0.16f, floorY + 0.35f, livingZ + 0.42f), new Vector3(Mathf.Min(2.35f, roomWidth * 0.52f), 0.5f, 0.82f), bedding);
                sofa.Rotation = new Vector3(0, side * 0.12f, 0);
                Solid("DiningTable", new Vector3(roomX + side * roomWidth * 0.14f, floorY + 0.42f, depth * 0.02f), new Vector3(1.5f, 0.12f, 0.8f), table);
                Solid("PlatformBed", new Vector3(roomX - side * roomWidth * 0.1f, floorY + 0.3f, bedZ), new Vector3(Mathf.Min(2.2f, roomWidth * 0.58f), 0.42f, 1.28f), wood);
                Visual("Mattress", new Vector3(roomX - side * roomWidth * 0.1f, floorY + 0.54f, bedZ), new Vector3(Mathf.Min(2.0f, roomWidth * 0.54f), 0.1f, 1.08f), bedding);
                Solid("KitchenCounter", new Vector3(kitchenX, floorY + 0.48f, depth * 0.13f), new Vector3(0.62f, 0.78f, 1.9f), appliance);
                break;
            }
            case ResidentialFloorLayout.OpenLoft:
                bedZ = -depth * 0.34f;
                Visual("LoftRug", new Vector3(roomX, floorY + 0.07f, depth * 0.18f), new Vector3(roomWidth * 0.78f, 0.04f, Mathf.Min(4.4f, depth * 0.34f)), carpet);
                Solid("ModularSofa", new Vector3(roomX - side * roomWidth * 0.18f, floorY + 0.34f, depth * 0.29f), new Vector3(Mathf.Min(2.8f, roomWidth * 0.62f), 0.48f, 0.78f), bedding);
                Solid("LoftWorkbench", new Vector3(roomX + side * roomWidth * 0.12f, floorY + 0.48f, -depth * 0.02f), new Vector3(Mathf.Min(2.6f, roomWidth * 0.58f), 0.78f, 0.72f), table);
                Solid("LowBed", new Vector3(roomX, floorY + 0.24f, bedZ), new Vector3(Mathf.Min(2.35f, roomWidth * 0.64f), 0.3f, 1.32f), wood);
                Visual("LowMattress", new Vector3(roomX, floorY + 0.43f, bedZ), new Vector3(Mathf.Min(2.12f, roomWidth * 0.58f), 0.1f, 1.1f), bedding);
                Solid("OpenShelf", new Vector3(roomX + side * roomWidth * 0.3f, floorY + 1.05f, -depth * 0.16f), new Vector3(0.68f, 1.8f, 0.38f), wood);
                break;
            case ResidentialFloorLayout.ClinicWard:
                bedZ = -depth * 0.3f;
                Visual("ClinicRunner", new Vector3(roomX, floorY + 0.07f, 0), new Vector3(roomWidth * 0.72f, 0.035f, depth * 0.16f), carpet);
                foreach (var z in new[] { -depth * 0.31f, depth * 0.28f })
                {
                    Solid($"ClinicCot_{(z < 0 ? "N" : "S")}", new Vector3(roomX - side * roomWidth * 0.16f, floorY + 0.34f, z), new Vector3(1.95f, 0.44f, 0.74f), appliance);
                    Visual($"ClinicPad_{(z < 0 ? "N" : "S")}", new Vector3(roomX - side * roomWidth * 0.16f, floorY + 0.59f, z), new Vector3(1.76f, 0.08f, 0.58f), bedding);
                }
                Solid("ReceptionDesk", new Vector3(roomX + side * roomWidth * 0.12f, floorY + 0.5f, depth * 0.08f), new Vector3(1.85f, 0.82f, 0.72f), table);
                Solid("MedicineCabinet", new Vector3(roomX + side * roomWidth * 0.34f, floorY + 1.0f, -depth * 0.12f), new Vector3(0.72f, 1.8f, 0.55f), appliance);
                Solid("WaitingBench", new Vector3(roomX, floorY + 0.32f, depth * 0.39f), new Vector3(Mathf.Min(2.2f, roomWidth * 0.55f), 0.42f, 0.55f), bedding);
                break;
            case ResidentialFloorLayout.ShelterDormitory:
                bedZ = -depth * 0.32f;
                Visual("DormRunner", new Vector3(roomX, floorY + 0.07f, 0), new Vector3(roomWidth * 0.58f, 0.035f, depth * 0.62f), carpet);
                foreach (var z in new[] { -depth * 0.3f, depth * 0.28f })
                {
                    Solid($"BunkLow_{(z < 0 ? "N" : "S")}", new Vector3(roomX - side * roomWidth * 0.14f, floorY + 0.28f, z), new Vector3(2.0f, 0.34f, 0.72f), wood);
                    Solid($"BunkHigh_{(z < 0 ? "N" : "S")}", new Vector3(roomX - side * roomWidth * 0.14f, floorY + 1.28f, z), new Vector3(2.0f, 0.2f, 0.72f), wood);
                    Visual($"BunkPad_{(z < 0 ? "N" : "S")}", new Vector3(roomX - side * roomWidth * 0.14f, floorY + 0.5f, z), new Vector3(1.8f, 0.08f, 0.58f), bedding);
                }
                Solid("RationTable", new Vector3(roomX + side * roomWidth * 0.12f, floorY + 0.42f, 0), new Vector3(1.65f, 0.12f, 0.82f), table);
                Solid("LuggageStack", new Vector3(roomX + side * roomWidth * 0.32f, floorY + 0.42f, depth * 0.18f), new Vector3(0.82f, 0.72f, 0.72f), appliance);
                break;
            case ResidentialFloorLayout.SecuritySuite:
                bedZ = -depth * 0.33f;
                Visual("SecurityFloorMark", new Vector3(roomX, floorY + 0.07f, depth * 0.03f), new Vector3(roomWidth * 0.65f, 0.035f, 0.62f), carpet);
                Solid("OperationsDesk", new Vector3(roomX - side * roomWidth * 0.1f, floorY + 0.5f, depth * 0.29f), new Vector3(Mathf.Min(2.45f, roomWidth * 0.6f), 0.82f, 0.76f), table);
                for (var screenIndex = -1; screenIndex <= 1; screenIndex++)
                {
                    Visual($"Monitor_{screenIndex + 2:00}", new Vector3(roomX - side * roomWidth * 0.1f + screenIndex * 0.62f, floorY + 1.18f, depth * 0.27f), new Vector3(0.5f, 0.4f, 0.06f), screen);
                }
                Solid("EquipmentLocker", new Vector3(roomX + side * roomWidth * 0.34f, floorY + 1.0f, -depth * 0.23f), new Vector3(0.78f, 1.8f, 0.65f), appliance);
                Solid("BriefingTable", new Vector3(roomX, floorY + 0.42f, -depth * 0.22f), new Vector3(1.8f, 0.12f, 0.82f), wood);
                break;
            case ResidentialFloorLayout.CommunityKitchen:
                bedZ = -depth * 0.34f;
                Visual("KitchenTile", new Vector3(roomX, floorY + 0.07f, depth * 0.02f), new Vector3(roomWidth * 0.78f, 0.035f, depth * 0.5f), carpet);
                foreach (var z in new[] { -depth * 0.16f, depth * 0.18f })
                {
                    Solid($"KitchenIsland_{(z < 0 ? "N" : "S")}", new Vector3(roomX - side * roomWidth * 0.08f, floorY + 0.52f, z), new Vector3(2.25f, 0.86f, 0.82f), appliance);
                }
                Solid("DiningTable", new Vector3(roomX - side * roomWidth * 0.12f, floorY + 0.42f, depth * 0.34f), new Vector3(1.9f, 0.12f, 0.86f), table);
                Solid("PantryRack", new Vector3(roomX + side * roomWidth * 0.33f, floorY + 1.05f, -depth * 0.3f), new Vector3(0.72f, 1.9f, 0.72f), wood);
                if (furnitureKind != ResidentialFurnitureKind.Refrigerator)
                {
                    Solid("ColdStore", new Vector3(kitchenX, floorY + 0.95f, depth * 0.32f), new Vector3(0.72f, 1.75f, 0.68f), appliance);
                }
                break;
            case ResidentialFloorLayout.WorkshopLoft:
                bedZ = -depth * 0.34f;
                Visual("WorkshopMat", new Vector3(roomX, floorY + 0.07f, 0), new Vector3(roomWidth * 0.72f, 0.035f, depth * 0.42f), carpet);
                foreach (var z in new[] { -depth * 0.27f, depth * 0.25f })
                {
                    Solid($"WorkBench_{(z < 0 ? "N" : "S")}", new Vector3(roomX - side * roomWidth * 0.12f, floorY + 0.48f, z), new Vector3(2.25f, 0.78f, 0.72f), table);
                }
                Solid("PartsRack", new Vector3(roomX + side * roomWidth * 0.34f, floorY + 1.0f, -depth * 0.08f), new Vector3(0.72f, 1.8f, 0.62f), appliance);
                for (var pipeIndex = -1; pipeIndex <= 1; pipeIndex++)
                {
                    Visual($"Pipe_{pipeIndex + 2:00}", new Vector3(roomX - side * roomWidth * 0.16f + pipeIndex * 0.34f, floorY + 1.2f, depth * 0.08f), new Vector3(0.16f, 0.16f, 1.7f), appliance);
                }
                Solid("RestCot", new Vector3(roomX, floorY + 0.28f, bedZ), new Vector3(1.9f, 0.34f, 0.72f), wood);
                Visual("RestPad", new Vector3(roomX, floorY + 0.49f, bedZ), new Vector3(1.72f, 0.08f, 0.58f), bedding);
                break;
            default:
            {
                var livingZ = depth * 0.28f;
                Visual("LivingRug", new Vector3(roomX, floorY + 0.07f, livingZ), new Vector3(roomWidth * 0.82f, 0.04f, Mathf.Min(3.2f, depth * 0.28f)), carpet);
                Solid("Sofa", new Vector3(roomX - side * 0.35f, floorY + 0.34f, livingZ + 0.35f), new Vector3(Mathf.Min(2.0f, roomWidth * 0.58f), 0.48f, 0.78f), bedding);
                Solid("CoffeeTable", new Vector3(roomX + side * 0.45f, floorY + 0.28f, livingZ - 0.35f), new Vector3(0.95f, 0.32f, 0.55f), table);
                Solid("Bed", new Vector3(roomX, floorY + 0.3f, bedZ), new Vector3(Mathf.Min(2.15f, roomWidth * 0.66f), 0.42f, 1.25f), wood);
                Visual("Mattress", new Vector3(roomX, floorY + 0.54f, bedZ), new Vector3(Mathf.Min(1.95f, roomWidth * 0.6f), 0.1f, 1.05f), bedding);
                Solid("KitchenCounter", new Vector3(kitchenX, floorY + 0.48f, depth * 0.05f), new Vector3(0.62f, 0.78f, 1.8f), appliance);
                Solid("Desk", new Vector3(roomX - side * 0.2f, floorY + 0.4f, -depth * 0.08f), new Vector3(1.35f, 0.12f, 0.62f), table);
                break;
            }
        }

        if (furnitureKind != ResidentialFurnitureKind.Wardrobe
            && layout is ResidentialFloorLayout.FamilySplit or ResidentialFloorLayout.OffsetApartment)
        {
            Solid("Wardrobe", new Vector3(roomX - side * roomWidth * 0.28f, floorY + 1.05f, bedZ - 0.15f), new Vector3(0.72f, 1.95f, 0.55f), wood);
        }
        if (furnitureKind != ResidentialFurnitureKind.Nightstand
            && layout is ResidentialFloorLayout.FamilySplit or ResidentialFloorLayout.OffsetApartment or ResidentialFloorLayout.OpenLoft)
        {
            Solid("Nightstand", new Vector3(roomX + side * roomWidth * 0.28f, floorY + 0.32f, bedZ + 0.75f), new Vector3(0.45f, 0.48f, 0.4f), wood);
        }
        if (furnitureKind != ResidentialFurnitureKind.Refrigerator
            && layout is ResidentialFloorLayout.FamilySplit or ResidentialFloorLayout.OffsetApartment or ResidentialFloorLayout.OpenLoft)
        {
            Solid("Refrigerator", new Vector3(kitchenX, floorY + 0.95f, depth * 0.05f - 1.15f), new Vector3(0.7f, 1.75f, 0.68f), appliance);
        }
        if (furnitureKind != ResidentialFurnitureKind.Refrigerator
            && layout is ResidentialFloorLayout.FamilySplit or ResidentialFloorLayout.OffsetApartment or ResidentialFloorLayout.OpenLoft)
        {
            Solid("Refrigerator", new Vector3(kitchenX, floorY + 0.95f, depth * 0.05f - 1.15f), new Vector3(0.7f, 1.75f, 0.68f), appliance);
        }
        return new ResidentialRoomAnchors(bedZ, kitchenX);
    }

    private void BuildResidentialFacadePattern(
        Node3D tower,
        ResidentialTowerSpec spec,
        int towerIndex,
        int floor,
        float floorY,
        BreakableGlassField glassField,
        Color accent)
    {
        var profile = ResidentialTowerDiversityPlan.ForTower(towerIndex);
        var width = spec.Footprint.X;
        var depth = spec.Footprint.Y;
        var serviceFloor = profile.Facade == ResidentialFacadeStyle.ServiceBands && floor % 3 == 1;
        var (spacing, paneWidth, paneHeight) = profile.Facade switch
        {
            ResidentialFacadeStyle.RibbonGlass => (2.85f, 2.42f, 1.46f),
            ResidentialFacadeStyle.VerticalBays => (4.15f, 1.5f, 1.72f),
            ResidentialFacadeStyle.StaggeredGrid => (3.45f, 1.92f, 1.3f),
            ResidentialFacadeStyle.ServiceBands when serviceFloor => (4.55f, 2.7f, 0.68f),
            ResidentialFacadeStyle.ServiceBands => (4.15f, 1.68f, 1.16f),
            ResidentialFacadeStyle.TerracedWindows => (3.15f, 2.38f, 1.34f),
            _ => (3.6f, 2.05f, 1.28f)
        };
        var stagger = profile.Facade == ResidentialFacadeStyle.StaggeredGrid && floor % 2 == 1
            ? spacing * 0.38f
            : 0.0f;
        var windowY = floorY + (serviceFloor ? 2.02f : 1.66f);
        var windowTint = profile.Facade switch
        {
            ResidentialFacadeStyle.RibbonGlass => new Color(0.48f, 0.72f, 0.78f, 0.88f),
            ResidentialFacadeStyle.VerticalBays => new Color(0.68f, 0.82f, 0.8f, 0.9f),
            ResidentialFacadeStyle.ServiceBands => new Color(0.44f, 0.57f, 0.59f, 0.82f),
            ResidentialFacadeStyle.TerracedWindows => new Color(0.72f, 0.86f, 0.82f, 0.9f),
            _ => new Color(0.58f, 0.76f, 0.8f, 0.86f)
        };
        if ((floor + towerIndex) % 4 == 0)
        {
            windowTint = windowTint.Lerp(new Color(accent.R, accent.G, accent.B, windowTint.A), 0.2f);
        }

        foreach (var x in ResidentialFacadePositions(width, 2.0f, spacing, stagger))
        {
            glassField.AddPane(new Vector3(x, windowY, -depth * 0.5f - 0.105f), new Vector3(paneWidth, paneHeight, 0.035f), windowTint);
            if (floor > 0 || Mathf.Abs(x) > 2.35f)
            {
                glassField.AddPane(new Vector3(x, windowY, depth * 0.5f + 0.105f), new Vector3(paneWidth, paneHeight, 0.035f), windowTint);
            }
        }
        var sideSpacing = profile.Facade is ResidentialFacadeStyle.RibbonGlass or ResidentialFacadeStyle.TerracedWindows
            ? spacing + 0.55f
            : spacing;
        foreach (var z in ResidentialFacadePositions(depth, 2.0f, sideSpacing, stagger * 0.5f))
        {
            glassField.AddPane(new Vector3(-width * 0.5f - 0.105f, windowY, z), new Vector3(0.035f, paneHeight, paneWidth), windowTint);
            glassField.AddPane(new Vector3(width * 0.5f + 0.105f, windowY, z), new Vector3(0.035f, paneHeight, paneWidth), windowTint);
        }
        BuildResidentialBalconyPattern(tower, spec, towerIndex, floor, floorY, profile.Facade, accent);
    }

    private static IReadOnlyList<float> ResidentialFacadePositions(
        float span,
        float margin,
        float spacing,
        float stagger)
    {
        var usable = Mathf.Max(0, span - margin * 2.0f);
        var count = Mathf.Max(1, (int)Mathf.Floor(usable / spacing) + 1);
        var centeredSpan = (count - 1) * spacing;
        var maxStagger = Mathf.Max(0, (usable - centeredSpan) * 0.5f);
        var offset = Mathf.Clamp(stagger, -maxStagger, maxStagger);
        var start = -centeredSpan * 0.5f + offset;
        var positions = new List<float>(count);
        for (var index = 0; index < count; index++)
        {
            positions.Add(start + index * spacing);
        }
        return positions;
    }

    private void BuildResidentialBalconyPattern(
        Node3D tower,
        ResidentialTowerSpec spec,
        int towerIndex,
        int floor,
        float floorY,
        ResidentialFacadeStyle style,
        Color accent)
    {
        if (floor == 0)
        {
            return;
        }
        var material = Mat(
            $"residential_balcony_{style}",
            new Color(
                Mathf.Lerp(0.42f, accent.R, 0.2f),
                Mathf.Lerp(0.44f, accent.G, 0.2f),
                Mathf.Lerp(0.43f, accent.B, 0.2f)),
            0.08f,
            0.82f);
        var width = spec.Footprint.X;
        var depth = spec.Footprint.Y;
        var placements = new List<(string Suffix, Vector3 Position, Vector3 Size)>();
        switch (style)
        {
            case ResidentialFacadeStyle.RibbonGlass when floor % 3 == 0:
                placements.Add(("Ribbon", new Vector3(0, floorY + 0.18f, depth * 0.5f + 0.78f), new Vector3(width * 0.68f, 0.14f, 1.55f)));
                break;
            case ResidentialFacadeStyle.VerticalBays when floor % 4 == 2:
            {
                var side = (floor + towerIndex) % 2 == 0 ? -1.0f : 1.0f;
                placements.Add(("Side", new Vector3(side * (width * 0.5f + 0.72f), floorY + 0.18f, depth * 0.18f), new Vector3(1.45f, 0.14f, Mathf.Min(6.4f, depth * 0.34f))));
                break;
            }
            case ResidentialFacadeStyle.StaggeredGrid when floor % 3 == 1:
            {
                var x = (floor + towerIndex) % 2 == 0 ? -width * 0.22f : width * 0.22f;
                placements.Add(("Staggered", new Vector3(x, floorY + 0.18f, depth * 0.5f + 0.8f), new Vector3(Mathf.Min(5.2f, width * 0.34f), 0.14f, 1.6f)));
                break;
            }
            case ResidentialFacadeStyle.ServiceBands when floor % 5 == 0:
                placements.Add(("Service", new Vector3(-width * 0.24f, floorY + 0.18f, -depth * 0.5f - 0.65f), new Vector3(Mathf.Min(4.8f, width * 0.3f), 0.14f, 1.3f)));
                break;
            case ResidentialFacadeStyle.TerracedWindows when floor % 2 == 1:
            {
                var x = (floor / 2 + towerIndex) % 2 == 0 ? -width * 0.2f : width * 0.2f;
                placements.Add(("Terrace", new Vector3(x, floorY + 0.18f, depth * 0.5f + 0.95f), new Vector3(Mathf.Min(6.4f, width * 0.4f), 0.14f, 1.9f)));
                break;
            }
            case ResidentialFacadeStyle.RecessedGrid when floor % 3 == 0:
                placements.Add(("Central", new Vector3(0, floorY + 0.18f, depth * 0.5f + 0.85f), new Vector3(Mathf.Min(8.0f, width * 0.48f), 0.14f, 1.7f)));
                break;
        }
        foreach (var placement in placements)
        {
            var balcony = ExpansionBox(
                tower,
                $"ResidentialBalcony_T{towerIndex + 1:00}_F{floor + 1:00}_{placement.Suffix}",
                placement.Position,
                placement.Size,
                material);
            balcony.AddToGroup("residential_balcony_profiles");
            balcony.SetMeta("residential_facade_style", style.ToString());
        }
    }

    private void BuildResidentialAuthoredDressing(
        Node3D tower,
        ResidentialTowerSpec spec,
        int towerIndex)
    {
        _residentialTowerArtBuilder ??= new ResidentialTowerArtBuilder();
        var profile = ResidentialTowerDiversityPlan.ForTower(towerIndex);
        _residentialTowerArtResults.Add(_residentialTowerArtBuilder.Build(
            tower,
            profile,
            spec.Footprint,
            spec.Floors * ResidentialFloorHeight));
    }

}