using System;
using Godot;

namespace OperationSteelTide;

internal sealed class AuthoredAnimatedReloadArmsVisual
{
    private Vector3 _leftPalmContactInBone;
    private Vector3 _rightPalmContactInBone;
    private bool _contactPointsInitialized;

    public AuthoredAnimatedReloadArmsVisual(Node3D root)
    {
        Root = root;
        Skeleton = CombatModelLibrary.RequireSkeleton(root);
        AnimationPlayer = CombatModelLibrary.RequireAnimationPlayer(root);
        Mesh = CombatModelLibrary.RequireNode(root, "ReloadArmsMesh");
        RightGripFrame = CombatModelLibrary.RequireNode(root, "RightGripFrame");
        SupportGripFrame = CombatModelLibrary.RequireNode(root, "SupportGripFrame");
        RightPalmFrame = CombatModelLibrary.RequireNode(root, "RightPalmFrame");
        LeftPalmFrame = CombatModelLibrary.RequireNode(root, "LeftPalmFrame");
        RightWristFrame = CombatModelLibrary.RequireNode(root, "RightWristFrame");
        LeftWristFrame = CombatModelLibrary.RequireNode(root, "LeftWristFrame");
        RightShoulderFrame = CombatModelLibrary.RequireNode(root, "RightShoulderFrame");
        LeftShoulderFrame = CombatModelLibrary.RequireNode(root, "LeftShoulderFrame");
        LeftShoulderBone = Skeleton.FindBone("L_arm_01");
        LeftElbowBone = Skeleton.FindBone("L_elbow_02");
        LeftWristBone = Skeleton.FindBone("L_wrist_03");
        LeftPalmBone = Skeleton.FindBone("L_palm_015");
        RightPalmBone = Skeleton.FindBone("R_palm_039");
        ValidateContract();
    }

    public Node3D Root { get; }
    public Skeleton3D Skeleton { get; }
    public AnimationPlayer AnimationPlayer { get; }
    public Node3D Mesh { get; }
    public Node3D RightGripFrame { get; }
    public Node3D SupportGripFrame { get; }
    public Node3D RightPalmFrame { get; }
    public Node3D LeftPalmFrame { get; }
    public Node3D RightWristFrame { get; }
    public Node3D LeftWristFrame { get; }
    public Node3D RightShoulderFrame { get; }
    public Node3D LeftShoulderFrame { get; }
    public int LeftShoulderBone { get; }
    public int LeftElbowBone { get; }
    public int LeftWristBone { get; }
    public int LeftPalmBone { get; }
    public int RightPalmBone { get; }

    public Vector3 LeftPalmContactGlobalPosition
    {
        get
        {
            EnsureContactPointsInitialized();
            return ContactPointGlobal(LeftPalmBone, _leftPalmContactInBone);
        }
    }

    public Vector3 RightPalmContactGlobalPosition
    {
        get
        {
            EnsureContactPointsInitialized();
            return ContactPointGlobal(RightPalmBone, _rightPalmContactInBone);
        }
    }

    public Transform3D LeftPalmContactGlobalTransform
        => ContactFrameGlobal(LeftPalmBone, LeftPalmContactGlobalPosition);

    public Transform3D RightPalmContactGlobalTransform
        => ContactFrameGlobal(RightPalmBone, RightPalmContactGlobalPosition);

    public Vector3 LeftWristGlobalPosition
        => BoneFrameGlobal(LeftWristBone).Origin;

    public Vector3 RightWristGlobalPosition
        => BoneFrameGlobal(Skeleton.FindBone("R_wrist_026")).Origin;

    public Transform3D MarkerTransformInRoot(Node3D marker)
        => Root.GlobalTransform.AffineInverse() * marker.GlobalTransform;

    public Transform3D RightGripTransformInRoot
        => MarkerTransformInRoot(RightGripFrame);

    public string ClipName(WeaponPlatform platform, bool emptyReload)
        => FirstPersonReloadProfileCatalog.For(platform).ClipName(emptyReload);

    public bool HasClip(WeaponPlatform platform, bool emptyReload)
        => AnimationPlayer.HasAnimation(ClipName(platform, emptyReload));

