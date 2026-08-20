using System;
using Godot;

namespace OperationSteelTide;

public partial class SquadMate
{
    private AuthoredOperatorVisual _authoredOperatorVisual = null!;
    private AuthoredOperatorAnimator _authoredOperatorAnimator = null!;

    internal bool UsesAuthoredOperatorForDiagnostics
        => IsInstanceValid(_authoredOperatorVisual?.Root);
    internal string AuthoredAnimationForDiagnostics
        => UsesAuthoredOperatorForDiagnostics
            ? _authoredOperatorAnimator.CurrentAnimation
            : string.Empty;
    internal int AuthoredAnimationCountForDiagnostics
        => UsesAuthoredOperatorForDiagnostics
            ? _authoredOperatorAnimator.AnimationCount
            : 0;

    private void AttachAuthoredOperatorVisual()
    {
        try
        {
            var authoredOperator = CombatModelLibrary.InstantiateOperator(
                WeaponCatalog.Build(WeaponPlatform.M4A1, 0));
            _rig.AddChild(authoredOperator.Root);
            _authoredOperatorVisual = authoredOperator;
            _authoredOperatorAnimator = new AuthoredOperatorAnimator(authoredOperator);
        }
        catch (Exception exception)
        {
            GD.PushWarning($"Authored squad operator unavailable; retaining procedural visual: {exception.Message}");
            return;
        }
        var children = _rig.GetChildren();
        using var childrenBacking = children.AsDisposable();
        foreach (var child in children)
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

    private void SetAuthoredWeaponVisible(bool visible)
    {
        if (IsInstanceValid(_authoredOperatorVisual?.Root))
        {
            _authoredOperatorVisual.SetWeaponVisible(visible);
        }
    }

    private void UpdateAuthoredStanceCollider()
    {
        if (!IsInstanceValid(_collider) || _collider.Shape is not CapsuleShape3D capsule)
        {
            return;
        }
        var kneeling = _revivePoseBlend > 0.5f && !IsDowned;
        var height = IsDowned ? 0.72f : kneeling ? 1.18f : 1.76f;
        capsule.Height = height;
        _collider.Position = new Vector3(0.0f, height * 0.5f, 0.0f);
    }
}
