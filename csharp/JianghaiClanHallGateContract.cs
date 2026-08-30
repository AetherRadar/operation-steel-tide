using System;
using Godot;

namespace OperationSteelTide;

internal readonly record struct JianghaiClanHallGateGeometry(
    Node3D Anchor,
    float Width,
    float Height,
    Basis Basis,
    Vector3 Position,
    Vector3 Tangent,
    Vector3 Outward,
    Vector3 Inward);

/// <summary>Resolves the DCC-authored clan-hall portal shared by doors and collision.</summary>
internal static class JianghaiClanHallGateContract
{
    public const string AnchorName = "JianghaiClanHallDoubleGateAnchor";
    public const string SourceName = "GuangchangClanHall";
    public const float WorldMinimumX = -94.440f;
    public const float WorldMaximumX = -77.483f;
    public const float WorldMinimumY = 0.180f;
    public const float WorldMaximumY = 18.622f;
    public const float WorldMinimumZ = -137.429f;
    public const float WorldMaximumZ = -119.784f;
    public const float EntryRampRun = 3.8f;
    public const float EntryRampHighInset = 0.20f;
    public const float EntryRampStreetY = 0.0f;
    public const float DoorInset = 0.22f;
    public const float ExpectedGateWidth = 3.6878103f;
    public const float ExpectedGateHeight = 4.0285267f;
    public static readonly Vector3 ExpectedGatePosition = new(
        -86.001892f,
        1.2787764f,
        -122.576271f);

    private const string ContractVersionMeta = "gate_contract_version";
    private const string WidthMeta = "gate_width_m";
    private const string HeightMeta = "gate_height_m";
    private const string FloorMeta = "gate_floor_y_m";
    private const string OutwardAxisMeta = "gate_outward_axis";
    private const string SourceObjectMeta = "gate_source_object";
    private const string TangentAxisMeta = "gate_tangent_axis";
    private const string UpAxisMeta = "gate_up_axis";

    public static bool TryResolve(
        Node3D? authoredRoot,
        out JianghaiClanHallGateGeometry geometry,
        out string error)
    {
        geometry = default;
        error = "missing_authored_scene";
        if (!GodotObject.IsInstanceValid(authoredRoot))
        {
            return false;
        }

        var matches = authoredRoot!.FindChildren(
            AnchorName,
            "Node3D",
            recursive: true,
            owned: false);
        using var matchesBacking = matches.AsDisposable();
        if (matches.Count != 1 || matches[0] is not Node3D anchor)
        {
            error = $"anchor_{AnchorName}_count_{matches.Count}";
            return false;
        }
        if (!TryReadPositiveMeta(anchor, ContractVersionMeta, out var contractVersion)
            || Mathf.Abs(contractVersion - 1.0f) > 0.001f)
        {
            error = $"anchor_{AnchorName}_meta_{ContractVersionMeta}";
            return false;
        }
        if (!TryReadPositiveMeta(anchor, WidthMeta, out var width))
        {
            error = $"anchor_{AnchorName}_meta_{WidthMeta}";
            return false;
        }
        if (!TryReadPositiveMeta(anchor, HeightMeta, out var height))
        {
            error = $"anchor_{AnchorName}_meta_{HeightMeta}";
            return false;
        }
        if (!TryReadPositiveMeta(anchor, FloorMeta, out var floorY))
        {
            error = $"anchor_{AnchorName}_meta_{FloorMeta}";
            return false;
        }
        if (!TryReadStringMeta(anchor, OutwardAxisMeta, out var outwardAxis)
            || outwardAxis != "+Z")
        {
            error = $"anchor_{AnchorName}_meta_{OutwardAxisMeta}";
            return false;
        }
        if (!TryReadStringMeta(anchor, SourceObjectMeta, out var sourceObject)
            || sourceObject != SourceName)
        {
            error = $"anchor_{AnchorName}_meta_{SourceObjectMeta}";
            return false;
        }
        if (!TryReadStringMeta(anchor, TangentAxisMeta, out var tangentAxis)
            || tangentAxis != "+X"
            || !TryReadStringMeta(anchor, UpAxisMeta, out var upAxis)
            || upAxis != "+Y")
        {
            error = $"anchor_{AnchorName}_axis_metadata";
            return false;
        }
        if (width is < 2.0f or > 12.0f || height is < 2.0f or > 8.0f)
        {
            error = $"anchor_{AnchorName}_dimensions_{width:0.000}x{height:0.000}";
            return false;
        }
        if (Mathf.Abs(width - ExpectedGateWidth) > 0.02f
            || Mathf.Abs(height - ExpectedGateHeight) > 0.02f)
        {
            error = $"anchor_{AnchorName}_authored_dimensions_{width:0.000}x{height:0.000}";
            return false;
        }

        var basis = anchor.GlobalBasis.Orthonormalized();
        if (basis.X.Dot(Vector3.Right) < 0.999f
            || basis.Y.Dot(Vector3.Up) < 0.999f
            || basis.Z.Dot(Vector3.Back) < 0.999f)
        {
            error = $"anchor_{AnchorName}_authored_basis";
            return false;
        }
        var tangent = basis.X;
        tangent.Y = 0.0f;
        var outward = basis.Z;
        outward.Y = 0.0f;
        if (tangent.LengthSquared() < 0.95f || outward.LengthSquared() < 0.95f)
        {
            error = $"anchor_{AnchorName}_basis_vertical";
            return false;
        }
        tangent = tangent.Normalized();
        outward = outward.Normalized();
        if (Mathf.Abs(tangent.Dot(outward)) > 0.01f)
        {
            error = $"anchor_{AnchorName}_basis_nonorthogonal";
            return false;
        }

        var hallCenter = new Vector3(
            (WorldMinimumX + WorldMaximumX) * 0.5f,
            anchor.GlobalPosition.Y,
            (WorldMinimumZ + WorldMaximumZ) * 0.5f);
        var inward = hallCenter - anchor.GlobalPosition;
        inward.Y = 0.0f;
        if (inward.LengthSquared() < 1.0f)
        {
            error = $"anchor_{AnchorName}_not_on_facade";
            return false;
        }
        inward = inward.Normalized();
        if (outward.Dot(-inward) < 0.95f)
        {
            error = $"anchor_{AnchorName}_outward_axis_invalid";
            return false;
        }
        if (Mathf.Abs(anchor.GlobalPosition.Y - floorY) > 0.01f
            || anchor.GlobalPosition.Y < WorldMinimumY
            || anchor.GlobalPosition.Y > WorldMaximumY)
        {
            error = $"anchor_{AnchorName}_height_{anchor.GlobalPosition.Y:0.000}";
            return false;
        }
        if (anchor.GlobalPosition.DistanceTo(ExpectedGatePosition) > 0.025f)
        {
            error = $"anchor_{AnchorName}_position_{anchor.GlobalPosition}";
            return false;
        }

        geometry = new JianghaiClanHallGateGeometry(
            anchor,
            width,
            height,
            basis,
            anchor.GlobalPosition,
            tangent,
            outward,
            inward);
        error = "none";
        return true;
    }

