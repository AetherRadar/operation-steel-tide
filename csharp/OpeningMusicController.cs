using Godot;

namespace OperationSteelTide;

/// <summary>
/// Owns the opening/menu theme. The composition root supplies menu activity;
/// the controller keeps playback alive while menus pause the scene tree and
/// fades it out before battlefield audio becomes the foreground.
/// </summary>
internal partial class OpeningMusicController : Node
{
    private const float MenuGain = 0.18f;
    private const float FadeInSeconds = 1.2f;
    private const float FadeOutSeconds = 0.65f;
    private AudioStreamPlayer _player = null!;
    private bool _menuActive;
    private float _gain;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _player = new AudioStreamPlayer
        {
            Name = "OpeningCombatThemePlayer",
            Stream = SoundLab.OpeningCombatTheme(),
            VolumeLinear = 0.0f,
            MaxPolyphony = 1,
            ProcessMode = ProcessModeEnum.Always
        };
        AddChild(_player);
    }

    public override void _Process(double delta)
    {
        var targetGain = _menuActive ? MenuGain : 0.0f;
        var fadeSeconds = _menuActive ? FadeInSeconds : FadeOutSeconds;
        _gain = Mathf.MoveToward(_gain, targetGain, MenuGain * (float)delta / fadeSeconds);
        _player.VolumeLinear = _gain;
        if (!_menuActive && _gain <= 0.0f && _player.Playing)
        {
            _player.Stop();
        }
    }

    public void SetMenuActive(bool active, bool immediate = false)
    {
        _menuActive = active;
        if (active && !_player.Playing)
        {
            _player.Play();
        }
        if (!immediate)
        {
            return;
        }

        _gain = active ? MenuGain : 0.0f;
        _player.VolumeLinear = _gain;
        if (!active)
        {
            _player.Stop();
        }
    }

    internal bool PlayingForDiagnostics => _player.Playing;
    internal bool MenuActiveForDiagnostics => _menuActive;
    internal float GainForDiagnostics => _gain;
}
