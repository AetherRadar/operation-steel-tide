using Godot;

namespace OperationSteelTide;

public partial class TacticalPlayer
{
    private Node3D _heldThrowableRoot = null!;
    private Node3D _heldFragVisual = null!;
    private Node3D _heldSmokeVisual = null!;

    internal bool HeldFragmentationGrenadeVisibleForDiagnostics
        => IsInstanceValid(_heldFragVisual) && _heldFragVisual.Visible;
    internal bool HeldSmokeGrenadeVisibleForDiagnostics
        => IsInstanceValid(_heldSmokeVisual) && _heldSmokeVisual.Visible;
    internal int HeldFragmentationGrenadeMeshCountForDiagnostics
        => CountMeshes(_heldFragVisual);
    internal int HeldSmokeGrenadeMeshCountForDiagnostics
        => CountMeshes(_heldSmokeVisual);

    private void BuildHeldThrowables()
    {
        _heldThrowableRoot = new Node3D
        {
            Name = "HeldThrowableRoot",
            Position = new Vector3(0.31f, -0.31f, -0.58f),
            Rotation = new Vector3(-0.18f, -0.28f, 0.12f),
            Scale = Vector3.One * 1.35f,
            Visible = false
        };
        _camera.AddChild(_heldThrowableRoot);

        _heldFragVisual = GrenadeVisualFactory.CreateFragmentationGrenade(firstPerson: true);
        _heldFragVisual.Name = "HeldFragmentationGrenade";
        _heldThrowableRoot.AddChild(_heldFragVisual);

        _heldSmokeVisual = GrenadeVisualFactory.CreateSmokeGrenade(firstPerson: true);
        _heldSmokeVisual.Name = "HeldSmokeGrenade";
        _heldThrowableRoot.AddChild(_heldSmokeVisual);

        var glove = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.09f, 0.12f, 0.095f),
            Metallic = 0.04f,
            Roughness = 0.9f
        };
        _heldThrowableRoot.AddChild(new MeshInstance3D
        {
            Name = "ThrowingHand",
            Mesh = new CapsuleMesh { Radius = 0.08f, Height = 0.28f, RadialSegments = 14, Rings = 6 },
            Position = new Vector3(0.07f, -0.13f, 0.055f),
            Rotation = new Vector3(0.65f, 0.18f, -0.35f),
            MaterialOverride = glove,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        });
        UpdateHeldThrowableVisual();
    }

    private void UpdateHeldThrowableVisual()
    {
        if (!IsInstanceValid(_heldThrowableRoot))
        {
            return;
        }
        var canShow = !IsInVehicle
            && !IsExtractionPassenger
            && !UiLocked
            && !_isPlating
            && !RoleActionBlocksWeapon
            && !MedicalActionBlocksWeapon
            && !IsDead;
        var fragVisible = canShow
            && _activeQuickSlot == PlayerQuickSlot.FragmentationGrenade
            && Grenades > 0;
        var smokeVisible = canShow
            && _activeQuickSlot == PlayerQuickSlot.Utility
            && SmokeGrenades > 0;
        _heldThrowableRoot.Visible = fragVisible || smokeVisible;
        _heldFragVisual.Visible = fragVisible;
        _heldSmokeVisual.Visible = smokeVisible;
    }

    private static int CountMeshes(Node? root)
    {
        if (!IsInstanceValid(root))
        {
            return 0;
        }
        var count = root is MeshInstance3D ? 1 : 0;
        var children = root!.GetChildren();
        using var childrenBacking = children.AsDisposable();
        foreach (var child in children)
        {
            count += CountMeshes(child);
        }
        return count;
    }
}
