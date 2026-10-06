using System;
using Godot;

namespace OperationSteelTide;

public partial class TacticalPlayer
{
    private void RefreshFirstPersonHandAppearance()
    {
        RebuildStaticFirstPersonArmRig(
            ref _authoredRifleArms,
            () => CombatModelLibrary.InstantiateFirstPersonRifleArms(Role));
        RebuildStaticFirstPersonArmRig(
            ref _authoredPistolServiceArms,
            () => CombatModelLibrary.InstantiateFirstPersonPistolServiceArms(Role));
        RebuildStaticFirstPersonArmRig(
            ref _authoredPistolLargeArms,
            () => CombatModelLibrary.InstantiateFirstPersonPistolLargeArms(Role));

        if (_authoredFirstPersonSmg is { } smg
            && GodotObject.IsInstanceValid(smg.Root))
        {
            var oldRoot = smg.Root;
            oldRoot.GetParent()?.RemoveChild(oldRoot);
            oldRoot.Free();
            _authoredFirstPersonSmg = null!;
            _authoredSmgWeaponBodyReadyTransformCaptured = false;
            if (EquippedWeapon.Platform == WeaponPlatform.M3A1)
            {
                EnsureAuthoredFirstPersonSmg();
            }
        }

        if (_authoredAnimatedReloadArms is { } reloadArms
            && GodotObject.IsInstanceValid(reloadArms.Root))
        {
            var oldRoot = reloadArms.Root;
            oldRoot.GetParent()?.RemoveChild(oldRoot);
            oldRoot.Free();
            _authoredAnimatedReloadArms = null!;
            _animatedReloadArmsLoadAttempted = false;
            if (UsesRifleAnimatedReloadArms(EquippedWeapon.Platform))
            {
                EnsureAuthoredAnimatedReloadArms();
            }
        }

        if (_meleeArms is { } meleeArms
            && GodotObject.IsInstanceValid(meleeArms.Root))
        {
            RebuildKnife();
        }
        if (GodotObject.IsInstanceValid(_ladderHandsRoot))
        {
            RebuildLadderHandsForRole();
        }
        _fieldUsePresentation?.SetRole(Role);

        if (IsInstanceValid(_weaponRoot))
        {
            RefreshAuthoredPrimaryWeapon();
        }
    }

    private static void RebuildStaticFirstPersonArmRig(
        ref AuthoredFirstPersonArmsVisual rig,
        Func<AuthoredFirstPersonArmsVisual> factory)
    {
        if (rig is null || !GodotObject.IsInstanceValid(rig.Root))
        {
            return;
        }

        var oldRoot = rig.Root;
        var parent = oldRoot.GetParent();
        var visible = oldRoot.Visible;
        var rightArmVisible = rig.RightArm.Visible;
        var leftArmVisible = rig.LeftArm.Visible;
        parent?.RemoveChild(oldRoot);
        oldRoot.Free();

        var rebuilt = factory();
        parent?.AddChild(rebuilt.Root);
        rebuilt.Root.Visible = visible;
        rebuilt.RightArm.Visible = rightArmVisible;
        rebuilt.LeftArm.Visible = leftArmVisible;
        rig = rebuilt;
    }

    private void RebuildLadderHandsForRole()
    {
        var visible = _ladderHandsRoot.Visible;
        foreach (var child in _ladderHandsRoot.GetChildren())
        {
            if (child is not Node node)
            {
                continue;
            }

            _ladderHandsRoot.RemoveChild(node);
            node.Free();
        }

        var authored = CombatModelLibrary.InstantiateFirstPersonLadderHands(Role);
        _ladderHandsRoot.AddChild(authored);
        _ladderLeftHand = CombatModelLibrary.RequireNodeEnding(authored, "LeftArm");
        _ladderRightHand = CombatModelLibrary.RequireNodeEnding(authored, "RightArm");
        _ladderLeftHandRest = _ladderLeftHand.Position;
        _ladderRightHandRest = _ladderRightHand.Position;
        _ladderHandsRoot.Visible = visible;
    }
}
