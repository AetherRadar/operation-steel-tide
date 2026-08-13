using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private async void ValidatePauseUi()
    {
        await WaitFrames(5);

        var uiReady = _hud.PauseMenuUiReady;
        var packedUiReady = _hud.PauseMenuUsesPackedScene;
        var intentsConnected = _hud.PauseMenuIntentSignalsConnected;
        var initiallyHidden = !_hud.IsPauseMenuVisible;

        var settingsSignalCount = 0;
        _hud.SensitivityChanged += _ => settingsSignalCount++;
        _hud.QualityChanged += _ => settingsSignalCount++;
        _hud.FullscreenChanged += _ => settingsSignalCount++;
        _hud.LanguageChanged += _ => settingsSignalCount++;

        _hud.SetSettings(1.35f, 1, _fullscreenSetting, "zh");
        var settingsReady = _hud.PauseSettingsMatch(1.35f, 1, _fullscreenSetting, "zh");
        var noFeedbackSignals = settingsSignalCount == 0;
        var chineseReady = _hud.PauseLanguageMatches("zh");
        _hud.SetLanguage("en");
        var englishReady = _hud.PauseLanguageMatches("en");

        TogglePause();
        await WaitFrames(2);
        var opened = GetTree().Paused
            && _hud.IsPauseMenuVisible
            && Input.MouseMode == Input.MouseModeEnum.Visible;

        _hud.PressPauseResumeForDiagnostics();
        await WaitFrames(2);
        var resumed = !GetTree().Paused
            && !_hud.IsPauseMenuVisible
            && Input.MouseMode == Input.MouseModeEnum.Captured;

        _hud.SetSettings(_sensitivitySetting, _qualitySetting, _fullscreenSetting, _languageSetting);
        var valid = uiReady
            && packedUiReady
            && intentsConnected
            && initiallyHidden
            && settingsReady
            && noFeedbackSignals
            && chineseReady
            && englishReady
            && opened
            && resumed;

        GD.Print($"PAUSE_UI_CHECK valid={valid} ui={uiReady} packed_ui={packedUiReady} intents={intentsConnected} hidden={initiallyHidden} settings={settingsReady} no_feedback={noFeedbackSignals} zh={chineseReady} en={englishReady} opened={opened} resumed={resumed}");
        GD.Print($"PAUSE_UI_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }
}
