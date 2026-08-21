using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OperationSteelTide;

internal sealed class AuthoredWeaponVisual
{
    private readonly Vector3 _chargingHandleRestPosition;

    public AuthoredWeaponVisual(Node3D root)
    {
        Root = root;
        Magazine = CombatModelLibrary.RequireNode(root, "Magazine");
        SpareMagazine = CombatModelLibrary.RequireNode(root, "SpareMagazine");
        ChargingHandle = CombatModelLibrary.RequireNode(root, "ChargingHandle");
        Stock = CombatModelLibrary.RequireNode(root, "Stock");
        Foregrip = CombatModelLibrary.RequireNode(root, "Foregrip");
        MuzzleDevice = CombatModelLibrary.RequireNode(root, "MuzzleDevice");
        Suppressor = CombatModelLibrary.RequireNode(root, "Suppressor");
        OpticMount = CombatModelLibrary.RequireNode(root, "OpticMount");
        _chargingHandleRestPosition = ChargingHandle.Position;
    }

    public Node3D Root { get; }
    public Node3D Magazine { get; }
    public Node3D SpareMagazine { get; }
    public Node3D ChargingHandle { get; }
    public Node3D Stock { get; }
    public Node3D Foregrip { get; }
    public Node3D MuzzleDevice { get; }
    public Node3D Suppressor { get; }
    public Node3D OpticMount { get; }

    public void Configure(WeaponBuild build)
    {
        var suppressed = build.Attachments.TryGetValue(AttachmentSlot.Muzzle, out var muzzleId)
            && muzzleId == "muzzle_suppressor";
        var hasForegrip = build.Attachments.ContainsKey(AttachmentSlot.Grip);
        MuzzleDevice.Visible = !suppressed;
        Suppressor.Visible = suppressed;
        Foregrip.Visible = hasForegrip;
        OpticMount.Visible = build.Attachments.ContainsKey(AttachmentSlot.Optic);
    }

    public void SyncMechanisms(Node3D magazine, Node3D spareMagazine, Node3D chargingHandle)
    {
        Magazine.Transform = magazine.Transform;
        Magazine.Visible = magazine.Visible;
        SpareMagazine.Transform = spareMagazine.Transform;
        SpareMagazine.Visible = spareMagazine.Visible;
        ChargingHandle.Transform = chargingHandle.Transform;
    }

    public void SyncMechanismState(Node3D magazine, Node3D spareMagazine, Node3D chargingHandle)
    {
        Magazine.Visible = magazine.Visible;
        SpareMagazine.Visible = spareMagazine.Visible;
        var reloadOffset = chargingHandle.Position.Z + 0.05f;
        ChargingHandle.Position = _chargingHandleRestPosition + new Vector3(0.0f, 0.0f, reloadOffset);
    }
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
    public AuthoredPreviewOperatorVisual(Node3D root)
    {
        Root = root;
    }

    public Node3D Root { get; }
}

internal sealed class AuthoredOperatorVisual
{
    private const float FieldWeaponScale = 0.42f;
    private static readonly Quaternion ReadiedWeaponRotation = new(
        -0.9934235f,
        0.0130106f,
        0.0974600f,
        0.0586704f);
    private readonly Skeleton3D _skeleton;
    private AuthoredWeaponVisual? _weapon;
    private bool _weaponReadied;

