using System;
using Godot;

namespace OperationSteelTide;

public partial class SquadMate
{
    private AuthoredOperatorVisual _authoredOperatorVisual = null!;

    internal bool UsesAuthoredOperatorForDiagnostics
        => IsInstanceValid(_authoredOperatorVisual?.Root);

    private void AttachAuthoredOperatorVisual()
    {
        try
        {
            var authoredOperator = CombatModelLibrary.InstantiateOperator();
            _rig.AddChild(authoredOperator.Root);
            _authoredOperatorVisual = authoredOperator;
        }
        catch (Exception exception)
        {
            GD.PushWarning($"Authored squad operator unavailable; retaining procedural visual: {exception.Message}");
            return;
        }
        foreach (var child in _rig.GetChildren())
        {
            if (child is MeshInstance3D mesh && mesh != _authoredOperatorVisual.Root)
            {
                mesh.QueueFree();
            }
        }
    }

    private void SetAuthoredRoleColor(Color color)
    {
        if (IsInstanceValid(_authoredOperatorVisual?.Root))
        {
            _authoredOperatorVisual.SetTeamColor(color);
        }
    }
}
