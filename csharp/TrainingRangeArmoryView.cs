using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

/// <summary>
/// Standalone, visual gunsmith surface for the live-fire range.
///
/// Inputs: <see cref="Configure"/> supplies a snapshot of the weapon build and
/// the requested language.  Optional translations can be supplied with
/// <see cref="SetTranslations"/> before or after Configure.
/// Outputs: ApplyRequested and BackRequested express user intent; the caller
/// reads CurrentBuild only when handling ApplyRequested.  Showing the view and
/// editing a card never mutates a player, inventory, or world directly.
/// </summary>
[GlobalClass]
public partial class TrainingRangeArmoryView : ColorRect
{
    [Signal] public delegate void ApplyRequestedEventHandler();
    [Signal] public delegate void BackRequestedEventHandler();

    public static readonly string[] RequiredLocalizationKeys =
    {
        "training_armory_kicker", "training_armory_title", "training_armory_subtitle",
        "training_armory_platform", "training_armory_preview", "training_armory_attachments",
        "training_armory_slot_hint", "training_armory_installed", "training_armory_none",
        "training_armory_fixed", "training_armory_no_parts", "training_armory_damage",
        "training_armory_range", "training_armory_recoil", "training_armory_handling",
        "training_armory_rate", "training_armory_magazine", "training_armory_sound",
        "training_armory_automatic", "training_armory_semiautomatic", "training_armory_apply",
        "training_armory_back"
    };

    internal static readonly WeaponPlatform[] RangeWeapons =
    {
        WeaponPlatform.M4A1, WeaponPlatform.AK74, WeaponPlatform.ScarL,
        WeaponPlatform.MP5A5, WeaponPlatform.M3A1, WeaponPlatform.VSS,
        WeaponPlatform.M24, WeaponPlatform.AXMC, WeaponPlatform.AWM,
        WeaponPlatform.P226, WeaponPlatform.M1911, WeaponPlatform.DesertEagle,
        WeaponPlatform.GSh18
    };

    internal static readonly AttachmentSlot[] Slots =
    {
        AttachmentSlot.Optic, AttachmentSlot.Barrel, AttachmentSlot.Muzzle,
        AttachmentSlot.Grip, AttachmentSlot.Stock, AttachmentSlot.Magazine
    };

    private ItemList _weaponList = null!;
    private InventoryModelPreview _preview = null!;
    private Label _kicker = null!;
    private Label _title = null!;
    private Label _subtitle = null!;
    private Label _platformLabel = null!;
    private Label _weaponName = null!;
    private Label _weaponMeta = null!;
    private Label _attachmentTitle = null!;
    private Label _slotHint = null!;
    private Label _selectedSlot = null!;
    private Label _summary = null!;
    private Label _status = null!;
    private Button _backButton = null!;
    private Button _applyButton = null!;
    private readonly Button[] _slotButtons = new Button[6];
    private readonly Label[] _statNames = new Label[7];
    private readonly Label[] _statValues = new Label[7];
    private readonly Label[] _statDeltas = new Label[7];
    private VBoxContainer _partList = null!;
    private PackedScene? _partCardScene;
    private ButtonGroup? _slotGroup;
    private WeaponBuild _workingBuild = WeaponCatalog.BuildTrainingRangeDefault(WeaponPlatform.M4A1);
    private readonly Dictionary<WeaponPlatform, WeaponBuild> _builds = new();
    private readonly Dictionary<string, string> _translations = new(StringComparer.OrdinalIgnoreCase);
    private string _language = "en";
    private AttachmentSlot _selectedAttachmentSlot = AttachmentSlot.Optic;
    private bool _ready;
    private bool _refreshing;

    /// <summary>Returns a defensive copy of the currently edited build.</summary>
    public WeaponBuild CurrentBuild => _workingBuild.Clone();

    public WeaponPlatform SelectedPlatform => _workingBuild.Platform;

    public bool UiReady
        => _ready
        && GodotObject.IsInstanceValid(_weaponList)
        && GodotObject.IsInstanceValid(_preview)
        && GodotObject.IsInstanceValid(_partList)
        && GodotObject.IsInstanceValid(_backButton)
        && GodotObject.IsInstanceValid(_applyButton)
        && Array.TrueForAll(_slotButtons, GodotObject.IsInstanceValid);

