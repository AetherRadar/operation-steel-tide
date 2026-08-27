using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

internal sealed partial class LowPolyBuildingArtBuilder
{
    private static void BuildIndustrialRoofProfile(
        List<Transform3D> boxes,
        List<Transform3D> facets,
        List<Transform3D> utilities,
        int variant,
        float width,
        float depth,
        float roofY)
    {
        var utilityDiameter = Mathf.Clamp(Mathf.Min(width, depth) * 0.1f, 1.1f, 2.2f);
        foreach (var x in new[] { -width * 0.28f, width * 0.27f })
        {
            utilities.Add(Part(
                new Vector3(x, roofY + utilityDiameter * 0.5f, -depth * 0.2f),
                new Vector3(utilityDiameter, utilityDiameter, utilityDiameter)));
        }

        switch (variant)
        {
            case 0:
                boxes.Add(Part(new Vector3(0, roofY + 0.3f, -depth * 0.12f), new Vector3(width * 0.66f, 0.44f, depth * 0.52f)));
                for (var index = -1; index <= 1; index++)
                {
                    facets.Add(Part(
                        new Vector3(index * width * 0.21f, roofY + 1.02f, -depth * 0.12f),
                        new Vector3(width * 0.2f, 1.55f + (index == 0 ? 0.4f : 0.0f), depth * 0.5f),
                        new Vector3(0, index % 2 == 0 ? 0.0f : Mathf.Pi, 0)));
                }
                break;
            case 1:
                boxes.Add(Part(new Vector3(-width * 0.1f, roofY + 0.48f, -depth * 0.12f), new Vector3(width * 0.52f, 0.82f, depth * 0.42f)));
                boxes.Add(Part(new Vector3(width * 0.18f, roofY + 1.03f, -depth * 0.14f), new Vector3(width * 0.24f, 0.42f, depth * 0.26f)));
                facets.Add(Part(new Vector3(-width * 0.3f, roofY + 1.08f, -depth * 0.13f), new Vector3(width * 0.2f, 1.7f, depth * 0.38f)));
                facets.Add(Part(new Vector3(width * 0.34f, roofY + 0.82f, -depth * 0.13f), new Vector3(width * 0.13f, 1.18f, depth * 0.34f), new Vector3(0, Mathf.Pi, 0)));
                break;
            case 2:
                for (var index = -2; index <= 2; index++)
                {
                    facets.Add(Part(
                        new Vector3(index * width * 0.18f, roofY + 0.88f, -depth * 0.1f),
                        new Vector3(width * 0.17f, 1.45f, depth * 0.58f),
                        new Vector3(0, index % 2 == 0 ? 0.0f : Mathf.Pi, 0)));
                }
                boxes.Add(Part(new Vector3(0, roofY + 0.2f, -depth * 0.1f), new Vector3(width * 0.9f, 0.22f, depth * 0.62f)));
                break;
            default:
                boxes.Add(Part(new Vector3(-width * 0.16f, roofY + 0.64f, -depth * 0.14f), new Vector3(width * 0.42f, 1.08f, depth * 0.38f)));
                boxes.Add(Part(new Vector3(width * 0.29f, roofY + 1.55f, -depth * 0.18f), new Vector3(width * 0.12f, 2.9f, depth * 0.16f)));
                facets.Add(Part(new Vector3(-width * 0.16f, roofY + 1.48f, -depth * 0.14f), new Vector3(width * 0.42f, 1.1f, depth * 0.38f)));
                facets.Add(Part(new Vector3(width * 0.29f, roofY + 3.22f, -depth * 0.18f), new Vector3(width * 0.2f, 0.48f, depth * 0.24f), new Vector3(0, Mathf.Pi, 0)));
                utilities.Add(Part(new Vector3(width * 0.4f, roofY + 1.55f, depth * 0.12f), new Vector3(0.92f, 3.0f, 0.92f)));
                break;
        }
    }

