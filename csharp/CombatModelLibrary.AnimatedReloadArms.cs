using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

internal sealed class AuthoredAnimatedReloadArmsVisual
{
    private static readonly Vector3 SidearmWristScreenOffset = new(
        -0.030f,
        -0.035f,
        0.0f);
    private const float SidearmWristDepthRetreat = 0.18f;
    private const float SidearmMaximumWristCorrectionRadians = 0.16f;
    private Vector3 _leftPalmContactInBone;
    private Vector3 _leftGripAnchorInBone;
    private Vector3 _leftSidearmMagazineAnchorInBone;
    private Vector3 _rightPalmContactInBone;
    private readonly Dictionary<WeaponPlatform, Node3D> _leftElbowPoleFrames = new();
    private bool _contactPointsInitialized;
    private Vector3 _presentedLeftSupportTargetGlobalPosition;
    private string _presentedClipName = string.Empty;
    private float _presentedClipProgress;

    public AuthoredAnimatedReloadArmsVisual(Node3D root)
    {
        Root = root;
        Skeleton = CombatModelLibrary.RequireSkeleton(root);
        AnimationPlayer = CombatModelLibrary.RequireAnimationPlayer(root);
        // ReloadArmsMesh is a visibility-only compatibility layer retained for
        // existing diagnostics. Runtime geometry is always one of the two
        // authored forearm crops below; the complete arms are audit-only.
        FullMesh = CombatModelLibrary.RequireNode(root, "ReloadArmsMesh");
        LongGunForearmsMesh = CombatModelLibrary.RequireNode(
            root,
            "LongGunReloadForearmsMesh");
        SidearmForearmsMesh = CombatModelLibrary.RequireNode(
            root,
            "SidearmReloadForearmsMesh");
        FullAuditMesh = CombatModelLibrary.RequireNode(
            root,
            "FullReloadArmsAuditMesh");
        FullMesh.Visible = true;
        LongGunForearmsMesh.Visible = true;
        SidearmForearmsMesh.Visible = false;
        FullAuditMesh.Visible = false;
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
    /// <summary>Compatibility visibility layer; it contains no geometry.</summary>
    public Node3D FullMesh { get; }
    public Node3D LongGunForearmsMesh { get; }
    public Node3D SidearmForearmsMesh { get; }
    public Node3D FullAuditMesh { get; }
    public Node3D Mesh
        => SidearmForearmsMesh.Visible
            ? SidearmForearmsMesh
            : LongGunForearmsMesh;
    public bool UsesSidearmForearms
        => SidearmForearmsMesh.Visible
            && !LongGunForearmsMesh.Visible
            && !FullAuditMesh.Visible;
    public bool UsesLongGunForearms
        => LongGunForearmsMesh.Visible
            && !SidearmForearmsMesh.Visible
            && !FullAuditMesh.Visible;
    // Compatibility name consumed by the existing TacticalPlayer diagnostic
    // surface. It now means the non-sidearm authored reload presentation, not
    // that the complete upper-arm audit mesh is rendered.
    public bool UsesFullArms
        => UsesLongGunForearms && FullMesh.Visible;
    public string PresentedClipName
        => _presentedClipName;
    public float PresentedClipProgress => _presentedClipProgress;
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
        float sidearmMagazineBlend = 1.0f,
        float sidearmActionBlend = 0.0f)
    {
        if (!WeaponCatalog.IsSidearm(platform))
        {
            // Place the visible palm itself on the long-gun target.  The DCC
            // foregrip marker is offset from the palm and leaves a noticeable
            // hand pop when the reload layer returns to the normal support
            // grip at the final frame.
            return LeftPalmCenterGlobalPosition;
        }
        EnsureContactPointsInitialized();
        // The imported glove is visually centred on the wrist weights; its
        // palm child marker can move onto the prop while the rendered fist
        // remains several centimetres away. During contact the control point
        // therefore becomes the visible wrist. At both clip boundaries it is
        // still the authored palm marker, preserving the exact ready-pose
        // handoff to the ordinary first-person arms.
        var palmAnchor = SidearmPalmAnchorGlobalPosition(
            sidearmMagazineBlend);
        var visibleContactBlend = Mathf.Max(
            Mathf.Clamp(sidearmMagazineBlend, 0.0f, 1.0f),
            Mathf.Clamp(sidearmActionBlend, 0.0f, 1.0f));
        return palmAnchor.Lerp(
            LeftWristGlobalPosition,
            visibleContactBlend);
    }

