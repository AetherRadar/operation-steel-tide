using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    /// <summary>
    /// Adds the small-scale structure that the tower shells were missing: service rooms,
    /// covered corners, exposed utility runs, and a readable stair core on every floor.
    /// The modules sit outside the navigation spine, so the original entry and stair probes
    /// remain meaningful while the empty courtyards gain believable cover and purpose.
    /// </summary>
    private void BuildResidentialGapInfill(
        Node3D community,
        Godot.Material concrete,
        Godot.Material steel,
        Godot.Material glass,
        Godot.Material trim)
    {
        var infillRoot = new Node3D { Name = "ResidentialGapInfill" };
        community.AddChild(infillRoot);
        var paving = Mat("residential_infill_paving", new Color(0.28f, 0.3f, 0.29f), 0.02f, 0.9f);
        var shell = Mat("residential_infill_shell", new Color(0.19f, 0.24f, 0.24f), 0.28f, 0.72f);
        var shellAlt = Mat("residential_infill_shell_alt", new Color(0.25f, 0.22f, 0.19f), 0.18f, 0.78f);
        var utility = Mat("residential_infill_utility", new Color(0.08f, 0.105f, 0.105f), 0.55f, 0.46f);
        var warning = Mat("residential_infill_warning", new Color(0.88f, 0.55f, 0.16f), 0.05f, 0.5f, new Color(0.72f, 0.28f, 0.06f));
        var green = Mat("residential_infill_medical", new Color(0.22f, 0.76f, 0.48f), 0.08f, 0.55f, new Color(0.12f, 0.46f, 0.28f));

        for (var towerIndex = 0; towerIndex < _residentialTowers.Count; towerIndex++)
        {
            var tower = _residentialTowers[towerIndex];
            var spec = ResidentialTowerSpecs[towerIndex];
            var accent = Mat($"residential_infill_accent_{towerIndex % 4}", spec.Accent * 0.72f, 0.12f, 0.68f);
            BuildTowerCornerModules(tower, spec, towerIndex, shell, shellAlt, utility, warning, green, accent, trim);
            BuildTowerCourtyardDressing(tower, spec, towerIndex, paving, utility, warning, glass, concrete);
        }

        foreach (var pair in new (int From, int To)[]
        {
            (7, 8), (8, 9), (9, 10), (2, 3), (3, 4), (0, 5), (1, 6)
        })
        {
            var from = ResidentialTowerSpecs[pair.From].Position + Vector3.Up * (5.2f + pair.From % 2 * 0.55f);
            var to = ResidentialTowerSpecs[pair.To].Position + Vector3.Up * (5.2f + pair.To % 2 * 0.55f);
            BuildResidentialUtilitySpan(infillRoot, pair.From, pair.To, from, to, utility, warning);
        }
    }

    private static void BuildResidentialUtilitySpan(
        Node3D root,
        int fromIndex,
        int toIndex,
        Vector3 from,
        Vector3 to,
        Godot.Material utility,
        Godot.Material warning)
    {
        var delta = to - from;
        delta.Y = 0.0f;
        var length = delta.Length();
        if (length < 4.0f)
        {
            return;
        }
        var yaw = Mathf.Atan2(delta.X, delta.Z);
        var center = (from + to) * 0.5f;
        var lateral = new Vector3(Mathf.Cos(yaw), 0.0f, -Mathf.Sin(yaw));
        for (var cable = -1; cable <= 1; cable++)
        {
            var mesh = MeshBox(
                root,
                center + lateral * cable * 0.22f + Vector3.Down * Mathf.Abs(cable) * 0.12f,
                new Vector3(0.075f, 0.075f, length),
                cable == 0 ? warning : utility,
                new Vector3(0, yaw, 0));
            mesh.Name = $"ResidentialUtilitySpan_{fromIndex}_{toIndex}_{cable + 1}";
        }
    }

    private void BuildTowerCornerModules(
        Node3D tower,
        ResidentialTowerSpec spec,
        int towerIndex,
        Godot.Material shell,
        Godot.Material shellAlt,
        Godot.Material utility,
        Godot.Material warning,
        Godot.Material green,
        Godot.Material accent,
        Godot.Material trim)
    {
        var width = spec.Footprint.X;
        var depth = spec.Footprint.Y;
        var cornerNumber = 0;
        foreach (var sx in new[] { -1.0f, 1.0f })
        {
            foreach (var sz in new[] { -1.0f, 1.0f })
            {
                var corner = new Vector3(
                    sx * (width * 0.5f + 1.25f),
                    1.18f,
                    sz * (depth * 0.5f + 1.25f));
                var moduleMaterial = (towerIndex + cornerNumber) % 2 == 0 ? shell : shellAlt;
                var module = ExpansionBox(
                    tower,
                    $"ResidentialInfill_T{towerIndex + 1:00}_C{cornerNumber:00}",
                    corner,
                    new Vector3(2.35f, 2.36f, 2.35f),
                    moduleMaterial);
                module.AddToGroup("residential_infill");
                _residentialInfillModuleCount++;

                // A shallow roof and a bright face marker give each annex a purpose at a glance.
                MeshBox(module, new Vector3(0, 1.25f, 0), new Vector3(2.62f, 0.16f, 2.62f), trim);
                var faceZ = sz > 0 ? -1.19f : 1.19f;
                MeshBox(module, new Vector3(0, -0.05f, faceZ), new Vector3(1.3f, 1.8f, 0.035f), utility);
                MeshBox(module, new Vector3(0, 0.18f, faceZ + (sz > 0 ? -0.03f : 0.03f)), new Vector3(0.78f, 0.08f, 0.05f), cornerNumber % 3 == 0 ? green : warning);
                MeshBox(module, new Vector3(0, -0.44f, faceZ), new Vector3(0.72f, 0.05f, 0.05f), accent);

                var ductX = sx > 0 ? -0.82f : 0.82f;
                MeshBox(module, new Vector3(ductX, 0.18f, sz * 0.15f), new Vector3(0.14f, 1.9f, 0.14f), trim);
                MeshBox(module, new Vector3(ductX, 0.98f, 0), new Vector3(0.75f, 0.13f, 0.13f), trim);
                // Low crates sit outside the walking line and make the corner usable cover.
                var crateOffset = new Vector3(sx * 1.55f, -0.78f, sz * 0.25f);
                ExpansionBox(module, $"ResidentialInfillCrate_T{towerIndex + 1:00}_C{cornerNumber:00}", crateOffset, new Vector3(0.52f, 0.68f, 0.62f), accent);
                MeshBox(module, crateOffset + new Vector3(0, 0.38f, 0), new Vector3(0.56f, 0.06f, 0.66f), warning);

                tower.AddChild(new Label3D
                {
                    Name = $"ResidentialInfillLabel_T{towerIndex + 1:00}_C{cornerNumber:00}",
                    Position = corner + new Vector3(0, 1.48f, sz * 0.08f),
                    Text = cornerNumber % 3 == 0 ? "MEDICAL / RESERVE" : cornerNumber % 3 == 1 ? "UTILITY / TENANT" : "SECURITY / SERVICE",
                    FontSize = 12,
                    OutlineSize = 4,
                    Modulate = cornerNumber % 3 == 0
                        ? new Color(0.22f, 0.9f, 0.56f)
                        : new Color(1.0f, 0.64f, 0.2f),
                    Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                    VisibilityRangeEnd = 18.0f
                });
                cornerNumber++;
            }
        }
    }

    private void BuildTowerCourtyardDressing(
        Node3D tower,
        ResidentialTowerSpec spec,
        int towerIndex,
        Godot.Material paving,
        Godot.Material utility,
        Godot.Material warning,
        Godot.Material glass,
        Godot.Material concrete)
    {
        var width = spec.Footprint.X;
        var depth = spec.Footprint.Y;
        var side = Mathf.Min(width * 0.5f - 2.0f, 7.0f);
        var serviceZ = -Mathf.Min(depth * 0.18f, 3.6f);
        // Narrow paving bands turn the courtyard corners into readable lanes instead of a
        // single empty slab. Mesh-only markings cannot trap a player or vehicle.
        MeshBox(tower, new Vector3(-side, 0.095f, serviceZ), new Vector3(0.14f, 0.035f, 6.8f), paving);
        MeshBox(tower, new Vector3(side, 0.095f, serviceZ), new Vector3(0.14f, 0.035f, 6.8f), paving);
        MeshBox(tower, new Vector3(-side * 0.5f, 0.1f, -depth * 0.31f), new Vector3(Mathf.Max(2.0f, side), 0.035f, 0.14f), paving);
        MeshBox(tower, new Vector3(side * 0.5f, 0.1f, -depth * 0.31f), new Vector3(Mathf.Max(2.0f, side), 0.035f, 0.14f), paving);

        // Wall-mounted condensers, cable trays and a rainwater tank occupy the otherwise
        // blank side elevations without introducing another tall collision wall.
        for (var sideSign = -1.0f; sideSign <= 1.0f; sideSign += 2.0f)
        {
            var x = sideSign * (width * 0.5f + 0.24f);
            for (var row = 0; row < 3; row++)
            {
                var z = serviceZ - 2.1f + row * 2.1f;
                MeshBox(tower, new Vector3(x, 0.82f + row * 0.46f, z), new Vector3(0.12f, 0.56f, 0.84f), utility);
                MeshBox(tower, new Vector3(x + sideSign * 0.08f, 1.12f + row * 0.46f, z), new Vector3(0.06f, 0.08f, 0.98f), warning);
            }
            MeshBox(tower, new Vector3(x, 2.55f, serviceZ), new Vector3(0.1f, 0.12f, 6.9f), glass);
        }
        ExpansionCylinder(tower, $"ResidentialRainTank_T{towerIndex + 1:00}", new Vector3(width * 0.28f, 1.22f, -depth * 0.42f), 0.48f, 2.15f, concrete);
        MeshBox(tower, new Vector3(width * 0.28f, 2.34f, -depth * 0.42f), new Vector3(0.82f, 0.08f, 0.82f), warning);
    }

    private void BuildTowerStairDetails(
        Node3D tower,
        ResidentialTowerSpec spec,
        int towerIndex,
        int floor,
        float floorY,
        float coreZ,
        Godot.Material rail,
        Godot.Material light)
    {
        const float run = ResidentialStairRun;
        const float halfRise = ResidentialFloorHeight * 0.5f;
        const float edge = 0.99f;
        var angle = Mathf.Atan2(halfRise, run);
        var lowerStartZ = coreZ - run * 0.5f;
        var upperStartZ = coreZ + run * 0.5f;
        var handrail = Mat("residential_stair_handrail", new Color(0.22f, 0.29f, 0.29f), 0.72f, 0.28f);
        var baluster = Mat("residential_stair_baluster", new Color(0.46f, 0.51f, 0.48f), 0.4f, 0.46f);
        var panel = Mat("residential_stair_panel", new Color(0.12f, 0.17f, 0.18f), 0.38f, 0.55f);
        var safety = Mat("residential_stair_safety", new Color(0.88f, 0.54f, 0.16f), 0.05f, 0.5f, new Color(0.55f, 0.22f, 0.05f));
        var lowerCenterZ = (lowerStartZ + upperStartZ) * 0.5f;
        var handrailTransforms = new List<Transform3D>(4);
        var safetyRailTransforms = new List<Transform3D>(4);
        var balusterTransforms = new List<Transform3D>(28);

        foreach (var flight in new[] { 0, 1 })
        {
            var centerX = flight == 0 ? -1.45f : 1.45f;
            var centerZ = flight == 0 ? lowerCenterZ : lowerCenterZ;
            var sign = flight == 0 ? -1.0f : 1.0f;
            var localAngle = flight == 0 ? angle : -angle;
            foreach (var x in new[] { centerX - edge, centerX + edge })
            {
                var y = floorY + (flight == 0 ? 0.78f : 2.35f);
                var railBasis = Basis.FromEuler(new Vector3(localAngle, 0, 0));
                handrailTransforms.Add(new Transform3D(railBasis, new Vector3(x, y, centerZ)));
                safetyRailTransforms.Add(new Transform3D(railBasis, new Vector3(x, y - 0.35f, centerZ)));
                for (var post = 0; post <= 6; post++)
                {
                    var t = post / 6.0f;
                    var z = flight == 0
                        ? Mathf.Lerp(upperStartZ - 0.35f, lowerStartZ + 0.35f, t)
                        : Mathf.Lerp(lowerStartZ + 0.35f, upperStartZ - 0.35f, t);
                    var top = y + sign * (t - 0.5f) * halfRise;
                    balusterTransforms.Add(new Transform3D(Basis.Identity, new Vector3(x, top - 0.38f, z)));
                }
            }
        }
        AddResidentialStairDetailBatch(tower, $"ResidentialStairHandrails_T{towerIndex + 1:00}_F{floor + 1:00}", new Vector3(0.075f, 0.075f, run + 0.25f), handrail, handrailTransforms);
        AddResidentialStairDetailBatch(tower, $"ResidentialStairSafetyRails_T{towerIndex + 1:00}_F{floor + 1:00}", new Vector3(0.05f, 0.05f, run + 0.25f), safety, safetyRailTransforms);
        AddResidentialStairDetailBatch(tower, $"ResidentialStairBalusters_T{towerIndex + 1:00}_F{floor + 1:00}", new Vector3(0.055f, 0.76f, 0.055f), baluster, balusterTransforms);

        var landingNorth = lowerStartZ + 0.2f - 2.8f;
        var landingCenter = landingNorth + 1.4f;
        // Dense wall panels and cable risers make the shaft read as a maintained building,
        // while their visual-only meshes cannot create a new collision trap.
        var panelTransforms = new List<Transform3D>(2);
        var lightTransforms = new List<Transform3D>(2);
        var landingSafetyTransforms = new List<Transform3D>(2);
        foreach (var x in new[] { -2.72f, 2.72f })
        {
            panelTransforms.Add(new Transform3D(Basis.Identity, new Vector3(x, floorY + 1.35f, landingCenter)));
            lightTransforms.Add(new Transform3D(Basis.Identity, new Vector3(x + (x < 0 ? 0.05f : -0.05f), floorY + 2.35f, landingCenter)));
            landingSafetyTransforms.Add(new Transform3D(Basis.Identity, new Vector3(x + (x < 0 ? 0.08f : -0.08f), floorY + 1.1f, landingCenter - 0.75f)));
        }
        AddResidentialStairDetailBatch(tower, $"ResidentialStairWallPanels_T{towerIndex + 1:00}_F{floor + 1:00}", new Vector3(0.08f, 2.15f, 2.25f), panel, panelTransforms);
        AddResidentialStairDetailBatch(tower, $"ResidentialStairWallLights_T{towerIndex + 1:00}_F{floor + 1:00}", new Vector3(0.05f, 0.08f, 1.65f), light, lightTransforms);
        AddResidentialStairDetailBatch(tower, $"ResidentialStairLandingPosts_T{towerIndex + 1:00}_F{floor + 1:00}", new Vector3(0.055f, 0.72f, 0.055f), safety, landingSafetyTransforms);
        var pipeTransforms = new List<Transform3D>(3);
        for (var pipe = -1; pipe <= 1; pipe++)
        {
            pipeTransforms.Add(new Transform3D(Basis.Identity, new Vector3(pipe * 0.18f, floorY + 1.52f, landingCenter + 1.0f)));
        }
        AddResidentialStairDetailBatch(tower, $"ResidentialStairRisers_T{towerIndex + 1:00}_F{floor + 1:00}", new Vector3(0.07f, 2.45f, 0.07f), baluster, pipeTransforms);
        var locker = ExpansionBox(
            tower,
            $"ResidentialStairUtilityLocker_T{towerIndex + 1:00}_F{floor + 1:00}",
            new Vector3(2.18f, floorY + 0.72f, landingCenter + 0.68f),
            new Vector3(0.58f, 1.32f, 0.5f),
            panel);
        locker.AddToGroup("residential_stair_details");
        var lockerAccent = MeshBox(locker, new Vector3(0, 0.0f, -0.27f), new Vector3(0.22f, 0.52f, 0.04f), safety);
        lockerAccent.Name = $"ResidentialStairLockerAccent_T{towerIndex + 1:00}_F{floor + 1:00}";
        RegisterMapDetailVisual(lockerAccent);
        tower.AddChild(new Label3D
        {
            Name = $"ResidentialStairFloorLabel_T{towerIndex + 1:00}_F{floor + 1:00}",
            Position = new Vector3(0, floorY + 2.18f, landingNorth + 0.2f),
            Text = $"FLOOR {floor + 1:00}  //  EXIT {floor + 2:00}",
            FontSize = 15,
            OutlineSize = 5,
            Modulate = new Color(1.0f, 0.78f, 0.42f),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            VisibilityRangeEnd = 16.0f
        });
        _residentialStairDetailCount++;
    }

    private MultiMeshInstance3D AddResidentialStairDetailBatch(
        Node3D parent,
        string name,
        Vector3 size,
        Godot.Material material,
        IReadOnlyList<Transform3D> transforms)
    {
        var multiMesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            Mesh = SharedBoxMesh(size),
            InstanceCount = transforms.Count
        };
        for (var index = 0; index < transforms.Count; index++)
        {
            multiMesh.SetInstanceTransform(index, transforms[index]);
        }
        var visual = new MultiMeshInstance3D
        {
            Name = name,
            Multimesh = multiMesh,
            MaterialOverride = material
        };
        parent.AddChild(visual);
        RegisterMapDetailVisual(visual);
        return visual;
    }
}
