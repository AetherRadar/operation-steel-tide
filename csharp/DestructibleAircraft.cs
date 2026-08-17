using Godot;

namespace OperationSteelTide;

[GlobalClass]
public partial class DestructibleAircraft : StaticBody3D
{
    public FreightTerminalWorld? Main { get; set; }
    public float Health { get; private set; } = 1200.0f;
    public float MaxHealth { get; private set; } = 1200.0f;
    public bool IsDestroyed { get; private set; }
    public bool IsHostile { get; private set; } = true;
    public bool SupplyDropReleased { get; private set; }
    public int AttackSalvosFired { get; private set; }
    public float LastAttackDamage { get; private set; }
    public float LastPatrolStepDistance { get; private set; }
    public float PatrolDistanceTravelled { get; private set; }

    public const float CruiseSpeed = 17.5f;
    private const float PatrolRadiusX = 62.0f;
    private const float PatrolRadiusZ = 30.0f;
    private const float PatrolAltitude = 40.5f;
    private const float PatrolAltitudeSwing = 1.5f;
    private static readonly Vector3 PatrolCenter = new(10.0f, PatrolAltitude, -78.0f);

    private Node3D _visual = null!;
    private CollisionShape3D _collider = null!;
    private float _patrolPhase;
    private Vector3 _flightDirection = Vector3.Right;
    private bool _rejoiningPatrol;
    private float _fallVelocity;
    private bool _falling;
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

        UpdateTargeting(dt);
        if (_currentTarget is not null && GodotObject.IsInstanceValid(_currentTarget))
        {
            UpdateAttackFlight(_currentTarget, dt);
        }
        else
        {
            UpdatePatrol(dt);
        }
        UpdateCombat();
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
        if (IsDestroyed || (_falling && Health <= 0.0f) || amount <= 0.0f)
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

    internal void SetPatrolPhaseForDiagnostics(float phase)
    {
        _currentTarget = null;
        _acquireTimer = 1.0f;
        IsAttackOrbitActive = false;
        _rejoiningPatrol = false;
        _patrolPhase = Mathf.PosMod(phase, Mathf.Tau);
        Position = PatrolPosition(_patrolPhase);
        LastPatrolStepDistance = 0.0f;
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
        var crashPosition = GlobalPosition;
        Main?.Explode(crashPosition + Vector3.Up * 1.2f, 14.0f, 160.0f, this);
        _visual.Visible = false;
        _collider.Disabled = true;
        CollisionLayer = 0;
        if (!SupplyDropReleased && Main is not null)
        {
            Main.SpawnAircraftSupplyDrop(crashPosition, GetRid());
            SupplyDropReleased = true;
        }
        QueueFree();
    }

    private void StartPatrol()
    {
        _patrolPhase = Mathf.Pi;
        Position = PatrolPosition(_patrolPhase);
        LastPatrolStepDistance = 0.0f;
        PatrolDistanceTravelled = 0.0f;
    }

    private void UpdatePatrol(float dt)
    {
        if (IsAttackOrbitActive)
        {
            IsAttackOrbitActive = false;
            AttackHorizontalDistance = float.PositiveInfinity;
            _patrolPhase = ClosestPatrolPhase(Position);
            _rejoiningPatrol = true;
        }

        if (_rejoiningPatrol)
        {
            var patrolDestination = PatrolPosition(_patrolPhase);
            ApplyFlightStep(Position.MoveToward(patrolDestination, CruiseSpeed * dt), countPatrolDistance: true, dt);
            if (Position.DistanceTo(patrolDestination) <= 0.05f)
            {
                _rejoiningPatrol = false;
            }
            return;
        }

        var sin = Mathf.Sin(_patrolPhase);
        var cos = Mathf.Cos(_patrolPhase);
        var tangent = new Vector3(
            -PatrolRadiusX * sin,
            -PatrolAltitudeSwing * sin,
            PatrolRadiusZ * cos);
        _patrolPhase += CruiseSpeed * dt / Mathf.Max(0.01f, tangent.Length());
        if (_patrolPhase >= Mathf.Tau)
        {
            _patrolPhase -= Mathf.Tau;
        }

        var nextPosition = PatrolPosition(_patrolPhase);
        ApplyFlightStep(nextPosition, countPatrolDistance: true, dt);
    }

    private void ApplyFlightStep(Vector3 nextPosition, bool countPatrolDistance, float dt)
    {
        var step = nextPosition - Position;
        LastPatrolStepDistance = step.Length();
        if (countPatrolDistance)
        {
            PatrolDistanceTravelled += LastPatrolStepDistance;
        }
        Position = nextPosition;

        var horizontalDirection = new Vector3(step.X, 0.0f, step.Z);
        if (horizontalDirection.LengthSquared() <= 0.0001f)
        {
            return;
        }

        _flightDirection = horizontalDirection.Normalized();
        var yaw = Mathf.Atan2(-_flightDirection.Z, _flightDirection.X);
        Rotation = new Vector3(0.0f, Mathf.LerpAngle(Rotation.Y, yaw, dt * 5.0f), 0.0f);
    }

    private static float ClosestPatrolPhase(Vector3 position)
    {
        return Mathf.PosMod(
            Mathf.Atan2(
                (position.Z - PatrolCenter.Z) / PatrolRadiusZ,
                (position.X - PatrolCenter.X) / PatrolRadiusX),
            Mathf.Tau);
    }

    private static Vector3 PatrolPosition(float phase)
    {
        return PatrolCenter + new Vector3(
            PatrolRadiusX * Mathf.Cos(phase),
            PatrolAltitudeSwing * Mathf.Cos(phase),
            PatrolRadiusZ * Mathf.Sin(phase));
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
