using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

[GlobalClass]
public partial class SquadMate : CharacterBody3D, ISquadCombatant
{
    public FreightTerminalWorld Main { get; private set; } = null!;
    public TacticalPlayer Leader { get; private set; } = null!;
    public OperatorRole Role { get; private set; }
    public SquadOrder Order { get; private set; } = SquadOrder.Follow;
    public int SquadSlot { get; private set; }
    public string Callsign { get; private set; } = "ECHO";
    public bool IsHumanProxy { get; private set; }
    public long NetworkPeerId { get; private set; }
    public bool IsDowned { get; private set; }
    public bool ReviveUsed { get; private set; }
    /// <summary>Second death after revive budget is spent becomes a lootable body bag.</summary>
    public bool IsBodyBag { get; private set; }
    public bool IsExtractionPassenger { get; private set; }
    public float Health { get; private set; }
    public float MaxHealth { get; private set; }
    public float SkillCooldownRemaining => _skillCooldown;
    public float SkillCooldownDuration => OperatorRoles.Spec(Role).SkillCooldown * (IsHumanProxy ? 1.0f : 2.0f);

    public Node3D CombatNode => this;
    public bool CombatDead => IsDowned || IsBodyBag;
    public bool CombatDowned => IsDowned && !IsBodyBag;
    public bool CanBeRevived => IsDowned && !ReviveUsed && !IsBodyBag;
    public float CombatHealth => Health;
    public float CombatMaxHealth => MaxHealth;
    /// <summary>AI operators deploy with a primary; diagnostics may temporarily strip it.</summary>
    public bool HasFireablePrimary { get; private set; } = true;
    /// <summary>Squad hold-fire stance blocks mate trigger pulls without dropping awareness.</summary>
    public bool HoldFireActive { get; private set; }
    /// <summary>Weapon the mate actually shoots with; loot pickups swap it for real stat changes.</summary>
    public WeaponBuild CarriedWeapon { get; private set; } = WeaponCatalog.Build(WeaponPlatform.M4A1, 0);
    /// <summary>Midpoint of the AI damage band (stats.Damage × 0.32–0.48) for deterministic checks.</summary>
    public float MeanShotDamageForDiagnostics => CarriedWeapon.Stats().Damage * 0.4f;
    /// <summary>Ammo grade adopted from the looted weapon kit; drives damage and armor penetration.</summary>
    public LootGrade AmmoGrade => _ammoGrade;
    /// <summary>Smoke canisters left for leader-called screens.</summary>
    public int SmokeChargesRemaining => _smokeCharges;

    private const int SquadSmokeCanisters = 2;
    private const float SquadSmokeThrowSpeed = 14.0f;

    public void ApplyColdStartUnarmed()
    {
        HasFireablePrimary = false;
        if (IsInstanceValid(_weapon))
        {
            _weapon.Visible = false;
        }
        // Also hide any late-added accent parts parented under the rifle root.
        if (IsInstanceValid(_muzzle))
        {
            _muzzle.Visible = false;
        }
        SetAuthoredWeaponVisible(false);
    }

    public void GrantFireablePrimaryForDiagnostics()
    {
        HasFireablePrimary = true;
        _ammoGrade = LootGrade.Uncommon;
        RefillMagazine();
        if (IsInstanceValid(_weapon))
        {
            _weapon.Visible = true;
        }
        SetAuthoredWeaponVisible(true);
    }

    /// <summary>Production path: equip a weapon taken from a world loot source.</summary>
    public bool EquipWeaponFromLoot(WeaponBuild build, LootGrade ammoGrade = LootGrade.Uncommon)
    {
        if (build is null)
        {
            return false;
        }
        CarriedWeapon = build.Clone();
        _audioWeapon = build.Clone();
        RefreshShotAudio();
        _ammoGrade = ammoGrade;
        RefillMagazine();
        HasFireablePrimary = true;
        if (IsInstanceValid(_weapon))
        {
            _weapon.Visible = true;
        }
        if (IsInstanceValid(_muzzle))
        {
            _muzzle.Visible = true;
        }
        SetAuthoredWeaponVisible(true);
        return HasFireablePrimary;
    }

    public void SetHoldFire(bool holdFire)
    {
        HoldFireActive = holdFire;
        if (holdFire)
        {
            _burstShotsRemaining = 0;
        }
    }

    /// <summary>Leader-called smoke screen: lob a canister that lands near the marked point.</summary>
    public bool DeploySmokeScreen(Vector3 targetPoint)
    {
        if (IsDowned || IsBodyBag || IsNetworkProxy || Main is null || !IsInstanceValid(Main) || _smokeCharges <= 0)
        {
            return false;
        }
        var flatTarget = FlattenToCurrentHeight(targetPoint);
        var toTarget = flatTarget - GlobalPosition;
        toTarget.Y = 0.0f;
        if (toTarget.LengthSquared() < 0.25f)
        {
            toTarget = -GlobalBasis.Z;
            toTarget.Y = 0.0f;
        }
        var distance = toTarget.Length();
        // Fixed 14 m/s throw: the loft sets flight time, so range ≈ 2.86 × loft.
        var loft = Mathf.Clamp(distance / 2.86f, 2.2f, 7.2f);
        var origin = IsInstanceValid(_muzzle) ? _muzzle.GlobalPosition : GlobalPosition + Vector3.Up * 1.35f;
        FaceTacticalPoint(flatTarget, 1.0f);
        Main.ThrowSmokeGrenade(origin, toTarget.Normalized(), this, SquadSmokeThrowSpeed, loft);
        _smokeCharges--;
        _burstShotsRemaining = 0;
        _weaponCooldown = Mathf.Max(_weaponCooldown, 0.7f);
        return true;
    }

    private void RefillMagazine()
    {
        _magazineRemaining = CarriedWeapon.Stats().MagazineSize;
        _reloadTimer = 0.0f;
    }

    internal void SetCarriedWeaponForDiagnostics(WeaponBuild build)
    {
        CarriedWeapon = build.Clone();
        _ammoGrade = LootGrade.Uncommon;
        RefillMagazine();
    }

    private readonly RandomNumberGenerator _rng = new();
    private Node3D _rig = null!;
    private CollisionShape3D _collider = null!;
    private Node3D _weapon = null!;
    private Node3D _roleDevice = null!;
    private Marker3D _muzzle = null!;
    private MeshInstance3D _healthFill = null!;
    private Label3D _nameLabel = null!;
    private Vector3 _orderPosition;
    private Vector3 _remotePosition;
    private Vector3 _remoteRotation;
    private float _weaponCooldown;
    private float _reloadTimer;
    private int _magazineRemaining;
    private int _smokeCharges = SquadSmokeCanisters;
    private LootGrade _ammoGrade = LootGrade.Uncommon;
    private float _skillCooldown;
    private float _skillActionTime;
    private float _overdriveTime;
    private float _decisionTimer;
    private float _animationPhase;
    private bool _skillEffectApplied;
    private float _remoteHealth;
    private bool _remoteDown;
    private bool _networkAbilityPending;
    private Vector3 _networkAbilityOrigin;
    private Vector3 _networkAbilityForward;
    private ILootSource? _lootHuntSource;
    private float _lootHuntCooldown;
    private float _doorWaitTimer;
    private ISquadCombatant? _reviveTarget;
    private float _revivePoseBlend;
    private Godot.Collections.Array<Rid>? _navigationProbeExclusions;