    public Vector3 SidearmSupportTargetGlobalPosition(
        Vector3 propTargetGlobalPosition,
        float sidearmMagazineBlend,
        float sidearmActionBlend)
    {
        var visibleContactBlend = Mathf.Max(
            Mathf.Clamp(sidearmMagazineBlend, 0.0f, 1.0f),
            Mathf.Clamp(sidearmActionBlend, 0.0f, 1.0f));
        if (visibleContactBlend <= 0.0f)
        {
            return propTargetGlobalPosition;
        }

        var desiredWrist = SidearmDesiredWristGlobalPosition(
            propTargetGlobalPosition);
        return propTargetGlobalPosition.Lerp(
            desiredWrist,
            visibleContactBlend);
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

    public Vector3 PresentedLeftSupportTargetGlobalPosition
        => _presentedLeftSupportTargetGlobalPosition;

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

        if (!string.Equals(
                AnimationPlayer.CurrentAnimation.ToString(),
                clip,
                StringComparison.Ordinal))
        {
            AnimationPlayer.Play(clip, 0.0);
        }
        // Runtime contact retargeting writes global poses on the left chain.
        // Clear those edits before sampling the authored clip so presenting
        // the same normalized time twice is deterministic instead of applying
        // the previous frame's shoulder translation a second time.
        Skeleton.ResetBonePose(LeftShoulderBone);
        Skeleton.ResetBonePose(LeftElbowBone);
        Skeleton.ResetBonePose(LeftWristBone);
        Skeleton.ResetBonePose(LeftPalmBone);
        Skeleton.ForceUpdateBoneChildTransform(LeftShoulderBone);
        var normalizedProgress = Mathf.Clamp(progress, 0.0f, 1.0f);
        var clipDuration = AnimationPlayer.GetAnimation(clip).Length;
        if (string.Equals(
                _presentedClipName,
                clip,
                StringComparison.Ordinal)
            && Mathf.IsEqualApprox(
                _presentedClipProgress,
                normalizedProgress))
        {
            // AnimationPlayer may elide a seek to its current timestamp. Move
            // through frame zero so the next seek reapplies every authored
            // track after the manual contact pose was cleared.
            AnimationPlayer.Seek(0.0, update: true, updateOnly: false);
        }
        AnimationPlayer.Seek(
            clipDuration * normalizedProgress,
            update: true,
            updateOnly: false);
        Skeleton.ForceUpdateBoneChildTransform(LeftShoulderBone);
        AnimationPlayer.Pause();
        _presentedClipName = clip;
        _presentedClipProgress = normalizedProgress;
    }

    public void SetPresentationPlatform(WeaponPlatform platform)
    {
        var sidearm = WeaponCatalog.IsSidearm(platform);
        FullMesh.Visible = !sidearm;
        LongGunForearmsMesh.Visible = !sidearm;
        SidearmForearmsMesh.Visible = sidearm;
        FullAuditMesh.Visible = false;
    }

