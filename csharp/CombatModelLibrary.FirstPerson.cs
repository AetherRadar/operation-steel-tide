using System;
using Godot;

namespace OperationSteelTide;

internal sealed class AuthoredFirstPersonSmgVisual
{
    private const string ReloadAnimationName = "reload";
    public AuthoredFirstPersonSmgVisual(Node3D root)
    {
        Root = root;
        Arms = CombatModelLibrary.RequireNode(root, "AuthoredArms");
        WeaponBody = CombatModelLibrary.RequireNode(root, "WeaponBody");
        Magazine = CombatModelLibrary.RequireNode(root, "MagazineGeometry");
        ChargingHandle = CombatModelLibrary.RequireNode(root, "ChargingHandleGeometry");
        Muzzle = CombatModelLibrary.RequireNode(root, "Muzzle");
        AnimationPlayer = CombatModelLibrary.RequireAnimationPlayer(root);
        Skeleton = CombatModelLibrary.RequireSkeleton(root);
        if (!AnimationPlayer.HasAnimation(ReloadAnimationName)
            || Skeleton.FindBone("L_wrist_03") < 0)
        {
            throw new InvalidOperationException("Authored SMG-45 is missing its reload animation rig.");
        }
        SetReloadProgress(0.0f);
    }

    public Node3D Root { get; }
    public Node3D Arms { get; }
    public Node3D WeaponBody { get; }
    public Node3D Magazine { get; }
    public Node3D ChargingHandle { get; }
    public Node3D Muzzle { get; }
    public AnimationPlayer AnimationPlayer { get; }
    public Skeleton3D Skeleton { get; }
    public float ReloadAnimationDuration
        => (float)AnimationPlayer.GetAnimation(ReloadAnimationName).Length;

    public Vector3 ArmBoundsSizeInRoot()
    {
        var minimum = new Vector3(
            float.PositiveInfinity,
            float.PositiveInfinity,
            float.PositiveInfinity);
        var maximum = new Vector3(
            float.NegativeInfinity,
            float.NegativeInfinity,
            float.NegativeInfinity);
        var hasBounds = false;
        AccumulateNodeBounds(Root, Transform3D.Identity, insideArms: false);

        return hasBounds ? maximum - minimum : Vector3.Zero;

        void AccumulateNodeBounds(
            Node3D node,
            Transform3D parentTransform,
            bool insideArms)
        {
            var transform = ReferenceEquals(node, Root)
                ? parentTransform
                : parentTransform * node.Transform;
            insideArms |= ReferenceEquals(node, Arms);
            if (insideArms && node is MeshInstance3D { Mesh: not null } mesh)
            {
                var bounds = mesh.Mesh.GetAabb();
                for (var x = 0; x <= 1; x++)
                {
                    for (var y = 0; y <= 1; y++)
                    {
                        for (var z = 0; z <= 1; z++)
                        {
                            var local = bounds.Position + new Vector3(
                                bounds.Size.X * x,
                                bounds.Size.Y * y,
                                bounds.Size.Z * z);
                            var point = transform * local;
                            minimum = minimum.Min(point);
                            maximum = maximum.Max(point);
                            hasBounds = true;
                        }
                    }
                }
            }
            var children = node.GetChildren();
            using var childrenBacking = children.AsDisposable();
            foreach (var child in children)
            {
                if (child is Node3D child3D)
                {
                    AccumulateNodeBounds(child3D, transform, insideArms);
                }
            }
        }
    }

    public void SyncMechanisms()
    {
        Magazine.Visible = true;
        ChargingHandle.Visible = true;
    }

    public void SetReloadProgress(float progress)
    {
        AnimationPlayer.Play(ReloadAnimationName, 0.0);
        AnimationPlayer.Seek(
            ReloadAnimationDuration * Mathf.Clamp(progress, 0.0f, 1.0f),
            update: true);
        AnimationPlayer.Pause();
    }