    public float ClipDuration(WeaponPlatform platform, bool emptyReload)
        => (float)AnimationPlayer.GetAnimation(ClipName(platform, emptyReload)).Length;

    public void SetReloadProgress(
        WeaponPlatform platform,
        bool emptyReload,
        float progress)
    {
        var clip = ClipName(platform, emptyReload);
        if (!AnimationPlayer.HasAnimation(clip))
        {
            throw new InvalidOperationException($"Animated reload arms are missing clip '{clip}'.");
        }

        AnimationPlayer.Play(clip, 0.0);
        AnimationPlayer.Seek(
            AnimationPlayer.GetAnimation(clip).Length
                * Mathf.Clamp(progress, 0.0f, 1.0f),
            update: true);
        AnimationPlayer.Pause();
    }

    public void RetargetLeftPalm(Vector3 targetGlobalPosition)
    {
        EnsureContactPointsInitialized();
        var targetInSkeleton = Skeleton.GlobalTransform.AffineInverse()
            * targetGlobalPosition;
        var shoulder = Skeleton.GetBoneGlobalPose(LeftShoulderBone);
        var elbow = Skeleton.GetBoneGlobalPose(LeftElbowBone);
        var wrist = Skeleton.GetBoneGlobalPose(LeftWristBone);
        var originalWristBasis = wrist.Basis;
        var contactInSkeleton = Skeleton.GlobalTransform.AffineInverse()
            * LeftPalmContactGlobalPosition;
        var targetWrist = targetInSkeleton - (contactInSkeleton - wrist.Origin);

        var proximal = elbow.Origin - shoulder.Origin;
        var distal = wrist.Origin - elbow.Origin;
        var proximalLength = proximal.Length();
        var distalLength = distal.Length();
        var shoulderToTarget = targetWrist - shoulder.Origin;
        if (proximalLength <= 0.0001f
            || distalLength <= 0.0001f
            || shoulderToTarget.LengthSquared() <= 0.000001f)
        {
            return;
        }

        var direction = shoulderToTarget.Normalized();
        var requestedDistance = shoulderToTarget.Length();
        var minimumDistance = Mathf.Abs(proximalLength - distalLength) + 0.0001f;
        var maximumDistance = proximalLength + distalLength - 0.0001f;
        var solvedDistance = Mathf.Clamp(
            requestedDistance,
            minimumDistance,
            maximumDistance);
        var projectedElbowDistance = (
            proximalLength * proximalLength
            - distalLength * distalLength
            + solvedDistance * solvedDistance)
            / (2.0f * solvedDistance);
        var elbowHeight = Mathf.Sqrt(Mathf.Max(
            0.0f,
            proximalLength * proximalLength
                - projectedElbowDistance * projectedElbowDistance));
        var currentElbowPlane = elbow.Origin
            - (shoulder.Origin
                + direction * (elbow.Origin - shoulder.Origin).Dot(direction));
        if (currentElbowPlane.LengthSquared() <= 0.000001f)
        {
            currentElbowPlane = Vector3.Up.Cross(direction);
            if (currentElbowPlane.LengthSquared() <= 0.000001f)
            {
                currentElbowPlane = Vector3.Right.Cross(direction);
            }
        }
        var elbowDirection = currentElbowPlane.Normalized();
        var desiredElbow = shoulder.Origin
            + direction * projectedElbowDistance
            + elbowDirection * elbowHeight;
        var desiredWrist = shoulder.Origin + direction * solvedDistance;

        var shoulderSwing = new Quaternion(
            proximal.Normalized(),
            (desiredElbow - shoulder.Origin).Normalized());
        Skeleton.SetBoneGlobalPose(
            LeftShoulderBone,
            new Transform3D(
                new Basis(shoulderSwing) * shoulder.Basis,
                shoulder.Origin));
        Skeleton.ForceUpdateBoneChildTransform(LeftShoulderBone);

        var solvedElbow = Skeleton.GetBoneGlobalPose(LeftElbowBone);
        var solvedWrist = Skeleton.GetBoneGlobalPose(LeftWristBone);
        var solvedDistal = solvedWrist.Origin - solvedElbow.Origin;
        var desiredDistal = desiredWrist - solvedElbow.Origin;
        if (solvedDistal.LengthSquared() > 0.000001f
            && desiredDistal.LengthSquared() > 0.000001f)
        {
            var elbowSwing = new Quaternion(
                solvedDistal.Normalized(),
                desiredDistal.Normalized());
            Skeleton.SetBoneGlobalPose(
                LeftElbowBone,
                new Transform3D(
                    new Basis(elbowSwing) * solvedElbow.Basis,
                    solvedElbow.Origin));
            Skeleton.ForceUpdateBoneChildTransform(LeftElbowBone);
        }

        solvedWrist = Skeleton.GetBoneGlobalPose(LeftWristBone);
        Skeleton.SetBoneGlobalPose(
            LeftWristBone,
            new Transform3D(originalWristBasis, solvedWrist.Origin));
        Skeleton.ForceUpdateBoneChildTransform(LeftWristBone);
    }

