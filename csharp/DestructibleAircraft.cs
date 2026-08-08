using Godot;

namespace OperationSteelTide;

[GlobalClass]
public partial class DestructibleAircraft : StaticBody3D
{
    public FreightTerminalWorld? Main { get; set; }
    public float Health { get; private set; } = 240.0f;
    public float MaxHealth { get; private set; } = 240.0f;
    public bool IsDestroyed { get; private set; }
    public bool IsHostile { get; private set; } = true;
    public int AttackSalvosFired { get; private set; }
    public float LastAttackDamage { get; private set; }

    private const float EngageRange = 118.0f;
    private const float FireCooldown = 3.1f;
    private const float ShellDamage = 48.0f;
    private const float ShellBlastRadius = 12.5f;

    private Node3D _visual = null!;
    private CollisionShape3D _collider = null!;
    private Tween? _flightTween;
    private Vector3 _start = new(-52, 39, -78);
    private Vector3 _end = new(72, 42, -92);
    private float _fallVelocity;
    private bool _falling;
    private float _fireCooldown;
    private float _acquireTimer;
    private Node3D? _currentTarget;
    private readonly RandomNumberGenerator _rng = new();

    public override void _Ready()
    {
        _rng.Randomize();
        CollisionLayer = 1;
        CollisionMask = 0;
        AddToGroup("aircraft");
        BuildVisuals();
        StartPatrol();
        // Always tick: combat while flying, fall when crippled.
        SetPhysicsProcess(true);
        _fireCooldown = 1.2f;
    }

    public override void _ExitTree()
    {
        // Infinite patrol tweens retain Variant pool pages if left running at process exit.
        _flightTween?.Kill();
        _flightTween = null;
        SetPhysicsProcess(false);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (IsDestroyed)
        {
            return;
        }

        var dt = (float)delta;
        if (_falling)
        {
            UpdateFall(dt);
            return;
        }

        UpdateCombat(dt);
    }

    /// <summary>Headless/diagnostic: force one attack tick against an explicit target.</summary>
    public bool TryAttackTarget(Node3D target, bool ignoreCooldown = false)
    {
        if (IsDestroyed || _falling || !IsHostile || target is null || !GodotObject.IsInstanceValid(target))
        {
            return false;
        }
        if (!ignoreCooldown && _fireCooldown > 0.0f)
        {
            return false;
        }
        return FireAt(target);
    }

    public bool TakeDamage(float amount, Vector3 hitPosition, Node? attacker = null)
    {
        if (IsDestroyed || amount <= 0.0f)
        {
            return false;
        }

        Health = Mathf.Max(0.0f, Health - amount);
        Main?.SpawnImpact(hitPosition, Vector3.Up);
        if (Health > MaxHealth * 0.45f)
        {
            return false;
        }

        if (Health > 0.0f)
        {
            if (!_falling && Health <= MaxHealth * 0.35f)
            {
                BeginFall();
            }

            return false;
        }

        BeginFall();
        return true;
    }

    private void UpdateCombat(float dt)
    {
        _fireCooldown = Mathf.Max(0.0f, _fireCooldown - dt);
        _acquireTimer -= dt;
        if (_acquireTimer <= 0.0f)
        {
            _acquireTimer = 0.45f;
            _currentTarget = AcquireTarget();
        }

        if (_currentTarget is null || !GodotObject.IsInstanceValid(_currentTarget))
        {
            return;
        }

        // Face roughly toward the intrusion target while patrolling.
        var toTarget = _currentTarget.GlobalPosition - GlobalPosition;
        toTarget.Y = 0.0f;
        if (toTarget.LengthSquared() > 0.01f)
        {
            var yaw = Mathf.Atan2(-toTarget.X, -toTarget.Z);
            Rotation = new Vector3(0.0f, Mathf.LerpAngle(Rotation.Y, yaw, dt * 1.8f), 0.0f);
        }

        if (_fireCooldown <= 0.0f && GlobalPosition.DistanceTo(_currentTarget.GlobalPosition) <= EngageRange)
        {
            FireAt(_currentTarget);
        }
    }