    public FirstPersonSmgReloadInspection InspectReloadAnimation()
    {
        var animation = AnimationPlayer.GetAnimation(ReloadAnimationName);
        var sampleTime = ReloadAnimationDuration * 0.46f;
        var supportArmRotation = 0.0f;
        var magazineTravel = 0.0f;
        for (var track = 0; track < animation.GetTrackCount(); track++)
        {
            var path = animation.TrackGetPath(track).ToString();
            if (animation.TrackGetType(track) == Animation.TrackType.Rotation3D
                && (path.Contains("L_arm_01", StringComparison.Ordinal)
                    || path.Contains("L_elbow_02", StringComparison.Ordinal)
                    || path.Contains("L_wrist_03", StringComparison.Ordinal)))
            {
                var idle = animation.RotationTrackInterpolate(track, 0.0, backward: false);
                var reload = animation.RotationTrackInterpolate(track, sampleTime, backward: false);
                supportArmRotation = Mathf.Max(supportArmRotation, idle.AngleTo(reload));
            }
            if (animation.TrackGetType(track) == Animation.TrackType.Position3D
                && path.Contains("clip", StringComparison.OrdinalIgnoreCase))
            {
                var idle = animation.PositionTrackInterpolate(track, 0.0, backward: false);
                var reload = animation.PositionTrackInterpolate(track, sampleTime, backward: false);
                magazineTravel = Mathf.Max(magazineTravel, idle.DistanceTo(reload));
            }
        }
        SetReloadProgress(0.0f);
        return new FirstPersonSmgReloadInspection(
            true,
            ReloadAnimationDuration,
            supportArmRotation,
            magazineTravel,
            ArmBoundsSizeInRoot());
    }
}

internal sealed class AuthoredFirstPersonArmsVisual
{
    public AuthoredFirstPersonArmsVisual(Node3D root)
    {
        Root = root;
        RightArm = CombatModelLibrary.RequireNode(root, "RightArm");
        LeftArm = CombatModelLibrary.RequireNode(root, "LeftArm");
        RightPalmFrame = CombatModelLibrary.RequireNode(root, "RightPalmFrame");
        LeftPalmFrame = CombatModelLibrary.RequireNode(root, "LeftPalmFrame");
        RightWristFrame = CombatModelLibrary.RequireNode(root, "RightWristFrame");
        LeftWristFrame = CombatModelLibrary.RequireNode(root, "LeftWristFrame");
        RightGripFrame = CombatModelLibrary.RequireNode(root, "RightGripFrame");
        LeftGripFrame = CombatModelLibrary.RequireNode(root, "LeftGripFrame");
    }

    public Node3D Root { get; }
    public Node3D RightArm { get; }
    public Node3D LeftArm { get; }
    public Node3D RightPalmFrame { get; }
    public Node3D LeftPalmFrame { get; }
    public Node3D RightWristFrame { get; }
    public Node3D LeftWristFrame { get; }
    public Node3D RightGripFrame { get; }
    public Node3D LeftGripFrame { get; }

    public Transform3D MarkerTransformInRoot(Node3D marker)
        => Root.GlobalTransform.AffineInverse() * marker.GlobalTransform;

    public Transform3D RightPalmTransformInRoot
        => MarkerTransformInRoot(RightPalmFrame);

    public Transform3D LeftPalmTransformInRoot
        => MarkerTransformInRoot(LeftPalmFrame);

    public Transform3D RightGripTransformInRoot
        => MarkerTransformInRoot(RightGripFrame);

    public Vector3 PalmPosition(string boneName)
        => boneName.StartsWith("L_", StringComparison.Ordinal)
            ? LeftPalmFrame.GlobalPosition
            : RightPalmFrame.GlobalPosition;
}

internal readonly record struct FirstPersonSmgReloadInspection(
    bool Loaded,
    float Duration,
    float SupportArmRotation,
    float MagazineTravel,
    Vector3 ArmBoundsSize);

