using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private readonly List<SquadMate> _squadMates = new();
    private SquadNetwork _squadNetwork = null!;
    private SquadOrder _squadOrder = SquadOrder.Follow;
    private Vector3 _squadMovePoint;
    private MeshInstance3D? _squadMoveMarker;
    private bool _squadDeployed;
    private bool _localPlayerDowned;
    private float _squadHudTimer;
    private float _allDownTimer;
    private float _localPlayerDownedTimer;
    private int _remoteNetworkShotCount;
    private int _remoteNetworkAbilityCount;

    public int ActiveSquadCount => 1 + _squadMates.Count(mate => IsInstanceValid(mate));
    public int AiSquadCount => _squadMates.Count(mate => IsInstanceValid(mate) && !mate.IsHumanProxy);

    private void BuildSquadSystem()
    {
        EnsureSquadInputActions();
        _squadNetwork = new SquadNetwork
        {
            Name = "SquadNetwork",
            LocalPlayer = _player
        };
        AddChild(_squadNetwork);
        _squadNetwork.RemoteStateReceived += OnRemoteSquadState;
        _squadNetwork.RemotePeerLeft += OnRemoteSquadPeerLeft;
        _squadNetwork.RemoteAbilityReceived += OnRemoteSquadAbility;
        _squadNetwork.RemoteShotReceived += OnRemoteSquadShot;
        _squadNetwork.StatusChanged += status => _hud.SetSquadStatus(status);
        _hud.SquadDeploymentRequested += OnSquadDeploymentRequested;
        _hud.SquadOrderRequested += value => IssueSquadOrder((SquadOrder)value);

        var args = OS.GetCmdlineUserArgs();
        var lobbyCapture = Array.Exists(args, value => value == "--capture-squad-lobby");
        var networkHostCheck = Array.Exists(args, value => value == "--validate-network-host");
        var networkClientCheck = Array.Exists(args, value => value == "--validate-network-client");
        var diagnostic = Array.Exists(args, value =>
            value.StartsWith("--capture", StringComparison.Ordinal)
            || value.StartsWith("--validate", StringComparison.Ordinal)) && !lobbyCapture;
        if (diagnostic)
        {
            var mode = networkHostCheck
                ? SquadSessionMode.Host
                : networkClientCheck ? SquadSessionMode.Join : SquadSessionMode.Local;
            DeploySquad(OperatorRole.Assault, mode, "127.0.0.1");
            if (networkHostCheck || networkClientCheck)
            {
                ValidateNetworkSession(networkHostCheck ? "host" : "client");
            }
        }
        else
        {
            _player.UiLocked = true;
            _player.DisarmFireInput();
            _player.DisarmMovementInput();
            Input.MouseMode = Input.MouseModeEnum.Visible;
            _hud.ShowSquadLobby(GameLocalization.IsChinese(_languageSetting)
                ? "\u672c\u5730\u5c0f\u961f  //  3 \u540d AI \u961f\u53cb\u5df2\u5c31\u7eea"
                : "LOCAL SQUAD  //  THREE AI TEAMMATES READY");
        }
    }

    private static void EnsureSquadInputActions()
    {
        EnsureKeyAction("use_class_skill", Key.H);
        EnsureKeyAction("squad_follow", Key.F1);
        EnsureKeyAction("squad_hold", Key.F2);
        EnsureKeyAction("squad_move", Key.F3);
    }

    private static void EnsureKeyAction(string action, Key key)
    {
        if (!InputMap.HasAction(action))
        {
            InputMap.AddAction(action);
        }
        if (InputMap.ActionGetEvents(action).Count > 0)
        {
            return;
        }
        InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = key });
    }

    private void OnSquadDeploymentRequested(int role, int mode, string address)
    {
        DeploySquad((OperatorRole)role, (SquadSessionMode)mode, address);
    }

    private void DeploySquad(OperatorRole role, SquadSessionMode mode, string address)
    {
        if (_squadDeployed)
        {
            return;
        }
        _player.ConfigureRole(role);
        _player.UiLocked = false;
        _player.DisarmFireInput();
        _player.RestoreMovementInput();
        _squadDeployed = true;
        _localPlayerDowned = false;
        _squadOrder = SquadOrder.Follow;

        Error networkError = Error.Ok;
        switch (mode)
        {
            case SquadSessionMode.Host:
                networkError = _squadNetwork.Host();
                break;
            case SquadSessionMode.Join:
                networkError = _squadNetwork.Join(address);
                break;
            default:
                _squadNetwork.Close();
                _hud.SetSquadStatus("LOCAL SQUAD  //  1 HUMAN + 3 AI");
                break;
        }
        if (networkError != Error.Ok)
        {
            _hud.SetSquadStatus($"NETWORK UNAVAILABLE  //  AI SQUAD ACTIVE ({networkError})");
        }

        EnsureAiSquadFill();
        _hud.HideSquadLobby();
        _hud.SetSquadOrder(_squadOrder);
        RefreshSquadHud();
        Input.MouseMode = Input.MouseModeEnum.Captured;
        _hud.ShowLocalizedMessage(
            "squad_ready",
            "SQUAD READY  //  F1 FOLLOW  F2 HOLD  F3 MOVE  H SKILL",
            OperatorRoles.Spec(role).Accent);
    }

    private void EnsureAiSquadFill()
    {
        if (!_squadDeployed)
        {
            return;
        }
        for (var slot = 1; slot <= 3; slot++)
        {
            if (_squadMates.Any(mate => IsInstanceValid(mate) && mate.SquadSlot == slot))
            {
                continue;
            }
            SpawnSquadMate(slot, RoleForSlot(slot), false, 0);
        }
    }

    private OperatorRole RoleForSlot(int slot)
    {
        var complements = _player.Role switch
        {
            OperatorRole.Medic => new[] { OperatorRole.Assault, OperatorRole.Recon, OperatorRole.Medic },
            OperatorRole.Recon => new[] { OperatorRole.Assault, OperatorRole.Medic, OperatorRole.Recon },
            _ => new[] { OperatorRole.Medic, OperatorRole.Recon, OperatorRole.Assault }
        };
        return complements[Mathf.Clamp(slot - 1, 0, complements.Length - 1)];
    }

    private SquadMate SpawnSquadMate(int slot, OperatorRole role, bool human, long peerId)
    {
        var callsigns = new[] { "RAVEN", "ECHO", "VIPER", "NOMAD" };
        var formation = slot switch
        {
            1 => new Vector3(-2.25f, 0.05f, 3.2f),
            2 => new Vector3(2.25f, 0.05f, 3.2f),
            _ => new Vector3(0.0f, 0.05f, 5.1f)
        };
        var position = _player.GlobalPosition + _player.GlobalBasis.X * formation.X + _player.GlobalBasis.Z * formation.Z;
        var mate = new SquadMate
        {
            Name = human ? $"NetworkSquadmate_{peerId}" : $"AiSquadmate_{slot}",
            Position = position
        };
        mate.Configure(this, _player, slot, role, callsigns[slot], human, peerId);
        AddChild(mate);
        mate.SetOrder(_squadOrder, _squadMovePoint);
        _squadMates.Add(mate);
        return mate;
    }

    private void OnRemoteSquadState(long peerId, OperatorRole role, Vector3 position, Vector3 rotation, float health, bool down)
    {
        if (!_squadDeployed)
        {
            return;
        }
        var proxy = _squadMates.FirstOrDefault(mate => IsInstanceValid(mate) && mate.IsHumanProxy && mate.NetworkPeerId == peerId);
        if (proxy is null)
        {
            var occupiedSlots = _squadMates
                .Where(mate => IsInstanceValid(mate) && mate.IsHumanProxy)
                .Select(mate => mate.SquadSlot)
                .ToHashSet();
            var slot = Enumerable.Range(1, 3).FirstOrDefault(value => !occupiedSlots.Contains(value));
            if (slot == 0)
            {
                return;
            }
            var ai = _squadMates.FirstOrDefault(mate => IsInstanceValid(mate) && !mate.IsHumanProxy && mate.SquadSlot == slot);
            if (ai is not null)
            {
                _squadMates.Remove(ai);
                ai.QueueFree();
            }
            proxy = SpawnSquadMate(slot, role, true, peerId);
            _hud.ShowLocalizedMessage("player_joined", $"SQUADMATE CONNECTED  //  PEER {peerId}", OperatorRoles.Spec(role).Accent);
        }
        proxy.SetRemoteState(role, position, rotation, health, down);
    }

    private void OnRemoteSquadPeerLeft(long peerId)
    {
        var proxy = _squadMates.FirstOrDefault(mate => IsInstanceValid(mate) && mate.IsHumanProxy && mate.NetworkPeerId == peerId);
        if (proxy is not null)
        {
            _squadMates.Remove(proxy);
            proxy.QueueFree();
        }
        EnsureAiSquadFill();
        _hud.ShowLocalizedMessage("player_left", "SQUADMATE DISCONNECTED  //  AI TOOK CONTROL", new Color(0.95f, 0.68f, 0.26f));
    }

    private void OnRemoteSquadAbility(long peerId, OperatorRole role, Vector3 origin, Vector3 forward)
    {
        _remoteNetworkAbilityCount++;
        var proxy = _squadMates.FirstOrDefault(mate => IsInstanceValid(mate) && mate.IsHumanProxy && mate.NetworkPeerId == peerId);
        if (proxy is not null)
        {
            proxy.TriggerRemoteRoleAbility(origin, forward);
        }
    }

    public void OnLocalRoleAbility(OperatorRole role, Vector3 origin, Vector3 forward)
    {
        _squadNetwork?.BroadcastAbility(role, origin, forward);
    }

    public void OnLocalPlayerShot(Vector3 origin, Vector3 end, int enemyId, float damage)
    {
        _squadNetwork?.BroadcastShot(origin, end, enemyId, damage);
    }

    private void OnRemoteSquadShot(long peerId, Vector3 origin, Vector3 end, int enemyId, float damage)
    {
        _remoteNetworkShotCount++;
        var proxy = _squadMates.FirstOrDefault(mate => IsInstanceValid(mate) && mate.IsHumanProxy && mate.NetworkPeerId == peerId);
        proxy?.PlayRemoteShot(end);
        if (enemyId < 0 || damage <= 0.0f)
        {
            return;
        }
        var enemy = _enemies.FirstOrDefault(candidate => IsInstanceValid(candidate) && candidate.NetworkId == enemyId);
        if (enemy is not null && !enemy.IsDead)
        {
            enemy.TakeDamage(damage, end, proxy);
        }
    }

    private void UpdateSquad(float delta)
    {
        if (!_squadDeployed || _missionEnded)
        {
            return;
        }
        if (!_player.UiLocked && !_player.IsDead)
        {
            if (Input.IsActionJustPressed("squad_follow"))
            {
                IssueSquadOrder(SquadOrder.Follow);
            }
            else if (Input.IsActionJustPressed("squad_hold"))
            {
                IssueSquadOrder(SquadOrder.Hold);
            }
            else if (Input.IsActionJustPressed("squad_move"))
            {
                IssueSquadOrder(SquadOrder.Move);
            }
        }

        _squadHudTimer -= delta;
        if (_squadHudTimer <= 0.0f)
        {
            _squadHudTimer = 0.12f;
            RefreshSquadHud();
        }

        var everyoneDown = _player.IsDead && _squadMates
            .Where(IsInstanceValid)
            .All(mate => mate.IsDowned);
        _allDownTimer = everyoneDown ? _allDownTimer + delta : 0.0f;
        if (everyoneDown && _allDownTimer > 1.25f)
        {
            FailSquadMission();
        }
        if (_localPlayerDowned)
        {
            _localPlayerDownedTimer += delta;
            if (_localPlayerDownedTimer >= 15.0f
                && _squadMates.Any(mate => IsInstanceValid(mate) && !mate.IsDowned))
            {
                _player.RestoreHealth(32.0f);
            }
        }
    }

    private void IssueSquadOrder(SquadOrder order)
    {
        if (!_squadDeployed)
        {
            return;
        }
        _squadOrder = order;
        if (order == SquadOrder.Move)
        {
            _squadMovePoint = _player.GetAimPoint(65.0f);
            ShowSquadMoveMarker(_squadMovePoint);
        }
        else if (order == SquadOrder.Hold)
        {
            _squadMovePoint = _player.GlobalPosition;
        }
        foreach (var mate in _squadMates)
        {
            if (IsInstanceValid(mate))
            {
                mate.SetOrder(order, order == SquadOrder.Hold ? mate.GlobalPosition : _squadMovePoint);
            }
        }
        _hud.SetSquadOrder(order);
        var accent = order == SquadOrder.Move ? new Color(0.3f, 0.76f, 1.0f) : new Color(0.38f, 0.9f, 0.68f);
        _hud.ShowLocalizedMessage("squad_order", $"SQUAD ORDER  //  {OperatorRoles.Spec(_player.Role).Name} LEAD  //  {order.ToString().ToUpperInvariant()}", accent);
    }

    private void ShowSquadMoveMarker(Vector3 point)
    {
        if (IsInstanceValid(_squadMoveMarker))
        {
            _squadMoveMarker!.QueueFree();
        }
        _squadMoveMarker = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.55f, BottomRadius = 0.55f, Height = 0.035f, RadialSegments = 28 },
            Position = point + Vector3.Up * 0.05f,
            MaterialOverride = EffectMaterial(new Color(0.24f, 0.75f, 1.0f, 0.75f)),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        AddChild(_squadMoveMarker);
        var marker = _squadMoveMarker;
        var tween = CreateTween().SetLoops(7);
        tween.TweenProperty(marker, "scale", new Vector3(1.45f, 1.0f, 1.45f), 0.35f);
        tween.TweenProperty(marker, "scale", Vector3.One, 0.35f);
        var cleanup = CreateTween();
        cleanup.TweenInterval(5.0f);
        cleanup.TweenCallback(Callable.From(() =>
        {
            if (IsInstanceValid(marker))
            {
                marker.QueueFree();
            }
        }));
    }

    private void RefreshSquadHud()
    {
        if (!IsInstanceValid(_hud) || !_squadDeployed)
        {
            return;
        }
        var views = new List<SquadMemberView>
        {
            new(
                "RAVEN",
                _player.Role,
                _player.Health,
                _player.MaxHealth,
                true,
                _player.IsDead,
                _squadOrder,
                _player.SkillCooldownRemaining,
                _player.SkillCooldownDuration)
        };
        views.AddRange(_squadMates
            .Where(IsInstanceValid)
            .OrderBy(mate => mate.SquadSlot)
            .Select(mate => new SquadMemberView(
                mate.Callsign,
                mate.Role,
                mate.Health,
                mate.MaxHealth,
                mate.IsHumanProxy,
                mate.IsDowned,
                mate.Order,
                mate.SkillCooldownRemaining,
                mate.SkillCooldownDuration)));
        _hud.SetSquadRoster(views);
    }

    public EnemyOperator? FindNearestEnemy(Vector3 origin, float range)
    {
        EnemyOperator? nearest = null;
        var bestDistance = range * range;
        foreach (var enemy in _enemies)
        {
            if (!IsInstanceValid(enemy) || enemy.IsDead)
            {
                continue;
            }
            var distance = origin.DistanceSquaredTo(enemy.GlobalPosition);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                nearest = enemy;
            }
        }
        return nearest;
    }

    public bool CanSquadEngage(EnemyOperator enemy)
    {
        if (_missionDirector.IsDeploymentProtected())
        {
            return false;
        }
        return enemy.Alerted || _missionPhase is "CONTACT" or "COMBAT";
    }

    public ISquadCombatant? FindNearestFriendly(Vector3 origin)
    {
        ISquadCombatant? nearest = null;
        var bestDistance = float.PositiveInfinity;
        foreach (var friendly in FriendlyCombatants())
        {
            if (friendly.CombatDead || friendly is SquadMate { IsHumanProxy: true })
            {
                continue;
            }
            var distance = origin.DistanceSquaredTo(friendly.CombatNode.GlobalPosition);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                nearest = friendly;
            }
        }
        return nearest;
    }

    public ISquadCombatant? FindLowestFriendly(float healthRatio, bool includeDowned)
    {
        ISquadCombatant? lowest = null;
        var bestRatio = healthRatio;
        foreach (var friendly in FriendlyCombatants())
        {
            if (friendly.CombatDead && !includeDowned)
            {
                continue;
            }
            var ratio = friendly.CombatHealth / Mathf.Max(1.0f, friendly.CombatMaxHealth);
            if (friendly.CombatDead)
            {
                ratio = -1.0f;
            }
            if (ratio <= bestRatio)
            {
                bestRatio = ratio;
                lowest = friendly;
            }
        }
        return lowest;
    }

    private IEnumerable<ISquadCombatant> FriendlyCombatants()
    {
        if (IsInstanceValid(_player))
        {
            yield return _player;
        }
        foreach (var mate in _squadMates)
        {
            if (IsInstanceValid(mate))
            {
                yield return mate;
            }
        }
    }

    public void ApplyMedicSpray(ISquadCombatant source, Vector3 origin, Vector3 forward)
    {
        ISquadCombatant? target = null;
        var bestScore = float.PositiveInfinity;
        var sourceNode = source.CombatNode;
        var normalizedForward = forward.Normalized();
        foreach (var friendly in FriendlyCombatants())
        {
            var offset = friendly.CombatNode.GlobalPosition - origin;
            var distance = offset.Length();
            if (friendly == source)
            {
                var selfRatio = friendly.CombatHealth / Mathf.Max(1.0f, friendly.CombatMaxHealth);
                if (selfRatio < 0.96f)
                {
                    var selfScore = selfRatio * 4.0f + 2.0f;
                    if (selfScore < bestScore)
                    {
                        bestScore = selfScore;
                        target = friendly;
                    }
                }
                continue;
            }
            if (distance > 8.0f)
            {
                continue;
            }
            var alignment = distance <= 0.01f ? 1.0f : normalizedForward.Dot(offset / distance);
            if (alignment < 0.3f)
            {
                continue;
            }
            var ratio = friendly.CombatDead ? -1.0f : friendly.CombatHealth / Mathf.Max(1.0f, friendly.CombatMaxHealth);
            var score = ratio * 4.0f + distance * 0.08f - alignment;
            if (score < bestScore && (ratio < 0.99f || friendly.CombatDead))
            {
                bestScore = score;
                target = friendly;
            }
        }

        target ??= source;
        var targetPoint = target.HitPoint(HitRegion.Torso);
        var wasDown = target.CombatDead;
        target.RestoreHealth(wasDown ? 58.0f : 44.0f);
        if (target != source)
        {
            source.RestoreHealth(18.0f);
        }
        SpawnMedicSprayEffect(origin, targetPoint);
        _hud.ShowLocalizedMessage(
            wasDown ? "squad_revive" : "medic_spray",
            wasDown ? "MEDIC SPRAY  //  SQUADMATE REVIVED" : "MEDIC SPRAY  //  TRAUMA STABILIZED",
            OperatorRoles.Spec(OperatorRole.Medic).Accent);
    }

    public void PerformReconScan(ISquadCombatant source, Vector3 origin)
    {
        var revealed = 0;
        foreach (var enemy in _enemies)
        {
            if (!IsInstanceValid(enemy) || enemy.IsDead || enemy.GlobalPosition.DistanceTo(origin) > 72.0f)
            {
                continue;
            }
            enemy.SetScanned(10.0f);
            revealed++;
        }
        SpawnReconPulse(origin);
        _hud.ShowLocalizedMessage(
            "recon_scan",
            $"PULSE SCAN  //  {revealed:00} HOSTILES REVEALED",
            OperatorRoles.Spec(OperatorRole.Recon).Accent);
    }

    public void SpawnRoleActivationPulse(Vector3 position, Color color, float radius)
    {
        var pulse = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.25f, Height = 0.5f, RadialSegments = 12, Rings = 6 },
            Position = position,
            MaterialOverride = EffectMaterial(new Color(color.R, color.G, color.B, 0.55f)),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        AddChild(pulse);
        var tween = CreateTween().SetParallel(true);
        tween.TweenProperty(pulse, "scale", Vector3.One * radius, 0.5f).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(pulse, "transparency", 1.0f, 0.55f);
        tween.Chain().TweenCallback(Callable.From(pulse.QueueFree));
    }

    private void SpawnMedicSprayEffect(Vector3 origin, Vector3 target)
    {
        var root = new Node3D { Name = "MedicSprayEffect" };
        AddChild(root);
        var sprayDirection = origin.DirectionTo(target);
        var sprayStart = origin + sprayDirection * 0.08f;
        var mistMaterial = new StandardMaterial3D
        {
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = new Color(0.28f, 0.95f, 0.58f, 0.26f),
            EmissionEnabled = true,
            Emission = new Color(0.18f, 0.65f, 0.38f),
            EmissionEnergyMultiplier = 0.55f
        };
        var jetLength = sprayStart.DistanceTo(target);
        var jet = new MeshInstance3D
        {
            Mesh = new CylinderMesh
            {
                TopRadius = 0.008f,
                BottomRadius = 0.016f,
                Height = jetLength,
                RadialSegments = 8
            },
            Position = sprayStart.Lerp(target, 0.5f),
            Quaternion = new Quaternion(Vector3.Up, sprayDirection),
            MaterialOverride = mistMaterial,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        root.AddChild(jet);
        var jetTween = CreateTween();
        jetTween.TweenInterval(0.14f);
        jetTween.TweenProperty(jet, "transparency", 1.0f, 0.3f);
        for (var i = 0; i < 14; i++)
        {
            var t = (i + 1) / 14.0f;
            var mist = new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = 0.006f + t * 0.012f, Height = 0.016f + t * 0.024f, RadialSegments = 7, Rings = 4 },
                Position = sprayStart.Lerp(target, t * 0.08f),
                MaterialOverride = mistMaterial,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            };
            root.AddChild(mist);
            var scatterRadius = 0.025f + t * 0.16f;
            var scatter = new Vector3(
                _rng.RandfRange(-scatterRadius, scatterRadius),
                _rng.RandfRange(-scatterRadius * 0.55f, scatterRadius),
                _rng.RandfRange(-scatterRadius, scatterRadius));
            var tween = CreateTween().SetParallel(true);
            tween.TweenProperty(mist, "position", sprayStart.Lerp(target, t) + scatter, 0.34f + t * 0.18f).SetDelay(i * 0.016f);
            tween.TweenProperty(mist, "transparency", 1.0f, 0.38f).SetDelay(0.2f + i * 0.016f);
        }
        var cleanup = CreateTween();
        cleanup.TweenInterval(1.2f);
        cleanup.TweenCallback(Callable.From(root.QueueFree));
    }

    private void SpawnReconPulse(Vector3 origin)
    {
        for (var i = 0; i < 3; i++)
        {
            var ring = new MeshInstance3D
            {
                Mesh = new TorusMesh { InnerRadius = 0.985f, OuterRadius = 1.0f, Rings = 48, RingSegments = 5 },
                Position = origin + Vector3.Up * (0.04f + i * 0.09f),
                MaterialOverride = EffectMaterial(new Color(0.24f, 0.68f, 1.0f, 0.28f)),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            };
            AddChild(ring);
            var tween = CreateTween().SetParallel(true);
            tween.TweenProperty(ring, "scale", Vector3.One * 28.0f, 1.0f).SetDelay(i * 0.12f)
                .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
            tween.TweenProperty(ring, "transparency", 1.0f, 1.05f).SetDelay(i * 0.12f);
            tween.Chain().TweenCallback(Callable.From(ring.QueueFree));
        }
    }

    private static StandardMaterial3D EffectMaterial(Color color)
    {
        return new StandardMaterial3D
        {
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = color,
            EmissionEnabled = true,
            Emission = new Color(color.R, color.G, color.B),
            EmissionEnergyMultiplier = 2.5f
        };
    }

    private void DamageSquadFromExplosion(Vector3 position, float radius, float maxDamage, Node? source)
    {
        foreach (var mate in _squadMates)
        {
            if (!IsInstanceValid(mate) || mate.IsDowned)
            {
                continue;
            }
            var distance = mate.GlobalPosition.DistanceTo(position);
            if (distance < radius)
            {
                mate.TakeCombatDamage(maxDamage * 0.72f * (1.0f - distance / radius), position, source);
            }
        }
    }

    public void OnSquadMateDowned(SquadMate mate)
    {
        _hud.ShowLocalizedMessage("squadmate_down", $"{mate.Callsign} DOWN  //  MEDIC SUPPORT REQUESTED", new Color(1.0f, 0.34f, 0.22f));
    }

    private bool HandleLocalPlayerDowned()
    {
        if (!_squadDeployed || !_squadMates.Any(mate => IsInstanceValid(mate) && !mate.IsDowned))
        {
            return false;
        }
        _localPlayerDowned = true;
        _localPlayerDownedTimer = 0.0f;
        _player.UiLocked = true;
        _hud.ShowLocalizedMessage("player_downed", "YOU ARE DOWN  //  AI MEDIC MOVING TO REVIVE", new Color(1.0f, 0.34f, 0.2f));
        return true;
    }

    public void OnLocalPlayerRevived()
    {
        _localPlayerDowned = false;
        _localPlayerDownedTimer = 0.0f;
        _player.UiLocked = false;
        _player.DisarmFireInput();
        _player.RestoreMovementInput();
        Input.MouseMode = Input.MouseModeEnum.Captured;
        _hud.ShowLocalizedMessage("player_revived", "REVIVED  //  BACK IN THE FIGHT", OperatorRoles.Spec(OperatorRole.Medic).Accent);
    }

    private void FailSquadMission()
    {
        if (_missionEnded)
        {
            return;
        }
        _missionEnded = true;
        _missionDirector.CompleteMission(false, _kills, _headshots, _shotsFired, _shotsHit);
        _hud.ShowResult(false);
    }

    private async void ValidateSquadFlow()
    {
        await ToSignal(GetTree().CreateTimer(0.45f), SceneTreeTimer.SignalName.Timeout);
        var defaultFollow = _squadMates.All(mate => mate.Order == SquadOrder.Follow);
        var cooldownMate = _squadMates.First(mate => !mate.IsHumanProxy);
        cooldownMate.SetSkillCooldownForDiagnostics(0.0f);
        var firstAiSkill = cooldownMate.TriggerRoleAbility(_player.GlobalPosition);
        var repeatedAiSkillBlocked = !cooldownMate.TriggerRoleAbility(_player.GlobalPosition);
        var aiCooldownEnforced = firstAiSkill
            && repeatedAiSkillBlocked
            && cooldownMate.SkillCooldownRemaining > OperatorRoles.Spec(cooldownMate.Role).SkillCooldown;
        _player.ConfigureRole(OperatorRole.Medic);
        _player.SetHealthForDiagnostics(72.0f);
        var healthBefore = _player.Health;
        _player.ActivateRoleAbility(false);
        await ToSignal(GetTree().CreateTimer(0.8f), SceneTreeTimer.SignalName.Timeout);
        var medicSelf = _player.Health > healthBefore;

        _player.ConfigureRole(OperatorRole.Recon);
        _player.ActivateRoleAbility(false);
        await ToSignal(GetTree().CreateTimer(0.9f), SceneTreeTimer.SignalName.Timeout);
        var scanned = _enemies.Count(enemy => enemy.IsScanned);

        _player.ConfigureRole(OperatorRole.Assault);
        _player.ActivateRoleAbility(false);
        var assaultSpeed = _player.RoleMovementMultiplier;
        var assaultFire = _player.RoleFireIntervalMultiplier;
        IssueSquadOrder(SquadOrder.Hold);
        var hold = _squadMates.Where(mate => !mate.IsHumanProxy).All(mate => mate.Order == SquadOrder.Hold);
        IssueSquadOrder(SquadOrder.Move);
        var move = _squadMates.Where(mate => !mate.IsHumanProxy).All(mate => mate.Order == SquadOrder.Move);
        IssueSquadOrder(SquadOrder.Follow);
        var follow = _squadMates.Where(mate => !mate.IsHumanProxy).All(mate => mate.Order == SquadOrder.Follow);
        var follower = _squadMates.First(mate => !mate.IsHumanProxy);
        follower.GlobalPosition = _player.GlobalPosition + new Vector3(12.0f, 0.1f, 0.0f);
        var followDistanceBefore = follower.GlobalPosition.DistanceTo(_player.GlobalPosition);
        await ToSignal(GetTree().CreateTimer(0.65f), SceneTreeTimer.SignalName.Timeout);
        var followMotion = follower.GlobalPosition.DistanceTo(_player.GlobalPosition) < followDistanceBefore - 0.5f;

        GD.Print($"SQUAD_CHECK members={ActiveSquadCount} ai={AiSquadCount} default_follow={defaultFollow} follow_motion={followMotion} ai_cooldown={aiCooldownEnforced} ai_cooldown_seconds={cooldownMate.SkillCooldownDuration:0} medic_self={medicSelf} recon={scanned} assault_speed={assaultSpeed:0.00} assault_fire={assaultFire:0.00} orders={hold && move && follow} hud={!_hud.IsSquadLobbyVisible} keys={(long)Key.H}/{(long)Key.F1}/{(long)Key.F2}/{(long)Key.F3}");
        GetTree().Quit();
    }

    private async void CaptureSquadFrame()
    {
        await ToSignal(GetTree().CreateTimer(0.65f), SceneTreeTimer.SignalName.Timeout);
        var stagedPositions = new[]
        {
            _player.GlobalPosition + new Vector3(-2.4f, 0.0f, -6.2f),
            _player.GlobalPosition + new Vector3(0.0f, 0.0f, -7.2f),
            _player.GlobalPosition + new Vector3(2.4f, 0.0f, -6.2f)
        };
        var staged = _squadMates.OrderBy(mate => mate.SquadSlot).ToArray();
        for (var i = 0; i < staged.Length && i < stagedPositions.Length; i++)
        {
            staged[i].GlobalPosition = stagedPositions[i];
            staged[i].SetOrder(SquadOrder.Hold, stagedPositions[i]);
            staged[i].LookAt(_player.GlobalPosition + Vector3.Up, Vector3.Up);
        }
        _player.ConfigureRole(OperatorRole.Medic);
        if (staged.Length > 1)
        {
            staged[0].SetSkillCooldownForDiagnostics(5.0f);
            staged[1].TakeCombatDamage(42.0f, staged[1].HitPoint(HitRegion.Torso), this);
        }
        _player.ActivateRoleAbility(false);
        await ToSignal(GetTree().CreateTimer(0.68f), SceneTreeTimer.SignalName.Timeout);
        var image = GetViewport().GetTexture().GetImage();
        image.SavePng("user://squad_validation.png");
        GD.Print("CAPTURE_SQUAD user://squad_validation.png");
        GetTree().Quit();
    }

    private async void CaptureSquadLobbyFrame()
    {
        await ToSignal(GetTree().CreateTimer(0.6f), SceneTreeTimer.SignalName.Timeout);
        var image = GetViewport().GetTexture().GetImage();
        image.SavePng("user://squad_lobby_validation.png");
        GD.Print("CAPTURE_SQUAD_LOBBY user://squad_lobby_validation.png");
        GetTree().Quit();
    }

    private async void ValidateNetworkSession(string mode)
    {
        await ToSignal(GetTree().CreateTimer(2.2f), SceneTreeTimer.SignalName.Timeout);
        _squadNetwork.BroadcastShot(_player.GlobalPosition + Vector3.Up, _player.GlobalPosition - Vector3.Forward * 4.0f, -1, 0.0f);
        _squadNetwork.BroadcastAbility(OperatorRole.Assault, _player.GlobalPosition + Vector3.Up, -Vector3.Forward);
        if (mode == "client")
        {
            _squadNetwork.BroadcastAbility(OperatorRole.Assault, _player.GlobalPosition + Vector3.Up, -Vector3.Forward);
        }
        await ToSignal(GetTree().CreateTimer(mode == "host" ? 1.5f : 1.7f), SceneTreeTimer.SignalName.Timeout);
        var remoteHumans = _squadMates.Count(mate => IsInstanceValid(mate) && mate.IsHumanProxy);
        var cooldownGate = _remoteNetworkAbilityCount == 1;
        GD.Print($"NETWORK_CHECK mode={mode} online={_squadNetwork.IsOnline} peers={_squadNetwork.ConnectedPeerCount} remote_humans={remoteHumans} remote_shots={_remoteNetworkShotCount} remote_abilities={_remoteNetworkAbilityCount} cooldown_gate={cooldownGate} members={ActiveSquadCount} ai={AiSquadCount}");
        if (mode == "host")
        {
            await ToSignal(GetTree().CreateTimer(2.5f), SceneTreeTimer.SignalName.Timeout);
        }
        GetTree().Quit(cooldownGate ? 0 : 2);
    }
}