    private Node3D? AcquireTarget()
    {
        if (Main is null || !IsHostile)
        {
            return null;
        }

        Node3D? best = null;
        var bestDistance = EngageRange;
        foreach (var combatant in Main.GetHostileAircraftTargets())
        {
            if (combatant is null || !GodotObject.IsInstanceValid(combatant))
            {
                continue;
            }
            var distance = GlobalPosition.DistanceTo(combatant.GlobalPosition);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = combatant;
            }
        }
        return best;
    }

    private bool FireAt(Node3D target)
    {
        if (Main is null)
        {
            return false;
        }

        var muzzle = GlobalPosition + Vector3.Down * 1.4f - GlobalBasis.Z * 2.2f;
        var aim = target.GlobalPosition + Vector3.Up * 0.4f;
        // Lead slightly for moving targets.
        if (target is CharacterBody3D body)
        {
            aim += body.Velocity * 0.35f;
        }
        aim += new Vector3(
            _rng.RandfRange(-2.4f, 2.4f),
            _rng.RandfRange(-0.2f, 0.8f),
            _rng.RandfRange(-2.4f, 2.4f));

        var damage = ShellDamage * _rng.RandfRange(0.92f, 1.08f);
        LastAttackDamage = damage;
        AttackSalvosFired++;
        _fireCooldown = FireCooldown;

        // Physical bomb: deliberately slow enough to dodge, but impossible to shoot down.
        Main.SpawnAircraftShell(muzzle, aim, damage, ShellBlastRadius, this);
        Main.SpawnTracer(muzzle, aim, new Color(1.0f, 0.4f, 0.12f));
        return true;
    }

    private void UpdateFall(float dt)
    {
        _fallVelocity += 18.0f * dt;
        Position += new Vector3(
            _rng.RandfRange(-0.4f, 0.4f) * dt * 8.0f,
            -_fallVelocity * dt,
            _rng.RandfRange(-0.3f, 0.3f) * dt * 6.0f);
        RotateZ(dt * 1.8f);
        RotateX(dt * 0.7f);

        if (Position.Y <= 1.2f)
        {
            Crash();
        }
    }

    private void BeginFall()
    {
        if (_falling || IsDestroyed)
        {
            return;
        }

        _falling = true;
        _flightTween?.Kill();
        Main?.SpawnImpact(GlobalPosition, Vector3.Up);
    }

    private void Crash()
    {
        if (IsDestroyed)
        {
            return;
        }

        IsDestroyed = true;
        SetPhysicsProcess(false);
        Main?.Explode(GlobalPosition + Vector3.Up * 1.2f, 14.0f, 160.0f, this);
        _visual.Visible = false;
        _collider.Disabled = true;
        CollisionLayer = 0;
        QueueFree();
    }

    private void StartPatrol()
    {
        Position = _start;
        _flightTween?.Kill();
        _flightTween = CreateTween().SetLoops();
        _flightTween.TweenProperty(this, "position", _end, 62.0)
            .From(_start)
            .SetTrans(Tween.TransitionType.Linear);
        _flightTween.TweenCallback(Callable.From(() =>
        {
            if (!IsDestroyed && !_falling)
            {
                Position = _start;
            }
        }));
    }

    private void BuildVisuals()
    {
        _visual = new Node3D { Name = "AircraftVisual" };
        AddChild(_visual);

        _collider = new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(8.4f, 2.4f, 10.8f) }
        };
        AddChild(_collider);

        var dark = Mat("aircraft_dark", new Color(0.055f, 0.072f, 0.073f), 0.72f, 0.4f);
        var steel = Mat("aircraft_steel", new Color(0.28f, 0.32f, 0.31f), 0.78f, 0.38f);
        var glass = Mat("aircraft_glass", new Color(0.05f, 0.16f, 0.18f), 0.82f, 0.12f);
        var navigation = Mat("aircraft_navigation", new Color(0.68f, 0.08f, 0.035f), 0.18f, 0.22f, new Color(1.0f, 0.06f, 0.02f));

        MeshBox(_visual, Vector3.Zero, new Vector3(7.2f, 0.9f, 1.2f), dark);
        MeshBox(_visual, new Vector3(2.9f, 0.12f, 0), new Vector3(1.65f, 0.74f, 1.05f), glass, new Vector3(0, 0, -0.14f));
        MeshBox(_visual, new Vector3(-0.3f, 0.05f, 0), new Vector3(2.5f, 0.16f, 10.4f), steel);
        MeshBox(_visual, new Vector3(-2.75f, 0.38f, 0), new Vector3(1.3f, 0.12f, 4.2f), steel);
        MeshBox(_visual, new Vector3(-3.0f, 1.02f, 0), new Vector3(1.3f, 1.8f, 0.18f), dark, new Vector3(0, 0, -0.18f));
        foreach (var z in new[] { -3.65f, 3.65f })
        {
            MeshBox(_visual, new Vector3(0.45f, 0.25f, z), new Vector3(1.8f, 0.72f, 0.68f), dark);
            _visual.AddChild(new MeshInstance3D
            {
                Position = new Vector3(1.25f, 0.25f, z),
                Rotation = new Vector3(0, 0, Mathf.Pi / 2.0f),
                Mesh = new TorusMesh { InnerRadius = 1.4f, OuterRadius = 1.46f, Rings = 28, RingSegments = 6 },
                MaterialOverride = steel
            });
            MeshBox(_visual, new Vector3(0.0f, 0.02f, z), new Vector3(0.16f, 0.16f, 0.16f), navigation);
        }

        var plate = new Label3D
        {
            Position = new Vector3(0.0f, 1.4f, 0.0f),
            Text = "HOSTILE TILT-ROTOR",
            FontSize = 18,
            OutlineSize = 6,
            Modulate = new Color(1.0f, 0.45f, 0.28f),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            VisibilityRangeEnd = 90.0f,
            VisibilityRangeEndMargin = 12.0f
        };
        _visual.AddChild(plate);
    }

    private static StandardMaterial3D Mat(
        string _name,
        Color color,
        float metallic,
        float roughness,
        Color emission = default)
    {
        var material = new StandardMaterial3D
        {
            AlbedoColor = color,
            Metallic = metallic,
            Roughness = roughness
        };
        if (emission != default)
        {
            material.EmissionEnabled = true;
            material.Emission = emission;
            material.EmissionEnergyMultiplier = 1.6f;
        }

        return material;
    }

    private static void MeshBox(
        Node3D parent,
        Vector3 position,
        Vector3 size,
        Godot.Material material,
        Vector3 rotation = default)
    {
        parent.AddChild(new MeshInstance3D
        {
            Position = position,
            Rotation = rotation,
            Mesh = new BoxMesh { Size = size },
            MaterialOverride = material
        });
    }
}
