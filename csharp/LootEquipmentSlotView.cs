using System;
using Godot;

namespace OperationSteelTide;

/// <summary>Equipment slot presentation; emits removal intent without changing inventory.</summary>
[GlobalClass]
public partial class LootEquipmentSlotView : LootDropZone
{
    public const string ScenePath = "res://ui/LootEquipmentSlotView.tscn";
    public Label Caption { get; private set; } = null!;
    public Label Description { get; private set; } = null!;
    private LootItemIconControl _icon = null!;
    private Button _remove = null!;
    private Label _itemName = null!;
    public event Action? RemoveRequested;

    public override void _Ready()
    {
        Caption = GetNode<Label>("Margin/Content/Header/Caption");
        Description = GetNode<Label>("Margin/Content/Body/Description");
        var iconArea = GetNode<Control>("Margin/Content/Body/IconArea");
        _icon = new LootItemIconControl { MouseFilter = MouseFilterEnum.Ignore };
        iconArea.AddChild(_icon);
        _icon.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _remove = GetNode<Button>("Margin/Content/Header/Remove");
        _itemName = GetNode<Label>("Margin/Content/ItemName");
        _remove.Pressed += () => RemoveRequested?.Invoke();
    }

    public void SetLanguage(string language)
        => _remove.TooltipText = GameLocalization.Get("equipment_unequip", language, "Unequip item");

    public void SetAccent(Color accent)
    {
        var style = ZoneStyle(accent);
        style.ContentMarginLeft = style.ContentMarginRight = 0;
        style.ContentMarginTop = style.ContentMarginBottom = 0;
        AddThemeStyleboxOverride("panel", style);
    }

    public void SetEquipment(EquipmentItem item, string language)
    {
        SetLanguage(language);
        _icon.Configure(LootItemKind.Equipment, item.Definition.Slot, Colors.White,
            item.Definition.Slot switch
            {
                EquipmentSlot.Helmet => "helmet",
                EquipmentSlot.BodyArmor => "heavy_armor",
                _ => "expedition_pack"
            });
        _itemName.Text = item.DisplayName(language);
        _itemName.TooltipText = item.DisplayName(language) + "\n" + item.Detail(language);
        var definition = item.Definition;
        _icon.Visible = definition.MaxDurability > 0;
        _remove.Disabled = definition.MaxDurability <= 0;
        Description.TooltipText = _itemName.TooltipText;
        if (_remove.Disabled)
        {
            Description.Text = "-";
            return;
        }
        var stat = definition.Slot == EquipmentSlot.Backpack
            ? GameLocalization.Get("equipment_capacity_short", language, "Capacity") + $" +{definition.CapacityBonus}"
            : GameLocalization.Get("equipment_protection_short", language, "Protection") + $" {definition.Protection * 100:0}%";
        Description.Text = stat + $"\n{item.Durability:0}/{definition.MaxDurability:0}";
    }

    internal bool ContentFits => Description.Text.Length > 0 && Description.Size.X >= 70 && Description.Size.Y >= 40
        && GetCombinedMinimumSize().Y <= Size.Y + 1
        && Description.GetLineCount() <= 2
        && Description.GetGlobalRect().Intersection(GetGlobalRect()).Size == Description.GetGlobalRect().Size;
    internal string LayoutDescription => $"text={Description.Text.Replace('\n', '|')} rect={Description.GetGlobalRect()} slot={GetGlobalRect()} lines={Description.GetLineCount()}";
}
