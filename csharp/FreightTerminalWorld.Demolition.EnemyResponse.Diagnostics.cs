using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private async void ValidateDemolitionEnemyResponse()
    {
        await WaitFrames(5);
        _hud.PressDemolitionModeForDiagnostics();
        _hud.PressDemolitionRoleForDiagnostics(OperatorRole.Medic);
        _hud.PressDemolitionMapForDiagnostics(DemolitionMapCatalog.TideforgeId);
        _hud.PressDemolitionDeployForDiagnostics();
        await WaitFrames(5);
        if (_demolitionBuyPhaseActive)
        {
            OnDemolitionPurchaseRequested(string.Empty, string.Empty, false, 0, 0, 0, 0);
        }
        await WaitFrames(5);

        var layout = _demolitionMode && _demolitionRoundActive
            ? DemolitionLayout()
            : null;
        var probe = _demolitionOpponents.FirstOrDefault(opponent =>
            IsInstanceValid(opponent)
            && !opponent.IsDead
            && IsAutonomousDemolitionOpponent(opponent));
        if (layout is null
            || probe is null
            || DemolitionPlayerSide != DemolitionTeam.Attackers)
        {
            GD.Print($"DEMOLITION_ENEMY_RESPONSE_CHECK valid=False stage=preconditions mode={_demolitionMode} active={_demolitionRoundActive} side={DemolitionPlayerSide} probe={probe is not null}");
            GD.Print("DEMOLITION_ENEMY_RESPONSE_PASS valid=False");
            QuitDiagnosticAfterSceneCleanup(2);
            return;
        }

        // The fixture is intentionally collision-only and elevated above authored geometry.
        // This isolates view-cone, objective arbitration, stance, and shooting behavior from
        // map routing while still running CharacterBody3D movement and ballistic raycasts.
        SetPhysicsProcess(false);
        var site = layout.SitePositions[0];
        var platformCenter = new Vector3(site.X, site.Y + 40.0f, site.Z);
        var platform = CreateDemolitionEnemyResponsePlatform(platformCenter);
        var floorY = platformCenter.Y + 0.32f;
        var probePosition = new Vector3(site.X, floorY, site.Z);
        var playerPosition = probePosition + Vector3.Back * 8.0f;

        foreach (var opponent in _demolitionOpponents)
        {
            if (!IsInstanceValid(opponent))
            {
                continue;
            }
            opponent.ProcessMode = ProcessModeEnum.Disabled;
            if (opponent != probe)
            {
                opponent.GlobalPosition = platformCenter
                    + new Vector3(800.0f + opponent.NetworkId * 4.0f, 0.0f, 800.0f);
            }
        }
        foreach (var mate in _squadMates)
        {
            if (!IsInstanceValid(mate))
            {
                continue;
            }
            mate.ProcessMode = ProcessModeEnum.Disabled;
            mate.GlobalPosition = platformCenter
                + new Vector3(-800.0f - mate.SquadSlot * 4.0f, 0.0f, -800.0f);
        }

        _player.ProcessMode = ProcessModeEnum.Inherit;
        _player.SetPhysicsProcess(false);
        _player.GlobalPosition = playerPosition;
        _player.Velocity = Vector3.Zero;
        _player.SetHealthForDiagnostics(_player.MaxHealth);
        _player.SetCombatMovementTrailForDiagnostics(new[] { playerPosition });

        probe.GlobalPosition = probePosition;
        probe.LookAt(probePosition + Vector3.Forward, Vector3.Up);
        probe.SentryMode = false;
        probe.ConfigureCombatProbeForDiagnostics(
            0x454E454D595F4149UL,
            playerPosition,
            bypassPlayerProtection: true,
            suppressContactSharing: true);
        probe.GrantFireablePrimaryForDiagnostics(WeaponCatalog.Build(WeaponPlatform.M4A1, 1));
        probe.ProcessMode = ProcessModeEnum.Disabled;
        await WaitFrames(3);

        // Pure production planners make stance and jump prerequisites deterministic.
        var coveredVisible = new EnemyCombatPostureContext(
            EnemyCombatPosture.Standing, true, false, true, false, true,
            18.0f, 0.0f, 0.0f, 0.0f, 0.0f);
        var pressuredMidrange = coveredVisible with
        {
            Pressured = true,
            InCover = false,
            Distance = 24.0f
        };
        var closeProne = pressuredMidrange with
        {
            Current = EnemyCombatPosture.Prone,
            Distance = 4.0f,
            HoldRemaining = 1.0f
        };
        var lostCrouch = coveredVisible with
        {
            Current = EnemyCombatPosture.Crouched,
            HasSight = false,
            InCover = false,
            HoldRemaining = 1.0f
        };
        var coveredCrouch = EnemyOperator.PlanCombatPostureForDiagnostics(coveredVisible).Posture
            == EnemyCombatPosture.Crouched;
        var pressuredProne = EnemyOperator.PlanCombatPostureForDiagnostics(pressuredMidrange).Posture
            == EnemyCombatPosture.Prone;
        var conditionLossStands = EnemyOperator.PlanCombatPostureForDiagnostics(closeProne).Posture
                == EnemyCombatPosture.Standing
            && EnemyOperator.PlanCombatPostureForDiagnostics(lostCrouch).Posture
                == EnemyCombatPosture.Standing;

        probe.SetCombatPostureForDiagnostics(EnemyCombatPosture.Standing);
        var standingDimensions = probe.CaptureCombatMovementForDiagnostics();
        probe.SetCombatPostureForDiagnostics(EnemyCombatPosture.Crouched);
        var crouchedDimensions = probe.CaptureCombatMovementForDiagnostics();
        probe.SetCombatPostureForDiagnostics(EnemyCombatPosture.Prone);
        var proneDimensions = probe.CaptureCombatMovementForDiagnostics();
        probe.SetCombatPostureForDiagnostics(EnemyCombatPosture.Standing);
        var stanceDimensionsReasonable = standingDimensions.ColliderHeight > crouchedDimensions.ColliderHeight
            && crouchedDimensions.ColliderHeight > proneDimensions.ColliderHeight
            && standingDimensions.EyeHeight > crouchedDimensions.EyeHeight
            && crouchedDimensions.EyeHeight > proneDimensions.EyeHeight
            && Mathf.IsEqualApprox(standingDimensions.ColliderHeight, 1.78f)
            && Mathf.IsEqualApprox(crouchedDimensions.ColliderHeight, 1.22f)
            && Mathf.IsEqualApprox(proneDimensions.ColliderHeight, 0.78f)
            && Mathf.IsEqualApprox(standingDimensions.EyeHeight, 1.55f)
            && Mathf.IsEqualApprox(crouchedDimensions.EyeHeight, 1.03f)
            && Mathf.IsEqualApprox(proneDimensions.EyeHeight, 0.55f);

        // A low ceiling overlaps only the volume required to expand beyond a crouch.
        // Both crouch and prone must retain their collision shape until that obstruction
        // is removed, including while weak flash movement continues at posture speed.
        var lowCeiling = new CollisionShape3D
        {
            Name = "CombatPostureLowCeiling",
            Position = new Vector3(0.0f, floorY - platformCenter.Y + 1.4f, 0.0f),
            Shape = new BoxShape3D { Size = new Vector3(3.0f, 0.2f, 3.0f) }
        };
        platform.AddChild(lowCeiling);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        var flashSource = probePosition + Vector3.Left * 2.0f;

        probe.SetCombatPostureForDiagnostics(EnemyCombatPosture.Crouched);
        var crouchedStandBlocked = !probe.TryStandForDiagnostics()
            && probe.CaptureCombatMovementForDiagnostics().Posture
                == EnemyCombatPosture.Crouched;
        probe.Velocity = Vector3.Right * 5.2f;
        probe.ApplyFlashbang(new FlashbangExposure(flashSource, 0.32f, 0.45f, 2.0f, 1.0f));
        probe.ApplyFlashbangMovementForDiagnostics(0.1f);
        var weakCrouchFlashState = probe.CaptureCombatMovementForDiagnostics();
        var weakCrouchFlashSpeed = HorizontalSpeed(probe.Velocity);
        var weakCrouchFlashMovementBounded = weakCrouchFlashState.Posture
                == EnemyCombatPosture.Crouched
            && weakCrouchFlashSpeed is > 0.2f and <= 1.86f;
        probe.AdvanceCombatMovementTimersForDiagnostics(1.0f);

        probe.SetCombatPostureForDiagnostics(EnemyCombatPosture.Prone);
        var proneStandBlocked = !probe.TryStandForDiagnostics()
            && probe.CaptureCombatMovementForDiagnostics().Posture
                == EnemyCombatPosture.Prone;
        probe.Velocity = Vector3.Right * 5.2f;
        probe.ApplyFlashbang(new FlashbangExposure(flashSource, 0.32f, 0.45f, 2.0f, 1.0f));
        probe.ApplyFlashbangMovementForDiagnostics(0.1f);
        var weakProneFlashState = probe.CaptureCombatMovementForDiagnostics();
        var weakProneFlashSpeed = HorizontalSpeed(probe.Velocity);
        var weakProneFlashMovementBounded = weakProneFlashState.Posture
                == EnemyCombatPosture.Prone
            && weakProneFlashSpeed is > 0.2f and <= 1.11f;
        probe.AdvanceCombatMovementTimersForDiagnostics(1.0f);
        var lowCeilingPreservesPosture = crouchedStandBlocked && proneStandBlocked;

        lowCeiling.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        var clearanceRemovalRestoresStanding = probe.TryStandForDiagnostics()
            && probe.CaptureCombatMovementForDiagnostics().Posture
                == EnemyCombatPosture.Standing;

        probe.ResetTacticalStateForDiagnostics();
        probe.GlobalPosition = probePosition;
        probe.Velocity = Vector3.Zero;
        probe.LookAt(
            new Vector3(playerPosition.X, probePosition.Y, playerPosition.Z),
            Vector3.Up);
        probe.ConfigureCombatProbeForDiagnostics(
            0x4A554D505F4149UL,
            playerPosition,
            bypassPlayerProtection: true,
            suppressContactSharing: true);
        probe.GrantFireablePrimaryForDiagnostics(WeaponCatalog.Build(WeaponPlatform.M4A1, 1));
        probe.ProcessMode = ProcessModeEnum.Disabled;
        _ = probe.TakeDamage(0.1f, probe.GlobalPosition + Vector3.Up, _player);
        probe.ArmWeaponForDiagnostics();

        var readyJump = new EnemyCombatJumpContext(
            true, true, true, true, true, false, true, 2.0f, 8.0f, 0.0f);
        var jumpConditionsRequired = EnemyOperator.CanStartCombatJumpAttackForDiagnostics(readyJump)
            && !EnemyOperator.CanStartCombatJumpAttackForDiagnostics(readyJump with { Pressured = false })
            && !EnemyOperator.CanStartCombatJumpAttackForDiagnostics(readyJump with { HasSight = false })
            && !EnemyOperator.CanStartCombatJumpAttackForDiagnostics(readyJump with { Distance = 4.49f })
            && !EnemyOperator.CanStartCombatJumpAttackForDiagnostics(readyJump with { Distance = 10.01f })
            && !EnemyOperator.CanStartCombatJumpAttackForDiagnostics(readyJump with { CooldownRemaining = 0.1f });
        probe.Velocity = new Vector3(2.0f, 0.0f, 0.0f);
        var invalidJumpStarted = probe.TryStartCombatJumpAttackForDiagnostics(
            readyJump with { Pressured = false },
            Vector3.Right);
        var invalidJumpStaysGrounded = !invalidJumpStarted && Mathf.Abs(probe.Velocity.Y) <= 0.001f;
        var validJumpStarted = probe.TryStartCombatJumpAttackForDiagnostics(readyJump, Vector3.Right);
        var jumpState = probe.CaptureCombatMovementForDiagnostics();
        var jumpImpulseY = probe.Velocity.Y;
        var validJumpProducesImpulse = validJumpStarted
            && jumpImpulseY > 4.0f
            && jumpState.JumpCooldown > 3.0f
            && jumpState.JumpAttacks == 1;
        var airborneShotsBefore = probe.AttackShotsFired;
        var airborneMarkersBefore = jumpState.AirborneAttackShots;
        probe.ProcessMode = ProcessModeEnum.Inherit;
        var airborneShotObserved = false;
        var airborneAimingPoseObserved = false;
        var airborneResponseFrames = 0;
        const int maximumAirborneResponseFrames = 90;
        while (airborneResponseFrames < maximumAirborneResponseFrames
            && (!airborneShotObserved || !airborneAimingPoseObserved))
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            _player.SetHealthForDiagnostics(_player.MaxHealth);
            var airborneState = probe.CaptureCombatMovementForDiagnostics();
            airborneShotObserved = !probe.IsOnFloor()
                && probe.AttackShotsFired > airborneShotsBefore
                && airborneState.AirborneAttackShots > airborneMarkersBefore;
            airborneAimingPoseObserved |= !probe.IsOnFloor()
                && probe.AuthoredAnimationForDiagnostics == "aim_idle";
            airborneResponseFrames++;
        }
        probe.ProcessMode = ProcessModeEnum.Disabled;
        var airborneAnimation = probe.AuthoredAnimationForDiagnostics;
        probe.Velocity = new Vector3(2.0f, 0.0f, 0.0f);
        var cooldownJumpStarted = probe.TryStartCombatJumpAttackForDiagnostics(
            readyJump,
            Vector3.Right);
        var jumpCooldownPreventsRepeat = !cooldownJumpStarted
            && Mathf.Abs(probe.Velocity.Y) <= 0.001f;

        probe.Velocity = Vector3.Zero;
        probe.ApplyFlashbang(new FlashbangExposure(flashSource, 1.0f, 0.9f, 2.0f, 1.0f));
        var strongFlashState = probe.CaptureCombatMovementForDiagnostics();
        var strongFlashSuppressesCombat = strongFlashState.VisionSuppressed
            && !strongFlashState.CanFire;
        probe.AdvanceCombatMovementTimersForDiagnostics(0.4f);
        var stackedFlashBeforeWeak = probe.CaptureCombatMovementForDiagnostics();
        probe.ApplyFlashbang(new FlashbangExposure(flashSource, 0.24f, 0.1f, 2.0f, 1.0f));
        var stackedFlashAfterWeak = probe.CaptureCombatMovementForDiagnostics();
        var weakFlashCannotReduceStrongFlash = stackedFlashAfterWeak.FlashIntensity
                >= stackedFlashBeforeWeak.FlashIntensity - 0.001f
            && stackedFlashAfterWeak.FlashRemaining
                >= stackedFlashBeforeWeak.FlashRemaining - 0.001f;
        probe.ApplyFlashbang(new FlashbangExposure(flashSource, 0.95f, 0.1f, 2.0f, 1.0f));
        var stackedFlashAfterStronger = probe.CaptureCombatMovementForDiagnostics();
        var strongerFlashRaisesCurrentIntensity = stackedFlashAfterStronger.FlashIntensity >= 0.94f
            && stackedFlashAfterStronger.FlashRemaining
                >= stackedFlashAfterWeak.FlashRemaining - 0.001f;
        probe.ApplyFlashbangMovementForDiagnostics(0.1f);
        var flashEscapeDirection = (probePosition - flashSource).Normalized();
        var flashKeepsEvading = HorizontalSpeed(probe.Velocity) > 0.2f
            && probe.Velocity.Dot(flashEscapeDirection) > 0.2f;
        probe.AdvanceCombatMovementTimersForDiagnostics(0.2f);
        var fadingFlashState = probe.CaptureCombatMovementForDiagnostics();
        var flashDecays = fadingFlashState.FlashIntensity > 0.0f
            && fadingFlashState.FlashIntensity < stackedFlashAfterWeak.FlashIntensity;
        probe.AdvanceCombatMovementTimersForDiagnostics(0.35f);
        var recoveredFlashState = probe.CaptureCombatMovementForDiagnostics();
        var flashRecovers = !recoveredFlashState.VisionSuppressed
            && recoveredFlashState.CanFire
            && recoveredFlashState.FlashIntensity <= 0.001f;

        probe.ResetTacticalStateForDiagnostics();
        probe.GlobalPosition = probePosition;
        probe.Velocity = Vector3.Zero;
        probe.LookAt(probePosition + Vector3.Forward, Vector3.Up);
        probe.ConfigureCombatProbeForDiagnostics(
            0x454E454D595F4149UL,
            playerPosition,
            bypassPlayerProtection: true,
            suppressContactSharing: true);
        probe.GrantFireablePrimaryForDiagnostics(WeaponCatalog.Build(WeaponPlatform.M4A1, 1));
        probe.ProcessMode = ProcessModeEnum.Disabled;

        _demolitionDevicePlanted = true;
        _demolitionActiveSite = 0;
        _demolitionDefuser = probe;
        _demolitionDefuseProgress = 0.25f;
        _demolitionRemaining = 25.0f;
        _demolitionOpponentAssignments[probe] = new DemolitionAssignment(
            probe.Name,
            DemolitionDuty.Defuse,
            0,
            "site_a",
            "enemy response diagnostic");
        _demolitionCombatBreakoffs.Remove(probe);

        // A prone actor must lose horizontal momentum immediately. The objective motor
        // must then stand it before selecting any movement velocity or channeling.
        probe.GlobalPosition = probePosition + Vector3.Right * 4.5f;
        probe.Velocity = Vector3.Right * 5.2f;
        probe.SetProne(true);
        var proneStopsMomentum = probe.IsProne
            && HorizontalSpeed(probe.Velocity) <= 0.001f;
        var objectiveHandledProne = TryHandleDemolitionDefenderMovement(
            probe,
            0.05f,
            combatTarget: null,
            targetVisible: false);
        var objectiveForcesStanding = objectiveHandledProne
            && !probe.IsProne;
        var noObjectiveProneSlide = !probe.IsProne
            || HorizontalSpeed(probe.Velocity) <= 0.001f;

        // Exercise the production demolition guard while the real pursuit ladder motor
        // owns the operator transform. Objective logic must leave position, velocity,
        // and channel progress untouched until traversal releases ownership.
        var ladderBottom = probePosition;
        var ladderTop = ladderBottom + Vector3.Up * 4.0f;
        probe.GlobalPosition = ladderBottom;
        probe.Velocity = Vector3.Zero;
        var ladderAdvanced = probe.AdvancePursuitLadderForDiagnostics(
            1.0f / 60.0f,
            ladderBottom,
            ladderTop,
            Vector3.Forward);
        var ladderWasActive = ladderAdvanced
            && probe.IsPursuitLadderActiveForDiagnostics;
        probe.Velocity = new Vector3(0.37f, 0.11f, -0.29f);
        var ladderTransformBeforeObjective = probe.GlobalTransform;
        var ladderVelocityBeforeObjective = probe.Velocity;
        var ladderProgressBeforeObjective = _demolitionDefuseProgress;
        var ladderTraversalsBeforeObjective = probe.PursuitLadderTraversalsForDiagnostics;
        var ladderBreakoffBeforeObjective = _demolitionCombatBreakoffs.Contains(probe);
        var objectiveHandledDuringLadder = TryHandleDemolitionDefenderMovement(
            probe,
            0.05f,
            combatTarget: null,
            targetVisible: false);
        var ladderTransformPreserved = probe.GlobalTransform.IsEqualApprox(
            ladderTransformBeforeObjective);
        var ladderVelocityPreserved = probe.Velocity.DistanceSquaredTo(
            ladderVelocityBeforeObjective) <= 0.00000001f;
        var ladderProgressPreserved = Mathf.IsEqualApprox(
            _demolitionDefuseProgress,
            ladderProgressBeforeObjective);
        var ladderActivePreserved = probe.IsPursuitLadderActiveForDiagnostics
            && probe.PursuitLadderTraversalsForDiagnostics == ladderTraversalsBeforeObjective;
        var ladderBreakoffPreserved = _demolitionCombatBreakoffs.Contains(probe)
            == ladderBreakoffBeforeObjective;
        var objectivePreservesLadder = ladderWasActive
            && !objectiveHandledDuringLadder
            && ladderTransformPreserved
            && ladderVelocityPreserved
            && ladderProgressPreserved
            && ladderActivePreserved
            && ladderBreakoffPreserved;
        probe.Velocity = Vector3.Zero;
        for (var frame = 0;
             frame < 480 && probe.IsPursuitLadderActiveForDiagnostics;
             frame++)
        {
            _ = probe.AdvancePursuitLadderForDiagnostics(
                1.0f / 60.0f,
                ladderBottom,
                ladderTop,
                Vector3.Forward);
        }
        var ladderReleased = !probe.IsPursuitLadderActiveForDiagnostics
            && probe.PursuitLadderTraversalsForDiagnostics
                == ladderTraversalsBeforeObjective + 1;

        // Investigation memory alone is not confirmed contact. A hidden, unconfirmed
        // actor must therefore leave a normal defuse channel running.
        probe.GlobalPosition = probePosition;
        probe.Velocity = Vector3.Zero;
        probe.LookAt(probePosition + Vector3.Forward, Vector3.Up);
        probe.ConfigureCombatProbeForDiagnostics(
            0x454E454D595F4149UL,
            playerPosition,
            bypassPlayerProtection: true,
            suppressContactSharing: true);
        probe.GrantFireablePrimaryForDiagnostics(WeaponCatalog.Build(WeaponPlatform.M4A1, 1));
        probe.ProcessMode = ProcessModeEnum.Disabled;
        _demolitionDefuseProgress = 0.31f;
        _demolitionRemaining = 25.0f;
        _demolitionCombatBreakoffs.Remove(probe);
        var hiddenProgressBefore = _demolitionDefuseProgress;
        var hiddenUnconfirmedContinues = TryHandleDemolitionDefenderMovement(
                probe,
                0.05f,
                _player,
                targetVisible: false)
            && _demolitionDefuseProgress > hiddenProgressBefore;

        var accuracyBefore = probe.AccuracyBonus;
        var weaponBefore = WeaponSignature(probe.CarriedWeapon);
        var normalProgress = _demolitionDefuseProgress;
        probe.LookAt(probe.GlobalPosition + Vector3.Forward, Vector3.Up);
        var initialDirection = playerPosition - probe.GlobalPosition;
        initialDirection.Y = 0.0f;
        var initiallyBackTurned = initialDirection.LengthSquared() > 0.01f
            && (-probe.GlobalBasis.Z).Dot(initialDirection.Normalized()) < -0.9f;
        _ = probe.TakeDamage(0.1f, probe.GlobalPosition + Vector3.Up, _player);
        var normalContact = probe.CapturePursuitContactStateForDiagnostics();
        const float noiseDelay = 0.75f;
        probe.AdvancePursuitTimersForDiagnostics(noiseDelay);
        var unrelatedSoundOrigin = probe.GlobalPosition + Vector3.Right * 3.0f;
        probe.HearGunshot(unrelatedSoundOrigin, 20.0f);
        var postNoiseContact = probe.CapturePursuitContactStateForDiagnostics();
        var noisePreservesConfirmedContact = postNoiseContact.ConfirmedPursuitTargetId
                == _player.GetInstanceId()
            && postNoiseContact.ConfirmedCombatContactPosition.IsEqualApprox(playerPosition)
            && postNoiseContact.ConfirmedCombatContactTimer > 2.0f
            && Mathf.IsEqualApprox(
                postNoiseContact.ConfirmedCombatContactTimer,
                normalContact.ConfirmedCombatContactTimer - noiseDelay)
            && postNoiseContact.LastKnownTargetPosition.IsEqualApprox(unrelatedSoundOrigin)
            && probe.HasFreshConfirmedCombatContact;
        var normalHitInterrupts = !TryHandleDemolitionDefenderMovement(
                probe,
                0.05f,
                _player,
                targetVisible: false)
            && Mathf.IsEqualApprox(_demolitionDefuseProgress, normalProgress);
        var normalDamageRecorded = ReferenceEquals(normalContact.CombatTarget, _player)
            && ReferenceEquals(normalContact.RawTarget, _player)
            && normalContact.ConfirmedPursuitTargetId == _player.GetInstanceId()
            && normalContact.RecentDamageThreatTargetId == _player.GetInstanceId()
            && normalContact.RecentDamageThreatTimer > 2.0f
            && probe.HasRecentDamageThreat;

        // Even a last-chance defuser must answer direct damage. This uses another real
        // TakeDamage call so the urgent result cannot pass on stale manual state.
        _demolitionDefuseProgress = 0.43f;
        _demolitionRemaining = DemolitionStrategyPlanner.EstimateDefuseCompletionSeconds(
                0.0f,
                _demolitionDefuseProgress)
            + DemolitionStrategyPlanner.DefuseCommitBufferSeconds - 0.1f;
        probe.LookAt(probe.GlobalPosition + Vector3.Forward, Vector3.Up);
        probe.SetProne(true);
        var responseStartedProne = probe.IsProne;
        _ = probe.TakeDamage(0.1f, probe.GlobalPosition + Vector3.Up, _player);
        var urgentProgress = _demolitionDefuseProgress;
        var urgentHitInterrupts = !TryHandleDemolitionDefenderMovement(
                probe,
                0.05f,
                _player,
                targetVisible: false)
            && Mathf.IsEqualApprox(_demolitionDefuseProgress, urgentProgress);
        var urgentContact = probe.CapturePursuitContactStateForDiagnostics();
        var urgentDamageRecorded = ReferenceEquals(urgentContact.CombatTarget, _player)
            && urgentContact.RecentDamageThreatTargetId == _player.GetInstanceId()
            && urgentContact.RecentDamageThreatTimer > 2.0f;

        // Let the production physics loop own perception, turning, movement, cadence,
        // and FireAtSquad. AttackShotsFired is the oracle so accuracy is not part of this
        // regression gate and the player is healed between frames only for test isolation.
        _demolitionRemaining = 25.0f;
        probe.ArmWeaponForDiagnostics();
        var shotsBefore = probe.AttackShotsFired;
        probe.ProcessMode = ProcessModeEnum.Inherit;
        var facedAttacker = false;
        var stoodDuringResponse = false;
        var proneSlideFrames = 0;
        var responseFrames = 0;
        const int maximumResponseFrames = 210;
        while (responseFrames < maximumResponseFrames
            && (!facedAttacker || probe.AttackShotsFired == shotsBefore))
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            _player.SetHealthForDiagnostics(_player.MaxHealth);
            stoodDuringResponse |= !probe.IsProne;
            if (probe.IsProne && HorizontalSpeed(probe.Velocity) > 0.05f)
            {
                proneSlideFrames++;
            }
            var toPlayer = playerPosition - probe.GlobalPosition;
            toPlayer.Y = 0.0f;
            if (toPlayer.LengthSquared() > 0.01f)
            {
                facedAttacker |= (-probe.GlobalBasis.Z).Dot(toPlayer.Normalized()) > 0.65f;
            }
            responseFrames++;
        }
        probe.ProcessMode = ProcessModeEnum.Disabled;

        var firedAtAttacker = probe.AttackShotsFired > shotsBefore;
        var retainedTarget = ReferenceEquals(probe.EngageTargetNode, _player);
        var clearLineOfSight = retainedTarget
            && probe.HasCurrentTargetLineOfSightForDiagnostics();
        var clearBallisticPath = Ballistics.HasClearShot(
            GetWorld3D(),
            probe.ResolvedShotOriginForDiagnostics,
            _player.HitPoint(HitRegion.Torso),
            _player,
            probe.GetRid());
        var rayHit = "none";
        if (PhysicsRaycast.TryHit(
                GetWorld3D(),
                probe.ResolvedShotOriginForDiagnostics,
                _player.HitPoint(HitRegion.Torso),
                probe.GetRid(),
                uint.MaxValue,
                out var blockingHit))
        {
            rayHit = blockingHit.Collider is Node blockingNode
                ? blockingNode.Name.ToString()
                : blockingHit.Collider?.GetType().Name ?? "unknown";
        }
        var accuracyUnchanged = Mathf.IsEqualApprox(probe.AccuracyBonus, accuracyBefore);
        var weaponUnchanged = WeaponSignature(probe.CarriedWeapon) == weaponBefore;

        // A smoke-covered operator must not become a stationary target. Direct damage
        // gives it one short suppression window while the movement motor drives it away
        // from the cloud center. Shot count is deterministic; hit chance intentionally is not.
        probe.GlobalPosition = probePosition;
        probe.Velocity = Vector3.Zero;
        probe.LookAt(probePosition + Vector3.Forward, Vector3.Up);
        probe.ConfigureCombatProbeForDiagnostics(
            0x534D4F4B455F4149UL,
            playerPosition,
            bypassPlayerProtection: true,
            suppressContactSharing: true);
        probe.GrantFireablePrimaryForDiagnostics(WeaponCatalog.Build(WeaponPlatform.M4A1, 1));
        probe.ProcessMode = ProcessModeEnum.Disabled;
        var responseSmoke = new SmokeGrenade
        {
            Name = "EnemyResponseSmoke",
            Position = probePosition
        };
        AddChild(responseSmoke);
        responseSmoke.Arm(Vector3.Forward);
        responseSmoke.BeginGroundFuseForDiagnostics();
        responseSmoke._PhysicsProcess(0.4f);
        var smokeProbePoint = probePosition + Vector3.Up * 0.9f;
        var smokeContainsProbe = responseSmoke.ContainsPoint(smokeProbePoint);
        var smokeBlocksSight = IsLineObscuredBySmoke(
            probePosition + Vector3.Up * 1.45f,
            playerPosition + Vector3.Up * 1.05f);
        probe.SetFireTimerForDiagnostics(4.2f);
        _ = probe.TakeDamage(0.1f, probe.GlobalPosition + Vector3.Up, _player);
        var smokeThreatRecorded = probe.HasRecentDamageThreat;
        var smokeReactionDelayCompressed = probe.FireTimerForDiagnostics <= 0.35f;
        var smokeLineOfSightRejected = !probe.HasCurrentTargetLineOfSightForDiagnostics();
        var smokeBallisticPathOpen = Ballistics.HasClearShot(
            GetWorld3D(),
            probe.ResolvedShotOriginForDiagnostics,
            _player.HitPoint(HitRegion.Torso),
            _player,
            probe.GetRid());
        var smokeStart = probe.GlobalPosition;
        var smokeStartDistance = HorizontalDistance(smokeStart, responseSmoke.CloudCenter);
        var smokeShotsBefore = probe.AttackShotsFired;
        probe.ProcessMode = ProcessModeEnum.Inherit;
        var smokeResponseFrames = 0;
        const int maximumSmokeResponseFrames = 180;
        while (smokeResponseFrames < maximumSmokeResponseFrames)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            _player.SetHealthForDiagnostics(_player.MaxHealth);
            var displacement = HorizontalDistance(smokeStart, probe.GlobalPosition);
            var cloudDistance = HorizontalDistance(
                probe.GlobalPosition,
                responseSmoke.CloudCenter);
            if (probe.AttackShotsFired > smokeShotsBefore
                && displacement > 0.75f
                && cloudDistance > smokeStartDistance + 0.75f)
            {
                break;
            }
            smokeResponseFrames++;
        }
        probe.ProcessMode = ProcessModeEnum.Disabled;
        var smokeDisplacement = HorizontalDistance(smokeStart, probe.GlobalPosition);
        var smokeEndDistance = HorizontalDistance(probe.GlobalPosition, responseSmoke.CloudCenter);
        var smokeReturnFire = probe.AttackShotsFired > smokeShotsBefore;
        var smokeKeepsMoving = smokeDisplacement > 0.75f;
        var smokeMovesOutward = smokeEndDistance > smokeStartDistance + 0.75f;
        var smokeTargetRetained = ReferenceEquals(probe.EngageTargetNode, _player);
        UnregisterActiveSmokeGrenade(responseSmoke);
        responseSmoke.RemoveFromGroup(SmokeGrenade.ActiveGroupName);
        responseSmoke.QueueFree();

        var valid = proneStopsMomentum
            && coveredCrouch
            && pressuredProne
            && conditionLossStands
            && stanceDimensionsReasonable
            && jumpConditionsRequired
            && invalidJumpStaysGrounded
            && validJumpProducesImpulse
            && airborneShotObserved
            && airborneAimingPoseObserved
            && jumpCooldownPreventsRepeat
            && lowCeilingPreservesPosture
            && clearanceRemovalRestoresStanding
            && weakCrouchFlashMovementBounded
            && weakProneFlashMovementBounded
            && strongFlashSuppressesCombat
            && weakFlashCannotReduceStrongFlash
            && strongerFlashRaisesCurrentIntensity
            && flashKeepsEvading
            && flashDecays
            && flashRecovers
            && objectiveForcesStanding
            && noObjectiveProneSlide
            && objectivePreservesLadder
            && ladderReleased
            && hiddenUnconfirmedContinues
            && initiallyBackTurned
            && normalHitInterrupts
            && normalDamageRecorded
            && noisePreservesConfirmedContact
            && urgentHitInterrupts
            && urgentDamageRecorded
            && responseStartedProne
            && stoodDuringResponse
            && facedAttacker
            && firedAtAttacker
            && proneSlideFrames == 0
            && retainedTarget
            && clearLineOfSight
            && clearBallisticPath
            && accuracyUnchanged
            && weaponUnchanged
            && smokeContainsProbe
            && smokeBlocksSight
            && smokeThreatRecorded
            && smokeReactionDelayCompressed
            && smokeLineOfSightRejected
            && smokeBallisticPathOpen
            && smokeReturnFire
            && smokeKeepsMoving
            && smokeMovesOutward
            && smokeTargetRetained;

        GD.Print($"DEMOLITION_ENEMY_RESPONSE_CHECK valid={valid} posture_cover={coveredCrouch} posture_pressure={pressuredProne} posture_recover={conditionLossStands} dimensions={stanceDimensionsReasonable}:{standingDimensions.ColliderHeight:0.00}/{crouchedDimensions.ColliderHeight:0.00}/{proneDimensions.ColliderHeight:0.00} eyes={standingDimensions.EyeHeight:0.00}/{crouchedDimensions.EyeHeight:0.00}/{proneDimensions.EyeHeight:0.00} clearance_hold={lowCeilingPreservesPosture} clearance_release={clearanceRemovalRestoresStanding} jump_gates={jumpConditionsRequired} jump_invalid={invalidJumpStaysGrounded} jump_impulse={validJumpProducesImpulse}:{jumpImpulseY:0.00} jump_fired={airborneShotObserved}:{airborneResponseFrames}/{maximumAirborneResponseFrames} jump_pose={airborneAimingPoseObserved}:{airborneAnimation} jump_cooldown={jumpCooldownPreventsRepeat} weak_flash_crouch={weakCrouchFlashMovementBounded}:{weakCrouchFlashSpeed:0.00} weak_flash_prone={weakProneFlashMovementBounded}:{weakProneFlashSpeed:0.00} flash_suppressed={strongFlashSuppressesCombat} flash_stack={weakFlashCannotReduceStrongFlash}:{stackedFlashBeforeWeak.FlashIntensity:0.00}/{stackedFlashBeforeWeak.FlashRemaining:0.00}->{stackedFlashAfterWeak.FlashIntensity:0.00}/{stackedFlashAfterWeak.FlashRemaining:0.00}->{stackedFlashAfterStronger.FlashIntensity:0.00}/{stackedFlashAfterStronger.FlashRemaining:0.00}:{strongerFlashRaisesCurrentIntensity} flash_move={flashKeepsEvading} flash_decay={flashDecays}:{strongFlashState.FlashIntensity:0.00}->{fadingFlashState.FlashIntensity:0.00} flash_recover={flashRecovers} prone_stop={proneStopsMomentum} objective_stand={objectiveForcesStanding} objective_no_slide={noObjectiveProneSlide} ladder_guard={objectivePreservesLadder} ladder_transform={ladderTransformPreserved} ladder_velocity={ladderVelocityPreserved} ladder_progress={ladderProgressPreserved} ladder_active={ladderActivePreserved} ladder_breakoff={ladderBreakoffPreserved} ladder_released={ladderReleased} hidden_continues={hiddenUnconfirmedContinues} initially_back={initiallyBackTurned} normal_interrupt={normalHitInterrupts} normal_damage={normalDamageRecorded} noise_isolated={noisePreservesConfirmedContact} urgent_interrupt={urgentHitInterrupts} urgent_damage={urgentDamageRecorded} response_prone={responseStartedProne} stood={stoodDuringResponse} faced={facedAttacker} fired={firedAtAttacker} shots={probe.AttackShotsFired - shotsBefore} prone_slide_frames={proneSlideFrames} response_frames={responseFrames}/{maximumResponseFrames} target={retainedTarget} los={clearLineOfSight} ballistic={clearBallisticPath} ray_hit={rayHit} accuracy_unchanged={accuracyUnchanged} weapon_unchanged={weaponUnchanged} smoke_inside={smokeContainsProbe} smoke_block={smokeBlocksSight} smoke_los_rejected={smokeLineOfSightRejected} smoke_ballistic={smokeBallisticPathOpen} smoke_threat={smokeThreatRecorded} smoke_delay={smokeReactionDelayCompressed} smoke_fired={smokeReturnFire} smoke_move={smokeKeepsMoving}:{smokeDisplacement:0.00} smoke_outward={smokeMovesOutward}:{smokeStartDistance:0.00}->{smokeEndDistance:0.00} smoke_target={smokeTargetRetained} smoke_frames={smokeResponseFrames}/{maximumSmokeResponseFrames} recent_timer={urgentContact.RecentDamageThreatTimer:0.00}");
        GD.Print($"DEMOLITION_ENEMY_RESPONSE_PASS valid={valid}");
        platform.QueueFree();
        QuitDiagnosticAfterSceneCleanup(valid ? 0 : 2);
    }

    private StaticBody3D CreateDemolitionEnemyResponsePlatform(Vector3 center)
    {
        var body = new StaticBody3D
        {
            Name = "DemolitionEnemyResponsePlatform",
            Position = center,
            CollisionLayer = 1,
            CollisionMask = 0
        };
        body.AddChild(new CollisionShape3D
        {
            Name = "Collision",
            Shape = new BoxShape3D { Size = new Vector3(36.0f, 0.4f, 36.0f) }
        });
        AddChild(body);
        return body;
    }

    private static float HorizontalSpeed(Vector3 velocity)
        => new Vector2(velocity.X, velocity.Z).Length();

    private static string WeaponSignature(WeaponBuild weapon)
        => $"{weapon.Platform}:{string.Join(',', weapon.Attachments.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}={pair.Value}"))}";
}
