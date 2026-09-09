using Godot;

namespace OperationSteelTide;

/// <summary>
/// Builds the requested authored operator preview. Required assets fail at
/// their owner; no alternate visual or silent recovery path is permitted.
/// </summary>
internal static class InventoryOperatorPreviewRecovery
{
    public static void Build(
        Node3D root,
        OperatorVisualId requestedVisual,
        WeaponBuild? weaponBuild = null,
        bool staticLoadout = false,
        EquipmentItem? helmet = null,
        EquipmentItem? bodyArmor = null,
        EquipmentItem? backpack = null)
    {
        Node3D previewRoot;
        if (!staticLoadout)
        {
            previewRoot = CombatModelLibrary
                .InstantiatePreviewOperator(requestedVisual, weaponBuild, helmet, bodyArmor, backpack)
                .Root;
        }
        else
        {
            var visual = CombatModelLibrary.InstantiateOperator(
                requestedVisual,
                weaponBuild: weaponBuild,
                attachDefaultWeapon: weaponBuild is not null,
                helmet: helmet,
                bodyArmor: bodyArmor,
                backpack: backpack);
            visual.AnimationPlayer.Stop();
            visual.ApplyPreviewNeutralPose();
            var staticBounds = CombatModelLibrary.ComputeBounds(visual.Root);
            if (staticBounds.MeshCount > 0)
            {
                visual.Root.Position -= staticBounds.Center;
            }
            previewRoot = visual.Root;
        }

        root.AddChild(previewRoot);
    }
}
