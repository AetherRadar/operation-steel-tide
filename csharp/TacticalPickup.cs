using Godot;

namespace OperationSteelTide;

public enum TacticalPickupKind
{
    Ammunition,
    ArmorPlate
}

[GlobalClass]
public partial class TacticalPickup : Area3D
{
    public TacticalPickupKind Kind { get; set; } = TacticalPickupKind.Ammunition;

    private Node3D _visualRoot = null!;
    private float _baseHeight;
    private bool _consumed;

    public override void _Ready()
    {
        CollisionLayer = 0;
        CollisionMask = 1;
        Monitoring = true;
        AddChild(new CollisionShape3D
        {
            Position = new Vector3(0, 0.28f, 0),
            Shape = new SphereShape3D { Radius = 0.7f }
        });
        BuildVisual();
        BodyEntered += OnBodyEntered;
        _baseHeight = _visualRoot.Position.Y;
    }

    public override void _Process(double delta)
    {
        if (_consumed)
        {
            return;
        }
        _visualRoot.RotateY((float)delta * 0.75f);
        var position = _visualRoot.Position;
        position.Y = _baseHeight + Mathf.Sin(Time.GetTicksMsec() * 0.004f) * 0.035f;
        _visualRoot.Position = position;
    }

    private void BuildVisual()
    {
        _visualRoot = new Node3D { Position = new Vector3(0, 0.24f, 0) };
        AddChild(_visualRoot);
        var isAmmo = Kind == TacticalPickupKind.Ammunition;
        var shell = new StandardMaterial3D
        {
            AlbedoColor = isAmmo ? new Color(0.16f, 0.25f, 0.17f) : new Color(0.13f, 0.24f, 0.34f),
            Metallic = 0.42f,
            Roughness = 0.5f,
            EmissionEnabled = true,
            Emission = isAmmo ? new Color(0.03f, 0.14f, 0.06f) : new Color(0.02f, 0.12f, 0.22f),
            EmissionEnergyMultiplier = 0.7f
        };
        var trim = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.04f, 0.055f, 0.052f),
            Metallic = 0.78f,
            Roughness = 0.3f
        };
        _visualRoot.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = isAmmo ? new Vector3(0.46f, 0.24f, 0.32f) : new Vector3(0.36f, 0.46f, 0.09f) },
            MaterialOverride = shell
        });
        _visualRoot.AddChild(new MeshInstance3D
        {
            Position = isAmmo ? new Vector3(0, 0.14f, 0) : Vector3.Zero,
            Mesh = new BoxMesh { Size = isAmmo ? new Vector3(0.22f, 0.04f, 0.12f) : new Vector3(0.42f, 0.07f, 0.12f) },
            MaterialOverride = trim
        });
        _visualRoot.AddChild(new OmniLight3D
        {
            LightColor = isAmmo ? new Color(0.25f, 0.9f, 0.46f) : new Color(0.26f, 0.62f, 1.0f),
            LightEnergy = 0.55f,
            OmniRange = 1.25f,
            ShadowEnabled = false
        });
    }

    private void OnBodyEntered(Node3D body)
    {
        if (_consumed || body is not TacticalPlayer player)
        {
            return;
        }
        var collected = Kind == TacticalPickupKind.Ammunition
            ? player.TryCollectAmmo(36)
            : player.TryCollectArmorPlate();
        if (!collected)
        {
            return;
        }
        _consumed = true;
        SetDeferred(Area3D.PropertyName.Monitoring, false);
        _visualRoot.Visible = false;
        CallDeferred(Node.MethodName.QueueFree);
    }
}
