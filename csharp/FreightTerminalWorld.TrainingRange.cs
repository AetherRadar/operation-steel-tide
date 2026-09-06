using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private sealed class TrainingRangeBotSlot
    {
        public TrainingRangeBotProfile Profile;
        public Vector3 Spawn;
        public EnemyOperator? Bot;
        public float RespawnTimer;
        public float MotionPhase;
        public float LastHealth = 100.0f;
        public bool RespawnPending;
        public bool IsDowned;
    }

    private readonly List<TrainingRangeBotSlot> _trainingRangeBotSlots = new();
    private bool _trainingRangeActive;
    private Vector3 _trainingRangeOrigin;
    private int _trainingRangeKills;
    private int _trainingRangeResetCueCount;
    // Patrol targets are the useful default for a live-fire lane; the setup
    // panel still exposes fixed targets when a stationary silhouette is wanted.
    private int _trainingRangeBotType = 1;
    private int _trainingRangeBotCount = 6;
    private int _trainingRangeWeaponIndex;
    private int _trainingRangeAmmoType;
    private int _trainingRangeAmmoLevel = 2;
    private float _trainingRangeInteractionCooldown;
    private float _trainingRangeElapsedSeconds;
    private float _trainingRangeRespawnDelaySeconds = 1.0f;
    private bool _trainingRangeDecreaseHeld;
    private bool _trainingRangeIncreaseHeld;
    private string?[] _trainingRangePendingAttachmentIds = new string?[6];
    private readonly RandomNumberGenerator _trainingRangeSpawnRng = new();

    private static readonly int[] TrainingRangeBotCountOptions = { 3, 6, 12, 24 };
    private const float TrainingRangeRespawnDelaySeconds = 1.0f;

    public bool IsTrainingRangeActive => _trainingRangeActive;
    public int TrainingRangeBotCount
        => _trainingRangeBotSlots.Count(slot => IsInstanceValid(slot.Bot) && !slot.Bot!.IsDead);
    public int TrainingRangeKills => _trainingRangeKills;
    public int TrainingRangeResetCueCount => _trainingRangeResetCueCount;
    public int TrainingRangeBotType => _trainingRangeBotType;
    public int TrainingRangeConfiguredBotCount => _trainingRangeBotCount;
    public int TrainingRangeConfiguredWeaponIndex => _trainingRangeWeaponIndex;
    public int TrainingRangeConfiguredAmmoType => _trainingRangeAmmoType;
    public int TrainingRangeConfiguredAmmoLevel => _trainingRangeAmmoLevel;

    /// <summary>
    /// Keeps the world payload in step with the live weapon-cycle control.  The
    /// player owns the Q/weapon-cycle index while a station panel owns the
    /// persisted configuration; synchronizing here prevents F3 from reopening
    /// with the previous platform after a live cycle.
    /// </summary>
    internal void SyncTrainingRangeWeaponIndex(int index)
    {
        if (!_trainingRangeActive || !IsInstanceValid(_player))
        {
            return;
        }

        _trainingRangeWeaponIndex = Mathf.Clamp(
            index,
            0,
            Mathf.Max(0, _player.TrainingRangeWeaponCount - 1));
    }

    /// <summary>Apply the setup panel selection; if already in the range, rebuild targets immediately.</summary>
    public void ConfigureTrainingRange(
        int botType,
        int botCount,
        int weaponIndex,
        int ammoType,
        int ammoLevel)
    {
        _trainingRangeBotType = Mathf.Clamp(botType, 0, 2);
        _trainingRangeBotCount = NormalizeTrainingRangeBotCount(botCount);
        var weaponCount = IsInstanceValid(_player) ? _player.TrainingRangeWeaponCount : 13;
        _trainingRangeWeaponIndex = Mathf.Clamp(weaponIndex, 0, Mathf.Max(0, weaponCount - 1));
        _trainingRangeAmmoType = Mathf.Clamp(ammoType, 0, 3);
        _trainingRangeAmmoLevel = Mathf.Clamp(ammoLevel, 0, 3);
        if (!_trainingRangeActive || !IsInstanceValid(_player))
        {
            return;
        }
        ApplyTrainingRangeConfiguration();
    }

    /// <summary>Public entry point for the setup panel and diagnostics.</summary>
    public void StartTrainingRange(
        int botType = 0,
        int botCount = 6,
        int weaponIndex = 0,
        int ammoType = 0,
        int ammoLevel = 2)
    {
        ConfigureTrainingRange(botType, botCount, weaponIndex, ammoType, ammoLevel);
        EnterTrainingRange();
    }

    private void EnterTrainingRange()
    {
        if (_trainingRangeActive)
        {
            ApplyTrainingRangeConfiguration();
            return;
        }

        // The setup panel is modal and normally leaves the scene paused.  Deploying
        // the selected kit is the hand-off back to the live simulation.
        GetTree().Paused = false;
        _hud.HideTrainingRangeSetup(applied: true);
        _player.UiLocked = false;
        _player.RestoreMovementInput();
        _player.DisarmFireInput();

        _trainingRangeActive = true;
        _trainingRangeKills = 0;
        _trainingRangeResetCueCount = 0;
        _trainingRangeElapsedSeconds = 0.0f;
        _trainingRangeRespawnDelaySeconds = TrainingRangeRespawnDelaySeconds;
        _trainingRangeSpawnRng.Seed = 0x535445454C544944UL;
        _trainingRangeDecreaseHeld = false;
        _trainingRangeIncreaseHeld = false;
        _demolitionMode = false;
        _missionEnded = false;
        _squadDeployed = true;
        _localPlayerDowned = false;
        _localPlayerEliminated = false;
        _missionPhase = "TRAINING_RANGE";
        _missionRemaining = 0.0f;
        _missionDirector.ProcessMode = ProcessModeEnum.Disabled;
        _squadNetwork.StopLanRoomBrowsing();
        _squadNetwork.Close();

        var arena = ActivateDedicatedTrainingRangeArena();

        // The range is a solo test venue.  Park any extraction squad actors that
        // may still exist when the player re-enters from a live mission so they
        // cannot follow the player, fire at targets, or leak into the range HUD.
        foreach (var mate in _squadMates)
        {
            if (!IsInstanceValid(mate))
            {
                continue;
            }
            mate.Visible = false;
            mate.ProcessMode = ProcessModeEnum.Disabled;
            mate.SetPhysicsProcess(false);
            mate.CollisionLayer = 0;
            mate.CollisionMask = 0;
        }

        foreach (var enemy in _enemies.ToArray())
        {
            if (!IsInstanceValid(enemy))
            {
                continue;
            }
            enemy.Visible = false;
            enemy.ProcessMode = ProcessModeEnum.Disabled;
            enemy.SetPhysicsProcess(false);
            enemy.CollisionLayer = 0;
            enemy.CollisionMask = 0;
            enemy.QueueFree();
        }
        _enemies.Clear();
        _hostileSquads.Clear();
        _lootSources.RemoveAll(source => source is EnemyOperator);
        _worldBoss = null;
        if (IsInstanceValid(_aircraft))
        {
            _aircraft!.Visible = false;
            _aircraft.SetPhysicsProcess(false);
        }
        if (IsInstanceValid(_extractionMarker))
        {
            _extractionMarker.Visible = false;
        }

        _trainingRangeOrigin = arena.Origin;
        ConfigureTrainingRangeMinimap(arena);
        _player.PrepareTrainingRangeLoadout(arena.PlayerSpawn);
        _player.SelectTrainingRangeWeapon(_trainingRangeWeaponIndex);
        _player.ApplyTrainingRangeAttachmentPreset(_trainingRangePendingAttachmentIds);
        _player.ApplyTrainingRangeAmmoProfile(_trainingRangeAmmoType, _trainingRangeAmmoLevel);
        RebuildTrainingRangeBots(arena);

        _enemiesRemaining = TrainingRangeBotCount;
        _hud.ShowTrainingRangeGameplay(GameLocalization.Get(
            "training_range_status",
            _languageSetting,
            "TRAINING RANGE  //  LIVE FIRE"));
        _hud.SetMissionPhase("TRAINING_RANGE", 0.0f, false);
        _hud.SetTrainingRangeTargetCount(_enemiesRemaining);
        _hud.SetTrainingRangeStats(_trainingRangeKills, _trainingRangeElapsedSeconds, _trainingRangeRespawnDelaySeconds, _trainingRangeBotType);
        _hud.SetObjective(GameLocalization.Get(
            "training_range_ready",
            _languageSetting,
            "SELECT TARGET, WEAPON, AMMO  //  START FIRING"));
        _hud.SetAlert(0.0f, "TRAINING_RANGE");
        Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    private static int NormalizeTrainingRangeBotCount(int count)
    {
        var nearest = TrainingRangeBotCountOptions[0];
        var distance = Math.Abs(count - nearest);
        foreach (var option in TrainingRangeBotCountOptions)
        {
            var candidateDistance = Math.Abs(count - option);
            if (candidateDistance < distance)
            {
                nearest = option;
                distance = candidateDistance;
            }
        }
        return nearest;
    }

    /// <summary>
    /// The live-fire venue has its own compact map.  Replacing the extraction map
    /// here is important: the player is hundreds of metres away from the production
    /// level, so showing relay/extraction landmarks would make the mode look broken.
    /// </summary>
    private void ConfigureTrainingRangeMinimap(TrainingRangeArenaRuntime arena)
    {
        var landmarks = new List<TacticalMapLandmark>();
        foreach (var station in arena.Stations)
        {
            var (key, english, accent) = station.Kind switch
            {
                TrainingRangeStationKind.Weapon =>
                    ("training_station_weapon_map", "ARMORY", new Color(0.28f, 0.84f, 1.0f)),
                TrainingRangeStationKind.Ammunition =>
                    ("training_station_ammo_map", "AMMO", new Color(1.0f, 0.7f, 0.24f)),
                _ =>
                    ("training_station_bot_map", "BOT", new Color(0.42f, 1.0f, 0.62f))
            };
            landmarks.Add(new TacticalMapLandmark(station.Position, key, english, accent));
        }

        // Keep the map readable at a glance: the target lanes are visible in front of
        // the player, while the map reserves its labels for the three real stations.
        _hud.ConfigureMinimap(
            new Rect2(arena.Origin.X - 50.0f, arena.Origin.Z - 50.0f, 100.0f, 100.0f),
            landmarks);
        _hud.SetMinimapTitle("training_range_minimap_title", "RANGE MAP");
        _hud.SetMinimapWorldBoss(Vector3.Zero, false);
        _hud.SetMinimapPlayer(_player.GlobalPosition, 0.0f);
    }

    private void ApplyTrainingRangeConfiguration()
    {
        var arena = EnsureTrainingRangeArena();
        _player.SelectTrainingRangeWeapon(_trainingRangeWeaponIndex);
        _player.ApplyTrainingRangeAttachmentPreset(_trainingRangePendingAttachmentIds);
        _player.ApplyTrainingRangeAmmoProfile(_trainingRangeAmmoType, _trainingRangeAmmoLevel);
        RebuildTrainingRangeBots(arena);
        _hud.SetTrainingRangeTargetCount(TrainingRangeBotCount);
        _hud.SetObjective(BuildTrainingRangeObjective());
    }

    private string BuildTrainingRangeObjective()
    {
        var botMode = _trainingRangeBotType switch
        {
            1 => GameLocalization.Get("training_bot_patrol", _languageSetting, "PATROL BOTS"),
            2 => GameLocalization.Get("training_bot_reactive", _languageSetting, "REACTIVE BOTS"),
            _ => GameLocalization.Get("training_bot_static", _languageSetting, "STATIC TARGETS")
        };
        var ammoName = _trainingRangeAmmoType switch
        {
            1 => GameLocalization.Get("training_ammo_ap", _languageSetting, "ARMOR PIERCING"),
            2 => GameLocalization.Get("training_ammo_hp", _languageSetting, "HOLLOW POINT"),
            3 => GameLocalization.Get("training_ammo_tracer", _languageSetting, "TRACER"),
            _ => GameLocalization.Get("training_ammo_fmj", _languageSetting, "FULL METAL JACKET")
        };
        var weapon = WeaponCatalog.Build(_player.TrainingRangeWeaponPlatform, 3)
            .DisplayName(_languageSetting);
        return GameLocalization.Format(
            "training_range_objective",
            _languageSetting,
            "RANGE  //  {0} {1}  //  {2}  //  {3} T{4}  //  F INTERACT",
            botMode,
            _trainingRangeBotCount,
            weapon,
            ammoName,
            _trainingRangeAmmoLevel + 1);
    }

    private void RebuildTrainingRangeBots(TrainingRangeArenaRuntime arena)
    {
        foreach (var slot in _trainingRangeBotSlots)
        {
            RemoveTrainingRangeBot(slot.Bot);
        }
        _trainingRangeBotSlots.Clear();
        var count = Mathf.Min(_trainingRangeBotCount, arena.BotProfiles.Count);
        for (var index = 0; index < count; index++)
        {
            var profile = arena.BotProfile(index);
            var slot = new TrainingRangeBotSlot
            {
                Profile = profile,
                Spawn = profile.Position,
                RespawnTimer = 0.0f,
                MotionPhase = index * 0.71f,
                LastHealth = 100.0f
            };
            _trainingRangeBotSlots.Add(slot);
            SpawnTrainingRangeBot(slot);
        }
        _enemiesRemaining = TrainingRangeBotCount;
    }

    private void RemoveTrainingRangeBot(EnemyOperator? bot)
    {
        if (bot is null || !IsInstanceValid(bot))
        {
            return;
        }
        bot.Eliminated -= OnEnemyEliminated;
        _enemies.Remove(bot);
        _lootSources.Remove(bot);
        bot.ProcessMode = ProcessModeEnum.Disabled;
        bot.SetPhysicsProcess(false);
        bot.QueueFree();
    }

    /// <summary>
    /// Spawn a range target directly instead of SpawnEnemy so extraction-network loot
    /// registration cannot leak into the standalone scene.
    /// </summary>
    private void SpawnTrainingRangeBot(TrainingRangeBotSlot slot)
    {
        var networkId = _nextEnemyNetworkId++;
        var bot = new EnemyOperator
        {
            Name = $"RANGE_BOT_{slot.Profile.Index + 1:00}",
            Position = slot.Spawn,
            NetworkId = networkId,
            SimulationSeed = ExtractionEntitySeed(networkId),
            Player = _player,
            Main = this,
            MissionDirector = _missionDirector,
            DetectionRange = _trainingRangeBotType == 2 ? 86.0f : 0.0f,
            AccuracyBonus = _trainingRangeBotType == 2 ? -0.28f : 0.0f,
            TeamId = 0,
            SentryMode = true,
            OperatorVisual = slot.Profile.Visual
        };
        bot.ConfigureInitialLoadout(WeaponCatalog.Build(
            _trainingRangeBotType == 2 ? WeaponPlatform.MP5A5 : WeaponPlatform.M4A1,
            1));
        bot.SetMeta("training_range_target", true);
        AddChild(bot);
        bot.Eliminated += OnEnemyEliminated;
        _enemies.Add(bot);
        InvalidateCombatTargetIndex();
        bot.Visible = true;
        bot.CollisionLayer = 2;
        bot.CollisionMask = 1 | BreakableGlassField.MovementCollisionLayer;
        FaceTrainingRangePlayer(bot);
        bot.PrepareTrainingRangeVisualForDiagnostics();
        bot.SetMeta("training_range_bot_mode", _trainingRangeBotType);
        if (_trainingRangeBotType == 2)
        {
            // Reactive targets use the real EnemyOperator aim/fire loop, but stay
            // deliberately inaccurate and inside the dedicated range.
            bot.ProcessMode = ProcessModeEnum.Inherit;
            bot.SetPhysicsProcess(true);
            bot.SetAlerted(_player.GlobalPosition);
        }
        else
        {
            // Static and patrol targets are driven by the deterministic range motor.
            // Keep the CharacterBody3D in the scene's inherited process mode even
            // when its script physics tick is disabled: Godot removes a disabled
            // physics body from the space, so player raycasts would pass through
            // every target and hit the ground behind it.
            bot.ProcessMode = ProcessModeEnum.Inherit;
            bot.SetPhysicsProcess(false);
        }
        slot.Bot = bot;
        slot.RespawnTimer = 0.0f;
        slot.RespawnPending = false;
        slot.IsDowned = false;
        slot.LastHealth = bot.CurrentHealth;
    }

    private void FaceTrainingRangePlayer(EnemyOperator bot)
    {
        var look = _player.GlobalPosition - bot.GlobalPosition;
        look.Y = 0.0f;
        if (look.LengthSquared() > 0.01f)
        {
            bot.LookAt(bot.GlobalPosition + look, Vector3.Up);
        }
    }

    private void UpdateTrainingRange(float delta)
    {
        _trainingRangeElapsedSeconds += Mathf.Max(0.0f, delta);
        var decreasePressed = Input.IsKeyPressed(Key.Minus);
        var increasePressed = Input.IsKeyPressed(Key.Equal);
        if (decreasePressed && !_trainingRangeDecreaseHeld)
        {
            _trainingRangeRespawnDelaySeconds = Mathf.Max(0.25f, _trainingRangeRespawnDelaySeconds - 0.25f);
        }
        if (increasePressed && !_trainingRangeIncreaseHeld)
        {
            _trainingRangeRespawnDelaySeconds = Mathf.Min(5.0f, _trainingRangeRespawnDelaySeconds + 0.25f);
        }
        _trainingRangeDecreaseHeld = decreasePressed;
        _trainingRangeIncreaseHeld = increasePressed;
        _hud.KeepTrainingRangeOverlaysHidden();
        _player.RefillTrainingRangeAmmo();
        foreach (var slot in _trainingRangeBotSlots)
        {
            var bot = slot.Bot;
            if (!IsInstanceValid(bot))
            {
                // A target node should normally be retained for revive.  If an
                // external diagnostic removed it, recreate that lane safely.
                slot.Bot = null;
                if (!slot.RespawnPending)
                {
                    slot.RespawnPending = true;
                    slot.RespawnTimer = _trainingRangeRespawnDelaySeconds;
                }
                slot.RespawnTimer -= delta;
                if (slot.RespawnTimer <= 0.0f)
                {
                    SpawnTrainingRangeBot(slot);
                }
                continue;
            }

            if (bot.IsDead)
            {
                // Eliminated is normally handled synchronously.  This is only a
                // safety net for external diagnostics or a future damage source:
                // never leave a dead target kneeling and invulnerable in the range.
                RespawnTrainingRangeBotImmediately(slot, bot);
                continue;
            }

            slot.LastHealth = bot.CurrentHealth;
            if (_trainingRangeBotType == 1)
            {
                UpdateTrainingRangePatrol(slot, bot, delta);
            }
            else if (_trainingRangeBotType == 0)
            {
                FaceTrainingRangePlayer(bot);
            }
        }
        _enemiesRemaining = TrainingRangeBotCount;
        _hud.SetTrainingRangeTargetCount(_enemiesRemaining);
        _hud.SetTrainingRangeStats(_trainingRangeKills, _trainingRangeElapsedSeconds, _trainingRangeRespawnDelaySeconds, _trainingRangeBotType);
        _hud.SetAlert(0.0f, "TRAINING_RANGE");
    }

    private void UpdateTrainingRangePatrol(
        TrainingRangeBotSlot slot,
        EnemyOperator bot,
        float delta)
    {
        slot.MotionPhase += delta * (0.85f + (slot.Profile.Index % 3) * 0.18f);
        var lateral = Mathf.Sin(slot.MotionPhase) * 2.8f;
        var position = slot.Spawn + new Vector3(lateral, 0.0f, 0.0f);
        var target = _player.GlobalPosition - position;
        target.Y = 0.0f;
        var yaw = target.LengthSquared() > 0.01f
            ? Mathf.Atan2(-target.X, -target.Z)
            : bot.Rotation.Y;
        bot.SetTrainingRangeTargetPose(position, yaw);
    }

    private void ReviveTrainingRangeBot(TrainingRangeBotSlot slot)
    {
        var bot = slot.Bot;
        if (!IsInstanceValid(bot))
        {
            SpawnTrainingRangeBot(slot);
            return;
        }
        bot.ReviveForTrainingRange(slot.Spawn);
        FaceTrainingRangePlayer(bot);
        bot.SetMeta("training_range_bot_mode", _trainingRangeBotType);
        if (_trainingRangeBotType == 2)
        {
            bot.ProcessMode = ProcessModeEnum.Inherit;
            bot.SetPhysicsProcess(true);
            bot.SetAlerted(_player.GlobalPosition);
        }
        else
        {
            // See SpawnTrainingRangeBot: disabling the node itself unregisters the
            // CharacterBody3D from physics.  Only the AI tick is paused here.
            bot.ProcessMode = ProcessModeEnum.Inherit;
            bot.SetPhysicsProcess(false);
        }
        if (!_enemies.Contains(bot))
        {
            _enemies.Add(bot);
            InvalidateCombatTargetIndex();
        }
        slot.RespawnPending = false;
        slot.IsDowned = false;
        slot.RespawnTimer = 0.0f;
        slot.LastHealth = bot.CurrentHealth;
        _hud.ShowLocalizedFormattedMessage(
            "training_range_respawn",
            "{0}  RESET",
            new Color(0.36f, 0.95f, 0.68f),
            bot.Name);
    }

    private void RespawnTrainingRangeBotImmediately(TrainingRangeBotSlot slot, EnemyOperator bot)
    {
        var previous = bot.GlobalPosition;
        slot.Spawn = ChooseTrainingRangeRespawnPosition(slot, previous);
        slot.MotionPhase = _trainingRangeSpawnRng.RandfRange(0.0f, Mathf.Tau);
        slot.RespawnPending = false;
        slot.IsDowned = false;
        slot.RespawnTimer = 0.0f;
        bot.SuppressDeathAnimationForTrainingRange();
        bot.ReviveForTrainingRange(slot.Spawn);
        // ReviveForTrainingRange clears transient flags; keep the death callback
        // from starting the mission corpse tween when Die() resumes after EmitSignal.
        bot.SuppressDeathAnimationForTrainingRange();
        FaceTrainingRangePlayer(bot);
        bot.SetMeta("training_range_bot_mode", _trainingRangeBotType);
        if (_trainingRangeBotType == 2)
        {
            bot.ProcessMode = ProcessModeEnum.Inherit;
            bot.SetPhysicsProcess(true);
            bot.SetAlerted(_player.GlobalPosition);
        }
        else
        {
            bot.ProcessMode = ProcessModeEnum.Inherit;
            bot.SetPhysicsProcess(false);
        }
        bot.Visible = true;
        if (!_enemies.Contains(bot))
        {
            _enemies.Add(bot);
            InvalidateCombatTargetIndex();
        }
        slot.LastHealth = bot.CurrentHealth;
    }

    private Vector3 ChooseTrainingRangeRespawnPosition(
        TrainingRangeBotSlot slot,
        Vector3 previous)
    {
        var arena = _trainingRangeArena;
        if (arena is null || arena.BotProfiles.Count == 0)
        {
            return previous;
        }

        var candidates = new List<Vector3>(arena.BotProfiles.Count);
        foreach (var profile in arena.BotProfiles)
        {
            var candidate = profile.Position;
            if (candidate.DistanceTo(previous) < 0.5f
                || candidate.DistanceTo(_player.GlobalPosition) < 6.0f)
            {
                continue;
            }
            var occupied = false;
            foreach (var other in _trainingRangeBotSlots)
            {
                if (ReferenceEquals(other, slot)
                    || !IsInstanceValid(other.Bot)
                    || other.Bot!.IsDead)
                {
                    continue;
                }
                if (other.Bot.GlobalPosition.DistanceTo(candidate) < 3.0f)
                {
                    occupied = true;
                    break;
                }
            }
            if (!occupied)
            {
                candidates.Add(candidate);
            }
        }

        if (candidates.Count == 0)
        {
            foreach (var profile in arena.BotProfiles)
            {
                if (profile.Position.DistanceTo(previous) >= 0.5f)
                {
                    candidates.Add(profile.Position);
                }
            }
        }
        return candidates.Count == 0
            ? previous
            : candidates[_trainingRangeSpawnRng.RandiRange(0, candidates.Count - 1)];
    }

    private void PlayTrainingRangeTargetResetSound(Vector3 position)
    {
        var audio = new AudioStreamPlayer3D
        {
            Name = "TrainingRangeTargetResetAudio",
            Stream = SoundLab.TrainingRangeTargetReset(),
            VolumeDb = -2.0f,
            MaxDistance = 52.0f,
            UnitSize = 8.0f,
            Position = position
        };
        AddChild(audio);
        audio.Finished += audio.QueueFree;
        audio.Play();
        _trainingRangeResetCueCount++;
    }

    private bool HandleTrainingRangeBotEliminated(EnemyOperator enemy)
    {
        if (!_trainingRangeActive)
        {
            return false;
        }
        var slot = _trainingRangeBotSlots.Find(candidate => candidate.Bot == enemy);
        if (slot is null)
        {
            return false;
        }
        if (enemy.LastDamageAttacker == _player)
        {
            _trainingRangeKills++;
            _hud.ShowKnockdown(
                enemy.OperatorCallsign(_languageSetting),
                GameLocalization.Get("you", _languageSetting, "YOU"));
        }
        enemy.HideCorpseLootBackpackForTrainingRange();
        RespawnTrainingRangeBotImmediately(slot, enemy);
        PlayTrainingRangeTargetResetSound(slot.Spawn);
        _hud.ShowLocalizedFormattedMessage(
            "training_range_respawn",
            "{0}  RESET",
            new Color(0.36f, 0.95f, 0.68f),
            enemy.Name);
        _hud.SetTrainingRangeTargetCount(TrainingRangeBotCount);
        InvalidateCombatTargetIndex();
        return true;
    }
}
