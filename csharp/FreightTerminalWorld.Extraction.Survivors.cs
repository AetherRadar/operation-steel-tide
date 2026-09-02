using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld : ISurvivorExtractionRuntime
{
    private SurvivorExtractionService? _survivorExtractionService;

    private SurvivorExtractionService SurvivorExtraction
        => _survivorExtractionService ??= new SurvivorExtractionService(this);

    internal bool SurvivorExtractionTakeoverForDiagnostics
        => SurvivorExtraction.TakeoverActive;

    private bool IsSurvivorExtractionTakeoverActive
        => SurvivorExtraction.TakeoverActive;

    private bool TryUpdateSurvivorExtractionSequence(float delta)
        => SurvivorExtraction.Update(delta);

    private (int Ready, int Total) CountSurvivorExtractionSquad()
        => SurvivorExtraction.CountReady();

    private void BoardSurvivorExtractionSquad()
        => SurvivorExtraction.BoardReadyMates();

    bool ISurvivorExtractionRuntime.TakeoverEligible
        // Network extraction has a host-authored human outcome protocol and must
        // not infer an AI-only departure independently on either peer.
        => !_demolitionMode
            && !IsExtractionNetworkMatch
            && _squadDeployed
            && _localPlayerEliminated
            && IsInstanceValid(_player)
            && _player.IsDead;

    bool ISurvivorExtractionRuntime.MissionEnded => _missionEnded;
    bool ISurvivorExtractionRuntime.DeparturePlaying => _extractionDeparturePlaying;
    bool ISurvivorExtractionRuntime.HasActionableRevive
        => HasActionableAiReviveForSurvivorExtraction();
    bool ISurvivorExtractionRuntime.CountdownActive => _extractionCountdownActive;
    float ISurvivorExtractionRuntime.CountdownRemaining => _extractionRemaining;
    bool ISurvivorExtractionRuntime.AircraftBoardingReady
        => IsActiveExtractionTransportReady();
    int ISurvivorExtractionRuntime.PassengerSeatCount
        => _extractionAircraft is not null && IsInstanceValid(_extractionAircraft)
            ? _extractionAircraft.PassengerSeatCount
            : 0;
    Vector3 ISurvivorExtractionRuntime.ExtractionPoint => ExtractionPoint;
    IReadOnlyList<SquadMate> ISurvivorExtractionRuntime.SquadMates => _squadMates;

    bool ISurvivorExtractionRuntime.IsInsideExtractionZone(Vector3 position)
        => IsInsideExtractionZone(position);

    void ISurvivorExtractionRuntime.ResetPlayerExtractionCall()
    {
        _extractionPlayerInside = false;
        if (!_extractionCountdownActive)
        {
            return;
        }
        _extractionCountdownActive = false;
        _extractionRemaining = CurrentExtractionCountdownDuration();
        _hud.HideExtractionCountdown();
        AbortActiveExtractionTransport();
    }

    void ISurvivorExtractionRuntime.PauseCountdownForRescue()
    {
        if (!_extractionCountdownActive)
        {
            return;
        }
        _extractionCountdownActive = false;
        _extractionRemaining = CurrentExtractionCountdownDuration();
        _hud.HideExtractionCountdown();
        AbortActiveExtractionTransport();
        _hud.ShowLocalizedMessage(
            "survivor_extraction_rescue",
            "AI EXTRACTION PAUSED  //  RESCUING SQUADMATE",
            OperatorRoles.Spec(OperatorRole.Medic).Accent);
    }

    void ISurvivorExtractionRuntime.BeginCountdown()
    {
        if (_extractionCountdownActive || _missionEnded)
        {
            return;
        }
        if (UsesOrbitalComplexTideGateExtraction
            && !OrbitalComplexExtraction.CanExtract(_objectiveStage))
        {
            return;
        }
        _extractionCountdownActive = true;
        _extractionRemaining = CurrentExtractionCountdownDuration();
        _missionDirector.ExitDeploymentZone();
        BeginActiveExtractionTransport();
        UpdateExtractionHud();
        if (UsesOrbitalComplexTideGateExtraction)
        {
            _hud.ShowLocalizedMessage(
                OrbitalComplexExtraction.StatusLocalizationKey(_objectiveStage),
                _objectiveStage >= 2
                    ? "AI SURVIVORS CYCLING THE TIDE GATE  //  EXPRESS HOLD"
                    : "AI SURVIVORS CYCLING THE TIDE GATE  //  EMERGENCY HOLD",
                new Color(0.35f, 0.88f, 0.78f));
            return;
        }
        _hud.ShowLocalizedMessage(
            ObjectivesIncompleteForExtraction()
                ? "survivor_extraction_cold"
                : "survivor_extraction_inbound",
            ObjectivesIncompleteForExtraction()
                ? "AI SURVIVORS CALLING COLD EXTRACTION  //  EXTENDED HOLD"
                : "AI SURVIVORS CALLING EXTRACTION  //  HOLDING THE GREEN ZONE",
            ObjectivesIncompleteForExtraction()
                ? new Color(1.0f, 0.62f, 0.26f)
                : new Color(0.3f, 1.0f, 0.66f));
    }

    void ISurvivorExtractionRuntime.AdvanceCountdown(float delta)
    {
        _extractionRemaining = Mathf.Max(0.0f, _extractionRemaining - delta);
        UpdateExtractionHud();
    }

    void ISurvivorExtractionRuntime.CompleteExtraction()
        => CompleteExtractionSequence();

    void ISurvivorExtractionRuntime.BeginRally()
    {
        _squadOrder = SquadOrder.Move;
        _squadMovePoint = ExtractionPoint;
    }

    void ISurvivorExtractionRuntime.SetRallyDestination(
        SquadMate mate,
        Vector3 destination)
        => mate.SetOrder(SquadOrder.Move, destination);

    void ISurvivorExtractionRuntime.FinishRally(bool orderChanged)
    {
        if (orderChanged)
        {
            _hud.SetSquadOrder(SquadOrder.Move);
        }
    }

    void ISurvivorExtractionRuntime.ResetBoardingCount()
        => _extractionBoardedSquadmates = 0;

    void ISurvivorExtractionRuntime.BoardMate(SquadMate mate, int seatIndex)
    {
        if (_extractionAircraft is null || !IsInstanceValid(_extractionAircraft))
        {
            return;
        }
        var seat = seatIndex == 0
            ? _extractionAircraft.PlayerSeat
            : _extractionAircraft.SquadSeat(seatIndex - 1);
        ClearSquadNavigation(mate);
        mate.BoardExtractionSeat(seat);
        _extractionBoardedSquadmates = seatIndex + 1;
    }
}
