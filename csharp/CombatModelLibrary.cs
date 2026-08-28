using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OperationSteelTide;

// File-size retention: this legacy compatibility facade still owns stable model
// node contracts used by every weapon platform, so extracting it inside an asset
// replacement would unnecessarily widen the migration risk. Follow-up: move
// AuthoredWeaponVisual and the inspection records/helpers into
// AuthoredWeaponVisual.cs and CombatModelInspection.cs, preserving this API and
// the --validate-combat-models contract.

internal sealed class AuthoredWeaponVisual
{
    private readonly MechanismTransforms _authoredMechanismRest;
    private MechanismTransforms? _sourceMechanismRest;
    private readonly Node3D? _legacyRearIronSightPrimary;
    private readonly Node3D? _legacyRearIronSightSecondary;

    public AuthoredWeaponVisual(Node3D root, WeaponPlatform platform)
    {
        Root = root;
        Platform = platform;
        Magazine = CombatModelLibrary.RequireNode(root, "Magazine");
        SpareMagazine = CombatModelLibrary.RequireNode(root, "SpareMagazine");
        ChargingHandle = CombatModelLibrary.RequireNode(root, "ChargingHandle");
        Stock = CombatModelLibrary.RequireNode(root, "Stock");
        Foregrip = CombatModelLibrary.RequireNode(root, "Foregrip");
        MuzzleDevice = CombatModelLibrary.RequireNode(root, "MuzzleDevice");
        Suppressor = CombatModelLibrary.RequireNode(root, "Suppressor");
        OpticMount = CombatModelLibrary.RequireNode(root, "OpticMount");
        RearIronSight = CombatModelLibrary.FindOptionalNode(root, "RearIronSight");
        _legacyRearIronSightPrimary = CombatModelLibrary.FindOptionalNode(
            root,
            "M4A1Body_05_Sight");
        _legacyRearIronSightSecondary = CombatModelLibrary.FindOptionalNode(
            root,
            "M4A1Body_06_Sight_2");
        FrontIronSight = CombatModelLibrary.FindOptionalNode(root, "FrontIronSight");
        MuzzleDeviceTip = CombatModelLibrary.RequireNode(root, "MuzzleDeviceTip");
        SuppressorTip = CombatModelLibrary.RequireNode(root, "SuppressorTip");
        OpticReticleAnchor = CombatModelLibrary.RequireNode(root, "OpticReticleAnchor");
        _authoredMechanismRest = CaptureMechanismTransforms(
            Magazine,
            SpareMagazine,
            ChargingHandle);
    }

    public Node3D Root { get; }
    public WeaponPlatform Platform { get; }
    public Node3D Magazine { get; }
    public Node3D SpareMagazine { get; }
    public Node3D ChargingHandle { get; }
    public Node3D Stock { get; }
    public Node3D Foregrip { get; }
    public Node3D MuzzleDevice { get; }
    public Node3D Suppressor { get; }
    public Node3D OpticMount { get; }
    public Node3D? RearIronSight { get; }
    public Node3D? FrontIronSight { get; }
    public Node3D MuzzleDeviceTip { get; }
    public Node3D SuppressorTip { get; }
    public Node3D OpticReticleAnchor { get; }
    public Node3D ActiveMuzzleTip => Suppressor.Visible ? SuppressorTip : MuzzleDeviceTip;
    public VssIntegratedScopeInspection IntegratedOpticInspection
        => Platform == WeaponPlatform.VSS
            ? CombatModelLibrary.InspectVssIntegratedScope(Root, OpticReticleAnchor)
            : default;
    public bool IntegratedOpticPresentationValid
        => Platform != WeaponPlatform.VSS || IntegratedOpticInspection.Valid;

    public void Configure(WeaponBuild build)
    {
        var suppressed = build.Attachments.TryGetValue(AttachmentSlot.Muzzle, out var muzzleId)
            && muzzleId == "muzzle_suppressor";
        var hasForegrip = build.Attachments.ContainsKey(AttachmentSlot.Grip);
        MuzzleDevice.Visible = !suppressed;
        Suppressor.Visible = suppressed;
        Foregrip.Visible = hasForegrip;
        var hasOptic = build.Attachments.TryGetValue(
            AttachmentSlot.Optic,
            out var opticId);
        var hasDedicatedIronVisibility = GodotObject.IsInstanceValid(RearIronSight)
            && GodotObject.IsInstanceValid(FrontIronSight);
        var usesIntegratedOptic = hasOptic
            && (!hasDedicatedIronVisibility || opticId == "optic_micro");
        OpticMount.Visible = usesIntegratedOptic;
        // Preserve the authored iron sights for the bare rifle, but fold them out
        // of the optical sight picture. Both assemblies remain in the GLB and are
        // restored when the optic is removed.
        if (GodotObject.IsInstanceValid(RearIronSight))
        {
            RearIronSight!.Visible = !hasOptic;
        }
        else
        {
            CombatModelLibrary.SetOptionalVisibility(_legacyRearIronSightPrimary, !hasOptic);
            CombatModelLibrary.SetOptionalVisibility(_legacyRearIronSightSecondary, !hasOptic);
        }
        CombatModelLibrary.SetOptionalVisibility(FrontIronSight, !hasOptic);
    }

    public void SyncMechanisms(Node3D magazine, Node3D spareMagazine, Node3D chargingHandle)
    {
        var sourceTransforms = CaptureMechanismTransforms(
            magazine,
            spareMagazine,
            chargingHandle);
        if (_sourceMechanismRest is not { } sourceRest)
        {
            _sourceMechanismRest = sourceTransforms;
            CopyMechanismVisibility(magazine, spareMagazine, chargingHandle);
            return;
        }

        // Transfer only the procedural root-space delta onto each DCC-authored rest pose.
        Magazine.Transform = ApplyRootSpaceDelta(
            sourceTransforms.Magazine,
            sourceRest.Magazine,
            _authoredMechanismRest.Magazine);
        SpareMagazine.Transform = ApplyRootSpaceDelta(
            sourceTransforms.SpareMagazine,
            sourceRest.SpareMagazine,
            _authoredMechanismRest.SpareMagazine);
        ChargingHandle.Transform = ApplyRootSpaceDelta(
            sourceTransforms.ChargingHandle,
            sourceRest.ChargingHandle,
            _authoredMechanismRest.ChargingHandle);
        CopyMechanismVisibility(magazine, spareMagazine, chargingHandle);
    }

    public void SyncMechanismState(Node3D magazine, Node3D spareMagazine, Node3D chargingHandle)
    {
        Magazine.Visible = magazine.Visible;
        SpareMagazine.Visible = spareMagazine.Visible;
        var reloadOffset = chargingHandle.Position.Z + 0.05f;
        ChargingHandle.Position = _authoredMechanismRest.ChargingHandle.Origin
            + new Vector3(0.0f, 0.0f, reloadOffset);
    }

    private void CopyMechanismVisibility(
        Node3D magazine,
        Node3D spareMagazine,
        Node3D chargingHandle)
    {
        Magazine.Visible = magazine.Visible;
        SpareMagazine.Visible = spareMagazine.Visible;
        ChargingHandle.Visible = chargingHandle.Visible;
    }

    private static MechanismTransforms CaptureMechanismTransforms(
        Node3D magazine,
        Node3D spareMagazine,
        Node3D chargingHandle)
        => new(
            magazine.Transform,
            spareMagazine.Transform,
            chargingHandle.Transform);

