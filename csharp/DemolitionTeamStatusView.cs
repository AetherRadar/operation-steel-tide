using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public enum DemolitionTeamStatusPhase
{
    Buy,
    Live,
    DeviceActive,
    Intermission
}

public readonly record struct DemolitionTeamStatusMember(
    string Id,
    string DisplayName,
    OperatorRole Role,
    bool Alive,
    bool IsLocalPlayer,
    bool HasDevice);

public readonly record struct DemolitionTeamStatusSnapshot(
    IReadOnlyList<DemolitionTeamStatusMember> Friendly,
    IReadOnlyList<DemolitionTeamStatusMember> Enemy,
    DemolitionTeam PlayerSide,
    DemolitionTeamStatusPhase Phase,
    int FriendlyScore,
    int EnemyScore,
    int Round,
    float SecondsRemaining,
    bool IsOvertime);

/// <summary>
/// Read-only Valorant-style top roster. The world supplies one snapshot and the
/// language; the scene owns card presentation and emits no gameplay intent.
/// </summary>
[GlobalClass]
public partial class DemolitionTeamStatusView : Control
{
    public const string ScenePath = "res://ui/DemolitionTeamStatusView.tscn";

    private HBoxContainer _friendly = null!;
    private HBoxContainer _enemy = null!;
    private Label _round = null!;
    private Label _score = null!;
    private Label _timer = null!;
    private Label _phase = null!;
    private PackedScene _cardScene = null!;
    private readonly List<DemolitionTeamMemberCard> _friendlyCards = new();
    private readonly List<DemolitionTeamMemberCard> _enemyCards = new();
    private DemolitionTeamStatusSnapshot? _snapshot;
    private string _language = "en";

    public bool UiReady
        => IsInstanceValid(_friendly)
        && IsInstanceValid(_enemy)
        && IsInstanceValid(_round)
        && IsInstanceValid(_score)
        && IsInstanceValid(_timer)
        && IsInstanceValid(_phase)
        && IsInstanceValid(_cardScene);
    public int FriendlyCount => _friendlyCards.Count;
    public int EnemyCount => _enemyCards.Count;
    public int LocalPlayerMarkerCount => CountCards(static card => card.IsLocalPlayer);
    public int DeviceMarkerCount => CountCards(static card => card.HasDevice);
    public int OutCount => CountCards(static card => !card.IsAlive);
    public string ScoreText => IsInstanceValid(_score) ? _score.Text : string.Empty;
    public string TimerText => IsInstanceValid(_timer) ? _timer.Text : string.Empty;
    public string PhaseText => IsInstanceValid(_phase) ? _phase.Text : string.Empty;

    public override void _Ready()
    {
        BindNodes();
        Refresh();
    }

    public void SetSnapshot(DemolitionTeamStatusSnapshot snapshot)
    {
        _snapshot = snapshot;
        Refresh();
    }

    public void SetLanguage(string language)
    {
        _language = GameLocalization.IsChinese(language) ? "zh" : "en";
        Refresh();
    }

    public bool LanguageMatches(string language)
    {
        if (_snapshot is not { } snapshot || !UiReady)
        {
            return false;
        }
        var normalized = GameLocalization.IsChinese(language) ? "zh" : "en";
        var expectedRound = GameLocalization.Format(
            "demolition_team_round",
            normalized,
            "ROUND {0}",
            snapshot.Round);
        return _language == normalized
            && _round.Text == expectedRound
            && AllCardsMatchLanguage(normalized);
    }

    private void BindNodes()
    {
        var band = GetNode<PanelContainer>("Band");
        var layout = band.GetNode<MarginContainer>("Margin").GetNode<HBoxContainer>("Layout");
        _friendly = layout.GetNode<HBoxContainer>("Friendly");
        _enemy = layout.GetNode<HBoxContainer>("Enemy");
        var center = layout.GetNode<PanelContainer>("Center").GetNode<VBoxContainer>("CenterStack");
        _round = center.GetNode<Label>("Round");
        _score = center.GetNode<Label>("Score");
        _timer = center.GetNode<Label>("Timer");
        _phase = center.GetNode<Label>("Phase");
        _cardScene = GD.Load<PackedScene>(DemolitionTeamMemberCard.ScenePath)
            ?? throw new System.InvalidOperationException(
                $"Unable to load {DemolitionTeamMemberCard.ScenePath}");
    }