    private static void BuildResidentialFacadeProfile(
        List<Transform3D> parts,
        ResidentialFacadeStyle style,
        float width,
        float depth,
        float roofY)
    {
        var front = depth * 0.5f + 0.25f;
        var rear = -depth * 0.5f - 0.25f;
        switch (style)
        {
            case ResidentialFacadeStyle.RibbonGlass:
                foreach (var y in new[] { roofY * 0.31f, roofY * 0.65f })
                {
                    parts.Add(Part(new Vector3(-width * 0.08f, y, front), new Vector3(width * 0.52f, 0.42f, 0.28f)));
                    parts.Add(Part(new Vector3(width * 0.12f, y + roofY * 0.06f, rear), new Vector3(width * 0.42f, 0.38f, 0.28f)));
                }
                parts.Add(Part(new Vector3(-width * 0.37f, roofY * 0.54f, front), new Vector3(width * 0.065f, roofY * 0.38f, 0.34f)));
                break;
            case ResidentialFacadeStyle.VerticalBays:
                foreach (var x in new[] { -width * 0.3f, width * 0.27f })
                {
                    parts.Add(Part(new Vector3(x, roofY * 0.52f, front), new Vector3(width * 0.055f, roofY * 0.62f, 0.32f)));
                }
                parts.Add(Part(new Vector3(width * 0.18f, roofY * 0.64f, rear), new Vector3(width * 0.2f, roofY * 0.1f, 0.3f)));
                break;
            case ResidentialFacadeStyle.StaggeredGrid:
                for (var level = 0; level < 4; level++)
                {
                    var side = level % 2 == 0 ? -1.0f : 1.0f;
                    parts.Add(Part(
                        new Vector3(side * width * 0.25f, roofY * (0.2f + level * 0.2f), front),
                        new Vector3(width * (0.15f + level * 0.012f), roofY * 0.08f, 0.32f)));
                }
                parts.Add(Part(new Vector3(0, roofY * 0.5f, rear), new Vector3(width * 0.07f, roofY * 0.42f, 0.3f)));
                break;
            case ResidentialFacadeStyle.ServiceBands:
                for (var index = 1; index <= 3; index++)
                {
                    var x = index % 2 == 0 ? width * 0.12f : -width * 0.14f;
                    parts.Add(Part(new Vector3(x, roofY * index * 0.245f, front), new Vector3(width * (0.34f + index * 0.04f), 0.5f, 0.34f)));
                }
                parts.Add(Part(new Vector3(width * 0.37f, roofY * 0.53f, rear), new Vector3(width * 0.055f, roofY * 0.44f, 0.3f)));
                break;
            case ResidentialFacadeStyle.TerracedWindows:
                for (var level = 0; level < 3; level++)
                {
                    var side = level % 2 == 0 ? -1.0f : 1.0f;
                    parts.Add(Part(
                        new Vector3(side * width * 0.25f, roofY * (0.26f + level * 0.24f), front),
                        new Vector3(width * (0.15f + level * 0.035f), 0.56f + level * 0.1f, 0.38f)));
                }
                parts.Add(Part(new Vector3(-width * 0.15f, roofY * 0.55f, rear), new Vector3(width * 0.3f, 0.46f, 0.32f)));
                break;
            default:
                foreach (var x in new[] { -width * 0.34f, 0.0f, width * 0.34f })
                {
                    var height = Mathf.Abs(x) < 0.01f ? roofY * 0.38f : roofY * 0.58f;
                    var y = x > 0 ? roofY * 0.62f : roofY * 0.48f;
                    parts.Add(Part(new Vector3(x, y, front), new Vector3(width * 0.052f, height * 0.78f, 0.32f)));
                }
                parts.Add(Part(new Vector3(width * 0.12f, roofY * 0.74f, rear), new Vector3(width * 0.32f, roofY * 0.075f, 0.3f)));
                break;
        }
    }

