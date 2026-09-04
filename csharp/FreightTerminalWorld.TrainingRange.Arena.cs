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
        return arena;
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
