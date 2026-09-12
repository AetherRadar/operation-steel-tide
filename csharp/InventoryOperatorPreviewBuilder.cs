using Godot;

namespace OperationSteelTide;

/// <summary>
/// Builds the requested authored operator preview. Required assets fail at
/// their owner; no alternate visual or silent recovery path is permitted.
/// </summary>
internal static class InventoryOperatorPreviewBuilder
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
        AuthoredPreviewOperatorVisual? previewVisual = null;
        AuthoredOperatorVisual? staticVisual = null;
        if (!staticLoadout)
        {
            previewVisual = CombatModelLibrary.InstantiatePreviewOperator(
                requestedVisual,
                weaponBuild,
                helmet,
                bodyArmor,
                backpack);
            previewRoot = previewVisual.Root;
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
            staticVisual = visual;
            visual.AnimationPlayer.Stop();
            visual.ApplyPreviewNeutralPose();
            previewRoot = visual.Root;
        }

        root.AddChild(previewRoot);
        if (previewVisual is not null)
        {
            previewVisual.FreezePreviewPose();
        }
        if (staticVisual is not null)
        {
            staticVisual.FreezePreviewPose();
            var staticBounds = CombatModelLibrary.ComputeBounds(staticVisual.Root);
            if (staticBounds.MeshCount > 0)
            {
                staticVisual.Root.Position -= staticBounds.Center;
            }
        }
    }
}