    private static Transform3D ApplyRootSpaceDelta(
        Transform3D sourceTransform,
        Transform3D sourceRest,
        Transform3D authoredRest)
        => sourceTransform * sourceRest.AffineInverse() * authoredRest;

    private readonly record struct MechanismTransforms(
        Transform3D Magazine,
        Transform3D SpareMagazine,
        Transform3D ChargingHandle);
}

internal sealed class AuthoredGsh18Visual
{
    public AuthoredGsh18Visual(Node3D root)
    {
        Root = root;
    }

    public Node3D Root { get; }
}

internal sealed class AuthoredDesertEagleVisual
{
    public AuthoredDesertEagleVisual(Node3D root)
    {
        Root = root;
    }

    public Node3D Root { get; }
}

internal sealed class AuthoredPreviewOperatorVisual
{
    public AuthoredPreviewOperatorVisual(
        Node3D root,
        OperatorVisualId visualId,
        bool hasWeapon = false)
    {
        Root = root;
        VisualId = visualId;
        HasWeapon = hasWeapon;
    }

    public Node3D Root { get; }
    public OperatorVisualId VisualId { get; }
    public bool HasWeapon { get; }
}

internal sealed class AuthoredOperatorVisual
{
    private const float FieldWeaponScale = 0.42f;
    private static readonly Quaternion ReadiedWeaponRotation = new(
        -0.9934235f,
        0.0130106f,
        0.0974600f,
        0.0586704f);
    // The Quaternius hand bones use a different roll from the Mixamo-authored
    // garrison rig. This inverse reference-hand rotation keeps the weapon root
    // aligned to operator-forward while the authored upper-body clips drive
    // the wrist.
    private static readonly Quaternion FemaleReadiedWeaponRotation = new(
        -0.6417337f,
        -0.0994191f,
        -0.7585650f,
        0.0535996f);
    private readonly Skeleton3D _skeleton;
    private AuthoredWeaponVisual? _weapon;
    private bool _weaponReadied;

    public AuthoredOperatorVisual(Node3D root, OperatorVisualId visualId = OperatorVisualId.Garrison)
    {
        Root = root;
        VisualId = visualId;
        AnimationPlayer = CombatModelLibrary.RequireAnimationPlayer(root);
        _skeleton = CombatModelLibrary.RequireSkeleton(root);
        WeaponSocket = CreateBoneAttachment(_skeleton, "RuntimeWeaponSocket", "mixamorig:RightHand");
        BackWeaponSocket = CreateBoneAttachment(_skeleton, "RuntimeBackWeaponSocket", "mixamorig:Spine2");
        HeadSocket = CombatModelLibrary.RequireNode(root, "HeadSocket");
        VestSocket = CombatModelLibrary.RequireNode(root, "VestSocket");
        BackpackSocket = CombatModelLibrary.RequireNode(root, "BackpackSocket");
        TeamPatchSocket = CombatModelLibrary.RequireNode(root, "TeamPatchSocket");
    }

    public Node3D Root { get; }
    public OperatorVisualId VisualId { get; }
    public AnimationPlayer AnimationPlayer { get; }
    public Node3D WeaponSocket { get; }
    public Node3D BackWeaponSocket { get; }
    public Node3D HeadSocket { get; }
    public Node3D VestSocket { get; }
    public Node3D BackpackSocket { get; }
    public Node3D TeamPatchSocket { get; }
    public Color TeamColorForDiagnostics { get; private set; }
    public Color GearTintForDiagnostics { get; private set; }
    public int GearOverlayCountForDiagnostics { get; private set; }

    public OperatorRifleFitInspection InspectRifleFit()
    {
        var weapon = _weapon;
        if (weapon is null)
        {
            return default;
        }
        var rightHandIndex = ResolveBoneIndex(_skeleton, "mixamorig:RightHand");
        var leftHandIndex = ResolveBoneIndex(_skeleton, "mixamorig:LeftHand");
        var rightHand = _skeleton.GlobalTransform * _skeleton.GetBoneGlobalPose(rightHandIndex);
        var leftHand = _skeleton.GlobalTransform * _skeleton.GetBoneGlobalPose(leftHandIndex);
        var weaponOrigin = weapon.Root.GlobalPosition;
        var primaryHandOffset = weaponOrigin - rightHand.Origin;
        var primaryHandDistance = primaryHandOffset.Length();
        var supportHandOffset = weapon.Foregrip.GlobalPosition - leftHand.Origin;
        var supportHandDistance = supportHandOffset.Length();
        var supportHandTargetOffset = leftHand.Origin - rightHand.Origin;
        var handSeparation = supportHandTargetOffset.Length();
        var primaryHandRotation = rightHand.Basis.Orthonormalized().GetRotationQuaternion();
        var muzzleOffset = weapon.MuzzleDevice.GlobalPosition - weaponOrigin;
        var stockOffset = weapon.Stock.GlobalPosition - weaponOrigin;
        var minimumStockOffset = CombatModelLibrary.UsesQuaterniusOperatorRig(VisualId) ? 0.1f : 0.14f;
        var valid = primaryHandDistance <= 0.025f
            && supportHandDistance <= 0.16f
            && muzzleOffset.Z <= -0.44f
            && Mathf.Abs(muzzleOffset.X) <= 0.16f
            && Mathf.Abs(muzzleOffset.Y) <= 0.12f
            && stockOffset.Z >= minimumStockOffset;
        return new OperatorRifleFitInspection(
            valid,
            primaryHandDistance,
            primaryHandOffset,
            primaryHandRotation,
            supportHandDistance,
            supportHandOffset,
            supportHandTargetOffset,
            handSeparation,
            weaponOrigin,
            muzzleOffset,
            stockOffset);
    }

    public OperatorCarryInspection InspectRifleCarry()
    {
        var weapon = _weapon;
        if (weapon is null)
        {
            return default;
        }

        var rightShoulder = BoneWorldPosition("mixamorig:RightArm");
        var rightElbow = BoneWorldPosition("mixamorig:RightForeArm");
        var rightWrist = BoneWorldPosition("mixamorig:RightHand");
        var leftShoulder = BoneWorldPosition("mixamorig:LeftArm");
        var leftElbow = BoneWorldPosition("mixamorig:LeftForeArm");
        var leftWrist = BoneWorldPosition("mixamorig:LeftHand");
        var headBase = BoneWorldPosition("mixamorig:Head");
        var chest = BoneWorldPosition("mixamorig:Spine2");
        var stock = weapon.Stock.GlobalPosition;
        var muzzle = weapon.MuzzleDevice.GlobalPosition;
        var rootInverse = Root.GlobalTransform.AffineInverse();
        var rightShoulderLocal = rootInverse * rightShoulder;
        var rightElbowLocal = rootInverse * rightElbow;
        var rightWristLocal = rootInverse * rightWrist;
        var chestLocal = rootInverse * chest;
        var weaponRootLocal = rootInverse * weapon.Root.GlobalPosition;
        var rightSideSign = Mathf.Sign(rightShoulderLocal.X - chestLocal.X);
        return new OperatorCarryInspection(
            Available: true,
            rightShoulder,
            rightElbow,
            rightWrist,
            leftShoulder,
            leftElbow,
            leftWrist,
            headBase,
            RightElbowAngleDegrees: JointAngleDegrees(rightShoulder, rightElbow, rightWrist),
            LeftElbowAngleDegrees: JointAngleDegrees(leftShoulder, leftElbow, leftWrist),
            RightWristBelowHead: headBase.Y - rightWrist.Y,
            LeftWristBelowHead: headBase.Y - leftWrist.Y,
            StockToRightShoulderDistance: stock.DistanceTo(rightShoulder),
            HeadToWeaponLineClearance: DistanceToSegment(headBase, stock, muzzle),
            PrimaryHandToWeaponDistance: rightWrist.DistanceTo(weapon.Root.GlobalPosition),
            SupportHandToForegripDistance: leftWrist.DistanceTo(weapon.Foregrip.GlobalPosition),
            SupportHandOffset: weapon.Foregrip.GlobalPosition - leftWrist,
            weapon.Root.GlobalPosition,
            stock,
            muzzle,
            RightElbowForwardOfShoulder: rightShoulderLocal.Z - rightElbowLocal.Z,
            RightElbowOutwardOfShoulder:
                (rightElbowLocal.X - rightShoulderLocal.X) * rightSideSign,
            RightWristForwardOfChest: chestLocal.Z - rightWristLocal.Z,
            WeaponRootForwardOfChest: chestLocal.Z - weaponRootLocal.Z);
    }

