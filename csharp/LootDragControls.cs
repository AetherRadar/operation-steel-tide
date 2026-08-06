using System;
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
    PrimaryWeapon,
    Helmet,
    BodyArmor,
    BackpackGear
}

[GlobalClass]
public partial class WeaponPreviewControl : Control
{
    private WeaponBuild? _weapon;
    private Color _accent = new(0.32f, 0.9f, 0.7f);

    public void Configure(WeaponBuild weapon, Color accent)
    {
        _weapon = weapon.Clone();
        _accent = accent;
        QueueRedraw();
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Resized += QueueRedraw;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_weapon is null || Size.X < 60.0f || Size.Y < 24.0f)
        {
            return;
        }

        var receiver = _weapon.Platform switch
        {
            WeaponPlatform.AK74 => new Color(0.24f, 0.27f, 0.25f),
            WeaponPlatform.ScarL => new Color(0.42f, 0.38f, 0.29f),
            _ => new Color(0.2f, 0.24f, 0.23f)
        };
        var furniture = _weapon.Platform == WeaponPlatform.AK74
            ? new Color(0.31f, 0.21f, 0.13f)
            : receiver.Lightened(0.12f);
        var metal = new Color(0.48f, 0.55f, 0.53f);
        var shadow = new Color(0, 0, 0, 0.58f);
        var width = Size.X - 18.0f;
        var centerY = Size.Y * 0.52f;
        var stockScale = PartScale(AttachmentSlot.Stock);
        var barrelScale = PartScale(AttachmentSlot.Barrel);
        var magazineScale = PartScale(AttachmentSlot.Magazine);
        var opticScale = PartScale(AttachmentSlot.Optic);
        var gripScale = PartScale(AttachmentSlot.Grip);
        var stockEnd = 14.0f + width * 0.17f * stockScale;
        var receiverEnd = stockEnd + width * 0.26f;
        var handguardEnd = receiverEnd + width * 0.21f;
        var barrelEnd = Mathf.Min(Size.X - 16.0f, handguardEnd + width * 0.23f * barrelScale);

        DrawLine(new Vector2(12, centerY + 3), new Vector2(barrelEnd + 4, centerY + 3), shadow, 12, true);
        DrawLine(new Vector2(12, centerY), new Vector2(stockEnd + 5, centerY - 2), furniture, 11, true);
        DrawRect(new Rect2(9, centerY - 12, 8, 25), furniture.Darkened(0.18f));
        DrawRect(new Rect2(stockEnd, centerY - 12, receiverEnd - stockEnd, 23), receiver);
        DrawRect(new Rect2(receiverEnd - 2, centerY - 9, handguardEnd - receiverEnd + 4, 17), furniture);
        DrawLine(new Vector2(handguardEnd, centerY - 1), new Vector2(barrelEnd, centerY - 1), metal, 5, true);
        DrawRect(new Rect2(stockEnd + 5, centerY - 15, receiverEnd - stockEnd - 9, 3), _accent.Darkened(0.25f));
        DrawRect(new Rect2(receiverEnd + 2, centerY - 13, handguardEnd - receiverEnd - 2, 3), metal.Darkened(0.2f));

        var magazineX = stockEnd + (receiverEnd - stockEnd) * 0.64f;
        var magazineLength = Size.Y * 0.28f * magazineScale;
        DrawLine(
            new Vector2(magazineX, centerY + 6),
            new Vector2(magazineX + (_weapon.Platform == WeaponPlatform.AK74 ? 6 : 2), centerY + 6 + magazineLength),
            furniture.Darkened(0.08f),
            10,
            true);
        DrawLine(new Vector2(stockEnd + 11, centerY + 7), new Vector2(stockEnd + 7, centerY + Size.Y * 0.22f), furniture, 8, true);

        if (_weapon.Attachments.ContainsKey(AttachmentSlot.Grip))
        {
            var gripLength = Size.Y * 0.2f * gripScale;
            DrawLine(new Vector2(receiverEnd + 18, centerY + 5), new Vector2(receiverEnd + 16, centerY + 5 + gripLength), furniture, 7, true);
        }
        if (_weapon.Attachments.ContainsKey(AttachmentSlot.Optic))
        {
            var opticWidth = 15.0f * opticScale;
            var opticX = stockEnd + (receiverEnd - stockEnd) * 0.5f - opticWidth * 0.5f;
            DrawRect(new Rect2(opticX, centerY - 24, opticWidth, 9), metal.Darkened(0.12f));
            DrawRect(new Rect2(opticX + 3, centerY - 27, Mathf.Max(5, opticWidth - 6), 4), _accent.Darkened(0.18f));
        }
        if (_weapon.Attachments.ContainsKey(AttachmentSlot.Muzzle))
        {
            var muzzleScale = PartScale(AttachmentSlot.Muzzle);
            DrawRect(new Rect2(barrelEnd - 1, centerY - 6, Mathf.Min(20, 10 * muzzleScale), 10), metal.Darkened(0.25f));
        }

        DrawCircle(new Vector2(stockEnd + 12, centerY - 2), 2.2f, _accent);
        DrawCircle(new Vector2(receiverEnd - 8, centerY - 2), 1.6f, metal.Lightened(0.18f));
    }

    private float PartScale(AttachmentSlot slot)
    {
        return _weapon is not null && _weapon.Attachments.TryGetValue(slot, out var id)
            ? WeaponCatalog.Attachment(id).VisualScale
            : 1.0f;
    }
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
            case LootItemKind.Attachment:
                DrawRect(new Rect2(center.X - 16, center.Y + 7, 32, 5), muted);
                DrawRect(new Rect2(center.X - 10, center.Y - 8, 20, 15), _accent.Darkened(0.18f));
                DrawCircle(center, 5, new Color(0.025f, 0.035f, 0.035f));
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

    public event Action<string, LootDragOrigin>? DoubleActivated;
    public event Action<WeaponBuild>? DetailsRequested;

    public void Configure(
        string itemId,
        LootDragOrigin origin,
        LootItemKind itemKind,
        EquipmentSlot? itemSlot,
        string title,
        string detail,
        Color accent,
        WeaponBuild? weapon = null,
        string detailAction = "DETAILS")
    {
        ItemId = itemId;
        Origin = origin;
        ItemKind = itemKind;
        ItemSlot = itemSlot;
        DragTitle = title;
        CustomMinimumSize = new Vector2(250, weapon is null ? 88 : 126);
        MouseFilter = MouseFilterEnum.Pass;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddThemeStyleboxOverride("panel", Style(new Color(0.055f, 0.067f, 0.067f, 0.96f), accent));

        if (weapon is not null)
        {
            BuildWeaponCard(title, detail, accent, weapon, detailAction);
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
    }

    private void BuildWeaponCard(string title, string detail, Color accent, WeaponBuild weapon, string detailAction)
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
        var preview = new WeaponPreviewControl { CustomMinimumSize = new Vector2(220, 48), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        preview.Configure(weapon, accent);
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
