using Godot;

namespace OperationSteelTide;

/// <summary>
/// Presents one demolition round outcome. Inputs are the localized reason, score,
/// outcome, and intermission countdown; the view emits no gameplay intent.
/// </summary>
[GlobalClass]
public partial class DemolitionRoundResultView : Control
{
    private static readonly Color VictoryColor = new(1.0f, 0.62f, 0.22f);
    private static readonly Color DefeatColor = new(1.0f, 0.27f, 0.18f);

    private ColorRect _topRule = null!;
    private ColorRect _bottomRule = null!;
    private Label _title = null!;
    private Label _reason = null!;
    private Label _score = null!;
    private Label _countdown = null!;
    private string _language = "en";
    private string _roundReason = string.Empty;
    private bool _victory;
    private int _playerScore;
    private int _opponentScore;
    private float _secondsRemaining;

    public bool UiReady
        => IsInstanceValid(_topRule)
        && IsInstanceValid(_bottomRule)
        && IsInstanceValid(_title)
        && IsInstanceValid(_reason)
        && IsInstanceValid(_score)
        && IsInstanceValid(_countdown);
    public bool Victory => _victory;
    public string TitleText => IsInstanceValid(_title) ? _title.Text : string.Empty;
    public string ScoreText => IsInstanceValid(_score) ? _score.Text : string.Empty;
    public float DisplayedSeconds => _secondsRemaining;

    public override void _Ready()
    {
        BindNodes();
        Refresh();
    }

    public void ShowResult(
        bool victory,
        string reason,
        int playerScore,
        int opponentScore,
        float secondsRemaining)
    {
        _victory = victory;
        _roundReason = reason;
        _playerScore = Mathf.Max(0, playerScore);
        _opponentScore = Mathf.Max(0, opponentScore);
        _secondsRemaining = Mathf.Max(0.0f, secondsRemaining);
        Visible = true;
        Refresh();
    }

    public void UpdateCountdown(float secondsRemaining)
    {
        _secondsRemaining = Mathf.Max(0.0f, secondsRemaining);
        if (IsInstanceValid(_countdown))
        {
            _countdown.Text = CountdownText();
        }
    }

    public void HideResult() => Visible = false;

    public void SetLanguage(string language)
    {
        _language = GameLocalization.IsChinese(language) ? "zh" : "en";
        Refresh();
    }

    public bool LanguageMatches(string language)
    {
        var normalized = GameLocalization.IsChinese(language) ? "zh" : "en";
        var expectedTitle = GameLocalization.Get(
            _victory ? "demolition_round_victory" : "demolition_round_defeat",
            normalized,
            _victory ? "ROUND WON" : "ROUND LOST");
        return _language == normalized && IsInstanceValid(_title) && _title.Text == expectedTitle;
    }

    private void BindNodes()
    {
        var band = GetNode<Control>("Band");
        _topRule = band.GetNode<ColorRect>("TopRule");
        _bottomRule = band.GetNode<ColorRect>("BottomRule");
        _title = band.GetNode<Label>("Title");
        _reason = band.GetNode<Label>("Reason");
        _score = band.GetNode<Label>("Score");
        _countdown = band.GetNode<Label>("Countdown");
    }

    private void Refresh()
    {
        if (!UiReady)
        {
            return;
        }

        var accent = _victory ? VictoryColor : DefeatColor;
        _topRule.Color = accent;
        _bottomRule.Color = accent;
        _title.AddThemeColorOverride("font_color", accent);
        _title.Text = Text(
            _victory ? "demolition_round_victory" : "demolition_round_defeat",
            _victory ? "ROUND WON" : "ROUND LOST");
        _reason.Text = _roundReason;
        _score.Text = GameLocalization.Format(
            "demolition_round_score",
            _language,
            "YOU  {0} : {1}  ENEMY",
            _playerScore,
            _opponentScore);
        _countdown.Text = CountdownText();
    }

    private string CountdownText()
        => GameLocalization.Format(
            "demolition_next_round_countdown",
            _language,
            "NEXT ROUND  {0:0.0}s",
            _secondsRemaining);

    private string Text(string key, string english)
        => GameLocalization.Get(key, _language, english);
}