    internal bool LeaderCollisionExcludedForDiagnostics { get; private set; }
    internal HitRegion LastDamageRegionForDiagnostics { get; private set; } = HitRegion.Torso;

    public void Configure(
        FreightTerminalWorld main,
        TacticalPlayer leader,
        int slot,
        OperatorRole role,
        string callsign,
        bool humanProxy = false,
        long peerId = 0,
        bool networkProxy = false)
    {
        Main = main;
        Leader = leader;
        SquadSlot = slot;
        Role = role;
        Callsign = callsign;
        IsHumanProxy = humanProxy;
        IsNetworkProxy = humanProxy || networkProxy;
        NetworkPeerId = peerId;
        // Role-flavoured default primaries; everything downstream reads the real stats.
        CarriedWeapon = WeaponCatalog.Build(role switch
        {
            OperatorRole.Medic => WeaponPlatform.ScarL,
            OperatorRole.Recon => WeaponPlatform.VSS,
            OperatorRole.Scavenger => WeaponPlatform.AK74,
            OperatorRole.Locksmith => WeaponPlatform.MP5A5,
            _ => WeaponPlatform.M4A1
        }, 0);
        var spec = OperatorRoles.Spec(role);
        MaxHealth = spec.MaxHealth;
        Health = MaxHealth;
        _remoteHealth = MaxHealth;
        ReviveUsed = false;
        IsDowned = false;
        Order = SquadOrder.Follow;
        _orderPosition = Position;
        if (IsInsideTree())
        {
            ApplyRoleVisuals();
        }
    }

    public override void _Ready()
    {
        _rng.Randomize();
        CollisionLayer = 4;
        CollisionMask = 1;
        FloorSnapLength = 0.35f;
        if (IsInstanceValid(Leader))
        {
            AddCollisionExceptionWith(Leader);
            LeaderCollisionExcludedForDiagnostics = true;
        }
        AddToGroup("player_squad_ai");
        BuildOperator();
        ApplyRoleVisuals();
        InitializeCombatTactics();
        if (!IsNetworkProxy)
        {
            _skillCooldown = SkillCooldownDuration * Mathf.Clamp(0.24f + SquadSlot * 0.11f, 0.35f, 0.62f);
        }
        _remotePosition = GlobalPosition;
        _remoteRotation = Rotation;
    }

    public override void _ExitTree()
    {
        _navigationProbeExclusions?.AsDisposable().Dispose();
        _navigationProbeExclusions = null;
    }

    public void SetOrder(SquadOrder order, Vector3 position)
    {
        if (IsNetworkProxy)
        {
            return;
        }
        Order = order;
        _orderPosition = order == SquadOrder.Follow ? GlobalPosition : position;
        if (IsInstanceValid(Main)
            && (order != SquadOrder.Follow || Main.IsDemolitionMode))
        {
            Main.ClearSquadNavigation(this);
        }
        OnSquadOrderChanged();
        UpdateLabel();
    }

    public void BoardExtractionSeat(Node3D seat)
    {
        if (!GodotObject.IsInstanceValid(seat) || IsExtractionPassenger || IsDowned || IsBodyBag)
        {
            return;
        }

        _reviveTarget = null;
        CancelNavigationTraversal();
        Velocity = Vector3.Zero;
        CollisionLayer = 0;
        CollisionMask = 0;
        var children = GetChildren();
        using var childrenBacking = children.AsDisposable();
        foreach (var child in children)
        {
            if (child is CollisionShape3D collision)
            {
                collision.Disabled = true;
            }
        }
        if (IsInstanceValid(_nameLabel))
        {
            _nameLabel.Visible = false;
        }
        if (IsInstanceValid(_healthFill))
        {
            _healthFill.Visible = false;
        }
        if (IsInstanceValid(_roleDevice))
        {
            _roleDevice.Visible = false;
        }
        if (IsInstanceValid(_rig))
        {
            _rig.Visible = true;
            _rig.Position = new Vector3(0.0f, -0.12f, 0.0f);
            _rig.Rotation = Vector3.Zero;
            _rig.Scale = Vector3.One * 0.82f;
        }

        Reparent(seat, keepGlobalTransform: false);
        Position = Vector3.Zero;
        Rotation = Vector3.Zero;
        Visible = true;
        IsExtractionPassenger = true;
        SetPhysicsProcess(false);
    }

    public bool IsRevivingLeader => ReferenceEquals(_reviveTarget, Leader) && HasActiveReviveTarget;
    internal bool IsRevivingFriendly => HasActiveReviveTarget;
    internal bool IsRevivingTarget(ISquadCombatant target)
        => ReferenceEquals(_reviveTarget, target) && HasActiveReviveTarget;

    private bool HasActiveReviveTarget
    {
        get
        {
            if (_reviveTarget is null || !_reviveTarget.CanBeRevived)
            {
                return false;
            }
            var node = _reviveTarget.CombatNode;
            return IsInstanceValid(node);
        }
    }

    private Node3D? ActiveReviveTargetNode => HasActiveReviveTarget
        ? _reviveTarget!.CombatNode
        : null;

    /// <summary>Commit this AI mate to reaching and reviving a downed friendly.</summary>
    public void BeginSquadRevive(ISquadCombatant target)
    {
        if (IsDowned || IsBodyBag || IsHumanProxy || !target.CanBeRevived
            || ReferenceEquals(target, this) || !IsInstanceValid(target.CombatNode))
        {
            return;
        }
        ResetEmergencyGlassEgressPlan();
        _reviveTarget = target;
    }

    public void EndSquadRevive()
    {
        _reviveTarget = null;
        ResetEmergencyGlassEgressPlan();
        CancelNavigationTraversal();
        _revivePoseBlend = 0.0f;
        if (IsInstanceValid(_rig))
        {
            _rig.Rotation = Vector3.Zero;
            var position = _rig.Position;
            position.Y = 0.0f;
            _rig.Position = position;
        }
    }

    public void SetRemoteState(OperatorRole role, Vector3 position, Vector3 rotation, float health, bool down)
    {
        if (!IsNetworkProxy)
        {
            return;
        }
        if (Role != role)
        {
            Role = role;
            var spec = OperatorRoles.Spec(role);
            MaxHealth = spec.MaxHealth;
            ApplyRoleVisuals();
        }
        _remotePosition = position;
        _remoteRotation = rotation;
        _remoteHealth = Mathf.Clamp(health, 0.0f, MaxHealth);
        _remoteDown = down;
    }

