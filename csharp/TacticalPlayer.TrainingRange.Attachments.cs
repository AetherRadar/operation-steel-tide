using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

/// <summary>
/// Live-fire range attachment controls.  Each slot has a direct, deterministic
/// hotkey so the player can change a part without opening the mission backpack:
/// Y optic, U barrel, I muzzle, O grip, P stock, and L magazine.
/// </summary>
public partial class TacticalPlayer
{
    /// <summary>Apply the six-slot gunsmith preset selected at the range station.</summary>
    public void ApplyTrainingRangeAttachmentPreset(IReadOnlyList<string?> ids)
    {
        if (Main?.IsTrainingRangeActive != true || ids is null || !IsFirearmQuickSlotSelected)
            return;
        var slots = new[] { AttachmentSlot.Optic, AttachmentSlot.Barrel, AttachmentSlot.Muzzle,
            AttachmentSlot.Grip, AttachmentSlot.Stock, AttachmentSlot.Magazine };
        for (var i = 0; i < slots.Length && i < ids.Count; i++)
            SetTrainingRangeAttachment(slots[i], ids[i], notify: false);
    }
    private static readonly string?[] TrainingRangeBareOptics =
        { null, "optic_micro", "optic_holo", "optic_scope" };
    private static readonly string?[] TrainingRangeSidearmOptics =
        { null, "optic_micro", "optic_holo" };
    private static readonly string?[] TrainingRangeFixedOptics =
        { "optic_scope", "optic_7x", "optic_sniper" };
    private static readonly string?[] TrainingRangeBarrels =
        { "barrel_cqb", "barrel_standard", "barrel_marksman" };
    private static readonly string?[] TrainingRangeMuzzles =
        { null, "muzzle_brake", "muzzle_suppressor" };
    private static readonly string?[] TrainingRangeGrips =
        { null, "grip_angled", "grip_vertical" };
    private static readonly string?[] TrainingRangeStocks =
        { null, "stock_light", "stock_precision" };
    private static readonly string?[] TrainingRangeMagazines =
        { "mag_standard", "mag_extended" };

    private static readonly (StringName Action, Key Key)[] TrainingRangeAttachmentActions =
    {
        (GameInputActions.WeaponAttachmentCycle, Key.Y),
        (GameInputActions.WeaponAttachmentCycleBarrel, Key.U),
        (GameInputActions.WeaponAttachmentCycleMuzzle, Key.I),
        (GameInputActions.WeaponAttachmentCycleGrip, Key.O),
        (GameInputActions.WeaponAttachmentCycleStock, Key.P),
        (GameInputActions.WeaponAttachmentCycleMagazine, Key.L)
    };

    /// <summary>Current optic id, or an empty string when iron sights are active.</summary>
    public string TrainingRangeCurrentOpticId
        => TrainingRangeCurrentAttachmentId(AttachmentSlot.Optic);

    /// <summary>
    /// Returns the currently installed id for a slot.  Empty means that the
    /// slot is intentionally bare (for example, AK iron sights).
    /// </summary>
    public string TrainingRangeCurrentAttachmentId(AttachmentSlot slot)
        => EquippedWeapon.Attachments.TryGetValue(slot, out var id) ? id : string.Empty;

    public string?[] TrainingRangeCurrentAttachmentIds
    {
        get
        {
            var slots = new[] { AttachmentSlot.Optic, AttachmentSlot.Barrel, AttachmentSlot.Muzzle,
                AttachmentSlot.Grip, AttachmentSlot.Stock, AttachmentSlot.Magazine };
            var values = new string?[slots.Length];
            for (var i = 0; i < slots.Length; i++)
                values[i] = TrainingRangeCurrentAttachmentId(slots[i]) is { Length: > 0 } id ? id : null;
            return values;
        }
    }

    /// <summary>
    /// Candidate ids shown by the range's attachment controls.  A null entry is
    /// the explicit bare/removed state and is not emitted by the string API.
    /// </summary>
    public IReadOnlyList<string?> TrainingRangeAttachmentOptions(AttachmentSlot slot)
        => AttachmentOptionsFor(EquippedWeapon.Platform, slot);

    /// <summary>
    /// Compact control legend used by the range HUD and by station panels that
    /// want to render their own prompt.  Keep this independent from localized
    /// resource keys so diagnostics and custom HUDs can consume it as well.
    /// </summary>
    public string TrainingRangeAttachmentControlHint
    {
        get
        {
            var chinese = Hud is not null
                && GameLocalization.IsChinese(Hud.CurrentLanguage);
            return chinese
                ? "\u914d\u4ef6  //  Y\u7784\u5177  U\u67aa\u7ba1  I\u67aa\u53e3  O\u63e1\u628a  P\u67aa\u6258  L\u5f39\u5323"
                : "ATTACHMENTS  //  Y OPTIC  U BARREL  I MUZZLE  O GRIP  P STOCK  L MAG  -/+ RESET TIME";
        }
    }

