using Godot;

namespace OperationSteelTide;

[GlobalClass]
public partial class IncendiaryGrenade : RigidBody3D
{
    private const float GroundFuseDuration = 0.4f;
    private const float MaximumAirborneLifetime = 18.0f;
    private const float DamageInterval = 0.4f;
    private const float DamagePerTick = 6.0f;
    private const float MinimumGroundNormalDot = 0.82f;
    private const float FireSurfaceOffset = 0.02f;
    private const float FireDecalProjectionDepth = 0.55f;
    private const int FireDecalTextureSize = 64;

    public const string ActiveGroupName = "active_incendiary_grenades";
    public const float FireRadius = 4.0f;
    public const float FireDuration = 7.2f;

    public Node? OwnerBody { get; set; }
    public bool DamageEnabled { get; set; } = true;
    public bool IsBurning { get; private set; }
    public float RemainingDuration { get; private set; }
    public bool HasTouchedGround { get; private set; }
    public bool FuseStarted => HasTouchedGround && _armed;
    public int ParticleEmitterCount
        => IsInstanceValid(_fireParticles) ? 1 : 0;
    internal bool FirePresentationGroundedForDiagnostics
        => IsBurning
            && IsInstanceValid(_fireGroundDecal)
            && GlobalBasis.IsEqualApprox(Basis.Identity)
            && GlobalPosition.IsEqualApprox(
                _fireSurfacePosition + _fireSurfaceNormal * FireSurfaceOffset)
            && _fireGroundDecal.GlobalBasis.Y.Dot(_fireSurfaceNormal) >= 0.999f
            && IsInstanceValid(_fireParticles)
            && _fireParticles.GlobalBasis.Y.Dot(_fireSurfaceNormal) >= 0.999f;
    internal float FireParticleCoverageRadiusForDiagnostics
        => IsInstanceValid(_fireParticles)
            && _fireParticles.ProcessMaterial is ParticleProcessMaterial particles
                ? particles.EmissionRingRadius
                : 0.0f;

    private MeshInstance3D _casing = null!;
    private GpuParticles3D _fireParticles = null!;
    private Decal _fireGroundDecal = null!;
    private bool _armed;
    private bool _hasFireSurface;
    private float _fuse;
    private float _airborneLifetime;
    private float _damageTimer;
    private Vector3 _fireSurfacePosition;
    private Vector3 _fireSurfaceNormal = Vector3.Up;
    private Vector3 _fireSurfaceLocalPosition;
    private Vector3 _fireSurfaceLocalNormal = Vector3.Up;
    private StaticBody3D? _fireSurfaceBody;
    private FreightTerminalWorld? _registeredWorld;

    public override void _Ready()
    {
        CollisionLayer = 4;
        CollisionMask = 1 | 2;
        Mass = 0.48f;
        GravityScale = 1.0f;
        ContinuousCd = true;
        ContactMonitor = true;
        MaxContactsReported = 6;
        AddToGroup(ActiveGroupName);
        _registeredWorld = GetParent() as FreightTerminalWorld;
        _registeredWorld?.RegisterActiveIncendiaryGrenade(this);
        if (OwnerBody is PhysicsBody3D owner && IsInstanceValid(owner))
        {
            AddCollisionExceptionWith(owner);
        }

        AddChild(new CollisionShape3D
        {
            Shape = new CapsuleShape3D { Radius = 0.075f, Height = 0.19f }
        });
        _casing = new MeshInstance3D { Name = "IncendiaryCasingVisibility" };
        AddChild(_casing);
        _casing.AddChild(GrenadeVisualFactory.CreateIncendiaryGrenade(firstPerson: false));
    }

    public override void _ExitTree()
    {
        if (_registeredWorld is not null && IsInstanceValid(_registeredWorld))
        {
            _registeredWorld.UnregisterActiveIncendiaryGrenade(this);
        }
        _registeredWorld = null;
    }

    public void Arm(Vector3 direction, float speed = 14.0f, float loft = 5.0f)
    {
        LinearVelocity = direction.Normalized() * speed + Vector3.Up * loft;
        AngularVelocity = new Vector3(6.0f, 11.0f, 7.0f);
        _armed = true;
    }

