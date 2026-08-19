using System;
using Godot;

namespace OperationSteelTide;

public partial class EnemyOperator
{
    private AuthoredOperatorVisual _authoredOperatorVisual = null!;

    internal bool UsesAuthoredOperatorForDiagnostics
        => IsInstanceValid(_authoredOperatorVisual?.Root);
    internal Color AuthoredTeamColorForDiagnostics
        => UsesAuthoredOperatorForDiagnostics
            ? _authoredOperatorVisual.TeamColorForDiagnostics
            : Colors.Transparent;
    internal Color AuthoredGearTintForDiagnostics
        => UsesAuthoredOperatorForDiagnostics
            ? _authoredOperatorVisual.GearTintForDiagnostics
            : Colors.Transparent;
    internal int AuthoredGearOverlayCountForDiagnostics
        => UsesAuthoredOperatorForDiagnostics
            ? _authoredOperatorVisual.GearOverlayCountForDiagnostics
            : 0;

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
        SetAuthoredThreatColor(ResolveAuthoredFactionAppearance().Patch);
    }

    private void SetAuthoredThreatColor(Color color)
    {
        if (IsInstanceValid(_authoredOperatorVisual?.Root))
        {
            _authoredOperatorVisual.SetFactionAppearance(
                color,
                ResolveAuthoredFactionAppearance().GearTint);
        }
    }

    private (Color Patch, Color GearTint) ResolveAuthoredFactionAppearance()
    {
        if (IsWorldBoss)
        {
            return (
                new Color(0.12f, 0.94f, 0.76f),
                new Color(0.02f, 0.30f, 0.27f, 0.28f));
        }
        if (!IsRivalSquad)
        {
            return (
                new Color(0.06f, 0.92f, 0.74f),
                new Color(0.015f, 0.34f, 0.25f, 0.30f));
        }
        return (Math.Abs(TeamId) % 4) switch
        {
            1 => (
                new Color(1.0f, 0.38f, 0.07f),
                new Color(0.34f, 0.055f, 0.018f, 0.24f)),
            2 => (
                new Color(0.96f, 0.16f, 0.52f),
                new Color(0.30f, 0.025f, 0.14f, 0.24f)),
            3 => (
                new Color(1.0f, 0.70f, 0.08f),
                new Color(0.28f, 0.18f, 0.015f, 0.24f)),
            _ => (
                new Color(0.58f, 0.34f, 1.0f),
                new Color(0.12f, 0.055f, 0.32f, 0.24f))
        };
    }
}
