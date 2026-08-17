using Godot;

namespace OperationSteelTide;

/// <summary>
/// Authored demolition buy surface. Inputs are immutable round snapshots; the only
/// gameplay output is a purchase-request signal containing the local selection.
/// </summary>
[GlobalClass]
public partial class DemolitionBuyView : ColorRect
{
    [Signal] public delegate void PurchaseRequestedEventHandler(
        string sidearmId,
        string primaryId,
        bool armorSelected,
        int grenadeCount,
        int smokeGrenadeCount);

    private Label _title = null!;
    private Label _roundLabel = null!;
    private Label _sideLabel = null!;
    private Label _fundsLabel = null!;
    private Label _countdownLabel = null!;
    private ProgressBar _countdownBar = null!;
    private Label _sidearmsTitle = null!;
    private Label _primariesTitle = null!;
    private Label _protectionTitle = null!;
    private Label _utilityTitle = null!;
    private Label _summaryTitle = null!;
    private Label _summary = null!;
    private Label _projectedBalance = null!;
    private Label _grenadeCount = null!;
    private Label _smokeGrenadeCount = null!;
    private Button _p226Button = null!;
    private Button _gsh18Button = null!;
    private Button _m1911Button = null!;
    private Button _mp5Button = null!;
    private Button _ak74Button = null!;
    private Button _m4a1Button = null!;
    private Button _scarLButton = null!;
    private CheckButton _armorButton = null!;
    private Button _grenadeDecrease = null!;
    private Button _grenadeIncrease = null!;
    private Button _smokeGrenadeDecrease = null!;
    private Button _smokeGrenadeIncrease = null!;
    private Button _clearButton = null!;
    private Button _confirmButton = null!;
    private DemolitionBuySnapshot _snapshot;
    private DemolitionPurchaseSelection _selection = DemolitionPurchaseSelection.Empty;
    private DemolitionPurchaseQuote _quote;
    private string _language = "en";

    public DemolitionPurchaseSelection CurrentSelection => _selection;
    public DemolitionPurchaseQuote CurrentQuote => _quote;
    public int DisplayedFunds => _snapshot.Funds;
    public float DisplayedSeconds => _snapshot.SecondsRemaining;
    public bool ConfirmEnabled => IsInstanceValid(_confirmButton) && !_confirmButton.Disabled;
    public bool IsSidearmOfferEnabled(string id) => id switch
    {
        DemolitionBuyCatalog.P226Id => !_p226Button.Disabled,
        DemolitionBuyCatalog.Gsh18Id => !_gsh18Button.Disabled,
        DemolitionBuyCatalog.M1911Id => !_m1911Button.Disabled,
        _ => false
    };
    public bool IsPrimaryOfferEnabled(string id) => id switch
    {
        DemolitionBuyCatalog.Mp5Id => !_mp5Button.Disabled,
        DemolitionBuyCatalog.Ak74Id => !_ak74Button.Disabled,
        DemolitionBuyCatalog.M4A1Id => !_m4a1Button.Disabled,
        DemolitionBuyCatalog.ScarLId => !_scarLButton.Disabled,
        _ => false
    };
    public bool UiReady
        => IsInstanceValid(_title)
        && IsInstanceValid(_countdownBar)
        && IsInstanceValid(_p226Button)
        && IsInstanceValid(_gsh18Button)
        && IsInstanceValid(_scarLButton)
        && IsInstanceValid(_armorButton)
        && IsInstanceValid(_smokeGrenadeCount)
        && IsInstanceValid(_confirmButton);
    public bool IntentSignalsConnected
        => HasConnections(SignalName.PurchaseRequested)
        && _p226Button.HasConnections(BaseButton.SignalName.Pressed)
        && _gsh18Button.HasConnections(BaseButton.SignalName.Pressed)
        && _m1911Button.HasConnections(BaseButton.SignalName.Pressed)
        && _mp5Button.HasConnections(BaseButton.SignalName.Pressed)
        && _ak74Button.HasConnections(BaseButton.SignalName.Pressed)
        && _m4a1Button.HasConnections(BaseButton.SignalName.Pressed)
        && _scarLButton.HasConnections(BaseButton.SignalName.Pressed)
        && _armorButton.HasConnections(BaseButton.SignalName.Toggled)
        && _grenadeDecrease.HasConnections(BaseButton.SignalName.Pressed)
        && _grenadeIncrease.HasConnections(BaseButton.SignalName.Pressed)
        && _smokeGrenadeDecrease.HasConnections(BaseButton.SignalName.Pressed)
        && _smokeGrenadeIncrease.HasConnections(BaseButton.SignalName.Pressed)
        && _clearButton.HasConnections(BaseButton.SignalName.Pressed)
        && _confirmButton.HasConnections(BaseButton.SignalName.Pressed);

