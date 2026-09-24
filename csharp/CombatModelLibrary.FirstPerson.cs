using System;
using System.Linq;
using Godot;

namespace OperationSteelTide;

internal sealed class AuthoredFirstPersonSmgVisual
{
    private const string ReloadAnimationName = "reload";
    private float _lastReloadProgress = float.NaN;
    public AuthoredFirstPersonSmgVisual(Node3D root)
    {
        Root = root;
        Arms = CombatModelLibrary.RequireNodeEnding(root, "AuthoredArms");
        WeaponBody = CombatModelLibrary.RequireNodeEnding(root, "WeaponBody");
        Magazine = CombatModelLibrary.RequireNodeEnding(root, "MagazineGeometry");
        ChargingHandle = CombatModelLibrary.RequireNodeEnding(root, "ChargingHandleGeometry");
        Muzzle = CombatModelLibrary.RequireNodeEnding(root, "Muzzle");
        AnimationPlayer = CombatModelLibrary.RequireAnimationPlayer(root);
        Skeleton = CombatModelLibrary.RequireSkeleton(root);
        if (!AnimationPlayer.HasAnimation(ReloadAnimationName)
            || Skeleton.FindBone("L_wrist_03") < 0)
        {
            throw new InvalidOperationException("Authored SMG-45 is missing its reload animation rig.");
        }
        SetReloadProgress(0.0f, forceRefresh: true);
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
        => BoundsSizeInRoot(Arms);

    public Vector3 WeaponBoundsSizeInRoot()
        => BoundsSizeInRoot(WeaponBody);

    private Vector3 BoundsSizeInRoot(Node3D subtree)
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
        AccumulateNodeBounds(Root, Transform3D.Identity, insideSubtree: false);

        return hasBounds ? maximum - minimum : Vector3.Zero;

        void AccumulateNodeBounds(
            Node3D node,
            Transform3D parentTransform,
            bool insideSubtree)
        {
            var transform = ReferenceEquals(node, Root)
                ? parentTransform
                : parentTransform * node.Transform;
            insideSubtree |= ReferenceEquals(node, subtree);
            if (insideSubtree && node is MeshInstance3D { Mesh: not null } mesh)
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
                    AccumulateNodeBounds(child3D, transform, insideSubtree);
                }
            }
        }
    }

    public void SyncMechanisms()
    {
        if (!Magazine.Visible)
        {
            Magazine.Visible = true;
        }
        if (!ChargingHandle.Visible)
        {
            ChargingHandle.Visible = true;
        }
    }

    public void SetReloadProgress(float progress, bool forceRefresh = false)
    {
        var normalized = Mathf.Clamp(progress, 0.0f, 1.0f);
        if (!forceRefresh && Mathf.IsEqualApprox(_lastReloadProgress, normalized))
        {
            return;
        }

        AnimationPlayer.Play(ReloadAnimationName, 0.0);
        AnimationPlayer.Seek(
            ReloadAnimationDuration * normalized,
            update: true);
        AnimationPlayer.Pause();
        _lastReloadProgress = normalized;
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
            ArmBoundsSizeInRoot(),
            WeaponBoundsSizeInRoot());
    }
}

internal sealed class AuthoredFirstPersonArmsVisual
{
    public AuthoredFirstPersonArmsVisual(Node3D root)
    {
        Root = root;
        RightArm = CombatModelLibrary.RequireNodeEnding(root, "RightArm");
        LeftArm = CombatModelLibrary.RequireNodeEnding(root, "LeftArm");
        RightPalmFrame = CombatModelLibrary.RequireNodeEnding(root, "RightPalmFrame");
        LeftPalmFrame = CombatModelLibrary.RequireNodeEnding(root, "LeftPalmFrame");
        RightWristFrame = CombatModelLibrary.RequireNodeEnding(root, "RightWristFrame");
        LeftWristFrame = CombatModelLibrary.RequireNodeEnding(root, "LeftWristFrame");
        RightGripFrame = CombatModelLibrary.RequireNodeEnding(root, "RightGripFrame");
        LeftGripFrame = CombatModelLibrary.RequireNodeEnding(root, "LeftGripFrame");
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
    Vector3 ArmBoundsSize,
    Vector3 WeaponBoundsSize);

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
    internal const string OperatorHandKitsScenePath =
        "res://assets/models/djmaesen_smg45/operator_hand_kits.glb";

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

    public static AuthoredFirstPersonSmgVisual InstantiateFirstPersonSmg45(OperatorRole role = OperatorRole.Assault)
    {
        var root = InstantiateRoleFamily(role, "Smg", "AuthoredSMG45FirstPersonVisual");
        foreach (var geometry in GeometryBelow(root))
        {
            geometry.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        }
        return new AuthoredFirstPersonSmgVisual(root);
    }

    public static AuthoredFirstPersonArmsVisual InstantiateFirstPersonRifleArms(OperatorRole role = OperatorRole.Assault)
        => InstantiateStaticFirstPersonArms(
            "Rifle",
            "AuthoredFirstPersonRifleArmsVisual", role);

    public static AuthoredFirstPersonArmsVisual InstantiateFirstPersonPistolServiceArms(OperatorRole role = OperatorRole.Assault)
        => InstantiateStaticFirstPersonArms(
            "PistolService",
            "AuthoredFirstPersonPistolServiceArmsVisual", role);

