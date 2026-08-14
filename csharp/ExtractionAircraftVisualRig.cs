using Godot;

namespace OperationSteelTide;

internal sealed class ExtractionAircraftVisualRig
{
    private const string ScenePath = "res://assets/models/extraction_aircraft/extraction_aircraft.glb";

    private ExtractionAircraftVisualRig(
        Node3D root,
        Node3D leftRotor,
        Node3D rightRotor,
        Node3D boardingDoor)
    {
        Root = root;
        LeftRotor = leftRotor;
        RightRotor = rightRotor;
        BoardingDoor = boardingDoor;
    }

    public Node3D Root { get; }
    public Node3D LeftRotor { get; }
    public Node3D RightRotor { get; }
    public Node3D BoardingDoor { get; }

    public static ExtractionAircraftVisualRig? TryInstantiate()
    {
        var scene = GD.Load<PackedScene>(ScenePath);
        if (scene is null)
        {
            GD.PushWarning($"Extraction aircraft visual could not load {ScenePath}; using runtime fallback.");
            return null;
        }

        var instance = scene.Instantiate<Node3D>();
        var leftRotor = FindNode(instance, "LeftRotorPivot");
        var rightRotor = FindNode(instance, "RightRotorPivot");
        var boardingDoor = FindNode(instance, "BoardingDoor");
        if (leftRotor is null || rightRotor is null || boardingDoor is null)
        {
            GD.PushWarning(
                "Extraction aircraft visual is missing LeftRotorPivot, RightRotorPivot, or BoardingDoor; "
                + "using runtime fallback.");
            instance.Free();
            return null;
        }

        instance.Name = "RescueTiltRotorVisual";
        return new ExtractionAircraftVisualRig(instance, leftRotor, rightRotor, boardingDoor);
    }

    private static Node3D? FindNode(Node3D root, string name)
    {
        if (root.Name == name)
        {
            return root;
        }
        return root.FindChild(name, recursive: true, owned: false) as Node3D;
    }
}
