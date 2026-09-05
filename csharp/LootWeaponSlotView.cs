using System;
using Godot;

namespace OperationSteelTide;

/// <summary>
/// Renders one weapon slot snapshot and forwards detail/drop intent without mutating inventory state.
/// </summary>
[GlobalClass]
public partial class LootWeaponSlotView : LootDropZone
{
    private Label _caption = null!;
    private Button _detailsButton = null!;
    private Button _detachOpticButton = null!;
    private InventoryModelPreview _preview = null!;
    private Label _weaponLabel = null!;
    private string _language = "en";
    private string _slotLocalizationKey = "primary_weapon";
    private string _slotEnglish = "PRIMARY WEAPON";
    private WeaponBuild? _weapon;
    private LootGrade _grade;
    private bool _configured;

    public event Action? DetailsRequested;
    public event Action? OpticDetachRequested;

    public bool UiReady
        => IsInstanceValid(_caption)
        && IsInstanceValid(_detailsButton)
        && IsInstanceValid(_detachOpticButton)
        && IsInstanceValid(_preview)
        && IsInstanceValid(_weaponLabel);

    public bool HasWeapon => _weapon is not null;
    public bool CanDetachOptic
        => _weapon is not null
        && _weapon.Attachments.ContainsKey(AttachmentSlot.Optic)
        && WeaponCatalog.CanDetachAttachment(_weapon.Platform, AttachmentSlot.Optic)
        && IsInstanceValid(_detachOpticButton)
        && _detachOpticButton.Visible;
    public WeaponPlatform? Platform => _weapon?.Platform;
    public string CaptionText => IsInstanceValid(_caption) ? _caption.Text : string.Empty;
    public bool EmptyCaptionHasNoGrade
        => _weapon is null
        && IsInstanceValid(_caption)
        && string.Equals(_caption.Text, SlotName(), StringComparison.Ordinal);

    public bool QualityColorMatchesGrade
    {
        get
        {
            if (_weapon is null)
            {
                return EmptyCaptionHasNoGrade;
            }
            var expected = new Color(LootGrades.GlowColor(_grade), 0.75f);
            return GetThemeStylebox("panel") is StyleBoxFlat style
                && ColorsMatch(style.BorderColor, expected);
        }
    }

    public override void _Ready()
    {
        Target = LootDropTarget.PrimaryWeapon;
        _caption = GetNode<Label>("Margin/Content/Header/Caption");
        _detailsButton = GetNode<Button>("Margin/Content/Header/DetailsButton");
        _detachOpticButton = GetNode<Button>("Margin/Content/Header/DetachOpticButton");
        _preview = GetNode<InventoryModelPreview>("Margin/Content/Preview");
        _weaponLabel = GetNode<Label>("Margin/Content/WeaponLabel");
        _detailsButton.Pressed += () => DetailsRequested?.Invoke();
        _detachOpticButton.Pressed += () => OpticDetachRequested?.Invoke();
        ApplyPresentation();
    }

    /// <summary>Supplies localized slot metadata and the weapon snapshot rendered by this view.</summary>
    public void SetWeapon(
        string language,
        string slotLocalizationKey,
        string slotEnglish,
        WeaponBuild? weapon,
        LootGrade grade)
    {
        _language = GameLocalization.IsChinese(language) ? "zh" : "en";
        _slotLocalizationKey = slotLocalizationKey;
        _slotEnglish = slotEnglish;
        _weapon = weapon?.Clone();
        _grade = grade;
        _configured = true;
        if (IsNodeReady())
        {
            ApplyPresentation();
        }
    }

    public void PressDetailsForDiagnostics()
    {
        if (IsInstanceValid(_detailsButton) && _detailsButton.Visible)
        {
            _detailsButton.EmitSignal(BaseButton.SignalName.Pressed);
        }
    }

    public void PressDetachOpticForDiagnostics()
    {
        if (IsInstanceValid(_detachOpticButton) && _detachOpticButton.Visible)
        {
            _detachOpticButton.EmitSignal(BaseButton.SignalName.Pressed);
        }
    }

    private void ApplyPresentation()
    {
        if (!UiReady)
        {
            return;
        }

        var slotName = SlotName();
        _detailsButton.TooltipText = GameLocalization.Get("weapon_details", _language, "WEAPON DETAILS");
        _detachOpticButton.Text = GameLocalization.Get("detach_optic", _language, "DETACH");
        _detachOpticButton.TooltipText = GameLocalization.Get(
            "detach_optic",
            _language,
            "DETACH OPTIC TO BACKPACK");
        if (!_configured || _weapon is null)
        {
            var emptyColor = new Color(0.48f, 0.54f, 0.52f);
            AddThemeStyleboxOverride("panel", SlotStyle(emptyColor));
            _caption.Text = slotName;
            _caption.AddThemeColorOverride("font_color", emptyColor);
            _preview.Visible = false;
            _detailsButton.Visible = false;
            _detachOpticButton.Visible = false;
            _weaponLabel.Text = GameLocalization.Get("empty_slot", _language, "EMPTY SLOT");
            _weaponLabel.TooltipText = _weaponLabel.Text;
            return;
        }

        var color = LootGrades.GlowColor(_grade);
        var stats = _weapon.Stats();
        var weaponName = _weapon.DisplayName(_language);
        AddThemeStyleboxOverride("panel", SlotStyle(color));
        _caption.Text = $"{slotName}  //  {LootGrades.DisplayName(_grade, _language)}";
        _caption.AddThemeColorOverride("font_color", color);
        _preview.Visible = true;
        _preview.Configure(InventoryPreviewKind.Rifle, weapon: _weapon);
        _detailsButton.Visible = true;
        _detachOpticButton.Visible = _weapon.Attachments.ContainsKey(AttachmentSlot.Optic)
            && WeaponCatalog.CanDetachAttachment(_weapon.Platform, AttachmentSlot.Optic);
        _weaponLabel.Text = GameLocalization.IsChinese(_language)
            ? $"{weaponName}  //  \u5f39\u5323 {stats.MagazineSize}"
            : $"{weaponName}  //  MAG {stats.MagazineSize}";
        _weaponLabel.TooltipText = _weaponLabel.Text;
    }

    private string SlotName()
        => GameLocalization.Get(_slotLocalizationKey, _language, _slotEnglish);

    private static StyleBoxFlat SlotStyle(Color accent)
    {
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.035f, 0.047f, 0.047f, 0.9f),
            BorderColor = new Color(accent, 0.75f)
        };
        style.SetBorderWidthAll(1);
        style.SetCornerRadiusAll(3);
        return style;
    }

    private static bool ColorsMatch(Color actual, Color expected)
        => Mathf.IsEqualApprox(actual.R, expected.R)
        && Mathf.IsEqualApprox(actual.G, expected.G)
        && Mathf.IsEqualApprox(actual.B, expected.B)
        && Mathf.IsEqualApprox(actual.A, expected.A);
}
