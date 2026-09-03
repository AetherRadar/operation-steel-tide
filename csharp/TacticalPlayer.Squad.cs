using Godot;

namespace OperationSteelTide;

public partial class TacticalPlayer
{
    public OperatorRole Role { get; private set; } = OperatorRole.Assault;
    public float MaxHealth { get; private set; } = 125.0f;
    public float SkillCooldownRemaining => _skillCooldownRemaining;
    public float SkillCooldownDuration => OperatorRoles.Spec(Role).SkillCooldown;
    public bool SkillActive => _skillActiveRemaining > 0.0f || _roleActionRemaining > 0.0f;
    public float RoleMovementMultiplier => OperatorRoles.Spec(Role).MovementMultiplier
        * (Role == OperatorRole.Assault && _skillActiveRemaining > 0.0f ? 1.25f : 1.0f);
    public float RoleFireIntervalMultiplier => OperatorRoles.Spec(Role).FireIntervalMultiplier
        * (Role == OperatorRole.Assault && _skillActiveRemaining > 0.0f ? 0.72f : 1.0f);
    public float RoleReloadMultiplier => OperatorRoles.Spec(Role).ReloadMultiplier
        * (Role == OperatorRole.Assault && _skillActiveRemaining > 0.0f ? 0.75f : 1.0f);
    public float RoleRecoilMultiplier => Role == OperatorRole.Assault && _skillActiveRemaining > 0.0f ? 0.72f : 1.0f;
    public float RoleSearchDurationMultiplier => OperatorRoles.Spec(Role).SearchDurationMultiplier
        * (Role == OperatorRole.Locksmith && _skillActiveRemaining > 0.0f ? 0.32f : 1.0f);
    public bool RoleActionBlocksWeapon => _roleActionRemaining > 0.0f;

    public Node3D CombatNode => this;
    public bool CombatDead => IsDead;
    public bool CombatDowned => IsDead;
    public bool ReviveUsed { get; private set; }
    public bool CanBeRevived => IsDead && !ReviveUsed;
    public float CombatHealth => Health;
    public float CombatMaxHealth => MaxHealth;
    public float CrawlSpeed => 1.15f;
    public bool IsExtractionPassenger { get; private set; }

    private Node3D _roleDeviceRoot = null!;
    private Node3D _medicSprayer = null!;
    private Node3D _reconScanner = null!;
    private Node3D _assaultInjector = null!;
    private float _skillCooldownRemaining;
    private float _skillActiveRemaining;
    private float _roleActionRemaining;
    private float _roleActionElapsed;
    private bool _roleEffectApplied;
    private float _activeReloadDuration = ReloadDuration;

    public void ConfigureRole(OperatorRole role, bool refillHealth = true)
    {
        Role = role;
        var spec = OperatorRoles.Spec(role);
        MaxHealth = spec.MaxHealth;
        Health = refillHealth ? MaxHealth : Mathf.Clamp(Health, 1.0f, MaxHealth);
        IsDead = false;
        ReviveUsed = false;
        _skillCooldownRemaining = 0.0f;
        _skillActiveRemaining = 0.0f;
        _roleActionRemaining = 0.0f;
        _roleActionElapsed = 0.0f;
        _roleEffectApplied = false;
        if (IsInstanceValid(_roleDeviceRoot))
        {
            SetRoleDeviceVisibility();
        }
        Hud?.SetClassSkill(Role, 0.0f, spec.SkillCooldown, false, false);
    }

    public void BoardExtractionSeat(Node3D seat)
    {
        if (!GodotObject.IsInstanceValid(seat) || IsExtractionPassenger)
        {
            return;
        }

        EjectFromVehicleIfAny();
        CloseMedicalWheelWithoutUse();
        CancelFieldUse(false);
        CancelReload();
        UiLocked = true;
        DisarmFireInput();
        DisarmMovementInput();
        Velocity = Vector3.Zero;
        CollisionLayer = 0;
        CollisionMask = 0;
        _collider.Disabled = true;
        if (IsInstanceValid(_camera))
        {
            _camera.Current = false;
        }
        if (IsInstanceValid(_weaponRoot))
        {
            _weaponRoot.Visible = false;
        }
        if (IsInstanceValid(_knifeRoot))
        {
            _knifeRoot.Visible = false;
        }
        if (IsInstanceValid(_roleDeviceRoot))
        {
            _roleDeviceRoot.Visible = false;
        }

        Reparent(seat, keepGlobalTransform: false);
        Position = Vector3.Zero;
        Rotation = Vector3.Zero;
        IsExtractionPassenger = true;
        UpdateHeldThrowableVisual();
        SetPhysicsProcess(false);
    }

