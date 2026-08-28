using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    // Screenshot-matching vault house: 3-storey蓝白楼，底层2车库+1门，上2层各4窗
    // Position chosen at open south ground near Security Checkpoint to avoid existing collisions:
    //  - Original screenshot isolated on flat sand, here near south open area (-38,42) away from FuelDepot(-34,14) / Barracks(25,21)
    public static readonly Vector3 VaultHouseCenter = new(-38.0f, 0.0f, 42.0f);
    private const float VaultWidth = 14.0f;
    private const float VaultDepth = 8.0f;
    private const float VaultGroundHeight = 3.2f;
    private const float VaultUpperHeight = 3.0f;
    private const float VaultTotalHeight = 9.2f;

    private Node3D? _vaultHouseRoot;
    public Vector3 VaultHouseInterior => VaultHouseCenter + new Vector3(0, 0.2f, -1.2f);
    public Vector3 VaultHouseEntry => VaultHouseCenter + new Vector3(0, 0.18f, VaultDepth * 0.5f + 1.4f);

    private void BuildHarborVaultHouse(
        Godot.Material concrete,
        Godot.Material steel,
        Godot.Material steelDark,
        Godot.Material yellow)
    {
        var center = VaultHouseCenter;
        var root = new Node3D { Name = "HarborVaultHouse", Position = center };
        root.AddToGroup("harbor_vault_house");
        root.AddToGroup("freight_terminal_accessible_building");
        _levelRoot.AddChild(root);
        _vaultHouseRoot = root;
        _complexBuildingCount++;
        _complexRoomCount++;
        _complexInteriorPropCount += 6;

        // Materials matching screenshot
        var groundFacade = Mat("vault_ground_facade", new Color(0.78f, 0.83f, 0.86f), 0.04f, 0.85f);
        var upperFacade = Mat("vault_upper_facade", new Color(0.09f, 0.14f, 0.23f), 0.1f, 0.68f);
        var trimDark = Mat("vault_trim_dark", new Color(0.13f, 0.16f, 0.19f), 0.55f, 0.42f);
        var windowGlass = Mat("vault_window_glass", new Color(0.22f, 0.48f, 0.72f, 0.92f), 0.62f, 0.08f);
        windowGlass.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        windowGlass.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        var windowFrame = Mat("vault_window_frame", new Color(0.16f, 0.20f, 0.24f), 0.62f, 0.34f);
        var shutterDark = Mat("vault_shutter_dark", new Color(0.08f, 0.10f, 0.12f), 0.06f, 0.88f);
        var interiorWall = Mat("vault_interior_wall", new Color(0.52f, 0.545f, 0.52f), 0.02f, 0.9f);
        var interiorFloor = Mat("vault_interior_floor", new Color(0.38f, 0.39f, 0.36f), 0.02f, 0.82f);

        // Ground floor: floor slab
        ExpansionBox(root, "VaultFloor_Ground", new Vector3(0, 0.06f, 0), new Vector3(VaultWidth, 0.12f, VaultDepth), interiorFloor);
        // Ground floor walls: North solid, West/East solid, South with main door + 2 fake garage doors
        // North wall
        ExpansionBox(root, "VaultNorth_G", new Vector3(0, VaultGroundHeight * 0.5f, -VaultDepth * 0.5f), new Vector3(VaultWidth, VaultGroundHeight, 0.22f), groundFacade);
        // West/East
        ExpansionBox(root, "VaultWest_G", new Vector3(-VaultWidth * 0.5f, VaultGroundHeight * 0.5f, 0), new Vector3(0.22f, VaultGroundHeight, VaultDepth), groundFacade);
        ExpansionBox(root, "VaultEast_G", new Vector3(VaultWidth * 0.5f, VaultGroundHeight * 0.5f, 0), new Vector3(0.22f, VaultGroundHeight, VaultDepth), groundFacade);

        // South wall segmented: left garage (4.0m), middle garage (4.0m), right door (2.8m) with trim
        // Layout matching screenshot: [ garage 3.8 | wall0.3 | garage 3.8 | wall0.3 | door 2.8 | wall 0? ] centered?
        // Screenshot has garages left/center, door right. Spacing approximated.
        const float garageW = 3.8f;
        const float garageH = 2.4f;
        const float doorW = 2.0f;
        const float doorH = 2.45f;
        // South wall pieces: we build full height wall then carve openings via boxes with header
        // Instead build as 3 segments: left garage surround, middle garage surround, door surround
        // Simpler: South wall is 0.22 thick, full height, with header above openings, side pillars.
        // From west to east: pillar, garage opening, pillar, garage opening, pillar, door opening, pillar
        float curX = -VaultWidth * 0.5f;
        // helper to add south wall segment
        void AddSouthSegment(string name, float x, float w)
        {
            ExpansionBox(root, name, new Vector3(x, VaultGroundHeight * 0.5f, VaultDepth * 0.5f), new Vector3(w, VaultGroundHeight, 0.22f), groundFacade);
        }
        // West pillar 1
        AddSouthSegment("VaultSouth_PillarW1", curX + 0.6f, 1.2f);
        curX += 1.2f;
        // Garage 1 opening header handled: we add header later, here just side pillars, bottom open
        // For collision, we need opening: so we don't build full wall at opening, only header above garageH
        // Approach: pillar, then opening is empty up to garageH, header from garageH to top
        ExpansionBox(root, "VaultSouth_Garage1_Header", new Vector3(curX + garageW * 0.5f, garageH + (VaultGroundHeight - garageH) * 0.5f, VaultDepth * 0.5f), new Vector3(garageW, VaultGroundHeight - garageH, 0.22f), groundFacade);
        // Garage shutter visual (recessed dark) + solid collision shutter (closed, not enterable)
        MeshBox(root, new Vector3(curX + garageW * 0.5f, garageH * 0.5f, VaultDepth * 0.5f - 0.06f), new Vector3(garageW - 0.12f, garageH - 0.08f, 0.04f), shutterDark);
        ExpansionBox(root, "VaultSouth_Garage1_ShutterCollision", new Vector3(curX + garageW * 0.5f, garageH * 0.5f, VaultDepth * 0.5f), new Vector3(garageW - 0.06f, garageH - 0.02f, 0.18f), shutterDark);
        // Garage frame trim
        MeshBox(root, new Vector3(curX + garageW * 0.5f, garageH * 0.5f, VaultDepth * 0.5f + 0.04f), new Vector3(garageW + 0.28f, garageH + 0.18f, 0.06f), trimDark);
        curX += garageW;
        // Middle pillar 0.6
        AddSouthSegment("VaultSouth_PillarM", curX + 0.3f, 0.6f);
        curX += 0.6f;
        // Garage 2
        ExpansionBox(root, "VaultSouth_Garage2_Header", new Vector3(curX + garageW * 0.5f, garageH + (VaultGroundHeight - garageH) * 0.5f, VaultDepth * 0.5f), new Vector3(garageW, VaultGroundHeight - garageH, 0.22f), groundFacade);
        MeshBox(root, new Vector3(curX + garageW * 0.5f, garageH * 0.5f, VaultDepth * 0.5f - 0.06f), new Vector3(garageW - 0.12f, garageH - 0.08f, 0.04f), shutterDark);
        ExpansionBox(root, "VaultSouth_Garage2_ShutterCollision", new Vector3(curX + garageW * 0.5f, garageH * 0.5f, VaultDepth * 0.5f), new Vector3(garageW - 0.06f, garageH - 0.02f, 0.18f), shutterDark);
        MeshBox(root, new Vector3(curX + garageW * 0.5f, garageH * 0.5f, VaultDepth * 0.5f + 0.04f), new Vector3(garageW + 0.28f, garageH + 0.18f, 0.06f), trimDark);
        curX += garageW;
        // Pillar between garage2 and door
        AddSouthSegment("VaultSouth_PillarM2", curX + 0.55f, 1.1f);
        curX += 1.1f;
        // Door opening header
        ExpansionBox(root, "VaultSouth_DoorHeader", new Vector3(curX + doorW * 0.5f, doorH + (VaultGroundHeight - doorH) * 0.5f, VaultDepth * 0.5f), new Vector3(doorW, VaultGroundHeight - doorH, 0.22f), groundFacade);
        // Door frame trim visual
        MeshBox(root, new Vector3(curX + doorW * 0.5f, doorH * 0.5f + 0.02f, VaultDepth * 0.5f + 0.04f), new Vector3(doorW + 0.32f, doorH + 0.22f, 0.06f), trimDark);
        curX += doorW;
        // East pillar remaining
        var remainingW = VaultWidth * 0.5f - curX;
        if (remainingW > 0.2f)
        {
            AddSouthSegment("VaultSouth_PillarE", curX + remainingW * 0.5f, remainingW);
        }

        // Upper floors exterior shells (2 floors)
        for (int floor = 1; floor <= 2; floor++)
        {
            float y0 = VaultGroundHeight + (floor - 1) * VaultUpperHeight;
            float yCenter = y0 + VaultUpperHeight * 0.5f;
            // North wall
            ExpansionBox(root, $"VaultNorth_F{floor}", new Vector3(0, yCenter, -VaultDepth * 0.5f), new Vector3(VaultWidth, VaultUpperHeight, 0.22f), upperFacade);
            ExpansionBox(root, $"VaultWest_F{floor}", new Vector3(-VaultWidth * 0.5f, yCenter, 0), new Vector3(0.22f, VaultUpperHeight, VaultDepth), upperFacade);
            ExpansionBox(root, $"VaultEast_F{floor}", new Vector3(VaultWidth * 0.5f, yCenter, 0), new Vector3(0.22f, VaultUpperHeight, VaultDepth), upperFacade);
            ExpansionBox(root, $"VaultSouth_F{floor}", new Vector3(0, yCenter, VaultDepth * 0.5f), new Vector3(VaultWidth, VaultUpperHeight, 0.22f), upperFacade);
            // Intermediate floor slab (between ground and 1st, 1st and 2nd)
            ExpansionBox(root, $"VaultSlab_F{floor}", new Vector3(0, y0 + 0.06f, 0), new Vector3(VaultWidth - 0.44f, 0.12f, VaultDepth - 0.44f), interiorFloor);
            // Roof for top floor
            if (floor == 2)
            {
                ExpansionBox(root, "VaultRoof", new Vector3(0, y0 + VaultUpperHeight + 0.12f, 0), new Vector3(VaultWidth + 0.5f, 0.24f, VaultDepth + 0.5f), upperFacade);
                // Roof trim
                MeshBox(root, new Vector3(0, y0 + VaultUpperHeight + 0.26f, 0), new Vector3(VaultWidth + 0.34f, 0.08f, VaultDepth + 0.34f), trimDark);
            }
            // Windows: 4 per south/north face per floor matching screenshot
            BuildVaultWindows(root, y0, VaultUpperHeight, windowGlass, windowFrame, upperFacade, trimDark, isSouth: true);
            BuildVaultWindows(root, y0, VaultUpperHeight, windowGlass, windowFrame, upperFacade, trimDark, isSouth: false);
        }

        // Real interactive door at right garage-door position's neighbor
        // Door mount at south face door center
        float doorCenterX = VaultWidth * 0.5f - doorW * 0.5f - remainingW - 0.05f; // approx
        // recompute exact door center: curX before east pillar is door left, so door center = curX - doorW*0.5? Wait curX tracked.
        // Simpler: place door at x = 4.2f (empirical to align right side)
        var doorMountPos = new Vector3(4.55f, 0, VaultDepth * 0.5f + 0.14f);
        AddIndustrialDoor(root, "HarborVaultDoor", doorMountPos, doorW, doorH, 220.0f);

        // Interior vault room setup
        BuildVaultInterior(center, root, interiorWall, interiorFloor, steel, steelDark, yellow);

        // Cover points for AI around building
        RegisterCoverPoint(center + new Vector3(-VaultWidth * 0.5f - 1.4f, 0, -VaultDepth * 0.5f - 1.4f));
        RegisterCoverPoint(center + new Vector3(VaultWidth * 0.5f + 1.4f, 0, -VaultDepth * 0.5f - 1.4f));
        RegisterCoverPoint(center + new Vector3(-VaultWidth * 0.5f - 1.4f, 0, VaultDepth * 0.5f + 1.4f));
        RegisterCoverPoint(center + new Vector3(VaultWidth * 0.5f + 1.4f, 0, VaultDepth * 0.5f + 1.4f));

        root.AddChild(new Label3D
        {
            Name = "VaultHouseLabel",
            Position = new Vector3(0, VaultTotalHeight + 0.7f, 0),
            Text = "VAULT HOUSE  //  SECURE STORAGE",
            FontSize = 24,
            OutlineSize = 6,
            Modulate = new Color(0.82f, 0.72f, 0.35f),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            VisibilityRangeEnd = 80.0f
        });
    }

    private void BuildVaultWindows(Node3D parent, float y0, float height, Godot.Material glass, Godot.Material frame, Godot.Material recess, Godot.Material trim, bool isSouth)
    {
        float z = isSouth ? VaultDepth * 0.5f : -VaultDepth * 0.5f;
        float face = isSouth ? 1 : -1;
        // 4 windows: x positions -4.6, -1.5, 1.5, 4.6  widths: small 2.0, large 2.6 alternating to match screenshot [small, medium, medium, small?]
        // Screenshot top: 2 small left, 2 large mid? We'll use 2.1 narrow outer, 2.7 wider inner
        var winDefs = new (float X, float W)[]
        {
            (-4.55f, 2.05f),
            (-1.55f, 2.65f),
            (1.55f, 2.65f),
            (4.55f, 2.05f)
        };
        float winH = 1.35f;
        float winY = y0 + height * 0.5f;
        foreach (var def in winDefs)
        {
            // Recess dark box
            MeshBox(parent, new Vector3(def.X, winY, z + face * 0.02f), new Vector3(def.W + 0.14f, winH + 0.14f, 0.06f), recess);
            // Glass
            var g = new MeshInstance3D
            {
                Name = $"VaultWindowGlass_{(isSouth ? "S" : "N")}_{def.X:0.0}",
                Position = new Vector3(def.X, winY, z + face * 0.08f),
                Mesh = new PlaneMesh { Size = new Vector2(def.W, winH) },
                MaterialOverride = glass
            };
            // Orient correctly
            g.RotationDegrees = new Vector3(isSouth ? 0 : 180, 0, 0);
            // Plane is horizontal, rotate to vertical facing Z
            // PlaneMesh faces +Y, so rotate 90 deg around X
            g.RotationDegrees = new Vector3(isSouth ? -90 : 90, 0, 0);
            parent.AddChild(g);
            // Frame top/bottom/left/right thin
            MeshBox(parent, new Vector3(def.X, winY + winH * 0.5f + 0.06f, z + face * 0.06f), new Vector3(def.W + 0.22f, 0.14f, 0.08f), frame);
            MeshBox(parent, new Vector3(def.X, winY - winH * 0.5f - 0.06f, z + face * 0.06f), new Vector3(def.W + 0.22f, 0.14f, 0.08f), frame);
            MeshBox(parent, new Vector3(def.X - def.W * 0.5f - 0.04f, winY, z + face * 0.06f), new Vector3(0.14f, winH, 0.08f), frame);
            MeshBox(parent, new Vector3(def.X + def.W * 0.5f + 0.04f, winY, z + face * 0.06f), new Vector3(0.14f, winH, 0.08f), frame);
            // Sill trim
            MeshBox(parent, new Vector3(def.X, winY - winH * 0.5f - 0.14f, z + face * 0.10f), new Vector3(def.W + 0.32f, 0.07f, 0.16f), trim);
        }
        // Floor band trim between storeys
        MeshBox(parent, new Vector3(0, y0 + 0.08f, z + face * 0.04f), new Vector3(VaultWidth, 0.14f, 0.08f), trim);
    }

    private void BuildVaultInterior(Vector3 center, Node3D parent, Godot.Material interiorWall, Godot.Material interiorFloor, Godot.Material steel, Godot.Material steelDark, Godot.Material yellow)
    {
        // Interior: single vault room occupies ground floor inner 11x6, with reinforced north wall where safe sits
        // Already floor done. Add inner partition creating vault chamber against north
        float roomW = 10.2f;
        float roomD = 6.2f;
        float roomH = 2.7f;
        // North reinforced wall inside (thick)
        ExpansionBox(parent, "VaultInnerNorthReinforced", new Vector3(0, roomH * 0.5f, -VaultDepth * 0.5f + roomD * 0.5f - 0.1f), new Vector3(roomW, roomH, 0.42f), steelDark);
        // Side safes wall? Just visual panels
        // Safe modeling: try load external GLB first, fallback procedural
        Vector3 safePos = new Vector3(0, 0.08f, -VaultDepth * 0.5f + 1.35f);
        Node3D safeNode;
        if (!TryLoadExternalSafe(parent, safePos, out safeNode))
        {
            safeNode = BuildProceduralSafe(safePos);
            parent.AddChild(safeNode);
        }

        // Vault door frame reinforcement around safe (decor)
        MeshBox(parent, new Vector3(0, 1.05f, -VaultDepth * 0.5f + 1.45f), new Vector3(2.2f, 1.8f, 0.12f), steel);

        // Interior props: desk, cabinet, crates
        ExpansionBox(parent, "VaultDesk", new Vector3(-2.6f, 0.42f, 1.2f), new Vector3(2.2f, 0.12f, 0.9f), interiorWall);
        MeshBox(parent, new Vector3(-2.6f, 0.78f, 1.2f), new Vector3(1.95f, 0.52f, 0.68f), steelDark);
        ExpansionBox(parent, "VaultCabinet", new Vector3(3.2f, 0.85f, 1.0f), new Vector3(0.72f, 1.45f, 0.55f), steel);
        ExpansionBox(parent, "VaultCrateA", new Vector3(2.1f, 0.38f, 1.55f), new Vector3(0.95f, 0.68f, 0.85f), yellow);
        ExpansionBox(parent, "VaultCrateB", new Vector3(-3.4f, 0.38f, -0.6f), new Vector3(0.85f, 0.68f, 0.78f), yellow);

        // Lighting: warm interior + safe glow
        parent.AddChild(new OmniLight3D
        {
            Name = "VaultInteriorLight",
            Position = new Vector3(0, 2.45f, -0.2f),
            LightColor = new Color(1.0f, 0.88f, 0.62f),
            LightEnergy = 1.45f,
            OmniRange = 10.5f,
            ShadowEnabled = false
        });
        parent.AddChild(new OmniLight3D
        {
            Name = "VaultSafeSpot",
            Position = safePos + new Vector3(0, 1.45f, 0.6f),
            LightColor = new Color(0.32f, 0.78f, 1.0f),
            LightEnergy = 0.85f,
            OmniRange = 4.8f,
            ShadowEnabled = false
        });

        // Loot placements via ComplexLoot placements (deferred to SpawnBuildingGradedLoot)
        // Safe loot must be south of the reinforced north wall (wall spans z -1.21 to -0.79 local), so place at -0.5
        var safeLootPos = center + safePos + new Vector3(0, 0.52f, 2.15f); // world ≈ (-38,0.60,41.5) south of wall, clear approach from door
        _complexLootPlacements.Add(new ComplexLootPlacement(safeLootPos, LootGrade.Legendary, "Vault House master safe", "保险库主保险柜"));

        // Side cache near east wall, lifted above floor to avoid floor collision and cabinet
        var sideLootPos = center + new Vector3(3.2f, 0.60f, 1.0f);
        _complexLootPlacements.Add(new ComplexLootPlacement(sideLootPos, LootGrade.Rare, "Vault side cache", "保险库侧柜"));

        // Desk stash just above desk top (desk top 0.48) so place at 0.65
        var deskLootPos = center + new Vector3(-2.6f, 0.65f, 1.2f);
        _complexLootPlacements.Add(new ComplexLootPlacement(deskLootPos, LootGrade.Uncommon, "Vault desk stash", "保险库桌柜"));

        // Register vault loot group for diagnostics
        _specialLandmarkLootCount += 0; // not special, counted via complex
    }

    private bool TryLoadExternalSafe(Node3D parent, Vector3 pos, out Node3D node)
    {
        node = null!;
        // Attempt to load CC0 external safe GLB if present.
        // Expected paths: res://assets/models/safe/low_poly_safe.glb (Sketchfab CC-BY) or res://assets/models/vault/vault_door.glb (Hard Cash CC0)
        string[] tryPaths = new[]
        {
            "res://assets/models/safe/low_poly_safe.glb",
            "res://assets/models/vault/vault_door.glb",
            "res://assets/models/safe/safe.glb"
        };
        foreach (var path in tryPaths)
        {
            if (!ResourceLoader.Exists(path)) continue;
            var scene = GD.Load<PackedScene>(path);
            if (scene?.Instantiate() is Node3D model)
            {
                var body = new StaticBody3D
                {
                    Name = "VaultSafe_External",
                    Position = pos,
                    CollisionLayer = 1,
                    CollisionMask = 0
                };
                model.Name = "Model";
                // Normalize scale: external safe ~1m tall, scale to 1.3m
                model.Scale = Vector3.One * 1.15f;
                ConfigureAuthoredMapModel(model, 80f, true);
                body.AddChild(model);
                // Collision approximate
                body.AddChild(new CollisionShape3D
                {
                    Name = "VaultCollision",
                    Position = new Vector3(0, 0.62f, 0),
                    Shape = new BoxShape3D { Size = new Vector3(1.15f, 1.25f, 0.85f) }
                });
                parent.AddChild(body);
                node = body;
                _complexInteriorPropCount++;
                GD.Print($"VAULT_SAFE_LOAD ok path={path} pos={pos}");
                return true;
            }
        }
        return false;
    }

    private Node3D BuildProceduralSafe(Vector3 pos)
    {
        // High-fidelity procedural safe approximating internet CC0 reference (Hard Cash / Low Poly Safe)
        // Body + door + combination dial + handle + bolts + emissive keypad
        var root = new StaticBody3D
        {
            Name = "VaultSafe_Procedural",
            Position = pos,
            CollisionLayer = 1,
            CollisionMask = 0
        };
        var bodyMat = Mat("vault_safe_body", new Color(0.14f, 0.15f, 0.145f), 0.68f, 0.38f);
        var doorMat = Mat("vault_safe_door", new Color(0.19f, 0.195f, 0.185f), 0.68f, 0.32f);
        var boltMat = Mat("vault_safe_bolt", new Color(0.72f, 0.68f, 0.22f), 0.62f, 0.34f);
        var dialMat = Mat("vault_safe_dial", new Color(0.08f, 0.08f, 0.085f), 0.82f, 0.22f);
        var keypadMat = Mat("vault_safe_keypad", new Color(0.02f, 0.18f, 0.28f), 0.12f, 0.32f, new Color(0.02f, 0.45f, 0.68f));
        var handleMat = Mat("vault_safe_handle", new Color(0.18f, 0.18f, 0.17f), 0.72f, 0.28f);

        // Main body 1.15 x 1.25 x 0.82
        var body = new MeshInstance3D { Mesh = SharedBoxMesh(new Vector3(1.15f, 1.25f, 0.82f)), MaterialOverride = bodyMat, Position = new Vector3(0, 0.625f, 0) };
        root.AddChild(body);
        root.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(1.15f, 1.25f, 0.82f) }, Position = new Vector3(0, 0.625f, 0) });

        // Door slab 1.02 x 1.12 x 0.09 recessed front
        var door = new MeshInstance3D { Mesh = SharedBoxMesh(new Vector3(1.02f, 1.12f, 0.09f)), MaterialOverride = doorMat, Position = new Vector3(0, 0.625f, 0.44f) };
        root.AddChild(door);
        // Door frame
        var frame = new MeshInstance3D { Mesh = SharedBoxMesh(new Vector3(1.12f, 1.22f, 0.06f)), MaterialOverride = bodyMat, Position = new Vector3(0, 0.625f, 0.40f) };
        root.AddChild(frame);

        // Bolts 4 sides
        foreach (var y in new[] { 0.28f, 0.62f, 0.96f })
        {
            var boltL = new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.045f, BottomRadius = 0.045f, Height = 0.08f, RadialSegments = 12 }, MaterialOverride = boltMat, Position = new Vector3(-0.48f, y, 0.50f), Rotation = new Vector3(0, 0, Mathf.Pi * 0.5f) };
            var boltR = new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.045f, BottomRadius = 0.045f, Height = 0.08f, RadialSegments = 12 }, MaterialOverride = boltMat, Position = new Vector3(0.48f, y, 0.50f), Rotation = new Vector3(0, 0, Mathf.Pi * 0.5f) };
            root.AddChild(boltL); root.AddChild(boltR);
        }
        // Combination dial
        var dial = new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.16f, BottomRadius = 0.16f, Height = 0.06f, RadialSegments = 24 }, MaterialOverride = dialMat, Position = new Vector3(0, 0.68f, 0.51f), Rotation = new Vector3(Mathf.Pi * 0.5f, 0, 0) };
        root.AddChild(dial);
        // Dial handle
        var dialHandle = new MeshInstance3D { Mesh = SharedBoxMesh(new Vector3(0.18f, 0.04f, 0.04f)), MaterialOverride = handleMat, Position = new Vector3(0, 0.68f, 0.56f) };
        root.AddChild(dialHandle);
        // Main wheel handle (3 spokes)
        for (int i = 0; i < 3; i++)
        {
            float ang = i * Mathf.Tau / 3.0f;
            var spoke = new MeshInstance3D { Mesh = SharedBoxMesh(new Vector3(0.42f, 0.06f, 0.06f)), MaterialOverride = handleMat, Position = new Vector3(Mathf.Cos(ang) * 0.0f, 0.62f, 0.51f), Rotation = new Vector3(0, 0, ang) };
            // offset outward
            spoke.Position = new Vector3(Mathf.Cos(ang) * 0.18f, 0.42f + Mathf.Sin(ang) * 0.05f + 0.2f, 0.52f);
            root.AddChild(spoke);
            var knob = new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.06f, Height = 0.12f, RadialSegments = 10, Rings = 6 }, MaterialOverride = boltMat, Position = new Vector3(Mathf.Cos(ang) * 0.22f, 0.42f + Mathf.Sin(ang) * 0.22f, 0.52f) };
            root.AddChild(knob);
        }
        var wheel = new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = 0.18f, OuterRadius = 0.22f, Rings = 24, RingSegments = 10 }, MaterialOverride = handleMat, Position = new Vector3(0, 0.42f, 0.52f), Rotation = new Vector3(Mathf.Pi * 0.5f, 0, 0) };
        root.AddChild(wheel);
        // Keypad emissive
        var keypad = new MeshInstance3D { Mesh = SharedBoxMesh(new Vector3(0.24f, 0.16f, 0.02f)), MaterialOverride = keypadMat, Position = new Vector3(0.32f, 0.72f, 0.51f) };
        root.AddChild(keypad);
        // Hinge
        var hinge = new MeshInstance3D { Mesh = SharedBoxMesh(new Vector3(0.06f, 1.02f, 0.06f)), MaterialOverride = boltMat, Position = new Vector3(0.54f, 0.625f, 0.45f) };
        root.AddChild(hinge);

        _complexInteriorPropCount += 4;
        return root;
    }
}
