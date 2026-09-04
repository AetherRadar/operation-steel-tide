using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private TrainingRangeArenaRuntime? _trainingRangeArena;

    /// <summary>Returns the lazily-built dedicated range scene.</summary>
    public TrainingRangeArenaRuntime? TrainingRangeArena => _trainingRangeArena;

    public bool TrainingRangeArenaReady
        => _trainingRangeArena is not null
        && GodotObject.IsInstanceValid(_trainingRangeArena.Root);

    /// <summary>
    /// Build the arena once and keep it inactive until the training mode is entered.
    /// Keeping construction lazy avoids adding its GLB instances to ordinary extraction
    /// and demolition captures, while still making subsequent mode switches instant.
    /// </summary>
    private TrainingRangeArenaRuntime EnsureTrainingRangeArena()
    {
        if (_trainingRangeArena is not null
            && GodotObject.IsInstanceValid(_trainingRangeArena.Root))
        {
            return _trainingRangeArena;
        }

        _trainingRangeArena = new TrainingRangeArenaBuilder().Build(this);
        return _trainingRangeArena;
    }

    /// <summary>
    /// Activates the remote arena and suspends the production map root.  No extraction
    /// or demolition geometry is moved or destroyed; both roots can be restored intact
    /// when a caller leaves the range or reloads the mission.
    /// </summary>
    private TrainingRangeArenaRuntime ActivateDedicatedTrainingRangeArena()
    {
        var arena = EnsureTrainingRangeArena();
        if (IsInstanceValid(_levelRoot))
        {
            _levelRoot.Visible = false;
            _levelRoot.ProcessMode = Node.ProcessModeEnum.Disabled;
        }
        if (IsInstanceValid(_extractionMarker))
        {
            _extractionMarker.Visible = false;
        }
        if (IsInstanceValid(_aircraft))
        {
            _aircraft!.Visible = false;
            _aircraft.SetPhysicsProcess(false);
        }
        arena.SetActive(true);
        LocalizeTrainingRangeArenaLabels(arena);
        return arena;
    }

    private void LocalizeTrainingRangeArenaLabels(TrainingRangeArenaRuntime arena)
    {
        SetTrainingRangeLabel(
            arena,
            "WeaponStationLabel",
            GameLocalization.Get("training_station_weapon", _languageSetting, "ARMORY  //  WEAPON SELECT"));
        SetTrainingRangeLabel(
            arena,
            "AmmoStationLabel",
            GameLocalization.Get("training_station_ammo", _languageSetting, "AMMO BENCH  //  ROUND SELECT"));
        SetTrainingRangeLabel(
            arena,
            "BotStationLabel",
            GameLocalization.Get("training_station_bot", _languageSetting, "BOT CONTROL  //  SET TARGETS"));
        SetTrainingRangeLabel(
            arena,
            "RangeTitle",
            GameLocalization.Get("training_range_status", _languageSetting, "TRAINING RANGE  //  LIVE FIRE"));
        SetTrainingRangeLabel(
            arena,
            "RangeRule",
            GameLocalization.Get(
                "training_range_rule",
                _languageSetting,
                "SELECT  →  LOAD  →  FIRE  →  RESET"));
        SetTrainingRangeLabel(
            arena,
            "RangeSpawnInstruction",
            GameLocalization.Get(
                "training_range_spawn_instruction",
                _languageSetting,
                "F  LOADOUT  //  FIRE LANES AHEAD"));
        SetTrainingRangeLabel(
            arena,
            "RangeFireLineLabel",
            GameLocalization.Get("training_range_fire_line", _languageSetting, "FIRE LINE"));
        SetTrainingRangeLabel(
            arena,
            "RangeBackstopLabel",
            GameLocalization.Get(
                "training_range_backstop",
                _languageSetting,
                "LIVE FIRE  //  BACKSTOP"));
        SetTrainingRangeLabel(
            arena,
            "RangeTargetHeader",
            GameLocalization.Get(
                "training_range_target_header",
                _languageSetting,
                "TARGET WALL  //  LANES 01-06"));
        for (var index = 1; index <= 6; index++)
        {
            SetTrainingRangeLabel(
                arena,
                $"LaneLabel_{index:00}",
                GameLocalization.Format(
                    "training_range_lane_label",
                    _languageSetting,
                    $"LANE {index:00}  //  BOT TARGET",
                    index));
        }
    }

    private static void SetTrainingRangeLabel(
        TrainingRangeArenaRuntime arena,
        string nodeName,
        string text)
    {
        var label = arena.Root.GetNodeOrNull<Label3D>(nodeName);
        if (label is not null)
        {
            label.Text = text;
        }
    }

    /// <summary>Deactivate the dedicated root and restore the production map root.</summary>
    private void DeactivateDedicatedTrainingRangeArena()
    {
        if (_trainingRangeArena is not null
            && GodotObject.IsInstanceValid(_trainingRangeArena.Root))
        {
            _trainingRangeArena.SetActive(false);
        }
        if (IsInstanceValid(_levelRoot))
        {
            _levelRoot.Visible = true;
            _levelRoot.ProcessMode = Node.ProcessModeEnum.Inherit;
        }
        if (IsInstanceValid(_player) && IsInstanceValid(_hud))
        {
            ConfigureTacticalMinimap();
        }
    }

    /// <summary>Diagnostic contract for mode-switch tests.</summary>
    public bool DedicatedTrainingRangeIsIsolated()
        => _trainingRangeActive
        && _trainingRangeArena is not null
        && _trainingRangeArena.Active
        && GodotObject.IsInstanceValid(_trainingRangeArena.Root)
        && !_levelRoot.Visible
        && _levelRoot.ProcessMode == Node.ProcessModeEnum.Disabled
        && _trainingRangeArena.CollisionLifecycleIsValid();
}
