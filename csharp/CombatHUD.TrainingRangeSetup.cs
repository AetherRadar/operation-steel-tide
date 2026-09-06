using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public partial class CombatHUD
{
    private const string TrainingRangeSetupViewScenePath = "res://ui/TrainingRangeSetupView.tscn";
    private const string TrainingRangeArmoryViewScenePath = "res://ui/TrainingRangeArmoryView.tscn";

    [Signal] public delegate void TrainingRangeDeployRequestedEventHandler(
        int botType,
        int botCount,
        int weaponIndex,
        int ammoType,
        int ammoLevel);
    [Signal] public delegate void TrainingRangeSetupBackRequestedEventHandler();
    [Signal] public delegate void TrainingRangeExitRequestedEventHandler();
    [Signal] public delegate void TrainingRangeSetupOpenedEventHandler(bool fromGameplay);
    [Signal] public delegate void TrainingRangeSetupClosedEventHandler(bool applied);

    private TrainingRangeSetupView? _trainingRangeSetupView;
    private TrainingRangeArmoryView? _trainingRangeArmoryView;
    private bool _trainingRangeGameplayInputEnabled;
    private bool _trainingRangeSetupOpen;

    public bool IsTrainingRangeSetupVisible
        => (IsInstanceValid(_trainingRangeSetupView) && _trainingRangeSetupView.Visible)
        || (IsInstanceValid(_trainingRangeArmoryView) && _trainingRangeArmoryView.Visible);
    public bool TrainingRangeSetupUiReady
        => IsInstanceValid(_trainingRangeSetupView) && _trainingRangeSetupView.UiReady;
    public bool TrainingRangeSetupUsesPackedScene
        => IsInstanceValid(_trainingRangeSetupView)
        && _trainingRangeSetupView.SceneFilePath == TrainingRangeSetupViewScenePath;
    public bool TrainingRangeArmoryUiReady
        => IsInstanceValid(_trainingRangeArmoryView) && _trainingRangeArmoryView.UiReady;
    public bool TrainingRangeArmoryIntentSignalsConnected
        => IsInstanceValid(_trainingRangeArmoryView) && _trainingRangeArmoryView.IntentBindingsReady;
    public bool TrainingRangeArmoryPreviewRenderableForDiagnostics
        => IsInstanceValid(_trainingRangeArmoryView)
        && _trainingRangeArmoryView.PreviewRenderableForDiagnostics;
    public bool TrainingRangeSetupSelectionContractReady
        => IsInstanceValid(_trainingRangeSetupView)
        && _trainingRangeSetupView.SelectionContractReady;
    public bool TrainingRangeSetupLanguageReady
        => IsInstanceValid(_trainingRangeSetupView)
        && _trainingRangeSetupView.LanguageMatches(_language);
    public bool TrainingRangeSetupIntentSignalsConnected
        => IsInstanceValid(_trainingRangeSetupView)
        && _trainingRangeSetupView.IntentSignalsConnected
        && HasConnections(SignalName.TrainingRangeDeployRequested)
        && HasConnections(SignalName.TrainingRangeSetupBackRequested)
        && HasConnections(SignalName.TrainingRangeExitRequested);
    public bool TrainingRangeSetupOpenedFromGameplay
        => _trainingRangeSetupOpen
        && ((IsInstanceValid(_trainingRangeSetupView) && _trainingRangeSetupView.IsInGameplay)
            || (IsInstanceValid(_trainingRangeArmoryView) && _trainingRangeArmoryView.Visible));
    public int SelectedTrainingRangeBotType
        => IsInstanceValid(_trainingRangeSetupView)
            ? _trainingRangeSetupView.SelectedBotType
            : 0;
    public int SelectedTrainingRangeBotCount
        => IsInstanceValid(_trainingRangeSetupView)
            ? _trainingRangeSetupView.SelectedBotCount
            : 6;
    public int SelectedTrainingRangeWeaponIndex
        => IsInstanceValid(_trainingRangeSetupView)
            ? _trainingRangeSetupView.SelectedWeaponIndex
            : 0;
    public WeaponPlatform SelectedTrainingRangeWeapon
        => IsInstanceValid(_trainingRangeSetupView)
            ? _trainingRangeSetupView.SelectedWeaponPlatform
            : WeaponPlatform.M4A1;
    public int SelectedTrainingRangeAmmoType
        => IsInstanceValid(_trainingRangeSetupView)
            ? _trainingRangeSetupView.SelectedAmmoType
            : 0;
    public int SelectedTrainingRangeAmmoLevel
        => IsInstanceValid(_trainingRangeSetupView)
            ? _trainingRangeSetupView.SelectedAmmoLevel
            : 2;
    public int TrainingRangeSetupStationContext
        => IsInstanceValid(_trainingRangeArmoryView) && _trainingRangeArmoryView.Visible
            ? 0
            : IsInstanceValid(_trainingRangeSetupView)
            ? _trainingRangeSetupView.StationContext
            : -1;
    public string?[] SelectedTrainingRangeAttachmentIds
        => IsInstanceValid(_trainingRangeSetupView)
            ? _trainingRangeSetupView.SelectedAttachmentIds
            : new string?[6];
    public void SetTrainingRangeAttachmentSelections(IReadOnlyList<string?> ids)
        => _trainingRangeSetupView?.SetAttachmentSelections(ids);

    /// <summary>
    /// Opens the range configuration panel.  <paramref name="fromGameplay"/> keeps
    /// the current selections and changes the action label to APPLY; the world may
    /// pause its simulation after receiving <see cref="TrainingRangeSetupOpened"/>.
    /// </summary>
    public void ShowTrainingRangeSetup(string status = "", bool fromGameplay = false)
    {
        EnsureTrainingRangeSetupView();
        if (IsInstanceValid(_trainingRangeArmoryView))
        {
            _trainingRangeArmoryView.Visible = false;
        }
        if (!fromGameplay)
        {
            _trainingRangeSetupView!.SetStationContext(-1);
        }
        _trainingRangeSetupView!.SetInGameplay(fromGameplay);
        _trainingRangeSetupView.SetLanguage(_language);
        _trainingRangeSetupView.Visible = true;
        _trainingRangeSetupOpen = true;
        _trainingRangeGameplayInputEnabled = fromGameplay || _trainingRangeGameplayInputEnabled;

        if (IsInstanceValid(_operationsOfficeRoot))
        {
            _operationsOfficeRoot.Visible = false;
        }
        if (IsInstanceValid(_demolitionBriefingView))
        {
            _demolitionBriefingView.Visible = false;
        }
        if (IsInstanceValid(_squadLobby))
        {
            _squadLobby.Visible = false;
        }
        if (IsInstanceValid(_gameplayHudRoot))
        {
            _gameplayHudRoot.Visible = false;
        }
        if (IsInstanceValid(_classSkillRoot))
        {
            _classSkillRoot.Visible = false;
        }
        if (IsInstanceValid(_squadRoster))
        {
            _squadRoster.Visible = false;
        }
        if (!string.IsNullOrWhiteSpace(status))
        {
            _trainingRangeSetupView.SetMeta("status", status);
        }
        Input.MouseMode = Input.MouseModeEnum.Visible;
        _trainingRangeSetupView.GrabDefaultFocus();
        EmitSignal(SignalName.TrainingRangeSetupOpened, fromGameplay);
    }

    /// <summary>
    /// Opens the same setup surface from an in-world station.  Station kinds are
    /// intentionally passed as integers so the HUD stays independent of the arena
    /// runtime assembly: 0 weapon bench, 1 ammunition bench, 2 bot control.
    /// </summary>
    public void ShowTrainingRangeStation(int stationKind, string status = "", WeaponBuild? build = null)
    {
        EnsureTrainingRangeSetupView();
        if (stationKind == 0)
        {
            EnsureTrainingRangeArmoryView();
            _trainingRangeSetupView!.Visible = false;
            _trainingRangeArmoryView!.Configure(
                build ?? WeaponCatalog.BuildTrainingRangeDefault(SelectedTrainingRangeWeapon),
                _language);
            _trainingRangeArmoryView.SetLanguage(_language);
            _trainingRangeArmoryView.Visible = true;
            _trainingRangeSetupOpen = true;
            _trainingRangeGameplayInputEnabled = true;
            HideTrainingRangeOverlayRoots();
            if (!string.IsNullOrWhiteSpace(status))
            {
                _trainingRangeArmoryView.SetMeta("status", status);
            }
            Input.MouseMode = Input.MouseModeEnum.Visible;
            EmitSignal(SignalName.TrainingRangeSetupOpened, true);
            return;
        }
        _trainingRangeSetupView!.SetStationContext(stationKind);
        ShowTrainingRangeSetup(status, fromGameplay: true);
    }

    public void HideTrainingRangeSetup(bool applied = false)
    {
        if (!IsInstanceValid(_trainingRangeSetupView))
        {
            return;
        }
        var wasVisible = _trainingRangeSetupView.Visible;
        _trainingRangeSetupView.Visible = false;
        if (IsInstanceValid(_trainingRangeArmoryView))
        {
            wasVisible |= _trainingRangeArmoryView.Visible;
            _trainingRangeArmoryView.Visible = false;
        }
        _trainingRangeSetupOpen = false;
        // A station context only belongs to the panel instance opened from that
        // bench.  Clear it on close so the next F3 global panel starts at the full
        // range configuration instead of inheriting a stale station heading/focus.
        _trainingRangeSetupView.SetStationContext(-1);
        if (wasVisible)
        {
            EmitSignal(SignalName.TrainingRangeSetupClosed, applied);
        }
    }

    public void SetTrainingRangeGameplayInputEnabled(bool active)
        => _trainingRangeGameplayInputEnabled = active;

    private void RefreshTrainingRangeSetupLanguage()
    {
        if (IsInstanceValid(_trainingRangeSetupView))
        {
            _trainingRangeSetupView.SetLanguage(_language);
        }
    }

    public void SetTrainingRangeSetupSelections(
        int botType,
        int botCount,
        int weaponIndex,
        int ammoType,
        int ammoLevel)
    {
        EnsureTrainingRangeSetupView();
        _trainingRangeSetupView!.SetSelections(
            botType,
            botCount,
            weaponIndex,
            ammoType,
            ammoLevel);
    }

    public void PressTrainingRangeSetupDeployForDiagnostics()
    {
        if (IsInstanceValid(_trainingRangeArmoryView) && _trainingRangeArmoryView.Visible)
        {
            _trainingRangeArmoryView.PressApplyForDiagnostics();
        }
        else
        {
            _trainingRangeSetupView?.PressDeployForDiagnostics();
        }
    }

    public void PressTrainingRangeSetupBackForDiagnostics()
    {
        if (IsInstanceValid(_trainingRangeArmoryView) && _trainingRangeArmoryView.Visible)
            _trainingRangeArmoryView.PressBackForDiagnostics();
        else
            _trainingRangeSetupView?.PressBackForDiagnostics();
    }

    public void SelectTrainingRangeBotTypeForDiagnostics(int value)
        => _trainingRangeSetupView?.SelectBotTypeForDiagnostics(value);

    public void SelectTrainingRangeBotCountForDiagnostics(int count)
        => _trainingRangeSetupView?.SelectBotCountForDiagnostics(count);

    public void SelectTrainingRangeWeaponForDiagnostics(int index)
        => _trainingRangeSetupView?.SelectWeaponForDiagnostics(index);

    public void SelectTrainingRangeAmmoForDiagnostics(int type, int level)
        => _trainingRangeSetupView?.SelectAmmoForDiagnostics(type, level);

    public override void _Input(InputEvent @event)
    {
        if (!_trainingRangeGameplayInputEnabled
            || IsTrainingRangeSetupVisible
            || IsLootVisible
            || IsPauseMenuVisible)
        {
            return;
        }
        if (@event is InputEventKey key
            && key.Pressed
            && !key.Echo
            && key.Keycode == Key.F3)
        {
            ShowTrainingRangeSetup(
                Text("training_setup_status_reopen", "RANGE CONFIGURATION"),
                fromGameplay: true);
            GetViewport().SetInputAsHandled();
        }
    }

    private void EnsureTrainingRangeSetupView()
    {
        if (IsInstanceValid(_trainingRangeSetupView))
        {
            return;
        }
        _trainingRangeSetupView = HudPackedSceneCache.Instantiate<TrainingRangeSetupView>(
            TrainingRangeSetupViewScenePath);
        AddChild(_trainingRangeSetupView);
        _trainingRangeSetupView.BackRequested += HandleTrainingRangeSetupBack;
        _trainingRangeSetupView.ExitRequested += HandleTrainingRangeSetupExit;
        _trainingRangeSetupView.DeployRequested += HandleTrainingRangeSetupDeploy;
        // The pause menu emits this signal before the world applies the setting;
        // mirroring it here keeps a hidden setup panel localized after a language swap.
        LanguageChanged += language => _trainingRangeSetupView?.SetLanguage(language);
        _trainingRangeSetupView.SetLanguage(_language);
    }

    private void EnsureTrainingRangeArmoryView()
    {
        if (IsInstanceValid(_trainingRangeArmoryView))
        {
            return;
        }
        _trainingRangeArmoryView = HudPackedSceneCache.Instantiate<TrainingRangeArmoryView>(
            TrainingRangeArmoryViewScenePath);
        AddChild(_trainingRangeArmoryView);
        _trainingRangeArmoryView.BackRequested += HandleTrainingRangeArmoryBack;
        _trainingRangeArmoryView.ApplyRequested += HandleTrainingRangeArmoryApply;
        LanguageChanged += language => _trainingRangeArmoryView?.SetLanguage(language);
        _trainingRangeArmoryView.SetLanguage(_language);
    }

    private void HideTrainingRangeOverlayRoots()
    {
        if (IsInstanceValid(_operationsOfficeRoot)) _operationsOfficeRoot.Visible = false;
        if (IsInstanceValid(_demolitionBriefingView)) _demolitionBriefingView.Visible = false;
        if (IsInstanceValid(_squadLobby)) _squadLobby.Visible = false;
        if (IsInstanceValid(_gameplayHudRoot)) _gameplayHudRoot.Visible = false;
        if (IsInstanceValid(_classSkillRoot)) _classSkillRoot.Visible = false;
        if (IsInstanceValid(_squadRoster)) _squadRoster.Visible = false;
    }

    private void HandleTrainingRangeArmoryBack()
    {
        HideTrainingRangeSetup(applied: false);
        RestoreTrainingRangeGameplayHud();
        Input.MouseMode = Input.MouseModeEnum.Captured;
        EmitSignal(SignalName.TrainingRangeSetupBackRequested);
    }

    private void HandleTrainingRangeArmoryApply()
    {
        if (!IsInstanceValid(_trainingRangeArmoryView)) return;
        var build = _trainingRangeArmoryView.CurrentBuild;
        var platforms = new[]
        {
            WeaponPlatform.M4A1, WeaponPlatform.AK74, WeaponPlatform.ScarL,
            WeaponPlatform.MP5A5, WeaponPlatform.M3A1, WeaponPlatform.VSS,
            WeaponPlatform.M24, WeaponPlatform.AXMC, WeaponPlatform.AWM,
            WeaponPlatform.P226, WeaponPlatform.M1911, WeaponPlatform.DesertEagle,
            WeaponPlatform.GSh18
        };
        var weaponIndex = Math.Max(0, Array.IndexOf(platforms, build.Platform));
        SetTrainingRangeSetupSelections(
            SelectedTrainingRangeBotType,
            SelectedTrainingRangeBotCount,
            weaponIndex,
            SelectedTrainingRangeAmmoType,
            SelectedTrainingRangeAmmoLevel);
        var ids = new string?[6];
        foreach (var pair in build.Attachments)
        {
            var index = pair.Key switch
            {
                AttachmentSlot.Optic => 0,
                AttachmentSlot.Barrel => 1,
                AttachmentSlot.Muzzle => 2,
                AttachmentSlot.Grip => 3,
                AttachmentSlot.Stock => 4,
                AttachmentSlot.Magazine => 5,
                _ => -1
            };
            if (index >= 0) ids[index] = pair.Value;
        }
        SetTrainingRangeAttachmentSelections(ids);
        HideTrainingRangeSetup(applied: true);
        RestoreTrainingRangeGameplayHud();
        Input.MouseMode = Input.MouseModeEnum.Captured;
        EmitSignal(
            SignalName.TrainingRangeDeployRequested,
            SelectedTrainingRangeBotType,
            SelectedTrainingRangeBotCount,
            weaponIndex,
            SelectedTrainingRangeAmmoType,
            SelectedTrainingRangeAmmoLevel);
    }

    private void HandleTrainingRangeSetupBack()
    {
        var fromGameplay = TrainingRangeSetupOpenedFromGameplay;
        HideTrainingRangeSetup(applied: false);
        if (fromGameplay)
        {
            RestoreTrainingRangeGameplayHud();
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }
        EmitSignal(SignalName.TrainingRangeSetupBackRequested);
    }

    private void HandleTrainingRangeSetupDeploy(
        int botType,
        int botCount,
        int weaponIndex,
        int ammoType,
        int ammoLevel)
    {
        var fromGameplay = TrainingRangeSetupOpenedFromGameplay;
        HideTrainingRangeSetup(applied: true);
        if (fromGameplay)
        {
            RestoreTrainingRangeGameplayHud();
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }
        EmitSignal(
            SignalName.TrainingRangeDeployRequested,
            botType,
            botCount,
            weaponIndex,
            ammoType,
            ammoLevel);
    }

    private void HandleTrainingRangeSetupExit()
    {
        HideTrainingRangeSetup(applied: false);
        Input.MouseMode = Input.MouseModeEnum.Visible;
        EmitSignal(SignalName.TrainingRangeExitRequested);
    }

    private void RestoreTrainingRangeGameplayHud()
    {
        if (IsInstanceValid(_gameplayHudRoot))
        {
            _gameplayHudRoot.Visible = true;
        }
        KeepTrainingRangeOverlaysHidden();
    }

}
