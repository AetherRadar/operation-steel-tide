using Godot;

namespace OperationSteelTide;

[GlobalClass]
public partial class DriveableVehicle : CharacterBody3D
{
    public FreightTerminalWorld? Main { get; set; }
    public string DisplayName { get; private set; } = "SERVICE TRUCK";
    public float Health { get; private set; } = 180.0f;
    public float MaxHealth { get; private set; } = 180.0f;
    public bool IsDestroyed { get; private set; }
    public bool HasDriver => _driver is not null && GodotObject.IsInstanceValid(_driver);

    private const float MaxSpeed = 17.5f;
    private const float ReverseSpeed = 7.5f;
    private const float Acceleration = 18.0f;
    private const float BrakeForce = 28.0f;
    private const float TurnRate = 1.55f;
    private const float RamDamagePerSpeed = 9.5f;

    private TacticalPlayer? _driver;
    private Node3D _bodyRoot = null!;
    private Node3D _cabinInterior = null!;
    private Node3D _seat = null!;
    private CollisionShape3D _collider = null!;
    private Label3D _prompt = null!;
    private MeshInstance3D _steeringWheel = null!;
    private readonly RandomNumberGenerator _rng = new();
    private float _steer;
    private float _speed;
    private float _damageCooldown;
    private float _wheelSpin;
    private float _blockTime;
    private float _blockedToastCooldown;
    private MeshInstance3D[] _wheels = System.Array.Empty<MeshInstance3D>();
    private Color _bodyColor = new(0.24f, 0.36f, 0.29f);

    public void Configure(string displayName, Color bodyColor, float maxHealth = 180.0f)
    {
        DisplayName = displayName;
        _bodyColor = bodyColor;
        MaxHealth = maxHealth;
        Health = maxHealth;
    }

    public override void _Ready()
    {
        _rng.Randomize();
        CollisionLayer = 1;
        CollisionMask = 1 | 2;
        FloorSnapLength = 0.35f;
        MotionMode = MotionModeEnum.Grounded;
        UpDirection = Vector3.Up;
        AddToGroup("vehicles");
        BuildVisuals();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (IsDestroyed)
        {
            return;
        }

        var dt = (float)delta;
        _damageCooldown = Mathf.Max(0.0f, _damageCooldown - dt);
        if (!HasDriver)
        {
            Coast(dt);
            return;
        }

        if (_driver is not null && _driver.IsDead)
        {
            ExitDriver(forced: true);
            Coast(dt);
            return;
        }

        Drive(dt);
        SpinWheels(dt);
        AnimateSteering(dt);
        ApplyRamDamage();
    }

    public bool TryEnter(TacticalPlayer player)
    {
        if (IsDestroyed || HasDriver || player.IsDead || player.IsInVehicle)
        {
            return false;
        }

        _driver = player;
        player.EnterVehicle(this, _seat);
        _prompt.Visible = false;
        SetCabinInteriorVisible(true);
        return true;
    }

    public void ExitDriver(bool forced = false)
    {
        if (_driver is null)
        {
            return;
        }

        var player = _driver;
        _driver = null;
        // Exit to the left of the cab (driver side), slightly forward of center.
        var exitPoint = GlobalPosition
            + GlobalBasis.X * -2.4f
            + Vector3.Up * 0.25f
            - GlobalBasis.Z * 0.6f;
        player.ExitVehicle(exitPoint, forced);
        _prompt.Visible = !IsDestroyed;
        SetCabinInteriorVisible(false);
        _speed *= 0.35f;
    }

    public bool TakeDamage(float amount, Vector3 hitPosition, Node? attacker = null)
    {
        if (IsDestroyed || amount <= 0.0f)
        {
            return false;
        }

        Health = Mathf.Max(0.0f, Health - amount);
        SpawnSpark(hitPosition);
        if (Health > 0.0f)
        {
            return false;
        }

        Destroy(attacker);
        return true;
    }

