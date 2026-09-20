using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

/// <summary>Playback calibration measured from the final Blender foot-contact tracks.</summary>
internal sealed class AuthoredLocomotionSpeeds
{
    private readonly Dictionary<string, float> _speeds;

    public AuthoredLocomotionSpeeds(OperatorVisualId visualId)
    {
        var slug = visualId switch
        {
            OperatorVisualId.Garrison => string.Empty,
            OperatorVisualId.Viper => "viper",
            OperatorVisualId.Heron => "heron",
            OperatorVisualId.Lynx => "lynx",
            OperatorVisualId.Magpie => "magpie",
            OperatorVisualId.Jackal => "jackal",
            _ => throw new InvalidOperationException($"Unknown operator visual {visualId}.")
        };
        var path = visualId == OperatorVisualId.Garrison
            ? "res://assets/models/enemy_operator/enemy_operator.locomotion.tres"
            : $"res://assets/models/hy3d_operators/{slug}.locomotion.tres";
        if (!ResourceLoader.Exists(path))
        {
            throw new InvalidOperationException($"Operator is missing authored stride calibration: {path}");
        }
        var calibration = GD.Load<Resource>(path)
            ?? throw new InvalidOperationException($"Operator stride calibration is empty: {path}");
        _speeds = new Dictionary<string, float>(StringComparer.Ordinal);
        foreach (var gait in new[] { "walk", "run", "sprint", "crouch_walk" })
        {
            if (!calibration.HasMeta(gait))
            {
                throw new InvalidOperationException($"Operator stride calibration is missing: {path} ({gait})");
            }
            var speed = calibration.GetMeta(gait).AsSingle();
            if (!float.IsFinite(speed) || speed <= 0.0f)
            {
                throw new InvalidOperationException($"Operator stride calibration is invalid: {path} ({gait})");
            }
            _speeds.Add(gait, speed);
        }
    }

    public float ForGait(string gait) => _speeds[gait];
}
