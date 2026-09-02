using System;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private async void ValidateExtractionSurvivors()
    {
        var cleanStart = false;
        var firstDown = false;
        var firstDownAiCommandHidden = false;
        var firstDownCommandsCancelled = false;
        var firstRevived = false;
        var mateDown = false;
        var secondEliminated = false;
        var secondEliminatedAiCommandVisible = false;
        var secondDownRuleCoverage = false;
        var mixedRosterPrepared = false;
        var unsafePreparationBounded = false;
        var activePreparationGrace = false;
        var criticalRecoveryBeforeRescue = false;
        var rescueAssigned = false;
        var rescueBeforeExtract = false;
        var mateRevived = false;
        var medicSkippedKia = false;
        var takeoverStarted = false;
        var manualAiCommandPreserved = false;
        var boardingReady = false;
        var unboardedValueExcluded = false;
        var boardedValueIncluded = false;
        var aiBoarded = false;
        var playerExcluded = false;
        var completed = false;
        var resultAiCommandHidden = false;
        var failure = string.Empty;

        try
        {
            await WaitFrames(6);
            EnsureAiSquadFill();
            DisableActorsForSurvivalDiagnostics();
            if (IsInstanceValid(_aircraft))
            {
                _aircraft.SetPhysicsProcess(false);
            }

            var medic = _squadMates.FirstOrDefault(mate =>
                IsInstanceValid(mate)
                && !mate.IsHumanProxy
                && mate.Role == OperatorRole.Medic);
            var casualty = _squadMates.FirstOrDefault(mate =>
                IsInstanceValid(mate)
                && !mate.IsHumanProxy
                && !ReferenceEquals(mate, medic));
            if (medic is null || casualty is null
                || _extractionAircraft is null || !IsInstanceValid(_extractionAircraft))
            {
                throw new InvalidOperationException("missing survivor extraction actors");
            }

            _missionDirector.ExitDeploymentZone();
            ClearLeaderReviveAi();
            ResetAiReviveAbandonment();
            _player.ProcessMode = ProcessModeEnum.Disabled;
            _player.GlobalPosition = ExtractionPoint
                + new Vector3(ExtractionZoneRadius + 4.0f, 0.12f, 0.0f);
            _player.Velocity = Vector3.Zero;

            medic.GlobalPosition = ExtractionPoint + new Vector3(-1.4f, 0.12f, 2.0f);
            casualty.GlobalPosition = ExtractionPoint + new Vector3(0.0f, 0.12f, 2.0f);
            foreach (var mate in new[] { medic, casualty })
            {
                mate.ProcessMode = ProcessModeEnum.Inherit;
                mate.Velocity = Vector3.Zero;
                mate.RestoreHealth(mate.MaxHealth);
                mate.GrantFireablePrimaryForDiagnostics();
                mate.ResetCombatTacticsForDiagnostics();
                mate.SetOrder(SquadOrder.Hold, mate.GlobalPosition);
                mate.SetSkillCooldownForDiagnostics(999.0f);
                ClearSquadNavigation(mate);
            }
            await WaitFrames(3);
            cleanStart = !_missionEnded && !_extractionCountdownActive;
            medic.SetSustainmentStateForDiagnostics(
                medic.MaxHealth * 0.30f,
                armorRatio: 1.0f,
                bandages: 1,
                plates: 0);
            var recoveredValueSeeded = medic.TryStoreSustainmentSupply(
                    new LootItem
                    {
                        Kind = LootItemKind.Medical,
                        MedicalKind = MedicalItemKind.FieldMedkit,
                        Quantity = 2,
                        Grade = LootGrade.Legendary
                    },
                    2)
                == 2;
            unboardedValueExcluded = recoveredValueSeeded
                && medic.RecoveredSustainmentValue > 0
                && LivingSquadRecoveredSustainmentValue() == 0;

            // Keep the permanently eliminated player in the Medic's spray cone.
            // The old selector would prefer that -1 health ratio over a living patient.
            _player.GlobalPosition = ExtractionPoint + new Vector3(2.8f, 0.12f, 2.0f);
            _player.SetHealthForDiagnostics(10.0f);
            _player.SetReviveUsedForDiagnostics(false);
            firstDown = _player.TakeCombatDamage(
                    999.0f,
                    _player.HitPoint(HitRegion.Torso),
                    this)
                && _player.IsDead
                && _player.CanBeRevived
                && _localPlayerDowned;
            firstDownAiCommandHidden = !_hud.IsAiSquadCommandPresentationVisibleForDiagnostics
                && !_hud.AreSquadCommandControlsVisibleForDiagnostics
                && _hud.DownedFooterSuppressedForDiagnostics;
            firstDownCommandsCancelled = _squadOrder == SquadOrder.Follow
                && !_squadHoldFire
                && new[] { medic, casualty }.All(mate =>
                    mate.Order == SquadOrder.Follow && !mate.HoldFireActive);
            firstRevived = _player.TryReceiveRevive(50.0f)
                && !_player.IsDead
                && _player.ReviveUsed;

            secondDownRuleCoverage = SquadReviveRules.ShouldFailExtractionOnSecondDown(
                    demolitionMode: false,
                    extractionNetworkClient: false,
                    playerReviveUsed: true,
                    allAiSquad: true)
                && !SquadReviveRules.ShouldFailExtractionOnSecondDown(false, false, true, false)
                && !SquadReviveRules.ShouldFailExtractionOnSecondDown(true, false, true, true)
                && !SquadReviveRules.ShouldFailExtractionOnSecondDown(false, true, true, true)
                && !SquadReviveRules.ShouldFailExtractionOnSecondDown(false, false, false, true);
            var diagnosticHumanProxy = SpawnSquadMate(
                99,
                OperatorRole.Assault,
                human: true,
                peerId: 900002,
                networkProxy: true);
            diagnosticHumanProxy.ProcessMode = ProcessModeEnum.Disabled;
            diagnosticHumanProxy.GlobalPosition = new Vector3(900.0f, 0.12f, 900.0f);
            mixedRosterPrepared = diagnosticHumanProxy.IsHumanProxy
                && diagnosticHumanProxy.IsNetworkProxy
                && !IsAllAiSquadSession;

            mateDown = casualty.TakeCombatDamage(
                    999.0f,
                    casualty.HitPoint(HitRegion.Torso),
                    this)
                && casualty.IsDowned
                && casualty.CanBeRevived;
            secondEliminated = _player.TakeCombatDamage(
                    999.0f,
                    _player.HitPoint(HitRegion.Torso),
                    this)
                && _player.IsDead
                && !_player.CanBeRevived
                && _player.ReviveUsed
                && _localPlayerEliminated
                && !_localPlayerDowned
                && !_missionEnded;
            secondEliminatedAiCommandVisible = _hud.IsAiSquadCommandPresentationVisibleForDiagnostics
                && _hud.AreSquadCommandControlsVisibleForDiagnostics;

            UpdateExtractionSequence(0.1f);
            var recoveryHeldExtraction = SurvivorExtractionTakeoverForDiagnostics
                && !_extractionCountdownActive
                && casualty.CanBeRevived;

            // Model a survivor who cannot find a safe self-care window. The
            // preparation hold must expire, cool down this casualty, and allow
            // evacuation rather than pausing the squad forever.
            AdvanceAiReviveRetryClock(AiRevivePreparationTimeout + 0.1f);
            UpdateExtractionSequence(0.1f);
            unsafePreparationBounded = _extractionCountdownActive
                && IsAiReviveTargetCoolingDown(casualty);
            ResetAiReviveAbandonment(casualty);
            UpdateExtractionSequence(0.1f);
            var recoveryStarted = medic.AdvanceSustainmentForDiagnostics(0.05f)
                && medic.SustainmentActionForDiagnostics == SquadSustainmentActionKind.Heal;
            AdvanceAiReviveRetryClock(AiRevivePreparationTimeout + 0.1f);
            UpdateExtractionSequence(0.1f);
            activePreparationGrace = !_extractionCountdownActive
                && medic.IsHealingForSquadRevivePreparation
                && _aiRevivePreparationCompletionGraceUsed;
            medic.CompleteSustainmentActionForDiagnostics();
            criticalRecoveryBeforeRescue = recoveryHeldExtraction
                && recoveryStarted
                && activePreparationGrace
                && medic.Health / medic.MaxHealth >= AiReviveMinimumHealthRatio;

            UpdateSquadReviveAi(0.1f);
            rescueAssigned = ReferenceEquals(_leaderReviver, medic)
                && ReferenceEquals(_aiReviveTarget, casualty)
                && medic.IsRevivingTarget(casualty);
            UpdateExtractionSequence(0.1f);
            rescueBeforeExtract = SurvivorExtractionTakeoverForDiagnostics
                && !_extractionCountdownActive
                && casualty.CanBeRevived;

            UpdateSquadReviveAi(3.0f);
            mateRevived = !casualty.IsDowned
                && casualty.ReviveUsed
                && _leaderReviver is null
                && _aiReviveTarget is null;

            var patient = FindLowestFriendly(0.82f, includeDowned: true);
            var patientHealthBefore = casualty.Health;
            var sprayTarget = patient?.CombatNode.GlobalPosition ?? casualty.GlobalPosition;
            ApplyMedicSpray(
                medic,
                medic.GlobalPosition + Vector3.Up * 1.2f,
                medic.GlobalPosition.DirectionTo(sprayTarget));
            medicSkippedKia = ReferenceEquals(patient, casualty)
                && casualty.Health > patientHealthBefore
                && _player.IsDead
                && !_player.CanBeRevived;

            UpdateExtractionSequence(0.1f);
            var (ready, total) = CountSurvivorExtractionSquad();
            takeoverStarted = SurvivorExtractionTakeoverForDiagnostics
                && _extractionCountdownActive
                && _hud.IsExtractionCountdownVisible
                && ready == 2
                && total == 2
                && _extractionAircraft.Phase == ExtractionAircraftPhase.Inbound;
            var manualHoldAccepted = TryIssuePlayerSquadOrder(SquadOrder.Hold);
            UpdateExtractionSequence(0.1f);
            manualAiCommandPreserved = manualHoldAccepted
                && new[] { medic, casualty }.All(mate => mate.Order == SquadOrder.Hold);

            _extractionAircraft.AdvanceForValidation(ExtractionAircraft.ArrivalDuration + 0.1f);
            UpdateExtractionSequence(0.1f);
            boardingReady = _extractionAircraft.BoardingReady
                && _hud.ExtractionAircraftReady;
            _skipExtractionCinematicForValidation = true;
            UpdateExtractionSequence(CurrentExtractionCountdownDuration() + 0.2f);
            await WaitFrames(2);

            aiBoarded = medic.IsExtractionPassenger
                && casualty.IsExtractionPassenger
                && _extractionBoardedSquadmates == 2
                && !ReferenceEquals(medic.GetParent(), casualty.GetParent());
            boardedValueIncluded = aiBoarded
                && LivingSquadRecoveredSustainmentValue()
                    >= medic.RecoveredSustainmentValue
                && medic.RecoveredSustainmentValue > 0;
            playerExcluded = !_player.IsExtractionPassenger
                && !_extractionAircraft.PlayerPassengerVisible
                && !ReferenceEquals(_player.GetParent(), _extractionAircraft.PlayerSeat);
            completed = _missionEnded
                && _extractionMissionSucceeded
                && !_extractionDeparturePlaying
                && _missionPhase == "COMPLETE"
                && _hud.IsMissionResultVisible
                && _extractionAircraft.DestinationReached;
            resultAiCommandHidden = !_hud.IsAiSquadCommandPresentationVisibleForDiagnostics
                && !_hud.AreSquadCommandControlsVisibleForDiagnostics
                && !_hud.ClassSkillHudVisibleForDiagnostics;
        }
        catch (Exception exception)
        {
            failure = exception.GetType().Name + ":" + exception.Message;
            GD.PushError($"EXTRACTION_SURVIVORS_EXCEPTION {failure}");
        }

        var valid = cleanStart
            && firstDown
            && firstDownAiCommandHidden
            && firstDownCommandsCancelled
            && firstRevived
            && mateDown
            && secondEliminated
            && secondEliminatedAiCommandVisible
            && secondDownRuleCoverage
            && mixedRosterPrepared
            && unsafePreparationBounded
            && activePreparationGrace
            && criticalRecoveryBeforeRescue
            && rescueAssigned
            && rescueBeforeExtract
            && mateRevived
            && medicSkippedKia
            && takeoverStarted
            && manualAiCommandPreserved
            && boardingReady
            && unboardedValueExcluded
            && boardedValueIncluded
            && aiBoarded
            && playerExcluded
            && completed
            && resultAiCommandHidden
            && string.IsNullOrEmpty(failure);
        GD.Print(
            $"EXTRACTION_SURVIVORS_CHECK valid={valid} clean={cleanStart} "
            + $"first_down={firstDown} first_revived={firstRevived} mate_down={mateDown} "
            + $"first_down_ai_hint_hidden={firstDownAiCommandHidden} "
            + $"first_down_commands_cancelled={firstDownCommandsCancelled} "
            + $"second_eliminated={secondEliminated} second_down_ai_hint={secondEliminatedAiCommandVisible} "
            + $"second_down_rule={secondDownRuleCoverage} mixed_roster={mixedRosterPrepared} "
            + $"unsafe_prep_bounded={unsafePreparationBounded} "
            + $"active_prep_grace={activePreparationGrace} "
            + $"recovery_before_rescue={criticalRecoveryBeforeRescue} "
            + $"rescue_assigned={rescueAssigned} "
            + $"rescue_before_extract={rescueBeforeExtract} mate_revived={mateRevived} "
            + $"medic_skipped_kia={medicSkippedKia} takeover={takeoverStarted} manual_command={manualAiCommandPreserved} "
            + $"boarding_ready={boardingReady} unboarded_value={unboardedValueExcluded} "
            + $"boarded_value={boardedValueIncluded} ai_boarded={aiBoarded} "
            + $"player_excluded={playerExcluded} completed={completed} "
            + $"result_ai_hint_hidden={resultAiCommandHidden} failure={failure}");
        GD.Print($"EXTRACTION_SURVIVORS_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }
}
