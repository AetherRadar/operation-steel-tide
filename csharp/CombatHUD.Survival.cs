using Godot;

namespace OperationSteelTide;

public partial class CombatHUD
{
    private Label? _survivalStatusLabel;
    private bool _survivalPresentationConfigured;

    internal bool SurvivalPresentationVisible
        => IsInstanceValid(_survivalStatusLabel) && _survivalStatusLabel.Visible;
    internal bool SurvivalPresentationConfigured => _survivalPresentationConfigured;

    /// <summary>Replaces the extraction mission header with the local wave ruleset.</summary>
    public void SetSurvivalModePresentation()
    {
        _survivalPresentationConfigured = true;
        if (!IsInstanceValid(_phaseLabel) || !IsInstanceValid(_objectiveLabel))
        {
            return;
        }
        var mode = Text("survival_mode", "SURVIVAL");
        var local = Text("local", "LOCAL");
        _phaseLabel.Text = $"{mode}   {local}";
        _phaseLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.55f, 0.3f));
        _objectiveLabel.Text = Text("survival_objective", "SURVIVE THE INCOMING WAVES");
    }

    /// <summary>Updates the compact survival pressure readout shown only on the survival map.</summary>
    public void SetSurvivalStatus(int wave, float nextWaveSeconds, int infected, int eliminations)
    {
        if (!IsInstanceValid(_gameplayHudRoot)) return;
        _survivalStatusLabel ??= CreateSurvivalStatusLabel();
        var language = CurrentLanguage;
        var waveText = language == "zh" ? $"{GameLocalization.Get("survival_wave", language, "WAVE {0}").Replace("{0}", wave.ToString("00"))}" : $"WAVE {wave:00}";
        var seconds = Mathf.CeilToInt(nextWaveSeconds).ToString("00");
        var pressureText = language == "zh" ? GameLocalization.Get("survival_next", language, "NEXT {0}s").Replace("{0}", seconds) : $"NEXT {seconds}s";
        var aliveText = language == "zh" ? GameLocalization.Get("survival_active", language, "ACTIVE {0}").Replace("{0}", infected.ToString("00")) : $"ACTIVE {infected:00}";
        var killsText = language == "zh" ? GameLocalization.Get("survival_cleared", language, "CLEARED {0}").Replace("{0}", eliminations.ToString("00")) : $"CLEARED {eliminations:00}";
        _survivalStatusLabel.Text = $"{waveText}   {pressureText}   {aliveText}   {killsText}";
        _survivalStatusLabel.Visible = true;
    }

    public void ShowSurvivalResult(bool victory, int wave, int eliminations)
    {
        HideOperationsMenus();
        _gameplayHudRoot.Visible = true;
        _downedBanner.Visible = false;
        _stateOverlay.Visible = true;
        if (victory)
        {
            _stateTitle.Text = Text("survival_complete", "SURVIVAL COMPLETE");
            _stateTitle.AddThemeColorOverride("font_color", new Color(0.33f, 0.92f, 0.74f));
            _stateSubtitle.Text = GameLocalization.Format(
                "survival_result",
                _language,
                "WAVES CLEARED {0}  //  INFECTED ELIMINATED {1}",
                wave,
                eliminations);
        }
        else
        {
            _stateTitle.Text = Text("operator_down", "OPERATOR DOWN");
            _stateTitle.AddThemeColorOverride("font_color", new Color(1.0f, 0.27f, 0.18f));
            _stateSubtitle.Text = Text("press_enter", "PRESS ENTER TO REDEPLOY");
        }
    }

    private Label CreateSurvivalStatusLabel()
    {
        var label = new Label
        {
            Name = "SurvivalStatusLabel",
            Position = new Vector2(30, 58),
            Size = new Vector2(640, 22),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Modulate = new Color(1.0f, 0.55f, 0.34f),
            ThemeTypeVariation = "CaptionLabel"
        };
        _gameplayHudRoot.AddChild(label);
        return label;
    }
}
