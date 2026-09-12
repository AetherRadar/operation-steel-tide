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
    private bool _survivalVictoryShown;

    private void InitializeSurvivalZombies()
    {
        if (!IsSurvivalMode) return;
        _survivalWave = 1;
        _survivalWaveTimer = 28.0f;
        _survivalEliminations = 0;
        _survivalVictoryShown = false;
        SpawnSurvivalWave(1);
        _hud?.ShowLocalizedFormattedMessage(
            "survival_start",
            "SURVIVAL START  //  WAVE {0}",
            new Color(1.0f, 0.55f, 0.3f),
            _survivalWave);
    }

    private void UpdateSurvivalZombies(float delta)
    {
        if (!IsSurvivalMode || _missionEnded) return;
        _survivalZombies.RemoveAll(z => !IsInstanceValid(z) || z.IsDead);
        _survivalWaveTimer -= delta;
        if (_survivalWaveTimer <= 0.0f)
        {
            if (_survivalWave < ResidentialSurvivalWaveLimit)
            {
                _survivalWave++;
                _survivalWaveTimer = Mathf.Max(18.0f, 52.0f - _survivalWave * 3.0f);
                SpawnSurvivalWave(_survivalWave);
                _hud?.ShowLocalizedFormattedMessage(
                    "survival_wave",
                    "INFECTED WAVE {0} INBOUND",
                    new Color(1.0f, 0.38f, 0.27f),
                    _survivalWave);
            }
            else if (_survivalZombies.Count == 0)
            {
                CompleteSurvivalVictory();
            }
            else
            {
                // Hold the final-wave readout at zero while the player clears the
                // last contacts; this avoids spawning an unbounded ninth wave.
                _survivalWaveTimer = 0.0f;
            }
        }
        _hud?.SetSurvivalStatus(_survivalWave, _survivalWaveTimer, _survivalZombies.Count, _survivalEliminations);
    }

    private void CompleteSurvivalVictory()
    {
        if (_survivalVictoryShown || _missionEnded || !IsInstanceValid(_player) || _player.IsDead)
        {
            return;
        }
        _survivalVictoryShown = true;
        _missionEnded = true;
        LockLootForMissionTransition(Input.MouseModeEnum.Visible);
        _player.UiLocked = true;
        _player.DisarmFireInput();
        _player.DisarmMovementInput();
        _hud.HideDownedState();
        _hud.SetSquadCommandPresentation(false, false, suppressFooter: true);
        _missionDirector.CompleteMission(true, _kills, _headshots, _shotsFired, _shotsHit);
        _hud.ShowSurvivalResult(true, _survivalWave, _survivalEliminations);
    }

    private void CompleteSurvivalDefeat()
    {
        if (_missionEnded)
        {
            return;
        }
        _missionEnded = true;
        LockLootForMissionTransition(Input.MouseModeEnum.Visible);
        _player.UiLocked = true;
        _player.DisarmFireInput();
        _player.DisarmMovementInput();
        _hud.HideDownedState();
        _hud.SetSquadCommandPresentation(false, false, suppressFooter: true);
        _missionDirector.CompleteMission(false, _kills, _headshots, _shotsFired, _shotsHit);
        _hud.ShowSurvivalResult(false, _survivalWave, _survivalEliminations);
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