    /// <summary>Shows the slot legend once when a fresh range run is deployed.</summary>
    public void ShowTrainingRangeAttachmentControls()
    {
        if (Main?.IsTrainingRangeActive != true || Hud is null)
        {
            return;
        }

        Hud.ShowRadioMessage(
            TrainingRangeAttachmentControlHint,
            new Color(0.42f, 0.9f, 0.72f));
    }

    /// <summary>
    /// Ensures all range attachment actions exist when a diagnostic scene
    /// supplies a custom InputMap rather than the project's normal map.
    /// </summary>
    private static void EnsureTrainingRangeAttachmentInput()
    {
        foreach (var action in TrainingRangeAttachmentActions)
        {
            if (!InputMap.HasAction(action.Action))
            {
                InputMap.AddAction(action.Action);
            }

            var events = InputMap.ActionGetEvents(action.Action);
            if (events.Count == 0)
            {
                InputMap.ActionAddEvent(
                    action.Action,
                    new InputEventKey { PhysicalKeycode = action.Key });
            }
        }
    }

    /// <summary>
    /// Called from the normal player loop; only the active range consumes these
    /// six slot keys.  One key is accepted per frame to avoid accidental double
    /// swaps if a keyboard reports multiple just-pressed events together.
    /// </summary>
    private void HandleTrainingRangeAttachmentCycleInput()
    {
        if (Main?.IsTrainingRangeActive != true
            || IsDead
            || UiLocked
            || _isReloading
            || _isPlating
            || _knifeEquipped
            || !IsFirearmQuickSlotSelected
            || RoleActionBlocksWeapon
            || MedicalActionBlocksWeapon
            )
        {
            return;
        }

        if (Input.IsActionJustPressed(GameInputActions.WeaponAttachmentCycle))
        {
            CycleTrainingRangeAttachment(AttachmentSlot.Optic);
        }
        else if (Input.IsActionJustPressed(GameInputActions.WeaponAttachmentCycleBarrel))
        {
            CycleTrainingRangeAttachment(AttachmentSlot.Barrel);
        }
        else if (Input.IsActionJustPressed(GameInputActions.WeaponAttachmentCycleMuzzle))
        {
            CycleTrainingRangeAttachment(AttachmentSlot.Muzzle);
        }
        else if (Input.IsActionJustPressed(GameInputActions.WeaponAttachmentCycleGrip))
        {
            CycleTrainingRangeAttachment(AttachmentSlot.Grip);
        }
        else if (Input.IsActionJustPressed(GameInputActions.WeaponAttachmentCycleStock))
        {
            CycleTrainingRangeAttachment(AttachmentSlot.Stock);
        }
        else if (Input.IsActionJustPressed(GameInputActions.WeaponAttachmentCycleMagazine))
        {
            CycleTrainingRangeAttachment(AttachmentSlot.Magazine);
        }
    }

    /// <summary>
    /// Cycles one attachment slot through the range's compatible, readable
    /// options.  The default Y binding calls this for the optic slot; the other
    /// five direct bindings use the same public API.
    /// </summary>
    public bool CycleTrainingRangeAttachment(AttachmentSlot slot, bool reverse = false)
    {
        if (Main?.IsTrainingRangeActive != true
            || _knifeEquipped
            || !IsFirearmQuickSlotSelected)
        {
            return false;
        }

        var options = AttachmentOptionsFor(EquippedWeapon.Platform, slot);
        if (options.Length == 0)
        {
            return false;
        }

        var current = EquippedWeapon.Attachments.TryGetValue(slot, out var currentId)
            ? currentId
            : null;
        var currentIndex = IndexOfOption(options, current);
        var offset = reverse ? -1 : 1;
        var nextIndex = currentIndex < 0
            ? (reverse ? options.Length - 1 : 0)
            : (currentIndex + offset + options.Length) % options.Length;
        return SetTrainingRangeAttachment(slot, options[nextIndex]);
    }

