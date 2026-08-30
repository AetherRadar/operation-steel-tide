using Godot;
using System.Collections.Generic;

namespace OperationSteelTide;

internal readonly record struct JianghaiEnterableRoomGeometry(
    Vector3 Center,
    Vector3 Size,
    float FrontInset,
    float FacadeWidth,
    float WingFrontInset,
    float RearWingInset,
    float WingInnerHalfWidth,
    float WingOuterHalfWidth,
    float SideHalfWidth,
    float SideFrontInset,
    float SideRearInset,
    float InteriorWidth,
    float InteriorDepth,
    float DoorWidth,
    float DoorHeight);

internal readonly record struct JianghaiCollisionFootprint(
    Vector3 Center,
    Basis Basis,
    Vector3 Size);

internal readonly record struct JianghaiPlacementCollisionFragment(
    Vector3 Center,
    Vector3 Size);

internal readonly record struct JianghaiSolidBuildingGeometry(
    Vector3 Center,
    Vector3 Size);

/// <summary>Resolves cheap oriented room shells and legacy-proxy overlap.</summary>
internal static class JianghaiGameplayCollisionGeometry
{
    private const float CarvePadding = 0.16f;
    private const float MinimumFragmentSpan = 0.16f;

    public static JianghaiEnterableRoomGeometry ResolveEnterableRoom(
        MeshInstance3D source,
        Basis basis,
        Vector3 visualCenter,
        Vector3 visualSize)
    {
        var availableWidth = Mathf.Max(2.4f, visualSize.X);
        var availableDepth = Mathf.Max(2.8f, visualSize.Z);
        var hasContract = JianghaiGameplayCollisionContract.TryGetEnterableRoom(
            source.Name.ToString(),
            out var contract);
        var doorWidth = Mathf.Clamp(
            source.GetMeta(
                "jianghai_door_width_m",
                hasContract ? contract.DoorWidth : 1.58f).AsSingle(),
            1.2f,
            availableWidth - 0.8f);
        var doorHeight = Mathf.Clamp(
            source.GetMeta(
                "jianghai_door_height_m",
                hasContract ? contract.DoorHeight : 2.48f).AsSingle(),
            2.1f,
            Mathf.Max(2.1f, visualSize.Y - 0.35f));
        var frontInset = Mathf.Clamp(
            source.GetMeta(
                "jianghai_door_front_inset_m",
                hasContract ? contract.FrontInset : 0.0f).AsSingle(),
            0.0f,
            Mathf.Max(0.0f, availableDepth - 2.8f));
        var roomWidth = Mathf.Clamp(
            source.GetMeta(
                "jianghai_collision_width_m",
                hasContract ? contract.CollisionWidth : availableWidth).AsSingle(),
            doorWidth + 0.8f,
            availableWidth);
        var roomDepth = Mathf.Clamp(
            source.GetMeta(
                "jianghai_collision_depth_m",
                hasContract ? contract.CollisionDepth : availableDepth - frontInset).AsSingle(),
            2.8f,
            availableDepth - frontInset);
        var roomHeight = Mathf.Clamp(
            source.GetMeta(
                "jianghai_collision_height_m",
                hasContract ? contract.CollisionHeight : 3.05f).AsSingle(),
            doorHeight + 0.3f,
            visualSize.Y);
        var interiorWidth = Mathf.Clamp(
            source.GetMeta(
                "jianghai_room_width_m",
                hasContract ? contract.InteriorWidth : roomWidth - 0.8f).AsSingle(),
            doorWidth + 0.4f,
            Mathf.Max(doorWidth + 0.4f, roomWidth - 0.35f));
        var interiorDepth = Mathf.Clamp(
            source.GetMeta(
                "jianghai_room_depth_m",
                hasContract ? contract.InteriorDepth : roomDepth - 0.5f).AsSingle(),
            2.8f,
            Mathf.Max(2.8f, roomDepth - 0.30f));
        var facadeWidth = Mathf.Clamp(
            source.GetMeta(
                "jianghai_collision_facade_width_m",
                hasContract ? contract.FacadeWidth : roomWidth).AsSingle(),
            doorWidth + 0.4f,
            roomWidth);
        var wingFrontInset = Mathf.Clamp(
            source.GetMeta(
                "jianghai_collision_wing_front_inset_m",
                hasContract ? contract.WingFrontInset : frontInset + 0.8f).AsSingle(),
            frontInset + 0.35f,
            frontInset + roomDepth - 0.35f);
        var wingInnerHalfWidth = Mathf.Clamp(
            source.GetMeta(
                "jianghai_collision_wing_inner_half_width_m",
                hasContract
                    ? contract.WingInnerHalfWidth
                    : facadeWidth * 0.5f + 0.4f).AsSingle(),
            facadeWidth * 0.5f + 0.2f,
            roomWidth * 0.5f - 0.3f);
        var rearWingInset = Mathf.Clamp(
            source.GetMeta(
                "jianghai_collision_rear_wing_inset_m",
                hasContract
                    ? contract.RearWingInset
                    : frontInset + roomDepth - 0.8f).AsSingle(),
            wingFrontInset + 0.8f,
            frontInset + roomDepth - 0.25f);
        var wingOuterHalfWidth = Mathf.Clamp(
            source.GetMeta(
                "jianghai_collision_wing_outer_half_width_m",
                hasContract ? contract.WingOuterHalfWidth : roomWidth * 0.5f).AsSingle(),
            wingInnerHalfWidth + 0.2f,
            roomWidth * 0.5f);
        var sideHalfWidth = Mathf.Clamp(
            source.GetMeta(
                "jianghai_collision_side_half_width_m",
                hasContract ? contract.SideHalfWidth : roomWidth * 0.5f).AsSingle(),
            wingOuterHalfWidth + 0.3f,
            availableWidth * 0.5f);
        var sideFrontInset = Mathf.Clamp(
            source.GetMeta(
                "jianghai_collision_side_front_inset_m",
                hasContract
                    ? contract.SideFrontInset
                    : wingFrontInset + 0.6f).AsSingle(),
            wingFrontInset + 0.2f,
            rearWingInset - 1.0f);
        var sideRearInset = Mathf.Clamp(
            source.GetMeta(
                "jianghai_collision_side_rear_inset_m",
                hasContract
                    ? contract.SideRearInset
                    : rearWingInset - 0.6f).AsSingle(),
            sideFrontInset + 0.8f,
            rearWingInset - 0.2f);
        var bottom = visualCenter.Y - visualSize.Y * 0.5f;
        var visualFront = visualCenter + basis.Z * (visualSize.Z * 0.5f);
        var front = visualFront - basis.Z * frontInset;
        var roomCenter = front - basis.Z * (roomDepth * 0.5f);
        roomCenter.Y = bottom + roomHeight * 0.5f;
        // Keep the later interior population pass on the exact same authored-to-physics
        // contract, including when Godot omits Blender custom properties on GLB import.
        source.SetMeta("jianghai_room_width_m", interiorWidth);
        source.SetMeta("jianghai_room_depth_m", interiorDepth);
        return new JianghaiEnterableRoomGeometry(
            roomCenter,
            new Vector3(roomWidth, roomHeight, roomDepth),
            frontInset,
            facadeWidth,
            wingFrontInset,
            rearWingInset,
            wingInnerHalfWidth,
            wingOuterHalfWidth,
            sideHalfWidth,
            sideFrontInset,
            sideRearInset,
            interiorWidth,
            interiorDepth,
            doorWidth,
            Mathf.Min(doorHeight, roomHeight - 0.3f));
    }

