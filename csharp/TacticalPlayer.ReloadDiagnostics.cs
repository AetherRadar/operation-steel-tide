using System;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class TacticalPlayer
{
    internal AllWeaponReloadInspection InspectAllWeaponReloadForDiagnostics()
    {
        if (EquippedWeapon.Platform == WeaponPlatform.M3A1)
        {
            return InspectNativeSmgReloadForDiagnostics();
        }

        var platform = EquippedWeapon.Platform;
        var arms = AnimatedReloadArmsForDiagnostics;
        var weapon = ActiveAuthoredReloadWeapon();
        var weaponRoot = ActiveAuthoredWeaponRootForDiagnostics;
        var staticArms = ActiveAuthoredArms();
        var useAnimatedPose = UsesAnimatedReloadArmsForDiagnostics;
        var pose = FirstPersonArmPoseCatalog.For(platform);
        var clipName = FirstPersonReloadProfileCatalog.For(platform)
            .ClipName(_reloadStartedEmpty);
        var clipExists = arms?.HasClip(platform, _reloadStartedEmpty) == true;
        var rootInverse = _weaponRoot.GlobalTransform.AffineInverse();
        var rightShoulder = ReloadBoneInWeaponRoot(
            useAnimatedPose ? arms?.Skeleton : null,
            "R_arm_024",
            rootInverse,
            out var rightShoulderAvailable);
        var leftShoulder = ReloadBoneInWeaponRoot(
            useAnimatedPose ? arms?.Skeleton : null,
            "L_arm_01",
            rootInverse,
            out var leftShoulderAvailable);
        var palmContactsAvailable = useAnimatedPose
            ? arms is not null && IsInstanceValid(arms.Root)
            : staticArms is not null && IsInstanceValid(staticArms.Root);
        var rightPalmAvailable = palmContactsAvailable;
        var rightPalmGlobal = palmContactsAvailable
            ? useAnimatedPose
                ? arms!.RightPalmContactGlobalPosition
                : staticArms!.RightPalmFrame.GlobalPosition
            : Vector3.Zero;
        var rightPalm = palmContactsAvailable
            ? rootInverse * rightPalmGlobal
            : Vector3.Zero;
        var rightWristGlobal = palmContactsAvailable
            ? useAnimatedPose
                ? arms!.RightWristGlobalPosition
                : staticArms!.RightWristFrame.GlobalPosition
            : Vector3.Zero;
        var leftPalmAvailable = palmContactsAvailable;
        var leftPalmGlobal = palmContactsAvailable
            ? useAnimatedPose
                ? arms!.LeftSupportAnchorGlobalPosition(
                    EquippedWeapon.Platform,
                    SidearmReloadMagazineAnchorBlend())
                : staticArms!.LeftGripFrame.GlobalPosition
            : Vector3.Zero;
        var visibleLeftPalmGlobal = palmContactsAvailable
            ? useAnimatedPose
                ? arms!.LeftPalmCenterGlobalPosition
                : staticArms!.LeftPalmFrame.GlobalPosition
            : Vector3.Zero;
        var leftWristGlobal = palmContactsAvailable
            ? useAnimatedPose
                ? arms!.LeftWristGlobalPosition
                : staticArms!.LeftWristFrame.GlobalPosition
            : Vector3.Zero;
        var leftPalm = palmContactsAvailable
            ? rootInverse * leftPalmGlobal
            : Vector3.Zero;
        var rightGrip = ReloadMarkerInWeaponRoot(
            useAnimatedPose ? arms?.RightGripFrame : staticArms?.RightGripFrame,
            rootInverse,
            out var rightGripAvailable);
        var markersAvailable = rightShoulderAvailable
            && leftShoulderAvailable
            && rightPalmAvailable
            && leftPalmAvailable
            && rightGripAvailable;

        var magazine = weapon?.Magazine;
        var spareMagazine = weapon?.SpareMagazine;
        var action = weapon?.ChargingHandle;
        var primaryMagazineGripGlobal = Vector3.Zero;
        var primaryMagazineGripAvailable = weapon is not null
            && weapon.TryMagazineGripGlobalPosition(
                spare: false,
                out primaryMagazineGripGlobal);
        var spareMagazineGripGlobal = Vector3.Zero;
        var spareMagazineGripAvailable = weapon is not null
            && weapon.TryMagazineGripGlobalPosition(
                spare: true,
                out spareMagazineGripGlobal);
        var actionGripGlobal = Vector3.Zero;
        var actionGripAvailable = weapon is not null
            && weapon.TryActionGripGlobalPosition(out actionGripGlobal);
        var bodyContinuity = InspectReloadBodyContinuity(
            useAnimatedPose ? arms?.Skeleton : null,
            rightPalmAvailable,
            rightPalmGlobal,
            leftPalmAvailable,
            visibleLeftPalmGlobal,
            useAnimatedPose ? arms?.Mesh : null);
        var screenContact = InspectReloadScreenContact(
            rightPalmGlobal,
            rightPalmAvailable,
            rightWristGlobal,
            palmContactsAvailable,
            visibleLeftPalmGlobal,
            leftPalmAvailable,
            leftWristGlobal,
            palmContactsAvailable,
            primaryMagazineGripGlobal,
            primaryMagazineGripAvailable,
            magazine,
            spareMagazineGripGlobal,
            spareMagazineGripAvailable,
            spareMagazine,
            actionGripGlobal,
            actionGripAvailable,
            action);
        var visibleSupportPalm = palmContactsAvailable
            ? rootInverse * visibleLeftPalmGlobal
            : Vector3.Zero;
        return new AllWeaponReloadInspection(
            platform,
            (useAnimatedPose
                ? arms is not null && IsInstanceValid(arms.Root)
                : staticArms is not null && IsInstanceValid(staticArms.Root))
                && IsInstanceValid(weaponRoot),
            false,
            _isReloading,
            _reloadStartedEmpty,
            ReloadProgress,
            clipName,
            clipExists,
            clipExists ? arms!.ClipDuration(platform, _reloadStartedEmpty) : 0.0f,
            arms?.Root.IsVisibleInTree() == true,
            arms?.Mesh.IsVisibleInTree() == true,
            staticArms?.Root.IsVisibleInTree() == true,
            IsInstanceValid(weaponRoot) && weaponRoot!.IsVisibleInTree(),
            markersAvailable,
            rightShoulder,
            leftShoulder,
            rightPalm,
            leftPalm,
            rightGripAvailable,
            rightGrip,
            pose.PrimaryGrip,
            IsInstanceValid(magazine) && magazine!.IsVisibleInTree(),
            IsInstanceValid(spareMagazine) && spareMagazine!.IsVisibleInTree(),
            IsInstanceValid(magazine)
                && IsInstanceValid(spareMagazine)
                && magazine!.GetInstanceId() != spareMagazine!.GetInstanceId(),
            HasRenderableReloadGeometry(magazine),
            HasRenderableReloadGeometry(spareMagazine),
            primaryMagazineGripAvailable,
            primaryMagazineGripAvailable
                ? rootInverse * primaryMagazineGripGlobal
                : Vector3.Zero,
            spareMagazineGripAvailable,
            spareMagazineGripAvailable
                ? rootInverse * spareMagazineGripGlobal
                : Vector3.Zero,
            IsInstanceValid(magazine)
                ? rootInverse * magazine!.GlobalTransform
                : Transform3D.Identity,
            IsInstanceValid(spareMagazine)
                ? rootInverse * spareMagazine!.GlobalTransform
                : Transform3D.Identity,
            IsInstanceValid(magazine)
                ? rootInverse * magazine!.GlobalPosition
                : Vector3.Zero,
            IsInstanceValid(spareMagazine)
                ? rootInverse * spareMagazine!.GlobalPosition
                : Vector3.Zero,
            actionGripAvailable,
            actionGripAvailable
                ? rootInverse * actionGripGlobal
                : Vector3.Zero,
            IsInstanceValid(action)
                ? rootInverse * action!.GlobalPosition
                : Vector3.Zero,
            HasRenderableReloadGeometry(action),
            0.0f,
            bodyContinuity,
            rootInverse * ReloadSupportTargetGlobal(),
            screenContact,
            visibleSupportPalm);
    }

    internal void CancelReloadForDiagnostics()
        => CancelReload();

    private AllWeaponReloadInspection InspectNativeSmgReloadForDiagnostics()
    {
        var smg = IsInstanceValid(_authoredFirstPersonSmg?.Root)
            ? _authoredFirstPersonSmg
            : null;
        var clipExists = smg?.AnimationPlayer.HasAnimation("reload") == true;
        var rootInverse = _weaponRoot.GlobalTransform.AffineInverse();
        var rightShoulder = ReloadBoneInWeaponRoot(
            smg?.Skeleton,
            "R_arm_024",
            rootInverse,
            out var rightShoulderAvailable);
        var leftShoulder = ReloadBoneInWeaponRoot(
            smg?.Skeleton,
            "L_arm_01",
            rootInverse,
            out var leftShoulderAvailable);
        var rightPalm = ReloadBoneInWeaponRoot(
            smg?.Skeleton,
            "R_palm_039",
            rootInverse,
            out var rightPalmAvailable);
        var leftPalm = ReloadBoneInWeaponRoot(
            smg?.Skeleton,
            "L_palm_015",
            rootInverse,
            out var leftPalmAvailable);
        var magazine = smg?.Magazine;
        var action = smg?.ChargingHandle;
        var rightGripFrame = smg is null
            ? null
            : CombatModelLibrary.FindOptionalNode(smg.Root, "RightGripFrame");
        var rightGrip = ReloadMarkerInWeaponRoot(
            rightGripFrame,
            rootInverse,
            out var rightGripAvailable);
        var bodyContinuity = InspectReloadBodyContinuity(
            smg?.Skeleton,
            rightPalmAvailable,
            rightPalmAvailable
                ? _weaponRoot.GlobalTransform * rightPalm
                : Vector3.Zero,
            leftPalmAvailable,
            leftPalmAvailable
                ? _weaponRoot.GlobalTransform * leftPalm
                : Vector3.Zero,
            smg?.Arms);
        return new AllWeaponReloadInspection(
            WeaponPlatform.M3A1,
            smg is not null,
            true,
            _isReloading,
            _reloadStartedEmpty,
            ReloadProgress,
            "reload",
            clipExists,
            clipExists ? smg!.ReloadAnimationDuration : 0.0f,
            smg?.Root.IsVisibleInTree() == true,
            smg?.Arms.IsVisibleInTree() == true,
            false,
            smg?.WeaponBody.IsVisibleInTree() == true,
            rightShoulderAvailable
                && leftShoulderAvailable
                && rightPalmAvailable
                && leftPalmAvailable,
            rightShoulder,
            leftShoulder,
            rightPalm,
            leftPalm,
            rightGripAvailable,
            rightGrip,
            FirstPersonArmPoseCatalog.For(WeaponPlatform.M3A1).PrimaryGrip,
            IsInstanceValid(magazine) && magazine!.IsVisibleInTree(),
            false,
            false,
            HasRenderableReloadGeometry(magazine),
            false,
            false,
            Vector3.Zero,
            false,
            Vector3.Zero,
            Transform3D.Identity,
            Transform3D.Identity,
            IsInstanceValid(magazine)
                ? rootInverse * magazine!.GlobalPosition
                : Vector3.Zero,
            Vector3.Zero,
            false,
            Vector3.Zero,
            IsInstanceValid(action)
                ? rootInverse * action!.GlobalPosition
                : Vector3.Zero,
            HasRenderableReloadGeometry(action),
            NativeSmgMagazineTrackTravel(smg),
            bodyContinuity,
            Vector3.Zero,
            default,
            leftPalm);
    }

    private ReloadScreenContactInspection InspectReloadScreenContact(
        Vector3 rightPalmGlobal,
        bool rightPalmAvailable,
        Vector3 rightWristGlobal,
        bool rightWristAvailable,
        Vector3 leftPalmGlobal,
        bool leftPalmAvailable,
        Vector3 leftWristGlobal,
        bool leftWristAvailable,
        Vector3 primaryMagazineGripGlobal,
        bool primaryMagazineGripAvailable,
        Node3D? primaryMagazine,
        Vector3 spareMagazineGripGlobal,
        bool spareMagazineGripAvailable,
        Node3D? spareMagazine,
        Vector3 actionGripGlobal,
        bool actionGripAvailable,
        Node3D? action)
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

        var screenScale = new Vector2(
            screenSize.X / logicalViewportSize.X,
            screenSize.Y / logicalViewportSize.Y);
        var rightPalmBehind = !rightPalmAvailable
            || _camera.IsPositionBehind(rightPalmGlobal);
        var rightWristBehind = !rightWristAvailable
            || _camera.IsPositionBehind(rightWristGlobal);
        var leftPalmBehind = !leftPalmAvailable
            || _camera.IsPositionBehind(leftPalmGlobal);
        var leftWristBehind = !leftWristAvailable
            || _camera.IsPositionBehind(leftWristGlobal);
        var primaryGripBehind = !primaryMagazineGripAvailable
            || _camera.IsPositionBehind(primaryMagazineGripGlobal);
        var spareGripBehind = !spareMagazineGripAvailable
            || _camera.IsPositionBehind(spareMagazineGripGlobal);
        var actionGripBehind = !actionGripAvailable
            || _camera.IsPositionBehind(actionGripGlobal);
        var rightPalmScreen = rightPalmBehind
            ? Vector2.Zero
            : _camera.UnprojectPosition(rightPalmGlobal) * screenScale;
        var rightWristScreen = rightWristBehind
            ? Vector2.Zero
            : _camera.UnprojectPosition(rightWristGlobal) * screenScale;
        var leftPalmScreen = leftPalmBehind
            ? Vector2.Zero
            : _camera.UnprojectPosition(leftPalmGlobal) * screenScale;
        var leftWristScreen = leftWristBehind
            ? Vector2.Zero
            : _camera.UnprojectPosition(leftWristGlobal) * screenScale;
        var primaryGripScreen = primaryGripBehind
            ? Vector2.Zero
            : _camera.UnprojectPosition(primaryMagazineGripGlobal) * screenScale;
        var spareGripScreen = spareGripBehind
            ? Vector2.Zero
            : _camera.UnprojectPosition(spareMagazineGripGlobal) * screenScale;
        var actionGripScreen = actionGripBehind
            ? Vector2.Zero
            : _camera.UnprojectPosition(actionGripGlobal) * screenScale;
        return new ReloadScreenContactInspection(
            screenSize,
            rightPalmAvailable,
            rightPalmBehind,
            rightPalmScreen,
            rightWristAvailable,
            rightWristBehind,
            rightWristScreen,
            leftPalmAvailable,
            leftPalmBehind,
            leftPalmScreen,
            leftWristAvailable,
            leftWristBehind,
            leftWristScreen,
            primaryMagazineGripAvailable,
            primaryGripBehind,
            primaryGripScreen,
            InspectVisibleMeshScreenProjection(
                primaryMagazine,
                logicalViewportSize,
                screenSize),
            spareMagazineGripAvailable,
            spareGripBehind,
            spareGripScreen,
            InspectVisibleMeshScreenProjection(
                spareMagazine,
                logicalViewportSize,
                screenSize),
            actionGripAvailable,
            actionGripBehind,
            actionGripScreen,
            InspectVisibleMeshScreenProjection(
                action,
                logicalViewportSize,
                screenSize));
    }

    private static Vector3 ReloadBoneInWeaponRoot(
        Skeleton3D? skeleton,
        string boneName,
        Transform3D weaponRootInverse,
        out bool available)
    {
        available = false;
        if (!IsInstanceValid(skeleton))
        {
            return Vector3.Zero;
        }

        var bone = skeleton!.FindBone(boneName);
        if (bone < 0)
        {
            return Vector3.Zero;
        }

        available = true;
        var boneGlobal = skeleton.GlobalTransform
            * skeleton.GetBoneGlobalPose(bone);
        return (weaponRootInverse * boneGlobal).Origin;
    }

    private static Vector3 ReloadMarkerInWeaponRoot(
        Node3D? marker,
        Transform3D weaponRootInverse,
        out bool available)
    {
        available = IsInstanceValid(marker);
        return available
            ? weaponRootInverse * marker!.GlobalPosition
            : Vector3.Zero;
    }

    private static float NativeSmgMagazineTrackTravel(
        AuthoredFirstPersonSmgVisual? smg)
    {
        if (smg is null || !smg.AnimationPlayer.HasAnimation("reload"))
        {
            return 0.0f;
        }

        var animation = smg.AnimationPlayer.GetAnimation("reload");
        var maximum = 0.0f;
        for (var track = 0; track < animation.GetTrackCount(); track++)
        {
            if (animation.TrackGetType(track) != Animation.TrackType.Position3D
                || !animation.TrackGetPath(track).ToString()
                    .Contains("clip", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var start = animation.PositionTrackInterpolate(track, 0.0, backward: false);
            for (var sample = 1; sample <= 32; sample++)
            {
                var time = animation.Length * sample / 32.0;
                var current = animation.PositionTrackInterpolate(
                    track,
                    time,
                    backward: false);
                maximum = Mathf.Max(maximum, start.DistanceTo(current));
            }
        }
        return maximum;
    }

    private static bool HasRenderableReloadGeometry(Node3D? root)
        => IsInstanceValid(root)
            && ((root is MeshInstance3D { Mesh: not null })
                || CombatModelLibrary.MeshesBelow(root!)
                    .Any(mesh => mesh.Mesh is not null));

}

internal readonly record struct AllWeaponReloadInspection(
    WeaponPlatform Platform,
    bool Available,
    bool NativeClip,
    bool Reloading,
    bool EmptyReload,
    float Progress,
    string ClipName,
    bool ClipExists,
    float ClipDuration,
    bool AnimatedRootActive,
    bool AnimatedMeshActive,
    bool StaticArmsActive,
    bool WeaponActive,
    bool RigMarkersAvailable,
    Vector3 RightShoulder,
    Vector3 LeftShoulder,
    Vector3 RightPalm,
    Vector3 LeftPalm,
    bool RightGripAvailable,
    Vector3 RightGrip,
    Vector3 PrimaryGrip,
    bool PrimaryMagazineVisible,
    bool SpareMagazineVisible,
    bool SeparateMagazineNodes,
    bool PrimaryMagazineGeometry,
    bool SpareMagazineGeometry,
    bool PrimaryMagazineGripAvailable,
    Vector3 PrimaryMagazineGrip,
    bool SpareMagazineGripAvailable,
    Vector3 SpareMagazineGrip,
    Transform3D PrimaryMagazineTransform,
    Transform3D SpareMagazineTransform,
    Vector3 PrimaryMagazinePosition,
    Vector3 SpareMagazinePosition,
    bool ActionGripAvailable,
    Vector3 ActionGrip,
    Vector3 ActionPosition,
    bool ActionGeometry,
    float NativeMagazineTravel,
    ReloadBodyContinuityInspection BodyContinuity,
    Vector3 SupportTarget,
    ReloadScreenContactInspection ScreenContact,
    Vector3 VisibleSupportPalm);

internal readonly record struct ReloadScreenContactInspection(
    Vector2 ScreenSize,
    bool RightPalmAvailable,
    bool RightPalmBehindCamera,
    Vector2 RightPalmScreen,
    bool RightWristAvailable,
    bool RightWristBehindCamera,
    Vector2 RightWristScreen,
    bool LeftPalmAvailable,
    bool LeftPalmBehindCamera,
    Vector2 LeftPalmScreen,
    bool LeftWristAvailable,
    bool LeftWristBehindCamera,
    Vector2 LeftWristScreen,
    bool PrimaryMagazineGripAvailable,
    bool PrimaryMagazineGripBehindCamera,
    Vector2 PrimaryMagazineGripScreen,
    VisibleMeshScreenProjection PrimaryMagazineScreen,
    bool SpareMagazineGripAvailable,
    bool SpareMagazineGripBehindCamera,
    Vector2 SpareMagazineGripScreen,
    VisibleMeshScreenProjection SpareMagazineScreen,
    bool ActionGripAvailable,
    bool ActionGripBehindCamera,
    Vector2 ActionGripScreen,
    VisibleMeshScreenProjection ActionScreen)
{
    private const float ReadableTopRatio = 0.20f;
    // A fully visible glove may place its authored palm centre a few pixels
    // below the old 85% line while the magazine and sleeve remain on-screen.
    private const float ReadableBottomRatio = 0.86f;
    private const float ReadableSideMarginRatio = 0.04f;

    public float LeftPalmYRatio
        => ScreenSize.Y > 0.0f ? LeftPalmScreen.Y / ScreenSize.Y : 1.0f;

    public float PrimaryMagazineGripYRatio
        => ScreenSize.Y > 0.0f
            ? PrimaryMagazineGripScreen.Y / ScreenSize.Y
            : 1.0f;

    public float SpareMagazineGripYRatio
        => ScreenSize.Y > 0.0f ? SpareMagazineGripScreen.Y / ScreenSize.Y : 1.0f;

    public float ActionGripYRatio
        => ScreenSize.Y > 0.0f ? ActionGripScreen.Y / ScreenSize.Y : 1.0f;

    public bool ExtractionReadable
        => ScreenSize.X > 0.0f
            && ScreenSize.Y > 0.0f
            && LeftPalmAvailable
            && PrimaryMagazineGripAvailable
            && !LeftPalmBehindCamera
            && !PrimaryMagazineGripBehindCamera
            && PointInsideReadableFrame(LeftPalmScreen)
            && MeshReadableAtGrip(
                PrimaryMagazineScreen,
                PrimaryMagazineGripScreen,
                0.012f,
                0.00030f);

    public bool InsertionReadable
        => ScreenSize.X > 0.0f
            && ScreenSize.Y > 0.0f
            && LeftPalmAvailable
            && SpareMagazineGripAvailable
            && !LeftPalmBehindCamera
            && !SpareMagazineGripBehindCamera
            && PointInsideReadableFrame(LeftPalmScreen)
            && MeshReadableAtGrip(
                SpareMagazineScreen,
                SpareMagazineGripScreen,
                0.012f,
                0.00030f);

    public bool ActionReadable
        => ScreenSize.X > 0.0f
            && ScreenSize.Y > 0.0f
            && LeftPalmAvailable
            && ActionGripAvailable
            && !LeftPalmBehindCamera
            && !ActionGripBehindCamera
            && PointInsideReadableFrame(LeftPalmScreen)
            && MeshReadableAtGrip(
                ActionScreen,
                ActionGripScreen,
                0.006f,
                0.00008f);

    private bool MeshReadableAtGrip(
        VisibleMeshScreenProjection mesh,
        Vector2 grip,
        float minimumDimensionRatio,
        float minimumAreaRatio)
    {
        if (!mesh.Available)
        {
            return false;
        }
        var center = mesh.Bounds.Position + mesh.Bounds.Size * 0.5f;
        var minimumDimension = ScreenSize.Y * minimumDimensionRatio;
        var minimumArea = ScreenSize.Y * ScreenSize.Y * minimumAreaRatio;
        var padding = Mathf.Max(
            ScreenSize.Y * 0.012f,
            Mathf.Max(mesh.Bounds.Size.X, mesh.Bounds.Size.Y) * 0.35f);
        return mesh.Bounds.Size.X >= minimumDimension
            && mesh.Bounds.Size.Y >= minimumDimension
            && mesh.Bounds.Size.X * mesh.Bounds.Size.Y >= minimumArea
            && PointInsideReadableFrame(center)
            && mesh.Bounds.Grow(padding).HasPoint(grip);
    }

    private bool PointInsideReadableFrame(Vector2 point)
        => point.X >= ScreenSize.X * ReadableSideMarginRatio
            && point.X <= ScreenSize.X * (1.0f - ReadableSideMarginRatio)
            && point.Y >= ScreenSize.Y * ReadableTopRatio
            && point.Y <= ScreenSize.Y * ReadableBottomRatio;
}

internal readonly record struct ReloadArmScreenChainInspection(
    bool Available,
    Vector2 ShoulderScreen,
    Vector2 ElbowScreen,
    Vector2 WristScreen,
    Vector2 PalmScreen,
    bool ShoulderBehindCamera,
    bool ElbowBehindCamera,
    bool WristBehindCamera,
    bool PalmBehindCamera,
    float UpperArmLength,
    float ForearmLength,
    float WristPalmLength,
    float UpperArmRestLength,
    float ForearmRestLength,
    float WristPalmRestLength,
    bool ParentChainValid,
    bool BodyEdgeConnected,
    Vector2 BodyEdgeScreen);

internal readonly record struct ReloadBodyContinuityInspection(
    Vector2 ScreenSize,
    Vector2 RightShoulderScreen,
    Vector2 LeftShoulderScreen,
    bool RightShoulderBehindCamera,
    bool LeftShoulderBehindCamera,
    VisibleMeshScreenProjection AnimatedMeshScreen,
    bool AnimatedMeshUsesSkeleton,
    bool AnimatedMeshUsesForearmSkeleton,
    ReloadArmScreenChainInspection RightArm,
    ReloadArmScreenChainInspection LeftArm)
{
    public float MinimumShoulderYRatio
    {
        get
        {
            if (ScreenSize.Y <= 0.0f)
            {
                return 0.0f;
            }
            var right = RightShoulderBehindCamera
                ? 1.0f
                : RightShoulderScreen.Y / ScreenSize.Y;
            var left = LeftShoulderBehindCamera
                ? 1.0f
                : LeftShoulderScreen.Y / ScreenSize.Y;
            return Mathf.Min(right, left);
        }
    }

    public float AnimatedMeshTopRatio
        => ScreenSize.Y > 0.0f && AnimatedMeshScreen.Available
            ? AnimatedMeshScreen.Bounds.Position.Y / ScreenSize.Y
            : 1.0f;

    public float RightShoulderXRatio
        => ShoulderXRatio(RightShoulderScreen, RightShoulderBehindCamera);

    public float LeftShoulderXRatio
        => ShoulderXRatio(LeftShoulderScreen, LeftShoulderBehindCamera);

    private float ShoulderXRatio(Vector2 shoulder, bool behindCamera)
        => ScreenSize.X <= 0.0f
            ? 0.0f
            : behindCamera ? -1.0f : shoulder.X / ScreenSize.X;
}
