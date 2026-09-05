using System;
using Godot;

namespace OperationSteelTide;

public partial class TrainingRangeArmoryView
{
    private void RefreshPresentation()
    {
        if (!UiReady || _refreshing)
        {
            return;
        }
        _refreshing = true;
        try
        {
            _kicker.Text = Text("training_armory_kicker", "LIVE FIRE CONTROL  //  ARMORY");
            _title.Text = Text("training_armory_title", "WEAPON ARMORY");
            _subtitle.Text = Text(
                "training_armory_subtitle",
                "SELECT A PLATFORM, PREVIEW THE AUTHORED MODEL, AND BUILD EVERY SLOT BEFORE DEPLOYMENT.");
            _platformLabel.Text = Text("training_armory_platform", "WEAPON PLATFORM");
            _attachmentTitle.Text = Text("training_armory_attachments", "ATTACHMENT STATION");
            _slotHint.Text = Text("training_armory_slot_hint", "SELECT A SLOT TO VIEW COMPATIBLE PARTS");
            _backButton.Text = Text("training_armory_back", "BACK");
            _applyButton.Text = Text("training_armory_apply", "APPLY LOADOUT");
            for (var index = 0; index < RangeWeapons.Length; index++)
            {
                _weaponList.SetItemText(index, DisplayWeaponName(RangeWeapons[index]));
            }
            _preview.Configure(InventoryPreviewKind.Rifle, weapon: _workingBuild);
            var definition = WeaponCatalog.Weapon(_workingBuild.Platform);
            _weaponName.Text = DisplayWeaponName(_workingBuild.Platform);
            var carry = definition.CarryClass == WeaponCarryClass.Sidearm
                ? Text("training_armory_sidearm", "SIDEARM")
                : Text("training_armory_primary", "PRIMARY");
            var fireMode = definition.SupportsAutomatic
                ? Text("training_armory_automatic", "AUTOMATIC")
                : Text("training_armory_semiautomatic", "SEMIAUTOMATIC");
            _weaponMeta.Text = $"{carry}  //  {fireMode}  //  {WeaponCatalog.AmmoDisplayName(definition.Caliber, _language)}";
            RefreshSlots();
            RefreshParts();
            RefreshStats();
            _summary.Text = $"{DisplayWeaponName(_workingBuild.Platform)}  //  {SlotSummary()}";
            _status.Text = Text("training_armory_status", "CHANGES ARE LOCAL UNTIL APPLY LOADOUT IS PRESSED.");
        }
        finally
        {
            _refreshing = false;
        }
    }

    private void RefreshSlots()
    {
        for (var index = 0; index < Slots.Length; index++)
        {
            var slot = Slots[index];
            var installed = _workingBuild.Attachments.TryGetValue(slot, out var id) ? id : string.Empty;
            _slotButtons[index].Text = $"{SlotName(slot)}\n{AttachmentName(installed)}";
            _slotButtons[index].ButtonPressed = slot == _selectedAttachmentSlot;
        }
        _selectedSlot.Text = $"{SlotName(_selectedAttachmentSlot)}  //  {Text("training_armory_installed", "INSTALLED")}: "
            + AttachmentName(_workingBuild.Attachments.TryGetValue(_selectedAttachmentSlot, out var current)
                ? current
                : string.Empty);
    }

    private void RefreshParts()
    {
        foreach (var child in _partList.GetChildren())
        {
            _partList.RemoveChild(child);
            child.QueueFree();
        }
        var candidates = Candidates(_workingBuild.Platform, _selectedAttachmentSlot);
        if (candidates.Count == 0)
        {
            var empty = new Label
            {
                Text = Text("training_armory_no_parts", "NO COMPATIBLE PARTS FOR THIS PLATFORM."),
                CustomMinimumSize = new Vector2(0, 54),
                VerticalAlignment = VerticalAlignment.Center
            };
            empty.AddThemeColorOverride("font_color", new Color(0.48f, 0.64f, 0.61f));
            _partList.AddChild(empty);
            return;
        }
        var installed = _workingBuild.Attachments.TryGetValue(_selectedAttachmentSlot, out var active)
            ? active
            : string.Empty;
        foreach (var id in candidates)
        {
            var card = _partCardScene?.Instantiate<TrainingRangeArmoryPartCard>();
            if (card is null)
            {
                continue;
            }
            _partList.AddChild(card);
            card.Configure(
                id,
                AttachmentName(id),
                id.Length == 0
                    ? Text("training_armory_none", "IRON SIGHTS / NONE")
                    : WeaponCatalog.Attachment(id).EffectDetail(_language),
                string.Equals(id, installed, StringComparison.OrdinalIgnoreCase),
                id.Length == 0 && !WeaponCatalog.CanDetachAttachment(_workingBuild.Platform, _selectedAttachmentSlot),
                _language);
            card.Chosen += HandlePartChosen;
        }
    }

    private void RefreshStats()
    {
        var current = _workingBuild.Stats();
        var baseline = new WeaponBuild { Platform = _workingBuild.Platform }.Stats();
        var names = new[]
        {
            Text("training_armory_damage", "DAMAGE"),
            Text("training_armory_range", "EFFECTIVE RANGE"),
            Text("training_armory_recoil", "RECOIL"),
            Text("training_armory_handling", "HANDLING"),
            Text("training_armory_rate", "FIRE RATE"),
            Text("training_armory_magazine", "MAGAZINE"),
            Text("training_armory_sound", "SOUND REPORT")
        };
        for (var index = 0; index < names.Length; index++)
        {
            _statNames[index].Text = names[index];
            _statValues[index].Text = FormatStat(index, current);
            _statDeltas[index].Text = FormatDeltaStat(index, current, baseline);
            var delta = DeltaStat(index, current, baseline);
            var color = MathF.Abs(delta) < 0.005f
                ? new Color(0.48f, 0.64f, 0.61f)
                : IsImprovement(index, delta)
                    ? new Color(0.3f, 0.95f, 0.7f)
                    : new Color(1.0f, 0.61f, 0.38f);
            _statDeltas[index].AddThemeColorOverride("font_color", color);
        }
    }

    private string SlotSummary()
    {
        var count = _workingBuild.Attachments.Count;
        return $"{count}/{Slots.Length} {Text("training_armory_slots", "SLOTS INSTALLED")}";
    }
}
