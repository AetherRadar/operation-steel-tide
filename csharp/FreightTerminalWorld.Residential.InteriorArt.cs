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
        if (!ResidentialAuthoredPropLibrary.TryCreateVisual(
                ResidentialAuthoredPropLibrary.PathForRoomProp(name),
                size,
                out var model,
                out _))
        {
            return body;
        }

        body.AddChild(model);
        ResidentialAuthoredPropLibrary.HidePrimitiveMeshes(body);
        body.SetMeta("residential_authored_interior", ResidentialAuthoredPropLibrary.PathForRoomProp(name));
        return body;
    }
}
