using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public partial class MedicalWheelControl : Control
{
    public event Action<MedicalItemKind>? Confirmed;

    private static readonly MedicalItemKind[] ItemOrder =
    {
        MedicalItemKind.Bandage,
        MedicalItemKind.FieldMedkit,
        MedicalItemKind.Adrenaline
    };

    private readonly Dictionary<MedicalItemKind, int> _counts = new();
    private readonly Label[] _labels = new Label[ItemOrder.Length];
    private int _highlightedIndex;
    private bool _pointerInRing;
    private string _language = "en";

    public MedicalItemKind HighlightedKind => ItemOrder[_highlightedIndex];
    public bool HighlightedAvailable => Count(HighlightedKind) > 0;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        FocusMode = FocusModeEnum.None;
        for (var index = 0; index < ItemOrder.Length; index++)
        {
            var label = new Label
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = MouseFilterEnum.Ignore,
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            label.AddThemeFontSizeOverride("font_size", 14);
            label.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.95f));
            label.AddThemeConstantOverride("shadow_offset_x", 2);
            label.AddThemeConstantOverride("shadow_offset_y", 2);
            AddChild(label);
            _labels[index] = label;
        }
        LayoutLabels();
        RefreshLabels();
        QueueRedraw();
    }

    public void Configure(string language, TacticalPlayer player)
    {
        _language = GameLocalization.IsChinese(language) ? "zh" : "en";
        foreach (var kind in ItemOrder)
        {
            _counts[kind] = player.MedicalCount(kind);
        }
        _highlightedIndex = FirstAvailableIndex();
        _pointerInRing = false;
        LayoutLabels();
        RefreshLabels();
        QueueRedraw();
    }

    public int Count(MedicalItemKind kind) => _counts.TryGetValue(kind, out var count) ? count : 0;

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseMotion motion)
        {
            UpdatePointer(motion.Position);
            AcceptEvent();
            return;
        }
        if (@event is InputEventMouseButton button
            && button.Pressed
            && button.ButtonIndex == MouseButton.Left)
        {
            UpdatePointer(button.Position);
            if (_pointerInRing && HighlightedAvailable)
            {
                Confirmed?.Invoke(HighlightedKind);
            }
            AcceptEvent();
        }
    }

    public override void _Draw()
    {
        var center = Size * 0.5f;
        var outerRadius = Mathf.Min(Size.X, Size.Y) * 0.44f;
        var innerRadius = outerRadius * 0.34f;
        const int arcSteps = 18;
        for (var index = 0; index < ItemOrder.Length; index++)
        {
            var definition = MedicalItems.Definition(ItemOrder[index]);
            var available = Count(ItemOrder[index]) > 0;
            var selected = index == _highlightedIndex;
            var color = available ? definition.Accent : new Color(0.22f, 0.24f, 0.24f);
            color = selected
                ? color.Lightened(0.12f) with { A = 0.96f }
                : color.Darkened(0.48f) with { A = available ? 0.88f : 0.72f };
            var centerAngle = SectorCenter(index);
            var start = centerAngle - Mathf.Pi / 3.0f + 0.025f;
            var end = centerAngle + Mathf.Pi / 3.0f - 0.025f;
            var points = new List<Vector2>(arcSteps * 2 + 2);
            for (var step = 0; step <= arcSteps; step++)
            {
                var angle = Mathf.Lerp(start, end, step / (float)arcSteps);
                points.Add(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * outerRadius);
            }
            for (var step = arcSteps; step >= 0; step--)
            {
                var angle = Mathf.Lerp(start, end, step / (float)arcSteps);
                points.Add(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * innerRadius);
            }
            var polygon = points.ToArray();
            DrawColoredPolygon(polygon, color);
            var outline = new Vector2[polygon.Length + 1];
            polygon.CopyTo(outline, 0);
            outline[^1] = polygon[0];
            DrawPolyline(outline, selected ? definition.Accent.Lightened(0.22f) : new Color(0.34f, 0.39f, 0.38f), selected ? 4.0f : 2.0f, true);
        }
        DrawCircle(center, innerRadius * 0.72f, new Color(0.012f, 0.018f, 0.02f, 0.98f));
        DrawArc(center, innerRadius * 0.78f, 0, Mathf.Pi * 2.0f, 64, new Color(0.38f, 0.9f, 0.72f, 0.9f), 3.0f, true);
    }

    internal void SelectForDiagnostics(MedicalItemKind kind)
    {
        var index = Array.IndexOf(ItemOrder, kind);
        if (index < 0)
        {
            return;
        }
        _highlightedIndex = index;
        _pointerInRing = true;
        RefreshLabels();
        QueueRedraw();
    }

    internal bool ConfirmForDiagnostics()
    {
        if (!HighlightedAvailable)
        {
            return false;
        }
        Confirmed?.Invoke(HighlightedKind);
        return true;
    }

    private void UpdatePointer(Vector2 point)
    {
        var offset = point - Size * 0.5f;
        var radius = offset.Length();
        var outerRadius = Mathf.Min(Size.X, Size.Y) * 0.44f;
        var innerRadius = outerRadius * 0.3f;
        _pointerInRing = radius >= innerRadius && radius <= outerRadius * 1.12f;
        if (!_pointerInRing)
        {
            QueueRedraw();
            return;
        }
        var angle = Mathf.Atan2(offset.Y, offset.X);
        var bestIndex = 0;
        var bestDistance = float.MaxValue;
        for (var index = 0; index < ItemOrder.Length; index++)
        {
            var difference = AngularDistance(angle, SectorCenter(index));
            if (difference < bestDistance)
            {
                bestDistance = difference;
                bestIndex = index;
            }
        }
        if (_highlightedIndex != bestIndex)
        {
            _highlightedIndex = bestIndex;
            RefreshLabels();
        }
        QueueRedraw();
    }

    private void LayoutLabels()
    {
        if (_labels[0] is null)
        {
            return;
        }
        var center = Size * 0.5f;
        var radius = Mathf.Min(Size.X, Size.Y) * 0.31f;
        for (var index = 0; index < _labels.Length; index++)
        {
            var angle = SectorCenter(index);
            var anchor = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            _labels[index].Position = anchor - new Vector2(92, 48);
            _labels[index].Size = new Vector2(184, 96);
        }
    }

    private void RefreshLabels()
    {
        if (_labels[0] is null)
        {
            return;
        }
        for (var index = 0; index < ItemOrder.Length; index++)
        {
            var kind = ItemOrder[index];
            var definition = MedicalItems.Definition(kind);
            var count = Count(kind);
            var selected = index == _highlightedIndex;
            _labels[index].Text = $"{definition.Glyph}  {MedicalItems.DisplayName(kind, _language)}\nx{count}\n{MedicalItems.EffectDescription(kind, _language)}";
            _labels[index].AddThemeColorOverride(
                "font_color",
                count <= 0
                    ? new Color(0.42f, 0.46f, 0.45f)
                    : selected ? Colors.White : definition.Accent.Lightened(0.18f));
        }
    }

    private int FirstAvailableIndex()
    {
        for (var index = 0; index < ItemOrder.Length; index++)
        {
            if (Count(ItemOrder[index]) > 0)
            {
                return index;
            }
        }
        return 0;
    }

    private static float SectorCenter(int index) => index switch
    {
        1 => Mathf.Pi / 6.0f,
        2 => Mathf.Pi * 5.0f / 6.0f,
        _ => -Mathf.Pi / 2.0f
    };

    private static float AngularDistance(float a, float b)
    {
        var difference = Mathf.PosMod(a - b + Mathf.Pi, Mathf.Pi * 2.0f) - Mathf.Pi;
        return Mathf.Abs(difference);
    }
}
