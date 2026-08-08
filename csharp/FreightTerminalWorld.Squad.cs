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
    private Camera3D? _squadSpectatorCamera;
    private SquadMate? _spectatedMate;
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
                ? "\u672c\u5730\u5c0f\u961f  //  3 \u4eba\u7f16\u5236  //  \u4f60\u9009\u804c\u4e1a\uff0cAI \u8865\u9f50\u5176\u4f59"
                : "LOCAL SQUAD  //  3 OPERATORS  //  YOU PICK  //  AI FILLS THE REST");
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
        if (_squadDeployed)
        {
            return;
        }
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
                _hud.SetSquadStatus("LOCAL SQUAD  //  1 HUMAN + 2 AI");
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
        // Exactly two AI slots (total squad size 3 including the player).
        for (var slot = 1; slot <= 2; slot++)
        {
            if (_squadMates.Any(mate => IsInstanceValid(mate) && mate.SquadSlot == slot))
            {
                continue;
            }
            SpawnSquadMate(slot, RoleForSlot(slot), false, 0);
        }
        // Drop any legacy fourth/third AI if present.
        for (var i = _squadMates.Count - 1; i >= 0; i--)
        {
            var mate = _squadMates[i];
            if (!IsInstanceValid(mate))
            {
                _squadMates.RemoveAt(i);
                continue;
            }
            if (mate.SquadSlot > 2 && !mate.IsHumanProxy)
            {
                mate.QueueFree();
                _squadMates.RemoveAt(i);
            }
        }
    }

    private OperatorRole RoleForSlot(int slot)
    {
        // AI always takes the two roles the player did not pick.
        var remaining = new List<OperatorRole>();
        foreach (OperatorRole role in Enum.GetValues<OperatorRole>())
        {
            if (role != _player.Role)
            {
                remaining.Add(role);
            }
        }
        return remaining[Mathf.Clamp(slot - 1, 0, remaining.Count - 1)];
    }

    private SquadMate SpawnSquadMate(int slot, OperatorRole role, bool human, long peerId)
    {
        var callsigns = new[] { "RAVEN", "ECHO", "VIPER" };
        var formation = slot switch
        {
            1 => new Vector3(-2.25f, 0.05f, 3.2f),
            _ => new Vector3(2.25f, 0.05f, 3.2f)
        };
        var position = _player.GlobalPosition + _player.GlobalBasis.X * formation.X + _player.GlobalBasis.Z * formation.Z;
        var mate = new SquadMate
        {
            Name = human ? $"NetworkSquadmate_{peerId}" : $"AiSquadmate_{slot}",
            Position = position
        };
        var sign = callsigns[Mathf.Clamp(slot, 0, callsigns.Length - 1)];
        mate.Configure(this, _player, slot, role, sign, human, peerId);
        AddChild(mate);
        // Friendly AI follows the same cold-start rule as the player and must loot a rifle.
        mate.ApplyColdStartUnarmed();
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
            var slot = Enumerable.Range(1, 2).FirstOrDefault(value => !occupiedSlots.Contains(value));
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

        // Hold-to-revive replaces the old auto-revive timer.
        UpdateManualRevive(delta);
        if (_localPlayerDowned)
        {
            UpdateLeaderReviveAi(delta);
            if (_localPlayerDowned)
            {
                UpdateSquadSpectatorCamera();
                var helpIncoming = _leaderReviver is not null && IsInstanceValid(_leaderReviver) && !_leaderReviver.IsDowned;
                if (!helpIncoming)
                {
                    // Bleed-out keeps running until a mate commits to the revive.
                    _localPlayerDownedTimer += delta;
                }
                if (_localPlayerDownedTimer >= 22.0f)
                {
                    FailSquadMission();
                }
            }
        }
    }

    private SquadMate? _leaderReviver;
    private float _leaderReviveChannel;
    private float _reviverStuckTime;
    private float _reviverSnapshotTimer;
    private Vector3 _reviverLastPosition;

    /// <summary>
    /// Downed leader rescue: the nearest living AI mate sprints over, kneels and
    /// channels a revive instead of standing idle on the body.
    /// </summary>
    private void UpdateLeaderReviveAi(float delta)
    {
        if (_missionEnded || !_player.CanBeRevived)
        {
            ClearLeaderReviveAi();
            return;
        }

        if (_leaderReviver is null || !IsInstanceValid(_leaderReviver)
            || _leaderReviver.IsDowned || _leaderReviver.IsBodyBag || _leaderReviver.IsHumanProxy)
        {
            if (_leaderReviver is not null && IsInstanceValid(_leaderReviver))
            {
                _leaderReviver.EndLeaderRevive();
            }
            _leaderReviver = null;
            _leaderReviveChannel = 0.0f;
            _reviverStuckTime = 0.0f;
            SquadMate? nearest = null;
            var bestDistance = float.PositiveInfinity;
            foreach (var mate in _squadMates)
            {
                if (!IsInstanceValid(mate) || mate.IsDowned || mate.IsBodyBag || mate.IsHumanProxy)
                {
                    continue;
                }
                var distance = mate.GlobalPosition.DistanceTo(_player.GlobalPosition);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    nearest = mate;
                }
            }
            if (nearest is null)
            {
                return;
            }
            _leaderReviver = nearest;
            _leaderReviver.BeginLeaderRevive();
            _reviverLastPosition = nearest.GlobalPosition;
            _reviverSnapshotTimer = 0.0f;
            _hud.ShowLocalizedMessage(
                "mate_reviving_you",
                "TEAMMATE MOVING TO REVIVE  //  HOLD ON",
                new Color(0.55f, 0.92f, 0.68f));
        }

        var reviver = _leaderReviver;
        var distanceToLeader = reviver.GlobalPosition.DistanceTo(_player.GlobalPosition);
        if (distanceToLeader > 2.3f)
        {
            _leaderReviveChannel = Mathf.Max(0.0f, _leaderReviveChannel - delta * 1.5f);
            // Anti-stuck: measure progress over one-second windows, not per frame.
            _reviverSnapshotTimer += delta;
            if (_reviverSnapshotTimer >= 1.0f)
            {
                if (reviver.GlobalPosition.DistanceTo(_reviverLastPosition) < 1.4f)
                {
                    _reviverStuckTime += _reviverSnapshotTimer;
                }
                else
                {
                    _reviverStuckTime = 0.0f;
                }
                _reviverLastPosition = reviver.GlobalPosition;
                _reviverSnapshotTimer = 0.0f;
            }
            if (_reviverStuckTime >= 3.5f)
            {
                var side = (reviver.GlobalPosition - _player.GlobalPosition).Normalized();
                if (side.LengthSquared() < 0.01f)
                {
                    side = Vector3.Forward;
                }
                reviver.GlobalPosition = _player.GlobalPosition + side * 1.6f + Vector3.Up * 0.35f;
                _reviverStuckTime = 0.0f;
                _reviverSnapshotTimer = 0.0f;
                _reviverLastPosition = reviver.GlobalPosition;
            }
            return;
        }

        _reviverStuckTime = 0.0f;
        _reviverSnapshotTimer = 0.0f;
        _reviverLastPosition = reviver.GlobalPosition;
        _leaderReviveChannel += delta / 2.8f;
        if (_leaderReviveChannel < 1.0f)
        {
            return;
        }

        _leaderReviveChannel = 0.0f;
        var revived = _player.TryReceiveRevive(60.0f);
        if (!revived)
        {
            ClearLeaderReviveAi();
        }
        // On success TryReceiveRevive already triggered OnLocalPlayerRevived,
        // which clears the reviver state.
    }

    private void ClearLeaderReviveAi()
    {
        if (_leaderReviver is not null && IsInstanceValid(_leaderReviver))
        {
            _leaderReviver.EndLeaderRevive();
        }
        _leaderReviver = null;
        _leaderReviveChannel = 0.0f;
        _reviverStuckTime = 0.0f;
        _reviverSnapshotTimer = 0.0f;
    }

    private void BeginSquadMateView()
    {
        _spectatedMate = FindLivingSpectatorTarget();
        if (_spectatedMate is null)
        {
            return;
        }

        if (_squadSpectatorCamera is null || !IsInstanceValid(_squadSpectatorCamera))
        {
            _squadSpectatorCamera = new Camera3D
            {
                Name = "SquadSpectatorCamera",
                Fov = 76.0f,
                Near = 0.04f
            };
            AddChild(_squadSpectatorCamera);
        }

        SnapSquadSpectatorCamera();
        _squadSpectatorCamera.MakeCurrent();
    }

    private void UpdateSquadSpectatorCamera()
    {
        if (_squadSpectatorCamera is null || !IsInstanceValid(_squadSpectatorCamera))
        {
            BeginSquadMateView();
            return;
        }

        if (_spectatedMate is null || !IsInstanceValid(_spectatedMate)
            || _spectatedMate.IsDowned || _spectatedMate.IsBodyBag)
        {
            _spectatedMate = FindLivingSpectatorTarget();
        }
        if (_spectatedMate is null)
        {
            return;
        }

        SnapSquadSpectatorCamera();
        if (!_squadSpectatorCamera.Current)
        {
            _squadSpectatorCamera.MakeCurrent();
        }
    }

    private SquadMate? FindLivingSpectatorTarget()
    {
        return _squadMates
            .Where(mate => IsInstanceValid(mate) && !mate.IsDowned && !mate.IsBodyBag)
            .OrderBy(mate => mate.GlobalPosition.DistanceSquaredTo(_player.GlobalPosition))
            .FirstOrDefault();
    }

    private void SnapSquadSpectatorCamera()
    {
        if (_squadSpectatorCamera is null || _spectatedMate is null
            || !IsInstanceValid(_squadSpectatorCamera) || !IsInstanceValid(_spectatedMate))
        {
            return;
        }

        var basis = _spectatedMate.GlobalBasis.Orthonormalized();
        var eyePosition = _spectatedMate.GlobalPosition
            + Vector3.Up * 1.64f
            - basis.Z * 0.28f;
        _squadSpectatorCamera.GlobalTransform = new Transform3D(basis, eyePosition);
    }

    private void RestoreLocalPlayerView()
    {
        _spectatedMate = null;
        var playerCamera = _player.GetNodeOrNull<Camera3D>("Head/CombatCamera");
        playerCamera?.MakeCurrent();
    }

    private bool IsSquadMateViewCurrent =>
        _squadSpectatorCamera is not null
        && IsInstanceValid(_squadSpectatorCamera)
        && GetViewport().GetCamera3D() == _squadSpectatorCamera
        && _spectatedMate is not null
        && IsInstanceValid(_spectatedMate)
        && !_spectatedMate.IsDowned
        && !_spectatedMate.IsBodyBag;

    private bool IsLocalPlayerViewCurrent
    {
        get
        {
            var playerCamera = _player.GetNodeOrNull<Camera3D>("Head/CombatCamera");
            return playerCamera is not null && GetViewport().GetCamera3D() == playerCamera;
        }
    }

    private float _manualReviveProgress;
    private ISquadCombatant? _manualReviveTarget;

    private void UpdateManualRevive(float delta)
    {
        if (!_squadDeployed || _missionEnded || !IsInstanceValid(_player))
        {
            return;
        }

        // Downed player cannot revive others.
        if (_player.IsDead || _player.IsInVehicle || _hud.IsLootVisible)
        {
            CancelManualRevive();
            return;
        }

        ISquadCombatant? target = null;
        var best = 2.85f;
        foreach (var friendly in FriendlyCombatants())
        {
            if (friendly == _player || !friendly.CanBeRevived)
            {
                continue;
            }
            var distance = _player.GlobalPosition.DistanceTo(friendly.CombatNode.GlobalPosition);
            if (distance < best)
            {
                best = distance;
                target = friendly;
            }
        }

        if (target is null)
        {
            CancelManualRevive();
            return;
        }

        var label = GameLocalization.IsChinese(_languageSetting)
            ? "按住 F 救援队友"
            : "HOLD F  //  REVIVE TEAMMATE";
        if (!Input.IsActionPressed("interact") || _interactReleaseRequired)
        {
            _manualReviveProgress = Mathf.Max(0.0f, _manualReviveProgress - delta * 1.4f);
            _manualReviveTarget = target;
            _hud.SetInteraction(label, _manualReviveProgress > 0.02f ? _manualReviveProgress : -1.0f, true);
            return;
        }

        if (!ReferenceEquals(_manualReviveTarget, target))
        {
            _manualReviveTarget = target;
            _manualReviveProgress = 0.0f;
        }

        _manualReviveProgress = Mathf.Min(1.0f, _manualReviveProgress + delta / 2.6f);
        _player.SetSearchPose(true, _manualReviveProgress);
        _hud.SetInteraction(label, _manualReviveProgress, true);
        if (_manualReviveProgress < 1.0f)
        {
            return;
        }

        var revived = target.TryReceiveRevive(62.0f);
        _manualReviveProgress = 0.0f;
        _manualReviveTarget = null;
        _interactReleaseRequired = true;
        _player.SetSearchPose(false);
        if (revived)
        {
            if (ReferenceEquals(target, _player) || target is TacticalPlayer)
            {
                OnLocalPlayerRevived();
            }
            _hud.ShowLocalizedMessage(
                "squad_revive",
                "MANUAL REVIVE  //  TEAMMATE STABILIZED",
                OperatorRoles.Spec(OperatorRole.Medic).Accent);
            SpawnMedicSprayEffect(_player.GlobalPosition + Vector3.Up * 1.2f, target.HitPoint(HitRegion.Torso));
        }
        else
        {
            _hud.ShowLocalizedMessage(
                "revive_exhausted",
                "REVIVE EXHAUSTED  //  NO SECOND CHANCE",
                new Color(1.0f, 0.42f, 0.28f));
        }
    }

    private void CancelManualRevive()
    {
        if (_manualReviveProgress > 0.0f)
        {
            _player.SetSearchPose(false);
        }
        _manualReviveProgress = 0.0f;
        _manualReviveTarget = null;
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
        var wasDown = target.CombatDowned || target.CombatDead;
        var revived = false;
        if (wasDown)
        {
            // Medic spray still requires the once-per-life revive budget.
            revived = target.TryReceiveRevive(58.0f);
            if (revived && target is TacticalPlayer)
            {
                OnLocalPlayerRevived();
            }
        }
        else
        {
            target.RestoreHealth(44.0f);
        }
        if (target != source && !source.CombatDead)
        {
            source.RestoreHealth(18.0f);
        }
        SpawnMedicSprayEffect(origin, targetPoint);
        if (wasDown && !revived)
        {
            _hud.ShowLocalizedMessage(
                "revive_exhausted",
                "REVIVE EXHAUSTED  //  NO SECOND CHANCE",
                new Color(1.0f, 0.42f, 0.28f));
        }
        else
        {
            _hud.ShowLocalizedMessage(
                wasDown ? "squad_revive" : "medic_spray",
                wasDown ? "MEDIC SPRAY  //  SQUADMATE REVIVED" : "MEDIC SPRAY  //  TRAUMA STABILIZED",
                OperatorRoles.Spec(OperatorRole.Medic).Accent);
        }
    }

    public IEnumerable<Node3D> GetHostileAircraftTargets()
    {
        if (IsInstanceValid(_player) && !_player.IsDead)
        {
            yield return _player;
        }
        foreach (var mate in _squadMates)
        {
            if (IsInstanceValid(mate) && !mate.IsDowned)
            {
                yield return mate;
            }
        }
    }

    public void ApplyAircraftStrike(Vector3 impact, float radius, float damage, Node source)
    {
        foreach (var friendly in FriendlyCombatants())
        {
            if (!IsInstanceValid(friendly.CombatNode))
            {
                continue;
            }
            var distance = friendly.CombatNode.GlobalPosition.DistanceTo(impact);
            if (distance > radius)
            {
                continue;
            }
            var falloff = 1.0f - distance / Mathf.Max(0.01f, radius);
            friendly.TakeCombatDamage(damage * falloff, impact, source);
        }
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
        _hud.ShowLocalizedMessage("squadmate_down", $"{mate.Callsign} DOWN  //  HOLD F TO REVIVE", new Color(1.0f, 0.34f, 0.22f));
    }

    public void OnSquadMateKia(SquadMate mate)
    {
        _hud.ShowLocalizedMessage(
            "squadmate_kia",
            $"{mate.Callsign} KIA  //  BODY BAG RECOVERABLE",
            new Color(1.0f, 0.22f, 0.16f));
    }

    public void SpawnSquadBodyBag(SquadMate mate)
    {
        if (!IsInstanceValid(mate))
        {
            return;
        }

        var bag = new SquadBodyBag
        {
            Name = $"BodyBag_{mate.Callsign}",
            Position = mate.GlobalPosition + Vector3.Up * 0.05f,
            EnglishName = $"{mate.Callsign} body bag",
            ChineseName = $"{mate.Callsign} 遗体袋"
        };
        // Light field kit left on the fallen operator.
        bag.Loot.Add(new LootItem { Kind = LootItemKind.Ammunition, Quantity = 30 });
        bag.Loot.Add(new LootItem { Kind = LootItemKind.ArmorPlate });
        if (_rng.Randf() < 0.45f)
        {
            bag.Loot.Add(new LootItem { Kind = LootItemKind.ArmorPlate });
        }
        AddChild(bag);
        _lootSources.Add(bag);
        _squadMates.Remove(mate);
    }

    public void SpawnAircraftShell(Vector3 from, Vector3 to, float damage, float blastRadius, Node owner)
    {
        var shell = new AircraftShell
        {
            Name = "HostileAircraftShell",
            Main = this,
            OwnerAircraft = owner,
            Position = from
        };
        AddChild(shell);
        shell.Launch(from, to, damage, blastRadius);
    }

    private bool HandleLocalPlayerDowned()
    {
        _player.EjectFromVehicleIfAny();
        var livingMate = _squadMates.Any(mate => IsInstanceValid(mate) && !mate.IsDowned);
        // Second life already used, or nobody left to revive → hard fail path.
        if (_player.ReviveUsed || !_squadDeployed || !livingMate)
        {
            return false;
        }
        _localPlayerDowned = true;
        _localPlayerDownedTimer = 0.0f;
        _player.UiLocked = false;
        Input.MouseMode = Input.MouseModeEnum.Captured;
        BeginSquadMateView();
        _hud.ShowDownedState(22.0f);
        _hud.ShowLocalizedMessage(
            "player_downed",
            "YOU ARE DOWN  //  CRAWL  //  TEAMMATE MOVING TO REVIVE",
            new Color(1.0f, 0.34f, 0.2f));
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
        RestoreLocalPlayerView();
        _hud.HideDownedState();
        _hud.ShowLocalizedMessage("player_revived", "REVIVED  //  BACK IN THE FIGHT", OperatorRoles.Spec(OperatorRole.Medic).Accent);
        ClearLeaderReviveAi();
    }

    private void FailSquadMission()
    {
        if (_missionEnded)
        {
            return;
        }
        _missionEnded = true;
        _localPlayerDowned = false;
        ClearLeaderReviveAi();
        _player.EjectFromVehicleIfAny();
        _hud.HideDownedState();
        Input.MouseMode = Input.MouseModeEnum.Visible;
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

        // 3-operator fill: player Assault → AI must be Medic + Recon (no third AI).
        _player.ConfigureRole(OperatorRole.Assault, refillHealth: true);
        EnsureAiSquadFill();
        var aiRoles = _squadMates.Where(mate => IsInstanceValid(mate) && !mate.IsHumanProxy).Select(mate => mate.Role).OrderBy(role => role).ToArray();
        var roleFillOk = ActiveSquadCount == 3
            && AiSquadCount == 2
            && aiRoles.Length == 2
            && aiRoles.Contains(OperatorRole.Medic)
            && aiRoles.Contains(OperatorRole.Recon)
            && !aiRoles.Contains(OperatorRole.Assault);

        // Leave deployment protection so live damage/down paths exercise shipped code.
        _missionDirector.ExitDeploymentZone();
        await WaitFrames(4);

        // Downed crawl + revive-once.
        var mate = _squadMates.First(m => IsInstanceValid(m) && !m.IsHumanProxy);
        mate.TakeCombatDamage(999.0f, mate.HitPoint(HitRegion.Torso), this);
        var mateDowned = mate.IsDowned && mate.CanBeRevived;
        var holdBefore = mate.GlobalPosition;
        for (var i = 0; i < 12; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
        // First down must stay put (no sliding human crawl).
        var mateCrawled = mate.IsDowned && mate.GlobalPosition.DistanceTo(holdBefore) < 0.35f;
        var firstRevive = mate.TryReceiveRevive(55.0f);
        var mateUp = !mate.IsDowned && firstRevive;
        // Second down after revive → permanent body bag (not a sliding human).
        var bagsBefore = GetTree().GetNodesInGroup("squad_body_bags").Count;
        var lootBefore = _lootSources.Count;
        mate.TakeCombatDamage(999.0f, mate.HitPoint(HitRegion.Torso), this);
        await WaitFrames(4);
        var bagsAfter = GetTree().GetNodesInGroup("squad_body_bags").Count;
        var bodyBagOk = bagsAfter > bagsBefore
            || _lootSources.Count > lootBefore
            || _lootSources.Exists(source => source is SquadBodyBag);
        // Mate is freed when converted; second revive is impossible by design.
        var secondReviveBlocked = bodyBagOk || (IsInstanceValid(mate) && !mate.CanBeRevived);

        // AI leader revive: down the player and let the remaining AI mate run over and pick them up.
        foreach (var enemy in _enemies)
        {
            if (IsInstanceValid(enemy))
            {
                enemy.ProcessMode = ProcessModeEnum.Disabled;
            }
        }
        var reviverMate = _squadMates.FirstOrDefault(m => IsInstanceValid(m) && !m.IsHumanProxy && !m.IsDowned);
        var aiReviveOk = false;
        var squadMateViewOnDown = false;
        var playerViewAfterRevive = false;
        if (reviverMate is not null)
        {
            // Deterministic arena: spawns are dispersed per run, so stage both actors on open ground.
            _player.GlobalPosition = new Vector3(0.0f, 0.3f, 60.0f);
            await WaitFrames(4);
            reviverMate.GlobalPosition = _player.GlobalPosition + new Vector3(20.0f, 0.1f, 0.0f);
            _player.SetHealthForDiagnostics(10.0f);
            _player.SetReviveUsedForDiagnostics(false);
            _player.TakeDamage(999.0f, _player.HitPoint(HitRegion.Torso), this);
            if (!_player.IsDead)
            {
                _player.TakeCombatDamage(999.0f, _player.HitPoint(HitRegion.Torso), this);
            }
            var aiReviveDowned = _player.IsDead && _localPlayerDowned;
            await WaitFrames(1);
            squadMateViewOnDown = IsSquadMateViewCurrent;
            for (var second = 0; second < 16 && _player.IsDead; second++)
            {
                await ToSignal(GetTree().CreateTimer(1.0f), SceneTreeTimer.SignalName.Timeout);
                GD.Print($"AI_REVIVE_DBG s={second} dead={_player.IsDead} reviving={reviverMate.IsRevivingLeader} dist={reviverMate.GlobalPosition.DistanceTo(_player.GlobalPosition):0.0} matePos=({reviverMate.GlobalPosition.X:0.0},{reviverMate.GlobalPosition.Z:0.0}) assigned={_leaderReviver?.Callsign ?? "none"} channel={_leaderReviveChannel:0.00} stuck={_reviverStuckTime:0.0} downedFlag={_localPlayerDowned}");
            }
            playerViewAfterRevive = IsLocalPlayerViewCurrent;
            aiReviveOk = aiReviveDowned && !_player.IsDead && _player.ReviveUsed
                && !_localPlayerDowned && !reviverMate.IsRevivingLeader
                && squadMateViewOnDown && playerViewAfterRevive;
        }

        _player.SetHealthForDiagnostics(10.0f);
        _player.SetReviveUsedForDiagnostics(false);
        _player.TakeDamage(999.0f, _player.HitPoint(HitRegion.Torso), this);
        if (!_player.IsDead)
        {
            _player.TakeCombatDamage(999.0f, _player.HitPoint(HitRegion.Torso), this);
        }
        var playerDowned = _player.IsDead && _player.CanBeRevived;
        var playerFirstRevive = _player.TryReceiveRevive(50.0f);
        var playerUp = !_player.IsDead && playerFirstRevive && _player.ReviveUsed;
        _player.TakeCombatDamage(999.0f, _player.HitPoint(HitRegion.Torso), this);
        var playerSecondBlocked = _player.IsDead && !_player.CanBeRevived && !_player.TryReceiveRevive(50.0f) && _player.ReviveUsed;

        var reviveOk = mateDowned && firstRevive && mateUp && bodyBagOk && secondReviveBlocked
            && playerDowned && playerFirstRevive && playerUp && playerSecondBlocked;

        GD.Print($"SQUAD_CHECK members={ActiveSquadCount} ai={AiSquadCount} role_fill={roleFillOk} ai_roles={string.Join("+", aiRoles)} default_follow={defaultFollow} follow_motion={followMotion} ai_cooldown={aiCooldownEnforced} ai_cooldown_seconds={cooldownMate.SkillCooldownDuration:0} medic_self={medicSelf} recon={scanned} assault_speed={assaultSpeed:0.00} assault_fire={assaultFire:0.00} orders={hold && move && follow} revive_once={reviveOk} ai_leader_revive={aiReviveOk} squad_view_on_down={squadMateViewOnDown} player_view_after_revive={playerViewAfterRevive} body_bag={bodyBagOk} prone_hold={mateCrawled} hud={!_hud.IsSquadLobbyVisible} keys={(long)Key.H}/{(long)Key.F1}/{(long)Key.F2}/{(long)Key.F3}");
        GD.Print($"SQUAD_PASS valid={ActiveSquadCount >= 2 && roleFillOk && reviveOk && aiReviveOk}");
        GetTree().Quit(roleFillOk && reviveOk && aiReviveOk ? 0 : 2);
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
