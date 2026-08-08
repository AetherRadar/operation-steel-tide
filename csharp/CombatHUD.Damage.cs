using Godot;

namespace OperationSteelTide;

public partial class CombatHUD
{
    private Control _incomingDamageIndicator = null!;
    private Polygon2D _incomingDamageArrow = null!;
    private Label _incomingDamageReadout = null!;
    private Label _incomingDamageSource = null!;
    private AudioStreamPlayer _incomingDamageAudio = null!;
    private Tween? _incomingDamageTween;

    public float LastIncomingDamage { get; private set; }
    public float LastIncomingAngle { get; private set; }
    public HitRegion LastIncomingRegion { get; private set; } = HitRegion.Torso;
    public string LastIncomingSource { get; private set; } = string.Empty;
    public bool IsIncomingDamageVisible => IsInstanceValid(_incomingDamageReadout) && _incomingDamageReadout.Visible;

    private void BuildIncomingDamageHud(Control root)
    {
        _incomingDamageIndicator = new Control
        {
            Position = new Vector2(-240, -240),
            Size = new Vector2(480, 480),
            PivotOffset = new Vector2(240, 240),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false
        };
        _incomingDamageIndicator.SetAnchorsPreset(Control.LayoutPreset.Center);
        root.AddChild(_incomingDamageIndicator);
        _incomingDamageArrow = new Polygon2D
        {
            Position = new Vector2(240, 34),
            Polygon = new[]
            {
                new Vector2(-24, 18),
                new Vector2(0, -19),
                new Vector2(24, 18),
                new Vector2(10, 12),
                new Vector2(0, -2),
                new Vector2(-10, 12)
            },
            Color = new Color(1.0f, 0.25f, 0.14f)
        };
        _incomingDamageIndicator.AddChild(_incomingDamageArrow);

        _incomingDamageReadout = Label(string.Empty, 25, new Color(1.0f, 0.82f, 0.75f));
        _incomingDamageReadout.HorizontalAlignment = HorizontalAlignment.Center;
        _incomingDamageReadout.SetAnchorsPreset(Control.LayoutPreset.Center);
        _incomingDamageReadout.Position = new Vector2(-360, -176);
        _incomingDamageReadout.Size = new Vector2(720, 36);
        _incomingDamageReadout.Visible = false;
        root.AddChild(_incomingDamageReadout);
        _incomingDamageSource = Label(string.Empty, 13, new Color(1.0f, 0.48f, 0.28f));
        _incomingDamageSource.HorizontalAlignment = HorizontalAlignment.Center;
        _incomingDamageSource.SetAnchorsPreset(Control.LayoutPreset.Center);
        _incomingDamageSource.Position = new Vector2(-300, -140);
        _incomingDamageSource.Size = new Vector2(600, 24);
        _incomingDamageSource.Visible = false;
        root.AddChild(_incomingDamageSource);

        _incomingDamageAudio = new AudioStreamPlayer
        {
            Stream = SoundLab.PlayerHit(),
            VolumeDb = -4.0f
        };
        AddChild(_incomingDamageAudio);
    }

    public void ShowIncomingDamage(
        float amount,
        float angle,
        HitRegion region,
        bool armorHit,
        string sourceKey,
        string sourceEnglish)
    {
        LastIncomingDamage = Mathf.Max(0.0f, amount);
        LastIncomingAngle = angle;
        LastIncomingRegion = region;
        LastIncomingSource = sourceEnglish;
        ShowDamage(armorHit ? 0.68f : Mathf.Clamp(0.72f + amount / 100.0f, 0.72f, 0.96f));

        var accent = armorHit ? new Color(0.3f, 0.68f, 1.0f) : new Color(1.0f, 0.24f, 0.12f);
        var directionKey = DirectionKey(angle);
        var directionEnglish = DirectionEnglish(angle);
        var regionKey = region switch
        {
            HitRegion.Head => "damage_region_head",
            HitRegion.Limbs => "damage_region_limbs",
            _ => "damage_region_torso"
        };
        var regionEnglish = region switch
        {
            HitRegion.Head => "HEAD",
            HitRegion.Limbs => "LIMBS",
            _ => "TORSO"
        };

        _incomingDamageIndicator.Visible = true;
        _incomingDamageIndicator.Modulate = Colors.White;
        _incomingDamageIndicator.Rotation = angle;
        _incomingDamageIndicator.Scale = Vector2.One * 1.2f;
        _incomingDamageArrow.Color = accent;
        _incomingDamageReadout.Visible = true;
        _incomingDamageReadout.Modulate = Colors.White;
        _incomingDamageReadout.Text = $"-{Mathf.CeilToInt(amount):00} HP  //  {Text(directionKey, directionEnglish)}  //  {Text(regionKey, regionEnglish)}";
        _incomingDamageReadout.AddThemeColorOverride("font_color", armorHit ? new Color(0.62f, 0.82f, 1.0f) : new Color(1.0f, 0.82f, 0.75f));
        _incomingDamageSource.Visible = true;
        _incomingDamageSource.Modulate = Colors.White;
        _incomingDamageSource.Text = $"{Text("damage_incoming", "INCOMING FIRE")}  //  {Text(sourceKey, sourceEnglish)}";
        _incomingDamageSource.AddThemeColorOverride("font_color", accent.Lightened(0.12f));

        if (_incomingDamageTween?.IsRunning() == true)
        {
            _incomingDamageTween.Kill();
        }
        _incomingDamageTween = CreateTween().SetParallel(true);
        _incomingDamageTween.TweenProperty(_incomingDamageIndicator, "scale", Vector2.One, 0.12f)
            .SetTrans(Tween.TransitionType.Back)
            .SetEase(Tween.EaseType.Out);
        _incomingDamageTween.TweenProperty(_incomingDamageIndicator, "modulate:a", 0.0f, 0.48f).SetDelay(0.34f);
        _incomingDamageTween.TweenProperty(_incomingDamageReadout, "modulate:a", 0.0f, 0.58f).SetDelay(0.58f);
        _incomingDamageTween.TweenProperty(_incomingDamageSource, "modulate:a", 0.0f, 0.58f).SetDelay(0.58f);
        _incomingDamageTween.Chain().TweenCallback(Callable.From(() =>
        {
            _incomingDamageIndicator.Visible = false;
            _incomingDamageReadout.Visible = false;
            _incomingDamageSource.Visible = false;
        }));

        _incomingDamageAudio.PitchScale = armorHit ? 1.18f : Mathf.Clamp(0.92f - amount / 240.0f, 0.72f, 0.92f);
        _incomingDamageAudio.VolumeDb = armorHit ? -6.0f : -2.0f;
        _incomingDamageAudio.Play();
    }

    private static string DirectionKey(float angle)
    {
        var normalized = Mathf.PosMod(angle + Mathf.Pi, Mathf.Pi * 2.0f) - Mathf.Pi;
        if (Mathf.Abs(normalized) <= Mathf.Pi * 0.25f)
        {
            return "damage_front";
        }
        if (Mathf.Abs(normalized) >= Mathf.Pi * 0.75f)
        {
            return "damage_rear";
        }
        return normalized > 0.0f ? "damage_right" : "damage_left";
    }

    private static string DirectionEnglish(float angle)
    {
        var key = DirectionKey(angle);
        return key switch
        {
            "damage_rear" => "REAR",
            "damage_right" => "RIGHT",
            "damage_left" => "LEFT",
            _ => "FRONT"
        };
    }
}
