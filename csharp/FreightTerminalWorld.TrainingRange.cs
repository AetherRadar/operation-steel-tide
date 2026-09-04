using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private sealed class TrainingRangeBotSlot
    {
        public Vector3 Spawn;
        public EnemyOperator? Bot;
        public float RespawnTimer;
    }

    private static readonly Vector3[] TrainingRangeBotOffsets =
    {
        new(-6.0f, 0.15f, -12.0f),
        new(0.0f, 0.15f, -17.0f),
        new(6.0f, 0.15f, -12.0f),
        new(-4.5f, 0.15f, -24.0f),
        new(4.5f, 0.15f, -24.0f),
        new(0.0f, 0.15f, -30.0f)
    };

    private readonly List<TrainingRangeBotSlot> _trainingRangeBotSlots = new();
    private bool _trainingRangeActive;
    private Vector3 _trainingRangeOrigin;
    private int _trainingRangeKills;

    public bool IsTrainingRangeActive => _trainingRangeActive;
    public int TrainingRangeBotCount
        => _trainingRangeBotSlots.Count(slot => IsInstanceValid(slot.Bot) && !slot.Bot!.IsDead);
    public int TrainingRangeKills => _trainingRangeKills;

    private void EnterTrainingRange()
    {
        if (_trainingRangeActive)
        {
            return;
        }

        _trainingRangeActive = true;
        _trainingRangeKills = 0;
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

        _trainingRangeOrigin = DeploymentPoint;
        _player.PrepareTrainingRangeLoadout(_trainingRangeOrigin);
        _trainingRangeBotSlots.Clear();
        for (var i = 0; i < TrainingRangeBotOffsets.Length; i++)
        {
            var slot = new TrainingRangeBotSlot
            {
                Spawn = _trainingRangeOrigin + TrainingRangeBotOffsets[i],
                RespawnTimer = 0.0f
            };
            _trainingRangeBotSlots.Add(slot);
            SpawnTrainingRangeBot(slot, i);
        }

        _enemiesRemaining = TrainingRangeBotCount;
        _hud.ShowTrainingRangeGameplay(GameLocalization.Get(
            "training_range_status",
            _languageSetting,
            "TRAINING RANGE  //  LIVE FIRE"));
        _hud.SetMissionPhase("TRAINING_RANGE", 0.0f, false);
        _hud.SetEnemyCount(_enemiesRemaining);
        _hud.SetObjective(GameLocalization.Get(
            "training_range_ready",
            _languageSetting,
            "TRAINING RANGE READY  //  MOUSE WHEEL CYCLE GUNS  //  INFINITE AMMO  //  BOTS RESPAWN"));
        _hud.SetAlert(0.0f, "TRAINING_RANGE");
        Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    private void SpawnTrainingRangeBot(TrainingRangeBotSlot slot, int index)
    {
        var visuals = new[]
        {
            OperatorVisualId.Garrison,
            OperatorVisualId.Heron,
            OperatorVisualId.Lynx,
            OperatorVisualId.Magpie,
            OperatorVisualId.Jackal,
            OperatorVisualId.Viper
        };
        var bot = SpawnEnemy(
            slot.Spawn,
            alerted: false,
            initialWeapon: WeaponCatalog.Build(WeaponPlatform.M4A1, 1),
            sentryMode: true,
            detectionRange: 0.0f,
            operatorVisual: visuals[index % visuals.Length]);
        bot.Name = $"RANGE_BOT_{index + 1:00}";
        bot.Visible = true;
        bot.CollisionLayer = 2;
        bot.CollisionMask = 1 | BreakableGlassField.MovementCollisionLayer;
        var look = _player.GlobalPosition - bot.GlobalPosition;
        look.Y = 0.0f;
        if (look.LengthSquared() > 0.01f)
        {
            bot.LookAt(bot.GlobalPosition + look, Vector3.Up);
        }
        bot.SetAuthoredCombatPoseForDiagnostics();
        bot.ProcessMode = ProcessModeEnum.Disabled;
        bot.SetPhysicsProcess(false);
        slot.Bot = bot;
        slot.RespawnTimer = 0.0f;
    }

    private void UpdateTrainingRange(float delta)
    {
        _player.RefillTrainingRangeAmmo();
        foreach (var slot in _trainingRangeBotSlots)
        {
            if (IsInstanceValid(slot.Bot))
            {
                continue;
            }
            slot.Bot = null;
            slot.RespawnTimer -= delta;
            if (slot.RespawnTimer <= 0.0f)
            {
                SpawnTrainingRangeBot(slot, _trainingRangeBotSlots.IndexOf(slot));
            }
        }
        _enemiesRemaining = TrainingRangeBotCount;
        _hud.SetEnemyCount(_enemiesRemaining);
        _hud.SetAlert(0.0f, "TRAINING_RANGE");
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
        slot.Bot = null;
        slot.RespawnTimer = 0.85f;
        _enemies.Remove(enemy);
        _lootSources.Remove(enemy);
        _enemiesRemaining = TrainingRangeBotCount;
        _hud.SetEnemyCount(_enemiesRemaining);
        enemy.Visible = false;
        enemy.ProcessMode = ProcessModeEnum.Disabled;
        enemy.SetPhysicsProcess(false);
        enemy.CollisionLayer = 0;
        enemy.CollisionMask = 0;
        enemy.QueueFree();
        InvalidateCombatTargetIndex();
        return true;
    }
}
