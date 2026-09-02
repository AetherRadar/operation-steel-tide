using System;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private const float ExtractionCountdownDuration = 12.0f;
    private const float PriorityExtractionSeconds = 9.0f;
    private const float PartialExtractionSeconds = 15.0f;
    private const float ColdExtractionSeconds = 18.0f;
    private const float ExtractionZoneRadius = 7.0f;
    private static readonly OrbitalComplexExtractionStrategy OrbitalComplexExtraction = new();
    private ExtractionAircraft? _extractionAircraft;
    private bool _extractionCountdownActive;
    private bool _extractionPlayerInside;
    private bool _orbitalExtractionLockedPromptShownWhileInside;
    private bool _extractionDeparturePlaying;
    private bool _extractionMissionSucceeded;
    private bool _skipExtractionCinematicForValidation;
    private int _extractionBoardedSquadmates;
    private float _extractionRemaining = ExtractionCountdownDuration;
    private SquadOrder _preExtractionSquadOrder = SquadOrder.Follow;
    private Vector3 _preExtractionSquadMovePoint;

    public bool IsExtractionCountdownActive => _extractionCountdownActive;
    public bool IsExtractionDeparturePlaying => _extractionDeparturePlaying;
    public int ExtractionBoardedSquadmateCount => _extractionBoardedSquadmates;
    public bool IsExtractionAtOperationsOffice => _extractionAircraft?.DestinationReached == true;

    private bool UsesOrbitalComplexTideGateExtraction
        => string.Equals(
            _activeRuntimeMapId,
            DeploymentMapCatalog.OrbitalComplexId,
            StringComparison.OrdinalIgnoreCase);

    private void BuildExtractionAircraft()
    {
        if (UsesOrbitalComplexTideGateExtraction)
        {
            _extractionAircraft = null;
            return;
        }

        _extractionAircraft = new ExtractionAircraft
        {
            Name = "FriendlyExtractionTiltRotor",
            PadPosition = ExtractionPoint
        };
        _levelRoot.AddChild(_extractionAircraft);
    }

    private void TryBeginExtractionSequence(Node3D body)
    {
        if (body != _player || _missionEnded || _demolitionMode || IsExtractionNetworkClient
            || _localPlayerEliminated || _player.IsDead)
        {
            return;
        }

        _extractionPlayerInside = true;
        BeginExtractionCountdown();
    }

    private void OnExtractionExited(Node3D body)
    {
        if (body != _player)
        {
            return;
        }
        _orbitalExtractionLockedPromptShownWhileInside = false;
        if (IsExtractionNetworkClient)
        {
            return;
        }
        if (IsSurvivorExtractionTakeoverActive)
        {
            return;
        }
        _extractionPlayerInside = false;
        if (_extractionCountdownActive)
        {
            CancelExtractionCountdown();
        }
    }

    private void UpdateExtractionSequence(float delta)
    {
        if (_demolitionMode || IsExtractionNetworkClient)
        {
            return;
        }
        if (!IsInstanceValid(_player) || !_player.IsInsideTree()
            || !IsInstanceValid(_hud) || !_hud.IsInsideTree())
        {
            return;
        }
        if (TryUpdateSurvivorExtractionSequence(delta))
        {
            return;
        }

        _extractionPlayerInside = IsPlayerInsideExtractionZone();
        if (!_extractionPlayerInside)
        {
            _orbitalExtractionLockedPromptShownWhileInside = false;
        }
        if (_missionEnded || _extractionDeparturePlaying)
        {
            return;
        }

        if (_extractionPlayerInside && !_extractionCountdownActive && !_localPlayerDowned && !_player.IsDead)
        {
            BeginExtractionCountdown();
        }
        if (!_extractionCountdownActive)
        {
            return;
        }
        if (!_extractionPlayerInside || _localPlayerDowned || _player.IsDead)
        {
            CancelExtractionCountdown();
            return;
        }

        RallySquadToExtraction();
        _extractionRemaining = Mathf.Max(0.0f, _extractionRemaining - delta);
        UpdateExtractionHud();
        if (_extractionRemaining <= 0.0f && IsActiveExtractionTransportReady())
        {
            CompleteExtractionSequence();
        }
    }

    private bool IsPlayerInsideExtractionZone()
    {
        if (IsInsideExtractionZone(_player.GlobalPosition))
        {
            return true;
        }
        return IsExtractionNetworkMatch && _squadNetwork.IsHost
            && _squadMates.Any(mate => IsInstanceValid(mate)
                && mate.IsHumanProxy
                && !mate.IsDowned
                && !mate.IsBodyBag
                && IsInsideExtractionZone(mate.GlobalPosition));
    }

    private bool IsInsideExtractionZone(Vector3 position)
    {
        var offset = position - ExtractionPoint;
        var horizontalSquared = offset.X * offset.X + offset.Z * offset.Z;
        var radius = UsesOrbitalComplexTideGateExtraction
            ? OrbitalComplexRuntimeExtractionRadius
            : ExtractionZoneRadius;
        return horizontalSquared <= radius * radius
            && offset.Y >= -1.5f
            && offset.Y <= 4.2f;
    }

    /// <summary>
    /// Objective-coupled hold time: skipping objectives forces a longer cold hold while a
    /// full clear earns a fast pickup. Maps without objective terminals keep the base time.
    /// </summary>
    private static float ExtractionCountdownForRemainingObjectives(int totalObjectives, int completedObjectives)
    {
        if (totalObjectives <= 0)
        {
            return ExtractionCountdownDuration;
        }
        var remaining = totalObjectives - Mathf.Clamp(completedObjectives, 0, totalObjectives);
        return remaining switch
        {
            0 => PriorityExtractionSeconds,
            1 => PartialExtractionSeconds,
            _ => ColdExtractionSeconds
        };
    }

    private float CurrentExtractionCountdownDuration()
        => UsesOrbitalComplexTideGateExtraction
            ? OrbitalComplexExtraction.CountdownSeconds(_objectiveStage)
            : ExtractionCountdownForRemainingObjectives(_objectiveTerminals.Count, _objectiveStage);

    private bool IsActiveExtractionTransportReady()
        => UsesOrbitalComplexTideGateExtraction
            ? OrbitalComplexExtraction.TransportReady(_objectiveStage)
            : _extractionAircraft?.BoardingReady == true;

    private void BeginActiveExtractionTransport()
    {
        if (!UsesOrbitalComplexTideGateExtraction)
        {
            _extractionAircraft?.BeginInbound();
        }
    }

    private void AbortActiveExtractionTransport()
    {
        if (!UsesOrbitalComplexTideGateExtraction)
        {
            _extractionAircraft?.AbortPickup();
        }
    }

    private bool ObjectivesIncompleteForExtraction()
        => _objectiveTerminals.Count > 0
            && Mathf.Clamp(_objectiveStage, 0, _objectiveTerminals.Count) < _objectiveTerminals.Count;

    private void BeginExtractionCountdown()
    {
        if (_extractionCountdownActive || _missionEnded)
        {
            return;
        }

        if (UsesOrbitalComplexTideGateExtraction
            && !OrbitalComplexExtraction.CanExtract(_objectiveStage))
        {
            ShowOrbitalExtractionLockedPrompt();
            return;
        }

        _extractionCountdownActive = true;
        _extractionRemaining = CurrentExtractionCountdownDuration();
        _missionDirector.ExitDeploymentZone();
        _preExtractionSquadOrder = _squadOrder;
        _preExtractionSquadMovePoint = _squadMovePoint;
        BeginActiveExtractionTransport();
        RallySquadToExtraction();
        UpdateExtractionHud();
        if (UsesOrbitalComplexTideGateExtraction)
        {
            ShowOrbitalExtractionPowerMessage();
        }
        else if (ObjectivesIncompleteForExtraction())
        {
            _hud.ShowLocalizedMessage(
                "extraction_cold",
                "COLD EXTRACTION  //  OBJECTIVES INCOMPLETE  //  EXTENDED HOLD",
                new Color(1.0f, 0.62f, 0.26f));
        }
        else
        {
            _hud.ShowLocalizedMessage(
                "extraction_inbound",
                "FRIENDLY TILT-ROTOR INBOUND  //  HOLD THE ZONE",
                new Color(0.3f, 1.0f, 0.66f));
        }
    }

    private void ShowOrbitalExtractionLockedPrompt()
    {
        if (_orbitalExtractionLockedPromptShownWhileInside)
        {
            return;
        }

        _orbitalExtractionLockedPromptShownWhileInside = true;
        _hud.ShowLocalizedMessage(
            OrbitalComplexExtraction.StatusLocalizationKey(_objectiveStage),
            "TIDE GATE OFFLINE  //  RESTORE EMERGENCY POWER",
            new Color(1.0f, 0.48f, 0.22f));
    }

    private void ShowOrbitalExtractionPowerMessage()
    {
        var fullPower = _objectiveStage >= 2;
        _hud.ShowLocalizedMessage(
            OrbitalComplexExtraction.StatusLocalizationKey(_objectiveStage),
            fullPower
                ? "FULL POWER  //  TIDE GATE EXPRESS CYCLE  //  HOLD 9 SECONDS"
                : "EMERGENCY POWER  //  TIDE GATE CYCLING  //  HOLD 18 SECONDS",
            fullPower
                ? new Color(0.3f, 1.0f, 0.66f)
                : new Color(0.35f, 0.8f, 1.0f));
    }

    private void CancelExtractionCountdown()
    {
        _extractionCountdownActive = false;
        _extractionRemaining = CurrentExtractionCountdownDuration();
        _hud.HideExtractionCountdown();
        AbortActiveExtractionTransport();
        RestoreSquadOrderAfterExtractionAbort();
        _hud.ShowLocalizedMessage(
            "extraction_aborted",
            "EXTRACTION ABORTED  //  RETURN TO THE GREEN ZONE TO CALL AGAIN",
            new Color(1.0f, 0.56f, 0.22f));
    }

    private void RallySquadToExtraction()
    {
        var readyOffsets = new[]
        {
            new Vector3(-2.2f, 0.1f, 2.0f),
            new Vector3(2.2f, 0.1f, 2.0f),
            new Vector3(0.0f, 0.1f, 3.2f)
        };
        _squadOrder = SquadOrder.Move;
        _squadMovePoint = ExtractionPoint;
        for (var i = 0; i < _squadMates.Count; i++)
        {
            var mate = _squadMates[i];
            if (!IsInstanceValid(mate) || !mate.IsInsideTree() || mate.IsDowned || mate.IsBodyBag)
            {
                continue;
            }
            mate.SetOrder(SquadOrder.Move, ExtractionPoint + readyOffsets[Mathf.Min(i, readyOffsets.Length - 1)]);
        }
        _hud.SetSquadOrder(SquadOrder.Move);
    }

    private void RestoreSquadOrderAfterExtractionAbort()
    {
        _squadOrder = _preExtractionSquadOrder;
        _squadMovePoint = _preExtractionSquadMovePoint;
        foreach (var mate in _squadMates)
        {
            if (IsInstanceValid(mate) && mate.IsInsideTree() && !mate.IsBodyBag)
            {
                var target = _squadOrder == SquadOrder.Hold ? mate.GlobalPosition : _squadMovePoint;
                mate.SetOrder(_squadOrder, target);
            }
        }
        _hud.SetSquadOrder(_squadOrder);
    }

    private void UpdateExtractionHud()
    {
        var (ready, total) = CountExtractionSquad();
        _hud.SetExtractionCountdown(
            _extractionRemaining,
            CurrentExtractionCountdownDuration(),
            IsActiveExtractionTransportReady(),
            ready,
            total);
    }

    private (int Ready, int Total) CountExtractionSquad()
    {
        if (IsSurvivorExtractionTakeoverActive)
        {
            return CountSurvivorExtractionSquad();
        }
        var ready = 1;
        var total = 1;
        foreach (var mate in _squadMates)
        {
            if (!IsInstanceValid(mate) || !mate.IsInsideTree() || mate.IsBodyBag)
            {
                continue;
            }
            total++;
            if (!mate.IsDowned && HorizontalDistance(mate.GlobalPosition, ExtractionPoint) <= 10.5f)
            {
                ready++;
            }
        }
        return (ready, total);
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        var dx = a.X - b.X;
        var dz = a.Z - b.Z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    private async void CompleteExtractionSequence()
    {
        if (_missionEnded)
        {
            return;
        }

        _extractionCountdownActive = false;
        _extractionDeparturePlaying = true;
        _extractionMissionSucceeded = true;
        _missionEnded = true;
        LockLootForMissionTransition(Input.MouseModeEnum.Captured);
        _player.EjectFromVehicleIfAny();
        _player.UiLocked = true;
        _player.DisarmFireInput();
        _player.DisarmMovementInput();
        _hud.HideExtractionCountdown();
        var aircraft = _extractionAircraft;
        if (aircraft is null || !IsInstanceValid(aircraft))
        {
            FinishExtractionMission();
            return;
        }

        BoardExtractionSquad();
        aircraft.BeginTransferTo(OperationsOfficeHelipad);
        aircraft.CinematicCamera.MakeCurrent();
        _hud.SetExtractionCinematicVisible(true);
        _hud.ShowLocalizedMessage(
            "extraction_departing",
            "SQUAD ABOARD  //  TRANSFER TO OPERATIONS OFFICE",
            new Color(0.36f, 1.0f, 0.7f));

        if (_skipExtractionCinematicForValidation)
        {
            aircraft.AdvanceForValidation(ExtractionAircraft.TransferDuration + 0.1f);
        }
        while (IsInsideTree() && IsInstanceValid(aircraft) && !aircraft.DestinationReached)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        if (!IsInsideTree())
        {
            return;
        }
        if (!IsInstanceValid(aircraft))
        {
            FinishExtractionMission();
            return;
        }

        _hud.ShowLocalizedMessage(
            "extraction_arrived",
            "OPERATIONS OFFICE REACHED  //  TOUCHDOWN CONFIRMED",
            new Color(0.4f, 0.92f, 0.74f));
        if (!_skipExtractionCinematicForValidation)
        {
            await ToSignal(GetTree().CreateTimer(1.15f), SceneTreeTimer.SignalName.Timeout);
            if (!IsInsideTree())
            {
                return;
            }
        }
        FinishExtractionMission();
    }

    private void BoardExtractionSquad()
    {
        if (IsSurvivorExtractionTakeoverActive)
        {
            BoardSurvivorExtractionSquad();
            return;
        }
        _extractionBoardedSquadmates = 0;
        if (_extractionAircraft is null || !IsInstanceValid(_extractionAircraft))
        {
            return;
        }

        _extractionAircraft.ShowPlayerPassenger(OperatorRoles.Spec(_player.Role).Accent);
        _player.BoardExtractionSeat(_extractionAircraft.PlayerSeat);
        foreach (var mate in _squadMates)
        {
            if (!IsInstanceValid(mate) || !mate.IsInsideTree() || mate.IsDowned || mate.IsBodyBag)
            {
                continue;
            }
            if (HorizontalDistance(mate.GlobalPosition, ExtractionPoint) <= 14.0f
                && _extractionBoardedSquadmates < _extractionAircraft.PassengerSeatCount - 1)
            {
                ClearSquadNavigation(mate);
                mate.BoardExtractionSeat(_extractionAircraft.SquadSeat(_extractionBoardedSquadmates));
                _extractionBoardedSquadmates++;
            }
        }
    }

    private void FinishExtractionMission()
    {
        _hud.SetExtractionCinematicVisible(false);
        _hud.SetSquadCommandPresentation(false, false, suppressFooter: true);
        _player.IsDead = true;
        Input.MouseMode = Input.MouseModeEnum.Visible;
        _missionDirector.CompleteMission(true, _kills, _headshots, _shotsFired, _shotsHit);
        var ranks = BuildExtractionLootRanking();
        var progression = CommitExtractionValue();
        var objectiveMultiplier = ObjectiveExtractionMultiplier();
        if (objectiveMultiplier > 1.0f)
        {
            _hud.ShowLocalizedMessage(
                "extraction_objective_bonus",
                $"OBJECTIVE BONUS  //  PAYOUT x{objectiveMultiplier:0.00}",
                new Color(0.35f, 0.95f, 0.6f));
        }
        _hud.ShowResult(true, ranks, progression.ExtractedValue, progression.Wallet, progression.Saved);
        _extractionDeparturePlaying = false;
    }

    private async void ValidateExtractionSequence()
    {
        await WaitFrames(6);
        foreach (var enemy in _enemies)
        {
            if (IsInstanceValid(enemy))
            {
                enemy.ProcessMode = ProcessModeEnum.Disabled;
            }
        }
        foreach (var squad in _hostileSquads)
        {
            foreach (var member in squad.Members)
            {
                if (IsInstanceValid(member))
                {
                    member.ProcessMode = ProcessModeEnum.Disabled;
                }
            }
        }
        if (IsInstanceValid(_aircraft))
        {
            _aircraft.SetPhysicsProcess(false);
        }

        _objectiveStage = 0;
        var coldDuration = CurrentExtractionCountdownDuration();
        var priorityDuration = ExtractionCountdownForRemainingObjectives(
            _objectiveTerminals.Count,
            _objectiveTerminals.Count);
        var objectiveScaled = coldDuration > ExtractionCountdownDuration
            && priorityDuration < ExtractionCountdownDuration
            && priorityDuration < coldDuration;
        var orbitalRulesReady = !OrbitalComplexExtraction.CanExtract(0)
            && Mathf.IsZeroApprox(OrbitalComplexExtraction.CountdownSeconds(0))
            && !OrbitalComplexExtraction.TransportReady(0)
            && OrbitalComplexExtraction.StatusLocalizationKey(0) == "falltide_extract_locked"
            && OrbitalComplexExtraction.CanExtract(1)
            && Mathf.IsEqualApprox(
                OrbitalComplexExtraction.CountdownSeconds(1),
                OrbitalComplexExtractionStrategy.EmergencyPowerCountdownSeconds)
            && OrbitalComplexExtraction.TransportReady(1)
            && OrbitalComplexExtraction.StatusLocalizationKey(1) == "falltide_extract_emergency"
            && OrbitalComplexExtraction.CanExtract(2)
            && Mathf.IsEqualApprox(
                OrbitalComplexExtraction.CountdownSeconds(2),
                OrbitalComplexExtractionStrategy.FullPowerCountdownSeconds)
            && OrbitalComplexExtraction.TransportReady(2)
            && OrbitalComplexExtraction.StatusLocalizationKey(2) == "falltide_extract_full";
        _missionDirector.ExitDeploymentZone();
        _missionDirector.RaiseConfirmedAlarm();
        _player.GlobalPosition = ExtractionPoint + new Vector3(0, 0.12f, 1.0f);
        TryBeginExtractionSequence(_player);
        var earlyCallPhase = _missionPhase;
        var entryStartedImmediately = _extractionCountdownActive
            && Mathf.IsEqualApprox(_extractionRemaining, coldDuration)
            && _hud.IsExtractionCountdownVisible
            && !IsPlayerProtected();
        var combatPhasePreserved = earlyCallPhase == "COMBAT";
        UpdateExtractionSequence(3.0f);
        var countdownStarted = _extractionCountdownActive
            && _extractionRemaining < coldDuration
            && _hud.IsExtractionCountdownVisible;
        var aircraftInbound = _extractionAircraft?.Phase == ExtractionAircraftPhase.Inbound;
        var authoredVisual = _extractionAircraft?.UsesAuthoredVisual == true;

        _player.GlobalPosition = ExtractionPoint + new Vector3(ExtractionZoneRadius + 2.0f, 0.12f, 0);
        UpdateExtractionSequence(0.1f);
        var leaveReset = !_extractionCountdownActive
            && Mathf.IsEqualApprox(_extractionRemaining, coldDuration)
            && !_hud.IsExtractionCountdownVisible
            && _missionPhase == earlyCallPhase;

        _player.GlobalPosition = ExtractionPoint + new Vector3(0, 0.12f, 1.0f);
        for (var i = 0; i < _squadMates.Count; i++)
        {
            var mate = _squadMates[i];
            if (IsInstanceValid(mate) && !mate.IsDowned && !mate.IsBodyBag)
            {
                mate.GlobalPosition = ExtractionPoint + new Vector3(-1.8f + i * 3.6f, 0.12f, 2.2f);
                mate.Velocity = Vector3.Zero;
            }
        }
        TryBeginExtractionSequence(_player);
        _extractionAircraft?.AdvanceForValidation(ExtractionAircraft.ArrivalDuration + 0.1f);
        UpdateExtractionSequence(0.1f);
        var aircraftArrived = _extractionAircraft?.BoardingReady == true;
        var boardingShown = aircraftArrived && _hud.ExtractionAircraftReady;
        UpdateExtractionSequence(coldDuration + 0.2f);
        var departureStarted = _missionEnded
            && _extractionDeparturePlaying
            && _extractionAircraft?.Phase == ExtractionAircraftPhase.Departing;
        var resultDelayed = !_hud.IsMissionResultVisible;
        var playerSeated = _player.IsExtractionPassenger
            && _player.GetParent() == _extractionAircraft?.PlayerSeat
            && _extractionAircraft?.PlayerPassengerVisible == true
            && _player.GlobalPosition.DistanceTo(_extractionAircraft.GlobalPosition) < 5.0f;
        var expectedBoardedMates = Mathf.Min(_squadMates.Count, 2);
        var squadSeated = _extractionBoardedSquadmates == expectedBoardedMates;
        foreach (var mate in _squadMates)
        {
            if (IsInstanceValid(mate) && !mate.IsDowned && !mate.IsBodyBag)
            {
                squadSeated &= mate.IsExtractionPassenger
                    && mate.Visible
                    && mate.GlobalPosition.DistanceTo(_extractionAircraft!.GlobalPosition) < 5.0f;
            }
        }
        var cameraFollowing = _extractionAircraft is not null
            && GetViewport().GetCamera3D() == _extractionAircraft.CinematicCamera;
        var cinematicHud = _hud.IsExtractionCinematicUiClear;
        _skipExtractionCinematicForValidation = true;
        _extractionAircraft?.AdvanceForValidation(ExtractionAircraft.TransferDuration * 0.5f);
        var aircraftFacesTravel = _extractionAircraft?.DepartureVisualAlignmentForDiagnostics > 0.98f;
        _extractionAircraft?.AdvanceForValidation(ExtractionAircraft.TransferDuration * 0.5f + 0.1f);
        await WaitFrames(2);
        var destinationReached = _extractionAircraft?.DestinationReached == true
            && _extractionAircraft.GlobalPosition.DistanceTo(OperationsOfficeHelipad) < 0.2f;
        var completed = destinationReached
            && _hud.IsMissionResultVisible
            && !_extractionDeparturePlaying
            && _missionPhase == "COMPLETE";
        var valid = entryStartedImmediately && countdownStarted && aircraftInbound && authoredVisual
            && combatPhasePreserved && leaveReset && aircraftArrived && boardingShown
            && departureStarted && resultDelayed && playerSeated && squadSeated
            && cameraFollowing && cinematicHud && aircraftFacesTravel && destinationReached && completed
            && objectiveScaled && orbitalRulesReady;
        GD.Print($"EXTRACTION_SEQUENCE_CHECK valid={valid} objective_free={entryStartedImmediately} objective_scaled={objectiveScaled} orbital_rules={orbitalRulesReady} cold_duration={coldDuration:0.0} priority_duration={priorityDuration:0.0} combat_phase_preserved={combatPhasePreserved} countdown={countdownStarted} inbound={aircraftInbound} authored_visual={authoredVisual} leave_reset={leaveReset} aircraft_arrived={aircraftArrived} boarding={boardingShown} departure={departureStarted} result_delayed={resultDelayed} player_seated={playerSeated} squad_seated={squadSeated} boarded={_extractionBoardedSquadmates}/{expectedBoardedMates} camera={cameraFollowing} cinematic_hud={cinematicHud} faces_travel={aircraftFacesTravel} destination={destinationReached} completed={completed} duration={coldDuration:0.0} transfer={ExtractionAircraft.TransferDuration:0.0}");
        GD.Print($"EXTRACTION_SEQUENCE_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }

    private async void CaptureExtractionFlight()
    {
        await WaitFrames(6);
        foreach (var enemy in _enemies)
        {
            if (IsInstanceValid(enemy))
            {
                enemy.ProcessMode = ProcessModeEnum.Disabled;
            }
        }
        foreach (var squad in _hostileSquads)
        {
            foreach (var member in squad.Members)
            {
                if (IsInstanceValid(member))
                {
                    member.ProcessMode = ProcessModeEnum.Disabled;
                }
            }
        }
        if (IsInstanceValid(_aircraft))
        {
            _aircraft.SetPhysicsProcess(false);
        }
        if (_extractionAircraft is null || !IsInstanceValid(_extractionAircraft))
        {
            GD.PushError("Extraction flight capture requires a valid friendly aircraft.");
            GetTree().Quit(2);
            return;
        }

        _player.GlobalPosition = ExtractionPoint + new Vector3(0.0f, 0.12f, 1.0f);
        for (var i = 0; i < _squadMates.Count; i++)
        {
            var mate = _squadMates[i];
            if (IsInstanceValid(mate) && !mate.IsDowned && !mate.IsBodyBag)
            {
                mate.GlobalPosition = ExtractionPoint + new Vector3(-1.8f + i * 3.6f, 0.12f, 2.2f);
                mate.Velocity = Vector3.Zero;
            }
        }

        TryBeginExtractionSequence(_player);
        _extractionAircraft.ForceBoardingReadyForValidation();
        _extractionRemaining = 0.0f;
        UpdateExtractionSequence(0.1f);
        await WaitFrames(2);
        _extractionAircraft.AdvanceForValidation(ExtractionAircraft.TransferDuration * 0.48f);
        await WaitFrames(12);

        SaveViewportImage("res://extraction_flight_validation.png");
        GD.Print($"EXTRACTION_FLIGHT_CAPTURE phase={_extractionAircraft.Phase} boarded={_extractionBoardedSquadmates} player_seated={_player.IsExtractionPassenger} camera={GetViewport().GetCamera3D() == _extractionAircraft.CinematicCamera} hud_hidden={_hud.IsExtractionCinematicUiClear} position={_extractionAircraft.GlobalPosition} path=extraction_flight_validation.png");
        GetTree().Quit();
    }
}
