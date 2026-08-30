using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

internal sealed partial class OldTownLandmarksBuilder
{
    private static void AddClanHallGameplayCollision(
        StaticBody3D collisionBody,
        Node3D? authoredRoot,
        BuildCounts counts)
    {
        if (!JianghaiClanHallGateContract.TryResolve(
                authoredRoot,
                out var gate,
                out var error))
        {
            throw new System.InvalidOperationException(
                $"Clan-hall collision requires authored portal contract: {error}.");
        }

        ProjectClanHallFootprint(
            gate,
            out var minimumTangent,
            out var maximumTangent,
            out var minimumDepth,
            out var maximumDepth);
        var gateHalfWidth = gate.Width * 0.5f;
        var leftWidth = -gateHalfWidth - minimumTangent;
        var rightWidth = maximumTangent - gateHalfWidth;
        var buildingDepth = maximumDepth - minimumDepth;
        if (leftWidth < 0.5f || rightWidth < 0.5f || buildingDepth < 4.0f)
        {
            throw new System.InvalidOperationException(
                "Clan-hall collision footprint cannot preserve the authored doorway "
                + $"(tangent={minimumTangent:0.000}..{maximumTangent:0.000}, "
                + $"depth={minimumDepth:0.000}..{maximumDepth:0.000}, "
                + $"gate={gate.Width:0.000}).");
        }

        const float wallThickness = 0.44f;
        var bottom = JianghaiClanHallGateContract.WorldMinimumY;
        var top = JianghaiClanHallGateContract.WorldMaximumY;
        var wallHeight = top - bottom;
        var wallCenterY = (bottom + top) * 0.5f;
        var lintelBottom = gate.Position.Y + gate.Height;
        if (lintelBottom >= top - 0.25f)
        {
            throw new System.InvalidOperationException(
                $"Clan-hall gate height {gate.Height:0.000} leaves no lintel collision.");
        }

        var yawDegrees = new Vector3(
            0.0f,
            Mathf.RadToDeg(Mathf.Atan2(gate.Inward.X, gate.Inward.Z)),
            0.0f);
        AddClanHallBox(
            collisionBody,
            "ClanHallFacadeLeft",
            gate,
            (minimumTangent - gateHalfWidth) * 0.5f,
            0.0f,
            wallCenterY,
            new Vector3(leftWidth, wallHeight, wallThickness),
            yawDegrees,
            "facade_left",
            counts);
        AddClanHallBox(
            collisionBody,
            "ClanHallFacadeRight",
            gate,
            (maximumTangent + gateHalfWidth) * 0.5f,
            0.0f,
            wallCenterY,
            new Vector3(rightWidth, wallHeight, wallThickness),
            yawDegrees,
            "facade_right",
            counts);
        AddClanHallBox(
            collisionBody,
            "ClanHallEntryLintel",
            gate,
            0.0f,
            0.0f,
            (lintelBottom + top) * 0.5f,
            new Vector3(gate.Width, top - lintelBottom, wallThickness),
            yawDegrees,
            "front_lintel",
            counts);
        AddClanHallBox(
            collisionBody,
            "ClanHallSideLeft",
            gate,
            minimumTangent + wallThickness * 0.5f,
            (minimumDepth + maximumDepth) * 0.5f,
            wallCenterY,
            new Vector3(wallThickness, wallHeight, buildingDepth),
            yawDegrees,
            "side_left",
            counts);
        AddClanHallBox(
            collisionBody,
            "ClanHallSideRight",
            gate,
            maximumTangent - wallThickness * 0.5f,
            (minimumDepth + maximumDepth) * 0.5f,
            wallCenterY,
            new Vector3(wallThickness, wallHeight, buildingDepth),
            yawDegrees,
            "side_right",
            counts);
        AddClanHallBox(
            collisionBody,
            "ClanHallRearWall",
            gate,
            (minimumTangent + maximumTangent) * 0.5f,
            maximumDepth - wallThickness * 0.5f,
            wallCenterY,
            new Vector3(
                maximumTangent - minimumTangent,
                wallHeight,
                wallThickness),
            yawDegrees,
            "back",
            counts);
        AddClanHallBox(
            collisionBody,
            "ClanHallThreshold",
            gate,
            0.0f,
            0.15f,
            (bottom + gate.Position.Y) * 0.5f,
            new Vector3(gate.Width, gate.Position.Y - bottom, 1.50f),
            yawDegrees,
            "threshold",
            counts);
        AddClanHallBox(
            collisionBody,
            "ClanHallInteriorFloor",
            gate,
            (minimumTangent + maximumTangent) * 0.5f,
            (0.45f + maximumDepth - wallThickness) * 0.5f,
            gate.Position.Y - 0.11f,
            new Vector3(
                maximumTangent - minimumTangent - wallThickness * 2.0f,
                0.22f,
                maximumDepth - wallThickness - 0.45f),
            yawDegrees,
            "floor",
            counts);
        AddClanHallEntryRamp(collisionBody, gate, counts);
    }