    public override void _PhysicsProcess(double delta)
    {
        var step = (float)delta;
        if (!IsBurning)
        {
            if (!_armed)
            {
                return;
            }
            if (!HasTouchedGround)
            {
                _airborneLifetime += step;
                if (_airborneLifetime >= MaximumAirborneLifetime)
                {
                    QueueFree();
                }
                return;
            }
            _fuse -= step;
            if (_fuse <= 0.0f)
            {
                if (!TryRefreshFireSurfaceAnchor())
                {
                    QueueFree();
                    return;
                }
                Ignite();
            }
            return;
        }

        if (!TryRefreshFireSurfaceAnchor())
        {
            QueueFree();
            return;
        }

        RemainingDuration = Mathf.Max(0.0f, RemainingDuration - step);
        _damageTimer -= step;
        if (DamageEnabled && _damageTimer <= 0.0f)
        {
            _damageTimer += DamageInterval;
            _registeredWorld?.ApplyIncendiaryDamageTick(
                GlobalPosition,
                FireRadius,
                DamagePerTick,
                OwnerBody ?? this,
                this);
        }
        if (RemainingDuration <= 0.0f)
        {
            QueueFree();
        }
    }

    public override void _IntegrateForces(PhysicsDirectBodyState3D state)
    {
        if (!_armed || HasTouchedGround || IsBurning)
        {
            return;
        }
        for (var contact = 0; contact < state.GetContactCount(); contact++)
        {
            var normal = state.GetContactLocalNormal(contact).Normalized();
            if (TryBeginGroundFuse(
                    state.GetContactColliderObject(contact),
                    state.GetContactColliderPosition(contact),
                    normal,
                    state.LinearVelocity.Y))
            {
                return;
            }
        }
    }

    internal bool TryBeginGroundFuseForDiagnostics(
        GodotObject collider,
        Vector3 surfacePosition,
        Vector3 surfaceNormal,
        float verticalVelocity)
        => TryBeginGroundFuse(collider, surfacePosition, surfaceNormal, verticalVelocity);

    internal void BeginGroundFuseForDiagnostics(
        Vector3 surfacePosition,
        Vector3 surfaceNormal)
        => BeginGroundFuse(surfacePosition, surfaceNormal);

    private bool TryBeginGroundFuse(
        GodotObject? collider,
        Vector3 surfacePosition,
        Vector3 surfaceNormal,
        float verticalVelocity)
    {
        if (collider is not StaticBody3D surfaceBody
            || surfaceBody.GetType() != typeof(StaticBody3D)
            || surfaceNormal.LengthSquared() <= 0.001f
            || surfaceNormal.Normalized().Dot(Vector3.Up) < MinimumGroundNormalDot
            || verticalVelocity > 3.0f)
        {
            return false;
        }
        BeginGroundFuse(surfacePosition, surfaceNormal, surfaceBody);
        return HasTouchedGround;
    }

    private void BeginGroundFuse(
        Vector3 surfacePosition,
        Vector3 surfaceNormal,
        StaticBody3D? surfaceBody = null)
    {
        if (!_armed || HasTouchedGround || IsBurning)
        {
            return;
        }
        HasTouchedGround = true;
        _fireSurfacePosition = surfacePosition;
        _fireSurfaceNormal = surfaceNormal.LengthSquared() > 0.001f
            ? surfaceNormal.Normalized()
            : Vector3.Up;
        _fireSurfaceBody = surfaceBody;
        if (surfaceBody is not null)
        {
            _fireSurfaceLocalPosition = surfaceBody.ToLocal(_fireSurfacePosition);
            _fireSurfaceLocalNormal = (surfaceBody.GlobalBasis.Inverse() * _fireSurfaceNormal)
                .Normalized();
        }
        _hasFireSurface = true;
        _fuse = GroundFuseDuration;
        LinearVelocity = Vector3.Zero;
        AngularVelocity = Vector3.Zero;
        Freeze = true;
    }

    private void Ignite()
    {
        if (IsBurning)
        {
            return;
        }
        IsBurning = true;
        RemainingDuration = FireDuration;
        _damageTimer = 0.0f;
        _armed = false;
        Freeze = true;
        CollisionLayer = 0;
        CollisionMask = 0;
        _casing.Visible = false;
        if (!_hasFireSurface)
        {
            QueueFree();
            return;
        }
        ApplyFireSurfaceTransform();
        BuildFirePresentation();
    }

