using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public enum LootDragOrigin
{
    Source,
    Backpack
}

public enum LootDropTarget
{
    Source,
    Backpack,
    Ground,
    PrimaryWeapon,
    Helmet,
    BodyArmor,
    BackpackGear
}

[GlobalClass]
public partial class LootItemIconControl : Control
{
    private LootItemKind _kind;
    private EquipmentSlot? _slot;
    private Color _accent;

    public void Configure(LootItemKind kind, EquipmentSlot? slot, Color accent)
    {
        _kind = kind;
        _slot = slot;
        _accent = accent;
        QueueRedraw();
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var center = Size * 0.5f;
        var muted = _accent.Darkened(0.42f);
        switch (_kind)
        {
            case LootItemKind.Ammunition:
                for (var index = -1; index <= 1; index++)
                {
                    var x = center.X + index * 9.0f;
                    DrawLine(new Vector2(x, center.Y - 12), new Vector2(x, center.Y + 12), _accent, 5, true);
                    DrawCircle(new Vector2(x, center.Y - 13), 2.5f, _accent.Lightened(0.18f));
                }
                break;
            case LootItemKind.ArmorPlate:
                DrawRect(new Rect2(center.X - 14, center.Y - 15, 28, 30), muted);
                DrawRect(new Rect2(center.X - 10, center.Y - 11, 20, 22), _accent.Darkened(0.16f));
                DrawLine(new Vector2(center.X - 7, center.Y), new Vector2(center.X + 7, center.Y), _accent.Lightened(0.28f), 2, true);
                break;
            case LootItemKind.Medical:
                DrawRect(new Rect2(center.X - 15, center.Y - 12, 30, 24), muted);
                DrawRect(new Rect2(center.X - 3, center.Y - 9, 6, 18), _accent);
                DrawRect(new Rect2(center.X - 10, center.Y - 3, 20, 6), _accent);
                break;
            case LootItemKind.Valuable:
                var diamond = new[]
                {
                    new Vector2(center.X, center.Y - 16),
                    new Vector2(center.X + 16, center.Y),
                    new Vector2(center.X, center.Y + 16),
                    new Vector2(center.X - 16, center.Y)
                };
                DrawColoredPolygon(diamond, muted);
                DrawPolyline(new[] { diamond[0], diamond[1], diamond[2], diamond[3], diamond[0] }, _accent, 3.0f, true);
                DrawCircle(center, 4.0f, _accent.Lightened(0.24f));
                break;
            case LootItemKind.Attachment:
                DrawRect(new Rect2(center.X - 16, center.Y + 7, 32, 5), muted);
                DrawRect(new Rect2(center.X - 10, center.Y - 8, 20, 15), _accent.Darkened(0.18f));
                DrawCircle(center, 5, new Color(0.025f, 0.035f, 0.035f));
                break;
            case LootItemKind.KnifeSkin:
                DrawLine(new Vector2(center.X - 15, center.Y + 13), new Vector2(center.X + 12, center.Y - 14), _accent, 7, true);
                DrawLine(new Vector2(center.X - 18, center.Y + 16), new Vector2(center.X - 10, center.Y + 8), muted, 8, true);
                DrawLine(new Vector2(center.X - 10, center.Y + 8), new Vector2(center.X - 4, center.Y + 14), _accent.Lightened(0.25f), 3, true);
                break;
            case LootItemKind.Equipment when _slot == EquipmentSlot.Helmet:
                DrawCircle(new Vector2(center.X, center.Y - 2), 14, muted);
                DrawRect(new Rect2(center.X - 15, center.Y - 1, 30, 15), new Color(0.035f, 0.043f, 0.042f));
                DrawLine(new Vector2(center.X - 15, center.Y), new Vector2(center.X + 15, center.Y), _accent, 4, true);
                break;
            case LootItemKind.Equipment when _slot == EquipmentSlot.BodyArmor:
                DrawLine(new Vector2(center.X - 10, center.Y - 13), new Vector2(center.X - 15, center.Y + 14), muted, 10, true);
                DrawLine(new Vector2(center.X + 10, center.Y - 13), new Vector2(center.X + 15, center.Y + 14), muted, 10, true);
                DrawRect(new Rect2(center.X - 10, center.Y - 12, 20, 28), _accent.Darkened(0.28f));
                break;
            case LootItemKind.Equipment:
                DrawRect(new Rect2(center.X - 14, center.Y - 14, 28, 29), muted);
                DrawLine(new Vector2(center.X - 14, center.Y - 8), new Vector2(center.X - 21, center.Y + 13), _accent, 3, true);
                DrawLine(new Vector2(center.X + 14, center.Y - 8), new Vector2(center.X + 21, center.Y + 13), _accent, 3, true);
                DrawRect(new Rect2(center.X - 8, center.Y - 8, 16, 4), _accent);
                break;
        }
    }
}

