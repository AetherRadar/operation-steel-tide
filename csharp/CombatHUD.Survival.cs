using Godot;

namespace OperationSteelTide;

public partial class CombatHUD
{
    private Label? _survivalStatusLabel;

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