    public bool RestoreHealth(float amount)
    {
        if (IsDestroyed || amount <= 0.0f || Health >= MaxHealth)
        {
            return false;
        }
        Health = Mathf.Min(MaxHealth, Health + amount);
        return true;
    }

    public string InteractionLabel(string language)
    {
        if (IsDestroyed)
        {
            return GameLocalization.IsChinese(language) ? "载具已损毁" : "VEHICLE DESTROYED";
        }

        if (HasDriver)
        {
            return GameLocalization.IsChinese(language) ? "下车  //  F" : "EXIT VEHICLE  //  F";
        }

        return GameLocalization.IsChinese(language)
            ? $"上车  //  {DisplayName}"
            : $"ENTER  //  {DisplayName}";
    }

    private void Drive(float dt)
    {
        var throttle = 0.0f;
        if (Input.IsActionPressed(GameInputActions.MoveForward))
        {
            throttle += 1.0f;
        }
        if (Input.IsActionPressed(GameInputActions.MoveBackward))
        {
            throttle -= 1.0f;
        }

        var steerInput = 0.0f;
        if (Input.IsActionPressed(GameInputActions.MoveLeft))
        {
            steerInput += 1.0f;
        }
        if (Input.IsActionPressed(GameInputActions.MoveRight))
        {
            steerInput -= 1.0f;
        }

        var targetSpeed = throttle >= 0.0f ? throttle * MaxSpeed : throttle * ReverseSpeed;
        var accel = Mathf.Abs(targetSpeed) < Mathf.Abs(_speed) ? BrakeForce : Acceleration;
        if (Mathf.Abs(throttle) < 0.01f)
        {
            accel = BrakeForce * 0.55f;
            targetSpeed = 0.0f;
        }

        _speed = Mathf.MoveToward(_speed, targetSpeed, accel * dt);
        _steer = Mathf.MoveToward(_steer, steerInput, dt * 4.5f);

        if (Mathf.Abs(_speed) > 0.35f)
        {
            var turn = _steer * TurnRate * Mathf.Clamp(Mathf.Abs(_speed) / MaxSpeed, 0.2f, 1.0f) * Mathf.Sign(_speed);
            RotateY(turn * dt);
        }

        // Godot cameras look along -Z; vehicle forward is -Z so W drives into the windshield.
        var velocity = -GlobalBasis.Z * _speed;
        if (!IsOnFloor())
        {
            velocity.Y = Velocity.Y - 22.0f * dt;
        }
        else
        {
            velocity.Y = -0.2f;
        }

        Velocity = velocity;
        var positionBeforeMove = GlobalPosition;
        MoveAndSlide();
        AlignToGround(dt);

        // Stuck assist: low props (curbs, bins, stalls) must not silently wall the truck.
        _blockedToastCooldown = Mathf.Max(0.0f, _blockedToastCooldown - dt);
        var expectedMove = Mathf.Abs(_speed) * dt;
        var actualMove = GlobalPosition.DistanceTo(positionBeforeMove);
        if (Mathf.Abs(throttle) > 0.5f && expectedMove > 0.02f && actualMove < expectedMove * 0.35f)
        {
            _blockTime += dt;
            if (_blockTime > 0.22f && TryCurbStep(Mathf.Sign(throttle)))
            {
                _blockTime = 0.0f;
            }
            else if (_blockTime > 0.75f && _blockedToastCooldown <= 0.0f)
            {
                _blockedToastCooldown = 3.0f;
                Main?.ShowVehicleBlockedToast();
            }
        }
        else
        {
            _blockTime = Mathf.Max(0.0f, _blockTime - dt * 3.0f);
        }
    }

