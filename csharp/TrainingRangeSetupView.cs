using System;
using Godot;

namespace OperationSteelTide;

/// <summary>
/// Configuration surface for the standalone live-fire range.  The view owns only
/// selection state and emits intent; the world interprets the integer payloads.
/// Payload contract: bot type (0 static, 1 patrol, 2 reactive), bot count (3/6/12/24),
/// weapon index (the authored range lineup below), ammunition type (0 FMJ, 1 AP,
/// 2 hollow-point, 3 tracer), and ammunition grade (0-3).
/// </summary>
[GlobalClass]
public partial class TrainingRangeSetupView : ColorRect
{
    [Signal] public delegate void BackRequestedEventHandler();
    [Signal] public delegate void ExitRequestedEventHandler();
    [Signal] public delegate void DeployRequestedEventHandler(
        int botType,
        int botCount,
        int weaponIndex,
        int ammoType,
        int ammoLevel);

    private static readonly int[] BotCounts = { 3, 6, 12, 24 };
    private static readonly WeaponPlatform[] RangeWeapons =
    {
        WeaponPlatform.M4A1,
        WeaponPlatform.AK74,
        WeaponPlatform.ScarL,
        WeaponPlatform.MP5A5,
        WeaponPlatform.M3A1,
        WeaponPlatform.VSS,
        WeaponPlatform.M24,
        WeaponPlatform.AXMC,
        WeaponPlatform.AWM,
        WeaponPlatform.P226,
        WeaponPlatform.M1911,
        WeaponPlatform.DesertEagle,
        WeaponPlatform.GSh18
    };

    private Label _section = null!;
    private Label _title = null!;
    private Label _subtitle = null!;
    private Label _botCaption = null!;
    private Label _botTypeCaption = null!;
    private Label _botCountCaption = null!;
    private Label _botHint = null!;
    private Label _weaponCaption = null!;
    private Label _weaponSelectCaption = null!;
    private Label _weaponHint = null!;
    private Label _ammoCaption = null!;
    private Label _ammoTypeCaption = null!;
    private Label _ammoLevelCaption = null!;
    private Label _ammoHint = null!;
    private OptionButton _botTypeSelect = null!;
    private OptionButton _botCountSelect = null!;
    private OptionButton _weaponSelect = null!;
    private OptionButton _ammoTypeSelect = null!;
    private OptionButton _ammoLevelSelect = null!;
    private Label _summary = null!;
    private Label _hint = null!;
    private Button _backButton = null!;
    private Button _exitButton = null!;
    private Button _deployButton = null!;
    private string _language = "en";
    private bool _inGameplay;
    private int _stationContext = -1;

    public int SelectedBotType
        => IsInstanceValid(_botTypeSelect) ? _botTypeSelect.Selected : 0;
    public int SelectedBotCount
        => BotCounts[Mathf.Clamp(
            IsInstanceValid(_botCountSelect) ? _botCountSelect.Selected : 1,
            0,
            BotCounts.Length - 1)];
    public int SelectedWeaponIndex
        => Mathf.Clamp(
            IsInstanceValid(_weaponSelect) ? _weaponSelect.Selected : 0,
            0,
            RangeWeapons.Length - 1);
    public WeaponPlatform SelectedWeaponPlatform => RangeWeapons[SelectedWeaponIndex];
    public int SelectedAmmoType
        => IsInstanceValid(_ammoTypeSelect) ? _ammoTypeSelect.Selected : 0;
    public int SelectedAmmoLevel
        => IsInstanceValid(_ammoLevelSelect) ? _ammoLevelSelect.Selected : 2;
    public bool IsInGameplay => _inGameplay;
    /// <summary>-1 opens the complete configuration; 0/1/2 focuses weapon/ammo/bot stations.</summary>
    public int StationContext => _stationContext;
    public bool UiReady
        => IsInstanceValid(_section)
        && IsInstanceValid(_title)
        && IsInstanceValid(_botTypeSelect)
        && IsInstanceValid(_botCountSelect)
        && IsInstanceValid(_weaponSelect)
        && IsInstanceValid(_ammoTypeSelect)
        && IsInstanceValid(_ammoLevelSelect)
        && IsInstanceValid(_summary)
        && IsInstanceValid(_backButton)
        && IsInstanceValid(_exitButton)
        && IsInstanceValid(_deployButton);
    public bool IntentSignalsConnected
        => HasConnections(SignalName.BackRequested)
        && HasConnections(SignalName.DeployRequested)
        && _botTypeSelect.HasConnections(OptionButton.SignalName.ItemSelected)
        && _botCountSelect.HasConnections(OptionButton.SignalName.ItemSelected)
        && _weaponSelect.HasConnections(OptionButton.SignalName.ItemSelected)
        && _ammoTypeSelect.HasConnections(OptionButton.SignalName.ItemSelected)
        && _ammoLevelSelect.HasConnections(OptionButton.SignalName.ItemSelected)
        && _backButton.HasConnections(BaseButton.SignalName.Pressed)
        && _exitButton.HasConnections(BaseButton.SignalName.Pressed)
        && _deployButton.HasConnections(BaseButton.SignalName.Pressed);
    public bool SelectionContractReady
        => UiReady
        && _botTypeSelect.ItemCount == 3
        && _botCountSelect.ItemCount == BotCounts.Length
        && _weaponSelect.ItemCount == RangeWeapons.Length
        && _ammoTypeSelect.ItemCount == 4
        && _ammoLevelSelect.ItemCount == 4;

