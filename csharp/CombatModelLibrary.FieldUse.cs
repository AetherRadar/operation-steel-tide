using System;
using Godot;

namespace OperationSteelTide;

internal sealed class AuthoredFieldUsePropsVisual
{
    public AuthoredFieldUsePropsVisual(Node3D root)
    {
        Root = root;
        TraumaKit = CombatModelLibrary.RequireNode(root, "TraumaKit");
        TraumaKitLid = CombatModelLibrary.RequireNode(root, "TraumaKitLid");
        TraumaGauzePack = CombatModelLibrary.RequireNode(root, "TraumaGauzePack");
        TraumaInjector = CombatModelLibrary.RequireNode(root, "TraumaInjector");
        ArmorPlate = CombatModelLibrary.RequireNode(root, "ArmorPlate");
        ArmorCarrier = CombatModelLibrary.RequireNode(root, "ArmorCarrier");
        ArmorCarrierFlap = CombatModelLibrary.RequireNode(root, "ArmorCarrierFlap");
        TraumaPrimaryGrip = CombatModelLibrary.RequireNode(root, "TraumaPrimaryGrip");
        TraumaLidGrip = CombatModelLibrary.RequireNode(root, "TraumaLidGrip");
        TraumaGauzeGrip = CombatModelLibrary.RequireNode(root, "TraumaGauzeGrip");
        InjectorPrimaryGrip = CombatModelLibrary.RequireNode(root, "InjectorPrimaryGrip");
        ArmorPrimaryGrip = CombatModelLibrary.RequireNode(root, "ArmorPrimaryGrip");
        ArmorSupportGrip = CombatModelLibrary.RequireNode(root, "ArmorSupportGrip");

        _kitRest = TraumaKit.Transform;
        _lidRest = TraumaKitLid.Transform;
        _gauzeRest = TraumaGauzePack.Transform;
        _injectorRest = TraumaInjector.Transform;
        _plateRest = ArmorPlate.Transform;
        _carrierRest = ArmorCarrier.Transform;
        _flapRest = ArmorCarrierFlap.Transform;
        ResetPose();
    }

    public Node3D Root { get; }
    public Node3D TraumaKit { get; }
    public Node3D TraumaKitLid { get; }
    public Node3D TraumaGauzePack { get; }
    public Node3D TraumaInjector { get; }
    public Node3D ArmorPlate { get; }
    public Node3D ArmorCarrier { get; }
    public Node3D ArmorCarrierFlap { get; }
    public Node3D TraumaPrimaryGrip { get; }
    public Node3D TraumaLidGrip { get; }
    public Node3D TraumaGauzeGrip { get; }
    public Node3D InjectorPrimaryGrip { get; }
    public Node3D ArmorPrimaryGrip { get; }
    public Node3D ArmorSupportGrip { get; }

    public Transform3D KitRest => _kitRest;
    public Transform3D LidRest => _lidRest;
    public Transform3D GauzeRest => _gauzeRest;
    public Transform3D InjectorRest => _injectorRest;
    public Transform3D PlateRest => _plateRest;
    public Transform3D CarrierRest => _carrierRest;
    public Transform3D FlapRest => _flapRest;

    private readonly Transform3D _kitRest;
    private readonly Transform3D _lidRest;
    private readonly Transform3D _gauzeRest;
    private readonly Transform3D _injectorRest;
    private readonly Transform3D _plateRest;
    private readonly Transform3D _carrierRest;
    private readonly Transform3D _flapRest;

    public Transform3D MarkerTransformInRoot(Node3D marker)
        => Root.GlobalTransform.AffineInverse() * marker.GlobalTransform;

    public void ResetPose()
    {
        TraumaKit.Transform = _kitRest;
        TraumaKitLid.Transform = _lidRest;
        TraumaGauzePack.Transform = _gauzeRest;
        TraumaInjector.Transform = _injectorRest;
        ArmorPlate.Transform = _plateRest;
        ArmorCarrier.Transform = _carrierRest;
        ArmorCarrierFlap.Transform = _flapRest;
        TraumaKit.Visible = false;
        TraumaGauzePack.Visible = false;
        TraumaInjector.Visible = false;
        ArmorPlate.Visible = false;
        ArmorCarrier.Visible = false;
    }
}

internal readonly record struct AuthoredFieldUsePropsInspection(
    bool Loaded,
    bool ContractValid,
    int MeshCount,
    int MaterialCount,
    Vector3 BoundsSize);

internal static partial class CombatModelLibrary
{
    internal const string FieldUsePropsScenePath =
        "res://assets/models/steel_tide_field_use/field_use_props.glb";

    private static readonly string[] FieldUsePropsNodes =
    {
        "SteelTideFieldUseProps", "TraumaKit", "TraumaKitLid",
        "TraumaGauzePack", "TraumaInjector", "ArmorPlate", "ArmorCarrier",
        "ArmorCarrierFlap", "TraumaPrimaryGrip", "TraumaLidGrip",
        "TraumaGauzeGrip", "InjectorPrimaryGrip", "ArmorPrimaryGrip",
        "ArmorSupportGrip"
    };

    public static AuthoredFieldUsePropsVisual InstantiateFieldUseProps()
    {
        var root = InstantiateRequired(FieldUsePropsScenePath, FieldUsePropsNodes);
        root.Name = "AuthoredFieldUsePropsVisual";
        foreach (var geometry in GeometryBelow(root))
        {
            geometry.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        }
        return new AuthoredFieldUsePropsVisual(root);
    }

    public static AuthoredFieldUsePropsInspection InspectFieldUseProps()
    {
        AuthoredFieldUsePropsVisual? visual = null;
        try
        {
            visual = InstantiateFieldUseProps();
            var bounds = ComputeBounds(visual.Root);
            return new AuthoredFieldUsePropsInspection(
                true,
                HasDistinctFieldUseContract(visual),
                bounds.MeshCount,
                CountMaterials(visual.Root),
                bounds.Size);
        }
        catch (Exception)
        {
            return new AuthoredFieldUsePropsInspection(
                false,
                false,
                0,
                0,
                Vector3.Zero);
        }
        finally
        {
            visual?.Root.Free();
        }
    }

    private static bool HasDistinctFieldUseContract(AuthoredFieldUsePropsVisual visual)
    {
        Node3D[] nodes =
        {
            visual.Root,
            visual.TraumaKit,
            visual.TraumaKitLid,
            visual.TraumaGauzePack,
            visual.TraumaInjector,
            visual.ArmorPlate,
            visual.ArmorCarrier,
            visual.ArmorCarrierFlap,
            visual.TraumaPrimaryGrip,
            visual.TraumaLidGrip,
            visual.TraumaGauzeGrip,
            visual.InjectorPrimaryGrip,
            visual.ArmorPrimaryGrip,
            visual.ArmorSupportGrip
        };
        for (var left = 0; left < nodes.Length; left++)
        {
            if (!GodotObject.IsInstanceValid(nodes[left]))
            {
                return false;
            }
            for (var right = left + 1; right < nodes.Length; right++)
            {
                if (nodes[left].GetInstanceId() == nodes[right].GetInstanceId())
                {
                    return false;
                }
            }
        }
        return true;
    }
}
