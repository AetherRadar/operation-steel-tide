using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private async void ValidateDemolitionRoundResult()
    {
        await WaitFrames(3);
        const string scenePath = "res://ui/DemolitionRoundResultView.tscn";
        var originalLanguage = _hud.CurrentLanguage;
        var packedScene = GD.Load<PackedScene>(scenePath);
        var probe = packedScene?.Instantiate<DemolitionRoundResultView>();
        var chineseVictory = false;
        var englishDefeat = false;
        var hiddenLifecycle = false;
        if (probe is not null)
        {
            probe.Visible = false;
            _hud.AddChild(probe);
            probe.SetLanguage("zh");
            probe.ShowResult(true, "OBJECTIVE SECURED", 3, 2, 4.2f);
            chineseVictory = probe.SceneFilePath == scenePath
                && probe.UiReady
                && probe.Visible
                && probe.Victory
                && probe.LanguageMatches("zh")
                && probe.TitleText == GameLocalization.Get(
                    "demolition_round_victory",
                    "zh",
                    "ROUND WON")
                && probe.ScoreText.Contains("3", System.StringComparison.Ordinal)
                && probe.ScoreText.Contains("2", System.StringComparison.Ordinal)
                && Mathf.IsEqualApprox(probe.DisplayedSeconds, 4.2f);

            probe.SetLanguage("en");
            probe.ShowResult(false, "ROUND COMPLETE", 3, 3, 2.5f);
            englishDefeat = !probe.Victory
                && probe.LanguageMatches("en")
                && probe.TitleText == "ROUND LOST"
                && probe.ScoreText == "YOU  3 : 3  ENEMY";
            probe.UpdateCountdown(1.25f);
            hiddenLifecycle = Mathf.IsEqualApprox(probe.DisplayedSeconds, 1.25f);
            probe.HideResult();
            hiddenLifecycle &= !probe.Visible;
        }
        probe?.QueueFree();

        _hud.SetLanguage("zh");
        _hud.ShowDemolitionRoundResult(
            true,
            GameLocalization.Get(
                "demolition_opponents_eliminated",
                "zh",
                "OPPONENT SQUAD ELIMINATED"),
            8,
            7,
            5.0f);
        var hudReady = _hud.IsDemolitionRoundResultVisible
            && _hud.DemolitionRoundResultUiReady
            && _hud.DemolitionRoundResultUsesPackedScene
            && _hud.DemolitionRoundResultLanguageReady
            && _hud.DemolitionRoundResultVictory
            && _hud.DemolitionRoundResultScore.Contains("8", System.StringComparison.Ordinal)
            && _hud.DemolitionRoundResultScore.Contains("7", System.StringComparison.Ordinal)
            && Mathf.IsEqualApprox(_hud.DemolitionRoundResultSeconds, 5.0f);
        _hud.UpdateDemolitionRoundResult(3.5f);
        var countdownUpdated = Mathf.IsEqualApprox(_hud.DemolitionRoundResultSeconds, 3.5f);
        _hud.HideDemolitionRoundResult();
        var hudHidden = !_hud.IsDemolitionRoundResultVisible;
        _hud.SetLanguage(originalLanguage);
        _hud.ShowOperationsOffice();
        await WaitFrames(3);

        var valid = chineseVictory && englishDefeat && hiddenLifecycle
            && hudReady && countdownUpdated && hudHidden;
        GD.Print($"DEMOLITION_ROUND_RESULT_CHECK valid={valid} scene={probe is not null} chinese={chineseVictory} english={englishDefeat} lifecycle={hiddenLifecycle} hud={hudReady} countdown={countdownUpdated} hidden={hudHidden}");
        GD.Print($"DEMOLITION_ROUND_RESULT_PASS valid={valid}");
        GetTree().Paused = false;
        GetTree().Quit(valid ? 0 : 2);
    }

    private async void CaptureDemolitionRoundResult()
    {
        await WaitFrames(3);
        _hud.SetLanguage("zh");
        _hud.ShowDemolitionRoundResult(
            true,
            GameLocalization.Get(
                "demolition_opponents_eliminated",
                "zh",
                "OPPONENT SQUAD ELIMINATED"),
            8,
            7,
            4.6f);
        await WaitFrames(18);
        SaveViewportImage("res://demolition_round_result_validation.png");
        GD.Print("DEMOLITION_ROUND_RESULT_CAPTURE path=demolition_round_result_validation.png");
        GetTree().Paused = false;
        GetTree().Quit();
    }
}
