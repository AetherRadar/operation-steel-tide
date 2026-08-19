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
        var authoritativeDown = down || bodyBag || health <= 0.0f;
        var wasDown = _localPlayerDowned;
        var wasEliminated = _localPlayerEliminated;
        _player.ApplyExtractionNetworkHealth(
            health,
            authoritativeDown,
            reviveUsed || bodyBag);
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
        if (authoritativeDown)
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

    internal void OnSquadMateDamageApplied(
        SquadMate mate,
        float appliedDamage,
        HitRegion region,
        Vector3 hitPosition,
        Node? attacker)
    {
        if (!IsExtractionNetworkMatch || !_squadNetwork.IsHost
            || !mate.IsHumanProxy || mate.NetworkPeerId <= 1
            || appliedDamage <= 0.0f && !mate.IsDowned && !mate.IsBodyBag)
        {
            return;
        }
        var sourcePosition = attacker is Node3D sourceNode && IsInstanceValid(sourceNode)
            ? sourceNode.GlobalPosition
            : hitPosition;
        var source = attacker switch
        {
            EnemyOperator => ExtractionDamageSourceKind.EnemyOperator,
            DestructibleAircraft or AircraftShell => ExtractionDamageSourceKind.AircraftStrike,
            ExplosiveBarrel or FragGrenade => ExtractionDamageSourceKind.Explosion,
            DriveableVehicle => ExtractionDamageSourceKind.Vehicle,
            _ => ExtractionDamageSourceKind.Environment
        };
        _squadNetwork.SendExtractionPlayerDamage(
            mate.NetworkPeerId,
            new ExtractionPlayerDamageNetworkEvent(
                unchecked(_extractionWorldSequence + 1),
                appliedDamage,
                mate.Health,
                region,
                sourcePosition,
                source,
                mate.IsDowned,
                mate.IsBodyBag,
                mate.ReviveUsed));
    }

    private void OnExtractionPlayerDamage(ExtractionPlayerDamageNetworkEvent damageEvent)
    {
        if (!IsExtractionNetworkClient)
        {
            return;
        }
        _player.ApplyExtractionNetworkDamageFeedback(
            damageEvent.AppliedDamage,
            damageEvent.Region,
            damageEvent.SourcePosition,
            damageEvent.Source);
        if (_lastExtractionWorldSequence < damageEvent.StateSequence)
        {
            _minimumExtractionWorldSequence = System.Math.Max(
                _minimumExtractionWorldSequence,
                damageEvent.StateSequence);
            ApplyExtractionLocalPlayerState(
                damageEvent.Health,
                damageEvent.Down,
                damageEvent.BodyBag,
                damageEvent.ReviveUsed);
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