[GlobalClass]
public partial class LootDragCard : PanelContainer
{
    public string ItemId { get; private set; } = string.Empty;
    public LootDragOrigin Origin { get; private set; }
    public LootItemKind ItemKind { get; private set; }
    public EquipmentSlot? ItemSlot { get; private set; }
    public string DragTitle { get; private set; } = string.Empty;
    public LootGrade ItemGrade { get; private set; }
    public int ComparisonCount { get; private set; }
    public bool HasUpgradeComparison { get; private set; }
    public bool HasDowngradeComparison { get; private set; }
    public bool QualityColorMatchesGrade
    {
        get
        {
            var expected = LootGrades.GlowColor(ItemGrade);
            return Mathf.IsEqualApprox(_qualityAccent.R, expected.R)
                && Mathf.IsEqualApprox(_qualityAccent.G, expected.G)
                && Mathf.IsEqualApprox(_qualityAccent.B, expected.B)
                && Mathf.IsEqualApprox(_qualityAccent.A, expected.A);
        }
    }

    public event Action<string, LootDragOrigin>? DoubleActivated;
    public event Action<WeaponBuild>? DetailsRequested;

    private Color _qualityAccent;

    public void Configure(
        string itemId,
        LootDragOrigin origin,
        LootItemKind itemKind,
        EquipmentSlot? itemSlot,
        string title,
        string detail,
        LootGrade grade,
        WeaponBuild? weapon = null,
        EquipmentItem? equipment = null,
        string detailAction = "DETAILS",
        bool compact = false,
        IReadOnlyList<LootStatComparison>? comparisons = null)
    {
        ItemId = itemId;
        Origin = origin;
        ItemKind = itemKind;
        ItemSlot = itemSlot;
        DragTitle = title;
        TooltipText = detail;
        ItemGrade = grade;
        _qualityAccent = LootGrades.GlowColor(grade);
        ComparisonCount = comparisons?.Count ?? 0;
        HasUpgradeComparison = false;
        HasDowngradeComparison = false;
        if (comparisons is not null)
        {
            foreach (var comparison in comparisons)
            {
                HasUpgradeComparison |= comparison.Tone == LootComparisonTone.Upgrade;
                HasDowngradeComparison |= comparison.Tone == LootComparisonTone.Downgrade;
            }
        }
        var accent = _qualityAccent;
        CustomMinimumSize = compact
            ? new Vector2(180, 68)
            : new Vector2(250, weapon is not null && ComparisonCount > 0
                ? 148
                : equipment is not null && ComparisonCount > 0
                    ? 122
                    : weapon is not null
                        ? 126
                        : equipment is not null ? 108 : 88);
        MouseFilter = MouseFilterEnum.Pass;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddThemeStyleboxOverride("panel", Style(new Color(0.055f, 0.067f, 0.067f, 0.96f), accent));

        if (compact)
        {
            BuildCompactCard(title, detail, accent, itemKind, itemSlot, weapon, detailAction, comparisons);
            return;
        }
        if (weapon is not null)
        {
            BuildWeaponCard(title, detail, accent, weapon, detailAction, comparisons);
            return;
        }
        if (equipment is not null)
        {
            BuildEquipmentCard(title, detail, accent, equipment, comparisons);
            return;
        }

        var row = new HBoxContainer
        {
            MouseFilter = Control.MouseFilterEnum.Pass,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        AddChild(row);
        var icon = new LootItemIconControl { CustomMinimumSize = new Vector2(48, 52) };
        icon.Configure(itemKind, itemSlot, accent);
        row.AddChild(icon);
        var text = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddChild(text);
        var name = new Label
        {
            Text = title,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        name.AddThemeFontSizeOverride("font_size", 15);
        name.AddThemeColorOverride("font_color", new Color(0.89f, 0.95f, 0.92f));
        text.AddChild(name);
        var detailLabel = new Label
        {
            Text = detail,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        detailLabel.AddThemeFontSizeOverride("font_size", 12);
        detailLabel.AddThemeColorOverride("font_color", new Color(0.57f, 0.68f, 0.65f));
        text.AddChild(detailLabel);
        AddComparisonGrid(text, comparisons, false);
    }

    private void BuildCompactCard(
        string title,
        string detail,
        Color accent,
        LootItemKind itemKind,
        EquipmentSlot? itemSlot,
        WeaponBuild? weapon,
        string detailAction,
        IReadOnlyList<LootStatComparison>? comparisons)
    {
        var row = new HBoxContainer
        {
            MouseFilter = MouseFilterEnum.Pass,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        row.AddThemeConstantOverride("separation", 5);
        AddChild(row);

        var icon = new LootItemIconControl { CustomMinimumSize = new Vector2(40, 44) };
        icon.Configure(itemKind, itemSlot, accent);
        row.AddChild(icon);

        var text = new VBoxContainer
        {
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        text.AddThemeConstantOverride("separation", 0);
        row.AddChild(text);

        var header = new HBoxContainer
        {
            MouseFilter = MouseFilterEnum.Pass,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        text.AddChild(header);
        var name = new Label
        {
            Text = title,
            ClipText = true,
            TooltipText = title,
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        name.AddThemeFontSizeOverride("font_size", 12);
        name.AddThemeColorOverride("font_color", new Color(0.89f, 0.95f, 0.92f));
        header.AddChild(name);
        if (weapon is not null)
        {
            var details = new Button
            {
                Text = "i",
                CustomMinimumSize = new Vector2(24, 20),
                FocusMode = FocusModeEnum.None,
                TooltipText = detailAction
            };
            details.AddThemeFontSizeOverride("font_size", 11);
            details.AddThemeColorOverride("font_color", accent);
            details.Pressed += () => DetailsRequested?.Invoke(weapon.Clone());
            header.AddChild(details);
        }

        if (comparisons is not null && comparisons.Count > 0)
        {
            AddComparisonGrid(text, comparisons, true);
        }
        else
        {
            var detailLabel = new Label
            {
                Text = detail,
                ClipText = true,
                TooltipText = detail,
                MouseFilter = MouseFilterEnum.Ignore,
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            detailLabel.AddThemeFontSizeOverride("font_size", 10);
            detailLabel.AddThemeColorOverride("font_color", new Color(0.57f, 0.68f, 0.65f));
            text.AddChild(detailLabel);
        }
    }

    private void BuildWeaponCard(
        string title,
        string detail,
        Color accent,
        WeaponBuild weapon,
        string detailAction,
        IReadOnlyList<LootStatComparison>? comparisons)
    {
        var box = new VBoxContainer { MouseFilter = MouseFilterEnum.Pass, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        box.AddThemeConstantOverride("separation", 3);
        AddChild(box);
        var header = new HBoxContainer { MouseFilter = MouseFilterEnum.Pass, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        box.AddChild(header);
        var name = new Label
        {
            Text = title,
            ClipText = true,
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        name.AddThemeFontSizeOverride("font_size", 14);
        name.AddThemeColorOverride("font_color", new Color(0.89f, 0.96f, 0.92f));
        header.AddChild(name);
        var details = new Button
        {
            Text = detailAction,
            CustomMinimumSize = new Vector2(62, 24),
            FocusMode = FocusModeEnum.None,
            TooltipText = detailAction
        };
        details.AddThemeFontSizeOverride("font_size", 11);
        details.AddThemeColorOverride("font_color", accent);
        details.Pressed += () => DetailsRequested?.Invoke(weapon.Clone());
        header.AddChild(details);
        var preview = new InventoryModelPreview { CustomMinimumSize = new Vector2(220, 48), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        preview.Configure(InventoryPreviewKind.Rifle, weapon: weapon);
        box.AddChild(preview);
        var detailLabel = new Label
        {
            Text = detail,
            ClipText = true,
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        detailLabel.AddThemeFontSizeOverride("font_size", 11);
        detailLabel.AddThemeColorOverride("font_color", new Color(0.57f, 0.68f, 0.65f));
        box.AddChild(detailLabel);
        AddComparisonGrid(box, comparisons, false);
    }

    private void BuildEquipmentCard(
        string title,
        string detail,
        Color accent,
        EquipmentItem equipment,
        IReadOnlyList<LootStatComparison>? comparisons)
    {
        var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Pass, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        AddChild(row);
        var preview = new InventoryModelPreview
        {
            CustomMinimumSize = new Vector2(86, 78),
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        preview.Configure(equipment.Definition.Slot switch
        {
            EquipmentSlot.Helmet => InventoryPreviewKind.Helmet,
            EquipmentSlot.BodyArmor => InventoryPreviewKind.BodyArmor,
            _ => InventoryPreviewKind.Backpack
        }, equipment);
        row.AddChild(preview);
        var text = new VBoxContainer
        {
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        row.AddChild(text);
        var name = new Label
        {
            Text = title,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        name.AddThemeFontSizeOverride("font_size", 14);
        name.AddThemeColorOverride("font_color", new Color(0.9f, 0.94f, 0.88f));
        text.AddChild(name);
        var separator = new ColorRect
        {
            CustomMinimumSize = new Vector2(100, 2),
            Color = new Color(accent, 0.55f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        text.AddChild(separator);
        var detailLabel = new Label
        {
            Text = detail,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        detailLabel.AddThemeFontSizeOverride("font_size", 11);
        detailLabel.AddThemeColorOverride("font_color", new Color(0.61f, 0.7f, 0.66f));
        text.AddChild(detailLabel);
        AddComparisonGrid(text, comparisons, false);
    }

    private static void AddComparisonGrid(
        Control parent,
        IReadOnlyList<LootStatComparison>? comparisons,
        bool compact)
    {
        if (comparisons is null || comparisons.Count == 0)
        {
            return;
        }
        var grid = new GridContainer
        {
            Columns = 2,
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        grid.AddThemeConstantOverride("h_separation", compact ? 3 : 7);
        grid.AddThemeConstantOverride("v_separation", 1);
        parent.AddChild(grid);
        var visibleCount = compact ? Math.Min(2, comparisons.Count) : comparisons.Count;
        for (var index = 0; index < visibleCount; index++)
        {
            var comparison = comparisons[index];
            var color = comparison.Tone switch
            {
                LootComparisonTone.Upgrade => new Color(0.27f, 0.94f, 0.5f),
                LootComparisonTone.Downgrade => new Color(1.0f, 0.36f, 0.28f),
                _ => new Color(0.55f, 0.64f, 0.61f)
            };
            var label = new Label
            {
                Text = comparison.Text,
                ClipText = true,
                TooltipText = comparison.Text,
                CustomMinimumSize = new Vector2(compact ? 65 : 104, compact ? 13 : 16),
                MouseFilter = MouseFilterEnum.Ignore,
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            label.AddThemeFontSizeOverride("font_size", compact ? 8 : 10);
            label.AddThemeColorOverride("font_color", color);
            grid.AddChild(label);
        }
    }

    public override Variant _GetDragData(Vector2 _atPosition)
    {
        if (string.IsNullOrEmpty(ItemId))
        {
            return default;
        }
        var data = new Godot.Collections.Dictionary
        {
            ["item_id"] = ItemId,
            ["origin"] = (int)Origin,
            ["kind"] = (int)ItemKind,
            ["slot"] = ItemSlot.HasValue ? (int)ItemSlot.Value : -1
        };
        var preview = new PanelContainer
        {
            CustomMinimumSize = new Vector2(280, 54),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        preview.AddThemeStyleboxOverride("panel", Style(new Color(0.055f, 0.08f, 0.075f, 0.96f), new Color(0.32f, 0.9f, 0.7f)));
        var previewLabel = new Label
        {
            Text = DragTitle,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        previewLabel.AddThemeFontSizeOverride("font_size", 14);
        preview.AddChild(previewLabel);
        SetDragPreview(preview);
        return data;
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouse && mouse.Pressed && mouse.ButtonIndex == MouseButton.Left && mouse.DoubleClick)
        {
            DoubleActivated?.Invoke(ItemId, Origin);
            AcceptEvent();
        }
    }

    private static StyleBoxFlat Style(Color background, Color accent)
    {
        var style = new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = new Color(accent, 0.8f)
        };
        style.SetBorderWidthAll(1);
        style.SetCornerRadiusAll(3);
        style.ContentMarginLeft = 8;
        style.ContentMarginRight = 8;
        style.ContentMarginTop = 6;
        style.ContentMarginBottom = 6;
        return style;
    }
}

[GlobalClass]
public partial class LootDropZone : PanelContainer
{
    public LootDropTarget Target { get; set; }
    public bool Enabled { get; set; } = true;
    public event Action<string, LootDragOrigin, LootDropTarget>? Dropped;

    public override bool _CanDropData(Vector2 _atPosition, Variant data)
    {
        return Enabled
            && TryRead(data, out _, out var origin, out var kind, out var slot)
            && Accepts(origin, kind, slot);
    }

    public override void _DropData(Vector2 _atPosition, Variant data)
    {
        if (!TryRead(data, out var itemId, out var origin, out var kind, out var slot)
            || !Accepts(origin, kind, slot))
        {
            return;
        }
        Dropped?.Invoke(itemId, origin, Target);
    }

    private bool Accepts(LootDragOrigin origin, LootItemKind kind, EquipmentSlot? slot)
    {
        return Target switch
        {
            LootDropTarget.Source => origin == LootDragOrigin.Backpack,
            LootDropTarget.Backpack => origin == LootDragOrigin.Source,
            LootDropTarget.Ground => origin == LootDragOrigin.Backpack,
            LootDropTarget.PrimaryWeapon => kind is LootItemKind.Weapon or LootItemKind.Attachment,
            LootDropTarget.Helmet => kind == LootItemKind.Equipment && slot == EquipmentSlot.Helmet,
            LootDropTarget.BodyArmor => kind == LootItemKind.Equipment && slot == EquipmentSlot.BodyArmor,
            LootDropTarget.BackpackGear => kind == LootItemKind.Equipment && slot == EquipmentSlot.Backpack,
            _ => false
        };
    }

    private static bool TryRead(
        Variant data,
        out string itemId,
        out LootDragOrigin origin,
        out LootItemKind kind,
        out EquipmentSlot? slot)
    {
        itemId = string.Empty;
        origin = LootDragOrigin.Source;
        kind = LootItemKind.Weapon;
        slot = null;
        if (data.VariantType != Variant.Type.Dictionary)
        {
            return false;
        }
        var dictionary = data.AsGodotDictionary();
        if (!dictionary.ContainsKey("item_id") || !dictionary.ContainsKey("origin") || !dictionary.ContainsKey("kind"))
        {
            return false;
        }
        itemId = dictionary["item_id"].AsString();
        origin = (LootDragOrigin)dictionary["origin"].AsInt32();
        kind = (LootItemKind)dictionary["kind"].AsInt32();
        if (dictionary.ContainsKey("slot") && dictionary["slot"].AsInt32() >= 0)
        {
            slot = (EquipmentSlot)dictionary["slot"].AsInt32();
        }
        return !string.IsNullOrEmpty(itemId);
    }

    public static StyleBoxFlat ZoneStyle(Color accent)
    {
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.035f, 0.047f, 0.047f, 0.9f),
            BorderColor = new Color(accent, 0.75f)
        };
        style.SetBorderWidthAll(1);
        style.SetCornerRadiusAll(3);
        style.ContentMarginLeft = 10;
        style.ContentMarginRight = 10;
        style.ContentMarginTop = 8;
        style.ContentMarginBottom = 8;
        return style;
    }
}
