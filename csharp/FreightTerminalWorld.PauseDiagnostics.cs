using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private async void ValidatePauseUi()
    {
        await WaitFrames(5);

        var tree = GetTree();
        var originalScene = tree.CurrentScene;
        var originalPaused = tree.Paused;
        var originalPauseVisible = _hud.IsPauseMenuVisible;
        var originalMouseMode = Input.MouseMode;
        var originalMissionEnded = _missionEnded;
        var cleanupErrors = new List<string>();
        CombatHUD? probeHud = null;
        var pauseCaptureConnected = false;

        var uiReady = false;
        var packedUiReady = false;
        var intentsConnected = false;
        var deterministicStart = false;
        var settingsReady = false;
        var noFeedbackSignals = false;
        var chineseReady = false;
        var englishReady = false;
        var opened = false;
        var settingsSignalsReady = false;
        var commandSignalsReady = false;
        var resumed = false;
        var diagnosticError = string.Empty;

        var sensitivityCount = 0;
        var qualityCount = 0;
        var fullscreenCount = 0;
        var languageCount = 0;
        var pauseCount = 0;
        var restartCount = 0;
        var quitCount = 0;
        var observedSensitivity = 0.0f;
        var observedQuality = -1;
        var observedFullscreen = false;
        var observedLanguage = string.Empty;

        void CaptureSensitivity(float value)
        {
            sensitivityCount++;
            observedSensitivity = value;
        }

        void CaptureQuality(int value)
        {
            qualityCount++;
            observedQuality = value;
        }

        void CaptureFullscreen(bool value)
        {
            fullscreenCount++;
            observedFullscreen = value;
        }

        void CaptureLanguage(string value)
        {
            languageCount++;
            observedLanguage = value;
        }

        void CapturePause() => pauseCount++;
        void CaptureRestart() => restartCount++;
        void CaptureQuit() => quitCount++;

        void Cleanup(string label, Action action)
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                cleanupErrors.Add($"{label}:{exception.GetType().Name}");
                GD.PushError($"Pause UI validation cleanup failed at {label}: {exception.Message}");
            }
        }

        try
        {
            tree.Paused = false;
            _hud.SetPauseVisible(false);
            Input.MouseMode = Input.MouseModeEnum.Captured;
            _missionEnded = false;
            deterministicStart = !tree.Paused
                && !_hud.IsPauseMenuVisible
                && Input.MouseMode == Input.MouseModeEnum.Captured;

            probeHud = new CombatHUD { Name = "PauseUiDiagnosticProbe" };
            AddChild(probeHud);
            await WaitFrames(2);

            uiReady = probeHud.PauseMenuUiReady;
            packedUiReady = probeHud.PauseMenuUsesPackedScene;
            intentsConnected = probeHud.PauseMenuIntentSignalsConnected;

            probeHud.SensitivityChanged += CaptureSensitivity;
            probeHud.QualityChanged += CaptureQuality;
            probeHud.FullscreenChanged += CaptureFullscreen;
            probeHud.LanguageChanged += CaptureLanguage;
            probeHud.RestartRequested += CaptureRestart;
            probeHud.QuitRequested += CaptureQuit;

            probeHud.SetSettings(0.85f, 0, false, "en");
            settingsReady = probeHud.PauseSettingsMatch(0.85f, 0, false, "en");
            noFeedbackSignals = sensitivityCount == 0
                && qualityCount == 0
                && fullscreenCount == 0
                && languageCount == 0;

            probeHud.SetLanguage("zh");
            chineseReady = probeHud.PauseLanguageMatches("zh");
            probeHud.SetLanguage("en");
            englishReady = probeHud.PauseLanguageMatches("en");

            probeHud.DrivePauseSettingsForDiagnostics(1.35f, 1, true, "zh");
            settingsSignalsReady = sensitivityCount == 1
                && Mathf.IsEqualApprox(observedSensitivity, 1.35f)
                && qualityCount == 1
                && observedQuality == 1
                && fullscreenCount == 1
                && observedFullscreen
                && languageCount == 1
                && observedLanguage == "zh"
                && probeHud.PauseSettingsMatch(1.35f, 1, true, "zh");

            probeHud.PressPauseRestartForDiagnostics();
            probeHud.PressPauseQuitForDiagnostics();
            commandSignalsReady = restartCount == 1
                && quitCount == 1
                && IsInsideTree()
                && tree.CurrentScene == originalScene;

            _hud.PauseRequested += CapturePause;
            pauseCaptureConnected = true;
            TogglePause();
            await WaitFrames(2);
            opened = tree.Paused
                && _hud.IsPauseMenuVisible
                && Input.MouseMode == Input.MouseModeEnum.Visible;

            _hud.PressPauseResumeForDiagnostics();
            await WaitFrames(2);
            resumed = pauseCount == 1
                && !tree.Paused
                && !_hud.IsPauseMenuVisible
                && Input.MouseMode == Input.MouseModeEnum.Captured;
        }
        catch (Exception exception)
        {
            diagnosticError = $"{exception.GetType().Name}:{exception.Message}";
            GD.PushError($"Pause UI validation failed with {diagnosticError}");
        }
        finally
        {
            if (pauseCaptureConnected)
            {
                Cleanup("pause_signal", () => _hud.PauseRequested -= CapturePause);
            }

            if (probeHud is not null && IsInstanceValid(probeHud))
            {
                Cleanup("probe_free", probeHud.QueueFree);
            }

            Cleanup("mission_state", () => _missionEnded = originalMissionEnded);
            Cleanup("tree_pause", () => tree.Paused = originalPaused);
            Cleanup("pause_visibility", () => _hud.SetPauseVisible(originalPauseVisible));
            Cleanup("mouse_mode", () => Input.MouseMode = originalMouseMode);
        }

        await WaitFrames(1);
        var cleanupReady = cleanupErrors.Count == 0
            && tree.Paused == originalPaused
            && _hud.IsPauseMenuVisible == originalPauseVisible
            && Input.MouseMode == originalMouseMode;
        var valid = uiReady
            && packedUiReady
            && intentsConnected
            && deterministicStart
            && settingsReady
            && noFeedbackSignals
            && chineseReady
            && englishReady
            && settingsSignalsReady
            && commandSignalsReady
            && opened
            && resumed
            && cleanupReady
            && diagnosticError.Length == 0;

        GD.Print($"PAUSE_UI_CHECK valid={valid} ui={uiReady} packed_ui={packedUiReady} intents={intentsConnected} deterministic_start={deterministicStart} settings={settingsReady} no_feedback={noFeedbackSignals} zh={chineseReady} en={englishReady} settings_signals={settingsSignalsReady} commands={commandSignalsReady} opened={opened} resumed={resumed} cleanup={cleanupReady} error={diagnosticError} cleanup_errors={string.Join(',', cleanupErrors)}");
        GD.Print($"PAUSE_UI_PASS valid={valid}");
        tree.Paused = false;
        tree.Quit(valid ? 0 : 2);
    }
}