    public override void _Ready()
    {
        BindNodes();
        PopulateOptions();
        ConnectIntentSignals();
        SetLanguage(_language);
        RefreshSummary();
    }

    public void SetLanguage(string language)
    {
        _language = GameLocalization.IsChinese(language) ? "zh" : "en";
        if (!UiReady)
        {
            return;
        }

        var chinese = GameLocalization.IsChinese(_language);
        _section.Text = Text(
            "training_setup_section",
            "LIVE FIRE CONTROL  //  TRAINING WING");
        _title.Text = _inGameplay
            ? Text("training_setup_title_live", "RANGE CONFIGURATION")
            : Text("training_setup_title", "TRAINING RANGE SETUP");
        _subtitle.Text = Text(
            "training_setup_subtitle",
            "CHOOSE THE TARGET, THE GUN, AND THE ROUND BEFORE YOU ENTER THE LIVE-FIRE LANE.");
        _botCaption.Text = Text("training_setup_bot_caption", "BOT TARGET");
        _botTypeCaption.Text = Text("training_setup_bot_type", "TARGET BEHAVIOR");
        _botCountCaption.Text = Text("training_setup_bot_count", "TARGET COUNT");
        _botHint.Text = Text(
            "training_setup_bot_hint",
            "EACH TARGET RETURNS AFTER A KNOCKDOWN.");
        _weaponCaption.Text = Text("training_setup_weapon_caption", "WEAPON BENCH");
        _weaponSelectCaption.Text = Text("training_setup_weapon", "WEAPON");
        _weaponHint.Text = Text(
            "training_setup_weapon_hint",
            "ALL 13 AUTHORED WEAPONS ARE AVAILABLE.\nUSE THE BENCH AGAIN IN THE LANE.");
        _ammoCaption.Text = Text("training_setup_ammo_caption", "AMMUNITION LAB");
        _ammoTypeCaption.Text = Text("training_setup_ammo_type", "ROUND TYPE");
        _ammoLevelCaption.Text = Text("training_setup_ammo_level", "ROUND GRADE");
        _ammoHint.Text = Text(
            "training_setup_ammo_hint",
            "AMMO AND UTILITY REFILL AUTOMATICALLY.");
        _backButton.Text = Text("training_setup_back", "BACK");
        _exitButton.Text = Text("training_setup_exit", "RETURN TO OPERATIONS");
        _exitButton.Visible = _inGameplay;
        _deployButton.Text = _inGameplay
            ? Text("training_setup_apply", "APPLY AND RESUME")
            : Text("training_setup_deploy", "ENTER LIVE-FIRE LANE");
        _hint.Text = _inGameplay
            ? Text(
                "training_setup_live_hint",
                "CHOOSE APPLY OR BACK TO CLOSE  //  CHANGES APPLY NOW")
            : Text(
                "training_setup_hint",
                "F3  OPEN THIS PANEL IN THE LANE  //  EVERY KNOCKDOWN STARTS A RESPAWN TIMER");

        SetBotTypeText(chinese);
        SetBotCountText(chinese);
        SetWeaponText();
        SetAmmoText(chinese);
        RefreshStationContext();
        RefreshSummary();
    }

