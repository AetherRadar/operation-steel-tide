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
public partial class LootDragCard : PanelContainer
{
    public string ItemId { get; private set; } = string.Empty;
    public LootDragOrigin Origin { get; private set; }
    public LootItemKind ItemKind { get; private set; }
    public EquipmentSlot? ItemSlot { get; private set; }
    public string DragTitle { get; private set; } = string.Empty;

    public event Action<string, LootDragOrigin>? DoubleActivated;

    public void Configure(
        string itemId,
        LootDragOrigin origin,
        LootItemKind itemKind,
        EquipmentSlot? itemSlot,
        string title,
        string detail,
        Color accent)
    {
        ItemId = itemId;
        Origin = origin;
        ItemKind = itemKind;
        ItemSlot = itemSlot;
        DragTitle = title;
        CustomMinimumSize = new Vector2(438, 82);
        MouseFilter = MouseFilterEnum.Pass;
        AddThemeStyleboxOverride("panel", Style(new Color(0.055f, 0.067f, 0.067f, 0.96f), accent));

        var row = new HBoxContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        AddChild(row);
        var glyph = new ColorRect
        {
            CustomMinimumSize = new Vector2(9, 58),
            Color = accent,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        row.AddChild(glyph);
        var text = new VBoxContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
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
        style.ContentMarginLeft = 12;
        style.ContentMarginRight = 12;
        style.ContentMarginTop = 7;
        style.ContentMarginBottom = 7;
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
