using Godot;

namespace OperationSteelTide;

/// <summary>
/// Presents actions for one backpack item. Inputs are supplied through <see cref="SetItem"/> or
/// <see cref="OpenNear"/>; outputs are user-intent signals and never mutate inventory state.
/// </summary>
[GlobalClass]
public partial class LootItemActionMenuView : PopupPanel
{
    public const string ScenePath = "res://ui/LootItemActionMenuView.tscn";

    [Signal]
    public delegate void EquipRequestedEventHandler(string itemId);

    [Signal]
    public delegate void DropRequestedEventHandler(string itemId);

    [Signal]
    public delegate void DismissRequestedEventHandler(string itemId);

    private static readonly Vector2I MenuSizeWithEquip = new(226, 128);
    private static readonly Vector2I MenuSizeWithoutEquip = new(226, 88);

    private Label _itemName = null!;
    private Button _closeButton = null!;
    private Button _equipButton = null!;
    private Button _dropButton = null!;
    private string _itemId = string.Empty;
    private string _displayName = string.Empty;
    private string _language = "en";
    private bool _canEquip;
    private bool _closingForAction;

    public bool UiReady
        => IsInstanceValid(_itemName)
        && IsInstanceValid(_closeButton)
        && IsInstanceValid(_equipButton)
        && IsInstanceValid(_dropButton);

    public string ItemId => _itemId;
    public bool CanEquip => _canEquip;
    public string EquipText => IsInstanceValid(_equipButton) ? _equipButton.Text : string.Empty;
    public string DropText => IsInstanceValid(_dropButton) ? _dropButton.Text : string.Empty;

    public override void _Ready()
    {
        _itemName = GetNode<Label>("%ItemName");
        _closeButton = GetNode<Button>("%CloseButton");
        _equipButton = GetNode<Button>("%EquipButton");
        _dropButton = GetNode<Button>("%DropButton");

        _closeButton.Pressed += RequestDismiss;
        _equipButton.Pressed += RequestEquip;
        _dropButton.Pressed += RequestDrop;
        PopupHide += OnPopupHidden;
        ApplyPresentation();
    }

    /// <summary>Supplies the item snapshot rendered by this view without opening it.</summary>
    public void SetItem(
        string itemId,
        string displayName,
        string language,
        bool canEquip)
    {
        _itemId = itemId ?? string.Empty;
        _displayName = displayName ?? string.Empty;
        _language = GameLocalization.IsChinese(language) ? "zh" : "en";
        _canEquip = canEquip;
        if (IsNodeReady())
        {
            ApplyPresentation();
        }
    }

    /// <summary>Opens the menu beside a card, clamped to its viewport.</summary>
    public void OpenNear(
        Control anchor,
        string itemId,
        string displayName,
        string language,
        bool canEquip)
    {
        SetItem(itemId, displayName, language, canEquip);
        var visibleRect = anchor.GetViewport().GetVisibleRect();
        OpenNear(anchor.GetGlobalRect(), visibleRect);
    }

    /// <summary>Opens the menu beside an explicit UI rectangle.</summary>
    public void OpenNear(Rect2 anchorRect, Rect2 viewportRect)
    {
        var menuSize = _canEquip ? MenuSizeWithEquip : MenuSizeWithoutEquip;
        var gap = 7.0f;
        var x = anchorRect.End.X + gap;
        if (x + menuSize.X > viewportRect.End.X)
        {
            x = anchorRect.Position.X - menuSize.X - gap;
        }
        var maxX = Mathf.Max(viewportRect.Position.X, viewportRect.End.X - menuSize.X);
        var maxY = Mathf.Max(viewportRect.Position.Y, viewportRect.End.Y - menuSize.Y);
        x = Mathf.Clamp(x, viewportRect.Position.X, maxX);
        var y = Mathf.Clamp(anchorRect.Position.Y, viewportRect.Position.Y, maxY);

        Popup(new Rect2I(
            Mathf.RoundToInt(x),
            Mathf.RoundToInt(y),
            menuSize.X,
            menuSize.Y));
    }

    public void PressEquipForDiagnostics() => _equipButton.EmitSignal(BaseButton.SignalName.Pressed);

    public void PressDropForDiagnostics() => _dropButton.EmitSignal(BaseButton.SignalName.Pressed);

    public void PressDismissForDiagnostics() => _closeButton.EmitSignal(BaseButton.SignalName.Pressed);

    private void ApplyPresentation()
    {
        if (!UiReady)
        {
            return;
        }

        _itemName.Text = _displayName;
        _itemName.TooltipText = _displayName;
        _equipButton.Visible = _canEquip;
        _equipButton.Disabled = !_canEquip || string.IsNullOrEmpty(_itemId);
        _dropButton.Disabled = string.IsNullOrEmpty(_itemId);
        _equipButton.Text = Text("equip", "EQUIP");
        _dropButton.Text = Text("drop_to_ground", "DROP TO GROUND");
    }

    private void RequestEquip()
    {
        if (!_canEquip || string.IsNullOrEmpty(_itemId))
        {
            return;
        }
        CompleteAction(SignalName.EquipRequested);
    }

    private void RequestDrop()
    {
        if (string.IsNullOrEmpty(_itemId))
        {
            return;
        }
        CompleteAction(SignalName.DropRequested);
    }

    private void CompleteAction(StringName signal)
    {
        var requestedItemId = _itemId;
        _closingForAction = true;
        Hide();
        EmitSignal(signal, requestedItemId);
    }

    private void RequestDismiss()
    {
        if (Visible)
        {
            Hide();
            return;
        }
        EmitDismissIntent();
    }

    private void OnPopupHidden()
    {
        if (_closingForAction)
        {
            _closingForAction = false;
            return;
        }
        EmitDismissIntent();
    }

    private void EmitDismissIntent()
    {
        EmitSignal(SignalName.DismissRequested, _itemId);
    }

    private string Text(string key, string english)
        => GameLocalization.Get(key, _language, english);
}