    public void SetInGameplay(bool inGameplay)
    {
        _inGameplay = inGameplay;
        if (UiReady)
        {
            // The exit button is hidden before the first deploy. Keep keyboard and
            // controller focus on the visible actions in that state; once a player
            // is in the range, include the explicit return-to-operations action.
            _backButton.FocusNeighborRight = new NodePath(
                inGameplay ? "../ExitButton" : "../DeployButton");
            _deployButton.FocusNeighborLeft = new NodePath(
                inGameplay ? "../ExitButton" : "../BackButton");
            SetLanguage(_language);
        }
    }

    public bool LanguageMatches(string language)
    {
        if (!UiReady)
        {
            return false;
        }
        var normalized = GameLocalization.IsChinese(language) ? "zh" : "en";
        var chinese = normalized == "zh";
        var expectedStatic = GameLocalization.Get(
            "training_setup_bot_static",
            normalized,
            "STATIC TARGETS");
        var expectedAmmo = GameLocalization.Get(
            "training_setup_ammo_fmj",
            normalized,
            "FULL METAL JACKET");
        return _language == normalized
            && _section.Text.Length > 0
            && _title.Text == GameLocalization.Get(
                _inGameplay ? "training_setup_title_live" : "training_setup_title",
                normalized,
                _inGameplay ? "RANGE CONFIGURATION" : "TRAINING RANGE SETUP")
            && _botTypeSelect.GetItemText(0) == expectedStatic
            && _ammoTypeSelect.GetItemText(0) == expectedAmmo
            && _botCountSelect.GetItemText(1)
                == (chinese ? $"{BotCounts[1]}{Text("training_setup_count_suffix", " TARGETS")}" : "6 TARGETS")
            && _deployButton.Text == GameLocalization.Get(
                _inGameplay ? "training_setup_apply" : "training_setup_deploy",
                normalized,
                _inGameplay ? "APPLY AND RESUME" : "ENTER LIVE-FIRE LANE");
    }

    public void SetStationContext(int stationKind)
    {
        _stationContext = stationKind is >= 0 and <= 2 ? stationKind : -1;
        if (UiReady)
        {
            RefreshStationContext();
            GrabDefaultFocus();
        }
    }

    public void SetSelections(
        int botType,
        int botCount,
        int weaponIndex,
        int ammoType,
        int ammoLevel)
    {
        if (!UiReady)
        {
            return;
        }
        _botTypeSelect.Select(Mathf.Clamp(botType, 0, 2));
        var countIndex = Array.IndexOf(BotCounts, botCount);
        _botCountSelect.Select(countIndex >= 0 ? countIndex : 1);
        _weaponSelect.Select(Mathf.Clamp(weaponIndex, 0, RangeWeapons.Length - 1));
        _ammoTypeSelect.Select(Mathf.Clamp(ammoType, 0, 3));
        _ammoLevelSelect.Select(Mathf.Clamp(ammoLevel, 0, 3));
        RefreshSummary();
    }

    public void SelectBotTypeForDiagnostics(int value)
        => _botTypeSelect.Select(Mathf.Clamp(value, 0, 2));

    public void SelectBotCountForDiagnostics(int count)
    {
        var index = Array.IndexOf(BotCounts, count);
        _botCountSelect.Select(index >= 0 ? index : 1);
    }

    public void SelectWeaponForDiagnostics(int index)
        => _weaponSelect.Select(Mathf.Clamp(index, 0, RangeWeapons.Length - 1));

    public void SelectAmmoForDiagnostics(int type, int level)
    {
        _ammoTypeSelect.Select(Mathf.Clamp(type, 0, 3));
        _ammoLevelSelect.Select(Mathf.Clamp(level, 0, 3));
    }

    public void PressDeployForDiagnostics()
        => _deployButton.EmitSignal(BaseButton.SignalName.Pressed);

    public void PressBackForDiagnostics()
        => _backButton.EmitSignal(BaseButton.SignalName.Pressed);

    public void GrabDefaultFocus()
    {
        var target = _stationContext switch
        {
            0 => _weaponSelect,
            1 => _ammoTypeSelect,
            2 => _botTypeSelect,
            _ => _botTypeSelect
        };
        if (IsInstanceValid(target))
        {
            target.GrabFocus();
        }
    }

