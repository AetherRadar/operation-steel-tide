using System;
using Godot;

namespace OperationSteelTide;

[GlobalClass]
public partial class QuickSlotBarView : Control
{
    [Signal]
    public delegate void SlotRequestedEventHandler(int slot);

    private readonly Button[] _buttons = new Button[5];
    private InventoryModelPreview _primaryPreview = null!;
    private InventoryModelPreview _secondaryPreview = null!;
    private InventoryModelPreview _knifePreview = null!;
    private Label _fragLabel = null!;
    private Label _utilityLabel = null!;
    private string _language = "en";
    private WeaponBuild? _primary;
    private WeaponBuild? _secondary;
    private string _knifeSkinId = KnifeSkinCatalog.DefaultId;
    private int _fragGrenades;
    private int _utilityItems;
    private int _activeSlot;
    private bool _hasPrimary;
    private bool _configured;
    private string _primarySignature = string.Empty;
    private string _secondarySignature = string.Empty;
    private string _knifeSignature = string.Empty;

    public bool UiReady
        => Array.TrueForAll(_buttons, IsInstanceValid)
        && IsInstanceValid(_primaryPreview)
        && IsInstanceValid(_secondaryPreview)
        && IsInstanceValid(_knifePreview)
        && IsInstanceValid(_fragLabel)
        && IsInstanceValid(_utilityLabel);

    public bool IntentSignalsConnected
        => HasConnections(SignalName.SlotRequested)
        && Array.TrueForAll(_buttons, button => button.HasConnections(BaseButton.SignalName.Pressed));

    public int ActiveSlot => _activeSlot;

    public int VisibleSlotCount
    {
        get
        {
            var count = 0;
            foreach (var button in _buttons)
            {
                if (IsInstanceValid(button) && button.Visible)
                {
                    count++;
                }
            }
            return count;
        }
    }

    public override void _Ready()
    {
        _buttons[0] = GetNode<Button>("%Primary");
        _buttons[1] = GetNode<Button>("%Secondary");
        _buttons[2] = GetNode<Button>("%Melee");
        _buttons[3] = GetNode<Button>("%Fragmentation");
        _buttons[4] = GetNode<Button>("%Utility");
        _primaryPreview = GetNode<InventoryModelPreview>("%PrimaryPreview");
        _secondaryPreview = GetNode<InventoryModelPreview>("%SecondaryPreview");
        _knifePreview = GetNode<InventoryModelPreview>("%KnifePreview");
        _fragLabel = GetNode<Label>("%FragmentationContent");
        _utilityLabel = GetNode<Label>("%UtilityContent");

        for (var slot = 0; slot < _buttons.Length; slot++)
        {
            var requestedSlot = slot;
            _buttons[slot].Pressed += () => EmitSignal(SignalName.SlotRequested, requestedSlot);
        }

        if (!_configured)
        {
            _primary = WeaponCatalog.StarterWeapon();
            _hasPrimary = true;
        }
        ApplyPresentation();
    }

    /// <summary>
    /// Supplies the current player-owned slots. The view only renders this snapshot and emits selection intent.
    /// </summary>
    public void SetLoadout(
        string language,
        WeaponBuild? primary,
        bool hasPrimary,
        WeaponBuild? secondary,
        string knifeSkinId,
        int fragGrenades,
        int utilityItems,
        int activeSlot)
    {
        _language = GameLocalization.IsChinese(language) ? "zh" : "en";
        _primary = primary;
        _hasPrimary = hasPrimary && primary is not null;
        _secondary = secondary;
        _knifeSkinId = knifeSkinId;
        _fragGrenades = Math.Max(0, fragGrenades);
        _utilityItems = Math.Max(0, utilityItems);
        _activeSlot = Math.Clamp(activeSlot, 0, _buttons.Length - 1);
        _configured = true;
        if (IsNodeReady())
        {
            ApplyPresentation();
        }
    }

    public bool IsSlotVisible(int slot)
        => slot >= 0
        && slot < _buttons.Length
        && IsInstanceValid(_buttons[slot])
        && _buttons[slot].Visible;

    public string SlotText(int slot)
    {
        if (slot == 3 && IsInstanceValid(_fragLabel))
        {
            return _fragLabel.Text;
        }
        if (slot == 4 && IsInstanceValid(_utilityLabel))
        {
            return _utilityLabel.Text;
        }
        return string.Empty;
    }

    public void PressSlotForDiagnostics(int slot)
    {
        if (slot >= 0 && slot < _buttons.Length && IsInstanceValid(_buttons[slot]))
        {
            _buttons[slot].EmitSignal(BaseButton.SignalName.Pressed);
        }
    }

    private void ApplyPresentation()
    {
        if (!UiReady)
        {
            return;
        }

        _buttons[0].Visible = _hasPrimary;
        _buttons[1].Visible = _secondary is not null;
        _buttons[2].Visible = true;
        _buttons[3].Visible = _fragGrenades > 0;
        _buttons[4].Visible = _utilityItems > 0;

        _fragLabel.Text = $"{Text("grenade", "FRAG")}  x{_fragGrenades}";
        _utilityLabel.Text = $"{Text("smoke_grenade", "SMOKE")}  x{_utilityItems}";
        _buttons[0].TooltipText = Text("select_primary", "SELECT PRIMARY WEAPON");
        _buttons[1].TooltipText = Text("select_secondary", "SELECT SIDEARM");
        _buttons[2].TooltipText = Text("select_knife", "SELECT TACTICAL KNIFE");
        _buttons[3].TooltipText = Text("select_frag_grenade", "SELECT FRAGMENTATION GRENADE");
        _buttons[4].TooltipText = Text("select_utility", "SELECT UTILITY ITEM");

        for (var slot = 0; slot < _buttons.Length; slot++)
        {
            _buttons[slot].SetPressedNoSignal(_buttons[slot].Visible && slot == _activeSlot);
        }

        ConfigurePreview(_primaryPreview, _primary, ref _primarySignature);
        ConfigurePreview(_secondaryPreview, _secondary, ref _secondarySignature);
        if (!string.Equals(_knifeSignature, _knifeSkinId, StringComparison.Ordinal))
        {
            _knifeSignature = _knifeSkinId;
            _knifePreview.Configure(InventoryPreviewKind.Knife, knifeSkinId: _knifeSkinId);
        }
    }

    private static void ConfigurePreview(
        InventoryModelPreview preview,
        WeaponBuild? weapon,
        ref string renderedSignature)
    {
        if (weapon is null)
        {
            renderedSignature = string.Empty;
            return;
        }
        var signature = weapon.Platform.ToString();
        foreach (var slot in Enum.GetValues<AttachmentSlot>())
        {
            if (weapon.Attachments.TryGetValue(slot, out var partId))
            {
                signature += $"|{slot}:{partId}";
            }
        }
        if (string.Equals(renderedSignature, signature, StringComparison.Ordinal))
        {
            return;
        }
        renderedSignature = signature;
        preview.Configure(InventoryPreviewKind.Rifle, weapon: weapon);
    }

    private string Text(string key, string english)
        => GameLocalization.Get(key, _language, english);
}
