using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

internal sealed partial class LowPolyBuildingArtBuilder
{
    private static readonly BuildingGradient[] ResidentialGradients =
    {
        new(new(0.19f, 0.27f, 0.25f), new(0.38f, 0.46f, 0.40f), new(0.56f, 0.58f, 0.48f), new(0.11f, 0.16f, 0.14f)),
        new(new(0.31f, 0.23f, 0.17f), new(0.51f, 0.41f, 0.31f), new(0.63f, 0.55f, 0.42f), new(0.17f, 0.12f, 0.09f)),
        new(new(0.19f, 0.27f, 0.33f), new(0.37f, 0.46f, 0.52f), new(0.55f, 0.60f, 0.61f), new(0.10f, 0.15f, 0.19f)),
        new(new(0.30f, 0.20f, 0.18f), new(0.51f, 0.36f, 0.32f), new(0.62f, 0.49f, 0.42f), new(0.17f, 0.11f, 0.10f)),
        new(new(0.23f, 0.28f, 0.18f), new(0.43f, 0.48f, 0.31f), new(0.58f, 0.58f, 0.40f), new(0.13f, 0.16f, 0.10f)),
        new(new(0.28f, 0.20f, 0.24f), new(0.49f, 0.36f, 0.39f), new(0.61f, 0.51f, 0.49f), new(0.16f, 0.11f, 0.13f)),
        new(new(0.16f, 0.28f, 0.30f), new(0.32f, 0.47f, 0.48f), new(0.49f, 0.59f, 0.56f), new(0.09f, 0.16f, 0.17f)),
        new(new(0.30f, 0.26f, 0.16f), new(0.51f, 0.46f, 0.29f), new(0.64f, 0.58f, 0.39f), new(0.17f, 0.15f, 0.09f))
    };

    private static readonly string[] ResidentialMassingStyles =
    {
        "twin_projecting_bays",
        "offset_service_spine",
        "split_height_wings",
        "staggered_terraces",
        "bridge_crown_frame",
        "paired_shoulders",
        "harbor_service_blade",
        "garden_step_decks"
    };

    private readonly record struct BuildingGradient(
        Color Lower,
        Color Middle,
        Color Upper,
        Color Weather)
    {
        public BuildingGradient Darkened(float amount) => new(
            Lower.Darkened(amount),
            Middle.Darkened(amount),
            Upper.Darkened(amount * 0.72f),
            Weather.Darkened(amount * 0.55f));

        public BuildingGradient Lightened(float amount) => new(
            Lower.Lightened(amount * 0.45f),
            Middle.Lightened(amount * 0.72f),
            Upper.Lightened(amount),
            Weather.Lightened(amount * 0.2f));
    }

    private readonly record struct ArchitectureProfile(
        int MassingIndex,
        int RoofIndex,
        int PaletteIndex,
        string MassingStyle)
    {
        public string Signature => $"{MassingStyle}:r{RoofIndex}:p{PaletteIndex}";
    }

    private static ArchitectureProfile IndustrialArchitecture(string identity)
        => identity switch
        {
            "CentralWarehouse" => new(0, 2, 5, "sawtooth_foundry"),
            "CentralBarracks" => new(1, 0, 3, "offset_barracks"),
            "CustomsWarehouseComplex" => new(2, 3, 0, "customs_spine"),
            "OpsAnnexComplex" => new(3, 1, 2, "cantilever_operations"),
            "FuelLogisticsHall" => new(4, 0, 1, "twin_plant_shoulders"),
            "QuayBondedStorage" => new(5, 2, 4, "bonded_bay_frame"),
            _ => FallbackIndustrialArchitecture(identity)
        };

    private static ArchitectureProfile FallbackIndustrialArchitecture(string identity)
    {
        var massing = StableVariant($"{identity}:massing", 6);
        var roof = StableVariant($"{identity}:roof", 4);
        var palette = StableVariant($"{identity}:palette", IndustrialGradients.Length);
        return new ArchitectureProfile(massing, roof, palette, $"harbor_industrial_{massing}");
    }