    private bool TryRefreshFireSurfaceAnchor()
    {
        if (!_hasFireSurface)
        {
            return false;
        }
        if (_fireSurfaceBody is null)
        {
            return true;
        }
        if (!IsInstanceValid(_fireSurfaceBody))
        {
            return false;
        }
        _fireSurfacePosition = _fireSurfaceBody.ToGlobal(_fireSurfaceLocalPosition);
        _fireSurfaceNormal = (_fireSurfaceBody.GlobalBasis * _fireSurfaceLocalNormal)
            .Normalized();
        if (IsBurning)
        {
            ApplyFireSurfaceTransform();
            if (IsInstanceValid(_fireParticles))
            {
                _fireParticles.Transform = new Transform3D(
                    SurfaceAlignedBasis(_fireSurfaceNormal),
                    Vector3.Zero);
            }
            if (IsInstanceValid(_fireGroundDecal))
            {
                _fireGroundDecal.Transform = FireGroundDecalTransform();
            }
        }
        return true;
    }

    private void ApplyFireSurfaceTransform()
    {
        GlobalTransform = new Transform3D(
            Basis.Identity,
            _fireSurfacePosition + _fireSurfaceNormal * FireSurfaceOffset);
    }

    private Transform3D FireGroundDecalTransform()
        => new(
            SurfaceAlignedBasis(_fireSurfaceNormal),
            _fireSurfaceNormal * (FireDecalProjectionDepth * 0.24f));

    private void BuildFirePresentation()
    {
        var fireTexture = BuildFireParticleTexture();
        var fireMaterial = new StandardMaterial3D
        {
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
            AlbedoColor = Colors.White,
            AlbedoTexture = fireTexture,
            EmissionEnabled = true,
            Emission = new Color(1.0f, 0.08f, 0.01f),
            EmissionTexture = fireTexture,
            EmissionEnergyMultiplier = 3.2f
        };
        _fireParticles = new GpuParticles3D
        {
            Name = "SharedFireParticles",
            Transform = new Transform3D(
                SurfaceAlignedBasis(_fireSurfaceNormal),
                Vector3.Zero),
            Amount = 96,
            Lifetime = 0.72f,
            Randomness = 0.45f,
            Explosiveness = 0.12f,
            DrawPass1 = new QuadMesh
            {
                Size = new Vector2(0.36f, 0.7f),
                Material = fireMaterial
            },
            ProcessMaterial = new ParticleProcessMaterial
            {
                Direction = Vector3.Up,
                Spread = 38.0f,
                InitialVelocityMin = 0.9f,
                InitialVelocityMax = 2.6f,
                Gravity = new Vector3(0.0f, 0.65f, 0.0f),
                ScaleMin = 0.55f,
                ScaleMax = 1.35f,
                Color = Colors.White,
                EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Ring,
                EmissionRingAxis = Vector3.Up,
                EmissionRingHeight = 0.04f,
                EmissionRingInnerRadius = 0.08f,
                EmissionRingRadius = FireRadius * 0.82f
            },
            LocalCoords = false,
            Emitting = true,
            VisibilityAabb = new Aabb(
                new Vector3(-FireRadius, -0.2f, -FireRadius),
                new Vector3(FireRadius * 2.0f, 3.5f, FireRadius * 2.0f))
        };
        AddChild(_fireParticles);

        var groundTexture = BuildFireGroundTexture();
        _fireGroundDecal = new Decal
        {
            Name = "FireGroundGlow",
            Transform = FireGroundDecalTransform(),
            Size = new Vector3(
                FireRadius * 1.8f,
                FireDecalProjectionDepth,
                FireRadius * 1.8f),
            TextureAlbedo = groundTexture,
            AlbedoMix = 0.24f,
            Modulate = new Color(1.0f, 0.38f, 0.12f, 0.52f),
            UpperFade = 0.18f,
            LowerFade = 0.18f
        };
        AddChild(_fireGroundDecal);

        AddChild(new OmniLight3D
        {
            Name = "FireLight",
            Position = Vector3.Up * 0.42f,
            LightColor = new Color(1.0f, 0.22f, 0.035f),
            LightEnergy = 0.85f,
            OmniRange = FireRadius * 1.3f,
            ShadowEnabled = false
        });
    }

