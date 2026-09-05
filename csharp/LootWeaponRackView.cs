using System;
using Godot;

namespace OperationSteelTide;

/// <summary>
/// Presents primary, secondary, and sidearm snapshots supplied through <see cref="SetLoadout"/>.
/// The view emits user intent and never mutates player inventory state.
/// </summary>
[GlobalClass]
public partial class LootWeaponRackView : Control
{
    public const string ScenePath = "res://ui/LootWeaponRackView.tscn";

    [Signal]
    public delegate void WeaponDetailsRequestedEventHandler(int slot);

    private LootWeaponSlotView _primarySlot = null!;
    private LootWeaponSlotView _secondarySlot = null!;
    private LootWeaponSlotView _sidearmSlot = null!;
    private string _language = "en";
    private WeaponBuild? _primary;
    private WeaponBuild? _secondary;
    private WeaponBuild? _sidearm;
    private LootGrade _primaryGrade;
    private LootGrade _secondaryGrade;
    private LootGrade _sidearmGrade;
    private bool _configured;

    public event Action<string, LootDragOrigin, LootDropTarget>? Dropped;
    public event Action<PlayerWeaponSlot>? OpticDetachRequested;

    public bool UiReady
        => IsInstanceValid(_primarySlot)
        && IsInstanceValid(_secondarySlot)
        && IsInstanceValid(_sidearmSlot)
        && _primarySlot.UiReady
        && _secondarySlot.UiReady
        && _sidearmSlot.UiReady;

    public bool IntentSignalsReady
        => HasConnections(SignalName.WeaponDetailsRequested)
        && IsInstanceValid(_primarySlot)
        && IsInstanceValid(_secondarySlot)
        && IsInstanceValid(_sidearmSlot);

    public int VisibleWeaponCount
        => (_primary is null ? 0 : 1)
        + (_secondary is null ? 0 : 1)
        + (_sidearm is null ? 0 : 1);

    public bool GradeStylesConsistent
        => UiReady
        && _primarySlot.QualityColorMatchesGrade
        && _secondarySlot.QualityColorMatchesGrade
        && _sidearmSlot.QualityColorMatchesGrade;

    public bool EmptyCaptionsHaveNoGrade
        => UiReady
        && _primarySlot.EmptyCaptionHasNoGrade
        && _secondarySlot.EmptyCaptionHasNoGrade
        && _sidearmSlot.EmptyCaptionHasNoGrade;

    public override void _Ready()
    {
        _primarySlot = GetNode<LootWeaponSlotView>("%PrimarySlot");
        _secondarySlot = GetNode<LootWeaponSlotView>("%SecondarySlot");
        _sidearmSlot = GetNode<LootWeaponSlotView>("%SidearmSlot");

        BindSlot(_primarySlot, PlayerWeaponSlot.Primary);
        BindSlot(_secondarySlot, PlayerWeaponSlot.Secondary);
        BindSlot(_sidearmSlot, PlayerWeaponSlot.Sidearm);
        ApplyPresentation();
    }

    /// <summary>Supplies a complete three-slot weapon snapshot for the loot paper doll.</summary>
    public void SetLoadout(
        string language,
        WeaponBuild? primary,
        LootGrade primaryGrade,
        WeaponBuild? secondary,
        LootGrade secondaryGrade,
        WeaponBuild? sidearm,
        LootGrade sidearmGrade)
    {
        _language = GameLocalization.IsChinese(language) ? "zh" : "en";
        _primary = primary?.Clone();
        _primaryGrade = primaryGrade;
        _secondary = secondary?.Clone();
        _secondaryGrade = secondaryGrade;
        _sidearm = sidearm?.Clone();
        _sidearmGrade = sidearmGrade;
        _configured = true;
        if (IsNodeReady())
        {
            ApplyPresentation();
        }
    }

    public WeaponPlatform? PlatformForSlot(PlayerWeaponSlot slot) => slot switch
    {
        PlayerWeaponSlot.Primary => _primary?.Platform,
        PlayerWeaponSlot.Secondary => _secondary?.Platform,
        PlayerWeaponSlot.Sidearm => _sidearm?.Platform,
        _ => null
    };