    private static ArchitectureProfile ResidentialArchitecture(int towerIndex)
    {
        var paletteOrder = new[] { 0, 3, 6, 1, 5, 2, 7, 4, 0, 5, 2 };
        var massing = Mathf.PosMod(towerIndex, ResidentialMassingStyles.Length);
        var palette = paletteOrder[Mathf.PosMod(towerIndex, paletteOrder.Length)];
        return new ArchitectureProfile(
            massing,
            Mathf.PosMod(towerIndex * 3 + 1, 6),
            palette,
            ResidentialMassingStyles[massing]);
    }

    private static BuildingGradient ResidentialGradient(int paletteIndex, Color accent)
    {
        var source = ResidentialGradients[Mathf.PosMod(paletteIndex, ResidentialGradients.Length)];
        return new BuildingGradient(
            source.Lower.Lerp(accent.Darkened(0.52f), 0.14f),
            source.Middle.Lerp(accent.Darkened(0.28f), 0.11f),
            source.Upper.Lerp(accent.Lightened(0.18f), 0.08f),
            source.Weather);
    }

    private static BuildingGradient AccentGradient(BuildingGradient source, Color accent)
        => new(
            source.Lower.Lerp(accent.Darkened(0.52f), 0.32f),
            source.Middle.Lerp(accent.Darkened(0.24f), 0.38f),
            source.Upper.Lerp(accent.Lightened(0.08f), 0.3f),
            source.Weather.Lerp(accent.Darkened(0.64f), 0.18f));

    private static BuildingGradient RecessGradient(BuildingGradient source, Color accent)
        => new(
            source.Lower.Darkened(0.62f).Lerp(accent.Darkened(0.72f), 0.12f),
            source.Middle.Darkened(0.56f).Lerp(accent.Darkened(0.66f), 0.14f),
            source.Upper.Darkened(0.48f).Lerp(accent.Darkened(0.58f), 0.16f),
            source.Weather.Darkened(0.68f));

