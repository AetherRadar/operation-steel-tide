using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public partial class CombatHUD
{
    private readonly Dictionary<string, Button> _deploymentWeaponButtons = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Button> _deploymentArmorButtons = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<LootGrade, Button> _deploymentAmmoButtons = new();
    private Label _deploymentCreditsLabel = null!;
    private Label _deploymentExtractedLabel = null!;
    private Label _deploymentCostLabel = null!;
    private Label _deploymentErrorLabel = null!;
    private Label _deploymentWeaponCaption = null!;
    private Label _deploymentArmorCaption = null!;
    private Label _deploymentAmmoCaption = null!;
    private OperatorProfileData _displayedProfile = new();
    private string _selectedWeaponId = "m4a1";
    private string _selectedArmorId = "standard";
    private LootGrade _selectedAmmoGrade = LootGrade.Uncommon;

    public DeploymentLoadoutSelection SelectedDeploymentLoadout
        => new(_selectedWeaponId, _selectedArmorId, _selectedAmmoGrade);

    private void BuildDeploymentStore(Control panel)
    {
        var band = new ColorRect
        {
            Position = new Vector2(22, 416),
            Size = new Vector2(996, 184),
            Color = new Color(0.012f, 0.021f, 0.023f, 0.96f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        panel.AddChild(band);
        band.AddChild(new ColorRect
        {
            Size = new Vector2(3, 184),
            Color = new Color(0.96f, 0.69f, 0.2f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        });

        _deploymentCreditsLabel = Label("BALANCE  18000", 14, new Color(1.0f, 0.76f, 0.25f));
        _deploymentCreditsLabel.Position = new Vector2(18, 10);
        _deploymentCreditsLabel.Size = new Vector2(270, 22);
        band.AddChild(_deploymentCreditsLabel);
        _deploymentExtractedLabel = Label("LIFETIME EXTRACTED  0", 11, new Color(0.54f, 0.7f, 0.65f));
        _deploymentExtractedLabel.Position = new Vector2(286, 12);
        _deploymentExtractedLabel.Size = new Vector2(300, 20);
        band.AddChild(_deploymentExtractedLabel);
        _deploymentCostLabel = Label("DEPLOY COST  5100", 14, new Color(0.72f, 0.94f, 0.84f));
        _deploymentCostLabel.Position = new Vector2(690, 10);
        _deploymentCostLabel.Size = new Vector2(286, 22);
        _deploymentCostLabel.HorizontalAlignment = HorizontalAlignment.Right;
        band.AddChild(_deploymentCostLabel);

        _deploymentWeaponCaption = DeploymentCaption("PRIMARY", 44);
        band.AddChild(_deploymentWeaponCaption);
        var weaponGroup = new ButtonGroup();
        for (var index = 0; index < DeploymentCatalog.Weapons.Count; index++)
        {
            var offer = DeploymentCatalog.Weapons[index];
            var button = DeploymentSegment(new Vector2(122 + index * 211, 38), new Vector2(202, 36));
            button.ToggleMode = true;
            button.ButtonGroup = weaponGroup;
            button.Pressed += () =>
            {
                _selectedWeaponId = offer.Id;
                _deploymentErrorLabel.Text = string.Empty;
                RefreshDeploymentStore();
            };
            band.AddChild(button);
            _deploymentWeaponButtons[offer.Id] = button;
        }

        _deploymentArmorCaption = DeploymentCaption("ARMOR", 87);
        band.AddChild(_deploymentArmorCaption);
        var armorGroup = new ButtonGroup();
        for (var index = 0; index < DeploymentCatalog.Armor.Count; index++)
        {
            var offer = DeploymentCatalog.Armor[index];
            var button = DeploymentSegment(new Vector2(122 + index * 260, 81), new Vector2(250, 36));
            button.ToggleMode = true;
            button.ButtonGroup = armorGroup;
            button.Pressed += () =>
            {
                _selectedArmorId = offer.Id;
                _deploymentErrorLabel.Text = string.Empty;
                RefreshDeploymentStore();
            };
            band.AddChild(button);
            _deploymentArmorButtons[offer.Id] = button;
        }

        _deploymentAmmoCaption = DeploymentCaption("AMMO", 130);
        band.AddChild(_deploymentAmmoCaption);
        var ammoGroup = new ButtonGroup();
        foreach (var grade in Enum.GetValues<LootGrade>())
        {
            var button = DeploymentSegment(new Vector2(122 + (int)grade * 112, 124), new Vector2(104, 36));
            button.ToggleMode = true;
            button.ButtonGroup = ammoGroup;
            button.AddThemeColorOverride("font_pressed_color", AmmoTiers.Color(grade));
            button.Pressed += () =>
            {
                _selectedAmmoGrade = grade;
                _deploymentErrorLabel.Text = string.Empty;
                RefreshDeploymentStore();
            };
            band.AddChild(button);
            _deploymentAmmoButtons[grade] = button;
        }

        _deploymentErrorLabel = Label(string.Empty, 11, new Color(1.0f, 0.42f, 0.25f));
        _deploymentErrorLabel.Position = new Vector2(696, 84);
        _deploymentErrorLabel.Size = new Vector2(280, 74);
        _deploymentErrorLabel.HorizontalAlignment = HorizontalAlignment.Right;
        _deploymentErrorLabel.VerticalAlignment = VerticalAlignment.Center;
        _deploymentErrorLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        band.AddChild(_deploymentErrorLabel);
        RefreshDeploymentStore();
    }

    private static Label DeploymentCaption(string text, float y)
    {
        var label = Label(text, 11, new Color(0.43f, 0.62f, 0.58f));
        label.Position = new Vector2(18, y);
        label.Size = new Vector2(92, 24);
        label.VerticalAlignment = VerticalAlignment.Center;
        return label;
    }

    private static Button DeploymentSegment(Vector2 position, Vector2 size)
    {
        var button = Button(string.Empty, position, size);
        button.FocusMode = Control.FocusModeEnum.None;
        button.AddThemeFontSizeOverride("font_size", 11);
        return button;
    }

    public void SetOperatorProfile(OperatorProfileData profile)
    {
        _displayedProfile = profile.Clone();
        _selectedWeaponId = DeploymentCatalog.Weapon(profile.LastWeaponId).Id;
        _selectedArmorId = DeploymentCatalog.ArmorKit(profile.LastArmorId).Id;
        _selectedAmmoGrade = profile.LastAmmoGrade;
        RefreshDeploymentStore();
    }

    public void ShowDeploymentPurchaseError(string reason)
    {
        _deploymentErrorLabel.Text = reason switch
        {
            "insufficient_credits" => Text("loadout_insufficient", "INSUFFICIENT BALANCE  //  SELECT A CHEAPER KIT"),
            _ => Text("loadout_save_failed", "PROFILE SAVE FAILED  //  DEPLOYMENT CANCELLED")
        };
    }

    private void RefreshDeploymentStore()
    {
        if (!IsInstanceValid(_deploymentCreditsLabel))
        {
            return;
        }
        var selected = DeploymentCatalog.Resolve(SelectedDeploymentLoadout);
        var chinese = GameLocalization.IsChinese(_language);
        _deploymentCreditsLabel.Text = chinese
            ? $"\u4f59\u989d  {_displayedProfile.Credits}"
            : $"BALANCE  {_displayedProfile.Credits}";
        _deploymentExtractedLabel.Text = chinese
            ? $"\u5386\u53f2\u64a4\u79bb\u4ef7\u503c  {_displayedProfile.LifetimeExtractedValue}"
            : $"LIFETIME EXTRACTED  {_displayedProfile.LifetimeExtractedValue}";
        _deploymentCostLabel.Text = chinese
            ? $"\u672c\u5c40\u6574\u5907  {selected.TotalCost}"
            : $"DEPLOY COST  {selected.TotalCost}";
        _deploymentCostLabel.AddThemeColorOverride(
            "font_color",
            selected.TotalCost <= _displayedProfile.Credits
                ? new Color(0.72f, 0.94f, 0.84f)
                : new Color(1.0f, 0.34f, 0.22f));

        foreach (var offer in DeploymentCatalog.Weapons)
        {
            var button = _deploymentWeaponButtons[offer.Id];
            button.SetPressedNoSignal(string.Equals(offer.Id, _selectedWeaponId, StringComparison.OrdinalIgnoreCase));
            button.Text = $"{Text(offer.LocalizationKey, offer.EnglishName)}  {offer.Price}";
        }
        foreach (var offer in DeploymentCatalog.Armor)
        {
            var button = _deploymentArmorButtons[offer.Id];
            button.SetPressedNoSignal(string.Equals(offer.Id, _selectedArmorId, StringComparison.OrdinalIgnoreCase));
            button.Text = $"{Text(offer.LocalizationKey, offer.EnglishName)}  {offer.Price}";
        }
        foreach (var grade in Enum.GetValues<LootGrade>())
        {
            var button = _deploymentAmmoButtons[grade];
            button.SetPressedNoSignal(grade == _selectedAmmoGrade);
            button.Text = $"T{(int)grade + 1}  {DeploymentCatalog.AmmoPrice(grade)}";
            button.TooltipText = AmmoTiers.DisplayName(grade, _language);
        }
    }

    private void RefreshDeploymentLanguage()
    {
        if (!IsInstanceValid(_deploymentWeaponCaption))
        {
            return;
        }
        var chinese = GameLocalization.IsChinese(_language);
        _deploymentWeaponCaption.Text = chinese ? "\u4e3b\u6b66\u5668" : "PRIMARY";
        _deploymentArmorCaption.Text = chinese ? "\u62a4\u7532" : "ARMOR";
        _deploymentAmmoCaption.Text = chinese ? "\u5f39\u836f" : "AMMO";
        RefreshDeploymentStore();
    }
}
