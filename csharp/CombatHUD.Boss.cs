using Godot;

namespace OperationSteelTide;

public partial class CombatHUD
{
    private Control _worldBossRoot = null!;
    private Label _worldBossName = null!;
    private Label _worldBossState = null!;
    private ProgressBar _worldBossHealth = null!;
    private bool _worldBossHudActive;
    private float _worldBossHudCurrent;
    private float _worldBossHudMaximum = 1.0f;
    private int _worldBossHudPhase = 1;
    private float _worldBossHudDistance;
    private bool _worldBossHudCharging;

    public bool WorldBossHudVisible => IsInstanceValid(_worldBossRoot) && _worldBossRoot.Visible;
    public int WorldBossHudPhase => _worldBossHudPhase;
    public float WorldBossHudHealthRatio => _worldBossHudMaximum > 0.0f
        ? Mathf.Clamp(_worldBossHudCurrent / _worldBossHudMaximum, 0.0f, 1.0f)
        : 0.0f;
    public bool MinimapWorldBossVisible => IsInstanceValid(_minimap) && _minimap.WorldBossVisible;

    private void BuildWorldBossHud(Control root)
    {
        _worldBossRoot = new Control
        {
            Name = "WorldBossHud",
            Size = new Vector2(640, 60),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false,
            ZIndex = 8
        };
        _worldBossRoot.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
        _worldBossRoot.Position = new Vector2(-320, 118);
        root.AddChild(_worldBossRoot);

        _worldBossRoot.AddChild(new ColorRect
        {
            Size = new Vector2(640, 60),
            Color = new Color(0.008f, 0.018f, 0.019f, 0.9f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        });
        _worldBossRoot.AddChild(new ColorRect
        {
            Size = new Vector2(4, 60),
            Color = new Color(0.16f, 1.0f, 0.78f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        });
        _worldBossRoot.AddChild(new ColorRect
        {
            Position = new Vector2(4, 0),
            Size = new Vector2(636, 1),
            Color = new Color(0.16f, 0.76f, 0.62f, 0.75f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        });

        _worldBossName = Label("TIDE HUNTER", 14, new Color(0.36f, 1.0f, 0.82f));
        _worldBossName.Position = new Vector2(16, 8);
        _worldBossName.Size = new Vector2(220, 20);
        _worldBossRoot.AddChild(_worldBossName);

        _worldBossState = Label("PHASE 1  //  HUNT  //  000 m", 11, new Color(0.7f, 0.82f, 0.78f));
        _worldBossState.Position = new Vector2(225, 9);
        _worldBossState.Size = new Vector2(399, 18);
        _worldBossState.HorizontalAlignment = HorizontalAlignment.Right;
        _worldBossState.ClipText = true;
        _worldBossRoot.AddChild(_worldBossState);

        _worldBossHealth = new ProgressBar
        {
            Position = new Vector2(16, 38),
            Size = new Vector2(608, 8),
            MinValue = 0,
            MaxValue = 1,
            Value = 1,
            ShowPercentage = false,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _worldBossHealth.AddThemeStyleboxOverride("background", FlatStyle(new Color(0.06f, 0.095f, 0.09f), Colors.Transparent));
        _worldBossHealth.AddThemeStyleboxOverride("fill", FlatStyle(new Color(0.16f, 0.92f, 0.7f), Colors.Transparent));
        _worldBossRoot.AddChild(_worldBossHealth);
    }

    public void SetWorldBossStatus(
        bool active,
        float currentHealth,
        float maximumHealth,
        int phase,
        float distance,
        bool charging)
    {
        _worldBossHudActive = active;
        _worldBossHudCurrent = Mathf.Max(0.0f, currentHealth);
        _worldBossHudMaximum = Mathf.Max(1.0f, maximumHealth);
        _worldBossHudPhase = Mathf.Clamp(phase, 1, 3);
        _worldBossHudDistance = Mathf.Max(0.0f, distance);
        _worldBossHudCharging = charging;
        RefreshWorldBossHud();
    }

    public void SetMinimapWorldBoss(Vector3 position, bool active)
    {
        if (IsInstanceValid(_minimap))
        {
            _minimap.SetWorldBoss(position, active);
        }
    }

    private void RefreshWorldBossHud()
    {
        if (!IsInstanceValid(_worldBossRoot))
        {
            return;
        }
        _worldBossRoot.Visible = _worldBossHudActive;
        if (IsInstanceValid(_radioLabel))
        {
            _radioLabel.Position = new Vector2(-310, _worldBossHudActive ? 188 : 128);
        }
        if (!_worldBossHudActive)
        {
            return;
        }

        var phaseKey = _worldBossHudPhase switch
        {
            2 => "boss_phase_surge",
            3 => "boss_phase_riptide",
            _ => "boss_phase_hunt"
        };
        var phaseEnglish = _worldBossHudPhase switch
        {
            2 => "TIDAL SURGE",
            3 => "RIPTIDE OVERDRIVE",
            _ => "LONG-RANGE HUNT"
        };
        var phaseName = GameLocalization.Get(phaseKey, _language, phaseEnglish);
        var charge = _worldBossHudCharging
            ? "  //  " + GameLocalization.Get("boss_pulse_charge", _language, "PULSE CHARGING")
            : string.Empty;
        _worldBossName.Text = GameLocalization.Get("boss_name", _language, "TIDE HUNTER");
        _worldBossState.Text = GameLocalization.IsChinese(_language)
            ? $"\u9636\u6bb5 {_worldBossHudPhase}  //  {phaseName}  //  {_worldBossHudDistance:0} \u7c73{charge}"
            : $"PHASE {_worldBossHudPhase}  //  {phaseName}  //  {_worldBossHudDistance:0} m{charge}";
        _worldBossHealth.MaxValue = _worldBossHudMaximum;
        _worldBossHealth.Value = _worldBossHudCurrent;
        var fill = _worldBossHudPhase switch
        {
            2 => new Color(0.12f, 0.84f, 0.72f),
            3 => new Color(1.0f, 0.28f, 0.18f),
            _ => new Color(0.2f, 0.95f, 0.7f)
        };
        _worldBossHealth.AddThemeStyleboxOverride("fill", FlatStyle(fill, Colors.Transparent));
        _worldBossName.AddThemeColorOverride("font_color", _worldBossHudPhase >= 3
            ? new Color(1.0f, 0.42f, 0.28f)
            : new Color(0.36f, 1.0f, 0.82f));
    }
}