    public static AuthoredFirstPersonArmsVisual InstantiateFirstPersonPistolLargeArms(OperatorRole role = OperatorRole.Assault)
        => InstantiateStaticFirstPersonArms(
            "PistolLarge",
            "AuthoredFirstPersonPistolLargeArmsVisual", role);

    internal static Node3D InstantiateFirstPersonLadderHands(OperatorRole role)
        => InstantiateRoleFamily(role, "Ladder", "AuthoredFirstPersonLadderHandsVisual");

    private static AuthoredFirstPersonArmsVisual InstantiateStaticFirstPersonArms(
        string family,
        string runtimeName, OperatorRole role)
    {
        var root = InstantiateRoleFamily(role, family, runtimeName);
        foreach (var geometry in GeometryBelow(root))
        {
            geometry.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        }
        return new AuthoredFirstPersonArmsVisual(root);
    }

    internal static Node3D RequireNodeEnding(Node3D root, string ending)
    {
        var node = FindNodeEnding(root, ending);
        return node ?? throw new InvalidOperationException(
            $"Combat model {root.Name} is missing required node ending {ending}.");
    }

    private static Node3D InstantiateRoleFamily(
        OperatorRole role,
        string family,
        string runtimeName)
    {
        var scene = GD.Load<PackedScene>(OperatorHandKitsScenePath)
            ?? throw new InvalidOperationException(
                $"Required operator hand kit is missing: {OperatorHandKitsScenePath}");
        var kitRoot = scene.Instantiate<Node3D>();
        var roleRoot = kitRoot.FindChild(RoleKitName(role), recursive: true, owned: false) as Node3D
            ?? throw new InvalidOperationException(
                $"Operator hand kit is missing role subtree {role}.");
        if (family == "Smg")
        {
            foreach (var visualName in new[] { "Viper", "Heron", "Lynx", "Magpie", "Jackal" })
            {
                if (kitRoot.FindChild(visualName, recursive: true, owned: false)
                    is Node3D visualRoot)
                {
                    var selected = string.Equals(
                        visualName,
                        RoleKitName(role),
                        StringComparison.Ordinal);
                    visualRoot.Visible = selected;
                    if (!selected)
                    {
                        // Keep the shared AnimationPlayer at the GLB root so
                        // reload track paths remain valid, while preventing
                        // inactive role meshes from affecting bounds/cameras.
                        visualRoot.Scale = Vector3.Zero;
                    }
                }
            }
            kitRoot.Name = runtimeName;
            FirstPersonHandAppearance.Apply(kitRoot, role);
            return kitRoot;
        }
        var familyRoot = FindNodeEnding(roleRoot, family)
            ?? throw new InvalidOperationException(
                $"Operator hand kit {role} is missing family subtree {family}.");
        var extracted = new Node3D
        {
            Name = runtimeName
        };
        familyRoot.Owner = null;
        familyRoot.GetParent()?.RemoveChild(familyRoot);
        extracted.AddChild(familyRoot);
        kitRoot.Free();
        FirstPersonHandAppearance.Apply(extracted, role);
        return extracted;
    }

    private static string RoleKitName(OperatorRole role)
        => role switch
        {
            OperatorRole.Assault => "Viper",
            OperatorRole.Medic => "Heron",
            OperatorRole.Recon => "Lynx",
            OperatorRole.Scavenger => "Magpie",
            OperatorRole.Locksmith => "Jackal",
            _ => "Viper"
        };

    private static Node3D? FindNodeEnding(Node root, string ending)
    {
        if (root is Node3D node
            && node.Visible
            && IsNodeNameEnding(node.Name.ToString(), ending))
        {
            return node;
        }
        if (root is Node3D hidden && !hidden.Visible)
        {
            return null;
        }
        var children = root.GetChildren();
        using var childrenBacking = children.AsDisposable();
        foreach (var child in children)
        {
            if (FindNodeEnding(child, ending) is { } match)
            {
                return match;
            }
        }
        return null;
    }

    private static bool IsNodeNameEnding(string name, string ending)
    {
        if (name.EndsWith(ending, StringComparison.Ordinal))
        {
            return true;
        }
        var suffixStart = name.LastIndexOf(ending + ".", StringComparison.Ordinal);
        if (suffixStart < 0)
        {
            suffixStart = name.LastIndexOf(ending + "_", StringComparison.Ordinal);
            if (suffixStart < 0)
            {
                return false;
            }
        }
        var numericSuffix = name[(suffixStart + ending.Length + 1)..];
        return numericSuffix.Length > 0 && numericSuffix.All(char.IsDigit);
    }

    public static CombatModelInspection InspectFirstPersonSmg45()
    {
        Node3D? root = null;
        try
        {
            root = InstantiateFirstPersonSmg45().Root;
            var inspectedRoot = FindNodeEnding(root, "Smg") ?? root;
            var bounds = ComputeBounds(inspectedRoot);
            return new CombatModelInspection(
                true,
                true,
                bounds.MeshCount,
                CountMaterials(inspectedRoot),
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
                Vector3.Zero,
                Vector3.Zero);
        }
        finally
        {
            visual?.Root.Free();
        }
    }
}