    public static Vector3 RampTraversalPoint(
        JianghaiClanHallGateGeometry gate,
        float outwardDistance,
        float feetClearance = 0.12f)
    {
        var lowDistance = EntryRampHighInset + EntryRampRun;
        var heightRatio = Mathf.Clamp(
            (lowDistance - outwardDistance) / EntryRampRun,
            0.0f,
            1.0f);
        var point = gate.Position + gate.Outward * outwardDistance;
        point.Y = Mathf.Lerp(EntryRampStreetY, gate.Position.Y, heightRatio)
            + feetClearance;
        return point;
    }

    private static bool TryReadPositiveMeta(Node anchor, string key, out float value)
    {
        value = 0.0f;
        if (!TryReadAuthoredMeta(anchor, key, out var authoredValue))
        {
            return false;
        }
        if (authoredValue.VariantType is not Variant.Type.Int
            and not Variant.Type.Float)
        {
            return false;
        }
        value = (float)authoredValue.AsDouble();
        return float.IsFinite(value) && value > 0.0f;
    }

    private static bool TryReadStringMeta(Node anchor, string key, out string value)
    {
        value = string.Empty;
        if (!TryReadAuthoredMeta(anchor, key, out var authoredValue))
        {
            return false;
        }
        if (authoredValue.VariantType is not Variant.Type.String
            and not Variant.Type.StringName)
        {
            return false;
        }
        value = authoredValue.AsString();
        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool TryReadAuthoredMeta(Node anchor, string key, out Variant value)
    {
        value = default;
        if (anchor.HasMeta(key))
        {
            value = anchor.GetMeta(key);
            return true;
        }

        // Godot 4.6 preserves glTF node extras as one Dictionary metadata value.
        // Accept flattened metadata as well so the contract survives importer changes.
        if (!anchor.HasMeta("extras"))
        {
            return false;
        }
        var extrasValue = anchor.GetMeta("extras");
        if (extrasValue.VariantType != Variant.Type.Dictionary)
        {
            return false;
        }
        var extras = extrasValue.AsGodotDictionary();
        if (!extras.ContainsKey(key))
        {
            return false;
        }
        value = extras[key];
        return true;
    }
}