    private Vector3 BoneWorldPosition(string boneName)
    {
        var index = ResolveBoneIndex(_skeleton, boneName);
        return (_skeleton.GlobalTransform * _skeleton.GetBoneGlobalPose(index)).Origin;
    }

    private static float JointAngleDegrees(Vector3 proximal, Vector3 joint, Vector3 distal)
    {
        var proximalVector = proximal - joint;
        var distalVector = distal - joint;
        if (proximalVector.IsZeroApprox() || distalVector.IsZeroApprox())
        {
            return 0.0f;
        }

        var cosine = Mathf.Clamp(proximalVector.Normalized().Dot(distalVector.Normalized()), -1.0f, 1.0f);
        return Mathf.RadToDeg(Mathf.Acos(cosine));
    }

    private static float DistanceToSegment(Vector3 point, Vector3 start, Vector3 end)
    {
        var segment = end - start;
        var lengthSquared = segment.LengthSquared();
        if (lengthSquared <= 0.000001f)
        {
            return point.DistanceTo(start);
        }

        var travel = Mathf.Clamp((point - start).Dot(segment) / lengthSquared, 0.0f, 1.0f);
        return point.DistanceTo(start + segment * travel);
    }

    public void AttachWeapon(AuthoredWeaponVisual weapon, WeaponBuild build)
    {
        _weapon = weapon;
        weapon.Configure(build);
        BackWeaponSocket.AddChild(weapon.Root);
        ApplyWeaponSocketTransform(readied: false);
    }

    public void SetWeaponVisible(bool visible)
    {
        var weapon = _weapon;
        if (weapon is not null && GodotObject.IsInstanceValid(weapon.Root))
        {
            weapon.Root.Visible = visible;
        }
    }

    public void SetWeaponReadied(bool readied)
    {
        var weapon = _weapon;
        if (weapon is null || !GodotObject.IsInstanceValid(weapon.Root) || _weaponReadied == readied)
        {
            return;
        }
        _weaponReadied = readied;
        ApplyWeaponSocketTransform(readied);
    }

    private void ApplyWeaponSocketTransform(bool readied)
    {
        if (_weapon is null)
        {
            return;
        }
        var socket = readied ? WeaponSocket : BackWeaponSocket;
        if (_weapon.Root.GetParent() != socket)
        {
            _weapon.Root.Reparent(socket, keepGlobalTransform: false);
        }
        _weapon.Root.Position = Vector3.Zero;
        _weapon.Root.Quaternion = readied
            ? CombatModelLibrary.UsesQuaterniusOperatorRig(VisualId)
                ? FemaleReadiedWeaponRotation
                : ReadiedWeaponRotation
            : Quaternion.Identity;
        var socketRelativeToRoot = TransformRelativeToAncestor(socket, Root);
        var inheritedScale = Mathf.Max(0.0001f, socketRelativeToRoot.Basis.Scale.X);
        _weapon.Root.Scale = Vector3.One * (FieldWeaponScale / inheritedScale);
        if (readied && CombatModelLibrary.UsesQuaterniusOperatorRig(VisualId))
        {
            var rightHandIndex = ResolveBoneIndex(_skeleton, "mixamorig:RightHand");
            var rightHand = _skeleton.GlobalTransform * _skeleton.GetBoneGlobalPose(rightHandIndex);
            _weapon.Root.GlobalPosition = rightHand.Origin;
        }
    }

    private static Transform3D TransformRelativeToAncestor(Node3D node, Node3D ancestor)
    {
        var result = node.Transform;
        var parent = node.GetParent();
        while (parent != ancestor)
        {
            if (parent is null)
            {
                throw new InvalidOperationException($"{node.Name} is not a descendant of {ancestor.Name}.");
            }
            if (parent is Node3D parent3D)
            {
                result = parent3D.Transform * result;
            }
            parent = parent.GetParent();
        }
        return result;
    }

    private static BoneAttachment3D CreateBoneAttachment(
        Skeleton3D skeleton,
        string name,
        string boneName)
    {
        var resolvedBoneName = ResolveBoneName(skeleton, boneName);
        if (resolvedBoneName is null)
        {
            throw new InvalidOperationException($"Animated operator skeleton is missing bone {boneName}.");
        }
        var attachment = new BoneAttachment3D
        {
            Name = name,
            BoneName = resolvedBoneName
        };
        skeleton.AddChild(attachment);
        return attachment;
    }

