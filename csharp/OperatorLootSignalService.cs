using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OperationSteelTide;

internal readonly record struct OperatorLootScanResult(
    int RevealedCount,
    int TotalValue,
    LootGrade BestGrade);

/// <summary>Creates short-lived world signals for the richest searchable loot around an operator.</summary>
internal sealed class OperatorLootSignalService
{
    private const float SignalLifetimeSeconds = 10.0f;
    private readonly Node3D _worldRoot;
    private readonly Func<string> _language;

    public OperatorLootSignalService(Node3D worldRoot, Func<string> language)
    {
        _worldRoot = worldRoot;
        _language = language;
    }

    public OperatorLootScanResult Reveal(
        IEnumerable<ILootSource> sources,
        Vector3 origin,
        float range = 38.0f,
        int maximumSignals = 8)
    {
        var selected = sources
            .Where(source => source.IsSearchable
                && GodotObject.IsInstanceValid(source.LootNode)
                && source.Loot.Count > 0
                && source.LootNode.GlobalPosition.DistanceTo(origin) <= range)
            .Select(source => new
            {
                Source = source,
                Value = LootItem.TotalValue(source.Loot),
                Grade = source.Loot.Max(item => item.Grade),
                Distance = source.LootNode.GlobalPosition.DistanceSquaredTo(origin)
            })
            .OrderByDescending(entry => entry.Value)
            .ThenBy(entry => entry.Distance)
            .Take(maximumSignals)
            .ToArray();

        var totalValue = 0;
        var bestGrade = LootGrade.Common;
        foreach (var entry in selected)
        {
            totalValue += entry.Value;
            if (entry.Grade > bestGrade)
            {
                bestGrade = entry.Grade;
            }
            AddSignal(entry.Source, entry.Value, entry.Grade);
        }
        return new OperatorLootScanResult(selected.Length, totalValue, bestGrade);
    }

    private void AddSignal(ILootSource source, int value, LootGrade grade)
    {
        var color = LootGrades.GlowColor(grade);
        var signal = new Node3D { Name = "FortuneFinderSignal", Scale = Vector3.One * 0.82f };
        _worldRoot.AddChild(signal);
        signal.GlobalPosition = source.LootNode.GlobalPosition + Vector3.Up * 1.45f;

        var material = new StandardMaterial3D
        {
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = new Color(color.R, color.G, color.B, 0.9f),
            EmissionEnabled = true,
            Emission = color,
            EmissionEnergyMultiplier = 2.8f,
            NoDepthTest = true
        };
        signal.AddChild(new MeshInstance3D
        {
            Name = "AppraisedLootRing",
            Mesh = new TorusMesh
            {
                InnerRadius = 0.28f,
                OuterRadius = 0.36f,
                Rings = 24,
                RingSegments = 10
            },
            RotationDegrees = new Vector3(90.0f, 0.0f, 0.0f),
            MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        });
        signal.AddChild(new Label3D
        {
            Name = "AppraisedLootLabel",
            Position = Vector3.Up * 0.5f,
            Text = $"{LootGrades.DisplayName(grade, _language())}  //  {value}",
            FontSize = 16,
            OutlineSize = 7,
            Modulate = color,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true,
            VisibilityRangeEnd = 58.0f
        });

        var tween = signal.CreateTween();
        tween.SetLoops(4);
        tween.TweenProperty(signal, "scale", Vector3.One * 1.12f, 0.55f)
            .SetTrans(Tween.TransitionType.Sine);
        tween.TweenProperty(signal, "scale", Vector3.One * 0.82f, 0.55f)
            .SetTrans(Tween.TransitionType.Sine);
        var cleanup = _worldRoot.GetTree().CreateTimer(SignalLifetimeSeconds);
        cleanup.Timeout += signal.QueueFree;
    }
}