    public override void _Ready()
    {
        BindNodes();
        ConnectIntentSignals();
        SetLanguage(_language);
        Refresh();
    }

    public void BeginRound(DemolitionBuySnapshot snapshot)
    {
        _selection = DemolitionPurchaseSelection.Empty;
        Visible = true;
        SetSnapshot(snapshot);
    }

    public void SetSnapshot(DemolitionBuySnapshot snapshot)
    {
        _snapshot = snapshot;
        Refresh();
    }

    public void SetLanguage(string language)
    {
        _language = GameLocalization.IsChinese(language) ? "zh" : "en";
        if (!IsInstanceValid(_title))
        {
            return;
        }
        _title.Text = Text("demolition_buy_title", "ROUND BUY");
        _sidearmsTitle.Text = Text("demolition_buy_sidearms", "SIDEARMS");
        _primariesTitle.Text = Text("demolition_buy_primaries", "PRIMARY WEAPONS");
        _protectionTitle.Text = Text("demolition_buy_protection", "PROTECTION");
        _utilityTitle.Text = Text("demolition_buy_utility", "UTILITY");
        _summaryTitle.Text = Text("demolition_buy_summary", "PURCHASE SUMMARY");
        _armorButton.Text = $"{Text("demolition_buy_armor", "ARMOR KIT")}  ${DemolitionBuyCatalog.ArmorPrice}";
        _clearButton.Text = Text("demolition_buy_clear", "CLEAR");
        _confirmButton.Text = Text("demolition_buy_confirm", "CONFIRM PURCHASE");
        SetOfferText(_p226Button, DemolitionBuyCatalog.Sidearms[0]);
        SetOfferText(_gsh18Button, DemolitionBuyCatalog.Sidearms[1]);
        SetOfferText(_m1911Button, DemolitionBuyCatalog.Sidearms[2]);
        SetOfferText(_mp5Button, DemolitionBuyCatalog.Primaries[0]);
        SetOfferText(_ak74Button, DemolitionBuyCatalog.Primaries[1]);
        SetOfferText(_m4a1Button, DemolitionBuyCatalog.Primaries[2]);
        SetOfferText(_scarLButton, DemolitionBuyCatalog.Primaries[3]);
        Refresh();
    }

    public bool LanguageMatches(string language)
    {
        var normalized = GameLocalization.IsChinese(language) ? "zh" : "en";
        return _language == normalized
            && _title.Text == GameLocalization.Get("demolition_buy_title", normalized, "ROUND BUY")
            && _confirmButton.Text == GameLocalization.Get(
                "demolition_buy_confirm",
                normalized,
                "CONFIRM PURCHASE");
    }

    public void SelectSidearmForDiagnostics(string id) => SelectSidearm(id);

    public void SelectPrimaryForDiagnostics(string id) => SelectPrimary(id);

    public void SetArmorForDiagnostics(bool selected)
    {
        _selection = _selection with { ArmorSelected = selected };
        Refresh();
    }

    public void SetGrenadesForDiagnostics(int count)
    {
        _selection = _selection with { GrenadeCount = count };
        Refresh();
    }

    public void SetSmokeGrenadesForDiagnostics(int count)
    {
        _selection = _selection with { SmokeGrenadeCount = count };
        Refresh();
    }