    public void PlayRemoteShot(Vector3 end)
    {
        if (!IsHumanProxy || IsDowned || !IsInstanceValid(_muzzle))
        {
            return;
        }
        HoldAuthoredAimAfterShot();
        PlayShotAudio();
        Main.SpawnTracer(_muzzle.GlobalPosition, end, new Color(0.32f, 0.78f, 1.0f));
    }

    public override void _PhysicsProcess(double delta)
    {
        RecordCombatMovementTrail();
        var dt = (float)delta;
        _weaponCooldown = Mathf.Max(0.0f, _weaponCooldown - dt);
        _reloadTimer = Mathf.Max(0.0f, _reloadTimer - dt);
        if (_reloadTimer <= 0.0f && _magazineRemaining <= 0)
        {
            RefillMagazine();
        }
        _skillCooldown = Mathf.Max(0.0f, _skillCooldown - dt);
        _overdriveTime = Mathf.Max(0.0f, _overdriveTime - dt);
        _decisionTimer = Mathf.Max(0.0f, _decisionTimer - dt);
        UpdateCombatTacticalTimers(dt);

        if (IsNetworkProxy)
        {
            UpdateRemoteProxy(dt);
            UpdateSkillAction(dt);
            AnimateRig(dt);
            return;
        }
        if (!GodotObject.IsInstanceValid(Leader))
        {
            Velocity = Vector3.Zero;
            AnimateRig(dt);
            return;
        }
        if (IsBodyBag)
        {
            Velocity = Vector3.Zero;
            return;
        }
        if (IsDowned)
        {
            // First down: stay prone in place (no sliding human ragdoll crawl).
            Velocity = Vector3.Zero;
            if (!IsOnFloor())
            {
                Velocity = new Vector3(0.0f, -12.0f, 0.0f);
                MoveAndSlide();
            }
            if (!UsesAuthoredOperatorForDiagnostics)
            {
                _rig.Rotation = new Vector3(Mathf.Pi * 0.5f, 0.0f, 0.0f);
            }
            AnimateRig(dt);
            UpdateLabel();
            return;
        }

        if (UpdateActiveNavigationTraversal(dt))
        {
            AnimateRig(dt);
            return;
        }

        UpdateSkillAction(dt);
        _lootHuntCooldown = Mathf.Max(0.0f, _lootHuntCooldown - dt);
        var hostile = UpdateCombatTarget(dt);
        var patient = Role == OperatorRole.Medic ? Main.FindLowestFriendly(0.72f, true) : null;
        // Cold-start: hunt a weapon cache before combat when still unarmed.
        if (!HasFireablePrimary && Order != SquadOrder.Hold)
        {
            UpdateWeaponLootHunt(dt, hostile);
        }
        var destination = ResolveFormationDestination();
        var objectivePriority = false;
        if (!HasFireablePrimary && _lootHuntSource is not null && IsInstanceValid(_lootHuntSource.LootNode))
        {
            destination = _lootHuntSource.LootNode.GlobalPosition;
            objectivePriority = true;
        }
        else if (patient is not null && patient != this && Order != SquadOrder.Hold
            && GlobalPosition.DistanceTo(patient.CombatNode.GlobalPosition) > 5.5f)
        {
            destination = patient.CombatNode.GlobalPosition;
            objectivePriority = hostile is null
                || GlobalPosition.DistanceTo(hostile.GlobalPosition) > 14.0f;
        }
        if (Main.IsDemolitionMode
            && Main.TryGetDemolitionObjectiveDestination(this, out var demolitionObjectiveDestination))
        {
            destination = demolitionObjectiveDestination;
            objectivePriority = true;
        }
        else if (Main.IsDemolitionMode
            && Main.TryGetDemolitionEscortTarget(
                     this,
                     out var escortLeader,
                     out var escortObjectivePriority))
        {
            var forward = ResolveDemolitionEscortForward(escortLeader);
            var preferredEscortDestination = FreightTerminalWorld
                .ResolveDemolitionEscortPreferredDestination(
                    SquadSlot,
                    escortLeader.GlobalPosition,
                    forward);
            if (!Main.TryResolveDemolitionEscortDestination(
                    this,
                    escortLeader,
                    preferredEscortDestination,
                    out destination))
            {
                // A missing physical landing is transient (airborne, boxed, or a moving
                // blocker). Hold this frame and force a grounded escape before the short
                // retry rather than walking toward self indefinitely or using a raw target.
                destination = GlobalPosition;
                Main.RequestDemolitionEscortNavigationRecovery(this);
            }
            // Keep the carrier as the engagement anchor under contact, but let the
            // combat layer maneuver around that anchor while a threat is actionable.
            objectivePriority = escortObjectivePriority;
        }
        var reviveTargetNode = ActiveReviveTargetNode;
        if (reviveTargetNode is not null)
        {
            destination = reviveTargetNode.GlobalPosition;
            objectivePriority = true;
        }
        else
        {
            if (Order == SquadOrder.Follow && !objectivePriority)
            {
                destination = Main.ResolveSquadFollowDestination(this, destination);
            }
            destination = ResolveTacticalDestination(destination, hostile, objectivePriority);
        }
        var holdFormation = ShouldHoldFollowFormation(destination, hostile, objectivePriority);
        var navigationDirective = holdFormation
            ? SquadNavigationDirective.Walk(GlobalPosition)
            : Main.ResolveSquadNavigationDestination(
                this,
                destination,
                emergency: reviveTargetNode is not null);
        destination = navigationDirective.Target;
        if (Main.TryPrepareAiDoorTraversal(GlobalPosition, destination, out var doorWaiting)
            && doorWaiting)
        {
            _doorWaitTimer += dt;
            if (_doorWaitTimer < 0.9f)
            {
                var doorVelocity = Velocity;
                doorVelocity.X = Mathf.MoveToward(doorVelocity.X, 0.0f, dt * 18.0f);
                doorVelocity.Z = Mathf.MoveToward(doorVelocity.Z, 0.0f, dt * 18.0f);
                Velocity = doorVelocity;
                ResetMovementProgress();
                MoveAndSlide();
                AnimateRig(dt);
                return;
            }
        }
        else
        {
            _doorWaitTimer = 0.0f;
        }
        // Required authored routes must not be displaced by generic obstacle avoidance;
        // doing so can leave the mate circling outside a valid doorway or stair path.
        var followPreciseNavigation = navigationDirective.PreciseTrail || navigationDirective.Required;
        UpdateTacticalMovement(
            destination,
            hostile,
            objectivePriority,
            navigationDirective.Kind,
            navigationDirective.SteppedDirect,
            followPreciseNavigation,
            dt);
        if (TryBeginNavigationTraversal(navigationDirective)
            || navigationDirective.Kind == SquadTraversalKind.Walk
                && (TryBeginVaultTowardDestination(destination)
                    || TryBeginDropTowardDestination(destination)))
        {
            UpdateActiveNavigationTraversal(dt);
            AnimateRig(dt);
            return;
        }
        MaintainStairNavigation(destination, dt);
        ConsiderMedicSupport(patient);
        ConsiderLootSupport(hostile);
        if (hostile is not null && !hostile.IsDead)
        {
            TryFire(hostile);
            ConsiderRoleAbility(hostile, _combatHasSight);
        }
        MoveAndSlide();
        TryNavigationStepUp(
            navigationDirective.Kind == SquadTraversalKind.Step
                || navigationDirective.SteppedDirect
                || followPreciseNavigation
                ? _combatPathDirection
                : _combatDesiredDirection,
            destination);
        TrackTacticalMovement(dt);
        AnimateRig(dt);
    }