    private static StringName? ResolveBoneName(Skeleton3D skeleton, string requestedName)
    {
        var suffix = requestedName[(requestedName.LastIndexOf(':') + 1)..];
        for (var index = 0; index < skeleton.GetBoneCount(); index++)
        {
            var candidate = skeleton.GetBoneName(index);
            var value = candidate.ToString();
            if (string.Equals(value, requestedName, StringComparison.OrdinalIgnoreCase)
                || value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }
        return null;
    }

    private static int ResolveBoneIndex(Skeleton3D skeleton, string requestedName)
    {
        var resolved = ResolveBoneName(skeleton, requestedName);
        if (resolved is null)
        {
            throw new InvalidOperationException($"Animated operator skeleton is missing bone {requestedName}.");
        }
        return skeleton.FindBone(resolved);
    }

    public void SetTeamColor(Color color)
    {
        TeamColorForDiagnostics = color;
        var roleOverlay = new StandardMaterial3D
        {
            AlbedoColor = new Color(color.R, color.G, color.B, 0.08f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            Metallic = 0.08f,
            Roughness = 0.72f
        };
        foreach (var mesh in CombatModelLibrary.MeshesBelow(Root))
        {
            mesh.MaterialOverlay = roleOverlay;
        }
        ClearWeaponOverlay();
    }

    public void SetFactionAppearance(Color patchColor, Color gearTint)
    {
        SetTeamColor(patchColor);
        var preserveOperatorMaterials = CombatModelLibrary.UsesQuaterniusOperatorRig(VisualId);
        var appliedGearTint = preserveOperatorMaterials
            ? new Color(gearTint.R, gearTint.G, gearTint.B, Mathf.Min(gearTint.A, 0.16f))
            : gearTint;
        GearTintForDiagnostics = appliedGearTint;
        GearOverlayCountForDiagnostics = 0;
        var gearOverlay = new StandardMaterial3D
        {
            AlbedoColor = appliedGearTint,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            Metallic = 0.08f,
            Roughness = 0.72f
        };
        foreach (var mesh in CombatModelLibrary.MeshesBelow(Root))
        {
            if (preserveOperatorMaterials && mesh.Name == "OperatorHead")
            {
                mesh.MaterialOverlay = null;
                continue;
            }
            mesh.MaterialOverlay = gearOverlay;
            GearOverlayCountForDiagnostics += Mathf.Max(1, mesh.Mesh?.GetSurfaceCount() ?? 0);
        }
        ClearWeaponOverlay();
    }

    private void ClearWeaponOverlay()
    {
        if (_weapon is null)
        {
            return;
        }
        foreach (var mesh in CombatModelLibrary.MeshesBelow(_weapon.Root))
        {
            mesh.MaterialOverlay = null;
        }
    }
}

internal readonly record struct OperatorRifleFitInspection(
    bool Valid,
    float PrimaryHandDistance,
    Vector3 PrimaryHandOffset,
    Quaternion PrimaryHandRotation,
    float SupportHandDistance,
    Vector3 SupportHandOffset,
    Vector3 SupportHandTargetOffset,
    float HandSeparation,
    Vector3 WeaponOrigin,
    Vector3 MuzzleOffset,
    Vector3 StockOffset);

internal readonly record struct OperatorCarryInspection(
    bool Available,
    Vector3 RightShoulder,
    Vector3 RightElbow,
    Vector3 RightWrist,
    Vector3 LeftShoulder,
    Vector3 LeftElbow,
    Vector3 LeftWrist,
    Vector3 HeadBase,
    float RightElbowAngleDegrees,
    float LeftElbowAngleDegrees,
    float RightWristBelowHead,
    float LeftWristBelowHead,
    float StockToRightShoulderDistance,
    float HeadToWeaponLineClearance,
    float PrimaryHandToWeaponDistance,
    float SupportHandToForegripDistance,
    Vector3 SupportHandOffset,
    Vector3 WeaponRoot,
    Vector3 WeaponStock,
    Vector3 WeaponMuzzle,
    float RightElbowForwardOfShoulder,
    float RightElbowOutwardOfShoulder,
    float RightWristForwardOfChest,
    float WeaponRootForwardOfChest);

internal readonly record struct CombatModelInspection(
    bool Loaded,
    bool RequiredNodes,
    int MeshCount,
    int MaterialCount,
    Vector3 Size,
    int VertexCount = 0,
    int TriangleCount = 0,
    int TexturedMaterialCount = 0,
    WeaponAttachmentGeometryInspection AttachmentGeometry = default);

internal readonly record struct WeaponAttachmentGeometryInspection(
    int ForegripMeshCount,
    int MuzzleDeviceMeshCount,
    int SuppressorMeshCount,
    int OpticMountMeshCount)
{
    public bool Valid => ForegripMeshCount > 0
        && MuzzleDeviceMeshCount > 0
        && SuppressorMeshCount > 0
        && OpticMountMeshCount > 0;
}

internal readonly record struct WeaponAttachmentConfigurationInspection(
    bool Loaded,
    bool BareValid,
    bool StandardValid,
    bool SuppressedValid)
{
    public bool Valid => Loaded && BareValid && StandardValid && SuppressedValid;
}

internal static partial class CombatModelLibrary
{
    internal const string WeaponScenePath = "res://assets/models/steel_tide_m4a1/steel_tide_m4a1.glb";
    private const string QuaterniusWeaponRoot = "res://assets/models/quaternius_ultimate_guns";
    internal const string OperatorScenePath = "res://assets/models/bamen_military_soldier/bamen_military_soldier_animated.glb";
    internal const string PreviewOperatorScenePath = "res://assets/models/bamen_military_soldier/bamen_military_soldier.glb";
    internal const string Gsh18ScenePath = "res://assets/models/tastytony_gsh18/gsh18_runtime.glb";
    internal const string DesertEagleScenePath = "res://assets/models/elizion_desert_eagle/desert_eagle.glb";

    private const float Gsh18FirstPersonLength = 0.43f;
    private const float Gsh18PreviewLength = 0.78f;
    private const float DesertEagleFirstPersonLength = 0.48f;
    private const float DesertEaglePreviewLength = 1.05f;
    private const float ServicePistolFirstPersonLength = 0.40f;
    private const float ServicePistolPreviewLength = 0.7616f;
    private const float OperatorPreviewHeight = 2.55f;
    private const float AnimatedOperatorHeight = 1.86f;
    private static readonly Vector3 PreviewOperatorSourceSize = new(1.3053f, 2.1079f, 0.4252f);
    private static readonly Vector3 PreviewOperatorSourceCenter = new(0.0f, 1.04885f, 0.0258f);

    private static readonly string[] WeaponNodes =
    {
        "SteelTideM4A1", "Magazine", "SpareMagazine", "ChargingHandle",
        "Stock", "Foregrip", "MuzzleDevice", "Suppressor", "OpticMount",
        "RearIronSight", "FrontIronSight",
        "MuzzleDeviceTip", "SuppressorTip", "OpticReticleAnchor"
    };

    private static readonly string[] OperatorNodes =
    {
        "BamenMilitarySoldier", "BamenMilitarySoldierRig", "BamenMilitarySoldierMesh",
        "WeaponSocket", "BackWeaponSocket", "HeadSocket", "VestSocket",
        "BackpackSocket", "TeamPatchSocket"
    };

    private static readonly string[] PreviewOperatorNodes =
    {
        "BamenMilitarySoldier", "BamenMilitarySoldierRig", "BamenMilitarySoldierMesh"
    };

    private static readonly string[] QuaterniusOperatorNodes =
    {
        "QuaterniusOperator", "QuaterniusOperatorRig",
        "OperatorBody", "OperatorFeet", "OperatorHead", "OperatorLegs",
        "WeaponSocket", "BackWeaponSocket", "HeadSocket", "VestSocket",
        "BackpackSocket", "TeamPatchSocket"
    };

    private static readonly string[] Gsh18Nodes =
    {
        "TastyTonyGsh18Runtime"
    };

    private static readonly string[] DesertEagleNodes =
    {
        "RootNode", "Frame_low", "Slide_low", "Magazine_low"
    };

    public static AuthoredWeaponVisual InstantiateWeapon(bool firstPerson)
        => InstantiateWeapon(WeaponPlatform.M4A1, firstPerson);

    public static AuthoredWeaponVisual InstantiateWeapon(WeaponPlatform platform, bool firstPerson)
    {
        if (platform != WeaponPlatform.M4A1)
        {
            return InstantiateAdaptedWeapon(platform, firstPerson);
        }
        var root = InstantiateRequired(WeaponScenePath, WeaponNodes);
        root.Name = "AuthoredM4A1Visual";
        if (firstPerson)
        {
            foreach (var geometry in GeometryBelow(root))
            {
                geometry.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            }
        }
        var visual = new AuthoredWeaponVisual(root, WeaponPlatform.M4A1);
        if (!firstPerson)
        {
            visual.SpareMagazine.Visible = false;
        }
        return visual;
    }

    public static AuthoredOperatorVisual InstantiateOperator(WeaponBuild? weaponBuild = null)
        => InstantiateOperator(OperatorVisualId.Garrison, weaponBuild);

    public static AuthoredOperatorVisual InstantiateOperator(
        OperatorVisualId visualId,
        WeaponBuild? weaponBuild = null,
        bool attachDefaultWeapon = true)
    {
        var asset = OperatorVisualAsset(visualId);
        var source = InstantiateRequired(
            asset.RuntimeScenePath,
            asset.RuntimeNodes);
        var sourceBounds = asset.UsesQuaterniusRig ? ComputeBounds(source) : default;
        if (asset.UsesQuaterniusRig && (sourceBounds.MeshCount == 0 || sourceBounds.Size.Y <= 0.01f))
        {
            source.Free();
            throw new InvalidOperationException($"Operator model {visualId} has no usable geometry bounds.");
        }
        var wrapper = new Node3D { Name = "AuthoredOperatorVisual" };
        var sourcePresentation = new Node3D
        {
            Name = "AnimatedOperatorPresentation",
            RotationDegrees = new Vector3(0.0f, 180.0f, 0.0f),
            Scale = Vector3.One * (AnimatedOperatorHeight /
                (asset.UsesQuaterniusRig ? sourceBounds.Size.Y : PreviewOperatorSourceSize.Y))
        };
        sourcePresentation.AddChild(source);
        wrapper.AddChild(sourcePresentation);
        var visual = new AuthoredOperatorVisual(wrapper, visualId);
        if (weaponBuild is not null || attachDefaultWeapon)
        {
            var carriedBuild = weaponBuild ?? WeaponCatalog.Build(WeaponPlatform.M4A1, 0);
            visual.AttachWeapon(
                InstantiateWeapon(carriedBuild.Platform, firstPerson: false),
                carriedBuild);
        }
        return visual;
    }

    private static AuthoredWeaponVisual InstantiateAdaptedWeapon(WeaponPlatform platform, bool firstPerson)
    {
        Node3D source;
        if (platform == WeaponPlatform.GSh18)
        {
            source = InstantiateGsh18(firstPerson).Root;
        }
        else if (platform == WeaponPlatform.DesertEagle)
        {
            source = InstantiateDesertEagle(firstPerson).Root;
        }
        else
        {
            source = InstantiateRequired(WeaponScenePathFor(platform), Array.Empty<string>());
        }

        if (platform == WeaponPlatform.VSS)
        {
            ConfigureVssIntegratedScopeGlass(source);
        }

        var sourceBounds = ComputeBounds(source);
        if (sourceBounds.MeshCount == 0 || sourceBounds.Size.X <= 0.001f)
        {
            source.Free();
            throw new InvalidOperationException($"Authored {platform} model has no usable geometry bounds.");
        }

        var targetLength = WeaponPresentationLength(platform, firstPerson);
        var root = new Node3D { Name = $"Authored{platform}Visual" };
        var presentation = new Node3D
        {
            Name = $"{platform}Presentation",
            Position = new Vector3(0.0f, 0.0f, 0.32f - targetLength * 0.5f),
            RotationDegrees = platform is WeaponPlatform.GSh18 or WeaponPlatform.DesertEagle
                ? Vector3.Zero
                : new Vector3(0.0f, 90.0f, 0.0f),
            Scale = platform is WeaponPlatform.GSh18 or WeaponPlatform.DesertEagle
                ? Vector3.One
                : Vector3.One * (targetLength / sourceBounds.Size.X)
        };
        if (platform is not WeaponPlatform.GSh18 and not WeaponPlatform.DesertEagle)
        {
            source.Position = -sourceBounds.Center;
        }
        presentation.AddChild(source);
        root.AddChild(presentation);
        var opticMount = AddWeaponMarkers(root, targetLength);
        if (platform == WeaponPlatform.VSS)
        {
            var aperture = InspectVssIntegratedScope(root);
            if (!aperture.GeometryValid)
            {
                root.Free();
                throw new InvalidOperationException(
                    "Authored VSS scope rear aperture could not be derived from its clear lens geometry.");
            }
            opticMount.Position = aperture.RearApertureCenter;
            var reticleAnchor = RequireNode(root, "OpticReticleAnchor");
            var verified = InspectVssIntegratedScope(root, reticleAnchor);
            if (!verified.Valid)
            {
                root.Free();
                throw new InvalidOperationException(
                    "Authored VSS scope marker does not match its clear rear aperture.");
            }
        }
        if (firstPerson)
        {
            foreach (var geometry in GeometryBelow(root))
            {
                geometry.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            }
        }
        return new AuthoredWeaponVisual(root, platform);
    }

    private static Node3D AddWeaponMarkers(Node3D root, float length)
    {
        AddMarker(root, "Magazine", new Vector3(0.0f, -0.2f, -0.18f));
        AddMarker(root, "SpareMagazine", new Vector3(-0.3f, -0.62f, -0.18f));
        AddMarker(root, "ChargingHandle", new Vector3(0.075f, 0.085f, -0.05f));
        AddMarker(root, "Stock", new Vector3(0.0f, 0.0f, 0.28f));
        AddMarker(root, "Foregrip", new Vector3(0.0f, -0.16f, 0.18f - length * 0.55f));
        var muzzleDevice = AddMarker(
            root,
            "MuzzleDevice",
            new Vector3(0.0f, 0.0f, 0.28f - length));
        AddMarker(muzzleDevice, "MuzzleDeviceTip", Vector3.Zero);
        var suppressor = AddMarker(
            root,
            "Suppressor",
            new Vector3(0.0f, 0.0f, 0.28f - length));
        AddMarker(suppressor, "SuppressorTip", Vector3.Zero);
        var opticMount = AddMarker(root, "OpticMount", new Vector3(0.0f, 0.16f, -0.16f));
        AddMarker(opticMount, "OpticReticleAnchor", Vector3.Zero);
        return opticMount;
    }

    private static Node3D AddMarker(Node3D root, string name, Vector3 position)
    {
        var marker = new Marker3D { Name = name, Position = position };
        root.AddChild(marker);
        return marker;
    }

    public static AuthoredPreviewOperatorVisual InstantiatePreviewOperator()
        => InstantiatePreviewOperator(OperatorVisualId.Garrison);

    public static AuthoredPreviewOperatorVisual InstantiatePreviewOperator(
        OperatorVisualId visualId,
        WeaponBuild? weaponBuild = null)
        => InstantiatePreviewOperator(visualId, weaponBuild, buildObserver: null);

    private static AuthoredPreviewOperatorVisual InstantiatePreviewOperator(
        OperatorVisualId visualId,
        Action<PreviewOperatorBuildStage, Node3D, Node3D?> buildObserver)
        => InstantiatePreviewOperator(visualId, weaponBuild: null, buildObserver: buildObserver);

    private static AuthoredPreviewOperatorVisual InstantiatePreviewOperator(
        OperatorVisualId visualId,
        WeaponBuild? weaponBuild,
        Action<PreviewOperatorBuildStage, Node3D, Node3D?>? buildObserver)
    {
        Node3D? source = null;
        Node3D? wrapper = null;
        AuthoredWeaponVisual? pendingWeapon = null;
        try
        {
            var asset = OperatorVisualAsset(visualId);
            source = InstantiateRequired(
                asset.PreviewScenePath,
                asset.PreviewNodes);
            buildObserver?.Invoke(PreviewOperatorBuildStage.SourceCreated, source, null);
            var sourceBounds = asset.UsesQuaterniusRig
                ? ComputeBounds(source)
                : (MeshCount: 1, Size: PreviewOperatorSourceSize, Center: PreviewOperatorSourceCenter);
            if (sourceBounds.MeshCount == 0 || sourceBounds.Size.Y <= 0.01f)
            {
                throw new InvalidOperationException(
                    $"Operator preview {visualId} has no usable geometry bounds.");
            }

            source.Position = -sourceBounds.Center;
            if (asset.UsesQuaterniusRig)
            {
                var animationPlayer = RequireAnimationPlayer(source);
                animationPlayer.Play("idle");
                animationPlayer.Seek(0.45, update: true);
                animationPlayer.Pause();

                if (weaponBuild is not null)
                {
                    var previewVisual = new AuthoredOperatorVisual(source, visualId);
                    pendingWeapon = InstantiateWeapon(weaponBuild.Platform, firstPerson: false);
                    previewVisual.AttachWeapon(pendingWeapon, weaponBuild);
                    previewVisual.SetWeaponReadied(false);
                    pendingWeapon = null;
                }
            }

            wrapper = new Node3D();
            wrapper.Name = "AuthoredPreviewOperatorVisual";
            wrapper.Scale = Vector3.One * (OperatorPreviewHeight / sourceBounds.Size.Y);
            wrapper.AddChild(source);
            buildObserver?.Invoke(PreviewOperatorBuildStage.WrapperOwnsSource, source, wrapper);
            var visual = new AuthoredPreviewOperatorVisual(
                wrapper,
                visualId,
                hasWeapon: asset.UsesQuaterniusRig && weaponBuild is not null);
            source = null;
            wrapper = null;
            return visual;
        }
        finally
        {
            if (GodotObject.IsInstanceValid(pendingWeapon?.Root))
            {
                pendingWeapon!.Root.Free();
            }
            if (GodotObject.IsInstanceValid(wrapper))
            {
                wrapper!.Free();
            }
            if (GodotObject.IsInstanceValid(source))
            {
                source!.Free();
            }
        }
    }

    public static AuthoredGsh18Visual InstantiateGsh18(bool firstPerson)
    {
        var source = InstantiateRequired(Gsh18ScenePath, Gsh18Nodes);
        RemoveStagingNode(source, "Lamp");
        RemoveStagingNode(source, "Camera");
        var sourceBounds = ComputeBounds(source);
        if (sourceBounds.MeshCount == 0
            || sourceBounds.Size.X <= 0.001f
            || sourceBounds.Size.Y <= 0.001f
            || sourceBounds.Size.Z <= 0.001f)
        {
            source.Free();
            throw new InvalidOperationException("GSh-18 model has no usable geometry bounds.");
        }

        source.Position = -sourceBounds.Center;
        var targetLength = firstPerson ? Gsh18FirstPersonLength : Gsh18PreviewLength;
        // Godot's glTF import contract is source X = barrel length, source Y =
        // height, source Z = thickness. The presentation yaw maps those to
        // world Z, Y, and X respectively. Length is the authoritative scale;
        // preserving the authored aspect ratio keeps the slide and grip from
        // becoming a distorted pillar.
        var scale = targetLength / sourceBounds.Size.X;
        var wrapper = new Node3D
        {
            Name = "AuthoredGsh18Visual",
            RotationDegrees = new Vector3(0.0f, 90.0f, 0.0f),
            Scale = Vector3.One * scale
        };
        wrapper.AddChild(source);
        if (firstPerson)
        {
            foreach (var geometry in GeometryBelow(wrapper))
            {
                geometry.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            }
        }
        return new AuthoredGsh18Visual(wrapper);
    }

    public static AuthoredDesertEagleVisual InstantiateDesertEagle(bool firstPerson)
    {
        var source = InstantiateRequired(DesertEagleScenePath, DesertEagleNodes);
        var sourceBounds = ComputeBounds(source);
        if (sourceBounds.MeshCount == 0 || sourceBounds.Size.Z <= 0.001f)
        {
            source.Free();
            throw new InvalidOperationException("Desert Eagle model has no usable geometry bounds.");
        }

        source.Position = -sourceBounds.Center;
        var targetLength = firstPerson ? DesertEagleFirstPersonLength : DesertEaglePreviewLength;
        var wrapper = new Node3D
        {
            Name = "AuthoredDesertEagleVisual",
            RotationDegrees = new Vector3(0.0f, 180.0f, 0.0f),
            Scale = Vector3.One * (targetLength / sourceBounds.Size.Z)
        };
        wrapper.AddChild(source);
        if (firstPerson)
        {
            foreach (var geometry in GeometryBelow(wrapper))
            {
                geometry.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            }
        }
        return new AuthoredDesertEagleVisual(wrapper);
    }

    public static CombatModelInspection InspectWeapon()
        => Inspect(WeaponScenePath, WeaponNodes);

    public static WeaponAttachmentConfigurationInspection InspectM4AttachmentConfiguration()
    {
        AuthoredWeaponVisual? visual = null;
        try
        {
            visual = InstantiateWeapon(firstPerson: false);
            visual.Configure(new WeaponBuild { Platform = WeaponPlatform.M4A1 });
            var bareValid = visual.MuzzleDevice.Visible
                && !visual.Suppressor.Visible
                && !visual.Foregrip.Visible
                && !visual.OpticMount.Visible
                && visual.RearIronSight is { Visible: true }
                && visual.FrontIronSight is { Visible: true };

            visual.Configure(WeaponCatalog.Build(WeaponPlatform.M4A1, 0));
            var standardValid = visual.MuzzleDevice.Visible
                && !visual.Suppressor.Visible
                && visual.Foregrip.Visible
                && visual.OpticMount.Visible
                && visual.RearIronSight is { Visible: false }
                && visual.FrontIronSight is { Visible: false };

            visual.Configure(WeaponCatalog.Build(WeaponPlatform.M4A1, 2));
            var suppressedValid = !visual.MuzzleDevice.Visible
                && visual.Suppressor.Visible
                && visual.Foregrip.Visible
                && !visual.OpticMount.Visible
                && visual.RearIronSight is { Visible: false }
                && visual.FrontIronSight is { Visible: false };
            return new WeaponAttachmentConfigurationInspection(
                true,
                bareValid,
                standardValid,
                suppressedValid);
        }
        catch
        {
            return default;
        }
        finally
        {
            visual?.Root.Free();
        }
    }

    public static CombatModelInspection InspectWeapon(WeaponPlatform platform)
    {
        Node3D? root = null;
        try
        {
            root = platform switch
            {
                WeaponPlatform.M4A1 => InstantiateWeapon(firstPerson: false).Root,
                WeaponPlatform.GSh18 => InstantiateGsh18(firstPerson: false).Root,
                WeaponPlatform.DesertEagle => InstantiateDesertEagle(firstPerson: false).Root,
                _ => InstantiateWeapon(platform, firstPerson: false).Root
            };
            var bounds = ComputeBounds(root);
            var geometry = CountGeometry(MeshesBelow(root));
            return new CombatModelInspection(
                true,
                true,
                bounds.MeshCount,
                CountMaterials(root),
                bounds.Size,
                geometry.VertexCount,
                geometry.TriangleCount,
                CountTexturedMaterials(root),
                platform == WeaponPlatform.M4A1
                    ? InspectWeaponAttachmentGeometry(root)
                    : default);
        }
        catch
        {
            return new CombatModelInspection(false, false, 0, 0, Vector3.Zero);
        }
        finally
        {
            root?.Free();
        }
    }

    internal static string WeaponScenePathFor(WeaponPlatform platform)
        => platform switch
        {
            WeaponPlatform.M4A1 => WeaponScenePath,
            WeaponPlatform.GSh18 => Gsh18ScenePath,
            WeaponPlatform.DesertEagle => DesertEagleScenePath,
            WeaponPlatform.AK74 => $"{QuaterniusWeaponRoot}/ak74.glb",
            WeaponPlatform.ScarL => $"{QuaterniusWeaponRoot}/scarl.glb",
            WeaponPlatform.M24 => $"{QuaterniusWeaponRoot}/m24.glb",
            WeaponPlatform.MP5A5 => $"{QuaterniusWeaponRoot}/mp5a5.glb",
            WeaponPlatform.M3A1 => Smg45WeaponScenePath,
            WeaponPlatform.AXMC => $"{QuaterniusWeaponRoot}/axmc.glb",
            WeaponPlatform.AWM => $"{QuaterniusWeaponRoot}/awm.glb",
            WeaponPlatform.VSS => $"{QuaterniusWeaponRoot}/vss.glb",
            WeaponPlatform.P226 => $"{QuaterniusWeaponRoot}/p226.glb",
            WeaponPlatform.M1911 => $"{QuaterniusWeaponRoot}/m1911.glb",
            _ => WeaponScenePath
        };

    private static float WeaponPresentationLength(WeaponPlatform platform, bool firstPerson)
    {
        if (platform is WeaponPlatform.P226 or WeaponPlatform.M1911)
        {
            return firstPerson
                ? ServicePistolFirstPersonLength
                : ServicePistolPreviewLength;
        }

        var length = platform switch
        {
            // The marketplace models are normalized from their source bounds. Their
            // former first-person lengths made the receiver and controls visibly
            // smaller than the authored M4 while sharing the same camera mount.
            // Give each carry class enough silhouette mass without changing the
            // third-person dimensions used by operators and world previews.
            WeaponPlatform.AWM => firstPerson ? 2.0f : 1.9f,
            WeaponPlatform.M24 or WeaponPlatform.AXMC => firstPerson ? 1.74f : 1.62f,
            WeaponPlatform.AK74 or WeaponPlatform.ScarL or WeaponPlatform.VSS
                => firstPerson ? 1.58f : 1.42f,
            WeaponPlatform.MP5A5 => firstPerson ? 1.17f : 1.08f,
            WeaponPlatform.M3A1 => 1.08f,
            WeaponPlatform.GSh18 => Gsh18FirstPersonLength,
            WeaponPlatform.DesertEagle => DesertEagleFirstPersonLength,
            _ => 1.36f
        };
        return firstPerson ? length : length * 1.12f;
    }

    public static CombatModelInspection InspectOperator()
        => InspectOperator(OperatorVisualId.Garrison);

    public static CombatModelInspection InspectOperator(OperatorVisualId visualId)
    {
        Node3D? root = null;
        try
        {
            root = InstantiateOperator(visualId, attachDefaultWeapon: false).Root;
            var size = !UsesQuaterniusOperatorRig(visualId)
                ? PreviewOperatorSourceSize * (AnimatedOperatorHeight / PreviewOperatorSourceSize.Y)
                : ComputeBounds(root).Size;
            var geometry = CountOperatorGeometry(root);
            return new CombatModelInspection(
                true,
                true,
                MeshesBelow(root).Count(),
                CountMaterials(root),
                size,
                geometry.VertexCount,
                geometry.TriangleCount,
                CountTexturedMaterials(root));
        }
        catch
        {
            return new CombatModelInspection(false, false, 0, 0, Vector3.Zero);
        }
        finally
        {
            root?.Free();
        }
    }

    public static CombatModelInspection InspectPreviewOperator()
        => InspectPreviewOperator(OperatorVisualId.Garrison);

    public static CombatModelInspection InspectPreviewOperator(OperatorVisualId visualId)
    {
        Node3D? root = null;
        try
        {
            root = InstantiatePreviewOperator(visualId).Root;
            var size = !UsesQuaterniusOperatorRig(visualId)
                ? PreviewOperatorSourceSize * (OperatorPreviewHeight / PreviewOperatorSourceSize.Y)
                : ComputeBounds(root).Size;
            var geometry = CountOperatorGeometry(root);
            return new CombatModelInspection(
                true,
                true,
                MeshesBelow(root).Count(),
                CountMaterials(root),
                size,
                geometry.VertexCount,
                geometry.TriangleCount,
                CountTexturedMaterials(root));
        }
        catch
        {
            return new CombatModelInspection(false, false, 0, 0, Vector3.Zero);
        }
        finally
        {
            root?.Free();
        }
    }

    public static CombatModelInspection InspectGsh18()
    {
        Node3D? root = null;
        try
        {
            root = InstantiateGsh18(firstPerson: false).Root;
            var bounds = ComputeBounds(root);
            var geometry = CountGeometry(MeshesBelow(root));
            return new CombatModelInspection(
                true,
                true,
                bounds.MeshCount,
                CountMaterials(root),
                bounds.Size,
                geometry.VertexCount,
                geometry.TriangleCount,
                CountTexturedMaterials(root));
        }
        catch
        {
            return new CombatModelInspection(false, false, 0, 0, Vector3.Zero);
        }
        finally
        {
            root?.Free();
        }
    }

    public static CombatModelInspection InspectDesertEagle()
    {
        Node3D? root = null;
        try
        {
            root = InstantiateDesertEagle(firstPerson: false).Root;
            var bounds = ComputeBounds(root);
            var geometry = CountGeometry(MeshesBelow(root));
            return new CombatModelInspection(
                true,
                true,
                bounds.MeshCount,
                CountMaterials(root),
                bounds.Size,
                geometry.VertexCount,
                geometry.TriangleCount,
                CountTexturedMaterials(root));
        }
        catch
        {
            return new CombatModelInspection(false, false, 0, 0, Vector3.Zero);
        }
        finally
        {
            root?.Free();
        }
    }

    internal static Node3D RequireNode(Node3D root, string name)
    {
        var node = FindNode(root, name);
        return node ?? throw new InvalidOperationException(
            $"Combat model {root.Name} is missing required node {name}.");
    }

    internal static void SetOptionalVisibility(Node3D? node, bool visible)
    {
        if (GodotObject.IsInstanceValid(node))
        {
            node!.Visible = visible;
        }
    }

    internal static Node3D? FindOptionalNode(Node3D root, string name)
        => FindNode(root, name);

    internal static AnimationPlayer RequireAnimationPlayer(Node root)
    {
        if (root is AnimationPlayer player)
        {
            return player;
        }
        var children = root.GetChildren();
        using var childrenBacking = children.AsDisposable();
        foreach (var child in children)
        {
            try
            {
                return RequireAnimationPlayer(child);
            }
            catch (InvalidOperationException)
            {
                // Continue until the imported glTF AnimationPlayer is found.
            }
        }
        throw new InvalidOperationException($"Combat model {root.Name} is missing AnimationPlayer.");
    }

    internal static Skeleton3D RequireSkeleton(Node root)
    {
        if (root is Skeleton3D skeleton)
        {
            return skeleton;
        }
        var children = root.GetChildren();
        using var childrenBacking = children.AsDisposable();
        foreach (var child in children)
        {
            try
            {
                return RequireSkeleton(child);
            }
            catch (InvalidOperationException)
            {
                // Continue until the imported glTF skeleton is found.
            }
        }
        throw new InvalidOperationException($"Combat model {root.Name} is missing Skeleton3D.");
    }

    internal static IEnumerable<MeshInstance3D> MeshesBelow(Node root)
    {
        foreach (var geometry in GeometryBelow(root))
        {
            if (geometry is MeshInstance3D mesh)
            {
                yield return mesh;
            }
        }
    }

    private static Node3D InstantiateRequired(string path, IReadOnlyList<string> requiredNodes)
    {
        var scene = GD.Load<PackedScene>(path)
            ?? throw new InvalidOperationException($"Required combat model could not load: {path}");
        var root = scene.Instantiate<Node3D>();
        foreach (var nodeName in requiredNodes)
        {
            if (FindNode(root, nodeName) is null)
            {
                root.Free();
                throw new InvalidOperationException(
                    $"Required combat model {path} is missing node {nodeName}.");
            }
        }
        return root;
    }

    private static CombatModelInspection Inspect(string path, IReadOnlyList<string> requiredNodes)
    {
        var scene = GD.Load<PackedScene>(path);
        if (scene is null)
        {
            return new CombatModelInspection(false, false, 0, 0, Vector3.Zero);
        }
        var root = scene.Instantiate<Node3D>();
        try
        {
            var required = true;
            foreach (var nodeName in requiredNodes)
            {
                required &= FindNode(root, nodeName) is not null;
            }
            var bounds = ComputeBounds(root);
            var geometry = CountGeometry(MeshesBelow(root));
            return new CombatModelInspection(
                true,
                required,
                bounds.MeshCount,
                CountMaterials(root),
                bounds.Size,
                geometry.VertexCount,
                geometry.TriangleCount,
                CountTexturedMaterials(root),
                string.Equals(path, WeaponScenePath, StringComparison.Ordinal)
                    ? InspectWeaponAttachmentGeometry(root)
                    : default);
        }
        finally
        {
            root.Free();
        }
    }

    private static int CountMaterials(Node root)
    {
        var materialCount = 0;
        foreach (var meshInstance in MeshesBelow(root))
        {
            if (meshInstance.MaterialOverride is not null)
            {
                materialCount += Mathf.Max(1, meshInstance.Mesh?.GetSurfaceCount() ?? 0);
                continue;
            }
            if (meshInstance.Mesh is not { } mesh)
            {
                continue;
            }
            for (var surface = 0; surface < mesh.GetSurfaceCount(); surface++)
            {
                if (mesh.SurfaceGetMaterial(surface) is not null)
                {
                    materialCount++;
                }
            }
        }
        return materialCount;
    }

    private static int CountTexturedMaterials(Node root)
    {
        var materialCount = 0;
        foreach (var meshInstance in MeshesBelow(root))
        {
            if (meshInstance.Mesh is not { } mesh)
            {
                continue;
            }
            var surfaceCount = mesh.GetSurfaceCount();
            if (meshInstance.MaterialOverride is { } materialOverride)
            {
                if (IsTexturedPbrMaterial(materialOverride))
                {
                    materialCount += Mathf.Max(1, surfaceCount);
                }
                continue;
            }
            for (var surface = 0; surface < surfaceCount; surface++)
            {
                var material = meshInstance.GetSurfaceOverrideMaterial(surface)
                    ?? mesh.SurfaceGetMaterial(surface);
                if (material is not null && IsTexturedPbrMaterial(material))
                {
                    materialCount++;
                }
            }
        }
        return materialCount;
    }

    private static WeaponAttachmentGeometryInspection InspectWeaponAttachmentGeometry(Node3D root)
        => new(
            CountRenderableMeshesBelow(FindNode(root, "Foregrip")),
            CountRenderableMeshesBelow(FindNode(root, "MuzzleDevice")),
            CountRenderableMeshesBelow(FindNode(root, "Suppressor")),
            CountRenderableMeshesBelow(FindNode(root, "OpticMount")));

    private static int CountRenderableMeshesBelow(Node3D? root)
    {
        if (root is null)
        {
            return 0;
        }
        return MeshesBelow(root).Count(mesh => CountGeometry(new[] { mesh }).TriangleCount > 0);
    }

    private static bool IsTexturedPbrMaterial(Material material)
        => material is BaseMaterial3D baseMaterial
            && baseMaterial.AlbedoTexture is not null
            && baseMaterial.NormalTexture is not null
            && (baseMaterial.OrmTexture is not null
                || baseMaterial.MetallicTexture is not null
                || baseMaterial.RoughnessTexture is not null);

    private static (int MeshCount, Vector3 Size, Vector3 Center) ComputeBounds(Node3D root)
    {
        var minimum = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        var maximum = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        var meshCount = 0;
        AccumulateBounds(root, Transform3D.Identity, ref minimum, ref maximum, ref meshCount);
        return meshCount == 0
            ? (0, Vector3.Zero, Vector3.Zero)
            : (meshCount, maximum - minimum, (minimum + maximum) * 0.5f);
    }

    private static (int VertexCount, int TriangleCount) CountOperatorGeometry(Node3D root)
    {
        var characterMeshes = new[]
        {
            "OperatorBody", "OperatorFeet", "OperatorHead", "OperatorLegs"
        }
            .Select(name => FindNode(root, name))
            .OfType<MeshInstance3D>()
            .ToArray();
        return CountGeometry(characterMeshes.Length == 4
            ? characterMeshes
            : MeshesBelow(root));
    }

    private static (int VertexCount, int TriangleCount) CountGeometry(
        IEnumerable<MeshInstance3D> meshInstances)
    {
        var vertexCount = 0;
        var triangleCount = 0;
        foreach (var meshInstance in meshInstances)
        {
            if (meshInstance.Mesh is not ArrayMesh mesh)
            {
                continue;
            }
            for (var surface = 0; surface < mesh.GetSurfaceCount(); surface++)
            {
                if (mesh.SurfaceGetPrimitiveType(surface) != Mesh.PrimitiveType.Triangles)
                {
                    continue;
                }
                var vertices = mesh.SurfaceGetArrayLen(surface);
                var indices = mesh.SurfaceGetArrayIndexLen(surface);
                vertexCount += vertices;
                triangleCount += (indices > 0 ? indices : vertices) / 3;
            }
        }
        return (vertexCount, triangleCount);
    }

    private static void AccumulateBounds(
        Node3D node,
        Transform3D parentTransform,
        ref Vector3 minimum,
        ref Vector3 maximum,
        ref int meshCount)
    {
        var transform = parentTransform * node.Transform;
        if (node is MeshInstance3D { Mesh: not null } mesh)
        {
            meshCount++;
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
                AccumulateBounds(child3D, transform, ref minimum, ref maximum, ref meshCount);
            }
        }
    }

    private static Node3D? FindNode(Node3D root, string name)
    {
        if (root.Name == name)
        {
            return root;
        }
        return root.FindChild(name, recursive: true, owned: false) as Node3D;
    }

    private static void RemoveStagingNode(Node3D root, string name)
    {
        var node = FindNode(root, name);
        node?.Free();
    }

    private static IEnumerable<GeometryInstance3D> GeometryBelow(Node root)
    {
        var children = root.GetChildren();
        using var childrenBacking = children.AsDisposable();
        foreach (var child in children)
        {
            if (child is GeometryInstance3D geometry)
            {
                yield return geometry;
            }
            foreach (var descendant in GeometryBelow(child))
            {
                yield return descendant;
            }
        }
    }
}