    private static void BuildResidentialRoofProfile(
        List<Transform3D> boxes,
        List<Transform3D> facets,
        List<Transform3D> utilities,
        ResidentialRoofStyle style,
        float width,
        float depth,
        float roofY,
        int towerIndex)
    {
        var mirror = towerIndex % 2 == 0 ? -1.0f : 1.0f;
        var rear = -depth * 0.5f + 1.1f;
        var utilityDiameter = Mathf.Clamp(Mathf.Min(width, depth) * 0.075f, 1.15f, 1.85f);
        foreach (var x in new[] { -width * 0.31f, width * 0.31f })
        {
            utilities.Add(Part(
                new Vector3(x, roofY + utilityDiameter * 0.5f, rear),
                new Vector3(utilityDiameter, utilityDiameter, utilityDiameter)));
        }

        switch (style)
        {
            case ResidentialRoofStyle.GardenServices:
                foreach (var x in new[] { -width * 0.27f, width * 0.25f })
                {
                    boxes.Add(Part(new Vector3(x, roofY + 1.1f, rear), new Vector3(width * 0.2f, 1.95f, depth * 0.19f)));
                }
                facets.Add(Part(new Vector3(mirror * width * 0.06f, roofY + 2.98f, rear), new Vector3(width * 0.78f, 1.8f, depth * 0.24f), new Vector3(0, mirror < 0.0f ? Mathf.Pi : 0.0f, 0)));
                break;
            case ResidentialRoofStyle.ClinicMechanical:
                for (var index = -1; index <= 1; index++)
                {
                    var height = index == 0 ? 2.65f : 1.8f;
                    boxes.Add(Part(new Vector3(index * width * 0.21f, roofY + height * 0.5f, rear), new Vector3(width * 0.17f, height, depth * 0.2f)));
                }
                facets.Add(Part(new Vector3(mirror * width * 0.24f, roofY + 3.55f, rear), new Vector3(width * 0.42f, 1.8f, depth * 0.24f), new Vector3(0, mirror < 0 ? Mathf.Pi : 0, 0)));
                break;
            case ResidentialRoofStyle.MarketCanopy:
                facets.Add(Part(new Vector3(-width * 0.22f, roofY + 1.55f, depth * 0.5f - 1.05f), new Vector3(width * 0.43f, 2.35f, depth * 0.24f)));
                facets.Add(Part(new Vector3(width * 0.22f, roofY + 1.55f, depth * 0.5f - 1.05f), new Vector3(width * 0.43f, 2.35f, depth * 0.24f), new Vector3(0, Mathf.Pi, 0)));
                boxes.Add(Part(new Vector3(0, roofY + 0.24f, depth * 0.5f - 1.05f), new Vector3(width * 0.86f, 0.24f, depth * 0.26f)));
                boxes.Add(Part(new Vector3(mirror * width * 0.45f, roofY + 1.75f, rear), new Vector3(width * 0.12f, 3.25f, depth * 0.18f)));
                break;
            case ResidentialRoofStyle.WorkshopPlant:
                for (var index = -2; index <= 2; index++)
                {
                    facets.Add(Part(
                        new Vector3(index * width * 0.18f, roofY + 1.47f + (index == 0 ? 0.35f : 0.0f), rear),
                        new Vector3(width * 0.17f, 2.35f + (index == 0 ? 0.7f : 0.0f), depth * 0.22f),
                        new Vector3(0, index % 2 == 0 ? 0.0f : Mathf.Pi, 0)));
                }
                boxes.Add(Part(new Vector3(0, roofY + 0.18f, rear), new Vector3(width * 0.9f, 0.22f, depth * 0.26f)));
                break;
            case ResidentialRoofStyle.ShelterCrown:
                boxes.Add(Part(new Vector3(0, roofY + 0.72f, rear), new Vector3(width * 0.7f, 1.18f, depth * 0.21f)));
                boxes.Add(Part(new Vector3(mirror * width * 0.13f, roofY + 1.68f, rear), new Vector3(width * 0.42f, 0.72f, depth * 0.18f)));
                facets.Add(Part(new Vector3(-mirror * width * 0.22f, roofY + 3.37f, rear), new Vector3(width * 0.32f, 2.65f, depth * 0.22f)));
                break;
            default:
                boxes.Add(Part(new Vector3(mirror * width * 0.48f, roofY + 2.15f, -depth * 0.15f), new Vector3(width * 0.13f, 4.05f, depth * 0.24f)));
                facets.Add(Part(new Vector3(mirror * width * 0.39f, roofY + 4.75f, -depth * 0.15f), new Vector3(width * 0.28f, 1.15f, depth * 0.32f), new Vector3(0, mirror < 0 ? Mathf.Pi : 0, 0)));
                utilities.Add(Part(new Vector3(mirror * width * 0.24f, roofY + 1.9f, rear), new Vector3(1.2f, 3.7f, 1.2f)));
                break;
        }
    }