    private void UpdateWeaponLootHunt(float delta, EnemyOperator? hostile)
    {
        _ = delta;
        // Break off loot hunt if a hostile is already on top of us.
        if (hostile is not null && GlobalPosition.DistanceTo(hostile.GlobalPosition) < 14.0f)
        {
            return;
        }
        if (_lootHuntSource is null || !_lootHuntSource.IsSearchable || !IsInstanceValid(_lootHuntSource.LootNode))
        {
            if (_lootHuntCooldown > 0.0f)
            {
                return;
            }
            _lootHuntSource = Main.FindNearestWeaponLootSource(GlobalPosition, 70.0f);
            _lootHuntCooldown = 1.5f;
        }
        if (_lootHuntSource is null || !IsInstanceValid(_lootHuntSource.LootNode))
        {
            return;
        }
        if (GlobalPosition.DistanceTo(_lootHuntSource.LootNode.GlobalPosition) < 1.8f)
        {
            if (Main.TryMateEquipWeaponFromLootSource(this, _lootHuntSource))
            {
                _lootHuntSource = null;
            }
            else
            {
                _lootHuntSource = null;
                _lootHuntCooldown = 2.0f;
            }
        }
    }

    private void UpdateRemoteProxy(float delta)
    {
        Health = Mathf.Lerp(Health, _remoteHealth, delta * 9.0f);
        IsDowned = _remoteDown;
        var distance = GlobalPosition.DistanceTo(_remotePosition);
        GlobalPosition = distance > 12.0f
            ? _remotePosition
            : GlobalPosition.Lerp(_remotePosition, delta * 10.0f);
        Rotation = Rotation.Lerp(_remoteRotation, delta * 9.0f);
        Velocity = Vector3.Zero;
        UpdateHealthVisual();
    }

    private void TryFire(EnemyOperator enemy)
    {
        if (!HasFireablePrimary
            || HoldFireActive
            || _weaponCooldown > 0.0f
            || _reloadTimer > 0.0f
            || _skillActionTime > 0.0f)
        {
            return;
        }
        var reviveTargetNode = ActiveReviveTargetNode;
        if (reviveTargetNode is not null && GlobalPosition.DistanceTo(reviveTargetNode.GlobalPosition) < 2.4f)
        {
            // A committed close-range revive is uninterrupted by ordinary contact.
            return;
        }
        var stats = CarriedWeapon.Stats();
        var engageRange = Mathf.Clamp(stats.EffectiveRange * 0.62f, 30.0f, 95.0f);
        var distance = GlobalPosition.DistanceTo(enemy.GlobalPosition);
        if (distance > engageRange || !HasLineOfSight(enemy))
        {
            return;
        }
        HoldAuthoredAimAfterShot();

        var spec = OperatorRoles.Spec(Role);
        var fireBoost = Role == OperatorRole.Assault && _overdriveTime > 0.0f ? 0.68f : 1.0f;
        if (_burstShotsRemaining <= 0)
        {
            _burstShotsRemaining = Role switch
            {
                OperatorRole.Assault => _rng.RandiRange(3, 5),
                OperatorRole.Recon => _rng.RandiRange(1, 2),
                _ => _rng.RandiRange(2, 4)
            };
        }
        _burstShotsRemaining--;
        // Slow primaries (DMRs, snipers) stretch the burst rhythm without stalling it.
        var cadenceScale = Mathf.Clamp(stats.FireInterval / 0.092f, 1.0f, 1.9f);
        _weaponCooldown = (_burstShotsRemaining > 0
            ? _rng.RandfRange(0.12f, 0.19f)
            : _rng.RandfRange(0.42f, 0.72f)) * spec.FireIntervalMultiplier * fireBoost * cadenceScale;
        var targetVelocity = enemy.Velocity;
        targetVelocity.Y = 0.0f;
        var leadSeconds = Mathf.Clamp(distance / 180.0f, 0.04f, 0.22f);
        var hitPoint = enemy.GlobalPosition
            + targetVelocity * leadSeconds
            + Vector3.Up * _rng.RandfRange(0.82f, 1.5f);
        var bodyOrigin = GlobalPosition + Vector3.Up * 1.4f;
        var muzzlePos = IsInstanceValid(_muzzle) ? _muzzle.GlobalPosition : bodyOrigin;
        var shotOrigin = Ballistics.ResolveShotOrigin(GetWorld3D(), bodyOrigin, muzzlePos, GetRid());
        CombatShotsFired++;
        PlayShotAudio();
        Main.NotifyAircraftOperatorAttack(this, GlobalPosition, stats.SoundRadius);
        if (BreakableGlassField.TryShatterAlongRay(
            GetWorld3D(),
            shotOrigin,
            hitPoint,
            stats.Damage * 0.4f,
            shotOrigin.DirectionTo(hitPoint),
            out var glassHitPosition))
        {
            Main.SpawnTracer(shotOrigin, glassHitPosition, new Color(0.34f, 0.78f, 1.0f));
            Main.ReportGunshot(GlobalPosition, stats.SoundRadius);
            return;
        }
        // Wallbang gate on the real damage path.
        if (!Ballistics.HasClearShot(GetWorld3D(), shotOrigin, hitPoint, enemy, GetRid()))
        {
            if (PhysicsRaycast.TryHit(
                    GetWorld3D(),
                    shotOrigin,
                    hitPoint,
                    GetRid(),
                    uint.MaxValue,
                    out var blocked))
            {
                hitPoint = blocked.Position;
            }
            Main.SpawnTracer(shotOrigin, hitPoint, new Color(0.34f, 0.78f, 1.0f));
            return;
        }
        var accuracy = Mathf.Clamp(0.94f - distance * 0.006f, 0.58f, 0.93f);
        if (_rng.Randf() < accuracy)
        {
            enemy.TakeDamage(
                stats.Damage * AmmoTiers.DamageMultiplier(_ammoGrade) * _rng.RandfRange(0.32f, 0.48f),
                hitPoint,
                this,
                AmmoTiers.ArmorPenetration(_ammoGrade));
        }
        else
        {
            hitPoint += new Vector3(_rng.RandfRange(-1.4f, 1.4f), _rng.RandfRange(-0.7f, 1.2f), _rng.RandfRange(-1.4f, 1.4f));
        }
        _magazineRemaining--;
        if (_magazineRemaining <= 0)
        {
            var reloadBoost = Role == OperatorRole.Assault && _overdriveTime > 0.0f ? 0.78f : 1.0f;
            _reloadTimer = Mathf.Clamp(2.6f * spec.ReloadMultiplier * reloadBoost, 1.2f, 3.6f);
            _burstShotsRemaining = 0;
        }
        Main.SpawnTracer(shotOrigin, hitPoint, new Color(0.34f, 0.78f, 1.0f));
        Main.ReportGunshot(GlobalPosition, stats.SoundRadius);
    }

