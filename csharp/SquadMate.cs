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
    /// <summary>Second death after revive budget is spent — converted to a lootable body bag.</summary>
    public bool IsBodyBag { get; private set; }
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
    }

    public void GrantFireablePrimaryForDiagnostics()
    {
        HasFireablePrimary = true;
        if (IsInstanceValid(_weapon))
        {
            _weapon.Visible = true;
        }
    }

    /// <summary>Production path: equip a weapon taken from a world loot source.</summary>
    public bool EquipWeaponFromLoot(WeaponBuild build)
    {
        if (build is null)
        {
            return false;
        }
        HasFireablePrimary = true;
        if (IsInstanceValid(_weapon))
        {
            _weapon.Visible = true;
        }
        if (IsInstanceValid(_muzzle))
        {
            _muzzle.Visible = true;
        }
        return HasFireablePrimary;
    }

    private readonly RandomNumberGenerator _rng = new();
    private Node3D _rig = null!;
    private Node3D _weapon = null!;
    private Node3D _roleDevice = null!;
    private Marker3D _muzzle = null!;
    private MeshInstance3D _healthFill = null!;
    private Label3D _nameLabel = null!;
    private Vector3 _orderPosition;
    private Vector3 _remotePosition;
    private Vector3 _remoteRotation;
    private float _weaponCooldown;
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
    private bool _revivingLeader;
    private float _revivePoseBlend;

    public void Configure(
        FreightTerminalWorld main,
        TacticalPlayer leader,
        int slot,
        OperatorRole role,
        string callsign,
        bool humanProxy = false,
        long peerId = 0)
    {
        Main = main;
        Leader = leader;
        SquadSlot = slot;
        Role = role;
        Callsign = callsign;
        IsHumanProxy = humanProxy;
        NetworkPeerId = peerId;
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
        AddToGroup("player_squad_ai");
        BuildOperator();
        ApplyRoleVisuals();
        InitializeCombatTactics();
        if (!IsHumanProxy)
        {
            _skillCooldown = SkillCooldownDuration * Mathf.Clamp(0.24f + SquadSlot * 0.11f, 0.35f, 0.62f);
        }
        _remotePosition = GlobalPosition;
        _remoteRotation = Rotation;
    }

    public void SetOrder(SquadOrder order, Vector3 position)
    {
        if (IsHumanProxy)
        {
            return;
        }
        Order = order;
        _orderPosition = order == SquadOrder.Follow ? GlobalPosition : position;
        if (order != SquadOrder.Follow && IsInstanceValid(Main))
        {
            Main.ClearSquadNavigation(this);
        }
        OnSquadOrderChanged();
        UpdateLabel();
    }

    public bool IsRevivingLeader => _revivingLeader;

    /// <summary>Task this mate with running to the downed leader and reviving them.</summary>
    public void BeginLeaderRevive()
    {
        if (IsDowned || IsBodyBag || IsHumanProxy)
        {
            return;
        }
        _revivingLeader = true;
    }

    public void EndLeaderRevive()
    {
        _revivingLeader = false;
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
        if (!IsHumanProxy)
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
        Main.SpawnTracer(_muzzle.GlobalPosition, end, new Color(0.32f, 0.78f, 1.0f));
    }

    public override void _PhysicsProcess(double delta)
    {
        var dt = (float)delta;
        _weaponCooldown = Mathf.Max(0.0f, _weaponCooldown - dt);
        _skillCooldown = Mathf.Max(0.0f, _skillCooldown - dt);
        _overdriveTime = Mathf.Max(0.0f, _overdriveTime - dt);
        _decisionTimer = Mathf.Max(0.0f, _decisionTimer - dt);
        UpdateCombatTacticalTimers(dt);

        if (IsHumanProxy)
        {
            UpdateRemoteProxy(dt);
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
            _rig.Rotation = new Vector3(Mathf.Pi * 0.5f, 0.0f, 0.0f);
            UpdateLabel();
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
        if (_revivingLeader)
        {
            destination = Main.ResolveSquadNavigationDestination(this, Leader.GlobalPosition, emergency: true);
            objectivePriority = true;
        }
        else if (Order == SquadOrder.Follow && hostile is null && !objectivePriority)
        {
            destination = Main.ResolveSquadNavigationDestination(this, destination, emergency: false);
        }
        UpdateTacticalMovement(destination, hostile, objectivePriority, dt);
        ConsiderMedicSupport(patient);
        if (hostile is not null && !hostile.IsDead)
        {
            TryFire(hostile);
            ConsiderRoleAbility(hostile, _combatHasSight);
        }
        MoveAndSlide();
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
        if (!HasFireablePrimary || _weaponCooldown > 0.0f || _skillActionTime > 0.0f)
        {
            return;
        }
        if (_revivingLeader && _revivePoseBlend > 0.5f)
        {
            // Kneeling revive channel: hold fire until back on the move.
            return;
        }
        var distance = GlobalPosition.DistanceTo(enemy.GlobalPosition);
        if (distance > 55.0f || !HasLineOfSight(enemy))
        {
            return;
        }

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
        _weaponCooldown = (_burstShotsRemaining > 0
            ? _rng.RandfRange(0.12f, 0.19f)
            : _rng.RandfRange(0.42f, 0.72f)) * spec.FireIntervalMultiplier * fireBoost;
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
        if (BreakableGlassField.TryShatterAlongRay(
            GetWorld3D(),
            shotOrigin,
            hitPoint,
            12.0f,
            shotOrigin.DirectionTo(hitPoint),
            out var glassHitPosition))
        {
            Main.SpawnTracer(shotOrigin, glassHitPosition, new Color(0.34f, 0.78f, 1.0f));
            Main.ReportGunshot(GlobalPosition, 52.0f);
            return;
        }
        // Wallbang gate on the real damage path.
        if (!Ballistics.HasClearShot(GetWorld3D(), shotOrigin, hitPoint, enemy, GetRid()))
        {
            var query = PhysicsRayQueryParameters3D.Create(shotOrigin, hitPoint);
            query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
            query.CollideWithAreas = false;
            var blocked = GetWorld3D().DirectSpaceState.IntersectRay(query);
            if (blocked.Count > 0)
            {
                hitPoint = blocked["position"].AsVector3();
            }
            Main.SpawnTracer(shotOrigin, hitPoint, new Color(0.34f, 0.78f, 1.0f));
            return;
        }
        var accuracy = Mathf.Clamp(0.91f - distance * 0.009f, 0.48f, 0.9f);
        if (_rng.Randf() < accuracy)
        {
            enemy.TakeDamage(_rng.RandfRange(11.0f, 16.5f), hitPoint, this);
        }
        else
        {
            hitPoint += new Vector3(_rng.RandfRange(-1.4f, 1.4f), _rng.RandfRange(-0.7f, 1.2f), _rng.RandfRange(-1.4f, 1.4f));
        }
        Main.SpawnTracer(shotOrigin, hitPoint, new Color(0.34f, 0.78f, 1.0f));
        Main.ReportGunshot(GlobalPosition, 52.0f);
    }

    private bool HasLineOfSight(EnemyOperator enemy)
    {
        var from = GlobalPosition + Vector3.Up * 1.55f;
        var to = enemy.GlobalPosition + Vector3.Up * 1.05f;
        var query = PhysicsRayQueryParameters3D.Create(from, to);
        query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
        query.CollideWithAreas = false;
        var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (hit.Count == 0)
        {
            return false;
        }
        var collider = hit["collider"].AsGodotObject();
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
        if (Role == OperatorRole.Assault)
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

    public bool TriggerRemoteRoleAbility(Vector3 origin, Vector3 forward)
    {
        var started = TriggerRoleAbility(origin + forward * 5.0f);
        if (started)
        {
            _networkAbilityPending = Role != OperatorRole.Assault;
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
                    Main.ApplyMedicSpray(this, _networkAbilityOrigin, _networkAbilityForward);
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
            else
            {
                Main.PerformReconScan(this, _networkAbilityPending ? _networkAbilityOrigin : GlobalPosition);
            }
        }
        if (_skillActionTime <= 0.0f)
        {
            _roleDevice.Visible = false;
            _networkAbilityPending = false;
        }
    }

    public Vector3 HitPoint(HitRegion region)
    {
        var height = region switch
        {
            HitRegion.Head => 1.7f,
            HitRegion.Torso => 1.08f,
            _ => 0.48f
        };
        return GlobalPosition + Vector3.Up * height;
    }

    public bool TakeCombatDamage(float amount, Vector3 hitPosition, Node? attacker = null)
    {
        if (IsBodyBag)
        {
            return true;
        }
        if (attacker is EnemyOperator enemy && !enemy.HasClearBallisticPath(this, hitPosition))
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
        var localHeight = hitPosition.Y - GlobalPosition.Y;
        var multiplier = localHeight > 1.5f ? 1.65f : localHeight < 0.58f ? 0.74f : 1.0f;
        Health = Mathf.Max(0.0f, Health - amount * multiplier);
        UpdateHealthVisual();
        if (Health > 0.0f)
        {
            return false;
        }

        // Revive budget already spent → permanent KIA body bag (loot box), not a sliding body.
        if (ReviveUsed)
        {
            ConvertToBodyBag();
            return true;
        }

        IsDowned = true;
        Velocity = Vector3.Zero;
        OnCombatIncapacitated();
        _rig.Rotation = new Vector3(Mathf.Pi * 0.5f, 0.0f, 0.0f);
        UpdateLabel();
        Main.OnSquadMateDowned(this);
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
        ResetMovementProgress();
        _rig.Rotation = Vector3.Zero;
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
        ConvertToBodyBag();
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
        AddChild(new CollisionShape3D
        {
            Position = new Vector3(0.0f, 0.88f, 0.0f),
            Shape = new CapsuleShape3D { Radius = 0.37f, Height = 1.76f }
        });
        _rig = new Node3D { Name = "FriendlyOperatorRig" };
        AddChild(_rig);
        _weapon = new Node3D { Name = "SquadRifle" };
        _rig.AddChild(_weapon);
        _roleDevice = new Node3D { Name = "RoleDevice", Visible = false };
        _rig.AddChild(_roleDevice);

        var uniform = Mat(new Color(0.11f, 0.13f, 0.125f), 0.02f, 0.9f);
        var armor = Mat(new Color(0.045f, 0.058f, 0.058f), 0.35f, 0.55f);
        var armorEdge = Mat(new Color(0.022f, 0.031f, 0.03f), 0.58f, 0.4f);
        var fabric = Mat(new Color(0.16f, 0.145f, 0.1f), 0.0f, 0.94f);
        var skin = Mat(new Color(0.39f, 0.27f, 0.19f), 0.0f, 0.92f);
        var gun = Mat(new Color(0.025f, 0.032f, 0.032f), 0.8f, 0.28f);
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
        Part(_weapon, Box(new Vector3(0.13f, 0.14f, 0.56f)), new Vector3(0.0f, 1.24f, -0.36f), gun);
        Part(_weapon, Box(new Vector3(0.1f, 0.24f, 0.13f)), new Vector3(0.0f, 1.07f, -0.35f), gun, new Vector3(-0.18f, 0.0f, 0.0f));
        Part(_weapon, Cylinder(0.025f, 0.48f), new Vector3(0.0f, 1.24f, -0.85f), gun, new Vector3(Mathf.Pi / 2.0f, 0.0f, 0.0f));
        _muzzle = new Marker3D { Position = new Vector3(0.0f, 1.24f, -1.1f) };
        _weapon.AddChild(_muzzle);
        HasFireablePrimary = true;
        _weapon.Visible = true;

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
        Part(_rig, Box(new Vector3(0.5f, 0.075f, 0.03f)), new Vector3(0.0f, 1.38f, -0.14f),
            Mat(accent, 0.0f, 0.35f, true));
        if (Role == OperatorRole.Medic)
        {
            Part(_roleDevice, Cylinder(0.045f, 0.25f), new Vector3(0.0f, 0.0f, -0.17f),
                Mat(accent, 0.0f, 0.3f, true), new Vector3(Mathf.Pi / 2.0f, 0.0f, 0.0f));
        }
        else if (Role == OperatorRole.Recon)
        {
            Part(_roleDevice, Box(new Vector3(0.21f, 0.12f, 0.012f)), new Vector3(0.0f, 0.0f, -0.055f),
                Mat(accent, 0.0f, 0.25f, true));
        }
        UpdateLabel();
    }

    private void AnimateRig(float delta)
    {
        var speed = new Vector2(Velocity.X, Velocity.Z).Length();
        _animationPhase += delta * (4.0f + speed * 1.5f);
        if (!IsDowned)
        {
            var position = _rig.Position;
            position.Y = Mathf.Lerp(position.Y, Mathf.Sin(_animationPhase * 2.0f) * 0.012f * Mathf.Clamp(speed, 0.0f, 1.0f), delta * 9.0f);
            _rig.Position = position;
        }
        UpdateRevivePose(delta);
        UpdateHealthVisual();
    }

    private void UpdateRevivePose(float delta)
    {
        if (!_revivingLeader && _revivePoseBlend <= 0.0f)
        {
            return;
        }
        var kneeling = _revivingLeader && !IsDowned
            && GlobalPosition.DistanceTo(Leader.GlobalPosition) < 2.4f;
        _revivePoseBlend = Mathf.MoveToward(_revivePoseBlend, kneeling ? 1.0f : 0.0f, delta * 5.0f);
        if (!IsInstanceValid(_rig))
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
        var state = IsDowned ? "  //  DOWN" : string.Empty;
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
