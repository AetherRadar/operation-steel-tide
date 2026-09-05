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
    private readonly MechanismTransforms _sourceMechanismRest;
    private readonly Node3D? _legacyRearIronSightPrimary;
    private readonly Node3D? _legacyRearIronSightSecondary;
    private AuthoredOpticsVisual? _worldExternalOptics;
    private bool _expectsWorldExternalOptics;
    private bool _worldExternalOpticsLoadAttempted;
    private readonly Vector3? _magazineGripInMagazine;
    private readonly Vector3? _actionGripInAction;
    private readonly IntegratedScopeInspection _integratedOpticInspection;
    private readonly bool _ironSightGeometryAuthoredVisible;

    public AuthoredWeaponVisual(
        Node3D root,
        WeaponPlatform platform,
        IntegratedScopeInspection? integratedOpticInspection = null)
    {
        Root = root;
        Platform = platform;
        Magazine = CombatModelLibrary.RequireNode(root, "Magazine");
        SpareMagazine = CombatModelLibrary.RequireNode(root, "SpareMagazine");
        MagazineGrip = CombatModelLibrary.FindOptionalNode(root, "MagazineGrip");
        SpareMagazineGrip = CombatModelLibrary.FindOptionalNode(root, "SpareMagazineGrip");
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
        IronSightGeometry = CombatModelLibrary.FindOptionalNode(root, "IronSightGeometry");
        _ironSightGeometryAuthoredVisible = IronSightGeometry?.Visible == true;
        MuzzleDeviceTip = CombatModelLibrary.RequireNode(root, "MuzzleDeviceTip");
        SuppressorTip = CombatModelLibrary.RequireNode(root, "SuppressorTip");
        OpticReticleAnchor = CombatModelLibrary.RequireNode(root, "OpticReticleAnchor");
        OpticRearApertureAnchor = CombatModelLibrary.FindOptionalNode(
            root,
            "OpticRearApertureAnchor");
        OpticFrontApertureAnchor = CombatModelLibrary.FindOptionalNode(
            root,
            "OpticFrontApertureAnchor");
        OpticRailContact = CombatModelLibrary.FindOptionalNode(root, "OpticRailContact");
        EjectionPort = CombatModelLibrary.FindOptionalNode(root, "EjectionPort");
        if (CombatModelLibrary.FindOptionalNode(Magazine, "MagazineGripSocket")
            is { } magazineGripSocket)
        {
            _magazineGripInMagazine = CombatModelLibrary.TransformBelowAncestor(
                magazineGripSocket,
                Magazine).Origin;
        }
        else
        {
            var magazineBounds = CombatModelLibrary.ComputeLocalBounds(Magazine);
            if (magazineBounds.MeshCount > 0)
            {
                // Legacy authored weapons predate explicit reload sockets. Put
                // the palm on the magazine's left wall instead of silently
                // falling back to a hand target unrelated to the moving part.
                _magazineGripInMagazine = magazineBounds.Center
                    + Vector3.Left * magazineBounds.Size.X * 0.5f;
            }
        }
        if (CombatModelLibrary.FindOptionalNode(ChargingHandle, "ChargingHandleSocket")
            is { } actionGripSocket)
        {
            _actionGripInAction = CombatModelLibrary.TransformBelowAncestor(
                actionGripSocket,
                ChargingHandle).Origin;
        }
        else
        {
            var actionBounds = CombatModelLibrary.ComputeLocalBounds(ChargingHandle);
            if (actionBounds.MeshCount > 0)
            {
                // Approach the rear-left face so empty reloads visibly rack the
                // real slide/bolt rather than moving the mechanism under a
                // stationary support hand.
                _actionGripInAction = actionBounds.Center
                    + Vector3.Left * actionBounds.Size.X * 0.5f
                    + Vector3.Back * actionBounds.Size.Z * 0.25f;
            }
        }
        _authoredMechanismRest = CaptureMechanismTransforms(
            Magazine,
            SpareMagazine,
            ChargingHandle);
        _sourceMechanismRest = CanonicalSourceMechanismRest(platform);
        _integratedOpticInspection = HasIntegratedScope
            ? integratedOpticInspection
                ?? CombatModelLibrary.InspectIntegratedScope(Root, OpticReticleAnchor)
            : default;
    }

    public Node3D Root { get; }
    public WeaponPlatform Platform { get; }
    public Node3D Magazine { get; }
    public Node3D SpareMagazine { get; }
    public Node3D? MagazineGrip { get; }
    public Node3D? SpareMagazineGrip { get; }
    public Node3D? ActiveMagazineGrip
        => SpareMagazine.Visible ? SpareMagazineGrip : MagazineGrip;
    public Node3D ChargingHandle { get; }
    public Node3D Stock { get; }
    public Node3D Foregrip { get; }
    public Node3D MuzzleDevice { get; }
    public Node3D Suppressor { get; }
    public Node3D OpticMount { get; }
    public Node3D? RearIronSight { get; }
    public Node3D? FrontIronSight { get; }
    public Node3D? IronSightGeometry { get; }
    public Node3D MuzzleDeviceTip { get; }
    public Node3D SuppressorTip { get; }
    public Node3D OpticReticleAnchor { get; }
    public Node3D? OpticRearApertureAnchor { get; }
    public Node3D? OpticFrontApertureAnchor { get; }
    public Node3D? OpticRailContact { get; }
    public Node3D? EjectionPort { get; }
    public Node3D ActiveMuzzleTip => Suppressor.Visible ? SuppressorTip : MuzzleDeviceTip;
    public bool HasVisibleMagazineMechanism
        => CombatModelLibrary.MeshesBelow(Magazine).Any(mesh => mesh.Mesh is not null)
            && CombatModelLibrary.MeshesBelow(SpareMagazine).Any(mesh => mesh.Mesh is not null);
    public bool HasIntegratedScope => CombatModelLibrary.HasIntegratedScope(Platform);
    public IntegratedScopeInspection IntegratedOpticInspection
        => _integratedOpticInspection;
    public bool IntegratedOpticPresentationValid
        => !HasIntegratedScope || IntegratedOpticInspection.Valid;
    public bool IntegratedM4OpticAxisValid
    {
        get
        {
            if (Platform != WeaponPlatform.M4A1
                || !GodotObject.IsInstanceValid(OpticRearApertureAnchor)
                || !GodotObject.IsInstanceValid(OpticFrontApertureAnchor))
            {
                return false;
            }

            var rear = CombatModelLibrary.TransformBelowAncestor(
                OpticRearApertureAnchor!,
                Root).Origin;
            var front = CombatModelLibrary.TransformBelowAncestor(
                OpticFrontApertureAnchor!,
                Root).Origin;
            var reticle = CombatModelLibrary.TransformBelowAncestor(
                OpticReticleAnchor,
                Root).Origin;
            var axis = front - rear;
            return rear.DistanceTo(reticle) <= 0.001f
                && axis.Length() >= 0.05f
                && Mathf.Abs(axis.X) <= 0.001f
                && Mathf.Abs(axis.Y) <= 0.001f
                && axis.Z < -0.05f;
        }
    }

    public bool WorldOpticPresentationMatches(WeaponBuild build)
    {
        if (!_expectsWorldExternalOptics)
        {
            return false;
        }

        var hasOptic = build.Attachments.TryGetValue(AttachmentSlot.Optic, out var opticId);
        var integrated = hasOptic
            && (HasIntegratedScope
                || Platform == WeaponPlatform.M4A1 && opticId == "optic_micro");
        var externalExpected = hasOptic && !integrated;
        var externalPresentationMatches = externalExpected
            ? _worldExternalOptics is not null
                && GodotObject.IsInstanceValid(_worldExternalOptics.Root)
                && _worldExternalOptics.PresentationMatches(opticId, externalExpected: true)
            : _worldExternalOptics is null
                || !GodotObject.IsInstanceValid(_worldExternalOptics.Root)
                || _worldExternalOptics.PresentationMatches(opticId, externalExpected: false);
        var integratedPresentationMatches = !integrated
            || HasIntegratedScope && IntegratedOpticPresentationValid
            || Platform == WeaponPlatform.M4A1
                && OpticMount.Visible
                && IntegratedM4OpticAxisValid;
        var hideIronSights = hasOptic && (integrated || externalExpected);
        var externalClearanceMatches = WorldExternalOpticMountMatches(
            opticId,
            externalExpected);
        var ironSightPresentationMatches = IronSightVisibilityMatches(!hideIronSights);
        var valid = externalPresentationMatches
            && integratedPresentationMatches
            && externalClearanceMatches
            && ironSightPresentationMatches;
        if (!valid)
        {
            GD.Print(
                $"WORLD_OPTIC_MISMATCH platform={Platform} optic={opticId ?? "none"} "
                + $"external={externalPresentationMatches} integrated={integratedPresentationMatches} "
                + $"clearance={externalClearanceMatches} irons={ironSightPresentationMatches}");
        }
        return valid;
    }

    private bool WorldExternalOpticMountMatches(
        string? opticId,
        bool externalExpected)
    {
        if (!externalExpected)
        {
            return true;
        }

        var hasHideableIronSights = GodotObject.IsInstanceValid(RearIronSight)
            || GodotObject.IsInstanceValid(FrontIronSight)
            || GodotObject.IsInstanceValid(IronSightGeometry)
            || GodotObject.IsInstanceValid(_legacyRearIronSightPrimary)
            || GodotObject.IsInstanceValid(_legacyRearIronSightSecondary);
        if (!GodotObject.IsInstanceValid(OpticRailContact)
            || _worldExternalOptics is null
            || !GodotObject.IsInstanceValid(_worldExternalOptics.Root))
        {
            return false;
        }

        var expectedOffset = Vector3.Up
            * CombatModelLibrary.AuthoredOpticRailContactOffset(opticId);
        var mountContractMatches = ReferenceEquals(
                _worldExternalOptics.Root.GetParent(),
                OpticRailContact)
            && _worldExternalOptics.Root.Position.DistanceTo(expectedOffset) <= 0.001f
            && _worldExternalOptics.Root.Transform.Basis.IsEqualApprox(Basis.Identity);
        if (!mountContractMatches)
        {
            return false;
        }

        if (hasHideableIronSights)
        {
            return true;
        }

        var weaponTop = float.NegativeInfinity;
        var opticBottom = float.PositiveInfinity;
        var toWeaponRoot = Root.GlobalTransform.AffineInverse();
        foreach (var mesh in CombatModelLibrary.MeshesBelow(Root))
        {
            if (mesh.Mesh is null
                || !mesh.IsVisibleInTree()
                || IsNodeBelow(mesh, _worldExternalOptics.Root))
            {
                continue;
            }

            var bounds = mesh.GetAabb();
            var meshToWeaponRoot = toWeaponRoot * mesh.GlobalTransform;
            for (var endpoint = 0; endpoint < 8; endpoint++)
            {
                weaponTop = Mathf.Max(
                    weaponTop,
                    (meshToWeaponRoot * bounds.GetEndpoint(endpoint)).Y);
            }
        }
        foreach (var mesh in CombatModelLibrary.MeshesBelow(_worldExternalOptics.Root))
        {
            if (mesh.Mesh is null || !mesh.IsVisibleInTree())
            {
                continue;
            }

            var bounds = mesh.GetAabb();
            var meshToWeaponRoot = toWeaponRoot * mesh.GlobalTransform;
            for (var endpoint = 0; endpoint < 8; endpoint++)
            {
                opticBottom = Mathf.Min(
                    opticBottom,
                    (meshToWeaponRoot * bounds.GetEndpoint(endpoint)).Y);
            }
        }
        return float.IsFinite(weaponTop)
            && float.IsFinite(opticBottom)
            && opticBottom >= weaponTop - 0.001f;
    }

    private static bool IsNodeBelow(Node node, Node ancestor)
    {
        for (var current = node; current is not null; current = current.GetParent())
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }
        }
        return false;
    }

    public void EnableWorldExternalOptics()
    {
        _expectsWorldExternalOptics = true;
    }

    private bool EnsureWorldExternalOptics()
    {
        if (_worldExternalOptics is not null
            && GodotObject.IsInstanceValid(_worldExternalOptics.Root))
        {
            return true;
        }
        if (_worldExternalOpticsLoadAttempted)
        {
            return false;
        }
        _worldExternalOpticsLoadAttempted = true;

        if (!GodotObject.IsInstanceValid(OpticRailContact))
        {
            GD.PushWarning(
                $"Authored {Platform} world optics unavailable; missing OpticRailContact.");
            return false;
        }

        AuthoredOpticsVisual? optics = null;
        try
        {
            optics = CombatModelLibrary.InstantiateAuthoredOptics(firstPerson: false);
            OpticRailContact!.AddChild(optics.Root);
            optics.Root.Transform = Transform3D.Identity;
            _worldExternalOptics = optics;
            return true;
        }
        catch (Exception exception)
        {
            optics?.Root.Free();
            GD.PushWarning(
                $"Authored {Platform} world optics unavailable; retaining iron sights: "
                + exception.Message);
            return false;
        }
    }

    public bool TryMagazineGripGlobalPosition(bool spare, out Vector3 position)
    {
        var explicitGrip = spare ? SpareMagazineGrip : MagazineGrip;
        if (GodotObject.IsInstanceValid(explicitGrip))
        {
            position = explicitGrip!.GlobalPosition;
            return true;
        }

        if (_magazineGripInMagazine is not { } localContact)
        {
            position = default;
            return false;
        }

        position = (spare ? SpareMagazine : Magazine).GlobalTransform
            * localContact;
        return true;
    }

    public bool AlignMagazineGripToGlobalPosition(
        bool spare,
        Vector3 targetGlobalPosition)
    {
        if (!TryMagazineGripGlobalPosition(spare, out var currentGrip))
        {
            return false;
        }

        var magazine = spare ? SpareMagazine : Magazine;
        if (!GodotObject.IsInstanceValid(magazine))
        {
            return false;
        }

        // The sidearm reload clip owns the hand and finger performance. Move
        // the rigid DCC-authored magazine by the remaining contact delta after
        // its normal mechanism phase has been synchronized; never deform or
        // translate the skinned arm to chase the prop.
        magazine.GlobalPosition += targetGlobalPosition - currentGrip;
        return true;
    }

    public bool TryActionGripGlobalPosition(out Vector3 position)
    {
        if (_actionGripInAction is not { } localContact)
        {
            position = default;
            return false;
        }

        position = ChargingHandle.GlobalTransform * localContact;
        return true;
    }

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
        var usesIntegratedOptic = hasOptic
            && (CombatModelLibrary.HasIntegratedScope(Platform)
                || Platform == WeaponPlatform.M4A1 && opticId == "optic_micro");
        var showWorldExternalOptic = hasOptic && !usesIntegratedOptic;
        if (_expectsWorldExternalOptics && showWorldExternalOptic)
        {
            EnsureWorldExternalOptics();
        }
        var worldExternalOpticVisible = false;
        if (_worldExternalOptics is not null
            && GodotObject.IsInstanceValid(_worldExternalOptics.Root))
        {
            worldExternalOpticVisible = _worldExternalOptics.Configure(
                opticId,
                showExternalModel: showWorldExternalOptic);
            _worldExternalOptics.Root.Position = Vector3.Up
                * CombatModelLibrary.AuthoredOpticRailContactOffset(opticId);
        }
        OpticMount.Visible = usesIntegratedOptic;
        var hideIronSights = hasOptic
            && (!_expectsWorldExternalOptics
                || worldExternalOpticVisible
                || usesIntegratedOptic);
        // Preserve the authored iron sights for the bare rifle, but fold them out
        // of the optical sight picture. Both assemblies remain in the GLB and are
        // restored when the optic is removed.
        if (GodotObject.IsInstanceValid(RearIronSight))
        {
            RearIronSight!.Visible = !hideIronSights;
        }
        else
        {
            CombatModelLibrary.SetOptionalVisibility(_legacyRearIronSightPrimary, !hideIronSights);
            CombatModelLibrary.SetOptionalVisibility(_legacyRearIronSightSecondary, !hideIronSights);
        }
        CombatModelLibrary.SetOptionalVisibility(FrontIronSight, !hideIronSights);
        CombatModelLibrary.SetOptionalVisibility(
            IronSightGeometry,
            !hideIronSights && _ironSightGeometryAuthoredVisible);
    }

    public void SyncMechanisms(Node3D magazine, Node3D spareMagazine, Node3D chargingHandle)
    {
        var sourceTransforms = CaptureMechanismTransforms(
            magazine,
            spareMagazine,
            chargingHandle);

        // Transfer only the procedural root-space delta onto each DCC-authored rest pose.
        Magazine.Transform = ApplyRootSpaceDelta(
            sourceTransforms.Magazine,
            _sourceMechanismRest.Magazine,
            _authoredMechanismRest.Magazine);
        // The staged magazine starts at a pouch pose but finishes in the same
        // physical magwell as the removed primary. Map its complete motion from
        // the primary source/authored rest frames so SeatEnd is guaranteed to
        // produce the exact installed DCC transform instead of preserving the
        // staged root's unrelated authored rotation.
        SpareMagazine.Transform = ApplyRootSpaceDelta(
            sourceTransforms.SpareMagazine,
            _sourceMechanismRest.Magazine,
            _authoredMechanismRest.Magazine);
        ChargingHandle.Transform = ApplyRootSpaceDelta(
            sourceTransforms.ChargingHandle,
            _sourceMechanismRest.ChargingHandle,
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

    private bool IronSightVisibilityMatches(bool expectedVisible)
    {
        var rearMatches = GodotObject.IsInstanceValid(RearIronSight)
            ? RearIronSight!.Visible == expectedVisible
            : (!GodotObject.IsInstanceValid(_legacyRearIronSightPrimary)
                    || _legacyRearIronSightPrimary!.Visible == expectedVisible)
                && (!GodotObject.IsInstanceValid(_legacyRearIronSightSecondary)
                    || _legacyRearIronSightSecondary!.Visible == expectedVisible);
        return rearMatches
            && (!GodotObject.IsInstanceValid(FrontIronSight)
                || FrontIronSight!.Visible == expectedVisible)
            && (!GodotObject.IsInstanceValid(IronSightGeometry)
                || IronSightGeometry!.Visible
                    == (expectedVisible && _ironSightGeometryAuthoredVisible));
    }

    private static MechanismTransforms CanonicalSourceMechanismRest(
        WeaponPlatform platform)
    {
        var profile = FirstPersonReloadProfileCatalog.For(platform);
        return new MechanismTransforms(
            new Transform3D(
                Basis.FromEuler(profile.MagazineRotation),
                profile.MagazineHome),
            new Transform3D(
                Basis.FromEuler(profile.StowedRotation),
                profile.SpareMagazineHome),
            new Transform3D(Basis.Identity, profile.ActionHome));
    }

    private static Transform3D ApplyRootSpaceDelta(
        Transform3D sourceTransform,
        Transform3D sourceRest,
        Transform3D authoredRest)
    {
        // Position and orientation are independent root-space mechanism
        // deltas. Multiplying the complete transforms rotates the authored
        // rest origin around the canonical source pivot, so an AK magazine
        // appears to translate merely because it is rocking in place.
        var orientationDelta = sourceTransform.Basis
            * sourceRest.Basis.Inverse();
        var positionDelta = sourceTransform.Origin - sourceRest.Origin;
        return new Transform3D(
            orientationDelta * authoredRest.Basis,
            authoredRest.Origin + positionDelta);
    }

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
    private static Quaternion Hy3dReadiedWeaponRotationFor(OperatorVisualId visualId)
        => visualId switch
        {
            OperatorVisualId.Heron => new Quaternion(0.9210141f, -0.1339542f, -0.3640152f, -0.0358069f),
            OperatorVisualId.Lynx => new Quaternion(0.8804879f, -0.0810482f, -0.4655326f, -0.0380999f),
            OperatorVisualId.Magpie => new Quaternion(0.8546273f, -0.0190107f, -0.5143517f, -0.0685067f),
            OperatorVisualId.Jackal => new Quaternion(0.8585695f, -0.0547701f, -0.5087583f, 0.0319925f),
            _ => new Quaternion(0.7124552f, 0.126017f, -0.6895026f, -0.0333694f)
        };
    private readonly Skeleton3D _skeleton;
    private readonly int _spineBone;
    private readonly int _rightShoulderBone;
    private readonly int _rightElbowBone;
    private readonly int _rightHandBone;
    private readonly int _leftShoulderBone;
    private readonly int _leftElbowBone;
    private readonly int _leftWristBone;
    private AuthoredWeaponVisual? _weapon;
    private bool _weaponReadied;
    private Node3D? _helmetVisual;
    private Node3D? _bodyArmorVisual;
    private Node3D? _backpackVisual;

    public AuthoredOperatorVisual(Node3D root, OperatorVisualId visualId = OperatorVisualId.Garrison)
    {
        Root = root;
        VisualId = visualId;
        AnimationPlayer = CombatModelLibrary.RequireAnimationPlayer(root);
        _skeleton = CombatModelLibrary.RequireSkeleton(root);
        _spineBone = ResolveBoneIndex(_skeleton, "mixamorig:Spine");
        _rightShoulderBone = ResolveBoneIndex(_skeleton, "mixamorig:RightArm");
        _rightElbowBone = ResolveBoneIndex(_skeleton, "mixamorig:RightForeArm");
        _rightHandBone = ResolveBoneIndex(_skeleton, "mixamorig:RightHand");
        _leftShoulderBone = ResolveBoneIndex(_skeleton, "mixamorig:LeftArm");
        _leftElbowBone = ResolveBoneIndex(_skeleton, "mixamorig:LeftForeArm");
        _leftWristBone = ResolveBoneIndex(_skeleton, "mixamorig:LeftHand");
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
        // AnimationPlayer seeks/advances do not call the gameplay animator.
        // Refresh the Tencent socket here as well so editor captures and the
        // deterministic roster probe inspect the same two-hand pose rendered
        // in-game.
        RefreshWeaponPose();
        var weapon = _weapon;
        if (weapon is null)
        {
            return default;
        }
        var rightHand = _skeleton.GlobalTransform * _skeleton.GetBoneGlobalPose(_rightHandBone);
        var leftHand = _skeleton.GlobalTransform * _skeleton.GetBoneGlobalPose(_leftWristBone);
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
        RefreshWeaponPose();
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
            ChestToWeaponLineClearance: DistanceToSegment(chest, stock, muzzle),
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

    /// <summary>Replaces the three optional world/paper-doll gear overlays.</summary>
    public void SetEquipment(
        EquipmentItem? helmet,
        EquipmentItem? bodyArmor,
        EquipmentItem? backpack)
    {
        // HY3D operators ship with authored clothing, armor, and headgear in
        // their production mesh.  The legacy steel_tide_operator paper-doll
        // overlays use a different rig scale and become oversized floating
        // blocks when attached to these characters, so keep the authored body
        // intact for roster/deployment previews and runtime operators.
        if (CombatModelLibrary.UsesHy3dOperator(VisualId))
        {
            return;
        }
        ReplaceEquipmentVisual(ref _helmetVisual, HeadSocket, helmet, EquipmentSlot.Helmet);
        ReplaceEquipmentVisual(ref _bodyArmorVisual, VestSocket, bodyArmor, EquipmentSlot.BodyArmor);
        ReplaceEquipmentVisual(ref _backpackVisual, BackpackSocket, backpack, EquipmentSlot.Backpack);
    }

    private static void ReplaceEquipmentVisual(
        ref Node3D? current,
        Node3D socket,
        EquipmentItem? equipment,
        EquipmentSlot slot)
    {
        if (GodotObject.IsInstanceValid(current))
        {
            current!.QueueFree();
        }
        current = null;
        if (equipment is null || equipment.DefinitionId.EndsWith("_none", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var root = new Node3D { Name = $"Equipped{slot}Visual" };
        // A few authored rigs keep their metre conversion on the skeleton and
        // expose gear sockets with the inverse scale (typically 100x).  Child
        // equipment must cancel that inherited scale or a normal helmet/pack
        // becomes a giant floating prop in front of the operator.
        root.Scale = InverseSocketScale(socket);
        socket.AddChild(root);
        if (TryAttachAuthoredEquipment(root, slot))
        {
            current = root;
            return;
        }
        root.QueueFree();
    }

    private static bool TryAttachAuthoredEquipment(Node3D root, EquipmentSlot slot)
    {
        var sourceNodeName = slot switch
        {
            EquipmentSlot.Helmet => "Helmet",
            EquipmentSlot.BodyArmor => "Vest",
            EquipmentSlot.Backpack => "Backpack",
            _ => string.Empty
        };
        if (string.IsNullOrEmpty(sourceNodeName))
        {
            return false;
        }
        Node3D? sourceRoot = null;
        try
        {
            var scene = GD.Load<PackedScene>("res://assets/models/steel_tide_operator/steel_tide_operator.glb")
                ?? throw new InvalidOperationException("Authored equipment scene could not load.");
            sourceRoot = scene.Instantiate<Node3D>();
            var source = CombatModelLibrary.FindOptionalNode(sourceRoot, sourceNodeName)
                ?? throw new InvalidOperationException($"Authored equipment scene is missing {sourceNodeName}.");
            var copy = source.Duplicate() as Node3D
                ?? throw new InvalidOperationException($"{sourceNodeName} could not be duplicated.");
            var bounds = CombatModelLibrary.ComputeLocalBounds(copy);
            if (bounds.MeshCount == 0 || bounds.Size.Y <= 0.01f)
            {
                copy.Free();
                throw new InvalidOperationException($"{sourceNodeName} has no usable bounds.");
            }
            var targetHeight = slot switch
            {
                EquipmentSlot.Helmet => 0.48f,
                EquipmentSlot.BodyArmor => 0.86f,
                EquipmentSlot.Backpack => 1.12f,
                _ => bounds.Size.Y
            };
            var presentationScale = targetHeight / bounds.Size.Y;
            copy.Scale = Vector3.One * presentationScale;
            var socketOffset = slot switch
            {
                EquipmentSlot.Helmet => new Vector3(0, 0.10f, 0),
                EquipmentSlot.BodyArmor => new Vector3(0, -0.02f, 0),
                EquipmentSlot.Backpack => new Vector3(0, 0.0f, 0.08f),
                _ => Vector3.Zero
            };
            // ComputeLocalBounds is in the duplicated node's own frame.  Keep
            // the centering correction after scaling so the visible geometry's
            // centre lands on the authored socket offset instead of the source
            // scene's metre-space origin.
            copy.Position = socketOffset - bounds.Center * presentationScale;
            root.AddChild(copy);
            return true;
        }
        catch (Exception exception)
        {
            GD.PushWarning($"Authored {slot} overlay unavailable; using fallback: {exception.Message}");
            return false;
        }
        finally
        {
            sourceRoot?.Free();
        }
    }

    private static Vector3 InverseSocketScale(Node3D socket)
    {
        // ReplaceEquipmentVisual can run while the operator scene is still
        // being assembled (before its root enters the SceneTree).  Reading
        // GlobalTransform in that state emits !is_inside_tree errors and
        // returns an unusable basis.  Accumulate the local scale chain instead;
        // for the authored rigs in this project the transforms are axis-aligned
        // and this is equivalent to the eventual global scale without requiring
        // a live tree.
        var scale = socket.Scale;
        var ancestor = socket.GetParent();
        while (ancestor is Node3D parent)
        {
            scale *= parent.Scale;
            ancestor = parent.GetParent();
        }
        static float Inverse(float value)
            => Mathf.Abs(value) <= 0.0001f ? 1.0f : 1.0f / value;
        return new Vector3(Inverse(scale.X), Inverse(scale.Y), Inverse(scale.Z));
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

    /// <summary>
    /// Re-applies the HY-3D rifle socket after an animation sample.  The
    /// imported clips use a hand roll that changes substantially between
    /// ready/aim locomotion states, so one baked quaternion cannot keep both
    /// hands on a rifle.  The socket is therefore solved in the actor frame
    /// each sample and the left upper-arm chain is fitted to the foregrip.
    /// </summary>
    internal void RefreshWeaponPose()
    {
        var animation = AnimationPlayer.CurrentAnimation.ToString();
        if (!_weaponReadied
            || _weapon is null
            || !GodotObject.IsInstanceValid(_weapon.Root)
            || (!string.IsNullOrEmpty(animation) && !IsTwoHandedReadyAnimation()))
        {
            return;
        }

#pragma warning disable CS0618
        _skeleton.ClearBonesGlobalPoseOverride();
        _skeleton.ForceUpdateAllBoneTransforms();
#pragma warning restore CS0618
        ApplyCarryTorsoCorrection(animation);
        var dynamicCarry = !WeaponCatalog.IsSidearm(_weapon.Platform);
        var dynamicHy3d = CombatModelLibrary.UsesHy3dOperator(VisualId);
        ApplyWeaponSocketTransform(readied: true, dynamicCarry: dynamicCarry);
        if (!dynamicHy3d && dynamicCarry)
        {
            // The default Bamen operator has authored rifle-ready clips, but
            // their left wrist is not constrained to the imported foregrip
            // marker.  Solve that short chain after the socket follows the
            // right hand so close third-person views show an actual two-hand
            // grip instead of a fist hovering over the receiver.
            RetargetHy3dLeftArm(_weapon.Foregrip.GlobalPosition);
        }
    }

    private void ApplyCarryTorsoCorrection(string animation)
    {
#pragma warning disable CS0618
        if (_spineBone < 0 || !animation.Contains("_", StringComparison.Ordinal))
        {
            return;
        }

        var correctionDegrees = animation switch
        {
            "ready_sprint" or "aim_sprint" => 7.0f,
            "ready_run" or "aim_run" => 4.5f,
            "ready_walk" or "aim_walk" => 2.5f,
            _ => 0.0f
        };
        if (correctionDegrees <= 0.0f)
        {
            return;
        }

        // The locomotion clips intentionally lean into a sprint, but the
        // source lean is exaggerated at close third-person distances. Lift
        // only the upper-body chain; the legs keep their authored stride and
        // the weapon/hand solve below follows the corrected shoulders.
        var spine = _skeleton.GetBoneGlobalPoseNoOverride(_spineBone);
        var actorBasis = Root.GlobalTransform.Basis.Orthonormalized();
        var correction = new Quaternion(actorBasis * Vector3.Right, Mathf.DegToRad(correctionDegrees));
        _skeleton.SetBoneGlobalPoseOverride(
            _spineBone,
            new Transform3D(
                (new Basis(correction) * spine.Basis).Orthonormalized(),
                spine.Origin),
            1.0f,
            persistent: true);
        _skeleton.ForceUpdateBoneChildTransform(_spineBone);
#pragma warning restore CS0618
    }

    private bool IsTwoHandedReadyAnimation()
    {
        var animation = AnimationPlayer.CurrentAnimation.ToString();
        return animation.StartsWith("ready_", StringComparison.Ordinal)
            || animation.StartsWith("aim_", StringComparison.Ordinal);
    }

    private void ApplyWeaponSocketTransform(bool readied, bool dynamicCarry = false)
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
        var isHy3d = readied && CombatModelLibrary.UsesHy3dOperator(VisualId);
        if (dynamicCarry && isHy3d)
        {
            // First put the trigger hand on a stable actor-frame target. The
            // imported HY-3D clips have a different upper-body rest axis, so
            // anchoring the rifle to their animated wrist leaves the weapon
            // behind the chest and makes the support arm impossible to solve.
            SolveHy3dRightArm();
        }
        else
        {
            _weapon.Root.Quaternion = readied
                ? CombatModelLibrary.UsesQuaterniusOperatorRig(VisualId)
                    ? isHy3d
                        ? Hy3dReadiedWeaponRotationFor(VisualId)
                        : FemaleReadiedWeaponRotation
                    : ReadiedWeaponRotation
                : Quaternion.Identity;
        }
        if (readied && dynamicCarry && socket is BoneAttachment3D dynamicAttachment)
        {
            // AnimationPlayer.Seek/Advance updates the skeleton immediately,
            // while BoneAttachment3D normally refreshes on the next scene
            // notification.  Refresh the hand attachment in the same tick so
            // the rifle never lags behind the trigger wrist during run/sprint
            // transitions.
            dynamicAttachment.OnSkeletonUpdate();
        }
        var socketRelativeToRoot = TransformRelativeToAncestor(socket, Root);
        var inheritedScale = Mathf.Max(0.0001f, socketRelativeToRoot.Basis.Scale.X);
        // HY-3D's normalized bodies have a slightly shorter forearm span than
        // the legacy Quaternius mannequin.  A 0.40 weapon scale keeps the
        // foregrip inside the support arm's reachable arc while retaining the
        // authored rifle silhouette and muzzle distance for both ready and aim.
        var weaponScale = isHy3d ? 0.40f : FieldWeaponScale;
        _weapon.Root.Scale = Vector3.One * (weaponScale / inheritedScale);
        if (readied && CombatModelLibrary.UsesQuaterniusOperatorRig(VisualId))
        {
            // Reparenting a weapon immediately after the visual enters the tree
            // can happen before BoneAttachment3D receives its first skeleton
            // update.  Assigning GlobalPosition against that stale attachment
            // bakes the hand's world position into the child's local offset;
            // the next animation frame then leaves the rifle ~0.7 m away from
            // both hands (the roster fit probe catches this as a large, constant
            // primary/support distance).  Force the attachment pose first and
            // solve the tiny residual in the socket's local frame.  Keeping this
            // local also makes subsequent animation updates follow the hand.
            if (_skeleton.IsInsideTree())
            {
#pragma warning disable CS0618
                _skeleton.ForceUpdateAllBoneTransforms();
#pragma warning restore CS0618
                if (socket is BoneAttachment3D attachment)
                {
                    attachment.OnSkeletonUpdate();
                }
            }

            var rightHand = _skeleton.GlobalTransform * _skeleton.GetBoneGlobalPose(_rightHandBone);
            _weapon.Root.Position = socket.GlobalTransform.AffineInverse() * rightHand.Origin;
        }
        if (dynamicCarry)
        {
            ApplyCarrySocketBasis();
            if (isHy3d)
            {
                RetargetHy3dLeftArm(_weapon.Foregrip.GlobalPosition);
            }
        }
    }

    private void ApplyCarrySocketBasis()
    {
        var actorBasis = Root.GlobalTransform.Basis.Orthonormalized();
        // The M4A1 markers use the project frame (-Z down-range). Compensate
        // the animated wrist in world space instead of applying a fixed local
        // quaternion.  A BoneAttachment can carry a non-orthogonal FBX hand
        // roll, so assigning the final global basis avoids leaking that roll
        // into the muzzle elevation on aim clips.
        // Bias the rifle a few degrees across the chest. This small ergonomic
        // cant brings the foregrip into the opposite arm's natural reach
        // without moving the trigger hand, while keeping muzzle lateral error
        // inside the combat model envelope for both authored rigs.
        var carryBasis = (actorBasis * new Basis(Vector3.Up, Mathf.DegToRad(12.0f)))
            .Orthonormalized();
        var global = _weapon!.Root.GlobalTransform;
        var globalScale = global.Basis.Scale;
        global.Basis = carryBasis.Scaled(globalScale);
        _weapon.Root.GlobalTransform = global;
    }

    private void SolveHy3dRightArm()
    {
        if (_rightShoulderBone < 0 || _rightElbowBone < 0 || _rightHandBone < 0)
        {
            return;
        }

        var actorBasis = Root.GlobalTransform.Basis.Orthonormalized();
        var chest = BoneWorldPosition("mixamorig:Spine2");
        // Keep the trigger hand just below and down-range of the sternum. The
        // offset is expressed in the actor frame, so turning an operator does
        // not change the stance. It also scales well across the five normalized
        // HY-3D bodies (all are presented at 1.86 m).
        var target = chest + actorBasis * new Vector3(0.02f, -0.12f, -0.11f);
        var shoulder = BoneWorldPosition("mixamorig:RightArm");
        var shoulderLocal = Root.GlobalTransform.AffineInverse() * shoulder;
        var chestLocal = Root.GlobalTransform.AffineInverse() * chest;
        float outwardSign = Mathf.Sign(shoulderLocal.X - chestLocal.X);
        if (Mathf.IsZeroApprox(outwardSign))
        {
            outwardSign = 1.0f;
        }
        var pole = chest + actorBasis * new Vector3(1.20f * outwardSign, -0.10f, -0.25f);
        SolveHy3dTwoBoneChain(
            _rightShoulderBone,
            _rightElbowBone,
            _rightHandBone,
            target,
            pole);
    }

    private void SolveHy3dTwoBoneChain(
        int shoulderBone,
        int elbowBone,
        int wristBone,
        Vector3 targetGlobalPosition,
        Vector3 poleGlobalPosition)
    {
        if (shoulderBone < 0 || elbowBone < 0 || wristBone < 0)
        {
            return;
        }

        var inverse = _skeleton.GlobalTransform.AffineInverse();
        var target = inverse * targetGlobalPosition;
        var pole = inverse * poleGlobalPosition;
#pragma warning disable CS0618
        var shoulder = _skeleton.GetBoneGlobalPoseNoOverride(shoulderBone);
        var elbow = _skeleton.GetBoneGlobalPoseNoOverride(elbowBone);
        var wrist = _skeleton.GetBoneGlobalPoseNoOverride(wristBone);
        var proximal = elbow.Origin - shoulder.Origin;
        var distal = wrist.Origin - elbow.Origin;
        var proximalLength = proximal.Length();
        var distalLength = distal.Length();
        var shoulderToTarget = target - shoulder.Origin;
        if (proximalLength <= 0.0001f
            || distalLength <= 0.0001f
            || shoulderToTarget.LengthSquared() <= 0.000001f)
        {
            return;
        }

        var direction = shoulderToTarget.Normalized();
        var requestedDistance = shoulderToTarget.Length();
        var allowStretch = shoulderBone == _leftShoulderBone;
        var rawReach = proximalLength + distalLength;
        var rawStretch = requestedDistance / Mathf.Max(0.0001f, rawReach);
        var rawCosine = Mathf.Clamp(
            (proximalLength * proximalLength
                + distalLength * distalLength
                - requestedDistance * requestedDistance)
                / Mathf.Max(0.0001f, 2.0f * proximalLength * distalLength),
            -1.0f,
            1.0f);
        var rawElbowAngle = Mathf.RadToDeg(Mathf.Acos(rawCosine));
        // A few HY-3D bodies have a 3–7% shorter forearm span than the
        // normalized rifle.  Let the presentation chain stretch a small,
        // bounded amount instead of pulling the support hand off the
        // foregrip.  The fallback target nudge below handles any pose that
        // would require a visibly excessive stretch.
        var useStretch = allowStretch
            && (rawStretch > 1.0f || rawElbowAngle > 168.0f)
            && rawStretch <= 1.12f;
        var stretch = useStretch
            ? Mathf.Max(1.0f, rawStretch) * 1.025f
            : 1.0f;
        var solvedProximalLength = proximalLength * stretch;
        var solvedDistalLength = distalLength * stretch;
        var solvedDistance = useStretch
            ? requestedDistance
            : Mathf.Clamp(
                requestedDistance,
                Mathf.Abs(proximalLength - distalLength) + 0.0001f,
                proximalLength + distalLength - 0.0001f);
        var projectedElbowDistance = (
            solvedProximalLength * solvedProximalLength
            - solvedDistalLength * solvedDistalLength
            + solvedDistance * solvedDistance)
            / (2.0f * solvedDistance);
        var elbowHeight = Mathf.Sqrt(Mathf.Max(
            0.0f,
            solvedProximalLength * solvedProximalLength
                - projectedElbowDistance * projectedElbowDistance));
        var poleDirection = pole - shoulder.Origin;
        poleDirection -= direction * poleDirection.Dot(direction);
        if (poleDirection.LengthSquared() <= 0.000001f)
        {
            var fallbackPole = Mathf.Abs(direction.Dot(Vector3.Up)) < 0.85f
                ? Vector3.Up
                : Vector3.Right;
            poleDirection = fallbackPole - direction * fallbackPole.Dot(direction);
        }
        poleDirection = poleDirection.Normalized();
        var desiredElbow = shoulder.Origin
            + direction * projectedElbowDistance
            + poleDirection * elbowHeight;
        var desiredWrist = shoulder.Origin + direction * solvedDistance;

        var shoulderSwing = new Quaternion(
            proximal.Normalized(),
            (desiredElbow - shoulder.Origin).Normalized());
        _skeleton.SetBoneGlobalPoseOverride(
            shoulderBone,
            new Transform3D(
                (new Basis(shoulderSwing) * shoulder.Basis).Orthonormalized(),
                shoulder.Origin),
            1.0f,
            persistent: true);
        _skeleton.ForceUpdateBoneChildTransform(shoulderBone);

        var solvedElbow = _skeleton.GetBoneGlobalPose(elbowBone);
        var solvedWrist = _skeleton.GetBoneGlobalPose(wristBone);
        var solvedDistal = solvedWrist.Origin - solvedElbow.Origin;
        var desiredDistal = desiredWrist - (useStretch ? desiredElbow : solvedElbow.Origin);
        if (solvedDistal.LengthSquared() > 0.000001f
            && desiredDistal.LengthSquared() > 0.000001f)
        {
            var elbowSwing = new Quaternion(
                solvedDistal.Normalized(),
                desiredDistal.Normalized());
            _skeleton.SetBoneGlobalPoseOverride(
                elbowBone,
                new Transform3D(
                    (new Basis(elbowSwing) * solvedElbow.Basis).Orthonormalized(),
                    useStretch ? desiredElbow : solvedElbow.Origin),
                1.0f,
                persistent: true);
            _skeleton.ForceUpdateBoneChildTransform(elbowBone);
        }

        var finalWrist = _skeleton.GetBoneGlobalPose(wristBone);
        _skeleton.SetBoneGlobalPoseOverride(
            wristBone,
            new Transform3D(
                wrist.Basis.Orthonormalized(),
                useStretch ? desiredWrist : finalWrist.Origin),
            1.0f,
            persistent: true);
        _skeleton.ForceUpdateBoneChildTransform(wristBone);
#pragma warning restore CS0618
    }

    private void RetargetHy3dLeftArm(Vector3 targetGlobalPosition)
    {
        if (_leftShoulderBone < 0 || _leftElbowBone < 0 || _leftWristBone < 0)
        {
            return;
        }

        var shoulder = BoneWorldPosition("mixamorig:LeftArm");
        var elbow = BoneWorldPosition("mixamorig:LeftForeArm");
        var wrist = BoneWorldPosition("mixamorig:LeftHand");
        var upperLength = shoulder.DistanceTo(elbow);
        var lowerLength = elbow.DistanceTo(wrist);
        var targetDistance = shoulder.DistanceTo(targetGlobalPosition);
        var maximumNaturalDistance = Mathf.Sqrt(Mathf.Max(
            0.0f,
            upperLength * upperLength
                + lowerLength * lowerLength
                - 2.0f * upperLength * lowerLength
                    * Mathf.Cos(Mathf.DegToRad(168.0f))));
        var targetShift = targetDistance > (upperLength + lowerLength) * 1.12f
            ? Mathf.Clamp(
                targetDistance - maximumNaturalDistance,
                0.0f,
                0.0295f)
            : 0.0f;
        if (targetShift > 0.0001f)
        {
            targetGlobalPosition += (shoulder - targetGlobalPosition).Normalized()
                * targetShift;
        }

        var actorBasis = Root.GlobalTransform.Basis.Orthonormalized();
        var chest = BoneWorldPosition("mixamorig:Spine2");
        var shoulderLocal = Root.GlobalTransform.AffineInverse() * shoulder;
        var chestLocal = Root.GlobalTransform.AffineInverse() * chest;
        float outwardSign = Mathf.Sign(shoulderLocal.X - chestLocal.X);
        if (Mathf.IsZeroApprox(outwardSign))
        {
            outwardSign = -1.0f;
        }
        var pole = chest + actorBasis * new Vector3(1.20f * outwardSign, -0.10f, -0.25f);
        SolveHy3dTwoBoneChain(
            _leftShoulderBone,
            _leftElbowBone,
            _leftWristBone,
            targetGlobalPosition,
            pole);
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
    float ChestToWeaponLineClearance,
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
    internal const string Gsh18ScenePath =
        "res://assets/models/steel_tide_reloadable_weapons/gsh18_reloadable.glb";
    internal const string DesertEagleScenePath = "res://assets/models/elizion_desert_eagle/desert_eagle.glb";
    internal const string Ak47FirstPersonScenePath =
        "res://assets/models/steel_tide_ak74/ak47_reloadable_fp.glb";
    internal const string Ak47WorldScenePath =
        "res://assets/models/steel_tide_ak74/ak47_reloadable_world.glb";

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

    internal static float AuthoredOpticRailContactOffset(string? opticId)
        => opticId switch
        {
            "optic_micro" => 0.070f,
            "optic_holo" => 0.092f,
            "optic_scope" or "optic_7x" or "optic_sniper" => 0.084f,
            _ => 0.0f
        };

    private static readonly string[] WeaponNodes =
    {
        "SteelTideM4A1", "Magazine", "SpareMagazine", "ChargingHandle",
        "Stock", "Foregrip", "MuzzleDevice", "Suppressor", "OpticMount",
        "RearIronSight", "FrontIronSight",
        "MuzzleDeviceTip", "SuppressorTip", "OpticReticleAnchor",
        "OpticRearApertureAnchor", "OpticFrontApertureAnchor"
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
        "SteelTideReloadableGSh18", "Magazine", "SpareMagazine",
        "ChargingHandle", "PrimaryGripSocket", "MagazineGripSocket",
        "ChargingHandleSocket"
    };

    private static readonly string[] DesertEagleNodes =
    {
        "RootNode", "Frame_low", "Slide_low", "Magazine_low"
    };

    public static AuthoredWeaponVisual InstantiateWeapon(bool firstPerson)
        => InstantiateWeapon(WeaponPlatform.M4A1, firstPerson);

    public static AuthoredWeaponVisual InstantiateWeapon(WeaponPlatform platform, bool firstPerson)
    {
        if (platform == WeaponPlatform.AK74)
        {
            return InstantiateAk47(firstPerson);
        }
        if (platform != WeaponPlatform.M4A1)
        {
            return InstantiateAdaptedWeapon(platform, firstPerson);
        }
        var root = InstantiateRequired(WeaponScenePath, WeaponNodes);
        root.Name = "AuthoredM4A1Visual";
        if (FindOptionalNode(root, "OpticRailContact") is null)
        {
            var reticleInWeaponRoot = TransformBelowAncestor(
                RequireNode(root, "OpticReticleAnchor"),
                root).Origin;
            AddMarker(
                root,
                "OpticRailContact",
                reticleInWeaponRoot
                    - Vector3.Up * AuthoredOpticRailContactOffset("optic_micro"));
        }
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
            visual.EnableWorldExternalOptics();
        }
        return visual;
    }

    private static AuthoredWeaponVisual InstantiateAk47(bool firstPerson)
    {
        // This DCC-authored asset is already normalized to the project weapon
        // frame and owns its reload mechanisms and gameplay markers. Loading it
        // directly avoids wrapping, rotating, rescaling, duplicating its magazine,
        // or stacking generic markers on top of the authored hierarchy.
        var root = InstantiateRequired(
            firstPerson ? Ak47FirstPersonScenePath : Ak47WorldScenePath,
            Array.Empty<string>());
        root.Name = "AuthoredAK47Visual";
        if (firstPerson)
        {
            foreach (var geometry in GeometryBelow(root))
            {
                geometry.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            }
        }
        var visual = new AuthoredWeaponVisual(root, WeaponPlatform.AK74);
        if (!firstPerson)
        {
            visual.SpareMagazine.Visible = false;
            visual.EnableWorldExternalOptics();
        }
        return visual;
    }

    public static AuthoredOperatorVisual InstantiateOperator(WeaponBuild? weaponBuild = null)
        => InstantiateOperator(OperatorVisualId.Garrison, weaponBuild);

    public static AuthoredOperatorVisual InstantiateOperator(
        OperatorVisualId visualId,
        WeaponBuild? weaponBuild = null,
        bool attachDefaultWeapon = true,
        EquipmentItem? helmet = null,
        EquipmentItem? bodyArmor = null,
        EquipmentItem? backpack = null)
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
        visual.SetEquipment(helmet, bodyArmor, backpack);
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

        if (HasIntegratedScope(platform))
        {
            try
            {
                ConfigureIntegratedScopeGlass(source, platform);
            }
            catch
            {
                source.Free();
                throw;
            }
        }

        var sourceBounds = FindOptionalNode(source, "WeaponBodyGeometry") is { } weaponBody
            ? ComputeBounds(weaponBody)
            : ComputeBounds(source);
        if (sourceBounds.MeshCount == 0 || sourceBounds.Size.X <= 0.001f)
        {
            source.Free();
            throw new InvalidOperationException($"Authored {platform} model has no usable geometry bounds.");
        }

        var targetLength = WeaponPresentationLength(platform, firstPerson);
        var usesRuntimeCoordinateContract = platform is WeaponPlatform.ScarL
            or WeaponPlatform.MP5A5
            or WeaponPlatform.M24
            or WeaponPlatform.AXMC
            or WeaponPlatform.AWM
            or WeaponPlatform.VSS
            or WeaponPlatform.P226
            or WeaponPlatform.M1911
            or WeaponPlatform.GSh18;
        var root = new Node3D { Name = $"Authored{platform}Visual" };
        var presentation = new Node3D
        {
            Name = $"{platform}Presentation",
            Position = usesRuntimeCoordinateContract
                ? Vector3.Zero
                : new Vector3(0.0f, 0.0f, 0.32f - targetLength * 0.5f),
            RotationDegrees = platform is WeaponPlatform.GSh18 or WeaponPlatform.DesertEagle
                    || usesRuntimeCoordinateContract
                ? Vector3.Zero
                : new Vector3(0.0f, 90.0f, 0.0f),
            Scale = platform is WeaponPlatform.GSh18 or WeaponPlatform.DesertEagle
                    || usesRuntimeCoordinateContract
                ? Vector3.One
                : Vector3.One * (targetLength / sourceBounds.Size.X)
        };
        if (platform is not WeaponPlatform.GSh18 and not WeaponPlatform.DesertEagle
            && !usesRuntimeCoordinateContract)
        {
            source.Position = -sourceBounds.Center;
        }
        presentation.AddChild(source);
        root.AddChild(presentation);
        var (authoredMagazine, authoredSpareMagazine) = AttachReloadableMagazine(
            root,
            source,
            platform);
        var authoredAction = AttachReloadableAction(root, source, platform);
        var opticMount = AddWeaponMarkers(
            root,
            targetLength,
            platform,
            authoredMagazine,
            authoredSpareMagazine,
            authoredAction,
            source);
        IntegratedScopeInspection? verifiedIntegratedScope = null;
        if (HasIntegratedScope(platform))
        {
            var aperture = InspectIntegratedScope(root);
            if (!aperture.GeometryValid || !aperture.OpticalAxisAligned)
            {
                root.Free();
                throw new InvalidOperationException(
                    $"Authored {platform} scope front/rear apertures do not form a valid optical axis.");
            }
            opticMount.Position = aperture.RearApertureCenter;
            var reticleAnchor = RequireNode(root, "OpticReticleAnchor");
            var verified = InspectIntegratedScope(root, reticleAnchor);
            if (!verified.Valid)
            {
                root.Free();
                throw new InvalidOperationException(
                    $"Authored {platform} scope marker does not match its clear rear aperture.");
            }
            verifiedIntegratedScope = verified;
        }
        if (firstPerson)
        {
            foreach (var geometry in GeometryBelow(root))
            {
                geometry.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            }
        }
        var visual = new AuthoredWeaponVisual(
            root,
            platform,
            verifiedIntegratedScope);
        if (!firstPerson)
        {
            visual.SpareMagazine.Visible = false;
            visual.EnableWorldExternalOptics();
        }
        return visual;
    }

    private static (Node3D? Magazine, Node3D? SpareMagazine) AttachReloadableMagazine(
        Node3D root,
        Node3D source,
        WeaponPlatform platform)
    {
        var authoredMagazine = FindOptionalNode(source, "Magazine");
        var authoredSpareMagazine = FindOptionalNode(source, "SpareMagazine");
        if (authoredMagazine is not null
            && authoredSpareMagazine is not null
            && MeshesBelow(authoredMagazine).Any()
            && MeshesBelow(authoredSpareMagazine).Any())
        {
            var magazineInWeaponRoot = TransformBelowAncestor(authoredMagazine, root);
            var spareInWeaponRoot = TransformBelowAncestor(authoredSpareMagazine, root);
            authoredMagazine.Owner = null;
            authoredMagazine.Reparent(root, keepGlobalTransform: false);
            authoredMagazine.Transform = magazineInWeaponRoot;
            authoredSpareMagazine.Owner = null;
            authoredSpareMagazine.Reparent(root, keepGlobalTransform: false);
            authoredSpareMagazine.Transform = spareInWeaponRoot;
            authoredSpareMagazine.Visible = false;
            return (authoredMagazine, authoredSpareMagazine);
        }

        var detachablePartNames = platform switch
        {
            WeaponPlatform.GSh18 => new[] { "GSh18_05" },
            WeaponPlatform.DesertEagle => new[]
            {
                "Magazine_low", "MagazineBase_low", "BaseInside_low"
            },
            _ => Array.Empty<string>()
        };
        if (detachablePartNames.Length > 0)
        {
            var detachableParts = detachablePartNames
                .Select(name => FindOptionalNode(source, name))
                .ToArray();
            if (detachableParts.Any(part => part is null))
            {
                throw new InvalidOperationException(
                    $"Reloadable {platform} asset is missing a magazine component.");
            }

            var assembledMagazine = new Node3D { Name = "Magazine" };
            root.AddChild(assembledMagazine);
            foreach (var part in detachableParts)
            {
                var geometry = part!;
                var partInWeaponRoot = TransformBelowAncestor(geometry, root);
                geometry.Owner = null;
                geometry.Reparent(assembledMagazine, keepGlobalTransform: false);
                geometry.Transform = partInWeaponRoot;
            }

            var assembledSpareMagazine = (Node3D)assembledMagazine.Duplicate();
            assembledSpareMagazine.Name = "SpareMagazine";
            assembledSpareMagazine.Position = new Vector3(-0.30f, -0.42f, 0.13f);
            assembledSpareMagazine.Visible = false;
            root.AddChild(assembledSpareMagazine);
            return (assembledMagazine, assembledSpareMagazine);
        }

        var magazineGeometry = FindOptionalNode(source, "MagazineGeometry");
        if (magazineGeometry is null)
        {
            return (null, null);
        }

        var geometryInWeaponRoot = TransformBelowAncestor(
            magazineGeometry,
            root);
        var magazine = new Node3D
        {
            Name = "Magazine",
            Position = new Vector3(0.0f, -0.2f, -0.31f)
        };
        root.AddChild(magazine);
        magazineGeometry.Owner = null;
        magazineGeometry.Reparent(magazine, keepGlobalTransform: false);
        magazineGeometry.Transform = magazine.Transform.AffineInverse()
            * geometryInWeaponRoot;

        var spareMagazine = new Node3D
        {
            Name = "SpareMagazine",
            Position = new Vector3(-0.3f, -0.62f, -0.18f),
            Visible = false
        };
        root.AddChild(spareMagazine);
        var spareGeometry = (Node3D)magazineGeometry.Duplicate();
        spareGeometry.Name = "SpareMagazineGeometry";
        spareMagazine.AddChild(spareGeometry);
        spareGeometry.Transform = magazineGeometry.Transform;
        return (magazine, spareMagazine);
    }

    internal static Transform3D TransformBelowAncestor(
        Node3D node,
        Node3D ancestor)
    {
        var transform = node.Transform;
        for (var parent = node.GetParent() as Node3D;
             parent is not null && !ReferenceEquals(parent, ancestor);
             parent = parent.GetParent() as Node3D)
        {
            transform = parent.Transform * transform;
        }
        return transform;
    }

    private static Node3D? AttachReloadableAction(
        Node3D root,
        Node3D source,
        WeaponPlatform platform)
    {
        var action = FindOptionalNode(source, "ChargingHandle")
            ?? (platform == WeaponPlatform.DesertEagle
                ? FindOptionalNode(source, "Group001")
                : null);
        if (action is null || !MeshesBelow(action).Any(mesh => mesh.Mesh is not null))
        {
            return null;
        }

        var actionInWeaponRoot = TransformBelowAncestor(action, root);
        action.Owner = null;
        action.Reparent(root, keepGlobalTransform: false);
        action.Transform = actionInWeaponRoot;
        action.Name = "ChargingHandle";
        return action;
    }

    private static Node3D AddWeaponMarkers(
        Node3D root,
        float length,
        WeaponPlatform platform,
        Node3D? authoredMagazine = null,
        Node3D? authoredSpareMagazine = null,
        Node3D? authoredAction = null,
        Node3D? source = null)
    {
        // Match TacticalPlayer's mechanism rest frame. Keeping adapted weapons
        // 0.13 m forward of that source frame made the visible support hand miss
        // the authored magazine marker throughout non-M4 reloads.
        _ = authoredMagazine
            ?? AddMarker(root, "Magazine", new Vector3(0.0f, -0.2f, -0.31f));
        _ = authoredSpareMagazine
            ?? AddMarker(root, "SpareMagazine", new Vector3(-0.3f, -0.62f, -0.18f));
        _ = authoredAction
            ?? AddMarker(
                root,
                "ChargingHandle",
                SocketPositionOr(
                    source,
                    root,
                    "ChargingHandleSocket",
                    new Vector3(0.075f, 0.085f, -0.05f)));
        AddMarker(root, "Stock", new Vector3(0.0f, 0.0f, 0.28f));
        AddMarker(
            root,
            "Foregrip",
            SocketPositionOr(
                source,
                root,
                "SupportGripSocket",
                new Vector3(0.0f, -0.16f, 0.18f - length * 0.55f)));
        var muzzleDevice = AddMarker(
            root,
            "MuzzleDevice",
            SocketPositionOr(
                source,
                root,
                "MuzzleSocket",
                new Vector3(0.0f, 0.0f, 0.28f - length)));
        AddMarker(muzzleDevice, "MuzzleDeviceTip", Vector3.Zero);
        var suppressor = AddMarker(
            root,
            "Suppressor",
            new Vector3(0.0f, 0.0f, 0.28f - length));
        AddMarker(suppressor, "SuppressorTip", Vector3.Zero);
        var opticRailContactPosition = SocketPositionOr(
            source,
            root,
            "OpticRailSocket",
            new Vector3(0.0f, 0.16f, -0.16f));
        var rearIronSight = FindOptionalNode(root, "RearIronSight");
        var frontIronSight = FindOptionalNode(root, "FrontIronSight");
        var canHideAuthoredIronSights = FindOptionalNode(root, "IronSightGeometry") is not null
            || rearIronSight is not null && frontIronSight is not null;
        if (!HasIntegratedScope(platform)
            && source is not null
            && !canHideAuthoredIronSights)
        {
            // Several licensed pistol and legacy SMG meshes weld their mechanical
            // sights into the main body. They cannot be hidden without modifying
            // the source art, so derive a safe rail plane once when the visual is
            // instantiated. This keeps every external housing above the welded
            // silhouette and adds no per-frame bounds work.
            var highestAuthoredPoint = HighestMeshPointBelow(root, root);
            if (float.IsFinite(highestAuthoredPoint))
            {
                opticRailContactPosition.Y = Mathf.Max(
                    opticRailContactPosition.Y,
                    highestAuthoredPoint);
            }
        }
        var opticMount = AddMarker(root, "OpticMount", opticRailContactPosition);
        AddMarker(root, "OpticRailContact", opticRailContactPosition);
        AddMarker(opticMount, "OpticReticleAnchor", Vector3.Zero);
        return opticMount;
    }

    private static float HighestMeshPointBelow(Node3D geometryRoot, Node3D ancestor)
    {
        var highest = float.NegativeInfinity;
        foreach (var mesh in MeshesBelow(geometryRoot))
        {
            if (mesh.Mesh is null || !IsVisibleBelowAncestor(mesh, ancestor))
            {
                continue;
            }

            var bounds = mesh.GetAabb();
            var toAncestor = TransformBelowAncestor(mesh, ancestor);
            for (var endpoint = 0; endpoint < 8; endpoint++)
            {
                highest = Mathf.Max(
                    highest,
                    (toAncestor * bounds.GetEndpoint(endpoint)).Y);
            }
        }
        return highest;
    }

    private static bool IsVisibleBelowAncestor(Node3D node, Node3D ancestor)
    {
        for (Node? current = node; current is not null; current = current.GetParent())
        {
            if (current is Node3D spatial && !spatial.Visible)
            {
                return false;
            }
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }
        }
        return false;
    }

    private static Vector3 SocketPositionOr(
        Node3D? source,
        Node3D root,
        string socketName,
        Vector3 fallback)
    {
        var socket = source is null ? null : FindOptionalNode(source, socketName);
        return socket is null
            ? fallback
            : TransformBelowAncestor(socket, root).Origin;
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
        WeaponBuild? weaponBuild = null,
        EquipmentItem? helmet = null,
        EquipmentItem? bodyArmor = null,
        EquipmentItem? backpack = null)
        => InstantiatePreviewOperator(visualId, weaponBuild, helmet, bodyArmor, backpack, buildObserver: null);

    private static AuthoredPreviewOperatorVisual InstantiatePreviewOperator(
        OperatorVisualId visualId,
        Action<PreviewOperatorBuildStage, Node3D, Node3D?> buildObserver)
        => InstantiatePreviewOperator(visualId, weaponBuild: null, helmet: null, bodyArmor: null, backpack: null, buildObserver: buildObserver);

    private static AuthoredPreviewOperatorVisual InstantiatePreviewOperator(
        OperatorVisualId visualId,
        WeaponBuild? weaponBuild,
        EquipmentItem? helmet,
        EquipmentItem? bodyArmor,
        EquipmentItem? backpack,
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
                // Inventory/deployment previews are product shots. Keep the
                // authored rest pose so every operator stands squarely instead
                // of freezing the weight-shifted idle animation.
                animationPlayer.Stop();

                if (weaponBuild is not null)
                {
                    var previewVisual = new AuthoredOperatorVisual(source, visualId);
                    pendingWeapon = InstantiateWeapon(weaponBuild.Platform, firstPerson: false);
                    previewVisual.AttachWeapon(pendingWeapon, weaponBuild);
                    previewVisual.SetWeaponReadied(false);
                    pendingWeapon = null;
                }
                var equipmentVisual = new AuthoredOperatorVisual(source, visualId);
                equipmentVisual.SetEquipment(helmet, bodyArmor, backpack);
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
        var weaponBody = FindOptionalNode(source, "WeaponBodyGeometry");
        var sourceBounds = weaponBody is not null
            ? ComputeBounds(weaponBody)
            : ComputeBounds(source);
        if (sourceBounds.MeshCount == 0
            || sourceBounds.Size.X <= 0.001f
            || sourceBounds.Size.Y <= 0.001f
            || sourceBounds.Size.Z <= 0.001f)
        {
            source.Free();
            throw new InvalidOperationException("GSh-18 model has no usable geometry bounds.");
        }

        // The reloadable DCC asset already uses the shared metre-space weapon
        // contract at the authored first-person size: X is lateral, Y is up,
        // and the muzzle points toward -Z. Do not normalize that viewmodel from
        // only the body bounds; doing so enlarges the weapon while its hand-fit
        // anchors remain in the original metre-space frame.
        var scale = firstPerson
            ? 1.0f
            : Gsh18PreviewLength / Gsh18FirstPersonLength;
        var wrapper = new Node3D
        {
            Name = "AuthoredGsh18Visual",
            Scale = Vector3.One * scale
        };
        RequireNode(source, "SpareMagazine").Visible = false;
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
            WeaponPlatform.AK74 => Ak47WorldScenePath,
            WeaponPlatform.ScarL =>
                "res://assets/models/steel_tide_scarl/scarl_reloadable.glb",
            WeaponPlatform.M24 =>
                "res://assets/models/steel_tide_reloadable_weapons/m24_reloadable.glb",
            WeaponPlatform.MP5A5 =>
                "res://assets/models/steel_tide_reloadable_weapons/mp5a5_reloadable.glb",
            WeaponPlatform.M3A1 => Smg45WeaponScenePath,
            WeaponPlatform.AXMC =>
                "res://assets/models/steel_tide_reloadable_weapons/axmc_reloadable.glb",
            WeaponPlatform.AWM =>
                "res://assets/models/steel_tide_reloadable_weapons/awm_reloadable.glb",
            WeaponPlatform.VSS =>
                "res://assets/models/steel_tide_reloadable_weapons/vss_reloadable.glb",
            WeaponPlatform.P226 =>
                "res://assets/models/steel_tide_reloadable_weapons/p226_reloadable.glb",
            WeaponPlatform.M1911 =>
                "res://assets/models/steel_tide_reloadable_weapons/m1911_reloadable.glb",
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
            var allBounds = ComputeBounds(root);
            var body = FindOptionalNode(root, "WeaponBodyGeometry");
            var bodyBounds = body is not null
                ? ComputeBounds(body)
                : allBounds;
            var size = bodyBounds.Size * root.Scale.Abs();
            var geometry = CountGeometry(MeshesBelow(root));
            return new CombatModelInspection(
                true,
                true,
                allBounds.MeshCount,
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

    internal static (int MeshCount, Vector3 Size, Vector3 Center) ComputeBounds(Node3D root)
    {
        return ComputeBounds(root, Transform3D.Identity);
    }

    internal static (int MeshCount, Vector3 Size, Vector3 Center) ComputeLocalBounds(
        Node3D root)
    {
        // AccumulateBounds normally includes root.Transform and therefore
        // reports bounds in the root's parent frame. Seed it with the inverse
        // so fallback reload contacts are stored in the moving node's own frame.
        return ComputeBounds(root, root.Transform.AffineInverse());
    }

    private static (int MeshCount, Vector3 Size, Vector3 Center) ComputeBounds(
        Node3D root,
        Transform3D initialTransform)
    {
        var minimum = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        var maximum = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        var meshCount = 0;
        AccumulateBounds(root, initialTransform, ref minimum, ref maximum, ref meshCount);
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
