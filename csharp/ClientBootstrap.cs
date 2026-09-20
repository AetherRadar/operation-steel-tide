using System;
using Godot;

namespace OperationSteelTide;

[GlobalClass]
public partial class ClientBootstrap : Node3D
{
    public override void _Ready()
    {
        if (Array.Exists(OS.GetCmdlineUserArgs(), argument => argument == "--validate-enemy-death-lifecycle"))
        {
            var fixture = GD.Load<PackedScene>("res://scenes/diagnostics/enemy_death_lifecycle.tscn")
                ?? throw new InvalidOperationException("The enemy death lifecycle diagnostic scene is missing.");
            AddChild(fixture.Instantiate<EnemyDeathLifecycleDiagnostics>());
            return;
        }
        if (Array.Exists(OS.GetCmdlineUserArgs(), argument =>
            argument is "--validate-operator-presentation" or "--capture-operator-presentation"))
        {
            var fixture = GD.Load<PackedScene>("res://scenes/diagnostics/operator_presentation.tscn")
                ?? throw new InvalidOperationException("The operator presentation diagnostic scene is missing.");
            AddChild(fixture.Instantiate<OperatorPresentationDiagnostics>());
            return;
        }
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
