using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private StaticBody3D AuthoredInteriorBox(
        Node3D parent,
        string name,
        Vector3 position,
        Vector3 size,
        Godot.Material fallbackMaterial,
        Vector3 rotation = default)
    {
        if (size.X < 0.15f || size.Z < 0.15f)
        {
            return ExpansionBox(parent, name, position, size, fallbackMaterial, rotation);
        }

        var body = ExpansionBox(parent, name, position, size, fallbackMaterial, rotation);
        var scenePath = ResidentialAuthoredPropLibrary.PathForRoomProp(name);
        if (!ResidentialAuthoredPropLibrary.TryCreateVisual(
                scenePath,
                size,
                out var model,
                out _))
        {
            HideMissingAuthoredInteriorVisual(body, scenePath);
            return body;
        }

        body.AddChild(model);
        ResidentialAuthoredPropLibrary.HidePrimitiveMeshes(body);
        body.SetMeta("residential_authored_interior", scenePath);
        return body;
    }

    private static void HideMissingAuthoredInteriorVisual(StaticBody3D body, string scenePath)
    {
        ResidentialAuthoredPropLibrary.HidePrimitiveMeshes(body);
        body.SetMeta("residential_authored_visual_missing", scenePath);
    }
}
