using Godot;

namespace OperationSteelTide;

public partial class CombatHUD
{
    private Control _extractionRoot = null!;
    private Label _extractionTitle = null!;
    private Label _extractionTimer = null!;
    private Label _extractionHint = null!;
    private Label _extractionSquad = null!;
    private ProgressBar _extractionProgress = null!;
    private float _extractionRemaining;
    private float _extractionTotal = 12.0f;
    private bool _extractionAircraftReady;
    private int _extractionSquadReady = 1;
    private int _extractionSquadTotal = 1;

    public bool IsExtractionCountdownVisible => IsInstanceValid(_extractionRoot) && _extractionRoot.Visible;
    public float ExtractionCountdownSeconds => _extractionRemaining;
    public bool ExtractionAircraftReady => _extractionAircraftReady;

    private void BuildExtractionHud(Control root)
    {
        _extractionRoot = new Control
        {
            Name = "ExtractionCountdown",
            Position = new Vector2(-270, 112),
            Size = new Vector2(540, 118),
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _extractionRoot.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
        root.AddChild(_extractionRoot);

        var background = new ColorRect
        {
            Size = new Vector2(540, 118),
            Color = new Color(0.008f, 0.016f, 0.015f, 0.985f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _extractionRoot.AddChild(background);
        background.AddChild(new ColorRect
        {
            Size = new Vector2(4, 118),
            Color = new Color(0.12f, 0.96f, 0.55f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        });
        background.AddChild(new ColorRect
        {
            Position = new Vector2(4, 0),
            Size = new Vector2(536, 1),
            Color = new Color(0.26f, 0.64f, 0.49f, 0.72f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        });

        _extractionTitle = Label("FRIENDLY TILT-ROTOR INBOUND", 15, new Color(0.55f, 1.0f, 0.74f));
        _extractionTitle.Position = new Vector2(22, 12);
        _extractionTitle.Size = new Vector2(350, 24);
        _extractionTitle.ClipText = true;
        background.AddChild(_extractionTitle);

        _extractionTimer = Label("00:12", 30, new Color(0.94f, 1.0f, 0.96f));
        _extractionTimer.Position = new Vector2(385, 4);
        _extractionTimer.Size = new Vector2(132, 42);
        _extractionTimer.HorizontalAlignment = HorizontalAlignment.Right;
        background.AddChild(_extractionTimer);

        _extractionHint = Label("STAY INSIDE THE EXTRACTION ZONE  //  LEAVING RESETS THE TIMER", 12, new Color(0.98f, 1.0f, 0.98f));
        _extractionHint.Position = new Vector2(22, 45);
        _extractionHint.Size = new Vector2(370, 38);
        _extractionHint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _extractionHint.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.0f));
        _extractionHint.AddThemeConstantOverride("shadow_offset_x", 0);
        _extractionHint.AddThemeConstantOverride("shadow_offset_y", 0);
        background.AddChild(_extractionHint);

        _extractionSquad = Label("SQUAD READY  1/3", 12, new Color(1.0f, 0.72f, 0.28f));
        _extractionSquad.Position = new Vector2(390, 49);
        _extractionSquad.Size = new Vector2(127, 24);
        _extractionSquad.HorizontalAlignment = HorizontalAlignment.Right;
        background.AddChild(_extractionSquad);

        _extractionProgress = new ProgressBar
        {
            Position = new Vector2(22, 92),
            Size = new Vector2(495, 7),
            MinValue = 0.0,
            MaxValue = 1.0,
            Value = 0.0,
            ShowPercentage = false,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        var progressBackground = new StyleBoxFlat { BgColor = new Color(0.07f, 0.13f, 0.12f, 0.95f) };
        progressBackground.SetCornerRadiusAll(2);
        var progressFill = new StyleBoxFlat { BgColor = new Color(0.12f, 0.92f, 0.52f) };
        progressFill.SetCornerRadiusAll(2);
        _extractionProgress.AddThemeStyleboxOverride("background", progressBackground);
        _extractionProgress.AddThemeStyleboxOverride("fill", progressFill);
        background.AddChild(_extractionProgress);
    }

    public void SetExtractionCountdown(
        float remaining,
        float total,
        bool aircraftReady,
        int squadReady,
        int squadTotal)
    {
        _extractionRemaining = Mathf.Max(0.0f, remaining);
        _extractionTotal = Mathf.Max(0.1f, total);
        _extractionAircraftReady = aircraftReady;
        _extractionSquadReady = Mathf.Max(1, squadReady);
        _extractionSquadTotal = Mathf.Max(_extractionSquadReady, squadTotal);
        _extractionRoot.Visible = true;
        _extractionProgress.Value = Mathf.Clamp(1.0f - _extractionRemaining / _extractionTotal, 0.0f, 1.0f);
        RefreshExtractionLanguage();
    }

    public void HideExtractionCountdown()
    {
        if (IsInstanceValid(_extractionRoot))
        {
            _extractionRoot.Visible = false;
        }
    }

    private void RefreshExtractionLanguage()
    {
        if (!IsInstanceValid(_extractionRoot))
        {
            return;
        }

        _extractionTitle.Text = _extractionAircraftReady
            ? Text("extraction_boarding", "RESCUE AIRCRAFT ON PAD  //  BOARDING")
            : Text("extraction_inbound", "FRIENDLY TILT-ROTOR INBOUND");
        _extractionHint.Text = Text(
            "extraction_hold",
            "STAY INSIDE THE EXTRACTION ZONE  //  LEAVING RESETS THE TIMER");
        _extractionSquad.Text = $"{Text("extraction_squad", "SQUAD READY")}  {_extractionSquadReady}/{_extractionSquadTotal}";
        _extractionTimer.Text = $"00:{Mathf.CeilToInt(_extractionRemaining):00}";
        var urgent = _extractionRemaining <= 3.0f;
        _extractionTimer.AddThemeColorOverride(
            "font_color",
            urgent ? new Color(1.0f, 0.66f, 0.22f) : new Color(0.94f, 1.0f, 0.96f));
    }
}
