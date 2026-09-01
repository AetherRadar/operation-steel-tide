using Godot;

namespace OperationSteelTide;

public readonly record struct FlashbangExposure(
    Vector3 SourcePosition,
    float Intensity,
    float DurationSeconds,
    float Distance,
    float FacingDot);

/// <summary>
/// Combatants opt into flashbang effects through this small target contract. The
/// grenade owns distance, line-of-sight, and facing calculations; targets own the
/// presentation and behavior changes caused by the resulting exposure.
/// </summary>
public interface IFlashbangTarget
{
    Vector3 FlashbangViewOrigin { get; }
    Vector3 FlashbangViewForward { get; }
    bool CanReceiveFlashbang { get; }
    void ApplyFlashbang(FlashbangExposure exposure);
}

public static class FlashbangExposureResolver
{
    public const float MaximumRadius = 20.0f;
    public const float FullEffectRadius = 4.0f;
    public const float MaximumDuration = 5.5f;
    public const float MinimumDuration = 0.35f;
    public const float RearFacingIntensityFloor = 0.18f;
    public const float CloseExposureIntensityFloor = 0.72f;

    public static FlashbangExposure Resolve(
        Vector3 sourcePosition,
        Vector3 viewOrigin,
        Vector3 viewForward,
        bool hasLineOfSight)
    {
        var toSource = sourcePosition - viewOrigin;
        var distance = toSource.Length();
        if (!hasLineOfSight || distance > MaximumRadius)
        {
            return new FlashbangExposure(sourcePosition, 0.0f, 0.0f, distance, -1.0f);
        }

        var direction = distance > 0.001f ? toSource / distance : viewForward.Normalized();
        var forward = viewForward.LengthSquared() > 0.001f
            ? viewForward.Normalized()
            : Vector3.Forward;
        var facingDot = Mathf.Clamp(forward.Dot(direction), -1.0f, 1.0f);
        var distanceBlend = Mathf.Clamp(
            (distance - FullEffectRadius) / (MaximumRadius - FullEffectRadius),
            0.0f,
            1.0f);
        var distanceFactor = 1.0f - Mathf.SmoothStep(0.0f, 1.0f, distanceBlend);
        var facingBlend = Mathf.Clamp((facingDot + 0.72f) / 1.07f, 0.0f, 1.0f);
        var facingFactor = Mathf.Lerp(
            RearFacingIntensityFloor,
            1.0f,
            Mathf.SmoothStep(0.0f, 1.0f, facingBlend));
        var proximityFloor = distance <= FullEffectRadius
            ? CloseExposureIntensityFloor
            : 0.0f;
        var intensity = Mathf.Clamp(
            Mathf.Max(proximityFloor, distanceFactor * facingFactor),
            0.0f,
            1.0f);
        var duration = intensity <= 0.01f
            ? 0.0f
            : Mathf.Lerp(MinimumDuration, MaximumDuration, intensity);
        return new FlashbangExposure(
            sourcePosition,
            intensity,
            duration,
            distance,
            facingDot);
    }
}

[GlobalClass]
public partial class FlashbangGrenade : RigidBody3D
{
    private const float FuseDuration = 1.35f;
    private const float EffectLingerDuration = 0.42f;

    public const string ActiveGroupName = "active_flashbang_grenades";
    public const string TargetGroupName = "flashbang_targets";

    public Node? OwnerBody { get; set; }
    public bool HasDetonated { get; private set; }
    public int NetworkSpawnId { get; private set; }
    public int NetworkRound { get; private set; }
    internal bool WaitsForAuthoritativeDetonation { get; private set; }
    internal int EligibleTargetCountForDiagnostics { get; private set; }
    internal int AppliedTargetCountForDiagnostics { get; private set; }

    private MeshInstance3D _casing = null!;
    private bool _armed;
    private float _fuse = FuseDuration;
    private float _effectLinger;
    private FreightTerminalWorld? _registeredWorld;

    public override void _Ready()
    {
        CollisionLayer = 4;
        CollisionMask = 1 | 2;
        Mass = 0.46f;
        GravityScale = 1.0f;
        ContinuousCd = true;
        AddToGroup(ActiveGroupName);
        _registeredWorld = GetParent() as FreightTerminalWorld;
        _registeredWorld?.RegisterActiveFlashbangGrenade(this);
        if (OwnerBody is PhysicsBody3D owner && IsInstanceValid(owner))
        {
            AddCollisionExceptionWith(owner);
        }

        AddChild(new CollisionShape3D
        {
            Shape = new CapsuleShape3D { Radius = 0.075f, Height = 0.19f }
        });
        _casing = new MeshInstance3D { Name = "FlashbangCasingVisibility" };
        AddChild(_casing);
        // The project currently has no redistributable authored flashbang model.
        // Reuse the completed throwable casing instead of shipping a new primitive.
        _casing.AddChild(GrenadeVisualFactory.CreateSmokeGrenade(firstPerson: false));
    }