    public void PressConfirmForDiagnostics() => EmitPurchaseIntent();

    public void SubmitTimeoutSelection()
    {
        if (!_quote.Affordable)
        {
            _selection = DemolitionPurchaseSelection.Empty;
            Refresh();
        }
        EmitPurchaseIntent();
    }

    private void BindNodes()
    {
        var panel = GetNode<Control>("Panel");
        _title = panel.GetNode<Label>("Title");
        _roundLabel = panel.GetNode<Label>("RoundLabel");
        _sideLabel = panel.GetNode<Label>("SideLabel");
        _fundsLabel = panel.GetNode<Label>("FundsLabel");
        _countdownLabel = panel.GetNode<Label>("CountdownLabel");
        _countdownBar = panel.GetNode<ProgressBar>("CountdownBar");
        var offers = panel.GetNode<Control>("Offers");
        _sidearmsTitle = offers.GetNode<Label>("SidearmsTitle");
        _primariesTitle = offers.GetNode<Label>("PrimariesTitle");
        _protectionTitle = offers.GetNode<Label>("ProtectionTitle");
        _utilityTitle = offers.GetNode<Label>("UtilityTitle");
        _p226Button = offers.GetNode<Button>("P226Button");
        _gsh18Button = offers.GetNode<Button>("GSh18Button");
        _m1911Button = offers.GetNode<Button>("M1911Button");
        _mp5Button = offers.GetNode<Button>("MP5Button");
        _ak74Button = offers.GetNode<Button>("AK74Button");
        _m4a1Button = offers.GetNode<Button>("M4A1Button");
        _scarLButton = offers.GetNode<Button>("ScarLButton");
        _armorButton = offers.GetNode<CheckButton>("ArmorButton");
        _grenadeDecrease = offers.GetNode<Button>("GrenadeDecrease");
        _grenadeIncrease = offers.GetNode<Button>("GrenadeIncrease");
        _grenadeCount = offers.GetNode<Label>("GrenadeCount");
        _smokeGrenadeDecrease = offers.GetNode<Button>("SmokeGrenadeDecrease");
        _smokeGrenadeIncrease = offers.GetNode<Button>("SmokeGrenadeIncrease");
        _smokeGrenadeCount = offers.GetNode<Label>("SmokeGrenadeCount");
        var summaryPanel = panel.GetNode<Control>("SummaryPanel");
        _summaryTitle = summaryPanel.GetNode<Label>("SummaryTitle");
        _summary = summaryPanel.GetNode<Label>("Summary");
        _projectedBalance = summaryPanel.GetNode<Label>("ProjectedBalance");
        _clearButton = summaryPanel.GetNode<Button>("ClearButton");
        _confirmButton = summaryPanel.GetNode<Button>("ConfirmButton");
    }

    private void ConnectIntentSignals()
    {
        _p226Button.Pressed += () => SelectSidearm(DemolitionBuyCatalog.P226Id);
        _gsh18Button.Pressed += () => SelectSidearm(DemolitionBuyCatalog.Gsh18Id);
        _m1911Button.Pressed += () => SelectSidearm(DemolitionBuyCatalog.M1911Id);
        _mp5Button.Pressed += () => SelectPrimary(DemolitionBuyCatalog.Mp5Id);
        _ak74Button.Pressed += () => SelectPrimary(DemolitionBuyCatalog.Ak74Id);
        _m4a1Button.Pressed += () => SelectPrimary(DemolitionBuyCatalog.M4A1Id);
        _scarLButton.Pressed += () => SelectPrimary(DemolitionBuyCatalog.ScarLId);
        _armorButton.Toggled += selected =>
        {
            _selection = _selection with { ArmorSelected = selected };
            Refresh();
        };
        _grenadeDecrease.Pressed += () =>
        {
            _selection = _selection with { GrenadeCount = _selection.GrenadeCount - 1 };
            Refresh();
        };
        _grenadeIncrease.Pressed += () =>
        {
            _selection = _selection with { GrenadeCount = _selection.GrenadeCount + 1 };
            Refresh();
        };
        _smokeGrenadeDecrease.Pressed += () =>
        {
            _selection = _selection with { SmokeGrenadeCount = _selection.SmokeGrenadeCount - 1 };
            Refresh();
        };
        _smokeGrenadeIncrease.Pressed += () =>
        {
            _selection = _selection with { SmokeGrenadeCount = _selection.SmokeGrenadeCount + 1 };
            Refresh();
        };
        _clearButton.Pressed += () =>
        {
            _selection = DemolitionPurchaseSelection.Empty;
            Refresh();
        };
        _confirmButton.Pressed += EmitPurchaseIntent;
    }