    private static ImageTexture BuildFireParticleTexture()
    {
        const int width = 48;
        const int height = 96;
        var image = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
        for (var y = 0; y < height; y++)
        {
            var flameHeight = 1.0f - y / (height - 1.0f);
            var verticalFade = Mathf.Clamp(flameHeight / 0.08f, 0.0f, 1.0f)
                * Mathf.Clamp((1.0f - flameHeight) / 0.08f, 0.0f, 1.0f);
            var halfWidth = Mathf.Lerp(0.48f, 0.045f, Mathf.Pow(flameHeight, 0.68f));
            var wave = Mathf.Sin(flameHeight * 12.4f) * 0.055f * flameHeight;
            for (var x = 0; x < width; x++)
            {
                var horizontal = (x / (width - 1.0f) * 2.0f - 1.0f) - wave;
                var horizontalFade = Mathf.Clamp(
                    (halfWidth - Mathf.Abs(horizontal)) / 0.11f,
                    0.0f,
                    1.0f);
                var flicker = 0.76f
                    + 0.16f * Mathf.Sin(x * 0.61f + y * 0.29f)
                    + 0.08f * Mathf.Sin(x * 0.17f - y * 0.73f);
                var alpha = verticalFade
                    * horizontalFade
                    * Mathf.Clamp(flicker, 0.42f, 1.0f);
                var color = new Color(1.0f, 0.78f, 0.12f, alpha)
                    .Lerp(new Color(1.0f, 0.08f, 0.005f, alpha), flameHeight);
                image.SetPixel(x, y, color);
            }
        }
        return ImageTexture.CreateFromImage(image);
    }

    private static ImageTexture BuildFireGroundTexture()
    {
        var image = Image.CreateEmpty(
            FireDecalTextureSize,
            FireDecalTextureSize,
            false,
            Image.Format.Rgba8);
        for (var y = 0; y < FireDecalTextureSize; y++)
        {
            for (var x = 0; x < FireDecalTextureSize; x++)
            {
                var offset = new Vector2(
                    (x + 0.5f) / FireDecalTextureSize * 2.0f - 1.0f,
                    (y + 0.5f) / FireDecalTextureSize * 2.0f - 1.0f);
                var radius = offset.Length();
                var edge = Mathf.Clamp((1.0f - radius) / 0.16f, 0.0f, 1.0f);
                var breakup = ValueNoise(x, y, 9.0f, 17) * 0.62f
                    + ValueNoise(x + 13, y - 19, 4.5f, 41) * 0.38f;
                var patches = Mathf.Clamp((breakup - 0.34f) / 0.36f, 0.0f, 1.0f);
                var alpha = edge * patches * 0.36f;
                image.SetPixel(x, y, new Color(1.0f, 0.11f, 0.01f, alpha));
            }
        }
        return ImageTexture.CreateFromImage(image);
    }

    private static float ValueNoise(float x, float y, float scale, int seed)
    {
        var sampleX = x / scale;
        var sampleY = y / scale;
        var x0 = (int)Mathf.Floor(sampleX);
        var y0 = (int)Mathf.Floor(sampleY);
        var blendX = sampleX - x0;
        var blendY = sampleY - y0;
        blendX *= blendX * (3.0f - 2.0f * blendX);
        blendY *= blendY * (3.0f - 2.0f * blendY);
        var top = Mathf.Lerp(
            HashNoise(x0, y0, seed),
            HashNoise(x0 + 1, y0, seed),
            blendX);
        var bottom = Mathf.Lerp(
            HashNoise(x0, y0 + 1, seed),
            HashNoise(x0 + 1, y0 + 1, seed),
            blendX);
        return Mathf.Lerp(top, bottom, blendY);
    }

    private static float HashNoise(int x, int y, int seed)
    {
        unchecked
        {
            var hash = (uint)(x * 374761393 + y * 668265263 + seed * 362437);
            hash = (hash ^ (hash >> 13)) * 1274126177u;
            return (hash & 0xffffu) / 65535.0f;
        }
    }

    private static Basis SurfaceAlignedBasis(Vector3 surfaceNormal)
    {
        var up = surfaceNormal.LengthSquared() > 0.001f
            ? surfaceNormal.Normalized()
            : Vector3.Up;
        var z = Vector3.Back - up * up.Dot(Vector3.Back);
        if (z.LengthSquared() <= 0.001f)
        {
            z = Vector3.Right - up * up.Dot(Vector3.Right);
        }
        z = z.Normalized();
        var x = up.Cross(z).Normalized();
        return new Basis(x, up, z);
    }
}