    public static bool OverlapsFootprint(
        Vector3 firstCenter,
        Basis firstBasis,
        Vector3 firstSize,
        Vector3 secondCenter,
        Basis secondBasis,
        Vector3 secondSize)
    {
        if (firstCenter.Y + firstSize.Y * 0.5f <= secondCenter.Y - secondSize.Y * 0.5f
            || secondCenter.Y + secondSize.Y * 0.5f <= firstCenter.Y - firstSize.Y * 0.5f)
        {
            return false;
        }

        var firstX = new Vector2(firstBasis.X.X, firstBasis.X.Z).Normalized();
        var firstZ = new Vector2(firstBasis.Z.X, firstBasis.Z.Z).Normalized();
        var secondX = new Vector2(secondBasis.X.X, secondBasis.X.Z).Normalized();
        var secondZ = new Vector2(secondBasis.Z.X, secondBasis.Z.Z).Normalized();
        var delta = new Vector2(
            secondCenter.X - firstCenter.X,
            secondCenter.Z - firstCenter.Z);
        foreach (var axis in new[] { firstX, firstZ, secondX, secondZ })
        {
            var firstRadius = firstSize.X * 0.5f * Mathf.Abs(firstX.Dot(axis))
                + firstSize.Z * 0.5f * Mathf.Abs(firstZ.Dot(axis));
            var secondRadius = secondSize.X * 0.5f * Mathf.Abs(secondX.Dot(axis))
                + secondSize.Z * 0.5f * Mathf.Abs(secondZ.Dot(axis));
            if (firstRadius + secondRadius - Mathf.Abs(delta.Dot(axis)) <= 0.35f)
            {
                return false;
            }
        }
        return true;
    }