    /// <summary>True when all six slot buttons and the two intent buttons are bound.</summary>
    public bool IntentBindingsReady
        => UiReady
        && _weaponList.HasConnections(ItemList.SignalName.ItemSelected)
        && _backButton.HasConnections(BaseButton.SignalName.Pressed)
        && _applyButton.HasConnections(BaseButton.SignalName.Pressed)
        && Array.TrueForAll(_slotButtons, b => b.HasConnections(BaseButton.SignalName.Pressed));

    /// <summary>Configure a fresh working snapshot. This does not emit a signal or save state.</summary>
    public void Configure(WeaponBuild build, string language)
    {
        _language = GameLocalization.IsChinese(language) ? "zh" : "en";
        _workingBuild = WeaponCatalog.NormalizeBuild((build ?? WeaponCatalog.StarterWeapon()).Clone());
        _builds.Clear();
        _builds[_workingBuild.Platform] = _workingBuild.Clone();
        if (UiReady)
        {
            SelectWeaponRow(_workingBuild.Platform);
            RefreshPresentation();
        }
    }

    /// <summary>
    /// Supplies translated strings from the composition root. Missing keys fall
    /// back to GameLocalization, allowing this view to remain independent of the
    /// localization registry while still rendering Chinese in the shipped game.
    /// </summary>
    public void SetTranslations(IReadOnlyDictionary<string, string>? translations)
    {
        _translations.Clear();
        if (translations is not null)
        {
            foreach (var pair in translations)
            {
                if (!string.IsNullOrWhiteSpace(pair.Key))
                {
                    _translations[pair.Key] = pair.Value ?? string.Empty;
                }
            }
        }
        if (UiReady)
        {
            RefreshPresentation();
        }
    }

