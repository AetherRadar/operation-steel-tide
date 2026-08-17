using System;
using Godot;

namespace OperationSteelTide;

public partial class EnemyOperator
{
    private AuthoredOperatorVisual _authoredOperatorVisual = null!;

    internal bool UsesAuthoredOperatorForDiagnostics
        => IsInstanceValid(_authoredOperatorVisual?.Root);

    private void AttachAuthoredOperatorVisual()
    {
        try
        {
            var authoredOperator = CombatModelLibrary.InstantiateOperator();
            _bodyRoot.AddChild(authoredOperator.Root);
            _authoredOperatorVisual = authoredOperator;
        }
        catch (Exception exception)
        {
            GD.PushWarning($"Authored enemy operator unavailable; retaining procedural visual: {exception.Message}");
            return;
        }
        var children = _bodyRoot.GetChildren();
        using var childrenBacking = children.AsDisposable();
        foreach (var child in children)
        {
            if (child is Node3D visual && visual != _authoredOperatorVisual.Root)
            {
                visual.QueueFree();
            }
        }
        _leftLegRig = _authoredOperatorVisual.LeftLegRig;
        _rightLegRig = _authoredOperatorVisual.RightLegRig;
        SetAuthoredThreatColor(IsWorldBoss
            ? new Color(0.12f, 0.94f, 0.76f)
            : IsRivalSquad
                ? new Color(1.0f, 0.4f, 0.12f)
                : new Color(0.92f, 0.12f, 0.075f));
    }

    private void SetAuthoredThreatColor(Color color)
    {
        if (IsInstanceValid(_authoredOperatorVisual?.Root))
        {
            _authoredOperatorVisual.SetTeamColor(color);
        }
    }
}
