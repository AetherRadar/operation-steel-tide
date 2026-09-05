using System;
using Godot;

namespace OperationSteelTide;

/// <summary>
/// Compact live-fire attachment readout.  The range exposes six direct cycle
/// keys, so the current part needs to remain visible after the transient toast
/// fades.  This view is deliberately text-only and lives inside the existing
/// weapon footer; it does not add a second panel or any world geometry.
/// </summary>
public partial class CombatHUD
{
    private Label _trainingRangeAttachmentLabel = null!;
    private bool _trainingRangeAttachmentReadoutActive;
    private string _trainingRangeAttachmentSignature = string.Empty;

    private void BuildTrainingRangeAttachmentHud(Control weaponPanel)
    {
        _trainingRangeAttachmentLabel = Label(
            string.Empty,
            10,
            new Color(0.64f, 0.88f, 0.78f));
        _trainingRangeAttachmentLabel.Position = new Vector2(22, 83);
        _trainingRangeAttachmentLabel.Size = new Vector2(706, 17);
        _trainingRangeAttachmentLabel.ClipText = true;
        _trainingRangeAttachmentLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
        _trainingRangeAttachmentLabel.Visible = false;
        _trainingRangeAttachmentLabel.TooltipText =
            "Y/U/I/O/P/L CYCLE TRAINING RANGE ATTACHMENTS";
        weaponPanel.AddChild(_trainingRangeAttachmentLabel);
    }

    /// <summary>
    /// Updates the persistent attachment line shown while the dedicated range
    /// is active.  Passing <c>false</c> (or no firearm) hides it immediately,
    /// which keeps extraction and demolition HUDs unchanged.
    /// </summary>
    public void SetTrainingRangeAttachmentReadout(bool active, WeaponBuild? build)
    {
        if (!IsInstanceValid(_trainingRangeAttachmentLabel))
        {
            return;
        }

        var shouldShow = active && build is not null;
        if (!shouldShow)
        {
            if (_trainingRangeAttachmentReadoutActive)
            {
                _trainingRangeAttachmentReadoutActive = false;
                _trainingRangeAttachmentSignature = string.Empty;
                _trainingRangeAttachmentLabel.Visible = false;
            }
            return;
        }

        var nextSignature = TrainingRangeAttachmentSignature(build!);
        if (_trainingRangeAttachmentReadoutActive
            && string.Equals(
                _trainingRangeAttachmentSignature,
                nextSignature,
                StringComparison.Ordinal))
        {
            return;
        }

        _trainingRangeAttachmentSignature = nextSignature;
        _trainingRangeAttachmentLabel.Text = FormatTrainingRangeAttachments(build!);
        _trainingRangeAttachmentReadoutActive = true;
        _trainingRangeAttachmentLabel.Visible = true;
    }

    internal bool TrainingRangeAttachmentReadoutVisibleForDiagnostics
        => IsInstanceValid(_trainingRangeAttachmentLabel)
        && _trainingRangeAttachmentLabel.Visible;

    internal string TrainingRangeAttachmentReadoutTextForDiagnostics
        => IsInstanceValid(_trainingRangeAttachmentLabel)
            ? _trainingRangeAttachmentLabel.Text
            : string.Empty;

    private void RefreshTrainingRangeAttachmentReadoutLanguage()
    {
        if (!_trainingRangeAttachmentReadoutActive
            || !IsInstanceValid(_trainingRangeAttachmentLabel))
        {
            return;
        }

        // The signature includes the language, so this also refreshes the
        // localized slot/part names after SetLanguage without a per-frame churn.
        _trainingRangeAttachmentSignature = string.Empty;
    }

    private string FormatTrainingRangeAttachments(WeaponBuild build)
    {
        var chinese = GameLocalization.IsChinese(_language);
        var fields = new string[6];
        var slots = new[]
        {
            AttachmentSlot.Optic,
            AttachmentSlot.Barrel,
            AttachmentSlot.Muzzle,
            AttachmentSlot.Grip,
            AttachmentSlot.Stock,
            AttachmentSlot.Magazine
        };
        var controlKeys = new[] { "Y", "U", "I", "O", "P", "L" };
        for (var index = 0; index < slots.Length; index++)
        {
            var slot = slots[index];
            var slotName = chinese
                ? WeaponCatalog.SlotChinese(slot)
                : EnglishSlotName(slot);
            var partName = build.Attachments.TryGetValue(slot, out var partId)
                ? CompactAttachmentName(partId, chinese)
                : slot == AttachmentSlot.Optic
                    ? Text("iron_sights", "IRON")
                    : "-";
            fields[index] = $"{controlKeys[index]} {slotName}:{partName}";
        }

        var controls = string.Join("   ", fields);
        return chinese
            ? $"{controls}   R 换弹"
            : $"{controls}   R RELOAD";
    }

    private string TrainingRangeAttachmentSignature(WeaponBuild build)
    {
        var signature = $"{_language}|{build.Platform}";
        foreach (var slot in new[]
        {
            AttachmentSlot.Optic,
            AttachmentSlot.Barrel,
            AttachmentSlot.Muzzle,
            AttachmentSlot.Grip,
            AttachmentSlot.Stock,
            AttachmentSlot.Magazine
        })
        {
            var partId = build.Attachments.TryGetValue(slot, out var installed)
                ? installed
                : string.Empty;
            signature += $"|{slot}:{partId}";
        }
        return signature;
    }

    private static string EnglishSlotName(AttachmentSlot slot) => slot switch
    {
        AttachmentSlot.Optic => "OPTIC",
        AttachmentSlot.Barrel => "BARREL",
        AttachmentSlot.Muzzle => "MUZZLE",
        AttachmentSlot.Grip => "GRIP",
        AttachmentSlot.Stock => "STOCK",
        AttachmentSlot.Magazine => "MAG",
        _ => "PART"
    };

    private static string CompactAttachmentName(string id, bool chinese)
    {
        if (chinese)
        {
            try
            {
                var chineseName = WeaponCatalog.Attachment(id).ChineseName;
                // Keep the line readable at 1280x720 while retaining the
                // distinguishing portion of authored Chinese part names.
                return chineseName.Length <= 6 ? chineseName : chineseName[..6];
            }
            catch (Exception)
            {
                return id.ToUpperInvariant();
            }
        }

        return id switch
        {
            "optic_micro" => "MICRO",
            "optic_holo" => "HOLO",
            "optic_scope" => "4X",
            "optic_7x" => "7X",
            "optic_sniper" => "8X",
            "barrel_cqb" => "CQB",
            "barrel_standard" => "STD",
            "barrel_marksman" => "MRK",
            "muzzle_brake" => "BRK",
            "muzzle_suppressor" => "SUP",
            "grip_angled" => "ANG",
            "grip_vertical" => "VERT",
            "stock_light" => "LGT",
            "stock_precision" => "PRS",
            "mag_standard" => "STD",
            "mag_extended" => "EXT",
            _ => id.ToUpperInvariant()
        };
    }
}
