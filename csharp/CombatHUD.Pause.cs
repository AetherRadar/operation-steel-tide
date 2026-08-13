using Godot;

namespace OperationSteelTide;

public partial class CombatHUD
{
    private const string PauseMenuViewScenePath = "res://ui/PauseMenuView.tscn";

    private PauseMenuView _pauseMenuView = null!;

    public bool IsPauseMenuVisible => IsInstanceValid(_pauseMenuView) && _pauseMenuView.Visible;
    public bool PauseMenuUiReady => IsInstanceValid(_pauseMenuView) && _pauseMenuView.UiReady;
    public bool PauseMenuIntentSignalsConnected
        => IsInstanceValid(_pauseMenuView) && _pauseMenuView.IntentSignalsConnected;
    public bool PauseMenuUsesPackedScene
        => IsInstanceValid(_pauseMenuView)
        && _pauseMenuView.SceneFilePath == PauseMenuViewScenePath
        && _pauseMenuView.GetNodeOrNull<Control>("Content/SensitivitySlider") is not null
        && _pauseMenuView.GetNodeOrNull<Control>("Content/ResumeButton") is not null;

    private void BuildPauseMenu(Control root)
    {
        var scene = GD.Load<PackedScene>(PauseMenuViewScenePath)
            ?? throw new System.InvalidOperationException($"Unable to load {PauseMenuViewScenePath}");
        _pauseMenuView = scene.Instantiate<PauseMenuView>();
        root.AddChild(_pauseMenuView);

        _pauseMenuView.ResumeRequested += () => EmitSignal(SignalName.PauseRequested);
        _pauseMenuView.RestartRequested += () => EmitSignal(SignalName.RestartRequested);
        _pauseMenuView.QuitRequested += () => EmitSignal(SignalName.QuitRequested);
        _pauseMenuView.SensitivityChanged += value => EmitSignal(SignalName.SensitivityChanged, value);
        _pauseMenuView.QualityChanged += index => EmitSignal(SignalName.QualityChanged, index);
        _pauseMenuView.FullscreenChanged += active => EmitSignal(SignalName.FullscreenChanged, active);
        _pauseMenuView.LanguageChanged += language => EmitSignal(SignalName.LanguageChanged, language);
    }

    public void SetPauseVisible(bool active) => _pauseMenuView.Visible = active;

    public void SetSettings(float sensitivity, int quality, bool fullscreen, string language)
    {
        _pauseMenuView.SetSettings(sensitivity, quality, fullscreen, language);
        SetLanguage(language);
    }

    public bool PauseSettingsMatch(float sensitivity, int quality, bool fullscreen, string language)
        => IsInstanceValid(_pauseMenuView)
        && _pauseMenuView.SettingsMatch(sensitivity, quality, fullscreen, language);

    public bool PauseLanguageMatches(string language)
        => IsInstanceValid(_pauseMenuView) && _pauseMenuView.LanguageMatches(language);

    public void PressPauseResumeForDiagnostics() => _pauseMenuView.PressResumeForDiagnostics();
}