    /// <summary>
    /// Climb low obstacles in the drive direction: probe for a walkable top surface
    /// just ahead and lift the chassis onto it (curbs, bins, low crates).
    /// </summary>
    private bool TryCurbStep(float throttleSign)
    {
        if (!IsOnFloor())
        {
            return false;
        }
        var forward = -GlobalBasis.Z * Mathf.Sign(throttleSign);
        forward.Y = 0.0f;
        if (forward.LengthSquared() < 0.001f)
        {
            return false;
        }
        forward = forward.Normalized();
        // Probe just past the bumper at a few depths so thin props are not missed.
        foreach (var probeDistance in new[] { 2.8f, 3.4f, 4.0f })
        {
            var origin = GlobalPosition + Vector3.Up * 0.82f + forward * probeDistance;
            if (!PhysicsRaycast.TryHit(
                    GetWorld3D(),
                    origin,
                    origin + Vector3.Down * 1.35f,
                    GetRid(),
                    CollisionMask,
                    out var hit))
            {
                continue;
            }
            var top = hit.Position;
            var lift = top.Y - GlobalPosition.Y;
            if (lift < 0.08f || lift > 0.62f)
            {
                continue;
            }
            if (PhysicsRaycast.HasHit(
                    GetWorld3D(),
                    top + Vector3.Up * 0.25f,
                    top + Vector3.Up * 2.3f,
                    GetRid(),
                    CollisionMask))
            {
                continue;
            }
            GlobalPosition = new Vector3(GlobalPosition.X, top.Y + 0.12f, GlobalPosition.Z);
            Velocity = new Vector3(Velocity.X, 0.0f, Velocity.Z);
            _speed *= 0.72f;
            return true;
        }
        return false;
    }

    private void Coast(float dt)
    {
        _speed = Mathf.MoveToward(_speed, 0.0f, BrakeForce * 0.8f * dt);
        var velocity = -GlobalBasis.Z * _speed;
        if (!IsOnFloor())
        {
            velocity.Y = Velocity.Y - 22.0f * dt;
        }
        else
        {
            velocity.Y = -0.2f;
        }

        Velocity = velocity;
        MoveAndSlide();
        AlignToGround(dt);
        SpinWheels(dt);
        AnimateSteering(dt);
    }

    private void AlignToGround(float dt)
    {
        if (!IsOnFloor())
        {
            return;
        }

        var forward = -GlobalBasis.Z;
        forward.Y = 0.0f;
        if (forward.LengthSquared() < 0.0001f)
        {
            return;
        }

        forward = forward.Normalized();
        var targetBasis = Basis.LookingAt(forward, Vector3.Up);
        GlobalBasis = GlobalBasis.Orthonormalized().Slerp(targetBasis.Orthonormalized(), dt * 8.0f);
    }

    private void ApplyRamDamage()
    {
        if (_damageCooldown > 0.0f || Mathf.Abs(_speed) < 4.5f)
        {
            return;
        }

        for (var i = 0; i < GetSlideCollisionCount(); i++)
        {
            var collision = GetSlideCollision(i);
            var collider = collision.GetCollider();
            if (collider is not Node node)
            {
                continue;
            }

            var impactSpeed = Mathf.Abs(_speed);
            var damage = impactSpeed * RamDamagePerSpeed;
            var hitPoint = collision.GetPosition();
            var dealt = false;
            if (node is EnemyOperator enemy && !enemy.IsDead)
            {
                enemy.TakeDamage(damage, hitPoint, _driver);
                dealt = true;
            }
            else if (node is ExplosiveBarrel barrel)
            {
                barrel.TakeDamage(damage * 0.65f, hitPoint, _driver);
                dealt = true;
            }
            else if (node is TacticalPlayer player && player != _driver && !player.IsDead)
            {
                player.TakeDamage(damage * 0.55f, hitPoint, _driver);
                dealt = true;
            }
            else if (node is DriveableVehicle other && other != this && !other.IsDestroyed)
            {
                other.TakeDamage(damage * 0.4f, hitPoint, _driver);
                dealt = true;
            }
            else if (collision.GetNormal().Dot(-GlobalBasis.Z.Normalized()) > 0.35f)
            {
                TakeDamage(impactSpeed * 1.8f, hitPoint, this);
                _speed *= 0.25f;
                dealt = true;
            }

            if (dealt)
            {
                _damageCooldown = 0.28f;
                _speed *= 0.55f;
                Main?.SpawnImpact(hitPoint, collision.GetNormal());
                break;
            }
        }
    }