    private static int BuildIndustrialMassing(
        List<Transform3D> structure,
        List<Transform3D> collisions,
        List<Transform3D> facetCollisions,
        List<Transform3D> accents,
        List<Transform3D> facets,
        string identity,
        int style,
        float width,
        float depth,
        float roofY)
    {
        var initialCount = structure.Count;
        var initialFacetCount = facets.Count;
        var halfWidth = width * 0.5f;
        var halfDepth = depth * 0.5f;
        var podiumHeight = Mathf.Clamp(roofY * 0.24f, 1.35f, 2.45f);
        AddEntrancePodium(
            structure,
            collisions,
            width,
            depth,
            podiumHeight,
            0.78f,
            Mathf.Min(9.4f, width * 0.58f));
        foreach (var side in new[] { -1.0f, 1.0f })
        {
            AddFacetedMassingPart(
                facets,
                facetCollisions,
                Part(
                    new Vector3(side * width * 0.32f, podiumHeight + 0.58f, halfDepth + 0.92f),
                    new Vector3(width * 0.17f, 1.16f, 1.62f),
                    new Vector3(0, side < 0.0f ? 0.0f : Mathf.Pi, 0)));
        }

        switch (style)
        {
            case 0:
                AddMassingPart(structure, collisions, Part(
                    new Vector3(0, roofY * 0.43f, -halfDepth - 0.72f),
                    new Vector3(width * 0.52f, roofY * 0.64f, 1.42f)));
                foreach (var x in new[] { -width * 0.31f, width * 0.31f })
                {
                    AddMassingPart(structure, collisions, Part(
                        new Vector3(x, roofY * 0.55f, halfDepth + 0.56f),
                        new Vector3(width * 0.17f, roofY * 0.52f, 1.08f)));
                }
                break;
            case 1:
                foreach (var x in new[] { -halfWidth - 0.52f, halfWidth + 0.52f })
                {
                    AddMassingPart(structure, collisions, Part(
                        new Vector3(x, roofY * 0.48f, -depth * 0.08f),
                        new Vector3(1.04f, roofY * 0.72f, depth * 0.48f)));
                }
                AddMassingPart(structure, collisions, Part(
                    new Vector3(-width * 0.2f, roofY * 0.42f, -halfDepth - 0.66f),
                    new Vector3(width * 0.32f, roofY * 0.58f, 1.28f)));
                break;
            case 2:
                AddMassingPart(structure, collisions, Part(
                    new Vector3(width * 0.19f, (roofY + 1.8f) * 0.5f, -halfDepth - 0.64f),
                    new Vector3(width * 0.24f, roofY + 1.8f, 1.25f)));
                AddMassingPart(structure, collisions, Part(
                    new Vector3(-width * 0.28f, roofY * 0.58f, halfDepth + 0.62f),
                    new Vector3(width * 0.22f, roofY * 0.56f, 1.18f)));
                break;
            case 3:
                AddMassingPart(structure, collisions, Part(
                    new Vector3(-width * 0.18f, roofY * 0.68f, halfDepth + 0.76f),
                    new Vector3(width * 0.46f, roofY * 0.38f, 1.46f)));
                AddMassingPart(structure, collisions, Part(
                    new Vector3(width * 0.31f, roofY * 0.46f, -halfDepth - 0.66f),
                    new Vector3(width * 0.18f, roofY * 0.72f, 1.26f)));
                break;
            case 4:
                foreach (var x in new[] { -width * 0.3f, width * 0.3f })
                {
                    AddMassingPart(structure, collisions, Part(
                        new Vector3(x, roofY * 0.52f, -halfDepth - 0.71f),
                        new Vector3(width * 0.2f, roofY * 0.68f, 1.38f)));
                }
                AddMassingPart(structure, collisions, Part(
                    new Vector3(-width * 0.34f, roofY * 0.7f, halfDepth + 0.58f),
                    new Vector3(width * 0.14f, roofY * 0.34f, 1.08f)));
                break;
            default:
                AddMassingPart(structure, collisions, Part(
                    new Vector3(0, roofY * 0.54f, -halfDepth - 0.68f),
                    new Vector3(width * 0.28f, roofY * 0.78f, 1.32f)));
                foreach (var x in new[] { -width * 0.3f, width * 0.3f })
                {
                    AddMassingPart(structure, collisions, Part(
                        new Vector3(x, roofY * 0.48f, halfDepth + 0.58f),
                        new Vector3(width * 0.16f, roofY * 0.54f, 1.1f)));
                }
                break;
        }

        AddIndustrialColorBlocking(accents, width, depth, roofY, style);
        RemoveCollisionNear(
            collisions,
            IndustrialLadderKeepout(identity),
            0.9f);
        RemoveCollisionNear(
            facetCollisions,
            IndustrialLadderKeepout(identity),
            0.9f);
        return structure.Count - initialCount + facets.Count - initialFacetCount;
    }

    private static Vector2 IndustrialLadderKeepout(string identity)
        => identity switch
        {
            "CentralWarehouse" => new Vector2(16.55f, -8.0f),
            "CentralBarracks" => new Vector2(9.45f, -0.5f),
            "CustomsWarehouseComplex" => new Vector2(-15.0f, 5.0f),
            "OpsAnnexComplex" => new Vector2(4.0f, -10.0f),
            "FuelLogisticsHall" => new Vector2(14.0f, 0.0f),
            "QuayBondedStorage" => new Vector2(13.0f, 5.0f),
            _ => new Vector2(float.PositiveInfinity, float.PositiveInfinity)
        };

    private static void RemoveCollisionNear(
        List<Transform3D> collisions,
        Vector2 keepout,
        float clearance)
    {
        if (!float.IsFinite(keepout.X) || !float.IsFinite(keepout.Y))
        {
            return;
        }
        collisions.RemoveAll(transform =>
        {
            var size = transform.Basis.Scale.Abs();
            return Mathf.Abs(transform.Origin.X - keepout.X) <= size.X * 0.5f + clearance
                && Mathf.Abs(transform.Origin.Z - keepout.Y) <= size.Z * 0.5f + clearance;
        });
    }

