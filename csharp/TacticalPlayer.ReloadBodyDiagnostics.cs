using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public partial class TacticalPlayer
{
    private static readonly string[] ReloadFullArmSkinBones =
    {
        "R_arm_024", "R_elbow_025", "R_wrist_026", "R_palm_039",
        "L_arm_01", "L_elbow_02", "L_wrist_03", "L_palm_015"
    };
    private static readonly string[] ReloadForearmSkinBones =
    {
        "R_wrist_026", "R_palm_039",
        "L_wrist_03", "L_palm_015"
    };

    private float ReloadNearPlaneZ
        => -(_camera.Near + Mathf.Max(0.0001f, _camera.Near * 0.01f));

    private ReloadBodyContinuityInspection InspectReloadBodyContinuity(
        Skeleton3D? skeleton,
        bool rightPalmAvailable,
        Vector3 rightPalmContactGlobal,
        bool leftPalmAvailable,
        Vector3 leftPalmContactGlobal,
        Node3D? animatedMesh)
    {
        if (!IsInstanceValid(_camera))
        {
            return default;
        }

        var logicalViewportSize = _camera.GetViewport().GetVisibleRect().Size;
        var windowSize = GetWindow().Size;
        var screenSize = new Vector2(windowSize.X, windowSize.Y);
        if (logicalViewportSize.X <= 0.0f
            || logicalViewportSize.Y <= 0.0f
            || screenSize.X <= 0.0f
            || screenSize.Y <= 0.0f)
        {
            return default;
        }

        var rightArm = InspectReloadArmScreenChain(
            skeleton,
            "R_arm_024",
            "R_elbow_025",
            "R_wrist_026",
            "R_palm_039",
            rightPalmAvailable,
            rightPalmContactGlobal,
            logicalViewportSize,
            screenSize);
        var leftArm = InspectReloadArmScreenChain(
            skeleton,
            "L_arm_01",
            "L_elbow_02",
            "L_wrist_03",
            "L_palm_015",
            leftPalmAvailable,
            leftPalmContactGlobal,
            logicalViewportSize,
            screenSize);
        return new ReloadBodyContinuityInspection(
            screenSize,
            rightArm.ShoulderScreen,
            leftArm.ShoulderScreen,
            rightArm.ShoulderBehindCamera,
            leftArm.ShoulderBehindCamera,
            InspectVisibleMeshScreenProjection(
                animatedMesh,
                logicalViewportSize,
                screenSize),
            ReloadMeshUsesSkeleton(animatedMesh, skeleton),
            ReloadMeshUsesForearmSkeleton(animatedMesh, skeleton),
            rightArm,
            leftArm);
    }

    private ReloadArmScreenChainInspection InspectReloadArmScreenChain(
        Skeleton3D? skeleton,
        string shoulderBoneName,
        string elbowBoneName,
        string wristBoneName,
        string palmBoneName,
        bool palmAvailable,
        Vector3 palmContactGlobal,
        Vector2 logicalViewportSize,
        Vector2 screenSize)
    {
        if (!IsInstanceValid(skeleton) || !palmAvailable)
        {
            return default;
        }

        var shoulderBone = skeleton!.FindBone(shoulderBoneName);
        var elbowBone = skeleton.FindBone(elbowBoneName);
        var wristBone = skeleton.FindBone(wristBoneName);
        var palmBone = skeleton.FindBone(palmBoneName);
        if (shoulderBone < 0 || elbowBone < 0 || wristBone < 0 || palmBone < 0)
        {
            return default;
        }
        var shoulderPose = skeleton.GetBoneGlobalPose(shoulderBone);
        var elbowPose = skeleton.GetBoneGlobalPose(elbowBone);
        var wristPose = skeleton.GetBoneGlobalPose(wristBone);
        var palmPose = skeleton.GetBoneGlobalPose(palmBone);
        var shoulderRest = skeleton.GetBoneGlobalRest(shoulderBone);
        var elbowRest = skeleton.GetBoneGlobalRest(elbowBone);
        var wristRest = skeleton.GetBoneGlobalRest(wristBone);
        var palmRest = skeleton.GetBoneGlobalRest(palmBone);
        var shoulderGlobal = skeleton.GlobalTransform * shoulderPose.Origin;
        var elbowGlobal = skeleton.GlobalTransform * elbowPose.Origin;
        var wristGlobal = skeleton.GlobalTransform * wristPose.Origin;
        var palmBoneGlobal = skeleton.GlobalTransform * palmPose.Origin;

        var screenScale = new Vector2(
            screenSize.X / logicalViewportSize.X,
            screenSize.Y / logicalViewportSize.Y);
        var shoulderScreen = ProjectReloadJoint(
            shoulderGlobal,
            screenScale,
            out var shoulderBehind);
        var elbowScreen = ProjectReloadJoint(
            elbowGlobal,
            screenScale,
            out var elbowBehind);
        var wristScreen = ProjectReloadJoint(
            wristGlobal,
            screenScale,
            out var wristBehind);
        var palmScreen = ProjectReloadJoint(
            palmBoneGlobal,
            screenScale,
            out var palmBehind);
        var bodyEdgeConnected = ReloadChainTouchesBodyEdge(
            shoulderGlobal,
            elbowGlobal,
            wristGlobal,
            palmBoneGlobal,
            palmContactGlobal,
            screenScale,
            screenSize,
            out var bodyEdgeScreen);
        return new ReloadArmScreenChainInspection(
            true,
            shoulderScreen,
            elbowScreen,
            wristScreen,
            palmScreen,
            shoulderBehind,
            elbowBehind,
            wristBehind,
            palmBehind,
            shoulderPose.Origin.DistanceTo(elbowPose.Origin),
            elbowPose.Origin.DistanceTo(wristPose.Origin),
            wristPose.Origin.DistanceTo(palmPose.Origin),
            shoulderRest.Origin.DistanceTo(elbowRest.Origin),
            elbowRest.Origin.DistanceTo(wristRest.Origin),
            wristRest.Origin.DistanceTo(palmRest.Origin),
            skeleton.GetBoneParent(elbowBone) == shoulderBone
                && skeleton.GetBoneParent(wristBone) == elbowBone
                && skeleton.GetBoneParent(palmBone) == wristBone,
            bodyEdgeConnected,
            bodyEdgeScreen);
    }

    private Vector2 ProjectReloadJoint(
        Vector3 worldPoint,
        Vector2 screenScale,
        out bool behindCamera)
    {
        var cameraPoint = _camera.GlobalTransform.AffineInverse() * worldPoint;
        behindCamera = cameraPoint.Z > ReloadNearPlaneZ;
        return behindCamera
            ? Vector2.Zero
            : _camera.UnprojectPosition(worldPoint) * screenScale;
    }

    private bool ReloadChainTouchesBodyEdge(
        Vector3 shoulderGlobal,
        Vector3 elbowGlobal,
        Vector3 wristGlobal,
        Vector3 palmBoneGlobal,
        Vector3 palmContactGlobal,
        Vector2 screenScale,
        Vector2 screenSize,
        out Vector2 bodyEdgeScreen)
    {
        bodyEdgeScreen = Vector2.Zero;
        foreach (var segment in new[]
                 {
                     (Start: shoulderGlobal, End: elbowGlobal),
                     (Start: elbowGlobal, End: wristGlobal),
                     // A close first-person pose can place the complete upper
                     // arm and most of the forearm below the viewport while
                     // the cuff crosses the bottom edge between wrist and
                     // palm. This remains a real body connection: the same
                     // parented chain is length-checked below and the visible
                     // mesh must carry positive weights for every arm bone.
                     // Including the distal bridge avoids misclassifying a
                     // continuous sleeve as a detached hand without restoring
                     // the old "shoulder behind camera" shortcut.
                     (Start: wristGlobal, End: palmBoneGlobal),
                     // The contact marker is authored on the palm surface,
                     // while the imported palm bone can sit well inside the
                     // hand volume. During a close M4 exchange the bone centres
                     // can all be below frame even though the skinned palm and
                     // cuff visibly bridge back through the lower edge. Test
                     // that final bone-to-contact segment so the continuity
                     // gate follows the rendered hand instead of treating the
                     // internal bone origin as the end of the body.
                     (Start: palmBoneGlobal, End: palmContactGlobal)
                 })
        {
            if (!TryProjectClippedReloadSegment(
                    segment.Start,
                    segment.End,
                    screenScale,
                    out var start,
                    out var end))
            {
                continue;
            }
            if (ReloadSegmentTouchesBodyEdge(
                    start,
                    end,
                    screenSize,
                    out bodyEdgeScreen))
            {
                return true;
            }
        }

        // Some authored poses keep the proximal shoulder fully visible. In
        // that case an arm does not need to cross the viewport edge at all:
        // the visible shoulder, parented chain, preserved bone lengths, and
        // weighted sleeve mesh are stronger evidence of continuity than an
        // artificial edge requirement. This is especially important for the
        // support arm while it is exchanging a magazine in the centre of the
        // view.
        var shoulderCamera = _camera.GlobalTransform.AffineInverse()
            * shoulderGlobal;
        if (shoulderCamera.Z <= ReloadNearPlaneZ)
        {
            var shoulder = _camera.UnprojectPosition(shoulderGlobal)
                * screenScale;
            var marginX = screenSize.X * 0.08f;
            var marginY = screenSize.Y * 0.08f;
            if (shoulder.X >= -marginX
                && shoulder.X <= screenSize.X + marginX
                && shoulder.Y >= -marginY
                && shoulder.Y <= screenSize.Y + marginY)
            {
                bodyEdgeScreen = shoulder;
                return true;
            }
        }

        return false;
    }

    private bool TryProjectClippedReloadSegment(
        Vector3 worldStart,
        Vector3 worldEnd,
        Vector2 screenScale,
        out Vector2 projectedStart,
        out Vector2 projectedEnd)
    {
        var cameraInverse = _camera.GlobalTransform.AffineInverse();
        var start = cameraInverse * worldStart;
        var end = cameraInverse * worldEnd;
        var nearZ = ReloadNearPlaneZ;
        var startVisible = start.Z <= nearZ;
        var endVisible = end.Z <= nearZ;
        if (!startVisible && !endVisible)
        {
            projectedStart = Vector2.Zero;
            projectedEnd = Vector2.Zero;
            return false;
        }

        if (startVisible != endVisible)
        {
            var denominator = end.Z - start.Z;
            if (Mathf.Abs(denominator) <= 0.000001f)
            {
                projectedStart = Vector2.Zero;
                projectedEnd = Vector2.Zero;
                return false;
            }
            var interpolation = Mathf.Clamp(
                (nearZ - start.Z) / denominator,
                0.0f,
                1.0f);
            var clipped = start.Lerp(end, interpolation);
            if (!startVisible)
            {
                start = clipped;
            }
            else
            {
                end = clipped;
            }
        }

        projectedStart = _camera.UnprojectPosition(
            _camera.GlobalTransform * start) * screenScale;
        projectedEnd = _camera.UnprojectPosition(
            _camera.GlobalTransform * end) * screenScale;
        return float.IsFinite(projectedStart.X)
            && float.IsFinite(projectedStart.Y)
            && float.IsFinite(projectedEnd.X)
            && float.IsFinite(projectedEnd.Y);
    }

    private static bool ReloadSegmentTouchesBodyEdge(
        Vector2 start,
        Vector2 end,
        Vector2 screenSize,
        out Vector2 bodyEdgeScreen)
    {
        var startInside = start.X >= 0.0f
            && start.X <= screenSize.X
            && start.Y >= 0.0f
            && start.Y <= screenSize.Y;
        // The shoulder joint sits inside the upper sleeve rather than at its
        // camera-facing hem. A joint in the lower 15% of the frame therefore
        // still has visibly skinned cloth continuing through the body edge.
        var startNearBottom = startInside
            && screenSize.Y - start.Y <= screenSize.Y * 0.15f;
        var startNearSide = startInside
            && start.Y >= screenSize.Y * 0.18f
            && Mathf.Min(start.X, screenSize.X - start.X)
                <= screenSize.X * 0.04f;
        // A palm-surface contact can sit just below the frame while the hand
        // volume around it remains visible and connected to the bottom edge.
        // Keep the same bounded 16% tolerance used by the expanded-frame
        // diagnostics; the parent chain, rest lengths, and skin weights still
        // have to prove that this is a real arm rather than a floating prop.
        var endJustBelowBottom = end.X >= 0.0f
            && end.X <= screenSize.X
            && end.Y >= screenSize.Y
            && end.Y <= screenSize.Y * 1.16f;
        if (startNearBottom || startNearSide || endJustBelowBottom)
        {
            bodyEdgeScreen = endJustBelowBottom
                ? new Vector2(end.X, screenSize.Y)
                : start;
            return true;
        }

        if (TrySegmentEdgeIntersection(
                start,
                end,
                horizontal: true,
                screenSize.Y,
                screenSize,
                out bodyEdgeScreen))
        {
            return true;
        }
        if (TrySegmentEdgeIntersection(
                start,
                end,
                horizontal: false,
                0.0f,
                screenSize,
                out bodyEdgeScreen)
            || TrySegmentEdgeIntersection(
                start,
                end,
                horizontal: false,
                screenSize.X,
                screenSize,
                out bodyEdgeScreen))
        {
            // A raised reload workspace legitimately sends the dominant arm
            // through the right edge above mid-screen. The chain and skin
            // gates still reject a floating hand; this threshold only accepts
            // the actual off-screen continuation of that full arm.
            return bodyEdgeScreen.Y >= screenSize.Y * 0.18f;
        }

        bodyEdgeScreen = Vector2.Zero;
        return false;
    }

    private static bool TrySegmentEdgeIntersection(
        Vector2 start,
        Vector2 end,
        bool horizontal,
        float edge,
        Vector2 screenSize,
        out Vector2 intersection)
    {
        var startAxis = horizontal ? start.Y : start.X;
        var endAxis = horizontal ? end.Y : end.X;
        var denominator = endAxis - startAxis;
        if (Mathf.Abs(denominator) <= 0.000001f)
        {
            intersection = Vector2.Zero;
            return false;
        }
        var factor = (edge - startAxis) / denominator;
        if (factor < 0.0f || factor > 1.0f)
        {
            intersection = Vector2.Zero;
            return false;
        }

        intersection = start.Lerp(end, factor);
        return horizontal
            ? intersection.X >= 0.0f && intersection.X <= screenSize.X
            : intersection.Y >= 0.0f && intersection.Y <= screenSize.Y;
    }

    private static bool ReloadMeshUsesSkeleton(
        Node3D? root,
        Skeleton3D? skeleton)
        => ReloadMeshUsesSkeleton(
            root,
            skeleton,
            ReloadFullArmSkinBones);

    private static bool ReloadMeshUsesForearmSkeleton(
        Node3D? root,
        Skeleton3D? skeleton)
        => ReloadMeshUsesSkeleton(
            root,
            skeleton,
            ReloadForearmSkinBones);

    private static bool ReloadMeshUsesSkeleton(
        Node3D? root,
        Skeleton3D? skeleton,
        IReadOnlyList<string> requiredBoneNames)
    {
        if (!IsInstanceValid(root) || !IsInstanceValid(skeleton))
        {
            return false;
        }
        if (root is MeshInstance3D rootMesh
            && ReloadMeshUsesSkeleton(
                rootMesh,
                skeleton!,
                requiredBoneNames))
        {
            return true;
        }
        foreach (var mesh in CombatModelLibrary.MeshesBelow(root!))
        {
            if (ReloadMeshUsesSkeleton(
                    mesh,
                    skeleton!,
                    requiredBoneNames))
            {
                return true;
            }
        }
        return false;
    }

    private static bool ReloadMeshUsesSkeleton(
        MeshInstance3D mesh,
        Skeleton3D skeleton,
        IReadOnlyList<string> requiredBoneNames)
    {
        if (mesh.Mesh is null
            || mesh.Skin is null
            || !mesh.HasNode(mesh.Skeleton)
            || mesh.GetNodeOrNull<Skeleton3D>(mesh.Skeleton) != skeleton)
        {
            return false;
        }
        var requiredBones = new HashSet<int>();
        foreach (var boneName in requiredBoneNames)
        {
            var bone = skeleton.FindBone(boneName);
            if (bone < 0)
            {
                return false;
            }
            requiredBones.Add(bone);
        }
        var weightedBones = new HashSet<int>();
        for (var surface = 0; surface < mesh.Mesh.GetSurfaceCount(); surface++)
        {
            using var arrays = mesh.Mesh.SurfaceGetArrays(surface);
            if (arrays[(int)Mesh.ArrayType.Bones].VariantType
                    == Variant.Type.PackedInt32Array
                && arrays[(int)Mesh.ArrayType.Weights].VariantType
                    == Variant.Type.PackedFloat32Array)
            {
                var bones = arrays[(int)Mesh.ArrayType.Bones].AsInt32Array();
                var weights = arrays[(int)Mesh.ArrayType.Weights].AsFloat32Array();
                if (bones.Length == 0 || bones.Length != weights.Length)
                {
                    continue;
                }
                for (var influence = 0; influence < bones.Length; influence++)
                {
                    var weight = weights[influence];
                    var bind = bones[influence];
                    if (!float.IsFinite(weight)
                        || weight <= 0.0001f
                        || bind < 0
                        || bind >= mesh.Skin.GetBindCount())
                    {
                        continue;
                    }
                    var skeletonBone = mesh.Skin.GetBindBone(bind);
                    if (skeletonBone < 0)
                    {
                        skeletonBone = skeleton.FindBone(
                            mesh.Skin.GetBindName(bind).ToString());
                    }
                    if (requiredBones.Contains(skeletonBone))
                    {
                        weightedBones.Add(skeletonBone);
                    }
                }
            }
        }
        return weightedBones.SetEquals(requiredBones);
    }

}
