using System.Threading.Tasks;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private async void ValidateTrainingRange()
    {
        await WaitFrames(18);
        // The public entry now opens a modal setup surface first.  Validate that the
        // panel really is a standalone venue before exercising the live-fire loop.
        var arena = _trainingRangeArena;
        var setupVisible = _hud.IsTrainingRangeSetupVisible;
        var setupReady = setupVisible
            && !_trainingRangeActive
            && TrainingRangeSetupIsPending
            && GetTree().Paused
            && _hud.TrainingRangeSetupUiReady
            && _hud.TrainingRangeSetupUsesPackedScene
            && _hud.TrainingRangeSetupSelectionContractReady
            && _hud.TrainingRangeSetupLanguageReady
            && _hud.TrainingRangeSetupIntentSignalsConnected;
        var arenaReady = arena is not null
            && TrainingRangeArenaReady
            && arena.Active
            && arena.BotSpawnCount >= 24
            && arena.Stations.Count == 3
            && arena.AuthoredModelCount >= 20
            && arena.CollisionBodyCount >= 5
            && arena.CollisionLifecycleIsValid()
            && !_levelRoot.Visible
            && _levelRoot.ProcessMode == ProcessModeEnum.Disabled;

        // Drive the same selectors a player uses.  This catches a broken payload or a
        // button that only changes its label without changing the world configuration.
        _hud.SelectTrainingRangeBotTypeForDiagnostics(0);
        _hud.SelectTrainingRangeBotCountForDiagnostics(12);
        _hud.SelectTrainingRangeWeaponForDiagnostics(7); // AXMC
        _hud.SelectTrainingRangeAmmoForDiagnostics(1, 3); // AP, T4
        var selectionReady = _hud.SelectedTrainingRangeBotType == 0
            && _hud.SelectedTrainingRangeBotCount == 12
            && _hud.SelectedTrainingRangeWeaponIndex == 7
            && _hud.SelectedTrainingRangeAmmoType == 1
            && _hud.SelectedTrainingRangeAmmoLevel == 3;
        _hud.PressTrainingRangeSetupDeployForDiagnostics();
        await WaitFrames(12);

        var started = _trainingRangeActive
            && _hud.IsGameplayHudVisible
            && !_hud.IsOperationsOfficeVisible
            && DedicatedTrainingRangeIsIsolated()
            && TrainingRangeBotCount == 12
            && TrainingRangeConfiguredBotCount == 12
            && TrainingRangeConfiguredWeaponIndex == 7
            && TrainingRangeConfiguredAmmoType == 1
            && TrainingRangeConfiguredAmmoLevel == 3
            && _player.HasActiveFirearm
            && _player.TrainingRangeWeaponPlatform == TacticalPlayer.TrainingRangeWeaponAt(7);
        var initialWeapon = _player.TrainingRangeWeaponPlatform;
        var initialAmmo = _player.Ammo;
        var infiniteAmmo = _player.ReserveAmmo >= 9999
            && initialAmmo == _player.CurrentWeaponStats.MagazineSize
            && _player.TrainingRangeAmmoType == 1
            && _player.TrainingRangeAmmoLevel == 3;

        _player.CycleTrainingRangeWeapon();
        var weaponCycle = _player.TrainingRangeWeaponPlatform != initialWeapon
            && _player.Ammo == _player.CurrentWeaponStats.MagazineSize
            && _player.ReserveAmmo >= 9999
            && _player.CurrentAmmoGrade == _player.TrainingRangeAmmoGrade;

        var stationsReady = arena is not null
            && arena.Stations.Count == 3
            && arena.IsStationInRange(arena.Stations[0].Position, TrainingRangeStationKind.Weapon)
            && arena.IsStationInRange(arena.Stations[1].Position, TrainingRangeStationKind.Ammunition)
            && arena.IsStationInRange(arena.Stations[2].Position, TrainingRangeStationKind.BotControl);

        var target = _trainingRangeBotSlots.Count > 0 ? _trainingRangeBotSlots[0].Bot : null;
        var targetId = target?.GetInstanceId() ?? 0UL;
        var targetHit = target is not null
            && target.TakeDamage(10000.0f, target.GlobalPosition + Vector3.Up * 1.4f, _player);
        await WaitFrames(2);
        var downed = targetHit
            && target is not null
            && target.IsDead
            && target.Visible
            && _trainingRangeBotSlots.Count > 0
            && _trainingRangeBotSlots[0].IsDowned
            && _trainingRangeBotSlots[0].RespawnPending;
        var reviveFrames = 0;
        while (reviveFrames < 720
            && (target is not null && target.IsDead || TrainingRangeBotCount < 12))
        {
            await WaitFrames(1);
            reviveFrames++;
        }
        var revivedTarget = _trainingRangeBotSlots.Count > 0 ? _trainingRangeBotSlots[0].Bot : null;
        var respawned = targetHit
            && downed
            && revivedTarget is not null
            && revivedTarget.GetInstanceId() == targetId
            && !revivedTarget.IsDead
            && TrainingRangeBotCount == 12
            && TrainingRangeKills == 1;

        // Open and close the same panel through a station context.  This is the
        // interaction contract used by F in the actual arena, without synthesizing a
        // keyboard event in a headless validator.
        _hud.ShowTrainingRangeStation((int)TrainingRangeStationKind.Weapon, "ARMORY");
        await WaitFrames(2);
        var stationPanel = _hud.IsTrainingRangeSetupVisible
            && _hud.TrainingRangeSetupOpenedFromGameplay
            && _hud.TrainingRangeSetupStationContext == (int)TrainingRangeStationKind.Weapon
            && GetTree().Paused;
        _hud.PressTrainingRangeSetupBackForDiagnostics();
        await WaitFrames(2);
        var stationResume = !_hud.IsTrainingRangeSetupVisible
            && _trainingRangeActive
            && !GetTree().Paused
            && _hud.IsGameplayHudVisible
            && _hud.TrainingRangeSetupStationContext == -1;

        var valid = setupReady
            && arenaReady
            && selectionReady
            && started
            && stationsReady
            && infiniteAmmo
            && weaponCycle
            && downed
            && respawned
            && stationPanel
            && stationResume;
        GD.Print($"TRAINING_RANGE_CHECK valid={valid} setup={setupReady} arena={arenaReady} selection={selectionReady} started={started} stations={stationsReady} infinite_ammo={infiniteAmmo} weapon_cycle={weaponCycle} target_hit={targetHit} downed={downed} respawned={respawned} revive_frames={reviveFrames} station_panel={stationPanel} station_resume={stationResume} target_id={targetId} bots={TrainingRangeBotCount} kills={TrainingRangeKills}");
        GD.Print($"TRAINING_RANGE_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }

    private async void CaptureTrainingRangeSetup(bool deployed)
    {
        // Capture both the pre-deploy configuration screen and the first-person range
        // spawn.  These are local QA artifacts; they are not gameplay UI shortcuts.
        SetCaptureLanguage("zh");
        OnTrainingRangeRequested();
        await WaitFrames(18);
        if (!deployed)
        {
            SaveViewportImage("res://training_range_setup_validation.png");
            GD.Print("TRAINING_RANGE_CAPTURE state=setup path=training_range_setup_validation.png");
            GetTree().Paused = false;
            GetTree().Quit();
            return;
        }

        _hud.SelectTrainingRangeBotTypeForDiagnostics(1);
        _hud.SelectTrainingRangeBotCountForDiagnostics(12);
        _hud.SelectTrainingRangeWeaponForDiagnostics(0);
        _hud.SelectTrainingRangeAmmoForDiagnostics(0, 2);
        _hud.PressTrainingRangeSetupDeployForDiagnostics();
        await WaitFrames(30);
        var captureCamera = _player.GetNodeOrNull<Camera3D>("Head/CombatCamera");
        captureCamera?.MakeCurrent();
        if (captureCamera is not null)
        {
            var captureHead = _player.GetNode<Node3D>("Head");
            GD.Print($"TRAINING_RANGE_CAMERA player={_player.GlobalPosition} camera={captureCamera.GlobalPosition} camera_rot={captureCamera.GlobalRotation} head={captureHead.GlobalRotation}");
        }
        SaveViewportImage("res://training_range_validation.png");
        GD.Print("TRAINING_RANGE_CAPTURE state=live path=training_range_validation.png");
        GetTree().Quit();
    }
}