    private void BindNodes()
    {
        var panel = GetNode<Control>("Panel");
        _section = panel.GetNode<Label>("Section");
        _title = panel.GetNode<Label>("Title");
        _subtitle = panel.GetNode<Label>("Subtitle");
        var botPanel = panel.GetNode<Control>("BotPanel");
        _botCaption = botPanel.GetNode<Label>("Caption");
        _botTypeCaption = botPanel.GetNode<Label>("TypeCaption");
        _botCountCaption = botPanel.GetNode<Label>("CountCaption");
        _botHint = botPanel.GetNode<Label>("BotHint");
        _botTypeSelect = botPanel.GetNode<OptionButton>("TypeSelect");
        _botCountSelect = botPanel.GetNode<OptionButton>("CountSelect");

        var weaponPanel = panel.GetNode<Control>("WeaponPanel");
        _weaponCaption = weaponPanel.GetNode<Label>("Caption");
        _weaponSelectCaption = weaponPanel.GetNode<Label>("WeaponCaption");
        _weaponHint = weaponPanel.GetNode<Label>("WeaponHint");
        _weaponSelect = weaponPanel.GetNode<OptionButton>("WeaponSelect");

        var ammoPanel = panel.GetNode<Control>("AmmoPanel");
        _ammoCaption = ammoPanel.GetNode<Label>("Caption");
        _ammoTypeCaption = ammoPanel.GetNode<Label>("AmmoTypeCaption");
        _ammoLevelCaption = ammoPanel.GetNode<Label>("AmmoLevelCaption");
        _ammoHint = ammoPanel.GetNode<Label>("AmmoHint");
        _ammoTypeSelect = ammoPanel.GetNode<OptionButton>("AmmoTypeSelect");
        _ammoLevelSelect = ammoPanel.GetNode<OptionButton>("AmmoLevelSelect");

        _summary = panel.GetNode<Label>("Summary");
        _hint = panel.GetNode<Label>("Hint");
        _backButton = panel.GetNode<Button>("BackButton");
        _exitButton = panel.GetNode<Button>("ExitButton");
        _deployButton = panel.GetNode<Button>("DeployButton");
    }

    private void PopulateOptions()
    {
        _botTypeSelect.AddItem("STATIC TARGETS");
        _botTypeSelect.AddItem("PATROL BOTS");
        _botTypeSelect.AddItem("REACTIVE BOTS");
        _botTypeSelect.Select(0);

        foreach (var count in BotCounts)
        {
            _botCountSelect.AddItem(count.ToString());
        }
        _botCountSelect.Select(1);

        foreach (var platform in RangeWeapons)
        {
            _weaponSelect.AddItem(WeaponCatalog.Weapon(platform).Name);
        }
        _weaponSelect.Select(0);

        _ammoTypeSelect.AddItem("FULL METAL JACKET");
        _ammoTypeSelect.AddItem("ARMOR PIERCING");
        _ammoTypeSelect.AddItem("HOLLOW POINT");
        _ammoTypeSelect.AddItem("TRACER");
        _ammoTypeSelect.Select(0);

        _ammoLevelSelect.AddItem("T1  //  TRAINING");
        _ammoLevelSelect.AddItem("T2  //  STANDARD");
        _ammoLevelSelect.AddItem("T3  //  MATCH");
        _ammoLevelSelect.AddItem("T4  //  HOT LOAD");
        _ammoLevelSelect.Select(2);
    }

    private void ConnectIntentSignals()
    {
        _botTypeSelect.ItemSelected += _ => RefreshSummary();
        _botCountSelect.ItemSelected += _ => RefreshSummary();
        _weaponSelect.ItemSelected += _ => RefreshSummary();
        _ammoTypeSelect.ItemSelected += _ => RefreshSummary();
        _ammoLevelSelect.ItemSelected += _ => RefreshSummary();
        _backButton.Pressed += () => EmitSignal(SignalName.BackRequested);
        _exitButton.Pressed += () => EmitSignal(SignalName.ExitRequested);
        _deployButton.Pressed += () => EmitSignal(
            SignalName.DeployRequested,
            SelectedBotType,
            SelectedBotCount,
            SelectedWeaponIndex,
            SelectedAmmoType,
            SelectedAmmoLevel);
    }

    private void SetBotTypeText(bool chinese)
    {
        var values = chinese
            ? new[]
            {
                Text("training_setup_bot_static", "STATIC TARGETS"),
                Text("training_setup_bot_patrol", "PATROL BOTS"),
                Text("training_setup_bot_reactive", "REACTIVE BOTS")
            }
            : new[] { "STATIC TARGETS", "PATROL BOTS", "REACTIVE BOTS" };
        for (var index = 0; index < values.Length; index++)
        {
            _botTypeSelect.SetItemText(index, values[index]);
        }
    }

