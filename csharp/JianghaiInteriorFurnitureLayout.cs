using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

internal readonly record struct JianghaiStaticFurnitureSpec(
    string Name,
    string ScenePath,
    Vector3 Size);

/// <summary>Deterministic authored furniture selection and placement for Old City rooms.</summary>
internal static class JianghaiInteriorFurnitureLayout
{
    public static IReadOnlyList<JianghaiStaticFurnitureSpec> StaticSpecs(string archetype)
    {
        var root = ResidentialAuthoredPropLibrary.FurnitureRoot;
        return archetype switch
        {
            "tea_house" => new[]
            {
                new JianghaiStaticFurnitureSpec(
                    "TeaTable",
                    $"{root}/table.glb",
                    new Vector3(1.2f, 0.72f, 0.82f)),
                new JianghaiStaticFurnitureSpec(
                    "TeaSofa",
                    $"{root}/loungeSofa.glb",
                    new Vector3(1.55f, 0.82f, 0.74f)),
                new JianghaiStaticFurnitureSpec(
                    "TeaBookcase",
                    $"{root}/bookcaseClosedDoors.glb",
                    new Vector3(0.82f, 1.72f, 0.42f))
            },
            "repair_shop" => new[]
            {
                new JianghaiStaticFurnitureSpec(
                    "RepairDesk",
                    $"{root}/desk.glb",
                    new Vector3(1.35f, 0.76f, 0.72f)),
                new JianghaiStaticFurnitureSpec(
                    "RepairBookcase",
                    $"{root}/bookcaseClosedDoors.glb",
                    new Vector3(0.84f, 1.76f, 0.44f)),
                new JianghaiStaticFurnitureSpec(
                    "RepairTable",
                    $"{root}/table.glb",
                    new Vector3(1.15f, 0.72f, 0.78f))
            },
            _ => new[]
            {
                new JianghaiStaticFurnitureSpec(
                    "ResidentBed",
                    $"{root}/bedSingle.glb",
                    new Vector3(1.82f, 0.52f, 0.84f)),
                new JianghaiStaticFurnitureSpec(
                    "ResidentTable",
                    $"{root}/table.glb",
                    new Vector3(1.08f, 0.72f, 0.78f)),
                new JianghaiStaticFurnitureSpec(
                    "ResidentSofa",
                    $"{root}/loungeSofa.glb",
                    new Vector3(1.58f, 0.82f, 0.74f))
            }
        };
    }

    public static (Vector3 Position, float Yaw) StaticSlot(
        int index,
        float width,
        float depth)
    {
        var side = Mathf.Max(0.9f, width * 0.31f);
        var back = Mathf.Min(depth * 0.72f, depth - 0.62f);
        return index switch
        {
            0 => (new Vector3(0, 0.02f, -back), 0.0f),
            1 => (new Vector3(-side, 0.02f, -depth * 0.46f), -Mathf.Pi * 0.5f),
            _ => (new Vector3(side, 0.02f, -depth * 0.5f), Mathf.Pi * 0.5f)
        };
    }

    public static (Vector3 Position, float Yaw) SearchableSlot(
        int index,
        float width,
        float depth)
    {
        var side = Mathf.Max(0.82f, width * 0.29f);
        return index == 0
            ? (new Vector3(-side, 0.02f, -depth * 0.25f), 0.0f)
            : (new Vector3(side, 0.02f, -depth * 0.27f), Mathf.Pi);
    }

    public static ResidentialFurnitureKind SearchableKind(
        string archetype,
        int roomIndex,
        int itemIndex)
        => archetype switch
        {
            "tea_house" => itemIndex == 0
                ? ResidentialFurnitureKind.Refrigerator
                : ResidentialFurnitureKind.DeskDrawers,
            "repair_shop" => itemIndex == 0
                ? ResidentialFurnitureKind.DeskDrawers
                : ResidentialFurnitureKind.Wardrobe,
            "family_shop" => itemIndex == 0
                ? ResidentialFurnitureKind.Wardrobe
                : ResidentialFurnitureKind.Nightstand,
            _ => (ResidentialFurnitureKind)Mathf.PosMod(
                roomIndex + itemIndex,
                Enum.GetValues<ResidentialFurnitureKind>().Length)
        };
}
