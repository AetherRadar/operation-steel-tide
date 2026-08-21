using System;
using Godot;

namespace OperationSteelTide;

internal sealed class AuthoredFirstPersonSmgVisual
{
    public AuthoredFirstPersonSmgVisual(Node3D root)
    {
        Root = root;
        Arms = CombatModelLibrary.RequireNode(root, "AuthoredArms");
        WeaponBody = CombatModelLibrary.RequireNode(root, "WeaponBody");
        Magazine = CombatModelLibrary.RequireNode(root, "MagazineGeometry");
        ChargingHandle = CombatModelLibrary.RequireNode(root, "ChargingHandleGeometry");
        Muzzle = CombatModelLibrary.RequireNode(root, "Muzzle");
    }

    public Node3D Root { get; }
    public Node3D Arms { get; }
    public Node3D WeaponBody { get; }
    public Node3D Magazine { get; }
    public Node3D ChargingHandle { get; }
    public Node3D Muzzle { get; }

    public void SyncMechanisms(Node3D magazine, Node3D chargingHandle)
    {
        Magazine.Visible = magazine.Visible;
        var reloadOffset = Mathf.Clamp(chargingHandle.Position.Z + 0.05f, -0.08f, 0.08f);
        ChargingHandle.Position = new Vector3(0.0f, 0.0f, reloadOffset);
    }
}

internal static partial class CombatModelLibrary
{
    internal const string Smg45FirstPersonScenePath =
        "res://assets/models/djmaesen_smg45/smg45_first_person.glb";
    internal const string Smg45WeaponScenePath =
        "res://assets/models/djmaesen_smg45/smg45_weapon.glb";

    private static readonly string[] Smg45FirstPersonNodes =
    {
        "DJMaesenSMG45FirstPerson", "AuthoredArms", "WeaponBody",
        "MagazineGeometry", "ChargingHandleGeometry", "Muzzle"
    };

    public static AuthoredFirstPersonSmgVisual InstantiateFirstPersonSmg45()
    {
        var root = InstantiateRequired(Smg45FirstPersonScenePath, Smg45FirstPersonNodes);
        root.Name = "AuthoredSMG45FirstPersonVisual";
        foreach (var geometry in GeometryBelow(root))
        {
            geometry.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        }
        return new AuthoredFirstPersonSmgVisual(root);
    }

    public static CombatModelInspection InspectFirstPersonSmg45()
    {
        Node3D? root = null;
        try
        {
            root = InstantiateFirstPersonSmg45().Root;
            var bounds = ComputeBounds(root);
            return new CombatModelInspection(
                true,
                true,
                bounds.MeshCount,
                CountMaterials(root),
                bounds.Size);
        }
        catch (Exception)
        {
            return new CombatModelInspection(false, false, 0, 0, Vector3.Zero);
        }
        finally
        {
            root?.Free();
        }
    }
}