    public bool ActivateRoleAbility(bool broadcast = true)
    {
        if (IsDead || UiLocked || MedicalActionBlocksWeapon || _skillCooldownRemaining > 0.0f || _isReloading || _isPlating)
        {
            return false;
        }

        var spec = OperatorRoles.Spec(Role);
        _skillCooldownRemaining = spec.SkillCooldown;
        _roleEffectApplied = false;
        if (Role is OperatorRole.Assault or OperatorRole.Locksmith)
        {
            _skillActiveRemaining = spec.SkillDuration;
            if (Role == OperatorRole.Assault)
            {
                _assaultInjector.Visible = true;
                Hud?.ShowLocalizedMessage(
                    "assault_overdrive",
                    "COMBAT OVERDRIVE  //  SPEED + FIRE RATE + HANDLING",
                    spec.Accent);
            }
            else
            {
                Hud?.ShowLocalizedMessage(
                    "skeleton_key_active",
                    "SKELETON KEY  //  RAPID UNLOCK + SEARCH ACTIVE",
                    spec.Accent);
            }
            Main?.SpawnRoleActivationPulse(GlobalPosition + Vector3.Up, spec.Accent, 3.2f);
        }
        else
        {
            _roleActionRemaining = spec.SkillDuration;
            _roleActionElapsed = 0.0f;
            _isAiming = false;
            CancelReload();
            SetRoleDeviceVisibility();
        }

        if (broadcast)
        {
            var authoritativeView = CaptureAuthoritativeViewTransform();
            Main?.OnLocalRoleAbility(
                Role,
                authoritativeView.Origin,
                -authoritativeView.Basis.Z);
        }
        return true;
    }

    private void UpdateRoleAbility(float delta)
    {
        _skillCooldownRemaining = Mathf.Max(0.0f, _skillCooldownRemaining - delta);
        _skillActiveRemaining = Mathf.Max(0.0f, _skillActiveRemaining - delta);

        if (_roleActionRemaining > 0.0f)
        {
            _roleActionRemaining = Mathf.Max(0.0f, _roleActionRemaining - delta);
            _roleActionElapsed += delta;
            AnimateRoleDevice(delta);

            var effectTime = Role == OperatorRole.Medic ? 0.48f : 0.62f;
            if (!_roleEffectApplied && _roleActionElapsed >= effectTime)
            {
                _roleEffectApplied = true;
                if (Role == OperatorRole.Medic)
                {
                    var authoritativeView = CaptureAuthoritativeViewTransform();
                    var nozzle = authoritativeView.Origin
                        + authoritativeView.Basis.X * 0.28f
                        - authoritativeView.Basis.Y * 0.2f
                        - authoritativeView.Basis.Z * 0.58f;
                    Main?.ApplyMedicSpray(
                        this,
                        nozzle,
                        -authoritativeView.Basis.Z);
                }
                else if (Role == OperatorRole.Recon)
                {
                    Main?.PerformReconScan(this, GlobalPosition);
                }
                else if (Role == OperatorRole.Scavenger)
                {
                    Main?.PerformLootScan(this, GlobalPosition);
                }
            }

            if (_roleActionRemaining <= 0.0f)
            {
                SetRoleDeviceVisibility();
            }
        }
        else if (IsInstanceValid(_assaultInjector))
        {
            _assaultInjector.Visible = Role == OperatorRole.Assault && _skillActiveRemaining > 0.0f;
            if (_assaultInjector.Visible)
            {
                _assaultInjector.Rotation = new Vector3(
                    -0.22f + Mathf.Sin(Time.GetTicksMsec() * 0.012f) * 0.035f,
                    0.16f,
                    0.18f);
            }
        }

        Hud?.SetClassSkill(
            Role,
            _skillCooldownRemaining,
            OperatorRoles.Spec(Role).SkillCooldown,
            _skillActiveRemaining > 0.0f,
            _roleActionRemaining > 0.0f);
    }