    private static int BuildResidentialMassing(
        List<Transform3D> structure,
        List<Transform3D> collisions,
        List<Transform3D> facetCollisions,
        List<Transform3D> accents,
        List<Transform3D> facets,
        int style,
        float width,
        float depth,
        float roofY,
        int towerIndex)
    {
        var initialCount = structure.Count;
        var initialFacetCount = facets.Count;
        var halfDepth = depth * 0.5f;
        var mirror = towerIndex % 2 == 0 ? -1.0f : 1.0f;
        var podiumHeight = Mathf.Clamp(roofY * 0.2f, 4.8f, 6.6f);
        AddEntrancePodium(
            structure,
            collisions,
            width,
            depth,
            podiumHeight,
            0.92f,
            Mathf.Min(5.6f, width * 0.42f));
        foreach (var side in new[] { -1.0f, 1.0f })
        {
            AddFacetedMassingPart(
                facets,
                facetCollisions,
                Part(
                    new Vector3(
                        side * width * 0.32f,
                        podiumHeight + 0.62f,
                        FacadeProjectionZ(depth, 1.78f, true)),
                    new Vector3(width * 0.17f, 1.24f, 1.78f),
                    new Vector3(0, side < 0.0f ? 0.0f : Mathf.Pi, 0)));
        }

        var profileStart = structure.Count;
        switch (style)
        {
            case 0:
                foreach (var x in new[] { -width * 0.29f, width * 0.29f })
                {
                    AddMassingPart(structure, collisions, Part(
                        new Vector3(x < 0.0f ? -width * 0.47f : width * 0.47f, roofY * 0.51f, FacadeProjectionZ(depth, 1.5f, true)),
                        new Vector3(width * 0.2f, roofY * 0.56f, 1.5f)));
                }
                AddMassingPart(structure, collisions, Part(
                    new Vector3(mirror * width * 0.39f, roofY * 0.78f, FacadeProjectionZ(depth, 1.36f, false)),
                    new Vector3(width * 0.26f, roofY * 0.24f, 1.36f)));
                break;
            case 1:
                AddMassingPart(structure, collisions, Part(
                    new Vector3(mirror * width * 0.47f, (roofY + 3.4f) * 0.5f, FacadeProjectionZ(depth, 1.42f, false)),
                    new Vector3(width * 0.18f, roofY + 3.4f, 1.42f)));
                AddMassingPart(structure, collisions, Part(
                    new Vector3(-mirror * width * 0.41f, roofY * 0.32f, FacadeProjectionZ(depth, 1.34f, true)),
                    new Vector3(width * 0.26f, roofY * 0.38f, 1.34f)));
                break;
            case 2:
                AddMassingPart(structure, collisions, Part(
                    new Vector3(-mirror * width * 0.44f, roofY * 0.35f, FacadeProjectionZ(depth, 1.38f, true)),
                    new Vector3(width * 0.24f, roofY * 0.48f, 1.38f)));
                AddMassingPart(structure, collisions, Part(
                    new Vector3(mirror * width * 0.45f, roofY * 0.67f, FacadeProjectionZ(depth, 1.46f, false)),
                    new Vector3(width * 0.22f, roofY * 0.58f, 1.46f)));
                break;
            case 3:
                for (var level = 0; level < 3; level++)
                {
                    var side = (level + towerIndex) % 2 == 0 ? -1.0f : 1.0f;
                    AddMassingPart(structure, collisions, Part(
                        new Vector3(side * width * (0.39f + level * 0.025f), roofY * (0.25f + level * 0.25f), FacadeProjectionZ(depth, 1.26f + level * 0.3f, true)),
                        new Vector3(width * (0.27f - level * 0.035f), roofY * 0.2f, 1.26f + level * 0.3f)));
                }
                break;
            case 4:
                foreach (var x in new[] { -width * 0.35f, width * 0.35f })
                {
                    AddMassingPart(structure, collisions, Part(
                        new Vector3(x < 0.0f ? -width * 0.49f : width * 0.49f, roofY * 0.72f, FacadeProjectionZ(depth, 1.3f, true)),
                        new Vector3(width * 0.13f, roofY * 0.62f + 3.4f, 1.3f)));
                }
                AddMassingPart(structure, collisions, Part(
                    new Vector3(0, roofY + 2.25f, FacadeProjectionZ(depth, 1.3f, true)),
                    new Vector3(width * 1.06f, 0.62f, 1.3f)));
                break;
            case 5:
                foreach (var x in new[] { -width * 0.33f, width * 0.33f })
                {
                    AddMassingPart(structure, collisions, Part(
                        new Vector3(x < 0.0f ? -width * 0.46f : width * 0.46f, roofY * 0.42f, FacadeProjectionZ(depth, 1.34f, false)),
                        new Vector3(width * 0.18f, roofY * 0.66f, 1.34f)));
                }
                AddMassingPart(structure, collisions, Part(
                    new Vector3(mirror * width * 0.37f, roofY * 0.76f, FacadeProjectionZ(depth, 1.26f, true)),
                    new Vector3(width * 0.35f, roofY * 0.3f, 1.26f)));
                break;
            case 6:
                AddMassingPart(structure, collisions, Part(
                    new Vector3(mirror * width * 0.5f, (roofY + 4.4f) * 0.5f, FacadeProjectionZ(depth, 1.3f, false)),
                    new Vector3(width * 0.13f, roofY + 4.4f, 1.3f)));
                AddMassingPart(structure, collisions, Part(
                    new Vector3(-mirror * width * 0.36f, roofY * 0.56f, FacadeProjectionZ(depth, 1.46f, true)),
                    new Vector3(width * 0.42f, roofY * 0.34f, 1.46f)));
                break;
            default:
                for (var level = 0; level < 2; level++)
                {
                    var projectionDepth = 1.4f + level * 0.2f;
                    AddMassingPart(structure, collisions, Part(
                        new Vector3(
                            mirror * width * (0.38f + level * 0.07f),
                            roofY * (0.32f + level * 0.34f),
                            FacadeProjectionZ(depth, projectionDepth, level == 0)),
                        new Vector3(width * (0.36f - level * 0.06f), roofY * 0.22f, projectionDepth)));
                }
                break;
        }

        // South Court 3 shares its inward-facing lot line with the authored South Security Plant.
        // Keep that attached facade flush so decorative projections cannot enter the
        // neighboring room volume or intercept its interior wall probes.
        if (towerIndex == 9)
        {
            RemoveAttachedFrontFacadeParts(structure, initialCount, halfDepth);
            RemoveAttachedFrontFacadeParts(collisions, 0, halfDepth);
        }

        AddResidentialMassingRecesses(
            accents,
            structure,
            profileStart,
            halfDepth,
            towerIndex);
        AddResidentialColorBlocking(accents, width, depth, roofY, towerIndex);
        return structure.Count - initialCount + facets.Count - initialFacetCount;
    }