    private void SelectSidearm(string id)
    {
        var normalized = DemolitionBuyCatalog.Sidearm(id)?.Id ?? string.Empty;
        _selection = _selection with
        {
            SidearmId = _selection.SidearmId == normalized ? string.Empty : normalized
        };
        Refresh();
    }

    private void SelectPrimary(string id)
    {
        var normalized = DemolitionBuyCatalog.Primary(id)?.Id ?? string.Empty;
        _selection = _selection with
        {
            PrimaryId = _selection.PrimaryId == normalized ? string.Empty : normalized
        };
        Refresh();
    }

    private void EmitPurchaseIntent()
    {
        if (!_quote.Affordable)
        {
            return;
        }
        EmitSignal(
            SignalName.PurchaseRequested,
            _quote.Selection.SidearmId,
            _quote.Selection.PrimaryId,
            _quote.Selection.ArmorSelected,
            _quote.Selection.GrenadeCount,
            _quote.Selection.SmokeGrenadeCount);
    }

    private void Refresh()
    {
        if (!IsInstanceValid(_confirmButton))
        {
            return;
        }
        _selection = DemolitionBuyCatalog.Normalize(_selection);
        _quote = DemolitionBuyCatalog.Quote(_selection, _snapshot.Funds);
        _roundLabel.Text = GameLocalization.Format(
            "demolition_buy_round_score",
            _language,
            "ROUND {0}  //  YOU {1}:{2} ENEMY",
            _snapshot.Round,
            _snapshot.PlayerScore,
            _snapshot.OpponentScore);
        _sideLabel.Text = _snapshot.PlayerSide == DemolitionTeam.Attackers
            ? Text("demolition_buy_attack", "ATTACK")
            : Text("demolition_buy_defend", "DEFEND");
        _fundsLabel.Text = $"{Text("demolition_buy_funds", "FUNDS")}  ${_snapshot.Funds}";
        _countdownLabel.Text = GameLocalization.Format(
            "demolition_buy_countdown",
            _language,
            "BUY  {0:0.0}s",
            _snapshot.SecondsRemaining);
        _countdownBar.MaxValue = Mathf.Max(0.1f, _snapshot.Duration);
        _countdownBar.Value = Mathf.Clamp(_snapshot.SecondsRemaining, 0.0f, _snapshot.Duration);
        _armorButton.SetPressedNoSignal(_selection.ArmorSelected);
        _grenadeCount.Text = $"{Text("grenade", "FRAG")}  x{_selection.GrenadeCount}  //  ${DemolitionBuyCatalog.GrenadePrice}";
        _smokeGrenadeCount.Text = $"{Text("smoke_grenade", "SMOKE")}  x{_selection.SmokeGrenadeCount}  //  ${DemolitionBuyCatalog.SmokeGrenadePrice}";
        SetSelected(_p226Button, _selection.SidearmId == DemolitionBuyCatalog.P226Id);
        SetSelected(_gsh18Button, _selection.SidearmId == DemolitionBuyCatalog.Gsh18Id);
        SetSelected(_m1911Button, _selection.SidearmId == DemolitionBuyCatalog.M1911Id);
        SetSelected(_mp5Button, _selection.PrimaryId == DemolitionBuyCatalog.Mp5Id);
        SetSelected(_ak74Button, _selection.PrimaryId == DemolitionBuyCatalog.Ak74Id);
        SetSelected(_m4a1Button, _selection.PrimaryId == DemolitionBuyCatalog.M4A1Id);
        SetSelected(_scarLButton, _selection.PrimaryId == DemolitionBuyCatalog.ScarLId);
        _p226Button.Disabled = DemolitionBuyCatalog.Sidearms[0].Price > _snapshot.Funds;
        _gsh18Button.Disabled = DemolitionBuyCatalog.Sidearms[1].Price > _snapshot.Funds;
        _m1911Button.Disabled = DemolitionBuyCatalog.Sidearms[2].Price > _snapshot.Funds;
        _mp5Button.Disabled = DemolitionBuyCatalog.Primaries[0].Price > _snapshot.Funds;
        _ak74Button.Disabled = DemolitionBuyCatalog.Primaries[1].Price > _snapshot.Funds;
        _m4a1Button.Disabled = DemolitionBuyCatalog.Primaries[2].Price > _snapshot.Funds;
        _scarLButton.Disabled = DemolitionBuyCatalog.Primaries[3].Price > _snapshot.Funds;
        _grenadeDecrease.Disabled = _selection.GrenadeCount <= 0;
        _grenadeIncrease.Disabled = _selection.GrenadeCount >= DemolitionBuyCatalog.MaximumGrenades;
        _smokeGrenadeDecrease.Disabled = _selection.SmokeGrenadeCount <= 0;
        _smokeGrenadeIncrease.Disabled = _selection.SmokeGrenadeCount >= DemolitionBuyCatalog.MaximumSmokeGrenades;
        _summary.Text = BuildSummary();
        _projectedBalance.Text = _quote.Affordable
            ? GameLocalization.Format(
                "demolition_buy_balance",
                _language,
                "TOTAL  ${0}  //  BALANCE  ${1}",
                _quote.TotalCost,
                _quote.RemainingFunds)
            : GameLocalization.Format(
                "demolition_buy_insufficient",
                _language,
                "TOTAL  ${0}  //  INSUFFICIENT FUNDS",
                _quote.TotalCost);
        _projectedBalance.AddThemeColorOverride(
            "font_color",
            _quote.Affordable ? new Color(0.42f, 0.92f, 0.67f) : new Color(1.0f, 0.37f, 0.22f));
        _confirmButton.Disabled = !_quote.Affordable;
    }

