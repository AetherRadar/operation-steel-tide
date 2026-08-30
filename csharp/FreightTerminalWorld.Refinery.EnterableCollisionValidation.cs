using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private bool ValidateEnterableDoorways(
        JianghaiGameplayCollisionResult gameplay,
        IReadOnlyDictionary<string, CollisionShape3D[]> sourceGroups,
        out int doorClears,
        out int facadeWallHits,
        out int sideWallHits,
        out int backWallHits,
        out int linerSurfaceHits,
        out int wingWallHits,
        out int overhangClears,
        out string summary)
    {
        doorClears = 0;
        facadeWallHits = 0;
        sideWallHits = 0;
        backWallHits = 0;
        linerSurfaceHits = 0;
        wingWallHits = 0;
        overhangClears = 0;
        summary = "ok";
        var exclusions = BuildRefineryLaneExclusions();
        using var exclusionsBacking = exclusions.AsDisposable();
        var expectedBodyId = gameplay.Body.GetInstanceId();
        var space = GetWorld3D().DirectSpaceState;
        foreach (var sourceName in JianghaiGameplayCollisionContract.ExpectedEnterableSourceNames)
        {
            if (!sourceGroups.TryGetValue(sourceName, out var sourceShapes))
            {
                summary = $"door_source_missing:{sourceName}";
                return false;
            }

            var lintel = sourceShapes.SingleOrDefault(shape =>
                shape.GetMeta("gameplay_proxy_role", string.Empty).AsString()
                    == "front_lintel");
            if (lintel is null
                || !lintel.HasMeta("gameplay_doorway_center")
                || !lintel.HasMeta("gameplay_doorway_forward"))
            {
                summary = $"door_metadata_missing:{sourceName}";
                return false;
            }

            var center = lintel.GetMeta(
                "gameplay_doorway_center",
                Vector3.Zero).AsVector3();
            var forward = lintel.GetMeta(
                "gameplay_doorway_forward",
                Vector3.Zero).AsVector3();
            forward.Y = 0.0f;
            if (forward.LengthSquared() <= 0.9f)
            {
                summary = $"door_forward_invalid:{sourceName}";
                return false;
            }
            forward = forward.Normalized();
            var tangent = new Vector3(forward.Z, 0.0f, -forward.X).Normalized();
            if (!ValidateEnterableEnvelopeAlignment(
                    sourceName,
                    lintel,
                    center,
                    forward,
                    out summary))
            {
                return false;
            }
            if (PhysicsRaycast.TryHit(
                    space,
                    center + forward * 0.65f,
                    center - forward * 0.75f,
                    exclusions,
                    1,
                    out var doorHit))
            {
                summary = doorHit.Collider is Node blocker
                    ? $"door_blocked:{sourceName}:{blocker.Name}"
                    : $"door_blocked:{sourceName}:unknown";
                return false;
            }
            doorClears++;

            if (!JianghaiGameplayCollisionContract.TryGetEnterableRoom(
                    sourceName,
                    out var roomContract))
            {
                summary = $"wing_contract_missing:{sourceName}";
                return false;
            }

            foreach (var facadeRole in new[]
                {
                    "front_left",
                    "front_right",
                    "front_connector_left",
                    "front_connector_right",
                    "rear_connector_left",
                    "rear_connector_right",
                    "front_outer_connector_left",
                    "front_outer_connector_right",
                    "rear_outer_connector_left",
                    "rear_outer_connector_right"
                })
            {
                var facade = sourceShapes.SingleOrDefault(shape =>
                    shape.GetMeta("gameplay_proxy_role", string.Empty).AsString()
                        == facadeRole);
                if (facade is null
                    || !PhysicsRaycast.TryHit(
                        space,
                        facade.GlobalPosition + facade.GlobalBasis.Z * 0.55f,
                        facade.GlobalPosition - facade.GlobalBasis.Z * 0.55f,
                        exclusions,
                        1,
                        out var facadeHit)
                    || facadeHit.Collider is not Node facadeCollider
                    || facadeCollider.GetInstanceId() != expectedBodyId)
                {
                    summary = $"facade_open:{sourceName}:{facadeRole}";
                    return false;
                }
                facadeWallHits++;
            }

            foreach (var sideRole in new[] { "side_left", "side_right" })
            {
                var side = sourceShapes.SingleOrDefault(shape =>
                    shape.GetMeta("gameplay_proxy_role", string.Empty).AsString()
                        == sideRole);
                if (side is null
                    || !PhysicsRaycast.TryHit(
                        space,
                        side.GlobalPosition + tangent * 0.55f,
                        side.GlobalPosition - tangent * 0.55f,
                        exclusions,
                        1,
                        out var sideHit)
                    || sideHit.Collider is not Node sideCollider
                    || sideCollider.GetInstanceId() != expectedBodyId)
                {
                    summary = $"side_open:{sourceName}:{sideRole}";
                    return false;
                }
                var relativeCenter = side.GlobalPosition - center;
                var tangentDistance = Mathf.Abs(relativeCenter.Dot(tangent));
                var expectedSideDepth = (roomContract.SideFrontInset
                    + roomContract.SideRearInset) * 0.5f
                    - roomContract.FrontInset;
                var actualSideDepth = -relativeCenter.Dot(forward);
                if (Mathf.Abs(tangentDistance - roomContract.SideHalfWidth) > 0.015f
                    || Mathf.Abs(actualSideDepth - expectedSideDepth) > 0.015f)
                {
                    summary = $"side_alignment:{sourceName}:{sideRole}:"
                        + $"{tangentDistance:0.000}:{actualSideDepth:0.000}";
                    return false;
                }
                sideWallHits++;
            }
            foreach (var wingRole in new[]
                {
                    "front_wing_left",
                    "front_wing_right",
                    "rear_wing_left",
                    "rear_wing_right"
                })
            {
                var wing = sourceShapes.SingleOrDefault(shape =>
                    shape.GetMeta("gameplay_proxy_role", string.Empty).AsString()
                        == wingRole);
                if (wing is null
                    || !PhysicsRaycast.TryHit(
                        space,
                        wing.GlobalPosition + forward * 0.55f,
                        wing.GlobalPosition - forward * 0.55f,
                        exclusions,
                        1,
                        out var wingHit)
                    || wingHit.Collider is not Node wingCollider
                    || wingCollider.GetInstanceId() != expectedBodyId)
                {
                    summary = $"wing_open:{sourceName}:{wingRole}";
                    return false;
                }
                wingWallHits++;
            }
            var frontSideDepth = roomContract.WingFrontInset - roomContract.FrontInset;
            var rearSideDepth = roomContract.RearWingInset - roomContract.FrontInset;
            var clearDepths = new[]
            {
                frontSideDepth * 0.5f,
                (rearSideDepth + roomContract.CollisionDepth) * 0.5f
            };
            foreach (var clearDepth in clearDepths)
            {
                foreach (var side in new[] { -1.0f, 1.0f })
                {
                    var overhangProbe = center
                        - forward * clearDepth
                        + tangent * (roomContract.WingOuterHalfWidth * side);
                    if (PhysicsRaycast.HasHit(
                            GetWorld3D(),
                            overhangProbe + tangent * 0.30f,
                            overhangProbe - tangent * 0.30f,
                            exclusions,
                            1))
                    {
                        summary = $"overhang_blocked:{sourceName}:{clearDepth:0.00}:{side:+0;-0}";
                        return false;
                    }
                    overhangClears++;
                }
            }

            var back = sourceShapes.SingleOrDefault(shape =>
                shape.GetMeta("gameplay_proxy_role", string.Empty).AsString() == "back");
            if (back is null
                || !PhysicsRaycast.TryHit(
                    space,
                    back.GlobalPosition + forward * 0.55f,
                    back.GlobalPosition - forward * 0.55f,
                    exclusions,
                    1,
                    out var backHit)
                || backHit.Collider is not Node backCollider
                || backCollider.GetInstanceId() != expectedBodyId)
            {
                summary = $"back_open:{sourceName}";
                return false;
            }
            backWallHits++;

            var interiorWidth = lintel.GetMeta("gameplay_interior_width", 0.0f).AsSingle();
            var interiorDepth = lintel.GetMeta("gameplay_interior_depth", 0.0f).AsSingle();
            if (interiorWidth < roomContract.DoorWidth + 0.4f || interiorDepth < 2.8f)
            {
                summary = $"liner_contract_invalid:{sourceName}";
                return false;
            }
            foreach (var linerRole in new[] { "liner_left", "liner_right", "liner_back" })
            {
                var linerWall = sourceShapes.SingleOrDefault(shape =>
                    shape.GetMeta("gameplay_proxy_role", string.Empty).AsString()
                        == linerRole);
                var probeDirection = linerRole == "liner_back" ? forward : tangent;
                if (linerWall is null
                    || !PhysicsRaycast.TryHit(
                        space,
                        linerWall.GlobalPosition + probeDirection * 0.55f,
                        linerWall.GlobalPosition - probeDirection * 0.55f,
                        exclusions,
                        1,
                        out var linerHit)
                    || linerHit.Collider is not Node linerCollider
                    || linerCollider.GetInstanceId() != expectedBodyId)
                {
                    summary = $"liner_open:{sourceName}:{linerRole}";
                    return false;
                }
                var relative = linerWall.GlobalPosition - center;
                var expectedTangent = linerRole switch
                {
                    "liner_left" => -interiorWidth * 0.5f,
                    "liner_right" => interiorWidth * 0.5f,
                    _ => 0.0f
                };
                var expectedDepth = linerRole == "liner_back"
                    ? interiorDepth
                    : interiorDepth * 0.5f;
                if (Mathf.Abs(relative.Dot(tangent) - expectedTangent) > 0.02f
                    || Mathf.Abs(-relative.Dot(forward) - expectedDepth) > 0.02f)
                {
                    summary = $"liner_alignment:{sourceName}:{linerRole}";
                    return false;
                }
                linerSurfaceHits++;
            }
            var linerCeiling = sourceShapes.SingleOrDefault(shape =>
                shape.GetMeta("gameplay_proxy_role", string.Empty).AsString()
                    == "liner_ceiling");
            if (linerCeiling is null
                || !PhysicsRaycast.TryHit(
                    space,
                    linerCeiling.GlobalPosition + Vector3.Up * 0.55f,
                    linerCeiling.GlobalPosition - Vector3.Up * 0.55f,
                    exclusions,
                    1,
                    out var ceilingHit)
                || ceilingHit.Collider is not Node ceilingCollider
                || ceilingCollider.GetInstanceId() != expectedBodyId)
            {
                summary = $"liner_open:{sourceName}:liner_ceiling";
                return false;
            }
            linerSurfaceHits++;
        }
        return doorClears == JianghaiGameplayCollisionContract.ExpectedEnterableSourceCount
            && facadeWallHits == JianghaiGameplayCollisionContract.ExpectedEnterableSourceCount * 10
            && sideWallHits == JianghaiGameplayCollisionContract.ExpectedEnterableSourceCount * 2
            && backWallHits == JianghaiGameplayCollisionContract.ExpectedEnterableSourceCount
            && linerSurfaceHits
                == JianghaiGameplayCollisionContract.ExpectedEnterableSourceCount * 4
            && wingWallHits == JianghaiGameplayCollisionContract.ExpectedEnterableSourceCount * 4
            && overhangClears == JianghaiGameplayCollisionContract.ExpectedEnterableSourceCount * 4;
    }

    private bool ValidateEnterableEnvelopeAlignment(
        string sourceName,
        CollisionShape3D lintel,
        Vector3 doorwayCenter,
        Vector3 doorwayForward,
        out string summary)
    {
        summary = "ok";
        if (!JianghaiGameplayCollisionContract.TryGetEnterableRoom(
                sourceName,
                out var contract)
            || !MetaApproximately(lintel, "gameplay_shell_front_inset", contract.FrontInset)
            || !MetaApproximately(lintel, "gameplay_shell_width", contract.CollisionWidth)
            || !MetaApproximately(lintel, "gameplay_shell_depth", contract.CollisionDepth)
            || !MetaApproximately(lintel, "gameplay_shell_height", contract.CollisionHeight)
            || !MetaApproximately(lintel, "gameplay_facade_width", contract.FacadeWidth)
            || !MetaApproximately(
                lintel,
                "gameplay_wing_front_inset",
                contract.WingFrontInset)
            || !MetaApproximately(
                lintel,
                "gameplay_rear_wing_inset",
                contract.RearWingInset)
            || !MetaApproximately(
                lintel,
                "gameplay_wing_inner_half_width",
                contract.WingInnerHalfWidth)
            || !MetaApproximately(
                lintel,
                "gameplay_wing_outer_half_width",
                contract.WingOuterHalfWidth)
            || !MetaApproximately(
                lintel,
                "gameplay_side_half_width",
                contract.SideHalfWidth)
            || !MetaApproximately(
                lintel,
                "gameplay_side_front_inset",
                contract.SideFrontInset)
            || !MetaApproximately(
                lintel,
                "gameplay_side_rear_inset",
                contract.SideRearInset)
            || contract.WingInnerHalfWidth - contract.FacadeWidth * 0.5f is < 0.35f or > 0.80f
            || contract.RearWingInset - contract.WingFrontInset < 2.5f)
        {
            summary = $"envelope_contract_drift:{sourceName}";
            return false;
        }

        if (_jianghaiOldCityScene?.Root.FindChild(
                sourceName,
                recursive: true,
                owned: false) is not MeshInstance3D source)
        {
            summary = $"envelope_visual_missing:{sourceName}";
            return false;
        }
        var roomWidth = source.GetMeta("jianghai_room_width_m", 0.0f).AsSingle();
        var roomDepth = source.GetMeta("jianghai_room_depth_m", 0.0f).AsSingle();
        if (roomWidth < contract.DoorWidth + 0.4f
            || roomDepth < 2.8f
            || !MetaApproximately(lintel, "gameplay_interior_width", roomWidth)
            || !MetaApproximately(lintel, "gameplay_interior_depth", roomDepth))
        {
            summary = $"liner_visual_physics_drift:{sourceName}";
            return false;
        }

        var bounds = source.GetAabb();
        var visualFront = source.GlobalTransform * new Vector3(
            bounds.GetCenter().X,
            bounds.Position.Y,
            bounds.End.Z);
        var visualForward = source.GlobalBasis.Z;
        visualForward.Y = 0.0f;
        visualForward = visualForward.Normalized();
        var expectedFront = visualFront - visualForward * contract.FrontInset;
        var horizontalError = new Vector2(
            doorwayCenter.X - expectedFront.X,
            doorwayCenter.Z - expectedFront.Z).Length();
        if (horizontalError > 0.035f || doorwayForward.Dot(visualForward) < 0.999f)
        {
            summary = $"envelope_alignment:{sourceName}:{horizontalError:0.000}";
            return false;
        }
        return true;
    }

    private static bool MetaApproximately(
        Node node,
        string key,
        float expected)
        => node.HasMeta(key)
            && Mathf.Abs(node.GetMeta(key).AsSingle() - expected) <= 0.011f;
}