    public static IReadOnlyList<JianghaiPlacementCollisionFragment> CarvePlacementProxy(
        Vector3 center,
        Basis basis,
        Vector3 size,
        IReadOnlyList<JianghaiCollisionFootprint> enterableFootprints,
        out bool carved)
    {
        var fragments = new List<LocalRectangle>
        {
            new(-size.X * 0.5f, size.X * 0.5f, -size.Z * 0.5f, size.Z * 0.5f)
        };
        carved = false;
        foreach (var enterable in enterableFootprints)
        {
            if (center.Y + size.Y * 0.5f
                    <= enterable.Center.Y - enterable.Size.Y * 0.5f
                || enterable.Center.Y + enterable.Size.Y * 0.5f
                    <= center.Y - size.Y * 0.5f)
            {
                continue;
            }

            var cut = ProjectFootprintToLocalRectangle(
                center,
                basis,
                enterable);
            cut = new LocalRectangle(
                Mathf.Max(-size.X * 0.5f, cut.MinimumX - CarvePadding),
                Mathf.Min(size.X * 0.5f, cut.MaximumX + CarvePadding),
                Mathf.Max(-size.Z * 0.5f, cut.MinimumZ - CarvePadding),
                Mathf.Min(size.Z * 0.5f, cut.MaximumZ + CarvePadding));
            if (!cut.IsUsable)
            {
                continue;
            }

            var nextFragments = new List<LocalRectangle>(fragments.Count + 3);
            foreach (var fragment in fragments)
            {
                if (!TryIntersect(fragment, cut, out var overlap))
                {
                    nextFragments.Add(fragment);
                    continue;
                }

                carved = true;
                AddIfUsable(nextFragments, new LocalRectangle(
                    fragment.MinimumX,
                    overlap.MinimumX,
                    fragment.MinimumZ,
                    fragment.MaximumZ));
                AddIfUsable(nextFragments, new LocalRectangle(
                    overlap.MaximumX,
                    fragment.MaximumX,
                    fragment.MinimumZ,
                    fragment.MaximumZ));
                AddIfUsable(nextFragments, new LocalRectangle(
                    overlap.MinimumX,
                    overlap.MaximumX,
                    fragment.MinimumZ,
                    overlap.MinimumZ));
                AddIfUsable(nextFragments, new LocalRectangle(
                    overlap.MinimumX,
                    overlap.MaximumX,
                    overlap.MaximumZ,
                    fragment.MaximumZ));
            }
            fragments = nextFragments;
        }

        var result = new List<JianghaiPlacementCollisionFragment>(fragments.Count);
        foreach (var fragment in fragments)
        {
            var localCenter = new Vector3(
                (fragment.MinimumX + fragment.MaximumX) * 0.5f,
                0.0f,
                (fragment.MinimumZ + fragment.MaximumZ) * 0.5f);
            result.Add(new JianghaiPlacementCollisionFragment(
                center + basis * localCenter,
                new Vector3(fragment.Width, size.Y, fragment.Depth)));
        }
        return result;
    }