    private string BuildSummary()
    {
        var primary = DemolitionBuyCatalog.Primary(_selection.PrimaryId);
        var sidearm = DemolitionBuyCatalog.Sidearm(_selection.SidearmId);
        var firearmLine = primary is null && sidearm is null
            ? Text("demolition_buy_knife_only", "KNIFE ONLY")
            : $"{OfferName(primary)}\n{OfferName(sidearm)}".Trim();
        var armor = _selection.ArmorSelected
            ? Text("demolition_buy_armor", "ARMOR KIT")
            : Text("demolition_buy_no_armor", "NO ARMOR");
        var utilities = new System.Collections.Generic.List<string>();
        if (_selection.GrenadeCount > 0)
        {
            utilities.Add($"{Text("grenade", "FRAG")}  x{_selection.GrenadeCount}");
        }
        if (_selection.SmokeGrenadeCount > 0)
        {
            utilities.Add($"{Text("smoke_grenade", "SMOKE")}  x{_selection.SmokeGrenadeCount}");
        }
        var utility = utilities.Count > 0
            ? string.Join("\n", utilities)
            : Text("demolition_buy_no_utility", "NO UTILITY");
        return $"{firearmLine}\n\n{armor}\n{utility}";
    }

    private string OfferName(DemolitionBuyOffer? offer)
        => offer is null ? string.Empty : Text(offer.LocalizationKey, offer.EnglishName);

    private void SetOfferText(Button button, DemolitionBuyOffer offer)
        => button.Text = $"{OfferName(offer)}\n${offer.Price}";

    private static void SetSelected(Button button, bool selected) => button.SetPressedNoSignal(selected);

    private string Text(string key, string english) => GameLocalization.Get(key, _language, english);
}