    private void Refresh()
    {
        if (!UiReady || _snapshot is not { } snapshot)
        {
            return;
        }

        EnsureCards(_friendly, _friendlyCards, snapshot.Friendly.Count, "FriendlyCard");
        EnsureCards(_enemy, _enemyCards, snapshot.Enemy.Count, "EnemyCard");
        ApplyCards(_friendlyCards, snapshot.Friendly, friendly: true);
        ApplyCards(_enemyCards, snapshot.Enemy, friendly: false);
        _round.Text = GameLocalization.Format(
            "demolition_team_round",
            _language,
            "ROUND {0}",
            snapshot.Round);
        _score.Text = $"{snapshot.FriendlyScore}  :  {snapshot.EnemyScore}";
        _timer.Text = FormatTime(snapshot.SecondsRemaining);
        _phase.Text = PhaseTextFor(snapshot);
        _timer.AddThemeColorOverride(
            "font_color",
            snapshot.Phase == DemolitionTeamStatusPhase.DeviceActive
                ? new Color(1.0f, 0.67f, 0.14f)
                : new Color(0.93f, 0.97f, 0.95f));
    }

    private void EnsureCards(
        HBoxContainer container,
        List<DemolitionTeamMemberCard> cards,
        int count,
        string namePrefix)
    {
        while (cards.Count > count)
        {
            var index = cards.Count - 1;
            var card = cards[index];
            cards.RemoveAt(index);
            container.RemoveChild(card);
            card.QueueFree();
        }
        while (cards.Count < count)
        {
            var card = _cardScene.Instantiate<DemolitionTeamMemberCard>();
            card.Name = $"{namePrefix}_{cards.Count + 1:00}";
            cards.Add(card);
            container.AddChild(card);
        }
    }

    private void ApplyCards(
        IReadOnlyList<DemolitionTeamMemberCard> cards,
        IReadOnlyList<DemolitionTeamStatusMember> members,
        bool friendly)
    {
        for (var index = 0; index < cards.Count; index++)
        {
            cards[index].Apply(members[index], friendly, _language);
        }
    }

    private int CountCards(System.Func<DemolitionTeamMemberCard, bool> predicate)
    {
        var count = 0;
        foreach (var card in _friendlyCards)
        {
            count += predicate(card) ? 1 : 0;
        }
        foreach (var card in _enemyCards)
        {
            count += predicate(card) ? 1 : 0;
        }
        return count;
    }

    private bool AllCardsMatchLanguage(string language)
    {
        foreach (var card in _friendlyCards)
        {
            if (!card.LanguageMatches(language))
            {
                return false;
            }
        }
        foreach (var card in _enemyCards)
        {
            if (!card.LanguageMatches(language))
            {
                return false;
            }
        }
        return true;
    }

    private string PhaseTextFor(DemolitionTeamStatusSnapshot snapshot)
    {
        if (snapshot.Phase == DemolitionTeamStatusPhase.DeviceActive)
        {
            return GameLocalization.Get(
                "demolition_team_device_active",
                _language,
                "DEVICE ACTIVE");
        }
        if (snapshot.Phase == DemolitionTeamStatusPhase.Buy)
        {
            return GameLocalization.Get("demolition_team_buy", _language, "BUY PHASE");
        }
        if (snapshot.Phase == DemolitionTeamStatusPhase.Intermission)
        {
            return GameLocalization.Get(
                "demolition_team_intermission",
                _language,
                "ROUND END");
        }
        return GameLocalization.Get(
            snapshot.PlayerSide == DemolitionTeam.Attackers
                ? "demolition_buy_attack"
                : "demolition_buy_defend",
            _language,
            snapshot.PlayerSide == DemolitionTeam.Attackers ? "ATTACK" : "DEFEND");
    }

    private static string FormatTime(float secondsRemaining)
    {
        var totalSeconds = Mathf.Max(0, Mathf.CeilToInt(secondsRemaining));
        return $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
    }
}