internal static partial class CombatModelLibrary
{
    internal const string Smg45FirstPersonScenePath =
        "res://assets/models/djmaesen_smg45/smg45_first_person.glb";
    internal const string Smg45WeaponScenePath =
        "res://assets/models/djmaesen_smg45/smg45_weapon.glb";
    internal const string Smg45RifleArmsScenePath =
        "res://assets/models/djmaesen_smg45/smg45_rifle_arms.glb";
    internal const string Smg45PistolServiceArmsScenePath =
        "res://assets/models/djmaesen_smg45/smg45_pistol_service_arms.glb";
    internal const string Smg45PistolLargeArmsScenePath =
        "res://assets/models/djmaesen_smg45/smg45_pistol_large_arms.glb";

    private static readonly string[] Smg45FirstPersonNodes =
    {
        "DJMaesenSMG45FirstPerson", "AuthoredArms", "WeaponBody",
        "MagazineGeometry", "ChargingHandleGeometry", "Muzzle"
    };

    private static readonly string[] StaticFirstPersonArmsNodes =
    {
        "RightArm", "LeftArm", "RightArmMesh", "LeftArmMesh",
        "RightPalmFrame", "LeftPalmFrame", "RightWristFrame", "LeftWristFrame",
        "RightGripFrame", "LeftGripFrame"
    };

    public static AuthoredFirstPersonSmgVisual InstantiateFirstPersonSmg45()
    {
        var root = InstantiateRequired(Smg45FirstPersonScenePath, Smg45FirstPersonNodes);
        root.Name = "AuthoredSMG45FirstPersonVisual";
        foreach (var geometry in GeometryBelow(root))
        {
            geometry.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        }
        return new AuthoredFirstPersonSmgVisual(root);
    }

    public static AuthoredFirstPersonArmsVisual InstantiateFirstPersonRifleArms()
        => InstantiateStaticFirstPersonArms(
            Smg45RifleArmsScenePath,
            "AuthoredFirstPersonRifleArmsVisual");

    public static AuthoredFirstPersonArmsVisual InstantiateFirstPersonPistolServiceArms()
        => InstantiateStaticFirstPersonArms(
            Smg45PistolServiceArmsScenePath,
            "AuthoredFirstPersonPistolServiceArmsVisual");

    public static AuthoredFirstPersonArmsVisual InstantiateFirstPersonPistolLargeArms()
        => InstantiateStaticFirstPersonArms(
            Smg45PistolLargeArmsScenePath,
            "AuthoredFirstPersonPistolLargeArmsVisual");

    private static AuthoredFirstPersonArmsVisual InstantiateStaticFirstPersonArms(
        string scenePath,
        string runtimeName)
    {
        var root = InstantiateRequired(scenePath, StaticFirstPersonArmsNodes);
        root.Name = runtimeName;
        foreach (var geometry in GeometryBelow(root))
        {
            geometry.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        }
        return new AuthoredFirstPersonArmsVisual(root);
    }

    public static CombatModelInspection InspectFirstPersonSmg45()
    {
        Node3D? root = null;
        try
        {
            root = InstantiateFirstPersonSmg45().Root;
            var bounds = ComputeBounds(root);
            return new CombatModelInspection(
                true,
                true,
                bounds.MeshCount,
                CountMaterials(root),
                bounds.Size);
        }
        catch (Exception)
        {
            return new CombatModelInspection(false, false, 0, 0, Vector3.Zero);
        }
        finally
        {
            root?.Free();
        }
    }

    public static FirstPersonSmgReloadInspection InspectFirstPersonSmg45Reload()
    {
        AuthoredFirstPersonSmgVisual? visual = null;
        try
        {
            visual = InstantiateFirstPersonSmg45();
            return visual.InspectReloadAnimation();
        }
        catch (Exception)
        {
            return new FirstPersonSmgReloadInspection(
                false,
                0.0f,
                0.0f,
                0.0f,
                Vector3.Zero);
        }
        finally
        {
            visual?.Root.Free();
        }
    }
}