    private static float FacadeProjectionZ(
        float buildingDepth,
        float projectionDepth,
        bool front)
    {
        const float facadeClearance = 0.18f;
        var position = buildingDepth * 0.5f
            + facadeClearance
            + projectionDepth * 0.5f;
        return front ? position : -position;
    }

    private static void AddResidentialMassingRecesses(
        List<Transform3D> accents,
        IReadOnlyList<Transform3D> structure,
        int firstProfilePart,
        float halfDepth,
        int towerIndex)
    {
        for (var index = firstProfilePart; index < structure.Count; index++)
        {
            var part = structure[index];
            var size = part.Basis.Scale.Abs();
            if (size.Y < 2.4f || Mathf.Abs(part.Origin.Z) <= halfDepth)
            {
                continue;
            }
            var outward = Mathf.Sign(part.Origin.Z);
            var faceZ = part.Origin.Z + outward * (size.Z * 0.5f + 0.035f);
            if (size.X >= 3.0f)
            {
                var rows = Mathf.Clamp(Mathf.FloorToInt(size.Y / 4.2f), 1, 4);
                for (var row = 0; row < rows; row++)
                {
                    var t = (row + 1.0f) / (rows + 1.0f);
                    var stagger = (row + towerIndex) % 2 == 0 ? -0.08f : 0.08f;
                    accents.Add(Part(
                        new Vector3(
                            part.Origin.X + size.X * stagger,
                            part.Origin.Y - size.Y * 0.5f + size.Y * t,
                            faceZ),
                        new Vector3(size.X * 0.68f, 0.38f, 0.08f)));
                }
                continue;
            }
            accents.Add(Part(
                new Vector3(part.Origin.X, part.Origin.Y, faceZ),
                new Vector3(
                    Mathf.Max(0.46f, size.X * 0.38f),
                    size.Y * 0.52f,
                    0.08f)));
        }
    }

    private static void AddPerimeterBand(
        List<Transform3D> parts,
        float width,
        float depth,
        float y,
        float height,
        float projection)
    {
        parts.Add(Part(new Vector3(-width * 0.11f, y, -depth * 0.5f - projection * 0.5f), new Vector3(width * 0.7f, height, projection)));
        parts.Add(Part(new Vector3(width * 0.17f, y, depth * 0.5f + projection * 0.5f), new Vector3(width * 0.56f, height, projection)));
        parts.Add(Part(new Vector3(-width * 0.5f - projection * 0.5f, y, -depth * 0.08f), new Vector3(projection, height, depth * 0.62f)));
        parts.Add(Part(new Vector3(width * 0.5f + projection * 0.5f, y, depth * 0.16f), new Vector3(projection, height, depth * 0.44f)));
    }

    private static void AddEntranceBaseBand(
        List<Transform3D> parts,
        float width,
        float depth,
        float y,
        float height,
        float projection,
        float openingWidth)
    {
        var halfDepth = depth * 0.5f;
        var totalWidth = width + projection * 2.0f;
        var clampedOpening = Mathf.Clamp(openingWidth, 0.0f, totalWidth - projection * 2.0f);
        var sideWidth = (totalWidth - clampedOpening) * 0.5f;
        var sideOffset = clampedOpening * 0.5f + sideWidth * 0.5f;
        parts.Add(Part(new Vector3(0, y, -halfDepth - projection * 0.5f), new Vector3(totalWidth, height, projection)));
        parts.Add(Part(new Vector3(-sideOffset, y, halfDepth + projection * 0.5f), new Vector3(sideWidth, height, projection)));
        parts.Add(Part(new Vector3(sideOffset, y, halfDepth + projection * 0.5f), new Vector3(sideWidth, height, projection)));
        parts.Add(Part(new Vector3(-width * 0.5f - projection * 0.5f, y, 0), new Vector3(projection, height, depth)));
        parts.Add(Part(new Vector3(width * 0.5f + projection * 0.5f, y, 0), new Vector3(projection, height, depth)));
    }
}