    private bool HasLineOfSight(EnemyOperator enemy)
    {
        var from = GlobalPosition + Vector3.Up * 1.55f;
        var to = enemy.GlobalPosition + Vector3.Up * 1.05f;
        if (Main?.IsLineObscuredBySmoke(from, to) == true)
        {
            return false;
        }
        if (!PhysicsRaycast.TryHit(
                GetWorld3D(),
                from,
                to,
                GetRid(),
                uint.MaxValue,
                out var hit))
        {
            return false;
        }
        var collider = hit.Collider;
        return collider == enemy
            || collider is Node node && (enemy.IsAncestorOf(node) || node.IsAncestorOf(enemy));
    }

    private void ConsiderRoleAbility(EnemyOperator hostile, bool hasSight)
    {
        if (_skillCooldown > 0.0f || _skillActionTime > 0.0f || _decisionTimer > 0.0f)
        {
            return;
        }
        _decisionTimer = _rng.RandfRange(0.7f, 1.3f);
        switch (Role)
        {
            case OperatorRole.Medic:
                var patient = Main.FindLowestFriendly(0.72f, true);
                if (patient is not null)
                {
                    TriggerRoleAbility(patient.CombatNode.GlobalPosition);
                }
                break;
            case OperatorRole.Recon:
                if (!hostile.IsScanned && (!hasSight || GlobalPosition.DistanceTo(hostile.GlobalPosition) > 18.0f))
                {
                    TriggerRoleAbility(hostile.GlobalPosition);
                }
                break;
            case OperatorRole.Assault:
                if (hasSight && GlobalPosition.DistanceTo(hostile.GlobalPosition) < 32.0f)
                {
                    TriggerRoleAbility(hostile.GlobalPosition);
                }
                break;
            case OperatorRole.Scavenger:
                if (!hasSight)
                {
                    TriggerRoleAbility(GlobalPosition);
                }
                break;
            case OperatorRole.Locksmith:
                if (_lootHuntSource is not null)
                {
                    TriggerRoleAbility(_lootHuntSource.LootNode.GlobalPosition);
                }
                break;
        }
    }

    private void ConsiderLootSupport(EnemyOperator? hostile)
    {
        if (_skillCooldown > 0.0f || _skillActionTime > 0.0f || _decisionTimer > 0.0f
            || hostile is not null && GlobalPosition.DistanceTo(hostile.GlobalPosition) < 18.0f)
        {
            return;
        }
        if (Role == OperatorRole.Scavenger)
        {
            _decisionTimer = 1.0f;
            TriggerRoleAbility(GlobalPosition);
        }
        else if (Role == OperatorRole.Locksmith && _lootHuntSource is not null)
        {
            _decisionTimer = 1.0f;
            TriggerRoleAbility(_lootHuntSource.LootNode.GlobalPosition);
        }
    }

    private void ConsiderMedicSupport(ISquadCombatant? patient)
    {
        if (Role != OperatorRole.Medic || _skillCooldown > 0.0f || _skillActionTime > 0.0f || _decisionTimer > 0.0f)
        {
            return;
        }
        if (patient is null || GlobalPosition.DistanceTo(patient.CombatNode.GlobalPosition) > 7.2f)
        {
            return;
        }
        _decisionTimer = 0.8f;
        TriggerRoleAbility(patient.CombatNode.GlobalPosition);
    }

    public bool TriggerRoleAbility(Vector3 focusPoint)
    {
        if (IsDowned || _skillCooldown > 0.0f)
        {
            return false;
        }
        var spec = OperatorRoles.Spec(Role);
        _skillCooldown = SkillCooldownDuration;
        _skillEffectApplied = false;
        if (Role is OperatorRole.Assault or OperatorRole.Locksmith)
        {
            _overdriveTime = spec.SkillDuration;
            Main.SpawnRoleActivationPulse(GlobalPosition + Vector3.Up, spec.Accent, 2.6f);
        }
        else
        {
            _skillActionTime = spec.SkillDuration;
            _roleDevice.Visible = true;
            var levelFocusPoint = new Vector3(
                focusPoint.X,
                _roleDevice.GlobalPosition.Y,
                focusPoint.Z);
            if (_roleDevice.GlobalPosition.DistanceSquaredTo(levelFocusPoint) > 0.01f)
            {
                _roleDevice.LookAt(levelFocusPoint, Vector3.Up);
            }
        }
        return true;
    }

    public bool TriggerRemoteRoleAbility(Vector3 origin, Vector3 forward, bool applyEffect)
    {
        var started = TriggerRoleAbility(origin + forward * 5.0f);
        if (started)
        {
            _networkAbilityPending = Role is not OperatorRole.Assault and not OperatorRole.Locksmith;
            _networkAbilityApplyEffect = applyEffect;
            _networkAbilityOrigin = origin;
            _networkAbilityForward = forward.Normalized();
        }
        return started;
    }