    public override void _Ready()
    {
        _slotGroup = new ButtonGroup { AllowUnpress = false };
        BindNodes();
        _partCardScene = ResourceLoader.Load<PackedScene>("res://ui/TrainingRangeArmoryPartCard.tscn");
        PopulateWeaponList();
        ConnectIntentSignals();
        _ready = true;
        SelectWeaponRow(_workingBuild.Platform);
        RefreshPresentation();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible || @event is not InputEventKey key || !key.Pressed || key.Echo)
        {
            return;
        }
        if (key.Keycode == Key.Escape)
        {
            EmitSignal(SignalName.BackRequested);
            GetViewport().SetInputAsHandled();
        }
    }

    public void SetLanguage(string language)
    {
        _language = GameLocalization.IsChinese(language) ? "zh" : "en";
        if (UiReady)
        {
            RefreshPresentation();
        }
    }

    public void SelectWeaponForDiagnostics(int index)
    {
        if (!UiReady || _weaponList.ItemCount == 0)
        {
            return;
        }
        _weaponList.Select(Mathf.Clamp(index, 0, _weaponList.ItemCount - 1));
        HandleWeaponSelected(_weaponList.GetSelectedItems()[0]);
    }

    public void SelectSlotForDiagnostics(int index)
    {
        if (!UiReady)
        {
            return;
        }
        var slotIndex = Mathf.Clamp(index, 0, Slots.Length - 1);
        _slotButtons[slotIndex].EmitSignal(BaseButton.SignalName.Pressed);
    }

    public void SelectAttachmentForDiagnostics(string attachmentId)
    {
        if (!UiReady)
        {
            return;
        }
        foreach (var child in _partList.GetChildren())
        {
            if (child is TrainingRangeArmoryPartCard card
                && string.Equals(card.AttachmentId, attachmentId ?? string.Empty, StringComparison.OrdinalIgnoreCase))
            {
                card.EmitSignal(TrainingRangeArmoryPartCard.SignalName.Chosen, card.AttachmentId);
                return;
            }
        }
    }

    public void PressApplyForDiagnostics() => _applyButton.EmitSignal(BaseButton.SignalName.Pressed);
    public void PressBackForDiagnostics() => _backButton.EmitSignal(BaseButton.SignalName.Pressed);

    private void BindNodes()
    {
        var panel = GetNode<Control>("Panel");
        _kicker = panel.GetNode<Label>("Kicker");
        _title = panel.GetNode<Label>("Title");
        _subtitle = panel.GetNode<Label>("Subtitle");
        _weaponList = panel.GetNode<ItemList>("Content/WeaponPanel/WeaponList");
        var previewPanel = panel.GetNode<Control>("Content/PreviewPanel");
        _preview = previewPanel.GetNode<InventoryModelPreview>("Preview");
        _platformLabel = previewPanel.GetNode<Label>("PlatformLabel");
        _weaponName = previewPanel.GetNode<Label>("WeaponName");
        _weaponMeta = previewPanel.GetNode<Label>("WeaponMeta");
        var stats = previewPanel.GetNode<Control>("Stats");
        for (var index = 0; index < _statNames.Length; index++)
        {
            _statNames[index] = stats.GetNode<Label>($"Stat{index}/Name");
            _statValues[index] = stats.GetNode<Label>($"Stat{index}/Value");
            _statDeltas[index] = stats.GetNode<Label>($"Stat{index}/Delta");
        }
        var attachmentPanel = panel.GetNode<Control>("Content/AttachmentPanel");
        _attachmentTitle = attachmentPanel.GetNode<Label>("Title");
        _slotHint = attachmentPanel.GetNode<Label>("SlotHint");
        for (var index = 0; index < _slotButtons.Length; index++)
        {
            _slotButtons[index] = attachmentPanel.GetNode<Button>($"Slot{index}");
            _slotButtons[index].ButtonGroup = _slotGroup;
        }
        _selectedSlot = attachmentPanel.GetNode<Label>("SelectedSlot");
        _partList = attachmentPanel.GetNode<VBoxContainer>("PartsScroll/PartsList");
        _summary = panel.GetNode<Label>("Summary");
        _status = panel.GetNode<Label>("Status");
        _backButton = panel.GetNode<Button>("BackButton");
        _applyButton = panel.GetNode<Button>("ApplyButton");
    }

    private void PopulateWeaponList()
    {
        _weaponList.Clear();
        foreach (var platform in RangeWeapons)
        {
            var definition = WeaponCatalog.Weapon(platform);
            var index = _weaponList.AddItem(DisplayWeaponName(platform));
            _weaponList.SetItemMetadata(index, (int)platform);
        }
    }

    private void ConnectIntentSignals()
    {
        _weaponList.ItemSelected += HandleWeaponSelected;
        for (var index = 0; index < _slotButtons.Length; index++)
        {
            var slotIndex = index;
            _slotButtons[index].Pressed += () => SelectSlot(Slots[slotIndex]);
        }
        _backButton.Pressed += () => EmitSignal(SignalName.BackRequested);
        _applyButton.Pressed += () => EmitSignal(SignalName.ApplyRequested);
    }

    private void HandleWeaponSelected(long index)
    {
        if (_refreshing || index < 0 || index >= RangeWeapons.Length)
        {
            return;
        }
        _builds[_workingBuild.Platform] = _workingBuild.Clone();
        var platform = RangeWeapons[index];
        _workingBuild = _builds.TryGetValue(platform, out var saved)
            ? saved.Clone()
            : WeaponCatalog.BuildTrainingRangeDefault(platform);
        _workingBuild = WeaponCatalog.NormalizeBuild(_workingBuild);
        _selectedAttachmentSlot = FirstSupportedSlot(platform);
        RefreshPresentation();
    }

    private void SelectWeaponRow(WeaponPlatform platform)
    {
        var index = Array.IndexOf(RangeWeapons, platform);
        if (index >= 0 && index < _weaponList.ItemCount)
        {
            _weaponList.Select(index);
        }
    }

    private void SelectSlot(AttachmentSlot slot)
    {
        _selectedAttachmentSlot = slot;
        RefreshPresentation();
    }

    private void HandlePartChosen(string attachmentId)
    {
        if (_refreshing)
        {
            return;
        }
        if (attachmentId.Length == 0)
        {
            if (WeaponCatalog.CanDetachAttachment(_workingBuild.Platform, _selectedAttachmentSlot))
            {
                _workingBuild.Attachments.Remove(_selectedAttachmentSlot);
            }
        }
        else if (WeaponCatalog.TryAttachment(attachmentId, out var part)
            && part.Slot == _selectedAttachmentSlot
            && IsCandidate(_workingBuild.Platform, _selectedAttachmentSlot, attachmentId))
        {
            _workingBuild.Attachments[_selectedAttachmentSlot] = attachmentId;
        }
        _workingBuild = WeaponCatalog.NormalizeBuild(_workingBuild);
        _builds[_workingBuild.Platform] = _workingBuild.Clone();
        RefreshPresentation();
    }

    private string DisplayWeaponName(WeaponPlatform platform)
    {
        var definition = WeaponCatalog.Weapon(platform);
        return _language == "zh"
            ? GameLocalization.Get(definition.LocalizationKey, _language, definition.ChineseName)
            : definition.Name;
    }

    private string Text(string key, string fallback)
    {
        if (_translations.TryGetValue(key, out var translated) && translated.Length > 0)
        {
            return translated;
        }
        return GameLocalization.Get(key, _language, fallback);
    }
}
