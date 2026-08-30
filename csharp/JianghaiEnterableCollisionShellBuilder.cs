using System;
using Godot;

namespace OperationSteelTide;

/// <summary>
/// Builds the segmented, box-only collision shell around one enterable JiangHai building.
/// </summary>
internal static class JianghaiEnterableCollisionShellBuilder
{
    public static int Build(
        StaticBody3D body,
        MeshInstance3D source,
        Basis basis,
        JianghaiEnterableRoomGeometry room,
        int shapeIndex)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(source);

        const float wallThickness = 0.24f;
        const float roofThickness = 0.24f;
        var center = room.Center;
        var size = room.Size;
        var bottom = center.Y - size.Y * 0.5f;
        var roomFront = center + basis.Z * (size.Z * 0.5f);
        var visualFront = roomFront + basis.Z * room.FrontInset;
        var frontWingPlane = visualFront - basis.Z * room.WingFrontInset;
        var rearWingPlane = visualFront - basis.Z * room.RearWingInset;
        var sideFrontCenter = visualFront - basis.Z * room.SideFrontInset;
        var sideRearCenter = visualFront - basis.Z * room.SideRearInset;
        var sideSpan = room.SideRearInset - room.SideFrontInset;
        var sideCenter = (sideFrontCenter + sideRearCenter) * 0.5f;
        AddBox(
            body,
            source,
            basis,
            sideCenter - basis.X * room.SideHalfWidth,
            new Vector3(wallThickness, size.Y, sideSpan),
            shapeIndex,
            "side_left");
        AddBox(
            body,
            source,
            basis,
            sideCenter + basis.X * room.SideHalfWidth,
            new Vector3(wallThickness, size.Y, sideSpan),
            shapeIndex + 1,
            "side_right");
        var backCenter = center - basis.Z * (size.Z * 0.5f - wallThickness * 0.5f);
        AddBox(
            body,
            source,
            basis,
            backCenter,
            new Vector3(room.FacadeWidth, size.Y, wallThickness),
            shapeIndex + 2,
            "back");