    private void UpdateSkillAction(float delta)
    {
        if (_skillActionTime <= 0.0f)
        {
            _roleDevice.Visible = false;
            return;
        }
        var duration = OperatorRoles.Spec(Role).SkillDuration;
        _skillActionTime = Mathf.Max(0.0f, _skillActionTime - delta);
        var elapsed = duration - _skillActionTime;
        _roleDevice.Visible = true;
        _roleDevice.Position = new Vector3(0.25f, 1.2f + Mathf.Sin(elapsed * 8.0f) * 0.025f, -0.42f);
        if (!_skillEffectApplied && elapsed >= (Role == OperatorRole.Medic ? 0.48f : 0.62f))
        {
            _skillEffectApplied = true;
            if (Role == OperatorRole.Medic)
            {
                if (_networkAbilityPending)
                {
                    if (_networkAbilityApplyEffect)
                    {
                        Main.ApplyMedicSpray(this, _networkAbilityOrigin, _networkAbilityForward);
                    }
                }
                else
                {
                    var patient = Main.FindLowestFriendly(0.82f, true);
                    var forward = patient is null
                        ? -GlobalBasis.Z
                        : GlobalPosition.DirectionTo(patient.CombatNode.GlobalPosition);
                    Main.ApplyMedicSpray(this, GlobalPosition + Vector3.Up * 1.2f, forward);
                }
            }
            else if (Role == OperatorRole.Recon)
            {
                if (!_networkAbilityPending || _networkAbilityApplyEffect)
                {
                    Main.PerformReconScan(this, _networkAbilityPending ? _networkAbilityOrigin : GlobalPosition);
                }
            }
            else if (Role == OperatorRole.Scavenger
                && (!_networkAbilityPending || _networkAbilityApplyEffect))
            {
                Main.PerformLootScan(this, _networkAbilityPending ? _networkAbilityOrigin : GlobalPosition);
            }
        }
        if (_skillActionTime <= 0.0f)
        {
            _roleDevice.Visible = false;
            _networkAbilityPending = false;
            _networkAbilityApplyEffect = false;
        }
    }

    public Vector3 HitPoint(HitRegion region)
    {
        if (IsDowned)
        {
            var downedHeight = region switch
            {
                HitRegion.Head => 0.58f,
                HitRegion.Torso => 0.42f,
                _ => 0.22f
            };
            return GlobalPosition + Vector3.Up * downedHeight;
        }
        var height = region switch
        {
            HitRegion.Head => 1.7f,
            HitRegion.Torso => 1.08f,
            _ => 0.48f
        };
        return GlobalPosition + Vector3.Up * height;
    }

    public bool TakeCombatDamage(
        float amount,
        Vector3 hitPosition,
        Node? attacker = null)
        => TakeCombatDamageInternal(
            amount,
            hitPosition,
            attacker,
            verifyBallisticPath: true);

    internal bool TakeMeleeCombatDamage(
        float amount,
        Vector3 hitPosition,
        Node? attacker = null)
        => TakeCombatDamageInternal(
            amount,
            hitPosition,
            attacker,
            verifyBallisticPath: false);

    internal bool TakeExplosionCombatDamage(
        float amount,
        Vector3 hitPosition,
        Node? attacker = null)
        => TakeCombatDamageInternal(
            amount,
            hitPosition,
            attacker,
            verifyBallisticPath: false,
            forcedRegion: HitRegion.Torso);

    private bool TakeCombatDamageInternal(
        float amount,
        Vector3 hitPosition,
        Node? attacker,
        bool verifyBallisticPath,
        HitRegion? forcedRegion = null)
    {
        if (IsBodyBag)
        {
            return true;
        }
        if (verifyBallisticPath
            && attacker is EnemyOperator enemy
            && !enemy.HasClearBallisticPath(this, hitPosition))
        {
            return false;
        }
        if (attacker is EnemyOperator combatThreat && !combatThreat.IsDead)
        {
            RegisterCombatThreat(combatThreat);
        }
        if (IsDowned)
        {
            // Already waiting for revive — extra hits do not convert yet.
            return true;
        }
        var region = forcedRegion ?? HitRegion.Torso;
        if (!forcedRegion.HasValue && attacker is EnemyOperator)
        {
            var localHeight = hitPosition.Y - GlobalPosition.Y;
            region = localHeight > 1.5f
                ? HitRegion.Head
                : localHeight < 0.58f ? HitRegion.Limbs : HitRegion.Torso;
        }
        LastDamageRegionForDiagnostics = region;
        var multiplier = region switch
        {
            HitRegion.Head => 1.65f,
            HitRegion.Limbs => 0.74f,
            _ => 1.0f
        };
        var healthBefore = Health;
        Health = Mathf.Max(0.0f, Health - amount * multiplier);
        var appliedDamage = Mathf.Max(0.0f, healthBefore - Health);
        UpdateHealthVisual();
        if (Health > 0.0f)
        {
            CommitAuthoritativeRemoteCombatState();
            Main.OnSquadMateDamageApplied(this, appliedDamage, region, hitPosition, attacker);
            return false;
        }

        // Revive budget already spent → permanent KIA body bag (loot box), not a sliding body.
        if (ReviveUsed)
        {
            ConvertToBodyBag();
            Main.OnSquadMateDamageApplied(this, appliedDamage, region, hitPosition, attacker);
            return true;
        }

        IsDowned = true;
        Velocity = Vector3.Zero;
        OnCombatIncapacitated();
        if (!UsesAuthoredOperatorForDiagnostics)
        {
            _rig.Rotation = new Vector3(Mathf.Pi * 0.5f, 0.0f, 0.0f);
        }
        UpdateAuthoredStanceCollider();
        UpdateLabel();
        CommitAuthoritativeRemoteCombatState();
        Main.OnSquadMateDowned(this);
        Main.OnSquadMateDamageApplied(this, appliedDamage, region, hitPosition, attacker);
        return true;
    }

    public void RestoreHealth(float amount)
    {
        if (amount <= 0.0f || IsDowned || IsBodyBag)
        {
            // Downed mates only recover through TryReceiveRevive.
            return;
        }
        Health = Mathf.Clamp(Health + amount, 0.0f, MaxHealth);
        CommitAuthoritativeRemoteCombatState();
        UpdateHealthVisual();
        UpdateLabel();
    }

    public bool TryReceiveRevive(float healAmount)
    {
        if (!IsDowned || ReviveUsed || IsBodyBag || healAmount <= 0.0f)
        {
            return false;
        }
        ReviveUsed = true;
        IsDowned = false;
        Health = Mathf.Clamp(healAmount, 1.0f, MaxHealth);
        CommitAuthoritativeRemoteCombatState();
        ResetMovementProgress();
        _rig.Rotation = Vector3.Zero;
        if (UsesAuthoredOperatorForDiagnostics)
        {
            _authoredOperatorAnimator.PlayRevived();
        }
        UpdateAuthoredStanceCollider();
        UpdateHealthVisual();
        UpdateLabel();
        return true;
    }

    public bool TryFinishDowned(Node? attacker = null)
    {
        if (!CanBeRevived)
        {
            return false;
        }
        var hitPosition = HitPoint(HitRegion.Torso);
        ConvertToBodyBag();
        Main.OnSquadMateDamageApplied(
            mate: this,
            appliedDamage: 0.0f,
            region: HitRegion.Torso,
            hitPosition: hitPosition,
            attacker: attacker);
        return true;
    }