    public AuthoredOperatorVisual(Node3D root)
    {
        Root = root;
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
        var primaryHandDistance = rightHand.Origin.DistanceTo(weaponOrigin);
        var supportHandOffset = weapon.Foregrip.GlobalPosition - leftHand.Origin;
        var supportHandDistance = supportHandOffset.Length();
        var handSeparation = rightHand.Origin.DistanceTo(leftHand.Origin);
        var muzzleOffset = weapon.MuzzleDevice.GlobalPosition - weaponOrigin;
        var stockOffset = weapon.Stock.GlobalPosition - weaponOrigin;
        var valid = primaryHandDistance <= 0.025f
            && supportHandDistance <= 0.16f
            && muzzleOffset.Z <= -0.44f
            && Mathf.Abs(muzzleOffset.X) <= 0.16f
            && Mathf.Abs(muzzleOffset.Y) <= 0.12f
            && stockOffset.Z >= 0.14f;
        return new OperatorRifleFitInspection(
            valid,
            primaryHandDistance,
            supportHandDistance,
            supportHandOffset,
            handSeparation,
            weaponOrigin,
            muzzleOffset,
            stockOffset);
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
            ? ReadiedWeaponRotation
            : Quaternion.Identity;
        var socketRelativeToRoot = TransformRelativeToAncestor(socket, Root);
        var inheritedScale = Mathf.Max(0.0001f, socketRelativeToRoot.Basis.Scale.X);
        _weapon.Root.Scale = Vector3.One * (FieldWeaponScale / inheritedScale);
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
        GearTintForDiagnostics = gearTint;
        GearOverlayCountForDiagnostics = 0;
        var gearOverlay = new StandardMaterial3D
        {
            AlbedoColor = gearTint,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            Metallic = 0.08f,
            Roughness = 0.72f
        };
        foreach (var mesh in CombatModelLibrary.MeshesBelow(Root))
        {
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
    float SupportHandDistance,
    Vector3 SupportHandOffset,
    float HandSeparation,
    Vector3 WeaponOrigin,
    Vector3 MuzzleOffset,
    Vector3 StockOffset);

internal readonly record struct CombatModelInspection(
    bool Loaded,
    bool RequiredNodes,
    int MeshCount,
    int MaterialCount,
    Vector3 Size);

internal static partial class CombatModelLibrary
{
    internal const string WeaponScenePath = "res://assets/models/steel_tide_m4a1/steel_tide_m4a1.glb";
    private const string QuaterniusWeaponRoot = "res://assets/models/quaternius_ultimate_guns";
    internal const string OperatorScenePath = "res://assets/models/bamen_military_soldier/bamen_military_soldier_animated.glb";
    internal const string PreviewOperatorScenePath = "res://assets/models/bamen_military_soldier/bamen_military_soldier.glb";
    internal const string Gsh18ScenePath = "res://assets/models/tastytony_gsh18/low-poly_gsh-18.glb";
    internal const string DesertEagleScenePath = "res://assets/models/elizion_desert_eagle/desert_eagle.glb";

    private const float Gsh18FirstPersonLength = 0.64f;
    private const float Gsh18PreviewLength = 0.78f;
    private const float DesertEagleFirstPersonLength = 0.82f;
    private const float DesertEaglePreviewLength = 1.05f;
    private const float OperatorPreviewHeight = 2.55f;
    private const float AnimatedOperatorHeight = 1.86f;
    private static readonly Vector3 PreviewOperatorSourceSize = new(1.3053f, 2.1079f, 0.4252f);
    private static readonly Vector3 PreviewOperatorSourceCenter = new(0.0f, 1.04885f, 0.0258f);

    private static readonly string[] WeaponNodes =
    {
        "SteelTideM4A1", "Magazine", "SpareMagazine", "ChargingHandle",
        "Stock", "Foregrip", "MuzzleDevice", "Suppressor", "OpticMount"
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

    private static readonly string[] Gsh18Nodes =
    {
        "Armature", "Skeleton3D"
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
        var visual = new AuthoredWeaponVisual(root);
        if (!firstPerson)
        {
            visual.SpareMagazine.Visible = false;
        }
        return visual;
    }

    public static AuthoredOperatorVisual InstantiateOperator(WeaponBuild? weaponBuild = null)
    {
        var source = InstantiateRequired(OperatorScenePath, OperatorNodes);
        var wrapper = new Node3D { Name = "AuthoredOperatorVisual" };
        var sourcePresentation = new Node3D
        {
            Name = "AnimatedOperatorPresentation",
            RotationDegrees = new Vector3(0.0f, 180.0f, 0.0f),
            Scale = Vector3.One * (AnimatedOperatorHeight / PreviewOperatorSourceSize.Y)
        };
        sourcePresentation.AddChild(source);
        wrapper.AddChild(sourcePresentation);
        var visual = new AuthoredOperatorVisual(wrapper);
        var carriedBuild = weaponBuild ?? WeaponCatalog.Build(WeaponPlatform.M4A1, 0);
        visual.AttachWeapon(
            InstantiateWeapon(carriedBuild.Platform, firstPerson: false),
            carriedBuild);
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
        AddWeaponMarkers(root, targetLength);
        if (firstPerson)
        {
            foreach (var geometry in GeometryBelow(root))
            {
                geometry.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            }
        }
        return new AuthoredWeaponVisual(root);
    }

    private static void AddWeaponMarkers(Node3D root, float length)
    {
        AddMarker(root, "Magazine", new Vector3(0.0f, -0.2f, -0.18f));
        AddMarker(root, "SpareMagazine", new Vector3(-0.3f, -0.62f, -0.18f));
        AddMarker(root, "ChargingHandle", new Vector3(0.075f, 0.085f, -0.05f));
        AddMarker(root, "Stock", new Vector3(0.0f, 0.0f, 0.28f));
        AddMarker(root, "Foregrip", new Vector3(0.0f, -0.16f, 0.18f - length * 0.55f));
        AddMarker(root, "MuzzleDevice", new Vector3(0.0f, 0.0f, 0.28f - length));
        AddMarker(root, "Suppressor", new Vector3(0.0f, 0.0f, 0.28f - length));
        AddMarker(root, "OpticMount", new Vector3(0.0f, 0.16f, -0.16f));
    }

    private static void AddMarker(Node3D root, string name, Vector3 position)
    {
        root.AddChild(new Marker3D { Name = name, Position = position });
    }

    public static AuthoredPreviewOperatorVisual InstantiatePreviewOperator()
    {
        var source = InstantiateRequired(PreviewOperatorScenePath, PreviewOperatorNodes);
        source.Position = -PreviewOperatorSourceCenter;
        var wrapper = new Node3D
        {
            Name = "AuthoredPreviewOperatorVisual",
            Scale = Vector3.One * (OperatorPreviewHeight / PreviewOperatorSourceSize.Y)
        };
        wrapper.AddChild(source);
        return new AuthoredPreviewOperatorVisual(wrapper);
    }

    public static AuthoredGsh18Visual InstantiateGsh18(bool firstPerson)
    {
        var source = InstantiateRequired(Gsh18ScenePath, Gsh18Nodes);
        RemoveStagingNode(source, "Lamp");
        RemoveStagingNode(source, "Camera");
        var sourceBounds = ComputeBounds(source);
        if (sourceBounds.MeshCount == 0 || sourceBounds.Size.X <= 0.001f)
        {
            source.Free();
            throw new InvalidOperationException("GSh-18 model has no usable geometry bounds.");
        }

        source.Position = -sourceBounds.Center;
        var targetLength = firstPerson ? Gsh18FirstPersonLength : Gsh18PreviewLength;
        var wrapper = new Node3D
        {
            Name = "AuthoredGsh18Visual",
            RotationDegrees = new Vector3(0.0f, 90.0f, 0.0f),
            Scale = Vector3.One * (targetLength / sourceBounds.Size.X)
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
            return new CombatModelInspection(
                true,
                true,
                bounds.MeshCount,
                CountMaterials(root),
                bounds.Size);
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
        var length = platform switch
        {
            WeaponPlatform.AWM => 1.9f,
            WeaponPlatform.M24 or WeaponPlatform.AXMC => 1.62f,
            WeaponPlatform.AK74 or WeaponPlatform.ScarL or WeaponPlatform.VSS => 1.42f,
            WeaponPlatform.MP5A5 or WeaponPlatform.M3A1 => 1.08f,
            WeaponPlatform.P226 or WeaponPlatform.M1911 => 0.68f,
            WeaponPlatform.GSh18 => Gsh18FirstPersonLength,
            WeaponPlatform.DesertEagle => DesertEagleFirstPersonLength,
            _ => 1.36f
        };
        return firstPerson ? length : length * 1.12f;
    }

    public static CombatModelInspection InspectOperator()
    {
        Node3D? root = null;
        try
        {
            root = InstantiateOperator().Root;
            var scale = AnimatedOperatorHeight / PreviewOperatorSourceSize.Y;
            return new CombatModelInspection(
                true,
                true,
                MeshesBelow(root).Count(),
                CountMaterials(root),
                PreviewOperatorSourceSize * scale);
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
    {
        Node3D? root = null;
        try
        {
            root = InstantiatePreviewOperator().Root;
            var scale = OperatorPreviewHeight / PreviewOperatorSourceSize.Y;
            return new CombatModelInspection(
                true,
                true,
                MeshesBelow(root).Count(),
                CountMaterials(root),
                PreviewOperatorSourceSize * scale);
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
            return new CombatModelInspection(
                true,
                true,
                bounds.MeshCount,
                CountMaterials(root),
                bounds.Size);
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
            return new CombatModelInspection(
                true,
                true,
                bounds.MeshCount,
                CountMaterials(root),
                bounds.Size);
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
            return new CombatModelInspection(
                true,
                required,
                bounds.MeshCount,
                CountMaterials(root),
                bounds.Size);
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
