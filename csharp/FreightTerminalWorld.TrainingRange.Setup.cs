using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private bool _trainingRangeSetupFromGameplay;
    private bool _trainingRangeSetupAwaitingDeploy;

    /// <summary>
    /// Move the player into the dedicated venue before showing configuration.  The
    /// production map remains disabled behind the panel, so the setup screen already
    /// communicates that this is a separate place rather than another mission phase.
    /// </summary>
    private void BeginTrainingRangeSetup()
    {
        var arena = ActivateDedicatedTrainingRangeArena();
        _trainingRangeOrigin = arena.Origin;
        ConfigureTrainingRangeMinimap(arena);
        _player.PrepareTrainingRangeLoadout(arena.PlayerSpawn);
        _player.SelectTrainingRangeWeapon(_trainingRangeWeaponIndex);
        _player.ApplyTrainingRangeAmmoProfile(_trainingRangeAmmoType, _trainingRangeAmmoLevel);
        _player.UiLocked = true;
        _player.DisarmFireInput();
        _player.DisarmMovementInput();
        _missionDirector.ProcessMode = ProcessModeEnum.Disabled;
        _trainingRangeSetupFromGameplay = false;
        _trainingRangeSetupAwaitingDeploy = true;
        _hud.SetTrainingRangeSetupSelections(
            _trainingRangeBotType,
            _trainingRangeBotCount,
            _trainingRangeWeaponIndex,
            _trainingRangeAmmoType,
            _trainingRangeAmmoLevel);
        _hud.ShowTrainingRangeSetup(
            GameLocalization.Get(
                "training_setup_status_ready",
                _languageSetting,
                "SELECT TARGETS  //  SELECT WEAPON  //  SELECT AMMO"),
            fromGameplay: false);
        GetTree().Paused = true;
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    private void OnTrainingRangeSetupOpened(bool fromGameplay)
    {
        _trainingRangeSetupFromGameplay = fromGameplay;
        _trainingRangeSetupAwaitingDeploy = !fromGameplay;
        _player.UiLocked = true;
        _player.DisarmFireInput();
        _player.DisarmMovementInput();
        _missionDirector.ProcessMode = ProcessModeEnum.Disabled;
        GetTree().Paused = true;
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    private void OnTrainingRangeSetupBackRequested()
    {
        var fromGameplay = _trainingRangeSetupFromGameplay;
        _trainingRangeSetupFromGameplay = false;
        _trainingRangeSetupAwaitingDeploy = false;
        if (fromGameplay || _trainingRangeActive)
        {
            // Back cancels an in-game edit.  Restore the last applied payload so
            // reopening the panel cannot show a gun/round/target set that never ran.
            if (_trainingRangeActive)
            {
                _hud.SetTrainingRangeSetupSelections(
                    _trainingRangeBotType,
                    _trainingRangeBotCount,
                    _trainingRangeWeaponIndex,
                    _trainingRangeAmmoType,
                    _trainingRangeAmmoLevel);
            }
            ResumeTrainingRangeGameplay();
            return;
        }

        // Back from the pre-deploy panel returns to the operations office and restores
        // the production map root.  No arena node or extraction actor is destroyed.
        DeactivateDedicatedTrainingRangeArena();
        _trainingRangeOrigin = DeploymentPoint;
        _player.GlobalPosition = DeploymentPoint;
        _player.Rotation = Vector3.Zero;
        _player.UiLocked = true;
        _player.DisarmFireInput();
        _player.DisarmMovementInput();
        _hud.HideTrainingRangeGameplay();
        EnterOperationsOffice();
    }

    private void OnTrainingRangeExitRequested()
    {
        if (!_trainingRangeActive)
        {
            return;
        }

        // Re-entering the office through a scene restart rebuilds the production
        // actors (enemies, squadmates, loot and minimap) instead of leaving any of
        // the range's direct-child targets behind in the mission world.
        RestartMission();
    }

    private void OnTrainingRangeDeployRequested(
        int botType,
        int botCount,
        int weaponIndex,
        int ammoType,
        int ammoLevel)
    {
        var wasActive = _trainingRangeActive;
        _trainingRangeSetupFromGameplay = false;
        _trainingRangeSetupAwaitingDeploy = false;
        if (wasActive)
        {
            ConfigureTrainingRange(botType, botCount, weaponIndex, ammoType, ammoLevel);
            ResumeTrainingRangeGameplay();
            return;
        }

        GetTree().Paused = false;
        StartTrainingRange(botType, botCount, weaponIndex, ammoType, ammoLevel);
    }

    private void ResumeTrainingRangeGameplay()
    {
        if (!_trainingRangeActive)
        {
            return;
        }
        GetTree().Paused = false;
        _player.UiLocked = false;
        _player.RestoreMovementInput();
        _player.DisarmFireInput();
        _missionDirector.ProcessMode = ProcessModeEnum.Disabled;
        _hud.ShowTrainingRangeGameplay(BuildTrainingRangeObjective());
        Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    public bool TrainingRangeSetupIsPending => _trainingRangeSetupAwaitingDeploy;
}