    public string CaptionForSlot(PlayerWeaponSlot slot) => slot switch
    {
        PlayerWeaponSlot.Primary => _primarySlot.CaptionText,
        PlayerWeaponSlot.Secondary => _secondarySlot.CaptionText,
        PlayerWeaponSlot.Sidearm => _sidearmSlot.CaptionText,
        _ => string.Empty
    };

    public bool CanDetachOpticForSlot(PlayerWeaponSlot slot) => slot switch
    {
        PlayerWeaponSlot.Primary => _primarySlot.CanDetachOptic,
        PlayerWeaponSlot.Secondary => _secondarySlot.CanDetachOptic,
        PlayerWeaponSlot.Sidearm => _sidearmSlot.CanDetachOptic,
        _ => false
    };

    public void PressDetailsForDiagnostics(PlayerWeaponSlot slot)
    {
        switch (slot)
        {
            case PlayerWeaponSlot.Primary:
                _primarySlot.PressDetailsForDiagnostics();
                break;
            case PlayerWeaponSlot.Secondary:
                _secondarySlot.PressDetailsForDiagnostics();
                break;
            case PlayerWeaponSlot.Sidearm:
                _sidearmSlot.PressDetailsForDiagnostics();
                break;
        }
    }

    public void PressDetachOpticForDiagnostics(PlayerWeaponSlot slot)
    {
        switch (slot)
        {
            case PlayerWeaponSlot.Primary:
                _primarySlot.PressDetachOpticForDiagnostics();
                break;
            case PlayerWeaponSlot.Secondary:
                _secondarySlot.PressDetachOpticForDiagnostics();
                break;
            case PlayerWeaponSlot.Sidearm:
                _sidearmSlot.PressDetachOpticForDiagnostics();
                break;
        }
    }

    private void BindSlot(LootWeaponSlotView slotView, PlayerWeaponSlot slot)
    {
        slotView.Target = slot switch
        {
            PlayerWeaponSlot.Secondary => LootDropTarget.SecondaryWeapon,
            PlayerWeaponSlot.Sidearm => LootDropTarget.SidearmWeapon,
            _ => LootDropTarget.PrimaryWeapon
        };
        slotView.Dropped += (itemId, origin, target) => Dropped?.Invoke(itemId, origin, target);
        slotView.DetailsRequested += () => EmitSignal(SignalName.WeaponDetailsRequested, (int)slot);
        slotView.OpticDetachRequested += () => OpticDetachRequested?.Invoke(slot);
    }

    public bool DropForDiagnostics(
        LootItem item,
        LootDragOrigin origin,
        PlayerWeaponSlot slot)
    {
        var slotView = slot switch
        {
            PlayerWeaponSlot.Primary => _primarySlot,
            PlayerWeaponSlot.Secondary => _secondarySlot,
            PlayerWeaponSlot.Sidearm => _sidearmSlot,
            _ => null
        };
        if (!IsInstanceValid(slotView))
        {
            return false;
        }
        var data = new Godot.Collections.Dictionary
        {
            ["item_id"] = item.Id,
            ["origin"] = (int)origin,
            ["kind"] = (int)item.Kind,
            ["slot"] = item.Kind == LootItemKind.Equipment && item.Equipment is not null
                ? (int)item.Equipment.Definition.Slot
                : -1
        };
        if (!slotView!._CanDropData(Vector2.Zero, data))
        {
            return false;
        }
        slotView._DropData(Vector2.Zero, data);
        return true;
    }

    private void ApplyPresentation()
    {
        if (!UiReady)
        {
            return;
        }

        _primarySlot.SetWeapon(
            _language,
            "primary_weapon",
            "PRIMARY WEAPON",
            _configured ? _primary : null,
            _primaryGrade);
        _secondarySlot.SetWeapon(
            _language,
            "secondary_weapon",
            "SECONDARY WEAPON",
            _configured ? _secondary : null,
            _secondaryGrade);
        _sidearmSlot.SetWeapon(
            _language,
            "sidearm_weapon",
            "SIDEARM",
            _configured ? _sidearm : null,
            _sidearmGrade);
    }
}