    /// <summary>
    /// Installs or removes an attachment on the active range weapon and updates
    /// its authored first-person presentation immediately.
    /// </summary>
    public bool SetTrainingRangeAttachment(
        AttachmentSlot slot,
        string? attachmentId,
        bool notify = true)
    {
        if (Main?.IsTrainingRangeActive != true
            || _knifeEquipped
            || !IsFirearmQuickSlotSelected)
        {
            return false;
        }

        var platform = EquippedWeapon.Platform;
        var currentId = EquippedWeapon.Attachments.TryGetValue(slot, out var installed)
            ? installed
            : null;
        if (string.Equals(currentId, attachmentId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (attachmentId is not null)
        {
            AttachmentDefinition attachment;
            try
            {
                attachment = WeaponCatalog.Attachment(attachmentId);
            }
            catch (KeyNotFoundException)
            {
                return false;
            }

            if (attachment.Slot != slot
                || !WeaponCatalog.CanEquipAttachment(platform, attachment.Id))
            {
                return false;
            }
        }
        else if (slot == AttachmentSlot.Optic
            && WeaponCatalog.HasFixedIntegratedScope(platform))
        {
            // Precision weapons own their glass in the authored receiver and
            // therefore cannot be switched to a bare rail.
            return false;
        }

        var preservedAmmo = Ammo;
        CancelReload();
        if (attachmentId is null)
        {
            EquippedWeapon.Attachments.Remove(slot);
            _equippedAttachmentGrades.Remove(slot);
        }
        else
        {
            EquippedWeapon.Attachments[slot] = attachmentId;
            _equippedAttachmentGrades[slot] = EquippedWeaponGrade;
        }

        ApplyWeaponBuildVisuals();
        Ammo = Mathf.Clamp(preservedAmmo, 0, EquippedWeapon.Stats().MagazineSize);
        SetAmmoReserve(CurrentAmmoCaliber, _trainingRangeAmmoGrade, 9999);
        StoreActiveFirearmState();
        PushHudStats();
        if (notify)
        {
            ShowTrainingRangeAttachmentStatus(slot, attachmentId);
        }
        return true;
    }

    private static string?[] AttachmentOptionsFor(
        WeaponPlatform platform,
        AttachmentSlot slot)
        => slot switch
        {
            AttachmentSlot.Optic => WeaponCatalog.HasFixedIntegratedScope(platform)
                ? TrainingRangeFixedOptics
                : WeaponCatalog.IsSidearm(platform)
                    ? TrainingRangeSidearmOptics
                    : TrainingRangeBareOptics,
            AttachmentSlot.Barrel => WeaponCatalog.IsSidearm(platform)
                ? new string?[] { "barrel_standard" }
                : TrainingRangeBarrels,
            AttachmentSlot.Muzzle => TrainingRangeMuzzles,
            AttachmentSlot.Grip => WeaponCatalog.IsSidearm(platform)
                ? new string?[] { null }
                : TrainingRangeGrips,
            AttachmentSlot.Stock => WeaponCatalog.IsSidearm(platform)
                ? new string?[] { null }
                : TrainingRangeStocks,
            AttachmentSlot.Magazine => TrainingRangeMagazines,
            _ => Array.Empty<string?>()
        };

    private static int IndexOfOption(IReadOnlyList<string?> options, string? value)
    {
        for (var index = 0; index < options.Count; index++)
        {
            if (string.Equals(options[index], value, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }
        return -1;
    }

    private void ShowTrainingRangeAttachmentStatus(
        AttachmentSlot slot,
        string? attachmentId)
    {
        if (Hud is null)
        {
            return;
        }

        var chinese = GameLocalization.IsChinese(Hud.CurrentLanguage);
        var slotName = chinese
            ? WeaponCatalog.SlotChinese(slot)
            : slot.ToString().ToUpperInvariant();
        var partName = attachmentId is null
            ? slot == AttachmentSlot.Optic
                ? chinese ? "\u65e0" : "IRON SIGHTS"
                : chinese ? "\u65e0" : "NONE"
            : WeaponCatalog.Attachment(attachmentId) is { } part
                ? chinese ? part.ChineseName : part.Name
                : attachmentId;
        var key = AttachmentCycleKey(slot);
        var message = chinese
            ? $"\u914d\u4ef6  //  {slotName}  //  {partName}  //  {key} \u5207\u6362"
            : $"ATTACHMENT  //  {slotName}  //  {partName}  //  {key} CYCLE";
        Hud.ShowRadioMessage(message, new Color(0.42f, 0.9f, 0.72f));
    }

    private static string AttachmentCycleKey(AttachmentSlot slot) => slot switch
    {
        AttachmentSlot.Optic => "Y",
        AttachmentSlot.Barrel => "U",
        AttachmentSlot.Muzzle => "I",
        AttachmentSlot.Grip => "O",
        AttachmentSlot.Stock => "P",
        AttachmentSlot.Magazine => "L",
        _ => "Y"
    };
}