    /// <summary>Force the permanent body-bag state (second down / diagnostics).</summary>
    public void ConvertToBodyBag()
    {
        if (IsBodyBag)
        {
            return;
        }
        IsBodyBag = true;
        IsDowned = true;
        ReviveUsed = true;
        Health = 0.0f;
        CommitAuthoritativeRemoteCombatState();
        Velocity = Vector3.Zero;
        CollisionLayer = 0;
        CollisionMask = 0;
        if (IsInstanceValid(_rig))
        {
            _rig.Visible = false;
        }
        if (IsInstanceValid(_weapon))
        {
            _weapon.Visible = false;
        }
        if (IsInstanceValid(_roleDevice))
        {
            _roleDevice.Visible = false;
        }
        if (IsInstanceValid(_nameLabel))
        {
            _nameLabel.Visible = false;
        }
        if (IsInstanceValid(_healthFill))
        {
            _healthFill.Visible = false;
        }
        Main.SpawnSquadBodyBag(this);
        Main.OnSquadMateKia(this);
        SetPhysicsProcess(false);
        QueueFree();
    }

    internal void SetSkillCooldownForDiagnostics(float value)
    {
        _skillCooldown = Mathf.Max(0.0f, value);
    }

    private void BuildOperator()
    {
        _collider = new CollisionShape3D
        {
            Position = new Vector3(0.0f, 0.88f, 0.0f),
            Shape = new CapsuleShape3D { Radius = 0.37f, Height = 1.76f }
        };
        AddChild(_collider);
        _rig = new Node3D { Name = "FriendlyOperatorRig" };
        AddChild(_rig);
        _weapon = new Node3D { Name = "SquadRifle" };
        _rig.AddChild(_weapon);
        _roleDevice = new Node3D { Name = "RoleDevice", Visible = false };
        _rig.AddChild(_roleDevice);

        var uniform = TacticalSurfaceLibrary.Fabric(new Color(0.11f, 0.13f, 0.125f), 0.93f, 7.5f);
        var armor = Mat(new Color(0.045f, 0.058f, 0.058f), 0.35f, 0.55f);
        var armorEdge = Mat(new Color(0.022f, 0.031f, 0.03f), 0.58f, 0.4f);
        var fabric = TacticalSurfaceLibrary.Fabric(new Color(0.16f, 0.145f, 0.1f), 0.96f, 9.0f);
        var skin = Mat(new Color(0.39f, 0.27f, 0.19f), 0.0f, 0.92f);
        var gun = TacticalSurfaceLibrary.WeaponFinish(new Color(0.025f, 0.032f, 0.032f), 0.8f, 0.28f);
        var lens = Mat(new Color(0.025f, 0.2f, 0.21f), 0.62f, 0.08f);
        Part(_rig, Capsule(0.27f, 0.88f), new Vector3(0.0f, 1.08f, 0.0f), uniform);
        Part(_rig, Box(new Vector3(0.58f, 0.52f, 0.25f)), new Vector3(0.0f, 1.2f, 0.0f), armor);
        Part(_rig, Box(new Vector3(0.52f, 0.46f, 0.16f)), new Vector3(0.0f, 1.2f, 0.19f), armorEdge);
        Part(_rig, Box(new Vector3(0.4f, 0.14f, 0.27f)), new Vector3(0.0f, 0.91f, 0.01f), fabric);
        for (var pouch = -1; pouch <= 1; pouch++)
        {
            Part(_rig, Box(new Vector3(0.13f, 0.17f, 0.1f)), new Vector3(pouch * 0.145f, 0.99f, -0.17f), fabric);
        }
        Part(_rig, Box(new Vector3(0.33f, 0.44f, 0.2f)), new Vector3(0.0f, 1.2f, 0.23f), armor);
        Part(_rig, Box(new Vector3(0.035f, 0.36f, 0.035f)), new Vector3(0.14f, 1.57f, 0.25f), armorEdge, new Vector3(0.08f, 0.0f, 0.04f));
        Part(_rig, Capsule(0.15f, 0.34f), new Vector3(0.0f, 1.7f, 0.0f), skin);
        Part(_rig, Box(new Vector3(0.28f, 0.12f, 0.055f)), new Vector3(0.0f, 1.67f, -0.145f), armorEdge);
        Part(_rig, Box(new Vector3(0.34f, 0.07f, 0.075f)), new Vector3(0.0f, 1.75f, -0.16f), lens);
        Part(_rig, Box(new Vector3(0.42f, 0.08f, 0.3f)), new Vector3(0.0f, 1.79f, 0.0f), armor);
        Part(_rig, Capsule(0.19f, 0.24f), new Vector3(0.0f, 1.84f, 0.01f), armor);
        Part(_rig, Cylinder(0.052f, 0.08f), new Vector3(-0.18f, 1.72f, 0.0f), armorEdge, new Vector3(0.0f, 0.0f, Mathf.Pi / 2.0f));
        Part(_rig, Cylinder(0.052f, 0.08f), new Vector3(0.18f, 1.72f, 0.0f), armorEdge, new Vector3(0.0f, 0.0f, Mathf.Pi / 2.0f));
        Part(_rig, Capsule(0.12f, 0.72f), new Vector3(-0.17f, 0.46f, 0.0f), uniform);
        Part(_rig, Capsule(0.12f, 0.72f), new Vector3(0.17f, 0.46f, 0.0f), uniform);
        Part(_rig, Box(new Vector3(0.18f, 0.18f, 0.16f)), new Vector3(-0.17f, 0.56f, -0.09f), armor);
        Part(_rig, Box(new Vector3(0.18f, 0.18f, 0.16f)), new Vector3(0.17f, 0.56f, -0.09f), armor);
        Part(_rig, Box(new Vector3(0.18f, 0.14f, 0.3f)), new Vector3(-0.17f, 0.13f, -0.055f), gun);
        Part(_rig, Box(new Vector3(0.18f, 0.14f, 0.3f)), new Vector3(0.17f, 0.13f, -0.055f), gun);
        Part(_rig, Capsule(0.1f, 0.58f), new Vector3(-0.33f, 1.22f, -0.12f), uniform, new Vector3(0.7f, 0.0f, -0.15f));
        Part(_rig, Capsule(0.1f, 0.58f), new Vector3(0.33f, 1.22f, -0.12f), uniform, new Vector3(0.7f, 0.0f, 0.15f));
        Part(_rig, Box(new Vector3(0.19f, 0.16f, 0.19f)), new Vector3(-0.34f, 1.35f, -0.02f), armor);
        Part(_rig, Box(new Vector3(0.19f, 0.16f, 0.19f)), new Vector3(0.34f, 1.35f, -0.02f), armor);
        AttachAuthoredOperatorVisual();
        Part(_weapon, Box(new Vector3(0.13f, 0.14f, 0.56f)), new Vector3(0.0f, 1.24f, -0.36f), gun);
        Part(_weapon, Box(new Vector3(0.1f, 0.24f, 0.13f)), new Vector3(0.0f, 1.07f, -0.35f), gun, new Vector3(-0.18f, 0.0f, 0.0f));
        Part(_weapon, Cylinder(0.025f, 0.48f), new Vector3(0.0f, 1.24f, -0.85f), gun, new Vector3(Mathf.Pi / 2.0f, 0.0f, 0.0f));
        _muzzle = new Marker3D { Position = new Vector3(0.0f, 1.24f, -1.1f) };
        _weapon.AddChild(_muzzle);
        BuildShotAudio();
        HasFireablePrimary = true;
        _weapon.Visible = true;
        if (UsesAuthoredOperatorForDiagnostics)
        {
            foreach (var mesh in CombatModelLibrary.MeshesBelow(_weapon))
            {
                mesh.Visible = false;
            }
            SetAuthoredWeaponVisible(true);
        }

        _nameLabel = new Label3D
        {
            Position = new Vector3(0.0f, 2.28f, 0.0f),
            FontSize = 22,
            OutlineSize = 7,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true
        };
        AddChild(_nameLabel);
        var healthBack = Part(this, Box(new Vector3(0.72f, 0.045f, 0.018f)), new Vector3(0.0f, 2.05f, 0.0f),
            Mat(new Color(0.02f, 0.025f, 0.025f), 0.0f, 0.8f));
        healthBack.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        _healthFill = Part(this, Box(new Vector3(0.68f, 0.025f, 0.022f)), new Vector3(0.0f, 2.05f, -0.012f),
            Mat(new Color(0.25f, 0.9f, 0.58f), 0.0f, 0.45f, true));
        _healthFill.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;

        Part(_roleDevice, Box(new Vector3(0.27f, 0.19f, 0.09f)), Vector3.Zero,
            Mat(new Color(0.06f, 0.08f, 0.085f), 0.4f, 0.45f));
        UpdateLabel();
        UpdateHealthVisual();
    }