        var frontCenter = roomFront - basis.Z * (wallThickness * 0.5f);
        var frontSideWidth = (room.FacadeWidth - room.DoorWidth) * 0.5f;
        var frontSideOffset = (room.DoorWidth + frontSideWidth) * 0.5f;
        AddBox(
            body,
            source,
            basis,
            frontCenter - basis.X * frontSideOffset,
            new Vector3(frontSideWidth, size.Y, wallThickness),
            shapeIndex + 3,
            "front_left");
        AddBox(
            body,
            source,
            basis,
            frontCenter + basis.X * frontSideOffset,
            new Vector3(frontSideWidth, size.Y, wallThickness),
            shapeIndex + 4,
            "front_right");
        var wingWidth = room.WingOuterHalfWidth - room.WingInnerHalfWidth;
        var wingOffset = (room.WingInnerHalfWidth + room.WingOuterHalfWidth) * 0.5f;
        var wingFrontCenter = frontWingPlane - basis.Z * (wallThickness * 0.5f);
        AddBox(
            body,
            source,
            basis,
            wingFrontCenter - basis.X * wingOffset,
            new Vector3(wingWidth, size.Y, wallThickness),
            shapeIndex + 5,
            "front_wing_left");
        AddBox(
            body,
            source,
            basis,
            wingFrontCenter + basis.X * wingOffset,
            new Vector3(wingWidth, size.Y, wallThickness),
            shapeIndex + 6,
            "front_wing_right");
        var connectorDepth = room.WingFrontInset - room.FrontInset;
        var connectorWidth = room.WingInnerHalfWidth - room.FacadeWidth * 0.5f;
        var connectorLength = Mathf.Sqrt(
            connectorWidth * connectorWidth + connectorDepth * connectorDepth);
        var connectorCenter = (frontCenter + wingFrontCenter) * 0.5f;
        var rightConnectorX = (
            basis.X * connectorWidth - basis.Z * connectorDepth).Normalized();
        var rightConnectorBasis = new Basis(
            rightConnectorX,
            basis.Y,
            rightConnectorX.Cross(basis.Y).Normalized());
        AddBox(
            body,
            source,
            rightConnectorBasis,
            connectorCenter
                + basis.X * ((room.FacadeWidth * 0.5f + room.WingInnerHalfWidth) * 0.5f),
            new Vector3(connectorLength + 0.20f, size.Y, wallThickness),
            shapeIndex + 7,
            "front_connector_right");
        var leftConnectorX = (
            -basis.X * connectorWidth - basis.Z * connectorDepth).Normalized();
        var leftConnectorBasis = new Basis(
            leftConnectorX,
            basis.Y,
            leftConnectorX.Cross(basis.Y).Normalized());
        AddBox(
            body,
            source,
            leftConnectorBasis,
            connectorCenter
                - basis.X * ((room.FacadeWidth * 0.5f + room.WingInnerHalfWidth) * 0.5f),
            new Vector3(connectorLength + 0.20f, size.Y, wallThickness),
            shapeIndex + 8,
            "front_connector_left");
        var rearWingCenter = rearWingPlane + basis.Z * (wallThickness * 0.5f);
        AddBox(
            body,
            source,
            basis,
            rearWingCenter - basis.X * wingOffset,
            new Vector3(wingWidth, size.Y, wallThickness),
            shapeIndex + 9,
            "rear_wing_left");
        AddBox(
            body,
            source,
            basis,
            rearWingCenter + basis.X * wingOffset,
            new Vector3(wingWidth, size.Y, wallThickness),
            shapeIndex + 10,
            "rear_wing_right");
        var rearConnectorDepth = room.FrontInset + size.Z - room.RearWingInset;
        var rearConnectorLength = Mathf.Sqrt(
            connectorWidth * connectorWidth + rearConnectorDepth * rearConnectorDepth);
        var rearConnectorCenter = (backCenter + rearWingCenter) * 0.5f;
        var rightRearConnectorX = (
            basis.X * connectorWidth + basis.Z * rearConnectorDepth).Normalized();
        var rightRearConnectorBasis = new Basis(
            rightRearConnectorX,
            basis.Y,
            rightRearConnectorX.Cross(basis.Y).Normalized());
        AddBox(
            body,
            source,
            rightRearConnectorBasis,
            rearConnectorCenter
                + basis.X * ((room.FacadeWidth * 0.5f + room.WingInnerHalfWidth) * 0.5f),
            new Vector3(rearConnectorLength + 0.20f, size.Y, wallThickness),
            shapeIndex + 11,
            "rear_connector_right");
        var leftRearConnectorX = (
            -basis.X * connectorWidth + basis.Z * rearConnectorDepth).Normalized();
        var leftRearConnectorBasis = new Basis(
            leftRearConnectorX,
            basis.Y,
            leftRearConnectorX.Cross(basis.Y).Normalized());
        AddBox(
            body,
            source,
            leftRearConnectorBasis,
            rearConnectorCenter
                - basis.X * ((room.FacadeWidth * 0.5f + room.WingInnerHalfWidth) * 0.5f),
            new Vector3(rearConnectorLength + 0.20f, size.Y, wallThickness),
            shapeIndex + 12,
            "rear_connector_left");
        var frontOuterWidth = room.SideHalfWidth - room.WingOuterHalfWidth;
        var frontOuterDepth = room.SideFrontInset
            - room.WingFrontInset
            - wallThickness * 0.5f;
        var frontOuterLength = Mathf.Sqrt(
            frontOuterWidth * frontOuterWidth + frontOuterDepth * frontOuterDepth);
        var frontOuterCenter = (wingFrontCenter + sideFrontCenter) * 0.5f;
        var rightFrontOuterX = (
            basis.X * frontOuterWidth - basis.Z * frontOuterDepth).Normalized();
        var rightFrontOuterBasis = new Basis(
            rightFrontOuterX,
            basis.Y,
            rightFrontOuterX.Cross(basis.Y).Normalized());
        AddBox(
            body,
            source,
            rightFrontOuterBasis,
            frontOuterCenter
                + basis.X * ((room.WingOuterHalfWidth + room.SideHalfWidth) * 0.5f),
            new Vector3(frontOuterLength + 0.20f, size.Y, wallThickness),
            shapeIndex + 13,
            "front_outer_connector_right");
        var leftFrontOuterX = (
            -basis.X * frontOuterWidth - basis.Z * frontOuterDepth).Normalized();
        var leftFrontOuterBasis = new Basis(
            leftFrontOuterX,
            basis.Y,
            leftFrontOuterX.Cross(basis.Y).Normalized());
        AddBox(
            body,
            source,
            leftFrontOuterBasis,
            frontOuterCenter
                - basis.X * ((room.WingOuterHalfWidth + room.SideHalfWidth) * 0.5f),
            new Vector3(frontOuterLength + 0.20f, size.Y, wallThickness),
            shapeIndex + 14,
            "front_outer_connector_left");
        var rearOuterDepth = room.RearWingInset
            - wallThickness * 0.5f
            - room.SideRearInset;
        var rearOuterLength = Mathf.Sqrt(
            frontOuterWidth * frontOuterWidth + rearOuterDepth * rearOuterDepth);
        var rearOuterCenter = (sideRearCenter + rearWingCenter) * 0.5f;
        var rightRearOuterX = (
            -basis.X * frontOuterWidth - basis.Z * rearOuterDepth).Normalized();
        var rightRearOuterBasis = new Basis(
            rightRearOuterX,
            basis.Y,
            rightRearOuterX.Cross(basis.Y).Normalized());
        AddBox(
            body,
            source,
            rightRearOuterBasis,
            rearOuterCenter
                + basis.X * ((room.WingOuterHalfWidth + room.SideHalfWidth) * 0.5f),
            new Vector3(rearOuterLength + 0.20f, size.Y, wallThickness),
            shapeIndex + 15,
            "rear_outer_connector_right");
        var leftRearOuterX = (
            basis.X * frontOuterWidth - basis.Z * rearOuterDepth).Normalized();
        var leftRearOuterBasis = new Basis(
            leftRearOuterX,
            basis.Y,
            leftRearOuterX.Cross(basis.Y).Normalized());
        AddBox(
            body,
            source,
            leftRearOuterBasis,
            rearOuterCenter
                - basis.X * ((room.WingOuterHalfWidth + room.SideHalfWidth) * 0.5f),
            new Vector3(rearOuterLength + 0.20f, size.Y, wallThickness),
            shapeIndex + 16,
            "rear_outer_connector_left");
        var lintelHeight = size.Y - room.DoorHeight;
        var lintelCenter = frontCenter;
        lintelCenter.Y = bottom + room.DoorHeight + lintelHeight * 0.5f;
        var lintel = AddBox(
            body,
            source,
            basis,
            lintelCenter,
            new Vector3(room.DoorWidth, lintelHeight, wallThickness),
            shapeIndex + 17,
            "front_lintel");
        var doorwayCenter = roomFront;
        doorwayCenter.Y = bottom + Mathf.Min(1.2f, room.DoorHeight * 0.5f);
        lintel.SetMeta("gameplay_doorway_center", doorwayCenter);
        lintel.SetMeta("gameplay_doorway_forward", basis.Z);
        lintel.SetMeta("gameplay_doorway_width", room.DoorWidth);
        lintel.SetMeta("gameplay_doorway_height", room.DoorHeight);
        lintel.SetMeta("gameplay_shell_front_inset", room.FrontInset);
        lintel.SetMeta("gameplay_shell_width", size.X);
        lintel.SetMeta("gameplay_shell_depth", size.Z);
        lintel.SetMeta("gameplay_shell_height", size.Y);
        lintel.SetMeta("gameplay_facade_width", room.FacadeWidth);
        lintel.SetMeta("gameplay_wing_front_inset", room.WingFrontInset);
        lintel.SetMeta("gameplay_rear_wing_inset", room.RearWingInset);
        lintel.SetMeta("gameplay_wing_inner_half_width", room.WingInnerHalfWidth);
        lintel.SetMeta("gameplay_wing_outer_half_width", room.WingOuterHalfWidth);
        lintel.SetMeta("gameplay_side_half_width", room.SideHalfWidth);
        lintel.SetMeta("gameplay_side_front_inset", room.SideFrontInset);
        lintel.SetMeta("gameplay_side_rear_inset", room.SideRearInset);
        lintel.SetMeta("gameplay_interior_width", room.InteriorWidth);
        lintel.SetMeta("gameplay_interior_depth", room.InteriorDepth);
        AddBox(
            body,
            source,
            basis,
            sideCenter + Vector3.Up * (size.Y * 0.5f - roofThickness * 0.5f),
            new Vector3(room.SideHalfWidth * 2.0f, roofThickness, sideSpan),
            shapeIndex + 18,
            "ceiling");
        var interiorHeight = Mathf.Min(3.0f, size.Y - 0.05f);
        var interiorCenter = roomFront - basis.Z * (room.InteriorDepth * 0.5f);
        interiorCenter.Y = bottom + interiorHeight * 0.5f;
        AddBox(
            body,
            source,
            basis,
            interiorCenter - basis.X * (room.InteriorWidth * 0.5f),
            new Vector3(wallThickness, interiorHeight, room.InteriorDepth),
            shapeIndex + 19,
            "liner_left");
        AddBox(
            body,
            source,
            basis,
            interiorCenter + basis.X * (room.InteriorWidth * 0.5f),
            new Vector3(wallThickness, interiorHeight, room.InteriorDepth),
            shapeIndex + 20,
            "liner_right");
        var interiorBack = roomFront - basis.Z * room.InteriorDepth;
        interiorBack.Y = bottom + interiorHeight * 0.5f;
        AddBox(
            body,
            source,
            basis,
            interiorBack,
            new Vector3(room.InteriorWidth, interiorHeight, wallThickness),
            shapeIndex + 21,
            "liner_back");
        var interiorCeiling = interiorCenter;
        interiorCeiling.Y = bottom + interiorHeight;
        AddBox(
            body,
            source,
            basis,
            interiorCeiling,
            new Vector3(room.InteriorWidth, roofThickness, room.InteriorDepth),
            shapeIndex + 22,
            "liner_ceiling");
        return JianghaiGameplayCollisionContract.EnterableShapesPerSource;
    }

    private static CollisionShape3D AddBox(
        StaticBody3D body,
        MeshInstance3D source,
        Basis basis,
        Vector3 center,
        Vector3 size,
        int shapeIndex,
        string role)
    {
        var collision = new CollisionShape3D
        {
            Name = $"AuthoredProxy_{shapeIndex + 1:000}_{source.Name}_{role}",
            Shape = new BoxShape3D { Size = size },
            Transform = new Transform3D(basis, center)
        };
        collision.SetMeta("gameplay_source_node", source.Name.ToString());
        var densitySource = IsAuthoredDensityBuilding(source);
        collision.SetMeta(
            "gameplay_source_district_role",
            densitySource
                ? JianghaiGameplayCollisionContract.AuthoredDensityDistrictRole
                : source.GetMeta(
                    "district_role",
                    "authored_chinese_shop").AsString());
        collision.SetMeta(
            "gameplay_source_collision_role",
            JianghaiGameplayCollisionContract.AuthoredDensityCollisionRole);
        collision.SetMeta(
            "gameplay_source_kind",
            IsEnterableBuilding(source)
                ? "enterable"
                : densitySource
                    ? "density"
                    : IsAuthoredSolidBuilding(source) ? "solid" : "legacy");
        collision.SetMeta("gameplay_proxy_role", role);
        body.AddChild(collision);
        return collision;
    }

    private static bool IsAuthoredDensityBuilding(MeshInstance3D source)
    {
        var metadataReady = string.Equals(
                source.GetMeta("district_role", string.Empty).AsString(),
                JianghaiGameplayCollisionContract.AuthoredDensityDistrictRole,
                StringComparison.Ordinal)
            && string.Equals(
                source.GetMeta("collision_role", string.Empty).AsString(),
                JianghaiGameplayCollisionContract.AuthoredDensityCollisionRole,
                StringComparison.Ordinal);
        return metadataReady || JianghaiGameplayCollisionContract.IsExpectedDensitySource(
            source.Name.ToString());
    }

    private static bool IsEnterableBuilding(MeshInstance3D source)
        => source.GetMeta("jianghai_enterable", false).AsBool()
            || JianghaiGameplayCollisionContract.IsExpectedEnterableSource(
                source.Name.ToString());

    private static bool IsAuthoredSolidBuilding(MeshInstance3D source)
        => JianghaiGameplayCollisionContract.IsExpectedSolidSource(source.Name.ToString());
}
