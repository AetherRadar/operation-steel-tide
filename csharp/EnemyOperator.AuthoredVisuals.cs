using System;
using Godot;

namespace OperationSteelTide;

public partial class EnemyOperator
{
    private AuthoredOperatorVisual _authoredOperatorVisual = null!;
    private AuthoredOperatorAnimator _authoredOperatorAnimator = null!;
    private TideHunterMonsterVisual? _tideHunterMonsterVisual;
    private float _authoredAimHoldRemaining;

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
    internal string AuthoredAnimationForDiagnostics
        => UsesAuthoredOperatorForDiagnostics
            ? _authoredOperatorAnimator.CurrentAnimation
            : string.Empty;
    internal int AuthoredAnimationCountForDiagnostics
        => UsesAuthoredOperatorForDiagnostics
            ? _authoredOperatorAnimator.AnimationCount
            : 0;
    internal OperatorVisualId AuthoredVisualIdForDiagnostics
        => UsesAuthoredOperatorForDiagnostics
            ? _authoredOperatorVisual.VisualId
            : OperatorVisual;
    internal bool UsesTideHunterMonsterForDiagnostics
        => IsInstanceValid(_tideHunterMonsterVisual?.Root);
    internal int TideHunterMonsterMeshCountForDiagnostics
        => UsesTideHunterMonsterForDiagnostics ? _tideHunterMonsterVisual!.MeshCount : 0;
    internal int TideHunterMonsterAnimationCountForDiagnostics
        => UsesTideHunterMonsterForDiagnostics ? _tideHunterMonsterVisual!.AnimationCount : 0;
    internal string TideHunterMonsterAnimationForDiagnostics
        => UsesTideHunterMonsterForDiagnostics ? _tideHunterMonsterVisual!.CurrentAnimation : string.Empty;
    internal bool TideHunterMonsterDeathStartedForDiagnostics
        => UsesTideHunterMonsterForDiagnostics && _tideHunterMonsterVisual!.DeathStarted;

    internal void SetAuthoredCombatPoseForDiagnostics()
    {
        if (!UsesAuthoredOperatorForDiagnostics)
        {
            return;
        }
        _authoredOperatorVisual.SetWeaponVisible(true);
        _authoredOperatorVisual.SetWeaponReadied(true);
        _authoredOperatorAnimator.Update(
            0.0f,
            0.0f,
            weaponReadied: true,
            prone: false,
            crouched: false,
            aiming: true,
            downed: false,
            reviving: false,
            dead: false);
    }

    private void HoldAuthoredAimAfterShot()
        => _authoredAimHoldRemaining = Mathf.Max(_authoredAimHoldRemaining, 0.36f);

    internal void SetDemolitionRoundFrozenPose()
    {
        Velocity = Vector3.Zero;
        if (IsDead)
        {
            return;
        }

        _ = TryStandForCombatMovement();
        _combatStanceHoldRemaining = 0.0f;
        _combatStanceCooldown = 0.0f;
        _combatPressureRemaining = 0.0f;
        _combatCoverSearchCooldown = 0.0f;
        ClearAirborneCombatForRoundFreeze();
        ResetFlashbangState();
        _authoredAimHoldRemaining = 0.0f;
        if (UsesAuthoredOperatorForDiagnostics)
        {
            var weaponReadied = HasFireablePrimary;
            _authoredOperatorVisual.SetWeaponReadied(weaponReadied);
            _authoredOperatorAnimator.SetRestingPose(weaponReadied);
        }
        else if (UsesTideHunterMonsterForDiagnostics)
        {
            UpdateTideHunterVisual(0.0f);
        }
    }

    private void AnimateAuthoredOperator(float delta, float speed)
    {
        _authoredAimHoldRemaining = Mathf.Max(0.0f, _authoredAimHoldRemaining - delta);
        var weaponReadied = HasFireablePrimary && !IsDead;
        var target = EngageTargetNode;
        var visibleTargetInRange = Alerted
            && target is not null
            && IsInstanceValid(target)
            && _cachedLineOfSight
            && GlobalPosition.DistanceTo(target.GlobalPosition) <= CurrentFireRange * 1.05f;
        _authoredOperatorVisual.SetWeaponReadied(weaponReadied);
        _authoredOperatorAnimator.Update(
            delta,
            IsCombatAirborneAttack ? 0.0f : speed,
            weaponReadied,
            IsProne,
            IsCrouched,
            visibleTargetInRange
                || _authoredAimHoldRemaining > 0.0f
                || IsCombatAirborneAttack,
            downed: false,
            reviving: false,
            IsDead);
    }

    private void AttachAuthoredOperatorVisual()
    {
        if (IsWorldBoss)
        {
            return;
        }
        AuthoredOperatorVisual? authoredOperator = null;
        try
        {
            authoredOperator = CombatModelLibrary.InstantiateOperator(
                OperatorVisual,
                weaponBuild: HasFireablePrimary ? CarriedWeapon : null,
                attachDefaultWeapon: false,
                helmet: EquippedHelmet,
                bodyArmor: EquippedBodyArmor,
                backpack: EquippedBackpack);
            _bodyRoot.AddChild(authoredOperator.Root);
            var authoredAnimator = new AuthoredOperatorAnimator(authoredOperator);
            _authoredOperatorVisual = authoredOperator;
            _authoredOperatorAnimator = authoredAnimator;
        }
        catch (Exception exception)
        {
            authoredOperator?.Root.QueueFree();
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
        SetAuthoredThreatColor(ResolveAuthoredFactionAppearance().Patch);
    }

    private void AttachTideHunterMonsterVisual()
    {
        var monster = TideHunterMonsterLibrary.Instantiate();
        _bodyRoot.AddChild(monster.Root);
        _tideHunterMonsterVisual = monster;
        _tideHunterMonsterVisual.SetPhase(WorldBossPhase);
        foreach (var child in _bodyRoot.GetChildren())
        {
            if (child is Node3D visual
                && visual != _tideHunterMonsterVisual.Root
                && visual != _carriedWeaponRoot)
            {
                visual.QueueFree();
            }
        }
        _carriedWeaponRoot.Visible = false;
        foreach (var mesh in CombatModelLibrary.MeshesBelow(_carriedWeaponRoot))
        {
            mesh.Visible = false;
        }
    }

    private void UpdateAuthoredStanceCollider()
    {
        if (!IsInstanceValid(_collider) || _collider.Shape is not CapsuleShape3D capsule)
        {
            return;
        }
        var height = CombatColliderHeight;
        capsule.Height = height;
        _collider.Position = new Vector3(0.0f, height * 0.5f, 0.0f);
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

    private void SetAuthoredWeaponVisible(bool visible)
    {
        if (IsInstanceValid(_authoredOperatorVisual?.Root))
        {
            _authoredOperatorVisual.SetWeaponVisible(visible);
        }
    }

    private void UpdateTideHunterVisual(float speed)
    {
        if (UsesTideHunterMonsterForDiagnostics)
        {
            _tideHunterMonsterVisual!.Update(speed);
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