    private Vector3 ContactPointInBone(Node3D marker, int bone)
    {
        var markerInSkeleton = Skeleton.GlobalTransform.AffineInverse()
            * marker.GlobalPosition;
        return Skeleton.GetBoneGlobalPose(bone).AffineInverse()
            * markerInSkeleton;
    }

    private void EnsureContactPointsInitialized()
    {
        if (_contactPointsInitialized)
        {
            return;
        }
        if (!Root.IsInsideTree())
        {
            throw new InvalidOperationException(
                "Animated reload arm contact points require an active scene tree.");
        }

        _leftPalmContactInBone = ContactPointInBone(
            LeftPalmFrame,
            LeftPalmBone);
        _rightPalmContactInBone = ContactPointInBone(
            RightPalmFrame,
            RightPalmBone);
        _contactPointsInitialized = true;
    }

    private Vector3 ContactPointGlobal(int bone, Vector3 pointInBone)
        => Skeleton.GlobalTransform
            * (Skeleton.GetBoneGlobalPose(bone) * pointInBone);

    private Transform3D ContactFrameGlobal(int bone, Vector3 contactGlobalPosition)
    {
        var boneFrame = BoneFrameGlobal(bone);
        return new Transform3D(
            boneFrame.Basis.Orthonormalized(),
            contactGlobalPosition);
    }

    private Transform3D BoneFrameGlobal(int bone)
        => Skeleton.GlobalTransform * Skeleton.GetBoneGlobalPose(bone);

    private void ValidateContract()
    {
        foreach (var platform in Enum.GetValues<WeaponPlatform>())
        {
            if (platform == WeaponPlatform.M3A1)
            {
                continue;
            }
            foreach (var emptyReload in new[] { false, true })
            {
                var clip = ClipName(platform, emptyReload);
                if (!AnimationPlayer.HasAnimation(clip))
                {
                    throw new InvalidOperationException(
                        $"Animated reload arms contract is missing '{clip}'.");
                }
            }
        }

        foreach (var bone in new[]
                 {
                     "L_arm_01", "L_elbow_02", "L_wrist_03",
                     "R_arm_024", "R_elbow_025", "R_wrist_026"
                 })
        {
            if (Skeleton.FindBone(bone) < 0)
            {
                throw new InvalidOperationException(
                    $"Animated reload arms contract is missing bone '{bone}'.");
            }
        }
    }
}

internal static partial class CombatModelLibrary
{
    internal const string AnimatedReloadArmsScenePath =
        "res://assets/models/djmaesen_smg45/animated_reload_arms.glb";

    private static readonly string[] AnimatedReloadArmsNodes =
    {
        "WeaponRoot", "ReloadArmsSkeleton", "ReloadArmsMesh",
        "RightGripFrame", "SupportGripFrame",
        "LeftPalmFrame", "RightPalmFrame",
        "LeftWristFrame", "RightWristFrame",
        "LeftShoulderFrame", "RightShoulderFrame"
    };

    public static AuthoredAnimatedReloadArmsVisual InstantiateAnimatedReloadArms()
    {
        var root = InstantiateRequired(
            AnimatedReloadArmsScenePath,
            AnimatedReloadArmsNodes);
        root.Name = "AuthoredAnimatedReloadArmsVisual";
        foreach (var geometry in GeometryBelow(root))
        {
            geometry.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        }
        return new AuthoredAnimatedReloadArmsVisual(root);
    }
}
