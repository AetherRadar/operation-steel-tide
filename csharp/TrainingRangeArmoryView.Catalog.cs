using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class TrainingRangeArmoryView
{
    private static AttachmentSlot FirstSupportedSlot(WeaponPlatform platform)
    {
        foreach (var slot in Slots)
        {
            if (Candidates(platform, slot).Count > 0)
            {
                return slot;
            }
        }
        return AttachmentSlot.Optic;
    }

    private static List<string> Candidates(WeaponPlatform platform, AttachmentSlot slot)
    {
        IEnumerable<string> ids;
        if (slot == AttachmentSlot.Optic && WeaponCatalog.HasFixedIntegratedScope(platform))
        {
            ids = new[] { "optic_scope", "optic_7x", "optic_sniper" };
        }
        else
        {
            ids = slot switch
            {
                AttachmentSlot.Optic => new[] { "", "optic_micro", "optic_holo", "optic_scope" },
                AttachmentSlot.Barrel => WeaponCatalog.IsSidearm(platform)
                    ? new[] { "barrel_standard" }
                    : new[] { "barrel_cqb", "barrel_standard", "barrel_marksman" },
                AttachmentSlot.Muzzle => new[] { "", "muzzle_brake", "muzzle_suppressor" },
                AttachmentSlot.Grip => WeaponCatalog.IsSidearm(platform)
                    ? new[] { "" }
                    : new[] { "", "grip_angled", "grip_vertical" },
                AttachmentSlot.Stock => WeaponCatalog.IsSidearm(platform)
                    ? new[] { "" }
                    : new[] { "", "stock_light", "stock_precision" },
                AttachmentSlot.Magazine => new[] { "mag_standard", "mag_extended" },
                _ => Array.Empty<string>()
            };
        }
        var result = new List<string>();
        foreach (var id in ids)
        {
            if (id.Length == 0 || (WeaponCatalog.TryAttachment(id, out _)
                && WeaponCatalog.CanEquipAttachment(platform, id)))
            {
                result.Add(id);
            }
        }
        return result;
    }

    private static bool IsCandidate(WeaponPlatform platform, AttachmentSlot slot, string id)
        => Candidates(platform, slot).Contains(id, StringComparer.OrdinalIgnoreCase);

    private string SlotName(AttachmentSlot slot)
        => _language == "zh" ? WeaponCatalog.SlotChinese(slot) : slot.ToString().ToUpperInvariant();

    private string AttachmentName(string id)
    {
        if (id.Length == 0)
        {
            return _selectedAttachmentSlot == AttachmentSlot.Optic
                ? Text("training_armory_none", "IRON SIGHTS / NONE")
                : Text("training_armory_none", "NONE / REMOVE");
        }
        var definition = WeaponCatalog.Attachment(id);
        return _language == "zh" ? definition.ChineseName : definition.Name;
    }

    private static string FormatDelta(float delta, string suffix = "")
        => MathF.Abs(delta) < 0.005f ? "—" : $"{delta:+0.##;-0.##;0}{suffix}";

    private static string FormatDelta(int delta)
        => delta == 0 ? "—" : $"{delta:+0;-0;0}";

    private static bool IsImprovement(int index, float delta)
        => index switch
        {
            2 or 6 => delta < -0.005f,
            4 => delta > 0.005f,
            _ => delta > 0.005f
        };

    private static bool IsImprovement(int index, int delta) => delta > 0;

    private static string FormatStat(int index, WeaponStats stats)
        => index switch
        {
            0 => $"{stats.Damage:0}",
            1 => $"{stats.EffectiveRange:0} m",
            2 => $"{stats.Recoil:0.00}",
            3 => $"{stats.Handling:0.00}",
            4 => $"{60.0f / stats.FireInterval:0} RPM",
            5 => $"{stats.MagazineSize:0}",
            6 => $"{stats.SoundRadius:0} m",
            _ => string.Empty
        };

    private static string FormatDeltaStat(int index, WeaponStats current, WeaponStats baseline)
    {
        return index switch
        {
            0 => FormatDelta(current.Damage - baseline.Damage),
            1 => FormatDelta(current.EffectiveRange - baseline.EffectiveRange, " m"),
            2 => FormatDelta(current.Recoil - baseline.Recoil),
            3 => FormatDelta(current.Handling - baseline.Handling),
            4 => FormatDelta(60.0f / current.FireInterval - 60.0f / baseline.FireInterval, " RPM"),
            5 => FormatDelta(current.MagazineSize - baseline.MagazineSize),
            6 => FormatDelta(current.SoundRadius - baseline.SoundRadius, " m"),
            _ => "—"
        };
    }

    private static float DeltaStat(int index, WeaponStats current, WeaponStats baseline)
        => index switch
        {
            0 => current.Damage - baseline.Damage,
            1 => current.EffectiveRange - baseline.EffectiveRange,
            2 => current.Recoil - baseline.Recoil,
            3 => current.Handling - baseline.Handling,
            4 => 60.0f / current.FireInterval - 60.0f / baseline.FireInterval,
            5 => current.MagazineSize - baseline.MagazineSize,
            6 => current.SoundRadius - baseline.SoundRadius,
            _ => 0
        };
}
