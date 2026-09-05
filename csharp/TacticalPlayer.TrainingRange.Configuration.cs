using Godot;

namespace OperationSteelTide;

public partial class TacticalPlayer
{
    private int _trainingRangeAmmoType;
    private int _trainingRangeAmmoLevel = 2;

    public int TrainingRangeAmmoType => _trainingRangeAmmoType;
    public int TrainingRangeAmmoLevel => _trainingRangeAmmoLevel;
    public LootGrade TrainingRangeAmmoGrade => _trainingRangeAmmoGrade;

    /// <summary>Round behavior used by the live-fire range's four ammo selectors.</summary>
    public float TrainingRangeAmmoDamageMultiplier
        => Main?.IsTrainingRangeActive != true
            ? 1.0f
            : _trainingRangeAmmoType switch
            {
                2 => 1.10f, // hollow point
                1 => 0.96f, // armor piercing trades a little raw damage for penetration
                _ => 1.0f
            };

    public float TrainingRangeAmmoPenetrationBonus
        => Main?.IsTrainingRangeActive == true && _trainingRangeAmmoType == 1
            ? 0.24f
            : Main?.IsTrainingRangeActive == true && _trainingRangeAmmoType == 2
                ? -0.12f
                : 0.0f;

    public bool TrainingRangeTracerRounds
        => Main?.IsTrainingRangeActive == true && _trainingRangeAmmoType == 3;

    public static WeaponPlatform TrainingRangeWeaponAt(int index)
        => TrainingRangeWeapons[Mathf.Clamp(index, 0, TrainingRangeWeapons.Length - 1)];

    /// <summary>Apply a setup-panel weapon selection without touching mission inventory.</summary>
    public void SelectTrainingRangeWeapon(int index)
    {
        _trainingRangeWeaponIndex = Mathf.Clamp(index, 0, TrainingRangeWeapons.Length - 1);
        InstallTrainingRangeWeapon(TrainingRangeWeapons[_trainingRangeWeaponIndex], notify: true);
        ApplyTrainingRangeAmmoProfile(_trainingRangeAmmoType, _trainingRangeAmmoLevel);
    }

    /// <summary>
    /// Applies the range's round selector to the production ammo-grade path.  The base
    /// weapon system models ammunition as caliber + grade; the four range round types
    /// therefore map to a grade offset so AP/HP/tracer choices have real shot effects
    /// instead of being cosmetic labels.
    /// </summary>
    public void ApplyTrainingRangeAmmoProfile(int ammoType, int ammoLevel)
    {
        _trainingRangeAmmoType = Mathf.Clamp(ammoType, 0, 3);
        _trainingRangeAmmoLevel = Mathf.Clamp(ammoLevel, 0, 3);
        var gradeValue = Mathf.Clamp(
            _trainingRangeAmmoLevel + (_trainingRangeAmmoType == 1 ? 1 : 0),
            (int)LootGrade.Common,
            (int)LootGrade.Legendary);
        _trainingRangeAmmoGrade = (LootGrade)gradeValue;
        _loadedAmmoGrade = _trainingRangeAmmoGrade;
        _primaryLoadedAmmoGrade = _trainingRangeAmmoGrade;
        _secondaryLoadedAmmoGrade = _trainingRangeAmmoGrade;
        _sidearmLoadedAmmoGrade = _trainingRangeAmmoGrade;
        if (IsFirearmQuickSlotSelected && !_knifeEquipped)
        {
            Ammo = EquippedWeapon.Stats().MagazineSize;
            SetAmmoReserve(CurrentAmmoCaliber, _trainingRangeAmmoGrade, 9999);
        }
        Hud?.SetAmmoTier(_trainingRangeAmmoGrade);
        PushHudStats();
        // Weapon selection runs immediately before this method during deploy;
        // emit the attachment legend last so the station's weapon-status toast
        // cannot hide the controls the player needs in the live-fire lane.
        ShowTrainingRangeAttachmentControls();
    }

    /// <summary>Reset selected ammo and all firearm reserves after a player reset.</summary>
    public void ResetTrainingRangeAmmoProfile()
    {
        _trainingRangeAmmoType = 0;
        _trainingRangeAmmoLevel = 2;
        _trainingRangeAmmoGrade = LootGrade.Rare;
        ApplyTrainingRangeAmmoProfile(_trainingRangeAmmoType, _trainingRangeAmmoLevel);
    }
}
