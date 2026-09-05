using System;
using Godot;

namespace OperationSteelTide;

[GlobalClass]
public partial class ClientBootstrap : Node3D
{
    public override void _Ready()
    {
        if (Array.Exists(OS.GetCmdlineUserArgs(), argument => argument == "--validate-opening-music"))
        {
            OpeningMusicController.RunDiagnostic(GetTree());
            return;
        }

        var world = new FreightTerminalWorld
        {
            Name = "FreightTerminalRuntime"
        };
        AddChild(world);
        GD.Print("STEEL_TIDE_RUNTIME_READY");
    }
}
