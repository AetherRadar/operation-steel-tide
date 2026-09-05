using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    /// <summary>
    /// The three authored benches in the range are real interaction points, not just
    /// decorative labels.  F opens the same focused configuration panel used before
    /// deployment, so players can change a gun, round, or target set without leaving
    /// the dedicated venue.
    /// </summary>
    private void UpdateTrainingRangeInteraction(float delta)
    {
        if (!_trainingRangeActive
            || _trainingRangeArena is null
            || !_trainingRangeArena.Active
            || _hud.IsTrainingRangeSetupVisible
            || _player.IsDead)
        {
            _hud.SetInteraction(string.Empty, 0.0f, false);
            return;
        }

        _trainingRangeInteractionCooldown = Mathf.Max(
            0.0f,
            _trainingRangeInteractionCooldown - delta);
        if (!Input.IsActionPressed(GameInputActions.Interact))
        {
            _interactReleaseRequired = false;
        }

        TrainingRangeStation? nearest = null;
        var nearestDistance = float.PositiveInfinity;
        foreach (var station in _trainingRangeArena.Stations)
        {
            var distance = _player.GlobalPosition.DistanceTo(station.Position);
            if (distance <= station.Radius + 0.9f && distance < nearestDistance)
            {
                nearest = station;
                nearestDistance = distance;
            }
        }

        if (nearest is null)
        {
            _hud.SetInteraction(string.Empty, 0.0f, false);
            return;
        }

        var stationLabel = TrainingRangeStationLabel(nearest.Value.Kind);
        _hud.SetInteraction(stationLabel, -1.0f, true);
        if (_trainingRangeInteractionCooldown > 0.0f
            || _interactReleaseRequired
            || !Input.IsActionJustPressed(GameInputActions.Interact))
        {
            return;
        }

        _interactReleaseRequired = true;
        _trainingRangeInteractionCooldown = 0.28f;
        _hud.SetTrainingRangeSetupSelections(
            _trainingRangeBotType,
            _trainingRangeBotCount,
            _trainingRangeWeaponIndex,
            _trainingRangeAmmoType,
            _trainingRangeAmmoLevel);
        _hud.ShowTrainingRangeStation(
            (int)nearest.Value.Kind,
            stationLabel,
            nearest.Value.Kind == TrainingRangeStationKind.Weapon
                ? _player.EquippedWeapon.Clone()
                : null);
        _player.UiLocked = true;
        _player.DisarmFireInput();
        _player.DisarmMovementInput();
        GetTree().Paused = true;
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    private string TrainingRangeStationLabel(TrainingRangeStationKind kind)
        => kind switch
        {
            TrainingRangeStationKind.Weapon => GameLocalization.Get(
                "training_station_weapon",
                _languageSetting,
                "ARMORY  //  WEAPON SELECT"),
            TrainingRangeStationKind.Ammunition => GameLocalization.Get(
                "training_station_ammo",
                _languageSetting,
                "AMMO BENCH  //  ROUND SELECT"),
            _ => GameLocalization.Get(
                "training_station_bot",
                _languageSetting,
                "BOT CONTROL  //  TARGET SET")
        };
}