    public static JianghaiSolidBuildingGeometry ResolveSolidBuilding(
        MeshInstance3D source,
        Basis basis,
        Vector3 visualCenter,
        Vector3 visualSize)
    {
        var profile = JianghaiGameplayCollisionContract.SolidProfileFor(
            source.Name.ToString());
        var worldScale = source.GlobalBasis.Scale.Abs();
        var (widthRatio, depthRatio, localCenterZ) = profile switch
        {
            JianghaiSolidBuildingProfile.Hall => (0.735f, 0.706f, -0.250f),
            JianghaiSolidBuildingProfile.Shop => (0.776f, 0.523f, 0.0f),
            JianghaiSolidBuildingProfile.Gate => (0.773f, 0.688f, -0.425f),
            _ => throw new System.ArgumentOutOfRangeException(nameof(profile), profile, null)
        };
        return new JianghaiSolidBuildingGeometry(
            visualCenter + basis.Z * (localCenterZ * worldScale.Z),
            new Vector3(
                visualSize.X * widthRatio,
                visualSize.Y,
                visualSize.Z * depthRatio));
    }

    private static LocalRectangle ProjectFootprintToLocalRectangle(
        Vector3 placementCenter,
        Basis placementBasis,
        JianghaiCollisionFootprint footprint)
    {
        var minimumX = float.PositiveInfinity;
        var maximumX = float.NegativeInfinity;
        var minimumZ = float.PositiveInfinity;
        var maximumZ = float.NegativeInfinity;
        foreach (var xSign in new[] { -1.0f, 1.0f })
        {
            foreach (var zSign in new[] { -1.0f, 1.0f })
            {
                var corner = footprint.Center
                    + footprint.Basis.X * (footprint.Size.X * 0.5f * xSign)
                    + footprint.Basis.Z * (footprint.Size.Z * 0.5f * zSign);
                var delta = corner - placementCenter;
                var localX = delta.Dot(placementBasis.X);
                var localZ = delta.Dot(placementBasis.Z);
                minimumX = Mathf.Min(minimumX, localX);
                maximumX = Mathf.Max(maximumX, localX);
                minimumZ = Mathf.Min(minimumZ, localZ);
                maximumZ = Mathf.Max(maximumZ, localZ);
            }
        }
        return new LocalRectangle(minimumX, maximumX, minimumZ, maximumZ);
    }

    private static bool TryIntersect(
        LocalRectangle first,
        LocalRectangle second,
        out LocalRectangle overlap)
    {
        overlap = new LocalRectangle(
            Mathf.Max(first.MinimumX, second.MinimumX),
            Mathf.Min(first.MaximumX, second.MaximumX),
            Mathf.Max(first.MinimumZ, second.MinimumZ),
            Mathf.Min(first.MaximumZ, second.MaximumZ));
        return overlap.IsUsable;
    }

    private static void AddIfUsable(
        ICollection<LocalRectangle> rectangles,
        LocalRectangle candidate)
    {
        if (candidate.IsUsable)
        {
            rectangles.Add(candidate);
        }
    }

    private readonly record struct LocalRectangle(
        float MinimumX,
        float MaximumX,
        float MinimumZ,
        float MaximumZ)
    {
        public float Width => MaximumX - MinimumX;

        public float Depth => MaximumZ - MinimumZ;

        public bool IsUsable => Width >= MinimumFragmentSpan
            && Depth >= MinimumFragmentSpan;
    }
}
