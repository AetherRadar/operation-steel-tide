using Godot;

namespace OperationSteelTide;

public partial class CombatHUD
{
    private InventoryModelPreview _lootOperatorPreview = null!;
    private Label _lootOperatorCaption = null!;

    public bool LootPaperDollReady
        => IsInstanceValid(_lootOperatorPreview) && _lootOperatorPreview.Visible;

    public bool LootWeaponRackReady
        => IsInstanceValid(_lootWeaponRack)
        && _lootWeaponRack.UiReady
        && _lootWeaponRack.IntentSignalsReady;

    public bool LootWeaponRackUsesPackedScene
        => IsInstanceValid(_lootWeaponRack)
        && _lootWeaponRack.SceneFilePath == LootWeaponRackView.ScenePath;

    public int LootVisibleWeaponSlotCount
        => IsInstanceValid(_lootWeaponRack) ? _lootWeaponRack.VisibleWeaponCount : 0;

    public WeaponPlatform? LootWeaponPlatformForSlot(PlayerWeaponSlot slot)
        => IsInstanceValid(_lootWeaponRack) ? _lootWeaponRack.PlatformForSlot(slot) : null;

    public string LootWeaponCaptionForSlot(PlayerWeaponSlot slot)
        => IsInstanceValid(_lootWeaponRack) ? _lootWeaponRack.CaptionForSlot(slot) : string.Empty;

    public void PressLootWeaponDetailsForDiagnostics(PlayerWeaponSlot slot)
    {
        if (IsInstanceValid(_lootWeaponRack))
        {
            _lootWeaponRack.PressDetailsForDiagnostics(slot);
        }
    }

    public bool LootBackpackPanelExpanded
        => !_shownSourceAvailable
        && IsInstanceValid(_backpackZone)
        && _backpackZone.Size.X >= 1100.0f
        && _backpackZone.Size.Y >= 700.0f
        && IsInstanceValid(_backpackList)
        && _backpackList.Columns >= 4;

    public bool LootBackpackContentFits
        => IsInstanceValid(_backpackScroll)
        && !_backpackScroll.GetVScrollBar().Visible;

    public bool LootSearchStorageExpanded
        => _shownSourceAvailable
        && IsInstanceValid(_lootSourceZone)
        && IsInstanceValid(_backpackZone)
        && IsInstanceValid(_backpackList)
        && _lootSourceZone.Size.X <= 900.0f
        && _backpackZone.Size.X >= 820.0f
        && _backpackZone.Size.Y >= 320.0f
        && _backpackList.Columns >= 4;

    public bool LootGroundDropReady
        => IsInstanceValid(_groundDropZone)
        && _groundDropZone.Visible
        && _groundDropZone.Enabled;

    public bool LootGroundDropInvisible
        => LootGroundDropReady
        && _groundDropZone.GetThemeStylebox("panel") is StyleBoxEmpty
        && _groundDropZone.GetChildCount() == 0;

    public bool LootBackpackSlotSeparated
    {
        get
        {
            if (!IsInstanceValid(_packSlot)
                || !IsInstanceValid(_helmetSlot)
                || !IsInstanceValid(_armorSlot)
                || !IsInstanceValid(_backpackZone))
            {
                return false;
            }
            var packRect = _packSlot.GetRect();
            var helmetRect = _helmetSlot.GetRect();
            var armorRect = _armorSlot.GetRect();
            var storageRect = _backpackZone.GetRect();
            var isSeparateFromGear = !packRect.Intersects(helmetRect)
                && !packRect.Intersects(armorRect)
                && Mathf.Abs(packRect.Position.Y - helmetRect.Position.Y) > 80.0f;
            var isSeparateFromStorage = !packRect.Intersects(storageRect)
                || storageRect.Position.X < packRect.Position.X;
            return isSeparateFromGear && isSeparateFromStorage;
        }
    }

    private void BuildLootOperatorDisplay(Control parent)
    {
        var frame = new Panel
        {
            Position = new Vector2(1390, 170),
            Size = new Vector2(210, 300),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        var frameStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.015f, 0.025f, 0.026f, 0.72f),
            BorderColor = new Color(0.22f, 0.78f, 0.67f, 0.72f)
        };
        frameStyle.SetBorderWidthAll(1);
        frameStyle.SetCornerRadiusAll(2);
        frame.AddThemeStyleboxOverride("panel", frameStyle);
        parent.AddChild(frame);

        _lootOperatorPreview = new InventoryModelPreview
        {
            Position = new Vector2(4, 4),
            Size = new Vector2(202, 292),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _lootOperatorPreview.Configure(InventoryPreviewKind.Operator);
        frame.AddChild(_lootOperatorPreview);

        var footer = new ColorRect
        {
            Position = new Vector2(1, 260),
            Size = new Vector2(208, 39),
            Color = new Color(0.008f, 0.016f, 0.017f, 0.88f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        frame.AddChild(footer);
        _lootOperatorCaption = Label("ACTIVE LOADOUT", 11, new Color(0.47f, 0.9f, 0.76f));
        _lootOperatorCaption.Position = new Vector2(10, 268);
        _lootOperatorCaption.Size = new Vector2(190, 22);
        _lootOperatorCaption.HorizontalAlignment = HorizontalAlignment.Center;
        _lootOperatorCaption.ClipText = true;
        _lootOperatorCaption.MouseFilter = Control.MouseFilterEnum.Ignore;
        frame.AddChild(_lootOperatorCaption);

        AddLootConnector(parent, new Vector2(1384, 220), new Vector2(6, 1), new Color(0.34f, 0.86f, 0.7f));
        AddLootConnector(parent, new Vector2(1384, 320), new Vector2(6, 1), new Color(0.34f, 0.86f, 0.7f));
        AddLootConnector(parent, new Vector2(1384, 420), new Vector2(6, 1), new Color(0.84f, 0.66f, 0.3f));
        AddLootConnector(parent, new Vector2(1600, 218), new Vector2(10, 1), new Color(0.84f, 0.66f, 0.3f));
        AddLootConnector(parent, new Vector2(1600, 316), new Vector2(10, 1), new Color(0.35f, 0.68f, 0.94f));
        AddLootConnector(parent, new Vector2(1600, 420), new Vector2(10, 1), new Color(0.62f, 0.55f, 0.86f));
    }

    private static void AddLootConnector(Control parent, Vector2 position, Vector2 size, Color color)
    {
        parent.AddChild(new ColorRect
        {
            Position = position,
            Size = size,
            Color = new Color(color, 0.75f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        });
    }
}
