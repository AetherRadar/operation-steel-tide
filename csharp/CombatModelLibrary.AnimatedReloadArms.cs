using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

internal sealed class AuthoredAnimatedReloadArmsVisual
{
    private Vector3 _leftPalmContactInBone;
    private Vector3 _leftGripAnchorInBone;
    private Vector3 _leftSidearmMagazineAnchorInBone;
    private Vector3 _rightPalmContactInBone;
    private readonly Dictionary<WeaponPlatform, Node3D> _leftElbowPoleFrames = new();
    private bool _contactPointsInitialized;

    public AuthoredAnimatedReloadArmsVisual(Node3D root)
    {
        Root = root;
        Skeleton = CombatModelLibrary.RequireSkeleton(root);
        AnimationPlayer = CombatModelLibrary.RequireAnimationPlayer(root);
        FullMesh = CombatModelLibrary.RequireNode(root, "ReloadArmsMesh");
        SidearmForearmsMesh = CombatModelLibrary.RequireNode(
            root,
            "SidearmReloadForearmsMesh");
        // Reload presentation is deliberately limited to the gloves and short
        // cuffs. The complete sleeve mesh can cross the camera near plane when
        // an authored reach is retargeted, producing the giant "tentacle" seen
        // in first person.
        FullMesh.Visible = false;
        SidearmForearmsMesh.Visible = true;
        RightGripFrame = CombatModelLibrary.RequireNode(root, "RightGripFrame");
        SupportGripFrame = CombatModelLibrary.RequireNode(root, "SupportGripFrame");
        RightPalmFrame = CombatModelLibrary.RequireNode(root, "RightPalmFrame");
        LeftPalmFrame = CombatModelLibrary.RequireNode(root, "LeftPalmFrame");
        LeftGripAnchorFrame = CombatModelLibrary.RequireNode(
            root,
            "LeftGripAnchorFrame");
        LeftSidearmMagazineAnchorFrame = CombatModelLibrary.RequireNode(
            root,
            "LeftSidearmMagazineAnchorFrame");
        RightWristFrame = CombatModelLibrary.RequireNode(root, "RightWristFrame");
        LeftWristFrame = CombatModelLibrary.RequireNode(root, "LeftWristFrame");
        RightShoulderFrame = CombatModelLibrary.RequireNode(root, "RightShoulderFrame");
        LeftShoulderFrame = CombatModelLibrary.RequireNode(root, "LeftShoulderFrame");
        LeftShoulderBone = Skeleton.FindBone("L_arm_01");
        LeftElbowBone = Skeleton.FindBone("L_elbow_02");
        LeftWristBone = Skeleton.FindBone("L_wrist_03");
        LeftPalmBone = Skeleton.FindBone("L_palm_015");
        RightPalmBone = Skeleton.FindBone("R_palm_039");
        foreach (var platform in Enum.GetValues<WeaponPlatform>())
        {
            if (platform == WeaponPlatform.M3A1)
            {
                continue;
            }
            var clipStem = FirstPersonReloadProfileCatalog.For(platform).ClipStem;
            _leftElbowPoleFrames[platform] = CombatModelLibrary.RequireNode(
                root,
                $"{clipStem}_ElbowPoleFrame");
        }
        ValidateContract();
    }

    public Node3D Root { get; }
    public Skeleton3D Skeleton { get; }
    public AnimationPlayer AnimationPlayer { get; }
    public Node3D FullMesh { get; }
    public Node3D SidearmForearmsMesh { get; }
    public Node3D Mesh
        => SidearmForearmsMesh.Visible ? SidearmForearmsMesh : FullMesh;
    public bool UsesSidearmForearms
        => SidearmForearmsMesh.Visible && !FullMesh.Visible;
    public Node3D RightGripFrame { get; }
    public Node3D SupportGripFrame { get; }
    public Node3D RightPalmFrame { get; }
    public Node3D LeftPalmFrame { get; }
    public Node3D LeftGripAnchorFrame { get; }
    public Node3D LeftSidearmMagazineAnchorFrame { get; }
    public Node3D RightWristFrame { get; }
    public Node3D LeftWristFrame { get; }
    public Node3D RightShoulderFrame { get; }
    public Node3D LeftShoulderFrame { get; }
    public int LeftShoulderBone { get; }
    public int LeftElbowBone { get; }
    public int LeftWristBone { get; }
    public int LeftPalmBone { get; }
    public int RightPalmBone { get; }

    public Vector3 LeftPalmCenterGlobalPosition
    {
        get
        {
            EnsureContactPointsInitialized();
            return ContactPointGlobal(LeftPalmBone, _leftPalmContactInBone);
        }
    }

    public Vector3 LeftGripAnchorGlobalPosition
    {
        get
        {
            EnsureContactPointsInitialized();
            return ContactPointGlobal(LeftPalmBone, _leftGripAnchorInBone);
        }
    }