    internal void AdvanceRoleAbilityForDiagnostics(float delta)
        => UpdateRoleAbility(delta);

    private void BuildRoleDevices()
    {
        _roleDeviceRoot = new Node3D { Name = "RoleDevices" };
        _camera.AddChild(_roleDeviceRoot);

        _medicSprayer = new Node3D
        {
            Name = "MedicSprayer",
            Position = new Vector3(0.34f, -0.31f, -0.76f),
            Rotation = new Vector3(-0.18f, -0.16f, -0.06f),
            Scale = Vector3.One * 0.68f
        };
        _roleDeviceRoot.AddChild(_medicSprayer);
        AddRoleMesh(_medicSprayer, Box(new Vector3(0.2f, 0.29f, 0.14f)), Vector3.Zero,
            new Color(0.1f, 0.16f, 0.15f), 0.15f);
        AddRoleMesh(_medicSprayer, Cylinder(0.065f, 0.2f), new Vector3(0.0f, 0.18f, -0.015f),
            new Color(0.24f, 0.64f, 0.46f), 0.0f, new Vector3(0.0f, 0.0f, Mathf.Pi / 2.0f));
        AddRoleMesh(_medicSprayer, Cylinder(0.025f, 0.22f), new Vector3(0.0f, 0.02f, -0.16f),
            new Color(0.24f, 0.29f, 0.27f), 0.4f, new Vector3(Mathf.Pi / 2.0f, 0.0f, 0.0f));
        AddRoleMesh(_medicSprayer, Box(new Vector3(0.1f, 0.06f, 0.035f)), new Vector3(0.0f, 0.02f, -0.27f),
            new Color(0.58f, 1.0f, 0.76f), 0.0f, Vector3.Zero, true);

        _reconScanner = new Node3D
        {
            Name = "ReconScanner",
            Position = new Vector3(0.18f, -0.2f, -0.48f),
            Rotation = new Vector3(-0.15f, 0.12f, 0.05f),
            Scale = Vector3.One * 0.78f
        };
        _roleDeviceRoot.AddChild(_reconScanner);
        AddRoleMesh(_reconScanner, Box(new Vector3(0.34f, 0.23f, 0.08f)), Vector3.Zero,
            new Color(0.055f, 0.085f, 0.1f), 0.45f);
        AddRoleMesh(_reconScanner, Box(new Vector3(0.27f, 0.15f, 0.015f)), new Vector3(0.0f, 0.01f, -0.048f),
            new Color(0.18f, 0.7f, 0.94f), 0.0f, Vector3.Zero, true);
        AddRoleMesh(_reconScanner, Cylinder(0.012f, 0.25f), new Vector3(0.13f, 0.22f, 0.0f),
            new Color(0.23f, 0.28f, 0.3f), 0.5f);
        AddRoleMesh(_reconScanner, Cylinder(0.025f, 0.04f), new Vector3(0.13f, 0.36f, 0.0f),
            new Color(0.32f, 0.82f, 1.0f), 0.0f, Vector3.Zero, true);

        _assaultInjector = new Node3D
        {
            Name = "AssaultInjector",
            Position = new Vector3(-0.28f, -0.29f, -0.5f),
            Rotation = new Vector3(-0.22f, 0.16f, 0.18f)
        };
        _roleDeviceRoot.AddChild(_assaultInjector);
        AddRoleMesh(_assaultInjector, Cylinder(0.035f, 0.3f), Vector3.Zero,
            new Color(0.18f, 0.2f, 0.21f), 0.65f, new Vector3(0.0f, 0.0f, Mathf.Pi / 2.0f));
        AddRoleMesh(_assaultInjector, Cylinder(0.024f, 0.2f), Vector3.Zero,
            new Color(1.0f, 0.45f, 0.12f), 0.0f, new Vector3(0.0f, 0.0f, Mathf.Pi / 2.0f), true);

        SetRoleDeviceVisibility();
    }

