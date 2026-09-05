using System;
using Godot;

namespace OperationSteelTide;

public static partial class SoundLab
{
    private const string OpeningCombatThemePath = "res://assets/audio/music/steel_tide_opening_combat.wav";
    private static AudioStreamWav? _openingCombatTheme;

    public static AudioStreamWav OpeningCombatTheme()
    {
        if (_openingCombatTheme is not null)
        {
            return _openingCombatTheme;
        }

        var stream = GD.Load<AudioStreamWav>(OpeningCombatThemePath)
            ?? throw new InvalidOperationException($"Unable to load {OpeningCombatThemePath}");
        stream.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
        stream.LoopBegin = 0;
        stream.LoopEnd = Mathf.RoundToInt((float)stream.GetLength() * stream.MixRate);
        _openingCombatTheme = stream;
        return stream;
    }
}
