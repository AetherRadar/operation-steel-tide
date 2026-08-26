using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private bool _extractionWorldLaunchPending;
    private bool _extractionWorldLaunchWaitObserved;
    private bool _extractionWorldLaunchPauseObserved;

    internal bool ExtractionWorldLaunchPendingForDiagnostics => _extractionWorldLaunchPending;
    internal bool ExtractionWorldLaunchWaitObservedForDiagnostics => _extractionWorldLaunchWaitObserved;
    internal bool ExtractionWorldLaunchPauseObservedForDiagnostics => _extractionWorldLaunchPauseObserved;

    private void PrepareExtractionWorldLaunch()
    {
        if (_extractionWorldLaunchPending)
        {
            return;
        }

        _extractionWorldLaunchPending = true;
        _extractionWorldLaunchWaitObserved = true;
        GetTree().Paused = true;
        _extractionWorldLaunchPauseObserved = GetTree().Paused;
        _player.UiLocked = true;
        _player.DisarmFireInput();
        _player.DisarmMovementInput();
        _player.ProcessMode = ProcessModeEnum.Disabled;
        _missionDirector.ProcessMode = ProcessModeEnum.Disabled;

        foreach (var enemy in GetChildren().OfType<EnemyOperator>())
        {
            enemy.ProcessMode = ProcessModeEnum.Disabled;
            enemy.SetPhysicsProcess(false);
        }
        foreach (var mate in _squadMates.Where(IsInstanceValid))
        {
            mate.ProcessMode = ProcessModeEnum.Disabled;
            mate.SetPhysicsProcess(false);
        }

        _hud.ShowSquadLobby("SYNCHRONIZING OPERATION  //  WORLD READY CHECK");
        _hud.SetSquadLobbyWaiting(
            _squadNetwork.IsHost,
            _squadNetwork.RegisteredExtractionPlayerCount,
            SquadNetwork.ExtractionSquadCapacity,
            canStart: false,
            status: _squadNetwork.IsHost
                ? GameLocalization.Get(
                    "squad_world_wait_all",
                    _languageSetting,
                    "WORLD READY  //  WAITING FOR ALL OPERATORS")
                : GameLocalization.Get(
                    "squad_world_wait_host",
                    _languageSetting,
                    "WORLD READY  //  WAITING FOR HOST"));
    }

    private void ReleaseExtractionWorldLaunch()
    {
        if (!_extractionWorldLaunchPending
            || !_squadNetwork.ExtractionWorldLaunchStarted)
        {
            return;
        }

        _extractionWorldLaunchPending = false;
        GetTree().Paused = false;
        _player.ProcessMode = ProcessModeEnum.Inherit;
        _player.UiLocked = false;
        _player.DisarmFireInput();
        _player.RestoreMovementInput();

        foreach (var enemy in GetChildren().OfType<EnemyOperator>())
        {
            enemy.ProcessMode = ProcessModeEnum.Inherit;
            enemy.SetPhysicsProcess(!_squadNetwork.IsExtractionSession || _squadNetwork.IsHost);
        }
        foreach (var mate in _squadMates.Where(IsInstanceValid))
        {
            mate.ResumeFromExtractionDeployment();
        }
        _missionDirector.ProcessMode = _squadNetwork.IsHost
            ? ProcessModeEnum.Always
            : ProcessModeEnum.Disabled;
    }

    private void CompleteSquadDeploymentPresentation(OperatorRole role)
    {
        if (IsExtractionNetworkMatch && !_squadNetwork.ExtractionWorldLaunchStarted)
        {
            PrepareExtractionWorldLaunch();
            return;
        }

        ReleaseExtractionWorldLaunch();
        _hud.ClearSquadLobbyWaiting();
        _hud.HideSquadLobby();
        _hud.SetSquadOrder(_squadOrder);
        RefreshSquadHud();
        Input.MouseMode = Input.MouseModeEnum.Captured;
        _hud.ShowLocalizedMessage(
            "squad_ready",
            "SQUAD READY  //  F1 FOLLOW  F2 HOLD  F3 MOVE  H SKILL",
            OperatorRoles.Spec(role).Accent);
    }

    private void OnExtractionWorldLaunch()
    {
        if (!IsExtractionNetworkMatch)
        {
            return;
        }
        ReleaseExtractionWorldLaunch();
        if (!_extractionWorldLaunchPending)
        {
            _hud.ClearSquadLobbyWaiting();
            _hud.HideSquadLobby();
            _hud.SetSquadOrder(_squadOrder);
            RefreshSquadHud();
            Input.MouseMode = Input.MouseModeEnum.Captured;
            _hud.ShowLocalizedMessage(
                "squad_ready",
                "SQUAD READY  //  F1 FOLLOW  F2 HOLD  F3 MOVE  H SKILL",
                OperatorRoles.Spec(_player.Role).Accent);
        }
    }
}