    private static MeshInstance3D AddRoleMesh(
        Node3D parent,
        PrimitiveMesh mesh,
        Vector3 position,
        Color color,
        float metallic = 0.0f,
        Vector3 rotation = default,
        bool emissive = false)
    {
        var material = Material(color, metallic, emissive ? 0.25f : 0.6f);
        if (emissive)
        {
            material.EmissionEnabled = true;
            material.Emission = color;
            material.EmissionEnergyMultiplier = 0.75f;
        }
        var part = new MeshInstance3D
        {
            Mesh = mesh,
            Position = position,
            Rotation = rotation,
            MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        parent.AddChild(part);
        return part;
    }

    private void AnimateRoleDevice(float delta)
    {
        var duration = Mathf.Max(0.01f, OperatorRoles.Spec(Role).SkillDuration);
        var progress = Mathf.Clamp(_roleActionElapsed / duration, 0.0f, 1.0f);
        var rise = SmoothStep(Mathf.Min(1.0f, progress * 4.0f));
        var lower = SmoothStep(Mathf.Clamp((progress - 0.78f) / 0.22f, 0.0f, 1.0f));
        var lift = rise * (1.0f - lower);
        if (Role == OperatorRole.Medic)
        {
            _medicSprayer.Position = _medicSprayer.Position.Lerp(
                new Vector3(0.32f, -0.27f - lift * 0.025f, -0.72f),
                delta * 13.0f);
            _medicSprayer.Rotation = new Vector3(-0.18f + Mathf.Sin(progress * 22.0f) * 0.025f, -0.16f, -0.06f);
        }
        else if (Role is OperatorRole.Recon or OperatorRole.Scavenger)
        {
            _reconScanner.Position = _reconScanner.Position.Lerp(
                new Vector3(0.08f, -0.16f, -0.58f),
                delta * 10.0f);
            _reconScanner.Rotation = new Vector3(-0.08f, Mathf.Sin(progress * 8.0f) * 0.025f, 0.0f);
        }
    }

    private void SetRoleDeviceVisibility()
    {
        var actionVisible = _roleActionRemaining > 0.0f;
        _medicSprayer.Visible = actionVisible && Role == OperatorRole.Medic;
        _reconScanner.Visible = actionVisible && Role is OperatorRole.Recon or OperatorRole.Scavenger;
        _assaultInjector.Visible = Role == OperatorRole.Assault && _skillActiveRemaining > 0.0f;
    }

    public Vector3 GetAimPoint(float maximumDistance = 55.0f)
    {
        var authoritativeView = CaptureAuthoritativeViewTransform();
        var from = authoritativeView.Origin;
        var to = from - authoritativeView.Basis.Z * maximumDistance;
        return PhysicsRaycast.TryHit(GetWorld3D(), from, to, GetRid(), 1 | 2, out var hit)
            ? hit.Position
            : to;
    }

    public bool TakeCombatDamage(float amount, Vector3 hitPosition, Node? attacker = null)
    {
        if (attacker is EnemyOperator enemy && !enemy.HasClearBallisticPath(this, hitPosition))
        {
            return false;
        }
        return TakeDamage(amount, hitPosition, attacker);
    }

    public void RestoreHealth(float amount)
    {
        if (amount <= 0.0f || Health >= MaxHealth && !IsDead)
        {
            return;
        }
        // Healing while upright only — downed operators must go through TryReceiveRevive.
        if (IsDead)
        {
            return;
        }
        Health = Mathf.Clamp(Health + amount, 0.0f, MaxHealth);
        Hud?.SetStats(Health, Armor, Stamina, Ammo, ReserveAmmo, Grenades);
    }

    public bool TryReceiveRevive(float healAmount)
    {
        if (!IsDead || ReviveUsed || healAmount <= 0.0f)
        {
            return false;
        }
        ReviveUsed = true;
        Health = Mathf.Clamp(healAmount, 1.0f, MaxHealth);
        IsDead = false;
        UiLocked = false;
        _stance = PlayerStance.Crouched;
        Main?.OnLocalPlayerRevived();
        Hud?.SetStats(Health, Armor, Stamina, Ammo, ReserveAmmo, Grenades);
        return true;
    }

    public bool TryFinishDowned(Node? attacker = null)
    {
        if (!IsDead || !ReviveUsed)
        {
            return false;
        }
        Main?.OnLocalPlayerFinishedByHostile();
        return true;
    }

    internal void SetHealthForDiagnostics(float value)
    {
        Health = Mathf.Clamp(value, 1.0f, MaxHealth);
        IsDead = false;
        UiLocked = false;
    }

    internal void SetReviveUsedForDiagnostics(bool used) => ReviveUsed = used;
}
