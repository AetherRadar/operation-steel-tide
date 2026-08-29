using System;
using Godot;

namespace OperationSteelTide;

internal sealed class AuthoredDemolitionDeviceVisual
{
    public AuthoredDemolitionDeviceVisual(Node3D root)
    {
        Root = root;
        DeviceCase = CombatModelLibrary.RequireNode(root, "DeviceCase");
        DeviceScreen = CombatModelLibrary.RequireNode(root, "DeviceScreen");
        DeviceStatusLight = CombatModelLibrary.RequireNode(root, "DeviceStatusLight");
        CarrySocket = CombatModelLibrary.RequireNode(root, "DeviceCarrySocket");
    }

    public Node3D Root { get; }
    public Node3D DeviceCase { get; }
    public Node3D DeviceScreen { get; }
    public Node3D DeviceStatusLight { get; }
    public Node3D CarrySocket { get; }
}

internal readonly record struct AuthoredDemolitionDeviceInspection(
    bool Loaded,
    bool ContractValid,
    int MeshCount,
    int MaterialCount,
    Vector3 BoundsSize,
    bool HasEmission);

internal static partial class CombatModelLibrary
{
    internal const string DemolitionDeviceScenePath =
        "res://assets/models/steel_tide_demolition_device/demolition_device.glb";

    private static readonly string[] DemolitionDeviceNodes =
    {
        "SteelTideDemolitionDevice",
        "DeviceCase",
        "DeviceScreen",
        "DeviceStatusLight",
        "DeviceCarrySocket"
    };

    public static AuthoredDemolitionDeviceVisual InstantiateDemolitionDevice()
    {
        var root = InstantiateRequired(DemolitionDeviceScenePath, DemolitionDeviceNodes);
        root.Name = "AuthoredDemolitionDeviceVisual";
        return new AuthoredDemolitionDeviceVisual(root);
    }

    public static AuthoredDemolitionDeviceInspection InspectDemolitionDevice()
    {
        AuthoredDemolitionDeviceVisual? visual = null;
        try
        {
            visual = InstantiateDemolitionDevice();
            var bounds = ComputeBounds(visual.Root);
            return new AuthoredDemolitionDeviceInspection(
                true,
                HasDistinctDemolitionDeviceContract(visual),
                bounds.MeshCount,
                CountMaterials(visual.Root),
                bounds.Size,
                HasEmissiveDemolitionMaterial(visual.Root));
        }
        catch (Exception)
        {
            return new AuthoredDemolitionDeviceInspection(
                false,
                false,
                0,
                0,
                Vector3.Zero,
                false);
        }
        finally
        {
            visual?.Root.Free();
        }
    }

    private static bool HasDistinctDemolitionDeviceContract(AuthoredDemolitionDeviceVisual visual)
    {
        Node3D[] nodes =
        {
            visual.Root,
            visual.DeviceCase,
            visual.DeviceScreen,
            visual.DeviceStatusLight,
            visual.CarrySocket
        };
        for (var left = 0; left < nodes.Length; left++)
        {
            if (!GodotObject.IsInstanceValid(nodes[left]))
            {
                return false;
            }
            for (var right = left + 1; right < nodes.Length; right++)
            {
                if (nodes[left].GetInstanceId() == nodes[right].GetInstanceId())
                {
                    return false;
                }
            }
        }
        return true;
    }

    private static bool HasEmissiveDemolitionMaterial(Node root)
    {
        foreach (var mesh in MeshesBelow(root))
        {
            if (mesh.Mesh is not { } resource)
            {
                continue;
            }
            for (var surface = 0; surface < resource.GetSurfaceCount(); surface++)
            {
                if (resource.SurfaceGetMaterial(surface) is BaseMaterial3D material
                    && material.EmissionEnabled)
                {
                    return true;
                }
            }
        }
        return false;
    }
}