    public Vector3 LeftSupportAnchorGlobalPosition(
        WeaponPlatform platform,
        float sidearmMagazineBlend = 1.0f)
    {
        if (!WeaponCatalog.IsSidearm(platform))
        {
            return LeftGripAnchorGlobalPosition;
        }
        EnsureContactPointsInitialized();
        var contactInBone = _leftPalmContactInBone.Lerp(
            _leftSidearmMagazineAnchorInBone,
            Mathf.Clamp(sidearmMagazineBlend, 0.0f, 1.0f));
        return ContactPointGlobal(
            LeftPalmBone,
            contactInBone);
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
        => ContactFrameGlobal(LeftPalmBone, LeftPalmCenterGlobalPosition);

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

    public void SetPresentationPlatform(WeaponPlatform platform)
    {
        _ = platform;
        FullMesh.Visible = false;
        SidearmForearmsMesh.Visible = true;
    }

    public void RetargetLeftPalm(
        WeaponPlatform platform,
        Vector3 targetGlobalPosition,
        float sidearmMagazineBlend = 1.0f)
    {
        EnsureContactPointsInitialized();
        var targetInSkeleton = Skeleton.GlobalTransform.AffineInverse()
            * targetGlobalPosition;
        if (UsesSidearmForearms)
        {
            // Only the glove, short cuff, and forearm are rendered for every
            // weapon. Move that compact chain as one authored unit instead of
            // solving an invisible shoulder/elbow IK chain; this preserves the
            // clip's hand pose and cannot stretch a sleeve across the viewport.
            var compactContactInSkeleton = Skeleton.GlobalTransform.AffineInverse()
                * LeftSupportAnchorGlobalPosition(
                    platform,
                    sidearmMagazineBlend);
            var compactShoulder = Skeleton.GetBoneGlobalPose(LeftShoulderBone);
            compactShoulder.Origin += targetInSkeleton - compactContactInSkeleton;
            Skeleton.SetBoneGlobalPose(LeftShoulderBone, compactShoulder);
            Skeleton.ForceUpdateBoneChildTransform(LeftShoulderBone);
            return;
        }

        targetGlobalPosition = ReachableLeftPalmTarget(platform, targetGlobalPosition);
        targetInSkeleton = Skeleton.GlobalTransform.AffineInverse()
            * targetGlobalPosition;

        var shoulder = Skeleton.GetBoneGlobalPose(LeftShoulderBone);
        var elbow = Skeleton.GetBoneGlobalPose(LeftElbowBone);
        var wrist = Skeleton.GetBoneGlobalPose(LeftWristBone);
        var originalElbowBasis = elbow.Basis;
        var originalWristBasis = wrist.Basis;
        var contactInSkeleton = Skeleton.GlobalTransform.AffineInverse()
            * LeftGripAnchorGlobalPosition;
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
        var solvedShoulderOrigin = shoulder.Origin;
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
        // Sidearms use the exact DCC pole that baked their clips. Their compact
        // target path crosses a nearly straight-chain singularity where a tiny
        // sign change otherwise flips the complete arm. Long guns retain the
        // continuously baked elbow plane; forcing their distant sidearm-style
        // pole pulls the upper sleeve through the camera near plane.
        var elbowDirection = WeaponCatalog.IsSidearm(platform)
            ? AuthoredElbowPoleDirection(platform, solvedShoulderOrigin, direction)
            : elbow.Origin
                - (solvedShoulderOrigin
                    + direction
                        * (elbow.Origin - solvedShoulderOrigin).Dot(direction));
        if (elbowDirection.LengthSquared() <= 0.000001f)
        {
            elbowDirection = AuthoredElbowPoleDirection(
                platform,
                solvedShoulderOrigin,
                direction);
        }
        if (elbowDirection.LengthSquared() <= 0.000001f)
        {
            var fallbackPole = Mathf.Abs(direction.Dot(Vector3.Up)) < 0.85f
                ? Vector3.Up
                : Vector3.Right;
            elbowDirection = fallbackPole
                - direction * fallbackPole.Dot(direction);
        }
        elbowDirection = elbowDirection.Normalized();
        var desiredElbow = solvedShoulderOrigin
            + direction * projectedElbowDistance
            + elbowDirection * elbowHeight;
        // Both articulated segments remain at their authored length, and the
        // shoulder stays at its DCC-authored origin. Translating a skinned
        // shoulder to make up an unreachable target stretches the sleeve away
        // from the torso and can pull a large triangle through the near plane.
        var desiredWrist = solvedShoulderOrigin + direction * solvedDistance;

        var shoulderSwing = new Quaternion(
            proximal.Normalized(),
            (desiredElbow - solvedShoulderOrigin).Normalized());
        Skeleton.SetBoneGlobalPose(
            LeftShoulderBone,
            new Transform3D(
                (new Basis(shoulderSwing) * shoulder.Basis).Orthonormalized(),
                solvedShoulderOrigin));
        Skeleton.ForceUpdateBoneChildTransform(LeftShoulderBone);

        var solvedElbow = Skeleton.GetBoneGlobalPose(LeftElbowBone);
        var desiredDistal = desiredWrist - solvedElbow.Origin;
        if (distal.LengthSquared() > 0.000001f
            && desiredDistal.LengthSquared() > 0.000001f)
        {
            var elbowSwing = new Quaternion(
                distal.Normalized(),
                desiredDistal.Normalized());
            Skeleton.SetBoneGlobalPose(
                LeftElbowBone,
                new Transform3D(
                    (new Basis(elbowSwing) * originalElbowBasis).Orthonormalized(),
                    solvedElbow.Origin));
            Skeleton.ForceUpdateBoneChildTransform(LeftElbowBone);
        }

        var solvedWrist = Skeleton.GetBoneGlobalPose(LeftWristBone);
        Skeleton.SetBoneGlobalPose(
            LeftWristBone,
            new Transform3D(
                originalWristBasis.Orthonormalized(),
                solvedWrist.Origin));
        Skeleton.ForceUpdateBoneChildTransform(LeftWristBone);
    }

    private Vector3 AuthoredElbowPoleDirection(
        WeaponPlatform platform,
        Vector3 shoulderOrigin,
        Vector3 armDirection)
    {
        var poleInSkeleton = Skeleton.GlobalTransform.AffineInverse()
            * _leftElbowPoleFrames[platform].GlobalPosition;
        var poleDirection = poleInSkeleton - shoulderOrigin;
        return poleDirection
            - armDirection * poleDirection.Dot(armDirection);
    }

    public Vector3 ReachableLeftPalmTarget(
        WeaponPlatform platform,
        Vector3 requestedGlobalPosition)
    {
        if (WeaponCatalog.IsSidearm(platform))
        {
            return requestedGlobalPosition;
        }

        EnsureContactPointsInitialized();
        var requestedInSkeleton = Skeleton.GlobalTransform.AffineInverse()
            * requestedGlobalPosition;
        var shoulder = Skeleton.GetBoneGlobalPose(LeftShoulderBone);
        var elbow = Skeleton.GetBoneGlobalPose(LeftElbowBone);
        var wrist = Skeleton.GetBoneGlobalPose(LeftWristBone);
        var contactInSkeleton = Skeleton.GlobalTransform.AffineInverse()
            * LeftGripAnchorGlobalPosition;
        var wristToContact = contactInSkeleton - wrist.Origin;
        var requestedWrist = requestedInSkeleton - wristToContact;
        var shoulderToWrist = requestedWrist - shoulder.Origin;
        var proximalLength = elbow.Origin.DistanceTo(shoulder.Origin);
        var distalLength = wrist.Origin.DistanceTo(elbow.Origin);
        if (proximalLength <= 0.0001f
            || distalLength <= 0.0001f
            || shoulderToWrist.LengthSquared() <= 0.000001f)
        {
            return requestedGlobalPosition;
        }

        var requestedDistance = shoulderToWrist.Length();
        var minimumDistance = Mathf.Abs(proximalLength - distalLength) + 0.0001f;
        var maximumDistance = proximalLength + distalLength - 0.0001f;
        var solvedDistance = Mathf.Clamp(
            requestedDistance,
            minimumDistance,
            maximumDistance);
        if (Mathf.IsEqualApprox(requestedDistance, solvedDistance))
        {
            return requestedGlobalPosition;
        }

        var solvedWrist = shoulder.Origin
            + shoulderToWrist.Normalized() * solvedDistance;
        return Skeleton.GlobalTransform * (solvedWrist + wristToContact);
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
        _leftGripAnchorInBone = ContactPointInBone(
            LeftGripAnchorFrame,
            LeftPalmBone);
        _leftSidearmMagazineAnchorInBone = ContactPointInBone(
            LeftSidearmMagazineAnchorFrame,
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
        "SidearmReloadForearmsMesh",
        "RightGripFrame", "SupportGripFrame",
        "LeftPalmFrame", "LeftGripAnchorFrame",
        "LeftSidearmMagazineAnchorFrame", "RightPalmFrame",
        "LeftWristFrame", "RightWristFrame",
        "LeftShoulderFrame", "RightShoulderFrame",
        "m4a1_ElbowPoleFrame", "ak74_ElbowPoleFrame",
        "scarl_ElbowPoleFrame", "mp5a5_ElbowPoleFrame",
        "m24_ElbowPoleFrame", "axmc_ElbowPoleFrame",
        "awm_ElbowPoleFrame", "vss_ElbowPoleFrame",
        "p226_ElbowPoleFrame", "m1911_ElbowPoleFrame",
        "gsh18_ElbowPoleFrame", "desert_eagle_ElbowPoleFrame"
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
