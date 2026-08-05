using Godot;

namespace OperationSteelTide;

[GlobalClass]
public partial class ClientBootstrap : Node3D
{
    public override void _Ready()
    {
        var world = new FreightTerminalWorld
        {
            Name = "FreightTerminalRuntime"
        };
        AddChild(world);
    }
}