    private void Destroy(Node? attacker)
    {
        if (IsDestroyed)
        {
            return;
        }

        IsDestroyed = true;
        if (HasDriver)
        {
            ExitDriver(forced: true);
        }

        Main?.Explode(GlobalPosition + Vector3.Up * 0.8f, 7.5f, 95.0f, attacker ?? this);
        _prompt.Visible = false;
        _bodyRoot.Visible = false;
        if (IsInstanceValid(_cabinInterior))
        {
            _cabinInterior.Visible = false;
        }
        _collider.Disabled = true;
        CollisionLayer = 0;
        CollisionMask = 0;
        SetPhysicsProcess(false);
        QueueFree();
    }

    private void SpinWheels(float dt)
    {
        _wheelSpin += _speed * dt * 1.4f;
        foreach (var wheel in _wheels)
        {
            if (GodotObject.IsInstanceValid(wheel))
            {
                wheel.Rotation = new Vector3(_wheelSpin, 0.0f, Mathf.Pi * 0.5f);
            }
        }
    }

    private void AnimateSteering(float dt)
    {
        if (!GodotObject.IsInstanceValid(_steeringWheel))
        {
            return;
        }

        var targetRoll = _steer * 0.85f;
        var rotation = _steeringWheel.Rotation;
        rotation.Z = Mathf.Lerp(rotation.Z, targetRoll, dt * 10.0f);
        _steeringWheel.Rotation = rotation;
    }

    private void SetCabinInteriorVisible(bool visible)
    {
        if (GodotObject.IsInstanceValid(_cabinInterior))
        {
            _cabinInterior.Visible = visible;
        }
    }

    private void SpawnSpark(Vector3 position)
    {
        Main?.SpawnImpact(position, Vector3.Up);
    }