    public override void _ExitTree()
    {
        if (_registeredWorld is not null && IsInstanceValid(_registeredWorld))
        {
            _registeredWorld.UnregisterActiveFlashbangGrenade(this);
        }
        _registeredWorld = null;
    }

    public void Arm(Vector3 direction, float speed = 14.0f, float loft = 5.0f)
    {
        LinearVelocity = direction.Normalized() * speed + Vector3.Up * loft;
        AngularVelocity = new Vector3(7.0f, 10.0f, 6.0f);
        _fuse = FuseDuration;
        _armed = true;
    }

    public override void _PhysicsProcess(double delta)
    {
        var step = (float)delta;
        if (!HasDetonated)
        {
            if (!_armed)
            {
                return;
            }
            _fuse -= step;
            if (_fuse <= 0.0f)
            {
                if (WaitsForAuthoritativeDetonation)
                {
                    _fuse = 0.0f;
                    return;
                }
                Detonate(authoritativeSignal: false);
            }
            return;
        }

        _effectLinger -= step;
        if (_effectLinger <= 0.0f)
        {
            QueueFree();
        }
    }

    internal void DetonateForDiagnostics() => Detonate();

    internal void ConfigureNetworkReplication(
        int spawnId,
        int round,
        bool waitForAuthoritativeDetonation)
    {
        if (spawnId < 1 || round < 1)
        {
            NetworkSpawnId = 0;
            NetworkRound = 0;
            WaitsForAuthoritativeDetonation = false;
            return;
        }
        NetworkSpawnId = spawnId;
        NetworkRound = round;
        WaitsForAuthoritativeDetonation = waitForAuthoritativeDetonation;
    }

    internal void ApplyAuthoritativeDetonation(Vector3 position)
    {
        if (HasDetonated
            || !float.IsFinite(position.X)
            || !float.IsFinite(position.Y)
            || !float.IsFinite(position.Z))
        {
            return;
        }
        GlobalPosition = position;
        LinearVelocity = Vector3.Zero;
        AngularVelocity = Vector3.Zero;
        WaitsForAuthoritativeDetonation = false;
        Detonate(authoritativeSignal: true);
    }

    private void Detonate(bool authoritativeSignal = false)
    {
        if (HasDetonated || WaitsForAuthoritativeDetonation && !authoritativeSignal)
        {
            return;
        }
        HasDetonated = true;
        _armed = false;
        _effectLinger = EffectLingerDuration;
        Freeze = true;
        CollisionLayer = 0;
        CollisionMask = 0;
        _casing.Visible = false;

        _registeredWorld?.NotifyAuthoritativeFlashbangDetonated(this, GlobalPosition);
        ApplyExposureToTargets();
        var audio = new AudioStreamPlayer3D
        {
            Name = "FlashbangReport",
            Stream = SoundLab.Explosion(),
            VolumeDb = -3.0f,
            MaxDistance = 90.0f
        };
        AddChild(audio);
        audio.Play();

        var flash = new OmniLight3D
        {
            Name = "FlashBurst",
            LightColor = new Color(0.95f, 0.98f, 1.0f),
            LightEnergy = 18.0f,
            OmniRange = 20.0f,
            ShadowEnabled = false
        };
        AddChild(flash);
        CreateTween().TweenProperty(flash, "light_energy", 0.0f, 0.22f);
    }

    private void ApplyExposureToTargets()
    {
        EligibleTargetCountForDiagnostics = 0;
        AppliedTargetCountForDiagnostics = 0;
        var targets = GetTree().GetNodesInGroup(TargetGroupName);
        using var targetsBacking = targets.AsDisposable();
        foreach (var node in targets)
        {
            if (node is not IFlashbangTarget target || !target.CanReceiveFlashbang)
            {
                continue;
            }
            EligibleTargetCountForDiagnostics++;
            var viewOrigin = target.FlashbangViewOrigin;
            var exposure = FlashbangExposureResolver.Resolve(
                GlobalPosition,
                viewOrigin,
                target.FlashbangViewForward,
                HasLineOfSight(viewOrigin, node));
            if (exposure.Intensity > 0.01f)
            {
                target.ApplyFlashbang(exposure);
                AppliedTargetCountForDiagnostics++;
            }
        }
    }

    private bool HasLineOfSight(Vector3 viewOrigin, Node targetNode)
    {
        var exclude = new Godot.Collections.Array<Rid> { GetRid() };
        if (targetNode is CollisionObject3D collisionObject)
        {
            exclude.Add(collisionObject.GetRid());
        }
        var query = PhysicsRayQueryParameters3D.Create(
            viewOrigin,
            GlobalPosition,
            1);
        query.Exclude = exclude;
        return GetWorld3D().DirectSpaceState.IntersectRay(query).Count == 0;
    }
}