    private static void RemoveAttachedFrontFacadeParts(
        List<Transform3D> parts,
        int firstPart,
        float halfDepth)
    {
        for (var index = parts.Count - 1; index >= firstPart; index--)
        {
            if (parts[index].Origin.Z > halfDepth)
            {
                parts.RemoveAt(index);
            }
        }
    }

    private static void AddIndustrialColorBlocking(
        List<Transform3D> parts,
        float width,
        float depth,
        float roofY,
        int style)
    {
        var front = depth * 0.5f + 0.34f;
        var rear = -depth * 0.5f - 0.34f;
        var mirror = style % 2 == 0 ? -1.0f : 1.0f;
        parts.Add(Part(new Vector3(mirror * width * 0.28f, roofY * 0.34f, front), new Vector3(width * 0.18f, roofY * 0.26f, 0.24f)));
        parts.Add(Part(new Vector3(-mirror * width * 0.25f, roofY * 0.7f, front), new Vector3(width * 0.23f, roofY * 0.2f, 0.24f)));
        parts.Add(Part(new Vector3(mirror * width * 0.14f, roofY * 0.54f, rear), new Vector3(width * 0.3f, roofY * 0.13f, 0.24f)));
        parts.Add(Part(new Vector3(-width * 0.28f, roofY * 0.68f, front + 0.03f), new Vector3(0.34f, roofY * 0.42f, 0.18f), new Vector3(0, 0, 0.34f)));
        parts.Add(Part(new Vector3(width * 0.28f, roofY * 0.68f, front + 0.03f), new Vector3(0.34f, roofY * 0.42f, 0.18f), new Vector3(0, 0, -0.34f)));
    }

