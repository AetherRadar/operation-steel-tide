using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public partial class EnemyOperator
{
    public const int WorldBossTeamId = -700;
    public const float WorldBossMaxHealth = 900.0f;

    public bool IsWorldBoss { get; private set; }
    public float MaxHealth => IsWorldBoss
        ? WorldBossMaxHealth
        : IsHumanProxy ? OperatorRoles.Spec(NetworkRole).MaxHealth : 100.0f;
    public float HealthRatio => Mathf.Clamp(_health / MaxHealth, 0.0f, 1.0f);
    public int WorldBossPhase => HealthRatio > 0.66f ? 1 : HealthRatio > 0.3f ? 2 : 3;
    public int WorldBossPatrolRouteCount => _worldBossPatrolRoute.Length;
    public int WorldBossPatrolRouteIndex => _worldBossPatrolIndex;
    public Vector2 WorldBossPatrolSpan { get; private set; }
    public Vector3 WorldBossPatrolTarget => _patrolTarget;
    public int WorldBossPulseCount { get; private set; }
    public bool IsWorldBossPulseCharging => IsWorldBoss && _worldBossPulseCharge > 0.0f;

    private Vector3[] _worldBossPatrolRoute = Array.Empty<Vector3>();
    private int _worldBossPatrolIndex = -1;
    private float _worldBossPatrolProgressTimer;
    private Vector3 _worldBossPatrolProgressOrigin;
    private float _worldBossPulseCooldown = 7.0f;
    private float _worldBossPulseCharge;
    private int _worldBossVisualPhase = 1;
    private MeshInstance3D? _worldBossChargeRing;
    private Label3D? _worldBossLabel;

    private float WorldBossMoveMultiplier => WorldBossPhase switch
    {
        2 => 1.12f,
        3 => 1.28f,
        _ => 1.0f
    };

    private float WorldBossFireCadenceMultiplier => WorldBossPhase switch
    {
        2 => 0.74f,
        3 => 0.52f,
        _ => 0.9f
    };

    private Color CurrentTracerColor => IsWorldBoss
        ? new Color(0.18f, 1.0f, 0.78f)
        : new Color(1.0f, 0.4f, 0.15f);

    public void ConfigureWorldBoss(IReadOnlyList<Vector3> patrolRoute)
    {
        IsWorldBoss = true;
        TeamId = WorldBossTeamId;
        DetectionRange = 240.0f;
        SentryMode = false;
        _health = WorldBossMaxHealth;
        _configuredLoadout = WeaponCatalog.Build(WeaponPlatform.AXMC, 3);
        _worldBossPatrolRoute = new Vector3[patrolRoute.Count];
        for (var index = 0; index < patrolRoute.Count; index++)
        {
            _worldBossPatrolRoute[index] = patrolRoute[index];
        }

        if (_worldBossPatrolRoute.Length > 0)
        {
            var minX = _worldBossPatrolRoute[0].X;
            var maxX = minX;
            var minZ = _worldBossPatrolRoute[0].Z;
            var maxZ = minZ;
            foreach (var point in _worldBossPatrolRoute)
            {
                minX = Mathf.Min(minX, point.X);
                maxX = Mathf.Max(maxX, point.X);
                minZ = Mathf.Min(minZ, point.Z);
                maxZ = Mathf.Max(maxZ, point.Z);
            }
            WorldBossPatrolSpan = new Vector2(maxX - minX, maxZ - minZ);
        }
    }

    private void BuildWorldBossLootInventory()
    {
        Loot.Clear();
        CarriedWeapon = (_configuredLoadout ?? WeaponCatalog.Build(WeaponPlatform.AXMC, 3)).Clone();
        HasFireablePrimary = true;
        EquippedHelmet = EquipmentCatalog.Create("helmet_heavy");
        EquippedBodyArmor = EquipmentCatalog.Create("armor_heavy");
        EquippedBackpack = EquipmentCatalog.Create("pack_heavy");

        Loot.Add(new LootItem
        {
            Kind = LootItemKind.Weapon,
            Weapon = CarriedWeapon.Clone(),
            Grade = LootGrade.Legendary
        });
        Loot.Add(new LootItem
        {
            Kind = LootItemKind.Attachment,
            AttachmentId = "optic_7x",
            Grade = LootGrade.Legendary
        });
        Loot.Add(new LootItem
        {
            Kind = LootItemKind.Ammunition,
            AmmoCaliber = AmmoCaliber.Magnum338,
            Quantity = 30,
            Grade = LootGrade.Legendary
        });
        Loot.Add(new LootItem
        {
            Kind = LootItemKind.Equipment,
            Equipment = EquipmentCatalog.Create("armor_heavy"),
            Grade = LootGrade.Legendary
        });
        Loot.Add(new LootItem
        {
            Kind = LootItemKind.KnifeSkin,
            KnifeSkinId = "knife_tidehunter",
            Grade = LootGrade.Legendary
        });
        Loot.Add(new LootItem
        {
            Kind = LootItemKind.Valuable,
            ValuableKind = ValuableItemKind.TideHunterTransponder,
            Grade = LootGrade.Legendary
        });
    }

    private void BuildWorldBossVisuals()
    {
        _bodyRoot.Scale = new Vector3(1.16f, 1.13f, 1.16f);
        _mainMaterial.AlbedoColor = new Color(0.035f, 0.2f, 0.19f);
        var armor = Material(new Color(0.025f, 0.065f, 0.065f), 0.72f, 0.32f);
        var tide = Material(new Color(0.08f, 0.72f, 0.62f), 0.28f, 0.2f);
        tide.EmissionEnabled = true;
        tide.Emission = new Color(0.03f, 0.82f, 0.68f);
        tide.EmissionEnergyMultiplier = 3.4f;

        Part(Box(new Vector3(0.76f, 0.17f, 0.34f)), new Vector3(0.0f, 1.46f, 0.02f), armor);
        Part(Box(new Vector3(0.18f, 0.2f, 0.24f)), new Vector3(-0.43f, 1.43f, 0.0f), armor);
        Part(Box(new Vector3(0.18f, 0.2f, 0.24f)), new Vector3(0.43f, 1.43f, 0.0f), armor);
        Part(Box(new Vector3(0.12f, 0.08f, 0.2f)), new Vector3(-0.43f, 1.56f, -0.02f), tide);
        Part(Box(new Vector3(0.12f, 0.08f, 0.2f)), new Vector3(0.43f, 1.56f, -0.02f), tide);
        Part(Box(new Vector3(0.3f, 0.24f, 0.045f)), new Vector3(0.0f, 1.22f, -0.205f), tide);
        Part(Cylinder(0.105f, 0.56f), new Vector3(-0.2f, 1.25f, 0.32f), armor);
        Part(Cylinder(0.105f, 0.56f), new Vector3(0.2f, 1.25f, 0.32f), armor);
        Part(Cylinder(0.115f, 0.045f), new Vector3(-0.2f, 1.28f, 0.32f), tide);
        Part(Cylinder(0.115f, 0.045f), new Vector3(0.2f, 1.28f, 0.32f), tide);
        Part(Box(new Vector3(0.42f, 0.06f, 0.055f)), new Vector3(0.0f, 1.76f, -0.2f), tide);
        Part(Box(new Vector3(0.09f, 0.22f, 0.18f)), new Vector3(0.0f, 2.03f, 0.02f), armor, new Vector3(0.0f, 0.0f, 0.16f));
        Part(Cylinder(0.025f, 0.52f), new Vector3(0.14f, 1.85f, 0.23f), armor, new Vector3(0.12f, 0.0f, -0.08f));
        Part(Cylinder(0.045f, 0.09f), new Vector3(0.11f, 2.1f, 0.21f), tide);

        _worldBossChargeRing = new MeshInstance3D
        {
            Name = "TidePulseChargeRing",
            Mesh = new TorusMesh { InnerRadius = 0.62f, OuterRadius = 0.69f, Rings = 32, RingSegments = 10 },
            Position = new Vector3(0.0f, 0.12f, 0.0f),
            MaterialOverride = tide,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Visible = false
        };
        AddChild(_worldBossChargeRing);

        _worldBossLabel = new Label3D
        {
            Name = "TideHunterIdentity",
            Text = "TIDE HUNTER  //  ROGUE",
            Position = new Vector3(0.0f, 2.65f, 0.0f),
            FontSize = 28,
            OutlineSize = 10,
            Modulate = new Color(0.28f, 1.0f, 0.84f),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true
        };
        AddChild(_worldBossLabel);

        _shotAudio.Stream = SoundLab.Gunshot();
        _shotAudio.VolumeDb = -1.5f;
        _shotAudio.MaxDistance = 185.0f;
        RefreshWorldBossPhaseVisuals(notify: false);
    }

    private void UpdateWorldBossState(float delta)
    {
        if (!IsWorldBoss || IsDead)
        {
            return;
        }

        if (IsInstanceValid(_worldBossChargeRing))
        {
            _worldBossChargeRing!.RotateY(delta * 2.4f);
        }
        if (_worldBossPulseCharge > 0.0f)
        {
            _worldBossPulseCharge = Mathf.Max(0.0f, _worldBossPulseCharge - delta);
            if (IsInstanceValid(_worldBossChargeRing))
            {
                _worldBossChargeRing!.Visible = true;
                var scale = 1.0f + (1.2f - _worldBossPulseCharge) * 2.2f;
                _worldBossChargeRing.Scale = Vector3.One * scale;
            }
            if (_worldBossPulseCharge <= 0.0f)
            {
                FireWorldBossPulse();
            }
            return;
        }

        _worldBossPulseCooldown = Mathf.Max(0.0f, _worldBossPulseCooldown - delta);
        if (WorldBossPhase < 2 || _worldBossPulseCooldown > 0.0f || EngageTargetNode is not Node3D target)
        {
            return;
        }
        if (GlobalPosition.DistanceTo(target.GlobalPosition) > 18.0f)
        {
            return;
        }

        _worldBossPulseCharge = 1.2f;
        if (IsInstanceValid(_worldBossLabel))
        {
            _worldBossLabel!.Text = "TIDE HUNTER  //  PULSE CHARGING";
        }
    }

    private void FireWorldBossPulse()
    {
        if (IsInstanceValid(_worldBossChargeRing))
        {
            _worldBossChargeRing!.Visible = false;
            _worldBossChargeRing.Scale = Vector3.One;
        }
        WorldBossPulseCount++;
        var radius = WorldBossPhase >= 3 ? 15.5f : 13.0f;
        var damage = WorldBossPhase >= 3 ? 54.0f : 42.0f;
        Main?.TriggerWorldBossPulse(this, radius, damage);
        _worldBossPulseCooldown = WorldBossPhase >= 3 ? 6.0f : 9.5f;
        RefreshWorldBossPhaseVisuals(notify: false);
    }

    private void UpdateWorldBossPatrol(float delta)
    {
        if (_worldBossPatrolRoute.Length == 0)
        {
            var stopped = Velocity;
            stopped.X = Mathf.MoveToward(stopped.X, 0.0f, delta * 8.0f);
            stopped.Z = Mathf.MoveToward(stopped.Z, 0.0f, delta * 8.0f);
            Velocity = stopped;
            return;
        }

        var flatTarget = new Vector3(_patrolTarget.X, GlobalPosition.Y, _patrolTarget.Z);
        var distance = GlobalPosition.DistanceTo(flatTarget);
        _worldBossPatrolProgressTimer += delta;
        if (distance < 2.4f || _patrolTimer <= 0.0f)
        {
            SelectNextWorldBossPatrolPoint();
            flatTarget = new Vector3(_patrolTarget.X, GlobalPosition.Y, _patrolTarget.Z);
        }
        else if (_worldBossPatrolProgressTimer >= 2.6f)
        {
            if (GlobalPosition.DistanceTo(_worldBossPatrolProgressOrigin) < 0.48f)
            {
                SelectNextWorldBossPatrolPoint();
                flatTarget = new Vector3(_patrolTarget.X, GlobalPosition.Y, _patrolTarget.Z);
            }
            _worldBossPatrolProgressOrigin = GlobalPosition;
            _worldBossPatrolProgressTimer = 0.0f;
        }

        var direction = GlobalPosition.DirectionTo(flatTarget);
        direction.Y = 0.0f;
        if (direction.LengthSquared() > 0.01f)
        {
            direction = ApplyPursuitObstacleAvoidance(direction.Normalized());
            LookAt(GlobalPosition + direction, Vector3.Up);
        }
        var speed = 3.4f * WorldBossMoveMultiplier;
        var velocity = Velocity;
        velocity.X = Mathf.MoveToward(velocity.X, direction.X * speed, delta * 10.0f);
        velocity.Z = Mathf.MoveToward(velocity.Z, direction.Z * speed, delta * 10.0f);
        Velocity = velocity;
    }

    private void SelectNextWorldBossPatrolPoint()
    {
        if (_worldBossPatrolRoute.Length == 0)
        {
            return;
        }
        _worldBossPatrolIndex = (_worldBossPatrolIndex + 1) % _worldBossPatrolRoute.Length;
        _patrolTarget = _worldBossPatrolRoute[_worldBossPatrolIndex];
        _patrolTimer = 48.0f;
        _worldBossPatrolProgressOrigin = GlobalPosition;
        _worldBossPatrolProgressTimer = 0.0f;
    }

    private void OnWorldBossDamaged()
    {
        if (!IsWorldBoss)
        {
            return;
        }
        var phase = WorldBossPhase;
        if (phase == _worldBossVisualPhase)
        {
            return;
        }
        _worldBossVisualPhase = phase;
        _worldBossPulseCooldown = Mathf.Min(_worldBossPulseCooldown, phase >= 3 ? 2.8f : 4.5f);
        RefreshWorldBossPhaseVisuals(notify: true);
    }

    private void RefreshWorldBossPhaseVisuals(bool notify)
    {
        if (!IsWorldBoss)
        {
            return;
        }
        var phase = WorldBossPhase;
        var color = phase switch
        {
            2 => new Color(0.04f, 0.36f, 0.3f),
            3 => new Color(0.3f, 0.075f, 0.06f),
            _ => new Color(0.035f, 0.2f, 0.19f)
        };
        if (IsInstanceValid(_mainMaterial))
        {
            _mainMaterial.AlbedoColor = color;
        }
        SetAuthoredThreatColor(phase >= 3
            ? new Color(1.0f, 0.24f, 0.12f)
            : new Color(0.12f, 0.94f, 0.76f));
        if (IsInstanceValid(_worldBossLabel))
        {
            _worldBossLabel!.Text = phase switch
            {
                2 => "TIDE HUNTER  //  SURGE",
                3 => "TIDE HUNTER  //  RIPTIDE",
                _ => "TIDE HUNTER  //  HUNT"
            };
            _worldBossLabel.Modulate = phase >= 3
                ? new Color(1.0f, 0.34f, 0.22f)
                : new Color(0.28f, 1.0f, 0.84f);
        }
        if (notify)
        {
            Main?.NotifyWorldBossPhaseChanged(phase);
        }
    }

    public void SetWorldBossHealthRatioForDiagnostics(float ratio)
    {
        if (!IsWorldBoss || IsDead)
        {
            return;
        }
        _health = Mathf.Clamp(ratio, 0.01f, 1.0f) * WorldBossMaxHealth;
        OnWorldBossDamaged();
    }

    public void AdvanceWorldBossPatrolForDiagnostics() => SelectNextWorldBossPatrolPoint();

    public void ForceWorldBossPulseForDiagnostics()
    {
        if (!IsWorldBoss || IsDead)
        {
            return;
        }
        _worldBossPulseCharge = 0.0f;
        FireWorldBossPulse();
    }
}
