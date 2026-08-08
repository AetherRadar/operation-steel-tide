using Godot;

namespace OperationSteelTide;

public enum InventoryPreviewKind
{
    Rifle,
    Knife,
    Helmet,
    BodyArmor,
    Backpack
}

[GlobalClass]
public partial class InventoryModelPreview : SubViewportContainer
{
    private InventoryPreviewKind _kind;
    private EquipmentItem? _equipment;
    private WeaponBuild? _weapon;
    private string _knifeSkinId = KnifeSkinCatalog.DefaultId;
    private SubViewport? _viewport;
    private Node3D? _modelRoot;
    private Camera3D? _camera;
    private int _renderRevision;

    public void Configure(
        InventoryPreviewKind kind,
        EquipmentItem? equipment = null,
        WeaponBuild? weapon = null,
        string knifeSkinId = KnifeSkinCatalog.DefaultId)
    {
        _kind = kind;
        _equipment = equipment?.Clone();
        _weapon = weapon?.Clone();
        _knifeSkinId = knifeSkinId;
        if (IsInsideTree())
        {
            RebuildModel();
        }
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Stretch = true;
        _viewport = new SubViewport
        {
            Size = new Vector2I(320, 180),
            TransparentBg = true,
            OwnWorld3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always
        };
        AddChild(_viewport);

        var environment = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color,
            BackgroundColor = new Color(0, 0, 0, 0),
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color(0.58f, 0.68f, 0.65f),
            AmbientLightEnergy = 1.15f,
            ReflectedLightSource = Godot.Environment.ReflectionSource.Disabled
        };
        _viewport.AddChild(new WorldEnvironment { Environment = environment });
        _modelRoot = new Node3D();
        _viewport.AddChild(_modelRoot);
        _camera = new Camera3D
        {
            Projection = Camera3D.ProjectionType.Orthogonal,
            KeepAspect = Camera3D.KeepAspectEnum.Width,
            Size = 2.2f,
            Position = new Vector3(0, 0.08f, 4.0f),
            Current = true
        };
        _viewport.AddChild(_camera);
        _camera.LookAt(Vector3.Zero, Vector3.Up);
        var keyLight = new DirectionalLight3D
        {
            LightColor = new Color(0.9f, 0.98f, 0.95f),
            LightEnergy = 1.8f,
            RotationDegrees = new Vector3(-34, -28, 0),
            ShadowEnabled = false
        };
        _viewport.AddChild(keyLight);
        _viewport.AddChild(new OmniLight3D
        {
            Position = new Vector3(-1.5f, -0.3f, 2.0f),
            LightColor = new Color(0.3f, 0.7f, 1.0f),
            LightEnergy = 1.6f,
            OmniRange = 5.0f
        });
        RebuildModel();
    }

    private void RebuildModel()
    {
        if (_modelRoot is null || _camera is null)
        {
            return;
        }
        foreach (var child in _modelRoot.GetChildren())
        {
            _modelRoot.RemoveChild(child);
            child.QueueFree();
        }
        _modelRoot.Rotation = Vector3.Zero;
        _modelRoot.Scale = Vector3.One;
        switch (_kind)
        {
            case InventoryPreviewKind.Rifle:
                BuildRifle(_modelRoot);
                _camera.Size = 2.75f;
                _modelRoot.RotationDegrees = new Vector3(-8, 13, -2);
                break;
            case InventoryPreviewKind.Knife:
                BuildKnife(_modelRoot);
                _camera.Size = 2.05f;
                _modelRoot.RotationDegrees = new Vector3(-11, 18, 5);
                break;
            case InventoryPreviewKind.Helmet:
                BuildHelmet(_modelRoot);
                _camera.Size = 1.8f;
                _modelRoot.RotationDegrees = new Vector3(-6, -22, 0);
                break;
            case InventoryPreviewKind.BodyArmor:
                BuildBodyArmor(_modelRoot);
                _camera.Size = 2.15f;
                _modelRoot.RotationDegrees = new Vector3(-4, -15, 0);
                break;
            case InventoryPreviewKind.Backpack:
                BuildBackpack(_modelRoot);
                _camera.Size = 2.15f;
                _modelRoot.RotationDegrees = new Vector3(-4, -18, 0);
                break;
        }
        RequestRender();
    }

    private void RequestRender()
    {
        if (_viewport is null)
        {
            return;
        }
        _viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
        FreezeRenderAfterWarmup(++_renderRevision);
    }

    private async void FreezeRenderAfterWarmup(int revision)
    {
        await ToSignal(GetTree().CreateTimer(0.16f), SceneTreeTimer.SignalName.Timeout);
        if (revision == _renderRevision && IsInstanceValid(_viewport))
        {
            _viewport!.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled;
        }
    }

    private void BuildRifle(Node3D root)
    {
        var platform = _weapon?.Platform ?? WeaponPlatform.M4A1;
        var metal = platform switch
        {
            WeaponPlatform.AK74 => new Color(0.19f, 0.2f, 0.18f),
            WeaponPlatform.ScarL => new Color(0.43f, 0.36f, 0.24f),
            WeaponPlatform.M24 => new Color(0.18f, 0.24f, 0.17f),
            WeaponPlatform.MP5A5 => new Color(0.055f, 0.065f, 0.06f),
            _ => new Color(0.12f, 0.15f, 0.145f)
        };
        var furniture = platform switch
        {
            WeaponPlatform.AK74 => new Color(0.35f, 0.19f, 0.09f),
            WeaponPlatform.M24 => new Color(0.2f, 0.31f, 0.18f),
            _ => metal.Lightened(0.12f)
        };
        var steel = new Color(0.44f, 0.5f, 0.48f);
        var definition = WeaponCatalog.Weapon(platform);
        var receiverLength = definition.ReceiverLength;
        var barrelLength = definition.BarrelLength;
        Box(root, new Vector3(receiverLength, 0.24f, 0.18f), new Vector3(0, 0, 0), metal, 0.55f);
        Box(root, new Vector3(Mathf.Max(0.3f, receiverLength * 1.05f), 0.17f, 0.16f), new Vector3(-receiverLength * 0.95f, 0.01f, 0), furniture, 0.25f);
        Box(root, new Vector3(Mathf.Max(0.3f, barrelLength * 0.72f), 0.16f, 0.14f), new Vector3(receiverLength * 0.92f, 0, 0), furniture, 0.28f);
        Cylinder(root, 0.045f, barrelLength, new Vector3(receiverLength * 0.8f + barrelLength * 0.55f, 0.01f, 0), new Vector3(0, 0, Mathf.Pi / 2), steel, 0.75f, 0.72f);
        Box(root, new Vector3(0.22f, 0.08f, 0.17f), new Vector3(-0.87f, 0, 0), furniture.Darkened(0.08f), 0.2f);
        var magazineHeight = platform == WeaponPlatform.M24 ? 0.2f : platform == WeaponPlatform.MP5A5 ? 0.5f : 0.44f;
        Box(root, new Vector3(0.16f, magazineHeight, 0.15f), new Vector3(0.06f, -magazineHeight * 0.58f, 0), furniture.Darkened(0.06f), 0.18f, rotation: new Vector3(0, 0, platform == WeaponPlatform.AK74 ? -0.12f : 0.04f));
        Box(root, new Vector3(0.13f, 0.32f, 0.13f), new Vector3(-0.2f, -0.25f, 0), furniture, 0.18f, rotation: new Vector3(0, 0, -0.18f));
        Box(root, new Vector3(0.36f, 0.045f, 0.18f), new Vector3(0.0f, 0.16f, 0), steel.Darkened(0.2f), 0.65f);
        if (_weapon?.Attachments.ContainsKey(AttachmentSlot.Optic) != false)
        {
            Box(root, new Vector3(0.24f, 0.13f, 0.14f), new Vector3(0.02f, 0.26f, 0), steel.Darkened(0.12f), 0.7f);
            Box(root, new Vector3(0.12f, 0.045f, 0.17f), new Vector3(0.02f, 0.18f, 0), steel, 0.72f);
        }
        if (_weapon?.Attachments.ContainsKey(AttachmentSlot.Muzzle) == true)
        {
            Cylinder(root, 0.075f, 0.3f, new Vector3(1.57f, 0.01f, 0), new Vector3(0, 0, Mathf.Pi / 2), steel.Darkened(0.2f), 0.82f, 0.7f);
        }
    }

    private void BuildKnife(Node3D root)
    {
        var skin = KnifeSkinCatalog.Definition(_knifeSkinId);
        var steel = skin.BladeColor;
        var edge = skin.EdgeColor;
        var grip = skin.GripColor;
        Box(root, new Vector3(1.0f, 0.16f, 0.065f), new Vector3(-0.36f, 0.02f, 0), steel, 0.9f, rotation: new Vector3(0, 0, -0.035f));
        Box(root, new Vector3(0.8f, 0.025f, 0.075f), new Vector3(-0.48f, -0.065f, 0.002f), edge, 0.95f);
        Box(root, new Vector3(0.09f, 0.38f, 0.09f), new Vector3(0.18f, 0, 0), steel.Darkened(0.25f), 0.75f);
        Cylinder(root, 0.12f, 0.72f, new Vector3(0.58f, 0, 0), new Vector3(0, 0, Mathf.Pi / 2), grip, 0.05f, 0.88f);
        for (var ring = 0; ring < 5; ring++)
        {
            Box(root, new Vector3(0.035f, 0.245f, 0.16f), new Vector3(0.3f + ring * 0.14f, 0, 0), grip.Lightened(0.14f), 0.08f);
        }
        Cylinder(root, 0.135f, 0.08f, new Vector3(0.98f, 0, 0), new Vector3(0, 0, Mathf.Pi / 2), steel.Darkened(0.22f), 0.65f, 0.68f);
        for (var tooth = 0; tooth < 4; tooth++)
        {
            Box(root, new Vector3(0.07f, 0.035f, 0.08f), new Vector3(-0.23f - tooth * 0.12f, 0.115f, 0), steel.Darkened(0.12f), 0.8f);
        }
    }

    private void BuildHelmet(Node3D root)
    {
        var heavy = _equipment?.DefinitionId == "helmet_heavy";
        var shell = heavy ? new Color(0.28f, 0.25f, 0.16f) : new Color(0.22f, 0.27f, 0.21f);
        Sphere(root, 0.57f, 0.76f, new Vector3(0, 0.12f, 0), shell, 0.12f, 0.82f);
        Box(root, new Vector3(0.72f, 0.12f, 0.72f), new Vector3(0, -0.18f, 0.02f), shell.Darkened(0.08f), 0.1f);
        Box(root, new Vector3(0.28f, 0.1f, 0.12f), new Vector3(0, 0.45f, -0.5f), shell.Darkened(0.22f), 0.4f);
        Box(root, new Vector3(0.13f, 0.3f, 0.09f), new Vector3(-0.55f, 0.05f, 0), new Color(0.08f, 0.1f, 0.09f), 0.15f);
        Box(root, new Vector3(0.13f, 0.3f, 0.09f), new Vector3(0.55f, 0.05f, 0), new Color(0.08f, 0.1f, 0.09f), 0.15f);
        Cylinder(root, 0.16f, 0.13f, new Vector3(-0.58f, -0.2f, 0), new Vector3(0, 0, Mathf.Pi / 2), new Color(0.055f, 0.065f, 0.06f), 0.12f, 0.9f);
        Cylinder(root, 0.16f, 0.13f, new Vector3(0.58f, -0.2f, 0), new Vector3(0, 0, Mathf.Pi / 2), new Color(0.055f, 0.065f, 0.06f), 0.12f, 0.9f);
    }

    private void BuildBodyArmor(Node3D root)
    {
        var heavy = _equipment?.DefinitionId == "armor_heavy";
        var fabric = heavy ? new Color(0.18f, 0.2f, 0.16f) : new Color(0.18f, 0.24f, 0.2f);
        var webbing = fabric.Lightened(0.14f);
        Box(root, new Vector3(0.88f, 1.05f, 0.25f), new Vector3(0, 0, 0), fabric, 0.05f, 0.95f);
        Box(root, new Vector3(0.62f, 0.7f, 0.08f), new Vector3(0, 0.08f, -0.17f), fabric.Lightened(0.08f), 0.28f, 0.85f);
        Box(root, new Vector3(0.22f, 0.57f, 0.18f), new Vector3(-0.54f, 0.03f, 0), fabric.Darkened(0.08f), 0.05f, 0.95f);
        Box(root, new Vector3(0.22f, 0.57f, 0.18f), new Vector3(0.54f, 0.03f, 0), fabric.Darkened(0.08f), 0.05f, 0.95f);
        Box(root, new Vector3(0.2f, 0.5f, 0.12f), new Vector3(-0.3f, 0.66f, 0), webbing, 0.08f, 0.9f, new Vector3(0, 0, -0.18f));
        Box(root, new Vector3(0.2f, 0.5f, 0.12f), new Vector3(0.3f, 0.66f, 0), webbing, 0.08f, 0.9f, new Vector3(0, 0, 0.18f));
        for (var pouch = -1; pouch <= 1; pouch++)
        {
            Box(root, new Vector3(0.23f, 0.28f, 0.18f), new Vector3(pouch * 0.27f, -0.48f, -0.23f), webbing.Darkened(0.05f), 0.04f, 0.95f);
            Box(root, new Vector3(0.18f, 0.035f, 0.205f), new Vector3(pouch * 0.27f, -0.37f, -0.25f), new Color(0.42f, 0.45f, 0.38f), 0.35f);
        }
        for (var row = 0; row < 3; row++)
        {
            Box(root, new Vector3(0.66f, 0.025f, 0.035f), new Vector3(0, 0.25f - row * 0.13f, -0.31f), webbing.Lightened(0.1f), 0.05f);
        }
    }

    private void BuildBackpack(Node3D root)
    {
        var heavy = _equipment?.DefinitionId == "pack_heavy";
        var fabric = heavy ? new Color(0.22f, 0.24f, 0.18f) : new Color(0.18f, 0.23f, 0.2f);
        var trim = fabric.Lightened(0.16f);
        Box(root, new Vector3(0.92f, 1.18f, 0.42f), new Vector3(0, 0, 0), fabric, 0.03f, 0.98f);
        Box(root, new Vector3(0.82f, 0.26f, 0.48f), new Vector3(0, 0.52f, -0.03f), trim, 0.05f, 0.95f);
        Box(root, new Vector3(0.66f, 0.43f, 0.25f), new Vector3(0, -0.34f, -0.31f), fabric.Lightened(0.08f), 0.04f, 0.98f);
        Box(root, new Vector3(0.18f, 0.75f, 0.12f), new Vector3(-0.55f, -0.05f, 0.08f), trim.Darkened(0.08f), 0.04f, 0.98f);
        Box(root, new Vector3(0.18f, 0.75f, 0.12f), new Vector3(0.55f, -0.05f, 0.08f), trim.Darkened(0.08f), 0.04f, 0.98f);
        Box(root, new Vector3(0.08f, 0.92f, 0.07f), new Vector3(-0.3f, 0, 0.27f), trim, 0.2f, 0.9f);
        Box(root, new Vector3(0.08f, 0.92f, 0.07f), new Vector3(0.3f, 0, 0.27f), trim, 0.2f, 0.9f);
        for (var buckle = -1; buckle <= 1; buckle += 2)
        {
            Box(root, new Vector3(0.11f, 0.09f, 0.08f), new Vector3(buckle * 0.22f, -0.31f, -0.47f), new Color(0.08f, 0.09f, 0.085f), 0.38f, 0.75f);
        }
    }

    private static MeshInstance3D Box(
        Node3D parent,
        Vector3 size,
        Vector3 position,
        Color color,
        float metallic = 0.0f,
        float roughness = 0.82f,
        Vector3? rotation = null)
    {
        return Part(parent, new BoxMesh { Size = size }, position, rotation ?? Vector3.Zero, color, metallic, roughness);
    }

    private static MeshInstance3D Cylinder(
        Node3D parent,
        float radius,
        float height,
        Vector3 position,
        Vector3 rotation,
        Color color,
        float metallic,
        float roughness)
    {
        return Part(parent, new CylinderMesh
        {
            TopRadius = radius,
            BottomRadius = radius,
            Height = height,
            RadialSegments = 16
        }, position, rotation, color, metallic, roughness);
    }

    private static MeshInstance3D Sphere(
        Node3D parent,
        float radius,
        float height,
        Vector3 position,
        Color color,
        float metallic,
        float roughness)
    {
        return Part(parent, new SphereMesh
        {
            Radius = radius,
            Height = height,
            RadialSegments = 24,
            Rings = 12
        }, position, Vector3.Zero, color, metallic, roughness);
    }

    private static MeshInstance3D Part(
        Node3D parent,
        PrimitiveMesh mesh,
        Vector3 position,
        Vector3 rotation,
        Color color,
        float metallic,
        float roughness)
    {
        var part = new MeshInstance3D
        {
            Mesh = mesh,
            Position = position,
            Rotation = rotation,
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = color,
                Metallic = metallic,
                Roughness = roughness
            }
        };
        parent.AddChild(part);
        return part;
    }
}
