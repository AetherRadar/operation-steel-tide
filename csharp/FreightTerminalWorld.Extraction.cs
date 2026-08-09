using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private const float ExtractionCountdownDuration = 12.0f;
    private const float ExtractionZoneRadius = 7.0f;
    private ExtractionAircraft? _extractionAircraft;
    private bool _extractionCountdownActive;
    private bool _extractionPlayerInside;
    private bool _extractionDeparturePlaying;
    private bool _skipExtractionCinematicForValidation;
    private float _extractionRemaining = ExtractionCountdownDuration;
    private SquadOrder _preExtractionSquadOrder = SquadOrder.Follow;
    private Vector3 _preExtractionSquadMovePoint;

    public bool IsExtractionCountdownActive => _extractionCountdownActive;
    public bool IsExtractionDeparturePlaying => _extractionDeparturePlaying;

    private void BuildExtractionAircraft()
    {
        _extractionAircraft = new ExtractionAircraft
        {
            Name = "FriendlyExtractionTiltRotor",
            PadPosition = ExtractionPoint
        };
        _levelRoot.AddChild(_extractionAircraft);
    }

    private void TryBeginExtractionSequence(Node3D body)
    {
        if (body != _player || _missionEnded)
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
        _extractionPlayerInside = false;
        if (_extractionCountdownActive)
        {
            CancelExtractionCountdown();
        }
    }

    private void UpdateExtractionSequence(float delta)
    {
        if (!IsInstanceValid(_player) || !_player.IsInsideTree()
            || !IsInstanceValid(_hud) || !_hud.IsInsideTree())
        {
            return;
        }

        _extractionPlayerInside = IsPlayerInsideExtractionZone();
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
        if (_extractionRemaining <= 0.0f && _extractionAircraft?.BoardingReady == true)
        {
            CompleteExtractionSequence();
        }
    }

    private bool IsPlayerInsideExtractionZone()
    {
        var offset = _player.GlobalPosition - ExtractionPoint;
        var horizontalSquared = offset.X * offset.X + offset.Z * offset.Z;
        return horizontalSquared <= ExtractionZoneRadius * ExtractionZoneRadius
            && offset.Y >= -1.5f
            && offset.Y <= 4.2f;
    }

    private void BeginExtractionCountdown()
    {
        if (_extractionCountdownActive || _missionEnded)
        {
            return;
        }

        _extractionCountdownActive = true;
        _extractionRemaining = ExtractionCountdownDuration;
        _missionDirector.ExitDeploymentZone();
        _preExtractionSquadOrder = _squadOrder;
        _preExtractionSquadMovePoint = _squadMovePoint;
        _extractionAircraft?.BeginInbound();
        RallySquadToExtraction();
        UpdateExtractionHud();
        _hud.ShowLocalizedMessage(
            "extraction_inbound",
            "FRIENDLY TILT-ROTOR INBOUND  //  HOLD THE ZONE",
            new Color(0.3f, 1.0f, 0.66f));
    }

    private void CancelExtractionCountdown()
    {
        _extractionCountdownActive = false;
        _extractionRemaining = ExtractionCountdownDuration;
        _hud.HideExtractionCountdown();
        _extractionAircraft?.AbortPickup();
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
            ExtractionCountdownDuration,
            _extractionAircraft?.BoardingReady == true,
            ready,
            total);
    }

    private (int Ready, int Total) CountExtractionSquad()
    {
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
        _missionEnded = true;
        _player.EjectFromVehicleIfAny();
        _player.UiLocked = true;
        _player.DisarmFireInput();
        _player.DisarmMovementInput();
        _hud.HideExtractionCountdown();
        _extractionAircraft?.BeginDeparture();
        BoardReadySquadmates();
        _hud.ShowLocalizedMessage(
            "extraction_departing",
            "SQUAD ABOARD  //  CLEARING THE COMBAT ZONE",
            new Color(0.36f, 1.0f, 0.7f));

        if (!_skipExtractionCinematicForValidation)
        {
            await ToSignal(GetTree().CreateTimer(2.4f), SceneTreeTimer.SignalName.Timeout);
            if (!IsInsideTree())
            {
                return;
            }
        }
        FinishExtractionMission();
    }

    private void BoardReadySquadmates()
    {
        foreach (var mate in _squadMates)
        {
            if (!IsInstanceValid(mate) || !mate.IsInsideTree() || mate.IsDowned || mate.IsBodyBag)
            {
                continue;
            }
            if (HorizontalDistance(mate.GlobalPosition, ExtractionPoint) <= 14.0f)
            {
                mate.Visible = false;
                mate.SetPhysicsProcess(false);
            }
        }
    }

    private void FinishExtractionMission()
    {
        _player.IsDead = true;
        Input.MouseMode = Input.MouseModeEnum.Visible;
        _missionDirector.CompleteMission(true, _kills, _headshots, _shotsFired, _shotsHit);
        var ranks = BuildExtractionLootRanking();
        var progression = CommitExtractionValue();
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
        _missionDirector.ExitDeploymentZone();
        _missionDirector.RaiseConfirmedAlarm();
        _player.GlobalPosition = ExtractionPoint + new Vector3(0, 0.12f, 1.0f);
        TryBeginExtractionSequence(_player);
        var earlyCallPhase = _missionPhase;
        var entryStartedImmediately = _extractionCountdownActive
            && Mathf.IsEqualApprox(_extractionRemaining, ExtractionCountdownDuration)
            && _hud.IsExtractionCountdownVisible
            && !IsPlayerProtected();
        var combatPhasePreserved = earlyCallPhase == "COMBAT";
        UpdateExtractionSequence(3.0f);
        var countdownStarted = _extractionCountdownActive
            && _extractionRemaining < ExtractionCountdownDuration
            && _hud.IsExtractionCountdownVisible;
        var aircraftInbound = _extractionAircraft?.Phase == ExtractionAircraftPhase.Inbound;

        _player.GlobalPosition = ExtractionPoint + new Vector3(ExtractionZoneRadius + 2.0f, 0.12f, 0);
        UpdateExtractionSequence(0.1f);
        var leaveReset = !_extractionCountdownActive
            && Mathf.IsEqualApprox(_extractionRemaining, ExtractionCountdownDuration)
            && !_hud.IsExtractionCountdownVisible
            && _missionPhase == earlyCallPhase;

        _player.GlobalPosition = ExtractionPoint + new Vector3(0, 0.12f, 1.0f);
        TryBeginExtractionSequence(_player);
        _extractionAircraft?.AdvanceForValidation(ExtractionAircraft.ArrivalDuration + 0.1f);
        UpdateExtractionSequence(0.1f);
        var aircraftArrived = _extractionAircraft?.BoardingReady == true;
        var boardingShown = aircraftArrived && _hud.ExtractionAircraftReady;
        _skipExtractionCinematicForValidation = true;
        UpdateExtractionSequence(ExtractionCountdownDuration + 0.2f);
        var completed = _missionEnded && !_extractionCountdownActive && _extractionAircraft?.Phase == ExtractionAircraftPhase.Departing;
        var valid = entryStartedImmediately && countdownStarted && aircraftInbound
            && combatPhasePreserved && leaveReset && aircraftArrived && boardingShown && completed;
        GD.Print($"EXTRACTION_SEQUENCE_CHECK valid={valid} objective_free={entryStartedImmediately} combat_phase_preserved={combatPhasePreserved} countdown={countdownStarted} inbound={aircraftInbound} leave_reset={leaveReset} aircraft_arrived={aircraftArrived} boarding={boardingShown} completed={completed} duration={ExtractionCountdownDuration:0.0}");
        GD.Print($"EXTRACTION_SEQUENCE_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }
}