    private void BuildVisuals()
    {
        _bodyRoot = new Node3D { Name = "VehicleBody" };
        AddChild(_bodyRoot);

        // Chassis collision: cab toward -Z (forward), bed toward +Z.
        _collider = new CollisionShape3D
        {
            Position = new Vector3(0.0f, 0.95f, 0.15f),
            Shape = new BoxShape3D { Size = new Vector3(2.15f, 1.7f, 5.6f) }
        };
        AddChild(_collider);

        // Driver seat inside the cab, looking along -Z (through the windshield).
        _seat = new Node3D
        {
            Name = "DriverSeat",
            Position = new Vector3(0.42f, 1.18f, -0.95f)
        };
        AddChild(_seat);

        var body = Mat("vehicle_body", _bodyColor, 0.55f, 0.48f);
        var dark = Mat("vehicle_dark", new Color(0.04f, 0.05f, 0.05f), 0.78f, 0.4f);
        var glass = Mat("vehicle_glass", new Color(0.12f, 0.28f, 0.32f, 0.22f), 0.15f, 0.08f);
        glass.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        glass.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        var light = Mat("vehicle_light", new Color(0.95f, 0.88f, 0.55f), 0.1f, 0.25f, new Color(1.0f, 0.85f, 0.35f));
        var tire = Mat("vehicle_tire", new Color(0.05f, 0.05f, 0.05f), 0.05f, 0.92f);
        var chrome = Mat("vehicle_chrome", new Color(0.55f, 0.58f, 0.6f), 0.92f, 0.22f);
        var dash = Mat("vehicle_dash", new Color(0.12f, 0.13f, 0.12f), 0.15f, 0.72f);
        var seatMat = Mat("vehicle_seat", new Color(0.1f, 0.12f, 0.11f), 0.05f, 0.88f);
        var trim = Mat("vehicle_trim", new Color(0.08f, 0.09f, 0.08f), 0.35f, 0.55f);

        // --- Exterior: cab faces -Z (forward), cargo bed faces +Z (rear) ---
        MeshBox(_bodyRoot, new Vector3(0.0f, 0.62f, 0.1f), new Vector3(2.05f, 0.32f, 5.45f), dark);

        // Cargo bed (rear / +Z)
        MeshBox(_bodyRoot, new Vector3(0.0f, 1.15f, 1.35f), new Vector3(2.0f, 0.95f, 2.9f), body);
        MeshBox(_bodyRoot, new Vector3(0.0f, 1.72f, 1.35f), new Vector3(1.95f, 0.08f, 2.85f), dark);
        MeshBox(_bodyRoot, new Vector3(-0.95f, 1.45f, 1.35f), new Vector3(0.08f, 0.7f, 2.8f), body);
        MeshBox(_bodyRoot, new Vector3(0.95f, 1.45f, 1.35f), new Vector3(0.08f, 0.7f, 2.8f), body);

        // Cab shell with open windows (frames only — no solid walls blocking the view)
        // Floor
        MeshBox(_bodyRoot, new Vector3(0.0f, 0.95f, -1.15f), new Vector3(1.95f, 0.12f, 2.15f), dark);
        // Roof
        MeshBox(_bodyRoot, new Vector3(0.0f, 2.15f, -1.15f), new Vector3(1.98f, 0.1f, 2.2f), body);
        // Rear cab wall (separates cab from bed)
        MeshBox(_bodyRoot, new Vector3(0.0f, 1.55f, -0.05f), new Vector3(1.95f, 1.15f, 0.12f), body);
        // A-pillars / side pillars (leave large window openings)
        MeshBox(_bodyRoot, new Vector3(-0.95f, 1.55f, -2.15f), new Vector3(0.1f, 1.15f, 0.12f), body);
        MeshBox(_bodyRoot, new Vector3(0.95f, 1.55f, -2.15f), new Vector3(0.1f, 1.15f, 0.12f), body);
        MeshBox(_bodyRoot, new Vector3(-0.95f, 1.55f, -0.2f), new Vector3(0.1f, 1.15f, 0.12f), body);
        MeshBox(_bodyRoot, new Vector3(0.95f, 1.55f, -0.2f), new Vector3(0.1f, 1.15f, 0.12f), body);
        // Door sills (lower side panels under windows)
        MeshBox(_bodyRoot, new Vector3(-0.98f, 1.15f, -1.15f), new Vector3(0.08f, 0.45f, 1.9f), body);
        MeshBox(_bodyRoot, new Vector3(0.98f, 1.15f, -1.15f), new Vector3(0.08f, 0.45f, 1.9f), body);
        // Roof rails / upper window frames
        MeshBox(_bodyRoot, new Vector3(-0.95f, 2.05f, -1.15f), new Vector3(0.08f, 0.08f, 1.95f), trim);
        MeshBox(_bodyRoot, new Vector3(0.95f, 2.05f, -1.15f), new Vector3(0.08f, 0.08f, 1.95f), trim);

        // Windshield frame only (open glass aperture for the driver)
        MeshBox(_bodyRoot, new Vector3(0.0f, 2.08f, -2.2f), new Vector3(1.75f, 0.08f, 0.08f), chrome);
        MeshBox(_bodyRoot, new Vector3(0.0f, 1.2f, -2.2f), new Vector3(1.75f, 0.08f, 0.08f), chrome);
        MeshBox(_bodyRoot, new Vector3(-0.88f, 1.64f, -2.2f), new Vector3(0.08f, 0.9f, 0.08f), chrome);
        MeshBox(_bodyRoot, new Vector3(0.88f, 1.64f, -2.2f), new Vector3(0.08f, 0.9f, 0.08f), chrome);
        // Thin translucent windshield pane (alpha) so you still see out clearly
        MeshBox(_bodyRoot, new Vector3(0.0f, 1.64f, -2.18f), new Vector3(1.65f, 0.82f, 0.03f), glass);

        // Side window glass (alpha)
        MeshBox(_bodyRoot, new Vector3(-0.99f, 1.62f, -1.15f), new Vector3(0.03f, 0.72f, 1.65f), glass);
        MeshBox(_bodyRoot, new Vector3(0.99f, 1.62f, -1.15f), new Vector3(0.03f, 0.72f, 1.65f), glass);

        // Hood / nose in front of cab
        MeshBox(_bodyRoot, new Vector3(0.0f, 1.05f, -2.55f), new Vector3(1.9f, 0.55f, 0.85f), body);
        MeshBox(_bodyRoot, new Vector3(0.0f, 0.72f, -2.95f), new Vector3(2.05f, 0.28f, 0.22f), dark);
        MeshBox(_bodyRoot, new Vector3(-0.72f, 1.0f, -2.95f), new Vector3(0.32f, 0.2f, 0.12f), light);
        MeshBox(_bodyRoot, new Vector3(0.72f, 1.0f, -2.95f), new Vector3(0.32f, 0.2f, 0.12f), light);
        // Rear bumper / lights
        MeshBox(_bodyRoot, new Vector3(0.0f, 0.58f, 2.85f), new Vector3(2.05f, 0.28f, 0.22f), dark);
        MeshBox(_bodyRoot, new Vector3(-0.72f, 0.95f, 2.82f), new Vector3(0.28f, 0.16f, 0.1f), Mat("tail_light", new Color(0.7f, 0.08f, 0.05f), 0.1f, 0.3f, new Color(1.0f, 0.05f, 0.02f)));
        MeshBox(_bodyRoot, new Vector3(0.72f, 0.95f, 2.82f), new Vector3(0.28f, 0.16f, 0.1f), Mat("tail_light_r", new Color(0.7f, 0.08f, 0.05f), 0.1f, 0.3f, new Color(1.0f, 0.05f, 0.02f)));

        // Wheels
        _wheels = new MeshInstance3D[4];
        var wheelIndex = 0;
        foreach (var x in new[] { -0.95f, 0.95f })
        {
            foreach (var z in new[] { 1.55f, -1.65f })
            {
                var wheel = new MeshInstance3D
                {
                    Position = new Vector3(x, 0.48f, z),
                    Rotation = new Vector3(0.0f, 0.0f, Mathf.Pi * 0.5f),
                    Mesh = new CylinderMesh
                    {
                        TopRadius = 0.48f,
                        BottomRadius = 0.48f,
                        Height = 0.28f,
                        RadialSegments = 16
                    },
                    MaterialOverride = tire
                };
                _bodyRoot.AddChild(wheel);
                _wheels[wheelIndex++] = wheel;
            }
        }

        // --- Cabin interior (visible when driving) ---
        _cabinInterior = new Node3D { Name = "CabinInterior", Visible = false };
        AddChild(_cabinInterior);

        // Dashboard
        MeshBox(_cabinInterior, new Vector3(0.0f, 1.28f, -1.85f), new Vector3(1.7f, 0.22f, 0.55f), dash);
        MeshBox(_cabinInterior, new Vector3(0.0f, 1.18f, -1.55f), new Vector3(1.65f, 0.12f, 0.35f), dash);
        // Instrument cluster
        MeshBox(_cabinInterior, new Vector3(0.35f, 1.42f, -1.95f), new Vector3(0.55f, 0.18f, 0.12f), trim);
        MeshBox(_cabinInterior, new Vector3(0.2f, 1.42f, -1.98f), new Vector3(0.18f, 0.12f, 0.04f), Mat("gauge", new Color(0.05f, 0.7f, 0.35f), 0.1f, 0.25f, new Color(0.1f, 0.9f, 0.4f)));
        MeshBox(_cabinInterior, new Vector3(0.48f, 1.42f, -1.98f), new Vector3(0.18f, 0.12f, 0.04f), Mat("gauge2", new Color(0.7f, 0.55f, 0.1f), 0.1f, 0.25f, new Color(0.95f, 0.7f, 0.15f)));

        // Steering column + wheel
        MeshBox(_cabinInterior, new Vector3(0.42f, 1.22f, -1.55f), new Vector3(0.1f, 0.1f, 0.55f), chrome);
        _steeringWheel = new MeshInstance3D
        {
            Name = "SteeringWheel",
            Position = new Vector3(0.42f, 1.38f, -1.35f),
            Rotation = new Vector3(Mathf.DegToRad(-18.0f), 0.0f, 0.0f),
            Mesh = new TorusMesh
            {
                InnerRadius = 0.16f,
                OuterRadius = 0.2f,
                Rings = 18,
                RingSegments = 10
            },
            MaterialOverride = dark
        };
        _cabinInterior.AddChild(_steeringWheel);
        // Hub + spokes
        MeshBox(_cabinInterior, new Vector3(0.42f, 1.38f, -1.35f), new Vector3(0.08f, 0.08f, 0.06f), chrome);
        MeshBox(_cabinInterior, new Vector3(0.42f, 1.38f, -1.35f), new Vector3(0.32f, 0.03f, 0.04f), dark);
        MeshBox(_cabinInterior, new Vector3(0.42f, 1.38f, -1.35f), new Vector3(0.03f, 0.32f, 0.04f), dark);

        // Driver seat cushion / back
        MeshBox(_cabinInterior, new Vector3(0.42f, 1.05f, -0.95f), new Vector3(0.55f, 0.18f, 0.55f), seatMat);
        MeshBox(_cabinInterior, new Vector3(0.42f, 1.45f, -0.72f), new Vector3(0.55f, 0.7f, 0.14f), seatMat);
        // Passenger seat
        MeshBox(_cabinInterior, new Vector3(-0.42f, 1.05f, -0.95f), new Vector3(0.55f, 0.18f, 0.55f), seatMat);
        MeshBox(_cabinInterior, new Vector3(-0.42f, 1.45f, -0.72f), new Vector3(0.55f, 0.7f, 0.14f), seatMat);

        // Rear-view mirror
        MeshBox(_cabinInterior, new Vector3(0.0f, 2.0f, -2.05f), new Vector3(0.35f, 0.08f, 0.05f), chrome);

        // Cabin ceiling light
        MeshBox(_cabinInterior, new Vector3(0.0f, 2.05f, -1.1f), new Vector3(0.35f, 0.04f, 0.2f), light);
        _cabinInterior.AddChild(new OmniLight3D
        {
            Name = "CabinLight",
            Position = new Vector3(0.0f, 1.95f, -1.1f),
            LightColor = new Color(1.0f, 0.88f, 0.7f),
            LightEnergy = 0.55f,
            OmniRange = 3.5f,
            ShadowEnabled = false
        });

        _prompt = new Label3D
        {
            Name = "VehiclePrompt",
            Position = new Vector3(0.0f, 2.65f, -1.0f),
            Text = DisplayName,
            FontSize = 22,
            OutlineSize = 7,
            Modulate = new Color(0.72f, 0.92f, 0.78f),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            VisibilityRangeEnd = 18.0f,
            VisibilityRangeEndMargin = 4.0f
        };
        AddChild(_prompt);
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
            material.EmissionEnergyMultiplier = 1.4f;
        }

        return material;
    }

    private static void MeshBox(Node3D parent, Vector3 position, Vector3 size, Godot.Material material)
    {
        parent.AddChild(new MeshInstance3D
        {
            Position = position,
            Mesh = new BoxMesh { Size = size },
            MaterialOverride = material
        });
    }
}