    private void ApplyRoleVisuals()
    {
        if (!IsInstanceValid(_rig))
        {
            return;
        }
        var accent = OperatorRoles.Spec(Role).Accent;
        SetAuthoredRoleColor(accent);
        if (Role == OperatorRole.Medic)
        {
            Part(_roleDevice, Cylinder(0.045f, 0.25f), new Vector3(0.0f, 0.0f, -0.17f),
                Mat(accent, 0.0f, 0.3f, true), new Vector3(Mathf.Pi / 2.0f, 0.0f, 0.0f));
        }
        else if (Role is OperatorRole.Recon or OperatorRole.Scavenger)
        {
            Part(_roleDevice, Box(new Vector3(0.21f, 0.12f, 0.012f)), new Vector3(0.0f, 0.0f, -0.055f),
                Mat(accent, 0.0f, 0.25f, true));
        }
        UpdateLabel();
    }

    private void AnimateRig(float delta)
    {
        var speed = new Vector2(Velocity.X, Velocity.Z).Length();
        UpdateRevivePose(delta);
        if (UsesAuthoredOperatorForDiagnostics)
        {
            AnimateAuthoredOperator(delta, speed);
            UpdateAuthoredStanceCollider();
            UpdateHealthVisual();
            return;
        }
        _animationPhase += delta * (4.0f + speed * 1.5f);
        if (!IsDowned)
        {
            var position = _rig.Position;
            position.Y = Mathf.Lerp(position.Y, Mathf.Sin(_animationPhase * 2.0f) * 0.012f * Mathf.Clamp(speed, 0.0f, 1.0f), delta * 9.0f);
            _rig.Position = position;
        }
        UpdateHealthVisual();
    }

    private void UpdateRevivePose(float delta)
    {
        var reviveTargetNode = ActiveReviveTargetNode;
        if (reviveTargetNode is null && _revivePoseBlend <= 0.0f)
        {
            return;
        }
        var kneeling = reviveTargetNode is not null && !IsDowned
            && GlobalPosition.DistanceTo(reviveTargetNode.GlobalPosition) < 2.4f;
        _revivePoseBlend = Mathf.MoveToward(_revivePoseBlend, kneeling ? 1.0f : 0.0f, delta * 5.0f);
        if (!IsInstanceValid(_rig))
        {
            return;
        }
        if (UsesAuthoredOperatorForDiagnostics)
        {
            return;
        }
        _rig.Rotation = new Vector3(0.52f * _revivePoseBlend, 0.0f, 0.0f);
        var position = _rig.Position;
        position.Y = Mathf.Lerp(position.Y, -0.22f * _revivePoseBlend, delta * 8.0f);
        _rig.Position = position;
    }

    private void UpdateLabel()
    {
        if (!IsInstanceValid(_nameLabel))
        {
            return;
        }
        var source = IsHumanProxy ? "HUMAN" : "AI";
        var state = IsDowned
            ? ReviveUsed ? "  //  ELIMINATED" : "  //  DOWN"
            : string.Empty;
        _nameLabel.Text = $"{Callsign}  [{OperatorRoles.Spec(Role).Name}]  {source}{state}";
        _nameLabel.Modulate = IsDowned ? new Color(1.0f, 0.3f, 0.2f) : OperatorRoles.Spec(Role).Accent;
    }

    private void UpdateHealthVisual()
    {
        if (!IsInstanceValid(_healthFill))
        {
            return;
        }
        var ratio = Mathf.Clamp(Health / Mathf.Max(1.0f, MaxHealth), 0.0f, 1.0f);
        _healthFill.Scale = new Vector3(Mathf.Max(0.01f, ratio), 1.0f, 1.0f);
        _healthFill.Position = new Vector3(-0.34f * (1.0f - ratio), 2.05f, -0.012f);
    }

    private static StandardMaterial3D Mat(Color color, float metallic, float roughness, bool emissive = false)
    {
        var material = new StandardMaterial3D
        {
            AlbedoColor = color,
            Metallic = metallic,
            Roughness = roughness
        };
        if (emissive)
        {
            material.EmissionEnabled = true;
            material.Emission = color;
            material.EmissionEnergyMultiplier = 1.6f;
        }
        return material;
    }

    private static MeshInstance3D Part(Node3D parent, PrimitiveMesh mesh, Vector3 position, Material material, Vector3 rotation = default)
    {
        var part = new MeshInstance3D
        {
            Mesh = mesh,
            Position = position,
            Rotation = rotation,
            MaterialOverride = material
        };
        parent.AddChild(part);
        return part;
    }

    private static BoxMesh Box(Vector3 size) => new() { Size = size };

    private static CapsuleMesh Capsule(float radius, float height) => new()
    {
        Radius = radius,
        Height = height,
        RadialSegments = 12,
        Rings = 6
    };

    private static CylinderMesh Cylinder(float radius, float height) => new()
    {
        TopRadius = radius,
        BottomRadius = radius,
        Height = height,
        RadialSegments = 12
    };
}
