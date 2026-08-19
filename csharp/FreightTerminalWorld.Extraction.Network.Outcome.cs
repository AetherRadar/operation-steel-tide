using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private bool _networkExtractionDepartureStarted;

    private void ApplyExtractionLocalPlayerState(
        float health,
        bool down,
        bool bodyBag,
        bool reviveUsed)
    {
        var wasDown = _localPlayerDowned;
        var wasEliminated = _localPlayerEliminated;
        _player.ApplyExtractionNetworkHealth(health, down || bodyBag, reviveUsed || bodyBag);
        if (bodyBag)
        {
            _localPlayerDowned = false;
            _localPlayerEliminated = true;
            _player.UiLocked = true;
            _player.DisarmFireInput();
            _player.DisarmMovementInput();
            _hud.HideDownedState();
            if (!wasEliminated)
            {
                BeginSquadMateView();
            }
            return;
        }
        if (down)
        {
            _localPlayerDowned = true;
            _localPlayerEliminated = false;
            _player.UiLocked = true;
            _player.DisarmFireInput();
            _player.DisarmMovementInput();
            if (!wasDown)
            {
                BeginSquadMateView();
                _hud.ShowDownedState(22.0f);
            }
            return;
        }
        if (wasDown || wasEliminated)
        {
            OnLocalPlayerRevived();
        }
    }

    private void ApplyExtractionNetworkOutcome(ExtractionMissionNetworkState state)
    {
        if (!IsExtractionNetworkClient || !state.MissionEnded)
        {
            return;
        }
        if (!state.MissionSucceeded)
        {
            if (_hud.IsMissionResultVisible)
            {
                return;
            }
            LockLootForMissionTransition(Input.MouseModeEnum.Visible);
            _hud.HideDownedState();
            _missionDirector.CompleteMission(false, _kills, _headshots, _shotsFired, _shotsHit);
            _hud.ShowResult(false);
            return;
        }
        if (state.ExtractionDeparturePlaying)
        {
            BeginNetworkExtractionDeparture();
            return;
        }
        FinishNetworkExtractionMission();
    }

    private void BeginNetworkExtractionDeparture()
    {
        if (_networkExtractionDepartureStarted)
        {
            return;
        }
        _networkExtractionDepartureStarted = true;
        _extractionDeparturePlaying = true;
        LockLootForMissionTransition(Input.MouseModeEnum.Captured);
        _player.EjectFromVehicleIfAny();
        _hud.HideExtractionCountdown();
        var aircraft = _extractionAircraft;
        if (aircraft is null || !IsInstanceValid(aircraft))
        {
            return;
        }
        BoardExtractionSquad();
        aircraft.BeginTransferTo(OperationsOfficeHelipad);
        aircraft.CinematicCamera.MakeCurrent();
        _hud.SetExtractionCinematicVisible(true);
    }

    private void FinishNetworkExtractionMission()
    {
        if (_hud.IsMissionResultVisible)
        {
            return;
        }
        _hud.SetExtractionCinematicVisible(false);
        _player.IsDead = true;
        Input.MouseMode = Input.MouseModeEnum.Visible;
        _missionDirector.CompleteMission(true, _kills, _headshots, _shotsFired, _shotsHit);
        var ranks = BuildExtractionLootRanking();
        var progression = CommitExtractionValue();
        _hud.ShowResult(true, ranks, progression.ExtractedValue, progression.Wallet, progression.Saved);
        _extractionDeparturePlaying = false;
    }
}