    public void RetargetLeftPalm(
        WeaponPlatform platform,
        Vector3 targetGlobalPosition,
        float sidearmMagazineBlend = 0.0f,
        float sidearmActionBlend = 0.0f)
    {
        EnsureContactPointsInitialized();
        targetGlobalPosition = ReachableLeftPalmTarget(
            platform,
            targetGlobalPosition,
            sidearmMagazineBlend);
        var targetInSkeleton = Skeleton.GlobalTransform.AffineInverse()
            * targetGlobalPosition;
        if (UsesSidearmForearms)
        {
            var targetControlGlobal = SidearmSupportTargetGlobalPosition(
                targetGlobalPosition,
                sidearmMagazineBlend,
                sidearmActionBlend);
            _presentedLeftSupportTargetGlobalPosition = targetControlGlobal;
            // Keep the DCC finger pose, but turn the wrist just enough for the
            // compact cuff to enter from below-left instead of pointing into
            // the camera near plane. Fade this correction with the magazine
            // contact marker so both visibility endpoints remain the exact
            // authored static pose.
            var compactWrist = Skeleton.GetBoneGlobalPose(LeftWristBone);
            var authoredContactInSkeleton = Skeleton.GlobalTransform
                .AffineInverse()
                * SidearmPalmAnchorGlobalPosition(sidearmMagazineBlend);
            var authoredWristToContact = authoredContactInSkeleton
                - compactWrist.Origin;
            if (authoredWristToContact.LengthSquared() > 0.000001f)
            {
                var sidearmDesiredWrist = SidearmDesiredWristGlobalPosition(
                    targetGlobalPosition);
                var desiredWristToContact = Skeleton.GlobalTransform
                    .Basis.Inverse()
                    * (targetGlobalPosition - sidearmDesiredWrist);
                if (desiredWristToContact.LengthSquared() > 0.000001f)
                {
                    var fullSwing = new Quaternion(
                        authoredWristToContact.Normalized(),
                        desiredWristToContact.Normalized());
                    var fullSwingAngle = Quaternion.Identity.AngleTo(
                        fullSwing);
                    var limitedSwing = Quaternion.Identity.Slerp(
                        fullSwing,
                        fullSwingAngle <= SidearmMaximumWristCorrectionRadians
                            ? 1.0f
                            : SidearmMaximumWristCorrectionRadians
                                / fullSwingAngle);
                    var blendedSwing = Quaternion.Identity.Slerp(
                        limitedSwing,
                        Mathf.Max(
                            Mathf.Clamp(sidearmMagazineBlend, 0.0f, 1.0f),
                            Mathf.Clamp(sidearmActionBlend, 0.0f, 1.0f)));
                    compactWrist.Basis = (
                        new Basis(blendedSwing) * compactWrist.Basis)
                        .Orthonormalized();
                    Skeleton.SetBoneGlobalPose(LeftWristBone, compactWrist);
                    Skeleton.ForceUpdateBoneChildTransform(LeftWristBone);
                }
            }

            // Translate the complete hidden-root chain by the remaining
            // contact residual so finger-weighted vertices and the short cuff
            // follow the live magazine together without stretching.
            var currentControlInSkeleton = Skeleton.GlobalTransform
                .AffineInverse()
                * LeftSupportAnchorGlobalPosition(
                    platform,
                    sidearmMagazineBlend,
                    sidearmActionBlend);
            var targetControlInSkeleton = Skeleton.GlobalTransform
                .AffineInverse()
                * targetControlGlobal;
            var compactShoulder = Skeleton.GetBoneGlobalPose(LeftShoulderBone);
            compactShoulder.Origin += targetControlInSkeleton
                - currentControlInSkeleton;
            Skeleton.SetBoneGlobalPose(LeftShoulderBone, compactShoulder);
            Skeleton.ForceUpdateBoneChildTransform(LeftShoulderBone);
            return;
        }

        _presentedLeftSupportTargetGlobalPosition = targetGlobalPosition;

        var shoulder = Skeleton.GetBoneGlobalPose(LeftShoulderBone);
        var elbow = Skeleton.GetBoneGlobalPose(LeftElbowBone);
        var wrist = Skeleton.GetBoneGlobalPose(LeftWristBone);
        var originalElbowBasis = elbow.Basis;
        var originalWristBasis = wrist.Basis;
        // Long-gun targets use the palm centre. Sidearms blend toward a marker
        // on the lower glove surface while holding a magazine, then back to the
        // palm centre for the ready-pose handoff. Solving that actual contact
        // marker keeps the glove on the prop without translating the hidden
        // shoulder or stretching either articulated segment.
        var contactInSkeleton = Skeleton.GlobalTransform.AffineInverse()
            * LeftSupportAnchorGlobalPosition(
                platform,
                sidearmMagazineBlend);
        var wristToContact = contactInSkeleton - wrist.Origin;
        var targetWrist = targetInSkeleton - wristToContact;

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
        Vector3 requestedGlobalPosition,
        float sidearmMagazineBlend = 0.0f)
    {
        if (UsesSidearmForearms)
        {
            // The upper chain is hidden for pistol reloads and translates as
            // one authored unit, so it does not need a two-bone reach clamp.
            return requestedGlobalPosition;
        }

        EnsureContactPointsInitialized();
        var requestedInSkeleton = Skeleton.GlobalTransform.AffineInverse()
            * requestedGlobalPosition;
        var shoulder = Skeleton.GetBoneGlobalPose(LeftShoulderBone);
        var elbow = Skeleton.GetBoneGlobalPose(LeftElbowBone);
        var wrist = Skeleton.GetBoneGlobalPose(LeftWristBone);
        var contactInSkeleton = Skeleton.GlobalTransform.AffineInverse()
            * LeftSupportAnchorGlobalPosition(
                platform,
                sidearmMagazineBlend);
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

    private Vector3 SidearmPalmAnchorGlobalPosition(float magazineBlend)
    {
        var contactInBone = _leftPalmContactInBone.Lerp(
            _leftSidearmMagazineAnchorInBone,
            Mathf.Clamp(magazineBlend, 0.0f, 1.0f));
        return ContactPointGlobal(LeftPalmBone, contactInBone);
    }

    private Vector3 SidearmDesiredWristGlobalPosition(
        Vector3 propTargetGlobalPosition)
    {
        var camera = FirstPersonCameraAncestor();
        if (camera is null || !GodotObject.IsInstanceValid(camera))
        {
            return propTargetGlobalPosition;
        }

        var cameraInverse = camera.GlobalTransform.AffineInverse();
        var targetInCamera = cameraInverse * propTargetGlobalPosition;
        if (Mathf.Abs(targetInCamera.Z) <= 0.0001f)
        {
            return propTargetGlobalPosition;
        }

        // Match the prop's screen ray at a fixed depth behind the contact.
        // Deriving that depth from the already-retargeted wrist makes the
        // whole hidden shoulder chain retreat again every presentation frame.
        // The fixed prop-relative depth is deterministic and still keeps the
        // glove away from the near plane. A small camera-plane offset leaves
        // the wrist below-left while the fingers overlap the magazine or slide.
        var desiredWristDepth = targetInCamera.Z - SidearmWristDepthRetreat;
        var depthScale = desiredWristDepth / targetInCamera.Z;
        var targetAtWristDepth = new Vector3(
            targetInCamera.X * depthScale,
            targetInCamera.Y * depthScale,
            desiredWristDepth);
        return camera.GlobalTransform
            * (targetAtWristDepth + SidearmWristScreenOffset);
    }

    private Camera3D? FirstPersonCameraAncestor()
    {
        for (Node? ancestor = Root.GetParent();
             ancestor is not null;
             ancestor = ancestor.GetParent())
        {
            if (ancestor is Camera3D camera)
            {
                return camera;
            }
        }
        return null;
    }

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
