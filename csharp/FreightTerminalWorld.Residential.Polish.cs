using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private void BuildTowerFacadeDetails(
        Node3D tower,
        ResidentialTowerSpec spec,
        int towerIndex,
        Color accent)
    {
        var profile = ResidentialTowerDiversityPlan.ForTower(towerIndex);
        var width = spec.Footprint.X;
        var depth = spec.Footprint.Y;
        var buildingHeight = spec.Floors * ResidentialFloorHeight;
        var accentMaterial = Mat(
            $"residential_facade_accent_{towerIndex % 5}",
            accent.Darkened(0.28f).Lerp(new Color(0.34f, 0.39f, 0.39f), 0.52f),
            0.04f,
            0.9f);
        var utilityMaterial = Mat(
            "residential_facade_utility",
            new Color(0.30f, 0.35f, 0.35f),
            0.04f,
            0.92f);
        var ventMaterial = Mat(
            "residential_facade_vent",
            new Color(0.045f, 0.06f, 0.062f),
            0.12f,
            0.82f);

        var accents = new List<Transform3D>(8);
        var frontZ = depth * 0.5f + 0.19f;
        AddFacadeBox(accents, new Vector3(-2.02f, 1.52f, frontZ), new Vector3(0.2f, 3.02f, 0.22f));
        AddFacadeBox(accents, new Vector3(2.02f, 1.52f, frontZ), new Vector3(0.2f, 3.02f, 0.22f));
        AddFacadeBox(accents, new Vector3(0, 2.94f, frontZ), new Vector3(4.25f, 0.18f, 0.22f));
        var accentSide = towerIndex % 2 == 0 ? -1.0f : 1.0f;
        AddFacadeBox(
            accents,
            new Vector3(accentSide * (width * 0.5f + 0.18f), buildingHeight * 0.61f, -depth * 0.22f),
            new Vector3(0.2f, buildingHeight * 0.28f, Mathf.Min(3.4f, depth * 0.18f)));
        if (profile.Facade == ResidentialFacadeStyle.RibbonGlass)
        {
            AddFacadeBox(accents, new Vector3(-width * 0.08f, buildingHeight * 0.52f, depth * 0.5f + 0.2f), new Vector3(width * 0.44f, 0.2f, 0.22f));
        }
        else if (profile.Facade == ResidentialFacadeStyle.ServiceBands)
        {
            AddFacadeBox(accents, new Vector3(width * 0.12f, buildingHeight - 0.65f, depth * 0.5f + 0.2f), new Vector3(width * 0.32f, 0.52f, 0.22f));
        }
        AddFacadeBoxBatch(tower, "FacadeAccents", accentMaterial, accents, 150.0f, false);

        var utilityBoxes = new List<Transform3D>();
        var utilityVents = new List<Transform3D>();
        for (var floor = 0; floor < spec.Floors; floor += 2)
        {
            var y = floor * ResidentialFloorHeight + 0.82f;
            var side = (floor + towerIndex) % 2 == 0 ? -1.0f : 1.0f;
            var x = side * width * 0.28f;
            var northZ = -depth * 0.5f - 0.27f;
            AddFacadeBox(utilityBoxes, new Vector3(x, y, northZ), new Vector3(0.72f, 0.44f, 0.34f));
            AddFacadeBox(utilityVents, new Vector3(x, y, northZ - 0.18f), new Vector3(0.52f, 0.28f, 0.025f));

            if ((floor + towerIndex) % 3 == 0)
            {
                var eastX = width * 0.5f + 0.27f;
                var z = -depth * 0.2f + floor % 4 * 1.15f;
                AddFacadeBox(utilityBoxes, new Vector3(eastX, y + 0.18f, z), new Vector3(0.34f, 0.44f, 0.72f));
                AddFacadeBox(utilityVents, new Vector3(eastX + 0.18f, y + 0.18f, z), new Vector3(0.025f, 0.28f, 0.52f));
            }
        }
        AddFacadeBoxBatch(tower, "FacadeUtilities", utilityMaterial, utilityBoxes, 76.0f, true);
        AddFacadeBoxBatch(tower, "FacadeUtilityVents", ventMaterial, utilityVents, 76.0f, true);
    }

    private static void AddFacadeBox(List<Transform3D> transforms, Vector3 position, Vector3 size)
    {
        transforms.Add(new Transform3D(Basis.Identity.Scaled(size), position));
    }

    private void AddFacadeBoxBatch(
        Node3D parent,
        string name,
        Godot.Material material,
        List<Transform3D> transforms,
        float visibilityRange,
        bool mapDetail)
    {
        if (transforms.Count == 0)
        {
            return;
        }
        var multiMesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            InstanceCount = transforms.Count,
            Mesh = SharedBoxMesh(Vector3.One)
        };
        for (var index = 0; index < transforms.Count; index++)
        {
            multiMesh.SetInstanceTransform(index, transforms[index]);
        }
        var visual = new MultiMeshInstance3D
        {
            Name = name,
            Multimesh = multiMesh,
            MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            VisibilityRangeEnd = visibilityRange,
            VisibilityRangeEndMargin = 12.0f
        };
        parent.AddChild(visual);
        if (mapDetail)
        {
            RegisterMapDetailVisual(visual);
        }
    }
}