    private static void AddResidentialColorBlocking(
        List<Transform3D> parts,
        float width,
        float depth,
        float roofY,
        int towerIndex)
    {
        var front = depth * 0.5f + 0.34f;
        var rear = -depth * 0.5f - 0.34f;
        var mirror = towerIndex % 2 == 0 ? -1.0f : 1.0f;
        parts.Add(Part(new Vector3(mirror * width * 0.32f, roofY * 0.3f, front), new Vector3(width * 0.12f, roofY * 0.11f, 0.24f)));
        parts.Add(Part(new Vector3(-mirror * width * 0.27f, roofY * 0.66f, front), new Vector3(width * 0.17f, roofY * 0.13f, 0.24f)));
        parts.Add(Part(new Vector3(mirror * width * 0.13f, roofY * 0.48f, rear), new Vector3(width * 0.23f, roofY * 0.075f, 0.24f)));
        parts.Add(Part(new Vector3(-mirror * width * 0.37f, roofY * 0.78f, rear), new Vector3(width * 0.075f, roofY * 0.17f, 0.24f)));
    }

    private static void AddEntrancePodium(
        List<Transform3D> visuals,
        List<Transform3D> collisions,
        float width,
        float depth,
        float height,
        float projection,
        float openingWidth)
    {
        var halfDepth = depth * 0.5f;
        var totalWidth = width + projection * 2.0f;
        var clampedOpening = Mathf.Clamp(openingWidth, 0.0f, totalWidth - projection * 2.0f);
        var sideWidth = (totalWidth - clampedOpening) * 0.5f;
        var sideOffset = clampedOpening * 0.5f + sideWidth * 0.5f;
        AddMassingPart(visuals, collisions, Part(new Vector3(0, height * 0.5f, -halfDepth - projection * 0.5f), new Vector3(totalWidth, height, projection)));
        AddMassingPart(visuals, collisions, Part(new Vector3(-sideOffset, height * 0.5f, halfDepth + projection * 0.5f), new Vector3(sideWidth, height, projection)));
        AddMassingPart(visuals, collisions, Part(new Vector3(sideOffset, height * 0.5f, halfDepth + projection * 0.5f), new Vector3(sideWidth, height, projection)));
        AddMassingPart(visuals, collisions, Part(new Vector3(-width * 0.5f - projection * 0.5f, height * 0.5f, 0), new Vector3(projection, height, depth)));
        AddMassingPart(visuals, collisions, Part(new Vector3(width * 0.5f + projection * 0.5f, height * 0.5f, 0), new Vector3(projection, height, depth)));
    }

    private static void AddMassingPart(
        List<Transform3D> visuals,
        List<Transform3D> collisions,
        Transform3D transform)
    {
        visuals.Add(transform);
        collisions.Add(transform);
    }

    private static void AddFacetedMassingPart(
        List<Transform3D> visuals,
        List<Transform3D> collisions,
        Transform3D transform)
    {
        visuals.Add(transform);
        collisions.Add(transform);
    }
}
