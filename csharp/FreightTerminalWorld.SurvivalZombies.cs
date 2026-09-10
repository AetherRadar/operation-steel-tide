using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private readonly List<ZombieNpc> _survivalZombies = new();
    private float _survivalWaveTimer;
    private int _survivalWave = 1;
    private int _survivalEliminations;

    private void InitializeSurvivalZombies()
    {
        if (!IsSurvivalMode) return;
        _survivalWave = 1;
        _survivalWaveTimer = 24.0f;
        SpawnSurvivalWave(1);
    }

    private void UpdateSurvivalZombies(float delta)
    {
        if (!IsSurvivalMode || _missionEnded) return;
        _survivalWaveTimer -= delta;
        if (_survivalWaveTimer <= 0.0f)
        {
            _survivalWave++;
            _survivalWaveTimer = Mathf.Max(18.0f, 52.0f - _survivalWave * 3.0f);
            SpawnSurvivalWave(_survivalWave);
            _hud?.ShowLocalizedFormattedMessage("survival_wave", "INFECTED WAVE {0} INBOUND", new Color(1.0f, 0.38f, 0.27f), _survivalWave);
        }
        _survivalZombies.RemoveAll(z => !IsInstanceValid(z) || z.IsDead);
        _hud?.SetSurvivalStatus(_survivalWave, _survivalWaveTimer, _survivalZombies.Count, _survivalEliminations);
    }

    private void SpawnSurvivalWave(int wave)
    {
        var count = Mathf.Min(4 + wave * 2, 24);
        for (var i = 0; i < count; i++)
        {
            var angle = Mathf.Tau * i / Mathf.Max(1, count) + _rng.RandfRange(-0.18f, 0.18f);
            var radius = _rng.RandfRange(15.0f, 27.0f);
            var zombie = new ZombieNpc
            {
                Name = $"Infected_{wave:00}_{i:00}",
                Main = this,
                Wave = wave,
                Position = DeploymentPoint + new Vector3(Mathf.Cos(angle) * radius, 0.2f, Mathf.Sin(angle) * radius)
            };
            AddChild(zombie);
            _survivalZombies.Add(zombie);
        }
    }

    internal void NotifyZombieEliminated(ZombieNpc zombie)
    {
        _survivalEliminations++;
        _kills++;
        _hud?.SetEnemyCount(_survivalZombies.Count);
    }

    internal void NotifySurvivalBreach(Vector3 position)
    {
        _hud?.ShowLocalizedMessage("survival_breach", "INFECTED BREACH", new Color(1.0f, 0.32f, 0.2f));
    }

    internal void HandleSurvivalHits(PhysicsRaycastHit hit, float damage, Vector3 end)
    {
        if (hit.Collider is ZombieNpc zombie)
            zombie.TakeDamage(damage, end, _player);
    }
}