    private void SetBotCountText(bool chinese)
    {
        for (var index = 0; index < BotCounts.Length; index++)
        {
            _botCountSelect.SetItemText(
                index,
                chinese
                    ? $"{BotCounts[index]}{Text("training_setup_count_suffix", " TARGETS")}"
                    : $"{BotCounts[index]} TARGETS");
        }
    }

    private void SetWeaponText()
    {
        for (var index = 0; index < RangeWeapons.Length; index++)
        {
            _weaponSelect.SetItemText(
                index,
                DisplayWeaponName(RangeWeapons[index]));
        }
    }

    private void SetAmmoText(bool chinese)
    {
        var types = chinese
            ? new[]
            {
                Text("training_setup_ammo_fmj", "FULL METAL JACKET"),
                Text("training_setup_ammo_ap", "ARMOR PIERCING"),
                Text("training_setup_ammo_hp", "HOLLOW POINT"),
                Text("training_setup_ammo_tracer", "TRACER")
            }
            : new[] { "FULL METAL JACKET", "ARMOR PIERCING", "HOLLOW POINT", "TRACER" };
        var levels = chinese
            ? new[]
            {
                Text("training_setup_level_t1", "T1  //  TRAINING"),
                Text("training_setup_level_t2", "T2  //  STANDARD"),
                Text("training_setup_level_t3", "T3  //  MATCH"),
                Text("training_setup_level_t4", "T4  //  HOT LOAD")
            }
            : new[] { "T1  //  TRAINING", "T2  //  STANDARD", "T3  //  MATCH", "T4  //  HOT LOAD" };
        for (var index = 0; index < types.Length; index++)
        {
            _ammoTypeSelect.SetItemText(index, types[index]);
            _ammoLevelSelect.SetItemText(index, levels[index]);
        }
    }

    private void RefreshSummary()
    {
        if (!UiReady)
        {
            return;
        }
        var chinese = GameLocalization.IsChinese(_language);
        var botType = _botTypeSelect.Selected switch
        {
            1 => Text("training_setup_bot_patrol", "PATROL BOTS"),
            2 => Text("training_setup_bot_reactive", "REACTIVE BOTS"),
            _ => Text("training_setup_bot_static", "STATIC TARGETS")
        };
        var ammoType = _ammoTypeSelect.Selected switch
        {
            1 => Text("training_setup_ammo_ap_short", "AP"),
            2 => Text("training_setup_ammo_hp_short", "HP"),
            3 => Text("training_setup_ammo_tracer_short", "TRACER"),
            _ => Text("training_setup_ammo_fmj_short", "FMJ")
        };
        var grade = $"T{_ammoLevelSelect.Selected + 1}";
        var weapon = DisplayWeaponName(SelectedWeaponPlatform);
        _summary.Text = chinese
            ? $"{Text("training_setup_summary_targets", "TARGETS")}  {SelectedBotCount}  //  {botType}  //  {weapon}  //  {ammoType}  //  {grade}"
            : $"TARGETS  {SelectedBotCount}  //  {botType}  //  {weapon}  //  {ammoType}  //  {grade}";
    }

    private void RefreshStationContext()
    {
        if (!UiReady)
        {
            return;
        }
        var chinese = GameLocalization.IsChinese(_language);
        _section.Text = _stationContext switch
        {
            0 => Text("training_setup_station_weapon", "WEAPON BENCH  //  SELECT WEAPON"),
            1 => Text("training_setup_station_ammo", "AMMUNITION LAB  //  SELECT ROUND"),
            2 => Text("training_setup_station_bot", "BOT CONTROL  //  SET TARGETS"),
            _ => Text("training_setup_section", "LIVE FIRE CONTROL  //  TRAINING WING")
        };
    }

    private string DisplayWeaponName(WeaponPlatform platform)
    {
        var definition = WeaponCatalog.Weapon(platform);
        return GameLocalization.IsChinese(_language)
            ? GameLocalization.Get(definition.LocalizationKey, _language, definition.ChineseName)
            : definition.Name;
    }

    private string Text(string key, string english)
        => GameLocalization.Get(key, _language, english);
}