    private static void AddClanHallEntryRamp(
        StaticBody3D collisionBody,
        JianghaiClanHallGateGeometry gate,
        BuildCounts counts)
    {
        const float thickness = 0.24f;
        var rise = gate.Position.Y - JianghaiClanHallGateContract.EntryRampStreetY;
        var slope = Mathf.Atan2(rise, JianghaiClanHallGateContract.EntryRampRun);
        var rampForward = gate.Outward * Mathf.Cos(slope) - Vector3.Up * Mathf.Sin(slope);
        var rampNormal = Vector3.Up * Mathf.Cos(slope) - gate.Inward * Mathf.Sin(slope);
        var basis = new Basis(gate.Tangent, rampNormal, rampForward).Orthonormalized();
        var center = gate.Position
            + gate.Outward * (
                JianghaiClanHallGateContract.EntryRampRun * 0.5f
                + JianghaiClanHallGateContract.EntryRampHighInset)
            + Vector3.Down * (rise * 0.5f + thickness * 0.5f);
        var collision = new CollisionShape3D
        {
            Name = "ClanHallEntryRamp",
            Transform = new Transform3D(basis, center),
            Shape = new BoxShape3D
            {
                Size = new Vector3(
                    gate.Width + 1.0f,
                    thickness,
                    JianghaiClanHallGateContract.EntryRampRun)
            }
        };
        collision.SetMeta("gameplay_source_node", JianghaiClanHallGateContract.SourceName);
        collision.SetMeta("gameplay_source_kind", "landmark_portal");
        collision.SetMeta("gameplay_source_collision_role", "building_shell");
        collision.SetMeta("gameplay_proxy_role", "entry_ramp");
        collisionBody.AddChild(collision);
        counts.CollisionShapes++;
    }

    private static void AddClanHallBox(
        StaticBody3D collisionBody,
        string name,
        JianghaiClanHallGateGeometry gate,
        float tangentOffset,
        float inwardOffset,
        float worldY,
        Vector3 size,
        Vector3 rotationDegrees,
        string proxyRole,
        BuildCounts counts)
    {
        var center = gate.Position
            + gate.Tangent * tangentOffset
            + gate.Inward * inwardOffset;
        center.Y = worldY;
        AddCollision(
            collisionBody,
            name,
            center,
            size,
            rotationDegrees,
            counts,
            JianghaiClanHallGateContract.SourceName,
            proxyRole);
    }

    private static void ProjectClanHallFootprint(
        JianghaiClanHallGateGeometry gate,
        out float minimumTangent,
        out float maximumTangent,
        out float minimumDepth,
        out float maximumDepth)
    {
        minimumTangent = float.PositiveInfinity;
        maximumTangent = float.NegativeInfinity;
        minimumDepth = float.PositiveInfinity;
        maximumDepth = float.NegativeInfinity;
        foreach (var x in new[]
        {
            JianghaiClanHallGateContract.WorldMinimumX,
            JianghaiClanHallGateContract.WorldMaximumX
        })
        {
            foreach (var z in new[]
            {
                JianghaiClanHallGateContract.WorldMinimumZ,
                JianghaiClanHallGateContract.WorldMaximumZ
            })
            {
                var delta = new Vector3(x, gate.Position.Y, z) - gate.Position;
                var tangent = delta.Dot(gate.Tangent);
                var depth = delta.Dot(gate.Inward);
                minimumTangent = Mathf.Min(minimumTangent, tangent);
                maximumTangent = Mathf.Max(maximumTangent, tangent);
                minimumDepth = Mathf.Min(minimumDepth, depth);
                maximumDepth = Mathf.Max(maximumDepth, depth);
            }
        }
    }

    private static void AddPawnshopGameplayCollision(
        StaticBody3D collisionBody,
        BuildCounts counts)
    {
        const float wallHeight = 2.2f;
        AddCollision(
            collisionBody,
            "PawnshopNorthWall",
            HotelCenter + new Vector3(0, wallHeight * 0.5f, -12.0f),
            new Vector3(24.5f, wallHeight, 0.5f),
            Vector3.Zero,
            counts);
        AddCollision(
            collisionBody,
            "PawnshopWestWall",
            HotelCenter + new Vector3(-12.0f, wallHeight * 0.5f, 0),
            new Vector3(0.5f, wallHeight, 24.5f),
            Vector3.Zero,
            counts);
        AddCollision(
            collisionBody,
            "PawnshopWestFacadePanel",
            new Vector3(-98.0f, wallHeight * 0.5f, -122.5f),
            new Vector3(0.6f, wallHeight, 0.3f),
            Vector3.Zero,
            counts);
        AddCollision(
            collisionBody,
            "PawnshopEastWall",
            HotelCenter + new Vector3(12.0f, wallHeight * 0.5f, 0),
            new Vector3(0.5f, wallHeight, 24.5f),
            Vector3.Zero,
            counts);
        AddCollision(
            collisionBody,
            "PawnshopEntryLeft",
            HotelCenter + new Vector3(-6.5f, wallHeight * 0.5f, 12.0f),
            new Vector3(11.0f, wallHeight, 1.0f),
            Vector3.Zero,
            counts);
        AddCollision(
            collisionBody,
            "PawnshopEntryRight",
            HotelCenter + new Vector3(6.5f, wallHeight * 0.5f, 12.0f),
            new Vector3(11.0f, wallHeight, 1.0f),
            Vector3.Zero,
            counts);
    }

    private static void AddFactoryGateGameplayCollision(
        StaticBody3D collisionBody,
        BuildCounts counts)
    {
        const float gateZ = -7.924f;
        AddCollision(
            collisionBody,
            "FactoryGateLeft",
            new Vector3(82.4f, 2.0f, gateZ),
            new Vector3(1.0f, 4.0f, 0.5f),
            Vector3.Zero,
            counts);
        AddCollision(
            collisionBody,
            "FactoryGateLeftFacade",
            new Vector3(84.1f, 2.0f, gateZ),
            new Vector3(2.2f, 4.0f, 0.5f),
            Vector3.Zero,
            counts);
        AddCollision(
            collisionBody,
            "FactoryGateRight",
            new Vector3(89.8f, 2.0f, gateZ),
            new Vector3(1.0f, 4.0f, 0.5f),
            Vector3.Zero,
            counts);
        AddCollision(
            collisionBody,
            "FactoryGateRightFacade",
            new Vector3(87.9f, 2.0f, gateZ),
            new Vector3(2.2f, 4.0f, 0.5f),
            Vector3.Zero,
            counts);
        AddCollision(
            collisionBody,
            "FactoryGateLintel",
            new Vector3(86.0f, 4.5f, gateZ),
            new Vector3(7.0f, 1.0f, 0.5f),
            Vector3.Zero,
            counts);
    }

    private static IReadOnlyList<Vector3> AddMarketGameplayCollision(
        StaticBody3D collisionBody,
        BuildCounts counts)
    {
        AddCollision(
            collisionBody,
            "MarketDeck",
            new Vector3(0, 4.14f, RooftopZ),
            new Vector3(45.0f, 0.34f, 4.4f),
            Vector3.Zero,
            counts);
        AddCollision(
            collisionBody,
            "MarketWestRamp",
            new Vector3(-29.0f, 2.05f, RooftopZ),
            new Vector3(12.5f, 0.38f, 4.0f),
            new Vector3(0, 0, 18.7f),
            counts);
        AddCollision(
            collisionBody,
            "MarketEastRamp",
            new Vector3(29.0f, 2.05f, RooftopZ),
            new Vector3(12.5f, 0.38f, 4.0f),
            new Vector3(0, 0, -18.7f),
            counts);
        foreach (var z in new[] { RooftopZ - 2.15f, RooftopZ + 2.15f })
        {
            foreach (var y in new[] { 4.845f, 5.445f })
            {
                AddCollision(
                    collisionBody,
                    $"MarketRail_{z:0.00}_{y:0.000}",
                    new Vector3(0, y, z),
                    new Vector3(45.0f, 0.18f, 0.3f),
                    Vector3.Zero,
                    counts);
            }
            AddCollision(
                collisionBody,
                $"MarketRailPost_{z:0.00}",
                new Vector3(0, 5.15f, z),
                new Vector3(0.3f, 1.2f, 0.3f),
                Vector3.Zero,
                counts);
        }
        return MarketRooftopRoute();
    }
}
